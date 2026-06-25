using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 清理同儲存空間（多數係動作）· Cleanup &amp; storage actions; file-deleting ones are destructive.
/// </summary>
public static class CleanupTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 清空資源回收筒 · Empty Recycle Bin.
        // 同舊版完全一樣嘅行為（Clear-RecycleBin -Force），只係加咗：
        //   • 彩色狀態藥丸顯示可回收空間／項目數（用 SHQueryRecycleBin，即時同步讀取）；
        //   • 執行時嘅不確定進度條。
        // Identical behaviour (Clear-RecycleBin -Force); presentation upgraded with a coloured
        // status pill showing reclaimable space/items (instant, synchronous SHQueryRecycleBin)
        // and an indeterminate progress bar while it runs.
        EmptyRecycleBin(),

        Tweak.Cmd("cleanup.user-temp", "Clear user temp files", "清除使用者暫存檔",
            "Delete the contents of your %TEMP% folder.", "刪除你 %TEMP% 資料夾入面嘅嘢。",
            "Clear", "清除",
            "del /q /f /s \"%TEMP%\\*\" & for /d %x in (\"%TEMP%\\*\") do @rd /s /q \"%x\"",
            destructive: true, keywords: "temp,暫存,temporary"),

        Tweak.Cmd("cleanup.windows-temp", "Clear Windows Temp", "清除 Windows 暫存",
            "Delete files in the C:\\Windows\\Temp folder.", "刪除 C:\\Windows\\Temp 資料夾入面嘅檔案。",
            "Clear", "清除",
            "del /q /f /s C:\\Windows\\Temp\\*",
            requiresAdmin: true, destructive: true, keywords: "temp,暫存,windows"),

        Tweak.Powershell("cleanup.thumbnail-cache", "Clear thumbnail cache", "清除縮圖快取",
            "Remove cached Explorer thumbnails so they rebuild fresh.", "刪除檔案總管嘅縮圖快取，等佢重新整。",
            "Clear", "清除",
            "Remove-Item \"$env:LocalAppData\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\" -Force -ErrorAction SilentlyContinue",
            destructive: true, keywords: "thumbnail,縮圖,thumbcache"),

        // 熄服務 → 刪快取 → 重啟服務，係多步驟兼耗時，所以加上不確定進度條。指令同 requiresAdmin／destructive 完全不變。
        // Stop services → delete cache → restart services is multi-step and slow, so it gets an
        // indeterminate progress bar. The exact command and the admin/destructive flags are unchanged.
        CmdWithProgress("cleanup.windows-update-cache", "Clear Windows Update cache", "清除 Windows Update 快取",
            "Stop update services and delete the SoftwareDistribution download cache.", "熄咗更新服務再刪除 SoftwareDistribution 下載快取。",
            "Clear", "清除",
            "net stop wuauserv & net stop bits & rd /s /q C:\\Windows\\SoftwareDistribution\\Download & net start wuauserv & net start bits",
            requiresAdmin: true, destructive: true, keywords: "update,更新,wuauserv,bits"),

        Tweak.Shell("cleanup.store-cache", "Reset Microsoft Store cache", "重設 Microsoft Store 快取",
            "Clears the Microsoft Store cache without changing settings.", "清除 Microsoft Store 快取，唔會改你嘅設定。",
            "Reset", "重設",
            "wsreset.exe", "",
            keywords: "store,商店,wsreset"),

        Tweak.Cmd("cleanup.prefetch", "Clear Prefetch", "清除 Prefetch",
            "Delete the contents of the Windows Prefetch folder.", "刪除 Windows Prefetch 資料夾入面嘅嘢。",
            "Clear", "清除",
            "del /q /f /s C:\\Windows\\Prefetch\\*",
            requiresAdmin: true, destructive: true, keywords: "prefetch,預取"),

        Tweak.Shell("cleanup.disk-cleanup", "Run Disk Cleanup", "執行磁碟清理",
            "Open the built-in Disk Cleanup tool.", "開啟內建嘅磁碟清理工具。",
            "Open", "開啟",
            "cleanmgr.exe", "",
            keywords: "cleanmgr,磁碟,disk"),

        // 清空 DO 快取可能要掃唔少檔案，加上不確定進度條；PowerShell 指令同 destructive 旗標不變。
        // Purging the DO cache can churn through many files, so it gets an indeterminate progress bar;
        // the PowerShell script and the destructive flag are unchanged.
        PowershellWithProgress("cleanup.delivery-optimization", "Clear delivery optimisation cache", "清除傳遞最佳化快取",
            "Free up space used by the Delivery Optimization cache.", "釋放傳遞最佳化快取用咗嘅空間。",
            "Clear", "清除",
            "Delete-DeliveryOptimizationCache -Force",
            destructive: true, keywords: "delivery,optimization,傳遞,最佳化"),

        Tweak.Powershell("cleanup.event-logs", "Clear Windows event logs", "清除 Windows 事件記錄",
            "Wipe all Windows event logs.", "清除所有 Windows 事件記錄。",
            "Clear", "清除",
            "wevtutil el | ForEach-Object { wevtutil cl $_ }",
            requiresAdmin: true, destructive: true, keywords: "event,log,事件,記錄,wevtutil"),

        Tweak.Cmd("cleanup.empty-clipboard", "Empty clipboard", "清空剪貼簿",
            "Clear whatever text is currently on the clipboard.", "清除而家剪貼簿上面嘅內容。",
            "Empty", "清空",
            "echo off | clip",
            keywords: "clipboard,剪貼簿,clip"),

        Tweak.Cmd("cleanup.storage-sense", "Open Storage Sense settings", "開啟儲存空間感知設定",
            "Open Storage Sense to automate cleanup over time.", "開啟儲存空間感知，自動定期幫你清理。",
            "Open", "開啟",
            "start ms-settings:storagesense",
            keywords: "storage,sense,儲存,空間"),

        // DISM 元件清理通常要跑好幾分鐘，最需要進度條提示；指令同 requiresAdmin 旗標完全不變。
        // DISM component cleanup typically runs for minutes — the strongest case for a progress bar;
        // the exact command and the admin flag are unchanged.
        CmdWithProgress("cleanup.dism-component", "DISM component cleanup", "DISM 元件清理",
            "Reclaim space from superseded Windows component store files.", "回收已經被取代嘅 Windows 元件存放區檔案空間。",
            "Run", "執行",
            "Dism.exe /Online /Cleanup-Image /StartComponentCleanup",
            requiresAdmin: true, keywords: "dism,component,元件,winsxs"),
    };

    // ======================================================================
    //  本地輔助：保留每個 tweak 嘅 Id／指令／旗標完全一致，只加進度條等顯示。
    //  Local helpers: keep each tweak's Id/command/flags identical, adding only
    //  presentation (progress bar, status pill). 跟 PerformanceTweaks 同一風格。
    // ======================================================================

    /// <summary>
    /// 行為同 <see cref="Tweak.Cmd"/> 一模一樣，但喺執行時顯示不確定進度條 ·
    /// Behaves exactly like <see cref="Tweak.Cmd"/> but shows an indeterminate progress bar while running.
    /// </summary>
    private static TweakDefinition CmdWithProgress(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string command,
        bool requiresAdmin = false, bool destructive = false, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Action,
            RequiresAdmin = requiresAdmin,
            Destructive = destructive,
            Keywords = SplitKeywords(keywords),
            ActionLabel = new(enBtn, zhBtn),
            ShowProgressBar = true,
            RunAsync = ct => ShellRunner.RunCmd(command, requiresAdmin, ct),
        };

    /// <summary>
    /// 行為同 <see cref="Tweak.Powershell"/> 一模一樣，但喺執行時顯示不確定進度條 ·
    /// Behaves exactly like <see cref="Tweak.Powershell"/> but shows an indeterminate progress bar while running.
    /// </summary>
    private static TweakDefinition PowershellWithProgress(
        string id, string enT, string zhT, string enD, string zhD,
        string enBtn, string zhBtn, string script,
        bool requiresAdmin = false, bool destructive = false, string? keywords = null)
        => new()
        {
            Id = id,
            Title = new(enT, zhT),
            Description = new(enD, zhD),
            Kind = TweakKind.Action,
            RequiresAdmin = requiresAdmin,
            Destructive = destructive,
            Keywords = SplitKeywords(keywords),
            ActionLabel = new(enBtn, zhBtn),
            ShowProgressBar = true,
            RunAsync = ct => ShellRunner.RunPowershell(script, requiresAdmin, ct),
        };

    /// <summary>
    /// 清空資源回收筒 · Empty Recycle Bin.
    /// 行為同舊版完全一致（Clear-RecycleBin -Force -ErrorAction SilentlyContinue），
    /// 另加：可回收空間／項目數嘅彩色狀態藥丸（SHQueryRecycleBin，同步即時讀取）＋執行時不確定進度條。
    /// Behaviour identical to the original (Clear-RecycleBin -Force -ErrorAction SilentlyContinue);
    /// adds a coloured status pill for reclaimable size/items (synchronous SHQueryRecycleBin) and a
    /// progress bar while emptying.
    /// </summary>
    private static TweakDefinition EmptyRecycleBin()
        => new()
        {
            Id = "cleanup.recycle-bin",
            Title = new("Empty Recycle Bin", "清空資源回收筒"),
            Description = new("Permanently delete everything in the Recycle Bin.",
                "永久刪除資源回收筒入面嘅所有嘢。"),
            Kind = TweakKind.Action,
            Destructive = true,
            Keywords = SplitKeywords("recycle,bin,回收,垃圾"),
            ActionLabel = new("Empty", "清空"),
            ShowProgressBar = true,
            RunAsync = ct => ShellRunner.RunPowershell(
                "Clear-RecycleBin -Force -ErrorAction SilentlyContinue", false, ct),
            ColoredStatus = RecycleBinStatus,
            // Rich: a generated stacked bar of the system drive — used vs reclaimable (bin) vs free.
            // Rebuilds after emptying so you watch the reclaimable slice disappear.
            // 豐富化：系統磁碟嘅堆疊長條圖（已用／可回收／可用）；清空後重畫，睇住可回收嗰格消失。
            VisualLiveUpdate = true,
            VisualBuilder = _ => TweakVisuals.StackedBar(SystemDriveSegments,
                "System drive (C:)", "系統磁碟 (C:)"),
        };

    /// <summary>
    /// 砌系統磁碟嘅長條圖分段：其他已用／回收筒可回收／可用 ·
    /// Build the C: drive bar segments: other-used / recycle-bin reclaimable / free.
    /// </summary>
    private static IReadOnlyList<TweakVisuals.BarSegment> SystemDriveSegments()
    {
        long binBytes = 0;
        try
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            if (SHQueryRecycleBin(null, ref info) == 0 && info.i64Size > 0) binBytes = info.i64Size;
        }
        catch { /* ignore */ }

        long total = 0, free = 0;
        try
        {
            var sys = System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
            var d = new System.IO.DriveInfo(sys);
            if (d.IsReady) { total = d.TotalSize; free = d.TotalFreeSpace; }
        }
        catch { /* ignore */ }

        if (total <= 0) // fall back to a bin-only bar when drive info is unavailable
            return new[]
            {
                new TweakVisuals.BarSegment(Math.Max(binBytes, 1), StatusColor.Warn,
                    $"Reclaimable {FormatBytes(binBytes)}", $"可回收 {FormatBytes(binBytes)}"),
            };

        long used = Math.Max(0, total - free);
        long otherUsed = Math.Max(0, used - binBytes);
        return new[]
        {
            new TweakVisuals.BarSegment(otherUsed, StatusColor.Neutral,
                $"Used {FormatBytes(otherUsed)}", $"已用 {FormatBytes(otherUsed)}"),
            new TweakVisuals.BarSegment(binBytes, StatusColor.Warn,
                $"Bin {FormatBytes(binBytes)}", $"回收筒 {FormatBytes(binBytes)}"),
            new TweakVisuals.BarSegment(free, StatusColor.Good,
                $"Free {FormatBytes(free)}", $"可用 {FormatBytes(free)}"),
        };
    }

    /// <summary>
    /// 用 SHQueryRecycleBin 即時讀取可回收空間／項目數，砌成彩色狀態藥丸 ·
    /// Read reclaimable bytes/items instantly via SHQueryRecycleBin and build a coloured pill.
    /// </summary>
    private static (string en, string zh, StatusColor color) RecycleBinStatus()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        // null pszRootPath = 全部磁碟機嘅回收筒總和 · null root path = all drives' bins summed.
        int hr = SHQueryRecycleBin(null, ref info);
        if (hr != 0)
            return ("Recycle Bin", "資源回收筒", StatusColor.Neutral);

        long bytes = info.i64Size;
        long items = info.i64NumItems;
        if (items <= 0 || bytes <= 0)
            return ("Empty", "已清空", StatusColor.Good);

        string size = FormatBytes(bytes);
        var color = bytes >= 1L * 1024 * 1024 * 1024 ? StatusColor.Warn : StatusColor.Neutral; // ≥1 GB ⇒ caution
        return ($"{size} · {items} item{(items == 1 ? "" : "s")}",
                $"{size} · {items} 個項目", color);
    }

    /// <summary>人類可讀檔案大小 · Human-readable byte size (B/KB/MB/GB).</summary>
    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{(long)v} {units[u]}" : $"{v:0.#} {units[u]}";
    }

    // 同 Tweak 工廠一致嘅關鍵字切割 · Keyword split matching the Tweak factory's behaviour.
    private static string[] SplitKeywords(string? kw) => string.IsNullOrWhiteSpace(kw)
        ? Array.Empty<string>()
        : kw.Split(new[] { ',', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ---- SHQueryRecycleBin P/Invoke (shell32) ----
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);
}