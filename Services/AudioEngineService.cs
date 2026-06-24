using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 音訊引擎 · The native-ish audio engine behind the Audacity-style editor.
///
/// 設計：錄音同解碼都用 ffmpeg（dshow 擷取麥克風、解碼任何格式做 WAV），波形峰值喺背景執行緒抽取並降採樣，
/// 完全唔阻塞 UI。播放交畀 Windows 內建嘅 MediaPlayer。冇 GPL 連結、冇額外 NuGet。
/// Design: recording + decoding go through ffmpeg (dshow mic capture, decode any format to WAV); waveform
/// peaks are extracted off the UI thread and downsampled so large files never freeze the UI; playback is
/// left to the Windows-native MediaPlayer. No GPL linking, no extra NuGet. Original code throughout.
/// </summary>
public static class AudioEngineService
{
    private static Process? _recProc;
    private static string? _recPath;

    public static bool IsRecording => _recProc is { HasExited: false };

    /// <summary>工作暫存資料夾（關閉時清走）· Scratch working dir, cleaned on app exit.</summary>
    public static string ScratchDir
    {
        get
        {
            var dir = Path.Combine(Path.GetTempPath(), "WinForge", "AudioEditor");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string NewScratch(string ext)
    {
        if (!ext.StartsWith('.')) ext = "." + ext;
        return Path.Combine(ScratchDir, $"clip_{DateTime.Now:yyyyMMdd_HHmmss_fff}{ext}");
    }

    /// <summary>清走暫存檔 · Best-effort wipe of the scratch dir.</summary>
    public static void CleanupScratch()
    {
        try
        {
            var dir = ScratchDir;
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                try { File.Delete(f); } catch { /* in use */ }
            }
        }
        catch { /* nothing to clean */ }
    }

    // ===================== device enumeration (dshow) =====================

    /// <summary>
    /// 列出可用嘅麥克風（用 ffmpeg dshow）· List capture (microphone) devices via ffmpeg's dshow enumerator.
    /// ffmpeg 將裝置清單印去 stderr，所以要解析 stderr。Returns friendly device names usable as -i audio="…".
    /// </summary>
    public static async Task<IReadOnlyList<string>> ListInputDevicesAsync(CancellationToken ct = default)
    {
        var names = new List<string>();
        if (!MediaService.IsInstalled) return names;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = MediaService.FFmpeg,
                Arguments = "-hide_banner -list_devices true -f dshow -i dummy",
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardErrorEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            if (p is null) return names;
            var err = await p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);

            // dshow prints:  [dshow @ ...] "Microphone (Realtek)" (audio)
            bool inAudio = false;
            foreach (var raw in err.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Contains("DirectShow audio devices", StringComparison.OrdinalIgnoreCase)) { inAudio = true; continue; }
                if (line.Contains("DirectShow video devices", StringComparison.OrdinalIgnoreCase)) { inAudio = false; continue; }
                var m = Regex.Match(line, "\"([^\"]+)\"");
                if (!m.Success) continue;
                var name = m.Groups[1].Value;
                if (name.StartsWith("@", StringComparison.Ordinal)) continue; // alternative-name line
                bool isAudio = inAudio || line.Contains("(audio)", StringComparison.OrdinalIgnoreCase);
                if (isAudio && !names.Contains(name)) names.Add(name);
            }
        }
        catch { /* enumeration failed — return what we have */ }
        return names;
    }

    // ===================== recording (dshow → WAV) =====================

    /// <summary>開始錄音到 WAV · Start recording the chosen mic to a 16-bit 44.1 kHz stereo WAV scratch file.</summary>
    public static TweakResult StartRecording(string deviceName)
    {
        if (IsRecording) return TweakResult.Fail("Already recording.", "已經錄緊。");
        if (!MediaService.IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");
        if (string.IsNullOrWhiteSpace(deviceName)) return TweakResult.Fail("No microphone selected.", "未揀麥克風。");

        var outp = NewScratch(".wav");
        var args = $"-y -f dshow -i audio=\"{deviceName}\" -ac 2 -ar 44100 -c:a pcm_s16le \"{outp}\"";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = MediaService.FFmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            _recProc = Process.Start(psi);
            if (_recProc is null) return TweakResult.Fail("Failed to start ffmpeg.", "無法啟動 ffmpeg。");
            _recPath = outp;
            return TweakResult.Ok("Recording…", "錄緊…");
        }
        catch (Exception ex)
        {
            _recProc = null; _recPath = null;
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>停止錄音，返回 WAV 路徑 · Stop recording cleanly (write 'q' to ffmpeg) and return the WAV path.</summary>
    public static async Task<(TweakResult result, string? path)> StopRecordingAsync()
    {
        var p = _recProc; var path = _recPath;
        _recProc = null; _recPath = null;
        if (p is null) return (TweakResult.Fail("Not recording.", "冇喺度錄。"), null);
        try
        {
            if (!p.HasExited)
            {
                await p.StandardInput.WriteLineAsync("q");
                await p.StandardInput.FlushAsync();
                p.StandardInput.Close();
                if (!p.WaitForExit(4000)) { try { p.Kill(); } catch { } }
            }
        }
        catch { try { if (!p.HasExited) p.Kill(); } catch { } }

        if (path is not null && File.Exists(path) && new FileInfo(path).Length > 1024)
            return (TweakResult.Ok("Saved the recording.", "已儲存錄音。"), path);
        return (TweakResult.Fail("Recording produced no audio.", "錄音冇內容。"), null);
    }

    // ===================== probing =====================

    /// <summary>讀取時長（秒）· Read duration in seconds via ffprobe; null if unknown.</summary>
    public static async Task<double?> GetDurationSecondsAsync(string path, CancellationToken ct = default)
    {
        if (!MediaService.IsInstalled || !File.Exists(path)) return null;
        var r = await ShellRunner.Run(MediaService.FFprobe,
            $"-v error -show_entries format=duration -of default=nw=1:nk=1 \"{path}\"", false, ct);
        var s = (r.Output ?? "").Trim();
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    // ===================== waveform peak extraction =====================

    /// <summary>
    /// 抽取波形峰值 · Decode the file to mono 8 kHz 16-bit PCM via ffmpeg, then downsample into
    /// <paramref name="buckets"/> min/max peak pairs for drawing. Runs entirely off the UI thread.
    /// Returns one float per bucket in 0..1 (absolute peak magnitude).
    /// </summary>
    public static async Task<float[]> ExtractPeaksAsync(string path, int buckets = 1200, CancellationToken ct = default)
    {
        if (!MediaService.IsInstalled || !File.Exists(path) || buckets <= 0)
            return Array.Empty<float>();

        return await Task.Run(async () =>
        {
            // ffmpeg streams raw s16le mono PCM to stdout — we read it as we go.
            var psi = new ProcessStartInfo
            {
                FileName = MediaService.FFmpeg,
                Arguments = $"-v quiet -i \"{path}\" -ac 1 -ar 8000 -f s16le -",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return Array.Empty<float>();

            // First pass: read all samples into memory (mono 8 kHz keeps even an hour clip ~57 MB of shorts —
            // acceptable for an editor; very long files are downsampled hard anyway).
            var samples = new List<short>(1 << 20);
            using (var stream = p.StandardOutput.BaseStream)
            {
                var buf = new byte[1 << 16];
                int read;
                while ((read = await stream.ReadAsync(buf.AsMemory(0, buf.Length), ct)) > 0)
                {
                    for (int i = 0; i + 1 < read; i += 2)
                        samples.Add((short)(buf[i] | (buf[i + 1] << 8)));
                    if (samples.Count > 60_000_000) break; // safety cap (~2 hr at 8 kHz)
                }
            }
            try { await p.WaitForExitAsync(ct); } catch { }

            int n = samples.Count;
            if (n == 0) return Array.Empty<float>();

            int b = Math.Min(buckets, n);
            var peaks = new float[b];
            double per = (double)n / b;
            for (int k = 0; k < b; k++)
            {
                int start = (int)(k * per);
                int end = (int)((k + 1) * per);
                if (end <= start) end = start + 1;
                if (end > n) end = n;
                short max = 0;
                for (int i = start; i < end; i++)
                {
                    short s = samples[i];
                    short a = s == short.MinValue ? short.MaxValue : Math.Abs(s);
                    if (a > max) max = a;
                }
                peaks[k] = max / 32768f;
            }
            return peaks;
        }, ct);
    }
}
