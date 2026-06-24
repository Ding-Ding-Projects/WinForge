using System.Collections.Generic;

namespace WinForge.Services;

/// <summary>一對雙語名 · A bilingual name pair.</summary>
public readonly record struct ViveName(string En, string Zh);

/// <summary>
/// 內建嘅功能旗標名字字典（社群整理）· Bundled community feature-name dictionary.
///
/// 重要：呢度嘅 ID 只係「名字提示」，唔係事實來源。真正嘅事實來源係本機 Feature Store（ViVeTool /query）。
/// IMPORTANT: the ids here are only LABEL HINTS — the source of truth is the live Feature Store (/query).
/// The UI always resolves and shows the numeric id from the running build before applying anything, and the
/// named toggles below carry candidate id groups whose membership varies by build. Nothing is ever applied
/// purely from a hard-coded value: the user sees the resolved id first.
/// </summary>
public static class ViveDictionary
{
    /// <summary>ID → 人類可讀名（畀 /query 行配對標籤）· id → friendly name, used to label /query rows.</summary>
    public static readonly IReadOnlyDictionary<uint, ViveName> ById = new Dictionary<uint, ViveName>
    {
        // File Explorer
        [37634385] = new("File Explorer tabs", "檔案總管分頁"),
        [39145991] = new("File Explorer tab bar", "檔案總管分頁列"),
        [36354489] = new("File Explorer duplicate tab", "檔案總管複製分頁"),
        [40729001] = new("File Explorer modern address bar", "檔案總管新版位址列"),
        [41040327] = new("AI actions in File Explorer", "檔案總管 AI 操作"),
        [49402389] = new("Click to Do", "Click to Do（隨點即做）"),

        // Start menu
        [42395152] = new("New Start menu redesign", "新版開始功能表"),
        [47205210] = new("New Start menu (category grid)", "新開始功能表（分類格）"),
        [49221331] = new("Start menu Phone Link panel", "開始功能表 Phone Link 面板"),

        // Context menu / command bar
        [34230003] = new("Modern context menu", "新版右鍵選單"),
        [29785184] = new("Command bar surfaces", "命令列介面"),

        // Taskbar / clock
        [37389010] = new("Taskbar 'End Task'", "工作列「結束工作」"),
        [45531387] = new("Seconds in system clock", "系統時鐘顯示秒"),

        // Snap
        [26008830] = new("Updated Snap Layouts", "新版貼齊版面"),
        [38764045] = new("Suggested snap groups", "建議貼齊群組"),

        // Power
        [42105254] = new("Energy Saver (desktop)", "節能模式（桌機）"),
    };

    /// <summary>
    /// 有名嘅切換 · The named toggles surfaced as one-click buttons. Each carries a candidate id group;
    /// the actual members present on the running build are resolved at runtime via /query.
    /// </summary>
    public static readonly IReadOnlyList<ViveNamedToggle> NamedToggles = new List<ViveNamedToggle>
    {
        new()
        {
            Key = "explorer-tabs", En = "File Explorer tabs", Zh = "檔案總管分頁",
            En2 = "Tab bar + duplicate tab", Zh2 = "分頁列 + 複製分頁",
            Ids = new uint[] { 37634385, 39145991, 36354489 }, ShellOnly = true,
        },
        new()
        {
            Key = "new-start", En = "New Start menu", Zh = "新版開始功能表",
            En2 = "Scrollable surface + category grid + Phone Link", Zh2 = "可捲版面 + 分類格 + Phone Link",
            Ids = new uint[] { 42395152, 47205210, 49221331 }, ShellOnly = true,
        },
        new()
        {
            Key = "modern-context", En = "Modern context menus", Zh = "新版右鍵選單",
            Ids = new uint[] { 34230003, 29785184 }, ShellOnly = true,
        },
        new()
        {
            Key = "clock-seconds", En = "Seconds in clock", Zh = "時鐘顯示秒",
            Ids = new uint[] { 45531387 }, ShellOnly = false,
        },
        new()
        {
            Key = "snap-layouts", En = "Snap Layouts (updated)", Zh = "新版貼齊版面",
            Ids = new uint[] { 26008830, 38764045 }, ShellOnly = false,
        },
        new()
        {
            Key = "energy-saver", En = "Energy Saver", Zh = "節能模式",
            Ids = new uint[] { 42105254 }, ShellOnly = false,
        },
        new()
        {
            Key = "end-task", En = "Taskbar 'End Task'", Zh = "工作列「結束工作」",
            Ids = new uint[] { 37389010 }, ShellOnly = true,
        },
        new()
        {
            Key = "click-to-do", En = "Click to Do / AI actions", Zh = "Click to Do／AI 操作",
            En2 = "Some surfaces are server-gated", Zh2 = "部分介面受伺服器控制",
            Ids = new uint[] { 49402389, 41040327 }, ShellOnly = false,
        },
    };
}
