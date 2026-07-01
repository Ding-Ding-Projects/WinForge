using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生螢幕及圖片文字辨識（NormCap／Text Extractor 式）· A dedicated, standalone OCR module built on the
/// BUILT-IN Windows OCR engine (Windows.Media.Ocr, WinRT) — pure managed C#, NO Tesseract, NO external tool.
/// 拖一個螢幕區域或者揀一張圖片，即時辨識文字；可揀語言、複製、清除，仲有一節內嘅辨識歷史。
/// Drag a screen region or pick an image file, recognise the text, choose the OCR language, copy/clear the
/// result, and browse a small in-session history of recent captures.
/// </summary>
public sealed partial class TextOcrModule : Page
{
    private readonly ObservableCollection<OcrHistoryItem> _history = new();

    public TextOcrModule()
    {
        InitializeComponent();
        HistoryList.ItemsSource = _history;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object? s, RoutedEventArgs e)
    {
        BuildLanguageList();
        Render();
        CheckEngine();
    }

    private void Render()
    {
        Header.Title = "Text Extractor (OCR) · 原生文字辨識";
        HeaderBlurb.Text = P(
            "Pull text out of anything on screen or out of an image file using the built-in Windows OCR engine — no external tools, nothing to install. Drag a region or open a picture, pick the recognition language, then copy the text.",
            "用內置嘅 Windows OCR 引擎，由螢幕上任何嘢或者圖片檔抽取文字 — 唔使外部工具、唔使裝任何嘢。拖一個區域或者開一張圖、揀辨識語言，再複製文字。");

        CaptureLbl.Text = P("Capture region · 擷取區域", "擷取區域");
        FileLbl.Text = P("OCR image file · 辨識圖片檔", "辨識圖片檔");
        LangLbl.Text = P("Language · 語言", "語言");

        ResultHeader.Text = P("Recognised text · 辨識結果", "辨識結果");
        CopyLbl.Text = P("Copy · 複製", "複製");
        ClearLbl.Text = P("Clear · 清除", "清除");
        ResultBox.PlaceholderText = P("Recognised text appears here — it's editable before you copy.",
            "辨識到嘅文字會喺呢度顯示 — 複製前可以直接編輯。");

        HistoryHeader.Text = P("History · 歷史", "歷史");
        HistoryEmptyText.Text = P("Recent captures from this session show up here.", "今次工作階段嘅最近辨識會喺呢度出現。");

        UpdateStats();
        UpdateHistoryVisibility();
    }

    private void BuildLanguageList()
    {
        LangBox.Items.Clear();
        var langs = OcrService.AvailableLanguages();
        // First entry follows the user's profile languages (engine default).
        LangBox.Items.Add(new ComboBoxItem { Content = P("Auto (profile) · 自動（設定檔）", "自動（設定檔）"), Tag = null });
        foreach (var l in langs)
        {
            // Surface Traditional Chinese explicitly when present.
            string label = l.LanguageTag.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase)
                ? $"{l.DisplayName} · 繁體中文 (zh-Hant)"
                : l.DisplayName;
            LangBox.Items.Add(new ComboBoxItem { Content = label, Tag = l });
        }
        LangBox.SelectedIndex = 0;
    }

    private void CheckEngine()
    {
        if (OcrService.IsAvailable())
        {
            EngineBar.IsOpen = false;
            return;
        }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("No OCR language installed", "未安裝 OCR 語言");
        EngineBar.Message = P(
            "Windows has no OCR language pack installed. Open Settings → Time & language → Language & region, add or open a language's options, and install its optional OCR feature. Then come back here.",
            "Windows 未裝任何 OCR 語言包。開 設定 → 時間與語言 → 語言與地區，加入或開啟某語言嘅選項，安裝其可選嘅 OCR 功能，再返嚟呢度。");
    }

    private Language? SelectedLanguage() => (LangBox.SelectedItem as ComboBoxItem)?.Tag as Language;

    // ── Capture a screen region ────────────────────────────────────────────────
    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        // Let the region selector paint over our own window first.
        var pick = RegionSelector.PickRegion();
        if (pick is not { } r) return;
        var rect = new ScreenRect(r.x, r.y, r.w, r.h);
        await RunOcr(
            () => OcrService.RecognizeRegionAsync(rect, SelectedLanguage()),
            P("Screen region", "螢幕區域"));
    }

    // ── OCR an image file ──────────────────────────────────────────────────────
    private async void File_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff");
        if (path is null) return;
        await RunOcr(
            () => OcrService.RecognizeFileAsync(path, SelectedLanguage()),
            Path.GetFileName(path));
    }

    private async Task RunOcr(Func<Task<OcrResultInfo>> op, string source)
    {
        Busy.IsActive = true;
        CaptureBtn.IsEnabled = FileBtn.IsEnabled = false;
        try
        {
            var info = await Task.Run(op);
            ResultBox.Text = info.Text;
            UpdateStats(info);

            if (string.IsNullOrWhiteSpace(info.Text))
            {
                ShowStatus(InfoBarSeverity.Informational,
                    P("No text found", "搵唔到文字"),
                    P("Nothing recognisable was found in that capture.", "今次擷取冇辨識到任何文字。"));
                return;
            }

            AddHistory(info, source);
            ShowStatus(InfoBarSeverity.Success,
                P("Recognised", "已辨識"),
                P($"{info.LineCount} line(s), {info.WordCount} word(s) from {source}.",
                  $"由「{source}」辨識到 {info.LineCount} 行、{info.WordCount} 個字。"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("OCR failed", "辨識失敗"), ex.Message);
        }
        finally
        {
            Busy.IsActive = false;
            CaptureBtn.IsEnabled = FileBtn.IsEnabled = true;
        }
    }

    // ── Result actions ─────────────────────────────────────────────────────────
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ResultBox.Text)) return;
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(ResultBox.Text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
        ShowStatus(InfoBarSeverity.Success, P("Copied", "已複製"),
            P("Text copied to the clipboard.", "文字已複製去剪貼簿。"));
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        ResultBox.Text = "";
        UpdateStats();
        StatusBar.IsOpen = false;
    }

    private void UpdateStats(OcrResultInfo? info = null)
    {
        if (info is { } i && !string.IsNullOrWhiteSpace(i.Text))
            StatsText.Text = P($"{i.LineCount} line(s) · {i.WordCount} word(s) · {i.Text.Length} char(s)",
                              $"{i.LineCount} 行 · {i.WordCount} 字 · {i.Text.Length} 個字元");
        else
        {
            int chars = ResultBox.Text?.Length ?? 0;
            StatsText.Text = chars == 0 ? "" :
                P($"{chars} char(s)", $"{chars} 個字元");
        }
    }

    // ── History ────────────────────────────────────────────────────────────────
    private void AddHistory(OcrResultInfo info, string source)
    {
        var preview = info.Text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (preview.Length > 120) preview = preview[..120] + "…";
        _history.Insert(0, new OcrHistoryItem
        {
            Text = info.Text,
            Preview = preview,
            Meta = $"{source} · {DateTime.Now:HH:mm:ss} · " +
                   P($"{info.LineCount}L {info.WordCount}W", $"{info.LineCount}行 {info.WordCount}字"),
        });
        while (_history.Count > 25) _history.RemoveAt(_history.Count - 1);
        UpdateHistoryVisibility();
    }

    private void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is OcrHistoryItem item)
        {
            ResultBox.Text = item.Text;
            UpdateStats(new OcrResultInfo(item.Text, 0, 0));
            // Recompute proper stats from the stored text.
            UpdateStatsFromText(item.Text);
        }
    }

    private void UpdateStatsFromText(string text)
    {
        var lines = text.Split('\n').Count(l => l.Trim().Length > 0);
        var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        StatsText.Text = P($"{lines} line(s) · {words} word(s) · {text.Length} char(s)",
                          $"{lines} 行 · {words} 字 · {text.Length} 個字元");
    }

    private void HistoryClear_Click(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        UpdateHistoryVisibility();
    }

    private void UpdateHistoryVisibility()
    {
        bool empty = _history.Count == 0;
        HistoryEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        HistoryList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowStatus(InfoBarSeverity sev, string title, string message)
    {
        StatusBar.Severity = sev;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}

/// <summary>一條 OCR 歷史紀錄 · One in-session OCR history entry.</summary>
public sealed class OcrHistoryItem
{
    public string Text { get; init; } = "";
    public string Preview { get; init; } = "";
    public string Meta { get; init; } = "";
}
