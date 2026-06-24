using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個排程工作 · One scheduled task row.</summary>
public sealed class TaskInfo
{
    public string TaskName { get; set; } = "";
    public string TaskPath { get; set; } = "";
    public string State { get; set; } = "";
    public string? Author { get; set; }

    public string Full => (TaskPath ?? "") + (TaskName ?? "");
    public bool IsDisabled => State == "Disabled";

    /// <summary>Bilingual display of the raw scheduler state · 排程狀態嘅雙語顯示。</summary>
    public string StateText => State switch
    {
        "Ready" => Loc.I.Pick("Ready", "就緒"),
        "Running" => Loc.I.Pick("Running", "執行中"),
        "Disabled" => Loc.I.Pick("Disabled", "已停用"),
        "Queued" => Loc.I.Pick("Queued", "排隊中"),
        "Unknown" => Loc.I.Pick("Unknown", "未知"),
        "" => "",
        _ => State,
    };
}

/// <summary>
/// 應用程式內排程工作管理（取代 taskschd.msc）· In-app Task Scheduler management (no redirect).
/// </summary>
public static class TaskSchedulerManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task<List<TaskInfo>> ListAsync(CancellationToken ct = default)
    {
        var json = await ShellRunner.CapturePowershellJson(
            "@(Get-ScheduledTask | Select-Object TaskName,TaskPath,@{n='State';e={$_.State.ToString()}},@{n='Author';e={$_.Author}} | Sort-Object TaskPath,TaskName) | ConvertTo-Json -Compress",
            ct);
        try
        {
            var list = JsonSerializer.Deserialize<List<TaskInfo>>(json, JsonOpts);
            if (list is not null && list.Count > 0) return list;
        }
        catch { /* maybe single */ }
        try
        {
            var one = JsonSerializer.Deserialize<TaskInfo>(json, JsonOpts);
            if (one is not null) return new List<TaskInfo> { one };
        }
        catch { /* give up */ }
        return new List<TaskInfo>();
    }

    private static string Esc(string s) => (s ?? "").Replace("'", "''");

    private static string Args(TaskInfo t) => $"-TaskName '{Esc(t.TaskName)}' -TaskPath '{Esc(t.TaskPath)}'";

    public static Task<TweakResult> Run(TaskInfo t, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Start-ScheduledTask {Args(t)}", elevated: false, ct);

    public static Task<TweakResult> Stop(TaskInfo t, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Stop-ScheduledTask {Args(t)}", elevated: false, ct);

    public static Task<TweakResult> Enable(TaskInfo t, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Enable-ScheduledTask {Args(t)}", elevated: false, ct);

    public static Task<TweakResult> Disable(TaskInfo t, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Disable-ScheduledTask {Args(t)}", elevated: false, ct);
}
