using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace WinForge.Services;

/// <summary>
/// 原生 NTFS 即時檔案搜尋索引 · Native instant file-search index, in the spirit of Everything
/// (voidtools) — but 100% managed/PInvoke C#. 唔會啟動、shell 或者綁定 Everything 或任何外部搜尋
/// 引擎；索引完全由自己讀 NTFS 主檔案表（MFT）經由 USN journal 重建。
///
/// This launches/shells/bundles NOTHING. The index is built ourselves by reading the NTFS Master
/// File Table via FSCTL_ENUM_USN_DATA on a raw volume handle (\\.\C:), reconstructing full paths
/// from parent file-reference-numbers (FRN). That requires Administrator + a raw volume handle.
/// If we are not elevated (the raw open fails), we fall back to a fast managed recursive directory
/// enumeration so the feature still works — just slower to build.
/// </summary>
public static class FileIndexService
{
    /// <summary>索引中一個檔案項目 · One indexed file entry.</summary>
    public sealed class Entry
    {
        public string Name = "";          // file name only (lower-cased copy lives in NameLower)
        public string NameLower = "";     // pre-lowered for fast substring matching
        public string Path = "";          // full path (resolved lazily for MFT mode)
        public long Size;                 // bytes; -1 if unknown
        public DateTime Modified;         // last-write time; default if unknown
        public bool IsDirectory;
    }

    /// <summary>邊種模式起咗索引 · Which mode built the index.</summary>
    public enum IndexMode { None, Mft, Recursive }

    /// <summary>一次索引重建嘅結果 · Result of one index build.</summary>
    public sealed class IndexResult
    {
        public List<Entry> Entries = new();
        public IndexMode Mode = IndexMode.None;
        public List<string> Volumes = new();   // volumes that used MFT mode
        public List<string> FallbackRoots = new(); // roots scanned recursively
        public bool ElevationWouldHelp;        // true when not elevated and we fell back
    }

    // ===== public API =====

    /// <summary>
    /// 起一個本機 NTFS 磁碟區嘅檔名索引 · Build a filename index of local fixed NTFS volumes.
    /// 有管理員權限就行 MFT 模式（快）；冇就退回遞迴列舉。
    /// Uses MFT mode (fast) when elevated; otherwise falls back to recursive enumeration.
    /// </summary>
    public static IndexResult Build(IProgress<string>? progress, CancellationToken ct)
    {
        var result = new IndexResult();
        bool elevated = AdminHelper.IsElevated;

        foreach (var di in EnumerateLocalFixedDrives())
        {
            ct.ThrowIfCancellationRequested();
            string root = di.RootDirectory.FullName; // e.g. "C:\"
            char letter = root.Length > 0 ? char.ToUpperInvariant(root[0]) : '?';

            bool isNtfs = string.Equals(SafeFormat(di), "NTFS", StringComparison.OrdinalIgnoreCase);
            bool mftDone = false;

            if (elevated && isNtfs)
            {
                progress?.Report($"{letter}: MFT…");
                try
                {
                    int before = result.Entries.Count;
                    if (TryReadMft(letter, result.Entries, progress, ct))
                    {
                        result.Volumes.Add($"{letter}:");
                        mftDone = true;
                        progress?.Report($"{letter}: {result.Entries.Count - before:N0}");
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { /* fall through to recursive for this volume */ }
            }

            if (!mftDone)
            {
                progress?.Report($"{letter}: scan…");
                if (!elevated || !isNtfs) result.ElevationWouldHelp = isNtfs && !elevated;
                ReadRecursive(root, result.Entries, progress, ct);
                result.FallbackRoots.Add(root);
            }
        }

        result.Mode = result.Volumes.Count > 0
            ? (result.FallbackRoots.Count > 0 ? IndexMode.Recursive /* mixed -> report as recursive-ish */ : IndexMode.Mft)
            : (result.Entries.Count > 0 || result.FallbackRoots.Count > 0 ? IndexMode.Recursive : IndexMode.None);

        // If at least one volume used MFT and none fell back, it's pure MFT.
        if (result.Volumes.Count > 0 && result.FallbackRoots.Count == 0) result.Mode = IndexMode.Mft;

        return result;
    }

    /// <summary>
    /// 搜尋已建立嘅索引 · Search a built index. Supports plain substring, wildcards (* ?), or regex.
    /// Returns up to <paramref name="limit"/> hits sorted by relevance.
    /// </summary>
    public static List<Entry> Search(IReadOnlyList<Entry> index, string query, bool useRegex, int limit, CancellationToken ct)
    {
        if (index.Count == 0) return new();
        query = (query ?? "").Trim();
        if (query.Length == 0) return new();

        Func<Entry, bool> match;
        Func<Entry, int> score;
        string qLower = query.ToLowerInvariant();

        if (useRegex)
        {
            Regex rx;
            try { rx = new Regex(query, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); }
            catch { return new(); } // invalid regex -> no results (UI surfaces this)
            match = e => rx.IsMatch(e.Name);
            score = e => rx.IsMatch(e.Name) ? RelevanceScore(e, qLower) : 0;
        }
        else if (query.IndexOf('*') >= 0 || query.IndexOf('?') >= 0)
        {
            var rx = WildcardToRegex(query);
            match = e => rx.IsMatch(e.Name);
            score = e => RelevanceScore(e, qLower);
        }
        else
        {
            match = e => e.NameLower.Contains(qLower, StringComparison.Ordinal);
            score = e => RelevanceScore(e, qLower);
        }

        var hits = new List<Entry>();
        int scanned = 0;
        foreach (var e in index)
        {
            if ((++scanned & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            if (match(e)) hits.Add(e);
        }

        hits.Sort((a, b) =>
        {
            int c = score(b).CompareTo(score(a));
            if (c != 0) return c;
            c = a.Name.Length.CompareTo(b.Name.Length);
            if (c != 0) return c;
            return string.Compare(a.NameLower, b.NameLower, StringComparison.Ordinal);
        });

        return hits.Count > limit ? hits.GetRange(0, limit) : hits;
    }

    public static string HumanSize(long bytes)
    {
        if (bytes < 0) return "";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes; int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return i == 0 ? $"{bytes} B" : $"{s:0.#} {u[i]}";
    }

    // ===== relevance =====

    private static int RelevanceScore(Entry e, string qLower)
    {
        int idx = e.NameLower.IndexOf(qLower, StringComparison.Ordinal);
        if (idx < 0) return 1; // matched by regex/wildcard but no literal substring
        if (e.NameLower.Length == qLower.Length) return 1000; // exact name
        if (idx == 0) return 600;                              // prefix
        // word-boundary bonus
        char prev = e.NameLower[idx - 1];
        if (prev is ' ' or '_' or '-' or '.' or '(' or '[') return 400;
        return 200;
    }

    private static Regex WildcardToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        foreach (char c in pattern)
        {
            sb.Append(c switch
            {
                '*' => ".*",
                '?' => ".",
                _ => Regex.Escape(c.ToString()),
            });
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    // ===== drive helpers =====

    private static IEnumerable<DriveInfo> EnumerateLocalFixedDrives()
    {
        DriveInfo[] all;
        try { all = DriveInfo.GetDrives(); }
        catch { yield break; }
        foreach (var d in all)
        {
            bool ok;
            try { ok = d.DriveType == DriveType.Fixed && d.IsReady; }
            catch { ok = false; }
            if (ok) yield return d;
        }
    }

    private static string SafeFormat(DriveInfo d)
    {
        try { return d.DriveFormat; } catch { return ""; }
    }

    // ===== recursive fallback (pure managed) =====

    private static void ReadRecursive(string root, List<Entry> sink, IProgress<string>? progress, CancellationToken ct)
    {
        var opts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint, // avoid junction/symlink loops
            ReturnSpecialDirectories = false,
        };

        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(root, "*", opts); }
        catch { return; }

        int n = 0;
        foreach (var path in entries)
        {
            if ((++n & 0x3FFF) == 0)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report($"{root[0]}: {n:N0}");
            }

            try
            {
                var fi = new FileInfo(path);
                bool isDir = (fi.Attributes & FileAttributes.Directory) != 0;
                sink.Add(new Entry
                {
                    Name = fi.Name,
                    NameLower = fi.Name.ToLowerInvariant(),
                    Path = path,
                    Size = isDir ? -1 : fi.Length,
                    Modified = SafeTime(fi),
                    IsDirectory = isDir,
                });
            }
            catch
            {
                // Path still useful even if metadata read fails.
                string name = System.IO.Path.GetFileName(path);
                sink.Add(new Entry { Name = name, NameLower = name.ToLowerInvariant(), Path = path, Size = -1 });
            }
        }
    }

    private static DateTime SafeTime(FileInfo fi)
    {
        try { return fi.LastWriteTime; } catch { return default; }
    }

    // ===== MFT mode (raw volume + USN journal enumeration) =====

    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

    private const uint FSCTL_ENUM_USN_DATA = 0x000900B3;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
        uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice, uint dwIoControlCode,
        IntPtr lpInBuffer, int nInBufferSize,
        IntPtr lpOutBuffer, int nOutBufferSize,
        out int lpBytesReturned, IntPtr lpOverlapped);

    [StructLayout(LayoutKind.Sequential)]
    private struct MFT_ENUM_DATA_V0
    {
        public ulong StartFileReferenceNumber;
        public long LowUsn;
        public long HighUsn;
    }

    // One reconstructed record before path resolution.
    private struct RawRecord
    {
        public ulong Frn;
        public ulong ParentFrn;
        public string Name;
        public bool IsDir;
    }

    /// <summary>
    /// 讀一個磁碟區嘅 MFT · Enumerate one volume's MFT via FSCTL_ENUM_USN_DATA, then rebuild full
    /// paths from parent FRNs. Returns false (caller falls back) if the raw handle can't be opened
    /// or the control fails — typically when not elevated.
    /// </summary>
    private static bool TryReadMft(char letter, List<Entry> sink, IProgress<string>? progress, CancellationToken ct)
    {
        string volPath = $"\\\\.\\{letter}:";
        using SafeFileHandle h = CreateFileW(volPath,
            GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero,
            OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);

        if (h.IsInvalid) return false; // no admin / no raw access

        // FRN -> (name, parentFrn, isDir). Capacity guess keeps re-hashing down.
        var byFrn = new Dictionary<ulong, RawRecord>(1 << 18);

        var med = new MFT_ENUM_DATA_V0 { StartFileReferenceNumber = 0, LowUsn = 0, HighUsn = long.MaxValue };
        int inSize = Marshal.SizeOf<MFT_ENUM_DATA_V0>();
        IntPtr inBuf = Marshal.AllocHGlobal(inSize);

        const int outSize = 1 << 16; // 64 KB working buffer
        IntPtr outBuf = Marshal.AllocHGlobal(outSize);

        try
        {
            bool any = false;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                Marshal.StructureToPtr(med, inBuf, false);

                if (!DeviceIoControl(h, FSCTL_ENUM_USN_DATA, inBuf, inSize, outBuf, outSize, out int bytes, IntPtr.Zero))
                {
                    // ERROR_HANDLE_EOF (38) at the end is normal; anything else before any data => fail.
                    int err = Marshal.GetLastWin32Error();
                    if (err == 38) break;
                    if (!any) return false;
                    break;
                }
                if (bytes <= 8) break;

                any = true;
                // First 8 bytes = next start FRN for the following call.
                ulong nextStart = unchecked((ulong)Marshal.ReadInt64(outBuf, 0));
                ParseUsnBuffer(outBuf, bytes, byFrn);
                med.StartFileReferenceNumber = nextStart;

                if ((byFrn.Count & 0x1FFFF) == 0) progress?.Report($"{letter}: {byFrn.Count:N0}");
            }

            if (byFrn.Count == 0) return false;

            // Resolve full paths from parent chains, with a cache.
            ResolveAndEmit(letter, byFrn, sink, ct);
            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(inBuf);
            Marshal.FreeHGlobal(outBuf);
        }
    }

    // USN_RECORD_V2 field offsets (packed, no padding in the native layout).
    private const int OFF_RecordLength = 0;       // uint
    private const int OFF_FileReferenceNumber = 8;   // ulong
    private const int OFF_ParentFileReferenceNumber = 16; // ulong
    private const int OFF_FileAttributes = 52;     // uint
    private const int OFF_FileNameLength = 56;      // ushort
    private const int OFF_FileNameOffset = 58;      // ushort

    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

    private static void ParseUsnBuffer(IntPtr outBuf, int bytes, Dictionary<ulong, RawRecord> byFrn)
    {
        int offset = 8; // skip the leading next-start FRN
        while (offset + OFF_FileNameOffset + 2 <= bytes)
        {
            int recLen = Marshal.ReadInt32(outBuf, offset + OFF_RecordLength);
            if (recLen <= 0 || offset + recLen > bytes) break;

            ulong frn = unchecked((ulong)Marshal.ReadInt64(outBuf, offset + OFF_FileReferenceNumber));
            ulong parent = unchecked((ulong)Marshal.ReadInt64(outBuf, offset + OFF_ParentFileReferenceNumber));
            uint attrs = unchecked((uint)Marshal.ReadInt32(outBuf, offset + OFF_FileAttributes));
            ushort nameLen = unchecked((ushort)Marshal.ReadInt16(outBuf, offset + OFF_FileNameLength));
            ushort nameOff = unchecked((ushort)Marshal.ReadInt16(outBuf, offset + OFF_FileNameOffset));

            if (nameLen > 0 && offset + nameOff + nameLen <= bytes)
            {
                string name = Marshal.PtrToStringUni(outBuf + offset + nameOff, nameLen / 2);
                byFrn[frn] = new RawRecord
                {
                    Frn = frn,
                    ParentFrn = parent,
                    Name = name,
                    IsDir = (attrs & FILE_ATTRIBUTE_DIRECTORY) != 0,
                };
            }

            offset += recLen;
        }
    }

    private static void ResolveAndEmit(char letter, Dictionary<ulong, RawRecord> byFrn, List<Entry> sink, CancellationToken ct)
    {
        string driveRoot = $"{letter}:\\";
        // Cache of FRN -> resolved directory path (including trailing separator semantics handled below).
        var pathCache = new Dictionary<ulong, string>(byFrn.Count / 4 + 16);
        var chain = new List<RawRecord>(64);
        int n = 0;

        foreach (var kv in byFrn)
        {
            if ((++n & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
            var rec = kv.Value;

            string parentDir = ResolveDir(rec.ParentFrn, byFrn, pathCache, driveRoot, chain);
            if (parentDir == null) continue; // orphaned record; skip

            string full = parentDir.EndsWith('\\') ? parentDir + rec.Name : parentDir + "\\" + rec.Name;

            sink.Add(new Entry
            {
                Name = rec.Name,
                NameLower = rec.Name.ToLowerInvariant(),
                Path = full,
                Size = -1,        // size/time omitted in MFT mode for speed; resolved on demand by UI
                Modified = default,
                IsDirectory = rec.IsDir,
            });
        }
    }

    /// <summary>
    /// 由 FRN 解析資料夾完整路徑（有快取，避免 stack overflow）· Resolve a directory FRN to its full
    /// path iteratively (cached), so deep trees don't overflow the stack.
    /// </summary>
    private static string ResolveDir(ulong frn, Dictionary<ulong, RawRecord> byFrn,
        Dictionary<ulong, string> pathCache, string driveRoot, List<RawRecord> chain)
    {
        if (pathCache.TryGetValue(frn, out var cached)) return cached;

        chain.Clear();
        ulong cur = frn;
        string basePath = null;

        // Walk up until we hit the cache, the volume root, or a missing link.
        while (true)
        {
            if (pathCache.TryGetValue(cur, out var hit)) { basePath = hit; break; }
            if (!byFrn.TryGetValue(cur, out var rec))
            {
                // Reached the volume root's parent (the root's own record may be absent) -> treat as root.
                basePath = driveRoot;
                break;
            }
            // A directory whose parent is itself / 5 is the root; the root's name is the volume label.
            if (rec.ParentFrn == cur || cur == rec.Frn && rec.ParentFrn == 0)
            {
                pathCache[cur] = driveRoot;
                basePath = driveRoot;
                break;
            }
            chain.Add(rec);
            cur = rec.ParentFrn;
            if (chain.Count > 4096) { basePath = driveRoot; break; } // pathological guard
        }

        // Build downward, caching each level.
        string path = basePath;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var rec = chain[i];
            path = path.EndsWith('\\') ? path + rec.Name : path + "\\" + rec.Name;
            pathCache[rec.Frn] = path;
        }
        pathCache[frn] = path;
        return path;
    }
}
