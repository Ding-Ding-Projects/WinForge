using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>一隻已安裝模型 · One locally-installed Ollama model (from GET /api/tags).</summary>
public sealed class OllamaModel
{
    public string Name { get; init; } = "";
    public long Size { get; init; }
    public string Family { get; init; } = "";
    public string ParameterSize { get; init; } = "";
    public string Quantization { get; init; } = "";
    public DateTimeOffset? Modified { get; init; }

    public string SizeText => OllamaService.HumanSize(Size);
    public string ModifiedText => Modified is { } m ? m.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";
    /// <summary>"7.6B · Q4_K_M · llama" — a compact detail line.</summary>
    public string Detail
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ParameterSize)) parts.Add(ParameterSize);
            if (!string.IsNullOrWhiteSpace(Quantization)) parts.Add(Quantization);
            if (!string.IsNullOrWhiteSpace(Family)) parts.Add(Family);
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>一隻載入記憶體嘅模型 · One model currently loaded in memory (from GET /api/ps).</summary>
public sealed class OllamaRunningModel
{
    public string Name { get; init; } = "";
    public long Size { get; init; }
    public long SizeVram { get; init; }
    public string ParameterSize { get; init; } = "";
    public string Quantization { get; init; } = "";
    public DateTimeOffset? ExpiresAt { get; init; }

    public string SizeText => OllamaService.HumanSize(Size);
    public string VramText => SizeVram > 0 ? OllamaService.HumanSize(SizeVram) : "—";
    public string Processor
    {
        get
        {
            if (Size <= 0) return "";
            if (SizeVram <= 0) return "100% CPU";
            if (SizeVram >= Size) return "100% GPU";
            int gpu = (int)Math.Round(100.0 * SizeVram / Size);
            return $"{gpu}% GPU / {100 - gpu}% CPU";
        }
    }
    public string Detail
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(ParameterSize)) parts.Add(ParameterSize);
            if (!string.IsNullOrWhiteSpace(Quantization)) parts.Add(Quantization);
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>一次 pull 進度更新 · One progress update streamed from POST /api/pull.</summary>
public readonly record struct OllamaPullProgress(string Status, long Completed, long Total, bool Done, bool Failed, string? Error)
{
    public double Fraction => Total > 0 ? Math.Clamp((double)Completed / Total, 0, 1) : 0;
    public bool HasBytes => Total > 0;
}

/// <summary>一個聊天訊息 · One chat message kept in conversation history.</summary>
public sealed class OllamaChatMessage
{
    public string Role { get; init; } = "user"; // system | user | assistant
    public string Content { get; set; } = "";
}

/// <summary>聊天請求參數 · The tunable options sent in the chat `options` object.</summary>
public sealed class OllamaChatOptions
{
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? TopK { get; set; }
    public int? NumCtx { get; set; }
    public int? Seed { get; set; }
}

/// <summary>聊天串流嘅一嚿 · One streamed chunk from POST /api/chat.</summary>
public readonly record struct OllamaChatChunk(string Content, bool Done, bool Failed, string? Error);

/// <summary>
/// 本機 Ollama 嘅 REST 客戶端 · Typed HttpClient wrapper over the local Ollama REST API
/// (http://localhost:11434 by default). Covers version probe, list/running models, streaming pull,
/// delete and streaming chat. The base URL persists via SettingsStore. Methods never throw on network
/// errors — they return empty/false results so the UI stays alive when Ollama is absent or offline.
/// Endpoints verified against the upstream docs/api.md.
/// </summary>
public sealed class OllamaService
{
    public const string KeyBaseUrl = "ollama.baseUrl";
    public const string WingetId = "Ollama.Ollama";
    public const string DefaultBaseUrl = "http://localhost:11434";

    // A no-timeout client for long streaming operations (pull / chat); a short one for probes.
    private static readonly HttpClient Stream = new() { Timeout = Timeout.InfiniteTimeSpan };
    private static readonly HttpClient Quick = new() { Timeout = TimeSpan.FromSeconds(8) };

    public string BaseUrl { get; private set; }

    public OllamaService()
    {
        BaseUrl = Normalize(SettingsStore.Get(KeyBaseUrl, DefaultBaseUrl));
    }

    private static string Normalize(string url)
    {
        url = (url ?? "").Trim().TrimEnd('/');
        return string.IsNullOrWhiteSpace(url) ? DefaultBaseUrl : url;
    }

    /// <summary>持久化 base URL · Persist the (normalised) base URL.</summary>
    public void SaveBaseUrl(string url)
    {
        BaseUrl = Normalize(url);
        SettingsStore.Set(KeyBaseUrl, BaseUrl);
    }

    // ── Version / reachability ────────────────────────────────────────────────

    /// <summary>GET /api/version → version string, or null when not reachable.</summary>
    public async Task<string?> GetVersionAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Quick.GetAsync(BaseUrl + "/api/version", ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("version", out var v) ? v.GetString() : null;
        }
        catch { return null; }
    }

    public async Task<bool> IsReachableAsync(CancellationToken ct = default)
        => await GetVersionAsync(ct).ConfigureAwait(false) is not null;

    // ── Installed models (GET /api/tags) ──────────────────────────────────────

    public async Task<List<OllamaModel>> ListModelsAsync(CancellationToken ct = default)
    {
        var list = new List<OllamaModel>();
        try
        {
            using var resp = await Quick.GetAsync(BaseUrl + "/api/tags", ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return list;
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var m in models.EnumerateArray())
            {
                string name = Str(m, "name");
                if (name.Length == 0) name = Str(m, "model");
                if (name.Length == 0) continue;
                DateTimeOffset? mod = null;
                if (m.TryGetProperty("modified_at", out var ma) && ma.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(ma.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    mod = dt;
                string fam = "", psize = "", quant = "";
                if (m.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Object)
                {
                    fam = Str(d, "family");
                    psize = Str(d, "parameter_size");
                    quant = Str(d, "quantization_level");
                }
                list.Add(new OllamaModel
                {
                    Name = name,
                    Size = Num(m, "size"),
                    Family = fam,
                    ParameterSize = psize,
                    Quantization = quant,
                    Modified = mod,
                });
            }
        }
        catch { }
        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    // ── Running models (GET /api/ps) ──────────────────────────────────────────

    public async Task<List<OllamaRunningModel>> ListRunningAsync(CancellationToken ct = default)
    {
        var list = new List<OllamaRunningModel>();
        try
        {
            using var resp = await Quick.GetAsync(BaseUrl + "/api/ps", ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return list;
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
                return list;
            foreach (var m in models.EnumerateArray())
            {
                string name = Str(m, "name");
                if (name.Length == 0) name = Str(m, "model");
                if (name.Length == 0) continue;
                DateTimeOffset? exp = null;
                if (m.TryGetProperty("expires_at", out var ea) && ea.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(ea.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    exp = dt;
                string psize = "", quant = "";
                if (m.TryGetProperty("details", out var d) && d.ValueKind == JsonValueKind.Object)
                {
                    psize = Str(d, "parameter_size");
                    quant = Str(d, "quantization_level");
                }
                list.Add(new OllamaRunningModel
                {
                    Name = name,
                    Size = Num(m, "size"),
                    SizeVram = Num(m, "size_vram"),
                    ParameterSize = psize,
                    Quantization = quant,
                    ExpiresAt = exp,
                });
            }
        }
        catch { }
        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    // ── Pull (POST /api/pull, streamed) ───────────────────────────────────────

    /// <summary>
    /// POST /api/pull with stream:true — yields a progress update per newline-delimited JSON object.
    /// Stops on {"status":"success"} (Done=true) or on a transport error (Failed=true).
    /// </summary>
    public async IAsyncEnumerable<OllamaPullProgress> PullModelAsync(string model,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var payload = $"{{\"model\":{JsonStr(model)},\"stream\":true}}";
        HttpResponseMessage? resp = null;
        Stream? body = null;
        StreamReader? reader = null;
        string? transportError = null;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/api/pull")
            { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            resp = await Stream.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                transportError = $"HTTP {(int)resp.StatusCode}";
            else
            {
                body = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                reader = new StreamReader(body, Encoding.UTF8);
            }
        }
        catch (OperationCanceledException) { transportError = "cancelled"; }
        catch (Exception ex) { transportError = ex.Message; }

        if (transportError is not null)
        {
            resp?.Dispose();
            yield return new OllamaPullProgress("error", 0, 0, false, true, transportError);
            yield break;
        }

        try
        {
            while (true)
            {
                string? line; string? readError = null; bool cancelled = false;
                try { line = await reader!.ReadLineAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { line = null; cancelled = true; }
                catch (Exception ex) { line = null; readError = ex.Message; }
                if (cancelled) { yield return new OllamaPullProgress("cancelled", 0, 0, false, true, "cancelled"); yield break; }
                if (readError is not null) { yield return new OllamaPullProgress("error", 0, 0, false, true, readError); yield break; }
                if (line is null) break;
                if (line.Length == 0) continue;

                OllamaPullProgress prog;
                bool ok = true;
                string status = "", err = "";
                long completed = 0, total = 0;
                bool done = false, failed = false;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    status = root.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
                    completed = Num(root, "completed");
                    total = Num(root, "total");
                    if (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                    { failed = true; err = e.GetString() ?? "error"; }
                    if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)) done = true;
                }
                catch { ok = false; }
                if (!ok) continue;
                prog = new OllamaPullProgress(status, completed, total, done, failed, failed ? err : null);
                yield return prog;
                if (done || failed) yield break;
            }
        }
        finally
        {
            reader?.Dispose();
            body?.Dispose();
            resp?.Dispose();
        }
    }

    // ── Delete (DELETE /api/delete) ───────────────────────────────────────────

    /// <summary>DELETE /api/delete {"model":...} → (ok, message). Destructive — confirm first.</summary>
    public async Task<(bool ok, string message)> DeleteModelAsync(string model, CancellationToken ct = default)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Delete, BaseUrl + "/api/delete")
            { Content = new StringContent($"{{\"model\":{JsonStr(model)}}}", Encoding.UTF8, "application/json") };
            using var resp = await Quick.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode) return (true, "ok");
            return (false, $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    // ── Chat (POST /api/chat, streamed) ───────────────────────────────────────

    /// <summary>
    /// POST /api/chat with stream:true — yields token chunks parsed from newline-delimited JSON.
    /// Stops on {"done":true}. The cancellation token cancels the underlying request (Stop button).
    /// </summary>
    public async IAsyncEnumerable<OllamaChatChunk> ChatStreamAsync(string model,
        IReadOnlyList<OllamaChatMessage> history, OllamaChatOptions? options,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        sb.Append("{\"model\":").Append(JsonStr(model)).Append(",\"stream\":true,\"messages\":[");
        for (int i = 0; i < history.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"role\":").Append(JsonStr(history[i].Role))
              .Append(",\"content\":").Append(JsonStr(history[i].Content)).Append('}');
        }
        sb.Append(']');
        if (options is not null)
        {
            var opts = new StringBuilder();
            void Add(string k, string v) { if (opts.Length > 0) opts.Append(','); opts.Append('"').Append(k).Append("\":").Append(v); }
            if (options.Temperature is { } t) Add("temperature", t.ToString(CultureInfo.InvariantCulture));
            if (options.TopP is { } tp) Add("top_p", tp.ToString(CultureInfo.InvariantCulture));
            if (options.TopK is { } tk) Add("top_k", tk.ToString(CultureInfo.InvariantCulture));
            if (options.NumCtx is { } nc) Add("num_ctx", nc.ToString(CultureInfo.InvariantCulture));
            if (options.Seed is { } sd) Add("seed", sd.ToString(CultureInfo.InvariantCulture));
            if (opts.Length > 0) sb.Append(",\"options\":{").Append(opts).Append('}');
        }
        sb.Append('}');
        var payload = sb.ToString();

        HttpResponseMessage? resp = null;
        Stream? body = null;
        StreamReader? reader = null;
        string? transportError = null;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/api/chat")
            { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            resp = await Stream.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                string detail = "";
                try { detail = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
                transportError = $"HTTP {(int)resp.StatusCode} {detail}".Trim();
            }
            else
            {
                body = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                reader = new StreamReader(body, Encoding.UTF8);
            }
        }
        catch (OperationCanceledException) { transportError = null; resp?.Dispose(); yield break; }
        catch (Exception ex) { transportError = ex.Message; }

        if (transportError is not null)
        {
            resp?.Dispose();
            yield return new OllamaChatChunk("", false, true, transportError);
            yield break;
        }

        try
        {
            while (true)
            {
                string? line; string? readError = null; bool cancelled = false;
                try { line = await reader!.ReadLineAsync(ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { line = null; cancelled = true; }
                catch (Exception ex) { line = null; readError = ex.Message; }
                if (cancelled) yield break;
                if (readError is not null) { yield return new OllamaChatChunk("", false, true, readError); yield break; }
                if (line is null) break;
                if (line.Length == 0) continue;

                string content = ""; bool done = false; bool failed = false; string? err = null;
                bool ok = true;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                    { failed = true; err = e.GetString(); }
                    if (root.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Object
                        && msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                        content = c.GetString() ?? "";
                    if (root.TryGetProperty("done", out var dn) && dn.ValueKind == JsonValueKind.True) done = true;
                }
                catch { ok = false; }
                if (!ok) continue;
                if (failed) { yield return new OllamaChatChunk("", false, true, err); yield break; }
                if (content.Length > 0) yield return new OllamaChatChunk(content, false, false, null);
                if (done) { yield return new OllamaChatChunk("", true, false, null); yield break; }
            }
        }
        finally
        {
            reader?.Dispose();
            body?.Dispose();
            resp?.Dispose();
        }
    }

    // ── Lifecycle (ollama serve) ──────────────────────────────────────────────

    /// <summary>
    /// 啟動 `ollama serve` · Launch `ollama serve` detached so the local API comes up.
    /// Returns true if the process was started (not whether it became reachable).
    /// </summary>
    public static bool StartServe()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ollama",
                Arguments = "serve",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            var p = System.Diagnostics.Process.Start(psi);
            return p is not null;
        }
        catch { return false; }
    }

    /// <summary>~/.ollama/models — the default model store folder (may not yet exist).</summary>
    public static string ModelsFolder
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("OLLAMA_MODELS");
            if (!string.IsNullOrWhiteSpace(env)) return env;
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".ollama", "models");
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string Str(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static long Num(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : 0;

    private static string JsonStr(string s) => JsonSerializer.Serialize(s);

    /// <summary>1.2 GB / 540 MB — a friendly byte size.</summary>
    public static string HumanSize(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{(long)v} {units[i]}" : $"{v:0.#} {units[i]}";
    }
}
