using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 一個別名 · One saved alias: a name plus an ordered list of git/gh steps run sequentially.
/// 對應 Gitty 嘅 <c>.gittyconf</c> 入面一行（例如 <c>save=add . and push main</c>）。
/// Mirrors one line of Gitty's <c>.gittyconf</c> (e.g. <c>save=add . and push main</c>).
/// </summary>
public sealed class GitAlias
{
    /// <summary>別名（按鈕標籤）· The alias name shown as the one-click button label.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 順序步驟 · The ordered steps. Each is "git &lt;args&gt;" or "gh &lt;args&gt;"; a bare line
    /// without a tool prefix is treated as git. 例如 ["add -A", "commit -m \"wip\"", "push"]。
    /// </summary>
    public List<string> Steps { get; set; } = new();
}

/// <summary>
/// 別名儲存 · Per-repository alias store, persisted as JSON in the app data dir (via
/// <see cref="SettingsStore"/>, keyed by repo path). 同 <see cref="RepoStore"/> 一樣用同一個
/// settings.json。Saved aliases become one-click buttons; running an alias executes its steps
/// in order and stops on the first failure, reporting which step failed.
/// </summary>
public static class GitAliasStore
{
    private const string KeyPrefix = "git.aliases::";
    private static readonly object Gate = new();

    /// <summary>任何別名改動就觸發 · Raised after aliases change for any repo.</summary>
    public static event EventHandler? Changed;

    private static string KeyFor(string repoPath)
    {
        var p = (repoPath ?? string.Empty).Trim().TrimEnd('\\', '/').ToLowerInvariant();
        return KeyPrefix + p;
    }

    /// <summary>讀取某儲存庫嘅所有別名 · Load all aliases for a repository (empty list if none).</summary>
    public static List<GitAlias> Load(string repoPath)
    {
        if (string.IsNullOrWhiteSpace(repoPath)) return new();
        lock (Gate)
        {
            try
            {
                var json = SettingsStore.Get(KeyFor(repoPath), string.Empty);
                if (string.IsNullOrWhiteSpace(json)) return new();
                var list = JsonSerializer.Deserialize<List<GitAlias>>(json);
                return list?.Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Name)).ToList() ?? new();
            }
            catch { return new(); }
        }
    }

    private static void SaveAll(string repoPath, List<GitAlias> aliases)
    {
        lock (Gate)
            SettingsStore.Set(KeyFor(repoPath),
                JsonSerializer.Serialize(aliases, new JsonSerializerOptions { WriteIndented = false }));
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>新增或更新一個別名（按名去重，唔分大細階）· Add or replace an alias by name.</summary>
    public static void Save(string repoPath, GitAlias alias)
    {
        if (string.IsNullOrWhiteSpace(repoPath) || alias is null || string.IsNullOrWhiteSpace(alias.Name)) return;
        var all = Load(repoPath);
        all.RemoveAll(a => string.Equals(a.Name, alias.Name, StringComparison.OrdinalIgnoreCase));
        all.Add(alias);
        SaveAll(repoPath, all);
    }

    /// <summary>刪除一個別名 · Remove an alias by name.</summary>
    public static void Remove(string repoPath, string name)
    {
        if (string.IsNullOrWhiteSpace(repoPath) || string.IsNullOrWhiteSpace(name)) return;
        var all = Load(repoPath);
        if (all.RemoveAll(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) > 0)
            SaveAll(repoPath, all);
    }

    /// <summary>
    /// 順序執行一個別名 · Run an alias' steps in order against the selected repo.
    /// 第一步失敗就停低並報告係邊一步。Stops on first failure and reports which step failed.
    /// 每步以 "git " 或 "gh " 開頭決定用邊個工具；冇前綴當 git。
    /// </summary>
    public static async Task<TweakResult> Run(GitAlias alias, IProgress<string>? progress, CancellationToken ct = default)
    {
        if (alias is null || alias.Steps.Count == 0)
            return TweakResult.Fail("This alias has no steps.", "呢個別名冇任何步驟。");
        if (!GitService.HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");

        int total = alias.Steps.Count;
        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = alias.Steps[i].Trim();
            if (step.Length == 0) continue;

            var (tool, args) = SplitTool(step);
            progress?.Report($"[{i + 1}/{total}] $ {tool} {args}\n");

            var r = await ShellRunner.RunIn(GitService.Repo, tool, args, elevated: false, ct);
            var outp = (r.Output ?? string.Empty).Trim();
            if (outp.Length > 0) progress?.Report(outp + "\n");

            if (!r.Success)
                return TweakResult.Fail(
                    $"Alias '{alias.Name}' stopped at step {i + 1}/{total}: {tool} {args}",
                    $"別名「{alias.Name}」喺第 {i + 1}/{total} 步停低：{tool} {args}", outp);
        }

        return TweakResult.Ok($"Alias '{alias.Name}' completed ({total} step(s)).",
            $"別名「{alias.Name}」完成（{total} 步）。");
    }

    /// <summary>拆出工具同參數 · Split a step into (tool, args); bare lines default to git.</summary>
    private static (string tool, string args) SplitTool(string step)
    {
        if (step.StartsWith("git ", StringComparison.OrdinalIgnoreCase))
            return ("git", step[4..].Trim());
        if (step.StartsWith("gh ", StringComparison.OrdinalIgnoreCase))
            return ("gh", step[3..].Trim());
        if (string.Equals(step, "git", StringComparison.OrdinalIgnoreCase)) return ("git", string.Empty);
        if (string.Equals(step, "gh", StringComparison.OrdinalIgnoreCase)) return ("gh", string.Empty);
        return ("git", step);
    }

    /// <summary>把多行文字解析成步驟 · Parse a multi-line textbox into trimmed, non-empty steps.</summary>
    public static List<string> ParseSteps(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length > 0) list.Add(line);
        }
        return list;
    }
}
