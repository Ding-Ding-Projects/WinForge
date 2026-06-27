using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 求職信語氣 · The tone to use for the cover letter.
/// </summary>
public enum CoverLetterTone
{
    Professional, // 專業 · neutral professional
    Enthusiastic, // 熱情 · warm and enthusiastic
    Concise,      // 精簡 · short and to the point
    Formal,       // 正式 · very formal
}

/// <summary>
/// 一次生成嘅結果 · The result of one generation run.
/// </summary>
public sealed class ResumeGenResult
{
    public bool Success { get; init; }
    public string Resume { get; init; } = string.Empty;
    public string CoverLetter { get; init; } = string.Empty;

    /// <summary>原始 stdout（除錯用）· Raw stdout, for diagnostics.</summary>
    public string Raw { get; init; } = string.Empty;

    /// <summary>失敗訊息（雙語）· Failure message (bilingual), or null on success.</summary>
    public LocalizedText? Error { get; init; }
}

/// <summary>
/// 履歷 + 求職信寫手（包住一個已安裝嘅 AI 編程代理）。
/// Resume &amp; cover-letter writer — wraps an installed terminal AI coding agent (claude / opencode / …).
/// 負責：砌 prompt、用非互動模式經 stdin 跑代理、由 stdout 解析兩段輸出。永遠唔擲例外。
/// Builds the prompt, runs the agent non-interactively via stdin (temp file → pipe), and splits the
/// two sections out of stdout. Defensive throughout; never throws.
/// </summary>
public static class ResumeWriterService
{
    public const string ResumeMarker = "===RESUME===";
    public const string CoverMarker = "===COVER LETTER===";

    /// <summary>能力已驗證、可用嚟非互動生成嘅代理 keys · Agent CLI keys we know how to drive non-interactively.</summary>
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "claude", "opencode", "codex", "pi",
    };

    /// <summary>邊啲已安裝嘅代理可以攞嚟用 · Installed agents we support, best-effort detection.</summary>
    public static async Task<IReadOnlyList<AiAgent>> SupportedInstalledAsync(CancellationToken ct = default)
    {
        var result = new List<AiAgent>();
        foreach (var a in AiAgentService.All)
        {
            if (!Supported.Contains(a.Key)) continue;
            bool ok = false;
            try { ok = await AiAgentService.IsInstalledAsync(a, ct); } catch { }
            if (ok) result.Add(a);
        }
        return result;
    }

    /// <summary>呢個代理支唔支援非互動生成 · Is this agent one we can drive non-interactively?</summary>
    public static bool IsSupported(AiAgent? a)
        => a is not null && Supported.Contains(a.Key);

    private static string ToneText(CoverLetterTone tone) => tone switch
    {
        CoverLetterTone.Enthusiastic => "enthusiastic, warm and energetic",
        CoverLetterTone.Concise => "concise and to the point (no more than three short paragraphs)",
        CoverLetterTone.Formal => "very formal and traditional",
        _ => "professional and confident",
    };

    /// <summary>
    /// 砌生成用嘅完整 prompt · Build the full generation prompt.
    /// 明確要求模型用清晰嘅分隔符輸出，方便解析。
    /// Explicitly instructs the model to emit clear delimiters so the output can be split reliably.
    /// </summary>
    public static string BuildPrompt(string baseResume, string jobDescription, CoverLetterTone tone)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are an expert career coach and professional resume writer.");
        sb.AppendLine("Tailor the candidate's base resume to the target job, and write a matching cover letter.");
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Keep everything truthful: only reorganize, rephrase and emphasize what is in the base resume; do NOT invent experience, employers, dates or credentials.");
        sb.AppendLine("- Mirror the keywords and priorities of the job description so the resume passes applicant-tracking screening.");
        sb.AppendLine("- Output the tailored resume in clean Markdown.");
        sb.AppendLine($"- Write the cover letter in a {ToneText(tone)} tone, addressed appropriately, ready to send.");
        sb.AppendLine("- Do NOT add any commentary, preamble or explanation before, between or after the two sections.");
        sb.AppendLine();
        sb.AppendLine($"Output EXACTLY in this format, including the two delimiter lines on their own lines:");
        sb.AppendLine(ResumeMarker);
        sb.AppendLine("<the tailored resume in Markdown>");
        sb.AppendLine(CoverMarker);
        sb.AppendLine("<the cover letter>");
        sb.AppendLine();
        sb.AppendLine("=== BASE RESUME ===");
        sb.AppendLine(string.IsNullOrWhiteSpace(baseResume) ? "(none provided)" : baseResume);
        sb.AppendLine();
        sb.AppendLine("=== TARGET JOB DESCRIPTION ===");
        sb.AppendLine(string.IsNullOrWhiteSpace(jobDescription) ? "(none provided)" : jobDescription);
        return sb.ToString();
    }

    /// <summary>
    /// 把 prompt 經 stdin 餵畀代理（避免命令列長度上限）· Pipe the prompt to the agent over stdin.
    /// 用臨時檔 + cmd 嘅「type file | cli flags」管道，所以唔受單一參數長度限制。
    /// Writes the prompt to a temp file and runs `cmd /c type "file" | <cli> <flags>` so we never hit
    /// the command-line length limit. Returns raw stdout.
    /// </summary>
    private static async Task<TweakResult> RunAgentAsync(AiAgent agent, string prompt, CancellationToken ct)
    {
        string? temp = null;
        try
        {
            temp = Path.Combine(Path.GetTempPath(), $"winforge_resume_{Guid.NewGuid():N}.txt");
            await File.WriteAllTextAsync(temp, prompt, new UTF8Encoding(false), ct);

            // Per-agent non-interactive invocation (prompt arrives on stdin).
            // claude   : `claude -p`  (print mode, reads prompt from stdin)
            // opencode : `opencode run`  (reads message from stdin when no positional arg)
            // codex    : `codex exec`  (non-interactive exec, reads from stdin)
            // pi       : `pi -p`  (print mode)
            string flags = agent.Key.ToLowerInvariant() switch
            {
                "claude" => "-p",
                "opencode" => "run",
                "codex" => "exec",
                "pi" => "-p",
                _ => "",
            };

            var cmd = $"type \"{temp}\" | {agent.Cli} {flags}".Trim();
            var r = await ShellRunner.RunCmd(cmd, false, ct);
            return r;
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Cancelled.", "已取消。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
        finally
        {
            try { if (temp is not null && File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    /// <summary>
    /// 生成度身履歷 + 求職信 · Generate a tailored resume and cover letter.
    /// 包含未支援代理、缺 API key 嘅友善處理；解析失敗時會把全部 stdout 倒落履歷欄做後備。
    /// Handles unsupported agents and missing API keys gracefully; on parse failure, falls back to
    /// dumping all stdout into the resume pane. Never throws.
    /// </summary>
    public static async Task<ResumeGenResult> GenerateAsync(AiAgent agent, string baseResume,
        string jobDescription, CoverLetterTone tone, CancellationToken ct = default)
    {
        if (agent is null || string.IsNullOrWhiteSpace(agent.Cli))
            return new ResumeGenResult { Success = false, Error = new("No agent selected.", "未揀代理。") };

        if (!IsSupported(agent))
            return new ResumeGenResult
            {
                Success = false,
                Error = new($"{agent.NameEn} cannot be driven non-interactively yet. Pick Claude Code, opencode, Codex or Pi.",
                            $"{agent.NameZh} 暫時唔支援非互動生成。請揀 Claude Code、opencode、Codex 或 Pi。"),
            };

        // Friendly check for a missing API key (only for agents that need one).
        if (!string.IsNullOrEmpty(agent.EnvKey) && string.IsNullOrEmpty(AiAgentService.GetEnvKey(agent)))
            return new ResumeGenResult
            {
                Success = false,
                Error = new($"No API key found for {agent.NameEn} ({agent.EnvKey}). Set it in the AI Agents module first.",
                            $"搵唔到 {agent.NameZh} 嘅 API 金鑰（{agent.EnvKey}）。請先喺 AI 代理模組設定。"),
            };

        var creditReady = CakeCreditService.I.CheckCanStartGeneration(agent.NameEn, agent.NameZh);
        if (!creditReady.Success)
            return new ResumeGenResult { Success = false, Error = creditReady.Message };

        var prompt = BuildPrompt(baseResume, jobDescription, tone);
        var r = await RunAgentAsync(agent, prompt, ct);

        var raw = (r.Output ?? string.Empty).Trim();

        if (ct.IsCancellationRequested)
            return new ResumeGenResult { Success = false, Raw = raw, Error = new("Cancelled.", "已取消。") };

        if (!r.Success && string.IsNullOrWhiteSpace(raw))
            return new ResumeGenResult
            {
                Success = false,
                Raw = raw,
                Error = new(
                    $"The agent did not return any output. {(Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? ""}".Trim(),
                    $"代理冇任何輸出。{r.Message?.Zh ?? ""}".Trim()),
            };

        var charge = CakeCreditService.I.TryChargeGeneratedUnits(
            agent.NameEn,
            agent.NameZh,
            CakeCreditService.EstimateGeneratedUnits(raw));
        if (!charge.Success)
            return new ResumeGenResult { Success = false, Raw = raw, Error = charge.Message };

        var (resume, cover) = Split(raw);
        return new ResumeGenResult
        {
            Success = true,
            Resume = resume,
            CoverLetter = cover,
            Raw = raw,
        };
    }

    /// <summary>
    /// 由 stdout 拆出履歷同求職信 · Split stdout into resume + cover-letter on the delimiters.
    /// 後備：搵唔到分隔符就把全部當做履歷。Fallback: everything → resume pane if markers are missing.
    /// </summary>
    public static (string resume, string cover) Split(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return (string.Empty, string.Empty);

        int ri = raw.IndexOf(ResumeMarker, StringComparison.OrdinalIgnoreCase);
        int ci = raw.IndexOf(CoverMarker, StringComparison.OrdinalIgnoreCase);

        // Both markers present and in order.
        if (ri >= 0 && ci > ri)
        {
            var resume = raw.Substring(ri + ResumeMarker.Length, ci - (ri + ResumeMarker.Length)).Trim();
            var cover = raw.Substring(ci + CoverMarker.Length).Trim();
            return (resume, cover);
        }

        // Only the cover marker present.
        if (ci >= 0)
        {
            var resume = raw.Substring(0, ci).Replace(ResumeMarker, "", StringComparison.OrdinalIgnoreCase).Trim();
            var cover = raw.Substring(ci + CoverMarker.Length).Trim();
            return (resume, cover);
        }

        // Fallback: dump everything into the resume pane.
        return (raw.Replace(ResumeMarker, "", StringComparison.OrdinalIgnoreCase).Trim(), string.Empty);
    }

    /// <summary>
    /// 把文字寫去檔案 · Best-effort write text to a path. Returns a bilingual TweakResult.
    /// </summary>
    public static async Task<TweakResult> SaveTextAsync(string path, string content, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return TweakResult.Fail("No path.", "冇路徑。");
        try
        {
            await File.WriteAllTextAsync(path, content ?? string.Empty, new UTF8Encoding(false), ct);
            return TweakResult.Ok($"Saved to {path}", $"已儲存至 {path}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }
}
