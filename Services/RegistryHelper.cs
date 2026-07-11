using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>登錄檔根 · Registry hive roots.</summary>
public enum RegRoot { HKCU, HKLM, HKCR, HKU }

/// <summary>
/// 安全嘅登錄檔讀寫包裝 · Thin, exception-safe wrapper around the Windows registry.
/// 全部用 64-bit view，呢個係 Windows 11 真正改設定嘅地方。
/// Uses the 64-bit view — this is where real Windows 11 settings live.
/// </summary>
public static class RegistryHelper
{
    private static RegistryKey BaseKey(RegRoot root) => root switch
    {
        RegRoot.HKCU => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
        RegRoot.HKLM => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
        RegRoot.HKCR => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64),
        RegRoot.HKU => RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64),
        _ => throw new ArgumentOutOfRangeException(nameof(root)),
    };

    public static object? GetValue(RegRoot root, string path, string name)
    {
        try
        {
            using var bk = BaseKey(root);
            using var key = bk.OpenSubKey(path);
            return key?.GetValue(name);
        }
        catch
        {
            return null;
        }
    }

    public static void SetValue(RegRoot root, string path, string name, object value, RegistryValueKind kind)
    {
        using var bk = BaseKey(root);
        using var key = bk.CreateSubKey(path, writable: true)
            ?? throw new InvalidOperationException($"Cannot open/create {root}\\{path}");
        key.SetValue(name, value, kind);
    }

    public static void DeleteValue(RegRoot root, string path, string name)
    {
        try
        {
            using var bk = BaseKey(root);
            using var key = bk.OpenSubKey(path, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
        catch { /* missing is fine */ }
    }

    /// <summary>
    /// Deletes a registry value while preserving the actual error for an interactive editor.
    /// The legacy fire-and-forget <see cref="DeleteValue"/> remains intentionally best-effort for
    /// catalog toggles that treat an already-absent value as the desired state.
    /// </summary>
    public static bool TryDeleteValue(RegRoot root, string path, string name, out Exception? error)
    {
        try
        {
            using var bk = BaseKey(root);
            using var key = bk.OpenSubKey(path, writable: true);
            if (key is null)
            {
                error = new InvalidOperationException($"Cannot open {root}\\{path} for writing.");
                return false;
            }

            key.DeleteValue(name, throwOnMissingValue: false);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    /// <summary>
    /// 比較現值同期望值（數值會做型別正規化）。
    /// Compares the current value with the expected one (numeric values are normalised).
    /// </summary>
    public static bool ValueEquals(RegRoot root, string path, string name, object expected)
    {
        var v = GetValue(root, path, name);
        if (v is null) return false;

        try
        {
            switch (expected)
            {
                case int ei: return Convert.ToInt64(v) == ei;
                case long el: return Convert.ToInt64(v) == el;
                case uint eu: return Convert.ToInt64(v) == eu;
                default: return string.Equals(v.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            return string.Equals(v.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public static void SetDefault(RegRoot root, string path, string value)
    {
        using var bk = BaseKey(root);
        using var key = bk.CreateSubKey(path, writable: true)
            ?? throw new InvalidOperationException($"Cannot open/create {root}\\{path}");
        key.SetValue(null, value, RegistryValueKind.String);
    }

    public static void DeleteSubKeyTree(RegRoot root, string path)
    {
        try
        {
            using var bk = BaseKey(root);
            bk.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
        catch { /* missing is fine */ }
    }

    public static bool KeyExists(RegRoot root, string path)
    {
        try
        {
            using var bk = BaseKey(root);
            using var key = bk.OpenSubKey(path);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    // ---- enumeration for the in-app Registry Editor ----
    public static string[] GetSubKeyNames(RegRoot root, string path)
    {
        try
        {
            using var bk = BaseKey(root);
            using var key = string.IsNullOrEmpty(path) ? bk : bk.OpenSubKey(path);
            return key?.GetSubKeyNames() ?? Array.Empty<string>();
        }
        catch { return Array.Empty<string>(); }
    }

    public static bool HasSubKeys(RegRoot root, string path) => GetSubKeyNames(root, path).Length > 0;

    public static List<(string name, RegistryValueKind kind, object? data)> GetValues(RegRoot root, string path)
    {
        var list = new List<(string, RegistryValueKind, object?)>();
        try
        {
            using var bk = BaseKey(root);
            using var key = string.IsNullOrEmpty(path) ? bk : bk.OpenSubKey(path);
            if (key is null) return list;
            foreach (var n in key.GetValueNames())
            {
                RegistryValueKind kind;
                try { kind = key.GetValueKind(n); } catch { kind = RegistryValueKind.Unknown; }
                object? data;
                try { data = key.GetValue(n, null, RegistryValueOptions.DoNotExpandEnvironmentNames); } catch { data = null; }
                list.Add((n, kind, data));
            }
        }
        catch { /* unreadable key */ }
        return list;
    }
}
