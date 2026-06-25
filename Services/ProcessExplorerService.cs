using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 一個程序嘅快照 · A snapshot of one process. Combines System.Diagnostics.Process
/// (working set, threads, CPU time) with WMI Win32_Process (parent PID, command line,
/// executable path, owner, creation date). All best-effort; missing fields stay blank.
/// </summary>
public sealed class ProcEntry
{
    public int Pid { get; init; }
    public int ParentPid { get; set; }
    public string Name { get; set; } = "";
    public double CpuPercent { get; set; }
    public long WorkingSetBytes { get; set; }
    public long PrivateBytes { get; set; }
    public int ThreadCount { get; set; }
    public string Owner { get; set; } = "";
    public string Description { get; set; } = "";
    public string CommandLine { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public DateTime? StartTime { get; set; }
    public int ModuleCount { get; set; }
    public bool AccessDenied { get; set; }
}

/// <summary>
/// 原生程序總管（純 managed + P/Invoke + WMI）· Native Process Explorer backend.
/// CPU% is computed from TotalProcessorTime deltas over the sampling interval, divided
/// by the logical core count (same maths as Task Manager). No external tools are launched.
/// </summary>
public static class ProcessExplorerService
{
    private static Dictionary<int, TimeSpan> _prevCpu = new();
    private static long _prevStamp;
    private static bool _primed;

    private static readonly Dictionary<int, string> _descCache = new();
    private static readonly object _gate = new();

    public static int CoreCount => Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    /// 取得所有程序嘅快照 · Snapshot every process. CPU% is the share of total CPU
    /// capacity used since the previous call (returns 0% on the first, priming call).
    /// </summary>
    public static List<ProcEntry> Snapshot()
    {
        long now = Environment.TickCount64;
        double elapsedMs = _primed ? Math.Max(1, now - _prevStamp) : 0;
        int cores = CoreCount;

        // ---- WMI side: parent PID, command line, exe path, owner, start time ----
        var wmi = QueryWmi();

        var cur = new Dictionary<int, TimeSpan>();
        var list = new List<ProcEntry>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                int pid = p.Id;
                var entry = new ProcEntry { Pid = pid, Name = p.ProcessName };

                // CPU time delta → percentage of one second of total CPU capacity.
                TimeSpan cpu = TimeSpan.Zero;
                try { cpu = p.TotalProcessorTime; }
                catch { entry.AccessDenied = true; }
                cur[pid] = cpu;
                if (_primed && _prevCpu.TryGetValue(pid, out var prev) && elapsedMs > 0)
                    entry.CpuPercent = Math.Clamp((cpu - prev).TotalMilliseconds / (elapsedMs * cores) * 100.0, 0, 100);

                try { entry.WorkingSetBytes = p.WorkingSet64; } catch { }
                try { entry.PrivateBytes = p.PrivateMemorySize64; } catch { entry.AccessDenied = true; }
                try { entry.ThreadCount = p.Threads.Count; } catch { }
                try { entry.ModuleCount = p.Modules.Count; } catch { entry.AccessDenied = true; }
                try { entry.StartTime = p.StartTime; } catch { entry.AccessDenied = true; }

                if (wmi.TryGetValue(pid, out var w))
                {
                    entry.ParentPid = w.ParentPid;
                    entry.CommandLine = w.CommandLine;
                    entry.ExecutablePath = w.ExecutablePath;
                    entry.Owner = w.Owner;
                    if (entry.StartTime is null && w.StartTime is { } st) entry.StartTime = st;
                }

                entry.Description = DescriptionFor(entry.ExecutablePath, pid);
                list.Add(entry);
            }
            catch { }
            finally { p.Dispose(); }
        }

        _prevCpu = cur;
        _prevStamp = now;
        _primed = true;
        return list;
    }

    private sealed class WmiRow
    {
        public int ParentPid;
        public string CommandLine = "";
        public string ExecutablePath = "";
        public string Owner = "";
        public DateTime? StartTime;
    }

    private static Dictionary<int, WmiRow> QueryWmi()
    {
        var map = new Dictionary<int, WmiRow>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, ParentProcessId, CommandLine, ExecutablePath, CreationDate FROM Win32_Process");
            foreach (ManagementObject mo in searcher.Get())
            {
                try
                {
                    int pid = Convert.ToInt32(mo["ProcessId"]);
                    var row = new WmiRow
                    {
                        ParentPid = mo["ParentProcessId"] is { } pp ? Convert.ToInt32(pp) : 0,
                        CommandLine = mo["CommandLine"]?.ToString() ?? "",
                        ExecutablePath = mo["ExecutablePath"]?.ToString() ?? "",
                    };
                    if (mo["CreationDate"]?.ToString() is { Length: > 0 } cd)
                    {
                        try { row.StartTime = ManagementDateTimeConverter.ToDateTime(cd); } catch { }
                    }
                    // Owner is a separate WMI method call; best-effort, can be slow / denied.
                    try
                    {
                        var args = new object[2];
                        if (Convert.ToInt32(mo.InvokeMethod("GetOwner", args)) == 0)
                            row.Owner = args[0]?.ToString() ?? "";
                    }
                    catch { }
                    map[pid] = row;
                }
                catch { }
                finally { mo.Dispose(); }
            }
        }
        catch { }
        return map;
    }

    private static string DescriptionFor(string exePath, int pid)
    {
        if (string.IsNullOrEmpty(exePath)) return "";
        lock (_gate)
        {
            if (_descCache.TryGetValue(pid, out var cached)) return cached;
            string desc = "";
            try
            {
                if (File.Exists(exePath))
                {
                    var fvi = FileVersionInfo.GetVersionInfo(exePath);
                    desc = fvi.FileDescription ?? "";
                    if (string.IsNullOrWhiteSpace(desc)) desc = fvi.ProductName ?? "";
                }
            }
            catch { }
            _descCache[pid] = desc;
            return desc;
        }
    }

    // ---------------- Actions ----------------

    public static bool Kill(int pid)
    {
        try { using var p = Process.GetProcessById(pid); p.Kill(); return true; }
        catch { return false; }
    }

    /// <summary>結束程序樹（先殺子程序）· End a process and all its descendants, children first.</summary>
    public static int KillTree(int pid, IReadOnlyList<ProcEntry>? snapshot = null)
    {
        snapshot ??= Snapshot();
        var children = new Dictionary<int, List<int>>();
        foreach (var e in snapshot)
        {
            if (!children.TryGetValue(e.ParentPid, out var l)) children[e.ParentPid] = l = new();
            l.Add(e.Pid);
        }

        var order = new List<int>();
        void Collect(int p)
        {
            if (children.TryGetValue(p, out var kids))
                foreach (var k in kids)
                    if (k != p) Collect(k);
            order.Add(p); // children appended before parent → kill leaves first
        }
        Collect(pid);

        int killed = 0;
        foreach (var p in order)
            if (Kill(p)) killed++;
        return killed;
    }

    public static bool SetPriority(int pid, ProcessPriorityClass cls)
    {
        try { using var p = Process.GetProcessById(pid); p.PriorityClass = cls; return true; }
        catch { return false; }
    }

    public static ProcessPriorityClass? GetPriority(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.PriorityClass; }
        catch { return null; }
    }

    /// <summary>喺檔案總管度開啟並選取執行檔 · Open Explorer with the executable selected.</summary>
    public static bool OpenFileLocation(string exePath)
    {
        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return false;
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{exePath}\"") { UseShellExecute = true });
            return true;
        }
        catch { return false; }
    }

    public static void CopyToClipboard(string text)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(text ?? "");
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
        catch { }
    }

    public static string Bytes(double b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
        return $"{Math.Round(b, 1)} {u[i]}";
    }

    /// <summary>係咪以系統管理員身份運行 · Whether the current process is elevated (admin).</summary>
    public static bool IsElevated()
    {
        try
        {
            using var id = System.Security.Principal.WindowsIdentity.GetCurrent();
            var pr = new System.Security.Principal.WindowsPrincipal(id);
            return pr.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }
}
