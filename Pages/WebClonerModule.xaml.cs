using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 網站複製器 · Website Cloner — point it at a URL, pick a folder, and it fetches the page,
/// downloads its assets, rewrites links to local paths and writes a browsable index.html.
/// 可選 AI 一步用終端機編程代理清理成 index.html／styles.css／script.js。
/// An optional AI pass hands the result to a terminal coding agent to tidy it up. Bilingual.
/// </summary>
public sealed partial class WebClonerModule : Page
{
    private CancellationTokenSource? _cts;
    private string? _lastIndexPath;
    private bool _webReady;

    public WebClonerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) =>
        {
            Render();
            FolderBox.Text = DefaultDest();
            await BuildAgentCombo();
        };
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

    private void Render()
    {
        HeaderTitle.Text = "Website Cloner · 網站複製器";
        HeaderBlurb.Text = P(
            "Fetch a live web page, download its assets and save a browsable local copy. Optionally let an AI coding agent clean it into tidy HTML/CSS/JS.",
            "下載一個網頁、攞埋佢嘅資源，儲存成可以喺本機瀏覽嘅副本。仲可以叫 AI 編程代理幫你清理成整齊嘅 HTML／CSS／JS。");

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

        // Mode radios — rebuild so labels follow language.
        var sel = ModeRadios.SelectedIndex < 0 ? 0 : ModeRadios.SelectedIndex;
        ModeRadios.Header = P("Mode", "模式");
        ModeRadios.Items.Clear();
        ModeRadios.Items.Add(P("Native only — fetch & save (fully offline)", "只用原生 — 下載並儲存（完全離線）"));
        ModeRadios.Items.Add(P("Native + AI cleanup — then tidy with a coding agent", "原生 + AI 清理 — 之後用編程代理整理"));
        ModeRadios.SelectedIndex = sel;
        ModeRadios.SelectionChanged -= Mode_Changed;
        ModeRadios.SelectionChanged += Mode_Changed;

        AgentLabel.Text = P("AI agent", "AI 代理");

        CloneBtn.Content = P("Clone website", "複製網站");
        CancelBtn.Content = P("Cancel", "取消");
        OpenFolderBtn.Content = P("Open folder", "打開資料夾");
        OpenBrowserBtn.Content = P("Open in browser", "喺瀏覽器打開");

        TokenTitle.Text = P("Extracted design tokens", "抽取到嘅設計符記");
        LogTitle.Text = P("Progress log", "進度記錄");

        UpdateAgentRowVisibility();
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e) => UpdateAgentRowVisibility();

    private void UpdateAgentRowVisibility()
    {
        bool ai = ModeRadios.SelectedIndex == 1;
        AgentRow.Visibility = ai ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task BuildAgentCombo()
    {
        AgentCombo.Items.Clear();
        foreach (var a in AiAgentService.All)
            AgentCombo.Items.Add(new ComboBoxItem { Content = a.Name, Tag = a.Key });
        if (AgentCombo.Items.Count > 0) AgentCombo.SelectedIndex = 0;

        // Surface a hint if no agent is installed (offered via AiAgents module).
        try
        {
            bool any = false;
            foreach (var a in AiAgentService.All)
                if (await AiAgentService.IsInstalledAsync(a)) { any = true; break; }
            if (!any)
            {
                AgentBar.IsOpen = true;
                AgentBar.Severity = InfoBarSeverity.Informational;
                AgentBar.Title = P("No AI agent detected", "偵測唔到 AI 代理");
                AgentBar.Message = P(
                    "Native mode works without any agent. For the AI cleanup pass, install a coding agent in the AI Agents module first.",
                    "原生模式唔需要任何代理。如要 AI 清理，請先喺「AI 代理」模組安裝一個編程代理。");
            }
        }
        catch { }
    }

    private AiAgent? SelectedAgent()
    {
        if (AgentCombo.SelectedItem is ComboBoxItem item && item.Tag is string key)
        {
            foreach (var a in AiAgentService.All)
                if (a.Key == key) return a;
        }
        return null;
    }

    // ===================== UI actions =====================

    private async void Folder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose where to save the clone", "揀儲存複製品嘅位置"));
        if (folder is not null) FolderBox.Text = folder;
    }

    private async void Clone_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text?.Trim() ?? "";
        var folder = FolderBox.Text?.Trim() ?? "";

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
            OpenFolderBtn.IsEnabled = true;
            OpenBrowserBtn.IsEnabled = result.IndexPath is not null;

            if (result.DesignTokens is { Count: > 0 })
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in result.DesignTokens)
                    sb.AppendLine($"{kv.Key}: {kv.Value}");
                TokenText.Text = sb.ToString().TrimEnd();
                TokenCard.Visibility = Visibility.Visible;
            }

            ShowResult(true, result.Message.Get(Loc.I.Language));

            // AI cleanup pass.
            if (ModeRadios.SelectedIndex == 1)
            {
                var agent = SelectedAgent();
                if (agent is null)
                {
                    AppendLog(new WebsiteClonerService.Progress(
                        "No agent selected — skipping AI pass.", "未揀代理 — 略過 AI 清理。",
                        WebsiteClonerService.LogLevel.Warn));
                }
                else if (!await AiAgentService.IsInstalledAsync(agent, _cts.Token))
                {
                    AppendLog(new WebsiteClonerService.Progress(
                        $"{agent.NameEn} is not installed — skipping AI pass.",
                        $"未安裝 {agent.NameZh} — 略過 AI 清理。", WebsiteClonerService.LogLevel.Warn));
                }
                else
                {
                    AppendLog(new WebsiteClonerService.Progress(
                        $"Starting AI cleanup with {agent.NameEn}…", $"開始用 {agent.NameZh} 做 AI 清理…",
                        WebsiteClonerService.LogLevel.Info));
                    var ai = await WebsiteClonerService.RunAiPassAsync(agent, folder, url, progress, _cts.Token);
                    ShowResult(ai.Success, (Loc.I.IsCantonesePrimary ? ai.Message?.Zh : ai.Message?.En) ?? "");
                }
            }
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

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = FolderBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(folder)) WebsiteClonerService.OpenFolder(folder);
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_lastIndexPath)) WebsiteClonerService.OpenInBrowser(_lastIndexPath);
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
