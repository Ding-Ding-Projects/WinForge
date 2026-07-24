using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CloudControlApi;
using Amazon.EC2;
using Amazon.ResourceExplorer2;
using Amazon.ResourceGroupsTaggingAPI;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.Runtime.Credentials;
using Amazon.Runtime.Documents;
using Amazon.S3;
using Amazon.SecurityToken;
using WinForge.Models;
using CloudModel = Amazon.CloudControlApi.Model;
using Ec2Model = Amazon.EC2.Model;
using ExplorerModel = Amazon.ResourceExplorer2.Model;
using S3Model = Amazon.S3.Model;
using StsModel = Amazon.SecurityToken.Model;
using TaggingModel = Amazon.ResourceGroupsTaggingAPI.Model;

namespace WinForge.Services;

/// <summary>
/// A per-instance, pure-managed AWS session used by the Console-style manager. Credentials are
/// obtained from AWS SDK providers and are never copied into a DTO, setting, log, or exception result.
/// </summary>
public sealed class AwsManagerService : IDisposable
{
    private const int MaxExplorerPageSize = 1_000;
    private const int MaxEc2PageSize = 1_000;
    private const int MaxTaggingPageSize = 100;
    private const int MaxS3ObjectPageSize = 1_000;
    private const int MaxS3DeleteBatchSize = 1_000;
    private const string TaggingContinuationPrefix = "winforge-tagging:";

    private static readonly Regex AccessKeyRegex = new(
        @"\b(?:AKIA|ASIA)[A-Z0-9]{12,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SecretAssignmentRegex = new(
        @"(?i)\b(secret(?:access)?key|sessiontoken|password|credential|authorization|apikey)\b\s*[:=]\s*[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AwsManagerSessionOptions _options;
    private readonly AmazonSecurityTokenServiceClient? _sts;
    private readonly AmazonResourceExplorer2Client? _resourceExplorer;
    private readonly AmazonResourceGroupsTaggingAPIClient? _tagging;
    private readonly AmazonCloudControlApiClient? _cloudControl;
    private readonly AmazonEC2Client? _ec2;
    private readonly AmazonS3Client? _s3;
    private readonly AwsManagerError? _initializationError;
    private bool _disposed;

    public AwsManagerContext Context { get; }
    public string RegionName => Context.RegionName;
    public string? ProfileName => Context.ProfileName;

    public AwsManagerService(AwsManagerSessionOptions? options = null)
    {
        _options = options ?? new AwsManagerSessionOptions();

        var requestedProfile = Normalize(_options.ProfileName);
        var region = ResolveRegionWithoutCredentials(_options, requestedProfile);
        var credentialSource = requestedProfile is null ? "AWS default credential chain" : $"Shared profile: {requestedProfile}";

        Context = new AwsManagerContext(
            requestedProfile,
            credentialSource,
            region.SystemName,
            region.DisplayName,
            region.PartitionName);

        try
        {
            var credentials = ResolveCredentials(requestedProfile, region);
            _sts = new AmazonSecurityTokenServiceClient(credentials, new AmazonSecurityTokenServiceConfig
            {
                RegionEndpoint = region,
                UseFIPSEndpoint = _options.UseFipsEndpoint,
                UseDualstackEndpoint = _options.UseDualstackEndpoint,
            });
            _resourceExplorer = new AmazonResourceExplorer2Client(credentials, new AmazonResourceExplorer2Config
            {
                RegionEndpoint = region,
                UseFIPSEndpoint = _options.UseFipsEndpoint,
                UseDualstackEndpoint = _options.UseDualstackEndpoint,
            });
            _tagging = new AmazonResourceGroupsTaggingAPIClient(credentials, new AmazonResourceGroupsTaggingAPIConfig
            {
                RegionEndpoint = region,
                UseFIPSEndpoint = _options.UseFipsEndpoint,
                UseDualstackEndpoint = _options.UseDualstackEndpoint,
            });
            _cloudControl = new AmazonCloudControlApiClient(credentials, new AmazonCloudControlApiConfig
            {
                RegionEndpoint = region,
                UseFIPSEndpoint = _options.UseFipsEndpoint,
                UseDualstackEndpoint = _options.UseDualstackEndpoint,
            });
            _ec2 = new AmazonEC2Client(credentials, new AmazonEC2Config
            {
                RegionEndpoint = region,
                UseFIPSEndpoint = _options.UseFipsEndpoint,
                UseDualstackEndpoint = _options.UseDualstackEndpoint,
            });
            _s3 = new AmazonS3Client(credentials, new AmazonS3Config
            {
                RegionEndpoint = region,
                UseFIPSEndpoint = _options.UseFipsEndpoint,
                UseDualstackEndpoint = _options.UseDualstackEndpoint,
            });
        }
        catch
        {
            _initializationError = new AwsManagerError(
                AwsManagerErrorKind.CredentialsUnavailable,
                "CredentialsUnavailable",
                new AwsManagerText(
                    requestedProfile is null
                        ? "AWS credentials are unavailable. Configure a shared profile, SSO session, environment credentials, or an AWS workload role."
                        : $"The shared AWS profile '{requestedProfile}' could not provide credentials. Sign in again if it uses IAM Identity Center.",
                    requestedProfile is null
                        ? "搵唔到 AWS 憑證。請設定 shared profile、SSO session、環境憑證或者 AWS workload role。"
                        : $"AWS shared profile「{requestedProfile}」攞唔到憑證；如果佢用 IAM Identity Center，請重新登入。"));
        }
    }

    /// <summary>Lists profile metadata only; access keys, secrets, and cached SSO tokens never leave the SDK.</summary>
    public static IReadOnlyList<AwsSharedProfileSummary> ListSharedProfiles()
    {
        try
        {
            var chain = new CredentialProfileStoreChain();
            return chain.ListProfiles()
                .Select(profile => new AwsSharedProfileSummary(
                    profile.Name,
                    profile.Region?.SystemName,
                    !string.IsNullOrWhiteSpace(profile.Options.SsoSession)
                        || !string.IsNullOrWhiteSpace(profile.Options.SsoStartUrl),
                    !string.IsNullOrWhiteSpace(profile.Options.RoleArn),
                    !string.IsNullOrWhiteSpace(profile.Options.CredentialProcess),
                    !string.IsNullOrWhiteSpace(profile.Options.AccessKey)
                        && !string.IsNullOrWhiteSpace(profile.Options.SecretKey)))
                .OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<AwsSharedProfileSummary>();
        }
    }

    public async Task<AwsManagerResult<AwsCallerIdentity>> GetCallerIdentityAsync(
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsCallerIdentity>(out var unavailable))
            return unavailable;

        try
        {
            var response = await _sts!.GetCallerIdentityAsync(
                new StsModel.GetCallerIdentityRequest(), cancellationToken).ConfigureAwait(false);
            var identity = new AwsCallerIdentity(
                response.Account ?? "",
                response.Arn ?? "",
                response.UserId ?? "",
                RegionName);
            return AwsManagerResult<AwsCallerIdentity>.Ok(
                identity,
                "AWS identity verified.",
                "已驗證 AWS 身份。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsCallerIdentity>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsResourceSearchPage>> SearchResourcesAsync(
        AwsResourceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsResourceSearchPage>(out var unavailable))
            return unavailable;

        var query = string.IsNullOrWhiteSpace(request.Query) ? "*" : request.Query.Trim();
        var maxResults = Math.Clamp(request.MaxResults, 1, MaxExplorerPageSize);
        var viewArn = Normalize(request.ViewArn) ?? Normalize(_options.ResourceExplorerViewArn);
        if (TryDecodeTaggingContinuationToken(request.NextToken, out var taggingToken))
            return await SearchTaggingFallbackAsync(
                request, query, maxResults, taggingToken, cancellationToken).ConfigureAwait(false);

        try
        {
            var response = await _resourceExplorer!.SearchAsync(new ExplorerModel.SearchRequest
            {
                QueryString = query,
                MaxResults = maxResults,
                NextToken = Normalize(request.NextToken),
                ViewArn = viewArn,
            }, cancellationToken).ConfigureAwait(false);

            var items = (response.Resources ?? new List<ExplorerModel.Resource>())
                .Select(MapExplorerResource)
                .Where(resource => MatchesResourceTypes(resource, request.ResourceTypeFilters))
                .Where(resource => MatchesTags(resource.Tags, request.TagFilters))
                .ToArray();
            var nextToken = Normalize(response.NextToken);
            return AwsManagerResult<AwsResourceSearchPage>.Ok(
                new AwsResourceSearchPage(
                    items,
                    nextToken,
                    nextToken is not null,
                    AwsResourceInventorySource.ResourceExplorer,
                    response.ViewArn),
                $"Found {items.Length} resource(s) with AWS Resource Explorer.",
                $"AWS Resource Explorer 搵到 {items.Length} 項資源。");
        }
        catch (Exception ex) when (request.UseTaggingFallback && IsResourceExplorerCapabilityFailure(ex))
        {
            var fallback = await SearchTaggingFallbackAsync(
                request, query, maxResults, null, cancellationToken).ConfigureAwait(false);
            if (!fallback.Success)
                return AwsManagerResult<AwsResourceSearchPage>.Fail(ToResourceExplorerError(ex));
            return fallback;
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsResourceSearchPage>.Fail(ToResourceExplorerError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsResourceSearchPage>> ListTaggedResourcesAsync(
        AwsTaggedResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsResourceSearchPage>(out var unavailable))
            return unavailable;

        try
        {
            var response = await _tagging!.GetResourcesAsync(new TaggingModel.GetResourcesRequest
            {
                PaginationToken = Normalize(request.NextToken),
                ResourcesPerPage = Math.Clamp(request.MaxResults, 1, MaxTaggingPageSize),
                ResourceTypeFilters = request.ResourceTypeFilters
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .ToList(),
                TagFilters = request.TagFilters
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                    .Select(pair => new TaggingModel.TagFilter
                    {
                        Key = pair.Key,
                        Values = pair.Value
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .ToList(),
                    })
                    .ToList(),
            }, cancellationToken).ConfigureAwait(false);

            var items = (response.ResourceTagMappingList ?? new List<TaggingModel.ResourceTagMapping>())
                .Select(MapTaggedResource)
                .ToArray();
            var nextToken = Normalize(response.PaginationToken);
            return AwsManagerResult<AwsResourceSearchPage>.Ok(
                new AwsResourceSearchPage(
                    items,
                    nextToken,
                    nextToken is not null,
                    AwsResourceInventorySource.ResourceGroupsTaggingApi),
                $"Loaded {items.Length} tagged resource(s).",
                $"已載入 {items.Length} 項有標籤資源。",
                new[]
                {
                    new AwsManagerNotice(
                        "TaggingInventoryScope",
                        new AwsManagerText(
                            "The Tagging API inventory excludes untagged resources that have never had tags and services that do not support this API.",
                            "Tagging API inventory 唔包括從未有標籤嘅資源，同埋唔支援呢個 API 嘅服務。")),
                });
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsResourceSearchPage>.Fail(ToSafeError(ex));
        }
    }

    private async Task<AwsManagerResult<AwsResourceSearchPage>> SearchTaggingFallbackAsync(
        AwsResourceSearchRequest request,
        string query,
        int maxResults,
        string? taggingToken,
        CancellationToken cancellationToken)
    {
        var fallback = await ListTaggedResourcesAsync(new AwsTaggedResourceRequest
        {
            MaxResults = Math.Min(maxResults, MaxTaggingPageSize),
            NextToken = taggingToken,
            ResourceTypeFilters = request.ResourceTypeFilters,
            TagFilters = request.TagFilters,
        }, cancellationToken).ConfigureAwait(false);
        if (!fallback.Success || fallback.Value is null)
            return fallback;

        var filtered = fallback.Value.Items
            .Where(resource => MatchesFreeText(resource, query))
            .ToArray();
        var wrappedToken = EncodeTaggingContinuationToken(fallback.Value.NextToken);
        var page = fallback.Value with
        {
            Items = filtered,
            NextToken = wrappedToken,
            HasMore = wrappedToken is not null,
        };
        return AwsManagerResult<AwsResourceSearchPage>.Ok(
            page,
            $"Showing {filtered.Length} fallback inventory resource(s).",
            $"後備 inventory 顯示 {filtered.Length} 項資源。",
            new[]
            {
                new AwsManagerNotice(
                    "TaggingInventoryFallback",
                    new AwsManagerText(
                        "Resource Explorer is unavailable or not configured in this Region. Showing the Resource Groups Tagging API inventory, which covers tagged or previously tagged supported resources only.",
                        "呢個 Region 嘅 Resource Explorer 未設定或者用唔到；而家改用 Resource Groups Tagging API，只會涵蓋支援而且有標籤或曾經有標籤嘅資源。")),
            });
    }

    public async Task<AwsManagerResult<AwsCloudResourcePage>> ListCloudResourcesAsync(
        AwsCloudControlListRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsCloudResourcePage>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(request.TypeName))
            return Invalid<AwsCloudResourcePage>("A CloudFormation type name is required.", "需要 CloudFormation 類型名稱。");
        if (!string.IsNullOrWhiteSpace(request.ResourceModelJson)
            && !IsJsonKind(request.ResourceModelJson, JsonValueKind.Object))
            return Invalid<AwsCloudResourcePage>("The resource model filter must be a JSON object.", "資源 model 篩選必須係 JSON object。");

        try
        {
            var response = await _cloudControl!.ListResourcesAsync(new CloudModel.ListResourcesRequest
            {
                TypeName = request.TypeName.Trim(),
                TypeVersionId = Normalize(request.TypeVersionId),
                RoleArn = Normalize(request.RoleArn),
                ResourceModel = Normalize(request.ResourceModelJson),
                MaxResults = Math.Clamp(request.MaxResults, 1, 100),
                NextToken = Normalize(request.NextToken),
            }, cancellationToken).ConfigureAwait(false);
            var typeName = response.TypeName ?? request.TypeName.Trim();
            var items = (response.ResourceDescriptions ?? new List<CloudModel.ResourceDescription>())
                .Select(resource => MapCloudResource(typeName, resource))
                .ToArray();
            var nextToken = Normalize(response.NextToken);
            return AwsManagerResult<AwsCloudResourcePage>.Ok(
                new AwsCloudResourcePage(typeName, items, nextToken, nextToken is not null),
                $"Loaded {items.Length} Cloud Control resource(s).",
                $"已載入 {items.Length} 項 Cloud Control 資源。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsCloudResourcePage>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsCloudResource>> GetCloudResourceAsync(
        AwsCloudControlGetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsCloudResource>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(request.TypeName) || string.IsNullOrWhiteSpace(request.Identifier))
            return Invalid<AwsCloudResource>("A type name and resource identifier are required.", "需要類型名稱同資源 identifier。");

        try
        {
            var response = await _cloudControl!.GetResourceAsync(new CloudModel.GetResourceRequest
            {
                TypeName = request.TypeName.Trim(),
                Identifier = request.Identifier.Trim(),
                TypeVersionId = Normalize(request.TypeVersionId),
                RoleArn = Normalize(request.RoleArn),
            }, cancellationToken).ConfigureAwait(false);
            var resource = MapCloudResource(
                response.TypeName ?? request.TypeName.Trim(),
                response.ResourceDescription ?? new CloudModel.ResourceDescription
                {
                    Identifier = request.Identifier.Trim(),
                    Properties = "{}",
                });
            return AwsManagerResult<AwsCloudResource>.Ok(resource, "Resource loaded.", "已載入資源。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsCloudResource>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsCloudOperation>> CreateCloudResourceAsync(
        AwsCloudControlCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsCloudOperation>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(request.TypeName))
            return Invalid<AwsCloudOperation>("A CloudFormation type name is required.", "需要 CloudFormation 類型名稱。");
        if (!IsJsonKind(request.DesiredStateJson, JsonValueKind.Object))
            return Invalid<AwsCloudOperation>("Desired state must be a JSON object.", "Desired state 必須係 JSON object。");

        try
        {
            var response = await _cloudControl!.CreateResourceAsync(new CloudModel.CreateResourceRequest
            {
                TypeName = request.TypeName.Trim(),
                DesiredState = request.DesiredStateJson,
                ClientToken = Normalize(request.ClientToken) ?? Guid.NewGuid().ToString("N"),
                RoleArn = Normalize(request.RoleArn),
                TypeVersionId = Normalize(request.TypeVersionId),
            }, cancellationToken).ConfigureAwait(false);
            return OperationAccepted(response.ProgressEvent, "Create request accepted.", "已接受建立要求。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsCloudOperation>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsCloudOperation>> UpdateCloudResourceAsync(
        AwsCloudControlUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsCloudOperation>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(request.TypeName) || string.IsNullOrWhiteSpace(request.Identifier))
            return Invalid<AwsCloudOperation>("A type name and resource identifier are required.", "需要類型名稱同資源 identifier。");
        if (!IsJsonKind(request.PatchDocumentJson, JsonValueKind.Array))
            return Invalid<AwsCloudOperation>("The update patch must be an RFC 6902 JSON array.", "更新 patch 必須係 RFC 6902 JSON array。");

        try
        {
            var response = await _cloudControl!.UpdateResourceAsync(new CloudModel.UpdateResourceRequest
            {
                TypeName = request.TypeName.Trim(),
                Identifier = request.Identifier.Trim(),
                PatchDocument = request.PatchDocumentJson,
                ClientToken = Normalize(request.ClientToken) ?? Guid.NewGuid().ToString("N"),
                RoleArn = Normalize(request.RoleArn),
                TypeVersionId = Normalize(request.TypeVersionId),
            }, cancellationToken).ConfigureAwait(false);
            return OperationAccepted(response.ProgressEvent, "Update request accepted.", "已接受更新要求。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsCloudOperation>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsCloudOperation>> DeleteCloudResourceAsync(
        AwsCloudControlDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsCloudOperation>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(request.TypeName) || string.IsNullOrWhiteSpace(request.Identifier))
            return Invalid<AwsCloudOperation>("A type name and resource identifier are required.", "需要類型名稱同資源 identifier。");

        try
        {
            var response = await _cloudControl!.DeleteResourceAsync(new CloudModel.DeleteResourceRequest
            {
                TypeName = request.TypeName.Trim(),
                Identifier = request.Identifier.Trim(),
                ClientToken = Normalize(request.ClientToken) ?? Guid.NewGuid().ToString("N"),
                RoleArn = Normalize(request.RoleArn),
                TypeVersionId = Normalize(request.TypeVersionId),
            }, cancellationToken).ConfigureAwait(false);
            return OperationAccepted(response.ProgressEvent, "Delete request accepted.", "已接受刪除要求。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsCloudOperation>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsCloudOperation>> GetCloudOperationAsync(
        string requestToken,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsCloudOperation>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(requestToken))
            return Invalid<AwsCloudOperation>("A Cloud Control request token is required.", "需要 Cloud Control request token。");

        try
        {
            var response = await _cloudControl!.GetResourceRequestStatusAsync(
                new CloudModel.GetResourceRequestStatusRequest { RequestToken = requestToken.Trim() },
                cancellationToken).ConfigureAwait(false);
            var operation = MapCloudOperation(response.ProgressEvent);
            return AwsManagerResult<AwsCloudOperation>.Ok(
                operation,
                $"Operation status: {operation.Status}.",
                $"操作狀態：{operation.Status}。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsCloudOperation>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsCloudOperation>> WaitForCloudOperationAsync(
        string requestToken,
        AwsCloudOperationWaitOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new AwsCloudOperationWaitOptions();
        if (string.IsNullOrWhiteSpace(requestToken))
            return Invalid<AwsCloudOperation>("A Cloud Control request token is required.", "需要 Cloud Control request token。");
        if (options.Timeout <= TimeSpan.Zero || options.PollInterval <= TimeSpan.Zero)
            return Invalid<AwsCloudOperation>("Polling interval and timeout must be positive.", "Polling 間距同 timeout 必須大過零。");

        var stopwatch = Stopwatch.StartNew();
        try
        {
            while (stopwatch.Elapsed < options.Timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await GetCloudOperationAsync(requestToken, cancellationToken).ConfigureAwait(false);
                if (!result.Success || result.Value is null)
                    return result;
                if (result.Value.IsTerminal)
                {
                    if (result.Value.Succeeded)
                        return AwsManagerResult<AwsCloudOperation>.Ok(
                            result.Value,
                            "Cloud Control operation completed successfully.",
                            "Cloud Control 操作已成功完成。");

                    var error = new AwsManagerError(
                        AwsManagerErrorKind.AwsService,
                        string.IsNullOrWhiteSpace(result.Value.ErrorCode) ? "CloudControlOperationFailed" : result.Value.ErrorCode!,
                        new AwsManagerText(
                            "The Cloud Control resource operation failed. Review the safe status and resource inputs.",
                            "Cloud Control 資源操作失敗；請檢查安全狀態同資源輸入。"));
                    return new AwsManagerResult<AwsCloudOperation>(false, result.Value, error.Message, error);
                }

                var delay = options.PollInterval;
                if (result.Value.RetryAfter is { } retryAfter)
                {
                    var serverDelay = retryAfter - DateTimeOffset.UtcNow;
                    if (serverDelay > delay)
                        delay = serverDelay;
                }
                if (delay > TimeSpan.FromSeconds(30))
                    delay = TimeSpan.FromSeconds(30);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex)
        {
            return AwsManagerResult<AwsCloudOperation>.Fail(ToSafeError(ex));
        }

        var timeoutError = new AwsManagerError(
            AwsManagerErrorKind.ServiceUnavailable,
            "CloudControlWaitTimeout",
            new AwsManagerText(
                "The Cloud Control operation is still running after the selected wait timeout.",
                "等到指定 timeout，Cloud Control 操作仍然進行緊。"),
            Retryable: true);
        return AwsManagerResult<AwsCloudOperation>.Fail(timeoutError);
    }

    public async Task<AwsManagerResult<AwsEc2InstancePage>> ListEc2InstancesAsync(
        AwsEc2ListInstancesRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new AwsEc2ListInstancesRequest();
        if (TryGetUnavailable<AwsEc2InstancePage>(out var unavailable))
            return unavailable;

        var states = (request.States ?? Array.Empty<string>())
            .Select(AwsEc2InstancePolicy.NormalizeState)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var knownStates = new HashSet<string>(
            new[] { "pending", "running", "shutting-down", "terminated", "stopping", "stopped" },
            StringComparer.Ordinal);
        if (states.Any(state => !knownStates.Contains(state)))
            return Invalid<AwsEc2InstancePage>(
                "One or more EC2 instance-state filters are invalid.",
                "一個或以上 EC2 執行個體狀態篩選無效。");

        try
        {
            var filters = new List<Ec2Model.Filter>();
            if (states.Length > 0)
            {
                filters.Add(new Ec2Model.Filter
                {
                    Name = "instance-state-name",
                    Values = states.ToList(),
                });
            }

            var response = await _ec2!.DescribeInstancesAsync(new Ec2Model.DescribeInstancesRequest
            {
                MaxResults = Math.Clamp(request.MaxResults, 5, MaxEc2PageSize),
                NextToken = Normalize(request.NextToken),
                Filters = filters,
            }, cancellationToken).ConfigureAwait(false);
            var instances = (response.Reservations ?? new List<Ec2Model.Reservation>())
                .SelectMany(reservation => reservation.Instances ?? new List<Ec2Model.Instance>())
                .Select(AwsEc2ModelMapper.MapInstance)
                .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(instance => instance.InstanceId, StringComparer.Ordinal)
                .ToArray();
            var nextToken = Normalize(response.NextToken);
            return AwsManagerResult<AwsEc2InstancePage>.Ok(
                new AwsEc2InstancePage(instances, nextToken, nextToken is not null),
                $"Loaded {instances.Length} EC2 instance(s) from {RegionName}.",
                $"已由 {RegionName} 載入 {instances.Length} 個 EC2 執行個體。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsEc2InstancePage>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsEc2InstanceStateChange>> ChangeEc2InstanceStateAsync(
        string instanceId,
        AwsEc2InstanceAction action,
        string currentState,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsEc2InstanceStateChange>(out var unavailable))
            return unavailable;
        if (!AwsEc2InstancePolicy.IsValidInstanceId(instanceId))
            return Invalid<AwsEc2InstanceStateChange>(
                "A valid EC2 instance ID is required.",
                "需要有效嘅 EC2 執行個體 ID。");
        if (!AwsEc2InstancePolicy.IsAllowed(action, currentState))
        {
            var label = AwsEc2InstancePolicy.ActionLabel(action);
            return Invalid<AwsEc2InstanceStateChange>(
                $"EC2 cannot {label.En} an instance from the '{AwsEc2InstancePolicy.NormalizeState(currentState)}' state.",
                $"EC2 執行個體處於「{AwsEc2InstancePolicy.NormalizeState(currentState)}」狀態時，唔可以{label.Zh}。");
        }

        var id = instanceId.Trim();
        try
        {
            AwsEc2InstanceStateChange change;
            switch (action)
            {
                case AwsEc2InstanceAction.Start:
                {
                    var response = await _ec2!.StartInstancesAsync(new Ec2Model.StartInstancesRequest
                    {
                        InstanceIds = new List<string> { id },
                    }, cancellationToken).ConfigureAwait(false);
                    change = MapSingleStateChange(response.StartingInstances, id, action, currentState, "pending");
                    break;
                }
                case AwsEc2InstanceAction.Stop:
                {
                    var response = await _ec2!.StopInstancesAsync(new Ec2Model.StopInstancesRequest
                    {
                        InstanceIds = new List<string> { id },
                    }, cancellationToken).ConfigureAwait(false);
                    change = MapSingleStateChange(response.StoppingInstances, id, action, currentState, "stopping");
                    break;
                }
                case AwsEc2InstanceAction.Reboot:
                    await _ec2!.RebootInstancesAsync(new Ec2Model.RebootInstancesRequest
                    {
                        InstanceIds = new List<string> { id },
                    }, cancellationToken).ConfigureAwait(false);
                    change = new AwsEc2InstanceStateChange(
                        id,
                        AwsEc2InstancePolicy.NormalizeState(currentState),
                        "running",
                        action);
                    break;
                case AwsEc2InstanceAction.Terminate:
                {
                    var response = await _ec2!.TerminateInstancesAsync(new Ec2Model.TerminateInstancesRequest
                    {
                        InstanceIds = new List<string> { id },
                    }, cancellationToken).ConfigureAwait(false);
                    change = MapSingleStateChange(response.TerminatingInstances, id, action, currentState, "shutting-down");
                    break;
                }
                default:
                    return Invalid<AwsEc2InstanceStateChange>("Unsupported EC2 instance action.", "唔支援呢個 EC2 執行個體操作。");
            }

            var actionLabel = AwsEc2InstancePolicy.ActionLabel(action);
            return AwsManagerResult<AwsEc2InstanceStateChange>.Ok(
                change,
                $"EC2 accepted the {actionLabel.En} request for '{id}'.",
                $"EC2 已接受對「{id}」嘅{actionLabel.Zh}要求。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsEc2InstanceStateChange>.Fail(ToSafeError(ex));
        }
    }

    private static AwsEc2InstanceStateChange MapSingleStateChange(
        IReadOnlyList<Ec2Model.InstanceStateChange>? changes,
        string instanceId,
        AwsEc2InstanceAction action,
        string previousState,
        string fallbackState)
    {
        var change = changes?.FirstOrDefault(item =>
            string.Equals(item.InstanceId, instanceId, StringComparison.Ordinal));
        return change is null
            ? new AwsEc2InstanceStateChange(
                instanceId,
                AwsEc2InstancePolicy.NormalizeState(previousState),
                fallbackState,
                action)
            : AwsEc2ModelMapper.MapStateChange(change, action);
    }

    public async Task<AwsManagerResult<AwsS3BucketPage>> ListBucketsAsync(
        AwsS3ListBucketsRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new AwsS3ListBucketsRequest();
        if (TryGetUnavailable<AwsS3BucketPage>(out var unavailable))
            return unavailable;

        try
        {
            var response = await _s3!.ListBucketsAsync(new S3Model.ListBucketsRequest
            {
                MaxBuckets = Math.Clamp(request.MaxResults, 1, 10_000),
                ContinuationToken = Normalize(request.NextToken),
                Prefix = Normalize(request.Prefix),
                BucketRegion = Normalize(request.BucketRegion),
            }, cancellationToken).ConfigureAwait(false);
            var buckets = (response.Buckets ?? new List<S3Model.S3Bucket>())
                .Select(bucket => new AwsS3Bucket(
                    bucket.BucketName ?? "",
                    ToOffset(bucket.CreationDate),
                    Normalize(bucket.BucketRegion),
                    Normalize(bucket.BucketArn)))
                .OrderBy(bucket => bucket.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var nextToken = Normalize(response.ContinuationToken);
            return AwsManagerResult<AwsS3BucketPage>.Ok(
                new AwsS3BucketPage(buckets, nextToken, nextToken is not null),
                $"Loaded {buckets.Length} S3 bucket(s).",
                $"已載入 {buckets.Length} 個 S3 bucket。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3BucketPage>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3ObjectPage>> ListObjectsAsync(
        AwsS3ListObjectsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsS3ObjectPage>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(request.BucketName))
            return Invalid<AwsS3ObjectPage>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            var response = await _s3!.ListObjectsV2Async(new S3Model.ListObjectsV2Request
            {
                BucketName = request.BucketName.Trim(),
                Prefix = request.Prefix ?? "",
                Delimiter = request.Delimiter ?? "",
                MaxKeys = Math.Clamp(request.MaxResults, 1, MaxS3ObjectPageSize),
                ContinuationToken = Normalize(request.NextToken),
                StartAfter = Normalize(request.StartAfter),
                FetchOwner = request.FetchOwner,
            }, cancellationToken).ConfigureAwait(false);
            var objects = (response.S3Objects ?? new List<S3Model.S3Object>())
                .Select(item => new AwsS3Object(
                    request.BucketName.Trim(),
                    item.Key ?? "",
                    item.Size ?? 0,
                    ToOffset(item.LastModified),
                    NormalizeEtag(item.ETag),
                    item.StorageClass?.Value,
                    item.Owner?.Id,
                    item.Owner?.DisplayName))
                .ToArray();
            var commonPrefixes = (response.CommonPrefixes ?? new List<string>())
                .Where(prefix => prefix is not null)
                .ToArray();
            var nextToken = Normalize(response.NextContinuationToken);
            return AwsManagerResult<AwsS3ObjectPage>.Ok(
                new AwsS3ObjectPage(
                    request.BucketName.Trim(),
                    response.Prefix ?? request.Prefix ?? "",
                    response.Delimiter ?? request.Delimiter ?? "",
                    commonPrefixes,
                    objects,
                    nextToken,
                    response.IsTruncated == true || nextToken is not null),
                $"Loaded {objects.Length} object(s) and {commonPrefixes.Length} prefix(es).",
                $"已載入 {objects.Length} 個 object 同 {commonPrefixes.Length} 個 prefix。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3ObjectPage>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3Bucket>> CreateBucketAsync(
        AwsS3CreateBucketRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsS3Bucket>(out var unavailable))
            return unavailable;
        if (!IsValidBucketName(request.BucketName))
            return Invalid<AwsS3Bucket>(
                "Enter a valid DNS-compatible S3 bucket name (3–63 lowercase characters).",
                "請輸入有效、符合 DNS 規格嘅 S3 bucket 名稱（3 至 63 個細寫字元）。");

        try
        {
            await _s3!.PutBucketAsync(new S3Model.PutBucketRequest
            {
                BucketName = request.BucketName.Trim(),
                UseClientRegion = true,
                ObjectLockEnabledForBucket = request.EnableObjectLock,
            }, cancellationToken).ConfigureAwait(false);
            var bucket = new AwsS3Bucket(
                request.BucketName.Trim(),
                DateTimeOffset.UtcNow,
                RegionName,
                $"arn:{Context.PartitionName}:s3:::{request.BucketName.Trim()}");
            return AwsManagerResult<AwsS3Bucket>.Ok(
                bucket,
                $"S3 bucket '{bucket.Name}' created in {RegionName}.",
                $"已喺 {RegionName} 建立 S3 bucket「{bucket.Name}」。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3Bucket>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult> DeleteBucketAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            await _s3!.DeleteBucketAsync(new S3Model.DeleteBucketRequest
            {
                BucketName = bucketName.Trim(),
                UseClientRegion = true,
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult.Ok(
                $"S3 bucket '{bucketName.Trim()}' deleted.",
                $"已刪除 S3 bucket「{bucketName.Trim()}」。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3UploadResult>> UploadObjectAsync(
        AwsS3UploadRequest request,
        IProgress<AwsTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsS3UploadResult>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(request.BucketName) || string.IsNullOrWhiteSpace(request.Key))
            return Invalid<AwsS3UploadResult>("A bucket name and object key are required.", "需要 bucket 名稱同 object key。");
        if (string.IsNullOrWhiteSpace(request.LocalFilePath) || !File.Exists(request.LocalFilePath))
            return Invalid<AwsS3UploadResult>("Select an existing local file to upload.", "請選擇現有本機檔案上載。");
        if (request.Tags.Count > 10)
            return Invalid<AwsS3UploadResult>("An S3 object can have at most 10 tags.", "一個 S3 object 最多可以有 10 個標籤。");

        try
        {
            var file = new FileInfo(request.LocalFilePath);
            var put = new S3Model.PutObjectRequest
            {
                BucketName = request.BucketName.Trim(),
                Key = request.Key,
                FilePath = file.FullName,
                ContentType = Normalize(request.ContentType),
                StorageClass = Normalize(request.StorageClass),
                ServerSideEncryptionMethod = Normalize(request.ServerSideEncryption)
                    ?? (Normalize(request.KmsKeyId) is null ? null : "aws:kms"),
                ServerSideEncryptionKeyManagementServiceKeyId = Normalize(request.KmsKeyId),
                IfNoneMatch = AwsS3UploadPolicy.IfNoneMatch(request.Overwrite),
                TagSet = request.Tags.Select(pair => new S3Model.Tag
                {
                    Key = pair.Key,
                    Value = pair.Value,
                }).ToList(),
            };
            foreach (var pair in request.Metadata)
                put.Metadata[pair.Key] = pair.Value;
            put.StreamTransferProgress += (_, args) => progress?.Report(new AwsTransferProgress(
                "upload",
                request.BucketName.Trim(),
                request.Key,
                args.TransferredBytes,
                args.TotalBytes,
                args.PercentDone));

            var response = await _s3!.PutObjectAsync(put, cancellationToken).ConfigureAwait(false);
            var result = new AwsS3UploadResult(
                request.BucketName.Trim(),
                request.Key,
                NormalizeEtag(response.ETag),
                Normalize(response.VersionId),
                response.Size ?? file.Length);
            return AwsManagerResult<AwsS3UploadResult>.Ok(
                result,
                $"Uploaded '{request.Key}' ({result.Size:N0} bytes).",
                $"已上載「{request.Key}」（{result.Size:N0} bytes）。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3UploadResult>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3DownloadResult>> DownloadObjectAsync(
        AwsS3DownloadRequest request,
        IProgress<AwsTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (TryGetUnavailable<AwsS3DownloadResult>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(request.BucketName) || string.IsNullOrWhiteSpace(request.Key))
            return Invalid<AwsS3DownloadResult>("A bucket name and object key are required.", "需要 bucket 名稱同 object key。");
        if (string.IsNullOrWhiteSpace(request.DestinationPath))
            return Invalid<AwsS3DownloadResult>("Choose a download destination.", "請選擇下載位置。");

        string? temporaryPath = null;
        try
        {
            var destination = Path.GetFullPath(request.DestinationPath);
            if (File.Exists(destination) && !request.Overwrite)
                return Invalid<AwsS3DownloadResult>(
                    "The destination file already exists. Enable overwrite or choose another path.",
                    "目標檔案已經存在；請啟用覆寫或者揀另一個位置。");
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            temporaryPath = destination + $".winforge-{Guid.NewGuid():N}.download";

            using var response = await _s3!.GetObjectAsync(new S3Model.GetObjectRequest
            {
                BucketName = request.BucketName.Trim(),
                Key = request.Key,
                VersionId = Normalize(request.VersionId),
            }, cancellationToken).ConfigureAwait(false);
            response.WriteObjectProgressEvent += (_, args) => progress?.Report(new AwsTransferProgress(
                "download",
                request.BucketName.Trim(),
                request.Key,
                args.TransferredBytes,
                args.TotalBytes,
                args.PercentDone));
            await response.WriteResponseStreamToFileAsync(
                temporaryPath, false, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, destination, request.Overwrite);
            temporaryPath = null;

            var result = new AwsS3DownloadResult(
                request.BucketName.Trim(),
                request.Key,
                destination,
                NormalizeEtag(response.ETag),
                Normalize(response.VersionId),
                response.ContentLength);
            return AwsManagerResult<AwsS3DownloadResult>.Ok(
                result,
                $"Downloaded '{request.Key}' ({result.Size:N0} bytes).",
                $"已下載「{request.Key}」（{result.Size:N0} bytes）。");
        }
        catch (Exception ex)
        {
            TryDeleteFile(temporaryPath);
            return AwsManagerResult<AwsS3DownloadResult>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3DeleteObjectsResult>> DeleteObjectsAsync(
        string bucketName,
        IReadOnlyList<AwsS3ObjectIdentifier> objects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(objects);
        if (TryGetUnavailable<AwsS3DeleteObjectsResult>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid<AwsS3DeleteObjectsResult>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");
        if (objects.Count == 0 || objects.Any(item => string.IsNullOrWhiteSpace(item.Key)))
            return Invalid<AwsS3DeleteObjectsResult>("Select at least one valid object key.", "請選擇最少一個有效 object key。");

        try
        {
            var deletedCount = 0;
            var failures = new List<AwsS3DeleteFailure>();
            foreach (var batch in objects.Chunk(MaxS3DeleteBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = await _s3!.DeleteObjectsAsync(new S3Model.DeleteObjectsRequest
                {
                    BucketName = bucketName.Trim(),
                    Quiet = false,
                    Objects = batch.Select(item => new S3Model.KeyVersion
                    {
                        Key = item.Key,
                        VersionId = Normalize(item.VersionId),
                    }).ToList(),
                }, cancellationToken).ConfigureAwait(false);
                deletedCount += response.DeletedObjects?.Count ?? 0;
                foreach (var error in response.DeleteErrors ?? new List<S3Model.DeleteError>())
                {
                    var code = SafeCode(error.Code, "DeleteFailed");
                    failures.Add(new AwsS3DeleteFailure(
                        error.Key ?? "",
                        Normalize(error.VersionId),
                        code,
                        new AwsManagerText(
                            $"AWS did not delete this object ({code}).",
                            $"AWS 冇刪除呢個 object（{code}）。")));
                }
            }

            var value = new AwsS3DeleteObjectsResult(objects.Count, deletedCount, failures);
            if (failures.Count == 0)
                return AwsManagerResult<AwsS3DeleteObjectsResult>.Ok(
                    value,
                    $"Deleted {deletedCount} S3 object(s).",
                    $"已刪除 {deletedCount} 個 S3 object。");

            var partialError = new AwsManagerError(
                AwsManagerErrorKind.AwsService,
                "PartialDeleteFailure",
                new AwsManagerText(
                    $"Deleted {deletedCount} object(s); {failures.Count} object(s) failed.",
                    $"已刪除 {deletedCount} 個 object；{failures.Count} 個失敗。"));
            return new AwsManagerResult<AwsS3DeleteObjectsResult>(
                false, value, partialError.Message, partialError);
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3DeleteObjectsResult>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3VersioningConfiguration>> GetBucketVersioningAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsS3VersioningConfiguration>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid<AwsS3VersioningConfiguration>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            var response = await _s3!.GetBucketVersioningAsync(new S3Model.GetBucketVersioningRequest
            {
                BucketName = bucketName.Trim(),
            }, cancellationToken).ConfigureAwait(false);
            var status = response.VersioningConfig?.Status?.Value;
            var config = new AwsS3VersioningConfiguration(
                !string.IsNullOrWhiteSpace(status) && !string.Equals(status, "Off", StringComparison.OrdinalIgnoreCase),
                string.IsNullOrWhiteSpace(status) ? "Off" : status,
                response.VersioningConfig?.EnableMfaDelete);
            return AwsManagerResult<AwsS3VersioningConfiguration>.Ok(
                config,
                $"Bucket versioning is {config.Status}.",
                $"Bucket versioning 狀態係 {config.Status}。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3VersioningConfiguration>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult> PutBucketVersioningAsync(
        string bucketName,
        AwsS3VersioningConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (TryGetUnavailable(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid("An S3 bucket name is required.", "需要 S3 bucket 名稱。");
        if (!string.Equals(configuration.Status, "Enabled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(configuration.Status, "Suspended", StringComparison.OrdinalIgnoreCase))
            return Invalid(
                "Versioning can be changed to Enabled or Suspended. AWS does not allow an enabled bucket to return to Off.",
                "Versioning 只可以改做 Enabled 或 Suspended；AWS 唔容許已啟用嘅 bucket 變返 Off。");
        if (configuration.MfaDeleteEnabled == true)
            return NotSupported(
                "Changing MFA Delete requires an MFA device serial and a fresh one-time code, so it is intentionally not sent by this session API.",
                "更改 MFA Delete 需要 MFA 裝置 serial 同即時一次性密碼，所以呢個 session API 唔會代傳。");

        try
        {
            await _s3!.PutBucketVersioningAsync(new S3Model.PutBucketVersioningRequest
            {
                BucketName = bucketName.Trim(),
                VersioningConfig = new S3Model.S3BucketVersioningConfig
                {
                    Status = Amazon.S3.VersionStatus.FindValue(configuration.Status),
                },
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult.Ok(
                $"Bucket versioning changed to {configuration.Status}.",
                $"Bucket versioning 已改做 {configuration.Status}。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3EncryptionConfiguration>> GetBucketEncryptionAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsS3EncryptionConfiguration>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid<AwsS3EncryptionConfiguration>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            var response = await _s3!.GetBucketEncryptionAsync(new S3Model.GetBucketEncryptionRequest
            {
                BucketName = bucketName.Trim(),
            }, cancellationToken).ConfigureAwait(false);
            var rules = (response.ServerSideEncryptionConfiguration?.ServerSideEncryptionRules
                         ?? new List<S3Model.ServerSideEncryptionRule>())
                .Select(rule => new AwsS3EncryptionRule(
                    rule.ServerSideEncryptionByDefault?.ServerSideEncryptionAlgorithm?.Value ?? "",
                    Normalize(rule.ServerSideEncryptionByDefault?.ServerSideEncryptionKeyManagementServiceKeyId),
                    rule.BucketKeyEnabled,
                    Normalize(rule.BlockedEncryptionTypes?.EncryptionType?.FirstOrDefault())))
                .ToArray();
            return AwsManagerResult<AwsS3EncryptionConfiguration>.Ok(
                new AwsS3EncryptionConfiguration(true, rules),
                $"Loaded {rules.Length} default encryption rule(s).",
                $"已載入 {rules.Length} 條預設加密規則。");
        }
        catch (Exception ex) when (IsS3ConfigurationMissing(ex, "ServerSideEncryptionConfigurationNotFoundError"))
        {
            return AwsManagerResult<AwsS3EncryptionConfiguration>.Ok(
                new AwsS3EncryptionConfiguration(false, Array.Empty<AwsS3EncryptionRule>()),
                "No explicit bucket encryption configuration is present.",
                "Bucket 冇明確加密設定。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3EncryptionConfiguration>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult> PutBucketEncryptionAsync(
        string bucketName,
        AwsS3EncryptionConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (TryGetUnavailable(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            if (!configuration.Configured || configuration.Rules.Count == 0)
            {
                await _s3!.DeleteBucketEncryptionAsync(new S3Model.DeleteBucketEncryptionRequest
                {
                    BucketName = bucketName.Trim(),
                }, cancellationToken).ConfigureAwait(false);
                return AwsManagerResult.Ok("Bucket encryption configuration removed.", "已移除 bucket 加密設定。");
            }

            if (configuration.Rules.Any(rule => string.IsNullOrWhiteSpace(rule.Algorithm)))
                return Invalid("Every encryption rule needs an algorithm.", "每條加密規則都需要 algorithm。");
            await _s3!.PutBucketEncryptionAsync(new S3Model.PutBucketEncryptionRequest
            {
                BucketName = bucketName.Trim(),
                ServerSideEncryptionConfiguration = new S3Model.ServerSideEncryptionConfiguration
                {
                    ServerSideEncryptionRules = configuration.Rules.Select(rule =>
                        new S3Model.ServerSideEncryptionRule
                        {
                            BucketKeyEnabled = rule.BucketKeyEnabled,
                            BlockedEncryptionTypes = string.IsNullOrWhiteSpace(rule.BlockedEncryptionType)
                                ? null
                                : new S3Model.BlockedEncryptionTypes
                                {
                                    EncryptionType = new List<string> { rule.BlockedEncryptionType },
                                },
                            ServerSideEncryptionByDefault = new S3Model.ServerSideEncryptionByDefault
                            {
                                ServerSideEncryptionAlgorithm = Amazon.S3.ServerSideEncryptionMethod.FindValue(rule.Algorithm),
                                ServerSideEncryptionKeyManagementServiceKeyId = Normalize(rule.KmsKeyId),
                            },
                        }).ToList(),
                },
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult.Ok("Bucket encryption configuration saved.", "已儲存 bucket 加密設定。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3PublicAccessBlockConfiguration>> GetPublicAccessBlockAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsS3PublicAccessBlockConfiguration>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid<AwsS3PublicAccessBlockConfiguration>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            var response = await _s3!.GetPublicAccessBlockAsync(new S3Model.GetPublicAccessBlockRequest
            {
                BucketName = bucketName.Trim(),
            }, cancellationToken).ConfigureAwait(false);
            var value = response.PublicAccessBlockConfiguration;
            var config = new AwsS3PublicAccessBlockConfiguration(
                true,
                value?.BlockPublicAcls == true,
                value?.IgnorePublicAcls == true,
                value?.BlockPublicPolicy == true,
                value?.RestrictPublicBuckets == true);
            return AwsManagerResult<AwsS3PublicAccessBlockConfiguration>.Ok(
                config, "Public access block configuration loaded.", "已載入 public access block 設定。");
        }
        catch (Exception ex) when (IsS3ConfigurationMissing(ex, "NoSuchPublicAccessBlockConfiguration"))
        {
            return AwsManagerResult<AwsS3PublicAccessBlockConfiguration>.Ok(
                new AwsS3PublicAccessBlockConfiguration(false, false, false, false, false),
                "No bucket-level public access block configuration is present.",
                "Bucket level 冇 public access block 設定。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3PublicAccessBlockConfiguration>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult> PutPublicAccessBlockAsync(
        string bucketName,
        AwsS3PublicAccessBlockConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (TryGetUnavailable(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            if (!configuration.Configured)
            {
                await _s3!.DeletePublicAccessBlockAsync(new S3Model.DeletePublicAccessBlockRequest
                {
                    BucketName = bucketName.Trim(),
                }, cancellationToken).ConfigureAwait(false);
                return AwsManagerResult.Ok("Bucket-level public access block removed.", "已移除 bucket level public access block。");
            }

            await _s3!.PutPublicAccessBlockAsync(new S3Model.PutPublicAccessBlockRequest
            {
                BucketName = bucketName.Trim(),
                PublicAccessBlockConfiguration = new S3Model.PublicAccessBlockConfiguration
                {
                    BlockPublicAcls = configuration.BlockPublicAcls,
                    IgnorePublicAcls = configuration.IgnorePublicAcls,
                    BlockPublicPolicy = configuration.BlockPublicPolicy,
                    RestrictPublicBuckets = configuration.RestrictPublicBuckets,
                },
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult.Ok("Public access block configuration saved.", "已儲存 public access block 設定。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3BucketTags>> GetBucketTagsAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsS3BucketTags>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid<AwsS3BucketTags>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            var response = await _s3!.GetBucketTaggingAsync(new S3Model.GetBucketTaggingRequest
            {
                BucketName = bucketName.Trim(),
            }, cancellationToken).ConfigureAwait(false);
            var tags = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var tag in response.TagSet ?? new List<S3Model.Tag>())
                if (!string.IsNullOrEmpty(tag.Key))
                    tags[tag.Key] = tag.Value ?? "";
            return AwsManagerResult<AwsS3BucketTags>.Ok(
                new AwsS3BucketTags(tags),
                $"Loaded {tags.Count} bucket tag(s).",
                $"已載入 {tags.Count} 個 bucket 標籤。");
        }
        catch (Exception ex) when (IsS3ConfigurationMissing(ex, "NoSuchTagSet"))
        {
            return AwsManagerResult<AwsS3BucketTags>.Ok(
                new AwsS3BucketTags(new Dictionary<string, string>()),
                "This bucket has no tags.",
                "呢個 bucket 冇標籤。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3BucketTags>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult> PutBucketTagsAsync(
        string bucketName,
        AwsS3BucketTags configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (TryGetUnavailable(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid("An S3 bucket name is required.", "需要 S3 bucket 名稱。");
        if (configuration.Tags.Count > 50)
            return Invalid("An S3 bucket can have at most 50 tags.", "一個 S3 bucket 最多可以有 50 個標籤。");
        if (configuration.Tags.Keys.Any(string.IsNullOrWhiteSpace))
            return Invalid("Bucket tag keys cannot be empty.", "Bucket 標籤 key 唔可以留空。");

        try
        {
            if (configuration.Tags.Count == 0)
            {
                await _s3!.DeleteBucketTaggingAsync(new S3Model.DeleteBucketTaggingRequest
                {
                    BucketName = bucketName.Trim(),
                }, cancellationToken).ConfigureAwait(false);
                return AwsManagerResult.Ok("All bucket tags removed.", "已移除所有 bucket 標籤。");
            }

            await _s3!.PutBucketTaggingAsync(new S3Model.PutBucketTaggingRequest
            {
                BucketName = bucketName.Trim(),
                TagSet = configuration.Tags.Select(pair => new S3Model.Tag
                {
                    Key = pair.Key,
                    Value = pair.Value,
                }).ToList(),
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult.Ok("Bucket tags saved.", "已儲存 bucket 標籤。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3BucketPolicy>> GetBucketPolicyAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsS3BucketPolicy>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid<AwsS3BucketPolicy>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            var response = await _s3!.GetBucketPolicyAsync(new S3Model.GetBucketPolicyRequest
            {
                BucketName = bucketName.Trim(),
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult<AwsS3BucketPolicy>.Ok(
                new AwsS3BucketPolicy(true, RedactJson(response.Policy ?? "{}")),
                "Bucket policy loaded.",
                "已載入 bucket policy。");
        }
        catch (Exception ex) when (IsS3ConfigurationMissing(ex, "NoSuchBucketPolicy"))
        {
            return AwsManagerResult<AwsS3BucketPolicy>.Ok(
                new AwsS3BucketPolicy(false, null),
                "This bucket has no bucket policy.",
                "呢個 bucket 冇 bucket policy。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3BucketPolicy>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult> PutBucketPolicyAsync(
        string bucketName,
        AwsS3BucketPolicy configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (TryGetUnavailable(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            if (!configuration.Configured || string.IsNullOrWhiteSpace(configuration.PolicyJson))
            {
                await _s3!.DeleteBucketPolicyAsync(new S3Model.DeleteBucketPolicyRequest
                {
                    BucketName = bucketName.Trim(),
                }, cancellationToken).ConfigureAwait(false);
                return AwsManagerResult.Ok("Bucket policy removed.", "已移除 bucket policy。");
            }
            if (!IsJsonKind(configuration.PolicyJson, JsonValueKind.Object))
                return Invalid("The bucket policy must be a JSON object.", "Bucket policy 必須係 JSON object。");

            await _s3!.PutBucketPolicyAsync(new S3Model.PutBucketPolicyRequest
            {
                BucketName = bucketName.Trim(),
                Policy = configuration.PolicyJson,
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult.Ok("Bucket policy saved.", "已儲存 bucket policy。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3LifecycleConfiguration>> GetLifecycleConfigurationAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsS3LifecycleConfiguration>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid<AwsS3LifecycleConfiguration>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            var response = await _s3!.GetLifecycleConfigurationAsync(
                new S3Model.GetLifecycleConfigurationRequest { BucketName = bucketName.Trim() },
                cancellationToken).ConfigureAwait(false);
            var rules = (response.Configuration?.Rules ?? new List<S3Model.LifecycleRule>())
                .Select(MapLifecycleRule)
                .ToArray();
            return AwsManagerResult<AwsS3LifecycleConfiguration>.Ok(
                new AwsS3LifecycleConfiguration(true, rules),
                $"Loaded {rules.Length} lifecycle rule(s).",
                $"已載入 {rules.Length} 條 lifecycle 規則。");
        }
        catch (Exception ex) when (IsS3ConfigurationMissing(ex, "NoSuchLifecycleConfiguration"))
        {
            return AwsManagerResult<AwsS3LifecycleConfiguration>.Ok(
                new AwsS3LifecycleConfiguration(false, Array.Empty<AwsS3LifecycleRule>()),
                "This bucket has no lifecycle configuration.",
                "呢個 bucket 冇 lifecycle 設定。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3LifecycleConfiguration>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult> PutLifecycleConfigurationAsync(
        string bucketName,
        AwsS3LifecycleConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (configuration is null)
            return Invalid("A lifecycle configuration is required.", "需要 lifecycle 設定。");
        if (TryGetUnavailable(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid("An S3 bucket name is required.", "需要 S3 bucket 名稱。");
        if (configuration.Rules is null)
            return Invalid("Lifecycle rules must be a JSON array, not null.", "Lifecycle rules 必須係 JSON array，唔可以係 null。");
        if (configuration.Rules.Any(rule => rule is null || string.IsNullOrWhiteSpace(rule.Id)))
            return Invalid("Every lifecycle rule needs an ID.", "每條 lifecycle 規則都需要 ID。");

        try
        {
            if (!configuration.Configured || configuration.Rules.Count == 0)
            {
                await _s3!.DeleteLifecycleConfigurationAsync(
                    new S3Model.DeleteLifecycleConfigurationRequest { BucketName = bucketName.Trim() },
                    cancellationToken).ConfigureAwait(false);
                return AwsManagerResult.Ok("Lifecycle configuration removed.", "已移除 lifecycle 設定。");
            }

            var sdkRules = new List<S3Model.LifecycleRule>(configuration.Rules.Count);
            foreach (var rule in configuration.Rules)
            {
                var mapped = TryMapLifecycleRule(rule, out var validationError);
                if (mapped is null)
                    return Invalid(validationError.En, validationError.Zh);
                sdkRules.Add(mapped);
            }
            await _s3!.PutLifecycleConfigurationAsync(new S3Model.PutLifecycleConfigurationRequest
            {
                BucketName = bucketName.Trim(),
                Configuration = new S3Model.LifecycleConfiguration { Rules = sdkRules },
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult.Ok("Lifecycle configuration saved.", "已儲存 lifecycle 設定。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult<AwsS3CorsConfiguration>> GetCorsConfigurationAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailable<AwsS3CorsConfiguration>(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid<AwsS3CorsConfiguration>("An S3 bucket name is required.", "需要 S3 bucket 名稱。");

        try
        {
            var response = await _s3!.GetCORSConfigurationAsync(
                new S3Model.GetCORSConfigurationRequest { BucketName = bucketName.Trim() },
                cancellationToken).ConfigureAwait(false);
            var rules = (response.Configuration?.Rules ?? new List<S3Model.CORSRule>())
                .Select(rule => new AwsS3CorsRule(
                    Normalize(rule.Id),
                    (rule.AllowedOrigins ?? new List<string>()).ToArray(),
                    (rule.AllowedMethods ?? new List<string>()).ToArray(),
                    (rule.AllowedHeaders ?? new List<string>()).ToArray(),
                    (rule.ExposeHeaders ?? new List<string>()).ToArray(),
                    rule.MaxAgeSeconds))
                .ToArray();
            return AwsManagerResult<AwsS3CorsConfiguration>.Ok(
                new AwsS3CorsConfiguration(true, rules),
                $"Loaded {rules.Length} CORS rule(s).",
                $"已載入 {rules.Length} 條 CORS 規則。");
        }
        catch (Exception ex) when (IsS3ConfigurationMissing(ex, "NoSuchCORSConfiguration"))
        {
            return AwsManagerResult<AwsS3CorsConfiguration>.Ok(
                new AwsS3CorsConfiguration(false, Array.Empty<AwsS3CorsRule>()),
                "This bucket has no CORS configuration.",
                "呢個 bucket 冇 CORS 設定。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult<AwsS3CorsConfiguration>.Fail(ToSafeError(ex));
        }
    }

    public async Task<AwsManagerResult> PutCorsConfigurationAsync(
        string bucketName,
        AwsS3CorsConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (configuration is null)
            return Invalid("A CORS configuration is required.", "需要 CORS 設定。");
        if (TryGetUnavailable(out var unavailable))
            return unavailable;
        if (string.IsNullOrWhiteSpace(bucketName))
            return Invalid("An S3 bucket name is required.", "需要 S3 bucket 名稱。");
        if (configuration.Rules is null)
            return Invalid("CORS rules must be a JSON array, not null.", "CORS rules 必須係 JSON array，唔可以係 null。");
        if (configuration.Rules.Any(rule => rule is null
            || rule.AllowedOrigins is null
            || rule.AllowedMethods is null
            || rule.AllowedHeaders is null
            || rule.ExposeHeaders is null
            || rule.AllowedOrigins.Count == 0
            || rule.AllowedMethods.Count == 0))
            return Invalid("Every CORS rule needs at least one allowed origin and method.", "每條 CORS 規則都需要最少一個 allowed origin 同 method。");

        try
        {
            if (!configuration.Configured || configuration.Rules.Count == 0)
            {
                await _s3!.DeleteCORSConfigurationAsync(
                    new S3Model.DeleteCORSConfigurationRequest { BucketName = bucketName.Trim() },
                    cancellationToken).ConfigureAwait(false);
                return AwsManagerResult.Ok("CORS configuration removed.", "已移除 CORS 設定。");
            }

            await _s3!.PutCORSConfigurationAsync(new S3Model.PutCORSConfigurationRequest
            {
                BucketName = bucketName.Trim(),
                Configuration = new S3Model.CORSConfiguration
                {
                    Rules = configuration.Rules.Select(rule => new S3Model.CORSRule
                    {
                        Id = Normalize(rule.Id),
                        AllowedOrigins = rule.AllowedOrigins.ToList(),
                        AllowedMethods = rule.AllowedMethods.ToList(),
                        AllowedHeaders = rule.AllowedHeaders.ToList(),
                        ExposeHeaders = rule.ExposeHeaders.ToList(),
                        MaxAgeSeconds = rule.MaxAgeSeconds,
                    }).ToList(),
                },
            }, cancellationToken).ConfigureAwait(false);
            return AwsManagerResult.Ok("CORS configuration saved.", "已儲存 CORS 設定。");
        }
        catch (Exception ex)
        {
            return AwsManagerResult.Fail(ToSafeError(ex));
        }
    }

    private static AWSCredentials ResolveCredentials(
        string? profileName,
        RegionEndpoint region)
    {
        if (profileName is null)
            return DefaultAWSCredentialsIdentityResolver.GetCredentials(new AmazonSecurityTokenServiceConfig
            {
                RegionEndpoint = region,
            });

        var chain = new CredentialProfileStoreChain();
        if (chain.TryGetAWSCredentials(profileName, out var credentials) && credentials is not null)
            return credentials;
        throw new InvalidOperationException("The selected shared profile cannot create AWS credentials.");
    }

    private static RegionEndpoint ResolveRegionWithoutCredentials(
        AwsManagerSessionOptions options,
        string? profileName)
    {
        var explicitRegion = Normalize(options.RegionName);
        if (explicitRegion is not null)
            return RegionEndpoint.GetBySystemName(explicitRegion);

        if (profileName is not null)
        {
            try
            {
                var chain = new CredentialProfileStoreChain();
                if (chain.TryGetProfile(profileName, out var profile) && profile.Region is not null)
                    return profile.Region;
            }
            catch
            {
                // Credential setup will return the actionable error. Region selection remains deterministic.
            }
        }

        try
        {
            return FallbackRegionFactory.GetRegionEndpoint() ?? RegionEndpoint.USEast1;
        }
        catch
        {
            return RegionEndpoint.USEast1;
        }
    }

    private bool TryGetUnavailable<T>(out AwsManagerResult<T> result)
    {
        var error = _disposed
            ? new AwsManagerError(
                AwsManagerErrorKind.Unknown,
                "SessionDisposed",
                new AwsManagerText("This AWS manager session has been closed.", "呢個 AWS manager session 已經關閉。"))
            : _initializationError;
        if (error is null)
        {
            result = null!;
            return false;
        }

        result = AwsManagerResult<T>.Fail(error);
        return true;
    }

    private bool TryGetUnavailable(out AwsManagerResult result)
    {
        var error = _disposed
            ? new AwsManagerError(
                AwsManagerErrorKind.Unknown,
                "SessionDisposed",
                new AwsManagerText("This AWS manager session has been closed.", "呢個 AWS manager session 已經關閉。"))
            : _initializationError;
        if (error is null)
        {
            result = null!;
            return false;
        }

        result = AwsManagerResult.Fail(error);
        return true;
    }

    private static AwsManagerResult<T> Invalid<T>(string en, string zh)
        => AwsManagerResult<T>.Fail(new AwsManagerError(
            AwsManagerErrorKind.InvalidInput,
            "InvalidInput",
            new AwsManagerText(en, zh)));

    private static AwsManagerResult Invalid(string en, string zh)
        => AwsManagerResult.Fail(new AwsManagerError(
            AwsManagerErrorKind.InvalidInput,
            "InvalidInput",
            new AwsManagerText(en, zh)));

    private static AwsManagerResult NotSupported(string en, string zh)
        => AwsManagerResult.Fail(new AwsManagerError(
            AwsManagerErrorKind.NotSupported,
            "NotSupported",
            new AwsManagerText(en, zh)));

    private static AwsManagerError ToSafeError(Exception exception)
    {
        if (exception is OperationCanceledException)
            return new AwsManagerError(
                AwsManagerErrorKind.Cancelled,
                "Cancelled",
                new AwsManagerText("The AWS operation was cancelled.", "AWS 操作已取消。"));

        if (exception is AmazonServiceException aws)
        {
            var code = SafeCode(aws.ErrorCode, exception.GetType().Name);
            var status = (int)aws.StatusCode;
            var lowerCode = code.ToLowerInvariant();
            var kind = status switch
            {
                (int)HttpStatusCode.Unauthorized => AwsManagerErrorKind.AuthenticationRequired,
                (int)HttpStatusCode.Forbidden => AwsManagerErrorKind.AccessDenied,
                (int)HttpStatusCode.NotFound => AwsManagerErrorKind.NotFound,
                (int)HttpStatusCode.Conflict => AwsManagerErrorKind.Conflict,
                (int)HttpStatusCode.PreconditionFailed => AwsManagerErrorKind.Conflict,
                429 => AwsManagerErrorKind.Throttled,
                >= 500 => AwsManagerErrorKind.ServiceUnavailable,
                _ when lowerCode.Contains("accessdenied", StringComparison.Ordinal)
                    || lowerCode.Contains("unauthorized", StringComparison.Ordinal) => AwsManagerErrorKind.AccessDenied,
                _ when lowerCode.Contains("expiredtoken", StringComparison.Ordinal)
                    || lowerCode.Contains("invalidclienttoken", StringComparison.Ordinal)
                    || lowerCode.Contains("unrecognizedclient", StringComparison.Ordinal) => AwsManagerErrorKind.AuthenticationRequired,
                _ when lowerCode.Contains("throttl", StringComparison.Ordinal)
                    || lowerCode.Contains("toomanyrequest", StringComparison.Ordinal)
                    || lowerCode.Contains("requestlimitexceeded", StringComparison.Ordinal) => AwsManagerErrorKind.Throttled,
                _ when lowerCode.Contains("notfound", StringComparison.Ordinal)
                    || lowerCode.StartsWith("nosuch", StringComparison.Ordinal)
                    || lowerCode.Contains("invalidinstanceid.notfound", StringComparison.Ordinal) => AwsManagerErrorKind.NotFound,
                _ when lowerCode.Contains("conflict", StringComparison.Ordinal)
                    || lowerCode.Contains("alreadyexists", StringComparison.Ordinal)
                    || lowerCode.Contains("incorrectinstancestate", StringComparison.Ordinal) => AwsManagerErrorKind.Conflict,
                _ => AwsManagerErrorKind.AwsService,
            };
            var message = kind switch
            {
                AwsManagerErrorKind.AuthenticationRequired => new AwsManagerText(
                    "AWS rejected or expired this session. Refresh the selected credentials or sign in to IAM Identity Center again.",
                    "AWS 拒絕咗或者呢個 session 已過期；請更新所選憑證，或者重新登入 IAM Identity Center。"),
                AwsManagerErrorKind.AccessDenied => new AwsManagerText(
                    "The active AWS identity does not have permission for this operation.",
                    "目前 AWS 身份冇權限做呢個操作。"),
                AwsManagerErrorKind.NotFound => new AwsManagerText(
                    "AWS could not find the requested resource or configuration in this Region.",
                    "AWS 喺呢個 Region 搵唔到要求嘅資源或者設定。"),
                AwsManagerErrorKind.Conflict => new AwsManagerText(
                    "AWS rejected the operation because the resource state conflicts with it.",
                    "資源目前狀態有衝突，所以 AWS 拒絕咗呢個操作。"),
                AwsManagerErrorKind.Throttled => new AwsManagerText(
                    "AWS is throttling requests. Wait briefly and retry.",
                    "AWS 正限制要求頻率；請等一陣再試。"),
                AwsManagerErrorKind.ServiceUnavailable => new AwsManagerText(
                    "The AWS service is temporarily unavailable. Retry later.",
                    "AWS 服務暫時用唔到；請遲啲再試。"),
                _ => new AwsManagerText(
                    $"AWS rejected the operation ({code}). Sensitive service details were withheld.",
                    $"AWS 拒絕咗操作（{code}）；敏感服務詳情已隱藏。"),
            };
            return new AwsManagerError(
                kind,
                code,
                message,
                SafeRequestId(aws.RequestId),
                status == 0 ? null : status,
                aws.Retryable?.Throttling == true || status == 429 || status >= 500);
        }

        if (exception is AmazonClientException clientException)
        {
            var internalText = clientException.Message ?? "";
            var auth = internalText.Contains("credential", StringComparison.OrdinalIgnoreCase)
                || internalText.Contains("token", StringComparison.OrdinalIgnoreCase)
                || internalText.Contains("sso", StringComparison.OrdinalIgnoreCase);
            return auth
                ? new AwsManagerError(
                    AwsManagerErrorKind.CredentialsUnavailable,
                    "CredentialsUnavailable",
                    new AwsManagerText(
                        "The AWS SDK could not obtain usable credentials. Refresh the selected profile or SSO session.",
                        "AWS SDK 攞唔到可用憑證；請更新所選 profile 或 SSO session。"))
                : new AwsManagerError(
                    AwsManagerErrorKind.Network,
                    "AwsClientFailure",
                    new AwsManagerText(
                        "The AWS SDK could not reach or prepare the service request. Check the network and Region.",
                        "AWS SDK 無法連線或者準備服務要求；請檢查網絡同 Region。"),
                    Retryable: true);
        }

        if (exception is IOException or UnauthorizedAccessException)
            return new AwsManagerError(
                AwsManagerErrorKind.LocalIo,
                "LocalIoFailure",
                new AwsManagerText(
                    "The local file operation failed. Check the path, free space, and permissions.",
                    "本機檔案操作失敗；請檢查路徑、可用空間同權限。"));

        return new AwsManagerError(
            AwsManagerErrorKind.Unknown,
            "UnexpectedFailure",
            new AwsManagerText(
                "The AWS operation failed. Exception details were withheld to protect credentials and resource data.",
                "AWS 操作失敗；為保護憑證同資源資料，例外詳情已隱藏。"));
    }

    private static AwsManagerError ToResourceExplorerError(Exception exception)
    {
        if (exception is ExplorerModel.ResourceNotFoundException)
            return new AwsManagerError(
                AwsManagerErrorKind.NotConfigured,
                "ResourceExplorerNotConfigured",
                new AwsManagerText(
                    "AWS Resource Explorer has no usable index/default view in this Region. Create an index and associate a default view, or use the tagging inventory fallback.",
                    "呢個 Region 冇可用嘅 AWS Resource Explorer index／default view；請建立 index 並連結 default view，或者用 tagging inventory 後備。"));
        if (exception is ExplorerModel.AccessDeniedException or ExplorerModel.UnauthorizedException)
            return new AwsManagerError(
                AwsManagerErrorKind.AccessDenied,
                "ResourceExplorerAccessDenied",
                new AwsManagerText(
                    "The active identity cannot search AWS Resource Explorer in this Region.",
                    "目前身份冇權限搜尋呢個 Region 嘅 AWS Resource Explorer。"));
        if (exception is ExplorerModel.ValidationException)
            return new AwsManagerError(
                AwsManagerErrorKind.InvalidInput,
                "ResourceExplorerInvalidQuery",
                new AwsManagerText(
                    "AWS Resource Explorer rejected the search query or view ARN.",
                    "AWS Resource Explorer 拒絕咗搜尋 query 或 view ARN。"));
        return ToSafeError(exception);
    }

    private static bool IsResourceExplorerCapabilityFailure(Exception exception)
        => exception is ExplorerModel.ResourceNotFoundException
            or ExplorerModel.AccessDeniedException
            or ExplorerModel.UnauthorizedException
            || exception is AmazonServiceException aws
                && ((int)aws.StatusCode is (int)HttpStatusCode.Forbidden or (int)HttpStatusCode.NotFound
                    || SafeCode(aws.ErrorCode, "").Contains("NotConfigured", StringComparison.OrdinalIgnoreCase)
                    || SafeCode(aws.ErrorCode, "").Contains("Unsupported", StringComparison.OrdinalIgnoreCase));

    private static AwsResourceSummary MapExplorerResource(ExplorerModel.Resource resource)
    {
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        var properties = new List<AwsResourceProperty>();
        foreach (var property in resource.Properties ?? new List<ExplorerModel.ResourceProperty>())
        {
            var json = DocumentToRedactedJson(property.Data);
            properties.Add(new AwsResourceProperty(
                property.Name ?? "",
                json,
                ToOffset(property.LastReportedAt)));
            if (string.Equals(property.Name, "tags", StringComparison.OrdinalIgnoreCase)
                && property.Data.IsDictionary())
            {
                foreach (var pair in property.Data.AsDictionary())
                    tags[pair.Key] = pair.Value.IsString() ? pair.Value.AsString() : DocumentToRedactedJson(pair.Value);
            }
        }

        return new AwsResourceSummary(
            resource.Arn ?? "",
            resource.Service ?? "",
            resource.ResourceType ?? "",
            Normalize(resource.CfnResourceType),
            resource.Region ?? "",
            resource.OwningAccountId ?? "",
            ToOffset(resource.LastReportedAt),
            tags,
            properties,
            AwsResourceInventorySource.ResourceExplorer);
    }

    private static AwsResourceSummary MapTaggedResource(TaggingModel.ResourceTagMapping mapping)
    {
        var arn = mapping.ResourceARN ?? "";
        ParseArn(arn, out var service, out var region, out var accountId, out var resourceType);
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in mapping.Tags ?? new List<TaggingModel.Tag>())
            if (!string.IsNullOrEmpty(tag.Key))
                tags[tag.Key] = tag.Value ?? "";
        return new AwsResourceSummary(
            arn,
            service,
            resourceType,
            null,
            region,
            accountId,
            null,
            tags,
            Array.Empty<AwsResourceProperty>(),
            AwsResourceInventorySource.ResourceGroupsTaggingApi);
    }

    private static void ParseArn(
        string arn,
        out string service,
        out string region,
        out string accountId,
        out string resourceType)
    {
        var parts = arn.Split(':', 6);
        service = parts.Length > 2 ? parts[2] : "";
        region = parts.Length > 3 ? parts[3] : "";
        accountId = parts.Length > 4 ? parts[4] : "";
        var resource = parts.Length > 5 ? parts[5] : "";
        var separator = resource.IndexOfAny(new[] { '/', ':' });
        resourceType = separator > 0 ? resource[..separator] : resource;
    }

    private static bool MatchesResourceTypes(AwsResourceSummary resource, IReadOnlyList<string> filters)
    {
        if (filters.Count == 0)
            return true;
        return filters.Any(filter =>
            string.Equals(filter, resource.ResourceType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(filter, resource.CloudFormationResourceType, StringComparison.OrdinalIgnoreCase)
            || string.Equals(filter, $"{resource.Service}:{resource.ResourceType}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesTags(
        IReadOnlyDictionary<string, string> tags,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filters)
    {
        foreach (var filter in filters)
        {
            var matching = tags.FirstOrDefault(pair => string.Equals(pair.Key, filter.Key, StringComparison.OrdinalIgnoreCase));
            if (matching.Key is null)
                return false;
            if (filter.Value.Count > 0
                && !filter.Value.Any(value => string.Equals(value, matching.Value, StringComparison.Ordinal)))
                return false;
        }
        return true;
    }

    private static bool MatchesFreeText(AwsResourceSummary resource, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query == "*")
            return true;
        foreach (var rawToken in query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var token = rawToken.Trim('(', ')');
            if (token.Equals("AND", StringComparison.OrdinalIgnoreCase)
                || token.Equals("OR", StringComparison.OrdinalIgnoreCase))
                continue;
            if (token.StartsWith("service:", StringComparison.OrdinalIgnoreCase))
            {
                if (!WildcardContains(resource.Service, token[8..])) return false;
                continue;
            }
            if (token.StartsWith("region:", StringComparison.OrdinalIgnoreCase))
            {
                if (!WildcardContains(resource.Region, token[7..])) return false;
                continue;
            }
            if (token.StartsWith("resourcetype:", StringComparison.OrdinalIgnoreCase))
            {
                if (!WildcardContains(resource.ResourceType, token[13..])) return false;
                continue;
            }
            var needle = token.Trim('*', '"');
            if (needle.Length == 0) continue;
            var found = resource.Arn.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || resource.Tags.Any(pair => pair.Key.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || pair.Value.Contains(needle, StringComparison.OrdinalIgnoreCase));
            if (!found) return false;
        }
        return true;
    }

    private static bool WildcardContains(string value, string pattern)
        => value.Contains(pattern.Trim('*', '"'), StringComparison.OrdinalIgnoreCase);

    private static AwsCloudResource MapCloudResource(string typeName, CloudModel.ResourceDescription resource)
        => new(
            typeName,
            resource.Identifier ?? "",
            RedactJson(string.IsNullOrWhiteSpace(resource.Properties) ? "{}" : resource.Properties));

    private static AwsManagerResult<AwsCloudOperation> OperationAccepted(
        CloudModel.ProgressEvent? progress,
        string en,
        string zh)
    {
        if (progress is null)
            return AwsManagerResult<AwsCloudOperation>.Fail(new AwsManagerError(
                AwsManagerErrorKind.AwsService,
                "MissingProgressEvent",
                new AwsManagerText(
                    "AWS accepted the request but returned no operation token.",
                    "AWS 接受咗要求，但冇回傳 operation token。")));
        return AwsManagerResult<AwsCloudOperation>.Ok(MapCloudOperation(progress), en, zh);
    }

    private static AwsCloudOperation MapCloudOperation(CloudModel.ProgressEvent? progress)
    {
        progress ??= new CloudModel.ProgressEvent();
        var status = progress.OperationStatus?.Value ?? "UNKNOWN";
        var isTerminal = status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase)
            || status.Equals("FAILED", StringComparison.OrdinalIgnoreCase)
            || status.Equals("CANCEL_COMPLETE", StringComparison.OrdinalIgnoreCase);
        return new AwsCloudOperation(
            progress.RequestToken ?? "",
            progress.Operation?.Value ?? "UNKNOWN",
            status,
            progress.TypeName ?? "",
            Normalize(progress.Identifier),
            Normalize(RedactFreeText(progress.StatusMessage)),
            Normalize(progress.ErrorCode?.Value),
            string.IsNullOrWhiteSpace(progress.ResourceModel) ? null : RedactJson(progress.ResourceModel),
            ToOffset(progress.EventTime),
            ToOffset(progress.RetryAfter),
            isTerminal,
            status.Equals("SUCCESS", StringComparison.OrdinalIgnoreCase));
    }

    private static AwsS3LifecycleRule MapLifecycleRule(S3Model.LifecycleRule rule)
    {
        var filter = rule.Filter;
        var tags = new Dictionary<string, string>(StringComparer.Ordinal);
        if (filter?.Tag is { Key: not null } tag)
            tags[tag.Key] = tag.Value ?? "";
        foreach (var item in filter?.And?.Tags ?? new List<S3Model.Tag>())
            if (!string.IsNullOrEmpty(item.Key))
                tags[item.Key] = item.Value ?? "";
        var mappedFilter = new AwsS3LifecycleFilter(
            Normalize(filter?.And?.Prefix) ?? Normalize(filter?.Prefix),
            tags,
            filter?.And?.ObjectSizeGreaterThan ?? filter?.ObjectSizeGreaterThan,
            filter?.And?.ObjectSizeLessThan ?? filter?.ObjectSizeLessThan);

        var expiration = rule.Expiration is null
            ? null
            : new AwsS3LifecycleExpiration(
                rule.Expiration.Days,
                ToOffset(rule.Expiration.Date),
                rule.Expiration.ExpiredObjectDeleteMarker);
        var transitions = (rule.Transitions ?? new List<S3Model.LifecycleTransition>())
            .Select(value => new AwsS3LifecycleTransition(
                value.Days,
                ToOffset(value.Date),
                value.StorageClass?.Value ?? ""))
            .ToArray();
        var noncurrentExpiration = rule.NoncurrentVersionExpiration is null
            ? null
            : new AwsS3LifecycleExpiration(
                rule.NoncurrentVersionExpiration.NoncurrentDays,
                null,
                null,
                true,
                rule.NoncurrentVersionExpiration.NewerNoncurrentVersions);
        var noncurrentTransitions = (rule.NoncurrentVersionTransitions
                                     ?? new List<S3Model.LifecycleRuleNoncurrentVersionTransition>())
            .Select(value => new AwsS3LifecycleTransition(
                value.NoncurrentDays,
                null,
                value.StorageClass?.Value ?? "",
                true,
                value.NewerNoncurrentVersions))
            .ToArray();
        return new AwsS3LifecycleRule(
            rule.Id ?? "",
            string.Equals(rule.Status?.Value, "Enabled", StringComparison.OrdinalIgnoreCase),
            mappedFilter,
            expiration,
            transitions,
            noncurrentExpiration,
            noncurrentTransitions,
            rule.AbortIncompleteMultipartUpload?.DaysAfterInitiation);
    }

    private static S3Model.LifecycleRule? TryMapLifecycleRule(
        AwsS3LifecycleRule rule,
        out AwsManagerText validationError)
    {
        validationError = default;
        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            validationError = new AwsManagerText("Every lifecycle rule needs an ID.", "每條 lifecycle 規則都需要 ID。");
            return null;
        }
        if (rule.Transitions.Any(value => string.IsNullOrWhiteSpace(value.StorageClass))
            || rule.NoncurrentVersionTransitions.Any(value => string.IsNullOrWhiteSpace(value.StorageClass)))
        {
            validationError = new AwsManagerText("Every lifecycle transition needs a storage class.", "每個 lifecycle transition 都需要 storage class。");
            return null;
        }
        if (rule.Expiration is null
            && rule.Transitions.Count == 0
            && rule.NoncurrentVersionExpiration is null
            && rule.NoncurrentVersionTransitions.Count == 0
            && rule.AbortIncompleteMultipartUploadAfterDays is null)
        {
            validationError = new AwsManagerText("Every lifecycle rule needs at least one action.", "每條 lifecycle 規則都需要最少一個 action。");
            return null;
        }

        var tags = rule.Filter.Tags ?? new Dictionary<string, string>();
        var prefix = Normalize(rule.Filter.Prefix) ?? "";
        S3Model.LifecycleFilter sdkFilter;
        if (tags.Count > 0)
        {
            sdkFilter = new S3Model.LifecycleFilter
            {
                And = new S3Model.LifecycleRuleAndOperator
                {
                    Prefix = prefix,
                    Tags = tags.Select(pair => new S3Model.Tag { Key = pair.Key, Value = pair.Value }).ToList(),
                    ObjectSizeGreaterThan = rule.Filter.ObjectSizeGreaterThan,
                    ObjectSizeLessThan = rule.Filter.ObjectSizeLessThan,
                },
            };
        }
        else
        {
            sdkFilter = new S3Model.LifecycleFilter
            {
                Prefix = prefix,
                ObjectSizeGreaterThan = rule.Filter.ObjectSizeGreaterThan,
                ObjectSizeLessThan = rule.Filter.ObjectSizeLessThan,
            };
        }

        return new S3Model.LifecycleRule
        {
            Id = rule.Id.Trim(),
            Status = rule.Enabled ? Amazon.S3.LifecycleRuleStatus.Enabled : Amazon.S3.LifecycleRuleStatus.Disabled,
            Filter = sdkFilter,
            Expiration = rule.Expiration is null
                ? null
                : new S3Model.LifecycleRuleExpiration
                {
                    Days = rule.Expiration.Days,
                    Date = rule.Expiration.Date?.UtcDateTime,
                    ExpiredObjectDeleteMarker = rule.Expiration.ExpiredObjectDeleteMarker,
                },
            Transitions = rule.Transitions.Select(value => new S3Model.LifecycleTransition
            {
                Days = value.Days,
                Date = value.Date?.UtcDateTime,
                StorageClass = Amazon.S3.S3StorageClass.FindValue(value.StorageClass),
            }).ToList(),
            NoncurrentVersionExpiration = rule.NoncurrentVersionExpiration is null
                ? null
                : new S3Model.LifecycleRuleNoncurrentVersionExpiration
                {
                    NoncurrentDays = rule.NoncurrentVersionExpiration.Days,
                    NewerNoncurrentVersions = rule.NoncurrentVersionExpiration.NewerNoncurrentVersions,
                },
            NoncurrentVersionTransitions = rule.NoncurrentVersionTransitions.Select(value =>
                new S3Model.LifecycleRuleNoncurrentVersionTransition
                {
                    NoncurrentDays = value.Days,
                    NewerNoncurrentVersions = value.NewerNoncurrentVersions,
                    StorageClass = Amazon.S3.S3StorageClass.FindValue(value.StorageClass),
                }).ToList(),
            AbortIncompleteMultipartUpload = rule.AbortIncompleteMultipartUploadAfterDays is null
                ? null
                : new S3Model.LifecycleRuleAbortIncompleteMultipartUpload
                {
                    DaysAfterInitiation = rule.AbortIncompleteMultipartUploadAfterDays,
                },
        };
    }

    private static bool IsS3ConfigurationMissing(Exception exception, params string[] expectedCodes)
        => exception is AmazonS3Exception s3
            && expectedCodes.Any(code => string.Equals(code, s3.ErrorCode, StringComparison.OrdinalIgnoreCase));

    private static bool IsJsonKind(string? json, JsonValueKind expectedKind)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == expectedKind;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string DocumentToRedactedJson(Document document)
    {
        object? value;
        if (document.IsNull()) value = null;
        else if (document.IsBool()) value = document.AsBool();
        else if (document.IsDouble()) value = document.AsDouble();
        else if (document.IsInt()) value = document.AsInt();
        else if (document.IsLong()) value = document.AsLong();
        else if (document.IsString()) value = document.AsString();
        else if (document.IsList()) value = document.AsList().Select(DocumentToPlainObject).ToArray();
        else if (document.IsDictionary()) value = document.AsDictionary()
            .ToDictionary(pair => pair.Key, pair => DocumentToPlainObject(pair.Value), StringComparer.Ordinal);
        else value = document.ToString();
        return RedactJson(JsonSerializer.Serialize(value));
    }

    private static object? DocumentToPlainObject(Document document)
    {
        if (document.IsNull()) return null;
        if (document.IsBool()) return document.AsBool();
        if (document.IsDouble()) return document.AsDouble();
        if (document.IsInt()) return document.AsInt();
        if (document.IsLong()) return document.AsLong();
        if (document.IsString()) return document.AsString();
        if (document.IsList()) return document.AsList().Select(DocumentToPlainObject).ToArray();
        if (document.IsDictionary()) return document.AsDictionary()
            .ToDictionary(pair => pair.Key, pair => DocumentToPlainObject(pair.Value), StringComparer.Ordinal);
        return document.ToString();
    }

    private static string RedactJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                WriteRedactedElement(writer, document.RootElement);
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch (JsonException)
        {
            return RedactFreeText(json);
        }
    }

    private static void WriteRedactedElement(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (IsSensitivePropertyName(property.Name))
                    writer.WriteStringValue("[REDACTED]");
                else
                    WriteRedactedElement(writer, property.Value);
            }
            writer.WriteEndObject();
            return;
        }
        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
                WriteRedactedElement(writer, item);
            writer.WriteEndArray();
            return;
        }
        if (element.ValueKind == JsonValueKind.String)
            writer.WriteStringValue(RedactFreeText(element.GetString()));
        else
            element.WriteTo(writer);
    }

    private static bool IsSensitivePropertyName(string name)
    {
        var normalized = new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return normalized is "secret" or "secretkey" or "secretaccesskey" or "awssecretaccesskey"
            or "clientsecret" or "password" or "credential" or "credentials" or "privatekey"
            or "accesskey" or "accesskeyid" or "awsaccesskeyid" or "apikey" or "authorization"
            or "sessiontoken" or "accesstoken" or "refreshtoken" or "idtoken"
            || normalized.EndsWith("password", StringComparison.Ordinal)
            || normalized.EndsWith("privatekey", StringComparison.Ordinal)
            || normalized.EndsWith("secretaccesskey", StringComparison.Ordinal)
            || normalized.EndsWith("sessiontoken", StringComparison.Ordinal)
            || normalized.EndsWith("accesstoken", StringComparison.Ordinal)
            || normalized.EndsWith("refreshtoken", StringComparison.Ordinal);
    }

    private static string RedactFreeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var redacted = AccessKeyRegex.Replace(text, "[REDACTED_AWS_ACCESS_KEY]");
        redacted = SecretAssignmentRegex.Replace(redacted, match =>
        {
            var equals = match.Value.IndexOfAny(new[] { ':', '=' });
            return equals < 0 ? "[REDACTED]" : match.Value[..(equals + 1)] + "[REDACTED]";
        });
        return redacted.Length <= 2_000 ? redacted : redacted[..2_000] + "…";
    }

    private static string SafeCode(string? code, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(code) ? fallback : code;
        var safe = new string(value.Where(character => char.IsLetterOrDigit(character)
            || character is '.' or '_' or '-').Take(100).ToArray());
        return safe.Length == 0 ? "AwsServiceFailure" : safe;
    }

    private static string? SafeRequestId(string? requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return null;
        var safe = new string(requestId.Where(character => char.IsLetterOrDigit(character)
            || character is '-' or '_').Take(128).ToArray());
        return safe.Length == 0 ? null : safe;
    }

    private static bool IsValidBucketName(string? bucketName)
    {
        if (string.IsNullOrWhiteSpace(bucketName)) return false;
        var value = bucketName.Trim();
        if (value.Length is < 3 or > 63) return false;
        if (!char.IsLetterOrDigit(value[0]) || !char.IsLetterOrDigit(value[^1])) return false;
        if (value.Any(character => !(character is >= 'a' and <= 'z')
                && !char.IsDigit(character) && character is not '.' and not '-')) return false;
        if (value.Contains("..", StringComparison.Ordinal) || value.Contains(".-", StringComparison.Ordinal)
            || value.Contains("-.", StringComparison.Ordinal)) return false;
        if (IPAddress.TryParse(value, out _)) return false;
        if (value.StartsWith("xn--", StringComparison.Ordinal)
            || value.StartsWith("sthree-", StringComparison.Ordinal)
            || value.StartsWith("amzn_s3_demo_", StringComparison.Ordinal)
            || value.EndsWith("-s3alias", StringComparison.Ordinal)
            || value.EndsWith("--ol-s3", StringComparison.Ordinal)
            || value.EndsWith(".mrap", StringComparison.Ordinal)
            || value.EndsWith("--x-s3", StringComparison.Ordinal)
            || value.EndsWith("--table-s3", StringComparison.Ordinal)) return false;
        return true;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? EncodeTaggingContinuationToken(string? token)
        => string.IsNullOrWhiteSpace(token)
            ? null
            : TaggingContinuationPrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(token));

    private static bool TryDecodeTaggingContinuationToken(string? token, out string? taggingToken)
    {
        taggingToken = null;
        if (string.IsNullOrWhiteSpace(token)
            || !token.StartsWith(TaggingContinuationPrefix, StringComparison.Ordinal))
            return false;
        try
        {
            taggingToken = Encoding.UTF8.GetString(Convert.FromBase64String(token[TaggingContinuationPrefix.Length..]));
            return !string.IsNullOrWhiteSpace(taggingToken);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string? NormalizeEtag(string? value)
        => Normalize(value)?.Trim('"');

    private static DateTimeOffset? ToOffset(DateTime? value)
    {
        if (value is null) return null;
        var date = value.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : value.Value.ToUniversalTime();
        return new DateTimeOffset(date);
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { File.Delete(path); } catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sts?.Dispose();
        _resourceExplorer?.Dispose();
        _tagging?.Dispose();
        _cloudControl?.Dispose();
        _ec2?.Dispose();
        _s3?.Dispose();
    }
}
