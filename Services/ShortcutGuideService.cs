using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;

namespace WinForge.Services;

/// <summary>
/// 一個鍵盤快捷鍵（雙語） · One Windows keyboard shortcut (bilingual).
/// Keys 係一連串按鍵標籤（例如 "Win", "E"），由 UI 砌成鍵帽。
/// Keys is the ordered list of key-cap labels (e.g. "Win", "E") the UI renders as chips.
/// </summary>
public sealed class ShortcutItem
{
    public string[] Keys { get; init; } = Array.Empty<string>();
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";

    public string Combo => string.Join(" + ", Keys);
    public string Desc => Loc.I.Pick(En, Zh);

    /// <summary>畀搜尋用嘅小寫乾草堆 · Lower-cased haystack for the reference-table search.</summary>
    public string Haystack => $"{Combo} {En} {Zh}".ToLowerInvariant();
}

/// <summary>一組同類嘅快捷鍵 · A titled group of related shortcuts.</summary>
public sealed class ShortcutGroup
{
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";
    public string Glyph { get; init; } = "";
    public List<ShortcutItem> Items { get; init; } = new();

    public string Title => Loc.I.Pick(En, Zh);
}

/// <summary>
/// 快捷鍵指南 · Shortcut Guide — a native clone of PowerToys Shortcut Guide.
///
/// 揿住 Windows 鍵一段時間（預設約 900ms）就會彈出一個半透明、置頂嘅覆蓋層，
/// 列出常用嘅 Win 鍵快捷鍵，按分類排好。放開 Win 鍵或者揿 Esc 就收起。
/// 用一個低階鍵盤掛鈎（WH_KEYBOARD_LL）偵測 Win 鍵嘅揿住／放開。
///
/// Holding the Windows key for a configurable duration (default ~900ms) shows a topmost,
/// semi-transparent overlay listing common Win-key shortcuts grouped by category. Releasing Win
/// or pressing Esc hides it. A low-level keyboard hook (WH_KEYBOARD_LL) detects the Win hold/release.
/// The overlay is shown/hidden on the UI thread via the captured DispatcherQueue.
/// </summary>
public static class ShortcutGuideService
{
    // ===================== settings =====================

    private const string EnabledKey = "shortcutguide.enabled";
    private const string HoldKey = "shortcutguide.holdms";
    private const string OpacityKey = "shortcutguide.opacity";   // 0..100
    private const string ThemeKey = "shortcutguide.theme";       // "Default" | "Light" | "Dark"

    public const int DefaultHoldMs = 900;
    public const int DefaultOpacity = 90;

    /// <summary>啟用整個功能（裝掛鈎） · Whether the hold-to-show hook is installed.</summary>
    public static bool Enabled
    {
        get => SettingsStore.Get(EnabledKey, "false") == "true";
        private set => SettingsStore.Set(EnabledKey, value ? "true" : "false");
    }

    /// <summary>要揿住 Win 幾耐（毫秒）先彈出 · How long to hold Win before the overlay appears (ms).</summary>
    public static int HoldMs
    {
        get => Clamp(GetIntPref(HoldKey, DefaultHoldMs), 200, 3000);
        set => SettingsStore.Set(HoldKey, Clamp(value, 200, 3000).ToString());
    }

    /// <summary>覆蓋層不透明度（0..100） · Overlay opacity 0..100.</summary>
    public static int Opacity
    {
        get => Clamp(GetIntPref(OpacityKey, DefaultOpacity), 30, 100);
        set => SettingsStore.Set(OpacityKey, Clamp(value, 30, 100).ToString());
    }

    private static int GetIntPref(string key, int fallback)
        => int.TryParse(SettingsStore.Get(key, fallback.ToString()), out var v) ? v : fallback;

    /// <summary>覆蓋層主題 · Overlay theme: Default / Light / Dark.</summary>
    public static string Theme
    {
        get => SettingsStore.Get(ThemeKey, "Dark");
        set => SettingsStore.Set(ThemeKey, value);
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    // ===================== lifecycle =====================

    private static DispatcherQueue? _ui;
    private static bool _hookRunning;
    private static Thread? _hookThread;
    private static uint _hookThreadId;
    private static Timer? _overlayFailsafeTimer;
    private const int OverlayFailsafeMs = 30000;

    /// <summary>覆蓋層而家係咪顯示緊 · Whether the overlay is currently visible.</summary>
    public static bool OverlayShowing { get; private set; }

    /// <summary>狀態改變（畀控制頁更新） · Raised when enabled/visible state changes.</summary>
    public static event Action? StateChanged;

    /// <summary>
    /// 由 app 啟動時呼叫一次，記住 UI dispatcher 並按設定裝掛鈎。
    /// Call once at app startup: capture the UI dispatcher and install the hook if the user enabled it.
    /// </summary>
    public static void Init(DispatcherQueue ui)
    {
        _ui = ui;
        if (Enabled) StartHook();
    }

    /// <summary>開或關功能（持久化 + 裝／拆掛鈎） · Turn the feature on/off (persist + install/remove the hook).</summary>
    public static void SetEnabled(bool on)
    {
        Enabled = on;
        if (on) StartHook();
        else StopHook();
        StateChanged?.Invoke();
    }

    private static void StartHook()
    {
        if (_hookRunning) return;
        _hookRunning = true;
        _hookThread = new Thread(HookLoop) { IsBackground = true, Name = "WinForge-ShortcutGuide" };
        _hookThread.Start();
    }

    private static void StopHook()
    {
        if (!_hookRunning) return;
        if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
        _hookRunning = false;
        HideOverlay();
    }

    // ===================== low-level keyboard hook =====================

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]
    private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int pt_x; public int pt_y; }

    [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint remove);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint WM_APP_QUIT = 0x8001;
    private const uint WM_APP_TIMER = 0x8002; // posted from the hold timer back to the hook thread

    private const uint VK_LWIN = 0x5B;
    private const uint VK_RWIN = 0x5C;
    private const uint VK_ESCAPE = 0x1B;

    private static IntPtr _hookHandle = IntPtr.Zero;
    private static HookProc? _hookProc; // keep the delegate alive

    // hold-detection state (all touched only on the hook thread)
    private static bool _winDown;
    private static bool _otherKeyWhileWin;  // another key was pressed → it's a chord, not a "show guide" hold
    private static long _winDownTick;
    private static Timer? _holdTimer;

    private static void HookLoop()
    {
        _hookThreadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // force a message queue on this thread
        _hookProc = HookCallback;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_APP_QUIT) break;
            if (msg.message == WM_APP_TIMER) { OnHoldElapsed(); continue; }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        CancelHoldTimer();
        if (_hookHandle != IntPtr.Zero) { UnhookWindowsHookEx(_hookHandle); _hookHandle = IntPtr.Zero; }
        _hookProc = null;
        _winDown = false; _otherKeyWhileWin = false;
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int msg = (int)wParam;
                bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;
                uint vk = data.vkCode;

                if (vk == VK_LWIN || vk == VK_RWIN)
                {
                    if (isDown && !_winDown)
                    {
                        _winDown = true;
                        _otherKeyWhileWin = false;
                        _winDownTick = Environment.TickCount64;
                        StartHoldTimer();
                    }
                    else if (isUp)
                    {
                        _winDown = false;
                        CancelHoldTimer();
                        if (OverlayShowing) HideOverlay();
                    }
                }
                else if (isDown)
                {
                    // Esc closes the overlay while it's up.
                    if (vk == VK_ESCAPE && OverlayShowing)
                    {
                        HideOverlay();
                        _otherKeyWhileWin = true;
                        CancelHoldTimer();
                    }
                    else if (_winDown)
                    {
                        // Win + something is a real chord (Win+E, Win+D…). Don't pop the guide for it,
                        // and if the guide already showed, leave it — the user can still read it until release.
                        _otherKeyWhileWin = true;
                        CancelHoldTimer();
                    }
                }
            }
            catch { /* never break the hook chain */ }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static void StartHoldTimer()
    {
        CancelHoldTimer();
        _holdTimer = new Timer(_ =>
        {
            // Marshal back onto the hook thread so all state is touched on one thread.
            if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, WM_APP_TIMER, IntPtr.Zero, IntPtr.Zero);
        }, null, HoldMs, Timeout.Infinite);
    }

    private static void CancelHoldTimer()
    {
        _holdTimer?.Dispose();
        _holdTimer = null;
    }

    private static void OnHoldElapsed()
    {
        CancelHoldTimer();
        // Still holding Win alone (no chord) and not already showing → pop the guide.
        if (_winDown && !_otherKeyWhileWin && !OverlayShowing)
            ShowOverlay();
    }

    // ===================== overlay orchestration (UI thread) =====================

    private static void ShowOverlay()
    {
        OverlayShowing = true;
        StartOverlayFailsafe();
        var ui = _ui;
        if (ui is null) return;
        ui.TryEnqueue(() =>
        {
            try { ShortcutGuideOverlay.Show(); }
            catch { OverlayShowing = false; CancelOverlayFailsafe(); }
            StateChanged?.Invoke();
        });
    }

    private static void HideOverlay()
    {
        if (!OverlayShowing) return;
        OverlayShowing = false;
        CancelOverlayFailsafe();
        var ui = _ui;
        if (ui is null) return;
        ui.TryEnqueue(() =>
        {
            try { ShortcutGuideOverlay.Hide(); }
            catch { }
            StateChanged?.Invoke();
        });
    }

    /// <summary>由控制頁手動預覽覆蓋層 · Manually preview the overlay (from the control page).</summary>
    public static void PreviewOverlay()
    {
        if (OverlayShowing) { HideOverlay(); return; }
        OverlayShowing = true;
        StartOverlayFailsafe();
        _ui?.TryEnqueue(() =>
        {
            try { ShortcutGuideOverlay.Show(autoCloseOnDeactivate: true); }
            catch { OverlayShowing = false; CancelOverlayFailsafe(); }
            StateChanged?.Invoke();
        });
    }

    /// <summary>覆蓋層自行收起時（例如失焦）通知返服務 · Called by the overlay when it self-closes.</summary>
    public static void NotifyOverlayClosed()
    {
        OverlayShowing = false;
        CancelOverlayFailsafe();
        StateChanged?.Invoke();
    }

    private static void StartOverlayFailsafe()
    {
        try
        {
            _overlayFailsafeTimer?.Dispose();
            _overlayFailsafeTimer = new Timer(_ =>
            {
                if (OverlayShowing) HideOverlay();
            }, null, OverlayFailsafeMs, Timeout.Infinite);
        }
        catch { }
    }

    private static void CancelOverlayFailsafe()
    {
        try
        {
            _overlayFailsafeTimer?.Dispose();
            _overlayFailsafeTimer = null;
        }
        catch { }
    }

    // ===================== the shortcut catalogue =====================

    /// <summary>
    /// 全部分類好嘅 Windows 鍵盤快捷鍵（雙語） · The full, categorised catalogue of Windows shortcuts (bilingual).
    /// Used both by the overlay and the searchable reference table on the control page.
    /// </summary>
    public static readonly List<ShortcutGroup> Groups = BuildCatalogue();

    /// <summary>覆蓋層只顯示嘅最常用 Win 鍵快捷鍵分類 · The subset shown in the overlay (the Win-key essentials).</summary>
    public static readonly List<ShortcutGroup> OverlayGroups = Groups
        .Where(g => g.En is "Essentials" or "Snap & Windows" or "Virtual Desktops & Tasks" or "Apps & Search" or "Capture & Tools")
        .ToList();

    /// <summary>所有快捷鍵攤平（畀搜尋用） · All items flattened (for the reference-table search).</summary>
    public static IEnumerable<(ShortcutGroup Group, ShortcutItem Item)> Flatten()
        => Groups.SelectMany(g => g.Items.Select(i => (g, i)));

    private static List<ShortcutGroup> BuildCatalogue()
    {
        ShortcutItem S(string[] keys, string en, string zh) => new() { Keys = keys, En = en, Zh = zh };

        return new List<ShortcutGroup>
        {
            // ---------- Essentials ----------
            new()
            {
                En = "Essentials", Zh = "必備快捷鍵", Glyph = "",
                Items =
                {
                    S(new[]{"Win"}, "Open or close the Start menu", "開／關開始功能表"),
                    S(new[]{"Win","E"}, "Open File Explorer", "開啟檔案總管"),
                    S(new[]{"Win","D"}, "Show or hide the desktop", "顯示或隱藏桌面"),
                    S(new[]{"Win","L"}, "Lock the PC", "鎖定電腦"),
                    S(new[]{"Win","I"}, "Open Settings", "開啟設定"),
                    S(new[]{"Win","A"}, "Open Quick Settings", "開啟快速設定"),
                    S(new[]{"Win","N"}, "Open notifications & calendar", "開啟通知與行事曆"),
                    S(new[]{"Win","R"}, "Open the Run dialog", "開啟執行對話框"),
                    S(new[]{"Win","X"}, "Open the Quick Link (power user) menu", "開啟快速連結（進階）選單"),
                    S(new[]{"Win","Pause"}, "Open System (About) page", "開啟系統（關於）頁面"),
                },
            },

            // ---------- Snap & Windows ----------
            new()
            {
                En = "Snap & Windows", Zh = "視窗貼齊與排列", Glyph = "",
                Items =
                {
                    S(new[]{"Win","←"}, "Snap window to the left", "視窗貼齊左邊"),
                    S(new[]{"Win","→"}, "Snap window to the right", "視窗貼齊右邊"),
                    S(new[]{"Win","↑"}, "Maximise the window", "最大化視窗"),
                    S(new[]{"Win","↓"}, "Minimise / restore the window", "最小化／還原視窗"),
                    S(new[]{"Win","Z"}, "Open the snap layouts flyout", "開啟貼齊版面選單"),
                    S(new[]{"Win","Home"}, "Minimise all but the active window", "最小化其他視窗"),
                    S(new[]{"Win","Shift","↑"}, "Stretch window to top and bottom", "視窗拉到上下"),
                    S(new[]{"Win","Shift","←"}, "Move window to the left monitor", "視窗移到左邊螢幕"),
                    S(new[]{"Win","Shift","→"}, "Move window to the right monitor", "視窗移到右邊螢幕"),
                    S(new[]{"Win",","}, "Peek at the desktop (hold)", "暫看桌面（揿住）"),
                },
            },

            // ---------- Virtual Desktops & Tasks ----------
            new()
            {
                En = "Virtual Desktops & Tasks", Zh = "虛擬桌面與工作", Glyph = "",
                Items =
                {
                    S(new[]{"Win","Tab"}, "Open Task View", "開啟工作檢視"),
                    S(new[]{"Win","Ctrl","D"}, "Create a new virtual desktop", "建立新虛擬桌面"),
                    S(new[]{"Win","Ctrl","←"}, "Switch to the desktop on the left", "切換到左邊虛擬桌面"),
                    S(new[]{"Win","Ctrl","→"}, "Switch to the desktop on the right", "切換到右邊虛擬桌面"),
                    S(new[]{"Win","Ctrl","F4"}, "Close the current virtual desktop", "關閉目前虛擬桌面"),
                    S(new[]{"Alt","Tab"}, "Switch between open apps", "在開啟的應用程式之間切換"),
                    S(new[]{"Ctrl","Shift","Esc"}, "Open Task Manager", "開啟工作管理員"),
                },
            },

            // ---------- Apps & Search ----------
            new()
            {
                En = "Apps & Search", Zh = "應用程式與搜尋", Glyph = "",
                Items =
                {
                    S(new[]{"Win","S"}, "Open search", "開啟搜尋"),
                    S(new[]{"Win","Q"}, "Open search", "開啟搜尋"),
                    S(new[]{"Win","1–9"}, "Open / switch to a taskbar app by number", "依編號開啟／切換工作列應用程式"),
                    S(new[]{"Win","T"}, "Cycle through taskbar apps", "循環切換工作列應用程式"),
                    S(new[]{"Win","B"}, "Focus the system tray (taskbar corner)", "聚焦系統匣（工作列角落）"),
                    S(new[]{"Win","C"}, "Open Copilot / chat", "開啟 Copilot／聊天"),
                    S(new[]{"Win","W"}, "Open Widgets", "開啟小工具"),
                    S(new[]{"Win","K"}, "Open Cast (Connect)", "開啟投放（連線）"),
                },
            },

            // ---------- Capture & Tools ----------
            new()
            {
                En = "Capture & Tools", Zh = "擷取與工具", Glyph = "",
                Items =
                {
                    S(new[]{"Win","Shift","S"}, "Snip a screen region (Snipping Tool)", "擷取螢幕區域（剪取工具）"),
                    S(new[]{"Win","PrtScn"}, "Save a full screenshot to Pictures", "整個螢幕截圖存到圖片"),
                    S(new[]{"Win","V"}, "Open clipboard history", "開啟剪貼簿歷史"),
                    S(new[]{"Win","."}, "Open emoji & symbols picker", "開啟表情符號選擇器"),
                    S(new[]{"Win",";"}, "Open emoji & symbols picker", "開啟表情符號選擇器"),
                    S(new[]{"Win","H"}, "Start voice typing (dictation)", "開始語音輸入（聽寫）"),
                    S(new[]{"Win","G"}, "Open the Xbox Game Bar", "開啟 Xbox Game Bar"),
                    S(new[]{"Win","Alt","R"}, "Start / stop game recording", "開始／停止遊戲錄影"),
                    S(new[]{"Win","P"}, "Choose a presentation display mode", "選擇投影顯示模式"),
                },
            },

            // ---------- Accessibility ----------
            new()
            {
                En = "Accessibility", Zh = "協助工具", Glyph = "",
                Items =
                {
                    S(new[]{"Win","+"}, "Open Magnifier / zoom in", "開啟放大鏡／放大"),
                    S(new[]{"Win","-"}, "Zoom out (Magnifier)", "縮小（放大鏡）"),
                    S(new[]{"Win","Esc"}, "Close Magnifier", "關閉放大鏡"),
                    S(new[]{"Win","Ctrl","Enter"}, "Turn Narrator on or off", "開或關朗讀程式"),
                    S(new[]{"Win","U"}, "Open Accessibility settings", "開啟協助工具設定"),
                    S(new[]{"Win","Ctrl","C"}, "Toggle colour filters", "切換色彩濾鏡"),
                    S(new[]{"Win","Ctrl","O"}, "Open the on-screen keyboard", "開啟螢幕小鍵盤"),
                    S(new[]{"Win","Ctrl","S"}, "Open Windows Speech Recognition", "開啟 Windows 語音辨識"),
                },
            },

            // ---------- Text editing ----------
            new()
            {
                En = "Text & Editing", Zh = "文字與編輯", Glyph = "",
                Items =
                {
                    S(new[]{"Ctrl","C"}, "Copy", "複製"),
                    S(new[]{"Ctrl","X"}, "Cut", "剪下"),
                    S(new[]{"Ctrl","V"}, "Paste", "貼上"),
                    S(new[]{"Ctrl","Z"}, "Undo", "復原"),
                    S(new[]{"Ctrl","Y"}, "Redo", "重做"),
                    S(new[]{"Ctrl","A"}, "Select all", "全選"),
                    S(new[]{"Ctrl","F"}, "Find", "尋找"),
                    S(new[]{"Ctrl","S"}, "Save", "儲存"),
                    S(new[]{"Ctrl","P"}, "Print", "列印"),
                    S(new[]{"Ctrl","←"}, "Move cursor to the previous word", "游標移到上一個字"),
                    S(new[]{"Ctrl","→"}, "Move cursor to the next word", "游標移到下一個字"),
                    S(new[]{"F2"}, "Rename the selected item", "重新命名所選項目"),
                },
            },

            // ---------- File Explorer ----------
            new()
            {
                En = "File Explorer", Zh = "檔案總管", Glyph = "",
                Items =
                {
                    S(new[]{"Ctrl","N"}, "Open a new window", "開新視窗"),
                    S(new[]{"Ctrl","W"}, "Close the current window", "關閉目前視窗"),
                    S(new[]{"Ctrl","Shift","N"}, "Create a new folder", "建立新資料夾"),
                    S(new[]{"Alt","↑"}, "Go up one folder", "上一層資料夾"),
                    S(new[]{"Alt","←"}, "Go back", "返回"),
                    S(new[]{"Alt","→"}, "Go forward", "前進"),
                    S(new[]{"Alt","Enter"}, "Open Properties for the selected item", "開啟所選項目內容"),
                    S(new[]{"F5"}, "Refresh", "重新整理"),
                    S(new[]{"Ctrl","Shift","E"}, "Expand the folder tree to the current folder", "展開資料夾樹至目前位置"),
                },
            },

            // ---------- Window management ----------
            new()
            {
                En = "Window Management", Zh = "視窗管理", Glyph = "",
                Items =
                {
                    S(new[]{"Alt","F4"}, "Close the active window", "關閉作用中視窗"),
                    S(new[]{"Alt","Space"}, "Open the window's system menu", "開啟視窗系統選單"),
                    S(new[]{"Alt","Esc"}, "Cycle through open windows", "循環切換開啟的視窗"),
                    S(new[]{"Ctrl","Alt","Tab"}, "View open apps (stays open)", "檢視開啟的應用程式（保持開啟）"),
                    S(new[]{"Win","M"}, "Minimise all windows", "最小化所有視窗"),
                    S(new[]{"Win","Shift","M"}, "Restore minimised windows", "還原最小化視窗"),
                    S(new[]{"Ctrl","Shift","Esc"}, "Open Task Manager", "開啟工作管理員"),
                },
            },
        };
    }
}
