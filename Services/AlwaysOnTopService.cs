using System;
using System.Collections.Generic;

namespace WinForge.Services;

/// <summary>
/// 視窗置頂（PowerToys Always On Top 式）· Pin any top-level window above the rest with SetWindowPos
/// HWND_TOPMOST, and keep track of what's pinned so the UI can toggle it back. Reuses WindowManager's
/// P/Invoke. No external tool, no redirect.
/// </summary>
public static class AlwaysOnTopService
{
    private static readonly HashSet<IntPtr> Pinned = new();

    public static bool IsPinned(IntPtr h) => Pinned.Contains(h);

    public static int PinnedCount => Pinned.Count;

    /// <summary>切換置頂並回傳新狀態 · Toggle topmost for a window; returns the new pinned state.</summary>
    public static bool Toggle(IntPtr h)
    {
        if (h == IntPtr.Zero) return false;
        bool pin = !Pinned.Contains(h);
        WindowManager.SetTopMost(h, pin);
        if (pin) Pinned.Add(h); else Pinned.Remove(h);
        return pin;
    }

    public static void Set(IntPtr h, bool pin)
    {
        if (h == IntPtr.Zero) return;
        WindowManager.SetTopMost(h, pin);
        if (pin) Pinned.Add(h); else Pinned.Remove(h);
    }

    /// <summary>全部解除置頂 · Un-pin everything that was pinned through this service.</summary>
    public static void UnpinAll()
    {
        foreach (var h in new List<IntPtr>(Pinned))
            WindowManager.SetTopMost(h, false);
        Pinned.Clear();
    }
}
