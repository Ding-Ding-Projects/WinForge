using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Nmap 操作目錄 · Catalog of one-shot Nmap helper operations rendered as TweakCards — print the
/// version, open the Zenmap GUI, show help/script docs, list NSE scripts, run a quick localhost scan.
/// 由 TweakCard 顯示；快速指令擷取輸出。Quick commands capture output; GUI launches open a window.
/// </summary>
public static class NmapOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Shell("nmap.version", "Nmap version", "Nmap 版本",
            "Print the installed Nmap version and build details.", "印出已安裝嘅 Nmap 版本同建置資訊。",
            "Check", "查睇", "nmap", "--version", keywords: "version build 版本"),

        Tweak.Shell("nmap.help", "Nmap help", "Nmap 說明",
            "Show the top-level Nmap usage / option reference.", "顯示 Nmap 頂層用法／選項參考。",
            "Show", "顯示", "nmap", "-h", keywords: "help usage options 說明 用法"),

        Tweak.Action("nmap.zenmap", "Open Zenmap GUI", "開啟 Zenmap 圖形介面",
            "Launch the bundled Zenmap GUI front-end (if installed alongside Nmap).",
            "啟動隨 Nmap 附帶嘅 Zenmap 圖形介面（如有安裝）。",
            "Open", "開啟", _ => Task.FromResult(LaunchZenmap()),
            keywords: "zenmap gui graphical 圖形 介面"),

        Tweak.Shell("nmap.localhost", "Quick scan of localhost", "快速掃描本機",
            "Run a fast scan (-T4 -F) against 127.0.0.1 — a safe smoke test.",
            "對 127.0.0.1 執行快速掃描（-T4 -F）— 安全嘅煙霧測試。",
            "Scan", "掃描", "nmap", "-T4 -F 127.0.0.1", keywords: "localhost test 本機 測試"),

        Tweak.Shell("nmap.scripts", "List NSE scripts", "列出 NSE 指令稿",
            "List the Nmap Scripting Engine scripts available on this machine.",
            "列出呢部機可用嘅 Nmap 指令稿引擎（NSE）指令稿。",
            "List", "列出", "nmap", "--script-help all", keywords: "nse script 指令稿 lua"),

        Tweak.Shell("nmap.iflist", "List interfaces & routes", "列出網絡介面同路由",
            "Show Nmap's view of local network interfaces and routes.", "顯示 Nmap 所見嘅本機網絡介面同路由。",
            "Show", "顯示", "nmap", "--iflist", keywords: "interface route network 介面 路由 網絡"),
    };

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
