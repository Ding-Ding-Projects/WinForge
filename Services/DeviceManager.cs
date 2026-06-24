using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個硬件裝置 · One PnP device row.</summary>
public sealed class DeviceInfo
{
    public string Class { get; set; } = "";
    public string? FriendlyName { get; set; }
    public string Status { get; set; } = "";
    public string InstanceId { get; set; } = "";

    public string Display => string.IsNullOrWhiteSpace(FriendlyName) ? InstanceId : FriendlyName!;
    public bool IsOk => Status == "OK";
}

/// <summary>
/// 應用程式內裝置管理（取代 devmgmt.msc）· In-app device management via Get-PnpDevice (no redirect).
/// </summary>
public static class DeviceManager
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static async Task<List<DeviceInfo>> ListAsync(CancellationToken ct = default)
    {
        var json = await ShellRunner.CapturePowershellJson(
            "@(Get-PnpDevice -PresentOnly | Select-Object @{n='Class';e={$_.Class}},FriendlyName,@{n='Status';e={$_.Status.ToString()}},InstanceId | Sort-Object Class,FriendlyName) | ConvertTo-Json -Compress",
            ct);
        try
        {
            var list = JsonSerializer.Deserialize<List<DeviceInfo>>(json, JsonOpts);
            if (list is not null && list.Count > 0) return list;
        }
        catch { /* maybe single */ }
        try
        {
            var one = JsonSerializer.Deserialize<DeviceInfo>(json, JsonOpts);
            if (one is not null) return new List<DeviceInfo> { one };
        }
        catch { /* give up */ }
        return new List<DeviceInfo>();
    }

    private static string Esc(string s) => (s ?? "").Replace("'", "''");

    public static Task<TweakResult> Enable(DeviceInfo d, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Enable-PnpDevice -InstanceId '{Esc(d.InstanceId)}' -Confirm:$false", elevated: false, ct);

    public static Task<TweakResult> Disable(DeviceInfo d, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Disable-PnpDevice -InstanceId '{Esc(d.InstanceId)}' -Confirm:$false", elevated: false, ct);
}
