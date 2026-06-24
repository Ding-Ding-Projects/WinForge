using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 保安相關調校 · Security-related tweaks (UAC, SmartScreen, sign-in, firewall, Defender).
/// 唔會關閉 Defender 即時保護（Tamper Protection 會阻止，會係假調校）。
/// Never disables Defender real-time protection — Tamper Protection blocks it.
/// </summary>
public static class SecurityTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.RegChoice("security.uac-prompt", "UAC prompt behaviour", "UAC 提示行為",
            "Choose how strict the User Account Control elevation prompt is.",
            "揀 UAC 提權提示有幾嚴格。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
            "ConsentPromptBehaviorAdmin", RegistryValueKind.DWord,
            new (string en, string zh, object value)[]
            {
                ("Never notify", "唔通知", 0),
                ("Default", "預設", 5),
                ("Always notify", "次次都通知", 2),
            },
            requiresAdmin: true, keywords: "uac,使用者帳戶控制,consent"),

        Tweak.RegToggle("security.uac-secure-desktop", "Dim the desktop for UAC", "UAC 時將桌面變暗",
            "Show the UAC prompt on the secure desktop (dims the screen).",
            "喺安全桌面顯示 UAC 提示，會將成個畫面變暗。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
            "PromptOnSecureDesktop", onValue: 1, offValue: 0,
            requiresAdmin: true, keywords: "uac,secure desktop,安全桌面"),

        Tweak.RegChoice("security.smartscreen-apps", "SmartScreen for apps & files", "應用程式同檔案 SmartScreen",
            "Control SmartScreen checking of downloaded apps and files.",
            "控制 SmartScreen 點樣檢查下載返嚟嘅應用程式同檔案。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
            "SmartScreenEnabled", RegistryValueKind.String,
            new (string en, string zh, object value)[]
            {
                ("Warn", "警告", "Warn"),
                ("Prompt (admin)", "提示", "Prompt"),
                ("Off", "熄", "Off"),
            },
            requiresAdmin: true, keywords: "smartscreen,智慧型畫面"),

        Tweak.RegToggle("security.smartscreen-edge", "SmartScreen for Store & web content", "商店同網頁內容 SmartScreen",
            "Toggle SmartScreen evaluation of Microsoft Store and web content.",
            "開關 SmartScreen 對 Microsoft Store 同網頁內容嘅檢查。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\AppHost",
            "EnableWebContentEvaluation", onValue: 1, offValue: 0,
            keywords: "smartscreen,web content,網頁"),

        Tweak.RegToggle("security.require-cad", "Require Ctrl+Alt+Del at sign-in", "登入要按 Ctrl+Alt+Del",
            "Force the secure attention sequence before the sign-in screen.",
            "登入畫面之前強制要先撳安全序列鍵。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
            "DisableCAD", onValue: 0, offValue: 1,
            requiresAdmin: true, restart: RestartScope.SignOut, keywords: "ctrl alt del,登入,sign-in"),

        Tweak.RegToggle("security.hide-last-user", "Hide last signed-in user name", "隱藏上次登入嘅使用者名稱",
            "Do not display the last account name on the sign-in screen.",
            "唔喺登入畫面顯示上次登入嗰個帳戶名。",
            RegRoot.HKLM, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System",
            "DontDisplayLastUserName", onValue: 1, offValue: 0,
            requiresAdmin: true, restart: RestartScope.SignOut, keywords: "last user,登入,privacy"),

        Tweak.RegToggle("security.remote-desktop", "Enable Remote Desktop", "開啟遠端桌面",
            "Allow incoming Remote Desktop (RDP) connections to this PC.",
            "允許其他電腦透過遠端桌面（RDP）連入嚟呢部機。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\Terminal Server",
            "fDenyTSConnections", onValue: 0, offValue: 1,
            requiresAdmin: true, restart: RestartScope.Reboot, keywords: "rdp,remote desktop,遠端"),

        Tweak.Cmd("security.firewall-off", "Turn Windows Firewall off (all profiles)", "熄咗 Windows 防火牆（所有設定檔）",
            "Disables the firewall on every profile — leaves the PC exposed.",
            "喺所有設定檔熄咗防火牆，會令部機冇咗保護。",
            "Turn off", "熄咗佢", "netsh advfirewall set allprofiles state off",
            requiresAdmin: true, destructive: true, keywords: "firewall,防火牆,netsh"),

        Tweak.Cmd("security.firewall-on", "Turn Windows Firewall on (all profiles)", "開返 Windows 防火牆（所有設定檔）",
            "Re-enables the firewall on every profile.",
            "喺所有設定檔重新開返防火牆。",
            "Turn on", "開返佢", "netsh advfirewall set allprofiles state on",
            requiresAdmin: true, keywords: "firewall,防火牆,netsh"),

        Tweak.Cmd("security.open-security-app", "Open Windows Security", "開啟 Windows 安全性",
            "Launches the Windows Security app to review protection status.",
            "開啟 Windows 安全性 App 睇下保護狀態。",
            "Open", "開啟", "start windowsdefender:",
            keywords: "windows security,defender,安全性"),

        Tweak.Cmd("security.bitlocker-status", "Show BitLocker status", "顯示 BitLocker 狀態",
            "Reports the encryption status of every drive.",
            "報告每個磁碟機嘅加密狀態。",
            "Check", "查詢", "manage-bde -status",
            requiresAdmin: true, keywords: "bitlocker,encryption,加密"),

        Tweak.Powershell("security.defender-exclude-downloads", "Exclude Downloads from Defender scans", "將 Downloads 排除喺 Defender 掃描之外",
            "Adds the Downloads folder to Microsoft Defender's exclusion list.",
            "將 Downloads 資料夾加入 Microsoft Defender 嘅排除清單。",
            "Add", "新增",
            "Add-MpPreference -ExclusionPath \"$env:USERPROFILE\\Downloads\"",
            requiresAdmin: true, keywords: "defender,exclusion,排除,downloads"),

        Tweak.Powershell("security.defender-quick-scan", "Run a quick Defender scan", "行一次 Defender 快速掃描",
            "Starts a Microsoft Defender quick antivirus scan.",
            "開始一次 Microsoft Defender 快速防毒掃描。",
            "Scan", "掃描",
            "Start-MpScan -ScanType QuickScan",
            requiresAdmin: true, keywords: "defender,scan,掃描,antivirus"),

        Tweak.Powershell("security.defender-update", "Update Defender definitions", "更新 Defender 病毒定義",
            "Downloads the latest Microsoft Defender security intelligence updates.",
            "下載最新嘅 Microsoft Defender 安全情報更新。",
            "Update", "更新",
            "Update-MpSignature",
            requiresAdmin: true, keywords: "defender,signature,定義,update"),
    };
}