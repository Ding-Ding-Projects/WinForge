using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.Services;

/// <summary>柴油券帳本讀取結果 · Outcome of reading or redeeming the shared voucher ledger.</summary>
public enum DieselLedgerStatus
{
    MissingFile,   // 未有帳本 · nobody has minted a voucher yet — an empty ledger, not an error
    Ok,            // 正常 · parsed fine
    ParseError,    // 解析失敗 · file exists but does not parse — preserved untouched, never overwritten
    NewerSchema,   // 較新版本 · schemaVersion above what this build understands — refuse rather than guess
}

/// <summary>帳本快照 · Immutable snapshot of the voucher ledger for UI and tests.</summary>
public readonly record struct DieselLedgerInfo(
    DieselLedgerStatus Status,
    int PendingVouchers,
    int PendingLitres,
    int ConsumedVouchers,
    int ConsumedLitres,
    string? Error);

/// <summary>兌換結果 · Result of one redeem pass over the ledger.</summary>
public readonly record struct DieselRedeemResult(
    DieselLedgerStatus Status,
    int VouchersConsumed,
    int LitresAdded,
    string? Error);

/// <summary>
/// 柴油券交易（消費端）· Consumer half of the Material Cookie Clicker diesel-voucher exchange.
///
/// Material Cookie Clicker spends cookies and appends vouchers to a shared per-user ledger:
///   %APPDATA%\DingDingProjects\exchange\diesel-vouchers.json
/// This service is the ONLY writer allowed to set <c>consumedAt</c>. It never deletes, reorders,
/// renumbers, or edits vouchers, never creates the ledger, and never overwrites a file it could
/// not first parse — a ledger that can forget is not evidence of anything. Writes go through a
/// temp file in the same directory and a single atomic rename (MoveFileEx REPLACE_EXISTING).
///
/// Redeemed litres feed the safety-grade EDG fuel tank in <see cref="ReactorElectrical"/>. The
/// tank starts EMPTY and there is no other fuel source: every litre the design-basis diesels burn
/// during LOOP/SBO was bought with cookies. The tank level and lifetime consumption counters are
/// persisted via <see cref="SettingsStore"/> so purchased fuel survives app restarts.
///
/// Pure managed C#: System.Text.Json only, no WinUI types, fully headless-testable via the
/// constructor's injectable ledger path and optional persistence.
/// </summary>
public sealed class DieselVoucherService
{
    public const int SupportedSchemaVersion = 1;
    public const string TankLitresSettingKey       = "reactor.edg.cookieFuelTankLitres";
    public const string LifetimeVouchersSettingKey = "reactor.edg.cookieVouchersConsumed";
    public const string LifetimeLitresSettingKey   = "reactor.edg.cookieLitresConsumed";

    private static readonly Lazy<DieselVoucherService> Shared = new(() => new DieselVoucherService());

    /// <summary>共用 runtime instance · Shared app-session instance bound to the real ledger path.</summary>
    public static DieselVoucherService I => Shared.Value;

    private readonly object _gate = new();
    private readonly string _ledgerPath;
    private readonly bool _persist;

    private double _tankLitres;          // authoritative persisted level (sim reports back each tick)
    private double _pendingSimLitres;    // litres redeemed but not yet injected into the live sim tank
    private double _lastPersistedLitres;
    private int _lifetimeVouchers;
    private int _lifetimeLitres;

    /// <summary>
    /// Runtime code uses <see cref="I"/>. Tests inject an isolated ledger path and disable the
    /// SettingsStore-backed persistence so scenarios stay deterministic and side-effect free.
    /// </summary>
    public DieselVoucherService(string? ledgerPath = null, bool persistTank = true)
    {
        _ledgerPath = ledgerPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DingDingProjects", "exchange", "diesel-vouchers.json");
        _persist = persistTank;
        if (_persist)
        {
            _tankLitres = ParseDouble(SettingsStore.Get(TankLitresSettingKey, "0"));
            _lifetimeVouchers = ParseInt(SettingsStore.Get(LifetimeVouchersSettingKey, "0"));
            _lifetimeLitres = ParseInt(SettingsStore.Get(LifetimeLitresSettingKey, "0"));
            _lastPersistedLitres = _tankLitres;
        }
    }

    public string LedgerPath => _ledgerPath;

    /// <summary>目前油缸存量 · Current EDG tank level in litres (persisted, cookie-bought only).</summary>
    public double TankLitres { get { lock (_gate) return _tankLitres; } }

    /// <summary>歷來兌換張數 · Lifetime vouchers this install has consumed (provenance counter).</summary>
    public int LifetimeVouchersConsumed { get { lock (_gate) return _lifetimeVouchers; } }

    /// <summary>歷來兌換公升 · Lifetime litres bought with cookies and redeemed here.</summary>
    public int LifetimeLitresConsumed { get { lock (_gate) return _lifetimeLitres; } }

    // ============================================================ read (never writes) ====
    /// <summary>讀取帳本 · Read-only ledger snapshot. A missing file is an empty ledger, not a fault.</summary>
    public DieselLedgerInfo ReadLedger()
    {
        string path = _ledgerPath;
        if (!File.Exists(path))
            return new DieselLedgerInfo(DieselLedgerStatus.MissingFile, 0, 0, 0, 0, null);

        try
        {
            var (status, root, error) = ParseLedger(File.ReadAllBytes(path));
            if (status != DieselLedgerStatus.Ok)
                return new DieselLedgerInfo(status, 0, 0, 0, 0, error);

            int pendingCount = 0, pendingLitres = 0, consumedCount = 0, consumedLitres = 0;
            foreach (var v in root!["vouchers"]!.AsArray())
            {
                int litres = (int)v!["litres"]!.GetValue<double>();
                if (IsConsumed(v)) { consumedCount++; consumedLitres += litres; }
                else { pendingCount++; pendingLitres += litres; }
            }
            return new DieselLedgerInfo(
                DieselLedgerStatus.Ok, pendingCount, pendingLitres, consumedCount, consumedLitres, null);
        }
        catch (Exception ex)
        {
            return new DieselLedgerInfo(DieselLedgerStatus.ParseError, 0, 0, 0, 0, ex.Message);
        }
    }

    // ============================================================ redeem (the only writer) ====
    /// <summary>
    /// 兌換全部未用券 · Stamp <c>consumedAt</c> on every pending voucher (in minting order), write
    /// the whole ledger back atomically, and credit the litres to the persisted EDG fuel tank.
    /// On any parse failure or newer schema the ledger is preserved byte-for-byte and nothing is
    /// consumed. A missing ledger or zero pending vouchers is a harmless no-op that writes nothing.
    /// </summary>
    public DieselRedeemResult RedeemAll()
    {
        lock (_gate)
        {
            string path = _ledgerPath;
            if (!File.Exists(path))
                return new DieselRedeemResult(DieselLedgerStatus.MissingFile, 0, 0, null);

            JsonObject root;
            try
            {
                var (status, parsed, error) = ParseLedger(File.ReadAllBytes(path));
                if (status != DieselLedgerStatus.Ok)
                    return new DieselRedeemResult(status, 0, 0, error);
                root = parsed!;
            }
            catch (Exception ex)
            {
                return new DieselRedeemResult(DieselLedgerStatus.ParseError, 0, 0, ex.Message);
            }

            // Stamp in memory only — vouchers keep their ids, order, and receipt strings untouched.
            string stamp = DateTimeOffset.UtcNow.UtcDateTime
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
            int vouchers = 0, litres = 0;
            foreach (var v in root["vouchers"]!.AsArray())
            {
                if (IsConsumed(v!)) continue;
                v!["consumedAt"] = stamp;
                vouchers++;
                litres += (int)v["litres"]!.GetValue<double>();
            }
            if (vouchers == 0)
                return new DieselRedeemResult(DieselLedgerStatus.Ok, 0, 0, null);

            // Whole-file temp+rename write: a crash leaves the previous ledger or the new one, never half.
            try
            {
                string json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
                string tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
                File.WriteAllBytes(tmp, Encoding.UTF8.GetBytes(json));
                File.Move(tmp, path, overwrite: true);
            }
            catch (Exception ex)
            {
                return new DieselRedeemResult(DieselLedgerStatus.ParseError, 0, 0,
                    "Ledger write failed: " + ex.Message);
            }

            _tankLitres += litres;
            _pendingSimLitres += litres;
            _lifetimeVouchers += vouchers;
            _lifetimeLitres += litres;
            PersistCore();
            return new DieselRedeemResult(DieselLedgerStatus.Ok, vouchers, litres, null);
        }
    }

    // ============================================================ live-sim bridge ====
    /// <summary>Sim start-up: the persisted tank level to seed <see cref="ReactorElectrical"/> with.</summary>
    public double LoadTankLitres() { lock (_gate) { _pendingSimLitres = 0; return _tankLitres; } }

    /// <summary>Atomically hands newly redeemed litres to the running sim (returns 0 when none).</summary>
    public double TakePendingLitres()
    {
        lock (_gate) { double p = _pendingSimLitres; _pendingSimLitres = 0; return p; }
    }

    /// <summary>Per-tick report of the live tank level; persisted only when it changed noticeably.</summary>
    public void ReportTankLitres(double litres)
    {
        if (!double.IsFinite(litres) || litres < 0) litres = 0;
        lock (_gate)
        {
            _tankLitres = litres;
            bool crossedEmpty = litres <= 0 && _lastPersistedLitres > 0;
            if (Math.Abs(litres - _lastPersistedLitres) >= 0.25 || crossedEmpty)
                PersistCore();
        }
    }

    // ============================================================ internals ====
    private void PersistCore()
    {
        _lastPersistedLitres = _tankLitres;
        if (!_persist) return;
        try
        {
            SettingsStore.Set(TankLitresSettingKey, _tankLitres.ToString("0.###", CultureInfo.InvariantCulture));
            SettingsStore.Set(LifetimeVouchersSettingKey, _lifetimeVouchers.ToString(CultureInfo.InvariantCulture));
            SettingsStore.Set(LifetimeLitresSettingKey, _lifetimeLitres.ToString(CultureInfo.InvariantCulture));
        }
        catch { }
    }

    /// <summary>
    /// Strict parse: returns Ok only when the document is an object with an integer schemaVersion
    /// ≤ 1 and a vouchers array whose entries all carry a positive whole litres value. Anything
    /// else is ParseError/NewerSchema and the caller must leave the file untouched.
    /// </summary>
    private static (DieselLedgerStatus Status, JsonObject? Root, string? Error) ParseLedger(byte[] bytes)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(bytes); }
        catch (Exception ex) { return (DieselLedgerStatus.ParseError, null, ex.Message); }

        if (node is not JsonObject root)
            return (DieselLedgerStatus.ParseError, null, "Ledger root is not a JSON object.");

        if (root["schemaVersion"] is not JsonValue sv || !sv.TryGetValue<double>(out double version))
            return (DieselLedgerStatus.ParseError, null, "Missing or non-numeric schemaVersion.");
        if (version > SupportedSchemaVersion)
            return (DieselLedgerStatus.NewerSchema, null,
                $"Ledger schemaVersion {version:0} is newer than supported version {SupportedSchemaVersion}.");

        if (root["vouchers"] is not JsonArray vouchers)
            return (DieselLedgerStatus.ParseError, null, "Missing vouchers array.");

        foreach (var v in vouchers)
        {
            if (v is not JsonObject o)
                return (DieselLedgerStatus.ParseError, null, "Voucher entry is not an object.");
            if (o["id"] is not JsonValue idv || idv.TryGetValue<string>(out var id) is false || string.IsNullOrWhiteSpace(id))
                return (DieselLedgerStatus.ParseError, null, "Voucher missing id.");
            if (o["litres"] is not JsonValue lv || !lv.TryGetValue<double>(out double litres)
                || litres <= 0 || litres != Math.Floor(litres))
                return (DieselLedgerStatus.ParseError, null, $"Voucher {id} has invalid litres.");
        }
        return (DieselLedgerStatus.Ok, root, null);
    }

    private static bool IsConsumed(JsonNode voucher)
        => voucher["consumedAt"] is JsonValue c
           && c.TryGetValue<string>(out var s)
           && !string.IsNullOrWhiteSpace(s);

    private static double ParseDouble(string s)
        => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
           && double.IsFinite(d) && d >= 0 ? d : 0;

    private static int ParseInt(string s)
        => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) && i >= 0 ? i : 0;
}
