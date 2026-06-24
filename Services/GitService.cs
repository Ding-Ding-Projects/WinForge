using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 包住 git CLI · A thin wrapper over the git command line, scoped to
/// <see cref="AppState.CurrentRepoPath"/>. Includes the chunked uploader that splits a large
/// working tree into commits of a chosen size and pushes them one at a time.
/// 包括「分批上載」：將大批改動切成指定大細嘅 commit，逐個 push。
/// </summary>
public static class GitService
{
    public static string Repo => AppState.CurrentRepoPath;

    public static bool HasRepo => !string.IsNullOrWhiteSpace(Repo) && Directory.Exists(Repo);

    /// <summary>執行 git 指令並擷取輸出 · Run a raw git command and capture output.</summary>
    public static async Task<TweakResult> RunRaw(string gitArgs, CancellationToken ct = default)
    {
        if (!HasRepo)
            return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        return await ShellRunner.RunIn(Repo, "git", gitArgs, elevated: false, ct);
    }

    private static async Task<(bool ok, string output)> Exec(string args, CancellationToken ct = default)
    {
        var r = await ShellRunner.RunIn(Repo, "git", args, elevated: false, ct);
        return (r.Success, (r.Output ?? string.Empty).Trim());
    }

    public static async Task<bool> IsGitRepo(CancellationToken ct = default)
    {
        if (!HasRepo) return false;
        var (ok, _) = await Exec("rev-parse --is-inside-work-tree", ct);
        return ok;
    }

    public static async Task<string> CurrentBranch(CancellationToken ct = default)
    {
        var (ok, outp) = await Exec("rev-parse --abbrev-ref HEAD", ct);
        return ok ? outp.Trim() : "—";
    }

    public static async Task<bool> HasUpstream(CancellationToken ct = default)
    {
        var (ok, _) = await Exec("rev-parse --abbrev-ref --symbolic-full-name @{u}", ct);
        return ok;
    }

    public static async Task<string> StatusText(CancellationToken ct = default)
    {
        var (_, outp) = await Exec("-c core.quotepath=false status", ct);
        return outp;
    }

    /// <summary>列出待提交嘅檔案同大細 · List changed/untracked files with on-disk sizes.</summary>
    public static async Task<List<(string path, long size)>> PendingFiles(CancellationToken ct = default)
    {
        var list = new List<(string, long)>();
        var (ok, outp) = await Exec("-c core.quotepath=false status --porcelain", ct);
        if (!ok) return list;

        foreach (var raw in outp.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            if (line.Length < 4) continue;
            var path = line.Substring(3).Trim();
            // renames: "old -> new" — keep the new path
            var arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0) path = path[(arrow + 4)..];
            path = path.Trim('"');

            long size = 0;
            try
            {
                var full = Path.Combine(Repo, path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full)) size = new FileInfo(full).Length;
            }
            catch { /* deleted / unreadable -> size 0 */ }

            list.Add((path, size));
        }
        return list;
    }

    /// <summary>
    /// 分批上載：每個 commit 唔超過 maxBytes，逐個 commit push 完先繼續。
    /// Chunked upload: bucket pending files so each commit is at most maxBytes, committing and
    /// pushing ONE commit at a time before moving to the next.
    /// </summary>
    public static async Task<TweakResult> ChunkedUpload(long maxBytes, string message,
        IProgress<string>? progress, CancellationToken ct = default)
    {
        if (!HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        if (maxBytes <= 0) maxBytes = 25L * 1024 * 1024;

        var files = await PendingFiles(ct);
        if (files.Count == 0)
            return TweakResult.Ok("Nothing to upload — working tree is clean.", "冇嘢可以上載 — 工作區乾淨。");

        // Bucket files greedily so each bucket's total size <= maxBytes (a single oversize file
        // gets its own bucket — git can't split one file across commits).
        var buckets = new List<List<string>>();
        var current = new List<string>();
        long running = 0;
        foreach (var (path, size) in files.OrderByDescending(f => f.size))
        {
            if (current.Count > 0 && running + size > maxBytes)
            {
                buckets.Add(current);
                current = new List<string>();
                running = 0;
            }
            current.Add(path);
            running += size;
        }
        if (current.Count > 0) buckets.Add(current);

        var branch = await CurrentBranch(ct);
        var hasUpstream = await HasUpstream(ct);
        int n = buckets.Count;
        progress?.Report($"Planned {n} commit(s) · 計劃 {n} 個 commit\n");

        for (int i = 0; i < n; i++)
        {
            ct.ThrowIfCancellationRequested();
            var bucket = buckets[i];

            // Stage this bucket via a pathspec file (handles many/long paths safely).
            var listFile = Path.Combine(Path.GetTempPath(), $"winforge_add_{i}.txt");
            await File.WriteAllLinesAsync(listFile, bucket, ct);
            var (addOk, addOut) = await Exec($"add --pathspec-from-file=\"{listFile}\"", ct);
            try { File.Delete(listFile); } catch { /* ignore */ }
            if (!addOk)
                return TweakResult.Fail($"git add failed on commit {i + 1}.", $"第 {i + 1} 個 commit add 失敗。", addOut);

            var msg = $"{message} [{i + 1}/{n}]";
            var (commitOk, commitOut) = await Exec($"commit -m \"{msg}\"", ct);
            if (!commitOk)
            {
                progress?.Report($"[{i + 1}/{n}] nothing staged, skipped · 冇嘢 staged，跳過\n");
                continue;
            }
            progress?.Report($"[{i + 1}/{n}] committed {bucket.Count} file(s) · 已提交 {bucket.Count} 個檔案\n");

            // Push THIS commit before continuing to the next bucket.
            var pushArgs = (i == 0 && !hasUpstream) ? $"push -u origin {branch}" : "push";
            var (pushOk, pushOut) = await Exec(pushArgs, ct);
            if (!pushOk)
                return TweakResult.Fail($"git push failed after commit {i + 1}.", $"第 {i + 1} 個 commit push 失敗。", pushOut);
            hasUpstream = true;
            progress?.Report($"[{i + 1}/{n}] pushed ✓ · 已推送 ✓\n");
        }

        return TweakResult.Ok($"Uploaded {n} commit(s), pushed one at a time.",
            $"已上載 {n} 個 commit，逐個 push 完成。");
    }
}
