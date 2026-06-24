using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 應用程式同開機項目（主要係動作）· Apps &amp; Startup — mostly one-shot actions.
/// 假設 winget 已經安裝 · Assumes winget is present.
/// </summary>
public static class AppsTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Cmd("apps.winget-upgrade-list", "List available app updates", "列出可更新嘅應用程式",
            "Show which installed apps have newer versions available via winget.", "用 winget 睇吓邊啲已安裝嘅應用程式有新版本。",
            "List", "列出", "winget upgrade",
            keywords: "winget,update,upgrade,更新"),

        Tweak.Cmd("apps.winget-upgrade-all", "Update all apps", "更新所有應用程式",
            "Upgrade every app that winget can update, accepting all agreements.", "用 winget 更新所有應用程式，並接受所有協議。",
            "Update all", "全部更新",
            "winget upgrade --all --accept-source-agreements --accept-package-agreements",
            requiresAdmin: true, keywords: "winget,update,upgrade,全部,更新"),

        Tweak.Cmd("apps.winget-list", "List installed apps", "列出已安裝應用程式",
            "Show all apps winget knows are installed on this device.", "列出 winget 認得、已經裝喺呢部機嘅應用程式。",
            "List", "列出", "winget list",
            keywords: "winget,installed,已安裝"),

        Tweak.Cmd("apps.store-updates", "Update Microsoft Store apps", "更新 Microsoft Store 應用程式",
            "Open the Microsoft Store downloads page to update Store apps.", "開 Microsoft Store 下載頁面去更新商店應用程式。",
            "Open Store", "開商店", "start ms-windows-store://downloadsandupdates",
            keywords: "store,商店,update,更新"),

        Tweak.Cmd("apps.startup-settings", "Open Startup apps settings", "開啟開機應用程式設定",
            "Open Settings to control which apps launch at sign-in.", "開設定去管理邊啲應用程式喺登入時自動啟動。",
            "Open", "開啟", "start ms-settings:startupapps",
            keywords: "startup,開機,啟動,settings"),

        Tweak.Cmd("apps.installed-settings", "Open Installed apps settings", "開啟已安裝應用程式設定",
            "Open Settings to view, repair or uninstall installed apps.", "開設定去檢視、修復或解除安裝已安裝嘅應用程式。",
            "Open", "開啟", "start ms-settings:appsfeatures",
            keywords: "apps,uninstall,解除安裝,settings"),

        Tweak.Cmd("apps.default-apps-settings", "Open Default apps settings", "開啟預設應用程式設定",
            "Open Settings to choose default apps for files and links.", "開設定去揀檔案同連結嘅預設應用程式。",
            "Open", "開啟", "start ms-settings:defaultapps",
            keywords: "default,預設,settings"),

        Tweak.Cmd("apps.restart-explorer", "Restart File Explorer", "重新啟動檔案總管",
            "Stop and relaunch explorer.exe to refresh the shell and taskbar.", "停咗再開返 explorer.exe，刷新桌面同工作列。",
            "Restart", "重啟", "taskkill /f /im explorer.exe & start explorer.exe",
            restart: RestartScope.Explorer, keywords: "explorer,檔案總管,shell,taskbar"),

        Tweak.Cmd("apps.restart-spooler", "Restart Print Spooler", "重新啟動列印多工緩衝處理器",
            "Stop and start the Print Spooler service to fix stuck print jobs.", "停咗再開返列印多工緩衝服務，解決卡住嘅列印工作。",
            "Restart", "重啟", "net stop spooler & net start spooler",
            requiresAdmin: true, keywords: "print,spooler,列印,service"),

        Tweak.Table("apps.top-memory", "Top processes by memory", "記憶體用量最高嘅程序",
            "List the 15 processes using the most memory right now.", "列出而家食記憶體最多嘅 15 個程序。",
            "Show", "顯示",
            "Get-Process | Sort-Object WorkingSet -Descending | Select-Object -First 15 Name,@{n='MB';e={[int]($_.WorkingSet/1MB)}},Id",
            keywords: "memory,記憶體,process,程序,ram"),

        Tweak.Cmd("apps.kill-not-responding", "Kill 'Not Responding' apps", "結束無回應嘅應用程式",
            "Force-close every app that Windows reports as not responding.", "強制關閉所有 Windows 報告為無回應嘅應用程式。",
            "Kill", "結束", "taskkill /F /FI \"STATUS eq NOT RESPONDING\"",
            destructive: true, keywords: "not responding,無回應,hang,kill,當機"),

        Tweak.Table("apps.list-autostart", "List auto-start programs", "列出自動啟動程式",
            "List programs that launch automatically at startup (Run keys).", "列出開機時經 Run 機碼自動啟動嘅程式。",
            "List", "列出",
            "Get-CimInstance Win32_StartupCommand | Select-Object Name,Command,Location",
            keywords: "startup,autostart,自動啟動,run"),

        Tweak.Cmd("apps.analyze-component-store", "Analyze component store", "分析元件存放區",
            "Analyze the WinSxS component store to see if cleanup is recommended.", "分析 WinSxS 元件存放區，睇吓使唔使清理。",
            "Analyze", "分析", "Dism.exe /Online /Cleanup-Image /AnalyzeComponentStore",
            requiresAdmin: true, keywords: "dism,winsxs,component store,元件,清理"),
    };
}