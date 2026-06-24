using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 包住 VS Code 嘅 <c>code</c> CLI · A thin wrapper over the Visual Studio Code <c>code</c> command line.
/// 解析 <c>code.cmd</c> 嘅位置（PATH 或者 user-scope 安裝路徑），再經 <see cref="ShellRunner"/> 行所有指令：
/// 開檔／資料夾／工作區、開新視窗、比對檔案、跳去 file:line、列出／安裝／解除安裝擴充功能、profile、tunnel。
/// Resolves <c>code.cmd</c> (on PATH or the user Programs install path), then runs every action through
/// <see cref="ShellRunner"/>: open files/folders/workspaces, new window, diff, goto file:line, list/install/
/// uninstall extensions, profiles and tunnels. Shared by the Git module's "Open in VS Code" action.
/// </summary>
public static class VsCodeService
{
    private static string? _cached;
    private static bool _insiders;

    /// <summary>用緊 Insiders 版本 · Whether actions target the Insiders build (code-insiders).</summary>
    public static bool UseInsiders
    {
        get => _insiders;
        set { if (_insiders != value) { _insiders = value; _cached = null; } }
    }

    private static string ExeStem => _insiders ? "code-insiders" : "code";

    /// <summary>
    /// 搵 <c>code.cmd</c>／<c>code-insiders.cmd</c> · Resolve the CLI shim path. Checks PATH first, then the
    /// user-scope Programs install (<c>%LOCALAPPDATA%\Programs\Microsoft VS Code\bin\code.cmd</c>), the
    /// system install under Program Files, and the Insiders equivalents. Returns the bare command name as a
    /// last resort so cmd's own PATH lookup still gets a chance.
    /// </summary>
    public static string? ResolvePath()
    {
        if (_cached is not null) return _cached;

        var stem = ExeStem;
        var candidates = new List<string>();

        // PATH lookup (where.exe-style) via PATHEXT-aware probing.
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var cmd = Path.Combine(dir.Trim(), stem + ".cmd");
                if (File.Exists(cmd)) candidates.Add(cmd);
            }
            catch { /* ignore malformed PATH entry */ }
        }

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var folder = _insiders ? "Microsoft VS Code Insiders" : "Microsoft VS Code";
        candidates.Add(Path.Combine(local, "Programs", folder, "bin", stem + ".cmd"));
        candidates.Add(Path.Combine(pf, folder, "bin", stem + ".cmd"));

        foreach (var c in candidates)
        {
            try { if (File.Exists(c)) { _cached = c; return _cached; } }
            catch { /* ignore */ }
        }
        return null;
    }

    /// <summary>VS Code 係咪裝咗（<c>code</c> 解析到）· Whether the CLI resolves at all.</summary>
    public static bool IsInstalled => ResolvePath() is not null;

    /// <summary>清除快取嘅路徑（winget 安裝後重新探測）· Clear the cached path (re-probe after a winget install).</summary>
    public static void Rescan()
    {
        _cached = null;
        // Refresh this process's PATH from the machine + user registry so a freshly-installed code resolves.
        try
        {
            var machine = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
            var user = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            Environment.SetEnvironmentVariable("PATH", machine + Path.PathSeparator + user);
        }
        catch { /* not fatal */ }
    }

    private static string Quoted(string p) => p.Contains(' ') && !p.StartsWith('"') ? $"\"{p}\"" : p;

    /// <summary>
    /// 經 cmd 行 <c>code</c>（佢係 .cmd batch shim）· Run the code CLI through cmd.exe because the shim is a
    /// batch file, never CreateProcess directly. Arguments are appended verbatim.
    /// </summary>
    private static async Task<TweakResult> RunCli(string args, CancellationToken ct = default)
    {
        var path = ResolvePath();
        if (path is null)
            return TweakResult.Fail("VS Code (code) was not found. Install it first.",
                "搵唔到 VS Code（code）。請先安裝。");
        // cmd /c "" "<code.cmd>" args  — outer quotes keep paths-with-spaces intact.
        return await ShellRunner.RunCmd($"\"{Quoted(path)} {args}\"", elevated: false, ct);
    }

    // ===== Version / detection =====

    public static async Task<string> Version(CancellationToken ct = default)
    {
        var r = await RunCli("--version", ct);
        var first = (r.Output ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(first) ? "—" : first.Trim();
    }

    // ===== Open actions =====

    /// <summary>開一個檔案／資料夾／工作區 · Open a file, folder or .code-workspace.</summary>
    public static Task<TweakResult> Open(string path, CancellationToken ct = default)
        => RunCli(Quoted(path), ct);

    /// <summary>喺新視窗開 · Open in a brand-new window (-n).</summary>
    public static Task<TweakResult> OpenNewWindow(string path, CancellationToken ct = default)
        => RunCli($"-n {Quoted(path)}", ct);

    /// <summary>喺現有視窗重用開 · Reuse the last active window (-r).</summary>
    public static Task<TweakResult> OpenReuseWindow(string path, CancellationToken ct = default)
        => RunCli($"-r {Quoted(path)}", ct);

    /// <summary>開一個空嘅新視窗 · Open an empty new window.</summary>
    public static Task<TweakResult> OpenEmptyWindow(CancellationToken ct = default)
        => RunCli("-n", ct);

    /// <summary>喺資料夾度開（Git 模組共用）· Open a folder (shared by the Git module's "Open in VS Code").</summary>
    public static Task<TweakResult> OpenFolder(string folder, CancellationToken ct = default)
        => RunCli(Quoted(folder), ct);

    /// <summary>跳去 file:line[:col] · Go to a file at a line/column (-g file:line:col).</summary>
    public static Task<TweakResult> GotoLine(string file, int line, int col, CancellationToken ct = default)
    {
        var target = col > 0 ? $"{file}:{line}:{col}" : $"{file}:{line}";
        return RunCli($"-g {Quoted(target)}", ct);
    }

    /// <summary>比對兩個檔案 · Diff two files (--diff a b), blocking with --wait so it acts like a tool.</summary>
    public static Task<TweakResult> Diff(string a, string b, bool wait, CancellationToken ct = default)
        => RunCli($"{(wait ? "--wait " : "")}--diff {Quoted(a)} {Quoted(b)}", ct);

    /// <summary>合併（4-way）· Merge four files (--merge path1 path2 base result).</summary>
    public static Task<TweakResult> Merge(string p1, string p2, string baseFile, string result, CancellationToken ct = default)
        => RunCli($"--merge {Quoted(p1)} {Quoted(p2)} {Quoted(baseFile)} {Quoted(result)}", ct);

    /// <summary>加資料夾去現有視窗 · Add a folder to the last active window (--add).</summary>
    public static Task<TweakResult> AddFolder(string folder, CancellationToken ct = default)
        => RunCli($"--add {Quoted(folder)}", ct);

    // ===== Extensions =====

    public readonly record struct Extension(string Id, string Version);

    /// <summary>
    /// 列出已安裝擴充功能（連版本）· List installed extensions with versions
    /// (<c>--list-extensions --show-versions</c>). Output rows look like <c>publisher.name@1.2.3</c>.
    /// </summary>
    public static async Task<List<Extension>> ListExtensions(CancellationToken ct = default)
    {
        var list = new List<Extension>();
        var r = await RunCli("--list-extensions --show-versions", ct);
        if (!r.Success) return list;
        foreach (var raw in (r.Output ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.Contains(' ')) continue; // skip stray cmd noise
            var at = line.LastIndexOf('@');
            if (at > 0) list.Add(new Extension(line[..at], line[(at + 1)..]));
            else list.Add(new Extension(line, string.Empty));
        }
        return list.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Task<TweakResult> InstallExtension(string id, CancellationToken ct = default)
        => RunCli($"--install-extension {id} --force", ct);

    public static Task<TweakResult> UninstallExtension(string id, CancellationToken ct = default)
        => RunCli($"--uninstall-extension {id}", ct);

    /// <summary>批次安裝一批擴充功能 ID · Install a batch of extension IDs (import an extension set).</summary>
    public static async Task<TweakResult> InstallMany(IEnumerable<string> ids, IProgress<string>? progress,
        CancellationToken ct = default)
    {
        int ok = 0, fail = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var trimmed = id.Trim();
            if (trimmed.Length == 0) continue;
            progress?.Report($"→ {trimmed}\n");
            var r = await InstallExtension(trimmed, ct);
            if (r.Success) { ok++; progress?.Report($"   ✓ {trimmed}\n"); }
            else { fail++; progress?.Report($"   ✗ {trimmed}\n"); }
        }
        return TweakResult.Ok($"Installed {ok}, failed {fail}.", $"已安裝 {ok}，失敗 {fail}。");
    }

    // ===== Profiles =====

    /// <summary>喺指定 profile 度開 · Open a path under a named profile (--profile).</summary>
    public static Task<TweakResult> OpenWithProfile(string profile, string path, CancellationToken ct = default)
        => RunCli($"--profile {Quoted(profile)} {Quoted(path)}", ct);

    // ===== Tunnel (remote dev) =====

    /// <summary>
    /// 啟動 <c>code tunnel</c>（remote dev）· Launch <c>code tunnel</c> in a visible console so the user can
    /// complete the device-login flow. This one is launched directly (not captured) because it's long-running
    /// and interactive.
    /// </summary>
    public static bool StartTunnel()
    {
        var path = ResolvePath();
        if (path is null) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"\"{path}\" tunnel\"",
                UseShellExecute = true,
            });
            return true;
        }
        catch { return false; }
    }

    /// <summary>喺資料夾度開終端機（VS Code 整合終端機要喺視窗入面）· Open the OS terminal at a folder.</summary>
    public static bool OpenTerminalAt(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "wt.exe", Arguments = $"-d \"{folder}\"", UseShellExecute = true });
            return true;
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo { FileName = "cmd.exe", Arguments = $"/k cd /d \"{folder}\"", UseShellExecute = true }); return true; }
            catch { return false; }
        }
    }

    /// <summary>搵到 user settings.json 嘅路徑 · Path to the user settings.json (for quick-edit).</summary>
    public static string UserSettingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = _insiders ? "Code - Insiders" : "Code";
        return Path.Combine(appData, folder, "User", "settings.json");
    }

    public static string UserKeybindingsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = _insiders ? "Code - Insiders" : "Code";
        return Path.Combine(appData, folder, "User", "keybindings.json");
    }
}
