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
    CondenserVacuumLow,
    LowSubcooling,
    DecayHeatHigh,
    AtwsActive,
    AmsacActuated,     // AMSAC (ATWS Mitigating System Actuation Circuitry, 10 CFR 50.62) actuated
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
    QuadrantPowerTiltHi, // QPTR > 1.02 above 50% RTP — azimuthal radial power tilt (LCO 3.2.4)
    DroppedRcca,         // a single full-length RCCA has dropped to the bottom of the core (rod-bottom)
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
    CoreUncovered,
    PeakCladTempLimit,
    CladOxidationLimit,
    HydrogenGenerationLimit,
    DnbrSafetyLimit,   // MinDnbr < 1.30  (W-3 95/95 design limit)
    DnbrLowMargin,     // MinDnbr < 1.55  (low-margin warning)
    RcpSealLoca,       // RCP seal-cooling lost → degraded seal leakoff (WOG-2000 seal LOCA)
    RvlisBelowTopOfFuel, // RVLIS advisory: vessel collapsed level below top of active fuel (NOT the ICC trip)
    RvlisFullRangeLoLo,  // RVLIS full-range collapsed level < 40% (well into the uncovered-fuel band)
    PtApproach,          // RCS pressure within 1.0 MPa of the 10 CFR 50 App G brittle-fracture P/T limit
    PtViolation,         // RCS pressure has crossed the App G P/T limit (brittle-fracture concern)
    RcsRateExceeded,     // |RCS heatup/cooldown rate| > 90% of the App G 100 °F/hr (55.6 °C/hr) limit
    LtopActive,          // LTOP/COMS armed and its low-setpoint PORV path is actively relieving
    IccOrange,           // ICC ORANGE — core-exit TC ≥ 700 °F (371 °C) OR subcooling margin lost (FR-C.2)
    IccRed,              // ICC RED — core-exit TC ≥ 1200 °F (649 °C); core damage imminent (FR-C.1)
    ContainmentH2Flammable, // containment H₂ ≥ 4 vol% (LFL) with O₂ — deflagration possible (10 CFR 50.44)
    ContainmentH2RegLimit,  // containment H₂ ≥ 10 vol% — 10 CFR 50.44(c)(2) not-to-exceed exceeded
    ContainmentH2Detonable, // containment H₂ in the 13–65 vol% detonable band — DDT-credible, containment threat
    IgnitersEnergized,      // Distributed Ignition System armed and powered (glow plugs hot) — status annunciator
    ContainmentDeflagration,// a containment hydrogen burn has occurred (latched event)
    RodEjectionAccident,    // RIA/REA (Ch 15.4.8) in progress — a CRDM has failed and ejected an RCCA
    FuelEnthalpyLimit,      // peak radial-average fuel enthalpy ≥ the RG 1.236 coolability limit (230 cal/g)
    RiaCladFailure,         // RIA fuel-rod failure (PCMI enthalpy-rise threshold, or DNB at power)
    RcsDeI131LcoExceeded,   // RCS Dose-Equiv I-131 > 1.0 µCi/g (LCO 3.4.16 steady-state)
    RcsDeI131SpikeLimit,    // RCS Dose-Equiv I-131 > 60 µCi/g (transient / iodine-spike limit)
    RcsDeXe133LcoExceeded,  // RCS Dose-Equiv Xe-133 > 280 µCi/g (noble-gas LCO 3.4.16)
    IodineSpikeInProgress,  // an 8-h concurrent iodine spike (RG 1.183) is active
    BoronDilution,          // uncontrolled boron dilution (FSAR 15.4.6) — source-range count-rate rising while subcritical
    BoronDilutionActionWindow, // dilution time-to-loss-of-SDM has fallen below the 15-min operator-action criterion
    BoricAcidPrecipApproach,   // post-LOCA core boric-acid conc within 90% of the solubility limit — establish hot-leg recirc (ES-1.4)
    BoricAcidPrecipitated,     // core boric acid has reached the solubility limit — crystals deposit on fuel (latched; long-term-cooling failure)
    RcsUnidentifiedLeakHi,     // RCS unidentified LEAKAGE > 1 gpm (LCO 3.4.13.b) — restore ≤ limit in 4 h, else MODE 3 in 6 h / MODE 5 in 36 h
    RcsIdentifiedLeakHi,       // RCS identified LEAKAGE > 10 gpm (LCO 3.4.13.c) — restore ≤ limit in 4 h, else MODE 3 in 6 h / MODE 5 in 36 h
    RcsPressureBoundaryLeak,   // any RCS pressure-boundary LEAKAGE (LCO 3.4.13.a) — ZERO allowed; immediate MODE 3 in 6 h / MODE 5 in 36 h
    ContainmentParticulateRadHi, // containment-atmosphere particulate (I-131) radioactivity monitor hi (RG 1.45 / LCO 3.4.15)
    ContainmentGaseousRadHi,   // containment-atmosphere gaseous (Xe-133 noble-gas) radioactivity monitor hi (RG 1.45 / LCO 3.4.15)
    RcpLockedRotor,            // RCP rotor seizure / locked rotor (FSAR 15.3.3) — one loop flow lost instantly
    RodsInDnbHi,               // predicted fuel rods in DNB exceeds the ~5% locked-rotor acceptance fraction
}

/// <summary>
/// RVLIS（反應堆壓力容器水位儀表系統）有效量程 · Which RVLIS range is trustworthy this tick.
/// Post-TMI (NUREG-0737 II.F.2) instrument: the reactor-coolant-pump state is the discriminator.
/// All RCPs OFF (natural circulation) → the collapsed-level ranges (Full + Upper) read true vessel
/// level; any RCP ON → the sensed ΔP column is dominated by pump head, so ONLY the dynamic-head
/// (pump-ΔP) range is meaningful and the level ranges read off-scale and must be disregarded.
/// </summary>
public enum RvlisValidRange { FullRange, DynamicHead }

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
    private const double SamariumWorthFull = -0.0064; // dk/k at equilibrium full-power samarium (~ -640 pcm)

    // Rod worth: total worth of all banks fully inserted (dk/k). Banks share this.
    private const double TotalRodWorth = 0.080; // 8000 pcm fully inserted

    // ---- Rod Ejection Accident (RIA / REA, FSAR Ch 15.4.8) ----
    // A failed CRDM housing lets RCS pressure (~2250 psia) expel one RCCA + drive shaft in ~0.1 s. The
    // step of positive reactivity drives a super-prompt-critical excursion at HZP (worth > β); the pulse is
    // turned over in tens of ms by PROMPT Doppler, NOT by the scram. Bounding worths per Westinghouse FSAR
    // 15.4.8 / NRC W-6382: HZP ~0.75 %Δk/k (deepest insertion, weakest Doppler), HFP ~0.20 %Δk/k.
    private const double RiaHzpWorthPcm   = 750.0;  // bounding hot-zero-power ejected single-RCCA worth (pcm)
    private const double RiaHfpWorthPcm   = 225.0;  // hot-full-power ejected worth (pcm)
    private const double RiaEjectTimeSec  = 0.10;   // mechanical ejection time (linear worth ramp, s)
    private const double RiaMicroDt       = 0.001;  // 1 ms inner sub-step to resolve the ~45 ms prompt pulse
    // Hot-pellet enthalpy node (the RIA figure of merit). Core-average UO₂ specific power 3411 MWth / 89 t ≈
    // 38 W/g; total nuclear peaking F_Q ≈ 2.5 → hot-pellet ≈ 95 W/g. Pellet→coolant time constant ≈ 5.5 s
    // (adiabatic on the pulse timescale — exactly why prompt Doppler must come from THIS node, not FuelTemp).
    private const double FuelSpecificPowerWg = 38.0;   // W/g UO₂, core average at rated power
    private const double RiaFqHotPellet      = 2.5;    // total nuclear hot-channel peaking factor F_Q
    private const double FuelEnthalpyTau     = 5.5;    // s, hot-pellet → coolant thermal time constant
    private const double CalPerJoule         = 1.0 / 4.1868; // 1 J = 0.2389 cal
    // UO₂ enthalpy fit H[cal/g] = a·T + b·T² (Fink 2000; T °C from ~25 °C). 256 cal/g @1000 °C, 1012 @melt.
    private const double Uo2EnthA = 0.20262;
    private const double Uo2EnthB = 5.3665e-5;
    // RIA acceptance limits on peak radial-average fuel enthalpy / enthalpy rise.
    private const double RiaCoolabilityCalG       = 230.0; // RG 1.236 (2021) low-burnup coolability / no-melt
    private const double RiaLegacyCoolabilityCalG = 280.0; // RG 1.77 (1974) fresh-fuel reference / incipient melt
    private const double RiaPcmiRiseFreshCalG     = 150.0; // PCMI clad-failure enthalpy RISE at ~zero excess H
    private const double RiaPcmiRiseHighBurnCalG  = 60.0;  // PCMI rise threshold near 68 GWd/MTU (high burnup)

    /// <summary>UO₂ 焓擬合（cal/g，由 ~25 °C 起）· UO₂ enthalpy above cold, cal/g.</summary>
    private static double Uo2Enthalpy(double tc)
    {
        if (tc < 0) tc = 0;
        return Uo2EnthA * tc + Uo2EnthB * tc * tc;
    }

    /// <summary>焓反推溫度（°C）· Invert the UO₂ enthalpy fit (quadratic solve).</summary>
    private static double Uo2EnthalpyToTemp(double hcal)
    {
        if (hcal <= 0) return 0;
        return (-Uo2EnthA + Math.Sqrt(Uo2EnthA * Uo2EnthA + 4.0 * Uo2EnthB * hcal)) / (2.0 * Uo2EnthB);
    }

    /// <summary>熱芯塊體積發熱率（cal/g·s）· Hot-pellet volumetric heat-deposition rate, cal/(g·s).</summary>
    private double RiaQHot(double powerFrac) =>
        FuelSpecificPowerWg * RiaFqHotPellet * Math.Max(0.0, powerFrac) * CalPerJoule;

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

    // --- Westinghouse automatic rod-control: Tavg/Tref program (NRC Tech Manual §8.1, ML11223A252) ---
    // AUTO rods regulate Tavg to a turbine-load-programmed reference Tref, NOT to a power setpoint. Tref is
    // linear in turbine first-stage (impulse) pressure between a no-load and a full-load endpoint; the
    // canonical span is 557→584.7 °F = 15.4 °C. We anchor full load at the existing 305 °C datum so the
    // OTΔT/OPΔT and criticality baselines are undisturbed, and back off the 15.4 °C span for no-load.
    private const double NoLoadTavg       = 289.6;  // °C — 0 % load Tref endpoint (305 − 15.4 span)
    private const double FullLoadTavg     = 305.0;  // °C — 100 % load Tref endpoint (= NominalTavg)
    // Temperature-error deadband: ±1.5 °F held about Tref. This is a ΔT-span conversion (1.5/1.8), not
    // an absolute-temperature conversion → ±0.833 °C. No rod motion inside.
    private const double RodDeadbandC     = 0.833;  // °C
    private const double RodRampStartC    = 1.667;  // °C — 3 °F, end of min-speed / start of proportional ramp
    private const double RodRampEndC      = 2.778;  // °C — 5 °F, max-speed error
    private const double RodSpeedMinSpm   = 8.0;    // steps/min — lockup/minimum
    private const double RodSpeedMaxSpm   = 72.0;   // steps/min — maximum (drive-mechanism limit)
    // Proportional-region slope: (72−8)/(2.778−1.667) = 57.6 spm per °C.
    private const double RodSpeedSlopeSpm = 57.6;   // steps/min per °C
    // Power-mismatch anticipatory gain: equivalent °C per unit (turbine load − nuclear power). A full
    // 100 % load/power mismatch reads as 3 °C of error (max speed). Lumped approximation of the manual's
    // nonlinear ~0.3 °F/% rate-comparator gain; self-decays as _power re-tracks the turbine load.
    private const double PowerMismatchGainC = 3.0;  // °C per unit mismatch

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

    // Uncontrolled boron dilution accident (FSAR Ch 15.4.6, ANS Condition II). Unborated reactor-makeup
    // water (RMW) is injected into the well-mixed RCS at a fixed charging-pump flow, so soluble boron
    // decays exponentially: C(t)=C0·exp(−(Q/V)t). The positive reactivity erodes shutdown margin toward
    // criticality. The licensing acceptance criterion is the operator-action time: the source-range
    // count-rate alarm must annunciate ≥15 min (Modes 1–5) / ≥30 min (Mode 6) before total loss of SDM.
    private const double DilutionFlowDefaultGpm  = 150.0;   // single charging/RMW pump, unborated water (gpm)
    private const double RcsMixVolumeGal         = 80000.0; // RCS+PZR active mixing volume (gal); τ=V/Q≈533 min
    private const double RequiredSdmPcm          = 1300.0;  // Tech-Spec minimum shutdown margin (pcm, 1.3% Δk/k)
    private const double DilutionActionWindowSec = 900.0;   // SRP 15.4.6: ≥15 min alarm→loss of SDM (Modes 1–5)

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

    // ---- Reactor-vessel Pressure-Temperature (P/T) operating limits — 10 CFR 50 Appendix G ----
    // The beltline shell of an irradiated RPV is brittle when cold: a flaw can propagate by fast fracture if
    // pressure (membrane stress) is applied below the material's ductile-brittle transition. Appendix G (via
    // ASME Section XI Appendix G) bounds the allowable RCS pressure as a function of the indicated coolant
    // temperature, using the reference fracture-toughness curve
    //     K_IC = 33.2 + 20.734·exp(0.02·(T − RT_NDT))   [ksi·√in, T & RT_NDT in °F]   (App G eq. G-2210)
    // with a safety factor of 2.0 on the pressure (membrane) stress intensity for normal heatup/cooldown
    // (Service Level A/B) and 1.5 for an inservice leak/hydrotest. As the vessel embrittles over life the
    // adjusted reference temperature (ART = RT_NDT + ΔRT_NDT + margin, per Reg. Guide 1.99 Rev.2) shifts the
    // whole curve to the right. Rather than re-evaluate the parameter-sensitive closed form every tick, we
    // store a representative, monotone composite *heatup* limit as a (°C → MPa-abs) table and interpolate it.
    // VALUES ARE REPRESENTATIVE / GENERIC for an aged 4-loop vessel (~82 °C / 180 °F ART) — NOT a plant PTLR.
    // Anchor (°F,psig) points converted with the file's factor MPa_abs = (psig + 14.7) / 145.038.
    private static readonly double[] PtTempC  = { 15.6, 37.8, 65.6, 93.3, 121.1, 148.9, 291.0 }; // °C bulk Tcold knots
    private static readonly double[] PtPmaxMPa = { 4.38, 4.93, 6.31, 9.07, 13.90, 17.24, 17.24 }; // MPa-abs allowable (flattens at the 17.24 MPa design ceiling at/above the knee)
    public  const double AppGRtndtF        = 60.0;    // °F  — representative mid-life adjusted RT_NDT (provenance for the table)
    public  const double KicFloorKsi       = 33.2;    // ksi·√in — exact ASME XI App G K_IC floor (eq. G-2210)
    public  const double KicCoeffB         = 20.734;  // ksi·√in — exact App G K_IC coefficient
    public  const double KicExpCoeff       = 0.02;    // /°F     — exact App G K_IC exponent coefficient
    public  const double AppGSfNormal      = 2.0;     // membrane-stress safety factor, normal heatup/cooldown (Level A/B)
    public  const double AppGSfHydroTest   = 1.5;     // membrane-stress safety factor, inservice leak / hydrotest
    public  const double CoreCriticalMarginC = 22.2;  // °C (= +40 °F) extra margin above the P/T-limit temp required for criticality (App G Table 1)
    private const double MinBoltupTempC    = 18.0;    // °C (~65 °F) minimum flange-boltup / pressurization-enable temperature
    private const double PtApproachWarnMPa = 1.0;     // MPa — warn when PrimaryPressure is within this of the App G allowable
    // ---- Low-Temperature Overpressure Protection (LTOP) / Cold Overpressure Mitigation System (COMS) ----
    // Below the LTOP enable temperature the App G allowable pressure is low, so a mass-input (inadvertent
    // SI/charging) or heat-input (starting an RCP with the SG hotter than the primary) transient can breach
    // the brittle-fracture limit in seconds. LTOP re-ranges the pressurizer PORVs to a low cold setpoint so
    // they relieve well below the App G limit at the enable temperature. The open setpoint must satisfy
    // P_set + overshoot + instrument uncertainty ≤ P_AppG(T_enable); here 3.10 MPa ≪ ~15.6 MPa allowable at 135 °C.
    private const double LtopEnableTempC     = 135.0; // °C (~275 °F) bulk-Tcold arm threshold (≈ max(App G transition, RT_NDT+50 °F))
    private const double LtopEnableHystC     = 5.0;   // °C — disarm only above enable+hyst to stop boundary chatter
    private const double LtopOpenPressureMPa = 3.10;  // MPa-abs (~435 psig) LTOP PORV lift setpoint while armed
    private const double LtopCloseHystMPa    = 0.21;  // MPa (~30 psi) blowdown → reseat at 2.89 MPa (strictly < open)
    // ---- App G heatup/cooldown rate limit + signed-rate filter ----
    private const double AppGRateLimitCperHr = 55.56; // °C/hr (= 100 °F/hr) Appendix-G heatup/cooldown rate limit
    private const double RateAlarmFraction   = 0.9;   // alarm at 90% of the rate limit (|rate| > 50 °C/hr)
    private const double RateFilterTauSec    = 45.0;  // s — single-pole EMA time constant for the displayed rate
    private const double RateSampleMinDt     = 0.05;  // s — guard the finite-difference divide below this dt

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
    public double SamariumReactivityPcm { get; private set; }

    // ---- Uncontrolled boron dilution (FSAR 15.4.6) readouts ----
    /// <summary>稀釋流量 · Unborated reactor-makeup-water flow into the RCS during a dilution event (gpm); 0 if inactive.</summary>
    public double DilutionFlowGpm => _dilutionActive ? _dilutionFlowGpm : 0.0;

    /// <summary>停堆裕度 · Current shutdown margin (pcm) — how far subcritical; 0 once critical/supercritical.</summary>
    public double ShutdownMarginPcm => Math.Max(0.0, -ReactivityPcm);

    /// <summary>
    /// 距臨界時間 · Closed-form seconds to criticality under the active dilution, from the LIVE reactivity
    /// margin and exponential boron decay: t = −(1/k)·ln(1 + ρ_now/(−α_B·C)). Doppler/MTC/xenon fold in via
    /// ReactivityPcm. +∞ if no dilution, or if the boron inventory can never reach criticality; 0 if already critical.
    /// </summary>
    public double TimeToCriticalitySeconds
    {
        get
        {
            if (!_dilutionActive || _dilutionFlowGpm <= 0.0) return double.PositiveInfinity;
            double rhoNow = ReactivityPcm / 1e5;
            if (rhoNow >= 0.0) return 0.0;
            double k   = (_dilutionFlowGpm / RcsMixVolumeGal) / 60.0;     // 1/s
            double arg = 1.0 + rhoNow / (-BoronWorth * BoronPpm);
            if (arg <= 0.0) return double.PositiveInfinity;              // not enough boron worth left to ever go critical
            return -Math.Log(arg) / k;
        }
    }
    public double TimeToCriticalityMinutes => TimeToCriticalitySeconds / 60.0;

    /// <summary>操作裕度時間 · Seconds of margin against the 15-min operator-action criterion (negative = violated).</summary>
    public double DilutionActionMarginSeconds => TimeToCriticalitySeconds - DilutionActionWindowSec;
    public bool   DilutionActionWindowViolated => _dilutionActive && DilutionActionMarginSeconds < 0.0;

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

    // ---- App G P/T limits + LTOP state (set once per outer tick by UpdatePtLimits) ----
    private double _prevTcoldForRate = ColdTemp;     // last tick's Tcold, for the heatup/cooldown finite difference
    private bool   _rateInit;                         // seeds the rate signal on the first sample (no startup spike)
    /// <summary>App G 容許 RCS 壓力（MPa-abs，按目前 Tcold）· App G allowable RCS pressure at the current Tcold (MPa-abs), cached per tick.</summary>
    public double MaxAllowablePressureMPa { get; private set; } = 4.38;
    /// <summary>距離脆性斷裂 P/T 限值嘅裕量（負值＝越限）· Signed margin to the brittle-fracture limit; negative = violation.</summary>
    public double PtMarginMPa => MaxAllowablePressureMPa - PrimaryPressure;
    /// <summary>RCS 壓力已越過 App G P/T 限值 · RCS pressure has crossed the App G P/T limit.</summary>
    public bool   PtViolation => PrimaryPressure > MaxAllowablePressureMPa;
    /// <summary>EMA 平滑後嘅 RCS 升/降溫率（°C/hr，正＝升溫）· EMA-filtered signed RCS heatup(+)/cooldown(−) rate, °C/hr.</summary>
    public double RcsRateCperHr { get; private set; }
    /// <summary>同一升降溫率以 °F/hr 表示 · The same rate expressed in °F/hr (US convention).</summary>
    public double RcsRateFperHr => RcsRateCperHr * 1.8;
    /// <summary>LTOP/COMS 已致動（Tcold 低於啟用溫度）· LTOP/COMS armed (Tcold below the enable temperature).</summary>
    public bool   LtopArmed { get; private set; }
    /// <summary>LTOP 低整定 PORV 正在洩放 · The LTOP-armed low-setpoint PORV path is actively relieving.</summary>
    public bool   LtopPorvOpen { get; private set; }

    /// <summary>有幾多個穩壓器規範安全閥正在開啟（0–3）· How many of the 3 code safety valves are currently popped.</summary>
    public int PzrCodeSafetiesOpen { get { int n = 0; for (int i = 0; i < 3; i++) if (_pzrSafetyOpen[i]) n++; return n; } }
    public bool AnyPzrCodeSafetyOpen => PzrCodeSafetiesOpen > 0;
    /// <summary>規範安全閥每次起跳的上升沿事件 · Rising-edge event each time a code safety valve pops (for annunciation/audio).</summary>
    public event Action? PzrCodeSafetyLifted;

    // Poisons
    public double Iodine { get; private set; }   // I-135 concentration (normalized)
    public double Xenon { get; private set; }    // Xe-135 concentration (normalized)
    // 釤-149／鉕-149 第二毒物對 · Sm-149 / Pm-149 — the second fission-product poison pair after xenon.
    // Pm-149 (t½≈53.1 h) is produced ∝ power and β-decays into Sm-149, which is STABLE and removed only by
    // neutron burnout (∝ flux). At equilibrium full power Samarium≈1 (worth ≈ −640 pcm) and, because the
    // production and burnout flux terms cancel, that worth is FLUX-INDEPENDENT. After a trip there is no
    // peak-and-decay: leftover Pm-149 keeps converting to Sm with nothing to burn it out, so samarium builds
    // monotonically to a permanent higher plateau (~2.84 normalized, ~ −1816 pcm) over ~10–13 days.
    private double _pm;                           // Pm-149 concentration (normalized)
    private double _sm;                           // Sm-149 concentration (normalized)
    public double Promethium => _pm;              // display
    public double Samarium => _sm;                // display

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

    // Uncontrolled boron dilution (FSAR 15.4.6) state. While active, UpdateScenarios is the SOLE writer of
    // BoronPpm (the normal charging/dilution ramp in UpdateBoron is gated off) so there is one author.
    private bool   _dilutionActive;
    private double _dilutionFlowGpm;
    private double _dilutionCpsRef = 1.0;   // source-range count-rate datum captured at event onset (flux-doubling alarm)
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
    public double AutoPowerSetpoint { get; set; } = 1.0;  // fraction of rated thermal power (manual/legacy target)
    // Westinghouse Tavg/Tref auto rod-control telemetry (read-only display)
    public double Tref { get; private set; } = NoLoadTavg;     // °C, load-programmed reference temperature
    public double TavgTrefError { get; private set; }          // °C, Tavg − Tref (positive = too hot)
    public double RodSpeedDemandSpm { get; private set; }      // steps/min commanded by the speed program (signed: + = withdraw)

    // Derived flow
    public double CoolantFlowFraction { get; private set; } // 0..1 actual primary flow
    // Per-loop flow contribution (0..RcpLoopShare each) — carries the hyperbolic coastdown of a tripped pump.
    private readonly double[] _rcpFlow = new double[4];
    // Locked-rotor (FSAR 15.3.3): index of the seized loop, pinned to ~0 flow each tick with NO flywheel
    // coastdown (the impeller is mechanically locked). -1 = no seized loop. The other three loops are untouched.
    private int _lockedRotorLoop = -1;
    public bool RcpLockedRotor => _lockedRotorLoop >= 0;     // a loop is currently seized (15.3.3)
    public int  LockedRotorLoop => _lockedRotorLoop;         // which loop (0-based), -1 if none
    public double PumpedFlowFraction { get; private set; }   // 0..1 forced (pumped) component of flow
    public double NaturalCircFraction { get; private set; }  // 0..1 buoyancy-thermosiphon floor this tick
    public bool RcpCoasting { get; private set; }            // a stopped pump is still carrying inertial flow
    public bool OnNaturalCirc { get; private set; }          // buoyancy floor is governing core flow (pumps gone)

    // --- RCP seal package (WOG-2000 seal-LOCA on loss of all seal cooling) ---
    // Each of the 4 reactor-coolant-pump shafts has a 3-stage film-riding face-seal package. Normally the
    // No.1 seal takes the full ~2235 psia drop with a small controlled bleed-off (~3 gpm/pump) that charging
    // makes up. Cooling is removed two ways: CCW to the thermal-barrier heat exchanger, and high-head charging
    // seal-injection down the shaft. Lose BOTH (the defining station-blackout condition — no AC bus) and the
    // stagnant seal water heats toward the hot-leg, the elastomer O-rings degrade, and per-pump leakoff escalates
    // through the canonical WOG-2000 bins (21→76→182→480 gpm/pump). At the gross-failure bin a 4-loop plant sheds
    // ~1920 gpm — a ~2-inch small-break LOCA that the existing PrimaryDeficitPct path turns into core uncovery.
    // Degradation is monotonically latched: extruded O-rings do NOT reseat when cooling is restored, so restoring
    // cooling before the cavity reaches a higher bin is the only thing that arrests the climb.
    private readonly double[] _sealCavityTempC = { SealCooledTempC, SealCooledTempC, SealCooledTempC, SealCooledTempC };
    private readonly double[] _sealLeakGpm     = new double[4];
    private readonly double[] _sealIntegrity   = { 1.0, 1.0, 1.0, 1.0 }; // 1 = intact; only ever decreases (latched)
    private bool _sealCoolingFailed;                          // scenario latch: loss of all seal cooling injected directly
    public double SealLeakGpmTotal   { get; private set; }   // Σ per-pump leakoff, gpm (display + deficit driver)
    public double SealCavityMaxTempC { get; private set; } = SealCooledTempC; // hottest seal cavity, °C (display)
    public bool   SealCoolingAvailable { get; private set; } = true; // either seal-cooling path alive (display)

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

    // ----------------------------------------------------------------- LOCA cladding / 50.46 ----
    // 堆芯裸露 → 峰值包殼溫度（PCT）連 10 CFR 50.46 驗收準則 · Core-uncovery Peak-Cladding-Temperature model
    // with Zircaloy–steam oxidation, scored against the three QUANTITATIVE 10 CFR 50.46(b) ECCS acceptance
    // criteria: (b)(1) PCT ≤ 1204.4 °C (2200 °F), (b)(2) max local clad oxidation ≤ 17 % ECR, (b)(3)
    // core-wide H₂ ≤ 1 % of the all-clad-reacted inventory. Purely additive INSTRUMENTATION: it reads
    // post-tick inventory/thermal state and writes ONLY the properties below + four alarms. It never writes
    // FuelTemp, DamageAccumulation or MeltdownTriggered, so the meltdown-ARM path is provably unaffected.
    public double CladTempC { get; private set; } = CladColdC;       // live hottest-exposed-node clad temp (°C)
    public double PeakCladTempC { get; private set; } = CladColdC;   // max-hold PCT — the 50.46(b)(1) figure of merit
    public double PeakCladTempF => PeakCladTempC * 1.8 + 32.0;       // °F, compare against the 2200 °F limit
    public double CollapsedLevelFrac { get; private set; } = 1.0;    // collapsed mixture level over active fuel (1 = covered)
    public double CoreExposedFrac { get; private set; }              // dry fraction of the hot channel (1 − level)
    public double MaxLocalOxidationPct { get; private set; }         // local ECR — 50.46(b)(2), limit 17 %
    public double CoreWideHydrogenPct { get; private set; }          // core-wide H₂ — 50.46(b)(3), limit 1 %
    public double CladOxidationHeatMW { get; private set; }          // instantaneous Zr–steam exothermic power (display)
    public double HydrogenMassKg { get; private set; }               // integrated H₂ generated (display)
    public bool CladQuenching { get; private set; }                  // a re-covered node is rewetting / quenching
    public bool CoolableGeometryOk => PeakCladTempC < PctLimitC && MaxLocalOxidationPct < EcrLimitPct; // (b)(4), qualitative

    // ----------------------------------------- Containment combustible-gas control (10 CFR 50.44) ----
    // 安全殼可燃氣體控制（10 CFR 50.44）· Hydrogen released by Zr–steam oxidation (HydrogenMassKg above) is
    // tracked into the containment ATMOSPHERE here, converted to a vol-% concentration, and acted on by the
    // two real mitigation paths: passive auto-catalytic recombiners (PARs, always passive) and an
    // operator-armed Distributed Ignition System (glow-plug igniters, default OFF). Flammable mixtures can
    // deflagrate, adding a one-shot AICC pressure/temperature spike onto the containment state (the TMI-2
    // hydrogen-burn signature: ~8 vol% → ~28 psig spike). Purely ADDITIVE: with no H₂ present every term is
    // zero, so normal operation is provably unaffected; the only feedback writes (a burn spike onto
    // ContainmentPressureKpa/ContainmentTempC and O₂ depletion) occur only during a severe-accident burn.
    public double ContainmentH2Pct      { get; private set; }            // vol% H₂ in the containment atmosphere (mole fraction ×100)
    public double ContainmentO2Pct      { get; private set; } = 20.95;   // vol% O₂; 0.5 mol consumed per mol H₂ recombined/burned
    public double ParRemovalKgPerHr     { get; private set; }            // current passive-recombiner bank H₂ removal rate (kg/hr)
    public double SteamInertFraction    { get; private set; }            // vol% steam in containment (≥55 % inerts the mixture)
    public bool   ContainmentFlammable  { get; private set; }            // H₂ ≥ LFL (4 vol%), O₂ ≥ 5 %, not steam-inerted
    public bool   ContainmentDetonable  { get; private set; }            // H₂ in the 13–65 vol% detonable band (DDT-credible)
    public bool   DeflagrationOccurred  { get; private set; }            // latched once a hydrogen burn has happened (event)
    public double LastBurnPeakKpa       { get; private set; }            // gauge-kPa peak of the most recent deflagration spike
    public double LastBurnPeakTempC     { get; private set; }            // gas-temperature peak of the most recent burn (°C)
    public bool   IgniterSystemArmed    { get; set; }                    // operator control — Distributed Ignition System; default OFF
    public bool   IgnitersEnergized     { get; private set; }            // armed AND powered (not in SBO) — glow plugs hot
    public double IgniterSurfaceTempC   { get; private set; } = ContainmentAmbientC; // ~930 °C energized, else ambient (display)
    private double _h2AirborneKg;     // airborne (un-recombined, un-burned) H₂ inventory in containment (kg)
    private double _h2GenPrevKg;      // last-seen cumulative HydrogenMassKg, to capture per-tick generation increments
    private bool   _burnArmed = true; // re-armable one-shot latch for a spontaneous deflagration (re-arms below the LFL)

    // ----------------------------------------------------------------- RVLIS (post-TMI II.F.2) ----
    // 反應堆壓力容器水位儀表系統 · Reactor Vessel Level Instrumentation System (NUREG-0737 II.F.2 / Westinghouse).
    // A ΔP-based, RTD-density-compensated indication of reactor-vessel collapsed liquid level, added after TMI-2
    // to give operators a direct vessel-inventory readout for detecting Inadequate Core Cooling (ICC) and
    // confirming natural circulation. Purely additive INSTRUMENTATION (same contract as the cladding model):
    // StepRvlis reads post-tick inventory/pump/thermal state and writes ONLY the four properties below + two
    // advisory alarms. It NEVER writes feedback state, so the meltdown/ECCS path is provably unaffected.
    public double RvlisFullRangePct { get; private set; } = 100.0;    // % of full vessel height (bottom-of-vessel→top-of-head); ~62%=top of active fuel, ~33%=bottom. Valid RCPs OFF.
    public double RvlisDynamicHeadPct { get; private set; }           // % of all-pumps ΔP span (0=no head/voided, 100=4-pump water-solid). NOT a level. Valid ≥1 RCP ON.
    public double RvlisUpperRangePct { get; private set; } = 100.0;   // % hot-leg elevation→top of head (head-vent guidance). Valid RCPs OFF.
    public RvlisValidRange RvlisRange { get; private set; } = RvlisValidRange.DynamicHead; // pump-state validity selector

    // --- Post-LOCA boric-acid precipitation + hot-leg-recirculation switchover (long-term core cooling) ---
    //     硼酸析出與熱段再循環切換 · After a LOCA the ECCS injects borated water (RWST/sump), decay heat boils
    //     it off in the core, and the non-volatile boric acid concentrates in the core mixing region. If it
    //     reaches the temperature-dependent solubility limit it precipitates on the fuel, blocking core flow —
    //     the long-term-cooling concern of 10 CFR 50.46(b)(5)/GDC 35. The operator prevents it by transferring
    //     to HOT-leg recirculation (Westinghouse ERG ES-1.4), establishing a through-core flush that sweeps the
    //     concentrated borate out the hot legs. StepBoricAcidPrecip reads post-tick state and writes ONLY the
    //     properties below + its own two advisory alarms — never FuelTemp/PrimaryDeficitPct/MeltdownTriggered,
    //     so it is instrumentation-only and the meltdown-ARM/ECCS consequence path is provably unaffected.
    public double CoreBoronPpm { get; private set; } = NominalBoron;          // core mixing-region boric-acid conc (ppm B); distinct from the well-mixed RCS BoronPpm
    public bool   HotLegRecircActive { get; set; }                            // OPERATOR action (ES-1.4) — establish the hot-leg/simultaneous recirc flush; default OFF
    public double BoricSolubilityLimitPpm { get; private set; } = SolubFloorPpm; // Cs at the current core temperature (ppm B) — the precipitation threshold
    public double PrecipMarginPpm => BoricSolubilityLimitPpm - CoreBoronPpm;  // ppm B headroom to precipitation (negative = past the limit)
    public double TimeToPrecipSeconds { get; private set; } = double.PositiveInfinity; // closed-form s to reach Cs; +∞ if recirc winning / ceiling below limit; 0 if past
    public double TimeToPrecipHours => TimeToPrecipSeconds / 3600.0;
    public bool   Precipitated { get; private set; }                         // LATCHED — crystals deposited on fuel; reseats only on a full Reset / new scenario
    public bool   BoricConcentrationActive { get; private set; }             // true ONLY in a real LOCA long-term-cooling boil-off state (gating result, for UI dimming)

    private double _w2;     // ∫ Cathcart–Pawel weight-gain² (g²/cm⁴) — the monotone oxidation state, never reported raw
    private double _wPrev;  // previous-tick weight gain w = √_w2 (g/cm²), for the per-tick oxidation increment

    private const double CladColdC = 300.0;          // °C clad datum at power / when covered
    private const double DeficitReserve = 8.0;       // % RCS inventory deficit at which top of active fuel just uncovers
    private const double DeficitBoildry = 34.0;      // % RCS inventory deficit at which the core is fully uncovered
    private const double PctLimitC = 1204.4;         // 50.46(b)(1) — 2200 °F = (2200−32)/1.8
    private const double EcrLimitPct = 17.0;         // 50.46(b)(2)
    private const double H2LimitPct = 1.0;           // 50.46(b)(3)
    private const double CladPeakingFactor = 2.5;    // Fq total hot-channel factor, applied to the hot node only
    private const double CladHeatCapMWsPerC = 0.9;   // MW·s/°C lumped hot-clad-node heat capacity
    private const double CladSteamCoolTau = 12.0;    // s steam/radiation cooling relaxation for a dry clad node
    private const double CladQuenchTau = 0.8;        // s fast quench relaxation once the node is re-covered
    private const double QuenchCoverThresh = 0.05;   // exposed below this ⇒ node covered ⇒ quench branch
    private const double ZrOxStartTempC = 800.0;     // °C oxidation onset floor (Kp treated as 0 below)
    private const double CpRateA = 0.1811;           // Cathcart–Pawel weight-gain² pre-exponential (g²/cm⁴/s)
    private const double CpRateB = 39940.0;          // Cathcart–Pawel activation term (K): Kp = A·e^(−B/T)
    private const double BjRateA = 33.3e6;           // Baker–Just oxide-thickness² pre-exponential (cm²/s)
    private const double BjEoverR = 22897.0;         // Baker–Just E/R = 45500/1.987 (K)
    private const double BjToWg2 = 0.0444;           // BJ cm²/s → CP weight-gain² g²/cm⁴/s basis conversion
    private const double OxCrossoverTk = 1853.0;     // K Cathcart–Pawel ↔ Baker–Just blend centre (±25 K band)
    private const double ZrHeatGain = 1.30;          // °C per (g/cm² weight-gain rate) lumped exothermic gain
    private const double FullWallWeightGain = 0.063; // g/cm² O uptake to consume the full clad wall ⇒ ECR 100 %
    private const double CoreCladMassKg = 26000.0;   // total Zircaloy clad inventory (kg)
    private const double FullCoreH2Kg = CoreCladMassKg * 0.0439; // kg H₂ if every clad reacted (≈ 1141 kg)
    private const double CladTempFloorC = 140.0;     // °C clad sink floor at primary pressure
    private const double CladCeilingC = 2500.0;      // °C hard backstop clamp (above ZrO₂ melt) — never the stability mechanism
    private const double CladSubstepTriggerC = 1200.0;// °C above which StepCladding sub-steps internally
    private const double CladSubstepDt = 0.05;       // s internal sub-step in the autocatalytic regime
    private const double BreakDeficitRate = 1.2;     // %/s per unit break area — RCS inventory the break sheds
    private const double BoiloffDeficitRate = 30.0;  // (%/s)/(decay-heat fraction) saturated boil-off the SG cannot remove

    // --- Boric-acid precipitation model constants (post-LOCA long-term cooling) ---
    private const double SolubGperL25        = 50.0;      // g H₃BO₃/L at 25 °C (handbook saturation anchor)
    private const double SolubSlopeGperLperC = 3.0;       // (g/L)/°C — linear-in-T fit → ~275 g/L near 100 °C (steep solubility rise)
    private const double SolubilityClampHiC  = 150.0;     // °C — upper clamp on the linear extrapolation (data tabulated only to ~100 °C)
    private const double BoronWtFracH3BO3    = 0.175;     // B is 17.48 wt% of H₃BO₃ (10.81/61.83) — converts g/L H₃BO₃ → ppm B
    private const double SolubFloorPpm       = 20000.0;   // ppm B — floor so a cold core has a finite limit (no divide-by-near-zero in band/clock)
    private const double CoreBoronEquilibTau = 30.0;      // s — when inactive, CoreBoronPpm relaxes back to the well-mixed BoronPpm in ~30 s
    private const double CoreMixFloor        = 1.0 / 600.0;  // 1/s — baseline core↔RCS turnover (V/Q ~10 min) that caps concentration even without recirc
    private const double HotLegFlushRate     = 1.0 / 300.0; // 1/s — hot-leg recirc sweep: ~5-min e-folding flush of the core mixing region
    private const double HotLegSwitchoverTimeSec = 19800.0; // s — typical ~5.5 h plant-specific BAP-derived HLSO action time (W generic ~7 h)
    private const double PrecipWarnFracOfLimit   = 0.90;  // raise the action-window alarm at 90% of Cs (analog of the 0.9 rate-alarm fraction)

    // Core Exit Thermocouples (CET) + Subcooling Margin Monitor (SMM) — post-TMI Inadequate Core Cooling
    // instrumentation (NUREG-0737 II.F.2, Reg Guide 1.97 Category 1). Type-K (Chromel-Alumel) incore TCs,
    // ~50-65 in a Westinghouse 4-loop plant; the EOP/ERG indicator is the HIGHEST valid CET (a localized
    // uncovered region must not be masked by an averaged reading). Display-only — see StepCet's contract.
    private const double CetCeilingC     = 1260.0; // qualified Type-K span ceiling (~2300 °F)
    private const double CetCoveredBiasC = 3.0;    // CET reads a few °C above Thot when covered/subcooled (hot-channel exit > loop avg)
    private const double CetTauSec       = 6.0;    // s TC-in-thermowell response (between the 0.8 s quench and 12 s dry-clad τ)
    private const double CetDhBase       = 0.30;   // exposure-weight floor (some superheat signal even at low decay heat)
    private const double CetDhGain       = 14.0;   // decay-heat sharpening: effExposure→1 at ~5% DH (0.30 + 14·0.05 = 1.0)
    private const double IccRedTempC     = 649.0;  // ICC RED / FR-C.1 — 1200 °F core-exit TC (core damage imminent)
    private const double IccOrangeTempC  = 371.0;  // ICC ORANGE / FR-C.2 — 700 °F core-exit TC

    // RVLIS Full-Range geometry + dynamic-head / alarm calibration. The active fuel occupies a SUB-BAND of the
    // full vessel: bottom of active fuel ≈ 33 %, top of active fuel ≈ 62 % of full-range span; above 62 % is the
    // upper plenum/head. LO-LO at 40 % sits in the uncovered-fuel band (≈ the real-plant TAF setpoint band).
    private const double RvlisFuelBottomPct     = 33.0;   // Full-Range % at bottom of active fuel
    private const double RvlisFuelTopPct        = 62.0;   // Full-Range % at top of active fuel (fuel just covered)
    private const double RvlisVesselEmptyDeficit = 40.0;  // deficit (%) at Full-Range floor — equals the PrimaryDeficitPct clamp ceiling
    private const double RvlisFullRangeLoLoPct  = 40.0;   // LO-LO setpoint (uncovered-fuel band)
    private const double RvlisDhGain            = 100.0;  // 4-pump (PumpedFlowFraction=1) → 100 % dynamic head
    private const double RvlisDhSubcoolRef      = 10.0;   // °C subcooling at which dynamic head reads full; voiding collapses it
    private const double RvlisHeadSubcoolRef    = 15.0;   // °C subcooling to ramp Full-Range 62→100 in the covered/head-fill region
    private const double RvlisCoverDeadband     = 0.98;   // ICC advisory alarm: raise below this, clear only when fully covered (anti-chatter)

    // RCP seal package — WOG-2000 seal-LOCA constants. Cooled datum, heat-up/relax time constants, the four
    // cavity-temperature bin breakpoints, and the canonical per-pump leakoff bins (21/76/182/480 gpm/pump).
    // τ=900 s heating 50→Thot(~320 °C) crosses 200 °C (the 76 gpm bin) at ≈13 min — the WOG-2000 time-to-onset.
    private const double SealCooledTempC     = 50.0;   // °C cooled-seal datum (charging/CCW removing seal heat)
    private const double SealHeatupTau       = 900.0;  // s  heat-up τ toward Thot when both cooling paths are lost
    private const double SealCooldownTau     = 120.0;  // s  faster relax back to the cooled datum once cooling returns
    private const double SealBin1TempC       = 93.0;   // 200 °F — intact-but-hot floor begins (→21 gpm)
    private const double SealBin2TempC       = 200.0;  // 392 °F — →76 gpm (WOG-2000 onset, ≈13 min)
    private const double SealBin3TempC       = 260.0;  // 500 °F — popped O-ring (→182 gpm)
    private const double SealBin4TempC       = 320.0;  // ≈hot-leg — gross seal failure (→480 gpm)
    private const double SealLeakNormalGpm   = 3.0;    // controlled #1-seal bleed-off per pump (cold/cooled)
    private const double SealLeakDegradedGpm = 21.0;   // intact-but-hot floor per pump
    private const double SealLeakBin2Gpm     = 76.0;   // WOG-2000 second bin per pump
    private const double SealLeakPoppedGpm   = 182.0;  // WOG-2000 third bin per pump
    private const double SealLeakGrossGpm    = 480.0;  // gross seal LOCA per pump (~2-inch SBLOCA, bounding)
    private const double SealChargingMakeupGpm = 132.0;// one centrifugal charging pump — makes up leakoff while an AC bus lives
    private const double SealGpmToDeficitPct = 0.0004; // %/s per net gpm (calibrated: 4×480 gpm ≈ a 0.6-area SBLOCA)
    private const double SealPressBleedK     = 0.5;    // MPa/s per 1000 gpm — gentle SBLOCA depressurization

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

    // Core Exit Thermocouples (CET) — representative (highest-valid) incore core-exit temperature (°C).
    // Tracks Thot (a few °C above) when the core is covered/subcooled; superheats toward the cladding/steam
    // temperature as the core uncovers — the post-TMI primary Inadequate-Core-Cooling diagnostic.
    public double CoreExitTempC { get; private set; } = ColdTemp;
    public double CoreExitTempF => CoreExitTempC * 1.8 + 32.0;
    // Subcooling Margin Monitor (SMM): Tsat(P) − max(Thot, CET). Conservative "higher-of" reference — > 0
    // sub-cooled, 0 saturation, < 0 superheat (an ICC indication). Diverges from SubcoolingMarginC at uncovery.
    public double CetSubcoolingMarginC { get; private set; }
    // ICC critical-safety-function status (WOG ERG FR-C). ADVISORY/diagnostic only — no automatic scram/ESF.
    public bool IccRed    => CoreExitTempC >= IccRedTempC;
    public bool IccOrange => CoreExitTempC >= IccOrangeTempC || CetSubcoolingMarginC <= 0.0;
    private bool _cetInit;   // seed CoreExitTempC = Thot on the first sample (avoid a cold-start spike)

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

    // --- Steam dump (turbine bypass / condenser dump) — Westinghouse 4-loop, 40% design capacity ---
    // Dual-mode condenser steam dump: on a load rejection or turbine/reactor trip the governor valves
    // shut and the NSSS power has nowhere to go; the dump valves bypass up to 40% of full main-steam flow
    // directly to the condenser, absorbing the power-load mismatch so the plant rides out the upset WITHOUT
    // a reactor trip and below the MSSV liftpoint. Two controllers (Tavg load-rejection mode toward Tref;
    // trip-open mode toward no-load Tavg), with a P-12 low-Tavg block that cuts dumps on real overcooling.
    private const double SteamDumpCapacityFrac = 0.40;            // 40% of full main-steam flow to condenser
    private const double SdLoadRejGainC        = 0.20;            // demand per °C of (Tavg−Tref): ~5 °C → full open
    private const double SdTripOpenGainC       = 0.05;            // demand per °C of (Tavg−NoLoadTavg) in trip-open mode
    private const double SdLoTavgBlockC        = NoLoadTavg - 5.0;// 284.6 °C — P-12 low-Tavg block centre
    private const double SdLoTavgBandC         = 6.0;             // °C — smooth block band width
    private const double SdSinkGainK           = 70.0;            // sgRemoval coupling gain for the dump heat path
    private const double SdPressReliefK        = 0.40;            // pTarget relief per unit dumpFlow (< SteamPressDrawK 0.6)
    private const double SdValveTau            = 3.0;             // s — air-operated dump-valve stroke lag (~2–5 s band)

    // --- Main condenser vacuum / backpressure — lumped heat-sink model · 主凝汽器真空／背壓 ---
    // The condensing temperature tracks the circulating-water inlet plus a load-scaled CW rise and terminal
    // temperature difference (TTD); its saturation pressure plus a non-condensable air partial pressure gives
    // the absolute backpressure. Deeper vacuum (lower backpressure) raises the LP enthalpy drop and output;
    // degrading vacuum penalises output, alarms, blocks the condenser steam dump, and finally trips the
    // turbine (→ P-9 reactor trip). Heat-balance / HEI typical numbers; calibrated so full load @ 25 °C CW
    // inlet lands at ~2.0 inHgA (6.77 kPa) with an output factor of 1.000 (present 1150 MWe unchanged).
    private const double CwInletDesignC        = 25.0;   // °C   default circulating-water inlet (design point)
    private const double CwRiseFullC           = 10.0;   // °C   CW rise across condenser at full load (×loadFrac)
    private const double CondenserTtdFullC      = 4.0;    // °C   terminal ΔT (Tcond−CWout), clean tubes (×loadFrac)
    private const double CondenserTauThermalS   = 20.0;   // s    shell/tube-metal thermal lag of sat temperature
    private const double AirRemovalCapKpaPerS   = 0.10;   // kPa/s air-ejector / vacuum-pump removal authority
    private const double AirLeakBaseKpaPerS     = 0.02;   // kPa/s baseline non-condensable in-leakage
    private const double AirLeakCircLossKpaPerS = 0.08;   // kPa/s extra rise when circulating water is lost
    private const double AirPartialMaxKpa       = 95.0;   // kPa  hard clamp (→ total asymptotes to atmospheric)
    private const double KPaPerInHg             = 3.386;  // kPa per inHgA
    private const double DesignBackpressureKpa  = 6.77;   // kPa abs = 2.0 inHgA → output correction = 1.000
    private const double BackpressureK          = 0.0045; // 1/kPa exhaust-pressure correction (~-1.5 %/inHg)
    private const double BackpressureFloorKpa   = 3.39;   // kPa abs ~1.0 inHgA LP last-stage choking floor
    private const double OutputFactorMin        = 0.80;   // clamp — single term never knocks >20 % off MWe
    private const double OutputFactorMax        = 1.04;   // clamp — deep-vacuum credit capped (~+4 %)
    private const double VacAlarmInHgA          = 5.0;    // degrading-vacuum annunciator setpoint
    private const double VacAlarmClearInHgA     = 4.5;    // annunciator clear (0.5 inHgA hysteresis)
    private const double VacDumpInhibitInHgA    = 7.0;    // condenser-available drops out → steam dump blocked
    private const double VacDumpRearmInHgA      = 6.0;    // condenser-available re-arm (1.0 inHgA hysteresis)
    private const double VacTripInHgA           = 8.0;    // low-vacuum TURBINE TRIP setpoint (≥ dump inhibit)

    // --- EHC state ---
    public double LoadReference      { get; private set; } // 0..1 rate-limited internal load demand the EHC tracks
    public double GovernorValve      { get; private set; } // 0..1 ACTUAL governor-valve position (single writer: UpdateSecondary)
    public double FirstStagePressure { get; private set; } // 0..1 calibrated load signal = k * GV steam flow (impulse chamber)
    public double TurbineSpeedError  { get; private set; } = SyncRpm; // rpm, (SyncRpm − TurbineRPM) — droop + display
    public bool   TurbineTripped     { get; private set; } // latched stop-valve trip (overspeed / manual)
    private double _gvCmd;                                  // commanded GV position (pre-actuator-lag)

    // --- Steam dump state (read-only telemetry; single writer = UpdateSteamDump) ---
    public double SteamDumpDemand   { get; private set; }             // 0..1 fraction of the 40% dump capacity
    public bool   SteamDumpArmed    { get; private set; }             // arming permissive satisfied
    public string SteamDumpModeEn   { get; private set; } = "Off";    // Off|Armed|LoadReject|TripOpen|Blocked
    public bool   SteamDumpValvesOpen => SteamDumpDemand > 0.001;     // any dump flow
    public double SteamDumpPercent  => SteamDumpDemand * 100.0;       // % of capacity, for display

    // --- Main condenser vacuum / backpressure state (single writer: UpdateCondenser, except the inputs) ---
    public double CirculatingWaterInletC { get; set; } = CwInletDesignC; // °C settable boundary input (clamped [1,40])
    public double CondenserSatTempC          { get; private set; } = 39.0; // °C condensing temperature (seed at design)
    public double CondenserPressureKpa       { get; private set; } = DesignBackpressureKpa; // kPa abs backpressure
    public double CondenserPressureInHg      { get; private set; } = 2.0;  // inHgA absolute backpressure
    public double CondenserVacuumInHg        { get; private set; } = 27.9; // inHg gauge vacuum (29.92 − backpressure)
    public double CondenserVacuumOutputFactor{ get; private set; } = 1.0;  // exhaust-pressure output correction
    public double AirPartialKpa              { get; private set; } = 0.2;  // kPa non-condensable partial pressure
    public bool   CondenserVacuumLow         { get; private set; }         // degrading-vacuum annunciator (hysteresis)
    public bool   CondenserAvailable         { get; private set; } = true; // steam-dump permissive (hysteresis)
    public bool   AirEjectorLost { get; set; }   // event toggle: loss of the steam-jet air ejector / vacuum pumps
    public bool   CircWaterLost  { get; set; }   // event toggle: loss of circulating-water flow

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

    // ---- AMSAC · ATWS Mitigating System Actuation Circuitry (10 CFR 50.62, "the ATWS rule") ----
    // A DIVERSE, RPS-independent backup: armed above the C-20 power permissive, it watches only process
    // signals (power + SG level + main-feedwater state) for loss of the secondary heat sink, and after a
    // deliberate delay trips the turbine and starts auxiliary feedwater. It NEVER inserts rods (the
    // Westinghouse negative MTC provides the inherent power reduction) and NEVER reads RPS/scram state —
    // so it still mitigates an ATWS where the reactor trip itself fails. Keeps peak RCS pressure below
    // the ASME Service Level C limit (~3200 psia). All setpoints are representative; validate vs UFSAR.
    private const double AmsacArmThreshold   = 0.40;  // fraction RTP — C-20 first-stage-pressure permissive
    private const double AmsacDisarmHyst     = 0.05;  // disarm below 0.35 to prevent permissive chatter
    private const double AmsacSgLoLoPct      = 18.0;  // % narrow range — SG low-low level initiating signal
    private const double AmsacDelaySeconds   = 25.0;  // s — deliberate delay so the RPS acts first on normal trips
    public  const double AsmeLevelCLimitMPa  = 22.06; // MPa = 3200 psia — ASME Service Level C peak-pressure limit
    public bool   AmsacArmed   { get; private set; }  // C-20 permissive satisfied (armed)
    public bool   AmsacActuated { get; private set; } // one-way latch: turbine trip + AFW initiated
    public bool   AmsacDefeated { get; set; }         // operator demo switch: run ATWS WITHOUT AMSAC (default OFF)
    private double _amsacTimer;                        // counts up while the initiating condition persists
    public double PeakPrimaryPressureMpa { get; private set; }       // running peak RCS pressure since reset
    public double PeakPrimaryPressurePsig => PeakPrimaryPressureMpa * 145.038 - 14.7;
    public bool   AmsacMitigationOk => PeakPrimaryPressureMpa < AsmeLevelCLimitMPa; // stayed below ASME Level C

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

    // ===== RCS radiochemistry · 放射化學源項 (StepRadiochemistry) ==============================
    // Real first-order coolant-activity ODEs in µCi/g:  dA/dt = appearance − (λ_decay + k_removal)·A.
    // A single "fuel-defect" knob (driven by DamageAccumulation) scales every source term, so a clean
    // core sits at a benign equilibrium ~14× below the Tech-Spec limit and a damaged core climbs toward
    // it. The model is purely ADDITIVE — at clean steady state it tracks a small constant and trips
    // nothing. It REPLACES the old crude CoolantActivity heuristic and feeds the SAME normalized
    // CoolantActivity that the existing SGTR/MSLB transport reads, so secondary dose now scales with the
    // real Dose-Equivalent I-131. Sources: STS LCO 3.4.16 (NUREG-1431), ANSI/ANS-18.1, RG 1.183 App E/F.
    public double RcsDeI131uCiPerG  { get; private set; } = 0.05;   // Dose-Equiv I-131 (LCO: 1.0 steady / 60 spike)
    public double RcsDeXe133uCiPerG { get; private set; } = 30.0;   // Dose-Equiv Xe-133 noble gas (LCO: 280)
    public double N16MonitorUSvPerH { get; private set; }           // main-steam-line N-16 monitor (power-proportional)
    public double LetdownMonitorUSvPerH { get; private set; }       // CVCS letdown / process radiation monitor
    public bool   IodineSpikeActive => _spikeTimerSec > 0.0;        // an 8-h iodine spike transient is in progress
    public double IodineSpikeFactor => _spikeFactor;               // active appearance-rate multiplier (1 = none)

    private double _aI131, _aI132, _aI133, _aI134, _aI135;          // per-isotope iodine activity (µCi/g)
    private double _aXe133, _aXe135;                                // lumped noble-gas activity (µCi/g)
    private double _spikeFactor = 1.0;                              // iodine-appearance multiplier during a spike
    private double _spikeTimerSec;                                  // remaining spike duration (s)
    private double _prevPrimaryPressureMpa = 15.5;                  // for depressurization-rate spike trigger
    private bool   _prevScrammed;                                   // for reactor-trip rising-edge spike trigger
    private double _baseI131, _baseI132, _baseI133, _baseI134, _baseI135, _baseXe133, _baseXe135; // clean appearance rates

    // decay constants λ = ln2 / half-life (per second)
    private const double LamI131 = 1.0007e-6;  // 8.02 d
    private const double LamI132 = 8.371e-5;   // 2.30 h
    private const double LamI133 = 9.256e-6;   // 20.8 h
    private const double LamI134 = 2.201e-4;   // 52.5 min
    private const double LamI135 = 2.930e-5;   // 6.57 h
    private const double LamXe133 = 1.531e-6;  // 5.24 d
    private const double LamXe135 = 2.106e-5;  // 9.14 h
    // DEI-131 thyroid-CDE dose-conversion-factor ratios (relative to I-131 = 1.0)
    private const double DcfI131 = 1.00, DcfI132 = 0.029, DcfI133 = 0.21, DcfI134 = 0.0073, DcfI135 = 0.044;
    // Dose-Equiv Xe-133 noble-gas dose ratios (relative to Xe-133 = 1.0)
    private const double DcfXe133 = 1.00, DcfXe135 = 11.0;
    // letdown/purification (iodine) + degasifier (noble gas) removal (per second)
    private const double KLetdownIodine = 8.0e-6;   // ~1/(35 h) purification removal
    private const double KDegasNoble    = 4.0e-5;   // ~1/(7 h)  degasifier + letdown
    // fuel-defect source scaling: clean → design-basis 1% failed fuel
    private const double DefectClean = 1.0, DefectFailed = 20.0;
    // iodine-spike factors + Tech-Spec limits
    private const double SpikeFactorSgtr = 335.0;   // RG 1.183 App E concurrent SGTR spike
    private const double SpikeFactorMslb = 500.0;   // RG 1.183 App F concurrent MSLB spike
    private const double SpikeFactorTrip = 335.0;   // generic reactor-trip iodine spike
    private const double SpikeDurationSec = 8.0 * 3600.0;        // 8 h sustained
    private const double DepressSpikeRateMpaPerS = 0.10;         // |dP/dt| above this ⇒ depressurization spike
    public  const double LcoDeI131SteadyLimit = 1.0;    // µCi/g  LCO 3.4.16 steady-state
    public  const double LcoDeI131SpikeLimit  = 60.0;   // µCi/g  transient/spiking limit
    public  const double LcoDeXe133Limit      = 280.0;  // µCi/g  noble-gas LCO 3.4.16
    private const double N16FullPowerUSvPerH  = 4.0e4;  // MSL N-16 monitor reading at 100% power

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

    // ===== RCS Leakage Detection · 反應堆冷卻劑系統洩漏偵測 (StepLeakDetection) =================
    // Operational LEAKAGE accounting + RG 1.45 leak-detection instrumentation, per STS LCO 3.4.13
    // ("RCS Operational LEAKAGE", NUREG-1431) and LCO 3.4.15 ("RCS Leakage Detection Instrumentation").
    // Three Tech-Spec categories are tracked: PRESSURE-BOUNDARY LEAKAGE (a non-isolable RCPB fault —
    // NONE allowed), UNIDENTIFIED LEAKAGE (uncollected, reaches the containment floor/sump — 1 gpm limit),
    // and IDENTIFIED LEAKAGE (a known, collected source — 10 gpm limit). RG 1.45 requires DIVERSE
    // detection: a containment-sump level/flow channel (must sense 1 gpm within 1 h) plus containment-
    // atmosphere PARTICULATE (I-131) and GASEOUS (Xe-133 noble-gas) radioactivity monitors whose response
    // scales with the live RCS specific activity (the DEI-131/Xe-133 source term already modelled). A slow
    // RCS water-inventory balance reproduces the SR 3.4.13.1 surveillance estimate. Identified LEAKAGE here
    // is the RCP-seal leakoff ABOVE the normal recovered #1-seal bleed-off (so a degraded/failed seal feeds
    // it); primary-to-secondary leakage is a separate TS category detected by the existing N-16/secondary
    // monitors and is intentionally excluded. The feature is QUIESCENT by default (all inputs zero/off).
    private const double UnidLeakLimitGpm   = 1.0;     // LCO 3.4.13.b — unidentified LEAKAGE limit (gpm)
    private const double IdentLeakLimitGpm  = 10.0;    // LCO 3.4.13.c — identified LEAKAGE limit (gpm)
    private const double PbLeakLimitGpm     = 0.0;     // LCO 3.4.13.a — pressure-boundary LEAKAGE: NONE allowed
    private const double PbLeakGpm          = 0.5;     // injected pressure-boundary leak when the demo toggle is on
    private const double SumpCapacityGal    = 5000.0;  // containment normal-sump hard ceiling (gal)
    private const double SumpHiSetpointGal  = 1000.0;  // sump-pump start setpoint
    private const double SumpLoSetpointGal  = 200.0;   // sump-pump stop setpoint (hysteresis, anti-chatter)
    private const double SumpPumpGpm        = 50.0;    // sump pump-down rate (gpm)
    private const double TauSumpSec         = 120.0;   // sump-level inferred-rate filter time constant (2 min)
    private const double TauInvBalanceSec   = 600.0;   // RCS water-inventory-balance filter time constant (10 min)
    private const double ParticulateGain    = 200.0;   // ratio 1.0 = setpoint: 200·0.1 gpm·0.05 µCi/g = 1.0 (on-scale at 0.1 gpm nominal)
    private const double GaseousGain        = 0.5;     // ratio 1.0 = setpoint: 0.5·0.07 gpm·30 µCi/g ≈ 1.0 (noble-gas backup channel)

    /// <summary>未辨識洩漏 · Unidentified RCS LEAKAGE (gpm) — uncollected, reaches the sump. LCO limit 1 gpm.</summary>
    public double UnidentifiedLeakGpm { get; private set; }
    /// <summary>已辨識洩漏 · Identified RCS LEAKAGE (gpm) — known/collected (degraded RCP-seal leakoff). LCO limit 10 gpm.</summary>
    public double IdentifiedLeakGpm { get; private set; }
    /// <summary>壓力邊界洩漏 · RCS pressure-boundary LEAKAGE (gpm). LCO 3.4.13.a: NONE allowed.</summary>
    public double PressureBoundaryLeakGpm { get; private set; }
    /// <summary>安全殼集水坑存量 · Integrated containment normal-sump inventory (gal).</summary>
    public double ContainmentSumpGal { get; private set; }
    /// <summary>集水坑推算洩漏率 · Leak rate inferred from the sump level-rise rate (RG 1.45 sump channel, gpm).</summary>
    public double SumpInferredLeakGpm { get; private set; }
    /// <summary>顆粒物輻射監測比 · Containment particulate (I-131) monitor (ratio; 1.0 = alarm setpoint).</summary>
    public double ParticulateMonitorRatio { get; private set; }
    /// <summary>氣體輻射監測比 · Containment gaseous (Xe-133 noble-gas) monitor (ratio; 1.0 = alarm setpoint).</summary>
    public double GaseousMonitorRatio { get; private set; }
    /// <summary>冷卻劑存量平衡推算 · SR 3.4.13.1 RCS water-inventory-balance leak-rate estimate (gpm).</summary>
    public double RcsInventoryBalanceLeakGpm { get; private set; }
    /// <summary>示範未辨識洩漏量 · Operator demo input — injected unidentified LEAKAGE (gpm). Default 0.</summary>
    public double DemoUnidentifiedLeakGpm { get; set; }
    /// <summary>壓力邊界洩漏開關 · Operator demo input — inject pressure-boundary LEAKAGE. Default off.</summary>
    public bool PressureBoundaryLeak { get; set; }
    private double _sumpPrevGal;   // previous-tick sump inventory (for the level-rate channel)
    private bool   _sumpPumpOn;    // sump-pump hysteresis latch

    // ===== Containment combustible-gas control (10 CFR 50.44) constants =====
    // Atmosphere / geometry — same large-dry containment as the pressure model above.
    private const double CtmtFreeVolM3 = 73624.0;   // 2.6×10⁶ ft³ net free volume (WTSM 5.3) — large-dry PWR
    private const double H2MolarKg     = 0.002016;  // kg/mol H₂
    private const double RGasJmolK     = 8.314;     // J/(mol·K) — ideal-gas constant
    // Flammability / detonation thresholds (vol% H₂ in air) — NUREG/CR-3468 FITS, Shapiro–Moffette.
    private const double H2LflPct      = 4.0;    // lower flammability limit (upward propagation)
    private const double H2AllDirPct   = 9.0;    // flame propagates in all directions / ~complete combustion
    private const double H2DdtPct      = 13.0;   // deflagration-to-detonation transition onset (the "14 % rule")
    private const double H2DetonHiPct  = 65.0;   // upper detonable-band edge
    private const double H2RegLimitPct = 10.0;   // 10 CFR 50.44(c)(2) not-to-exceed (uniformly distributed)
    private const double O2FloorPct    = 5.0;    // combustion impossible below ~5 vol% O₂
    private const double SteamInertPct = 55.0;   // ≥55 vol% steam → non-flammable regardless of H₂ (FITS)
    // AICC constant-volume burn spike — pressure/temperature rise per vol% H₂ burned.
    private const double AiccKpaPerVolPct   = 32.0;   // ~0.32 atm rise per vol% H₂ (lean regime)
    private const double AiccTempCPerVolPct = 78.0;   // ~78 °C gas-temp rise per vol% H₂ burned
    private const double AiccPeakKpaCap     = 710.0;  // clamp the pressure rise to ~8 atm abs (stoichiometric ceiling)
    private const double AiccPeakTempCCap   = 2227.0; // clamp gas temp to ~2500 K equilibrium AFT
    private const double DetonationMult     = 2.0;    // detonation multiplies the spike (reflected CJ pressures)
    // PAR bank (passive auto-catalytic recombiners) — AREVA FR380 class, ~5 kg/hr/unit at 4 vol%.
    private const double ParOnsetPct   = 1.0;    // catalytic onset ~1 vol% H₂ (well below the LFL)
    private const int    ParUnits      = 50;     // EPR-class installed bank (40–65 units typical)
    private const double ParKgPerHrPerVolPctPerUnit = 1.25; // per-unit slope: 1.25×4 = 5 kg/hr/unit at 4 vol%
    // (recombination enthalpy is 242 kJ/mol-H₂, water as vapour/LHV — applied via the AICC temp coefficient)
    // Distributed Ignition System (glow-plug igniters) — ice-condenser/Mark III hardware, modelled here as an option.
    private const double IgniteSetpointPct      = 6.0;   // deliberate-burn trigger, dry (5.5–7.5 vol% band)
    private const double IgniteSetpointSteamPct = 8.5;   // raised toward 8–9 vol% under high steam
    private const double IgniterSurfaceC        = 930.0; // glow-plug surface temp ~1700 °F / 1200 K
    private const double BurnDownTargetPct      = 4.1;   // controlled burn trims H₂ down to the lean limit
    private const double BurnTauSec             = 5.0;   // deflagration burn-down time constant (s)
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
        _pm = 0; _sm = 0;
        InitRadiochemistry(); // seed RCS DEI-131/Xe-133 activity states to clean-fuel equilibrium on first launch
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
        AmsacActuated = false; _amsacTimer = 0; PeakPrimaryPressureMpa = PrimaryPressure; // re-arm AMSAC / peak tally
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
        ResetCladding(); // re-arm the PCT / 50.46 tally for the new scenario
        _breakArea = 0;
        _rodsFailToInsert = false;
        _lofwTimer = 0;
        AmsacActuated = false; AmsacArmed = false; _amsacTimer = 0; PeakPrimaryPressureMpa = PrimaryPressure; // re-arm AMSAC (keep AmsacDefeated)
        _sgtrSeverity = 0;
        SgtrIsolated = false;
        PrimaryDeficitPct = 0;
        _mslbSeverity = 0;
        MslbIsolated = false;
        SiActuated = false;
        MslbBreakFlow = 0;
        _sealCoolingFailed = false;
        for (int i = 0; i < _sealCavityTempC.Length; i++) { _sealCavityTempC[i] = SealCooledTempC; _sealLeakGpm[i] = 0; _sealIntegrity[i] = 1.0; }
        SealLeakGpmTotal = 0; SealCavityMaxTempC = SealCooledTempC; SealCoolingAvailable = true;
        // Clear any prior rod-ejection event; re-arm the enthalpy figure of merit from the current state.
        _riaActive = false; _ejectRamp = 0; EjectedRodWorthPcm = 0; EjectedRodReactivityPcm = 0;
        PeakFuelEnthalpyCalPerG = _hotPelletEnthalpy; PeakFuelEnthalpyRiseCalPerG = 0;
        RiaCladdingFailure = false; RiaFuelMelt = false; RiaCoolabilityViolated = false; RiaFailedRodPercent = 0;
        _dilutionActive = false; _dilutionFlowGpm = 0;
        _lockedRotorLoop = -1;     // clear any prior RCP rotor seizure (15.3.3)
        // Re-arm the boric-acid precipitation monitor for the new scenario (operator must re-elect ES-1.4).
        CoreBoronPpm = BoronPpm; HotLegRecircActive = false; Precipitated = false;
        TimeToPrecipSeconds = double.PositiveInfinity; BoricConcentrationActive = false;
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
                _pm = Math.Max(_pm, 1.8371);  // promethium reservoir (= full-power Pm equilibrium) from prior operation
                _sm = Math.Max(_sm, 1.3);     // partial samarium build-in (dead-time band) atop the xenon pit
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
            case ReactorScenario.RcpSealLoca:
                _sealCoolingFailed = true; // loss of all seal cooling (CCW thermal barrier + seal injection) with AC up
                EccsArmed = true;          // charging/HHSI available to make up — but cannot match a gross seal failure
                break;
            case ReactorScenario.RodEjection:
                // A CRDM housing fails: RCS pressure expels one RCCA + drive shaft. Worth is power-dependent —
                // HZP (deepest insertion, weakest Doppler) is the bounding, super-prompt-critical case; HFP is
                // mild. The plant state is left as-is so the operator sees the bounding case by ejecting from a
                // just-critical hot-zero-power condition. The pulse is Doppler-terminated, NOT scrammed.
                _riaActive = true; _ejectRamp = 0;
                EjectedRodWorthPcm = RiaHfpWorthPcm + (RiaHzpWorthPcm - RiaHfpWorthPcm) * (1.0 - Math.Clamp(_power, 0.0, 1.0));
                _riaEnthalpyAtTrigger = _hotPelletEnthalpy;
                PeakFuelEnthalpyCalPerG = _hotPelletEnthalpy; PeakFuelEnthalpyRiseCalPerG = 0;
                break;
            case ReactorScenario.BoronDilution:
                // A CVCS makeup-control failure leaves the reactor-makeup-water path injecting unborated water
                // at full charging-pump flow. UpdateScenarios decays BoronPpm exponentially; the positive
                // reactivity erodes shutdown margin toward criticality over a ~hours timescale. The credited
                // detection is the source-range high-flux/count-rate-doubling alarm — snapshot the datum now.
                _dilutionActive = true;
                _dilutionFlowGpm = DilutionFlowDefaultGpm;
                _dilutionCpsRef  = Math.Max(1.0, SourceRangeCps);
                break;
            case ReactorScenario.CompleteLossOfFlow:
                // FSAR 15.3.2: loss of power to all RCP buses (undervoltage/underfrequency) — every pump trips
                // together and coasts down on its flywheel. The existing per-loop coastdown carries the flow
                // (W/W0 ≈ 1/(1+t/τ): ~0.93@1s, ~0.70@5s, ~0.50@10s) down to the ~3–5% natural-circ floor. The
                // low-RCS-flow reactor trip (P-7 permissive) fires within ~1–2 s; min DNBR stays above 1.30.
                for (int i = 0; i < RcpRunning.Length; i++) RcpRunning[i] = false;
                RcpFlowDemand = 0;     // _lockedRotorLoop stays -1 → all four coast via the normal else-branch
                break;
            case ReactorScenario.LockedRotor:
                // FSAR 15.3.3: a single RCP rotor seizes instantaneously. Its loop flow collapses to ~0 in one
                // tick (no flywheel coastdown); the other three pumps keep running, so core flow steps to
                // ~3/4 rated almost at once — the fastest flow loss and the DNBR-limiting Condition-IV event.
                // Low-flow trip + scram fire normally; this is the bounding case for min DNBR / % rods in DNB.
                _lockedRotorLoop = 0;  // affected loop (0-based); other RCPs keep running — do NOT clear them
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

    // ===== Minimum DNBR (Departure-from-Nucleate-Boiling Ratio) — Westinghouse W-3 correlation =====
    // 最小偏離核態沸騰比（W-3 關聯式）· The operator's direct anti-DNB margin readout. DNBR = critical
    // heat flux / local heat flux; a value of 1.0 means the hot spot is about to enter film boiling and the
    // clad would overheat. We evaluate the Tong-1967 W-3 CHF correlation at a REPRESENTATIVE hot-channel
    // local point synthesised from the lumped state (power, flow, pressure, Thot/Tcold) — there is no
    // sub-channel mesh, so this is an engineering single-point estimate, not a DNB analysis-of-record.
    // PURE INSTRUMENTATION: ComputeDnbr writes ONLY MinDnbr/DnbrRawUnfiltered/DnbrLocalQuality + 2 alarms.
    // It is NOT a trip — the licensed anti-DNB protection stays the variable-setpoint OverTemp ΔT / OverPower
    // ΔT functions in the RPS. W-3 runs internally in English engineering units (psia, Btu/lbm, lb/hr-ft²),
    // its native form, so the constants below are English; SI inputs are converted at the boundary.
    public double MinDnbr { get; private set; } = 10.0;           // smoothed minimum DNBR, capped at DnbrCeiling
    public double DnbrRawUnfiltered { get; private set; } = 10.0; // pre-filter value (strip-chart / debug)
    public double DnbrLocalQuality { get; private set; }          // thermodynamic quality x at the DNB node
    // Predicted fraction of fuel rods in DNB (%), the locked-rotor (FSAR 15.3.3) figure of merit. The
    // licensing acceptance is < ~5–10% of rods in DNB (assumed failed for dose). This is an engineering
    // surrogate of the hot-channel DNBR distribution, not a sub-channel census: 0% while MinDnbr ≥ the 1.30
    // 95/95 limit, rising as MinDnbr falls below it. The 95/95 basis means ~the worst 5% of rods reach DNB
    // exactly at MinDnbr = 1.30, so the curve is anchored to 5% at the limit and saturates toward 100%.
    public double RodsInDnbPercent { get; private set; }

    private const double Fq_Total        = CladPeakingFactor; // 2.5  total heat-flux peaking (reuse existing Fq)
    private const double F_dH            = 1.65;     // enthalpy-rise hot-channel factor (drives local quality)
    private const double AxialDnbFrac    = 0.70;     // cumulative power fraction at the DNB-limiting elevation (~2/3 h)
    private const double QavgRatedBtu    = 0.189e6;  // Btu/hr-ft² rated core-AVERAGE surface heat flux
    private const double GRatedBtu       = 2.5e6;    // lb/hr-ft² rated hot-channel mass flux (~3460 kg/m²·s)
    private const double DeFt            = 0.044;    // ft equivalent hydraulic diameter (11.8 mm) for the W-3 De term
    private const double CpBtu           = 1.30;     // Btu/lbm-°F hot pressurized-water specific heat (enthalpy rise)
    private const double DnbrCeiling     = 10.0;     // off-scale-high cap
    private const double DnbrFilterTau   = 2.5;      // s first-order display smoothing
    private const double DnbrSafetyLimit = 1.30;     // W-3 95/95 design limit → safety-limit alarm
    private const double DnbrLowMargin   = 1.55;     // low-margin warning threshold
    private const double DnbrAlarmGate   = 0.15;     // suppress both DNBR alarms below this power fraction
    private const double MpaToPsia       = 145.038;  // MPa → psia
    private const double CToFspan        = 1.8;      // °C → °F span (no +32 offset; ΔT/enthalpy terms only)

    /// <summary>飽和液焓擬合（Btu/lbm，1000–2300 psia）· Saturated-liquid enthalpy fit.</summary>
    private static double HfPsia(double p)  { p = Math.Clamp(p, 1000, 2300); return 397.7 + 0.1769 * p - 2.066e-5 * p * p; }
    /// <summary>蒸發潛熱擬合（Btu/lbm，1000–2300 psia）· Latent heat of vaporization fit.</summary>
    private static double HfgPsia(double p) { p = Math.Clamp(p, 1000, 2300); return 730.9 - 0.2153 * p + 1.66e-5 * p * p; }

    /// <summary>最小 DNBR 計算（W-3 關聯式）· Minimum-DNBR (Tong W-3 CHF) instrument. Called once per tick from
    /// the end of UpdateNis, right after SubcoolingMarginC. Reads post-tick lumped state; writes ONLY the three
    /// DNBR telemetry properties (the alarms are raised separately in UpdateAlarms). Display-only — never a trip,
    /// never feeds back into kinetics/thermal/damage. Off-scale-high (10.0) when the core is not DNB-limited.</summary>
    private void ComputeDnbr(double dt)
    {
        // 1. Low-power / stagnant-flow guard → off-scale-high; the W-3 correlation is meaningless there
        //    (a cold or no-flow core is not DNB-limited), so never let it read a small/alarming DNBR.
        double g = Math.Max(CoolantFlowFraction, 0.0);
        if (_power < 0.05 || g < 0.02)
        {
            DnbrRawUnfiltered = DnbrCeiling;
            DnbrLocalQuality  = -0.15;
        }
        else
        {
            // 2. SI → English local conditions, clamped to the W-3 validity window (1000–2300 psia).
            double pPsia = Math.Clamp(PrimaryPressure * MpaToPsia, 1000.0, 2300.0);
            double hf    = HfPsia(pPsia);
            double hfg   = HfgPsia(pPsia);

            // 3. Inlet subcooled-liquid enthalpy (consistent with SubcoolingMarginC), local enthalpy at the
            //    DNB node, and the resulting thermodynamic quality x.
            double tSatC  = SatTempAt(PrimaryPressure);
            double hIn    = hf - CpBtu * Math.Max(0.0, (tSatC - Tcold) * CToFspan);     // subcooled inlet
            double dhCore = CpBtu * Math.Max(0.0, (Thot - Tcold) * CToFspan);           // lumped core Δh = q/ṁ
            // A quadrant power tilt (QPTR ≥ 1) is a radial peaking augmentation: it raises both the enthalpy-rise
            // hot-channel factor F_ΔH and the heat-flux factor Fq in the high-power quadrant, eroding DNBR margin.
            // No-op at QPTR = 1.00, so a symmetric core leaves the W-3 baseline exactly unchanged.
            double qptrPeak = Qptr;
            double hLocal = hIn + AxialDnbFrac * (F_dH * qptrPeak) * dhCore;              // hot-channel local Δh
            double x      = Math.Clamp((hLocal - hf) / Math.Max(hfg, 1.0), -0.15, 0.45);
            DnbrLocalQuality = x;

            // 4. Local heat flux (peaked) and hot-channel mass flux, English units.
            double qLocal = _power * (Fq_Total * qptrPeak) * QavgRatedBtu;   // Btu/hr-ft²
            double gLoc   = g * GRatedBtu;                       // lb/hr-ft²

            // 5. Tong-1967 W-3 critical heat flux (Btu/hr-ft²). exp argument clamped against overflow.
            double expArg = Math.Clamp((18.177 - 0.004129 * pPsia) * x, -30.0, 30.0);
            double term1  = (2.022 - 0.0004302 * pPsia)
                          + (0.1722 - 0.0000984 * pPsia) * Math.Exp(expArg);
            double term2  = (0.1484 - 1.596 * x + 0.1729 * x * Math.Abs(x)) * (gLoc / 1.0e6) + 1.037;
            double term3  = 1.157 - 0.869 * x;
            double term4  = 0.2664 + 0.8357 * Math.Exp(-3.151 * DeFt);   // De term ≈ 0.93, near-constant
            double term5  = 0.8258 + 0.000794 * (hf - hIn);              // inlet-subcooling correction
            double qDnb   = term1 * term2 * term3 * term4 * term5 * 1.0e6;

            // 6. DNBR = CHF / local flux, guarded and clamped to the gauge range.
            double raw = qDnb / Math.Max(qLocal, 1.0);
            DnbrRawUnfiltered = Math.Clamp(raw, 0.0, DnbrCeiling);
        }

        // 7. First-order smoothing of the final clamped value (display steadiness; this is indication, not a trip).
        double a = (dt > 1e-6) ? Math.Min(1.0, dt / DnbrFilterTau) : 1.0;
        MinDnbr += (DnbrRawUnfiltered - MinDnbr) * a;

        // 8. Predicted % of rods in DNB (locked-rotor figure of merit). Monotonic surrogate of the hot-channel
        //    DNBR distribution: zero while MinDnbr ≥ 1.30, rising linearly with the fractional deficit below it
        //    (≈0% at 1.30, ≈8% at 1.20, ≈23% at 1.00). Compared against the < ~5–10% Condition-IV acceptance.
        double deficit = Math.Clamp((DnbrSafetyLimit - MinDnbr) / DnbrSafetyLimit, 0.0, 1.0);
        RodsInDnbPercent = (_power < DnbrAlarmGate) ? 0.0 : 100.0 * deficit;
    }

    // ===== Quadrant Power Tilt Ratio (QPTR) — Tech-Spec LCO 3.2.4, ex-core power-range NIS N-41…N-44 =====
    // 象限功率傾斜比（QPTR，LCO 3.2.4）· Four ex-core power-range channels (one per core quadrant, NI-41…44),
    // each an upper+lower uncompensated ion chamber, also feed AFD/ΔI. QPTR = (max quadrant signal) / (average
    // of the four). A symmetric core reads 1.00; the LCO limit is 1.02 (≤2% azimuthal tilt) in MODE 1 > 50% RTP.
    // The dominant abnormal cause is a DROPPED full-length RCCA: that quadrant is locally power-DEPRESSED (the
    // rod is a strong local absorber, its detector reads LOW), so flux redistributes to the other three quadrants
    // (they read HIGH) → QPTR rises to ~1.03–1.10. The radial peaking augmentation raises F_ΔH, eroding DNBR
    // margin — which is exactly why QPTR is a safety-limit-protecting LCO. Pure instrumentation + one small
    // reactivity term; a no-op (QPTR=1.0) preserves the existing kinetics/thermal/DNBR baselines exactly.
    public double[] QuadrantPower => _qpd;                     // 4 normalized ex-core quadrant signals (avg ≡ 1)
    public double Qptr => Math.Max(Math.Max(_qpd[0], _qpd[1]), Math.Max(_qpd[2], _qpd[3])); // max/avg, avg held at 1
    public int DroppedRodQuadrant => _droppedRodQuad;         // 0..3 depressed quadrant index, −1 = none dropped
    public bool DroppedRodActive => _droppedRodQuad >= 0;
    public double DroppedRodReactivityPcm { get; private set; } // negative pcm currently inserted by the dropped rod
    // LCO 3.2.4 Required Action A.1 advisory: reduce THERMAL POWER ≥3% RTP for each 1% QPTR exceeds 1.00 (CT 2 h).
    public double QptrRequiredPowerReductionPct =>
        (_power > QptrApplicabilityPower && Qptr > QptrAlarmLimit) ? 3.0 * (Qptr - 1.0) * 100.0 : 0.0;
    public bool QptrOutOfLimit => _power > QptrApplicabilityPower && Qptr > QptrAlarmLimit;

    // ---- Rod Ejection Accident (RIA / REA, FSAR Ch 15.4.8) display surface ----
    public bool   RodEjectionActive => _riaActive;                     // a CRDM has ejected an RCCA (latched until reset)
    public double EjectedRodWorthPcm { get; private set; }             // bounding ejected-rod worth captured at trigger (pcm)
    public double EjectedRodReactivityPcm { get; private set; }        // current inserted ejected-rod reactivity (ramp × worth)
    public double FuelEnthalpyCalPerG => _hotPelletEnthalpy;           // live hot-pellet radial-average enthalpy (cal/g)
    public double PeakFuelEnthalpyCalPerG { get; private set; }        // max-hold ABSOLUTE peak fuel enthalpy — coolability metric
    public double PeakFuelEnthalpyRiseCalPerG { get; private set; }    // max-hold enthalpy RISE since trigger — PCMI metric
    public double RiaCoolabilityLimitCalPerG => RiaCoolabilityCalG;    // 230 cal/g (RG 1.236)
    public double RiaLegacyLimitCalPerG => RiaLegacyCoolabilityCalG;   // 280 cal/g (RG 1.77)
    public double RiaPcmiRiseLimitCalPerG { get; private set; } = RiaPcmiRiseFreshCalG; // burnup-dependent PCMI rise limit
    public bool   RiaCladdingFailure { get; private set; }             // PCMI (HZP) or DNB (HFP) fuel-rod failure
    public bool   RiaFuelMelt { get; private set; }                    // peak ≥ 280 cal/g — incipient fuel melt
    public bool   RiaCoolabilityViolated { get; private set; }         // peak ≥ 230 cal/g — coolability limit exceeded
    public int    RiaFailedRodPercent { get; private set; }            // qualitative failed-fuel estimate (% of core)

    private bool   _riaActive;                 // ejection in progress / latched event
    private double _ejectRamp;                 // 0 = seated … 1 = fully ejected (linear over RiaEjectTimeSec)
    private double _hotPelletEnthalpy;         // cal/g — adiabatic-on-pulse hot-pellet enthalpy node
    private double _hotPelletEnthalpyRef;      // cal/g — slow baseline; the prompt-Doppler term reads the excess over this
    private double _riaEnthalpyAtTrigger;      // cal/g — node value captured at ejection (datum for the enthalpy RISE)

    private readonly double[] _qpd = { 1.0, 1.0, 1.0, 1.0 }; // NW, NE, SE, SW quadrant detector signals
    private int _droppedRodQuad = -1;                        // which quadrant holds the dropped rod (−1 = none)
    private double _dropRamp;                                // 0 = withdrawn … 1 = fully dropped (free-fall ramp)
    private const double QptrAlarmLimit         = 1.02; // LCO 3.2.4 limit (≤2% azimuthal tilt)
    private const double QptrApplicabilityPower = 0.50; // MODE 1 > 50% RTP applicability (reuse the AFD/CAOC gate)
    private const double QptrTiltCoeff          = 0.08; // depressed = 1−3k·r, others = 1+k·r → QPTR = 1+k·r (1.08 @ r=1)
    private const double DroppedRodWorthPcm     = 200.0; // single full-length RCCA integral worth (HFP; range 100–800)
    private const double DropFallTau            = 1.5;  // s — rod free-fall to dashpot (realistic 1–2 s)
    private const double TiltSettleTau          = 3.0;  // s — flux-redistribution + ex-core detector settle

    /// <summary>落棒 · Command a single full-length RCCA to drop into the given core quadrant (0..3).</summary>
    public void DropRod(int quadrant) => _droppedRodQuad = Math.Clamp(quadrant, 0, 3);
    /// <summary>復位落棒 · Retrieve/re-latch the dropped rod; QPTR, tilt and the inserted reactivity decay back to nominal.</summary>
    public void RecoverDroppedRod() => _droppedRodQuad = -1;

    /// <summary>QPTR 儀表步進 · Advance the four ex-core quadrant detectors + the dropped-rod fall-ramp once per tick.
    /// Average is conserved algebraically (every target set sums to 4); a final renormalize kills round-off drift so
    /// Qptr = max/avg is exactly max(_qpd). The inserted reactivity is read from _dropRamp inside the kinetics sub-step.</summary>
    private void StepQptr(double dt)
    {
        double cmd = DroppedRodActive ? 1.0 : 0.0;
        _dropRamp += (cmd - _dropRamp) * Math.Min(1.0, dt / DropFallTau);
        _dropRamp = Math.Clamp(_dropRamp, 0.0, 1.0);
        for (int i = 0; i < 4; i++)
        {
            double target = 1.0;
            if (_droppedRodQuad >= 0)
                target = (i == _droppedRodQuad) ? 1.0 - 3.0 * QptrTiltCoeff * _dropRamp
                                                : 1.0 + QptrTiltCoeff * _dropRamp;
            _qpd[i] += (target - _qpd[i]) * Math.Min(1.0, dt / TiltSettleTau);
        }
        double sum = _qpd[0] + _qpd[1] + _qpd[2] + _qpd[3];
        if (sum > 1e-6) for (int i = 0; i < 4; i++) _qpd[i] *= 4.0 / sum; // hold avg ≡ 1 exactly
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
        // App G P/T limits + LTOP: clear so a fresh scenario starts clean with no first-tick rate spike.
        _rateInit = false; RcsRateCperHr = 0; _prevTcoldForRate = ColdTemp;
        LtopArmed = false; LtopPorvOpen = false;
        MaxAllowablePressureMPa = Lerp(PtTempC, PtPmaxMPa, ColdTemp);
        SteamPressure = 0.5; SteamGenLevel = 60; ElectricPowerMW = 0; TurbineRPM = 0;
        LoadReference = 0; GovernorValve = 0; _gvCmd = 0;
        SteamDumpDemand = 0; SteamDumpArmed = false; SteamDumpModeEn = "Off";
        CirculatingWaterInletC = CwInletDesignC; CondenserSatTempC = 39.0; AirPartialKpa = 0.2;
        CondenserPressureKpa = DesignBackpressureKpa; CondenserPressureInHg = 2.0; CondenserVacuumInHg = 27.9;
        CondenserVacuumOutputFactor = 1.0; CondenserVacuumLow = false; CondenserAvailable = true;
        AirEjectorLost = false; CircWaterLost = false;
        FirstStagePressure = 0; TurbineSpeedError = SyncRpm; TurbineTripped = false;
        IndicatedSgLevel = 60; SteamFlow = 0; FeedRegValve = 0; SgLevelSetpoint = 50;
        FeedwaterAuto = true; _iLevel = 0; _steamFlowSlow = 0; _threeElementActive = false; _fwAutoWasOn = false;
        Iodine = 0; Xenon = 0; _pm = 0; _sm = 0;
        _iodineTop = _iodineBot = _xenonTop = _xenonBot = 0;
        _axialSplit = _axialSplitTarget = 0;
        TopPowerFraction = BottomPowerFraction = 0.5;
        AxialOffsetPercent = 0; AxialFluxDifferencePercent = 0;
        for (int i = 0; i < _qpd.Length; i++) _qpd[i] = 1.0;
        _droppedRodQuad = -1; _dropRamp = 0; DroppedRodReactivityPcm = 0;
        // Rod ejection (RIA): clear the event and seed the hot-pellet enthalpy node at the cold datum.
        _riaActive = false; _ejectRamp = 0;
        EjectedRodWorthPcm = 0; EjectedRodReactivityPcm = 0;
        _hotPelletEnthalpy = _hotPelletEnthalpyRef = _riaEnthalpyAtTrigger = Uo2Enthalpy(ColdTemp);
        PeakFuelEnthalpyCalPerG = _hotPelletEnthalpy; PeakFuelEnthalpyRiseCalPerG = 0;
        RiaPcmiRiseLimitCalPerG = RiaPcmiRiseFreshCalG;
        RiaCladdingFailure = false; RiaFuelMelt = false; RiaCoolabilityViolated = false; RiaFailedRodPercent = 0;
        for (int i = 0; i < RodBankInsertion.Length; i++) RodBankInsertion[i] = 100.0;
        for (int i = 0; i < RcpRunning.Length; i++) RcpRunning[i] = false;
        Array.Clear(_rcpFlow); _lockedRotorLoop = -1;
        PumpedFlowFraction = 0; NaturalCircFraction = 0; RcpCoasting = false; OnNaturalCirc = false;
        BoronPpm = NominalBoron; TargetBoronPpm = NominalBoron;
        _dilutionActive = false; _dilutionFlowGpm = 0; _dilutionCpsRef = 1.0;
        PressurizerHeater = false; PressurizerSpray = false; RcpFlowDemand = 0;
        FeedwaterFlow = 0; TurbineLoadSetpoint = 0; GeneratorBreakerClosed = false;
        ReliefValveOpen = false; EccsArmed = false; EccsInjecting = false;
        CoolantFlowFraction = 0; DamageAccumulation = 0;
        IsScrammed = false; MeltdownTriggered = false; AutoRodControl = false;
        _autoRodWasOn = false; Tref = NoLoadTavg; TavgTrefError = 0; RodSpeedDemandSpm = 0;
        _rps.ClearLatch(); LastTripFunctionEn = ""; LastTripFunctionZh = ""; _powerRate = 0;
        Mode = ReactorMode.Shutdown;
        for (int i = 0; i < _alarms.Length; i++) _alarms[i] = false;
        Array.Clear(_decayGroup); Array.Clear(_actinide);
        DecayHeatFraction = 0; SubcoolingMarginC = 0; SourceRangeCps = SourceBaselineCps;
        CoreExitTempC = ColdTemp; CetSubcoolingMarginC = 0; _cetInit = false;
        MinDnbr = DnbrRawUnfiltered = DnbrCeiling; DnbrLocalQuality = 0; RodsInDnbPercent = 0;
        OneOverM = 1.0; StartupRateDpm = 0; BurnupMwdPerTonne = 0;
        IntermediateRangeAmps = IrBottomAmps; IntermediateRangeDecades = 0; IntermediateRangePercent = 0;
        PowerRangePercent = 0; SourceRangeEnergized = true; _p6Latched = false;
        ActiveScenario = ReactorScenario.Normal; _breakArea = 0; _rodsFailToInsert = false;
        AccumulatorInjecting = false; AuxFeedwaterRunning = false; _lofwTimer = 0;
        AmsacActuated = false; AmsacArmed = false; AmsacDefeated = false; _amsacTimer = 0; PeakPrimaryPressureMpa = PrimaryPressure;
        _elec.Reset();
        _sgtrSeverity = 0; SgtrLeakRate = 0; CoolantActivity = 0.02;
        InitRadiochemistry(); // reseed RCS DEI-131/Xe-133 activity states to clean-fuel equilibrium
        SecondaryActivity = 0; AtmosphericRelease = 0; SgReliefLifted = false; SgtrIsolated = false;
        // RCS leakage detection (LCO 3.4.13 / RG 1.45): zero all state + demo inputs (feature quiescent on reset).
        UnidentifiedLeakGpm = 0; IdentifiedLeakGpm = 0; PressureBoundaryLeakGpm = 0;
        ContainmentSumpGal = 0; SumpInferredLeakGpm = 0; ParticulateMonitorRatio = 0; GaseousMonitorRatio = 0;
        RcsInventoryBalanceLeakGpm = 0; _sumpPrevGal = 0; _sumpPumpOn = false;
        DemoUnidentifiedLeakGpm = 0; PressureBoundaryLeak = false;
        _mslbSeverity = 0; MslbIsolated = false; SiActuated = false; MslbBreakFlow = 0;
        _sealCoolingFailed = false;
        for (int i = 0; i < _sealCavityTempC.Length; i++) { _sealCavityTempC[i] = SealCooledTempC; _sealLeakGpm[i] = 0; _sealIntegrity[i] = 1.0; }
        SealLeakGpmTotal = 0; SealCavityMaxTempC = SealCooledTempC; SealCoolingAvailable = true;
        PrimaryDeficitPct = 0;
        // Boric-acid precipitation monitor back to a clean, well-mixed, no-recirc state.
        CoreBoronPpm = NominalBoron; HotLegRecircActive = false; Precipitated = false;
        BoricSolubilityLimitPpm = BoricSolubilityPpm(Thot); TimeToPrecipSeconds = double.PositiveInfinity;
        BoricConcentrationActive = false;
        ContainmentPressureKpa = 0; ContainmentTempC = ContainmentAmbientC;
        ContainmentSprayActive = false; ContainmentIsolationPhaseA = false;
        ContainmentIsolationPhaseB = false; ContainmentFanCoolers = false;
        _ctmtHi1 = _ctmtHi2 = _ctmtHi3 = false; _spraySetupTimer = 0;
        // Containment combustible-gas control (10 CFR 50.44): clear the H₂ atmosphere back to a fresh air charge.
        ContainmentH2Pct = 0; ContainmentO2Pct = 20.95; ParRemovalKgPerHr = 0; SteamInertFraction = 0;
        ContainmentFlammable = false; ContainmentDetonable = false; DeflagrationOccurred = false;
        LastBurnPeakKpa = 0; LastBurnPeakTempC = 0;
        IgniterSystemArmed = false; IgnitersEnergized = false; IgniterSurfaceTempC = ContainmentAmbientC;
        _h2AirborneKg = 0; _h2GenPrevKg = 0; _burnArmed = true;
        ResetCladding();
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
            StepRadiochemistry(dt); // keep coolant-activity / DEI-131 / dose climbing with core damage through a meltdown
            StepLeakDetection(dt);  // keep RCS-leakage detection / sump / atmosphere monitors live through a meltdown
            UpdateNis(dt);
            StepCladding(dt); // keep PCT / 50.46 instrumentation live through a meltdown
            StepRvlis(dt);    // keep RVLIS vessel-level indication live through a meltdown
            StepCet(dt);      // keep CET / subcooling-margin (ICC) indication live through a meltdown
            StepBoricAcidPrecip(dt); // keep boric-acid precipitation / hot-leg-recirc indication live through a meltdown
            StepContainmentH2(dt); // keep H₂ atmosphere / PAR / igniter / burn model live through a meltdown
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
        StepRadiochemistry(dt); // RCS DEI-131/Xe-133 source term + iodine spike + N-16 monitor; drives CoolantActivity
        StepLeakDetection(dt);  // RCS operational LEAKAGE accounting + RG 1.45 sump/particulate/gaseous detection (reads seal leak + source term)
        UpdateContainment(dt);
        // --- QPTR / dropped-rod tilt: advance the fall-ramp + quadrant detectors BEFORE the kinetics sub-step
        //     so the inserted single-rod reactivity (read from _dropRamp) is current this tick ---
        StepQptr(dt);

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

        // --- main condenser vacuum / backpressure: compute the heat-sink condition from this tick's turbine
        //     load BEFORE the steam dump (it gates on CondenserAvailable) and the secondary (grossElec scales
        //     by CondenserVacuumOutputFactor). May latch a low-vacuum turbine trip → P-9 reactor trip. ---
        UpdateCondenser(dt);

        // --- steam dump / turbine bypass (40% condenser dump): cache the dump demand BEFORE the secondary
        //     so a load rejection / turbine-or-reactor trip is ridden out without an RPS trip ---
        UpdateSteamDump(dt);

        // --- secondary plant + turbine ---
        UpdateSecondary(dt);

        // --- auto rod control ---
        if (AutoRodControl && !IsScrammed) UpdateAutoRods(dt);
        else { _autoRodWasOn = false; RodSpeedDemandSpm = 0.0; } // reset bumpless-transfer latch when AUTO is dropped

        // --- nuclear instrumentation BEFORE protection: the SR/IR/PR readings and the IR-derived P-6
        //     permissive must be current when the protection system evaluates the NIS trips this tick. ---
        UpdateNis(dt);
        UpdateProtection(dt);
        StepCladding(dt); // after protection so it reads the final post-tick inventory / ECCS / thermal state
        StepRvlis(dt);    // RVLIS reads the post-cladding CollapsedLevelFrac; advisory alarms only
        StepCet(dt);      // CET + subcooling-margin monitor reads the post-cladding clad/level state; advisory ICC alarms only
        StepBoricAcidPrecip(dt); // boric-acid precipitation monitor reads post-tick Thot/boron/saturation/SI; advisory alarms only
        StepContainmentH2(dt); // containment H₂ atmosphere / PAR / igniter / deflagration — reads the post-cladding HydrogenMassKg
        UpdatePtLimits(dt); // App G P/T brittle-fracture limit + LTOP arming (reads the final post-tick Tcold/pressure)
        UpdateRiaConsequences(); // RIA fuel-enthalpy acceptance evaluation (reads the post-tick DNBR / enthalpy peak)
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

    /// <summary>Seed the radiochemistry activity states to their analytic clean-fuel equilibrium so the sim
    /// starts at steady state (no cold-start transient). Equilibrium A_eq = S/(λ+k) is inverted to recover the
    /// per-isotope clean appearance rates from the chosen clean-fuel µCi/g values, giving DEI-131 ≈ 0.07 and
    /// Dose-Equiv Xe-133 ≈ 30 µCi/g — both well below the LCO 3.4.16 limits.</summary>
    private void InitRadiochemistry()
    {
        // Chosen clean-fuel per-isotope equilibria (µCi/g). Their DEI-131-weighted sum ≈ 0.07 (≈14× below the
        // 1.0 µCi/g LCO); the noble-gas sum ≈ 30 (≈9× below the 280 µCi/g LCO).
        double eqI131 = 0.041, eqI132 = 0.18, eqI133 = 0.085, eqI134 = 0.35, eqI135 = 0.10;
        double eqXe133 = 28.0, eqXe135 = 0.18;
        // Invert S = A_eq·(λ+k) to get clean appearance rates (DefectClean = 1).
        _baseI131 = eqI131 * (LamI131 + KLetdownIodine);
        _baseI132 = eqI132 * (LamI132 + KLetdownIodine);
        _baseI133 = eqI133 * (LamI133 + KLetdownIodine);
        _baseI134 = eqI134 * (LamI134 + KLetdownIodine);
        _baseI135 = eqI135 * (LamI135 + KLetdownIodine);
        _baseXe133 = eqXe133 * (LamXe133 + KDegasNoble);
        _baseXe135 = eqXe135 * (LamXe135 + KDegasNoble);
        // Start AT equilibrium.
        _aI131 = eqI131; _aI132 = eqI132; _aI133 = eqI133; _aI134 = eqI134; _aI135 = eqI135;
        _aXe133 = eqXe133; _aXe135 = eqXe135;
        _spikeFactor = 1.0; _spikeTimerSec = 0.0;
        _prevPrimaryPressureMpa = PrimaryPressure; _prevScrammed = IsScrammed;
        RcsDeI131uCiPerG = DcfI131*eqI131 + DcfI132*eqI132 + DcfI133*eqI133 + DcfI134*eqI134 + DcfI135*eqI135;
        RcsDeXe133uCiPerG = DcfXe133*eqXe133 + DcfXe135*eqXe135;
    }

    /// <summary>
    /// 反應堆冷卻劑放射化學源項 · RCS coolant radiochemistry source term (LCO 3.4.16 / ANS-18.1 / RG 1.183).
    /// Tracks five iodine isotopes (I-131…I-135) and two noble-gas groups (Xe-133/Xe-135) as first-order
    /// activity ODEs in µCi/g:  A ← A + (S·defect·spike − (λ + k)·A)·dt. A single fuel-defect multiplier
    /// (driven by DamageAccumulation, clean → design-basis 1% failed fuel) scales every source term. On a
    /// reactor-trip rising edge OR a rapid RCS depressurization the iodine appearance rate is multiplied by an
    /// 8-hour spike factor (335× generic/SGTR, 500× MSLB), reproducing the licensing concurrent iodine spike.
    /// The DEI-131-weighted sum drives the SAME normalized CoolantActivity the SGTR/MSLB transport already
    /// reads, so secondary dose now scales with real coolant activity. Purely additive — clean steady state
    /// holds a benign constant and trips nothing.
    /// </summary>
    private void StepRadiochemistry(double dt)
    {
        if (dt <= 0) return;

        // 1 — fuel-defect fraction: clean baseline, raised toward the 1%-failed multiplier by core damage.
        double defect = DefectClean + (DefectFailed - DefectClean) * Math.Clamp(DamageAccumulation / 50.0, 0.0, 1.0);

        // 2 — iodine-spike triggers (rising edges only): reactor trip, or fast depressurization.
        double dPdt = (_prevPrimaryPressureMpa - PrimaryPressure) / dt;   // +ve ⇒ depressurizing
        bool tripEdge    = IsScrammed && !_prevScrammed;
        bool depressEdge = dPdt > DepressSpikeRateMpaPerS;
        if ((tripEdge || depressEdge) && _spikeTimerSec <= 0.0)
        {
            _spikeFactor = _mslbSeverity > 0 ? SpikeFactorMslb
                         : _sgtrSeverity > 0 ? SpikeFactorSgtr
                         :                     SpikeFactorTrip;
            _spikeTimerSec = SpikeDurationSec;
        }
        if (_spikeTimerSec > 0.0)
        {
            _spikeTimerSec -= dt;
            if (_spikeTimerSec <= 0.0) { _spikeTimerSec = 0.0; _spikeFactor = 1.0; }
        }
        double iodineSpike = _spikeTimerSec > 0.0 ? _spikeFactor : 1.0;

        // 3 — first-order activity ODEs (explicit Euler; λ·dt ≪ 1 even for the fastest group, I-134).
        _aI131 += (_baseI131 * defect * iodineSpike - (LamI131 + KLetdownIodine) * _aI131) * dt;
        _aI132 += (_baseI132 * defect * iodineSpike - (LamI132 + KLetdownIodine) * _aI132) * dt;
        _aI133 += (_baseI133 * defect * iodineSpike - (LamI133 + KLetdownIodine) * _aI133) * dt;
        _aI134 += (_baseI134 * defect * iodineSpike - (LamI134 + KLetdownIodine) * _aI134) * dt;
        _aI135 += (_baseI135 * defect * iodineSpike - (LamI135 + KLetdownIodine) * _aI135) * dt;
        // Noble gases: not iodine-spiked, but scale with defect; Xe-135 is also fed by I-135 decay.
        _aXe133 += (_baseXe133 * defect - (LamXe133 + KDegasNoble) * _aXe133) * dt;
        _aXe135 += (_baseXe135 * defect + LamI135 * _aI135 - (LamXe135 + KDegasNoble) * _aXe135) * dt;

        _aI131 = Math.Max(0, _aI131); _aI132 = Math.Max(0, _aI132); _aI133 = Math.Max(0, _aI133);
        _aI134 = Math.Max(0, _aI134); _aI135 = Math.Max(0, _aI135);
        _aXe133 = Math.Max(0, _aXe133); _aXe135 = Math.Max(0, _aXe135);

        // 4 — dose-equivalent rollups (weighted sums of the isotope activities).
        RcsDeI131uCiPerG  = DcfI131*_aI131 + DcfI132*_aI132 + DcfI133*_aI133 + DcfI134*_aI134 + DcfI135*_aI135;
        RcsDeXe133uCiPerG = DcfXe133*_aXe133 + DcfXe135*_aXe135;

        // 5 — radiation monitors. MSL N-16 (7.13 s, power-proportional, near-instant) reads only with the
        //     primary at power; the CVCS letdown/process monitor tracks total RCS specific activity.
        N16MonitorUSvPerH = N16FullPowerUSvPerH * Math.Clamp(_power, 0.0, 1.2);
        LetdownMonitorUSvPerH = 0.5 + 8.0 * RcsDeI131uCiPerG + 0.1 * RcsDeXe133uCiPerG;

        // 6 — feed the EXISTING normalized CoolantActivity (1.0 == LCO 3.4.16 limit): the single coupling
        //     line the SGTR/MSLB secondary-transport reads.
        CoolantActivity = RcsDeI131uCiPerG / LcoDeI131SteadyLimit;

        // 7 — edge-detector memory.
        _prevPrimaryPressureMpa = PrimaryPressure;
        _prevScrammed = IsScrammed;
    }

    /// <summary>
    /// RCP 軸封冷卻喪失 → WOG-2000 軸封失水事故 · Reactor-coolant-pump seal-cooling-loss → WOG-2000 seal LOCA.
    /// Each pump's lumped seal cavity heats toward the hot-leg (first-order, τ=900 s) when BOTH seal-cooling
    /// paths are lost — CCW thermal-barrier cooling (needs a live AC bus) and charging seal-injection (needs a
    /// live AC bus). Either path keeps the cavity at the cooled datum. Per-pump leakoff steps through the
    /// canonical WOG-2000 bins by cavity temperature; degradation is Math.Min-latched so extruded O-rings never
    /// reseat. Pure instrumentation here: it only writes the seal telemetry — UpdateScenarios folds the total
    /// leak into the shared PrimaryDeficitPct accumulator (see below), so the meltdown-ARM behaviour is untouched.
    /// </summary>
    private void StepSeals(double dt)
    {
        if (dt <= 0) return;

        // Either seal-cooling path removes seal heat. CCW thermal-barrier and charging seal-injection both ride a
        // vital AC bus, so losing both is exactly "no AC bus energized" — the station-blackout condition. The
        // scenario latch forces the loss directly (loss of CCW + seal-injection alignment) with AC still up.
        bool sealInjOk = _elec.MotorEccsSiAvailable;          // charging/HHSI seal injection
        bool barrierOk = _elec.AnyAcBusEnergized;             // CCW thermal-barrier HX pump on a vital AC bus
        bool sealCoolOk = !_sealCoolingFailed && (sealInjOk || barrierOk);
        SealCoolingAvailable = sealCoolOk;

        double total = 0.0, maxT = SealCooledTempC;
        for (int i = 0; i < _sealCavityTempC.Length; i++)
        {
            // First-order relaxation (clamped factor ≤ 1 ⇒ no overshoot, no stiff exp): heat toward the hot-leg
            // when cooling is lost, relax to the cooled datum when restored. Below ~100 °C (cold/depressurized
            // plant) the cavity can never reach a failure bin, so a cold shutdown carries no seal-LOCA risk.
            double target = sealCoolOk ? SealCooledTempC : Math.Max(Thot, SealCooledTempC);
            double tau    = sealCoolOk ? SealCooldownTau : SealHeatupTau;
            _sealCavityTempC[i] += (target - _sealCavityTempC[i]) * Math.Min(1.0, dt / tau);

            double t = _sealCavityTempC[i];
            double binLeak = t >= SealBin4TempC ? SealLeakGrossGpm
                           : t >= SealBin3TempC ? SealLeakPoppedGpm
                           : t >= SealBin2TempC ? SealLeakBin2Gpm
                           : t >= SealBin1TempC ? SealLeakDegradedGpm
                           :                      SealLeakNormalGpm;

            // Monotonic degradation latch (mirrors the W² oxidation monotonicity): integrity only ever falls,
            // so once an O-ring pops the leak stays at its worst-reached level even after the cavity cools.
            double degFrac = (binLeak - SealLeakNormalGpm) / (SealLeakGrossGpm - SealLeakNormalGpm);
            _sealIntegrity[i] = Math.Min(_sealIntegrity[i], 1.0 - degFrac);
            double effLeak = SealLeakNormalGpm + (1.0 - _sealIntegrity[i]) * (SealLeakGrossGpm - SealLeakNormalGpm);

            _sealLeakGpm[i] = Math.Max(binLeak, effLeak); // current bin OR latched floor, whichever is higher
            total += _sealLeakGpm[i];
            if (_sealCavityTempC[i] > maxT) maxT = _sealCavityTempC[i];
        }
        SealLeakGpmTotal = total;
        SealCavityMaxTempC = maxT;
    }

    private void UpdateScenarios(double dt)
    {
        // Uncontrolled boron dilution (FSAR 15.4.6): unborated RMW added at fixed flow into the well-mixed
        // RCS. Boron decays by the exact solution of dC/dt = −(Q/V)·C — unconditionally stable for any dt.
        // This is the single writer of BoronPpm while active (UpdateBoron's control ramp is gated off above).
        if (_dilutionActive && _dilutionFlowGpm > 0.0)
        {
            double kPerSec = (_dilutionFlowGpm / RcsMixVolumeGal) / 60.0;   // 1/s  (gpm/gal/60 = 1/s)
            BoronPpm = Math.Max(0.0, BoronPpm * Math.Exp(-kPerSec * dt));
        }

        // LOCA: bleed primary pressure/inventory through the break.
        if (_breakArea > 0)
        {
            PrimaryPressure -= 0.9 * _breakArea * dt;
            PressurizerLevel -= 6.0 * _breakArea * dt;
            PrimaryDeficitPct += BreakDeficitRate * _breakArea * dt; // the break sheds RCS inventory → collapsed level falls
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

            // Coolant specific activity (CoolantActivity, normalized 1.0 == LCO 3.4.16 limit) is now set by
            // StepRadiochemistry from the real Dose-Equivalent I-131 ODE — including the failed-fuel rise and
            // the iodine spike a depressurizing SGTR itself triggers — so the transport below scales with it.

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
        // Saturated inadequate-core-cooling boil-off: with the SG heat sink gone and the RCS at saturation,
        // decay heat boils the inventory away → the deficit climbs even with no pipe break. This is the
        // SBO / loss-of-all-feedwater path to core uncovery (e.g. after the TDAFW battery depletes). It is
        // gated tightly (saturated + low flow + no feedwater + ECCS not making up) so normal operation,
        // ordinary trips and mitigated transients — which stay subcooled with a heat sink — never trigger it.
        if (SubcoolingMarginC <= 1.0 && DecayHeatFraction > 0.005
            && CoolantFlowFraction < 0.30 && FeedwaterFlow < 0.05 && !EccsInjecting)
            PrimaryDeficitPct += BoiloffDeficitRate * DecayHeatFraction * dt;

        // RCP seal LOCA: advance the seal cavities, then fold the leakoff into the shared deficit. The normal
        // controlled bleed-off — and, while an AC bus is alive to run a charging pump, up to one pump's makeup
        // capacity — is replaced, so only the NET leak drains inventory (no deficit creeps in during normal
        // operation, where 4×3=12 gpm is fully made up). A pressure-driven √(P/Pnom) factor self-limits the leak
        // as the RCS depressurizes (like the SGTR dP term), so the seal LOCA cannot run away below ~atmospheric.
        StepSeals(dt);
        double sealMakeupGpm = _elec.MotorEccsSiAvailable ? SealChargingMakeupGpm : 0.0;
        double sealPressFactor = Math.Clamp(Math.Sqrt(Math.Max(PrimaryPressure, 0.0) / PzrProgramPressure), 0.0, 1.1);
        double sealNetGpm = Math.Max(0.0, SealLeakGpmTotal * sealPressFactor - sealMakeupGpm);
        if (sealNetGpm > 0.0)
        {
            PrimaryDeficitPct += SealGpmToDeficitPct * sealNetGpm * dt;     // joins break/SGTR/boil-off
            PrimaryPressure   -= SealPressBleedK * (sealNetGpm / 1000.0) * dt; // gentle SBLOCA depressurization
            if (PrimaryPressure < 0.2) PrimaryPressure = 0.2;
            if (sealNetGpm > 2.0 * SealLeakPoppedGpm) EccsArmed = true;     // >364 gpm net arms SI like the break path
        }

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
        // AFW starts on the normal 60 s loss-of-MFW timer OR immediately when AMSAC actuates (diverse path,
        // bypassing the 60 s gate). Either way the pumps still need a live AC bus (MDAFW) or SG steam (TDAFW).
        AuxFeedwaterRunning = (_lofwTimer > 60.0 || AmsacActuated) && (_elec.MdafwAvailable || _elec.TdafwAvailable);
        if (AuxFeedwaterRunning) FeedwaterFlow = Math.Max(FeedwaterFlow, 0.15); // small AFW flow
    }

    /// <summary>清零包殼／50.46 計量 · Clear the cladding / 50.46 tally back to a covered, cold-clad core.</summary>
    private void ResetCladding()
    {
        CladTempC = CladColdC; PeakCladTempC = CladColdC;
        CollapsedLevelFrac = 1.0; CoreExposedFrac = 0.0;
        MaxLocalOxidationPct = 0.0; CoreWideHydrogenPct = 0.0;
        CladOxidationHeatMW = 0.0; HydrogenMassKg = 0.0; CladQuenching = false;
        _w2 = 0.0; _wPrev = 0.0;
    }

    /// <summary>
    /// 安全殼可燃氣體控制（10 CFR 50.44）· Containment combustible-gas control. Reads the cumulative Zr–steam
    /// hydrogen mass (HydrogenMassKg, set by StepCladding) into the containment ATMOSPHERE, converts it to a
    /// vol-% concentration against the live atmosphere mole count, and applies the two real mitigation paths:
    ///  • Passive Auto-catalytic Recombiners (PARs) — always passive; a slow first-order H₂ trickle removal.
    ///  • Distributed Ignition System (glow-plug igniters) — operator-armed (IgniterSystemArmed, default OFF);
    ///    when energized it deliberately burns H₂ down to the lean limit so it never reaches detonable levels.
    /// A spontaneous deflagration fires when a flammable mixture finds an ignition source (the TMI-2 ~8 vol%
    /// burn), depositing a one-shot AICC pressure/temperature spike that the existing UpdateContainment
    /// relaxation then bleeds down — reproducing the sharp-rise / minutes-decay burn transient. Purely
    /// ADDITIVE: with no H₂ present every term is identically zero. The only feedback it ever writes is the
    /// burn spike onto ContainmentPressureKpa/ContainmentTempC and O₂ depletion, both severe-accident-only.
    /// </summary>
    private void StepContainmentH2(double dt)
    {
        if (dt <= 0) return;

        // --- fold newly generated H₂ into the airborne (un-recombined, un-burned) inventory ---------
        double dGen = HydrogenMassKg - _h2GenPrevKg;
        if (dGen > 0) _h2AirborneKg += dGen;
        _h2GenPrevKg = HydrogenMassKg;
        if (_h2AirborneKg < 0) _h2AirborneKg = 0;

        // --- live atmosphere moles at the current containment T & P (fixed free volume) -------------
        double tK   = ContainmentTempC + 273.15;
        double pAbs = 101325.0 + ContainmentPressureKpa * 1000.0;        // Pa absolute
        double nAtm = pAbs * CtmtFreeVolM3 / (RGasJmolK * tK);           // mol
        double nH2  = _h2AirborneKg / H2MolarKg;                         // mol
        ContainmentH2Pct = (nAtm + nH2) > 0 ? 100.0 * nH2 / (nAtm + nH2) : 0.0;

        // --- steam-inerting proxy: a hot, pressurized building is steam-rich (LOCA/MSLB blowdown). The
        //     mixture is inerted during the steam phase and only becomes flammable as sprays/fans
        //     condense it back down — exactly the TMI-2 sequencing (burn ~10 h in, after condensation). -
        SteamInertFraction = Math.Clamp(
            (ContainmentTempC - ContainmentAmbientC) / (ContainmentPeakC - ContainmentAmbientC) * 60.0, 0.0, 60.0);
        bool inerted = SteamInertFraction >= SteamInertPct;

        // --- PAR bank: passive catalytic recombination — first-order in H₂, ∝ pressure, O₂-limited -----
        ParRemovalKgPerHr = 0.0;
        if (ContainmentH2Pct > ParOnsetPct && _h2AirborneKg > 0 && ContainmentO2Pct > 0.5)
        {
            double ramp = Math.Clamp((ContainmentH2Pct - ParOnsetPct) / 1.0, 0.0, 1.0);  // 1→2 vol% startup
            double perUnit = ParKgPerHrPerVolPctPerUnit * ContainmentH2Pct * (pAbs / 1000.0 / 150.0) * ramp;
            double o2f = Math.Clamp(ContainmentO2Pct / (1.5 * ContainmentH2Pct + 1e-9), 0.4, 1.0); // O₂ starvation
            ParRemovalKgPerHr = ParUnits * perUnit * o2f;
            double dkg = Math.Min(ParRemovalKgPerHr / 3600.0 * dt, _h2AirborneKg);
            _h2AirborneKg -= dkg;
            DepleteO2(dkg, nAtm);
            // Recombination is exothermic (242 kJ/mol-H₂) but slow — a gentle continuous warming of the building.
            double volPctRecomb = nAtm > 0 ? 100.0 * (dkg / H2MolarKg) / (nAtm + nH2) : 0.0;
            ContainmentTempC += AiccTempCPerVolPct * volPctRecomb;   // tiny per tick (≈ the burn coefficient, slow)
        }

        // --- Distributed Ignition System: armed by the operator AND powered (de-energized in SBO) -----
        IgnitersEnergized   = IgniterSystemArmed && !_elec.InSbo;
        IgniterSurfaceTempC = IgnitersEnergized ? IgniterSurfaceC : ContainmentTempC;

        bool o2Ok = ContainmentO2Pct >= O2FloorPct;
        double igniteSetpt = SteamInertFraction > 20.0 ? IgniteSetpointSteamPct : IgniteSetpointPct;

        if (IgnitersEnergized && !inerted && o2Ok && ContainmentH2Pct >= igniteSetpt && _h2AirborneKg > 0)
        {
            // Controlled deliberate burn — fast first-order decay toward the lean limit; stays sub-detonable.
            double targetKg = _h2AirborneKg * (BurnDownTargetPct / Math.Max(ContainmentH2Pct, 1e-6));
            double burnKg   = (_h2AirborneKg - targetKg) * Math.Min(1.0, dt / BurnTauSec);
            ApplyBurn(burnKg, nAtm, nH2, detonates: false);
        }
        else if (!inerted && o2Ok && _burnArmed && ContainmentH2Pct >= H2AllDirPct && _h2AirborneKg > 0)
        {
            // Spontaneous deflagration — a flammable, all-direction-propagating mixture finds a spark and
            // burns its whole airborne inventory in one shot. If it lit already in the detonable band it is a
            // DDT/detonation (the late-actuation hazard) and the AICC spike is doubled.
            bool det = ContainmentH2Pct >= H2DdtPct;
            ApplyBurn(_h2AirborneKg, nAtm, nH2, detonates: det);
            _burnArmed = false;
        }
        if (ContainmentH2Pct < H2LflPct) _burnArmed = true;   // re-arm once the building is non-flammable again

        ContainmentFlammable = ContainmentH2Pct >= H2LflPct && o2Ok && !inerted;
        ContainmentDetonable = ContainmentH2Pct >= H2DdtPct && ContainmentH2Pct <= H2DetonHiPct && o2Ok && !inerted;
    }

    /// <summary>把可燃氣體燃燒轉成 AICC 壓力／溫度尖峰 · Convert a hydrogen burn into a one-shot AICC pressure +
    /// temperature spike, O₂-limited for rich mixtures and knocked down by steam dilution.</summary>
    private void ApplyBurn(double burnKg, double nAtm, double nH2, bool detonates)
    {
        if (burnKg <= 0 || _h2AirborneKg <= 0 || nAtm <= 0) return;
        burnKg = Math.Min(burnKg, _h2AirborneKg);

        // O₂-limit: 2 H₂ + O₂ → 2 H₂O, so the burnable H₂ is capped at twice the available O₂ moles.
        double o2MolAvail = ContainmentO2Pct / 100.0 * nAtm;
        double burnMol = Math.Min(burnKg / H2MolarKg, 2.0 * o2MolAvail);
        if (burnMol <= 0) return;
        burnKg = burnMol * H2MolarKg;

        double volPctBurned = 100.0 * burnMol / (nAtm + nH2);
        double g = Math.Clamp(1.0 - 1.4 * (SteamInertFraction / 100.0), 0.0, 1.0);  // steam knock-down

        double dP = Math.Min(AiccKpaPerVolPct * volPctBurned * g, AiccPeakKpaCap);
        double dT = Math.Min(AiccTempCPerVolPct * volPctBurned * g, AiccPeakTempCCap);
        if (detonates) dP *= DetonationMult;

        ContainmentPressureKpa += dP;
        ContainmentTempC = Math.Min(ContainmentTempC + dT, AiccPeakTempCCap);
        LastBurnPeakKpa   = ContainmentPressureKpa;
        LastBurnPeakTempC = ContainmentTempC;
        DeflagrationOccurred = true;

        _h2AirborneKg -= burnKg;
        DepleteO2(burnKg, nAtm);
    }

    /// <summary>消耗氧氣 · Deplete containment O₂: 0.5 mol O₂ consumed per mol H₂ recombined or burned.</summary>
    private void DepleteO2(double h2Kg, double nAtm)
    {
        if (h2Kg <= 0 || nAtm <= 0) return;
        double o2MolDrop = 0.5 * (h2Kg / H2MolarKg);
        ContainmentO2Pct = Math.Max(0.0, ContainmentO2Pct - 100.0 * o2MolDrop / nAtm);
    }

    /// <summary>
    /// LOCA 堆芯裸露 → 峰值包殼溫度（PCT）連 10 CFR 50.46 驗收準則 · Core-uncovery Peak-Cladding-Temperature
    /// model with Zircaloy–steam oxidation, scored against the three quantitative 50.46(b) limits. Runs once
    /// per tick after the inventory/ECCS/thermal state is final. INSTRUMENTATION ONLY — it writes none of the
    /// core-damage / meltdown state, so the meltdown-ARM behaviour is untouched.
    /// Numerical robustness: the collapsed level is ALGEBRAIC in PrimaryDeficitPct (cannot drift, recovers
    /// the instant ECCS pulls the deficit back); the clad node uses the codebase's clamped-relaxation idiom
    /// (factor capped at 1, τ ≫ sub-step ⇒ cannot overshoot, no stiff exp ever evaluated); oxidation
    /// integrates W² so W is monotone; an internal sub-step engages only above 1200 °C to tame the
    /// autocatalytic term; hard clamps backstop every output.
    /// </summary>
    private void StepCladding(double dt)
    {
        if (dt <= 0) return;

        // --- collapsed mixture level over the active fuel: algebraic in the RCS inventory deficit ---
        double levelFrac = Math.Clamp(
            1.0 - (PrimaryDeficitPct - DeficitReserve) / (DeficitBoildry - DeficitReserve), 0.0, 1.0);
        if (AccumulatorInjecting || EccsInjecting)
            levelFrac += (1.0 - levelFrac) * Math.Min(1.0, dt / 2.0); // reflood swell brings the level back up
        levelFrac = Math.Clamp(levelFrac, 0.0, 1.0);
        CollapsedLevelFrac = levelFrac;
        double exposed = 1.0 - levelFrac;
        CoreExposedFrac = exposed;

        // --- coolant / steam sink temperature for the clad node ---
        double tCool = Math.Max(Thot, SatTempAt(PrimaryPressure));
        tCool = Math.Max(tCool, CladTempFloorC);

        // --- internal sub-step only in the autocatalytic regime (>1200 °C); normal ticks run nSub = 1 ---
        int nSub = CladTempC > CladSubstepTriggerC
            ? Math.Max(1, (int)Math.Ceiling(dt / CladSubstepDt)) : 1;
        double hc = dt / nSub;

        double oxHeatSum = 0.0; // Σ instantaneous Zr–steam power samples over the sub-steps
        double dWtick = 0.0;    // total weight-gain increment this tick (g/cm²)
        for (int s = 0; s < nSub; s++)
        {
            if (exposed < QuenchCoverThresh)
            {
                // Re-covered: rewet and quench rapidly toward the coolant temperature.
                CladQuenching = CladTempC > tCool + 1.0;
                CladTempC += (tCool - CladTempC) * Math.Min(1.0, hc / CladQuenchTau);
            }
            else
            {
                CladQuenching = false;
                // Decay-heat drive on the dry hot channel (peaking on the hot node only).
                double qDecay = DecayHeatFraction * RatedThermalMW * CladPeakingFactor * exposed
                                / CladHeatCapMWsPerC;

                // Zircaloy–steam parabolic oxidation, monotone via ∫ d(W²) = Kp·dt; blended CP↔BJ correlation.
                double qZr = 0.0;
                if (CladTempC > ZrOxStartTempC)
                {
                    double tk = CladTempC + 273.15;
                    double kpCp = CpRateA * Math.Exp(-CpRateB / tk);
                    double kpBj = BjRateA * Math.Exp(-BjEoverR / tk) * BjToWg2;
                    double f = Math.Clamp((tk - (OxCrossoverTk - 25.0)) / 50.0, 0.0, 1.0);
                    double kp = (1.0 - f) * kpCp + f * kpBj;
                    _w2 += kp * hc;
                    double w = Math.Sqrt(_w2);
                    double dW = w - _wPrev;        // ≥ 0 (monotone)
                    if (dW < 0) dW = 0;
                    _wPrev = w;
                    dWtick += dW;
                    qZr = ZrHeatGain * (dW / hc);  // exothermic kick (°C/s)
                    oxHeatSum += qZr * CladHeatCapMWsPerC;
                }

                // Clamped-relaxation toward the steady drive target — unconditionally bounded, no overshoot.
                double tDrive = tCool + (qDecay + qZr) * CladSteamCoolTau;
                CladTempC += (tDrive - CladTempC) * Math.Min(1.0, hc / CladSteamCoolTau);
            }

            CladTempC = Math.Clamp(CladTempC, ColdTemp, CladCeilingC);
            if (CladTempC > PeakCladTempC) PeakCladTempC = CladTempC;
        }

        CladOxidationHeatMW = oxHeatSum / nSub; // representative power over the tick

        // --- 50.46(b)(2) local ECR + (b)(3) core-wide H₂, both monotone from the weight gain ---
        double wNow = Math.Sqrt(_w2);
        MaxLocalOxidationPct = Math.Min(100.0, wNow / FullWallWeightGain * 100.0);
        double dEcrFrac = dWtick / FullWallWeightGain;        // fraction-of-wall reacted this tick at the hot node
        HydrogenMassKg += dEcrFrac * exposed * FullCoreH2Kg;  // core-wide-average mass of H₂ liberated
        CoreWideHydrogenPct = Math.Min(100.0, HydrogenMassKg / FullCoreH2Kg * 100.0);
    }

    /// <summary>
    /// RVLIS（反應堆壓力容器水位儀表系統）· Reactor Vessel Level Instrumentation System (NUREG-0737 II.F.2).
    /// Purely additive INSTRUMENTATION (mirrors StepCladding, lines ~400-404): reads post-tick inventory / pump /
    /// thermal state (CollapsedLevelFrac, PrimaryDeficitPct, RcpRunning[], PumpedFlowFraction, SubcoolingMarginC)
    /// and writes ONLY the four Rvlis* properties + two advisory alarms. It NEVER writes CollapsedLevelFrac,
    /// PrimaryDeficitPct, pump state, pressure or temperature, so the meltdown / ECCS path is provably unaffected.
    /// NOTE: the two alarms are ADVISORY. The ICC trip / FR-C.1 entry is owned by the core-exit-thermocouple
    /// 1200 °F (649 °C) criterion + subcooling monitor, NOT by RVLIS — these never command a scram.
    /// </summary>
    private void StepRvlis(double dt)
    {
        if (dt <= 0) return;

        // --- validity: any RCP running ⇒ the sensed ΔP column is pump-head-dominated, so only the
        //     dynamic-head range is trustworthy; pumps all off ⇒ the collapsed-level ranges are valid. ---
        bool anyPump = false;
        for (int i = 0; i < RcpRunning.Length; i++) if (RcpRunning[i]) { anyPump = true; break; }
        RvlisRange = anyPump ? RvlisValidRange.DynamicHead : RvlisValidRange.FullRange;

        // --- FULL RANGE (collapsed level over the whole vessel) ---------------------------------------
        // CollapsedLevelFrac is normalized over the ACTIVE FUEL only: 1 ⇒ fuel top (62 %), 0 ⇒ fuel bottom (33 %).
        double full;
        if (CollapsedLevelFrac >= 1.0 && SubcoolingMarginC > 0.0)
        {
            // covered + subcooled: ramp 62 → 100 % as the upper plenum/head fills (subcooling as the proxy).
            double headFill = Math.Clamp(SubcoolingMarginC / RvlisHeadSubcoolRef, 0.0, 1.0);
            full = RvlisFuelTopPct + (100.0 - RvlisFuelTopPct) * headFill;
        }
        else if (CollapsedLevelFrac > 0.0)
        {
            // within the active-fuel band: 33 % (frac 0) .. 62 % (frac 1).
            full = RvlisFuelBottomPct + CollapsedLevelFrac * (RvlisFuelTopPct - RvlisFuelBottomPct);
        }
        else
        {
            // fuel fully uncovered: drain the 0..33 % lower-plenum band off the inventory deficit past boil-dry.
            // span is [DeficitBoildry=34 .. RvlisVesselEmptyDeficit=40] — the PrimaryDeficitPct clamp ceiling.
            double drain = Math.Clamp(
                1.0 - (PrimaryDeficitPct - DeficitBoildry) / (RvlisVesselEmptyDeficit - DeficitBoildry),
                0.0, 1.0);
            full = RvlisFuelBottomPct * drain;
        }
        RvlisFullRangePct = Math.Clamp(full, 0.0, 100.0);

        // --- DYNAMIC HEAD (pump ΔP; valid only with pumps running). Voiding the column collapses it. ---
        // PumpedFlowFraction is already pump-count-weighted (≈0.25/pump) — do NOT re-scale by pump count.
        double voidFactor = Math.Clamp(SubcoolingMarginC / RvlisDhSubcoolRef, 0.0, 1.0);
        RvlisDynamicHeadPct = anyPump
            ? Math.Clamp(RvlisDhGain * PumpedFlowFraction * voidFactor, 0.0, 120.0)
            : 0.0;

        // --- UPPER RANGE (hot-leg elevation → top of head; pumps-off validity, same as Full Range) ----
        RvlisUpperRangePct = Math.Clamp(100.0 * CollapsedLevelFrac, 0.0, 100.0);

        // --- advisory alarms (anti-chatter deadband at the fuel-cover boundary) -----------------------
        bool belowFuel = _alarms[(int)ReactorAlarm.RvlisBelowTopOfFuel]
            ? CollapsedLevelFrac < 1.0                    // latched on: clear only when fully re-covered
            : CollapsedLevelFrac < RvlisCoverDeadband;    // off: raise only below 0.98
        SetAlarm(ReactorAlarm.RvlisBelowTopOfFuel, belowFuel);
        SetAlarm(ReactorAlarm.RvlisFullRangeLoLo, RvlisFullRangePct < RvlisFullRangeLoLoPct);
    }

    /// <summary>
    /// CET + 過冷度監測（堆芯出口熱電偶）· Core Exit Thermocouples + Subcooling Margin Monitor — the remaining
    /// two-thirds of the post-TMI Inadequate Core Cooling instrumentation triad (NUREG-0737 II.F.2,
    /// Reg Guide 1.97 Cat 1); RVLIS (StepRvlis) is the third. Type-K incore thermocouples; the displayed
    /// indicator is the HIGHEST valid CET.
    ///
    /// Purely additive INSTRUMENTATION (mirrors StepRvlis / StepCladding): reads post-tick thermal/inventory
    /// state (Thot, PrimaryPressure→SatTempAt, CladTempC, CollapsedLevelFrac, CoreExposedFrac,
    /// DecayHeatFraction) and writes ONLY CoreExitTempC, CetSubcoolingMarginC and the two ADVISORY ICC alarms.
    /// It NEVER writes FuelTemp / CladTempC / pressure / temperature / inventory / Damage / Mode, so the
    /// meltdown / ECCS path is provably unaffected. The ICC RED/ORANGE status is the WOG ERG FR-C entry
    /// criterion — operator/EOP action only; like RVLIS and MinDnbr it is never read by UpdateProtection and
    /// commands no scram.
    /// </summary>
    private void StepCet(double dt)
    {
        if (dt <= 0) return;

        // Seed to Thot on the first sample so a fresh/cold start doesn't ramp up from the ColdTemp default.
        if (!_cetInit) { CoreExitTempC = Thot; _cetInit = true; }

        // Exposure driver (0 = covered + sub-cooled, 1 = fully uncovered). Either signal can lead.
        double e = Math.Clamp(Math.Max(CoreExposedFrac, 1.0 - CollapsedLevelFrac), 0.0, 1.0);

        // Decay-heat-sharpened exposure: a barely-uncovered core with little decay heat barely superheats;
        // a freshly-tripped core (~5–7% DH) drives the CET hard. Saturates to 1 at ~5% DH + full exposure.
        double effExposure = Math.Clamp(e * (CetDhBase + CetDhGain * DecayHeatFraction), 0.0, 1.0);

        // Steam/clad superheat target: a dry node sits in superheated steam (≥ Tsat) and cannot read hotter
        // than the cladding it is strapped to, so the target is bounded below by Tsat and above by CladTempC.
        double cetTarget = Math.Max(SatTempAt(PrimaryPressure), CladTempC);

        // Interpolate Thot+bias → target by effective exposure; clamp to the qualified Type-K ceiling.
        double baseC  = Thot + CetCoveredBiasC;
        double cetRaw = Math.Min(CetCeilingC, baseC + effExposure * (cetTarget - baseC));

        // First-order clamped relaxation (the file-wide unconditionally-stable pattern; τ is realism only).
        CoreExitTempC += (cetRaw - CoreExitTempC) * Math.Min(1.0, dt / CetTauSec);
        CoreExitTempC  = Math.Clamp(CoreExitTempC, ColdTemp, CetCeilingC);

        // SMM — conservative "higher-of": max(Thot, CET) guards a CET momentarily lagging below Thot during a
        // fast heat-up, so the margin never reads more sub-cooled than the hot leg.
        CetSubcoolingMarginC = SatTempAt(PrimaryPressure) - Math.Max(Thot, CoreExitTempC);

        // Advisory ICC status flags (FR-C). Never command a scram (see method contract).
        SetAlarm(ReactorAlarm.IccRed,    CoreExitTempC >= IccRedTempC);
        SetAlarm(ReactorAlarm.IccOrange, CoreExitTempC >= IccOrangeTempC || CetSubcoolingMarginC <= 0.0);
    }

    /// <summary>
    /// 硼酸溶解度極限 · Boric-acid (H₃BO₃) solubility limit, expressed as ppm boron, at coolant temperature
    /// <paramref name="tC"/> (°C). Linear-in-T fit anchored to the handbook saturation curve (~50 g/L at 25 °C,
    /// ~275 g/L near 100 °C — the steep negative-heat-of-solution rise). Clamped to [25, 150] °C to stay
    /// monotone within/just beyond the tabulated range; floored so a cold core still has a finite limit.
    /// </summary>
    private static double BoricSolubilityPpm(double tC)
    {
        double gPerL = SolubGperL25 + SolubSlopeGperLperC * (Math.Clamp(tC, 25.0, SolubilityClampHiC) - 25.0);
        return Math.Max(SolubFloorPpm, gPerL * BoronWtFracH3BO3 * 1000.0); // g/L H₃BO₃ → ppm B (1 g/L ≈ 1 ppm at sump density)
    }

    /// <summary>
    /// 堆芯硼酸析出與熱段再循環 · Post-LOCA boric-acid precipitation + hot-leg-recirculation (ES-1.4) monitor.
    /// Models the core-mixing-region boric-acid concentration that builds as decay heat boils off the borated
    /// ECCS makeup (non-volatile boron stays behind). The closed-form exp-relaxation update is unconditionally
    /// stable — it relaxes toward a quasi-steady ceiling and can never overshoot or diverge at any dt. The
    /// operator's hot-leg-recirc transfer collapses the ceiling to the injection concentration, flushing the
    /// core. INSTRUMENTATION-ONLY: writes only its own readouts + two advisory alarms; no feedback/consequence
    /// path, so the meltdown-ARM / ECCS logic is provably unaffected (same contract as StepRvlis/StepCet).
    /// </summary>
    private void StepBoricAcidPrecip(double dt)
    {
        if (dt <= 0) return;

        // The solubility ceiling tracks the hottest covered-liquid temperature (Thot is the core-mixing proxy).
        BoricSolubilityLimitPpm = BoricSolubilityPpm(Thot);

        // Gate: only a genuine LOCA long-term-cooling boil-off state concentrates boron — an actual break OR a
        // latched inventory deficit past the uncovery reserve, with borated injection actually feeding the core
        // (SI actuated or ECCS injecting) and the core at/near saturation (boiling) with meaningful decay heat.
        // All four must hold, so normal operation, ordinary trips, and still-subcooled transients never trigger it.
        BoricConcentrationActive =
            (_breakArea > 0 || PrimaryDeficitPct > DeficitReserve)
            && (SiActuated || EccsInjecting)
            && SubcoolingMarginC <= 2.0
            && DecayHeatFraction > 0.005;

        // INACTIVE (and not yet precipitated): relax the core concentration back toward the well-mixed RCS and
        // stop the clock. Precipitated stays latched until a full Reset / new scenario.
        if (!BoricConcentrationActive && !Precipitated)
        {
            CoreBoronPpm += (BoronPpm - CoreBoronPpm) * Math.Min(1.0, dt / CoreBoronEquilibTau);
            TimeToPrecipSeconds = double.PositiveInfinity;
            SetAlarm(ReactorAlarm.BoricAcidPrecipApproach, false);
            SetAlarm(ReactorAlarm.BoricAcidPrecipitated, Precipitated);
            return;
        }

        // ACTIVE concentration physics. Fractional core-water boil-off rate reuses the engine's boil-off basis
        // (BoiloffDeficitRate is %/s per decay-heat fraction → /100 gives a fractional 1/s). That fraction of the
        // core water leaves as steam each second, carrying ~no boron, so boron concentrates.
        double kConc  = BoiloffDeficitRate * DecayHeatFraction / 100.0;   // 1/s
        double cInj   = BoronPpm;                                         // injected makeup concentration (RWST/sump → live RCS boron)
        double kFlush = HotLegRecircActive ? HotLegFlushRate : 0.0;       // operator ES-1.4 through-core flush

        // Quasi-steady ceiling: with recirc the core flushes toward the injected conc; without it, boil-off
        // concentrates toward a boil-off/mixing ceiling set by the turnover floor.
        double target = HotLegRecircActive ? cInj : cInj * (kConc + CoreMixFloor) / CoreMixFloor;
        double kNet   = Math.Max(1e-9, kConc + kFlush + CoreMixFloor);    // net relaxation rate

        // Closed-form, unconditionally-stable relaxation toward the ceiling (mirrors the dilution exp solution).
        CoreBoronPpm = target + (CoreBoronPpm - target) * Math.Exp(-kNet * dt);

        // Latch + operator-action-window clock (closed-form from the same exp law, like TimeToCriticalitySeconds).
        if (CoreBoronPpm >= BoricSolubilityLimitPpm)
        {
            Precipitated = true;
            TimeToPrecipSeconds = 0;
        }
        else if (target <= BoricSolubilityLimitPpm || target <= CoreBoronPpm)
        {
            TimeToPrecipSeconds = double.PositiveInfinity;               // ceiling below the limit, or falling (recirc winning) → never precipitates
        }
        else
        {
            double arg = (target - BoricSolubilityLimitPpm) / (target - CoreBoronPpm);
            TimeToPrecipSeconds = (arg > 0 && arg < 1) ? -Math.Log(arg) / kNet : double.PositiveInfinity;
        }

        SetAlarm(ReactorAlarm.BoricAcidPrecipApproach,
            BoricConcentrationActive && !Precipitated && CoreBoronPpm > PrecipWarnFracOfLimit * BoricSolubilityLimitPpm);
        SetAlarm(ReactorAlarm.BoricAcidPrecipitated, Precipitated);
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

        // Minimum DNBR (W-3) — display-only anti-DNB margin; not read by UpdateProtection.
        ComputeDnbr(dt);
    }

    private void UpdateBoron(double dt)
    {
        // During an uncontrolled boron dilution (FSAR 15.4.6) the exponential-decay model in UpdateScenarios
        // is the SOLE writer of BoronPpm — skip the normal control ramp so there is exactly one author.
        if (_dilutionActive) return;

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
            // Locked rotor (15.3.3): a seized impeller stops its loop ~instantly — NO flywheel coastdown,
            // NO spin-up. Pin to zero and skip both branches so this loop drops out in ~one tick while the
            // others keep pumping (core flow steps to ~0.74–0.76 rated — the DNBR-limiting flow transient).
            if (i == _lockedRotorLoop) { _rcpFlow[i] = 0.0; continue; }
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
        double dopplerRho = DopplerCoeff * (FuelTemp - RefFuelTemp);   // slow lumped-fuel Doppler
        double modRho = ModTempCoeff * (Tavg - RefModTemp);
        // Xenon worth proportional to xenon concentration (normalized so equilibrium full power = 1).
        double xenonRho = XenonWorthFull * Xenon;
        // Samarium worth proportional to Sm-149 concentration (normalized so equilibrium full power = 1).
        double samariumRho = SamariumWorthFull * _sm;

        // Excess reactivity from fresh-core / fuel state baseline so that the core can be made
        // critical with rods/boron in the operating band.
        const double ExcessBaseline = 0.080 + BoronWorth * NominalBoron * -1.0; // brings band into reach
        // A dropped full-length RCCA inserts its integral worth (negative) as the fall-ramp completes.
        double droppedRho = -(DroppedRodWorthPcm * 1e-5) * _dropRamp;
        // Everything above is constant across the (possibly fine-grained) RIA micro-steps below.
        double rhoSlow = ExcessBaseline + rodRho + boronRho + dopplerRho + modRho + xenonRho + samariumRho + droppedRho;

        // ---- Rod Ejection Accident (RIA / REA, Ch 15.4.8): super-prompt-critical excursion ----
        // The ejected rod adds +worth as a 0.1 s ramp. The resulting ~45 ms power pulse is turned over NOT by
        // the scram (orders of magnitude too slow) but by PROMPT Doppler sourced from the hot-pellet ENTHALPY
        // node — the lumped FuelTemp (τ≈5.5 s) cannot quench it. While the pulse is energetic we resolve the
        // stiff prompt mode with 1 ms inner steps so the deposited enthalpy (the figure of merit) is real and
        // does not lean on the denom/power numerical backstops. Outside an RIA, micro == 1 → exact prior path.
        bool riaPulse = _riaActive && (_ejectRamp < 0.999 || _power > 1.05 || Math.Abs(ReactorPeriodSeconds) < 25.0);
        int micro = riaPulse ? Math.Max(1, (int)Math.Ceiling(h / RiaMicroDt)) : 1;
        double hm = h / micro;
        double coolantEnth = Uo2Enthalpy(Math.Max(ColdTemp, Tcold)); // hot-pellet relaxation sink
        double ejectRho = (EjectedRodWorthPcm * 1e-5) * _ejectRamp;
        double dopplerPromptRho = 0.0;
        double rho = rhoSlow + ejectRho;
        double newPower = _power;

        for (int m = 0; m < micro; m++)
        {
            if (_riaActive && _ejectRamp < 1.0)
                _ejectRamp = Math.Min(1.0, _ejectRamp + hm / RiaEjectTimeSec);
            ejectRho = (EjectedRodWorthPcm * 1e-5) * _ejectRamp;

            // Prompt Doppler = DopplerCoeff × (hot-pellet temp − its slow baseline). Zero in steady state
            // (enthalpy ≈ ref), strongly negative on a ms power spike. Only active during the RIA event so the
            // tuned normal-operation reactivity balance is untouched.
            dopplerPromptRho = _riaActive
                ? DopplerCoeff * (Uo2EnthalpyToTemp(_hotPelletEnthalpy) - Uo2EnthalpyToTemp(_hotPelletEnthalpyRef))
                : 0.0;
            rho = rhoSlow + ejectRho + dopplerPromptRho;

            // ---- point kinetics (6 groups), unconditionally-stable backward (implicit) Euler ----
            // Collapse the full 7-state implicit system into one scalar solve: substitute each
            // C_i^{n+1} = (C_i + h·(β_i/Λ)·P^{n+1})/(1 + h·λ_i) into the power balance.
            double precursorContribution = 0; // lagged-but-corrected precursor source
            double implicitFeedback = 0;      // implicit precursor feedback into power
            for (int i = 0; i < 6; i++)
            {
                double di = 1.0 + hm * Lambda[i];
                precursorContribution += Lambda[i] * _precursor[i] / di;
                implicitFeedback += hm * Lambda[i] * Beta[i] / (PromptLifetime * di);
            }

            double denom = 1.0 - hm * (rho - BetaTotal) / PromptLifetime - hm * implicitFeedback;
            if (denom < 1e-3) denom = 1e-3; // backstop: denom crosses zero at/above prompt-critical (ρ ≥ ~1$)

            newPower = (_power + hm * (precursorContribution + SourceLevel)) / denom;
            if (newPower < 1e-12) newPower = 1e-12; // floor, never zero/negative (would poison precursors+source)

            for (int i = 0; i < 6; i++)
            {
                _precursor[i] = (_precursor[i] + hm * (Beta[i] / PromptLifetime) * newPower) / (1.0 + hm * Lambda[i]);
                if (_precursor[i] < 0) _precursor[i] = 0;
            }

            // Reactor period from rate of change (computed before _power is overwritten).
            double rate = (newPower - _power) / (Math.Max(_power, 1e-12) * hm);
            ReactorPeriodSeconds = Math.Abs(rate) < 1e-9 ? 1e9 : 1.0 / rate;
            _power = newPower;
            if (_power > 50) _power = 50; // numerical backstop only (proper prompt Doppler keeps it well below)

            // Hot-pellet enthalpy node (cal/g): adiabatic on the pulse timescale, relaxing to coolant over τ.
            // Updated AFTER the kinetics step so the prompt-Doppler predictor-corrector sees this tick's power.
            double qHot = RiaQHot(_power + DecayHeatFraction);
            _hotPelletEnthalpy += (qHot - (_hotPelletEnthalpy - coolantEnth) / FuelEnthalpyTau) * hm;
            if (_hotPelletEnthalpy < 0) _hotPelletEnthalpy = 0;
            _hotPelletEnthalpyRef += (_hotPelletEnthalpy - _hotPelletEnthalpyRef) * Math.Min(1.0, hm / FuelEnthalpyTau);

            if (_hotPelletEnthalpy > PeakFuelEnthalpyCalPerG) PeakFuelEnthalpyCalPerG = _hotPelletEnthalpy;
            if (_riaActive)
            {
                double riseNow = _hotPelletEnthalpy - _riaEnthalpyAtTrigger;
                if (riseNow > PeakFuelEnthalpyRiseCalPerG) PeakFuelEnthalpyRiseCalPerG = riseNow;
            }
        }

        EjectedRodReactivityPcm = ejectRho * 1e5;
        RodReactivityPcm = (rodRho + droppedRho + ejectRho) * 1e5;  // fold dropped + ejected worth into the rod line
        DroppedRodReactivityPcm = droppedRho * 1e5;
        BoronReactivityPcm = (boronRho + ExcessBaseline) * 1e5;     // fold baseline into boron line for display
        DopplerReactivityPcm = (dopplerRho + dopplerPromptRho) * 1e5; // slow + prompt (RIA) Doppler
        ModeratorReactivityPcm = modRho * 1e5;
        XenonReactivityPcm = xenonRho * 1e5;
        SamariumReactivityPcm = samariumRho * 1e5;
        ReactivityPcm = rho * 1e5;

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
        // Steam dump heat sink: up to 40% main-steam bypass to the condenser. Reuses the primary-to-secondary
        // head and the condenser-feed gate; reads the per-tick cached demand. This is what keeps removing
        // core + decay heat after the GVs shut on a load rejection, holding Tavg below the OTΔT / Hi-Thot
        // trips so the plant rides out the upset without a reactor trip.
        sgRemoval += SdSinkGainK * SteamDumpDemand * SteamDumpCapacityFrac
                   * Math.Max(0.0, Tavg - SecondarySatTemp()) * 0.01
                   * (0.3 + 0.7 * FeedwaterFlow);
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
        // first-responder, so it is evaluated and relieves BEFORE the code safeties each step. When LTOP/COMS
        // is armed (cold RCS, set by UpdatePtLimits) the PORVs are re-ranged to a low cold setpoint so they
        // relieve well below the Appendix-G brittle-fracture limit on a cold mass/heat-input transient;
        // effClose < effOpen is guaranteed by construction so the open/close latch can never thrash.
        double effOpen  = LtopArmed ? LtopOpenPressureMPa : PorvOpenPressure;
        double effClose = LtopArmed ? (LtopOpenPressureMPa - LtopCloseHystMPa) : PorvClosePressure;
        if (PrimaryPressure > effOpen) _porvAuto = true;
        else if (PrimaryPressure < effClose) _porvAuto = false;
        LtopPorvOpen = LtopArmed && _porvAuto;
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

    /// <summary>Clamped piecewise-linear interpolation of ys(xs) at x (xs strictly increasing).</summary>
    private static double Lerp(double[] xs, double[] ys, double x)
    {
        if (x <= xs[0]) return ys[0];
        int n = xs.Length;
        if (x >= xs[n - 1]) return ys[n - 1];
        int i = 1;
        while (i < n && x > xs[i]) i++;
        double t = (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
        return ys[i - 1] + t * (ys[i] - ys[i - 1]);
    }

    /// <summary>
    /// 反應堆壓力容器 P/T 操作限值（10 CFR 50 附錄 G）連低溫超壓保護 LTOP/COMS ·
    /// Reactor-vessel Pressure-Temperature operating limits (10 CFR 50 Appendix G) + LTOP/COMS.
    ///
    /// Runs once per outer Update tick AFTER Tcold/PrimaryPressure are current. It (1) caches the App G
    /// allowable pressure at the indicated Tcold from the representative heatup limit table, (2) tracks a
    /// smoothed signed heatup/cooldown rate for the 100 °F/hr Appendix-G rate limit, (3) arms LTOP below the
    /// enable temperature (with hysteresis) — the StepThermal PORV block reads <see cref="LtopArmed"/> next
    /// substep and substitutes the low cold setpoint — and (4) wires the four P/T-limit annunciators. The
    /// table values, ART (~82 °C) and LTOP setpoint are REPRESENTATIVE GENERIC values, not a plant PTLR;
    /// the load-bearing exact numbers are the K_IC constants, the safety factors and the 100 °F/hr limit.
    /// </summary>
    private void UpdatePtLimits(double dt)
    {
        // (1) App G allowable pressure at the indicated cold-leg temperature — cached for the property reads.
        MaxAllowablePressureMPa = Lerp(PtTempC, PtPmaxMPa, Tcold);

        // (2) Smoothed signed RCS heatup(+)/cooldown(−) rate, °C/hr (single-pole EMA; first sample seeds it).
        if (!_rateInit) { _prevTcoldForRate = Tcold; _rateInit = true; }
        else if (dt >= RateSampleMinDt)
        {
            double inst = ((Tcold - _prevTcoldForRate) / dt) * 3600.0;   // instantaneous °C/hr
            double a = dt / (RateFilterTauSec + dt);                     // exact discrete single-pole α
            RcsRateCperHr += a * (inst - RcsRateCperHr);
            _prevTcoldForRate = Tcold;
        }

        // (3) LTOP arm/disarm with hysteresis so the boundary doesn't chatter.
        if (Tcold < LtopEnableTempC) LtopArmed = true;
        else if (Tcold > LtopEnableTempC + LtopEnableHystC) LtopArmed = false;

        // (4) Annunciators (existing SetAlarm pattern). LtopPorvOpen is set in the StepThermal PORV block.
        SetAlarm(ReactorAlarm.PtApproach, PtMarginMPa < PtApproachWarnMPa && PtMarginMPa >= 0.0);
        SetAlarm(ReactorAlarm.PtViolation, PtViolation);
        SetAlarm(ReactorAlarm.RcsRateExceeded, Math.Abs(RcsRateCperHr) > AppGRateLimitCperHr * RateAlarmFraction);
        SetAlarm(ReactorAlarm.LtopActive, LtopPorvOpen);
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

        // Sm-149 / Pm-149 second poison pair (Nd-149 lumped into direct Pm production). Normalized so that
        // equilibrium full power gives Samarium≈1 (worth ≈ −640 pcm). Pm-149 is produced ∝ power and decays
        // with λ_Pm = ln2/53.1h; Sm-149 is born from that decay and — being STABLE — is removed ONLY by
        // neutron burnout (σφ, ∝ flux). Both evolve on a tens-of-hours scale, so plain forward Euler at the
        // sim's dt is unconditionally stable. Equilibrium Sm = λ_Pm·γ_Pm/(σ·φ)·φ = λ_Pm·γ_Pm/σ is therefore
        // FLUX-INDEPENDENT; after a trip (φ→0) the burnout term vanishes and Sm builds monotonically toward
        // Sm_eq + Pm_eq ≈ 2.84 (worth ≈ −1816 pcm) over ~10–13 days — the "samarium dead time".
        const double lamPm   = 3.6260e-6; // 1/s, Pm-149 decay (ln2 / 53.1 h)
        const double sigmaSm = 6.6613e-6; // 1/s, Sm-149 burnout at full power (φ=1) → τ ≈ 41.7 h
        const double gammaPm = 1.8371;    // = sigmaSm/lamPm, so equilibrium Samarium normalizes to 1
        _pm += (gammaPm * lamPm * phi - lamPm * _pm) * dt; // promethium: production ∝ power, β-decay removal
        if (_pm < 0) _pm = 0;
        _sm += (lamPm * _pm - sigmaSm * phi * _sm) * dt;   // samarium: born from Pm decay, removed by burnout only
        if (_sm < 0) _sm = 0;

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

        // --- SteamPressure (single writer, MOVED here) — closing the GVs reduces draw ⇒ header pressure rises;
        // the 40% condenser dump relieves it. On a load rejection steamDraw→0 would drive pTarget toward the
        // MSSV liftpoint; the dump term subtracts up to 0.40·0.40 = 0.16 of normalized power, holding header
        // pressure in the dump-controlled band below the safety valves. SdPressReliefK (0.40) < SteamPressDrawK
        // (0.6) so the bypass is correctly weaker than full turbine draw.
        double dumpFlow = SteamDumpDemand * SteamDumpCapacityFrac;
        double pTarget = 0.5 + 6.5 * Math.Clamp(
            _power - SteamPressDrawK * steamDraw - SdPressReliefK * dumpFlow + 0.4, 0, 1.2);
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
        // Condenser backpressure scales the gross output: deeper vacuum credits a little, degraded vacuum penalises.
        double grossElec = Math.Min(_power * 0.33, FirstStagePressure) * RatedElectricMW * CondenserVacuumOutputFactor;
        if (GeneratorBreakerClosed && !TurbineTripped && TurbineRPM > SyncRpm * 0.94 && steamAvail)
            ElectricPowerMW += (grossElec - ElectricPowerMW) * Math.Min(1, dt / 4.0);
        else
            ElectricPowerMW += (0 - ElectricPowerMW) * Math.Min(1, dt / 2.0);
        if (ElectricPowerMW < 0) ElectricPowerMW = 0;
    }

    // ===================== Main condenser vacuum / backpressure · 主凝汽器真空／背壓 =====================
    // Single writer of all condenser state. Runs once per tick AFTER the secondary (so FirstStagePressure is
    // current as the turbine steam-flow / heat-rejection load proxy) and BEFORE the steam dump (which gates on
    // CondenserAvailable) and the next UpdateSecondary's grossElec (which scales by CondenserVacuumOutputFactor,
    // set here this tick). Pure: reads FirstStagePressure / CirculatingWaterInletC / AirEjectorLost /
    // CircWaterLost; writes only condenser properties + may call TripTurbine(). It can only REDUCE an output
    // term and BLOCK a cooling bypass — it never adds heat to the primary, so the meltdown-arm path is intact.
    private void UpdateCondenser(double dt)
    {
        CirculatingWaterInletC = Math.Clamp(CirculatingWaterInletC, 1.0, 40.0);

        // Condensing-temperature target: CW inlet + load-scaled CW rise + load-scaled terminal ΔT.
        double loadFrac  = Math.Clamp(FirstStagePressure, 0.0, 1.0);
        double satTarget = CirculatingWaterInletC + CwRiseFullC * loadFrac + CondenserTtdFullC * loadFrac;
        // First-order thermal lag (shell water + tube metal). Vapor pressure is taken instantaneous off sat temp.
        CondenserSatTempC += (satTarget - CondenserSatTempC) * Math.Min(1.0, dt / CondenserTauThermalS);

        // Non-condensable (air) partial-pressure balance. The air-removal system pumps it down toward ~0.2 kPa;
        // losing the ejector lets the in-leakage accumulate unbounded (slow), losing circ water adds a faster term.
        double leakIn = AirLeakBaseKpaPerS + (CircWaterLost ? AirLeakCircLossKpaPerS : 0.0);
        if (!AirEjectorLost) AirPartialKpa += (leakIn - AirRemovalCapKpaPerS * AirPartialKpa) * dt;
        else                 AirPartialKpa += leakIn * dt;
        AirPartialKpa = Math.Clamp(AirPartialKpa, 0.0, AirPartialMaxKpa);

        // Dalton: total backpressure = water saturation (Magnus/Arden-Buck, ~1% vs steam tables) + air partial.
        double psat = 0.6112 * Math.Exp(17.62 * CondenserSatTempC / (243.12 + CondenserSatTempC));
        CondenserPressureKpa  = Math.Clamp(psat + AirPartialKpa, 0.5, 101.3);
        CondenserPressureInHg = CondenserPressureKpa / KPaPerInHg;
        CondenserVacuumInHg   = 29.92 - CondenserPressureInHg;

        // Exhaust-pressure output correction, with the LP last-stage choking floor below which deeper vacuum
        // yields no further output. Calibrated so design 6.77 kPa (2.0 inHgA) → factor 1.000.
        double effKpa = Math.Max(CondenserPressureKpa, BackpressureFloorKpa);
        CondenserVacuumOutputFactor = Math.Clamp(
            1.0 - BackpressureK * (effKpa - DesignBackpressureKpa), OutputFactorMin, OutputFactorMax);

        // Degrading-vacuum annunciator (hysteresis).
        if (CondenserPressureInHg >= VacAlarmInHgA)      CondenserVacuumLow = true;
        else if (CondenserPressureInHg < VacAlarmClearInHgA) CondenserVacuumLow = false;

        // Condenser-available steam-dump permissive (hysteresis): cannot dump into a condenser that lost vacuum.
        if (CondenserPressureInHg >= VacDumpInhibitInHgA)      CondenserAvailable = false;
        else if (CondenserPressureInHg < VacDumpRearmInHgA)    CondenserAvailable = true;

        // Low-vacuum TURBINE TRIP: latch the existing TurbineTripped flag (the P-9 cascade then trips the
        // reactor above ~50 % power and rides the runback below it). Stays latched until an explicit reset.
        if (CondenserPressureInHg >= VacTripInHgA && !TurbineTripped)
        {
            TripTurbine();
            LastTripFunctionEn = "Low Condenser Vacuum (Turbine)";
            LastTripFunctionZh = "凝汽器真空低（汽輪機）";
        }
    }

    // ===================== Steam dump / turbine bypass · 汽輪機旁路（凝汽器排汽） =====================
    // Single writer of SteamDumpDemand. Runs once per tick AFTER the kinetics/thermal substeps (so Tavg is
    // current) and BEFORE UpdateSecondary (so the cached demand is ready for the pTarget relief term). Tref
    // is from the prior tick's UpdateAutoRods. Both controller terms are Max(0,…) → dump is cooling-only and
    // can never add heat to the primary, preserving the meltdown-arm path.
    private void UpdateSteamDump(double dt)
    {
        // Arming permissive: turbine tripped, OR generator breaker open while at power, OR reactor scrammed.
        bool armed = TurbineTripped || !GeneratorBreakerClosed || IsScrammed;

        // P-12 low-Tavg block: smooth 0..1 gate — 0 at/below SdLoTavgBlockC, 1 a band above it. Cuts dumps
        // on real overcooling (e.g. an MSLB) so the bypass cannot drive an excessive cooldown.
        double loBlock = Math.Clamp((Tavg - SdLoTavgBlockC) / SdLoTavgBandC, 0.0, 1.0);

        double raw = 0.0;
        string mode;
        if (armed)
        {
            // Mode 1 — load rejection: modulate on (Tavg − Tref). Mode 2 — trip-open: dump toward no-load Tavg.
            double loadRej  = SdLoadRejGainC  * Math.Max(0.0, Tavg - Tref);
            double tripOpen = SdTripOpenGainC * Math.Max(0.0, Tavg - NoLoadTavg);
            raw  = Math.Max(loadRej, tripOpen) * loBlock;   // the more demanding controller wins (W mode-select)
            mode = loBlock <= 0.0           ? "Blocked"
                 : tripOpen >= loadRej      ? "TripOpen"
                 :                            "LoadReject";
        }
        else
        {
            mode = "Off";
        }

        double target = Math.Clamp(raw, 0.0, 1.0);
        // Condenser-available interlock: a condenser that has lost vacuum cannot accept the dump, so the bypass
        // valves are blocked. The un-relieved steam then raises SG header pressure toward the atmospheric-dump /
        // MSSV path (existing pTarget). Cooling-reducing only — never adds heat (meltdown-arm path preserved).
        if (!CondenserAvailable)
        {
            target = 0.0;
            if (armed) mode = "NoCondenser";
        }
        // First-order air-operated valve stroke lag.
        SteamDumpDemand += (target - SteamDumpDemand) * Math.Min(1.0, dt / SdValveTau);
        SteamDumpDemand  = Math.Clamp(SteamDumpDemand, 0.0, 1.0);

        SteamDumpArmed  = armed;
        SteamDumpModeEn = SteamDumpValvesOpen ? mode : (armed ? "Armed" : "Off");
    }

    private bool _autoRodWasOn;

    private void UpdateAutoRods(double dt)
    {
        // On engaging AUTO, sync the group-demand counter to the current bank stack so control is bumpless.
        if (!_autoRodWasOn) _rodDemandCounter = InferRodDemandFromBanks();
        _autoRodWasOn = true;

        // --- Westinghouse Tavg/Tref rod control (NRC Tech Manual §8.1). The controller regulates Tavg to a
        //     turbine-load-programmed reference Tref — NOT to a power setpoint — summed with a power-mismatch
        //     anticipatory term, then mapped through a deadband + variable-speed program onto the group-demand
        //     counter. Power emerges as whatever satisfies the steam load at Tavg = Tref. ---
        double load = Math.Clamp(FirstStagePressure, 0.0, 1.0);     // impulse-pressure load proxy (0..1)
        Tref = NoLoadTavg + (FullLoadTavg - NoLoadTavg) * load;     // linear Tref program
        TavgTrefError = Tavg - Tref;                                // °C, + = too hot

        // Anticipatory power mismatch: turbine load minus nuclear power, as an equivalent-temperature signal.
        // Acts the instant load diverges from power (before Tavg measurably shifts) and self-decays as _power
        // re-tracks the load — reproducing the rate comparator's decay-to-zero behaviour.
        double mismatch = load - _power;                           // −1..1
        double combinedError = TavgTrefError + PowerMismatchGainC * mismatch;
        double e = Math.Abs(combinedError);

        // Variable-speed program: deadband → 8 spm (min/lockup) → linear ramp → 72 spm (max).
        double speedSpm;
        if (e <= RodDeadbandC)        speedSpm = 0.0;
        else if (e <= RodRampStartC)  speedSpm = RodSpeedMinSpm;
        else                          speedSpm = Math.Clamp(
                                          RodSpeedMinSpm + RodSpeedSlopeSpm * (e - RodRampStartC),
                                          RodSpeedMinSpm, RodSpeedMaxSpm);

        // Direction: too hot (combinedError>0) → insert rods → lower the counter; too cold → withdraw.
        double dir = combinedError > 0 ? -1.0 : +1.0;
        RodSpeedDemandSpm = dir * speedSpm;                        // signed telemetry (+ = withdraw)
        // Accumulate the counter as a double (carry the fractional step remainder across ticks).
        _rodDemandCounter = Math.Clamp(_rodDemandCounter + dir * (speedSpm / 60.0) * dt, 0, RodTotalSpan);
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

        // AMSAC runs here — AFTER the RPS evaluation/scram and the P-9 trip — but it is deliberately
        // DIVERSE: it reads only process signals and ignores IsScrammed / _rps state, so it actuates
        // regardless of whether the reactor trip succeeded (the whole point during an ATWS).
        StepAmsac(dt);

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

    /// <summary>
    /// AMSAC（ATWS 緩解系統致動電路）· ATWS Mitigating System Actuation Circuitry per 10 CFR 50.62.
    /// Diverse, RPS-independent: trips the turbine and starts AFW on loss of the secondary heat sink once
    /// armed above the C-20 power permissive — without ever inserting rods or reading reactor-trip state.
    /// </summary>
    private void StepAmsac(double dt)
    {
        // Track the running peak RCS pressure (figure of merit: stays below ASME Service Level C ~3200 psia).
        PeakPrimaryPressureMpa = Math.Max(PeakPrimaryPressureMpa, PrimaryPressure);

        // 1) C-20 arming permissive with hysteresis (≥40% arm, <35% disarm) — no challenge at low power.
        if (_power >= AmsacArmThreshold) AmsacArmed = true;
        else if (_power < AmsacArmThreshold - AmsacDisarmHyst) AmsacArmed = false;

        // 2) Initiating condition — loss of the secondary heat sink: SG low-low level OR loss of all main
        //    feedwater while at power. Process signals only; no RPS/scram coupling (diversity constraint).
        bool condition = AmsacArmed
            && (IndicatedSgLevel <= AmsacSgLoLoPct || (FeedwaterFlow < 0.02 && _power > 0.05));

        // 3) Deliberate time delay so the RPS gets first crack during a normal trip the protection set handles.
        if (condition) _amsacTimer += dt; else _amsacTimer = 0;

        // 4) Latch actuation (cleared only by ResetTrip / scenario reset). The operator can defeat AMSAC to
        //    demonstrate the unmitigated ATWS where the code safeties lift and peak pressure climbs to Level C.
        if (_amsacTimer >= AmsacDelaySeconds && !AmsacDefeated && Mode != ReactorMode.Meltdown)
            AmsacActuated = true;

        // 5) Actuation output vector: turbine trip + immediate AFW (bypassing the 60 s _lofwTimer gate);
        //    NO rod insertion. AFW flow still requires a live motor/turbine-driven pump (set in UpdateScenarios).
        if (AmsacActuated)
            TurbineTripped = true;
    }

    private void SetAlarm(ReactorAlarm a, bool on) => _alarms[(int)a] = on;

    /// <summary>
    /// 彈棒事故後果評定 · Rod-ejection acceptance evaluation. Compares the peak hot-pellet fuel enthalpy
    /// (and enthalpy RISE) against the RIA limits: RG 1.236 230 cal/g coolability / RG 1.77 280 cal/g
    /// (incipient melt) on the absolute peak, and a burnup-dependent PCMI enthalpy-rise threshold (HZP)
    /// or the W-3 DNBR safety limit (at power) for cladding failure. Runs every tick; cheap when idle.
    /// </summary>
    private void UpdateRiaConsequences()
    {
        // PCMI cladding-failure enthalpy-RISE threshold falls with burnup (RG 1.236 Figs 2–5): ~150 cal/g
        // rise fresh → ~60 cal/g near 68 GWd/MTU as cladding hydrides and embrittles.
        double bf = Math.Clamp(BurnupMwdPerTonne / 68000.0, 0.0, 1.0);
        RiaPcmiRiseLimitCalPerG = RiaPcmiRiseFreshCalG + (RiaPcmiRiseHighBurnCalG - RiaPcmiRiseFreshCalG) * bf;

        RiaCoolabilityViolated = PeakFuelEnthalpyCalPerG >= RiaCoolabilityCalG;       // RG 1.236, 230 cal/g
        RiaFuelMelt            = PeakFuelEnthalpyCalPerG >= RiaLegacyCoolabilityCalG; // RG 1.77, 280 cal/g (incipient melt)

        // Two failure modes: PCMI (enthalpy rise, dominant from a low-power initial state) and DNB (assumed
        // failure at power, preserving the RG 1.77 "DNB = failure" presumption).
        bool pcmiFail = _riaActive && PeakFuelEnthalpyRiseCalPerG >= RiaPcmiRiseLimitCalPerG;
        bool dnbFail  = _riaActive && _power > 0.10 && MinDnbr < DnbrSafetyLimit;
        RiaCladdingFailure = pcmiFail || dnbFail;

        int failed = 0;
        if (RiaCladdingFailure)
            failed = (int)Math.Clamp(
                8.0 + (PeakFuelEnthalpyRiseCalPerG - RiaPcmiRiseLimitCalPerG) / RiaPcmiRiseLimitCalPerG * 120.0,
                1.0, 100.0);
        if (RiaCoolabilityViolated) failed = 100; // coolability lost → bounding failed-fuel population
        RiaFailedRodPercent = failed;

        SetAlarm(ReactorAlarm.RodEjectionAccident, _riaActive);
        SetAlarm(ReactorAlarm.FuelEnthalpyLimit, RiaCoolabilityViolated);
        SetAlarm(ReactorAlarm.RiaCladFailure, RiaCladdingFailure);
    }

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
        SetAlarm(ReactorAlarm.CondenserVacuumLow, CondenserVacuumLow);
        SetAlarm(ReactorAlarm.LowSubcooling, SubcoolingMarginC < 10.0 && (_power > 0.05 || Thot > 200));
        // Minimum DNBR (W-3) annunciators — gated above DnbrAlarmGate so an off-scale low-power reading never trips.
        bool dnbrActive = _power > DnbrAlarmGate;
        SetAlarm(ReactorAlarm.DnbrSafetyLimit, dnbrActive && MinDnbr < DnbrSafetyLimit);
        SetAlarm(ReactorAlarm.DnbrLowMargin,  dnbrActive && MinDnbr >= DnbrSafetyLimit && MinDnbr < DnbrLowMargin);
        // FSAR 15.3 loss-of-flow annunciators: the rotor-seizure status, and the locked-rotor acceptance FoM
        // (predicted rods in DNB above the ~5% Condition-IV limit → fuel failures assumed for dose).
        SetAlarm(ReactorAlarm.RcpLockedRotor, RcpLockedRotor);
        SetAlarm(ReactorAlarm.RodsInDnbHi, dnbrActive && RodsInDnbPercent > 5.0);
        SetAlarm(ReactorAlarm.DecayHeatHigh, DecayHeatFraction > 0.03 && CoolantFlowFraction < 0.4 && FeedwaterFlow < 0.1);
        SetAlarm(ReactorAlarm.AtwsActive, _rodsFailToInsert && IsScrammed);
        SetAlarm(ReactorAlarm.AmsacActuated, AmsacActuated);
        SetAlarm(ReactorAlarm.AccumulatorInject, AccumulatorInjecting);
        SetAlarm(ReactorAlarm.AuxFeedwater, AuxFeedwaterRunning);
        bool natCirc = CoolantFlowFraction > 0.005 && CoolantFlowFraction < 0.1 && Thot > 150;
        SetAlarm(ReactorAlarm.NaturalCirc, natCirc);
        SetAlarm(ReactorAlarm.SgtrLeak, _sgtrSeverity > 0 && SgtrLeakRate > 0.01);
        // RCP seal LOCA: cooling lost and the cavity has heated into a degraded leakoff bin (total > 4×normal).
        SetAlarm(ReactorAlarm.RcpSealLoca, !SealCoolingAvailable && SealLeakGpmTotal > 4.0 * SealLeakNormalGpm);
        // RCS operational LEAKAGE (LCO 3.4.13) + RG 1.45 detection-instrument annunciators (LCO 3.4.15).
        SetAlarm(ReactorAlarm.RcsUnidentifiedLeakHi,       UnidentifiedLeakGpm     > UnidLeakLimitGpm);   // > 1 gpm
        SetAlarm(ReactorAlarm.RcsIdentifiedLeakHi,         IdentifiedLeakGpm       > IdentLeakLimitGpm);  // > 10 gpm
        SetAlarm(ReactorAlarm.RcsPressureBoundaryLeak,     PressureBoundaryLeakGpm > PbLeakLimitGpm);     // NONE allowed
        SetAlarm(ReactorAlarm.ContainmentParticulateRadHi, ParticulateMonitorRatio >= 1.0);
        SetAlarm(ReactorAlarm.ContainmentGaseousRadHi,     GaseousMonitorRatio     >= 1.0);
        SetAlarm(ReactorAlarm.SteamlineBreak, _mslbSeverity > 0 && !MslbIsolated);
        SetAlarm(ReactorAlarm.SafetyInjection, SiActuated);
        SetAlarm(ReactorAlarm.PzrCodeSafetyOpen, AnyPzrCodeSafetyOpen);
        SetAlarm(ReactorAlarm.SecondaryRadiationHi, SecondaryActivity > 1.0);
        SetAlarm(ReactorAlarm.RcsDeI131LcoExceeded, RcsDeI131uCiPerG > LcoDeI131SteadyLimit);
        SetAlarm(ReactorAlarm.RcsDeI131SpikeLimit,  RcsDeI131uCiPerG > LcoDeI131SpikeLimit);
        SetAlarm(ReactorAlarm.RcsDeXe133LcoExceeded, RcsDeXe133uCiPerG > LcoDeXe133Limit);
        SetAlarm(ReactorAlarm.IodineSpikeInProgress, IodineSpikeActive);
        // Boron dilution: the credited subcritical detection is source-range count-rate doubling (flux rising
        // while shut down). The action-window alarm fires once time-to-loss-of-SDM drops below the 15-min SRP criterion.
        SetAlarm(ReactorAlarm.BoronDilution,
            _dilutionActive && SourceRangeEnergized && !IsScrammed
            && _power < 1.0 && SourceRangeCps > 2.0 * _dilutionCpsRef);
        SetAlarm(ReactorAlarm.BoronDilutionActionWindow, DilutionActionWindowViolated);
        SetAlarm(ReactorAlarm.SgReliefLift, SgReliefLifted);
        SetAlarm(ReactorAlarm.RodInsertionLimitLo, RilLowAlarm && !RilLowLowAlarm);
        SetAlarm(ReactorAlarm.RodInsertionLimitLoLo, RilLowLowAlarm);
        SetAlarm(ReactorAlarm.RodDeviation, RodDeviationAlarm);
        SetAlarm(ReactorAlarm.AxialFluxDiffOutOfBand, AfdOutsideBand);
        SetAlarm(ReactorAlarm.QuadrantPowerTiltHi, QptrOutOfLimit);          // LCO 3.2.4: QPTR > 1.02 above 50% RTP
        SetAlarm(ReactorAlarm.DroppedRcca, DroppedRodActive && _dropRamp > 0.5); // rod has reached the core bottom
        SetAlarm(ReactorAlarm.ContainmentPressureHi, _ctmtHi1);
        SetAlarm(ReactorAlarm.ContainmentIsolation, ContainmentIsolationPhaseA || ContainmentIsolationPhaseB);
        SetAlarm(ReactorAlarm.ContainmentSpray, ContainmentSprayActive);
        SetAlarm(ReactorAlarm.LossOfOffsitePower, !_elec.OffsitePowerAvailable);
        SetAlarm(ReactorAlarm.StationBlackout, _elec.InSbo);
        SetAlarm(ReactorAlarm.EdgSupplyingBus, _elec.OnEdgPower);
        SetAlarm(ReactorAlarm.TurbineDrivenAfw, _elec.TdafwRunning);
        SetAlarm(ReactorAlarm.DcBusDepleted, !_elec.DcAvailable);
        // LOCA core-uncovery / 10 CFR 50.46(b) acceptance-criteria alarms (instrumentation only).
        SetAlarm(ReactorAlarm.CoreUncovered, CoreExposedFrac > 0.05);
        SetAlarm(ReactorAlarm.PeakCladTempLimit, PeakCladTempC >= PctLimitC);
        SetAlarm(ReactorAlarm.CladOxidationLimit, MaxLocalOxidationPct >= EcrLimitPct);
        SetAlarm(ReactorAlarm.HydrogenGenerationLimit, CoreWideHydrogenPct >= H2LimitPct);
        // Containment combustible-gas control (10 CFR 50.44).
        SetAlarm(ReactorAlarm.ContainmentH2Flammable, ContainmentFlammable);
        SetAlarm(ReactorAlarm.ContainmentH2RegLimit, ContainmentH2Pct >= H2RegLimitPct);
        SetAlarm(ReactorAlarm.ContainmentH2Detonable, ContainmentDetonable);
        SetAlarm(ReactorAlarm.IgnitersEnergized, IgnitersEnergized);
        SetAlarm(ReactorAlarm.ContainmentDeflagration, DeflagrationOccurred);
    }

    // RCS Leakage Detection (LCO 3.4.13 / RG 1.45). Pure instrumentation: categorizes operational LEAKAGE,
    // integrates the containment normal-sump, infers the leak rate from the sump level-rate, drives the
    // particulate/gaseous atmosphere radiation monitors from the live coolant source term, and produces the
    // SR 3.4.13.1 inventory-balance estimate. Alarm latching is done centrally in UpdateAlarms (codebase
    // contract). Every dynamic state uses a clamped first-order relaxation, stable for any dt > 0.
    private void StepLeakDetection(double dt)
    {
        if (dt <= 0) return;

        // (1) Categorize leaks (gpm). Identified = RCP-seal leakoff above the normal recovered #1-seal
        //     bleed-off (4 pumps × 3 gpm, returned to the VCT — not LEAKAGE); the degraded excess is a known,
        //     collected source. Primary-to-secondary (SGTR) is a separate TS category and is excluded here.
        double pb = PressureBoundaryLeak ? PbLeakGpm : 0.0;
        PressureBoundaryLeakGpm = pb;
        UnidentifiedLeakGpm     = Math.Max(0.0, DemoUnidentifiedLeakGpm) + pb;
        IdentifiedLeakGpm       = Math.Max(0.0, SealLeakGpmTotal - 4.0 * SealLeakNormalGpm);

        // (2) Fill the containment normal sump from unidentified LEAKAGE (gpm → gal over dt).
        double dtMin = dt / 60.0;
        ContainmentSumpGal = Math.Clamp(ContainmentSumpGal + UnidentifiedLeakGpm * dtMin, 0.0, SumpCapacityGal);

        // (3) RG 1.45 sump channel — infer the leak rate from the level-rise rate BEFORE pumping down.
        double rawSumpRateGpm = (ContainmentSumpGal - _sumpPrevGal) / dtMin;   // gal/min
        _sumpPrevGal = ContainmentSumpGal;
        SumpInferredLeakGpm += (rawSumpRateGpm - SumpInferredLeakGpm) * Math.Min(1.0, dt / TauSumpSec);
        SumpInferredLeakGpm = Math.Max(0.0, SumpInferredLeakGpm);   // a pump-down cycle is not a negative leak

        // (4) Pump the sump out at the hi setpoint (hysteresis latch, anti-chatter).
        if (ContainmentSumpGal >= SumpHiSetpointGal)      _sumpPumpOn = true;
        else if (ContainmentSumpGal <= SumpLoSetpointGal) _sumpPumpOn = false;
        if (_sumpPumpOn)
            ContainmentSumpGal = Math.Max(0.0, ContainmentSumpGal - SumpPumpGpm * dtMin);

        // (5,6) Atmosphere radiation monitors — scale with the leak rate AND the live RCS specific activity.
        ParticulateMonitorRatio = ParticulateGain * UnidentifiedLeakGpm * RcsDeI131uCiPerG;   // I-131 particulate
        GaseousMonitorRatio     = GaseousGain     * UnidentifiedLeakGpm * RcsDeXe133uCiPerG;  // Xe-133 noble gas

        // (7) SR 3.4.13.1 RCS water-inventory balance — a slow filter of the TOTAL operational LEAKAGE.
        double totalLeak = UnidentifiedLeakGpm + IdentifiedLeakGpm;
        RcsInventoryBalanceLeakGpm += (totalLeak - RcsInventoryBalanceLeakGpm) * Math.Min(1.0, dt / TauInvBalanceSec);
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
