using System;

namespace WinForge.Services;

/// <summary>
/// 電熱水泥迴轉窯模型 · Electric cement rotary-kiln model — an ⚛️ reactor-powered rotary kiln that calcines
/// limestone (CaCO₃) into clinker (→ cement) using electrical heat instead of a fossil (coal/gas/petcoke) burner.
/// The operator sets a power draw (0..400 MW). While adequately powered the kiln shell temperature climbs toward
/// the ~1450 °C calcination temperature; below a clinkering threshold no clinker forms, and tonnes of cement per
/// hour scale with the delivered heat ABOVE that threshold. Starve the kiln of power and it cools and production
/// stalls. Pure managed C#; deterministic, driven by an integer tick counter (500 ms/tick); never throws.
/// </summary>
public sealed class CementKilnService
{
    // --- physical set-points -------------------------------------------------
    /// <summary>Target calcination / clinkering temperature the kiln climbs toward when hot. (°C)</summary>
    public const double CalcinationTempC = 1450.0;
    /// <summary>Clinker only forms above this shell temperature. (°C)</summary>
    public const double ClinkerThresholdC = 1300.0;
    /// <summary>Maximum operator power draw. (MW)</summary>
    public const double MaxDrawMW = 400.0;
    /// <summary>Ambient temperature the kiln decays toward when unpowered. (°C)</summary>
    public const double AmbientC = 40.0;
    /// <summary>Real cement-kiln CO₂ intensity avoided vs a fossil-fired kiln. (tonnes CO₂ / tonne cement)</summary>
    public const double FossilCo2PerTonne = 0.90;

    private const double TickSeconds = 0.5; // 500 ms per tick

    // --- state ---------------------------------------------------------------
    /// <summary>Operator power-draw set-point (0..MaxDrawMW), independent of what the reactor can supply.</summary>
    public double SetpointMW { get; private set; }
    /// <summary>Whether the operator has fired the kiln (arc/heat on). When idle it cools toward ambient.</summary>
    public bool Firing { get; private set; }

    /// <summary>Actual power drawn this tick (limited by both the set-point and available reactor MW). (MW)</summary>
    public double DrawnMW { get; private set; }
    /// <summary>Current kiln shell temperature. (°C)</summary>
    public double KilnTempC { get; private set; } = AmbientC;
    /// <summary>Instantaneous production rate. (tonnes cement / hour)</summary>
    public double TonnesPerHour { get; private set; }
    /// <summary>Lifetime cement produced. (tonnes)</summary>
    public double TotalTonnes { get; private set; }
    /// <summary>Lifetime CO₂ avoided vs a fossil kiln for the same cement. (tonnes)</summary>
    public double Co2AvoidedTonnes => TotalTonnes * FossilCo2PerTonne;

    /// <summary>True when the kiln is actually heating (fired AND reactor supplying power).</summary>
    public bool Powered => Firing && DrawnMW > 1.0;
    /// <summary>True once the kiln is hot enough to clinker.</summary>
    public bool Clinkering => KilnTempC >= ClinkerThresholdC;

    private int _lastTick = int.MinValue;

    /// <summary>Set the operator power-draw set-point (0..1 fraction of full draw).</summary>
    public void SetPowerFraction(double fraction)
    {
        try
        {
            if (double.IsNaN(fraction)) fraction = 0;
            SetpointMW = Math.Clamp(fraction, 0, 1) * MaxDrawMW;
        }
        catch { }
    }

    /// <summary>Fire the kiln (start heating).</summary>
    public void Fire() { Firing = true; }

    /// <summary>Idle the kiln (stop heating; it cools toward ambient).</summary>
    public void Idle() { Firing = false; }

    /// <summary>Reset all counters and cool the kiln fully.</summary>
    public void Reset()
    {
        Firing = false;
        DrawnMW = 0;
        KilnTempC = AmbientC;
        TonnesPerHour = 0;
        TotalTonnes = 0;
        _lastTick = int.MinValue;
    }

    /// <summary>
    /// Advance the simulation one UI tick. <paramref name="availableMW"/> is the reactor's live electrical output;
    /// <paramref name="generating"/> gates whether any power can flow. Uses the integer tick to derive elapsed
    /// time so timing never depends on DateTime.Now. Never throws.
    /// </summary>
    public void Step(int tick, double availableMW, bool generating)
    {
        try
        {
            // Elapsed seconds since last step (defensive against skipped/duplicate ticks).
            double dt = TickSeconds;
            if (_lastTick != int.MinValue)
            {
                int delta = tick - _lastTick;
                if (delta > 0) dt = Math.Clamp(delta * TickSeconds, TickSeconds, 5.0);
            }
            _lastTick = tick;

            if (double.IsNaN(availableMW) || availableMW < 0) availableMW = 0;

            // How much power actually reaches the elements: min(set-point, available), only if fired & generating.
            double want = Firing && generating ? SetpointMW : 0;
            DrawnMW = Math.Min(want, availableMW);
            if (DrawnMW < 0) DrawnMW = 0;

            // --- kiln thermal model (first-order toward a driven target) ---
            // Heating: more drawn MW drives the equilibrium temperature higher (up to the calcination temp).
            // Cooling: without power the kiln relaxes toward ambient. Time constants tuned for a visible ramp.
            if (DrawnMW > 1.0)
            {
                double drive = Math.Clamp(DrawnMW / MaxDrawMW, 0, 1);
                double target = AmbientC + (CalcinationTempC - AmbientC) * drive;
                // heat up quickly, but never overshoot the target meaningfully
                double k = 0.06 * dt; // heating rate constant
                KilnTempC += (target - KilnTempC) * Math.Clamp(k, 0, 1);
            }
            else
            {
                double k = 0.03 * dt; // slower cooling
                KilnTempC += (AmbientC - KilnTempC) * Math.Clamp(k, 0, 1);
            }
            KilnTempC = Math.Clamp(KilnTempC, AmbientC, CalcinationTempC + 20);

            // --- production: clinker only above threshold; rate ∝ heat above threshold ---
            if (KilnTempC > ClinkerThresholdC && DrawnMW > 1.0)
            {
                double above = (KilnTempC - ClinkerThresholdC) / (CalcinationTempC - ClinkerThresholdC);
                above = Math.Clamp(above, 0, 1);
                // Peak throughput at full calcination temp (a big kiln ≈ 180 t/h of cement).
                TonnesPerHour = 180.0 * above * Math.Clamp(DrawnMW / MaxDrawMW, 0, 1);
            }
            else
            {
                TonnesPerHour = 0;
            }

            // integrate production over the elapsed time
            double producedNow = TonnesPerHour * (dt / 3600.0);
            if (producedNow > 0 && !double.IsNaN(producedNow)) TotalTonnes += producedNow;
        }
        catch { }
    }
}
