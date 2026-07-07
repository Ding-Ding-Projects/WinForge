using System;

namespace WinForge.Services;

/// <summary>
/// 海水淡化廠 · Seawater Desalination Plant — a reactor-powered industrial load. A reverse-osmosis train
/// draws megawatts from the flagship nuclear station and turns seawater into potable fresh water (m³/h),
/// filling a storage tank. Produces only while the reactor is generating; idle otherwise. The plant deposits
/// its output into the shared reactor economy (⚡). Pure managed C#, thread-agnostic, never throws.
/// </summary>
public sealed class DesalService
{
    // ── plant constants ──────────────────────────────────────────────────────
    public const double ReactorMaxMWe = 1150.0;   // station nameplate output
    public const double PlantMaxDrawMW = 400.0;    // operator can request up to this

    // Reverse-osmosis yield: cubic metres of fresh water produced per MWh of electrical energy.
    // Real seawater RO uses ~3–4 kWh/m³ ⇒ ~250–330 m³/MWh; pick 280 m³/MWh (specific energy ≈ 3.57 kWh/m³).
    public const double YieldM3PerMWh = 280.0;

    public const double TankCapacityM3 = 50000.0;  // potable storage tank

    // Economy: ⚡ earned per m³ of desalinated water sold.
    public const double WattsPerM3 = 0.05;

    // ── operator inputs ──────────────────────────────────────────────────────
    /// <summary>運行/閒置 · Whether the RO train is running.</summary>
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
    public double RateM3PerHour { get; private set; }
    public double TankM3 { get; private set; }
    public double TotalProducedM3 { get; private set; }

    /// <summary>累計已入賬（避免重複計數）· Total m³ already deposited to the economy (dedupe guard).</summary>
    private double _depositedM3;
    private double _sinceDepositSeconds;

    public double TankFillFraction => TankCapacityM3 <= 0 ? 0 : Math.Clamp(TankM3 / TankCapacityM3, 0, 1);

    /// <summary>比能耗 (kWh/m³) · Specific energy: constant for this RO train.</summary>
    public double SpecificEnergyKWhPerM3 => YieldM3PerMWh <= 0 ? 0 : 1000.0 / YieldM3PerMWh;

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

            if (Running && generating)
            {
                // Draw is clamped to whatever the station can actually spare.
                DrawnMW = Math.Clamp(RequestedDrawMW, 0, Math.Min(PlantMaxDrawMW, electricMW));
                // m³/h = MW * (m³ per MWh)  (MW × hours = MWh; here rate is per-hour).
                RateM3PerHour = DrawnMW * YieldM3PerMWh;

                double producedM3 = RateM3PerHour * (dtSeconds / 3600.0);
                double room = Math.Max(0, TankCapacityM3 - TankM3);
                if (producedM3 > room) producedM3 = room; // don't overfill
                if (producedM3 > 0)
                {
                    TankM3 += producedM3;
                    TotalProducedM3 += producedM3;
                }
            }
            else
            {
                DrawnMW = 0;
                RateM3PerHour = 0;
            }

            // ── economy deposit (in increments, not every tick) ──────────────
            _sinceDepositSeconds += dtSeconds;
            double undeposited = TotalProducedM3 - _depositedM3;
            if (undeposited >= 1.0 || (_sinceDepositSeconds >= 3.0 && undeposited > 0))
            {
                _sinceDepositSeconds = 0;
                double watts = undeposited * WattsPerM3;
                if (watts > 0)
                {
                    _depositedM3 = TotalProducedM3;
                    try { ReactorEconomyService.I.Earn(watts, Loc.I.Pick("Desalinated water", "淡化食水")); } catch { }
                }
            }
        }
        catch { /* never throw from the sim tick */ }
    }

    /// <summary>倒空儲水缸 · Empty the potable-water tank (water sold/consumed).</summary>
    public void EmptyTank() { try { TankM3 = 0; } catch { } }

    /// <summary>重設 · Reset the plant to a cold, empty state (keeps economy deposits intact).</summary>
    public void Reset()
    {
        try
        {
            Running = false;
            _requestedMW = 0;
            DrawnMW = 0;
            RateM3PerHour = 0;
            TankM3 = 0;
            TotalProducedM3 = 0;
            _depositedM3 = 0;
            _sinceDepositSeconds = 0;
            PowerAvailable = false;
            ReactorAvailableMW = 0;
        }
        catch { }
    }
}
