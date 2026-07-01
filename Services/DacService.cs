using System;

namespace WinForge.Services;

/// <summary>
/// 直接空氣捕集廠 · Direct Air Capture (DAC) plant — a reactor-powered carbon-scrubbing load. Giant contactor
/// fans pull ambient air across a sorbent that binds atmospheric CO₂; the sorbent is then regenerated with the
/// nuclear station's megawatts (DAC is very energy-intensive). CO₂ captured (tonnes/hour) is proportional to the
/// drawn power divided by the specific energy, only while the reactor is generating. Captured CO₂ accumulates in
/// permanent storage (lifetime total) and earns carbon credits, which are deposited into the shared reactor
/// economy (⚡). Idle whenever the reactor is not generating. Pure managed C#, thread-agnostic, never throws.
/// </summary>
public sealed class DacService
{
    // ── plant constants ──────────────────────────────────────────────────────
    public const double ReactorMaxMWe = 1150.0;   // station nameplate output
    public const double PlantMaxDrawMW = 500.0;    // operator can request up to this

    // Specific energy of direct air capture. Real DAC needs roughly 1.5–2.5 MWh of energy per tonne of CO₂;
    // pick 2.0 MWh/t. Capture rate (t/h) = drawn MW / (MWh per tonne).
    public const double SpecificEnergyMWhPerTonne = 2.0;

    // A typical passenger car emits ~4.6 tonnes CO₂ per year; used for the "cars offset/year" equivalence.
    public const double TonnesCo2PerCarPerYear = 4.6;

    // Economy: carbon credits earned per tonne of CO₂ captured (1 credit ≈ 1 tonne), each worth this many ⚡.
    public const double WattsPerTonne = 0.5;

    // ── operator inputs ──────────────────────────────────────────────────────
    /// <summary>運行/閒置 · Whether the DAC contactor fans are running.</summary>
    public bool Running { get; set; }

    private double _requestedMW;
    /// <summary>要求功率 (MW) · Requested power draw, clamped to [0, PlantMaxDrawMW].</summary>
    public double RequestedDrawMW
    {
        get => _requestedMW;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            _requestedMW = Math.Clamp(value, 0.0, PlantMaxDrawMW);
        }
    }

    // ── live reactor readouts (mirrored from the snapshot each tick) ─────────
    public double ReactorAvailableMW { get; private set; }
    public string ReactorMode { get; private set; } = "5";
    public bool PowerAvailable { get; private set; }

    // ── plant state ──────────────────────────────────────────────────────────
    public double DrawnMW { get; private set; }
    public double RateTonnesPerHour { get; private set; }
    public double TotalCapturedTonnes { get; private set; }

    /// <summary>風扇轉速 0..1 · Fan spin fraction (spins up when capturing, spins down when idle).</summary>
    public double FanSpin { get; private set; }

    /// <summary>累計已入賬（避免重複計數）· Total tonnes already deposited to the economy (dedupe guard).</summary>
    private double _depositedTonnes;
    private double _sinceDepositSeconds;

    /// <summary>比能耗 (MWh/t) · Specific energy: constant for this DAC train.</summary>
    public double SpecificEnergyMWhPerT => SpecificEnergyMWhPerTonne;

    /// <summary>running "carbon credits" balance (1 credit ≈ 1 tonne captured).</summary>
    public double CarbonCredits => TotalCapturedTonnes;

    /// <summary>等效汽車 · Cars whose annual emissions this lifetime capture offsets.</summary>
    public double CarsOffsetPerYear =>
        TonnesCo2PerCarPerYear <= 0 ? 0 : TotalCapturedTonnes / TonnesCo2PerCarPerYear;

    /// <summary>
    /// Advance the simulation by <paramref name="dtSeconds"/> using the live reactor snapshot.
    /// <paramref name="snap"/> is a non-nullable value struct — its fields are read directly.
    /// </summary>
    public void Tick(double dtSeconds, ReactorStatusSnapshot snap)
    {
        try
        {
            if (double.IsNaN(dtSeconds) || dtSeconds <= 0) dtSeconds = 0;
            dtSeconds = Math.Clamp(dtSeconds, 0.0, 1.0);

            // Mirror the reactor.
            double electricMW = snap.ElectricMW;
            if (double.IsNaN(electricMW) || electricMW < 0) electricMW = 0;
            string mode = snap.Mode ?? "";
            ReactorMode = string.IsNullOrWhiteSpace(mode) ? "5" : mode;

            bool cold = mode.Contains("5") || mode.ToLowerInvariant().Contains("cold");
            bool generating = snap.IsGenerating && electricMW > 1 && !snap.IsScrammed && !snap.IsMeltdown && !cold;
            PowerAvailable = generating;
            ReactorAvailableMW = generating ? electricMW : 0;

            bool capturing = Running && generating;

            if (capturing)
            {
                // Draw is clamped to whatever the station can actually spare.
                DrawnMW = Math.Clamp(RequestedDrawMW, 0, Math.Min(PlantMaxDrawMW, electricMW));
                // t/h = MW / (MWh per tonne).  (MW × h = MWh; MWh ÷ MWh/t = tonnes.)
                RateTonnesPerHour = SpecificEnergyMWhPerTonne <= 0 ? 0 : DrawnMW / SpecificEnergyMWhPerTonne;

                double capturedTonnes = RateTonnesPerHour * (dtSeconds / 3600.0);
                if (capturedTonnes > 0) TotalCapturedTonnes += capturedTonnes;

                // fans spin up
                FanSpin = Math.Clamp(FanSpin + dtSeconds * 1.0, 0, 1);
            }
            else
            {
                DrawnMW = 0;
                RateTonnesPerHour = 0;
                // fans spin down
                FanSpin = Math.Clamp(FanSpin - dtSeconds * 0.6, 0, 1);
            }

            // ── economy deposit — carbon credits (in increments, not every tick) ──
            _sinceDepositSeconds += dtSeconds;
            double undeposited = TotalCapturedTonnes - _depositedTonnes;
            if (undeposited >= 1.0 || (_sinceDepositSeconds >= 3.0 && undeposited > 0))
            {
                _sinceDepositSeconds = 0;
                double watts = undeposited * WattsPerTonne;
                if (watts > 0)
                {
                    _depositedTonnes = TotalCapturedTonnes;
                    try { ReactorEconomyService.I.Earn(watts, Loc.I.Pick("Carbon credits", "碳信用")); } catch { }
                }
            }
        }
        catch { /* never throw from the sim tick */ }
    }

    /// <summary>重設 · Reset the plant to a cold, empty state (keeps economy deposits intact).</summary>
    public void Reset()
    {
        try
        {
            Running = false;
            _requestedMW = 0;
            DrawnMW = 0;
            RateTonnesPerHour = 0;
            TotalCapturedTonnes = 0;
            _depositedTonnes = 0;
            _sinceDepositSeconds = 0;
            FanSpin = 0;
            PowerAvailable = false;
            ReactorAvailableMW = 0;
        }
        catch { }
    }
}
