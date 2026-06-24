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
/// 一條鍵盤綁定（指令 ↔ 按鍵）· One keybinding parsed out of the GlazeWM config (commands ↔ bindings).
/// </summary>
public sealed class GlazeBinding
{
    public string Commands { get; set; } = "";   // joined by " ; " for display
    public string Keys { get; set; } = "";        // joined by ", "
}

/// <summary>
/// 應用程式內 GlazeWM 控制 · In-app control for GlazeWM, the open-source tiling window manager.
/// 薄薄包住官方二進位（經 winget 安裝）：偵測安裝、起／停／重載 daemon、讀寫 config.yaml。
/// A thin wrapper over the official binaries (installed via winget): detects the install, starts/stops/
/// reloads the daemon, and reads/writes config.yaml. Defensive throughout — never throws.
///
/// 重要路徑 · Key paths:
///   daemon : C:\Program Files\glzr.io\glazewm.exe
///   CLI    : C:\Program Files\glzr.io\cli\glazewm.exe (added to PATH by the installer)
///   config : %USERPROFILE%\.glzr\glazewm\config.yaml
///
/// Config 編輯刻意採用「結構化 + 原文」混合：只用逐行替換改動已建模嘅鍵，
/// 保留註解同未建模嘅鍵，避免成個檔案重新序列化（會掉註解）。
/// Config editing is deliberately a "structured + raw" hybrid: modeled keys are changed by targeted
/// line edits so comments and unmodeled keys survive — no full re-serialization (which would drop comments).
/// </summary>
public static class GlazeWmService
{
    /// <summary>winget 套件 ID · The winget package ID for installs.</summary>
    public const string WingetId = "glzr-io.glazewm";

    private static readonly string CliDefault = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "glzr.io", "cli", "glazewm.exe");
    private static readonly string DaemonDefault = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "glzr.io", "glazewm.exe");

    /// <summary>config.yaml 嘅預設路徑 · The default path to config.yaml.</summary>
    public static string DefaultConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".glzr", "glazewm", "config.yaml");

    // 使用者揀咗其他 config 路徑就記住（FileDialogs）· remember a non-default config path chosen via FileDialogs.
    private static string? _configOverride;

    /// <summary>目前生效嘅 config 路徑 · The config path currently in effect (override or default).</summary>
    public static string ConfigPath
    {
        get => string.IsNullOrEmpty(_configOverride) ? DefaultConfigPath : _configOverride!;
        set => _configOverride = value;
    }

    // ===== install detection =====

    /// <summary>搵到 CLI 嘅 glazewm.exe（PATH 或預設安裝路徑）· Resolve the CLI glazewm.exe (PATH or default install path).</summary>
    public static string CliPath()
    {
        if (File.Exists(CliDefault)) return CliDefault;
        var onPath = OnPath("glazewm.exe");
        return onPath ?? "glazewm"; // last resort: rely on PATH resolution at launch time
    }

    /// <summary>搵到 daemon 嘅 glazewm.exe · Resolve the daemon glazewm.exe.</summary>
    public static string DaemonPath()
        => File.Exists(DaemonDefault) ? DaemonDefault : CliPath();

    /// <summary>GlazeWM 裝咗未（搵 exe 或 PATH 上嘅 CLI）· True if GlazeWM appears installed.</summary>
    public static async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        if (File.Exists(CliDefault) || File.Exists(DaemonDefault)) return true;
        try
        {
            var output = await ShellRunner.Capture("glazewm", "--version", ct);
            return output.Trim().Length > 0
                && !output.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("not found", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string? OnPath(string exe)
    {
        try
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            foreach (var dir in paths)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var full = Path.Combine(dir.Trim(), exe);
                if (File.Exists(full)) return full;
            }
        }
        catch { }
        return null;
    }

    // ===== running state =====

    /// <summary>GlazeWM daemon 而家行緊未 · Whether the GlazeWM daemon process is currently running.</summary>
    public static bool IsRunning()
    {
        try { return Process.GetProcessesByName("glazewm").Length > 0; }
        catch { return false; }
    }

    /// <summary>已安裝版本字串 · The installed version string ("glazewm --version"), or empty.</summary>
    public static async Task<string> VersionAsync(CancellationToken ct = default)
    {
        try
        {
            var cli = CliPath();
            var r = await ShellRunner.Capture(cli, "--version", ct);
            return r.Trim();
        }
        catch { return string.Empty; }
    }

    // ===== process control =====

    /// <summary>
    /// 啟動 GlazeWM daemon · Start the GlazeWM daemon (using the override config path if one was set).
    /// 用 ShellExecute 啟動，等佢自己背景行，唔阻塞 · launched detached so it runs in the background.
    /// </summary>
    public static TweakResult Start()
    {
        try
        {
            if (IsRunning())
                return TweakResult.Ok("GlazeWM is already running.", "GlazeWM 已經喺度行緊。");

            var exe = DaemonPath();
            var args = string.IsNullOrEmpty(_configOverride) ? "" : $"--config \"{_configOverride}\"";
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var p = Process.Start(psi);
            return p is not null
                ? TweakResult.Ok("GlazeWM started.", "GlazeWM 已啟動。")
                : TweakResult.Fail("Failed to start GlazeWM.", "無法啟動 GlazeWM。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Failed to start GlazeWM: {ex.Message}", $"無法啟動 GlazeWM：{ex.Message}");
        }
    }

    /// <summary>
    /// 停止 GlazeWM · Stop GlazeWM cleanly via "glazewm command wm-exit"; falls back to killing the process.
    /// </summary>
    public static async Task<TweakResult> StopAsync(CancellationToken ct = default)
    {
        if (!IsRunning())
            return TweakResult.Ok("GlazeWM is not running.", "GlazeWM 而家冇行緊。");

        // 優先用 CLI 乾淨退出（會還原所有視窗）· prefer a clean CLI exit (restores all managed windows).
        try
        {
            var r = await Command("wm-exit", ct);
            // 等一陣畀 daemon 收手 · give the daemon a moment to wind down.
            for (int i = 0; i < 20 && IsRunning(); i++) await Task.Delay(100, ct);
            if (!IsRunning())
                return TweakResult.Ok("GlazeWM stopped.", "GlazeWM 已停止。");
        }
        catch { /* fall through to kill */ }

        // 退返用直接終止 · fall back to a hard kill.
        try
        {
            foreach (var p in Process.GetProcessesByName("glazewm"))
            {
                try { p.Kill(true); } catch { }
            }
            // watcher 進程亦一併收 · also clean up the watchdog if present.
            foreach (var p in Process.GetProcessesByName("glazewm-watcher"))
            {
                try { p.Kill(true); } catch { }
            }
            return TweakResult.Ok("GlazeWM stopped (process killed).", "GlazeWM 已停止（強制終止）。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Failed to stop GlazeWM: {ex.Message}", $"無法停止 GlazeWM：{ex.Message}");
        }
    }

    /// <summary>重新載入 config · Reload the config via "glazewm command wm-reload-config".</summary>
    public static async Task<TweakResult> ReloadAsync(CancellationToken ct = default)
    {
        if (!IsRunning())
            return TweakResult.Fail("GlazeWM is not running — start it first.", "GlazeWM 未行緊 — 請先啟動。");
        var r = await Command("wm-reload-config", ct);
        return r.Success
            ? TweakResult.Ok("Config reloaded.", "設定已重新載入。", r.Output)
            : TweakResult.Fail("Reload failed.", "重新載入失敗。", r.Output);
    }

    /// <summary>行一句 "glazewm command &lt;cmd&gt;" · Run "glazewm command &lt;cmd&gt;" and capture output.</summary>
    public static Task<TweakResult> Command(string wmCommand, CancellationToken ct = default)
    {
        try { return ShellRunner.Run(CliPath(), $"command {wmCommand}", false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    /// <summary>行一句 "glazewm query &lt;q&gt;" · Run "glazewm query &lt;q&gt;" and capture output.</summary>
    public static Task<TweakResult> Query(string query, CancellationToken ct = default)
    {
        try { return ShellRunner.Run(CliPath(), $"query {query}", false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    /// <summary>行任意 glazewm CLI 參數 · Run arbitrary glazewm CLI args (for quick actions).</summary>
    public static Task<TweakResult> RunRaw(string args, CancellationToken ct = default)
    {
        try { return ShellRunner.Run(CliPath(), args, false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }

    // ===== "Start with Windows" =====

    private const string SelfRunName = "GlazeWM";

    /// <summary>GlazeWM 開機自啟動開咗未 · Whether GlazeWM is set to launch at login (HKCU Run).</summary>
    public static bool IsStartWithWindows()
    {
        var v = RegistryHelper.GetValue(RegRoot.HKCU,
            @"Software\Microsoft\Windows\CurrentVersion\Run", SelfRunName) as string;
        return !string.IsNullOrEmpty(v);
    }

    /// <summary>設定 GlazeWM 開機自啟動 · Enable/disable launching GlazeWM at login via HKCU\...\Run.</summary>
    public static void SetStartWithWindows(bool enabled)
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        if (enabled)
        {
            var exe = DaemonPath();
            var cmd = string.IsNullOrEmpty(_configOverride)
                ? $"\"{exe}\""
                : $"\"{exe}\" --config \"{_configOverride}\"";
            RegistryHelper.SetValue(RegRoot.HKCU, key, SelfRunName, cmd, Microsoft.Win32.RegistryValueKind.String);
        }
        else
        {
            RegistryHelper.DeleteValue(RegRoot.HKCU, key, SelfRunName);
        }
    }

    // ===== config: read / write (structured + raw hybrid) =====

    /// <summary>config.yaml 存在未 · Whether the config file exists.</summary>
    public static bool ConfigExists() => File.Exists(ConfigPath);

    /// <summary>讀成個 config.yaml 嘅原文 · Read the raw text of config.yaml (empty string if missing).</summary>
    public static string ReadConfig()
    {
        try { return ConfigExists() ? File.ReadAllText(ConfigPath) : string.Empty; }
        catch { return string.Empty; }
    }

    /// <summary>寫返成個 config.yaml · Overwrite config.yaml with the given raw text (creates folders).</summary>
    public static TweakResult WriteConfig(string text)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, text);
            return TweakResult.Ok("Config saved.", "設定已儲存。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Failed to save config: {ex.Message}", $"無法儲存設定：{ex.Message}");
        }
    }

    /// <summary>
    /// 由官方範本建立預設 config · Create a default config from the bundled sample (only if absent).
    /// </summary>
    public static TweakResult CreateDefaultConfig()
    {
        try
        {
            if (ConfigExists())
                return TweakResult.Ok("Config already exists.", "設定檔已存在。");
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(ConfigPath, SampleConfig);
            return TweakResult.Ok("Default config created.", "已建立預設設定檔。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Failed to create config: {ex.Message}", $"無法建立設定檔：{ex.Message}");
        }
    }

    // ----- structured getters (read a single scalar under a top-level section) -----

    /// <summary>讀內邊距（gaps.inner_gap，例如 "20px"）· Read gaps.inner_gap (e.g. "20px").</summary>
    public static string GetInnerGap() => ScalarUnder("gaps", "inner_gap") ?? "";

    /// <summary>讀外邊距 top（gaps.outer_gap.top）· Read gaps.outer_gap.top.</summary>
    public static string GetOuterGapTop() => ScalarUnder2("gaps", "outer_gap", "top") ?? GetOuterGapFlat() ?? "";

    /// <summary>focus_follows_cursor 開咗未 · Read general.focus_follows_cursor.</summary>
    public static bool GetFocusFollowsCursor()
        => string.Equals(ScalarUnder("general", "focus_follows_cursor"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>讀 general.startup_commands 嘅原文（YAML 行內陣列）· Read the raw general.startup_commands list line.</summary>
    public static string GetStartupCommands() => ScalarUnder("general", "startup_commands") ?? "";

    // ----- structured setters (targeted line edits; preserve everything else) -----

    public static TweakResult SetInnerGap(string value) => SetScalarUnder("gaps", "inner_gap", Quote(value));
    public static TweakResult SetFocusFollowsCursor(bool on) => SetScalarUnder("general", "focus_follows_cursor", on ? "true" : "false");
    public static TweakResult SetStartupCommands(string rawListValue) => SetScalarUnder("general", "startup_commands", rawListValue);

    /// <summary>設定 gaps.outer_gap.top（支援巢狀或扁平寫法）· Set gaps.outer_gap.top (nested or flat form).</summary>
    public static TweakResult SetOuterGapTop(string value)
    {
        var nested = SetScalarUnder2("gaps", "outer_gap", "top", Quote(value));
        if (nested.Success) return nested;
        // 扁平寫法（outer_gap: '20px'）· flat form (outer_gap: '20px').
        return SetScalarUnder("gaps", "outer_gap", Quote(value));
    }

    private static string? GetOuterGapFlat() => ScalarUnder("gaps", "outer_gap");

    // ----- workspaces -----

    /// <summary>讀工作區名稱清單 · Read the list of workspace names under "workspaces:".</summary>
    public static List<string> GetWorkspaces()
    {
        var result = new List<string>();
        var lines = ReadLines();
        int idx = TopLevelIndex(lines, "workspaces");
        if (idx < 0) return result;
        for (int i = idx + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.TrimStart().StartsWith("#"))
                break; // next top-level key
            var t = line.TrimStart();
            // entries look like "- name: '1'" or "- name: 1"
            int n = t.IndexOf("name:", StringComparison.Ordinal);
            if (t.StartsWith("-") && n >= 0)
            {
                var val = t.Substring(n + "name:".Length).Trim().Trim('\'', '"');
                if (val.Length > 0) result.Add(val);
            }
        }
        return result;
    }

    /// <summary>
    /// 用新嘅名單重寫 workspaces 區塊 · Replace the "workspaces:" block with the given names.
    /// 保留其餘檔案不變 · everything outside the block is preserved verbatim.
    /// </summary>
    public static TweakResult SetWorkspaces(IEnumerable<string> names)
    {
        try
        {
            var lines = ReadLines();
            int idx = TopLevelIndex(lines, "workspaces");
            var newBlock = new List<string> { "workspaces:" };
            foreach (var n in names)
            {
                var clean = n.Trim();
                if (clean.Length == 0) continue;
                newBlock.Add($"  - name: '{clean.Replace("'", "''")}'");
            }

            if (idx < 0)
            {
                // append a fresh block at the end.
                if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add("");
                lines.AddRange(newBlock);
            }
            else
            {
                int end = idx + 1;
                while (end < lines.Count)
                {
                    var line = lines[end];
                    if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.TrimStart().StartsWith("#"))
                        break;
                    end++;
                }
                lines.RemoveRange(idx, end - idx);
                lines.InsertRange(idx, newBlock);
            }

            return WriteConfig(string.Join("\n", lines));
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Failed to update workspaces: {ex.Message}", $"無法更新工作區：{ex.Message}");
        }
    }

    // ----- keybinding read (display-only parse) -----

    /// <summary>
    /// 解析 keybindings 區塊做一覽表 · Parse the top-level "keybindings:" block into a display list.
    /// 純展示用（指令 + 按鍵）· display-only (commands + bindings); edits go through the raw YAML editor.
    /// </summary>
    public static List<GlazeBinding> GetKeybindings()
    {
        var result = new List<GlazeBinding>();
        var lines = ReadLines();
        int idx = TopLevelIndex(lines, "keybindings");
        if (idx < 0) return result;

        GlazeBinding? cur = null;
        for (int i = idx + 1; i < lines.Count; i++)
        {
            var raw = lines[i];
            if (raw.Length > 0 && !char.IsWhiteSpace(raw[0]) && !raw.TrimStart().StartsWith("#"))
                break; // next top-level key
            var t = raw.TrimStart();
            if (t.StartsWith("#") || t.Length == 0) continue;

            if (t.StartsWith("- commands:"))
            {
                if (cur is not null) result.Add(cur);
                cur = new GlazeBinding { Commands = ExtractList(t.Substring("- commands:".Length)) };
            }
            else if (t.StartsWith("commands:") && cur is not null)
            {
                cur.Commands = ExtractList(t.Substring("commands:".Length));
            }
            else if (t.StartsWith("bindings:") && cur is not null)
            {
                cur.Keys = ExtractList(t.Substring("bindings:".Length));
            }
        }
        if (cur is not null) result.Add(cur);
        return result;
    }

    private static string ExtractList(string inlineArray)
    {
        // turn "['a', 'b']" into "a, b"; turn "['focus --direction left']" into "focus --direction left".
        var s = inlineArray.Trim();
        s = s.Trim('[', ']');
        var parts = s.Split(',')
            .Select(p => p.Trim().Trim('\'', '"'))
            .Where(p => p.Length > 0);
        return string.Join(" ; ", parts);
    }

    // ===== low-level line helpers =====

    private static List<string> ReadLines()
    {
        var text = ReadConfig();
        return text.Replace("\r\n", "\n").Split('\n').ToList();
    }

    private static int TopLevelIndex(List<string> lines, string key)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0])) continue;
            var t = line.TrimEnd();
            if (t.StartsWith(key + ":")) return i;
        }
        return -1;
    }

    /// <summary>讀某個 top-level 區塊下、縮排一層嘅純量值 · Read a scalar one indent level under a top-level section.</summary>
    private static string? ScalarUnder(string section, string key)
    {
        var lines = ReadLines();
        int idx = TopLevelIndex(lines, section);
        if (idx < 0) return null;
        for (int i = idx + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.TrimStart().StartsWith("#")) break;
            var t = line.TrimStart();
            if (t.StartsWith(key + ":"))
            {
                var val = t.Substring(key.Length + 1).Trim();
                int hash = ValueCommentIndex(val);
                if (hash >= 0) val = val.Substring(0, hash).Trim();
                return val.Trim('\'', '"');
            }
        }
        return null;
    }

    private static string? ScalarUnder2(string section, string sub, string key)
    {
        var lines = ReadLines();
        int idx = TopLevelIndex(lines, section);
        if (idx < 0) return null;
        bool inSub = false;
        for (int i = idx + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.TrimStart().StartsWith("#")) break;
            var t = line.TrimStart();
            if (!inSub) { if (t.StartsWith(sub + ":")) inSub = true; continue; }
            // inside the sub-map; stop if we hit another key at the sub's indent level (1 space deeper than section)
            if (t.StartsWith(key + ":"))
            {
                var val = t.Substring(key.Length + 1).Trim();
                int hash = ValueCommentIndex(val);
                if (hash >= 0) val = val.Substring(0, hash).Trim();
                return val.Trim('\'', '"');
            }
            // a sibling sub-map key (less indented than key but still under section) ends our search window only loosely;
            // keep scanning until the section ends.
        }
        return null;
    }

    private static TweakResult SetScalarUnder(string section, string key, string newValueText)
    {
        try
        {
            var lines = ReadLines();
            int idx = TopLevelIndex(lines, section);
            if (idx < 0) return TweakResult.Fail($"Section '{section}' not found in config.", $"設定中搵唔到「{section}」區塊。");
            for (int i = idx + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.TrimStart().StartsWith("#")) break;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith(key + ":"))
                {
                    var indent = line.Substring(0, line.Length - trimmed.Length);
                    // preserve any trailing inline comment.
                    var after = trimmed.Substring(key.Length + 1);
                    int hash = ValueCommentIndex(after);
                    var comment = hash >= 0 ? " " + after.Substring(hash).Trim() : "";
                    lines[i] = $"{indent}{key}: {newValueText}{comment}";
                    return WriteConfig(string.Join("\n", lines));
                }
            }
            return TweakResult.Fail($"Key '{key}' not found under '{section}'.", $"喺「{section}」下搵唔到「{key}」。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    private static TweakResult SetScalarUnder2(string section, string sub, string key, string newValueText)
    {
        try
        {
            var lines = ReadLines();
            int idx = TopLevelIndex(lines, section);
            if (idx < 0) return TweakResult.Fail($"Section '{section}' not found.", $"搵唔到「{section}」區塊。");
            bool inSub = false;
            for (int i = idx + 1; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !line.TrimStart().StartsWith("#")) break;
                var trimmed = line.TrimStart();
                if (!inSub) { if (trimmed.StartsWith(sub + ":")) inSub = true; continue; }
                if (trimmed.StartsWith(key + ":"))
                {
                    var indent = line.Substring(0, line.Length - trimmed.Length);
                    var after = trimmed.Substring(key.Length + 1);
                    int hash = ValueCommentIndex(after);
                    var comment = hash >= 0 ? " " + after.Substring(hash).Trim() : "";
                    lines[i] = $"{indent}{key}: {newValueText}{comment}";
                    return WriteConfig(string.Join("\n", lines));
                }
            }
            return TweakResult.Fail($"Key '{sub}.{key}' not found (config may use flat form).",
                $"搵唔到「{sub}.{key}」（設定可能用扁平寫法）。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>搵值後面個 inline 註解 # 嘅位置（避免誤中引號內嘅 #）· Locate an inline "# comment" outside quotes.</summary>
    private static int ValueCommentIndex(string value)
    {
        bool inSingle = false, inDouble = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '\'' && !inDouble) inSingle = !inSingle;
            else if (c == '"' && !inSingle) inDouble = !inDouble;
            else if (c == '#' && !inSingle && !inDouble && i > 0 && char.IsWhiteSpace(value[i - 1]))
                return i;
        }
        return -1;
    }

    private static string Quote(string value)
    {
        var v = value.Trim();
        if (v.Length == 0) return "''";
        // already an inline array / number / bool → leave as-is.
        if (v.StartsWith("[") || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("false", StringComparison.OrdinalIgnoreCase)) return v;
        if (v.StartsWith("'") || v.StartsWith("\"")) return v;
        return $"'{v.Replace("'", "''")}'";
    }

    /// <summary>官方範本嘅精簡版（建立預設 config 用）· A trimmed copy of the official sample (for first-run config).</summary>
    public const string SampleConfig =
@"general:
  # Commands to run when the WM has started.
  startup_commands: []

  # Commands to run just before the WM is shutdown.
  shutdown_commands: []

  # Commands to run after the WM config is reloaded.
  config_reload_commands: []

  # Whether to automatically focus windows underneath the cursor.
  focus_follows_cursor: false

  # How windows should be hidden when switching workspaces.
  hide_method: 'cloak'

gaps:
  # Whether to scale the gaps with the DPI of the monitor.
  scale_with_dpi: true

  # Gap between adjacent windows.
  inner_gap: '20px'

  # Gap between windows and the screen edge.
  outer_gap:
    top: '20px'
    right: '20px'
    bottom: '20px'
    left: '20px'

window_behavior:
  # New windows are created in this state whenever possible.
  initial_state: 'tiling'

workspaces:
  - name: '1'
  - name: '2'
  - name: '3'
  - name: '4'
  - name: '5'

keybindings:
  # Shift focus in a given direction.
  - commands: ['focus --direction left']
    bindings: ['alt+h', 'alt+left']
  - commands: ['focus --direction right']
    bindings: ['alt+l', 'alt+right']
  - commands: ['focus --direction up']
    bindings: ['alt+k', 'alt+up']
  - commands: ['focus --direction down']
    bindings: ['alt+j', 'alt+down']

  # Move the focused window in a given direction.
  - commands: ['move --direction left']
    bindings: ['alt+shift+h', 'alt+shift+left']
  - commands: ['move --direction right']
    bindings: ['alt+shift+l', 'alt+shift+right']
  - commands: ['move --direction up']
    bindings: ['alt+shift+k', 'alt+shift+up']
  - commands: ['move --direction down']
    bindings: ['alt+shift+j', 'alt+shift+down']

  # Toggle floating / fullscreen / minimize, and close.
  - commands: ['toggle-floating --centered']
    bindings: ['alt+shift+space']
  - commands: ['toggle-fullscreen']
    bindings: ['alt+f']
  - commands: ['toggle-minimized']
    bindings: ['alt+m']
  - commands: ['close']
    bindings: ['alt+shift+q']

  # Exit / reload GlazeWM.
  - commands: ['wm-exit']
    bindings: ['alt+shift+e']
  - commands: ['wm-reload-config']
    bindings: ['alt+shift+r']

  # Focus a workspace.
  - commands: ['focus --workspace 1']
    bindings: ['alt+1']
  - commands: ['focus --workspace 2']
    bindings: ['alt+2']
  - commands: ['focus --workspace 3']
    bindings: ['alt+3']
  - commands: ['focus --workspace 4']
    bindings: ['alt+4']
  - commands: ['focus --workspace 5']
    bindings: ['alt+5']

  # Move the focused window to a workspace.
  - commands: ['move --workspace 1', 'focus --workspace 1']
    bindings: ['alt+shift+1']
  - commands: ['move --workspace 2', 'focus --workspace 2']
    bindings: ['alt+shift+2']
  - commands: ['move --workspace 3', 'focus --workspace 3']
    bindings: ['alt+shift+3']
  - commands: ['move --workspace 4', 'focus --workspace 4']
    bindings: ['alt+shift+4']
  - commands: ['move --workspace 5', 'focus --workspace 5']
    bindings: ['alt+shift+5']
";
}
