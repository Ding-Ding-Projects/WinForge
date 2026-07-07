using System;

namespace WinForge.Services;

/// <summary>
/// 氫電解廠模型 · High-temperature electrolysis plant driven as a reactor-powered load.
/// The electrolyser stack must warm up before it produces efficiently: efficiency ramps from
/// 0 to full over many ticks while powered and decays when the reactor is not generating.
/// Produced hydrogen accumulates in a storage tank (kg, with a cap). Pure managed, never throws.
/// </summary>
public sealed class H2PlantService
{
    // --- fixed plant characteristics ---
    public const double PlantCapacityMW = 500.0;      // max load the electrolysers can draw
    public const double ReactorMaxMWe = 1150.0;       // full station output, for the "available" meter
    public const double PeakKgPerMWh = 20.0;          // kg H2 per MWh at full stack temperature
    public const double TankCapacityKg = 50000.0;     // storage tank cap

    // temperature model (arbitrary 0..1 "warmth", stands in for stack °C)
    private const double WarmUpPerSecond = 0.020;     // ~50 s of powered run to reach full warmth
    private const double CoolDownPerSecond = 0.035;   // cools faster than it warms when idle
    private const double AmbientWarmth = 0.0;

    // --- operator inputs ---
    /// <summary>Requested load in MW (clamped to available &amp; capacity each tick).</summary>
    public double RequestedLoadMW { get; set; }
    /// <summary>Operator Run/Idle switch. When idle the plant draws nothing.</summary>
    public bool Running { get; set; }

    // --- live state ---
    public long Ticks { get; private set; }
    public double Warmth { get; private set; }         // 0..1 stack temperature fraction
    public double DrawnMW { get; private set; }         // actual MW consumed this tick
    public double RateKgPerHour { get; private set; }   // instantaneous production rate
    public double TankKg { get; private set; }          // hydrogen currently stored
    public double TotalProducedKg { get; private set; } // lifetime production

    // --- reactor-derived, mirrored for the UI ---
    public double ReactorAvailableMW { get; private set; }
    public string ReactorMode { get; private set; } = "-";
    public bool ReactorGenerating { get; private set; }

    /// <summary>Stack temperature as a pseudo-°C reading for display (150 °C ambient → 850 °C hot).</summary>
    public double StackTempC => 150.0 + Warmth * 700.0;
    /// <summary>Efficiency in kg per MWh at the current warmth.</summary>
    public double EfficiencyKgPerMWh => PeakKgPerMWh * Warmth;
    public double TankFillFraction => TankCapacityKg <= 0 ? 0 : Math.Clamp(TankKg / TankCapacityKg, 0, 1);
    /// <summary>True when the reactor can actually supply the electrolysers.</summary>
    public bool PowerAvailable => ReactorGenerating && ReactorAvailableMW > 1.0 &&
        !string.Equals(ReactorMode, "5", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Advance the model by <paramref name="dt"/> seconds using a live reactor snapshot.
    /// Robust to nulls / NaN / a stopped reactor. Never throws.
    /// </summary>
    public void Tick(double dt, ReactorStatusSnapshot? reactor)
    {
        try
        {
            if (double.IsNaN(dt) || double.IsInfinity(dt)) dt = 0;
            dt = Math.Clamp(dt, 0, 1.0);
            Ticks++;

            // Read the reactor safely.
            double avail = 0;
            bool generating = false;
            string mode = "-";
            bool scrammed = false;
            if (reactor is { } r)
            {
                avail = double.IsNaN(r.ElectricMW) || double.IsInfinity(r.ElectricMW)
                    ? 0 : Math.Max(0, r.ElectricMW);
                generating = r.IsGenerating;
                mode = string.IsNullOrWhiteSpace(r.Mode) ? "-" : r.Mode;
                scrammed = r.IsScrammed;
            }
            ReactorAvailableMW = avail;
            ReactorMode = mode;
            // cold MODE 5 or a scram means no usable generation, whatever the flag says
            bool coldOrTripped = scrammed || string.Equals(mode, "5", StringComparison.OrdinalIgnoreCase);
            ReactorGenerating = generating && !coldOrTripped && avail > 1.0;

            // How much can we actually draw right now?
            bool powered = Running && ReactorGenerating;
            double want = Math.Clamp(
                double.IsNaN(RequestedLoadMW) ? 0 : RequestedLoadMW, 0, PlantCapacityMW);
            double ceiling = Math.Min(PlantCapacityMW, Math.Max(0, ReactorAvailableMW));
            DrawnMW = powered ? Math.Min(want, ceiling) : 0;

            // Stack temperature ramp: warms only while genuinely drawing power.
            if (DrawnMW > 1.0)
                Warmth += WarmUpPerSecond * dt;
            else
                Warmth -= CoolDownPerSecond * dt;
            Warmth = Math.Clamp(Warmth, AmbientWarmth, 1.0);

            // Production this tick.
            if (DrawnMW > 0.0 && Warmth > 0.0)
            {
                RateKgPerHour = DrawnMW * EfficiencyKgPerMWh; // MW * (kg/MWh) = kg/h
                double producedKg = RateKgPerHour * (dt / 3600.0);
                if (double.IsNaN(producedKg) || double.IsInfinity(producedKg)) producedKg = 0;
                double room = Math.Max(0, TankCapacityKg - TankKg);
                double stored = Math.Min(producedKg, room);
                TankKg = Math.Clamp(TankKg + stored, 0, TankCapacityKg);
                TotalProducedKg += stored;
            }
            else
            {
                RateKgPerHour = 0;
            }
        }
        catch
        {
            // never throw from the model
        }
    }

    /// <summary>Empty the storage tank (vent to the grid / trailer). Lifetime total is preserved.</summary>
    public void VentTank()
    {
        try { TankKg = 0; } catch { }
    }

    /// <summary>Full reset — clears warmth, tank, totals and stops the plant.</summary>
    public void Reset()
    {
        try
        {
            Running = false;
            RequestedLoadMW = 0;
            Warmth = 0;
            DrawnMW = 0;
            RateKgPerHour = 0;
            TankKg = 0;
            TotalProducedKg = 0;
        }
        catch { }
    }
}
