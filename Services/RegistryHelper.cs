using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>登錄檔根 · Registry hive roots.</summary>
public enum RegRoot { HKCU, HKLM, HKCR, HKU }

/// <summary>Outcome for a user-visible registry-value deletion attempt.</summary>
public sealed record RegistryValueDeleteResult(bool Success, Exception? Error = null);

/// <summary>
/// 安全嘅登錄檔讀寫包裝 · Thin, exception-safe wrapper around the Windows registry.
/// 全部用 64-bit view，呢個係 Windows 11 真正改設定嘅地方。
/// Uses the 64-bit view — this is where real Windows 11 settings live.
/// </summary>
public static class RegistryHelper
{
    // Keep this small seam beside the native wrapper: focused tests can prove the UI result mapping without
    // opening or modifying the real registry.
    internal interface IValueDeleteBackend
    {
        void DeleteValue(RegRoot root, string path, string name);
    }

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
        // Existing callers intentionally use best-effort deletion for reversible tweaks and cleanup.
        // Preserve that behavior while allowing interactive editors to ask for a truthful result below.
        _ = TryDeleteValue(root, path, name);
    }

    /// <summary>
    /// Deletes a value and reports whether the registry accepted the write. Unlike <see cref="DeleteValue"/>,
    /// this is for UI that must not claim a deletion after an access or concurrency failure.
    /// </summary>
    public static RegistryValueDeleteResult TryDeleteValue(RegRoot root, string path, string name)
        => TryDeleteValue(new NativeValueDeleteBackend(), root, path, name);

    internal static RegistryValueDeleteResult TryDeleteValue(IValueDeleteBackend backend, RegRoot root, string path, string name)
    {
        ArgumentNullException.ThrowIfNull(backend);
        try
        {
            backend.DeleteValue(root, path, name);
            return new RegistryValueDeleteResult(true);
        }
        catch (Exception ex)
        {
            return new RegistryValueDeleteResult(false, ex);
        }
    }

    // Test seam for the original best-effort semantics; production callers stay on the three-argument API.
    internal static void DeleteValue(IValueDeleteBackend backend, RegRoot root, string path, string name)
        => _ = TryDeleteValue(backend, root, path, name);

    private sealed class NativeValueDeleteBackend : IValueDeleteBackend
    {
        public void DeleteValue(RegRoot root, string path, string name)
        {
            using var bk = BaseKey(root);
            using var key = bk.OpenSubKey(path, writable: true)
                ?? throw new InvalidOperationException($"Cannot open {root}\\{path} for deletion.");

            // A selected UI row may race with another editor. Do not report success merely because
            // DeleteValue(..., false) would silently accept an already-missing value.
            var missing = new object();
            if (ReferenceEquals(key.GetValue(name, missing, RegistryValueOptions.DoNotExpandEnvironmentNames), missing))
                throw new InvalidOperationException($"Registry value '{name}' no longer exists.");

            key.DeleteValue(name, throwOnMissingValue: true);
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
