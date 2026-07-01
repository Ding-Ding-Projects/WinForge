using System;

namespace WinForge.Services;

/// <summary>
/// 電網調度中心引擎 · Grid Dispatch Center engine — a pure-managed simulated electricity market that
/// consumes live reactor output. Demand follows a deterministic sine over an internal tick counter
/// (never wall-clock), spot price rises with demand, and the operator sells a chosen setpoint of the
/// reactor's available MWe. Grid frequency drifts around 60 Hz with over/under supply. Never throws.
/// </summary>
public sealed class GridDispatchService
{
    // Grid scale (MW).
    public const double GridBaseMW = 620.0;    // baseline demand
    public const double GridSwingMW = 380.0;   // ± demand swing amplitude

    private long _tick;                 // deterministic internal step counter (NOT wall-clock)
    private bool _selling;

    /// <summary>Simulated grid frequency (Hz), nudged around 60 by supply/demand balance.</summary>
    public double FrequencyHz { get; private set; } = 60.0;

    /// <summary>Current grid demand (MW).</summary>
    public double DemandMW { get; private set; } = GridBaseMW;

    /// <summary>Current spot price ($/MWh).</summary>
    public double PriceUsdPerMWh { get; private set; } = 40.0;

    /// <summary>Power actually dispatched (sold) this instant (MW).</summary>
    public double DispatchedMW { get; private set; }

    /// <summary>Cumulative revenue earned ($).</summary>
    public double TotalRevenueUsd { get; private set; }

    /// <summary>Cumulative energy sold (MWh).</summary>
    public double TotalEnergyMWh { get; private set; }

    /// <summary>Whether the operator has armed selling.</summary>
    public bool Selling => _selling;

    public void StartSelling() => _selling = true;
    public void StopSelling() { _selling = false; DispatchedMW = 0; }

    public void Reset()
    {
        _tick = 0;
        _selling = false;
        FrequencyHz = 60.0;
        DemandMW = GridBaseMW;
        PriceUsdPerMWh = 40.0;
        DispatchedMW = 0;
        TotalRevenueUsd = 0;
        TotalEnergyMWh = 0;
    }

    /// <summary>
    /// Advance the simulated market one step. <paramref name="dtSeconds"/> is the real elapsed time used
    /// only to scale accrued energy/revenue; all market logic is driven by the internal tick counter so
    /// results are deterministic. <paramref name="setpointMW"/> is how much the operator wants to sell;
    /// <paramref name="availableMW"/> is the live reactor electric output; <paramref name="generating"/>
    /// gates accrual (false while cold / MODE 5 / scrammed). Never throws.
    /// </summary>
    public void Tick(double dtSeconds, double setpointMW, double availableMW, bool generating)
    {
        try
        {
            if (double.IsNaN(dtSeconds) || double.IsInfinity(dtSeconds)) dtSeconds = 0.5;
            dtSeconds = Math.Clamp(dtSeconds, 0.0, 2.0);
            _tick++;

            // Deterministic demand: two superposed sines over the tick counter for a plausible daily shape.
            double phase = _tick * 0.010;
            double shape = 0.62 * Math.Sin(phase) + 0.24 * Math.Sin(phase * 2.7 + 1.1);
            DemandMW = GridBaseMW + GridSwingMW * shape;
            if (DemandMW < 0) DemandMW = 0;

            // Spot price rises with demand as a fraction of the swing envelope, plus mild convex scarcity.
            double load = Math.Clamp((DemandMW - (GridBaseMW - GridSwingMW)) / (2 * GridSwingMW), 0, 1);
            PriceUsdPerMWh = 18.0 + 90.0 * load + 70.0 * load * load;

            if (double.IsNaN(availableMW) || double.IsInfinity(availableMW) || availableMW < 0) availableMW = 0;
            if (double.IsNaN(setpointMW) || double.IsInfinity(setpointMW) || setpointMW < 0) setpointMW = 0;

            double supply;
            if (_selling && generating && availableMW > 1.0)
            {
                DispatchedMW = Math.Min(setpointMW, availableMW);
                supply = DispatchedMW;

                double hours = dtSeconds / 3600.0;
                double energy = DispatchedMW * hours;
                TotalEnergyMWh += energy;
                TotalRevenueUsd += energy * PriceUsdPerMWh;
            }
            else
            {
                DispatchedMW = 0;
                supply = 0;
            }

            // Frequency: over-supply pushes above 60 Hz, under-supply below. Pulls back toward 60.
            double demandForBalance = Math.Max(DemandMW, 1.0);
            double imbalance = Math.Clamp((supply - DemandMW) / demandForBalance, -1.0, 1.0);
            double target = 60.0 + imbalance * 0.45;
            FrequencyHz += (target - FrequencyHz) * 0.25;
            FrequencyHz = Math.Clamp(FrequencyHz, 58.5, 61.5);
        }
        catch
        {
            // Never throw from the sim tick.
        }
    }
}
