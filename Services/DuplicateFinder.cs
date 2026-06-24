using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace WinForge.Services;

/// <summary>一組內容相同嘅檔案 · A set of byte-identical files.</summary>
public sealed class DupGroup
{
    public string Hash { get; init; } = "";
    public long Size { get; init; }
    public List<string> Files { get; init; } = new();

    public long Wasted => Size * (Files.Count - 1); // space reclaimable by keeping one
}

/// <summary>
/// 重複檔案搜尋（純 C#）· Content-based duplicate finder. Groups by size first (cheap), then hashes
/// only same-size files with SHA-256, so matches are byte-identical — no false positives.
/// </summary>
public static class DuplicateFinder
{
    public static List<DupGroup> Scan(string folder, bool recursive, IProgress<int>? progress, CancellationToken ct)
    {
        var result = new List<DupGroup>();
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return result;

        var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        List<FileInfo> files;
        try
        {
            files = Directory.EnumerateFiles(folder, "*", opt)
                .Select(f => { try { return new FileInfo(f); } catch { return null; } })
                .Where(fi => fi is { Length: > 0 })
                .Cast<FileInfo>()
                .ToList();
        }
        catch { return result; }

        int hashed = 0;
        foreach (var sizeGroup in files.GroupBy(fi => fi.Length).Where(g => g.Count() > 1))
        {
            ct.ThrowIfCancellationRequested();
            var byHash = new Dictionary<string, List<string>>();
            foreach (var fi in sizeGroup)
            {
                ct.ThrowIfCancellationRequested();
                var h = HashFile(fi.FullName);
                if (h is null) continue;
                if (!byHash.TryGetValue(h, out var list)) byHash[h] = list = new List<string>();
                list.Add(fi.FullName);
                progress?.Report(++hashed);
            }
            foreach (var kv in byHash.Where(k => k.Value.Count > 1))
                result.Add(new DupGroup { Hash = kv.Key, Size = sizeGroup.Key, Files = kv.Value });
        }

        return result.OrderByDescending(g => g.Wasted).ToList();
    }

    private static string? HashFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var bytes = SHA256.HashData(stream);
            return Convert.ToHexString(bytes);
        }
        catch
        {
            return null; // locked / unreadable
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
