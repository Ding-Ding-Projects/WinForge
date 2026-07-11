using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 套件來源管理（UniGetUI 式）· Package SOURCE management (UniGetUI parity):
/// 加 / 移除 / 重新整理 winget 來源、scoop bucket、choco 來源、dotnet nuget feed、
/// PSGallery／PSResource 資源庫，連帶每個來源嘅名、URL、套件數同最後更新日期。
/// Add / remove / refresh winget sources, scoop buckets, choco sources, dotnet nuget feeds and
/// PSGallery/PSResource repositories, with per-source name, URL, package count and last-updated date.
/// 永遠唔擲例外：解析失敗回空 list，動作失敗回 <see cref="TweakResult.Fail"/>。
/// Never throws: parsing failure returns an empty list; an action failure returns Fail.
/// </summary>
public static class SourceManager
{
    /// <summary>
    /// 一個來源嘅結構化資料 · One structured source row.
    /// PackageCount / UpdatedDate 喺管理器唔提供時為 null · null where the manager exposes no such metadata.
    /// </summary>
    public sealed record SourceInfo(string Manager, string Name, string Url, string? PackageCount, string? UpdatedDate);

    /// <summary>
    /// 一個精選嘅已知來源（畀「加來源」對話框做下拉選單）· A curated known source for the Add dialog dropdown.
    /// </summary>
    public sealed record KnownSource(string Name, string Url);

    /// <summary>Validate a source name before it reaches cmd/PowerShell. Reject control and shell syntax
    /// instead of silently stripping it, because imported or pasted values are an execution boundary.</summary>
    private static string SafeName(string value)
    {
        var s = (value ?? "").Trim();
        if (s.Length is 0 or > 128) return "";
        foreach (var c in s)
            if (!(char.IsLetterOrDigit(c) || c is ' ' or '.' or '_' or '-')) return "";
        return s;
    }

    /// <summary>Accept only credential-free HTTP(S) source URLs.</summary>
    private static string SafeUrl(string value)
    {
        var s = (value ?? "").Trim();
        if (s.Length is 0 or > 2048) return "";
        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.UserInfo)) return "";
        return uri.AbsoluteUri;
    }

    private static string PsLiteral(string value) => "'" + (value ?? "").Replace("'", "''") + "'";

    /// <summary>切成一行行 · Split text into trimmed-of-CR lines.</summary>
    private static string[] Lines(string s)
        => string.IsNullOrEmpty(s) ? Array.Empty<string>() : s.Replace("\r", "").Split('\n');

    // ===== capabilities =====

    /// <summary>呢個管理器嘅來源變更要唔要管理員 · Do this manager's source mutations require admin (winget, choco).</summary>
    public static bool RequiresAdmin(string managerKey)
        => managerKey is "winget" or "choco";

    /// <summary>呢個管理器支唔支援加／移除來源 · Can this manager add/remove sources.</summary>
    public static bool CanAddRemove(string managerKey)
        => managerKey is "winget" or "scoop" or "choco" or "dotnet" or "psgallery" or "pwsh7";

    /// <summary>呢個管理器支唔支援重新整理來源索引 · Does refresh do anything meaningful (winget, scoop).</summary>
    public static bool CanRefresh(string managerKey)
        => managerKey is "winget" or "scoop";

    // ===== curated known sources (for the Add dialog dropdown) =====

    /// <summary>
    /// 每個管理器嘅精選已知來源 · Curated known sources per manager, surfaced as a dropdown in the Add dialog.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<KnownSource>> KnownSources =
        new Dictionary<string, IReadOnlyList<KnownSource>>(StringComparer.OrdinalIgnoreCase)
        {
            ["winget"] = new[]
            {
                new KnownSource("winget", "https://cdn.winget.microsoft.com/cache"),
                new KnownSource("msstore", "https://storeedgefd.dsx.mp.microsoft.com/v9.0"),
            },
            ["scoop"] = new[]
            {
                new KnownSource("main", "https://github.com/ScoopInstaller/Main"),
                new KnownSource("extras", "https://github.com/ScoopInstaller/Extras"),
                new KnownSource("versions", "https://github.com/ScoopInstaller/Versions"),
                new KnownSource("nerd-fonts", "https://github.com/matthewjberger/scoop-nerd-fonts"),
                new KnownSource("java", "https://github.com/ScoopInstaller/Java"),
                new KnownSource("nonportable", "https://github.com/ScoopInstaller/Nonportable"),
                new KnownSource("games", "https://github.com/Calinou/scoop-games"),
                new KnownSource("sysinternals", "https://github.com/niheaven/scoop-sysinternals"),
            },
            ["choco"] = new[]
            {
                new KnownSource("chocolatey", "https://community.chocolatey.org/api/v2/"),
            },
            ["dotnet"] = new[]
            {
                new KnownSource("nuget.org", "https://api.nuget.org/v3/index.json"),
            },
            ["psgallery"] = new[]
            {
                new KnownSource("PSGallery", "https://www.powershellgallery.com/api/v2"),
                new KnownSource("NuGet", "https://api.nuget.org/v3/index.json"),
            },
            ["pwsh7"] = new[]
            {
                new KnownSource("PSGallery", "https://www.powershellgallery.com/api/v2"),
                new KnownSource("NuGetGallery", "https://api.nuget.org/v3/index.json"),
            },
        };

    /// <summary>攞精選已知來源（搵唔到回空）· Known sources for a manager (empty if none).</summary>
    public static IReadOnlyList<KnownSource> KnownSourcesFor(string managerKey)
        => KnownSources.TryGetValue(managerKey ?? "", out var v) ? v : Array.Empty<KnownSource>();

    // ===== list =====

    /// <summary>
    /// 列出一個管理器嘅結構化來源 · List a manager's structured sources.
    /// 解析穩陣，永遠唔擲例外，失敗回空 list。 Defensive parsing; never throws; empty list on failure.
    /// </summary>
    public static async Task<List<SourceInfo>> ListAsync(string managerKey, CancellationToken ct)
    {
        try
        {
            return managerKey switch
            {
                "winget" => await ListWingetAsync(ct),
                "scoop" => await ListScoopAsync(ct),
                "choco" => await ListChocoAsync(ct),
                "dotnet" => await ListDotnetAsync(ct),
                "psgallery" => await ListPsGalleryAsync(ct),
                "pwsh7" => await ListPwsh7Async(ct),
                "pip" => Synthetic(managerKey, "PyPI", "https://pypi.org/simple"),
                "npm" => await ListNpmAsync(ct),
                "cargo" => Synthetic(managerKey, "crates.io", "https://crates.io"),
                "bun" => Synthetic(managerKey, "npmjs.org", "https://registry.npmjs.org"),
                "vcpkg" => Synthetic(managerKey, "vcpkg registry", "https://github.com/microsoft/vcpkg"),
                _ => new List<SourceInfo>(),
            };
        }
        catch { return new List<SourceInfo>(); }
    }

    /// <summary>單一唯讀合成來源（描述預設登記處）· A single read-only synthetic row describing the default registry.</summary>
    private static List<SourceInfo> Synthetic(string manager, string name, string url)
        => new() { new SourceInfo(manager, name, url, null, null) };

    /// <summary>winget：解析 `winget source list` 嘅 Name／Argument 欄 · Parse Name/Argument columns.</summary>
    private static async Task<List<SourceInfo>> ListWingetAsync(CancellationToken ct)
    {
        var res = new List<SourceInfo>();
        // 用 Out-String -Width 確保長 URL 唔被截斷 · widen so long URLs aren't truncated.
        var outp = await ShellRunner.CapturePowershell(
            "winget source list --disable-interactivity | Out-String -Width 400", ct);
        var lines = Lines(outp);

        int hdr = -1;
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains("Name", StringComparison.Ordinal) && lines[i].Contains("Argument", StringComparison.Ordinal))
            { hdr = i; break; }
        if (hdr < 0) return res;

        var h = lines[hdr];
        int nameCol = h.IndexOf("Name", StringComparison.Ordinal);
        int argCol = h.IndexOf("Argument", StringComparison.Ordinal);
        if (nameCol < 0 || argCol < 0) return res;

        for (int i = hdr + 1; i < lines.Length; i++)
        {
            var ln = lines[i];
            if (ln.Trim().Length == 0 || ln.TrimStart().StartsWith("---")) continue;
            var name = Cut(ln, nameCol, argCol);
            var url = argCol < ln.Length ? ln.Substring(Math.Min(argCol, ln.Length)).Trim() : "";
            if (name.Length == 0) continue;
            res.Add(new SourceInfo("winget", name, url, null, null));
        }
        return res;
    }

    /// <summary>scoop：`scoop bucket list` 嘅 Name／Source／Updated／Manifests · map Manifests->count, Updated->date.</summary>
    private static async Task<List<SourceInfo>> ListScoopAsync(CancellationToken ct)
    {
        var res = new List<SourceInfo>();
        var outp = await ShellRunner.CapturePowershell("scoop bucket list | Out-String -Width 400", ct);
        var lines = Lines(outp);

        bool past = false;
        foreach (var raw in lines)
        {
            if (!past)
            {
                if (raw.Contains("---")) past = true;
                continue;
            }
            var ln = raw.Trim();
            if (ln.Length == 0) continue;
            // 欄：Name  Source(URL)  Updated(date time)  Manifests · split on 2+ spaces, fall back to whitespace.
            var cols = SplitCols(raw);
            if (cols.Length == 0) continue;
            string name = cols[0];
            string url = cols.Length > 1 ? cols[1] : "";
            // Updated 可能係 "yyyy-MM-dd HH:mm:ss"（含空格），Manifests 係最後一個純數字。
            // Updated may contain a space; Manifests is the trailing integer.
            string? count = null;
            string? updated = null;
            if (cols.Length >= 4)
            {
                updated = string.Join(' ', cols.Skip(2).Take(cols.Length - 3));
                var last = cols[^1].Trim();
                count = last.All(char.IsDigit) && last.Length > 0 ? last : null;
            }
            else if (cols.Length == 3)
            {
                var last = cols[2].Trim();
                if (last.All(char.IsDigit) && last.Length > 0) count = last; else updated = last;
            }
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) url = url[..^4];
            res.Add(new SourceInfo("scoop", name, url, count, updated));
        }
        return res;
    }

    /// <summary>choco：`choco source list -r`（"name|url|..."）· parse pipe-delimited rows.</summary>
    private static async Task<List<SourceInfo>> ListChocoAsync(CancellationToken ct)
    {
        var res = new List<SourceInfo>();
        var outp = await ShellRunner.Capture("choco", "source list -r", ct);
        foreach (var raw in Lines(outp))
        {
            var ln = raw.Trim();
            if (ln.Length == 0 || !ln.Contains('|')) continue;
            var parts = ln.Split('|');
            if (parts.Length < 2 || parts[0].Length == 0) continue;
            res.Add(new SourceInfo("choco", parts[0].Trim(), parts[1].Trim(), null, null));
        }
        return res;
    }

    /// <summary>dotnet：`dotnet nuget list source` 嘅編號 'Name [Enabled] / URL' 對 · numbered pairs.</summary>
    private static async Task<List<SourceInfo>> ListDotnetAsync(CancellationToken ct)
    {
        var res = new List<SourceInfo>();
        var outp = await ShellRunner.Capture("dotnet", "nuget list source", ct);
        var lines = Lines(outp);
        string? pendingName = null;
        foreach (var raw in lines)
        {
            var ln = raw.Trim();
            if (ln.Length == 0) continue;
            // 名行格式："1.  nuget.org [Enabled]" · name line: "<n>.  <name> [Enabled|Disabled]".
            int dot = ln.IndexOf('.');
            bool isNameLine = dot > 0 && dot <= 3 && int.TryParse(ln[..dot], out _);
            if (isNameLine)
            {
                var rest = ln[(dot + 1)..].Trim();
                int br = rest.IndexOf('[');
                pendingName = (br >= 0 ? rest[..br] : rest).Trim();
            }
            else if (pendingName is not null &&
                     (ln.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                      || ln.Contains("://") || ln.Contains(":\\") || ln.StartsWith("\\\\")))
            {
                res.Add(new SourceInfo("dotnet", pendingName, ln, null, null));
                pendingName = null;
            }
        }
        return res;
    }

    /// <summary>psgallery：`Get-PSRepository` 嘅 Name／SourceLocation · Name + SourceLocation.</summary>
    private static async Task<List<SourceInfo>> ListPsGalleryAsync(CancellationToken ct)
    {
        var res = new List<SourceInfo>();
        var outp = await ShellRunner.CapturePowershell(
            "Get-PSRepository | ForEach-Object { \"$($_.Name)`t$($_.SourceLocation)\" }", ct);
        foreach (var raw in Lines(outp))
        {
            var ln = raw.Trim();
            if (ln.Length == 0) continue;
            var parts = ln.Split('\t');
            var name = parts[0].Trim();
            if (name.Length == 0) continue;
            var url = parts.Length > 1 ? parts[1].Trim() : "";
            res.Add(new SourceInfo("psgallery", name, url, null, null));
        }
        return res;
    }

    /// <summary>pwsh7：`Get-PSResourceRepository` 嘅 Name／Uri · Name + Uri (fall back to PSRepository).</summary>
    private static async Task<List<SourceInfo>> ListPwsh7Async(CancellationToken ct)
    {
        var res = new List<SourceInfo>();
        var script =
            "if (Get-Command Get-PSResourceRepository -ErrorAction SilentlyContinue) { " +
            "Get-PSResourceRepository | ForEach-Object { \"$($_.Name)`t$($_.Uri)\" } } " +
            "else { Get-PSRepository | ForEach-Object { \"$($_.Name)`t$($_.SourceLocation)\" } }";
        var bytes = System.Text.Encoding.Unicode.GetBytes(script);
        var encoded = Convert.ToBase64String(bytes);
        var r = await ShellRunner.Run("pwsh", $"-NoProfile -NonInteractive -EncodedCommand {encoded}", false, ct);
        var outp = r.Output ?? "";
        foreach (var raw in Lines(outp))
        {
            var ln = raw.Trim();
            if (ln.Length == 0) continue;
            var parts = ln.Split('\t');
            var name = parts[0].Trim();
            if (name.Length == 0) continue;
            var url = parts.Length > 1 ? parts[1].Trim() : "";
            res.Add(new SourceInfo("pwsh7", name, url, null, null));
        }
        return res;
    }

    /// <summary>npm：讀預設 registry（唯讀合成行）· read the default registry as one read-only row.</summary>
    private static async Task<List<SourceInfo>> ListNpmAsync(CancellationToken ct)
    {
        string url = "https://registry.npmjs.org";
        try
        {
            var got = (await ShellRunner.Capture("npm", "config get registry", ct)).Trim();
            if (!string.IsNullOrWhiteSpace(got) && got.Contains("://")) url = got;
        }
        catch { }
        return Synthetic("npm", "npm registry", url);
    }

    // ===== add =====

    /// <summary>加一個來源 · Add a source. 名／URL 已消毒，需要管理員時自動提權。 Sanitised; elevates where required.</summary>
    public static async Task<TweakResult> AddAsync(string managerKey, string name, string url, CancellationToken ct)
    {
        try
        {
            string n = SafeName(name);
            string u = SafeUrl(url);
            if (n.Length == 0) return TweakResult.Fail(
                "Use a source name containing only letters, numbers, spaces, dots, dashes or underscores.",
                "來源名稱只可以用字母、數字、空格、點、橫線或底線。");

            switch (managerKey)
            {
                case "winget":
                    if (u.Length == 0) return TweakResult.Fail("A valid HTTP(S) URL without embedded credentials is required.", "請填寫有效而且唔內嵌帳密嘅 HTTP(S) URL。");
                    return await ShellRunner.RunCmd(
                        $"winget source add --name \"{n}\" --arg \"{u}\" --accept-source-agreements --disable-interactivity",
                        elevated: true, ct);
                case "scoop":
                    return await ShellRunner.RunPowershell(
                        $"scoop bucket add {PsLiteral(n)}{(u.Length == 0 ? "" : " " + PsLiteral(u))}", elevated: false, ct);
                case "choco":
                    if (u.Length == 0) return TweakResult.Fail("A valid HTTP(S) URL without embedded credentials is required.", "請填寫有效而且唔內嵌帳密嘅 HTTP(S) URL。");
                    return await ShellRunner.RunCmd($"choco source add -n=\"{n}\" -s=\"{u}\" -y", elevated: true, ct);
                case "dotnet":
                    if (u.Length == 0) return TweakResult.Fail("A valid HTTP(S) URL without embedded credentials is required.", "請填寫有效而且唔內嵌帳密嘅 HTTP(S) URL。");
                    return await ShellRunner.RunCmd($"dotnet nuget add source \"{u}\" --name \"{n}\"", elevated: false, ct);
                case "psgallery":
                    if (u.Length == 0) return TweakResult.Fail("A valid HTTP(S) URL without embedded credentials is required.", "請填寫有效而且唔內嵌帳密嘅 HTTP(S) URL。");
                    return await ShellRunner.RunPowershell(
                        $"Register-PSRepository -Name {PsLiteral(n)} -SourceLocation {PsLiteral(u)} -InstallationPolicy Untrusted", elevated: false, ct);
                case "pwsh7":
                    if (u.Length == 0) return TweakResult.Fail("A valid HTTP(S) URL without embedded credentials is required.", "請填寫有效而且唔內嵌帳密嘅 HTTP(S) URL。");
                    return await RunPwsh7($"Register-PSResourceRepository -Name {PsLiteral(n)} -Uri {PsLiteral(u)}", ct);
                default:
                    return TweakResult.Fail("This manager does not support adding sources.", "呢個管理器唔支援加來源。");
            }
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ===== remove =====

    /// <summary>移除一個來源 · Remove a source by name. 需要管理員時自動提權。 Elevates where required.</summary>
    public static async Task<TweakResult> RemoveAsync(string managerKey, string name, CancellationToken ct)
    {
        try
        {
            string n = SafeName(name);
            if (n.Length == 0) return TweakResult.Fail("The source name is invalid.", "來源名稱無效。");

            switch (managerKey)
            {
                case "winget":
                    return await ShellRunner.RunCmd(
                        $"winget source remove --name \"{n}\" --disable-interactivity", elevated: true, ct);
                case "scoop":
                    return await ShellRunner.RunPowershell($"scoop bucket rm {PsLiteral(n)}", elevated: false, ct);
                case "choco":
                    return await ShellRunner.RunCmd($"choco source remove -n=\"{n}\" -y", elevated: true, ct);
                case "dotnet":
                    return await ShellRunner.RunCmd($"dotnet nuget remove source \"{n}\"", elevated: false, ct);
                case "psgallery":
                    return await ShellRunner.RunPowershell($"Unregister-PSRepository -Name {PsLiteral(n)}", elevated: false, ct);
                case "pwsh7":
                    return await RunPwsh7($"Unregister-PSResourceRepository -Name {PsLiteral(n)}", ct);
                default:
                    return TweakResult.Fail("This manager does not support removing sources.", "呢個管理器唔支援移除來源。");
            }
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ===== refresh =====

    /// <summary>重新整理來源索引 · Refresh source indexes. winget/scoop 真做，其餘盡力而為回 Ok。</summary>
    public static async Task<TweakResult> RefreshAsync(string managerKey, CancellationToken ct)
    {
        try
        {
            switch (managerKey)
            {
                case "winget":
                    return await ShellRunner.RunCmd("winget source update --disable-interactivity", elevated: false, ct);
                case "scoop":
                    return await ShellRunner.RunPowershell("scoop update", elevated: false, ct);
                default:
                    // 其餘管理器冇便宜嘅來源重新整理 · others have no cheap index refresh — best-effort Ok.
                    return TweakResult.Ok("Nothing to refresh for this manager.", "呢個管理器冇嘢需要重新整理。");
            }
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ===== helpers =====

    /// <summary>用 pwsh（PowerShell 7）跑 EncodedCommand · Run a snippet under pwsh 7 via EncodedCommand.</summary>
    private static async Task<TweakResult> RunPwsh7(string script, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.Unicode.GetBytes(script);
        var encoded = Convert.ToBase64String(bytes);
        return await ShellRunner.Run("pwsh",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", false, ct);
    }

    /// <summary>用標題列欄位起點切一段 · Cut a substring between two column offsets (defensive).</summary>
    private static string Cut(string ln, int a, int b)
    {
        if (a < 0 || a >= ln.Length) return "";
        b = Math.Min(b, ln.Length);
        return b > a ? ln.Substring(a, b - a).Trim() : "";
    }

    /// <summary>用 2+ 空白切欄，後備用任何空白 · Split a row on 2+ spaces, falling back to any whitespace.</summary>
    private static string[] SplitCols(string raw)
    {
        var line = (raw ?? "").TrimEnd();
        if (line.Trim().Length == 0) return Array.Empty<string>();
        var cols = System.Text.RegularExpressions.Regex
            .Split(line.Trim(), " {2,}|\t+")
            .Where(s => s.Trim().Length > 0).Select(s => s.Trim()).ToArray();
        if (cols.Length <= 1)
            cols = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return cols;
    }
}
