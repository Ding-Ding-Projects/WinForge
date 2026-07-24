using System;

namespace WinForge.Services;

/// <summary>
/// 核電合成氨（哈柏法）廠模型 · Nuclear ammonia (Haber-Bosch) plant model — an ⚛️ reactor-powered green-ammonia
/// plant. Electrolyzers split water into H₂, an air-separation unit supplies N₂, and the Haber-Bosch synthesis
/// loop (N₂ + 3H₂ → 2NH₃, iron catalyst, ~450 °C) converts them into ammonia — the feedstock of nitrogen
/// fertilizer. The operator sets a power draw (0..350 MW). While adequately powered the synthesis-loop pressure
/// climbs toward ~200 bar; below a synthesis threshold no ammonia forms, and tonnes of NH₃ per hour scale with
/// the loop pressure above that threshold and the power delivered. Starve the plant of power and the loop
/// depressurises and production stalls. Pure managed C#; deterministic, driven by an integer tick counter
/// (500 ms/tick); never throws.
/// </summary>
public sealed class AmmoniaPlantService
{
    // --- physical set-points -------------------------------------------------
    /// <summary>Target Haber-Bosch synthesis-loop pressure the compressors climb toward when powered. (bar)</summary>
    public const double LoopPressureMaxBar = 200.0;
    /// <summary>Ammonia only synthesises above this loop pressure. (bar)</summary>
    public const double SynthesisThresholdBar = 150.0;
    /// <summary>Maximum operator power draw. (MW)</summary>
    public const double MaxDrawMW = 350.0;
    /// <summary>Default draw is deliberately above the pressure model's synthesis threshold. (MW)</summary>
    public const double DefaultDrawMW = 280.0;
    /// <summary>Ambient (depressurised) loop pressure the plant decays toward when unpowered. (bar)</summary>
    public const double AmbientBar = 1.0;
    /// <summary>Green-ammonia electricity intensity — electrolysis dominates. (MWh / tonne NH₃)</summary>
    public const double MWhPerTonne = 10.0;
    /// <summary>Electrolyzer specific energy. (kWh / kg H₂)</summary>
    public const double ElectrolyzerKWhPerKgH2 = 50.0;
    /// <summary>Share of drawn power feeding the electrolyzers (rest runs ASU + syn-loop compressors).</summary>
    public const double ElectrolyzerPowerShare = 0.85;
    /// <summary>CO₂ avoided vs conventional steam-methane-reformed ("grey") ammonia. (t CO₂ / t NH₃)</summary>
    public const double GreyCo2PerTonne = 1.9;

    private const double TickSeconds = 0.5; // 500 ms per tick

    // --- state ---------------------------------------------------------------
    /// <summary>Operator power-draw set-point (0..MaxDrawMW), independent of what the reactor can supply.</summary>
    public double SetpointMW { get; private set; } = DefaultDrawMW;
    /// <summary>Whether the operator has started the plant. When stopped the loop depressurises.</summary>
    public bool Running { get; private set; }

    /// <summary>Actual power drawn this tick (limited by both the set-point and available reactor MW). (MW)</summary>
    public double DrawnMW { get; private set; }
    /// <summary>Current synthesis-loop pressure. (bar)</summary>
    public double LoopPressureBar { get; private set; } = AmbientBar;
    /// <summary>Instantaneous electrolyzer hydrogen output. (kg H₂ / hour)</summary>
    public double H2RateKgPerHour { get; private set; }
    /// <summary>Instantaneous production rate. (tonnes NH₃ / hour)</summary>
    public double TonnesPerHour { get; private set; }
    /// <summary>Lifetime ammonia produced. (tonnes)</summary>
    public double TotalTonnes { get; private set; }
    /// <summary>Lifetime CO₂ avoided vs grey (SMR) ammonia for the same output. (tonnes)</summary>
    public double Co2AvoidedTonnes => TotalTonnes * GreyCo2PerTonne;

    /// <summary>True when the plant is actually energised (running AND reactor supplying power).</summary>
    public bool Powered => Running && DrawnMW > 1.0;
    /// <summary>True once the loop is pressurised enough to synthesise ammonia.</summary>
    public bool Synthesizing => LoopPressureBar >= SynthesisThresholdBar;
    /// <summary>Smallest steady draw that can hold the loop at the synthesis threshold. (MW)</summary>
    public static double MinimumSynthesisDrawMW =>
        MaxDrawMW * (SynthesisThresholdBar - AmbientBar) / (LoopPressureMaxBar - AmbientBar);

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

    /// <summary>Start the plant (electrolyzers + compressors on).</summary>
    public void Start() { Running = true; }

    /// <summary>Stop the plant (loop depressurises toward ambient).</summary>
    public void Stop() { Running = false; }

    /// <summary>Reset all counters and depressurise the loop fully.</summary>
    public void Reset()
    {
        SetpointMW = DefaultDrawMW;
        Running = false;
        DrawnMW = 0;
        LoopPressureBar = AmbientBar;
        H2RateKgPerHour = 0;
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
            double dt = 0;
            if (_lastTick == int.MinValue)
            {
                dt = TickSeconds;
            }
            else
            {
                int delta = tick - _lastTick;
                if (delta > 0) dt = Math.Clamp(delta * TickSeconds, TickSeconds, 5.0);
            }
            _lastTick = tick;

            if (!double.IsFinite(availableMW) || availableMW < 0) availableMW = 0;

            // How much power actually reaches the plant: min(set-point, available), only if running & generating.
            double want = Running && generating ? SetpointMW : 0;
            DrawnMW = Math.Min(want, availableMW);
            if (DrawnMW < 0) DrawnMW = 0;

            // --- synthesis-loop pressure model (first-order toward a driven target) ---
            // Pressurising: more drawn MW drives the compressor equilibrium pressure higher (up to loop max).
            // Depressurising: without power the loop bleeds down toward ambient. Tuned for a visible ramp.
            if (DrawnMW > 1.0)
            {
                double drive = Math.Clamp(DrawnMW / MaxDrawMW, 0, 1);
                double target = AmbientBar + (LoopPressureMaxBar - AmbientBar) * drive;
                double k = 0.05 * dt; // pressurisation rate constant
                LoopPressureBar += (target - LoopPressureBar) * Math.Clamp(k, 0, 1);
            }
            else
            {
                double k = 0.025 * dt; // slower bleed-down
                LoopPressureBar += (AmbientBar - LoopPressureBar) * Math.Clamp(k, 0, 1);
            }
            LoopPressureBar = Math.Clamp(LoopPressureBar, AmbientBar, LoopPressureMaxBar + 5);

            // --- electrolyzer hydrogen output (feeds the loop; ∝ electrolyzer share of drawn MW) ---
            H2RateKgPerHour = DrawnMW > 1.0
                ? DrawnMW * ElectrolyzerPowerShare * 1000.0 / ElectrolyzerKWhPerKgH2
                : 0;

            // --- production: ammonia only above threshold pressure; rate ∝ pressure above threshold ---
            if (LoopPressureBar > SynthesisThresholdBar && DrawnMW > 1.0)
            {
                double above = (LoopPressureBar - SynthesisThresholdBar) / (LoopPressureMaxBar - SynthesisThresholdBar);
                above = Math.Clamp(above, 0, 1);
                // Peak throughput at full loop pressure & full draw (350 MW / 10 MWh-per-tonne = 35 t/h).
                TonnesPerHour = (MaxDrawMW / MWhPerTonne) * above * Math.Clamp(DrawnMW / MaxDrawMW, 0, 1);
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
