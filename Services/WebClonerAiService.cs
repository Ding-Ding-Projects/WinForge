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
/// 網站複製器嘅 AI 重建步驟 · AI reconstruction pass for the Website Cloner.
/// 攞原生複製出嚟嘅 HTML（連資源），交畀一個已安裝嘅終端機編程代理（claude／codex／opencode…）
/// 喺背景（非互動模式）跑，整出乾淨、可編輯嘅 <c>index.html</c> / <c>styles.css</c> / <c>script.js</c>。
/// Hands the natively-cloned HTML (plus assets) to an installed terminal coding agent and runs it
/// head­lessly so it emits tidy, editable <c>index.html</c> / <c>styles.css</c> / <c>script.js</c>.
/// 冇代理或者代理失敗時會優雅退回——原生複製結果照樣可用。Graceful fallback when no agent is
/// configured or the run fails — the native clone is left untouched and remains usable.
/// 全部防禦性寫法，永遠唔會擲未捕捉嘅例外。Defensive throughout — never throws to the caller.
/// </summary>
public static class WebClonerAiService
{
    /// <summary>畀 AI 睇嘅 HTML 上限（控制 token）· Cap on HTML size fed to the agent (token budget).</summary>
    public const int MaxPromptHtmlChars = 200_000;

    /// <summary>AI 跑嘅時間上限 · Hard wall-clock cap on the agent run.</summary>
    public static readonly TimeSpan RunTimeout = TimeSpan.FromMinutes(5);

    /// <summary>AI 重建結果 · Outcome of the AI reconstruction pass.</summary>
    public sealed record AiResult(
        bool Available,
        bool Success,
        string? AgentName,
        string? OutputPath,
        LocalizedText Message);

    // ===================== Availability =====================

    /// <summary>
    /// 搵第一個已安裝嘅代理 · Find the first installed agent we know how to drive head­lessly.
    /// 回傳 null = 冇得用（UI 應該優雅退回原生模式）。Returns null when none is available.
    /// </summary>
    public static async Task<AiAgent?> FindAvailableAgentAsync(CancellationToken ct = default)
    {
        foreach (var agent in AiAgentService.All)
        {
            if (!SupportsHeadless(agent.Key)) continue;
            try
            {
                if (await AiAgentService.IsInstalledAsync(agent, ct)) return agent;
            }
            catch (Exception ex)
            {
                CrashLogger.Log("WebClonerAiService.FindAvailableAgent", ex);
            }
        }
        return null;
    }

    /// <summary>我哋識點樣非互動咁驅動呢個代理 · Do we know a non-interactive flag for this agent?</summary>
    private static bool SupportsHeadless(string key) => key is "claude" or "codex" or "opencode" or "pi";

    // ===================== Reconstruction =====================

    /// <summary>
    /// 用代理重建已複製嘅頁面 · Reconstruct an already-cloned page with the agent.
    /// <paramref name="cloneFolder"/> 必須已經有原生複製出嚟嘅 index.html。
    /// <paramref name="cloneFolder"/> must already contain the natively-cloned index.html.
    /// </summary>
    public static async Task<AiResult> ReconstructAsync(
        string cloneFolder,
        IProgress<WebsiteClonerService.Progress>? progress,
        CancellationToken ct,
        AiAgent? agent = null)
    {
        try
        {
            agent ??= await FindAvailableAgentAsync(ct);
            if (agent is null)
                return Unavailable();

            var indexPath = Path.Combine(cloneFolder, "index.html");
            if (!File.Exists(indexPath))
                return Failed(agent.Name,
                    "No cloned index.html to reconstruct — run the native clone first.",
                    "冇可供重建嘅 index.html — 請先跑原生複製。");

            Report(progress, $"Using {agent.NameEn} for AI reconstruction…",
                $"用緊 {agent.NameZh} 做 AI 重建…", WebsiteClonerService.LogLevel.Info);

            // 1) 喺一個 ai/ 子資料夾度準備工作區，唔好踩到原生複製品。
            //    Stage a working copy in an ai/ sub-folder so we never clobber the native clone.
            var aiDir = Path.Combine(cloneFolder, "ai");
            Directory.CreateDirectory(aiDir);

            var html = await File.ReadAllTextAsync(indexPath, ct);
            if (html.Length > MaxPromptHtmlChars)
            {
                Report(progress,
                    $"HTML is large ({html.Length:N0} chars) — the agent may truncate it.",
                    $"HTML 偏大（{html.Length:N0} 字元）— 代理可能會截斷。",
                    WebsiteClonerService.LogLevel.Warn);
            }

            await File.WriteAllTextAsync(Path.Combine(aiDir, "source.html"), html, new UTF8Encoding(false), ct);

            var prompt = BuildPrompt();

            // 2) 喺工作資料夾度非互動跑代理 · Run the agent non-interactively in the working folder.
            Report(progress, "Running the agent (this can take a few minutes)…",
                "代理執行中（可能要幾分鐘）…", WebsiteClonerService.LogLevel.Info);

            using var runCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            runCts.CancelAfter(RunTimeout);

            var (cli, args) = HeadlessInvocation(agent.Key, prompt);
            TweakResult run;
            try
            {
                run = await ShellRunner.RunIn(aiDir, cli, args, elevated: false, runCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // 代理逾時 · The agent timed out (not a user cancel).
                Report(progress, "Agent timed out — keeping the native clone.",
                    "代理逾時 — 保留原生複製品。", WebsiteClonerService.LogLevel.Warn);
                return Failed(agent.Name,
                    "The AI agent timed out. The native clone is unchanged and still usable.",
                    "AI 代理逾時。原生複製品冇改動，仍然可用。");
            }

            // 3) 收集代理整出嚟嘅輸出 · Collect what the agent produced.
            var produced = Directory.EnumerateFiles(aiDir, "*.*", SearchOption.AllDirectories)
                .Where(f => IsWebFile(f) &&
                            !f.EndsWith("source.html", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (produced.Count == 0)
            {
                var hint = string.IsNullOrWhiteSpace(run.Output) ? "" : "\n" + WebsiteClonerService_Trunc(run.Output);
                Report(progress, "Agent produced no web files — keeping the native clone." + hint,
                    "代理冇整出網頁檔案 — 保留原生複製品。" + hint, WebsiteClonerService.LogLevel.Warn);
                return Failed(agent.Name,
                    "The AI agent finished but produced no usable output. The native clone is unchanged.",
                    "AI 代理完成咗但冇可用輸出。原生複製品冇改動。");
            }

            foreach (var f in produced)
                Report(progress, $"AI wrote {Path.GetFileName(f)}.",
                    $"AI 寫咗 {Path.GetFileName(f)}。", WebsiteClonerService.LogLevel.Ok);

            var aiIndex = produced.FirstOrDefault(
                f => string.Equals(Path.GetFileName(f), "index.html", StringComparison.OrdinalIgnoreCase));

            return new AiResult(true, true, agent.Name, aiIndex ?? aiDir,
                new LocalizedText(
                    $"AI reconstruction complete via {agent.NameEn} — {produced.Count} file(s) written to the ai/ sub-folder.",
                    $"AI 重建完成（{agent.NameZh}）— {produced.Count} 個檔案寫咗入 ai/ 子資料夾。"));
        }
        catch (OperationCanceledException)
        {
            return Failed(agent?.Name,
                "AI reconstruction cancelled. The native clone is unchanged.",
                "AI 重建已取消。原生複製品冇改動。");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("WebClonerAiService.Reconstruct", ex);
            return Failed(agent?.Name,
                $"AI reconstruction failed: {ex.Message}. The native clone is unchanged.",
                $"AI 重建失敗：{ex.Message}。原生複製品冇改動。");
        }
    }

    // ===================== prompt & invocation =====================

    /// <summary>畀代理嘅指示 · The instruction handed to the agent (English; agents reason in English).</summary>
    private static string BuildPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are reconstructing a cloned web page into clean, editable static source.");
        sb.AppendLine("Read source.html (the raw cloned DOM) in this folder.");
        sb.AppendLine("Also read design-tokens.txt in the PARENT folder if present (extracted colours/fonts/title).");
        sb.AppendLine("Produce three files IN THIS FOLDER:");
        sb.AppendLine("  - index.html : semantic, de-duplicated HTML that links ./styles.css and ./script.js");
        sb.AppendLine("  - styles.css : all styles consolidated, inline <style> and style=\"\" moved out, dead rules removed");
        sb.AppendLine("  - script.js  : any inline scripts moved out (skip third-party trackers/analytics)");
        sb.AppendLine("Keep all local asset references pointing at ../assets/ exactly as they appear in source.html.");
        sb.AppendLine("Do NOT fetch anything from the network. Do NOT add new external dependencies.");
        sb.AppendLine("Preserve visible text and structure faithfully; only clean up the markup and styles.");
        return sb.ToString();
    }

    /// <summary>
    /// 每個代理嘅非互動呼叫方式 · Per-agent headless invocation (cli, args).
    /// 提示經 stdin 唔可靠，所以用各代理嘅一次性執行旗標。Uses each agent's one-shot run flag.
    /// </summary>
    private static (string cli, string args) HeadlessInvocation(string key, string prompt)
    {
        var q = Quote(prompt);
        return key switch
        {
            // claude -p "<prompt>"  — print mode, runs once and exits.
            "claude" => ("claude", $"-p {q}"),
            // codex exec "<prompt>"  — non-interactive execution.
            "codex" => ("codex", $"exec {q}"),
            // opencode run "<prompt>"  — one-shot run.
            "opencode" => ("opencode", $"run {q}"),
            // pi -p "<prompt>"  — best-effort print mode.
            "pi" => ("pi", $"-p {q}"),
            _ => (key, q),
        };
    }

    private static string Quote(string s) => "\"" + s.Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ") + "\"";

    private static bool IsWebFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".html" or ".htm" or ".css" or ".js";
    }

    // ===================== helpers =====================

    private static void Report(IProgress<WebsiteClonerService.Progress>? p, string en, string zh,
        WebsiteClonerService.LogLevel level)
        => p?.Report(new WebsiteClonerService.Progress(en, zh, level));

    private static AiResult Unavailable() => new(false, false, null, null,
        new LocalizedText(
            "No AI coding agent is installed, so the AI step was skipped — the native clone is ready to use. Install one (e.g. Claude Code, Codex or opencode) via the AI Agents module to enable AI reconstruction.",
            "冇安裝 AI 編程代理，所以略過咗 AI 步驟 — 原生複製品已經可以用。喺「AI 代理」模組安裝一個（例如 Claude Code、Codex 或 opencode）就可以用 AI 重建。"));

    private static AiResult Failed(string? agentName, string en, string zh)
        => new(true, false, agentName, null, new LocalizedText(en, zh));

    private static string WebsiteClonerService_Trunc(string s, int max = 200)
        => s.Length <= max ? s : s[..(max - 1)] + "…";
}
