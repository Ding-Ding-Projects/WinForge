using System.Collections.Generic;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// FancyZones 操作目錄 · Catalog of FancyZones behaviour toggles, expressed as data-driven
/// <see cref="TweakDefinition"/>s and rendered by <c>Controls/TweakCard</c>. 每個開關直接讀寫
/// PowerToys 嘅 FancyZones settings.json 對應屬性（包成 {"value":...}），改完通知 PowerToys 重讀。
/// Each toggle reads/writes the matching property in the FancyZones settings.json and nudges PowerToys
/// to reload. All read-throughs are defensive (null → treated as the PowerToys default).
/// </summary>
public static class FancyZonesOperations
{
    /// <summary>
    /// 由 FancyZones settings.json 一個布林屬性支援嘅開關 · A toggle backed by one FancyZones boolean property.
    /// </summary>
    private static TweakDefinition Toggle(
        string id, string prop, bool def,
        string enT, string zhT, string enD, string zhD, string? keywords = null)
        => Tweak.CustomToggle(
            id, enT, zhT, enD, zhD,
            getIsOn: () => FancyZonesService.GetBoolProperty(prop) ?? def,
            setIsOn: on => FancyZonesService.SetBoolProperty(prop, on),
            keywords: keywords);

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // ===== 拖曳與貼齊 · Dragging & snapping =====
        Toggle("fz.shiftDrag", "fancyzones_shiftDrag", true,
            "Hold Shift to snap while dragging", "拖曳時要按住 Shift 先貼齊",
            "When on, hold Shift while dragging a window to show zones and snap. When off, dragging snaps automatically and Shift temporarily disables it.",
            "開咗：拖窗時按住 Shift 先會顯示分區並貼齊。關咗：拖窗自動貼齊，按 Shift 暫時停用。",
            keywords: "shift drag snap 貼齊 拖曳"),
        Toggle("fz.mouseSwitch", "fancyzones_mouseSwitch", false,
            "Use a non-primary mouse button to toggle zones", "用副滑鼠鍵切換分區",
            "While dragging, click a non-primary mouse button to toggle the zone activation.",
            "拖曳時撳副滑鼠鍵切換分區啟用。",
            keywords: "mouse switch 滑鼠"),
        Toggle("fz.middleClickSpan", "fancyzones_mouseMiddleClickSpanningMultipleZones", false,
            "Middle-click to span multiple zones", "中鍵跨多個分區",
            "While dragging, middle-click to make the window span several zones at once.",
            "拖曳時撳中鍵，令窗一次跨幾個分區。",
            keywords: "middle click span 中鍵 跨"),
        Toggle("fz.allowChildSnap", "fancyzones_allowChildWindowSnap", false,
            "Allow child windows to snap", "允許子視窗貼齊",
            "Let child windows (not just top-level windows) snap into zones.",
            "連子視窗（唔淨係頂層窗）都可以貼入分區。",
            keywords: "child window snap 子視窗"),

        // ===== 鍵盤 · Keyboard =====
        Toggle("fz.overrideSnapHotkeys", "fancyzones_overrideSnapHotkeys", false,
            "Override Windows Snap hotkeys (Win+Arrow / Win+Ctrl+Arrow)", "覆寫 Windows 貼齊熱鍵（Win+方向／Win+Ctrl+方向）",
            "Let FancyZones take over the Win+Arrow snap hotkeys so they move windows between zones (Win+Ctrl+Arrow by default).",
            "畀 FancyZones 接管 Win+方向 熱鍵，令佢哋喺分區之間移動窗（預設 Win+Ctrl+方向）。",
            keywords: "hotkey override win arrow 熱鍵 方向鍵 移動"),
        Toggle("fz.moveBasedOnPosition", "fancyzones_moveWindowsBasedOnPosition", false,
            "Move between zones by relative position", "按相對位置喺分區間移動",
            "When moving windows with the keyboard, choose the next zone by its relative position rather than zone index.",
            "用鍵盤移動窗時，按相對位置揀下一個分區，而唔係按分區編號。",
            keywords: "position move 位置 移動"),
        Toggle("fz.windowSwitching", "fancyzones_windowSwitching", false,
            "Switch between windows in the same zone", "喺同一分區內切換視窗",
            "Enable a hotkey to cycle between windows snapped to the same zone.",
            "啟用熱鍵，喺貼入同一分區嘅窗之間循環切換。",
            keywords: "switch windows tab 切換"),

        // ===== 多顯示器 · Multiple monitors =====
        Toggle("fz.moveAcrossMonitors", "fancyzones_moveWindowAcrossMonitors", false,
            "Move windows across monitors", "跨顯示器移動視窗",
            "Allow moving snapped windows across all connected monitors.",
            "允許將已貼齊嘅窗跨所有顯示器移動。",
            keywords: "monitor move 顯示器 跨"),
        Toggle("fz.spanAcrossMonitors", "fancyzones_span_zones_across_monitors", false,
            "Span zones across monitors", "分區跨顯示器",
            "Treat all monitors as one continuous surface so a single zone can span them.",
            "將所有顯示器當成一塊連續畫面，令一個分區可以跨越。",
            keywords: "span monitor 跨 顯示器"),
        Toggle("fz.showOnAllMonitors", "fancyzones_show_on_all_monitors", false,
            "Show zones on all monitors while dragging", "拖曳時喺所有顯示器顯示分區",
            "Show the zone overlay on every monitor during a drag, not just the active one.",
            "拖曳時喺每個顯示器都顯示分區覆蓋層，唔淨係作用中嗰個。",
            keywords: "all monitors show 所有 顯示器"),
        Toggle("fz.openOnActiveMonitor", "fancyzones_openWindowOnActiveMonitor", false,
            "Open new windows on the active monitor", "喺作用中顯示器開新窗",
            "Place newly opened windows on the monitor where the cursor currently is.",
            "將新開嘅窗放喺游標所在嘅顯示器。",
            keywords: "active monitor open 作用中 顯示器"),

        // ===== 行為 · Behaviour =====
        Toggle("fz.restoreSize", "fancyzones_restoreSize", false,
            "Restore original size when unsnapping", "取消貼齊時還原原本大小",
            "Restore a window's original size when it is dragged out of a zone.",
            "將窗拖出分區時還原返佢原本嘅大小。",
            keywords: "restore size 還原 大小"),
        Toggle("fz.appLastZone", "fancyzones_appLastZone_moveWindows", false,
            "Move newly created windows to their last zone", "新窗回到上次嘅分區",
            "Remember each app's last zone and move its new windows there automatically.",
            "記住每個程式上次嘅分區，自動將佢新開嘅窗放返去。",
            keywords: "last zone app 上次 分區"),
        Toggle("fz.zoneSetChangeMove", "fancyzones_zoneSetChange_moveWindows", false,
            "Keep windows in zones when the layout changes", "切換版面時保持窗喺分區",
            "When you switch layouts, move existing snapped windows into the new zones.",
            "切換版面時，將已貼齊嘅窗移入新分區。",
            keywords: "layout change move 版面 切換"),
        Toggle("fz.displayChangeMove", "fancyzones_displayOrWorkAreaChange_moveWindows", false,
            "Keep windows in zones when displays change", "顯示器改變時保持窗喺分區",
            "Re-snap windows when monitors are connected/disconnected or the work area changes.",
            "接駁／拔走顯示器或工作區改變時，重新貼齊啲窗。",
            keywords: "display change move 顯示 改變"),
        Toggle("fz.quickSwitch", "fancyzones_quickLayoutSwitch", true,
            "Enable quick layout switch (Ctrl+Win+Alt+Number)", "啟用快速版面切換（Ctrl+Win+Alt+數字）",
            "Assign number hotkeys to layouts for instant switching.",
            "畀版面綁定數字熱鍵，即時切換。",
            keywords: "quick switch number 快速 數字"),
        Toggle("fz.flashOnSwitch", "fancyzones_flashZonesOnQuickSwitch", true,
            "Flash zones on quick switch", "快速切換時閃動分區",
            "Briefly flash the zones when you quick-switch a layout.",
            "快速切換版面時，短暫閃一閃分區。",
            keywords: "flash quick 閃 快速"),

        // ===== 外觀 · Appearance =====
        Toggle("fz.systemTheme", "fancyzones_systemTheme", true,
            "Use the system theme for zone colours", "分區顏色跟系統主題",
            "Match the zone overlay colours to the Windows light/dark theme.",
            "令分區覆蓋層顏色跟 Windows 淺色／深色主題。",
            keywords: "theme colour 主題 顏色"),
        Toggle("fz.showZoneNumber", "fancyzones_showZoneNumber", true,
            "Show zone numbers", "顯示分區編號",
            "Show a number on each zone in the overlay.",
            "喺覆蓋層每個分區顯示一個編號。",
            keywords: "number zone 編號 分區"),
        Toggle("fz.makeTransparent", "fancyzones_makeDraggedWindowTransparent", true,
            "Make the dragged window transparent", "拖曳中嘅窗變透明",
            "Make the window you are dragging semi-transparent so you can see the zones underneath.",
            "拖曳緊嘅窗變半透明，令你睇到底下嘅分區。",
            keywords: "transparent drag 透明 拖曳"),
        Toggle("fz.disableRoundCorners", "fancyzones_disableRoundCornersOnSnap", false,
            "Disable rounded corners on snapped windows", "貼齊窗停用圓角",
            "Turn off Windows 11 rounded corners for windows snapped into a zone.",
            "為貼入分區嘅窗關閉 Windows 11 圓角。",
            keywords: "rounded corners 圓角"),
    };
}
