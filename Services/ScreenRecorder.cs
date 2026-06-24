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
    private static Process? _proc;

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
            _proc = Process.Start(psi);
            if (_proc is null) return TweakResult.Fail("Failed to start ffmpeg.", "無法啟動 ffmpeg。");
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
        _proc = null;
        if (p is null || p.HasExited) return TweakResult.Fail("Not recording.", "冇喺度錄。");
        try
        {
            await p.StandardInput.WriteLineAsync("q"); // tell ffmpeg to finish cleanly
            await p.StandardInput.FlushAsync();
            p.StandardInput.Close();
            await p.WaitForExitAsync();
            return TweakResult.Ok("Saved the recording.", "已儲存錄影。");
        }
        catch (Exception ex)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }
}
