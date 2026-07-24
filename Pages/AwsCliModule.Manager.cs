using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>Managed AWS SDK bindings for the Console shell, Resource Explorer, Cloud Control and S3.</summary>
public sealed partial class AwsCliModule
{
    private static readonly JsonSerializerOptions ManagerJson = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private AwsManagerService? _awsManager;
    private string? _awsManagerContextKey;
    private AwsS3VersioningConfiguration? _loadedVersioning;
    private AwsS3EncryptionConfiguration? _loadedEncryption;
    private AwsS3PublicAccessBlockConfiguration? _loadedPublicAccess;
    private AwsS3BucketTags? _loadedTags;
    private AwsS3BucketPolicy? _loadedPolicy;
    private AwsS3LifecycleConfiguration? _loadedLifecycle;
    private AwsS3CorsConfiguration? _loadedCors;

    private AwsManagerService EnsureAwsManager()
    {
        var profile = string.IsNullOrWhiteSpace(AwsCliService.ActiveProfile) ? null : AwsCliService.ActiveProfile.Trim();
        var region = string.IsNullOrWhiteSpace(AwsCliService.ActiveRegion) ? null : AwsCliService.ActiveRegion.Trim();
        var key = $"{profile ?? "<default>"}|{region ?? "<profile-region>"}";
        if (_awsManager is not null && string.Equals(key, _awsManagerContextKey, StringComparison.Ordinal))
            return _awsManager;

        if (_awsManager is not null)
        {
            _awsContext.Restart();
            _s3OperationCts?.Cancel();
            InvalidateConsoleAccountState();
        }
        _awsManager?.Dispose();
        _awsManager = new AwsManagerService(new AwsManagerSessionOptions
        {
            ProfileName = profile,
            RegionName = region,
        });
        _awsManagerContextKey = key;
        return _awsManager;
    }

    private partial void DisposeAwsManager()
    {
        _awsManager?.Dispose();
        _awsManager = null;
        _awsManagerContextKey = null;
    }

    private partial async Task RefreshConsoleContextAsync(bool loadResources, CancellationToken cancellationToken)
    {
        var manager = EnsureAwsManager();
        if (!TryCaptureAwsContext(out var contextGeneration, out var contextToken)) return;
        using var linkedSource = cancellationToken.CanBeCanceled && cancellationToken != contextToken
            ? CancellationTokenSource.CreateLinkedTokenSource(contextToken, cancellationToken)
            : null;
        var requestToken = linkedSource?.Token ?? contextToken;
        IdentityNameText.Text = P("Connecting…", "連線緊…");
        IdentityArnText.Text = manager.Context.CredentialSource;
        IdentityRegionText.Text = P(
            $"Region: {manager.Context.RegionDisplayName} ({manager.RegionName})",
            $"區域：{manager.Context.RegionDisplayName}（{manager.RegionName}）");

        var identity = await manager.GetCallerIdentityAsync(requestToken);
        if (requestToken.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
        if (identity.Success && identity.Value is { } caller)
        {
            IdentityNameText.Text = $"{FriendlyIdentity(caller.Arn)} · {caller.AccountId}";
            IdentityArnText.Text = caller.Arn;
            IdentityRegionText.Text = P(
                $"{manager.Context.CredentialSource} · {manager.Context.RegionDisplayName} ({manager.RegionName})",
                $"{manager.Context.CredentialSource} · {manager.Context.RegionDisplayName}（{manager.RegionName}）");
            ConsoleScopeText.Text = $"{caller.AccountId} · {manager.RegionName}";
        }
        else
        {
            IdentityNameText.Text = P("Not connected", "未連線");
            IdentityArnText.Text = ManagerMessage(identity.Message);
            ConsoleScopeText.Text = P("Sign in", "登入");
            if (loadResources) ShowManagerError(identity.Error, identity.Message);
        }

        if (!loadResources || !identity.Success) return;
        await Task.WhenAll(RefreshResourcesAsync(), RefreshS3Async());
    }

    private static string FriendlyIdentity(string arn)
    {
        if (string.IsNullOrWhiteSpace(arn)) return "AWS identity";
        var slash = arn.LastIndexOf('/');
        if (slash >= 0 && slash < arn.Length - 1) return arn[(slash + 1)..];
        var colon = arn.LastIndexOf(':');
        return colon >= 0 && colon < arn.Length - 1 ? arn[(colon + 1)..] : arn;
    }

    private partial Task RefreshResourcesAsync() => SearchResourcesPageAsync(append: false);

    private partial Task LoadMoreResourcesAsync() => SearchResourcesPageAsync(append: true);

    private async Task SearchResourcesPageAsync(bool append)
    {
        var manager = EnsureAwsManager();
        if (!TryCaptureAwsContext(out var contextGeneration, out var token)) return;
        if (append && string.IsNullOrWhiteSpace(_resourceNextToken)) return;
        ResourceProgress.IsActive = true;
        ResourceRefreshBtn.IsEnabled = false;
        ResourceLoadMoreBtn.IsEnabled = false;
        ResourceStatusText.Text = append
            ? P("Loading more AWS resources…", "載入緊更多 AWS 資源…")
            : P("Searching AWS resources…", "搜尋緊 AWS 資源…");
        try
        {
            string query;
            if (append)
            {
                query = _resourceActiveQuery ?? "*";
            }
            else
            {
                query = (ResourceQueryBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(query)) query = "*";
                if (!CrossRegionToggle.IsOn && !query.Contains("region:", StringComparison.OrdinalIgnoreCase))
                    query = query == "*" ? $"region:{manager.RegionName}" : $"{query} region:{manager.RegionName}";

                if (ResourceTypeFilter.SelectedIndex > 0 && ResourceTypeFilter.SelectedItem is string selected)
                {
                    var service = _consoleServices.FirstOrDefault(s => s.Name.Equals(selected, StringComparison.OrdinalIgnoreCase));
                    if (service is not null && !query.Contains("service:", StringComparison.OrdinalIgnoreCase))
                        query = query == "*" ? $"service:{service.ResourceNamespace}" : $"{query} service:{service.ResourceNamespace}";
                }
                _resourceActiveQuery = query;
                _resourceNextToken = null;
            }

            var result = await manager.SearchResourcesAsync(new AwsResourceSearchRequest
            {
                Query = query,
                MaxResults = 500,
                NextToken = append ? _resourceNextToken : null,
                UseTaggingFallback = true,
            }, token);
            token.ThrowIfCancellationRequested();
            if (!_awsContext.IsCurrent(contextGeneration)) return;
            if (!result.Success || result.Value is null)
            {
                ResourceStatusText.Text = ManagerMessage(result.Message);
                ShowManagerError(result.Error, result.Message);
                return;
            }

            if (!append) _resourceRows.Clear();
            foreach (var resource in result.Value.Items)
            {
                if (_resourceRows.Any(row => row.Arn.Equals(resource.Arn, StringComparison.OrdinalIgnoreCase)))
                    continue;
                var name = ResourceName(resource);
                var type = !string.IsNullOrWhiteSpace(resource.CloudFormationResourceType)
                    ? resource.CloudFormationResourceType!
                    : $"{resource.Service}:{resource.ResourceType}";
                var state = ResourceState(resource);
                var propertiesJson = JsonSerializer.Serialize(resource.Properties.ToDictionary(p => p.Name, p => p.JsonValue), ManagerJson);
                _resourceRows.Add(new AwsResourceRowView(
                    name,
                    resource.Arn,
                    type,
                    string.IsNullOrWhiteSpace(resource.Region) ? "Global" : resource.Region,
                    state,
                    resource.Service,
                    resource.CloudFormationResourceType,
                    null,
                    propertiesJson));
            }

            _resourceNextToken = result.Value.NextToken;
            ApplyResourceFilter();
            ResourceCountText.Text = _resourceRows.Count.ToString();
            ResourceLoadMoreBtn.Visibility = result.Value.HasMore ? Visibility.Visible : Visibility.Collapsed;
            ResourceLoadMoreBtn.IsEnabled = result.Value.HasMore;
            ResourceStatusText.Text = P(
                $"{_resourceRows.Count} resources · {result.Value.Source}{(result.Value.HasMore ? " · more available" : "")}",
                $"{_resourceRows.Count} 個資源 · {result.Value.Source}{(result.Value.HasMore ? " · 仲有更多" : "")}");
        }
        catch (OperationCanceledException)
        {
            if (_awsContext.IsCurrent(contextGeneration))
                ResourceStatusText.Text = P("Resource search cancelled.", "已取消資源搜尋。");
        }
        finally
        {
            if (_awsContext.IsCurrent(contextGeneration))
            {
                ResourceProgress.IsActive = false;
                ResourceRefreshBtn.IsEnabled = true;
                ResourceLoadMoreBtn.IsEnabled = !string.IsNullOrWhiteSpace(_resourceNextToken);
            }
        }
    }

    private static string ResourceName(AwsResourceSummary resource)
    {
        if (resource.Tags.TryGetValue("Name", out var name) && !string.IsNullOrWhiteSpace(name)) return name;
        var property = resource.Properties.FirstOrDefault(p => p.Name.Equals("Name", StringComparison.OrdinalIgnoreCase));
        if (property is not null && !string.IsNullOrWhiteSpace(property.JsonValue)) return UnquoteJson(property.JsonValue);
        var arn = resource.Arn.TrimEnd('/');
        var slash = arn.LastIndexOf('/');
        if (slash >= 0 && slash < arn.Length - 1) return arn[(slash + 1)..];
        var colon = arn.LastIndexOf(':');
        return colon >= 0 && colon < arn.Length - 1 ? arn[(colon + 1)..] : arn;
    }

    private static string ResourceState(AwsResourceSummary resource)
    {
        var property = resource.Properties.FirstOrDefault(p =>
            p.Name.Contains("status", StringComparison.OrdinalIgnoreCase)
            || p.Name.Contains("state", StringComparison.OrdinalIgnoreCase));
        return property is null || string.IsNullOrWhiteSpace(property.JsonValue)
            ? "Discovered"
            : UnquoteJson(property.JsonValue);
    }

    private static string UnquoteJson(string value)
    {
        try
        {
            using var doc = JsonDocument.Parse(value);
            return doc.RootElement.ValueKind == JsonValueKind.String
                ? doc.RootElement.GetString() ?? value
                : doc.RootElement.ToString();
        }
        catch { return value.Trim('"'); }
    }

    private partial Task RefreshEc2Async() => LoadEc2PageAsync(append: false);

    private partial Task LoadMoreEc2Async() => LoadEc2PageAsync(append: true);

    private async Task LoadEc2PageAsync(bool append)
    {
        var manager = EnsureAwsManager();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        if (append && string.IsNullOrWhiteSpace(_ec2NextToken)) return;
        var operation = BeginEc2Operation();
        var token = operation.Token;
        var selectedId = (Ec2InstanceList.SelectedItem as AwsEc2InstanceRowView)?.InstanceId;
        if (!append)
        {
            _ec2NextToken = null;
            Ec2LoadMoreBtn.Visibility = Visibility.Collapsed;
        }
        Ec2Progress.IsActive = true;
        Ec2RefreshBtn.IsEnabled = false;
        Ec2LoadMoreBtn.IsEnabled = false;
        SetEc2ActionButtonsEnabled(false);
        Ec2StatusText.Text = append
            ? P("Loading more EC2 instances…", "載入緊更多 EC2 執行個體…")
            : P($"Loading EC2 instances from {manager.RegionName}…", $"由 {manager.RegionName} 載入緊 EC2 執行個體…");
        try
        {
            var result = await manager.ListEc2InstancesAsync(new AwsEc2ListInstancesRequest
            {
                MaxResults = 100,
                NextToken = append ? _ec2NextToken : null,
            }, token);
            token.ThrowIfCancellationRequested();
            if (!_awsContext.IsCurrent(contextGeneration)) return;
            if (!result.Success || result.Value is null)
            {
                if (!append)
                {
                    _ec2Rows.Clear();
                    _ec2NextToken = null;
                    Ec2LoadMoreBtn.Visibility = Visibility.Collapsed;
                    ApplyEc2Filter();
                    ResetEc2Details();
                }
                Ec2StatusText.Text = ManagerMessage(result.Message);
                ShowManagerError(result.Error, result.Message);
                _ec2LoadedContextGeneration = contextGeneration;
                return;
            }

            if (!append) _ec2Rows.Clear();
            foreach (var instance in result.Value.Items)
            {
                if (_ec2Rows.Any(row => row.InstanceId.Equals(instance.InstanceId, StringComparison.Ordinal)))
                    continue;
                _ec2Rows.Add(ToEc2Row(instance));
            }
            _ec2Rows.Sort((left, right) =>
            {
                var byName = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
                return byName != 0 ? byName : StringComparer.Ordinal.Compare(left.InstanceId, right.InstanceId);
            });
            _ec2NextToken = result.Value.NextToken;
            _ec2LoadedContextGeneration = contextGeneration;
            ApplyEc2Filter();
            if (!string.IsNullOrWhiteSpace(selectedId))
                Ec2InstanceList.SelectedItem = Ec2InstanceList.Items.OfType<AwsEc2InstanceRowView>()
                    .FirstOrDefault(row => row.InstanceId.Equals(selectedId, StringComparison.Ordinal));
            Ec2LoadMoreBtn.Visibility = result.Value.HasMore ? Visibility.Visible : Visibility.Collapsed;
            Ec2LoadMoreBtn.IsEnabled = result.Value.HasMore;
            Ec2StatusText.Text = P(
                $"{_ec2Rows.Count} instance(s) loaded from {manager.RegionName}{(result.Value.HasMore ? " · more available" : string.Empty)}.",
                $"已由 {manager.RegionName} 載入 {_ec2Rows.Count} 個執行個體{(result.Value.HasMore ? " · 仲有更多" : string.Empty)}。");
        }
        catch (OperationCanceledException)
        {
            if (_awsContext.IsCurrent(contextGeneration))
                Ec2StatusText.Text = P("EC2 refresh cancelled.", "已取消 EC2 重新整理。");
        }
        finally
        {
            var ownsOperation = IsCurrentEc2Operation(operation.Id);
            if (ownsOperation) EndEc2Operation(operation.Id);
            if (ownsOperation && _awsContext.IsCurrent(contextGeneration))
            {
                Ec2Progress.IsActive = false;
                Ec2RefreshBtn.IsEnabled = true;
                Ec2LoadMoreBtn.IsEnabled = !string.IsNullOrWhiteSpace(_ec2NextToken);
                if (Ec2InstanceList.SelectedItem is AwsEc2InstanceRowView current)
                    SetEc2ActionButtonsForState(current.State);
            }
        }
    }

    private static AwsEc2InstanceRowView ToEc2Row(AwsEc2Instance instance) => new(
        instance.Name,
        instance.InstanceId,
        instance.State,
        instance.InstanceType,
        instance.AvailabilityZone,
        instance.PrivateIpAddress ?? "—",
        instance.PublicIpAddress ?? "—",
        instance);

    private partial async Task ChangeEc2StateAsync(AwsEc2InstanceAction action)
    {
        if (_ec2ReviewPending) return;
        var manager = EnsureAwsManager();
        if (Ec2InstanceList.SelectedItem is not AwsEc2InstanceRowView row
            || !AwsEc2InstancePolicy.IsAllowed(action, row.State)) return;
        if (!TryCaptureAwsContext(out var reviewGeneration, out _)) return;

        var confirmed = false;
        _ec2ReviewPending = true;
        SetEc2ActionButtonsEnabled(false);
        try
        {
            confirmed = await ConfirmEc2ActionAsync(row, action);
        }
        finally
        {
            _ec2ReviewPending = false;
            if (_awsContext.IsCurrent(reviewGeneration)
                && Ec2InstanceList.SelectedItem is AwsEc2InstanceRowView current)
                SetEc2ActionButtonsForState(current.State);
        }
        if (!confirmed) return;
        if (!_awsContext.IsCurrent(reviewGeneration)
            || Ec2InstanceList.SelectedItem is not AwsEc2InstanceRowView reviewed
            || !reviewed.InstanceId.Equals(row.InstanceId, StringComparison.Ordinal))
        {
            Ec2StatusText.Text = P(
                "AWS context or selection changed; the EC2 action was not submitted.",
                "AWS 情境或選擇已變更；未有提交 EC2 操作。");
            return;
        }

        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        if (contextGeneration != reviewGeneration) return;
        var operation = BeginEc2Operation();
        var token = operation.Token;
        Ec2Progress.IsActive = true;
        Ec2RefreshBtn.IsEnabled = false;
        Ec2LoadMoreBtn.IsEnabled = false;
        SetEc2ActionButtonsEnabled(false);
        var actionLabel = AwsEc2InstancePolicy.ActionLabel(action);
        Ec2StatusText.Text = P(
            $"Submitting {actionLabel.En} for {row.InstanceId}…",
            $"提交緊 {row.InstanceId} 嘅{actionLabel.Zh}要求…");
        try
        {
            var result = await manager.ChangeEc2InstanceStateAsync(row.InstanceId, action, row.State, token);
            token.ThrowIfCancellationRequested();
            if (!_awsContext.IsCurrent(contextGeneration)) return;
            if (!result.Success || result.Value is null)
            {
                Ec2StatusText.Text = ManagerMessage(result.Message);
                ShowManagerError(result.Error, result.Message);
                return;
            }

            var index = _ec2Rows.FindIndex(item => item.InstanceId.Equals(row.InstanceId, StringComparison.Ordinal));
            if (index >= 0)
                _ec2Rows[index] = ToEc2Row(_ec2Rows[index].Instance with { State = result.Value.CurrentState });
            ApplyEc2Filter();
            Ec2InstanceList.SelectedItem = Ec2InstanceList.Items.OfType<AwsEc2InstanceRowView>()
                .FirstOrDefault(item => item.InstanceId.Equals(row.InstanceId, StringComparison.Ordinal));
            Ec2StatusText.Text = ManagerMessage(result.Message);
            ShowManagerSuccess(result.Message);
        }
        catch (OperationCanceledException)
        {
            if (_awsContext.IsCurrent(contextGeneration))
                Ec2StatusText.Text = P("EC2 action cancelled.", "已取消 EC2 操作。");
        }
        finally
        {
            var ownsOperation = IsCurrentEc2Operation(operation.Id);
            if (ownsOperation) EndEc2Operation(operation.Id);
            if (ownsOperation && _awsContext.IsCurrent(contextGeneration))
            {
                Ec2Progress.IsActive = false;
                Ec2RefreshBtn.IsEnabled = true;
                Ec2LoadMoreBtn.IsEnabled = !string.IsNullOrWhiteSpace(_ec2NextToken);
                if (Ec2InstanceList.SelectedItem is AwsEc2InstanceRowView current)
                    SetEc2ActionButtonsForState(current.State);
            }
        }
    }

    private async Task<bool> ConfirmEc2ActionAsync(AwsEc2InstanceRowView row, AwsEc2InstanceAction action)
    {
        if (action == AwsEc2InstanceAction.Terminate)
        {
            return await ConfirmTypedAsync(
                P("Permanently terminate EC2 instance", "永久終止 EC2 執行個體"),
                P(
                    $"Terminate {row.Name} ({row.InstanceId})? Termination protection may block this request. Volumes with DeleteOnTermination enabled will also be deleted. Type the exact instance ID to continue.",
                    $"終止 {row.Name}（{row.InstanceId}）？終止保護可能會封鎖要求；已啟用 DeleteOnTermination 嘅磁碟區亦會一併刪除。輸入完整執行個體 ID 先繼續。"),
                row.InstanceId,
                "Terminate permanently",
                "永久終止");
        }

        var (titleEn, titleZh, messageEn, messageZh, primaryEn, primaryZh) = action switch
        {
            AwsEc2InstanceAction.Start => (
                "Start EC2 instance", "啟動 EC2 執行個體",
                $"Start {row.Name} ({row.InstanceId})? Compute and attached-service charges may resume.",
                $"啟動 {row.Name}（{row.InstanceId}）？運算同已連接服務可能會重新開始收費。",
                "Start instance", "啟動執行個體"),
            AwsEc2InstanceAction.Stop => (
                "Stop EC2 instance", "停止 EC2 執行個體",
                $"Stop {row.Name} ({row.InstanceId})? In-memory work will be lost and the workload will become unavailable.",
                $"停止 {row.Name}（{row.InstanceId}）？記憶體內工作會遺失，服務亦會暫時用唔到。",
                "Stop instance", "停止執行個體"),
            AwsEc2InstanceAction.Reboot => (
                "Reboot EC2 instance", "重新啟動 EC2 執行個體",
                $"Reboot {row.Name} ({row.InstanceId})? The workload will be interrupted.",
                $"重新啟動 {row.Name}（{row.InstanceId}）？工作負載會中斷。",
                "Reboot instance", "重新啟動執行個體"),
            _ => ("Change EC2 instance", "變更 EC2 執行個體", "Review this instance action.", "請覆核呢個執行個體操作。", "Continue", "繼續"),
        };
        var dialog = new ContentDialog
        {
            Title = P(titleEn, titleZh),
            Content = new TextBlock { Text = P(messageEn, messageZh), TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            PrimaryButtonText = P(primaryEn, primaryZh),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private (int Id, CancellationToken Token) BeginEc2Operation()
    {
        _ec2OperationCts?.Cancel();
        _ec2OperationCts?.Dispose();
        _ec2OperationId++;
        if (!TryCaptureAwsContext(out _, out var contextToken))
            return (_ec2OperationId, new CancellationToken(canceled: true));
        _ec2OperationCts = CancellationTokenSource.CreateLinkedTokenSource(contextToken);
        return (_ec2OperationId, _ec2OperationCts.Token);
    }

    private bool IsCurrentEc2Operation(int operationId)
        => operationId == _ec2OperationId && _ec2OperationCts is not null;

    private void EndEc2Operation(int operationId)
    {
        if (!IsCurrentEc2Operation(operationId)) return;
        _ec2OperationCts!.Dispose();
        _ec2OperationCts = null;
    }

    private void SetEc2ActionButtonsEnabled(bool enabled)
    {
        Ec2StartBtn.IsEnabled = enabled;
        Ec2StopBtn.IsEnabled = enabled;
        Ec2RebootBtn.IsEnabled = enabled;
        Ec2TerminateBtn.IsEnabled = enabled;
    }

    private void SetEc2ActionButtonsForState(string state)
    {
        if (_ec2ReviewPending || _ec2OperationCts is not null)
        {
            SetEc2ActionButtonsEnabled(false);
            return;
        }
        Ec2StartBtn.IsEnabled = AwsEc2InstancePolicy.IsAllowed(AwsEc2InstanceAction.Start, state);
        Ec2StopBtn.IsEnabled = AwsEc2InstancePolicy.IsAllowed(AwsEc2InstanceAction.Stop, state);
        Ec2RebootBtn.IsEnabled = AwsEc2InstancePolicy.IsAllowed(AwsEc2InstanceAction.Reboot, state);
        Ec2TerminateBtn.IsEnabled = AwsEc2InstancePolicy.IsAllowed(AwsEc2InstanceAction.Terminate, state);
    }

    private partial async Task RefreshS3Async()
    {
        var manager = EnsureAwsManager();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        S3BucketList.SelectedItem = null;
        ResetS3WorkspaceForBucketChange();
        using var operation = BeginS3Operation();
        var token = operation.Token;
        S3Progress.IsActive = true;
        S3RefreshBtn.IsEnabled = false;
        S3StatusText.Text = P("Loading S3 buckets…", "載入緊 S3 儲存桶…");
        try
        {
            var result = await manager.ListBucketsAsync(new AwsS3ListBucketsRequest { MaxResults = 10_000 }, token);
            token.ThrowIfCancellationRequested();
            if (!_awsContext.IsCurrent(contextGeneration)) return;
            if (!result.Success || result.Value is null)
            {
                S3StatusText.Text = ManagerMessage(result.Message);
                ShowManagerError(result.Error, result.Message);
                return;
            }

            _bucketRows.Clear();
            foreach (var bucket in result.Value.Items)
                _bucketRows.Add(new S3BucketRowView(
                    bucket.Name,
                    string.IsNullOrWhiteSpace(bucket.Region) ? P("discover on open", "開啟時偵測") : bucket.Region!,
                    bucket.CreationDate?.LocalDateTime.ToString("g") ?? "—"));
            ApplyBucketFilter();
            BucketCountText.Text = _bucketRows.Count.ToString();
            S3StatusText.Text = P($"{_bucketRows.Count} buckets loaded.", $"已載入 {_bucketRows.Count} 個儲存桶。");
        }
        catch (OperationCanceledException)
        {
            if (_awsContext.IsCurrent(contextGeneration))
                S3StatusText.Text = P("S3 refresh cancelled.", "已取消 S3 重新整理。");
        }
        finally
        {
            if (operation.IsCurrent && _awsContext.IsCurrent(contextGeneration))
            {
                S3Progress.IsActive = false;
                S3RefreshBtn.IsEnabled = true;
            }
        }
    }

    private partial async Task LoadSelectedBucketAsync()
    {
        var manager = EnsureAwsManager();
        if (S3BucketList.SelectedItem is not S3BucketRowView bucket) return;
        var bucketName = bucket.Name;
        var loadGeneration = _s3BucketLoadGeneration;
        using var operation = BeginS3Operation();
        var token = operation.Token;
        S3Progress.IsActive = true;
        S3StatusText.Text = P($"Loading {bucketName}…", $"載入緊 {bucketName}…");
        try
        {
            var objectsTask = LoadObjectsCoreAsync(bucketName, token);
            var versioningTask = manager.GetBucketVersioningAsync(bucketName, token);
            var encryptionTask = manager.GetBucketEncryptionAsync(bucketName, token);
            var publicTask = manager.GetPublicAccessBlockAsync(bucketName, token);
            var tagsTask = manager.GetBucketTagsAsync(bucketName, token);
            var policyTask = manager.GetBucketPolicyAsync(bucketName, token);
            var lifecycleTask = manager.GetLifecycleConfigurationAsync(bucketName, token);
            var corsTask = manager.GetCorsConfigurationAsync(bucketName, token);
            await Task.WhenAll(objectsTask, versioningTask, encryptionTask, publicTask, tagsTask, policyTask, lifecycleTask, corsTask);

            token.ThrowIfCancellationRequested();
            if (loadGeneration != _s3BucketLoadGeneration
                || S3BucketList.SelectedItem is not S3BucketRowView current
                || !string.Equals(current.Name, bucketName, StringComparison.Ordinal)) return;

            var objectsLoaded = await objectsTask;
            var versioningResult = await versioningTask;
            var encryptionResult = await encryptionTask;
            var publicResult = await publicTask;
            var tagsResult = await tagsTask;
            var policyResult = await policyTask;
            var lifecycleResult = await lifecycleTask;
            var corsResult = await corsTask;

            _suppressS3Settings = true;
            _loadedVersioning = versioningResult.Success ? versioningResult.Value : null;
            _loadedEncryption = encryptionResult.Success ? encryptionResult.Value : null;
            _loadedPublicAccess = publicResult.Success ? publicResult.Value : null;
            _loadedTags = tagsResult.Success ? tagsResult.Value : null;
            _loadedPolicy = policyResult.Success ? policyResult.Value : null;
            _loadedLifecycle = lifecycleResult.Success ? lifecycleResult.Value : null;
            _loadedCors = corsResult.Success ? corsResult.Value : null;

            if (_loadedVersioning is not null)
                S3VersioningToggle.IsOn = string.Equals(_loadedVersioning.Status, "Enabled", StringComparison.OrdinalIgnoreCase);
            var rule = _loadedEncryption?.Rules.FirstOrDefault();
            var encryptionIndex = rule?.Algorithm switch
            {
                "AES256" => 0,
                "aws:kms" => 1,
                "aws:kms:dsse" => 2,
                null or "" => 3,
                _ => -1,
            };
            if (_loadedEncryption is not null)
            {
                S3EncryptionBox.SelectedIndex = encryptionIndex;
                S3KmsKeyBox.Text = rule?.KmsKeyId ?? string.Empty;
                S3BucketKeyToggle.IsOn = rule?.BucketKeyEnabled == true;
                S3BlockSseCToggle.IsOn = string.Equals(
                    rule?.BlockedEncryptionType, "SSE-C", StringComparison.OrdinalIgnoreCase);
            }
            if (_loadedPublicAccess is not null)
            {
                S3BlockPublicAclsToggle.IsOn = _loadedPublicAccess.BlockPublicAcls;
                S3IgnorePublicAclsToggle.IsOn = _loadedPublicAccess.IgnorePublicAcls;
                S3BlockPublicPolicyToggle.IsOn = _loadedPublicAccess.BlockPublicPolicy;
                S3RestrictPublicBucketsToggle.IsOn = _loadedPublicAccess.RestrictPublicBuckets;
                S3BlockPublicToggle.IsOn = _loadedPublicAccess is
                {
                    BlockPublicAcls: true,
                    IgnorePublicAcls: true,
                    BlockPublicPolicy: true,
                    RestrictPublicBuckets: true,
                };
            }
            if (_loadedPolicy is not null)
                S3PolicyBox.Text = _loadedPolicy.PolicyJson is { Length: > 0 } policy ? PrettyJson(policy) : string.Empty;
            if (_loadedLifecycle is not null)
                S3LifecycleBox.Text = JsonSerializer.Serialize(_loadedLifecycle, ManagerJson);
            if (_loadedCors is not null)
                S3CorsBox.Text = JsonSerializer.Serialize(_loadedCors, ManagerJson);
            if (_loadedTags is not null)
                S3TagsBox.Text = string.Join(Environment.NewLine,
                    _loadedTags.Tags.OrderBy(t => t.Key).Select(t => $"{t.Key}={t.Value}"));
            S3BucketRegionText.Text = P($"Bucket: {bucketName} · {bucket.Region}", $"儲存桶：{bucketName} · {bucket.Region}");

            _loadedS3BucketName = bucketName;
            S3UploadBtn.IsEnabled = true;
            S3DeleteBtn.IsEnabled = true;
            S3PrefixBox.IsEnabled = objectsLoaded;
            S3ObjectList.IsEnabled = objectsLoaded;
            S3ObjectLoadMoreBtn.Visibility = objectsLoaded && !string.IsNullOrWhiteSpace(_s3ObjectNextToken)
                ? Visibility.Visible
                : Visibility.Collapsed;
            S3ObjectLoadMoreBtn.IsEnabled = objectsLoaded && !string.IsNullOrWhiteSpace(_s3ObjectNextToken);
            S3VersioningToggle.IsEnabled = versioningResult.Success && _loadedVersioning is not null;
            S3EncryptionBox.IsEnabled = encryptionResult.Success && _loadedEncryption is not null && encryptionIndex >= 0;
            S3BlockSseCToggle.IsEnabled = S3EncryptionBox.IsEnabled;
            S3BlockPublicToggle.IsEnabled = publicResult.Success && _loadedPublicAccess is not null;
            S3BlockPublicAclsToggle.IsEnabled = S3BlockPublicToggle.IsEnabled;
            S3IgnorePublicAclsToggle.IsEnabled = S3BlockPublicToggle.IsEnabled;
            S3BlockPublicPolicyToggle.IsEnabled = S3BlockPublicToggle.IsEnabled;
            S3RestrictPublicBucketsToggle.IsEnabled = S3BlockPublicToggle.IsEnabled;
            S3PolicyBox.IsEnabled = policyResult.Success && _loadedPolicy is not null;
            S3LifecycleBox.IsEnabled = lifecycleResult.Success && _loadedLifecycle is not null;
            S3CorsBox.IsEnabled = corsResult.Success && _loadedCors is not null;
            S3TagsBox.IsEnabled = tagsResult.Success && _loadedTags is not null;
            S3SavePropertiesBtn.IsEnabled = S3VersioningToggle.IsEnabled || S3EncryptionBox.IsEnabled;
            S3SavePermissionsBtn.IsEnabled = S3BlockPublicToggle.IsEnabled || S3PolicyBox.IsEnabled;
            S3SaveManagementBtn.IsEnabled = S3LifecycleBox.IsEnabled || S3CorsBox.IsEnabled;
            S3SaveTagsBtn.IsEnabled = S3TagsBox.IsEnabled;
            S3Setting_Changed(this, new RoutedEventArgs());
            _suppressS3Settings = false;
            ResetS3DirtyFlags();

            AwsManagerError? firstError = null;
            AwsManagerText? firstFailureMessage = null;
            var failureCount = 0;
            void NoteFailure<T>(AwsManagerResult<T> result)
            {
                if (result.Success) return;
                failureCount++;
                firstError ??= result.Error;
                firstFailureMessage ??= result.Message;
            }
            NoteFailure(versioningResult);
            NoteFailure(encryptionResult);
            NoteFailure(publicResult);
            NoteFailure(tagsResult);
            NoteFailure(policyResult);
            NoteFailure(lifecycleResult);
            NoteFailure(corsResult);
            if (failureCount == 0 && objectsLoaded)
            {
                S3StatusText.Text = P($"{bucketName} ready.", $"{bucketName} 已就緒。");
            }
            else
            {
                S3StatusText.Text = P(
                    $"{bucketName} loaded with {failureCount + (objectsLoaded ? 0 : 1)} unavailable section(s). Disabled editors preserve unknown settings.",
                    $"{bucketName} 已載入；有 {failureCount + (objectsLoaded ? 0 : 1)} 個部分未能讀取。已停用嘅編輯器會保留未知設定。");
                if (firstFailureMessage is { } message) ShowManagerError(firstError, message);
            }
        }
        catch (OperationCanceledException)
        {
            if (loadGeneration == _s3BucketLoadGeneration)
                S3StatusText.Text = P("Bucket load cancelled.", "已取消載入儲存桶。");
        }
        catch (Exception)
        {
            if (loadGeneration == _s3BucketLoadGeneration)
            {
                S3StatusText.Text = P("The bucket workspace could not be rendered safely.", "無法安全顯示儲存桶工作區。");
                ShowManagerError(null, new AwsManagerText(
                    "The bucket workspace could not be rendered. No settings were changed.",
                    "無法顯示儲存桶工作區；未有修改任何設定。"));
            }
        }
        finally
        {
            if (operation.IsCurrent && loadGeneration == _s3BucketLoadGeneration)
            {
                _suppressS3Settings = false;
                S3Progress.IsActive = false;
            }
        }
    }

    private partial async Task LoadObjectsAsync()
    {
        if (!TryGetLoadedS3Bucket(out var bucket)) return;
        using var operation = BeginS3Operation();
        S3Progress.IsActive = true;
        try { await LoadObjectsCoreAsync(bucket.Name, operation.Token); }
        finally { if (operation.IsCurrent) S3Progress.IsActive = false; }
    }

    private partial async Task LoadMoreS3ObjectsAsync()
    {
        if (!TryGetLoadedS3Bucket(out var bucket) || string.IsNullOrWhiteSpace(_s3ObjectNextToken)) return;
        using var operation = BeginS3Operation();
        S3Progress.IsActive = true;
        S3ObjectLoadMoreBtn.IsEnabled = false;
        try { await LoadObjectsCoreAsync(bucket.Name, operation.Token, append: true); }
        finally { if (operation.IsCurrent) S3Progress.IsActive = false; }
    }

    private async Task<bool> LoadObjectsCoreAsync(string bucketName, CancellationToken token, bool append = false)
    {
        var requestedPrefix = _activeS3Prefix;
        if (append && (string.IsNullOrWhiteSpace(_s3ObjectNextToken)
                       || !string.Equals(_s3ObjectPageBucket, bucketName, StringComparison.Ordinal)
                       || !string.Equals(_s3ObjectPagePrefix, requestedPrefix, StringComparison.Ordinal))) return false;
        var result = await EnsureAwsManager().ListObjectsAsync(new AwsS3ListObjectsRequest
        {
            BucketName = bucketName,
            Prefix = requestedPrefix,
            Delimiter = "/",
            MaxResults = 1_000,
            NextToken = append ? _s3ObjectNextToken : null,
        }, token);
        if (token.IsCancellationRequested) return false;
        if (S3BucketList.SelectedItem is not S3BucketRowView selected
            || !string.Equals(selected.Name, bucketName, StringComparison.Ordinal)
            || !string.Equals(_activeS3Prefix, requestedPrefix, StringComparison.Ordinal)) return false;
        if (!result.Success || result.Value is null)
        {
            if (!append)
            {
                _objectRows.Clear();
                S3ObjectList.Items.Clear();
                S3ObjectList.SelectedItem = null;
                S3DownloadBtn.IsEnabled = false;
                _s3ObjectNextToken = null;
                _s3ObjectPageBucket = null;
                _s3ObjectPagePrefix = null;
                S3ObjectLoadMoreBtn.Visibility = Visibility.Collapsed;
            }
            S3StatusText.Text = ManagerMessage(result.Message);
            ShowManagerError(result.Error, result.Message);
            return false;
        }

        if (!append) _objectRows.Clear();
        foreach (var prefix in result.Value.CommonPrefixes)
        {
            if (_objectRows.Any(row => row.IsPrefix && row.Key.Equals(prefix, StringComparison.Ordinal))) continue;
            var name = prefix.TrimEnd('/').Split('/').LastOrDefault() ?? prefix;
            _objectRows.Add(new S3ObjectRowView(bucketName, name + "/", prefix, "", "", "\uE8B7", true));
        }
        foreach (var item in result.Value.Objects.Where(o => !o.Key.Equals(requestedPrefix, StringComparison.Ordinal)))
        {
            if (_objectRows.Any(row => !row.IsPrefix && row.Key.Equals(item.Key, StringComparison.Ordinal))) continue;
            var name = item.Key.StartsWith(requestedPrefix, StringComparison.Ordinal)
                ? item.Key[requestedPrefix.Length..]
                : item.Key;
            _objectRows.Add(new S3ObjectRowView(
                bucketName,
                name,
                item.Key,
                FormatBytes(item.Size),
                item.LastModified?.LocalDateTime.ToString("g") ?? "—",
                "\uE8A5",
                false));
        }
        _s3ObjectNextToken = result.Value.NextToken;
        _s3ObjectPageBucket = bucketName;
        _s3ObjectPagePrefix = requestedPrefix;
        S3ObjectList.Items.Clear();
        foreach (var row in _objectRows) S3ObjectList.Items.Add(row);
        S3UpBtn.IsEnabled = !string.IsNullOrEmpty(requestedPrefix);
        S3ObjectLoadMoreBtn.Visibility = result.Value.HasMore ? Visibility.Visible : Visibility.Collapsed;
        S3ObjectLoadMoreBtn.IsEnabled = result.Value.HasMore && HasLoadedS3Bucket();
        S3StatusText.Text = P(
            $"{_objectRows.Count(row => row.IsPrefix)} prefixes · {_objectRows.Count(row => !row.IsPrefix)} objects{(result.Value.HasMore ? " · more available" : "")}",
            $"{_objectRows.Count(row => row.IsPrefix)} 個前綴 · {_objectRows.Count(row => !row.IsPrefix)} 個物件{(result.Value.HasMore ? " · 仲有更多" : "")}");
        return true;
    }

    private partial async Task CreateBucketAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryCaptureAwsContext(out var reviewGeneration, out _)) return;
        var name = new TextBox { Header = P("Bucket name", "儲存桶名稱"), PlaceholderText = "example-company-assets" };
        var region = new TextBlock
        {
            Text = P($"Region: {manager.RegionName} (change it in the top bar first)",
                $"區域：{manager.RegionName}（如要更改，請先用頂部列）"),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var objectLock = new ToggleSwitch { Header = P("Enable Object Lock (cannot be disabled later)", "啟用 Object Lock（之後唔可以關閉）") };
        var versioning = new ToggleSwitch { Header = P("Enable versioning", "啟用版本控制") };
        var blockPublic = new ToggleSwitch { Header = P("Block all public access", "封鎖所有公開存取"), IsOn = true };
        var encryption = new ComboBox { Header = P("Default encryption", "預設加密"), HorizontalAlignment = HorizontalAlignment.Stretch };
        encryption.Items.Add("SSE-S3 (AES-256)");
        encryption.Items.Add("SSE-KMS (alias/aws/s3)");
        encryption.Items.Add("DSSE-KMS (alias/aws/s3)");
        encryption.SelectedIndex = 0;
        var kmsKey = new TextBox
        {
            Header = P("KMS key ARN / alias", "KMS key ARN／alias"),
            Text = "alias/aws/s3",
            Visibility = Visibility.Collapsed,
        };
        var bucketKey = new ToggleSwitch
        {
            Header = P("Use S3 Bucket Key (SSE-KMS only)", "使用 S3 Bucket Key（只限 SSE-KMS）"),
            Visibility = Visibility.Collapsed,
        };
        var blockSseC = new ToggleSwitch
        {
            Header = P("Block new SSE-C uploads", "封鎖新 SSE-C 上載"),
            IsOn = true,
        };
        encryption.SelectionChanged += (_, _) =>
        {
            var kmsSelected = encryption.SelectedIndex is 1 or 2;
            kmsKey.Visibility = kmsSelected ? Visibility.Visible : Visibility.Collapsed;
            bucketKey.Visibility = kmsSelected ? Visibility.Visible : Visibility.Collapsed;
            bucketKey.IsEnabled = encryption.SelectedIndex == 1;
            if (encryption.SelectedIndex != 1) bucketKey.IsOn = false;
        };
        var panel = new StackPanel { Spacing = 9, Width = 430 };
        panel.Children.Add(new TextBlock
        {
            Text = P("Bucket names are globally unique. Object Lock is irreversible and automatically requires versioning.",
                "儲存桶名稱係全球唯一。Object Lock 不可逆，並會自動需要版本控制。"),
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(name);
        panel.Children.Add(region);
        panel.Children.Add(blockPublic);
        panel.Children.Add(versioning);
        panel.Children.Add(encryption);
        panel.Children.Add(kmsKey);
        panel.Children.Add(bucketKey);
        panel.Children.Add(blockSseC);
        panel.Children.Add(objectLock);
        var dialog = new ContentDialog
        {
            Title = P("Create S3 bucket", "建立 S3 儲存桶"),
            Content = panel,
            PrimaryButtonText = P("Create bucket", "建立儲存桶"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!_awsContext.IsCurrent(reviewGeneration)) return;
        if ((!blockPublic.IsOn || !blockSseC.IsOn)
            && !await ConfirmTypedAsync(
                P("Confirm reduced bucket protections", "確認降低儲存桶保護"),
                P("One or more recommended protections are disabled. Type UNPROTECTED to continue.",
                    "一個或以上建議保護已停用。輸入 UNPROTECTED 先繼續。"),
                "UNPROTECTED")) return;
        if (!_awsContext.IsCurrent(reviewGeneration)) return;

        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        if (contextGeneration != reviewGeneration) return;
        using var operation = BeginS3Operation();
        var token = operation.Token;
        S3Progress.IsActive = true;
        S3StatusText.Text = P("Creating bucket…", "建立緊儲存桶…");
        try
        {
            var result = await manager.CreateBucketAsync(new AwsS3CreateBucketRequest
            {
                BucketName = name.Text.Trim(),
                EnableObjectLock = objectLock.IsOn,
            }, token);
            if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
            if (!result.Success || result.Value is null) { ShowManagerError(result.Error, result.Message); return; }

            async Task<bool> ContinueOrRollbackAsync(AwsManagerResult step, string stageEn, string stageZh)
            {
                if (step.Success) return true;
                var rollback = await manager.DeleteBucketAsync(result.Value.Name, CancellationToken.None);
                if (!_awsContext.IsCurrent(contextGeneration)) return false;
                var rollbackEn = rollback.Success
                    ? "The empty bucket was rolled back."
                    : "Automatic cleanup also failed; the bucket may still exist and needs review.";
                var rollbackZh = rollback.Success
                    ? "已回復並刪除空儲存桶。"
                    : "自動清理亦失敗；儲存桶可能仍然存在，需要檢查。";
                var message = new AwsManagerText(
                    $"Bucket '{result.Value.Name}' was created, but {stageEn} failed. {rollbackEn}",
                    $"儲存桶「{result.Value.Name}」已建立，但{stageZh}失敗。{rollbackZh}");
                var error = step.Error is null ? null : step.Error with { Message = message };
                ShowManagerError(error, message);
                S3StatusText.Text = ManagerMessage(message);
                return false;
            }

            var publicResult = await manager.PutPublicAccessBlockAsync(result.Value.Name,
                new AwsS3PublicAccessBlockConfiguration(true, blockPublic.IsOn, blockPublic.IsOn, blockPublic.IsOn, blockPublic.IsOn), token);
            if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
            if (!await ContinueOrRollbackAsync(publicResult, "public-access protection", "公開存取保護設定")) return;

            var algorithm = encryption.SelectedIndex switch
            {
                1 => "aws:kms",
                2 => "aws:kms:dsse",
                _ => "AES256",
            };
            var encryptionResult = await manager.PutBucketEncryptionAsync(result.Value.Name,
                new AwsS3EncryptionConfiguration(true,
                    new[]
                    {
                        new AwsS3EncryptionRule(
                            algorithm,
                            encryption.SelectedIndex is 1 or 2 ? NullIfWhiteSpace(kmsKey.Text) : null,
                            encryption.SelectedIndex == 1 ? bucketKey.IsOn : null,
                            blockSseC.IsOn ? "SSE-C" : null),
                    }), token);
            if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
            if (!await ContinueOrRollbackAsync(encryptionResult, "default encryption", "預設加密設定")) return;

            if (versioning.IsOn || objectLock.IsOn)
            {
                var versioningResult = await manager.PutBucketVersioningAsync(result.Value.Name,
                    new AwsS3VersioningConfiguration(true, "Enabled", null), token);
                if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
                if (!await ContinueOrRollbackAsync(versioningResult, "versioning", "版本控制設定")) return;
            }
            ShowManagerSuccess(result.Message);
            await RefreshS3Async();
        }
        finally { if (operation.IsCurrent) S3Progress.IsActive = false; }
    }

    private partial async Task UploadObjectAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryGetLoadedS3Bucket(out var bucket)) return;
        if (!TryCaptureAwsContext(out var reviewGeneration, out _)) return;
        var path = await FileDialogs.OpenFileAsync();
        if (path is null) return;
        if (!_awsContext.IsCurrent(reviewGeneration) || !TryGetLoadedS3Bucket(out var currentBucket)
            || !currentBucket.Name.Equals(bucket.Name, StringComparison.Ordinal)) return;
        var key = new TextBox { Header = P("Object key", "物件 key"), Text = _activeS3Prefix + Path.GetFileName(path) };
        var contentType = new TextBox { Header = P("Content type (optional)", "Content type（選填）"), PlaceholderText = "application/octet-stream" };
        var storage = new ComboBox { Header = P("Storage class", "儲存級別"), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var value in new[] { "STANDARD", "INTELLIGENT_TIERING", "STANDARD_IA", "ONEZONE_IA", "GLACIER_IR", "GLACIER", "DEEP_ARCHIVE" }) storage.Items.Add(value);
        storage.SelectedIndex = 0;
        var encryption = new ComboBox { Header = P("Server-side encryption", "伺服器端加密"), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var value in new[] { P("Bucket default", "儲存桶預設"), "AES256", "aws:kms", "aws:kms:dsse" }) encryption.Items.Add(value);
        encryption.SelectedIndex = 0;
        var kms = new TextBox { Header = P("KMS key (when using aws:kms)", "KMS key（使用 aws:kms 時）"), PlaceholderText = "alias/aws/s3" };
        var tags = new TextBox { Header = P("Object tags (key=value, one per line)", "物件標籤（key=value，每行一個）"), AcceptsReturn = true, Height = 70 };
        var metadata = new TextBox { Header = P("Metadata (key=value, one per line)", "Metadata（key=value，每行一個）"), AcceptsReturn = true, Height = 70 };
        var panel = new StackPanel { Width = 440, Spacing = 8 };
        foreach (var control in new UIElement[] { key, contentType, storage, encryption, kms, tags, metadata }) panel.Children.Add(control);
        var dialog = new ContentDialog
        {
            Title = P($"Upload to {bucket.Name}", $"上傳到 {bucket.Name}"),
            Content = panel,
            PrimaryButtonText = P("Upload", "上傳"),
            CloseButtonText = P("Cancel", "取消"),
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!_awsContext.IsCurrent(reviewGeneration) || !TryGetLoadedS3Bucket(out currentBucket)
            || !currentBucket.Name.Equals(bucket.Name, StringComparison.Ordinal)) return;

        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        if (contextGeneration != reviewGeneration) return;
        using var operation = BeginS3Operation(showCancel: true);
        var token = operation.Token;
        S3TransferProgress.Value = 0;
        S3TransferProgress.Visibility = Visibility.Visible;
        S3TransferText.Text = P("Starting upload…", "開始上傳…");
        var progress = new Progress<AwsTransferProgress>(p =>
        {
            S3TransferProgress.Value = p.PercentComplete;
            S3TransferText.Text = P($"Uploading {p.Key} · {p.PercentComplete}% · {FormatBytes(p.BytesTransferred)} / {FormatBytes(p.TotalBytes)}",
                $"上傳緊 {p.Key} · {p.PercentComplete}% · {FormatBytes(p.BytesTransferred)} / {FormatBytes(p.TotalBytes)}");
        });
        try
        {
            var request = new AwsS3UploadRequest
            {
                BucketName = bucket.Name,
                Key = key.Text.Trim(),
                LocalFilePath = path,
                ContentType = NullIfWhiteSpace(contentType.Text),
                StorageClass = storage.SelectedItem as string,
                ServerSideEncryption = encryption.SelectedIndex <= 0 ? null : encryption.SelectedItem as string,
                KmsKeyId = encryption.SelectedIndex is 2 or 3 ? NullIfWhiteSpace(kms.Text) : null,
                Tags = ParseKeyValueLines(tags.Text),
                Metadata = ParseKeyValueLines(metadata.Text),
            };
            var result = await manager.UploadObjectAsync(request, progress, token);
            token.ThrowIfCancellationRequested();
            if (!result.Success && result.Error?.Kind == AwsManagerErrorKind.Conflict
                && await ConfirmTypedAsync(
                    P("Replace existing S3 object", "取代現有 S3 物件"),
                    P($"An object may already exist at '{request.Key}'. Type the exact object key to replace it. This can permanently replace the current data when versioning is off.",
                        $"「{request.Key}」可能已有物件。輸入完整 object key 先取代；如果版本控制關閉，現有資料可能會永久被覆寫。"),
                    request.Key,
                    "Replace object",
                    "取代物件"))
            {
                S3TransferProgress.Value = 0;
                result = await manager.UploadObjectAsync(request with { Overwrite = true }, progress, token);
                token.ThrowIfCancellationRequested();
            }
            if (result.Success) { ShowManagerSuccess(result.Message); await LoadObjectsCoreAsync(bucket.Name, token); }
            else ShowManagerError(result.Error, result.Message);
        }
        catch (OperationCanceledException)
        {
            if (_awsContext.IsCurrent(contextGeneration))
                S3TransferText.Text = P("Upload cancelled.", "已取消上傳。");
        }
        finally
        {
            if (operation.IsCurrent && _awsContext.IsCurrent(contextGeneration))
            {
                S3TransferProgress.Visibility = Visibility.Collapsed;
            }
        }
    }

    private partial async Task DownloadObjectAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryGetLoadedS3Bucket(out var bucket)
            || S3ObjectList.SelectedItem is not S3ObjectRowView { IsPrefix: false } item
            || !string.Equals(item.BucketName, bucket.Name, StringComparison.Ordinal)) return;
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        var destination = await FileDialogs.SaveFileAsync(Path.GetFileName(item.Key));
        if (destination is null) return;
        if (!_awsContext.IsCurrent(contextGeneration)
            || S3ObjectList.SelectedItem is not S3ObjectRowView currentItem
            || !currentItem.BucketName.Equals(bucket.Name, StringComparison.Ordinal)
            || !currentItem.Key.Equals(item.Key, StringComparison.Ordinal)) return;
        using var operation = BeginS3Operation(showCancel: true);
        var token = operation.Token;
        S3TransferProgress.Value = 0;
        S3TransferProgress.Visibility = Visibility.Visible;
        var progress = new Progress<AwsTransferProgress>(p =>
        {
            S3TransferProgress.Value = p.PercentComplete;
            S3TransferText.Text = P($"Downloading {p.Key} · {p.PercentComplete}%", $"下載緊 {p.Key} · {p.PercentComplete}%");
        });
        try
        {
            var result = await manager.DownloadObjectAsync(new AwsS3DownloadRequest
            {
                BucketName = bucket.Name,
                Key = item.Key,
                DestinationPath = destination,
                Overwrite = true,
            }, progress, token);
            token.ThrowIfCancellationRequested();
            if (!_awsContext.IsCurrent(contextGeneration)) return;
            if (result.Success) ShowManagerSuccess(result.Message); else ShowManagerError(result.Error, result.Message);
        }
        catch (OperationCanceledException)
        {
            if (_awsContext.IsCurrent(contextGeneration))
                S3TransferText.Text = P("Download cancelled.", "已取消下載。");
        }
        finally
        {
            if (operation.IsCurrent && _awsContext.IsCurrent(contextGeneration))
            {
                S3TransferProgress.Visibility = Visibility.Collapsed;
            }
        }
    }

    private partial async Task DeleteS3SelectionAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryGetLoadedS3Bucket(out var bucket)) return;
        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        using var operation = BeginS3Operation();
        var token = operation.Token;
        if (S3ObjectList.SelectedItem is S3ObjectRowView { IsPrefix: false } item
            && string.Equals(item.BucketName, bucket.Name, StringComparison.Ordinal))
        {
            if (!await ConfirmTypedAsync(P("Delete S3 object", "刪除 S3 物件"),
                    P($"Delete '{item.Key}'? In a versioned bucket this normally creates a delete marker and retains older versions. Type DELETE to confirm.",
                        $"刪除「{item.Key}」？如果儲存桶有版本控制，通常會建立 delete marker 並保留舊版本。輸入 DELETE 確認。"), "DELETE")) return;
            var result = await manager.DeleteObjectsAsync(bucket.Name,
                new[] { new AwsS3ObjectIdentifier(item.Key) }, token);
            if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
            if (result.Success) { ShowManagerSuccess(result.Message); await LoadObjectsAsync(); }
            else ShowManagerError(result.Error, result.Message);
            return;
        }

        if (!await ConfirmTypedAsync(P("Delete S3 bucket", "刪除 S3 儲存桶"),
                P($"The bucket must be empty. Type its exact name to delete it: {bucket.Name}", $"儲存桶必須係空。輸入完整名稱以刪除：{bucket.Name}"), bucket.Name)) return;
        var bucketResult = await manager.DeleteBucketAsync(bucket.Name, token);
        if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
        if (bucketResult.Success) { ShowManagerSuccess(bucketResult.Message); await RefreshS3Async(); }
        else ShowManagerError(bucketResult.Error, bucketResult.Message);
    }

    private partial async Task SaveS3PropertiesAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryGetLoadedS3Bucket(out var bucket)) return;
        if (!_s3VersioningDirty && !_s3EncryptionDirty)
        {
            ShowManagerSuccess(new AwsManagerText("No property changes to save.", "冇屬性變更需要儲存。"));
            return;
        }

        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        using var operation = BeginS3Operation();
        var token = operation.Token;
        AwsManagerResult? last = null;
        if (_s3VersioningDirty)
        {
            last = await manager.PutBucketVersioningAsync(bucket.Name,
                new AwsS3VersioningConfiguration(true, S3VersioningToggle.IsOn ? "Enabled" : "Suspended", null), token);
            if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
            if (!last.Success) { ShowManagerOutcome(last); return; }
        }

        if (_s3EncryptionDirty)
        {
            var loadedRule = _loadedEncryption?.Rules.FirstOrDefault();
            var wasDsse = string.Equals(loadedRule?.Algorithm, "aws:kms:dsse", StringComparison.OrdinalIgnoreCase);
            var wasBlockingSseC = string.Equals(loadedRule?.BlockedEncryptionType, "SSE-C", StringComparison.OrdinalIgnoreCase);
            if (wasDsse && S3EncryptionBox.SelectedIndex != 2
                && !await ConfirmTypedAsync(
                    P("Confirm encryption downgrade", "確認降低加密層級"),
                    P("This changes DSSE-KMS to a different encryption mode. Type DOWNGRADE to continue.",
                        "呢個操作會將 DSSE-KMS 改做另一種加密模式。輸入 DOWNGRADE 先繼續。"),
                    "DOWNGRADE")) return;
            if (wasBlockingSseC && !S3BlockSseCToggle.IsOn
                && !await ConfirmTypedAsync(
                    P("Confirm SSE-C unblock", "確認解除 SSE-C 封鎖"),
                    P("This allows new objects encrypted with customer-provided keys. Type UNBLOCK to continue.",
                        "呢個操作會容許新物件使用客戶提供密鑰加密。輸入 UNBLOCK 先繼續。"),
                    "UNBLOCK")) return;
            if (_loadedEncryption?.Configured == true && S3EncryptionBox.SelectedIndex == 3
                && !await ConfirmTypedAsync(
                    P("Remove bucket encryption configuration", "移除儲存桶加密設定"),
                    P("This removes the explicit bucket encryption rule and its SSE-C block. Type REMOVE to continue.",
                        "呢個操作會移除明確儲存桶加密規則同 SSE-C 封鎖。輸入 REMOVE 先繼續。"),
                    "REMOVE")) return;

            var blockedType = S3BlockSseCToggle.IsOn ? "SSE-C" : null;
            var encryption = S3EncryptionBox.SelectedIndex switch
            {
                0 => new AwsS3EncryptionConfiguration(true,
                    new[] { new AwsS3EncryptionRule("AES256", null, null, blockedType) }),
                1 => new AwsS3EncryptionConfiguration(true,
                    new[] { new AwsS3EncryptionRule("aws:kms", NullIfWhiteSpace(S3KmsKeyBox.Text), S3BucketKeyToggle.IsOn, blockedType) }),
                2 => new AwsS3EncryptionConfiguration(true,
                    new[] { new AwsS3EncryptionRule("aws:kms:dsse", NullIfWhiteSpace(S3KmsKeyBox.Text), null, blockedType) }),
                _ => new AwsS3EncryptionConfiguration(false, Array.Empty<AwsS3EncryptionRule>()),
            };
            last = await manager.PutBucketEncryptionAsync(bucket.Name, encryption, token);
            if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
            if (!last.Success) { ShowManagerOutcome(last); return; }
        }

        if (last is not null) ShowManagerOutcome(last);
        await LoadSelectedBucketAsync();
    }

    private partial async Task SaveS3PermissionsAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryGetLoadedS3Bucket(out var bucket)) return;
        if (!_s3PublicAccessDirty && !_s3PolicyDirty)
        {
            ShowManagerSuccess(new AwsManagerText("No permission changes to save.", "冇權限變更需要儲存。"));
            return;
        }

        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        using var operation = BeginS3Operation();
        var token = operation.Token;
        AwsManagerResult? last = null;
        if (_s3PublicAccessDirty)
        {
            var weakensProtection = _loadedPublicAccess is { } loaded
                && ((loaded.BlockPublicAcls && !S3BlockPublicAclsToggle.IsOn)
                    || (loaded.IgnorePublicAcls && !S3IgnorePublicAclsToggle.IsOn)
                    || (loaded.BlockPublicPolicy && !S3BlockPublicPolicyToggle.IsOn)
                    || (loaded.RestrictPublicBuckets && !S3RestrictPublicBucketsToggle.IsOn));
            if (weakensProtection
                && !await ConfirmTypedAsync(
                    P("Confirm public-access change", "確認公開存取變更"),
                    P("This weakens one or more bucket-level public-access blocks. Type PUBLIC to continue.",
                        "呢個操作會降低一項或以上儲存桶級公開存取保護。輸入 PUBLIC 先繼續。"),
                    "PUBLIC")) return;
            last = await manager.PutPublicAccessBlockAsync(bucket.Name,
                new AwsS3PublicAccessBlockConfiguration(
                    true,
                    S3BlockPublicAclsToggle.IsOn,
                    S3IgnorePublicAclsToggle.IsOn,
                    S3BlockPublicPolicyToggle.IsOn,
                    S3RestrictPublicBucketsToggle.IsOn), token);
            if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
            if (!last.Success) { ShowManagerOutcome(last); return; }
        }

        if (_s3PolicyDirty)
        {
            var policy = S3PolicyBox.Text?.Trim();
            if (_loadedPolicy?.Configured == true && string.IsNullOrEmpty(policy)
                && !await ConfirmTypedAsync(
                    P("Delete bucket policy", "刪除儲存桶政策"),
                    P("The bucket policy will be deleted. Type POLICY to continue.", "儲存桶政策將會刪除。輸入 POLICY 先繼續。"),
                    "POLICY")) return;
            last = await manager.PutBucketPolicyAsync(bucket.Name,
                new AwsS3BucketPolicy(!string.IsNullOrEmpty(policy), policy), token);
            if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
            if (!last.Success) { ShowManagerOutcome(last); return; }
        }

        if (last is not null) ShowManagerOutcome(last);
        await LoadSelectedBucketAsync();
    }

    private partial async Task SaveS3ManagementAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryGetLoadedS3Bucket(out var bucket)) return;
        if (!_s3LifecycleDirty && !_s3CorsDirty)
        {
            ShowManagerSuccess(new AwsManagerText("No management changes to save.", "冇管理變更需要儲存。"));
            return;
        }

        using var mutation = BeginAwsMutation();
        try
        {
            if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
            using var operation = BeginS3Operation();
            var token = operation.Token;
            AwsManagerResult? last = null;
            if (_s3LifecycleDirty)
            {
                var lifecycle = JsonSerializer.Deserialize<AwsS3LifecycleConfiguration>(S3LifecycleBox.Text, ManagerJson);
                if (lifecycle is null || lifecycle.Rules is null)
                {
                    ShowManagerError(null, new AwsManagerText("Lifecycle JSON must contain a non-null rules array.", "Lifecycle JSON 必須包含非 null 嘅 rules array。"));
                    return;
                }
                if (_loadedLifecycle?.Configured == true && (!lifecycle.Configured || lifecycle.Rules.Count == 0)
                    && !await ConfirmTypedAsync(P("Delete lifecycle rules", "刪除生命週期規則"),
                        P("All lifecycle rules will be removed. Type DELETE to continue.", "全部生命週期規則將會移除。輸入 DELETE 先繼續。"), "DELETE")) return;
                last = await manager.PutLifecycleConfigurationAsync(bucket.Name, lifecycle, token);
                if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
                if (!last.Success) { ShowManagerOutcome(last); return; }
            }

            if (_s3CorsDirty)
            {
                var cors = JsonSerializer.Deserialize<AwsS3CorsConfiguration>(S3CorsBox.Text, ManagerJson);
                if (cors is null || cors.Rules is null)
                {
                    ShowManagerError(null, new AwsManagerText("CORS JSON must contain a non-null rules array.", "CORS JSON 必須包含非 null 嘅 rules array。"));
                    return;
                }
                if (_loadedCors?.Configured == true && (!cors.Configured || cors.Rules.Count == 0)
                    && !await ConfirmTypedAsync(P("Delete CORS rules", "刪除 CORS 規則"),
                        P("All CORS rules will be removed. Type DELETE to continue.", "全部 CORS 規則將會移除。輸入 DELETE 先繼續。"), "DELETE")) return;
                last = await manager.PutCorsConfigurationAsync(bucket.Name, cors, token);
                if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
                if (!last.Success) { ShowManagerOutcome(last); return; }
            }

            if (last is not null) ShowManagerOutcome(last);
            await LoadSelectedBucketAsync();
        }
        catch (JsonException)
        {
            ShowManagerError(null, new AwsManagerText("Lifecycle and CORS editors must contain valid JSON.", "Lifecycle 同 CORS 編輯器必須包含有效 JSON。"));
        }
        catch (Exception)
        {
            ShowManagerError(null, new AwsManagerText("The S3 management settings were not saved.", "S3 管理設定未有儲存。"));
        }
    }

    private partial async Task SaveS3TagsAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryGetLoadedS3Bucket(out var bucket)) return;
        if (!_s3TagsDirty)
        {
            ShowManagerSuccess(new AwsManagerText("No tag changes to save.", "冇標籤變更需要儲存。"));
            return;
        }

        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out _)) return;
        using var operation = BeginS3Operation();
        var token = operation.Token;
        var tags = ParseKeyValueLines(S3TagsBox.Text);
        if ((_loadedTags?.Tags.Count ?? 0) > 0 && tags.Count == 0
            && !await ConfirmTypedAsync(P("Delete all bucket tags", "刪除全部儲存桶標籤"),
                P("All bucket tags will be removed. Type DELETE to continue.", "全部儲存桶標籤將會移除。輸入 DELETE 先繼續。"), "DELETE")) return;
        var result = await manager.PutBucketTagsAsync(bucket.Name, new AwsS3BucketTags(tags), token);
        if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
        ShowManagerOutcome(result);
        if (result.Success) await LoadSelectedBucketAsync();
    }

    private partial async Task CreateCloudResourceAsync()
    {
        var manager = EnsureAwsManager();
        if (!TryCaptureAwsContext(out var reviewGeneration, out _)) return;
        var type = new TextBox { Header = P("CloudFormation resource type", "CloudFormation 資源類型"), PlaceholderText = "AWS::EC2::SecurityGroup" };
        var json = new TextBox { Header = P("Desired state (JSON)", "目標狀態（JSON）"), Text = "{\n  \n}", AcceptsReturn = true, Height = 250, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
        var wait = new CheckBox { Content = P("Wait for completion", "等候完成"), IsChecked = true };
        var panel = new StackPanel { Width = 480, Spacing = 8 };
        panel.Children.Add(type); panel.Children.Add(json); panel.Children.Add(wait);
        var dialog = new ContentDialog { Title = P("Create resource with Cloud Control", "用 Cloud Control 建立資源"), Content = panel, PrimaryButtonText = P("Create", "建立"), CloseButtonText = P("Cancel", "取消"), XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!_awsContext.IsCurrent(reviewGeneration)) return;
        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out var token)) return;
        if (contextGeneration != reviewGeneration) return;
        var result = await manager.CreateCloudResourceAsync(new AwsCloudControlCreateRequest { TypeName = type.Text.Trim(), DesiredStateJson = json.Text }, token);
        if (result.Success && result.Value is { } op && wait.IsChecked == true)
            result = await manager.WaitForCloudOperationAsync(op.RequestToken, cancellationToken: token);
        if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
        ShowManagerOutcome(result);
        if (result.Success) await RefreshResourcesAsync();
    }

    private partial async Task InspectCloudResourceAsync()
    {
        var manager = EnsureAwsManager();
        if (ResourceList.SelectedItem is not AwsResourceRowView { CloudFormationType: { Length: > 0 } type, Identifier: { Length: > 0 } id } row) return;
        if (!TryCaptureAwsContext(out var contextGeneration, out var token)) return;
        var result = await manager.GetCloudResourceAsync(new AwsCloudControlGetRequest { TypeName = type, Identifier = id }, token);
        if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
        if (!result.Success || result.Value is null) { ShowManagerError(result.Error, result.Message); return; }
        await ShowJsonDialogAsync(P($"{row.Name} · resource JSON", $"{row.Name} · 資源 JSON"), result.Value.PropertiesJson);
    }

    private partial async Task UpdateCloudResourceAsync()
    {
        var manager = EnsureAwsManager();
        if (ResourceList.SelectedItem is not AwsResourceRowView { CloudFormationType: { Length: > 0 } type, Identifier: { Length: > 0 } id } row) return;
        if (!TryCaptureAwsContext(out var reviewGeneration, out _)) return;
        var patch = new TextBox { Header = P("JSON Patch document", "JSON Patch 文件"), Text = "[\n  { \"op\": \"replace\", \"path\": \"/Property\", \"value\": \"new-value\" }\n]", AcceptsReturn = true, Height = 260, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
        var wait = new CheckBox { Content = P("Wait for completion", "等候完成"), IsChecked = true };
        var panel = new StackPanel { Width = 500, Spacing = 8 }; panel.Children.Add(patch); panel.Children.Add(wait);
        var dialog = new ContentDialog { Title = P($"Update {row.Name}", $"更新 {row.Name}"), Content = panel, PrimaryButtonText = P("Submit update", "提交更新"), CloseButtonText = P("Cancel", "取消"), XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!_awsContext.IsCurrent(reviewGeneration)
            || ResourceList.SelectedItem is not AwsResourceRowView currentRow
            || !string.Equals(currentRow.Identifier, id, StringComparison.Ordinal)) return;
        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out var token)) return;
        if (contextGeneration != reviewGeneration) return;
        var result = await manager.UpdateCloudResourceAsync(new AwsCloudControlUpdateRequest { TypeName = type, Identifier = id, PatchDocumentJson = patch.Text }, token);
        if (result.Success && result.Value is { } op && wait.IsChecked == true)
            result = await manager.WaitForCloudOperationAsync(op.RequestToken, cancellationToken: token);
        if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
        ShowManagerOutcome(result);
        if (result.Success) await RefreshResourcesAsync();
    }

    private partial async Task DeleteCloudResourceAsync()
    {
        var manager = EnsureAwsManager();
        if (ResourceList.SelectedItem is not AwsResourceRowView { CloudFormationType: { Length: > 0 } type, Identifier: { Length: > 0 } id } row) return;
        if (!TryCaptureAwsContext(out var reviewGeneration, out _)) return;
        if (!await ConfirmTypedAsync(P("Delete AWS resource", "刪除 AWS 資源"),
                P($"This uses Cloud Control and is destructive. Type DELETE to remove {row.Name}.", $"呢個操作會用 Cloud Control，而且有破壞性。輸入 DELETE 刪除 {row.Name}。"), "DELETE")) return;
        if (!_awsContext.IsCurrent(reviewGeneration)
            || ResourceList.SelectedItem is not AwsResourceRowView currentRow
            || !string.Equals(currentRow.Identifier, id, StringComparison.Ordinal)) return;
        using var mutation = BeginAwsMutation();
        if (!TryCaptureAwsContext(out var contextGeneration, out var token)) return;
        if (contextGeneration != reviewGeneration) return;
        var result = await manager.DeleteCloudResourceAsync(new AwsCloudControlDeleteRequest { TypeName = type, Identifier = id }, token);
        if (result.Success && result.Value is { } op)
            result = await manager.WaitForCloudOperationAsync(op.RequestToken, cancellationToken: token);
        if (token.IsCancellationRequested || !_awsContext.IsCurrent(contextGeneration)) return;
        ShowManagerOutcome(result);
        if (result.Success) await RefreshResourcesAsync();
    }

    private S3OperationLease BeginS3Operation(bool showCancel = false)
    {
        CancelCurrentS3Operation();
        unchecked { _s3OperationId++; }
        if (TryCaptureAwsContext(out _, out var contextToken))
        {
            _s3OperationCts = CancellationTokenSource.CreateLinkedTokenSource(contextToken);
        }
        else
        {
            _s3OperationCts = new CancellationTokenSource();
            _s3OperationCts.Cancel();
        }
        S3BusyShield.Visibility = Visibility.Visible;
        S3CancelBtn.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
        return new S3OperationLease(this, _s3OperationId, _s3OperationCts.Token);
    }

    private bool IsCurrentS3Operation(int operationId)
        => operationId == _s3OperationId && _s3OperationCts is not null;

    private void EndS3Operation(int operationId)
    {
        if (!IsCurrentS3Operation(operationId)) return;
        _s3OperationCts!.Dispose();
        _s3OperationCts = null;
        S3BusyShield.Visibility = Visibility.Collapsed;
        S3CancelBtn.Visibility = Visibility.Collapsed;
    }

    private void CancelCurrentS3Operation()
    {
        _s3OperationCts?.Cancel();
        _s3OperationCts?.Dispose();
        _s3OperationCts = null;
        unchecked { _s3OperationId++; }
        S3BusyShield.Visibility = Visibility.Collapsed;
        S3CancelBtn.Visibility = Visibility.Collapsed;
    }

    private sealed class S3OperationLease(
        AwsCliModule owner,
        int operationId,
        CancellationToken token) : IDisposable
    {
        private AwsCliModule? _owner = owner;

        public CancellationToken Token { get; } = token;

        public bool IsCurrent => _owner?.IsCurrentS3Operation(operationId) == true;

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            current?.EndS3Operation(operationId);
        }
    }

    private async Task<bool> ConfirmTypedAsync(
        string title,
        string message,
        string expected,
        string primaryEn = "Delete permanently",
        string primaryZh = "永久刪除")
    {
        var input = new TextBox { Header = P("Confirmation", "確認"), PlaceholderText = expected };
        var panel = new StackPanel { Width = 420, Spacing = 9 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(input);
        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = P(primaryEn, primaryZh),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary && string.Equals(input.Text.Trim(), expected, StringComparison.Ordinal);
    }

    private async Task ShowJsonDialogAsync(string title, string json)
    {
        var box = new TextBox { Text = PrettyJson(json), AcceptsReturn = true, IsReadOnly = true, Height = 420, Width = 620, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap };
        var dialog = new ContentDialog { Title = title, Content = box, CloseButtonText = P("Close", "關閉"), XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    private void ShowManagerOutcome(AwsManagerResult result)
    {
        if (result.Success) ShowManagerSuccess(result.Message);
        else ShowManagerError(result.Error, result.Message);
    }

    private void ShowManagerOutcome<T>(AwsManagerResult<T> result)
    {
        if (result.Success) ShowManagerSuccess(result.Message);
        else ShowManagerError(result.Error, result.Message);
    }

    private void ShowManagerSuccess(AwsManagerText message)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成");
        ResultBar.Message = ManagerMessage(message);
    }

    private void ShowManagerError(AwsManagerError? error, AwsManagerText fallback)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = error?.Kind == AwsManagerErrorKind.AccessDenied
            ? InfoBarSeverity.Warning
            : InfoBarSeverity.Error;
        ResultBar.Title = error?.Kind switch
        {
            AwsManagerErrorKind.CredentialsUnavailable or AwsManagerErrorKind.AuthenticationRequired
                => P("Sign-in required", "需要登入"),
            AwsManagerErrorKind.AccessDenied => P("Permission required", "需要權限"),
            _ => P("AWS request failed", "AWS 要求失敗"),
        };
        ResultBar.Message = ManagerMessage(error?.Message ?? fallback);
    }

    private string ManagerMessage(AwsManagerText text) => P(text.En, text.Zh);

    private static string PrettyJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, ManagerJson);
        }
        catch { return json; }
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Dictionary<string, string> ParseKeyValueLines(string? text)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;
            var equals = trimmed.IndexOf('=');
            if (equals <= 0) continue;
            values[trimmed[..equals].Trim()] = trimmed[(equals + 1)..].Trim();
        }
        return values;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
