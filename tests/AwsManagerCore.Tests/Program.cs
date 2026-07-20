using Amazon.EC2;
using Amazon.EC2.Model;
using WinForge.Models;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("EC2 instance IDs accept legacy and current lengths", ValidInstanceIds);
Run("EC2 instance IDs reject malformed values", InvalidInstanceIds);
Run("EC2 lifecycle policy allows only state-safe actions", LifecyclePolicy);
Run("EC2 lifecycle policy fails closed for unknown states", UnknownStateFailsClosed);
Run("EC2 lifecycle action labels are bilingual", ActionLabelsAreBilingual);
Run("EC2 mapper preserves display and network metadata", MapsInstanceMetadata);
Run("EC2 mapper uses safe defaults for sparse responses", MapsSparseInstance);
Run("EC2 state-change mapper preserves transition", MapsStateChange);
Run("AWS session model exposes no credential values", SessionModelHasNoCredentialValues);
Run("AWS context restart cancels and rejects stale work", ContextRestartRejectsStaleWork);
Run("S3 uploads fail closed on overwrite by default", S3UploadsRequireExplicitOverwrite);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} AWS manager core tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} AWS manager core tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

static void ValidInstanceIds()
{
    Assert(AwsEc2InstancePolicy.IsValidInstanceId("i-1234abcd"), "legacy ID was rejected");
    Assert(AwsEc2InstancePolicy.IsValidInstanceId(" i-0123456789abcdef0 "), "current ID was rejected");
}

static void InvalidInstanceIds()
{
    foreach (var value in new[] { "", "i-123", "i-0123456789abcdef01", "I-0123456789abcdef0", "i-0123456789abcdeg0", "vol-0123456789abcdef0" })
        Assert(!AwsEc2InstancePolicy.IsValidInstanceId(value), $"malformed ID was accepted: {value}");
}

static void LifecyclePolicy()
{
    Allowed("pending", AwsEc2InstanceAction.Terminate);
    Allowed("running", AwsEc2InstanceAction.Stop, AwsEc2InstanceAction.Reboot, AwsEc2InstanceAction.Terminate);
    Allowed("stopping", AwsEc2InstanceAction.Terminate);
    Allowed("stopped", AwsEc2InstanceAction.Start, AwsEc2InstanceAction.Terminate);
    Allowed("shutting-down");
    Allowed("terminated");
}

static void UnknownStateFailsClosed()
{
    foreach (var action in Enum.GetValues<AwsEc2InstanceAction>())
    {
        Assert(!AwsEc2InstancePolicy.IsAllowed(action, null), $"null state allowed {action}");
        Assert(!AwsEc2InstancePolicy.IsAllowed(action, "future-state"), $"unknown state allowed {action}");
    }
}

static void ActionLabelsAreBilingual()
{
    var expected = new Dictionary<AwsEc2InstanceAction, string>
    {
        [AwsEc2InstanceAction.Start] = "啟動",
        [AwsEc2InstanceAction.Stop] = "停止",
        [AwsEc2InstanceAction.Reboot] = "重新啟動",
        [AwsEc2InstanceAction.Terminate] = "終止",
    };
    foreach (var pair in expected)
    {
        var label = AwsEc2InstancePolicy.ActionLabel(pair.Key);
        Assert(!string.IsNullOrWhiteSpace(label.En), $"{pair.Key} English label was blank");
        Equal(pair.Value, label.Zh, $"{pair.Key} Cantonese label");
    }
}

static void MapsInstanceMetadata()
{
    var launched = DateTime.SpecifyKind(new DateTime(2026, 7, 20, 12, 34, 56), DateTimeKind.Utc);
    var source = new Instance
    {
        InstanceId = "i-0123456789abcdef0",
        ImageId = "ami-0123456789abcdef0",
        State = new InstanceState { Name = InstanceStateName.Running },
        InstanceType = InstanceType.T3Micro,
        Architecture = ArchitectureValues.X86_64,
        Placement = new Placement { AvailabilityZone = "ca-central-1a", Tenancy = Tenancy.Default },
        VpcId = "vpc-1234abcd",
        SubnetId = "subnet-1234abcd",
        PrivateIpAddress = "10.0.0.12",
        PublicIpAddress = "203.0.113.12",
        PrivateDnsName = "ip-10-0-0-12.internal",
        PublicDnsName = "ec2-203-0-113-12.example.invalid",
        PlatformDetails = "Linux/UNIX",
        Monitoring = new Monitoring { State = MonitoringState.Enabled },
        KeyName = "operator-key",
        IamInstanceProfile = new IamInstanceProfile { Arn = "arn:aws:iam::123456789012:instance-profile/app" },
        LaunchTime = launched,
        Tags = new List<Tag>
        {
            new() { Key = "Name", Value = "api-prod-1" },
            new() { Key = "Environment", Value = "prod" },
        },
        SecurityGroups = new List<GroupIdentifier>
        {
            new() { GroupId = "sg-1234abcd", GroupName = "api" },
        },
    };

    var mapped = AwsEc2ModelMapper.MapInstance(source);
    Equal("api-prod-1", mapped.Name, "name");
    Equal("running", mapped.State, "state");
    Equal("t3.micro", mapped.InstanceType, "type");
    Equal("ca-central-1a", mapped.AvailabilityZone, "availability zone");
    Equal("10.0.0.12", mapped.PrivateIpAddress, "private IP");
    Equal("203.0.113.12", mapped.PublicIpAddress, "public IP");
    Equal("prod", mapped.Tags["Environment"], "tag");
    Equal("sg-1234abcd", mapped.SecurityGroups.Single().GroupId, "security group");
    Equal("on-demand", mapped.Lifecycle, "default lifecycle");
    Equal(new DateTimeOffset(launched), mapped.LaunchTime, "launch time");
}

static void MapsSparseInstance()
{
    var mapped = AwsEc2ModelMapper.MapInstance(new Instance { InstanceId = "i-1234abcd" });
    Equal("i-1234abcd", mapped.Name, "fallback name");
    Equal("unknown", mapped.State, "fallback state");
    Equal("unknown", mapped.InstanceType, "fallback type");
    Equal("on-demand", mapped.Lifecycle, "fallback lifecycle");
    Equal(0, mapped.Tags.Count, "fallback tags");
    Equal(0, mapped.SecurityGroups.Count, "fallback security groups");
}

static void MapsStateChange()
{
    var mapped = AwsEc2ModelMapper.MapStateChange(new InstanceStateChange
    {
        InstanceId = "i-0123456789abcdef0",
        PreviousState = new InstanceState { Name = InstanceStateName.Stopped },
        CurrentState = new InstanceState { Name = InstanceStateName.Pending },
    }, AwsEc2InstanceAction.Start);
    Equal("stopped", mapped.PreviousState, "previous state");
    Equal("pending", mapped.CurrentState, "current state");
    Equal(AwsEc2InstanceAction.Start, mapped.Action, "action");
}

static void SessionModelHasNoCredentialValues()
{
    var names = typeof(AwsManagerSessionOptions).GetProperties()
        .Select(property => property.Name)
        .ToArray();
    foreach (var forbidden in new[] { "AccessKey", "Secret", "Token", "Password", "Credential" })
        Assert(!names.Any(name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase)),
            $"session model exposed {forbidden}");
}

static void ContextRestartRejectsStaleWork()
{
    using var context = new AwsContextGeneration();
    var first = context.Restart();
    Assert(context.TryGetToken(first, out var oldToken), "initial context did not provide a token");
    var second = context.Restart();
    Assert(oldToken.IsCancellationRequested, "old context token was not cancelled");
    Assert(!context.IsCurrent(first), "old context remained current");
    Assert(!context.TryGetToken(first, out _), "old context returned a live token");
    Assert(context.IsCurrent(second), "new context was not current");
    Assert(context.TryGetToken(second, out var currentToken), "new context did not provide a token");
    context.Invalidate();
    Assert(currentToken.IsCancellationRequested, "invalidation did not cancel the current context");
    Assert(!context.IsCurrent(second), "invalidated context remained current");
}

static void S3UploadsRequireExplicitOverwrite()
{
    var request = new AwsS3UploadRequest
    {
        BucketName = "example-bucket",
        Key = "report.csv",
        LocalFilePath = "report.csv",
    };
    Assert(!request.Overwrite, "upload defaulted to replacing an existing object");
    Equal("*", AwsS3UploadPolicy.IfNoneMatch(request.Overwrite), "no-overwrite request precondition");
    Assert((request with { Overwrite = true }).Overwrite, "explicit overwrite intent was not preserved");
    Equal<string?>(null, AwsS3UploadPolicy.IfNoneMatch(overwrite: true), "explicit overwrite precondition");
}

static void Allowed(string state, params AwsEc2InstanceAction[] expected)
{
    var allowed = expected.ToHashSet();
    foreach (var action in Enum.GetValues<AwsEc2InstanceAction>())
        Equal(allowed.Contains(action), AwsEc2InstancePolicy.IsAllowed(action, state), $"{state}/{action}");
}

static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'");
}
