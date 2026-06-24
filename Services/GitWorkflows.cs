using System;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Gitty 工作流程 · Native C# port of the Gitty CLI's opinionated git/GitHub shortcuts
/// (https://github.com/Omibranch/gitty, MIT). 每個方法都係薄薄一層砌返 git/gh 指令，
/// 喺選中嘅儲存庫上跑，唔包外部 Go binary。Each helper composes git/gh commands over
/// <see cref="GitService.RunRaw"/> / <see cref="ShellRunner"/> against the selected repo — no Go binary.
/// </summary>
public static class GitWorkflows
{
    /// <summary>
    /// Gitty「up」· One-shot: stage everything (add -A), commit with a message, then push.
    /// 一鍵：暫存全部、提交、再推送。Streams every step through <paramref name="progress"/>.
    /// </summary>
    public static async Task<TweakResult> Up(string message, IProgress<string>? progress, CancellationToken ct = default)
    {
        if (!GitService.HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        message = string.IsNullOrWhiteSpace(message) ? $"WinForge up [{Timestamp()}]" : message.Trim();
        var safe = message.Replace("\"", "'");

        progress?.Report("$ git add -A\n");
        var add = await GitService.RunRaw("add -A", ct);
        if (!add.Success) return Fail("git add failed.", "git add 失敗。", add.Output);

        progress?.Report($"$ git commit -m \"{safe}\"\n");
        var commit = await GitService.RunRaw($"commit -m \"{safe}\"", ct);
        Report(progress, commit.Output);
        // "nothing to commit" is not fatal — still try to push existing commits.
        bool nothing = (commit.Output ?? string.Empty).Contains("nothing to commit", StringComparison.OrdinalIgnoreCase)
            || (commit.Output ?? string.Empty).Contains("nothing added to commit", StringComparison.OrdinalIgnoreCase);
        if (!commit.Success && !nothing)
            return Fail("git commit failed.", "git commit 失敗。", commit.Output);

        var branch = await GitService.CurrentBranch(ct);
        bool hasUpstream = await GitService.HasUpstream(ct);
        var pushArgs = hasUpstream ? "push" : $"push -u origin {branch}";
        progress?.Report($"$ git {pushArgs}\n");
        var push = await GitService.RunRaw(pushArgs, ct);
        Report(progress, push.Output);
        if (!push.Success) return Fail("git push failed.", "git push 失敗。", push.Output);

        return TweakResult.Ok("Up complete — staged, committed and pushed.",
            "「Up」完成 — 已暫存、提交同推送。");
    }

    /// <summary>
    /// Gitty「checkpoint」· Create a tag at the tip of the chosen branch and push it to origin.
    /// 喺指定分支頂點開一個 tag 並推上 origin，凍結當前狀態。
    /// </summary>
    public static async Task<TweakResult> Checkpoint(string name, string branch,
        IProgress<string>? progress, CancellationToken ct = default)
    {
        if (!GitService.HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        name = (name ?? string.Empty).Trim().Trim('"', '\'');
        if (string.IsNullOrEmpty(name))
            return TweakResult.Fail("Checkpoint name is required.", "請輸入檢查點名稱。");
        if (string.IsNullOrWhiteSpace(branch))
            branch = await GitService.CurrentBranch(ct);

        progress?.Report($"$ git tag \"{name}\" {branch}\n");
        var tag = await GitService.RunRaw($"tag \"{name}\" \"{branch}\"", ct);
        Report(progress, tag.Output);
        if (!tag.Success)
            return Fail($"Could not create tag '{name}' (it may already exist).",
                $"開唔到 tag「{name}」（可能已存在）。", tag.Output);

        progress?.Report($"$ git push origin \"{name}\"\n");
        var push = await GitService.RunRaw($"push origin \"{name}\"", ct);
        Report(progress, push.Output);
        if (!push.Success)
            return TweakResult.Ok($"Checkpoint '{name}' created locally (push to origin failed).",
                $"檢查點「{name}」已喺本機建立（推送 origin 失敗）。");

        return TweakResult.Ok($"Checkpoint '{name}' created and pushed to origin.",
            $"檢查點「{name}」已建立並推上 origin。");
    }

    /// <summary>
    /// Gitty「restore」· Check out a checkpoint tag. WARNING: leaves a detached HEAD.
    /// 還原到檢查點（checkout tag），會進入 detached HEAD 狀態。
    /// </summary>
    public static async Task<TweakResult> Restore(string name, IProgress<string>? progress, CancellationToken ct = default)
    {
        if (!GitService.HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        name = (name ?? string.Empty).Trim().Trim('"', '\'');
        if (string.IsNullOrEmpty(name))
            return TweakResult.Fail("Checkpoint name is required.", "請輸入檢查點名稱。");

        progress?.Report($"$ git checkout \"{name}\"\n");
        var co = await GitService.RunRaw($"checkout \"{name}\"", ct);
        Report(progress, co.Output);
        if (!co.Success) return Fail($"Could not restore checkpoint '{name}'.", $"還原唔到檢查點「{name}」。", co.Output);

        return TweakResult.Ok($"Restored to checkpoint '{name}'. You are now in detached HEAD — create a branch to keep working.",
            $"已還原到檢查點「{name}」。而家係 detached HEAD — 想繼續開發請開新分支。");
    }

    /// <summary>
    /// Gitty「undo」· Soft-reset the last commit, keeping its changes staged (reset --soft HEAD~1).
    /// 撤回上一個提交，改動留喺暫存區。
    /// </summary>
    public static async Task<TweakResult> Undo(IProgress<string>? progress, CancellationToken ct = default)
    {
        if (!GitService.HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");

        var parent = await GitService.RunRaw("rev-parse HEAD~1", ct);
        if (!parent.Success)
            return TweakResult.Fail("Nothing to undo — no previous commit (this is the root commit).",
                "冇嘢可以撤回 — 冇上一個提交（呢個係根提交）。");

        progress?.Report("$ git reset --soft HEAD~1\n");
        var reset = await GitService.RunRaw("reset --soft HEAD~1", ct);
        Report(progress, reset.Output);
        if (!reset.Success) return Fail("git reset failed.", "git reset 失敗。", reset.Output);

        return TweakResult.Ok("Last commit undone. Changes are staged, ready to re-commit.",
            "已撤回上一個提交。改動已暫存，可以再提交。");
    }

    /// <summary>
    /// Gitty「push --share」· Push the current branch, then resolve the GitHub repo URL via gh.
    /// 推送目前分支，再用 gh 攞返 GitHub 網址（俾呼叫者複製到剪貼簿）。
    /// Returns the URL in <see cref="TweakResult.Output"/> on success.
    /// </summary>
    public static async Task<TweakResult> PushAndShare(IProgress<string>? progress, CancellationToken ct = default)
    {
        if (!GitService.HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");

        var branch = await GitService.CurrentBranch(ct);
        bool hasUpstream = await GitService.HasUpstream(ct);
        var pushArgs = hasUpstream ? "push" : $"push -u origin {branch}";
        progress?.Report($"$ git {pushArgs}\n");
        var push = await GitService.RunRaw(pushArgs, ct);
        Report(progress, push.Output);
        if (!push.Success) return Fail("git push failed.", "git push 失敗。", push.Output);

        // Try gh first for the canonical web URL; fall back to the origin remote.
        var url = await ResolveRepoUrl(branch, ct);
        if (string.IsNullOrEmpty(url))
            return TweakResult.Ok("Pushed, but could not resolve the GitHub URL.",
                "已推送，但攞唔到 GitHub 網址。");

        return new TweakResult(true,
            new LocalizedText($"Pushed. Link copied: {url}", $"已推送，連結已複製：{url}"), url);
    }

    /// <summary>攞 PR 網址 · Resolve the open pull-request URL for the current branch via gh.</summary>
    public static async Task<TweakResult> PrUrl(CancellationToken ct = default)
    {
        if (!GitService.HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        var r = await ShellRunner.RunIn(GitService.Repo, "gh", "pr view --json url --jq .url", elevated: false, ct);
        var url = (r.Output ?? string.Empty).Trim();
        if (!r.Success || string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return TweakResult.Fail("No pull request found for this branch.", "呢個分支搵唔到 PR。", r.Output);
        return new TweakResult(true, new LocalizedText($"PR link: {url}", $"PR 連結：{url}"), url);
    }

    private static async Task<string> ResolveRepoUrl(string branch, CancellationToken ct)
    {
        // 1) gh repo view --json url
        var gh = await ShellRunner.RunIn(GitService.Repo, "gh", "repo view --json url --jq .url", elevated: false, ct);
        var url = (gh.Output ?? string.Empty).Trim();
        if (gh.Success && url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return $"{url.TrimEnd('/')}/tree/{branch}";

        // 2) Fall back to origin remote, normalising SSH → HTTPS.
        var remote = await GitService.RunRaw("remote get-url origin", ct);
        var r = (remote.Output ?? string.Empty).Trim();
        if (!remote.Success || string.IsNullOrEmpty(r)) return string.Empty;
        r = r.TrimEnd('/');
        if (r.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) r = r[..^4];
        if (r.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            r = "https://github.com/" + r["git@github.com:".Length..];
        return $"{r}/tree/{branch}";
    }

    private static void Report(IProgress<string>? progress, string? output)
    {
        var t = (output ?? string.Empty).Trim();
        if (t.Length > 0) progress?.Report(t + "\n");
    }

    private static TweakResult Fail(string en, string zh, string? output) =>
        TweakResult.Fail(en, zh, output);

    private static string Timestamp() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");
}
