using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個 OneDrive 項目（檔案或資料夾）· One OneDrive item (file or folder) with its pin/online state.</summary>
public sealed class OneDriveEntry
{
    public string Path { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsFolder { get; init; }
    public long Size { get; init; }

    /// <summary>檔案屬性 · Raw attributes; null if unreadable.</summary>
    public FileAttributes? Attributes { get; init; }

    /// <summary>+P (FILE_ATTRIBUTE_PINNED) = always-local · pinned/always-local.</summary>
    public bool IsPinned => Attributes is { } a && a.HasFlag((FileAttributes)OneDriveService.FilePinned);

    /// <summary>+U (FILE_ATTRIBUTE_UNPINNED) = online-only · dehydrated/online-only.</summary>
    public bool IsOnlineOnly => Attributes is { } a && a.HasFlag((FileAttributes)OneDriveService.FileUnpinned);

    /// <summary>已下載到本機（佔空間）· Locally available content (not a placeholder).</summary>
    public bool IsLocallyAvailable => Attributes is { } a && !a.HasFlag((FileAttributes)OneDriveService.FileRecallOnDataAccess)
                                                          && !a.HasFlag((FileAttributes)OneDriveService.FileOffline);
}

/// <summary>
/// OneDrive 檔案隨選控制（IN-APP，無重新導向）· OneDrive Files-On-Demand control, fully in-app.
///
/// 釘選（永遠本機）· Pin (always-local):   attrib +P -U &lt;path&gt;
/// 脫水（只在雲端）· Dehydrate (online-only): attrib +U -P &lt;path&gt;
/// 暫停同步 · Pause sync:                    OneDrive.exe /shutdown
/// 自動脫水年期 · Auto-dehydration age (days) DWORD ConfigStorageSenseCloudContentDehydrationThreshold
///   under HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy
/// </summary>
public static class OneDriveService
{
    // Cloud Files placeholder attributes (winnt.h).
    public const int FilePinned = 0x00080000;              // FILE_ATTRIBUTE_PINNED
    public const int FileUnpinned = 0x00100000;            // FILE_ATTRIBUTE_UNPINNED
    public const int FileRecallOnDataAccess = 0x00400000;  // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS
    public const int FileOffline = 0x00001000;             // FILE_ATTRIBUTE_OFFLINE

    private const string StoragePolicyPath = @"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy";
    private const string DehydrationThresholdValue = "ConfigStorageSenseCloudContentDehydrationThreshold";

    /// <summary>
    /// 估算預設 OneDrive 資料夾 · Best guess at the user's OneDrive root folder (env var, else profile\OneDrive).
    /// </summary>
    public static string? DefaultRoot()
    {
        var fromEnv = Environment.GetEnvironmentVariable("OneDrive")
                      ?? Environment.GetEnvironmentVariable("OneDriveConsumer")
                      ?? Environment.GetEnvironmentVariable("OneDriveCommercial");
        if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv)) return fromEnv;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var guess = Path.Combine(profile, "OneDrive");
        return Directory.Exists(guess) ? guess : null;
    }

    /// <summary>列出一個資料夾入面嘅項目（唔遞迴）· List the immediate children of a folder.</summary>
    public static List<OneDriveEntry> List(string folder)
    {
        var rows = new List<OneDriveEntry>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return rows;

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(folder))
            {
                FileAttributes? attr = null;
                try { attr = File.GetAttributes(dir); } catch { /* ignore */ }
                rows.Add(new OneDriveEntry
                {
                    Path = dir,
                    Name = Path.GetFileName(dir),
                    IsFolder = true,
                    Attributes = attr,
                });
            }
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                FileAttributes? attr = null;
                long size = 0;
                try { attr = File.GetAttributes(file); } catch { /* ignore */ }
                try { size = new FileInfo(file).Length; } catch { /* ignore */ }
                rows.Add(new OneDriveEntry
                {
                    Path = file,
                    Name = Path.GetFileName(file),
                    IsFolder = false,
                    Size = size,
                    Attributes = attr,
                });
            }
        }
        catch { /* unreadable folder */ }

        rows.Sort((a, b) =>
        {
            if (a.IsFolder != b.IsFolder) return a.IsFolder ? -1 : 1;
            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });
        return rows;
    }

    private static string AttribArgs(string flags, string path) => $"{flags} \"{path}\" /S /D";

    /// <summary>釘選（永遠保留本機）· Pin = always-local. attrib +P -U &lt;path&gt; (recursive on folders).</summary>
    public static Task<TweakResult> Pin(string path, CancellationToken ct = default)
        => RunAttrib("+P -U", path, ct);

    /// <summary>脫水（變回只在雲端）· Dehydrate = online-only. attrib +U -P &lt;path&gt; (recursive on folders).</summary>
    public static Task<TweakResult> Dehydrate(string path, CancellationToken ct = default)
        => RunAttrib("+U -P", path, ct);

    private static async Task<TweakResult> RunAttrib(string flags, string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
            return TweakResult.Fail("Path not found.", "搵唔到路徑。");

        // For a single file, /S is invalid; only apply recursion to directories.
        bool isDir = Directory.Exists(path);
        var args = isDir ? AttribArgs(flags, path) : $"{flags} \"{path}\"";
        return await ShellRunner.Run("attrib.exe", args, elevated: false, ct);
    }

    /// <summary>暫停／關閉 OneDrive 同步 · Pause sync by shutting OneDrive.exe down.</summary>
    public static async Task<TweakResult> PauseSync(CancellationToken ct = default)
    {
        var exe = FindOneDriveExe();
        if (exe is null) return TweakResult.Fail("OneDrive.exe not found.", "搵唔到 OneDrive.exe。");
        return await ShellRunner.Run(exe, "/shutdown", elevated: false, ct);
    }

    /// <summary>重新啟動 OneDrive · Start OneDrive again (resume sync).</summary>
    public static async Task<TweakResult> ResumeSync(CancellationToken ct = default)
    {
        var exe = FindOneDriveExe();
        if (exe is null) return TweakResult.Fail("OneDrive.exe not found.", "搵唔到 OneDrive.exe。");
        return await ShellRunner.Run(exe, "/background", elevated: false, ct);
    }

    public static string? FindOneDriveExe()
    {
        var candidates = new[]
        {
            Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Microsoft\OneDrive\OneDrive.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft OneDrive\OneDrive.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Microsoft OneDrive\OneDrive.exe"),
        };
        foreach (var c in candidates)
            if (!string.IsNullOrWhiteSpace(c) && File.Exists(c)) return c;
        return null;
    }

    /// <summary>
    /// 讀取目前自動脫水年期（日）· Read the current auto-dehydration threshold in days; null if unset.
    /// </summary>
    public static int? GetDehydrationThresholdDays()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StoragePolicyPath);
            var v = key?.GetValue(DehydrationThresholdValue);
            if (v is int i && i > 0) return i;
        }
        catch { /* ignore */ }
        return null;
    }

    /// <summary>
    /// 設定自動脫水年期 · Set the auto-free (dehydration) threshold in days. days &lt;= 0 removes it.
    /// </summary>
    public static TweakResult SetDehydrationThresholdDays(int days)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(StoragePolicyPath, writable: true);
            if (key is null) return TweakResult.Fail("Could not open registry key.", "開唔到登錄機碼。");

            if (days <= 0)
            {
                if (key.GetValue(DehydrationThresholdValue) is not null)
                    key.DeleteValue(DehydrationThresholdValue, throwOnMissingValue: false);
                return TweakResult.Ok("Auto-free threshold cleared.", "已清除自動釋放空間年期。");
            }

            key.SetValue(DehydrationThresholdValue, days, RegistryValueKind.DWord);
            return TweakResult.Ok($"Auto-free threshold set to {days} day(s).", $"已將自動釋放空間年期設為 {days} 日。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    public static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes;
        int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{Math.Round(s, 1)} {u[i]}";
    }
}
