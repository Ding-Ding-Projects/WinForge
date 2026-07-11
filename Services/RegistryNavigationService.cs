using System;

namespace WinForge.Services;

/// <summary>
/// 登錄檔路徑導覽 · Parses full registry paths and hands one requested location to the in-app editor.
/// The handoff is deliberately in-process so Command Palette never needs to launch regedit.exe.
/// </summary>
public readonly record struct RegistryLocation(RegRoot Root, string Path);

public static class RegistryNavigationService
{
    private static readonly object Gate = new();
    private static RegistryLocation? _pending;

    /// <summary>Request one location for the next Registry Editor activation.</summary>
    public static void Request(RegistryLocation location)
    {
        lock (Gate) _pending = location;
    }

    /// <summary>Consume the one pending location, if Command Palette supplied one.</summary>
    public static RegistryLocation? ConsumeRequest()
    {
        lock (Gate)
        {
            var location = _pending;
            _pending = null;
            return location;
        }
    }

    /// <summary>
    /// Accepts HKCU/HKLM/HKCR/HKU and their long HKEY_* names, with an optional key path.
    /// PowerShell-style <c>HKCU:</c> roots and forward slashes are accepted for paste convenience.
    /// </summary>
    public static bool TryParse(string? text, out RegistryLocation location)
    {
        location = default;
        string source = (text ?? "").Trim().Replace('/', '\\');
        if (source.Length == 0) return false;

        int slash = source.IndexOf('\\');
        string rootText = (slash >= 0 ? source[..slash] : source).Trim().TrimEnd(':');
        string path = slash >= 0 ? source[(slash + 1)..].Trim('\\') : "";
        if (!TryParseRoot(rootText, out var root)) return false;

        location = new RegistryLocation(root, path);
        return true;
    }

    /// <summary>Formats a location using canonical long hive names for display and copying.</summary>
    public static string Format(RegistryLocation location)
    {
        string root = location.Root switch
        {
            RegRoot.HKCU => "HKEY_CURRENT_USER",
            RegRoot.HKLM => "HKEY_LOCAL_MACHINE",
            RegRoot.HKCR => "HKEY_CLASSES_ROOT",
            RegRoot.HKU => "HKEY_USERS",
            _ => location.Root.ToString(),
        };
        return string.IsNullOrWhiteSpace(location.Path) ? root : root + "\\" + location.Path;
    }

    private static bool TryParseRoot(string rootText, out RegRoot root)
    {
        switch (rootText.Trim().ToUpperInvariant())
        {
            case "HKCU":
            case "HKEY_CURRENT_USER":
                root = RegRoot.HKCU;
                return true;
            case "HKLM":
            case "HKEY_LOCAL_MACHINE":
                root = RegRoot.HKLM;
                return true;
            case "HKCR":
            case "HKEY_CLASSES_ROOT":
                root = RegRoot.HKCR;
                return true;
            case "HKU":
            case "HKEY_USERS":
                root = RegRoot.HKU;
                return true;
            default:
                root = default;
                return false;
        }
    }
}
