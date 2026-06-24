using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinForge.Services;

/// <summary>一個頂層視窗 · One top-level window.</summary>
public sealed class WinInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = "";
    public string Process { get; init; } = "";
}

public enum Zone
{
    LeftHalf, RightHalf, TopHalf, BottomHalf,
    TopLeft, TopRight, BottomLeft, BottomRight,
    LeftThird, CenterThird, RightThird,
    Maximize, Center, FullArea,
}

/// <summary>
/// 應用程式內視窗管理（純 Win32 P/Invoke）· In-app window manager — list top-level windows and snap
/// the selected one to halves/quarters/thirds, maximise, centre or always-on-top. No external tool.
/// </summary>
public static class WindowManager
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint action, uint uiParam, ref RECT pv, uint winIni);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const uint GW_OWNER = 4;
    private const uint SPI_GETWORKAREA = 0x0030;
    private const int SW_RESTORE = 9, SW_MAXIMIZE = 3;
    private const uint SWP_SHOWWINDOW = 0x0040, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public static List<WinInfo> List()
    {
        var list = new List<WinInfo>();
        EnumWindows((h, _) =>
        {
            try
            {
                if (!IsWindowVisible(h)) return true;
                if (GetWindow(h, GW_OWNER) != IntPtr.Zero) return true; // skip owned/dialog windows
                if (((long)GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return true;
                int len = GetWindowTextLength(h);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(h, sb, sb.Capacity);
                var title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;

                string proc = "";
                try { GetWindowThreadProcessId(h, out var pid); proc = Process.GetProcessById((int)pid).ProcessName; } catch { }
                if (proc.Equals("WinTune", StringComparison.OrdinalIgnoreCase)) return true; // skip ourselves

                list.Add(new WinInfo { Handle = h, Title = title, Process = proc });
            }
            catch { /* skip */ }
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static (int x, int y, int w, int h) WorkArea()
    {
        var r = new RECT();
        if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref r, 0))
            return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        return (0, 0, 1920, 1080);
    }

    public static void Snap(IntPtr h, Zone zone)
    {
        if (h == IntPtr.Zero) return;
        var (ax, ay, aw, ah) = WorkArea();

        if (zone == Zone.Maximize) { ShowWindow(h, SW_MAXIMIZE); SetForegroundWindow(h); return; }
        ShowWindow(h, SW_RESTORE);

        int hw = aw / 2, hh = ah / 2, tw = aw / 3;
        var (x, y, w, hgt) = zone switch
        {
            Zone.LeftHalf => (ax, ay, hw, ah),
            Zone.RightHalf => (ax + hw, ay, aw - hw, ah),
            Zone.TopHalf => (ax, ay, aw, hh),
            Zone.BottomHalf => (ax, ay + hh, aw, ah - hh),
            Zone.TopLeft => (ax, ay, hw, hh),
            Zone.TopRight => (ax + hw, ay, aw - hw, hh),
            Zone.BottomLeft => (ax, ay + hh, hw, ah - hh),
            Zone.BottomRight => (ax + hw, ay + hh, aw - hw, ah - hh),
            Zone.LeftThird => (ax, ay, tw, ah),
            Zone.CenterThird => (ax + tw, ay, tw, ah),
            Zone.RightThird => (ax + 2 * tw, ay, aw - 2 * tw, ah),
            Zone.Center => (ax + aw / 6, ay + ah / 6, aw * 2 / 3, ah * 2 / 3),
            _ => (ax, ay, aw, ah), // FullArea
        };
        SetWindowPos(h, IntPtr.Zero, x, y, w, hgt, SWP_SHOWWINDOW | SWP_NOZORDER);
        SetForegroundWindow(h);
    }

    public static void SetTopMost(IntPtr h, bool topMost)
        => SetWindowPos(h, topMost ? HWND_TOPMOST : HWND_NOTOPMOST, 0, 0, 0, 0, 0x0001 | 0x0002 | SWP_NOACTIVATE); // SWP_NOSIZE|SWP_NOMOVE

    public static void Focus(IntPtr h) { ShowWindow(h, SW_RESTORE); SetForegroundWindow(h); }
}
