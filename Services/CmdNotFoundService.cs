using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 「搵唔到指令」引擎 · The "Command Not Found" engine — a native clone of PowerToys' Command Not Found.
///
/// 點運作 · How it works:
/// PowerShell 7（pwsh）有個實驗功能 <c>PSFeedbackProvider</c> 同 <c>PSCommandNotFoundSuggestion</c>，
/// 配合官方模組 <c>Microsoft.WinGet.CommandNotFound</c>，當你打錯／打咗一個未安裝嘅指令，
/// PowerShell 會建議邊個 winget 套件可以裝。WinForge 喺你嘅 PowerShell 7 <c>$PROFILE</c> 入面
/// 安全咁加入 <c>Import-Module</c> 掛鈎（連 GUID 標記、自動備份），提供開／關／更新／測試。
///
/// PowerShell 7 (pwsh) ships experimental features <c>PSFeedbackProvider</c> and
/// <c>PSCommandNotFoundSuggestion</c>; together with the official
/// <c>Microsoft.WinGet.CommandNotFound</c> module, typing a missing command makes PowerShell
/// suggest the winget package that provides it. WinForge writes the <c>Import-Module</c> hook into
/// the user's PowerShell 7 <c>$PROFILE</c> safely (GUID-marked block, automatic backup) and offers
/// enable / disable / update / test. Defensive throughout — never throws.
/// </summary>
public static class CmdNotFoundService
{
    /// <summary>PowerToys 用嘅 GUID 標記（保持相容）· The marker GUID PowerToys uses (kept compatible).</summary>
    public const string MarkerGuid = "f45873b3-b655-43a6-b217-97c00aa0db58";

    /// <summary>舊版 GUID（升級時清走）· The legacy GUID (cleaned up on enable/disable).</summary>
    public const string LegacyGuid = "34de4b3d-13a8-4540-b76d-b9e8d3851756";

    /// <summary>官方 winget 建議模組 · The official WinGet suggestion module.</summary>
    public const string CnfModule = "Microsoft.WinGet.CommandNotFound";

    /// <summary>winget 客戶端模組（CNF 嘅相依）· The WinGet client module (a CNF dependency).</summary>
    public const string ClientModule = "Microsoft.WinGet.Client";

    private static string? _pwshPath;

    // ── pwsh 偵測 · pwsh discovery ─────────────────────────────────────────────

    /// <summary>
    /// 解析 pwsh.exe（PATH 或常見安裝位置）· Resolve pwsh.exe (PATH or well-known install dirs).
    /// 揾唔到就回傳 "pwsh"（靠 PATH）· falls back to plain "pwsh" (relies on PATH).
    /// </summary>
    public static string PwshPath
    {
        get
        {
            if (!string.IsNullOrEmpty(_pwshPath)) return _pwshPath!;
            _pwshPath = "pwsh";
            try
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string[] candidates =
                {
                    Path.Combine(pf, "PowerShell", "7", "pwsh.exe"),
                    Path.Combine(pf, "PowerShell", "7-preview", "pwsh.exe"),
                    Path.Combine(pf86, "PowerShell", "7", "pwsh.exe"),
                    Path.Combine(local, "Microsoft", "WindowsApps", "pwsh.exe"),
                };
                foreach (var c in candidates)
                    if (File.Exists(c)) { _pwshPath = c; break; }
            }
            catch { }
            return _pwshPath!;
        }
    }

    /// <summary>清掉 pwsh 路徑快取（裝完 PowerShell 7 之後）· Clear the cached path after installing pwsh.</summary>
    public static void Rescan() => _pwshPath = null;

    // ── 內部：行 pwsh 並擷取輸出 · internal: run pwsh and capture output ───────────

    /// <summary>用 -EncodedCommand 行一段 pwsh 腳本（避免引號地獄）· Run pwsh via -EncodedCommand.</summary>
    private static async Task<string> RunPwsh(string script, CancellationToken ct, bool useProfile = false)
    {
        try
        {
            var bytes = Encoding.Unicode.GetBytes(script);
            var encoded = Convert.ToBase64String(bytes);
            var profileFlag = useProfile ? "" : "-NoProfile ";
            var args = $"{profileFlag}-NoLogo -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}";
            return await ShellRunner.Capture(PwshPath, args, ct);
        }
        catch { return string.Empty; }
    }

    // ── 狀態 · status ──────────────────────────────────────────────────────────

    public sealed class CnfStatus
    {
        /// <summary>pwsh 裝咗未 · Is pwsh present at all.</summary>
        public bool PwshPresent;
        /// <summary>pwsh 版本字串 · The pwsh version string.</summary>
        public string PwshVersion = "";
        /// <summary>pwsh 7.4+ · Is PowerShell ≥ 7.4 (required for the feedback provider).</summary>
        public bool PwshOk;
        /// <summary>WinGet 客戶端模組 · Microsoft.WinGet.Client present.</summary>
        public bool ClientModulePresent;
        /// <summary>WinGet 客戶端模組版本夠新 (≥1.8.1133)· Client module new enough.</summary>
        public bool ClientModuleUpToDate;
        /// <summary>CommandNotFound 模組 · Microsoft.WinGet.CommandNotFound present.</summary>
        public bool CnfModulePresent;
        /// <summary>$PROFILE 路徑 · The resolved $PROFILE path.</summary>
        public string ProfilePath = "";
        /// <summary>profile 入面有冇掛鈎 · Is the hook registered in the profile.</summary>
        public bool HookEnabled;
        /// <summary>profile 入面有舊版掛鈎 · Legacy hook present (suggest re-enable to upgrade).</summary>
        public bool LegacyHookPresent;
        /// <summary>實驗功能 PSFeedbackProvider 已開 · experimental feature enabled.</summary>
        public bool FeedbackProviderEnabled;
        /// <summary>實驗功能 PSCommandNotFoundSuggestion 已開 · experimental feature enabled.</summary>
        public bool SuggestionFeatureEnabled;
        /// <summary>raw 偵測輸出（畀進階用戶睇）· raw detection text for the curious.</summary>
        public string RawOutput = "";

        /// <summary>全部就緒（會建議套件）· Fully wired up — suggestions will appear in pwsh.</summary>
        public bool FullyReady => PwshOk && CnfModulePresent && HookEnabled;
    }

    /// <summary>
    /// 偵測 pwsh / 模組 / profile 掛鈎 / 實驗功能嘅狀態 · Probe pwsh, modules, the profile hook and
    /// experimental features in one pwsh round-trip. Returns a populated <see cref="CnfStatus"/>.
    /// </summary>
    public static async Task<CnfStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var s = new CnfStatus();

        // 1) pwsh 在唔在 + 版本 · is pwsh there + version (and experimental-feature names).
        const string probe = @"
$out = [ordered]@{}
$out.PSVersion = $PSVersionTable.PSVersion.ToString()
$feats = (Get-ExperimentalFeature -ErrorAction SilentlyContinue)
$out.FeedbackEnabled = [bool]($feats | Where-Object { $_.Name -eq 'PSFeedbackProvider' -and $_.Enabled })
$out.SuggestionEnabled = [bool]($feats | Where-Object { $_.Name -eq 'PSCommandNotFoundSuggestion' -and $_.Enabled })
$client = Get-Module -ListAvailable -Name Microsoft.WinGet.Client -ErrorAction SilentlyContinue
$out.ClientPresent = [bool]$client
$out.ClientUpToDate = [bool]($client | Where-Object { $_.Version -ge [version]'1.8.1133' })
$out.CnfPresent = [bool](Get-Module -ListAvailable -Name Microsoft.WinGet.CommandNotFound -ErrorAction SilentlyContinue)
$out.ProfilePath = $PROFILE
$out | ConvertTo-Json -Compress
";
        var raw = await RunPwsh(probe, ct);
        s.RawOutput = raw;

        if (string.IsNullOrWhiteSpace(raw))
        {
            // pwsh 唔喺度 · pwsh not present / not launchable.
            s.PwshPresent = false;
            return s;
        }

        s.PwshPresent = true;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(ExtractJson(raw));
            var root = doc.RootElement;
            s.PwshVersion = GetStr(root, "PSVersion");
            s.PwshOk = TryVersionAtLeast(s.PwshVersion, 7, 4);
            s.FeedbackProviderEnabled = GetBool(root, "FeedbackEnabled");
            s.SuggestionFeatureEnabled = GetBool(root, "SuggestionEnabled");
            s.ClientModulePresent = GetBool(root, "ClientPresent");
            s.ClientModuleUpToDate = GetBool(root, "ClientUpToDate");
            s.CnfModulePresent = GetBool(root, "CnfPresent");
            s.ProfilePath = GetStr(root, "ProfilePath");
        }
        catch { /* leave defaults */ }

        // 2) profile 掛鈎狀態（C# 直接讀檔，唔使再行 pwsh）· hook state read directly from the file.
        if (!string.IsNullOrEmpty(s.ProfilePath) && File.Exists(s.ProfilePath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(s.ProfilePath, ct);
                s.HookEnabled = content.Contains(MarkerGuid, StringComparison.OrdinalIgnoreCase);
                s.LegacyHookPresent = content.Contains(LegacyGuid, StringComparison.OrdinalIgnoreCase);
            }
            catch { }
        }

        return s;
    }

    /// <summary>讀 $PROFILE 文字（畀「檢視 profile」用）· Read the profile text for the "View profile" pane.</summary>
    public static async Task<string> ReadProfileAsync(string profilePath, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(profilePath) || !File.Exists(profilePath))
                return string.Empty;
            return await File.ReadAllTextAsync(profilePath, ct);
        }
        catch (Exception ex) { return $"<error: {ex.Message}>"; }
    }

    // ── 安裝模組 · install module ──────────────────────────────────────────────

    /// <summary>
    /// 確保官方 CommandNotFound 模組已安裝 · Ensure the official CNF module is installed
    /// (installs the WinGet.Client dependency too if missing). Returns the captured transcript.
    /// </summary>
    public static async Task<TweakResult> InstallModuleAsync(CancellationToken ct = default)
    {
        const string script = @"
$ErrorActionPreference = 'Stop'
try {
  if (-not (Get-Module -ListAvailable -Name Microsoft.WinGet.Client)) {
    Write-Host 'Installing Microsoft.WinGet.Client...'
    Install-Module -Name Microsoft.WinGet.Client -Force -Scope CurrentUser -Repository PSGallery
  } else {
    Write-Host 'Microsoft.WinGet.Client already present.'
  }
  if (-not (Get-Module -ListAvailable -Name Microsoft.WinGet.CommandNotFound)) {
    Write-Host 'Installing Microsoft.WinGet.CommandNotFound...'
    Install-Module -Name Microsoft.WinGet.CommandNotFound -Force -Scope CurrentUser -Repository PSGallery
    Write-Host 'INSTALL_OK'
  } else {
    Write-Host 'Microsoft.WinGet.CommandNotFound already present.'
    Write-Host 'INSTALL_OK'
  }
} catch {
  Write-Host (""INSTALL_FAIL: "" + $_.Exception.Message)
}
";
        var output = await RunPwsh(script, ct);
        if (output.Contains("INSTALL_OK", StringComparison.Ordinal))
            return TweakResult.Ok("Module ready.", "模組已就緒。", output.Trim());
        return TweakResult.Fail(
            "Could not install the module. See the log.",
            "未能安裝模組，請睇記錄。", output.Trim());
    }

    /// <summary>更新模組 · Update both modules to their latest versions.</summary>
    public static async Task<TweakResult> UpdateModuleAsync(CancellationToken ct = default)
    {
        const string script = @"
$ErrorActionPreference = 'Continue'
try {
  if (Get-Module -ListAvailable -Name Microsoft.WinGet.Client) {
    Write-Host 'Updating Microsoft.WinGet.Client...'
    Update-Module -Name Microsoft.WinGet.Client -Force -ErrorAction SilentlyContinue
  } else {
    Install-Module -Name Microsoft.WinGet.Client -Force -Scope CurrentUser -ErrorAction SilentlyContinue
  }
  if (Get-Module -ListAvailable -Name Microsoft.WinGet.CommandNotFound) {
    Write-Host 'Updating Microsoft.WinGet.CommandNotFound...'
    Update-Module -Name Microsoft.WinGet.CommandNotFound -Force -ErrorAction SilentlyContinue
  } else {
    Install-Module -Name Microsoft.WinGet.CommandNotFound -Force -Scope CurrentUser -ErrorAction SilentlyContinue
  }
  Write-Host 'UPDATE_OK'
} catch {
  Write-Host (""UPDATE_FAIL: "" + $_.Exception.Message)
}
";
        var output = await RunPwsh(script, ct);
        if (output.Contains("UPDATE_OK", StringComparison.Ordinal))
            return TweakResult.Ok("Modules updated.", "模組已更新。", output.Trim());
        return TweakResult.Fail("Update failed. See the log.", "更新失敗，請睇記錄。", output.Trim());
    }

    // ── 開啟掛鈎 · enable the hook ─────────────────────────────────────────────

    /// <summary>
    /// 開啟「搵唔到指令」· Enable Command Not Found.
    /// 步驟 · Steps:
    ///  1. 開實驗功能（PSFeedbackProvider / PSCommandNotFoundSuggestion）· enable experimental features
    ///  2. 確保模組已安裝 · ensure the module is installed (caller should install first)
    ///  3. 安全咁喺 $PROFILE 加入 GUID 標記嘅 Import-Module 區塊（先備份）·
    ///     safely add the GUID-marked Import-Module block to $PROFILE (after a backup).
    /// </summary>
    public static async Task<TweakResult> EnableAsync(CnfStatus status, CancellationToken ct = default)
    {
        // 1) 開實驗功能（會喺新 session 生效）· enable experimental features (effective next session).
        const string feats = @"
$ErrorActionPreference = 'SilentlyContinue'
$names = (Get-ExperimentalFeature).Name
if ($names -contains 'PSFeedbackProvider') { Enable-ExperimentalFeature PSFeedbackProvider -ErrorAction SilentlyContinue }
if ($names -contains 'PSCommandNotFoundSuggestion') { Enable-ExperimentalFeature PSCommandNotFoundSuggestion -ErrorAction SilentlyContinue }
Write-Host 'FEATURES_DONE'
";
        var featOut = await RunPwsh(feats, ct);

        // 2) 解析 $PROFILE 路徑 · resolve the profile path (status may be stale).
        var profilePath = status.ProfilePath;
        if (string.IsNullOrEmpty(profilePath))
            profilePath = (await RunPwsh("$PROFILE", ct)).Trim();
        if (string.IsNullOrEmpty(profilePath))
            return TweakResult.Fail("Could not resolve the PowerShell profile path.",
                "未能取得 PowerShell profile 路徑。", featOut);

        try
        {
            // 確保 profile 目錄存在 · ensure the profile directory exists.
            var dir = Path.GetDirectoryName(profilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var content = File.Exists(profilePath) ? await File.ReadAllTextAsync(profilePath, ct) : string.Empty;

            // 已啟用就唔好重複 · already enabled → no-op (idempotent).
            if (content.Contains(MarkerGuid, StringComparison.OrdinalIgnoreCase) &&
                !content.Contains(LegacyGuid, StringComparison.OrdinalIgnoreCase))
            {
                return TweakResult.Ok("Already enabled in the profile.", "profile 入面已經啟用。",
                    $"{featOut}\nProfile: {profilePath}");
            }

            // 先備份 · back up first.
            var backup = BackupProfile(profilePath);

            // 如果有舊版掛鈎，先清走（避免重複）· strip any legacy block first.
            content = RemoveHookBlock(content);

            // 加入新區塊 · append the marker block (mirrors PowerToys' EnableModule.ps1).
            var block = new StringBuilder();
            if (content.Length > 0 && !content.EndsWith("\n")) block.Append("\r\n");
            block.Append("\r\n#").Append(MarkerGuid).Append(" WinForge CommandNotFound module\r\n");
            block.Append("Import-Module -Name ").Append(CnfModule).Append("\r\n");
            block.Append("#").Append(MarkerGuid).Append("\r\n");

            await File.WriteAllTextAsync(profilePath, content + block.ToString(), ct);

            var note = backup is null ? "" : $"\nBackup: {backup}";
            return TweakResult.Ok(
                "Enabled. Open a NEW PowerShell 7 window to start getting suggestions.",
                "已啟用。開一個新嘅 PowerShell 7 視窗就會見到建議。",
                $"{featOut}\nProfile: {profilePath}{note}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not edit the profile: {ex.Message}",
                $"未能編輯 profile：{ex.Message}", featOut);
        }
    }

    // ── 關閉掛鈎 · disable the hook ────────────────────────────────────────────

    /// <summary>
    /// 關閉「搵唔到指令」· Disable Command Not Found — remove the GUID-marked block from $PROFILE
    /// (after a backup). Modules and experimental features are left untouched.
    /// </summary>
    public static async Task<TweakResult> DisableAsync(CnfStatus status, CancellationToken ct = default)
    {
        var profilePath = status.ProfilePath;
        if (string.IsNullOrEmpty(profilePath))
            profilePath = (await RunPwsh("$PROFILE", ct)).Trim();

        if (string.IsNullOrEmpty(profilePath) || !File.Exists(profilePath))
            return TweakResult.Ok("Nothing to remove — no profile file.", "冇嘢需要移除——profile 唔存在。");

        try
        {
            var content = await File.ReadAllTextAsync(profilePath, ct);
            if (!content.Contains(MarkerGuid, StringComparison.OrdinalIgnoreCase) &&
                !content.Contains(LegacyGuid, StringComparison.OrdinalIgnoreCase))
                return TweakResult.Ok("Already disabled — no hook found.", "已經停用——profile 入面冇掛鈎。");

            var backup = BackupProfile(profilePath);
            var stripped = RemoveHookBlock(content);
            await File.WriteAllTextAsync(profilePath, stripped, ct);

            var note = backup is null ? "" : $"\nBackup: {backup}";
            return TweakResult.Ok(
                "Disabled. The hook was removed from the profile.",
                "已停用。掛鈎已從 profile 移除。",
                $"Profile: {profilePath}{note}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not edit the profile: {ex.Message}",
                $"未能編輯 profile：{ex.Message}");
        }
    }

    /// <summary>備份 profile（時間戳）· Back up the profile with a timestamp; returns the backup path or null.</summary>
    private static string? BackupProfile(string profilePath)
    {
        try
        {
            if (!File.Exists(profilePath)) return null;
            var backup = $"{profilePath}.winforge-bak-{DateTime.Now:yyyyMMdd-HHmmss}";
            File.Copy(profilePath, backup, overwrite: true);
            return backup;
        }
        catch { return null; }
    }

    /// <summary>
    /// 移走任何 GUID（新或舊）標記嘅掛鈎區塊 · Remove any hook block fenced by either GUID marker.
    /// 跟 PowerToys DisableModule.ps1 嘅做法：搵到第一個標記行就刪，直到下一個標記行。
    /// Mirrors PowerToys' DisableModule.ps1 line-fence logic but done in C#.
    /// </summary>
    private static string RemoveHookBlock(string content)
    {
        if (string.IsNullOrEmpty(content)) return content;
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        bool insideBlock = false;
        foreach (var line in lines)
        {
            bool isMarker = line.Contains(MarkerGuid, StringComparison.OrdinalIgnoreCase) ||
                            line.Contains(LegacyGuid, StringComparison.OrdinalIgnoreCase);
            if (isMarker && !insideBlock) { insideBlock = true; continue; }   // opening fence → skip
            if (isMarker && insideBlock) { insideBlock = false; continue; }   // closing fence → skip
            if (insideBlock) continue;                                        // body → skip
            sb.Append(line).Append("\r\n");
        }
        var result = sb.ToString();
        // 收尾多餘空行 · trim trailing blank lines we may have introduced.
        return result.TrimEnd('\r', '\n') + (result.Length > 0 ? "\r\n" : "");
    }

    // ── 測試 · test ────────────────────────────────────────────────────────────

    /// <summary>
    /// 測試掛鈎：喺新 pwsh（載入 profile）行一個唔存在嘅指令，攞返建議 · Test the hook: run a deliberately
    /// missing command in a fresh pwsh that loads the profile, and capture the suggestion text.
    /// </summary>
    public static async Task<TweakResult> TestAsync(string missingCommand, CancellationToken ct = default)
    {
        var cmd = string.IsNullOrWhiteSpace(missingCommand) ? "pyton" : missingCommand.Trim();
        // 用單引號保護指令名，禁止注入 · single-quote the command name; escape any embedded quotes.
        var safe = cmd.Replace("'", "''");
        var script = $@"
$ErrorActionPreference = 'SilentlyContinue'
# 行一個未必存在嘅指令，觸發 CommandNotFound 回饋 · invoke a (likely) missing command.
& '{safe}' 2>&1 | Out-String | Write-Host
";
        // useProfile:true 先會載入掛鈎 · must load the profile so the hook runs.
        var output = await RunPwsh(script, ct, useProfile: true);
        output = (output ?? string.Empty).Trim();

        bool gotSuggestion = output.Contains("winget", StringComparison.OrdinalIgnoreCase) ||
                             output.Contains("install", StringComparison.OrdinalIgnoreCase) ||
                             output.Contains("Try", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(output))
            return TweakResult.Fail(
                $"No output. The command '{cmd}' may exist, or the hook is not active in this session.",
                $"冇輸出。指令「{cmd}」可能存在，或者掛鈎喺呢個 session 未生效。", output);

        return gotSuggestion
            ? TweakResult.Ok($"Got a suggestion for '{cmd}':", $"收到「{cmd}」嘅建議：", output)
            : TweakResult.Ok(
                $"Ran '{cmd}'. If no winget hint appears below, enable the hook and try a NEW window.",
                $"已執行「{cmd}」。若下面冇 winget 提示，請啟用掛鈎再開新視窗試。", output);
    }

    // ── 直接 winget 查詢（app 內即用）· in-app winget lookup ─────────────────────

    /// <summary>
    /// 「邊個 winget 套件提供 X？」· "Which winget package provides X?" — runs <c>winget search</c>
    /// on the typed command/token and returns the table so the feature is useful inside WinForge too.
    /// </summary>
    public static async Task<TweakResult> WingetSearchAsync(string query, CancellationToken ct = default)
    {
        var q = (query ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(q))
            return TweakResult.Fail("Type a command or package name to search.",
                "請輸入指令或套件名稱嚟搜尋。");

        try
        {
            // --accept-source-agreements 避免互動提示 · avoid the interactive source prompt.
            var args = $"search --query \"{q.Replace("\"", "")}\" --accept-source-agreements --disable-interactivity";
            var r = await ShellRunner.Run("winget", args, false, ct);
            var output = (r.Output ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(output))
                return TweakResult.Fail($"No packages found for '{q}'.", $"搵唔到「{q}」嘅套件。", output);

            if (output.Contains("No package found", StringComparison.OrdinalIgnoreCase) ||
                output.Contains("找不到", StringComparison.Ordinal))
                return TweakResult.Fail($"No packages found for '{q}'.", $"搵唔到「{q}」嘅套件。", output);

            return TweakResult.Ok($"Packages matching '{q}':", $"符合「{q}」嘅套件：", output);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"winget search failed: {ex.Message}",
                $"winget 搜尋失敗：{ex.Message}");
        }
    }

    /// <summary>開啟 $PROFILE（用記事本）· Open the profile in Notepad for manual inspection.</summary>
    public static void OpenProfileInEditor(string profilePath)
    {
        try
        {
            if (string.IsNullOrEmpty(profilePath)) return;
            if (!File.Exists(profilePath))
            {
                var dir = Path.GetDirectoryName(profilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(profilePath, "");
            }
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{profilePath}\"") { UseShellExecute = true });
        }
        catch { }
    }

    // ── 小工具 · helpers ───────────────────────────────────────────────────────

    private static string ExtractJson(string raw)
    {
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('{'), b = raw.LastIndexOf('}');
        if (a >= 0 && b > a) return raw.Substring(a, b - a + 1);
        return "{}";
    }

    private static string GetStr(System.Text.Json.JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString() ?? "" : "";

    private static bool GetBool(System.Text.Json.JsonElement root, string name)
        => root.TryGetProperty(name, out var v) &&
           (v.ValueKind == System.Text.Json.JsonValueKind.True ||
            (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.GetInt32() != 0));

    /// <summary>"7.4.1" ≥ 7.4 之類嘅版本比較 · version-at-least comparison tolerant of pre-release suffixes.</summary>
    private static bool TryVersionAtLeast(string version, int major, int minor)
    {
        if (string.IsNullOrEmpty(version)) return false;
        try
        {
            var core = version.Split('-', '+')[0];
            var parts = core.Split('.');
            int maj = parts.Length > 0 && int.TryParse(parts[0], out var m0) ? m0 : 0;
            int min = parts.Length > 1 && int.TryParse(parts[1], out var m1) ? m1 : 0;
            if (maj > major) return true;
            if (maj < major) return false;
            return min >= minor;
        }
        catch { return false; }
    }
}
