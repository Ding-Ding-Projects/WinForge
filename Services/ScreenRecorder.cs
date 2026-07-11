using System;
using System.Diagnostics;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 應用程式內螢幕錄影（包 ffmpeg gdigrab）· In-app screen recorder wrapping ffmpeg's gdigrab — records
/// the WHOLE desktop (incl. Explorer/Start), unlike Game Bar. Graceful stop via 'q' on stdin.
/// </summary>
public static class ScreenRecorder
{
    private static IScreenRecorderProcess? _proc;

    public static bool IsRecording => _proc is { HasExited: false };

    public static TweakResult Start(string outputPath, int fps)
    {
        if (IsRecording) return TweakResult.Fail("Already recording.", "已經喺度錄緊。");
        if (!MediaService.IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");

        var args = $"-y -f gdigrab -framerate {Math.Clamp(fps, 5, 60)} -i desktop " +
                   $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{outputPath}\"";
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
            var process = Process.Start(psi);
            if (process is null) return TweakResult.Fail("Failed to start ffmpeg.", "無法啟動 ffmpeg。");

            var recorderProcess = new ProcessScreenRecorderProcess(process);
            try
            {
                // Continuously drain redirected stderr before exposing the recording session. ffmpeg emits
                // progress there; an unread pipe can otherwise block both encoder work and its q command.
                ScreenRecorderProcessLifecycle.Begin(recorderProcess);
                _proc = recorderProcess;
            }
            catch
            {
                recorderProcess.TryKill();
                recorderProcess.Dispose();
                throw;
            }
            return TweakResult.Ok("Recording…", "錄緊…");
        }
        catch (Exception ex)
        {
            _proc = null;
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    public static async Task<TweakResult> Stop()
    {
        var p = _proc;
        if (p is null || p.HasExited)
        {
            ReleaseExited(p);
            return TweakResult.Fail("Not recording.", "冇喺度錄。");
        }

        var result = await ScreenRecorderProcessLifecycle.StopAsync(p).ConfigureAwait(false);
        if (result.Exited || p.HasExited) ReleaseExited(p);

        return result.Status switch
        {
            ScreenRecorderStopStatus.Saved => TweakResult.Ok("Saved the recording.", "已儲存錄影。"),
            ScreenRecorderStopStatus.StopCommandFailed => TweakResult.Fail(
                "Could not ask ffmpeg to stop; it was terminated and the recording may be incomplete.",
                "無法叫 ffmpeg 停止；已終止，錄影檔可能唔完整。"),
            ScreenRecorderStopStatus.ForcedStop => TweakResult.Fail(
                "ffmpeg did not stop in time and was terminated; the recording may be incomplete.",
                "ffmpeg 未能及時停止，已終止，錄影檔可能唔完整。"),
            _ => TweakResult.Fail(
                "ffmpeg did not exit after the stop timeout. It is still running; try Stop again.",
                "ffmpeg 停止逾時之後仲喺度行緊，請再撳停止。"),
        };
    }

    private static void ReleaseExited(IScreenRecorderProcess? process)
    {
        if (process is null || !process.HasExited || !ReferenceEquals(_proc, process)) return;
        _proc = null;
        process.Dispose();
    }
}
