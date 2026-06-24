using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 一個 Rainmeter 皮膚設定（一個 .ini 檔）· One Rainmeter skin config — a single <c>.ini</c> file
/// inside a config folder under the Skins tree. <see cref="Config"/> is the folder path relative to
/// Skins (the Rainmeter "config name", e.g. <c>illustro\Clock</c>); <see cref="File"/> is the ini file
/// (e.g. <c>Clock.ini</c>). <see cref="Active"/> reflects whether Rainmeter.ini marks it loaded.
/// </summary>
public sealed class RainmeterSkin
{
    public string Config { get; init; } = "";
    public string File { get; init; } = "";
    public bool Active { get; set; }

    /// <summary>顯示名 · "config\file" without the trailing .ini, for compact display.</summary>
    public string Display => File.Equals("skin.ini", StringComparison.OrdinalIgnoreCase) || CountOfFiles <= 1
        ? Config
        : $"{Config}\\{File}";

    /// <summary>同一個 config 入面有幾多個 ini（畀 Display 用）· file count in the same config.</summary>
    public int CountOfFiles { get; set; } = 1;

    /// <summary>狀態文字 · Localized active/inactive label.</summary>
    public string StatusText(bool zh) => Active ? (zh ? "已載入" : "Loaded") : (zh ? "未載入" : "Not loaded");
}

/// <summary>
/// 應用程式內 Rainmeter 控制 · In-app Rainmeter front-end built around the binary's command-line
/// "!bang" interface. Locates <c>Rainmeter.exe</c> + <c>SkinInstaller.exe</c> via the registry
/// (<c>HKLM\SOFTWARE\Rainmeter</c>), resolves the Skins folder (registry/ini → My Documents fallback,
/// never hard-coded), scans the Skins tree for every <c>.ini</c> config, reads which configs are active
/// from <c>Rainmeter.ini</c>, and runs activate / deactivate / toggle / refresh / show / hide bangs by
/// passing them to a running instance. State is re-read from <c>Rainmeter.ini</c> rather than trusting a
/// bang's exit code (bangs fail silently). No source linkage — WinForge only wraps the binary.
/// </summary>
public sealed class RainmeterService
{
    public const string WingetId = "Rainmeter.Rainmeter";

    private string? _exePath;
    private string? _skinPath;
    private string? _settingsPath; // folder containing Rainmeter.ini

    // ── Discovery ────────────────────────────────────────────────────────────

    /// <summary>清空快取嘅路徑（安裝後 rescan）· Forget cached paths so the next call re-discovers.</summary>
    public void Rescan() { _exePath = null; _skinPath = null; _settingsPath = null; }

    /// <summary>Rainmeter.exe 路徑（或 null）· Full path to Rainmeter.exe, or null if not installed.</summary>
    public string? ExePath
    {
        get
        {
            if (_exePath is not null) return _exePath.Length == 0 ? null : _exePath;
            _exePath = ResolveExe() ?? "";
            return _exePath.Length == 0 ? null : _exePath;
        }
    }

    public bool IsInstalled => ExePath is not null;

    /// <summary>SkinInstaller.exe 路徑（與 Rainmeter.exe 同資料夾）· SkinInstaller.exe alongside Rainmeter.exe.</summary>
    public string? SkinInstallerPath
    {
        get
        {
            var exe = ExePath;
            if (exe is null) return null;
            var dir = Path.GetDirectoryName(exe);
            if (dir is null) return null;
            var p = Path.Combine(dir, "SkinInstaller.exe");
            return System.IO.File.Exists(p) ? p : null;
        }
    }

    private string? ResolveExe()
    {
        // 1) HKLM\SOFTWARE\Rainmeter (installer writes a default value with the install dir).
        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var k = bk.OpenSubKey(@"SOFTWARE\Rainmeter");
                if (k is null) continue;
                var dir = (k.GetValue(null) as string) ?? (k.GetValue("InstallPath") as string);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    var exe = Path.Combine(dir!, "Rainmeter.exe");
                    if (System.IO.File.Exists(exe)) return exe;
                }
            }
            catch { /* ignore */ }
        }

        // 2) App Paths.
        try
        {
            using var bk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var k = bk.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Rainmeter.exe");
            if ((k?.GetValue(null) as string) is { Length: > 0 } p && System.IO.File.Exists(p)) return p;
        }
        catch { /* ignore */ }

        // 3) Default install locations.
        foreach (var env in new[] { "ProgramFiles", "ProgramFiles(x86)" })
        {
            var root = Environment.GetEnvironmentVariable(env);
            if (string.IsNullOrEmpty(root)) continue;
            var exe = Path.Combine(root, "Rainmeter", "Rainmeter.exe");
            if (System.IO.File.Exists(exe)) return exe;
        }
        return null;
    }

    /// <summary>Rainmeter.ini 路徑 · Path to the active Rainmeter.ini (settings file).</summary>
    public string? SettingsIniPath
    {
        get
        {
            var dir = SettingsFolder;
            if (dir is null) return null;
            var p = Path.Combine(dir, "Rainmeter.ini");
            return System.IO.File.Exists(p) ? p : null;
        }
    }

    /// <summary>設定資料夾（含 Rainmeter.ini）· Folder that holds Rainmeter.ini.</summary>
    public string? SettingsFolder
    {
        get
        {
            if (_settingsPath is not null) return _settingsPath.Length == 0 ? null : _settingsPath;
            _settingsPath = ResolveSettingsFolder() ?? "";
            return _settingsPath.Length == 0 ? null : _settingsPath;
        }
    }

    private string? ResolveSettingsFolder()
    {
        // Portable install keeps Rainmeter.ini next to the exe; standard install uses %APPDATA%\Rainmeter.
        var exe = ExePath;
        if (exe is not null)
        {
            var dir = Path.GetDirectoryName(exe);
            if (dir is not null && System.IO.File.Exists(Path.Combine(dir, "Rainmeter.ini")))
                return dir;
        }
        var appdata = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rainmeter");
        if (Directory.Exists(appdata)) return appdata;
        return null;
    }

    /// <summary>Skins 資料夾 · Resolve the Skins folder (registry/ini → My Documents fallback).</summary>
    public string? SkinPath
    {
        get
        {
            if (_skinPath is not null) return _skinPath.Length == 0 ? null : _skinPath;
            _skinPath = ResolveSkinPath() ?? "";
            return _skinPath.Length == 0 ? null : _skinPath;
        }
    }

    private string? ResolveSkinPath()
    {
        // 1) Rainmeter.ini [Rainmeter] SkinPath=...
        var ini = SettingsIniPath;
        if (ini is not null)
        {
            var sp = ReadIniValue(ini, "Rainmeter", "SkinPath");
            if (!string.IsNullOrWhiteSpace(sp))
            {
                sp = Environment.ExpandEnvironmentVariables(sp!.Trim());
                if (Directory.Exists(sp)) return sp.TrimEnd('\\') + "\\";
            }
        }

        // 2) My Documents\Rainmeter\Skins (the default for a normal install).
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(docs))
        {
            var p = Path.Combine(docs, "Rainmeter", "Skins");
            if (Directory.Exists(p)) return p.TrimEnd('\\') + "\\";
        }

        // 3) Portable: <exeDir>\Skins.
        var exe = ExePath;
        if (exe is not null)
        {
            var dir = Path.GetDirectoryName(exe);
            if (dir is not null)
            {
                var p = Path.Combine(dir, "Skins");
                if (Directory.Exists(p)) return p.TrimEnd('\\') + "\\";
            }
        }
        return null;
    }

    /// <summary>Layouts 資料夾 · The Layouts folder (sibling of Skins), if present.</summary>
    public string? LayoutsFolder
    {
        get
        {
            var skins = SkinPath;
            if (skins is null) return null;
            var parent = Path.GetDirectoryName(skins.TrimEnd('\\'));
            if (parent is null) return null;
            var p = Path.Combine(parent, "Layouts");
            return Directory.Exists(p) ? p : null;
        }
    }

    // ── Enumeration ──────────────────────────────────────────────────────────

    /// <summary>
    /// 列出所有皮膚設定 · Enumerate every skin config (each <c>.ini</c> under the Skins tree) and mark
    /// which are active by cross-referencing Rainmeter.ini. Returns sorted by config path.
    /// </summary>
    public List<RainmeterSkin> EnumerateSkins()
    {
        var list = new List<RainmeterSkin>();
        var root = SkinPath;
        if (root is null || !Directory.Exists(root)) return list;

        var active = ReadActiveConfigs(); // config(lower) -> active ini file name

        // Group .ini files by their containing config folder, to compute CountOfFiles.
        var byConfig = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> inis;
        try { inis = Directory.EnumerateFiles(root, "*.ini", SearchOption.AllDirectories); }
        catch { return list; }

        foreach (var file in inis)
        {
            var dir = Path.GetDirectoryName(file);
            if (dir is null) continue;
            // Config = folder path relative to Skins root.
            var config = Path.GetRelativePath(root.TrimEnd('\\'), dir).Trim('\\');
            if (config == "." || config.Length == 0) continue; // ini directly in Skins root isn't a config
            var fileName = Path.GetFileName(file);
            if (!byConfig.TryGetValue(config, out var bucket)) { bucket = new(); byConfig[config] = bucket; }
            bucket.Add(fileName);
        }

        foreach (var (config, files) in byConfig)
        {
            foreach (var f in files)
            {
                bool isActive = active.TryGetValue(config.ToLowerInvariant(), out var activeFile)
                    && string.Equals(activeFile, f, StringComparison.OrdinalIgnoreCase);
                list.Add(new RainmeterSkin
                {
                    Config = config,
                    File = f,
                    Active = isActive,
                    CountOfFiles = files.Count,
                });
            }
        }

        return list
            .OrderByDescending(s => s.Active)
            .ThenBy(s => s.Config, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.File, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 由 Rainmeter.ini 讀取已載入 config · Parse Rainmeter.ini for active configs. Each loaded config
    /// is a <c>[Config\Sub]</c> section with <c>Active=N</c> (1-based ini index) and <c>FileN=name.ini</c>.
    /// Returns config(lowercased) → active ini file name.
    /// </summary>
    public Dictionary<string, string> ReadActiveConfigs()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ini = SettingsIniPath;
        if (ini is null) return result;

        string[] lines;
        try { lines = System.IO.File.ReadAllLines(ini); }
        catch { return result; }

        string? section = null;
        int activeIndex = 0;
        var fileByIndex = new Dictionary<int, string>();

        void Flush()
        {
            if (section is not null && activeIndex > 0 && fileByIndex.TryGetValue(activeIndex, out var fn))
                result[section.ToLowerInvariant()] = fn;
        }

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                Flush();
                section = line[1..^1].Trim();
                activeIndex = 0;
                fileByIndex.Clear();
                continue;
            }
            if (section is null) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var val = line[(eq + 1)..].Trim();
            if (key.Equals("Active", StringComparison.OrdinalIgnoreCase))
                int.TryParse(val, out activeIndex);
            else if (key.StartsWith("File", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(key.AsSpan(4), out var idx))
                fileByIndex[idx] = val;
        }
        Flush();

        // The "[Rainmeter]" general section is not a config — drop it if it leaked in.
        result.Remove("rainmeter");
        return result;
    }

    private static string? ReadIniValue(string iniPath, string section, string key)
    {
        try
        {
            string? cur = null;
            foreach (var raw in System.IO.File.ReadAllLines(iniPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(';')) continue;
                if (line.StartsWith('[') && line.EndsWith(']')) { cur = line[1..^1].Trim(); continue; }
                if (!string.Equals(cur, section, StringComparison.OrdinalIgnoreCase)) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (line[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                    return line[(eq + 1)..].Trim();
            }
        }
        catch { /* ignore */ }
        return null;
    }

    // ── Process / bangs ──────────────────────────────────────────────────────

    /// <summary>Rainmeter 係咪喺度行緊 · Is a Rainmeter instance running?</summary>
    public bool IsRunning()
    {
        try { return Process.GetProcessesByName("Rainmeter").Length > 0; }
        catch { return false; }
    }

    /// <summary>啟動 Rainmeter（若未行）· Launch Rainmeter if it isn't already running.</summary>
    public async Task<TweakResult> EnsureRunning(CancellationToken ct = default)
    {
        var exe = ExePath;
        if (exe is null) return TweakResult.Fail("Rainmeter is not installed.", "未安裝 Rainmeter。");
        if (IsRunning()) return TweakResult.Ok("Rainmeter is already running.", "Rainmeter 已經喺度行緊。");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
            // Give it a moment to come up so the first bang lands.
            for (int i = 0; i < 20 && !IsRunning(); i++) await Task.Delay(150, ct);
            return TweakResult.Ok("Rainmeter started.", "已啟動 Rainmeter。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>
    /// 向行緊嘅 Rainmeter 發送一個 bang · Send one !bang to the running instance (launching it first if
    /// needed). Rainmeter.exe forwards the bang to the live instance, then exits.
    /// </summary>
    public async Task<TweakResult> SendBang(string bangWithArgs, CancellationToken ct = default)
    {
        var exe = ExePath;
        if (exe is null) return TweakResult.Fail("Rainmeter is not installed.", "未安裝 Rainmeter。");
        if (!IsRunning())
        {
            var started = await EnsureRunning(ct);
            if (!started.Success) return started;
        }
        return await ShellRunner.Run(exe, bangWithArgs, elevated: false, ct);
    }

    private static string Q(string s) => $"\"{s}\"";

    /// <summary>!ActivateConfig "config" "file.ini" — 載入皮膚 · load a skin.</summary>
    public Task<TweakResult> ActivateConfig(RainmeterSkin skin, CancellationToken ct = default)
        => SendBang($"!ActivateConfig {Q(skin.Config)} {Q(skin.File)}", ct);

    /// <summary>!DeactivateConfig "config" — 卸載皮膚 · unload a skin.</summary>
    public Task<TweakResult> DeactivateConfig(RainmeterSkin skin, CancellationToken ct = default)
        => SendBang($"!DeactivateConfig {Q(skin.Config)}", ct);

    /// <summary>!ToggleConfig "config" "file.ini" — 切換載入／卸載 · toggle loaded state.</summary>
    public Task<TweakResult> ToggleConfig(RainmeterSkin skin, CancellationToken ct = default)
        => SendBang($"!ToggleConfig {Q(skin.Config)} {Q(skin.File)}", ct);

    /// <summary>!Refresh "config" — 重新整理單一皮膚 · refresh one loaded skin.</summary>
    public Task<TweakResult> RefreshSkin(RainmeterSkin skin, CancellationToken ct = default)
        => SendBang($"!Refresh {Q(skin.Config)}", ct);

    /// <summary>!Hide "config" — 隱藏皮膚 · hide a loaded skin.</summary>
    public Task<TweakResult> HideSkin(RainmeterSkin skin, CancellationToken ct = default)
        => SendBang($"!Hide {Q(skin.Config)}", ct);

    /// <summary>!Show "config" — 顯示皮膚 · show a loaded skin.</summary>
    public Task<TweakResult> ShowSkin(RainmeterSkin skin, CancellationToken ct = default)
        => SendBang($"!Show {Q(skin.Config)}", ct);

    /// <summary>!ToggleFade "config" — 淡入／淡出 · toggle skin visibility with a fade.</summary>
    public Task<TweakResult> ToggleFade(RainmeterSkin skin, CancellationToken ct = default)
        => SendBang($"!ToggleFade {Q(skin.Config)}", ct);

    /// <summary>!RefreshApp — 重新整理全部 · re-read Skins + reload everything.</summary>
    public Task<TweakResult> RefreshApp(CancellationToken ct = default) => SendBang("!RefreshApp", ct);

    /// <summary>!Manage — 開管理視窗 · open Rainmeter's Manage window.</summary>
    public Task<TweakResult> OpenManage(CancellationToken ct = default) => SendBang("!Manage", ct);

    /// <summary>!About — 開「關於」視窗（含 log）· open the About / log window.</summary>
    public Task<TweakResult> OpenAbout(CancellationToken ct = default) => SendBang("!About", ct);

    /// <summary>!EditSkin "config" "file.ini" — 編輯皮膚 ini · open the skin's ini in the configured editor.</summary>
    public Task<TweakResult> EditSkin(RainmeterSkin skin, CancellationToken ct = default)
        => SendBang($"!EditSkin {Q(skin.Config)} {Q(skin.File)}", ct);

    /// <summary>!LoadLayout "name" — 載入版面配置 · load a saved layout.</summary>
    public Task<TweakResult> LoadLayout(string layoutName, CancellationToken ct = default)
        => SendBang($"!LoadLayout {Q(layoutName)}", ct);

    /// <summary>!Quit — 退出 Rainmeter · quit Rainmeter.</summary>
    public Task<TweakResult> Quit(CancellationToken ct = default) => SendBang("!Quit", ct);

    /// <summary>列出已儲存嘅版面配置 · List saved layout names (folders under Layouts).</summary>
    public List<string> EnumerateLayouts()
    {
        var folder = LayoutsFolder;
        if (folder is null) return new();
        try
        {
            return Directory.EnumerateDirectories(folder)
                .Select(d => Path.GetFileName(d) ?? "")
                .Where(n => n.Length > 0)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return new(); }
    }

    // ── Skin pack install ─────────────────────────────────────────────────────

    /// <summary>
    /// 安裝一個 .rmskin 皮膚包 · Install a <c>.rmskin</c> pack by launching SkinInstaller.exe with the
    /// chosen file. Falls back to ShellExecute (the .rmskin file association) if SkinInstaller is missing.
    /// </summary>
    public TweakResult InstallSkinPack(string rmskinPath)
    {
        if (!System.IO.File.Exists(rmskinPath))
            return TweakResult.Fail("File not found.", "搵唔到檔案。");
        try
        {
            var installer = SkinInstallerPath;
            if (installer is not null)
                Process.Start(new ProcessStartInfo { FileName = installer, Arguments = Q(rmskinPath), UseShellExecute = true });
            else
                Process.Start(new ProcessStartInfo { FileName = rmskinPath, UseShellExecute = true });
            return TweakResult.Ok("Skin installer launched — follow its prompts.",
                "已開皮膚安裝器 — 跟住佢嘅指示做。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>開資料夾（Skins／Layouts／Settings）· Open a discovered folder in Explorer.</summary>
    public TweakResult OpenFolder(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return TweakResult.Fail("Folder not found.", "搵唔到資料夾。");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return TweakResult.Ok("Opened.", "已開啟。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }
}
