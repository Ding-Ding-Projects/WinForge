using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Storage;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// PowerToys 額外工具 · PowerToys extras — a bilingual hub for the built-in PowerToys-style utilities:
/// Image Resizer (bulk resize via WinRT imaging), Text Extractor / OCR (Windows.Media.Ocr over a screen
/// capture → clipboard), Always On Top (SetWindowPos HWND_TOPMOST on a picked window), and
/// Paste as Plain Text (strip clipboard formatting + a Ctrl+Shift+V global hotkey). No redirects.
/// </summary>
public sealed partial class PowerToysExtrasModule : Page
{
    private readonly ObservableCollection<string> _resizeFiles = new();

    /// <summary>視窗連置頂狀態（畀 Always On Top 個列表用）· A window with its pinned/label state.</summary>
    public sealed class TopWin : INotifyPropertyChanged
    {
        public IntPtr Handle { get; init; }
        public string Title { get; init; } = "";
        public string Process { get; init; } = "";
        private bool _pinned;
        public bool Pinned { get => _pinned; set { _pinned = value; OnChanged(nameof(Pinned)); OnChanged(nameof(PinLabel)); } }
        public string PinLabel => _pinned ? PinLabelPinned : PinLabelNormal;
        public static string PinLabelPinned = "On top";
        public static string PinLabelNormal = "Pin on top";
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public PowerToysExtrasModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); InitOnce(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged; // keep hotkey + pins running app-wide; nothing else to tear down here
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private bool _inited;
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void InitOnce()
    {
        if (_inited) return;
        _inited = true;

        ResizeList.ItemsSource = _resizeFiles;

        // presets
        PresetBox.Items.Clear();
        foreach (var p in ImageResizeService.Presets)
            PresetBox.Items.Add(p.Label(Loc.I.IsCantonesePrimary));
        PresetBox.SelectedIndex = 2; // Large 1920x1080
        ShrinkOnlyChk.IsChecked = true;
        OutFolderBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WinForge Resized");

        // OCR languages
        var langs = TextExtractorService.AvailableLanguages();
        OcrLangBox.Items.Clear();
        foreach (var l in langs) OcrLangBox.Items.Add(l);
        if (OcrLangBox.Items.Count > 0) OcrLangBox.SelectedIndex = 0;

        // Paste hotkey current state
        PasteHotkeySwitch.IsOn = PlainTextPasteService.HotkeyActive;

        RefreshTopWindows();
    }

    private void Render()
    {
        Header.Title = "PowerToys Extras · PowerToys 額外工具";
        HeaderBlurb.Text = P(
            "Four native, in-app PowerToys-style utilities plus a discoverability tab for the rest of WinForge's PowerToys-equivalent pages.",
            "四個原生、應用程式內嘅 PowerToys 式工具，再加一個索引分頁去搵 WinForge 其餘同 PowerToys 對應嘅頁面。");

        TabResize.Header = P("Image Resizer", "圖片縮放");
        TabOcr.Header = P("Text Extractor (OCR)", "文字擷取（OCR）");
        TabTop.Header = P("Always On Top", "視窗置頂");
        TabPaste.Header = P("Paste Plain Text", "純文字貼上");

        // Image Resizer
        ResizeIntro.Text = P("Add pictures, pick a size preset (or set your own width/height), choose an output folder, then resize the whole batch. Aspect ratio is always kept.",
            "加入圖片、揀一個尺寸預設（或者自訂闊高）、揀輸出資料夾，再一次過縮放成批。長闊比例永遠會保持。");
        ResizeAddBtn.Content = P("Add images…", "加入圖片…");
        ResizeClearBtn.Content = P("Clear", "清空");
        UpdateResizeCount();
        PresetLabel.Text = P("Size preset", "尺寸預設");
        ShrinkOnlyChk.Content = P("Shrink only (never enlarge)", "只縮唔放（唔放大）");
        WidthLabel.Text = P("Max width (px)", "最大闊度（像素）");
        HeightLabel.Text = P("Max height (px)", "最大高度（像素）");
        QualityLabel.Text = P("JPEG quality (1–100)", "JPEG 品質（1–100）");
        SuffixLabel.Text = P("Filename suffix", "檔名後綴");
        OutFolderLabel.Text = P("Output folder", "輸出資料夾");
        OutFolderBtn.Content = P("Browse…", "瀏覽…");
        ResizeRunBtn.Content = P("Resize all", "全部縮放");

        // OCR
        OcrIntro.Text = P("Grab text off the screen. Click “Extract from screen”, then the recognised text appears below and is copied to the clipboard automatically. Choose a language if more than one OCR pack is installed.",
            "由螢幕攞文字。撳「由螢幕擷取」，辨識到嘅文字會喺下面出現，亦會自動複製去剪貼簿。如果裝咗多過一個 OCR 語言包，可以揀語言。");
        OcrLangLabel.Text = P("OCR language", "OCR 語言");
        OcrFullBtn.Content = P("Extract from screen", "由螢幕擷取");
        OcrCopyBtn.Content = P("Copy text", "複製文字");
        OcrClearBtn.Content = P("Clear", "清空");
        if (OcrLangBox.Items.Count == 0)
            OcrHint.Text = P("No OCR language pack found — install one in Windows language settings.", "搵唔到 OCR 語言包 — 喺 Windows 語言設定安裝一個。");
        else
            OcrHint.Text = "";

        // Always On Top
        TopIntro.Text = P("Pin any open window so it stays above everything else (SetWindowPos HWND_TOPMOST). Toggle it off, or un-pin all at once.",
            "將任何開住嘅視窗釘喺最上面，永遠喺其他視窗之上（SetWindowPos HWND_TOPMOST）。可以逐個取消，或者一次過全部取消。");
        TopRefreshBtn.Content = P("Refresh", "重新整理");
        TopUnpinAllBtn.Content = P("Un-pin all", "全部取消置頂");
        TopWin.PinLabelPinned = P("On top ✓", "置頂中 ✓");
        TopWin.PinLabelNormal = P("Pin on top", "釘喺最上");
        UpdateTopCount();

        // Paste as plain text
        PasteIntro.Text = P("Remove all formatting (fonts, colours, links) from the clipboard so the next paste is clean plain text.",
            "移除剪貼簿入面所有格式（字型、顏色、連結），令下次貼上係乾淨嘅純文字。");
        PasteStripTitle.Text = P("Strip the clipboard now", "立即淨化剪貼簿");
        PasteStripBlurb.Text = P("One-shot: replaces the current clipboard contents with their plain-text equivalent.",
            "一次過：將而家剪貼簿嘅內容換成純文字版本。");
        PasteStripBtn.Content = P("Strip formatting now", "立即移除格式");
        PasteHotkeyTitle.Text = P($"Global hotkey ({PlainTextPasteService.HotkeyText})", $"全域熱鍵（{PlainTextPasteService.HotkeyText}）");
        PasteHotkeyBlurb.Text = P("When on, pressing Ctrl+Shift+V anywhere strips the clipboard then pastes it as plain text. Works while WinForge is running.",
            "開咗之後，喺任何地方撳 Ctrl+Shift+V 都會先淨化剪貼簿，再以純文字貼上。WinForge 開住就生效。");
        PasteHotkeySwitch.OnContent = P("Enabled", "已啟用");
        PasteHotkeySwitch.OffContent = P("Disabled", "已停用");
    }

    private void Info(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    // ===================== Image Resizer =====================

    private void UpdateResizeCount() => ResizeCountText.Text = P($"{_resizeFiles.Count} image(s)", $"{_resizeFiles.Count} 張圖");

    private async void ResizeAdd_Click(object sender, RoutedEventArgs e)
    {
        var files = await FileDialogs.OpenFilesAsync(ImageResizeService.SupportedExtensions.ToArray());
        if (files is null) return;
        foreach (var f in files)
            if (!_resizeFiles.Contains(f)) _resizeFiles.Add(f);
        UpdateResizeCount();
    }

    private void ResizeClear_Click(object sender, RoutedEventArgs e) { _resizeFiles.Clear(); UpdateResizeCount(); }

    private void Preset_Changed(object sender, SelectionChangedEventArgs e)
    {
        int i = PresetBox.SelectedIndex;
        if (i < 0 || i >= ImageResizeService.Presets.Count) return;
        var p = ImageResizeService.Presets[i];
        WidthBox.Value = p.Width;
        HeightBox.Value = p.Height;
    }

    private async void OutFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync();
        if (folder is not null) OutFolderBox.Text = folder;
    }

    private async void ResizeRun_Click(object sender, RoutedEventArgs e)
    {
        if (_resizeFiles.Count == 0) { Info(InfoBarSeverity.Warning, P("Nothing to do", "冇嘢做"), P("Add some images first.", "請先加入圖片。")); return; }
        var outFolder = OutFolderBox.Text;
        if (string.IsNullOrWhiteSpace(outFolder)) { Info(InfoBarSeverity.Warning, P("Heads up", "注意"), P("Choose an output folder.", "請揀一個輸出資料夾。")); return; }

        int w = (int)(double.IsNaN(WidthBox.Value) ? 0 : WidthBox.Value);
        int h = (int)(double.IsNaN(HeightBox.Value) ? 0 : HeightBox.Value);
        int q = (int)(double.IsNaN(QualityBox.Value) ? 90 : QualityBox.Value);
        bool shrink = ShrinkOnlyChk.IsChecked == true;
        string suffix = SuffixBox.Text ?? "";

        ResizeRunBtn.IsEnabled = false;
        ResizeRing.IsActive = true;
        var sources = _resizeFiles.ToList();
        try
        {
            var results = await ImageResizeService.ResizeBatchAsync(
                sources, outFolder, w, h, shrink, q, suffix,
                (i, total, file) => DispatcherQueue.TryEnqueue(() =>
                    ResizeProgressText.Text = P($"{i}/{total} — {Path.GetFileName(file)}", $"{i}/{total} — {Path.GetFileName(file)}")));

            int ok = results.Count(r => r.Ok);
            int fail = results.Count - ok;
            ResizeProgressText.Text = "";
            if (fail == 0)
                Info(InfoBarSeverity.Success, P("Done", "完成"), P($"Resized {ok} image(s) into {outFolder}", $"已縮放 {ok} 張圖到 {outFolder}"));
            else
            {
                var firstErr = results.First(r => !r.Ok).Message;
                Info(InfoBarSeverity.Warning, P("Finished with errors", "完成但有錯誤"),
                    P($"{ok} done, {fail} failed. First error: {firstErr}", $"{ok} 成功、{fail} 失敗。第一個錯誤：{firstErr}"));
            }
        }
        catch (Exception ex) { Info(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
        finally { ResizeRing.IsActive = false; ResizeRunBtn.IsEnabled = true; }
    }

    // ===================== Text Extractor (OCR) =====================

    private async void OcrFull_Click(object sender, RoutedEventArgs e)
    {
        OcrFullBtn.IsEnabled = false;
        OcrRing.IsActive = true;
        try
        {
            Language? lang = OcrLangBox.SelectedItem as Language;
            var rect = TextExtractorService.VirtualScreen();
            // Capture/OCR off the UI thread where possible.
            var text = await Task.Run(async () => await TextExtractorService.ExtractTextAsync(rect, lang));
            OcrResult.Text = text;
            if (string.IsNullOrWhiteSpace(text))
                Info(InfoBarSeverity.Informational, P("No text found", "搵唔到文字"), P("The screen capture had no recognisable text.", "今次螢幕擷取冇辨識到文字。"));
            else
            {
                CopyTextToClipboard(text);
                Info(InfoBarSeverity.Success, P("Extracted", "已擷取"), P("Recognised text copied to the clipboard.", "辨識到嘅文字已複製去剪貼簿。"));
            }
        }
        catch (Exception ex) { Info(InfoBarSeverity.Error, P("OCR failed", "OCR 失敗"), ex.Message); }
        finally { OcrRing.IsActive = false; OcrFullBtn.IsEnabled = true; }
    }

    private void OcrCopy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(OcrResult.Text)) return;
        CopyTextToClipboard(OcrResult.Text);
        Info(InfoBarSeverity.Success, P("Copied", "已複製"), P("Text copied to the clipboard.", "文字已複製去剪貼簿。"));
    }

    private void OcrClear_Click(object sender, RoutedEventArgs e) => OcrResult.Text = "";

    private static void CopyTextToClipboard(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
    }

    // ===================== Always On Top =====================

    private void UpdateTopCount() => TopCountText.Text = P($"{AlwaysOnTopService.PinnedCount} pinned", $"{AlwaysOnTopService.PinnedCount} 個置頂");

    private void RefreshTopWindows()
    {
        var wins = WindowManager.List()
            .Select(w => new TopWin { Handle = w.Handle, Title = w.Title, Process = w.Process, Pinned = AlwaysOnTopService.IsPinned(w.Handle) })
            .ToList();
        TopList.ItemsSource = wins;
        UpdateTopCount();
    }

    private void TopRefresh_Click(object sender, RoutedEventArgs e) => RefreshTopWindows();

    private void TopUnpinAll_Click(object sender, RoutedEventArgs e)
    {
        AlwaysOnTopService.UnpinAll();
        RefreshTopWindows();
        Info(InfoBarSeverity.Success, P("Un-pinned", "已取消"), P("All windows returned to normal z-order.", "全部視窗已還原正常層級。"));
    }

    private void TopPin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb || tb.Tag is not TopWin win) return;
        bool pinned = AlwaysOnTopService.Toggle(win.Handle);
        win.Pinned = pinned;
        tb.IsChecked = pinned;
        UpdateTopCount();
    }

    // ===================== Paste as Plain Text =====================

    private async void PasteStrip_Click(object sender, RoutedEventArgs e)
    {
        bool ok = await PlainTextPasteService.StripToPlainTextAsync();
        if (ok) Info(InfoBarSeverity.Success, P("Stripped", "已淨化"), P("Clipboard is now plain text — paste anywhere.", "剪貼簿而家係純文字 — 隨處貼上即可。"));
        else Info(InfoBarSeverity.Warning, P("No text", "冇文字"), P("The clipboard has no text to strip.", "剪貼簿冇文字可以淨化。"));
    }

    private void PasteHotkey_Toggled(object sender, RoutedEventArgs e)
    {
        if (PasteHotkeySwitch.IsOn)
        {
            PlainTextPasteService.EnableHotkey(DispatcherQueue);
            Info(InfoBarSeverity.Success, P("Hotkey on", "熱鍵已開"), P($"Press {PlainTextPasteService.HotkeyText} anywhere to paste as plain text.", $"喺任何地方撳 {PlainTextPasteService.HotkeyText} 即可純文字貼上。"));
        }
        else
        {
            PlainTextPasteService.DisableHotkey();
            Info(InfoBarSeverity.Informational, P("Hotkey off", "熱鍵已關"), P("Ctrl+Shift+V works normally again.", "Ctrl+Shift+V 回復正常。"));
        }
    }
    // ===================== More PowerToys =====================

    private static void OpenModule(string tag) => Navigator.GoToModule?.Invoke(tag);

    private void MoreScreenRuler_Click(object sender, RoutedEventArgs e) => OpenModule("module.screenruler");
    private void MoreMouseUtils_Click(object sender, RoutedEventArgs e) => OpenModule("module.mouseutils");
    private void MoreQuickAccent_Click(object sender, RoutedEventArgs e) => OpenModule("module.quickaccent");
    private void MoreShortcutGuide_Click(object sender, RoutedEventArgs e) => OpenModule("module.shortcutguide");
    private void MoreTextOcr_Click(object sender, RoutedEventArgs e) => OpenModule("module.textocr");
    private void MoreAwake_Click(object sender, RoutedEventArgs e) => OpenModule("module.awake");
    private void MoreColorPicker_Click(object sender, RoutedEventArgs e) => OpenModule("module.colorpicker");
    private void MorePeek_Click(object sender, RoutedEventArgs e) => OpenModule("module.peek");
    private void MoreWorkspaces_Click(object sender, RoutedEventArgs e) => OpenModule("module.workspaces");
    private void MoreFancyZones_Click(object sender, RoutedEventArgs e) => OpenModule("module.fancyzones");
    private void MoreNewPlus_Click(object sender, RoutedEventArgs e) => OpenModule("module.newplus");
    private void MoreAdvancedPaste_Click(object sender, RoutedEventArgs e) => OpenModule("module.advancedpaste");
    private void MoreCmdNotFound_Click(object sender, RoutedEventArgs e) => OpenModule("module.cmdnotfound");
    private void MoreRegistry_Click(object sender, RoutedEventArgs e) => OpenModule("module.regedit");
    private void MoreRename_Click(object sender, RoutedEventArgs e) => OpenModule("module.rename");
    private void MoreKeyboard_Click(object sender, RoutedEventArgs e) => OpenModule("module.keyboard");
    private void MoreHosts_Click(object sender, RoutedEventArgs e) => OpenModule("module.hosts");
    private void MoreEnvVars_Click(object sender, RoutedEventArgs e) => OpenModule("module.envvars");
    private void MoreMwb_Click(object sender, RoutedEventArgs e) => OpenModule("module.mwb");
    private void MoreCmdPalette_Click(object sender, RoutedEventArgs e) => OpenModule("module.cmdpalette");
}
