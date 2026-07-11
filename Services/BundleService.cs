using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml.Linq;
using WinForge.Models;

namespace WinForge.Services;

// ============================================================================
//  套件清單服務 · Bundle service
// ----------------------------------------------------------------------------
//  將 WinForge 的扁平 JSON 清單升級做 UniGetUI 式、版本化、多格式、帶安裝選項
//  嘅清單系統。支援 JSON / YAML / XML / .ubundle 之間互換、由副檔名挑格式、
//  匯出可獨立執行嘅 .ps1，仲會檢查清單入面有冇危險嘅自訂指令／參數／kill-list。
//
//  Versioned (export_version = 3), multi-format, options-aware bundle system,
//  matching UniGetUI's SerializableBundle shape. Hand-rolled minimal YAML/XML
//  writers + tolerant readers (NO NuGet packages); JSON via System.Text.Json.
//  Every method is defensive and NEVER throws.
// ============================================================================

/// <summary>
/// 一個套件清單（版本化）· One versioned bundle: compatible packages + a separate
/// list of incompatible ones (local / unknown source) kept for logging only.
/// </summary>
public sealed class SerializableBundle
{
    public int export_version = 3;
    public List<SerializablePackage> packages = new();
    public List<SerializableIncompatiblePackage> incompatible_packages = new();
}

/// <summary>清單入面一個可安裝套件 · One installable package inside a bundle.</summary>
public sealed class SerializablePackage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";

    /// <summary>管理器名／鍵 · Manager name or key (e.g. "winget", "scoop").</summary>
    public string ManagerName { get; set; } = "";

    /// <summary>每套件安裝選項（可空）· Per-package install options (may be null).</summary>
    public InstallOptions? InstallationOptions { get; set; }
}

/// <summary>
/// 不相容套件（本機／未知來源）· An incompatible package (local / unknown source) — kept
/// for logging only; it cannot be reinstalled from the bundle.
/// </summary>
public sealed class SerializableIncompatiblePackage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Source { get; set; } = "";
}

/// <summary>
/// 安全檢查報告 · Result of inspecting a bundle for dangerous per-package options.
/// </summary>
public sealed class BundleSecurityReport
{
    /// <summary>有冇任何危險項目 · Whether any dangerous option was found.</summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>雙語警告行（每個危險套件一條）· Bilingual warning lines (one per flagged package field).</summary>
    public List<LocalizedText> Warnings { get; } = new();
}

/// <summary>
/// 載入結果（連版本不符旗標）· Result of loading a bundle, carrying a version-mismatch flag
/// so the UI can warn when export_version != 3.
/// </summary>
public sealed class BundleLoadResult
{
    public SerializableBundle Bundle { get; set; } = new();

    /// <summary>版本不符（export_version != 3）· True when export_version != 3.</summary>
    public bool VersionMismatch { get; set; }

    /// <summary>實際讀到嘅版本 · The export_version actually read.</summary>
    public int FoundVersion { get; set; } = 3;
}

/// <summary>
/// 清單序列化／安裝指令／安全檢查 · Bundle (de)serialization, install-script generation,
/// and security inspection. All methods defensive — they swallow errors and return
/// safe defaults rather than throwing.
/// </summary>
public static class BundleService
{
    private const int ExpectedVersion = 3;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        // SerializableBundle exposes export_version/packages/incompatible_packages as public
        // FIELDS (per spec), so fields must be included or the JSON would be empty.
        IncludeFields = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions OptionJsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ===== 格式偵測 · Format detection by extension =====

    private enum BundleFormat { Json, Yaml, Xml }

    private static BundleFormat FormatFor(string path)
    {
        var ext = "";
        try { ext = (Path.GetExtension(path) ?? "").TrimStart('.').ToLowerInvariant(); } catch { }
        return ext switch
        {
            "yaml" or "yml" => BundleFormat.Yaml,
            "xml" => BundleFormat.Xml,
            // .json / .ubundle / anything else -> JSON
            _ => BundleFormat.Json,
        };
    }

    // ===== 由 PackageItem 建清單 · Build a bundle from PackageItems =====

    /// <summary>
    /// 將一組套件轉做清單 · Convert packages to a bundle, routing local/unknown-source items
    /// into <see cref="SerializableBundle.incompatible_packages"/>. The optional lookup attaches
    /// per-package install options. Never throws.
    /// </summary>
    public static SerializableBundle ToBundle(IEnumerable<PackageItem> items, Func<PackageItem, InstallOptions?>? optsLookup = null)
    {
        var bundle = new SerializableBundle();
        if (items is null) return bundle;
        try
        {
            foreach (var i in items)
            {
                if (i is null) continue;
                if (IsIncompatibleSource(i.Source)
                    || !TryValidatePackageReference(i.ManagerKey, i.Id, out _, out _))
                {
                    bundle.incompatible_packages.Add(new SerializableIncompatiblePackage
                    {
                        Id = i.Id ?? "",
                        Name = i.Name ?? "",
                        Version = i.Version ?? "",
                        Source = i.Source ?? "",
                    });
                }
                else
                {
                    InstallOptions? opts = null;
                    try { opts = optsLookup?.Invoke(i); } catch { opts = null; }
                    bundle.packages.Add(new SerializablePackage
                    {
                        Id = i.Id ?? "",
                        Name = i.Name ?? "",
                        Version = i.Version ?? "",
                        Source = i.Source ?? "",
                        ManagerName = i.ManagerKey ?? "",
                        InstallationOptions = opts,
                    });
                }
            }
        }
        catch { /* defensive */ }
        return bundle;
    }

    /// <summary>
    /// 來源明確標示為本機／未知 · True when a source is explicitly local / unknown / store-fallback.
    /// A blank source is normal for valid pip/npm/Cargo/.NET packages and is not incompatible by itself.
    /// </summary>
    private static bool IsIncompatibleSource(string? source)
    {
        var s = (source ?? "").Trim().ToLowerInvariant();
        return s == "local"
            || s == "local pc"
            || s == "msstore-fallback"
            || s == "unknown";
    }

    // ===== JSON =====

    /// <summary>清單 → JSON · Bundle to JSON string.</summary>
    public static string ToJson(SerializableBundle bundle)
    {
        try { return JsonSerializer.Serialize(bundle ?? new SerializableBundle(), JsonOpts); }
        catch { return "{\"export_version\":3,\"packages\":[],\"incompatible_packages\":[]}"; }
    }

    /// <summary>JSON → 清單（容錯）· JSON to bundle (tolerant). Also accepts the legacy flat array shape.</summary>
    public static SerializableBundle FromJson(string json)
    {
        var bundle = new SerializableBundle();
        if (string.IsNullOrWhiteSpace(json)) return bundle;
        try
        {
            var trimmed = json.TrimStart('﻿', ' ', '\t', '\r', '\n');
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            // 舊版扁平陣列 [{Manager,Id,Name,Version}] · legacy flat array upgrade.
            if (root.ValueKind == JsonValueKind.Array)
                return FromLegacyArray(root);

            if (root.ValueKind != JsonValueKind.Object) return bundle;

            bundle.export_version = ReadInt(root, "export_version", ExpectedVersion);

            if (root.TryGetProperty("packages", out var pkgs) && pkgs.ValueKind == JsonValueKind.Array)
                foreach (var p in pkgs.EnumerateArray())
                    bundle.packages.Add(ReadPackage(p));

            if (root.TryGetProperty("incompatible_packages", out var inc) && inc.ValueKind == JsonValueKind.Array)
                foreach (var p in inc.EnumerateArray())
                    bundle.incompatible_packages.Add(new SerializableIncompatiblePackage
                    {
                        Id = Str(p, "Id"),
                        Name = Str(p, "Name"),
                        Version = Str(p, "Version"),
                        Source = Str(p, "Source"),
                    });
        }
        catch { /* defensive — return whatever parsed */ }
        return bundle;
    }

    /// <summary>偵測舊版扁平 JSON 陣列 · Detect a legacy flat [{Manager,Id,...}] array.</summary>
    public static bool LooksLikeLegacyArray(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            var trimmed = json.TrimStart('﻿', ' ', '\t', '\r', '\n');
            using var doc = JsonDocument.Parse(trimmed);
            return doc.RootElement.ValueKind == JsonValueKind.Array;
        }
        catch { return false; }
    }

    private static SerializableBundle FromLegacyArray(JsonElement arr)
    {
        var bundle = new SerializableBundle();
        try
        {
            foreach (var el in arr.EnumerateArray())
            {
                if (el.ValueKind != JsonValueKind.Object) continue;
                var mgr = Str(el, "Manager");
                if (mgr.Length == 0) mgr = Str(el, "ManagerName");
                var src = Str(el, "Source");
                var pkg = new SerializablePackage
                {
                    Id = Str(el, "Id"),
                    Name = Str(el, "Name"),
                    Version = Str(el, "Version"),
                    Source = src,
                    ManagerName = mgr,
                };
                if (mgr.Length == 0 || IsIncompatibleSource(src))
                    bundle.incompatible_packages.Add(new SerializableIncompatiblePackage { Id = pkg.Id, Name = pkg.Name, Version = pkg.Version, Source = pkg.Source });
                else
                    bundle.packages.Add(pkg);
            }
        }
        catch { }
        return bundle;
    }

    private static SerializablePackage ReadPackage(JsonElement p)
    {
        var pkg = new SerializablePackage
        {
            Id = Str(p, "Id"),
            Name = Str(p, "Name"),
            Version = Str(p, "Version"),
            Source = Str(p, "Source"),
            ManagerName = Str(p, "ManagerName"),
        };
        try
        {
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("InstallationOptions", out var io)
                && io.ValueKind == JsonValueKind.Object)
                pkg.InstallationOptions = ReadInstallOptions(io);
        }
        catch { }
        return pkg;
    }

    private static InstallOptions ReadInstallOptions(JsonElement io)
    {
        InstallOptions o;
        try
        {
            o = JsonSerializer.Deserialize<InstallOptions>(io.GetRawText(), OptionJsonOpts)
                ?? new InstallOptions();
        }
        catch { o = new InstallOptions(); }

        ApplyLegacyJsonBool(io, o, nameof(InstallOptions.Interactive), "InteractiveInstallation");
        ApplyLegacyJsonString(io, o, nameof(InstallOptions.Scope), "InstallationScope");
        ApplyLegacyJsonString(io, o, nameof(InstallOptions.CustomArgsInstall), "CustomParameters_Install");
        ApplyLegacyJsonString(io, o, nameof(InstallOptions.CustomArgsUpdate), "CustomParameters_Update");
        ApplyLegacyJsonString(io, o, nameof(InstallOptions.CustomArgsUninstall), "CustomParameters_Uninstall");
        o.KillBeforeOperation ??= new List<string>();
        return o;
    }

    // ===== YAML（手寫，最小）· Hand-rolled minimal YAML =====

    /// <summary>清單 → YAML · Bundle to a minimal YAML document.</summary>
    public static string ToYaml(SerializableBundle bundle)
    {
        var b = new StringBuilder();
        try
        {
            bundle ??= new SerializableBundle();
            b.Append("export_version: ").Append(bundle.export_version).Append('\n');
            b.Append("packages:\n");
            if (bundle.packages.Count == 0) { /* empty list */ }
            foreach (var p in bundle.packages)
            {
                b.Append("  - Id: ").Append(Yq(p.Id)).Append('\n');
                b.Append("    Name: ").Append(Yq(p.Name)).Append('\n');
                b.Append("    Version: ").Append(Yq(p.Version)).Append('\n');
                b.Append("    Source: ").Append(Yq(p.Source)).Append('\n');
                b.Append("    ManagerName: ").Append(Yq(p.ManagerName)).Append('\n');
                if (p.InstallationOptions is not null)
                    AppendYamlOptions(b, p.InstallationOptions);
            }
            b.Append("incompatible_packages:\n");
            foreach (var p in bundle.incompatible_packages)
            {
                b.Append("  - Id: ").Append(Yq(p.Id)).Append('\n');
                b.Append("    Name: ").Append(Yq(p.Name)).Append('\n');
                b.Append("    Version: ").Append(Yq(p.Version)).Append('\n');
                b.Append("    Source: ").Append(Yq(p.Source)).Append('\n');
            }
        }
        catch { }
        return b.ToString();
    }

    private static void AppendYamlOptions(StringBuilder b, InstallOptions o)
    {
        b.Append("    InstallationOptions:\n");
        foreach (var (name, val) in BoolOptions(o))
            b.Append("      ").Append(name).Append(": ").Append(val ? "true" : "false").Append('\n');
        foreach (var (name, val) in StringOptions(o))
            if (!string.IsNullOrEmpty(val))
                b.Append("      ").Append(name).Append(": ").Append(Yq(val)).Append('\n');
        foreach (var (name, list) in ListOptions(o))
            if (list.Count > 0)
            {
                b.Append("      ").Append(name).Append(":\n");
                foreach (var item in list)
                    b.Append("        - ").Append(Yq(item)).Append('\n');
            }
    }

    /// <summary>YAML → 清單（容錯，只認得本服務寫出嘅縮排格式）· Tolerant YAML reader for our own layout.</summary>
    public static SerializableBundle FromYaml(string yaml)
    {
        var bundle = new SerializableBundle();
        if (string.IsNullOrWhiteSpace(yaml)) return bundle;
        try
        {
            var lines = yaml.Replace("\r", "").Split('\n');
            string section = "";              // packages | incompatible_packages
            SerializablePackage? cur = null;
            SerializableIncompatiblePackage? curInc = null;
            InstallOptions? curOpts = null;
            string optsList = "";             // when inside a list-valued option

            void Flush()
            {
                if (cur is not null) bundle.packages.Add(cur);
                if (curInc is not null) bundle.incompatible_packages.Add(curInc);
                cur = null; curInc = null; curOpts = null; optsList = "";
            }

            // 縮排規格 · indent layout written by ToYaml:
            //   0  export_version / packages / incompatible_packages
            //   2  "- Id: ..." (start of a package/incompatible item)
            //   4  package fields + "InstallationOptions:"
            //   6  option scalars + list-option headers ("KillBeforeOperation:")
            //   8  "- value" entries of a list-valued option
            foreach (var raw in lines)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var indent = raw.Length - raw.TrimStart(' ').Length;
                var line = raw.Trim();

                // top-level keys / section headers
                if (indent == 0)
                {
                    Flush();
                    if (line.StartsWith("export_version:"))
                        bundle.export_version = ParseIntSafe(After(line, ':'), ExpectedVersion);
                    else if (line.StartsWith("packages:")) section = "packages";
                    else if (line.StartsWith("incompatible_packages:")) section = "incompatible_packages";
                    continue;
                }

                // an option-list entry ("- value") lives DEEPER than a package field (indent >= 6).
                if (curOpts is not null && optsList.Length > 0 && indent >= 6 && line.StartsWith("- "))
                {
                    AddToOptionList(curOpts, optsList, Unq(line.Substring(2).Trim()));
                    continue;
                }

                // start of a new package / incompatible item ("- Id: ...") at indent 2.
                if (indent <= 2 && line.StartsWith("- "))
                {
                    Flush();
                    optsList = "";
                    var inner = line.Substring(2).Trim();
                    if (section == "incompatible_packages")
                    {
                        curInc = new SerializableIncompatiblePackage();
                        ApplyIncField(curInc, inner);
                    }
                    else
                    {
                        cur = new SerializablePackage();
                        ApplyPkgField(cur, inner);
                    }
                    continue;
                }

                // start of the InstallationOptions block (indent 4).
                if (line.StartsWith("InstallationOptions:") && cur is not null)
                {
                    curOpts = new InstallOptions();
                    cur.InstallationOptions = curOpts;
                    optsList = "";
                    continue;
                }

                // inside an InstallationOptions block (indent >= 6): scalar or list-header.
                if (curOpts is not null && indent >= 6)
                {
                    var (k2, v2) = SplitKv(line);
                    if (k2.Length == 0) continue;
                    if (v2.Length == 0)
                        optsList = k2;            // list-valued option header
                    else
                    {
                        optsList = "";
                        ApplyOptionScalar(curOpts, k2, Unq(v2));
                    }
                    continue;
                }

                // otherwise a plain package / incompatible field (indent 4).
                if (curInc is not null) ApplyIncField(curInc, line);
                else if (cur is not null) ApplyPkgField(cur, line);
            }
            Flush();
        }
        catch { }
        return bundle;
    }

    private static void ApplyPkgField(SerializablePackage p, string kv)
    {
        var (k, v) = SplitKv(kv);
        v = Unq(v);
        switch (k)
        {
            case "Id": p.Id = v; break;
            case "Name": p.Name = v; break;
            case "Version": p.Version = v; break;
            case "Source": p.Source = v; break;
            case "ManagerName": p.ManagerName = v; break;
        }
    }

    private static void ApplyIncField(SerializableIncompatiblePackage p, string kv)
    {
        var (k, v) = SplitKv(kv);
        v = Unq(v);
        switch (k)
        {
            case "Id": p.Id = v; break;
            case "Name": p.Name = v; break;
            case "Version": p.Version = v; break;
            case "Source": p.Source = v; break;
        }
    }

    // ===== XML（System.Xml.Linq）· XML via System.Xml.Linq =====

    /// <summary>清單 → XML · Bundle to XML.</summary>
    public static string ToXml(SerializableBundle bundle)
    {
        try
        {
            bundle ??= new SerializableBundle();
            var root = new XElement("Bundle",
                new XElement("export_version", bundle.export_version));

            var pkgs = new XElement("packages");
            foreach (var p in bundle.packages)
            {
                var el = new XElement("Package",
                    new XElement("Id", p.Id ?? ""),
                    new XElement("Name", p.Name ?? ""),
                    new XElement("Version", p.Version ?? ""),
                    new XElement("Source", p.Source ?? ""),
                    new XElement("ManagerName", p.ManagerName ?? ""));
                if (p.InstallationOptions is not null)
                    el.Add(OptionsToXml(p.InstallationOptions));
                pkgs.Add(el);
            }
            root.Add(pkgs);

            var inc = new XElement("incompatible_packages");
            foreach (var p in bundle.incompatible_packages)
                inc.Add(new XElement("Package",
                    new XElement("Id", p.Id ?? ""),
                    new XElement("Name", p.Name ?? ""),
                    new XElement("Version", p.Version ?? ""),
                    new XElement("Source", p.Source ?? "")));
            root.Add(inc);

            return new XDocument(new XDeclaration("1.0", "utf-8", null), root).ToString();
        }
        catch { return "<Bundle><export_version>3</export_version><packages /><incompatible_packages /></Bundle>"; }
    }

    private static XElement OptionsToXml(InstallOptions o)
    {
        var el = new XElement("InstallationOptions");
        foreach (var (name, val) in BoolOptions(o))
            el.Add(new XElement(name, val ? "true" : "false"));
        foreach (var (name, val) in StringOptions(o))
            if (!string.IsNullOrEmpty(val))
                el.Add(new XElement(name, val));
        foreach (var (name, list) in ListOptions(o))
            if (list.Count > 0)
            {
                var listEl = new XElement(name);
                foreach (var item in list) listEl.Add(new XElement("item", item));
                el.Add(listEl);
            }
        return el;
    }

    /// <summary>XML → 清單（容錯）· Tolerant XML reader.</summary>
    public static SerializableBundle FromXml(string xml)
    {
        var bundle = new SerializableBundle();
        if (string.IsNullOrWhiteSpace(xml)) return bundle;
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root is null) return bundle;

            bundle.export_version = ParseIntSafe(root.Element("export_version")?.Value, ExpectedVersion);

            var pkgs = root.Element("packages");
            if (pkgs is not null)
                foreach (var el in pkgs.Elements("Package"))
                {
                    var pkg = new SerializablePackage
                    {
                        Id = el.Element("Id")?.Value ?? "",
                        Name = el.Element("Name")?.Value ?? "",
                        Version = el.Element("Version")?.Value ?? "",
                        Source = el.Element("Source")?.Value ?? "",
                        ManagerName = el.Element("ManagerName")?.Value ?? "",
                    };
                    var io = el.Element("InstallationOptions");
                    if (io is not null) pkg.InstallationOptions = OptionsFromXml(io);
                    bundle.packages.Add(pkg);
                }

            var inc = root.Element("incompatible_packages");
            if (inc is not null)
                foreach (var el in inc.Elements("Package"))
                    bundle.incompatible_packages.Add(new SerializableIncompatiblePackage
                    {
                        Id = el.Element("Id")?.Value ?? "",
                        Name = el.Element("Name")?.Value ?? "",
                        Version = el.Element("Version")?.Value ?? "",
                        Source = el.Element("Source")?.Value ?? "",
                    });
        }
        catch { }
        return bundle;
    }

    private static InstallOptions OptionsFromXml(XElement io)
    {
        var o = new InstallOptions();
        try
        {
            foreach (var option in io.Elements())
            {
                var name = CanonicalOptionName(option.Name.LocalName);
                if (IsBoolOption(name))
                {
                    SetBool(o, name, string.Equals(option.Value.Trim(), "true", StringComparison.OrdinalIgnoreCase));
                    continue;
                }
                if (IsListOption(name))
                {
                    var items = option.Elements("item").Select(x => x.Value ?? "").Where(x => x.Length > 0).ToList();
                    if (items.Count == 0 && !string.IsNullOrWhiteSpace(option.Value)) items.Add(option.Value);
                    SetList(o, name, items);
                    continue;
                }
                if (IsStringOption(name))
                {
                    var items = option.Elements("item").Select(x => x.Value ?? "").Where(x => x.Length > 0).ToList();
                    SetStr(o, name, items.Count > 0 ? string.Join(" ", items) : option.Value);
                }
            }
        }
        catch { }
        o.KillBeforeOperation ??= new List<string>();
        return o;
    }

    // ===== 儲存／載入（按副檔名）· Save / load by extension =====

    /// <summary>儲存清單到檔案，格式按副檔名 · Save a bundle, format chosen by file extension. Never throws.</summary>
    public static async Task SaveAsync(SerializableBundle bundle, string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string text = FormatFor(path) switch
            {
                BundleFormat.Yaml => ToYaml(bundle),
                BundleFormat.Xml => ToXml(bundle),
                _ => ToJson(bundle),
            };
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
        }
        catch { /* defensive — never throw */ }
    }

    /// <summary>由檔案載入清單，格式按副檔名 · Load a bundle, format chosen by extension. Never throws.</summary>
    public static async Task<BundleLoadResult> LoadAsync(string path)
    {
        var result = new BundleLoadResult();
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return result;
            var text = await File.ReadAllTextAsync(path);
            result.Bundle = FormatFor(path) switch
            {
                BundleFormat.Yaml => FromYaml(text),
                BundleFormat.Xml => FromXml(text),
                _ => FromJson(text),
            };
            result.FoundVersion = result.Bundle.export_version;
            result.VersionMismatch = result.Bundle.export_version != ExpectedVersion;
        }
        catch { /* defensive */ }
        return result;
    }

    // ===== 每管理器安裝指令對應 · Per-manager install command mapping =====

    /// <summary>
    /// 為一個套件砌出安裝指令（同 app 內 IPackageManager.InstallAsync 一致）。
    /// Build the install command line for one package, mirroring what the app's
    /// per-manager InstallAsync drivers run. Returns "" if the manager is unknown.
    /// </summary>
    public static string InstallCommandFor(string managerKey, string id, InstallOptions? opts = null)
    {
        try
        {
            if (!TryValidatePackageReference(managerKey, id, out var key, out var packageId)) return "";
            var effective = opts ?? new InstallOptions();
            if (!TryValidateStructuredOptions(effective, out _)) return "";
            var command = PackageOperations.BuildCommandPreview(
                key, packageId, PackageOperations.Op.Install, effective);
            if (string.IsNullOrWhiteSpace(command) || command.StartsWith('#')) return "";
            if (key is "psgallery" or "pwsh7")
            {
                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
                var shell = key == "pwsh7" ? "pwsh" : "powershell";
                return $"{shell} -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}";
            }
            return command;
        }
        catch { return ""; }
    }

    // ===== 安裝指令稿（.ps1）· Standalone PowerShell install script =====

    /// <summary>
    /// 由清單產生獨立 .ps1 · Generate a standalone PowerShell script that installs every
    /// compatible package using the correct manager command, prints progress and a final
    /// success/fail summary. Incompatible packages are listed but not installed. Never throws.
    /// </summary>
    public static string GenerateInstallScript(SerializableBundle bundle)
    {
        var b = new StringBuilder();
        try
        {
            bundle ??= new SerializableBundle();
            var entries = new List<(string name, string cmd)>();
            foreach (var p in bundle.packages)
            {
                var cmd = InstallCommandFor(p.ManagerName, p.Id, p.InstallationOptions);
                if (cmd.Length == 0) continue;
                entries.Add((string.IsNullOrEmpty(p.Name) ? p.Id : p.Name, cmd));
            }

            b.Append("# ============================================================\n");
            b.Append("#  WinForge 套件清單安裝指令稿 · WinForge bundle install script\n");
            b.Append("#  自動產生 · Auto-generated. Run elevated if installs need admin.\n");
            b.Append("# ============================================================\n");
            b.Append("$ErrorActionPreference = 'Continue'\n");
            b.Append("Write-Host ''\n");
            b.Append("Write-Host '======================================================='\n");
            b.Append("Write-Host '   WinForge Package Installer · WinForge 套件安裝器'\n");
            b.Append("Write-Host '======================================================='\n");
            b.Append("Write-Host ''\n");
            b.Append("Write-Host 'NOTES · 注意:' -ForegroundColor Yellow\n");
            b.Append("Write-Host '  - Some packages may require elevation · 部分套件可能需要系統管理員權限' -ForegroundColor Yellow\n");
            b.Append("Write-Host '  - Error/success detection may not be exact · 成功／失敗判斷未必完全準確' -ForegroundColor Yellow\n");
            b.Append("Write-Host ''\n");

            // incompatible list (logged only)
            if (bundle.incompatible_packages.Count > 0)
            {
                b.Append("Write-Host 'Skipped (incompatible) · 已略過（不相容）:' -ForegroundColor DarkYellow\n");
                foreach (var p in bundle.incompatible_packages)
                    b.Append("Write-Host '  - ").Append(PsLit(string.IsNullOrEmpty(p.Name) ? p.Id : p.Name)).Append("'\n");
                b.Append("Write-Host ''\n");
            }

            b.Append("Write-Host 'The following packages will be installed · 將會安裝以下套件:'\n");
            foreach (var (name, _) in entries)
                b.Append("Write-Host '  - ").Append(PsLit(name)).Append("'\n");
            b.Append("Write-Host ''\n");

            b.Append("$success_count = 0\n");
            b.Append("$failure_count = 0\n");
            b.Append("$packages = @(\n");
            for (int i = 0; i < entries.Count; i++)
            {
                b.Append("    @{ Name = '").Append(PsLit(entries[i].name)).Append("'; Command = '")
                    .Append(PsLit(entries[i].cmd)).Append("' }");
                b.Append(i < entries.Count - 1 ? ",\n" : "\n");
            }
            b.Append(")\n\n");

            b.Append("foreach ($package in $packages) {\n");
            b.Append("    $command = $package.Command\n");
            b.Append("    Write-Host \"Installing · 安裝緊: $($package.Name)\" -ForegroundColor Yellow\n");
            b.Append("    cmd.exe /C $command\n");
            b.Append("    if ($LASTEXITCODE -eq 0) {\n");
            b.Append("        Write-Host \"[  OK  ] $($package.Name)\" -ForegroundColor Green\n");
            b.Append("        $success_count++\n");
            b.Append("    } else {\n");
            b.Append("        Write-Host \"[ FAIL ] $($package.Name)\" -ForegroundColor Red\n");
            b.Append("        $failure_count++\n");
            b.Append("    }\n");
            b.Append("    Write-Host ''\n");
            b.Append("}\n\n");

            b.Append("Write-Host '======================================================='\n");
            b.Append("Write-Host '   Summary · 總結'\n");
            b.Append("Write-Host '======================================================='\n");
            b.Append("Write-Host \"Successful · 成功: $success_count\"\n");
            b.Append("Write-Host \"Failed · 失敗: $failure_count\"\n");
            b.Append("if ($failure_count -gt 0) {\n");
            b.Append("    Write-Host 'Some packages failed · 部分套件安裝失敗' -ForegroundColor Yellow\n");
            b.Append("} else {\n");
            b.Append("    Write-Host 'All packages installed · 全部套件已安裝' -ForegroundColor Green\n");
            b.Append("}\n");
            b.Append("exit $failure_count\n");
        }
        catch { }
        return b.ToString();
    }

    // ===== 安全檢查 · Security inspection =====

    /// <summary>
    /// 檢查清單入面危險嘅安裝選項 · Flag packages whose install options carry custom CLI args,
    /// pre/post commands, or kill-lists — the fields that can run arbitrary code. Bilingual.
    /// Never throws.
    /// </summary>
    public static BundleSecurityReport Inspect(SerializableBundle bundle)
    {
        var report = new BundleSecurityReport();
        try
        {
            if (bundle is null) return report;
            foreach (var p in bundle.packages)
            {
                var name = string.IsNullOrEmpty(p.Name) ? p.Id : p.Name;

                if (!TryValidatePackageReference(p.ManagerName, p.Id, out _, out _))
                    report.Warnings.Add(new LocalizedText(
                        $"“{name}” has an invalid manager or package ID and will not be scripted.",
                        $"「{name}」嘅管理器或套件 ID 無效，唔會寫入安裝指令稿。"));

                var o = p.InstallationOptions;
                if (o is null) continue;

                if (!TryValidateStructuredOptions(o, out var optionError))
                    report.Warnings.Add(new LocalizedText(
                        $"“{name}” has unsafe structured install options ({optionError}) and will not be scripted or queued.",
                        $"「{name}」嘅結構化安裝選項唔安全（{optionError}），唔會寫入指令稿或者排隊。"));

                // 自訂 CLI 參數 · custom CLI args
                foreach (var key in new[]
                {
                    nameof(InstallOptions.CustomArgsInstall), nameof(InstallOptions.CustomArgsUpdate),
                    nameof(InstallOptions.CustomArgsUninstall),
                })
                {
                    var value = ReadString(o, key);
                    if (!string.IsNullOrWhiteSpace(value))
                        report.Warnings.Add(new LocalizedText(
                            $"“{name}” carries custom CLI arguments ({key}); review the bundle source before running it.",
                            $"「{name}」帶有自訂命令列參數（{key}）；執行之前請檢查清單來源。"));
                }

                // 前／後置指令 · pre/post commands
                foreach (var key in new[] { "PreInstallCommand", "PostInstallCommand", "PreUpdateCommand", "PostUpdateCommand", "PreUninstallCommand", "PostUninstallCommand" })
                {
                    var cmd = ReadString(o, key);
                    if (!string.IsNullOrWhiteSpace(cmd))
                        report.Warnings.Add(new LocalizedText(
                            $"“{name}” runs a custom command ({key}); its contents are hidden to avoid exposing embedded secrets.",
                            $"「{name}」會執行自訂指令（{key}）；內容已隱藏，避免洩漏內嵌機密。"));
                }

                // kill-list
                var kill = ReadStringList(o, "KillBeforeOperation");
                if (kill.Count > 0)
                    report.Warnings.Add(new LocalizedText(
                        $"“{name}” will terminate processes before installing ({kill.Count} name(s)); inspect the bundle before continuing.",
                        $"「{name}」安裝前會終止 {kill.Count} 個程序名稱；繼續之前請檢查清單。"));
            }
        }
        catch { }
        return report;
    }

    /// <summary>套件有冇附帶安裝選項 · Whether a package carries any non-trivial install options.</summary>
    public static bool HasOptions(SerializablePackage p)
    {
        try
        {
            var o = p?.InstallationOptions;
            if (o is null) return false;
            foreach (var (_, v) in BoolOptions(o)) if (v) return true;
            foreach (var (_, v) in StringOptions(o)) if (!string.IsNullOrEmpty(v)) return true;
            foreach (var (_, l) in ListOptions(o)) if (l.Count > 0) return true;
        }
        catch { }
        return false;
    }

    // ===== InstallOptions schema + legacy aliases · 安裝選項結構與舊名兼容 =====

    private static readonly string[] BoolNames =
    {
        nameof(InstallOptions.RunAsAdministrator), nameof(InstallOptions.Interactive),
        nameof(InstallOptions.SkipHashCheck), nameof(InstallOptions.PreRelease),
        nameof(InstallOptions.RemoveDataOnUninstall), nameof(InstallOptions.UninstallPreviousOnUpdate),
        nameof(InstallOptions.SkipMinorUpdates), nameof(InstallOptions.AutoUpdate),
        nameof(InstallOptions.AbortOnPreInstallFail), nameof(InstallOptions.AbortOnPreUpdateFail),
        nameof(InstallOptions.AbortOnPreUninstallFail), nameof(InstallOptions.ForceKill),
    };
    private static readonly string[] StringNames =
    {
        nameof(InstallOptions.CustomArgsInstall), nameof(InstallOptions.CustomArgsUpdate),
        nameof(InstallOptions.CustomArgsUninstall), nameof(InstallOptions.Scope),
        nameof(InstallOptions.Architecture), nameof(InstallOptions.Version),
        nameof(InstallOptions.CustomInstallLocation), nameof(InstallOptions.PreInstallCommand),
        nameof(InstallOptions.PostInstallCommand), nameof(InstallOptions.PreUpdateCommand),
        nameof(InstallOptions.PostUpdateCommand), nameof(InstallOptions.PreUninstallCommand),
        nameof(InstallOptions.PostUninstallCommand),
    };
    private static readonly string[] ListNames =
    {
        nameof(InstallOptions.KillBeforeOperation),
    };

    private static readonly IReadOnlyDictionary<string, string> LegacyOptionAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["InteractiveInstallation"] = nameof(InstallOptions.Interactive),
            ["InstallationScope"] = nameof(InstallOptions.Scope),
            ["CustomParameters_Install"] = nameof(InstallOptions.CustomArgsInstall),
            ["CustomParameters_Update"] = nameof(InstallOptions.CustomArgsUpdate),
            ["CustomParameters_Uninstall"] = nameof(InstallOptions.CustomArgsUninstall),
        };

    private static IEnumerable<(string name, bool val)> BoolOptions(InstallOptions o)
    {
        foreach (var n in BoolNames) yield return (n, ReadBool(o, n));
    }
    private static IEnumerable<(string name, string val)> StringOptions(InstallOptions o)
    {
        foreach (var n in StringNames) yield return (n, ReadString(o, n));
    }
    private static IEnumerable<(string name, List<string> val)> ListOptions(InstallOptions o)
    {
        foreach (var n in ListNames) yield return (n, ReadStringList(o, n));
    }

    private static bool ReadBool(InstallOptions o, string prop)
    {
        return prop switch
        {
            nameof(InstallOptions.RunAsAdministrator) => o.RunAsAdministrator,
            nameof(InstallOptions.Interactive) => o.Interactive,
            nameof(InstallOptions.SkipHashCheck) => o.SkipHashCheck,
            nameof(InstallOptions.PreRelease) => o.PreRelease,
            nameof(InstallOptions.RemoveDataOnUninstall) => o.RemoveDataOnUninstall,
            nameof(InstallOptions.UninstallPreviousOnUpdate) => o.UninstallPreviousOnUpdate,
            nameof(InstallOptions.SkipMinorUpdates) => o.SkipMinorUpdates,
            nameof(InstallOptions.AutoUpdate) => o.AutoUpdate,
            nameof(InstallOptions.AbortOnPreInstallFail) => o.AbortOnPreInstallFail,
            nameof(InstallOptions.AbortOnPreUpdateFail) => o.AbortOnPreUpdateFail,
            nameof(InstallOptions.AbortOnPreUninstallFail) => o.AbortOnPreUninstallFail,
            nameof(InstallOptions.ForceKill) => o.ForceKill,
            _ => false,
        };
    }
    private static string ReadString(InstallOptions o, string prop)
    {
        return prop switch
        {
            nameof(InstallOptions.CustomArgsInstall) => o.CustomArgsInstall ?? "",
            nameof(InstallOptions.CustomArgsUpdate) => o.CustomArgsUpdate ?? "",
            nameof(InstallOptions.CustomArgsUninstall) => o.CustomArgsUninstall ?? "",
            nameof(InstallOptions.Scope) => o.Scope ?? "",
            nameof(InstallOptions.Architecture) => o.Architecture ?? "",
            nameof(InstallOptions.Version) => o.Version ?? "",
            nameof(InstallOptions.CustomInstallLocation) => o.CustomInstallLocation ?? "",
            nameof(InstallOptions.PreInstallCommand) => o.PreInstallCommand ?? "",
            nameof(InstallOptions.PostInstallCommand) => o.PostInstallCommand ?? "",
            nameof(InstallOptions.PreUpdateCommand) => o.PreUpdateCommand ?? "",
            nameof(InstallOptions.PostUpdateCommand) => o.PostUpdateCommand ?? "",
            nameof(InstallOptions.PreUninstallCommand) => o.PreUninstallCommand ?? "",
            nameof(InstallOptions.PostUninstallCommand) => o.PostUninstallCommand ?? "",
            _ => "",
        };
    }
    private static List<string> ReadStringList(InstallOptions o, string prop)
    {
        return prop == nameof(InstallOptions.KillBeforeOperation)
            ? new List<string>(o.KillBeforeOperation ?? new List<string>())
            : new List<string>();
    }
    private static void SetBool(InstallOptions o, string prop, bool val)
    {
        switch (prop)
        {
            case nameof(InstallOptions.RunAsAdministrator): o.RunAsAdministrator = val; break;
            case nameof(InstallOptions.Interactive): o.Interactive = val; break;
            case nameof(InstallOptions.SkipHashCheck): o.SkipHashCheck = val; break;
            case nameof(InstallOptions.PreRelease): o.PreRelease = val; break;
            case nameof(InstallOptions.RemoveDataOnUninstall): o.RemoveDataOnUninstall = val; break;
            case nameof(InstallOptions.UninstallPreviousOnUpdate): o.UninstallPreviousOnUpdate = val; break;
            case nameof(InstallOptions.SkipMinorUpdates): o.SkipMinorUpdates = val; break;
            case nameof(InstallOptions.AutoUpdate): o.AutoUpdate = val; break;
            case nameof(InstallOptions.AbortOnPreInstallFail): o.AbortOnPreInstallFail = val; break;
            case nameof(InstallOptions.AbortOnPreUpdateFail): o.AbortOnPreUpdateFail = val; break;
            case nameof(InstallOptions.AbortOnPreUninstallFail): o.AbortOnPreUninstallFail = val; break;
            case nameof(InstallOptions.ForceKill): o.ForceKill = val; break;
        }
    }
    private static void SetStr(InstallOptions o, string prop, string val)
    {
        val ??= "";
        switch (prop)
        {
            case nameof(InstallOptions.CustomArgsInstall): o.CustomArgsInstall = val; break;
            case nameof(InstallOptions.CustomArgsUpdate): o.CustomArgsUpdate = val; break;
            case nameof(InstallOptions.CustomArgsUninstall): o.CustomArgsUninstall = val; break;
            case nameof(InstallOptions.Scope): o.Scope = val; break;
            case nameof(InstallOptions.Architecture): o.Architecture = val; break;
            case nameof(InstallOptions.Version): o.Version = val; break;
            case nameof(InstallOptions.CustomInstallLocation): o.CustomInstallLocation = val; break;
            case nameof(InstallOptions.PreInstallCommand): o.PreInstallCommand = val; break;
            case nameof(InstallOptions.PostInstallCommand): o.PostInstallCommand = val; break;
            case nameof(InstallOptions.PreUpdateCommand): o.PreUpdateCommand = val; break;
            case nameof(InstallOptions.PostUpdateCommand): o.PostUpdateCommand = val; break;
            case nameof(InstallOptions.PreUninstallCommand): o.PreUninstallCommand = val; break;
            case nameof(InstallOptions.PostUninstallCommand): o.PostUninstallCommand = val; break;
        }
    }
    private static void SetList(InstallOptions o, string prop, List<string> val)
    {
        if (prop == nameof(InstallOptions.KillBeforeOperation)) o.KillBeforeOperation = val ?? new List<string>();
    }
    private static string CanonicalOptionName(string key)
    {
        key = (key ?? "").Trim();
        if (LegacyOptionAliases.TryGetValue(key, out var canonical)) return canonical;
        foreach (var name in BoolNames) if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) return name;
        foreach (var name in StringNames) if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) return name;
        foreach (var name in ListNames) if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) return name;
        return "";
    }

    private static bool IsBoolOption(string name) => BoolNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    private static bool IsStringOption(string name) => StringNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
    private static bool IsListOption(string name) => ListNames.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase));

    private static void ApplyOptionScalar(InstallOptions o, string key, string val)
    {
        key = CanonicalOptionName(key);
        if (IsBoolOption(key)) SetBool(o, key, string.Equals(val.Trim(), "true", StringComparison.OrdinalIgnoreCase));
        else if (IsStringOption(key)) SetStr(o, key, val);
        else if (IsListOption(key) && val.Length > 0) SetList(o, key, new List<string> { val });
    }

    private static void AddToOptionList(InstallOptions o, string key, string val)
    {
        key = CanonicalOptionName(key);
        if (val.Length == 0 || key.Length == 0) return;
        if (IsListOption(key))
        {
            var list = ReadStringList(o, key);
            list.Add(val);
            SetList(o, key, list);
        }
        else if (key is nameof(InstallOptions.CustomArgsInstall)
                 or nameof(InstallOptions.CustomArgsUpdate)
                 or nameof(InstallOptions.CustomArgsUninstall))
        {
            var current = ReadString(o, key);
            SetStr(o, key, current.Length == 0 ? val : current + " " + val);
        }
    }

    private static void ApplyLegacyJsonBool(JsonElement io, InstallOptions o, string canonical, string legacy)
    {
        if (HasJsonProperty(io, canonical) || !TryGetJsonProperty(io, legacy, out var value)) return;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) SetBool(o, canonical, value.GetBoolean());
        else if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed)) SetBool(o, canonical, parsed);
    }

    private static void ApplyLegacyJsonString(JsonElement io, InstallOptions o, string canonical, string legacy)
    {
        if (HasJsonProperty(io, canonical) || !TryGetJsonProperty(io, legacy, out var value)) return;
        if (value.ValueKind == JsonValueKind.String) { SetStr(o, canonical, value.GetString() ?? ""); return; }
        if (value.ValueKind == JsonValueKind.Array)
        {
            var parts = value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? "").Where(x => x.Length > 0);
            SetStr(o, canonical, string.Join(" ", parts));
        }
    }

    private static bool HasJsonProperty(JsonElement obj, string name) => TryGetJsonProperty(obj, name, out _);

    private static bool TryGetJsonProperty(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.ValueKind == JsonValueKind.Object)
            foreach (var property in obj.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                { value = property.Value; return true; }
        value = default;
        return false;
    }

    // ===== 小工具 · Small helpers =====

    private static readonly HashSet<string> SupportedManagerKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "winget", "scoop", "choco", "pip", "npm", "dotnet",
        "psgallery", "pwsh7", "cargo", "bun", "vcpkg",
    };

    private static bool TryValidatePackageReference(
        string? managerKey, string? id, out string normalizedManager, out string normalizedId)
    {
        normalizedManager = (managerKey ?? "").Trim().ToLowerInvariant();
        normalizedId = (id ?? "").Trim();
        if (!SupportedManagerKeys.Contains(normalizedManager) || normalizedId.Length is < 1 or > 256) return false;
        return normalizedManager switch
        {
            "npm" or "bun" => IsSafeNpmPackageId(normalizedId),
            "scoop" => IsSafeScoopPackageId(normalizedId),
            "vcpkg" => IsSafeVcpkgPackageId(normalizedId),
            _ => IsSafePackageSegment(normalizedId, false),
        };
    }

    private static bool TryValidateStructuredOptions(InstallOptions options, out string error)
    {
        error = "";
        var version = (options.Version ?? "").Trim();
        if (version.Length > 128 || version.Any(c => !(char.IsLetterOrDigit(c)
                || c is '.' or '_' or '-' or '+' or ':' or '!' or '~' or '*')))
        {
            error = "version";
            return false;
        }

        var scope = (options.Scope ?? "").Trim().ToLowerInvariant();
        if (scope.Length > 0 && scope is not ("user" or "machine" or "currentuser" or "allusers"))
        {
            error = "scope";
            return false;
        }

        var architecture = (options.Architecture ?? "").Trim().ToLowerInvariant();
        if (architecture.Length > 0 && architecture is not ("x64" or "x86" or "arm64" or "arm" or "neutral"))
        {
            error = "architecture";
            return false;
        }

        var location = (options.CustomInstallLocation ?? "").Trim();
        if (location.Length > 1024 || location.Any(char.IsControl)
            || location.IndexOfAny(new[] { '"', '`', '%', '!', '&', '|', '<', '>' }) >= 0)
        {
            error = "install location";
            return false;
        }
        return true;
    }

    private static bool IsSafeNpmPackageId(string id)
    {
        if (!id.StartsWith('@')) return !id.Contains('/') && IsSafePackageSegment(id, true);
        int slash = id.IndexOf('/');
        return slash > 1 && slash == id.LastIndexOf('/') && slash < id.Length - 1
            && IsSafePackageSegment(id.Substring(1, slash - 1), true)
            && IsSafePackageSegment(id[(slash + 1)..], true);
    }

    private static bool IsSafeScoopPackageId(string id)
    {
        var parts = id.Split('/');
        return parts.Length is 1 or 2 && parts.All(x => IsSafePackageSegment(x, false));
    }

    private static bool IsSafeVcpkgPackageId(string id)
    {
        var target = id.Split(':');
        if (target.Length > 2 || target.Length == 2 && !IsSafePackageSegment(target[1], false)) return false;
        var spec = target[0];
        int open = spec.IndexOf('['), close = spec.IndexOf(']');
        if (open < 0 && close < 0) return IsSafePackageSegment(spec, false);
        if (open <= 0 || close != spec.Length - 1 || close <= open + 1
            || open != spec.LastIndexOf('[') || close != spec.LastIndexOf(']')) return false;
        if (!IsSafePackageSegment(spec[..open], false)) return false;
        return spec.Substring(open + 1, close - open - 1).Split(',').All(x => IsSafePackageSegment(x, false));
    }

    private static bool IsSafePackageSegment(string value, bool allowTilde)
    {
        if (string.IsNullOrEmpty(value) || !IsAsciiAlphaNumeric(value[0])) return false;
        foreach (var c in value)
        {
            if (IsAsciiAlphaNumeric(c) || c is '.' or '_' or '-' or '+' || allowTilde && c == '~') continue;
            return false;
        }
        return true;
    }

    private static bool IsAsciiAlphaNumeric(char c)
        => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    private static int ReadInt(JsonElement obj, string prop, int fallback)
    {
        try
        {
            if (obj.TryGetProperty(prop, out var v))
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n)) return n;
                if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var m)) return m;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return (int)d;
            }
        }
        catch { }
        return fallback;
    }

    private static string Str(JsonElement el, string prop)
    {
        try
        {
            if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v))
                return v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString() ?? "",
                    JsonValueKind.Number => v.ToString(),
                    _ => "",
                };
        }
        catch { }
        return "";
    }

    private static int ParseIntSafe(string? s, int fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        s = s.Trim();
        if (int.TryParse(s, out var n)) return n;
        if (double.TryParse(s, out var d)) return (int)d;
        return fallback;
    }

    /// <summary>PowerShell 單引號字串內跳脫 · Escape for a PowerShell single-quoted literal.</summary>
    private static string PsLit(string? s) => (s ?? "").Replace("'", "''").Replace("\r", "").Replace("\n", " ");

    /// <summary>YAML 標量加引號（需要時）· Quote a YAML scalar when needed.</summary>
    private static string Yq(string? s)
    {
        s ??= "";
        if (s.Length == 0) return "\"\"";
        bool needs = s.IndexOfAny(new[] { ':', '#', '\'', '"', '\r', '\n', '\t', '-', '{', '}', '[', ']', ',', '&', '*', '?', '|', '>', '%', '@', '`' }) >= 0
                     || s.StartsWith(" ") || s.EndsWith(" ");
        if (!needs) return s;
        var b = new StringBuilder(s.Length + 8).Append('"');
        foreach (var c in s)
            switch (c)
            {
                case '\\': b.Append("\\\\"); break;
                case '"': b.Append("\\\""); break;
                case '\r': b.Append("\\r"); break;
                case '\n': b.Append("\\n"); break;
                case '\t': b.Append("\\t"); break;
                default: b.Append(c); break;
            }
        return b.Append('"').ToString();
    }

    /// <summary>去掉 YAML 引號 · Unquote a YAML scalar.</summary>
    private static string Unq(string? s)
    {
        s = (s ?? "").Trim();
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
        {
            var inner = s.Substring(1, s.Length - 2);
            var b = new StringBuilder(inner.Length);
            for (int i = 0; i < inner.Length; i++)
            {
                var c = inner[i];
                if (c != '\\' || i + 1 >= inner.Length) { b.Append(c); continue; }
                var escaped = inner[++i];
                b.Append(escaped switch { 'r' => '\r', 'n' => '\n', 't' => '\t', '"' => '"', '\\' => '\\', _ => escaped });
            }
            return b.ToString();
        }
        if (s.Length >= 2 && s[0] == '\'' && s[^1] == '\'')
            return s.Substring(1, s.Length - 2).Replace("''", "'");
        return s;
    }

    private static (string key, string val) SplitKv(string line)
    {
        int i = line.IndexOf(':');
        if (i < 0) return ("", "");
        var k = line.Substring(0, i).Trim();
        var v = line.Substring(i + 1).Trim();
        return (k, v);
    }

    private static string After(string line, char c)
    {
        int i = line.IndexOf(c);
        return i < 0 ? "" : line.Substring(i + 1).Trim();
    }
}
