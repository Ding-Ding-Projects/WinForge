using System.Collections.Generic;
using System.Text;
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
        // tshark 版本（彩色狀態藥丸 + 執行時進度條）· version check with a Wireshark/Npcap readiness pill
        // and an indeterminate progress bar while `tshark -v` runs. Id / Kind / RunAsync unchanged.
        new()
        {
            Id = "ws.version",
            Title = new("tshark version", "tshark 版本"),
            Description = new(
                "Show the installed tshark / Wireshark version and build info.",
                "顯示已安裝嘅 tshark／Wireshark 版本同建置資料。"),
            Kind = TweakKind.Action,
            Keywords = new[] { "version", "tshark", "wireshark", "版本" },
            ActionLabel = new("Check", "查睇"),
            ShowProgressBar = true,
            ColoredStatus = ReadinessPill,
            RunAsync = async _ =>
            {
                var v = await WiresharkService.Version();
                return string.IsNullOrWhiteSpace(v)
                    ? TweakResult.Fail("tshark not found.", "搵唔到 tshark。")
                    : TweakResult.Ok("OK", "OK", v);
            },
        },

        // Npcap 驅動檢查（彩色狀態藥丸）· Npcap driver check with an installed/missing pill. Cheap registry/dir probe.
        new()
        {
            Id = "ws.npcap",
            Title = new("Check Npcap driver", "檢查 Npcap 驅動"),
            Description = new(
                "Check whether the Npcap capture driver/service is installed (needed for live capture).",
                "檢查 Npcap 擷取驅動／服務有冇裝（即時擷取必需）。"),
            Kind = TweakKind.Action,
            Keywords = new[] { "npcap", "driver", "winpcap", "驅動" },
            ActionLabel = new("Check", "檢查"),
            ColoredStatus = () => WiresharkService.IsNpcapInstalled()
                ? ("Npcap installed", "Npcap 已安裝", StatusColor.Good)
                : ("Npcap missing", "冇 Npcap", StatusColor.Bad),
            RunAsync = _ =>
            {
                bool ok = WiresharkService.IsNpcapInstalled();
                return Task.FromResult(ok
                    ? TweakResult.Ok("Npcap is installed.", "Npcap 已安裝。", "Npcap: installed")
                    : TweakResult.Fail("Npcap not detected — reinstall Wireshark and enable Npcap.",
                        "偵測唔到 Npcap — 重裝 Wireshark 並啟用 Npcap。"));
            },
        },

        // 擷取權限檢查（彩色狀態藥丸）· Capture-privilege check with an elevated/standard pill. Cheap token probe.
        new()
        {
            Id = "ws.admin",
            Title = new("Check capture privileges", "檢查擷取權限"),
            Description = new(
                "Capture needs administrator rights (Npcap kernel driver). Check the current elevation state.",
                "擷取需要管理員權限（Npcap 核心驅動）。檢查目前提權狀態。"),
            Kind = TweakKind.Action,
            Keywords = new[] { "admin", "elevation", "privilege", "權限", "管理員" },
            ActionLabel = new("Check", "檢查"),
            ColoredStatus = () => WiresharkService.IsElevated
                ? ("Administrator", "管理員", StatusColor.Good)
                : ("Standard user", "標準使用者", StatusColor.Warn),
            RunAsync = _ =>
            {
                bool ok = WiresharkService.IsElevated;
                return Task.FromResult(ok
                    ? TweakResult.Ok("Running as administrator.", "正以管理員身分運行。", "Elevated: yes")
                    : TweakResult.Fail("Not elevated — relaunch WinForge as administrator to capture.",
                        "未提權 — 以管理員身分重開 WinForge 先可以擷取。"));
            },
        },

        // 列出擷取介面（表格輸出 + 進度條 + 狀態藥丸）· List interfaces as a native sortable table (No / Id /
        // Device / Name columns) instead of a flat text dump, with an indeterminate bar while `dumpcap -D`
        // runs and a readiness pill. The same WiresharkService.Interfaces() drives it; Id and the empty-list
        // failure path are unchanged.
        new()
        {
            Id = "ws.interfaces",
            Title = new("List capture interfaces", "列出擷取介面"),
            Description = new(
                "List the network interfaces available for capture (dumpcap -D).",
                "列出可用嚟擷取嘅網絡介面（dumpcap -D）。"),
            Kind = TweakKind.Action,
            Keywords = new[] { "interfaces", "nic", "list", "adapters", "介面", "網卡" },
            ActionLabel = new("List", "列出"),
            TabularOutput = true,
            ShowProgressBar = true,
            ColoredStatus = ReadinessPill,
            RunAsync = async _ =>
            {
                var ifs = await WiresharkService.Interfaces();
                if (ifs.Count == 0)
                    return TweakResult.Fail("No interfaces — Npcap may be missing or you're not elevated.",
                        "冇介面 — 可能未裝 Npcap 或者未提權。");

                // CSV: header + one row per interface so the card renders a grid (No · Id · Device · Name).
                var sb = new StringBuilder();
                sb.Append("No,Id,Device,Name\r\n");
                int n = 1;
                foreach (var i in ifs)
                    sb.Append(Csv(n++.ToString())).Append(',')
                      .Append(Csv(i.Id)).Append(',')
                      .Append(Csv(i.Device)).Append(',')
                      .Append(Csv(i.FriendlyName)).Append("\r\n");

                return TweakResult.Ok($"{ifs.Count} interface(s).", $"{ifs.Count} 個介面。", sb.ToString());
            },
        },

        Tweak.Action("ws.openfolder", "Open capture folder", "打開擷取資料夾",
            "Open the default capture folder (%TEMP%) where new captures are saved.",
            "打開預設擷取資料夾（%TEMP%），新擷取會儲存喺度。",
            "Open", "打開",
            _ => Task.FromResult(WiresharkService.OpenCaptureFolder()),
            keywords: "folder temp open 資料夾"),
    };

    /// <summary>
    /// 已安裝狀態藥丸 · Cheap synchronous readiness pill for cards that need the CLI tools + Npcap.
    /// 只做檔案／登錄檔存在性檢查，唔會起任何外殼程序，喺 getter 入面行都安全。
    /// File/registry existence checks only — no shell, safe to evaluate in the card's status getter.
    /// </summary>
    private static (string en, string zh, StatusColor color) ReadinessPill()
    {
        if (!WiresharkService.IsInstalled)
            return ("Not installed", "未安裝", StatusColor.Bad);
        return WiresharkService.IsNpcapInstalled()
            ? ("Wireshark + Npcap ready", "Wireshark + Npcap 就緒", StatusColor.Good)
            : ("tshark only — no Npcap", "只有 tshark — 冇 Npcap", StatusColor.Warn);
    }

    /// <summary>把一個欄位轉做 RFC-4180 CSV（需要時加引號）· Quote a CSV field per RFC-4180 when needed.</summary>
    private static string Csv(string s)
    {
        s ??= "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
