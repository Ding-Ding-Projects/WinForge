using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 執行外部程序同 PowerShell · Runs external processes and PowerShell, capturing output.
/// 呢個係 app 真正「改變 Windows 11」嘅其中一條途徑（powercfg、ipconfig、sfc 等）。
/// One of the real ways this app changes Windows 11 (powercfg, ipconfig, sfc, DISM…).
/// </summary>
public static class ShellRunner
{
    /// <summary>
    /// 執行一個程序並擷取輸出 · Run a process and capture stdout/stderr.
    /// elevated=true 會經 UAC（無法擷取輸出）· elevated runs via UAC (no captured output).
    /// </summary>
    public static Task<TweakResult> Run(string fileName, string arguments, bool elevated = false,
        CancellationToken ct = default)
        => RunIn(null, fileName, arguments, elevated, ct);

    /// <summary>
    /// 喺指定資料夾執行程序 · Run a process with an explicit working directory (used by the Git module).
    /// </summary>
    public static async Task<TweakResult> RunIn(string? workingDirectory, string fileName, string arguments,
        bool elevated = false, CancellationToken ct = default)
    {
        try
        {
            if (elevated && !AdminHelper.IsElevated)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };
                if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;
                using var ep = Process.Start(psi);
                if (ep is null) return TweakResult.Fail("Failed to start process.", "無法啟動程序。");
                await ep.WaitForExitAsync(ct);
                return ep.ExitCode == 0
                    ? TweakResult.Ok("Done.", "完成。")
                    : TweakResult.Fail($"Exit code {ep.ExitCode}.", $"結束代碼 {ep.ExitCode}。");
            }

            var info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            if (!string.IsNullOrEmpty(workingDirectory)) info.WorkingDirectory = workingDirectory;

            using var p = Process.Start(info);
            if (p is null) return TweakResult.Fail("Failed to start process.", "無法啟動程序。");

            var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
            output = output.Trim();

            return p.ExitCode == 0
                ? TweakResult.Ok("Done.", "完成。", output)
                : TweakResult.Fail($"Exit code {p.ExitCode}.", $"結束代碼 {p.ExitCode}。", output);
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Cancelled.", "已取消。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>
    /// 執行一段 PowerShell（用 EncodedCommand 避免引號地獄）。
    /// Run a PowerShell snippet via -EncodedCommand to dodge quoting issues.
    /// </summary>
    public static Task<TweakResult> RunPowershell(string script, bool elevated = false, CancellationToken ct = default)
    {
        var bytes = Encoding.Unicode.GetBytes(script);
        var encoded = Convert.ToBase64String(bytes);
        return Run("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            elevated, ct);
    }

    /// <summary>
    /// 執行一句 cmd 指令 · Run a single cmd.exe command line.
    /// </summary>
    public static Task<TweakResult> RunCmd(string command, bool elevated = false, CancellationToken ct = default)
        => Run("cmd.exe", $"/c {command}", elevated, ct);

    /// <summary>純擷取輸出（唔理結束代碼）· Capture text output, ignoring exit code.</summary>
    public static async Task<string> Capture(string fileName, string arguments, CancellationToken ct = default)
    {
        var r = await Run(fileName, arguments, elevated: false, ct);
        return r.Output ?? string.Empty;
    }

    public static Task<string> CapturePowershell(string script, CancellationToken ct = default)
    {
        var bytes = Encoding.Unicode.GetBytes(script);
        var encoded = Convert.ToBase64String(bytes);
        return Capture("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", ct);
    }

    /// <summary>執行 PowerShell 並抽返純 JSON（去除雜訊／BOM）· Run PowerShell and return just the JSON.</summary>
    public static async Task<string> CapturePowershellJson(string script, CancellationToken ct = default)
    {
        var raw = await CapturePowershell(script, ct);
        if (string.IsNullOrEmpty(raw)) return "[]";
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('['), b = raw.LastIndexOf(']');
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        int c = raw.IndexOf('{'), d = raw.LastIndexOf('}');
        if (c >= 0 && d > c) return raw.Substring(c, d - c + 1);
        return "[]";
    }
}
