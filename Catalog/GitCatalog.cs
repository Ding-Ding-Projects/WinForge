using System.Collections.Generic;
using System.Linq;
using WinForge.Models;

namespace WinForge.Catalog;

/// <summary>
/// Git／GitHub 操作總目錄 · The master catalog of every Git &amp; GitHub operation.
/// 合併三個來源：原有 <see cref="GitOperations"/>（常用）、<see cref="GitCliOperations"/>（完整 git CLI）、
/// 同 <see cref="GitHubOperations"/>（完整 GitHub／gh）。按 Id 去重，保留首次出現嘅。
/// Aggregates the original common ops, the complete git CLI surface, and the complete GitHub/gh surface,
/// de-duplicating by Id (first occurrence wins) so the GUI shows one clean, exhaustive list.
/// </summary>
public static class GitCatalog
{
    private static List<TweakDefinition>? _all;

    /// <summary>全部去重後嘅操作 · Every operation, de-duplicated by Id.</summary>
    public static IReadOnlyList<TweakDefinition> All => _all ??= Build();

    private static List<TweakDefinition> Build()
    {
        var seen = new HashSet<string>();
        var list = new List<TweakDefinition>();
        foreach (var t in GitOperations.All()
                     .Concat(GitCliOperations.All())
                     .Concat(GitHubOperations.All()))
        {
            if (seen.Add(t.Id)) list.Add(t);
        }
        return list;
    }

    public static int Count => All.Count;

    /// <summary>淨係本機 git 操作（唔包 gh／GitHub）· Local git operations only (no gh/GitHub).</summary>
    public static IEnumerable<TweakDefinition> GitOnly =>
        All.Where(t => !t.Id.StartsWith("gh.") && !t.Id.StartsWith("git.gh-"));

    /// <summary>淨係 GitHub／gh 操作 · GitHub/gh operations only.</summary>
    public static IEnumerable<TweakDefinition> GitHubOnly =>
        All.Where(t => t.Id.StartsWith("gh.") || t.Id.StartsWith("git.gh-"));

    /// <summary>跨語言搜尋全部操作 · Search across both languages and keywords.</summary>
    public static IEnumerable<TweakDefinition> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        var q = query.Trim().ToLowerInvariant();
        return All.Where(t => t.SearchHaystack.Contains(q));
    }
}
