using System;

namespace WinForge.Services;

/// <summary>母線狀態 · State of a 4.16 kV Class 1E safety bus.</summary>
public enum BusState
{
    EnergizedOffsite, // 由廠外電源供電 · fed from the offsite/preferred grid via the station service transformer
    EnergizedEdg,     // 由應急柴油發電機供電 · fed from this train's emergency diesel generator
    Dead,             // 失電（已甩負載）· stripped / de-energized
}

/// <summary>應急柴油發電機狀態 · Emergency Diesel Generator state machine.</summary>
public enum EdgState
{
    Standby,   // 備用 · idle, ready to auto-start
    Starting,  // 起動中 · cranking/accelerating toward rated V/f (the "10-second diesel")
    Loaded,    // 帶載 · at rated voltage/frequency, breaker closed onto its bus, carrying safeguards loads
    Failed,    // 故障 · will not reach rated conditions this event (SBO / injected fault)
}

/// <summary>
/// 1E 級廠用電配電（交流／直流）· Class 1E plant electrical distribution: offsite (preferred) power, two
/// 4.16 kV safety trains, two emergency diesel generators with the design-basis 10-second start and a
/// stepped load sequencer, a 125 VDC station battery with a 10 CFR 50.63 SBO coping time, and the
/// equipment-availability gates those buses drive (RCPs, motor-driven ECCS/SI, motor- vs turbine-driven AFW).
///
/// Pure managed C#: no threads, no timers, no RNG. All timing is accumulated sim-seconds compared against
/// fixed setpoints, so the next state is a pure function of (previous state, injected inputs, dt) — fully
/// reproducible. SBO = LOOP (offsite lost) coincident with loss of all onsite emergency AC (both EDGs).
/// </summary>
public sealed class ReactorElectrical
{
    // ----------------------------------------------------------------- constants ----
    public const double BusNominalKv          = 4.16;   // kV — 4160 V three-phase Class 1E safety bus
    public const double EdgStartTimeSec       = 10.0;   // s — design-basis "10-second diesel": start signal → rated V/f
    public const double EdgRatedFreqHz        = 60.0;   // Hz — ready-to-load frequency
    public const double SeqBlockIntervalSec   = 5.0;    // s — load-block spacing so each inrush decays before the next
    public const int    SeqBlockCount         = 5;      // HHSI · RHR/LPSI · CCW · SW · CtmtFanCooler/Spray

    public const double BatteryNominalV        = 125.0; // VDC — 125 V vital bus (60-cell lead-acid string)
    public const double BatteryFloatV          = 132.0; // VDC — ~2.20 V/cell float at full charge (SOC = 1)
    public const double BatteryEndOfDischargeV = 105.0; // VDC — 1.75 V/cell end-of-discharge floor (IEEE 485 basis)
    public const double CopingTimeHoursDefault = 4.0;   // h — typical Westinghouse PWR SBO coping category (10 CFR 50.63)
    public const double LoadShedFactor         = 0.6;   // shedding non-vital DC loads stretches coping ~1.67×
    public const double RechargeRatePerHour    = 0.5;   // SOC/h — charger recovers the battery once any AC returns
    public const double TdafwMinSteamPsig      = 100.0; // psig — turbine-driven AFW loses motive steam below the usable band (design 80–150)

    // ----------------------------------------------------------------- bus / offsite ----
    public bool     OffsitePowerAvailable { get; private set; } = true;  // preferred/grid power present (false ⇒ LOOP)
    public BusState BusA => _trainA.Bus;
    public BusState BusB => _trainB.Bus;
    public bool BusAEnergized => BusA != BusState.Dead;
    public bool BusBEnergized => BusB != BusState.Dead;
    public bool AnyAcBusEnergized => BusAEnergized || BusBEnergized;

    // ----------------------------------------------------------------- EDGs ----
    public EdgState EdgA => _trainA.Edg;
    public EdgState EdgB => _trainB.Edg;
    public double EdgAStartProgressSec => _trainA.StartProg;  // 0..EdgStartTimeSec (UI progress)
    public double EdgBStartProgressSec => _trainB.StartProg;
    public int    SequencerBlocksApplied { get; private set; } // 0..SeqBlockCount, max across loaded trains (UI)

    // Each safety train is an identical little state machine (EDG + its 4.16 kV bus + timers).
    private struct Train { public EdgState Edg; public BusState Bus; public double StartProg; public double SeqTimer; }
    private Train _trainA = new() { Edg = EdgState.Standby, Bus = BusState.EnergizedOffsite };
    private Train _trainB = new() { Edg = EdgState.Standby, Bus = BusState.EnergizedOffsite };

    // ----------------------------------------------------------------- 125 VDC battery ----
    public double BatterySoc { get; private set; } = 1.0;     // state of charge, 1.0 = full
    public bool   LoadShed { get; set; }                      // operator action: strip non-vital DC loads
    public double CopingTimeHours { get; set; } = CopingTimeHoursDefault;
    public bool   DcAvailable => BatterySoc > 0.0;            // vital DC bus alive (instruments, RPS, TDAFW control)
    public double BatteryVoltage => DcAvailable
        ? BatteryEndOfDischargeV + BatterySoc * (BatteryFloatV - BatteryEndOfDischargeV)
        : 0.0;

    // ----------------------------------------------------------------- derived plant-wide ----
    /// <summary>全廠斷電 · SBO = loss of offsite power AND no EDG carrying a bus.</summary>
    public bool InSbo => !OffsitePowerAvailable && EdgA != EdgState.Loaded && EdgB != EdgState.Loaded;
    public bool OnEdgPower => EdgA == EdgState.Loaded || EdgB == EdgState.Loaded;

    // ----------------------------------------------------------------- availability outputs ----
    public bool RcpAvailable        { get; private set; } = true;  // non-1E reactor coolant pumps — lost on LOOP
    public bool MotorEccsSiAvailable{ get; private set; } = true;  // motor-driven ECCS/SI/charging — needs a live AC bus
    public bool MdafwAvailable      { get; private set; } = true;  // motor-driven AFW — needs AC bus + DC control
    public bool TdafwAvailable      { get; private set; }          // turbine-driven AFW — needs DC + steam (the SBO survivor)
    public bool TdafwRunning        { get; private set; }          // TDAFW actually feeding (available AND demanded)

    /// <summary>每 tick 的電氣輸入 · Per-tick inputs from the plant model (by-ref, no allocation).</summary>
    public readonly record struct Inputs(
        bool OffsiteAvailable,     // grid/preferred power present (false ⇒ LOOP)
        bool SiSignal,             // Safety Injection actuation — also auto-starts the EDGs
        bool EdgAFault,            // injected: EDG-A will not reach rated conditions (SBO ⇒ true)
        bool EdgBFault,            // injected: EDG-B will not reach rated conditions (SBO ⇒ true)
        double SgSteamPressurePsig,// governing SG pressure for TDAFW motive steam
        bool AfwDemand);           // a heat sink is needed (loss-of-feedwater / decay-heat removal)

    public void Reset()
    {
        OffsitePowerAvailable = true;
        _trainA = new Train { Edg = EdgState.Standby, Bus = BusState.EnergizedOffsite };
        _trainB = new Train { Edg = EdgState.Standby, Bus = BusState.EnergizedOffsite };
        SequencerBlocksApplied = 0;
        BatterySoc = 1.0; LoadShed = false; CopingTimeHours = CopingTimeHoursDefault;
        RcpAvailable = MotorEccsSiAvailable = MdafwAvailable = true;
        TdafwAvailable = TdafwRunning = false;
    }

    /// <summary>推進電氣模型一個 tick · Advance the electrical model by <paramref name="dt"/> seconds.</summary>
    public void Step(double dt, in Inputs inp)
    {
        if (dt <= 0) return;
        OffsitePowerAvailable = inp.OffsiteAvailable;

        // --- per-train EDG state machine + bus energization -------------------------------------------
        StepTrain(dt, ref _trainA, inp, inp.EdgAFault);
        StepTrain(dt, ref _trainB, inp, inp.EdgBFault);
        SequencerBlocksApplied = Math.Max(BlocksFor(_trainA), BlocksFor(_trainB));

        // --- 125 VDC station battery: depletes ONLY while no AC source is available -------------------
        // The charger carries the DC load and recharges the moment offsite or an EDG returns, so SOC
        // bleeds strictly across the AC-unavailable (SBO) interval. Coping time is the contract: at
        // exactly CopingTimeHours of continuous no-AC, SOC reaches 0 (battery-death endgame).
        bool acAvailable = OffsitePowerAvailable || OnEdgPower;
        double dtHours = dt / 3600.0;
        if (!acAvailable)
            BatterySoc -= (1.0 / CopingTimeHours) * (LoadShed ? LoadShedFactor : 1.0) * dtHours;
        else
            BatterySoc += RechargeRatePerHour * dtHours;
        BatterySoc = Math.Clamp(BatterySoc, 0.0, 1.0);

        // --- equipment availability gates -------------------------------------------------------------
        RcpAvailable         = OffsitePowerAvailable;                 // big non-1E pump motors die on LOOP regardless of EDGs
        MotorEccsSiAvailable = AnyAcBusEnergized;                     // SI/HHSI/RHR/charging pumps need a live 4.16 kV bus
        MdafwAvailable       = AnyAcBusEnergized && DcAvailable;      // motor-driven AFW: AC bus + DC control
        TdafwAvailable       = DcAvailable && inp.SgSteamPressurePsig > TdafwMinSteamPsig; // steam-driven; DC only for the governor
        TdafwRunning         = TdafwAvailable && !MdafwAvailable && inp.AfwDemand;          // the SBO decay-heat path
    }

    private void StepTrain(double dt, ref Train t, in Inputs inp, bool fault)
    {
        // Auto-start signal: LOOP (loss of voltage) OR Safety Injection — SI starts the diesel to ready
        // even with a healthy grid, so it is already up if the grid then fails.
        bool startSignal = !OffsitePowerAvailable || inp.SiSignal;

        if (OffsitePowerAvailable)
        {
            // Grid healthy: bus rides on offsite power; a previously-loaded EDG unloads back to standby.
            t.Bus = BusState.EnergizedOffsite;
            if (t.Edg == EdgState.Loaded) { t.Edg = EdgState.Standby; t.StartProg = 0; t.SeqTimer = 0; }
            else if (!startSignal && t.Edg != EdgState.Failed) t.Edg = EdgState.Standby;
        }
        else
        {
            // LOOP: strip the bus dead, then start and load the diesel onto it.
            if (t.Edg != EdgState.Loaded) t.Bus = BusState.Dead;
            if (startSignal)
            {
                if (fault) { t.Edg = EdgState.Failed; t.StartProg = 0; }
                else if (t.Edg == EdgState.Standby) { t.Edg = EdgState.Starting; t.StartProg = 0; }
            }
        }

        if (t.Edg == EdgState.Starting)
        {
            t.StartProg += dt;
            if (t.StartProg >= EdgStartTimeSec && !OffsitePowerAvailable)
            {
                t.Edg = EdgState.Loaded;          // breaker closes onto the dead bus at rated V/f
                t.Bus = BusState.EnergizedEdg;
                t.SeqTimer = 0;                   // load sequencer reference t = 0 is breaker close
            }
        }

        if (t.Edg == EdgState.Loaded)
        {
            t.Bus = OffsitePowerAvailable ? BusState.EnergizedOffsite : BusState.EnergizedEdg;
            t.SeqTimer += dt;
        }
        else t.SeqTimer = 0;
    }

    private static int BlocksFor(in Train t)
        => t.Edg == EdgState.Loaded
            ? Math.Min(SeqBlockCount, 1 + (int)(t.SeqTimer / SeqBlockIntervalSec))
            : 0;
}
