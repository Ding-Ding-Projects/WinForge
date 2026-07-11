using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    /// <summary>程序取消後仍未退出 · Process did not exit within the bounded cancellation cleanup window.</summary>
    public const string ProcessCleanupTimeoutCode = "process-cleanup-timeout";

    /// <summary>
    /// 執行一個程序並擷取輸出 · Run a process and capture stdout/stderr.
    /// elevated=true 且未提權時，會經 UAC 但將輸出導向臨時 log 讀返（唔再靜靜吞掉錯誤）。
    /// When elevated=true and the app is NOT yet elevated, we run via UAC but redirect output to a
    /// temp log we read back — so even elevated runs surface real stderr + exit code.
    /// </summary>
    public static Task<TweakResult> Run(string fileName, string arguments, bool elevated = false,
        CancellationToken ct = default)
        => RunIn(null, fileName, arguments, elevated, ct);

    /// <summary>
    /// Runs a process with a real argument vector rather than a shell-parsed command line.
    /// This is the safe path for values that originate outside WinForge (for example device addresses,
    /// file paths, and remote commands). Argument boundaries are preserved with
    /// <see cref="ProcessStartInfo.ArgumentList"/>.
    /// </summary>
    /// <remarks>
    /// UAC elevation is intentionally not synthesized here. The existing elevated runner must use
    /// cmd.exe to capture output, which would discard the argument-vector safety guarantee. Callers
    /// needing elevation must validate and construct their own trusted command instead.
    /// </remarks>
    public static Task<TweakResult> RunArguments(string fileName, IReadOnlyList<string> arguments,
        bool elevated = false, CancellationToken ct = default)
        => RunArgumentsStreaming(fileName, arguments, onLine: null, elevated: elevated,
            workingDirectory: null, ct: ct);

    /// <summary>
    /// 喺指定資料夾執行程序 · Run a process with an explicit working directory (used by the Git module).
    /// </summary>
    public static Task<TweakResult> RunIn(string? workingDirectory, string fileName, string arguments,
        bool elevated = false, CancellationToken ct = default)
        => RunStreaming(fileName, arguments, onLine: null, elevated: elevated,
            workingDirectory: workingDirectory, ct: ct);

    /// <summary>Streams a process started with an argument vector. See <see cref="RunArguments"/>.</summary>
    public static async Task<TweakResult> RunArgumentsStreaming(string fileName, IReadOnlyList<string> arguments,
        IProgress<string>? onLine, bool elevated = false, string? workingDirectory = null,
        CancellationToken ct = default)
    {
        arguments ??= Array.Empty<string>();
        try
        {
            if (elevated && !AdminHelper.IsElevated)
                return TweakResult.Fail(
                    "Safe argument-list execution cannot prompt for elevation. Run WinForge as administrator first.",
                    "安全參數清單執行唔可以彈出提權提示。請先用管理員身分開啟 WinForge。");

            return await RunRedirected(fileName, arguments, onLine, workingDirectory, ct);
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
    /// 串流執行：逐行報告輸出（畀進度／狀態 UI 用），並擷取完整輸出 + 真正結束代碼。
    /// Streaming run: reports output line-by-line (for a progress/status UI) while capturing the full
    /// combined output and the real exit code. Never throws — always returns a <see cref="TweakResult"/>.
    ///
    /// 非提權：用 async OutputDataReceived/ErrorDataReceived，逐行 <paramref name="onLine"/>。
    /// Non-elevated: uses async OutputDataReceived/ErrorDataReceived, reporting each line via
    /// <paramref name="onLine"/>.
    ///
    /// 提權（未提權 app）：ShellExecute+runas 無法擷取輸出（呢個係 root cause）。改為將指令包成
    /// `cmd /c "&lt;cmd&gt; &gt; "%TEMP%\winforge_install_&lt;guid&gt;.log" 2>&amp;1"`，等佢結束，再讀返個 log、
    /// 串流出去、刪除，放入 <see cref="TweakResult.Output"/>。連提權安裝都會surface真正錯誤 + 結束代碼。
    /// Elevated (app not yet elevated): ShellExecute+runas cannot capture output (the root cause). We wrap
    /// the command to redirect stdout+stderr to a temp log, WaitForExit, then read the log back, stream it,
    /// delete it, and place it in <see cref="TweakResult.Output"/> — so elevated installs surface the real
    /// error + exit code. A declined UAC (Win32 1223) returns a clear bilingual message.
    /// </summary>
    public static async Task<TweakResult> RunStreaming(string fileName, string arguments,
        IProgress<string>? onLine, bool elevated = false, string? workingDirectory = null,
        CancellationToken ct = default)
    {
        try
        {
            if (elevated && !AdminHelper.IsElevated)
                return await RunElevatedViaLog(fileName, arguments, onLine, workingDirectory, ct);

            return await RunRedirected(fileName, arguments, onLine, workingDirectory, ct);
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Cancelled.", "已取消。");
        }
        catch (System.ComponentModel.Win32Exception w) when (w.NativeErrorCode == 1223)
        {
            return TweakResult.Fail(
                "Elevation was declined (UAC cancelled).",
                "已拒絕提權（UAC 被取消）。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    // ── Non-elevated redirected run with live line streaming ─────────────────────
    private static Task<TweakResult> RunRedirected(string fileName, string arguments,
        IProgress<string>? onLine, string? workingDirectory, CancellationToken ct)
        => RunRedirected(fileName, info => info.Arguments = arguments, onLine, workingDirectory, ct);

    private static Task<TweakResult> RunRedirected(string fileName, IReadOnlyList<string> arguments,
        IProgress<string>? onLine, string? workingDirectory, CancellationToken ct)
        => RunRedirected(fileName, info =>
        {
            foreach (var argument in arguments)
                info.ArgumentList.Add(argument ?? string.Empty);
        }, onLine, workingDirectory, ct);

    private static async Task<TweakResult> RunRedirected(string fileName, Action<ProcessStartInfo> setArguments,
        IProgress<string>? onLine, string? workingDirectory, CancellationToken ct)
    {
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        setArguments(info);
        if (!string.IsNullOrEmpty(workingDirectory)) info.WorkingDirectory = workingDirectory;

        using var p = new Process { StartInfo = info, EnableRaisingEvents = true };

        // 用併發佇列保留出現次序 · Keep lines in arrival order across both streams.
        var lines = new ConcurrentQueue<string>();
        void Handle(string? data)
        {
            if (data is null) return;              // stream closed
            lines.Enqueue(data);
            if (!string.IsNullOrWhiteSpace(data))
            {
                try { onLine?.Report(data); } catch { /* never let a bad reporter kill the run */ }
            }
        }
        p.OutputDataReceived += (_, e) => Handle(e.Data);
        p.ErrorDataReceived += (_, e) => Handle(e.Data);

        ct.ThrowIfCancellationRequested();
        if (!p.Start()) return TweakResult.Fail("Failed to start process.", "無法啟動程序。");
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();

        try
        {
            await p.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { /* best effort */ }
            // Cancellation is not complete until the child is actually gone and the async stdout/stderr
            // readers have drained. Bound that wait so a protected/hung child cannot trap a non-closable UI.
            bool exited = false;
            try
            {
                var exitTask = p.WaitForExitAsync(CancellationToken.None);
                exited = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(10))) == exitTask;
                if (exited)
                {
                    await exitTask;
                    p.WaitForExit();
                }
            }
            catch { exited = false; }

            if (!exited)
            {
                int processId = 0;
                try { processId = p.Id; } catch { }
                var captured = string.Join("\n", lines).Trim();
                var detail = $"Process ID {processId} did not exit after cancellation."
                    + (captured.Length > 0 ? $"\n{captured}" : "");
                return TweakResult.Fail(
                    $"Cancellation was requested, but process {processId} did not exit within 10 seconds. It may still be running.",
                    $"已要求取消，但程序 {processId} 喺 10 秒內未有退出，可能仍然運行緊。", detail)
                    with { Code = ProcessCleanupTimeoutCode };
            }
            throw;
        }

        // WaitForExitAsync returns before the async readers necessarily flush; drain them.
        try { p.WaitForExit(); } catch { /* already exited */ }

        var output = string.Join("\n", lines).Trim();
        return p.ExitCode == 0
            ? TweakResult.Ok("Done.", "完成。", output)
            : TweakResult.Fail($"Exit code {p.ExitCode}.", $"結束代碼 {p.ExitCode}。", output);
    }

    // ── Elevated run that redirects to a temp log we read back ───────────────────
    private static async Task<TweakResult> RunElevatedViaLog(string fileName, string arguments,
        IProgress<string>? onLine, string? workingDirectory, CancellationToken ct)
    {
        // 將 <fileName> <arguments> 收成一句，導向 log，並回寫真正 ERRORLEVEL。
        // Fold <fileName> <arguments> into one line, redirect to the log, and echo the real ERRORLEVEL.
        var logPath = Path.Combine(Path.GetTempPath(), $"winforge_install_{Guid.NewGuid():N}.log");
        var inner = $"{QuoteForCmd(fileName)} {arguments}".Trim();

        // cmd /s /c "<inner> > "<log>" 2>&1 & echo EXIT:%ERRORLEVEL%>>"<log>""
        // /s + one outer pair of quotes is the documented single-outer-quote rule.
        var cmdArgs =
            $"/s /c \"{inner} > \"{logPath}\" 2>&1 & echo EXIT:%ERRORLEVEL%>>\"{logPath}\"\"";

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = cmdArgs,
            UseShellExecute = true,           // required for the runas verb
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        if (!string.IsNullOrEmpty(workingDirectory)) psi.WorkingDirectory = workingDirectory;

        using var ep = Process.Start(psi);     // throws Win32Exception(1223) if UAC declined
        if (ep is null)
        {
            TryDelete(logPath);
            return TweakResult.Fail("Failed to start elevated process.", "無法啟動提權程序。");
        }

        try
        {
            await ep.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // 提權後嘅程序已 detached，唔保證殺得死；盡力而為。
            // The elevated child is detached — best-effort kill, then surface the cancel.
            try { if (!ep.HasExited) ep.Kill(entireProcessTree: true); } catch { /* best effort */ }
            var partial = ReadLog(logPath, onLine, out _);
            TryDelete(logPath);
            return TweakResult.Fail("Cancelled.", "已取消。", partial);
        }

        // 讀返個 log，串流每行，抽出真正 EXIT: 代碼。
        // Read the log back, stream each line, and parse the real EXIT: code.
        var body = ReadLog(logPath, onLine, out var exitCode);
        TryDelete(logPath);

        // ShellExecute's own exit code (ep.ExitCode) is cmd's, but the redirected app's real code is EXIT:.
        int code = exitCode ?? ep.ExitCode;
        return code == 0
            ? TweakResult.Ok("Done.", "完成。", body)
            : TweakResult.Fail($"Exit code {code}.", $"結束代碼 {code}。", body);
    }

    /// <summary>讀 log、串流出去、抽 EXIT 代碼 · Read the log, stream lines, extract the EXIT: code.</summary>
    private static string ReadLog(string logPath, IProgress<string>? onLine, out int? exitCode)
    {
        exitCode = null;
        try
        {
            if (!File.Exists(logPath)) return string.Empty;
            var raw = File.ReadAllText(logPath, Encoding.UTF8);
            var sb = new StringBuilder();
            foreach (var line in raw.Replace("\r", "").Split('\n'))
            {
                if (line.StartsWith("EXIT:", StringComparison.Ordinal))
                {
                    if (int.TryParse(line.AsSpan(5).Trim(), out var c)) exitCode = c;
                    continue;                  // don't show the sentinel to the user
                }
                sb.Append(line).Append('\n');
                if (!string.IsNullOrWhiteSpace(line))
                {
                    try { onLine?.Report(line); } catch { /* ignore reporter faults */ }
                }
            }
            return sb.ToString().Trim();
        }
        catch { return string.Empty; }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }

    /// <summary>路徑有空格就用引號包住（畀 cmd）· Quote a path/exe with spaces for cmd.exe.</summary>
    private static string QuoteForCmd(string s)
    {
        s ??= "";
        if (s.Length == 0) return s;
        if (s.StartsWith("\"")) return s;                 // already quoted
        return s.Contains(' ') ? $"\"{s}\"" : s;
    }

    // ── PATH resolution for package-manager CLIs ─────────────────────────────────

    /// <summary>
    /// 解析 winget / choco / scoop 等 CLI 嘅絕對路徑（避免提權後 PATH 唔同而「唔識」）。
    /// Resolve the absolute path of a package-manager CLI (winget / choco / scoop / …) so a different
    /// (elevated / refreshed) PATH doesn't produce a silent "'winget' is not recognized" (9009).
    /// 搵唔到就回原本 token（照舊靠 PATH）· Falls back to the bare token if nothing is found.
    /// </summary>
    public static string ResolveExe(string tool)
    {
        try
        {
            var key = (tool ?? "").Trim().ToLowerInvariant();
            if (key.EndsWith(".exe")) key = key[..^4];

            switch (key)
            {
                case "winget":
                {
                    // winget is an App-Execution-Alias stub under WindowsApps.
                    var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var alias = Path.Combine(local, "Microsoft", "WindowsApps", "winget.exe");
                    if (File.Exists(alias)) return alias;
                    var real = FindPackagedWinget();
                    if (real is not null) return real;
                    break;
                }
                case "choco":
                {
                    var pd = Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData";
                    var choco = Path.Combine(pd, "chocolatey", "bin", "choco.exe");
                    if (File.Exists(choco)) return choco;
                    break;
                }
                case "scoop":
                {
                    var up = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var scoop = Path.Combine(up, "scoop", "shims", "scoop.cmd");
                    if (File.Exists(scoop)) return scoop;
                    break;
                }
            }

            // Generic: fall back to `where` so we still invoke a real path when possible.
            var found = WhereFirst(tool ?? key);
            if (found is not null) return found;
        }
        catch { /* fall through */ }
        return tool ?? "";
    }

    /// <summary>喺 WindowsApps 下搵真正嘅 winget.exe · Locate the real packaged winget.exe.</summary>
    private static string? FindPackagedWinget()
    {
        try
        {
            var pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
            var root = Path.Combine(pf, "WindowsApps");
            if (!Directory.Exists(root)) return null;
            string? best = null;
            foreach (var dir in Directory.EnumerateDirectories(root, "Microsoft.DesktopAppInstaller_*"))
            {
                var candidate = Path.Combine(dir, "winget.exe");
                if (File.Exists(candidate)) best = candidate; // last wins ≈ newest
            }
            return best;
        }
        catch { return null; }
    }

    /// <summary>行 `where` 攞第一個命中 · Run `where` and return the first hit (best-effort).</summary>
    private static string? WhereFirst(string tool)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = tool,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            // Drain stderr too — it is redirected, and an undrained pipe can wedge the child.
            var soTask = p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(4000))
            {
                try { p.Kill(entireProcessTree: true); } catch { }
            }
            try { soTask.Wait(1000); } catch { }
            var outp = soTask.IsCompletedSuccessfully ? soTask.Result : "";
            foreach (var raw in outp.Replace("\r", "").Split('\n'))
            {
                var ln = raw.Trim();
                if (ln.Length > 0 && File.Exists(ln)) return ln;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>
    /// 用管理員權限(UAC)執行一句指令，同時經臨時檔擷取合併輸出 · Run a command line ELEVATED (a real UAC
    /// prompt) while STILL capturing its combined stdout+stderr — the elevated ShellExecute path can't
    /// redirect handles, so we redirect the child's own output to a temp file and read it back. This is
    /// what makes installs both succeed (admin rights) AND surface the real error text.
    /// Returns (exitCode, output, started). started=false ⇒ the UAC prompt was declined / couldn't launch.
    /// </summary>
    public static async Task<(int exitCode, string output, bool started)> RunElevatedCaptureAsync(
        string command, System.Threading.CancellationToken ct = default)
    {
        var log = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"winforge-install-{Guid.NewGuid():N}.log");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                // /c "<command> > "<log>" 2>&1" — the child redirects its own combined output to the temp file.
                Arguments = $"/c \"{command} > \"{log}\" 2>&1\"",
                UseShellExecute = true,     // required for Verb=runas
                Verb = "runas",             // triggers the UAC elevation prompt
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var p = Process.Start(psi);
            if (p is null) return (-1, "", false);
            await p.WaitForExitAsync(ct);
            string outp = "";
            try { if (System.IO.File.Exists(log)) outp = (await System.IO.File.ReadAllTextAsync(log, ct)).Trim(); }
            catch { /* best-effort capture */ }
            return (p.ExitCode, outp, true);
        }
        catch (System.ComponentModel.Win32Exception wex) when (wex.NativeErrorCode == 1223)
        {
            return (-1, "The administrator (UAC) prompt was cancelled.", false);
        }
        catch (OperationCanceledException) { return (-1, "Cancelled.", false); }
        catch (Exception ex) { return (-1, ex.Message, false); }
        finally { try { if (System.IO.File.Exists(log)) System.IO.File.Delete(log); } catch { } }
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
    /// 串流執行一段 PowerShell · Streaming PowerShell run (reports each output line via onLine).
    /// </summary>
    public static Task<TweakResult> RunPowershellStreaming(string script, IProgress<string>? onLine,
        bool elevated = false, CancellationToken ct = default)
    {
        var bytes = Encoding.Unicode.GetBytes(script);
        var encoded = Convert.ToBase64String(bytes);
        return RunStreaming("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            onLine, elevated, workingDirectory: null, ct);
    }

    /// <summary>
    /// 執行一句 cmd 指令 · Run a single cmd.exe command line.
    /// </summary>
    public static Task<TweakResult> RunCmd(string command, bool elevated = false, CancellationToken ct = default)
        => RunStreaming("cmd.exe", $"/s /c \"{command}\"", null, elevated, workingDirectory: null, ct);

    /// <summary>
    /// 串流執行一句 cmd 指令 · Streaming cmd.exe command line (reports each output line via onLine).
    /// </summary>
    public static Task<TweakResult> RunCmdStreaming(string command, IProgress<string>? onLine,
        bool elevated = false, CancellationToken ct = default)
        => RunStreaming("cmd.exe", $"/s /c \"{command}\"", onLine, elevated, workingDirectory: null, ct);

    /// <summary>純擷取輸出（唔理結束代碼）· Capture text output, ignoring exit code.</summary>
    public static async Task<string> Capture(string fileName, string arguments, CancellationToken ct = default)
    {
        var r = await Run(fileName, arguments, elevated: false, ct);
        return r.Output ?? string.Empty;
    }

    /// <summary>Captures output from a process launched with a real argument vector.</summary>
    public static async Task<string> CaptureArguments(string fileName, IReadOnlyList<string> arguments,
        CancellationToken ct = default)
    {
        var r = await RunArguments(fileName, arguments, elevated: false, ct: ct);
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
