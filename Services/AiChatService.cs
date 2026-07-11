using System;
using System.Collections.Generic;
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

/// <summary>
/// 原生「OpenWebUI 式」聊天後端 · Native OpenWebUI-style chat back-end for WinForge.
///
/// 三類供應商 · Three provider kinds:
///   • Ollama —— 本機 REST（/api/tags、/api/chat 串流、/api/pull 串流、/api/delete）。
///   • OpenAI 相容 —— 任何 /v1/chat/completions（OpenAI、OpenRouter、LM Studio、llama.cpp…），SSE 串流。
///   • CLI —— 透過 AiAgentService 已安裝嘅終端機代理做一次性提問。
///
/// 供應商設定（含 DPAPI 加密金鑰）同所有對話都持久化到 %LOCALAPPDATA%\WinForge\。
/// Provider configs (keys DPAPI-encrypted) and every conversation persist under %LOCALAPPDATA%\WinForge\.
/// 全防禦性：網絡或檔案出錯都唔會擲到 UI；用 TweakResult / 回傳值表達失敗。
/// </summary>
public sealed class AiChatService
{
    private static readonly string Root =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");
    private static readonly string ChatDir = Path.Combine(Root, "chats");
    private static readonly string ProvidersFile = Path.Combine(Root, "ai-providers.json");

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // Must follow Root/ProvidersFile initialization: the singleton loads persisted
    // providers in its constructor.
    public static AiChatService I { get; } = new();

    private readonly object _gate = new();
    private readonly AiProviderPersistence _providerPersistence;
    private List<AiProvider> _providers;

    private AiChatService()
    {
        _providerPersistence = new AiProviderPersistence(ProvidersFile, new DpapiAiProviderSecretProtector());
        _providers = _providerPersistence.Load();
        if (_providers.Count == 0)
        {
            // 預設加入本機 Ollama · Seed a default local Ollama provider.
            _providers.Add(new AiProvider
            {
                Name = "Ollama (local · 本機)",
                Kind = AiProviderKind.Ollama,
                BaseUrl = "http://localhost:11434",
            });
            SaveProviders();
        }
    }

    // ===================== Providers =====================

    public IReadOnlyList<AiProvider> Providers
    {
        get { lock (_gate) return _providers.Select(CloneProvider).ToList(); }
    }

    public AiProvider? GetProvider(string id)
    {
        lock (_gate)
        {
            var provider = _providers.FirstOrDefault(p => p.Id == id);
            return provider is null ? null : CloneProvider(provider);
        }
    }

    /// <summary>
    /// Saves a provider only when all non-empty API keys can be DPAPI-protected.
    /// A <c>false</c> result leaves both disk and the service's provider snapshot unchanged.
    /// </summary>
    public bool UpsertProvider(AiProvider p)
    {
        if (p is null) return false;
        lock (_gate)
        {
            var next = _providers.Select(CloneProvider).ToList();
            var candidate = CloneProvider(p);
            var idx = next.FindIndex(x => x.Id == candidate.Id);
            if (idx >= 0) next[idx] = candidate; else next.Add(candidate);
            if (!SaveProviders(next)) return false;
            _providers = next;
            return true;
        }
    }

    public bool DeleteProvider(string id)
    {
        lock (_gate)
        {
            var next = _providers.Where(p => p.Id != id).Select(CloneProvider).ToList();
            if (!SaveProviders(next)) return false;
            _providers = next;
            return true;
        }
    }

    /// <summary>Whether a provider's DPAPI-encrypted key could not be read in this user context.</summary>
    public bool HasUnreadableProviderSecret(string id)
    {
        lock (_gate) return _providerPersistence.HasUnreadableSecret(id);
    }

    private bool SaveProviders(IEnumerable<AiProvider> providers)
    {
        return _providerPersistence.TrySave(providers);
    }

    private void SaveProviders()
    {
        _ = SaveProviders(_providers);
    }

    private static AiProvider CloneProvider(AiProvider provider)
    {
        return new AiProvider
        {
            Id = provider.Id,
            Name = provider.Name,
            Kind = provider.Kind,
            BaseUrl = provider.BaseUrl,
            ApiKey = provider.ApiKey,
            DefaultModel = provider.DefaultModel,
        };
    }

    // ===================== Conversations =====================

    /// <summary>列出所有對話（最新喺前）· List all conversations, newest first.</summary>
    public List<ChatConversation> LoadConversations()
    {
        var list = new List<ChatConversation>();
        try
        {
            Directory.CreateDirectory(ChatDir);
            foreach (var f in Directory.EnumerateFiles(ChatDir, "*.json"))
            {
                try
                {
                    var c = JsonSerializer.Deserialize<ChatConversation>(File.ReadAllText(f));
                    if (c is not null) list.Add(c);
                }
                catch { }
            }
        }
        catch { }
        return list.OrderByDescending(c => c.UpdatedTicks).ToList();
    }

    public void SaveConversation(ChatConversation c)
    {
        if (c is null) return;
        try
        {
            Directory.CreateDirectory(ChatDir);
            c.UpdatedTicks = DateTime.UtcNow.Ticks;
            File.WriteAllText(Path.Combine(ChatDir, c.Id + ".json"), JsonSerializer.Serialize(c, JsonOpts));
        }
        catch { }
    }

    public void DeleteConversation(string id)
    {
        try
        {
            var f = Path.Combine(ChatDir, id + ".json");
            if (File.Exists(f)) File.Delete(f);
        }
        catch { }
    }

    // ===================== Ollama management =====================

    /// <summary>Ollama 服務喺唔喺度（GET /api/tags 通到就當有）· Is the Ollama server reachable?</summary>
    public async Task<bool> OllamaRunningAsync(string baseUrl, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(4));
            using var r = await Http.GetAsync(Norm(baseUrl) + "/api/tags", cts.Token);
            return r.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    /// <summary>列出本機 Ollama 模型（GET /api/tags）· List installed Ollama models.</summary>
    public async Task<List<OllamaModelInfo>> ListOllamaModelsAsync(string baseUrl, CancellationToken ct = default)
    {
        var result = new List<OllamaModelInfo>();
        try
        {
            using var r = await Http.GetAsync(Norm(baseUrl) + "/api/tags", ct);
            if (!r.IsSuccessStatusCode) return result;
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
            if (doc.RootElement.TryGetProperty("models", out var models))
            {
                foreach (var m in models.EnumerateArray())
                {
                    var om = new OllamaModelInfo
                    {
                        Name = m.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Size = m.TryGetProperty("size", out var s) && s.TryGetInt64(out var sv) ? sv : 0,
                    };
                    if (m.TryGetProperty("details", out var d))
                    {
                        if (d.TryGetProperty("parameter_size", out var ps)) om.ParameterSize = ps.GetString() ?? "";
                        if (d.TryGetProperty("quantization_level", out var q)) om.Quantization = q.GetString() ?? "";
                    }
                    if (!string.IsNullOrWhiteSpace(om.Name)) result.Add(om);
                }
            }
        }
        catch { }
        return result;
    }

    /// <summary>
    /// 拉取一個模型（POST /api/pull 串流）· Pull a model, reporting (status, completed, total) progress.
    /// 回傳成功與否 · Returns success.
    /// </summary>
    public async Task<bool> PullOllamaModelAsync(string baseUrl, string model,
        Action<string, long, long> onProgress, CancellationToken ct = default)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { model, stream = true });
            using var req = new HttpRequestMessage(HttpMethod.Post, Norm(baseUrl) + "/api/pull")
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return false;
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            bool success = false;
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
                    long completed = root.TryGetProperty("completed", out var c) && c.TryGetInt64(out var cv) ? cv : 0;
                    long total = root.TryGetProperty("total", out var t) && t.TryGetInt64(out var tv) ? tv : 0;
                    onProgress(status, completed, total);
                    if (status.Equals("success", StringComparison.OrdinalIgnoreCase)) success = true;
                }
                catch { }
            }
            return success;
        }
        catch { return false; }
    }

    /// <summary>刪除一個模型（DELETE /api/delete）· Delete an installed Ollama model.</summary>
    public async Task<bool> DeleteOllamaModelAsync(string baseUrl, string model, CancellationToken ct = default)
    {
        try
        {
            var body = JsonSerializer.Serialize(new { model });
            using var req = new HttpRequestMessage(HttpMethod.Delete, Norm(baseUrl) + "/api/delete")
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            using var resp = await Http.SendAsync(req, ct);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ===================== Model listing (any provider) =====================

    /// <summary>列出某 provider 嘅可用模型 id · List model ids available from a provider.</summary>
    public async Task<List<string>> ListModelsAsync(AiProvider p, CancellationToken ct = default)
    {
        if (p is null) return new();
        if (p.Kind == AiProviderKind.Ollama)
            return (await ListOllamaModelsAsync(p.BaseUrl, ct)).Select(m => m.Name).ToList();

        if (p.Kind == AiProviderKind.OpenAiCompatible)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, Norm(p.BaseUrl) + "/v1/models");
                if (!string.IsNullOrEmpty(p.ApiKey))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", p.ApiKey);
                using var resp = await Http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) return new();
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                var ids = new List<string>();
                if (doc.RootElement.TryGetProperty("data", out var data))
                    foreach (var m in data.EnumerateArray())
                        if (m.TryGetProperty("id", out var id) && id.GetString() is { } s) ids.Add(s);
                return ids.OrderBy(x => x).ToList();
            }
            catch { return new(); }
        }

        // CLI providers expose the agent keys themselves.
        if (p.Kind == AiProviderKind.Cli)
            return AiAgentService.All.Select(a => a.Key).ToList();

        return new();
    }

    // ===================== Chat (streaming) =====================

    /// <summary>
    /// 串流聊天 · Stream a chat completion. Builds the request from the conversation (system prompt +
    /// history), calls <paramref name="onChunk"/> for every token delta, and returns the full text.
    /// Stop 撳掣 → 傳一個已取消嘅 token 落嚟即停。Cancel the token (Stop button) to halt mid-stream.
    /// </summary>
    public async Task<string> StreamChatAsync(ChatConversation chat, AiProvider provider,
        Action<ChatStreamChunk> onChunk, CancellationToken ct)
    {
        if (chat is null || provider is null) return "";
        var model = !string.IsNullOrWhiteSpace(chat.Model) ? chat.Model : provider.DefaultModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            onChunk(new ChatStreamChunk { Delta = Loc.I.Pick(
                "[No model selected]", "[未揀模型]"), Done = true });
            return "";
        }

        var creditReady = CakeCreditService.I.CheckCanStartGeneration("Communication AI", "通訊 AI");
        if (!creditReady.Success)
        {
            onChunk(new ChatStreamChunk
            {
                Done = true,
                CreditSuccess = false,
                CreditUnits = creditReady.UnitsRequested,
                CreditMessage = creditReady.Message,
            });
            return "";
        }

        var msgs = BuildMessages(chat);
        int? completionTokens = null;
        void Capture(ChatStreamChunk chunk)
        {
            if (chunk.CompletionTokens is int ctok) completionTokens = ctok;
            onChunk(chunk);
        }

        string output = provider.Kind == AiProviderKind.Cli
            ? await StreamCliAsync(chat, model, Capture, ct)
            : provider.Kind == AiProviderKind.Ollama
                ? await StreamOllamaAsync(provider, model, chat, msgs, Capture, ct)
                : await StreamOpenAiAsync(provider, model, chat, msgs, Capture, ct);

        if (!string.IsNullOrWhiteSpace(output))
        {
            var units = CakeCreditService.GeneratedUnitsFrom(completionTokens, output);
            var charge = CakeCreditService.I.TryChargeGeneratedUnits("Communication AI", "通訊 AI", units);
            onChunk(new ChatStreamChunk
            {
                Done = true,
                CreditSuccess = charge.Success,
                CreditUnits = units,
                CreditMessage = charge.Message,
            });
        }

        return output;
    }

    private static List<(string role, string content)> BuildMessages(ChatConversation chat)
    {
        var list = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(chat.SystemPrompt))
            list.Add((ChatRoles.System, chat.SystemPrompt));
        foreach (var m in chat.Messages)
            if (!string.IsNullOrEmpty(m.Content))
                list.Add((m.Role, m.Content));
        return list;
    }

    private async Task<string> StreamOllamaAsync(AiProvider p, string model, ChatConversation chat,
        List<(string role, string content)> msgs, Action<ChatStreamChunk> onChunk, CancellationToken ct)
    {
        var sb = new StringBuilder();
        try
        {
            var options = new Dictionary<string, object> { ["temperature"] = chat.Temperature };
            if (chat.MaxTokens > 0) options["num_predict"] = chat.MaxTokens;
            var payload = new
            {
                model,
                messages = msgs.Select(m => new { role = m.role, content = m.content }).ToArray(),
                stream = true,
                options,
            };
            using var req = new HttpRequestMessage(HttpMethod.Post, Norm(p.BaseUrl) + "/api/chat")
            { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                onChunk(new ChatStreamChunk { Delta = $"[HTTP {(int)resp.StatusCode}] " +
                    await SafeBody(resp), Done = true });
                return sb.ToString();
            }
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var msg) &&
                    msg.TryGetProperty("content", out var content))
                {
                    var delta = content.GetString() ?? "";
                    if (delta.Length > 0) { sb.Append(delta); onChunk(new ChatStreamChunk { Delta = delta }); }
                }
                if (root.TryGetProperty("done", out var done) && done.GetBoolean())
                {
                    int? pt = root.TryGetProperty("prompt_eval_count", out var pe) && pe.TryGetInt32(out var pv) ? pv : null;
                    int? ctk = root.TryGetProperty("eval_count", out var ec) && ec.TryGetInt32(out var ev) ? ev : null;
                    onChunk(new ChatStreamChunk { Done = true, PromptTokens = pt, CompletionTokens = ctk });
                    break;
                }
            }
        }
        catch (OperationCanceledException) { onChunk(new ChatStreamChunk { Done = true }); }
        catch (Exception ex) { onChunk(new ChatStreamChunk { Delta = $"\n[{ex.Message}]", Done = true }); }
        return sb.ToString();
    }

    private async Task<string> StreamOpenAiAsync(AiProvider p, string model, ChatConversation chat,
        List<(string role, string content)> msgs, Action<ChatStreamChunk> onChunk, CancellationToken ct)
    {
        var sb = new StringBuilder();
        try
        {
            var payload = new Dictionary<string, object>
            {
                ["model"] = model,
                ["messages"] = msgs.Select(m => new { role = m.role, content = m.content }).ToArray(),
                ["stream"] = true,
                ["temperature"] = chat.Temperature,
            };
            if (chat.MaxTokens > 0) payload["max_tokens"] = chat.MaxTokens;

            using var req = new HttpRequestMessage(HttpMethod.Post, Norm(p.BaseUrl) + "/v1/chat/completions")
            { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") };
            if (!string.IsNullOrEmpty(p.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", p.ApiKey);

            using var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
            {
                onChunk(new ChatStreamChunk { Delta = $"[HTTP {(int)resp.StatusCode}] " +
                    await SafeBody(resp), Done = true });
                return sb.ToString();
            }
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            int? completion = null;
            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null)
            {
                if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
                var data = line.Substring(5).Trim();
                if (data == "[DONE]") break;
                if (data.Length == 0) continue;
                try
                {
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var ch0 = choices[0];
                        if (ch0.TryGetProperty("delta", out var delta) &&
                            delta.TryGetProperty("content", out var dc) && dc.ValueKind == JsonValueKind.String)
                        {
                            var s = dc.GetString() ?? "";
                            if (s.Length > 0) { sb.Append(s); onChunk(new ChatStreamChunk { Delta = s }); }
                        }
                    }
                    if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object &&
                        usage.TryGetProperty("completion_tokens", out var ctk) && ctk.TryGetInt32(out var cv))
                        completion = cv;
                }
                catch { }
            }
            onChunk(new ChatStreamChunk { Done = true, CompletionTokens = completion });
        }
        catch (OperationCanceledException) { onChunk(new ChatStreamChunk { Done = true }); }
        catch (Exception ex) { onChunk(new ChatStreamChunk { Delta = $"\n[{ex.Message}]", Done = true }); }
        return sb.ToString();
    }

    /// <summary>CLI 一次性提問 · One-shot prompt to an installed terminal agent (non-streaming).</summary>
    private async Task<string> StreamCliAsync(ChatConversation chat, string agentKey,
        Action<ChatStreamChunk> onChunk, CancellationToken ct)
    {
        var agent = AiAgentService.All.FirstOrDefault(a => a.Key == agentKey);
        var lastUser = chat.Messages.LastOrDefault(m => m.Role == ChatRoles.User)?.Content ?? "";
        if (agent is null || string.IsNullOrWhiteSpace(lastUser))
        {
            onChunk(new ChatStreamChunk { Delta = Loc.I.Pick(
                "[CLI agent not available]", "[CLI 代理唔可用]"), Done = true });
            return "";
        }
        try
        {
            // 大部分代理支援 -p / --print 做一次性無互動輸出。Most agents accept "-p" for one-shot output.
            var output = await ShellRunner.Capture(agent.Cli, $"-p \"{lastUser.Replace("\"", "\\\"")}\"", ct);
            onChunk(new ChatStreamChunk { Delta = output ?? "", Done = false });
            onChunk(new ChatStreamChunk { Done = true });
            return output ?? "";
        }
        catch (Exception ex)
        {
            onChunk(new ChatStreamChunk { Delta = $"[{ex.Message}]", Done = true });
            return "";
        }
    }

    // ===================== helpers =====================

    private static string Norm(string baseUrl) => (baseUrl ?? "").Trim().TrimEnd('/');

    private static async Task<string> SafeBody(HttpResponseMessage r)
    {
        try { return (await r.Content.ReadAsStringAsync()).Trim(); } catch { return ""; }
    }
}
