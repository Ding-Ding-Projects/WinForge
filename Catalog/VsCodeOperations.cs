using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// VS Code 操作目錄 · Catalog of parameterless / reference <c>code</c> CLI operations rendered as
/// <see cref="TweakCard"/>s in the VS Code module's operation library (and surfaced in master search).
/// 需要揀檔案／資料夾嘅動作住喺頁面上面（用 FileDialogs）· Actions needing a file/folder picker live on the
/// page itself; this list is the no-argument and informational commands.
/// </summary>
public static class VsCodeOperations
{
    private static TweakDefinition Cli(string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string args, string? keywords = null)
        => Tweak.Action(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            async ct =>
            {
                var path = VsCodeService.ResolvePath();
                if (path is null)
                    return TweakResult.Fail("VS Code (code) not found.", "搵唔到 VS Code（code）。");
                var q = path.Contains(' ') ? $"\"{path}\"" : path;
                return await ShellRunner.RunCmd($"\"{q} {args}\"", elevated: false, ct);
            }, keywords: keywords);

    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // ===== info =====
        Cli("vsc.version", "code --version", "code 版本",
            "Show the installed VS Code version, commit and architecture.",
            "顯示已安裝 VS Code 嘅版本、commit 同架構。",
            "Check", "查睇", "--version", "version 版本"),
        Cli("vsc.status", "code --status", "code 狀態",
            "Print process usage and diagnostics from the running VS Code instance.",
            "印出運行中 VS Code 嘅程序用量同診斷資料。",
            "Show", "顯示", "--status", "status diagnostics 狀態 診斷"),
        Cli("vsc.help", "code --help", "code 說明",
            "Show the full code CLI help (every flag).", "顯示完整 code CLI 說明（所有旗標）。",
            "Show", "顯示", "--help", "help 說明"),

        // ===== windows =====
        Cli("vsc.new-window", "New empty window", "開新空白視窗",
            "Open a brand-new, empty VS Code window (code -n).", "開一個全新空白嘅 VS Code 視窗（code -n）。",
            "Open", "開啟", "-n", "new window empty 新視窗 空白"),

        // ===== extensions =====
        Cli("vsc.list-ext", "List extensions", "列出擴充功能",
            "List installed extensions with their versions (--list-extensions --show-versions).",
            "列出已安裝嘅擴充功能連版本（--list-extensions --show-versions）。",
            "List", "列出", "--list-extensions --show-versions", "extensions list installed 擴充功能 列出"),
        Cli("vsc.list-ext-cat", "List extension categories", "列出擴充功能分類",
            "List installed extensions grouped by category.", "依分類列出已安裝嘅擴充功能。",
            "List", "列出", "--list-extensions --category", "extensions category 分類"),

        // ===== profiles / tunnel reference =====
        Cli("vsc.tunnel-help", "code tunnel --help", "code tunnel 說明",
            "Show help for the remote-dev tunnel subcommand.", "顯示 remote-dev tunnel 子指令嘅說明。",
            "Show", "顯示", "tunnel --help", "tunnel remote help 隧道 遠端 說明"),
        Cli("vsc.tunnel-status", "Tunnel status", "Tunnel 狀態",
            "Show the current code tunnel service status.", "顯示目前 code tunnel 服務嘅狀態。",
            "Show", "顯示", "tunnel status", "tunnel status 隧道 狀態"),
    };
}
