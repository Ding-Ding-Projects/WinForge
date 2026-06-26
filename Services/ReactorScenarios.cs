using System;

namespace WinForge.Services;

/// <summary>
/// 設計基準事故情景 · Design-basis accident scenarios the operator can inject.
/// </summary>
public enum ReactorScenario
{
    Normal,            // 正常 · no abnormal condition
    Loca,              // 失水事故 · loss-of-coolant accident (break)
    StationBlackout,   // 全廠斷電 · loss of all AC power (SBO)
    LossOfFeedwater,   // 喪失給水 · loss of feedwater (LOFW)
    Atws,              // 未能緊急停堆之預期暫態 · anticipated transient without scram
    XenonRestart,      // 氙毒重啟 · post-trip xenon transient / iodine pit
    SgTubeRupture,     // 蒸發器爆管 · steam-generator tube rupture (primary→secondary leak)
    MainSteamLineBreak,// 主蒸汽管爆裂 · main steam line break (secondary blowdown → primary overcooling)
    RcpSealLoca,       // 主泵軸封失水 · RCP seal LOCA — loss of all seal cooling → degraded seal leakoff (WOG-2000)
    RodEjection,       // 彈棒事故 · rod ejection accident (RIA/REA, FSAR Ch 15.4.8) — CRDM failure ejects an RCCA
    BoronDilution,     // 失控硼稀釋 · uncontrolled boron dilution (FSAR Ch 15.4.6) — unborated water added to RCS → +reactivity
    RccaWithdrawal,    // 失控提棒 · uncontrolled RCCA bank withdrawal (FSAR Ch 15.4.1 HZP / 15.4.2 at-power) — a control bank is driven out at maximum drive speed (72 spm); reactivity is inserted purely through rodRho and the event is terminated emergently by the RPS (Power-Range/IR Hi-Flux, OTΔT/OPΔT) — no scripted trip
    CompleteLossOfFlow,// 全喪失強制流量 · complete loss of forced flow (FSAR Ch 15.3.2) — all 4 RCPs trip, coast on flywheels
    LockedRotor,       // 主泵卡軸 · RCP rotor seizure / locked rotor (FSAR Ch 15.3.3) — one loop flow→0 instantly, no coastdown
    LossOfFeedwaterHeating, // 喪失給水加熱 · loss of feedwater heating (FSAR Ch 15.1.1) — HP heater string trips → colder feedwater → secondary overcooling → +reactivity via MTC
    LossOfComponentCoolingWater, // 喪失設備冷卻水 · loss of component cooling water (CCW) — header heats up → letdown isolation + loss of RCP thermal-barrier cooling → seal heatup (LCO 3.7.7)
}

/// <summary>一條儀表限值帶 · One coloured limit band on a gauge / strip chart.</summary>
public sealed class GaugeBand
{
    public double Lo;
    public double Hi;
    public string Kind = "normal"; // "normal" | "warn" | "danger"
    public GaugeBand(double lo, double hi, string kind) { Lo = lo; Hi = hi; Kind = kind; }
}

/// <summary>一個設定點標記（雙語）· A labelled setpoint tick (bilingual).</summary>
public readonly record struct GaugeSetpoint(double V, string En, string Zh);

/// <summary>啟動程序步驟（雙語＋判定）· A startup-sequence checklist step.</summary>
public sealed class StartupStep
{
    public string En = "";
    public string Zh = "";
    public string ControlEn = "";
    public string ControlZh = "";
    public string ControlTarget = "";
    public string ControlRoom = "control";
    public Func<ReactorSimService, bool> IsSatisfied = _ => false;
}

/// <summary>
/// 反應堆工程單位限值帶、設定點同啟動程序 · Engineering-unit limit bands, setpoints and a live
/// approach-to-criticality checklist, kept out of the sim core to avoid bloating it.
/// </summary>
public static class ReactorScenarios
{
    private static GaugeBand[] Bands(params (double lo, double hi, string kind)[] b)
    {
        var arr = new GaugeBand[b.Length];
        for (int i = 0; i < b.Length; i++) arr[i] = new GaugeBand(b[i].lo, b[i].hi, b[i].kind);
        return arr;
    }

    private static GaugeSetpoint[] Sets(params (double v, string en, string zh)[] s)
    {
        var arr = new GaugeSetpoint[s.Length];
        for (int i = 0; i < s.Length; i++) arr[i] = new GaugeSetpoint(s[i].v, s[i].en, s[i].zh);
        return arr;
    }

    /// <summary>
    /// 取得儀表規格（量程、限值帶、設定點）· Range + bands + setpoints for a gauge id.
    /// Units shown to the operator are the realistic engineering units (psia, °C, etc).
    /// </summary>
    public static (double min, double max, GaugeBand[] bands, GaugeSetpoint[] sets) Spec(string id) => id switch
    {
        "power" => (0, 120,
            Bands((0, 100, "normal"), (100, 109, "warn"), (109, 120, "danger")),
            Sets((109, "Hi-flux trip", "高通量跳機"))),

        // Pressurizer pressure in psia (1 MPa = 145.038 psia). Operating band ~2235 psia.
        "pzrPress" => (0, 3000,
            Bands((0, 1860, "danger"), (1860, 2100, "warn"), (2100, 2335, "normal"),
                  (2335, 2485, "warn"), (2485, 3000, "danger")),
            Sets((1860, "Lo-press trip", "低壓跳機"), (2335, "PORV", "釋壓閥"), (2485, "Code safety", "安全閥"))),

        "tavg" => (530, 620,
            Bands((530, 557, "warn"), (557, 590, "normal"), (590, 620, "warn")),
            Sets((588, "HFP Tavg", "滿載 Tavg"))),

        "thot" => (530, 660,
            Bands((530, 560, "warn"), (560, 620, "normal"), (620, 660, "danger")),
            Sets((653, "Hi-Thot trip", "熱腿高溫跳機"))),

        "tcold" => (520, 600,
            Bands((520, 545, "warn"), (545, 575, "normal"), (575, 600, "warn")),
            Array.Empty<GaugeSetpoint>()),

        "sgLevel" => (0, 100,
            Bands((0, 17, "danger"), (17, 35, "warn"), (35, 75, "normal"), (75, 90, "warn"), (90, 100, "danger")),
            Sets((17, "Lo-lo SG trip", "蒸發器低低水位跳機"))),

        "sgPress" => (0, 1300,
            Bands((0, 900, "normal"), (900, 1100, "warn"), (1100, 1300, "danger")),
            Sets((1185, "MSSV lift", "主蒸汽安全閥"))),

        "pzrLevel" => (0, 100,
            Bands((0, 17, "danger"), (17, 30, "warn"), (30, 70, "normal"), (70, 92, "warn"), (92, 100, "danger")),
            Array.Empty<GaugeSetpoint>()),

        "fuelTemp" => (0, 3000,
            Bands((0, 1000, "normal"), (1000, 1204, "warn"), (1204, 3000, "danger")),
            Sets((1204, "Clad damage", "包殼受損"), (2865, "UO₂ melt", "二氧化鈾熔點"))),

        "boron" => (0, 2500,
            Bands((0, 2500, "normal")),
            Array.Empty<GaugeSetpoint>()),

        // Core-mixing-region boric-acid concentration (ppm B) for post-LOCA long-term cooling. The danger band's
        // lower edge tracks the temperature-dependent solubility limit Cs(T) ≈ 48,000 ppm B at 100 °C; the warn
        // band opens at 90% of Cs (the ES-1.4 hot-leg-recirc action window). Reads ~1200 ppm (well-mixed RCS)
        // until a LOCA boil-off concentrates it. 10 CFR 50.46(b)(5) long-term cooling.
        "coreBoron" => (0, 60000,
            Bands((0, 34000, "normal"), (34000, 43300, "warn"), (43300, 60000, "danger")),
            Sets((43300, "ES-1.4 hot-leg recirc", "ES-1.4 熱段再循環"), (48125, "Solubility Cs(100°C)", "溶解度極限 Cs(100°C)"))),

        // Hours to boric-acid precipitation (the ES-1.4 hot-leg-recirc operator-action window). Danger < 2 h,
        // warn 2–4 h, green beyond. Reads 8 (off-scale) when the precipitation model is idle.
        "timeToPrecip" => (0, 8,
            Bands((0, 2, "danger"), (2, 4, "warn"), (4, 8, "normal")),
            Sets((2, "2 h action", "2 小時行動"))),

        // Peak radial-average fuel enthalpy (cal/g) — the rod-ejection (RIA) figure of merit. Green below
        // the RG 1.236 230 cal/g coolability/no-melt limit; warn into the 230–280 band; danger past the
        // legacy RG 1.77 280 cal/g (incipient fuel-melt) line.
        "fuelEnth" => (0, 300,
            Bands((0, 150, "normal"), (150, 230, "warn"), (230, 300, "danger")),
            Sets((230, "Coolability (RG 1.236)", "可冷卻性 RG 1.236"), (280, "Fuel melt (RG 1.77)", "燃料熔化 RG 1.77"))),

        // Quadrant Power Tilt Ratio (LCO 3.2.4): 1.00 = flat radial power, ≤1.02 within limit, >1.02 = action.
        "qptr" => (0.95, 1.15,
            Bands((0.95, 1.02, "normal"), (1.02, 1.06, "warn"), (1.06, 1.15, "danger")),
            Sets((1.02, "QPTR limit (LCO 3.2.4)", "QPTR 限值 LCO 3.2.4"))),

        // 125 VDC vital station battery state-of-charge (%). Below ~20 % the end-of-discharge floor and
        // loss of vital instrumentation / TDAFW control loom — the battery-death endgame of an SBO.
        "battery" => (0, 100,
            Bands((0, 20, "danger"), (20, 50, "warn"), (50, 100, "normal")),
            Sets((20, "EoD floor", "放電終止"))),

        "xenon" => (0, 100,
            Bands((0, 100, "normal")),
            Array.Empty<GaugeSetpoint>()),

        "flow" => (0, 100,
            Bands((0, 85, "danger"), (85, 95, "warn"), (95, 100, "normal")),
            Sets((85, "Lo-flow trip", "低流量跳機"))),

        "period" => (0, 100,
            Bands((0, 18, "danger"), (18, 40, "warn"), (40, 100, "normal")),
            Sets((18, "Short-period trip", "週期過短跳機"))),

        "subcool" => (-20, 120,
            Bands((-20, 0, "danger"), (0, 15, "warn"), (15, 120, "normal")),
            Sets((0, "Saturation", "飽和"))),

        // Axial flux difference (ΔI, %RTP). CAOC target band ±5 % is green; ±5–15 % warns; beyond ±15 %
        // is danger (matches the OTΔT f₁(ΔI) penalty onset). Signed: negative = bottom-peaked (rods in).
        "afd" => (-30, 30,
            Bands((-30, -15, "danger"), (-15, -5, "warn"), (-5, 5, "normal"), (5, 15, "warn"), (15, 30, "danger")),
            Sets((-5, "CAOC lo", "目標帶下限"), (5, "CAOC hi", "目標帶上限"))),

        // Peak cladding temperature (°C). 10 CFR 50.46(b)(1) acceptance limit = 1204.4 °C (2200 °F).
        "cladTemp" => (300, 2500,
            Bands((300, 800, "normal"), (800, 1204.4, "warn"), (1204.4, 2500, "danger")),
            Sets((1204.4, "PCT limit 2200°F", "PCT 限值 2200°F"), (800, "Zr-steam onset", "鋯水反應起始"))),

        // Collapsed core liquid level over the active fuel (%). Below ~5 % the core is fully uncovered.
        "coreLevel" => (0, 100,
            Bands((0, 5, "danger"), (5, 95, "warn"), (95, 100, "normal")),
            Sets((5, "Top of active fuel", "燃料活性段頂"))),

        // Local cladding oxidation — Equivalent Cladding Reacted (%). 50.46(b)(2) limit = 17 %.
        "ecr" => (0, 30,
            Bands((0, 10, "normal"), (10, 17, "warn"), (17, 30, "danger")),
            Sets((17, "17% ECR limit", "17% ECR 限值"))),

        // Core-wide hydrogen generation (% of the all-clad-reacted inventory). 50.46(b)(3) limit = 1 %.
        "h2" => (0, 3,
            Bands((0, 0.5, "normal"), (0.5, 1.0, "warn"), (1.0, 3, "danger")),
            Sets((1.0, "1% H₂ limit", "1% 氫氣限值"))),

        // RCP seal leakoff (total, gpm). 4-loop WOG-2000 bins: normal ~12 gpm, intact-hot 84, popped 728,
        // gross seal failure 1920 gpm (≈2-inch SBLOCA). Setpoints mark the per-pump-bin 4-loop totals.
        "sealLeak" => (0, 1920,
            Bands((0, 84, "normal"), (84, 728, "warn"), (728, 1920, "danger")),
            Sets((84, "21 gpm/pump (intact)", "每泵21加侖（完好）"), (728, "182 gpm/pump (popped)", "每泵182加侖（彈出）"),
                 (1920, "480 gpm/pump (gross)", "每泵480加侖（全失效）"))),

        // 10 CFR 50 Appendix G P/T-limit margin (MPa): negative = brittle-fracture limit exceeded.
        "ptMargin" => (-2, 13,
            Bands((-2, 0, "danger"), (0, 1, "warn"), (1, 13, "normal")),
            Sets((0, "App G limit", "附錄G 限值"))),

        // RCS heatup/cooldown rate (°C/hr); App G limit ±55.6 °C/hr (= ±100 °F/hr).
        "rcsRate" => (-60, 60,
            Bands((-60, -55.56, "danger"), (-55.56, -50, "warn"), (-50, 50, "normal"),
                  (50, 55.56, "warn"), (55.56, 60, "danger")),
            Sets((-55.56, "−100 °F/hr", "−100 °F/小時"), (55.56, "+100 °F/hr", "+100 °F/小時"))),

        // LTOP/COMS status: 0 disarmed · 1 armed · 2 relieving.
        "ltop" => (0, 2,
            Bands((0, 1, "normal"), (1, 2, "warn")),
            Sets((1, "Armed", "已致動"), (2, "Relieving", "洩放中"))),

        // Boron-dilution operator-action window (minutes to loss of shutdown margin). FSAR 15.4.6 / SRP
        // criterion: ≥15 min from the source-range alarm to total loss of SDM (Modes 1–5). Danger below
        // 15 min, warn 15–30 min (Mode-6 refueling criterion), green beyond.
        "dilutionWindow" => (0, 60,
            Bands((0, 15, "danger"), (15, 30, "warn"), (30, 60, "normal")),
            Sets((15, "15-min criterion", "15分鐘準則"), (30, "30-min (Mode 6)", "30分鐘（模式6）"))),

        // ---- Main generator electrical (4-pole, 60 Hz, 1300 MVA / 0.90 PF / 24 kV synchronous machine) ----
        // Reactive power (MVAR). Capability curve: overexcited (lagging) +567 MVAR rotor/field-heating limit;
        // underexcited (leading) −350 MVAR end-core/stability limit. Rated operating point ≈ +557 MVAR.
        "genMvar" => (-400, 600,
            Bands((-400, -350, "danger"), (-350, 0, "warn"), (0, 567, "normal"), (567, 600, "danger")),
            Sets((-350, "Underexc limit", "欠勵限值"), (567, "Overexc limit", "過勵限值"))),

        // Terminal voltage (kV). AVR holds 24 kV (1.0 p.u.); 27 trips at 0.80 p.u. (19.2 kV), 59 at 1.10 p.u.
        "genKv" => (20, 27,
            Bands((20, 22.8, "danger"), (22.8, 23.5, "warn"), (23.5, 24.5, "normal"), (24.5, 26.4, "warn"), (26.4, 27, "danger")),
            Sets((19.2, "27 UV", "低壓"), (26.4, "59 OV", "過壓"))),

        // Power factor (cos φ). 0.90 lagging is the rated overexcited limit; green near unity / lagging.
        "genPf" => (0.80, 1.00,
            Bands((0.80, 0.88, "warn"), (0.88, 1.00, "normal")),
            Sets((0.90, "Rated PF", "額定功率因數"))),

        // Grid frequency (Hz). 60 ± 0.05 normal; 81U trips 57.5 Hz, 81O trips 62.0 Hz (turbine-blade fatigue).
        "genHz" => (57, 63,
            Bands((57, 57.5, "danger"), (57.5, 59.5, "warn"), (59.5, 60.5, "normal"), (60.5, 62, "warn"), (62, 63, "danger")),
            Sets((57.5, "81U", "低頻"), (62.0, "81O", "過頻"))),

        // Field (excitation) current (p.u.). 1.0 at rated; ceiling ~2.6 on forcing; overexcitation cue >1.1.
        "genIfd" => (0, 2.6,
            Bands((0, 0.4, "warn"), (0.4, 1.1, "normal"), (1.1, 2.6, "warn")),
            Sets((1.0, "Rated field", "額定勵磁"))),

        // Final feedwater temperature (°F). Full-load design ~440 °F; loss of an HP heater string (FSAR 15.1.1)
        // drops it ~50 °F. Part-load operation is normally below 440 °F, so only flag a gross low-temperature.
        "fwTemp" => (80, 480,
            Bands((80, 360, "warn"), (360, 460, "normal"), (460, 480, "warn")),
            Sets((440, "Full-load FW temp", "滿載給水溫度"), (390, "1 HP htr lost", "失一台高壓加熱器"))),

        _ => (0, 100, Array.Empty<GaugeBand>(), Array.Empty<GaugeSetpoint>()),
    };

    /// <summary>
    /// 趨近臨界啟動程序清單 · The ordered approach-to-criticality checklist.
    /// </summary>
    public static StartupStep[] StartupSequence() => new[]
    {
        new StartupStep
        {
            En = "Select Startup mode",
            Zh = "選擇啟動模式",
            ControlEn = "Mode & automation -> Reactor mode: Startup.",
            ControlZh = "模式與自動化 -> 反應堆模式：啟動。",
            ControlTarget = "mode-automation",
            ControlRoom = "control",
            IsSatisfied = s => s.Mode == ReactorMode.Startup || s.Mode == ReactorMode.Run,
        },
        new StartupStep
        {
            En = "Start ≥3 reactor coolant pumps",
            Zh = "啟動 ≥3 部主泵",
            ControlEn = "Primary system -> Reactor coolant pumps: turn on RCP 1-4 until at least three are on.",
            ControlZh = "一迴路系統 -> 反應堆冷卻劑泵：開啟主泵 1-4，至少三部要開。",
            ControlTarget = "primary-system",
            ControlRoom = "contain",
            IsSatisfied = s => { int n = 0; foreach (var r in s.RcpRunning) if (r) n++; return n >= 3; },
        },
        new StartupStep
        {
            En = "Establish primary flow > 85%",
            Zh = "建立一迴路流量 > 85%",
            ControlEn = "Primary system -> RCP flow demand (%): raise the slider, then verify the RCP flow gauge.",
            ControlZh = "一迴路系統 -> 主泵流量需求（%）：調高滑桿，然後確認主泵流量儀表。",
            ControlTarget = "primary-system",
            ControlRoom = "contain",
            IsSatisfied = s => s.CoolantFlowFraction > 0.85,
        },
        new StartupStep
        {
            En = "Pressurizer heaters on, pressure → ~2235 psia",
            Zh = "穩壓器加熱器開，壓力 → 約 2235 psia",
            ControlEn = "Primary system -> Pressurizer & relief: leave Auto press ctrl on and turn Heater on; watch the Pressurizer pressure gauge.",
            ControlZh = "一迴路系統 -> 穩壓器與釋壓：保持自動壓力開啟並開加熱器；監察穩壓器壓力儀表。",
            ControlTarget = "primary-system",
            ControlRoom = "contain",
            IsSatisfied = s => s.PressurizerHeater && s.PrimaryPressure > 14.5,
        },
        new StartupStep
        {
            En = "Pull shutdown banks / dilute boron toward ECP",
            Zh = "提起停堆棒組／稀釋硼至估算臨界位置",
            ControlEn = "Reactor controls -> Control rod bank A-D, Soluble boron target, and CVCS makeup blender mode (Dilute / Alternate dilute).",
            ControlZh = "反應堆控制 -> 控制棒組 A-D、硼濃度目標、化容系統補水混合器模式（稀釋／交替稀釋）。",
            ControlTarget = "reactor-controls",
            ControlRoom = "control",
            IsSatisfied = s => { double avg = 0; foreach (var p in s.RodBankInsertion) avg += p; avg /= s.RodBankInsertion.Length; return avg < 60 || s.BoronPpm < 1000; },
        },
        new StartupStep
        {
            En = "Watch source-range count-rate, 1/M → 0",
            Zh = "監察起動範圍計數率，1/M → 0",
            ControlEn = "Use the Nuclear instrumentation (NIS), 1/M plot, and reactimeter period/SUR readouts.",
            ControlZh = "使用核儀表（NIS）、1/M 圖、反應性儀週期／起動率讀數。",
            ControlTarget = "nis",
            ControlRoom = "control",
            IsSatisfied = s => s.OneOverM < 0.25,
        },
        new StartupStep
        {
            En = "Declare criticality at stable positive period > 30 s",
            Zh = "於穩定正週期 > 30 秒時宣布臨界",
            ControlEn = "Use the reactimeter period/SUR readouts and reactor power gauge; hold rod/boron changes steady.",
            ControlZh = "使用反應性儀週期／起動率讀數及反應堆功率儀表；保持棒位／硼濃度變更穩定。",
            ControlTarget = "nis",
            ControlRoom = "control",
            IsSatisfied = s => s.NeutronPowerFraction > 1e-3 && s.ReactorPeriodSeconds > 30 && s.ReactorPeriodSeconds < 1e8,
        },
        new StartupStep
        {
            En = "Raise power, sync turbine, close generator breaker",
            Zh = "升功率、汽輪機併網、合發電機開關",
            ControlEn = "Secondary & turbine -> Turbine load setpoint, Grid synchronization -> Generator breaker, and optional Sync interlock (25).",
            ControlZh = "二迴路與汽輪機 -> 汽輪機負載設定、併網 -> 發電機開關，以及可選同步聯鎖（25）。",
            ControlTarget = "secondary-turbine",
            ControlRoom = "turbine",
            IsSatisfied = s => s.GeneratorBreakerClosed && s.ElectricPowerMW > 1.0,
        },
    };

    public static int CompletedStartupSteps(IReadOnlyList<StartupStep> steps, ReactorSimService sim)
    {
        int done = 0;
        foreach (var step in steps)
        {
            if (!step.IsSatisfied(sim)) break;
            done++;
        }
        return done;
    }
}
