using System.Collections.Generic;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 封包擷取操作目錄 · Catalog of one-shot Packet Capture operations rendered as TweakCards:
/// version/diagnostic checks, Npcap/admin status, protocol &amp; conversation statistics on the last file,
/// and folder shortcuts. Long-running capture lives in the page's live grid, not here.
/// </summary>
public static class WiresharkOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Action("ws.version", "tshark version", "tshark 版本",
            "Show the installed tshark / Wireshark version and build info.",
            "顯示已安裝嘅 tshark／Wireshark 版本同建置資料。",
            "Check", "查睇",
            async _ =>
            {
                var v = await WiresharkService.Version();
                return string.IsNullOrWhiteSpace(v)
                    ? TweakResult.Fail("tshark not found.", "搵唔到 tshark。")
                    : TweakResult.Ok("OK", "OK", v);
            }, keywords: "version tshark wireshark 版本"),

        Tweak.Action("ws.npcap", "Check Npcap driver", "檢查 Npcap 驅動",
            "Check whether the Npcap capture driver/service is installed (needed for live capture).",
            "檢查 Npcap 擷取驅動／服務有冇裝（即時擷取必需）。",
            "Check", "檢查",
            _ =>
            {
                bool ok = WiresharkService.IsNpcapInstalled();
                return Task.FromResult(ok
                    ? TweakResult.Ok("Npcap is installed.", "Npcap 已安裝。", "Npcap: installed")
                    : TweakResult.Fail("Npcap not detected — reinstall Wireshark and enable Npcap.",
                        "偵測唔到 Npcap — 重裝 Wireshark 並啟用 Npcap。"));
            }, keywords: "npcap driver winpcap 驅動"),

        Tweak.Action("ws.admin", "Check capture privileges", "檢查擷取權限",
            "Capture needs administrator rights (Npcap kernel driver). Check the current elevation state.",
            "擷取需要管理員權限（Npcap 核心驅動）。檢查目前提權狀態。",
            "Check", "檢查",
            _ =>
            {
                bool ok = WiresharkService.IsElevated;
                return Task.FromResult(ok
                    ? TweakResult.Ok("Running as administrator.", "正以管理員身分運行。", "Elevated: yes")
                    : TweakResult.Fail("Not elevated — relaunch WinForge as administrator to capture.",
                        "未提權 — 以管理員身分重開 WinForge 先可以擷取。"));
            }, keywords: "admin elevation privilege 權限 管理員"),

        Tweak.Action("ws.interfaces", "List capture interfaces", "列出擷取介面",
            "List the network interfaces available for capture (dumpcap -D).",
            "列出可用嚟擷取嘅網絡介面（dumpcap -D）。",
            "List", "列出",
            async _ =>
            {
                var ifs = await WiresharkService.Interfaces();
                if (ifs.Count == 0)
                    return TweakResult.Fail("No interfaces — Npcap may be missing or you're not elevated.",
                        "冇介面 — 可能未裝 Npcap 或者未提權。");
                var lines = string.Join("\n", ifs.ConvertAll(i => i.Display));
                return TweakResult.Ok($"{ifs.Count} interface(s).", $"{ifs.Count} 個介面。", lines);
            }, keywords: "interfaces nic list adapters 介面 網卡"),

        Tweak.Action("ws.openfolder", "Open capture folder", "打開擷取資料夾",
            "Open the default capture folder (%TEMP%) where new captures are saved.",
            "打開預設擷取資料夾（%TEMP%），新擷取會儲存喺度。",
            "Open", "打開",
            _ => Task.FromResult(WiresharkService.OpenCaptureFolder()),
            keywords: "folder temp open 資料夾"),
    };
}
