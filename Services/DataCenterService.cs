using System;

namespace WinForge.Services;

/// <summary>
/// 核能資料中心引擎 · Nuclear Data Center engine — a pure-managed simulation of a nuclear-powered
/// hyperscale data centre that is a HEAVY reactor-powered load. The operator sets an IT load (MW of
/// servers); the total facility draw is IT × PUE (power usage effectiveness), where cooling overhead
/// grows worse when the reactor is stressed / hot. Requests served per second scale with online rack
/// capacity. Uptime/SLA stays ~99.99 % while the reactor can supply the full draw, but DEGRADES —
/// racks are shed — when reactor output cannot meet demand, and the SLA figure bleeds downward.
///
/// All timing for the simulation is driven by an internal integer tick counter (NEVER wall-clock), so
/// results are deterministic and reproducible. Every method is exception-safe and never throws.
/// </summary>
public sealed class DataCenterService
{
    /// <summary>Maximum IT (server) load the operator can request, in MW.</summary>
    public const double MaxItLoadMW = 500.0;

    /// <summary>Best-case cooling overhead — total draw = IT × this when the plant is healthy.</summary>
    public const double BasePue = 1.20;

    /// <summary>Nominal / advertised SLA (%).</summary>
    public const double NominalUptime = 99.99;

    private long _tick;                 // deterministic internal step counter (NOT wall-clock)

    // ---------------------------------------------------------------- live readouts ----

    /// <summary>Requested IT load (MW of servers) — the operator's setpoint.</summary>
    public double ItLoadMW { get; private set; }

    /// <summary>Power Usage Effectiveness — total draw / IT load. 1.20 healthy, worse when stressed.</summary>
    public double Pue { get; private set; } = BasePue;

    /// <summary>Total facility electrical draw including cooling (MW) = IT × PUE.</summary>
    public double TotalDrawMW { get; private set; }

    /// <summary>Power the reactor is actually able to supply to the facility this instant (MW).</summary>
    public double SuppliedMW { get; private set; }

    /// <summary>Fraction (0..1) of the requested IT load that is currently online (powered).</summary>
    public double OnlineFraction { get; private set; } = 1.0;

    /// <summary>IT capacity currently online / running (MW).</summary>
    public double OnlineItMW { get; private set; }

    /// <summary>IT capacity shed / dark because power is insufficient (MW).</summary>
    public double ShedItMW { get; private set; }

    /// <summary>Requests served per second, proportional to online rack capacity.</summary>
    public double RequestsPerSec { get; private set; }

    /// <summary>Rolling SLA / uptime figure (%). Holds ~99.99 healthy; bleeds when starved.</summary>
    public double UptimePercent { get; private set; } = NominalUptime;

    /// <summary>Total racks (derived from IT load; ~5 MW per rack-row block for display).</summary>
    public int TotalRacks { get; private set; }

    /// <summary>Racks currently online.</summary>
    public int OnlineRacks { get; private set; }

    /// <summary>Racks currently shed (offline for lack of power).</summary>
    public int ShedRacks { get; private set; }

    /// <summary>Cumulative requests served since last reset.</summary>
    public double TotalRequestsServed { get; private set; }

    /// <summary>Whether the facility is currently running on nuclear power (reactor generating).</summary>
    public bool OnNuclearPower { get; private set; }

    // requests-per-MW-of-online-IT, per second (each MW of servers ~ 3,200 req/s here).
    private const double ReqsPerMwPerSec = 3200.0;
    private const double MwPerRack = 5.0;    // display granularity for rack counts

    /// <summary>Scale the IT load up by a step (MW), clamped to the maximum.</summary>
    public void ScaleUp(double stepMW) => ItLoadMW = Clamp(ItLoadMW + Abs(stepMW), 0, MaxItLoadMW);

    /// <summary>Scale the IT load down by a step (MW), clamped to zero.</summary>
    public void ScaleDown(double stepMW) => ItLoadMW = Clamp(ItLoadMW - Abs(stepMW), 0, MaxItLoadMW);

    /// <summary>Directly set the requested IT load (MW), clamped to valid range. NaN → 0.</summary>
    public void SetItLoad(double mw)
    {
        if (double.IsNaN(mw) || double.IsInfinity(mw)) mw = 0;
        ItLoadMW = Clamp(mw, 0, MaxItLoadMW);
    }

    /// <summary>Reset the rolling SLA/uptime figure back to nominal.</summary>
    public void ResetSla() => UptimePercent = NominalUptime;

    /// <summary>Full reset of the whole simulation.</summary>
    public void Reset()
    {
        _tick = 0;
        ItLoadMW = 0;
        Pue = BasePue;
        TotalDrawMW = 0;
        SuppliedMW = 0;
        OnlineFraction = 1.0;
        OnlineItMW = 0;
        ShedItMW = 0;
        RequestsPerSec = 0;
        UptimePercent = NominalUptime;
        TotalRacks = 0;
        OnlineRacks = 0;
        ShedRacks = 0;
        TotalRequestsServed = 0;
        OnNuclearPower = false;
    }

    /// <summary>
    /// Advance the simulation one step. <paramref name="dtSeconds"/> is the real elapsed time, used only
    /// to accrue served requests and to pace SLA drift; all sim logic keys off the internal tick counter.
    /// <paramref name="availableMW"/> is the live reactor electric output (what the plant can supply);
    /// <paramref name="generating"/> gates nuclear power (false while cold / MODE 5 / scrammed / melted).
    /// Never throws.
    /// </summary>
    public void Tick(double dtSeconds, double availableMW, bool generating)
    {
        try
        {
            if (double.IsNaN(dtSeconds) || double.IsInfinity(dtSeconds)) dtSeconds = 0.5;
            dtSeconds = Clamp(dtSeconds, 0.0, 2.0);
            _tick++;

            if (double.IsNaN(availableMW) || double.IsInfinity(availableMW) || availableMW < 0) availableMW = 0;
            OnNuclearPower = generating && availableMW > 1.0;

            // On generator reserve when the reactor isn't generating: a small diesel/battery reserve keeps
            // a sliver of capacity alive, but nowhere near the full facility draw.
            double reserveMW = 12.0;
            double supplyCap = OnNuclearPower ? availableMW : reserveMW;

            double it = Clamp(ItLoadMW, 0, MaxItLoadMW);

            // PUE worsens as the plant is pushed harder relative to its ~1150 MWe envelope (hotter → more
            // cooling). A gentle deterministic ripple over the tick counter mimics ambient/thermal drift.
            double stress = OnNuclearPower ? Clamp((1150.0 - availableMW) / 1150.0, 0, 1) : 1.0;
            double ripple = 0.02 * Math.Sin(_tick * 0.013);
            Pue = Clamp(BasePue + 0.35 * stress + ripple, BasePue, 1.9);

            // Requested total draw (IT + cooling).
            double requestedDraw = it * Pue;
            TotalDrawMW = requestedDraw;

            // How much can we actually power?
            double supplied = Math.Min(requestedDraw, supplyCap);
            SuppliedMW = supplied;

            // Online fraction: if supply can't meet the requested draw, we shed racks proportionally.
            double frac = requestedDraw > 0.001 ? Clamp(supplied / requestedDraw, 0, 1) : 1.0;
            OnlineFraction = frac;

            OnlineItMW = it * frac;
            ShedItMW = it - OnlineItMW;
            if (ShedItMW < 0) ShedItMW = 0;

            // Rack accounting for display.
            TotalRacks = (int)Math.Ceiling(it / MwPerRack);
            OnlineRacks = (int)Math.Floor(OnlineItMW / MwPerRack + 0.0001);
            if (OnlineRacks > TotalRacks) OnlineRacks = TotalRacks;
            if (OnlineRacks < 0) OnlineRacks = 0;
            ShedRacks = TotalRacks - OnlineRacks;
            if (ShedRacks < 0) ShedRacks = 0;

            // Requests served per second scale with ONLINE IT capacity only.
            RequestsPerSec = OnlineItMW * ReqsPerMwPerSec;
            TotalRequestsServed += RequestsPerSec * dtSeconds;

            // SLA / uptime: holds ~99.99 while fully powered; bleeds toward a floor set by how much of the
            // requested load we can actually keep online. Recovers slowly back to nominal when healthy.
            double slaTarget;
            if (it <= 0.001)
            {
                slaTarget = NominalUptime; // nothing requested → nothing to miss
            }
            else if (frac >= 0.999)
            {
                slaTarget = NominalUptime;
            }
            else
            {
                // Floor drops steeply as more of the fleet goes dark.
                slaTarget = Clamp(NominalUptime - (1.0 - frac) * 45.0, 0, NominalUptime);
            }

            // First-order approach; faster to bleed down than to heal back up, paced by real dt.
            double rate = (slaTarget < UptimePercent ? 0.9 : 0.15) * Clamp(dtSeconds / 0.5, 0.1, 4.0);
            UptimePercent += (slaTarget - UptimePercent) * Clamp(rate, 0, 1);
            UptimePercent = Clamp(UptimePercent, 0, NominalUptime);
        }
        catch
        {
            // Never throw from the sim tick.
        }
    }

    // ---------------------------------------------------------------- helpers ----
    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
    private static double Abs(double v) => v < 0 ? -v : v;
}
