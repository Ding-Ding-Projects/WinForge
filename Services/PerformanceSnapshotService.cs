using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// Lightweight, on-demand system performance snapshot for Command Palette. It retains only one
/// previous GetSystemTimes sample to calculate CPU percentage; it does not run a background monitor.
/// </summary>
public sealed class PerformanceSnapshot
{
    public double? CpuPercent { get; init; }
    public ulong TotalPhysicalMemory { get; init; }
    public ulong AvailablePhysicalMemory { get; init; }
    public TimeSpan Uptime { get; init; }
    public string SystemDriveName { get; init; } = "";
    public ulong SystemDriveFree { get; init; }
    public ulong SystemDriveTotal { get; init; }
}

public static class PerformanceSnapshotService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
        public ulong Value => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    private static readonly object CpuGate = new();
    private static ulong _previousIdle;
    private static ulong _previousKernel;
    private static ulong _previousUser;

    public static PerformanceSnapshot Get()
    {
        var memory = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        try { GlobalMemoryStatusEx(ref memory); } catch { }

        double? cpu = SampleCpu();
        string driveName = Path.GetPathRoot(Environment.SystemDirectory) ?? "";
        ulong free = 0, total = 0;
        try
        {
            var drive = new DriveInfo(driveName);
            if (drive.IsReady)
            {
                free = (ulong)Math.Max(0, drive.AvailableFreeSpace);
                total = (ulong)Math.Max(0, drive.TotalSize);
            }
        }
        catch { }

        return new PerformanceSnapshot
        {
            CpuPercent = cpu,
            TotalPhysicalMemory = memory.TotalPhys,
            AvailablePhysicalMemory = memory.AvailPhys,
            Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
            SystemDriveName = driveName,
            SystemDriveFree = free,
            SystemDriveTotal = total,
        };
    }

    public static string FormatBytes(ulong bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    public static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1) return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1) return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{uptime.Minutes}m";
    }

    private static double? SampleCpu()
    {
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
            lock (CpuGate)
            {
                ulong total = kernel.Value + user.Value;
                double? result = null;
                if (_previousKernel != 0 || _previousUser != 0)
                {
                    ulong previousTotal = _previousKernel + _previousUser;
                    ulong totalDelta = total > previousTotal ? total - previousTotal : 0;
                    ulong idleDelta = idle.Value > _previousIdle ? idle.Value - _previousIdle : 0;
                    if (totalDelta > 0) result = Math.Clamp((1d - (double)idleDelta / totalDelta) * 100d, 0d, 100d);
                }
                _previousIdle = idle.Value;
                _previousKernel = kernel.Value;
                _previousUser = user.Value;
                return result;
            }
        }
        catch { return null; }
    }
}
