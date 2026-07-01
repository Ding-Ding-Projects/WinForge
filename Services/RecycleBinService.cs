using System;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 回收筒管理 · Recycle Bin manager — pure P/Invoke over shell32.dll (SHQueryRecycleBin / SHEmptyRecycleBin).
/// A null root path queries the totals across ALL drives. Every call is guarded by the caller; nothing throws.
/// </summary>
public static class RecycleBinService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    /// <summary>Result of a query across all drives. <see cref="Ok"/> is false if the HRESULT failed.</summary>
    public readonly record struct BinInfo(bool Ok, long Bytes, long Items);

    /// <summary>Query total item count and total size across every drive. Guarded — never throws.</summary>
    public static BinInfo Query()
    {
        try
        {
            var info = new SHQUERYRBINFO();
            info.cbSize = Marshal.SizeOf(info);
            // null root => aggregate over all drives.
            int hr = SHQueryRecycleBin(null, ref info);
            if (hr != 0) return new BinInfo(false, 0, 0);
            return new BinInfo(true, info.i64Size, info.i64NumItems);
        }
        catch
        {
            return new BinInfo(false, 0, 0);
        }
    }

    /// <summary>Result of an empty operation. <see cref="Ok"/> is false if the HRESULT failed.</summary>
    public readonly record struct EmptyResult(bool Ok, long FreedBytes, long FreedItems);

    /// <summary>Empty the Recycle Bin silently on all drives, returning how much was freed. Guarded — never throws.</summary>
    public static EmptyResult Empty()
    {
        try
        {
            var before = Query();
            int hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
            // S_OK (0) or E_UNEXPECTED when already empty; treat non-zero as a soft failure but still report deltas.
            if (hr != 0) return new EmptyResult(false, 0, 0);
            return new EmptyResult(true, before.Ok ? before.Bytes : 0, before.Ok ? before.Items : 0);
        }
        catch
        {
            return new EmptyResult(false, 0, 0);
        }
    }

    /// <summary>Human-readable byte formatter (1024-based). Guarded — never throws.</summary>
    public static string FormatBytes(long bytes)
    {
        try
        {
            if (bytes < 0) bytes = 0;
            string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
            double value = bytes;
            int u = 0;
            while (value >= 1024 && u < units.Length - 1) { value /= 1024; u++; }
            return u == 0 ? $"{value:0} {units[u]}" : $"{value:0.##} {units[u]}";
        }
        catch
        {
            return $"{bytes} B";
        }
    }
}
