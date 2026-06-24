using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內螢幕錄影 · In-app screen recorder (ffmpeg gdigrab) — records the whole desktop, with a
/// live timer and graceful stop. No external tool, no redirect. Bilingual.
/// </summary>
public sealed partial class ScreenRecorderModule : Page
{
    private string _output = "";
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _elapsed;

    public ScreenRecorderModule()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => { _elapsed++; UpdateStatus(); };
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); DefaultOutput(); SyncButtons(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Screen Recorder · 螢幕錄影";
        HeaderBlurb.Text = P("Record the whole desktop — including File Explorer and the Start menu — which Xbox Game Bar can't. Saved as MP4 (H.264). Video only for now.",
            "錄成個桌面 — 連檔案總管同開始功能表都得 — Xbox Game Bar 做唔到。存做 MP4 (H.264)。暫時淨係錄畫面。");
        OutCap.Text = P("Save to", "存去");
        ChangeBtn.Content = P("Change…", "更改…");
        FpsCap.Text = P("Frame rate (fps)", "幀率 (fps)");
        RecordBtn.Content = P("● Record", "● 開始錄影");
        StopBtn.Content = P("■ Stop", "■ 停止");

        if (!MediaService.IsInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("ffmpeg not found", "搵唔到 ffmpeg");
            EngineBar.Message = P("Click to install ffmpeg automatically (winget) — no restart needed.", "撳一下自動安裝 ffmpeg（winget）— 唔使重開。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                "Gyan.FFmpeg", "Install ffmpeg automatically", "自動安裝 ffmpeg",
                () => { Render(); return Task.CompletedTask; }, MediaService.Rescan);
        }
        else { EngineBar.IsOpen = false; EngineBar.ActionButton = null; }
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

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        if (ScreenRecorder.IsRecording) return;
        if (string.IsNullOrEmpty(_output)) DefaultOutput();
        var r = ScreenRecorder.Start(_output, (int)FpsBox.Value);
        if (r.Success)
        {
            _elapsed = 0;
            _timer.Start();
            ResultBar.IsOpen = false;
        }
        else
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            ResultBar.Message = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
            ResultBar.IsOpen = true;
        }
        SyncButtons();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        var r = await ScreenRecorder.Stop();
        SyncButtons();
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Saved", "已儲存") : P("Failed", "失敗");
        ResultBar.Message = r.Success ? _output : ((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "");
        ResultBar.IsOpen = true;
        DefaultOutput(); // fresh filename for the next take
    }

    private void SyncButtons()
    {
        bool rec = ScreenRecorder.IsRecording;
        RecordBtn.IsEnabled = !rec;
        StopBtn.IsEnabled = rec;
        ChangeBtn.IsEnabled = !rec;
        FpsBox.IsEnabled = !rec;
        Dot.Visibility = rec ? Visibility.Visible : Visibility.Collapsed;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (ScreenRecorder.IsRecording)
            StatusText.Text = P($"REC  {_elapsed / 60:00}:{_elapsed % 60:00}", $"錄緊  {_elapsed / 60:00}:{_elapsed % 60:00}");
        else
            StatusText.Text = P("Idle", "閒置");
    }
}
