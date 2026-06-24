using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 應用程式內 Cloudflare 控制（薄薄包住 cloudflared，以及如有就連 warp-cli）·
/// In-app Cloudflare control: a thin wrapper over the Cloudflare CLIs (cloudflared, and warp-cli where present).
/// 提供安裝偵測、快速指令、擷取輸出，以及為長跑指令（tunnel run、quick tunnel）開一個睇得到嘅終端機。
/// Offers install detection, quick commands, output capture, and a visible terminal for long-running
/// commands (tunnel run, quick tunnel). Defensive throughout — never throws. Install via the Package Manager.
/// </summary>
public static class CloudflareService
{
    /// <summary>winget 套件 ID · The winget package ID for installs.</summary>
    public const string WingetId = "Cloudflare.cloudflared";

    /// <summary>cloudflared 裝咗未（行 "cloudflared --version"，有輸出就當有）· True if "cloudflared --version" produced output.</summary>
    public static async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await Capture("--version", ct);
            return output.Trim().Length > 0
                && !output.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("not found", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>WARP CLI 裝咗未（行 "warp-cli --version"）· True if "warp-cli --version" produced output.</summary>
    public static async Task<bool> IsWarpInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await ShellRunner.Capture("warp-cli", "--version", ct);
            return output.Trim().Length > 0
                && !output.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("not found", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>直接行一句 cloudflared 指令（快速指令用）· Run a raw cloudflared command (for quick commands).</summary>
    public static Task<TweakResult> RunRaw(string args, CancellationToken ct = default)
    {
        try { return ShellRunner.Run("cloudflared", args, false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    /// <summary>直接行一句 warp-cli 指令 · Run a raw warp-cli command.</summary>
    public static Task<TweakResult> Warp(string args, CancellationToken ct = default)
    {
        try { return ShellRunner.Run("warp-cli", args, false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    /// <summary>
    /// 為長跑指令開一個睇得到嘅終端機（例如 tunnel run、quick tunnel）·
    /// Open a visible terminal running "&lt;fileName&gt; &lt;args&gt;" for long-running commands (tunnel run, quick tunnel).
    /// 先試 Windows Terminal（wt.exe），唔得就退返用 cmd.exe /k · Tries Windows Terminal first, falls back to cmd.exe /k.
    /// </summary>
    public static TweakResult LaunchInTerminal(string fileName, string args)
    {
        // 先試 Windows Terminal · Try Windows Terminal first.
        try
        {
            var wt = new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"{fileName} {args}",
                UseShellExecute = true,
            };
            var p = Process.Start(wt);
            if (p is not null)
                return TweakResult.Ok($"Launched in Windows Terminal: {fileName} {args}",
                    $"已喺 Windows Terminal 開：{fileName} {args}");
        }
        catch { /* 退返用 cmd · fall through to cmd */ }

        // 退返用 cmd.exe /k（保留視窗）· Fall back to cmd.exe /k (keeps the window open).
        try
        {
            var cmd = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k {fileName} {args}",
                UseShellExecute = true,
            };
            var p = Process.Start(cmd);
            if (p is not null)
                return TweakResult.Ok($"Launched in cmd: {fileName} {args}",
                    $"已喺 cmd 開：{fileName} {args}");

            return TweakResult.Fail("Failed to start a terminal.", "無法啟動終端機。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Failed to launch terminal: {ex.Message}",
                $"無法開終端機：{ex.Message}");
        }
    }

    /// <summary>行一句 cloudflared 指令並擷取輸出 · Run a cloudflared command and capture its text output.</summary>
    public static Task<string> Capture(string args, CancellationToken ct = default)
    {
        try { return ShellRunner.Capture("cloudflared", args, ct); }
        catch { return Task.FromResult(string.Empty); }
    }
}
