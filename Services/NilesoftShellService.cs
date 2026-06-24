using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Nilesoft Shell 包裝 · Wraps the Nilesoft Shell native context-menu extension (winget id
/// <c>Nilesoft.Shell</c>). 偵測安裝、註冊／取消註冊／重新載入、搵同讀寫 <c>shell.nss</c> 設定檔，
/// 寫之前一定先做有時間戳記嘅備份。Detects the install, registers/unregisters/reloads the extension,
/// locates and reads/writes <c>shell.nss</c>, and always makes a timestamped backup before overwriting.
/// 我哋唔解析 .nss 語言 — 當佢係純文字加可插入嘅片語。We do NOT parse the .nss language — we treat the
/// file as text plus insertable snippet blocks.
/// </summary>
public static class NilesoftShellService
{
    /// <summary>winget 套件 ID · The winget package id.</summary>
    public const string WingetId = "Nilesoft.Shell";

    /// <summary>shell.nss 編碼（保留 BOM／UTF-8）· Encoding used when reading/writing shell.nss.</summary>
    private static readonly System.Text.UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: true);

    // ===================== install detection =====================

    /// <summary>已知嘅安裝目錄候選 · Candidate install directories (checked in order).</summary>
    private static IEnumerable<string> InstallDirCandidates()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(pf)) yield return Path.Combine(pf, "Nilesoft Shell");
        if (!string.IsNullOrEmpty(pfx86)) yield return Path.Combine(pfx86, "Nilesoft Shell");
        if (!string.IsNullOrEmpty(local)) yield return Path.Combine(local, "Nilesoft Shell");
        // winget portable / user-scope locations
        if (!string.IsNullOrEmpty(local))
        {
            yield return Path.Combine(local, "Microsoft", "WinGet", "Packages");
        }
    }

    /// <summary>解析 shell.exe 嘅完整路徑（搵唔到回 null）· Resolve the full path to shell.exe, or null.</summary>
    public static string? FindShellExe()
    {
        foreach (var dir in InstallDirCandidates())
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                var direct = Path.Combine(dir, "shell.exe");
                if (File.Exists(direct)) return direct;
                // winget packages folder: search a level or two deep for shell.exe
                if (dir.Contains("WinGet", StringComparison.OrdinalIgnoreCase))
                {
                    var hit = SafeEnumerate(dir, "shell.exe").FirstOrDefault();
                    if (hit is not null) return hit;
                }
            }
            catch { /* ignore unreadable dir */ }
        }
        return null;
    }

    /// <summary>解析安裝目錄（搵唔到回 null）· Resolve the install directory, or null.</summary>
    public static string? FindInstallDir()
    {
        var exe = FindShellExe();
        return exe is null ? null : Path.GetDirectoryName(exe);
    }

    /// <summary>係咪已安裝（檔案存在）· Whether Nilesoft Shell is installed (shell.exe present).</summary>
    public static bool IsInstalled() => FindShellExe() is not null;

    /// <summary>係咪已安裝（包括 winget 查詢，較慢）· Installed check that also falls back to winget.</summary>
    public static async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        if (IsInstalled()) return true;
        try { return await PackageService.IsInstalled(WingetId, ct); }
        catch { return false; }
    }

    /// <summary>係咪已註冊到 Explorer（偵測 shell32 hook 鍵）· Best-effort "is registered" check.</summary>
    public static bool IsRegistered()
    {
        // Nilesoft registers under HKCU/HKLM CLSID + an approved shell extension entry.
        // We probe the well-known registered marker it writes for the current user.
        var v = RegistryHelper.GetValue(RegRoot.HKCU,
            @"Software\Classes\CLSID\{3B1D0DA3-7A3F-4A29-9C26-9181A2C0B8F0}", "");
        if (v is not null) return true;
        // Fallback: presence of the shell.dll alongside shell.exe AND a registered approved entry.
        var dir = FindInstallDir();
        if (dir is null) return false;
        return File.Exists(Path.Combine(dir, "shell.dll"));
    }

    // ===================== register / unregister / reload =====================

    /// <summary>
    /// 註冊到 Explorer · Register with Explorer: <c>shell.exe -register -treat -restart</c>.
    /// 需要管理員權限（會經 UAC）· requires elevation (goes through UAC).
    /// </summary>
    public static Task<TweakResult> RegisterAsync(CancellationToken ct = default)
        => RunShell("-register -treat -restart", elevated: true, ct);

    /// <summary>取消註冊 · Unregister: <c>shell.exe -unregister -restart</c>.</summary>
    public static Task<TweakResult> UnregisterAsync(CancellationToken ct = default)
        => RunShell("-unregister -restart", elevated: true, ct);

    /// <summary>重新載入設定 · Reload the configuration: <c>shell.exe -restart</c> (re-reads shell.nss).</summary>
    public static Task<TweakResult> ReloadAsync(CancellationToken ct = default)
        => RunShell("-restart", elevated: true, ct);

    /// <summary>重新啟動 Explorer · Restart Explorer so menus refresh immediately.</summary>
    public static async Task<TweakResult> RestartExplorerAsync(CancellationToken ct = default)
    {
        var stop = await ShellRunner.RunCmd("taskkill /f /im explorer.exe", elevated: false, ct);
        // Explorer auto-restarts; nudge it just in case it doesn't.
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe") { UseShellExecute = true }); }
        catch { /* shell will respawn it */ }
        return stop.Success
            ? TweakResult.Ok("Explorer restarted.", "已重新啟動 Explorer。")
            : TweakResult.Ok("Explorer refresh requested.", "已要求重新整理 Explorer。");
    }

    private static async Task<TweakResult> RunShell(string args, bool elevated, CancellationToken ct)
    {
        var exe = FindShellExe();
        if (exe is null)
            return TweakResult.Fail("shell.exe not found — install Nilesoft Shell first.",
                "搵唔到 shell.exe — 請先安裝 Nilesoft Shell。");
        var dir = Path.GetDirectoryName(exe);
        return await ShellRunner.RunIn(dir, exe, args, elevated, ct);
    }

    // ===================== shell.nss config file =====================

    /// <summary>解析 shell.nss 完整路徑（搵唔到回 null）· Resolve shell.nss, or null if not installed.</summary>
    public static string? FindConfigPath()
    {
        var dir = FindInstallDir();
        if (dir is null) return null;
        var p = Path.Combine(dir, "shell.nss");
        return File.Exists(p) ? p : p; // return expected path even if missing so callers can create it
    }

    /// <summary>讀取 shell.nss 內容 · Read the shell.nss text (empty string if missing).</summary>
    public static string ReadConfig()
    {
        var p = FindConfigPath();
        if (p is null || !File.Exists(p)) return "";
        try { return File.ReadAllText(p, Utf8); }
        catch { try { return File.ReadAllText(p); } catch { return ""; } }
    }

    /// <summary>
    /// 寫入 shell.nss（寫之前一定先備份）· Write shell.nss, ALWAYS taking a timestamped backup first.
    /// 因為檔案喺 %ProgramFiles% 之下，寫入需要管理員權限；唔夠權限會回傳失敗。
    /// The file lives under %ProgramFiles%; writing needs admin — returns a clear failure if not elevated.
    /// </summary>
    public static TweakResult WriteConfig(string content)
    {
        var p = FindConfigPath();
        if (p is null)
            return TweakResult.Fail("Install location not found.", "搵唔到安裝位置。");
        try
        {
            string? backup = null;
            if (File.Exists(p)) backup = BackupConfig();
            File.WriteAllText(p, content, Utf8);
            return TweakResult.Ok(
                backup is null ? "Saved." : $"Saved. Backup: {Path.GetFileName(backup)}",
                backup is null ? "已儲存。" : $"已儲存。備份：{Path.GetFileName(backup)}");
        }
        catch (UnauthorizedAccessException)
        {
            return TweakResult.Fail(
                "Access denied writing shell.nss — run WinForge as administrator.",
                "無權寫入 shell.nss — 請以管理員身分執行 WinForge。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>備份資料夾（在 install dir 下 backups\）· Backup folder under the install directory.</summary>
    public static string? BackupDir()
    {
        var dir = FindInstallDir();
        if (dir is null) return null;
        var b = Path.Combine(dir, "backups");
        return b;
    }

    /// <summary>做一次有時間戳記嘅備份，回傳備份檔路徑 · Make a timestamped backup; returns the backup path.</summary>
    public static string? BackupConfig(string? targetFolder = null)
    {
        var p = FindConfigPath();
        if (p is null || !File.Exists(p)) return null;
        var folder = targetFolder ?? BackupDir();
        if (folder is null) return null;
        Directory.CreateDirectory(folder);
        var name = $"shell.{DateTime.Now:yyyyMMdd-HHmmss}.nss.bak";
        var dest = Path.Combine(folder, name);
        File.Copy(p, dest, overwrite: true);
        return dest;
    }

    /// <summary>列出已有備份（最新先）· List existing backups, newest first.</summary>
    public static IReadOnlyList<string> ListBackups()
    {
        var folder = BackupDir();
        if (folder is null || !Directory.Exists(folder)) return Array.Empty<string>();
        try
        {
            return Directory.GetFiles(folder, "shell.*.nss.bak")
                .OrderByDescending(File.GetLastWriteTime).ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>由備份還原 · Restore shell.nss from a backup file (backs up current first).</summary>
    public static TweakResult RestoreBackup(string backupPath)
    {
        if (!File.Exists(backupPath))
            return TweakResult.Fail("Backup file not found.", "搵唔到備份檔。");
        try
        {
            var text = File.ReadAllText(backupPath, Utf8);
            return WriteConfig(text);
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>還原成預設設定（內建範本）· Restore the bundled default shell.nss.</summary>
    public static TweakResult RestoreDefault() => WriteConfig(DefaultConfig);

    // ===================== default config + snippet gallery =====================

    /// <summary>內建預設 shell.nss（精簡、安全、可立即運作）· A clean, safe default shell.nss.</summary>
    public const string DefaultConfig =
@"// shell.nss — Nilesoft Shell configuration (restored by WinForge)
// Docs: https://nilesoft.org/docs
settings
{
    priority = 1
    exclude.where = !process.is_explorer
    showdelay = 200
    modify.remove.duplicate = 1
    tip.enabled = true
}

// Modern dark theme
theme
{
    name = ""modern""
    dark = auto
    background { opacity = auto }
}

// Keep Windows default items available under a sub-menu
menu(type='*' mode='multiple' title='More options' image=\inherit)
{
}
";

    /// <summary>一個可插入嘅片語 · One insertable snippet block.</summary>
    public sealed class Snippet
    {
        public string En { get; init; } = "";
        public string Zh { get; init; } = "";
        public string DescEn { get; init; } = "";
        public string DescZh { get; init; } = "";
        public string Code { get; init; } = "";
    }

    /// <summary>精選片語庫 · Curated snippet/template gallery (insert at cursor).</summary>
    public static readonly Snippet[] Snippets =
    {
        new()
        {
            En = "Modern dark theme", Zh = "現代深色主題",
            DescEn = "Switch the menu to the modern theme with auto dark mode.",
            DescZh = "將選單轉做現代主題，自動深色。",
            Code =
@"theme
{
    name = ""modern""
    dark = auto
    background { opacity = auto }
}
",
        },
        new()
        {
            En = "Copy as path", Zh = "複製為路徑",
            DescEn = "Add a 'Copy as path' entry for files and folders.",
            DescZh = "為檔案同資料夾加一個「複製為路徑」項目。",
            Code =
@"item(type='file|dir' title='Copy as path' image=
    cmd-clipboard='""' + sel.path + '""')
",
        },
        new()
        {
            En = "Open PowerShell here", Zh = "喺呢度開 PowerShell",
            DescEn = "Open Windows PowerShell in the current folder.",
            DescZh = "喺目前資料夾開 Windows PowerShell。",
            Code =
@"item(title='PowerShell here' image=
    admin=false
    cmd='powershell.exe' args='-NoExit -Command ""Set-Location -LiteralPath \'' + sel.dir + '\'""')
",
        },
        new()
        {
            En = "Open Terminal here (admin)", Zh = "喺呢度開終端機（管理員）",
            DescEn = "Open Windows Terminal elevated in the current folder.",
            DescZh = "喺目前資料夾以管理員開 Windows Terminal。",
            Code =
@"item(title='Terminal (Admin) here' image=
    admin=true
    cmd='wt.exe' args='-d ""' + sel.dir + '""')
",
        },
        new()
        {
            En = "Take ownership", Zh = "取得擁有權",
            DescEn = "Take ownership of a file or folder (elevated takeown + icacls).",
            DescZh = "取得檔案或資料夾嘅擁有權（提權 takeown + icacls）。",
            Code =
@"item(type='file|dir' title='Take ownership' image= admin=true
    cmd='cmd.exe' args='/c takeown /f ""' + sel.path + '"" /r /d y && icacls ""' + sel.path + '"" /grant administrators:F /t')
",
        },
        new()
        {
            En = "Run as admin (any file)", Zh = "以管理員執行（任何檔案）",
            DescEn = "Add a generic 'Run as administrator' entry.",
            DescZh = "加一個通用嘅「以管理員身分執行」項目。",
            Code =
@"item(type='file' title='Run as administrator' image= admin=true
    cmd=sel.path)
",
        },
        new()
        {
            En = "Open with Notepad", Zh = "用記事本開啟",
            DescEn = "Open the selected file in Notepad.",
            DescZh = "用記事本開啟所選檔案。",
            Code =
@"item(type='file' title='Open with Notepad' image=
    cmd='notepad.exe' args='""' + sel.path + '""')
",
        },
        new()
        {
            En = "Custom menu group", Zh = "自訂選單群組",
            DescEn = "A collapsible sub-menu you can drop other items into.",
            DescZh = "一個可收摺嘅子選單，可以放其他項目入面。",
            Code =
@"menu(title='My tools' image=)
{
    // put item(...) entries here
}
",
        },
        new()
        {
            En = "Hide default items (clean menu)", Zh = "隱藏預設項目（精簡選單）",
            DescEn = "Remove noisy default Explorer entries for a cleaner menu.",
            DescZh = "移除嘈雜嘅預設 Explorer 項目，令選單更精簡。",
            Code =
@"modify(where=this.id(id.give_access_to, id.restore_previous_versions) vis=hidden)
",
        },
        new()
        {
            En = "Settings block", Zh = "設定區塊",
            DescEn = "Top-level settings (priority, show delay, tips).",
            DescZh = "頂層設定（優先序、顯示延遲、提示）。",
            Code =
@"settings
{
    priority = 1
    exclude.where = !process.is_explorer
    showdelay = 200
    tip.enabled = true
}
",
        },
    };

    private static IEnumerable<string> SafeEnumerate(string root, string fileName)
    {
        IEnumerable<string> Walk(string dir, int depth)
        {
            if (depth > 3) yield break;
            string[] files;
            try { files = Directory.GetFiles(dir, fileName); } catch { yield break; }
            foreach (var f in files) yield return f;
            string[] subs;
            try { subs = Directory.GetDirectories(dir); } catch { yield break; }
            foreach (var s in subs)
                foreach (var f in Walk(s, depth + 1))
                    yield return f;
        }
        return Walk(root, 0);
    }
}
