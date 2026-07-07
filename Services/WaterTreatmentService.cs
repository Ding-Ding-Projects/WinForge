using System;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 超寫實補水處理廠 · ULTRA-REALISTIC makeup-water treatment plant (pure managed C#, no I/O).
///
/// Models the makeup-water train end to end, the way a real PWR makes the ultrapure demineralised
/// water its primary/secondary systems need:
///
///   raw water intake  ->  coagulation / clarifier  ->  multimedia filtration
///   ->  reverse osmosis (2-pass)  ->  mixed-bed ion-exchange demineraliser
///   ->  vacuum degasifier  ->  ULTRAPURE makeup-water storage tank
///
/// Tracks the chemistry that matters for reactor-grade water — conductivity (µS/cm; ultrapure target
/// &lt; 0.1), pH, dissolved oxygen (ppb), silica, chlorides — plus tank inventory/level, throughput
/// (L/min), RO-membrane fouling and ion-exchange resin saturation (needs regeneration).
///
/// It is stepped each reactor tick. The reactor's makeup demand (set by the window from
/// <see cref="ReactorSimService.MakeupDemandLpm"/>) draws DOWN the treated-water tank; the plant must
/// be run to refill it and keep chemistry in-spec, or the reactor cannot maintain level / corrodes.
///
/// All maths are bounded toy/engineering approximations tuned for plausibility and stability.
/// </summary>
public sealed class WaterTreatmentService
{
    // ----- tank -----
    public const double TankCapacityL = 200_000.0;     // 200 m³ ultrapure storage
    public double TankLevelL { get; private set; } = 140_000.0; // start ~70 %
    public double TankLevelPct => Math.Clamp(100.0 * TankLevelL / TankCapacityL, 0, 100);

    // ----- chemistry of the stored ultrapure product water -----
    public double ConductivityUScm { get; private set; } = 0.08; // µS/cm (ultrapure target < 0.1)
    public double Ph { get; private set; } = 7.0;
    public double DissolvedO2Ppb { get; private set; } = 5.0;    // ppb (degasified target < 10)
    public double SilicaPpb { get; private set; } = 3.0;         // ppb
    public double ChloridesPpb { get; private set; } = 2.0;      // ppb

    // ----- raw intake water quality (the dirty feed) -----
    private const double RawConductivity = 450.0;  // µS/cm raw
    private const double RawO2Ppb = 9000.0;        // ppb (air-saturated)
    private const double RawSilicaPpb = 12000.0;   // ppb
    private const double RawChloridesPpb = 40000.0;// ppb

    // ----- equipment state -----
    public bool IntakePumpOn { get; set; }
    public double IntakeRate { get; set; } = 0.7;       // 0..1 commanded intake rate
    public bool RoOn { get; set; }                      // reverse-osmosis trains
    public bool DegasifierOn { get; set; }              // vacuum degasifier
    public bool MakeupValveOpen { get; set; } = true;   // makeup-to-reactor isolation valve

    public double RoFouling { get; private set; }       // 0..1 membrane fouling (rises with throughput)
    public double ResinSaturation { get; private set; } // 0..1 ion-exchange saturation (needs regen)
    private double _regenTimer;                          // seconds remaining in a regeneration cycle
    public bool Regenerating => _regenTimer > 0;

    // ----- live flows (L/min) -----
    public double IntakeLpm { get; private set; }
    public double ProductLpm { get; private set; }      // ultrapure water produced into the tank
    public double MakeupDrawLpm { get; private set; }   // drawn by the reactor

    // ----- nominal capacities -----
    private const double MaxIntakeLpm = 1200.0;
    private const double RoRecovery = 0.75;             // 2-pass RO recovery fraction

    public bool LowTankAlarm => TankLevelPct < 12.0;
    public bool OffSpecAlarm => ConductivityUScm > 0.10 || DissolvedO2Ppb > 10.0
                                || ChloridesPpb > 5.0 || SilicaPpb > 10.0;

    /// <summary>開始混床再生（約 90 秒）· Begin a mixed-bed resin regeneration cycle (~90 s).</summary>
    public void Regenerate() { if (_regenTimer <= 0) _regenTimer = 90.0; }

    /// <summary>沖洗 RO 膜（手動降低結垢）· Flush/clean RO membranes (reduces fouling).</summary>
    public void FlushRo() { RoFouling = Math.Max(0, RoFouling - 0.5); }

    public void Reset()
    {
        TankLevelL = 140_000.0;
        ConductivityUScm = 0.08; Ph = 7.0; DissolvedO2Ppb = 5.0; SilicaPpb = 3.0; ChloridesPpb = 2.0;
        IntakePumpOn = false; IntakeRate = 0.7; RoOn = false; DegasifierOn = false; MakeupValveOpen = true;
        RoFouling = 0; ResinSaturation = 0; _regenTimer = 0;
        IntakeLpm = ProductLpm = MakeupDrawLpm = 0;
    }

    /// <summary>
    /// 推進處理廠一個 tick · Step the treatment plant by dt seconds. <paramref name="reactorDemandLpm"/>
    /// is the makeup demand from the reactor; <paramref name="reactorRunning"/> gates whether the
    /// reactor is actually drawing makeup (so a shut reactor doesn't drain the tank).
    /// </summary>
    public void Step(double dt, double reactorDemandLpm, bool reactorRunning)
    {
        if (dt <= 0) return;
        dt = Math.Min(dt, 1.0); // guard against long stalls

        // ---- regeneration timer ----
        if (_regenTimer > 0)
        {
            _regenTimer -= dt;
            if (_regenTimer <= 0) { _regenTimer = 0; ResinSaturation = 0; } // fresh resin
        }

        // ---- intake ----
        IntakeLpm = (IntakePumpOn ? Math.Clamp(IntakeRate, 0, 1) * MaxIntakeLpm : 0);

        // ---- production train: only makes product when intake + RO are running and resin isn't dead ----
        bool trainOk = IntakePumpOn && RoOn && !Regenerating && ResinSaturation < 0.98 && TankLevelL < TankCapacityL;
        double roThroughput = trainOk ? IntakeLpm * RoRecovery * (1.0 - 0.6 * RoFouling) : 0;
        ProductLpm = Math.Max(0, roThroughput);

        // ---- tank inventory ----
        MakeupDrawLpm = (MakeupValveOpen && reactorRunning) ? Math.Max(0, reactorDemandLpm) : 0;
        double netLpm = ProductLpm - MakeupDrawLpm;          // L/min
        TankLevelL = Math.Clamp(TankLevelL + netLpm * (dt / 60.0), 0, TankCapacityL);

        // ---- equipment wear ----
        // RO membranes foul slowly with throughput; ion-exchange resin saturates as it polishes water.
        if (ProductLpm > 0)
        {
            RoFouling = Math.Clamp(RoFouling + 1.5e-5 * (ProductLpm / 100.0) * dt, 0, 1);
            ResinSaturation = Math.Clamp(ResinSaturation + 2.0e-4 * (ProductLpm / 100.0) * dt, 0, 1);
        }

        // ---- product-water chemistry ----
        // Each stage removes a fraction of impurity. With the full train running and fresh resin the
        // product is ultrapure; as resin saturates, polishing degrades and conductivity/ions climb.
        double polish = trainOk ? (1.0 - ResinSaturation) : 0.0; // 1 = perfect, 0 = no polishing
        // Target chemistry the running train drives the STORED water toward.
        double targetCond, targetO2, targetSi, targetCl, targetPh;
        if (trainOk)
        {
            // RO (2-pass) + mixed bed: residual = raw * (1 - removalEfficiency).
            double ixResidual = 0.0002 + 0.05 * (1.0 - polish); // worse as resin saturates
            targetCond = Math.Max(0.055, RawConductivity * ixResidual / 1000.0); // µS/cm
            targetSi = RawSilicaPpb * (0.0003 + 0.04 * (1.0 - polish));
            targetCl = RawChloridesPpb * (0.0001 + 0.03 * (1.0 - polish));
            targetO2 = DegasifierOn ? 5.0 : RawO2Ppb * 0.02; // degasifier strips O2 to a few ppb
            targetPh = 7.0;
        }
        else
        {
            // Train down: stored water slowly drifts back toward raw quality (ingress / equilibration).
            targetCond = RawConductivity * 0.10;
            targetSi = RawSilicaPpb * 0.10;
            targetCl = RawChloridesPpb * 0.10;
            targetO2 = RawO2Ppb * 0.10;
            targetPh = 7.4;
        }

        // First-order approach; faster when actively producing/turning over the tank.
        double k = Math.Min(1.0, dt / (trainOk ? 12.0 : 120.0));
        ConductivityUScm += (targetCond - ConductivityUScm) * k;
        SilicaPpb += (targetSi - SilicaPpb) * k;
        ChloridesPpb += (targetCl - ChloridesPpb) * k;
        DissolvedO2Ppb += (targetO2 - DissolvedO2Ppb) * Math.Min(1.0, dt / (DegasifierOn ? 8.0 : 60.0));
        Ph += (targetPh - Ph) * Math.Min(1.0, dt / 30.0);

        // Bound everything sanely.
        ConductivityUScm = Math.Clamp(ConductivityUScm, 0.055, RawConductivity);
        SilicaPpb = Math.Clamp(SilicaPpb, 0, RawSilicaPpb);
        ChloridesPpb = Math.Clamp(ChloridesPpb, 0, RawChloridesPpb);
        DissolvedO2Ppb = Math.Clamp(DissolvedO2Ppb, 0, RawO2Ppb);
        Ph = Math.Clamp(Ph, 4.0, 10.0);
    }

    /// <summary>
    /// 供應充足度（0..1）· Availability the reactor should see: how well-supplied makeup is, driven by
    /// tank level (and the makeup valve). Above ~25 % level = ample (1.0); falls to 0 as the tank empties.
    /// </summary>
    public double Availability()
    {
        if (!MakeupValveOpen) return 0.0;
        double pct = TankLevelPct;
        if (pct >= 25.0) return 1.0;
        return Math.Clamp(pct / 25.0, 0, 1);
    }

    /// <summary>水質達標 · True when the stored makeup water is in-spec for reactor use.</summary>
    public bool InSpec() => !OffSpecAlarm;

    /// <summary>匯出處理廠狀態為 JSON · Flat JSON snapshot for the Water Treatment HTML5 room.</summary>
    public string ExportStateJson()
    {
        var dto = new
        {
            type = "water",
            tankLevelL = Math.Round(TankLevelL, 0),
            tankLevelPct = Math.Round(TankLevelPct, 1),
            tankCapacityL = TankCapacityL,
            conductivity = Math.Round(ConductivityUScm, 4),
            ph = Math.Round(Ph, 2),
            o2ppb = Math.Round(DissolvedO2Ppb, 1),
            silicappb = Math.Round(SilicaPpb, 1),
            chloridesppb = Math.Round(ChloridesPpb, 1),
            intakePumpOn = IntakePumpOn,
            intakeRate = IntakeRate,
            roOn = RoOn,
            degasifierOn = DegasifierOn,
            makeupValveOpen = MakeupValveOpen,
            roFouling = Math.Round(RoFouling, 3),
            resinSaturation = Math.Round(ResinSaturation, 3),
            regenerating = Regenerating,
            intakeLpm = Math.Round(IntakeLpm, 0),
            productLpm = Math.Round(ProductLpm, 0),
            makeupDrawLpm = Math.Round(MakeupDrawLpm, 0),
            lowTankAlarm = LowTankAlarm,
            offSpecAlarm = OffSpecAlarm,
            inSpec = InSpec(),
        };
        return JsonSerializer.Serialize(dto);
    }
}
