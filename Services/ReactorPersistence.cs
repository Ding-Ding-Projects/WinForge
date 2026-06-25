using System;
using System.IO;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 「常駐反應堆」可選持久化 · Opt-in "always-on reactor" persistence.
///
/// ETHICS (mandatory, baked in):
///  • Default OFF (reactor.keepalive defaults to "False").
///  • HKCU\...\Run only — NO admin, NO HKLM. It therefore appears in the in-app Startup Apps module
///    AND in Task Manager → Startup, where the user can disable it in one click.
///  • Transparent name "WinForgeReactor" — never obfuscated.
///  • RemoveAll() is admin-free and complete.
///  • The launcher watchdog respects user deletion as authoritative and never re-creates the entry.
///
/// Reuses the StartupManager registry pattern; no schtasks.exe shell-out (forbidden).
/// </summary>
public static class ReactorPersistence
{
    private const string RunName = "WinForgeReactor";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>用家有冇開咗常駐 · Whether keep-alive is enabled (single source of truth).</summary>
    public static bool Enabled => SettingsStore.Get("reactor.keepalive", "False") == "True";

    /// <summary>登錄檔 Run 項目係咪存在 · Whether the visible Run entry currently exists.</summary>
    public static bool RunEntryPresent =>
        !string.IsNullOrEmpty(RegistryHelper.GetValue(RegRoot.HKCU, RunKey, RunName) as string);

    private static string LauncherPathNextToExe()
    {
        try
        {
            var dir = AppContext.BaseDirectory;
            var launcher = Path.Combine(dir, "WinForgeLauncher.exe");
            if (File.Exists(launcher)) return launcher;
            // Fall back to the main exe with the reactor flag if the launcher isn't deployed alongside.
            return Environment.ProcessPath ?? launcher;
        }
        catch
        {
            return Environment.ProcessPath ?? "WinForgeLauncher.exe";
        }
    }

    public static void SetEnabled(bool on)
    {
        SettingsStore.Set("reactor.keepalive", on ? "True" : "False");
        if (on)
        {
            var launcher = LauncherPathNextToExe();
            // Point the Run value at the crash-resilient launcher with reactor + minimized flags.
            RegistryHelper.SetValue(RegRoot.HKCU, RunKey, RunName,
                $"\"{launcher}\" --reactor --minimized", RegistryValueKind.String);
        }
        else
        {
            RemoveAll();
        }
    }

    /// <summary>移除全部常駐設定（免管理員、完整、可重入）· Remove all persistence (idempotent, admin-free).</summary>
    public static void RemoveAll()
    {
        SettingsStore.Set("reactor.keepalive", "False");
        RegistryHelper.DeleteValue(RegRoot.HKCU, RunKey, RunName);
        // No Task Scheduler task is created by default; nothing else to clean up.
    }

    /// <summary>狀態文字（畀色彩標籤用）· Status for a coloured pill.</summary>
    public static string Status()
    {
        if (Enabled && RunEntryPresent) return "Enabled";
        if (Enabled && !RunEntryPresent) return "Enabled (entry removed)";
        if (!Enabled && RunEntryPresent) return "Disabling…";
        return "Disabled";
    }
}
