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

        _ => (0, 100, Array.Empty<GaugeBand>(), Array.Empty<GaugeSetpoint>()),
    };

    /// <summary>
    /// 趨近臨界啟動程序清單 · The ordered approach-to-criticality checklist.
    /// </summary>
    public static StartupStep[] StartupSequence() => new[]
    {
        new StartupStep
        {
            En = "Start ≥3 reactor coolant pumps",
            Zh = "啟動 ≥3 部主泵",
            IsSatisfied = s => { int n = 0; foreach (var r in s.RcpRunning) if (r) n++; return n >= 3; },
        },
        new StartupStep
        {
            En = "Establish primary flow > 85%",
            Zh = "建立一迴路流量 > 85%",
            IsSatisfied = s => s.CoolantFlowFraction > 0.85,
        },
        new StartupStep
        {
            En = "Pressurizer heaters on, pressure → ~2235 psia",
            Zh = "穩壓器加熱器開，壓力 → 約 2235 psia",
            IsSatisfied = s => s.PressurizerHeater && s.PrimaryPressure > 14.5,
        },
        new StartupStep
        {
            En = "Pull shutdown banks / dilute boron toward ECP",
            Zh = "提起停堆棒組／稀釋硼至估算臨界位置",
            IsSatisfied = s => { double avg = 0; foreach (var p in s.RodBankInsertion) avg += p; avg /= s.RodBankInsertion.Length; return avg < 60 || s.BoronPpm < 1000; },
        },
        new StartupStep
        {
            En = "Watch source-range count-rate, 1/M → 0",
            Zh = "監察起動範圍計數率，1/M → 0",
            IsSatisfied = s => s.OneOverM < 0.25,
        },
        new StartupStep
        {
            En = "Declare criticality at stable positive period > 30 s",
            Zh = "於穩定正週期 > 30 秒時宣布臨界",
            IsSatisfied = s => s.NeutronPowerFraction > 1e-3 && s.ReactorPeriodSeconds > 30 && s.ReactorPeriodSeconds < 1e8,
        },
        new StartupStep
        {
            En = "Raise power, sync turbine, close generator breaker",
            Zh = "升功率、汽輪機併網、合發電機開關",
            IsSatisfied = s => s.GeneratorBreakerClosed && s.ElectricPowerMW > 1.0,
        },
    };
}
