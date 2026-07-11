using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>連線狀態 · The current connection state for the InfoBar.</summary>
public enum PveConnState
{
    Disconnected,   // 未連線 · not connected yet
    Connected,      // 已連線 · authenticated and reachable
    Unreachable,    // 連唔到 · host unreachable / network error
    Unauthorized,   // 未授權 · bad token / credentials
    CertUntrusted,  // 憑證未信任 · TLS validation failed (self-signed)
}

/// <summary>一次 Proxmox REST 呼叫嘅結果 · Result of one Proxmox REST call.</summary>
public sealed record PveResult(bool Ok, string Body, int Status, PveConnState State)
{
    public static PveResult Fail(string body, PveConnState state) => new(false, body, 0, state);
}

/// <summary>
/// 一個客體（虛擬機或容器）· One guest: a QEMU VM or an LXC container, flattened across nodes.
/// </summary>
public sealed class PveGuest
{
    public string Node { get; init; } = "";
    public int VmId { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "qemu";   // "qemu" | "lxc"
    public string Status { get; init; } = "";      // "running" | "stopped" | …
    public double CpuFraction { get; init; }        // 0..1 of allocated cores
    public int MaxCpu { get; init; }                // allocated cores
    public long MemBytes { get; init; }             // used memory, bytes
    public long MaxMemBytes { get; init; }          // allocated memory, bytes
    public long DiskBytes { get; init; }
    public long MaxDiskBytes { get; init; }
    public long UptimeSec { get; init; }
    public string Lock { get; init; } = "";
    public bool Template { get; init; }

    public bool IsRunning => string.Equals(Status, "running", StringComparison.OrdinalIgnoreCase);
    public bool IsContainer => string.Equals(Type, "lxc", StringComparison.OrdinalIgnoreCase);
    public string Key => $"{Node}/{Type}/{VmId}";
}

/// <summary>一個節點 · One Proxmox node in the cluster.</summary>
public sealed record PveNode(string Name, string Status, double CpuFraction, long MemBytes, long MaxMemBytes, long UptimeSec);

/// <summary>
/// 應用程式內 Proxmox VE REST 客戶端 · In-app Proxmox VE REST API client (api2/json), pure managed HttpClient.
/// 兩種驗證：API Token（Authorization: PVEAPIToken=…）或者帳號／密碼 ticket（/access/ticket → cookie + CSRF）。
/// Two auth modes: API Token header, or username/password ticket login (cookie + CSRF token for writes).
/// 主機／token／設定經 SettingsStore 持久化，token 用 DPAPI 加密；密碼／ticket 永不寫落磁碟。
/// Host/token/settings persist via SettingsStore with the token DPAPI-encrypted; passwords/tickets are never persisted.
/// 自簽憑證需要使用者開啟「信任自簽憑證」開關先會接受 · Self-signed certs are only accepted when the user opts in.
/// </summary>
public sealed class ProxmoxService
{
    public const string KeyHost = "pve.host";
    public const string KeyPort = "pve.port";
    public const string KeyAuthMode = "pve.authmode";   // "token" | "ticket"
    public const string KeyTokenId = "pve.tokenid";     // USER@REALM!TOKENID
    public const string KeyTokenSecretEnc = "pve.tokensecret.enc"; // DPAPI base64
    public const string KeyUser = "pve.user";           // username (without realm)
    public const string KeyRealm = "pve.realm";         // pam / pve / …
    public const string KeyTrustCert = "pve.trustcert"; // "1" to trust self-signed

    // 額外 entropy，令 token 只可以喺呢個 app 解密 · App-specific DPAPI entropy.
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.ProxmoxService.v1");

    private HttpClient _http;
    private readonly CookieContainer _cookies = new();

    // ticket-login session state (never persisted)
    private string? _ticket;
    private string? _csrfToken;

    public string Host { get; private set; } = "";
    public int Port { get; private set; } = 8006;
    public string AuthMode { get; private set; } = "token";   // "token" | "ticket"
    public string TokenId { get; private set; } = "";
    public string User { get; private set; } = "root";
    public string Realm { get; private set; } = "pam";
    public bool TrustCert { get; private set; }
    public bool Connected { get; private set; }

    public ProxmoxService()
    {
        Host = SettingsStore.Get(KeyHost, "");
        Port = int.TryParse(SettingsStore.Get(KeyPort, "8006"), out var p) ? p : 8006;
        AuthMode = SettingsStore.Get(KeyAuthMode, "token");
        TokenId = SettingsStore.Get(KeyTokenId, "");
        User = SettingsStore.Get(KeyUser, "root");
        Realm = SettingsStore.Get(KeyRealm, "pam");
        TrustCert = SettingsStore.Get(KeyTrustCert, "0") == "1";
        _http = BuildClient(TrustCert);
    }

    public string BaseUrl => $"https://{Host}:{Port}/api2/json";

    /// <summary>有冇儲存過 token 秘密 · Whether an encrypted token secret is stored.</summary>
    public bool HasSavedTokenSecret => !string.IsNullOrEmpty(SettingsStore.Get(KeyTokenSecretEnc, ""));

    /// <summary>解密儲存嘅 token 秘密 · Decrypt the stored token secret ("" if none / on failure).</summary>
    public string SavedTokenSecret => Unprotect(SettingsStore.Get(KeyTokenSecretEnc, ""));

    // ── client / cert handling ─────────────────────────────────────────────────

    private static HttpClient BuildClient(bool trustCert)
    {
        var handler = new HttpClientHandler
        {
            CookieContainer = new CookieContainer(),
            UseCookies = true,
            AllowAutoRedirect = false,
        };
        if (trustCert)
        {
            // Only when the user has explicitly opted in to a current,
            // self-signed leaf with an otherwise clean one-element chain.
            handler.ServerCertificateCustomValidationCallback =
                static (HttpRequestMessage _, X509Certificate2? certificate, X509Chain? chain, SslPolicyErrors errors) =>
                    AcceptServerCertificate(certificate, chain, errors);
        }
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
    }

    /// <summary>
    /// Accept a normally trusted certificate, or the narrow self-signed case the UI promises.
    /// Name mismatches, missing certificates, expired leaves, non-self-issued leaves, multi-element
    /// chains, and every chain state other than UntrustedRoot remain rejected.
    /// </summary>
    internal static bool AcceptServerCertificate(X509Certificate2? certificate, X509Chain? chain,
        SslPolicyErrors errors, DateTime? utcNow = null)
    {
        if (errors == SslPolicyErrors.None) return true;
        if ((errors & (SslPolicyErrors.RemoteCertificateNotAvailable | SslPolicyErrors.RemoteCertificateNameMismatch)) != 0)
            return false;
        if (errors != SslPolicyErrors.RemoteCertificateChainErrors || certificate is null || chain is null)
            return false;

        var now = (utcNow ?? DateTime.UtcNow).ToUniversalTime();
        if (certificate.NotBefore.ToUniversalTime() > now || certificate.NotAfter.ToUniversalTime() < now)
            return false;
        if (!CryptographicOperations.FixedTimeEquals(certificate.SubjectName.RawData, certificate.IssuerName.RawData))
            return false;
        if (chain.ChainElements.Count != 1 ||
            !CryptographicOperations.FixedTimeEquals(chain.ChainElements[0].Certificate.RawData, certificate.RawData))
            return false;

        bool untrustedRoot = false;
        foreach (var status in chain.ChainStatus)
        {
            if (status.Status == X509ChainStatusFlags.NoError) continue;
            if (status.Status == X509ChainStatusFlags.UntrustedRoot)
            {
                untrustedRoot = true;
                continue;
            }
            return false;
        }
        return untrustedRoot;
    }

    private void RebuildClient()
    {
        try { _http.Dispose(); } catch { }
        _http = BuildClient(TrustCert);
    }

    // ── persistence ─────────────────────────────────────────────────────────────

    /// <summary>持久化連線設定（token 秘密用 DPAPI 加密；密碼從不儲存）· Persist connection settings.</summary>
    public void SaveConnection(string host, int port, string authMode, string tokenId, string? tokenSecret,
        string user, string realm, bool trustCert, bool rememberTokenSecret)
    {
        Host = (host ?? "").Trim();
        Port = port <= 0 ? 8006 : port;
        AuthMode = authMode == "ticket" ? "ticket" : "token";
        TokenId = (tokenId ?? "").Trim();
        User = string.IsNullOrWhiteSpace(user) ? "root" : user.Trim();
        Realm = string.IsNullOrWhiteSpace(realm) ? "pam" : realm.Trim();
        bool trustChanged = TrustCert != trustCert;
        TrustCert = trustCert;

        SettingsStore.Set(KeyHost, Host);
        SettingsStore.Set(KeyPort, Port.ToString(CultureInfo.InvariantCulture));
        SettingsStore.Set(KeyAuthMode, AuthMode);
        SettingsStore.Set(KeyTokenId, TokenId);
        SettingsStore.Set(KeyUser, User);
        SettingsStore.Set(KeyRealm, Realm);
        SettingsStore.Set(KeyTrustCert, trustCert ? "1" : "0");
        // Token secret is persisted (DPAPI-encrypted) only if the user opts to remember it.
        SettingsStore.Set(KeyTokenSecretEnc,
            rememberTokenSecret && !string.IsNullOrEmpty(tokenSecret) ? Protect(tokenSecret) : "");

        if (trustChanged) RebuildClient();
    }

    // ── auth ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 連線並驗證 · Connect and authenticate. For token mode, the secret is supplied per-call (or read from
    /// the saved encrypted store). For ticket mode, the password is supplied and exchanged for a ticket.
    /// A successful GET /version is used as the connectivity + auth probe.
    /// </summary>
    public async Task<PveResult> Connect(string? tokenSecret, string? password, CancellationToken ct = default)
    {
        Connected = false;
        _ticket = null;
        _csrfToken = null;

        if (string.IsNullOrWhiteSpace(Host))
            return PveResult.Fail("No host configured · 未設定主機", PveConnState.Unreachable);

        if (AuthMode == "ticket")
        {
            var login = await TicketLogin(password ?? "", ct).ConfigureAwait(false);
            if (!login.Ok) return login;
        }
        else
        {
            _activeTokenSecret = string.IsNullOrEmpty(tokenSecret) ? SavedTokenSecret : tokenSecret;
            if (string.IsNullOrWhiteSpace(TokenId) || string.IsNullOrWhiteSpace(_activeTokenSecret))
                return PveResult.Fail("API token incomplete · API token 不完整", PveConnState.Unauthorized);
        }

        // Probe with a read that requires auth.
        var probe = await Get("/version", ct).ConfigureAwait(false);
        Connected = probe.Ok;
        return probe;
    }

    public void Disconnect()
    {
        Connected = false;
        _ticket = null;
        _csrfToken = null;
        _activeTokenSecret = null;
    }

    private string? _activeTokenSecret;

    /// <summary>POST /access/ticket (form: username=user@realm, password) → ticket + CSRF token.</summary>
    private async Task<PveResult> TicketLogin(string password, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/access/ticket");
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = $"{User}@{Realm}",
                ["password"] = password ?? "",
            });
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return PveResult.Fail("Login failed · 登入失敗", PveConnState.Unauthorized);
            if (!resp.IsSuccessStatusCode)
                return new PveResult(false, body, (int)resp.StatusCode, PveConnState.Unauthorized);
            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data");
            _ticket = data.TryGetProperty("ticket", out var t) ? t.GetString() : null;
            _csrfToken = data.TryGetProperty("CSRFPreventionToken", out var c) ? c.GetString() : null;
            if (string.IsNullOrEmpty(_ticket))
                return PveResult.Fail("No ticket returned · 未收到 ticket", PveConnState.Unauthorized);
            return new PveResult(true, "Ok", (int)resp.StatusCode, PveConnState.Connected);
        }
        catch (Exception ex) { return PveResult.Fail(ex.Message, ClassifyException(ex)); }
    }

    private void AddAuthHeaders(HttpRequestMessage req, bool isWrite)
    {
        if (AuthMode == "ticket")
        {
            if (!string.IsNullOrEmpty(_ticket))
                req.Headers.TryAddWithoutValidation("Cookie", $"PVEAuthCookie={_ticket}");
            if (isWrite && !string.IsNullOrEmpty(_csrfToken))
                req.Headers.TryAddWithoutValidation("CSRFPreventionToken", _csrfToken);
        }
        else
        {
            // PVEAPIToken=USER@REALM!TOKENID=SECRET
            if (!string.IsNullOrEmpty(TokenId) && !string.IsNullOrEmpty(_activeTokenSecret))
                req.Headers.TryAddWithoutValidation("Authorization", $"PVEAPIToken={TokenId}={_activeTokenSecret}");
        }
    }

    // ── low-level GET / POST ──────────────────────────────────────────────────────

    private async Task<PveResult> Get(string path, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            AddAuthHeaders(req, isWrite: false);
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var state = StateFor(resp.StatusCode);
            return new PveResult(resp.IsSuccessStatusCode, body, (int)resp.StatusCode, state);
        }
        catch (Exception ex) { return PveResult.Fail(ex.Message, ClassifyException(ex)); }
    }

    private async Task<PveResult> Post(string path, IEnumerable<KeyValuePair<string, string>>? form, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + path);
            AddAuthHeaders(req, isWrite: true);
            req.Content = new FormUrlEncodedContent(form ?? Array.Empty<KeyValuePair<string, string>>());
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var state = StateFor(resp.StatusCode);
            return new PveResult(resp.IsSuccessStatusCode, body, (int)resp.StatusCode, state);
        }
        catch (Exception ex) { return PveResult.Fail(ex.Message, ClassifyException(ex)); }
    }

    private PveConnState StateFor(HttpStatusCode code) => code switch
    {
        HttpStatusCode.OK => PveConnState.Connected,
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => PveConnState.Unauthorized,
        _ => (int)code >= 500 ? PveConnState.Unreachable : PveConnState.Connected,
    };

    private static PveConnState ClassifyException(Exception ex)
    {
        // A TLS validation failure surfaces as an AuthenticationException nested in HttpRequestException.
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is System.Security.Authentication.AuthenticationException ||
                e.GetType().Name.Contains("Authentication", StringComparison.OrdinalIgnoreCase) ||
                (e.Message.Contains("certificate", StringComparison.OrdinalIgnoreCase) ||
                 e.Message.Contains("SSL", StringComparison.OrdinalIgnoreCase) ||
                 e.Message.Contains("TLS", StringComparison.OrdinalIgnoreCase) ||
                 e.Message.Contains("trust", StringComparison.OrdinalIgnoreCase)))
                return PveConnState.CertUntrusted;
        return PveConnState.Unreachable;
    }

    // ── discovery ─────────────────────────────────────────────────────────────────

    /// <summary>GET /nodes → list of cluster nodes.</summary>
    public async Task<List<PveNode>> GetNodes(CancellationToken ct = default)
    {
        var list = new List<PveNode>();
        var r = await Get("/nodes", ct).ConfigureAwait(false);
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                foreach (var e in data.EnumerateArray())
                    list.Add(new PveNode(
                        Str(e, "node"), Str(e, "status"), Dbl(e, "cpu"),
                        Lng(e, "mem"), Lng(e, "maxmem"), Lng(e, "uptime")));
        }
        catch { }
        return list.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>列出所有節點上嘅 VM 同容器 · List every QEMU VM and LXC container across all nodes.</summary>
    public async Task<List<PveGuest>> GetAllGuests(CancellationToken ct = default)
    {
        var result = new List<PveGuest>();
        var nodes = await GetNodes(ct).ConfigureAwait(false);
        foreach (var node in nodes)
        {
            if (!string.Equals(node.Status, "online", StringComparison.OrdinalIgnoreCase) && node.Status.Length > 0)
                continue;
            result.AddRange(await GetGuestsOnNode(node.Name, "qemu", ct).ConfigureAwait(false));
            result.AddRange(await GetGuestsOnNode(node.Name, "lxc", ct).ConfigureAwait(false));
        }
        return result
            .OrderBy(g => g.Node, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.VmId)
            .ToList();
    }

    private async Task<List<PveGuest>> GetGuestsOnNode(string node, string type, CancellationToken ct)
    {
        var list = new List<PveGuest>();
        var r = await Get($"/nodes/{Uri.EscapeDataString(node)}/{type}", ct).ConfigureAwait(false);
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                foreach (var e in data.EnumerateArray())
                {
                    bool template = (Lng(e, "template") != 0);
                    list.Add(new PveGuest
                    {
                        Node = node,
                        Type = type,
                        VmId = (int)Lng(e, "vmid"),
                        Name = Str(e, "name"),
                        Status = Str(e, "status"),
                        CpuFraction = Dbl(e, "cpu"),
                        MaxCpu = (int)Lng(e, "cpus"),
                        MemBytes = Lng(e, "mem"),
                        MaxMemBytes = Lng(e, "maxmem"),
                        DiskBytes = Lng(e, "disk"),
                        MaxDiskBytes = Lng(e, "maxdisk"),
                        UptimeSec = Lng(e, "uptime"),
                        Lock = Str(e, "lock"),
                        Template = template,
                    });
                }
        }
        catch { }
        return list;
    }

    /// <summary>GET …/{vmid}/config → raw config key/values (cores, memory, boot disk, net…).</summary>
    public async Task<Dictionary<string, string>> GetConfig(PveGuest g, CancellationToken ct = default)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var r = await Get($"/nodes/{Uri.EscapeDataString(g.Node)}/{g.Type}/{g.VmId}/config", ct).ConfigureAwait(false);
        if (!r.Ok) return dict;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                foreach (var prop in data.EnumerateObject())
                    dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.ToString();
        }
        catch { }
        return dict;
    }

    /// <summary>
    /// 嘗試由 guest agent 攞 IP（只限 QEMU + agent）· Best-effort guest IP via the QEMU guest agent.
    /// Returns null when no agent / not running. Failures are swallowed.
    /// </summary>
    public async Task<string?> TryGetGuestIp(PveGuest g, CancellationToken ct = default)
    {
        if (g.IsContainer || !g.IsRunning) return null;
        var r = await Get($"/nodes/{Uri.EscapeDataString(g.Node)}/qemu/{g.VmId}/agent/network-get-interfaces", ct).ConfigureAwait(false);
        if (!r.Ok) return null;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
            if (!data.TryGetProperty("result", out var ifaces) || ifaces.ValueKind != JsonValueKind.Array) return null;
            var ips = new List<string>();
            foreach (var iface in ifaces.EnumerateArray())
            {
                var nm = Str(iface, "name");
                if (nm.StartsWith("lo", StringComparison.OrdinalIgnoreCase)) continue;
                if (!iface.TryGetProperty("ip-addresses", out var addrs) || addrs.ValueKind != JsonValueKind.Array) continue;
                foreach (var a in addrs.EnumerateArray())
                {
                    var ip = Str(a, "ip-address");
                    var fam = Str(a, "ip-address-type");
                    if (string.IsNullOrEmpty(ip)) continue;
                    if (ip.StartsWith("127.") || ip == "::1") continue;
                    if (fam.Equals("ipv4", StringComparison.OrdinalIgnoreCase)) ips.Insert(0, ip);
                    else ips.Add(ip);
                }
            }
            return ips.Count == 0 ? null : string.Join(", ", ips.Distinct());
        }
        catch { return null; }
    }

    // ── power actions ─────────────────────────────────────────────────────────────

    private Task<PveResult> Power(PveGuest g, string action, CancellationToken ct)
        => Post($"/nodes/{Uri.EscapeDataString(g.Node)}/{g.Type}/{g.VmId}/status/{action}", null, ct);

    /// <summary>POST …/status/start — power the guest on.</summary>
    public Task<PveResult> Start(PveGuest g, CancellationToken ct = default) => Power(g, "start", ct);

    /// <summary>POST …/status/shutdown — graceful (ACPI) shutdown.</summary>
    public Task<PveResult> Shutdown(PveGuest g, CancellationToken ct = default) => Power(g, "shutdown", ct);

    /// <summary>POST …/status/stop — hard stop (pull the plug).</summary>
    public Task<PveResult> Stop(PveGuest g, CancellationToken ct = default) => Power(g, "stop", ct);

    /// <summary>POST …/status/reboot — reboot the guest.</summary>
    public Task<PveResult> Reboot(PveGuest g, CancellationToken ct = default) => Power(g, "reboot", ct);

    /// <summary>POST …/status/suspend — suspend (QEMU: to RAM; LXC: freeze).</summary>
    public Task<PveResult> Suspend(PveGuest g, CancellationToken ct = default) => Power(g, "suspend", ct);

    /// <summary>POST …/status/resume — resume a suspended guest.</summary>
    public Task<PveResult> Resume(PveGuest g, CancellationToken ct = default) => Power(g, "resume", ct);

    // ── DPAPI helpers ─────────────────────────────────────────────────────────────

    private static string Protect(string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return "";
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch { return ""; }
    }

    private static string Unprotect(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────────

    private static string Str(JsonElement e, string p)
        => e.TryGetProperty(p, out var v)
            ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : (v.ValueKind == JsonValueKind.Number ? v.ToString() : ""))
            : "";

    private static long Lng(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l)) return l;
        if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var s)) return s;
        return 0;
    }

    private static double Dbl(JsonElement e, string p)
    {
        if (!e.TryGetProperty(p, out var v)) return 0;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s)) return s;
        return 0;
    }

    // ── formatting (UI-facing) ──────────────────────────────────────────────────────

    public static string HumanSize(long bytes)
    {
        if (bytes <= 0) return "—";
        double b = bytes;
        string[] u = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        int i = 0;
        while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
        return i == 0 ? $"{bytes} {u[i]}" : $"{b:0.##} {u[i]}";
    }

    public static string HumanUptime(long secs)
    {
        if (secs <= 0) return "—";
        var t = TimeSpan.FromSeconds(secs);
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
        if (t.TotalHours >= 1) return $"{t.Hours}h {t.Minutes}m";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}m";
        return $"{t.Seconds}s";
    }

    /// <summary>把狀態碼變成雙語標籤 · Map a status code to a bilingual label.</summary>
    public static (string En, string Zh) StatusLabel(string status) => status.ToLowerInvariant() switch
    {
        "running" => ("Running", "運行中"),
        "stopped" => ("Stopped", "已停止"),
        "paused" => ("Paused", "已暫停"),
        "suspended" => ("Suspended", "已暫停"),
        _ => (status, status),
    };
}
