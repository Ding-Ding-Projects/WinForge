using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>一條事件記錄 · One event-log entry.</summary>
public sealed class EventRow
{
    public string Time { get; set; } = "";
    public int Id { get; set; }
    public string Level { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Message { get; set; } = "";
}

/// <summary>
/// 應用程式內事件檢視器（取代 eventvwr.msc）· In-app Event Viewer wrapping Get-WinEvent — browse the
/// System / Application / Security / Setup logs with a level filter. Replaces the eventvwr.msc redirect.
/// </summary>
public static class EventLogService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <param name="log">System | Application | Security | Setup</param>
    /// <param name="levels">all | error (Critical+Error) | warn (+Warning) | info</param>
    public static async Task<List<EventRow>> QueryAsync(string log, string levels, int max, CancellationToken ct = default)
    {
        string levelClause = levels switch
        {
            "error" => "; Level=1,2",
            "warn" => "; Level=1,2,3",
            "info" => "; Level=4",
            _ => "",
        };

        string ps =
            "@(Get-WinEvent -FilterHashtable @{LogName='" + log + "'" + levelClause + "} " +
            "-MaxEvents " + max + " -ErrorAction SilentlyContinue | Select-Object " +
            "@{n='Time';e={$_.TimeCreated.ToString('yyyy-MM-dd HH:mm:ss')}},Id," +
            "@{n='Level';e={$_.LevelDisplayName}},@{n='Provider';e={$_.ProviderName}}," +
            "@{n='Message';e={$_.Message}}) | ConvertTo-Json -Compress -Depth 3";

        var raw = await ShellRunner.CapturePowershell(ps, ct);
        var json = ExtractJson(raw);
        try
        {
            var list = JsonSerializer.Deserialize<List<EventRow>>(json, JsonOpts);
            if (list is not null && list.Count > 0) return list;
        }
        catch { /* maybe a single object */ }
        try
        {
            var one = JsonSerializer.Deserialize<EventRow>(json, JsonOpts);
            if (one is not null && (one.Id != 0 || one.Time.Length > 0)) return new List<EventRow> { one };
        }
        catch { /* give up */ }
        return new List<EventRow>();
    }

    private static string ExtractJson(string s)
    {
        if (string.IsNullOrEmpty(s)) return "[]";
        s = s.Trim().TrimStart('﻿');
        int a = s.IndexOf('['), b = s.LastIndexOf(']');
        if (a >= 0 && b > a) return s.Substring(a, b - a + 1);
        int c = s.IndexOf('{'), d = s.LastIndexOf('}');
        if (c >= 0 && d > c) return s.Substring(c, d - c + 1);
        return "[]";
    }
}
