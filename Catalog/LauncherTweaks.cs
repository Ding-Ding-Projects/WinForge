using System;
using System.Collections.Generic;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// 免 UAC 提權啟動器 · No-UAC elevated launcher via Task Scheduler.
/// 一個有「最高權限」嘅排程工作 + 一個捷徑去觸發佢，咁就唔會彈 UAC。
/// A scheduled task with RunLevel=Highest plus a shortcut that triggers it — so launching is elevated
/// with NO UAC prompt. Creating the task needs admin once; after that the shortcut never prompts.
/// </summary>
public static class LauncherTweaks
{
    private const string TaskName = "WinForgeSuiteElevated";

    private static string ExePath => Environment.ProcessPath ?? "WinForge.exe";

    public static IEnumerable<TweakDefinition> All()
    {
        var exe = ExePath.Replace("'", "''");

        // PowerShell: register the highest-privilege task + a desktop & Start-menu shortcut to run it.
        var createScript =
            $"$exe = '{exe}';" +
            "$tn = 'WinForgeSuiteElevated';" +
            "$action = New-ScheduledTaskAction -Execute $exe;" +
            "$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Highest;" +
            "$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero);" +
            "Register-ScheduledTask -TaskName $tn -Action $action -Principal $principal -Settings $settings -Force | Out-Null;" +
            "$ws = New-Object -ComObject WScript.Shell;" +
            "foreach ($dir in @([Environment]::GetFolderPath('Desktop'), (Join-Path $env:AppData 'Microsoft\\Windows\\Start Menu\\Programs'))) {" +
            "  $lnk = $ws.CreateShortcut((Join-Path $dir 'WinForge (Admin).lnk'));" +
            "  $lnk.TargetPath = \"$env:SystemRoot\\System32\\schtasks.exe\";" +
            "  $lnk.Arguments = \"/run /tn $tn\";" +
            "  $lnk.IconLocation = $exe;" +
            "  $lnk.Description = 'Launch WinForge Suite elevated without a UAC prompt';" +
            "  $lnk.Save();" +
            "}" +
            "Write-Output 'Created task WinForgeSuiteElevated and shortcuts (Desktop + Start menu).'";

        var removeScript =
            "Unregister-ScheduledTask -TaskName 'WinForgeSuiteElevated' -Confirm:$false -ErrorAction SilentlyContinue;" +
            "Remove-Item (Join-Path ([Environment]::GetFolderPath('Desktop')) 'WinForge (Admin).lnk') -ErrorAction SilentlyContinue;" +
            "Remove-Item (Join-Path (Join-Path $env:AppData 'Microsoft\\Windows\\Start Menu\\Programs') 'WinForge (Admin).lnk') -ErrorAction SilentlyContinue;" +
            "Write-Output 'Removed the elevated launcher task and shortcuts.'";

        return new List<TweakDefinition>
        {
            Tweak.Powershell("launcher.create", "Create no-UAC elevated launcher", "建立免 UAC 提權啟動器",
                "Register a Task Scheduler task (highest privileges) and a 'WinForge (Admin)' shortcut on the Desktop and Start menu that launches the suite elevated with NO UAC prompt.",
                "註冊一個工作排程器工作（最高權限），同埋喺桌面同開始功能表整一個「WinForge (Admin)」捷徑，撳一下就以管理員身分啟動，唔會彈 UAC。",
                "Set up", "設定", createScript,
                requiresAdmin: true, keywords: "uac,task scheduler,elevate,shortcut,排程,提權,捷徑"),

            Tweak.Shell("launcher.run-now", "Run WinForge elevated now", "立即以管理員運行",
                "Trigger the scheduled task to start a fresh elevated instance — no UAC prompt (requires the launcher to be set up first).",
                "觸發排程工作，開一個全新嘅管理員實例 — 唔彈 UAC（要先設定咗啟動器）。",
                "Run", "運行", "schtasks.exe", "/run /tn WinForgeSuiteElevated",
                keywords: "run,elevated,task,運行,管理員"),

            Tweak.Powershell("launcher.remove", "Remove the elevated launcher", "移除提權啟動器",
                "Delete the scheduled task and the 'WinForge (Admin)' shortcuts.",
                "刪除排程工作同「WinForge (Admin)」捷徑。",
                "Remove", "移除", removeScript,
                requiresAdmin: true, destructive: true, keywords: "remove,uninstall,task,移除"),

            Tweak.Shell("launcher.open-scheduler", "Open Task Scheduler", "開啟工作排程器",
                "Open the Windows Task Scheduler console to inspect the launcher task.",
                "開啟 Windows 工作排程器主控台，睇返個啟動器工作。",
                "Open", "開啟", "mmc.exe", "taskschd.msc",
                keywords: "task scheduler,排程器"),

            Status(),
        };
    }

    /// <summary>
    /// 提權狀態（唯讀資訊 + 彩色狀態藥丸）· Elevation status as a read-only Info row with a coloured pill.
    /// 行為唔變：Id、種類（Info）同 GetInfo 文字一模一樣，淨係加埋一粒綠／灰色狀態藥丸。
    /// Behaviour is unchanged — same Id, same Info kind, same GetInfo text — we only add a green/grey
    /// status pill so the elevation state reads at a glance.
    /// </summary>
    private static TweakDefinition Status() => new()
    {
        Id = "launcher.status",
        Title = new("Elevation status", "提權狀態"),
        Description = new(
            "Whether this WinForge instance is currently running as administrator.",
            "而家呢個 WinForge 實例係咪以管理員身分運行。"),
        Kind = TweakKind.Info,
        GetInfo = () => AdminHelper.IsElevated ? "Elevated · 已提權" : "Standard user · 標準使用者",
        ColoredStatus = () => AdminHelper.IsElevated
            ? ("Elevated", "已提權", StatusColor.Good)
            : ("Standard user", "標準使用者", StatusColor.Neutral),
    };
}
