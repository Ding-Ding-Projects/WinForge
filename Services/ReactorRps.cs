using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 跳脫方向 · Direction in which a bistable comparator trips.
/// </summary>
public enum RpsTripDir
{
    High, // trips when the measured value rises to/above the setpoint
    Low,  // trips when the measured value falls to/below the setpoint
}

/// <summary>
/// 一個保護功能（含其冗餘儀表通道）· One Reactor Protection System protection FUNCTION, owning up to
/// four physically-independent analogue instrument channels (Westinghouse protection sets I–IV).
///
/// Each channel has a fixed, deterministic per-channel calibration offset (NOT a per-tick RNG) so the
/// channels read slightly differently — modelling real instrument scatter while staying bit-reproducible.
/// A channel's bistable comparator trips when its measured value crosses the function setpoint; the
/// reactor trips on this function only when at least <see cref="RequiredCoincidence"/> channels are
/// tripped at once (classic 2-out-of-4 / 2-out-of-3 coincidence). A single failed or noisy channel
/// therefore produces only a PARTIAL TRIP (alarm) and never a spurious reactor trip.
/// </summary>
public sealed class RpsFunction
{
    public string NameEn { get; }
    public string NameZh { get; }
    public RpsTripDir Dir { get; }

    private readonly double _setpoint;          // constant setpoint (ignored when _setpointFunc != null)
    private readonly Func<double>? _setpointFunc; // variable setpoint (OTΔT / OPΔT)
    private readonly Func<double> _signal;        // pulls the true process value from the plant model
    private readonly Func<bool>? _permissive;     // null ⇒ always active; else gates the function on/off

    public int ChannelCount { get; }
    public int RequiredCoincidence { get; }

    public double[] ChannelOffset { get; }   // fixed calibration bias, fraction of |setpoint|
    public bool[] Bypass { get; }            // channel removed from the vote (2/4 → 2/3)
    public bool[] ForceTrip { get; }         // channel forced tripped (pre-tripped test position)
    public bool[] ChannelTripped { get; }    // current bistable state, exposed for the panel
    public double[] ChannelValue { get; }    // current per-channel measured value, for the panel

    public int TrippedCount { get; private set; }
    public bool Blocked { get; private set; }      // gated out by a permissive (e.g. below P-7)
    public bool PartialTrip { get; private set; }  // exactly one channel tripped — annunciate only
    public bool FunctionTrip { get; private set; } // coincidence met → contributes to reactor trip
    public double Setpoint => _setpointFunc?.Invoke() ?? _setpoint;

    public RpsFunction(string nameEn, string nameZh, RpsTripDir dir, double setpoint,
        Func<double> signal, Func<bool>? permissive = null, Func<double>? setpointFunc = null,
        int channelCount = 4, int coincidence = 2, double biasSpan = 0.006)
    {
        NameEn = nameEn; NameZh = nameZh; Dir = dir;
        _setpoint = setpoint; _signal = signal; _permissive = permissive; _setpointFunc = setpointFunc;
        ChannelCount = Math.Clamp(channelCount, 2, 4);
        RequiredCoincidence = Math.Clamp(coincidence, 1, ChannelCount);

        ChannelOffset = new double[ChannelCount];
        Bypass = new bool[ChannelCount];
        ForceTrip = new bool[ChannelCount];
        ChannelTripped = new bool[ChannelCount];
        ChannelValue = new double[ChannelCount];

        // Deterministic symmetric scatter about zero: e.g. 4 ch → {-1.5,-0.5,+0.5,+1.5}·biasSpan.
        double mid = (ChannelCount - 1) / 2.0;
        for (int i = 0; i < ChannelCount; i++)
            ChannelOffset[i] = (i - mid) * biasSpan;
    }

    public void Evaluate()
    {
        Blocked = _permissive != null && !_permissive();
        double sp = _setpointFunc?.Invoke() ?? _setpoint;
        double trueVal = _signal();
        double scale = sp == 0 ? 1.0 : Math.Abs(sp);

        TrippedCount = 0;
        for (int i = 0; i < ChannelCount; i++)
        {
            double v = trueVal + ChannelOffset[i] * scale;
            ChannelValue[i] = v;
            bool raw = Dir == RpsTripDir.High ? v >= sp : v <= sp;
            if (ForceTrip[i]) raw = true;
            ChannelTripped[i] = raw;
            if (Bypass[i]) continue;        // excluded from the vote → coincidence auto-degrades to 2/3
            if (raw) TrippedCount++;
        }

        PartialTrip = TrippedCount == 1;
        FunctionTrip = !Blocked && TrippedCount >= RequiredCoincidence;
    }
}

/// <summary>
/// 反應堆保護系統 · The Reactor Protection System: a flat, synchronous, deterministic state machine that
/// evaluates every protection function each tick, applies permissive interlocks (P-6/P-7/P-8/P-9/P-10),
/// and asserts a reactor trip when ANY function reaches its coincidence. The trip seals in (latches)
/// until an operator reset, exactly like the real trip breakers.
///
/// Pure managed C#: no threads, no events, no external libraries — the next state is a pure function of
/// (previous state, injected plant signals, switch positions), so it is fully reproducible.
/// </summary>
public sealed class ReactorRps
{
    public List<RpsFunction> Functions { get; } = new();

    public bool ReactorTrip { get; private set; }       // instantaneous coincidence trip present
    public bool TripLatched { get; private set; }       // sealed-in trip (breakers open)
    public string ControllingFunctionEn { get; private set; } = "";
    public string ControllingFunctionZh { get; private set; } = "";

    // Permissive interlock states (derived each tick), exposed for the control-room display.
    public bool P6 { get; private set; }
    public bool P7 { get; private set; }
    public bool P8 { get; private set; }
    public bool P9 { get; private set; }
    public bool P10 { get; private set; }

    public void SetPermissives(bool p6, bool p7, bool p8, bool p9, bool p10)
    {
        P6 = p6; P7 = p7; P8 = p8; P9 = p9; P10 = p10;
    }

    public void Evaluate()
    {
        ReactorTrip = false;
        ControllingFunctionEn = "";
        ControllingFunctionZh = "";
        foreach (var f in Functions)   // fixed evaluation order ⇒ deterministic "controlling" function
        {
            f.Evaluate();
            if (f.FunctionTrip && !ReactorTrip)
            {
                ReactorTrip = true;
                ControllingFunctionEn = f.NameEn;
                ControllingFunctionZh = f.NameZh;
            }
        }
        TripLatched |= ReactorTrip;
    }

    /// <summary>Clear the sealed-in trip — refused while any function is still in coincidence.</summary>
    public bool Reset()
    {
        if (Functions.Any(f => f.FunctionTrip)) return false;
        TripLatched = false;
        return true;
    }

    public void ClearLatch() => TripLatched = false;
}
