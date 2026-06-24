using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一次 HA REST 呼叫嘅結果 · Result of one Home Assistant REST call.</summary>
public sealed record HaResult(bool Ok, string Body, int Status)
{
    public static HaResult Fail(string body) => new(false, body, 0);
}

/// <summary>HA 嘅一個實體（state）· One Home Assistant entity state row.</summary>
public sealed class HaEntity
{
    public string EntityId { get; init; } = "";
    public string State { get; init; } = "";
    public string FriendlyName { get; init; } = "";
    public string Domain => EntityId.Contains('.') ? EntityId[..EntityId.IndexOf('.')] : "";
    public string Display => string.IsNullOrWhiteSpace(FriendlyName) ? EntityId : $"{FriendlyName} ({EntityId})";
}

/// <summary>歷史走勢嘅一個取樣點 · One numeric sample from /api/history/period.</summary>
public sealed record HaHistoryPoint(DateTime When, double Value);

/// <summary>日曆事件 · One calendar event.</summary>
public sealed class HaCalendarEvent
{
    public string Summary { get; init; } = "";
    public string Start { get; init; } = "";
    public string End { get; init; } = "";
}

/// <summary>
/// 應用程式內 Home Assistant REST 控制 · In-app Home Assistant control over the documented REST API
/// (HttpClient + long-lived access token). Base URL + token persist via SettingsStore. No redirect —
/// everything (template render, config check + restart, history, states, camera, scenes/scripts,
/// events, calendars, error log, light/climate/notify/intent) runs in-app against the real endpoints.
/// Endpoints verified against developers.home-assistant.io/docs/api/rest.
/// </summary>
public sealed class HomeAssistantService
{
    public const string KeyBaseUrl = "ha.baseUrl";
    public const string KeyToken = "ha.token";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string BaseUrl { get; private set; } = "";
    public string Token { get; private set; } = "";

    public HomeAssistantService()
    {
        BaseUrl = SettingsStore.Get(KeyBaseUrl, "");
        Token = SettingsStore.Get(KeyToken, "");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Token);

    /// <summary>持久化 base URL + token · Persist config (URL is normalised, trailing slash trimmed).</summary>
    public void SaveConfig(string baseUrl, string token)
    {
        BaseUrl = (baseUrl ?? "").Trim().TrimEnd('/');
        Token = (token ?? "").Trim();
        SettingsStore.Set(KeyBaseUrl, BaseUrl);
        SettingsStore.Set(KeyToken, Token);
    }

    private HttpRequestMessage Build(HttpMethod method, string path, string? jsonBody = null)
    {
        var req = new HttpRequestMessage(method, BaseUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        req.Headers.Accept.ParseAdd("application/json");
        if (jsonBody is not null)
            req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return req;
    }

    private async Task<HaResult> SendText(HttpMethod method, string path, string? jsonBody, CancellationToken ct)
    {
        if (!IsConfigured) return HaResult.Fail("Not configured · 未設定");
        try
        {
            using var resp = await Http.SendAsync(Build(method, path, jsonBody), ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return new HaResult(resp.IsSuccessStatusCode, body, (int)resp.StatusCode);
        }
        catch (Exception ex) { return HaResult.Fail(ex.Message); }
    }

    // ── Connectivity ──────────────────────────────────────────────────────────

    /// <summary>GET /api/ — verifies URL + token. Returns the {"message":"API running."} body.</summary>
    public Task<HaResult> Ping(CancellationToken ct = default) => SendText(HttpMethod.Get, "/api/", null, ct);

    // ── Entities ─────────────────────────────────────────────────────────────

    /// <summary>GET /api/states → all entities (optionally filtered to one or more domain prefixes).</summary>
    public async Task<List<HaEntity>> States(string[]? domains = null, CancellationToken ct = default)
    {
        var r = await SendText(HttpMethod.Get, "/api/states", null, ct).ConfigureAwait(false);
        var list = new List<HaEntity>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.TryGetProperty("entity_id", out var eid) ? eid.GetString() ?? "" : "";
                if (id.Length == 0) continue;
                if (domains is { Length: > 0 } && !domains.Any(d => id.StartsWith(d + ".", StringComparison.Ordinal))) continue;
                string fn = "";
                if (el.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object
                    && attrs.TryGetProperty("friendly_name", out var f))
                    fn = f.GetString() ?? "";
                list.Add(new HaEntity
                {
                    EntityId = id,
                    State = el.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "",
                    FriendlyName = fn,
                });
            }
        }
        catch { /* tolerate malformed payloads */ }
        return list.OrderBy(e => e.EntityId, StringComparer.Ordinal).ToList();
    }

    /// <summary>POST /api/states/&lt;entity_id&gt; — set/override an in-memory state (+ optional attrs JSON).</summary>
    public Task<HaResult> SetState(string entityId, string state, string? attributesJson, CancellationToken ct = default)
    {
        string body;
        if (!string.IsNullOrWhiteSpace(attributesJson))
            body = $"{{\"state\":{JsonString(state)},\"attributes\":{attributesJson}}}";
        else
            body = $"{{\"state\":{JsonString(state)}}}";
        return SendText(HttpMethod.Post, $"/api/states/{entityId}", body, ct);
    }

    // ── Templates ────────────────────────────────────────────────────────────

    /// <summary>POST /api/template {"template":"..."} → rendered plain-text result.</summary>
    public Task<HaResult> RenderTemplate(string template, CancellationToken ct = default)
        => SendText(HttpMethod.Post, "/api/template", $"{{\"template\":{JsonString(template)}}}", ct);

    // ── Config check + restart ───────────────────────────────────────────────

    /// <summary>POST /api/config/core/check_config → {"result":"valid"|"invalid","errors":...}.</summary>
    public Task<HaResult> CheckConfig(CancellationToken ct = default)
        => SendText(HttpMethod.Post, "/api/config/core/check_config", "", ct);

    /// <summary>POST /api/services/homeassistant/restart — only call after a valid check.</summary>
    public Task<HaResult> Restart(CancellationToken ct = default)
        => SendText(HttpMethod.Post, "/api/services/homeassistant/restart", "{}", ct);

    /// <summary>POST /api/services/homeassistant/reload_config_entry {"entry_id":"..."}.</summary>
    public Task<HaResult> ReloadConfigEntry(string entryId, CancellationToken ct = default)
        => SendText(HttpMethod.Post, "/api/services/homeassistant/reload_config_entry",
            $"{{\"entry_id\":{JsonString(entryId)}}}", ct);

    /// <summary>POST /api/services/&lt;domain&gt;/reload — e.g. automation/scene/template/script.</summary>
    public Task<HaResult> ReloadDomain(string domain, CancellationToken ct = default)
        => SendText(HttpMethod.Post, $"/api/services/{domain}/reload", "{}", ct);

    // ── History ──────────────────────────────────────────────────────────────

    /// <summary>
    /// GET /api/history/period/&lt;ISO start&gt;?filter_entity_id=..&amp;end_time=..&amp;minimal_response.
    /// Returns numeric samples over the last <paramref name="hours"/> for a sparkline.
    /// </summary>
    public async Task<List<HaHistoryPoint>> History(string entityId, int hours = 24, CancellationToken ct = default)
    {
        var end = DateTime.UtcNow;
        var start = end.AddHours(-hours);
        string startIso = Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        string endIso = Uri.EscapeDataString(end.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        string path = $"/api/history/period/{startIso}?filter_entity_id={Uri.EscapeDataString(entityId)}&end_time={endIso}&minimal_response";
        var r = await SendText(HttpMethod.Get, path, null, ct).ConfigureAwait(false);
        var pts = new List<HaHistoryPoint>();
        if (!r.Ok) return pts;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            foreach (var series in doc.RootElement.EnumerateArray())
            {
                foreach (var st in series.EnumerateArray())
                {
                    string raw = st.TryGetProperty("state", out var s) ? s.GetString() ?? "" : "";
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var val)) continue;
                    DateTime when = end;
                    if (st.TryGetProperty("last_changed", out var lc) && lc.ValueKind == JsonValueKind.String
                        && DateTime.TryParse(lc.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                        when = dt;
                    pts.Add(new HaHistoryPoint(when, val));
                }
            }
        }
        catch { /* non-numeric series → empty */ }
        return pts.OrderBy(p => p.When).ToList();
    }

    // ── Camera ───────────────────────────────────────────────────────────────

    /// <summary>GET /api/camera_proxy/&lt;entity&gt; → current JPEG bytes saved to <paramref name="savePath"/>.</summary>
    public async Task<HaResult> CameraSnapshot(string cameraEntityId, string savePath, CancellationToken ct = default)
    {
        if (!IsConfigured) return HaResult.Fail("Not configured · 未設定");
        try
        {
            using var resp = await Http.SendAsync(Build(HttpMethod.Get, $"/api/camera_proxy/{cameraEntityId}"), ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return new HaResult(false, err, (int)resp.StatusCode);
            }
            var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            await File.WriteAllBytesAsync(savePath, bytes, ct).ConfigureAwait(false);
            return new HaResult(true, savePath, (int)resp.StatusCode);
        }
        catch (Exception ex) { return HaResult.Fail(ex.Message); }
    }

    // ── Scenes / scripts / services ──────────────────────────────────────────

    /// <summary>POST /api/services/scene/turn_on {"entity_id":"scene.x"}.</summary>
    public Task<HaResult> RunScene(string sceneEntityId, CancellationToken ct = default)
        => SendText(HttpMethod.Post, "/api/services/scene/turn_on", $"{{\"entity_id\":{JsonString(sceneEntityId)}}}", ct);

    /// <summary>POST /api/services/script/&lt;object_id&gt; — a script entity exposes itself as a service.</summary>
    public Task<HaResult> RunScript(string scriptEntityId, CancellationToken ct = default)
    {
        string obj = scriptEntityId.StartsWith("script.", StringComparison.Ordinal) ? scriptEntityId["script.".Length..] : scriptEntityId;
        return SendText(HttpMethod.Post, $"/api/services/script/{obj}", "{}", ct);
    }

    /// <summary>Generic POST /api/services/&lt;domain&gt;/&lt;service&gt; with a raw JSON data body.</summary>
    public Task<HaResult> CallService(string domain, string service, string jsonData, CancellationToken ct = default)
        => SendText(HttpMethod.Post, $"/api/services/{domain}/{service}",
            string.IsNullOrWhiteSpace(jsonData) ? "{}" : jsonData, ct);

    // ── Events ───────────────────────────────────────────────────────────────

    /// <summary>POST /api/events/&lt;event_type&gt; with an optional JSON event-data body.</summary>
    public Task<HaResult> FireEvent(string eventType, string? jsonData, CancellationToken ct = default)
        => SendText(HttpMethod.Post, $"/api/events/{eventType}",
            string.IsNullOrWhiteSpace(jsonData) ? null : jsonData, ct);

    // ── Calendars ────────────────────────────────────────────────────────────

    /// <summary>GET /api/calendars → list of calendar entities (entity_id + name).</summary>
    public async Task<List<HaEntity>> Calendars(CancellationToken ct = default)
    {
        var r = await SendText(HttpMethod.Get, "/api/calendars", null, ct).ConfigureAwait(false);
        var list = new List<HaEntity>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            foreach (var el in doc.RootElement.EnumerateArray())
                list.Add(new HaEntity
                {
                    EntityId = el.TryGetProperty("entity_id", out var id) ? id.GetString() ?? "" : "",
                    FriendlyName = el.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                });
        }
        catch { }
        return list;
    }

    /// <summary>GET /api/calendars/&lt;entity&gt;?start=..&amp;end=.. → events in a time window.</summary>
    public async Task<List<HaCalendarEvent>> CalendarEvents(string calendarEntityId, DateTime startLocal, DateTime endLocal, CancellationToken ct = default)
    {
        string s = Uri.EscapeDataString(startLocal.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        string e = Uri.EscapeDataString(endLocal.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        var r = await SendText(HttpMethod.Get, $"/api/calendars/{calendarEntityId}?start={s}&end={e}", null, ct).ConfigureAwait(false);
        var list = new List<HaCalendarEvent>();
        if (!r.Ok) return list;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            foreach (var el in doc.RootElement.EnumerateArray())
                list.Add(new HaCalendarEvent
                {
                    Summary = el.TryGetProperty("summary", out var su) ? su.GetString() ?? "" : "",
                    Start = ReadDateOrTime(el, "start"),
                    End = ReadDateOrTime(el, "end"),
                });
        }
        catch { }
        return list;
    }

    private static string ReadDateOrTime(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p)) return "";
        if (p.ValueKind == JsonValueKind.Object)
        {
            if (p.TryGetProperty("dateTime", out var dt)) return dt.GetString() ?? "";
            if (p.TryGetProperty("date", out var d)) return d.GetString() ?? "";
        }
        return p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : "";
    }

    // ── Error log ────────────────────────────────────────────────────────────

    /// <summary>GET /api/error_log → plaintext of the current session's home-assistant.log.</summary>
    public Task<HaResult> ErrorLog(CancellationToken ct = default) => SendText(HttpMethod.Get, "/api/error_log", null, ct);

    // ── Lights ───────────────────────────────────────────────────────────────

    /// <summary>POST /api/services/light/turn_on with brightness_pct + colour-temp (kelvin) or RGB.</summary>
    public Task<HaResult> SetLight(string entityId, int? brightnessPct, int? colorTempK, (int r, int g, int b)? rgb, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.Append("{\"entity_id\":").Append(JsonString(entityId));
        if (brightnessPct is int bp) sb.Append(",\"brightness_pct\":").Append(Math.Clamp(bp, 0, 100).ToString(CultureInfo.InvariantCulture));
        if (colorTempK is int ct2) sb.Append(",\"color_temp_kelvin\":").Append(Math.Clamp(ct2, 2000, 6500).ToString(CultureInfo.InvariantCulture));
        if (rgb is { } c) sb.Append(",\"rgb_color\":[").Append(c.r).Append(',').Append(c.g).Append(',').Append(c.b).Append(']');
        sb.Append('}');
        return SendText(HttpMethod.Post, "/api/services/light/turn_on", sb.ToString(), ct);
    }

    public Task<HaResult> LightOff(string entityId, CancellationToken ct = default)
        => SendText(HttpMethod.Post, "/api/services/light/turn_off", $"{{\"entity_id\":{JsonString(entityId)}}}", ct);

    // ── Climate / thermostat ─────────────────────────────────────────────────

    /// <summary>POST /api/services/climate/set_temperature {"entity_id":..,"temperature":n}.</summary>
    public Task<HaResult> SetThermostatTemp(string entityId, double temperature, CancellationToken ct = default)
        => SendText(HttpMethod.Post, "/api/services/climate/set_temperature",
            $"{{\"entity_id\":{JsonString(entityId)},\"temperature\":{temperature.ToString(CultureInfo.InvariantCulture)}}}", ct);

    /// <summary>POST /api/services/climate/set_hvac_mode {"entity_id":..,"hvac_mode":"heat"|...}.</summary>
    public Task<HaResult> SetHvacMode(string entityId, string mode, CancellationToken ct = default)
        => SendText(HttpMethod.Post, "/api/services/climate/set_hvac_mode",
            $"{{\"entity_id\":{JsonString(entityId)},\"hvac_mode\":{JsonString(mode)}}}", ct);

    public static readonly string[] HvacModes = { "off", "heat", "cool", "heat_cool", "auto", "dry", "fan_only" };

    // ── Notify ───────────────────────────────────────────────────────────────

    /// <summary>POST /api/services/notify/&lt;target&gt; {"title":..,"message":..}.</summary>
    public Task<HaResult> Notify(string target, string title, string message, CancellationToken ct = default)
    {
        string svc = target.StartsWith("notify.", StringComparison.Ordinal) ? target["notify.".Length..] : target;
        string body = string.IsNullOrWhiteSpace(title)
            ? $"{{\"message\":{JsonString(message)}}}"
            : $"{{\"title\":{JsonString(title)},\"message\":{JsonString(message)}}}";
        return SendText(HttpMethod.Post, $"/api/services/notify/{svc}", body, ct);
    }

    /// <summary>GET /api/services → notify-domain service names (notify.notify, notify.mobile_app_*…).</summary>
    public async Task<List<string>> NotifyTargets(CancellationToken ct = default)
    {
        var r = await SendText(HttpMethod.Get, "/api/services", null, ct).ConfigureAwait(false);
        var targets = new List<string>();
        if (!r.Ok) return targets;
        try
        {
            using var doc = JsonDocument.Parse(r.Body);
            foreach (var dom in doc.RootElement.EnumerateArray())
            {
                if (!dom.TryGetProperty("domain", out var d) || d.GetString() != "notify") continue;
                if (!dom.TryGetProperty("services", out var svcs) || svcs.ValueKind != JsonValueKind.Object) continue;
                foreach (var svc in svcs.EnumerateObject())
                    targets.Add($"notify.{svc.Name}");
            }
        }
        catch { }
        return targets.OrderBy(t => t, StringComparer.Ordinal).ToList();
    }

    // ── Intents ──────────────────────────────────────────────────────────────

    /// <summary>POST /api/intent/handle {"name":"&lt;Intent&gt;","data":{...}}.</summary>
    public Task<HaResult> HandleIntent(string name, string? jsonData, CancellationToken ct = default)
    {
        string data = string.IsNullOrWhiteSpace(jsonData) ? "{}" : jsonData;
        return SendText(HttpMethod.Post, "/api/intent/handle", $"{{\"name\":{JsonString(name)},\"data\":{data}}}", ct);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Validate that a string is well-formed JSON (used to gate raw-body inputs).</summary>
    public static bool IsValidJson(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        try { using var _ = JsonDocument.Parse(s); return true; } catch { return false; }
    }

    /// <summary>Try to pretty-print a JSON body; returns the original text if it is not JSON.</summary>
    public static string Pretty(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return body; }
    }

    private static string JsonString(string s) => JsonSerializer.Serialize(s);
}
