using System;
using System.Collections.Generic;

namespace WinForge.Models;

/// <summary>
/// 聊天供應商種類 · The kind of chat back-end a provider talks to.
/// Ollama 用本機 REST；OpenAI 相容用任何 /v1 端點；CLI 用已安裝嘅終端機代理（一次性提問）。
/// Ollama = local REST; OpenAiCompatible = any /v1 endpoint (OpenAI, OpenRouter, LM Studio…);
/// Cli = installed terminal agents for one-shot prompts.
/// </summary>
public enum AiProviderKind
{
    Ollama = 0,
    OpenAiCompatible = 1,
    Cli = 2,
}

/// <summary>
/// 一個聊天供應商設定 · One configured chat provider/back-end.
/// BaseUrl/Model 純文字儲存；ApiKey 用 DPAPI 加密後先存盤（見 AiChatService）。
/// BaseUrl/Model are plain; ApiKey is DPAPI-encrypted at rest (see AiChatService).
/// </summary>
public sealed class AiProvider
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public AiProviderKind Kind { get; set; } = AiProviderKind.Ollama;

    /// <summary>基底網址 · Base URL, e.g. http://localhost:11434 or https://api.openai.com.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>API 金鑰（記憶體中明文；存盤時加密）· API key (plaintext in memory; encrypted on disk).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>預設模型 id · Default model id for this provider.</summary>
    public string DefaultModel { get; set; } = "";
}

/// <summary>聊天角色 · A chat message role.</summary>
public static class ChatRoles
{
    public const string System = "system";
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>一條聊天訊息 · One message in a conversation.</summary>
public sealed class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Role { get; set; } = ChatRoles.User;
    public string Content { get; set; } = "";

    /// <summary>建立時間（UTC ticks）· Creation time (UTC ticks).</summary>
    public long CreatedTicks { get; set; } = DateTime.UtcNow.Ticks;

    /// <summary>回應 token 數（若 provider 有提供）· Response token count if the provider reports it.</summary>
    public int? TokenCount { get; set; }
}

/// <summary>一段聊天對話（含完整歷史同每段設定）· One conversation with full history and per-chat settings.</summary>
public sealed class ChatConversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";

    /// <summary>用邊個 provider · Which provider id this chat uses.</summary>
    public string ProviderId { get; set; } = "";

    /// <summary>每段模型覆寫（空 = 用 provider 預設）· Per-chat model override (empty = provider default).</summary>
    public string Model { get; set; } = "";

    public string SystemPrompt { get; set; } = "";
    public double Temperature { get; set; } = 0.7;

    /// <summary>0 = 用模型預設 · Max tokens to generate (0 = model default).</summary>
    public int MaxTokens { get; set; } = 0;

    public long CreatedTicks { get; set; } = DateTime.UtcNow.Ticks;
    public long UpdatedTicks { get; set; } = DateTime.UtcNow.Ticks;

    public List<ChatMessage> Messages { get; set; } = new();
}

/// <summary>一個本機 Ollama 模型 · One locally-installed Ollama model row.</summary>
public sealed class OllamaModelInfo
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string ParameterSize { get; set; } = "";
    public string Quantization { get; set; } = "";

    public string SizeDisplay
    {
        get
        {
            double s = Size;
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
            return $"{s:0.#} {u[i]}";
        }
    }
}

/// <summary>一個聊天串流事件 · One streaming chunk callback payload.</summary>
public sealed class ChatStreamChunk
{
    public string Delta { get; set; } = "";
    public bool Done { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
}
