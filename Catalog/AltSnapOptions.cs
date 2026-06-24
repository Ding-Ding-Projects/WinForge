using System.Collections.Generic;

namespace WinForge.Catalog;

/// <summary>選項類型 · The kind of editor a curated AltSnap.ini option renders as.</summary>
public enum AltSnapOptionKind
{
    /// <summary>開／關（寫 1／0）· On/off, written as 1/0.</summary>
    Toggle,
    /// <summary>多選一（寫指定字串值）· Pick one of a fixed set of string values.</summary>
    Choice,
    /// <summary>整數（NumberBox）· An integer value edited via a NumberBox.</summary>
    Number,
    /// <summary>自由文字（例如黑名單清單）· Free text, e.g. a comma-separated blacklist.</summary>
    Text,
}

/// <summary>多選一其中一個值 · One labelled value for a Choice option.</summary>
public sealed record AltSnapChoice(string En, string Zh, string Value);

/// <summary>
/// 一個受策劃嘅 AltSnap.ini 設定 · One curated AltSnap.ini setting, described as data
/// (section/key + bilingual text + editor metadata). The page turns each into a real control.
/// </summary>
public sealed class AltSnapOption
{
    public string Id { get; init; } = "";
    public string Section { get; init; } = "";
    public string Key { get; init; } = "";
    public AltSnapOptionKind Kind { get; init; }
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";
    public string EnDesc { get; init; } = "";
    public string ZhDesc { get; init; } = "";
    /// <summary>預設值（檔案未設時顯示）· The default AltSnap uses when the key is absent.</summary>
    public string Default { get; init; } = "";
    public IReadOnlyList<AltSnapChoice> Choices { get; init; } = new List<AltSnapChoice>();
    public int Min { get; init; }
    public int Max { get; init; } = 9999;
}

/// <summary>
/// 受策劃嘅 AltSnap.ini 設定目錄 · The curated AltSnap.ini key catalog, grouped logically.
/// 全部係資料；行為（讀寫 ini、重啟）喺 Pages/AltSnapModule + Services/AltSnapService。
/// Pure data; behaviour (ini read/write, restart) lives in the page + service. Keys/values mirror the
/// upstream AltSnap config schema (see external/altsnap AltSnap.txt / config.c).
/// </summary>
public static class AltSnapOptions
{
    /// <summary>修飾鍵（[Input] Hotkeys）嘅值對照 · The modifier-key values for [Input] Hotkeys (virtual-key codes).</summary>
    public static readonly IReadOnlyList<AltSnapChoice> ModifierKeys = new List<AltSnapChoice>
    {
        new("Alt (both)", "Alt（左右）", "A4 A5"),
        new("Left Alt", "左 Alt", "A4"),
        new("Right Alt", "右 Alt", "A5"),
        new("Win (both)", "Win（左右）", "5B 5C"),
        new("Ctrl (both)", "Ctrl（左右）", "A2 A3"),
        new("Shift (both)", "Shift（左右）", "A0 A1"),
    };

    /// <summary>MoveUp／ResizeUp 鬆手動作 · Actions performed when the modifier is released after a move/resize.</summary>
    private static readonly IReadOnlyList<AltSnapChoice> UpActions = new List<AltSnapChoice>
    {
        new("Nothing", "唔做", "Nothing"),
        new("Toggle maximize", "切換最大化", "Maximize"),
        new("Minimize", "最小化", "Minimize"),
        new("Center", "置中", "Center"),
        new("Always on top", "永遠置頂", "AlwaysOnTop"),
        new("Close", "關閉", "Close"),
        new("Lower (send to back)", "送到最底", "Lower"),
    };

    public static readonly IReadOnlyList<AltSnapOption> All = new List<AltSnapOption>
    {
        // ---- Input / modifier ----
        new()
        {
            Id = "hotkeys", Section = "Input", Key = "Hotkeys", Kind = AltSnapOptionKind.Choice,
            En = "Modifier key", Zh = "修飾鍵",
            EnDesc = "Hold this key, then drag anywhere in a window to move it (Linux-style alt-drag).",
            ZhDesc = "撳住呢個鍵，就可以喺視窗任何位置拖動嚟移動（Linux 式 alt 拖曳）。",
            Default = "A4 A5", Choices = ModifierKeys,
        },
        new()
        {
            Id = "moveup", Section = "Input", Key = "MoveUp", Kind = AltSnapOptionKind.Choice,
            En = "Release action (after move)", Zh = "鬆手動作（移動後）",
            EnDesc = "What happens when you let go of the modifier with the left button still down after a move.",
            ZhDesc = "移動後左鍵仲撳住、鬆開修飾鍵時做嘅動作。",
            Default = "Nothing", Choices = UpActions,
        },
        new()
        {
            Id = "resizeup", Section = "Input", Key = "ResizeUp", Kind = AltSnapOptionKind.Choice,
            En = "Release action (after resize)", Zh = "鬆手動作（縮放後）",
            EnDesc = "What happens when you let go of the modifier with the right button still down after a resize.",
            ZhDesc = "縮放後右鍵仲撳住、鬆開修飾鍵時做嘅動作。",
            Default = "Nothing", Choices = UpActions,
        },
        new()
        {
            Id = "rbactn", Section = "Input", Key = "GrabWithAlt", Kind = AltSnapOptionKind.Choice,
            En = "Left-button action", Zh = "左鍵動作",
            EnDesc = "What the modifier + left mouse button does (Move is the classic behaviour).",
            ZhDesc = "修飾鍵 + 滑鼠左鍵嘅動作（移動係經典行為）。",
            Default = "Move",
            Choices = new List<AltSnapChoice>
            {
                new("Move", "移動", "Move"),
                new("Resize", "縮放", "Resize"),
                new("Nothing", "唔做", "Nothing"),
            },
        },

        // ---- General behaviour ----
        new()
        {
            Id = "autosnap", Section = "General", Key = "AutoSnap", Kind = AltSnapOptionKind.Choice,
            En = "Auto-snap to edges", Zh = "自動貼邊",
            EnDesc = "Snap windows to screen edges / other windows while dragging.",
            ZhDesc = "拖動時自動貼齊螢幕邊緣或其他視窗。",
            Default = "0",
            Choices = new List<AltSnapChoice>
            {
                new("Off", "關", "0"),
                new("Outer edges", "外邊緣", "1"),
                new("Outer + inner edges", "外 + 內邊緣", "2"),
                new("Outer + inner + windows", "外 + 內 + 視窗", "3"),
            },
        },
        new()
        {
            Id = "aerotopmax", Section = "Advanced", Key = "AeroTopMaximizes", Kind = AltSnapOptionKind.Toggle,
            En = "Drag to top maximizes", Zh = "拖到頂部最大化",
            EnDesc = "Dragging a window to the top edge of the screen maximizes it (Aero Snap style).",
            ZhDesc = "將視窗拖到螢幕頂邊就最大化（Aero Snap 風格）。",
            Default = "1",
        },
        new()
        {
            Id = "snapthreshold", Section = "Advanced", Key = "SnapThreshold", Kind = AltSnapOptionKind.Number,
            En = "Snap threshold (px)", Zh = "貼邊距離（像素）",
            EnDesc = "How close (in pixels) a window must be to an edge before it snaps.",
            ZhDesc = "視窗要幾近邊緣（像素）先會貼齊。",
            Default = "20", Min = 0, Max = 200,
        },
        new()
        {
            Id = "movetrans", Section = "Advanced", Key = "MoveTrans", Kind = AltSnapOptionKind.Number,
            En = "Transparency while dragging (0–255)", Zh = "拖動時透明度（0–255）",
            EnDesc = "Window opacity while it is being moved (255 = opaque, lower = more see-through).",
            ZhDesc = "拖動時視窗嘅透明度（255 = 不透明，越細越透明）。",
            Default = "255", Min = 0, Max = 255,
        },
        new()
        {
            Id = "fullwin", Section = "Performance", Key = "FullWin", Kind = AltSnapOptionKind.Choice,
            En = "Show window contents while dragging", Zh = "拖動時顯示視窗內容",
            EnDesc = "On = solid live window; Off = a lightweight outline (snappier on slow machines).",
            ZhDesc = "開 = 實時實體視窗；關 = 輕量外框（慢機更順暢）。",
            Default = "1",
            Choices = new List<AltSnapChoice>
            {
                new("Outline only", "只顯示外框", "0"),
                new("Full contents", "完整內容", "1"),
            },
        },
        new()
        {
            Id = "usezones", Section = "Zones", Key = "UseZones", Kind = AltSnapOptionKind.Choice,
            En = "Snap layouts / zones", Zh = "貼齊版面／區域",
            EnDesc = "Enable FancyZones-style snap layouts when dragging windows.",
            ZhDesc = "拖動視窗時啟用類似 FancyZones 嘅貼齊版面。",
            Default = "0",
            Choices = new List<AltSnapChoice>
            {
                new("Off", "關", "0"),
                new("Snap layouts", "貼齊版面", "1"),
                new("Grid zones", "格狀區域", "3"),
            },
        },

        // ---- Multi-monitor ----
        new()
        {
            Id = "multiplemonitors", Section = "General", Key = "MultipleAltSnap", Kind = AltSnapOptionKind.Toggle,
            En = "Span multiple monitors", Zh = "跨越多個螢幕",
            EnDesc = "Allow snapping and maximizing across all connected monitors.",
            ZhDesc = "允許喺所有連接螢幕之間貼齊同最大化。",
            Default = "1",
        },

        // ---- Blacklists ----
        new()
        {
            Id = "bl_processes", Section = "Blacklist", Key = "Processes", Kind = AltSnapOptionKind.Text,
            En = "Process blacklist", Zh = "程式黑名單",
            EnDesc = "Comma-separated process names AltSnap must ignore. Example: Notepad.exe, vlc.exe",
            ZhDesc = "用逗號分隔嘅程式名，AltSnap 會忽略佢哋。例如：Notepad.exe, vlc.exe",
            Default = "",
        },
        new()
        {
            Id = "bl_windows", Section = "Blacklist", Key = "Windows", Kind = AltSnapOptionKind.Text,
            En = "Window blacklist (title|class)", Zh = "視窗黑名單（標題|類別）",
            EnDesc = "Comma-separated title|class entries to ignore. Example: Program Manager|Progman, *|Shell_TrayWnd",
            ZhDesc = "用逗號分隔嘅 標題|類別，會被忽略。例如：Program Manager|Progman, *|Shell_TrayWnd",
            Default = "",
        },
    };
}
