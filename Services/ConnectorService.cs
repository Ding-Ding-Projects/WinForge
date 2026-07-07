using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>連接器種類 · Kind of external connector.</summary>
public enum ConnectorKind { McpServer, RestApi, Webhook, Database, Custom }

/// <summary>連接器驗證方式 · How the connector authenticates.</summary>
public enum ConnectorAuth { None, Bearer, ApiKeyHeader, Basic }

/// <summary>
/// 一個連接器：app 連去外部服務嘅具名整合 · One connector — a named integration WinForge can use to
/// reach an external service (MCP server, REST API, webhook, database, …). The secret is DPAPI-protected
/// and never serialized in clear / logged. Other modules (e.g. AI) can read <see cref="ConnectorService.Enabled"/>.
/// </summary>
public sealed class Connector
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public ConnectorKind Kind { get; set; } = ConnectorKind.RestApi;
    public ConnectorAuth Auth { get; set; } = ConnectorAuth.None;
    public string Endpoint { get; set; } = "";        // URL, or a command for MCP-stdio
    public string AuthHeaderName { get; set; } = "X-API-Key"; // for ApiKeyHeader
    public string Username { get; set; } = "";          // for Basic
    public string Headers { get; set; } = "";           // extra headers, "Key: Value" per line
    public string Notes { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string SecretBlob { get; set; } = "";        // DPAPI-protected token/key/password
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");

    [JsonIgnore] public string KindLabel => Kind switch
    {
        ConnectorKind.McpServer => "MCP server",
        ConnectorKind.RestApi => "REST API",
        ConnectorKind.Webhook => "Webhook",
        ConnectorKind.Database => "Database",
        _ => "Custom",
    };

    [JsonIgnore] public string Summary => $"{KindLabel}  ·  {Endpoint}";
}

/// <summary>
/// 連接器引擎 · Connector engine — persists user-defined external integrations and tests reachability.
/// Pure managed C#; secrets via DPAPI (never plaintext/logs). Used so WinForge modules can connect to
/// external services without each module reinventing credential storage.
/// </summary>
public static class ConnectorService
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");
    private static readonly string StoreFile = Path.Combine(AppDir, "connectors.json");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.Connectors.v1");
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public static event EventHandler? Changed;

    // ───────────────────────── persistence ─────────────────────────

    public static List<Connector> Load()
    {
        try
        {
            if (!File.Exists(StoreFile)) return new();
            return JsonSerializer.Deserialize<List<Connector>>(File.ReadAllText(StoreFile)) ?? new();
        }
        catch { return new(); }
    }

    private static void Save(List<Connector> list)
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllText(StoreFile, JsonSerializer.Serialize(list, JsonOpts));
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>啟用咗嘅連接器（畀其他模組用）· Enabled connectors, for other modules to consume.</summary>
    public static List<Connector> Enabled() => Load().Where(c => c.Enabled).ToList();

    /// <summary>新增／更新一個連接器（連加密密鑰）· Upsert a connector, encrypting the secret if provided.</summary>
    public static Connector SaveConnector(Connector c, string? secret)
    {
        var list = Load();
        if (secret is not null) c.SecretBlob = string.IsNullOrEmpty(secret) ? "" : Protect(secret);
        var idx = list.FindIndex(x => x.Id == c.Id);
        if (idx >= 0) list[idx] = c; else list.Add(c);
        Save(list);
        return c;
    }

    public static void Delete(string id) { var l = Load(); l.RemoveAll(x => x.Id == id); Save(l); }
    public static string GetSecret(Connector c) => string.IsNullOrEmpty(c.SecretBlob) ? "" : Unprotect(c.SecretBlob);

    private static string Protect(string plain)
    {
        try { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }
    private static string Unprotect(string blob)
    {
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(blob), Entropy, DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }

    // ───────────────────────── test reachability ─────────────────────────

    /// <summary>試連線 · Test a connector — best-effort reachability + auth header wiring.</summary>
    public static async Task<TweakResult> TestAsync(Connector c, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(c.Endpoint))
            return TweakResult.Fail("No endpoint set.", "未設定端點。");

        // Non-HTTP kinds: validate shape only (we don't spawn processes here).
        bool isHttp = c.Endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                   || c.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (c.Kind == ConnectorKind.McpServer && !isHttp)
            return TweakResult.Ok($"MCP (stdio) command saved: {c.Endpoint}", $"已儲存 MCP（stdio）指令：{c.Endpoint}",
                "Stdio MCP servers are launched on demand by the consuming feature; no network test performed.");
        if (c.Kind == ConnectorKind.Database)
            return TweakResult.Ok("Database connector saved (connection string stored).", "已儲存資料庫連接器（連接字串已儲存）。",
                "Connection is validated by the module that uses it (e.g. Postgres / SQLite tools).");
        if (!isHttp)
            return TweakResult.Ok("Connector saved.", "已儲存連接器。", "No HTTP endpoint to test.");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, c.Endpoint);
            ApplyAuth(c, req);
            ApplyHeaders(c, req);
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            int code = (int)resp.StatusCode;
            bool ok = code < 500; // reachable even if 401/404 — the host answered
            return ok
                ? TweakResult.Ok($"Reachable — HTTP {code} {resp.ReasonPhrase}.", $"可連線 — HTTP {code}。", $"{c.Endpoint} → {code}")
                : TweakResult.Fail($"Server error — HTTP {code}.", $"伺服器錯誤 — HTTP {code}。", $"{c.Endpoint} → {code}");
        }
        catch (TaskCanceledException) { return TweakResult.Fail("Timed out reaching the endpoint.", "連線端點逾時。"); }
        catch (Exception ex) { return TweakResult.Fail($"Could not reach the endpoint: {ex.Message}", $"連唔到端點：{ex.Message}"); }
    }

    private static void ApplyAuth(Connector c, HttpRequestMessage req)
    {
        var secret = GetSecret(c);
        switch (c.Auth)
        {
            case ConnectorAuth.Bearer when !string.IsNullOrEmpty(secret):
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + secret); break;
            case ConnectorAuth.ApiKeyHeader when !string.IsNullOrEmpty(secret):
                req.Headers.TryAddWithoutValidation(string.IsNullOrWhiteSpace(c.AuthHeaderName) ? "X-API-Key" : c.AuthHeaderName, secret); break;
            case ConnectorAuth.Basic when !string.IsNullOrEmpty(c.Username):
                var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.Username}:{secret}"));
                req.Headers.TryAddWithoutValidation("Authorization", "Basic " + token); break;
        }
    }

    private static void ApplyHeaders(Connector c, HttpRequestMessage req)
    {
        if (string.IsNullOrWhiteSpace(c.Headers)) return;
        foreach (var line in c.Headers.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            req.Headers.TryAddWithoutValidation(line[..colon].Trim(), line[(colon + 1)..].Trim());
        }
    }
}
