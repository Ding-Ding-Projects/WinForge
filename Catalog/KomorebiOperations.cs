using System.Collections.Generic;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Komorebi 操作目錄 · Catalog of komorebic verbs rendered via TweakCard. These are the "fire and forget"
/// daemon operations — toggles, cycles, promote/retile, autostart, config reload, health check. Each runs
/// `komorebic &lt;verb&gt;` through ShellRunner and shows the captured output. Layout switching, workspace focus
/// and padding live in the page's dedicated controls (they need live indices), not here.
/// </summary>
public static class KomorebiOperations
{
    private static TweakDefinition K(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string args, bool destructive = false, string? keywords = null)
        => Tweak.Shell(id, enT, zhT, enD, zhD, enBtn, zhBtn, "komorebic", args,
            destructive: destructive, keywords: keywords);

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // ===== focus mode toggles =====
        K("kb.toggle-tiling", "Toggle tiling", "切換平鋪",
            "Turn tiling on or off for the focused workspace.", "喺目前工作區開／關平鋪。",
            "Toggle", "切換", "toggle-tiling", keywords: "tiling toggle 平鋪 切換"),
        K("kb.toggle-float", "Toggle float", "切換浮動",
            "Float or un-float the focused window (it stops being tiled).", "將目前視窗設為浮動或取消浮動（唔再平鋪）。",
            "Toggle", "切換", "toggle-float", keywords: "float toggle 浮動"),
        K("kb.toggle-monocle", "Toggle monocle", "切換單片鏡（全屏堆疊）",
            "Make the focused container fill the workspace (monocle / fullscreen-stack).", "令目前容器佔滿工作區（單片鏡／全屏堆疊）。",
            "Toggle", "切換", "toggle-monocle", keywords: "monocle fullscreen 單片鏡 全屏"),
        K("kb.toggle-pause", "Toggle pause", "切換暫停",
            "Pause or resume all window tiling globally.", "全域暫停或回復所有視窗平鋪。",
            "Toggle", "切換", "toggle-pause", keywords: "pause resume 暫停"),

        // ===== arrangement =====
        K("kb.promote", "Promote window", "提升視窗",
            "Promote the focused window to the largest tile.", "將目前視窗提升到最大嗰格。",
            "Promote", "提升", "promote", keywords: "promote main 提升 主"),
        K("kb.retile", "Force retile", "強制重新平鋪",
            "Force a retile of all managed windows (fixes a stuck layout).", "強制重新平鋪所有受管理視窗（修正卡住嘅排版）。",
            "Retile", "重排", "retile", keywords: "retile refresh 重排 重新平鋪"),
        K("kb.flip-h", "Flip layout horizontally", "水平翻轉排版",
            "Flip the focused workspace layout horizontally.", "將目前工作區排版水平翻轉。",
            "Flip", "翻轉", "flip-layout horizontal", keywords: "flip horizontal 翻轉 水平"),
        K("kb.flip-v", "Flip layout vertically", "垂直翻轉排版",
            "Flip the focused workspace layout vertically.", "將目前工作區排版垂直翻轉。",
            "Flip", "翻轉", "flip-layout vertical", keywords: "flip vertical 翻轉 垂直"),

        // ===== monitors / focus =====
        K("kb.cycle-monitor-next", "Focus next monitor", "聚焦下一個顯示器",
            "Move focus to the next monitor.", "將焦點移去下一個顯示器。",
            "Next", "下一個", "cycle-monitor next", keywords: "monitor cycle 顯示器"),
        K("kb.cycle-monitor-prev", "Focus previous monitor", "聚焦上一個顯示器",
            "Move focus to the previous monitor.", "將焦點移去上一個顯示器。",
            "Prev", "上一個", "cycle-monitor previous", keywords: "monitor cycle 顯示器"),
        K("kb.cycle-ws-next", "Focus next workspace", "聚焦下一個工作區",
            "Cycle focus to the next workspace.", "聚焦去下一個工作區。",
            "Next", "下一個", "cycle-workspace next", keywords: "workspace cycle 工作區"),
        K("kb.cycle-ws-prev", "Focus previous workspace", "聚焦上一個工作區",
            "Cycle focus to the previous workspace.", "聚焦去上一個工作區。",
            "Prev", "上一個", "cycle-workspace previous", keywords: "workspace cycle 工作區"),

        // ===== mouse follows focus =====
        K("kb.mff-on", "Mouse follows focus: on", "滑鼠跟隨焦點：開",
            "Move the cursor to the focused window automatically.", "自動將游標移去聚焦視窗。",
            "Enable", "開啟", "mouse-follows-focus enable", keywords: "mouse follows focus 滑鼠 焦點"),
        K("kb.mff-off", "Mouse follows focus: off", "滑鼠跟隨焦點：關",
            "Stop moving the cursor to the focused window.", "停止自動移游標。",
            "Disable", "關閉", "mouse-follows-focus disable", keywords: "mouse follows focus 滑鼠 焦點"),

        // ===== session float rules =====
        K("kb.session-float", "Float focused (this session)", "本次浮動聚焦視窗",
            "Add a session float rule for the focused window.", "為目前視窗加一條本次浮動規則。",
            "Float", "浮動", "session-float-rule", keywords: "float session 浮動"),
        K("kb.clear-session-float", "Clear session float rules", "清除本次浮動規則",
            "Remove all session float rules.", "移除所有本次浮動規則。",
            "Clear", "清除", "clear-session-float-rules", destructive: true, keywords: "clear float session 清除 浮動"),

        // ===== config / autostart / health =====
        K("kb.reload", "Reload configuration", "重新載入設定",
            "Reload legacy komorebi.ahk / komorebi.ps1 configuration.", "重新載入舊式 komorebi.ahk／komorebi.ps1 設定。",
            "Reload", "重新載入", "reload-configuration", keywords: "reload config 重新載入 設定"),
        K("kb.check", "Check configuration", "檢查設定",
            "Run komorebic's configuration health check.", "執行 komorebic 設定健康檢查。",
            "Check", "檢查", "check", keywords: "check health doctor 檢查"),
        K("kb.quickstart", "Quickstart (gather examples)", "快速開始（取得範例）",
            "Gather example configuration files for a new-user quickstart.", "為新用戶收集範例設定檔。",
            "Run", "執行", "quickstart", keywords: "quickstart example config 範例 設定"),
        K("kb.enable-autostart", "Enable autostart", "啟用開機自啟",
            "Create a startup shortcut so komorebi launches at logon.", "建立啟動捷徑，令 komorebi 喺登入時自動啟動。",
            "Enable", "啟用", "enable-autostart", keywords: "autostart startup 開機 自啟"),
        K("kb.enable-autostart-bar", "Enable autostart (with bar)", "啟用開機自啟（連狀態列）",
            "Autostart komorebi together with komorebi-bar at logon.", "登入時連同 komorebi-bar 一齊自動啟動。",
            "Enable", "啟用", "enable-autostart --bar", keywords: "autostart bar startup 開機 自啟 狀態列"),
        K("kb.disable-autostart", "Disable autostart", "停用開機自啟",
            "Remove the komorebi startup shortcut.", "移除 komorebi 啟動捷徑。",
            "Disable", "停用", "disable-autostart", destructive: true, keywords: "autostart startup 開機 自啟"),
    };
}
