using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 豐富預覽 · Rich Preview — a native, in-app clone of the PowerToys File Explorer add-ons /
/// Preview Pane handler set, adapted for WinForge. Pick or drop a file and it renders a rich
/// preview by type: SVG and Markdown and source code via WebView2, PDF via the WebView2 viewer,
/// developer data (JSON/XML/YAML/TOML) pretty-printed, G-code with stats + embedded thumbnail,
/// QOI images decoded to a bitmap, and ordinary raster images. Prev/next walks the folder.
///
/// True system-wide Explorer preview-pane integration needs a registered COM shell extension,
/// which an unpackaged WinUI app can't provide — the Settings panel explains this and links to
/// the Windows preview-pane settings. This module delivers the equivalent value in-app.
/// </summary>
public sealed partial class RichPreviewModule : Page
{
    private readonly ObservableCollection<TypeToggleVM> _toggles = new();
    private readonly ObservableCollection<MetaRow> _meta = new();
    private List<string> _siblings = new();
    private int _index = -1;
    private string? _current;
    private bool _webReady;
    private bool _settingsOpen;

    public RichPreviewModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); BuildToggles(); MetaRows.ItemsSource = _meta; };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        ActualThemeChanged += (_, _) => { if (_current is not null) _ = RenderCurrentAsync(); };
    }

    private void OnLang(object? s, EventArgs e) { Render(); BuildToggles(); if (_current is not null) _ = RenderCurrentAsync(); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private bool Dark => ActualTheme == ElementTheme.Dark;

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string path && File.Exists(path))
            _ = LoadAsync(path);
    }

    // ===================== bilingual UI text =====================

    private void Render()
    {
        HeaderTitle.Text = "Rich Preview · 豐富預覽";
        HeaderBlurb.Text = P(
            "Pick or drop a file for a rich, type-aware preview — SVG, Markdown, PDF, source code, developer data (JSON/XML/YAML/TOML), G-code, QOI and ordinary images. The same file types PowerToys' File Explorer add-ons cover, rendered right here.",
            "揀檔或者拖入檔案，即時得到按類型嘅豐富預覽 — SVG、Markdown、PDF、原始碼、開發者資料（JSON／XML／YAML／TOML）、G-code、QOI 同一般圖片。等同 PowerToys 檔案總管增益所涵蓋嘅類型，喺呢度直接渲染。");

        PickLabel.Text = P("Open file…", "開啟檔案…");
        OpenLabel.Text = P("Open", "開啟");
        OpenFolderLabel.Text = P("Show in folder", "喺資料夾顯示");

        EmptyTitle.Text = P("No file selected", "未揀檔案");
        EmptyBlurb.Text = P("Click “Open file…” or drag a file here. Supported: SVG, Markdown, PDF, code, JSON/XML/YAML/TOML, G-code, QOI and images.",
            "撳「開啟檔案…」或者將檔案拖入嚟。支援：SVG、Markdown、PDF、程式碼、JSON／XML／YAML／TOML、G-code、QOI 同圖片。");

        SettingsTitle.Text = P("Rich Preview settings", "豐富預覽設定");
        SettingsIntro.Text = P("Turn individual file types on or off. Disabled types are skipped when picking files and during prev/next navigation.",
            "逐個檔案類型開關。停用嘅類型喺揀檔同上一個／下一個導覽時會略過。");

        SystemTitle.Text = P("System-wide Explorer preview pane", "系統層級檔案總管預覽窗格");
        SystemBlurb.Text = P(
            "Integrating these previews directly into the Windows File Explorer preview pane requires a registered COM shell extension, which is only possible in the packaged build of WinForge. This in-app Rich Preview module gives you the same previews without that requirement. The buttons below open the relevant Windows settings.",
            "要將呢啲預覽直接整合入 Windows 檔案總管嘅預覽窗格，需要註冊 COM 殼層擴充，呢個只可以喺 WinForge 嘅封裝版做到。呢個應用程式內嘅豐富預覽模組唔需要呢個條件就提供到相同預覽。下面嘅按鈕會開啟相關 Windows 設定。");
        OpenPreviewSettingsBtn.Content = P("Open File Explorer options", "開啟檔案總管選項");
        OpenFolderOptionsBtn.Content = P("Enable preview pane (Win+P tip)", "啟用預覽窗格（提示）");

        UpdatePosition();
    }

    private sealed class TypeToggleVM : INotifyPropertyChanged
    {
        public string Id { get; init; } = "";
        public string Title { get; init; } = "";
        public string ExtList { get; init; } = "";
        private bool _enabled;
        public bool Enabled { get => _enabled; set { _enabled = value; PropertyChanged?.Invoke(this, new(nameof(Enabled))); } }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed record MetaRow(string Key, string Value);

    private void BuildToggles()
    {
        _toggles.Clear();
        foreach (var t in RichPreviewService.Types)
        {
            _toggles.Add(new TypeToggleVM
            {
                Id = t.Id,
                Title = P(t.En, t.Zh),
                ExtList = string.Join("  ", t.Extensions.Take(10)) + (t.Extensions.Length > 10 ? "  …" : ""),
                Enabled = RichPreviewService.IsEnabled(t.Id),
            });
        }
        ToggleList.ItemsSource = _toggles;
    }

    // ===================== toolbar =====================

    private async void Pick_Click(object sender, RoutedEventArgs e)
    {
        var exts = RichPreviewService.EnabledExtensions();
        var path = await FileDialogs.OpenFileAsync(FileDialogs.BuildFilters(exts),
            P("Pick a file to preview", "揀一個檔案預覽"));
        if (path is not null) await LoadAsync(path);
    }

    private async void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_siblings.Count == 0) return;
        _index = (_index - 1 + _siblings.Count) % _siblings.Count;
        await LoadAsync(_siblings[_index], keepSiblings: true);
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_siblings.Count == 0) return;
        _index = (_index + 1) % _siblings.Count;
        await LoadAsync(_siblings[_index], keepSiblings: true);
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_current) { UseShellExecute = true });
        }
        catch (Exception ex) { Warn(ex.Message); }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe")
            { Arguments = $"/select,\"{_current}\"", UseShellExecute = true });
        }
        catch (Exception ex) { Warn(ex.Message); }
    }

    private void ToggleSettings_Click(object sender, RoutedEventArgs e)
    {
        _settingsOpen = !_settingsOpen;
        SettingsPanel.Visibility = _settingsOpen ? Visibility.Visible : Visibility.Collapsed;
        PreviewArea.Visibility = _settingsOpen ? Visibility.Collapsed : Visibility.Visible;
    }

    private void TypeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch ts && ts.Tag is string id)
            RichPreviewService.SetEnabled(id, ts.IsOn);
    }

    private void OpenPreviewSettings_Click(object sender, RoutedEventArgs e)
    {
        // Open the classic File Explorer Options (Folder Options) where the preview pane is configured.
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("control.exe") { Arguments = "folders", UseShellExecute = true }); }
        catch (Exception ex) { Warn(ex.Message); }
    }

    private void OpenFolderOptions_Click(object sender, RoutedEventArgs e)
    {
        Note(P("In File Explorer, press Alt+P (or View ▸ Preview pane) to show the preview pane.",
                "喺檔案總管按 Alt+P（或 檢視 ▸ 預覽窗格）就可以顯示預覽窗格。"));
    }

    // ===================== drag & drop =====================

    private void DropHost_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = P("Drop to preview", "放低嚟預覽");
            e.DragUIOverride.IsContentVisible = true;
            DragOverlay.Visibility = Visibility.Visible;
        }
    }

    private async void DropHost_Drop(object sender, DragEventArgs e)
    {
        DragOverlay.Visibility = Visibility.Collapsed;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var def = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<StorageFile>().FirstOrDefault();
            if (file is not null) await LoadAsync(file.Path);
        }
        catch (Exception ex) { Warn(ex.Message); }
        finally { def.Complete(); }
    }

    // ===================== load / render =====================

    private async Task LoadAsync(string path, bool keepSiblings = false)
    {
        _current = path;
        if (!keepSiblings)
        {
            _siblings = RichPreviewService.SiblingFiles(path);
            _index = _siblings.FindIndex(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
            if (_index < 0) _index = 0;
        }
        UpdatePosition();
        OpenBtn.IsEnabled = OpenFolderBtn.IsEnabled = true;
        await RenderCurrentAsync();
    }

    private void UpdatePosition()
    {
        bool hasSiblings = _siblings.Count > 1;
        PrevBtn.IsEnabled = NextBtn.IsEnabled = hasSiblings;
        PositionText.Text = _current is null ? "" :
            (hasSiblings ? $"{_index + 1} / {_siblings.Count}" : "1 / 1");
    }

    private async Task RenderCurrentAsync()
    {
        if (_current is null) return;
        var path = _current;
        ShowEmptyHint(false);
        HideAllViews();
        ShowLoading(true);

        try
        {
            var type = RichPreviewService.Classify(path, honourToggles: false);
            FillMeta(path, type);

            switch (type?.Kind)
            {
                case PreviewKind.Svg: await RenderSvgAsync(path); break;
                case PreviewKind.Markdown: await RenderMarkdownAsync(path); break;
                case PreviewKind.Pdf: await RenderPdfAsync(path); break;
                case PreviewKind.Developer: await RenderCodeAsync(path, developer: true); break;
                case PreviewKind.Code: await RenderCodeAsync(path, developer: false); break;
                case PreviewKind.Gcode: await RenderGcodeAsync(path); break;
                case PreviewKind.Qoi: RenderQoi(path); break;
                case PreviewKind.Image: RenderImage(path); break;
                default:
                    ShowLoading(false);
                    ShowEmptyHint(true);
                    EmptyTitle.Text = P("Unsupported file type", "唔支援嘅檔案類型");
                    EmptyBlurb.Text = P($"“{Path.GetFileName(path)}” isn't one of the rich-preview types.",
                                        $"「{Path.GetFileName(path)}」唔屬於豐富預覽支援嘅類型。");
                    break;
            }
        }
        catch (Exception ex)
        {
            ShowLoading(false);
            ShowEmptyHint(true);
            EmptyTitle.Text = P("Could not preview this file", "無法預覽呢個檔案");
            EmptyBlurb.Text = ex.Message;
        }
    }

    // ---- WebView2-backed renders (SVG / Markdown / code / PDF) ----

    private async Task EnsureWebAsync()
    {
        if (_webReady) return;
        try { _ = CoreWebView2Environment.GetAvailableBrowserVersionString(); }
        catch { throw new InvalidOperationException(P("WebView2 Runtime not found. It ships with Windows 11; install it to preview SVG, Markdown, code and PDF.",
            "搵唔到 WebView2 執行階段。Windows 11 一般已內建；安裝後先可以預覽 SVG、Markdown、程式碼同 PDF。")); }
        await Web.EnsureCoreWebView2Async();
        _webReady = true;
    }

    private void Web_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        if (sender.CoreWebView2 is null) return;
        var s = sender.CoreWebView2.Settings;
        s.AreDefaultContextMenusEnabled = true;
        s.AreDevToolsEnabled = false;
        s.IsZoomControlEnabled = true;
        // Open clicked links in the default browser, not inside the preview.
        sender.CoreWebView2.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri) { UseShellExecute = true }); } catch { }
        };
    }

    private async Task ShowHtmlAsync(string html)
    {
        await EnsureWebAsync();
        ShowLoading(false);
        Web.Visibility = Visibility.Visible;
        Web.NavigateToString(html);
    }

    private async Task RenderSvgAsync(string path)
    {
        var svg = await File.ReadAllTextAsync(path);
        await ShowHtmlAsync(RichPreviewService.SvgHostHtml(svg, Dark));
    }

    private async Task RenderMarkdownAsync(string path)
    {
        var md = RichPreviewService.ReadTextCapped(path, out _);
        var body = RichPreviewService.MarkdownToHtmlFragment(md);
        await ShowHtmlAsync(RichPreviewService.HtmlShell(body, Dark, isCode: false));
    }

    private async Task RenderCodeAsync(string path, bool developer)
    {
        var text = RichPreviewService.ReadTextCapped(path, out var truncated);
        string language;
        if (developer)
            text = RichPreviewService.PrettyPrint(Path.GetExtension(path), text, out language);
        else
            language = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var body = RichPreviewService.CodeToHtml(text, language, Dark);
        await ShowHtmlAsync(RichPreviewService.HtmlShell(body, Dark, isCode: true));
        if (truncated) Note(P("Large file — preview truncated.", "檔案太大 — 預覽已截斷。"));
    }

    private async Task RenderPdfAsync(string path)
    {
        await EnsureWebAsync();
        ShowLoading(false);
        Web.Visibility = Visibility.Visible;
        // WebView2 (Edge/Chromium) has a built-in PDF viewer; navigate to the file:// URI.
        var uri = new Uri(path).AbsoluteUri;
        Web.CoreWebView2!.Navigate(uri);
    }

    // ---- G-code (text + stats + embedded thumbnail) ----

    private async Task RenderGcodeAsync(string path)
    {
        var stats = await Task.Run(() => RichPreviewService.AnalyzeGcode(path));
        var text = RichPreviewService.ReadTextCapped(path, out var truncated, 1 * 1024 * 1024);
        var body = RichPreviewService.CodeToHtml(text, "gcode", Dark);
        await ShowHtmlAsync(RichPreviewService.HtmlShell(body, Dark, isCode: true));

        // Extra G-code rows + thumbnail into the metadata panel.
        _meta.Add(new MetaRow(P("Lines", "行數"), stats.LineCount.ToString("N0")));
        if (stats.LayerCount > 0) _meta.Add(new MetaRow(P("Layers", "層數"), stats.LayerCount.ToString("N0")));
        if (stats.MaxZ is double z) _meta.Add(new MetaRow(P("Height", "高度"), $"{z:0.##} mm"));
        if (stats.FilamentMm is double mm) _meta.Add(new MetaRow(P("Filament", "線材"), $"{mm / 1000.0:0.##} m"));
        if (stats.FilamentG is double gr) _meta.Add(new MetaRow(P("Weight", "重量"), $"{gr:0.##} g"));
        if (stats.PrintMinutes is double pm) _meta.Add(new MetaRow(P("Print time", "列印時間"), RichPreviewService.FormatMinutes(pm)));
        if (!string.IsNullOrWhiteSpace(stats.Slicer)) _meta.Add(new MetaRow(P("Slicer", "切片器"), stats.Slicer!));

        if (stats.ThumbnailDataUri is string uri && uri.StartsWith("data:image/png", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var b64 = uri.Substring(uri.IndexOf(',') + 1);
                var bytes = Convert.FromBase64String(b64);
                var bmp = new BitmapImage();
                using var ms = new MemoryStream(bytes);
                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                MetaThumb.Source = bmp;
                MetaThumb.Visibility = Visibility.Visible;
            }
            catch { }
        }
        if (truncated) Note(P("Large G-code — text preview truncated; stats scan full file.", "G-code 太大 — 文字預覽已截斷；統計仍掃描整個檔案。"));
    }

    // ---- QOI (decode → WriteableBitmap) ----

    private void RenderQoi(string path)
    {
        try
        {
            var q = RichPreviewService.DecodeQoi(path);
            var wb = new WriteableBitmap(q.Width, q.Height);
            using (var s = wb.PixelBuffer.AsStream())
                s.Write(q.Bgra, 0, q.Bgra.Length);
            wb.Invalidate();
            ImageView.Source = wb;
            ShowLoading(false);
            ImageScroll.Visibility = Visibility.Visible;
            _meta.Add(new MetaRow(P("Dimensions", "尺寸"), $"{q.Width} × {q.Height}"));
            _meta.Add(new MetaRow(P("Channels", "通道"), q.Channels == 4 ? "RGBA" : "RGB"));
            _meta.Add(new MetaRow(P("Color space", "色彩空間"), q.ColorSpace));
        }
        catch (Exception ex)
        {
            ShowLoading(false);
            ShowEmptyHint(true);
            EmptyTitle.Text = P("Could not decode QOI image", "無法解碼 QOI 影像");
            EmptyBlurb.Text = ex.Message;
        }
    }

    // ---- ordinary raster image ----

    private void RenderImage(string path)
    {
        try
        {
            var bmp = new BitmapImage(new Uri(path));
            bmp.ImageOpened += (_, _) =>
            {
                _meta.Add(new MetaRow(P("Dimensions", "尺寸"), $"{bmp.PixelWidth} × {bmp.PixelHeight}"));
            };
            ImageView.Source = bmp;
            ShowLoading(false);
            ImageScroll.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowLoading(false);
            ShowEmptyHint(true);
            EmptyTitle.Text = P("Could not load image", "無法載入圖片");
            EmptyBlurb.Text = ex.Message;
        }
    }

    // ===================== metadata panel =====================

    private void FillMeta(string path, PreviewType? type)
    {
        _meta.Clear();
        MetaThumb.Visibility = Visibility.Collapsed;
        MetaThumb.Source = null;
        MetaPanel.Visibility = Visibility.Visible;
        var fi = new FileInfo(path);
        MetaName.Text = fi.Name;
        MetaTypeBadge.Text = type is null
            ? P("Unsupported", "唔支援")
            : P(type.En, type.Zh);
        _meta.Add(new MetaRow(P("Size", "大小"), RichPreviewService.HumanSize(fi.Exists ? fi.Length : 0)));
        try { _meta.Add(new MetaRow(P("Modified", "修改時間"), fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm"))); } catch { }
        _meta.Add(new MetaRow(P("Type", "類型"), Path.GetExtension(path).TrimStart('.').ToUpperInvariant()));
        _meta.Add(new MetaRow(P("Folder", "資料夾"), Path.GetDirectoryName(path) ?? ""));
    }

    // ===================== view-state helpers =====================

    private void HideAllViews()
    {
        Web.Visibility = Visibility.Collapsed;
        ImageScroll.Visibility = Visibility.Collapsed;
        ImageView.Source = null;
    }

    private void ShowLoading(bool on)
    {
        LoadingPanel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) LoadingText.Text = P("Rendering preview…", "正在渲染預覽…");
    }

    private void ShowEmptyHint(bool on)
    {
        EmptyHint.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) MetaPanel.Visibility = Visibility.Collapsed;
    }

    private void Note(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Informational;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
