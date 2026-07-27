using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace WinForge.Services;

/// <summary>
/// One in-memory reactor simulation session that can move between the full control-room page and a
/// minimal UI-thread background loop. Only the simulated physics and truthful local status API keep
/// ticking in the background; audio, Home Assistant, keep-awake, real Windows settings, and real
/// shutdown handling remain page-owned and are stopped/restored when the page unloads.
/// </summary>
public sealed class ReactorSimulationSession
{
    internal ReactorSimulationSession() => Sim = new ReactorSimService();

    public ReactorSimService Sim { get; }
    public double SimClockSeconds { get; set; }
    public bool PersistenceRestoreAttempted { get; set; }
    public bool CommandLineAutoStartApplied { get; set; }
    public DateTime? RealShutdownDeadlineUtc { get; set; }
    public bool RealShutdownAborted { get; set; }
    public bool RealShutdownIssued { get; set; }
    public bool RealShutdownFailed { get; set; }

    public void ResetRealShutdownSequence()
    {
        RealShutdownDeadlineUtc = null;
        RealShutdownAborted = false;
        RealShutdownIssued = false;
        RealShutdownFailed = false;
    }
}

public sealed class ReactorSessionRuntime
{
    private static readonly Lazy<ReactorSessionRuntime> Shared = new(() => new());

    private readonly DispatcherTimer _backgroundTimer =
        new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly List<string> _foregroundOwners = new();
    private readonly ReactorSimulationSession _session = new();

    private DateTime _lastBackgroundTickUtc;

    private ReactorSessionRuntime()
    {
        _backgroundTimer.Tick += OnBackgroundTick;
    }

    public static ReactorSessionRuntime I => Shared.Value;

    /// <summary>
    /// Raised on the UI thread whenever the authoritative visible reactor page changes. A null
    /// token means no reactor page remains visible and the session has moved to its safe background
    /// loop. Page-owned safety UI and real-world effects use this to transfer or stop cleanly.
    /// </summary>
    public event Action<string?>? ForegroundDriverChanged;

    /// <summary>
    /// Gets the one canonical in-memory simulation shared by reactor pages and the background loop.
    /// </summary>
    public ReactorSimulationSession AcquireForPage() => _session;

    /// <summary>Marks a page session as visible and authoritative for the public status API.</summary>
    public void RegisterForeground(ReactorSimulationSession session, string ownerToken)
    {
        if (!ReferenceEquals(session, _session) || string.IsNullOrWhiteSpace(ownerToken)) return;
        string? previousDriver = CurrentForegroundDriver();
        _backgroundTimer.Stop();

        // Never carry a live real-shutdown countdown across visible control-room ownership. The new
        // page may still be building its visual tree, so keeping the wall-clock deadline alive could
        // let Windows shutdown fire before the ABORT control can paint or accept input.
        if (!string.IsNullOrWhiteSpace(previousDriver)
            && !string.Equals(previousDriver, ownerToken, StringComparison.Ordinal)
            && ReactorRealShutdownArm.Armed
            && session.Sim.Mode == ReactorMode.Meltdown
            && !session.RealShutdownIssued
            && !session.RealShutdownFailed)
        {
            session.RealShutdownAborted = true;
            session.RealShutdownDeadlineUtc = null;
        }

        _foregroundOwners.RemoveAll(
            owner => string.Equals(owner, ownerToken, StringComparison.Ordinal));
        _foregroundOwners.Add(ownerToken);
        ReactorHomeAssistantMirror.I.Resume();

        try
        {
            ReactorStatusApiService.I.Start();
            ReactorStatusApiService.I.Bind(session.Sim);
            ReactorStatusApiService.I.Publish();
        }
        catch { }
        PublishForegroundDriverChanged(previousDriver);
    }

    /// <summary>
    /// Hands the canonical session to the minimal background loop after the last visible owner leaves.
    /// With multiple visible reactor surfaces, the most recently registered owner drives the physics.
    /// </summary>
    public bool ReleaseForeground(ReactorSimulationSession session, string ownerToken)
    {
        if (!ReferenceEquals(session, _session) || string.IsNullOrWhiteSpace(ownerToken)) return false;
        string? previousDriver = CurrentForegroundDriver();
        _foregroundOwners.RemoveAll(
            owner => string.Equals(owner, ownerToken, StringComparison.Ordinal));
        if (_foregroundOwners.Count > 0)
        {
            try
            {
                ReactorStatusApiService.I.Bind(_session.Sim);
                ReactorStatusApiService.I.Publish();
            }
            catch { }
            PublishForegroundDriverChanged(previousDriver);
            return false;
        }

        // A real shutdown must always remain visibly abortable. Once the last reactor page leaves,
        // automatically disarm and cancel any in-flight session countdown before background physics
        // resumes. A later page can explicitly arm a fresh, visible ten-second countdown.
        ReactorRealShutdownArm.Armed = false;
        ReactorRealShutdownArm.CancelPendingVisibleArm();
        _session.ResetRealShutdownSequence();
        ReactorHomeAssistantMirror.I.RestoreOff();
        _lastBackgroundTickUtc = DateTime.UtcNow;
        try
        {
            ReactorStatusApiService.I.Start();
            ReactorStatusApiService.I.Bind(_session.Sim);
            ReactorStatusApiService.I.Publish();
        }
        catch { }
        _backgroundTimer.Start();
        PublishForegroundDriverChanged(previousDriver);
        return true;
    }

    public bool IsForegroundDriver(string ownerToken)
        => _foregroundOwners.Count > 0
           && string.Equals(_foregroundOwners[^1], ownerToken, StringComparison.Ordinal);

    public bool HasForegroundDriver => _foregroundOwners.Count > 0;

    private string? CurrentForegroundDriver()
        => _foregroundOwners.Count == 0 ? null : _foregroundOwners[^1];

    private void PublishForegroundDriverChanged(string? previousDriver)
    {
        string? currentDriver = CurrentForegroundDriver();
        if (string.Equals(previousDriver, currentDriver, StringComparison.Ordinal)) return;
        if (ForegroundDriverChanged is not { } handlers) return;

        foreach (Action<string?> handler in handlers.GetInvocationList())
        {
            try { handler(currentDriver); }
            catch (Exception ex) { CrashLogger.Log("reactor.foreground-driver", ex); }
        }
    }

    private void OnBackgroundTick(object? sender, object e)
    {
        if (_foregroundOwners.Count > 0)
        {
            _backgroundTimer.Stop();
            return;
        }

        var now = DateTime.UtcNow;
        double dt = (now - _lastBackgroundTickUtc).TotalSeconds;
        _lastBackgroundTickUtc = now;
        if (!double.IsFinite(dt) || dt <= 0 || dt > 1.0) dt = 0.1;

        try
        {
            if (_session.Sim.AutoStartMode)
                _session.Sim.DriveAutoStart(dt);
            _session.Sim.Update(dt);
            _session.SimClockSeconds += dt;
            ReactorStatusApiService.I.Publish();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("reactor.background.tick", ex);
        }
    }
}
