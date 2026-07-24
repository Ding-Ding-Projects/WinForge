using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Windows-backed execution for the eight reconciled Windows/System and Maintenance workflows.
/// User-originated paths travel through <see cref="ShellRunner.RunArguments"/> so command shells never
/// reinterpret them. Privileged operations fail closed unless WinForge is already elevated.
/// </summary>
public static class SystemMaintenanceService
{
    private const string StoragePolicy = @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy";
    private const string UpdateSettingsPath = @"SOFTWARE\Microsoft\WindowsUpdate\UX\Settings";

    private static readonly string[] UpdatePauseStringValues =
    {
        "PauseUpdatesStartTime",
        "PauseUpdatesExpiryTime",
        "PauseFeatureUpdatesStartTime",
        "PauseFeatureUpdatesEndTime",
        "PauseQualityUpdatesStartTime",
        "PauseQualityUpdatesEndTime",
    };

    private static readonly string[] UpdatePauseDwordValues =
    {
        "PauseFeatureUpdates",
        "PauseQualityUpdates",
    };

    public static StorageSenseSettings ReadStorageSense()
        => new(
            ReadInt(RegRoot.HKCU, StoragePolicy, "01", 0) == 1,
            ReadAllowed(RegRoot.HKCU, StoragePolicy, "2048", SystemMaintenanceContracts.StorageCadenceDays, 0),
            ReadAllowed(RegRoot.HKCU, StoragePolicy, "256", SystemMaintenanceContracts.RetentionDays, 30),
            ReadAllowed(RegRoot.HKCU, StoragePolicy, "512", SystemMaintenanceContracts.RetentionDays, 0));

    public static TweakResult ApplyStorageSense(StorageSenseSettings settings)
    {
        try
        {
            SystemMaintenanceContracts.ValidateStorageSense(settings);
            RegistryHelper.SetValue(RegRoot.HKCU, StoragePolicy, "01", settings.Enabled ? 1 : 0, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, StoragePolicy, "2048", settings.CadenceDays, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, StoragePolicy, "256", settings.RecycleBinDays, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, StoragePolicy, "512", settings.DownloadsDays, RegistryValueKind.DWord);
            return TweakResult.Ok(
                "Storage Sense cadence and retention policies were saved.",
                "儲存空間感知嘅週期同保留政策已儲存。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"儲存空間感知設定失敗：{ex.Message}");
        }
    }

    public static FilterKeysSettings ReadFilterKeys()
    {
        FilterKeysNative native = CreateDefaultFilterKeysNative();
        SystemParametersInfo(GetFilterKeys, native.Size, ref native, 0);
        return new FilterKeysSettings(
            (native.Flags & FilterKeysOn) != 0,
            native.WaitMs,
            native.DelayMs,
            native.RepeatMs,
            native.BounceMs);
    }

    public static TweakResult ApplyFilterKeys(FilterKeysSettings settings)
    {
        try
        {
            SystemMaintenanceContracts.ValidateFilterKeys(settings);
            FilterKeysNative native = CreateDefaultFilterKeysNative();
            if (!SystemParametersInfo(GetFilterKeys, native.Size, ref native, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Windows rejected SPI_GETFILTERKEYS; no accessibility setting was changed.");
            native.Flags = SystemMaintenanceContracts.FilterKeysFlagsWithEnabled(native.Flags, settings.Enabled);
            native.WaitMs = settings.DelayBeforeAcceptanceMs;
            native.DelayMs = settings.AutoRepeatDelayMs;
            native.RepeatMs = settings.AutoRepeatRateMs;
            native.BounceMs = settings.BounceTimeMs;
            if (!SystemParametersInfo(SetFilterKeys, native.Size, ref native, UpdateIniFile | SendChange))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows rejected SPI_SETFILTERKEYS.");

            return TweakResult.Ok(
                "Filter Keys timings were saved and applied to the live session.",
                "篩選鍵時間已儲存，亦即時套用到而家個工作階段。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"套用篩選鍵失敗：{ex.Message}");
        }
    }

    public static Task<TweakResult> ExportDefaultAssociations(string path, CancellationToken ct = default)
        => RunValidated(() => SystemMaintenanceContracts.DismExportDefaultAssociationsArguments(path),
            args => ShellRunner.RunArguments("dism.exe", args, elevated: true, ct: ct));

    public static Task<TweakResult> ImportDefaultAssociations(string path, CancellationToken ct = default)
        => RunValidated(() => SystemMaintenanceContracts.DismImportDefaultAssociationsArguments(path),
            args => ShellRunner.RunArguments("dism.exe", args, elevated: true, ct: ct));

    public static TweakResult PauseWindowsUpdate(int days, DateTimeOffset? now = null)
    {
        try
        {
            UpdatePauseWindow window = SystemMaintenanceContracts.BuildUpdatePauseWindow(now ?? DateTimeOffset.UtcNow, days);
            string start = SystemMaintenanceContracts.FormatUpdateTimestamp(window.Start);
            string end = SystemMaintenanceContracts.FormatUpdateTimestamp(window.End);
            foreach (string name in new[] { "PauseUpdatesStartTime", "PauseFeatureUpdatesStartTime", "PauseQualityUpdatesStartTime" })
                RegistryHelper.SetValue(RegRoot.HKLM, UpdateSettingsPath, name, start, RegistryValueKind.String);
            foreach (string name in new[] { "PauseUpdatesExpiryTime", "PauseFeatureUpdatesEndTime", "PauseQualityUpdatesEndTime" })
                RegistryHelper.SetValue(RegRoot.HKLM, UpdateSettingsPath, name, end, RegistryValueKind.String);
            foreach (string name in UpdatePauseDwordValues)
                RegistryHelper.SetValue(RegRoot.HKLM, UpdateSettingsPath, name, 1, RegistryValueKind.DWord);
            return TweakResult.Ok(
                $"Windows Update is paused until {window.End.LocalDateTime:g}.",
                $"Windows Update 已暫停到 {window.End.LocalDateTime:g}。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"暫停 Windows Update 失敗：{ex.Message}");
        }
    }

    public static TweakResult ResumeWindowsUpdate()
    {
        try
        {
            var failures = new List<string>();
            foreach (string name in UpdatePauseStringValues.Concat(UpdatePauseDwordValues))
            {
                if (RegistryHelper.GetValue(RegRoot.HKLM, UpdateSettingsPath, name) is null) continue;
                RegistryValueDeleteResult result = RegistryHelper.TryDeleteValue(RegRoot.HKLM, UpdateSettingsPath, name);
                if (!result.Success) failures.Add(name + ": " + result.Error?.Message);
            }
            if (failures.Count > 0)
                return TweakResult.Fail("Some Windows Update pause values could not be removed.",
                    "部分 Windows Update 暫停值刪唔到。", string.Join(Environment.NewLine, failures));
            return TweakResult.Ok("Windows Update pause values were removed.", "Windows Update 暫停值已移除。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"恢復 Windows Update 失敗：{ex.Message}");
        }
    }

    public static DateTimeOffset? ReadWindowsUpdatePauseExpiry()
    {
        string? raw = RegistryHelper.GetValue(RegRoot.HKLM, UpdateSettingsPath, "PauseUpdatesExpiryTime")?.ToString();
        return DateTimeOffset.TryParse(raw, out DateTimeOffset parsed) ? parsed : null;
    }

    public static IReadOnlyList<string> ListPublishedDriverPackages()
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
            return Directory.EnumerateFiles(folder, "oem*.inf", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(DriverOrdinal)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static Task<TweakResult> ExportDriver(string publishedName, string folder, CancellationToken ct = default)
        => RunValidated(() => SystemMaintenanceContracts.DriverExportArguments(publishedName, folder),
            args => ShellRunner.RunArguments("pnputil.exe", args, elevated: true, ct: ct));

    public static Task<TweakResult> ExportAllDrivers(string folder, CancellationToken ct = default)
        => RunValidated(() => SystemMaintenanceContracts.DriverExportAllArguments(folder),
            args => ShellRunner.RunArguments("pnputil.exe", args, elevated: true, ct: ct));

    public static Task<TweakResult> RollBackDriver(string publishedName, CancellationToken ct = default)
        => RunValidated(() => SystemMaintenanceContracts.DriverRollbackArguments(publishedName),
            args => ShellRunner.RunArguments("pnputil.exe", args, elevated: true, ct: ct));

    public static Task<TweakResult> RestoreExportedDrivers(string folder, CancellationToken ct = default)
        => RunValidated(() => SystemMaintenanceContracts.DriverRestoreArguments(folder),
            args => ShellRunner.RunArguments("pnputil.exe", args, elevated: true, ct: ct));

    public static async Task<IReadOnlyList<StartupAuditEntry>> AuditStartupAsync(CancellationToken ct = default)
    {
        var entries = await Task.Run(AuditRegistryStartup, ct);
        try
        {
            const string taskScript =
                "$rows = foreach ($t in Get-ScheduledTask -ErrorAction SilentlyContinue) { " +
                "$trigger = @($t.Triggers | Where-Object { $_.CimClass.CimClassName -match 'Logon|Boot' } | Select-Object -First 1); " +
                "if ($trigger.Count -gt 0) { [pscustomobject]@{ TaskName=$t.TaskName; TaskPath=$t.TaskPath; " +
                "Command=(($t.Actions | ForEach-Object { ($_.Execute + ' ' + $_.Arguments).Trim() }) -join '; '); " +
                "Trigger=$trigger[0].CimClass.CimClassName } } }; @($rows) | ConvertTo-Json -Compress";
            string json = await ShellRunner.CapturePowershellJson(taskScript, ct);
            var tasks = JsonSerializer.Deserialize<List<ScheduledAutorunDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<ScheduledAutorunDto>();
            foreach (ScheduledAutorunDto task in tasks)
            {
                StartupAutorunSource source = task.Trigger.Contains("Boot", StringComparison.OrdinalIgnoreCase)
                    ? StartupAutorunSource.BootTask
                    : StartupAutorunSource.LogonTask;
                entries.Add(NewStartupEntry(task.TaskName, task.Command, "Scheduled task " + task.TaskPath, source));
            }
        }
        catch
        {
            // Registry/folder/service evidence remains useful if Task Scheduler is unavailable.
        }

        return entries
            .GroupBy(entry => (entry.Name, entry.Command, entry.Location), StringTupleComparer.Instance)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.Impact)
            .ThenBy(entry => entry.Location, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static Task<TweakResult> ResetComponentBase(CancellationToken ct = default)
        => ShellRunner.RunArguments("dism.exe", SystemMaintenanceContracts.ResetBaseArguments(), elevated: true, ct: ct);

    public static Task<TweakResult> ResetStoreApp(string packageName, CancellationToken ct = default)
        => RunValidated(() => SystemMaintenanceContracts.BuildStoreResetScript(packageName),
            script => ShellRunner.RunPowershell(script, elevated: false, ct));

    public static Task<TweakResult> ReregisterStoreApp(string packageName, CancellationToken ct = default)
        => RunValidated(() => SystemMaintenanceContracts.BuildStoreReregisterScript(packageName),
            script => ShellRunner.RunPowershell(script, elevated: false, ct));

    private static List<StartupAuditEntry> AuditRegistryStartup()
    {
        var entries = new List<StartupAuditEntry>();
        AddRegistryValues(entries, RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU Run", StartupAutorunSource.Run);
        AddRegistryValues(entries, RegRoot.HKLM, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM Run", StartupAutorunSource.Run);
        AddRegistryValues(entries, RegRoot.HKLM, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM Run (32-bit)", StartupAutorunSource.Run);
        AddRegistryValues(entries, RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKCU RunOnce", StartupAutorunSource.RunOnce);
        AddRegistryValues(entries, RegRoot.HKLM, @"Software\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM RunOnce", StartupAutorunSource.RunOnce);
        AddRegistryValues(entries, RegRoot.HKLM, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\RunOnce", "HKLM RunOnce (32-bit)", StartupAutorunSource.RunOnce);

        AddStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup folder");
        AddStartupFolder(entries, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup folder (all users)");

        const string winlogon = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon";
        foreach (string name in new[] { "Shell", "Userinit", "Taskman", "VmApplet" })
        {
            string? value = RegistryHelper.GetValue(RegRoot.HKLM, winlogon, name)?.ToString();
            if (!string.IsNullOrWhiteSpace(value)) entries.Add(NewStartupEntry(name, value, "HKLM Winlogon", StartupAutorunSource.Winlogon));
        }

        foreach (string path in new[]
        {
            @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows NT\CurrentVersion\Windows",
        })
        {
            string? value = RegistryHelper.GetValue(RegRoot.HKLM, path, "AppInit_DLLs")?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                entries.Add(NewStartupEntry("AppInit_DLLs", value, "HKLM " + path, StartupAutorunSource.AppInit));
        }

        foreach (string serviceName in RegistryHelper.GetSubKeyNames(RegRoot.HKLM, @"SYSTEM\CurrentControlSet\Services"))
        {
            string path = @"SYSTEM\CurrentControlSet\Services\" + serviceName;
            if (ReadInt(RegRoot.HKLM, path, "Start", -1) != 2) continue;
            string? image = RegistryHelper.GetValue(RegRoot.HKLM, path, "ImagePath")?.ToString();
            if (!string.IsNullOrWhiteSpace(image))
                entries.Add(NewStartupEntry(serviceName, image, "Automatic service", StartupAutorunSource.AutoStartService));
        }
        return entries;
    }

    private static void AddRegistryValues(List<StartupAuditEntry> entries, RegRoot root, string path, string location,
        StartupAutorunSource source)
    {
        foreach (var (name, _, data) in RegistryHelper.GetValues(root, path))
            if (!string.IsNullOrWhiteSpace(name) && data is not null)
                entries.Add(NewStartupEntry(name, data.ToString() ?? string.Empty, location, source));
    }

    private static void AddStartupFolder(List<StartupAuditEntry> entries, string folder, string location)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            foreach (string file in Directory.EnumerateFiles(folder))
            {
                if (string.Equals(Path.GetFileName(file), "desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                entries.Add(NewStartupEntry(Path.GetFileNameWithoutExtension(file), file, location, StartupAutorunSource.StartupFolder));
            }
        }
        catch { }
    }

    private static StartupAuditEntry NewStartupEntry(string name, string command, string location, StartupAutorunSource source)
    {
        StartupImpact impact = SystemMaintenanceContracts.EstimateStartupImpact(source);
        return new StartupAuditEntry
        {
            Name = name,
            Command = command,
            Location = location,
            Source = source,
            Impact = impact,
            ImpactReason = source switch
            {
                StartupAutorunSource.Winlogon => "Winlogon shell/user-init chain · Winlogon 外殼／登入鏈",
                StartupAutorunSource.AppInit => "DLL injection into GUI processes · DLL 注入 GUI 程序",
                StartupAutorunSource.AutoStartService => "Starts before or around sign-in · 登入前後自動啟動",
                StartupAutorunSource.BootTask => "Boot-triggered scheduled task · 開機觸發排程工作",
                StartupAutorunSource.LogonTask => "Logon-triggered scheduled task · 登入觸發排程工作",
                StartupAutorunSource.RunOnce => "Runs once at sign-in · 登入時執行一次",
                StartupAutorunSource.Run => "Runs at every sign-in · 每次登入都執行",
                _ => "Startup-folder launch · 開機資料夾啟動",
            },
        };
    }

    private static int ReadAllowed(RegRoot root, string path, string name, IEnumerable<int> allowed, int fallback)
    {
        int value = ReadInt(root, path, name, fallback);
        return allowed.Contains(value) ? value : fallback;
    }

    private static int ReadInt(RegRoot root, string path, string name, int fallback)
    {
        object? raw = RegistryHelper.GetValue(root, path, name);
        try { return raw is null ? fallback : Convert.ToInt32(raw); }
        catch { return fallback; }
    }

    private static FilterKeysNative CreateDefaultFilterKeysNative()
        => new()
        {
            Size = checked((uint)Marshal.SizeOf<FilterKeysNative>()),
            Flags = FilterKeysAvailable | FilterKeysHotkeyActive | FilterKeysConfirmHotkey |
                    FilterKeysHotkeySound | FilterKeysIndicator,
            WaitMs = 1000,
            DelayMs = 1000,
            RepeatMs = 500,
            BounceMs = 0,
        };

    private static int DriverOrdinal(string name)
    {
        string digits = new(name.Skip(3).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out int parsed) ? parsed : int.MaxValue;
    }

    private static async Task<TweakResult> RunValidated<T>(Func<T> validate, Func<T, Task<TweakResult>> run)
    {
        try { return await run(validate()); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"驗證失敗：{ex.Message}"); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FilterKeysNative
    {
        public uint Size;
        public uint Flags;
        public uint WaitMs;
        public uint DelayMs;
        public uint RepeatMs;
        public uint BounceMs;
    }

    private const uint GetFilterKeys = 0x0032;
    private const uint SetFilterKeys = 0x0033;
    private const uint FilterKeysOn = 0x00000001;
    private const uint FilterKeysAvailable = 0x00000002;
    private const uint FilterKeysHotkeyActive = 0x00000004;
    private const uint FilterKeysConfirmHotkey = 0x00000008;
    private const uint FilterKeysHotkeySound = 0x00000010;
    private const uint FilterKeysIndicator = 0x00000020;
    private const uint UpdateIniFile = 0x0001;
    private const uint SendChange = 0x0002;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref FilterKeysNative value, uint flags);

    private sealed class ScheduledAutorunDto
    {
        public string TaskName { get; set; } = string.Empty;
        public string TaskPath { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public string Trigger { get; set; } = string.Empty;
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string Name, string Command, string Location)>
    {
        public static readonly StringTupleComparer Instance = new();

        public bool Equals((string Name, string Command, string Location) x,
            (string Name, string Command, string Location) y)
            => string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Command, y.Command, StringComparison.OrdinalIgnoreCase)
               && string.Equals(x.Location, y.Location, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Name, string Command, string Location) obj)
            => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Command ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Location ?? string.Empty));
    }
}

public sealed class StartupAuditEntry
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public StartupAutorunSource Source { get; set; }
    public StartupImpact Impact { get; set; }
    public string ImpactReason { get; set; } = string.Empty;
    public string ImpactText => Impact + " · " + Impact switch
    {
        StartupImpact.Critical => "關鍵",
        StartupImpact.High => "高",
        StartupImpact.Medium => "中",
        _ => "低",
    };
}
