using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 結構化套件詳情解析（UniGetUI 式）· Structured package-details parser, UniGetUI-style.
/// 唔同 <see cref="PackageManagerRegistry.DetailsAsync"/>（原始文字傾倒），呢個服務
/// 將每個管理器嘅機器可讀輸出拆成欄位，畀詳情面板渲染。
/// Unlike <see cref="PackageManagerRegistry.DetailsAsync"/> (raw text dump), this service parses each
/// manager's machine-readable output into fields for the structured details panel.
/// 所有方法都要穩陣：包住 shell 呼叫、出錯就回空白／Fail，永遠唔好擲例外。
/// Every method must be robust: wrap shell calls, return blank / Fail on error — NEVER throw.
/// </summary>
public static class PackageDetails
{
    /// <summary>一個相依項目 · One dependency entry.</summary>
    public sealed record DependencyInfo(string Name, string Version, bool Mandatory);

    /// <summary>
    /// 一個套件嘅完整結構化詳情 · Full structured details for one package.
    /// 每個欄位由所屬管理器盡量填；管理器唔提供嘅就留空。
    /// Each field is filled as far as the owning manager exposes it; the rest stay blank.
    /// </summary>
    public sealed record Details(
        string Name,
        string Id,
        string Version,
        string Description,
        string Author,
        string Publisher,
        string Homepage,
        string License,
        string LicenseUrl,
        string ManifestUrl,
        string InstallerUrl,
        string InstallerType,
        string InstallerHash,
        string ReleaseDate,
        string ReleaseNotes,
        string ReleaseNotesUrl,
        List<string> Tags,
        List<DependencyInfo> Dependencies)
    {
        /// <summary>全空白詳情（只帶名／id／版本）· Empty details carrying only name/id/version.</summary>
        public static Details Empty(PackageItem item) => new(
            item.Name, item.Id, item.Version, "", "", "", "", "", "", "", "", "", "", "", "", "",
            new List<string>(), new List<DependencyInfo>());
    }

    // ===== public API =====

    /// <summary>
    /// 攞一個套件嘅結構化詳情 · Get structured details for one package, per its manager.
    /// 永遠唔擲例外；出錯就回只帶名／id／版本嘅空白詳情。
    /// Never throws; on error returns blank details carrying only name/id/version.
    /// </summary>
    public static async Task<Details> GetAsync(PackageItem item, CancellationToken ct = default)
    {
        try
        {
            var id = item.Id ?? "";
            if (string.IsNullOrWhiteSpace(id)) return Details.Empty(item);
            return item.ManagerKey switch
            {
                "winget" => await WingetAsync(item, ct),
                "scoop" => await ScoopAsync(item, ct),
                "choco" => await ChocoAsync(item, ct),
                "pip" => await PipAsync(item, ct),
                "npm" => await NpmAsync(item, ct),
                "bun" => await NpmAsync(item, ct),
                "dotnet" => await DotnetAsync(item, ct),
                "cargo" => await CargoAsync(item, ct),
                "psgallery" => await PsGalleryAsync(item, ct),
                "pwsh7" => await Pwsh7Async(item, ct),
                _ => Details.Empty(item),
            };
        }
        catch { return Details.Empty(item); }
    }

    /// <summary>
    /// 攞可安裝嘅版本清單（新到舊）· List installable versions (newest first); empty on failure.
    /// </summary>
    public static async Task<List<string>> GetInstallableVersionsAsync(PackageItem item, CancellationToken ct = default)
    {
        try
        {
            var id = Esc(item.Id);
            if (string.IsNullOrWhiteSpace(id)) return new List<string>();
            return item.ManagerKey switch
            {
                "winget" => await WingetVersionsAsync(id, ct),
                "scoop" => await ScoopVersionsAsync(item, ct),
                "choco" => await ChocoVersionsAsync(id, ct),
                "pip" => await PipVersionsAsync(id, ct),
                "npm" => await NpmVersionsAsync(id, ct),
                "bun" => await NpmVersionsAsync(id, ct),
                "dotnet" => await DotnetVersionsAsync(id, ct),
                "cargo" => await CargoVersionsAsync(item, ct),
                "psgallery" => await PsGalleryVersionsAsync(id, ct),
                "pwsh7" => await Pwsh7VersionsAsync(id, ct),
                _ => new List<string>(),
            };
        }
        catch { return new List<string>(); }
    }

    /// <summary>
    /// 下載安裝程式到指定資料夾 · Download the installer to a destination folder.
    /// winget 有原生 download 動詞；其他管理器若知道 InstallerUrl 就用 Invoke-WebRequest 盡力下載，
    /// 否則回 Fail 並附雙語「唔支援」訊息。
    /// winget has a native download verb; other managers best-effort download via Invoke-WebRequest if an
    /// InstallerUrl is known, otherwise Fail with a bilingual "not supported" message.
    /// </summary>
    public static async Task<TweakResult> DownloadInstallerAsync(PackageItem item, string destDir, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(destDir) || !Directory.Exists(destDir))
                return TweakResult.Fail("Destination folder is invalid.", "目的地資料夾無效。");

            var id = Esc(item.Id);
            if (item.ManagerKey == "winget")
            {
                var cmd = $"winget download --id \"{id}\" -e --accept-source-agreements --accept-package-agreements --disable-interactivity -d \"{destDir}\"";
                return await ShellRunner.RunCmd(cmd, false, ct);
            }

            // 其他管理器：若知道直接連結就盡力下載 · Other managers: best-effort direct download if URL is known.
            var details = await GetAsync(item, ct);
            if (!string.IsNullOrWhiteSpace(details.InstallerUrl))
            {
                var url = details.InstallerUrl.Trim();
                var fileName = SafeFileName(url, item);
                var dest = Path.Combine(destDir, fileName);
                var script =
                    "$ErrorActionPreference='Stop'; [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; " +
                    $"Invoke-WebRequest -Uri '{url.Replace("'", "''")}' -OutFile '{dest.Replace("'", "''")}' -UseBasicParsing";
                var r = await ShellRunner.RunPowershell(script, false, ct);
                return r.Success
                    ? TweakResult.Ok($"Downloaded to {dest}.", $"已下載到 {dest}。", r.Output)
                    : TweakResult.Fail($"Download failed: {r.Message?.En}", $"下載失敗：{r.Message?.Zh}", r.Output);
            }

            return TweakResult.Fail(
                "Download is not supported for this package manager.",
                "呢個套件管理器唔支援下載安裝程式。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>
    /// 砌出可複製嘅人類可讀安裝指令 · Build the human-readable CLI install command to copy.
    /// </summary>
    public static string BuildInstallCommand(PackageItem item)
    {
        try
        {
            var id = item?.Id ?? "";
            if (string.IsNullOrWhiteSpace(id)) return "";
            return item!.ManagerKey switch
            {
                "winget" => $"winget install --id {id} -e",
                "scoop" => $"scoop install {id}",
                "choco" => $"choco install {id} -y",
                "pip" => $"pip install {id}",
                "npm" => $"npm install -g {id}",
                "bun" => $"bun add -g {id}",
                "dotnet" => $"dotnet tool install -g {id}",
                "cargo" => $"cargo install {id}",
                "vcpkg" => $"vcpkg install {id}",
                "psgallery" => $"Install-Module -Name {id} -Scope CurrentUser",
                "pwsh7" => $"Install-PSResource -Name {id}",
                _ => id,
            };
        }
        catch { return item?.Id ?? ""; }
    }

    // ===== per-manager details =====

    /// <summary>winget show --id … -e（縮排 Key: value 文字）· Parse winget's indented "Key: value" show text.</summary>
    private static async Task<Details> WingetAsync(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var text = await ShellRunner.CapturePowershell(
            $"winget show --id \"{id}\" -e --accept-source-agreements --disable-interactivity | Out-String -Width 400", ct);

        var map = ParseIndentedKv(text);
        string Get(params string[] keys)
        {
            foreach (var k in keys)
                if (map.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            return "";
        }

        // 標籤可能本地化，所以盡量用多個常見鍵 · keys may be localized; try several common labels.
        var tags = ParseListSection(text, "Tags");
        var deps = ParseWingetDependencies(text);
        var releaseNotes = ParseWingetReleaseNotes(text);

        return new Details(
            Name: string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
            Id: item.Id,
            Version: Get("Version") is { Length: > 0 } v ? v : item.Version,
            Description: Get("Description", "Summary"),
            Author: Get("Author"),
            Publisher: Get("Publisher"),
            Homepage: Get("Homepage", "Home page", "PublisherUrl", "Publisher Url"),
            License: Get("License"),
            LicenseUrl: Get("License Url", "LicenseUrl"),
            ManifestUrl: Get("Manifest", "Manifest Url"),
            InstallerUrl: Get("Installer Url", "InstallerUrl", "Download Url"),
            InstallerType: Get("Installer Type", "InstallerType", "Type"),
            InstallerHash: Get("Installer SHA256", "SHA256", "Installer Sha256"),
            ReleaseDate: Get("Release Date", "ReleaseDate", "Release Notes Date"),
            ReleaseNotes: releaseNotes,
            ReleaseNotesUrl: Get("Release Notes Url", "ReleaseNotesUrl"),
            Tags: tags,
            Dependencies: deps);
    }

    /// <summary>scoop info（Key: value 行 + Bucket）· scoop info: key:value lines, including Bucket.</summary>
    private static async Task<Details> ScoopAsync(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var text = await ShellRunner.CapturePowershell($"scoop info {id} | Out-String -Width 400", ct);
        var map = ParseColonKv(text);
        string Get(params string[] keys)
        {
            foreach (var k in keys)
                if (map.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            return "";
        }
        var deps = SplitList(Get("Depends", "Dependencies"))
            .Select(d => new DependencyInfo(d, "", true)).ToList();

        return new Details(
            Name: Get("Name") is { Length: > 0 } n ? n : (string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name),
            Id: item.Id,
            Version: Get("Version") is { Length: > 0 } v ? v : item.Version,
            Description: Get("Description"),
            Author: "",
            Publisher: "",
            Homepage: Get("Website", "Homepage"),
            License: Get("License"),
            LicenseUrl: "",
            ManifestUrl: Get("Manifest"),
            InstallerUrl: "",
            InstallerType: "",
            InstallerHash: "",
            ReleaseDate: Get("Updated at", "Updated"),
            ReleaseNotes: Get("Notes"),
            ReleaseNotesUrl: "",
            Tags: SplitList(Get("Bucket")),
            Dependencies: deps);
    }

    /// <summary>choco info -r（行格式：title|ver|…）+ 文字後備 · choco info -r line + text fallback.</summary>
    private static async Task<Details> ChocoAsync(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var text = await ShellRunner.Capture("choco", $"info {id}", ct);
        var map = ParseColonKv(text);
        string Get(params string[] keys)
        {
            foreach (var k in keys)
                if (map.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            return "";
        }
        // 描述喺 "Description:" 之後嘅自由文字 · description is free text after "Description:".
        var desc = Get("Description");
        if (string.IsNullOrWhiteSpace(desc)) desc = ExtractAfter(text, "Description:");

        return new Details(
            Name: Get("Title") is { Length: > 0 } n ? n : (string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name),
            Id: item.Id,
            Version: item.Version,
            Description: desc,
            Author: Get("Author(s)", "Author", "Software Author(s)"),
            Publisher: Get("Published by", "Publisher"),
            Homepage: Get("Software Site", "Project Url", "Documentation", "Homepage"),
            License: Get("License"),
            LicenseUrl: Get("License Url", "Software License"),
            ManifestUrl: Get("Package Source", "Package Url"),
            InstallerUrl: "",
            InstallerType: "",
            InstallerHash: "",
            ReleaseDate: Get("Published", "Published on"),
            ReleaseNotes: Get("Release Notes"),
            ReleaseNotesUrl: Get("Release Notes"),
            Tags: SplitList(Get("Tags")),
            Dependencies: new List<DependencyInfo>());
    }

    /// <summary>pip show（Name/Version/Summary/Home-page/Author/License/Requires）· pip show fields.</summary>
    private static async Task<Details> PipAsync(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var text = await ShellRunner.Capture("pip", $"show {id}", ct);
        var map = ParseColonKv(text);
        string Get(params string[] keys)
        {
            foreach (var k in keys)
                if (map.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            return "";
        }
        var deps = SplitList(Get("Requires"))
            .Select(d => new DependencyInfo(d, "", true)).ToList();

        return new Details(
            Name: Get("Name") is { Length: > 0 } n ? n : (string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name),
            Id: item.Id,
            Version: Get("Version") is { Length: > 0 } v ? v : item.Version,
            Description: Get("Summary"),
            Author: Get("Author"),
            Publisher: Get("Author"),
            Homepage: Get("Home-page", "Homepage"),
            License: Get("License"),
            LicenseUrl: "",
            ManifestUrl: "",
            InstallerUrl: "",
            InstallerType: "wheel/sdist",
            InstallerHash: "",
            ReleaseDate: "",
            ReleaseNotes: "",
            ReleaseNotesUrl: "",
            Tags: new List<string>(),
            Dependencies: deps);
    }

    /// <summary>npm/bun view --json（JSON）· npm view --json (also used by Bun, which installs npm packages).</summary>
    private static async Task<Details> NpmAsync(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var raw = await ShellRunner.Capture("npm", $"view {id} --json", ct);
        var json = ExtractObject(raw);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Details.Empty(item);

            string homepage = PkgParse.Str(root, "homepage");
            string license = ReadLicense(root);
            string installerUrl = "", hash = "";
            if (root.TryGetProperty("dist", out var dist) && dist.ValueKind == JsonValueKind.Object)
            {
                installerUrl = PkgParse.Str(dist, "tarball");
                hash = PkgParse.Str(dist, "shasum");
                if (!string.IsNullOrEmpty(hash)) hash = "sha1:" + hash;
                if (string.IsNullOrEmpty(hash))
                {
                    var integrity = PkgParse.Str(dist, "integrity");
                    if (!string.IsNullOrEmpty(integrity)) hash = integrity;
                }
            }
            string releaseDate = "";
            if (root.TryGetProperty("time", out var time) && time.ValueKind == JsonValueKind.Object)
            {
                releaseDate = PkgParse.Str(time, "modified");
                if (string.IsNullOrEmpty(releaseDate)) releaseDate = PkgParse.Str(time, "created");
            }
            var tags = ReadStringArray(root, "keywords");
            var deps = ReadDependencyObject(root, "dependencies");
            string author = ReadPerson(root, "author");

            return new Details(
                Name: PkgParse.Str(root, "name") is { Length: > 0 } n ? n : item.Id,
                Id: item.Id,
                Version: PkgParse.Str(root, "version") is { Length: > 0 } v ? v : item.Version,
                Description: PkgParse.Str(root, "description"),
                Author: author,
                Publisher: author,
                Homepage: homepage,
                License: license,
                LicenseUrl: "",
                ManifestUrl: homepage,
                InstallerUrl: installerUrl,
                InstallerType: "tarball (npm)",
                InstallerHash: hash,
                ReleaseDate: releaseDate,
                ReleaseNotes: "",
                ReleaseNotesUrl: PkgParse.Str(root, "homepage"),
                Tags: tags,
                Dependencies: deps);
        }
        catch { return Details.Empty(item); }
    }

    /// <summary>dotnet tool search --detail（縮排 Key: value）· dotnet tool search --detail text.</summary>
    private static async Task<Details> DotnetAsync(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var text = await ShellRunner.Capture("dotnet", $"tool search {id} --detail", ct);
        var map = ParseColonKv(text);
        string Get(params string[] keys)
        {
            foreach (var k in keys)
                if (map.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            return "";
        }
        return new Details(
            Name: Get("Package ID", "Package Id") is { Length: > 0 } n ? n : (string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name),
            Id: item.Id,
            Version: Get("Latest Version", "Version") is { Length: > 0 } v ? v : item.Version,
            Description: Get("Summary", "Description"),
            Author: Get("Authors", "Author"),
            Publisher: Get("Owners", "Authors"),
            Homepage: Get("Project URL", "Project Url", "Homepage"),
            License: Get("License"),
            LicenseUrl: Get("License URL", "License Url"),
            ManifestUrl: "",
            InstallerUrl: "",
            InstallerType: "nuget tool",
            InstallerHash: "",
            ReleaseDate: "",
            ReleaseNotes: "",
            ReleaseNotesUrl: "",
            Tags: SplitList(Get("Tags")),
            Dependencies: new List<DependencyInfo>());
    }

    /// <summary>cargo search（盡力而為，得名同版本同描述）· cargo search best-effort (name/version/desc).</summary>
    private static async Task<Details> CargoAsync(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var text = await ShellRunner.Capture("cargo", $"search {id}", ct);
        string ver = item.Version, desc = "";
        foreach (var raw in PkgParse.Lines(text))
        {
            var ln = raw.Trim();
            if (ln.Length == 0) continue;
            int eq = ln.IndexOf('=');
            if (eq <= 0) continue;
            var name = ln.Substring(0, eq).Trim();
            if (!string.Equals(name, item.Id, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = ln.Substring(eq + 1).Trim();
            int q1 = rest.IndexOf('"');
            int q2 = q1 >= 0 ? rest.IndexOf('"', q1 + 1) : -1;
            if (q1 >= 0 && q2 > q1) ver = rest.Substring(q1 + 1, q2 - q1 - 1);
            int hash = rest.IndexOf('#');
            if (hash >= 0 && hash + 1 < rest.Length) desc = rest.Substring(hash + 1).Trim();
            break;
        }
        return new Details(
            Name: string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name,
            Id: item.Id,
            Version: ver,
            Description: desc,
            Author: "",
            Publisher: "",
            Homepage: $"https://crates.io/crates/{item.Id}",
            License: "",
            LicenseUrl: "",
            ManifestUrl: $"https://crates.io/crates/{item.Id}",
            InstallerUrl: "",
            InstallerType: "crate",
            InstallerHash: "",
            ReleaseDate: "",
            ReleaseNotes: "",
            ReleaseNotesUrl: "",
            Tags: new List<string>(),
            Dependencies: new List<DependencyInfo>());
    }

    /// <summary>Find-Module | Format-List *（ProjectUri/LicenseUri/PublishedDate/Dependencies）· PSGallery.</summary>
    private static async Task<Details> PsGalleryAsync(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var json = await ShellRunner.CapturePowershellJson(
            $"Find-Module -Name \"{id}\" -ErrorAction SilentlyContinue | Select-Object -First 1 " +
            "Name,Version,Description,Author,CompanyName,ProjectUri,LicenseUri,PublishedDate,Tags | ConvertTo-Json -Depth 3", ct);
        return FromPsJson(item, json);
    }

    /// <summary>Find-PSResource | … （pwsh7）· PowerShell 7 PSResource details.</summary>
    private static async Task<Details> Pwsh7Async(PackageItem item, CancellationToken ct)
    {
        var id = Esc(item.Id);
        var script =
            $"Find-PSResource -Name '{id}' -ErrorAction SilentlyContinue | Select-Object -First 1 " +
            "Name,Version,Description,Author,CompanyName,@{N='ProjectUri';E={$_.ProjectUri}},@{N='LicenseUri';E={$_.LicenseUri}},@{N='PublishedDate';E={$_.PublishedDate}},Tags | ConvertTo-Json -Depth 3";
        var json = await CapturePwshJson(script, ct);
        return FromPsJson(item, json);
    }

    private static Details FromPsJson(PackageItem item, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
                root = root.GetArrayLength() > 0 ? root[0] : default;
            if (root.ValueKind != JsonValueKind.Object) return Details.Empty(item);

            string author = PkgParse.Str(root, "Author");
            return new Details(
                Name: PkgParse.Str(root, "Name") is { Length: > 0 } n ? n : item.Id,
                Id: item.Id,
                Version: PkgParse.Str(root, "Version") is { Length: > 0 } v ? v : item.Version,
                Description: PkgParse.Str(root, "Description"),
                Author: author,
                Publisher: PkgParse.Str(root, "CompanyName") is { Length: > 0 } c ? c : author,
                Homepage: PkgParse.Str(root, "ProjectUri"),
                License: "",
                LicenseUrl: PkgParse.Str(root, "LicenseUri"),
                ManifestUrl: PkgParse.Str(root, "ProjectUri"),
                InstallerUrl: "",
                InstallerType: "PowerShell module",
                InstallerHash: "",
                ReleaseDate: PkgParse.Str(root, "PublishedDate"),
                ReleaseNotes: "",
                ReleaseNotesUrl: "",
                Tags: ReadStringArray(root, "Tags"),
                Dependencies: new List<DependencyInfo>());
        }
        catch { return Details.Empty(item); }
    }

    // ===== per-manager installable versions =====

    private static async Task<List<string>> WingetVersionsAsync(string id, CancellationToken ct)
    {
        var text = await ShellRunner.CapturePowershell(
            $"winget show --id \"{id}\" -e --versions --accept-source-agreements --disable-interactivity | Out-String -Width 200", ct);
        var res = new List<string>();
        bool past = false;
        foreach (var raw in PkgParse.Lines(text))
        {
            var ln = raw.Trim();
            if (!past) { if (ln.StartsWith("---")) past = true; continue; }
            if (ln.Length == 0) continue;
            if (ln.Contains(' ')) continue; // 只取單欄版本 · single-column version only
            res.Add(ln);
        }
        return Dedup(res);
    }

    private static async Task<List<string>> ScoopVersionsAsync(PackageItem item, CancellationToken ct)
    {
        // scoop 冇方便嘅版本清單；用 info 嘅 Version 做單一最佳值 · scoop has no easy version list — single best from info.
        var d = await ScoopAsync(item, ct);
        return string.IsNullOrWhiteSpace(d.Version) ? new List<string>() : new List<string> { d.Version };
    }

    private static async Task<List<string>> ChocoVersionsAsync(string id, CancellationToken ct)
    {
        var text = await ShellRunner.Capture("choco", $"find {id} --all-versions -r", ct);
        var res = new List<string>();
        foreach (var raw in PkgParse.Lines(text))
        {
            var ln = raw.Trim();
            if (ln.Length == 0 || !ln.Contains('|')) continue;
            var parts = ln.Split('|');
            if (parts.Length < 2) continue;
            if (!string.Equals(parts[0].Trim(), id.Trim('"'), StringComparison.OrdinalIgnoreCase)) continue;
            res.Add(parts[1].Trim());
        }
        res.Reverse(); // choco lists oldest-first · choco 由舊到新，反轉成新先
        return Dedup(res);
    }

    private static async Task<List<string>> PipVersionsAsync(string id, CancellationToken ct)
    {
        var text = await ShellRunner.Capture("pip", $"index versions {id}", ct);
        foreach (var raw in PkgParse.Lines(text))
        {
            var ln = raw.Trim();
            int idx = ln.IndexOf("Available versions:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var rest = ln.Substring(idx + "Available versions:".Length);
            var versions = rest.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            return Dedup(versions); // pip index already newest-first
        }
        return new List<string>();
    }

    private static async Task<List<string>> NpmVersionsAsync(string id, CancellationToken ct)
    {
        var raw = await ShellRunner.Capture("npm", $"view {id} versions --json", ct);
        var json = ExtractArrayOrObject(raw);
        var res = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
                foreach (var el in root.EnumerateArray())
                    if (el.ValueKind == JsonValueKind.String) res.Add(el.GetString() ?? "");
            else if (root.ValueKind == JsonValueKind.String)
                res.Add(root.GetString() ?? "");
        }
        catch { }
        res.Reverse(); // npm lists oldest-first · npm 由舊到新
        return Dedup(res.Where(s => s.Length > 0).ToList());
    }

    private static async Task<List<string>> DotnetVersionsAsync(string id, CancellationToken ct)
    {
        // dotnet 冇直接版本清單；由詳情攞最新版本做單一值 · no list verb — single latest from search detail.
        var text = await ShellRunner.Capture("dotnet", $"tool search {id} --detail", ct);
        var map = ParseColonKv(text);
        if (map.TryGetValue("Latest Version", out var v) && !string.IsNullOrWhiteSpace(v))
            return new List<string> { v.Trim() };
        return new List<string>();
    }

    private static async Task<List<string>> CargoVersionsAsync(PackageItem item, CancellationToken ct)
    {
        var d = await CargoAsync(item, ct);
        return string.IsNullOrWhiteSpace(d.Version) ? new List<string>() : new List<string> { d.Version };
    }

    private static async Task<List<string>> PsGalleryVersionsAsync(string id, CancellationToken ct)
    {
        var json = await ShellRunner.CapturePowershellJson(
            $"Find-Module -Name \"{id}\" -AllVersions -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Version | ForEach-Object {{ $_.ToString() }} | ConvertTo-Json", ct);
        return JsonVersions(json);
    }

    private static async Task<List<string>> Pwsh7VersionsAsync(string id, CancellationToken ct)
    {
        var script =
            $"Find-PSResource -Name '{id}' -Version '*' -ErrorAction SilentlyContinue | " +
            "Select-Object -ExpandProperty Version | ForEach-Object { $_.ToString() } | ConvertTo-Json";
        var json = await CapturePwshJson(script, ct);
        return JsonVersions(json);
    }

    private static List<string> JsonVersions(string json)
    {
        var res = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
                foreach (var el in root.EnumerateArray())
                    res.Add(el.ValueKind == JsonValueKind.String ? (el.GetString() ?? "") : el.ToString());
            else if (root.ValueKind == JsonValueKind.String)
                res.Add(root.GetString() ?? "");
        }
        catch { }
        return Dedup(res.Where(s => s.Length > 0).ToList()); // Find-* yields newest-first
    }

    // ===== JSON helpers =====

    private static string ReadLicense(JsonElement root)
    {
        try
        {
            if (root.TryGetProperty("license", out var lic))
            {
                if (lic.ValueKind == JsonValueKind.String) return lic.GetString() ?? "";
                if (lic.ValueKind == JsonValueKind.Object) return PkgParse.Str(lic, "type");
            }
        }
        catch { }
        return "";
    }

    private static string ReadPerson(JsonElement root, string prop)
    {
        try
        {
            if (root.TryGetProperty(prop, out var p))
            {
                if (p.ValueKind == JsonValueKind.String) return p.GetString() ?? "";
                if (p.ValueKind == JsonValueKind.Object) return PkgParse.Str(p, "name");
            }
        }
        catch { }
        return "";
    }

    private static List<string> ReadStringArray(JsonElement root, string prop)
    {
        var res = new List<string>();
        try
        {
            if (root.TryGetProperty(prop, out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var el in arr.EnumerateArray())
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) res.Add(s.Trim());
                    }
        }
        catch { }
        return res;
    }

    private static List<DependencyInfo> ReadDependencyObject(JsonElement root, string prop)
    {
        var res = new List<DependencyInfo>();
        try
        {
            if (root.TryGetProperty(prop, out var obj) && obj.ValueKind == JsonValueKind.Object)
                foreach (var p in obj.EnumerateObject())
                    res.Add(new DependencyInfo(p.Name,
                        p.Value.ValueKind == JsonValueKind.String ? (p.Value.GetString() ?? "") : "", true));
        }
        catch { }
        return res;
    }

    // ===== text parsing helpers =====

    /// <summary>解析縮排「Key: value」（winget show 格式）· Parse indented "Key: value" (winget show layout).</summary>
    private static Dictionary<string, string> ParseIndentedKv(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return map;
        foreach (var raw in PkgParse.Lines(text))
        {
            var line = raw.TrimEnd();
            if (line.Trim().Length == 0) continue;
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            // 鍵唔可以有太多前導空白邏輯——winget 嘅頂層欄位有 2-space 縮排 · top-level fields are 2-space indented.
            var key = line.Substring(0, colon).Trim();
            var val = line.Substring(colon + 1).Trim();
            if (key.Length == 0 || key.Contains("://")) continue;
            if (!map.ContainsKey(key)) map[key] = val;
        }
        return map;
    }

    /// <summary>解析「Key: value」（任意縮排）· Parse "Key: value" lines (any indentation).</summary>
    private static Dictionary<string, string> ParseColonKv(string text)
        => ParseIndentedKv(text);

    /// <summary>抽出某 header 之後嘅縮排清單（如 winget 嘅 Tags）· Extract an indented list under a header (e.g. winget Tags).</summary>
    private static List<string> ParseListSection(string text, string header)
    {
        var res = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return res;
        var lines = PkgParse.Lines(text);
        for (int i = 0; i < lines.Length; i++)
        {
            var ln = lines[i];
            if (ln.Trim().TrimEnd(':').Equals(header, StringComparison.OrdinalIgnoreCase)
                || ln.TrimStart().StartsWith(header + ":", StringComparison.OrdinalIgnoreCase))
            {
                int baseIndent = Indent(ln);
                // 同行如果有值（Tags: a b c），切開 · inline values after the header.
                var inline = ln.Contains(':') ? ln.Substring(ln.IndexOf(':') + 1).Trim() : "";
                if (inline.Length > 0) res.AddRange(SplitList(inline));
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var sub = lines[j];
                    if (sub.Trim().Length == 0) break;
                    if (Indent(sub) <= baseIndent) break;
                    var item = sub.Trim();
                    if (item.Length > 0) res.Add(item);
                }
                break;
            }
        }
        return Dedup(res);
    }

    /// <summary>解析 winget 詳情入面嘅 Dependencies 區段 · Parse the winget "Dependencies" section.</summary>
    private static List<DependencyInfo> ParseWingetDependencies(string text)
    {
        var res = new List<DependencyInfo>();
        if (string.IsNullOrWhiteSpace(text)) return res;
        var lines = PkgParse.Lines(text);
        for (int i = 0; i < lines.Length; i++)
        {
            var ln = lines[i];
            var trimmed = ln.Trim().TrimEnd(':');
            if (!trimmed.Equals("Dependencies", StringComparison.OrdinalIgnoreCase)) continue;
            int baseIndent = Indent(ln);
            for (int j = i + 1; j < lines.Length; j++)
            {
                var sub = lines[j];
                if (sub.Trim().Length == 0) break;
                if (Indent(sub) <= baseIndent) break;
                var item = sub.Trim();
                // 跳過子標題（如 "Package Dependencies:"）· skip sub-headers like "Package Dependencies:".
                if (item.EndsWith(":")) continue;
                res.Add(new DependencyInfo(item, "", true));
            }
            break;
        }
        return res;
    }

    /// <summary>抽出 winget 詳情入面嘅 Release Notes 文字 · Extract winget "Release Notes" body text.</summary>
    private static string ParseWingetReleaseNotes(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var lines = PkgParse.Lines(text);
        for (int i = 0; i < lines.Length; i++)
        {
            var ln = lines[i];
            var t = ln.Trim();
            if (t.StartsWith("Release Notes:", StringComparison.OrdinalIgnoreCase)
                && !t.StartsWith("Release Notes Url", StringComparison.OrdinalIgnoreCase))
            {
                int baseIndent = Indent(ln);
                var inline = ln.Substring(ln.IndexOf(':') + 1).Trim();
                var sb = new List<string>();
                if (inline.Length > 0) sb.Add(inline);
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var sub = lines[j];
                    if (sub.Trim().Length == 0) { if (sb.Count > 0) break; else continue; }
                    if (Indent(sub) <= baseIndent) break;
                    sb.Add(sub.Trim());
                }
                return string.Join("\n", sb).Trim();
            }
        }
        return "";
    }

    private static string ExtractAfter(string text, string marker)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        int idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";
        var rest = text.Substring(idx + marker.Length);
        var sb = new List<string>();
        foreach (var raw in PkgParse.Lines(rest))
        {
            var ln = raw.Trim();
            if (sb.Count > 0 && (ln.Length == 0 || ln.EndsWith(":") || ln.Contains("|"))) break;
            if (ln.Length > 0) sb.Add(ln);
            if (sb.Count > 6) break;
        }
        return string.Join(" ", sb).Trim();
    }

    private static int Indent(string line)
    {
        int n = 0;
        foreach (var c in line) { if (c == ' ') n++; else break; }
        return n;
    }

    private static List<string> SplitList(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return new List<string>();
        return s.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> Dedup(List<string> input)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var res = new List<string>();
        foreach (var s in input)
        {
            var v = (s ?? "").Trim();
            if (v.Length == 0) continue;
            if (seen.Add(v)) res.Add(v);
        }
        return res;
    }

    private static string Esc(string? s) => (s ?? "").Replace("\"", "").Replace("`", "").Trim();

    private static string SafeFileName(string url, PackageItem item)
    {
        try
        {
            var name = url;
            int q = name.IndexOf('?');
            if (q >= 0) name = name.Substring(0, q);
            int slash = name.LastIndexOf('/');
            if (slash >= 0 && slash + 1 < name.Length) name = name.Substring(slash + 1);
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            if (string.IsNullOrWhiteSpace(name)) name = Esc(item.Id) + ".bin";
            return name;
        }
        catch { return Esc(item.Id) + ".bin"; }
    }

    private static string ExtractObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "{}";
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('{'), b = raw.LastIndexOf('}');
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        return "{}";
    }

    private static string ExtractArrayOrObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "[]";
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('['), b = raw.LastIndexOf(']');
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        int c = raw.IndexOf('{'), d = raw.LastIndexOf('}');
        if (c >= 0 && d > c) return raw.Substring(c, d - c + 1);
        return "[]";
    }

    private static async Task<string> CapturePwshJson(string script, CancellationToken ct)
    {
        try
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
        catch { return "[]"; }
    }
}
