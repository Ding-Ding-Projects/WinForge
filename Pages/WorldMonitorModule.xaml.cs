using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// World Monitor — 實時全球情報儀表板（新聞、地緣政治、金融、能源、不穩定指數），
/// 經 WebView2 內嵌官方寄存網頁，配原生 WinForge 工具列（變體切換、重載、縮放、複製網址）。
/// AGPL 合規：只內嵌寄存網頁／啟動未經修改嘅上游二進位檔，絕不 fork 或重新編譯 WM 原始碼。
///
/// World Monitor — a real-time global intelligence dashboard (news, geopolitics, finance,
/// energy, an instability index) embedded via WebView2 with a native WinForge toolbar
/// (variant switch, reload, zoom, copy URL). AGPL-clean: embeds the hosted web app /
/// never forks, vendors, recompiles or launches the upstream binary.
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
        CopyUrlTxt.Text = P("Copy URL", "複製網址");
        ZoomLbl.Text = $"{(int)Math.Round(_svc.Zoom * 100)}%";
        RetryBtn.Content = P("Retry", "重試");
        ErrCopyUrlBtn.Content = P("Copy URL", "複製網址");
        RtCopyUrlBtn.Content = P("Copy dashboard URL", "複製儀表板網址");
        CopyRuntimeBtn.Content = P("Copy WebView2 Runtime link", "複製 WebView2 執行階段連結");
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

    private void CopyUrl_Click(object sender, RoutedEventArgs e)
        => CopyUrl(CurrentUrl);

    private void CopyRuntimeUrl_Click(object sender, RoutedEventArgs e)
        => CopyUrl("https://developer.microsoft.com/microsoft-edge/webview2/");

    private void CopyUrl(string url)
    {
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(url);
            Clipboard.SetContent(dp);
            Clipboard.Flush();
            UrlText.Text = P("Copied URL: ", "已複製網址：") + url;
        }
        catch { }
    }

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
            "Check your network connection, then retry — or copy the dashboard URL. ",
            "請檢查網絡連線再重試 — 或者複製儀表板網址。 ") + detail;
        ErrorPanel.Visibility = Visibility.Visible;
    }

    private void ShowRuntimeMissing(string? detail = null)
    {
        RuntimeBar.Title = P("WebView2 Runtime not found", "搵唔到 WebView2 執行階段");
        RuntimeBar.Message = P(
            "World Monitor is embedded via WebView2, which ships with Windows 11. Copy the runtime link, install it, then reload.",
            "World Monitor 經 WebView2 內嵌，Windows 11 一般已內建。請複製執行階段連結，安裝後再重新載入。")
            + (string.IsNullOrEmpty(detail) ? "" : "\n" + detail);
        RuntimePanel.Visibility = Visibility.Visible;
    }

    // ── Advanced: URL overrides ───────────────────────────────────────────────

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
        if (res == ContentDialogResult.Primary)
        {
            foreach (var v in WorldMonitorService.DefaultVariants)
                _svc.SetUrlOverride(v.Key, boxes[v.Key].Text ?? "");
            // 重新導向到更新後嘅網址 · re-navigate to the updated URL
            if (Web.CoreWebView2 is not null) NavigateToCurrent();
        }
    }
}
