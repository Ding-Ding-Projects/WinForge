using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// Window Walker · 視窗切換器。Enumerates normal visible top-level windows and restores/activates one
/// on demand. This is a managed in-process counterpart to Command Palette's open-window extension.
/// </summary>
public sealed class WindowWalkerItem
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = "";
    public string ProcessName { get; init; } = "";
}

public static class WindowWalkerService
{
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const int SW_RESTORE = 9;

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextLengthW(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int maxCount);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);

    public static IReadOnlyList<WindowWalkerItem> List()
    {
        var windows = new List<WindowWalkerItem>();
        try
        {
            EnumWindows((window, _) =>
            {
                try
                {
                    if (!IsWindowVisible(window)) return true;
                    if ((GetWindowLongPtr(window, GWL_EXSTYLE).ToInt64() & WS_EX_TOOLWINDOW) != 0) return true;
                    int length = GetWindowTextLengthW(window);
                    if (length <= 0 || length > 4096) return true;
                    var buffer = new StringBuilder(length + 1);
                    if (GetWindowTextW(window, buffer, buffer.Capacity) <= 0) return true;
                    string title = buffer.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(title) || string.Equals(title, "Program Manager", StringComparison.OrdinalIgnoreCase)) return true;

                    string processName = "Windows";
                    GetWindowThreadProcessId(window, out uint processId);
                    if (processId != 0)
                    {
                        try
                        {
                            using var process = Process.GetProcessById((int)processId);
                            processName = string.IsNullOrWhiteSpace(process.ProcessName) ? processName : process.ProcessName;
                        }
                        catch { }
                    }
                    windows.Add(new WindowWalkerItem { Handle = window, Title = title, ProcessName = processName });
                }
                catch { }
                return true;
            }, IntPtr.Zero);
        }
        catch { }

        windows.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));
        return windows;
    }

    public static void Activate(IntPtr window)
    {
        try
        {
            if (window == IntPtr.Zero) return;
            if (IsIconic(window)) ShowWindowAsync(window, SW_RESTORE);
            BringWindowToTop(window);
            SetForegroundWindow(window);
        }
        catch { }
    }
}
