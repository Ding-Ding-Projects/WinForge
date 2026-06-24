using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.Services;

/// <summary>
/// 一個 Windows Terminal 設定檔的扁平視圖 · A flat, editable view of one Windows Terminal profile.
/// 只暴露常用欄位；其餘未知欄位喺背後嘅 JsonObject 度原封不動保留。
/// Exposes the common fields; every other (unknown) key is preserved in the backing JsonObject so a
/// round-trip never corrupts the user's config.
/// </summary>
public sealed class WtProfile
{
    public JsonObject Node { get; }

    public WtProfile(JsonObject node) { Node = node; }

    public string Guid
    {
        get => Get("guid");
        set => Set("guid", value);
    }

    public string Name
    {
        get => Get("name");
        set => Set("name", value);
    }

    public string CommandLine
    {
        get => Get("commandline");
        set => SetOrRemove("commandline", value);
    }

    public string StartingDirectory
    {
        get => Get("startingDirectory");
        set => SetOrRemove("startingDirectory", value);
    }

    public string ColorScheme
    {
        get => Get("colorScheme");
        set => SetOrRemove("colorScheme", value);
    }

    public string FontFace
    {
        get
        {
            if (Node["font"] is JsonObject f && f["face"] is JsonValue v) return v.ToString();
            return "";
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                if (Node["font"] is JsonObject f) { f.Remove("face"); if (f.Count == 0) Node.Remove("font"); }
                return;
            }
            if (Node["font"] is not JsonObject fo) { fo = new JsonObject(); Node["font"] = fo; }
            ((JsonObject)Node["font"]!)["face"] = value;
        }
    }

    public string Icon
    {
        get => Get("icon");
        set => SetOrRemove("icon", value);
    }

    public bool Hidden
    {
        get => Node["hidden"] is JsonValue v && v.TryGetValue<bool>(out var b) && b;
        set => Node["hidden"] = value;
    }

    /// <summary>來源（WSL / VS 等自動產生嘅 profile 唔可以亂改）· The auto-generator source, if any.</summary>
    public string Source => Get("source");

    public bool IsGenerated => !string.IsNullOrEmpty(Source);

    private string Get(string key) => Node[key] is JsonValue v ? v.ToString() : "";

    private void Set(string key, string value) => Node[key] = value;

    private void SetOrRemove(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) Node.Remove(key);
        else Node[key] = value;
    }
}

/// <summary>
/// Windows Terminal 整合 · Reads, edits and safely writes Windows Terminal's settings.json natively
/// (System.Text.Json DOM so unknown keys survive), enumerates profiles + colour schemes, locates
/// wt.exe and builds wt launch arguments. 全部雙語。
/// </summary>
public static class WindowsTerminalService
{
    /// <summary>偵測到嘅 settings.json 路徑（可能係 null）· The resolved settings.json path (may be null).</summary>
    public static string? SettingsPath { get; private set; }

    /// <summary>所有可能嘅 settings.json 路徑（packaged / unpackaged / Preview）。</summary>
    public static IReadOnlyList<string> CandidatePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var list = new List<string>
        {
            // Stable (Store / winget packaged)
            Path.Combine(local, "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
            // Preview
            Path.Combine(local, "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json"),
            // Unpackaged / portable
            Path.Combine(local, "Microsoft", "Windows Terminal", "settings.json"),
        };
        return list;
    }

    /// <summary>搵到第一個存在嘅 settings.json · Resolve the first settings.json that exists on disk.</summary>
    public static string? Resolve()
    {
        foreach (var p in CandidatePaths())
        {
            if (File.Exists(p)) { SettingsPath = p; return p; }
        }
        SettingsPath = null;
        return null;
    }

    /// <summary>有冇任何 settings.json · Whether any Windows Terminal settings.json exists.</summary>
    public static bool SettingsExist() => Resolve() is not null;

    /// <summary>搵 wt.exe（直接喺 PATH，或 WindowsApps 連結）· Locate wt.exe.</summary>
    public static string? ResolveWtExe()
    {
        // wt.exe normally resolves on PATH via the WindowsApps app-execution alias.
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var alias = Path.Combine(local, "Microsoft", "WindowsApps", "wt.exe");
        if (File.Exists(alias)) return alias;

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var c = Path.Combine(dir.Trim(), "wt.exe");
                if (File.Exists(c)) return c;
            }
            catch { }
        }
        return null;
    }

    /// <summary>wt.exe 是否可用 · Whether wt.exe resolves.</summary>
    public static bool WtAvailable() => ResolveWtExe() is not null;

    /// <summary>recheck 用：wt.exe 或 settings.json 任一存在即視為已安裝。</summary>
    public static bool Installed() => WtAvailable() || SettingsExist();

    // ===================== Parse =====================

    /// <summary>讀入並 parse settings.json（容許註解／尾逗號）· Read and parse the DOM.</summary>
    public static JsonObject Load(string path)
    {
        var text = File.ReadAllText(path);
        var opts = new JsonNodeOptions { PropertyNameCaseInsensitive = false };
        var docOpts = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        var node = JsonNode.Parse(text, opts, docOpts);
        if (node is JsonObject obj) return obj;
        throw new InvalidDataException("settings.json root is not a JSON object.");
    }

    /// <summary>由 DOM 抽出 profile 清單 · Enumerate the profiles list from a loaded DOM.</summary>
    public static List<WtProfile> Profiles(JsonObject root)
    {
        var list = new List<WtProfile>();
        if (root["profiles"] is JsonObject prof && prof["list"] is JsonArray arr)
        {
            foreach (var item in arr)
                if (item is JsonObject o) list.Add(new WtProfile(o));
        }
        else if (root["profiles"] is JsonArray flat) // very old schema
        {
            foreach (var item in flat)
                if (item is JsonObject o) list.Add(new WtProfile(o));
        }
        return list;
    }

    /// <summary>取得 profiles.list JsonArray（必要時建立）· Get (or create) the profiles list array.</summary>
    public static JsonArray ProfileListArray(JsonObject root)
    {
        if (root["profiles"] is JsonArray flat) return flat;
        if (root["profiles"] is not JsonObject prof) { prof = new JsonObject(); root["profiles"] = prof; }
        if (prof["list"] is not JsonArray arr) { arr = new JsonArray(); prof["list"] = arr; }
        return arr;
    }

    /// <summary>預設 profile GUID · The default profile guid.</summary>
    public static string DefaultProfile(JsonObject root) =>
        root["defaultProfile"] is JsonValue v ? v.ToString() : "";

    public static void SetDefaultProfile(JsonObject root, string guid) => root["defaultProfile"] = guid;

    /// <summary>色彩配置名稱清單 · The colour-scheme names defined in this file.</summary>
    public static List<string> SchemeNames(JsonObject root)
    {
        var names = new List<string>();
        if (root["schemes"] is JsonArray arr)
            foreach (var item in arr)
                if (item is JsonObject o && o["name"] is JsonValue v) names.Add(v.ToString());
        return names;
    }

    /// <summary>內建色彩配置（即使 schemes:[] 都有得揀）· Built-in scheme names always available in WT.</summary>
    public static readonly string[] BuiltInSchemes =
    {
        "Campbell", "Campbell Powershell", "Vintage", "One Half Dark", "One Half Light",
        "Solarized Dark", "Solarized Light", "Tango Dark", "Tango Light",
    };

    // ===================== Mutate =====================

    /// <summary>新建一個基本 profile · Build a new minimal profile JsonObject with a fresh guid.</summary>
    public static WtProfile NewProfile(string name, string commandline)
    {
        var o = new JsonObject
        {
            ["guid"] = NewGuid(),
            ["name"] = string.IsNullOrWhiteSpace(name) ? "New profile" : name,
            ["hidden"] = false,
        };
        if (!string.IsNullOrWhiteSpace(commandline)) o["commandline"] = commandline;
        return new WtProfile(o);
    }

    /// <summary>複製一個 profile（新 guid、改名）· Deep-clone a profile with a new guid and "(copy)" name.</summary>
    public static WtProfile Duplicate(WtProfile src)
    {
        var clone = (JsonObject)JsonNode.Parse(src.Node.ToJsonString())!;
        clone["guid"] = NewGuid();
        var baseName = src.Name;
        clone["name"] = string.IsNullOrEmpty(baseName) ? "Copy" : baseName + " (copy)";
        // A duplicate of a generated profile becomes a standalone user profile.
        clone.Remove("source");
        return new WtProfile(clone);
    }

    public static string NewGuid() => "{" + System.Guid.NewGuid().ToString() + "}";

    // ===================== Save (backup + atomic) =====================

    /// <summary>
    /// 安全寫入 · Safe save: back up the existing file, write to a temp file, then atomically replace.
    /// 保留所有未知欄位（因為我哋寫返成個 DOM）· Preserves unknown keys because we serialise the whole DOM.
    /// 回傳備份檔路徑 · Returns the backup path.
    /// </summary>
    public static string Save(string path, JsonObject root)
    {
        var backup = "";
        if (File.Exists(path))
        {
            backup = path + ".winforge-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".bak";
            File.Copy(path, backup, overwrite: true);
        }

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            // settings.json may contain non-ASCII (font names, comments) — don't escape them.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        var json = root.ToJsonString(opts);

        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        var tmp = Path.Combine(dir, ".winforge-tmp-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(tmp, json, new UTF8Encoding(false));

        // Atomic replace so Windows Terminal's file watcher never sees a half-written file.
        if (File.Exists(path))
        {
            try { File.Replace(tmp, path, null); }
            catch { File.Copy(tmp, path, overwrite: true); File.Delete(tmp); }
        }
        else
        {
            File.Move(tmp, path);
        }
        return backup;
    }

    // ===================== wt.exe launch args =====================

    /// <summary>
    /// 砌 wt 啟動參數 · Build wt.exe arguments to open a new window/tab on a profile and/or directory.
    /// </summary>
    public static string BuildLaunchArgs(string? profileName, string? startingDir, bool newTab)
    {
        var sb = new StringBuilder();
        if (newTab) sb.Append("nt ");
        if (!string.IsNullOrWhiteSpace(profileName)) sb.Append($"-p \"{profileName}\" ");
        if (!string.IsNullOrWhiteSpace(startingDir)) sb.Append($"-d \"{startingDir}\" ");
        return sb.ToString().Trim();
    }
}
