using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinForge.Services;

/// <summary>
/// 快速重音符候選彈出視窗 · The Quick Accent candidate popup.
/// 一個無邊框、置頂、唔搶焦點嘅 Win32 分層視窗，用 GDI 畫候選字元。
/// A borderless, topmost, no-activate Win32 layered window that draws the accent candidates with GDI.
/// It runs on its own dedicated UI thread (own message pump) so it never blocks the keyboard hook,
/// and uses WS_EX_NOACTIVATE so showing it never steals focus from the app the user is typing into.
/// </summary>
internal sealed class QuickAccentPopup
{
    // ===================== Win32 interop =====================

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd; public uint message; public IntPtr wParam, lParam;
        public uint time; public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LOGFONT
    {
        public int lfHeight, lfWidth, lfEscapement, lfOrientation, lfWeight;
        public byte lfItalic, lfUnderline, lfStrikeOut, lfCharSet, lfOutPrecision;
        public byte lfClipPrecision, lfQuality, lfPitchAndFamily;
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
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] private static extern bool InvalidateRect(IntPtr hWnd, IntPtr rect, bool erase);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
    [DllImport("user32.dll")] private static extern bool FillRect(IntPtr hDC, ref RECT rc, IntPtr brush);
    [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte alpha, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO info);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateFontIndirect(ref LOGFONT lf);
    [DllImport("gdi32.dll")] private static extern uint SetTextColor(IntPtr hdc, uint color);
    [DllImport("gdi32.dll")] private static extern uint SetBkMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool TextOut(IntPtr hdc, int x, int y, string s, int len);
    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)] private static extern bool GetTextExtentPoint32(IntPtr hdc, string s, int len, out SIZE size);

    [StructLayout(LayoutKind.Sequential)]
    private struct SIZE { public int cx, cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public uint cbSize, flags;
        public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
        public RECT rcCaret;
    }

    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_POPUP = 0x80000000;
    private const int SW_SHOWNOACTIVATE = 4;
    private const int SW_HIDE = 0;
    private const uint LWA_ALPHA = 0x2;
    private const uint WM_PAINT = 0x000F;
    private const uint WM_DESTROY = 0x0002;
    private const uint WM_APP = 0x8000;
    private const uint WM_APP_SHOW = WM_APP + 1;
    private const uint WM_APP_UPDATE = WM_APP + 2;
    private const uint WM_APP_HIDE = WM_APP + 3;
    private const uint WM_APP_QUIT = WM_APP + 4;
    private const int SM_CXSCREEN = 0, SM_CYSCREEN = 1;
    private const int TRANSPARENT_BK = 1;
    private const int DEFAULT_CHARSET = 1;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    // 主題色 (BGR) · Theme colours (GDI uses 0x00BBGGRR).
    private const uint ColBack = 0x002B2B2B;      // dark panel
    private const uint ColBorder = 0x00505050;
    private const uint ColText = 0x00F0F0F0;
    private const uint ColSelBack = 0x00C57A2B;   // accent blue-ish highlight
    private const uint ColSelText = 0x00FFFFFF;

    // ===================== layout constants =====================

    private const int CellW = 44;     // candidate cell width
    private const int CellH = 52;     // candidate cell height
    private const int PadX = 8;
    private const int PadY = 8;
    private const int FontPx = 26;

    // ===================== instance state =====================

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _hwnd = IntPtr.Zero;
    private WndProc? _proc;       // keep delegate alive
    private IntPtr _bgBrush, _borderBrush, _selBrush, _font;

    private readonly object _gate = new();
    private string[] _items = Array.Empty<string>();
    private int _selected = -1;
    private QuickAccentPosition _position = QuickAccentPosition.Caret;

    private static QuickAccentPopup? _instance;
    public static QuickAccentPopup Instance => _instance ??= new QuickAccentPopup();

    /// <summary>啟動彈出視窗線程（idempotent）· Start the popup's dedicated UI thread (idempotent).</summary>
    public void EnsureStarted()
    {
        if (_thread is { IsAlive: true }) return;
        _thread = new Thread(Pump) { IsBackground = true, Name = "WinForge-QuickAccentPopup" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public void Stop()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>顯示候選清單 · Show the popup with the given items and position preference.</summary>
    public void Show(string[] items, QuickAccentPosition position)
    {
        lock (_gate) { _items = items; _selected = -1; _position = position; }
        if (_threadId != 0) PostThreadMessage(_threadId, WM_APP_SHOW, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>更新揀中嘅索引 · Update the selected index (highlight).</summary>
    public void Select(int index)
    {
        lock (_gate) { _selected = index; }
        if (_threadId != 0) PostThreadMessage(_threadId, WM_APP_UPDATE, IntPtr.Zero, IntPtr.Zero);
    }

    public void Hide()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WM_APP_HIDE, IntPtr.Zero, IntPtr.Zero);
    }

    // ===================== thread / window =====================

    private void Pump()
    {
        _threadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // force a message queue on this thread

        var inst = GetModuleHandle(null);
        _proc = WindowProc;
        var cls = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_proc),
            hInstance = inst,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero,
            lpszClassName = "WinForgeQuickAccentPopup",
        };
        RegisterClassEx(ref cls);

        _bgBrush = CreateSolidBrush(ColBack);
        _borderBrush = CreateSolidBrush(ColBorder);
        _selBrush = CreateSolidBrush(ColSelBack);

        var lf = new LOGFONT
        {
            lfHeight = -FontPx,
            lfWeight = 400,
            lfCharSet = DEFAULT_CHARSET,
            lfQuality = 5, // CLEARTYPE_QUALITY
            lfFaceName = "Segoe UI",
        };
        _font = CreateFontIndirect(ref lf);

        _hwnd = CreateWindowEx(
            WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE,
            "WinForgeQuickAccentPopup", null, WS_POPUP,
            0, 0, 10, 10, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);

        if (_hwnd == IntPtr.Zero) { CleanupGdi(); return; }
        SetLayeredWindowAttributes(_hwnd, 0, 245, LWA_ALPHA);

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_APP_QUIT) break;
            if (msg.hwnd == IntPtr.Zero)
            {
                // thread message (not bound to a window) — handle ourselves
                switch (msg.message)
                {
                    case WM_APP_SHOW: DoShow(); break;
                    case WM_APP_UPDATE: InvalidateRect(_hwnd, IntPtr.Zero, false); break;
                    case WM_APP_HIDE: ShowWindow(_hwnd, SW_HIDE); break;
                }
                continue;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hwnd != IntPtr.Zero) { DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
        CleanupGdi();
    }

    private void CleanupGdi()
    {
        if (_bgBrush != IntPtr.Zero) { DeleteObject(_bgBrush); _bgBrush = IntPtr.Zero; }
        if (_borderBrush != IntPtr.Zero) { DeleteObject(_borderBrush); _borderBrush = IntPtr.Zero; }
        if (_selBrush != IntPtr.Zero) { DeleteObject(_selBrush); _selBrush = IntPtr.Zero; }
        if (_font != IntPtr.Zero) { DeleteObject(_font); _font = IntPtr.Zero; }
        _proc = null;
    }

    private void DoShow()
    {
        string[] items;
        QuickAccentPosition pos;
        lock (_gate) { items = _items; pos = _position; }

        int count = Math.Max(items.Length, 1);
        int w = count * CellW + PadX * 2;
        int h = CellH + PadY * 2;

        var (x, y) = ComputePosition(pos, w, h);
        MoveWindow(_hwnd, x, y, w, h, false);
        ShowWindow(_hwnd, SW_SHOWNOACTIVATE);
        InvalidateRect(_hwnd, IntPtr.Zero, true);
        UpdateWindow(_hwnd);
    }

    private (int x, int y) ComputePosition(QuickAccentPosition pos, int w, int h)
    {
        int sw = GetSystemMetrics(SM_CXSCREEN);
        int sh = GetSystemMetrics(SM_CYSCREEN);

        // anchor near the caret when possible, else near the cursor
        int ax, ay;
        if (TryGetCaret(out var cx, out var cy))
        {
            ax = cx; ay = cy;
        }
        else
        {
            GetCursorPos(out var p);
            ax = p.X; ay = p.Y;
        }

        int x, y;
        switch (pos)
        {
            case QuickAccentPosition.Top:
                x = (sw - w) / 2; y = 80;
                break;
            case QuickAccentPosition.Bottom:
                x = (sw - w) / 2; y = sh - h - 120;
                break;
            case QuickAccentPosition.Center:
                x = (sw - w) / 2; y = (sh - h) / 2;
                break;
            default: // Caret
                x = ax - w / 2;
                y = ay - h - 14; // above the caret/cursor
                if (y < 8) y = ay + 28; // not enough room above → below
                break;
        }

        // clamp to the primary work area
        if (x < 8) x = 8;
        if (x + w > sw - 8) x = sw - w - 8;
        if (y < 8) y = 8;
        if (y + h > sh - 8) y = sh - h - 8;
        return (x, y);
    }

    private static bool TryGetCaret(out int x, out int y)
    {
        x = 0; y = 0;
        try
        {
            var info = new GUITHREADINFO { cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>() };
            if (!GetGUIThreadInfo(0, ref info)) return false;
            if (info.hwndCaret == IntPtr.Zero) return false;
            var r = info.rcCaret;
            if (r.Left == 0 && r.Top == 0 && r.Right == 0 && r.Bottom == 0) return false;
            var pt = new POINT { X = r.Left, Y = r.Top };
            if (!ClientToScreen(info.hwndCaret, ref pt)) return false;
            x = pt.X; y = pt.Y;
            return true;
        }
        catch { return false; }
    }

    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT pt);

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_PAINT:
                OnPaint(hWnd);
                return IntPtr.Zero;
            case WM_DESTROY:
                return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void OnPaint(IntPtr hWnd)
    {
        string[] items;
        int sel;
        lock (_gate) { items = _items; sel = _selected; }

        var hdc = BeginPaint(hWnd, out var ps);
        try
        {
            var full = ps.rcPaint;
            // panel background + border
            FillRect(hdc, ref full, _borderBrush);
            var inner = new RECT { Left = full.Left + 1, Top = full.Top + 1, Right = full.Right - 1, Bottom = full.Bottom - 1 };
            FillRect(hdc, ref inner, _bgBrush);

            SetBkMode(hdc, TRANSPARENT_BK);
            var oldFont = SelectObject(hdc, _font);

            for (int i = 0; i < items.Length; i++)
            {
                int cellX = PadX + i * CellW;
                int cellY = PadY;
                var cell = new RECT { Left = cellX, Top = cellY, Right = cellX + CellW - 2, Bottom = cellY + CellH };

                if (i == sel)
                {
                    FillRect(hdc, ref cell, _selBrush);
                    SetTextColor(hdc, ColSelText);
                }
                else
                {
                    SetTextColor(hdc, ColText);
                }

                var s = items[i];
                GetTextExtentPoint32(hdc, s, s.Length, out var size);
                int tx = cellX + (CellW - 2 - size.cx) / 2;
                int ty = cellY + (CellH - size.cy) / 2;
                TextOut(hdc, tx, ty, s, s.Length);
            }

            SelectObject(hdc, oldFont);
        }
        finally
        {
            EndPaint(hWnd, ref ps);
        }
    }
}
