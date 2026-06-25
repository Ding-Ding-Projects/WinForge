using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 廣播優先級 · Announcement priority.
/// Normal announcements queue behind one another; Urgent jumps the queue and can
/// interrupt a Normal one already speaking (for alarms / reactor PA events).
/// </summary>
public enum AnnouncementPriority
{
    /// <summary>一般 · normal — appended to the back of the queue.</summary>
    Normal,
    /// <summary>緊急 · urgent — jumps to the front and pre-empts a normal announcement.</summary>
    Urgent,
}

/// <summary>
/// 一條排隊緊嘅廣播 · One queued public-address announcement.
/// </summary>
internal sealed class AnnouncementItem
{
    public required string Text { get; init; }
    public AnnouncementPriority Priority { get; init; }
    public bool Chime { get; init; }
}

/// <summary>
/// 喇叭語音廣播系統 · Speaker text-to-speech public-address (PA) announcement system.
///
/// 喺成個 app 任何地方都可以叫 <see cref="Announce(string,string,AnnouncementPriority,bool)"/>，
/// 講出跟當前語言（Loc.I）嘅文字。廣播會排隊唔會疊聲；緊急（Urgent）會插隊，俾警報用。
/// 廣播前可以播一段喺 C# 即時合成嘅雙音 PA 叮咚（無外部音檔）。
///
/// Call <see cref="Announce(string,string,AnnouncementPriority,bool)"/> from anywhere in the app to
/// speak the string matching the current language. Announcements queue (never overlap); an Urgent
/// one jumps the queue and pre-empts a Normal one already speaking — so the reactor PA / alarms can
/// barge in. A short two-tone PA chime is synthesised in C# (PCM sine WAV in memory, played via
/// <see cref="SoundPlayer"/>) before speech — no external audio asset.
///
/// System.Speech is a desktop SAPI assembly; all synthesis runs on a dedicated background pump
/// thread so the UI thread is never blocked and there is no STA/threading conflict.
/// </summary>
public sealed class AnnouncementService
{
    /// <summary>單例 · The app-wide singleton. Call AnnouncementService.I from anywhere.</summary>
    public static AnnouncementService I { get; } = new();

    // ---- Persisted settings keys ----
    private const string KeyVoice = "announce.voice";
    private const string KeyVolume = "announce.volume";
    private const string KeyRate = "announce.rate";
    private const string KeyChime = "announce.chime";
    private const string KeyMute = "announce.mute";

    private readonly object _gate = new();
    private readonly LinkedList<AnnouncementItem> _queue = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly Thread _pump;
    private volatile bool _disposed;

    // The synthesizer that is *currently* speaking, so an Urgent item can cancel it.
    private SpeechSynthesizer? _active;

    private AnnouncementService()
    {
        _pump = new Thread(PumpLoop)
        {
            IsBackground = true,
            Name = "WinForge-PA-Announcer",
        };
        _pump.Start();
    }

    // ===================================================================
    // Settings (persisted via SettingsStore)
    // ===================================================================

    /// <summary>揀咗嘅語音名（空＝自動）· Chosen SAPI voice name (empty = auto-select per language).</summary>
    public string VoiceName
    {
        get => SettingsStore.Get(KeyVoice, "");
        set => SettingsStore.Set(KeyVoice, value ?? "");
    }

    /// <summary>音量 0..100 · Speech volume 0..100.</summary>
    public int Volume
    {
        get => Clamp(ParseInt(SettingsStore.Get(KeyVolume, "100"), 100), 0, 100);
        set => SettingsStore.Set(KeyVolume, Clamp(value, 0, 100).ToString());
    }

    /// <summary>語速 -10..10 · Speech rate -10..10.</summary>
    public int Rate
    {
        get => Clamp(ParseInt(SettingsStore.Get(KeyRate, "0"), 0), -10, 10);
        set => SettingsStore.Set(KeyRate, Clamp(value, -10, 10).ToString());
    }

    /// <summary>播放前嘅叮咚 · Play the PA chime before speech.</summary>
    public bool ChimeEnabled
    {
        get => SettingsStore.Get(KeyChime, "True") == "True";
        set => SettingsStore.Set(KeyChime, value.ToString());
    }

    /// <summary>靜音（仍會排隊但唔出聲）· Muted: items still queue but are spoken silently-skipped.</summary>
    public bool Muted
    {
        get => SettingsStore.Get(KeyMute, "False") == "True";
        set => SettingsStore.Set(KeyMute, value.ToString());
    }

    /// <summary>而家有冇廣播緊 · True while an announcement is playing.</summary>
    public bool IsSpeaking { get; private set; }

    /// <summary>狀態改變（播放／隊列）· Raised when speaking state or the queue changes; marshal to UI yourself.</summary>
    public event EventHandler? StateChanged;

    /// <summary>等緊嘅廣播數目 · Number of announcements waiting in the queue.</summary>
    public int QueueLength { get { lock (_gate) return _queue.Count; } }

    // ===================================================================
    // Public API — call from anywhere
    // ===================================================================

    /// <summary>
    /// 廣播（雙語自動揀）· Queue an announcement, speaking the string for the app's current language.
    /// </summary>
    /// <param name="en">English text.</param>
    /// <param name="zh">粵語 text.</param>
    /// <param name="priority">Normal queues; Urgent jumps the queue and pre-empts a normal item.</param>
    /// <param name="chime">Play the PA chime first (defaults to the user's ChimeEnabled setting).</param>
    public void Announce(string en, string zh, AnnouncementPriority priority = AnnouncementPriority.Normal, bool? chime = null)
    {
        var text = Loc.I.Pick(en, zh);
        Enqueue(text, priority, chime ?? ChimeEnabled);
    }

    /// <summary>
    /// 廣播兩種語言（英文之後粵語）· Announce in BOTH languages back-to-back (English then 粵語),
    /// for bilingual PA where every announcement must be understood by everyone.
    /// </summary>
    public void AnnounceBoth(string en, string zh, AnnouncementPriority priority = AnnouncementPriority.Normal, bool? chime = null)
    {
        bool ch = chime ?? ChimeEnabled;
        // Single queued item carries both; the chime plays once, then both languages are spoken.
        Enqueue(en + "\n" + zh, priority, ch, both: true);
    }

    /// <summary>廣播一段已經揀好語言嘅文字 · Announce a single, already-localized string.</summary>
    public void AnnounceRaw(string text, AnnouncementPriority priority = AnnouncementPriority.Normal, bool? chime = null)
        => Enqueue(text, priority, chime ?? ChimeEnabled);

    private void Enqueue(string text, AnnouncementPriority priority, bool chime, bool both = false)
    {
        if (string.IsNullOrWhiteSpace(text) || _disposed) return;

        var item = new AnnouncementItem { Text = text, Priority = priority, Chime = chime };

        lock (_gate)
        {
            if (priority == AnnouncementPriority.Urgent)
            {
                _queue.AddFirst(item);
                // Pre-empt whatever Normal item is currently speaking so the alarm is heard now.
                try { _active?.SpeakAsyncCancelAll(); } catch { /* race on dispose */ }
            }
            else
            {
                _queue.AddLast(item);
            }
        }
        _signal.Set();
        RaiseState();
    }

    /// <summary>停止並清空隊列 · Stop the current announcement and clear everything queued.</summary>
    public void StopAll()
    {
        lock (_gate)
        {
            _queue.Clear();
            try { _active?.SpeakAsyncCancelAll(); } catch { }
        }
        RaiseState();
    }

    // ===================================================================
    // Voice enumeration / selection
    // ===================================================================

    /// <summary>列出所有已安裝語音 · Enumerate every installed & enabled SAPI voice.</summary>
    public List<VoiceInfo> GetVoices()
    {
        try
        {
            using var probe = new SpeechSynthesizer();
            return probe.GetInstalledVoices()
                .Where(v => v.Enabled)
                .Select(v => new VoiceInfo
                {
                    Name = v.VoiceInfo.Name,
                    Culture = v.VoiceInfo.Culture?.Name ?? "",
                    Gender = v.VoiceInfo.Gender.ToString(),
                    Age = v.VoiceInfo.Age.ToString(),
                })
                .ToList();
        }
        catch
        {
            return new List<VoiceInfo>();
        }
    }

    /// <summary>
    /// 揀一把最啱當前語言嘅語音 · Resolve the best voice for the current app language.
    /// Honours an explicit user choice first; otherwise prefers a Chinese voice
    /// (zh-HK / zh-TW / zh-CN) when the app is in Cantonese, an English voice otherwise.
    /// Returns null when nothing suitable is installed (caller falls back to the default voice).
    /// </summary>
    public string? ResolveVoiceForCurrentLanguage()
    {
        var voices = GetVoices();
        if (voices.Count == 0) return null;

        // 1) Honour an explicit user choice if it is still installed.
        var chosen = VoiceName;
        if (!string.IsNullOrEmpty(chosen) &&
            voices.Any(v => string.Equals(v.Name, chosen, StringComparison.OrdinalIgnoreCase)))
            return chosen;

        bool wantChinese = Loc.I.Language == AppLanguage.Cantonese;

        if (wantChinese)
        {
            // Prefer Hong Kong Cantonese, then Taiwan, then mainland, then any zh-*.
            string[] order = { "zh-hk", "zh-tw", "zh-cn", "zh" };
            foreach (var pref in order)
            {
                var hit = voices.FirstOrDefault(v =>
                    v.Culture.StartsWith(pref, StringComparison.OrdinalIgnoreCase));
                if (hit != null) return hit.Name;
            }
            // No Chinese voice installed — fall through to an English voice so it still speaks.
        }

        var en = voices.FirstOrDefault(v => v.Culture.StartsWith("en", StringComparison.OrdinalIgnoreCase));
        return en?.Name ?? voices[0].Name;
    }

    /// <summary>
    /// 報告語音狀況 · Describe whether a language-appropriate voice is available, for the UI to note
    /// gracefully (no install button — System voices are provisioned by Windows).
    /// </summary>
    public (bool ok, string en, string zh) DescribeVoiceAvailability()
    {
        var voices = GetVoices();
        if (voices.Count == 0)
            return (false,
                "No text-to-speech voices are installed. Announcements are silent until a voice is added under Windows Settings → Time & language → Speech.",
                "未安裝任何文字轉語音語音。喺 Windows 設定 → 時間與語言 → 語音 加咗把聲之前，廣播會冇聲。");

        bool wantChinese = Loc.I.Language == AppLanguage.Cantonese;
        if (wantChinese &&
            !voices.Any(v => v.Culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)))
            return (false,
                "No Chinese voice is installed; Cantonese announcements will use an English voice. Add a Chinese voice under Windows Settings → Time & language → Speech.",
                "未安裝中文語音；粵語廣播會用英文聲讀出。喺 Windows 設定 → 時間與語言 → 語音 加返中文語音。");

        return (true, "A suitable voice is available.", "已有合適嘅語音。");
    }

    // ===================================================================
    // Background pump — one announcement at a time
    // ===================================================================

    private void PumpLoop()
    {
        while (!_disposed)
        {
            AnnouncementItem? item = null;
            lock (_gate)
            {
                if (_queue.Count > 0)
                {
                    item = _queue.First!.Value;
                    _queue.RemoveFirst();
                }
            }

            if (item == null)
            {
                _signal.WaitOne();        // sleep until something is queued
                continue;
            }

            if (Muted)
            {
                // Honour mute: drain the item without speaking, but still report state.
                RaiseState();
                continue;
            }

            IsSpeaking = true;
            RaiseState();
            try
            {
                if (item.Chime) PlayChime();
                Speak(item.Text, item.Priority);
            }
            catch { /* never let a single bad item kill the pump */ }
            finally
            {
                IsSpeaking = false;
                RaiseState();
            }
        }
    }

    private void Speak(string text, AnnouncementPriority priority)
    {
        SpeechSynthesizer synth;
        try
        {
            synth = new SpeechSynthesizer();
        }
        catch
        {
            return; // SAPI unavailable
        }

        try
        {
            var voice = ResolveVoiceForCurrentLanguage();
            if (!string.IsNullOrEmpty(voice))
            {
                try { synth.SelectVoice(voice); } catch { /* fall back to default voice */ }
            }
            synth.Rate = Clamp(Rate, -10, 10);
            synth.Volume = Clamp(Volume, 0, 100);
            synth.SetOutputToDefaultAudioDevice();

            lock (_gate) { _active = synth; }

            // Blocking on this dedicated pump thread, so announcements never overlap.
            // An Urgent enqueue calls SpeakAsyncCancelAll on _active to barge in.
            synth.SpeakAsync(text);
            // Wait for completion (or cancellation) by polling the synthesizer state.
            WaitUntilDone(synth);
        }
        finally
        {
            lock (_gate) { if (ReferenceEquals(_active, synth)) _active = null; }
            try { synth.Dispose(); } catch { }
        }
    }

    private void WaitUntilDone(SpeechSynthesizer synth)
    {
        // SpeakAsync + poll keeps SpeakAsyncCancelAll responsive for the urgent barge-in path.
        while (!_disposed && synth.State != SynthesizerState.Ready)
            Thread.Sleep(40);
    }

    // ===================================================================
    // PA chime — synthesised in C# (no external asset)
    // ===================================================================

    // winmm PlaySound — plays an in-memory WAV image. SND_MEMORY|SND_SYNC blocks until done.
    // Pure P/Invoke (no extra managed assembly reference needed).
    private const uint SND_SYNC = 0x0000;
    private const uint SND_MEMORY = 0x0004;
    private const uint SND_NODEFAULT = 0x0002;

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern bool PlaySound(byte[] pszSound, IntPtr hmod, uint fdwSound);

    /// <summary>
    /// 播一段雙音 PA 叮咚 · Play a synthesised two-tone "ding-dong" PA chime (G5 → C5).
    /// Tones are generated as 16-bit PCM sine waves in memory and played as an in-memory WAV
    /// via winmm PlaySound (SND_MEMORY|SND_SYNC) — no external audio asset, no extra assembly.
    /// </summary>
    public void PlayChime()
    {
        try
        {
            byte[] wav = BuildChimeWav();
            // Synchronous so the chime finishes before speech starts (we are on the pump thread).
            PlaySound(wav, IntPtr.Zero, SND_MEMORY | SND_SYNC | SND_NODEFAULT);
        }
        catch { /* chime is best-effort */ }
    }

    private const int SampleRate = 44100;

    private static byte[] BuildChimeWav()
    {
        // Classic two-tone PA chime: a high note then a lower note, each with a soft attack/decay.
        var samples = new List<short>();
        AppendTone(samples, 784.0, 0.28, 0.35);  // G5
        AppendTone(samples, 523.25, 0.45, 0.35); // C5
        return EncodeWav(samples);
    }

    private static void AppendTone(List<short> outp, double freq, double seconds, double amplitude)
    {
        int n = (int)(SampleRate * seconds);
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / SampleRate;
            // Cosine attack/decay envelope so the tone fades in and out (no clicks).
            double env = Math.Sin(Math.PI * i / n);
            double s = Math.Sin(2.0 * Math.PI * freq * t) * env * amplitude;
            outp.Add((short)(s * short.MaxValue));
        }
    }

    private static byte[] EncodeWav(List<short> samples)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int dataBytes = samples.Count * sizeof(short);
        const short channels = 1;
        const short bitsPerSample = 16;
        int byteRate = SampleRate * channels * bitsPerSample / 8;
        short blockAlign = (short)(channels * bitsPerSample / 8);

        // RIFF header
        bw.Write(new[] { 'R', 'I', 'F', 'F' });
        bw.Write(36 + dataBytes);
        bw.Write(new[] { 'W', 'A', 'V', 'E' });
        // fmt chunk
        bw.Write(new[] { 'f', 'm', 't', ' ' });
        bw.Write(16);                       // PCM fmt chunk size
        bw.Write((short)1);                 // PCM
        bw.Write(channels);
        bw.Write(SampleRate);
        bw.Write(byteRate);
        bw.Write(blockAlign);
        bw.Write(bitsPerSample);
        // data chunk
        bw.Write(new[] { 'd', 'a', 't', 'a' });
        bw.Write(dataBytes);
        foreach (var s in samples) bw.Write(s);

        bw.Flush();
        return ms.ToArray();
    }

    /// <summary>試播叮咚（畀 UI 預覽）· Preview the chime on a background task (for the UI test button).</summary>
    public Task PreviewChimeAsync() => Task.Run(PlayChime);

    // ===================================================================
    // Helpers
    // ===================================================================

    private void RaiseState() => StateChanged?.Invoke(this, EventArgs.Empty);

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    private static int ParseInt(string s, int fallback) =>
        int.TryParse(s, out var v) ? v : fallback;
}
