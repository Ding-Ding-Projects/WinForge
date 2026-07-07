using System;

namespace WinForge.Services;

/// <summary>
/// AI 訓練叢集 · AI Training Cluster — a heavy, reactor-powered GPU load. The cluster only makes
/// progress while the nuclear reactor is actually feeding it big MW. The operator picks a target
/// power draw; throughput scales with the delivered power (≈0.05 PFLOP/s per MW) and accumulates
/// PFLOP-days toward a chosen model size. If the reactor cannot supply the demand — or is not
/// generating (cold MODE 5, scrammed, meltdown) — the run checkpoints and PAUSES, freezing progress.
///
/// Pure managed, deterministic (internal integer tick counter — never DateTime-based), never throws.
/// </summary>
public sealed class AiClusterService
{
    // ---- Tunables ------------------------------------------------------------
    public const double MaxDrawMW = 900.0;               // operator draw ceiling (clamped to available)
    private const double PflopsPerMW = 0.05;             // PFLOP/s delivered per MW actually drawn
    private const double SecondsPerTick = 0.5;           // timer cadence (informational; ramps use tick counter)

    // ---- Reactor economy (Watts ⚡) ------------------------------------------
    public const double WattsPerPflopDay = 12.0;         // ⚡ awarded per whole PFLOP-day of compute output
    public const double OverclockMultiplier = 1.30;      // +30% throughput when the perk is owned

    // ---- Model-size presets (target PFLOP-days for a full training run) -------
    public enum ModelSize { Small, Medium, Large, Frontier }

    public static double TargetPflopDays(ModelSize size) => size switch
    {
        ModelSize.Small => 8.0,
        ModelSize.Medium => 40.0,
        ModelSize.Large => 150.0,
        ModelSize.Frontier => 600.0,
        _ => 40.0,
    };

    // ---- Live state ----------------------------------------------------------
    public bool Running { get; private set; }            // operator armed the run
    public bool Stalled { get; private set; }            // paused because reactor can't feed it
    public ModelSize Size { get; private set; } = ModelSize.Medium;

    public double DrawnMW { get; private set; }          // power actually drawn this step
    public double PflopsNow { get; private set; }        // instantaneous throughput (PFLOP/s)
    public double PflopDaysDone { get; private set; }    // accumulated compute toward the run
    public double GpuUtilPct { get; private set; }       // 0..100
    public double RackTempC { get; private set; } = 24.0;// idle ambient
    public long Checkpoints { get; private set; }        // how many times we checkpointed on a stall

    /// <summary>由反應堆銀行擁有嘅永久 +30% 超頻 · Permanent +30% throughput perk (owned via the Reactor Bank).</summary>
    public bool OverclockActive { get; set; }

    /// <summary>已入賬俾經濟系統嘅 PFLOP-日 · PFLOP-days already awarded to the reactor economy.</summary>
    public double PflopDaysAwarded { get; private set; }

    private long _ticks;                                 // internal deterministic counter
    private double _rampUtil;                             // smoothed utilisation ramp 0..1

    public double TargetPflopDaysCurrent => TargetPflopDays(Size);

    public double ProgressPct
    {
        get
        {
            double target = TargetPflopDaysCurrent;
            if (target <= 0) return 0;
            double p = PflopDaysDone / target * 100.0;
            return p < 0 ? 0 : (p > 100 ? 100 : p);
        }
    }

    public bool Complete => PflopDaysDone >= TargetPflopDaysCurrent && TargetPflopDaysCurrent > 0;

    /// <summary>
    /// 攞出未入賬嘅整數 PFLOP-日 · Claim any WHOLE PFLOP-days of compute produced since the last claim,
    /// marking them as awarded. The UI multiplies the returned count by <see cref="WattsPerPflopDay"/> and
    /// calls <c>ReactorEconomyService.I.Earn</c>. Returns 0 when less than a whole PFLOP-day is pending, so
    /// Watts are minted in sensible increments (never every tick). Never throws.
    /// </summary>
    public int ClaimWholePflopDays()
    {
        try
        {
            double pending = PflopDaysDone - PflopDaysAwarded;
            if (double.IsNaN(pending) || pending < 1.0) return 0;
            int whole = (int)Math.Floor(pending);
            if (whole <= 0) return 0;
            PflopDaysAwarded += whole;
            return whole;
        }
        catch { return 0; }
    }

    // ---- Operator actions ----------------------------------------------------
    public void Start() { Running = true; }

    public void Pause()
    {
        if (Running) Checkpoints++;   // pausing writes a checkpoint
        Running = false;
        Stalled = false;
    }

    public void NewRun(ModelSize size)
    {
        Size = size;
        PflopDaysDone = 0;
        PflopDaysAwarded = 0;
        Running = false;
        Stalled = false;
        _ticks = 0;
        _rampUtil = 0;
        PflopsNow = 0;
        DrawnMW = 0;
        GpuUtilPct = 0;
        RackTempC = 24.0;
    }

    public void Reset()
    {
        Running = false;
        Stalled = false;
        Size = ModelSize.Medium;
        PflopDaysDone = 0;
        PflopDaysAwarded = 0;
        Checkpoints = 0;
        _ticks = 0;
        _rampUtil = 0;
        PflopsNow = 0;
        DrawnMW = 0;
        GpuUtilPct = 0;
        RackTempC = 24.0;
    }

    /// <summary>
    /// Advance one step. <paramref name="requestedMW"/> is the operator's target draw (already clamped
    /// to the slider); <paramref name="availableMW"/> is live reactor output; <paramref name="generating"/>
    /// is true only when the reactor is truly generating. Never throws.
    /// </summary>
    public void Tick(double requestedMW, double availableMW, bool generating)
    {
        try
        {
            _ticks++;

            double want = Sanitize(requestedMW);
            if (want < 0) want = 0;
            if (want > MaxDrawMW) want = MaxDrawMW;

            double avail = Sanitize(availableMW);
            if (avail < 0) avail = 0;

            // The cluster can only ever draw what the reactor delivers. If the reactor is not
            // generating, or cannot meet the demand, the run stalls & checkpoints.
            bool canFeed = generating && avail >= want && want > 0;

            if (Running && canFeed)
            {
                if (Stalled) { Stalled = false; }   // recovered

                DrawnMW = want;

                // Smooth utilisation ramp toward full using the tick counter (deterministic).
                _rampUtil += (1.0 - _rampUtil) * 0.08;
                if (_rampUtil > 1) _rampUtil = 1;

                GpuUtilPct = Math.Round(_rampUtil * 100.0, 1);
                PflopsNow = DrawnMW * PflopsPerMW * _rampUtil * (OverclockActive ? OverclockMultiplier : 1.0);

                // PFLOP-days accrual: PFLOP/s * elapsed seconds -> PFLOP-seconds, /86400 -> PFLOP-days.
                double pflopSeconds = PflopsNow * SecondsPerTick;
                PflopDaysDone += pflopSeconds / 86400.0;

                double target = TargetPflopDaysCurrent;
                if (PflopDaysDone >= target)
                {
                    PflopDaysDone = target;
                    Running = false;        // run complete
                    _rampUtil = 0;
                }

                // Rack temperature climbs with load, capped.
                double targetTemp = 24.0 + _rampUtil * 44.0;    // up to ~68C at full tilt
                RackTempC += (targetTemp - RackTempC) * 0.10;
            }
            else
            {
                // Stalled or idle: checkpoint once on the transition into a stall, spin GPUs down, cool off.
                if (Running && !canFeed && !Stalled)
                {
                    Stalled = true;
                    Checkpoints++;          // checkpoint-and-pause
                }
                if (!Running) Stalled = false;

                DrawnMW = 0;
                PflopsNow = 0;
                _rampUtil += (0.0 - _rampUtil) * 0.15;
                if (_rampUtil < 0.001) _rampUtil = 0;
                GpuUtilPct = Math.Round(_rampUtil * 100.0, 1);
                RackTempC += (24.0 - RackTempC) * 0.06;   // drift back to ambient
            }

            if (RackTempC < 20) RackTempC = 20;
            if (RackTempC > 95) RackTempC = 95;
        }
        catch
        {
            // Never throw out of the sim.
        }
    }

    private static double Sanitize(double v) => double.IsNaN(v) || double.IsInfinity(v) ? 0 : v;
}
