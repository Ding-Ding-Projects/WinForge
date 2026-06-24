using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// World Monitor — 實時全球情報儀表板（新聞、地緣政治、金融、能源、不穩定指數），
/// 經 WebView2 內嵌官方寄存網頁，配原生 WinForge 工具列（變體切換、重載、縮放、外開瀏覽器）。
/// AGPL 合規：只內嵌寄存網頁／啟動未經修改嘅上游二進位檔，絕不 fork 或重新編譯 WM 原始碼。
///
/// World Monitor — a real-time global intelligence dashboard (news, geopolitics, finance,
/// energy, an instability index) embedded via WebView2 with a native WinForge toolbar
/// (variant switch, reload, zoom, open-in-browser). AGPL-clean: embeds the hosted web app /
/// launches the unmodified upstream binary; never forks or recompiles WM source.
/// </summary>
public sealed partial class WorldMonitorModule : Page
{
    private readonly WorldMonitorService _svc = new();

    public WorldMonitorModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += OnLoaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 填變體選單 · populate the variant combo
        if (VariantBox.Items.Count == 0)
        {
            foreach (var v in WorldMonitorService.DefaultVariants)
                VariantBox.Items.Add(new ComboBoxItem { Content = $"{v.En} · {v.Zh}", Tag = v.Key });
        }
        var last = _svc.LastVariantKey;
        var idx = WorldMonitorService.DefaultVariants
            .Select((v, i) => (v, i)).FirstOrDefault(t => t.v.Key == last).i;
        VariantBox.SelectedIndex = idx;

        Render();
        _ = InitWebViewAsync();
    }

    private void Render()
    {
        HeaderTitle.Text = "World Monitor · 世界監察";
        VariantLbl.Text = P("Variant", "變體");
        OpenBrowserTxt.Text = P("Open in browser", "用瀏覽器開");
        ZoomLbl.Text = $"{(int)Math.Round(_svc.Zoom * 100)}%";
        RetryBtn.Content = P("Retry", "重試");
        ErrOpenBrowserBtn.Content = P("Open in browser", "用瀏覽器開");
        RtOpenBrowserBtn.Content = P("Open in browser", "用瀏覽器開");
        GetRuntimeBtn.Content = P("Get WebView2 Runtime", "下載 WebView2 執行階段");
        FootNote.Text = P(
            "World Monitor is open-source (AGPL-3.0) by koala73. WinForge embeds the hosted web app — it does not fork or recompile the source. AI features and some feeds need third-party keys / network.",
            "World Monitor 係 koala73 嘅開源項目（AGPL-3.0）。WinForge 只係內嵌官方網頁，唔會 fork 或重新編譯原始碼。AI 功能同部分資料源需要第三方金鑰／網絡。");
        UpdateNavButtons();
    }

    // ── WebView2 init ─────────────────────────────────────────────────────────

    private async Task InitWebViewAsync()
    {
        try
        {
            // 偵測 WebView2 執行階段 · detect the WebView2 Runtime
            try { _ = CoreWebView2Environment.GetAvailableBrowserVersionString(); }
            catch
            {
                ShowRuntimeMissing();
                return;
            }
            await Web.EnsureCoreWebView2Async();
        }
        catch (Exception ex)
        {
            ShowRuntimeMissing(ex.Message);
            return;
        }
        NavigateToCurrent();
    }

    private void Web_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        if (sender.CoreWebView2 is null) return;
        var s = sender.CoreWebView2.Settings;
        s.AreDefaultContextMenusEnabled = true;
        s.IsStatusBarEnabled = true;
        s.AreDevToolsEnabled = false;
        ApplyZoom();
    }

    private WmVariant CurrentVariant
    {
        get
        {
            var key = (VariantBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "world";
            return WorldMonitorService.DefaultVariants.FirstOrDefault(v => v.Key == key)
                   ?? WorldMonitorService.DefaultVariants[0];
        }
    }

    private string CurrentUrl => _svc.UrlFor(CurrentVariant);

    private void NavigateToCurrent()
    {
        if (Web.CoreWebView2 is null) return;
        HideOverlays();
        var url = CurrentUrl;
        UrlText.Text = url;
        try { Web.CoreWebView2.Navigate(url); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    // ── Toolbar handlers ──────────────────────────────────────────────────────

    private void VariantBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VariantBox.SelectedItem is not ComboBoxItem cbi || cbi.Tag is not string key) return;
        _svc.LastVariantKey = key;
        if (Web.CoreWebView2 is not null) NavigateToCurrent();
    }

    private void Home_Click(object sender, RoutedEventArgs e) => NavigateToCurrent();

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is null) { _ = InitWebViewAsync(); return; }
        HideOverlays();
        try { Web.CoreWebView2.Reload(); } catch { NavigateToCurrent(); }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2?.CanGoBack == true) Web.CoreWebView2.GoBack();
    }

    private void Fwd_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2?.CanGoForward == true) Web.CoreWebView2.GoForward();
    }

    private void UpdateNavButtons()
    {
        BackBtn.IsEnabled = Web.CoreWebView2?.CanGoBack == true;
        FwdBtn.IsEnabled = Web.CoreWebView2?.CanGoForward == true;
    }

    private void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (Web.CoreWebView2 is null) _ = InitWebViewAsync();
        else NavigateToCurrent();
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
        => WorldMonitorService.OpenInBrowser(CurrentUrl);

    private void GetRuntime_Click(object sender, RoutedEventArgs e)
        => WorldMonitorService.OpenInBrowser("https://developer.microsoft.com/microsoft-edge/webview2/");

    // ── Zoom ──────────────────────────────────────────────────────────────────

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => SetZoom(_svc.Zoom + 0.1);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => SetZoom(_svc.Zoom - 0.1);

    private void SetZoom(double z)
    {
        _svc.Zoom = z;
        ZoomLbl.Text = $"{(int)Math.Round(_svc.Zoom * 100)}%";
        ApplyZoom();
    }

    private async void ApplyZoom()
    {
        // WinUI 嘅 WebView2 控件冇 ZoomFactor 屬性，所以用 CSS zoom 注入。
        // The WinUI WebView2 control has no ZoomFactor property; inject CSS zoom instead.
        try
        {
            if (Web.CoreWebView2 is null) return;
            var z = _svc.Zoom.ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture);
            await Web.CoreWebView2.ExecuteScriptAsync(
                $"try{{document.documentElement.style.zoom='{z}';}}catch(e){{}}");
        }
        catch { }
    }

    // ── Navigation events ─────────────────────────────────────────────────────

    private void Web_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        LoadRing.IsActive = true;
    }

    private void Web_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        LoadRing.IsActive = false;
        UpdateNavButtons();
        if (!args.IsSuccess)
        {
            // WebErrorStatus 提供失敗原因 · failure reason
            ShowError(P($"Could not load the page ({args.WebErrorStatus}).",
                       $"載入唔到呢一頁（{args.WebErrorStatus}）。"));
        }
        else
        {
            ApplyZoom();
            HideOverlays();
        }
    }

    // ── Overlays ──────────────────────────────────────────────────────────────

    private void HideOverlays()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        RuntimePanel.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string detail)
    {
        ErrorBar.Title = P("Couldn't load World Monitor", "載入唔到 World Monitor");
        ErrorBar.Message = P(
            "Check your network connection, then retry — or open the dashboard in your browser. ",
            "請檢查網絡連線再重試 — 或者用瀏覽器打開儀表板。 ") + detail;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void ShowRuntimeMissing(string? detail = null)
    {
        RuntimeBar.Title = P("WebView2 Runtime not found", "搵唔到 WebView2 執行階段");
        RuntimeBar.Message = P(
            "World Monitor is embedded via WebView2, which ships with Windows 11. Install the runtime, then reload — or open the dashboard in your browser.",
            "World Monitor 經 WebView2 內嵌，Windows 11 一般已內建。請安裝執行階段後重新載入 — 或者用瀏覽器打開。")
            + (string.IsNullOrEmpty(detail) ? "" : "\n" + detail);
        RuntimePanel.Visibility = Visibility.Visible;
    }

    // ── Advanced: URL overrides + desktop binary ──────────────────────────────

    private async void Adv_Click(object sender, RoutedEventArgs e)
    {
        var stack = new StackPanel { Spacing = 12, MinWidth = 460 };

        // URL overrides
        stack.Children.Add(new TextBlock
        {
            Text = P("Variant URLs (override the built-ins; leave blank to reset)",
                     "變體網址（覆寫內建；留空＝還原）"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        var boxes = new System.Collections.Generic.Dictionary<string, TextBox>();
        foreach (var v in WorldMonitorService.DefaultVariants)
        {
            var tb = new TextBox
            {
                Header = $"{v.En} · {v.Zh}",
                PlaceholderText = v.Url,
                Text = SettingsStore.Get("worldmonitor.url." + v.Key, ""),
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            };
            boxes[v.Key] = tb;
            stack.Children.Add(tb);
        }

        // Desktop binary section
        stack.Children.Add(new TextBlock
        {
            Text = P("Desktop app (optional) — for full 3D-globe performance",
                     "桌面版（選用）— 完整 3D 地球效能"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0),
        });
        var binBox = new TextBox
        {
            Header = P("Path to World Monitor desktop binary", "World Monitor 桌面安裝檔路徑"),
            Text = _svc.BinaryPath,
            PlaceholderText = "C:\\...\\WorldMonitor.exe",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        };
        stack.Children.Add(binBox);

        var binBtns = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var browseBtn = new Button { Content = P("Browse…", "瀏覽…") };
        var dlBtn = new Button { Content = P("Download installer", "下載安裝檔") };
        var launchBtn = new Button { Content = P("Launch desktop app", "啟動桌面版") };
        binBtns.Children.Add(browseBtn);
        binBtns.Children.Add(dlBtn);
        binBtns.Children.Add(launchBtn);
        stack.Children.Add(binBtns);

        var advBar = new InfoBar { IsClosable = false, IsOpen = false };
        var dlRing = new ProgressBar { Minimum = 0, Maximum = 1, Visibility = Visibility.Collapsed };
        stack.Children.Add(dlRing);
        stack.Children.Add(advBar);

        browseBtn.Click += async (_, _) =>
        {
            var path = await FileDialogs.OpenFileAsync(".exe");
            if (path is not null) binBox.Text = path;
        };

        var cts = new CancellationTokenSource();
        dlBtn.Click += async (_, _) =>
        {
            var dir = await FileDialogs.OpenFolderAsync(P("Choose a download folder", "揀下載資料夾"));
            if (dir is null) return;
            dlBtn.IsEnabled = false; dlRing.Visibility = Visibility.Visible; dlRing.Value = 0;
            advBar.Severity = InfoBarSeverity.Informational;
            advBar.Title = P("Downloading…", "下載緊…");
            advBar.Message = WorldMonitorService.WindowsDownloadUrl;
            advBar.IsOpen = true;
            var prog = new Progress<double>(p => dlRing.Value = p);
            var (ok, path, msg) = await _svc.DownloadInstallerAsync(dir, prog, cts.Token);
            dlRing.Visibility = Visibility.Collapsed; dlBtn.IsEnabled = true;
            if (ok)
            {
                binBox.Text = path;
                advBar.Severity = InfoBarSeverity.Success;
                advBar.Title = P("Downloaded — run it to install", "下載完成 — 執行佢嚟安裝");
                advBar.Message = $"{path}  ({msg})";
            }
            else
            {
                advBar.Severity = InfoBarSeverity.Error;
                advBar.Title = P("Download failed", "下載失敗");
                advBar.Message = msg;
            }
        };

        launchBtn.Click += async (_, _) =>
        {
            _svc.BinaryPath = (binBox.Text ?? "").Trim();
            var (ok, msg) = await _svc.LaunchBinaryAsync();
            advBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            advBar.Title = ok ? P("Launched", "已啟動") : P("Could not launch", "啟動唔到");
            advBar.Message = msg;
            advBar.IsOpen = true;
        };

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Advanced · 進階", "進階"),
            Content = new ScrollViewer { Content = stack, MaxHeight = 560, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Close", "關閉"),
            DefaultButton = ContentDialogButton.Primary,
        };

        var res = await dlg.ShowAsync();
        cts.Cancel();
        if (res == ContentDialogResult.Primary)
        {
            foreach (var v in WorldMonitorService.DefaultVariants)
                _svc.SetUrlOverride(v.Key, boxes[v.Key].Text ?? "");
            _svc.BinaryPath = (binBox.Text ?? "").Trim();
            // 重新導向到更新後嘅網址 · re-navigate to the updated URL
            if (Web.CoreWebView2 is not null) NavigateToCurrent();
        }
    }
}
