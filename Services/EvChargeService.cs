using System;
using System.Collections.Generic;

namespace WinForge.Services;

/// <summary>
/// 電動車充電站引擎 · EV Fast-Charge Depot engine — a pure-managed, never-throwing simulation of a
/// bank of DC fast-charge stalls fed by the flagship reactor. Each stall charges one vehicle (battery
/// SoC 0..100%) at up to <see cref="PerStallMaxKw"/>; total draw is capped to the reactor's available
/// MWe, throttling per-stall power when the reactor can't supply every open stall. Vehicles that reach
/// full leave and a queued one arrives. Energy delivered (kWh) is tracked so the UI can mint ⚡.
/// Deterministic (driven by an integer tick counter, not wall-clock). No I/O, no threads.
/// </summary>
public sealed class EvChargeService
{
    public const double PerStallMaxKw = 350.0;   // DC fast-charge ceiling per stall
    public const double BatteryKwh = 80.0;        // nominal usable pack size per vehicle
    public const int MaxStalls = 40;

    private readonly Random _rng = new(0xE7C4);
    private int _nextVehicleId = 1;

    /// <summary>單一充電位 · One stall's live state (also the ListView row model lives in the page).</summary>
    public sealed class Stall
    {
        public int Id;
        public int VehicleId;      // 0 = empty
        public double Soc;         // 0..100 %
        public double DeliveredKw; // instantaneous power this tick
        public double TargetSoc;   // where this vehicle unplugs
    }

    private readonly List<Stall> _stalls = new();

    public bool IsOpen { get; private set; }
    public int StallCount => _stalls.Count;
    public int ActiveStalls { get; private set; }
    public double TotalDrawMW { get; private set; }
    public double PerStallKw { get; private set; }
    public double FleetAvgSoc { get; private set; }
    public int Completed { get; private set; }
    public int QueueLength { get; private set; }
    public double UndeliveredKwh { get; private set; } // energy delivered but not yet minted to ⚡

    public IReadOnlyList<Stall> Stalls => _stalls;

    public EvChargeService()
    {
        SetStallCount(8);
    }

    public void Open() { IsOpen = true; }
    public void Close() { IsOpen = false; }

    public void SetStallCount(int count)
    {
        try
        {
            count = Math.Clamp(count, 0, MaxStalls);
            while (_stalls.Count < count) _stalls.Add(new Stall { Id = _stalls.Count + 1, VehicleId = 0, Soc = 0 });
            if (_stalls.Count > count) _stalls.RemoveRange(count, _stalls.Count - count);
        }
        catch { }
    }

    public void AddStalls(int n)
    {
        if (n <= 0) return;
        SetStallCount(_stalls.Count + n);
    }

    public void RemoveStalls(int n)
    {
        if (n <= 0) return;
        SetStallCount(_stalls.Count - n);
    }

    public void Reset()
    {
        try
        {
            IsOpen = false;
            Completed = 0;
            QueueLength = 0;
            UndeliveredKwh = 0;
            TotalDrawMW = 0;
            ActiveStalls = 0;
            PerStallKw = 0;
            FleetAvgSoc = 0;
            _nextVehicleId = 1;
            foreach (var s in _stalls) { s.VehicleId = 0; s.Soc = 0; s.DeliveredKw = 0; s.TargetSoc = 0; }
        }
        catch { }
    }

    private double NewTargetSoc() => 78 + _rng.NextDouble() * 22;  // vehicles leave at 78..100%
    private double NewArrivalSoc() => 8 + _rng.NextDouble() * 42;  // arrive nearly-empty to half

    /// <summary>
    /// 行一格模擬 · Advance one simulation step. <paramref name="dtSeconds"/> is elapsed sim seconds,
    /// <paramref name="availableMW"/> is the reactor's spare electrical output, <paramref name="generating"/>
    /// gates all charging. Returns the kWh delivered this step (also accumulated in <see cref="UndeliveredKwh"/>).
    /// </summary>
    public double Tick(double dtSeconds, double availableMW, bool generating)
    {
        try
        {
            if (double.IsNaN(dtSeconds) || dtSeconds <= 0) dtSeconds = 0;
            dtSeconds = Math.Clamp(dtSeconds, 0, 5);
            if (double.IsNaN(availableMW) || availableMW < 0) availableMW = 0;

            // Determine which stalls want power (open + occupied, or auto-fill open depot).
            if (IsOpen && generating)
            {
                // Arrivals: fill empty stalls from the (implied infinite) queue.
                foreach (var s in _stalls)
                {
                    if (s.VehicleId == 0)
                    {
                        s.VehicleId = _nextVehicleId++;
                        s.Soc = NewArrivalSoc();
                        s.TargetSoc = NewTargetSoc();
                        s.DeliveredKw = 0;
                    }
                }
            }

            int wanting = 0;
            foreach (var s in _stalls)
                if (s.VehicleId != 0 && s.Soc < s.TargetSoc) wanting++;

            bool active = IsOpen && generating && wanting > 0;

            // Power budget: available MWe -> kW, shared across wanting stalls, capped per stall.
            double budgetKw = availableMW * 1000.0;
            double perStall;
            if (!active) perStall = 0;
            else
            {
                perStall = budgetKw / wanting;
                if (perStall > PerStallMaxKw) perStall = PerStallMaxKw;
                if (perStall < 0) perStall = 0;
            }

            double deliveredKwh = 0;
            double totalKw = 0;
            int completedThisTick = 0;

            foreach (var s in _stalls)
            {
                if (!active || s.VehicleId == 0 || s.Soc >= s.TargetSoc)
                {
                    s.DeliveredKw = 0;
                    continue;
                }

                double kw = perStall;
                double kwh = kw * (dtSeconds / 3600.0);
                double socGain = (kwh / BatteryKwh) * 100.0;

                // Don't overshoot the target SoC.
                double room = s.TargetSoc - s.Soc;
                if (socGain > room)
                {
                    socGain = room;
                    kwh = (socGain / 100.0) * BatteryKwh;
                    kw = dtSeconds > 0 ? kwh / (dtSeconds / 3600.0) : 0;
                }

                s.Soc += socGain;
                s.DeliveredKw = kw;
                totalKw += kw;
                deliveredKwh += kwh;

                if (s.Soc >= s.TargetSoc - 0.01)
                {
                    // Vehicle full -> leaves. Depot open+generating repopulates next tick.
                    completedThisTick++;
                    s.VehicleId = 0;
                    s.Soc = 0;
                    s.DeliveredKw = 0;
                    s.TargetSoc = 0;
                }
            }

            Completed += completedThisTick;
            UndeliveredKwh += deliveredKwh;

            // Live aggregates.
            int occupied = 0; double socSum = 0; int activeNow = 0;
            foreach (var s in _stalls)
            {
                if (s.VehicleId != 0) { occupied++; socSum += s.Soc; }
                if (s.DeliveredKw > 0.01) activeNow++;
            }
            ActiveStalls = activeNow;
            TotalDrawMW = totalKw / 1000.0;
            PerStallKw = active ? perStall : 0;
            FleetAvgSoc = occupied > 0 ? socSum / occupied : 0;

            // "Queue" = stalls that want charge but are power-starved this tick (throttled to zero),
            // i.e. vehicles waiting on capacity. When idle/closed, no queue.
            QueueLength = active ? Math.Max(0, wanting - activeNow) : 0;

            return deliveredKwh;
        }
        catch { return 0; }
    }

    /// <summary>抽起已入賬嘅 kWh · Drain whole kWh that have been minted, keeping the fractional remainder.</summary>
    public double DrainDeliveredKwh(double amount)
    {
        try
        {
            if (amount <= 0) return 0;
            double taken = Math.Min(amount, UndeliveredKwh);
            UndeliveredKwh -= taken;
            if (UndeliveredKwh < 0) UndeliveredKwh = 0;
            return taken;
        }
        catch { return 0; }
    }
}
