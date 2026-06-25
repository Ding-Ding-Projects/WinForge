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
    SgtrLeak,
    SecondaryRadiationHi,
    SgReliefLift,
    RodInsertionLimitLo,
    RodInsertionLimitLoLo,
    RodDeviation,
    AxialFluxDiffOutOfBand,
    SteamlineBreak,
    SafetyInjection,
    PzrCodeSafetyOpen,
    ContainmentPressureHi,
    ContainmentIsolation,
    ContainmentSpray,
    LossOfOffsitePower,
    StationBlackout,
    EdgSupplyingBus,
    TurbineDrivenAfw,
    DcBusDepleted,
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

    // --- Westinghouse rod-control geometry / sequencing ---
    // Each control bank spans 228 steps (0 = fully inserted, 228 = fully withdrawn); banks withdraw
    // in sequence A→B→C→D with a 128-step overlap, so the demand counter spans 4·228 − 3·128 = 528.
    private const int RodStepsPerBank = 228;
    private const int RodOverlap      = 128;
    private const int RodStride       = RodStepsPerBank - RodOverlap;          // 100
    private const int RodTotalSpan    = 4 * RodStepsPerBank - 3 * RodOverlap;  // 528
    // Per-bank integral-worth fractions (A,B,C,D). MUST sum to 1.0 so the all-in / all-out endpoints
    // that the criticality baseline is tuned to are preserved exactly. Lead (regulating) bank D smallest.
    private static readonly double[] RodWorthFrac = { 0.30, 0.27, 0.23, 0.20 };
    private const int RilLowLowBias        = 15; // steps the Low-Low insertion limit sits below Low
    private const int RodDeviationBandSteps = 12; // LCO 3.1.4 rod-alignment / deviation band (steps)

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

    // Reactor-coolant-pump (RCP) flow dynamics. Real loop flow does NOT follow a symmetric lag: a running
    // pump spins up to rated in a couple of seconds, but a tripped pump COASTS DOWN on its flywheel/fluid
    // inertia along a HYPERBOLIC curve G(t)=G0/(1+t/τ½) — flow halves at t=τ½, then tails off slowly toward
    // the single-phase natural-circulation floor. The flywheel (high WR²) deliberately stretches τ½ to hold
    // DNBR margin through the first seconds of a loss-of-flow. Each of the 4 loops carries 1/4 of rated flow.
    private const double RcpSpinUpTau   = 1.5;   // s    per-pump first-order spin-up lag (energised pump)
    private const double RcpCoastHalf   = 8.0;   // s    flow-halving time of a tripped pump's coastdown (W 4-loop w/ flywheel)
    private const double RcpLoopShare    = 0.25;  // -    rated flow fraction carried by one of the 4 loops
    // Single-phase natural circulation: buoyancy thermosiphon once pumps are gone. Driving head ∝ ρ·g·β·ΔT·H
    // with ΔT = Q/(W·cp) and turbulent loop resistance ∝ W² ⇒ W ∝ Q^(1/3) (cube-root law). Calibrated so the
    // post-trip iodine-pit decay level (~1.5 % core power) gives ~4 % rated flow, rising to ~6 % right after a
    // from-power trip; capped at a physical single-phase ceiling. Gated on a real hot/cold ΔT head and an
    // intact SG heat sink — lose the secondary inventory and the thermosiphon stalls.
    private const double NatCircCoef    = 0.16;  // -    W∝Q^(1/3) coefficient (cube-root natural-circ scaling)
    private const double NatCircMax     = 0.08;  // -    physical single-phase natural-circ ceiling (8 % rated)
    private const double NatCircDtMin   = 8.0;   // °C   min hot-cold ΔT before a thermosiphon establishes
    private const double NatCircHeadSpan = 20.0; // °C   ΔT span over which the buoyancy head ramps in
    private const double NatCircSinkSpan = 15.0; // °C   primary-to-secondary head span gating the SG heat sink

    // Pressurizer pressure-control program (Westinghouse 4-loop), converted from psig to MPa absolute.
    private const double PzrProgramPressure = 15.51; // 2235 psig — nominal program pressure
    private const double PzrPropHeaterFull  = 15.41; // 2220 psig — proportional heaters full-on
    private const double PzrBackupHeaterOn  = 15.34; // 2210 psig — backup heater banks energize
    private const double PzrSprayOpen       = 15.68; // 2260 psig — spray valves begin to open
    private const double PzrSprayFull       = 16.03; // 2310 psig — spray valves full open
    private const double PorvOpenPressure   = 16.20; // 2335 psig — power-operated relief valve opens
    private const double PorvClosePressure  = 16.06; // 2315 psig — PORV reseats
    // Pressurizer lumped thermal-hydraulic gains (primary pressure = Psat of the pzr liquid temperature).
    private const double PzrHeatCap     = 15.0;   // MW·s/°C   pressurizer-liquid lump heat capacity
    private const double PzrHeaterMW    = 1.8;    // MW        full heater-bank power (~1800 kW)
    private const double PzrSprayK      = 0.06;   // MW/°C     spray condensation gain at full spray
    private const double PzrSurgeK      = 0.40;   // MW per (%/s) per °C  insurge enthalpy gain
    private const double PzrLossK       = 0.003;  // MW/°C     ambient standing loss
    private const double PzrMakeupFloor = 2.5;    // MPa       charging/makeup-held cold pressure floor
    private const double PzrCompK       = 0.40;   // MPa per (%/s)  adiabatic bubble-compression spike gain
    private const double PzrSpikeTau    = 8.0;    // s         compression-spike relaxation
    private const double PzrPresTau     = 2.0;    // s         output-pressure lag (slow dynamics live in _tpzr)
    private const double PorvReliefRate = 2.5;    // MPa/s     PORV blowdown rate while lifted
    // Pressurizer ASME Section III code (spring) safety valves — three self-actuated valves ABOVE the PORV,
    // the last-ditch RCS overpressure protection. Westinghouse 4-loop standard set 2485 psig (≈2500 psia,
    // the RCS design pressure). MPa_abs = (psig + 14.7) / 145.038. The three are staggered within ±1%
    // as-found tolerance so they don't all pop on the same simulation step. Full lift at +3% accumulation
    // (2560 psig) keeps peak RCS pressure under the 110%-design ASME service limit (2750 psia). Blowdown
    // ~5% below each valve's own set gives the open-vs-reseat hysteresis that prevents chatter; every reseat
    // point stays above PORV-close (16.06) so the safeties and PORV never fight. A stuck-open code safety
    // would behave as a small-break LOCA (the TMI-2 analog) — they are not designed for frequent cycling.
    private const double PzrSafety1Set      = 17.18; // 2477 psig (−0.3% tol) — code safety #1 lift
    private const double PzrSafety2Set      = 17.24; // 2485 psig (nominal)   — code safety #2 lift
    private const double PzrSafety3Set      = 17.30; // 2494 psig (+0.4% tol) — code safety #3 lift
    private const double PzrSafetyAccum     = 17.75; // 2560 psig (+3% accumulation) — pressure at full lift
    private const double PzrSafetyBlowdown  = 0.86;  // MPa (~5% of set) — open→reseat hysteresis band
    private const double PzrSafetyReliefRate = 4.0;  // MPa/s per valve at full lift (> PORV; greater capacity)
    private static readonly double[] PzrSafetySet = { PzrSafety1Set, PzrSafety2Set, PzrSafety3Set };

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

    // Pressurizer saturated-volume model — primary pressure tracks the saturation pressure of _tpzr.
    private double _tpzr = ColdTemp;                 // pressurizer liquid temperature (°C) — sets primary pressure
    private double _prevLevel = NominalPzrLevel;     // previous-substep level, for surge rate d(level)/dt
    private double _pSpike;                          // transient adiabatic bubble-compression pressure (MPa)
    private bool _porvAuto;                          // automatic PORV currently lifted
    private readonly bool[] _pzrSafetyOpen = new bool[3]; // latched per-valve lift state of the 3 code safeties
    public double PressurizerLiquidTemp => _tpzr;    // °C (display)
    public double PressurizerHeaterDuty { get; private set; } // 0..1 effective heater duty (display)
    public bool PorvAutoOpen => _porvAuto;           // automatic relief valve currently cycling
    /// <summary>有幾多個穩壓器規範安全閥正在開啟（0–3）· How many of the 3 code safety valves are currently popped.</summary>
    public int PzrCodeSafetiesOpen { get { int n = 0; for (int i = 0; i < 3; i++) if (_pzrSafetyOpen[i]) n++; return n; } }
    public bool AnyPzrCodeSafetyOpen => PzrCodeSafetiesOpen > 0;
    /// <summary>規範安全閥每次起跳的上升沿事件 · Rising-edge event each time a code safety valve pops (for annunciation/audio).</summary>
    public event Action? PzrCodeSafetyLifted;

    // Poisons
    public double Iodine { get; private set; }   // I-135 concentration (normalized)
    public double Xenon { get; private set; }    // Xe-135 concentration (normalized)

    // Axial power distribution (two-node top/bottom split). Sign convention: top minus bottom; rods
    // enter from the top so insertion drives the offset NEGATIVE (bottom-peaked).
    public double TopPowerFraction    { get; private set; } = 0.5;  // top axial node power fraction (0..1)
    public double BottomPowerFraction { get; private set; } = 0.5;  // bottom axial node power fraction (0..1)
    public double AxialFluxDifferencePercent { get; private set; }  // ΔI = P_top - P_bot, % RTP (signed)
    public double AxialOffsetPercent         { get; private set; }  // AO = (P_top-P_bot)/(P_top+P_bot)*100
    // Constant Axial Offset Control (CAOC) Technical-Specification target band (LCO 3.2.1 surrogate). The
    // operator keeps ΔI inside a window centred on the all-rods-out equilibrium target (≈ −5 %RTP). The
    // window is tight (±5 %) at high power and widens trapezoidally as power is reduced (xenon control is
    // easier at low flux); the LCO is not enforced below ~50 % RTP. Outside the band you accrue "penalty
    // minutes" and must restore ΔI — modelled here as a simple out-of-band alarm.
    public double AfdTargetPercent => -5.0;            // CAOC target ΔI, %RTP (all-rods-out equilibrium)
    public double AfdBandHalfWidthPercent =>           // power-dependent half-width of the target window
        _power >= 0.90 ? 5.0
        : _power >= 0.50 ? 5.0 + (0.90 - _power) / 0.40 * 10.0   // widen 5 %→15 % as power 90 %→50 %
        : double.PositiveInfinity;                     // LCO inactive below 50 % RTP
    public bool AfdOutsideBand =>
        _power > 0.50 && Math.Abs(AxialFluxDifferencePercent - AfdTargetPercent) > AfdBandHalfWidthPercent;

    // Controls
    public double[] RodBankInsertion { get; } = { 100.0, 100.0, 100.0, 100.0 }; // % inserted per bank (A,B,C,D)
    public double BoronPpm { get; private set; } = NominalBoron;
    public double TargetBoronPpm { get; set; } = NominalBoron;
    public bool PressurizerHeater { get; set; }
    public bool PressurizerSpray { get; set; }
    public bool PzrAutoPressureControl { get; set; } = true; // automatic Westinghouse pressure-control program
    public double RcpFlowDemand { get; set; } = 0.0;     // 0..1 commanded pump flow
    public bool[] RcpRunning { get; } = { false, false, false, false };
    public double FeedwaterFlow { get; set; } = 0.0;     // 0..1 actual main-feedwater flow (auto-driven when FeedwaterAuto)
    // Three-element steam-generator level control (Westinghouse: level + steam flow + feed flow).
    public bool FeedwaterAuto { get; set; } = true;      // automatic feed-reg controller (off = operator owns FeedwaterFlow)
    public double SgLevelSetpoint { get; private set; } = 50.0; // % NR programmed setpoint (33% lo-pwr → 50% HFP)
    public double SteamFlow { get; private set; }        // 0..1 measured steam flow (feedforward + shrink/swell driver)
    public double FeedRegValve { get; private set; }     // 0..1 main feed-reg valve position (inner loop)
    public double IndicatedSgLevel { get; private set; } = 60.0; // % NR shown on the gauge = inventory + shrink/swell offset
    public bool ThreeElementActive => _threeElementActive; // true once steam/feed-flow signals are valid (>~18% power)
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
    // Per-loop flow contribution (0..RcpLoopShare each) — carries the hyperbolic coastdown of a tripped pump.
    private readonly double[] _rcpFlow = new double[4];
    public double PumpedFlowFraction { get; private set; }   // 0..1 forced (pumped) component of flow
    public double NaturalCircFraction { get; private set; }  // 0..1 buoyancy-thermosiphon floor this tick
    public bool RcpCoasting { get; private set; }            // a stopped pump is still carrying inertial flow
    public bool OnNaturalCirc { get; private set; }          // buoyancy floor is governing core flow (pumps gone)

    // Failure accumulation
    public double DamageAccumulation { get; private set; }  // 0..100+ ; >=100 => meltdown
    public bool IsScrammed { get; private set; }
    public bool MeltdownTriggered { get; private set; }
    public event Action? MeltdownOccurred;

    // ----------------------------------------------------------------- realism additions ----
    // Decay-heat: ALWAYS-present residual heat source from fission-product decay. Modelled to
    // ANSI/ANS-5.1 fidelity as a 23-group exponential sum for U-235 thermal fission-product decay
    // PLUS a 2-pole U-239→Np-239 actinide (capture-product) contribution. Each group charges while
    // at power and decays after a trip; the equilibrium yield a_i = (α_i/λ_i)/Q_t (Q_t = 200 MeV)
    // IS that group's fraction-of-rated power at infinite irradiation. Σ(a_i) = 0.065913 (fission
    // products) + 0.0038 (actinides) → a 6.97 % plateau. Charged-then-tripped this reproduces the
    // canonical ANS shutdown curve: 6.54 % @1 s, 5.13 % @10 s, 3.47 % @100 s, 1.48 % @1 h, 0.60 % @1 day.
    // λ_i in 1/s. (Verified against the standard's post-shutdown decay-heat bands.)
    private static readonly double[] DecayA = {
        1.469351e-04, 4.968694e-03, 6.222313e-03, 6.714175e-03, 8.236273e-03,
        9.513312e-03, 4.612211e-03, 3.338658e-03, 6.461999e-03, 5.174812e-03,
        2.958373e-03, 1.803488e-03, 1.260340e-03, 9.817596e-04, 1.396227e-03,
        1.082506e-03, 4.115313e-04, 9.338084e-06, 5.792887e-04, 5.069596e-07,
        7.187277e-06, 9.154065e-06, 2.382031e-05 };
    private static readonly double[] DecayLambda = {
        2.2138e+01, 5.1587e-01, 1.9594e-01, 1.0314e-01, 3.3656e-02,
        1.1681e-02, 3.5870e-03, 1.3930e-03, 6.2630e-04, 1.8906e-04,
        5.4988e-05, 2.0958e-05, 1.0010e-05, 2.5438e-06, 6.6361e-07,
        1.2290e-07, 2.7213e-08, 4.3714e-09, 7.5780e-10, 2.4786e-10,
        2.2384e-13, 2.4600e-14, 1.5699e-14 };
    // Actinide chain (U-239 t½≈23.45 min, Np-239 t½≈2.356 d) — adds ~0.38 % at equilibrium.
    private static readonly double[] ActinideA = { 2.5e-3, 1.3e-3 };
    private static readonly double[] ActinideLambda = { 4.902e-4, 3.448e-6 }; // 1/s
    private readonly double[] _decayGroup = new double[23];
    private readonly double[] _actinide = new double[2];
    public double DecayHeatFraction { get; private set; }

    // Axial flux difference (ΔI) / axial offset / CAOC. A lumped two-node (top/bottom) split whose
    // imbalance is driven by control-rod insertion (rods bite the top half → bottom-peaked → ΔI<0)
    // and by a top-minus-bottom Xe-135 difference (axial xenon redistribution). The signed split is
    // run through a stable first-order lag, then mapped to ΔI in %RTP for the OTΔT f₁(ΔI) penalty and
    // the CAOC operating-band gauge. All coefficients are representative Westinghouse 4-loop / COLR
    // values (plant-specific in a real cycle).
    private double _axialSplit;          // signed top-minus-bottom share, -1..+1 (lagged)
    private double _axialSplitTarget;    // instantaneous target for _axialSplit
    private double _iodineTop, _iodineBot;   // per-node I-135 (mean == Iodine)
    private double _xenonTop,  _xenonBot;    // per-node Xe-135 (mean == Xenon)
    private const double AxialTau         = 4.0;   // s, lag on the prompt rod-driven shape response
    private const double AxialRodWeight   = 0.55;  // unit-imbalance a fully-inserted lead bank D produces
    private const double AxialXenonWeight = 0.30;  // share from top-minus-bottom xenon difference
    private const double AfdFullScalePct  = 30.0;  // maps unit split → ΔI %RTP at 100 % power
    private const double AxialEquilBias   = -0.02; // small negative bias → equilibrium AO ≈ -2 % (moderator/burnup)
    private const double DBandHfpFrac     = 0.05;  // bank D ~5 % inserted at steady HFP (lead-bank bite)

    // Sub-cooling margin (°C): SatTemp(P) - Thot. < 0 means saturation / void onset.
    public double SubcoolingMarginC { get; private set; }

    // Nuclear instrumentation (NIS): three overlapping ranges, exactly like a Westinghouse 4-loop NIS.
    //   • Source Range (SR)  — BF3 proportional counters, count rate in cps, ~6 decades (1..1e6).
    //   • Intermediate Range (IR) — compensated ion chambers, DC current in amps, ~8 decades (1e-11..1e-3),
    //                               also the source of Startup Rate (SUR/DPM) and the P-6 permissive.
    //   • Power Range (PR)  — uncompensated ion chambers, LINEAR % rated power (0..120 %).
    // All three are driven from the single dimensionless neutron power fraction by linear-then-Clamp maps
    // (no Log/Pow on the hot path), so the readings stay finite across the full 12-decade span and overlap
    // by ≥1 decade between adjacent ranges, as the real instruments do.
    public double SourceRangeCps { get; private set; }
    public double OneOverM { get; private set; } = 1.0;
    public double StartupRateDpm { get; private set; } // decades per minute (SUR), sourced from IR
    public double IntermediateRangeAmps { get; private set; } = IrBottomAmps; // CIC current (A)
    public double IntermediateRangeDecades { get; private set; } // 0..8, decades above IR detector floor
    public double IntermediateRangePercent { get; private set; } // log power % indicated on the IR
    public double PowerRangePercent { get; private set; } // linear % rated power on the PR UICs
    public bool SourceRangeEnergized { get; private set; } = true; // SR HV present (cut by P-6 / P-10)
    private const double SourceBaselineCps = 100.0;
    private const double SourceRangeMaxCps = 1.0e6;      // BF3 counter saturation ceiling
    private const double IrFullScaleAmps = 1.0e-3;       // IR current at 100 % rated power
    private const double IrBottomAmps = 1.0e-11;         // IR detector floor (de-energized / off-scale low)
    private const double P6CurrentThresholdA = 1.0e-10;  // IR current that asserts the P-6 permissive
    private bool _p6Latched;                             // P-6 latch (IR on-scale), 10 % deadband

    // Turbine reference speed: 4-pole 60 Hz nuclear set runs at 1800 rpm.
    public const double SyncRpm = 1800.0;
    public double TurbineRatedRpm => SyncRpm;
    private const double OverspeedTripRpm = 1980.0;          // 110 % — latching mechanical/electronic trip

    // --- EHC (Electro-Hydraulic Control) turbine constants ---
    // 調速器電液控制（EHC）汽輪機常數 · governor-valve load control, droop, overspeed protection.
    private const double GvRateUpPerMin   = 5.0;             // load-ref ramp UP,   %rated/min (routine 3–5 %/min band)
    private const double GvRateDownPerMin = 20.0;           // load-ref ramp DOWN, %rated/min (faster asymmetric runback)
    private const double GvActuatorTau    = 0.5;            // s, first-order governor-valve servo lag
    private const double DroopFrac        = 0.05;          // 5 % governor droop (IEEE steady-state regulation)
    private const double FspGain          = 1.0;          // FirstStagePressure = FspGain * GV steam flow (0..1)
    private const double SpeedRampRpmPerS = 60.0;        // pre-sync acceleration rate (~30 s roll to 1800 rpm)
    private const double OpcSetpointRpm   = SyncRpm * 1.03; // 1854 rpm — OPC / power-load-unbalance fast-close (non-latching)
    private const double SteamPressDrawK  = 0.6;           // SG-pressure relief coupling per unit GV steam draw

    // --- EHC state ---
    public double LoadReference      { get; private set; } // 0..1 rate-limited internal load demand the EHC tracks
    public double GovernorValve      { get; private set; } // 0..1 ACTUAL governor-valve position (single writer: UpdateSecondary)
    public double FirstStagePressure { get; private set; } // 0..1 calibrated load signal = k * GV steam flow (impulse chamber)
    public double TurbineSpeedError  { get; private set; } = SyncRpm; // rpm, (SyncRpm − TurbineRPM) — droop + display
    public bool   TurbineTripped     { get; private set; } // latched stop-valve trip (overspeed / manual)
    private double _gvCmd;                                  // commanded GV position (pre-actuator-lag)

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

    // Class 1E plant electrical distribution (offsite power, EDGs + load sequencer, 125 VDC battery,
    // and the RCP/ECCS/AFW availability gates those buses drive). See Services/ReactorElectrical.cs.
    private readonly ReactorElectrical _elec = new();
    public ReactorElectrical Electrical => _elec;
    // Three-element feedwater controller internal state.
    private double _iLevel;           // master level-PI integrator (flow-fraction units)
    private double _steamFlowSlow;    // slow-lagged steam flow → shrink/swell is the high-pass residual
    private bool _threeElementActive; // single-element (level only) vs three-element latch w/ hysteresis
    private bool _fwAutoWasOn;        // bumpless-transfer latch for AUTO engagement

    // SGTR — steam-generator tube rupture. A primary→secondary leak driven by the primary-to-
    // secondary pressure difference: leaked RCS water floods the affected SG and carries activity
    // (N-16, fission products) into the secondary, which the SG-blowdown / air-ejector radiation
    // monitors see. Depressurising the RCS toward SG pressure (the E-3 procedure) collapses the
    // dP and stops the leak; that self-limiting behaviour is what makes it physical, not scripted.
    private double _sgtrSeverity;                                    // 0..1 rupture severity latch (0 = tubes intact)
    public double SgtrLeakRate { get; private set; }                // dP-driven leak magnitude (display/diagnostic)
    public double CoolantActivity { get; private set; } = 0.02;     // normalized RCS specific activity (1.0 = tech-spec limit)
    public double SecondaryActivity { get; private set; }           // normalized secondary accumulator; 1.0 == monitor alarm setpoint
    public double SecondaryRadiation => SecondaryActivity * 100.0;  // SG-blowdown / air-ejector monitor reading (µSv/h-ish)
    public double AtmosphericRelease { get; private set; }          // integrated activity vented to atmosphere (permanent)
    public bool SgReliefLifted { get; private set; }                // affected-SG safety/relief valve venting contaminated steam
    public bool SgtrIsolated { get; set; }                          // operator latch: affected SG isolated (MSIV + feedwater)
    public double PrimaryDeficitPct { get; private set; }           // integrated RCS inventory deficit (%) — biases pzr level/pressure targets
    private const double SgtrLeakScale = 0.11;   // leak gain (1/MPa·severity) — dP-driven choked-flow surrogate
    private const double SgtrActivityGain = 2.5; // primary→secondary activity transport gain

    // MSLB — main steam line break. The inverse of SGTR: a rupture downstream of the SG vents the
    // secondary to atmosphere, crashing steam pressure. The saturation temperature collapses with it, so
    // the SG pulls heat from the primary far faster than the turbine ever did — the RCS OVERCOOLS. With a
    // strongly-negative end-of-cycle moderator coefficient, that cooldown inserts POSITIVE reactivity and
    // can drive a tripped core back toward criticality (the design-basis "return to power"). Nothing here
    // is scripted: the cooldown falls out of the existing sgRemoval/SecondarySatTemp coupling and the
    // return-to-power out of the existing moderator feedback. We only model the break boundary condition,
    // the low-steamline-pressure SI actuation, MSIV isolation, and the borated safety injection that wins.
    private double _mslbSeverity;                                   // 0..1 break severity latch (0 = intact)
    public bool MslbIsolated { get; set; }                          // operator MSIV button + auto-close on SI
    public bool SiActuated { get; private set; }                    // latched on Lo-Steamline-Pressure SI coincidence
    public double MslbBreakFlow { get; private set; }               // normalized break steam flow (display/diagnostic)
    private int _mslbSiFnIndex = -1;                                // cached index of the Lo-Steamline-Press SI function
    private const double MslbSiSetpointMpa = 4.14;                  // ≈600 psia Westinghouse Lo-Steamline-Press SI setpoint
    private const double MslbSiBoronPpm = 2000.0;                   // borated-SI target concentration (shutdown margin)

    // ---- Containment · 安全殼 -----------------------------------------------------------------
    // Lumped 0-D containment-atmosphere node for a large-dry (4-loop Westinghouse class) building.
    // A break INSIDE containment — an MSLB (the bounding temperature case) or a LOCA — dumps mass and
    // energy into the free volume, so pressure rises toward a scenario peak with a short time constant.
    // Passive steel/concrete heat sinks always condense steam (slow); fan coolers and — decisively —
    // containment spray accelerate the removal. Three containment-pressure ESFAS bistables actuate, in
    // order: Hi-1 → Safety Injection + Containment Isolation Phase A + reactor trip; Hi-2 → Main Steam
    // Line Isolation; Hi-3 → Containment Spray + Phase B. SGTR and out-of-containment secondary breaks
    // deliberately do NOT feed this node (they bypass containment), so spray/isolation stay quiescent.
    public double ContainmentPressureKpa { get; private set; }       // gauge kPa (0 = atmospheric)
    public double ContainmentPressurePsig => ContainmentPressureKpa / 6.895;
    public double ContainmentTempC { get; private set; } = ContainmentAmbientC;
    public bool ContainmentSprayActive { get; private set; }
    public bool ContainmentIsolationPhaseA { get; private set; }     // Hi-1: non-essential penetrations isolated
    public bool ContainmentIsolationPhaseB { get; private set; }     // Hi-3: full isolation (incl. CCW) on spray
    public bool ContainmentFanCoolers { get; private set; }
    private bool _ctmtHi1, _ctmtHi2, _ctmtHi3;                       // pressure-bistable latches (anti-chatter)
    private double _spraySetupTimer;                                 // spray pump-start/valve-stroke delay
    private const double ContainmentAmbientC = 49.0;        // ~120 °F normal containment atmosphere
    private const double ContainmentPeakC = 125.0;          // ~257 °F bounding (MSLB superheat) peak
    private const double ContainmentSprayTempC = 35.0;      // spray-quench floor
    private const double CtmtPeakLocaKpa = 415.0;           // ~60 psig blowdown peak for a large-break LOCA
    private const double CtmtPeakMslbKpa = 350.0;           // ~51 psig peak for an in-containment MSLB
    private const double CtmtTauPressUp = 8.0;              // s — pressurization time constant
    private const double CtmtTauPassive = 300.0;            // s — passive steel/concrete heat sinks
    private const double CtmtTauFan = 120.0;                // s — fan-cooler condensation
    private const double CtmtTauSpray = 30.0;               // s — containment-spray condensation (dominant)
    public const double CtmtDesignPsig = 47.0;              // ~324 kPa-g design pressure (display reference)
    // Westinghouse containment-pressure ESFAS bistable setpoints (gauge kPa).
    private const double CtmtHi1Kpa = 28.0;   // ~4.0  psig — SI + Containment Isolation Phase A + reactor trip
    private const double CtmtHi2Kpa = 71.0;   // ~10.3 psig — Main Steam Line Isolation
    private const double CtmtHi3Kpa = 186.0;  // ~27   psig — Containment Spray + Phase B isolation
    private const double CtmtHystKpa = 7.0;   // ~1 psi reset deadband (anti-chatter)
    private const double SpraySetupSeconds = 35.0; // spray pump-start + valve-stroke actuation delay

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
        _iodineTop = _iodineBot = _xenonTop = _xenonBot = 0;
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
        // f₁(ΔI): the axial-flux-difference penalty on the OTΔT setpoint. Zero inside the protection
        // deadband, then reduces the allowable ΔT once ΔI leaves the band — the anti-DNB margin shrinks
        // as the flux skews axially. The real Westinghouse f₁ is ASYMMETRIC: a wide negative leg (bottom-
        // peaked is tolerated — boiling is at the cooler core inlet) and a tight, steeper positive leg
        // (top-peaked drives the DNB-limiting hot spot toward the saturated core outlet). Representative
        // legacy 4-loop breakpoints/slopes (plant COLR varies): deadband −29 %ΔI … +5 %ΔI; negative slope
        // 1.5 %ΔT₀ per %ΔI, positive slope 2.5 %ΔT₀ per %ΔI. Capped so a garbage ΔI cannot zero the trip.
        const double AfdNegEdge  = -29.0;    // %RTP ΔI — below this the bottom-skew penalty starts
        const double AfdPosEdge  =  +5.0;    // %RTP ΔI — above this the top-skew penalty starts
        const double AfdNegSlope = 0.015;    // fractional ΔT₀ reduction per %ΔI on the negative leg
        const double AfdPosSlope = 0.025;    // fractional ΔT₀ reduction per %ΔI on the positive leg
        double F1DeltaI()
        {
            double di = AxialFluxDifferencePercent;             // signed ΔI, %RTP
            double pen =
                di < AfdNegEdge ? AfdNegSlope * (AfdNegEdge - di) :  // more negative → larger penalty
                di > AfdPosEdge ? AfdPosSlope * (di - AfdPosEdge) :  // more positive → larger penalty
                0.0;                                                 // inside the deadband: no penalty
            return Math.Clamp(pen, 0.0, 0.40);
        }
        double OtDeltaTAllow() => FullPowerDeltaTF *
            (1.14 - 0.0166 * (TavgF() - 588.4) + 0.00091 * (Ppsig() - 2235.0) - F1DeltaI());
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
        // 10 — Steam Generator Level Low-Low: 17%. Always active (initiates AFW). 2/3. Evaluated on the
        //      INDICATED narrow-range level (inventory + shrink/swell) — exactly what the transmitters see,
        //      so a load-rejection shrink can challenge the setpoint just as it does in the real plant.
        _rps.Functions.Add(new RpsFunction("SG Level Lo-Lo", "蒸發器水位－低低",
            RpsTripDir.Low, 17.0, () => IndicatedSgLevel, channelCount: 3));
        // 11 — Source Range High Flux at Shutdown: 1e5 cps. Low-power startup protection; armed only while
        //      the SR is energized — blocked once P-6 (IR on-scale) or P-10 (>10% power) is reached. 2/2.
        _rps.Functions.Add(new RpsFunction("Source Range Flux Hi", "起動範圍中子通量－高",
            RpsTripDir.High, 1.0e5, () => SourceRangeCps,
            permissive: () => !_rps.P6 && !_rps.P10, channelCount: 2));
        // 12 — Intermediate Range High Flux: 25% rated power. Armed once the IR is on-scale (P-6) and
        //      blocked above P-10 — the overlap-band startup trip between source and power range. 2/2.
        _rps.Functions.Add(new RpsFunction("Intermediate Range Flux Hi", "中間範圍中子通量－高",
            RpsTripDir.High, 0.25, () => NeutronPowerFraction,
            permissive: () => _rps.P6 && !_rps.P10, channelCount: 2));
        // 13 — Low Steamline Pressure → Safety Injection (ESFAS). ~600 psia (4.14 MPa). 2/4. A steam-line
        //      break crashes secondary pressure past this setpoint; the coincidence trips the reactor AND
        //      (in UpdateProtection) actuates SI: automatic MSIV closure + borated injection. Blocked by a
        //      P-11-style permissive (RCS not at operating pressure) so the naturally-low secondary
        //      pressure during cold shutdown / heat-up does not spuriously actuate SI — exactly the manual
        //      low-pressure SI block the operators insert below P-11 for cooldown.
        Func<bool> rcsAtPressure = () => PrimaryPressure >= 10.0; // ≈P-11 (~1915 psig) arming
        _mslbSiFnIndex = _rps.Functions.Count;
        _rps.Functions.Add(new RpsFunction("Steamline Pressure Lo SI", "蒸汽管壓力－低（安全注入）",
            RpsTripDir.Low, MslbSiSetpointMpa, () => SteamPressure, permissive: rcsAtPressure));
    }

    // ----------------------------------------------------------------- controls ----
    public void SetRodBank(int bank, double percentInserted)
    {
        if (bank < 0 || bank >= RodBankInsertion.Length) return;
        RodBankInsertion[bank] = Math.Clamp(percentInserted, 0, 100);
    }

    // ---- rod-control instrumentation / sequencing (Westinghouse 4-loop) ----
    private double _rodDemandCounter; // 0..528, drives the overlap sequence in AUTO

    // S-shaped normalized integral rod-worth curve. S(0)=0, S(1)=1, S(0.5)=0.5.
    private static double RodS(double x)
    {
        if (x <= 0.0) return 0.0;
        if (x >= 1.0) return 1.0;
        return x - Math.Sin(2.0 * Math.PI * x) / (2.0 * Math.PI);
    }

    // Step position shown on the rod-position indication: 0 = fully in, 228 = fully out.
    public int RodStepsWithdrawn(int bank)
    {
        if (bank < 0 || bank >= RodBankInsertion.Length) return 0;
        return (int)Math.Round((1.0 - RodBankInsertion[bank] / 100.0) * RodStepsPerBank);
    }

    public double RodDemandCounter => _rodDemandCounter;          // 0..528 group-demand counter (AUTO)

    // Control-bank insertion limit (LCO 3.1.6): the lead bank (D) must stay withdrawn ABOVE a
    // power-dependent minimum to preserve shutdown margin / ejected-rod worth. Low = 4·%RTP − 40 steps.
    public double RilLowLimitSteps(double powerFrac)
    {
        double pct = Math.Clamp(powerFrac, 0, 1) * 100.0;
        return Math.Clamp(4.0 * pct - 40.0, 0, RodStepsPerBank);
    }
    public double RilLowLowLimitSteps(double powerFrac) =>
        Math.Clamp(RilLowLimitSteps(powerFrac) - RilLowLowBias, 0, RodStepsPerBank);

    // Lead bank D = index 3. Below the limit line = inserted too deep for the current power.
    public bool RilLowAlarm    => !IsScrammed && RodStepsWithdrawn(3) < RilLowLimitSteps(_power);
    public bool RilLowLowAlarm => !IsScrammed && RodStepsWithdrawn(3) < RilLowLowLimitSteps(_power);

    // Rod-deviation: in AUTO, any bank whose actual step position departs from its overlap-demanded
    // position by more than the alignment band flags a stuck/dropped rod (e.g. ATWS).
    public bool RodDeviationAlarm
    {
        get
        {
            if (!AutoRodControl) return false; // manual: operator owns positioning
            for (int k = 0; k < RodBankInsertion.Length; k++)
            {
                int demanded = (int)Math.Round(Math.Clamp(_rodDemandCounter - k * RodStride, 0, RodStepsPerBank));
                if (Math.Abs(RodStepsWithdrawn(k) - demanded) > RodDeviationBandSteps) return true;
            }
            return false;
        }
    }

    // Reconstruct the demand counter from the present bank stack (used when AUTO is engaged).
    private double InferRodDemandFromBanks()
    {
        double c = 0;
        for (int k = 0; k < RodBankInsertion.Length; k++)
        {
            double w = (1.0 - RodBankInsertion[k] / 100.0) * RodStepsPerBank;
            if (w <= 0.0) break;
            c = k * RodStride + w;        // last contributing bank sets the counter
            if (w < RodStepsPerBank) break; // partial bank = the lead bank
        }
        return Math.Clamp(c, 0, RodTotalSpan);
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

    /// <summary>手動汽輪機跳脫 · Manually trip the turbine: latch the stop valves shut.</summary>
    public void TripTurbine() => TurbineTripped = true;

    /// <summary>
    /// 重置汽輪機跳脫閂鎖 · Reset a latched turbine (overspeed/manual) trip so the stop valves can
    /// reopen — permitted only once the shaft has coasted below ~90 % speed, as on a real EHC.
    /// </summary>
    public void ResetTurbineTrip()
    {
        if (TurbineRPM <= SyncRpm * 0.90) TurbineTripped = false;
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
        _sgtrSeverity = 0;
        SgtrIsolated = false;
        PrimaryDeficitPct = 0;
        _mslbSeverity = 0;
        MslbIsolated = false;
        SiActuated = false;
        MslbBreakFlow = 0;
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
            case ReactorScenario.SgTubeRupture:
                _sgtrSeverity = 0.45;  // ~one ruptured tube; a minutes-scale transient (0.3 gentle … 0.8 multi-tube)
                EccsArmed = true;      // armed; ECCS auto-injects on low pressurizer pressure as the RCS drains
                break;
            case ReactorScenario.MainSteamLineBreak:
                _mslbSeverity = 0.7;   // large un-isolable break (0.3 small … 1.0 full double-ended rupture)
                MslbIsolated = false;
                EccsArmed = true;      // SI armed; the borated-injection path is driven off SiActuated, not EccsInjecting
                break;
        }
    }

    // Two-parameter Clausius–Clapeyron saturation-line fit: ln(P[MPa]) = SatA − SatB/T[K].
    // Anchored to IAPWS at (344.8 °C, 15.5 MPa) and (285 °C, 6.9 MPa); reproduces 2.5 MPa @ 224 °C
    // and 0.1 MPa @ ~99 °C to within a few percent across the whole 80–360 °C operating band.
    // Analytically invertible, so SatTempAt and SatPressAt are exact inverses — that keeps the
    // sub-cooling margin (SatTempAt(P) − Thot) thermodynamically self-consistent and lets primary
    // pressure be the saturation pressure of the pressurizer liquid surface (see StepThermal).
    private const double SatA = 10.2958;
    private const double SatB = 4668.6;

    /// <summary>飽和溫度估算 · Saturation temperature of water (°C) at a given pressure (MPa).</summary>
    private static double SatTempAt(double mpa)
    {
        mpa = Math.Clamp(mpa, 0.01, 22.0);
        return SatB / (SatA - Math.Log(mpa)) - 273.15;
    }

    /// <summary>飽和壓力估算 · Saturation pressure of water (MPa) at a given temperature (°C).
    /// Exact inverse of <see cref="SatTempAt"/>.</summary>
    private static double SatPressAt(double tC)
    {
        tC = Math.Clamp(tC, 80.0, 373.0);            // ≥80 °C keeps the fit real; below the 373.9 °C critical point
        return Math.Exp(SatA - SatB / (tC + 273.15));
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
        _tpzr = ColdTemp; _prevLevel = NominalPzrLevel; _pSpike = 0; _porvAuto = false; PressurizerHeaterDuty = 0;
        Array.Clear(_pzrSafetyOpen, 0, 3); // re-seat all code safety valves on reset
        SteamPressure = 0.5; SteamGenLevel = 60; ElectricPowerMW = 0; TurbineRPM = 0;
        LoadReference = 0; GovernorValve = 0; _gvCmd = 0;
        FirstStagePressure = 0; TurbineSpeedError = SyncRpm; TurbineTripped = false;
        IndicatedSgLevel = 60; SteamFlow = 0; FeedRegValve = 0; SgLevelSetpoint = 50;
        FeedwaterAuto = true; _iLevel = 0; _steamFlowSlow = 0; _threeElementActive = false; _fwAutoWasOn = false;
        Iodine = 0; Xenon = 0;
        _iodineTop = _iodineBot = _xenonTop = _xenonBot = 0;
        _axialSplit = _axialSplitTarget = 0;
        TopPowerFraction = BottomPowerFraction = 0.5;
        AxialOffsetPercent = 0; AxialFluxDifferencePercent = 0;
        for (int i = 0; i < RodBankInsertion.Length; i++) RodBankInsertion[i] = 100.0;
        for (int i = 0; i < RcpRunning.Length; i++) RcpRunning[i] = false;
        Array.Clear(_rcpFlow);
        PumpedFlowFraction = 0; NaturalCircFraction = 0; RcpCoasting = false; OnNaturalCirc = false;
        BoronPpm = NominalBoron; TargetBoronPpm = NominalBoron;
        PressurizerHeater = false; PressurizerSpray = false; RcpFlowDemand = 0;
        FeedwaterFlow = 0; TurbineLoadSetpoint = 0; GeneratorBreakerClosed = false;
        ReliefValveOpen = false; EccsArmed = false; EccsInjecting = false;
        CoolantFlowFraction = 0; DamageAccumulation = 0;
        IsScrammed = false; MeltdownTriggered = false; AutoRodControl = false;
        _rps.ClearLatch(); LastTripFunctionEn = ""; LastTripFunctionZh = ""; _powerRate = 0;
        Mode = ReactorMode.Shutdown;
        for (int i = 0; i < _alarms.Length; i++) _alarms[i] = false;
        Array.Clear(_decayGroup); Array.Clear(_actinide);
        DecayHeatFraction = 0; SubcoolingMarginC = 0; SourceRangeCps = SourceBaselineCps;
        OneOverM = 1.0; StartupRateDpm = 0; BurnupMwdPerTonne = 0;
        IntermediateRangeAmps = IrBottomAmps; IntermediateRangeDecades = 0; IntermediateRangePercent = 0;
        PowerRangePercent = 0; SourceRangeEnergized = true; _p6Latched = false;
        ActiveScenario = ReactorScenario.Normal; _breakArea = 0; _rodsFailToInsert = false;
        AccumulatorInjecting = false; AuxFeedwaterRunning = false; _lofwTimer = 0;
        _elec.Reset();
        _sgtrSeverity = 0; SgtrLeakRate = 0; CoolantActivity = 0.02;
        SecondaryActivity = 0; AtmosphericRelease = 0; SgReliefLifted = false; SgtrIsolated = false;
        _mslbSeverity = 0; MslbIsolated = false; SiActuated = false; MslbBreakFlow = 0;
        PrimaryDeficitPct = 0;
        ContainmentPressureKpa = 0; ContainmentTempC = ContainmentAmbientC;
        ContainmentSprayActive = false; ContainmentIsolationPhaseA = false;
        ContainmentIsolationPhaseB = false; ContainmentFanCoolers = false;
        _ctmtHi1 = _ctmtHi2 = _ctmtHi3 = false; _spraySetupTimer = 0;
        StatusEn = "Cold shutdown"; StatusZh = "冷停機";
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

        // --- Class 1E electrical FIRST: it gates RCP availability (read by UpdateFlow), motor-driven
        //     ECCS/SI (read by UpdateProtection) and motor- vs turbine-driven AFW (read by UpdateScenarios). ---
        UpdateElectrical(dt);

        // --- slow process controls that don't need sub-stepping ---
        UpdateBoron(dt);
        UpdateFlow(dt);
        UpdateDecayHeat(dt);
        UpdateScenarios(dt);
        UpdateContainment(dt);

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

        // --- three-element feedwater regulating control (runs before the secondary so the valve
        //     demand and shrink/swell indication are current when level integrates this tick) ---
        UpdateFeedwaterControl(dt);

        // --- secondary plant + turbine ---
        UpdateSecondary(dt);

        // --- auto rod control ---
        if (AutoRodControl && !IsScrammed) UpdateAutoRods(dt);
        else _autoRodWasOn = false; // reset bumpless-transfer latch when AUTO is dropped

        // --- nuclear instrumentation BEFORE protection: the SR/IR/PR readings and the IR-derived P-6
        //     permissive must be current when the protection system evaluates the NIS trips this tick. ---
        UpdateNis(dt);
        UpdateProtection(dt);
        UpdateAlarms();
        UpdateStatus();
    }

    private void UpdateDecayHeat(double dt)
    {
        // ANS-5.1 exponential-group model. Each group holds H_i (fraction of rated) and obeys
        //   dH_i/dt = a_i·λ_i·P − λ_i·H_i   (production ∝ instantaneous fission power P, then decay).
        // We advance with the EXACT discrete solution over a piecewise-constant-P step:
        //   H_i ← H_i·e^(−λ_i·dt) + a_iP·(1 − e^(−λ_i·dt))
        // which is unconditionally stable and non-negative for ANY dt (the λ span ~15 decades is
        // stiff, but this analytic recurrence needs no sub-stepping, unlike the kinetics loop).
        // OneMinusExp preserves precision for the slow groups where λ·dt → 0.
        double p = Math.Max(_power, 0.0); // fission fraction of rated drives fission-product production
        double frac = 0.0;
        for (int i = 0; i < DecayA.Length; i++)
        {
            double oneMinusE = OneMinusExp(DecayLambda[i] * dt); // 1 − e^(−λ·dt), accurate near 0
            _decayGroup[i] = _decayGroup[i] * (1.0 - oneMinusE) + DecayA[i] * p * oneMinusE;
            frac += _decayGroup[i];
        }
        for (int i = 0; i < ActinideA.Length; i++)
        {
            double oneMinusE = OneMinusExp(ActinideLambda[i] * dt);
            _actinide[i] = _actinide[i] * (1.0 - oneMinusE) + ActinideA[i] * p * oneMinusE;
            frac += _actinide[i];
        }
        // Each H_i is a convex combination of non-negative terms, so it stays ≥ 0; clamp the SUM only.
        DecayHeatFraction = Math.Clamp(frac, 0.0, 0.12);

        // Burnup accrual + slow MTC drift across the cycle.
        BurnupMwdPerTonne += ThermalPowerMW * dt / 86400.0 / CoreTonnesU; // MWd/tonne
    }

    /// <summary>1 − e^(−x) for x ≥ 0, computed without catastrophic cancellation for tiny x
    /// (Taylor series below the threshold) — the .NET-portable stand-in for expm1.</summary>
    private static double OneMinusExp(double x)
    {
        if (x < 1e-5) return x * (1.0 - 0.5 * x * (1.0 - x / 3.0)); // x − x²/2 + x³/6
        return 1.0 - Math.Exp(-x);
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

        // SGTR: primary→secondary leak, driven by the primary-to-secondary pressure difference.
        // Drains the primary like a slow LOCA, but the inventory goes INTO the affected steam
        // generator (handled in UpdateSecondary) and carries activity across with it. The leak is
        // self-limiting: as the operator depressurises the RCS toward SG pressure, dP → 0 → leak → 0.
        if (_sgtrSeverity > 0)
        {
            double dP = Math.Max(0.0, PrimaryPressure - SteamPressure);   // MPa
            SgtrLeakRate = SgtrLeakScale * _sgtrSeverity * dP;

            // Lost RCS mass accrues as an inventory deficit that biases the pressurizer level/pressure
            // targets down in StepThermal (see lvlTarget/pTarget). Safety injection (ECCS) makes it up.
            PrimaryDeficitPct += 1.5 * SgtrLeakRate * dt;
            EccsArmed = true;

            // Failed fuel ⇒ dirtier coolant ⇒ a worse radiological consequence for the same leak.
            CoolantActivity = 0.02 + 0.02 * Math.Min(DamageAccumulation, 50.0);

            // Secondary radiation accumulator: rises with leak × activity, slow washout (τ ≈ 1800 s).
            SecondaryActivity += SgtrActivityGain * SgtrLeakRate * CoolantActivity * dt;
            SecondaryActivity -= SecondaryActivity / 1800.0 * dt;
            if (SecondaryActivity < 0) SecondaryActivity = 0;
        }
        else
        {
            SgtrLeakRate = 0;
            SgReliefLifted = false;
        }

        // MSLB: the faulted SG vents to atmosphere through the broken steam line. Model the secondary as
        // a vessel blowing down toward atmospheric through a choked orifice — mass flux ∝ √P, so the
        // pressure relaxes (first-order, time-constant ∝ 1/severity) toward a near-atmospheric floor.
        // Crashing SteamPressure drops SecondarySatTemp(), which widens the existing sgRemoval term and
        // overcools the primary EMERGENTLY (plus a small direct break-energy term in StepThermal). The
        // term gates off the instant the MSIVs close (operator button or auto-SI) so UpdateSecondary's
        // lag owns the recovery — exactly the single-writer discipline the SGTR level comment relies on.
        if (_mslbSeverity > 0 && !MslbIsolated)
        {
            double wBreak = Math.Clamp(
                _mslbSeverity * Math.Sqrt(Math.Max(SteamPressure, 0.0)) / Math.Sqrt(NominalSteamPressure),
                0.0, 1.0);                                   // choked-flow surrogate: mass flux ∝ √P
            MslbBreakFlow = wBreak;
            const double pBreakFloor = 0.2;                  // MPa (~29 psia) — never relax to 0
            double tauBlow = 8.0 / _mslbSeverity;            // s; ≈11 s at severity 0.7, faster for bigger breaks
            SteamPressure += (pBreakFloor - SteamPressure) * Math.Min(1.0, dt / tauBlow);
            SteamPressure = Math.Max(SteamPressure, 0.3);    // stay within UpdateSecondary's [0.3, 8.5] band
            AtmosphericRelease += 0.1 * wBreak * dt;         // steam vented to atmosphere (mostly clean for an MSLB)
        }
        else
        {
            MslbBreakFlow = 0;
        }

        // Safety injection / charging make up lost inventory; otherwise the deficit holds (latched leak).
        if (EccsInjecting) PrimaryDeficitPct -= 2.5 * dt;
        PrimaryDeficitPct = Math.Clamp(PrimaryDeficitPct, 0.0, 40.0);

        // Passive accumulators discharge once primary pressure falls below ~4.5 MPa.
        AccumulatorInjecting = PrimaryPressure < 4.5 && (FuelTemp > 200 || _breakArea > 0);
        if (AccumulatorInjecting)
        {
            FuelTemp -= 25 * dt;
            Tcold -= 4 * dt; Thot -= 4 * dt;
            PressurizerLevel = Math.Min(100, PressurizerLevel + 4 * dt);
        }

        // Loss of feedwater → aux feedwater auto-starts after 60 s. AFW now respects the electrical state:
        // the motor-driven pumps need a live AC bus, while the turbine-driven (TDAFW) pump runs on SG steam
        // with only 125 VDC for its governor — so it survives a station blackout and is the SBO heat-removal
        // path until the battery depletes or steam pressure falls below the turbine's motive-steam band.
        bool feedLost = FeedwaterFlow < 0.02 && (_power > 0.05 || Thot > 150);
        if (feedLost) _lofwTimer += dt; else _lofwTimer = 0;
        AuxFeedwaterRunning = _lofwTimer > 60.0 && (_elec.MdafwAvailable || _elec.TdafwAvailable);
        if (AuxFeedwaterRunning) FeedwaterFlow = Math.Max(FeedwaterFlow, 0.15); // small AFW flow
    }

    /// <summary>
    /// 安全殼壓力響應與 Hi-1/Hi-2/Hi-3 ESFAS · Lumped containment-atmosphere pressure/temperature
    /// response plus the three containment-pressure ESFAS actuations. Unconditionally stable: every
    /// state relaxes by a Math.Min(1, dt/τ) factor toward a target, so it cannot overshoot at any dt.
    /// </summary>
    private void UpdateContainment(double dt)
    {
        // --- mass/energy source into the building ---------------------------------------------------
        // MSLB inside containment drives off the normalized break steam flow; the LOCA blowdown drives
        // off break area but FADES as the RCS depressurizes (the energy is the hot pressurized primary
        // flashing — once the vessel has emptied, the source is just decay-heat boil-off). SGTR and
        // out-of-containment breaks are intentionally absent: they bypass the containment boundary.
        double locaDrive = _breakArea * Math.Clamp(PrimaryPressure / 6.0, 0.0, 1.0);
        double pTarget = Math.Max(MslbBreakFlow * CtmtPeakMslbKpa, locaDrive * CtmtPeakLocaKpa);

        // --- pressurize fast, depressurize via parallel heat-removal conductances -------------------
        double tau;
        if (pTarget > ContainmentPressureKpa + 1.0)
        {
            tau = CtmtTauPressUp;                         // a break is adding energy: pressure rising
        }
        else
        {
            double g = 1.0 / CtmtTauPassive;              // passive heat sinks always condensing
            if (ContainmentFanCoolers) g += 1.0 / CtmtTauFan;
            if (ContainmentSprayActive) g += 1.0 / CtmtTauSpray;
            tau = 1.0 / g;
        }
        double fP = Math.Min(1.0, dt / tau);              // stable relaxation factor
        ContainmentPressureKpa += (pTarget - ContainmentPressureKpa) * fP;
        if (ContainmentPressureKpa < 0) ContainmentPressureKpa = 0;

        // Atmosphere temperature loosely tracks pressure; spray quenches it toward the ~35 °C floor.
        double pf = Math.Clamp(ContainmentPressureKpa / CtmtPeakLocaKpa, 0.0, 1.0);
        double tTarget = ContainmentAmbientC + pf * (ContainmentPeakC - ContainmentAmbientC);
        if (ContainmentSprayActive) tTarget = Math.Min(tTarget, ContainmentSprayTempC + 10.0);
        ContainmentTempC += (tTarget - ContainmentTempC) * fP;

        // --- containment-pressure ESFAS bistables (latch on up-crossing, reset below set − deadband) -
        if (ContainmentPressureKpa >= CtmtHi1Kpa) _ctmtHi1 = true;
        else if (ContainmentPressureKpa < CtmtHi1Kpa - CtmtHystKpa) _ctmtHi1 = false;
        if (ContainmentPressureKpa >= CtmtHi2Kpa) _ctmtHi2 = true;
        else if (ContainmentPressureKpa < CtmtHi2Kpa - CtmtHystKpa) _ctmtHi2 = false;
        if (ContainmentPressureKpa >= CtmtHi3Kpa) _ctmtHi3 = true;
        else if (ContainmentPressureKpa < CtmtHi3Kpa - CtmtHystKpa) _ctmtHi3 = false;

        // Hi-1 (~4 psig): Safety Injection + Containment Isolation Phase A; safeguards-sequence fan
        // coolers start; the SI signal trips the reactor (P-4) — mirrors the Lo-Steamline-Press path.
        ContainmentIsolationPhaseA = _ctmtHi1;
        if (_ctmtHi1)
        {
            SiActuated = true;
            EccsArmed = true;
            ContainmentFanCoolers = true;
            if (!IsScrammed && Mode != ReactorMode.Meltdown)
            {
                LastTripFunctionEn = "Containment Pressure Hi-1 (SI)";
                LastTripFunctionZh = "安全殼壓力高 Hi-1（安全注入）";
                Scram();
            }
        }

        // Hi-2 (~10 psig): Main Steam Line Isolation — close the MSIVs (terminates an MSLB blowdown so
        // MslbBreakFlow → 0 next tick and the source collapses, exactly like the SI MSIV closure).
        if (_ctmtHi2) MslbIsolated = true;

        // Hi-3 (~27 psig): Containment Spray (after a ~35 s pump-start/valve-stroke delay) + Phase B.
        ContainmentIsolationPhaseB = _ctmtHi3;
        if (_ctmtHi3)
        {
            _spraySetupTimer += dt;
            if (_spraySetupTimer >= SpraySetupSeconds) ContainmentSprayActive = true;
        }
        else
        {
            _spraySetupTimer = 0;
            ContainmentSprayActive = false;
        }
    }

    private void UpdateNis(double dt)
    {
        // Source-range count rate ∝ (source + power); 1/M for the approach-to-criticality plot.
        // Ceiling at the BF3 saturation point (1e6 cps) so the 1e5-cps Source-Range High-Flux trip is
        // on-scale; above P-6/P-10 the SR detectors are de-energized anyway (see SourceRangeEnergized).
        SourceRangeCps = Math.Clamp((SourceLevel + _power) * 1e11, 1, SourceRangeMaxCps);
        OneOverM = Math.Clamp(SourceBaselineCps / SourceRangeCps, 0, 1);
        StartupRateDpm = (ReactorPeriodSeconds > 0 && ReactorPeriodSeconds < 1e8)
            ? 0.4343 * 60.0 / ReactorPeriodSeconds : 0; // = 26.06 / period (decades per minute)

        // Intermediate range — compensated ion chamber current, log over ~8 decades (1e-11..1e-3 A).
        IntermediateRangeAmps = Math.Clamp(Math.Max(_power, 0) * IrFullScaleAmps, IrBottomAmps, IrFullScaleAmps);
        IntermediateRangeDecades = Math.Log10(IntermediateRangeAmps / IrBottomAmps); // 0..8, always finite
        IntermediateRangePercent = Math.Max(_power, 0) * 100.0;
        // Power range — uncompensated ion chambers, linear 0..120 % rated power.
        PowerRangePercent = Math.Clamp(_power * 100.0, 0, 120);

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

    /// <summary>
    /// 推進 1E 級配電並施加其供電閘 · Advance the Class 1E electrical model and apply its availability gates.
    /// SBO is LOOP (offsite lost) coincident with both EDGs failing — exactly the design-basis definition.
    /// </summary>
    private void UpdateElectrical(double dt)
    {
        bool sbo = ActiveScenario == ReactorScenario.StationBlackout;
        _elec.Step(dt, new ReactorElectrical.Inputs(
            OffsiteAvailable: !sbo,                       // LOOP only in the SBO scenario for now
            SiSignal: SiActuated,                         // SI also auto-starts the diesels
            EdgAFault: sbo, EdgBFault: sbo,               // SBO ⇒ both EDGs unavailable (the definition)
            SgSteamPressurePsig: SteamPressure * 145.038, // motive steam for the turbine-driven AFW pump
            AfwDemand: _lofwTimer > 60.0));               // a secondary heat sink is being called for

        // Reactor coolant pumps are large non-1E motors fed from offsite power — a LOOP drops them all,
        // and they cannot be restarted on diesel power. UpdateFlow then coasts them down on their flywheels.
        if (!_elec.RcpAvailable)
            for (int i = 0; i < RcpRunning.Length; i++) RcpRunning[i] = false;
    }

    private void UpdateFlow(double dt)
    {
        // ---- forced flow: per-loop, asymmetric (fast spin-up, inertial coastdown) ----
        double demand = Math.Clamp(RcpFlowDemand, 0, 1);
        double pumped = 0.0;
        bool coasting = false;
        double spinAlpha = 1.0 - Math.Exp(-dt / RcpSpinUpTau);     // exact first-order lag factor
        double coastDen  = 1.0 + dt * (0.6931471805599453 / RcpCoastHalf); // implicit hyperbolic 1/(1+k·dt)
        for (int i = 0; i < _rcpFlow.Length; i++)
        {
            if (RcpRunning[i])
            {
                // Energised: relax toward this loop's share of the commanded flow.
                _rcpFlow[i] += (RcpLoopShare * demand - _rcpFlow[i]) * spinAlpha;
            }
            else
            {
                // Tripped: hyperbolic coastdown on flywheel/fluid inertia — flow halves every RcpCoastHalf.
                _rcpFlow[i] /= coastDen;
                if (_rcpFlow[i] > 0.01) coasting = true;
            }
            if (_rcpFlow[i] < 0) _rcpFlow[i] = 0;
            pumped += _rcpFlow[i];
        }

        // ---- natural-circulation floor: buoyancy thermosiphon, W ∝ Q^(1/3) ----
        double powerFrac = Math.Max(_power, 0.0) + DecayHeatFraction;     // core heat driving the head
        double headGate  = Math.Clamp((Thot - Tcold - NatCircDtMin) / NatCircHeadSpan, 0, 1);
        double sinkGate  = Math.Clamp((Tavg - SecondarySatTemp()) / NatCircSinkSpan, 0, 1);
        double hot       = Thot > 100.0 ? 1.0 : 0.0;                       // no phantom floor on a cold core
        double natural   = Math.Min(NatCircMax, NatCircCoef * Math.Cbrt(powerFrac) * headGate * sinkGate * hot);

        PumpedFlowFraction = Math.Clamp(pumped, 0, 1);
        NaturalCircFraction = natural;
        RcpCoasting = coasting;
        OnNaturalCirc = natural > 0 && natural >= pumped;
        // Take whichever path moves more coolant — never sum (the buoyancy loop IS the pump loop).
        CoolantFlowFraction = Math.Clamp(Math.Max(pumped, natural), 0, 1);
    }

    private void StepKineticsAndThermal(double h)
    {
        // ---- compute reactivity ----
        // Per-bank S-curve integral worth: S(x)=x − sin(2πx)/(2π), x = fraction inserted. The
        // differential worth dS/dx ∝ (1 − cos 2πx) is bell-shaped — ~zero at the core top/bottom and
        // peaking at mid-plane — the classic control-rod worth curve. Σ frac_b = 1 with S(0)=0, S(1)=1
        // keeps the all-out (0) and all-in (−TotalRodWorth) endpoints exactly where the baseline expects.
        double rodWorthSum = 0;
        for (int b = 0; b < RodBankInsertion.Length; b++)
            rodWorthSum += RodWorthFrac[b] * RodS(RodBankInsertion[b] / 100.0);
        double rodRho = -TotalRodWorth * rodWorthSum;

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

        // ---- point kinetics (6 groups), unconditionally-stable backward (implicit) Euler ----
        // The prompt mode is stiff: near critical its eigenvalue is ~ -BetaTotal/PromptLifetime
        // (~-325/s for these constants), so explicit forward-Euler needs h < ~6 ms. We step at
        // h = SubDt = 0.02 s, ~3x over that limit, where forward-Euler oscillates sign-to-sign and
        // diverges on ANY reactivity insertion. Backward Euler is stable for all h below prompt-
        // critical. We collapse the full 7-state implicit system into one scalar solve: substitute
        // each C_i^{n+1} = (C_i + h*(beta_i/Lambda)*P^{n+1})/(1 + h*lambda_i) into the power balance.
        double precursorContribution = 0; // A: lagged-but-corrected precursor source
        double implicitFeedback = 0;      // B: implicit precursor feedback into power
        for (int i = 0; i < 6; i++)
        {
            double di = 1.0 + h * Lambda[i];
            precursorContribution += Lambda[i] * _precursor[i] / di;
            implicitFeedback += h * Lambda[i] * Beta[i] / (PromptLifetime * di);
        }

        double denom = 1.0 - h * (rho - BetaTotal) / PromptLifetime - h * implicitFeedback;
        if (denom < 1e-3) denom = 1e-3; // guard: denom crosses zero at/above prompt-critical (rho >= ~1$)

        double newPower = (_power + h * (precursorContribution + SourceLevel)) / denom;
        if (newPower < 1e-12) newPower = 1e-12; // floor, never zero/negative (would poison precursors+source)

        for (int i = 0; i < 6; i++)
        {
            // Implicit precursor update uses the new power; unconditionally stable, no overshoot.
            _precursor[i] = (_precursor[i] + h * (Beta[i] / PromptLifetime) * newPower) / (1.0 + h * Lambda[i]);
            if (_precursor[i] < 0) _precursor[i] = 0;
        }

        // Reactor period from rate of change (computed before _power is overwritten).
        double rate = (newPower - _power) / (Math.Max(_power, 1e-12) * h);
        ReactorPeriodSeconds = Math.Abs(rate) < 1e-9 ? 1e9 : 1.0 / rate;
        _power = newPower;
        if (_power > 50) _power = 50; // numerical guard (5000 % is way past meltdown anyway)

        // ---- thermal-hydraulics (lumped) ----
        StepThermal(h);

        // ---- two-node axial split → axial offset / axial flux difference (ΔI) ----
        // Lead control bank D (index 3) bites the top of the core; its insertion relative to the steady
        // HFP bite (DBandHfpFrac) swings the shape negative. A top-minus-bottom xenon difference adds a
        // slower term. Both go through a stable first-order lag; everything is clamped so a transient
        // cannot blow up ΔI.
        double rodInsFrac = Math.Clamp(RodBankInsertion[3] / 100.0, 0.0, 1.0); // bank D, 1 = fully in
        double rodTerm = -AxialRodWeight * (rodInsFrac - DBandHfpFrac) / (1.0 - DBandHfpFrac) + AxialEquilBias;
        double xeDiff = Math.Clamp(_xenonTop - _xenonBot, -1.0, 1.0); // top-heavy Xe depresses top flux
        double xeTerm = -AxialXenonWeight * xeDiff;
        _axialSplitTarget = Math.Clamp(rodTerm + xeTerm, -1.0, 1.0);
        _axialSplit += (_axialSplitTarget - _axialSplit) * Math.Min(1.0, h / AxialTau);

        double half = 0.5 * Math.Clamp(_axialSplit, -1.0, 1.0);
        TopPowerFraction = 0.5 + half;
        BottomPowerFraction = 0.5 - half;
        AxialOffsetPercent = (TopPowerFraction - BottomPowerFraction) * 100.0;           // AO, % (shape only)
        AxialFluxDifferencePercent =
            (TopPowerFraction - BottomPowerFraction) * AfdFullScalePct * Math.Clamp(_power, 0.0, 1.2); // ΔI, %RTP
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
        // MSLB direct overcooling: the break itself flashes SG inventory and pulls primary heat even when
        // feedwater is gone (the 0.3+0.7·FW factor above throttles the base term). Scales with break flow
        // and the primary-to-secondary temperature head; clamped ≥0 so it can never heat the primary.
        if (MslbBreakFlow > 0)
            sgRemoval += 0.6 * MslbBreakFlow * Math.Max(0, Tavg - SecondarySatTemp());
        double coolantHeatCap = 60.0; // MW·s per °C
        double netCoolant = fuelToCoolant - sgRemoval;
        double avg = Tavg + netCoolant / coolantHeatCap * h;

        // Flow sets the Thot-Tcold spread for a given power: deltaT ~ power / flow.
        double flow = Math.Max(CoolantFlowFraction, 0.02);
        double deltaT = Math.Clamp(35.0 * _power / flow, 0, 120);
        Tcold = avg - deltaT / 2.0;
        Thot = avg + deltaT / 2.0;
        if (Tcold < ColdTemp) Tcold = ColdTemp;

        // ---- pressurizer water level: programmed to Tavg (insurge/outsurge swell) + ECCS/relief/leak ----
        // Level is computed first so the surge rate d(level)/dt below reflects this substep.
        double lvlTarget = NominalPzrLevel + (avg - NominalTavg) * 0.30;
        if (EccsInjecting) lvlTarget += 25;
        if (ReliefValveOpen || _porvAuto) lvlTarget -= 10;
        lvlTarget -= PrimaryDeficitPct; // pressurizer level falls as a leak drains the RCS
        PressurizerLevel += (lvlTarget - PressurizerLevel) * Math.Min(1, h / 8.0);
        PressurizerLevel = Math.Clamp(PressurizerLevel, 0, 100);

        // ---- primary pressure via a saturated-pressurizer model ----
        // Real PWR pressure is the saturation pressure of the one free liquid/steam surface in the plant
        // (the pressurizer), NOT Psat(Tavg): the rest of the loop is deliberately sub-cooled. Heaters and
        // spray therefore act on the pressurizer LIQUID temperature _tpzr (energy), and P follows Psat(_tpzr).
        double dLevelDt = Math.Clamp((PressurizerLevel - _prevLevel) / Math.Max(h, 1e-6), -20.0, 20.0);
        _prevLevel = PressurizerLevel;

        // Automatic Westinghouse pressure-control program → heater duty + spray fraction from pressure.
        double heaterDuty, sprayFrac;
        if (PzrAutoPressureControl)
        {
            heaterDuty = Math.Clamp((PzrProgramPressure - PrimaryPressure) /
                                    (PzrProgramPressure - PzrPropHeaterFull), 0, 1); // proportional heaters
            if (PrimaryPressure < PzrBackupHeaterOn) heaterDuty = 1.0;               // backup banks energize
            sprayFrac = Math.Clamp((PrimaryPressure - PzrSprayOpen) /
                                   (PzrSprayFull - PzrSprayOpen), 0, 1);             // modulating spray
        }
        else
        {
            heaterDuty = PressurizerHeater ? 1.0 : 0.0; // manual on/off
            sprayFrac  = PressurizerSpray ? 1.0 : 0.0;
        }
        PressurizerHeaterDuty = heaterDuty;

        // Energy balance on the pressurizer liquid lump → its temperature.
        double qHeater = PzrHeaterMW * heaterDuty;
        double qSpray  = PzrSprayK * sprayFrac * Math.Max(0, _tpzr - Tcold);          // cold-leg spray condenses steam
        double qSurge  = dLevelDt > 0 ? PzrSurgeK * dLevelDt * Math.Max(0, avg - _tpzr) : 0.0; // insurge of hot water
        double qLoss   = PzrLossK * (_tpzr - 50.0);
        _tpzr += (qHeater - qSpray + qSurge - qLoss) / PzrHeatCap * h;
        _tpzr = Math.Clamp(_tpzr, 80.0, 360.0);

        // Pressure = saturation pressure of the liquid surface, plus a fast adiabatic compression spike on
        // insurge (rising level shrinks the steam bubble → +dP, recondensing over ~8 s), minus leak deficit.
        double pSat = SatPressAt(_tpzr);
        _pSpike += PzrCompK * dLevelDt * h;
        _pSpike -= _pSpike * Math.Min(1, h / PzrSpikeTau);
        _pSpike = Math.Clamp(_pSpike, -1.5, 2.0);
        double pTarget = Math.Max(pSat, PzrMakeupFloor) + _pSpike - 0.11 * PrimaryDeficitPct;
        if (pTarget < 0.3) pTarget = 0.3;

        // During a LOCA the break owns depressurization — the saturated model may only pull pressure DOWN.
        if (_breakArea > 0)
        {
            if (pTarget < PrimaryPressure)
                PrimaryPressure += (pTarget - PrimaryPressure) * Math.Min(1, h / PzrPresTau);
        }
        else
        {
            PrimaryPressure += (pTarget - PrimaryPressure) * Math.Min(1, h / PzrPresTau);
        }

        // Automatic PORV: opens at 2335 psig, reseats at 2315 psig. The PORV is the lower-set, reclosable
        // first-responder, so it is evaluated and relieves BEFORE the code safeties each step.
        if (PrimaryPressure > PorvOpenPressure) _porvAuto = true;
        else if (PrimaryPressure < PorvClosePressure) _porvAuto = false;
        if (_porvAuto) PrimaryPressure -= PorvReliefRate * h;

        // ASME code (spring) safety valves — the last-ditch overpressure layer above the PORV. Pop-action:
        // each valve latches fully open when pressure exceeds its own set and only reseats once pressure has
        // fallen a full blowdown band below that set (hysteresis → no chatter at the fixed timestep). Combined
        // relief scales with valve count and a choked-flow surrogate (lift fraction ∝ overpressure above seat).
        double lowestLiftedSeat = double.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            if (!_pzrSafetyOpen[i] && PrimaryPressure > PzrSafetySet[i])
            {
                _pzrSafetyOpen[i] = true;          // rising edge — fire the annunciator once per individual pop
                PzrCodeSafetyLifted?.Invoke();
            }
            else if (_pzrSafetyOpen[i] && PrimaryPressure < PzrSafetySet[i] - PzrSafetyBlowdown)
            {
                _pzrSafetyOpen[i] = false;         // reseat only after full blowdown
            }
            if (_pzrSafetyOpen[i] && PzrSafetySet[i] < lowestLiftedSeat) lowestLiftedSeat = PzrSafetySet[i];
        }
        int safetiesOpen = PzrCodeSafetiesOpen;
        if (safetiesOpen > 0)
        {
            double liftFrac = Math.Clamp((PrimaryPressure - lowestLiftedSeat) / (PzrSafetyAccum - lowestLiftedSeat), 0.0, 1.0);
            double safetyDrop = safetiesOpen * PzrSafetyReliefRate * liftFrac * h;
            safetyDrop = Math.Min(safetyDrop, Math.Max(0.0, PrimaryPressure - 0.1)); // never overshoot the floor in one step
            PrimaryPressure -= safetyDrop;
        }

        if (ReliefValveOpen && PrimaryPressure > 2.0) PrimaryPressure -= 3.0 * h;
        PrimaryPressure = Math.Clamp(PrimaryPressure, 0.1, VesselBurstPressure);
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

        // Per-node (top/bottom) I-Xe split, same balance forms with production weighted by node power
        // fraction. The node mean is written back to the scalars so the global xenon reactivity is
        // identical to the legacy single-node model — only the top-minus-bottom DIFFERENCE is new, and
        // it feeds the axial-offset / ΔI shape (axial xenon oscillations after a rod/load change).
        double phiTop = phi * 2.0 * TopPowerFraction;  // ×2: each node nominally carries half the core
        double phiBot = phi * 2.0 * BottomPowerFraction;
        _iodineTop += (0.95 * phiTop * lamI - lamI * _iodineTop) * dt; if (_iodineTop < 0) _iodineTop = 0;
        _iodineBot += (0.95 * phiBot * lamI - lamI * _iodineBot) * dt; if (_iodineBot < 0) _iodineBot = 0;
        _xenonTop += (lamI * _iodineTop + gammaX * phiTop - lamX * _xenonTop - sigmaPhi * phiTop * _xenonTop / 7.0e-5) * dt;
        _xenonBot += (lamI * _iodineBot + gammaX * phiBot - lamX * _xenonBot - sigmaPhi * phiBot * _xenonBot / 7.0e-5) * dt;
        if (_xenonTop < 0) _xenonTop = 0;
        if (_xenonBot < 0) _xenonBot = 0;
        Iodine = 0.5 * (_iodineTop + _iodineBot);  // mean == legacy scalar (identity)
        Xenon = 0.5 * (_xenonTop + _xenonBot);
    }

    // 三元給水水位控制 · Three-element steam-generator level control with shrink/swell.
    //   A real SG narrow-range level tap measures the height of the *two-phase* swell, not pure liquid
    //   inventory, so on a load increase (more steaming, falling SG pressure) the voids expand and the
    //   indicated level momentarily SWELLS UP even as mass drains — and shrinks on a load rejection. A
    //   level-only controller reacts backwards to that, which is precisely why the protection scheme adds
    //   steam-flow feedforward and feed-flow trim above ~18% power. Below that, the dP-based flow signals
    //   (∝ √dP, so ~1% of span at 10% flow) are too noisy to trust, so control falls back to level-only on
    //   the low-load valve. Here: true inventory stays SteamGenLevel (the conserved integral of feed−steam);
    //   the washed-out void transient is added only to IndicatedSgLevel, which is what the gauge and the
    //   17 % low-low trip actually see.
    private void UpdateFeedwaterControl(double dt)
    {
        // Steam-flow transmitter (lagged) — both the feedforward term and the shrink/swell driver. Tracks the
        // REAL first-stage pressure (governor-valve steam flow), not the raw demand, so feed follows the EHC.
        double steamDrawRaw = GeneratorBreakerClosed ? FirstStagePressure : 0.0;
        SteamFlow += (steamDrawRaw - SteamFlow) * Math.Min(1, dt / 1.0);
        _steamFlowSlow += (SteamFlow - _steamFlowSlow) * Math.Min(1, dt / 12.0); // 12 s void-relaxation washout

        // Shrink/swell = high-pass residual of steam flow (rises → swell, falls → shrink), decays to 0.
        double shrinkSwell = Math.Clamp(45.0 * (SteamFlow - _steamFlowSlow), -15.0, 15.0);
        IndicatedSgLevel = Math.Clamp(SteamGenLevel + shrinkSwell, 0, 100);

        // Programmed (compressed) narrow-range setpoint: 33% NR at low power → 50% NR at full power.
        SgLevelSetpoint = 33.0 + 17.0 * Math.Clamp(_power, 0, 1);

        // Station blackout: no AC power to the main feed pumps — the regulating controller is dead.
        if (ActiveScenario == ReactorScenario.StationBlackout) { _fwAutoWasOn = false; FeedRegValve = 0; return; }

        // Manual: the operator's slider owns FeedwaterFlow exactly as before.
        if (!FeedwaterAuto) { _fwAutoWasOn = false; return; }

        // Bumpless transfer when AUTO is (re)engaged: preload the valve to the current flow, zero the integrator.
        if (!_fwAutoWasOn) { FeedRegValve = FeedwaterFlow; _iLevel = 0; _fwAutoWasOn = true; }

        // Single ↔ three-element transfer at ~18% power, 2% hysteresis so the boundary doesn't chatter.
        if (_power >= 0.18) _threeElementActive = true;
        else if (_power < 0.16) _threeElementActive = false;

        double err = SgLevelSetpoint - IndicatedSgLevel;            // % NR error (controller acts on indicated)
        _iLevel = Math.Clamp(_iLevel + 0.004 * err * dt, -0.5, 0.5); // master level-PI integral
        double demand = _threeElementActive
            ? SteamFlow + 0.020 * err + _iLevel                    // three-element: feedforward + level trim
            : 0.05 + 0.020 * err + _iLevel;                        // single-element: level-only on low-load valve
        double demandClamped = Math.Clamp(demand, 0, 1);
        _iLevel -= demand - demandClamped;                          // back-calculation anti-windup
        _iLevel = Math.Clamp(_iLevel, -0.5, 0.5);

        // Feed-reg valve first-order lag (~4 s stroke) is the inner (flow) loop.
        FeedRegValve += (demandClamped - FeedRegValve) * Math.Min(1, dt / 4.0);
        FeedRegValve = Math.Clamp(FeedRegValve, 0, 1);

        // Loss of feedwater: main feed-reg path unavailable — never command MFW; AFW floor (set this tick in
        // UpdateScenarios, which runs first) is the only inventory source. Otherwise apply the valve flow.
        double autoFlow = ActiveScenario == ReactorScenario.LossOfFeedwater ? 0.0 : FeedRegValve;
        FeedwaterFlow = Math.Max(autoFlow, AuxFeedwaterRunning ? 0.15 : 0.0);
    }

    private void UpdateSecondary(double dt)
    {
        // NOTE: SteamPressure is updated in the EHC turbine block below (single writer), AFTER the
        // governor valves set the real steam draw — closing the GVs must be able to RAISE header pressure.

        // SG inventory is an INTEGRATOR: level is the time-integral of (feedwater − steaming) mismatch.
        // SteamFlow (the lagged controlled-by feedwater-control signal) is the boil-off the feed must replace,
        // so inventory balance and the three-element feedforward stay consistent.
        SteamGenLevel += 12.0 * (FeedwaterFlow - SteamFlow) * dt;
        // SGTR: leaked primary water floods the affected SG, biasing its level toward "solid" (overfill)
        // until the operator isolates it (MSIV + feedwater). UpdateSecondary is the sole writer of
        // SteamGenLevel, so biasing here drives the fill without a tug-of-war with the controller.
        if (_sgtrSeverity > 0 && SgtrLeakRate > 0 && !SgtrIsolated)
            SteamGenLevel = Math.Max(SteamGenLevel, 60 + 130 * SgtrLeakRate);
        SteamGenLevel = Math.Clamp(SteamGenLevel, 0, 100);

        // SGTR: once the affected SG goes solid (or oversteams) its safety/relief valve lifts and vents
        // contaminated steam to atmosphere — the radiological release the whole event hinges on.
        if (_sgtrSeverity > 0 && !SgtrIsolated && (SteamGenLevel > 95.0 || SteamPressure > 8.0))
        {
            SgReliefLifted = true;
            double released = 0.25 * SecondaryActivity * dt;
            AtmosphericRelease += released;
            SecondaryActivity = Math.Max(0, SecondaryActivity - released);
            SteamPressure = Math.Max(SteamPressure - 0.4 * dt, 0.3); // relief blowdown holds pressure in check
        }
        else if (_sgtrSeverity > 0)
        {
            SgReliefLifted = false;
        }

        // ===================== EHC turbine (Electro-Hydraulic Control) · 電液調速控制 =====================
        // The operator demand (TurbineLoadSetpoint) no longer drives load directly: it is a SETPOINT the
        // EHC tracks through a rate-limited load reference, real governor-valve dynamics, droop speed
        // control, OPC fast-close and a latching overspeed trip — exactly as a Westinghouse/GE DEH does.

        // 1) Operator demand → rate-limited internal LOAD REFERENCE (%/min, asymmetric: faster runback).
        double rampUp   = (GvRateUpPerMin   / 100.0) / 60.0 * dt;
        double rampDown = (GvRateDownPerMin / 100.0) / 60.0 * dt;
        double demand   = Math.Clamp(TurbineLoadSetpoint, 0.0, 1.0);
        if (demand > LoadReference) LoadReference = Math.Min(demand, LoadReference + rampUp);
        else                        LoadReference = Math.Max(demand, LoadReference - rampDown);

        // 2) Speed error (drives droop post-sync; display + acceleration control pre-sync).
        TurbineSpeedError = SyncRpm - TurbineRPM;

        // 3) Commanded governor-valve position from the EHC mode logic.
        bool steamAvail = SteamPressure > 3.0;
        double gvCmd;
        if (TurbineTripped || !steamAvail)
        {
            gvCmd = 0.0;                                       // stop valves shut / no steam to admit
        }
        else if (!GeneratorBreakerClosed)
        {
            // PRE-SYNC: SPEED control — accelerate toward 1800 rpm at a limited rate, then hold.
            double speedDemandRpm = Math.Min(SyncRpm, TurbineRPM + SpeedRampRpmPerS * dt);
            double spErr = (speedDemandRpm - TurbineRPM) / SyncRpm;
            gvCmd = Math.Clamp(0.5 * TurbineRPM / SyncRpm + 4.0 * spErr, 0.0, 1.0);
        }
        else
        {
            // POST-SYNC: LOAD control with 5 % droop. The grid pins the shaft at 1800 rpm so droop is ~0
            // in steady state, but any overspeed excursion (load-rejection onset) biases the GVs closed.
            double droopBias = -(TurbineSpeedError / SyncRpm) / DroopFrac;
            gvCmd = Math.Clamp(LoadReference + droopBias, 0.0, 1.0);
        }

        // 4) OPC / power-load-unbalance fast-close (NON-latching): momentary overspeed slams the GVs shut.
        if (TurbineRPM > OpcSetpointRpm) gvCmd = 0.0;

        // 5) Overspeed TRIP (latching) at 110 %: stop valves shut and stay shut until a reset.
        if (!TurbineTripped && TurbineRPM > OverspeedTripRpm) TurbineTripped = true;

        // 6) Governor-valve ACTUATOR: first-order hydraulic servo toward the command.
        _gvCmd = gvCmd;
        GovernorValve += (_gvCmd - GovernorValve) * Math.Min(1, dt / GvActuatorTau);
        GovernorValve  = Math.Clamp(GovernorValve, 0.0, 1.0);

        // 7) Steam flow THROUGH the GV = position × (header-pressure factor).
        double steamDraw = TurbineTripped ? 0.0
            : GovernorValve * Math.Clamp(SteamPressure / NominalSteamPressure, 0, 1.1);

        // 8) First-stage (impulse-chamber) pressure — the calibrated load signal, linear in GV steam flow.
        FirstStagePressure = Math.Clamp(FspGain * steamDraw, 0.0, 1.0);

        // --- SteamPressure (single writer, MOVED here) — closing the GVs reduces draw ⇒ header pressure rises.
        double pTarget = 0.5 + 6.5 * Math.Clamp(_power - SteamPressDrawK * steamDraw + 0.4, 0, 1.2);
        pTarget = Math.Clamp(pTarget, 0.3, 8.5);
        SteamPressure += (pTarget - SteamPressure) * Math.Min(1, dt / 5.0);

        // 9) TurbineRPM (single writer).
        double rpmTarget;
        if (GeneratorBreakerClosed && !TurbineTripped && steamAvail)
            rpmTarget = SyncRpm;                                              // grid-locked at synchronous speed
        else if (!GeneratorBreakerClosed && steamAvail && !TurbineTripped)
            rpmTarget = SyncRpm * Math.Clamp(GovernorValve, 0.1, 1.0);        // GV admits steam → shaft spins up
        else
            rpmTarget = 0.0;                                                  // tripped / no steam → coast down
        // Load rejection: breaker opens while the GVs still admit steam → the unloaded shaft overspeeds
        // toward the trip (OPC at 1854 then mechanical trip at 1980 arrest it via steps 4–5).
        if (!GeneratorBreakerClosed && steamAvail && steamDraw > 0.05)
            rpmTarget = Math.Max(rpmTarget, SyncRpm * (1.0 + 0.12 * steamDraw)); // up to ~2016 rpm
        TurbineRPM += (rpmTarget - TurbineRPM) * Math.Min(1, dt / 4.0);
        if (TurbineRPM < 0) TurbineRPM = 0;

        // 10) Electrical output (single writer). Real load follows first-stage pressure, capped by available
        // mechanical power (≈33 % of thermal). Zero when not synchronized, tripped, or below 94 % speed.
        double grossElec = Math.Min(_power * 0.33, FirstStagePressure) * RatedElectricMW;
        if (GeneratorBreakerClosed && !TurbineTripped && TurbineRPM > SyncRpm * 0.94 && steamAvail)
            ElectricPowerMW += (grossElec - ElectricPowerMW) * Math.Min(1, dt / 4.0);
        else
            ElectricPowerMW += (0 - ElectricPowerMW) * Math.Min(1, dt / 2.0);
        if (ElectricPowerMW < 0) ElectricPowerMW = 0;
    }

    private bool _autoRodWasOn;

    private void UpdateAutoRods(double dt)
    {
        // On engaging AUTO, sync the group-demand counter to the current bank stack so control is bumpless.
        if (!_autoRodWasOn) _rodDemandCounter = InferRodDemandFromBanks();
        _autoRodWasOn = true;

        // Proportional controller drives the single group-demand counter (0..528); banks then follow
        // it with 128-step overlap — never all moving uniformly, exactly like a Westinghouse rod-control.
        double err  = AutoPowerSetpoint - _power;          // err>0 (need power) -> withdraw -> raise counter
        double gain = 6.0 * (RodTotalSpan / 100.0);        // steps/s per unit power error
        _rodDemandCounter = Math.Clamp(_rodDemandCounter + err * gain * dt, 0, RodTotalSpan);
        ApplyOverlapToBanks(_rodDemandCounter);
    }

    // Map the group-demand counter to per-bank insertion %. Bank k begins withdrawing at k·stride and
    // is fully out after a further 228 steps; the 128-step overlap means two banks move together.
    private void ApplyOverlapToBanks(double counter)
    {
        for (int k = 0; k < RodBankInsertion.Length; k++)
        {
            double stepsWithdrawn = Math.Clamp(counter - k * RodStride, 0.0, RodStepsPerBank);
            RodBankInsertion[k] = (1.0 - stepsWithdrawn / RodStepsPerBank) * 100.0;
        }
    }

    private void UpdateProtection(double dt)
    {
        // ---- Reactor Protection System: derive permissives from power, then evaluate coincidence. ----
        // P-10 / P-7 ≈ 10 % power; P-8 ≈ 48 %; P-9 ≈ 50 % (P-13 turbine-load input folded into P-7).
        bool p10 = _power >= 0.10;
        bool p13 = FirstStagePressure >= 0.10 && GeneratorBreakerClosed; // turbine first-stage pressure permissive (real load)
        bool p7 = p10 || p13;
        // P-6: asserted when the intermediate-range current rises on-scale (≥ 1e-10 A), latched with a
        // ±10 % deadband so it cannot chatter. Permits blocking the Source-Range High-Flux trip and
        // de-energizing the SR detectors. (The old `_power < 1e-4` form was power-based and inverted.)
        if (!_p6Latched && IntermediateRangeAmps >= P6CurrentThresholdA * 1.10) _p6Latched = true;
        else if (_p6Latched && IntermediateRangeAmps < P6CurrentThresholdA * 0.90) _p6Latched = false;
        SourceRangeEnergized = !(_p6Latched || p10); // SR high voltage removed above P-6 or P-10
        _rps.SetPermissives(p6: _p6Latched, p7: p7, p8: _power >= 0.48, p9: _power >= 0.50, p10: p10);
        _rps.Evaluate();
        if (_rps.ReactorTrip && !IsScrammed && Mode != ReactorMode.Meltdown)
        {
            LastTripFunctionEn = _rps.ControllingFunctionEn;
            LastTripFunctionZh = _rps.ControllingFunctionZh;
            Scram();
        }

        // P-9: anticipatory reactor-trip-on-turbine-trip, ABOVE ~50 % power. A turbine trip at high load
        // would otherwise spike primary temperature/pressure (loss of the heat sink); the reactor is
        // tripped first. Below P-9 the interlock is blocked — the plant rides the runback instead.
        if (TurbineTripped && _power >= 0.50 && !IsScrammed && Mode != ReactorMode.Meltdown)
        {
            LastTripFunctionEn = "Turbine Trip (P-9)";
            LastTripFunctionZh = "汽輪機跳脫（P-9）";
            Scram();
        }

        // ---- ESFAS: Low Steamline Pressure → Safety Injection. Latches once the 2-of-4 coincidence is met
        //      (the SI signal seals in). SI does two decisive things for an MSLB: it auto-closes the MSIVs
        //      (terminating the blowdown so SteamPressure recovers and the overcooling stops) and it injects
        //      heavily-borated water, ramping RCS boron toward ~2000 ppm so the negative boron worth swamps
        //      the positive moderator reactivity from the cooldown — the design-basis defence against the
        //      MSLB return-to-power. Drive the boron path off this SI flag, NOT EccsInjecting: an MSLB is a
        //      secondary break, so the primary may never fall below the ECCS-inject pressure.
        if (_mslbSiFnIndex >= 0 && _rps.Functions[_mslbSiFnIndex].FunctionTrip)
        {
            SiActuated = true;
            MslbIsolated = true;                                       // automatic MSIV closure on SI
            TargetBoronPpm = Math.Max(TargetBoronPpm, MslbSiBoronPpm); // borated SI; UpdateBoron ramps at 4 ppm/s
            EccsArmed = true;
        }

        // ECCS auto-inject on low pressure if armed — but the SI/HHSI/charging pumps are motor-driven and
        // need a live 4.16 kV Class 1E bus. In a station blackout (no AC) only the passive accumulators and
        // the turbine-driven AFW pump remain; the powered injection path is unavailable.
        EccsInjecting = EccsArmed && PrimaryPressure < 11.0 && _elec.MotorEccsSiAvailable;
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
        SetAlarm(ReactorAlarm.TurbineTrip,
            TurbineTripped || (GeneratorBreakerClosed && TurbineRPM < SyncRpm * 0.83 && SteamPressure > 3.0));
        SetAlarm(ReactorAlarm.LowSubcooling, SubcoolingMarginC < 10.0 && (_power > 0.05 || Thot > 200));
        SetAlarm(ReactorAlarm.DecayHeatHigh, DecayHeatFraction > 0.03 && CoolantFlowFraction < 0.4 && FeedwaterFlow < 0.1);
        SetAlarm(ReactorAlarm.AtwsActive, _rodsFailToInsert && IsScrammed);
        SetAlarm(ReactorAlarm.AccumulatorInject, AccumulatorInjecting);
        SetAlarm(ReactorAlarm.AuxFeedwater, AuxFeedwaterRunning);
        bool natCirc = CoolantFlowFraction > 0.005 && CoolantFlowFraction < 0.1 && Thot > 150;
        SetAlarm(ReactorAlarm.NaturalCirc, natCirc);
        SetAlarm(ReactorAlarm.SgtrLeak, _sgtrSeverity > 0 && SgtrLeakRate > 0.01);
        SetAlarm(ReactorAlarm.SteamlineBreak, _mslbSeverity > 0 && !MslbIsolated);
        SetAlarm(ReactorAlarm.SafetyInjection, SiActuated);
        SetAlarm(ReactorAlarm.PzrCodeSafetyOpen, AnyPzrCodeSafetyOpen);
        SetAlarm(ReactorAlarm.SecondaryRadiationHi, SecondaryActivity > 1.0);
        SetAlarm(ReactorAlarm.SgReliefLift, SgReliefLifted);
        SetAlarm(ReactorAlarm.RodInsertionLimitLo, RilLowAlarm && !RilLowLowAlarm);
        SetAlarm(ReactorAlarm.RodInsertionLimitLoLo, RilLowLowAlarm);
        SetAlarm(ReactorAlarm.RodDeviation, RodDeviationAlarm);
        SetAlarm(ReactorAlarm.AxialFluxDiffOutOfBand, AfdOutsideBand);
        SetAlarm(ReactorAlarm.ContainmentPressureHi, _ctmtHi1);
        SetAlarm(ReactorAlarm.ContainmentIsolation, ContainmentIsolationPhaseA || ContainmentIsolationPhaseB);
        SetAlarm(ReactorAlarm.ContainmentSpray, ContainmentSprayActive);
        SetAlarm(ReactorAlarm.LossOfOffsitePower, !_elec.OffsitePowerAvailable);
        SetAlarm(ReactorAlarm.StationBlackout, _elec.InSbo);
        SetAlarm(ReactorAlarm.EdgSupplyingBus, _elec.OnEdgPower);
        SetAlarm(ReactorAlarm.TurbineDrivenAfw, _elec.TdafwRunning);
        SetAlarm(ReactorAlarm.DcBusDepleted, !_elec.DcAvailable);
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
        if (_sgtrSeverity > 0)
        {
            if (SgReliefLifted) { StatusEn = "SGTR — atmospheric release in progress"; StatusZh = "爆管事故 — 放射性正排入大氣"; return; }
            if (SgtrIsolated) { StatusEn = "SGTR isolated — depressurize RCS to equalize (dP→0)"; StatusZh = "已隔離爆管 — 降低一次側壓力使壓差歸零止漏"; return; }
            StatusEn = "SGTR — identify & isolate affected SG"; StatusZh = "爆管事故 — 辨識並隔離受影響蒸發器"; return;
        }
        if (_power < 1e-4) { StatusEn = "Shut down / subcritical"; StatusZh = "停機／次臨界"; Mode = Mode == ReactorMode.Run ? ReactorMode.Startup : Mode; return; }
        if (Math.Abs(_power - 1.0) < 0.03 && Math.Abs(ReactorPeriodSeconds) > 200)
        { StatusEn = "Stable at power"; StatusZh = "穩定運轉"; return; }
        if (ReactorPeriodSeconds > 0 && ReactorPeriodSeconds < 60)
        { StatusEn = "Rising power — watch period"; StatusZh = "功率上升中 — 注意週期"; return; }
        StatusEn = "Operating"; StatusZh = "運轉中";
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
