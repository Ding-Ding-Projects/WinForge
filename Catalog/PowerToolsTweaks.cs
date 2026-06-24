using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 強力工具（進階公用程式同快速電源動作）· Power Tools: advanced utilities and quick power actions.
/// </summary>
public static class PowerToolsTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 長時間掃描／修復：保留原本指令同管理員需求，淨係加一條不確定進度條畀視覺回饋。
        // Long-running scan/repair: identical command + admin requirement, with an added
        // indeterminate progress bar so the multi-minute run shows visible activity.
        LongCmd("powertools.sfc-scannow", "System File Checker (SFC)", "系統檔案檢查 (SFC)",
            "Scan and repair protected Windows system files.", "掃描同修復受保護嘅 Windows 系統檔案。",
            "Scan now", "即刻掃描", "sfc /scannow",
            requiresAdmin: true, keywords: "sfc,scannow,repair,系統檔案,修復"),

        LongCmd("powertools.dism-restorehealth", "DISM restore health", "DISM 還原健康",
            "Repair the Windows component store image online.", "上網修復 Windows 元件存放區嘅映像。",
            "Restore", "還原", "DISM /Online /Cleanup-Image /RestoreHealth",
            requiresAdmin: true, keywords: "dism,restorehealth,image,映像,修復"),

        LongCmd("powertools.chkdsk-c", "Check system drive", "檢查系統磁碟",
            "Run a read-only check on the C: drive.", "對 C: 磁碟做唯讀檢查。",
            "Check", "檢查", "chkdsk C:",
            requiresAdmin: true, keywords: "chkdsk,disk,磁碟,檢查"),

        // 產生報告期間（數秒）顯示不確定進度條 · Indeterminate progress while the report is generated.
        LongPowershell("powertools.battery-report", "Generate battery report", "產生電池報告",
            "Save a battery health report to your user profile.", "將電池健康報告儲存喺你嘅使用者設定檔。",
            "Generate", "產生", "powercfg /batteryreport /output \"$env:USERPROFILE\\battery-report.html\"",
            keywords: "battery,powercfg,report,電池,報告"),

        LongCmd("powertools.energy-report", "Generate energy report", "產生能源報告",
            "Trace power efficiency issues and save a report.", "追蹤電源效率問題再儲存份報告。",
            "Generate", "產生", "powercfg /energy /output \"%USERPROFILE%\\energy-report.html\"",
            requiresAdmin: true, keywords: "energy,powercfg,report,能源,報告"),

        Tweak.Shell("powertools.msinfo32", "Open System Information", "開啟系統資訊",
            "Launch the Windows System Information tool.", "開啟 Windows 系統資訊工具。",
            "Open", "開啟", "msinfo32.exe", "",
            keywords: "msinfo32,system information,系統資訊"),

        Tweak.Cmd("powertools.reliability-monitor", "Open Reliability Monitor", "開啟可靠性監視器",
            "View the system stability and failure history.", "睇下系統嘅穩定性同故障歷史。",
            "Open", "開啟", "perfmon /rel",
            keywords: "reliability,perfmon,monitor,可靠性,監視器"),

        Tweak.Cmd("powertools.activation-status", "Show Windows activation status", "顯示 Windows 啟用狀態",
            "Display the Windows licensing expiry status.", "顯示 Windows 授權幾時到期。",
            "Show", "顯示", "cscript //nologo C:\\Windows\\System32\\slmgr.vbs /xpr",
            keywords: "activation,slmgr,license,啟用,授權"),

        Tweak.Cmd("powertools.lock-workstation", "Lock the workstation", "鎖定電腦",
            "Lock this PC and return to the sign-in screen.", "鎖住呢部機再返去登入畫面。",
            "Lock", "鎖定", "rundll32.exe user32.dll,LockWorkStation",
            keywords: "lock,workstation,鎖定"),

        Tweak.Cmd("powertools.sleep-now", "Sleep now", "即刻睡眠",
            "Put this PC into sleep immediately.", "即刻令呢部機入睡眠。",
            "Sleep", "睡眠", "rundll32.exe powrprof.dll,SetSuspendState 0,1,0",
            keywords: "sleep,suspend,睡眠"),

        Tweak.Cmd("powertools.sign-out", "Sign out", "登出",
            "Sign out of the current Windows session.", "登出而家嘅 Windows 工作階段。",
            "Sign out", "登出", "shutdown /l",
            destructive: true, keywords: "sign out,logoff,登出"),

        Tweak.Cmd("powertools.restart-now", "Restart now", "即刻重新啟動",
            "Restart this PC after a short delay.", "等陣就重新啟動呢部機。",
            "Restart", "重新啟動", "shutdown /r /t 3",
            destructive: true, keywords: "restart,reboot,重新啟動"),

        Tweak.Cmd("powertools.shutdown-now", "Shut down now", "即刻關機",
            "Shut down this PC after a short delay.", "等陣就熄機。",
            "Shut down", "關機", "shutdown /s /t 3",
            destructive: true, keywords: "shutdown,power off,關機"),

        Tweak.Info("powertools.edit-hosts", "Edit the hosts file", "編輯 hosts 檔案",
            "Now built in — use the in-app Hosts Editor module (no Notepad needed).",
            "已經內建 — 用 app 內嘅 hosts 編輯器模組（唔使記事本）。",
            () => "→ Hosts Editor · hosts 編輯器", "hosts,block,website,封鎖,網站"),
    };

    // ======================================================================
    //  本地工廠：長時間動作 + 不確定進度條 · Local factories: long-running action + indeterminate progress bar.
    //  同 Tweak.Cmd / Tweak.Powershell 完全一樣（相同指令、Id、管理員、重啟範圍），
    //  淨係額外設 ShowProgressBar = true，純粹改善視覺回饋，唔改任何行為。
    //  Identical to Tweak.Cmd / Tweak.Powershell (same command, Id, admin, restart scope);
    //  only ShowProgressBar = true is added — a purely presentational cue, no behaviour change.
    // ======================================================================

    private static TweakDefinition LongAction(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, Func<CancellationToken, Task<TweakResult>> run,
        bool requiresAdmin, string? keywords)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Action,
            RequiresAdmin = requiresAdmin,
            Keywords = string.IsNullOrWhiteSpace(keywords)
                ? Array.Empty<string>()
                : keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            ActionLabel = new(enBtn, zhBtn),
            RunAsync = run,
            ShowProgressBar = true,
        };

    private static TweakDefinition LongCmd(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string command,
        bool requiresAdmin = false, string? keywords = null)
        => LongAction(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => ShellRunner.RunCmd(command, requiresAdmin, ct), requiresAdmin, keywords);

    private static TweakDefinition LongPowershell(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string script,
        bool requiresAdmin = false, string? keywords = null)
        => LongAction(id, enT, zhT, enD, zhD, enBtn, zhBtn,
            ct => ShellRunner.RunPowershell(script, requiresAdmin, ct), requiresAdmin, keywords);
}