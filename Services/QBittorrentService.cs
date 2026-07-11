using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>一次 qBittorrent Web API 呼叫嘅結果 · Result of one qBittorrent Web API call.</summary>
public sealed record QbResult(bool Ok, string Body, int Status)
{
    public static QbResult Fail(string body) => new(false, body, 0);
}

/// <summary>一個 torrent 嘅快照（嚟自 /torrents/info）· One torrent row from /torrents/info.</summary>
public sealed class QbTorrent
{
    public string Hash { get; init; } = "";
    public string Name { get; init; } = "";
    public string State { get; init; } = "";
    public double Progress { get; init; }          // 0..1
    public long Size { get; init; }                 // wanted size, bytes
    public long Downloaded { get; init; }
    public long Uploaded { get; init; }
    public long DlSpeed { get; init; }              // bytes/s
    public long UpSpeed { get; init; }              // bytes/s
    public long Eta { get; init; }                  // seconds (8640000 = ∞)
    public double Ratio { get; init; }
    public string Category { get; init; } = "";
    public string Tags { get; init; } = "";
    public string SavePath { get; init; } = "";
    public int NumSeeds { get; init; }
    public int NumLeechs { get; init; }
    public long AddedOn { get; init; }              // unix secs
    public string MagnetUri { get; init; } = "";
}

/// <summary>傳輸全域統計（/transfer/info）· Global transfer stats.</summary>
public sealed record QbGlobalStats(long DlSpeed, long UpSpeed, long DlLimit, long UpLimit,
    string ConnectionStatus, long DhtNodes, bool AltSpeedEnabled);

/// <summary>一個分類（/torrents/categories）· One category (name + save path).</summary>
public sealed record QbCategory(string Name, string SavePath);

/// <summary>一個檔案（/torrents/files）· One file inside a torrent.</summary>
public sealed record QbFile(int Index, string Name, long Size, double Progress, int Priority);

/// <summary>一個 tracker（/torrents/trackers）· One tracker row.</summary>
public sealed record QbTracker(string Url, string Status, int NumPeers, int NumSeeds, string Message);

/// <summary>一個 peer（/sync/torrentPeers）· One connected peer.</summary>
public sealed record QbPeer(string IpPort, string Client, double Progress, long DlSpeed, long UpSpeed, string Flags);

/// <summary>
/// 應用程式內 qBittorrent Web API v2 客戶端 · In-app qBittorrent Web API v2 client.
/// HttpClient + CookieContainer holds the SID session cookie obtained from /api/v2/auth/login.
/// Host/port/user persist via SettingsStore (password is NOT persisted by default; an opt-in remembered
/// password is DPAPI-encrypted for the current Windows user). Everything (login, torrent lifecycle, categories/tags, files/trackers/
/// peers, speed limits, preferences) runs in-app against the local WebUI — no redirect. Endpoints
/// verified against the upstream torrentscontroller / transfercontroller / appcontroller sources and
/// the documented Web API v2 (v4.6 / v5: stop/start are the canonical pause/resume actions).
/// </summary>
public sealed class QBittorrentService
{
    public const string KeyHost = "qb.host";
    public const string KeyPort = "qb.port";
    public const string KeyUser = "qb.user";
    /// <summary>Historical plaintext key retained only for one-time migration; never write a new value here.</summary>
    public const string KeyPass = QBittorrentCredentialStore.LegacyPasswordKey;
    /// <summary>Current-user DPAPI blob for the remembered WebUI password.</summary>
    public const string KeyPassEncrypted = QBittorrentCredentialStore.PasswordBlobKey;
    public const string KeyRemember = "qb.remember";

    private readonly CookieContainer _cookies = new();
    private readonly HttpClient _http;
    private readonly QBittorrentCredentialStore _credentials;

    public string Host { get; private set; } = "localhost";
    public int Port { get; private set; } = 8080;
    public string User { get; private set; } = "admin";
    public bool LoggedIn { get; private set; }

    public QBittorrentService()
    {
        _credentials = new QBittorrentCredentialStore(new SettingsAdapter(), new DpapiQBittorrentSecretProtector());
        // Convert the legacy qb.pass value before this service can expose a saved password.
        // A DPAPI failure intentionally fails closed and leaves the legacy value untouched for recovery.
        _credentials.MigrateLegacyPassword();
        Host = SettingsStore.Get(KeyHost, "localhost");
        Port = int.TryParse(SettingsStore.Get(KeyPort, "8080"), out var p) ? p : 8080;
        User = SettingsStore.Get(KeyUser, "admin");
        var handler = new HttpClientHandler { CookieContainer = _cookies, UseCookies = true, AllowAutoRedirect = false };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        // qBittorrent rejects requests whose Referer/Origin host does not match unless WebUI CSRF/host
        // checks are relaxed; sending a matching Referer keeps it happy on default settings.
    }

    public string BaseUrl => $"http://{Host}:{Port}";
    private string Api(string path) => $"{BaseUrl}/api/v2{path}";

    public string SavedPassword => _credentials.ReadSavedPassword();
    public bool Remember => _credentials.Remember;

    /// <summary>持久化連線設定 · Persist host/port/user (and password only if remember=true).</summary>
    public bool SaveConnection(string host, int port, string user, string password, bool remember)
    {
        Host = string.IsNullOrWhiteSpace(host) ? "localhost" : host.Trim();
        Port = port <= 0 ? 8080 : port;
        User = string.IsNullOrWhiteSpace(user) ? "admin" : user.Trim();
        SettingsStore.Set(KeyHost, Host);
        SettingsStore.Set(KeyPort, Port.ToString(CultureInfo.InvariantCulture));
        SettingsStore.Set(KeyUser, User);
        return _credentials.SaveRememberedPassword(password, remember);
    }

    private void AddCommonHeaders(HttpRequestMessage req)
    {
        // Matching Referer satisfies qBittorrent's default cross-site request guard.
        req.Headers.TryAddWithoutValidation("Referer", BaseUrl);
        req.Headers.TryAddWithoutValidation("Origin", BaseUrl);
    }

    // ── Auth ─────────────────────────────────────────────────────────────────

    /// <summary>POST /auth/login (form: username, password). On success the SID cookie is stored.</summary>
    public async Task<QbResult> Login(string password, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Api("/auth/login"));
            AddCommonHeaders(req);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = User,
                ["password"] = password ?? "",
            });
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = (await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).Trim();
            ct.ThrowIfCancellationRequested();
            // qBittorrent returns "Ok." on success, "Fails." on bad creds, and 403 if IP-banned.
            bool ok = resp.IsSuccessStatusCode && body.Equals("Ok.", StringComparison.OrdinalIgnoreCase);
            LoggedIn = ok;
            if (!ok && resp.IsSuccessStatusCode && string.IsNullOrEmpty(body))
            {
                // Some builds answer with an empty 200 + cookie; treat presence of SID as success.
                LoggedIn = HasSidCookie();
                ok = LoggedIn;
            }
            return new QbResult(ok, ok ? "Ok." : (body.Length == 0 ? $"HTTP {(int)resp.StatusCode}" : body), (int)resp.StatusCode);
        }
        catch (Exception ex) { return QbResult.Fail(ex.Message); }
    }

    private bool HasSidCookie()
    {
        try { return _cookies.GetCookies(new Uri(BaseUrl)).Cast<Cookie>().Any(c => c.Name == "SID"); }
        catch { return false; }
    }

    /// <summary>POST /auth/logout — clears the server session.</summary>
    public async Task Logout(CancellationToken ct = default)
    {
        // Prevent a racing refresh from treating this client as connected while the best-effort
        // server logout request is still in flight.
        LoggedIn = false;
        try { await Post("/auth/logout", null, ct).ConfigureAwait(false); } catch { }
    }

    private sealed class SettingsAdapter : IQBittorrentSettingsStore
    {
        public string Get(string key, string fallback) => SettingsStore.Get(key, fallback);
        public void Set(string key, string value) => SettingsStore.Set(key, value);
    }

    // ── Low-level GET / POST ───────────────────────────────────────────────────

    private async Task<QbResult> Get(string path, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Api(path));
            AddCommonHeaders(req);
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (resp.StatusCode == HttpStatusCode.Forbidden) LoggedIn = false;
            return new QbResult(resp.IsSuccessStatusCode, body, (int)resp.StatusCode);
        }
        catch (Exception ex) { return QbResult.Fail(ex.Message); }
    }

    private async Task<QbResult> Post(string path, IEnumerable<KeyValuePair<string, string>>? form, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Api(path));
            AddCommonHeaders(req);
            req.Content = new FormUrlEncodedContent(form ?? Array.Empty<KeyValuePair<string, string>>());
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            if (resp.StatusCode == HttpStatusCode.Forbidden) LoggedIn = false;
            return new QbResult(resp.IsSuccessStatusCode, body, (int)resp.StatusCode);
        }
        catch (Exception ex) { return QbResult.Fail(ex.Message); }
    }

    private static KeyValuePair<string, string> KV(string k, string v) => new(k, v);

    // ── App / version ──────────────────────────────────────────────────────────

    /// <summary>GET /app/version — the qBittorrent version string (also a quick connectivity probe).</summary>
    public Task<QbResult> AppVersion(CancellationToken ct = default) => Get("/app/version", ct);

    /// <summary>GET /app/webapiVersion — the Web API version.</summary>
    public Task<QbResult> WebApiVersion(CancellationToken ct = default) => Get("/app/webapiVersion", ct);

    /// <summary>GET /app/preferences — raw JSON of all WebUI/app preferences.</summary>
    public Task<QbResult> GetPreferences(CancellationToken ct = default) => Get("/app/preferences", ct);

    /// <summary>POST /app/setPreferences (form json={...}) — set one or more preferences.</summary>
    public Task<QbResult> SetPreferences(string jsonObject, CancellationToken ct = default)
        => Post("/app/setPreferences", new[] { KV("json", jsonObject) }, ct);

    /// <summary>GET /app/defaultSavePath — the default download directory.</summary>
    public Task<QbResult> DefaultSavePath(CancellationToken ct = default) => Get("/app/defaultSavePath", ct);

    // ── Transfer / global stats ────────────────────────────────────────────────

    /// <summary>GET /transfer/info → parsed global speeds + limits + connection status.</summary>
    public async Task<QbGlobalStats?> GlobalStats(CancellationToken ct = default)
    {
        var r = await Get("/transfer/info", ct).ConfigureAwait(false);
        if (!r.Ok) return null;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            var e = doc.RootElement;
            bool alt = false;
            var ra = await Get("/transfer/speedLimitsMode", ct).ConfigureAwait(false);
            if (ra.Ok) alt = ra.Body.Trim() == "1";
            return new QbGlobalStats(
                Lng(e, "dl_info_speed"), Lng(e, "up_info_speed"),
                Lng(e, "dl_rate_limit"), Lng(e, "up_rate_limit"),
                Str(e, "connection_status"), Lng(e, "dht_nodes"), alt);
        }
        catch { return null; }
    }

    /// <summary>GET /transfer/speedLimitsMode → "1" if alt-speed (throttle) is currently active.</summary>
    public async Task<bool> IsAltSpeedOn(CancellationToken ct = default)
    {
        var r = await Get("/transfer/speedLimitsMode", ct).ConfigureAwait(false);
        return r.Ok && r.Body.Trim() == "1";
    }

    /// <summary>POST /transfer/toggleSpeedLimitsMode — flip the alternative-speed (throttle) mode.</summary>
    public Task<QbResult> ToggleAltSpeed(CancellationToken ct = default)
        => Post("/transfer/toggleSpeedLimitsMode", null, ct);

    /// <summary>POST /transfer/setDownloadLimit (limit bytes/s, 0 = unlimited).</summary>
    public Task<QbResult> SetGlobalDownloadLimit(long bytesPerSec, CancellationToken ct = default)
        => Post("/transfer/setDownloadLimit", new[] { KV("limit", bytesPerSec.ToString(CultureInfo.InvariantCulture)) }, ct);

    /// <summary>POST /transfer/setUploadLimit (limit bytes/s, 0 = unlimited).</summary>
    public Task<QbResult> SetGlobalUploadLimit(long bytesPerSec, CancellationToken ct = default)
        => Post("/transfer/setUploadLimit", new[] { KV("limit", bytesPerSec.ToString(CultureInfo.InvariantCulture)) }, ct);

    // ── Torrent list ───────────────────────────────────────────────────────────

    /// <summary>
    /// GET /torrents/info → parsed torrent rows. Optional filter ("all"/"downloading"/"completed"/
    /// "stopped"/"active"/…), category and search string narrow the result. A null category
    /// requests every category; an empty category requests qBittorrent's uncategorised torrents.
    /// </summary>
    public async Task<List<QbTorrent>> GetTorrents(string filter = "all", string? category = null,
        string? search = null, CancellationToken ct = default)
    {
        var sb = new StringBuilder("/torrents/info?filter=").Append(Uri.EscapeDataString(filter));
        if (category is not null) sb.Append("&category=").Append(Uri.EscapeDataString(category));
        var r = await Get(sb.ToString(), ct).ConfigureAwait(false);
        var list = new List<QbTorrent>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                var name = Str(e, "name");
                if (!string.IsNullOrEmpty(search) &&
                    name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(new QbTorrent
                {
                    Hash = Str(e, "hash"),
                    Name = name,
                    State = Str(e, "state"),
                    Progress = Dbl(e, "progress"),
                    Size = Lng(e, "size"),
                    Downloaded = Lng(e, "downloaded"),
                    Uploaded = Lng(e, "uploaded"),
                    DlSpeed = Lng(e, "dlspeed"),
                    UpSpeed = Lng(e, "upspeed"),
                    Eta = Lng(e, "eta"),
                    Ratio = Dbl(e, "ratio"),
                    Category = Str(e, "category"),
                    Tags = Str(e, "tags"),
                    SavePath = Str(e, "save_path"),
                    NumSeeds = (int)Lng(e, "num_seeds"),
                    NumLeechs = (int)Lng(e, "num_leechs"),
                    AddedOn = Lng(e, "added_on"),
                    MagnetUri = Str(e, "magnet_uri"),
                });
            }
        }
        catch { }
        return list;
    }

    // ── Add torrents ───────────────────────────────────────────────────────────

    /// <summary>
    /// POST /torrents/add (multipart) with one or more local .torrent files plus optional save path,
    /// category, tags, paused-on-add flag.
    /// </summary>
    public async Task<QbResult> AddFiles(IEnumerable<string> filePaths, string? savePath, string? category,
        string? tags, bool startPaused, CancellationToken ct = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            int n = 0;
            foreach (var path in filePaths)
            {
                if (!File.Exists(path)) continue;
                var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
                var part = new ByteArrayContent(bytes);
                part.Headers.TryAddWithoutValidation("Content-Type", "application/x-bittorrent");
                content.Add(part, "torrents", Path.GetFileName(path));
                n++;
            }
            if (n == 0) return QbResult.Fail("No valid .torrent files · 冇有效嘅 .torrent 檔");
            AppendAddOptions(content, savePath, category, tags, startPaused);
            return await PostMultipart("/torrents/add", content, ct).ConfigureAwait(false);
        }
        catch (Exception ex) { return QbResult.Fail(ex.Message); }
    }

    /// <summary>
    /// POST /torrents/add (multipart) with magnet/HTTP URLs (newline-separated) plus optional options.
    /// </summary>
    public Task<QbResult> AddUrls(string urlsNewlineSeparated, string? savePath, string? category,
        string? tags, bool startPaused, CancellationToken ct = default)
    {
        var content = new MultipartFormDataContent { { new StringContent(urlsNewlineSeparated), "urls" } };
        AppendAddOptions(content, savePath, category, tags, startPaused);
        return PostMultipart("/torrents/add", content, ct);
    }

    private static void AppendAddOptions(MultipartFormDataContent content, string? savePath, string? category,
        string? tags, bool startPaused)
    {
        if (!string.IsNullOrWhiteSpace(savePath)) content.Add(new StringContent(savePath), "savepath");
        if (!string.IsNullOrWhiteSpace(category)) content.Add(new StringContent(category), "category");
        if (!string.IsNullOrWhiteSpace(tags)) content.Add(new StringContent(tags), "tags");
        // "stopped" is the v5 key; "paused" is honoured by older builds — send both for safety.
        content.Add(new StringContent(startPaused ? "true" : "false"), "stopped");
        content.Add(new StringContent(startPaused ? "true" : "false"), "paused");
    }

    private async Task<QbResult> PostMultipart(string path, MultipartFormDataContent content, CancellationToken ct)
    {
        try
        {
            using (content)
            using (var req = new HttpRequestMessage(HttpMethod.Post, Api(path)) { Content = content })
            {
                AddCommonHeaders(req);
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                if (resp.StatusCode == HttpStatusCode.Forbidden) LoggedIn = false;
                return new QbResult(resp.IsSuccessStatusCode, body, (int)resp.StatusCode);
            }
        }
        catch (Exception ex) { return QbResult.Fail(ex.Message); }
    }

    // ── Lifecycle (pause / resume / delete / recheck / reannounce) ──────────────

    private static string HashList(IEnumerable<string> hashes) => string.Join("|", hashes);

    /// <summary>POST /torrents/stop (the canonical "pause"); hashes="all" stops everything.</summary>
    public Task<QbResult> Pause(IEnumerable<string> hashes, CancellationToken ct = default)
        => Post("/torrents/stop", new[] { KV("hashes", HashList(hashes)) }, ct);

    public Task<QbResult> PauseAll(CancellationToken ct = default)
        => Post("/torrents/stop", new[] { KV("hashes", "all") }, ct);

    /// <summary>POST /torrents/start (the canonical "resume").</summary>
    public Task<QbResult> Resume(IEnumerable<string> hashes, CancellationToken ct = default)
        => Post("/torrents/start", new[] { KV("hashes", HashList(hashes)) }, ct);

    public Task<QbResult> ResumeAll(CancellationToken ct = default)
        => Post("/torrents/start", new[] { KV("hashes", "all") }, ct);

    /// <summary>POST /torrents/delete (deleteFiles toggles removing data from disk too).</summary>
    public Task<QbResult> Delete(IEnumerable<string> hashes, bool deleteFiles, CancellationToken ct = default)
        => Post("/torrents/delete", new[]
        {
            KV("hashes", HashList(hashes)),
            KV("deleteFiles", deleteFiles ? "true" : "false"),
        }, ct);

    /// <summary>POST /torrents/recheck — force a hash re-check.</summary>
    public Task<QbResult> Recheck(IEnumerable<string> hashes, CancellationToken ct = default)
        => Post("/torrents/recheck", new[] { KV("hashes", HashList(hashes)) }, ct);

    /// <summary>POST /torrents/reannounce — force a tracker re-announce.</summary>
    public Task<QbResult> Reannounce(IEnumerable<string> hashes, CancellationToken ct = default)
        => Post("/torrents/reannounce", new[] { KV("hashes", HashList(hashes)) }, ct);

    /// <summary>POST /torrents/topPrio / bottomPrio / increasePrio / decreasePrio.</summary>
    public Task<QbResult> QueueMove(string direction, IEnumerable<string> hashes, CancellationToken ct = default)
        => Post($"/torrents/{direction}", new[] { KV("hashes", HashList(hashes)) }, ct);

    /// <summary>POST /torrents/setLocation (location) — move the torrent's save path.</summary>
    public Task<QbResult> SetLocation(IEnumerable<string> hashes, string location, CancellationToken ct = default)
        => Post("/torrents/setLocation", new[] { KV("hashes", HashList(hashes)), KV("location", location) }, ct);

    /// <summary>POST /torrents/rename (hash, name) — rename a torrent.</summary>
    public Task<QbResult> RenameTorrent(string hash, string name, CancellationToken ct = default)
        => Post("/torrents/rename", new[] { KV("hash", hash), KV("name", name) }, ct);

    /// <summary>POST /torrents/downloadLimit / uploadLimit per-torrent (bytes/s, 0 = unlimited).</summary>
    public Task<QbResult> SetTorrentDownloadLimit(IEnumerable<string> hashes, long limit, CancellationToken ct = default)
        => Post("/torrents/setDownloadLimit", new[] { KV("hashes", HashList(hashes)), KV("limit", limit.ToString(CultureInfo.InvariantCulture)) }, ct);

    public Task<QbResult> SetTorrentUploadLimit(IEnumerable<string> hashes, long limit, CancellationToken ct = default)
        => Post("/torrents/setUploadLimit", new[] { KV("hashes", HashList(hashes)), KV("limit", limit.ToString(CultureInfo.InvariantCulture)) }, ct);

    // ── Per-torrent detail (files / trackers / peers) ──────────────────────────

    /// <summary>GET /torrents/files?hash=… → file list.</summary>
    public async Task<List<QbFile>> Files(string hash, CancellationToken ct = default)
    {
        var r = await Get($"/torrents/files?hash={Uri.EscapeDataString(hash)}", ct).ConfigureAwait(false);
        var list = new List<QbFile>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            int i = 0;
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                int idx = e.TryGetProperty("index", out var ix) && ix.ValueKind == JsonValueKind.Number ? ix.GetInt32() : i;
                list.Add(new QbFile(idx, Str(e, "name"), Lng(e, "size"), Dbl(e, "progress"), (int)Lng(e, "priority")));
                i++;
            }
        }
        catch { }
        return list;
    }

    /// <summary>POST /torrents/filePrio (hash, id, priority) — set a file's priority (0 = don't download).</summary>
    public Task<QbResult> SetFilePriority(string hash, int fileId, int priority, CancellationToken ct = default)
        => Post("/torrents/filePrio", new[]
        {
            KV("hash", hash), KV("id", fileId.ToString(CultureInfo.InvariantCulture)),
            KV("priority", priority.ToString(CultureInfo.InvariantCulture)),
        }, ct);

    /// <summary>GET /torrents/trackers?hash=… → tracker rows.</summary>
    public async Task<List<QbTracker>> Trackers(string hash, CancellationToken ct = default)
    {
        var r = await Get($"/torrents/trackers?hash={Uri.EscapeDataString(hash)}", ct).ConfigureAwait(false);
        var list = new List<QbTracker>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                int st = (int)Lng(e, "status");
                string status = st switch
                {
                    0 => "Disabled", 1 => "Not contacted", 2 => "Working", 3 => "Updating", 4 => "Not working", _ => st.ToString()
                };
                list.Add(new QbTracker(Str(e, "url"), status, (int)Lng(e, "num_peers"), (int)Lng(e, "num_seeds"), Str(e, "msg")));
            }
        }
        catch { }
        return list;
    }

    /// <summary>GET /sync/torrentPeers?hash=… → connected peers.</summary>
    public async Task<List<QbPeer>> Peers(string hash, CancellationToken ct = default)
    {
        var r = await Get($"/sync/torrentPeers?hash={Uri.EscapeDataString(hash)}&rid=0", ct).ConfigureAwait(false);
        var list = new List<QbPeer>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (doc.RootElement.TryGetProperty("peers", out var peers) && peers.ValueKind == JsonValueKind.Object)
            {
                foreach (var kv in peers.EnumerateObject())
                {
                    var e = kv.Value;
                    list.Add(new QbPeer(kv.Name, Str(e, "client"), Dbl(e, "progress"),
                        Lng(e, "dl_speed"), Lng(e, "up_speed"), Str(e, "flags")));
                }
            }
        }
        catch { }
        return list;
    }

    // ── Categories ─────────────────────────────────────────────────────────────

    /// <summary>GET /torrents/categories → category name → save path.</summary>
    public async Task<List<QbCategory>> Categories(CancellationToken ct = default)
    {
        var r = await Get("/torrents/categories", ct).ConfigureAwait(false);
        var list = new List<QbCategory>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var kv in doc.RootElement.EnumerateObject())
                    list.Add(new QbCategory(Str(kv.Value, "name").Length > 0 ? Str(kv.Value, "name") : kv.Name,
                        Str(kv.Value, "savePath")));
        }
        catch { }
        return list.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>POST /torrents/createCategory (category, savePath).</summary>
    public Task<QbResult> CreateCategory(string name, string savePath, CancellationToken ct = default)
        => Post("/torrents/createCategory", new[] { KV("category", name), KV("savePath", savePath ?? "") }, ct);

    /// <summary>POST /torrents/editCategory (category, savePath).</summary>
    public Task<QbResult> EditCategory(string name, string savePath, CancellationToken ct = default)
        => Post("/torrents/editCategory", new[] { KV("category", name), KV("savePath", savePath ?? "") }, ct);

    /// <summary>POST /torrents/removeCategories (categories newline-separated).</summary>
    public Task<QbResult> RemoveCategories(IEnumerable<string> names, CancellationToken ct = default)
        => Post("/torrents/removeCategories", new[] { KV("categories", string.Join("\n", names)) }, ct);

    /// <summary>POST /torrents/setCategory (hashes, category) — assign a category to torrents.</summary>
    public Task<QbResult> SetCategory(IEnumerable<string> hashes, string category, CancellationToken ct = default)
        => Post("/torrents/setCategory", new[] { KV("hashes", HashList(hashes)), KV("category", category) }, ct);

    // ── Tags ───────────────────────────────────────────────────────────────────

    /// <summary>GET /torrents/tags → all tag names.</summary>
    public async Task<List<string>> Tags(CancellationToken ct = default)
    {
        var r = await Get("/torrents/tags", ct).ConfigureAwait(false);
        var list = new List<string>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var e in doc.RootElement.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String) list.Add(e.GetString() ?? "");
        }
        catch { }
        return list.Where(t => t.Length > 0).OrderBy(t => t, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>POST /torrents/createTags (tags comma-separated).</summary>
    public Task<QbResult> CreateTags(string commaTags, CancellationToken ct = default)
        => Post("/torrents/createTags", new[] { KV("tags", commaTags) }, ct);

    /// <summary>POST /torrents/deleteTags (tags comma-separated).</summary>
    public Task<QbResult> DeleteTags(string commaTags, CancellationToken ct = default)
        => Post("/torrents/deleteTags", new[] { KV("tags", commaTags) }, ct);

    /// <summary>POST /torrents/addTags (hashes, tags) — attach tags to torrents.</summary>
    public Task<QbResult> AddTags(IEnumerable<string> hashes, string commaTags, CancellationToken ct = default)
        => Post("/torrents/addTags", new[] { KV("hashes", HashList(hashes)), KV("tags", commaTags) }, ct);

    /// <summary>POST /torrents/removeTags (hashes, tags) — detach tags from torrents.</summary>
    public Task<QbResult> RemoveTags(IEnumerable<string> hashes, string commaTags, CancellationToken ct = default)
        => Post("/torrents/removeTags", new[] { KV("hashes", HashList(hashes)), KV("tags", commaTags) }, ct);

    // ── JSON helpers ───────────────────────────────────────────────────────────

    private static string Str(JsonElement e, string p)
        => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static long Lng(JsonElement e, string p)
        => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l) ? l : 0;

    private static double Dbl(JsonElement e, string p)
        => e.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : 0;

    // ── Formatting helpers (UI-facing) ─────────────────────────────────────────

    public static string HumanSize(long bytes)
    {
        if (bytes < 0) return "—";
        double b = bytes;
        string[] u = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        int i = 0;
        while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
        return i == 0 ? $"{bytes} {u[i]}" : $"{b:0.##} {u[i]}";
    }

    public static string HumanSpeed(long bytesPerSec)
        => bytesPerSec <= 0 ? "—" : HumanSize(bytesPerSec) + "/s";

    public static string HumanEta(long secs)
    {
        if (secs <= 0 || secs >= 8640000) return "∞";
        var t = TimeSpan.FromSeconds(secs);
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
        if (t.TotalHours >= 1) return $"{t.Hours}h {t.Minutes}m";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
        return $"{t.Seconds}s";
    }

    /// <summary>把英文 state code 變成雙語顯示 · Map a state code to a bilingual label.</summary>
    public static (string En, string Zh) StateLabel(string state) => state switch
    {
        "error" => ("Error", "錯誤"),
        "missingFiles" => ("Missing files", "缺少檔案"),
        "uploading" or "forcedUP" => ("Seeding", "做種"),
        "stoppedUP" => ("Completed", "已完成"),
        "queuedUP" => ("Queued (seed)", "排隊（做種）"),
        "stalledUP" => ("Seeding (idle)", "做種（停滯）"),
        "checkingUP" or "checkingDL" or "checkingResumeData" => ("Checking", "檢查中"),
        "downloading" or "forcedDL" => ("Downloading", "下載中"),
        "metaDL" or "forcedMetaDL" => ("Fetching metadata", "取中繼資料"),
        "stoppedDL" => ("Stopped", "已停止"),
        "queuedDL" => ("Queued", "排隊中"),
        "stalledDL" => ("Stalled", "停滯"),
        "moving" => ("Moving", "移動中"),
        _ => (state, state),
    };

    public static bool IsPausedState(string state)
        => state is "stoppedDL" or "stoppedUP" or "pausedDL" or "pausedUP" or "error" or "missingFiles";
}
