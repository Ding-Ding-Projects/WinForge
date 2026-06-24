using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 精選 Windhawk mod 目錄（資料驅動）· A curated, bilingual catalog of popular Windhawk mods.
///
/// 每個 mod 渲染成一張 <see cref="WinForge.Controls.TweakCard"/>，按鈕「喺 Windhawk 開」會深層連結到
/// windhawk.net 上嗰個 mod 嘅頁面（喺 Windhawk 內可一鍵安裝／設定）。
/// Each mod renders as a TweakCard whose "Open in Windhawk" button deep-links to that mod's page on
/// windhawk.net (where the user installs/configures it inside Windhawk). The list is data only so it is
/// trivial to update as the upstream catalog evolves.
/// </summary>
public static class WindhawkMods
{
    /// <summary>一個精選 mod 嘅資料 · One curated mod entry.</summary>
    public sealed record ModEntry(
        string Id,            // windhawk.net mod id (URL slug)
        string EnTitle, string ZhTitle,
        string EnDesc, string ZhDesc,
        string Author,
        string Keywords);

    /// <summary>~14 個熱門 mod · ~14 popular mods grouped by what they change.</summary>
    public static readonly List<ModEntry> Catalog = new()
    {
        new("taskbar-icon-size",
            "Taskbar height and icon size", "工作列高度同圖示大小",
            "Make the Windows 11 taskbar shorter or taller and resize its icons — the most popular taskbar mod.",
            "調校 Windows 11 工作列高度，並重新設定圖示大小 — 最受歡迎嘅工作列 mod。",
            "m417z", "taskbar height icon size 工作列 高度 圖示"),

        new("taskbar-clock-customization",
            "Taskbar Clock Customization", "工作列時鐘自訂",
            "Add seconds, the date, week number, custom text or even weather to the system-tray clock.",
            "喺系統匣時鐘加上秒數、日期、週次、自訂文字甚至天氣。",
            "m417z", "clock seconds date weather tray 時鐘 秒 日期 天氣"),

        new("taskbar-grouping",
            "Disable grouping on the taskbar", "停用工作列群組",
            "Stop Windows from combining windows of the same app into a single taskbar button.",
            "唔再將同一個程式嘅視窗併埋成一粒工作列按鈕。",
            "m417z", "taskbar grouping ungroup labels 工作列 群組 標籤"),

        new("windows-11-start-menu-styler",
            "Windows 11 Start Menu Styler", "Windows 11 開始功能表美化",
            "Deeply restyle the Start menu with community themes — hide the recommended section, change layout, and more.",
            "用社群主題深度美化開始功能表 — 隱藏推薦區、改版面等等。",
            "m417z", "start menu styler theme recommended 開始 功能表 主題 推薦"),

        new("windows-11-taskbar-styler",
            "Windows 11 Taskbar Styler", "Windows 11 工作列美化",
            "Restyle the taskbar with community themes (translucent, segmented, classic-like, and many more).",
            "用社群主題美化工作列（半透明、分段、近似經典等多款）。",
            "m417z", "taskbar styler theme translucent 工作列 美化 主題 半透明"),

        new("classic-taskbar-background-fixed",
            "Classic Taskbar (background fix)", "經典工作列（背景修正）",
            "Bring back a more classic, opaque taskbar look on Windows 11.",
            "喺 Windows 11 帶返較經典、不透明嘅工作列外觀。",
            "ujk", "classic taskbar opaque background 經典 工作列 不透明"),

        new("taskbar-on-top",
            "Taskbar position on screen", "工作列螢幕位置",
            "Move the Windows 11 taskbar to the top, left or right edge of the screen.",
            "將 Windows 11 工作列移到螢幕頂部、左邊或右邊。",
            "m417z", "taskbar top left right position 工作列 頂部 位置"),

        new("aerexplorer",
            "Aerexplorer (classic Explorer tweaks)", "Aerexplorer（經典檔案總管調校）",
            "A bundle of File Explorer tweaks: classic search box, ribbon, details pane and more Aero-era behaviour.",
            "一系列檔案總管調校：經典搜尋框、功能區、詳細資料窗格等 Aero 年代行為。",
            "Anixx", "explorer aero ribbon search 檔案總管 經典 功能區"),

        new("better-file-sizes-in-explorer-details",
            "Better file sizes in Explorer", "檔案總管更佳檔案大小",
            "Show file sizes for folders and use MB/GB units consistently in the Explorer details view.",
            "喺檔案總管詳細資料檢視顯示資料夾大小，並一致使用 MB／GB 單位。",
            "Waldemar", "explorer file size folder mb gb 檔案 大小 資料夾"),

        new("disable-rounded-corners",
            "Disable rounded corners", "停用圓角",
            "Turn off the Windows 11 rounded window corners to get sharp, square edges back.",
            "關閉 Windows 11 圓角視窗，帶返尖角方正邊緣。",
            "m417z", "rounded corners square sharp window 圓角 方角 視窗"),

        new("aero-tray",
            "Aero Tray", "Aero 系統匣",
            "Restore Aero-style behaviour to the notification area / system tray.",
            "為通知區／系統匣帶返 Aero 風格行為。",
            "Anixx", "aero tray notification area 系統匣 通知區"),

        new("start-menu-all-apps",
            "Open Start menu on 'All apps'", "開始功能表直接顯示「所有應用程式」",
            "Make the Start menu open straight to the All apps list instead of the pinned/recommended page.",
            "令開始功能表一開就顯示「所有應用程式」清單，唔再停喺釘選／推薦頁。",
            "m417z", "start menu all apps pinned 開始 所有應用程式 釘選"),

        new("middle-click-to-close",
            "Middle click to close on the taskbar", "中鍵點擊關閉工作列項目",
            "Close a taskbar window with a middle mouse click — like a browser tab.",
            "用滑鼠中鍵一㩒就關閉工作列視窗 — 似瀏覽器分頁咁。",
            "m417z", "middle click close taskbar tab 中鍵 關閉 工作列"),

        new("acrylic-effect-radius-changer",
            "Acrylic / blur effect tuner", "壓克力／模糊效果調校",
            "Tune the acrylic blur radius and effects used across the Windows 11 UI.",
            "調校 Windows 11 介面所用嘅壓克力模糊半徑同效果。",
            "m417z", "acrylic blur radius effect transparency 壓克力 模糊 半透明"),
    };

    /// <summary>
    /// 將精選目錄轉成 TweakDefinition（每個都係一個「喺 Windhawk 開」嘅深層連結動作）·
    /// Build the curated catalog as TweakDefinitions — each is an "Open in Windhawk" deep-link action.
    /// </summary>
    public static List<TweakDefinition> All()
    {
        var list = new List<TweakDefinition>(Catalog.Count);
        foreach (var m in Catalog)
        {
            string id = m.Id; // capture
            list.Add(Tweak.Action(
                id: $"windhawk.mod.{id}",
                enT: m.EnTitle, zhT: m.ZhTitle,
                enD: $"{m.EnDesc}  ·  by {m.Author}",
                zhD: $"{m.ZhDesc}  ·  作者：{m.Author}",
                enBtn: "Open in Windhawk", zhBtn: "喺 Windhawk 開",
                run: _ => Task.FromResult(WindhawkService.OpenModPage(id)),
                keywords: m.Keywords + " windhawk mod"));
        }
        return list;
    }
}
