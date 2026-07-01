using System;

namespace WinForge.Services;

/// <summary>
/// 鋁冶煉廠 · Hall-Héroult aluminium smelter pot-line — a HEAVY reactor-powered load. Draws up to
/// ~700 MW of continuous power; tonnes of aluminium per day are proportional to the drawn power
/// (Faraday's law of electrolysis, lumped into a constant). The pots must stay molten: pot temperature
/// rises toward operating temperature while adequately powered and falls when power is below a floor.
/// If temperature drops past a freeze threshold the pots "freeze" — production collapses and a red
/// warning shows — recoverable by restoring power (a slow re-melt). Everything is computed here and
/// nothing ever throws; the caller feeds it a tick count and the live reactor snapshot each step.
/// </summary>
public sealed class SmelterService
{
    // --- Physical / model constants (all fictional-but-plausible) ---
    /// <summary>Maximum electrical draw of the pot-line when running at full line current, MW.</summary>
    public const double MaxDrawMW = 700.0;

    /// <summary>Operating (molten) pot temperature, °C — Hall-Héroult cells run ~960 °C.</summary>
    public const double OperatingTempC = 960.0;

    /// <summary>Cryolite bath freezing point, °C — below this the pot solidifies.</summary>
    public const double FreezeTempC = 830.0;

    /// <summary>Ambient temperature the pots cool toward when unpowered, °C.</summary>
    public const double AmbientTempC = 25.0;

    /// <summary>Fraction of full draw below which the pots start to cool instead of heat.</summary>
    private const double HeatFloorFraction = 0.35;

    /// <summary>Faraday-lumped yield constant: tonnes/day of aluminium per MW actually drawn.</summary>
    private const double TonnesPerDayPerMW = 0.153;

    /// <summary>Line current at full draw, kA (a large modern pot-line).</summary>
    private const double FullLineCurrentKA = 600.0;

    // --- Operator setpoint ---
    /// <summary>Whether the operator has the pot-line running (banked = idle/off).</summary>
    public bool LineRunning { get; private set; }

    /// <summary>Requested draw as a fraction 0..1 of <see cref="MaxDrawMW"/> (line load setpoint).</summary>
    public double LoadSetpoint { get; private set; } = 1.0;

    // --- Live state ---
    /// <summary>Power actually being drawn from the reactor this step, MW.</summary>
    public double DrawnMW { get; private set; }

    /// <summary>Current pot bath temperature, °C.</summary>
    public double PotTempC { get; private set; } = AmbientTempC;

    /// <summary>Line amperage, kA (scales with drawn power).</summary>
    public double LineCurrentKA { get; private set; }

    /// <summary>Whether the pots have frozen (bath solidified).</summary>
    public bool Frozen { get; private set; }

    /// <summary>Lifetime aluminium produced, tonnes.</summary>
    public double TonnesProduced { get; private set; }

    /// <summary>Instantaneous production rate, tonnes/day.</summary>
    public double TonnesPerDay { get; private set; }

    /// <summary>Whether the reactor was generating enough on the last tick.</summary>
    public bool Powered { get; private set; }

    /// <summary>⚡ 預熱器 · Reactor-Bank perk: when true the pots are far more freeze-resistant —
    /// they cool much slower when power is short and the freeze point is lowered by a safety margin,
    /// so they stay effectively "freeze-proof" within reason. Toggled by the UI from the economy service.</summary>
    public bool PreheatersActive { get; set; }

    /// <summary>Extra margin (°C) subtracted from the freeze threshold when pre-heaters are active.</summary>
    private const double PreheatFreezeMarginC = 120.0;

    /// <summary>Effective freeze temperature this tick — lowered when the pre-heater perk is owned.</summary>
    public double EffectiveFreezeTempC => PreheatersActive ? FreezeTempC - PreheatFreezeMarginC : FreezeTempC;

    private int _lastTick;
    private bool _first = true;

    /// <summary>Set the operator's line-load setpoint (0..1); clamped, never throws.</summary>
    public void SetLoad(double fraction)
    {
        if (double.IsNaN(fraction) || double.IsInfinity(fraction)) return;
        LoadSetpoint = Math.Clamp(fraction, 0.0, 1.0);
    }

    /// <summary>Start the pot-line (begin drawing power / producing).</summary>
    public void Run() => LineRunning = true;

    /// <summary>Bank the pot-line — stop drawing power (pots will cool and may freeze).</summary>
    public void Bank() => LineRunning = false;

    /// <summary>Full reset back to a cold, banked, empty state.</summary>
    public void Reset()
    {
        LineRunning = false;
        LoadSetpoint = 1.0;
        DrawnMW = 0;
        PotTempC = AmbientTempC;
        LineCurrentKA = 0;
        Frozen = false;
        TonnesProduced = 0;
        TonnesPerDay = 0;
        Powered = false;
        _first = true;
    }

    /// <summary>
    /// Advance the simulation. <paramref name="tick"/> is a monotonically increasing internal counter;
    /// simulation ramps use the tick DELTA (never wall-clock). <paramref name="available"/> is the MWe
    /// the reactor is offering and <paramref name="generating"/> whether it is actually generating.
    /// Never throws.
    /// </summary>
    public void Step(int tick, double available, bool generating)
    {
        try
        {
            if (_first) { _lastTick = tick; _first = false; }
            int dTicks = tick - _lastTick;
            _lastTick = tick;
            if (dTicks < 0) dTicks = 0;
            if (dTicks > 20) dTicks = 20; // clamp against long stalls

            if (double.IsNaN(available) || available < 0) available = 0;

            // How much power the line WANTS this step (running + healthy reactor).
            double want = LineRunning && generating ? MaxDrawMW * LoadSetpoint : 0.0;

            // A frozen pot-line cannot draw its full load — only a trickle for the slow re-melt.
            if (Frozen && want > 0) want = Math.Min(want, MaxDrawMW * 0.25);

            // The reactor can only give what it has available.
            double got = Math.Min(want, Math.Max(0, available));
            DrawnMW = got;
            Powered = generating && got > 1.0;

            LineCurrentKA = MaxDrawMW <= 0 ? 0 : FullLineCurrentKA * (got / MaxDrawMW);

            // --- Thermal model: heat toward operating temp when adequately powered, else cool. ---
            double heatFloor = MaxDrawMW * HeatFloorFraction;
            // Per-tick coefficients (a tick is ~0.5 s of UI time; sim is deliberately faster than real).
            double perTick = dTicks;

            if (got >= heatFloor && Powered)
            {
                // Heating: approach operating temperature. Surplus power above the floor heats faster.
                double surplus = (got - heatFloor) / Math.Max(1.0, MaxDrawMW - heatFloor); // 0..1
                double rate = 2.0 + 10.0 * Math.Clamp(surplus, 0, 1); // °C per tick
                PotTempC += rate * perTick;
                if (PotTempC > OperatingTempC) PotTempC = OperatingTempC;
            }
            else
            {
                // Cooling: partial power slows the cooling; no power cools fastest toward ambient.
                double powerHelp = Math.Clamp(got / Math.Max(1.0, heatFloor), 0, 1); // 0..1
                double coolRate = 6.0 * (1.0 - 0.7 * powerHelp); // °C per tick
                // ⚡ Pre-heaters (Reactor-Bank perk): keep the bath warm when power is short —
                // cooling is dramatically slowed and it never sinks below the lowered freeze margin.
                if (PreheatersActive) coolRate *= 0.15;
                PotTempC -= coolRate * perTick;
                double floorTemp = PreheatersActive
                    ? Math.Max(AmbientTempC, EffectiveFreezeTempC + 8.0) // stay above the (lowered) freeze point
                    : AmbientTempC;
                if (PotTempC < floorTemp) PotTempC = floorTemp;
            }

            // --- Freeze / thaw logic (freeze point is lowered while pre-heaters are owned) ---
            double freezeAt = EffectiveFreezeTempC;
            if (!Frozen && PotTempC <= freezeAt)
                Frozen = true;
            else if (Frozen && PotTempC >= OperatingTempC - 5.0)
                Frozen = false; // fully re-melted

            // --- Production (Faraday-lumped): only real when hot, powered and NOT frozen. ---
            bool producing = Powered && !Frozen && PotTempC >= freezeAt + 10.0;
            if (producing)
            {
                // Efficiency tapers off if the bath is cooler than operating temp.
                double tempEff = Math.Clamp((PotTempC - freezeAt) / Math.Max(1.0, OperatingTempC - freezeAt), 0, 1);
                TonnesPerDay = got * TonnesPerDayPerMW * tempEff;
                // A tick is treated as ~0.5 s; convert tonnes/day into tonnes for the elapsed ticks.
                double days = (perTick * 0.5) / 86400.0;
                TonnesProduced += TonnesPerDay * days;
            }
            else
            {
                TonnesPerDay = 0;
            }
        }
        catch
        {
            // Never throw out of the sim.
        }
    }
}
