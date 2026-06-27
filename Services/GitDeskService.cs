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
/// GitHub Desktop 風格嘅 Git 服務 · A GitHub-Desktop-style service that ports the desktop app's
/// workflows natively over the <c>git</c> and <c>gh</c> CLIs (no Electron wrapping). Parses
/// <c>git status --porcelain=v2</c>, <c>git diff</c> (into coloured hunks), <c>git log</c> history
/// with per-commit changed files and diffs, stages/unstages individual files and hunks, commits,
/// pushes, lists branches with ahead/behind, surfaces <c>gh auth status</c>, and creates PRs.
/// 全部用 git／gh 原生重做 GitHub Desktop 嘅工作流程，唔包 Electron 二進位檔。
/// </summary>
public static class GitDeskService
{
    public static string Repo => AppState.CurrentRepoPath;
    public static bool HasRepo => !string.IsNullOrWhiteSpace(Repo) && Directory.Exists(Repo);

    // ===== engine availability =====

    private static bool? _gitOk;
    private static bool? _ghOk;

    /// <summary>git 喺唔喺 PATH · Whether git is on PATH.</summary>
    public static async Task<bool> GitAvailable(CancellationToken ct = default)
    {
        if (_gitOk is { } v) return v;
        var r = await ShellRunner.RunIn(null, "git", "--version", elevated: false, ct);
        _gitOk = r.Success;
        return r.Success;
    }

    /// <summary>gh（GitHub CLI）喺唔喺 PATH · Whether the GitHub CLI (gh) is on PATH.</summary>
    public static async Task<bool> GhAvailable(CancellationToken ct = default)
    {
        if (_ghOk is { } v) return v;
        var r = await ShellRunner.RunIn(null, "gh", "--version", elevated: false, ct);
        _ghOk = r.Success;
        return r.Success;
    }

    /// <summary>裝完之後重新偵測引擎 · Forget the cached engine probes after an install.</summary>
    public static void ResetEngineCache()
    {
        _gitOk = null;
        _ghOk = null;
        PackageService.RefreshProcessPath();
    }

    /// <summary>gh 已認證？ · Whether gh is authenticated (returns the status text too).</summary>
    public static async Task<(bool ok, string text)> GhAuthStatus(CancellationToken ct = default)
    {
        var r = await ShellRunner.RunIn(Repo, "gh", "auth status", elevated: false, ct);
        return (r.Success, (r.Output ?? string.Empty).Trim());
    }

    // ===== low-level exec =====

    private static async Task<(bool ok, string output)> Exec(string args, CancellationToken ct = default)
    {
        if (!HasRepo) return (false, "No repository selected.");
        var r = await ShellRunner.RunIn(Repo, "git", args, elevated: false, ct);
        return (r.Success, r.Output ?? string.Empty);
    }

    public static Task<bool> IsGitRepo(CancellationToken ct = default) => GitService.IsGitRepo(ct);
    public static Task<string> CurrentBranch(CancellationToken ct = default) => GitService.CurrentBranch(ct);

    // ===== status / changes =====

    /// <summary>一個工作區改動 · One working-tree change parsed from porcelain v2.</summary>
    public sealed class Change
    {
        public string Path = string.Empty;        // current path (new path for renames)
        public string? OldPath;                    // original path for renames/copies
        public char IndexStatus = '.';             // staged (X) code
        public char WorkStatus = '.';              // unstaged (Y) code
        public bool Untracked;
        public bool Conflicted;

        /// <summary>有任何已暫存改動 · Has staged content.</summary>
        public bool Staged => !Untracked && IndexStatus is not ('.' or '?' or '!');
        /// <summary>有任何未暫存改動 · Has unstaged content (or is untracked).</summary>
        public bool Unstaged => Untracked || (WorkStatus is not ('.' or '!'));

        /// <summary>單字母狀態（A/M/D/R/?/U…）· One-letter status for the UI badge.</summary>
        public char Badge
        {
            get
            {
                if (Conflicted) return 'U';
                if (Untracked) return '?';
                if (IndexStatus is not ('.' or '?')) return IndexStatus;
                return WorkStatus is not '.' ? WorkStatus : 'M';
            }
        }
    }

    /// <summary>
    /// 列出所有工作區改動（porcelain v2）· List all working-tree changes via porcelain v2.
    /// 同時解析 staged／unstaged／重新命名／衝突／未追蹤。Parses staged/unstaged/renamed/conflict/untracked.
    /// </summary>
    public static async Task<List<Change>> Changes(CancellationToken ct = default)
    {
        var list = new List<Change>();
        var (ok, outp) = await Exec("-c core.quotepath=false status --porcelain=v2 --untracked-files=all", ct);
        if (!ok) return list;

        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            if (raw.Length < 2) continue;
            char kind = raw[0];
            try
            {
                switch (kind)
                {
                    case '1': // ordinary changed entry: "1 <XY> <sub> <mH> <mI> <mW> <hH> <hI> <path>"
                    {
                        var parts = raw.Split(' ', 9);
                        if (parts.Length < 9) break;
                        var xy = parts[1];
                        list.Add(new Change { IndexStatus = xy[0], WorkStatus = xy[1], Path = Unquote(parts[8]) });
                        break;
                    }
                    case '2': // renamed/copied: "2 <XY> ... <Xscore> <path>\t<origPath>" (path TAB origPath)
                    {
                        var parts = raw.Split(' ', 10);
                        if (parts.Length < 10) break;
                        var xy = parts[1];
                        var rest = parts[9];
                        var tab = rest.IndexOf('\t');
                        string cur = tab >= 0 ? rest[..tab] : rest;
                        string old = tab >= 0 ? rest[(tab + 1)..] : "";
                        list.Add(new Change { IndexStatus = xy[0], WorkStatus = xy[1], Path = Unquote(cur), OldPath = Unquote(old) });
                        break;
                    }
                    case 'u': // unmerged (conflict): "u <XY> <sub> <m1> <m2> <m3> <mW> <h1> <h2> <h3> <path>"
                    {
                        var parts = raw.Split(' ', 11);
                        if (parts.Length < 11) break;
                        var xy = parts[1];
                        list.Add(new Change { IndexStatus = xy[0], WorkStatus = xy[1], Path = Unquote(parts[10]), Conflicted = true });
                        break;
                    }
                    case '?': // untracked: "? <path>"
                        list.Add(new Change { Path = Unquote(raw[2..]), Untracked = true, WorkStatus = '?' });
                        break;
                    // '!' ignored entries are skipped (not requested)
                }
            }
            catch { /* tolerate one bad line */ }
        }
        return list.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Unquote(string p)
    {
        p = p.Trim();
        if (p.Length >= 2 && p[0] == '"' && p[^1] == '"')
            p = p[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        return p;
    }

    private static string QuoteArg(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    // ===== diff =====

    /// <summary>一行 diff · One diff line with its colour role.</summary>
    public enum DiffKind { Context, Added, Removed, Header, Hunk, Meta }
    public readonly record struct DiffLine(DiffKind Kind, string Text);

    /// <summary>
    /// 攞一個檔案嘅 diff · Get a unified diff for one path.
    /// staged=true 比較 index vs HEAD；否則比較工作區 vs index。Untracked 檔案用 --no-index 對空樹。
    /// </summary>
    public static async Task<List<DiffLine>> FileDiff(Change change, bool staged, CancellationToken ct = default)
    {
        string path = change.Path;
        string quoted = "\"" + path.Replace("\"", "\\\"") + "\"";
        string args;
        if (change.Untracked && !staged)
        {
            // Show the whole new file as additions.
            args = $"-c core.quotepath=false diff --no-color --no-index -- /dev/null {quoted}";
        }
        else if (staged)
        {
            args = $"-c core.quotepath=false diff --no-color --cached -- {quoted}";
        }
        else
        {
            args = $"-c core.quotepath=false diff --no-color -- {quoted}";
        }
        var (_, outp) = await Exec(args, ct);
        return ParseDiff(outp);
    }

    /// <summary>解析 unified diff 做帶顏色嘅行 · Parse a unified diff into coloured lines.</summary>
    public static List<DiffLine> ParseDiff(string text)
    {
        var lines = new List<DiffLine>();
        if (string.IsNullOrEmpty(text)) return lines;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            if (raw.StartsWith("diff ", StringComparison.Ordinal) ||
                raw.StartsWith("index ", StringComparison.Ordinal) ||
                raw.StartsWith("new file", StringComparison.Ordinal) ||
                raw.StartsWith("deleted file", StringComparison.Ordinal) ||
                raw.StartsWith("similarity ", StringComparison.Ordinal) ||
                raw.StartsWith("rename ", StringComparison.Ordinal) ||
                raw.StartsWith("old mode", StringComparison.Ordinal) ||
                raw.StartsWith("new mode", StringComparison.Ordinal))
                lines.Add(new DiffLine(DiffKind.Meta, raw));
            else if (raw.StartsWith("@@", StringComparison.Ordinal))
                lines.Add(new DiffLine(DiffKind.Hunk, raw));
            else if (raw.StartsWith("+++", StringComparison.Ordinal) || raw.StartsWith("---", StringComparison.Ordinal))
                lines.Add(new DiffLine(DiffKind.Header, raw));
            else if (raw.StartsWith("+", StringComparison.Ordinal))
                lines.Add(new DiffLine(DiffKind.Added, raw));
            else if (raw.StartsWith("-", StringComparison.Ordinal))
                lines.Add(new DiffLine(DiffKind.Removed, raw));
            else
                lines.Add(new DiffLine(DiffKind.Context, raw));
        }
        return lines;
    }

    // ===== stage / unstage / discard =====

    public static Task<TweakResult> Stage(string path, CancellationToken ct = default)
        => RunRaw($"add -- \"{path.Replace("\"", "\\\"")}\"", ct);

    public static Task<TweakResult> Unstage(string path, CancellationToken ct = default)
        => RunRaw($"restore --staged -- \"{path.Replace("\"", "\\\"")}\"", ct);

    public static Task<TweakResult> StageAll(CancellationToken ct = default) => RunRaw("add -A", ct);
    public static Task<TweakResult> UnstageAll(CancellationToken ct = default) => RunRaw("reset", ct);

    /// <summary>放棄一個檔案嘅未暫存改動 · Discard a file's unstaged changes (or delete if untracked).</summary>
    public static async Task<TweakResult> Discard(Change change, CancellationToken ct = default)
    {
        if (change.Untracked)
        {
            try
            {
                var full = Path.Combine(Repo, change.Path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full)) File.Delete(full);
                return TweakResult.Ok("Discarded.", "已放棄。");
            }
            catch (Exception ex) { return TweakResult.Fail(ex.Message, "放棄失敗。"); }
        }
        return await RunRaw($"restore -- \"{change.Path.Replace("\"", "\\\"")}\"", ct);
    }

    /// <summary>
    /// 套用一段 patch 去 index（hunk 級暫存）· Apply a patch hunk to the index (hunk-level staging).
    /// reverse=true 由 index 撤回（取消暫存個 hunk）。reverse=true unstages the hunk.
    /// </summary>
    public static async Task<TweakResult> ApplyPatch(string patch, bool reverse, CancellationToken ct = default)
    {
        if (!HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        var tmp = Path.Combine(Path.GetTempPath(), $"winforge_patch_{Guid.NewGuid():N}.patch");
        try
        {
            // git apply needs LF line endings and a trailing newline.
            await File.WriteAllTextAsync(tmp, patch.Replace("\r\n", "\n"), new UTF8Encoding(false), ct);
            var rev = reverse ? "--reverse " : "";
            var r = await ShellRunner.RunIn(Repo, "git",
                $"apply --cached --whitespace=nowarn --unidiff-zero {rev}\"{tmp}\"", elevated: false, ct);
            return r;
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"套用失敗：{ex.Message}"); }
        finally { try { File.Delete(tmp); } catch { } }
    }

    // ===== commit / sync =====

    /// <summary>提交已暫存嘅改動 · Commit staged changes with summary (+ optional description).</summary>
    public static async Task<TweakResult> Commit(string summary, string? description, CancellationToken ct = default)
    {
        if (!HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        if (string.IsNullOrWhiteSpace(summary)) return TweakResult.Fail("Enter a summary first.", "請先輸入摘要。");
        var args = $"commit -m \"{summary.Replace("\"", "'")}\"";
        if (!string.IsNullOrWhiteSpace(description))
            args += $" -m \"{description.Replace("\"", "'")}\"";
        return await RunRaw(args, ct);
    }

    public static Task<TweakResult> Push(CancellationToken ct = default) => RunRaw("push", ct);
    public static Task<TweakResult> Pull(CancellationToken ct = default) => RunRaw("pull --ff-only", ct);
    public static Task<TweakResult> Fetch(CancellationToken ct = default) => RunRaw("fetch --all --prune", ct);

    public static async Task<TweakResult> PushSetUpstream(CancellationToken ct = default)
    {
        var branch = await CurrentBranch(ct);
        return await RunRaw($"push -u origin \"{branch}\"", ct);
    }

    // ===== ahead / behind =====

    /// <summary>領先／落後上游嘅 commit 數 · Commits ahead/behind the upstream (or null if no upstream).</summary>
    public static async Task<(int ahead, int behind)?> AheadBehind(CancellationToken ct = default)
    {
        var (ok, outp) = await Exec("rev-list --left-right --count @{u}...HEAD", ct);
        if (!ok) return null;
        var parts = outp.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var behind) && int.TryParse(parts[1], out var ahead))
            return (ahead, behind);
        return null;
    }

    // ===== repository overview / remotes / stashes / tags =====

    /// <summary>儲存庫概覽 · Repository summary for the Desktop-style overview tab.</summary>
    public sealed class RepoOverview
    {
        public string Root = string.Empty;
        public string Branch = string.Empty;
        public bool Detached;
        public string Upstream = string.Empty;
        public string OriginUrl = string.Empty;
        public string DefaultBranch = string.Empty;
        public string Head = string.Empty;
        public string ShortHead = string.Empty;
        public string LastSubject = string.Empty;
        public string LastAuthor = string.Empty;
        public string LastDate = string.Empty;
        public string UserName = string.Empty;
        public string UserEmail = string.Empty;
        public int TotalChanges;
        public int Staged;
        public int Unstaged;
        public int Untracked;
        public int Conflicted;
        public int? Ahead;
        public int? Behind;
    }

    /// <summary>攞儲存庫概覽 · Build a local repository overview without touching the network.</summary>
    public static async Task<RepoOverview> Overview(CancellationToken ct = default)
    {
        var overview = new RepoOverview { Root = Repo };
        if (!HasRepo) return overview;

        async Task<string> Read(string args)
        {
            var (ok, outp) = await Exec(args, ct);
            return ok ? outp.Trim() : string.Empty;
        }

        overview.Root = await Read("rev-parse --show-toplevel");
        overview.Branch = await Read("branch --show-current");
        if (string.IsNullOrWhiteSpace(overview.Branch))
        {
            overview.Detached = true;
            overview.Branch = "HEAD";
        }
        overview.Upstream = await Read("rev-parse --abbrev-ref --symbolic-full-name @{u}");
        overview.OriginUrl = await Read("config --get remote.origin.url");
        var defaultRef = await Read("symbolic-ref --quiet --short refs/remotes/origin/HEAD");
        overview.DefaultBranch = defaultRef.StartsWith("origin/", StringComparison.OrdinalIgnoreCase)
            ? defaultRef["origin/".Length..]
            : defaultRef;
        overview.Head = await Read("rev-parse HEAD");
        overview.ShortHead = await Read("rev-parse --short HEAD");
        overview.UserName = await Read("config user.name");
        overview.UserEmail = await Read("config user.email");

        var (logOk, log) = await Exec("log -1 --date=short --format=%an%ad%s", ct);
        if (logOk)
        {
            var f = log.Trim().Split('');
            if (f.Length > 0) overview.LastAuthor = f[0];
            if (f.Length > 1) overview.LastDate = f[1];
            if (f.Length > 2) overview.LastSubject = f[2];
        }

        var changes = await Changes(ct);
        overview.TotalChanges = changes.Count;
        overview.Staged = changes.Count(c => c.Staged);
        overview.Unstaged = changes.Count(c => c.Unstaged);
        overview.Untracked = changes.Count(c => c.Untracked);
        overview.Conflicted = changes.Count(c => c.Conflicted);

        var ab = await AheadBehind(ct);
        if (ab is { } counts)
        {
            overview.Ahead = counts.ahead;
            overview.Behind = counts.behind;
        }

        return overview;
    }

    public sealed class RemoteInfo
    {
        public string Name = string.Empty;
        public string FetchUrl = string.Empty;
        public string PushUrl = string.Empty;
    }

    /// <summary>列出 remotes · List configured remotes with fetch/push URLs.</summary>
    public static async Task<List<RemoteInfo>> Remotes(CancellationToken ct = default)
    {
        var map = new Dictionary<string, RemoteInfo>(StringComparer.OrdinalIgnoreCase);
        var (ok, outp) = await Exec("remote -v", ct);
        if (!ok) return [];

        foreach (var raw in outp.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            var tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            var name = line[..tab].Trim();
            var rest = line[(tab + 1)..].Trim();
            var modeStart = rest.LastIndexOf(" (", StringComparison.Ordinal);
            var url = modeStart >= 0 ? rest[..modeStart].Trim() : rest;
            var mode = modeStart >= 0 ? rest[(modeStart + 2)..].TrimEnd(')') : "";
            if (!map.TryGetValue(name, out var remote))
            {
                remote = new RemoteInfo { Name = name };
                map[name] = remote;
            }
            if (string.Equals(mode, "push", StringComparison.OrdinalIgnoreCase))
                remote.PushUrl = url;
            else
                remote.FetchUrl = url;
        }

        return map.Values.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static Task<TweakResult> AddRemote(string name, string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            return Task.FromResult(TweakResult.Fail("Enter a remote name and URL.", "請輸入 remote 名同網址。"));
        return RunRaw($"remote add {QuoteArg(name.Trim())} {QuoteArg(url.Trim())}", ct);
    }

    public static Task<TweakResult> SetRemoteUrl(string name, string url, bool pushUrl, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            return Task.FromResult(TweakResult.Fail("Enter a remote name and URL.", "請輸入 remote 名同網址。"));
        var mode = pushUrl ? "--push " : "";
        return RunRaw($"remote set-url {mode}{QuoteArg(name.Trim())} {QuoteArg(url.Trim())}", ct);
    }

    public static Task<TweakResult> RemoveRemote(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(TweakResult.Fail("Choose a remote first.", "請先揀一個 remote。"));
        return RunRaw($"remote remove {QuoteArg(name.Trim())}", ct);
    }

    public sealed class StashInfo
    {
        public string Selector = string.Empty;
        public string Hash = string.Empty;
        public string Age = string.Empty;
        public string Message = string.Empty;
    }

    /// <summary>列出 stash · List local stash entries.</summary>
    public static async Task<List<StashInfo>> Stashes(CancellationToken ct = default)
    {
        var list = new List<StashInfo>();
        var (ok, outp) = await Exec("stash list --pretty=format:%gd%h%cr%s", ct);
        if (!ok) return list;
        foreach (var raw in outp.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = raw.Split('');
            if (f.Length < 4) continue;
            list.Add(new StashInfo { Selector = f[0], Hash = f[1], Age = f[2], Message = f[3] });
        }
        return list;
    }

    public static Task<TweakResult> StashPush(string message, bool includeUntracked, CancellationToken ct = default)
    {
        var msg = string.IsNullOrWhiteSpace(message) ? "WinForge stash" : message.Trim();
        var untracked = includeUntracked ? "--include-untracked " : "";
        return RunRaw($"stash push {untracked}-m {QuoteArg(msg)}", ct);
    }

    public static Task<TweakResult> StashApply(string selector, CancellationToken ct = default)
        => RunRaw($"stash apply {QuoteArg(selector)}", ct);

    public static Task<TweakResult> StashPop(string selector, CancellationToken ct = default)
        => RunRaw($"stash pop {QuoteArg(selector)}", ct);

    public static Task<TweakResult> StashDrop(string selector, CancellationToken ct = default)
        => RunRaw($"stash drop {QuoteArg(selector)}", ct);

    public sealed class TagInfo
    {
        public string Name = string.Empty;
        public string Date = string.Empty;
        public string Subject = string.Empty;
    }

    /// <summary>列出 tags · List tags, newest first.</summary>
    public static async Task<List<TagInfo>> Tags(CancellationToken ct = default)
    {
        var list = new List<TagInfo>();
        var (ok, outp) = await Exec("tag --list --sort=-creatordate --format=%(refname:short)%(creatordate:short)%(subject)", ct);
        if (!ok) return list;
        foreach (var raw in outp.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = raw.Split('');
            if (f.Length < 1 || string.IsNullOrWhiteSpace(f[0])) continue;
            list.Add(new TagInfo
            {
                Name = f[0],
                Date = f.Length > 1 ? f[1] : "",
                Subject = f.Length > 2 ? f[2] : "",
            });
        }
        return list;
    }

    public static Task<TweakResult> CreateTag(string name, string? message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(TweakResult.Fail("Enter a tag name first.", "請先輸入 tag 名。"));
        var n = name.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return RunRaw($"tag {QuoteArg(n)}", ct);
        return RunRaw($"tag -a {QuoteArg(n)} -m {QuoteArg(message.Trim())}", ct);
    }

    public static Task<TweakResult> DeleteTag(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(TweakResult.Fail("Choose a tag first.", "請先揀一個 tag。"));
        return RunRaw($"tag -d {QuoteArg(name.Trim())}", ct);
    }

    public static Task<TweakResult> PushTags(CancellationToken ct = default) => RunRaw("push --tags", ct);

    // ===== branches =====

    public sealed class BranchInfo
    {
        public string Name = string.Empty;
        public bool Current;
        public string Upstream = string.Empty;
        public string Subject = string.Empty;
    }

    public static async Task<List<BranchInfo>> Branches(CancellationToken ct = default)
    {
        var list = new List<BranchInfo>();
        // %(HEAD) is "*" for current; fields separated by a unit separator.
        var (ok, outp) = await Exec(
            "branch --format=%(HEAD)%(refname:short)%(upstream:short)%(contents:subject)", ct);
        if (!ok) return list;
        foreach (var raw in outp.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = raw.Split('');
            if (f.Length < 2) continue;
            list.Add(new BranchInfo
            {
                Current = f[0].Trim() == "*",
                Name = f[1].Trim(),
                Upstream = f.Length > 2 ? f[2].Trim() : "",
                Subject = f.Length > 3 ? f[3].Trim() : "",
            });
        }
        return list;
    }

    public static Task<TweakResult> SwitchBranch(string name, CancellationToken ct = default)
        => RunRaw($"switch \"{name}\"", ct);
    public static Task<TweakResult> CreateBranch(string name, CancellationToken ct = default)
        => RunRaw($"switch -c \"{name}\"", ct);
    public static Task<TweakResult> DeleteBranch(string name, CancellationToken ct = default)
        => RunRaw($"branch -D \"{name}\"", ct);
    public static Task<TweakResult> MergeBranch(string name, CancellationToken ct = default)
        => RunRaw($"merge \"{name}\"", ct);

    // ===== history =====

    public sealed class CommitInfo
    {
        public string Hash = string.Empty;
        public string ShortHash = string.Empty;
        public string Author = string.Empty;
        public string Date = string.Empty;
        public string Subject = string.Empty;
        public string Graph = string.Empty; // ASCII lane prefix from --graph
    }

    /// <summary>
    /// 提交歷史（連簡化 lane 圖）· Commit history with a simplified ASCII lane graph prefix.
    /// </summary>
    public static async Task<List<CommitInfo>> History(int max = 200, CancellationToken ct = default)
    {
        var list = new List<CommitInfo>();
        // Use a rare record separator so subjects with any char survive; graph prefix precedes it.
        const string sep = "";
        var fmt = $"%H{sep}%h{sep}%an{sep}%ad{sep}%s";
        var (ok, outp) = await Exec(
            $"-c core.quotepath=false log --graph --date=short --pretty=format:{fmt} --max-count={max}", ct);
        if (!ok) return list;

        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            if (raw.Length == 0) continue;
            int idx = raw.IndexOf(sep, StringComparison.Ordinal);
            if (idx < 0)
            {
                // Pure graph line (merge lanes) — attach to the previous commit's graph for context.
                continue;
            }
            // Everything before the first hash char is the graph prefix; the hash is 40 hex chars.
            // Find where the 40-char hash starts by scanning back from the separator.
            string graph = "";
            string afterGraph = raw;
            // The format begins with %H (40 hex). Locate it.
            int hashStart = FindHashStart(raw, idx);
            if (hashStart > 0)
            {
                graph = raw[..hashStart];
                afterGraph = raw[hashStart..];
            }
            var f = afterGraph.Split(sep);
            if (f.Length < 5) continue;
            list.Add(new CommitInfo
            {
                Graph = graph.TrimEnd(),
                Hash = f[0],
                ShortHash = f[1],
                Author = f[2],
                Date = f[3],
                Subject = f[4],
            });
        }
        return list;
    }

    private static int FindHashStart(string raw, int sepIndex)
    {
        // hash is 40 hex chars immediately before the first separator.
        int start = sepIndex - 40;
        if (start < 0) return -1;
        for (int i = start; i < sepIndex; i++)
            if (!Uri.IsHexDigit(raw[i])) return -1;
        return start;
    }

    /// <summary>一個提交改咗嘅檔案 · Files changed in one commit (name-status).</summary>
    public static async Task<List<(char status, string path)>> CommitFiles(string hash, CancellationToken ct = default)
    {
        var list = new List<(char, string)>();
        var (ok, outp) = await Exec($"-c core.quotepath=false show --name-status --format= {hash}", ct);
        if (!ok) return list;
        foreach (var raw in outp.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();
            if (line.Length < 2) continue;
            char st = line[0];
            var tab = line.IndexOf('\t');
            var path = tab >= 0 ? line[(tab + 1)..].Trim() : line[1..].Trim();
            // renames carry "old\tnew" — show the new path.
            var lastTab = path.LastIndexOf('\t');
            if (lastTab >= 0) path = path[(lastTab + 1)..];
            list.Add((st, Unquote(path)));
        }
        return list;
    }

    /// <summary>一個提交嘅完整 diff · Full diff for one commit.</summary>
    public static async Task<List<DiffLine>> CommitDiff(string hash, CancellationToken ct = default)
    {
        var (_, outp) = await Exec($"-c core.quotepath=false show --no-color {hash}", ct);
        return ParseDiff(outp);
    }

    /// <summary>單一檔案喺某提交嘅 diff · Diff for one file within a commit.</summary>
    public static async Task<List<DiffLine>> CommitFileDiff(string hash, string path, CancellationToken ct = default)
    {
        var q = "\"" + path.Replace("\"", "\\\"") + "\"";
        var (_, outp) = await Exec($"-c core.quotepath=false show --no-color {hash} -- {q}", ct);
        return ParseDiff(outp);
    }

    // ===== clone =====

    /// <summary>複製一個 repo · Clone a repo URL (or owner/repo) into parent, returns dest path.</summary>
    public static async Task<(TweakResult result, string? dest)> Clone(string urlOrSlug, string parent,
        IProgress<string>? progress, CancellationToken ct = default)
    {
        var url = NormalizeCloneUrl(urlOrSlug);
        progress?.Report($"$ git clone {url}\n");
        var r = await ShellRunner.RunIn(parent, "git", $"clone {url}", elevated: false, ct);
        if (!string.IsNullOrWhiteSpace(r.Output)) progress?.Report(r.Output!.Trim() + "\n");
        var name = RepoNameFromUrl(url);
        var dest = Path.Combine(parent, name);
        return (r, Directory.Exists(dest) ? dest : null);
    }

    /// <summary>把 "owner/repo" 變做完整 https URL · Expand "owner/repo" to a full https URL.</summary>
    public static string NormalizeCloneUrl(string s)
    {
        s = s.Trim();
        if (s.Length == 0) return s;
        if (s.Contains("://") || s.StartsWith("git@", StringComparison.Ordinal)) return s;
        // owner/repo shorthand
        var slashes = s.Count(c => c == '/');
        if (slashes == 1 && !s.Contains(' '))
            return $"https://github.com/{s.TrimEnd('/')}.git";
        return s;
    }

    private static string RepoNameFromUrl(string url)
    {
        var s = url.TrimEnd('/');
        var slash = s.LastIndexOf('/');
        var name = slash >= 0 ? s[(slash + 1)..] : s;
        if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) name = name[..^4];
        return string.IsNullOrEmpty(name) ? "repo" : name;
    }

    // ===== pull requests (gh) =====

    /// <summary>用 gh 建立 PR · Create a pull request via gh.</summary>
    public static async Task<TweakResult> CreatePr(string title, string body, bool draft, bool fill,
        CancellationToken ct = default)
    {
        if (!HasRepo) return TweakResult.Fail("No repository selected.", "未揀儲存庫。");
        var args = new StringBuilder("pr create");
        if (draft) args.Append(" --draft");
        if (fill)
        {
            args.Append(" --fill");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(title))
                return TweakResult.Fail("Enter a PR title (or use auto-fill).", "請輸入 PR 標題（或者用自動填）。");
            args.Append($" --title \"{title.Replace("\"", "'")}\"");
            args.Append($" --body \"{(body ?? string.Empty).Replace("\"", "'")}\"");
        }
        return await ShellRunner.RunIn(Repo, "gh", args.ToString(), elevated: false, ct);
    }

    // ===== util =====

    public static Task<TweakResult> RunRaw(string args, CancellationToken ct = default)
    {
        if (!HasRepo) return Task.FromResult(TweakResult.Fail("No repository selected.", "未揀儲存庫。"));
        return ShellRunner.RunIn(Repo, "git", args, elevated: false, ct);
    }
}
