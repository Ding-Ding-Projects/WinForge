using Microsoft.Win32;
using System.Runtime.Versioning;

namespace DewEncryption.Core;

public sealed class DewStartupService
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DewEncryptionDewDrive";

    [SupportedOSPlatformGuard("windows")]
    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsEnabledForCurrentUser()
    {
        if (!IsSupported)
        {
            return false;
        }

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
        return !string.IsNullOrWhiteSpace(key?.GetValue(ValueName)?.ToString());
    }

    public void EnableForCurrentUser(string? executablePath = null)
    {
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("Startup registration is only supported on Windows.");
        }

        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true);
        key.SetValue(ValueName, BuildStartupCommand(executablePath), RegistryValueKind.String);
    }

    public void DisableForCurrentUser()
    {
        if (!IsSupported)
        {
            return;
        }

        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static string BuildStartupCommand(string? executablePath = null)
    {
        string path = string.IsNullOrWhiteSpace(executablePath) ? ResolveExecutablePath() : executablePath;
        return $"\"{path}\" --auto-sync --minimized";
    }

    private static string ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath) && File.Exists(Environment.ProcessPath))
        {
            return Environment.ProcessPath;
        }

        string executableName = OperatingSystem.IsWindows() ? "DewEncryption.Gui.exe" : "DewEncryption.Gui";
        return Path.Combine(AppContext.BaseDirectory, executableName);
    }
}
