using System;

namespace WinForge.Services;

/// <summary>
/// 數位反應性計算機（反應性計）· Digital inverse-point-kinetics REACTIVITY COMPUTER (reactimeter).
///
/// <para>EN — A real plant instrument used in startup physics testing (rod-worth, boron-worth, MTC/ITC,
/// rod-drop, rod-swap and Dynamic Rod Worth Measurement). It reconstructs the net core reactivity ρ(t)
/// <b>purely from the measured neutron-flux signal n(t)</b> by INVERTING the point-kinetics equations —
/// it must NOT read the engine's internally computed reactivity. Because it solves the same six-group
/// kinetics independently (its own reconstructed delayed-neutron precursors, its own filtered flux
/// derivative), it tracks but does not exactly echo the true ρ — a small, authentic dynamic lag during
/// fast transients is expected, exactly as a field reactimeter behaves. That independence is the whole
/// point: it lets the operator <i>measure</i> an unknown rod/boron worth from the flux response alone.</para>
///
/// <para>粵語 — 真實電廠用嘅儀器，喺起動物理試驗（量度棒價值、硼價值、慢化劑／等溫溫度係數、落棒、換棒同
/// 動態棒價值量度 DRWM）用到。佢淨係由量到嘅中子通量 n(t)，反轉點動力學方程式去重建堆芯淨反應性 ρ(t)，
/// 唔會去讀引擎內部已知嘅反應性。佢獨立解返一套六羣動力學（自己重建緩發中子先驅核、自己濾波求導數），
/// 所以會跟住真 ρ 行但唔會完全一樣 —— 喺快暫態時有少少真實嘅動態滯後，同現場反應性計一模一樣。
/// 正正因為佢獨立，先可以淨靠通量響應去「量度」未知嘅棒／硼價值。</para>
///
/// Inverse point kinetics (same normalization as the engine's forward 6-group solver):
///   forward:  dn/dt = ((ρ−β)/Λ*)·n + Σ λ_i C_i ,   dC_i/dt = (β_i/Λ*)·n − λ_i C_i
///   inverse:  ρ(t) = β_eff + (Λ*/n)·dn/dt − (Λ*/n)·Σ λ_i C_i
/// At any steady critical state (dn/dt = 0, C_i = β_i/(Λ*·λ_i)·n) this returns ρ = 0 by construction.
/// Refs: Lamarsh &amp; Baratta ch.7; Hetrick, Dynamics of Nuclear Reactors; Keepin/Tamura reactimeter;
/// ANSI/ANS-19.6.1 (digital reactivity-computer parameters must match the core).
/// </summary>
public sealed class ReactivityComputer
{
    private readonly double[] _beta;       // BOL group delayed fractions (engine's Beta[])
    private readonly double[] _lambda;     // group decay constants 1/s (engine's Lambda[])
    private readonly double _promptLife;   // Λ*, prompt generation time (s)
    private readonly double _betaTotalBol; // Σ β_i at BOL
    private readonly double[] _c = new double[6]; // reconstructed precursors (engine normalization)

    // Display / derivative low-pass filters. Λ* is tiny (2e-5 s) so the prompt term (Λ*/n)·dn/dt is
    // noise-dominated; the flux must be filtered before differencing or ρ jitters violently.
    private const double FluxFilterTau = 0.8;   // s — input flux EMA for a clean derivative
    private const double RhoFilterTau  = 0.5;   // s — output ρ display smoothing (never on the precursor state)
    private const double RateDeadband  = 1.0e-4; // 1/s — |d ln n/dt| below this → "steady", period → ∞
    private const double FloorFraction = 1.0e-9; // n floor for division guards (≈ source level)
    private const double SurPerInvSec  = 26.05568; // 60/ln(10): Startup-Rate DPM per (1/T), T in s

    private double _nFilt = 1.0, _nFiltPrev = 1.0, _nPrev = 1.0;
    private double _rhoFilt;            // smoothed reactivity, Δk/k
    private double _markPcm;            // physics-test reference baseline (pcm)
    private bool   _hasMark;

    /// <summary>反應性（pcm）· Measured net reactivity reconstructed from flux, pcm (1 pcm = 1e-5 Δk/k).</summary>
    public double ReactivityPcm { get; private set; }
    /// <summary>反應性（元）· Measured reactivity in dollars (ρ / β_eff). 1 $ = β_eff = prompt critical.</summary>
    public double ReactivityDollars { get; private set; }
    /// <summary>反應堆週期（秒，帶正負號）· Asymptotic reactor period from the flux rate; + rising, − falling.</summary>
    public double PeriodSeconds { get; private set; } = 1e9;
    /// <summary>起動率（每分鐘十倍，帶號）· Startup Rate, decades/min = 60/(T·ln10); + rising, − falling.</summary>
    public double StartupRateDpm { get; private set; }
    /// <summary>正週期警報 · Positive-rate (short positive period) advisory: SUR above the alarm setpoint.</summary>
    public bool PositiveRateAlarm { get; private set; }
    /// <summary>量度價值（pcm）· Integrated reactivity since the operator marked a reference (rod/boron worth).</summary>
    public double MeasuredWorthPcm => _hasMark ? ReactivityPcm - _markPcm : 0.0;
    /// <summary>量度價值（元）· Marked-reference reactivity change in dollars.</summary>
    public double MeasuredWorthDollars { get; private set; }
    /// <summary>已標記參考 · True once a physics-test reference baseline has been captured.</summary>
    public bool HasMark => _hasMark;

    private const double PositiveRateAlarmDpm = 1.0; // advisory: > +1 DPM is a brisk positive transient

    public ReactivityComputer(double[] beta, double[] lambda, double promptLifetime)
    {
        _beta = (double[])beta.Clone();
        _lambda = (double[])lambda.Clone();
        _promptLife = promptLifetime;
        double s = 0; foreach (var b in _beta) s += b;
        _betaTotalBol = s;
    }

    /// <summary>Seed the reconstructed precursors to the assumed-critical steady state for the current flux and
    /// cycle β scale, so the meter reads ρ ≈ 0 at any equilibrium power. Clears any physics-test mark.</summary>
    public void Reset(double n0, double betaCycleFactor)
    {
        double n = Math.Max(n0, FloorFraction);
        for (int i = 0; i < 6; i++)
            _c[i] = betaCycleFactor * _beta[i] / (_promptLife * _lambda[i]) * n;
        _nFilt = _nFiltPrev = _nPrev = n;
        _rhoFilt = 0; ReactivityPcm = 0; ReactivityDollars = 0;
        PeriodSeconds = 1e9; StartupRateDpm = 0; PositiveRateAlarm = false;
        _hasMark = false; _markPcm = 0; MeasuredWorthDollars = 0;
    }

    /// <summary>Capture the current measured reactivity as the baseline for a worth measurement (rod-drop /
    /// rod-swap / boron-dilution). MeasuredWorthPcm then reads the integrated change from here.</summary>
    public void Mark() { _markPcm = ReactivityPcm; _hasMark = true; }

    /// <summary>Clear the physics-test reference baseline.</summary>
    public void ClearMark() { _hasMark = false; _markPcm = 0; MeasuredWorthDollars = 0; }

    /// <summary>
    /// Advance one tick from the measured flux only. <paramref name="nMeasured"/> is the indicated neutron
    /// power fraction (NIS power-range signal); <paramref name="betaCycleFactor"/> scales β BOL→EOL exactly
    /// as the engine does; <paramref name="sourceFloor"/> is the neutron-source level used as the division floor.
    /// </summary>
    public void Update(double nMeasured, double dt, double betaCycleFactor, double sourceFloor)
    {
        if (dt < 1.0e-4) return; // derivative guard (mirrors the engine's rate-sample minimum)

        double floor = Math.Max(FloorFraction, sourceFloor);
        double n1 = Math.Max(nMeasured, floor);
        double n0 = Math.Max(_nPrev, floor);
        double betaEff = _betaTotalBol * betaCycleFactor;

        // --- precursor reconstruction: analytic exponential integrator, piecewise-LINEAR source (n0→n1) ---
        // Exact for a linearly-varying flux over the step; L-stable and non-negative for ANY dt, and removes
        // the half-step lag a piecewise-constant update leaves during fast ramps (rod drop / DRWM).
        double precursorSum = 0.0;
        for (int i = 0; i < 6; i++)
        {
            double x = _lambda[i] * dt;
            double om = OneMinusExp(x);              // 1 − e^(−λΔt), cancellation-safe near 0
            double e  = 1.0 - om;                    // e^(−λΔt)
            double srcCoef = betaCycleFactor * _beta[i] / (_promptLife * _lambda[i]);
            // linear-source weight: equilibrium target n1, minus the ramp lag (n1−n0)·(1−e)/(λΔt)
            double drive = n1 - (n1 - n0) * (om / x);
            _c[i] = _c[i] * e + srcCoef * om * drive;
            if (_c[i] < 0) _c[i] = 0;
            precursorSum += _lambda[i] * _c[i];
        }

        // --- filtered flux derivative (prompt-jump term) ---
        double aFlux = Math.Min(1.0, dt / FluxFilterTau);
        _nFilt += (nMeasured - _nFilt) * aFlux;
        double nDivPrompt = Math.Max(_nFilt, floor);
        double dndt = (_nFilt - _nFiltPrev) / dt;     // derivative of the SMOOTHED signal
        _nFiltPrev = _nFilt;

        // --- inverse point kinetics: ρ = β_eff + (Λ*/n)·(dn/dt − Σ λ_i C_i) ---
        double rho = betaEff + (_promptLife / nDivPrompt) * (dndt - precursorSum);

        double aRho = Math.Min(1.0, dt / RhoFilterTau);
        _rhoFilt += (rho - _rhoFilt) * aRho;          // smooth the DISPLAYED ρ only
        ReactivityPcm = _rhoFilt * 1.0e5;
        ReactivityDollars = (betaEff > 1e-9) ? _rhoFilt / betaEff : 0.0;
        MeasuredWorthDollars = (betaEff > 1e-9) ? MeasuredWorthPcm * 1e-5 / betaEff : 0.0;

        // --- asymptotic period & startup rate from the logarithmic flux rate ---
        double dlnn = dndt / nDivPrompt;              // d(ln n)/dt
        if (Math.Abs(dlnn) < RateDeadband)
        {
            PeriodSeconds = (dlnn >= 0 ? 1.0 : -1.0) * 1e9; // effectively infinite / steady
            StartupRateDpm = 0.0;
            PositiveRateAlarm = false;
        }
        else
        {
            PeriodSeconds = 1.0 / dlnn;               // signed: + rising, − falling
            StartupRateDpm = SurPerInvSec * dlnn;     // 60/ln10 · d(ln n)/dt
            PositiveRateAlarm = StartupRateDpm > PositiveRateAlarmDpm;
        }

        _nPrev = nMeasured;
    }

    // 1 − e^(−x), accurate near 0 (avoids catastrophic cancellation in 1 − exp(−x)).
    private static double OneMinusExp(double x)
    {
        if (x < 1e-5) return x * (1.0 - 0.5 * x * (1.0 - x / 3.0)); // x − x²/2 + x³/6
        return 1.0 - Math.Exp(-x);
    }
}
