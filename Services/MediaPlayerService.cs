using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibVLCSharp.Shared;
using LibVLCSharp.Shared.Structures;

namespace WinForge.Services;

/// <summary>
/// 內嵌 VLC 引擎（libVLC）· Owns the embedded libVLC engine and a single MediaPlayer for the
/// in-app Media Player module. 真正用 VLC 嘅引擎喺 WinForge 入面播片，唔係彈出 VLC app。
/// The same engine VLC ships, embedded — no external redirect.
///
/// Lifecycle: <see cref="Attach"/> is called once the WinUI VideoView raises its Initialized
/// event (it hands us the SwapChain options that wire the engine to the on-screen surface).
/// <see cref="Dispose"/> tears down the native objects on page unload to avoid leaks.
/// </summary>
public sealed class MediaPlayerService : IDisposable
{
    private static bool _coreInit;

    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private Media? _current;

    /// <summary>底層 MediaPlayer（畀 VideoView 綁定）· The native MediaPlayer the VideoView renders.</summary>
    public MediaPlayer? Player => _player;

    public bool IsReady => _player is not null && _libVlc is not null;

    /// <summary>確保 libVLC 原生庫已載入 · Ensure Core.Initialize() ran once (loads libvlc.dll + plugins).</summary>
    public static void EnsureCore()
    {
        if (_coreInit) return;
        // 由打包嘅 VideoLAN.LibVLC.Windows 載入原生庫（無需另外安裝）。
        // Loads the native libvlc.dll + plugins bundled by VideoLAN.LibVLC.Windows — no separate install.
        Core.Initialize();
        _coreInit = true;
    }

    /// <summary>
    /// 由 VideoView 嘅 Initialized 事件接通引擎 · Build the engine using the swap-chain options the
    /// WinUI VideoView provides, then create the MediaPlayer. Returns the player to assign to the view.
    /// </summary>
    public MediaPlayer Attach(string[] swapChainOptions)
    {
        EnsureCore();
        if (_player is not null) return _player;
        _libVlc = new LibVLC(swapChainOptions ?? Array.Empty<string>());
        _player = new MediaPlayer(_libVlc) { EnableHardwareDecoding = true };
        return _player;
    }

    // ===================== transport · 播放控制 =====================

    /// <summary>開一個本機檔案 · Open a local file by path.</summary>
    public bool OpenFile(string path, bool autoPlay = true)
    {
        if (_libVlc is null || _player is null || string.IsNullOrWhiteSpace(path)) return false;
        var media = new Media(_libVlc, path, FromType.FromPath);
        return Swap(media, autoPlay);
    }

    /// <summary>開一條 URL／串流 · Open a URL or network stream (http/https/rtsp/mms/…).</summary>
    public bool OpenUrl(string url, bool autoPlay = true)
    {
        if (_libVlc is null || _player is null || string.IsNullOrWhiteSpace(url)) return false;
        var media = new Media(_libVlc, url, FromType.FromLocation);
        return Swap(media, autoPlay);
    }

    private bool Swap(Media media, bool autoPlay)
    {
        try
        {
            _current?.Dispose();
            _current = media;
            _player!.Media = media;
            if (autoPlay) _player.Play();
            return true;
        }
        catch { return false; }
    }

    public void Play()
    {
        if (_player is null) return;
        if (_player.Media is null) return;
        _player.Play();
    }

    public void Pause() { if (_player?.CanPause == true) _player.Pause(); }

    public void TogglePlayPause()
    {
        if (_player is null) return;
        if (_player.IsPlaying) _player.Pause();
        else _player.Play();
    }

    public void Stop() => _player?.Stop();

    public bool IsPlaying => _player?.IsPlaying ?? false;

    /// <summary>目前位置（毫秒）· Current position in milliseconds.</summary>
    public long TimeMs
    {
        get => _player?.Time ?? 0;
        set { if (_player is not null && _player.IsSeekable) _player.Time = value; }
    }

    /// <summary>媒體總長度（毫秒）· Total length in milliseconds (0 if unknown).</summary>
    public long LengthMs => _player?.Length ?? 0;

    /// <summary>位置 0..1 · Normalised position 0..1.</summary>
    public float Position
    {
        get => _player?.Position ?? 0f;
        set { if (_player is not null && _player.IsSeekable) _player.Position = Math.Clamp(value, 0f, 1f); }
    }

    public bool IsSeekable => _player?.IsSeekable ?? false;

    /// <summary>音量 0..100 · Volume 0..100.</summary>
    public int Volume
    {
        get => _player?.Volume ?? 100;
        set { if (_player is not null) _player.Volume = Math.Clamp(value, 0, 100); }
    }

    public bool Mute
    {
        get => _player?.Mute ?? false;
        set { if (_player is not null) _player.Mute = value; }
    }

    /// <summary>播放速度（1.0 = 正常）· Playback rate (1.0 = normal).</summary>
    public float Rate
    {
        get => _player?.Rate ?? 1f;
        set { _player?.SetRate(Math.Clamp(value, 0.25f, 4f)); }
    }

    // ===================== tracks · 音訊／字幕軌 =====================

    public readonly record struct TrackItem(int Id, string Name);

    /// <summary>音訊軌清單 · Audio tracks (Id, Name).</summary>
    public IReadOnlyList<TrackItem> AudioTracks => Describe(_player?.AudioTrackDescription);

    /// <summary>字幕軌清單 · Subtitle (SPU) tracks (Id, Name). Id -1 = off.</summary>
    public IReadOnlyList<TrackItem> SubtitleTracks => Describe(_player?.SpuDescription);

    private static IReadOnlyList<TrackItem> Describe(TrackDescription[]? td)
        => td is null ? Array.Empty<TrackItem>()
                      : td.Select(t => new TrackItem(t.Id, t.Name ?? $"#{t.Id}")).ToList();

    public int CurrentAudioTrack
    {
        get => _player?.AudioTrack ?? -1;
        set { if (_player is not null) _player.SetAudioTrack(value); }
    }

    public int CurrentSubtitleTrack
    {
        get => _player?.Spu ?? -1;
        set { if (_player is not null) _player.SetSpu(value); }
    }

    /// <summary>載入外掛字幕檔 · Load an external subtitle file as a slave track.</summary>
    public bool AddSubtitleFile(string path)
    {
        if (_player is null || string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var uri = new Uri(path).AbsoluteUri;
            return _player.AddSlave(MediaSlaveType.Subtitle, uri, select: true);
        }
        catch { return false; }
    }

    /// <summary>字幕延遲（微秒）· Subtitle delay in microseconds.</summary>
    public long SubtitleDelayUs
    {
        get => _player?.SpuDelay ?? 0;
        set { _player?.SetSpuDelay(value); }
    }

    // ===================== snapshot · 截圖 =====================

    /// <summary>影低目前畫面做 PNG · Take a PNG snapshot of the current frame. Returns the saved path or null.</summary>
    public string? TakeSnapshot(string path)
    {
        if (_player is null) return null;
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            // num=0 → all video outputs; width/height 0 → native size.
            return _player.TakeSnapshot(0, path, 0, 0) ? path : null;
        }
        catch { return null; }
    }

    // ===================== fullscreen · 全螢幕 =====================

    public bool Fullscreen
    {
        get => _player?.Fullscreen ?? false;
        set { if (_player is not null) _player.Fullscreen = value; }
    }

    // ===================== transcode · 轉檔／轉碼 =====================

    /// <summary>一個轉檔預設 · One transcode preset (libVLC sout chain).</summary>
    public sealed record TranscodePreset(string Key, string En, string Zh, string Ext, string Sout);

    /// <summary>內建轉檔預設 · Built-in presets built on libVLC's #transcode{…}:std{…} chain.</summary>
    public static readonly IReadOnlyList<TranscodePreset> Presets = new[]
    {
        new TranscodePreset("mp4", "MP4 · H.264 + AAC", "MP4 · H.264 + AAC", ".mp4",
            "transcode{vcodec=h264,vb=2000,acodec=mp4a,ab=192,channels=2,samplerate=44100}:standard{access=file,mux=mp4,dst=\"{out}\"}"),
        new TranscodePreset("mp3", "MP3 (audio only) · 淨音訊", "MP3（淨音訊）", ".mp3",
            "transcode{acodec=mp3,ab=192,channels=2,samplerate=44100,vcodec=none}:standard{access=file,mux=raw,dst=\"{out}\"}"),
        new TranscodePreset("webm", "WebM · VP8 + Vorbis", "WebM · VP8 + Vorbis", ".webm",
            "transcode{vcodec=VP80,vb=2000,acodec=vorb,ab=192,channels=2,samplerate=44100}:standard{access=file,mux=webm,dst=\"{out}\"}"),
        new TranscodePreset("wav", "WAV (audio only) · 無損音訊", "WAV（無損音訊）", ".wav",
            "transcode{acodec=s16l,channels=2,samplerate=44100,vcodec=none}:standard{access=file,mux=wav,dst=\"{out}\"}"),
        new TranscodePreset("ogg", "OGG/Theora · 開放格式", "OGG/Theora · 開放格式", ".ogg",
            "transcode{vcodec=theo,vb=2000,acodec=vorb,ab=192,vcodec=theo}:standard{access=file,mux=ogg,dst=\"{out}\"}"),
    };

    /// <summary>
    /// 用 libVLC 喺背景轉檔（唔影響螢幕播放）· Transcode a file in the background using a throwaway,
    /// video-output-less MediaPlayer. Raises <paramref name="onDone"/>(ok) on the engine thread.
    /// </summary>
    public bool Transcode(string input, string output, TranscodePreset preset, Action<bool>? onDone = null)
    {
        if (_libVlc is null || string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output)) return false;
        try
        {
            var media = new Media(_libVlc, input, FromType.FromPath);
            var sout = preset.Sout.Replace("{out}", output);
            media.AddOption(":sout=#" + sout);
            media.AddOption(":sout-keep");
            media.AddOption(":no-sout-all");

            var mp = new MediaPlayer(media);
            void Finish(bool ok)
            {
                try { mp.Dispose(); } catch { }
                try { media.Dispose(); } catch { }
                onDone?.Invoke(ok);
            }
            mp.EndReached += (_, _) => Finish(true);
            mp.EncounteredError += (_, _) => Finish(false);
            return mp.Play();
        }
        catch { return false; }
    }

    // ===================== cleanup · 清理 =====================

    public void Dispose()
    {
        try { _player?.Stop(); } catch { }
        try { _current?.Dispose(); } catch { }
        try { _player?.Dispose(); } catch { }
        try { _libVlc?.Dispose(); } catch { }
        _current = null;
        _player = null;
        _libVlc = null;
    }
}
