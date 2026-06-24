using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 擷取工作室 · Capture Studio — region screen-record to MP4/GIF, instant rectangular snip to the
/// clipboard, and OCR text from a screen region or image file. A transparent overlay supplies the
/// region. All in-app (ffmpeg + Windows.Media.Ocr), no redirect. Bilingual.
/// </summary>
public sealed partial class CaptureStudioModule : Page
{
    private string _output = "";
    private byte[]? _lastSnip;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _elapsed;

    public CaptureStudioModule()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => { _elapsed++; UpdateStatus(); };
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); DefaultOutput(); SyncButtons(); RefreshOcrLangs(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private string Msg(TweakResult r) => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Render()
    {
        HeaderTitle.Text = "Capture Studio · 擷取工作室";
        HeaderBlurb.Text = P("Record a region of the screen to MP4 or GIF, snip a rectangle straight to the clipboard, or pull text out of any region or image with OCR. Everything runs in-app — no redirects.",
            "錄螢幕一忽做 MP4 或 GIF、㩒個矩形截圖直入剪貼簿、或者用 OCR 由任何區域／圖片認返啲字出嚟。全部喺 app 內做，唔會跳走。");

        RecLabel.Text = P("Region screen-record → MP4 / GIF", "區域螢幕錄影 → MP4／GIF");
        RecBlurb.Text = P("Click Record, then drag a rectangle on screen. Esc or right-click cancels. Saved as H.264 MP4; tick GIF to also make a high-quality GIF.",
            "撳「開始錄影」之後喺螢幕拖一個框。Esc 或右鍵取消。存做 H.264 MP4；剔咗 GIF 仲會整埋一個高質 GIF。");
        OutCap.Text = P("Save to", "存去");
        ChangeBtn.Content = P("Change…", "更改…");
        FpsCap.Text = P("Frame rate (fps)", "幀率 (fps)");
        GifChk.Content = P("Also make a GIF", "順手整 GIF");
        RecordBtn.Content = P("● Record region", "● 錄影區域");
        StopBtn.Content = P("■ Stop", "■ 停止");

        SnipLabel.Text = P("Instant snip → clipboard", "即時截圖 → 剪貼簿");
        SnipBlurb.Text = P("Drag a rectangle; the snip lands on the clipboard right away (paste anywhere). You can also save it as a PNG.",
            "拖一個框，截圖即刻入剪貼簿（任何地方都貼到）。亦可以存做 PNG。");
        SnipBtn.Content = P("Snip to clipboard", "截圖入剪貼簿");
        SaveSnipBtn.Content = P("Save as PNG…", "存做 PNG…");

        OcrLabel.Text = P("OCR — text from a region or image", "OCR — 由區域或圖片認字");
        OcrBlurb.Text = P("Recognise text from a dragged screen region or an image file. The result is copied to the clipboard.",
            "由拖出嚟嘅螢幕區域或者圖檔認返文字。結果會複製到剪貼簿。");
        OcrRegionBtn.Content = P("OCR a region", "OCR 一個區域");
        OcrFileBtn.Content = P("OCR an image file…", "OCR 一個圖檔…");

        if (!MediaService.IsInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("ffmpeg not found", "搵唔到 ffmpeg");
            EngineBar.Message = P("Install ffmpeg (winget install Gyan.FFmpeg) to record video and make GIFs. Snip and OCR still work without it.",
                "請安裝 ffmpeg（winget install Gyan.FFmpeg）先錄到片同整 GIF。截圖同 OCR 唔使佢都用得。");
        }
        else { EngineBar.IsOpen = false; }

        RefreshOcrLangs();
    }

    private void RefreshOcrLangs()
    {
        OcrLangs.Text = P("Installed OCR languages: ", "已安裝 OCR 語言：") + CaptureService.AvailableLanguagesSummary;
        if (!CaptureService.HasChineseRecognizer)
        {
            OcrLangBar.IsOpen = true;
            OcrLangBar.Title = P("No Chinese OCR recognizer", "未有中文 OCR 辨識器");
            OcrLangBar.Message = P("To recognise Chinese (zh-Hant / zh-Hans), add the language in Settings › Time & language › Language & region (include the optional text-recognition feature). English and other installed languages still work.",
                "想認中文（繁／簡），請喺「設定 › 時間與語言 › 語言與地區」加入該語言（連同「文字辨識」可選功能）。英文同其他已安裝語言照樣用得。");
        }
        else { OcrLangBar.IsOpen = false; }
    }

    private void DefaultOutput()
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        _output = Path.Combine(dir, $"WinTune-{DateTime.Now:yyyyMMdd-HHmmss}.mp4");
        OutputBox.Text = _output;
    }

    private async void Change_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync($"WinTune-{DateTime.Now:yyyyMMdd-HHmmss}", ".mp4");
        if (path is not null) { _output = path; OutputBox.Text = _output; }
    }

    // ---------- region recording ----------

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (CaptureService.IsRecording) return;
        if (string.IsNullOrEmpty(_output)) DefaultOutput();

        var region = RegionSelector.PickRegion();
        if (region is null) { ShowBar(RecBar, false, P("Cancelled", "已取消"), P("No region was selected.", "未揀區域。")); return; }

        var (x, y, w, h) = region.Value;
        var r = CaptureService.StartRegionRecording(x, y, w, h, (int)FpsBox.Value, _output, GifChk.IsChecked == true, 15);
        if (r.Success)
        {
            _elapsed = 0;
            _timer.Start();
            RecBar.IsOpen = false;
        }
        else
        {
            ShowBar(RecBar, false, P("Failed", "失敗"), Msg(r));
        }
        SyncButtons();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        StopBtn.IsEnabled = false;
        var r = await CaptureService.StopRegionRecording();
        SyncButtons();
        ShowBar(RecBar, r.Success, r.Success ? P("Saved", "已儲存") : P("Failed", "失敗"),
            r.Success ? _output : Msg(r));
        DefaultOutput();
    }

    private void SyncButtons()
    {
        bool rec = CaptureService.IsRecording;
        RecordBtn.IsEnabled = !rec;
        StopBtn.IsEnabled = rec;
        ChangeBtn.IsEnabled = !rec;
        FpsBox.IsEnabled = !rec;
        GifChk.IsEnabled = !rec;
        Dot.Visibility = rec ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        StatusText.Text = CaptureService.IsRecording
            ? P($"REC  {_elapsed / 60:00}:{_elapsed % 60:00}", $"錄緊  {_elapsed / 60:00}:{_elapsed % 60:00}")
            : P("Idle", "閒置");
    }

    // ---------- snip ----------

    private async void Snip_Click(object sender, RoutedEventArgs e)
    {
        var region = RegionSelector.PickRegion();
        if (region is null) { ShowBar(SnipBar, false, P("Cancelled", "已取消"), P("No region was selected.", "未揀區域。")); return; }

        var (x, y, w, h) = region.Value;
        var (r, png) = await CaptureService.SnipToClipboard(x, y, w, h);
        if (r.Success && png is not null)
        {
            _lastSnip = png;
            await ShowPreview(png);
            SaveSnipBtn.IsEnabled = true;
        }
        ShowBar(SnipBar, r.Success, r.Success ? P("Copied", "已複製") : P("Failed", "失敗"), Msg(r));
    }

    private async Task ShowPreview(byte[] png)
    {
        var bmp = new BitmapImage();
        using var ms = new MemoryStream(png);
        await bmp.SetSourceAsync(ms.AsRandomAccessStream());
        SnipPreview.Source = bmp;
        PreviewBorder.Visibility = Visibility.Visible;
    }

    private async void SaveSnip_Click(object sender, RoutedEventArgs e)
    {
        if (_lastSnip is null) return;
        var path = await FileDialogs.SaveFileAsync($"WinTune-snip-{DateTime.Now:yyyyMMdd-HHmmss}", ".png");
        if (path is null) return;
        var r = await CaptureService.SavePng(_lastSnip, path);
        ShowBar(SnipBar, r.Success, r.Success ? P("Saved", "已儲存") : P("Failed", "失敗"),
            r.Success ? path : Msg(r));
    }

    // ---------- OCR ----------

    private async void OcrRegion_Click(object sender, RoutedEventArgs e)
    {
        var region = RegionSelector.PickRegion();
        if (region is null) { ShowBar(OcrBar, false, P("Cancelled", "已取消"), P("No region was selected.", "未揀區域。")); return; }

        var (x, y, w, h) = region.Value;
        var (r, text) = await CaptureService.OcrRegion(x, y, w, h);
        ShowOcr(r, text);
    }

    private async void OcrFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff");
        if (path is null) return;
        var (r, text) = await CaptureService.OcrFile(path);
        ShowOcr(r, text);
    }

    private void ShowOcr(TweakResult r, string? text)
    {
        if (text is not null)
        {
            OcrResult.Text = text;
            OcrResult.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
        }
        ShowBar(OcrBar, r.Success, r.Success ? P("Done", "完成") : P("Failed", "失敗"), Msg(r));
    }

    // ---------- helper ----------

    private static void ShowBar(InfoBar bar, bool ok, string title, string message)
    {
        bar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        bar.Title = title;
        bar.Message = message;
        bar.IsOpen = true;
    }
}
