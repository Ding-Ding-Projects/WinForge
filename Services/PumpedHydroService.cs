using System;

namespace WinForge.Services;

/// <summary>抽水蓄能運作模式 · Operating mode for the pumped-storage plant.</summary>
public enum HydroMode
{
    Pump,      // consume reactor MW to pump water uphill (store)
    Hold,      // idle
    Generate,  // release water downhill through turbines (produce MWe)
}

/// <summary>
/// 抽水蓄能水力發電 · Pumped-storage hydro — a grid BUFFER tied to the flagship reactor. Two reservoirs
/// share a fixed water volume; the upper-reservoir level (%) IS the stored energy. PUMP consumes spare
/// reactor power to lift water uphill (charging); GENERATE releases it back through turbines to produce
/// MWe (discharging) even when the reactor is down. Round-trip efficiency ~80% — you get back less than
/// you store. Pure managed C#, never throws.
/// </summary>
public sealed class PumpedHydroService
{
    // Plant sizing (deterministic, illustrative).
    public const double CapacityMWh = 2000.0;   // energy the full upper reservoir holds
    public const double MaxPumpMW = 400.0;       // max electrical draw while pumping
    public const double MaxGenMW = 350.0;        // max electrical output while generating
    public const double RoundTripEfficiency = 0.80; // stored energy returned on discharge

    // Split the round-trip loss across the two legs (√0.80 ≈ 0.894 each way).
    private static readonly double LegEff = Math.Sqrt(RoundTripEfficiency);

    private double _stored;     // MWh currently in the upper reservoir (0..CapacityMWh)
    public HydroMode Mode { get; private set; } = HydroMode.Hold;
    public bool Auto { get; private set; }

    // Live readouts from the last Tick.
    public double PumpDrawMW { get; private set; }   // MW consumed while pumping (>0 only in Pump)
    public double GenOutMW { get; private set; }      // MW produced while generating (>0 only in Generate)
    public double EarnedTotal { get; private set; }   // ⚡ earned this session (informational)
    public string StatusNote { get; private set; } = "";

    private double _earnAccum;   // ⚡ carry so we deposit in whole increments

    public double StoredMWh => _stored;
    public double LevelFraction => CapacityMWh <= 0 ? 0 : Math.Clamp(_stored / CapacityMWh, 0, 1);
    public double LevelPercent => LevelFraction * 100.0;
    public bool IsFull => LevelFraction >= 0.999;
    public bool IsEmpty => _stored <= 0.0001;

    public void SetMode(HydroMode mode) { Mode = mode; }
    public void SetAuto(bool on) { Auto = on; }

    public void Reset()
    {
        _stored = 0;
        Mode = HydroMode.Hold;
        Auto = false;
        PumpDrawMW = GenOutMW = 0;
        EarnedTotal = 0;
        _earnAccum = 0;
        StatusNote = "";
    }

    /// <summary>
    /// 每格更新 · Advance the plant one tick.
    /// <paramref name="dt"/> seconds elapsed; <paramref name="reactorMW"/> live reactor electrical output;
    /// <paramref name="reactorGenerating"/> whether the reactor is actually generating;
    /// <paramref name="requestPumpMW"/> operator-chosen pump draw. Auto-mode retargets <see cref="Mode"/>.
    /// Returns the ⚡ to deposit this tick (already tracked into <see cref="EarnedTotal"/>).
    /// </summary>
    public double Tick(double dt, double reactorMW, bool reactorGenerating, double requestPumpMW)
    {
        try
        {
            if (double.IsNaN(dt) || dt <= 0) dt = 0;
            dt = Math.Clamp(dt, 0, 5);
            if (double.IsNaN(reactorMW) || reactorMW < 0) reactorMW = 0;

            // Reactor "spare" power available for pumping: only the slice above a base threshold.
            const double SpareThresholdMW = 200.0;
            double spare = reactorGenerating ? Math.Max(0, reactorMW - SpareThresholdMW) : 0;

            if (Auto) ApplyAuto(reactorGenerating, spare);

            PumpDrawMW = 0;
            GenOutMW = 0;
            double earnNow = 0;
            double hours = dt / 3600.0;

            switch (Mode)
            {
                case HydroMode.Pump:
                    if (!reactorGenerating)
                        StatusNote = Loc.I.Pick("Can't pump — reactor isn't generating.", "唔抽得水 — 反應堆冇發電。");
                    else if (IsFull)
                        StatusNote = Loc.I.Pick("Upper reservoir full — pumping paused.", "上水塘滿咗 — 暫停抽水。");
                    else if (spare <= 1)
                        StatusNote = Loc.I.Pick("No spare reactor power to pump with.", "反應堆冇多餘電力可以抽水。");
                    else
                    {
                        double draw = Math.Clamp(double.IsNaN(requestPumpMW) ? 0 : requestPumpMW, 0, Math.Min(MaxPumpMW, spare));
                        if (draw > 0)
                        {
                            // Electrical energy in → stored energy up (with pump-leg loss).
                            double add = draw * hours * LegEff;
                            add = Math.Min(add, CapacityMWh - _stored);
                            _stored += add;
                            PumpDrawMW = draw;
                            StatusNote = Loc.I.Pick("Pumping water uphill — storing surplus nuclear power.",
                                                    "抽水上山 — 儲起多餘核電。");
                        }
                    }
                    break;

                case HydroMode.Generate:
                    if (IsEmpty)
                        StatusNote = Loc.I.Pick("Reservoir empty — pump first (needs reactor power).",
                                                "水塘空咗 — 要先抽水（需要反應堆電力）。");
                    else
                    {
                        double outMW = MaxGenMW;
                        // Cap output by what the reservoir can actually supply this tick.
                        double maxByStore = hours > 0 ? _stored / hours : outMW;
                        outMW = Math.Min(outMW, maxByStore);
                        if (outMW > 0)
                        {
                            double drawn = outMW * hours;      // stored energy removed
                            drawn = Math.Min(drawn, _stored);
                            _stored -= drawn;
                            GenOutMW = outMW;
                            // Earn ⚡ for the electricity returned to the grid (generate-leg loss folded in).
                            double delivered = drawn * LegEff;
                            _earnAccum += delivered * 1000.0 * ReactorEconomyService.MintPerMWSecond * 3600.0;
                            StatusNote = Loc.I.Pick("Generating — releasing water to back up the grid.",
                                                    "發電中 — 放水撐住電網。");
                        }
                    }
                    break;

                default: // Hold
                    StatusNote = Loc.I.Pick("Idle — holding the reservoir.", "閒置 — 保持水位。");
                    break;
            }

            // Deposit earnings in whole-ish increments (≥1 ⚡) to avoid ledger spam.
            if (_earnAccum >= 1.0)
            {
                earnNow = Math.Floor(_earnAccum);
                _earnAccum -= earnNow;
                EarnedTotal += earnNow;
            }
            return earnNow;
        }
        catch { return 0; }
    }

    private void ApplyAuto(bool reactorGenerating, double spare)
    {
        try
        {
            const double PumpSpareThreshold = 50.0; // need at least this much spare to bother pumping
            if (reactorGenerating && spare > PumpSpareThreshold && !IsFull)
                Mode = HydroMode.Pump;
            else if (!reactorGenerating && !IsEmpty)
                Mode = HydroMode.Generate;
            else
                Mode = HydroMode.Hold;
        }
        catch { }
    }
}
