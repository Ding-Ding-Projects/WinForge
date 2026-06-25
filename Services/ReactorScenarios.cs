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
