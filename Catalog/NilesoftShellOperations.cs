using System.Collections.Generic;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Nilesoft Shell 操作目錄 · Catalog of register / unregister / reload / restart-Explorer operations,
/// rendered with <c>Controls/TweakCard</c>. 全部用 <see cref="Tweak.Action"/> 包住
/// <see cref="NilesoftShellService"/>，需要管理員權限嘅會經 UAC。
/// All wrap <see cref="NilesoftShellService"/>; elevation goes through UAC where required.
/// </summary>
public static class NilesoftShellOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Action("nss.register",
            "Register with Explorer", "註冊到 Explorer",
            "Hook Nilesoft Shell into the Explorer context menu (shell.exe -register -treat -restart). Requires admin; Explorer windows briefly refresh.",
            "將 Nilesoft Shell 掛入 Explorer 右鍵選單（shell.exe -register -treat -restart）。需要管理員權限；Explorer 視窗會短暫重新整理。",
            "Register", "註冊",
            ct => NilesoftShellService.RegisterAsync(ct),
            requiresAdmin: true, restart: RestartScope.Explorer,
            keywords: "register install hook enable 註冊 安裝 啟用"),

        Tweak.Action("nss.unregister",
            "Unregister", "取消註冊",
            "Remove the Nilesoft Shell hook and restore the default Windows context menu (shell.exe -unregister -restart). Requires admin.",
            "移除 Nilesoft Shell 掛鈎，還原 Windows 預設右鍵選單（shell.exe -unregister -restart）。需要管理員權限。",
            "Unregister", "取消註冊",
            ct => NilesoftShellService.UnregisterAsync(ct),
            requiresAdmin: true, destructive: true, restart: RestartScope.Explorer,
            keywords: "unregister disable remove uninstall hook 取消 註冊 停用 移除"),

        Tweak.Action("nss.reload",
            "Reload configuration", "重新載入設定",
            "Re-read shell.nss and refresh the menu without unregistering (shell.exe -restart). Use after editing the config.",
            "唔取消註冊都重新讀取 shell.nss 並更新選單（shell.exe -restart）。改完設定後用。",
            "Reload", "重新載入",
            ct => NilesoftShellService.ReloadAsync(ct),
            requiresAdmin: true, restart: RestartScope.Explorer,
            keywords: "reload refresh apply restart 重新載入 重新整理 套用"),

        Tweak.Action("nss.restart-explorer",
            "Restart Explorer", "重新啟動 Explorer",
            "Kill and relaunch explorer.exe so menu changes take effect immediately. Open Explorer windows will close briefly.",
            "結束並重新啟動 explorer.exe，令選單變更即時生效。開住嘅 Explorer 視窗會短暫關閉。",
            "Restart", "重新啟動",
            ct => NilesoftShellService.RestartExplorerAsync(ct),
            requiresAdmin: false, destructive: true, restart: RestartScope.Explorer,
            keywords: "explorer restart taskbar refresh 重新啟動 工作列"),

        Tweak.Action("nss.backup",
            "Backup shell.nss", "備份 shell.nss",
            "Make a timestamped backup of the current shell.nss into the install's backups folder.",
            "將目前嘅 shell.nss 做一個有時間戳記嘅備份，放入安裝目錄嘅 backups 資料夾。",
            "Backup", "備份",
            ct =>
            {
                var path = NilesoftShellService.BackupConfig();
                return System.Threading.Tasks.Task.FromResult(path is null
                    ? TweakResult.Fail("Nothing to back up (shell.nss not found).", "冇嘢可備份（搵唔到 shell.nss）。")
                    : TweakResult.Ok($"Backed up to {path}", $"已備份到 {path}"));
            },
            keywords: "backup snapshot save copy 備份 快照"),

        Tweak.Action("nss.restore-default",
            "Restore default config", "還原預設設定",
            "Overwrite shell.nss with WinForge's clean default template (a backup is taken first). Requires admin.",
            "用 WinForge 嘅乾淨預設範本覆蓋 shell.nss（會先備份）。需要管理員權限。",
            "Restore", "還原",
            ct => System.Threading.Tasks.Task.FromResult(NilesoftShellService.RestoreDefault()),
            requiresAdmin: true, destructive: true,
            keywords: "restore default reset template 還原 預設 重設 範本"),
    };
}
