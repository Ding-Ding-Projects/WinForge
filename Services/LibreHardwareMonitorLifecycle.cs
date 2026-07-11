using System;
using System.Threading;
using LHM = LibreHardwareMonitor.Hardware;

namespace WinForge.Services;

/// <summary>
/// Process-wide lifecycle for the one LibreHardwareMonitor <see cref="LHM.Computer"/> WinForge owns.
/// 系統監察同電池與散熱可以同時持有 lease；只有最後一個 lease 釋放時先會 close 個由 WinForge 打開嘅 Computer。
/// </summary>
/// <remarks>
/// <para><see cref="LHM.Computer.Close"/> is the upstream, instance-scoped cleanup path; it performs
/// LibreHardwareMonitor's own Ring0 cleanup. This class never enumerates, stops, deletes, or rewrites
/// Windows services, so it cannot act on a stale or unrelated external driver service.</para>
/// <para>The shared computer enables the union of the System Monitor and Battery &amp; Thermal sensor
/// categories up front. That avoids one page closing the library-wide Ring0 transport while another
/// page is still sampling it.</para>
/// </remarks>
internal static class LibreHardwareMonitorLifecycle
{
    private static readonly ResourceLeaseCoordinator<LHM.Computer> Coordinator = new(
        CreateComputer,
        static computer => computer.Open(),
        static computer => computer.Close());

    static LibreHardwareMonitorLifecycle()
    {
        // MainWindow closes this synchronously during a normal quit. ProcessExit is only the final
        // best-effort backstop for termination paths that bypass the window close event.
        try { AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown(); } catch { }
    }

    internal static LibreHardwareMonitorLease? Acquire()
    {
        var lease = Coordinator.Acquire();
        return lease is null ? null : new LibreHardwareMonitorLease(lease);
    }

    internal static void Shutdown() => Coordinator.Shutdown();

    private static LHM.Computer CreateComputer() => new()
    {
        IsCpuEnabled = true,
        IsGpuEnabled = true,
        IsMotherboardEnabled = true,
        IsControllerEnabled = true,
    };
}

/// <summary>Safe access token for WinForge's shared LibreHardwareMonitor computer.</summary>
internal sealed class LibreHardwareMonitorLease : IDisposable
{
    private ResourceLeaseCoordinator<LHM.Computer>.Lease? _inner;

    internal LibreHardwareMonitorLease(ResourceLeaseCoordinator<LHM.Computer>.Lease inner) => _inner = inner;

    /// <summary>Run one short sample while the underlying computer remains open.</summary>
    internal bool TryUse(Action<LHM.Computer> action)
    {
        var inner = Volatile.Read(ref _inner);
        return inner is not null && inner.TryUse(action);
    }

    public void Dispose() => Interlocked.Exchange(ref _inner, null)?.Dispose();
}
