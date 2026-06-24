using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Nmap 操作目錄 · Catalog of one-shot Nmap helper operations rendered as TweakCards — print the
/// version, open the Zenmap GUI, show help/script docs, list NSE scripts, run a quick localhost scan.
/// 由 TweakCard 顯示；快速指令擷取輸出。Quick commands capture output; GUI launches open a window.
///
/// 顯示升級（行為不變）· Presentation upgrades (behaviour unchanged): 每張卡掛一個「已裝／搵唔到」彩色
/// 狀態藥丸（平價同步探測，唔行 shell）；耗時嘅掃描同 NSE 列舉顯示進度條。Each card gains an
/// "installed / not found" status pill from a cheap synchronous probe (no shell in the getter), and
/// the long-running scan / NSE enumeration show a progress bar while they run.
/// </summary>
public static class NmapOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        ShellPill("nmap.version", "Nmap version", "Nmap 版本",
            "Print the installed Nmap version and build details.", "印出已安裝嘅 Nmap 版本同建置資訊。",
            "Check", "查睇", "nmap", "--version", keywords: "version build 版本"),

        ShellPill("nmap.help", "Nmap help", "Nmap 說明",
            "Show the top-level Nmap usage / option reference.", "顯示 Nmap 頂層用法／選項參考。",
            "Show", "顯示", "nmap", "-h", keywords: "help usage options 說明 用法"),

        ActionPill("nmap.zenmap", "Open Zenmap GUI", "開啟 Zenmap 圖形介面",
            "Launch the bundled Zenmap GUI front-end (if installed alongside Nmap).",
            "啟動隨 Nmap 附帶嘅 Zenmap 圖形介面（如有安裝）。",
            "Open", "開啟", _ => Task.FromResult(LaunchZenmap()),
            keywords: "zenmap gui graphical 圖形 介面"),

        // 真正嘅掃描，耗時 · A real scan — long enough to warrant a progress bar.
        ShellPill("nmap.localhost", "Quick scan of localhost", "快速掃描本機",
            "Run a fast scan (-T4 -F) against 127.0.0.1 — a safe smoke test.",
            "對 127.0.0.1 執行快速掃描（-T4 -F）— 安全嘅煙霧測試。",
            "Scan", "掃描", "nmap", "-T4 -F 127.0.0.1", showProgress: true,
            keywords: "localhost test 本機 測試"),

        // 列舉全部 NSE 指令稿，耗時 · Enumerating every NSE script is slow — show a progress bar.
        ShellPill("nmap.scripts", "List NSE scripts", "列出 NSE 指令稿",
            "List the Nmap Scripting Engine scripts available on this machine.",
            "列出呢部機可用嘅 Nmap 指令稿引擎（NSE）指令稿。",
            "List", "列出", "nmap", "--script-help all", showProgress: true,
            keywords: "nse script 指令稿 lua"),

        ShellPill("nmap.iflist", "List interfaces & routes", "列出網絡介面同路由",
            "Show Nmap's view of local network interfaces and routes.", "顯示 Nmap 所見嘅本機網絡介面同路由。",
            "Show", "顯示", "nmap", "--iflist", keywords: "interface route network 介面 路由 網絡"),
    };

    // ======================================================================
    //  Presentation helpers (PRESENTATION + WIRING ONLY — behaviour unchanged)
    //  顯示輔助（只改顯示同接線，行為完全不變）
    // ======================================================================

    /// <summary>
    /// 探測 Nmap 係咪裝咗，回傳彩色狀態藥丸 · A cheap synchronous "installed / not found" pill.
    /// 用 <see cref="NmapService.IsAvailable"/>（快取嘅路徑查找，唔會行 shell）· backed by
    /// <see cref="NmapService.IsAvailable"/>, which is a cached path lookup — no shell launched in the getter.
    /// </summary>
    private static (string, string, StatusColor) NmapStatus()
        => NmapService.IsAvailable()
            ? ("Installed", "已安裝", StatusColor.Good)
            : ("Not found", "搵唔到", StatusColor.Bad);

    /// <summary>
    /// 同 <see cref="Tweak.Shell(string,string,string,string,string,string,string,string,string,bool,bool,RestartScope,string)"/>
    /// 行為一模一樣嘅 shell 動作，額外加上 Nmap 彩色狀態藥丸，可選掃描進度條 ·
    /// A shell action byte-for-byte identical to <see cref="Tweak.Shell"/> (same fileName/arguments run on
    /// click), plus the Nmap status pill and an optional indeterminate progress bar for long scans.
    /// </summary>
    private static TweakDefinition ShellPill(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string fileName, string arguments,
        bool showProgress = false, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Action,
            Keywords = Keys(keywords),
            ActionLabel = new(enBtn, zhBtn),
            RunAsync = ct => ShellRunner.Run(fileName, arguments, false, ct),
            ColoredStatus = NmapStatus,
            ShowProgressBar = showProgress,
        };

    /// <summary>
    /// 同 <see cref="Tweak.Action"/> 行為一模一樣嘅動作，額外加上 Nmap 彩色狀態藥丸 ·
    /// An action byte-for-byte identical to <see cref="Tweak.Action"/>, plus the Nmap status pill.
    /// </summary>
    private static TweakDefinition ActionPill(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, Func<CancellationToken, Task<TweakResult>> run,
        string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Action,
            Keywords = Keys(keywords),
            ActionLabel = new(enBtn, zhBtn),
            RunAsync = run,
            ColoredStatus = NmapStatus,
        };

    /// <summary>搜尋關鍵字切割（同 <see cref="Tweak"/> 內部一致：只切 , 同 ;）· Split keywords exactly as the
    /// Tweak factory does — on ',' and ';' only (NOT spaces), so existing search behaviour is unchanged.</summary>
    private static string[] Keys(string? kw) => string.IsNullOrWhiteSpace(kw)
        ? Array.Empty<string>()
        : kw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static TweakResult LaunchZenmap()
    {
        // Zenmap ships next to nmap.exe as zenmap.exe (or nmap\\zenmap.exe).
        var nmap = NmapService.FindNmap();
        string? zen = null;
        if (nmap is not null)
        {
            var dir = System.IO.Path.GetDirectoryName(nmap);
            if (dir is not null)
            {
                var cand = System.IO.Path.Combine(dir, "zenmap.exe");
                if (System.IO.File.Exists(cand)) zen = cand;
            }
        }
        try
        {
            Process.Start(new ProcessStartInfo(zen ?? "zenmap.exe") { UseShellExecute = true });
            return TweakResult.Ok("Launched Zenmap.", "已啟動 Zenmap。");
        }
        catch
        {
            return TweakResult.Fail("Zenmap not found — it is bundled with the Nmap installer.",
                "搵唔到 Zenmap — 佢隨 Nmap 安裝程式一齊附帶。");
        }
    }
}
