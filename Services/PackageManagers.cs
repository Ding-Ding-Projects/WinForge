using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 一個套件（跨任何管理器）· One package row, manager-agnostic.
/// 可變、可無參數建構，方便綁定到 UI · Mutable and parameterless-constructible for easy UI binding.
/// </summary>
public sealed class PackageItem
{
    /// <summary>顯示名稱 · Display name.</summary>
    public string Name { get; set; } = "";

    /// <summary>套件 ID（用嚟安裝／移除）· Package id used for install/uninstall.</summary>
    public string Id { get; set; } = "";

    /// <summary>已安裝／可用版本字串 · Installed or available version string.</summary>
    public string Version { get; set; } = "";

    /// <summary>更新時嘅較新版本，否則空白 · Newer version for updates, else "".</summary>
    public string AvailableVersion { get; set; } = "";

    /// <summary>來源／bucket／feed，可空 · Per-manager source/bucket/feed, may be "".</summary>
    public string Source { get; set; } = "";

    /// <summary>邊個管理器（如 "winget"）· Which manager produced this (e.g. "winget").</summary>
    public string ManagerKey { get; set; } = "";
}

/// <summary>
/// 一個套件管理器嘅統一介面 · Unified interface over one package manager (UniGetUI-style).
/// 每個方法都要穩陣：包住 shell 呼叫、出錯就回空 list／Fail，永遠唔好擲例外。
/// Every method must be robust: wrap shell calls, return empty lists / Fail on error — NEVER throw.
/// </summary>
public interface IPackageManager
{
    /// <summary>穩定鍵值 · Stable key, e.g. "winget","scoop","choco","pip","npm","dotnet","psgallery","cargo".</summary>
    string Key { get; }

    /// <summary>英文名 · English display name.</summary>
    string NameEn { get; }

    /// <summary>粵語名 · Cantonese display name.</summary>
    string NameZh { get; }

    /// <summary>背後嘅可執行檔 · The backing executable, e.g. "winget","scoop","choco","pip","npm","dotnet","cargo","powershell".</summary>
    string Cli { get; }

    /// <summary>CLI 喺唔喺 PATH 度（行平 "--version"）· Is the CLI present on PATH (cheap "--version" probe).</summary>
    Task<bool> IsAvailableAsync(CancellationToken ct);

    /// <summary>搜尋套件 · Search packages by query.</summary>
    Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct);

    /// <summary>列出已安裝 · List installed packages.</summary>
    Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct);

    /// <summary>列出可更新 · List packages with updates available.</summary>
    Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct);

    /// <summary>安裝 · Install a package by id.</summary>
    Task<TweakResult> InstallAsync(string id, CancellationToken ct);

    /// <summary>移除 · Uninstall a package by id.</summary>
    Task<TweakResult> UninstallAsync(string id, CancellationToken ct);

    /// <summary>更新 · Update a package by id.</summary>
    Task<TweakResult> UpdateAsync(string id, CancellationToken ct);
}

/// <summary>
/// 共用工具：穩陣解析 · Shared helpers for defensive parsing — never throw on bad input.
/// </summary>
internal static class PkgParse
{
    /// <summary>安全去掉引號避免 shell 出事 · Strip quotes so a query can't break the shell line.</summary>
    public static string Q(string s) => (s ?? "").Replace("\"", "").Replace("`", "").Trim();

    /// <summary>PowerShell single-quoted literal, safe for user search text and package IDs.</summary>
    public static string Ps(string s) => "'" + (s ?? "").Replace("'", "''") + "'";

    /// <summary>切成一行行（統一換行）· Split text into lines on any newline.</summary>
    public static string[] Lines(string s)
        => string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Replace("\r", "").Split('\n');

    /// <summary>嘗試攞 JSON 屬性字串 · Best-effort read a string property from a JSON element.</summary>
    public static string Str(JsonElement el, string prop)
    {
        try
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v))
            {
                return v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString() ?? "",
                    JsonValueKind.Number => v.ToString(),
                    _ => v.ToString(),
                };
            }
        }
        catch { }
        return "";
    }
}

/// <summary>winget 管理器 · winget package manager (self-contained column-table parser).</summary>
public sealed class WingetManager : IPackageManager
{
    public string Key => "winget";
    public string NameEn => "Windows Package Manager";
    public string NameZh => "Windows 套件管理員";
    public string Cli => "winget";

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.Capture("winget", "--version", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.CapturePowershell(
                $"winget search --query {PkgParse.Ps(query.Trim())} --accept-source-agreements --disable-interactivity | Out-String -Width 400", ct);
            return ParseTable(o);
        }
        catch { return new List<PackageItem>(); }
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.CapturePowershell(
                "winget list --accept-source-agreements --disable-interactivity | Out-String -Width 400", ct);
            return ParseTable(o);
        }
        catch { return new List<PackageItem>(); }
    }

    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.CapturePowershell(
                "winget upgrade --accept-source-agreements --disable-interactivity | Out-String -Width 400", ct);
            var list = ParseTable(o);
            // 表內第二個版本欄（Available）放咗去 AvailableVersion · Available column maps to AvailableVersion.
            return list;
        }
        catch { return new List<PackageItem>(); }
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafeRun($"winget install --id {PkgParse.Q(id)} -e --silent --accept-source-agreements --accept-package-agreements --disable-interactivity", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafeRun($"winget uninstall --id {PkgParse.Q(id)} -e --silent --disable-interactivity", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafeRun($"winget upgrade --id {PkgParse.Q(id)} -e --silent --accept-source-agreements --accept-package-agreements --disable-interactivity", ct);

    private static async Task<TweakResult> SafeRun(string cmd, CancellationToken ct)
    {
        try { return await ShellRunner.RunCmd(cmd, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>用標題列嘅欄位起始位置切欄 · Parse winget's column table by header column offsets.</summary>
    private List<PackageItem> ParseTable(string outp)
    {
        var res = new List<PackageItem>();
        try
        {
            if (string.IsNullOrWhiteSpace(outp)) return res;
            var lines = PkgParse.Lines(outp);

            int hdr = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Contains("Id") && lines[i].Contains("Version")) { hdr = i; break; }
            if (hdr < 0 || hdr + 2 > lines.Length) return res;

            var h = lines[hdr];
            int idCol = h.IndexOf("Id", StringComparison.Ordinal);
            int verCol = h.IndexOf("Version", StringComparison.Ordinal);
            int availCol = h.IndexOf("Available", StringComparison.Ordinal);
            int matchCol = h.IndexOf("Match", StringComparison.Ordinal);
            int srcCol = h.IndexOf("Source", StringComparison.Ordinal);
            int endVer = Min4(availCol, matchCol, srcCol, h.Length);

            for (int i = hdr + 2; i < lines.Length; i++)
            {
                var ln = lines[i];
                if (ln.Trim().Length == 0 || ln.TrimStart().StartsWith("---")) continue;
                var name = Cut(ln, 0, idCol);
                var id = Cut(ln, idCol, verCol);
                var ver = Cut(ln, verCol, endVer);
                var avail = availCol > 0 ? Cut(ln, availCol, Min4(matchCol, srcCol, -1, ln.Length)) : "";
                var src = srcCol > 0 && srcCol < ln.Length ? ln.Substring(Math.Min(srcCol, ln.Length)).Trim() : "";
                if (id.Length > 0 && !id.Contains(' '))
                    res.Add(new PackageItem
                    {
                        Name = name,
                        Id = id,
                        Version = ver,
                        AvailableVersion = avail,
                        Source = src,
                        ManagerKey = Key,
                    });
            }
        }
        catch { /* swallow — defensive */ }
        return res;
    }

    private static string Cut(string ln, int a, int b)
    {
        if (a < 0 || a >= ln.Length) return "";
        b = Math.Min(b, ln.Length);
        return b > a ? ln.Substring(a, b - a).Trim() : "";
    }

    private static int Min4(int a, int b, int c, int d)
    {
        int m = d;
        if (a > 0) m = Math.Min(m, a);
        if (b > 0) m = Math.Min(m, b);
        if (c > 0) m = Math.Min(m, c);
        return m;
    }
}

/// <summary>Scoop 管理器 · Scoop package manager.</summary>
public sealed class ScoopManager : IPackageManager
{
    public string Key => "scoop";
    public string NameEn => "Scoop";
    public string NameZh => "Scoop";
    public string Cli => "scoop";

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            // scoop 係 PowerShell shim，行 PowerShell 探測最穩 · scoop is a shim; probe via PowerShell.
            var o = await ShellRunner.CapturePowershell("scoop --version", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var o = await ShellRunner.CapturePowershell($"scoop search {PkgParse.Ps(query.Trim())} | Out-String -Width 400", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0) continue;
                if (ln.StartsWith("Name") || ln.StartsWith("---") || ln.StartsWith("Results")) continue;
                var parts = ln.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                var name = parts[0];
                var ver = parts.Length > 1 ? parts[1] : "";
                if (ver.StartsWith("(")) ver = ver.Trim('(', ')');
                var src = parts.Length > 2 ? parts[2] : "";
                if (name.Contains("'") || name.Contains(":")) continue;
                res.Add(new PackageItem { Name = name, Id = name, Version = ver, Source = src, ManagerKey = Key });
            }
        }
        catch { }
        return res;
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var json = await ShellRunner.CapturePowershellJson("scoop export", ct);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                // 新版 scoop export 係 { apps: [ {Name,Version,Source} ] }，舊版係 array · handle both shapes.
                JsonElement apps;
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("apps", out var a))
                    apps = a;
                else
                    apps = root;
                if (apps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in apps.EnumerateArray())
                    {
                        var name = PkgParse.Str(el, "Name");
                        if (name.Length == 0) name = PkgParse.Str(el, "name");
                        if (name.Length == 0) continue;
                        var ver = PkgParse.Str(el, "Version");
                        if (ver.Length == 0) ver = PkgParse.Str(el, "version");
                        var src = PkgParse.Str(el, "Source");
                        if (src.Length == 0) src = PkgParse.Str(el, "Bucket");
                        res.Add(new PackageItem { Name = name, Id = name, Version = ver, Source = src, ManagerKey = Key });
                    }
                    if (res.Count > 0) return res;
                }
            }
            catch { }

            // 後備：解析 "scoop list" 表 · Fallback: parse "scoop list" table.
            var o = await ShellRunner.CapturePowershell("scoop list | Out-String -Width 400", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0) continue;
                if (ln.StartsWith("Name") || ln.StartsWith("---") || ln.StartsWith("Installed")) continue;
                var parts = ln.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                res.Add(new PackageItem
                {
                    Name = parts[0],
                    Id = parts[0],
                    Version = parts[1],
                    Source = parts.Length > 2 ? parts[2] : "",
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var o = await ShellRunner.CapturePowershell("scoop status | Out-String -Width 400", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0) continue;
                if (ln.StartsWith("Name") || ln.StartsWith("---") || ln.StartsWith("Scoop")
                    || ln.StartsWith("Everything") || ln.StartsWith("WARN") || ln.StartsWith("Updates")) continue;
                var parts = ln.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;
                res.Add(new PackageItem
                {
                    Name = parts[0],
                    Id = parts[0],
                    Version = parts[1],
                    AvailableVersion = parts[2],
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafePwsh($"scoop install {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafePwsh($"scoop uninstall {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafePwsh($"scoop update {PkgParse.Q(id)}", ct);

    private static async Task<TweakResult> SafePwsh(string script, CancellationToken ct)
    {
        try { return await ShellRunner.RunPowershell(script, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}

/// <summary>Chocolatey 管理器 · Chocolatey package manager.</summary>
public sealed class ChocoManager : IPackageManager
{
    public string Key => "choco";
    public string NameEn => "Chocolatey";
    public string NameZh => "Chocolatey";
    public string Cli => "choco";

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.Capture("choco", "--version", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var o = await ShellRunner.Capture("choco", $"search {PkgParse.Q(query)} --limit-output", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0 || !ln.Contains('|')) continue;
                var parts = ln.Split('|');
                if (parts.Length < 1 || parts[0].Length == 0) continue;
                res.Add(new PackageItem
                {
                    Name = parts[0],
                    Id = parts[0],
                    Version = parts.Length > 1 ? parts[1] : "",
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var o = await ShellRunner.Capture("choco", "list --local-only --limit-output", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0 || !ln.Contains('|')) continue;
                var parts = ln.Split('|');
                if (parts.Length < 1 || parts[0].Length == 0) continue;
                res.Add(new PackageItem
                {
                    Name = parts[0],
                    Id = parts[0],
                    Version = parts.Length > 1 ? parts[1] : "",
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            // 行格式："id|cur|avail|pinned" · Lines: "id|cur|avail|pinned".
            var o = await ShellRunner.Capture("choco", "outdated --limit-output", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0 || !ln.Contains('|')) continue;
                var parts = ln.Split('|');
                if (parts.Length < 3 || parts[0].Length == 0) continue;
                res.Add(new PackageItem
                {
                    Name = parts[0],
                    Id = parts[0],
                    Version = parts[1],
                    AvailableVersion = parts[2],
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafeRun($"choco install {PkgParse.Q(id)} -y", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafeRun($"choco uninstall {PkgParse.Q(id)} -y", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafeRun($"choco upgrade {PkgParse.Q(id)} -y", ct);

    private static async Task<TweakResult> SafeRun(string cmd, CancellationToken ct)
    {
        try { return await ShellRunner.RunCmd(cmd, true, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}

/// <summary>pip 管理器 · pip (Python) package manager.</summary>
public sealed class PipManager : IPackageManager
{
    public string Key => "pip";
    public string NameEn => "pip (Python)";
    public string NameZh => "pip（Python）";
    public string Cli => "pip";

    private static readonly HttpClient PyPiHttp = CreatePyPiClient();
    private static readonly SemaphoreSlim PyPiIndexGate = new(1, 1);
    private static readonly SemaphoreSlim PyPiVersionGate = new(6, 6);
    private static readonly object PyPiCacheLock = new();
    private static string[]? _pyPiProjectNames;
    private static DateTimeOffset _pyPiProjectNamesAt;

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.Capture("pip", "--version", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    /// <summary>
    /// 經 PyPI Simple JSON API 搜尋；索引快取 24 小時，再有限度並行攞最新版本。
    /// Search the PyPI Simple JSON API, cache its index for 24 hours, then fetch latest versions
    /// with bounded concurrency. Modern pip deliberately has no search command.
    /// </summary>
    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<PackageItem>();

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));

            var names = await GetPyPiProjectNamesAsync(timeout.Token);
            var q = query.Trim();
            var matches = names
                .Where(n => n.Contains(q, StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n.StartsWith(q, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(n => n.Length)
                .ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToArray();
            if (matches.Length == 0) return new List<PackageItem>();

            var versions = await Task.WhenAll(matches.Select(n => GetLatestPyPiVersionAsync(n, timeout.Token)));
            var result = new List<PackageItem>(matches.Length);
            for (int i = 0; i < matches.Length; i++)
            {
                result.Add(new PackageItem
                {
                    Name = matches[i],
                    Id = matches[i],
                    Version = versions[i],
                    Source = "pypi.org",
                    ManagerKey = Key,
                });
            }
            return result;
        }
        catch { return new List<PackageItem>(); }
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var json = await ShellRunner.Capture("pip", "list --format=json", ct);
            json = ExtractJson(json);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var name = PkgParse.Str(el, "name");
                    if (name.Length == 0) continue;
                    res.Add(new PackageItem
                    {
                        Name = name,
                        Id = name,
                        Version = PkgParse.Str(el, "version"),
                        ManagerKey = Key,
                    });
                }
            }
        }
        catch { return new List<PackageItem>(); }
        return res;
    }

    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var json = await ShellRunner.Capture("pip", "list --outdated --format=json", ct);
            json = ExtractJson(json);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var name = PkgParse.Str(el, "name");
                    if (name.Length == 0) continue;
                    res.Add(new PackageItem
                    {
                        Name = name,
                        Id = name,
                        Version = PkgParse.Str(el, "version"),
                        AvailableVersion = PkgParse.Str(el, "latest_version"),
                        ManagerKey = Key,
                    });
                }
            }
        }
        catch { return new List<PackageItem>(); }
        return res;
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafeRun($"pip install {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafeRun($"pip uninstall -y {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafeRun($"pip install --upgrade {PkgParse.Q(id)}", ct);

    private static async Task<TweakResult> SafeRun(string cmd, CancellationToken ct)
    {
        try { return await ShellRunner.RunCmd(cmd, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    private static string ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "[]";
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('['), b = raw.LastIndexOf(']');
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        return "[]";
    }

    private static HttpClient CreatePyPiClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge/1.0");
        return client;
    }

    private static async Task<string[]> GetPyPiProjectNamesAsync(CancellationToken ct)
    {
        lock (PyPiCacheLock)
        {
            if (_pyPiProjectNames is not null
                && DateTimeOffset.UtcNow - _pyPiProjectNamesAt < TimeSpan.FromHours(24))
                return _pyPiProjectNames;
        }

        await PyPiIndexGate.WaitAsync(ct);
        try
        {
            lock (PyPiCacheLock)
            {
                if (_pyPiProjectNames is not null
                    && DateTimeOffset.UtcNow - _pyPiProjectNamesAt < TimeSpan.FromHours(24))
                    return _pyPiProjectNames;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://pypi.org/simple/");
            request.Headers.Accept.ParseAdd("application/vnd.pypi.simple.v1+json");
            using var response = await PyPiHttp.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) return Array.Empty<string>();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("projects", out var projects)
                || projects.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var names = projects.EnumerateArray()
                .Select(p => PkgParse.Str(p, "name"))
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length == 0) return names;

            lock (PyPiCacheLock)
            {
                _pyPiProjectNames = names;
                _pyPiProjectNamesAt = DateTimeOffset.UtcNow;
            }
            return names;
        }
        finally { PyPiIndexGate.Release(); }
    }

    private static async Task<string> GetLatestPyPiVersionAsync(string packageName, CancellationToken ct)
    {
        bool entered = false;
        try
        {
            await PyPiVersionGate.WaitAsync(ct);
            entered = true;
            var id = Uri.EscapeDataString(packageName);
            using var response = await PyPiHttp.GetAsync($"https://pypi.org/pypi/{id}/json", ct);
            if (!response.IsSuccessStatusCode) return "";
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return doc.RootElement.TryGetProperty("info", out var info)
                ? PkgParse.Str(info, "version")
                : "";
        }
        catch { return ""; }
        finally { if (entered) PyPiVersionGate.Release(); }
    }
}

/// <summary>npm 管理器（全域）· npm (Node) global package manager.</summary>
public sealed class NpmManager : IPackageManager
{
    public string Key => "npm";
    public string NameEn => "npm (Node global)";
    public string NameZh => "npm（Node 全域）";
    public string Cli => "npm";

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.Capture("npm", "--version", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var json = await ShellRunner.Capture("npm", $"search {PkgParse.Q(query)} --json", ct);
            json = ExtractArray(json);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var name = PkgParse.Str(el, "name");
                    if (name.Length == 0) continue;
                    res.Add(new PackageItem
                    {
                        Name = name,
                        Id = name,
                        Version = PkgParse.Str(el, "version"),
                        ManagerKey = Key,
                    });
                }
            }
        }
        catch { return new List<PackageItem>(); }
        return res;
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var json = await ShellRunner.Capture("npm", "ls -g --depth=0 --json", ct);
            json = ExtractObject(json);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("dependencies", out var deps)
                && deps.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in deps.EnumerateObject())
                {
                    var name = prop.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    res.Add(new PackageItem
                    {
                        Name = name,
                        Id = name,
                        Version = PkgParse.Str(prop.Value, "version"),
                        ManagerKey = Key,
                    });
                }
            }
        }
        catch { return new List<PackageItem>(); }
        return res;
    }

    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var json = await ShellRunner.Capture("npm", "outdated -g --json", ct);
            json = ExtractObject(json);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var name = prop.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    res.Add(new PackageItem
                    {
                        Name = name,
                        Id = name,
                        Version = PkgParse.Str(prop.Value, "current"),
                        AvailableVersion = PkgParse.Str(prop.Value, "latest"),
                        ManagerKey = Key,
                    });
                }
            }
        }
        catch { return new List<PackageItem>(); }
        return res;
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafeRun($"npm install -g {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafeRun($"npm uninstall -g {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafeRun($"npm install -g {PkgParse.Q(id)}@latest", ct);

    private static async Task<TweakResult> SafeRun(string cmd, CancellationToken ct)
    {
        try { return await ShellRunner.RunCmd(cmd, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    private static string ExtractArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "[]";
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('['), b = raw.LastIndexOf(']');
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        return "[]";
    }

    private static string ExtractObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "{}";
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('{'), b = raw.LastIndexOf('}');
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        return "{}";
    }
}

/// <summary>.NET 全域工具管理器 · dotnet global tool manager.</summary>
public sealed class DotnetToolManager : IPackageManager
{
    public string Key => "dotnet";
    public string NameEn => ".NET Tools";
    public string NameZh => ".NET 工具";
    public string Cli => "dotnet";

    private static readonly HttpClient NuGetHttp = CreateNuGetClient();
    private static readonly SemaphoreSlim NuGetLookupGate = new(6, 6);

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.Capture("dotnet", "--version", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var o = await ShellRunner.Capture("dotnet", $"tool search {PkgParse.Q(query)}", ct);
            // 表頭：Package Id / Latest Version / Authors / Downloads · header columns vary.
            bool past = false;
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.TrimEnd();
                if (ln.Trim().Length == 0) continue;
                if (!past)
                {
                    if (ln.TrimStart().StartsWith("---")) { past = true; }
                    continue;
                }
                var parts = ln.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                if (parts.Length < 1) continue;
                var name = parts[0];
                if (name.Length == 0) continue;
                res.Add(new PackageItem
                {
                    Name = name,
                    Id = name,
                    Version = parts.Length > 1 ? parts[1] : "",
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var o = await ShellRunner.Capture("dotnet", "tool list -g", ct);
            // 表：Package Id / Version / Commands · table columns.
            bool past = false;
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.TrimEnd();
                if (ln.Trim().Length == 0) continue;
                if (!past)
                {
                    if (ln.TrimStart().StartsWith("---")) { past = true; }
                    continue;
                }
                var parts = ln.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
                if (parts.Length < 1) continue;
                var name = parts[0];
                if (name.Length == 0) continue;
                res.Add(new PackageItem
                {
                    Name = name,
                    Id = name,
                    Version = parts.Length > 1 ? parts[1] : "",
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    /// <summary>
    /// dotnet 冇 outdated 子命令；用已安裝清單配 NuGet V3 版本索引比較。
    /// dotnet has no outdated command, so compare installed tools with NuGet's V3 version index.
    /// </summary>
    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(35));
            var installed = await ListInstalledAsync(timeout.Token);
            if (installed.Count == 0) return new List<PackageItem>();

            var candidates = await Task.WhenAll(
                installed.Select(p => GetDotnetToolUpdateAsync(p, timeout.Token)));
            return candidates.Where(p => p is not null).Select(p => p!).ToList();
        }
        catch { return new List<PackageItem>(); }
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafeRun($"dotnet tool install -g {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafeRun($"dotnet tool uninstall -g {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafeRun($"dotnet tool update -g {PkgParse.Q(id)}", ct);

    private static async Task<TweakResult> SafeRun(string cmd, CancellationToken ct)
    {
        try { return await ShellRunner.RunCmd(cmd, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    private static HttpClient CreateNuGetClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge/1.0");
        return client;
    }

    private async Task<PackageItem?> GetDotnetToolUpdateAsync(PackageItem installed, CancellationToken ct)
    {
        bool entered = false;
        try
        {
            await NuGetLookupGate.WaitAsync(ct);
            entered = true;
            var id = Uri.EscapeDataString(installed.Id.ToLowerInvariant());
            using var response = await NuGetHttp.GetAsync(
                $"https://api.nuget.org/v3-flatcontainer/{id}/index.json", ct);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("versions", out var versions)
                || versions.ValueKind != JsonValueKind.Array)
                return null;

            string latestStable = "";
            foreach (var value in versions.EnumerateArray())
            {
                var version = value.GetString() ?? "";
                if (version.Length == 0 || version.Contains('-')) continue;
                if (latestStable.Length == 0 || IsNewerVersion(version, latestStable))
                    latestStable = version;
            }
            if (latestStable.Length == 0 || !IsNewerVersion(latestStable, installed.Version))
                return null;

            return new PackageItem
            {
                Name = installed.Name,
                Id = installed.Id,
                Version = installed.Version,
                AvailableVersion = latestStable,
                Source = "nuget.org",
                ManagerKey = Key,
            };
        }
        catch { return null; }
        finally { if (entered) NuGetLookupGate.Release(); }
    }

    private static bool IsNewerVersion(string available, string current)
    {
        if (SemverRangeService.TryParse(available, out var a, out _)
            && SemverRangeService.TryParse(current, out var c, out _))
            return a.CompareTo(c) > 0;

        var availableCore = available.Split(new[] { '-', '+' }, 2)[0];
        var currentCore = current.Split(new[] { '-', '+' }, 2)[0];
        if (!Version.TryParse(availableCore, out var av) || !Version.TryParse(currentCore, out var cv))
            return false;
        int comparison = av.CompareTo(cv);
        return comparison > 0
            || (comparison == 0 && current.Contains('-') && !available.Contains('-'));
    }
}

/// <summary>PowerShell Gallery 模組管理器 · PowerShell Gallery module manager.</summary>
public sealed class PsGalleryManager : IPackageManager
{
    public string Key => "psgallery";
    public string NameEn => "PowerShell Gallery";
    public string NameZh => "PowerShell 資源庫";
    public string Cli => "powershell";

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.CapturePowershell("$PSVersionTable.PSVersion.ToString()", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        try
        {
            var q = PkgParse.Ps("*" + query.Trim() + "*");
            var json = await ShellRunner.CapturePowershellJson(
                $"Find-Module -Name {q} -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json", ct);
            return FromNameVersionJson(json);
        }
        catch { return new List<PackageItem>(); }
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        try
        {
            var json = await ShellRunner.CapturePowershellJson(
                "Get-InstalledModule -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json", ct);
            return FromNameVersionJson(json);
        }
        catch { return new List<PackageItem>(); }
    }

    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            const string script =
                "$ErrorActionPreference = 'SilentlyContinue'; " +
                "$updates = @(Get-InstalledModule -ErrorAction SilentlyContinue | Group-Object Name | ForEach-Object { " +
                "$installed = $_.Group | Sort-Object Version -Descending | Select-Object -First 1; " +
                "$available = Find-Module -Name $installed.Name -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; " +
                "if ($null -ne $available -and $available.Version -gt $installed.Version) { " +
                "[pscustomobject]@{ Name = $installed.Name; Version = $installed.Version.ToString(); " +
                "AvailableVersion = $available.Version.ToString(); Repository = $available.Repository } } }); " +
                "ConvertTo-Json -InputObject @($updates) -Depth 3 -Compress";
            var json = await ShellRunner.CapturePowershellJson(script, timeout.Token);
            return FromNameVersionJson(json);
        }
        catch { return new List<PackageItem>(); }
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafePwsh($"Install-Module -Name {PkgParse.Ps(id)} -Force -Scope CurrentUser", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafePwsh($"Uninstall-Module -Name {PkgParse.Ps(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafePwsh($"Update-Module -Name {PkgParse.Ps(id)}", ct);

    private List<PackageItem> FromNameVersionJson(string json)
    {
        var res = new List<PackageItem>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in root.EnumerateArray()) AddOne(res, el);
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                AddOne(res, root); // 單一結果 ConvertTo-Json 會係一個 object · single result is an object.
            }
        }
        catch { }
        return res;
    }

    private void AddOne(List<PackageItem> res, JsonElement el)
    {
        var name = PkgParse.Str(el, "Name");
        if (name.Length == 0) return;
        res.Add(new PackageItem
        {
            Name = name,
            Id = name,
            Version = PkgParse.Str(el, "Version"),
            AvailableVersion = PkgParse.Str(el, "AvailableVersion"),
            Source = PkgParse.Str(el, "Repository"),
            ManagerKey = Key,
        });
    }

    private static async Task<TweakResult> SafePwsh(string script, CancellationToken ct)
    {
        try { return await ShellRunner.RunPowershell(script, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}

/// <summary>Cargo（Rust）管理器 · Cargo (Rust) package manager.</summary>
public sealed class CargoManager : IPackageManager
{
    public string Key => "cargo";
    public string NameEn => "Cargo (Rust)";
    public string NameZh => "Cargo（Rust）";
    public string Cli => "cargo";

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.Capture("cargo", "--version", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            // 行格式：name = "ver"    # desc · Lines: name = "ver"   # description.
            var o = await ShellRunner.Capture("cargo", $"search {PkgParse.Q(query)}", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0) continue;
                int eq = ln.IndexOf('=');
                if (eq <= 0) continue;
                var name = ln.Substring(0, eq).Trim();
                if (name.Length == 0 || name.Contains(' ')) continue;
                var rest = ln.Substring(eq + 1).Trim();
                var ver = "";
                int q1 = rest.IndexOf('"');
                int q2 = q1 >= 0 ? rest.IndexOf('"', q1 + 1) : -1;
                if (q1 >= 0 && q2 > q1) ver = rest.Substring(q1 + 1, q2 - q1 - 1);
                res.Add(new PackageItem { Name = name, Id = name, Version = ver, ManagerKey = Key });
            }
        }
        catch { }
        return res;
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            // 行格式："name vX.Y.Z:" 之後係縮排嘅 bin 名 · "name vX.Y.Z:" then indented bins.
            var o = await ShellRunner.Capture("cargo", "install --list", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                if (raw.Length == 0) continue;
                if (char.IsWhiteSpace(raw[0])) continue; // 縮排行係 bin · indented = binary name.
                var ln = raw.Trim().TrimEnd(':');
                if (ln.Length == 0) continue;
                var parts = ln.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1) continue;
                var name = parts[0];
                var ver = parts.Length > 1 ? parts[1].TrimStart('v') : "";
                res.Add(new PackageItem { Name = name, Id = name, Version = ver, ManagerKey = Key });
            }
        }
        catch { }
        return res;
    }

    /// <summary>
    /// Cargo 本身冇可靠 outdated；只會喺用戶已裝 cargo-update 時讀佢嘅清單，絕不靜默加裝 helper。
    /// Cargo has no reliable built-in outdated command. Use cargo-update only when already installed;
    /// never install the optional helper implicitly.
    /// </summary>
    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        var result = new List<PackageItem>();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var output = await ShellRunner.Capture("cargo", "install-update --list", timeout.Token);
            foreach (var raw in PkgParse.Lines(output))
            {
                var parts = raw.Trim().Split(
                    new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                var versions = parts
                    .Where(p => p.Length > 1 && (p[0] == 'v' || p[0] == 'V'))
                    .Select(p => p.TrimStart('v', 'V').TrimEnd(',', ':'))
                    .Where(p => SemverRangeService.TryParse(p, out _, out _))
                    .Take(2)
                    .ToArray();
                if (versions.Length < 2 || versions[0] == versions[1]) continue;

                var id = parts[0].TrimEnd(':');
                if (id.Length == 0 || id.Equals("Package", StringComparison.OrdinalIgnoreCase)) continue;
                result.Add(new PackageItem
                {
                    Name = id,
                    Id = id,
                    Version = versions[0],
                    AvailableVersion = versions[1],
                    Source = "crates.io",
                    ManagerKey = Key,
                });
            }
        }
        catch { return new List<PackageItem>(); }
        return result;
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafeRun($"cargo install {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafeRun($"cargo uninstall {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafeRun($"cargo install {PkgParse.Q(id)} --force", ct);

    private static async Task<TweakResult> SafeRun(string cmd, CancellationToken ct)
    {
        try { return await ShellRunner.RunCmd(cmd, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}

/// <summary>vcpkg（C/C++ 程式庫）· The vcpkg C/C++ library manager.</summary>
public sealed class VcpkgManager : IPackageManager
{
    public string Key => "vcpkg";
    public string NameEn => "vcpkg";
    public string NameZh => "vcpkg";
    public string Cli => "vcpkg";

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try { var o = await ShellRunner.Capture("vcpkg", "version", ct); return !string.IsNullOrWhiteSpace(o); }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var o = await ShellRunner.Capture("vcpkg", $"search {PkgParse.Q(query)}", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0 || ln.StartsWith("The result")) continue;
                var parts = ln.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1) continue;
                res.Add(new PackageItem
                {
                    Name = parts[0], Id = parts[0],
                    Version = parts.Length > 1 ? parts[1] : "",
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            var o = await ShellRunner.Capture("vcpkg", "list", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim();
                if (ln.Length == 0) continue;
                var parts = ln.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1) continue;
                var name = parts[0]; // name:triplet
                res.Add(new PackageItem
                {
                    Name = name, Id = name,
                    Version = parts.Length > 1 ? parts[1] : "",
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        var result = new List<PackageItem>();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            // `vcpkg update` is the non-mutating, built-in outdated check; `upgrade` performs changes.
            var output = await ShellRunner.Capture("vcpkg", "update", timeout.Token);
            foreach (var raw in PkgParse.Lines(output))
            {
                var line = raw.Trim();
                int arrow = line.IndexOf("->", StringComparison.Ordinal);
                if (arrow <= 0) continue;

                var left = line.Substring(0, arrow).Trim();
                var right = line.Substring(arrow + 2).Trim();
                var leftParts = left.Split(
                    new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var rightParts = right.Split(
                    new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (leftParts.Length < 2 || rightParts.Length < 1) continue;

                var id = leftParts[0];
                var current = leftParts[^1];
                var available = rightParts[0];
                if (!id.Contains(':') || current.Length == 0 || available.Length == 0
                    || current == available) continue;

                int tripletSeparator = id.LastIndexOf(':');
                result.Add(new PackageItem
                {
                    Name = tripletSeparator > 0 ? id.Substring(0, tripletSeparator) : id,
                    Id = id,
                    Version = current,
                    AvailableVersion = available,
                    Source = tripletSeparator >= 0 && tripletSeparator + 1 < id.Length
                        ? id.Substring(tripletSeparator + 1)
                        : "vcpkg",
                    ManagerKey = Key,
                });
            }
        }
        catch { return new List<PackageItem>(); }
        return result;
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafeRun($"vcpkg install {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafeRun($"vcpkg remove {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafeRun($"vcpkg upgrade {PkgParse.Q(id)} --no-dry-run", ct);

    private static async Task<TweakResult> SafeRun(string cmd, CancellationToken ct)
    {
        try { return await ShellRunner.RunCmd(cmd, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}

/// <summary>Bun（JavaScript 全域）· Bun global package manager (UniGetUI parity).</summary>
public sealed class BunManager : IPackageManager
{
    public string Key => "bun";
    public string NameEn => "Bun (global)";
    public string NameZh => "Bun（全域）";
    public string Cli => "bun";

    private static readonly HttpClient NpmRegistryHttp = CreateNpmRegistryClient();

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try { var o = await ShellRunner.Capture("bun", "--version", ct); return !string.IsNullOrWhiteSpace(o); }
        catch { return false; }
    }

    /// <summary>用 npm registry 搜尋（同 Bun 一樣食 npm 套件）· Search the npm registry (Bun installs npm packages).</summary>
    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        var res = new List<PackageItem>();
        if (string.IsNullOrWhiteSpace(query)) return res;
        try
        {
            // Bun consumes npm packages but does not require npm itself. Query the registry API directly.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var url = "https://registry.npmjs.org/-/v1/search?size=100&text="
                + Uri.EscapeDataString(query.Trim());
            using var response = await NpmRegistryHttp.GetAsync(url, timeout.Token);
            if (!response.IsSuccessStatusCode) return res;
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("objects", out var objects)
                && objects.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in objects.EnumerateArray())
                {
                    if (!entry.TryGetProperty("package", out var package)
                        || package.ValueKind != JsonValueKind.Object) continue;
                    var name = PkgParse.Str(package, "name");
                    if (name.Length == 0) continue;
                    res.Add(new PackageItem
                    {
                        Name = name,
                        Id = name,
                        Version = PkgParse.Str(package, "version"),
                        Source = "npmjs.org",
                        ManagerKey = Key,
                    });
                }
            }
        }
        catch { return new List<PackageItem>(); }
        return res;
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            // Bun's global flag is ignored by `pm ls`; run from its dedicated global package manifest.
            var globalDir = GetGlobalPackagesDirectory();
            if (!File.Exists(Path.Combine(globalDir, "package.json"))) return res;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var run = await ShellRunner.RunIn(globalDir, "bun", "pm ls", false, timeout.Token);
            var o = run.Output ?? "";
            foreach (var raw in PkgParse.Lines(o))
            {
                int tree = raw.IndexOf("──", StringComparison.Ordinal);
                if (tree < 0) continue;
                var entry = raw.Substring(tree + 2).Trim();
                int at = entry.LastIndexOf('@');
                string name, ver;
                if (at > 0) { name = entry.Substring(0, at).Trim(); ver = entry.Substring(at + 1).Trim(); }
                else { name = entry; ver = ""; }
                if (name.Length == 0 || name.Contains(' ')) continue;
                res.Add(new PackageItem
                {
                    Name = name,
                    Id = name,
                    Version = ver,
                    Source = "npmjs.org",
                    ManagerKey = Key,
                });
            }
        }
        catch { }
        return res;
    }

    /// <summary>
    /// Bun 嘅 outdated 只睇目前 project；喺專用全域 package.json 資料夾執行，先至真係查全域套件。
    /// Bun outdated is project-scoped, so run it from Bun's dedicated global package directory.
    /// </summary>
    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        var result = new List<PackageItem>();
        try
        {
            var globalDir = GetGlobalPackagesDirectory();
            if (!File.Exists(Path.Combine(globalDir, "package.json"))) return result;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var run = await ShellRunner.RunIn(globalDir, "bun", "outdated", false, timeout.Token);
            var output = run.Output ?? "";

            foreach (var raw in PkgParse.Lines(output))
            {
                var trimmed = raw.TrimStart();
                if (!trimmed.StartsWith('│') && !trimmed.StartsWith('|')) continue;
                var parts = raw.Split(new[] { '│', '|' }, StringSplitOptions.None);
                if (parts.Length < 5) continue;

                var id = parts[1].Trim();
                var current = parts[2].Trim();
                // UpdateAsync explicitly installs @latest, so report the Latest column when present.
                var available = parts.Length > 4 ? parts[4].Trim() : parts[3].Trim();
                if (id.Length == 0 || id.Equals("Package", StringComparison.OrdinalIgnoreCase)
                    || current.Length == 0 || available.Length == 0 || current == available
                    || id.All(c => c is '-' or '─' or '┬' or '┼' or '┴' or '├' or '┤' or '┌' or '└' or '┘' or '┐'))
                    continue;

                result.Add(new PackageItem
                {
                    Name = id,
                    Id = id,
                    Version = current,
                    AvailableVersion = available,
                    Source = "npmjs.org",
                    ManagerKey = Key,
                });
            }
        }
        catch { return new List<PackageItem>(); }
        return result;
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafeRun($"bun add -g {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafeRun($"bun remove -g {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafeRun($"bun add -g {PkgParse.Q(id)}@latest", ct);

    private static async Task<TweakResult> SafeRun(string cmd, CancellationToken ct)
    {
        try { return await ShellRunner.RunCmd(cmd, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    private static HttpClient CreateNpmRegistryClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge/1.0");
        return client;
    }

    private static string GetGlobalPackagesDirectory()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".bun", "install", "global");
}

/// <summary>PowerShell 7（pwsh）資源庫管理器 · PowerShell 7 (pwsh) PSResource manager (UniGetUI parity).</summary>
public sealed class PowerShell7Manager : IPackageManager
{
    public string Key => "pwsh7";
    public string NameEn => "PowerShell 7 (PSResource)";
    public string NameZh => "PowerShell 7（PSResource）";
    public string Cli => "pwsh";

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try { var o = await ShellRunner.Capture("pwsh", "-NoProfile -Command \"$PSVersionTable.PSVersion.Major\"", ct); return !string.IsNullOrWhiteSpace(o); }
        catch { return false; }
    }

    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        try
        {
            var q = PkgParse.Ps("*" + query.Trim() + "*");
            var json = await CapturePwshJson($"Find-PSResource -Name {q} -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json", ct);
            return FromNameVersionJson(json);
        }
        catch { return new List<PackageItem>(); }
    }

    public async Task<List<PackageItem>> ListInstalledAsync(CancellationToken ct)
    {
        try
        {
            var json = await CapturePwshJson("Get-InstalledPSResource -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json", ct);
            return FromNameVersionJson(json);
        }
        catch { return new List<PackageItem>(); }
    }

    public async Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            const string script =
                "$ErrorActionPreference = 'SilentlyContinue'; " +
                "$updates = @(Get-InstalledPSResource -ErrorAction SilentlyContinue | Group-Object Name | ForEach-Object { " +
                "$installed = $_.Group | Sort-Object Version -Descending | Select-Object -First 1; " +
                "$available = Find-PSResource -Name $installed.Name -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; " +
                "if ($null -ne $available -and $available.Version -gt $installed.Version) { " +
                "[pscustomobject]@{ Name = $installed.Name; Version = $installed.Version.ToString(); " +
                "AvailableVersion = $available.Version.ToString(); Repository = $available.Repository } } }); " +
                "ConvertTo-Json -InputObject @($updates) -Depth 3 -Compress";
            var json = await CapturePwshJson(script, timeout.Token);
            return FromNameVersionJson(json);
        }
        catch { return new List<PackageItem>(); }
    }

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafePwsh($"Install-PSResource -Name {PkgParse.Ps(id)} -TrustRepository -Scope CurrentUser", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafePwsh($"Uninstall-PSResource -Name {PkgParse.Ps(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafePwsh($"Update-PSResource -Name {PkgParse.Ps(id)} -TrustRepository", ct);

    private static async Task<string> CapturePwshJson(string script, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes(script);
        var encoded = Convert.ToBase64String(bytes);
        var r = await ShellRunner.Run("pwsh", $"-NoProfile -NonInteractive -EncodedCommand {encoded}", false, ct);
        var raw = (r.Output ?? "").Trim().TrimStart('﻿');
        if (string.IsNullOrEmpty(raw)) return "[]";
        int a = raw.IndexOf('['), b = raw.LastIndexOf(']');
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        int c = raw.IndexOf('{'), d = raw.LastIndexOf('}');
        if (c >= 0 && d > c) return raw.Substring(c, d - c + 1);
        return "[]";
    }

    private static async Task<TweakResult> SafePwsh(string script, CancellationToken ct)
    {
        try
        {
            var bytes = System.Text.Encoding.Unicode.GetBytes(script);
            var encoded = Convert.ToBase64String(bytes);
            return await ShellRunner.Run("pwsh", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", false, ct);
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    private List<PackageItem> FromNameVersionJson(string json)
    {
        var res = new List<PackageItem>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
                foreach (var el in root.EnumerateArray()) AddOne(res, el);
            else if (root.ValueKind == JsonValueKind.Object)
                AddOne(res, root);
        }
        catch { }
        return res;
    }

    private void AddOne(List<PackageItem> res, JsonElement el)
    {
        var name = PkgParse.Str(el, "Name");
        if (name.Length == 0) return;
        res.Add(new PackageItem
        {
            Name = name,
            Id = name,
            Version = PkgParse.Str(el, "Version"),
            AvailableVersion = PkgParse.Str(el, "AvailableVersion"),
            Source = PkgParse.Str(el, "Repository"),
            ManagerKey = Key,
        });
    }
}

/// <summary>
/// 套件管理器登記處 · Registry of all package managers + cross-manager helpers.
/// 一處集齊所有引擎，畀 UI 一鍵跨管理器搜尋／更新。
/// One place that holds every engine, so the UI can search/update across managers at once.
/// </summary>
public static class PackageManagerRegistry
{
    /// <summary>全部管理器（固定次序）· All managers in a fixed display order.</summary>
    public static readonly IReadOnlyList<IPackageManager> All = new IPackageManager[]
    {
        new WingetManager(),
        new ScoopManager(),
        new ChocoManager(),
        new PipManager(),
        new NpmManager(),
        new DotnetToolManager(),
        new PsGalleryManager(),
        new PowerShell7Manager(),
        new CargoManager(),
        new BunManager(),
        new VcpkgManager(),
    };

    /// <summary>按鍵值搵管理器，搵唔到回 null · Look up a manager by key, null if none.</summary>
    public static IPackageManager? ByKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var m in All)
            if (string.Equals(m.Key, key, StringComparison.OrdinalIgnoreCase)) return m;
        return null;
    }

    /// <summary>
    /// 跨管理器並行搜尋並合併 · Search across the chosen (default all available) managers concurrently and concat.
    /// 跳過唔可用嘅引擎，每個引擎自己嘅錯誤都會吞咗。
    /// Skips unavailable engines and swallows per-manager failures.
    /// </summary>
    public static async Task<List<PackageItem>> SearchAllAsync(
        string query, IEnumerable<string>? managerKeys, CancellationToken ct)
        => await RunAcrossAsync(managerKeys, (m, c) => m.SearchAsync(query, c), ct);

    /// <summary>跨管理器收集所有更新並合併 · Collect updates across managers and concat.</summary>
    public static async Task<List<PackageItem>> AllUpdatesAsync(
        IEnumerable<string>? managerKeys, CancellationToken ct)
        => await RunAcrossAsync(managerKeys, (m, c) => m.ListUpdatesAsync(c), ct);

    /// <summary>跨管理器收集所有已安裝並合併 · Collect installed packages across managers and concat.</summary>
    public static async Task<List<PackageItem>> AllInstalledAsync(
        IEnumerable<string>? managerKeys, CancellationToken ct)
        => await RunAcrossAsync(managerKeys, (m, c) => m.ListInstalledAsync(c), ct);

    /// <summary>選出要用嘅管理器（預設全部）· Resolve the chosen managers (default = all).</summary>
    private static List<IPackageManager> Select(IEnumerable<string>? managerKeys)
    {
        if (managerKeys is null) return All.ToList();
        var wanted = new HashSet<string>(managerKeys, StringComparer.OrdinalIgnoreCase);
        var list = All.Where(m => wanted.Contains(m.Key)).ToList();
        return list.Count > 0 ? list : All.ToList();
    }

    /// <summary>
    /// 共用核心：只揀可用嘅、並行執行、合併、吞錯。
    /// Shared core: pick available managers, run concurrently, concat, swallow failures.
    /// </summary>
    private static async Task<List<PackageItem>> RunAcrossAsync(
        IEnumerable<string>? managerKeys,
        Func<IPackageManager, CancellationToken, Task<List<PackageItem>>> op,
        CancellationToken ct)
    {
        var result = new List<PackageItem>();
        try
        {
            var managers = Select(managerKeys);

            async Task<List<PackageItem>> SafeOne(IPackageManager m)
            {
                try
                {
                    if (!await m.IsAvailableAsync(ct)) return new List<PackageItem>();
                    return await op(m, ct) ?? new List<PackageItem>();
                }
                catch { return new List<PackageItem>(); }
            }

            var tasks = managers.Select(SafeOne).ToArray();
            var batches = await Task.WhenAll(tasks);
            foreach (var b in batches)
                if (b is { Count: > 0 }) result.AddRange(b);
        }
        catch { /* swallow — defensive */ }
        return result;
    }

    // ===== package details / sources / advanced install (UniGetUI parity) =====

    /// <summary>顯示一個套件嘅詳情 · Show full details for one package (description, version, homepage…).</summary>
    public static async Task<string> DetailsAsync(PackageItem item, CancellationToken ct = default)
    {
        try
        {
            var id = item.Id;
            if (!PackageOperationCoordinator.IsSafePackageId(id))
                return Loc.I.Pick("The package ID contains unsafe characters.", "套件 ID 含有唔安全字元。");
            return item.ManagerKey switch
            {
                "winget" => await ShellRunner.CapturePowershell($"winget show --id \"{id}\" -e --accept-source-agreements --disable-interactivity | Out-String -Width 200", ct),
                "scoop" => await ShellRunner.Capture("scoop", $"info \"{id}\"", ct),
                "choco" => await ShellRunner.Capture("choco", $"info \"{id}\"", ct),
                "pip" => await ShellRunner.Capture("pip", $"show \"{id}\"", ct),
                "npm" => await ShellRunner.Capture("npm", $"view \"{id}\"", ct),
                "dotnet" => await ShellRunner.Capture("dotnet", $"tool search \"{id}\" --detail", ct),
                "cargo" => await ShellRunner.Capture("cargo", $"search \"{id}\"", ct),
                "bun" => await ShellRunner.Capture("npm", $"view \"{id}\"", ct),
                "vcpkg" => await ShellRunner.Capture("vcpkg", $"search \"{id}\"", ct),
                "psgallery" => await ShellRunner.CapturePowershell($"Find-Module -Name \"{id}\" | Format-List Name,Version,Author,ProjectUri,Description | Out-String -Width 200", ct),
                "pwsh7" => await ShellRunner.Capture("pwsh", $"-NoProfile -Command \"Find-PSResource -Name '{id}' | Format-List Name,Version,Author,Repository,Description | Out-String -Width 200\"", ct),
                _ => "",
            };
        }
        catch (Exception ex) { return ex.Message; }
    }

    /// <summary>列出一個管理器嘅來源／bucket／feed · List a manager's sources / buckets / feeds.</summary>
    public static async Task<string> SourcesAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return key switch
            {
                "winget" => await ShellRunner.Capture("winget", "source list", ct),
                "scoop" => await ShellRunner.Capture("scoop", "bucket list", ct),
                "choco" => await ShellRunner.Capture("choco", "source list", ct),
                "pip" => await ShellRunner.Capture("pip", "config list", ct),
                "npm" => await ShellRunner.Capture("npm", "config get registry", ct),
                "dotnet" => await ShellRunner.Capture("dotnet", "nuget list source", ct),
                "psgallery" => await ShellRunner.CapturePowershell("Get-PSRepository | Format-Table Name,InstallationPolicy,SourceLocation | Out-String -Width 200", ct),
                "pwsh7" => await ShellRunner.Capture("pwsh", "-NoProfile -Command \"Get-PSResourceRepository | Format-Table Name,Trusted,Uri | Out-String -Width 200\"", ct),
                "cargo" => "crates.io (default registry)",
                "bun" => "registry.npmjs.org (default registry)",
                "vcpkg" => await ShellRunner.Capture("vcpkg", "x-update-baseline --dry-run", ct),
                _ => "",
            };
        }
        catch (Exception ex) { return ex.Message; }
    }

    /// <summary>
    /// 進階安裝（版本／範圍／架構／互動／自訂參數）· Advanced install with version / scope / architecture /
    /// interactive / custom args. winget supports them all; other managers map what they can.
    /// </summary>
    public static async Task<TweakResult> InstallAdvancedAsync(string key, string id, string? version,
        string? scope, string? arch, bool interactive, string? customArgs, CancellationToken ct = default)
    {
        try
        {
            // Keep this compatibility entry point, but route it through the same global coordinator as
            // every row, batch, bundle and scheduled operation. This preserves validation, cancellation,
            // history, bounded concurrency and the caller's explicit advanced options.
            var options = InstallOptions.Load(key, id).Clone();
            if (!string.IsNullOrWhiteSpace(version)) options.Version = version.Trim();
            if (!string.IsNullOrWhiteSpace(scope)) options.Scope = scope.Trim();
            if (!string.IsNullOrWhiteSpace(arch)) options.Architecture = arch.Trim();
            options.Interactive = interactive;
            if (!string.IsNullOrWhiteSpace(customArgs)) options.CustomArgsInstall = customArgs.Trim();

            var snapshot = await PackageOperationCoordinator.RunAsync(new PackageItem
            {
                ManagerKey = key ?? "",
                Id = id ?? "",
                Name = id ?? "",
            }, PackageOperations.Op.Install, options, ct);

            return snapshot.Result ?? TweakResult.Fail("The package operation did not return a result.", "套件操作冇回傳結果。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>
    /// 一個管理器嘅「全部更新」· "Update all" for ONE manager: enumerate its updates and update each.
    /// 回傳 (成功數, 總數) · Returns (succeeded, total).
    /// </summary>
    public static async Task<(int done, int total)> UpdateAllForManagerAsync(
        string key, IProgress<PackageItem>? progress = null, CancellationToken ct = default)
    {
        var m = ByKey(key);
        if (m is null) return (0, 0);
        List<PackageItem> ups;
        try { ups = await m.ListUpdatesAsync(ct); } catch { ups = new(); }
        foreach (var item in ups) progress?.Report(item);
        var results = await PackageOperationCoordinator.RunManyAsync(
            ups, PackageOperations.Op.Update, ct: ct);
        return (results.Count(r => r.Status == PackageOperationStatus.Succeeded), results.Count);
    }

}
