using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace WinForge.Services;

/// <summary>一個磁碟用量項目 · One disk-usage entry (folder or file).</summary>
public sealed class DiskEntry
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public long Size { get; init; }
    public bool IsDir { get; init; }
}

/// <summary>
/// 磁碟用量分析（純 C#）· Pure-C# disk-usage analyser: recursive size per immediate child, and a
/// top-N largest-files scan. Like WinDirStat/TreeSize, fully in-app.
/// </summary>
public static class DiskAnalyzer
{
    public static long DirSize(string path, CancellationToken ct)
    {
        long total = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(path))
            {
                try { total += new FileInfo(f).Length; } catch { /* unreadable */ }
            }
            foreach (var d in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                total += DirSize(d, ct);
            }
        }
        catch { /* access denied */ }
        return total;
    }

    /// <summary>呢個資料夾入面每個子項目嘅大細 · Size of each immediate child (subdirs recursive + loose files).</summary>
    public static List<DiskEntry> ByChild(string folder, IProgress<string>? progress, CancellationToken ct)
    {
        var entries = new List<DiskEntry>();
        if (!Directory.Exists(folder)) return entries;
        try
        {
            foreach (var d in Directory.EnumerateDirectories(folder))
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report(System.IO.Path.GetFileName(d));
                entries.Add(new DiskEntry { Name = System.IO.Path.GetFileName(d), Path = d, Size = DirSize(d, ct), IsDir = true });
            }
            foreach (var f in Directory.EnumerateFiles(folder))
            {
                try { entries.Add(new DiskEntry { Name = System.IO.Path.GetFileName(f), Path = f, Size = new FileInfo(f).Length, IsDir = false }); }
                catch { /* unreadable */ }
            }
        }
        catch { /* access denied */ }
        return entries.OrderByDescending(e => e.Size).ToList();
    }

    public static List<DiskEntry> LargestFiles(string folder, int topN, IProgress<string>? progress, CancellationToken ct)
    {
        var all = new List<DiskEntry>();
        try
        {
            foreach (var f in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(f);
                    all.Add(new DiskEntry { Name = fi.Name, Path = f, Size = fi.Length, IsDir = false });
                }
                catch { /* unreadable */ }
                if (all.Count % 2000 == 0) progress?.Report(all.Count.ToString());
            }
        }
        catch { /* access denied partway — return what we have */ }
        return all.OrderByDescending(e => e.Size).Take(topN).ToList();
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
