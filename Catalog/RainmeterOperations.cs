using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Rainmeter 一鍵操作目錄 · Catalog of one-shot Rainmeter operations rendered as TweakCards —
/// global bangs (refresh all, manage, about, quit) plus folder shortcuts. Each runs against the
/// shared <see cref="RainmeterService"/> so the live instance is launched on demand. Bilingual.
/// </summary>
public static class RainmeterOperations
{
    /// <summary>
    /// 精選皮膚包 · A small curated list of well-known, freely distributed <c>.rmskin</c> suite pages.
    /// We link to the project/release page (not a hot-linked binary) — the user downloads the .rmskin
    /// there, then installs it from the module via "Install .rmskin". (v1 has no online catalog API.)
    /// </summary>
    public readonly record struct SkinPack(string Name, string En, string Zh, string Url);

    public static readonly IReadOnlyList<SkinPack> CuratedPacks = new List<SkinPack>
    {
        new("illustro", "Bundled minimal suite (clock, CPU, disk, network).",
            "內附簡約套裝（時鐘、CPU、磁碟、網絡）。", "https://docs.rainmeter.net/manual/skins/"),
        new("Mond", "Clean modular system-monitor suite.",
            "簡潔模組化系統監察套裝。", "https://www.deviantart.com/antondeluxe/art/Mond-507130559"),
        new("Win10 Widgets", "Windows-styled widgets (battery, weather, drives, media).",
            "Windows 風格小工具（電池、天氣、磁碟、媒體）。", "https://win10widgets.com/"),
        new("Enigma", "Large all-in-one information suite.",
            "大型一站式資訊套裝。", "https://www.rainmeter.net/discover/"),
        new("Jarvis / Honeycomb", "Sci-fi style monitoring HUDs.",
            "科幻風格監察 HUD。", "https://www.rainmeter.net/discover/"),
        new("Discover more skins", "Browse the official Rainmeter skin discovery hub.",
            "瀏覽官方 Rainmeter 皮膚發掘中心。", "https://www.rainmeter.net/discover/"),
    };

    public static IEnumerable<TweakDefinition> All(RainmeterService svc) => new List<TweakDefinition>
    {
        Tweak.Action("rm.refreshapp", "Refresh all skins", "重新整理全部皮膚",
            "Re-read the Skins folder and reload everything (!RefreshApp).",
            "重新讀取 Skins 資料夾並重載全部（!RefreshApp）。",
            "Refresh", "重新整理", ct => svc.RefreshApp(ct),
            keywords: "refresh reload all 重新整理 重載"),

        Tweak.Action("rm.ensure", "Start Rainmeter", "啟動 Rainmeter",
            "Launch the Rainmeter engine if it isn't already running.",
            "若 Rainmeter 未行就啟動佢。",
            "Start", "啟動", ct => svc.EnsureRunning(ct),
            keywords: "start launch run engine 啟動 執行"),

        Tweak.Action("rm.manage", "Open Manage window", "開啟管理視窗",
            "Open Rainmeter's built-in Manage window (!Manage).",
            "開 Rainmeter 內建嘅管理視窗（!Manage）。",
            "Open", "開啟", ct => svc.OpenManage(ct),
            keywords: "manage window 管理 視窗"),

        Tweak.Action("rm.about", "Open About / Log", "開啟關於／記錄",
            "Open the About window, which also shows the Rainmeter log (!About).",
            "開「關於」視窗，亦會顯示 Rainmeter 記錄（!About）。",
            "Open", "開啟", ct => svc.OpenAbout(ct),
            keywords: "about log 關於 記錄 日誌"),

        Tweak.Action("rm.skinsfolder", "Open Skins folder", "開啟 Skins 資料夾",
            "Open the resolved Skins folder in File Explorer.",
            "喺檔案總管開啟搵到嘅 Skins 資料夾。",
            "Open", "開啟", _ => Task.FromResult(svc.OpenFolder(svc.SkinPath)),
            keywords: "skins folder explorer 資料夾"),

        Tweak.Action("rm.settingsfolder", "Open settings folder", "開啟設定資料夾",
            "Open the folder that holds Rainmeter.ini.",
            "開啟存放 Rainmeter.ini 嘅資料夾。",
            "Open", "開啟", _ => Task.FromResult(svc.OpenFolder(svc.SettingsFolder)),
            keywords: "settings folder rainmeter.ini 設定 資料夾"),

        Tweak.Action("rm.quit", "Quit Rainmeter", "退出 Rainmeter",
            "Quit the running Rainmeter engine (!Quit).",
            "退出行緊嘅 Rainmeter 引擎（!Quit）。",
            "Quit", "退出", ct => svc.Quit(ct), destructive: true,
            keywords: "quit exit close 退出 關閉"),
    };
}
