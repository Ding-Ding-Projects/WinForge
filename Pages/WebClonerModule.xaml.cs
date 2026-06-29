using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 網站複製器 · Website Cloner — point it at a URL, pick a folder, and it fetches the page,
/// downloads its assets, rewrites links to local paths and writes a browsable index.html.
/// 以原生 HttpClient/WebView2 工作，不會啟動瀏覽器、檔案總管或終端機代理。Bilingual.
/// </summary>
public sealed partial class WebClonerModule : Page
{
    private CancellationTokenSource? _cts;
    private string? _lastIndexPath;
    private bool _webReady;
    private string? _aiAgentName;

    public WebClonerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) =>
        {
            Render();
            FolderBox.Text = DisplayPath(DefaultDest());
            await DetectAgentAsync();
        };
    }

    /// <summary>背景偵測有冇可用代理，更新提示 · Detect an available agent off the UI thread, refresh the hint.</summary>
    private async Task DetectAgentAsync()
    {
        try
        {
            var agent = await Task.Run(() => WebClonerAiService.FindAvailableAgentAsync());
            _aiAgentName = agent?.Name;
        }
        catch (Exception ex)
        {
            CrashLogger.Log("WebClonerModule.DetectAgent", ex);
            _aiAgentName = null;
        }
        try { Render(); } catch (Exception ex) { CrashLogger.Log("WebClonerModule.DetectAgent.Render", ex); }
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private static string DefaultDest()
    {
        try
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return System.IO.Path.Combine(docs, "WinForge Clones");
        }
        catch { return ""; }
    }

    private static string DisplayPath(string path)
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
            if (!string.IsNullOrWhiteSpace(home) &&
                path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                return "%USERPROFILE%" + path[home.Length..];
        }
        catch { }
        return path;
    }

    private static string ExpandDisplayPath(string path)
    {
        if (path.StartsWith("%USERPROFILE%", StringComparison.OrdinalIgnoreCase))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
            return home + path["%USERPROFILE%".Length..];
        }
        return Environment.ExpandEnvironmentVariables(path);
    }

    private void Render()
    {
        HeaderTitle.Text = "Website Cloner · 網站複製器";
        HeaderBlurb.Text = P(
            "Fetch a live web page, download its assets and save a browsable local copy. Preview the result inside WinForge without opening an external browser or folder.",
            "下載一個網頁、攞埋佢嘅資源，儲存成可以喺本機瀏覽嘅副本，並喺 WinForge 入面預覽，唔會開外部瀏覽器或資料夾。");

        DisclaimerBar.Title = P("For personal & learning use only", "只供個人及學習用途");
        DisclaimerBar.Message = P(
            "Cloning a site you don't own may breach copyright or terms of service. Only clone pages you have the right to copy.",
            "複製唔屬於你嘅網站可能侵犯版權或者違反服務條款。請只複製你有權複製嘅頁面。");

        UrlLabel.Text = P("Website URL", "網站網址");
        FolderLabel.Text = P("Destination folder", "目的資料夾");
        FolderBtn.Content = P("Browse…", "瀏覽…");

        OptionsLabel.Text = P("Options", "選項");
        AssetsCheck.Content = P("Download assets (images, CSS, JS, fonts) and rewrite links",
            "下載資源（圖片、CSS、JS、字型）並改寫連結");
        RenderedCheck.Content = P("Capture JS-rendered DOM via WebView2 (better for dynamic sites)",
            "用 WebView2 擷取 JS 渲染後嘅 DOM（適合動態網站）");
        AiCheck.Content = P("AI reconstruction: clean up the HTML/CSS/JS with an installed coding agent",
            "AI 重建：用已安裝嘅編程代理整靚 HTML／CSS／JS");
        AiHint.Text = _aiAgentName is null
            ? P("No AI coding agent detected — this step will be skipped (the native clone still works).",
                "未偵測到 AI 編程代理 — 呢一步會略過（原生複製照樣可用）。")
            : P($"Detected agent: {_aiAgentName}. Output is written to an ai/ sub-folder.",
                $"偵測到代理：{_aiAgentName}。輸出會寫入 ai/ 子資料夾。");

        CloneBtn.Content = P("Clone website", "複製網站");
        CancelBtn.Content = P("Cancel", "取消");
        CopyFolderBtn.Content = P("Copy folder path", "複製資料夾路徑");
        PreviewBtn.Content = P("Preview in app", "喺 app 預覽");
        PreviewTitle.Text = P("Local clone preview", "本機副本預覽");

        TokenTitle.Text = P("Extracted design tokens", "抽取到嘅設計符記");
        LogTitle.Text = P("Progress log", "進度記錄");
    }

    // ===================== UI actions =====================

    private async void Folder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose where to save the clone", "揀儲存複製品嘅位置"));
        if (folder is not null) FolderBox.Text = DisplayPath(folder);
    }

    private async void Clone_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text?.Trim() ?? "";
        var folder = ExpandDisplayPath(FolderBox.Text?.Trim() ?? "");

        if (string.IsNullOrWhiteSpace(url)) { ShowResult(false, P("Enter a URL first.", "請先輸入網址。")); return; }
        if (string.IsNullOrWhiteSpace(folder)) { ShowResult(false, P("Choose a destination folder first.", "請先揀目的資料夾。")); return; }

        SetBusy(true);
        LogPanel.Children.Clear();
        ResultBar.IsOpen = false;
        TokenCard.Visibility = Visibility.Collapsed;
        _cts = new CancellationTokenSource();

        var progress = new Progress<WebsiteClonerService.Progress>(AppendLog);

        try
        {
            // Optional rendered-DOM capture via WebView2.
            string? rawHtml = null;
            if (RenderedCheck.IsChecked == true)
            {
                AppendLog(new WebsiteClonerService.Progress(
                    "Rendering page in WebView2…", "用 WebView2 渲染頁面…",
                    WebsiteClonerService.LogLevel.Info));
                rawHtml = await CaptureRenderedDomAsync(url, _cts.Token);
                if (rawHtml is null)
                    AppendLog(new WebsiteClonerService.Progress(
                        "WebView2 capture failed — falling back to HttpClient.",
                        "WebView2 擷取失敗 — 改用 HttpClient。", WebsiteClonerService.LogLevel.Warn));
            }

            var result = await WebsiteClonerService.CloneAsync(
                url, folder, AssetsCheck.IsChecked == true, progress, _cts.Token, rawHtml);

            if (!result.Success)
            {
                ShowResult(false, result.Message.Get(Loc.I.Language));
                return;
            }

            _lastIndexPath = result.IndexPath;
            CopyFolderBtn.IsEnabled = true;
            PreviewBtn.IsEnabled = result.IndexPath is not null;

            if (result.DesignTokens is { Count: > 0 })
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in result.DesignTokens)
                    sb.AppendLine($"{kv.Key}: {kv.Value}");
                TokenText.Text = sb.ToString().TrimEnd();
                TokenCard.Visibility = Visibility.Visible;
            }

            // Optional AI reconstruction pass — best-effort, never breaks the native result.
            if (AiCheck.IsChecked == true)
            {
                var ai = await WebClonerAiService.ReconstructAsync(folder, progress, _cts.Token);
                AppendLog(new WebsiteClonerService.Progress(
                    ai.Message.En, ai.Message.Zh,
                    ai.Success ? WebsiteClonerService.LogLevel.Ok
                    : ai.Available ? WebsiteClonerService.LogLevel.Warn
                    : WebsiteClonerService.LogLevel.Info));

                if (ai.Success && ai.OutputPath is not null &&
                    System.IO.File.Exists(ai.OutputPath))
                {
                    _lastIndexPath = ai.OutputPath;  // preview the cleaned AI index instead.
                }
            }

            ShowResult(true, result.Message.Get(Loc.I.Language));
        }
        catch (OperationCanceledException)
        {
            ShowResult(false, P("Cancelled.", "已取消。"));
        }
        catch (Exception ex)
        {
            ShowResult(false, ex.Message);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void CopyFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = ExpandDisplayPath(FolderBox.Text?.Trim() ?? "");
        if (string.IsNullOrWhiteSpace(folder)) return;
        var package = new DataPackage();
        package.SetText(folder);
        Clipboard.SetContent(package);
        ShowResult(true, P("Folder path copied to clipboard.", "資料夾路徑已複製到剪貼簿。"));
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastIndexPath) || !System.IO.File.Exists(_lastIndexPath)) return;
        try
        {
            PreviewCard.Visibility = Visibility.Visible;
            await PreviewWeb.EnsureCoreWebView2Async();
            PreviewWeb.CoreWebView2.Navigate(new Uri(_lastIndexPath).AbsoluteUri);
        }
        catch (Exception ex)
        {
            ShowResult(false, ex.Message);
        }
    }

    // ===================== WebView2 rendered-DOM capture =====================

    private async Task<string?> CaptureRenderedDomAsync(string url, CancellationToken ct)
    {
        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

            if (!_webReady)
            {
                await HiddenWeb.EnsureCoreWebView2Async();
                _webReady = true;
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void Handler(Microsoft.UI.Xaml.Controls.WebView2 s,
                Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs a)
                => tcs.TrySetResult(a.IsSuccess);

            HiddenWeb.NavigationCompleted += Handler;
            try
            {
                HiddenWeb.CoreWebView2.Navigate(uri.AbsoluteUri);
                // Wait for navigation (cap at 30s) — honour cancellation.
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(30));
                using (timeout.Token.Register(() => tcs.TrySetResult(false)))
                    await tcs.Task;
            }
            finally { HiddenWeb.NavigationCompleted -= Handler; }

            // Give late scripts a moment, then dump the DOM.
            await Task.Delay(1500, ct);
            var json = await HiddenWeb.CoreWebView2.ExecuteScriptAsync(
                "document.documentElement.outerHTML");
            return UnwrapJsString(json);
        }
        catch { return null; }
    }

    /// <summary>ExecuteScriptAsync 回傳 JSON 字串字面量，要解封 · ExecuteScriptAsync returns a JSON string literal.</summary>
    private static string? UnwrapJsString(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "null") return null;
        try { return System.Text.Json.JsonSerializer.Deserialize<string>(json); }
        catch { return json; }
    }

    // ===================== UI helpers =====================

    private void SetBusy(bool busy)
    {
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        CloneBtn.IsEnabled = !busy;
        CancelBtn.IsEnabled = busy;
        FolderBtn.IsEnabled = !busy;
        UrlBox.IsEnabled = !busy;
    }

    private void AppendLog(WebsiteClonerService.Progress p)
    {
        var text = P(p.En, p.Zh);
        var brushKey = p.Level switch
        {
            WebsiteClonerService.LogLevel.Ok => "SystemFillColorSuccessBrush",
            WebsiteClonerService.LogLevel.Warn => "SystemFillColorCautionBrush",
            WebsiteClonerService.LogLevel.Error => "SystemFillColorCriticalBrush",
            _ => "TextFillColorSecondaryBrush",
        };
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources[brushKey],
        };
        LogPanel.Children.Add(tb);
        LogScroller.UpdateLayout();
        LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
    }

    private void ShowResult(bool ok, string msg)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = ok ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = msg;
    }
}
