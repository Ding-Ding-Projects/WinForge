using System;
using System.Collections.Generic;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 效能與電源 · Performance &amp; Power tweaks.
/// </summary>
public static class PerformanceTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.RegChoice("performance.visual-effects", "Visual effects mode", "視覺效果模式",
            "Choose how Windows balances visual effects against performance.", "揀 Windows 喺視覺效果同效能之間點樣平衡。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting",
            RegistryValueKind.DWord,
            new (string en, string zh, object value)[]
            {
                ("Let Windows choose", "由 Windows 決定", 0),
                ("Best appearance", "最佳外觀", 1),
                ("Best performance", "最佳效能", 2),
                ("Custom", "自訂", 3),
            },
            restart: RestartScope.Explorer, keywords: "visual,effects,animation,視覺,效果,動畫"),

        Tweak.RegToggle("performance.fast-startup", "Fast startup", "快速啟動",
            "Use hybrid hibernation to boot the PC faster.", "用混合休眠令部機開機快啲。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled",
            onValue: 1, offValue: 0, requiresAdmin: true, restart: RestartScope.Reboot,
            keywords: "boot,hiberboot,啟動,開機"),

        Tweak.RegToggle("performance.power-throttling-off", "Disable power throttling", "停用電源節流",
            "Stop Windows from throttling background apps to save power.", "唔再為咗慳電而節流背景程式。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff",
            onValue: 1, offValue: 0, requiresAdmin: true,
            keywords: "throttle,power,節流,電源"),

        Tweak.RegToggle("performance.clear-pagefile", "Clear page file at shutdown", "關機時清除分頁檔",
            "Wipe the page file every shutdown for privacy (slower shutdowns).", "每次關機都清除分頁檔以保私隱（關機會慢啲）。",
            RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "ClearPageFileAtShutdown",
            onValue: 1, offValue: 0, requiresAdmin: true, restart: RestartScope.Reboot,
            keywords: "pagefile,shutdown,分頁檔,關機"),

        Tweak.RegChoice("performance.menu-show-delay", "Menu show delay", "選單顯示延遲",
            "How long menus wait before popping open, in milliseconds.", "選單彈出之前要等幾耐（毫秒）。",
            RegRoot.HKCU, @"Control Panel\Desktop", "MenuShowDelay",
            RegistryValueKind.String,
            new (string en, string zh, object value)[]
            {
                ("Instant", "即時", "0"),
                ("Fast", "快", "100"),
                ("Default", "預設", "400"),
            },
            restart: RestartScope.SignOut, keywords: "menu,delay,選單,延遲"),

        Tweak.RegToggle("performance.game-mode", "Game Mode", "遊戲模式",
            "Prioritise system resources for games when one is running.", "開咗遊戲嗰陣優先分配系統資源畀佢。",
            RegRoot.HKCU, @"Software\Microsoft\GameBar", "AutoGameModeEnabled",
            onValue: 1, offValue: 0,
            keywords: "game,gaming,遊戲"),

        Tweak.RegToggle("performance.game-dvr", "Game DVR background recording", "遊戲 DVR 背景錄製",
            "Enable Xbox Game DVR background recording of gameplay.", "開啟 Xbox 遊戲 DVR 喺背景錄製遊戲畫面。",
            RegRoot.HKCU, @"System\GameConfigStore", "GameDVR_Enabled",
            onValue: 1, offValue: 0,
            keywords: "dvr,recording,xbox,錄製,遊戲"),

        Tweak.RegToggle("performance.startup-delay-off", "No startup app delay", "取消啟動程式延遲",
            "Launch startup apps immediately at sign-in without the built-in delay.", "登入嗰陣即刻開啟啟動程式，唔使等內建延遲。",
            RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize", "StartupDelayInMSec",
            onValue: 0, offValue: null, restart: RestartScope.SignOut,
            keywords: "startup,delay,啟動,延遲"),

        Tweak.Cmd("performance.ultimate-plan", "Add Ultimate Performance plan", "新增極致效能電源計劃",
            "Unlock the hidden Ultimate Performance power plan.", "解鎖隱藏咗嘅極致效能電源計劃。",
            "Add plan", "新增計劃",
            "powercfg -duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61",
            requiresAdmin: true, keywords: "powercfg,ultimate,電源,計劃"),

        Tweak.Cmd("performance.high-perf-plan", "Activate High performance plan", "啟用高效能電源計劃",
            "Switch the active power plan to High performance.", "將而家嘅電源計劃切換到高效能。",
            "Activate", "啟用",
            "powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
            requiresAdmin: true, keywords: "powercfg,high,電源,高效能"),

        Tweak.Cmd("performance.balanced-plan", "Activate Balanced plan", "啟用平衡電源計劃",
            "Switch the active power plan back to Balanced.", "將而家嘅電源計劃切返做平衡。",
            "Activate", "啟用",
            "powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e",
            requiresAdmin: true, keywords: "powercfg,balanced,電源,平衡"),

        Tweak.Cmd("performance.hibernate-off", "Turn off hibernation", "熄咗休眠",
            "Disable hibernation and delete hiberfil.sys to free disk space.", "停用休眠並刪除 hiberfil.sys 騰返啲磁碟空間。",
            "Turn off", "關閉",
            "powercfg /hibernate off",
            requiresAdmin: true, keywords: "hibernate,hiberfil,休眠"),

        Tweak.Cmd("performance.hibernate-on", "Turn on hibernation", "開啟休眠",
            "Re-enable hibernation support on this device.", "喺呢部機重新開啟休眠支援。",
            "Turn on", "開啟",
            "powercfg /hibernate on",
            requiresAdmin: true, keywords: "hibernate,休眠"),
    };
}
