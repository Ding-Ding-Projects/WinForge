using System;
using System.Collections.Generic;

namespace WinForge.Models;

/// <summary>
/// UI-independent English/Cantonese text returned by the managed AWS layer.
/// AWS 管理層回傳嘅英語／廣東話文字；唔依賴任何 UI framework。
/// </summary>
public readonly record struct AwsManagerText(string En, string Zh)
{
    public override string ToString() => $"{En} · {Zh}";
}

public enum AwsManagerErrorKind
{
    None,
    InvalidInput,
    CredentialsUnavailable,
    AuthenticationRequired,
    AccessDenied,
    NotFound,
    Conflict,
    Throttled,
    ServiceUnavailable,
    NotConfigured,
    NotSupported,
    Cancelled,
    Network,
    AwsService,
    LocalIo,
    Unknown,
}

/// <summary>A safe error envelope. It deliberately excludes exception text and credential material.</summary>
public sealed record AwsManagerError(
    AwsManagerErrorKind Kind,
    string Code,
    AwsManagerText Message,
    string? RequestId = null,
    int? HttpStatusCode = null,
    bool Retryable = false);

public sealed record AwsManagerNotice(string Code, AwsManagerText Message);

public sealed record AwsManagerResult(
    bool Success,
    AwsManagerText Message,
    AwsManagerError? Error = null,
    IReadOnlyList<AwsManagerNotice>? Notices = null)
{
    public static AwsManagerResult Ok(string en, string zh, IReadOnlyList<AwsManagerNotice>? notices = null)
        => new(true, new AwsManagerText(en, zh), null, notices);

    public static AwsManagerResult Fail(AwsManagerError error)
        => new(false, error.Message, error);
}

public sealed record AwsManagerResult<T>(
    bool Success,
    T? Value,
    AwsManagerText Message,
    AwsManagerError? Error = null,
    IReadOnlyList<AwsManagerNotice>? Notices = null)
{
    public static AwsManagerResult<T> Ok(
        T value,
        string en,
        string zh,
        IReadOnlyList<AwsManagerNotice>? notices = null)
        => new(true, value, new AwsManagerText(en, zh), null, notices);

    public static AwsManagerResult<T> Fail(AwsManagerError error)
        => new(false, default, error.Message, error);
}

/// <summary>
/// Selects an SDK credential context. No access key, secret key, session token, or SSO token is accepted
/// or persisted by this model; credentials stay in the AWS SDK credential providers.
/// </summary>
public sealed record AwsManagerSessionOptions
{
    public string? ProfileName { get; init; }
    public string? RegionName { get; init; }
    public string? ResourceExplorerViewArn { get; init; }
    public bool UseFipsEndpoint { get; init; }
    public bool UseDualstackEndpoint { get; init; }
}

public sealed record AwsManagerContext(
    string? ProfileName,
    string CredentialSource,
    string RegionName,
    string RegionDisplayName,
    string PartitionName);

public sealed record AwsSharedProfileSummary(
    string Name,
    string? RegionName,
    bool UsesSso,
    bool UsesRole,
    bool UsesCredentialProcess,
    bool HasStaticCredentials);

public sealed record AwsCallerIdentity(string AccountId, string Arn, string UserId, string RegionName);

public enum AwsResourceInventorySource
{
    ResourceExplorer,
    ResourceGroupsTaggingApi,
}

public sealed record AwsResourceProperty(string Name, string JsonValue, DateTimeOffset? LastReportedAt);

public sealed record AwsResourceSummary(
    string Arn,
    string Service,
    string ResourceType,
    string? CloudFormationResourceType,
    string Region,
    string AccountId,
    DateTimeOffset? LastReportedAt,
    IReadOnlyDictionary<string, string> Tags,
    IReadOnlyList<AwsResourceProperty> Properties,
    AwsResourceInventorySource Source);

public sealed record AwsResourceSearchRequest
{
    public string Query { get; init; } = "*";
    public int MaxResults { get; init; } = 100;
    public string? NextToken { get; init; }
    public string? ViewArn { get; init; }
    public bool UseTaggingFallback { get; init; } = true;
    public IReadOnlyList<string> ResourceTypeFilters { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> TagFilters { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
}

public sealed record AwsTaggedResourceRequest
{
    public int MaxResults { get; init; } = 100;
    public string? NextToken { get; init; }
    public IReadOnlyList<string> ResourceTypeFilters { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, IReadOnlyList<string>> TagFilters { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
}

public sealed record AwsResourceSearchPage(
    IReadOnlyList<AwsResourceSummary> Items,
    string? NextToken,
    bool HasMore,
    AwsResourceInventorySource Source,
    string? ViewArn = null);

public sealed record AwsCloudControlListRequest
{
    public required string TypeName { get; init; }
    public string? ResourceModelJson { get; init; }
    public string? RoleArn { get; init; }
    public string? TypeVersionId { get; init; }
    public int MaxResults { get; init; } = 100;
    public string? NextToken { get; init; }
}

public sealed record AwsCloudControlGetRequest
{
    public required string TypeName { get; init; }
    public required string Identifier { get; init; }
    public string? RoleArn { get; init; }
    public string? TypeVersionId { get; init; }
}

public sealed record AwsCloudControlCreateRequest
{
    public required string TypeName { get; init; }
    public required string DesiredStateJson { get; init; }
    public string? ClientToken { get; init; }
    public string? RoleArn { get; init; }
    public string? TypeVersionId { get; init; }
}

public sealed record AwsCloudControlUpdateRequest
{
    public required string TypeName { get; init; }
    public required string Identifier { get; init; }
    public required string PatchDocumentJson { get; init; }
    public string? ClientToken { get; init; }
    public string? RoleArn { get; init; }
    public string? TypeVersionId { get; init; }
}

public sealed record AwsCloudControlDeleteRequest
{
    public required string TypeName { get; init; }
    public required string Identifier { get; init; }
    public string? ClientToken { get; init; }
    public string? RoleArn { get; init; }
    public string? TypeVersionId { get; init; }
}

public sealed record AwsCloudResource(string TypeName, string Identifier, string PropertiesJson);

public sealed record AwsCloudResourcePage(
    string TypeName,
    IReadOnlyList<AwsCloudResource> Items,
    string? NextToken,
    bool HasMore);

public sealed record AwsCloudOperation(
    string RequestToken,
    string Operation,
    string Status,
    string TypeName,
    string? Identifier,
    string? StatusMessage,
    string? ErrorCode,
    string? ResourceModelJson,
    DateTimeOffset? EventTime,
    DateTimeOffset? RetryAfter,
    bool IsTerminal,
    bool Succeeded);

public sealed record AwsCloudOperationWaitOptions
{
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(15);
}

public enum AwsEc2InstanceAction
{
    Start,
    Stop,
    Reboot,
    Terminate,
}

public sealed record AwsEc2ListInstancesRequest
{
    public int MaxResults { get; init; } = 100;
    public string? NextToken { get; init; }
    public IReadOnlyList<string> States { get; init; } = Array.Empty<string>();
}

public sealed record AwsEc2SecurityGroup(string GroupId, string GroupName);

public sealed record AwsEc2Instance(
    string InstanceId,
    string Name,
    string State,
    string InstanceType,
    string AvailabilityZone,
    string? VpcId,
    string? SubnetId,
    string? PrivateIpAddress,
    string? PublicIpAddress,
    string? PrivateDnsName,
    string? PublicDnsName,
    string ImageId,
    string Architecture,
    string PlatformDetails,
    string Lifecycle,
    string MonitoringState,
    string Tenancy,
    string? KeyName,
    string? IamInstanceProfileArn,
    DateTimeOffset? LaunchTime,
    IReadOnlyList<AwsEc2SecurityGroup> SecurityGroups,
    IReadOnlyDictionary<string, string> Tags);

public sealed record AwsEc2InstancePage(
    IReadOnlyList<AwsEc2Instance> Items,
    string? NextToken,
    bool HasMore);

public sealed record AwsEc2InstanceStateChange(
    string InstanceId,
    string PreviousState,
    string CurrentState,
    AwsEc2InstanceAction Action);

public sealed record AwsS3ListBucketsRequest
{
    public int MaxResults { get; init; } = 1000;
    public string? NextToken { get; init; }
    public string? Prefix { get; init; }
    public string? BucketRegion { get; init; }
}

public sealed record AwsS3Bucket(
    string Name,
    DateTimeOffset? CreationDate,
    string? Region,
    string? Arn);

public sealed record AwsS3BucketPage(
    IReadOnlyList<AwsS3Bucket> Items,
    string? NextToken,
    bool HasMore);

public sealed record AwsS3ListObjectsRequest
{
    public required string BucketName { get; init; }
    public string Prefix { get; init; } = "";
    public string Delimiter { get; init; } = "/";
    public int MaxResults { get; init; } = 1000;
    public string? NextToken { get; init; }
    public string? StartAfter { get; init; }
    public bool FetchOwner { get; init; }
}

public sealed record AwsS3Object(
    string BucketName,
    string Key,
    long Size,
    DateTimeOffset? LastModified,
    string? ETag,
    string? StorageClass,
    string? OwnerId,
    string? OwnerName);

public sealed record AwsS3ObjectPage(
    string BucketName,
    string Prefix,
    string Delimiter,
    IReadOnlyList<string> CommonPrefixes,
    IReadOnlyList<AwsS3Object> Objects,
    string? NextToken,
    bool HasMore);

public sealed record AwsS3CreateBucketRequest
{
    public required string BucketName { get; init; }
    public bool EnableObjectLock { get; init; }
}

public sealed record AwsS3UploadRequest
{
    public required string BucketName { get; init; }
    public required string Key { get; init; }
    public required string LocalFilePath { get; init; }
    public string? ContentType { get; init; }
    public string? StorageClass { get; init; }
    public string? ServerSideEncryption { get; init; }
    public string? KmsKeyId { get; init; }
    public bool Overwrite { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed record AwsS3DownloadRequest
{
    public required string BucketName { get; init; }
    public required string Key { get; init; }
    public required string DestinationPath { get; init; }
    public string? VersionId { get; init; }
    public bool Overwrite { get; init; }
}

public sealed record AwsTransferProgress(
    string Operation,
    string BucketName,
    string Key,
    long BytesTransferred,
    long TotalBytes,
    int PercentComplete);

public sealed record AwsS3UploadResult(string BucketName, string Key, string? ETag, string? VersionId, long Size);

public sealed record AwsS3DownloadResult(
    string BucketName,
    string Key,
    string DestinationPath,
    string? ETag,
    string? VersionId,
    long Size);

public sealed record AwsS3ObjectIdentifier(string Key, string? VersionId = null);

public sealed record AwsS3DeleteObjectsResult(
    int RequestedCount,
    int DeletedCount,
    IReadOnlyList<AwsS3DeleteFailure> Failures);

public sealed record AwsS3DeleteFailure(string Key, string? VersionId, string Code, AwsManagerText Message);

public sealed record AwsS3VersioningConfiguration(bool Configured, string Status, bool? MfaDeleteEnabled);

public sealed record AwsS3EncryptionRule(
    string Algorithm,
    string? KmsKeyId,
    bool? BucketKeyEnabled,
    string? BlockedEncryptionType = null);

public sealed record AwsS3EncryptionConfiguration(bool Configured, IReadOnlyList<AwsS3EncryptionRule> Rules);

public sealed record AwsS3PublicAccessBlockConfiguration(
    bool Configured,
    bool BlockPublicAcls,
    bool IgnorePublicAcls,
    bool BlockPublicPolicy,
    bool RestrictPublicBuckets);

public sealed record AwsS3BucketTags(IReadOnlyDictionary<string, string> Tags);

public sealed record AwsS3BucketPolicy(bool Configured, string? PolicyJson);

public sealed record AwsS3LifecycleTransition(
    int? Days,
    DateTimeOffset? Date,
    string StorageClass,
    bool AppliesToNoncurrentVersions = false,
    int? NewerNoncurrentVersions = null);

public sealed record AwsS3LifecycleExpiration(
    int? Days,
    DateTimeOffset? Date,
    bool? ExpiredObjectDeleteMarker,
    bool AppliesToNoncurrentVersions = false,
    int? NewerNoncurrentVersions = null);

public sealed record AwsS3LifecycleFilter(
    string? Prefix,
    IReadOnlyDictionary<string, string> Tags,
    long? ObjectSizeGreaterThan,
    long? ObjectSizeLessThan);

public sealed record AwsS3LifecycleRule(
    string Id,
    bool Enabled,
    AwsS3LifecycleFilter Filter,
    AwsS3LifecycleExpiration? Expiration,
    IReadOnlyList<AwsS3LifecycleTransition> Transitions,
    AwsS3LifecycleExpiration? NoncurrentVersionExpiration,
    IReadOnlyList<AwsS3LifecycleTransition> NoncurrentVersionTransitions,
    int? AbortIncompleteMultipartUploadAfterDays);

public sealed record AwsS3LifecycleConfiguration(
    bool Configured,
    IReadOnlyList<AwsS3LifecycleRule> Rules);

public sealed record AwsS3CorsRule(
    string? Id,
    IReadOnlyList<string> AllowedOrigins,
    IReadOnlyList<string> AllowedMethods,
    IReadOnlyList<string> AllowedHeaders,
    IReadOnlyList<string> ExposeHeaders,
    int? MaxAgeSeconds);

public sealed record AwsS3CorsConfiguration(bool Configured, IReadOnlyList<AwsS3CorsRule> Rules);
