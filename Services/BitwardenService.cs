using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 原生 Bitwarden 保險庫用戶端（純 managed C#）·
/// A native, fully managed Bitwarden vault client — no <c>bw</c> CLI, no desktop app, no browser.
///
/// 端對端流程 · End-to-end flow (all in-process):
/// • 預登入 · Prelogin: GET /identity/accounts/prelogin → KDF type + iterations (+ Argon2 memory/parallelism).
/// • 主金鑰 · Master key: PBKDF2-SHA256(password, email, iters) OR Argon2id(password, SHA256(email), …).
/// • 認證雜湊 · Master password hash: PBKDF2(masterKey, password, 1 iter), base64 — sent to the token endpoint.
/// • 權杖 · Token: POST /identity/connect/token (password grant) with device headers; supports two-step (2FA).
/// • 解保護金鑰 · Decrypt the protected symmetric key (EncString type 2: AesCbc256_HmacSha256_B64) using the
///   master key directly, or the HKDF-stretched master key (enc+mac), to recover the vault enc+mac keys.
/// • 同步 · Sync: GET /api/sync → decrypt every EncString field of ciphers + folders with the vault key.
/// • TOTP 喺本機產生（RFC 6238）· TOTP codes generated locally (RFC 6238).
///
/// 安全 · Security: tokens are DPAPI-wrapped at rest; key material is held in memory only and zeroed on
/// lock/logout. Secrets are never logged. Every method is defensive and never throws to the UI.
/// </summary>
public sealed class BitwardenService
{
    // ===================== 端點 · Endpoints =====================

    public const string DefaultBase = "https://vault.bitwarden.com";

    private readonly HttpClient _http;
    private string _baseUrl = DefaultBase;

    /// <summary>單例（畀 Catalog 操作用）· Shared instance used by the page and the maintenance catalog.</summary>
    public static BitwardenService Shared { get; } = new();

    public BitwardenService()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Bitwarden 用呢啲 header 嚟識別客戶端類型 · Bitwarden uses these to identify the client type.
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Bitwarden-Client-Name", "cli");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Bitwarden-Client-Version", "2024.1.0");
    }

    // api/identity base URLs. Official cloud uses api.bitwarden.com / identity.bitwarden.com (or the EU
    // equivalents); self-hosted / Vaultwarden typically uses {base}/api and {base}/identity. We detect
    // the official hosts and split accordingly.
    private string ApiBase()
    {
        var b = _baseUrl.TrimEnd('/');
        if (Eq(b, "https://vault.bitwarden.com")) return "https://api.bitwarden.com";
        if (Eq(b, "https://vault.bitwarden.eu")) return "https://api.bitwarden.eu";
        return b + "/api";
    }

    private string IdentityBase()
    {
        var b = _baseUrl.TrimEnd('/');
        if (Eq(b, "https://vault.bitwarden.com")) return "https://identity.bitwarden.com";
        if (Eq(b, "https://vault.bitwarden.eu")) return "https://identity.bitwarden.eu";
        return b + "/identity";
    }

    private static bool Eq(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);

    // ===================== 持久化設定 · Persisted settings =====================

    public const string KeyBaseUrl = "bw.baseUrl";
    public const string KeyEmail = "bw.email";

    // 持久化權杖（DPAPI 包住）· refresh/access tokens persisted, DPAPI-wrapped.
    public const string KeyTokens = "bw.tokens";   // DPAPI(base64) of TokenBundle JSON

    public string SavedEmail => SettingsStore.Get(KeyEmail, "");
    public string SavedBaseUrl => SettingsStore.Get(KeyBaseUrl, "");

    public void SetBaseUrl(string? url)
    {
        url = (url ?? "").Trim();
        _baseUrl = string.IsNullOrWhiteSpace(url) ? DefaultBase : url.TrimEnd('/');
    }

    public string BaseUrl => _baseUrl;

    // ===================== 記憶體中嘅金鑰／權杖 · In-memory keys & tokens =====================

    private readonly object _gate = new();
    private byte[]? _encKey;        // 32-byte vault enc key
    private byte[]? _macKey;        // 32-byte vault mac key
    private string? _accessToken;   // bearer
    private string? _refreshToken;
    private DateTimeOffset _accessExpires;
    private string? _email;

    private VaultSnapshot? _vault;  // last decrypted sync

    public bool IsUnlocked { get { lock (_gate) return _encKey is not null; } }
    public bool IsAuthenticated { get { lock (_gate) return _refreshToken is not null || _accessToken is not null; } }
    public string? UserEmail { get { lock (_gate) return _email; } }
    public DateTimeOffset? LastSync { get; private set; }

    /// <summary>清除所有金鑰物料（鎖定）· Wipe all key material from memory (lock).</summary>
    public void Lock()
    {
        lock (_gate)
        {
            if (_encKey is not null) CryptographicOperations.ZeroMemory(_encKey);
            if (_macKey is not null) CryptographicOperations.ZeroMemory(_macKey);
            _encKey = null;
            _macKey = null;
            _vault = null;
        }
    }

    /// <summary>登出（清除金鑰＋權杖＋持久化權杖）· Log out: wipe keys, tokens, and persisted tokens.</summary>
    public void Logout()
    {
        Lock();
        lock (_gate)
        {
            _accessToken = null;
            _refreshToken = null;
            _email = null;
        }
        try { SettingsStore.Set(KeyTokens, ""); } catch { }
        _vault = null;
        LastSync = null;
    }

    // ===================== 狀態 · Status =====================

    public enum VaultStatus { Unauthenticated, Locked, Unlocked }

    public sealed record StatusInfo(VaultStatus Status, string? ServerUrl, string? UserEmail, DateTimeOffset? LastSync);

    public StatusInfo GetStatus()
    {
        lock (_gate)
        {
            if (_encKey is not null) return new StatusInfo(VaultStatus.Unlocked, _baseUrl, _email, LastSync);
            if (_refreshToken is not null || _accessToken is not null) return new StatusInfo(VaultStatus.Locked, _baseUrl, _email, LastSync);
            return new StatusInfo(VaultStatus.Unauthenticated, _baseUrl, _email, LastSync);
        }
    }

    // ===================== 登入 · Login =====================

    public sealed record LoginResult(bool Success, LocalizedText? Message, bool TwoFactorRequired, List<int>? Methods);

    /// <summary>
    /// 用電郵 + 主密碼登入（選用 2FA 碼）· Log in with email + master password (optional 2FA code).
    /// 成功後即時解保護金鑰並解鎖 · On success the protected key is decrypted and the vault is unlocked.
    /// </summary>
    public async Task<LoginResult> LoginAsync(string email, string masterPassword, string baseUrl,
        string? twoFactorCode, int? twoFactorMethod, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(masterPassword))
            return new LoginResult(false, new("Email and master password are required.", "需要電郵同主密碼。"), false, null);

        SetBaseUrl(baseUrl);
        email = email.Trim().ToLowerInvariant();

        byte[]? masterKey = null;
        try
        {
            // 1) Prelogin → KDF config
            KdfConfig kdf;
            try { kdf = await PreloginAsync(email, ct); }
            catch (Exception ex) { return new LoginResult(false, NetMsg(ex), false, null); }

            // 2) Derive master key + auth hash
            masterKey = await DeriveMasterKeyAsync(email, masterPassword, kdf, ct);
            string masterHash = MasterPasswordHash(masterKey, masterPassword);

            // 3) Token request (password grant)
            var token = await RequestTokenAsync(email, masterHash, twoFactorCode, twoFactorMethod, ct);
            if (!token.Success)
            {
                if (token.TwoFactorRequired)
                    return new LoginResult(false,
                        new("Two-step verification required. Enter your code.", "需要兩步驗證。請輸入驗證碼。"),
                        true, token.Methods);
                return new LoginResult(false, token.Message, false, null);
            }

            // 4) Decrypt protected user key → vault enc/mac keys
            if (string.IsNullOrEmpty(token.Key))
                return new LoginResult(false, new("Server did not return the protected key.", "伺服器無回傳受保護金鑰。"), false, null);

            (byte[] encKey, byte[] macKey) keys;
            try { keys = DecryptUserKey(token.Key!, masterKey, masterPassword, email, kdf); }
            catch
            {
                return new LoginResult(false,
                    new("Could not decrypt the vault key (wrong master password?).",
                        "解密保險庫金鑰失敗（主密碼錯咗？）。"), false, null);
            }

            lock (_gate)
            {
                _encKey = keys.encKey;
                _macKey = keys.macKey;
                _accessToken = token.AccessToken;
                _refreshToken = token.RefreshToken;
                _accessExpires = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 60);
                _email = email;
            }
            PersistTokens();
            try { SettingsStore.Set(KeyEmail, email); SettingsStore.Set(KeyBaseUrl, IsOfficial2(_baseUrl) ? "" : _baseUrl); } catch { }

            return new LoginResult(true, new("Logged in and unlocked.", "已登入並解鎖。"), false, null);
        }
        catch (Exception ex)
        {
            return new LoginResult(false, NetMsg(ex), false, null);
        }
        finally
        {
            if (masterKey is not null) CryptographicOperations.ZeroMemory(masterKey);
        }
    }

    private static bool IsOfficial2(string b) =>
        b.TrimEnd('/').Equals(DefaultBase, StringComparison.OrdinalIgnoreCase);

    // ----- Prelogin -----

    public sealed record KdfConfig(int Kdf, int Iterations, int? Memory, int? Parallelism);

    private async Task<KdfConfig> PreloginAsync(string email, CancellationToken ct)
    {
        var url = IdentityBase() + "/accounts/prelogin";
        var payload = JsonSerializer.Serialize(new { email });
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            // Some self-hosted/older servers expose prelogin under /api instead.
            var alt = ApiBase() + "/accounts/prelogin";
            using var req2 = new HttpRequestMessage(HttpMethod.Post, alt)
            { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            using var resp2 = await _http.SendAsync(req2, ct);
            body = await resp2.Content.ReadAsStringAsync(ct);
            if (!resp2.IsSuccessStatusCode)
                throw new InvalidOperationException("prelogin failed");
        }
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        int kdf = GetInt(root, "kdf", 0);
        int iters = GetInt(root, "kdfIterations", 600_000);
        int? mem = root.TryGetProperty("kdfMemory", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : null;
        int? par = root.TryGetProperty("kdfParallelism", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : null;
        return new KdfConfig(kdf, iters, mem, par);
    }

    // ----- Token endpoint -----

    private sealed record TokenResult(bool Success, string? AccessToken, string? RefreshToken, int ExpiresIn,
        string? Key, string? PrivateKey, LocalizedText? Message, bool TwoFactorRequired, List<int>? Methods);

    private static readonly string DeviceId = GetOrCreateDeviceId();

    private async Task<TokenResult> RequestTokenAsync(string email, string masterHash,
        string? twoFactorCode, int? twoFactorMethod, CancellationToken ct)
    {
        var url = IdentityBase() + "/connect/token";

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "password"),
            new("username", email),
            new("password", masterHash),
            new("scope", "api offline_access"),
            new("client_id", "cli"),
            new("deviceType", "23"),          // 23 = Windows desktop
            new("deviceIdentifier", DeviceId),
            new("deviceName", "WinForge"),
        };
        if (!string.IsNullOrWhiteSpace(twoFactorCode) && twoFactorMethod is int tfm)
        {
            form.Add(new("twoFactorToken", twoFactorCode.Trim()));
            form.Add(new("twoFactorProvider", tfm.ToString(CultureInfo.InvariantCulture)));
            form.Add(new("twoFactorRemember", "0"));
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        req.Headers.TryAddWithoutValidation("Auth-Email", Base64Url(Encoding.UTF8.GetBytes(email)));
        req.Headers.TryAddWithoutValidation("Device-Type", "23");
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);

        if (resp.IsSuccessStatusCode)
        {
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            return new TokenResult(true,
                GetStr(r, "access_token"), GetStr(r, "refresh_token"),
                GetInt(r, "expires_in", 3600),
                GetStr(r, "Key") ?? GetStr(r, "key"),
                GetStr(r, "PrivateKey") ?? GetStr(r, "privateKey"),
                null, false, null);
        }

        // Two-factor required / error parsing
        try
        {
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            if (r.TryGetProperty("TwoFactorProviders2", out var tfp2) || r.TryGetProperty("twoFactorProviders2", out tfp2))
            {
                var methods = new List<int>();
                foreach (var prop in tfp2.EnumerateObject())
                    if (int.TryParse(prop.Name, out var mi)) methods.Add(mi);
                return new TokenResult(false, null, null, 0, null, null,
                    new("Two-step verification required.", "需要兩步驗證。"), true, methods);
            }
            // error_description / ErrorModel.Message
            string? desc = GetStr(r, "error_description");
            if (desc is null && r.TryGetProperty("ErrorModel", out var em)) desc = GetStr(em, "Message");
            var en = desc ?? "Login failed.";
            return new TokenResult(false, null, null, 0, null, null,
                new(en, "登入失敗：" + en), false, null);
        }
        catch
        {
            return new TokenResult(false, null, null, 0, null, null,
                new($"Login failed ({(int)resp.StatusCode}).", $"登入失敗（{(int)resp.StatusCode}）。"), false, null);
        }
    }

    // ===================== 同步 · Sync =====================

    public sealed record VaultSnapshot(List<VaultItem> Items, List<Folder> Folders);
    public sealed record Folder(string Id, string Name);

    /// <summary>同步並解密整個保險庫 · Pull /api/sync and decrypt the whole vault.</summary>
    public async Task<TweakResult> SyncAsync(CancellationToken ct = default)
    {
        if (!IsUnlocked) return TweakResult.Fail("Vault is locked.", "保險庫已鎖定。");
        try
        {
            var resp = await AuthGet("/sync?excludeDomains=true", ct);
            if (resp is null) return TweakResult.Fail("Sync failed.", "同步失敗。");
            var (ok, body) = resp.Value;
            if (!ok) return TweakResult.Fail("Sync failed.", "同步失敗。", body);

            var snap = DecryptSync(body);
            _vault = snap;
            LastSync = DateTimeOffset.UtcNow;
            return TweakResult.Ok("Synced.", "已同步。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, "同步出錯：" + ex.Message); }
    }

    private VaultSnapshot DecryptSync(string body)
    {
        var folders = new List<Folder>();
        var items = new List<VaultItem>();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("folders", out var farr) && farr.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in farr.EnumerateArray())
            {
                var id = GetStr(f, "id") ?? "";
                var name = DecStr(GetStr(f, "name"));
                folders.Add(new Folder(id, name ?? ""));
            }
        }

        if (root.TryGetProperty("ciphers", out var carr) && carr.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in carr.EnumerateArray())
            {
                try { items.Add(DecryptCipher(c)); }
                catch { /* skip un-decryptable item, never crash the whole sync */ }
            }
        }
        return new VaultSnapshot(items, folders);
    }

    private VaultItem DecryptCipher(JsonElement c)
    {
        var item = new VaultItem
        {
            Id = GetStr(c, "id") ?? "",
            Type = GetInt(c, "type", 1),
            Favorite = c.TryGetProperty("favorite", out var fav) && fav.ValueKind == JsonValueKind.True,
            FolderId = GetStr(c, "folderId"),
            Name = DecStr(GetStr(c, "name")) ?? "",
            Notes = DecStr(GetStr(c, "notes")),
        };

        if (c.TryGetProperty("login", out var login) && login.ValueKind == JsonValueKind.Object)
        {
            item.Login = new LoginInfo
            {
                Username = DecStr(GetStr(login, "username")),
                Password = DecStr(GetStr(login, "password")),
                Totp = DecStr(GetStr(login, "totp")),
            };
            if (login.TryGetProperty("uris", out var uris) && uris.ValueKind == JsonValueKind.Array)
            {
                item.Login.Uris = new List<UriEntry>();
                foreach (var u in uris.EnumerateArray())
                    item.Login.Uris.Add(new UriEntry { Uri = DecStr(GetStr(u, "uri")) });
            }
        }

        if (c.TryGetProperty("card", out var card) && card.ValueKind == JsonValueKind.Object)
        {
            var brand = DecStr(GetStr(card, "brand"));
            var num = DecStr(GetStr(card, "number"));
            item.CardSummary = string.Join("  ", new[] { brand, num }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        return item;
    }

    // ===================== 列表 / 取項 · Listing =====================

    /// <summary>取得（並選擇性搜尋）已解密項目 · Get decrypted items, optionally filtered.</summary>
    public List<VaultItem> ListItems(string? search)
    {
        var snap = _vault;
        if (snap is null) return new List<VaultItem>();
        IEnumerable<VaultItem> q = snap.Items;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(i =>
                (i.Name?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.Username?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (i.PrimaryUri?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return q.ToList();
    }

    public List<Folder> Folders() => _vault?.Folders ?? new List<Folder>();

    public string FolderName(string? id, bool zh)
    {
        if (string.IsNullOrEmpty(id)) return zh ? "無資料夾" : "No Folder";
        var f = _vault?.Folders.FirstOrDefault(x => x.Id == id);
        return f?.Name ?? (zh ? "無資料夾" : "No Folder");
    }

    public VaultItem? GetItem(string id) => _vault?.Items.FirstOrDefault(i => i.Id == id);

    // ===================== TOTP（RFC 6238）· Local TOTP =====================

    public sealed record TotpResult(string Code, int RemainingSeconds, int Period);

    /// <summary>由項目嘅 TOTP 密鑰本機產生驗證碼 · Generate the current TOTP locally from the item's secret.</summary>
    public TotpResult? GetTotp(string id)
    {
        var item = GetItem(id);
        var secret = item?.Login?.Totp;
        if (string.IsNullOrWhiteSpace(secret)) return null;
        return ComputeTotp(secret);
    }

    /// <summary>RFC 6238 TOTP。支援 otpauth:// URI 同裸 base32 密鑰 · Supports otpauth URIs and bare base32 secrets.</summary>
    public static TotpResult? ComputeTotp(string secretOrUri)
    {
        try
        {
            string secret = secretOrUri.Trim();
            int digits = 6, period = 30;
            string algo = "SHA1";

            if (secret.StartsWith("otpauth://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(secret);
                var qs = ParseQuery(uri.Query);
                if (qs.TryGetValue("secret", out var sv)) secret = sv;
                if (qs.TryGetValue("digits", out var dv) && int.TryParse(dv, out var dd)) digits = dd;
                if (qs.TryGetValue("period", out var pv) && int.TryParse(pv, out var pp)) period = pp;
                if (qs.TryGetValue("algorithm", out var av)) algo = av.ToUpperInvariant();
            }
            else if (secret.Contains("&secret=", StringComparison.OrdinalIgnoreCase) || secret.StartsWith("secret=", StringComparison.OrdinalIgnoreCase))
            {
                var qs = ParseQuery(secret.StartsWith("?") ? secret : "?" + secret);
                if (qs.TryGetValue("secret", out var sv)) secret = sv;
                if (qs.TryGetValue("digits", out var dv) && int.TryParse(dv, out var dd)) digits = dd;
                if (qs.TryGetValue("period", out var pv) && int.TryParse(pv, out var pp)) period = pp;
            }

            var key = Base32Decode(secret);
            if (key.Length == 0) return null;

            long counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / period;
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

            using HMAC hmac = algo switch
            {
                "SHA256" => new HMACSHA256(key),
                "SHA512" => new HMACSHA512(key),
                _ => new HMACSHA1(key),
            };
            var hash = hmac.ComputeHash(counterBytes);
            int offset = hash[^1] & 0x0F;
            int bin = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16)
                      | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);
            int mod = (int)Math.Pow(10, digits);
            string code = (bin % mod).ToString(CultureInfo.InvariantCulture).PadLeft(digits, '0');

            int remaining = period - (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % period);
            return new TotpResult(code, remaining, period);
        }
        catch { return null; }
    }

    // ===================== 密碼產生器 · Password generator =====================

    public sealed record GenOptions(
        bool Passphrase, int Length, bool Uppercase, bool Lowercase, bool Numbers, bool Special,
        int Words, string Separator, bool Capitalize);

    private const string WordsResource = "correct horse battery staple apple river table cloud stone light forest copper silver maple ocean planet rocket garden window mirror anchor bridge candle dragon engine flower guitar hammer island jungle kettle ladder magnet needle orange pillow puzzle quartz ribbon saddle tunnel velvet walnut yellow zephyr breeze canyon ember falcon glacier harbor";

    /// <summary>本機產生密碼或通行短語 · Generate a password/passphrase locally (no network).</summary>
    public static string Generate(GenOptions o)
    {
        if (o.Passphrase)
        {
            var words = WordsResource.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int n = Math.Clamp(o.Words, 3, 20);
            var sb = new List<string>();
            for (int i = 0; i < n; i++)
            {
                var w = words[RandomNumberGenerator.GetInt32(words.Length)];
                if (o.Capitalize) w = char.ToUpperInvariant(w[0]) + w.Substring(1);
                sb.Add(w);
            }
            var sep = string.IsNullOrEmpty(o.Separator) ? "-" : o.Separator;
            var result = string.Join(sep, sb);
            if (o.Numbers) result += RandomNumberGenerator.GetInt32(10).ToString(CultureInfo.InvariantCulture);
            return result;
        }
        else
        {
            var sets = new List<string>();
            if (o.Uppercase) sets.Add("ABCDEFGHJKLMNPQRSTUVWXYZ");
            if (o.Lowercase) sets.Add("abcdefghijkmnpqrstuvwxyz");
            if (o.Numbers) sets.Add("23456789");
            if (o.Special) sets.Add("!@#$%^&*()-_=+[]{}");
            if (sets.Count == 0) sets.Add("abcdefghijkmnpqrstuvwxyz");
            var all = string.Concat(sets);
            int len = Math.Clamp(o.Length, 5, 128);
            var chars = new char[len];
            // ensure at least one from each selected set
            for (int i = 0; i < sets.Count && i < len; i++)
                chars[i] = sets[i][RandomNumberGenerator.GetInt32(sets[i].Length)];
            for (int i = sets.Count; i < len; i++)
                chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
            // shuffle
            for (int i = len - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars);
        }
    }

    // ===================== 認證 HTTP 輔助 · Authenticated HTTP =====================

    private async Task<(bool ok, string body)?> AuthGet(string path, CancellationToken ct)
    {
        await EnsureFreshTokenAsync(ct);
        string? tok; lock (_gate) tok = _accessToken;
        if (tok is null) return null;
        using var req = new HttpRequestMessage(HttpMethod.Get, ApiBase() + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tok);
        using var resp = await _http.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return (resp.IsSuccessStatusCode, body);
    }

    private async Task EnsureFreshTokenAsync(CancellationToken ct)
    {
        bool needsRefresh; string? refresh;
        lock (_gate)
        {
            needsRefresh = _accessToken is null || DateTimeOffset.UtcNow >= _accessExpires;
            refresh = _refreshToken;
        }
        if (!needsRefresh || refresh is null) return;
        try
        {
            var form = new List<KeyValuePair<string, string>>
            {
                new("grant_type", "refresh_token"),
                new("refresh_token", refresh),
                new("client_id", "cli"),
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, IdentityBase() + "/connect/token")
            { Content = new FormUrlEncodedContent(form) };
            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return;
            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var r = doc.RootElement;
            lock (_gate)
            {
                _accessToken = GetStr(r, "access_token") ?? _accessToken;
                _refreshToken = GetStr(r, "refresh_token") ?? _refreshToken;
                _accessExpires = DateTimeOffset.UtcNow.AddSeconds(GetInt(r, "expires_in", 3600) - 60);
            }
            PersistTokens();
        }
        catch { /* keep old token; the GET will surface failure */ }
    }

    // ===================== 持久化權杖（DPAPI）· Token persistence =====================

    private sealed record TokenBundle(string? Access, string? Refresh, long ExpiresUnix, string? Email, string? BaseUrl);

    private void PersistTokens()
    {
        try
        {
            TokenBundle b;
            lock (_gate) b = new TokenBundle(_accessToken, _refreshToken, _accessExpires.ToUnixTimeSeconds(), _email,
                IsOfficial2(_baseUrl) ? null : _baseUrl);
            var json = JsonSerializer.Serialize(b);
            var prot = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
            SettingsStore.Set(KeyTokens, Convert.ToBase64String(prot));
        }
        catch { }
    }

    /// <summary>啟動時嘗試還原已驗證階段（保險庫仍需主密碼解鎖）· Restore the authenticated session at startup
    /// (the vault still needs the master password to unlock — keys are never persisted).</summary>
    public void TryRestoreSession()
    {
        try
        {
            var b64 = SettingsStore.Get(KeyTokens, "");
            if (string.IsNullOrEmpty(b64)) return;
            var raw = ProtectedData.Unprotect(Convert.FromBase64String(b64), null, DataProtectionScope.CurrentUser);
            var b = JsonSerializer.Deserialize<TokenBundle>(Encoding.UTF8.GetString(raw));
            if (b is null) return;
            lock (_gate)
            {
                _accessToken = b.Access;
                _refreshToken = b.Refresh;
                _accessExpires = DateTimeOffset.FromUnixTimeSeconds(b.ExpiresUnix);
                _email = b.Email;
            }
            if (!string.IsNullOrWhiteSpace(b.BaseUrl)) _baseUrl = b.BaseUrl!;
        }
        catch { }
    }

    /// <summary>用主密碼重新解鎖一個已驗證帳戶（重做 prelogin + 取金鑰）· Re-unlock an authenticated account
    /// by re-deriving keys from the master password (re-runs prelogin and re-fetches the protected key).</summary>
    public async Task<TweakResult> UnlockAsync(string masterPassword, CancellationToken ct = default)
    {
        string? email; lock (_gate) email = _email;
        if (string.IsNullOrEmpty(email))
            return TweakResult.Fail("Not signed in.", "未登入。");
        if (string.IsNullOrEmpty(masterPassword))
            return TweakResult.Fail("Master password is required.", "需要主密碼。");

        byte[]? masterKey = null;
        try
        {
            var kdf = await PreloginAsync(email, ct);
            masterKey = await DeriveMasterKeyAsync(email, masterPassword, kdf, ct);

            // Fetch the protected user key from the account profile (sync includes it under profile.key).
            await EnsureFreshTokenAsync(ct);
            var resp = await AuthGet("/sync?excludeDomains=true", ct);
            if (resp is null || !resp.Value.ok)
                return TweakResult.Fail("Could not reach the server to unlock.", "連唔到伺服器解鎖。");

            string? protectedKey = null;
            using (var doc = JsonDocument.Parse(resp.Value.body))
            {
                if (doc.RootElement.TryGetProperty("profile", out var prof))
                    protectedKey = GetStr(prof, "key") ?? GetStr(prof, "Key");
            }
            if (string.IsNullOrEmpty(protectedKey))
                return TweakResult.Fail("Server did not return the protected key.", "伺服器無回傳受保護金鑰。");

            (byte[] encKey, byte[] macKey) keys;
            try { keys = DecryptUserKey(protectedKey!, masterKey, masterPassword, email, kdf); }
            catch
            {
                return TweakResult.Fail("Wrong master password.", "主密碼錯咗。");
            }

            lock (_gate) { _encKey = keys.encKey; _macKey = keys.macKey; }

            // We already have the sync body — decrypt it now.
            _vault = DecryptSync(resp.Value.body);
            LastSync = DateTimeOffset.UtcNow;
            return TweakResult.Ok("Vault unlocked.", "保險庫已解鎖。");
        }
        catch (Exception ex) { var m = NetMsg(ex); return TweakResult.Fail(m.En, m.Zh); }
        finally { if (masterKey is not null) CryptographicOperations.ZeroMemory(masterKey); }
    }

    // ===================== 金鑰衍生 · Key derivation =====================

    /// <summary>由電郵 + 密碼衍生主金鑰（PBKDF2-SHA256 或 Argon2id）· Derive the master key.</summary>
    private static async Task<byte[]> DeriveMasterKeyAsync(string email, string password, KdfConfig kdf, CancellationToken ct)
    {
        var pw = Encoding.UTF8.GetBytes(password);
        var salt = Encoding.UTF8.GetBytes(email);

        if (kdf.Kdf == 1) // Argon2id
        {
            // Bitwarden: salt = SHA-256(email), memory in KiB (kdfMemory is MiB → *1024).
            byte[] argonSalt = SHA256.HashData(salt);
            int memKib = Math.Max(1024, (kdf.Memory ?? 64) * 1024);
            int par = Math.Max(1, kdf.Parallelism ?? 4);
            int iters = Math.Max(1, kdf.Iterations);

            using var argon = new Konscious.Security.Cryptography.Argon2id(pw)
            {
                Salt = argonSalt,
                MemorySize = memKib,
                Iterations = iters,
                DegreeOfParallelism = par,
            };
            var hash = await argon.GetBytesAsync(32);
            return hash;
        }

        // PBKDF2-SHA256
        int it = Math.Max(5000, kdf.Iterations);
        return Rfc2898DeriveBytes.Pbkdf2(pw, salt, it, HashAlgorithmName.SHA256, 32);
    }

    /// <summary>認證用嘅主密碼雜湊：PBKDF2(masterKey, password, 1 iter)，base64 ·
    /// Master password hash for auth: PBKDF2 of the master key with the password as salt, 1 iter, base64.</summary>
    private static string MasterPasswordHash(byte[] masterKey, string password)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(masterKey, Encoding.UTF8.GetBytes(password), 1, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(hash);
    }

    /// <summary>HKDF-SHA256「expand only」拉伸（Bitwarden 2.x 金鑰格式）· HKDF expand-only stretch for the 2.x key format.</summary>
    private static (byte[] enc, byte[] mac) StretchMasterKey(byte[] masterKey)
    {
        var enc = HkdfExpand(masterKey, "enc", 32);
        var mac = HkdfExpand(masterKey, "mac", 32);
        return (enc, mac);
    }

    private static byte[] HkdfExpand(byte[] prk, string info, int length)
    {
        // RFC 5869 expand (assumes prk already pseudorandom — Bitwarden uses the master key directly).
        var infoBytes = Encoding.UTF8.GetBytes(info);
        var result = new byte[length];
        byte[] previous = Array.Empty<byte>();
        using var hmac = new HMACSHA256(prk);
        int offset = 0;
        byte counter = 1;
        while (offset < length)
        {
            hmac.Initialize();
            var input = new byte[previous.Length + infoBytes.Length + 1];
            Buffer.BlockCopy(previous, 0, input, 0, previous.Length);
            Buffer.BlockCopy(infoBytes, 0, input, previous.Length, infoBytes.Length);
            input[^1] = counter;
            previous = hmac.ComputeHash(input);
            int toCopy = Math.Min(previous.Length, length - offset);
            Buffer.BlockCopy(previous, 0, result, offset, toCopy);
            offset += toCopy;
            counter++;
        }
        return result;
    }

    /// <summary>
    /// 解密使用者保護金鑰（EncString type 2）→ (encKey, macKey)。
    /// Decrypt the protected user key. The EncString prefix tells us the format:
    ///   type 0 (AesCbc256_B64): 32-byte key, decrypted with the master key directly (legacy).
    ///   type 2 (AesCbc256_HmacSha256_B64): 64-byte key → HKDF-stretch the master key into (enc, mac) and use those.
    /// The decrypted blob is the 64-byte user key (32 enc + 32 mac).
    /// </summary>
    private static (byte[] encKey, byte[] macKey) DecryptUserKey(string protectedKey, byte[] masterKey,
        string password, string email, KdfConfig kdf)
    {
        var es = EncString.Parse(protectedKey);
        byte[] decrypted;

        if (es.Type == 0)
        {
            // legacy: encrypt key with raw master key, no HMAC
            decrypted = AesCbcDecrypt(masterKey, es.Iv!, es.Data);
        }
        else
        {
            // type 2: stretch the master key into enc+mac and HMAC-validate, then decrypt
            var (enc, mac) = StretchMasterKey(masterKey);
            try { decrypted = DecryptEncStringType2(es, enc, mac); }
            finally { CryptographicOperations.ZeroMemory(enc); CryptographicOperations.ZeroMemory(mac); }
        }

        if (decrypted.Length == 64)
            return (decrypted.Take(32).ToArray(), decrypted.Skip(32).Take(32).ToArray());
        if (decrypted.Length == 32)
            return (decrypted, Array.Empty<byte>());
        throw new CryptographicException("Unexpected user key length.");
    }

    // ===================== EncString 解析 / 解密 · EncString parsing & decryption =====================

    private sealed class EncString
    {
        public int Type;
        public byte[]? Iv;
        public byte[] Data = Array.Empty<byte>();
        public byte[]? Mac;

        /// <summary>解析 "type.iv|data|mac" 形式嘅 CipherString · Parse the "type.iv|data|mac" CipherString.</summary>
        public static EncString Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) throw new FormatException("Empty EncString.");
            int dot = s.IndexOf('.');
            int type;
            string rest;
            if (dot >= 0 && int.TryParse(s.Substring(0, dot), out type))
                rest = s.Substring(dot + 1);
            else { type = 0; rest = s; }

            var parts = rest.Split('|');
            var es = new EncString { Type = type };
            switch (type)
            {
                case 0: // AesCbc256_B64: iv|data
                    es.Iv = Convert.FromBase64String(parts[0]);
                    es.Data = Convert.FromBase64String(parts[1]);
                    break;
                case 1: // AesCbc128_HmacSha256_B64
                case 2: // AesCbc256_HmacSha256_B64: iv|data|mac
                    es.Iv = Convert.FromBase64String(parts[0]);
                    es.Data = Convert.FromBase64String(parts[1]);
                    es.Mac = parts.Length > 2 ? Convert.FromBase64String(parts[2]) : null;
                    break;
                default:
                    // RSA types (3,4,5,6) not used for symmetric vault fields here
                    es.Data = Convert.FromBase64String(parts[0]);
                    break;
            }
            return es;
        }
    }

    /// <summary>解密 type-2 EncString（驗 HMAC 再 AES-CBC 解密）· Validate HMAC then AES-256-CBC decrypt.</summary>
    private static byte[] DecryptEncStringType2(EncString es, byte[] encKey, byte[] macKey)
    {
        if (es.Iv is null) throw new CryptographicException("Missing IV.");
        if (macKey.Length > 0 && es.Mac is not null)
        {
            using var hmac = new HMACSHA256(macKey);
            var macData = new byte[es.Iv.Length + es.Data.Length];
            Buffer.BlockCopy(es.Iv, 0, macData, 0, es.Iv.Length);
            Buffer.BlockCopy(es.Data, 0, macData, es.Iv.Length, es.Data.Length);
            var computed = hmac.ComputeHash(macData);
            if (!CryptographicOperations.FixedTimeEquals(computed, es.Mac))
                throw new CryptographicException("HMAC validation failed.");
        }
        return AesCbcDecrypt(encKey, es.Iv, es.Data);
    }

    private static byte[] AesCbcDecrypt(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var dec = aes.CreateDecryptor();
        return dec.TransformFinalBlock(data, 0, data.Length);
    }

    /// <summary>用保險庫金鑰解密一個 EncString 字段 → 文字（解唔到回 null）· Decrypt one EncString field to text.</summary>
    private string? DecStr(string? enc)
    {
        if (string.IsNullOrWhiteSpace(enc)) return null;
        byte[] encKey, macKey;
        lock (_gate)
        {
            if (_encKey is null) return null;
            encKey = _encKey; macKey = _macKey ?? Array.Empty<byte>();
        }
        try
        {
            var es = EncString.Parse(enc);
            byte[] plain = es.Type == 0
                ? AesCbcDecrypt(encKey, es.Iv!, es.Data)
                : DecryptEncStringType2(es, encKey, macKey);
            return Encoding.UTF8.GetString(plain);
        }
        catch { return null; }
    }

    // ===================== 雜項輔助 · Misc helpers =====================

    private static LocalizedText NetMsg(Exception ex) =>
        new("Could not reach the Bitwarden server. Check your connection and base URL.",
            "連唔到 Bitwarden 伺服器。檢查網絡同伺服器網址。");

    private static string GetOrCreateDeviceId()
    {
        const string key = "bw.deviceId";
        var existing = SettingsStore.Get(key, "");
        if (!string.IsNullOrWhiteSpace(existing)) return existing;
        var id = Guid.NewGuid().ToString();
        try { SettingsStore.Set(key, id); } catch { }
        return id;
    }

    private static string? GetStr(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int GetInt(JsonElement e, string name, int fallback) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : fallback;

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        query = query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2) d[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
        }
        return d;
    }

    private static byte[] Base32Decode(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        input = input.Trim().Replace(" ", "").Replace("-", "").TrimEnd('=').ToUpperInvariant();
        if (input.Length == 0) return Array.Empty<byte>();
        int bits = 0, value = 0;
        var output = new List<byte>(input.Length * 5 / 8);
        foreach (char c in input)
        {
            int idx = alphabet.IndexOf(c);
            if (idx < 0) continue;
            value = (value << 5) | idx;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)((value >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }
        return output.ToArray();
    }

    // ===================== 模型 · Models =====================

    public sealed class VaultItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Type { get; set; }
        public bool Favorite { get; set; }
        public string? Notes { get; set; }
        public string? FolderId { get; set; }
        public LoginInfo? Login { get; set; }
        public string? CardSummary { get; set; }

        public string TypeLabel(bool zh) => Type switch
        {
            1 => zh ? "登入" : "Login",
            2 => zh ? "安全筆記" : "Secure note",
            3 => zh ? "信用卡" : "Card",
            4 => zh ? "身分" : "Identity",
            _ => zh ? "項目" : "Item",
        };

        public string? PrimaryUri =>
            Login?.Uris is { Count: > 0 } u && !string.IsNullOrWhiteSpace(u[0].Uri) ? u[0].Uri : null;

        public bool HasTotp => !string.IsNullOrWhiteSpace(Login?.Totp);
        public string? Username => Login?.Username;
    }

    public sealed class LoginInfo
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Totp { get; set; }
        public List<UriEntry>? Uris { get; set; }
    }

    public sealed class UriEntry
    {
        public string? Uri { get; set; }
    }
}
