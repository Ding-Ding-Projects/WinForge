using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個 Feature Store 嘅功能旗標 · One feature flag from the live Feature Store.</summary>
public sealed class ViveFeature
{
    public uint Id { get; set; }
    public string State { get; set; } = "";      // Enabled / Disabled / Default
    public string Priority { get; set; } = "";   // Service / User / Default …
    public string Type { get; set; } = "";       // Override / Experiment …

    /// <summary>人類可讀名（由字典解析）· Human-readable name resolved from the dictionary (may be empty).</summary>
    public string FriendlyEn { get; set; } = "";
    public string FriendlyZh { get; set; } = "";

    public bool IsEnabled => State.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
    public bool IsDisabled => State.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
    public bool IsDefault => string.IsNullOrEmpty(State) || State.Equals("Default", StringComparison.OrdinalIgnoreCase);

    public bool HasFriendly => FriendlyEn.Length > 0;
}

/// <summary>一個有名嘅功能旗標切換（File Explorer tabs 等）· A named feature toggle (File Explorer tabs, etc.).</summary>
public sealed class ViveNamedToggle
{
    public string Key { get; init; } = "";
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";
    /// <summary>呢個切換可能對應嘅 ID 群 · Candidate ids this toggle maps to (build-specific; resolved at runtime).</summary>
    public uint[] Ids { get; init; } = Array.Empty<uint>();
    /// <summary>true = shell-only（重啟 explorer 即可）· shell-only feature (restart explorer is enough).</summary>
    public bool ShellOnly { get; init; }
    public string En2 { get; init; } = "";  // optional sub-note
    public string Zh2 { get; init; } = "";
}

/// <summary>
/// 應用程式內 ViVeTool 功能旗標管理員（包真實 ViVeTool.exe）· In-app ViVeTool feature-flag manager wrapping the
/// real ViVeTool.exe (thebookisclosed/ViVe). Lists the live Feature Store via /query, enables/disables/resets by
/// id, /fullreset, /export, /import, /lkgstatus — plus a bundled human-name dictionary and named toggles.
/// IDs are never hard-coded as truth: the live store is the source of truth and the dictionary is only a label hint.
/// </summary>
public static class ViveToolService
{
    private static string? _cachedPath;

    /// <summary>
    /// 搵 ViVeTool.exe · Locate ViVeTool.exe (PATH, then known winget/manual install locations).
    /// Returns null if not found.
    /// </summary>
    public static async Task<string?> LocateAsync(CancellationToken ct = default)
    {
        if (_cachedPath is not null && File.Exists(_cachedPath)) return _cachedPath;
        _cachedPath = null;

        // 1) On PATH?
        try
        {
            var where = await ShellRunner.Capture("where.exe", "ViVeTool.exe", ct);
            var first = where.Replace("\r", "").Split('\n').FirstOrDefault(l => l.Trim().EndsWith("ViVeTool.exe", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(first) && File.Exists(first.Trim()))
                return _cachedPath = first.Trim();
        }
        catch { /* ignore */ }

        // 2) Common install locations (winget package thebookisclosed.ViVeTool drops a versioned folder).
        var roots = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files",
            Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "tools"),
        };
        foreach (var root in roots.Distinct())
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                // Search a bounded depth for ViVeTool.exe (winget folders look like
                // ...\Microsoft\WinGet\Packages\thebookisclosed.ViVeTool_*\ViVeTool*\ViVeTool.exe).
                foreach (var hit in SafeEnumerate(root, "ViVeTool.exe", 6))
                    if (File.Exists(hit)) return _cachedPath = hit;
            }
            catch { /* ignore */ }
        }
        return null;
    }

    private static IEnumerable<string> SafeEnumerate(string root, string pattern, int maxDepth)
    {
        var stack = new Stack<(string dir, int depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();
            string[] files = Array.Empty<string>();
            try { files = Directory.GetFiles(dir, pattern); } catch { }
            foreach (var f in files) yield return f;
            if (depth >= maxDepth) continue;
            string[] subs = Array.Empty<string>();
            try { subs = Directory.GetDirectories(dir); } catch { }
            foreach (var s in subs) stack.Push((s, depth + 1));
        }
    }

    public static async Task<bool> IsAvailable(CancellationToken ct = default)
        => await LocateAsync(ct) is not null;

    /// <summary>清快取，等啱啱裝完即刻搵到 · Clear the cached path so a just-installed ViVeTool is re-located.</summary>
    public static void Rescan() => _cachedPath = null;

    /// <summary>用 winget 安裝 ViVeTool · Install ViVeTool via winget (thebookisclosed.ViVeTool).</summary>
    public static async Task<TweakResult> InstallViaWinget(CancellationToken ct = default)
    {
        var r = await ShellRunner.RunCmd(
            "winget install --id thebookisclosed.ViVeTool -e --accept-source-agreements --accept-package-agreements",
            elevated: false, ct);
        _cachedPath = null; // force re-detect
        if (r.Success || (await LocateAsync(ct)) is not null)
            return TweakResult.Ok("ViVeTool installed.", "已安裝 ViVeTool。", r.Output);
        return TweakResult.Fail("winget install failed. Is winget available?", "winget 安裝失敗，winget 是否可用？", r.Output);
    }

    private static async Task<string> Capture(string args, CancellationToken ct)
    {
        var exe = await LocateAsync(ct);
        if (exe is null) return "";
        var r = await ShellRunner.Run(exe, args, elevated: false, ct);
        return r.Output ?? "";
    }

    private static async Task<TweakResult> Run(string args, bool elevated, CancellationToken ct)
    {
        var exe = await LocateAsync(ct);
        if (exe is null) return TweakResult.Fail("ViVeTool.exe not found.", "搵唔到 ViVeTool.exe。");
        return await ShellRunner.Run(exe, args, elevated, ct);
    }

    // ---- /query : the live Feature Store -------------------------------------------------

    /// <summary>
    /// 由 ViVeTool /query 讀取整個 Feature Store · Read the full live Feature Store via /query.
    /// 每個 feature 解析 Id / State / Priority / Type 並配對人類可讀名。
    /// </summary>
    public static async Task<List<ViveFeature>> QueryAsync(CancellationToken ct = default)
    {
        var raw = await Capture("/query", ct);
        var list = ParseQuery(raw);
        var dict = ViveDictionary.ById;
        foreach (var f in list)
        {
            if (dict.TryGetValue(f.Id, out var name))
            {
                f.FriendlyEn = name.En;
                f.FriendlyZh = name.Zh;
            }
        }
        return list;
    }

    /// <summary>解析 /query 文字輸出 · Parse the textual /query output into feature rows.</summary>
    public static List<ViveFeature> ParseQuery(string raw)
    {
        var list = new List<ViveFeature>();
        if (string.IsNullOrWhiteSpace(raw)) return list;

        ViveFeature? cur = null;
        foreach (var lineRaw in raw.Replace("\r", "").Split('\n'))
        {
            var line = lineRaw.Trim();
            if (line.Length == 0) continue;

            // Header line, e.g. "[Feature 12345678]" or "[12345678]".
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                var inner = line.Trim('[', ']').Trim();
                if (inner.StartsWith("Feature", StringComparison.OrdinalIgnoreCase))
                    inner = inner.Substring("Feature".Length).Trim();
                if (uint.TryParse(inner, out var id))
                {
                    cur = new ViveFeature { Id = id };
                    list.Add(cur);
                }
                else
                {
                    cur = null; // a section header like [Features in store]
                }
                continue;
            }

            if (cur is null) continue;

            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line.Substring(0, colon).Trim();
            var val = line.Substring(colon + 1).Trim();
            if (key.Equals("State", StringComparison.OrdinalIgnoreCase)) cur.State = val;
            else if (key.Equals("Priority", StringComparison.OrdinalIgnoreCase)) cur.Priority = val;
            else if (key.Equals("Type", StringComparison.OrdinalIgnoreCase)) cur.Type = val;
        }
        return list;
    }

    // ---- enable / disable / reset by id (id resolved/confirmed at runtime) ---------------

    public static Task<TweakResult> Enable(uint id, CancellationToken ct = default)
        => Run($"/enable /id:{id}", elevated: true, ct);

    public static Task<TweakResult> Disable(uint id, CancellationToken ct = default)
        => Run($"/disable /id:{id}", elevated: true, ct);

    public static Task<TweakResult> Reset(uint id, CancellationToken ct = default)
        => Run($"/reset /id:{id}", elevated: true, ct);

    public static Task<TweakResult> EnableMany(IEnumerable<uint> ids, CancellationToken ct = default)
        => Run($"/enable /id:{string.Join(",", ids)}", elevated: true, ct);

    public static Task<TweakResult> DisableMany(IEnumerable<uint> ids, CancellationToken ct = default)
        => Run($"/disable /id:{string.Join(",", ids)}", elevated: true, ct);

    public static Task<TweakResult> ResetMany(IEnumerable<uint> ids, CancellationToken ct = default)
        => Run($"/reset /id:{string.Join(",", ids)}", elevated: true, ct);

    /// <summary>清除全部自訂配置（高危）· Remove every custom feature configuration (guarded by caller).</summary>
    public static Task<TweakResult> FullReset(CancellationToken ct = default)
        => Run("/fullreset", elevated: true, ct);

    // ---- export / import profiles --------------------------------------------------------

    public static Task<TweakResult> Export(string filePath, CancellationToken ct = default)
        => Run($"/export \"{filePath}\"", elevated: true, ct);

    public static Task<TweakResult> Import(string filePath, CancellationToken ct = default)
        => Run($"/import \"{filePath}\"", elevated: true, ct);

    // ---- lkgstatus (read-only) -----------------------------------------------------------

    public static async Task<string> LkgStatus(CancellationToken ct = default)
    {
        var exe = await LocateAsync(ct);
        if (exe is null) return "";
        var r = await ShellRunner.Run(exe, "/lkgstatus", elevated: false, ct);
        return (r.Output ?? "").Trim();
    }

    // ---- apply helpers -------------------------------------------------------------------

    /// <summary>重啟檔案總管（套用 shell-only 功能）· Restart explorer.exe to apply shell-only features.</summary>
    public static Task<TweakResult> RestartExplorer(CancellationToken ct = default)
        => ShellRunner.RunCmd("taskkill /f /im explorer.exe & start explorer.exe", elevated: false, ct);

    /// <summary>立即重新開機（套用 store-level 功能）· Reboot now to apply store-level features.</summary>
    public static Task<TweakResult> Reboot(CancellationToken ct = default)
        => ShellRunner.RunCmd("shutdown /r /t 0", elevated: false, ct);

    // ---- scan: available but disabled experiments ----------------------------------------

    /// <summary>
    /// 掃描「可試但未開」嘅實驗 · Scan for experiments that exist on THIS build but sit at Default/Disabled —
    /// resolved by diffing the live /query store against the bundled name dictionary.
    /// </summary>
    public static async Task<List<ViveFeature>> ScanAvailableDisabled(CancellationToken ct = default)
    {
        var all = await QueryAsync(ct);
        // "available to try" = present in the store with a known friendly name but not currently enabled.
        return all.Where(f => f.HasFriendly && !f.IsEnabled).ToList();
    }

    /// <summary>有名嘅切換 · The named toggles (ids resolved against the live store at runtime).</summary>
    public static IReadOnlyList<ViveNamedToggle> NamedToggles => ViveDictionary.NamedToggles;
}
