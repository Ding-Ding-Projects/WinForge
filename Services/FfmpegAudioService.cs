using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 音訊效果引擎（包 ffmpeg）· A thin wrapper over ffmpeg for the destructive, file-to-file audio effects
/// used by the Audacity-style editor. Every effect reads the editor's current clip and writes a new scratch
/// WAV, which then becomes the current clip (so chaining effects works like an edit history).
/// ffmpeg is invoked as an external binary (never linked) to keep clear of its GPL components.
/// </summary>
public static class FfmpegAudioService
{
    public static bool IsInstalled => MediaService.IsInstalled;
    public static void Rescan() => MediaService.Rescan();

    private static string Q(string p) => $"\"{p}\"";
    private static string Inv(double d) => d.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>
    /// 用一條 -af 濾鏡鏈處理目前嘅 clip · Run an audio-filter chain on the current clip, producing a new
    /// 16-bit WAV scratch file. Returns the new clip path on success.
    /// </summary>
    public static Task<(TweakResult, string?)> ApplyFilterAsync(string input, string filter, CancellationToken ct = default)
        => RunAsync(input, $"-i {{in}} -af \"{filter}\" -c:a pcm_s16le {{out}}", ct);

    /// <summary>用任意參數處理（args 內可用 {in}/{out}）· Run arbitrary ffmpeg args; {in}/{out} placeholders.</summary>
    public static async Task<(TweakResult, string?)> RunAsync(string input, string args, CancellationToken ct = default)
    {
        if (!IsInstalled) return (TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。"), null);
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            return (TweakResult.Fail("No clip loaded.", "未載入音訊。"), null);

        var outp = AudioEngineService.NewScratch(".wav");
        var resolved = "-y " + args.Replace("{in}", Q(input)).Replace("{out}", Q(outp));
        var r = await ShellRunner.Run(MediaService.FFmpeg, resolved, false, ct);
        if (r.Success && File.Exists(outp)) return (r, outp);
        return (r.Success ? TweakResult.Fail("ffmpeg produced no output.", "ffmpeg 冇輸出。", r.Output) : r, null);
    }

    // ===================== effects =====================

    /// <summary>剪裁／截取片段（秒）· Trim to keep only [start,end] seconds.</summary>
    public static Task<(TweakResult, string?)> TrimAsync(string input, double startSec, double endSec, CancellationToken ct = default)
    {
        if (endSec <= startSec) return Task.FromResult<(TweakResult, string?)>(
            (TweakResult.Fail("Empty selection.", "選取範圍係空。"), null));
        var args = $"-ss {Inv(startSec)} -to {Inv(endSec)} -i {{in}} -c:a pcm_s16le {{out}}";
        return RunAsync(input, args, ct);
    }

    /// <summary>刪除選取（保留外面）· Delete the selection, keeping the rest (concat head + tail).</summary>
    public static async Task<(TweakResult, string?)> DeleteSelectionAsync(string input, double startSec, double endSec, double durationSec, CancellationToken ct = default)
    {
        if (endSec <= startSec) return (TweakResult.Fail("Empty selection.", "選取範圍係空。"), null);
        var filter =
            $"[0:a]atrim=0:{Inv(startSec)},asetpts=PTS-STARTPTS[a];" +
            $"[0:a]atrim={Inv(endSec)}:{Inv(durationSec)},asetpts=PTS-STARTPTS[b];" +
            $"[a][b]concat=n=2:v=0:a=1[out]";
        return await RunAsync(input, $"-i {{in}} -filter_complex \"{filter}\" -map \"[out]\" -c:a pcm_s16le {{out}}", ct);
    }

    /// <summary>淡入 · Fade in over the first <paramref name="seconds"/>.</summary>
    public static Task<(TweakResult, string?)> FadeInAsync(string input, double seconds, CancellationToken ct = default)
        => ApplyFilterAsync(input, $"afade=t=in:st=0:d={Inv(Math.Max(0.01, seconds))}", ct);

    /// <summary>淡出 · Fade out over the last <paramref name="seconds"/> (needs total duration).</summary>
    public static Task<(TweakResult, string?)> FadeOutAsync(string input, double seconds, double durationSec, CancellationToken ct = default)
    {
        double d = Math.Max(0.01, seconds);
        double st = Math.Max(0, durationSec - d);
        return ApplyFilterAsync(input, $"afade=t=out:st={Inv(st)}:d={Inv(d)}", ct);
    }

    /// <summary>正規化響度（EBU R128）· Loudness-normalize with loudnorm.</summary>
    public static Task<(TweakResult, string?)> NormalizeAsync(string input, CancellationToken ct = default)
        => ApplyFilterAsync(input, "loudnorm=I=-16:TP=-1.5:LRA=11", ct);

    /// <summary>增益（dB）· Apply a gain in decibels via the volume filter.</summary>
    public static Task<(TweakResult, string?)> GainAsync(string input, double db, CancellationToken ct = default)
        => ApplyFilterAsync(input, $"volume={Inv(db)}dB", ct);

    /// <summary>變速（唔變音高）· Change tempo without changing pitch (atempo, chained for big factors).</summary>
    public static Task<(TweakResult, string?)> SpeedAsync(string input, double factor, CancellationToken ct = default)
        => ApplyFilterAsync(input, BuildAtempoChain(Math.Clamp(factor, 0.25, 4.0)), ct);

    /// <summary>
    /// 變調（半音）· Shift pitch by <paramref name="semitones"/> while preserving duration:
    /// resample the sample rate (asetrate) to bend pitch, then atempo back to original tempo.
    /// </summary>
    public static Task<(TweakResult, string?)> PitchShiftAsync(string input, double semitones, int sampleRate = 44100, CancellationToken ct = default)
    {
        double ratio = Math.Pow(2.0, semitones / 12.0);            // pitch multiplier
        int newRate = (int)Math.Round(sampleRate * ratio);
        // asetrate changes pitch+speed; aresample fixes the declared rate; atempo restores duration.
        var filter = $"asetrate={newRate},aresample={sampleRate},{BuildAtempoChain(1.0 / ratio)}";
        return ApplyFilterAsync(input, filter, ct);
    }

    /// <summary>降噪 · Reduce broadband noise with the FFT denoiser.</summary>
    public static Task<(TweakResult, string?)> NoiseReductionAsync(string input, double db = 12, CancellationToken ct = default)
        => ApplyFilterAsync(input, $"afftdn=nr={Inv(Math.Clamp(db, 1, 97))}:nf=-25", ct);

    /// <summary>反轉 · Reverse the clip.</summary>
    public static Task<(TweakResult, string?)> ReverseAsync(string input, CancellationToken ct = default)
        => ApplyFilterAsync(input, "areverse", ct);

    /// <summary>靜音選取 · Silence the selection range in place.</summary>
    public static Task<(TweakResult, string?)> SilenceSelectionAsync(string input, double startSec, double endSec, CancellationToken ct = default)
    {
        if (endSec <= startSec) return Task.FromResult<(TweakResult, string?)>(
            (TweakResult.Fail("Empty selection.", "選取範圍係空。"), null));
        return ApplyFilterAsync(input, $"volume=enable='between(t,{Inv(startSec)},{Inv(endSec)})':volume=0", ct);
    }

    /// <summary>轉單聲道 · Downmix to mono.</summary>
    public static Task<(TweakResult, string?)> ToMonoAsync(string input, CancellationToken ct = default)
        => RunAsync(input, "-i {in} -ac 1 -c:a pcm_s16le {out}", ct);

    /// <summary>echo / 簡單混響 · Add a simple echo/reverb tail.</summary>
    public static Task<(TweakResult, string?)> EchoAsync(string input, CancellationToken ct = default)
        => ApplyFilterAsync(input, "aecho=0.8:0.88:60:0.4", ct);

    /// <summary>低音 / 高音 EQ · Bass/treble shelf EQ.</summary>
    public static Task<(TweakResult, string?)> EqAsync(string input, double bassDb, double trebleDb, CancellationToken ct = default)
        => ApplyFilterAsync(input, $"bass=g={Inv(bassDb)},treble=g={Inv(trebleDb)}", ct);

    /// <summary>壓縮器 · Dynamic-range compressor.</summary>
    public static Task<(TweakResult, string?)> CompressorAsync(string input, CancellationToken ct = default)
        => ApplyFilterAsync(input, "acompressor=threshold=-18dB:ratio=4:attack=20:release=250", ct);

    // ===================== mix / concat / convert =====================

    /// <summary>混音兩個檔 · Mix the current clip with another file (amix).</summary>
    public static Task<(TweakResult, string?)> MixWithAsync(string input, string other, CancellationToken ct = default)
    {
        if (!File.Exists(other)) return Task.FromResult<(TweakResult, string?)>(
            (TweakResult.Fail("Second file not found.", "搵唔到第二個檔。"), null));
        var args = $"-i {{in}} -i {Q(other)} -filter_complex \"amix=inputs=2:duration=longest:normalize=0\" -c:a pcm_s16le {{out}}";
        return RunAsync(input, args, ct);
    }

    /// <summary>串接兩個檔 · Append another file to the end of the current clip (concat).</summary>
    public static Task<(TweakResult, string?)> ConcatWithAsync(string input, string other, CancellationToken ct = default)
    {
        if (!File.Exists(other)) return Task.FromResult<(TweakResult, string?)>(
            (TweakResult.Fail("Second file not found.", "搵唔到第二個檔。"), null));
        var args = $"-i {{in}} -i {Q(other)} -filter_complex \"[0:a][1:a]concat=n=2:v=0:a=1[out]\" -map \"[out]\" -c:a pcm_s16le {{out}}";
        return RunAsync(input, args, ct);
    }

    /// <summary>
    /// 匯出做指定格式 · Export the current clip to <paramref name="outputPath"/>, picking a codec from
    /// the chosen extension. This writes to a user-chosen destination (not scratch).
    /// </summary>
    public static async Task<TweakResult> ExportAsync(string input, string outputPath, CancellationToken ct = default)
    {
        if (!IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");
        if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            return TweakResult.Fail("No clip loaded.", "未載入音訊。");

        var ext = Path.GetExtension(outputPath).ToLowerInvariant();
        string codec = ext switch
        {
            ".mp3" => "-c:a libmp3lame -q:a 2",
            ".m4a" or ".aac" => "-c:a aac -b:a 256k",
            ".flac" => "-c:a flac",
            ".ogg" => "-c:a libvorbis -q:a 5",
            ".opus" => "-c:a libopus -b:a 160k",
            ".wav" => "-c:a pcm_s16le",
            _ => "-c:a pcm_s16le",
        };
        var args = $"-y -i {Q(input)} {codec} {Q(outputPath)}";
        return await ShellRunner.Run(MediaService.FFmpeg, args, false, ct);
    }

    // atempo only accepts 0.5..2.0; chain multiple stages to reach any factor.
    private static string BuildAtempoChain(double factor)
    {
        factor = Math.Clamp(factor, 0.25, 4.0);
        var sb = new System.Text.StringBuilder();
        double remaining = factor;
        bool first = true;
        // decompose into factors within [0.5, 2.0]
        while (remaining > 2.0 + 1e-9)
        {
            if (!first) sb.Append(',');
            sb.Append("atempo=2.0"); remaining /= 2.0; first = false;
        }
        while (remaining < 0.5 - 1e-9)
        {
            if (!first) sb.Append(',');
            sb.Append("atempo=0.5"); remaining /= 0.5; first = false;
        }
        if (!first) sb.Append(',');
        sb.Append("atempo=").Append(Inv(remaining));
        return sb.ToString();
    }
}
