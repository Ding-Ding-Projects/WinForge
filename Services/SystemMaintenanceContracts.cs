using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// Pure validation and command contracts for the guided Windows/maintenance workflows.
/// Keeping this file free of WinUI, registry, and process dependencies lets the focused harness
/// exercise every safety boundary without changing the host PC.
/// </summary>
public static class SystemMaintenanceContracts
{
    public static readonly int[] StorageCadenceDays = { 0, 1, 7, 30 };
    public static readonly int[] RetentionDays = { 0, 1, 14, 30, 60 };
    public static readonly int[] UpdatePauseDays = { 7, 14, 21, 28, 35 };

    private static readonly Regex PublishedDriverPattern =
        new("^oem[0-9]{1,6}\\.inf$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex PackageNamePattern =
        new("^[A-Za-z0-9._-]{1,240}$", RegexOptions.CultureInvariant);

    public static void ValidateStorageSense(StorageSenseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequireAllowed(settings.CadenceDays, StorageCadenceDays, nameof(settings.CadenceDays));
        RequireAllowed(settings.RecycleBinDays, RetentionDays, nameof(settings.RecycleBinDays));
        RequireAllowed(settings.DownloadsDays, RetentionDays, nameof(settings.DownloadsDays));
    }

    public static void ValidateFilterKeys(FilterKeysSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        RequireMilliseconds(settings.DelayBeforeAcceptanceMs, nameof(settings.DelayBeforeAcceptanceMs));
        RequireMilliseconds(settings.AutoRepeatDelayMs, nameof(settings.AutoRepeatDelayMs));
        RequireMilliseconds(settings.AutoRepeatRateMs, nameof(settings.AutoRepeatRateMs));
        RequireMilliseconds(settings.BounceTimeMs, nameof(settings.BounceTimeMs));
    }

    public static uint FilterKeysFlagsWithEnabled(uint existingFlags, bool enabled)
    {
        const uint filterKeysOn = 0x00000001;
        return enabled ? existingFlags | filterKeysOn : existingFlags & ~filterKeysOn;
    }

    public static IReadOnlyList<string> DismExportDefaultAssociationsArguments(string path)
        => new[] { "/Online", "/Export-DefaultAppAssociations:" + RequireXmlPath(path) };

    public static IReadOnlyList<string> DismImportDefaultAssociationsArguments(string path)
        => new[] { "/Online", "/Import-DefaultAppAssociations:" + RequireExistingXmlPath(path) };

    public static IReadOnlyList<string> DriverExportArguments(string publishedName, string folder)
        => new[] { "/export-driver", RequirePublishedDriverName(publishedName), RequireDirectory(folder) };

    public static IReadOnlyList<string> DriverExportAllArguments(string folder)
        => new[] { "/export-driver", "*", RequireDirectory(folder) };

    public static IReadOnlyList<string> DriverRollbackArguments(string publishedName)
        => new[] { "/delete-driver", RequirePublishedDriverName(publishedName), "/uninstall" };

    public static IReadOnlyList<string> DriverRestoreArguments(string folder)
        => new[] { "/add-driver", Path.Combine(RequireDirectory(folder), "*.inf"), "/subdirs", "/install" };

    public static IReadOnlyList<string> ResetBaseArguments()
        => new[] { "/Online", "/Cleanup-Image", "/StartComponentCleanup", "/ResetBase" };

    public static string BuildStoreResetScript(string packageName)
    {
        string safe = RequirePackageName(packageName);
        return "$p = Get-AppxPackage -Name '" + safe + "' -ErrorAction Stop; " +
               "if (-not $p) { throw 'Package not found.' }; $p | Reset-AppxPackage -ErrorAction Stop";
    }

    public static string BuildStoreReregisterScript(string packageName)
    {
        string safe = RequirePackageName(packageName);
        return "$p = Get-AppxPackage -Name '" + safe + "' -ErrorAction Stop; " +
               "if (-not $p) { throw 'Package not found.' }; " +
               "$manifest = Join-Path $p.InstallLocation 'AppXManifest.xml'; " +
               "if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'AppXManifest.xml is missing.' }; " +
               "Add-AppxPackage -DisableDevelopmentMode -Register $manifest -ErrorAction Stop";
    }

    public static UpdatePauseWindow BuildUpdatePauseWindow(DateTimeOffset now, int days)
    {
        RequireAllowed(days, UpdatePauseDays, nameof(days));
        DateTimeOffset start = now.ToUniversalTime();
        return new UpdatePauseWindow(start, start.AddDays(days));
    }

    public static string FormatUpdateTimestamp(DateTimeOffset value)
        => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    public static StartupImpact EstimateStartupImpact(StartupAutorunSource source)
        => source switch
        {
            StartupAutorunSource.Winlogon or StartupAutorunSource.AppInit => StartupImpact.Critical,
            StartupAutorunSource.AutoStartService or StartupAutorunSource.BootTask => StartupImpact.High,
            StartupAutorunSource.Run or StartupAutorunSource.RunOnce or StartupAutorunSource.LogonTask => StartupImpact.Medium,
            _ => StartupImpact.Low,
        };

    public static string RequirePublishedDriverName(string value)
    {
        string candidate = (value ?? string.Empty).Trim();
        if (!PublishedDriverPattern.IsMatch(candidate))
            throw new ArgumentException("Driver package must be a published OEM INF name such as oem42.inf.", nameof(value));
        return candidate.ToLowerInvariant();
    }

    public static string RequirePackageName(string value)
    {
        string candidate = (value ?? string.Empty).Trim();
        if (!PackageNamePattern.IsMatch(candidate))
            throw new ArgumentException("Store package identity contains unsupported characters.", nameof(value));
        return candidate;
    }

    private static string RequireXmlPath(string value)
    {
        string path = RequireRootedPath(value, nameof(value));
        if (!string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Default-app associations must use an .xml file.", nameof(value));
        return path;
    }

    private static string RequireExistingXmlPath(string value)
    {
        string path = RequireXmlPath(value);
        if (!File.Exists(path)) throw new FileNotFoundException("The associations XML file does not exist.", path);
        return path;
    }

    private static string RequireDirectory(string value)
    {
        string path = RequireRootedPath(value, nameof(value));
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Folder not found: {path}");
        return path;
    }

    private static string RequireRootedPath(string value, string parameter)
    {
        string path = (value ?? string.Empty).Trim();
        if (path.Length == 0 || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("A fully qualified local path is required.", parameter);
        return Path.GetFullPath(path);
    }

    private static void RequireAllowed(int value, IEnumerable<int> allowed, string parameter)
    {
        if (!allowed.Contains(value))
            throw new ArgumentOutOfRangeException(parameter, value, "Value is outside the supported Windows policy set.");
    }

    private static void RequireMilliseconds(uint value, string parameter)
    {
        if (value > 20_000)
            throw new ArgumentOutOfRangeException(parameter, value, "Filter Keys timings are bounded to 20 seconds.");
    }
}

public sealed record StorageSenseSettings(bool Enabled, int CadenceDays, int RecycleBinDays, int DownloadsDays);

public sealed record FilterKeysSettings(
    bool Enabled,
    uint DelayBeforeAcceptanceMs,
    uint AutoRepeatDelayMs,
    uint AutoRepeatRateMs,
    uint BounceTimeMs);

public sealed record UpdatePauseWindow(DateTimeOffset Start, DateTimeOffset End);

public enum StartupAutorunSource
{
    Run,
    RunOnce,
    StartupFolder,
    Winlogon,
    AppInit,
    AutoStartService,
    LogonTask,
    BootTask,
}

public enum StartupImpact
{
    Low,
    Medium,
    High,
    Critical,
}
