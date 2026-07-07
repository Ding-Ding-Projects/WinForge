using System;

namespace WinForge.Services;

/// <summary>
/// 區域供熱（熱電聯產）· District Heating cogeneration plant — a reactor-powered thermal load. The station
/// taps electrical power plus waste heat to feed a city hot-water network. The operator sets a heat demand /
/// power draw (MW-thermal drawn) and a target supply temperature; the outdoor temperature raises demand when
/// cold. Delivered heat (MW-th) is proportional to the drawn power times a cogeneration efficiency, homes
/// heated scale with delivered heat, and heat sales deposit into the shared reactor economy (⚡). Runs only
/// while the reactor is generating; otherwise the district goes cold. Pure managed C#, thread-agnostic,
/// never throws.
/// </summary>
public sealed class DistrictHeatService
{
    // ── plant constants ──────────────────────────────────────────────────────
    public const double ReactorMaxMWe = 1150.0;    // station nameplate electrical output
    public const double PlantMaxDrawMW = 300.0;    // operator can request up to 300 MW drawn

    // Cogeneration efficiency: fraction of drawn power (plus recovered waste heat) delivered as network heat.
    // CHP recovers far more than pure electrical draw, so effective delivery can exceed 1.0.
    public const double CogenEfficiency = 1.25;

    // Homes served per MW-thermal delivered (~1 MW-th per ~40 homes).
    public const double HomesPerMWth = 40.0;

    // Network water temperature envelope.
    public const double ReturnTempC = 45.0;        // return leg temperature (cool side)
    public const double MinSupplyC = 60.0;
    public const double MaxSupplyC = 120.0;

    // Outdoor temperature envelope for the demand model.
    public const double MinOutdoorC = -20.0;
    public const double MaxOutdoorC = 15.0;

    // Economy: ⚡ earned per MWh-thermal of heat sold.
    public const double WattsPerMWhTh = 0.6;

    // ── operator inputs ──────────────────────────────────────────────────────
    /// <summary>運行/閒置 · Whether the district-heating loop is running.</summary>
    public bool Running { get; set; }

    private double _requestedMW;
    /// <summary>要求抽取功率 (MW) · Requested thermal power draw, clamped to [0, PlantMaxDrawMW].</summary>
    public double RequestedDrawMW
    {
        get => _requestedMW;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            _requestedMW = Math.Clamp(value, 0.0, PlantMaxDrawMW);
        }
    }

    private double _targetSupplyC = 85.0;
    /// <summary>目標供水溫度 (°C) · Target network supply temperature, clamped to [MinSupplyC, MaxSupplyC].</summary>
    public double TargetSupplyC
    {
        get => _targetSupplyC;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            _targetSupplyC = Math.Clamp(value, MinSupplyC, MaxSupplyC);
        }
    }

    private double _outdoorC = 0.0;
    /// <summary>室外氣溫 (°C) · Outdoor temperature; colder means more demand.</summary>
    public double OutdoorC
    {
        get => _outdoorC;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return;
            _outdoorC = Math.Clamp(value, MinOutdoorC, MaxOutdoorC);
        }
    }

    // ── live reactor readouts (mirrored from the snapshot each tick) ─────────
    public double ReactorAvailableMW { get; private set; }
    public string ReactorMode { get; private set; } = "5";
    public bool PowerAvailable { get; private set; }

    // ── plant state ──────────────────────────────────────────────────────────
    public double DrawnMW { get; private set; }
    public double DeliveredMWth { get; private set; }
    public double DemandMWth { get; private set; }
    public double SupplyTempC { get; private set; } = ReturnTempC;
    public int HomesHeated { get; private set; }
    public double TotalDeliveredMWhTh { get; private set; }
    public bool ColdHomes { get; private set; }

    /// <summary>累計已入賬（避免重複計數）· MWh-thermal already deposited to the economy (dedupe guard).</summary>
    private double _depositedMWhTh;
    private double _sinceDepositSeconds;

    /// <summary>需求覆蓋率 · Fraction of demand actually met (0..1).</summary>
    public double DemandCoverage => DemandMWth <= 0 ? 1.0 : Math.Clamp(DeliveredMWth / DemandMWth, 0, 1);

    /// <summary>
    /// 需求模型 · Heat demand (MW-th) implied by outdoor temperature and requested draw. Colder outdoor air
    /// raises the demand fraction; the requested draw sets the plant's nominal capacity target.
    /// </summary>
    public double ComputeDemandMWth()
    {
        // Cold fraction: 1.0 at MinOutdoorC (very cold), ~0.15 at MaxOutdoorC (mild).
        double span = MaxOutdoorC - MinOutdoorC;
        double coldFrac = span <= 0 ? 1.0 : Math.Clamp((MaxOutdoorC - _outdoorC) / span, 0, 1);
        double demandFactor = 0.15 + 0.85 * coldFrac; // 0.15..1.0
        return RequestedDrawMW * CogenEfficiency * demandFactor;
    }

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

            // Mirror the reactor (read struct fields directly — non-nullable value struct).
            double electricMW = snap.ElectricMW;
            if (double.IsNaN(electricMW) || electricMW < 0) electricMW = 0;
            string mode = snap.Mode ?? "";
            ReactorMode = string.IsNullOrWhiteSpace(mode) ? "5" : mode;

            bool cold = mode.Contains("5") || mode.ToLowerInvariant().Contains("cold");
            bool generating = snap.IsGenerating && electricMW > 1 && !snap.IsScrammed && !snap.IsMeltdown && !cold;
            PowerAvailable = generating;
            ReactorAvailableMW = generating ? electricMW : 0;

            DemandMWth = ComputeDemandMWth();

            if (Running && generating)
            {
                // Draw is clamped to whatever the station can actually spare.
                DrawnMW = Math.Clamp(RequestedDrawMW, 0, Math.Min(PlantMaxDrawMW, electricMW));
                // Heat delivered (MW-th) ∝ drawn power × cogeneration efficiency, capped by demand.
                double maxDeliverable = DrawnMW * CogenEfficiency;
                DeliveredMWth = Math.Min(maxDeliverable, DemandMWth);

                // Supply temperature approaches the operator target when demand is met, else sags toward return.
                double cov = DemandCoverage;
                double reached = ReturnTempC + (_targetSupplyC - ReturnTempC) * cov;
                // Smooth the temperature toward its reached value.
                double blend = Math.Clamp(dtSeconds / 2.0, 0, 1);
                SupplyTempC += (reached - SupplyTempC) * blend;

                HomesHeated = (int)Math.Max(0, DeliveredMWth * HomesPerMWth);
                ColdHomes = DemandMWth > 0 && DeliveredMWth < DemandMWth - 0.5;

                double deliveredMWhTh = DeliveredMWth * (dtSeconds / 3600.0);
                if (deliveredMWhTh > 0) TotalDeliveredMWhTh += deliveredMWhTh;
            }
            else
            {
                DrawnMW = 0;
                DeliveredMWth = 0;
                HomesHeated = 0;
                // District cools toward return temperature when not delivering heat.
                double blend = Math.Clamp(dtSeconds / 6.0, 0, 1);
                SupplyTempC += (ReturnTempC - SupplyTempC) * blend;
                ColdHomes = Running && DemandMWth > 0; // wants heat but can't deliver
            }

            // ── economy deposit (in increments, not every tick) ──────────────
            _sinceDepositSeconds += dtSeconds;
            double undeposited = TotalDeliveredMWhTh - _depositedMWhTh;
            if (undeposited >= 1.0 || (_sinceDepositSeconds >= 3.0 && undeposited > 0))
            {
                _sinceDepositSeconds = 0;
                double watts = undeposited * WattsPerMWhTh;
                if (watts > 0)
                {
                    _depositedMWhTh = TotalDeliveredMWhTh;
                    try { ReactorEconomyService.I.Earn(watts, Loc.I.Pick("District heat sales", "區域供熱收入")); } catch { }
                }
            }
        }
        catch { /* never throw from the sim tick */ }
    }

    /// <summary>重設 · Reset the plant to a cold, idle state (keeps economy deposits intact).</summary>
    public void Reset()
    {
        try
        {
            Running = false;
            _requestedMW = 0;
            _targetSupplyC = 85.0;
            _outdoorC = 0.0;
            DrawnMW = 0;
            DeliveredMWth = 0;
            DemandMWth = 0;
            SupplyTempC = ReturnTempC;
            HomesHeated = 0;
            TotalDeliveredMWhTh = 0;
            ColdHomes = false;
            _depositedMWhTh = 0;
            _sinceDepositSeconds = 0;
            PowerAvailable = false;
            ReactorAvailableMW = 0;
        }
        catch { }
    }
}
