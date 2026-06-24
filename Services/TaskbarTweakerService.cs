using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 偵測同啟動 7+ Taskbar Tweaker 與 Windhawk · Detect and launch 7+ Taskbar Tweaker and Windhawk.
///
/// 7+TT 同 Windhawk 嘅深層行為（中鍵關閉、雙擊顯示桌面、捲動切換、拖放重排）係靠注入 explorer.exe
/// 嘅 DLL 喺執行時改寫工作列嘅視窗程序，C# 託管程式碼根本做唔到，所以呢度只係偵測 +（如已安裝）啟動。
///
/// The deep behaviours of 7+TT / Windhawk (middle-click close, double-click show desktop, scroll-to-switch,
/// drag-reorder) work by injecting a DLL into explorer.exe and rewriting the taskbar's window procedures at
/// runtime — genuinely impossible in managed C#. So this service only detects and (if installed) launches them.
/// We NEVER bundle, download, or auto-install 7+TT (closed-source freeware with no winget id).
/// </summary>
public static class TaskbarTweakerService
{
    /// <summary>偵測結果 · One tool's detection result.</summary>
    public sealed class Detection
    {
        public bool Installed { get; init; }
        public string? ExecutablePath { get; init; }
        public string? Version { get; init; }
    }

    // ---------------- 7+ Taskbar Tweaker ----------------

    /// <summary>偵測 7+ Taskbar Tweaker（解除安裝登錄機碼 + 預設安裝路徑）· Detect 7+TT via uninstall key + default paths.</summary>
    public static Detection Detect7Tt()
    {
        // 1) Uninstall registry keys (both 64- and 32-bit views, HKLM and HKCU).
        var fromReg = ProbeUninstall("7+ Taskbar Tweaker", "7\\+ Taskbar Tweaker");
        if (fromReg is not null) return fromReg;

        // 2) Default install path probe.
        foreach (var dir in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            foreach (var sub in new[] { "7+ Taskbar Tweaker", @"7+ Taskbar Tweaker\7+ Taskbar Tweaker" })
            {
                var exe = Path.Combine(dir, sub, "7+ Taskbar Tweaker.exe");
                if (File.Exists(exe))
                    return new Detection { Installed = true, ExecutablePath = exe };
            }
        }
        return new Detection { Installed = false };
    }

    // ---------------- Windhawk ----------------

    /// <summary>偵測 Windhawk（解除安裝機碼 + 預設路徑）· Detect Windhawk via uninstall key + default path.</summary>
    public static Detection DetectWindhawk()
    {
        var fromReg = ProbeUninstall("Windhawk", "Windhawk");
        if (fromReg is not null) return fromReg;

        foreach (var dir in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        })
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var exe = Path.Combine(dir, "Windhawk", "windhawk.exe");
            if (File.Exists(exe))
                return new Detection { Installed = true, ExecutablePath = exe };
        }
        return new Detection { Installed = false };
    }

    // ---------------- Launch ----------------

    /// <summary>啟動已偵測到嘅工具 · Launch a detected tool. Returns false if the path is missing/unlaunchable.</summary>
    public static bool Launch(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath)) return false;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------- Helpers ----------------

    /// <summary>
    /// 掃描 HKLM/HKCU（64 + 32 位）嘅解除安裝機碼，搵 DisplayName 包含關鍵字嘅項目。
    /// Scan HKLM/HKCU (64- and 32-bit) uninstall keys for an entry whose DisplayName contains the keyword.
    /// </summary>
    private static Detection? ProbeUninstall(string displayNameContains, string _unusedRegex)
    {
        const string uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        const string uninstall32 = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

        foreach (var (hive, path) in new[]
        {
            (RegistryHive.LocalMachine, uninstall),
            (RegistryHive.LocalMachine, uninstall32),
            (RegistryHive.CurrentUser, uninstall),
            (RegistryHive.CurrentUser, uninstall32),
        })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default);
                using var root = baseKey.OpenSubKey(path);
                if (root is null) continue;
                foreach (var subName in root.GetSubKeyNames())
                {
                    using var sub = root.OpenSubKey(subName);
                    if (sub is null) continue;
                    var display = sub.GetValue("DisplayName") as string;
                    if (string.IsNullOrEmpty(display) ||
                        display.IndexOf(displayNameContains, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var version = sub.GetValue("DisplayVersion") as string;
                    var exe = ResolveExe(sub);
                    return new Detection { Installed = true, ExecutablePath = exe, Version = version };
                }
            }
            catch
            {
                // Ignore unreadable hives/keys and keep probing.
            }
        }
        return null;
    }

    /// <summary>由解除安裝機碼推算主程式路徑 · Best-effort resolve the main .exe from an uninstall key.</summary>
    private static string? ResolveExe(RegistryKey sub)
    {
        // DisplayIcon often points straight at the exe ("path,index").
        if (sub.GetValue("DisplayIcon") is string icon && !string.IsNullOrWhiteSpace(icon))
        {
            var p = icon.Split(',')[0].Trim('"', ' ');
            if (p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(p)) return p;
        }

        // Otherwise look inside InstallLocation for a likely exe.
        if (sub.GetValue("InstallLocation") is string loc && !string.IsNullOrWhiteSpace(loc) && Directory.Exists(loc))
        {
            foreach (var name in new[] { "7+ Taskbar Tweaker.exe", "windhawk.exe" })
            {
                var candidate = Path.Combine(loc, name);
                if (File.Exists(candidate)) return candidate;
            }
            try
            {
                foreach (var exe in Directory.GetFiles(loc, "*.exe", SearchOption.TopDirectoryOnly))
                    return exe;
            }
            catch { /* ignore */ }
        }
        return null;
    }
}
