using System;
using System.Collections.Generic;

namespace WinForge.Services;

/// <summary>
/// 電網卸載調度模型 · Grid MW-budget / load-shed dispatcher model — the ⚛️ epic's "live MW-budget dashboard"
/// item: a city grid fed by the flagship reactor. A fixed catalog of feeders (hospital, water works, rail,
/// heating, homes, commerce, EV depots, industry) each declares a demand and a priority class. Every tick the
/// dispatcher takes the reactor's available MW, holds back an operator-set spinning reserve, then serves feeders
/// in strict priority order — the first feeder that no longer fits trips the cutoff and it plus every
/// lower-priority feeder is shed (breakers open instantly). A shed feeder only recloses after the budget would
/// have fit it for a consecutive stability window (anti-flap hysteresis). Unserved energy integrates while shed.
/// Pure managed C#; deterministic, driven by an integer tick counter (500 ms/tick); never throws.
/// </summary>
public sealed class GridLoadShedService
{
    /// <summary>One city feeder on the dispatch ladder.</summary>
    public sealed class Feeder
    {
        public string Id { get; init; } = "";
        public string En { get; init; } = "";
        public string Zh { get; init; } = "";
        /// <summary>Priority class, 1 = most critical (served first, shed last).</summary>
        public int Priority { get; init; }
        /// <summary>Feeder demand. (MW)</summary>
        public double DemandMW { get; set; }
        /// <summary>Operator breaker: false = intentionally offline (not counted as unserved).</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>True when the dispatcher has shed this feeder (wanted power but none fits).</summary>
        public bool IsShed { get; internal set; }
        /// <summary>MW actually delivered this tick (DemandMW when served, else 0).</summary>
        public double ServedMW { get; internal set; }
        /// <summary>Consecutive ticks the ideal dispatch would have served a currently-shed feeder.</summary>
        internal int RecloseStreak;
    }

    // --- tuning --------------------------------------------------------------
    /// <summary>Ticks the budget must keep fitting a shed feeder before its breaker recloses (~5 s).</summary>
    public const int RecloseDelayTicks = 10;
    /// <summary>Default spinning-reserve hold-back. (%)</summary>
    public const double DefaultReservePct = 10.0;

    private const double TickSeconds = 0.5; // 500 ms per tick

    // --- state ---------------------------------------------------------------
    /// <summary>The fixed feeder catalog, highest priority first. Demands sum to 990 MW.</summary>
    public IReadOnlyList<Feeder> Feeders => _feeders;
    private readonly List<Feeder> _feeders = new()
    {
        new Feeder { Id = "hospital",  En = "City hospitals",          Zh = "城市醫院電網",   Priority = 1, DemandMW = 60 },
        new Feeder { Id = "water",     En = "Water & sewage works",    Zh = "供水及污水處理", Priority = 1, DemandMW = 80 },
        new Feeder { Id = "rail",      En = "Rail & metro traction",   Zh = "鐵路及地鐵",     Priority = 2, DemandMW = 120 },
        new Feeder { Id = "heating",   En = "District-heating pumps",  Zh = "區域供熱泵",     Priority = 2, DemandMW = 90 },
        new Feeder { Id = "homes",     En = "Residential districts",   Zh = "住宅區",         Priority = 3, DemandMW = 250 },
        new Feeder { Id = "commerce",  En = "Commercial towers",       Zh = "商業大廈",       Priority = 3, DemandMW = 180 },
        new Feeder { Id = "ev",        En = "EV charging depots",      Zh = "電動車充電站",   Priority = 4, DemandMW = 110 },
        new Feeder { Id = "industry",  En = "Industrial park",         Zh = "工業園",         Priority = 5, DemandMW = 100 },
    };

    /// <summary>Operator spinning-reserve hold-back. (0..30 %)</summary>
    public double ReservePct { get; private set; } = DefaultReservePct;

    /// <summary>Reactor MW available to the grid this tick (input echo). (MW)</summary>
    public double AvailableMW { get; private set; }
    /// <summary>Dispatchable budget after the reserve hold-back. (MW)</summary>
    public double UsableMW { get; private set; }
    /// <summary>Total MW delivered to served feeders. (MW)</summary>
    public double ServedMW { get; private set; }
    /// <summary>Total demand of feeders shed by the dispatcher (excludes operator-disabled). (MW)</summary>
    public double ShedMW { get; private set; }
    /// <summary>Lifetime energy the shed feeders wanted but never received. (MWh)</summary>
    public double UnservedMWh { get; private set; }
    /// <summary>Lifetime count of served→shed breaker trips.</summary>
    public int ShedEvents { get; private set; }
    /// <summary>True while the reactor bus is energised (last Step saw generating power).</summary>
    public bool BusEnergised { get; private set; }

    private int _lastTick = int.MinValue;

    /// <summary>Set the spinning-reserve hold-back percentage (clamped 0..30).</summary>
    public void SetReservePct(double pct)
    {
        try
        {
            if (!double.IsFinite(pct)) pct = DefaultReservePct;
            ReservePct = Math.Clamp(pct, 0, 30);
        }
        catch { }
    }

    /// <summary>Operator breaker toggle for one feeder (intentional, never counts as unserved).</summary>
    public void SetFeederEnabled(string id, bool enabled)
    {
        try
        {
            foreach (var f in _feeders)
                if (string.Equals(f.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    f.Enabled = enabled;
                    if (!enabled) { f.IsShed = false; f.ServedMW = 0; f.RecloseStreak = 0; }
                }
        }
        catch { }
    }

    /// <summary>Reset counters and reclose every operator breaker.</summary>
    public void Reset()
    {
        try
        {
            foreach (var f in _feeders)
            {
                f.Enabled = true;
                f.IsShed = false;
                f.ServedMW = 0;
                f.RecloseStreak = 0;
            }
            ReservePct = DefaultReservePct;
            AvailableMW = 0;
            UsableMW = 0;
            ServedMW = 0;
            ShedMW = 0;
            UnservedMWh = 0;
            ShedEvents = 0;
            BusEnergised = false;
            _lastTick = int.MinValue;
        }
        catch { }
    }

    /// <summary>
    /// Advance the dispatcher one UI tick. <paramref name="availableMW"/> is the reactor's live electrical
    /// output; <paramref name="generating"/> gates whether the bus is energised at all. Uses the integer tick to
    /// derive elapsed time so timing never depends on DateTime.Now. Never throws.
    /// </summary>
    public void Step(int tick, double availableMW, bool generating)
    {
        try
        {
            double dt = 0;
            bool tickAdvanced = _lastTick == int.MinValue;
            if (tickAdvanced)
            {
                dt = TickSeconds;
            }
            else
            {
                int delta = tick - _lastTick;
                if (delta > 0)
                {
                    tickAdvanced = true;
                    dt = Math.Clamp(delta * TickSeconds, TickSeconds, 5.0);
                }
            }
            _lastTick = tick;

            if (!double.IsFinite(availableMW) || availableMW < 0) availableMW = 0;
            BusEnergised = generating && availableMW > 1.0;
            AvailableMW = BusEnergised ? availableMW : 0;
            UsableMW = AvailableMW * (1.0 - ReservePct / 100.0);

            // --- ideal dispatch: strict priority cutoff -------------------------------
            // Serve in priority order; the first enabled feeder that does not fit trips the
            // cutoff — it and every feeder after it is dropped (no cherry-picking a smaller,
            // lower-priority feeder past a bigger critical one that just went dark).
            double remaining = UsableMW;
            bool cutoff = !BusEnergised;
            var wantServe = new bool[_feeders.Count];
            for (int i = 0; i < _feeders.Count; i++)
            {
                var f = _feeders[i];
                if (!f.Enabled) continue;
                double demand = NormalizedDemand(f);
                if (!cutoff && demand <= remaining)
                {
                    wantServe[i] = true;
                    remaining -= demand;
                }
                else
                {
                    cutoff = true;
                }
            }

            // --- apply with anti-flap hysteresis --------------------------------------
            // Shedding is instant; reclosing needs RecloseDelayTicks consecutive fitting ticks.
            double served = 0, shed = 0;
            foreach (var (f, want) in Zip(wantServe))
            {
                if (!f.Enabled) { f.ServedMW = 0; continue; }

                if (f.IsShed || f.ServedMW <= 0)
                {
                    // currently dark (shed or never served)
                    if (want)
                    {
                        bool firstEnergisation = !f.IsShed; // initial pickup needs no delay
                        if (tickAdvanced) f.RecloseStreak++;
                        if (firstEnergisation || (tickAdvanced && f.RecloseStreak >= RecloseDelayTicks))
                        {
                            f.IsShed = false;
                            f.ServedMW = NormalizedDemand(f);
                            f.RecloseStreak = 0;
                        }
                    }
                    else
                    {
                        // A cold/de-energised bus still represents enabled demand that is shed.
                        // It is not a served→shed transition, so it does not increment ShedEvents.
                        f.IsShed = true;
                        f.ServedMW = 0;
                        f.RecloseStreak = 0;
                    }
                }
                else
                {
                    // currently served
                    if (!want)
                    {
                        f.IsShed = true;      // breaker trips instantly
                        f.ServedMW = 0;
                        f.RecloseStreak = 0;
                        ShedEvents++;
                    }
                    else
                    {
                        f.ServedMW = NormalizedDemand(f); // track any demand edits while served
                    }
                }

                if (f.IsShed) shed += NormalizedDemand(f);
                served += f.ServedMW;
            }

            ServedMW = served;
            ShedMW = shed;

            // unserved energy integrates while feeders sit shed
            double unservedNow = shed * (dt / 3600.0);
            if (unservedNow > 0 && !double.IsNaN(unservedNow)) UnservedMWh += unservedNow;
        }
        catch { }
    }

    private IEnumerable<(Feeder f, bool want)> Zip(bool[] want)
    {
        for (int i = 0; i < _feeders.Count; i++) yield return (_feeders[i], want[i]);
    }

    private static double NormalizedDemand(Feeder feeder) =>
        double.IsFinite(feeder.DemandMW) && feeder.DemandMW > 0 ? feeder.DemandMW : 0;
}
