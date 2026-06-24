using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 唯讀系統資訊（廉價、同步、即時讀取）。
/// Read-only system facts gathered cheaply and synchronously from the registry,
/// the environment, P/Invoke and DriveInfo — no slow WMI on the UI thread.
/// </summary>
public static class SystemInfo
{
    private const string CurrentVersion = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    private static string Reg(string path, string name, string fallback = "—")
        => RegistryHelper.GetValue(RegRoot.HKLM, path, name)?.ToString() ?? fallback;

    public static string OsProductName => Reg(CurrentVersion, "ProductName");

    public static string OsDisplayVersion => Reg(CurrentVersion, "DisplayVersion");

    public static string OsEdition => Reg(CurrentVersion, "EditionID");

    public static string OsBuild
    {
        get
        {
            var build = Reg(CurrentVersion, "CurrentBuildNumber");
            var ubr = RegistryHelper.GetValue(RegRoot.HKLM, CurrentVersion, "UBR");
            return ubr is int u ? $"{build}.{u}" : build;
        }
    }

    public static string OsFull => $"{OsProductName} ({OsDisplayVersion}) — {OsBuild}";

    public static string CpuName =>
        Reg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString").Trim();

    public static int LogicalProcessors => Environment.ProcessorCount;

    public static string Architecture => RuntimeInformation.OSArchitecture.ToString();

    public static string MachineName => Environment.MachineName;

    public static string UserName => Environment.UserName;

    public static string GpuName =>
        Reg(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DriverDesc");

    public static string RegisteredOwner => Reg(CurrentVersion, "RegisteredOwner");

    public static string Uptime
    {
        get
        {
            var ms = Environment.TickCount64;
            var ts = TimeSpan.FromMilliseconds(ms);
            return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        }
    }

    public static string BootTime
    {
        get
        {
            try
            {
                var boot = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount64);
                return boot.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }
            catch { return "—"; }
        }
    }

    // ---- Memory via GlobalMemoryStatusEx ----
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    private static MemoryStatusEx Mem()
    {
        var m = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        GlobalMemoryStatusEx(ref m);
        return m;
    }

    private static double ToGb(ulong bytes) => Math.Round(bytes / 1024.0 / 1024.0 / 1024.0, 1);

    public static string RamTotal => $"{ToGb(Mem().ullTotalPhys)} GB";

    public static string RamUsage
    {
        get
        {
            var m = Mem();
            return $"{m.dwMemoryLoad}% — {ToGb(m.ullTotalPhys - m.ullAvailPhys)} / {ToGb(m.ullTotalPhys)} GB";
        }
    }

    public static double RamLoadPercent => Mem().dwMemoryLoad;

    public static string SystemDrive
    {
        get
        {
            try
            {
                var root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var d = new DriveInfo(root);
                var freeGb = ToGb((ulong)d.AvailableFreeSpace);
                var totalGb = ToGb((ulong)d.TotalSize);
                var usedPct = totalGb > 0 ? (int)Math.Round((totalGb - freeGb) / totalGb * 100) : 0;
                return $"{root} — {freeGb} GB free / {totalGb} GB ({usedPct}% used)";
            }
            catch { return "—"; }
        }
    }

    public static string DotNetRuntime => $".NET {Environment.Version}";

    public static string TimeZone => TimeZoneInfo.Local.DisplayName;
}
