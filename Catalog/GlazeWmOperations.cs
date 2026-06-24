using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// GlazeWM 操作目錄 · Catalog of GlazeWM CLI operations rendered as TweakCards.
/// 全部經 glazewm CLI（command / query）執行並擷取輸出 · all run through the glazewm CLI and capture output.
/// </summary>
public static class GlazeWmOperations
{
    private static TweakDefinition Cmd(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string wmCommand, bool destructive = false, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => GlazeWmService.Command(wmCommand, ct), destructive: destructive, keywords: keywords);

    private static TweakDefinition Q(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string query, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => GlazeWmService.Query(query, ct), keywords: keywords);

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // ===== lifecycle =====
        Cmd("glaze.reload", "Reload config", "重新載入設定",
            "Re-evaluate config.yaml without restarting the daemon (wm-reload-config).",
            "唔使重啟 daemon 就重新讀取 config.yaml（wm-reload-config）。",
            "Reload", "重載", "wm-reload-config", keywords: "reload config 重載 設定"),
        Cmd("glaze.exit", "Exit GlazeWM", "退出 GlazeWM",
            "Cleanly stop the window manager and restore all managed windows (wm-exit).",
            "乾淨咁停止視窗管理員並還原所有受管理嘅視窗（wm-exit）。",
            "Exit", "退出", "wm-exit", destructive: true, keywords: "exit quit stop 退出 停止"),
        Cmd("glaze.redraw", "Redraw windows", "重畫視窗",
            "Force a redraw of all managed windows (wm-redraw).",
            "強制重畫所有受管理嘅視窗（wm-redraw）。",
            "Redraw", "重畫", "wm-redraw", keywords: "redraw refresh 重畫 重新整理"),
        Cmd("glaze.pause", "Toggle pause", "切換暫停",
            "Pause/resume window management and all keybindings (wm-toggle-pause).",
            "暫停／恢復視窗管理同所有鍵盤綁定（wm-toggle-pause）。",
            "Toggle", "切換", "wm-toggle-pause", keywords: "pause toggle 暫停"),
        Cmd("glaze.cycle-focus", "Cycle focus", "循環聚焦",
            "Cycle focus between tiling, floating and fullscreen windows (wm-cycle-focus).",
            "喺平鋪、浮動同全螢幕視窗之間循環聚焦（wm-cycle-focus）。",
            "Cycle", "循環", "wm-cycle-focus", keywords: "cycle focus 聚焦 循環"),

        // ===== focus / move =====
        Cmd("glaze.focus-left", "Focus left", "聚焦左",
            "Shift focus to the window on the left.", "將焦點移去左邊嘅視窗。",
            "Focus", "聚焦", "focus --direction left", keywords: "focus left 聚焦 左"),
        Cmd("glaze.focus-right", "Focus right", "聚焦右",
            "Shift focus to the window on the right.", "將焦點移去右邊嘅視窗。",
            "Focus", "聚焦", "focus --direction right", keywords: "focus right 聚焦 右"),
        Cmd("glaze.move-left", "Move window left", "視窗向左移",
            "Move the focused window left.", "將聚焦視窗向左移。",
            "Move", "移動", "move --direction left", keywords: "move left 移動 左"),
        Cmd("glaze.move-right", "Move window right", "視窗向右移",
            "Move the focused window right.", "將聚焦視窗向右移。",
            "Move", "移動", "move --direction right", keywords: "move right 移動 右"),

        // ===== state toggles =====
        Cmd("glaze.toggle-floating", "Toggle floating", "切換浮動",
            "Toggle the focused window between tiling and floating (centered).",
            "將聚焦視窗喺平鋪同浮動（置中）之間切換。",
            "Toggle", "切換", "toggle-floating --centered", keywords: "floating tiling 浮動 平鋪"),
        Cmd("glaze.toggle-fullscreen", "Toggle fullscreen", "切換全螢幕",
            "Toggle fullscreen for the focused window.", "切換聚焦視窗嘅全螢幕。",
            "Toggle", "切換", "toggle-fullscreen", keywords: "fullscreen 全螢幕"),
        Cmd("glaze.toggle-tiling-dir", "Toggle tiling direction", "切換平鋪方向",
            "Change where new tiling windows are inserted (toggle-tiling-direction).",
            "改變新平鋪視窗插入嘅位置（toggle-tiling-direction）。",
            "Toggle", "切換", "toggle-tiling-direction", keywords: "tiling direction 平鋪 方向"),
        Cmd("glaze.close", "Close window", "關閉視窗",
            "Close the focused window.", "關閉聚焦視窗。",
            "Close", "關閉", "close", destructive: true, keywords: "close 關閉"),

        // ===== queries =====
        Q("glaze.q-windows", "Query windows", "查詢視窗",
            "List all windows managed by GlazeWM (JSON).", "列出 GlazeWM 管理嘅所有視窗（JSON）。",
            "Query", "查詢", "windows", keywords: "query windows 查詢 視窗"),
        Q("glaze.q-workspaces", "Query workspaces", "查詢工作區",
            "List all workspaces (JSON).", "列出所有工作區（JSON）。",
            "Query", "查詢", "workspaces", keywords: "query workspaces 查詢 工作區"),
        Q("glaze.q-monitors", "Query monitors", "查詢顯示器",
            "List all monitors (JSON).", "列出所有顯示器（JSON）。",
            "Query", "查詢", "monitors", keywords: "query monitors 查詢 顯示器"),
        Q("glaze.q-focused", "Query focused", "查詢聚焦",
            "Show the currently focused container (JSON).", "顯示目前聚焦嘅容器（JSON）。",
            "Query", "查詢", "focused", keywords: "query focused 查詢 聚焦"),
        Q("glaze.q-bindingmodes", "Query binding modes", "查詢綁定模式",
            "Show the active binding modes (JSON).", "顯示目前生效嘅綁定模式（JSON）。",
            "Query", "查詢", "binding-modes", keywords: "query binding modes 查詢 綁定 模式"),
    };
}
