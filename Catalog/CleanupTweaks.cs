using System;
using System.Collections.Generic;
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
        Tweak.Powershell("cleanup.recycle-bin", "Empty Recycle Bin", "清空資源回收筒",
            "Permanently delete everything in the Recycle Bin.", "永久刪除資源回收筒入面嘅所有嘢。",
            "Empty", "清空",
            "Clear-RecycleBin -Force -ErrorAction SilentlyContinue",
            destructive: true, keywords: "recycle,bin,回收,垃圾"),

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

        Tweak.Cmd("cleanup.windows-update-cache", "Clear Windows Update cache", "清除 Windows Update 快取",
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

        Tweak.Powershell("cleanup.delivery-optimization", "Clear delivery optimisation cache", "清除傳遞最佳化快取",
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

        Tweak.Cmd("cleanup.dism-component", "DISM component cleanup", "DISM 元件清理",
            "Reclaim space from superseded Windows component store files.", "回收已經被取代嘅 Windows 元件存放區檔案空間。",
            "Run", "執行",
            "Dism.exe /Online /Cleanup-Image /StartComponentCleanup",
            requiresAdmin: true, keywords: "dism,component,元件,winsxs"),
    };
}