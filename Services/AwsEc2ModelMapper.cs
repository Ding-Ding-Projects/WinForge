using System;
using System.Collections.Generic;
using System.Linq;
using WinForge.Models;
using Ec2Model = Amazon.EC2.Model;

namespace WinForge.Services;

internal static class AwsEc2ModelMapper
{
    internal static AwsEc2Instance MapInstance(Ec2Model.Instance instance)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in instance.Tags ?? new List<Ec2Model.Tag>())
        {
            if (!string.IsNullOrWhiteSpace(tag.Key))
                tags[tag.Key] = tag.Value ?? string.Empty;
        }

        tags.TryGetValue("Name", out var name);
        var groups = (instance.SecurityGroups ?? new List<Ec2Model.GroupIdentifier>())
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupId) || !string.IsNullOrWhiteSpace(group.GroupName))
            .Select(group => new AwsEc2SecurityGroup(group.GroupId ?? string.Empty, group.GroupName ?? string.Empty))
            .ToArray();

        return new AwsEc2Instance(
            instance.InstanceId ?? string.Empty,
            string.IsNullOrWhiteSpace(name) ? instance.InstanceId ?? string.Empty : name,
            AwsEc2InstancePolicy.NormalizeState(instance.State?.Name?.Value),
            instance.InstanceType?.Value ?? "unknown",
            instance.Placement?.AvailabilityZone ?? string.Empty,
            Normalize(instance.VpcId),
            Normalize(instance.SubnetId),
            Normalize(instance.PrivateIpAddress),
            Normalize(instance.PublicIpAddress),
            Normalize(instance.PrivateDnsName),
            Normalize(instance.PublicDnsName),
            instance.ImageId ?? string.Empty,
            instance.Architecture?.Value ?? "unknown",
            Normalize(instance.PlatformDetails) ?? "unknown",
            instance.InstanceLifecycle?.Value ?? "on-demand",
            instance.Monitoring?.State?.Value ?? "unknown",
            instance.Placement?.Tenancy?.Value ?? "unknown",
            Normalize(instance.KeyName),
            Normalize(instance.IamInstanceProfile?.Arn),
            ToOffset(instance.LaunchTime),
            groups,
            tags);
    }

    internal static AwsEc2InstanceStateChange MapStateChange(
        Ec2Model.InstanceStateChange change,
        AwsEc2InstanceAction action)
        => new(
            change.InstanceId ?? string.Empty,
            AwsEc2InstancePolicy.NormalizeState(change.PreviousState?.Name?.Value),
            AwsEc2InstancePolicy.NormalizeState(change.CurrentState?.Name?.Value),
            action);

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTimeOffset? ToOffset(DateTime? value)
    {
        if (value is null) return null;
        var date = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
        return new DateTimeOffset(date);
    }
}
