using System.Collections.Generic;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 系統資訊（唯讀，即時讀取）· Read-only, live system information rows.
/// </summary>
public static class InfoTweaks
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Info("info.os", "Edition", "版本",
            "Windows edition and product name.", "Windows 版本同產品名稱。",
            () => $"{SystemInfo.OsProductName} · {SystemInfo.OsEdition}"),

        Tweak.Info("info.version", "Version & build", "版本與組建",
            "Feature update version and full build number.", "功能更新版本同完整組建編號。",
            () => $"{SystemInfo.OsDisplayVersion} · {SystemInfo.OsBuild}"),

        Tweak.Info("info.cpu", "Processor", "處理器",
            "CPU model as reported by the firmware.", "韌體所報告嘅 CPU 型號。",
            () => SystemInfo.CpuName),

        Tweak.Info("info.cpu-threads", "Logical processors", "邏輯處理器",
            "Logical processor count and OS architecture.", "邏輯處理器數目同作業系統架構。",
            () => $"{SystemInfo.LogicalProcessors} {Loc.I.Pick("threads", "執行緒")} · {SystemInfo.Architecture}"),

        Tweak.Info("info.ram-total", "Installed memory", "已安裝記憶體",
            "Total physical RAM.", "實體記憶體總量。",
            () => SystemInfo.RamTotal),

        Tweak.Info("info.ram-usage", "Memory in use", "記憶體用量",
            "Current memory load.", "目前記憶體負載。",
            () => SystemInfo.RamUsage),

        Tweak.Info("info.gpu", "Graphics adapter", "顯示卡",
            "Primary display adapter.", "主要顯示卡。",
            () => SystemInfo.GpuName),

        Tweak.Info("info.disk", "System drive", "系統磁碟",
            "Free and total space on the Windows drive.", "Windows 磁碟嘅可用同總空間。",
            () => SystemInfo.SystemDrive),

        Tweak.Info("info.uptime", "Uptime", "運行時間",
            "Time since the last boot.", "由上次開機到而家嘅時間。",
            () => SystemInfo.Uptime),

        Tweak.Info("info.boot", "Last boot", "上次開機",
            "Approximate time the system last started.", "系統上次啟動嘅大約時間。",
            () => SystemInfo.BootTime),

        Tweak.Info("info.machine", "Device name", "裝置名稱",
            "Computer name on the network.", "喺網絡上嘅電腦名稱。",
            () => SystemInfo.MachineName),

        Tweak.Info("info.user", "Signed-in user", "登入使用者",
            "Current Windows account.", "目前嘅 Windows 帳戶。",
            () => SystemInfo.UserName),

        Tweak.Info("info.timezone", "Time zone", "時區",
            "Active system time zone.", "目前嘅系統時區。",
            () => SystemInfo.TimeZone),

        Tweak.Info("info.runtime", "App runtime", "應用程式執行階段",
            ".NET runtime hosting WinForge.", "運行 WinForge 嘅 .NET 執行階段。",
            () => SystemInfo.DotNetRuntime),
    };
}
