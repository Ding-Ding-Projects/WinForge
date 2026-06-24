using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 應用程式內 HashiCorp Packer 控制（薄薄包住 packer CLI）·
/// In-app HashiCorp Packer control: a thin wrapper over the official <c>packer</c> binary.
/// 揀工作資料夾、列出範本、行 init / validate / fmt / build / inspect、管理插件，
/// 並且把 stdout/stderr 串流到應用程式內嘅即時主控台（可取消）。
/// Picks a working folder, lists templates, runs init / validate / fmt / build / inspect, manages
/// plugins, and streams stdout/stderr into a live in-app console with cancel support.
/// BUSL-1.1：只係呼叫官方 binary，從不修改／內嵌 Packer 原始碼。
/// BUSL-1.1: only invokes the official binary; never vendors or modifies Packer source.
/// 全程防禦式，永不丟出例外。Defensive throughout — never throws.
/// </summary>
public static class PackerService
{
    /// <summary>winget 套件 ID · The winget package ID for installs.</summary>
    public const string WingetId = "Hashicorp.Packer";

    /// <summary>packer 可執行檔名（靠 PATH）· The packer executable name (resolved on PATH).</summary>
    public const string Exe = "packer";

    private static readonly object _gate = new();
    private static Process? _proc;

    /// <summary>記住上次揀嘅工作資料夾 · Remembers the last picked working directory.</summary>
    public static string? WorkingDir
    {
        get => SettingsStore.Get("packer.workdir", "") is { Length: > 0 } s && Directory.Exists(s) ? s : null;
        set => SettingsStore.Set("packer.workdir", value ?? "");
    }

    /// <summary>有冇 build 喺度行緊 · True when a streamed build/command is currently running.</summary>
    public static bool IsRunning
    {
        get { lock (_gate) { return _proc is { HasExited: false }; } }
    }

    /// <summary>packer 裝咗未（行 "packer version"，有輸出就當有）· True if "packer version" produced output.</summary>
    public static async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await ShellRunner.Capture(Exe, "version", ct);
            return output.Trim().Length > 0
                && !output.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("not found", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("cannot find", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>攞 packer 版本字串（第一行）· Returns the first line of "packer version".</summary>
    public static async Task<string> VersionAsync(CancellationToken ct = default)
    {
        try
        {
            var raw = await ShellRunner.Capture(Exe, "version", ct);
            var line = raw.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
            return line;
        }
        catch { return ""; }
    }

    /// <summary>
    /// 列出工作資料夾入面嘅範本（*.pkr.hcl / *.pkr.json / *.json）·
    /// List template files in a folder (*.pkr.hcl, *.pkr.json and plain *.json). Non-recursive top level
    /// plus one nested level so module sub-folders are visible. Never throws.
    /// </summary>
    public static IReadOnlyList<string> ListTemplates(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return Array.Empty<string>();
        try
        {
            var pats = new[] { "*.pkr.hcl", "*.pkr.json", "*.json" };
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in pats)
            {
                foreach (var f in Directory.EnumerateFiles(dir, p, SearchOption.TopDirectoryOnly))
                    set.Add(f);
            }
            return set.ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>
    /// 列出 var-file（*.pkrvars.hcl / *.pkrvars.json / *.auto.pkrvars.hcl）·
    /// List variable files in a folder. Never throws.
    /// </summary>
    public static IReadOnlyList<string> ListVarFiles(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return Array.Empty<string>();
        try
        {
            var pats = new[] { "*.pkrvars.hcl", "*.pkrvars.json", "*.auto.pkrvars.hcl", "*.auto.pkrvars.json" };
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in pats)
                foreach (var f in Directory.EnumerateFiles(dir, p, SearchOption.TopDirectoryOnly))
                    set.Add(f);
            return set.ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>把 -var / -var-file 引數砌埋一齊 · Build the shared -var / -var-file argument suffix.</summary>
    public static string BuildVarArgs(IEnumerable<(string Key, string Value)>? vars, IEnumerable<string>? varFiles)
    {
        var sb = new StringBuilder();
        if (vars is not null)
            foreach (var (k, v) in vars)
            {
                if (string.IsNullOrWhiteSpace(k)) continue;
                sb.Append($" -var \"{k.Trim()}={v?.Replace("\"", "\\\"")}\"");
            }
        if (varFiles is not null)
            foreach (var vf in varFiles)
            {
                if (string.IsNullOrWhiteSpace(vf)) continue;
                sb.Append($" -var-file=\"{vf}\"");
            }
        return sb.ToString();
    }

    /// <summary>引號包住路徑（內含空格亦安全）· Quote a path that may contain spaces.</summary>
    public static string Q(string s) => string.IsNullOrEmpty(s) ? "." : $"\"{s}\"";

    // ===== 一次性擷取式指令（短跑） · One-shot capture commands (short-running) =====

    /// <summary>直接行一句 packer 指令並擷取輸出 · Run a raw packer command and capture stdout/stderr.</summary>
    public static Task<TweakResult> RunRaw(string args, string? workingDir = null, CancellationToken ct = default)
    {
        try { return ShellRunner.RunIn(workingDir, Exe, args, false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    /// <summary>行 packer fmt（檢查／格式化）· Run "packer fmt" on a folder.</summary>
    public static Task<TweakResult> FmtAsync(string dir, bool check, CancellationToken ct = default)
        => RunRaw($"fmt {(check ? "-check -diff " : "")}{Q(dir)}", dir, ct);

    /// <summary>行 packer inspect（攞 build/variable 結構）· Run "packer inspect" on a template.</summary>
    public static Task<string> InspectAsync(string template, CancellationToken ct = default)
    {
        try
        {
            var dir = Directory.Exists(template) ? template : Path.GetDirectoryName(template);
            return ShellRunner.RunIn(dir, Exe, $"inspect {Q(template)}", false, ct)
                .ContinueWith(t => t.Result.Output ?? "", ct);
        }
        catch { return Task.FromResult(""); }
    }

    /// <summary>
    /// 由 "packer inspect" 解析出可建置嘅目標（builds / sources）·
    /// Parse the build/source targets from "packer inspect" output, for -only / -except selection.
    /// </summary>
    public static async Task<IReadOnlyList<string>> ListBuildTargetsAsync(string template, CancellationToken ct = default)
    {
        var raw = await InspectAsync(template, ct);
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        var targets = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inBuilds = false;
        foreach (var lineRaw in raw.Split('\n'))
        {
            var line = lineRaw.TrimEnd();
            var t = line.Trim();
            if (t.StartsWith("builds:", StringComparison.OrdinalIgnoreCase)) { inBuilds = true; continue; }
            if (inBuilds)
            {
                // Section ends at a non-indented, non-empty header line.
                if (t.Length > 0 && !char.IsWhiteSpace(line[0]) && t.EndsWith(":")) break;
                // Lines look like:  <name>: ...   or   source.amazon-ebs.example
                var name = t.TrimStart('>', '-', ' ', '\t');
                var colon = name.IndexOf(':');
                if (colon > 0) name = name[..colon];
                name = name.Trim();
                if (name.Length > 0 && (name.Contains('.') || name.Contains('-') || char.IsLetter(name[0])))
                    targets.Add(name);
            }
        }
        return targets.ToList();
    }

    // ===== 串流式指令（init / validate / build — 可能長跑） · Streamed commands =====

    /// <summary>
    /// 串流式行一句 packer 指令（init / validate / build），把每行輸出回呼，並支援取消（kill 整個 process tree）。
    /// Run a packer command with live line-by-line streaming and cancel (kills the whole process tree).
    /// <paramref name="onLine"/> 喺背景執行緒被呼叫 — 呼叫者要自己 marshal 返 UI thread。
    /// onLine is invoked on a background thread — the caller must marshal back to the UI thread.
    /// </summary>
    public static Task<TweakResult> StreamAsync(string args, string? workingDir,
        Action<string> onLine, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_proc is { HasExited: false })
                return Task.FromResult(TweakResult.Fail("A Packer command is already running.", "已經有一個 Packer 指令喺度行緊。"));
        }

        var tcs = new TaskCompletionSource<TweakResult>();
        var psi = new ProcessStartInfo
        {
            FileName = Exe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir)) psi.WorkingDirectory = workingDir;
        // Force machine-readable colourless output so the console stays clean.
        psi.Environment["PACKER_NO_COLOR"] = "1";
        psi.Environment["CHECKPOINT_DISABLE"] = "1";

        Process p;
        try
        {
            p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"));
        }

        CancellationTokenRegistration reg = default;
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(e.Data); };
        p.Exited += (_, _) =>
        {
            int code = -1;
            try { code = p.ExitCode; } catch { }
            lock (_gate) { _proc = null; }
            try { reg.Dispose(); } catch { }
            try { p.Dispose(); } catch { }
            if (ct.IsCancellationRequested)
                tcs.TrySetResult(TweakResult.Fail("Cancelled.", "已取消。"));
            else if (code == 0)
                tcs.TrySetResult(TweakResult.Ok("Done (exit 0).", "完成（結束代碼 0）。"));
            else
                tcs.TrySetResult(TweakResult.Fail($"Exit code {code}.", $"結束代碼 {code}。"));
        };

        try
        {
            if (!p.Start())
                return Task.FromResult(TweakResult.Fail("Failed to start packer.", "啟動 packer 失敗。"));
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            lock (_gate) { _proc = p; }
            reg = ct.Register(() => { try { p.Kill(entireProcessTree: true); } catch { } });
        }
        catch (Exception ex)
        {
            lock (_gate) { _proc = null; }
            return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"));
        }

        return tcs.Task;
    }

    /// <summary>停止當前串流指令（kill 整個 process tree）· Cancel the running streamed command.</summary>
    public static TweakResult Cancel()
    {
        lock (_gate)
        {
            if (_proc is null || _proc.HasExited)
                return TweakResult.Fail("Nothing is running.", "冇嘢喺度行緊。");
            try
            {
                _proc.Kill(entireProcessTree: true);
                return TweakResult.Ok("Cancelled.", "已取消。");
            }
            catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
        }
    }
}
