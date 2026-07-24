using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// AWS Console-style shell and resource workspaces. The original CLI workbench remains in the other
/// partial file as an advanced destination; this partial owns the primary manager experience.
/// </summary>
public sealed partial class AwsCliModule
{
    private sealed record AwsServiceCardView(
        string Id,
        string Name,
        string Category,
        string Summary,
        string Glyph,
        string Capability,
        string Scope,
        string ResourceNamespace);

    private sealed record AwsResourceRowView(
        string Name,
        string Arn,
        string Type,
        string Region,
        string Status,
        string Service,
        string? CloudFormationType,
        string? Identifier,
        string? PropertiesJson);

    private sealed record S3BucketRowView(string Name, string Region, string Created);

    private sealed record S3ObjectRowView(
        string BucketName,
        string Name,
        string Key,
        string Size,
        string Modified,
        string Glyph,
        bool IsPrefix);

    private sealed record AwsEc2InstanceRowView(
        string Name,
        string InstanceId,
        string State,
        string InstanceType,
        string AvailabilityZone,
        string PrivateIpAddress,
        string PublicIpAddress,
        AwsEc2Instance Instance);

    private readonly List<AwsServiceCardView> _consoleServices = new();
    private readonly List<AwsResourceRowView> _resourceRows = new();
    private readonly List<S3BucketRowView> _bucketRows = new();
    private readonly List<S3ObjectRowView> _objectRows = new();
    private readonly List<AwsEc2InstanceRowView> _ec2Rows = new();
    private readonly AwsContextGeneration _awsContext = new();
    private CancellationTokenSource? _s3OperationCts;
    private int _s3OperationId;
    private CancellationTokenSource? _ec2OperationCts;
    private int _ec2OperationId;
    private bool _ec2ReviewPending;
    private bool _consoleInitialized;
    private bool _cliInstalled;
    private bool _suppressS3Settings;
    private bool _s3VersioningDirty;
    private bool _s3EncryptionDirty;
    private bool _s3PublicAccessDirty;
    private bool _s3PolicyDirty;
    private bool _s3LifecycleDirty;
    private bool _s3CorsDirty;
    private bool _s3TagsDirty;
    private int _awsMutationCount;
    private string? _loadedS3BucketName;
    private long _s3BucketLoadGeneration;
    private string? _resourceNextToken;
    private string? _resourceActiveQuery;
    private string? _s3ObjectNextToken;
    private string? _s3ObjectPageBucket;
    private string? _s3ObjectPagePrefix;
    private string? _ec2NextToken;
    private int _ec2LoadedContextGeneration = -1;
    private string _activeConsoleView = "home";
    private string _activeS3Prefix = string.Empty;

    private void InitializeConsoleShell()
    {
        if (_consoleInitialized) return;
        _consoleInitialized = true;
        _awsContext.Restart();

        _suppressS3Settings = true;
        S3EncryptionBox.Items.Add("SSE-S3 (AES-256)");
        S3EncryptionBox.Items.Add("SSE-KMS");
        S3EncryptionBox.Items.Add("DSSE-KMS");
        S3EncryptionBox.Items.Add(P("Not configured", "未設定"));
        S3EncryptionBox.SelectedIndex = 3;
        _suppressS3Settings = false;
        ResetS3DirtyFlags();

        ResourceTypeFilter.Items.Add(P("All resource types", "所有資源類型"));
        ResourceTypeFilter.SelectedIndex = 0;

        RebuildEc2StateFilter();

        BuildOperationsDashboard();
        ConsoleNavigation.SelectedItem = ConsoleHomeNav;
        ShowConsoleView("home");
    }

    private void RenderConsoleShell()
    {
        if (ConsoleNavigation is null) return;

        ConsoleBrandText.Text = "AWS";
        ConsoleScopeText.Text = P("Management Console", "管理主控台");
        GlobalSearchBox.PlaceholderText = P("Search services, resources, ARNs and tags", "搜尋服務、資源、ARN 同標籤");

        ConsoleHomeNav.Content = P("Console home", "Console 首頁");
        ResourcesNav.Content = P("All resources", "所有資源");
        S3Nav.Content = P("S3 storage", "S3 儲存");
        Ec2Nav.Content = P("EC2 instances", "EC2 執行個體");
        ServicesNav.Content = P("All services", "所有服務");
        OperationsNav.Content = P("Operations", "營運管理");
        AdvancedNav.Content = P("CLI workbench", "CLI 工作台");

        HomeTitle.Text = P("Console home", "Console 首頁");
        HomeSubtitle.Text = P(
            "Manage the account by resources and services. Search across Regions, open a native service workspace, or inspect operations and governance from one place.",
            "用資源同服務管理帳戶。跨區域搜尋、開原生服務工作區，或者喺同一處檢視營運同管治狀態。");
        HomeRefreshBtn.Content = P("Refresh", "重新整理");
        OpenS3Btn.Content = P("S3", "S3");
        OpenEc2Btn.Content = P("EC2", "EC2");
        OpenResourcesBtn.Content = P("Resources", "資源");
        IdentityCardTitle.Text = P("Current identity", "目前身份");
        ResourceCountLabel.Text = P("Resources found", "搵到嘅資源");
        BucketCountLabel.Text = P("S3 buckets", "S3 儲存桶");
        AlarmCountLabel.Text = P("Alarms in alert", "警報中嘅 Alarm");
        ServiceCountLabel.Text = P("Available services", "可用服務");
        UnifiedSearchTitle.Text = P("Find any AWS resource", "搵任何 AWS 資源");
        UnifiedSearchHint.Text = P(
            "Resource Explorer searches names, IDs, ARNs, tags, service, type and Region. Permission failures stay inside this widget instead of blocking the manager.",
            "Resource Explorer 可以搜尋名稱、ID、ARN、標籤、服務、類型同區域。權限失敗只會留喺呢個 widget，唔會阻住成個管理中心。");
        HomeResourceSearchBox.PlaceholderText = P("Example: service:ec2 region:us-east-1 environment:prod", "例如：service:ec2 region:us-east-1 environment:prod");
        HomeSearchBtn.Content = P("Search resources", "搜尋資源");
        FavoriteServicesTitle.Text = P("Favorites and core services", "收藏同核心服務");

        ResourcesTitle.Text = P("All resources", "所有資源");
        ResourcesHint.Text = P(
            "Cross-service inventory powered by AWS Resource Explorer, with tag inventory fallback. Filter, inspect metadata, copy an ARN, then open the owning service workspace.",
            "由 AWS Resource Explorer 驅動嘅跨服務資源清單，並有標籤資源後備。篩選、檢視 metadata、複製 ARN，再開資源所屬服務工作區。");
        ResourceQueryBox.PlaceholderText = P("Search query, ARN, resource ID, tag or service", "搜尋查詢、ARN、資源 ID、標籤或服務");
        CrossRegionToggle.Header = P("Cross-Region", "跨區域");
        ResourceRefreshBtn.Content = P("Search", "搜尋");
        ResourceLoadMoreBtn.Content = P("Load more", "載入更多");
        ResourceCreateBtn.Content = P("Create resource", "建立資源");
        ResourceNameColumn.Text = P("Name / ARN", "名稱／ARN");
        ResourceTypeColumn.Text = P("Resource type", "資源類型");
        ResourceRegionColumn.Text = P("Region", "區域");
        ResourceStatusColumn.Text = P("Status", "狀態");
        ResourceDetailsTitle.Text = P("Resource details", "資源詳情");
        ResourceCopyArnBtn.Content = P("Copy ARN", "複製 ARN");
        ResourceOpenServiceBtn.Content = P("Open service", "開啟服務");
        ResourceInspectBtn.Content = P("Inspect JSON", "檢視 JSON");
        ResourceEditBtn.Content = P("Update", "更新");
        ResourceDeleteBtn.Content = P("Delete", "刪除");
        if (_resourceRows.Count == 0)
            ResourceStatusText.Text = P("Run a resource search to load the account inventory.", "執行資源搜尋以載入帳戶清單。");

        S3Title.Text = P("Amazon S3", "Amazon S3");
        S3Hint.Text = P(
            "Native bucket and object management with structured settings, guarded destructive actions, progress and cancellation.",
            "原生儲存桶同物件管理，配合結構化設定、受保護破壞性操作、進度同取消功能。");
        S3RefreshBtn.Content = P("Refresh", "重新整理");
        S3CreateBucketBtn.Content = P("Create bucket", "建立儲存桶");
        S3UploadBtn.Content = P("Upload", "上傳");
        S3DeleteBtn.Content = P("Delete", "刪除");
        S3BucketsHeader.Text = P("Buckets", "儲存桶");
        S3BucketFilterBox.PlaceholderText = P("Filter buckets", "篩選儲存桶");
        S3ObjectsHeader.Text = P("Objects and prefixes", "物件同前綴");
        S3UpBtn.Content = P("Up", "上一層");
        S3DownloadBtn.Content = P("Download", "下載");
        S3ObjectLoadMoreBtn.Content = P("Load more", "載入更多");
        S3PrefixBox.PlaceholderText = P("Prefix / folder path", "前綴／資料夾路徑");
        S3DetailsHeader.Text = P("Bucket settings", "儲存桶設定");
        S3PropertiesTab.Header = P("Properties", "屬性");
        S3PermissionsTab.Header = P("Permissions", "權限");
        S3ManagementTab.Header = P("Management", "管理");
        S3TagsTab.Header = P("Tags", "標籤");
        S3VersioningToggle.Header = P("Bucket versioning", "儲存桶版本控制");
        S3ObjectLockToggle.Header = P("Object Lock (creation-time only)", "Object Lock（只可建立時設定）");
        S3KmsKeyBox.Header = P("KMS key ARN / alias", "KMS key ARN／alias");
        S3KmsKeyBox.PlaceholderText = P("alias/aws/s3 or a customer-managed key ARN", "alias/aws/s3 或客戶管理 key ARN");
        S3BucketKeyToggle.Header = P("Use S3 Bucket Key", "使用 S3 Bucket Key");
        S3BlockSseCToggle.Header = P("Block new SSE-C uploads", "封鎖新 SSE-C 上載");
        S3BlockPublicToggle.Header = P("Block all public access (set all four)", "封鎖所有公開存取（設定全部四項）");
        S3BlockPublicAclsToggle.Header = P("Block public ACLs", "封鎖公開 ACL");
        S3IgnorePublicAclsToggle.Header = P("Ignore public ACLs", "忽略公開 ACL");
        S3BlockPublicPolicyToggle.Header = P("Block public bucket policies", "封鎖公開儲存桶政策");
        S3RestrictPublicBucketsToggle.Header = P("Restrict public buckets", "限制公開儲存桶");
        S3PolicyBox.Header = P("Bucket policy (JSON)", "儲存桶政策（JSON）");
        S3LifecycleLabel.Text = P("Lifecycle rules (JSON)", "生命週期規則（JSON）");
        S3CorsLabel.Text = P("CORS rules (JSON)", "CORS 規則（JSON）");
        S3TagsHint.Text = P("One tag per line: key=value", "每行一個標籤：key=value");
        S3SavePropertiesBtn.Content = P("Save properties", "儲存屬性");
        S3SavePermissionsBtn.Content = P("Save permissions", "儲存權限");
        S3SaveManagementBtn.Content = P("Save management rules", "儲存管理規則");
        S3SaveTagsBtn.Content = P("Save tags", "儲存標籤");
        S3CancelBtn.Content = P("Cancel operation", "取消操作");
        if (_bucketRows.Count == 0)
            S3StatusText.Text = P("Choose a profile, then refresh to load buckets.", "揀 profile，再重新整理以載入儲存桶。");

        Ec2Title.Text = P("Amazon EC2 instances", "Amazon EC2 執行個體");
        Ec2Hint.Text = P(
            "Native, Region-scoped instance inventory and guarded lifecycle controls. WinForge never starts, stops, reboots, or terminates an instance without an explicit review step.",
            "原生、按 Region 劃分嘅執行個體清單同受保護生命週期控制。未經明確覆核，WinForge 絕對唔會啟動、停止、重新啟動或者終止執行個體。");
        Ec2RefreshBtn.Content = P("Refresh", "重新整理");
        Ec2FilterBox.PlaceholderText = P("Filter by name, ID, type, IP, zone or tag", "按名稱、ID、類型、IP、區域或標籤篩選");
        Ec2NameColumn.Text = P("Name / instance ID", "名稱／執行個體 ID");
        Ec2StateColumn.Text = P("State", "狀態");
        Ec2TypeColumn.Text = P("Type", "類型");
        Ec2ZoneColumn.Text = P("Availability Zone", "可用區域");
        Ec2IpColumn.Text = P("Private / public IP", "私人／公開 IP");
        Ec2DetailsTitle.Text = P("Instance details", "執行個體詳情");
        Ec2StartBtn.Content = P("Start", "啟動");
        Ec2StopBtn.Content = P("Stop", "停止");
        Ec2RebootBtn.Content = P("Reboot", "重新啟動");
        Ec2TerminateBtn.Content = P("Terminate", "終止");
        Ec2LoadMoreBtn.Content = P("Load more", "載入更多");
        Ec2ActionHint.Text = P(
            "Starting can incur charges; stop and reboot interrupt workloads. Termination is permanent and attached volumes may be deleted according to their DeleteOnTermination settings.",
            "啟動可能產生費用；停止同重新啟動會中斷工作。終止係永久操作，而已連接磁碟區可能會按 DeleteOnTermination 設定一併刪除。");
        if (_ec2Rows.Count == 0)
            Ec2StatusText.Text = P("Choose a profile, then refresh this Region.", "揀 profile，再重新整理呢個 Region。");
        RebuildEc2StateFilter();

        ServicesTitle.Text = P("All AWS services", "所有 AWS 服務");
        ServicesHint.Text = P(
            "Browse by category or search by service, feature and resource type. Native and generic resource workspaces progressively cover the full installed AWS surface.",
            "按分類瀏覽，或者用服務、功能同資源類型搜尋。原生同通用資源工作區會逐步覆蓋已安裝 AWS 嘅完整範圍。");
        ConsoleServiceSearch.PlaceholderText = P("Search services and capabilities", "搜尋服務同功能");

        OperationsTitle.Text = P("Operations and governance", "營運同管治");
        OperationsHint.Text = P(
            "Independent account widgets for deployments, monitoring, security, cost, health and quotas. A missing permission degrades only its own card.",
            "部署、監控、安全、成本、健康同 quota 嘅獨立帳戶 widget。缺少權限只會影響自己張卡。");

        AdvancedTitle.Text = P("Advanced CLI workbench", "進階 CLI 工作台");
        AdvancedHint.Text = P(
            "Exact command-level access, streaming output, cancellation and history. This is the escape hatch for the moving CLI surface—not the primary resource-management workflow.",
            "提供精確指令級存取、即時輸出、取消同歷史。呢度係應付不斷變動 CLI 範圍嘅後備入口，唔係主要資源管理流程。");

        IdentityRegionText.Text = P($"Region: {DisplayRegion()}", $"區域：{DisplayRegion()}");
        FilterConsoleServices();
        BuildOperationsDashboard();
    }

    private async Task LoadConsoleAsync()
    {
        _services = _cliInstalled
            ? await AwsCliService.ListServicesAsync()
            : AwsServiceCatalog.CommonServices.ToList();
        BuildInitialServiceCatalog();
        RenderConsoleShell();
        await RefreshConsoleContextAsync(loadResources: false);
    }

    private string DisplayRegion()
        => string.IsNullOrWhiteSpace(AwsCliService.ActiveRegion)
            ? P("profile default / Global", "profile 預設／Global")
            : AwsCliService.ActiveRegion;

    private void BuildInitialServiceCatalog()
    {
        if (_consoleServices.Count > 0) return;
        var categories = AwsConsoleCatalog.Categories.ToDictionary(c => c.Id);
        foreach (var service in AwsConsoleCatalog.Build(_services))
        {
            var category = categories[service.Category];
            var capability = service.CliId is "s3" or "ec2"
                ? P("Native manager", "原生管理")
                : service.AdapterCapability switch
            {
                AwsAdapterCapabilityLevel.ResourceLifecycle => P("Resource workspace", "資源工作區"),
                AwsAdapterCapabilityLevel.ResourceDiscovery => P("Resource workspace", "資源工作區"),
                _ => P("Resource inventory + CLI", "資源清單 + CLI"),
            };
            var global = service.CliId is "iam" or "route53" or "cloudfront" or "organizations"
                or "account" or "billing" or "budgets" or "ce" or "support";
            _consoleServices.Add(new AwsServiceCardView(
                service.CliId,
                P(service.NameEn, service.NameZh),
                P(category.NameEn, category.NameZh),
                P(service.DescriptionEn, service.DescriptionZh),
                service.Glyph,
                capability,
                global ? P("Global", "Global") : P("Regional", "區域"),
                service.ResourceExplorerNamespace));
        }

        ConsoleCategoryBox.Items.Clear();
        ConsoleCategoryBox.Items.Add(P("All categories", "所有分類"));
        foreach (var category in _consoleServices.Select(s => s.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s))
            ConsoleCategoryBox.Items.Add(category);
        ConsoleCategoryBox.SelectedIndex = 0;

        var homeServiceIds = new[]
        {
            "s3", "ec2", "lambda", "iam", "cloudformation", "cloudwatch", "rds", "dynamodb",
        };
        foreach (var serviceId in homeServiceIds)
        {
            var service = _consoleServices.FirstOrDefault(item =>
                item.Id.Equals(serviceId, StringComparison.OrdinalIgnoreCase));
            if (service is not null) FavoriteServicesGrid.Items.Add(service);
        }
        ServiceCountText.Text = _consoleServices.Count.ToString();

        ResourceTypeFilter.Items.Clear();
        ResourceTypeFilter.Items.Add(P("All resource types", "所有資源類型"));
        foreach (var service in _consoleServices.OrderBy(s => s.Name)) ResourceTypeFilter.Items.Add(service.Name);
        ResourceTypeFilter.SelectedIndex = 0;
        FilterConsoleServices();
    }

    private void RebuildConsoleServiceCatalogForLanguage()
    {
        if (!_consoleInitialized) return;
        _consoleServices.Clear();
        FavoriteServicesGrid.Items.Clear();
        AllServicesGrid.Items.Clear();
        BuildInitialServiceCatalog();
    }

    private void FilterConsoleServices()
    {
        if (AllServicesGrid is null || _consoleServices.Count == 0) return;
        var query = (ConsoleServiceSearch.Text ?? string.Empty).Trim();
        var category = ConsoleCategoryBox.SelectedIndex > 0 ? ConsoleCategoryBox.SelectedItem as string : null;
        var shown = _consoleServices.Where(s =>
            (string.IsNullOrEmpty(query)
                || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.Summary.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || s.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            && (string.IsNullOrEmpty(category) || s.Category.Equals(category, StringComparison.OrdinalIgnoreCase)));
        AllServicesGrid.Items.Clear();
        foreach (var service in shown) AllServicesGrid.Items.Add(service);
    }

    private void BuildOperationsDashboard()
    {
        if (StacksCard is null) return;
        SetOperationsCard(StacksCard, "\uE9D9", P("CloudFormation", "CloudFormation"),
            P("Stacks, change sets, drift and deployment events", "Stack、change set、drift 同部署事件"), "cloudformation");
        SetOperationsCard(MonitoringCard, "\uE9D2", P("Monitoring", "監控"),
            P("CloudWatch alarms, metrics, dashboards and logs", "CloudWatch alarm、指標、儀表板同記錄"), "cloudwatch");
        SetOperationsCard(SecurityCard, "\uE7BA", P("Security posture", "安全狀態"),
            P("Security Hub, GuardDuty, Config and IAM findings", "Security Hub、GuardDuty、Config 同 IAM finding"), "securityhub");
        SetOperationsCard(CostCard, "\uE8C7", P("Cost and usage", "成本同用量"),
            P("Budgets, forecasts, anomalies and cost allocation", "預算、預測、異常同成本分配"), "ce");
        SetOperationsCard(HealthCard, "\uE95E", P("Account health", "帳戶健康"),
            P("AWS Health events, maintenance and affected resources", "AWS Health 事件、維護同受影響資源"), "health");
        SetOperationsCard(QuotasCard, "\uE9F9", P("Service quotas", "服務 quota"),
            P("Usage, adjustable limits and increase requests", "用量、可調整限制同提升申請"), "service-quotas");
    }

    private void SetOperationsCard(Border host, string glyph, string title, string summary, string serviceId)
    {
        var icon = new FontIcon { Glyph = glyph, FontSize = 18 };
        var heading = new TextBlock { Text = title, FontSize = 17, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var body = new TextBlock
        {
            Text = summary,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var button = new Button { Content = P("Open workspace", "開啟工作區"), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => OpenServiceWorkspace(serviceId);
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(icon);
        panel.Children.Add(heading);
        panel.Children.Add(body);
        panel.Children.Add(button);
        host.Child = panel;
    }

    private void ShowConsoleView(string view)
    {
        _activeConsoleView = view;
        ConsoleHomeView.Visibility = view == "home" ? Visibility.Visible : Visibility.Collapsed;
        ResourcesView.Visibility = view == "resources" ? Visibility.Visible : Visibility.Collapsed;
        S3View.Visibility = view == "s3" ? Visibility.Visible : Visibility.Collapsed;
        Ec2View.Visibility = view == "ec2" ? Visibility.Visible : Visibility.Collapsed;
        ServicesView.Visibility = view == "services" ? Visibility.Visible : Visibility.Collapsed;
        OperationsView.Visibility = view == "operations" ? Visibility.Visible : Visibility.Collapsed;
        AdvancedView.Visibility = view == "advanced" ? Visibility.Visible : Visibility.Collapsed;

        if (view != "advanced") EngineBar.IsOpen = false;
    }

    private async void ConsoleNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string view) return;
        ShowConsoleView(view);
        if (view == "advanced")
        {
            if (!_cliInstalled) ShowAdvancedCliInstall();
            if (_services.Count == 0) await LoadServices();
        }
        else if (view == "ec2" && _ec2LoadedContextGeneration != _awsContext.CurrentGeneration)
        {
            await RefreshEc2Async();
        }
        else if (view == "s3" && _bucketRows.Count == 0)
        {
            await RefreshS3Async();
        }
    }

    private void ApplyPendingConsoleView()
    {
        var view = _pendingConsoleView;
        _pendingConsoleView = null;
        var item = view switch
        {
            "resources" => ResourcesNav,
            "s3" => S3Nav,
            "ec2" => Ec2Nav,
            "services" => ServicesNav,
            "operations" => OperationsNav,
            "advanced" => AdvancedNav,
            _ => ConsoleHomeNav,
        };
        NavigateConsole(item, view ?? "home");
    }

    private void ShowAdvancedCliInstall()
    {
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Informational;
        EngineBar.Title = P("AWS CLI is optional and not installed", "AWS CLI 係選用功能，現時未安裝");
        EngineBar.Message = P(
            "The Console manager uses the managed AWS SDK. Install aws.exe only for this Advanced CLI workbench.",
            "Console 管理中心用受管理 AWS SDK。只需為呢個進階 CLI 工作台安裝 aws.exe。");
        EngineBar.ActionButton = null;
        EngineBar.Content = EngineBars.AutoInstallProgress(
            AwsCliService.WingetId, "Install AWS CLI for the workbench", "為工作台安裝 AWS CLI",
            recheck: async () => { await CheckEngine(); await LoadServices(); },
            rescan: AwsCliService.Rescan);
    }

    private void GlobalSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var query = (args.QueryText ?? sender.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(query)) return;
        var service = _consoleServices.FirstOrDefault(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || s.Id.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (service is not null) OpenServiceWorkspace(service.Id);
        else OpenResourceSearch(query);
    }

    private async void HomeRefresh_Click(object sender, RoutedEventArgs e)
        => await RefreshConsoleContextAsync(loadResources: true);

    private void OpenS3_Click(object sender, RoutedEventArgs e) => NavigateConsole(S3Nav, "s3");
    private void OpenEc2_Click(object sender, RoutedEventArgs e) => NavigateConsole(Ec2Nav, "ec2");
    private void OpenResources_Click(object sender, RoutedEventArgs e) => NavigateConsole(ResourcesNav, "resources");

    private void HomeResourceSearch_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        OpenResourceSearch(HomeResourceSearchBox.Text ?? string.Empty);
    }

    private void HomeSearch_Click(object sender, RoutedEventArgs e)
        => OpenResourceSearch(HomeResourceSearchBox.Text ?? string.Empty);

    private void OpenResourceSearch(string query)
    {
        ResourceQueryBox.Text = query.Trim();
        NavigateConsole(ResourcesNav, "resources");
        _ = RefreshResourcesAsync();
    }

    private void NavigateConsole(NavigationViewItem item, string view)
    {
        ConsoleNavigation.SelectedItem = item;
        ShowConsoleView(view);
    }

    private void ServiceGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AwsServiceCardView service) OpenServiceWorkspace(service.Id);
    }

    private void OpenServiceWorkspace(string serviceId)
    {
        if (serviceId.Equals("s3", StringComparison.OrdinalIgnoreCase))
        {
            NavigateConsole(S3Nav, "s3");
            return;
        }
        if (serviceId.Equals("ec2", StringComparison.OrdinalIgnoreCase))
        {
            NavigateConsole(Ec2Nav, "ec2");
            return;
        }
        var resourceNamespace = _consoleServices.FirstOrDefault(service =>
            service.Id.Equals(serviceId, StringComparison.OrdinalIgnoreCase))?.ResourceNamespace ?? serviceId;
        OpenResourceSearch($"service:{resourceNamespace}");
    }

    private void ConsoleServiceSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) FilterConsoleServices();
    }

    private void ConsoleCategory_Changed(object sender, SelectionChangedEventArgs e) => FilterConsoleServices();

    private void ResourceQuery_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        _ = RefreshResourcesAsync();
    }

    private void ResourceTypeFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyResourceFilter();
    private void CrossRegionToggle_Toggled(object sender, RoutedEventArgs e) { }
    private async void ResourceRefresh_Click(object sender, RoutedEventArgs e) => await RefreshResourcesAsync();
    private async void ResourceLoadMore_Click(object sender, RoutedEventArgs e) => await LoadMoreResourcesAsync();

    private void ApplyResourceFilter()
    {
        ResourceList.Items.Clear();
        var selected = ResourceTypeFilter.SelectedIndex > 0 ? ResourceTypeFilter.SelectedItem as string : null;
        var selectedServiceId = string.IsNullOrWhiteSpace(selected)
            ? null
            : _consoleServices.FirstOrDefault(service =>
                service.Name.Equals(selected, StringComparison.OrdinalIgnoreCase))?.ResourceNamespace;
        foreach (var row in _resourceRows.Where(r => string.IsNullOrEmpty(selected)
            || (!string.IsNullOrWhiteSpace(selectedServiceId)
                && r.Service.Equals(selectedServiceId, StringComparison.OrdinalIgnoreCase))))
            ResourceList.Items.Add(row);
    }

    private void ResourceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResourceList.SelectedItem is not AwsResourceRowView row) return;
        ResourceDetailName.Text = row.Name;
        ResourceDetailType.Text = $"{row.Type} · {row.Region} · {row.Status}";
        ResourceDetailArn.Text = row.Arn;
        ResourceDetailMeta.Text = string.IsNullOrWhiteSpace(row.Identifier)
            ? P(
                "Resource Explorer does not guarantee the Cloud Control primary identifier. Lifecycle actions stay disabled until a verified identifier is available.",
                "Resource Explorer 唔保證會提供 Cloud Control 主要識別碼；未驗證識別碼之前，生命週期操作會保持停用。")
            : P(
                "Use the owning service workspace for configuration and lifecycle actions.",
                "用所屬服務工作區做設定同生命週期操作。");
        ResourceCopyArnBtn.IsEnabled = !string.IsNullOrWhiteSpace(row.Arn);
        ResourceOpenServiceBtn.IsEnabled = true;
        var lifecycle = !string.IsNullOrWhiteSpace(row.CloudFormationType)
                        && !string.IsNullOrWhiteSpace(row.Identifier);
        ResourceInspectBtn.IsEnabled = lifecycle;
        ResourceEditBtn.IsEnabled = lifecycle;
        ResourceDeleteBtn.IsEnabled = lifecycle;
    }

    private void ResourceCopyArn_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceList.SelectedItem is not AwsResourceRowView row || string.IsNullOrWhiteSpace(row.Arn)) return;
        var data = new DataPackage();
        data.SetText(row.Arn);
        Clipboard.SetContent(data);
        ShowResult(WinForge.Models.TweakResult.Ok("ARN copied.", "已複製 ARN。"));
    }

    private void ResourceOpenService_Click(object sender, RoutedEventArgs e)
    {
        if (ResourceList.SelectedItem is AwsResourceRowView row) OpenServiceWorkspace(row.Service);
    }

    private async void ResourceCreate_Click(object sender, RoutedEventArgs e) => await CreateCloudResourceAsync();
    private async void ResourceInspect_Click(object sender, RoutedEventArgs e) => await InspectCloudResourceAsync();
    private async void ResourceEdit_Click(object sender, RoutedEventArgs e) => await UpdateCloudResourceAsync();
    private async void ResourceDelete_Click(object sender, RoutedEventArgs e) => await DeleteCloudResourceAsync();

    private void S3BucketFilter_TextChanged(object sender, TextChangedEventArgs e) => ApplyBucketFilter();

    private void ApplyBucketFilter()
    {
        var filter = (S3BucketFilterBox.Text ?? string.Empty).Trim();
        S3BucketList.Items.Clear();
        foreach (var bucket in _bucketRows.Where(b => string.IsNullOrEmpty(filter)
            || b.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            S3BucketList.Items.Add(bucket);
    }

    private async void S3Refresh_Click(object sender, RoutedEventArgs e) => await RefreshS3Async();

    private void S3BucketList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = S3BucketList.SelectedItem is S3BucketRowView;
        ResetS3WorkspaceForBucketChange();
        if (!selected) return;
        _activeS3Prefix = string.Empty;
        S3PrefixBox.Text = string.Empty;
        _ = LoadSelectedBucketAsync();
    }

    private void S3ObjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (S3ObjectList.SelectedItem is not S3ObjectRowView row
            || !HasLoadedS3Bucket()
            || !string.Equals(row.BucketName, _loadedS3BucketName, StringComparison.Ordinal))
        {
            S3DownloadBtn.IsEnabled = false;
            return;
        }
        S3DownloadBtn.IsEnabled = !row.IsPrefix;
    }

    private void S3ObjectList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (S3ObjectList.SelectedItem is not S3ObjectRowView { IsPrefix: true } row
            || !string.Equals(row.BucketName, _loadedS3BucketName, StringComparison.Ordinal)) return;
        _activeS3Prefix = row.Key;
        S3PrefixBox.Text = _activeS3Prefix;
        _ = LoadObjectsAsync();
    }

    private void S3Prefix_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        e.Handled = true;
        _activeS3Prefix = (S3PrefixBox.Text ?? string.Empty).TrimStart('/');
        _ = LoadObjectsAsync();
    }

    private void S3Up_Click(object sender, RoutedEventArgs e)
    {
        var clean = _activeS3Prefix.TrimEnd('/');
        var slash = clean.LastIndexOf('/');
        _activeS3Prefix = slash < 0 ? string.Empty : clean[..(slash + 1)];
        S3PrefixBox.Text = _activeS3Prefix;
        _ = LoadObjectsAsync();
    }

    private async void S3CreateBucket_Click(object sender, RoutedEventArgs e) => await CreateBucketAsync();
    private async void S3Upload_Click(object sender, RoutedEventArgs e) => await UploadObjectAsync();
    private async void S3Download_Click(object sender, RoutedEventArgs e) => await DownloadObjectAsync();
    private async void S3ObjectLoadMore_Click(object sender, RoutedEventArgs e) => await LoadMoreS3ObjectsAsync();
    private async void S3Delete_Click(object sender, RoutedEventArgs e) => await DeleteS3SelectionAsync();
    private void S3Setting_Changed(object sender, RoutedEventArgs e)
    {
        var kms = S3EncryptionBox.SelectedIndex is 1 or 2;
        S3KmsKeyBox.Visibility = kms ? Visibility.Visible : Visibility.Collapsed;
        S3BucketKeyToggle.Visibility = kms ? Visibility.Visible : Visibility.Collapsed;
        S3KmsKeyBox.IsEnabled = kms && HasLoadedS3Bucket() && S3EncryptionBox.IsEnabled;
        S3BucketKeyToggle.IsEnabled = S3EncryptionBox.SelectedIndex == 1 && HasLoadedS3Bucket() && S3EncryptionBox.IsEnabled;
        var configuredEncryption = S3EncryptionBox.SelectedIndex is >= 0 and <= 2;
        S3BlockSseCToggle.IsEnabled = configuredEncryption && HasLoadedS3Bucket() && S3EncryptionBox.IsEnabled;
        if (!configuredEncryption && S3BlockSseCToggle.IsOn)
        {
            var wasSuppressed = _suppressS3Settings;
            _suppressS3Settings = true;
            S3BlockSseCToggle.IsOn = false;
            _suppressS3Settings = wasSuppressed;
        }
        if (_suppressS3Settings) return;

        if (ReferenceEquals(sender, S3BlockPublicToggle))
        {
            _suppressS3Settings = true;
            S3BlockPublicAclsToggle.IsOn = S3BlockPublicToggle.IsOn;
            S3IgnorePublicAclsToggle.IsOn = S3BlockPublicToggle.IsOn;
            S3BlockPublicPolicyToggle.IsOn = S3BlockPublicToggle.IsOn;
            S3RestrictPublicBucketsToggle.IsOn = S3BlockPublicToggle.IsOn;
            _suppressS3Settings = false;
        }
        else if (ReferenceEquals(sender, S3BlockPublicAclsToggle)
                 || ReferenceEquals(sender, S3IgnorePublicAclsToggle)
                 || ReferenceEquals(sender, S3BlockPublicPolicyToggle)
                 || ReferenceEquals(sender, S3RestrictPublicBucketsToggle))
        {
            _suppressS3Settings = true;
            S3BlockPublicToggle.IsOn = S3BlockPublicAclsToggle.IsOn
                                      && S3IgnorePublicAclsToggle.IsOn
                                      && S3BlockPublicPolicyToggle.IsOn
                                      && S3RestrictPublicBucketsToggle.IsOn;
            _suppressS3Settings = false;
        }

        if (ReferenceEquals(sender, S3VersioningToggle)) _s3VersioningDirty = true;
        if (ReferenceEquals(sender, S3EncryptionBox)
            || ReferenceEquals(sender, S3KmsKeyBox)
            || ReferenceEquals(sender, S3BucketKeyToggle)
            || ReferenceEquals(sender, S3BlockSseCToggle)) _s3EncryptionDirty = true;
        if (ReferenceEquals(sender, S3BlockPublicToggle)
            || ReferenceEquals(sender, S3BlockPublicAclsToggle)
            || ReferenceEquals(sender, S3IgnorePublicAclsToggle)
            || ReferenceEquals(sender, S3BlockPublicPolicyToggle)
            || ReferenceEquals(sender, S3RestrictPublicBucketsToggle)) _s3PublicAccessDirty = true;
        if (ReferenceEquals(sender, S3PolicyBox)) _s3PolicyDirty = true;
        if (ReferenceEquals(sender, S3LifecycleBox)) _s3LifecycleDirty = true;
        if (ReferenceEquals(sender, S3CorsBox)) _s3CorsDirty = true;
        if (ReferenceEquals(sender, S3TagsBox)) _s3TagsDirty = true;
        S3StatusText.Text = P("Unsaved changes", "有未儲存變更");
    }
    private void S3Cancel_Click(object sender, RoutedEventArgs e) => _s3OperationCts?.Cancel();
    private async void S3SaveProperties_Click(object sender, RoutedEventArgs e) => await SaveS3PropertiesAsync();
    private async void S3SavePermissions_Click(object sender, RoutedEventArgs e) => await SaveS3PermissionsAsync();
    private async void S3SaveManagement_Click(object sender, RoutedEventArgs e) => await SaveS3ManagementAsync();
    private async void S3SaveTags_Click(object sender, RoutedEventArgs e) => await SaveS3TagsAsync();

    private void RebuildEc2StateFilter()
    {
        if (Ec2StateFilter is null) return;
        var selected = Math.Max(0, Ec2StateFilter.SelectedIndex);
        Ec2StateFilter.Items.Clear();
        foreach (var value in new[]
                 {
                     P("All states", "所有狀態"), P("Pending", "等待中"), P("Running", "運行中"),
                     P("Stopping", "停止緊"), P("Stopped", "已停止"), P("Shutting down", "關閉緊"),
                     P("Terminated", "已終止"),
                 })
            Ec2StateFilter.Items.Add(value);
        Ec2StateFilter.SelectedIndex = Math.Min(selected, Ec2StateFilter.Items.Count - 1);
    }

    private static string? SelectedEc2State(int index) => index switch
    {
        1 => "pending",
        2 => "running",
        3 => "stopping",
        4 => "stopped",
        5 => "shutting-down",
        6 => "terminated",
        _ => null,
    };

    private void Ec2Filter_TextChanged(object sender, TextChangedEventArgs e) => ApplyEc2Filter();
    private void Ec2StateFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyEc2Filter();
    private async void Ec2Refresh_Click(object sender, RoutedEventArgs e) => await RefreshEc2Async();
    private async void Ec2LoadMore_Click(object sender, RoutedEventArgs e) => await LoadMoreEc2Async();
    private async void Ec2Start_Click(object sender, RoutedEventArgs e) => await ChangeEc2StateAsync(AwsEc2InstanceAction.Start);
    private async void Ec2Stop_Click(object sender, RoutedEventArgs e) => await ChangeEc2StateAsync(AwsEc2InstanceAction.Stop);
    private async void Ec2Reboot_Click(object sender, RoutedEventArgs e) => await ChangeEc2StateAsync(AwsEc2InstanceAction.Reboot);
    private async void Ec2Terminate_Click(object sender, RoutedEventArgs e) => await ChangeEc2StateAsync(AwsEc2InstanceAction.Terminate);

    private void ApplyEc2Filter()
    {
        if (Ec2InstanceList is null) return;
        var query = (Ec2FilterBox.Text ?? string.Empty).Trim();
        var state = SelectedEc2State(Ec2StateFilter.SelectedIndex);
        Ec2InstanceList.Items.Clear();
        foreach (var row in _ec2Rows.Where(row =>
                     (state is null || row.State.Equals(state, StringComparison.Ordinal))
                     && (query.Length == 0
                         || row.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || row.InstanceId.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || row.InstanceType.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || row.AvailabilityZone.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || row.PrivateIpAddress.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || row.PublicIpAddress.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || row.Instance.Tags.Any(tag => tag.Key.Contains(query, StringComparison.OrdinalIgnoreCase)
                                                        || tag.Value.Contains(query, StringComparison.OrdinalIgnoreCase)))))
            Ec2InstanceList.Items.Add(row);
        Ec2StatusText.Text = P(
            $"Showing {Ec2InstanceList.Items.Count} of {_ec2Rows.Count} loaded instance(s).",
            $"顯示已載入 {_ec2Rows.Count} 個執行個體之中嘅 {Ec2InstanceList.Items.Count} 個。");
    }

    private void Ec2InstanceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Ec2InstanceList.SelectedItem is not AwsEc2InstanceRowView row)
        {
            ResetEc2Details();
            return;
        }

        var instance = row.Instance;
        Ec2DetailName.Text = instance.Name;
        Ec2DetailId.Text = instance.InstanceId;
        Ec2DetailStateType.Text = P($"State: {instance.State} · Type: {instance.InstanceType} · Monitoring: {instance.MonitoringState}",
            $"狀態：{instance.State} · 類型：{instance.InstanceType} · 監察：{instance.MonitoringState}");
        Ec2DetailLocation.Text = P($"Zone: {instance.AvailabilityZone} · VPC: {instance.VpcId ?? "—"} · Subnet: {instance.SubnetId ?? "—"}",
            $"可用區域：{instance.AvailabilityZone} · VPC：{instance.VpcId ?? "—"} · 子網絡：{instance.SubnetId ?? "—"}");
        Ec2DetailNetwork.Text = P($"Private: {instance.PrivateIpAddress ?? "—"} · Public: {instance.PublicIpAddress ?? "—"}",
            $"私人：{instance.PrivateIpAddress ?? "—"} · 公開：{instance.PublicIpAddress ?? "—"}");
        Ec2DetailImage.Text = P($"AMI: {instance.ImageId} · Launched: {instance.LaunchTime?.LocalDateTime.ToString("g") ?? "—"}",
            $"AMI：{instance.ImageId} · 啟動時間：{instance.LaunchTime?.LocalDateTime.ToString("g") ?? "—"}");
        Ec2DetailPlatform.Text = P($"Platform: {instance.PlatformDetails} · Architecture: {instance.Architecture} · Lifecycle: {instance.Lifecycle} · Tenancy: {instance.Tenancy}",
            $"平台：{instance.PlatformDetails} · 架構：{instance.Architecture} · 生命週期：{instance.Lifecycle} · 租用：{instance.Tenancy}");
        Ec2DetailProfile.Text = P($"IAM profile: {instance.IamInstanceProfileArn ?? "—"} · Key pair: {instance.KeyName ?? "—"}",
            $"IAM profile：{instance.IamInstanceProfileArn ?? "—"} · Key pair：{instance.KeyName ?? "—"}");
        Ec2DetailSecurity.Text = P("Security groups: ", "保安群組：") + (instance.SecurityGroups.Count == 0
            ? "—"
            : string.Join(", ", instance.SecurityGroups.Select(group => $"{group.GroupName} ({group.GroupId})")));
        Ec2DetailTags.Text = instance.Tags.Count == 0
            ? P("Tags: none", "標籤：冇")
            : P("Tags: ", "標籤：") + string.Join(" · ", instance.Tags.OrderBy(tag => tag.Key).Select(tag => $"{tag.Key}={tag.Value}"));
        SetEc2ActionButtonsForState(instance.State);
    }

    private void ResetEc2Details()
    {
        Ec2DetailName.Text = "—";
        Ec2DetailId.Text = string.Empty;
        Ec2DetailStateType.Text = string.Empty;
        Ec2DetailLocation.Text = string.Empty;
        Ec2DetailNetwork.Text = string.Empty;
        Ec2DetailImage.Text = string.Empty;
        Ec2DetailPlatform.Text = string.Empty;
        Ec2DetailProfile.Text = string.Empty;
        Ec2DetailSecurity.Text = string.Empty;
        Ec2DetailTags.Text = string.Empty;
        Ec2StartBtn.IsEnabled = false;
        Ec2StopBtn.IsEnabled = false;
        Ec2RebootBtn.IsEnabled = false;
        Ec2TerminateBtn.IsEnabled = false;
    }

    private bool HasLoadedS3Bucket()
        => S3BucketList.SelectedItem is S3BucketRowView bucket
           && string.Equals(bucket.Name, _loadedS3BucketName, StringComparison.Ordinal);

    private bool TryGetLoadedS3Bucket(out S3BucketRowView bucket)
    {
        if (S3BucketList.SelectedItem is S3BucketRowView selected
            && string.Equals(selected.Name, _loadedS3BucketName, StringComparison.Ordinal))
        {
            bucket = selected;
            return true;
        }

        bucket = null!;
        S3StatusText.Text = P("Wait for the selected bucket to finish loading.", "請等候所選儲存桶完成載入。");
        return false;
    }

    private void ResetS3DirtyFlags()
    {
        _s3VersioningDirty = false;
        _s3EncryptionDirty = false;
        _s3PublicAccessDirty = false;
        _s3PolicyDirty = false;
        _s3LifecycleDirty = false;
        _s3CorsDirty = false;
        _s3TagsDirty = false;
    }

    private void ResetS3WorkspaceForBucketChange()
    {
        unchecked { _s3BucketLoadGeneration++; }
        CancelCurrentS3Operation();
        _loadedS3BucketName = null;
        _s3ObjectNextToken = null;
        _s3ObjectPageBucket = null;
        _s3ObjectPagePrefix = null;
        _objectRows.Clear();
        S3ObjectList.Items.Clear();
        S3ObjectList.SelectedItem = null;
        S3UploadBtn.IsEnabled = false;
        S3DeleteBtn.IsEnabled = false;
        S3DownloadBtn.IsEnabled = false;
        S3UpBtn.IsEnabled = false;
        S3ObjectLoadMoreBtn.IsEnabled = false;
        S3ObjectLoadMoreBtn.Visibility = Visibility.Collapsed;
        S3PrefixBox.IsEnabled = false;
        S3ObjectList.IsEnabled = false;

        _suppressS3Settings = true;
        _loadedVersioning = null;
        _loadedEncryption = null;
        _loadedPublicAccess = null;
        _loadedTags = null;
        _loadedPolicy = null;
        _loadedLifecycle = null;
        _loadedCors = null;
        S3VersioningToggle.IsOn = false;
        S3EncryptionBox.SelectedIndex = 3;
        S3KmsKeyBox.Text = string.Empty;
        S3BucketKeyToggle.IsOn = false;
        S3BlockSseCToggle.IsOn = false;
        S3BlockPublicToggle.IsOn = false;
        S3BlockPublicAclsToggle.IsOn = false;
        S3IgnorePublicAclsToggle.IsOn = false;
        S3BlockPublicPolicyToggle.IsOn = false;
        S3RestrictPublicBucketsToggle.IsOn = false;
        S3PolicyBox.Text = string.Empty;
        S3LifecycleBox.Text = string.Empty;
        S3CorsBox.Text = string.Empty;
        S3TagsBox.Text = string.Empty;
        S3BucketRegionText.Text = string.Empty;
        _suppressS3Settings = false;
        ResetS3DirtyFlags();

        S3VersioningToggle.IsEnabled = false;
        S3EncryptionBox.IsEnabled = false;
        S3KmsKeyBox.IsEnabled = false;
        S3BucketKeyToggle.IsEnabled = false;
        S3BlockSseCToggle.IsEnabled = false;
        S3BlockPublicToggle.IsEnabled = false;
        S3BlockPublicAclsToggle.IsEnabled = false;
        S3IgnorePublicAclsToggle.IsEnabled = false;
        S3BlockPublicPolicyToggle.IsEnabled = false;
        S3RestrictPublicBucketsToggle.IsEnabled = false;
        S3PolicyBox.IsEnabled = false;
        S3LifecycleBox.IsEnabled = false;
        S3CorsBox.IsEnabled = false;
        S3TagsBox.IsEnabled = false;
        S3SavePropertiesBtn.IsEnabled = false;
        S3SavePermissionsBtn.IsEnabled = false;
        S3SaveManagementBtn.IsEnabled = false;
        S3SaveTagsBtn.IsEnabled = false;
    }

    private void ScheduleConsoleContextRefresh()
    {
        if (!_consoleInitialized) return;
        _awsContext.Restart();
        _s3OperationCts?.Cancel();
        DisposeAwsManager();
        InvalidateConsoleAccountState();
        _ = RefreshConsoleContextAsync(loadResources: false);
    }

    private void DisposeConsoleSession()
    {
        _awsContext.Dispose();
        CancelCurrentS3Operation();
        _ec2OperationCts?.Cancel();
        _ec2OperationCts?.Dispose();
        _ec2OperationCts = null;
        _ec2OperationId++;
        DisposeAwsManager();
    }

    private bool TryCaptureAwsContext(out int generation, out CancellationToken token)
    {
        generation = _awsContext.CurrentGeneration;
        if (_awsContext.TryGetToken(generation, out token)) return true;
        if (!_consoleInitialized) return false;
        generation = _awsContext.Restart();
        return _awsContext.TryGetToken(generation, out token);
    }

    private void InvalidateConsoleAccountState()
    {
        _resourceRows.Clear();
        ResourceList.Items.Clear();
        ResourceList.SelectedItem = null;
        _resourceNextToken = null;
        _resourceActiveQuery = null;
        ResourceCountText.Text = "—";
        ResourceStatusText.Text = P("Choose a profile and search to load this account.", "揀 profile 再搜尋，載入呢個帳戶。");
        ResourceLoadMoreBtn.Visibility = Visibility.Collapsed;
        ResourceLoadMoreBtn.IsEnabled = false;
        ResourceDetailName.Text = "—";
        ResourceDetailType.Text = string.Empty;
        ResourceDetailArn.Text = string.Empty;
        ResourceDetailMeta.Text = string.Empty;
        ResourceCopyArnBtn.IsEnabled = false;
        ResourceOpenServiceBtn.IsEnabled = false;
        ResourceInspectBtn.IsEnabled = false;
        ResourceEditBtn.IsEnabled = false;
        ResourceDeleteBtn.IsEnabled = false;

        _bucketRows.Clear();
        S3BucketList.Items.Clear();
        S3BucketList.SelectedItem = null;
        BucketCountText.Text = "—";
        ResetS3WorkspaceForBucketChange();
        S3StatusText.Text = P("Choose a profile, then refresh to load buckets.", "揀 profile，再重新整理以載入儲存桶。");

        _ec2OperationCts?.Cancel();
        _ec2OperationCts?.Dispose();
        _ec2OperationCts = null;
        _ec2OperationId++;
        _ec2ReviewPending = false;
        _ec2Rows.Clear();
        Ec2InstanceList.Items.Clear();
        Ec2InstanceList.SelectedItem = null;
        _ec2NextToken = null;
        _ec2LoadedContextGeneration = -1;
        Ec2LoadMoreBtn.Visibility = Visibility.Collapsed;
        Ec2LoadMoreBtn.IsEnabled = false;
        Ec2Progress.IsActive = false;
        Ec2StatusText.Text = P("Choose a profile, then refresh this Region.", "揀 profile，再重新整理呢個 Region。");
        ResetEc2Details();

        IdentityNameText.Text = P("Switching context…", "切換緊情境…");
        IdentityArnText.Text = string.Empty;
        ConsoleScopeText.Text = P("Verifying", "驗證緊");
        ResultBar.IsOpen = false;
    }

    private IDisposable BeginAwsMutation()
    {
        _awsMutationCount++;
        SetAwsContextControlsEnabled(false);
        return new AwsMutationLease(this);
    }

    private void EndAwsMutation()
    {
        if (_awsMutationCount > 0) _awsMutationCount--;
        if (_awsMutationCount == 0) SetAwsContextControlsEnabled(true);
    }

    private void SetAwsContextControlsEnabled(bool enabled)
    {
        ProfileBox.IsEnabled = enabled;
        RegionBox.IsEnabled = enabled;
        SsoBtn.IsEnabled = enabled;
        ConfigureBtn.IsEnabled = enabled;
    }

    private sealed class AwsMutationLease(AwsCliModule owner) : IDisposable
    {
        private AwsCliModule? _owner = owner;

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            current?.EndAwsMutation();
        }
    }

    // Implemented in AwsCliModule.Manager.cs once the managed AWS SDK service is available.
    private partial Task RefreshConsoleContextAsync(bool loadResources, CancellationToken cancellationToken = default);
    private partial Task RefreshResourcesAsync();
    private partial Task LoadMoreResourcesAsync();
    private partial Task RefreshS3Async();
    private partial Task RefreshEc2Async();
    private partial Task LoadMoreEc2Async();
    private partial Task ChangeEc2StateAsync(AwsEc2InstanceAction action);
    private partial Task LoadMoreS3ObjectsAsync();
    private partial Task LoadSelectedBucketAsync();
    private partial Task LoadObjectsAsync();
    private partial Task CreateBucketAsync();
    private partial Task UploadObjectAsync();
    private partial Task DownloadObjectAsync();
    private partial Task DeleteS3SelectionAsync();
    private partial Task SaveS3PropertiesAsync();
    private partial Task SaveS3PermissionsAsync();
    private partial Task SaveS3ManagementAsync();
    private partial Task SaveS3TagsAsync();
    private partial Task CreateCloudResourceAsync();
    private partial Task InspectCloudResourceAsync();
    private partial Task UpdateCloudResourceAsync();
    private partial Task DeleteCloudResourceAsync();
    private partial void DisposeAwsManager();
}
