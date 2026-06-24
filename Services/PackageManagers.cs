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
                $"winget search --query \"{PkgParse.Q(query)}\" --accept-source-agreements --disable-interactivity | Out-String -Width 400", ct);
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
            var o = await ShellRunner.CapturePowershell($"scoop search {PkgParse.Q(query)} | Out-String -Width 400", ct);
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

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            var o = await ShellRunner.Capture("pip", "--version", ct);
            return !string.IsNullOrWhiteSpace(o);
        }
        catch { return false; }
    }

    /// <summary>現代 pip 唔再支援 search · Modern pip no longer supports search — return empty.</summary>
    public Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
        => Task.FromResult(new List<PackageItem>());

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

    /// <summary>dotnet 冇內建 outdated，盡力而為 -> 回空 · No built-in outdated; best-effort empty.</summary>
    public Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
        => Task.FromResult(new List<PackageItem>());

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
            var q = PkgParse.Q(query);
            var json = await ShellRunner.CapturePowershellJson(
                $"Find-Module -Name *{q}* -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json", ct);
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

    /// <summary>盡力而為，回空 · Best-effort empty (no cheap built-in outdated).</summary>
    public Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
        => Task.FromResult(new List<PackageItem>());

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafePwsh($"Install-Module -Name {PkgParse.Q(id)} -Force -Scope CurrentUser", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafePwsh($"Uninstall-Module -Name {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafePwsh($"Update-Module -Name {PkgParse.Q(id)}", ct);

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

    /// <summary>cargo 冇內建 outdated -> 回空 · No built-in outdated; return empty.</summary>
    public Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
        => Task.FromResult(new List<PackageItem>());

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

    public Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
        => Task.FromResult(new List<PackageItem>());

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

    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try { var o = await ShellRunner.Capture("bun", "--version", ct); return !string.IsNullOrWhiteSpace(o); }
        catch { return false; }
    }

    /// <summary>用 npm registry 搜尋（同 Bun 一樣食 npm 套件）· Search the npm registry (Bun installs npm packages).</summary>
    public async Task<List<PackageItem>> SearchAsync(string query, CancellationToken ct)
    {
        var res = new List<PackageItem>();
        try
        {
            // bun 冇 search，借 npm 嘅 registry 結果 · Bun has no search; reuse npm's registry results.
            var json = await ShellRunner.Capture("npm", $"search {PkgParse.Q(query)} --json", ct);
            json = NpmJson(json, '[', ']');
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var name = PkgParse.Str(el, "name");
                    if (name.Length == 0) continue;
                    res.Add(new PackageItem { Name = name, Id = name, Version = PkgParse.Str(el, "version"), Source = "npmjs.org", ManagerKey = Key });
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
            // "bun pm ls -g" 列出全域套件 · "bun pm ls -g" lists global packages ("name@version" lines).
            var o = await ShellRunner.Capture("bun", "pm ls -g", ct);
            foreach (var raw in PkgParse.Lines(o))
            {
                var ln = raw.Trim().TrimStart('├', '└', '─', '│', ' ');
                if (ln.Length == 0 || ln.StartsWith("/") || ln.Contains("node_modules")) continue;
                int at = ln.LastIndexOf('@');
                string name, ver;
                if (at > 0) { name = ln.Substring(0, at).Trim(); ver = ln.Substring(at + 1).Trim(); }
                else { name = ln; ver = ""; }
                if (name.Length == 0 || name.Contains(' ')) continue;
                res.Add(new PackageItem { Name = name, Id = name, Version = ver, ManagerKey = Key });
            }
        }
        catch { }
        return res;
    }

    /// <summary>Bun 冇方便嘅全域 outdated -> 回空 · No convenient global outdated; return empty.</summary>
    public Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
        => Task.FromResult(new List<PackageItem>());

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

    private static string NpmJson(string raw, char open, char close)
    {
        if (string.IsNullOrWhiteSpace(raw)) return open == '[' ? "[]" : "{}";
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf(open), b = raw.LastIndexOf(close);
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        return open == '[' ? "[]" : "{}";
    }
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
            var q = PkgParse.Q(query);
            var json = await CapturePwshJson($"Find-PSResource -Name *{q}* -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json", ct);
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

    public Task<List<PackageItem>> ListUpdatesAsync(CancellationToken ct)
        => Task.FromResult(new List<PackageItem>());

    public Task<TweakResult> InstallAsync(string id, CancellationToken ct)
        => SafePwsh($"Install-PSResource -Name {PkgParse.Q(id)} -TrustRepository -Scope CurrentUser", ct);

    public Task<TweakResult> UninstallAsync(string id, CancellationToken ct)
        => SafePwsh($"Uninstall-PSResource -Name {PkgParse.Q(id)}", ct);

    public Task<TweakResult> UpdateAsync(string id, CancellationToken ct)
        => SafePwsh($"Update-PSResource -Name {PkgParse.Q(id)} -TrustRepository", ct);

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
        res.Add(new PackageItem { Name = name, Id = name, Version = PkgParse.Str(el, "Version"), ManagerKey = Key });
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
        string q = $"\"{id}\"";
        string extra = string.IsNullOrWhiteSpace(customArgs) ? "" : " " + customArgs.Trim();
        try
        {
            switch (key)
            {
                case "winget":
                {
                    var cmd = $"winget install --id {q} -e --accept-source-agreements --accept-package-agreements";
                    if (!string.IsNullOrWhiteSpace(version)) cmd += $" --version {version}";
                    if (!string.IsNullOrWhiteSpace(scope)) cmd += $" --scope {scope}";
                    if (!string.IsNullOrWhiteSpace(arch)) cmd += $" --architecture {arch}";
                    cmd += interactive ? " --interactive" : " --silent --disable-interactivity";
                    return await ShellRunner.RunCmd(cmd + extra, false, ct);
                }
                case "scoop":
                    return await ShellRunner.RunCmd($"scoop install {id}{(string.IsNullOrWhiteSpace(version) ? "" : "@" + version)}{extra}", false, ct);
                case "choco":
                {
                    var cmd = $"choco install {id} -y";
                    if (!string.IsNullOrWhiteSpace(version)) cmd += $" --version {version}";
                    if (interactive) cmd += " --notsilent";
                    return await ShellRunner.RunCmd(cmd + extra, false, ct);
                }
                case "pip":
                    return await ShellRunner.RunCmd($"pip install {id}{(string.IsNullOrWhiteSpace(version) ? "" : "==" + version)}{extra}", false, ct);
                case "npm":
                    return await ShellRunner.RunCmd($"npm install -g {id}{(string.IsNullOrWhiteSpace(version) ? "" : "@" + version)}{extra}", false, ct);
                case "dotnet":
                {
                    var cmd = $"dotnet tool install -g {id}";
                    if (!string.IsNullOrWhiteSpace(version)) cmd += $" --version {version}";
                    return await ShellRunner.RunCmd(cmd + extra, false, ct);
                }
                case "cargo":
                {
                    var cmd = $"cargo install {id}";
                    if (!string.IsNullOrWhiteSpace(version)) cmd += $" --version {version}";
                    return await ShellRunner.RunCmd(cmd + extra, false, ct);
                }
                case "vcpkg":
                    return await ShellRunner.RunCmd($"vcpkg install {id}{extra}", false, ct);
                case "bun":
                    return await ShellRunner.RunCmd($"bun add -g {id}{(string.IsNullOrWhiteSpace(version) ? "" : "@" + version)}{extra}", false, ct);
                case "psgallery":
                {
                    var ps = $"Install-Module -Name {q} -Force -Scope CurrentUser";
                    if (!string.IsNullOrWhiteSpace(version)) ps += $" -RequiredVersion {version}";
                    return await ShellRunner.RunPowershell(ps, false, ct);
                }
                case "pwsh7":
                {
                    var ps = $"Install-PSResource -Name {q} -TrustRepository -Scope CurrentUser";
                    if (!string.IsNullOrWhiteSpace(version)) ps += $" -Version {version}";
                    var bytes = System.Text.Encoding.Unicode.GetBytes(ps);
                    var enc = Convert.ToBase64String(bytes);
                    return await ShellRunner.Run("pwsh", $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {enc}", false, ct);
                }
                default:
                {
                    var m = ByKey(key);
                    return m is not null ? await m.InstallAsync(id, ct)
                                         : TweakResult.Fail("Unknown manager.", "未知管理器。");
                }
            }
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
        int done = 0;
        foreach (var item in ups)
        {
            progress?.Report(item);
            try { var r = await m.UpdateAsync(item.Id, ct); if (r.Success) done++; } catch { }
        }
        return (done, ups.Count);
    }

    /// <summary>
    /// 啟動 UniGetUI（裝咗就開，否則經 winget 裝）· Launch UniGetUI for power features; install it via winget first
    /// if it is not present. Provides the "fallback to UniGetUI" escape hatch the native wrapper can't cover.
    /// </summary>
    public static async Task<TweakResult> LaunchUniGetUIAsync(CancellationToken ct = default)
    {
        try
        {
            // 嘗試直接啟動已安裝嘅 UniGetUI · Try to start an already-installed UniGetUI.
            var start = await ShellRunner.RunCmd("start \"\" \"UniGetUI\"", false, ct);
            if (start.Success) return TweakResult.Ok("Launched UniGetUI.", "已啟動 UniGetUI。");

            // 否則經 winget 安裝（兩個常見 id）· Otherwise install via winget (two known ids).
            var r = await ShellRunner.RunCmd(
                "winget install --id MartiCliment.UniGetUI -e --accept-source-agreements --accept-package-agreements --silent --disable-interactivity", false, ct);
            if (!r.Success)
                r = await ShellRunner.RunCmd(
                    "winget install --id SomePythonThings.WingetUIStore -e --accept-source-agreements --accept-package-agreements --silent --disable-interactivity", false, ct);
            PackageService.RefreshProcessPath();
            return r.Success
                ? TweakResult.Ok("UniGetUI installed — relaunch it from Start.", "已安裝 UniGetUI — 由開始功能表啟動。")
                : TweakResult.Fail("Could not install UniGetUI.", "無法安裝 UniGetUI。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}
