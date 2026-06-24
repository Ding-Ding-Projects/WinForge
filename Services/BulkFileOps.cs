using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace WinForge.Services;

public enum MatchMode { Wildcard, Regex, Extension }

/// <summary>
/// 批次檔案操作（純 C#）· Pure-C# bulk file operations: match by pattern, then copy/move/recycle/
/// flatten/organise. Deletes go to the Recycle Bin (reversible) via SHFileOperation.
/// </summary>
public static class BulkFileOps
{
    public static List<string> Match(string folder, string pattern, MatchMode mode, bool recursive)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return new();
        var opt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        try
        {
            switch (mode)
            {
                case MatchMode.Wildcard:
                    var pat = string.IsNullOrWhiteSpace(pattern) ? "*" : pattern;
                    return Directory.EnumerateFiles(folder, pat, opt).ToList();
                case MatchMode.Extension:
                    var ext = string.IsNullOrWhiteSpace(pattern) ? "" : (pattern.StartsWith('.') ? pattern : "." + pattern);
                    return Directory.EnumerateFiles(folder, "*", opt)
                        .Where(f => Path.GetExtension(f).Equals(ext, StringComparison.OrdinalIgnoreCase)).ToList();
                case MatchMode.Regex:
                    var rx = new Regex(pattern, RegexOptions.IgnoreCase);
                    return Directory.EnumerateFiles(folder, "*", opt).Where(f => rx.IsMatch(Path.GetFileName(f))).ToList();
            }
        }
        catch { /* bad pattern / unreadable */ }
        return new();
    }

    private static string Unique(string target)
    {
        if (!File.Exists(target)) return target;
        var dir = Path.GetDirectoryName(target) ?? "";
        var name = Path.GetFileNameWithoutExtension(target);
        var ext = Path.GetExtension(target);
        for (int i = 1; ; i++)
        {
            var cand = Path.Combine(dir, $"{name} ({i}){ext}");
            if (!File.Exists(cand)) return cand;
        }
    }

    public static (int ok, int fail) Copy(IEnumerable<string> files, string target)
    {
        int ok = 0, fail = 0;
        Directory.CreateDirectory(target);
        foreach (var f in files)
        {
            try { File.Copy(f, Unique(Path.Combine(target, Path.GetFileName(f)))); ok++; }
            catch { fail++; }
        }
        return (ok, fail);
    }

    public static (int ok, int fail) Move(IEnumerable<string> files, string target)
    {
        int ok = 0, fail = 0;
        Directory.CreateDirectory(target);
        foreach (var f in files)
        {
            try { File.Move(f, Unique(Path.Combine(target, Path.GetFileName(f)))); ok++; }
            catch { fail++; }
        }
        return (ok, fail);
    }

    public static (int ok, int fail) Flatten(string folder, IEnumerable<string> files)
    {
        int ok = 0, fail = 0;
        foreach (var f in files)
        {
            try
            {
                var dest = Path.Combine(folder, Path.GetFileName(f));
                if (string.Equals(Path.GetDirectoryName(f), folder, StringComparison.OrdinalIgnoreCase)) { ok++; continue; }
                File.Move(f, Unique(dest));
                ok++;
            }
            catch { fail++; }
        }
        return (ok, fail);
    }

    public static (int ok, int fail) OrganizeByExtension(string folder, IEnumerable<string> files)
    {
        int ok = 0, fail = 0;
        foreach (var f in files)
        {
            try
            {
                var ext = Path.GetExtension(f).TrimStart('.').ToUpperInvariant();
                if (string.IsNullOrEmpty(ext)) ext = "_noext";
                var sub = Path.Combine(folder, ext);
                Directory.CreateDirectory(sub);
                File.Move(f, Unique(Path.Combine(sub, Path.GetFileName(f))));
                ok++;
            }
            catch { fail++; }
        }
        return (ok, fail);
    }

    // ---- Recycle Bin delete via SHFileOperation (reversible) ----
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEOPSTRUCT
    {
        public IntPtr hwnd;
        public uint wFunc;
        public string pFrom;
        public string? pTo;
        public ushort fFlags;
        public int fAnyOperationsAborted;
        public IntPtr hNameMappings;
        public string? lpszProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

    private const uint FO_DELETE = 0x0003;
    private const ushort FOF_ALLOWUNDO = 0x0040;
    private const ushort FOF_NOCONFIRMATION = 0x0010;
    private const ushort FOF_SILENT = 0x0004;
    private const ushort FOF_NOERRORUI = 0x0400;

    public static (int ok, int fail) Recycle(IEnumerable<string> files)
    {
        var list = files.Where(File.Exists).ToList();
        if (list.Count == 0) return (0, 0);
        try
        {
            var op = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = string.Join('\0', list) + "\0\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
            };
            int rc = SHFileOperation(ref op);
            return rc == 0 ? (list.Count, 0) : (0, list.Count);
        }
        catch { return (0, list.Count); }
    }
}
