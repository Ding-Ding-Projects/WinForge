using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinForge.Services;

/// <summary>ZoomIt 嘅模式 · Which ZoomIt mode an overlay session runs in.</summary>
public enum ZoomItMode
{
    /// <summary>放大 + 可畫 · Zoom into a frozen frame; can also draw on top.</summary>
    Zoom,
    /// <summary>即時畫（唔放大）· Draw on a frozen snapshot of the live screen without zooming.</summary>
    Draw,
    /// <summary>小休倒數 · Large break-timer countdown over a dimmed frozen screen.</summary>
    Break,
}

/// <summary>畫筆形狀 · The pen tool the user is drawing with.</summary>
public enum ZoomItPenShape
{
    Freehand,
    Rectangle,
    Arrow,
    Highlighter,
}

/// <summary>
/// ZoomIt 引擎（原生克隆）· Native ZoomIt clone — screen zoom, freehand/shape annotation, and a break
/// timer, all on a single pure-Win32 layered, topmost overlay window painted with GDI. Mirrors the
/// approach of <see cref="RegionSelector"/>: a borderless WS_POPUP window covering the virtual desktop,
/// a freeze-frame captured via BitBlt on entry, and a private modal message loop on a dedicated STA
/// thread. No WinUI window transparency, no Win2D — just GDI StretchBlt for zoom/pan and GDI pens for
/// annotation. Global hotkeys (configurable) are registered on a background pump thread.
/// </summary>
public static class ZoomItService
{
    // ====================== persisted settings ======================

    private const string KeyZoomMods = "zoomit.zoom.mods";
    private const string KeyZoomVk = "zoomit.zoom.vk";
    private const string KeyDrawMods = "zoomit.draw.mods";
    private const string KeyDrawVk = "zoomit.draw.vk";
    private const string KeyBreakMods = "zoomit.break.mods";
    private const string KeyBreakVk = "zoomit.break.vk";
    private const string KeyPenColor = "zoomit.pen.color";   // ARGB-less: stored as 0xRRGGBB
    private const string KeyPenWidth = "zoomit.pen.width";
    private const string KeyBreakMinutes = "zoomit.break.minutes";

    /// <summary>每組熱鍵嘅修飾鍵／按鍵（HotMod | VK） · Modifiers + VK for each mode's global hotkey.</summary>
    public static uint ZoomMods { get; set; } = (uint)HotMod.Control;
    public static uint ZoomVk { get; set; } = 0x31;   // '1'
    public static uint DrawMods { get; set; } = (uint)HotMod.Control;
    public static uint DrawVk { get; set; } = 0x32;   // '2'
    public static uint BreakMods { get; set; } = (uint)HotMod.Control;
    public static uint BreakVk { get; set; } = 0x33;  // '3'

    /// <summary>預設畫筆顏色（0xRRGGBB）同闊度（像素） · Default pen colour (0xRRGGBB) and width (px).</summary>
    public static int PenColorRgb { get; set; } = 0xFF0000; // red
    public static int PenWidth { get; set; } = 6;
    public static int BreakMinutes { get; set; } = 10;

    /// <summary>最近一次狀態（畀 UI 顯示）· Last status line, for the control page.</summary>
    public static string LastEvent { get; private set; } = "";
    public static event Action? Fired;     // a hotkey fired / a session opened or closed
    public static event Action? Changed;   // settings persisted

    private static bool _loaded;

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        ZoomMods = ReadUint(KeyZoomMods, ZoomMods);
        ZoomVk = ReadUint(KeyZoomVk, ZoomVk);
        DrawMods = ReadUint(KeyDrawMods, DrawMods);
        DrawVk = ReadUint(KeyDrawVk, DrawVk);
        BreakMods = ReadUint(KeyBreakMods, BreakMods);
        BreakVk = ReadUint(KeyBreakVk, BreakVk);
        PenColorRgb = (int)ReadUint(KeyPenColor, (uint)PenColorRgb);
        PenWidth = (int)ReadUint(KeyPenWidth, (uint)PenWidth);
        BreakMinutes = (int)ReadUint(KeyBreakMinutes, (uint)BreakMinutes);
    }

    public static void Save()
    {
        SettingsStore.Set(KeyZoomMods, ZoomMods.ToString());
        SettingsStore.Set(KeyZoomVk, ZoomVk.ToString());
        SettingsStore.Set(KeyDrawMods, DrawMods.ToString());
        SettingsStore.Set(KeyDrawVk, DrawVk.ToString());
        SettingsStore.Set(KeyBreakMods, BreakMods.ToString());
        SettingsStore.Set(KeyBreakVk, BreakVk.ToString());
        SettingsStore.Set(KeyPenColor, ((uint)PenColorRgb).ToString());
        SettingsStore.Set(KeyPenWidth, ((uint)PenWidth).ToString());
        SettingsStore.Set(KeyBreakMinutes, ((uint)BreakMinutes).ToString());
        Changed?.Invoke();
        RestartHotkeys();
    }

    private static uint ReadUint(string key, uint fallback) =>
        uint.TryParse(SettingsStore.Get(key, fallback.ToString()), out var v) ? v : fallback;

    // ====================== global hotkeys (RegisterHotKey + WM_HOTKEY) ======================

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint WM_APP_RELOAD = 0x8000;
    private const uint WM_APP_QUIT = 0x8001;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint remove);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int pt_x, pt_y; }

    private static Thread? _hotkeyThread;
    private static uint _hotkeyThreadId;
    private static bool _hotkeysRunning;

    private const int IdZoom = 1, IdDraw = 2, IdBreak = 3;

    /// <summary>啟動背景熱鍵泵（idempotent） · Start the background hotkey pump (idempotent).</summary>
    public static void StartHotkeys()
    {
        Load();
        if (_hotkeysRunning) { RestartHotkeys(); return; }
        _hotkeysRunning = true;
        _hotkeyThread = new Thread(HotkeyLoop) { IsBackground = true, Name = "WinForge-ZoomIt-Hotkeys" };
        _hotkeyThread.SetApartmentState(ApartmentState.STA);
        _hotkeyThread.Start();
    }

    public static void RestartHotkeys()
    {
        if (_hotkeysRunning && _hotkeyThreadId != 0)
            PostThreadMessage(_hotkeyThreadId, WM_APP_RELOAD, IntPtr.Zero, IntPtr.Zero);
    }

    public static void StopHotkeys()
    {
        if (_hotkeysRunning && _hotkeyThreadId != 0)
            PostThreadMessage(_hotkeyThreadId, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
        _hotkeysRunning = false;
    }

    private static void HotkeyLoop()
    {
        _hotkeyThreadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // force a message queue
        RegisterAll();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOTKEY)
            {
                int id = (int)msg.wParam;
                switch (id)
                {
                    case IdZoom: TriggerOnThisThread(ZoomItMode.Zoom); break;
                    case IdDraw: TriggerOnThisThread(ZoomItMode.Draw); break;
                    case IdBreak: TriggerOnThisThread(ZoomItMode.Break); break;
                }
            }
            else if (msg.message == WM_APP_RELOAD) { RegisterAll(); }
            else if (msg.message == WM_APP_QUIT) { UnregisterAll(); break; }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private static void RegisterAll()
    {
        Load();
        UnregisterAll();
        if (ZoomVk != 0) RegisterHotKey(IntPtr.Zero, IdZoom, ZoomMods | MOD_NOREPEAT, ZoomVk);
        if (DrawVk != 0) RegisterHotKey(IntPtr.Zero, IdDraw, DrawMods | MOD_NOREPEAT, DrawVk);
        if (BreakVk != 0) RegisterHotKey(IntPtr.Zero, IdBreak, BreakMods | MOD_NOREPEAT, BreakVk);
    }

    private static void UnregisterAll()
    {
        UnregisterHotKey(IntPtr.Zero, IdZoom);
        UnregisterHotKey(IntPtr.Zero, IdDraw);
        UnregisterHotKey(IntPtr.Zero, IdBreak);
    }

    /// <summary>喺熱鍵泵執行緒上開覆蓋層（已有訊息迴圈）· Open the overlay on the hotkey-pump thread.</summary>
    private static void TriggerOnThisThread(ZoomItMode mode)
    {
        if (_sessionOpen) return; // ignore re-entry while an overlay is up
        LastEvent = Loc.I.Pick($"Opened {mode} overlay", $"已開啟{ModeZh(mode)}覆蓋層");
        try { Fired?.Invoke(); } catch { }
        RunOverlay(mode);
        LastEvent = Loc.I.Pick($"Closed {mode} overlay", $"已關閉{ModeZh(mode)}覆蓋層");
        try { Fired?.Invoke(); } catch { }
    }

    private static string ModeZh(ZoomItMode m) => m switch
    {
        ZoomItMode.Zoom => "放大",
        ZoomItMode.Draw => "畫筆",
        _ => "小休",
    };

    /// <summary>
    /// 由 UI 執行緒手動開覆蓋層 · Manually open an overlay from the UI thread (control-page buttons).
    /// Runs a private modal message loop, so it returns only after the user exits the overlay.
    /// </summary>
    public static void OpenOverlay(ZoomItMode mode)
    {
        Load();
        if (_sessionOpen) return;
        LastEvent = Loc.I.Pick($"Opened {mode} overlay", $"已開啟{ModeZh(mode)}覆蓋層");
        try { Fired?.Invoke(); } catch { }
        RunOverlay(mode);
        LastEvent = Loc.I.Pick($"Closed {mode} overlay", $"已關閉{ModeZh(mode)}覆蓋層");
        try { Fired?.Invoke(); } catch { }
    }

    // ====================== Win32 plumbing for the overlay ======================

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEX
    {
        public uint cbSize, style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PAINTSTRUCT
    {
        public IntPtr hdc; public bool fErase; public RECT rcPaint;
        public bool fRestore, fIncUpdate;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] rgbReserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LOGFONT
    {
        public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
        public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision, lfClipPrecision, lfQuality, lfPitchAndFamily;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string lfFaceName;
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string? windowName,
        uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr SetCapture(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr inst, int id);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool FillRect(IntPtr hDC, ref RECT rc, IntPtr brush);
    [DllImport("user32.dll")] private static extern int SetTimer(IntPtr hWnd, int id, uint elapseMs, IntPtr proc);
    [DllImport("user32.dll")] private static extern bool KillTimer(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? name);

    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int dx, int dy, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr dst, int dx, int dy, int dw, int dh, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int obj);
    [DllImport("gdi32.dll")] private static extern bool MoveToEx(IntPtr hdc, int x, int y, IntPtr old);
    [DllImport("gdi32.dll")] private static extern bool LineTo(IntPtr hdc, int x, int y);
    [DllImport("gdi32.dll")] private static extern bool Rectangle(IntPtr hdc, int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern bool Polygon(IntPtr hdc, POINT[] pts, int count);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateFontIndirect(ref LOGFONT lf);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool TextOut(IntPtr hdc, int x, int y, string s, int len);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool GetTextExtentPoint32(IntPtr hdc, string s, int len, out POINT sz);

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("msimg32.dll")] private static extern bool AlphaBlend(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, BLENDFUNCTION blend);

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_POPUP = 0x80000000;
    private const int SW_SHOW = 5;
    private const uint WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_TIMER = 0x0113;
    private const uint WM_KEYDOWN = 0x0100, WM_CHAR = 0x0102;
    private const uint WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_MOUSEMOVE = 0x0200;
    private const uint WM_RBUTTONDOWN = 0x0204, WM_MOUSEWHEEL = 0x020A;
    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    private const int IDC_ARROW = 32512, IDC_CROSS = 32515;
    private const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;
    private const int HALFTONE = 4, NULL_BRUSH = 5, PS_SOLID = 0, TRANSPARENT = 1;
    private const int VK_ESCAPE = 0x1B, VK_ADD = 0x6B, VK_SUBTRACT = 0x6D, VK_OEM_PLUS = 0xBB, VK_OEM_MINUS = 0xBD;

    private static readonly float[] ZoomLevels = { 1.0f, 1.25f, 1.5f, 1.75f, 2.0f, 2.5f, 3.0f, 4.0f, 6.0f, 8.0f };

    // ====================== session state (overlay-thread-local) ======================

    private static volatile bool _sessionOpen;
    private static ZoomItMode _mode;
    private static IntPtr _hwnd;
    private static WndProc? _proc;        // keep the delegate alive

    private static int _vx, _vy, _vw, _vh; // virtual-screen origin + size (physical px)

    // freeze-frame DIB
    private static IntPtr _frameDc;
    private static IntPtr _frameBmp;
    private static IntPtr _frameOldBmp;

    // zoom/pan
    private static int _zoomIndex;        // index into ZoomLevels
    private static int _mouseX, _mouseY;  // last cursor pos in window-local coords
    private static int _viewX, _viewY;    // top-left of the source region currently shown (window-local)

    // drawing
    private static bool _drawing;
    private static int _penColor;         // 0xRRGGBB
    private static int _penWidth;
    private static ZoomItPenShape _shape;
    private static readonly List<Stroke> _strokes = new();
    private static Stroke? _current;

    // break timer
    private static int _breakRemaining;   // seconds

    private sealed class Stroke
    {
        public int Color;
        public int Width;
        public ZoomItPenShape Shape;
        public List<POINT> Points = new();
    }

    private static void RunOverlay(ZoomItMode mode)
    {
        _sessionOpen = true;
        _mode = mode;
        _strokes.Clear();
        _current = null;
        _drawing = false;
        _penColor = PenColorRgb;
        _penWidth = Math.Clamp(PenWidth, 1, 60);
        _shape = ZoomItPenShape.Freehand;
        _zoomIndex = mode == ZoomItMode.Zoom ? 4 : 0; // start at 2.0x when zooming
        _breakRemaining = Math.Max(1, BreakMinutes) * 60;

        _vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        _vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        _vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        _vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (_vw <= 0 || _vh <= 0) { _sessionOpen = false; return; }

        if (!CaptureFrame()) { ReleaseFrame(); _sessionOpen = false; return; }

        // initial cursor → window-local
        GetCursorPos(out var cp);
        _mouseX = Math.Clamp(cp.X - _vx, 0, _vw - 1);
        _mouseY = Math.Clamp(cp.Y - _vy, 0, _vh - 1);
        RecomputeView();

        var inst = GetModuleHandle(null);
        _proc = WindowProc;
        var cls = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = inst,
            hCursor = LoadCursor(IntPtr.Zero, mode == ZoomItMode.Break ? IDC_ARROW : IDC_CROSS),
            hbrBackground = IntPtr.Zero,
            lpszClassName = "WinForgeZoomItOverlay",
        };
        RegisterClassEx(ref cls); // safe to call repeatedly

        _hwnd = CreateWindowEx(
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
            "WinForgeZoomItOverlay", null, WS_POPUP,
            _vx, _vy, _vw, _vh, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) { ReleaseFrame(); _proc = null; _sessionOpen = false; return; }

        ShowWindow(_hwnd, SW_SHOW);
        UpdateWindow(_hwnd);
        SetForegroundWindow(_hwnd);
        if (mode == ZoomItMode.Break) SetTimer(_hwnd, 1, 1000, IntPtr.Zero);

        // private modal loop
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
            if (_hwnd == IntPtr.Zero) break;
        }

        Cleanup();
        _sessionOpen = false;
    }

    private static void Cleanup()
    {
        if (_hwnd != IntPtr.Zero) { KillTimer(_hwnd, 1); DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
        ReleaseFrame();
        _proc = null;
        _strokes.Clear();
        _current = null;
    }

    /// <summary>影低整個虛擬桌面 → 記憶體 DIB · BitBlt the whole virtual desktop into a memory bitmap.</summary>
    private static bool CaptureFrame()
    {
        var screen = WinGetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) return false;
        try
        {
            _frameDc = CreateCompatibleDC(screen);
            _frameBmp = CreateCompatibleBitmap(screen, _vw, _vh);
            if (_frameDc == IntPtr.Zero || _frameBmp == IntPtr.Zero) return false;
            _frameOldBmp = SelectObject(_frameDc, _frameBmp);
            return BitBlt(_frameDc, 0, 0, _vw, _vh, screen, _vx, _vy, SRCCOPY | CAPTUREBLT);
        }
        finally { WinReleaseDC(IntPtr.Zero, screen); }
    }

    [DllImport("user32.dll", EntryPoint = "GetDC")] private static extern IntPtr WinGetDC(IntPtr hWnd);
    [DllImport("user32.dll", EntryPoint = "ReleaseDC")] private static extern int WinReleaseDC(IntPtr hWnd, IntPtr hDC);

    private static void ReleaseFrame()
    {
        if (_frameDc != IntPtr.Zero)
        {
            if (_frameOldBmp != IntPtr.Zero) SelectObject(_frameDc, _frameOldBmp);
            DeleteDC(_frameDc); _frameDc = IntPtr.Zero; _frameOldBmp = IntPtr.Zero;
        }
        if (_frameBmp != IntPtr.Zero) { DeleteObject(_frameBmp); _frameBmp = IntPtr.Zero; }
    }

    // ====================== view (zoom + pan) maths ======================

    private static float Zoom => ZoomLevels[Math.Clamp(_zoomIndex, 0, ZoomLevels.Length - 1)];

    /// <summary>由游標位置計返要顯示嘅來源左上角 · Recompute the source top-left so the cursor stays centred.</summary>
    private static void RecomputeView()
    {
        float z = Zoom;
        int srcW = (int)(_vw / z);
        int srcH = (int)(_vh / z);
        // centre the visible source window on the cursor
        int x = _mouseX - srcW / 2;
        int y = _mouseY - srcH / 2;
        x = Math.Clamp(x, 0, Math.Max(0, _vw - srcW));
        y = Math.Clamp(y, 0, Math.Max(0, _vh - srcH));
        _viewX = x; _viewY = y;
    }

    /// <summary>視窗座標 → 凍結畫面（來源）座標 · Map a window point to a frozen-frame source point.</summary>
    private static POINT WindowToSource(int wx, int wy)
    {
        if (_mode != ZoomItMode.Zoom) return new POINT { X = wx, Y = wy };
        float z = Zoom;
        return new POINT { X = _viewX + (int)(wx / z), Y = _viewY + (int)(wy / z) };
    }

    // ====================== window proc ======================

    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_MOUSEMOVE:
                _mouseX = LoWord(lParam); _mouseY = HiWord(lParam);
                if (_mode == ZoomItMode.Zoom && !_drawing) RecomputeView();
                if (_drawing && _current is not null)
                {
                    var p = WindowToSource(_mouseX, _mouseY);
                    if (_current.Shape == ZoomItPenShape.Freehand || _current.Shape == ZoomItPenShape.Highlighter)
                        _current.Points.Add(p);
                    else
                    {
                        // shapes keep just start + current
                        if (_current.Points.Count < 2) _current.Points.Add(p);
                        else _current.Points[1] = p;
                    }
                }
                InvalidateRect(hWnd, IntPtr.Zero, false);
                return IntPtr.Zero;

            case WM_LBUTTONDOWN:
                if (_mode != ZoomItMode.Break)
                {
                    _drawing = true;
                    _current = new Stroke { Color = _penColor, Width = _penWidth, Shape = _shape };
                    _current.Points.Add(WindowToSource(LoWord(lParam), HiWord(lParam)));
                    SetCapture(hWnd);
                    InvalidateRect(hWnd, IntPtr.Zero, false);
                }
                return IntPtr.Zero;

            case WM_LBUTTONUP:
                if (_drawing)
                {
                    _drawing = false;
                    ReleaseCapture();
                    if (_current is not null && _current.Points.Count > 0) _strokes.Add(_current);
                    _current = null;
                    InvalidateRect(hWnd, IntPtr.Zero, false);
                }
                return IntPtr.Zero;

            case WM_RBUTTONDOWN:
                PostQuitMessage(0);
                return IntPtr.Zero;

            case WM_MOUSEWHEEL:
                OnWheel(HiWord(wParam));
                InvalidateRect(hWnd, IntPtr.Zero, false);
                return IntPtr.Zero;

            case WM_CHAR:
                OnChar((char)(wParam.ToInt64() & 0xFFFF));
                InvalidateRect(hWnd, IntPtr.Zero, false);
                return IntPtr.Zero;

            case WM_KEYDOWN:
                OnKeyDown((int)(wParam.ToInt64() & 0xFFFF), hWnd);
                return IntPtr.Zero;

            case WM_TIMER:
                if (_mode == ZoomItMode.Break)
                {
                    _breakRemaining--;
                    if (_breakRemaining <= 0) { PostQuitMessage(0); return IntPtr.Zero; }
                    InvalidateRect(hWnd, IntPtr.Zero, false);
                }
                return IntPtr.Zero;

            case WM_PAINT:
                OnPaint(hWnd);
                return IntPtr.Zero;

            case WM_DESTROY:
                PostQuitMessage(0);
                return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static void OnWheel(int delta)
    {
        if (_mode == ZoomItMode.Break) return;
        bool up = delta > 0;
        if (_mode == ZoomItMode.Zoom && !_drawing)
        {
            // wheel zooms in/out
            _zoomIndex = Math.Clamp(_zoomIndex + (up ? 1 : -1), 0, ZoomLevels.Length - 1);
            RecomputeView();
        }
        else
        {
            // in draw mode (or while a stroke is active) the wheel changes pen thickness
            _penWidth = Math.Clamp(_penWidth + (up ? 1 : -1), 1, 60);
        }
    }

    private static void OnChar(char c)
    {
        switch (char.ToLowerInvariant(c))
        {
            case 'r': _penColor = 0xFF0000; break;
            case 'g': _penColor = 0x00C000; break;
            case 'b': _penColor = 0x0078D4; break;
            case 'o': _penColor = 0xFF8C00; break;
            case 'y': _penColor = 0xFFD400; break;
            case '+': case '=': _penWidth = Math.Clamp(_penWidth + 1, 1, 60); break;
            case '-': case '_': _penWidth = Math.Clamp(_penWidth - 1, 1, 60); break;
        }
    }

    private static void OnKeyDown(int vk, IntPtr hWnd)
    {
        switch (vk)
        {
            case VK_ESCAPE:
                // ESC: if there are strokes, clear them first; otherwise exit.
                if (_mode != ZoomItMode.Break && _strokes.Count > 0)
                {
                    _strokes.Clear();
                    InvalidateRect(hWnd, IntPtr.Zero, false);
                }
                else PostQuitMessage(0);
                return;

            case VK_ADD: case VK_OEM_PLUS:
                if (_mode == ZoomItMode.Zoom)
                { _zoomIndex = Math.Clamp(_zoomIndex + 1, 0, ZoomLevels.Length - 1); RecomputeView(); }
                else _penWidth = Math.Clamp(_penWidth + 1, 1, 60);
                InvalidateRect(hWnd, IntPtr.Zero, false);
                return;

            case VK_SUBTRACT: case VK_OEM_MINUS:
                if (_mode == ZoomItMode.Zoom)
                { _zoomIndex = Math.Clamp(_zoomIndex - 1, 0, ZoomLevels.Length - 1); RecomputeView(); }
                else _penWidth = Math.Clamp(_penWidth - 1, 1, 60);
                InvalidateRect(hWnd, IntPtr.Zero, false);
                return;

            // shape selectors (letters also arrive via WM_CHAR, but VKs are reliable for these)
            case 0x52: /* R */ break; // colour handled in WM_CHAR
        }

        // shape selection by VK letter
        switch (vk)
        {
            case 0x45: _shape = ZoomItPenShape.Freehand; break;    // E = freehand (pen)
            case 0x4B: _shape = ZoomItPenShape.Rectangle; break;   // K = rectangle (boX-like)
            case 0x41: _shape = ZoomItPenShape.Arrow; break;       // A = arrow
            case 0x48: _shape = ZoomItPenShape.Highlighter; break; // H = highlighter
        }
        InvalidateRect(hWnd, IntPtr.Zero, false);
    }

    // ====================== painting ======================

    private static void OnPaint(IntPtr hWnd)
    {
        var hdc = BeginPaint(hWnd, out var ps);
        // double buffer to avoid flicker
        var memDc = CreateCompatibleDC(hdc);
        var memBmp = CreateCompatibleBitmap(hdc, _vw, _vh);
        var oldBmp = SelectObject(memDc, memBmp);
        try
        {
            PaintFrame(memDc);
            PaintStrokes(memDc);
            if (_mode == ZoomItMode.Break) PaintBreak(memDc);
            PaintHud(memDc);
            BitBlt(hdc, 0, 0, _vw, _vh, memDc, 0, 0, SRCCOPY);
        }
        finally
        {
            SelectObject(memDc, oldBmp);
            DeleteObject(memBmp);
            DeleteDC(memDc);
            EndPaint(hWnd, ref ps);
        }
    }

    private static void PaintFrame(IntPtr dc)
    {
        if (_mode == ZoomItMode.Zoom)
        {
            float z = Zoom;
            int srcW = (int)(_vw / z);
            int srcH = (int)(_vh / z);
            SetStretchBltMode(dc, HALFTONE);
            StretchBlt(dc, 0, 0, _vw, _vh, _frameDc, _viewX, _viewY, srcW, srcH, SRCCOPY);
        }
        else
        {
            // Draw / Break: show the frozen frame 1:1
            BitBlt(dc, 0, 0, _vw, _vh, _frameDc, 0, 0, SRCCOPY);
            if (_mode == ZoomItMode.Break)
            {
                // genuinely dim the frozen desktop with a ~75% black alpha-blend so the countdown pops
                DimFrame(dc, 190);
            }
        }
    }

    /// <summary>用黑色 alpha 蓋住成個畫面（alpha 0–255） · Alpha-blend solid black over the whole frame.</summary>
    private static void DimFrame(IntPtr dc, byte alpha)
    {
        var mem = CreateCompatibleDC(dc);
        var bmp = CreateCompatibleBitmap(dc, 1, 1);
        var old = SelectObject(mem, bmp);
        var black = new RECT { Left = 0, Top = 0, Right = 1, Bottom = 1 };
        var brush = CreateSolidBrush(0x000000);
        FillRect(mem, ref black, brush);
        DeleteObject(brush);
        var bf = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = alpha, AlphaFormat = 0 };
        AlphaBlend(dc, 0, 0, _vw, _vh, mem, 0, 0, 1, 1, bf);
        SelectObject(mem, old);
        DeleteObject(bmp);
        DeleteDC(mem);
    }

    private static POINT SourceToWindow(POINT src)
    {
        if (_mode != ZoomItMode.Zoom) return src;
        float z = Zoom;
        return new POINT { X = (int)((src.X - _viewX) * z), Y = (int)((src.Y - _viewY) * z) };
    }

    private static void PaintStrokes(IntPtr dc)
    {
        foreach (var s in _strokes) PaintStroke(dc, s);
        if (_current is not null) PaintStroke(dc, _current);
    }

    private static void PaintStroke(IntPtr dc, Stroke s)
    {
        if (s.Points.Count == 0) return;
        // pen width scales with zoom so it looks consistent on the magnified frame
        int w = _mode == ZoomItMode.Zoom ? Math.Max(1, (int)(s.Width * Zoom)) : s.Width;
        uint penBgr = ToBgr(s.Color);
        var pen = CreatePen(PS_SOLID, w, penBgr);
        var oldPen = SelectObject(dc, pen);
        var oldBrush = SelectObject(dc, GetStockObject(NULL_BRUSH));

        switch (s.Shape)
        {
            case ZoomItPenShape.Freehand:
            case ZoomItPenShape.Highlighter:
            {
                var p0 = SourceToWindow(s.Points[0]);
                MoveToEx(dc, p0.X, p0.Y, IntPtr.Zero);
                for (int i = 1; i < s.Points.Count; i++)
                {
                    var p = SourceToWindow(s.Points[i]);
                    LineTo(dc, p.X, p.Y);
                }
                break;
            }
            case ZoomItPenShape.Rectangle:
            {
                if (s.Points.Count >= 2)
                {
                    var a = SourceToWindow(s.Points[0]);
                    var b = SourceToWindow(s.Points[1]);
                    Rectangle(dc, Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
                }
                break;
            }
            case ZoomItPenShape.Arrow:
            {
                if (s.Points.Count >= 2)
                {
                    var a = SourceToWindow(s.Points[0]);
                    var b = SourceToWindow(s.Points[1]);
                    MoveToEx(dc, a.X, a.Y, IntPtr.Zero);
                    LineTo(dc, b.X, b.Y);
                    DrawArrowHead(dc, a, b, penBgr, w);
                }
                break;
            }
        }

        SelectObject(dc, oldPen);
        SelectObject(dc, oldBrush);
        DeleteObject(pen);
    }

    private static void DrawArrowHead(IntPtr dc, POINT a, POINT b, uint penBgr, int w)
    {
        double ang = Math.Atan2(b.Y - a.Y, b.X - a.X);
        double len = 10 + w * 2.5;
        double spread = Math.PI / 7;
        var p1 = new POINT { X = (int)(b.X - len * Math.Cos(ang - spread)), Y = (int)(b.Y - len * Math.Sin(ang - spread)) };
        var p2 = new POINT { X = (int)(b.X - len * Math.Cos(ang + spread)), Y = (int)(b.Y - len * Math.Sin(ang + spread)) };
        var brush = CreateSolidBrush(penBgr);
        var oldBrush = SelectObject(dc, brush);
        var pts = new[] { b, p1, p2 };
        Polygon(dc, pts, 3);
        SelectObject(dc, oldBrush);
        DeleteObject(brush);
    }

    private static void PaintBreak(IntPtr dc)
    {
        int total = _breakRemaining;
        string txt = $"{total / 60:00}:{total % 60:00}";
        int fontH = Math.Min(_vw, _vh) / 4;
        var lf = new LOGFONT
        {
            lfHeight = -fontH,
            lfWeight = 700,
            lfCharSet = 1, // DEFAULT_CHARSET
            lfFaceName = "Segoe UI",
        };
        var font = CreateFontIndirect(ref lf);
        var oldFont = SelectObject(dc, font);
        SetBkMode(dc, TRANSPARENT);
        GetTextExtentPoint32(dc, txt, txt.Length, out var sz);
        int tx = (_vw - sz.X) / 2;
        int ty = (_vh - sz.Y) / 2;
        // soft shadow then bright text
        SetTextColor(dc, ToBgr(0x000000));
        TextOut(dc, tx + 4, ty + 4, txt, txt.Length);
        SetTextColor(dc, ToBgr(0xFFFFFF));
        TextOut(dc, tx, ty, txt, txt.Length);
        SelectObject(dc, oldFont);
        DeleteObject(font);
    }

    private static void PaintHud(IntPtr dc)
    {
        string hint = _mode switch
        {
            ZoomItMode.Zoom => Loc.I.Pick(
                "Move mouse to pan · Wheel / +/- to zoom · Drag to draw · r/g/b/o/y colour · E pen K box A arrow H highlight · Esc clears/exits",
                "郁滑鼠平移 · 滾輪／+/- 縮放 · 拖曳畫畫 · r/g/b/o/y 顏色 · E 筆 K 框 A 箭咀 H 螢光 · Esc 清除／離開"),
            ZoomItMode.Draw => Loc.I.Pick(
                "Drag to draw · Wheel changes thickness · r/g/b/o/y colour · E pen K box A arrow H highlight · Esc clears/exits",
                "拖曳畫畫 · 滾輪調粗幼 · r/g/b/o/y 顏色 · E 筆 K 框 A 箭咀 H 螢光 · Esc 清除／離開"),
            _ => Loc.I.Pick("Break time · Esc to end", "小休時間 · 按 Esc 結束"),
        };

        var lf = new LOGFONT { lfHeight = -18, lfWeight = 600, lfCharSet = 1, lfFaceName = "Segoe UI" };
        var font = CreateFontIndirect(ref lf);
        var oldFont = SelectObject(dc, font);
        SetBkMode(dc, TRANSPARENT);
        GetTextExtentPoint32(dc, hint, hint.Length, out var sz);

        int pad = 12;
        int barH = sz.Y + pad;
        var bar = new RECT { Left = 0, Top = _vh - barH, Right = _vw, Bottom = _vh };
        var bg = CreateSolidBrush(ToBgr(0x101010));
        FillRect(dc, ref bar, bg);
        DeleteObject(bg);

        SetTextColor(dc, ToBgr(0xF0F0F0));
        TextOut(dc, 16, _vh - barH + pad / 2, hint, hint.Length);

        if (_mode != ZoomItMode.Break)
        {
            // colour/width swatch on the right
            string info = _mode == ZoomItMode.Zoom
                ? Loc.I.Pick($"Zoom {Zoom:0.##}x · pen {_penWidth}px", $"放大 {Zoom:0.##}x · 筆 {_penWidth}px")
                : Loc.I.Pick($"Pen {_penWidth}px", $"筆 {_penWidth}px");
            GetTextExtentPoint32(dc, info, info.Length, out var isz);
            int swatch = sz.Y;
            int rx = _vw - isz.X - swatch - 28;
            var sw = new RECT { Left = rx, Top = _vh - barH + pad / 2, Right = rx + swatch, Bottom = _vh - barH + pad / 2 + swatch };
            var swBrush = CreateSolidBrush(ToBgr(_penColor));
            FillRect(dc, ref sw, swBrush);
            DeleteObject(swBrush);
            SetTextColor(dc, ToBgr(0xF0F0F0));
            TextOut(dc, rx + swatch + 8, _vh - barH + pad / 2, info, info.Length);
        }

        SelectObject(dc, oldFont);
        DeleteObject(font);
    }

    /// <summary>0xRRGGBB → GDI 嘅 0x00BBGGRR · Convert an 0xRRGGBB colour to GDI's 0x00BBGGRR.</summary>
    private static uint ToBgr(int rgb)
    {
        uint r = (uint)((rgb >> 16) & 0xFF);
        uint g = (uint)((rgb >> 8) & 0xFF);
        uint b = (uint)(rgb & 0xFF);
        return (b << 16) | (g << 8) | r;
    }

    private static int LoWord(IntPtr v) => unchecked((short)(v.ToInt64() & 0xFFFF));
    private static int HiWord(IntPtr v) => unchecked((short)((v.ToInt64() >> 16) & 0xFFFF));

    // ====================== chord helpers for the control page ======================

    /// <summary>把修飾鍵 + VK 變返友善文字 · Friendly text for a modifiers+VK chord (e.g. "Ctrl + 1").</summary>
    public static string ChordText(uint mods, uint vk)
    {
        var parts = new List<string>();
        var m = (HotMod)mods;
        if (m.HasFlag(HotMod.Control)) parts.Add("Ctrl");
        if (m.HasFlag(HotMod.Alt)) parts.Add("Alt");
        if (m.HasFlag(HotMod.Shift)) parts.Add("Shift");
        if (m.HasFlag(HotMod.Win)) parts.Add("Win");
        parts.Add(VkName(vk));
        return string.Join(" + ", parts);
    }

    private static string VkName(uint vk) => vk switch
    {
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),       // 0-9
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),       // A-Z
        >= 0x70 and <= 0x7B => $"F{vk - 0x70 + 1}",          // F1-F12
        0x20 => "Space",
        _ => $"0x{vk:X2}",
    };
}
