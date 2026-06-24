using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 一個已儲存嘅 Git 儲存庫紀錄 · One saved Git repository entry in the multi-repo list.
/// 純資料、可變、可無參數建構（俾 System.Text.Json 用）。
/// Plain mutable data, parameterless-constructible for System.Text.Json round-tripping.
/// </summary>
public sealed class RepoEntry
{
    /// <summary>儲存庫資料夾嘅絕對路徑 · Absolute path to the repository folder.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>顯示名（預設係資料夾名）· Display name (defaults to the folder leaf name).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>origin 遠端 URL（搵唔到就空）· The origin remote URL, or "" if none.</summary>
    public string Remote { get; set; } = string.Empty;

    /// <summary>最後已知嘅目前分支 · Last-known current branch, or "".</summary>
    public string Branch { get; set; } = string.Empty;
}

/// <summary>
/// 多儲存庫清單 · A persistent, multi-repository list for the Git/GitHub module.
/// 喺 <see cref="AppState.CurrentRepoPath"/>（單一選中庫）之上，額外保存一個用戶可以加入、掃描、
/// 揀選同移除嘅儲存庫清單，寫入 <see cref="SettingsStore"/> 嘅 "git.repos" key（JSON 陣列）。
/// On top of the single selected repo, this keeps a saved list the user can add, scan for, select
/// and remove, persisted to the SettingsStore "git.repos" key as a JSON array.
/// </summary>
public static class RepoStore
{
    private const string Key = "git.repos";

    private static readonly object Gate = new();
    private static readonly List<RepoEntry> _all = new();

    /// <summary>清單一有改動就觸發 · Raised after any mutation to the list.</summary>
    public static event EventHandler? Changed;

    /// <summary>記憶體中嘅儲存庫清單（只讀）· The in-memory repo list (read-only view).</summary>
    public static IReadOnlyList<RepoEntry> All
    {
        get { lock (Gate) return _all.ToArray(); }
    }

    static RepoStore()
    {
        LoadLocked();
        RefreshActive();
    }

    // ---- persistence -------------------------------------------------------

    private static void LoadLocked()
    {
        lock (Gate)
        {
            _all.Clear();
            try
            {
                var json = SettingsStore.Get(Key, string.Empty);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonSerializer.Deserialize<List<RepoEntry>>(json);
                    if (loaded is not null)
                    {
                        foreach (var e in loaded)
                            if (e is not null && !string.IsNullOrWhiteSpace(e.Path))
                                _all.Add(e);
                    }
                }
            }
            catch { /* 損壞就當空 · ignore corrupt value */ }
        }
    }

    private static void SaveLocked()
    {
        try
        {
            SettingsStore.Set(Key, JsonSerializer.Serialize(_all,
                new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { /* best effort */ }
    }

    private static void RaiseChanged() => Changed?.Invoke(null, EventArgs.Empty);

    // ---- mutations ---------------------------------------------------------

    /// <summary>
    /// 加一個儲存庫入清單 · Add a repository to the list.
    /// 會正規化路徑、略過空白或唔存在嘅資料夾；同路徑（唔分大細階）已存在就直接回傳原本嗰個。
    /// 否則建立新紀錄，盡力由 .git/config 讀 origin URL、由 .git/HEAD 讀目前分支。
    /// Normalizes/trims; ignores null/empty or non-existent dirs; returns the existing entry on a
    /// case-insensitive Path match; otherwise creates one, best-effort filling Remote and Branch.
    /// </summary>
    public static RepoEntry? Add(string path)
    {
        var norm = Normalize(path);
        if (string.IsNullOrEmpty(norm)) return null;
        if (!Directory.Exists(norm)) return null;

        lock (Gate)
        {
            var existing = _all.FirstOrDefault(e =>
                string.Equals(e.Path, norm, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) return existing;

            var entry = new RepoEntry
            {
                Path = norm,
                Name = LeafName(norm),
                Remote = ReadOriginUrl(norm),
                Branch = ReadHeadBranch(norm),
            };
            _all.Add(entry);
            SaveLocked();
            RaiseChanged();
            return entry;
        }
    }

    /// <summary>由清單移除（唔分大細階配對路徑）· Remove by case-insensitive Path match.</summary>
    public static void Remove(string path)
    {
        var norm = Normalize(path);
        if (string.IsNullOrEmpty(norm)) return;

        bool removed;
        lock (Gate)
        {
            removed = _all.RemoveAll(e =>
                string.Equals(e.Path, norm, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) SaveLocked();
        }
        if (removed) RaiseChanged();
    }

    /// <summary>
    /// 揀一個庫做目前作業對象 · Select a repo as the active one.
    /// 只有當佢喺清單內、或者係真實資料夾先會設定。
    /// Sets <see cref="AppState.CurrentRepoPath"/> only if it is present in the list or a real dir.
    /// </summary>
    public static void Select(string path)
    {
        var norm = Normalize(path);
        if (string.IsNullOrEmpty(norm)) return;
        if (Contains(norm) || Directory.Exists(norm))
            AppState.CurrentRepoPath = norm;
    }

    /// <summary>清單入面有冇呢個路徑（唔分大細階）· Whether the list contains this path (case-insensitive).</summary>
    public static bool Contains(string path)
    {
        var norm = Normalize(path);
        if (string.IsNullOrEmpty(norm)) return false;
        lock (Gate)
            return _all.Any(e => string.Equals(e.Path, norm, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 掃描資料夾搵儲存庫 · Scan a folder tree for repositories.
    /// 由 parentFolder 行落最多 maxDepth 層，凡係直接含有 ".git"（檔案或資料夾）嘅子目錄就 Add。
    /// 對拒絕存取等錯誤要穩陣（逐個目錄 try/catch，永遠唔會喺行走途中拋出）。回傳新加入嘅數目。
    /// Walks up to maxDepth levels deep; for each subdirectory directly containing a ".git" entry
    /// (file or directory), calls Add. Robust to access-denied (never throws out of the walk).
    /// Returns the number of NEW repos added. Pure filesystem — does not use ShellRunner.
    /// </summary>
    public static async Task<int> ScanFolderAsync(string parentFolder, int maxDepth = 2,
        CancellationToken ct = default)
    {
        var root = Normalize(parentFolder);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) return 0;

        return await Task.Run(() =>
        {
            int added = 0;
            // BFS so maxDepth counts levels below the root (root itself is depth 0).
            var queue = new Queue<(string dir, int depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var (dir, depth) = queue.Dequeue();

                // Does this directory directly contain a ".git" entry (file OR directory)?
                try
                {
                    var dotGit = System.IO.Path.Combine(dir, ".git");
                    if (Directory.Exists(dotGit) || File.Exists(dotGit))
                    {
                        var before = Contains(dir);
                        Add(dir);
                        if (!before && Contains(dir)) added++;
                    }
                }
                catch { /* unreadable -> skip · 唔讀得就跳過 */ }

                if (depth >= maxDepth) continue;

                // Enqueue children, tolerating access-denied per directory.
                try
                {
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        ct.ThrowIfCancellationRequested();
                        queue.Enqueue((sub, depth + 1));
                    }
                }
                catch { /* access denied / gone -> skip this level · 拒絕存取就跳過 */ }
            }
            return added;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 把目前選中嘅庫補入清單 · Ensure the active repo is in the list.
    /// 若 <see cref="AppState.CurrentRepoPath"/> 係真實資料夾而又未喺清單，就 Add 佢。
    /// </summary>
    public static void RefreshActive()
    {
        var active = AppState.CurrentRepoPath;
        if (!string.IsNullOrWhiteSpace(active) && Directory.Exists(active) && !Contains(active))
            Add(active);
    }

    // ---- helpers -----------------------------------------------------------

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        var p = path.Trim().Trim('"');
        if (string.IsNullOrEmpty(p)) return string.Empty;
        try
        {
            p = System.IO.Path.GetFullPath(p);
        }
        catch { return string.Empty; }
        // Drop a trailing separator (but keep roots like "C:\").
        if (p.Length > 3)
            p = p.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        return p;
    }

    private static string LeafName(string fullPath)
    {
        try
        {
            var name = new DirectoryInfo(fullPath).Name;
            return string.IsNullOrEmpty(name) ? fullPath : name;
        }
        catch { return fullPath; }
    }

    /// <summary>
    /// 由 .git/config 讀 origin URL · Best-effort parse of the [remote "origin"] url = ... line.
    /// 任何錯誤都回傳 ""。Returns "" on any failure.
    /// </summary>
    private static string ReadOriginUrl(string repoPath)
    {
        try
        {
            var config = System.IO.Path.Combine(repoPath, ".git", "config");
            if (!File.Exists(config)) return string.Empty;

            bool inOrigin = false;
            foreach (var raw in File.ReadAllLines(config))
            {
                var line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    // Section header, e.g. [remote "origin"].
                    inOrigin = line.StartsWith("[remote ", StringComparison.OrdinalIgnoreCase)
                               && line.IndexOf("\"origin\"", StringComparison.OrdinalIgnoreCase) >= 0;
                    continue;
                }
                if (inOrigin && line.StartsWith("url", StringComparison.OrdinalIgnoreCase))
                {
                    var eq = line.IndexOf('=');
                    if (eq >= 0)
                        return line[(eq + 1)..].Trim();
                }
            }
        }
        catch { /* leave "" on any failure · 出錯就留空 */ }
        return string.Empty;
    }

    /// <summary>
    /// 由 .git/HEAD 讀目前分支 · Best-effort parse of "ref: refs/heads/&lt;branch&gt;".
    /// 脫離 HEAD（detached）或任何錯誤都回傳 ""。Returns "" when detached or on any failure.
    /// </summary>
    private static string ReadHeadBranch(string repoPath)
    {
        try
        {
            var head = System.IO.Path.Combine(repoPath, ".git", "HEAD");
            if (!File.Exists(head)) return string.Empty;

            var text = File.ReadAllText(head).Trim();
            const string prefix = "ref: refs/heads/";
            if (text.StartsWith(prefix, StringComparison.Ordinal))
                return text[prefix.Length..].Trim();
        }
        catch { /* leave "" on any failure · 出錯就留空 */ }
        return string.Empty;
    }
}
