using System;

namespace WinForge.Services;

/// <summary>
/// 運算礦場 · Compute Mine — a heavy, reactor-powered compute/crypto load. This rig ONLY runs on
/// nuclear power: hashrate, earnings and efficiency are all gated on the reactor actually generating.
/// The operator sets a power draw (MW) and arms mining; hashrate = drawnMW * TH-per-MW, earnings
/// accrue at hashrate * price-per-TH-hour while generating, and a slow difficulty walk nudges price.
/// All state lives here; every method is defensive and NEVER throws. Deterministic — the only clock is
/// an internal integer tick counter, never DateTime.
/// </summary>
public sealed class ComputeMineService
{
    // Tuning constants.
    public const double MaxDrawMW = 600.0;          // slider ceiling
    private const double ThPerMW = 0.9;             // ~0.9 TH/s produced per MW drawn
    private const double BasePriceUsdPerThHour = 0.42; // baseline earnings rate per TH/s per hour
    private const double JoulesPerMWs = 1_000_000.0; // 1 MW for 1 s = 1e6 J

    // Reactor-economy wallet hook.
    public const string TurboPerkId = "perk.mine.turbo";  // Reactor-Bank perk → permanent +25% hashrate
    private const double TurboMultiplier = 1.25;
    private const double DepositThreshold = 1.0;          // deposit once un-banked earnings reach ≥ 1 ⚡
    private const double DepositEverySeconds = 3.0;        // …or at least once per ~3 s of mining

    private double _pendingDeposit;   // earnings earned but not yet banked into the Watts wallet
    private double _depositTimer;      // seconds of mining since the last wallet deposit
    private double _bankedTotalUsd;    // lifetime earnings already deposited (guards against double-count)

    private bool _mining;
    private long _ticks;                 // internal integer tick counter (sim clock)
    private double _difficulty = 1.00;   // slow random-ish walk, [0.6 .. 1.8]
    private int _diffDir = 1;
    private double _hashrateThs;         // current TH/s (0 when not generating)
    private double _drawnMW;             // MW actually consumed this tick
    private double _totalEarnedUsd;
    private double _totalThHashed;       // cumulative TH hashed (for lifetime stat)
    private bool _generating;            // last-known reactor-generating state

    /// <summary>Whether the operator has armed mining. Rigs still only run when the reactor generates.</summary>
    public bool Mining => _mining;

    /// <summary>Whether the reactor was generating on the last tick (rigs actually running).</summary>
    public bool Running => _mining && _generating && _hashrateThs > 0;

    public double HashrateThs => _hashrateThs;
    public double DrawnMW => _drawnMW;
    public double TotalEarnedUsd => _totalEarnedUsd;
    public double Difficulty => _difficulty;
    public long Ticks => _ticks;

    /// <summary>渦輪機組 · Whether the Reactor-Bank "turbo rigs" perk is owned (permanent +25% hashrate).</summary>
    public bool TurboActive
    {
        get
        {
            try { return ReactorEconomyService.I.IsUnlocked(TurboPerkId); }
            catch { return false; }
        }
    }

    /// <summary>Current spot price per TH/s per hour, softened by difficulty (higher difficulty = lower yield).</summary>
    public double PriceUsdPerThHour
    {
        get
        {
            double d = _difficulty <= 0.01 ? 0.01 : _difficulty;
            double p = BasePriceUsdPerThHour / d;
            return double.IsNaN(p) || double.IsInfinity(p) ? 0 : Math.Max(0, p);
        }
    }

    /// <summary>Energy efficiency in Joules per TH. Lower is better. 0 when idle.</summary>
    public double JoulesPerTh
    {
        get
        {
            if (_hashrateThs <= 0.0001) return 0;
            // Power (W) / hashrate (TH/s) = J per TH.
            double watts = _drawnMW * 1_000_000.0;
            double jth = watts / _hashrateThs;
            return double.IsNaN(jth) || double.IsInfinity(jth) ? 0 : jth;
        }
    }

    public void StartMining() => _mining = true;
    public void StopMining() { _mining = false; FlushDeposit(); }

    /// <summary>Cash out — bank nothing, just reset the running earnings counter.</summary>
    public void Sell() { FlushDeposit(); _totalEarnedUsd = 0; }

    /// <summary>
    /// 存入錢包 · Deposit any staged (un-banked) earnings into the shared reactor-backed Watts wallet.
    /// Guards against double-counting via a running banked total; never throws. Called on cadence from
    /// <see cref="Tick"/> (≥ 1 ⚡ or ~3 s) and on stop/sell so no earnings are stranded.
    /// </summary>
    private void FlushDeposit()
    {
        try
        {
            _depositTimer = 0;
            double delta = _pendingDeposit;
            if (double.IsNaN(delta) || double.IsInfinity(delta) || delta <= 0) { _pendingDeposit = 0; return; }
            _pendingDeposit = 0;
            _bankedTotalUsd += delta; // lifetime deposited (double-count guard / audit)
            ReactorEconomyService.I.Earn(delta, Loc.I.Pick("Compute Mine", "運算礦場"));
        }
        catch { _pendingDeposit = 0; }
    }

    /// <summary>Full reset — clears mining state, earnings, difficulty and the tick counter.</summary>
    public void Reset()
    {
        _mining = false;
        _ticks = 0;
        _difficulty = 1.00;
        _diffDir = 1;
        _hashrateThs = 0;
        _drawnMW = 0;
        _totalEarnedUsd = 0;
        _totalThHashed = 0;
        _generating = false;
        _pendingDeposit = 0;
        _depositTimer = 0;
        _bankedTotalUsd = 0;
    }

    /// <summary>
    /// Advance the simulation by <paramref name="dtSeconds"/> real seconds. <paramref name="requestedMW"/>
    /// is the operator's power-draw setpoint (clamped to [0, min(MaxDrawMW, availableMW)]).
    /// <paramref name="generating"/> reflects whether the reactor is actually producing power right now;
    /// when false the rigs are starved — hashrate and earnings both go to zero.
    /// </summary>
    public void Tick(double dtSeconds, double requestedMW, double availableMW, bool generating)
    {
        try
        {
            _ticks++;
            _generating = generating;

            double dt = double.IsNaN(dtSeconds) || dtSeconds < 0 ? 0 : Math.Min(dtSeconds, 2.0);

            // Slow difficulty walk driven by the integer tick counter (no wall-clock).
            if (_ticks % 8 == 0)
            {
                _difficulty += _diffDir * 0.03;
                if (_difficulty >= 1.8) { _difficulty = 1.8; _diffDir = -1; }
                else if (_difficulty <= 0.6) { _difficulty = 0.6; _diffDir = 1; }
            }

            // Clamp the operator's request to what's physically available.
            double avail = double.IsNaN(availableMW) || availableMW < 0 ? 0 : availableMW;
            double want = double.IsNaN(requestedMW) || requestedMW < 0 ? 0 : requestedMW;
            double cap = Math.Min(MaxDrawMW, avail);
            double target = Math.Max(0, Math.Min(want, cap));

            if (!_mining || !generating)
            {
                // Starved / disarmed: no draw, no hash, no earnings.
                _drawnMW = 0;
                _hashrateThs = 0;
                return;
            }

            // Spend-gated perk: owning the Reactor-Bank turbo perk permanently boosts hashrate +25%.
            double boost = TurboActive ? TurboMultiplier : 1.0;

            _drawnMW = target;
            _hashrateThs = Math.Max(0, target * ThPerMW * boost);

            // Earnings = hashrate (TH/s) * price (USD per TH/s per hour) * elapsed hours.
            double hours = dt / 3600.0;
            double earned = _hashrateThs * PriceUsdPerThHour * hours;
            if (!double.IsNaN(earned) && !double.IsInfinity(earned) && earned > 0)
            {
                _totalEarnedUsd += earned;
                _pendingDeposit += earned;   // stage for wallet deposit
            }

            double thisHash = _hashrateThs * dt; // TH hashed this tick
            if (!double.IsNaN(thisHash) && !double.IsInfinity(thisHash) && thisHash > 0)
                _totalThHashed += thisHash;

            if (_totalEarnedUsd > 1e12) _totalEarnedUsd = 1e12; // sanity cap

            // Deposit staged earnings into the shared Watts wallet in sensible increments —
            // when ≥ 1 ⚡ has accrued, or at least once per ~3 s of mining — never every tick.
            _depositTimer += dt;
            if (_pendingDeposit >= DepositThreshold || _depositTimer >= DepositEverySeconds)
                FlushDeposit();
        }
        catch
        {
            // Never throw from the sim.
            _drawnMW = 0;
            _hashrateThs = 0;
        }
    }
}
