using System;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 反應堆運行模式 · Reactor operating mode.
/// </summary>
public enum ReactorMode
{
    Shutdown,  // 停機 · cold shutdown
    Startup,   // 啟動 · approach to criticality
    Run,       // 運轉 · at-power operation
    Tripped,   // 已跳機（SCRAM）· tripped / scrammed
    Meltdown,  // 熔毀 · core damage / meltdown
}

/// <summary>
/// 一個警報旗標 · A single annunciator alarm flag.
/// </summary>
public enum ReactorAlarm
{
    HighPower,
    HighFuelTemp,
    HighCoolantTemp,
    HighPressure,
    LowPressure,
    LowFlow,
    LowPzrLevel,
    HighPzrLevel,
    ShortPeriod,
    Scram,
    HighNeutronFlux,
    SteamPressureHigh,
    CoreDamage,
    EccsActive,
    TurbineTrip,
    LowSubcooling,
    DecayHeatHigh,
    AtwsActive,
    AccumulatorInject,
    AuxFeedwater,
    NaturalCirc,
}

/// <summary>
/// 全模擬壓水式核反應堆引擎（純 C#）· Fully-simulated Pressurized Water Reactor engine (pure managed C#).
///
/// Implements a believable real-time PWR model:
///  - Point reactor kinetics with 6 delayed-neutron groups (precursor concentrations, prompt
///    neutron lifetime, beta-effective).
///  - Reactivity from: control rod banks, soluble boron (ppm), Doppler fuel-temperature feedback,
///    moderator-temperature feedback, and Xe-135 poisoning (iodine/xenon dynamics).
///  - Thermal-hydraulics: fuel temperature, coolant inlet/outlet (Tcold/Thot), primary pressure
///    (pressurizer heaters/spray), RCP flow, steam-generator heat transfer, secondary steam
///    pressure, turbine electrical output vs load demand, condenser.
///  - Safety systems: SCRAM, high/low trips, ECCS injection, pressurizer relief valves.
///  - Failure: sustained over-temperature / over-pressure -> core damage -> MELTDOWN.
///
/// The model is integrated with a small fixed timestep (sub-stepped from the UI tick) for the
/// stiff kinetics equations. All quantities are toy/engineering approximations for an educational
/// simulation — they are tuned for plausibility and playability, not licensing accuracy.
/// </summary>
public sealed class ReactorSimService
{
    // ----------------------------------------------------------------- constants ----
    // Six delayed-neutron groups (typical U-235 thermal values).
    private static readonly double[] Beta = { 0.000215, 0.001424, 0.001274, 0.002568, 0.000748, 0.000273 };
    private static readonly double[] Lambda = { 0.0124, 0.0305, 0.111, 0.301, 1.14, 3.01 }; // 1/s decay
    private static readonly double BetaTotal; // total delayed fraction
    private const double PromptLifetime = 2.0e-5; // s, prompt neutron generation time (Lambda*)

    // Reactivity coefficients (delta-k/k per unit) — tuned, plausible PWR magnitudes.
    private const double DopplerCoeff = -2.8e-5;   // per °C of fuel temp (Doppler, negative)
    private const double ModTempCoeff = -2.0e-4;   // per °C of moderator (coolant) temp (negative, MTC)
    private const double BoronWorth = -9.5e-6;     // per ppm boron (negative)
    private const double XenonWorthFull = -0.028;  // dk/k at equilibrium full-power xenon (~ -2800 pcm)

    // Rod worth: total worth of all banks fully inserted (dk/k). Banks share this.
    private const double TotalRodWorth = 0.080; // 8000 pcm fully inserted

    // Reference / nominal operating points.
    public const double RatedThermalMW = 3411.0;  // MWth (typical 4-loop PWR core)
    public const double RatedElectricMW = 1150.0; // MWe gross
    private const double RefFuelTemp = 600.0;      // °C reference fuel temp for Doppler datum
    private const double RefModTemp = 305.0;       // °C reference Tavg datum
    private const double ColdTemp = 35.0;          // °C cold shutdown coolant temp
    private const double NominalTavg = 305.0;      // °C
    private const double NominalPressure = 15.5;   // MPa primary pressure
    private const double NominalPzrLevel = 55.0;   // % pressurizer level
    private const double NominalSteamPressure = 6.9; // MPa secondary
    private const double NominalBoron = 1200.0;    // ppm at BOL hot-zero-power-ish

    // Limits / trip setpoints.
    public const double FuelMeltTemp = 2800.0;     // °C — UO2 melting point ~2865 °C
    public const double FuelDamageTemp = 1200.0;   // °C — clad/fuel damage onset (sustained)
    public const double VesselPressureLimit = 17.2;// MPa — design pressure trip
    public const double VesselBurstPressure = 19.0;// MPa — catastrophic
    public const double HighPowerTrip = 1.18;      // fraction (118 %)
    public const double ShortPeriodTrip = 10.0;    // s — trip on period shorter than this
    public const double LowFlowTrip = 0.85;        // fraction of nominal flow
    public const double HighThotTrip = 345.0;      // °C
    public const double MeltdownDamageThreshold = 100.0; // accumulated "damage" units -> meltdown

    static ReactorSimService()
    {
        double s = 0;
        foreach (var b in Beta) s += b;
        BetaTotal = s;
    }

    // ----------------------------------------------------------------- state ----
    // Kinetics
    private double _power = 1e-6;          // neutron power, fraction of rated (start ~ source level)
    private readonly double[] _precursor = new double[6];
    public double NeutronPowerFraction => _power;
    public double ReactorPeriodSeconds { get; private set; } = 1e9;
    public double ReactivityPcm { get; private set; }       // total reactivity in pcm
    public double SourceLevel { get; set; } = 1e-8;         // neutron source (keeps subcritical core alive)

    // Reactivity component breakdown (pcm) for display.
    public double RodReactivityPcm { get; private set; }
    public double BoronReactivityPcm { get; private set; }
    public double DopplerReactivityPcm { get; private set; }
    public double ModeratorReactivityPcm { get; private set; }
    public double XenonReactivityPcm { get; private set; }

    // Thermal-hydraulics
    public double FuelTemp { get; private set; } = ColdTemp;     // °C
    public double Tcold { get; private set; } = ColdTemp;        // °C coolant inlet
    public double Thot { get; private set; } = ColdTemp;         // °C coolant outlet
    public double Tavg => (Tcold + Thot) / 2.0;
    public double PrimaryPressure { get; private set; } = 2.5;   // MPa (cold)
    public double PressurizerLevel { get; private set; } = NominalPzrLevel; // %
    public double SteamPressure { get; private set; } = 0.5;     // MPa secondary
    public double SteamGenLevel { get; private set; } = 60.0;    // %
    public double ThermalPowerMW => _power * RatedThermalMW;
    public double ElectricPowerMW { get; private set; }          // MWe actual
    public double TurbineRPM { get; private set; }               // rpm

    // Poisons
    public double Iodine { get; private set; }   // I-135 concentration (normalized)
    public double Xenon { get; private set; }    // Xe-135 concentration (normalized)

    // Controls
    public double[] RodBankInsertion { get; } = { 100.0, 100.0, 100.0, 100.0 }; // % inserted per bank (A,B,C,D)
    public double BoronPpm { get; private set; } = NominalBoron;
    public double TargetBoronPpm { get; set; } = NominalBoron;
    public bool PressurizerHeater { get; set; }
    public bool PressurizerSpray { get; set; }
    public double RcpFlowDemand { get; set; } = 0.0;     // 0..1 commanded pump flow
    public bool[] RcpRunning { get; } = { false, false, false, false };
    public double FeedwaterFlow { get; set; } = 0.0;     // 0..1
    public double TurbineLoadSetpoint { get; set; } = 0.0; // 0..1 (fraction of rated MWe)
    public bool GeneratorBreakerClosed { get; set; }
    public bool ReliefValveOpen { get; set; }
    public bool EccsArmed { get; set; }
    public bool EccsInjecting { get; private set; }
    public ReactorMode Mode { get; private set; } = ReactorMode.Shutdown;

    // Auto-control
    public bool AutoRodControl { get; set; }
    public double AutoPowerSetpoint { get; set; } = 1.0;  // fraction of rated thermal power

    // Derived flow
    public double CoolantFlowFraction { get; private set; } // 0..1 actual primary flow

    // Failure accumulation
    public double DamageAccumulation { get; private set; }  // 0..100+ ; >=100 => meltdown
    public bool IsScrammed { get; private set; }
    public bool MeltdownTriggered { get; private set; }
    public event Action? MeltdownOccurred;

    // ----------------------------------------------------------------- realism additions ----
    // Decay-heat: ALWAYS-present residual heat source from fission-product decay. Modelled as three
    // exponential groups that charge while at power and decay after a trip. Fraction of rated power.
    private static readonly double[] DecayFrac = { 0.038, 0.020, 0.011 };
    private static readonly double[] DecayTau = { 1.0, 50.0, 5000.0 }; // s
    private readonly double[] _decayGroup = new double[3];
    public double DecayHeatFraction { get; private set; }

    // Sub-cooling margin (°C): SatTemp(P) - Thot. < 0 means saturation / void onset.
    public double SubcoolingMarginC { get; private set; }

    // Nuclear instrumentation (NIS): source-range count rate (cps) + 1/M for approach to criticality.
    public double SourceRangeCps { get; private set; }
    public double OneOverM { get; private set; } = 1.0;
    public double StartupRateDpm { get; private set; } // decades per minute
    private const double SourceBaselineCps = 100.0;

    // Turbine reference speed: 4-pole 60 Hz nuclear set runs at 1800 rpm.
    public const double SyncRpm = 1800.0;
    public double TurbineRatedRpm => SyncRpm;
    private const double OverspeedTripRpm = 1980.0;

    // Burnup drift (cosmetic realism flourish).
    public double BurnupMwdPerTonne { get; private set; }
    private const double CoreTonnesU = 100.0; // ~100 tonnes U in a large PWR core

    // Accident scenario state.
    public ReactorScenario ActiveScenario { get; private set; } = ReactorScenario.Normal;
    private double _breakArea;        // LOCA break "area" (0..1)
    private bool _rodsFailToInsert;   // ATWS
    public bool AccumulatorInjecting { get; private set; }
    public bool AuxFeedwaterRunning { get; private set; }
    private double _lofwTimer;        // counts up after feedwater lost (aux start at 60 s)

    // Alarms
    private readonly bool[] _alarms = new bool[Enum.GetValues(typeof(ReactorAlarm)).Length];
    public bool Alarm(ReactorAlarm a) => _alarms[(int)a];

    public string StatusEn { get; private set; } = "Cold shutdown";
    public string StatusZh { get; private set; } = "冷停機";

    // Reactor Protection System — 4-channel 2-of-4 coincidence logic with Westinghouse trip
    // setpoints and P-6/P-7/P-8/P-9/P-10 permissive interlocks. Replaces the old single-channel
    // boolean trip OR. Built once and re-evaluated every protection tick.
    private readonly ReactorRps _rps = new();
    public ReactorRps Rps => _rps;
    public string LastTripFunctionEn { get; private set; } = "";
    public string LastTripFunctionZh { get; private set; } = "";

    // Power rate-of-change (per second), for the power-range positive/negative rate trip.
    private double _powerRate;
    public double PowerRatePerSec => _powerRate;

    // Reference full-power primary coolant ΔT (Thot−Tcold) in °F — datum for OTΔT/OPΔT.
    private const double FullPowerDeltaTF = 60.0;

    public ReactorSimService()
    {
        // Initialize precursors consistent with the (very low) initial power.
        for (int i = 0; i < 6; i++)
            _precursor[i] = Beta[i] / (PromptLifetime * Lambda[i]) * _power;
        Xenon = 0;
        Iodine = 0;
        BuildRps();
    }

    /// <summary>
    /// 建立反應堆保護系統的保護功能 · Build the RPS protection functions with Westinghouse nominal trip
    /// setpoints (expressed in the sim's native units) and their permissive gating. Pressure
    /// conversions: psia = psig + 14.7, MPa = psia / 145.038.
    /// </summary>
    private void BuildRps()
    {
        // Permissive interlocks are derived from reactor power each tick in UpdateProtection();
        // these lambdas read the latched permissive states the RPS holds.
        Func<bool> aboveP7 = () => _rps.P7;
        Func<bool> belowP10 = () => !_rps.P10;

        // Variable OTΔT / OPΔT allowable-ΔT setpoints (Westinghouse functional form, in °F).
        double TavgF() => Tavg * 1.8 + 32.0;
        double Ppsig() => PrimaryPressure * 145.038 - 14.7;
        double MeasuredDeltaTF() => (Thot - Tcold) * 1.8;
        double OtDeltaTAllow() => FullPowerDeltaTF *
            (1.14 - 0.0166 * (TavgF() - 588.4) + 0.00091 * (Ppsig() - 2235.0));
        double OpDeltaTAllow() => FullPowerDeltaTF *
            (1.08 - 0.00072 * Math.Max(0.0, TavgF() - 588.4));

        _rps.Functions.Clear();
        // 1 — Power Range Neutron Flux High (high setpoint). Always active. 2/4.
        _rps.Functions.Add(new RpsFunction("Power Range Flux Hi", "高量程中子通量－高",
            RpsTripDir.High, 1.09, () => NeutronPowerFraction));
        // 2 — Power Range Neutron Flux Low setpoint. Active below P-10 (startup protection). 2/4.
        _rps.Functions.Add(new RpsFunction("Power Range Flux Lo", "高量程中子通量－低",
            RpsTripDir.High, 0.25, () => NeutronPowerFraction, permissive: belowP10));
        // 3 — Power Range positive rate (rod-ejection). +5% / 2 s ≈ +0.025 /s. Always active. 2/4.
        _rps.Functions.Add(new RpsFunction("Power Range +Rate", "高量程中子通量－正變化率",
            RpsTripDir.High, 0.025, () => _powerRate, biasSpan: 0.02));
        // 4 — Pressurizer Pressure High: 2385 psig → 16.55 MPa. Always active. 2/4.
        _rps.Functions.Add(new RpsFunction("Pzr Pressure Hi", "穩壓器壓力－高",
            RpsTripDir.High, 16.55, () => PrimaryPressure));
        // 5 — Pressurizer Pressure Low: 1865 psig → 12.96 MPa. Blocked below P-7. 2/4.
        _rps.Functions.Add(new RpsFunction("Pzr Pressure Lo", "穩壓器壓力－低",
            RpsTripDir.Low, 12.96, () => PrimaryPressure, permissive: aboveP7));
        // 6 — Overtemperature ΔT (anti-DNB). Variable setpoint. Always active. 2/4.
        _rps.Functions.Add(new RpsFunction("Overtemp ΔT", "超溫 ΔT",
            RpsTripDir.High, 0.0, MeasuredDeltaTF, setpointFunc: OtDeltaTAllow));
        // 7 — Overpower ΔT (fuel kW/ft limit). Variable setpoint. Always active. 2/4.
        _rps.Functions.Add(new RpsFunction("Overpower ΔT", "超功率 ΔT",
            RpsTripDir.High, 0.0, MeasuredDeltaTF, setpointFunc: OpDeltaTAllow));
        // 8 — Low Reactor Coolant Flow: 90% rated. Blocked below P-7. 2/4.
        _rps.Functions.Add(new RpsFunction("Low RCS Flow", "冷卻劑流量－低",
            RpsTripDir.Low, 0.90, () => CoolantFlowFraction, permissive: aboveP7));
        // 9 — Pressurizer Water Level High: 92%. Blocked below P-7. 2/3.
        _rps.Functions.Add(new RpsFunction("Pzr Level Hi", "穩壓器水位－高",
            RpsTripDir.High, 92.0, () => PressurizerLevel, permissive: aboveP7,
            channelCount: 3));
        // 10 — Steam Generator Level Low-Low: 17%. Always active (initiates AFW). 2/3.
        _rps.Functions.Add(new RpsFunction("SG Level Lo-Lo", "蒸發器水位－低低",
            RpsTripDir.Low, 17.0, () => SteamGenLevel, channelCount: 3));
    }

    // ----------------------------------------------------------------- controls ----
    public void SetRodBank(int bank, double percentInserted)
    {
        if (bank < 0 || bank >= RodBankInsertion.Length) return;
        RodBankInsertion[bank] = Math.Clamp(percentInserted, 0, 100);
    }

    public void Scram()
    {
        IsScrammed = true;
        // ATWS: the trip signal latches but the rods physically fail to insert.
        if (!_rodsFailToInsert)
            for (int i = 0; i < RodBankInsertion.Length; i++) RodBankInsertion[i] = 100.0;
        AutoRodControl = false;
        if (Mode != ReactorMode.Meltdown) Mode = ReactorMode.Tripped;
    }

    /// <summary>Reset a trip so the operator can restart (only if conditions are safe).</summary>
    public void ResetTrip()
    {
        if (Mode == ReactorMode.Meltdown) return;
        _rps.ClearLatch();          // clear the sealed-in RPS trip so the breakers can re-close
        LastTripFunctionEn = ""; LastTripFunctionZh = "";
        IsScrammed = false;
        if (Mode == ReactorMode.Tripped) Mode = ReactorMode.Shutdown;
    }

    public void SetMode(ReactorMode m)
    {
        if (Mode == ReactorMode.Meltdown) return;
        if (m == ReactorMode.Tripped || m == ReactorMode.Meltdown) return;
        Mode = m;
    }

    public void StartRcp(int i) { if (i >= 0 && i < RcpRunning.Length) RcpRunning[i] = true; }
    public void StopRcp(int i) { if (i >= 0 && i < RcpRunning.Length) RcpRunning[i] = false; }

    /// <summary>
    /// 注入一個設計基準事故情景 · Trigger an accident scenario. Emergent — driven by physics,
    /// not scripted: the scenario just changes boundary conditions (break, lost pumps, lost feed, …).
    /// </summary>
    public void TriggerScenario(ReactorScenario s)
    {
        ActiveScenario = s;
        // Clear scenario-specific latches first.
        _breakArea = 0;
        _rodsFailToInsert = false;
        _lofwTimer = 0;
        switch (s)
        {
            case ReactorScenario.Normal:
                break;
            case ReactorScenario.Loca:
                _breakArea = 0.6;          // sizeable cold-leg break
                EccsArmed = true;
                break;
            case ReactorScenario.StationBlackout:
                for (int i = 0; i < RcpRunning.Length; i++) RcpRunning[i] = false;
                RcpFlowDemand = 0;
                FeedwaterFlow = 0;
                EccsArmed = false;        // no AC power for ECCS pumps; passive accumulators only
                break;
            case ReactorScenario.LossOfFeedwater:
                FeedwaterFlow = 0;
                break;
            case ReactorScenario.Atws:
                _rodsFailToInsert = true;  // rods latched but will not move on demand
                break;
            case ReactorScenario.XenonRestart:
                Xenon = Math.Max(Xenon, 2.6); // jump to a post-trip xenon peak (iodine pit)
                break;
        }
    }

    /// <summary>飽和溫度估算 · Saturation temperature of water (°C) at a given pressure (MPa).</summary>
    private static double SatTempAt(double mpa)
    {
        // Smooth fit anchored at ~345 °C @ 15.5 MPa; monotonic over the operating range.
        mpa = Math.Clamp(mpa, 0.05, 22.0);
        return 100.0 + 188.0 * Math.Pow(mpa / 15.5, 0.27);
    }

    /// <summary>渲染快照（不可變）· Immutable snapshot for the 60 fps render clock (no live state mid-frame).</summary>
    public readonly record struct Snapshot(
        double Power, double FuelTemp, double Thot, double Tcold,
        double PrimaryPressure, double SteamPressure, double ElectricMW, double TurbineRpm,
        bool Scrammed, ReactorMode Mode, double DamageAccumulation, double DecayHeatFraction, double FlowFraction);

    public Snapshot Capture() => new(
        _power, FuelTemp, Thot, Tcold, PrimaryPressure, SteamPressure, ElectricPowerMW, TurbineRPM,
        IsScrammed, Mode, DamageAccumulation, DecayHeatFraction, CoolantFlowFraction);

    /// <summary>
    /// 重設整個模擬到冷停機初始狀態 · Reset the entire simulation back to cold-shutdown.
    /// </summary>
    public void Reset()
    {
        _power = 1e-6;
        for (int i = 0; i < 6; i++) _precursor[i] = Beta[i] / (PromptLifetime * Lambda[i]) * _power;
        ReactorPeriodSeconds = 1e9; ReactivityPcm = 0;
        FuelTemp = ColdTemp; Tcold = ColdTemp; Thot = ColdTemp;
        PrimaryPressure = 2.5; PressurizerLevel = NominalPzrLevel;
        SteamPressure = 0.5; SteamGenLevel = 60; ElectricPowerMW = 0; TurbineRPM = 0;
        Iodine = 0; Xenon = 0;
        for (int i = 0; i < RodBankInsertion.Length; i++) RodBankInsertion[i] = 100.0;
        for (int i = 0; i < RcpRunning.Length; i++) RcpRunning[i] = false;
        BoronPpm = NominalBoron; TargetBoronPpm = NominalBoron;
        PressurizerHeater = false; PressurizerSpray = false; RcpFlowDemand = 0;
        FeedwaterFlow = 0; TurbineLoadSetpoint = 0; GeneratorBreakerClosed = false;
        ReliefValveOpen = false; EccsArmed = false; EccsInjecting = false;
        CoolantFlowFraction = 0; DamageAccumulation = 0;
        IsScrammed = false; MeltdownTriggered = false; AutoRodControl = false;
        _rps.ClearLatch(); LastTripFunctionEn = ""; LastTripFunctionZh = ""; _powerRate = 0;
        Mode = ReactorMode.Shutdown;
        for (int i = 0; i < _alarms.Length; i++) _alarms[i] = false;
        for (int i = 0; i < _decayGroup.Length; i++) _decayGroup[i] = 0;
        DecayHeatFraction = 0; SubcoolingMarginC = 0; SourceRangeCps = SourceBaselineCps;
        OneOverM = 1.0; StartupRateDpm = 0; BurnupMwdPerTonne = 0;
        ActiveScenario = ReactorScenario.Normal; _breakArea = 0; _rodsFailToInsert = false;
        AccumulatorInjecting = false; AuxFeedwaterRunning = false; _lofwTimer = 0;
        StatusEn = "Cold shutdown"; StatusZh = "冷停機";
        // External limitation / makeup / forged-fuel state.
        SpentFuelStorageFull = false; ExternalPowerCap = 1e9;
        OperationBlockEn = ""; OperationBlockZh = "";
        MakeupWaterAvailability = 1.0; MakeupWaterInSpec = true; MakeupDemandLpm = 0;
        LowMakeupAlarm = false; ChemistryAlarm = false;
        _forgedTransientTimer = 0; _forgedSeverity = 0; RadiationLevel = 0; CounterfeitFuelAlarm = false;
    }

    // ----------------------------------------------------------------- update ----
    /// <summary>
    /// 推進模擬一個 UI tick · Advance the simulation by <paramref name="dt"/> seconds (UI tick).
    /// Sub-steps the stiff kinetics internally for stability.
    /// </summary>
    public void Update(double dt)
    {
        if (Mode == ReactorMode.Meltdown)
        {
            UpdateDecayHeat(dt);
            UpdateMeltdownPhysics(dt);
            UpdateNis(dt);
            UpdateAlarms();
            return;
        }

        // Held cold shutdown — THE OPERATOR MUST START THE REACTOR UP.
        // While in Shutdown the plant is held subcritical and idle (rods fully in); it does not
        // run by itself. To begin, the operator selects Startup or Run on the Mode selector and
        // then withdraws rods / dilutes boron (see the approach-to-criticality procedure). This
        // also prevents an unattended boot from running the (still-being-calibrated) core up to power.
        if (Mode == ReactorMode.Shutdown)
        {
            UpdateBoron(dt);
            UpdateFlow(dt);
            UpdateDecayHeat(dt);
            UpdateXenon(dt);
            // Neutron power decays to the source level; delayed-neutron precursors die away.
            _power = SourceLevel + Math.Max(0, _power - SourceLevel) * Math.Max(0, 1 - dt / 3.0);
            if (_power < SourceLevel) _power = SourceLevel;
            for (int i = 0; i < 6; i++)
                _precursor[i] = Math.Max(0, _precursor[i] * (1 - Math.Min(1, Lambda[i] * dt)));
            _powerRate = 0;
            ReactivityPcm = -9000; RodReactivityPcm = -8000; ReactorPeriodSeconds = 1e9;
            // Temps & pressure relax toward cold-shutdown conditions.
            double cool = Math.Min(1, dt / 25.0);
            FuelTemp += (ColdTemp - FuelTemp) * cool;
            Tcold += (ColdTemp - Tcold) * cool;
            Thot += (ColdTemp - Thot) * cool;
            double pCold = PressurizerHeater ? 8.0 : 2.5;
            PrimaryPressure += (pCold - PrimaryPressure) * Math.Min(1, dt / 8.0);
            PressurizerLevel += (NominalPzrLevel - PressurizerLevel) * Math.Min(1, dt / 10.0);
            UpdateSecondary(dt);
            // Forged-fuel cladding-breach transient + corrosion keep evolving even after the trip,
            // and the contamination decays slowly. (SIM-only state.)
            UpdateForgedTransient(dt);
            if (DamageAccumulation >= MeltdownDamageThreshold && !MeltdownTriggered) TriggerMeltdown();
            UpdateNis(dt);
            UpdateAlarms();
            UpdateStatus();
            return;
        }

        // --- slow process controls that don't need sub-stepping ---
        UpdateBoron(dt);
        UpdateFlow(dt);
        UpdateDecayHeat(dt);
        UpdateScenarios(dt);

        // --- sub-step the kinetics + thermal coupling ---
        double powerBefore = _power;
        const double subDt = 0.02; // 50 Hz internal integration
        int steps = Math.Max(1, (int)Math.Round(dt / subDt));
        double h = dt / steps;
        for (int s = 0; s < steps; s++)
            StepKineticsAndThermal(h);

        // Power rate-of-change (fraction/s) for the power-range rate trip — lightly smoothed.
        if (dt > 1e-6)
            _powerRate += ((_power - powerBefore) / dt - _powerRate) * Math.Min(1.0, dt / 0.5);

        // --- xenon/iodine evolve slowly; once per tick is fine ---
        UpdateXenon(dt);

        // --- secondary plant + turbine ---
        UpdateSecondary(dt);

        // --- auto rod control ---
        if (AutoRodControl && !IsScrammed) UpdateAutoRods(dt);

        // --- external limitations (waste runback, makeup water, forged-fuel transient) ---
        UpdateMakeupWater(dt);
        UpdateExternalLimits(dt);
        UpdateForgedTransient(dt);

        // --- protection system & failure accumulation ---
        UpdateProtection(dt);
        UpdateNis(dt);
        UpdateAlarms();
        UpdateStatus();
    }

    private void UpdateDecayHeat(double dt)
    {
        // Each group charges toward the current fission power and decays with its time constant.
        double frac = 0;
        for (int i = 0; i < 3; i++)
        {
            double source = DecayFrac[i] * _power; // production proportional to instantaneous power
            _decayGroup[i] += (source - _decayGroup[i] / DecayTau[i]) * dt;
            if (_decayGroup[i] < 0) _decayGroup[i] = 0;
            frac += _decayGroup[i] / DecayTau[i] * DecayTau[i]; // equilibrium ≈ DecayFrac[i]*power
        }
        // Simplify: equilibrium DecayHeatFraction ≈ 0.069*power at steady state; after trip it decays.
        frac = 0;
        for (int i = 0; i < 3; i++) frac += _decayGroup[i];
        DecayHeatFraction = Math.Clamp(frac, 0, 0.10);

        // Burnup accrual + slow MTC drift across the cycle.
        BurnupMwdPerTonne += ThermalPowerMW * dt / 86400.0 / CoreTonnesU; // MWd/tonne
    }

    private void UpdateScenarios(double dt)
    {
        // LOCA: bleed primary pressure/inventory through the break.
        if (_breakArea > 0)
        {
            PrimaryPressure -= 0.9 * _breakArea * dt;
            PressurizerLevel -= 6.0 * _breakArea * dt;
            if (PrimaryPressure < 0.2) PrimaryPressure = 0.2;
            if (PressurizerLevel < 0) PressurizerLevel = 0;
            EccsArmed = true;
        }

        // Passive accumulators discharge once primary pressure falls below ~4.5 MPa.
        AccumulatorInjecting = PrimaryPressure < 4.5 && (FuelTemp > 200 || _breakArea > 0);
        if (AccumulatorInjecting)
        {
            FuelTemp -= 25 * dt;
            Tcold -= 4 * dt; Thot -= 4 * dt;
            PressurizerLevel = Math.Min(100, PressurizerLevel + 4 * dt);
        }

        // Loss of feedwater → aux feedwater auto-starts after 60 s.
        bool feedLost = FeedwaterFlow < 0.02 && (_power > 0.05 || Thot > 150);
        if (feedLost) _lofwTimer += dt; else _lofwTimer = 0;
        AuxFeedwaterRunning = _lofwTimer > 60.0 && ActiveScenario != ReactorScenario.StationBlackout;
        if (AuxFeedwaterRunning) FeedwaterFlow = Math.Max(FeedwaterFlow, 0.15); // small AFW flow
    }

    private void UpdateNis(double dt)
    {
        // Source-range count rate ∝ (source + power); 1/M for the approach-to-criticality plot.
        SourceRangeCps = Math.Clamp((SourceLevel + _power) * 1e11, 1, 1e12);
        OneOverM = Math.Clamp(SourceBaselineCps / SourceRangeCps, 0, 1);
        StartupRateDpm = (ReactorPeriodSeconds > 0 && ReactorPeriodSeconds < 1e8)
            ? 0.4343 * 60.0 / ReactorPeriodSeconds : 0;

        // Sub-cooling margin: positive = sub-cooled liquid; negative = saturation / void risk.
        SubcoolingMarginC = SatTempAt(PrimaryPressure) - Thot;
    }

    private void UpdateBoron(double dt)
    {
        // Charging/dilution moves boron toward target at a limited rate (ppm/s).
        double rate = 4.0; // ppm per second max change
        double diff = TargetBoronPpm - BoronPpm;
        double step = Math.Clamp(diff, -rate * dt, rate * dt);
        BoronPpm = Math.Clamp(BoronPpm + step, 0, 3000);
    }

    private void UpdateFlow(double dt)
    {
        int running = 0;
        foreach (var r in RcpRunning) if (r) running++;
        double pumpFraction = running / (double)RcpRunning.Length;
        double target = pumpFraction * Math.Clamp(RcpFlowDemand, 0, 1);
        // Natural circulation floor when hot and pumps off.
        double natural = (Thot > 100 && running == 0) ? 0.04 : 0.0;
        target = Math.Max(target, natural);
        // First-order lag toward target flow.
        double tau = 3.0;
        CoolantFlowFraction += (target - CoolantFlowFraction) * Math.Min(1, dt / tau);
        CoolantFlowFraction = Math.Clamp(CoolantFlowFraction, 0, 1);
    }

    private void StepKineticsAndThermal(double h)
    {
        // ---- compute reactivity ----
        double rodInsertAvg = 0;
        foreach (var p in RodBankInsertion) rodInsertAvg += p;
        rodInsertAvg /= RodBankInsertion.Length; // 0..100
        // Rods worth: fully inserted (100%) = -TotalRodWorth; withdrawn (0%) = 0.
        double rodRho = -TotalRodWorth * (rodInsertAvg / 100.0);

        double boronRho = BoronWorth * BoronPpm;
        double dopplerRho = DopplerCoeff * (FuelTemp - RefFuelTemp);
        double modRho = ModTempCoeff * (Tavg - RefModTemp);
        // Xenon worth proportional to xenon concentration (normalized so equilibrium full power = 1).
        double xenonRho = XenonWorthFull * Xenon;

        // Excess reactivity from fresh-core / fuel state baseline so that the core can be made
        // critical with rods/boron in the operating band.
        const double ExcessBaseline = 0.080 + BoronWorth * NominalBoron * -1.0; // brings band into reach
        double rho = ExcessBaseline + rodRho + boronRho + dopplerRho + modRho + xenonRho;

        RodReactivityPcm = rodRho * 1e5;
        BoronReactivityPcm = (boronRho + ExcessBaseline) * 1e5; // fold baseline into boron line for display
        DopplerReactivityPcm = dopplerRho * 1e5;
        ModeratorReactivityPcm = modRho * 1e5;
        XenonReactivityPcm = xenonRho * 1e5;
        ReactivityPcm = rho * 1e5;

        // ---- point kinetics (6 groups) ----
        double precursorSum = 0;
        for (int i = 0; i < 6; i++) precursorSum += Lambda[i] * _precursor[i];

        // Backward (implicit) Euler — unconditionally stable for the stiff prompt mode, so a real
        // startup (reactivity insertion) ramps smoothly instead of oscillating sign-to-sign and
        // diverging the way explicit Euler did at this 20 ms step.
        double denom = 1.0 - h * (rho - BetaTotal) / PromptLifetime;
        if (denom < 1e-3) denom = 1e-3; // guard at/above prompt critical
        double newPower = (_power + h * (precursorSum + SourceLevel)) / denom;
        if (newPower < 1e-12) newPower = 1e-12;

        for (int i = 0; i < 6; i++)
        {
            _precursor[i] = (_precursor[i] + h * (Beta[i] / PromptLifetime) * newPower)
                            / (1.0 + h * Lambda[i]);
            if (_precursor[i] < 0) _precursor[i] = 0;
        }

        // Reactor period from rate of change.
        double rate = (newPower - _power) / (Math.Max(_power, 1e-12) * h);
        ReactorPeriodSeconds = Math.Abs(rate) < 1e-9 ? 1e9 : 1.0 / rate;
        _power = newPower;
        if (_power > 50) _power = 50; // numerical guard (5000 % is way past meltdown anyway)

        // ---- thermal-hydraulics (lumped) ----
        StepThermal(h);
    }

    private void StepThermal(double h)
    {
        // Fuel heats from fission power PLUS decay heat (present even after SCRAM — this is what makes
        // station-blackout / loss-of-feedwater emergent: the core keeps heating with no heat sink).
        double q = (_power + DecayHeatFraction) * RatedThermalMW; // MW generated in fuel
        // Heat-transfer fuel->coolant proportional to (Tfuel - Tcoolant).
        double fuelToCoolant = 0.06 * (FuelTemp - Tavg); // MW per °C scaling (lumped)
        double fuelHeatCap = 35.0; // MW·s per °C (fuel lump)
        FuelTemp += (q - fuelToCoolant) / fuelHeatCap * h;
        if (FuelTemp < ColdTemp) FuelTemp = ColdTemp;

        // Coolant: receives fuelToCoolant, rejects heat to SG proportional to flow & secondary delta.
        double sgRemoval = (8.0 + 90.0 * CoolantFlowFraction) * Math.Max(0, Tavg - SecondarySatTemp()) * 0.01;
        sgRemoval *= (0.3 + 0.7 * FeedwaterFlow); // feedwater enables heat sink
        double coolantHeatCap = 60.0; // MW·s per °C
        double netCoolant = fuelToCoolant - sgRemoval;
        double avg = Tavg + netCoolant / coolantHeatCap * h;

        // Flow sets the Thot-Tcold spread for a given power: deltaT ~ power / flow.
        double flow = Math.Max(CoolantFlowFraction, 0.02);
        double deltaT = Math.Clamp(35.0 * _power / flow, 0, 120);
        Tcold = avg - deltaT / 2.0;
        Thot = avg + deltaT / 2.0;
        if (Tcold < ColdTemp) Tcold = ColdTemp;

        // ---- primary pressure (pressurizer) ----
        // Pressure rises with Tavg (thermal expansion / steam bubble), heaters; falls with spray/relief.
        double pTarget = 2.5 + (avg - ColdTemp) * 0.052; // ~15.5 MPa near 305 °C
        if (PressurizerHeater) pTarget += 1.2;
        if (PressurizerSpray) pTarget -= 1.6;
        double pTau = 6.0;
        PrimaryPressure += (pTarget - PrimaryPressure) * Math.Min(1, h / pTau);
        if (ReliefValveOpen && PrimaryPressure > 2.0)
            PrimaryPressure -= 3.0 * h; // relief blows down
        if (PrimaryPressure > VesselBurstPressure)
            PrimaryPressure = VesselBurstPressure;
        if (PrimaryPressure < 0.1) PrimaryPressure = 0.1;

        // ---- pressurizer level tracks Tavg (insurge/outsurge) + ECCS/relief ----
        double lvlTarget = NominalPzrLevel + (avg - NominalTavg) * 0.30;
        if (EccsInjecting) lvlTarget += 25;
        if (ReliefValveOpen) lvlTarget -= 10;
        PressurizerLevel += (lvlTarget - PressurizerLevel) * Math.Min(1, h / 8.0);
        PressurizerLevel = Math.Clamp(PressurizerLevel, 0, 100);
    }

    private double SecondarySatTemp()
    {
        // Saturation temperature of secondary at SteamPressure (rough): ~285 °C at 6.9 MPa.
        return 100.0 + 26.8 * Math.Pow(Math.Max(SteamPressure, 0.05), 0.5) * 1.0 + SteamPressure * 8.0;
    }

    private void UpdateXenon(double dt)
    {
        // I-135 -> Xe-135 -> (decay + burnup). Normalized so equilibrium full power gives Xenon≈1.
        const double lamI = 2.9e-5;  // 1/s (I-135, ~6.6 h)
        const double lamX = 2.1e-5;  // 1/s (Xe-135, ~9.2 h)
        const double gammaI = 1.0;   // production scaling
        const double gammaX = 0.06;  // direct xenon yield scaling
        const double sigmaPhi = 7.0e-5; // burnup rate * flux scaling (per s at full power)

        double phi = _power; // proportional to flux
        double dI = gammaI * phi * lamI / lamI * 1.0; // production ∝ power
        // Use simple balance forms:
        double prodI = 0.95 * phi;            // iodine production ∝ power
        Iodine += (prodI * lamI - lamI * Iodine) * dt; // scaled so equilibrium Iodine≈power
        if (Iodine < 0) Iodine = 0;

        double prodX = lamI * Iodine + gammaX * phi;
        double burn = sigmaPhi * phi * Xenon / 7.0e-5; // burnup ∝ flux*xenon
        double dX = prodX - lamX * Xenon - burn;
        Xenon += dX * dt;
        if (Xenon < 0) Xenon = 0;
        // Normalize: at steady full power, equilibrium Xenon should approach ~1.
    }

    private void UpdateSecondary(double dt)
    {
        // Steam pressure builds from core heat into SG, relieved by turbine steam draw.
        double heatIn = _power; // fraction
        double steamDraw = GeneratorBreakerClosed ? TurbineLoadSetpoint : 0.0;
        double pTarget = 0.5 + 6.5 * Math.Clamp(heatIn - 0.6 * steamDraw + 0.4, 0, 1.2);
        pTarget = Math.Clamp(pTarget, 0.3, 8.5);
        SteamPressure += (pTarget - SteamPressure) * Math.Min(1, dt / 5.0);

        // SG level from feedwater vs steaming.
        double sgTarget = 50 + 20 * (FeedwaterFlow - steamDraw);
        SteamGenLevel += (Math.Clamp(sgTarget, 0, 100) - SteamGenLevel) * Math.Min(1, dt / 6.0);
        SteamGenLevel = Math.Clamp(SteamGenLevel, 0, 100);

        // Turbine: RPM spins up toward 1800 (4-pole 60 Hz nuclear set) when steam available.
        double rpmTarget;
        if (SteamPressure > 3.0 && TurbineLoadSetpoint > 0.01)
            rpmTarget = SyncRpm * Math.Clamp(0.5 + 0.5 * TurbineLoadSetpoint, 0, 1);
        else
            rpmTarget = GeneratorBreakerClosed && SteamPressure > 3.0 ? SyncRpm : 0;
        if (GeneratorBreakerClosed && SteamPressure > 2.0) rpmTarget = SyncRpm; // grid-locked at synchronous speed
        TurbineRPM += (rpmTarget - TurbineRPM) * Math.Min(1, dt / 4.0);
        if (TurbineRPM < 0) TurbineRPM = 0;
        // Overspeed protection: if the breaker is open and load is dumped while steam keeps driving the
        // shaft, an overspeed would trip the stop valves (modelled by capping just under the trip point).
        if (!GeneratorBreakerClosed && TurbineRPM > OverspeedTripRpm) TurbineRPM = OverspeedTripRpm;

        // Electrical output: only when synchronized (breaker closed, near 1800 rpm) & steam present.
        double grossElec = Math.Min(_power * 0.33, TurbineLoadSetpoint) * RatedElectricMW;
        if (GeneratorBreakerClosed && TurbineRPM > SyncRpm * 0.94 && SteamPressure > 3.0)
            ElectricPowerMW += (grossElec - ElectricPowerMW) * Math.Min(1, dt / 4.0);
        else
            ElectricPowerMW += (0 - ElectricPowerMW) * Math.Min(1, dt / 2.0);
        if (ElectricPowerMW < 0) ElectricPowerMW = 0;
    }

    private void UpdateAutoRods(double dt)
    {
        // Simple proportional controller: move average rod position to hold power at setpoint.
        // A mandated external cap (e.g. waste runback) takes priority over the operator setpoint.
        double effSetpoint = Math.Min(AutoPowerSetpoint, ExternalPowerCap);
        double err = effSetpoint - _power; // want power up -> withdraw rods (decrease insertion)
        double gain = 6.0; // %/s per unit error
        double delta = -err * gain * dt; // err>0 (need more power) -> negative delta -> less insertion
        for (int i = 0; i < RodBankInsertion.Length; i++)
            RodBankInsertion[i] = Math.Clamp(RodBankInsertion[i] + delta, 0, 100);
    }

    private void UpdateProtection(double dt)
    {
        // ---- Reactor Protection System: derive permissives from power, then evaluate coincidence. ----
        // P-10 / P-7 ≈ 10 % power; P-8 ≈ 48 %; P-9 ≈ 50 % (P-13 turbine-load input folded into P-7).
        bool p10 = _power >= 0.10;
        bool p13 = TurbineLoadSetpoint >= 0.10 && GeneratorBreakerClosed; // turbine first-stage proxy
        bool p7 = p10 || p13;
        _rps.SetPermissives(p6: _power < 1e-4, p7: p7, p8: _power >= 0.48, p9: _power >= 0.50, p10: p10);
        _rps.Evaluate();
        if (_rps.ReactorTrip && !IsScrammed && Mode != ReactorMode.Meltdown)
        {
            LastTripFunctionEn = _rps.ControllingFunctionEn;
            LastTripFunctionZh = _rps.ControllingFunctionZh;
            Scram();
        }

        // ECCS auto-inject on low pressure if armed.
        EccsInjecting = EccsArmed && PrimaryPressure < 11.0;
        if (EccsInjecting)
        {
            PrimaryPressure += 0.4 * dt;          // restores inventory/pressure somewhat
            FuelTemp -= 30 * dt;                  // strong cooling
            Tcold -= 5 * dt; Thot -= 5 * dt;
        }

        // ---- failure accumulation ----
        bool overTemp = FuelTemp > FuelDamageTemp;
        bool overPress = PrimaryPressure > VesselBurstPressure - 0.2;
        if (overTemp)
            DamageAccumulation += (FuelTemp - FuelDamageTemp) / 100.0 * dt; // faster as hotter
        if (FuelTemp > FuelMeltTemp)
            DamageAccumulation += 25.0 * dt; // very fast once melting
        if (overPress)
            DamageAccumulation += 20.0 * dt;
        if (!overTemp && !overPress && DamageAccumulation > 0)
            DamageAccumulation = Math.Max(0, DamageAccumulation - 0.5 * dt); // slight healing if recovered

        if (DamageAccumulation >= MeltdownDamageThreshold && !MeltdownTriggered)
            TriggerMeltdown();
    }

    private void TriggerMeltdown()
    {
        MeltdownTriggered = true;
        Mode = ReactorMode.Meltdown;
        MeltdownOccurred?.Invoke();
    }

    private void UpdateMeltdownPhysics(double dt)
    {
        // Runaway: fuel keeps heating from decay heat / molten core; pressure spikes then vessel fails.
        FuelTemp += 80 * dt;
        if (FuelTemp > 3500) FuelTemp = 3500;
        Thot = Math.Min(Thot + 20 * dt, 900);
        Tcold = Math.Min(Tcold + 15 * dt, 700);
        PrimaryPressure = Math.Max(PrimaryPressure - 1.0 * dt, 0.1); // breach -> depressurized
        _power = Math.Max(_power * (1 - 0.3 * dt), 0.02); // fission collapses, decay heat remains
        DamageAccumulation = Math.Min(DamageAccumulation + 5 * dt, 999);
    }

    private void SetAlarm(ReactorAlarm a, bool on) => _alarms[(int)a] = on;

    private void UpdateAlarms()
    {
        SetAlarm(ReactorAlarm.HighPower, _power > 1.05);
        SetAlarm(ReactorAlarm.HighNeutronFlux, _power > HighPowerTrip);
        SetAlarm(ReactorAlarm.HighFuelTemp, FuelTemp > FuelDamageTemp * 0.8);
        SetAlarm(ReactorAlarm.HighCoolantTemp, Thot > HighThotTrip - 5);
        SetAlarm(ReactorAlarm.HighPressure, PrimaryPressure > VesselPressureLimit - 0.5);
        SetAlarm(ReactorAlarm.LowPressure, PrimaryPressure < 12.5 && _power > 0.05);
        SetAlarm(ReactorAlarm.LowFlow, CoolantFlowFraction < LowFlowTrip && _power > 0.05);
        SetAlarm(ReactorAlarm.LowPzrLevel, PressurizerLevel < 20);
        SetAlarm(ReactorAlarm.HighPzrLevel, PressurizerLevel > 90);
        SetAlarm(ReactorAlarm.ShortPeriod, ReactorPeriodSeconds > 0 && ReactorPeriodSeconds < 20 && _power > 0.01);
        SetAlarm(ReactorAlarm.Scram, IsScrammed);
        SetAlarm(ReactorAlarm.SteamPressureHigh, SteamPressure > 7.8);
        SetAlarm(ReactorAlarm.CoreDamage, DamageAccumulation > 1.0);
        SetAlarm(ReactorAlarm.EccsActive, EccsInjecting);
        SetAlarm(ReactorAlarm.TurbineTrip, GeneratorBreakerClosed && TurbineRPM < SyncRpm * 0.83 && SteamPressure > 3.0);
        SetAlarm(ReactorAlarm.LowSubcooling, SubcoolingMarginC < 10.0 && (_power > 0.05 || Thot > 200));
        SetAlarm(ReactorAlarm.DecayHeatHigh, DecayHeatFraction > 0.03 && CoolantFlowFraction < 0.4 && FeedwaterFlow < 0.1);
        SetAlarm(ReactorAlarm.AtwsActive, _rodsFailToInsert && IsScrammed);
        SetAlarm(ReactorAlarm.AccumulatorInject, AccumulatorInjecting);
        SetAlarm(ReactorAlarm.AuxFeedwater, AuxFeedwaterRunning);
        bool natCirc = CoolantFlowFraction > 0.005 && CoolantFlowFraction < 0.1 && Thot > 150;
        SetAlarm(ReactorAlarm.NaturalCirc, natCirc);
    }

    private void UpdateStatus()
    {
        if (Mode == ReactorMode.Meltdown) { StatusEn = "CORE MELTDOWN"; StatusZh = "爐心熔毀"; return; }
        if (IsScrammed)
        {
            StatusEn = LastTripFunctionEn.Length > 0 ? $"SCRAM — {LastTripFunctionEn}" : "SCRAM — reactor tripped";
            StatusZh = LastTripFunctionZh.Length > 0 ? $"緊急停堆 — {LastTripFunctionZh}" : "緊急停堆（SCRAM）";
            return;
        }
        if (DamageAccumulation > 1.0) { StatusEn = "WARNING — core damage accruing"; StatusZh = "警告 — 爐心受損中"; return; }
        if (_power < 1e-4) { StatusEn = "Shut down / subcritical"; StatusZh = "停機／次臨界"; Mode = Mode == ReactorMode.Run ? ReactorMode.Startup : Mode; return; }
        if (Math.Abs(_power - 1.0) < 0.03 && Math.Abs(ReactorPeriodSeconds) > 200)
        { StatusEn = "Stable at power"; StatusZh = "穩定運轉"; return; }
        if (ReactorPeriodSeconds > 0 && ReactorPeriodSeconds < 60)
        { StatusEn = "Rising power — watch period"; StatusZh = "功率上升中 — 注意週期"; return; }
        StatusEn = "Operating"; StatusZh = "運轉中";
    }

    // =================================================================== HTML5 BRIDGE ====
    // Additive-only surface for the WebView2/HTML5 control room. NO physics here — these are a
    // read-only snapshot exporter and a clamped write dispatcher that funnels every JS action
    // through the EXISTING public setters/methods above. JS therefore cannot reach the model
    // directly; the held-cold-shutdown and meltdown branches in Update() are untouched.

    /// <summary>
    /// 燃料可用性閘 · Fuel-availability gate. The window sets this each tick from the
    /// FuelFactoryService. When false, rod withdrawal and Startup/Run mode changes are ignored in
    /// <see cref="ApplyControl"/> (pure input gating — no physics edit), so an empty core cannot
    /// be taken critical.
    /// </summary>
    public bool FuelAvailable { get; set; } = true;

    /// <summary>燃料阻擋備註 · Last reason a control was refused due to no fuel (bilingual).</summary>
    public string FuelGateNoteEn { get; private set; } = "";
    public string FuelGateNoteZh { get; private set; } = "";

    // =================================================================== EXTERNAL LIMITATIONS ====
    // Additive, input-only couplings driven from the window tick by the waste store and the water
    // treatment plant. They never reach into the kinetics directly — they (a) gate operator inputs,
    // (b) impose a power CAP / runback, and (c) bias the makeup-level dynamics. The held-cold-shutdown
    // and meltdown branches of Update() remain untouched.

    /// <summary>乏燃料貯存已滿 · Spent-fuel storage is FULL (waste at/over the cap). When true the plant
    /// must run back power and — after a grace — be forced to shutdown; startup is blocked.</summary>
    public bool SpentFuelStorageFull { get; set; }

    /// <summary>外部強加的功率上限（分數，0..1.2）· An externally-mandated power cap (e.g. waste runback).
    /// 1e9 means "no cap". The auto-rod controller and a gentle insertion bias honour it.</summary>
    public double ExternalPowerCap { get; set; } = 1e9;

    /// <summary>運轉封鎖原因（雙語）· If non-empty, startup/run is externally blocked (waste full).</summary>
    public string OperationBlockEn { get; set; } = "";
    public string OperationBlockZh { get; set; } = "";
    public bool OperationBlocked => OperationBlockEn.Length > 0;

    // ----- makeup water (from the Water Treatment plant) -----
    /// <summary>可用補水（0..1，治理水箱供應充足度）· Makeup-water availability from the treated-water
    /// tank (0 = tank empty, 1 = ample). Below ~0.15 the plant cannot maintain pzr/SG level.</summary>
    public double MakeupWaterAvailability { get; set; } = 1.0;
    /// <summary>補水水質達標（電導率/溶氧/氯化物在規範內）· True when makeup chemistry is in-spec.</summary>
    public bool MakeupWaterInSpec { get; set; } = true;
    /// <summary>補水需求（L/min，供水處理廠按功率＋洩漏估算）· Current makeup demand for the treatment plant.</summary>
    public double MakeupDemandLpm { get; private set; }
    public bool LowMakeupAlarm { get; private set; }
    public bool ChemistryAlarm { get; private set; }

    // ----- forged / counterfeit fuel harm (SIM ONLY) -----
    /// <summary>偽冒燃料破損計時 · Counts down while a counterfeit-fuel cladding-breach transient is active.</summary>
    private double _forgedTransientTimer;
    private double _forgedSeverity;     // 0..1 how bad the forgery is
    /// <summary>輻射／污染水平（0..1，偽冒燃料釋放裂變產物時上升）· Radiation/contamination level.</summary>
    public double RadiationLevel { get; private set; }
    public bool CounterfeitFuelAlarm { get; private set; }

    /// <summary>強加運轉封鎖（雙語）· Externally block startup/run (waste full). Pass empty to clear.</summary>
    public void SetOperationBlock(string en, string zh) { OperationBlockEn = en ?? ""; OperationBlockZh = zh ?? ""; }

    /// <summary>
    /// 注入偽冒／不合格燃料的破壞性暫態（僅限模擬）· Inject a damaging transient from loading
    /// counterfeit / off-spec fuel. SIM-ONLY: this only perturbs the simulated reactor state
    /// (fuel-temp spike, reactivity peaking, core DamageAccumulation, radiation, auto-SCRAM). It does
    /// NOT touch any real machine state. <paramref name="severity"/> in [0,1] scales the harm.
    /// </summary>
    public void InjectForgedFuelHarm(double severity)
    {
        if (Mode == ReactorMode.Meltdown) return;
        severity = Math.Clamp(severity, 0.05, 1.0);
        _forgedSeverity = Math.Max(_forgedSeverity, severity);
        _forgedTransientTimer = Math.Max(_forgedTransientTimer, 6.0 + 10.0 * severity); // seconds of transient
        CounterfeitFuelAlarm = true;

        // Immediate cladding-breach insults (all SIMULATED quantities only):
        // local power peaking / fuel-temperature spike, contamination, and a damage seed.
        FuelTemp += 350.0 * severity;                       // °C local fuel-temp spike
        _power += 0.20 * severity;                          // prompt power peaking
        DamageAccumulation += 12.0 * severity;             // core damage toward meltdown
        RadiationLevel = Math.Clamp(RadiationLevel + 0.5 * severity, 0, 1);
        // Mandatory protective trip on a fuel-handling fault.
        Scram();
    }

    private void UpdateForgedTransient(double dt)
    {
        if (_forgedTransientTimer > 0)
        {
            _forgedTransientTimer -= dt;
            // Sustained insult while the breach evolves: continued fuel heating + damage accrual.
            FuelTemp += 60.0 * _forgedSeverity * dt;
            DamageAccumulation += 6.0 * _forgedSeverity * dt;
            RadiationLevel = Math.Clamp(RadiationLevel + 0.05 * _forgedSeverity * dt, 0, 1);
            if (_forgedTransientTimer <= 0) { _forgedSeverity = 0; }
        }
        else
        {
            CounterfeitFuelAlarm = false;
        }
        // Radiation/contamination decays slowly once the transient is over.
        if (_forgedTransientTimer <= 0 && RadiationLevel > 0)
            RadiationLevel = Math.Max(0, RadiationLevel - 0.01 * dt);
    }

    private void UpdateMakeupWater(double dt)
    {
        // Makeup demand scales with power plus any leakage/relief/ECCS draw. Drives the WT plant sizing.
        double leak = (ReliefValveOpen ? 0.25 : 0) + (EccsInjecting ? 0.4 : 0)
                      + (ActiveScenario == ReactorScenario.Loca ? 0.5 : 0);
        MakeupDemandLpm = (40.0 + 360.0 * Math.Clamp(_power, 0, 1.2) + 600.0 * leak); // L/min, toy scale

        // If the treated-water tank is depleted, the plant cannot maintain inventory: pzr & SG levels
        // sag toward a low value, raising low-level alarms and (via the RPS) eventually a trip.
        double avail = Math.Clamp(MakeupWaterAvailability, 0, 1);
        LowMakeupAlarm = avail < 0.15 && (_power > 0.05 || Thot > 150);
        if (avail < 0.5 && (_power > 0.02 || Thot > 120))
        {
            double deficit = (0.5 - avail) / 0.5;            // 0..1 how starved we are
            double pull = deficit * dt;                       // % per second sag, scaled
            PressurizerLevel = Math.Max(0, PressurizerLevel - 2.5 * pull);
            SteamGenLevel = Math.Max(0, SteamGenLevel - 2.0 * pull);
        }

        // Off-spec chemistry (high conductivity / O2 / chlorides) -> slow corrosion damage accrual.
        ChemistryAlarm = !MakeupWaterInSpec && (_power > 0.05 || Thot > 150);
        if (ChemistryAlarm)
            DamageAccumulation += 0.15 * dt; // slow corrosion (well below over-temp accrual)
    }

    private void UpdateExternalLimits(double dt)
    {
        // Enforce a mandated power cap by gently inserting rods when above it (a controlled runback),
        // independent of the operator's auto/manual rod selection. Honours ATWS (rods may not move).
        if (ExternalPowerCap < 1.1 && _power > ExternalPowerCap + 0.01 && !_rodsFailToInsert)
        {
            double over = _power - ExternalPowerCap;
            double bias = Math.Clamp(over, 0, 1) * 12.0 * dt; // %/s insertion proportional to overshoot
            for (int i = 0; i < RodBankInsertion.Length; i++)
                RodBankInsertion[i] = Math.Clamp(RodBankInsertion[i] + bias, 0, 100);
        }
    }

    /// <summary>
    /// 匯出整個反應堆狀態為 JSON · Export the full reactor state as a flat JSON snapshot for the
    /// HTML5 client (one object posted per 10 Hz tick).
    /// </summary>
    public string ExportStateJson(double clock, bool zhPrimary)
    {
        var alarms = new System.Collections.Generic.List<int>();
        foreach (ReactorAlarm a in Enum.GetValues(typeof(ReactorAlarm)))
            if (_alarms[(int)a]) alarms.Add((int)a);

        var funcs = new System.Collections.Generic.List<object>();
        foreach (var f in _rps.Functions)
        {
            funcs.Add(new
            {
                nameEn = f.NameEn,
                nameZh = f.NameZh,
                tripped = f.FunctionTrip,
                partial = f.PartialTrip,
                blocked = f.Blocked,
                setpoint = f.Setpoint,
                channelTrips = f.ChannelTripped,
            });
        }

        var dto = new
        {
            type = "state",
            clock,
            zhPrimary,
            mode = (int)Mode,
            statusEn = StatusEn,
            statusZh = StatusZh,
            // power / kinetics
            power = NeutronPowerFraction,
            thermalMW = ThermalPowerMW,
            electricMW = ElectricPowerMW,
            periodS = ReactorPeriodSeconds,
            reactivityPcm = ReactivityPcm,
            rodPcm = RodReactivityPcm,
            boronPcm = BoronReactivityPcm,
            dopplerPcm = DopplerReactivityPcm,
            modPcm = ModeratorReactivityPcm,
            xenonPcm = XenonReactivityPcm,
            // thermal-hydraulics
            fuelTemp = FuelTemp,
            tcold = Tcold,
            thot = Thot,
            tavg = Tavg,
            primaryPressureMPa = PrimaryPressure,
            pzrLevel = PressurizerLevel,
            steamPressureMPa = SteamPressure,
            sgLevel = SteamGenLevel,
            // poisons
            iodine = Iodine,
            xenon = Xenon,
            // controls (echo back so JS sliders track the model)
            rodBank = RodBankInsertion,
            boronPpm = BoronPpm,
            targetBoronPpm = TargetBoronPpm,
            pzrHeater = PressurizerHeater,
            pzrSpray = PressurizerSpray,
            rcpFlowDemand = RcpFlowDemand,
            rcpRunning = RcpRunning,
            feedwaterFlow = FeedwaterFlow,
            turbineLoad = TurbineLoadSetpoint,
            genBreaker = GeneratorBreakerClosed,
            reliefValve = ReliefValveOpen,
            eccsArmed = EccsArmed,
            eccsInjecting = EccsInjecting,
            autoRods = AutoRodControl,
            autoSetpoint = AutoPowerSetpoint,
            // turbine / flow / decay
            turbineRpm = TurbineRPM,
            syncRpm = SyncRpm,
            flowFraction = CoolantFlowFraction,
            decayHeat = DecayHeatFraction,
            subcoolingC = SubcoolingMarginC,
            // NIS
            sourceCps = SourceRangeCps,
            oneOverM = OneOverM,
            startupRateDpm = StartupRateDpm,
            burnup = BurnupMwdPerTonne,
            // safety / failure
            scrammed = IsScrammed,
            damage = DamageAccumulation,
            meltdown = MeltdownTriggered,
            scenario = (int)ActiveScenario,
            accumInject = AccumulatorInjecting,
            auxFeed = AuxFeedwaterRunning,
            lastTripEn = LastTripFunctionEn,
            lastTripZh = LastTripFunctionZh,
            alarms,
            rps = new
            {
                trip = _rps.ReactorTrip,
                latched = _rps.TripLatched,
                funcs,
                perms = new { p6 = _rps.P6, p7 = _rps.P7, p8 = _rps.P8, p9 = _rps.P9, p10 = _rps.P10 },
            },
            // fuel gate (window injects FuelAvailable before serialize)
            fuelLoaded = FuelAvailable,
            fuelCanRun = FuelAvailable,
            fuelGateEn = FuelGateNoteEn,
            fuelGateZh = FuelGateNoteZh,
            // external limitations + makeup water + forged-fuel harm (sim-only)
            spentFuelFull = SpentFuelStorageFull,
            externalPowerCap = ExternalPowerCap > 1.5 ? -1.0 : ExternalPowerCap,
            operationBlocked = OperationBlocked,
            operationBlockEn = OperationBlockEn,
            operationBlockZh = OperationBlockZh,
            makeupAvail = MakeupWaterAvailability,
            makeupInSpec = MakeupWaterInSpec,
            makeupDemandLpm = MakeupDemandLpm,
            lowMakeupAlarm = LowMakeupAlarm,
            chemistryAlarm = ChemistryAlarm,
            radiationLevel = RadiationLevel,
            counterfeitAlarm = CounterfeitFuelAlarm,
        };
        return System.Text.Json.JsonSerializer.Serialize(dto);
    }

    /// <summary>
    /// 由 HTML5 客戶端套用一個控制動作 · Apply one control action from the HTML5 client. Every value is
    /// clamped / routed through an existing public setter — this is the WHOLE write surface JS has.
    /// </summary>
    public void ApplyControl(string? action, int index, double value, bool flag)
    {
        if (string.IsNullOrEmpty(action)) return;

        // Fuel gate: with no loaded fuel, refuse rod withdrawal and Run/Startup so an empty core
        // cannot be taken critical. (Pure input gating — physics untouched.)
        bool wantsWithdraw =
            (action == "setRod" || action == "setAllRods") && value < AverageRodInsertion();
        bool wantsRun = action == "setMode" && (index == (int)ReactorMode.Startup || index == (int)ReactorMode.Run);
        if (!FuelAvailable && (wantsWithdraw || wantsRun))
        {
            FuelGateNoteEn = "No fuel loaded — load a valid assembly before startup.";
            FuelGateNoteZh = "未裝燃料 — 啟動前請先裝入有效燃料組件。";
            return;
        }

        // Waste-full operation block: cannot take the core critical / start up when there is nowhere
        // left to put spent fuel. Operator must dispose waste below the cap first.
        if (OperationBlocked && (wantsWithdraw || wantsRun))
        {
            FuelGateNoteEn = OperationBlockEn;
            FuelGateNoteZh = OperationBlockZh;
            return;
        }

        switch (action)
        {
            case "setRod": SetRodBank(index, value); break;
            case "setAllRods":
                for (int i = 0; i < RodBankInsertion.Length; i++) SetRodBank(i, value);
                break;
            case "setBoronTarget": TargetBoronPpm = Math.Clamp(value, 0, 3000); break;
            case "pzrHeater": PressurizerHeater = flag; break;
            case "pzrSpray": PressurizerSpray = flag; break;
            case "reliefValve": ReliefValveOpen = flag; break;
            case "rcpStart": StartRcp(index); break;
            case "rcpStop": StopRcp(index); break;
            case "rcpFlow": RcpFlowDemand = Math.Clamp(value, 0, 1); break;
            case "feedwater": FeedwaterFlow = Math.Clamp(value, 0, 1); break;
            case "turbineLoad": TurbineLoadSetpoint = Math.Clamp(value, 0, 1); break;
            case "genBreaker": GeneratorBreakerClosed = flag; break;
            case "eccsArm": EccsArmed = flag; break;
            case "autoRods": AutoRodControl = flag; break;
            case "autoSetpoint": AutoPowerSetpoint = Math.Clamp(value, 0, 1.2); break;
            case "setMode": SetMode((ReactorMode)index); break;
            case "scram": Scram(); break;
            case "resetTrip": ResetTrip(); break;
            case "reset": Reset(); break;
            case "scenario": TriggerScenario((ReactorScenario)index); break;
        }
    }

    private double AverageRodInsertion()
    {
        double s = 0; foreach (var p in RodBankInsertion) s += p;
        return s / RodBankInsertion.Length;
    }

    // =================================================================== PERSISTENCE ====
    // 可序列化嘅完整模擬狀態快照（畀 PersistenceService 防崩潰自動儲存／還原用）。
    // A fully-serializable snapshot of the simulation state, used by PersistenceService for
    // crash/shutdown-safe autosave + restore. Every volatile quantity is captured so the reactor
    // resumes seamlessly: power & 6-group precursors, temps, pressures, pzr/SG levels, xenon/iodine,
    // rod positions, boron, mode, setpoints, alarms, damage and the sim clock.
    public sealed class PersistSnapshot
    {
        public int Version { get; set; } = 1;

        // Kinetics
        public double Power { get; set; }
        public double[] Precursor { get; set; } = new double[6];
        public double SourceLevel { get; set; }
        public double ReactorPeriodSeconds { get; set; }

        // Thermal-hydraulics
        public double FuelTemp { get; set; }
        public double Tcold { get; set; }
        public double Thot { get; set; }
        public double PrimaryPressure { get; set; }
        public double PressurizerLevel { get; set; }
        public double SteamPressure { get; set; }
        public double SteamGenLevel { get; set; }
        public double ElectricPowerMW { get; set; }
        public double TurbineRPM { get; set; }
        public double CoolantFlowFraction { get; set; }

        // Poisons
        public double Iodine { get; set; }
        public double Xenon { get; set; }

        // Controls
        public double[] RodBankInsertion { get; set; } = new double[4];
        public double BoronPpm { get; set; }
        public double TargetBoronPpm { get; set; }
        public bool PressurizerHeater { get; set; }
        public bool PressurizerSpray { get; set; }
        public double RcpFlowDemand { get; set; }
        public bool[] RcpRunning { get; set; } = new bool[4];
        public double FeedwaterFlow { get; set; }
        public double TurbineLoadSetpoint { get; set; }
        public bool GeneratorBreakerClosed { get; set; }
        public bool ReliefValveOpen { get; set; }
        public bool EccsArmed { get; set; }
        public bool AutoRodControl { get; set; }
        public double AutoPowerSetpoint { get; set; }

        // Safety / state
        public double DamageAccumulation { get; set; }
        public bool IsScrammed { get; set; }
        public bool MeltdownTriggered { get; set; }
        public int Mode { get; set; }
        public bool[] Alarms { get; set; } = Array.Empty<bool>();

        // Sim clock (seconds since the page started the run); the page owns it but we persist it here.
        public double SimClockSeconds { get; set; }
    }

    /// <summary>影一個完整狀態快照 · Capture a complete serializable snapshot of the simulation.</summary>
    public PersistSnapshot CaptureSnapshot(double simClockSeconds)
    {
        var s = new PersistSnapshot
        {
            Power = _power,
            SourceLevel = SourceLevel,
            ReactorPeriodSeconds = ReactorPeriodSeconds,
            FuelTemp = FuelTemp, Tcold = Tcold, Thot = Thot,
            PrimaryPressure = PrimaryPressure, PressurizerLevel = PressurizerLevel,
            SteamPressure = SteamPressure, SteamGenLevel = SteamGenLevel,
            ElectricPowerMW = ElectricPowerMW, TurbineRPM = TurbineRPM,
            CoolantFlowFraction = CoolantFlowFraction,
            Iodine = Iodine, Xenon = Xenon,
            BoronPpm = BoronPpm, TargetBoronPpm = TargetBoronPpm,
            PressurizerHeater = PressurizerHeater, PressurizerSpray = PressurizerSpray,
            RcpFlowDemand = RcpFlowDemand, FeedwaterFlow = FeedwaterFlow,
            TurbineLoadSetpoint = TurbineLoadSetpoint, GeneratorBreakerClosed = GeneratorBreakerClosed,
            ReliefValveOpen = ReliefValveOpen, EccsArmed = EccsArmed,
            AutoRodControl = AutoRodControl, AutoPowerSetpoint = AutoPowerSetpoint,
            DamageAccumulation = DamageAccumulation, IsScrammed = IsScrammed,
            MeltdownTriggered = MeltdownTriggered, Mode = (int)Mode,
            SimClockSeconds = simClockSeconds,
            Precursor = (double[])_precursor.Clone(),
            RodBankInsertion = (double[])RodBankInsertion.Clone(),
            RcpRunning = (bool[])RcpRunning.Clone(),
            Alarms = (bool[])_alarms.Clone(),
        };
        return s;
    }

    /// <summary>由快照還原整個模擬狀態 · Restore the entire simulation from a snapshot. Never throws.</summary>
    public void RestoreSnapshot(PersistSnapshot s)
    {
        if (s is null) return;
        try
        {
            _power = s.Power > 0 ? s.Power : 1e-12;
            if (s.Precursor is { Length: 6 }) Array.Copy(s.Precursor, _precursor, 6);
            SourceLevel = s.SourceLevel > 0 ? s.SourceLevel : 1e-8;
            ReactorPeriodSeconds = s.ReactorPeriodSeconds;
            FuelTemp = s.FuelTemp; Tcold = s.Tcold; Thot = s.Thot;
            PrimaryPressure = s.PrimaryPressure; PressurizerLevel = s.PressurizerLevel;
            SteamPressure = s.SteamPressure; SteamGenLevel = s.SteamGenLevel;
            ElectricPowerMW = s.ElectricPowerMW; TurbineRPM = s.TurbineRPM;
            CoolantFlowFraction = s.CoolantFlowFraction;
            Iodine = s.Iodine; Xenon = s.Xenon;
            if (s.RodBankInsertion is { Length: 4 })
                for (int i = 0; i < 4; i++) RodBankInsertion[i] = s.RodBankInsertion[i];
            BoronPpm = s.BoronPpm; TargetBoronPpm = s.TargetBoronPpm;
            PressurizerHeater = s.PressurizerHeater; PressurizerSpray = s.PressurizerSpray;
            RcpFlowDemand = s.RcpFlowDemand;
            if (s.RcpRunning is { Length: 4 })
                for (int i = 0; i < 4; i++) RcpRunning[i] = s.RcpRunning[i];
            FeedwaterFlow = s.FeedwaterFlow; TurbineLoadSetpoint = s.TurbineLoadSetpoint;
            GeneratorBreakerClosed = s.GeneratorBreakerClosed; ReliefValveOpen = s.ReliefValveOpen;
            EccsArmed = s.EccsArmed; AutoRodControl = s.AutoRodControl;
            AutoPowerSetpoint = s.AutoPowerSetpoint;
            DamageAccumulation = s.DamageAccumulation;
            IsScrammed = s.IsScrammed; MeltdownTriggered = s.MeltdownTriggered;
            Mode = Enum.IsDefined(typeof(ReactorMode), s.Mode) ? (ReactorMode)s.Mode : ReactorMode.Shutdown;
            if (s.Alarms is not null && s.Alarms.Length == _alarms.Length)
                Array.Copy(s.Alarms, _alarms, _alarms.Length);
            UpdateStatus();
        }
        catch { /* never throw into the UI — leave the sim at whatever was restored */ }
    }

    // =================================================================== REAL SHUTDOWN ====
    // Win32 shutdown helper. SAFE BY DEFAULT: the caller only invokes this after the user has
    // explicitly armed the toggle AND a 10-second abortable countdown elapsed. We use the Win32
    // API (advapi32 InitiateSystemShutdownExW) — NOT shutdown.exe — and request a normal
    // (non-forced) shutdown so applications can save unsaved work.

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privilege; }

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x0002;
    private const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";

    // Shutdown reason codes (major: other/software; flagged planned).
    private const uint SHTDN_REASON_MAJOR_OTHER = 0x00000000;
    private const uint SHTDN_REASON_MINOR_OTHER = 0x00000000;
    private const uint SHTDN_REASON_FLAG_PLANNED = 0x80000000;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool LookupPrivilegeValueW(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool InitiateSystemShutdownExW(
        string? lpMachineName, string? lpMessage, uint dwTimeout,
        bool bForceAppsClosed, bool bRebootAfterShutdown, uint dwReason);

    /// <summary>
    /// 啟用關機權限 · Enable SeShutdownPrivilege on the current process token.
    /// </summary>
    private static bool EnableShutdownPrivilege()
    {
        IntPtr token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                return false;
            if (!LookupPrivilegeValueW(null, SE_SHUTDOWN_NAME, out LUID luid))
                return false;
            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Privilege = new LUID_AND_ATTRIBUTES { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED },
            };
            return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            if (token != IntPtr.Zero) CloseHandle(token);
        }
    }

    /// <summary>
    /// 真實關機（經 Win32 API，非 shutdown.exe）· Initiate a REAL Windows shutdown via the Win32
    /// API. Non-forced (apps can save). Returns true if the request was accepted. The caller is
    /// responsible for the safety gating (default-off toggle + abortable countdown).
    /// </summary>
    public static bool InitiateRealShutdown(string message)
    {
        if (!EnableShutdownPrivilege()) return false;
        // dwTimeout 0 => shut down immediately (the abortable countdown already happened in-app).
        // bForceAppsClosed = false => normal shutdown so apps get a chance to save.
        return InitiateSystemShutdownExW(
            null, message, 0, false, false,
            SHTDN_REASON_MAJOR_OTHER | SHTDN_REASON_MINOR_OTHER | SHTDN_REASON_FLAG_PLANNED);
    }
}
