using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>賬簿一行 · One ledger line (earn or spend).</summary>
public sealed class WattLedgerEntry
{
    public string When { get; set; } = "";     // display timestamp (stamped by caller / persistence)
    public string Reason { get; set; } = "";    // bilingual reason text
    public double Amount { get; set; }          // + earn, − spend
    public double Balance { get; set; }         // running balance after this entry

    public string AmountText => (Amount >= 0 ? "+" : "−") + Math.Abs(Amount).ToString("N1", CultureInfo.InvariantCulture) + " ⚡";
}

/// <summary>
/// 反應堆經濟：一種由核電「鑄造」出嚟嘅貨幣 · WinForge's reactor-backed economy. A single shared currency —
/// <b>Watts (⚡)</b> — is MINTED from the flagship reactor's live electrical output and EARNED from the
/// reactor-powered industrial loads (grid sales, mining, smelting, hydrogen, compute…). It can be SPENT to
/// unlock perks/upgrades, so some features literally require power you generated. Persisted via SettingsStore;
/// thread-safe; raises <see cref="Changed"/> on every balance change. Pure managed C#, never throws.
/// </summary>
public sealed class ReactorEconomyService
{
    public static ReactorEconomyService I { get; } = new();

    public const string Symbol = "⚡";
    public static string CurrencyName => Loc.I.Pick("Watts", "瓦特幣");

    // Mint rate: Watts minted per MWe per second while the reactor is generating. 1150 MWe ⇒ ~11.5 ⚡/s.
    public const double MintPerMWSecond = 0.00001;

    private const string KeyBalance = "economy.watts.balance";
    private const string KeyLedger = "economy.watts.ledger";
    private const string KeyUnlocks = "economy.watts.unlocks";

    private readonly object _gate = new();
    private double _balance;
    private readonly List<WattLedgerEntry> _ledger = new();
    private readonly HashSet<string> _unlocks = new(StringComparer.OrdinalIgnoreCase);
    private double _mintAccumulator; // sub-unit mint carry so tiny per-tick mints aren't lost

    /// <summary>任何餘額改變都會觸發（UI 更新用）· Raised on any balance/unlock change.</summary>
    public event Action? Changed;

    private ReactorEconomyService() { Load(); }

    public double Balance { get { lock (_gate) return _balance; } }

    public IReadOnlyList<WattLedgerEntry> Ledger
    {
        get { lock (_gate) return _ledger.AsEnumerable().Reverse().ToList(); } // newest first
    }

    /// <summary>賺取貨幣（會入賬）· Earn currency and record a ledger line.</summary>
    public void Earn(double amount, string reason)
    {
        if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0) return;
        lock (_gate)
        {
            _balance += amount;
            AddLedger(amount, reason);
            Save();
        }
        Raise();
    }

    /// <summary>由反應堆功率靜默鑄幣（唔逐格入賬）· Silently mint from live reactor power (no per-tick ledger spam).
    /// Returns the amount minted this call. Call each UI tick with the live snapshot values.</summary>
    public double MintFromPower(double electricMW, double dtSeconds, bool generating)
    {
        if (!generating || electricMW <= 1 || dtSeconds <= 0) return 0;
        if (double.IsNaN(electricMW) || double.IsNaN(dtSeconds)) return 0;
        double minted = electricMW * MintPerMWSecond * Math.Clamp(dtSeconds, 0, 5);
        if (minted <= 0) return 0;
        lock (_gate)
        {
            _balance += minted;
            _mintAccumulator += minted;
            // Persist periodically (every whole ⚡ of minting) to avoid hammering settings each tick.
            if (_mintAccumulator >= 1.0) { _mintAccumulator = 0; Save(); }
        }
        Raise();
        return minted;
    }

    /// <summary>可否負擔 · Whether the balance can cover a cost.</summary>
    public bool CanAfford(double cost) { lock (_gate) return _balance >= cost; }

    /// <summary>花費（成功先扣數）· Spend if affordable; records a ledger line. Returns false when too poor.</summary>
    public bool TrySpend(double cost, string reason)
    {
        if (cost <= 0) return true;
        lock (_gate)
        {
            if (_balance < cost) return false;
            _balance -= cost;
            AddLedger(-cost, reason);
            Save();
        }
        Raise();
        return true;
    }

    // ─────────────── unlockable perks/features (require currency) ───────────────

    public bool IsUnlocked(string perkId) { lock (_gate) return _unlocks.Contains(perkId); }

    /// <summary>用貨幣解鎖一個功能（一次性）· Buy a one-time feature unlock; false if already owned or too poor.</summary>
    public bool Unlock(string perkId, double cost, string reason)
    {
        lock (_gate)
        {
            if (_unlocks.Contains(perkId)) return true;
            if (_balance < cost) return false;
            _balance -= cost;
            _unlocks.Add(perkId);
            AddLedger(-cost, reason);
            Save();
        }
        Raise();
        return true;
    }

    // ─────────────── internals ───────────────

    private void AddLedger(double amount, string reason)
    {
        _ledger.Add(new WattLedgerEntry
        {
            When = DateTime.Now.ToString("MM-dd HH:mm:ss"),
            Reason = reason,
            Amount = amount,
            Balance = _balance,
        });
        // keep the last 200 lines
        if (_ledger.Count > 200) _ledger.RemoveRange(0, _ledger.Count - 200);
    }

    private void Raise() { try { Changed?.Invoke(); } catch { } }

    private void Load()
    {
        try
        {
            _balance = double.TryParse(SettingsStore.Get(KeyBalance, "0"), NumberStyles.Any, CultureInfo.InvariantCulture, out var b) ? b : 0;
            var lj = SettingsStore.Get(KeyLedger, "");
            if (!string.IsNullOrWhiteSpace(lj))
            {
                var list = JsonSerializer.Deserialize<List<WattLedgerEntry>>(lj);
                if (list is not null) { _ledger.Clear(); _ledger.AddRange(list); }
            }
            var uj = SettingsStore.Get(KeyUnlocks, "");
            if (!string.IsNullOrWhiteSpace(uj))
            {
                var u = JsonSerializer.Deserialize<List<string>>(uj);
                if (u is not null) foreach (var s in u) _unlocks.Add(s);
            }
        }
        catch { /* fresh economy on any parse issue */ }
    }

    private void Save()
    {
        try
        {
            SettingsStore.Set(KeyBalance, _balance.ToString("R", CultureInfo.InvariantCulture));
            SettingsStore.Set(KeyLedger, JsonSerializer.Serialize(_ledger));
            SettingsStore.Set(KeyUnlocks, JsonSerializer.Serialize(_unlocks.ToList()));
        }
        catch { /* best-effort persistence */ }
    }
}
