using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

public sealed record CakeFileRecord(
    string CakeId,
    string RecipeName,
    string RecipeNameZh,
    string LotId,
    int CakeNumber,
    int BatchSize,
    DateTime BakedUtc,
    DateTime ExpiresUtc,
    double QualityScore,
    double SanitationScore,
    string OriginDevice,
    string IssuerKeyId,
    string Path,
    bool SignatureValid);

public sealed record CakeValidationResult(bool Valid, string Reason, string ReasonEn, string ReasonZh, string CakeId = "");

public sealed record CakeEatResult(bool Eaten, bool FileDeleted, string CakeId, string ReasonEn, string ReasonZh);

/// <summary>
/// Portable signed cake-file service. It creates one .cake file per packed cake, signs it with an
/// ECDSA private key, embeds the public key for portable verification, and requires the verifier to
/// trust that public key. Eating a valid cake consumes the physical file by deleting it.
/// </summary>
public sealed class CakeFileService
{
    private const string Schema = "winforge.cake.v1";
    private const string Alg = "ECDSA-P256-SHA256";

    private readonly string _root;
    private readonly string _cakeDir;
    private readonly string _keyDir;
    private readonly string _privateKeyPath;
    private readonly string _publicKeyPath;
    private readonly string _trustedPath;
    private readonly string _eatenLedgerPath;
    private readonly ECDsa _privateKey;
    private readonly object _lock = new();

    public CakeFileService(string? root = null)
    {
        _root = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "cake-factory")
            : root;
        _cakeDir = Path.Combine(_root, "cakes");
        _keyDir = Path.Combine(_root, "keys");
        _privateKeyPath = Path.Combine(_keyDir, "bakery-private.p8.dpapi");
        _publicKeyPath = Path.Combine(_keyDir, "bakery-public.pem");
        _trustedPath = Path.Combine(_keyDir, "trusted-public-keys.json");
        _eatenLedgerPath = Path.Combine(_root, "eaten-ledger.json");

        Directory.CreateDirectory(_cakeDir);
        Directory.CreateDirectory(_keyDir);

        _privateKey = LoadOrCreatePrivateKey();
        PublicKeyPem = _privateKey.ExportSubjectPublicKeyInfoPem();
        PublicKeyId = ComputeKeyId(PublicKeyPem);
        TryWriteText(_publicKeyPath, PublicKeyPem);
        TrustPublicKey(PublicKeyPem, "This WinForge bakery");
    }

    public string CakeDir => _cakeDir;
    public string PublicKeyPem { get; }
    public string PublicKeyId { get; }

    private ECDsa LoadOrCreatePrivateKey()
    {
        try
        {
            if (File.Exists(_privateKeyPath))
            {
                var protectedBytes = File.ReadAllBytes(_privateKeyPath);
                var raw = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                var ecdsa = ECDsa.Create();
                ecdsa.ImportPkcs8PrivateKey(raw, out _);
                CryptographicOperations.ZeroMemory(raw);
                return ecdsa;
            }
        }
        catch
        {
            // Corrupt or undecryptable private key: rotate the local bakery identity.
        }

        var created = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            var raw = created.ExportPkcs8PrivateKey();
            var protectedBytes = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(_privateKeyPath, protectedBytes);
            CryptographicOperations.ZeroMemory(raw);
        }
        catch
        {
            // In-memory signing still works for this session.
        }
        return created;
    }

    public IReadOnlyList<CakeFileRecord> IssueBatch(CakeRecipe recipe, int count, double qualityScore, double sanitationScore)
    {
        if (count <= 0) return Array.Empty<CakeFileRecord>();

        lock (_lock)
        {
            var issued = new List<CakeFileRecord>();
            var now = DateTime.UtcNow;
            string lotId = $"CAKE-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            for (int i = 1; i <= count; i++)
            {
                string cakeId = $"{lotId}-{i:D3}";
                var payload = new CakePayload
                {
                    cakeId = cakeId,
                    recipeKey = recipe.Key,
                    recipeName = recipe.Name,
                    recipeNameZh = recipe.NameZh,
                    lotId = lotId,
                    cakeNumber = i,
                    batchSize = count,
                    bakedUtc = now.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    expiresUtc = now.AddDays(5).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
                    qualityScore = Math.Round(Math.Clamp(qualityScore, 0, 100), 2),
                    sanitationScore = Math.Round(Math.Clamp(sanitationScore, 0, 100), 2),
                    originDevice = Environment.MachineName,
                    issuerKeyId = PublicKeyId,
                    allergens = new[] { "wheat", "egg", "milk" },
                    status = "packed",
                };

                var path = Path.Combine(_cakeDir, SanitizeFileName(cakeId) + ".cake");
                WriteCakeFile(path, payload);
                var file = ReadCakeFile(path);
                if (file?.payload is not null)
                    issued.Add(ToRecord(path, file, signatureValid: true));
            }
            return issued;
        }
    }

    public IReadOnlyList<CakeFileRecord> ListFresh()
    {
        var list = new List<CakeFileRecord>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(_cakeDir, "*.cake"))
            {
                var f = ReadCakeFile(path);
                if (f?.payload is null) continue;
                var v = Validate(path);
                list.Add(ToRecord(path, f, v.Valid));
            }
        }
        catch { }
        return list.OrderByDescending(c => c.BakedUtc).ThenBy(c => c.CakeId, StringComparer.Ordinal).ToList();
    }

    public CakeValidationResult ImportCakeFile(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return new CakeValidationResult(false, "missing", "Cake file was not found.", "搵唔到蛋糕檔。");

        if (!string.Equals(Path.GetExtension(sourcePath), ".cake", StringComparison.OrdinalIgnoreCase))
            return new CakeValidationResult(false, "not-cake", "Only .cake files can be imported.", "只可以匯入 .cake 蛋糕檔。");

        Directory.CreateDirectory(_cakeDir);
        var name = SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));
        if (string.IsNullOrWhiteSpace(name)) name = "imported-cake";
        var dest = Path.Combine(_cakeDir, name + ".cake");
        var sourceFull = Path.GetFullPath(sourcePath);
        var destFull = Path.GetFullPath(dest);
        if (!string.Equals(sourceFull, destFull, StringComparison.OrdinalIgnoreCase))
        {
            var suffix = 1;
            while (File.Exists(destFull))
            {
                dest = Path.Combine(_cakeDir, $"{name}-{suffix++:D2}.cake");
                destFull = Path.GetFullPath(dest);
            }
            File.Copy(sourceFull, destFull, overwrite: false);
        }

        return Validate(destFull);
    }

    public CakeFileRecord? LatestFresh()
        => ListFresh().Where(c => c.SignatureValid).OrderByDescending(c => c.BakedUtc).FirstOrDefault();

    public CakeValidationResult ValidateLatest()
    {
        var latest = LatestFresh();
        return latest is null
            ? new CakeValidationResult(false, "no-file", "No trusted cake file is ready.", "沒有可信蛋糕檔可用。")
            : Validate(latest.Path);
    }

    public CakeEatResult EatLatest()
    {
        var latest = LatestFresh();
        return latest is null
            ? new CakeEatResult(false, false, "", "No trusted cake file is ready to eat.", "沒有可信蛋糕檔可食用。")
            : EatCake(latest.Path);
    }

    public CakeValidationResult Validate(string path)
    {
        var file = ReadCakeFile(path);
        if (file?.payload is null)
            return new CakeValidationResult(false, "forged", "Unreadable or malformed cake file.", "蛋糕檔無法讀取或格式錯誤。");

        var p = file.payload;
        if (!string.Equals(file.schema, Schema, StringComparison.Ordinal))
            return new CakeValidationResult(false, "forged", "Unknown cake file schema.", "蛋糕檔格式不受信任。", p.cakeId);

        if (!string.Equals(file.alg, Alg, StringComparison.Ordinal))
            return new CakeValidationResult(false, "forged", "Unknown cake signature algorithm.", "蛋糕簽章演算法不受信任。", p.cakeId);

        string keyId;
        try { keyId = ComputeKeyId(file.publicKeyPem ?? ""); }
        catch
        {
            return new CakeValidationResult(false, "forged", "Missing or invalid public key.", "缺少或無效的公鑰。", p.cakeId);
        }

        if (!string.Equals(keyId, file.keyId, StringComparison.Ordinal) ||
            !string.Equals(keyId, p.issuerKeyId, StringComparison.Ordinal))
        {
            return new CakeValidationResult(false, "forged", "Public key id does not match the signed payload.", "公鑰編號與已簽章內容不相符。", p.cakeId);
        }

        if (!VerifySignature(file.publicKeyPem ?? "", p, file.sig ?? ""))
            return new CakeValidationResult(false, "tampered", "Signature invalid - forged or tampered cake.", "簽章無效 - 偽造或被竄改的蛋糕。", p.cakeId);

        if (!IsTrustedKey(keyId))
            return new CakeValidationResult(false, "untrusted", "Cake was signed by an untrusted bakery public key.", "蛋糕由未信任的烘焙公鑰簽署。", p.cakeId);

        if (LoadEatenLedger().Contains(p.cakeId))
            return new CakeValidationResult(false, "already-eaten", "Cake id is already in the eaten ledger.", "蛋糕編號已在食用記錄中。", p.cakeId);

        if (!string.Equals(p.status, "packed", StringComparison.OrdinalIgnoreCase))
            return new CakeValidationResult(false, "status", "Cake file is not in packed/edible state.", "蛋糕檔並非已包裝可食用狀態。", p.cakeId);

        if (DateTime.TryParse(p.expiresUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expires) &&
            expires < DateTime.UtcNow)
        {
            return new CakeValidationResult(false, "expired", "Cake file is expired.", "蛋糕檔已過期。", p.cakeId);
        }

        return new CakeValidationResult(true, "ok", "Valid authentic cake.", "有效且可信的蛋糕。", p.cakeId);
    }

    public CakeEatResult EatCake(string path)
    {
        lock (_lock)
        {
            var v = Validate(path);
            if (!v.Valid)
            {
                return new CakeEatResult(false, false, v.CakeId,
                    "Eat refused - " + v.ReasonEn + " The cake file was not consumed.",
                    "拒絕食用 - " + v.ReasonZh + " 蛋糕檔未被消耗。");
            }

            AppendEatenLedger(v.CakeId);
            bool deleted = TryDelete(path);
            return new CakeEatResult(true, deleted, v.CakeId,
                deleted
                    ? $"Cake {v.CakeId} eaten - .cake file deleted from disk."
                    : $"Cake {v.CakeId} eaten, but the .cake file could not be deleted.",
                deleted
                    ? $"蛋糕 {v.CakeId} 已食用 - .cake 檔已從磁碟刪除。"
                    : $"蛋糕 {v.CakeId} 已食用，但 .cake 檔無法刪除。");
        }
    }

    public string TrustPublicKeyFromCake(string path, string? name = null)
    {
        var file = ReadCakeFile(path);
        if (string.IsNullOrWhiteSpace(file?.publicKeyPem))
            return "";
        return TrustPublicKey(file.publicKeyPem, name ?? "Imported WinForge bakery");
    }

    public string TrustPublicKey(string publicKeyPem, string name)
    {
        string keyId = ComputeKeyId(publicKeyPem);
        var keys = LoadTrustedKeys();
        var existing = keys.FirstOrDefault(k => string.Equals(k.KeyId, keyId, StringComparison.Ordinal));
        if (existing is null)
        {
            keys.Add(new TrustedCakeKey
            {
                KeyId = keyId,
                Name = string.IsNullOrWhiteSpace(name) ? keyId : name,
                PublicKeyPem = publicKeyPem,
                AddedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            });
        }
        else
        {
            existing.Name = string.IsNullOrWhiteSpace(name) ? existing.Name : name;
            existing.PublicKeyPem = publicKeyPem;
        }
        SaveTrustedKeys(keys);
        return keyId;
    }

    private void WriteCakeFile(string path, CakePayload payload)
    {
        var envelope = new CakeFile
        {
            schema = Schema,
            alg = Alg,
            keyId = PublicKeyId,
            publicKeyPem = PublicKeyPem,
            payload = payload,
            sig = Sign(payload),
        };
        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private string Sign(CakePayload payload)
    {
        var bytes = Encoding.UTF8.GetBytes(Canonical(payload));
        return Convert.ToBase64String(_privateKey.SignData(bytes, HashAlgorithmName.SHA256));
    }

    private static bool VerifySignature(string publicKeyPem, CakePayload payload, string sig)
    {
        try
        {
            using var publicKey = ECDsa.Create();
            publicKey.ImportFromPem(publicKeyPem);
            var bytes = Encoding.UTF8.GetBytes(Canonical(payload));
            var signature = Convert.FromBase64String(sig);
            return publicKey.VerifyData(bytes, signature, HashAlgorithmName.SHA256);
        }
        catch
        {
            return false;
        }
    }

    private static string Canonical(CakePayload p)
    {
        var ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append("cakeId=").Append(p.cakeId).Append('\n');
        sb.Append("recipeKey=").Append(p.recipeKey).Append('\n');
        sb.Append("recipeName=").Append(p.recipeName).Append('\n');
        sb.Append("recipeNameZh=").Append(p.recipeNameZh).Append('\n');
        sb.Append("lotId=").Append(p.lotId).Append('\n');
        sb.Append("cakeNumber=").Append(p.cakeNumber.ToString(ci)).Append('\n');
        sb.Append("batchSize=").Append(p.batchSize.ToString(ci)).Append('\n');
        sb.Append("bakedUtc=").Append(p.bakedUtc).Append('\n');
        sb.Append("expiresUtc=").Append(p.expiresUtc).Append('\n');
        sb.Append("qualityScore=").Append(p.qualityScore.ToString("R", ci)).Append('\n');
        sb.Append("sanitationScore=").Append(p.sanitationScore.ToString("R", ci)).Append('\n');
        sb.Append("originDevice=").Append(p.originDevice).Append('\n');
        sb.Append("issuerKeyId=").Append(p.issuerKeyId).Append('\n');
        sb.Append("allergens=").Append(string.Join("|", p.allergens ?? Array.Empty<string>())).Append('\n');
        sb.Append("status=").Append(p.status);
        return sb.ToString();
    }

    private CakeFileRecord ToRecord(string path, CakeFile file, bool signatureValid)
    {
        var p = file.payload!;
        return new CakeFileRecord(
            p.cakeId,
            p.recipeName,
            p.recipeNameZh,
            p.lotId,
            p.cakeNumber,
            p.batchSize,
            ParseUtc(p.bakedUtc),
            ParseUtc(p.expiresUtc),
            p.qualityScore,
            p.sanitationScore,
            p.originDevice,
            p.issuerKeyId,
            path,
            signatureValid);
    }

    private static CakeFile? ReadCakeFile(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<CakeFile>(File.ReadAllText(path));
        }
        catch { return null; }
    }

    private bool IsTrustedKey(string keyId)
        => LoadTrustedKeys().Any(k => string.Equals(k.KeyId, keyId, StringComparison.Ordinal));

    private List<TrustedCakeKey> LoadTrustedKeys()
    {
        try
        {
            if (!File.Exists(_trustedPath)) return new List<TrustedCakeKey>();
            return JsonSerializer.Deserialize<TrustedCakeKeyFile>(File.ReadAllText(_trustedPath))?.Keys ?? new List<TrustedCakeKey>();
        }
        catch { return new List<TrustedCakeKey>(); }
    }

    private void SaveTrustedKeys(List<TrustedCakeKey> keys)
    {
        var deduped = keys
            .GroupBy(k => k.KeyId, StringComparer.Ordinal)
            .Select(g => g.Last())
            .OrderBy(k => k.KeyId, StringComparer.Ordinal)
            .ToList();
        var json = JsonSerializer.Serialize(new TrustedCakeKeyFile { Keys = deduped }, new JsonSerializerOptions { WriteIndented = true });
        TryWriteText(_trustedPath, json);
    }

    private HashSet<string> LoadEatenLedger()
    {
        try
        {
            if (!File.Exists(_eatenLedgerPath)) return new HashSet<string>(StringComparer.Ordinal);
            var ids = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_eatenLedgerPath)) ?? new List<string>();
            return new HashSet<string>(ids, StringComparer.Ordinal);
        }
        catch { return new HashSet<string>(StringComparer.Ordinal); }
    }

    private void AppendEatenLedger(string cakeId)
    {
        try
        {
            var set = LoadEatenLedger();
            set.Add(cakeId);
            var json = JsonSerializer.Serialize(set.OrderBy(x => x, StringComparer.Ordinal).ToList(), new JsonSerializerOptions { WriteIndented = true });
            TryWriteText(_eatenLedgerPath, json);
        }
        catch { }
    }

    private static string ComputeKeyId(string publicKeyPem)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);
        var spki = key.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(spki);
        return Base64Url(hash.Take(18).ToArray());
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static DateTime ParseUtc(string value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt)
            ? dt
            : DateTime.MinValue;

    private static bool TryDelete(string path)
    {
        try { File.Delete(path); return true; }
        catch { return false; }
    }

    private static void TryWriteText(string path, string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        catch { }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '-');
        return value;
    }

    private sealed class CakeFile
    {
        public string? schema { get; set; }
        public string? alg { get; set; }
        public string? keyId { get; set; }
        public string? publicKeyPem { get; set; }
        public CakePayload? payload { get; set; }
        public string? sig { get; set; }
    }

    private sealed class CakePayload
    {
        public string cakeId { get; set; } = "";
        public string recipeKey { get; set; } = "";
        public string recipeName { get; set; } = "";
        public string recipeNameZh { get; set; } = "";
        public string lotId { get; set; } = "";
        public int cakeNumber { get; set; }
        public int batchSize { get; set; }
        public string bakedUtc { get; set; } = "";
        public string expiresUtc { get; set; } = "";
        public double qualityScore { get; set; }
        public double sanitationScore { get; set; }
        public string originDevice { get; set; } = "";
        public string issuerKeyId { get; set; } = "";
        public string[] allergens { get; set; } = Array.Empty<string>();
        public string status { get; set; } = "packed";
    }

    private sealed class TrustedCakeKeyFile
    {
        public List<TrustedCakeKey> Keys { get; set; } = new();
    }

    private sealed class TrustedCakeKey
    {
        public string KeyId { get; set; } = "";
        public string Name { get; set; } = "";
        public string PublicKeyPem { get; set; } = "";
        public string AddedUtc { get; set; } = "";
    }
}
