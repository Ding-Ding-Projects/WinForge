using System;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 建立 Git 操作 · Factory for Git/GitHub operations that run inside the selected repository.
/// 由 <see cref="GitOperations"/> 目錄使用。Used by the generated <see cref="GitOperations"/> catalog.
/// </summary>
public static class GitTweak
{
    /// <summary>執行 git 子指令 · An operation that runs `git &lt;gitArgs&gt;` in the selected repo.</summary>
    public static TweakDefinition Git(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string gitArgs, bool destructive = false, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => GitService.RunRaw(gitArgs, ct),
            requiresAdmin: false, destructive: destructive, keywords: keywords);

    /// <summary>執行其他工具（例如 gh）· An operation that runs another tool (e.g. gh) in the repo.</summary>
    public static TweakDefinition Tool(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string fileName, string arguments, bool destructive = false, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => RunTool(fileName, arguments, ct),
            requiresAdmin: false, destructive: destructive, keywords: keywords);

    private static Task<TweakResult> RunTool(string fileName, string arguments, CancellationToken ct)
    {
        if (!GitService.HasRepo)
            return Task.FromResult(TweakResult.Fail("No repository selected.", "未揀儲存庫。"));
        return ShellRunner.RunIn(GitService.Repo, fileName, arguments, elevated: false, ct);
    }
}
