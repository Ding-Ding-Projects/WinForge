using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 模擬功能匯流排應急柴油機狀態 · State of the simulated emergency diesel that can feed
/// WinForge's deliberately playful reactor-dependent feature bus.
/// </summary>
public enum FeatureEmergencyDieselState
{
    Stopped,
    Starting,
    Running,
}

/// <summary>
/// 功能匯流排柴油機快照 · Immutable snapshot used by dependency checks, UI, and tests.
/// </summary>
public readonly record struct FeatureEmergencyDieselSnapshot(
    FeatureEmergencyDieselState State,
    double StartProgressSeconds,
    double StartTimeSeconds,
    double CapacityMW,
    double FuelLitres = 0,
    double FuelCapacityLitres = 0,
    double FuelBurnLitresPerMinute = 0,
    int ActiveModuleCount = 0,
    int MaxModuleCount = 0)
{
    public bool IsRunning => State == FeatureEmergencyDieselState.Running;
    public bool HasFuel => FuelLitres > 0;
    public double FuelPercent => FuelCapacityLitres > 0
        ? Math.Clamp(FuelLitres / FuelCapacityLitres * 100.0, 0, 100)
        : 0;
    public int AvailableModuleSlots => Math.Max(0, MaxModuleCount - ActiveModuleCount);
    public double RemainingStartSeconds => State == FeatureEmergencyDieselState.Starting
        ? Math.Max(0, StartTimeSeconds - StartProgressSeconds)
        : 0;
}

/// <summary>
/// Local feature-bus view used by in-process loads. It deliberately does not reuse or counterfeit
/// the public reactor-status wire DTO when the simulated diesel is supplying power.
/// </summary>
public readonly record struct FeaturePowerSnapshot(
    bool IsAvailable,
    ReactorDependencyPowerSource Source,
    double ElectricMW,
    string ModeEn,
    string ModeZh);

/// <summary>
/// Optional, simulated backup power for the small set of modules registered in
/// <see cref="ReactorDependencyService"/>.
///
/// Nuclear generation remains the preferred source. When the persisted fallback policy is enabled
/// and the live reactor path is unavailable, the operator must manually fill and start this
/// session-only emergency diesel, then wait for its ten-second start sequence. The EDG has two
/// concurrent module outlets and a session-only fuel tank. This service never starts a real
/// generator, changes Windows power settings, or writes diesel/fuel/running state to disk.
/// </summary>
public sealed class ReactorFeaturePowerService
{
    public const string AllowFallbackSettingKey = "reactor.dependencies.allowEmergencyDieselFallback";
    public const double EmergencyDieselStartTimeSeconds = 10.0;
    public const double EmergencyDieselCapacityMW = 250.0;
    public const double EmergencyDieselFuelCapacityLitres = 60.0;
    public const double EmergencyDieselFuelBurnLitresPerMinute = 1.0;
    public const int EmergencyDieselMaxModules = 2;

    private static readonly Lazy<ReactorFeaturePowerService> Shared = new(CreateDefault);

    private readonly object _gate = new();
    private readonly Action<bool>? _persistFallback;
    private readonly bool _useRealtimeClock;

    private bool _allowEmergencyDieselFallback;
    private FeatureEmergencyDieselState _dieselState;
    private double _dieselStartProgressSeconds;
    private double _dieselFuelLitres;
    private long _lastClockTimestamp;
    private readonly Dictionary<string, string> _moduleLeases =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>共用 runtime instance · Shared app-session instance.</summary>
    public static ReactorFeaturePowerService I => Shared.Value;

    /// <summary>
    /// Creates an isolated instance. Runtime code uses <see cref="I"/>; the public constructor keeps
    /// the state machine deterministic and independently testable without touching user settings.
    /// </summary>
    public ReactorFeaturePowerService(
        bool allowEmergencyDieselFallback = false,
        Action<bool>? persistFallback = null,
        bool useRealtimeClock = false)
    {
        _allowEmergencyDieselFallback = allowEmergencyDieselFallback;
        _persistFallback = persistFallback;
        _useRealtimeClock = useRealtimeClock;
        _dieselState = FeatureEmergencyDieselState.Stopped;
    }

    private static ReactorFeaturePowerService CreateDefault()
    {
        bool allowFallback = SettingsStore.Get(AllowFallbackSettingKey, "false") == "true";
        return new ReactorFeaturePowerService(
            allowFallback,
            value => SettingsStore.Set(AllowFallbackSettingKey, value ? "true" : "false"),
            useRealtimeClock: true);
    }

    /// <summary>
    /// 持久化後備政策 · Persisted policy. Turning it off also stops the session-only diesel so a
    /// stale running generator can never silently bypass strict nuclear mode.
    /// </summary>
    public bool AllowEmergencyDieselFallback
    {
        get
        {
            lock (_gate) return _allowEmergencyDieselFallback;
        }
        set
        {
            bool changed;
            lock (_gate)
            {
                changed = _allowEmergencyDieselFallback != value;
                _allowEmergencyDieselFallback = value;
                if (!value) StopEmergencyDieselCore(clearFuel: true);
            }

            if (changed)
            {
                try { _persistFallback?.Invoke(value); } catch { }
            }
        }
    }

    /// <summary>
    /// 手動加滿柴油 · Fill the simulated tank. Fuel is deliberately session-only and filling is
    /// allowed only while the EDG is stopped.
    /// </summary>
    public bool FillEmergencyDiesel()
    {
        lock (_gate)
        {
            if (!_allowEmergencyDieselFallback
                || _dieselState != FeatureEmergencyDieselState.Stopped
                || _dieselFuelLitres >= EmergencyDieselFuelCapacityLitres)
                return false;

            _dieselFuelLitres = EmergencyDieselFuelCapacityLitres;
            return true;
        }
    }

    /// <summary>目前柴油機快照 · Current diesel state, refreshed from a monotonic clock at runtime.</summary>
    public FeatureEmergencyDieselSnapshot EmergencyDiesel
    {
        get
        {
            RefreshFromClock();
            lock (_gate) return SnapshotCore();
        }
    }

    /// <summary>
    /// 手動啟動 · Start the simulated diesel. Returns false when fallback is disabled or the machine
    /// is already starting/running.
    /// </summary>
    public bool StartEmergencyDiesel()
    {
        lock (_gate)
        {
            if (!_allowEmergencyDieselFallback
                || _dieselState != FeatureEmergencyDieselState.Stopped
                || _dieselFuelLitres <= 0)
                return false;

            _dieselState = FeatureEmergencyDieselState.Starting;
            _dieselStartProgressSeconds = 0;
            _lastClockTimestamp = Stopwatch.GetTimestamp();
            return true;
        }
    }

    /// <summary>手動停機 · Stop the simulated diesel immediately.</summary>
    public bool StopEmergencyDiesel()
    {
        lock (_gate)
        {
            if (_dieselState == FeatureEmergencyDieselState.Stopped) return false;
            StopEmergencyDieselCore(clearFuel: false);
            return true;
        }
    }

    /// <summary>
    /// Atomically acquires one of the two EDG module outlets for a stable runtime owner token.
    /// Duplicate acquisition by the same owner/module is idempotent; changing module reuses the
    /// owner's existing outlet.
    /// </summary>
    public bool TryAcquireModule(string ownerToken, string moduleTag)
    {
        RefreshFromClock();
        if (string.IsNullOrWhiteSpace(ownerToken) || string.IsNullOrWhiteSpace(moduleTag))
            return false;

        lock (_gate)
        {
            if (!_allowEmergencyDieselFallback
                || _dieselState != FeatureEmergencyDieselState.Running
                || _dieselFuelLitres <= 0)
                return false;

            if (_moduleLeases.TryGetValue(ownerToken, out var existing))
            {
                if (string.Equals(existing, moduleTag, StringComparison.OrdinalIgnoreCase))
                    return true;

                _moduleLeases[ownerToken] = moduleTag;
                return true;
            }

            if (_moduleLeases.Count >= EmergencyDieselMaxModules)
                return false;

            _moduleLeases[ownerToken] = moduleTag;
            return true;
        }
    }

    /// <summary>Checks whether an owner can atomically take/reuse an EDG outlet.</summary>
    public bool CanAcquireModule(string? ownerToken, string moduleTag)
    {
        RefreshFromClock();
        if (string.IsNullOrWhiteSpace(moduleTag)) return false;

        lock (_gate)
        {
            if (!_allowEmergencyDieselFallback
                || _dieselState != FeatureEmergencyDieselState.Running
                || _dieselFuelLitres <= 0)
                return false;

            return !string.IsNullOrWhiteSpace(ownerToken) && _moduleLeases.ContainsKey(ownerToken)
                || _moduleLeases.Count < EmergencyDieselMaxModules;
        }
    }

    /// <summary>Releases a runtime owner's EDG outlet, if any.</summary>
    public bool ReleaseModule(string? ownerToken)
    {
        if (string.IsNullOrWhiteSpace(ownerToken)) return false;
        lock (_gate) return _moduleLeases.Remove(ownerToken);
    }

    /// <summary>Returns the module currently attached to an owner token.</summary>
    public string? LeasedModuleFor(string? ownerToken)
    {
        if (string.IsNullOrWhiteSpace(ownerToken)) return null;
        lock (_gate) return _moduleLeases.TryGetValue(ownerToken, out var module) ? module : null;
    }

    /// <summary>Whether at least one active EDG outlet currently powers this module tag.</summary>
    public bool IsModulePowered(string moduleTag)
    {
        RefreshFromClock();
        lock (_gate)
        {
            return _allowEmergencyDieselFallback
                   && _dieselState == FeatureEmergencyDieselState.Running
                   && _dieselFuelLitres > 0
                   && _moduleLeases.Values.Any(
                       tag => string.Equals(tag, moduleTag, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Whether this exact runtime owner currently holds an outlet for this module.</summary>
    public bool IsOwnerPoweringModule(string? ownerToken, string moduleTag)
    {
        RefreshFromClock();
        if (string.IsNullOrWhiteSpace(ownerToken) || string.IsNullOrWhiteSpace(moduleTag))
            return false;
        lock (_gate)
        {
            return _allowEmergencyDieselFallback
                   && _dieselState == FeatureEmergencyDieselState.Running
                   && _dieselFuelLitres > 0
                   && _moduleLeases.TryGetValue(ownerToken, out var leasedModule)
                   && string.Equals(leasedModule, moduleTag, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Deterministically advances the start sequence. Tests call this directly; the shared runtime
    /// instance advances from <see cref="Stopwatch"/> through <see cref="EmergencyDiesel"/>.
    /// </summary>
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        lock (_gate) AdvanceCore(Math.Min(seconds, 86_400.0));
    }

    /// <summary>
    /// Returns a local feature-bus view for in-process loads such as Cake Factory. Nuclear stays
    /// preferred; diesel power is reported only while this module actually owns an EDG outlet.
    /// The public reactor-status API itself remains truthful and unchanged.
    /// </summary>
    public FeaturePowerSnapshot ResolveFeaturePower(
        ReactorDependency dependency,
        ReactorStatusSnapshot liveSnapshot,
        bool apiEnabled,
        string? ownerToken = null)
    {
        var diesel = EmergencyDiesel;
        var check = ReactorDependencyService.Evaluate(
            dependency,
            liveSnapshot,
            apiEnabled,
            AllowEmergencyDieselFallback,
            diesel,
            dieselModuleSlotAvailable: IsOwnerPoweringModule(ownerToken, dependency.Tag));

        return check.Source switch
        {
            ReactorDependencyPowerSource.Nuclear => new FeaturePowerSnapshot(
                true,
                ReactorDependencyPowerSource.Nuclear,
                Math.Max(0, liveSnapshot.ElectricMW),
                liveSnapshot.Mode ?? "Nuclear",
                "核電"),
            ReactorDependencyPowerSource.EmergencyDiesel => new FeaturePowerSnapshot(
                true,
                ReactorDependencyPowerSource.EmergencyDiesel,
                diesel.CapacityMW,
                "Emergency diesel",
                "應急柴油發電機"),
            _ => new FeaturePowerSnapshot(
                false,
                ReactorDependencyPowerSource.None,
                0,
                "Offline",
                "離線"),
        };
    }

    private void RefreshFromClock()
    {
        if (!_useRealtimeClock) return;

        lock (_gate)
        {
            long now = Stopwatch.GetTimestamp();
            if (_dieselState == FeatureEmergencyDieselState.Stopped)
            {
                _lastClockTimestamp = now;
                return;
            }

            if (_lastClockTimestamp == 0)
            {
                _lastClockTimestamp = now;
                return;
            }

            double elapsed = (now - _lastClockTimestamp) / (double)Stopwatch.Frequency;
            _lastClockTimestamp = now;
            if (double.IsFinite(elapsed) && elapsed > 0)
                AdvanceCore(Math.Min(elapsed, 3_600.0));
        }
    }

    private void AdvanceCore(double seconds)
    {
        if (_dieselState == FeatureEmergencyDieselState.Stopped) return;

        _dieselFuelLitres = Math.Max(
            0,
            _dieselFuelLitres
            - seconds * EmergencyDieselFuelBurnLitresPerMinute / 60.0);
        if (_dieselFuelLitres <= 0)
        {
            StopEmergencyDieselCore(clearFuel: false);
            return;
        }

        if (_dieselState == FeatureEmergencyDieselState.Starting)
        {
            _dieselStartProgressSeconds = Math.Min(
                EmergencyDieselStartTimeSeconds,
                _dieselStartProgressSeconds + seconds);

            if (_dieselStartProgressSeconds >= EmergencyDieselStartTimeSeconds)
                _dieselState = FeatureEmergencyDieselState.Running;
        }
    }

    private FeatureEmergencyDieselSnapshot SnapshotCore() => new(
        _dieselState,
        _dieselStartProgressSeconds,
        EmergencyDieselStartTimeSeconds,
        EmergencyDieselCapacityMW,
        _dieselFuelLitres,
        EmergencyDieselFuelCapacityLitres,
        EmergencyDieselFuelBurnLitresPerMinute,
        _moduleLeases.Count,
        EmergencyDieselMaxModules);

    private void StopEmergencyDieselCore(bool clearFuel)
    {
        _dieselState = FeatureEmergencyDieselState.Stopped;
        _dieselStartProgressSeconds = 0;
        _lastClockTimestamp = 0;
        _moduleLeases.Clear();
        if (clearFuel) _dieselFuelLitres = 0;
    }
}
