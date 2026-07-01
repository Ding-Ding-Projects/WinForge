using System;

namespace WinForge.Services;

/// <summary>
/// 垂直農場（補光燈陣列）· Vertical-farm (grow-light array) simulation core. A reactor-powered indoor farm:
/// the operator dials a power draw (MW) that runs LED grow-lights + HVAC over a canopy. Crops accrue growth
/// while adequately lit and powered (growth ∝ delivered light within a cap, gated by a simple photoperiod
/// day/night cycle driven off an integer tick counter). When the reactor cannot power the lights the lights
/// go out, growth pauses, and produce may slightly spoil. Sells harvested produce into the shared reactor
/// economy (⚡). Pure managed C#, thread-agnostic (single-threaded UI tick), never throws.
/// </summary>
public sealed class VertFarmService
{
    // ── station / capacity constants ──
    public const double ReactorMaxMWe = 1150.0;   // flagship station electrical output
    public const double FarmMaxDrawMW = 200.0;    // operator power-draw cap (LEDs + HVAC)
    public const double MinDrawMW = 0.0;

    // Canopy: each MW of grow-light power lights this many m² of canopy and this many light fixtures.
    private const double AreaPerMW = 45.0;        // m² of canopy lit per delivered MW
    private const double LightsPerMW = 320.0;     // LED grow-light fixtures per delivered MW

    // HVAC overhead: a fraction of drawn power keeps the environment conditioned rather than growing plants.
    private const double HvacFraction = 0.18;

    // Growth model: growth-percent per second at full effective light, capped so extra light saturates.
    private const double GrowthPctPerSecAtCap = 0.55; // ~ light-saturated growth
    private const double LightCapMW = FarmMaxDrawMW;  // light beyond this doesn't grow faster (saturation)

    // Photoperiod: ~18h light / 6h dark scaled onto the tick counter (each cycle = PhotoCycleTicks).
    private const int PhotoCycleTicks = 240;          // one simulated day = 240 ticks (~2 min real @ 0.5s)
    private const int PhotoLightTicks = 180;          // 180/240 = 18h "on"

    // Yield: kg of produce per m² of canopy per completed harvest.
    private const double YieldKgPerM2 = 3.2;
    // Spoilage: fraction of standing growth lost per second when the lights are out.
    private const double SpoilPctPerSec = 0.05;

    // Economy: ⚡ earned per kg of produce sold.
    private const double WattsPerKg = 0.04;

    // ── operator-set state ──
    public bool Running { get; set; }
    private double _requestedDrawMW = 90.0;
    public double RequestedDrawMW
    {
        get => _requestedDrawMW;
        set => _requestedDrawMW = double.IsNaN(value) ? _requestedDrawMW : Math.Clamp(value, MinDrawMW, FarmMaxDrawMW);
    }

    // ── live readouts (last tick) ──
    public double ReactorAvailableMW { get; private set; }
    public string ReactorMode { get; private set; } = "5";
    public bool PowerAvailable { get; private set; }
    public double DrawnMW { get; private set; }          // power actually delivered to the farm
    public double GrowLightMW { get; private set; }       // portion driving the LEDs (after HVAC)
    public int ActiveLights { get; private set; }
    public double CanopyAreaM2 { get; private set; }
    public double GrowthPct { get; private set; }         // 0..100 toward the current harvest
    public bool LightsOn { get; private set; }
    public bool DayPhase { get; private set; }            // photoperiod: true = lights-on window

    // ── cumulative results ──
    public int HarvestsCompleted { get; private set; }
    public double TotalYieldKg { get; private set; }
    public double DepositedWatts { get; private set; }

    private double _pendingSaleKg;       // harvested kg awaiting ⚡ deposit
    private double _earnCarrySeconds;    // throttle economy deposits (~3s)

    /// <summary>true once growth has reached the harvest threshold.</summary>
    public bool ReadyToHarvest => GrowthPct >= 100.0 - 1e-6;

    /// <summary>Advance the sim by <paramref name="dtSeconds"/> using the live reactor snapshot.</summary>
    public void Tick(double dtSeconds, ReactorStatusSnapshot snap, int tick)
    {
        try
        {
            if (double.IsNaN(dtSeconds) || dtSeconds <= 0) dtSeconds = 0.5;
            dtSeconds = Math.Clamp(dtSeconds, 0, 5);

            ReactorAvailableMW = double.IsNaN(snap.ElectricMW) ? 0 : Math.Max(0, snap.ElectricMW);
            ReactorMode = string.IsNullOrWhiteSpace(snap.Mode) ? "5" : snap.Mode;

            string modeLo = ReactorMode.ToLowerInvariant();
            bool coldMode = modeLo.Contains("5") || modeLo.Contains("cold");
            bool generating = snap.IsGenerating && snap.ElectricMW > 1 && !snap.IsScrammed && !snap.IsMeltdown && !coldMode;
            PowerAvailable = generating;

            // Photoperiod window (independent of run state so the UI can show day/night).
            DayPhase = (tick % PhotoCycleTicks) < PhotoLightTicks;

            if (!Running || !PowerAvailable)
            {
                LightsOn = false;
                DrawnMW = 0; GrowLightMW = 0; ActiveLights = 0; CanopyAreaM2 = 0;

                // Lights out → slight spoilage of the standing crop (only meaningful while it has grown).
                if (GrowthPct > 0)
                    GrowthPct = Math.Max(0, GrowthPct - SpoilPctPerSec * dtSeconds);

                DepositPending(dtSeconds);
                return;
            }

            // Deliver requested power, capped by what the reactor actually has available.
            DrawnMW = Math.Clamp(Math.Min(RequestedDrawMW, ReactorAvailableMW), 0, FarmMaxDrawMW);
            GrowLightMW = Math.Max(0, DrawnMW * (1.0 - HvacFraction));

            ActiveLights = (int)Math.Round(GrowLightMW * LightsPerMW);
            CanopyAreaM2 = GrowLightMW * AreaPerMW;

            // Lights only actually illuminate the canopy during the "day" photoperiod window.
            LightsOn = DayPhase && GrowLightMW > 0.01;

            if (LightsOn)
            {
                double effLight = Math.Min(GrowLightMW, LightCapMW) / LightCapMW; // 0..1 saturating
                GrowthPct = Math.Min(100.0, GrowthPct + GrowthPctPerSecAtCap * effLight * dtSeconds);
            }
            // During the dark window with power still on: no growth, no spoilage (rest period).

            DepositPending(dtSeconds);
        }
        catch { /* never throw from the sim tick */ }
    }

    /// <summary>Harvest the standing crop if ready; converts canopy area → kg and queues it for sale.</summary>
    public bool Harvest()
    {
        try
        {
            if (!ReadyToHarvest || CanopyAreaM2 <= 0) return false;
            double kg = CanopyAreaM2 * YieldKgPerM2;
            if (double.IsNaN(kg) || kg <= 0) return false;

            HarvestsCompleted++;
            TotalYieldKg += kg;
            _pendingSaleKg += kg;
            GrowthPct = 0; // replant
            return true;
        }
        catch { return false; }
    }

    public void Reset()
    {
        Running = false;
        GrowthPct = 0;
        HarvestsCompleted = 0;
        TotalYieldKg = 0;
        DepositedWatts = 0;
        _pendingSaleKg = 0;
        _earnCarrySeconds = 0;
        DrawnMW = 0; GrowLightMW = 0; ActiveLights = 0; CanopyAreaM2 = 0;
        LightsOn = false;
    }

    /// <summary>kg of harvested produce still waiting to be sold for ⚡.</summary>
    public double PendingSaleKg => _pendingSaleKg;

    // Deposit accrued produce sales into the shared economy in increments (~every 3s, ≥1 ⚡).
    private void DepositPending(double dtSeconds)
    {
        try
        {
            if (_pendingSaleKg <= 0) return;
            _earnCarrySeconds += dtSeconds;

            double potential = _pendingSaleKg * WattsPerKg;
            if (_earnCarrySeconds < 3.0 && potential < 1.0) return;
            _earnCarrySeconds = 0;

            double watts = _pendingSaleKg * WattsPerKg;
            if (watts <= 0) return;

            _pendingSaleKg = 0;
            DepositedWatts += watts;
            ReactorEconomyService.I.Earn(watts, Loc.I.Pick("Vertical-farm produce", "垂直農場農產"));
        }
        catch { /* best-effort economy deposit */ }
    }
}
