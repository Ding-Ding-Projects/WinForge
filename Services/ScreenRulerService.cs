using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 螢幕間尺（PowerToys Screen Ruler 式）· On-screen pixel measurement overlay — a native clone of
/// PowerToys' Measure Tool. A borderless, layered, topmost, full-virtual-screen Win32 window paints
/// the live measurement directly with GDI, so it works over Explorer/Start and across every monitor
/// without any WinUI transparency quirks. Modes:
///   • Distance   — click-drag for the straight-line pixel distance + angle.
///   • Horizontal — a horizontal ruler line; counts horizontal pixels.
///   • Vertical   — a vertical ruler line; counts vertical pixels.
///   • Cross      — a live full-screen crosshair following the cursor with px coordinates.
///   • Bounds     — auto-detect the rectangular region under the cursor by sampling screen-pixel
///                  edge contrast (the Measure Tool "Bounds" feature), then click to lock it.
/// Esc or right-click closes. Left-click copies the current measurement to the clipboard.
/// All coordinates are PHYSICAL screen pixels (the process is PerMonitorV2 DPI aware).
/// </summary>
public static class ScreenRulerService
{
    public enum RulerMode { Distance, Horizontal, Vertical, Cross, Bounds }

    // ---- Win32 interop ----
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd; public uint message; public IntPtr wParam, lParam;
        public uint time; public POINT pt;
    }

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
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);
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
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? name);

    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int obj);
    [DllImport("gdi32.dll")] private static extern bool Rectangle(IntPtr hdc, int l, int t, int r, int b);
    [DllImport("gdi32.dll")] private static extern bool MoveToEx(IntPtr hdc, int x, int y, IntPtr lpPoint);
    [DllImport("gdi32.dll")] private static extern bool LineTo(IntPtr hdc, int x, int y);
    [DllImport("gdi32.dll")] private static extern int SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool TextOut(IntPtr hdc, int x, int y, string s, int len);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetTextExtentPoint32(IntPtr hdc, string s, int len, out POINT size);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFont(int h, int w, int esc, int orient, int weight, uint italic,
        uint underline, uint strikeout, uint charset, uint outPrec, uint clipPrec, uint quality,
        uint pitchAndFamily, string face);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr hdc, int x, int y);

    // ---- clipboard ----
    [DllImport("user32.dll")] private static extern bool OpenClipboard(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("user32.dll")] private static extern IntPtr SetClipboardData(uint fmt, IntPtr mem);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll")] private static extern IntPtr GlobalLock(IntPtr mem);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(IntPtr mem);

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_POPUP = 0x80000000;
    private const int SW_SHOW = 5;
    private const uint WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_KEYDOWN = 0x0100;
    private const uint WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_MOUSEMOVE = 0x0200;
    private const uint WM_RBUTTONDOWN = 0x0204, WM_MOUSEWHEEL = 0x020A;
    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    private const int IDC_CROSS = 32515;
    private const int NULL_BRUSH = 5, PS_SOLID = 0;
    private const int VK_ESCAPE = 0x1B, VK_SHIFT = 0x10;
    private const int TRANSPARENT_BK = 1;
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;
    private const int FW_SEMIBOLD = 600;
    private const uint DEFAULT_CHARSET = 1, CLEARTYPE_QUALITY = 5;

    // ---- live state ----
    private static int _vx, _vy, _vw, _vh;          // virtual-screen origin + size (physical px)
    private static RulerMode _mode;
    private static uint _lineColor;                  // BGR (GDI COLORREF)
    private static int _thickness;
    private static bool _dragging;
    private static int _sx, _sy, _cx, _cy;           // start / current point (window-local)
    private static bool _haveStart;
    private static IntPtr _hwnd;
    private static WndProc? _proc;
    private static IntPtr _bgBrush;
    private static IntPtr _font;
    private static IntPtr _smallFont;
    private static byte _tolerance = 30;             // Bounds edge-detect tolerance (per-channel)
    private static RECT _bounds;                     // detected bounds rect (window-local)
    private static bool _haveBounds;
    private static bool _boundsLocked;

    /// <summary>是否正在量度 · True while the overlay is up.</summary>
    public static bool IsActive => _hwnd != IntPtr.Zero;

    /// <summary>
    /// 顯示量度覆蓋層 · Show the measurement overlay for the given mode and block until the user finishes
    /// (Esc / right-click). Must be called on a thread that pumps messages; the UI thread is fine.
    /// </summary>
    /// <param name="mode">Distance / Horizontal / Vertical / Cross / Bounds.</param>
    /// <param name="lineColorBgr">Line colour as a GDI COLORREF (0x00BBGGRR).</param>
    /// <param name="thickness">Line thickness in pixels (1–10).</param>
    public static void Start(RulerMode mode, uint lineColorBgr, int thickness)
    {
        if (IsActive) return;

        _mode = mode;
        _lineColor = lineColorBgr;
        _thickness = Math.Clamp(thickness, 1, 10);
        _dragging = false;
        _haveStart = false;
        _haveBounds = false;
        _boundsLocked = false;

        _vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        _vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        _vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        _vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (_vw <= 0 || _vh <= 0) return;

        var inst = GetModuleHandle(null);
        _proc = WindowProc;
        var cls = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = inst,
            hCursor = LoadCursor(IntPtr.Zero, IDC_CROSS),
            hbrBackground = IntPtr.Zero,
            lpszClassName = "WinForgeScreenRuler",
        };
        RegisterClassEx(ref cls); // safe to re-register across invocations

        // 1×1 transparent veil: colour-key the background so the screen shows through, but our
        // GDI lines/text (which are NOT the key colour) stay fully opaque. We use a near-black
        // key (0x010101) and paint the backdrop with it, then draw on top.
        _bgBrush = CreateSolidBrush(0x00010101);
        _font = CreateFont(-16, 0, 0, 0, FW_SEMIBOLD, 0, 0, 0, DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, "Segoe UI");
        _smallFont = CreateFont(-13, 0, 0, 0, FW_SEMIBOLD, 0, 0, 0, DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, "Consolas");

        _hwnd = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
            "WinForgeScreenRuler", null, WS_POPUP,
            _vx, _vy, _vw, _vh, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero) { Cleanup(); return; }

        // Colour-key transparency: pixels equal to 0x010101 become see-through.
        SetLayeredWindowAttributes(_hwnd, 0x00010101, 0, LWA_COLORKEY);
        ShowWindow(_hwnd, SW_SHOW);
        UpdateWindow(_hwnd);
        SetForegroundWindow(_hwnd);

        // Cross / Horizontal / Vertical / Bounds follow the cursor live without a button press,
        // so seed the current point and start a redraw cadence via mouse-move messages.
        GetCursorPos(out var cp);
        _cx = cp.X - _vx; _cy = cp.Y - _vy;
        InvalidateRect(_hwnd, IntPtr.Zero, true);

        // local modal message loop
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
            if (_hwnd == IntPtr.Zero) break;
        }

        Cleanup();
    }

    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte alpha, uint flags);
    private const uint LWA_COLORKEY = 0x1;

    private static void Cleanup()
    {
        if (_hwnd != IntPtr.Zero) { DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
        if (_bgBrush != IntPtr.Zero) { DeleteObject(_bgBrush); _bgBrush = IntPtr.Zero; }
        if (_font != IntPtr.Zero) { DeleteObject(_font); _font = IntPtr.Zero; }
        if (_smallFont != IntPtr.Zero) { DeleteObject(_smallFont); _smallFont = IntPtr.Zero; }
        _proc = null;
    }

    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_LBUTTONDOWN:
                if (_mode == RulerMode.Distance)
                {
                    _dragging = true; _haveStart = true;
                    _sx = _cx = LoWord(lParam); _sy = _cy = HiWord(lParam);
                    SetCapture(hWnd);
                }
                else if (_mode == RulerMode.Bounds)
                {
                    // First click locks the currently-detected bounds and copies it; second click closes.
                    if (_haveBounds && !_boundsLocked) { _boundsLocked = true; CopyBoundsToClipboard(); InvalidateRect(hWnd, IntPtr.Zero, true); }
                    else PostQuitMessage(0);
                }
                else
                {
                    // Horizontal / Vertical / Cross — a click copies the current measurement, then closes.
                    CopyAxisToClipboard();
                    PostQuitMessage(0);
                }
                return IntPtr.Zero;

            case WM_MOUSEMOVE:
                _cx = LoWord(lParam); _cy = HiWord(lParam);
                if (_mode == RulerMode.Bounds && !_boundsLocked) DetectBoundsUnderCursor();
                InvalidateRect(hWnd, IntPtr.Zero, true);
                return IntPtr.Zero;

            case WM_LBUTTONUP:
                if (_mode == RulerMode.Distance && _dragging)
                {
                    _dragging = false;
                    ReleaseCapture();
                    CopyDistanceToClipboard();
                    // Hold Shift to keep measuring (PowerToys behaviour); otherwise close.
                    if ((GetKeyState(VK_SHIFT) & 0x8000) == 0) PostQuitMessage(0);
                    else InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                return IntPtr.Zero;

            case WM_MOUSEWHEEL:
                if (_mode == RulerMode.Bounds && !_boundsLocked)
                {
                    short delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);
                    int t = _tolerance + (delta > 0 ? 5 : -5);
                    _tolerance = (byte)Math.Clamp(t, 0, 255);
                    DetectBoundsUnderCursor();
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                return IntPtr.Zero;

            case WM_RBUTTONDOWN:
                PostQuitMessage(0);
                return IntPtr.Zero;

            case WM_KEYDOWN:
                if ((int)wParam == VK_ESCAPE) PostQuitMessage(0);
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

    [DllImport("user32.dll")] private static extern short GetKeyState(int vk);

    // ---- Bounds edge detection (samples the live screen, like PowerToys EdgeDetection) ----
    private static void DetectBoundsUnderCursor()
    {
        int px = _cx + _vx, py = _cy + _vy;  // physical screen coords
        IntPtr dc = GetDC(IntPtr.Zero);
        try
        {
            uint start = GetPixel(dc, px, py);
            if (start == 0xFFFFFFFF) { _haveBounds = false; return; }

            int left = ScanEdge(dc, px, py, -1, 0, start);
            int right = ScanEdge(dc, px, py, 1, 0, start);
            int top = ScanEdge(dc, px, py, 0, -1, start);
            int bottom = ScanEdge(dc, px, py, 0, 1, start);

            // window-local
            _bounds = new RECT { Left = left - _vx, Top = top - _vy, Right = right - _vx, Bottom = bottom - _vy };
            _haveBounds = (right - left) >= 2 && (bottom - top) >= 2;
        }
        finally { ReleaseDC(IntPtr.Zero, dc); }
    }

    private static int ScanEdge(IntPtr dc, int x, int y, int dx, int dy, uint startColor)
    {
        int limitMinX = _vx, limitMinY = _vy, limitMaxX = _vx + _vw - 1, limitMaxY = _vy + _vh - 1;
        int cx = x, cy = y;
        while (true)
        {
            int nx = cx + dx, ny = cy + dy;
            if (nx < limitMinX || nx > limitMaxX || ny < limitMinY || ny > limitMaxY)
                return dx != 0 ? cx : cy;
            uint c = GetPixel(dc, nx, ny);
            if (c == 0xFFFFFFFF || !PixelsClose(startColor, c, _tolerance))
                return dx != 0 ? cx : cy;
            cx = nx; cy = ny;
        }
    }

    private static bool PixelsClose(uint a, uint b, byte tol)
    {
        int ar = (int)(a & 0xFF), ag = (int)((a >> 8) & 0xFF), ab = (int)((a >> 16) & 0xFF);
        int br = (int)(b & 0xFF), bg = (int)((b >> 8) & 0xFF), bb = (int)((b >> 16) & 0xFF);
        return Math.Abs(ar - br) <= tol && Math.Abs(ag - bg) <= tol && Math.Abs(ab - bb) <= tol;
    }

    private static void OnPaint(IntPtr hWnd)
    {
        var hdc = BeginPaint(hWnd, out var ps);
        try
        {
            // Paint the whole window with the colour-key (=> fully transparent veil).
            var full = new RECT { Left = 0, Top = 0, Right = _vw, Bottom = _vh };
            FillRect(hdc, ref full, _bgBrush);

            SetBkMode(hdc, TRANSPARENT_BK);

            switch (_mode)
            {
                case RulerMode.Distance: PaintDistance(hdc); break;
                case RulerMode.Horizontal: PaintHorizontal(hdc); break;
                case RulerMode.Vertical: PaintVertical(hdc); break;
                case RulerMode.Cross: PaintCross(hdc); break;
                case RulerMode.Bounds: PaintBounds(hdc); break;
            }
        }
        finally { EndPaint(hWnd, ref ps); }
    }

    private static IntPtr UsePen(IntPtr hdc, int style, int width, uint color, out IntPtr old)
    {
        var pen = CreatePen(style, width, color);
        old = SelectObject(hdc, pen);
        return pen;
    }

    private static void DrawLineGdi(IntPtr hdc, int x1, int y1, int x2, int y2)
    {
        MoveToEx(hdc, x1, y1, IntPtr.Zero);
        LineTo(hdc, x2, y2);
    }

    private static void PaintDistance(IntPtr hdc)
    {
        if (!_haveStart) { DrawHint(hdc, "Click-drag to measure distance · 拖曳量度距離"); return; }
        var pen = UsePen(hdc, PS_SOLID, _thickness, _lineColor, out var old);
        DrawLineGdi(hdc, _sx, _sy, _cx, _cy);
        SelectObject(hdc, old); DeleteObject(pen);

        int dx = _cx - _sx, dy = _cy - _sy;
        double dist = Math.Sqrt((double)dx * dx + (double)dy * dy);
        double angle = Math.Atan2(-dy, dx) * 180.0 / Math.PI; // screen y grows downward
        string label = $"{dist:0.0} px   Δx {Math.Abs(dx)}  Δy {Math.Abs(dy)}   {angle:0.0}°";
        DrawLabel(hdc, _cx + 14, _cy + 14, label);
        DrawCoords(hdc, _sx + _vx, _sy + _vy, _cx + _vx, _cy + _vy);
    }

    private static void PaintHorizontal(IntPtr hdc)
    {
        var pen = UsePen(hdc, PS_SOLID, _thickness, _lineColor, out var old);
        DrawLineGdi(hdc, 0, _cy, _vw, _cy);
        SelectObject(hdc, old); DeleteObject(pen);
        DrawLabel(hdc, _cx + 14, _cy + 14, $"{_vw} px wide · 闊 {_vw} px   y = {_cy + _vy}");
    }

    private static void PaintVertical(IntPtr hdc)
    {
        var pen = UsePen(hdc, PS_SOLID, _thickness, _lineColor, out var old);
        DrawLineGdi(hdc, _cx, 0, _cx, _vh);
        SelectObject(hdc, old); DeleteObject(pen);
        DrawLabel(hdc, _cx + 14, _cy + 14, $"{_vh} px tall · 高 {_vh} px   x = {_cx + _vx}");
    }

    private static void PaintCross(IntPtr hdc)
    {
        var pen = UsePen(hdc, PS_SOLID, _thickness, _lineColor, out var old);
        DrawLineGdi(hdc, 0, _cy, _vw, _cy);
        DrawLineGdi(hdc, _cx, 0, _cx, _vh);
        SelectObject(hdc, old); DeleteObject(pen);
        DrawLabel(hdc, _cx + 14, _cy + 14, $"({_cx + _vx}, {_cy + _vy}) px");
    }

    private static void PaintBounds(IntPtr hdc)
    {
        if (!_haveBounds) { DrawHint(hdc, "Hover a region · scroll = tolerance · click to lock · 移到區域上，滾輪調容差，撳一下鎖定"); return; }
        int w = _bounds.Right - _bounds.Left + 1;
        int h = _bounds.Bottom - _bounds.Top + 1;
        var pen = UsePen(hdc, PS_SOLID, Math.Max(2, _thickness), _lineColor, out var old);
        var nb = SelectObject(hdc, GetStockObject(NULL_BRUSH));
        Rectangle(hdc, _bounds.Left, _bounds.Top, _bounds.Right + 1, _bounds.Bottom + 1);
        SelectObject(hdc, nb);
        SelectObject(hdc, old); DeleteObject(pen);

        string lockTxt = _boundsLocked ? "  ✓ copied · 已複製" : "";
        DrawLabel(hdc, _bounds.Left + 6, _bounds.Bottom + 8, $"{w} × {h} px (tol {_tolerance}){lockTxt}");
        DrawCoords(hdc, _bounds.Left + _vx, _bounds.Top + _vy, _bounds.Right + _vx, _bounds.Bottom + _vy);
    }

    // ---- text helpers (drawn with a dark pill behind for legibility on any background) ----
    private static void DrawLabel(IntPtr hdc, int x, int y, string text)
    {
        var oldFont = SelectObject(hdc, _font);
        GetTextExtentPoint32(hdc, text, text.Length, out var sz);
        // clamp into the virtual screen
        if (x + sz.X + 16 > _vw) x = _vw - sz.X - 16;
        if (y + sz.Y + 10 > _vh) y = _vh - sz.Y - 10;
        if (x < 2) x = 2;
        if (y < 2) y = 2;

        var pill = new RECT { Left = x - 6, Top = y - 4, Right = x + sz.X + 6, Bottom = y + sz.Y + 4 };
        var bg = CreateSolidBrush(0x00202020); // dark grey (not the key colour)
        FillRect(hdc, ref pill, bg);
        DeleteObject(bg);
        // thin accent border using the line colour
        var pen = CreatePen(PS_SOLID, 1, _lineColor);
        var op = SelectObject(hdc, pen);
        var nb = SelectObject(hdc, GetStockObject(NULL_BRUSH));
        Rectangle(hdc, pill.Left, pill.Top, pill.Right, pill.Bottom);
        SelectObject(hdc, nb); SelectObject(hdc, op); DeleteObject(pen);

        SetTextColor(hdc, 0x00FFFFFF);
        TextOut(hdc, x, y, text, text.Length);
        SelectObject(hdc, oldFont);
    }

    private static void DrawCoords(IntPtr hdc, int x1, int y1, int x2, int y2)
    {
        var oldFont = SelectObject(hdc, _smallFont);
        SetTextColor(hdc, 0x00C8C8C8);
        string s = $"start ({x1}, {y1})  end ({x2}, {y2})";
        TextOut(hdc, 8, 8, s, s.Length);
        SelectObject(hdc, oldFont);
    }

    private static void DrawHint(IntPtr hdc, string text)
    {
        var oldFont = SelectObject(hdc, _font);
        GetTextExtentPoint32(hdc, text, text.Length, out var sz);
        int x = (_vw - sz.X) / 2, y = 40;
        var pill = new RECT { Left = x - 10, Top = y - 6, Right = x + sz.X + 10, Bottom = y + sz.Y + 6 };
        var bg = CreateSolidBrush(0x00202020);
        FillRect(hdc, ref pill, bg);
        DeleteObject(bg);
        SetTextColor(hdc, 0x00FFFFFF);
        TextOut(hdc, x, y, text, text.Length);
        SelectObject(hdc, oldFont);
    }

    // ---- clipboard ----
    private static void CopyDistanceToClipboard()
    {
        int dx = _cx - _sx, dy = _cy - _sy;
        double dist = Math.Sqrt((double)dx * dx + (double)dy * dy);
        double angle = Math.Atan2(-dy, dx) * 180.0 / Math.PI;
        SetClipboardText(string.Format(CultureInfo.InvariantCulture,
            "Distance {0:0.0} px (Δx {1} px, Δy {2} px), angle {3:0.0}°  start ({4},{5}) end ({6},{7})",
            dist, Math.Abs(dx), Math.Abs(dy), angle,
            _sx + _vx, _sy + _vy, _cx + _vx, _cy + _vy));
    }

    private static void CopyAxisToClipboard()
    {
        string s = _mode switch
        {
            RulerMode.Horizontal => $"Horizontal {_vw} px (y = {_cy + _vy})",
            RulerMode.Vertical => $"Vertical {_vh} px (x = {_cx + _vx})",
            RulerMode.Cross => $"Cursor ({_cx + _vx}, {_cy + _vy}) px",
            _ => "",
        };
        if (s.Length > 0) SetClipboardText(s);
    }

    private static void CopyBoundsToClipboard()
    {
        int w = _bounds.Right - _bounds.Left + 1, h = _bounds.Bottom - _bounds.Top + 1;
        SetClipboardText(string.Format(CultureInfo.InvariantCulture,
            "Bounds {0} × {1} px  at ({2},{3})-({4},{5})",
            w, h, _bounds.Left + _vx, _bounds.Top + _vy, _bounds.Right + _vx, _bounds.Bottom + _vy));
    }

    private static void SetClipboardText(string text)
    {
        if (!OpenClipboard(_hwnd)) return;
        try
        {
            EmptyClipboard();
            var bytes = Encoding.Unicode.GetBytes(text + "\0");
            var mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
            if (mem == IntPtr.Zero) return;
            var ptr = GlobalLock(mem);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            GlobalUnlock(mem);
            SetClipboardData(CF_UNICODETEXT, mem);
        }
        finally { CloseClipboard(); }
    }

    private static int LoWord(IntPtr v) => unchecked((short)(v.ToInt64() & 0xFFFF));
    private static int HiWord(IntPtr v) => unchecked((short)((v.ToInt64() >> 16) & 0xFFFF));
}
