using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace WinForge.Services;

public sealed class ProcInfo
{
    public int Pid { get; init; }
    public string Name { get; init; } = "";
    public long MemoryBytes { get; init; }
    public double CpuPercent { get; init; }
}

/// <summary>
/// 即時系統監察（純 managed + P/Invoke）· Live system monitor: CPU% via GetSystemTimes deltas, RAM via
/// GlobalMemoryStatusEx, network rates via NetworkInterface byte deltas, and top processes by memory.
/// </summary>
public static class SystemMonitor
{
    // ---- CPU via GetSystemTimes ----
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    private static long _pIdle, _pKernel, _pUser;
    private static bool _cpuPrimed;

    public static double CpuPercent()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        if (!_cpuPrimed) { _pIdle = idle; _pKernel = kernel; _pUser = user; _cpuPrimed = true; return 0; }

        long dIdle = idle - _pIdle, dKernel = kernel - _pKernel, dUser = user - _pUser;
        _pIdle = idle; _pKernel = kernel; _pUser = user;

        long total = dKernel + dUser; // kernel already includes idle
        if (total <= 0) return 0;
        double busy = total - dIdle;
        return Math.Clamp(busy * 100.0 / total, 0, 100);
    }

    // ---- Memory ----
    [StructLayout(LayoutKind.Sequential)]
    private struct MemStatus
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemStatus b);

    public static (double percent, long usedBytes, long totalBytes) Memory()
    {
        var m = new MemStatus { dwLength = (uint)Marshal.SizeOf<MemStatus>() };
        GlobalMemoryStatusEx(ref m);
        long total = (long)m.ullTotalPhys, used = (long)(m.ullTotalPhys - m.ullAvailPhys);
        return (m.dwMemoryLoad, used, total);
    }

    // ---- Network rates ----
    private static long _pRx, _pTx;
    private static bool _netPrimed;

    public static (double downBps, double upBps) Network(double seconds)
    {
        long rx = 0, tx = 0;
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var s = ni.GetIPv4Statistics();
                rx += s.BytesReceived;
                tx += s.BytesSent;
            }
        }
        catch { }

        if (!_netPrimed) { _pRx = rx; _pTx = tx; _netPrimed = true; return (0, 0); }
        double down = Math.Max(0, (rx - _pRx) / Math.Max(0.001, seconds));
        double up = Math.Max(0, (tx - _pTx) / Math.Max(0.001, seconds));
        _pRx = rx; _pTx = tx;
        return (down, up);
    }

    // ---- Per-process CPU% (TotalProcessorTime deltas) ----
    private static Dictionary<int, TimeSpan> _prevProcCpu = new();
    private static long _prevProcStamp;
    private static bool _procPrimed;

    /// <summary>Sample all processes; CPU% is the share of one second of total CPU capacity used since
    /// the last call. Returns the top <paramref name="n"/> sorted by CPU or working set.</summary>
    public static List<ProcInfo> Sample(int n, bool byCpu)
    {
        long now = Environment.TickCount64;
        double elapsedMs = _procPrimed ? Math.Max(1, now - _prevProcStamp) : 0;
        int cores = Math.Max(1, Environment.ProcessorCount);
        var cur = new Dictionary<int, TimeSpan>();
        var list = new List<ProcInfo>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                TimeSpan cpu;
                try { cpu = p.TotalProcessorTime; } catch { cpu = TimeSpan.Zero; }
                cur[p.Id] = cpu;

                double pct = 0;
                if (_procPrimed && _prevProcCpu.TryGetValue(p.Id, out var prev))
                    pct = Math.Clamp((cpu - prev).TotalMilliseconds / (elapsedMs * cores) * 100.0, 0, 100);

                list.Add(new ProcInfo { Pid = p.Id, Name = p.ProcessName, MemoryBytes = p.WorkingSet64, CpuPercent = pct });
            }
            catch { }
            finally { p.Dispose(); }
        }

        _prevProcCpu = cur;
        _prevProcStamp = now;
        _procPrimed = true;

        return list
            .OrderByDescending(x => byCpu ? x.CpuPercent : x.MemoryBytes)
            .ThenByDescending(x => byCpu ? (double)x.MemoryBytes : x.CpuPercent)
            .Take(n).ToList();
    }

    public static bool Kill(int pid)
    {
        try { using var p = Process.GetProcessById(pid); p.Kill(); return true; }
        catch { return false; }
    }

    public static bool SetPriority(int pid, ProcessPriorityClass cls)
    {
        try { using var p = Process.GetProcessById(pid); p.PriorityClass = cls; return true; }
        catch { return false; }
    }

    // ---- CPU affinity (managed) ----
    public static int CoreCount => Math.Min(64, Math.Max(1, Environment.ProcessorCount));

    public static long GetAffinity(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.ProcessorAffinity.ToInt64(); }
        catch { return 0; }
    }

    public static bool SetAffinity(int pid, long mask)
    {
        if (mask == 0) return false;
        try { using var p = Process.GetProcessById(pid); p.ProcessorAffinity = new IntPtr(mask); return true; }
        catch { return false; }
    }

    // ---- Efficiency Mode (EcoQoS) — same as Task Manager: EXECUTION_SPEED throttle + Idle priority.
    // Verified against MS Learn SetProcessInformation / PROCESS_POWER_THROTTLING_STATE (Win11 22000+). ----
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_POWER_THROTTLING_STATE { public uint Version, ControlMask, StateMask; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr h, int infoClass, ref PROCESS_POWER_THROTTLING_STATE info, uint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetPriorityClass(IntPtr h, uint cls);

    private const int ProcessPowerThrottling = 4;
    private const uint PT_VERSION = 1, PT_EXECUTION_SPEED = 0x1;
    private const uint IDLE_CLASS = 0x40, NORMAL_CLASS = 0x20;
    private const uint PROCESS_SET_INFORMATION = 0x0200;

    /// <summary>Toggle Windows 11 "Efficiency mode" (EcoQoS) on a process. Needs admin for other users'
    /// processes; silently returns false where unsupported (pre-22000) or denied.</summary>
    public static bool SetEfficiency(int pid, bool on)
    {
        IntPtr h = OpenProcess(PROCESS_SET_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) return false;
        try
        {
            var st = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PT_VERSION,
                ControlMask = on ? PT_EXECUTION_SPEED : 0,
                StateMask = on ? PT_EXECUTION_SPEED : 0,
            };
            bool ok = SetProcessInformation(h, ProcessPowerThrottling, ref st, (uint)Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>());
            ok &= SetPriorityClass(h, on ? IDLE_CLASS : NORMAL_CLASS);
            return ok;
        }
        catch { return false; }
        finally { CloseHandle(h); }
    }

    public static string Bytes(double b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
        return $"{Math.Round(b, 1)} {u[i]}";
    }

    public static string Uptime()
    {
        var ts = TimeSpan.FromMilliseconds(Environment.TickCount64);
        return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
    }
}
