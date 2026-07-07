using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>HTTP 動詞 · The HTTP method for a request.</summary>
public enum ApiBodyMode
{
    None,            // 無內文 · no body
    RawJson,         // 原始 JSON · raw JSON (application/json)
    RawText,         // 原始文字 · raw text (text/plain)
    FormUrlEncoded,  // 表單 · application/x-www-form-urlencoded
}

/// <summary>驗證方式 · The auth helper kind.</summary>
public enum ApiAuthKind
{
    None,    // 無 · none
    Bearer,  // Bearer 權杖 · Bearer token
    Basic,   // 基本驗證 · Basic username/password
}

/// <summary>一對鍵值（可啟用／停用）· One key/value pair (header, query param, or form field).</summary>
public sealed class ApiKeyValue
{
    public bool Enabled { get; set; } = true;
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

/// <summary>
/// 一個可儲存嘅請求 · A saved/editable request: method, URL, params, headers, body and auth.
/// 純資料；序列化到磁碟 · pure data, serialised to disk inside a collection.
/// </summary>
public sealed class ApiRequest
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Request";
    public string Method { get; set; } = "GET";
    public string Url { get; set; } = "";
    public List<ApiKeyValue> QueryParams { get; set; } = new();
    public List<ApiKeyValue> Headers { get; set; } = new();
    public ApiBodyMode BodyMode { get; set; } = ApiBodyMode.None;
    public string Body { get; set; } = "";
    public List<ApiKeyValue> FormFields { get; set; } = new();
    public ApiAuthKind AuthKind { get; set; } = ApiAuthKind.None;
    public string AuthToken { get; set; } = "";   // bearer token
    public string AuthUser { get; set; } = "";    // basic username
    public string AuthPassword { get; set; } = ""; // basic password

    public ApiRequest Clone()
    {
        return new ApiRequest
        {
            Id = Id,
            Name = Name,
            Method = Method,
            Url = Url,
            QueryParams = QueryParams.Select(p => new ApiKeyValue { Enabled = p.Enabled, Key = p.Key, Value = p.Value }).ToList(),
            Headers = Headers.Select(p => new ApiKeyValue { Enabled = p.Enabled, Key = p.Key, Value = p.Value }).ToList(),
            BodyMode = BodyMode,
            Body = Body,
            FormFields = FormFields.Select(p => new ApiKeyValue { Enabled = p.Enabled, Key = p.Key, Value = p.Value }).ToList(),
            AuthKind = AuthKind,
            AuthToken = AuthToken,
            AuthUser = AuthUser,
            AuthPassword = AuthPassword,
        };
    }
}

/// <summary>一個命名集合（一組請求）· A named collection holding a list of requests.</summary>
public sealed class ApiCollection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Collection";
    public List<ApiRequest> Requests { get; set; } = new();
}

/// <summary>一個環境（一組變數）· An environment: a named set of {{var}} substitutions.</summary>
public sealed class ApiEnvironment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Environment";
    public List<ApiKeyValue> Variables { get; set; } = new();
}

/// <summary>磁碟上嘅整個工作區 · The whole persisted workspace (collections + environments).</summary>
public sealed class ApiWorkspace
{
    public List<ApiCollection> Collections { get; set; } = new();
    public List<ApiEnvironment> Environments { get; set; } = new();
    public string? ActiveEnvironmentId { get; set; }
}

/// <summary>一次回應嘅結果 · The outcome of a Send: status, timing, size, headers and body.</summary>
public sealed class ApiResponse
{
    public bool Ok { get; init; }                 // transport succeeded (got an HTTP response)
    public int StatusCode { get; init; }
    public string ReasonPhrase { get; init; } = "";
    public long ElapsedMs { get; init; }
    public long SizeBytes { get; init; }
    public string Body { get; init; } = "";
    public string ContentType { get; init; } = "";
    public List<ApiKeyValue> Headers { get; init; } = new();
    public string? Error { get; init; }           // set when the request never completed
    public string RequestLine { get; init; } = "";

    public bool IsSuccessRange => StatusCode is >= 200 and < 300;
    public bool LooksJson =>
        ContentType.Contains("json", StringComparison.OrdinalIgnoreCase)
        || (Body.TrimStart() is { Length: > 0 } t && (t[0] == '{' || t[0] == '['));
}

/// <summary>
/// 原生 REST API 用戶端服務 · Native REST API client built purely on HttpClient.
/// 處理 {{var}} 替換、查詢字串組合、驗證標頭、內文編碼，並執行請求同量度時間／大小。
/// Handles {{var}} substitution, query-string assembly, auth headers, body encoding, and runs the
/// request while measuring time and size. The workspace (collections + environments) is persisted to
/// %LOCALAPPDATA%\WinForge\apiclient.json. No external processes, no browser — pure managed C#.
/// </summary>
public sealed class ApiClientService
{
    private static readonly HttpClient Http = CreateClient();

    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");
    private static readonly string FilePath = Path.Combine(Dir, "apiclient.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ApiWorkspace Workspace { get; private set; } = new();

    public ApiClientService()
    {
        Load();
    }

    private static HttpClient CreateClient()
    {
        // Don't auto-follow redirects so the user sees 3xx; don't use cookies implicitly.
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
        };
        var c = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        return c;
    }

    // ── Persistence ───────────────────────────────────────────────────────────

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                Workspace = JsonSerializer.Deserialize<ApiWorkspace>(json, JsonOpts) ?? new();
            }
        }
        catch { Workspace = new(); }
        if (Workspace.Collections.Count == 0)
            SeedSample();
    }

    private void SeedSample()
    {
        // A friendly starter so the sidebar isn't empty on first run.
        var coll = new ApiCollection { Name = "Sample · 範例" };
        coll.Requests.Add(new ApiRequest
        {
            Name = "Get IP · 取得 IP",
            Method = "GET",
            Url = "https://httpbin.org/get",
        });
        coll.Requests.Add(new ApiRequest
        {
            Name = "Post JSON · 送出 JSON",
            Method = "POST",
            Url = "https://httpbin.org/post",
            BodyMode = ApiBodyMode.RawJson,
            Body = "{\n  \"hello\": \"world\"\n}",
        });
        Workspace.Collections.Add(coll);
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(Workspace, JsonOpts));
        }
        catch { /* best effort */ }
    }

    public string StorePath => FilePath;

    // ── Collections / requests CRUD ─────────────────────────────────────────────

    public ApiCollection AddCollection(string name)
    {
        var c = new ApiCollection { Name = string.IsNullOrWhiteSpace(name) ? "Collection" : name.Trim() };
        Workspace.Collections.Add(c);
        Save();
        return c;
    }

    public void RemoveCollection(ApiCollection c)
    {
        Workspace.Collections.Remove(c);
        Save();
    }

    public void RenameCollection(ApiCollection c, string name)
    {
        c.Name = string.IsNullOrWhiteSpace(name) ? c.Name : name.Trim();
        Save();
    }

    public ApiRequest SaveRequestInto(ApiCollection c, ApiRequest req)
    {
        var existing = c.Requests.FirstOrDefault(r => r.Id == req.Id);
        var copy = req.Clone();
        if (existing is not null)
        {
            int idx = c.Requests.IndexOf(existing);
            c.Requests[idx] = copy;
        }
        else c.Requests.Add(copy);
        Save();
        return copy;
    }

    public void RemoveRequest(ApiCollection c, ApiRequest req)
    {
        var existing = c.Requests.FirstOrDefault(r => r.Id == req.Id);
        if (existing is not null) c.Requests.Remove(existing);
        Save();
    }

    // ── Environments ────────────────────────────────────────────────────────────

    public ApiEnvironment AddEnvironment(string name)
    {
        var e = new ApiEnvironment { Name = string.IsNullOrWhiteSpace(name) ? "Environment" : name.Trim() };
        Workspace.Environments.Add(e);
        Save();
        return e;
    }

    public void RemoveEnvironment(ApiEnvironment e)
    {
        Workspace.Environments.Remove(e);
        if (Workspace.ActiveEnvironmentId == e.Id) Workspace.ActiveEnvironmentId = null;
        Save();
    }

    public ApiEnvironment? ActiveEnvironment =>
        Workspace.Environments.FirstOrDefault(e => e.Id == Workspace.ActiveEnvironmentId);

    public void SetActiveEnvironment(ApiEnvironment? e)
    {
        Workspace.ActiveEnvironmentId = e?.Id;
        Save();
    }

    /// <summary>替換 {{var}} · Substitute {{var}} tokens from the active environment.</summary>
    public string Substitute(string? input)
    {
        if (string.IsNullOrEmpty(input)) return input ?? "";
        var env = ActiveEnvironment;
        if (env is null) return input;
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var v in env.Variables)
            if (v.Enabled && !string.IsNullOrEmpty(v.Key)) map[v.Key] = v.Value ?? "";
        return Regex.Replace(input, @"\{\{\s*([^}\s]+)\s*\}\}", m =>
            map.TryGetValue(m.Groups[1].Value, out var val) ? val : m.Value);
    }

    // ── Send ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 執行請求 · Build and run the request through HttpClient, measuring time and size.
    /// Never throws — failures come back as an <see cref="ApiResponse"/> with <c>Error</c> set.
    /// </summary>
    public async Task<ApiResponse> SendAsync(ApiRequest req, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        string reqLine = "";
        try
        {
            // 1) URL + query string (after substitution).
            string url = Substitute(req.Url).Trim();
            if (url.Length == 0) return Err("Enter a URL first.", sw.ElapsedMilliseconds, "");
            if (!url.Contains("://")) url = "https://" + url;

            var query = req.QueryParams
                .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
                .Select(p => Uri.EscapeDataString(Substitute(p.Key)) + "=" + Uri.EscapeDataString(Substitute(p.Value)))
                .ToList();
            if (query.Count > 0)
                url += (url.Contains('?') ? "&" : "?") + string.Join("&", query);

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return Err($"Invalid URL: {url}", sw.ElapsedMilliseconds, "");

            var method = new HttpMethod((req.Method ?? "GET").Trim().ToUpperInvariant());
            reqLine = $"{method.Method} {uri}";
            using var msg = new HttpRequestMessage(method, uri);

            // 2) Body.
            string? bodyContentType = null;
            if (method.Method is not "GET" and not "HEAD")
            {
                switch (req.BodyMode)
                {
                    case ApiBodyMode.RawJson:
                        msg.Content = new StringContent(Substitute(req.Body), Encoding.UTF8);
                        bodyContentType = "application/json";
                        break;
                    case ApiBodyMode.RawText:
                        msg.Content = new StringContent(Substitute(req.Body), Encoding.UTF8);
                        bodyContentType = "text/plain";
                        break;
                    case ApiBodyMode.FormUrlEncoded:
                        var pairs = req.FormFields
                            .Where(p => p.Enabled && !string.IsNullOrWhiteSpace(p.Key))
                            .Select(p => new KeyValuePair<string, string>(Substitute(p.Key), Substitute(p.Value)))
                            .ToList();
                        msg.Content = new FormUrlEncodedContent(pairs);
                        bodyContentType = "application/x-www-form-urlencoded";
                        break;
                }
            }

            // 3) Headers (after substitution). Content-Type overrides the body default.
            foreach (var h in req.Headers.Where(h => h.Enabled && !string.IsNullOrWhiteSpace(h.Key)))
            {
                string k = Substitute(h.Key).Trim();
                string v = Substitute(h.Value);
                if (string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    if (msg.Content is not null)
                    {
                        try { msg.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(v); bodyContentType = null; }
                        catch { }
                    }
                    continue;
                }
                if (!msg.Headers.TryAddWithoutValidation(k, v))
                    msg.Content?.Headers.TryAddWithoutValidation(k, v);
            }
            if (bodyContentType is not null && msg.Content is not null)
            {
                try { msg.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(bodyContentType + "; charset=utf-8"); }
                catch { msg.Content.Headers.TryAddWithoutValidation("Content-Type", bodyContentType); }
            }

            // 4) Auth.
            switch (req.AuthKind)
            {
                case ApiAuthKind.Bearer:
                    var token = Substitute(req.AuthToken).Trim();
                    if (token.Length > 0) msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    break;
                case ApiAuthKind.Basic:
                    var raw = Substitute(req.AuthUser) + ":" + Substitute(req.AuthPassword);
                    var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                    msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", b64);
                    break;
            }

            // 5) Send with a per-request timeout so the UI never hangs.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(100));

            using var resp = await Http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                .ConfigureAwait(false);

            var bytes = await resp.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);
            sw.Stop();

            string body = TryDecode(bytes, resp);
            var headers = new List<ApiKeyValue>();
            foreach (var h in resp.Headers)
                headers.Add(new ApiKeyValue { Key = h.Key, Value = string.Join(", ", h.Value) });
            foreach (var h in resp.Content.Headers)
                headers.Add(new ApiKeyValue { Key = h.Key, Value = string.Join(", ", h.Value) });

            return new ApiResponse
            {
                Ok = true,
                StatusCode = (int)resp.StatusCode,
                ReasonPhrase = resp.ReasonPhrase ?? "",
                ElapsedMs = sw.ElapsedMilliseconds,
                SizeBytes = bytes.LongLength,
                Body = body,
                ContentType = resp.Content.Headers.ContentType?.ToString() ?? "",
                Headers = headers,
                RequestLine = reqLine,
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            sw.Stop();
            return Err("Cancelled.", sw.ElapsedMilliseconds, reqLine);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return Err("Request timed out (100s).", sw.ElapsedMilliseconds, reqLine);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var detail = ex.InnerException?.Message is { Length: > 0 } inner ? $"{ex.Message} — {inner}" : ex.Message;
            return Err(detail, sw.ElapsedMilliseconds, reqLine);
        }
    }

    private static ApiResponse Err(string message, long ms, string reqLine)
        => new() { Ok = false, Error = message, ElapsedMs = ms, RequestLine = reqLine };

    private static string TryDecode(byte[] bytes, HttpResponseMessage resp)
    {
        if (bytes.Length == 0) return "";
        try
        {
            var enc = Encoding.UTF8;
            var cs = resp.Content.Headers.ContentType?.CharSet;
            if (!string.IsNullOrWhiteSpace(cs))
            {
                try { enc = Encoding.GetEncoding(cs.Trim('"')); } catch { enc = Encoding.UTF8; }
            }
            return enc.GetString(bytes);
        }
        catch { return Encoding.UTF8.GetString(bytes); }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>美化 JSON · Pretty-print JSON; returns null if the text isn't valid JSON.</summary>
    public static string? PrettyJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            using var doc = JsonDocument.Parse(text);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { return null; }
    }

    /// <summary>友善位元組大小 · Friendly byte size (e.g. 12.3 KB).</summary>
    public static string HumanSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{(long)v} {units[i]}" : $"{v:0.#} {units[i]}";
    }

    public static readonly string[] Methods = { "GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS" };
}
