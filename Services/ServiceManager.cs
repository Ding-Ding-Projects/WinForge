using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個 Windows 服務 · One Windows service row.</summary>
public sealed class ServiceInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string State { get; set; } = "";
    public string StartMode { get; set; } = "";
    public string Status { get; set; } = "";
    public int ProcessId { get; set; }

    public bool IsRunning => State == "Running";
}

/// <summary>
/// 應用程式內服務管理（取代 services.msc）· In-app service management (replaces the services.msc redirect).
/// 用 CIM 攞清單，用 *-Service 指令控制。Lists via CIM, controls via the *-Service cmdlets.
/// </summary>
public static class ServiceManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task<List<ServiceInfo>> ListAsync(CancellationToken ct = default)
    {
        var raw = await ShellRunner.CapturePowershell(
            "@(Get-CimInstance Win32_Service | Select-Object Name,DisplayName,State,StartMode,Status,ProcessId | Sort-Object DisplayName) | ConvertTo-Json -Compress",
            ct);
        var json = ExtractJson(raw);
        try
        {
            var list = JsonSerializer.Deserialize<List<ServiceInfo>>(json, JsonOpts);
            if (list is not null && list.Count > 0) return list;
        }
        catch { /* maybe a single object */ }
        try
        {
            var one = JsonSerializer.Deserialize<ServiceInfo>(json, JsonOpts);
            if (one is not null) return new List<ServiceInfo> { one };
        }
        catch { /* give up */ }
        return new List<ServiceInfo>();
    }

    /// <summary>由可能有雜訊嘅輸出抽返純 JSON · Pull the JSON array/object out of possibly noisy output.</summary>
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

    public static Task<TweakResult> Start(string name, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Start-Service -Name '{name}'", elevated: false, ct);

    public static Task<TweakResult> Stop(string name, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Stop-Service -Name '{name}' -Force", elevated: false, ct);

    public static Task<TweakResult> Restart(string name, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Restart-Service -Name '{name}' -Force", elevated: false, ct);

    /// <summary>mode = Automatic | Manual | Disabled (PowerShell -StartupType values).</summary>
    public static Task<TweakResult> SetStartup(string name, string mode, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Set-Service -Name '{name}' -StartupType {mode}", elevated: false, ct);
}
