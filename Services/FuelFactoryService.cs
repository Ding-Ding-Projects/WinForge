using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 一個燃料組件（不可變記錄）· One fuel assembly record exposed to the UI. Now carries the full
/// ultra-realistic PWR-assembly metadata (lattice, manufacturer, lot, target burnup, fab chain).
/// </summary>
public sealed record FuelAssembly(
    string Id, double EnrichmentPct, double MassKgHM, DateTime FabDateUtc,
    double BurnupMwdPerTonne, string Status, string Path, bool SignatureValid,
    string Lattice, string Material, string Manufacturer, string Lot,
    double TargetBurnupMwdPerTonne, IReadOnlyList<string> FabChain);

/// <summary>燃料驗證結果（雙語原因）· Result of validating a fuel file, with a bilingual reason.</summary>
public sealed record ValidationResult(bool Valid, string Reason, string ReasonEn, string ReasonZh);

/// <summary>
/// 入料結果 · Result of a "send-in" / load operation. Reports whether the fuel file was consumed
/// (deleted from disk), so the UI can confirm the physical consumption of the assembly.
/// </summary>
public sealed record LoadResult(bool Loaded, bool FileDeleted, string Id,
    string ReasonEn, string ReasonZh);

/// <summary>
/// 燃料工廠服務 · The cryptographic, ULTRA-REALISTIC FUEL FACTORY.
///
/// Fabricates HMAC-SHA256-signed PWR fuel-assembly files modelling a real 17x17 UO2 assembly
/// (enrichment, kgU, lattice, manufacturer, fabrication lot, serial, target/accrued burnup, and the
/// yellowcake→UF6→enrichment→UO2 pelletizing→rod/assembly fabrication chain), validates their
/// authenticity, and rejects forged / tampered / depleted / spent / already-consumed assemblies.
/// The signing secret is a 32-byte random key persisted at rest with Windows DPAPI (CurrentUser).
///
/// PHYSICAL CONSUMPTION: loading ("sending in") a VALID assembly registers it in-core and then
/// AUTOMATICALLY DELETES the fresh-fuel file from disk — the fuel has been consumed into the core.
/// A forged/tampered/depleted/spent assembly is rejected and is NOT deleted.
///
/// Defense in depth: a used-assembly ledger (also HMAC-protected) refuses a replayed low-burnup copy
/// of an assembly that has already been loaded, and burnup accrual re-signs the on-disk in-core record
/// every tick so the recorded burnup can never be rolled back without the key.
/// </summary>
public sealed class FuelFactoryService
{
    private const string KeyId = "factory-v2";
    private const string Alg = "HMAC-SHA256";
    public const double DischargeThreshold = 50000.0; // MWd/tonne — depleted at/above this
    private const double CoreTonnesU = 100.0;         // mirrors ReactorSimService core mass

    // Realistic PWR 17x17 reference values.
    private const string DefaultLattice = "17x17";
    private const string DefaultMaterial = "UO2";
    private const string DefaultManufacturer = "WinForge Nuclear Fuels (OPEN100)";
    private const double DefaultTargetBurnup = 45000.0; // MWd/t typical discharge target

    private readonly string _root;       // <localappdata>/WinForge/reactor/fuel
    private readonly string _freshDir;
    private readonly string _spentDir;
    private readonly string _loadedDir;
    private readonly string _ledgerPath;
    private readonly string _keyPath;
    private readonly byte[] _secret;
    private readonly object _lock = new();

    public FuelFactoryService()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinForge", "reactor", "fuel");
        _root = baseDir;
        _freshDir = Path.Combine(baseDir, "fresh");
        _spentDir = Path.Combine(baseDir, "spent");
        _loadedDir = Path.Combine(baseDir, "loaded");
        _ledgerPath = Path.Combine(baseDir, "ledger.json");
        _keyPath = Path.Combine(baseDir, "factory.key");
        try
        {
            Directory.CreateDirectory(_freshDir);
            Directory.CreateDirectory(_spentDir);
            Directory.CreateDirectory(_loadedDir);
        }
        catch { /* best effort */ }
        _secret = LoadOrCreateKey();
    }

    /// <summary>在堆燃料目錄 · Where in-core assemblies live (used by the waste service to size waste).</summary>
    public string LoadedDir => _loadedDir;

    // ------------------------------------------------------------------ key mgmt ----
    private byte[] LoadOrCreateKey()
    {
        try
        {
            if (File.Exists(_keyPath))
            {
                var protectedBytes = File.ReadAllBytes(_keyPath);
                return System.Security.Cryptography.ProtectedData.Unprotect(
                    protectedBytes, null, DataProtectionScope.CurrentUser);
            }
        }
        catch { /* corrupt / undecryptable → regenerate (old fuel will simply read as forged) */ }

        var secret = RandomNumberGenerator.GetBytes(32);
        try
        {
            var prot = System.Security.Cryptography.ProtectedData.Protect(
                secret, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_keyPath, prot);
        }
        catch { /* best effort; in-memory key still works for this session */ }
        return secret;
    }

    // ------------------------------------------------------------ canonical signing ----
    /// <summary>
    /// 規範化負載 · Deterministic canonical string of the payload in a FIXED key order, invariant
    /// culture. Round-trippable numeric formatting ("R") so a re-read value re-signs identically.
    /// Every authenticity-bearing field is covered so tampering with ANY of them voids the signature.
    /// </summary>
    private static string Canonical(FuelPayload p)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("assemblyId=").Append(p.assemblyId).Append('\n');
        sb.Append("lattice=").Append(p.lattice).Append('\n');
        sb.Append("material=").Append(p.material).Append('\n');
        sb.Append("manufacturer=").Append(p.manufacturer).Append('\n');
        sb.Append("fabricationLot=").Append(p.fabricationLot).Append('\n');
        sb.Append("enrichmentU235Pct=").Append(p.enrichmentU235Pct.ToString("R", ci)).Append('\n');
        sb.Append("massKgHM=").Append(p.massKgHM.ToString("R", ci)).Append('\n');
        sb.Append("targetBurnupMwdPerTonne=").Append(p.targetBurnupMwdPerTonne.ToString("R", ci)).Append('\n');
        sb.Append("fabricationDateUtc=").Append(p.fabricationDateUtc).Append('\n');
        sb.Append("burnupMwdPerTonne=").Append(p.burnupMwdPerTonne.ToString("R", ci)).Append('\n');
        sb.Append("status=").Append(p.status);
        return sb.ToString();
    }

    private string Sign(FuelPayload p)
    {
        var bytes = Encoding.UTF8.GetBytes(Canonical(p));
        var mac = HMACSHA256.HashData(_secret, bytes);
        return Convert.ToBase64String(mac);
    }

    private bool VerifySig(FuelPayload p, string sig)
    {
        byte[] stored;
        try { stored = Convert.FromBase64String(sig); } catch { return false; }
        var bytes = Encoding.UTF8.GetBytes(Canonical(p));
        var computed = HMACSHA256.HashData(_secret, bytes);
        return CryptographicOperations.FixedTimeEquals(computed, stored);
    }

    // ------------------------------------------------------------------ file I/O ----
    private void WriteFile(string path, FuelPayload p)
    {
        var doc = new FuelFile { alg = Alg, keyId = KeyId, payload = p, sig = Sign(p) };
        var json = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static FuelFile? ReadFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<FuelFile>(json);
        }
        catch { return null; }
    }

    private static string[] FabChain(FuelPayload p)
    {
        var ci = CultureInfo.InvariantCulture;
        return new[]
        {
            "Yellowcake (U3O8) milling",
            "Conversion to UF6",
            $"Enrichment to {p.enrichmentU235Pct.ToString("0.##", ci)}% U-235 (centrifuge cascade)",
            "Reconversion + UO2 pelletizing & sintering",
            $"Rod loading & {p.lattice} assembly fabrication (lot {p.fabricationLot})",
        };
    }

    private FuelAssembly ToAssembly(string path, FuelFile f)
    {
        bool ok = f.payload is not null && VerifySig(f.payload, f.sig ?? "");
        var p = f.payload!;
        return new FuelAssembly(
            p.assemblyId, p.enrichmentU235Pct, p.massKgHM,
            DateTime.TryParse(p.fabricationDateUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d) ? d : DateTime.UtcNow,
            p.burnupMwdPerTonne, p.status ?? "fresh", path, ok,
            p.lattice ?? DefaultLattice, p.material ?? DefaultMaterial,
            p.manufacturer ?? DefaultManufacturer, p.fabricationLot ?? "",
            p.targetBurnupMwdPerTonne <= 0 ? DefaultTargetBurnup : p.targetBurnupMwdPerTonne,
            FabChain(p));
    }

    // ------------------------------------------------------------------ fabricate ----
    /// <summary>
    /// 製造一個寫實 PWR 17x17 燃料組件 · Fabricate one realistic 17x17 UO2 PWR assembly. The on-disk
    /// file stays SMALL (metadata + HMAC signature only); the bulk material is modelled, not stored.
    /// </summary>
    public FuelAssembly Fabricate(double enrichmentPct, double massKgHM)
    {
        lock (_lock)
        {
            // Real PWR fresh-fuel envelope: 3.0–4.95% LEU; ~460 kgU per 17x17 assembly.
            enrichmentPct = Math.Clamp(enrichmentPct, 3.0, 4.95);
            massKgHM = Math.Clamp(massKgHM, 400.0, 540.0);
            int serial = NextSerial();
            string id = $"OPEN100-{DefaultLattice}-{serial:D4}";
            var now = DateTime.UtcNow;
            var p = new FuelPayload
            {
                assemblyId = id,
                lattice = DefaultLattice,
                material = DefaultMaterial,
                manufacturer = DefaultManufacturer,
                fabricationLot = $"LOT-{now:yyyyMM}-{(serial / 24) + 1:D2}",
                enrichmentU235Pct = Math.Round(enrichmentPct, 2),
                massKgHM = Math.Round(massKgHM, 1),
                targetBurnupMwdPerTonne = DefaultTargetBurnup,
                fabricationDateUtc = now.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                burnupMwdPerTonne = 0.0,
                status = "fresh",
            };
            var path = Path.Combine(_freshDir, id + ".fuel");
            WriteFile(path, p);
            return ToAssembly(path, ReadFile(path)!);
        }
    }

    private int NextSerial()
    {
        int max = 0;
        foreach (var dir in new[] { _freshDir, _loadedDir, _spentDir })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var f in Directory.EnumerateFiles(dir, "*.fuel"))
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var bits = name.Split('-');
                if (bits.Length > 0 && int.TryParse(bits[^1], out var n)) max = Math.Max(max, n);
            }
        }
        return max + 1;
    }

    // ------------------------------------------------------------------ listings ----
    private IReadOnlyList<FuelAssembly> List(string dir)
    {
        var list = new List<FuelAssembly>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(dir, "*.fuel"))
            {
                var f = ReadFile(path);
                if (f?.payload is not null) list.Add(ToAssembly(path, f));
            }
        }
        catch { }
        return list.OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
    }

    public IReadOnlyList<FuelAssembly> ListFresh() => List(_freshDir);
    public IReadOnlyList<FuelAssembly> ListSpent() => List(_spentDir);
    public IReadOnlyList<FuelAssembly> ListLoaded() => List(_loadedDir);

    // ------------------------------------------------------------------ validate ----
    public ValidationResult Validate(string path)
    {
        var f = ReadFile(path);
        if (f?.payload is null)
            return new ValidationResult(false, "forged",
                "Unreadable or malformed fuel file.", "燃料檔案無法讀取或格式錯誤。");

        if (!VerifySig(f.payload, f.sig ?? ""))
            return new ValidationResult(false, "tampered",
                "Signature invalid — forged or tampered file.", "簽章無效 — 偽造或被竄改的檔案。");

        var p = f.payload;
        if (p.status == "spent")
            return new ValidationResult(false, "spent",
                "Assembly already discharged (spent).", "組件已退役（乏燃料）。");

        if (p.burnupMwdPerTonne >= DischargeThreshold)
            return new ValidationResult(false, "depleted",
                $"Burnup {p.burnupMwdPerTonne:F0} ≥ discharge limit {DischargeThreshold:F0} MWd/t.",
                $"燃耗 {p.burnupMwdPerTonne:F0} ≥ 退役限值 {DischargeThreshold:F0} MWd/t。");

        if (p.enrichmentU235Pct < 0.7 || p.enrichmentU235Pct > 20.0)
            return new ValidationResult(false, "enrichment",
                $"Enrichment {p.enrichmentU235Pct:F2}% out of sane range (0.7–20%).",
                $"濃度 {p.enrichmentU235Pct:F2}% 超出合理範圍（0.7–20%）。");

        if (LoadLedger().Contains(p.assemblyId) && p.status != "loaded")
            return new ValidationResult(false, "already-consumed",
                "Assembly id is in the used ledger (replay refused).", "組件編號已在使用記錄中（拒絕重放）。");

        return new ValidationResult(true, "ok", "Valid, authentic fuel.", "有效且可信的燃料。");
    }

    // ------------------------------------------------------------------ load ("send in") ----
    /// <summary>
    /// 入料（消耗） · "Send in" / load an assembly. On success, the assembly is registered in-core and
    /// its fresh-fuel file is AUTOMATICALLY DELETED from disk — physically consumed into the reactor.
    /// On rejection (forged / tampered / spent / depleted / replayed) NOTHING is loaded or deleted.
    /// </summary>
    public LoadResult LoadIntoCore(string path)
    {
        lock (_lock)
        {
            var v = Validate(path);
            if (!v.Valid)
                return new LoadResult(false, false, Path.GetFileNameWithoutExtension(path),
                    "Load refused — " + v.ReasonEn + " The fuel file was NOT deleted.",
                    "拒絕入料 — " + v.ReasonZh + " 燃料檔案未被刪除。");

            var f = ReadFile(path);
            if (f?.payload is null)
                return new LoadResult(false, false, Path.GetFileNameWithoutExtension(path),
                    "Load refused — unreadable file.", "拒絕入料 — 檔案無法讀取。");
            var p = f.payload;

            // Ledger the id (defense against replaying a low-burnup copy later), flip status, re-sign in-core.
            AppendLedger(p.assemblyId);
            p.status = "loaded";
            var dest = Path.Combine(_loadedDir, p.assemblyId + ".fuel");
            WriteFile(dest, p);

            // PHYSICAL CONSUMPTION: delete the source fresh-fuel file from disk.
            bool deleted = false;
            if (!string.Equals(Path.GetFullPath(dest), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                deleted = TryDelete(path);

            return new LoadResult(true, deleted, p.assemblyId,
                deleted
                    ? $"Assembly {p.assemblyId} consumed into the core — fresh-fuel file deleted from disk."
                    : $"Assembly {p.assemblyId} loaded into the core.",
                deleted
                    ? $"組件 {p.assemblyId} 已消耗入堆芯 — 新燃料檔案已從磁碟刪除。"
                    : $"組件 {p.assemblyId} 已裝入堆芯。");
        }
    }

    public bool UnloadFromCore(string assemblyId)
    {
        lock (_lock)
        {
            var path = Path.Combine(_loadedDir, assemblyId + ".fuel");
            var f = ReadFile(path);
            if (f?.payload is null) return false;
            var p = f.payload;
            p.status = "fresh";
            WriteFile(Path.Combine(_freshDir, assemblyId + ".fuel"), p);
            TryDelete(path);
            return true;
        }
    }

    // ------------------------------------------------------------------ burnup ----
    /// <summary>
    /// 累積燃耗 · Accrue burnup onto every loaded assembly each tick and re-sign on disk so the
    /// recorded burnup can never be rolled back without the signing key. The whole-core thermal
    /// energy is split across the loaded assemblies by mass. Any assembly crossing the discharge
    /// threshold is reported back as a "newly spent" event (used to mandate waste generation).
    /// Returns the list of assembly ids that became spent this call.
    /// </summary>
    public IReadOnlyList<string> AccrueBurnup(double thermalMW, double dtSeconds)
    {
        var newlySpent = new List<string>();
        if (thermalMW <= 0 || dtSeconds <= 0) return newlySpent;
        lock (_lock)
        {
            List<string> loaded;
            try { loaded = Directory.EnumerateFiles(_loadedDir, "*.fuel").ToList(); }
            catch { return newlySpent; }
            if (loaded.Count == 0) return newlySpent;

            double mwd = thermalMW * dtSeconds / 86400.0; // MWd for the whole core this tick
            double totalKg = 0;
            var docs = new List<(string path, FuelFile f)>();
            foreach (var path in loaded)
            {
                var f = ReadFile(path);
                if (f?.payload is null || !VerifySig(f.payload, f.sig ?? "")) continue; // skip tampered
                docs.Add((path, f));
                totalKg += f.payload.massKgHM;
            }
            if (totalKg <= 0) return newlySpent;

            foreach (var (path, f) in docs)
            {
                double tonnes = f.payload!.massKgHM / 1000.0;
                double share = f.payload.massKgHM / totalKg;
                if (tonnes <= 0) continue;
                double before = f.payload.burnupMwdPerTonne;
                f.payload.burnupMwdPerTonne += (mwd * share) / tonnes;
                WriteFile(path, f.payload);

                if (before < DischargeThreshold && f.payload.burnupMwdPerTonne >= DischargeThreshold)
                {
                    // Auto-discharge the now-spent assembly to the spent pool.
                    var p = f.payload;
                    p.status = "spent";
                    AppendLedger(p.assemblyId);
                    try
                    {
                        WriteFile(Path.Combine(_spentDir, p.assemblyId + ".fuel"), p);
                        TryDelete(path);
                    }
                    catch { }
                    newlySpent.Add(p.assemblyId);
                }
            }
        }
        return newlySpent;
    }

    /// <summary>在堆組件累積總燃耗（MWd/t），供廢料里程碑判斷 · Sum of accrued burnup over in-core assemblies.</summary>
    public double TotalLoadedBurnup()
    {
        lock (_lock)
        {
            double sum = 0;
            try
            {
                foreach (var path in Directory.EnumerateFiles(_loadedDir, "*.fuel"))
                {
                    var f = ReadFile(path);
                    if (f?.payload is not null) sum += f.payload.burnupMwdPerTonne;
                }
            }
            catch { }
            return sum;
        }
    }

    // ------------------------------------------------------------------ discharge ----
    public void DischargeAll()
    {
        lock (_lock)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(_loadedDir, "*.fuel").ToList())
                {
                    var f = ReadFile(path);
                    if (f?.payload is null) { TryDelete(path); continue; }
                    var p = f.payload;
                    p.status = "spent";
                    AppendLedger(p.assemblyId);
                    WriteFile(Path.Combine(_spentDir, p.assemblyId + ".fuel"), p);
                    TryDelete(path);
                }
            }
            catch { }
        }
    }

    public bool CanReactorRun
    {
        get
        {
            try
            {
                return Directory.EnumerateFiles(_loadedDir, "*.fuel")
                    .Select(ReadFile)
                    .Any(f => f?.payload is not null
                              && VerifySig(f.payload, f.sig ?? "")
                              && f.payload.status == "loaded"
                              && f.payload.burnupMwdPerTonne < DischargeThreshold);
            }
            catch { return false; }
        }
    }

    // ------------------------------------------------------------------ ledger ----
    private HashSet<string> LoadLedger()
    {
        try
        {
            if (!File.Exists(_ledgerPath)) return new HashSet<string>(StringComparer.Ordinal);
            var doc = JsonSerializer.Deserialize<LedgerFile>(File.ReadAllText(_ledgerPath));
            if (doc?.ids is null) return new HashSet<string>(StringComparer.Ordinal);
            // Verify ledger integrity; a tampered ledger is treated as empty (fail-safe: nothing trusted-used).
            var canonical = string.Join("\n", doc.ids.OrderBy(x => x, StringComparer.Ordinal));
            var mac = Convert.ToBase64String(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(canonical)));
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(mac), Encoding.UTF8.GetBytes(doc.sig ?? "")))
                return new HashSet<string>(StringComparer.Ordinal);
            return new HashSet<string>(doc.ids, StringComparer.Ordinal);
        }
        catch { return new HashSet<string>(StringComparer.Ordinal); }
    }

    private void AppendLedger(string id)
    {
        try
        {
            var set = LoadLedger();
            set.Add(id);
            var ids = set.OrderBy(x => x, StringComparer.Ordinal).ToList();
            var canonical = string.Join("\n", ids);
            var sig = Convert.ToBase64String(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(canonical)));
            File.WriteAllText(_ledgerPath, JsonSerializer.Serialize(new LedgerFile { ids = ids, sig = sig }));
        }
        catch { }
    }

    private static bool TryDelete(string path) { try { File.Delete(path); return true; } catch { return false; } }

    /// <summary>把相對／純檔名解析成受控燃料目錄內的絕對路徑 · Resolve a JS-supplied id/relative path
    /// to an absolute path strictly inside the managed fuel dir (no arbitrary filesystem access).</summary>
    public string? ResolvePath(string idOrPath)
    {
        if (string.IsNullOrWhiteSpace(idOrPath)) return null;
        string name = Path.GetFileName(idOrPath);
        if (!name.EndsWith(".fuel", StringComparison.OrdinalIgnoreCase)) name += ".fuel";
        foreach (var dir in new[] { _freshDir, _loadedDir, _spentDir })
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    // ------------------------------------------------------------------ DTO types ----
    private sealed class FuelFile
    {
        public string? alg { get; set; }
        public string? keyId { get; set; }
        public FuelPayload? payload { get; set; }
        public string? sig { get; set; }
    }

    private sealed class FuelPayload
    {
        public string assemblyId { get; set; } = "";
        public string lattice { get; set; } = DefaultLattice;
        public string material { get; set; } = DefaultMaterial;
        public string manufacturer { get; set; } = DefaultManufacturer;
        public string fabricationLot { get; set; } = "";
        public double enrichmentU235Pct { get; set; }
        public double massKgHM { get; set; }
        public double targetBurnupMwdPerTonne { get; set; } = DefaultTargetBurnup;
        public string fabricationDateUtc { get; set; } = "";
        public double burnupMwdPerTonne { get; set; }
        public string status { get; set; } = "fresh";
    }

    private sealed class LedgerFile
    {
        public List<string> ids { get; set; } = new();
        public string? sig { get; set; }
    }
}
