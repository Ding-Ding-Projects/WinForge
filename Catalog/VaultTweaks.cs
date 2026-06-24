using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

public static class VaultTweaks
{
    /// <summary>
    /// 執行時用嘅 ASR 規則 GUID → 人類可讀名稱對照（喺 PowerShell 解析，唔硬編喺 UI）。
    /// Runtime GUID → human-readable name map for Attack Surface Reduction rules,
    /// resolved inside PowerShell so the catalog never hardcodes names in the UI.
    /// Source: Microsoft "Attack surface reduction rules reference".
    /// </summary>
    private const string AsrNameMapPs = @"
$AsrNames = @{
  '56a863a9-875e-4185-98a7-b882c64b5ce5' = 'Block abuse of exploited vulnerable signed drivers';
  '7674ba52-37eb-4a4f-a9a1-f0f9a1619a2c' = 'Block Adobe Reader from creating child processes';
  'd4f940ab-401b-4efc-aadc-ad5f3c50688a' = 'Block all Office applications from creating child processes';
  '9e6c4e1f-7d60-472f-ba1a-a39ef669e4b2' = 'Block credential stealing from LSASS';
  'be9ba2d9-53ea-4cdc-84e5-9b1eeee46550' = 'Block executable content from email client and webmail';
  '01443614-cd74-433a-b99e-2ecdc07bfc25' = 'Block executable files unless they meet prevalence/age/trusted criteria';
  '5beb7efe-fd9a-4556-801d-275e5ffc04cc' = 'Block execution of potentially obfuscated scripts';
  'd3e037e1-3eb8-44c8-a917-57927947596d' = 'Block JavaScript or VBScript from launching downloaded executable content';
  '3b576869-a4ec-4529-8536-b80a7769e899' = 'Block Office applications from creating executable content';
  '75668c1f-73b5-4cf0-bb93-3ecf5cb7cc84' = 'Block Office applications from injecting code into other processes';
  '26190899-1602-49e8-8b27-eb1d0a1ce869' = 'Block Office communication application from creating child processes';
  'e6db77e5-3df2-4cf1-b95a-636979351e5b' = 'Block persistence through WMI event subscription';
  'd1e49aac-8f56-4280-b9ba-993a6d77406c' = 'Block process creations originating from PSExec and WMI commands';
  '33ddedf1-c6e0-47cb-833e-de6133960387' = 'Block rebooting machine in Safe Mode';
  'b2b3f03d-6a65-4f7b-a9c7-1c7ef74a9ba4' = 'Block untrusted and unsigned processes that run from USB';
  'c0033c00-d16d-4114-a5a0-dc9b3a7d2ceb' = 'Block use of copied or impersonated system tools';
  'a8f5898e-1dc8-49a9-9878-85004b8a61e6' = 'Block Webshell creation for Servers';
  '92e97fa1-2edf-4476-bdd6-9dd0b4dddc7b' = 'Block Win32 API calls from Office macros';
  'c1db55ab-c21a-4637-bb3f-a12568109d35' = 'Use advanced protection against ransomware'
};
";

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // --- bitlocker (20) ---
        Tweak.Cmd("vault.bitlocker.status-all", "BitLocker status (all)", "BitLocker 狀態（全部）",
                "Show BitLocker status for every drive on the machine.", "顯示部機所有磁碟嘅 BitLocker 狀態。",
                "Check", "查詢", "manage-bde -status",
                requiresAdmin: true, keywords: "bitlocker,manage-bde,status,狀態,加密"),
        
            Tweak.Cmd("vault.bitlocker.status-c", "BitLocker status (C:)", "BitLocker 狀態（C:）",
                "Show detailed BitLocker status for the system drive C:.", "顯示系統磁碟 C: 嘅詳細 BitLocker 狀態。",
                "Check C:", "查 C:", "manage-bde -status C:",
                requiresAdmin: true, keywords: "bitlocker,manage-bde,status,c drive,系統碟"),
        
            Tweak.Cmd("vault.bitlocker.lock-drive", "Lock a drive", "鎖定磁碟",
                "Lock drive D: so its data is inaccessible until unlocked, force-dismounting it if in use. Change the letter as needed.", "鎖定 D: 磁碟，鎖咗之後要解鎖先用到，如果用緊會強制卸載。需要時自行改字母。",
                "Lock D:", "鎖 D:", "manage-bde -lock D: -ForceDismount",
                requiresAdmin: true, destructive: true, keywords: "bitlocker,lock,鎖定,manage-bde"),
        
            Tweak.Cmd("vault.bitlocker.unlock-password", "Unlock with password", "用密碼解鎖",
                "Unlock drive D: using its BitLocker password (you will be prompted to type it). Change the letter as needed.", "用 BitLocker 密碼解鎖 D: 磁碟（會叫你打密碼）。需要時自行改字母。",
                "Unlock D:", "解鎖 D:", "manage-bde -unlock D: -Password",
                requiresAdmin: true, keywords: "bitlocker,unlock,解鎖,password,密碼"),
        
            Tweak.Cmd("vault.bitlocker.backup-key-file", "Backup recovery key to file", "備份還原金鑰到檔案",
                "Read the key protectors for C: (including the 48-digit recovery password) and save the output to a text file in your profile.", "讀取 C: 嘅金鑰保護器（包括 48 位還原密碼）並將結果存去你個人資料夾嘅文字檔。",
                "Backup", "備份", "manage-bde -protectors -get C: > \"%USERPROFILE%\\BitLocker-Recovery-C.txt\" & echo Saved to %USERPROFILE%\\BitLocker-Recovery-C.txt",
                requiresAdmin: true, keywords: "bitlocker,recovery,key,還原,金鑰,備份,protectors"),
        
            Tweak.Cmd("vault.bitlocker.enable-drive", "Enable BitLocker on a drive", "為磁碟啟用 BitLocker",
                "Turn on BitLocker encryption for drive D: with a recovery password protector, encrypting used space only. Change the letter as needed.", "為 D: 磁碟開啟 BitLocker 加密，用還原密碼保護器，只加密已用空間。需要時自行改字母。",
                "Enable D:", "啟用 D:", "manage-bde -on D: -RecoveryPassword -UsedSpaceOnly",
                requiresAdmin: true, keywords: "bitlocker,enable,on,加密,啟用,encrypt"),
        
            Tweak.Cmd("vault.bitlocker.turn-off", "Turn off BitLocker (decrypt)", "關閉 BitLocker（解密）",
                "Fully decrypt drive D: and turn BitLocker off. This can take a long time and removes encryption protection. Change the letter as needed.", "完全解密 D: 磁碟並關閉 BitLocker。可能要好耐，亦會移除加密保護。需要時自行改字母。",
                "Turn off D:", "關 D:", "manage-bde -off D:",
                requiresAdmin: true, destructive: true, keywords: "bitlocker,off,decrypt,解密,關閉"),
        
            Tweak.Cmd("vault.bitlocker.pause-protection", "Pause encryption/decryption", "暫停加密／解密",
                "Pause an in-progress encryption or decryption on drive D:. Change the letter as needed.", "暫停 D: 磁碟正進行緊嘅加密或解密。需要時自行改字母。",
                "Pause D:", "暫停 D:", "manage-bde -pause D:",
                requiresAdmin: true, keywords: "bitlocker,pause,暫停,manage-bde"),
        
            Tweak.Cmd("vault.bitlocker.resume-conversion", "Resume encryption/decryption", "繼續加密／解密",
                "Resume a paused encryption or decryption on drive D:. Change the letter as needed.", "繼續 D: 磁碟暫停咗嘅加密或解密。需要時自行改字母。",
                "Resume D:", "繼續 D:", "manage-bde -resume D:",
                requiresAdmin: true, keywords: "bitlocker,resume,繼續,manage-bde"),
        
            Tweak.Cmd("vault.bitlocker.add-password-protector", "Add password protector", "加入密碼保護器",
                "Add a password key protector to drive D: (you will be prompted to set the password). Change the letter as needed.", "為 D: 磁碟加入密碼金鑰保護器（會叫你設定密碼）。需要時自行改字母。",
                "Add D:", "加入 D:", "manage-bde -protectors -add D: -Password",
                requiresAdmin: true, keywords: "bitlocker,protector,password,密碼,保護器,add"),
        
            Tweak.Cmd("vault.bitlocker.show-protectors", "Show key protectors", "顯示金鑰保護器",
                "List all key protectors configured on the system drive C:, including TPM and recovery password.", "列出系統磁碟 C: 上所有金鑰保護器，包括 TPM 同還原密碼。",
                "Show", "顯示", "manage-bde -protectors -get C:",
                requiresAdmin: true, keywords: "bitlocker,protectors,key,金鑰,保護器"),
        
            Tweak.Cmd("vault.bitlocker.suspend-1-reboot", "Suspend for 1 reboot", "暫停 1 次重開機",
                "Suspend BitLocker protection on C: for exactly one reboot, then it auto-resumes. Useful before firmware/TPM changes.", "暫停 C: 嘅 BitLocker 保護，淨係跳過一次重開機，之後自動恢復。改韌體或 TPM 之前好用。",
                "Suspend", "暫停", "manage-bde -protectors -disable C: -RebootCount 1",
                requiresAdmin: true, keywords: "bitlocker,suspend,暫停,reboot,disable,重開機"),
        
            Tweak.Cmd("vault.bitlocker.tpm-status", "TPM status (manage-bde)", "TPM 狀態（manage-bde）",
                "Show the Trusted Platform Module status as seen by BitLocker.", "顯示 BitLocker 睇到嘅信賴平台模組（TPM）狀態。",
                "Check TPM", "查 TPM", "manage-bde -tpm -turnon C:",
                requiresAdmin: true, keywords: "bitlocker,tpm,信賴平台模組,manage-bde"),
        
            Tweak.Powershell("vault.bitlocker.get-tpm", "Get-Tpm details", "Get-Tpm 詳情",
                "Query detailed TPM presence, readiness and ownership state via the Get-Tpm cmdlet.", "用 Get-Tpm cmdlet 查詢 TPM 嘅存在、就緒同擁有權狀態詳情。",
                "Get-Tpm", "查 TPM", "Get-Tpm | Format-List",
                requiresAdmin: true, keywords: "tpm,get-tpm,powershell,信賴平台模組"),
        
            Tweak.Powershell("vault.bitlocker.get-volume", "Get-BitLockerVolume", "Get-BitLockerVolume",
                "Use PowerShell to list BitLocker volumes with their mount point, protection status and encryption percentage.", "用 PowerShell 列出 BitLocker 磁碟區，連掛載點、保護狀態同加密百分比。",
                "Run", "執行", "Get-BitLockerVolume | Format-Table MountPoint, VolumeStatus, ProtectionStatus, EncryptionPercentage -AutoSize",
                requiresAdmin: true, keywords: "bitlocker,get-bitlockervolume,powershell,磁碟區"),
        
            Tweak.Powershell("vault.bitlocker.list-encrypted", "List encrypted volumes", "列出已加密磁碟區",
                "List only the volumes that are fully or partially encrypted by BitLocker.", "淨係列出畀 BitLocker 完全或部分加密咗嘅磁碟區。",
                "List", "列出", "Get-BitLockerVolume | Where-Object { $_.VolumeStatus -ne 'FullyDecrypted' } | Format-Table MountPoint, VolumeStatus, EncryptionPercentage -AutoSize",
                requiresAdmin: true, keywords: "bitlocker,encrypted,加密,list,volumes,磁碟區"),
        
            Tweak.Shell("vault.bitlocker.control-panel", "Open BitLocker control panel", "開 BitLocker 控制台",
                "Open the classic BitLocker Drive Encryption control panel page to manage drives in the GUI.", "開傳統 BitLocker 磁碟加密控制台頁面，喺圖形介面管理磁碟。",
                "Open", "開啟", "control.exe", "/name Microsoft.BitLockerDriveEncryption",
                keywords: "bitlocker,control panel,控制台,gui"),
        
            Tweak.Cmd("vault.bitlocker.enable-autounlock", "Enable auto-unlock", "啟用自動解鎖",
                "Enable automatic unlocking of fixed data drive D: when the system drive is unlocked (system drive must already be BitLocker-protected). Change the letter as needed.", "啟用固定資料磁碟 D: 嘅自動解鎖，系統碟解鎖時佢就跟住解（系統碟要已經有 BitLocker 保護）。需要時自行改字母。",
                "Enable D:", "啟用 D:", "manage-bde -autounlock -enable D:",
                requiresAdmin: true, keywords: "bitlocker,autounlock,自動解鎖,enable"),
        
            Tweak.Powershell("vault.bitlocker.backup-key-ad", "Backup recovery key to AD", "備份還原金鑰到 AD",
                "Back up the recovery password key protector for C: to Active Directory Domain Services (requires a domain-joined PC with the right policy).", "將 C: 嘅還原密碼金鑰保護器備份去 Active Directory 網域服務（要加入網域兼有對應原則）。",
                "Backup", "備份", "$kp = (Get-BitLockerVolume -MountPoint 'C:').KeyProtector | Where-Object { $_.KeyProtectorType -eq 'RecoveryPassword' }; if ($kp) { Backup-BitLockerKeyProtector -MountPoint 'C:' -KeyProtectorId $kp[0].KeyProtectorId } else { Write-Host 'No recovery password protector found on C:' }",
                requiresAdmin: true, keywords: "bitlocker,active directory,ad,recovery,還原,備份"),
        
            Tweak.Cmd("vault.bitlocker.resume-protection", "Resume protection", "恢復保護",
                "Re-enable (resume) BitLocker protection on C: after it was suspended, restoring all key protectors.", "喺 C: 暫停過 BitLocker 保護之後重新啟用（恢復），還原晒所有金鑰保護器。",
                "Resume", "恢復", "manage-bde -protectors -enable C:",
                requiresAdmin: true, keywords: "bitlocker,resume,protection,恢復,保護,enable"),

            Tweak.Cmd("vault.bitlocker.force-recovery", "Force recovery on next boot", "下次開機逼出還原畫面",
                "Force C: into BitLocker recovery so the very next boot demands the 48-digit recovery key (useful before firmware/TPM changes). DANGEROUS: you cannot boot without the recovery key. Change the letter as needed.", "強制 C: 進入 BitLocker 還原狀態，下次開機就要打 48 位還原密碼先開到機（改韌體或 TPM 之前好用）。危險：冇還原密碼就開唔到機。需要時自行改字母。",
                "Force recovery", "逼出還原", "manage-bde -forcerecovery C:",
                requiresAdmin: true, destructive: true, keywords: "bitlocker,forcerecovery,recovery,還原,逼出,manage-bde,危險"),

            Tweak.Cmd("vault.bitlocker.repair-bde", "Repair a damaged drive (repair-bde)", "修復爛咗嘅磁碟 (repair-bde)",
                "Reconstruct and decrypt salvageable data from a corrupted BitLocker volume to a SEPARATE empty target, using the 48-digit recovery password. The target is overwritten, so it must be a different, empty drive/image. Edit the input volume, output target and recovery password first.", "用 48 位還原密碼，將損壞嘅 BitLocker 磁碟區可救嘅資料重建並解密到另一個空白目標。目標會被覆寫，所以一定要係另一隻空白磁碟／映像。先改輸入磁碟區、輸出目標同還原密碼。",
                "Repair", "修復", "repair-bde D: E:\\Recovered.img -rp 000000-000000-000000-000000-000000-000000-000000-000000",
                requiresAdmin: true, destructive: true, keywords: "bitlocker,repair-bde,repair,修復,recovery,還原,damaged,損壞"),

            Tweak.Cmd("vault.bitlocker.change-password", "Change volume password", "更改磁碟區密碼",
                "Change the BitLocker password protector on drive D: (you will be prompted for the current and new password). Change the letter as needed.", "更改 D: 磁碟嘅 BitLocker 密碼保護器（會叫你打現有同新密碼）。需要時自行改字母。",
                "Change", "更改", "manage-bde -changepassword D:",
                requiresAdmin: true, keywords: "bitlocker,changepassword,password,密碼,更改,manage-bde"),

        // --- WinForge Vault (de-branded encrypted-volume engine; quick-actions that drive the bundled binary) ---
        VaultShell("vault.veracrypt.gui", "Launch WinForge Vault", "啟動 WinForge 保險庫",
            "Open the WinForge Vault main window (mount, settings and benchmark live here).", "打開 WinForge 保險庫主視窗（掛載、設定同效能測試都喺度）。",
            "Open", "打開", mount: true, args: "",
            keywords: "vault,launch,啟動,加密,保險庫"),

        VaultShell("vault.veracrypt.dismount-all", "Dismount all volumes", "卸載所有磁碟區",
            "Dismount every mounted vault volume quietly.", "靜默卸載所有已掛載嘅保險庫磁碟區。",
            "Dismount all", "全部卸載", mount: true, args: "/q /d", elevated: true,
            keywords: "vault,dismount,卸載,unmount,保險庫"),

        VaultShell("vault.veracrypt.mount-favorites", "Mount favorites", "掛載最愛磁碟區",
            "Auto-mount all favourite volumes quietly.", "靜默自動掛載所有最愛磁碟區。",
            "Mount", "掛載", mount: true, args: "/q /a favorites", elevated: true,
            keywords: "vault,favorites,最愛,mount,保險庫"),

        VaultShell("vault.veracrypt.auto-mount-devices", "Auto-mount devices", "自動掛載裝置",
            "Scan and auto-mount all encrypted device partitions.", "掃描並自動掛載所有加密裝置分割區。",
            "Auto-mount", "自動掛載", mount: true, args: "/q /a devices", elevated: true,
            keywords: "vault,devices,裝置,mount,保險庫"),

        VaultShell("vault.veracrypt.mount-dialog", "Open mount dialog", "打開掛載對話框",
            "Open WinForge Vault where you can pick a file and mount a volume.", "打開 WinForge 保險庫，可以揀檔案再掛載磁碟區。",
            "Mount", "掛載", mount: true, args: "",
            keywords: "vault,mount,掛載,dialog,保險庫"),

        VaultShell("vault.veracrypt.create-volume", "Create new volume", "建立新磁碟區",
            "Launch the Volume Creation Wizard.", "啟動磁碟區建立精靈。",
            "Create", "建立", mount: false, args: "",
            keywords: "vault,create,建立,volume,保險庫"),

        VaultShell("vault.veracrypt.volume-wizard", "Volume Creation Wizard", "磁碟區建立精靈",
            "Open the volume-creation wizard executable directly.", "直接打開磁碟區建立精靈程式。",
            "Open wizard", "打開精靈", mount: false, args: "",
            keywords: "vault,wizard,精靈,format,保險庫"),

        VaultShell("vault.veracrypt.keyfile-generator", "Keyfile generator", "金鑰檔產生器",
            "Open WinForge Vault where the Keyfile Generator is under the Tools menu.", "打開 WinForge 保險庫，金鑰檔產生器喺工具選單度。",
            "Open", "打開", mount: true, args: "",
            keywords: "vault,keyfile,金鑰檔,generator,保險庫"),

        VaultShell("vault.veracrypt.wipe-cache", "Wipe password cache", "清除密碼快取",
            "Clear cached passwords held in memory quietly.", "靜默清除記憶體中快取嘅密碼。",
            "Wipe cache", "清除快取", mount: true, args: "/q /w", elevated: true,
            keywords: "vault,cache,快取,wipe,password,保險庫"),

        VaultShell("vault.veracrypt.force-dismount", "Force dismount all", "強制卸載全部",
            "Force-dismount every volume even if files are open.", "即使有檔案開住都強制卸載所有磁碟區。",
            "Force dismount", "強制卸載", mount: true, args: "/q /d /f", elevated: true,
            destructive: true, keywords: "vault,force,強制,dismount,unmount,保險庫"),

        VaultShell("vault.veracrypt.background-task", "Start background task", "啟動背景工作",
            "Run WinForge Vault silently so the system-tray background task stays active.", "靜默執行 WinForge 保險庫，等系統匣背景工作保持啟用。",
            "Start", "啟動", mount: true, args: "/q /silent",
            keywords: "vault,background,背景,tray,保險庫"),

        Tweak.Action("vault.veracrypt.version", "Show engine path", "顯示引擎路徑",
            "Show the resolved WinForge Vault engine executable path and whether the bundled de-branded build is in use.", "顯示已解析嘅 WinForge 保險庫引擎執行檔路徑，同係咪用緊隨附嘅去品牌版本。",
            "Show", "顯示", ct => System.Threading.Tasks.Task.FromResult(
                Models.TweakResult.Ok("Engine path", "引擎路徑",
                    (VaultVolumeService.FindMountBinary() ?? "(not found / 搵唔到)")
                    + (VaultVolumeService.IsBundledPresent() ? "  [bundled]" : "  [fallback]"))),
            keywords: "vault,version,版本,path,路徑,保險庫"),

        Tweak.Cmd("vault.veracrypt.list-mounted", "List mounted drives", "列出已掛載磁碟",
            "List current drive letters to see mounted volumes.", "列出目前磁碟機代號睇下掛載咗咩磁碟區。",
            "List", "列出", "wmic logicaldisk get DeviceID,VolumeName,Description,Size",
            keywords: "vault,list,列出,mounted,drives,保險庫"),

        VaultShell("vault.veracrypt.dismount-letter", "Dismount drive letter", "卸載指定磁碟機",
            "Dismount the volume on drive X (edit the letter as needed).", "卸載 X 磁碟機上嘅磁碟區（按需要改代號）。",
            "Dismount X", "卸載 X", mount: true, args: "/q /d X", elevated: true,
            keywords: "vault,dismount,磁碟機,letter,X,保險庫"),

        VaultShell("vault.veracrypt.mount-readonly", "Mount read-only", "唯讀掛載",
            "Open WinForge Vault and use Mount Options to mount as read-only.", "打開 WinForge 保險庫，喺掛載選項度以唯讀方式掛載。",
            "Mount RO", "唯讀掛載", mount: true, args: "",
            keywords: "vault,readonly,唯讀,mount,保險庫"),

        VaultShell("vault.veracrypt.set-pim", "Mount with PIM", "以 PIM 掛載",
            "Open WinForge Vault where the PIM field is available in the password prompt.", "打開 WinForge 保險庫，密碼提示度可以輸入 PIM。",
            "Mount PIM", "PIM 掛載", mount: true, args: "",
            keywords: "vault,pim,掛載,iterations,保險庫"),

        VaultShell("vault.veracrypt.benchmark", "Open settings / benchmark", "打開設定／效能測試",
            "Open WinForge Vault where you can run the algorithm benchmark from Settings.", "打開 WinForge 保險庫，喺設定度可以做演算法效能測試。",
            "Open", "打開", mount: true, args: "",
            keywords: "vault,benchmark,效能,settings,設定,保險庫"),

        VaultShell("vault.veracrypt.traveler-disk", "Traveler disk setup", "隨身碟設定",
            "Open WinForge Vault to set up a portable Traveler Disk from the Tools menu.", "打開 WinForge 保險庫，喺工具選單度設定可攜式隨身碟。",
            "Open", "打開", mount: true, args: "",
            keywords: "vault,traveler,隨身碟,portable,保險庫"),

        Tweak.Shell("vault.veracrypt.explore-volume", "Explore mounted volume", "瀏覽已掛載磁碟區",
            "Open File Explorer to browse a mounted volume (edit drive letter).", "打開檔案總管瀏覽已掛載磁碟區（改磁碟機代號）。",
            "Explore", "瀏覽", "explorer.exe", "X:\\",
            keywords: "vault,explore,瀏覽,explorer,volume,保險庫"),

        VaultShell("vault.veracrypt.mount-keyfile-pim", "Mount with keyfile + PIM", "用鎖匙檔加 PIM 掛載",
            "Mount a vault container to drive X using a keyfile and a PIM, quietly and silently. Edit the volume path, drive letter, keyfile path and PIM number first.", "用鎖匙檔同 PIM 靜默掛載一個保險庫容器到 X 磁碟機。先改容器路徑、磁碟機代號、鎖匙檔路徑同 PIM 數值。",
            "Mount", "掛載", mount: true, args: "/v \"C:\\path\\to\\volume.wfv\" /l X /k \"C:\\path\\to\\keyfile\" /pim 0 /q /silent", elevated: true,
            keywords: "vault,keyfile,pim,鎖匙檔,掛載,mount,silent,保險庫"),

        VaultShell("vault.veracrypt.backup-header-gui", "Backup volume header", "備份磁碟區檔頭",
            "Open WinForge Vault so you can use Tools ▸ Backup Volume Header — the official, GUI-only way to save a volume header backup (both primary and embedded backup headers).", "打開 WinForge 保險庫，用工具 ▸ 備份磁碟區檔頭 — 官方、淨係喺圖形介面先做到嘅檔頭備份方法（連主檔頭同內嵌備份檔頭）。",
            "Open", "打開", mount: true, args: "",
            keywords: "vault,backup,header,檔頭,備份,tools,工具,保險庫"),

        Tweak.Powershell("vault.veracrypt.backup-header-raw", "Snapshot raw header (first 131072 bytes)", "快照原始檔頭（首 131072 位元組）",
            "CLI fallback: copy the first 131072 bytes of a vault container to a .hdrbak file next to it. This captures the embedded primary header as a RAW copy — it is NOT the official backup-header format. Edit the volume path first.", "命令列備援：將保險庫容器嘅首 131072 位元組複製去旁邊嘅 .hdrbak 檔。呢個係抓內嵌主檔頭嘅原始複本 — 唔係官方嘅備份檔頭格式。先改容器路徑。",
            "Snapshot", "快照", "$src='C:\\path\\to\\volume.wfv'; if (-not (Test-Path $src)) { Write-Host 'Volume not found. Edit the path in this op.'; return }; $dst=$src + '.hdrbak'; $fs=[IO.File]::OpenRead($src); try { $buf=New-Object byte[] 131072; $read=$fs.Read($buf,0,131072); [IO.File]::WriteAllBytes($dst,$buf[0..($read-1)]); Write-Host (\"Wrote raw header snapshot ($read bytes) to: \" + $dst); Write-Host 'NOTE: raw copy only, not the official backup-header format.' } finally { $fs.Dispose() }",
            keywords: "vault,header,raw,131072,檔頭,快照,backup,備份,保險庫"),

        // --- efs (20) ---
        Tweak.Cmd("vault.efs.show-file-encryption", "Show file encryption state", "顯示檔案加密狀態",
            "Run cipher in your Documents folder to display the encryption state of every file and subfolder there.", "喺你嘅文件資料夾度行 cipher，顯示入面每個檔案同子資料夾嘅加密狀態。",
            "Show", "顯示", "cipher \"%USERPROFILE%\\Documents\"",
            keywords: "cipher,efs,encryption,state,加密,狀態"),
        
        Tweak.Cmd("vault.efs.encrypt-folder", "Encrypt a folder (EFS)", "加密資料夾 (EFS)",
            "Mark a folder for EFS encryption with cipher /e. Note: edit the path to your target folder first.", "用 cipher /e 將資料夾標記為 EFS 加密。注意：先改成你要嘅資料夾路徑。",
            "Encrypt", "加密", "cipher /e \"%USERPROFILE%\\Documents\\Private\"",
            keywords: "cipher,efs,encrypt,/e,加密"),
        
        Tweak.Cmd("vault.efs.decrypt-folder", "Decrypt a folder (EFS)", "解密資料夾 (EFS)",
            "Remove EFS encryption from a folder with cipher /d. Edit the path to your target folder.", "用 cipher /d 將資料夾移除 EFS 加密。記得改成你要嘅資料夾路徑。",
            "Decrypt", "解密", "cipher /d \"%USERPROFILE%\\Documents\\Private\"",
            keywords: "cipher,efs,decrypt,/d,解密"),
        
        Tweak.Cmd("vault.efs.wipe-free-space-c", "Wipe free space on C:", "抹除 C: 可用空間",
            "Overwrite all deleted/unallocated data on C: with cipher /w so erased files can't be recovered. This can take a long time.", "用 cipher /w 覆寫 C: 上面所有已刪除／未分配嘅資料，令刪咗嘅檔案唔可以還原。可能會好耐㗎。",
            "Wipe", "抹除", "cipher /w:C:\\",
            requiresAdmin: true, destructive: true, keywords: "cipher,/w,wipe,free space,secure,抹除,可用空間"),
        
        Tweak.Cmd("vault.efs.show-efs-certs", "Show EFS certificates", "顯示 EFS 憑證",
            "List your personal certificates with certutil; EFS certs use the Encrypting File System purpose.", "用 certutil 列出你嘅個人憑證；EFS 憑證係 Encrypting File System 用途嚟㗎。",
            "Show", "顯示", "certutil -user -store My",
            keywords: "efs,certificate,certutil,憑證"),
        
        Tweak.Shell("vault.efs.backup-efs-cert", "Backup EFS certificate", "備份 EFS 憑證",
            "Open the Certificate Manager so you can export (back up) your EFS certificate and private key.", "開啟憑證管理員，等你可以匯出（備份）你嘅 EFS 憑證同私鑰。",
            "certmgr.msc", "",
            keywords: "efs,backup,certificate,export,備份,憑證"),
        
        Tweak.Cmd("vault.efs.encrypt-subfolders", "Encrypt folder & subfolders", "加密資料夾及子資料夾",
            "Recursively mark a folder and all its subfolders for EFS encryption with cipher /e /s. Edit the path first.", "用 cipher /e /s 將資料夾連埋所有子資料夾遞迴標記為 EFS 加密。先改路徑。",
            "Encrypt", "加密", "cipher /e /s:\"%USERPROFILE%\\Documents\\Private\"",
            keywords: "cipher,efs,encrypt,/s,recursive,子資料夾,遞迴"),
        
        Tweak.Cmd("vault.efs.display-folder-state", "Display folder encryption state", "顯示資料夾加密狀態",
            "Run cipher with no switches in your Documents folder to display the encryption state of every file there.", "喺你嘅文件資料夾度行 cipher（唔加任何開關），顯示入面每個檔案嘅加密狀態。",
            "Display", "顯示", "cipher \"%USERPROFILE%\\Documents\"",
            keywords: "cipher,state,display,狀態,顯示"),
        
        Tweak.Cmd("vault.efs.show-subfolder-state", "Show state of subfolders", "顯示子資料夾狀態",
            "Use cipher /s on a folder to show the encryption state of it and all subfolders. Edit the path first.", "用 cipher /s 喺資料夾度顯示佢同所有子資料夾嘅加密狀態。先改路徑。",
            "Show", "顯示", "cipher /s:\"%USERPROFILE%\\Documents\"",
            keywords: "cipher,/s,subfolder,state,子資料夾,狀態"),
        
        Tweak.Cmd("vault.efs.create-new-key", "Create new EFS key", "建立新 EFS 金鑰",
            "Run cipher /k to generate a brand-new EFS certificate and key for the current user.", "行 cipher /k 為目前使用者產生一個全新嘅 EFS 憑證同金鑰。",
            "Create key", "建立金鑰", "cipher /k",
            keywords: "cipher,/k,key,certificate,金鑰,憑證"),
        
        Tweak.Shell("vault.efs.rekeywiz", "Manage EFS (rekeywiz)", "管理 EFS (rekeywiz)",
            "Open the Encrypting File System wizard (rekeywiz) to manage, update or back up your EFS certificates.", "開啟加密檔案系統精靈 (rekeywiz)，等你管理、更新或者備份你嘅 EFS 憑證。",
            "rekeywiz.exe", "",
            keywords: "efs,rekeywiz,manage,wizard,管理,精靈"),
        
        Tweak.Shell("vault.efs.open-cert-manager", "Open EFS cert manager", "開啟 EFS 憑證管理員",
            "Open the Personal certificates store in Certificate Manager where your EFS certificate lives.", "開啟憑證管理員入面嘅個人憑證存放區，你嘅 EFS 憑證就喺度。",
            "certmgr.msc", "",
            keywords: "efs,certificate,manager,certmgr,憑證,管理員"),
        
        Tweak.Cmd("vault.efs.secure-delete-file", "Secure-delete a file", "安全刪除檔案",
            "Delete a file, then wipe the free space on its drive with cipher /w so the data can't be recovered. Edit the file path first.", "刪除一個檔案，再用 cipher /w 抹除佢所在磁碟嘅可用空間，令資料唔可以還原。先改檔案路徑。",
            "Secure delete", "安全刪除", "del /f /q \"%USERPROFILE%\\Documents\\secret.txt\" && cipher /w:\"%USERPROFILE%\\Documents\"",
            requiresAdmin: true, destructive: true, keywords: "secure delete,wipe,cipher,/w,安全刪除,抹除"),
        
        Tweak.Cmd("vault.efs.cipher-version", "Show cipher help", "顯示 cipher 說明",
            "Display the cipher.exe help/usage banner, which lists every available switch.", "顯示 cipher.exe 嘅說明／用法資訊，列出所有可用開關。",
            "Show help", "顯示說明", "cipher /?",
            keywords: "cipher,version,help,版本,說明"),
        
        Tweak.Cmd("vault.efs.encrypt-documents", "Encrypt Documents folder", "加密文件資料夾",
            "Mark your whole Documents folder for EFS encryption with cipher /e. New files added later inherit encryption.", "用 cipher /e 將你成個文件資料夾標記為 EFS 加密。之後新增嘅檔案會繼承加密。",
            "Encrypt", "加密", "cipher /e \"%USERPROFILE%\\Documents\"",
            keywords: "cipher,efs,encrypt,documents,加密,文件"),
        
        Tweak.Cmd("vault.efs.list-encrypted-files", "List encrypted files", "列出已加密檔案",
            "Use cipher /u /n to find and list all EFS-encrypted files for the current user without updating keys.", "用 cipher /u /n 搵出並列出目前使用者所有 EFS 加密嘅檔案，唔會更新金鑰。",
            "List", "列出", "cipher /u /n",
            keywords: "cipher,/u,/n,list,encrypted,列出,加密"),
        
        Tweak.Cmd("vault.efs.reset-efs-keys", "Reset EFS keys (rekey)", "重設 EFS 金鑰",
            "Use cipher /u to update (rekey) all encrypted files to your current EFS certificate and keys.", "用 cipher /u 將所有已加密檔案更新（重設金鑰）到你目前嘅 EFS 憑證同金鑰。",
            "Reset", "重設", "cipher /u",
            keywords: "cipher,/u,rekey,reset,update,重設,金鑰"),
        
        Tweak.Cmd("vault.efs.wipe-free-space-folder", "Wipe free space on a drive", "抹除磁碟可用空間",
            "Overwrite deleted data on the drive that holds a chosen folder using cipher /w. Edit the path to target another drive.", "用 cipher /w 覆寫指定資料夾所在磁碟上面已刪除嘅資料。改路徑就可以指向另一隻磁碟。",
            "Wipe", "抹除", "cipher /w:\"%USERPROFILE%\"",
            requiresAdmin: true, destructive: true, keywords: "cipher,/w,wipe,free space,抹除,可用空間"),
        
        Tweak.Shell("vault.efs.add-recovery-agent", "Show EFS recovery agent", "顯示 EFS 復原代理",
            "Open the local security policy to view or add an EFS Data Recovery Agent under Public Key Policies.", "開啟本機安全性原則，喺公開金鑰原則底下檢視或新增 EFS 資料復原代理。",
            "secpol.msc", "",
            requiresAdmin: true, keywords: "efs,recovery agent,secpol,DRA,復原代理"),
        
        Tweak.Cmd("vault.efs.decrypt-documents", "Decrypt Documents folder", "解密文件資料夾",
            "Remove EFS encryption from your whole Documents folder and its files with cipher /d /s.", "用 cipher /d /s 將你成個文件資料夾連埋入面嘅檔案移除 EFS 加密。",
            "Decrypt", "解密", "cipher /d /s:\"%USERPROFILE%\\Documents\"",
            keywords: "cipher,/d,/s,decrypt,documents,解密,文件"),

        // --- certs (20) ---
        Tweak.Shell("vault.certs.open-certmgr", "Open Certificate Manager (User)", "開啟憑證管理員（使用者）",
            "Launch the current-user certificate manager (certmgr.msc).", "開啟目前使用者嘅憑證管理員（certmgr.msc）。",
            "Open", "開啟", "certmgr.msc", "", keywords: "certificate,certmgr,user,憑證,管理員"),
        
        Tweak.Shell("vault.certs.open-certlm", "Open Certificate Manager (Machine)", "開啟憑證管理員（電腦）",
            "Launch the local-machine certificate manager (certlm.msc).", "開啟本機電腦嘅憑證管理員（certlm.msc）。",
            "Open", "開啟", "certlm.msc", "", requiresAdmin: true, keywords: "certificate,certlm,machine,本機,憑證"),
        
        Tweak.Powershell("vault.certs.list-personal", "List Personal Certificates", "列出個人憑證",
            "List certificates in your personal (My) store with subject, thumbprint and expiry.", "列出你個人（My）憑證庫入面嘅憑證，連主體、指紋同到期日。",
            "List", "列出", "Get-ChildItem Cert:\\CurrentUser\\My | Format-Table Subject,Thumbprint,NotAfter -AutoSize",
            keywords: "personal,my,certificates,個人,憑證,指紋"),
        
        Tweak.Powershell("vault.certs.list-root-ca", "List Trusted Root CAs", "列出受信任根 CA",
            "Show the top trusted root certification authorities in the machine Root store.", "顯示本機 Root 憑證庫入面頭幾個受信任嘅根憑證授權單位。",
            "List", "列出", "Get-ChildItem Cert:\\LocalMachine\\Root | Sort-Object NotAfter | Select-Object -First 30 Subject,Thumbprint,NotAfter | Format-Table -AutoSize",
            keywords: "root,ca,trusted,根,授權,憑證"),
        
        Tweak.Powershell("vault.certs.list-expiring", "List Expiring Certificates", "列出即將到期憑證",
            "Find personal certificates that expire within the next 60 days.", "搵出未來 60 日內就會到期嘅個人憑證。",
            "Check", "檢查", "Get-ChildItem Cert:\\CurrentUser\\My | Where-Object { $_.NotAfter -lt (Get-Date).AddDays(60) } | Format-Table Subject,Thumbprint,NotAfter -AutoSize",
            keywords: "expiring,expire,60,到期,憑證,過期"),
        
        Tweak.Powershell("vault.certs.export-note", "Export a Certificate (Note)", "匯出憑證（提示）",
            "Show how to export a certificate to a .cer file using Export-Certificate.", "示範點樣用 Export-Certificate 將憑證匯出做 .cer 檔。",
            "Show", "顯示", "Write-Host 'Example: Export-Certificate -Cert Cert:\\CurrentUser\\My\\<Thumbprint> -FilePath $env:USERPROFILE\\Desktop\\cert.cer'; Write-Host 'Replace <Thumbprint> with a value from List Personal Certificates.'",
            keywords: "export,cer,匯出,憑證,提示"),
        
        Tweak.Shell("vault.certs.open-credman", "Open Credential Manager", "開啟認證管理員",
            "Launch the Windows Credential Manager (keymgr.dll) to manage stored credentials.", "開啟 Windows 認證管理員（keymgr.dll）去管理已儲存嘅認證。",
            "Open", "開啟", "control.exe", "keymgr.dll", keywords: "credential,manager,keymgr,認證,管理員"),
        
        Tweak.Cmd("vault.certs.list-windows-creds", "List Windows Credentials", "列出 Windows 認證",
            "List stored Windows credentials with cmdkey /list.", "用 cmdkey /list 列出已儲存嘅 Windows 認證。",
            "List", "列出", "cmdkey /list", keywords: "cmdkey,credentials,windows,認證,清單"),
        
        Tweak.Powershell("vault.certs.keystore-note", "KeyStore Location (Note)", "金鑰庫位置（提示）",
            "Show where the user certificate/key store lives on disk.", "顯示使用者憑證／金鑰庫喺磁碟上嘅位置。",
            "Show", "顯示", "Write-Host \"Certificate store paths: Cert:\\CurrentUser  and  Cert:\\LocalMachine\"; Write-Host \"Private keys: $env:APPDATA\\Microsoft\\Crypto  and  $env:APPDATA\\Microsoft\\SystemCertificates\"",
            keywords: "keystore,key,store,金鑰,憑證庫,位置"),
        
        Tweak.Powershell("vault.certs.code-signing", "Show Code-Signing Certificates", "顯示程式碼簽署憑證",
            "List certificates in your personal store that are valid for code signing.", "列出你個人憑證庫入面可以用嚟程式碼簽署嘅憑證。",
            "List", "列出", "Get-ChildItem Cert:\\CurrentUser\\My -CodeSigningCert | Format-Table Subject,Thumbprint,NotAfter -AutoSize",
            keywords: "code,signing,authenticode,簽署,憑證"),
        
        Tweak.Powershell("vault.certs.root-count", "Count Root Store Certificates", "統計根憑證庫數量",
            "Count how many certificates are in the machine trusted Root store.", "統計本機受信任 Root 憑證庫入面有幾多張憑證。",
            "Count", "統計", "(Get-ChildItem Cert:\\LocalMachine\\Root | Measure-Object).Count | ForEach-Object { Write-Host \"Root store certificate count: $_\" }",
            keywords: "root,count,store,根,數量,統計"),
        
        Tweak.Powershell("vault.certs.gpo-note", "Group Policy Cert Settings (Note)", "群組原則憑證設定（提示）",
            "Show where certificate-related policy lives in Group Policy.", "顯示憑證相關原則喺群組原則入面嘅位置。",
            "Show", "顯示", "Write-Host 'gpedit.msc > Computer Configuration > Windows Settings > Security Settings > Public Key Policies'; Write-Host 'Run gpedit.msc manually (Pro/Enterprise editions only).'",
            keywords: "group,policy,gpedit,public,key,群組原則,憑證"),
        
        Tweak.Powershell("vault.certs.check-thumbprint", "Check a Certificate Thumbprint", "檢查憑證指紋",
            "Search every store for a certificate by thumbprint (edit the value in the script).", "用指紋喺所有憑證庫搜尋一張憑證（喺指令碼入面改個值）。",
            "Check", "檢查", "$tp='ABCDEF0123456789ABCDEF0123456789ABCDEF01'; Get-ChildItem Cert:\\ -Recurse | Where-Object { $_.Thumbprint -eq $tp } | Format-Table PSParentPath,Subject,NotAfter -AutoSize",
            keywords: "thumbprint,check,search,指紋,檢查,憑證"),
        
        Tweak.Powershell("vault.certs.list-ca", "List Intermediate CA Certificates", "列出中繼 CA 憑證",
            "List certificates in the machine intermediate certification authorities (CA) store.", "列出本機中繼憑證授權單位（CA）憑證庫入面嘅憑證。",
            "List", "列出", "Get-ChildItem Cert:\\LocalMachine\\CA | Format-Table Subject,Thumbprint,NotAfter -AutoSize",
            keywords: "ca,intermediate,authority,中繼,授權,憑證"),
        
        Tweak.Shell("vault.certs.manage-user-certs", "Open Manage User Certificates", "開啟管理使用者憑證",
            "Open the Manage User Certificates console (certmgr.msc).", "開啟「管理使用者憑證」主控台（certmgr.msc）。",
            "Open", "開啟", "certmgr.msc", "", keywords: "manage,user,certificates,管理,使用者,憑證"),
        
        Tweak.Powershell("vault.certs.tpm-info", "Show TPM Information", "顯示 TPM 資訊",
            "Display Trusted Platform Module status with Get-Tpm.", "用 Get-Tpm 顯示信任平台模組（TPM）狀態。",
            "Show", "顯示", "Get-Tpm | Format-List", requiresAdmin: true, keywords: "tpm,trusted,platform,module,信任,平台,模組"),
        
        Tweak.Powershell("vault.certs.smartcard-note", "List Smart Card Readers (Note)", "列出智能卡讀卡機（提示）",
            "Enumerate smart card reader devices via WMI.", "用 WMI 列舉智能卡讀卡機裝置。",
            "List", "列出", "Get-CimInstance Win32_PnPEntity | Where-Object { $_.Name -match 'Smart Card|SmartCard' } | Format-Table Name,Status -AutoSize",
            keywords: "smartcard,smart,card,reader,智能卡,讀卡機"),
        
        Tweak.Shell("vault.certs.open-hello", "Open Windows Hello Settings", "開啟 Windows Hello 設定",
            "Open the Windows sign-in options page for Windows Hello.", "開啟 Windows Hello 嘅登入選項設定頁。",
            "Open", "開啟", "ms-settings:signinoptions", "", keywords: "windows,hello,signin,signinoptions,登入,設定"),
        
        Tweak.Powershell("vault.certs.list-my-store-all", "List All My-Store Certificate Fields", "列出個人憑證庫詳細欄位",
            "Show issuer, subject and validity for every certificate in the personal store.", "顯示個人憑證庫每張憑證嘅簽發者、主體同有效期。",
            "List", "列出", "Get-ChildItem Cert:\\CurrentUser\\My | Format-Table Subject,Issuer,NotBefore,NotAfter -AutoSize",
            keywords: "my,store,issuer,subject,個人,簽發者,憑證"),
        
        Tweak.Cmd("vault.certs.cert-stores-list", "List Certificate Stores", "列出憑證庫",
            "Dump the current-user My certificate store with certutil.", "用 certutil 列出目前使用者 My 憑證庫入面嘅憑證。",
            "List", "列出", "certutil -user -store My", keywords: "certutil,store,my,憑證庫,清單"),

        // --- defender (20) ---
        Tweak.Powershell("vault.defender.status", "Defender status", "Defender 狀態",
            "Show Microsoft Defender antivirus engine, signature and real-time protection status.", "顯示 Microsoft Defender 防毒引擎、特徵碼同即時保護嘅狀態。",
            "Get status", "睇狀態", "Get-MpComputerStatus | Format-List",
            requiresAdmin: true, keywords: "defender,status,antivirus,狀態,防毒"),
        
        Tweak.Powershell("vault.defender.preferences", "Defender preferences", "Defender 偏好設定",
            "Display key Microsoft Defender preference settings such as scan and real-time options.", "顯示 Microsoft Defender 嘅主要偏好設定，例如掃描同即時保護選項。",
            "Get prefs", "睇設定", "Get-MpPreference | Select-Object DisableRealtimeMonitoring,DisableBehaviorMonitoring,DisableIOAVProtection,MAPSReporting,SubmitSamplesConsent,CloudBlockLevel,ScanScheduleDay | Format-List",
            requiresAdmin: true, keywords: "defender,preference,setting,偏好,設定"),
        
        Tweak.Powershell("vault.defender.add-exclusion", "Add scan exclusion (C:\\Temp)", "新增掃描排除 (C:\\Temp)",
            "Add C:\\Temp as a Defender exclusion path so it is skipped during scans. Edit the path as needed.", "將 C:\\Temp 加入 Defender 排除路徑，掃描時會跳過。可自行改路徑。",
            "Add exclusion", "加排除", "Add-MpPreference -ExclusionPath 'C:\\Temp'",
            requiresAdmin: true, keywords: "defender,exclusion,exclude,排除,例外"),
        
        Tweak.Powershell("vault.defender.remove-exclusion", "Remove scan exclusion (C:\\Temp)", "移除掃描排除 (C:\\Temp)",
            "Remove the C:\\Temp Defender exclusion path so it is scanned again. Edit the path as needed.", "將 C:\\Temp 由 Defender 排除路徑移除，之後會重新掃描。可自行改路徑。",
            "Remove exclusion", "移排除", "Remove-MpPreference -ExclusionPath 'C:\\Temp'",
            requiresAdmin: true, keywords: "defender,exclusion,remove,移除,排除"),
        
        Tweak.Powershell("vault.defender.list-exclusions", "List exclusions", "列出排除清單",
            "List all Defender exclusion paths, extensions and processes currently configured.", "列出 Defender 而家設定嘅所有排除路徑、副檔名同程序。",
            "List", "列出", "Get-MpPreference | Select-Object ExclusionPath,ExclusionExtension,ExclusionProcess | Format-List",
            requiresAdmin: true, keywords: "defender,exclusion,list,排除,清單"),
        
        Tweak.Powershell("vault.defender.quick-scan", "Quick scan", "快速掃描",
            "Run a Microsoft Defender quick scan of the most likely infection locations.", "用 Microsoft Defender 快速掃描最易中招嘅位置。",
            "Quick scan", "快速掃描", "Start-MpScan -ScanType QuickScan",
            requiresAdmin: true, keywords: "defender,scan,quick,掃描,快速"),
        
        Tweak.Powershell("vault.defender.full-scan", "Full scan", "完整掃描",
            "Run a Microsoft Defender full scan of every file and running program. This can take a long time.", "用 Microsoft Defender 完整掃描所有檔案同執行緊嘅程式，會掃好耐㗎。",
            "Full scan", "完整掃描", "Start-MpScan -ScanType FullScan",
            requiresAdmin: true, keywords: "defender,scan,full,掃描,完整"),
        
        Tweak.Powershell("vault.defender.update-signatures", "Update signatures", "更新特徵碼",
            "Download and install the latest Microsoft Defender threat definition updates.", "下載並安裝最新嘅 Microsoft Defender 威脅特徵碼更新。",
            "Update", "更新", "Update-MpSignature",
            requiresAdmin: true, keywords: "defender,signature,update,definition,特徵碼,更新"),
        
        Tweak.Powershell("vault.defender.threat-history", "Threat history", "威脅記錄",
            "Show the history of threats detected by Microsoft Defender on this device.", "顯示呢部機 Microsoft Defender 偵測到嘅威脅記錄。",
            "Show threats", "睇威脅", "Get-MpThreat | Format-List; Get-MpThreatDetection | Format-List",
            requiresAdmin: true, keywords: "defender,threat,history,威脅,記錄"),
        
        Tweak.Powershell("vault.defender.firewall-status", "Firewall status", "防火牆狀態",
            "Show Windows Firewall state and settings for all profiles (domain, private, public).", "顯示所有設定檔（網域、私人、公用）嘅 Windows 防火牆狀態同設定。",
            "Show status", "睇狀態", "netsh advfirewall show allprofiles",
            requiresAdmin: true, keywords: "firewall,status,netsh,防火牆,狀態"),
        
        Tweak.Powershell("vault.defender.firewall-rules", "List firewall rules", "列出防火牆規則",
            "List the first 50 enabled Windows Firewall rules with their direction and action.", "列出頭 50 條已啟用嘅 Windows 防火牆規則，連方向同動作。",
            "List rules", "列規則", "Get-NetFirewallRule | Where-Object { $_.Enabled -eq 'True' } | Select-Object -First 50 DisplayName,Direction,Action,Profile | Format-Table -AutoSize",
            requiresAdmin: true, keywords: "firewall,rule,list,防火牆,規則"),
        
        Tweak.Powershell("vault.defender.block-app", "Block an app (note)", "封鎖應用程式 (說明)",
            "Show the command to create an outbound firewall rule blocking an app. Edit the program path before running.", "顯示建立封鎖某程式對外連線嘅防火牆規則指令。執行前請改程式路徑。",
            "Show note", "睇說明", "Write-Host 'To block an app outbound, run (edit the path):'; Write-Host 'New-NetFirewallRule -DisplayName \"Block MyApp\" -Direction Outbound -Program \"C:\\Path\\app.exe\" -Action Block'",
            requiresAdmin: true, keywords: "firewall,block,app,封鎖,程式"),
        
        Tweak.Powershell("vault.defender.enable-fw-logging", "Enable firewall logging", "啟用防火牆記錄",
            "Enable logging of dropped and allowed connections for all Windows Firewall profiles.", "為所有 Windows 防火牆設定檔啟用被擋同允許連線嘅記錄。",
            "Enable logging", "開記錄", "netsh advfirewall set allprofiles logging droppedconnections enable; netsh advfirewall set allprofiles logging allowedconnections enable",
            requiresAdmin: true, keywords: "firewall,logging,log,記錄,防火牆"),
        
        Tweak.Powershell("vault.defender.reset-firewall", "Reset firewall to defaults", "重設防火牆做預設",
            "Reset Windows Firewall to its default policy, removing all custom rules. This cannot be undone.", "將 Windows 防火牆重設返做預設政策，會清走所有自訂規則，無得還原。",
            "Reset", "重設", "netsh advfirewall reset",
            requiresAdmin: true, destructive: true, keywords: "firewall,reset,default,重設,防火牆"),
        
        Tweak.Powershell("vault.defender.firewall-on", "Turn firewall on", "開防火牆",
            "Turn Windows Firewall on for all profiles (domain, private and public).", "為所有設定檔（網域、私人、公用）開 Windows 防火牆。",
            "Turn on", "開", "netsh advfirewall set allprofiles state on",
            requiresAdmin: true, keywords: "firewall,on,enable,開,防火牆"),
        
        Tweak.Powershell("vault.defender.firewall-off", "Turn firewall off", "熄防火牆",
            "Turn Windows Firewall off for all profiles. This leaves the device exposed; not recommended.", "為所有設定檔熄 Windows 防火牆，會令部機冇保護，唔建議咁做。",
            "Turn off", "熄", "netsh advfirewall set allprofiles state off",
            requiresAdmin: true, destructive: true, keywords: "firewall,off,disable,熄,防火牆"),
        
        Tweak.Powershell("vault.defender.blocked-connections", "Show blocked connections (note)", "顯示被擋連線 (說明)",
            "Read the firewall log to view recently dropped (blocked) connections. Requires logging to be enabled.", "讀取防火牆記錄，睇近期被擋（DROP）嘅連線。需先啟用記錄。",
            "Show drops", "睇被擋", "if (Test-Path \"$env:windir\\system32\\LogFiles\\Firewall\\pfirewall.log\") { Get-Content \"$env:windir\\system32\\LogFiles\\Firewall\\pfirewall.log\" | Select-String ' DROP ' | Select-Object -Last 30 } else { Write-Host 'Firewall log not found. Enable firewall logging first.' }",
            requiresAdmin: true, keywords: "firewall,blocked,drop,被擋,連線"),
        
        Tweak.Powershell("vault.defender.controlled-folder-access", "Enable controlled folder access", "啟用受控資料夾存取",
            "Enable Microsoft Defender controlled folder access to protect folders from ransomware.", "啟用 Microsoft Defender 受控資料夾存取，保護資料夾免受勒索軟件破壞。",
            "Enable CFA", "開保護", "Set-MpPreference -EnableControlledFolderAccess Enabled; Write-Host 'Controlled folder access enabled. Use Get-MpPreference to review.'",
            requiresAdmin: true, keywords: "defender,controlled,folder,ransomware,勒索,資料夾"),
        
        Tweak.Powershell("vault.defender.set-cloud-level", "Set cloud protection level (high)", "設定雲端保護等級 (高)",
            "Set Microsoft Defender cloud-delivered protection blocking level to High via Set-MpPreference.", "用 Set-MpPreference 將 Microsoft Defender 雲端保護封鎖等級設做「高」。",
            "Set high", "設高", "Set-MpPreference -CloudBlockLevel High; Set-MpPreference -MAPSReporting Advanced; Write-Host 'Cloud block level set to High.'",
            requiresAdmin: true, keywords: "defender,cloud,protection,level,雲端,保護,等級"),
        
        Tweak.Powershell("vault.defender.version", "Show Defender version", "顯示 Defender 版本",
            "Show Microsoft Defender engine, antivirus and signature version numbers.", "顯示 Microsoft Defender 引擎、防毒同特徵碼嘅版本號。",
            "Show version", "睇版本", "Get-MpComputerStatus | Select-Object AMEngineVersion,AMProductVersion,AntivirusSignatureVersion,NISSignatureVersion,AntivirusSignatureLastUpdated | Format-List",
            requiresAdmin: true, keywords: "defender,version,engine,版本"),
        
        Tweak.Powershell("vault.defender.asr-list", "List ASR rules (with names)", "列出 ASR 規則（連名）",
            "List every configured Attack Surface Reduction rule with its human-readable name and current action (Enabled / AuditMode / Disabled / Warn). Names are resolved at runtime from the GUID, so unknown rules still show their GUID.", "列出每條已設定嘅攻擊面收窄（ASR）規則，連人類可讀名稱同目前動作（啟用／稽核／停用／警告）。名稱喺執行時由 GUID 解析，未知規則都會顯示 GUID。",
            "List", "列出", AsrNameMapPs + @"
$p = Get-MpPreference; $ids = $p.AttackSurfaceReductionRules_Ids; $acts = $p.AttackSurfaceReductionRules_Actions;
$actionNames = @{0='Disabled';1='Enabled';2='AuditMode';6='Warn'};
if (-not $ids -or $ids.Count -eq 0) { Write-Host 'No ASR rules are configured on this machine.'; return }
$rows = for ($i=0; $i -lt $ids.Count; $i++) {
  $g = [string]$ids[$i]; $a = [int]$acts[$i];
  [pscustomobject]@{ Rule = $(if ($AsrNames.ContainsKey($g.ToLower())) { $AsrNames[$g.ToLower()] } else { '(unknown rule)' }); Action = $(if ($actionNames.ContainsKey($a)) { $actionNames[$a] } else { ""Action$a"" }); Guid = $g }
}
$rows | Format-Table -AutoSize -Wrap",
            requiresAdmin: true, keywords: "defender,asr,attack surface reduction,規則,收窄,rules,guid"),

        Tweak.Powershell("vault.defender.asr-lsass-enable", "ASR: block LSASS theft (Enabled)", "ASR：封鎖 LSASS 竊取（啟用）",
            "Enable the Attack Surface Reduction rule that blocks credential stealing from the Windows local security authority (lsass.exe). Set to Enabled (block).", "啟用「封鎖由 Windows 本機安全性授權（lsass.exe）竊取憑證」嘅攻擊面收窄規則，設為「啟用」（封鎖）。",
            "Enable", "啟用", "Add-MpPreference -AttackSurfaceReductionRules_Ids 9e6c4e1f-7d60-472f-ba1a-a39ef669e4b2 -AttackSurfaceReductionRules_Actions Enabled; Write-Host 'ASR rule 9e6c4e1f... (Block credential stealing from LSASS) set to Enabled.'",
            requiresAdmin: true, keywords: "defender,asr,lsass,credential,憑證,封鎖,enabled"),

        Tweak.Powershell("vault.defender.asr-lsass-audit", "ASR: block LSASS theft (Audit)", "ASR：封鎖 LSASS 竊取（稽核）",
            "Set the LSASS credential-theft ASR rule to AuditMode so it only logs what it would block, without enforcing.", "將 LSASS 憑證竊取嘅 ASR 規則設為「稽核模式」，淨係記錄會封鎖咩，唔真係執行封鎖。",
            "Audit", "稽核", "Add-MpPreference -AttackSurfaceReductionRules_Ids 9e6c4e1f-7d60-472f-ba1a-a39ef669e4b2 -AttackSurfaceReductionRules_Actions AuditMode; Write-Host 'ASR rule 9e6c4e1f... set to AuditMode.'",
            requiresAdmin: true, keywords: "defender,asr,lsass,audit,稽核,模式"),

        Tweak.Powershell("vault.defender.asr-lsass-warn", "ASR: block LSASS theft (Warn)", "ASR：封鎖 LSASS 竊取（警告）",
            "Set the LSASS credential-theft ASR rule to Warn so the user is prompted and can allow the action.", "將 LSASS 憑證竊取嘅 ASR 規則設為「警告」，會提示使用者，佢可以選擇允許。",
            "Warn", "警告", "Add-MpPreference -AttackSurfaceReductionRules_Ids 9e6c4e1f-7d60-472f-ba1a-a39ef669e4b2 -AttackSurfaceReductionRules_Actions Warn; Write-Host 'ASR rule 9e6c4e1f... set to Warn.'",
            requiresAdmin: true, keywords: "defender,asr,lsass,warn,警告"),

        Tweak.Powershell("vault.defender.asr-lsass-disable", "ASR: block LSASS theft (Disabled)", "ASR：封鎖 LSASS 竊取（停用）",
            "Disable the LSASS credential-theft ASR rule so it no longer blocks, audits or warns.", "停用 LSASS 憑證竊取嘅 ASR 規則，唔再封鎖、稽核或者警告。",
            "Disable", "停用", "Add-MpPreference -AttackSurfaceReductionRules_Ids 9e6c4e1f-7d60-472f-ba1a-a39ef669e4b2 -AttackSurfaceReductionRules_Actions Disabled; Write-Host 'ASR rule 9e6c4e1f... set to Disabled.'",
            requiresAdmin: true, keywords: "defender,asr,lsass,disable,停用"),

        Tweak.Powershell("vault.defender.asr-all-audit", "ASR: all rules to Audit mode", "ASR：全部規則設稽核",
            "Set every well-known Attack Surface Reduction rule to AuditMode at once — a safe way to see what ASR would block on this machine before enforcing.", "一次過將所有常見攻擊面收窄規則設為稽核模式 — 喺真正執行之前，安全咁睇下 ASR 喺呢部機會封鎖咩。",
            "Audit all", "全部稽核", AsrNameMapPs + @"
foreach ($g in $AsrNames.Keys) { Add-MpPreference -AttackSurfaceReductionRules_Ids $g -AttackSurfaceReductionRules_Actions AuditMode }
Write-Host (""Set "" + $AsrNames.Count + "" ASR rules to AuditMode. Use 'List ASR rules' to review."")",
            requiresAdmin: true, keywords: "defender,asr,audit,all,全部,稽核,規則"),

        Tweak.RegChoice("vault.defender.cloud-level-policy", "Cloud protection level (policy)", "雲端保護等級 (政策)",
            "Set the Microsoft Defender cloud-delivered protection blocking aggressiveness level via policy.", "用群組原則設定 Microsoft Defender 雲端保護嘅封鎖積極程度。",
            RegRoot.HKLM, @"SOFTWARE\Policies\Microsoft\Windows Defender\Spynet", "CloudBlockLevel", RegistryValueKind.DWord,
            new (string en, string zh, object value)[] {
                ("Default", "預設", 0),
                ("High", "高", 2),
                ("High Plus", "更高", 4),
                ("Zero tolerance", "零容忍", 6)
            },
            requiresAdmin: true, keywords: "defender,cloud,protection,level,雲端,保護,等級"),
    };

    /// <summary>
    /// 驅動已綁定去品牌引擎嘅快速動作 · A quick-action that resolves the bundled de-branded vault engine
    /// at runtime (mount binary or format binary) and shells out to it — never hardcodes a "VeraCrypt" path.
    /// </summary>
    private static TweakDefinition VaultShell(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, bool mount, string args,
        bool elevated = false, bool destructive = false, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            async ct =>
            {
                var exe = mount ? VaultVolumeService.FindMountBinary() : VaultVolumeService.FindFormatBinary();
                if (exe is null)
                    return TweakResult.Fail("WinForge Vault engine not found — install it from the Vault module.",
                        "搵唔到 WinForge 保險庫引擎 — 喺保險庫模組度安裝。");
                return await ShellRunner.Run(exe, args, elevated, ct);
            },
            requiresAdmin: elevated, destructive: destructive, keywords: keywords);
}
