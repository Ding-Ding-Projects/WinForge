using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 螢幕區域選擇覆蓋層 · A transparent full-virtual-desktop overlay that lets the user drag a rectangle to
/// pick a screen region. Pure Win32 (a layered, topmost window with a dimmed backdrop and a live
/// selection box), so it works over Explorer/Start and across every monitor — no WinUI window
/// transparency quirks. Returns the chosen rectangle in physical screen pixels, or null if cancelled.
/// </summary>
public static class RegionSelector
{
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
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr inst, int id);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool FillRect(IntPtr hDC, ref RECT rc, IntPtr brush);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr CreatePen(int style, int width, uint color);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int obj);
    [DllImport("gdi32.dll")] private static extern bool Rectangle(IntPtr hdc, int l, int t, int r, int b);

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_POPUP = 0x80000000;
    private const int SW_SHOW = 5;
    private const uint LWA_ALPHA = 0x2;
    private const uint WM_DESTROY = 0x0002, WM_PAINT = 0x000F, WM_KEYDOWN = 0x0100;
    private const uint WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_MOUSEMOVE = 0x0200, WM_RBUTTONDOWN = 0x0204;
    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    private const int IDC_CROSS = 32515;
    private const int NULL_BRUSH = 5, PS_SOLID = 0;
    private const int VK_ESCAPE = 0x1B;

    // selection state (window-local coords; window origin == virtual-screen origin)
    private static int _vx, _vy;                 // virtual-screen origin
    private static bool _dragging;
    private static int _sx, _sy, _cx, _cy;       // start / current point
    private static bool _have;
    private static (int x, int y, int w, int h)? _result;
    private static IntPtr _hwnd;
    private static WndProc? _proc;               // keep alive
    private static IntPtr _dimBrush;

    /// <summary>
    /// 同步顯示覆蓋層、等用家拖一個框 · Show the overlay and block until the user drags a rectangle (or
    /// presses Esc / right-clicks to cancel). Returns the rect in PHYSICAL pixels, or null if cancelled.
    /// Must be called on a thread that can pump messages; the UI thread is fine.
    /// </summary>
    public static (int x, int y, int w, int h)? PickRegion()
    {
        _dragging = false; _have = false; _result = null;
        _vx = GetSystemMetrics(SM_XVIRTUALSCREEN);
        _vy = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int vw = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int vh = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (vw <= 0 || vh <= 0) return null;

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
            lpszClassName = "WinForgeRegionSelector",
        };
        RegisterClassEx(ref cls); // safe to call again across invocations

        _dimBrush = CreateSolidBrush(0x00000000); // black; window alpha provides the dim
        _hwnd = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW,
            "WinForgeRegionSelector", null, WS_POPUP,
            _vx, _vy, vw, vh, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero) { Cleanup(); return null; }

        SetLayeredWindowAttributes(_hwnd, 0, 110, LWA_ALPHA); // ~43% dim
        ShowWindow(_hwnd, SW_SHOW);
        UpdateWindow(_hwnd);
        SetForegroundWindow(_hwnd);

        // local modal message loop — wrap so an unexpected fault in a paint/pointer callback
        // cancels the pick cleanly instead of crashing the process.
        try
        {
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
                if (_hwnd == IntPtr.Zero) break;
            }
        }
        catch (Exception)
        {
            _result = null;
        }

        Cleanup();
        return _result;
    }

    /// <summary>非同步版本 · Async-shaped wrapper so callers can await the (synchronous, UI-thread) pick.</summary>
    public static Task<(int x, int y, int w, int h)?> PickRegionAsync()
    {
        var tcs = new TaskCompletionSource<(int x, int y, int w, int h)?>();
        try { tcs.SetResult(PickRegion()); }
        catch (Exception) { tcs.SetResult(null); }
        return tcs.Task;
    }

    private static void Cleanup()
    {
        if (_hwnd != IntPtr.Zero) { DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
        if (_dimBrush != IntPtr.Zero) { DeleteObject(_dimBrush); _dimBrush = IntPtr.Zero; }
        _proc = null;
    }

    private static IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_LBUTTONDOWN:
                _dragging = true; _have = true;
                _sx = _cx = LoWord(lParam); _sy = _cy = HiWord(lParam);
                SetCapture(hWnd);
                return IntPtr.Zero;

            case WM_MOUSEMOVE:
                if (_dragging)
                {
                    _cx = LoWord(lParam); _cy = HiWord(lParam);
                    InvalidateRect(hWnd, IntPtr.Zero, true);
                }
                return IntPtr.Zero;

            case WM_LBUTTONUP:
                if (_dragging)
                {
                    _dragging = false;
                    ReleaseCapture();
                    Finish();
                }
                return IntPtr.Zero;

            case WM_RBUTTONDOWN:
                _result = null;
                PostQuitMessage(0);
                return IntPtr.Zero;

            case WM_KEYDOWN:
                if ((int)wParam == VK_ESCAPE) { _result = null; PostQuitMessage(0); }
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

    private static void Finish()
    {
        int x = Math.Min(_sx, _cx), y = Math.Min(_sy, _cy);
        int w = Math.Abs(_cx - _sx), h = Math.Abs(_cy - _sy);
        // A click-without-drag (or a sub-minimum drag) must never yield a 0-size region — cancel cleanly.
        if (w < 4 || h < 4) { _result = null; PostQuitMessage(0); return; }
        // ffmpeg gdigrab needs even dimensions for yuv420p
        w -= w % 2; h -= h % 2;
        // even-rounding must not collapse the region to zero either
        if (w <= 0 || h <= 0) { _result = null; PostQuitMessage(0); return; }
        // translate window-local → physical virtual-screen coords
        _result = (x + _vx, y + _vy, w, h);
        PostQuitMessage(0);
    }

    private static void OnPaint(IntPtr hWnd)
    {
        var hdc = BeginPaint(hWnd, out var ps);
        try
        {
            var full = ps.rcPaint;
            FillRect(hdc, ref full, _dimBrush); // layered alpha makes this a dimming veil

            // Only draw the selection box while the overlay is live and the rect is non-degenerate;
            // never deref drag state once the window is finishing/closed.
            if (_have && _hwnd != IntPtr.Zero)
            {
                int x = Math.Min(_sx, _cx), y = Math.Min(_sy, _cy);
                int r = Math.Max(_sx, _cx), b = Math.Max(_sy, _cy);
                if (r > x && b > y)
                {
                var pen = CreatePen(PS_SOLID, 2, 0x0000FFFF); // BGR: bright yellow outline
                var oldPen = SelectObject(hdc, pen);
                var oldBrush = SelectObject(hdc, GetStockObject(NULL_BRUSH));
                Rectangle(hdc, x, y, r, b);
                SelectObject(hdc, oldPen);
                SelectObject(hdc, oldBrush);
                DeleteObject(pen);
                }
            }
        }
        catch (Exception) { /* never throw across the native DispatchMessage boundary */ }
        finally { EndPaint(hWnd, ref ps); }
    }

    private static int LoWord(IntPtr v) => unchecked((short)(v.ToInt64() & 0xFFFF));
    private static int HiWord(IntPtr v) => unchecked((short)((v.ToInt64() >> 16) & 0xFFFF));
}
