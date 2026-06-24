using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 系統與開機 · System &amp; Boot tweaks.
/// 全部用真實 Windows 11 登錄檔路徑同指令 · All real Windows 11 registry paths and commands.
/// </summary>
public static class SystemTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.RegToggle("system.long-paths", "Enable long path support", "啟用長路徑支援",
            "Allow file paths longer than 260 characters across Windows.", "畀 Windows 用超過 260 個字元嘅檔案路徑。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled",
            onValue: 1, offValue: 0, requiresAdmin: true, restart: RestartScope.SignOut,
            keywords: "path,maxpath,260,長路徑"),

        Tweak.RegToggle("system.developer-mode", "Developer Mode", "開發人員模式",
            "Turn on app sideloading and developer features.", "開啟 App 側載同開發人員功能。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock", "AllowDevelopmentWithoutDevLicense",
            onValue: 1, offValue: 0, requiresAdmin: true,
            keywords: "developer,sideload,開發,側載"),

        Tweak.RegToggle("system.clipboard-history", "Clipboard history", "剪貼簿記錄",
            "Keep a history of copied items, opened with Win+V.", "保留複製過嘅項目記錄，用 Win+V 開。",
            RegRoot.HKCU, @"Software\Microsoft\Clipboard", "EnableClipboardHistory",
            onValue: 1, offValue: 0,
            keywords: "clipboard,win+v,剪貼簿"),

        Tweak.RegToggle("system.cloud-clipboard", "Cloud clipboard sync", "雲端剪貼簿同步",
            "Automatically upload copied items to roam across your devices via your Microsoft account.",
            "自動上載複製嘅項目，透過 Microsoft 帳戶喺各部裝置之間漫遊。",
            RegRoot.HKCU, @"Software\Microsoft\Clipboard", "CloudClipboardAutomaticUpload",
            onValue: 1, offValue: 0,
            keywords: "clipboard,cloud,sync,roam,剪貼簿,雲端,同步"),

        Tweak.RegToggle("system.verbose-status", "Verbose sign-in messages", "詳細登入訊息",
            "Show detailed status messages during sign-in and sign-out.", "喺登入同登出時顯示詳細狀態訊息。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "VerboseStatus",
            onValue: 1, offValue: 0, requiresAdmin: true, restart: RestartScope.SignOut,
            keywords: "verbose,logon,status,登入"),

        Tweak.RegToggle("system.linked-connections", "Mapped-drive reconnect fix", "網絡磁碟重連修正",
            "Let elevated and non-elevated apps share the same mapped network drives (fixes drives showing as disconnected).",
            "令提權同非提權程式共用同一批已對應網絡磁碟（修正磁碟顯示為已中斷）。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLinkedConnections",
            onValue: 1, offValue: 0, requiresAdmin: true, restart: RestartScope.Reboot,
            keywords: "mapped drive,network drive,linked connections,reconnect,網絡磁碟,重連"),

        Tweak.RegToggle("system.auto-reboot", "Auto-restart on crash", "當機自動重新開機",
            "Automatically reboot after a blue screen (BSOD).", "藍畫面之後自動重新開機。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\CrashControl", "AutoReboot",
            onValue: 1, offValue: 0, requiresAdmin: true, restart: RestartScope.Reboot,
            keywords: "bsod,crash,reboot,藍畫面,當機"),

        Tweak.RegChoice("system.numlock-startup", "NumLock at startup", "開機 NumLock",
            "Set whether NumLock is on when Windows starts.", "設定 Windows 開機時 NumLock 開唔開。",
            RegRoot.HKU, @".DEFAULT\Control Panel\Keyboard", "InitialKeyboardIndicators",
            RegistryValueKind.String,
            new (string en, string zh, object value)[] { ("On", "開", "2"), ("Off", "熄", "0") },
            requiresAdmin: true, restart: RestartScope.Reboot,
            keywords: "numlock,keyboard,數字鎖,鍵盤"),

        Tweak.RegChoice("system.hung-app-timeout", "Hung app close timeout", "無回應程式關閉等候",
            "How long Windows waits before offering to close a frozen app.", "Windows 等幾耐先提出關閉當咗機嘅程式。",
            RegRoot.HKCU, @"Control Panel\Desktop", "HungAppTimeout",
            RegistryValueKind.String,
            new (string en, string zh, object value)[]
            {
                ("1s", "1秒", "1000"),
                ("3s", "3秒", "3000"),
                ("5s (default)", "5秒(預設)", "5000"),
            },
            restart: RestartScope.SignOut,
            keywords: "hung,timeout,frozen,無回應,當機"),

        Tweak.Powershell("system.restore-point", "Create a restore point", "建立還原點",
            "Create a System Restore point right now.", "即刻建立一個系統還原點。",
            "Create", "建立",
            "Checkpoint-Computer -Description 'WinForge' -RestorePointType MODIFY_SETTINGS",
            requiresAdmin: true,
            keywords: "restore,checkpoint,還原點"),

        Tweak.Powershell("system.enable-protection", "Enable System Protection (C:)", "啟用系統保護 (C:)",
            "Turn on System Protection for the C: drive.", "為 C: 磁碟開啟系統保護。",
            "Enable", "啟用",
            "Enable-ComputerRestore -Drive 'C:\\'",
            requiresAdmin: true,
            keywords: "protection,restore,系統保護"),

        Tweak.Powershell("system.god-mode", "Create God Mode folder", "建立 God Mode 資料夾",
            "Drop an all-settings God Mode folder on the Desktop.", "喺桌面整一個集齊所有設定嘅 God Mode 資料夾。",
            "Create", "建立",
            "New-Item -ItemType Directory -Path \"$env:USERPROFILE\\Desktop\\GodMode.{ED7BA470-8E54-465E-825C-99712043E01C}\" -Force | Out-Null",
            keywords: "god mode,settings,所有設定"),

        Tweak.Cmd("system.boot-uefi", "Restart to UEFI firmware", "重新開機入 UEFI 韌體",
            "Reboot directly into the UEFI/BIOS firmware settings.", "直接重新開機入 UEFI/BIOS 韌體設定。",
            "Restart", "重新開機", "shutdown /r /fw /t 3",
            requiresAdmin: true, destructive: true,
            keywords: "uefi,bios,firmware,韌體"),

        Tweak.Cmd("system.boot-recovery", "Restart to Advanced startup", "重新開機入進階啟動",
            "Reboot into the Advanced startup recovery menu.", "重新開機入進階啟動復原選單。",
            "Restart", "重新開機", "shutdown /r /o /t 3",
            requiresAdmin: true, destructive: true,
            keywords: "recovery,advanced startup,winre,復原,進階啟動"),

        Tweak.Cmd("system.env-vars", "Edit Environment Variables", "編輯環境變數",
            "Open the Windows Environment Variables editor.", "開啟 Windows 環境變數編輯器。",
            "Open", "開啟", "rundll32.exe sysdm.cpl,EditEnvironmentVariables",
            keywords: "environment,path,variables,環境變數"),
    };
}