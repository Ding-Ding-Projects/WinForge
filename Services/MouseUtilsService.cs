using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 滑鼠工具核心服務（PowerToys Mouse Utilities 原生複製）· Native clone of PowerToys Mouse Utilities.
/// One service that owns global low-level mouse + keyboard hooks and four GDI overlay windows
/// (per-pixel-alpha layered, topmost, click-through), driving:
///   • Find My Mouse — double-tap Left Ctrl (or shake) → dim screen + spotlight ring round the cursor.
///   • Mouse Highlighter — hotkey toggles; left/right click draws a fading colored circle.
///   • Mouse Crosshairs — hotkey toggles full-length horizontal+vertical lines through the cursor.
///   • Mouse Jump — hotkey shows a shrunken screenshot of all displays; click to teleport the cursor.
///   • Grab and Move — modifier-drag moves a window; modifier-right-drag resizes it from a nearby edge.
///
/// All coordinates are PHYSICAL virtual-screen pixels (the low-level hooks report physical pixels and
/// the process is PerMonitorV2-aware), so overlays line up across mixed-DPI multi-monitor setups.
/// Hooks, overlay windows, and the 60 Hz paint timer run on a private STA message-pump thread, so
/// expensive full-screen GDI work cannot starve the WinUI dispatcher.
/// </summary>
public static class MouseUtilsService
{
    // ---------------------------------------------------------------- P/Invoke
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential)] private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

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
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)] private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsZoomed(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("shell32.dll")] private static extern int SHQueryUserNotificationState(out int state);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string? windowName,
        uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr id, uint elapse, IntPtr func);
    [DllImport("user32.dll")] private static extern bool KillTimer(IntPtr hWnd, IntPtr id);
    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hWnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, uint dwFlags);

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

    private const int WH_MOUSE_LL = 14, WH_KEYBOARD_LL = 13;
    private const int WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205, WM_MBUTTONDOWN = 0x0207;
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const int WM_DESTROY = 0x0002, WM_TIMER = 0x0113;
    private const int VK_LCONTROL = 0xA2, VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_MENU = 0x12;
    private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;
    private const uint WS_EX_LAYERED = 0x00080000, WS_EX_TRANSPARENT = 0x00000020, WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
    private const uint WS_POPUP = 0x80000000;
    private const int SW_SHOW = 5, SW_HIDE = 0;
    private const uint ULW_ALPHA = 0x02;
    private const byte AC_SRC_OVER = 0x00, AC_SRC_ALPHA = 0x01;
    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    private const uint GA_ROOT = 2;
    private const uint SWP_NOACTIVATE = 0x0010, SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004;
    private const uint SRCCOPY = 0x00CC0020;
    private const int HALFTONE = 4, BI_RGB = 0;
    private const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;
    private static readonly IntPtr HWND_TOPMOST = new(-1);

    // ---------------------------------------------------------------- state
    private static IntPtr _mouseHook = IntPtr.Zero, _keyHook = IntPtr.Zero;
    private static HookProc? _mouseProc, _keyProc;     // keep alive while hooked
    private static WndProc? _wndProc;                  // keep alive while windows exist
    private static readonly NativeMessagePump Pump = new("WinForge-MouseUtils");
    private static bool _classRegistered;
    private const string ClassName = "WinForgeMouseUtilsOverlay";
    private static readonly Dictionary<char, string[]> GeometryGlyphs = new()
    {
        ['0'] = new[] { "###", "#.#", "#.#", "#.#", "###" },
        ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
        ['2'] = new[] { "###", "..#", "###", "#..", "###" },
        ['3'] = new[] { "###", "..#", ".##", "..#", "###" },
        ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" },
        ['5'] = new[] { "###", "#..", "###", "..#", "###" },
        ['6'] = new[] { "###", "#..", "###", "#.#", "###" },
        ['7'] = new[] { "###", "..#", ".#.", ".#.", ".#." },
        ['8'] = new[] { "###", "#.#", "###", "#.#", "###" },
        ['9'] = new[] { "###", "#.#", "###", "..#", "###" },
        ['-'] = new[] { "...", "...", "###", "...", "..." },
        [','] = new[] { "...", "...", "...", ".#.", "#.." },
        ['x'] = new[] { "...", "#.#", ".#.", "#.#", "..." },
        [' '] = new[] { "...", "...", "...", "...", "..." },
    };

    // virtual-screen geometry (physical px)
    private static int _vx, _vy, _vw, _vh;

    /// <summary>有冇任何工具開咗 · True if any utility is enabled (hooks installed).</summary>
    public static bool IsRunning => _mouseHook != IntPtr.Zero;

    // ============================================================ Find My Mouse
    public static class FindMyMouse
    {
        public static bool Enabled;
        public static int ActivationMethod;            // 0 = double Ctrl, 1 = shake
        public static int SpotlightRadius = 100;       // physical px
        public static byte BackdropOpacity = 200;      // 0..255 dim alpha
        public static uint SpotlightColor = 0xFFFFFF;  // RRGGBB ring/fill colour
        public static uint BackgroundColor = 0x000000; // RRGGBB dim colour
        public static int FadeMs = 400;                // appear/disappear fade

        // runtime
        internal static bool Active;
        internal static double Alpha;                  // 0..1 fade level
        internal static POINT Pos;
        internal static ulong LastCtrlTapTick;
        internal static bool CtrlDownPrev;
        // shake detection
        internal static readonly List<(POINT p, ulong t)> ShakeHistory = new();
    }

    // ============================================================ Mouse Highlighter
    public static class Highlighter
    {
        public static bool Enabled;
        public static int Radius = 30;                 // physical px
        public static byte Opacity = 160;              // 0..255
        public static uint LeftColor = 0xFFFF00;       // yellow
        public static uint RightColor = 0x0000FF;      // blue
        public static int FadeMs = 500;

        internal static bool Highlighting;             // toggled by hotkey
        internal static readonly List<Splash> Splashes = new();
        internal struct Splash { public POINT pos; public uint color; public ulong start; }
    }

    // ============================================================ Mouse Crosshairs
    public static class Crosshairs
    {
        public static bool Enabled;
        public static int Thickness = 5;               // physical px
        public static byte Opacity = 190;              // 0..255
        public static uint Color = 0xFF0000;           // red
        public static int Radius = 20;                 // gap radius around cursor (0 = full)

        internal static bool Showing;                  // toggled by hotkey
        internal static POINT Pos;
    }

    // ============================================================ Mouse Jump
    public static class MouseJump
    {
        public static bool Enabled;
        internal static bool Showing;
        internal static IntPtr Bitmap = IntPtr.Zero;   // captured virtual-screen DIB (compatible)
        internal static IntPtr MemDc = IntPtr.Zero;
        internal static int PreviewX, PreviewY, PreviewW, PreviewH; // overlay rect (physical px, centred)
        internal static double Scale = 1;
    }

    // ============================================================ Grab and Move
    public static class GrabAndMove
    {
        public static bool Enabled;
        public static int ActivationModifier;          // 0 = Alt, 1 = Windows key
        public static bool ResizeWithRightDrag = true;
        public static bool SuppressAltMenu = true;
        public static bool PauseWhenFullscreenGame = true;
        public static bool ShowGeometry = true;
        public static string ExcludedApps = string.Empty;

        internal static bool Active;
        internal static bool Resizing;
        internal static bool ResizeLeft, ResizeRight, ResizeTop, ResizeBottom;
        internal static bool Changed;
        internal static bool SuppressNextAltUp;
        internal static IntPtr Target;
        internal static POINT StartPointer;
        internal static RECT StartRect, CurrentRect;
    }

    // overlay windows (one per utility, created lazily)
    private static IntPtr _hwndFmm, _hwndHi, _hwndCross, _hwndJump, _hwndGrab;

    // ---------------------------------------------------------------- persistence
    /// <summary>由設定檔載入所有滑鼠工具設定 · Load every mouse-utility setting from the JSON store.</summary>
    public static void LoadSettings()
    {
        int GI(string k, int d) => int.TryParse(SettingsStore.Get(k, d.ToString()), out var v) ? v : d;
        bool GB(string k, bool d) => SettingsStore.Get(k, d ? "1" : "0") == "1";
        uint GC(string k, uint d) => uint.TryParse(SettingsStore.Get(k, d.ToString()), out var v) ? v : d;

        FindMyMouse.Enabled = GB("mouseutils.fmm.enabled", false);
        FindMyMouse.ActivationMethod = GI("mouseutils.fmm.activation", 0);
        FindMyMouse.SpotlightRadius = GI("mouseutils.fmm.radius", 100);
        FindMyMouse.BackdropOpacity = (byte)Math.Clamp(GI("mouseutils.fmm.backdrop", 200), 0, 255);
        FindMyMouse.SpotlightColor = GC("mouseutils.fmm.spotcolor", 0xFFFFFF);
        FindMyMouse.BackgroundColor = GC("mouseutils.fmm.bgcolor", 0x000000);
        FindMyMouse.FadeMs = GI("mouseutils.fmm.fade", 400);

        Highlighter.Enabled = GB("mouseutils.hi.enabled", false);
        Highlighter.Radius = GI("mouseutils.hi.radius", 30);
        Highlighter.Opacity = (byte)Math.Clamp(GI("mouseutils.hi.opacity", 160), 0, 255);
        Highlighter.LeftColor = GC("mouseutils.hi.left", 0xFFFF00);
        Highlighter.RightColor = GC("mouseutils.hi.right", 0x0000FF);
        Highlighter.FadeMs = GI("mouseutils.hi.fade", 500);

        Crosshairs.Enabled = GB("mouseutils.cross.enabled", false);
        Crosshairs.Thickness = GI("mouseutils.cross.thickness", 5);
        Crosshairs.Opacity = (byte)Math.Clamp(GI("mouseutils.cross.opacity", 190), 0, 255);
        Crosshairs.Color = GC("mouseutils.cross.color", 0xFF0000);
        Crosshairs.Radius = GI("mouseutils.cross.radius", 20);

        MouseJump.Enabled = GB("mouseutils.jump.enabled", false);

        GrabAndMove.Enabled = GB("mouseutils.grab.enabled", false);
        GrabAndMove.ActivationModifier = Math.Clamp(GI("mouseutils.grab.modifier", 0), 0, 1);
        GrabAndMove.ResizeWithRightDrag = GB("mouseutils.grab.resize", true);
        GrabAndMove.SuppressAltMenu = GB("mouseutils.grab.suppressalt", true);
        GrabAndMove.PauseWhenFullscreenGame = GB("mouseutils.grab.pausefullscreen", true);
        GrabAndMove.ShowGeometry = GB("mouseutils.grab.geometry", true);
        GrabAndMove.ExcludedApps = SettingsStore.Get("mouseutils.grab.excluded", string.Empty);
    }

    /// <summary>Loads settings and reconciles the private hook/overlay pump at application startup.</summary>
    public static void LoadAndSync()
    {
        LoadSettings();
        Sync();
    }

    /// <summary>寫返所有滑鼠工具設定入設定檔 · Persist every mouse-utility setting back to the JSON store.</summary>
    public static void SaveSettings()
    {
        SettingsStore.Set("mouseutils.fmm.enabled", FindMyMouse.Enabled ? "1" : "0");
        SettingsStore.Set("mouseutils.fmm.activation", FindMyMouse.ActivationMethod.ToString());
        SettingsStore.Set("mouseutils.fmm.radius", FindMyMouse.SpotlightRadius.ToString());
        SettingsStore.Set("mouseutils.fmm.backdrop", FindMyMouse.BackdropOpacity.ToString());
        SettingsStore.Set("mouseutils.fmm.spotcolor", FindMyMouse.SpotlightColor.ToString());
        SettingsStore.Set("mouseutils.fmm.bgcolor", FindMyMouse.BackgroundColor.ToString());
        SettingsStore.Set("mouseutils.fmm.fade", FindMyMouse.FadeMs.ToString());

        SettingsStore.Set("mouseutils.hi.enabled", Highlighter.Enabled ? "1" : "0");
        SettingsStore.Set("mouseutils.hi.radius", Highlighter.Radius.ToString());
        SettingsStore.Set("mouseutils.hi.opacity", Highlighter.Opacity.ToString());
        SettingsStore.Set("mouseutils.hi.left", Highlighter.LeftColor.ToString());
        SettingsStore.Set("mouseutils.hi.right", Highlighter.RightColor.ToString());
        SettingsStore.Set("mouseutils.hi.fade", Highlighter.FadeMs.ToString());

        SettingsStore.Set("mouseutils.cross.enabled", Crosshairs.Enabled ? "1" : "0");
        SettingsStore.Set("mouseutils.cross.thickness", Crosshairs.Thickness.ToString());
        SettingsStore.Set("mouseutils.cross.opacity", Crosshairs.Opacity.ToString());
        SettingsStore.Set("mouseutils.cross.color", Crosshairs.Color.ToString());
        SettingsStore.Set("mouseutils.cross.radius", Crosshairs.Radius.ToString());

        SettingsStore.Set("mouseutils.jump.enabled", MouseJump.Enabled ? "1" : "0");

        SettingsStore.Set("mouseutils.grab.enabled", GrabAndMove.Enabled ? "1" : "0");
        SettingsStore.Set("mouseutils.grab.modifier", GrabAndMove.ActivationModifier.ToString());
        SettingsStore.Set("mouseutils.grab.resize", GrabAndMove.ResizeWithRightDrag ? "1" : "0");
        SettingsStore.Set("mouseutils.grab.suppressalt", GrabAndMove.SuppressAltMenu ? "1" : "0");
        SettingsStore.Set("mouseutils.grab.pausefullscreen", GrabAndMove.PauseWhenFullscreenGame ? "1" : "0");
        SettingsStore.Set("mouseutils.grab.geometry", GrabAndMove.ShowGeometry ? "1" : "0");
        SettingsStore.Set("mouseutils.grab.excluded", GrabAndMove.ExcludedApps ?? string.Empty);
    }

    // ---------------------------------------------------------------- lifecycle
    /// <summary>
    /// 按目前各工具開關，裝／拆全域掛鈎同覆蓋層 · Reconcile hooks &amp; overlays with the current Enabled flags.
    /// Call after changing any FindMyMouse/Highlighter/Crosshairs/MouseJump.Enabled. Idempotent.
    /// </summary>
    public static void Sync()
    {
        Pump.Post(() =>
        {
            bool any = FindMyMouse.Enabled || Highlighter.Enabled || MouseJump.Enabled || Crosshairs.Enabled || GrabAndMove.Enabled;
            if (any) Start(); else Stop();
        });
    }

    private static void Start()
    {
        EnsureGeometry();
        EnsureClass();
        if (_mouseHook == IntPtr.Zero)
        {
            _mouseProc = MouseHookProc;
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
        }
        if (_keyHook == IntPtr.Zero)
        {
            _keyProc = KeyHookProc;
            _keyHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyProc, GetModuleHandle(null), 0);
        }
        EnsureOverlays();
    }

    private static void Stop()
    {
        if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        if (_keyHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyHook); _keyHook = IntPtr.Zero; }
        _mouseProc = null; _keyProc = null;
        // hide all visuals
        FindMyMouse.Active = false; FindMyMouse.Alpha = 0;
        Highlighter.Highlighting = false; Highlighter.Splashes.Clear();
        Crosshairs.Showing = false;
        EndGrabAndMove(false);
        if (MouseJump.Showing) HideJump();
        DestroyOverlays();
    }

    private static void EnsureGeometry()
    {
        _vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        _vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        _vw = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN));
        _vh = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN));
    }

    private static void EnsureClass()
    {
        if (_classRegistered) return;
        _wndProc = WindowProc;
        var cls = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = ClassName,
        };
        RegisterClassEx(ref cls);
        _classRegistered = true;
    }

    // A full-virtual-desktop layered click-through overlay; per-pixel alpha via UpdateLayeredWindow.
    private static IntPtr CreateOverlay()
    {
        var inst = GetModuleHandle(null);
        var h = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            ClassName, null, WS_POPUP,
            _vx, _vy, _vw, _vh, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);
        return h;
    }

    private static void EnsureOverlays()
    {
        if (_hwndFmm == IntPtr.Zero) _hwndFmm = CreateOverlay();
        if (_hwndHi == IntPtr.Zero) _hwndHi = CreateOverlay();
        if (_hwndCross == IntPtr.Zero) _hwndCross = CreateOverlay();
        if (_hwndJump == IntPtr.Zero) _hwndJump = CreateOverlay();
        if (_hwndGrab == IntPtr.Zero) _hwndGrab = CreateOverlay();
        // single 60 Hz timer hung off the FMM window drives all redraws
        if (_hwndFmm != IntPtr.Zero) SetTimer(_hwndFmm, new IntPtr(1), 16, IntPtr.Zero);
    }

    private static void DestroyOverlays()
    {
        if (_hwndFmm != IntPtr.Zero) { KillTimer(_hwndFmm, new IntPtr(1)); DestroyWindow(_hwndFmm); _hwndFmm = IntPtr.Zero; }
        if (_hwndHi != IntPtr.Zero) { DestroyWindow(_hwndHi); _hwndHi = IntPtr.Zero; }
        if (_hwndCross != IntPtr.Zero) { DestroyWindow(_hwndCross); _hwndCross = IntPtr.Zero; }
        if (_hwndJump != IntPtr.Zero) { DestroyWindow(_hwndJump); _hwndJump = IntPtr.Zero; }
        if (_hwndGrab != IntPtr.Zero) { DestroyWindow(_hwndGrab); _hwndGrab = IntPtr.Zero; }
        ReleaseJumpBitmap();
    }

    // ---------------------------------------------------------------- window proc
    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TIMER) { try { RenderAll(); } catch { } return IntPtr.Zero; }
        if (msg == WM_DESTROY) return IntPtr.Zero;
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    // ---------------------------------------------------------------- mouse hook
    private static IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int m = (int)wParam;
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int x = data.pt.X, y = data.pt.Y;

            if (TryHandleGrabAndMoveMouse(m, data.pt)) return (IntPtr)1;

            // ---- Mouse Jump teleport (consumes the click while preview is up) ----
            if (MouseJump.Showing && m == WM_LBUTTONDOWN)
            {
                TeleportFromJump(x, y);
                return (IntPtr)1; // swallow
            }
            if (MouseJump.Showing && (m == WM_RBUTTONDOWN || m == WM_MBUTTONDOWN))
            {
                HideJump();
                return (IntPtr)1;
            }

            if (m == WM_MOUSEMOVE)
            {
                Crosshairs.Pos = data.pt;
                // Find My Mouse: any movement fades the spotlight out
                if (FindMyMouse.Active)
                {
                    if (FindMyMouse.ActivationMethod == 0) FindMyMouse.Active = false; // ctrl mode: move dismisses
                }
                if (FindMyMouse.ActivationMethod == 1 && FindMyMouse.Enabled) DetectShake(data.pt);
            }
            else if (m == WM_LBUTTONDOWN || m == WM_RBUTTONDOWN)
            {
                // Find My Mouse: click dismisses
                if (FindMyMouse.Active) FindMyMouse.Active = false;
                // Highlighter splash
                if (Highlighter.Highlighting)
                {
                    uint col = m == WM_LBUTTONDOWN ? Highlighter.LeftColor : Highlighter.RightColor;
                    lock (Highlighter.Splashes)
                        Highlighter.Splashes.Add(new Highlighter.Splash { pos = data.pt, color = col, start = GetTickCount64() });
                }
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    // ---------------------------------------------------------------- keyboard hook
    private static IntPtr KeyHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int m = (int)wParam;
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)data.vkCode;

            if (TryHandleGrabAndMoveKey(m, vk)) return (IntPtr)1;

            if (m == WM_KEYDOWN || m == WM_SYSKEYDOWN)
            {
                // ---- Find My Mouse: double-tap Left Ctrl ----
                if (FindMyMouse.Enabled && FindMyMouse.ActivationMethod == 0 && vk == VK_LCONTROL)
                {
                    if (!FindMyMouse.CtrlDownPrev)
                    {
                        ulong now = GetTickCount64();
                        if (now - FindMyMouse.LastCtrlTapTick <= 500 && FindMyMouse.LastCtrlTapTick != 0)
                        {
                            ActivateFindMyMouse();
                            FindMyMouse.LastCtrlTapTick = 0;
                        }
                        else FindMyMouse.LastCtrlTapTick = now;
                        FindMyMouse.CtrlDownPrev = true;
                    }
                }

                // ---- Hotkeys: Win+Shift+<key> chords (matches PowerToys defaults) ----
                bool win = (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;
                bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                if (win && shift)
                {
                    if (Highlighter.Enabled && vk == 'H') { Highlighter.Highlighting = !Highlighter.Highlighting; if (!Highlighter.Highlighting) lock (Highlighter.Splashes) Highlighter.Splashes.Clear(); }
                    else if (Crosshairs.Enabled && vk == 'X') { Crosshairs.Showing = !Crosshairs.Showing; }
                    else if (MouseJump.Enabled && vk == 'D') { if (MouseJump.Showing) HideJump(); else ShowJump(); }
                }
                // Esc dismisses the Mouse Jump preview
                if (MouseJump.Showing && vk == 0x1B) HideJump();
            }
            else if (m == WM_KEYUP || m == WM_SYSKEYUP)
            {
                if (vk == VK_LCONTROL) FindMyMouse.CtrlDownPrev = false;
            }
        }
        return CallNextHookEx(_keyHook, nCode, wParam, lParam);
    }

    // ---------------------------------------------------------------- Grab and Move
    private static bool TryHandleGrabAndMoveMouse(int message, POINT point)
    {
        if (!GrabAndMove.Enabled) return false;

        if (GrabAndMove.Active)
        {
            int release = GrabAndMove.Resizing ? WM_RBUTTONUP : WM_LBUTTONUP;
            if (message == WM_MOUSEMOVE)
            {
                UpdateGrabAndMove(point);
                return true;
            }
            if (message == release)
            {
                EndGrabAndMove(GrabAndMove.Changed && GrabAndMove.SuppressAltMenu && GrabAndMove.ActivationModifier == 0);
                return true;
            }
            return false;
        }

        bool move = message == WM_LBUTTONDOWN;
        bool resize = message == WM_RBUTTONDOWN && GrabAndMove.ResizeWithRightDrag;
        if ((!move && !resize) || !IsGrabModifierDown()) return false;
        if (GrabAndMove.PauseWhenFullscreenGame && IsFullscreenGameRunning()) return false;
        return BeginGrabAndMove(point, resize);
    }

    private static bool TryHandleGrabAndMoveKey(int message, int virtualKey)
    {
        if (!GrabAndMove.Enabled)
        {
            GrabAndMove.SuppressNextAltUp = false;
            return false;
        }

        if (GrabAndMove.SuppressNextAltUp && GrabAndMove.ActivationModifier == 0 && virtualKey == VK_MENU &&
            (message == WM_KEYUP || message == WM_SYSKEYUP))
        {
            GrabAndMove.SuppressNextAltUp = false;
            return true;
        }
        return false;
    }

    private static bool BeginGrabAndMove(POINT point, bool resize)
    {
        IntPtr target = GetAncestor(WindowFromPoint(point), GA_ROOT);
        if (target == IntPtr.Zero || target == GetDesktopWindow() || target == GetShellWindow() ||
            !IsWindowVisible(target) || IsIconic(target) || IsZoomed(target) || IsExcludedGrabTarget(target))
            return false;
        if (!GetWindowRect(target, out var rect) || rect.Right <= rect.Left || rect.Bottom <= rect.Top)
            return false;

        GrabAndMove.Target = target;
        GrabAndMove.StartPointer = point;
        GrabAndMove.StartRect = rect;
        GrabAndMove.CurrentRect = rect;
        GrabAndMove.Resizing = resize;
        GrabAndMove.Changed = false;
        GrabAndMove.Active = true;

        if (resize) SelectResizeEdges(point, rect);
        return true;
    }

    private static void SelectResizeEdges(POINT point, RECT rect)
    {
        int nearLeft = Math.Abs(point.X - rect.Left);
        int nearRight = Math.Abs(rect.Right - point.X);
        int nearTop = Math.Abs(point.Y - rect.Top);
        int nearBottom = Math.Abs(rect.Bottom - point.Y);
        int horizontalDistance = Math.Min(nearLeft, nearRight);
        int verticalDistance = Math.Min(nearTop, nearBottom);

        bool horizontal = horizontalDistance * 3 < verticalDistance * 2;
        bool vertical = verticalDistance * 3 < horizontalDistance * 2;
        if (!horizontal && !vertical)
        {
            horizontal = horizontalDistance <= verticalDistance;
            vertical = !horizontal;
        }

        GrabAndMove.ResizeLeft = horizontal && nearLeft <= nearRight;
        GrabAndMove.ResizeRight = horizontal && !GrabAndMove.ResizeLeft;
        GrabAndMove.ResizeTop = vertical && nearTop <= nearBottom;
        GrabAndMove.ResizeBottom = vertical && !GrabAndMove.ResizeTop;
    }

    private static void UpdateGrabAndMove(POINT point)
    {
        if (GrabAndMove.Target == IntPtr.Zero) return;

        int dx = point.X - GrabAndMove.StartPointer.X;
        int dy = point.Y - GrabAndMove.StartPointer.Y;
        int left = GrabAndMove.StartRect.Left;
        int top = GrabAndMove.StartRect.Top;
        int right = GrabAndMove.StartRect.Right;
        int bottom = GrabAndMove.StartRect.Bottom;

        if (!GrabAndMove.Resizing)
        {
            left += dx;
            top += dy;
            right += dx;
            bottom += dy;
        }
        else
        {
            if (GrabAndMove.ResizeLeft) left += dx;
            if (GrabAndMove.ResizeRight) right += dx;
            if (GrabAndMove.ResizeTop) top += dy;
            if (GrabAndMove.ResizeBottom) bottom += dy;

            const int minimumWidth = 160;
            const int minimumHeight = 100;
            if (right - left < minimumWidth)
            {
                if (GrabAndMove.ResizeLeft) left = right - minimumWidth;
                else right = left + minimumWidth;
            }
            if (bottom - top < minimumHeight)
            {
                if (GrabAndMove.ResizeTop) top = bottom - minimumHeight;
                else bottom = top + minimumHeight;
            }
        }

        int width = Math.Max(1, right - left);
        int height = Math.Max(1, bottom - top);
        if (SetWindowPos(GrabAndMove.Target, IntPtr.Zero, left, top, width, height, SWP_NOACTIVATE | SWP_NOZORDER))
        {
            GrabAndMove.CurrentRect = new RECT { Left = left, Top = top, Right = right, Bottom = bottom };
            GrabAndMove.Changed |= left != GrabAndMove.StartRect.Left || top != GrabAndMove.StartRect.Top ||
                                   right != GrabAndMove.StartRect.Right || bottom != GrabAndMove.StartRect.Bottom;
        }
    }

    private static void EndGrabAndMove(bool suppressAltMenu)
    {
        GrabAndMove.SuppressNextAltUp = suppressAltMenu;
        GrabAndMove.Active = false;
        GrabAndMove.Resizing = false;
        GrabAndMove.Target = IntPtr.Zero;
        GrabAndMove.ResizeLeft = GrabAndMove.ResizeRight = GrabAndMove.ResizeTop = GrabAndMove.ResizeBottom = false;
        GrabAndMove.Changed = false;
    }

    private static bool IsGrabModifierDown() => GrabAndMove.ActivationModifier == 1
        ? (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0
        : (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;

    private static bool IsFullscreenGameRunning() =>
        SHQueryUserNotificationState(out var state) == 0 && state == QUNS_RUNNING_D3D_FULL_SCREEN;

    private static bool IsExcludedGrabTarget(IntPtr target)
    {
        if (string.IsNullOrWhiteSpace(GrabAndMove.ExcludedApps)) return false;
        GetWindowThreadProcessId(target, out uint processId);
        if (processId == 0) return false;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;
            foreach (string raw in GrabAndMove.ExcludedApps.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = raw.Trim();
                if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) candidate = candidate[..^4];
                if (string.Equals(candidate, processName, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch { }
        return false;
    }

    private static void ActivateFindMyMouse()
    {
        GetCursorPos(out var p);
        FindMyMouse.Pos = p;
        FindMyMouse.Active = true;
        FindMyMouse.Alpha = 0;
    }

    private static void DetectShake(POINT p)
    {
        var hist = FindMyMouse.ShakeHistory;
        ulong now = GetTickCount64();
        hist.Add((p, now));
        hist.RemoveAll(e => now - e.t > 600);
        if (FindMyMouse.Active) return;
        if (hist.Count < 12) return;
        // count direction reversals in X — a "shake" is many quick reversals over a short window
        int reversals = 0; int lastDir = 0; int travel = 0;
        for (int i = 1; i < hist.Count; i++)
        {
            int dx = hist[i].p.X - hist[i - 1].p.X;
            travel += Math.Abs(dx) + Math.Abs(hist[i].p.Y - hist[i - 1].p.Y);
            int dir = Math.Sign(dx);
            if (dir != 0 && lastDir != 0 && dir != lastDir) reversals++;
            if (dir != 0) lastDir = dir;
        }
        if (reversals >= 5 && travel > 600)
        {
            ActivateFindMyMouse();
            hist.Clear();
        }
    }

    // ---------------------------------------------------------------- rendering
    private static void RenderAll()
    {
        RenderFindMyMouse();
        RenderHighlighter();
        RenderCrosshairs();
        RenderGrabAndMove();
        // Mouse Jump preview is static; it is drawn once on ShowJump.
    }

    private static void RenderGrabAndMove()
    {
        if (_hwndGrab == IntPtr.Zero) return;
        if (!GrabAndMove.Enabled || !GrabAndMove.Active || !GrabAndMove.ShowGeometry)
        {
            HideOverlay(_hwndGrab);
            return;
        }

        var rect = GrabAndMove.CurrentRect;
        string geometry = $"{rect.Left},{rect.Top}  {rect.Right - rect.Left}x{rect.Bottom - rect.Top}";
        using var fb = new Framebuffer(_vw, _vh);
        DrawGeometryLabel(fb, rect.Left - _vx + 16, rect.Top - _vy + 16, geometry);
        fb.Present(_hwndGrab, _vx, _vy);
    }

    private static void DrawGeometryLabel(Framebuffer fb, int x, int y, string text)
    {
        const int scale = 2;
        const int padding = 10;
        const int glyphAdvance = 4 * scale;
        int textWidth = Math.Max(1, text.Length * glyphAdvance - scale);
        int labelWidth = textWidth + padding * 2;
        int labelHeight = 5 * scale + padding * 2;
        x = Math.Clamp(x, 0, Math.Max(0, _vw - labelWidth));
        y = Math.Clamp(y, 0, Math.Max(0, _vh - labelHeight));

        FillFramebufferRect(fb, x, y, labelWidth, labelHeight, 12, 16, 24, 230);
        int cursor = x + padding;
        foreach (char c in text)
        {
            DrawGeometryGlyph(fb, cursor, y + padding, c, scale);
            cursor += glyphAdvance;
        }
    }

    private static void DrawGeometryGlyph(Framebuffer fb, int x, int y, char c, int scale)
    {
        if (!GeometryGlyphs.TryGetValue(c, out var rows)) return;
        for (int row = 0; row < rows.Length; row++)
        {
            for (int column = 0; column < rows[row].Length; column++)
            {
                if (rows[row][column] == '#')
                    FillFramebufferRect(fb, x + column * scale, y + row * scale, scale, scale, 245, 247, 250, 255);
            }
        }
    }

    private static void FillFramebufferRect(Framebuffer fb, int x, int y, int width, int height, byte red, byte green, byte blue, byte alpha)
    {
        int left = Math.Max(0, x);
        int top = Math.Max(0, y);
        int right = Math.Min(_vw, x + width);
        int bottom = Math.Min(_vh, y + height);
        for (int py = top; py < bottom; py++)
            for (int px = left; px < right; px++)
                fb.SetPremul((py * _vw + px) * 4, red, green, blue, alpha);
    }

    // ===== Find My Mouse: dim the screen + punch a bright spotlight hole at the cursor =====
    private static void RenderFindMyMouse()
    {
        if (_hwndFmm == IntPtr.Zero) return;
        if (!FindMyMouse.Enabled) { HideOverlay(_hwndFmm); return; }

        // fade alpha towards target
        double target = FindMyMouse.Active ? 1.0 : 0.0;
        double step = 16.0 / Math.Max(1, FindMyMouse.FadeMs);
        if (FindMyMouse.Alpha < target) FindMyMouse.Alpha = Math.Min(target, FindMyMouse.Alpha + step);
        else if (FindMyMouse.Alpha > target) FindMyMouse.Alpha = Math.Max(target, FindMyMouse.Alpha - step);

        if (FindMyMouse.Alpha <= 0.001) { HideOverlay(_hwndFmm); return; }

        // follow cursor while active
        if (FindMyMouse.Active) GetCursorPos(out FindMyMouse.Pos);

        using var fb = new Framebuffer(_vw, _vh);
        int cx = FindMyMouse.Pos.X - _vx, cy = FindMyMouse.Pos.Y - _vy;
        int r = Math.Max(8, FindMyMouse.SpotlightRadius);
        byte dim = (byte)(FindMyMouse.BackdropOpacity * FindMyMouse.Alpha);
        uint bg = FindMyMouse.BackgroundColor, sp = FindMyMouse.SpotlightColor;
        byte br = (byte)(bg >> 16), bgr = (byte)(bg >> 8), bb = (byte)bg;
        byte sr = (byte)(sp >> 16), sg = (byte)(sp >> 8), sb = (byte)sp;

        int r2 = r * r, ring = (r + 3) * (r + 3);
        for (int py = 0; py < _vh; py++)
        {
            int dy = py - cy; int dy2 = dy * dy;
            int rowBase = py * _vw;
            for (int px = 0; px < _vw; px++)
            {
                int dx = px - cx; int dist2 = dx * dx + dy2;
                int idx = (rowBase + px) * 4;
                if (dist2 <= r2)
                {
                    // spotlight fill — semi-transparent tint of the spotlight colour
                    byte a = (byte)(90 * FindMyMouse.Alpha);
                    fb.SetPremul(idx, sr, sg, sb, a);
                }
                else if (dist2 <= ring)
                {
                    // bright ring edge
                    fb.SetPremul(idx, sr, sg, sb, (byte)(220 * FindMyMouse.Alpha));
                }
                else
                {
                    fb.SetPremul(idx, br, bgr, bb, dim);
                }
            }
        }
        fb.Present(_hwndFmm, _vx, _vy);
    }

    // ===== Mouse Highlighter: fading colored circles at each click =====
    private static void RenderHighlighter()
    {
        if (_hwndHi == IntPtr.Zero) return;
        if (!Highlighter.Enabled) { HideOverlay(_hwndHi); return; }

        List<Highlighter.Splash> active;
        ulong now = GetTickCount64();
        lock (Highlighter.Splashes)
        {
            Highlighter.Splashes.RemoveAll(s => now - s.start > (ulong)Highlighter.FadeMs);
            active = new List<Highlighter.Splash>(Highlighter.Splashes);
        }
        if (active.Count == 0) { HideOverlay(_hwndHi); return; }

        using var fb = new Framebuffer(_vw, _vh);
        int rad = Math.Max(4, Highlighter.Radius);
        int rad2 = rad * rad;
        foreach (var s in active)
        {
            double life = 1.0 - (double)(now - s.start) / Highlighter.FadeMs;
            if (life <= 0) continue;
            byte a = (byte)(Highlighter.Opacity * life);
            byte cr = (byte)(s.color >> 16), cg = (byte)(s.color >> 8), cb = (byte)s.color;
            int cx = s.pos.X - _vx, cy = s.pos.Y - _vy;
            int x0 = Math.Max(0, cx - rad), x1 = Math.Min(_vw - 1, cx + rad);
            int y0 = Math.Max(0, cy - rad), y1 = Math.Min(_vh - 1, cy + rad);
            for (int py = y0; py <= y1; py++)
            {
                int dy = py - cy; int dy2 = dy * dy; int rowBase = py * _vw;
                for (int px = x0; px <= x1; px++)
                {
                    int dx = px - cx;
                    if (dx * dx + dy2 <= rad2)
                        fb.BlendPremul((rowBase + px) * 4, cr, cg, cb, a);
                }
            }
        }
        fb.Present(_hwndHi, _vx, _vy);
    }

    // ===== Mouse Crosshairs: full-length lines through the cursor =====
    private static void RenderCrosshairs()
    {
        if (_hwndCross == IntPtr.Zero) return;
        if (!Crosshairs.Enabled || !Crosshairs.Showing) { HideOverlay(_hwndCross); return; }

        if (!GetCursorPos(out var cp)) cp = Crosshairs.Pos;
        Crosshairs.Pos = cp;

        using var fb = new Framebuffer(_vw, _vh);
        int cx = cp.X - _vx, cy = cp.Y - _vy;
        int t = Math.Max(1, Crosshairs.Thickness);
        int gap = Math.Max(0, Crosshairs.Radius);
        byte a = Crosshairs.Opacity;
        byte cr = (byte)(Crosshairs.Color >> 16), cg = (byte)(Crosshairs.Color >> 8), cb = (byte)Crosshairs.Color;
        int half = t / 2;

        // horizontal band
        for (int py = cy - half; py <= cy - half + t - 1; py++)
        {
            if (py < 0 || py >= _vh) continue;
            int rowBase = py * _vw;
            for (int px = 0; px < _vw; px++)
            {
                if (gap > 0 && Math.Abs(px - cx) <= gap && Math.Abs(py - cy) <= gap) continue;
                fb.SetPremul((rowBase + px) * 4, cr, cg, cb, a);
            }
        }
        // vertical band
        for (int px = cx - half; px <= cx - half + t - 1; px++)
        {
            if (px < 0 || px >= _vw) continue;
            for (int py = 0; py < _vh; py++)
            {
                if (gap > 0 && Math.Abs(px - cx) <= gap && Math.Abs(py - cy) <= gap) continue;
                fb.SetPremul((py * _vw + px) * 4, cr, cg, cb, a);
            }
        }
        fb.Present(_hwndCross, _vx, _vy);
    }

    // ===== Mouse Jump: capture all displays, show a shrunken preview, click to teleport =====
    private static void ShowJump()
    {
        if (_hwndJump == IntPtr.Zero) return;
        EnsureGeometry();
        ReleaseJumpBitmap();

        // capture the whole virtual screen into a memory DC
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        var bmi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = _vw, biHeight = -_vh, biPlanes = 1, biBitCount = 32, biCompression = BI_RGB,
        };
        IntPtr bmp = CreateDIBSection(screenDc, ref bmi, 0, out _, IntPtr.Zero, 0);
        IntPtr old = SelectObject(memDc, bmp);
        BitBlt(memDc, 0, 0, _vw, _vh, screenDc, _vx, _vy, SRCCOPY);
        ReleaseDC(IntPtr.Zero, screenDc);
        MouseJump.MemDc = memDc; MouseJump.Bitmap = bmp;
        SelectObject(memDc, old); // keep bmp alive via field; reselect on present

        // compute centred preview rect — fit the virtual screen into ~55% of itself
        double scale = Math.Min(_vw * 0.55 / _vw, _vh * 0.55 / _vh);
        scale = 0.45; // fixed 45% shrink, like PowerToys' default
        MouseJump.Scale = scale;
        MouseJump.PreviewW = (int)(_vw * scale);
        MouseJump.PreviewH = (int)(_vh * scale);
        MouseJump.PreviewX = (_vw - MouseJump.PreviewW) / 2;
        MouseJump.PreviewY = (_vh - MouseJump.PreviewH) / 2;

        DrawJumpPreview();
        MouseJump.Showing = true;
    }

    private static void DrawJumpPreview()
    {
        if (_hwndJump == IntPtr.Zero || MouseJump.MemDc == IntPtr.Zero) return;
        using var fb = new Framebuffer(_vw, _vh);
        // dim backdrop
        for (int i = 0; i < _vw * _vh; i++) fb.SetPremul(i * 4, 0, 0, 0, 150);

        // blit the shrunken capture into the framebuffer's own DC region, then mark those pixels opaque.
        // We render the scaled screenshot via StretchBlt into a temp 32-bpp DIB, then copy into fb.
        IntPtr scrDc = GetDC(IntPtr.Zero);
        IntPtr tmpDc = CreateCompatibleDC(scrDc);
        var bmi = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = MouseJump.PreviewW, biHeight = -MouseJump.PreviewH, biPlanes = 1, biBitCount = 32, biCompression = BI_RGB,
        };
        IntPtr tmpBmp = CreateDIBSection(scrDc, ref bmi, 0, out IntPtr bits, IntPtr.Zero, 0);
        IntPtr oldTmp = SelectObject(tmpDc, tmpBmp);
        SetStretchBltMode(tmpDc, HALFTONE);
        IntPtr oldSrc = SelectObject(MouseJump.MemDc, MouseJump.Bitmap);
        StretchBlt(tmpDc, 0, 0, MouseJump.PreviewW, MouseJump.PreviewH, MouseJump.MemDc, 0, 0, _vw, _vh, SRCCOPY);
        SelectObject(MouseJump.MemDc, oldSrc);
        ReleaseDC(IntPtr.Zero, scrDc);

        // copy scaled pixels into the framebuffer at the centred rect, fully opaque + thin border
        int pw = MouseJump.PreviewW, ph = MouseJump.PreviewH, ox = MouseJump.PreviewX, oy = MouseJump.PreviewY;
        unsafe
        {
            byte* src = (byte*)bits;
            for (int yy = 0; yy < ph; yy++)
            {
                int fy = oy + yy; if (fy < 0 || fy >= _vh) continue;
                for (int xx = 0; xx < pw; xx++)
                {
                    int fx = ox + xx; if (fx < 0 || fx >= _vw) continue;
                    int s = (yy * pw + xx) * 4;
                    byte b = src[s], g = src[s + 1], rr = src[s + 2];
                    bool border = xx < 2 || yy < 2 || xx >= pw - 2 || yy >= ph - 2;
                    if (border) fb.SetPremul((fy * _vw + fx) * 4, 80, 160, 255, 255);
                    else fb.SetPremul((fy * _vw + fx) * 4, rr, g, b, 255);
                }
            }
        }

        SelectObject(tmpDc, oldTmp);
        DeleteObject(tmpBmp);
        DeleteDC(tmpDc);

        fb.Present(_hwndJump, _vx, _vy);
    }

    private static void TeleportFromJump(int physX, int physY)
    {
        // map click (physical px) within the preview rect back to virtual-screen coords
        int lx = physX - _vx, ly = physY - _vy;
        if (lx < MouseJump.PreviewX || lx >= MouseJump.PreviewX + MouseJump.PreviewW ||
            ly < MouseJump.PreviewY || ly >= MouseJump.PreviewY + MouseJump.PreviewH)
        {
            HideJump(); // click outside the map = cancel
            return;
        }
        double fx = (lx - MouseJump.PreviewX) / (double)MouseJump.PreviewW;
        double fy = (ly - MouseJump.PreviewY) / (double)MouseJump.PreviewH;
        int tx = _vx + (int)(fx * _vw);
        int ty = _vy + (int)(fy * _vh);
        HideJump();
        SetCursorPos(tx, ty);
    }

    private static void HideJump()
    {
        MouseJump.Showing = false;
        if (_hwndJump != IntPtr.Zero) HideOverlay(_hwndJump);
        ReleaseJumpBitmap();
    }

    private static void ReleaseJumpBitmap()
    {
        if (MouseJump.MemDc != IntPtr.Zero) { DeleteDC(MouseJump.MemDc); MouseJump.MemDc = IntPtr.Zero; }
        if (MouseJump.Bitmap != IntPtr.Zero) { DeleteObject(MouseJump.Bitmap); MouseJump.Bitmap = IntPtr.Zero; }
    }

    private static void HideOverlay(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;
        ShowWindow(hwnd, SW_HIDE);
    }

    // ---------------------------------------------------------------- framebuffer
    /// <summary>
    /// 一個 32-bpp 預乘 alpha 緩衝，經 UpdateLayeredWindow 呈現 · A premultiplied-alpha 32-bpp framebuffer
    /// backed by a DIB section, presented to a layered window via UpdateLayeredWindow. Disposing frees the DC/bitmap.
    /// </summary>
    private sealed class Framebuffer : IDisposable
    {
        public readonly int W, H;
        private readonly IntPtr _dc, _bmp, _old, _bits;
        public unsafe byte* Bits => (byte*)_bits;

        public Framebuffer(int w, int h)
        {
            W = w; H = h;
            IntPtr screen = GetDC(IntPtr.Zero);
            _dc = CreateCompatibleDC(screen);
            var bmi = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32, biCompression = BI_RGB,
            };
            _bmp = CreateDIBSection(screen, ref bmi, 0, out _bits, IntPtr.Zero, 0);
            _old = SelectObject(_dc, _bmp);
            ReleaseDC(IntPtr.Zero, screen);
            // DIB section starts zeroed → fully transparent
        }

        /// <summary>設定一個像素（覆寫，預乘）· Set a pixel (overwrite) with premultiplied alpha.</summary>
        public unsafe void SetPremul(int byteIndex, byte r, byte g, byte b, byte a)
        {
            byte* p = (byte*)_bits + byteIndex;
            p[0] = (byte)(b * a / 255);
            p[1] = (byte)(g * a / 255);
            p[2] = (byte)(r * a / 255);
            p[3] = a;
        }

        /// <summary>疊加一個像素（source-over，預乘）· Blend a pixel over what's there (source-over, premultiplied).</summary>
        public unsafe void BlendPremul(int byteIndex, byte r, byte g, byte b, byte a)
        {
            byte* p = (byte*)_bits + byteIndex;
            int inv = 255 - a;
            int nb = b * a / 255 + p[0] * inv / 255;
            int ng = g * a / 255 + p[1] * inv / 255;
            int nr = r * a / 255 + p[2] * inv / 255;
            int na = a + p[3] * inv / 255;
            p[0] = (byte)Math.Min(255, nb);
            p[1] = (byte)Math.Min(255, ng);
            p[2] = (byte)Math.Min(255, nr);
            p[3] = (byte)Math.Min(255, na);
        }

        public void Present(IntPtr hwnd, int screenX, int screenY)
        {
            var dst = new POINT { X = screenX, Y = screenY };
            var src = new POINT { X = 0, Y = 0 };
            var size = new SIZE { cx = W, cy = H };
            var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            // make sure it stays topmost and visible
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            ShowWindow(hwnd, SW_SHOW);
            UpdateLayeredWindow(hwnd, IntPtr.Zero, ref dst, ref size, _dc, ref src, 0, ref blend, ULW_ALPHA);
        }

        public void Dispose()
        {
            if (_dc != IntPtr.Zero)
            {
                SelectObject(_dc, _old);
                DeleteObject(_bmp);
                DeleteDC(_dc);
            }
        }
    }
}
