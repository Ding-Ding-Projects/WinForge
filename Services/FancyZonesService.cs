using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// FancyZones（PowerToys 視窗分區）管理前端 · A manager front-end for PowerToys FancyZones.
/// FancyZones 嘅分區引擎係原生 PowerToys（C++ 鈎住滑鼠／鍵盤、畫分區、用 SetWindowPos 貼窗）—
/// 唔可以重寫，所以呢度只係偵測安裝、啟動 PowerToys、開分區編輯器、用 settings.json 開關模組，
/// 同埋盡力讀返 FancyZones 嘅 JSON 設定／自訂版面。全部防禦式，絕不拋出例外。
/// The zone engine stays native PowerToys; this service only detects the install, launches PowerToys,
/// opens the zone editor (named toggle event, with an exe fallback), toggles the module via settings.json,
/// and best-effort reads the FancyZones JSON (settings + saved custom layouts). Defensive throughout.
/// </summary>
public static class FancyZonesService
{
    /// <summary>winget 套件 ID · The winget package ID for installs.</summary>
    public const string WingetId = "Microsoft.PowerToys";

    /// <summary>PowerToys 主程式檔名 · The PowerToys host executable file name.</summary>
    public const string HostExe = "PowerToys.exe";

    /// <summary>FancyZones 編輯器檔名 · The FancyZones editor executable file name.</summary>
    public const string EditorExe = "PowerToys.FancyZonesEditor.exe";

    /// <summary>
    /// 切換編輯器嘅具名事件（PowerToys runner 監聽緊）· The named auto-reset event the PowerToys runner
    /// listens on to open the FancyZones editor itself (writes monitor parameters first).
    /// </summary>
    public const string EditorToggleEvent = "Local\\FancyZones-ToggleEditorEvent-1e174338-06a3-472b-874d-073b21c62f14";

    private static string? _cachedHostPath;
    private static bool _scanned;

    // ===================== 偵測 · Detection =====================

    /// <summary>清除已快取嘅偵測路徑（安裝後重新掃描）· Clear the cached detection so a rescan picks up a fresh install.</summary>
    public static void Rescan()
    {
        _cachedHostPath = null;
        _scanned = false;
    }

    /// <summary>PowerToys 嘅安裝資料夾（搵到 PowerToys.exe 嘅話）· The PowerToys install folder, if found.</summary>
    public static string? InstallDir
    {
        get
        {
            var host = HostPath;
            return host is null ? null : Path.GetDirectoryName(host);
        }
    }

    /// <summary>
    /// 解析 PowerToys.exe 嘅完整路徑 · Resolve the full path to PowerToys.exe.
    /// 先試 %ProgramFiles%\PowerToys、再 %LOCALAPPDATA%\PowerToys、再 registry uninstall key。
    /// Tries Program Files, then the per-user LocalAppData install, then the uninstall registry keys.
    /// </summary>
    public static string? HostPath
    {
        get
        {
            if (_scanned) return _cachedHostPath;
            _scanned = true;
            _cachedHostPath = ResolveHostPath();
            return _cachedHostPath;
        }
    }

    private static string? ResolveHostPath()
    {
        foreach (var candidate in CandidateHostPaths())
        {
            try { if (File.Exists(candidate)) return candidate; }
            catch { /* ignore */ }
        }
        return null;
    }

    private static IEnumerable<string> CandidateHostPaths()
    {
        string? pf = SafeFolder(Environment.SpecialFolder.ProgramFiles);
        if (pf is not null) yield return Path.Combine(pf, "PowerToys", HostExe);
        string? pfx86 = SafeFolder(Environment.SpecialFolder.ProgramFilesX86);
        if (pfx86 is not null) yield return Path.Combine(pfx86, "PowerToys", HostExe);
        string? local = SafeFolder(Environment.SpecialFolder.LocalApplicationData);
        if (local is not null)
        {
            yield return Path.Combine(local, "PowerToys", HostExe);
            yield return Path.Combine(local, "Programs", "PowerToys", HostExe);
        }
        // registry InstallLocation (per-machine + per-user uninstall keys)
        foreach (var loc in RegistryInstallDirs())
            yield return Path.Combine(loc, HostExe);
    }

    private static IEnumerable<string> RegistryInstallDirs()
    {
        var results = new List<string>();
        TryRegInstall(Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", results);
        TryRegInstall(Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", results);
        TryRegInstall(Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", results);
        return results;
    }

    private static void TryRegInstall(RegistryKey root, string subPath, List<string> results)
    {
        try
        {
            using var key = root.OpenSubKey(subPath);
            if (key is null) return;
            foreach (var name in key.GetSubKeyNames())
            {
                try
                {
                    using var sub = key.OpenSubKey(name);
                    var display = sub?.GetValue("DisplayName") as string;
                    if (display is null || display.IndexOf("PowerToys", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var loc = sub?.GetValue("InstallLocation") as string;
                    if (!string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc)) results.Add(loc!);
                }
                catch { /* ignore one bad subkey */ }
            }
        }
        catch { /* ignore */ }
    }

    private static string? SafeFolder(Environment.SpecialFolder f)
    {
        try { var p = Environment.GetFolderPath(f); return string.IsNullOrEmpty(p) ? null : p; }
        catch { return null; }
    }

    /// <summary>PowerToys 裝咗未 · True if PowerToys.exe was found.</summary>
    public static Task<bool> IsInstalledAsync(CancellationToken ct = default)
        => Task.Run(() => HostPath is not null, ct);

    /// <summary>讀 PowerToys 版本（由檔案版本資訊）· Read the PowerToys version from the exe's file version info.</summary>
    public static string? Version
    {
        get
        {
            try
            {
                var host = HostPath;
                if (host is null) return null;
                var fvi = FileVersionInfo.GetVersionInfo(host);
                return string.IsNullOrWhiteSpace(fvi.ProductVersion) ? fvi.FileVersion : fvi.ProductVersion;
            }
            catch { return null; }
        }
    }

    /// <summary>PowerToys runner 而家行緊未 · True if a PowerToys process appears to be running.</summary>
    public static bool IsRunning
    {
        get
        {
            try { return Process.GetProcessesByName("PowerToys").Length > 0; }
            catch { return false; }
        }
    }

    /// <summary>FancyZones 編輯器而家開咗未 · True if the FancyZones editor is open.</summary>
    public static bool IsEditorRunning
    {
        get
        {
            try { return Process.GetProcessesByName("PowerToys.FancyZonesEditor").Length > 0; }
            catch { return false; }
        }
    }

    // ===================== 啟動 · Launch =====================

    /// <summary>啟動 PowerToys（如果未行）· Launch PowerToys if it is not already running.</summary>
    public static TweakResult LaunchPowerToys()
    {
        var host = HostPath;
        if (host is null)
            return TweakResult.Fail("PowerToys is not installed.", "未安裝 PowerToys。");
        try
        {
            if (IsRunning)
                return TweakResult.Ok("PowerToys is already running.", "PowerToys 已經行緊。");
            Process.Start(new ProcessStartInfo { FileName = host, UseShellExecute = true });
            return TweakResult.Ok("Launched PowerToys.", "已啟動 PowerToys。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>
    /// 開 FancyZones 分區編輯器 · Open the FancyZones zone editor.
    /// 做法：確保 PowerToys 行緊，再 Set 具名切換事件等 runner 自己開編輯器（會先寫好顯示器參數）。
    /// 若 runner 唔在、事件唔得，就退返直接啟動編輯器 exe。
    /// Ensures PowerToys is running, then signals the named toggle event so the runner opens the editor
    /// (writing monitor parameters first); falls back to launching the editor exe directly.
    /// </summary>
    public static TweakResult OpenZoneEditor()
    {
        var dir = InstallDir;
        if (dir is null)
            return TweakResult.Fail("PowerToys is not installed.", "未安裝 PowerToys。");

        // 確保 runner 行緊（事件法需要佢）· Make sure the runner is up (the event approach needs it).
        if (!IsRunning) LaunchPowerToys();

        // 1) 具名事件 — 最受支援嘅做法 · Named event — the supported path.
        try
        {
            using var ev = new EventWaitHandle(false, EventResetMode.AutoReset, EditorToggleEvent);
            ev.Set();
            return TweakResult.Ok("Opened the FancyZones zone editor.", "已開啟 FancyZones 分區編輯器。");
        }
        catch { /* 退返直接開 exe · fall through to the exe */ }

        // 2) 退返直接啟動編輯器 exe · Fall back to launching the editor exe.
        try
        {
            var editor = Path.Combine(dir, EditorExe);
            if (File.Exists(editor))
            {
                Process.Start(new ProcessStartInfo { FileName = editor, UseShellExecute = true, WorkingDirectory = dir });
                return TweakResult.Ok("Opened the FancyZones zone editor.", "已開啟 FancyZones 分區編輯器。");
            }
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }

        return TweakResult.Fail("Could not open the zone editor.", "開唔到分區編輯器。");
    }

    /// <summary>
    /// 開 PowerToys 設定並跳到 FancyZones 頁 · Open PowerToys Settings on the FancyZones page
    /// via the <c>--open-settings=FancyZones</c> deep-link.
    /// </summary>
    public static TweakResult OpenSettingsPage()
    {
        var host = HostPath;
        if (host is null)
            return TweakResult.Fail("PowerToys is not installed.", "未安裝 PowerToys。");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = host,
                Arguments = "--open-settings=FancyZones",
                UseShellExecute = true,
            });
            return TweakResult.Ok("Opened PowerToys Settings (FancyZones).", "已開啟 PowerToys 設定（FancyZones）。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ===================== 設定 JSON · Settings JSON =====================

    /// <summary>PowerToys 設定根目錄 · The PowerToys settings root folder.</summary>
    public static string SettingsRoot
    {
        get
        {
            var local = SafeFolder(Environment.SpecialFolder.LocalApplicationData) ?? "";
            return Path.Combine(local, "Microsoft", "PowerToys");
        }
    }

    /// <summary>頂層 settings.json（模組開關喺度）· The top-level settings.json holding the module-enabled flags.</summary>
    public static string TopSettingsPath => Path.Combine(SettingsRoot, "settings.json");

    /// <summary>FancyZones 模組設定資料夾 · The FancyZones module settings folder.</summary>
    public static string FancyZonesDir => Path.Combine(SettingsRoot, "FancyZones");

    /// <summary>FancyZones 模組 settings.json · The FancyZones module settings.json (snap/colours/hotkeys).</summary>
    public static string FancyZonesSettingsPath => Path.Combine(FancyZonesDir, "settings.json");

    /// <summary>自訂版面 JSON · The saved custom-layouts JSON.</summary>
    public static string CustomLayoutsPath => Path.Combine(FancyZonesDir, "custom-layouts.json");

    /// <summary>套用咗嘅版面 JSON · The applied-layouts JSON.</summary>
    public static string AppliedLayoutsPath => Path.Combine(FancyZonesDir, "applied-layouts.json");

    /// <summary>
    /// FancyZones 喺頂層 settings.json 入面開咗未 · Whether FancyZones is enabled in the top-level settings.json.
    /// 回傳 null = 讀唔到（檔案唔在或解析唔到）· Returns null when the file is missing or unreadable.
    /// </summary>
    public static bool? IsModuleEnabled()
    {
        try
        {
            if (!File.Exists(TopSettingsPath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(TopSettingsPath));
            if (doc.RootElement.TryGetProperty("enabled", out var enabled)
                && enabled.TryGetProperty("FancyZones", out var fz)
                && (fz.ValueKind == JsonValueKind.True || fz.ValueKind == JsonValueKind.False))
                return fz.GetBoolean();
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// 喺頂層 settings.json 開／關 FancyZones · Enable or disable FancyZones in the top-level settings.json.
    /// 用最少侵入嘅文字改寫，保留檔案其餘部分；改完通知 PowerToys 重讀（若行緊就重啟 runner）。
    /// Minimally rewrites the "enabled":{"FancyZones":bool} flag, preserving the rest, then nudges
    /// PowerToys to reload (restarts the runner if it is running). Best-effort; never throws.
    /// </summary>
    public static TweakResult SetModuleEnabled(bool enable)
    {
        try
        {
            Directory.CreateDirectory(SettingsRoot);
            JsonNode? root = File.Exists(TopSettingsPath)
                ? JsonNode.Parse(File.ReadAllText(TopSettingsPath))
                : new JsonObject();
            root ??= new JsonObject();
            if (root["enabled"] is not JsonObject en)
            {
                en = new JsonObject();
                root["enabled"] = en;
            }
            en["FancyZones"] = enable;
            File.WriteAllText(TopSettingsPath,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

            // 叫 PowerToys 重讀設定（重啟 runner 最穩陣）· Nudge PowerToys to reload (restart the runner).
            RestartRunner();

            return enable
                ? TweakResult.Ok("FancyZones enabled. PowerToys will apply it.", "已啟用 FancyZones，PowerToys 會套用。")
                : TweakResult.Ok("FancyZones disabled.", "已停用 FancyZones。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    private static void RestartRunner()
    {
        var host = HostPath;
        if (host is null) return;
        try
        {
            foreach (var p in Process.GetProcessesByName("PowerToys"))
            {
                try { p.Kill(true); p.WaitForExit(3000); } catch { }
            }
        }
        catch { }
        try { Process.Start(new ProcessStartInfo { FileName = host, UseShellExecute = true }); } catch { }
    }

    /// <summary>
    /// 讀返 FancyZones 模組 settings.json 入面一個布林屬性嘅 value · Read a boolean FancyZones property value.
    /// PowerToys 把每個屬性包成 {"value": ...}。回傳 null = 讀唔到。
    /// FancyZones wraps each property as {"value": ...}; returns null when unreadable.
    /// </summary>
    public static bool? GetBoolProperty(string propertyName)
    {
        try
        {
            if (!File.Exists(FancyZonesSettingsPath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(FancyZonesSettingsPath));
            if (!doc.RootElement.TryGetProperty("properties", out var props)) return null;
            if (!props.TryGetProperty(propertyName, out var prop)) return null;
            if (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False) return prop.GetBoolean();
            if (prop.TryGetProperty("value", out var val)
                && (val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False))
                return val.GetBoolean();
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// 喺 FancyZones 模組 settings.json 寫一個布林屬性 · Write a boolean FancyZones property (wrapped as {"value":...}).
    /// 保留檔案其餘部分；改完重啟 runner 等 PowerToys 重讀。Preserves the rest of the file and restarts the
    /// runner so PowerToys reloads. Best-effort; never throws.
    /// </summary>
    public static TweakResult SetBoolProperty(string propertyName, bool value)
    {
        try
        {
            Directory.CreateDirectory(FancyZonesDir);
            JsonNode? root = File.Exists(FancyZonesSettingsPath)
                ? JsonNode.Parse(File.ReadAllText(FancyZonesSettingsPath))
                : new JsonObject();
            root ??= new JsonObject();
            if (root["name"] is null) root["name"] = "FancyZones";
            if (root["version"] is null) root["version"] = "1.0";
            if (root["properties"] is not JsonObject props)
            {
                props = new JsonObject();
                root["properties"] = props;
            }
            props[propertyName] = new JsonObject { ["value"] = value };
            File.WriteAllText(FancyZonesSettingsPath,
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            RestartRunner();
            return TweakResult.Ok("Saved. PowerToys will apply it.", "已儲存，PowerToys 會套用。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>
    /// 列出已儲存嘅自訂版面名 · List saved custom-layout names from custom-layouts.json. Best-effort.
    /// </summary>
    public static IReadOnlyList<string> ReadCustomLayoutNames()
    {
        var names = new List<string>();
        try
        {
            if (!File.Exists(CustomLayoutsPath)) return names;
            using var doc = JsonDocument.Parse(File.ReadAllText(CustomLayoutsPath));
            JsonElement arr;
            if (doc.RootElement.ValueKind == JsonValueKind.Array) arr = doc.RootElement;
            else if (doc.RootElement.TryGetProperty("custom-layouts", out var inner)) arr = inner;
            else return names;
            if (arr.ValueKind != JsonValueKind.Array) return names;
            foreach (var item in arr.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                {
                    var s = n.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) names.Add(s!);
                }
            }
        }
        catch { /* 防禦式 · defensive — schema can change */ }
        return names;
    }

    /// <summary>
    /// 匯出 FancyZones 設定＋版面 JSON 到一個資料夾 · Export the FancyZones JSON files to a folder.
    /// </summary>
    public static TweakResult ExportLayouts(string targetFolder)
    {
        try
        {
            if (!Directory.Exists(FancyZonesDir))
                return TweakResult.Fail("No FancyZones data folder found yet.", "仲未搵到 FancyZones 資料夾。");
            Directory.CreateDirectory(targetFolder);
            int n = 0;
            foreach (var src in Directory.EnumerateFiles(FancyZonesDir, "*.json"))
            {
                try { File.Copy(src, Path.Combine(targetFolder, Path.GetFileName(src)), overwrite: true); n++; }
                catch { }
            }
            return n > 0
                ? TweakResult.Ok($"Exported {n} FancyZones file(s).", $"已匯出 {n} 個 FancyZones 檔案。")
                : TweakResult.Fail("Nothing was exported.", "冇匯出任何嘢。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>
    /// 匯入一個自訂版面 JSON（複製入 FancyZones 資料夾）· Import a custom-layouts JSON into the FancyZones folder.
    /// </summary>
    public static TweakResult ImportLayoutFile(string sourceFile)
    {
        try
        {
            if (!File.Exists(sourceFile))
                return TweakResult.Fail("Source file not found.", "搵唔到來源檔案。");
            // 確認係有效 JSON · validate it parses as JSON first.
            try { using var _ = JsonDocument.Parse(File.ReadAllText(sourceFile)); }
            catch { return TweakResult.Fail("That file is not valid JSON.", "嗰個檔案唔係有效 JSON。"); }

            Directory.CreateDirectory(FancyZonesDir);
            var name = Path.GetFileName(sourceFile);
            // 若唔係已知檔名，就當作 custom-layouts.json · default unknown names to custom-layouts.json.
            string dest = name.Equals("custom-layouts.json", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("applied-layouts.json", StringComparison.OrdinalIgnoreCase)
                          || name.Equals("layout-templates.json", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(FancyZonesDir, name)
                : CustomLayoutsPath;
            File.Copy(sourceFile, dest, overwrite: true);
            RestartRunner();
            return TweakResult.Ok($"Imported into {Path.GetFileName(dest)}.", $"已匯入到 {Path.GetFileName(dest)}。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}
