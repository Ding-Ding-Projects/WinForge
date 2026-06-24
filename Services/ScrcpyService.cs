using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>scrcpy 鏡像會話選項 · Options for one scrcpy mirroring session.</summary>
public sealed class ScrcpyOptions
{
    public string Serial { get; set; } = "";
    public int MaxSize { get; set; } = 0;        // 0 = device native; otherwise cap the longest side (px)
    public int Bitrate { get; set; } = 8;        // Mbps
    public bool StayAwake { get; set; } = true;  // keep the screen on while mirroring
    public bool TurnScreenOff { get; set; }      // blank the phone's own screen while mirroring
    public bool ShowTouches { get; set; }
    public bool Record { get; set; }             // record to a file as well as / instead of mirroring
    public string RecordPath { get; set; } = "";

    public string BuildArgs()
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrEmpty(Serial)) sb.Append($"-s {Serial} ");
        if (MaxSize > 0) sb.Append($"--max-size {MaxSize} ");
        if (Bitrate > 0) sb.Append($"--video-bit-rate {Bitrate}M ");
        if (StayAwake) sb.Append("--stay-awake ");
        if (TurnScreenOff) sb.Append("--turn-screen-off ");
        if (ShowTouches) sb.Append("--show-touches ");
        if (Record && !string.IsNullOrEmpty(RecordPath)) sb.Append($"--record \"{RecordPath}\" ");
        return sb.ToString().Trim();
    }
}

/// <summary>
/// 應用程式內螢幕鏡像（包 scrcpy.exe）· In-app screen mirroring wrapping Genymobile's scrcpy — launches a
/// tracked scrcpy process so mirroring can be started/stopped from the UI. scrcpy installs via winget
/// (Genymobile.scrcpy). No redirect — WinForge drives the real binary.
/// </summary>
public static class ScrcpyService
{
    public const string WingetId = "Genymobile.scrcpy";

    private static Process? _proc;

    public static bool IsRunning => _proc is { HasExited: false };

    public static async Task<bool> IsAvailable(CancellationToken ct = default)
    {
        try
        {
            var outp = await ShellRunner.CapturePowershell("scrcpy --version 2>&1 | Out-String -Width 200", ct);
            return outp.Contains("scrcpy", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    public static TweakResult Start(ScrcpyOptions opts)
    {
        if (IsRunning) return TweakResult.Fail("Mirroring is already running.", "已經喺度鏡像緊。");
        try
        {
            // Do NOT redirect stdout/stderr: scrcpy is long-running and chatty, and with no reader an
            // unread redirected pipe would eventually deadlock it. Let it write to its own hidden console.
            var psi = new ProcessStartInfo
            {
                FileName = "scrcpy",
                Arguments = opts.BuildArgs(),
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _proc = Process.Start(psi);
            if (_proc is null) return TweakResult.Fail("Failed to start scrcpy.", "無法啟動 scrcpy。");
            _proc.EnableRaisingEvents = true;
            return TweakResult.Ok("Mirroring started.", "開始鏡像。");
        }
        catch (Exception ex)
        {
            _proc = null;
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    public static void Stop()
    {
        var p = _proc;
        _proc = null;
        try { if (p is { HasExited: false }) p.Kill(true); } catch { }
        try { p?.Dispose(); } catch { }
    }
}
