using System;

namespace WinForge.Services;

/// <summary>
/// 電弧爐煉鋼廠 · Electric Arc Furnace (EAF) steel mill — a huge INTERMITTENT reactor-powered load. A batch
/// ("heat") process: charge a basket of scrap, then strike the arc to melt it — the furnace draws a big block
/// of MW (up to ~800 MW) and the bath temperature climbs toward the ~1600 °C tapping temperature. Melting only
/// progresses while the reactor supplies enough MW; if power is short the melt stalls and the bath cools. When
/// the heat reaches tapping temperature it can be TAPPED — a heat of molten steel is poured (tonnes produced,
/// heats counted) and the furnace returns cold-ish, ready for the next charge. Everything is computed here on an
/// internal tick delta (never wall-clock); the caller feeds a tick count and the live reactor snapshot. Never throws.
/// </summary>
public sealed class SteelMillService
{
    // --- Physical / model constants (fictional-but-plausible for a big AC EAF) ---
    /// <summary>Peak electrical draw of the furnace transformer when arcing at full power, MW.</summary>
    public const double MaxDrawMW = 800.0;

    /// <summary>Tapping temperature — the bath is ready to pour at this temperature, °C.</summary>
    public const double TapTempC = 1600.0;

    /// <summary>Ambient / cold-charge temperature the bath cools toward when unpowered, °C.</summary>
    public const double AmbientTempC = 40.0;

    /// <summary>Fraction of full draw below which the melt stalls and the bath cools instead of heating.</summary>
    private const double MeltFloorFraction = 0.30;

    /// <summary>Tonnes of steel in one full heat (tap size of a large furnace).</summary>
    public const double HeatTonnes = 150.0;

    /// <summary>Line current at full arc, kA (a very large graphite-electrode furnace).</summary>
    private const double FullElectrodeCurrentKA = 90.0;

    // --- Operator setpoint ---
    /// <summary>Whether the operator has the furnace charging &amp; melting (else idle).</summary>
    public bool Melting { get; private set; }

    /// <summary>Requested arc power as a fraction 0..1 of <see cref="MaxDrawMW"/>.</summary>
    public double PowerSetpoint { get; private set; } = 1.0;

    // --- Live state ---
    /// <summary>Power actually being drawn from the reactor this step, MW.</summary>
    public double DrawnMW { get; private set; }

    /// <summary>Current bath / furnace temperature, °C.</summary>
    public double BathTempC { get; private set; } = AmbientTempC;

    /// <summary>Electrode line current, kA (scales with drawn power).</summary>
    public double ElectrodeCurrentKA { get; private set; }

    /// <summary>Melt progress of the current heat, 0..1 (1 = ready to tap).</summary>
    public double HeatProgress { get; private set; }

    /// <summary>Whether the current heat has reached tapping temperature and can be tapped.</summary>
    public bool ReadyToTap => HeatProgress >= 1.0 && BathTempC >= TapTempC - 5.0;

    /// <summary>Whether the reactor was supplying enough on the last tick.</summary>
    public bool Powered { get; private set; }

    /// <summary>Number of heats tapped (lifetime).</summary>
    public int HeatsTapped { get; private set; }

    /// <summary>Lifetime steel produced, tonnes.</summary>
    public double TonnesProduced { get; private set; }

    /// <summary>Estimated power factor of the arc (display flavour, 0..1).</summary>
    public double PowerFactor { get; private set; } = 0.0;

    private int _lastTick;
    private bool _first = true;

    /// <summary>Set the arc-power setpoint (0..1); clamped, never throws.</summary>
    public void SetPower(double fraction)
    {
        if (double.IsNaN(fraction) || double.IsInfinity(fraction)) return;
        PowerSetpoint = Math.Clamp(fraction, 0.0, 1.0);
    }

    /// <summary>Charge scrap and strike the arc — begin melting the current heat.</summary>
    public void Charge() => Melting = true;

    /// <summary>Stop arcing — the furnace idles and the bath cools.</summary>
    public void Idle() => Melting = false;

    /// <summary>
    /// Tap the current heat if it is ready. Pours a heat of molten steel: counts a heat, adds its tonnes,
    /// resets the melt for a fresh cold charge. Returns tonnes tapped (0 when not ready). Never throws.
    /// </summary>
    public double Tap()
    {
        try
        {
            if (!ReadyToTap) return 0;
            HeatsTapped++;
            TonnesProduced += HeatTonnes;
            // Furnace returns to a fresh charge: progress cleared, bath drops toward a warm-but-cold-charge state.
            HeatProgress = 0;
            BathTempC = Math.Min(BathTempC, AmbientTempC + 260.0); // residual heat in the shell
            return HeatTonnes;
        }
        catch { return 0; }
    }

    /// <summary>Full reset back to a cold, idle, empty furnace.</summary>
    public void Reset()
    {
        Melting = false;
        PowerSetpoint = 1.0;
        DrawnMW = 0;
        BathTempC = AmbientTempC;
        ElectrodeCurrentKA = 0;
        HeatProgress = 0;
        Powered = false;
        HeatsTapped = 0;
        TonnesProduced = 0;
        PowerFactor = 0;
        _first = true;
    }

    /// <summary>
    /// Advance the simulation. <paramref name="tick"/> is a monotonic internal counter; ramps use the tick
    /// DELTA (never wall-clock). <paramref name="available"/> is the MWe the reactor is offering and
    /// <paramref name="generating"/> whether it is actually generating. Never throws.
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

            // The furnace only wants power while the operator is melting, the reactor is generating,
            // and the current heat isn't already sitting ready to tap.
            bool wantMelt = Melting && generating && HeatProgress < 1.0;
            double want = wantMelt ? MaxDrawMW * PowerSetpoint : 0.0;

            // The reactor can only give what it has available.
            double got = Math.Min(want, Math.Max(0, available));
            DrawnMW = got;
            Powered = generating && got > 1.0;

            ElectrodeCurrentKA = MaxDrawMW <= 0 ? 0 : FullElectrodeCurrentKA * (got / MaxDrawMW);
            // Power factor: a hot, well-supplied arc runs near ~0.75; a starved/short arc is worse.
            PowerFactor = Powered ? 0.60 + 0.20 * Math.Clamp(got / MaxDrawMW, 0, 1) : 0.0;

            double perTick = dTicks;
            double meltFloor = MaxDrawMW * MeltFloorFraction;

            if (got >= meltFloor && Powered && HeatProgress < 1.0)
            {
                // Melting: bath climbs toward tapping temperature; surplus arc power melts faster.
                double surplus = (got - meltFloor) / Math.Max(1.0, MaxDrawMW - meltFloor); // 0..1
                double rate = 6.0 + 26.0 * Math.Clamp(surplus, 0, 1); // °C per tick
                BathTempC += rate * perTick;
                if (BathTempC > TapTempC) BathTempC = TapTempC;
                // Heat progress tracks how close the bath is to tapping temperature.
                HeatProgress = Math.Clamp((BathTempC - AmbientTempC) / Math.Max(1.0, TapTempC - AmbientTempC), 0, 1);
            }
            else if (HeatProgress < 1.0)
            {
                // Stalled: partial power slows cooling; no power cools fastest toward ambient.
                double powerHelp = Math.Clamp(got / Math.Max(1.0, meltFloor), 0, 1); // 0..1
                double coolRate = 5.0 * (1.0 - 0.6 * powerHelp); // °C per tick
                BathTempC -= coolRate * perTick;
                if (BathTempC < AmbientTempC) BathTempC = AmbientTempC;
                HeatProgress = Math.Clamp((BathTempC - AmbientTempC) / Math.Max(1.0, TapTempC - AmbientTempC), 0, 1);
            }
            // When HeatProgress >= 1 the heat simply holds ready (superheat), awaiting a tap.
        }
        catch
        {
            // Never throw out of the sim.
        }
    }
}
