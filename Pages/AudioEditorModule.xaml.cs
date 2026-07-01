using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 音訊編輯器 · In-app audio editor: open/record a clip, view + select its waveform,
/// apply ffmpeg-backed effects (trim/fade/normalize/gain/speed/pitch/noise + ~40 more), mix/concat, and export.
/// 原生波形繪製 + 內建 MediaPlayer 播放 + ffmpeg 效果引擎。Native waveform render, in-box MediaPlayer playback,
/// ffmpeg effects engine — no GPL linking, no extra NuGet. All strings bilingual (English · 粵語).
/// </summary>
public sealed partial class AudioEditorModule : Page
{
    private static readonly string[] AudioExts =
        { ".wav", ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aiff", ".aif" };

    private List<TweakDefinition>? _fx;
    private MediaPlayer? _player;
    private DispatcherTimer? _playTimer;

    private float[] _peaks = Array.Empty<float>();
    private double _durationSec;
    private double _selStart, _selEnd;       // seconds
    private bool _dragging;
    private double _dragAnchorX;
    private bool _busy;
    private bool _rowBusy;   // guard so only one effect-row action runs at a time

    public AudioEditorModule()
    {
        InitializeComponent();
        GainSlider.ValueChanged += GainSlider_ValueChanged;
        SpeedSlider.ValueChanged += SpeedSlider_ValueChanged;
        PitchSlider.ValueChanged += PitchSlider_ValueChanged;
        Loc.I.LanguageChanged += OnLang;
        AppState.AudioClipChanged += OnClipChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private string ClipPath => AppState.CurrentAudioClip;
    private bool HasClipPath => !string.IsNullOrWhiteSpace(ClipPath) && File.Exists(ClipPath);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        PopulateFx(string.Empty);
        await RefreshDevicesAsync();
        if (HasClipPath) await ReloadClipAsync();
        else UpdateClipUi();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        AppState.AudioClipChanged -= OnClipChanged;
        StopTimer();
        try { _player?.Dispose(); } catch { }
        _player = null;
    }

    private void OnLang(object? sender, EventArgs e) { Render(); PopulateFx(FxFilter.Text ?? string.Empty); }

    private async void OnClipChanged(object? sender, EventArgs e)
    {
        // An effect (from an effect row or a transport button) swapped in a new clip — reload the waveform.
        if (DispatcherQueue.HasThreadAccess) await ReloadClipAsync();
        else DispatcherQueue.TryEnqueue(async () => await ReloadClipAsync());
    }

    // ===================== text / engine bars =====================

    private void Render()
    {
        Header.Title = "Audio Editor · 音訊編輯器";
        HeaderBlurb.Text = P("Open or record a clip, see and select its waveform, apply effects (trim, fade, normalize, gain, speed, pitch, noise reduction and more) and export — all in-app.",
            "開檔或者錄音、睇同揀波形、套效果（剪裁、淡入淡出、正規化、增益、變速、變調、降噪等等）再匯出 — 全部喺 app 內。");

        ScopeBar.Title = P("Focused in-app editor", "精簡 app 內編輯器");
        ScopeBar.Message = P("Effects are destructive and operate on a working copy; use Revert to reload the original. Advanced multitrack, label-track and plug-in workflows are outside this focused in-app editor.",
            "效果係破壞性嘅，喺工作副本上做；撳「還原」可重載原檔。進階多軌、標籤軌同外掛流程唔屬於呢個精簡 app 內編輯器。");

        SourceLabel.Text = P("Source — open a file or record", "來源 — 開檔或錄音");
        OpenBtn.Content = P("Open audio…", "開啟音訊…");
        MicCap.Text = P("Mic", "麥克風");
        RefreshDevicesBtn.Content = P("Refresh", "重新整理");
        UpdateRecordButton();

        WaveLabel.Text = P("Waveform — drag to select a range", "波形 — 拖曳揀範圍");
        WavePlaceholder.Text = P("Open or record audio to see its waveform here.", "開檔或錄音，波形會喺呢度顯示。");
        PlayBtn.Content = P("▶ Play", "▶ 播放");
        PauseBtn.Content = P("⏸ Pause", "⏸ 暫停");
        StopBtn.Content = P("⏹ Stop", "⏹ 停止");
        PlaySelBtn.Content = P("Play selection", "播放選取");
        SelectAllBtn.Content = P("Select all", "全選");
        ClearSelBtn.Content = P("Clear", "清除選取");

        EditLabel.Text = P("Edit selection", "編輯選取");
        TrimBtn.Content = P("Trim to selection", "剪裁成選取");
        DeleteBtn.Content = P("Delete selection", "刪除選取");
        SilenceBtn.Content = P("Silence selection", "靜音選取");
        EditHint.Text = P("Trim keeps only the selected range; Delete removes it; Silence mutes it.",
            "「剪裁」只留選取範圍；「刪除」移除佢；「靜音」將佢調靜。");

        FadeLabel.Text = P("Fade & normalize", "淡化與正規化");
        FadeSecCap.Text = P("Seconds", "秒數");
        FadeInBtn.Content = P("Fade in", "淡入");
        FadeOutBtn.Content = P("Fade out", "淡出");
        NormalizeBtn.Content = P("Normalize", "正規化");

        GainBtn.Content = P("Apply gain", "套用增益");
        SpeedBtn.Content = P("Apply speed", "套用速度");
        PitchBtn.Content = P("Apply pitch", "套用音高");
        DenoiseBtn.Content = P("Noise reduction", "降噪");
        UpdateSliderLabels();

        MixLabel.Text = P("Mix · concat · reverse", "混音 · 串接 · 倒轉");
        MixBtn.Content = P("Mix with file…", "與檔混音…");
        ConcatBtn.Content = P("Append file…", "接駁檔案…");
        ReverseBtn.Content = P("Reverse", "倒轉");
        ExportBtn.Content = P("Export…", "匯出…");
        RevertBtn.Content = P("Revert to original", "還原原檔");

        EffectsHeader.Text = P($"All effects ({(_fx ??= AudioEffectsOperations.All().ToList()).Count})",
            $"全部效果（{(_fx ??= AudioEffectsOperations.All().ToList()).Count}）");
        EffectsBlurb.Text = P("Each effect applies to the whole current clip and chains onto the previous one.",
            "每個效果套用喺成段目前嘅 clip，並接喺上一個之後。");
        FxFilter.PlaceholderText = P("Filter effects…", "篩選效果…");

        UpdateEngineBar();
    }

    private void UpdateEngineBar()
    {
        if (!FfmpegAudioService.IsInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("ffmpeg not found", "搵唔到 ffmpeg");
            EngineBar.Message = P("Recording, playback decoding and all effects need ffmpeg. Click to install it automatically (winget) — no restart needed.",
                "錄音、解碼播放同所有效果都要 ffmpeg。撳一下自動安裝（winget）— 唔使重開。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                "Gyan.FFmpeg", "Install ffmpeg automatically", "自動安裝 ffmpeg",
                async () => { FfmpegAudioService.Rescan(); Render(); await RefreshDevicesAsync(); }, FfmpegAudioService.Rescan);
        }
        else { EngineBar.IsOpen = false; EngineBar.ActionButton = null; }
    }

    private void UpdateSliderLabels()
    {
        GainLabel.Text = P($"Gain: {GainSlider.Value:+0;-0;0} dB", $"增益：{GainSlider.Value:+0;-0;0} dB");
        SpeedLabel.Text = P($"Speed: {SpeedSlider.Value:0.00}×", $"速度：{SpeedSlider.Value:0.00}×");
        PitchLabel.Text = P($"Pitch: {PitchSlider.Value:+0;-0;0} semitones", $"音高：{PitchSlider.Value:+0;-0;0} 半音");
    }

    private void GainSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) => UpdateSliderLabels();
    private void SpeedSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) => UpdateSliderLabels();
    private void PitchSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) => UpdateSliderLabels();

    // ===================== devices / record =====================

    private async Task RefreshDevicesAsync()
    {
        DeviceCombo.Items.Clear();
        var devices = await AudioEngineService.ListInputDevicesAsync();
        foreach (var d in devices) DeviceCombo.Items.Add(d);
        if (DeviceCombo.Items.Count > 0) DeviceCombo.SelectedIndex = 0;
        RecordBtn.IsEnabled = FfmpegAudioService.IsInstalled;
        if (DeviceCombo.Items.Count == 0)
        {
            DeviceCombo.PlaceholderText = FfmpegAudioService.IsInstalled
                ? P("No microphone found", "搵唔到麥克風")
                : P("Install ffmpeg first", "請先安裝 ffmpeg");
        }
    }

    private async void RefreshDevices_Click(object sender, RoutedEventArgs e) => await RefreshDevicesAsync();

    private void UpdateRecordButton()
    {
        RecordBtn.Content = AudioEngineService.IsRecording ? P("⏹ Stop recording", "⏹ 停止錄音") : P("⏺ Record", "⏺ 錄音");
    }

    private async void Record_Click(object sender, RoutedEventArgs e)
    {
        if (AudioEngineService.IsRecording)
        {
            var (res, path) = await AudioEngineService.StopRecordingAsync();
            UpdateRecordButton();
            ShowStatus(res);
            if (path is not null) { AppState.CurrentAudioClip = path; }
            return;
        }
        if (DeviceCombo.SelectedItem is not string dev || string.IsNullOrWhiteSpace(dev))
        {
            ShowStatus(TweakResult.Fail("Select a microphone first.", "請先揀麥克風。"));
            return;
        }
        StopPlayback();
        var r = AudioEngineService.StartRecording(dev);
        UpdateRecordButton();
        ShowStatus(r);
    }

    // ===================== open / load =====================

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(AudioExts);
        if (path is null) return;
        StopPlayback();
        // Decode to a fresh WAV scratch so editing always works on a uniform, seekable format.
        if (!FfmpegAudioService.IsInstalled)
        {
            // Without ffmpeg we can still load a WAV directly for viewing/playing.
            AppState.CurrentAudioClip = path;
            return;
        }
        ShowStatus(TweakResult.Ok("Loading…", "載入緊…"));
        var (r, wav) = await FfmpegAudioService.RunAsync(path, "-i {in} -c:a pcm_s16le {out}");
        if (r.Success && wav is not null) AppState.CurrentAudioClip = wav;
        else { AppState.CurrentAudioClip = path; }   // fall back to the original
        ShowStatus(TweakResult.Ok("Loaded.", "已載入。"));
    }

    private async Task ReloadClipAsync()
    {
        StopPlayback();
        UpdateClipUi();
        if (!HasClipPath) { _peaks = Array.Empty<float>(); _durationSec = 0; DrawWaveform(); return; }

        _durationSec = await AudioEngineService.GetDurationSecondsAsync(ClipPath) ?? 0;
        _selStart = 0; _selEnd = 0;
        UpdateSelectionUi();
        UpdateTimeText(0);

        WavePlaceholder.Text = P("Reading waveform…", "讀緊波形…");
        _peaks = await AudioEngineService.ExtractPeaksAsync(ClipPath, 1400);
        WavePlaceholder.Visibility = _peaks.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_peaks.Length == 0)
            WavePlaceholder.Text = P("Could not read waveform (need ffmpeg).", "讀唔到波形（要 ffmpeg）。");
        DrawWaveform();
    }

    private void UpdateClipUi()
    {
        bool has = HasClipPath;
        foreach (var b in new[] { PlayBtn, PauseBtn, StopBtn, PlaySelBtn, SelectAllBtn, ClearSelBtn,
            TrimBtn, DeleteBtn, SilenceBtn, FadeInBtn, FadeOutBtn, NormalizeBtn, GainBtn, SpeedBtn,
            PitchBtn, DenoiseBtn, MixBtn, ConcatBtn, ReverseBtn, ExportBtn, RevertBtn })
            b.IsEnabled = has;

        if (has)
        {
            var fi = new FileInfo(ClipPath);
            var name = System.IO.Path.GetFileName(ClipPath);
            ClipInfo.Text = P($"Loaded: {name}  ·  {fi.Length / 1024:N0} KB",
                $"已載入：{name}  ·  {fi.Length / 1024:N0} KB");
        }
        else ClipInfo.Text = P("No clip loaded.", "未載入音訊。");
    }

    // ===================== waveform drawing =====================

    private void Wave_SizeChanged(object sender, SizeChangedEventArgs e) { DrawWaveform(); UpdateSelectionUi(); }

    private void DrawWaveform()
    {
        WaveCanvas.Children.Clear();
        double w = WaveHost.ActualWidth, h = WaveHost.ActualHeight;
        if (w <= 0 || h <= 0 || _peaks.Length == 0) return;
        WavePlaceholder.Visibility = Visibility.Collapsed;

        var brush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        double mid = h / 2;
        // Choose a column count that fits the width; map peaks onto it.
        int cols = (int)Math.Max(1, Math.Min(_peaks.Length, w));
        double colW = w / cols;
        for (int i = 0; i < cols; i++)
        {
            int pi = (int)((double)i / cols * _peaks.Length);
            float amp = _peaks[Math.Min(pi, _peaks.Length - 1)];
            double barH = Math.Max(1, amp * (h - 6));
            var rect = new Rectangle
            {
                Width = Math.Max(1, colW - 0.5),
                Height = barH,
                Fill = brush,
                RadiusX = 0.5,
                RadiusY = 0.5,
            };
            Canvas.SetLeft(rect, i * colW);
            Canvas.SetTop(rect, mid - barH / 2);
            WaveCanvas.Children.Add(rect);
        }
    }

    // ===================== selection (pointer) =====================

    private void Wave_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!HasClipPath || _durationSec <= 0) return;
        _dragging = true;
        WaveHost.CapturePointer(e.Pointer);
        _dragAnchorX = e.GetCurrentPoint(WaveHost).Position.X;
        _selStart = _selEnd = XToTime(_dragAnchorX);
        UpdateSelectionUi();
    }

    private void Wave_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        double x = e.GetCurrentPoint(WaveHost).Position.X;
        double a = Math.Min(_dragAnchorX, x), b = Math.Max(_dragAnchorX, x);
        _selStart = XToTime(a); _selEnd = XToTime(b);
        UpdateSelectionUi();
    }

    private void Wave_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        WaveHost.ReleasePointerCapture(e.Pointer);
        // a near-zero drag = a click: clear selection (place a seek point)
        if (Math.Abs(_selEnd - _selStart) < 0.02) { _selEnd = _selStart; }
        UpdateSelectionUi();
    }

    private double XToTime(double x)
    {
        double w = WaveHost.ActualWidth;
        if (w <= 0) return 0;
        return Math.Clamp(x / w, 0, 1) * _durationSec;
    }

    private double TimeToX(double t)
    {
        if (_durationSec <= 0) return 0;
        return Math.Clamp(t / _durationSec, 0, 1) * WaveHost.ActualWidth;
    }

    private void UpdateSelectionUi()
    {
        AppState.AudioSelStart = _selStart;
        AppState.AudioSelEnd = _selEnd;
        if (_selEnd - _selStart > 0.01 && _durationSec > 0)
        {
            double x1 = TimeToX(_selStart), x2 = TimeToX(_selEnd);
            SelectionRect.Margin = new Thickness(x1, 0, 0, 0);
            SelectionRect.Width = Math.Max(0, x2 - x1);
            SelectionRect.Visibility = Visibility.Visible;
            SelText.Text = $"{Fmt(_selStart)} – {Fmt(_selEnd)} ({Fmt(_selEnd - _selStart)})";
        }
        else
        {
            SelectionRect.Visibility = Visibility.Collapsed;
            SelText.Text = "";
        }
    }

    private bool HasSelection => _selEnd - _selStart > 0.02;

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        _selStart = 0; _selEnd = _durationSec; UpdateSelectionUi();
    }

    private void ClearSel_Click(object sender, RoutedEventArgs e)
    {
        _selStart = _selEnd = 0; UpdateSelectionUi();
    }

    // ===================== playback (MediaPlayer) =====================

    private MediaPlayer EnsurePlayer()
    {
        if (_player is null)
        {
            _player = new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.Media };
        }
        return _player;
    }

    private void Play_Click(object sender, RoutedEventArgs e) => StartPlayback(0, _durationSec);
    private void PlaySel_Click(object sender, RoutedEventArgs e)
    {
        if (HasSelection) StartPlayback(_selStart, _selEnd);
        else StartPlayback(0, _durationSec);
    }

    private double _playStartAt;

    private void StartPlayback(double fromSec, double toSec)
    {
        if (!HasClipPath) return;
        try
        {
            var pl = EnsurePlayer();
            _playStartAt = Math.Max(0, fromSec);
            _playStopAt = toSec > fromSec ? toSec : _durationSec;
            // Seek after the media actually opens — setting Position before MediaOpened is unreliable.
            pl.MediaOpened -= OnMediaOpened;
            pl.MediaOpened += OnMediaOpened;
            pl.Source = MediaSource.CreateFromUri(new Uri(ClipPath));
            Playhead.Visibility = Visibility.Visible;
            StartTimer();
        }
        catch (Exception ex) { ShowStatus(TweakResult.Fail(ex.Message, $"播放出錯：{ex.Message}")); }
    }

    private void OnMediaOpened(MediaPlayer sender, object args)
    {
        try
        {
            if (_playStartAt > 0) sender.Position = TimeSpan.FromSeconds(_playStartAt);
            sender.Play();
        }
        catch { }
    }

    private double _playStopAt;

    private void Pause_Click(object sender, RoutedEventArgs e) { try { _player?.Pause(); } catch { } StopTimer(); }
    private void Stop_Click(object sender, RoutedEventArgs e) => StopPlayback();

    private void StopPlayback()
    {
        try { _player?.Pause(); if (_player is not null) _player.Position = TimeSpan.Zero; } catch { }
        StopTimer();
        Playhead.Visibility = Visibility.Collapsed;
        UpdateTimeText(0);
    }

    private void StartTimer()
    {
        StopTimer();
        _playTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _playTimer.Tick += PlayTimer_Tick;
        _playTimer.Start();
    }

    private void StopTimer()
    {
        if (_playTimer is not null) { _playTimer.Stop(); _playTimer.Tick -= PlayTimer_Tick; _playTimer = null; }
    }

    private void PlayTimer_Tick(object? sender, object e)
    {
        if (_player is null) return;
        double pos;
        try { pos = _player.Position.TotalSeconds; } catch { return; }
        if (_playStopAt > 0 && pos >= _playStopAt) { StopPlayback(); return; }
        UpdateTimeText(pos);
        double x = TimeToX(pos);
        Playhead.Margin = new Thickness(x, 0, 0, 0);
    }

    private void UpdateTimeText(double pos) => TimeText.Text = $"{Fmt(pos)} / {Fmt(_durationSec)}";

    private static string Fmt(double sec)
    {
        if (sec < 0 || double.IsNaN(sec)) sec = 0;
        var t = TimeSpan.FromSeconds(sec);
        return t.Hours > 0 ? t.ToString(@"h\:mm\:ss\.ff") : t.ToString(@"m\:ss\.ff");
    }

    // ===================== primary effect handlers =====================

    private async Task RunSwap(Button btn, Func<Task<(TweakResult, string?)>> op)
    {
        if (_busy || !HasClipPath) return;
        if (!FfmpegAudioService.IsInstalled) { ShowStatus(TweakResult.Fail("ffmpeg is required.", "需要 ffmpeg。")); return; }
        _busy = true;
        var label = btn.Content;
        btn.IsEnabled = false;
        btn.Content = new ProgressRing { IsActive = true, Width = 16, Height = 16 };
        StopPlayback();
        try
        {
            var (r, path) = await op();
            if (r.Success && path is not null) AppState.CurrentAudioClip = path;  // triggers reload
            ShowStatus(r);
        }
        catch (Exception ex) { ShowStatus(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
        finally { btn.Content = label; btn.IsEnabled = true; _busy = false; }
    }

    private async void Trim_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireSelection()) return;
        await RunSwap((Button)sender, () => FfmpegAudioService.TrimAsync(ClipPath, _selStart, _selEnd));
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireSelection()) return;
        await RunSwap((Button)sender, () => FfmpegAudioService.DeleteSelectionAsync(ClipPath, _selStart, _selEnd, _durationSec));
    }

    private async void Silence_Click(object sender, RoutedEventArgs e)
    {
        if (!RequireSelection()) return;
        await RunSwap((Button)sender, () => FfmpegAudioService.SilenceSelectionAsync(ClipPath, _selStart, _selEnd));
    }

    private async void FadeIn_Click(object sender, RoutedEventArgs e)
        => await RunSwap((Button)sender, () => FfmpegAudioService.FadeInAsync(ClipPath, FadeSeconds()));

    private async void FadeOut_Click(object sender, RoutedEventArgs e)
        => await RunSwap((Button)sender, () => FfmpegAudioService.FadeOutAsync(ClipPath, FadeSeconds(), _durationSec));

    private async void Normalize_Click(object sender, RoutedEventArgs e)
        => await RunSwap((Button)sender, () => FfmpegAudioService.NormalizeAsync(ClipPath));

    private async void Gain_Click(object sender, RoutedEventArgs e)
        => await RunSwap((Button)sender, () => FfmpegAudioService.GainAsync(ClipPath, GainSlider.Value));

    private async void Speed_Click(object sender, RoutedEventArgs e)
        => await RunSwap((Button)sender, () => FfmpegAudioService.SpeedAsync(ClipPath, SpeedSlider.Value));

    private async void Pitch_Click(object sender, RoutedEventArgs e)
        => await RunSwap((Button)sender, () => FfmpegAudioService.PitchShiftAsync(ClipPath, PitchSlider.Value));

    private async void Denoise_Click(object sender, RoutedEventArgs e)
        => await RunSwap((Button)sender, () => FfmpegAudioService.NoiseReductionAsync(ClipPath));

    private async void Reverse_Click(object sender, RoutedEventArgs e)
        => await RunSwap((Button)sender, () => FfmpegAudioService.ReverseAsync(ClipPath));

    private async void Mix_Click(object sender, RoutedEventArgs e)
    {
        var other = await FileDialogs.OpenFileAsync(AudioExts);
        if (other is null) return;
        await RunSwap((Button)sender, () => FfmpegAudioService.MixWithAsync(ClipPath, other));
    }

    private async void Concat_Click(object sender, RoutedEventArgs e)
    {
        var other = await FileDialogs.OpenFileAsync(AudioExts);
        if (other is null) return;
        await RunSwap((Button)sender, () => FfmpegAudioService.ConcatWithAsync(ClipPath, other));
    }

    private double FadeSeconds() => double.IsNaN(FadeSec.Value) ? 2 : Math.Max(0.1, FadeSec.Value);

    private bool RequireSelection()
    {
        if (HasSelection) return true;
        ShowStatus(TweakResult.Fail("Drag on the waveform to select a range first.", "請先喺波形上拖曳揀範圍。"));
        return false;
    }

    // ===================== export / revert =====================

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (!HasClipPath) return;
        var path = await FileDialogs.SaveFileAsync("audio", ".wav", ".mp3", ".flac", ".m4a", ".ogg", ".opus");
        if (path is null) return;
        if (_busy) return;
        _busy = true;
        var label = ExportBtn.Content;
        ExportBtn.IsEnabled = false;
        ExportBtn.Content = new ProgressRing { IsActive = true, Width = 16, Height = 16 };
        try
        {
            var r = await FfmpegAudioService.ExportAsync(ClipPath, path);
            ShowStatus(r.Success ? TweakResult.Ok($"Exported to {path}", $"已匯出到 {path}") : r);
        }
        catch (Exception ex) { ShowStatus(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
        finally { ExportBtn.Content = label; ExportBtn.IsEnabled = true; _busy = false; }
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        // Re-open lets the user reload a clean source; we simply prompt to open again.
        var path = await FileDialogs.OpenFileAsync(AudioExts);
        if (path is null) return;
        StopPlayback();
        if (FfmpegAudioService.IsInstalled)
        {
            var (r, wav) = await FfmpegAudioService.RunAsync(path, "-i {in} -c:a pcm_s16le {out}");
            AppState.CurrentAudioClip = (r.Success && wav is not null) ? wav : path;
        }
        else AppState.CurrentAudioClip = path;
    }

    // ===================== effects list =====================

    private void FxFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateFx(sender.Text ?? string.Empty);
    }

    private void PopulateFx(string filter)
    {
        _fx ??= AudioEffectsOperations.All().ToList();
        FxPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _fx;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _fx.Where(t => t.SearchHaystack.Contains(f));
        }

        bool first = true;
        foreach (var op in shown)
        {
            if (!first) FxPanel.Children.Add(BuildDivider());
            first = false;
            FxPanel.Children.Add(BuildRow(op));
        }
    }

    // ---- One clean row: bilingual title + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        text.Children.Add(new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Action → Button awaiting RunAsync ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");
        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        btn.Click += async (_, _) =>
        {
            if (_rowBusy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _rowBusy = true;
            btn.IsEnabled = false;
            var restore = btn.Content;
            btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
            StopPlayback();
            try
            {
                var result = await op.RunAsync(System.Threading.CancellationToken.None);
                ShowResult(op, result);   // effect's RunAsync swaps AppState.CurrentAudioClip → reloads waveform
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                btn.Content = restore;
                btn.IsEnabled = true;
                _rowBusy = false;
            }
        };
        return btn;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = op.GetCurrentChoice?.Invoke();
            if (cur is not null && op.Choices is not null)
                for (int i = 0; i < op.Choices.Count; i++)
                    if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { combo.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try
                    {
                        var cur = op.GetCurrentChoice?.Invoke();
                        if (cur is not null && op.Choices is not null)
                            for (int i = 0; i < op.Choices.Count; i++)
                                if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                                { combo.SelectedIndex = i; break; }
                    }
                    catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    // ---------------- Slider → Slider + value label ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        string Format(double v)
        {
            bool whole = op.Step >= 1 && Math.Abs(op.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                               : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return op.Unit is null ? num : $"{num} {op.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = op.Min, Maximum = op.Max, StepFrequency = op.Step > 0 ? op.Step : 1, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        var valueText = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        bool suppress = true;
        try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { slider.Value = op.Min; }
        suppress = false;
        valueText.Text = Format(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = Format(e.NewValue);
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                valueText.Text = Format(slider.Value);
                suppress = false;
            }
        };
        panel.Children.Add(slider);
        panel.Children.Add(valueText);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step > 0 ? op.Step : 1,
            LargeChange = op.Step > 0 ? op.Step : 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        bool suppress = true;
        try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, e) =>
        {
            if (suppress || op.SetNumber is null || double.IsNaN(e.NewValue)) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        return box;
    }

    // ---------------- Info → refreshable TextBlock ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ---------------- Shared result / status (persistent InfoBar) ----------------
    private void ShowResult(TweakDefinition op, TweakResult result)
    {
        FxResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        FxResultBar.Title = result.Success ? P("Done", "完成") : P("Failed", "失敗");
        FxResultBar.Message = result.Message is null ? string.Empty : result.Message.Get(Loc.I.Language);
        FxResultBar.IsOpen = true;

        // Mirror any raw output into the existing monospace status pane.
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            StatusBorder.Visibility = Visibility.Visible;
            var body = result.Output!;
            StatusText.Text = body.Length > 4000 ? body[^4000..] : body;
        }
    }

    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        FxResultBar.Severity = InfoBarSeverity.Success;
        FxResultBar.Title = P("Done", "完成");
        FxResultBar.Message = P(en, zh);
        FxResultBar.IsOpen = true;
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        FxResultBar.Severity = InfoBarSeverity.Error;
        FxResultBar.Title = P("Failed", "失敗");
        FxResultBar.Message = needAdmin
            ? P("This change needs administrator rights.", "呢項更改需要管理員權限。")
            : ex.Message;
        FxResultBar.IsOpen = true;
    }

    // ===================== status =====================

    private void ShowStatus(TweakResult r)
    {
        StatusBorder.Visibility = Visibility.Visible;
        var head = r.Success ? P("✓ Done", "✓ 完成") : P("✗ Failed", "✗ 失敗");
        var body = string.IsNullOrWhiteSpace(r.Output)
            ? ((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "")
            : r.Output!;
        if (body.Length > 4000) body = body[^4000..];
        StatusText.Text = head + "\n" + body;
    }
}
