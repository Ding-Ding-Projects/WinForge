using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 應用程式內螢幕錄影（包 ffmpeg gdigrab）· In-app screen recorder wrapping ffmpeg's gdigrab — records
/// the WHOLE desktop (incl. Explorer/Start), unlike Game Bar. Graceful stop via 'q' on stdin.
/// </summary>
public static class ScreenRecorder
{
    private static Process? _proc;
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    public static bool IsRecording => _proc is { HasExited: false };

    public static TweakResult Start(string outputPath, int fps)
    {
        if (IsRecording) return TweakResult.Fail("Already recording.", "已經喺度錄緊。");
        if (!MediaService.IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");

        var args = $"-y -f gdigrab -framerate {Math.Clamp(fps, 5, 60)} -i desktop " +
                   $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{outputPath}\"";
        Process? started = null;
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
            started = Process.Start(psi);
            if (started is null) return TweakResult.Fail("Failed to start ffmpeg.", "無法啟動 ffmpeg。");
            // ffmpeg reports its live progress on stderr. Drain the pipe throughout a recording so
            // a long capture cannot block waiting for an undrained redirected buffer.
            started.ErrorDataReceived += static (_, _) => { };
            started.BeginErrorReadLine();
            _proc = started;
            return TweakResult.Ok("Recording…", "錄緊…");
        }
        catch (Exception ex)
        {
            try { if (started is { HasExited: false }) started.Kill(entireProcessTree: true); } catch { }
            try { started?.Dispose(); } catch { }
            _proc = null;
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    public static async Task<TweakResult> Stop()
    {
        var p = _proc;
        _proc = null;
        if (p is null || p.HasExited) return TweakResult.Fail("Not recording.", "冇喺度錄。");
        try
        {
            await p.StandardInput.WriteLineAsync("q"); // tell ffmpeg to finish cleanly
            await p.StandardInput.FlushAsync();
            p.StandardInput.Close();
            using var stopTimeout = new CancellationTokenSource(StopTimeout);
            await p.WaitForExitAsync(stopTimeout.Token);
            return TweakResult.Ok("Saved the recording.", "已儲存錄影。");
        }
        catch (OperationCanceledException)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            return TweakResult.Fail(
                "ffmpeg did not stop within 15 seconds; the recording process was terminated.",
                "ffmpeg 15 秒內未能停止；錄影程序已終止。");
        }
        catch (Exception ex)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
        finally
        {
            p.Dispose();
        }
    }
}
