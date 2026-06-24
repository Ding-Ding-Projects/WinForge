using System;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 保持喚醒（PowerToys Awake 式）· Keep-awake (PowerToys Awake-style) via SetThreadExecutionState.
/// ES_CONTINUOUS persists the request on the calling (UI) thread until cleared. No redirect.
/// </summary>
public static class AwakeService
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(uint esFlags);

    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    /// <summary>Whether a keep-awake request is currently active (persists across pages).</summary>
    public static bool Active { get; private set; }
    public static bool KeepDisplay { get; private set; }

    /// <summary>Keep the system awake. When <paramref name="keepDisplay"/> is true the screen stays on too.</summary>
    public static bool KeepAwake(bool keepDisplay)
    {
        uint flags = ES_CONTINUOUS | ES_SYSTEM_REQUIRED | (keepDisplay ? ES_DISPLAY_REQUIRED : 0);
        bool ok = SetThreadExecutionState(flags) != 0;
        if (ok) { Active = true; KeepDisplay = keepDisplay; }
        return ok;
    }

    /// <summary>Release the request — Windows may sleep/dim again per the active power plan.</summary>
    public static bool AllowSleep()
    {
        bool ok = SetThreadExecutionState(ES_CONTINUOUS) != 0;
        if (ok) { Active = false; KeepDisplay = false; }
        return ok;
    }
}
