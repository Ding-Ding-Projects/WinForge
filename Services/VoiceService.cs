using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 一把已安裝嘅語音 · One installed SAPI voice (name + culture + gender), for the picker.
/// </summary>
public sealed class VoiceInfo
{
    public string Name { get; init; } = "";
    public string Culture { get; init; } = "";
    public string Gender { get; init; } = "";
    public string Age { get; init; } = "";

    /// <summary>Friendly one-line label, e.g. "Microsoft Zira (en-US, Female)".</summary>
    public string Display =>
        string.IsNullOrEmpty(Culture) ? Name : $"{Name} ({Culture}, {Gender})";
}

/// <summary>
/// 語音朗讀（文字轉語音）· Voice / Read-Aloud module — wraps the built-in Windows SAPI engine via
/// System.Speech. Enumerates installed voices, speaks asynchronously, and exports a WAV file.
/// 100% in-app, no redirects. ROADMAP: "Read selected text aloud (TTS) / export WAV".
/// </summary>
public static class VoiceService
{
    private static SpeechSynthesizer? _player;
    private static readonly object _gate = new();

    /// <summary>True while a SpeakAsync playback is in progress.</summary>
    public static bool IsSpeaking { get; private set; }

    /// <summary>Raised when playback starts/stops/completes so the UI can refresh button state.</summary>
    public static event EventHandler? SpeakingChanged;

    private static void SetSpeaking(bool value)
    {
        IsSpeaking = value;
        SpeakingChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// 列出所有已安裝、啟用嘅語音 · Enumerate every installed & enabled voice (GetInstalledVoices).
    /// Returns an empty list if SAPI is unavailable rather than throwing.
    /// </summary>
    public static List<VoiceInfo> GetVoices()
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
    /// 朗讀文字（非同步播放）· Speak text aloud asynchronously to the default audio device.
    /// rate is -10..10, volume is 0..100. Cancels any current playback first.
    /// </summary>
    public static void Speak(string text, string? voiceName, int rate, int volume)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        Stop();

        lock (_gate)
        {
            _player = new SpeechSynthesizer();
            ApplySettings(_player, voiceName, rate, volume);
            _player.SetOutputToDefaultAudioDevice();
            _player.SpeakCompleted += (_, _) => SetSpeaking(false);
            SetSpeaking(true);
            _player.SpeakAsync(text);
        }
    }

    /// <summary>停止朗讀 · Stop any in-progress playback and release the player.</summary>
    public static void Stop()
    {
        lock (_gate)
        {
            if (_player == null) return;
            try
            {
                _player.SpeakAsyncCancelAll();
                _player.Dispose();
            }
            catch { /* ignore disposal races */ }
            finally
            {
                _player = null;
            }
        }
        SetSpeaking(false);
    }

    /// <summary>
    /// 匯出做 WAV 檔 · Render the spoken text to a WAV file (SetOutputToWaveFile + Speak).
    /// Runs synchronously on a background thread; Dispose flushes/closes the file.
    /// </summary>
    public static Task ExportWavAsync(string text, string? voiceName, int rate, int volume, string path)
    {
        return Task.Run(() =>
        {
            using var synth = new SpeechSynthesizer();
            ApplySettings(synth, voiceName, rate, volume);
            synth.SetOutputToWaveFile(path);
            synth.Speak(text);            // blocking render
            synth.SetOutputToNull();      // close the file handle
        });
    }

    private static void ApplySettings(SpeechSynthesizer synth, string? voiceName, int rate, int volume)
    {
        if (!string.IsNullOrEmpty(voiceName))
        {
            try { synth.SelectVoice(voiceName); } catch { /* fall back to default voice */ }
        }
        synth.Rate = Math.Clamp(rate, -10, 10);
        synth.Volume = Math.Clamp(volume, 0, 100);
    }
}
