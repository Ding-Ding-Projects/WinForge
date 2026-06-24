using System;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 螢幕取色（PowerToys Color Picker 式）· Screen colour picker. Reads the pixel under any point via the
/// screen DC, and uses a global low-level mouse hook to capture a click anywhere (suppressing it).
/// The hook callback runs on the UI thread (which owns the message loop), so events are UI-safe.
/// </summary>
public static class ColorPickService
{
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr hdc, int x, int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_RBUTTONDOWN = 0x0204;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr dwExtraInfo; }

    private static IntPtr _hook = IntPtr.Zero;
    private static LowLevelMouseProc? _proc;        // kept alive while hooked
    private static Action<int, int, byte, byte, byte>? _onMove;
    private static Action<int, int, byte, byte, byte>? _onPick;  // x,y,r,g,b
    private static Action? _onCancel;

    public static bool IsPicking => _hook != IntPtr.Zero;

    public static (byte r, byte g, byte b) PixelAt(int x, int y)
    {
        IntPtr dc = GetDC(IntPtr.Zero);
        uint c = GetPixel(dc, x, y);     // COLORREF: 0x00BBGGRR
        ReleaseDC(IntPtr.Zero, dc);
        return ((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF));
    }

    public static POINT Cursor() { GetCursorPos(out var p); return p; }

    /// <summary>Begin picking. onMove fires live while hovering; onPick on left-click; onCancel on right-click.</summary>
    public static void StartPick(Action<int, int, byte, byte, byte> onMove,
                                 Action<int, int, byte, byte, byte> onPick,
                                 Action onCancel)
    {
        if (IsPicking) return;
        _onMove = onMove; _onPick = onPick; _onCancel = onCancel;
        _proc = HookProc;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
    }

    public static void StopPick()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        _proc = null; _onMove = null; _onPick = null; _onCancel = null;
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int x = data.pt.X, y = data.pt.Y;

            if (msg == WM_MOUSEMOVE)
            {
                var (r, g, b) = PixelAt(x, y);
                _onMove?.Invoke(x, y, r, g, b);
            }
            else if (msg == WM_LBUTTONDOWN)
            {
                var (r, g, b) = PixelAt(x, y);
                var cb = _onPick;
                StopPick();
                cb?.Invoke(x, y, r, g, b);
                return (IntPtr)1; // swallow the click
            }
            else if (msg == WM_RBUTTONDOWN)
            {
                var cb = _onCancel;
                StopPick();
                cb?.Invoke();
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    // ---- colour conversions ----
    public static string Hex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    public static string Hsl(byte r, byte g, byte b)
    {
        double rr = r / 255.0, gg = g / 255.0, bb = b / 255.0;
        double max = Math.Max(rr, Math.Max(gg, bb)), min = Math.Min(rr, Math.Min(gg, bb));
        double h = 0, s, l = (max + min) / 2;
        double d = max - min;
        if (d == 0) { h = 0; s = 0; }
        else
        {
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == rr) h = (gg - bb) / d + (gg < bb ? 6 : 0);
            else if (max == gg) h = (bb - rr) / d + 2;
            else h = (rr - gg) / d + 4;
            h /= 6;
        }
        return $"hsl({Math.Round(h * 360)}, {Math.Round(s * 100)}%, {Math.Round(l * 100)}%)";
    }
}
