using System;
using System.Text.RegularExpressions;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Pure EC2 lifecycle policy shared by the managed service, UI, and focused tests. AWS remains the
/// final authority, but WinForge fails closed before presenting an action that is invalid for a known
/// instance state.
/// </summary>
public static partial class AwsEc2InstancePolicy
{
    [GeneratedRegex(@"^i-[0-9a-f]{8}(?:[0-9a-f]{9})?$", RegexOptions.CultureInvariant)]
    private static partial Regex InstanceIdPattern();

    public static bool IsValidInstanceId(string? instanceId)
        => !string.IsNullOrWhiteSpace(instanceId)
           && InstanceIdPattern().IsMatch(instanceId.Trim());

    public static bool IsAllowed(AwsEc2InstanceAction action, string? state)
    {
        var normalized = NormalizeState(state);
        return action switch
        {
            AwsEc2InstanceAction.Start => normalized == "stopped",
            AwsEc2InstanceAction.Stop => normalized == "running",
            AwsEc2InstanceAction.Reboot => normalized == "running",
            AwsEc2InstanceAction.Terminate => normalized is "pending" or "running" or "stopping" or "stopped",
            _ => false,
        };
    }

    public static string NormalizeState(string? state)
        => string.IsNullOrWhiteSpace(state) ? "unknown" : state.Trim().ToLowerInvariant();

    public static AwsManagerText ActionLabel(AwsEc2InstanceAction action) => action switch
    {
        AwsEc2InstanceAction.Start => new AwsManagerText("start", "啟動"),
        AwsEc2InstanceAction.Stop => new AwsManagerText("stop", "停止"),
        AwsEc2InstanceAction.Reboot => new AwsManagerText("reboot", "重新啟動"),
        AwsEc2InstanceAction.Terminate => new AwsManagerText("terminate", "終止"),
        _ => new AwsManagerText("change", "變更"),
    };
}
