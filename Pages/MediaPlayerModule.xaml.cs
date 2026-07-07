using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using LibVLCSharp.Platforms.Windows;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 內嵌 VLC 媒體播放器 · Embedded VLC media player. 用真正 VLC 嘅引擎（libVLC）喺 WinForge 入面播片，
/// 唔係跳出去開 VLC app。Plays media with the same engine VLC ships (libVLC via LibVLCSharp), fully
/// inside WinForge — open files/URLs/streams, transport + seek + volume, playlist, audio/subtitle
/// tracks, external subtitles, PNG snapshot, fullscreen and preset transcode. Bilingual throughout.
/// </summary>
public sealed partial class MediaPlayerModule : Page
{
    /// <summary>一個播放清單項目 · One playlist entry.</summary>
    public sealed class PlaylistEntry
    {
        public string Path { get; init; } = "";
        public bool IsUrl { get; init; }
        public string DisplayName => IsUrl ? Path : System.IO.Path.GetFileName(Path);
    }

    private readonly MediaPlayerService _svc = new();
    private readonly ObservableCollection<PlaylistEntry> _playlist = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private bool _attached;
    private bool _seeking;          // user is dragging the seek slider
    private bool _suppressSeek;     // we're programmatically updating the slider
    private bool _suppressVol;
    private int _currentIndex = -1;
    private bool _suppressTrackEvents;

    public MediaPlayerModule()
    {
        InitializeComponent();

        PlaylistView.ItemsSource = _playlist;
        VideoView.Initialized += VideoView_Initialized;
        _tick.Tick += (_, _) => UpdateProgress();

        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); SyncButtons(); RefreshPresets(); };
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    // ===================== engine wiring · 引擎接通 =====================

    private void VideoView_Initialized(object? sender, InitializedEventArgs e)
    {
        try
        {
            var mp = _svc.Attach(e.SwapChainOptions);
            VideoView.MediaPlayer = mp;
            _attached = true;

            mp.Playing += (_, _) => DispatcherQueue.TryEnqueue(() => { SyncButtons(); RefreshTracks(); });
            mp.Paused += (_, _) => DispatcherQueue.TryEnqueue(SyncButtons);
            mp.Stopped += (_, _) => DispatcherQueue.TryEnqueue(() => { SyncButtons(); ResetProgress(); });
            mp.EndReached += (_, _) => DispatcherQueue.TryEnqueue(OnEndReached);
            mp.EncounteredError += (_, _) => DispatcherQueue.TryEnqueue(() =>
                ShowResult(false, P("Playback error", "播放出錯"),
                    P("libVLC could not play this media. The file or stream may be unsupported.",
                      "libVLC 播唔到呢個媒體，可能係格式唔支援。")));

            DispatcherQueue.TryEnqueue(() => { SyncButtons(); _tick.Start(); });
        }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() => ShowResult(false, P("Engine failed to start", "引擎啟動失敗"), ex.Message));
        }
    }

    // ===================== open · 開檔／開 URL =====================

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(MediaFilters(), P("Open media file", "開媒體檔案"));
        if (path is null) return;
        Enqueue(new PlaylistEntry { Path = path, IsUrl = false }, playNow: true);
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var files = await FileDialogs.OpenFilesAsync(MediaFilters(), P("Add media files", "加入媒體檔案"));
        if (files.Count == 0) return;
        foreach (var f in files) _playlist.Add(new PlaylistEntry { Path = f, IsUrl = false });
        UpdateEmptyHint();
        if (_currentIndex < 0) PlayIndex(0);
    }

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = (UrlBox.Text ?? "").Trim();
        if (url.Length == 0) return;
        Enqueue(new PlaylistEntry { Path = url, IsUrl = true }, playNow: true);
        UrlBox.Text = "";
    }

    private void Enqueue(PlaylistEntry entry, bool playNow)
    {
        _playlist.Add(entry);
        UpdateEmptyHint();
        if (playNow) PlayIndex(_playlist.Count - 1);
    }

    private void PlayIndex(int index)
    {
        if (!_attached) { ShowResult(false, P("Engine not ready", "引擎未準備好"), P("The video engine is still starting — try again in a moment.", "影片引擎仲喺度啟動 — 等一陣再試。")); return; }
        if (index < 0 || index >= _playlist.Count) return;
        _currentIndex = index;
        PlaylistView.SelectedIndex = index;
        var entry = _playlist[index];
        bool ok = entry.IsUrl ? _svc.OpenUrl(entry.Path) : _svc.OpenFile(entry.Path);
        if (!ok)
            ShowResult(false, P("Could not open", "開唔到"), entry.DisplayName);
        else
            ResultBar.IsOpen = false;
        NoMediaHint.Visibility = Visibility.Collapsed;
        SyncButtons();
    }

    // ===================== transport · 播放控制 =====================

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_svc.Player?.Media is null && _playlist.Count > 0) { PlayIndex(Math.Max(0, _currentIndex)); return; }
        _svc.TogglePlayPause();
        SyncButtons();
    }

    private void Stop_Click(object sender, RoutedEventArgs e) { _svc.Stop(); ResetProgress(); SyncButtons(); }

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_playlist.Count == 0) return;
        int i = _currentIndex <= 0 ? _playlist.Count - 1 : _currentIndex - 1;
        PlayIndex(i);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_playlist.Count == 0) return;
        int i = _currentIndex >= _playlist.Count - 1 ? 0 : _currentIndex + 1;
        PlayIndex(i);
    }

    private void OnEndReached()
    {
        // auto-advance the playlist
        if (_currentIndex >= 0 && _currentIndex < _playlist.Count - 1) PlayIndex(_currentIndex + 1);
        else { ResetProgress(); SyncButtons(); }
    }

    // ===================== seek · 拖動進度 =====================

    private void Seek_PointerPressed(object sender, PointerRoutedEventArgs e) => _seeking = true;

    private void Seek_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_svc.IsSeekable) _svc.Position = (float)(SeekSlider.Value / 1000.0);
        _seeking = false;
    }

    private void Seek_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppressSeek) return;
        if (_seeking) CurTime.Text = Fmt((long)(_svc.LengthMs * (SeekSlider.Value / 1000.0)));
    }

    private void UpdateProgress()
    {
        if (!_attached || _seeking) return;
        long len = _svc.LengthMs;
        long cur = _svc.TimeMs;
        _suppressSeek = true;
        SeekSlider.Value = len > 0 ? Math.Clamp(cur * 1000.0 / len, 0, 1000) : 0;
        _suppressSeek = false;
        CurTime.Text = Fmt(cur);
        TotTime.Text = Fmt(len);
    }

    private void ResetProgress()
    {
        _suppressSeek = true;
        SeekSlider.Value = 0;
        _suppressSeek = false;
        CurTime.Text = "00:00";
    }

    // ===================== volume / speed · 音量／速度 =====================

    private void Volume_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppressVol) return;
        _svc.Volume = (int)VolumeSlider.Value;
        if (_svc.Mute && VolumeSlider.Value > 0) { _svc.Mute = false; UpdateMuteIcon(); }
    }

    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _svc.Mute = !_svc.Mute;
        UpdateMuteIcon();
    }

    private void UpdateMuteIcon()
        => MuteIcon.Glyph = _svc.Mute || _svc.Volume == 0 ? ((char)0xE74F).ToString() : ((char)0xE767).ToString();

    private void Speed_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SpeedBox.SelectedItem is ComboBoxItem item && item.Tag is string tag
            && float.TryParse(tag, System.Globalization.CultureInfo.InvariantCulture, out var r))
            _svc.Rate = r;
    }

    // ===================== tracks · 音訊／字幕軌 =====================

    private void RefreshTracks()
    {
        _suppressTrackEvents = true;

        var audio = _svc.AudioTracks;
        AudioTrackBox.Items.Clear();
        foreach (var t in audio) AudioTrackBox.Items.Add(new ComboBoxItem { Content = t.Name, Tag = t.Id });
        SelectTrack(AudioTrackBox, _svc.CurrentAudioTrack);
        AudioTrackBox.IsEnabled = audio.Count > 0;

        var subs = _svc.SubtitleTracks;
        SubTrackBox.Items.Clear();
        // Always offer an explicit "off" entry (id -1)
        SubTrackBox.Items.Add(new ComboBoxItem { Content = P("Off", "關閉"), Tag = -1 });
        foreach (var t in subs.Where(t => t.Id != -1))
            SubTrackBox.Items.Add(new ComboBoxItem { Content = t.Name, Tag = t.Id });
        SelectTrack(SubTrackBox, _svc.CurrentSubtitleTrack);
        SubTrackBox.IsEnabled = true;

        _suppressTrackEvents = false;
    }

    private static void SelectTrack(ComboBox box, int id)
    {
        for (int i = 0; i < box.Items.Count; i++)
            if (box.Items[i] is ComboBoxItem ci && ci.Tag is int t && t == id) { box.SelectedIndex = i; return; }
        if (box.Items.Count > 0) box.SelectedIndex = 0;
    }

    private void AudioTrack_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTrackEvents) return;
        if (AudioTrackBox.SelectedItem is ComboBoxItem ci && ci.Tag is int id) _svc.CurrentAudioTrack = id;
    }

    private void SubTrack_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressTrackEvents) return;
        if (SubTrackBox.SelectedItem is ComboBoxItem ci && ci.Tag is int id) _svc.CurrentSubtitleTrack = id;
    }

    private async void LoadSub_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(
            new[]
            {
                new FileDialogs.Filter("Subtitles / 字幕", "*.srt;*.ass;*.ssa;*.sub;*.vtt"),
                new FileDialogs.Filter("All files / 所有檔案", "*.*"),
            },
            P("Load subtitle file", "載入字幕檔"));
        if (path is null) return;
        bool ok = _svc.AddSubtitleFile(path);
        ShowResult(ok, ok ? P("Subtitles loaded", "字幕已載入") : P("Failed to load subtitles", "載入字幕失敗"),
            System.IO.Path.GetFileName(path));
        if (ok) RefreshTracks();
    }

    private void SubDelay_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (double.IsNaN(args.NewValue)) return;
        _svc.SubtitleDelayUs = (long)(args.NewValue * 1_000_000); // seconds → microseconds
    }

    // ===================== snapshot / fullscreen · 截圖／全螢幕 =====================

    private async void Snapshot_Click(object sender, RoutedEventArgs e)
    {
        if (_svc.Player?.Media is null) { ShowResult(false, P("Nothing playing", "未有播放"), P("Open a video first.", "先開個影片。")); return; }
        var suggested = $"WinForge-snapshot-{DateTime.Now:yyyyMMdd-HHmmss}";
        var path = await FileDialogs.SaveFileAsync(suggested, ".png");
        if (path is null) return;
        var saved = _svc.TakeSnapshot(path);
        ShowResult(saved is not null, saved is not null ? P("Snapshot saved", "截圖已儲存") : P("Snapshot failed", "截圖失敗"),
            saved ?? "");
    }

    private void Fullscreen_Click(object sender, RoutedEventArgs e)
    {
        // libVLC's native fullscreen flag does not apply to the embedded SwapChainPanel surface, so we
        // toggle the app window into a borderless full-screen presenter instead — the VideoView fills it.
        try
        {
            if (App.Shell is { } win)
            {
                var ap = win.AppWindow;
                bool full = ap.Presenter.Kind == Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen;
                ap.SetPresenter(full
                    ? Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped
                    : Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
            }
        }
        catch (Exception ex) { ShowResult(false, P("Fullscreen failed", "全螢幕失敗"), ex.Message); }
    }

    // ===================== playlist · 播放清單 =====================

    private void Playlist_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (PlaylistView.SelectedIndex >= 0) PlayIndex(PlaylistView.SelectedIndex);
    }

    private void Playlist_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool has = PlaylistView.SelectedIndex >= 0;
        PlaySelBtn.IsEnabled = has;
        RemoveSelBtn.IsEnabled = has;
    }

    private void PlaySel_Click(object sender, RoutedEventArgs e)
    {
        if (PlaylistView.SelectedIndex >= 0) PlayIndex(PlaylistView.SelectedIndex);
    }

    private void RemoveSel_Click(object sender, RoutedEventArgs e)
    {
        int i = PlaylistView.SelectedIndex;
        if (i < 0) return;
        _playlist.RemoveAt(i);
        if (i == _currentIndex) { _svc.Stop(); _currentIndex = -1; ResetProgress(); }
        else if (i < _currentIndex) _currentIndex--;
        UpdateEmptyHint();
        SyncButtons();
    }

    private void ClearList_Click(object sender, RoutedEventArgs e)
    {
        _svc.Stop();
        _playlist.Clear();
        _currentIndex = -1;
        ResetProgress();
        UpdateEmptyHint();
        SyncButtons();
    }

    private void UpdateEmptyHint()
    {
        EmptyList.Visibility = _playlist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PlaylistView.Visibility = _playlist.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ===================== transcode · 轉檔 =====================

    private void RefreshPresets()
    {
        PresetBox.Items.Clear();
        foreach (var p in MediaPlayerService.Presets)
            PresetBox.Items.Add(new ComboBoxItem { Content = P(p.En, p.Zh), Tag = p.Key });
        if (PresetBox.Items.Count > 0) PresetBox.SelectedIndex = 0;
    }

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        if (!_attached) { ShowResult(false, P("Engine not ready", "引擎未準備好"), ""); return; }
        if (_currentIndex < 0 || _currentIndex >= _playlist.Count || _playlist[_currentIndex].IsUrl)
        {
            ShowResult(false, P("Pick a local file first", "先揀一個本機檔案"),
                P("Select a local media file in the playlist, then convert.", "喺播放清單揀一個本機媒體檔，然後轉檔。"));
            return;
        }
        if (PresetBox.SelectedItem is not ComboBoxItem ci || ci.Tag is not string key) return;
        var preset = MediaPlayerService.Presets.FirstOrDefault(p => p.Key == key);
        if (preset is null) return;

        var input = _playlist[_currentIndex].Path;
        var suggested = System.IO.Path.GetFileNameWithoutExtension(input) + "-converted";
        var output = await FileDialogs.SaveFileAsync(suggested, preset.Ext);
        if (output is null) return;

        ConvertBtn.IsEnabled = false;
        ConvertProgress.Visibility = Visibility.Visible;
        ShowResult(true, P("Converting…", "轉檔緊…"),
            P("Transcoding with libVLC in the background. Large files take a while.",
              "用 libVLC 喺背景轉檔緊。大檔案要等耐啲。"));

        bool started = _svc.Transcode(input, output, preset, ok => DispatcherQueue.TryEnqueue(() =>
        {
            ConvertBtn.IsEnabled = true;
            ConvertProgress.Visibility = Visibility.Collapsed;
            ShowResult(ok, ok ? P("Converted", "已轉檔") : P("Conversion failed", "轉檔失敗"), ok ? output : "");
        }));

        if (!started)
        {
            ConvertBtn.IsEnabled = true;
            ConvertProgress.Visibility = Visibility.Collapsed;
            ShowResult(false, P("Conversion failed", "轉檔失敗"), P("libVLC could not start the transcode.", "libVLC 開唔到轉檔。"));
        }
    }

    // ===================== render / state · 介面文字／狀態 =====================

    private void Render()
    {
        Header.Title = "Media Player · 媒體播放器";
        HeaderBlurb.Text = P("A real media player powered by the VLC engine (libVLC), embedded inside WinForge — no separate VLC install or redirect. Open files, URLs and streams; manage a playlist; switch audio and subtitle tracks; snapshot a frame; and convert with presets.",
            "用真正 VLC 引擎（libVLC）內嵌喺 WinForge 入面嘅媒體播放器 — 唔使另外裝 VLC，亦唔會跳出去。可以開檔案、URL、串流；管理播放清單；切換音訊同字幕軌；截圖；仲可以用預設轉檔。");
        OpenFileText.Text = P("Open file…", "開檔案…");
        AddToListText.Text = P("Add to playlist…", "加入清單…");
        OpenUrlBtn.Content = P("Open URL", "開 URL");
        NoMediaHint.Text = P("Open a file, paste a URL, or add to the playlist to start playing.",
            "開個檔案、貼條 URL，或者加入播放清單就開始播放。");
        SpeedCap.Text = P("Speed", "速度");

        TracksTitle.Text = P("Tracks", "音軌");
        AudioCap.Text = P("Audio track", "音訊軌");
        SubCap.Text = P("Subtitle track", "字幕軌");
        LoadSubBtn.Content = P("Load subtitle file…", "載入字幕檔…");
        SubDelayCap.Text = P("Sub delay (s)", "字幕延遲（秒）");

        PlaylistTitle.Text = P("Playlist", "播放清單");
        EmptyList.Text = P("No items yet. Use “Add to playlist…”.", "未有項目。用「加入清單…」。");
        PlaySelBtn.Content = P("Play", "播放");
        RemoveSelBtn.Content = P("Remove", "移除");

        ConvertTitle.Text = P("Convert / Transcode", "轉檔／轉碼");
        ConvertBlurb.Text = P("Re-encode the current playlist file using the VLC engine.", "用 VLC 引擎將目前清單檔案重新編碼。");
        ConvertBtn.Content = P("Convert…", "轉檔…");

        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Informational;
        EngineBar.Title = P("VLC engine embedded", "已內嵌 VLC 引擎");
        EngineBar.Message = P("The native libVLC engine ships with WinForge — no separate VLC install or redirect is needed.",
            "原生 libVLC 引擎隨 WinForge 一齊提供 — 唔使另外安裝 VLC，亦唔會跳出去。");
        EngineBar.ActionButton = null;

        UpdateEmptyHint();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); RefreshPresets(); }

    private void SyncButtons()
    {
        bool playing = _svc.IsPlaying;
        PlayPauseIcon.Glyph = playing ? ((char)0xE769).ToString() : ((char)0xE768).ToString(); // pause : play
        bool hasList = _playlist.Count > 0;
        PrevBtn.IsEnabled = hasList;
        NextBtn.IsEnabled = hasList;
        bool hasMedia = _svc.Player?.Media is not null;
        StopBtn.IsEnabled = hasMedia;
        SnapshotBtn.IsEnabled = hasMedia;
        bool hasSel = PlaylistView.SelectedIndex >= 0;
        PlaySelBtn.IsEnabled = hasSel;
        RemoveSelBtn.IsEnabled = hasSel;
        UpdateMuteIcon();
    }

    private void ShowResult(bool ok, string title, string message)
    {
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private static string Fmt(long ms)
    {
        if (ms <= 0) return "00:00";
        var t = TimeSpan.FromMilliseconds(ms);
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }

    private static System.Collections.Generic.IEnumerable<FileDialogs.Filter> MediaFilters() => new[]
    {
        new FileDialogs.Filter("Video / 影片", "*.mp4;*.mkv;*.avi;*.mov;*.webm;*.flv;*.wmv;*.m4v;*.mpg;*.mpeg;*.ts;*.m2ts"),
        new FileDialogs.Filter("Audio / 音訊", "*.mp3;*.flac;*.wav;*.aac;*.ogg;*.opus;*.m4a;*.wma"),
        new FileDialogs.Filter("All files / 所有檔案", "*.*"),
    };

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _tick.Stop();
        Loc.I.LanguageChanged -= OnLanguageChanged;
        try { if (VideoView is not null) VideoView.MediaPlayer = null; } catch { }
        _svc.Dispose();
    }
}
