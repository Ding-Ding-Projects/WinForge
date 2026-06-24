using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 應用程式內滑鼠設定（即時生效）· In-app mouse settings applied live via SystemParametersInfo,
/// persisted with SPIF_UPDATEINIFILE. Replaces the ms-settings:mousetouchpad redirect.
/// </summary>
public static class MouseSettings
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint a, uint u, IntPtr pv, uint f);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint a, uint u, ref int pv, uint f);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint a, uint u, int[] pv, uint f);
    [DllImport("user32.dll")] private static extern bool SwapMouseButton(bool swap);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern bool SetDoubleClickTime(uint ms);
    [DllImport("user32.dll")] private static extern uint GetDoubleClickTime();

    private const uint SPIF = 0x03; // SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE
    private const uint SPI_GETMOUSESPEED = 0x0070, SPI_SETMOUSESPEED = 0x0071;
    private const uint SPI_GETMOUSE = 0x0003, SPI_SETMOUSE = 0x0004;
    private const uint SPI_GETWHEELSCROLLLINES = 0x0068, SPI_SETWHEELSCROLLLINES = 0x0069;
    private const uint SPI_GETMOUSEVANISH = 0x1000, SPI_SETMOUSEVANISH = 0x1001;
    private const uint SPI_GETSNAPTODEFBUTTON = 0x005E, SPI_SETSNAPTODEFBUTTON = 0x005F;
    private const int SM_SWAPBUTTON = 23;

    // Swap primary/secondary buttons
    public static bool GetSwap() => GetSystemMetrics(SM_SWAPBUTTON) != 0;
    public static void SetSwap(bool on)
    {
        SwapMouseButton(on);
        try { RegistryHelper.SetValue(RegRoot.HKCU, @"Control Panel\Mouse", "SwapMouseButtons", on ? "1" : "0", RegistryValueKind.String); } catch { }
    }

    // Pointer speed 1..20 (default 10)
    public static int GetSpeed() { int v = 10; SystemParametersInfo(SPI_GETMOUSESPEED, 0, ref v, 0); return v; }
    public static void SetSpeed(int v) => SystemParametersInfo(SPI_SETMOUSESPEED, 0, new IntPtr(Math.Clamp(v, 1, 20)), SPIF);

    // Enhance pointer precision (acceleration)
    public static bool GetAccel() { var a = new int[3]; SystemParametersInfo(SPI_GETMOUSE, 0, a, 0); return a[2] != 0; }
    public static void SetAccel(bool on) => SystemParametersInfo(SPI_SETMOUSE, 0, on ? new[] { 6, 10, 1 } : new[] { 0, 0, 0 }, SPIF);

    // Wheel scroll lines (1..100; -1 = one screen)
    public static int GetWheelLines() { int v = 3; SystemParametersInfo(SPI_GETWHEELSCROLLLINES, 0, ref v, 0); return v; }
    public static void SetWheelLines(int v) => SystemParametersInfo(SPI_SETWHEELSCROLLLINES, (uint)Math.Clamp(v, 1, 100), IntPtr.Zero, SPIF);

    // Double-click time (ms)
    public static int GetDoubleClick() => (int)GetDoubleClickTime();
    public static void SetDoubleClick(int ms)
    {
        ms = Math.Clamp(ms, 100, 900);
        SetDoubleClickTime((uint)ms);
        try { RegistryHelper.SetValue(RegRoot.HKCU, @"Control Panel\Mouse", "DoubleClickSpeed", ms.ToString(), RegistryValueKind.String); } catch { }
    }

    // Hide pointer while typing
    public static bool GetVanish() { int v = 0; SystemParametersInfo(SPI_GETMOUSEVANISH, 0, ref v, 0); return v != 0; }
    public static void SetVanish(bool on) => SystemParametersInfo(SPI_SETMOUSEVANISH, 0, new IntPtr(on ? 1 : 0), SPIF);

    // Snap to default button in dialogs
    public static bool GetSnap() { int v = 0; SystemParametersInfo(SPI_GETSNAPTODEFBUTTON, 0, ref v, 0); return v != 0; }
    public static void SetSnap(bool on) => SystemParametersInfo(SPI_SETSNAPTODEFBUTTON, (uint)(on ? 1 : 0), IntPtr.Zero, SPIF);
}
