using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 語音朗讀（文字轉語音）· Voice / Read-Aloud module — type text, pick an installed voice, set rate &amp;
/// volume, then Play (SpeakAsync), Stop, or Export to a WAV file. Uses the built-in Windows SAPI engine
/// (System.Speech). 100% in-app, no redirects. Bilingual.
/// </summary>
public sealed partial class VoiceModule : Page
{
    public VoiceModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        VoiceService.SpeakingChanged += OnSpeakingChanged;
        Loaded += (_, _) => { Render(); LoadVoices(); SyncButtons(); };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            VoiceService.SpeakingChanged -= OnSpeakingChanged;
            VoiceService.Stop();
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Voice & Read-Aloud · 語音朗讀";
        HeaderBlurb.Text = P("Read any text aloud with a built-in Windows voice, or export it to a WAV file. Choose a voice, adjust the speed and volume, then Play, Stop or Export.",
            "用 Windows 內置語音將文字讀出嚟，或者出成 WAV 檔。揀把聲、調速度同音量，跟住撳播放、停止或者匯出。");
        TextLabel.Text = P("Text to read aloud", "要朗讀嘅文字");
        TextBox.PlaceholderText = P("Type or paste text here…", "喺呢度打字或者貼上文字…");
        VoiceLabel.Text = P("Voice", "語音");
        RateLabel.Text = P("Speed (-10 … +10)", "速度（-10 … +10）");
        VolumeLabel.Text = P("Volume (0 … 100)", "音量（0 … 100）");
        PlayText.Text = P("Play", "播放");
        StopText.Text = P("Stop", "停止");
        ExportText.Text = P("Export WAV…", "匯出 WAV…");
    }

    private void LoadVoices()
    {
        var voices = VoiceService.GetVoices();
        VoiceCombo.Items.Clear();
        foreach (var v in voices)
            VoiceCombo.Items.Add(new ComboBoxItem { Content = v.Display, Tag = v.Name });

        if (voices.Count == 0)
        {
            NoVoiceBar.Title = P("No voices found", "搵唔到語音");
            NoVoiceBar.Message = P("No installed text-to-speech voices were detected. Add a voice under Windows Settings → Time & language → Speech.",
                "搵唔到已安裝嘅文字轉語音語音。可以喺 Windows 設定 → 時間與語言 → 語音 度加。");
            NoVoiceBar.IsOpen = true;
            PlayBtn.IsEnabled = false;
            ExportBtn.IsEnabled = false;
        }
        else
        {
            NoVoiceBar.IsOpen = false;
            VoiceCombo.SelectedIndex = 0;
        }
    }

    private string? SelectedVoice =>
        (VoiceCombo.SelectedItem as ComboBoxItem)?.Tag as string;

    private void OnSpeakingChanged(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess) SyncButtons();
        else DispatcherQueue.TryEnqueue(SyncButtons);
    }

    private void SyncButtons()
    {
        bool speaking = VoiceService.IsSpeaking;
        bool hasVoice = VoiceCombo.Items.Count > 0;
        StopBtn.IsEnabled = speaking;
        PlayBtn.IsEnabled = hasVoice && !speaking;
        ExportBtn.IsEnabled = hasVoice && !speaking;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        var text = TextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowResult(InfoBarSeverity.Warning, P("Nothing to read", "冇文字可讀"),
                P("Type some text first.", "請先輸入文字。"));
            return;
        }
        ResultBar.IsOpen = false;
        VoiceService.Speak(text, SelectedVoice, (int)RateSlider.Value, (int)VolumeSlider.Value);
        SyncButtons();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        VoiceService.Stop();
        SyncButtons();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var text = TextBox.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            ShowResult(InfoBarSeverity.Warning, P("Nothing to export", "冇文字可匯出"),
                P("Type some text first.", "請先輸入文字。"));
            return;
        }

        var path = await FileDialogs.SaveFileAsync($"WinForge-speech-{DateTime.Now:yyyyMMdd-HHmmss}", ".wav");
        if (path is null) return;

        ResultBar.IsOpen = false;
        ExportBtn.IsEnabled = false;
        try
        {
            await VoiceService.ExportWavAsync(text, SelectedVoice, (int)RateSlider.Value, (int)VolumeSlider.Value, path);
            ShowResult(InfoBarSeverity.Success, P("Exported", "已匯出"), path);
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, P("Export failed", "匯出失敗"), ex.Message);
        }
        finally
        {
            SyncButtons();
        }
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        ResultBar.Severity = severity;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }
}
