using System.Collections.Generic;
using System.IO;
using System;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>一個開機啟動項目 · One startup entry.</summary>
public sealed class StartupItem
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string Location { get; set; } = "";
    public bool Enabled { get; set; }

    // 啟用／停用狀態存喺邊 · where the enabled/disabled flag lives (StartupApproved).
    public RegRoot ApprovedRoot { get; set; }
    public string ApprovedKey { get; set; } = "";
    public string ApprovedName { get; set; } = "";
    public bool RequiresAdmin { get; set; }

    public string StateText => Enabled ? "Enabled · 已啟用" : "Disabled · 已停用";
}

/// <summary>
/// 應用程式內開機程式管理（取代工作管理員嘅啟動分頁）· In-app startup-apps management (no redirect).
/// 由 Run 機碼同啟動資料夾讀取，用 StartupApproved 二進位值切換。
/// Reads Run keys + Startup folders, toggles via the Explorer StartupApproved blob.
/// </summary>
public static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunWow = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
    private const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string ApprovedRun32 = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
    private const string ApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public static List<StartupItem> List()
    {
        var items = new List<StartupItem>();

        AddRun(items, RegRoot.HKCU, RunKey, RegRoot.HKCU, ApprovedRun, "HKCU Run", false);
        AddRun(items, RegRoot.HKLM, RunKey, RegRoot.HKLM, ApprovedRun, "HKLM Run", true);
        AddRun(items, RegRoot.HKLM, RunWow, RegRoot.HKLM, ApprovedRun32, "HKLM Run (32-bit)", true);

        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup folder");
        AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup folder (all users)");

        items.Sort((a, b) => string.Compare(a.Name, b.Name, System.StringComparison.OrdinalIgnoreCase));
        return items;
    }

    private static void AddRun(List<StartupItem> items, RegRoot root, string key, RegRoot approvedRoot, string approvedKey, string label, bool admin)
    {
        foreach (var (name, _, data) in RegistryHelper.GetValues(root, key))
        {
            if (string.IsNullOrEmpty(name)) continue;
            items.Add(new StartupItem
            {
                Name = name,
                Command = data?.ToString() ?? "",
                Location = label,
                Enabled = ReadEnabled(approvedRoot, approvedKey, name),
                ApprovedRoot = approvedRoot,
                ApprovedKey = approvedKey,
                ApprovedName = name,
                RequiresAdmin = admin,
            });
        }
    }

    private static void AddFolder(List<StartupItem> items, string folder, string label)
    {
        try
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", System.StringComparison.OrdinalIgnoreCase)) continue;
                items.Add(new StartupItem
                {
                    Name = Path.GetFileNameWithoutExtension(file),
                    Command = file,
                    Location = label,
                    Enabled = ReadEnabled(RegRoot.HKCU, ApprovedFolder, fileName),
                    ApprovedRoot = RegRoot.HKCU,
                    ApprovedKey = ApprovedFolder,
                    ApprovedName = fileName,
                    RequiresAdmin = false,
                });
            }
        }
        catch { /* ignore unreadable folder */ }
    }

    private static bool ReadEnabled(RegRoot root, string key, string name)
    {
        var v = RegistryHelper.GetValue(root, key, name);
        // StartupApproved blob: byte[0] even = enabled (0x02), odd = disabled (0x03). Absent = enabled.
        if (v is byte[] b && b.Length > 0) return b[0] % 2 == 0;
        return true;
    }

    /// <summary>切換啟用狀態（寫 StartupApproved 二進位值）· Toggle by writing the StartupApproved blob.</summary>
    public static void SetEnabled(StartupItem item, bool enabled)
    {
        var blob = new byte[12];
        blob[0] = enabled ? (byte)0x02 : (byte)0x03; // rest left as zeros (timestamp optional)
        RegistryHelper.SetValue(item.ApprovedRoot, item.ApprovedKey, item.ApprovedName, blob, RegistryValueKind.Binary);
    }

    // ===== WinTune self-startup (run on login, minimized to the tray) =====

    private const string SelfStartupName = "WinTune";

    private static string SelfExePath()
    {
        try { return Environment.ProcessPath ?? ""; } catch { return ""; }
    }

    /// <summary>WinTune 開機自啟動係咪開咗 · Whether WinTune is set to run at login.</summary>
    public static bool IsSelfStartupEnabled()
    {
        var v = RegistryHelper.GetValue(RegRoot.HKCU, RunKey, SelfStartupName) as string;
        return !string.IsNullOrEmpty(v);
    }

    /// <summary>
    /// 設定 WinTune 開機自啟動（HKCU Run，加 --minimized 收入系統匣）。
    /// Enable/disable launching WinTune at login via HKCU\...\Run, started minimized to the tray.
    /// </summary>
    public static void SetSelfStartup(bool enabled)
    {
        if (enabled)
        {
            var exe = SelfExePath();
            if (string.IsNullOrEmpty(exe)) return;
            RegistryHelper.SetValue(RegRoot.HKCU, RunKey, SelfStartupName,
                $"\"{exe}\" --minimized", RegistryValueKind.String);
        }
        else
        {
            RegistryHelper.DeleteValue(RegRoot.HKCU, RunKey, SelfStartupName);
        }
    }
}
