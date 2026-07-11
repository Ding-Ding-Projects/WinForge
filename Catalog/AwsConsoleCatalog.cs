using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WinForge.Catalog;

/// <summary>
/// Top-level groups used by the AWS Console-style service navigator.
/// </summary>
public enum AwsConsoleCategory
{
    Compute,
    Storage,
    Database,
    NetworkingAndContentDelivery,
    SecurityIdentityAndCompliance,
    Containers,
    Serverless,
    ApplicationIntegration,
    ManagementAndGovernance,
    Analytics,
    MachineLearningAndAI,
    DeveloperTools,
    MigrationAndTransfer,
    BillingAndCostManagement,
    Other,
}

/// <summary>
/// Describes how much purpose-built UI an adapter can provide for a service.
/// The generic command surface remains available at every level.
/// </summary>
public enum AwsAdapterCapabilityLevel
{
    /// <summary>Generic operation and parameter forms generated from the AWS CLI.</summary>
    GenericCommands = 0,

    /// <summary>Generic commands plus Resource Explorer-backed inventory.</summary>
    ResourceDiscovery = 1,

    /// <summary>Inventory plus common list, inspect, create, update and delete actions.</summary>
    ResourceLifecycle = 2,

    /// <summary>A rich, service-specific management workspace.</summary>
    RichManager = 3,
}

/// <summary>
/// Localized metadata for one AWS Console navigation category.
/// </summary>
public sealed record AwsConsoleCategoryDefinition(
    AwsConsoleCategory Id,
    string NameEn,
    string NameZh,
    string Glyph,
    int SortOrder);

/// <summary>
/// One discoverable AWS CLI service. Resource type hints use the suffix from the
/// AWS Resource Explorer type (for example, <c>AWS::EC2::Instance</c> becomes
/// namespace <c>ec2</c> and hint <c>instance</c>).
/// </summary>
public sealed record AwsConsoleServiceDefinition(
    string CliId,
    string NameEn,
    string NameZh,
    string DescriptionEn,
    string DescriptionZh,
    AwsConsoleCategory Category,
    string Glyph,
    string ResourceExplorerNamespace,
    IReadOnlyList<string> ResourceTypeHints,
    AwsAdapterCapabilityLevel AdapterCapability,
    bool IsCurated = true)
{
    public string DisplayName(bool cantonese) => cantonese ? NameZh : NameEn;
    public string Description(bool cantonese) => cantonese ? DescriptionZh : DescriptionEn;

    public string SearchText => string.Join(' ', new[]
    {
        CliId, NameEn, NameZh, DescriptionEn, DescriptionZh, ResourceExplorerNamespace,
        string.Join(' ', ResourceTypeHints),
    }).ToLowerInvariant();
}

/// <summary>
/// Curated AWS Console-style service metadata. <see cref="Build"/> always unions this
/// catalog with the service ids reported by the installed AWS CLI, so new or uncommon
/// AWS services remain reachable even before WinForge gains bespoke metadata for them.
/// </summary>
public static class AwsConsoleCatalog
{
    private const string ComputeGlyph = "\uE950";
    private const string StorageGlyph = "\uE7C0";
    private const string DatabaseGlyph = "\uE8F1";
    private const string NetworkGlyph = "\uE968";
    private const string SecurityGlyph = "\uE72E";
    private const string ContainersGlyph = "\uE7B8";
    private const string ServerlessGlyph = "\uE945";
    private const string IntegrationGlyph = "\uE774";
    private const string ManagementGlyph = "\uE713";
    private const string AnalyticsGlyph = "\uE9D2";
    private const string AiGlyph = "\uE99A";
    private const string DeveloperGlyph = "\uE943";
    private const string MigrationGlyph = "\uE8AB";
    private const string CostGlyph = "\uE8C7";
    private const string OtherGlyph = "\uE8FD";

    public static IReadOnlyList<AwsConsoleCategoryDefinition> Categories { get; } =
    [
        new(AwsConsoleCategory.Compute, "Compute", "運算", ComputeGlyph, 0),
        new(AwsConsoleCategory.Storage, "Storage", "儲存", StorageGlyph, 1),
        new(AwsConsoleCategory.Database, "Database", "資料庫", DatabaseGlyph, 2),
        new(AwsConsoleCategory.NetworkingAndContentDelivery, "Networking & content delivery", "網絡同內容傳送", NetworkGlyph, 3),
        new(AwsConsoleCategory.SecurityIdentityAndCompliance, "Security, identity & compliance", "保安、身分同合規", SecurityGlyph, 4),
        new(AwsConsoleCategory.Containers, "Containers", "容器", ContainersGlyph, 5),
        new(AwsConsoleCategory.Serverless, "Serverless", "無伺服器運算", ServerlessGlyph, 6),
        new(AwsConsoleCategory.ApplicationIntegration, "Application integration", "應用程式整合", IntegrationGlyph, 7),
        new(AwsConsoleCategory.ManagementAndGovernance, "Management & governance", "管理同管治", ManagementGlyph, 8),
        new(AwsConsoleCategory.Analytics, "Analytics", "分析", AnalyticsGlyph, 9),
        new(AwsConsoleCategory.MachineLearningAndAI, "Machine learning & AI", "機器學習同 AI", AiGlyph, 10),
        new(AwsConsoleCategory.DeveloperTools, "Developer tools", "開發者工具", DeveloperGlyph, 11),
        new(AwsConsoleCategory.MigrationAndTransfer, "Migration & transfer", "遷移同傳輸", MigrationGlyph, 12),
        new(AwsConsoleCategory.BillingAndCostManagement, "Billing & cost management", "帳單同成本管理", CostGlyph, 13),
        new(AwsConsoleCategory.Other, "Other AWS services", "其他 AWS 服務", OtherGlyph, 99),
    ];

    /// <summary>Purpose-written metadata for the major AWS Console services.</summary>
    public static IReadOnlyList<AwsConsoleServiceDefinition> Curated { get; } = CreateCurated();

    /// <summary>
    /// Builds the service navigator by merging curated metadata with live AWS CLI ids.
    /// Curated entries win on duplicate ids; every non-empty live id receives a fallback entry.
    /// </summary>
    public static IReadOnlyList<AwsConsoleServiceDefinition> Build(IEnumerable<string>? liveCliServiceIds = null) =>
        MergeWithLiveServices(liveCliServiceIds);

    public static IReadOnlyList<AwsConsoleServiceDefinition> MergeWithLiveServices(
        IEnumerable<string>? liveCliServiceIds)
    {
        var merged = new Dictionary<string, AwsConsoleServiceDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in Curated)
            merged[service.CliId] = service;

        if (liveCliServiceIds is not null)
        {
            foreach (var rawId in liveCliServiceIds)
            {
                var id = NormalizeCliId(rawId);
                if (id.Length == 0 || merged.ContainsKey(id)) continue;
                merged[id] = CreateLiveFallback(id);
            }
        }

        return merged.Values
            .OrderBy(service => CategoryOrder(service.Category))
            .ThenBy(service => service.NameEn, StringComparer.OrdinalIgnoreCase)
            .ThenBy(service => service.CliId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static AwsConsoleServiceDefinition? Find(string? cliId) =>
        string.IsNullOrWhiteSpace(cliId)
            ? null
            : Curated.FirstOrDefault(service =>
                string.Equals(service.CliId, cliId.Trim(), StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<AwsConsoleServiceDefinition> CreateCurated() =>
    [
        // Compute
        S("ec2", "Amazon EC2", "EC2 虛擬伺服器", "Create and operate virtual machines, images, volumes, networking and capacity.", "開同管理虛擬伺服器、映像、磁碟、網絡同運算容量。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.RichManager, "ec2", "instance", "image", "volume", "snapshot", "security-group", "key-pair", "subnet", "vpc"),
        S("autoscaling", "EC2 Auto Scaling", "EC2 自動擴縮", "Scale EC2 fleets from policies, schedules and health signals.", "按政策、時間表同健康狀態自動加減 EC2 主機。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceLifecycle, "autoscaling", "auto-scaling-group", "launch-configuration", "scaling-policy"),
        S("application-autoscaling", "Application Auto Scaling", "應用程式自動擴縮", "Manage scalable targets and policies for supported AWS services.", "管理各項 AWS 服務嘅擴縮目標同政策。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceLifecycle, "application-autoscaling", "scalable-target", "scaling-policy"),
        S("elasticbeanstalk", "AWS Elastic Beanstalk", "Elastic Beanstalk 應用程式平台", "Deploy web applications while AWS manages their compute stack.", "部署網站應用程式，底層運算環境就交畀 AWS 管理。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceLifecycle, "elasticbeanstalk", "application", "application-version", "environment"),
        S("lightsail", "Amazon Lightsail", "Lightsail 簡易雲端主機", "Operate simple virtual servers, containers, databases and static IPs.", "用簡單介面管理雲端主機、容器、資料庫同固定 IP。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceLifecycle, "lightsail", "instance", "container-service", "database", "static-ip"),
        S("batch", "AWS Batch", "AWS Batch 批次運算", "Run queued batch workloads on managed compute environments.", "喺受管運算環境排隊同執行批次工作。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceLifecycle, "batch", "compute-environment", "job-queue", "job-definition"),
        S("imagebuilder", "EC2 Image Builder", "EC2 映像建置器", "Automate secure VM and container image pipelines.", "自動建立同測試安全嘅虛擬機及容器映像。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceLifecycle, "imagebuilder", "image-pipeline", "component", "distribution-configuration", "container-recipe"),
        S("appstream", "Amazon AppStream 2.0", "AppStream 2.0 應用程式串流", "Stream desktop applications from managed fleets.", "由受管機群將桌面應用程式串流畀使用者。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceLifecycle, "appstream", "fleet", "stack", "image", "directory-config"),
        S("workspaces", "Amazon WorkSpaces", "WorkSpaces 雲端桌面", "Provision and manage persistent cloud desktops.", "建立同管理持久嘅雲端桌面。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceLifecycle, "workspaces", "workspace", "workspace-bundle", "workspace-directory"),
        S("outposts", "AWS Outposts", "AWS Outposts 混合雲硬件", "Manage AWS infrastructure installed at on-premises sites.", "管理裝喺本地機房嘅 AWS 基礎設施。", AwsConsoleCategory.Compute, AwsAdapterCapabilityLevel.ResourceDiscovery, "outposts", "outpost", "site"),

        // Storage
        S("s3", "Amazon S3", "S3 物件儲存", "Manage buckets and objects with high-level S3 commands.", "用高階指令管理 S3 儲存桶同物件。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.RichManager, "s3", "bucket", "access-point", "storage-lens"),
        S("s3api", "Amazon S3 API", "S3 完整管理 API", "Use the complete S3 control and data-plane command set.", "用完整 S3 控制面同資料面指令管理每項設定。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.RichManager, "s3", "bucket", "access-point", "storage-lens"),
        S("s3control", "Amazon S3 Control", "S3 帳戶級控制", "Manage account-level access points, jobs and Storage Lens settings.", "管理帳戶級存取點、批次工作同 Storage Lens 設定。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.ResourceLifecycle, "s3", "access-point", "job", "storage-lens"),
        S("efs", "Amazon EFS", "EFS 彈性檔案系統", "Operate elastic NFS file systems for Linux workloads.", "管理畀 Linux 工作負載用嘅彈性 NFS 檔案系統。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.ResourceLifecycle, "elasticfilesystem", "file-system", "access-point", "mount-target"),
        S("fsx", "Amazon FSx", "FSx 受管檔案系統", "Manage Windows, Lustre, NetApp ONTAP and OpenZFS file systems.", "管理 Windows、Lustre、NetApp ONTAP 同 OpenZFS 檔案系統。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.ResourceLifecycle, "fsx", "file-system", "volume", "storage-virtual-machine", "backup"),
        S("glacier", "S3 Glacier", "S3 Glacier 封存儲存", "Manage vaults and long-term archive retrieval jobs.", "管理長期封存保管庫同資料擷取工作。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.ResourceLifecycle, "glacier", "vault"),
        S("backup", "AWS Backup", "AWS Backup 集中備份", "Create central backup plans, vaults, selections and restore jobs.", "集中建立備份計劃、保管庫、資源選擇同還原工作。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.RichManager, "backup", "backup-plan", "backup-vault", "recovery-point", "restore-testing-plan"),
        S("storagegateway", "AWS Storage Gateway", "Storage Gateway 混合儲存", "Connect on-premises environments to AWS file, volume and tape storage.", "將本地環境接去 AWS 檔案、磁碟區同虛擬磁帶儲存。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.ResourceLifecycle, "storagegateway", "gateway", "file-share", "volume", "tape"),
        S("dlm", "Amazon Data Lifecycle Manager", "EC2 資料生命週期管理", "Automate EBS snapshot and AMI lifecycle policies.", "用政策自動管理 EBS 快照同 AMI 生命週期。", AwsConsoleCategory.Storage, AwsAdapterCapabilityLevel.ResourceLifecycle, "dlm", "lifecycle-policy"),

        // Database
        S("rds", "Amazon RDS", "RDS 關聯式資料庫", "Operate relational databases, clusters, snapshots, proxies and parameter groups.", "管理關聯式資料庫、叢集、快照、代理同參數組。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.RichManager, "rds", "db-instance", "db-cluster", "db-snapshot", "db-proxy", "db-parameter-group"),
        S("rds-data", "RDS Data API", "RDS Data API", "Run SQL statements against supported Aurora databases.", "對支援嘅 Aurora 資料庫執行 SQL 語句。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.GenericCommands, "rds", "db-cluster"),
        S("dynamodb", "Amazon DynamoDB", "DynamoDB NoSQL 資料庫", "Manage tables, indexes, backups, global tables and items.", "管理資料表、索引、備份、全域資料表同項目。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.RichManager, "dynamodb", "table", "global-table", "backup"),
        S("dynamodbstreams", "DynamoDB Streams", "DynamoDB 變更串流", "Read ordered table change records from DynamoDB Streams.", "讀取 DynamoDB 資料表嘅順序變更紀錄。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.GenericCommands, "dynamodb", "table"),
        S("elasticache", "Amazon ElastiCache", "ElastiCache 記憶體快取", "Operate managed Valkey, Redis OSS and Memcached caches.", "管理 Valkey、Redis OSS 同 Memcached 記憶體快取。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.ResourceLifecycle, "elasticache", "cache-cluster", "replication-group", "serverless-cache"),
        S("memorydb", "Amazon MemoryDB", "MemoryDB 持久記憶體資料庫", "Manage durable Valkey and Redis-compatible in-memory databases.", "管理持久嘅 Valkey 同 Redis 相容記憶體資料庫。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.ResourceLifecycle, "memorydb", "cluster", "snapshot", "parameter-group"),
        S("docdb", "Amazon DocumentDB", "DocumentDB 文件資料庫", "Operate MongoDB-compatible document database clusters.", "管理兼容 MongoDB 嘅文件資料庫叢集。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.ResourceLifecycle, "docdb", "db-cluster", "db-instance", "db-cluster-snapshot"),
        S("neptune", "Amazon Neptune", "Neptune 圖形資料庫", "Operate graph database clusters, instances and snapshots.", "管理圖形資料庫叢集、執行個體同快照。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.ResourceLifecycle, "neptune", "db-cluster", "db-instance", "db-cluster-snapshot"),
        S("keyspaces", "Amazon Keyspaces", "Keyspaces Cassandra 資料庫", "Manage serverless Apache Cassandra-compatible keyspaces and tables.", "管理無伺服器、兼容 Apache Cassandra 嘅 keyspace 同資料表。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.ResourceLifecycle, "cassandra", "keyspace", "table"),
        S("qldb", "Amazon QLDB", "QLDB 可驗證帳本資料庫", "Manage cryptographically verifiable ledger databases.", "管理可以用密碼學驗證嘅帳本資料庫。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.ResourceLifecycle, "qldb", "ledger"),
        S("dax", "Amazon DynamoDB Accelerator", "DAX DynamoDB 快取", "Manage in-memory acceleration clusters for DynamoDB.", "管理畀 DynamoDB 用嘅記憶體加速叢集。", AwsConsoleCategory.Database, AwsAdapterCapabilityLevel.ResourceLifecycle, "dax", "cluster", "parameter-group", "subnet-group"),

        // Networking and content delivery
        S("elbv2", "Elastic Load Balancing v2", "新版彈性負載平衡", "Manage application, network and gateway load balancers.", "管理應用程式、網絡同 Gateway Load Balancer。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.RichManager, "elasticloadbalancing", "loadbalancer", "targetgroup", "listener", "listener-rule"),
        S("elb", "Classic Load Balancer", "Classic Load Balancer", "Manage legacy Classic Load Balancers.", "管理舊式 Classic Load Balancer。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.ResourceLifecycle, "elasticloadbalancing", "loadbalancer"),
        S("cloudfront", "Amazon CloudFront", "CloudFront 內容傳送網絡", "Operate global distributions, origins, cache policies and invalidations.", "管理全球分派、來源、快取政策同清除要求。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.RichManager, "cloudfront", "distribution", "cache-policy", "origin-access-control", "function"),
        S("route53", "Amazon Route 53", "Route 53 DNS", "Manage hosted zones, DNS records, health checks and traffic policies.", "管理託管區域、DNS 紀錄、健康檢查同流量政策。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.RichManager, "route53", "hostedzone", "healthcheck", "trafficpolicy"),
        S("route53domains", "Route 53 Domains", "Route 53 網域註冊", "Register and administer internet domains.", "註冊同管理互聯網網域。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.ResourceLifecycle, "route53domains", "domain"),
        S("route53resolver", "Route 53 Resolver", "Route 53 Resolver 混合 DNS", "Manage DNS resolver endpoints, rules, query logging and DNS Firewall.", "管理 DNS Resolver 端點、規則、查詢記錄同 DNS Firewall。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.ResourceLifecycle, "route53resolver", "resolver-endpoint", "resolver-rule", "resolver-query-logging-config", "firewall-rule-group"),
        S("apigateway", "Amazon API Gateway REST APIs", "API Gateway REST API", "Build and operate REST APIs, stages, authorizers and API keys.", "建立同管理 REST API、stage、授權器同 API key。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.RichManager, "apigateway", "restapi", "stage", "apikey"),
        S("apigatewayv2", "Amazon API Gateway HTTP & WebSocket APIs", "API Gateway HTTP 同 WebSocket API", "Build and operate HTTP and WebSocket APIs.", "建立同管理 HTTP 同 WebSocket API。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.RichManager, "apigateway", "api", "stage", "domainname"),
        S("directconnect", "AWS Direct Connect", "Direct Connect 專線", "Manage dedicated network links between sites and AWS.", "管理由機房直連 AWS 嘅專用網絡線路。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.ResourceLifecycle, "directconnect", "connection", "virtual-interface", "gateway"),
        S("globalaccelerator", "AWS Global Accelerator", "Global Accelerator 全球加速", "Route global traffic through the AWS edge network.", "經 AWS 邊緣網絡路由全球流量。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.ResourceLifecycle, "globalaccelerator", "accelerator", "listener", "endpoint-group"),
        S("network-firewall", "AWS Network Firewall", "AWS Network Firewall", "Manage stateful firewalls, policies and rule groups for VPCs.", "管理 VPC 嘅狀態式防火牆、政策同規則組。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.ResourceLifecycle, "network-firewall", "firewall", "firewall-policy", "rule-group"),
        S("networkmanager", "AWS Network Manager", "AWS Network Manager", "Visualize and manage global and core network resources.", "睇同管理全球網絡及核心網絡資源。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.ResourceDiscovery, "networkmanager", "global-network", "core-network", "site", "device", "link"),
        S("servicediscovery", "AWS Cloud Map", "Cloud Map 服務探索", "Register application services and instances for discovery.", "登記應用程式服務同執行個體，等其他服務搵得到佢哋。", AwsConsoleCategory.NetworkingAndContentDelivery, AwsAdapterCapabilityLevel.ResourceLifecycle, "servicediscovery", "namespace", "service", "instance"),

        // Security, identity and compliance
        S("iam", "AWS Identity and Access Management", "IAM 身分同存取管理", "Manage users, roles, groups, policies, credentials and account security.", "管理使用者、角色、群組、政策、憑證同帳戶保安。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.RichManager, "iam", "role", "user", "group", "policy", "instance-profile", "saml-provider"),
        S("sts", "AWS Security Token Service", "STS 臨時安全憑證", "Inspect identity and request temporary AWS credentials.", "檢查目前身分同申請臨時 AWS 憑證。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.GenericCommands, "sts"),
        S("organizations", "AWS Organizations", "AWS Organizations 多帳戶管理", "Govern accounts, organizational units, policies and delegated administrators.", "管理帳戶、組織單位、政策同委派管理員。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.RichManager, "organizations", "account", "organizationalunit", "policy"),
        S("sso", "AWS IAM Identity Center sign-in", "IAM Identity Center 登入", "Retrieve IAM Identity Center role credentials and account access.", "取得 IAM Identity Center 角色憑證同帳戶存取權。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.GenericCommands, "sso"),
        S("sso-admin", "IAM Identity Center administration", "IAM Identity Center 管理", "Manage permission sets, assignments and identity sources.", "管理權限集、帳戶指派同身分來源。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceLifecycle, "sso", "permissionset", "application", "instance"),
        S("identitystore", "IAM Identity Center directory", "IAM Identity Center 目錄", "Manage users and groups in an Identity Center directory.", "管理 Identity Center 目錄入面嘅使用者同群組。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceLifecycle, "identitystore", "user", "group"),
        S("cognito-idp", "Amazon Cognito user pools", "Cognito 使用者集區", "Manage application users, sign-in flows, groups and app clients.", "管理應用程式使用者、登入流程、群組同 app client。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.RichManager, "cognito-idp", "userpool", "userpoolclient", "userpooldomain"),
        S("cognito-identity", "Amazon Cognito identity pools", "Cognito 身分集區", "Federate application identities and AWS credentials.", "整合應用程式身分同 AWS 憑證。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceLifecycle, "cognito-identity", "identitypool"),
        S("kms", "AWS Key Management Service", "KMS 加密金鑰管理", "Create and control encryption keys, aliases and grants.", "建立同控制加密金鑰、別名同授權。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.RichManager, "kms", "key", "alias"),
        S("secretsmanager", "AWS Secrets Manager", "Secrets Manager 機密資料管理", "Store, rotate and audit application secrets.", "安全儲存、輪替同稽核應用程式機密資料。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.RichManager, "secretsmanager", "secret"),
        S("acm", "AWS Certificate Manager", "ACM TLS 憑證管理", "Provision and manage public and private TLS certificates.", "申請同管理公開及私人 TLS 憑證。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceLifecycle, "acm", "certificate"),
        S("acm-pca", "AWS Private Certificate Authority", "AWS 私人憑證授權中心", "Operate private certificate authorities and issue certificates.", "管理私人憑證授權中心同簽發憑證。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceLifecycle, "acm-pca", "certificate-authority", "certificate"),
        S("wafv2", "AWS WAF", "AWS WAF 網頁防火牆", "Protect applications with web ACLs, rules and managed rule groups.", "用 Web ACL、規則同受管規則組保護應用程式。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.RichManager, "wafv2", "webacl", "rulegroup", "regexpatternset", "ipset"),
        S("shield", "AWS Shield", "AWS Shield DDoS 防護", "Manage DDoS protections and response-team access.", "管理 DDoS 防護同回應團隊存取。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceLifecycle, "shield", "protection", "protection-group"),
        S("guardduty", "Amazon GuardDuty", "GuardDuty 威脅偵測", "Detect threats across accounts, workloads and data sources.", "偵測跨帳戶、工作負載同資料來源嘅威脅。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.RichManager, "guardduty", "detector", "filter", "ipset", "threatintelset"),
        S("inspector2", "Amazon Inspector", "Inspector 漏洞管理", "Find software vulnerabilities and unintended network exposure.", "搵出軟件漏洞同意外公開嘅網絡存取。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceDiscovery, "inspector2", "filter", "cis-scan-configuration"),
        S("securityhub", "AWS Security Hub", "Security Hub 保安中心", "Aggregate findings and manage security standards and controls.", "集中保安發現，管理保安標準同控制。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.RichManager, "securityhub", "hub", "standard", "automation-rule", "finding-aggregator"),
        S("macie2", "Amazon Macie", "Macie 敏感資料偵測", "Discover and protect sensitive data stored in S3.", "搵出同保護存喺 S3 入面嘅敏感資料。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceDiscovery, "macie", "classification-job", "findings-filter", "custom-data-identifier"),
        S("detective", "Amazon Detective", "Detective 保安調查", "Investigate security findings through linked activity data.", "用關聯活動資料調查保安發現。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceDiscovery, "detective", "graph"),
        S("accessanalyzer", "IAM Access Analyzer", "IAM 存取分析器", "Find external, public and unused access to AWS resources.", "搵出 AWS 資源嘅外部、公開同未使用存取權。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceDiscovery, "access-analyzer", "analyzer", "archive-rule"),
        S("auditmanager", "AWS Audit Manager", "AWS Audit Manager 合規稽核", "Collect evidence and assess controls against compliance frameworks.", "收集合規證據，同框架要求逐項評估控制。", AwsConsoleCategory.SecurityIdentityAndCompliance, AwsAdapterCapabilityLevel.ResourceLifecycle, "auditmanager", "assessment", "framework", "control"),

        // Containers
        S("ecs", "Amazon Elastic Container Service", "ECS 容器服務", "Run and manage container clusters, services and tasks.", "執行同管理容器叢集、服務同工作。", AwsConsoleCategory.Containers, AwsAdapterCapabilityLevel.RichManager, "ecs", "cluster", "service", "task-definition", "task", "capacity-provider"),
        S("ecr", "Amazon Elastic Container Registry", "ECR 容器映像倉庫", "Store, scan, replicate and lifecycle-manage private container images.", "儲存、掃描、複製同管理私人容器映像生命週期。", AwsConsoleCategory.Containers, AwsAdapterCapabilityLevel.RichManager, "ecr", "repository", "pullthroughcacherule", "registryscanningconfiguration"),
        S("ecr-public", "Amazon ECR Public", "ECR 公開映像倉庫", "Publish and manage public container image repositories.", "發佈同管理公開容器映像倉庫。", AwsConsoleCategory.Containers, AwsAdapterCapabilityLevel.ResourceLifecycle, "ecr", "publicrepository"),
        S("eks", "Amazon Elastic Kubernetes Service", "EKS Kubernetes 服務", "Operate Kubernetes clusters, node groups, add-ons and access policies.", "管理 Kubernetes 叢集、節點組、附加元件同存取政策。", AwsConsoleCategory.Containers, AwsAdapterCapabilityLevel.RichManager, "eks", "cluster", "nodegroup", "addon", "fargateprofile", "accessentry"),
        S("apprunner", "AWS App Runner", "App Runner 容器應用程式", "Deploy web services directly from source or container images.", "直接由原始碼或者容器映像部署網站服務。", AwsConsoleCategory.Containers, AwsAdapterCapabilityLevel.ResourceLifecycle, "apprunner", "service", "connection", "observabilityconfiguration"),
        S("redshift-serverless", "Amazon Redshift Serverless", "Redshift 無伺服器數據倉庫", "Run analytics warehouses without managing clusters.", "唔使管理叢集都可以執行數據倉庫分析。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.ResourceLifecycle, "redshift-serverless", "namespace", "workgroup", "snapshot"),

        // Serverless
        S("lambda", "AWS Lambda", "Lambda 無伺服器函數", "Build, deploy, invoke and monitor event-driven functions.", "建立、部署、執行同監察事件驅動函數。", AwsConsoleCategory.Serverless, AwsAdapterCapabilityLevel.RichManager, "lambda", "function", "layerversion", "eventsourcemapping", "codesigningconfig"),
        S("serverlessrepo", "AWS Serverless Application Repository", "無伺服器應用程式倉庫", "Discover, publish and deploy reusable serverless applications.", "搜尋、發佈同部署可以重用嘅無伺服器應用程式。", AwsConsoleCategory.Serverless, AwsAdapterCapabilityLevel.ResourceLifecycle, "serverlessrepo", "application"),
        S("cloudformation", "AWS CloudFormation", "CloudFormation 基礎設施範本", "Model and deploy AWS infrastructure as versionable stacks.", "用可版本管理嘅 stack 範本建立同更新 AWS 基礎設施。", AwsConsoleCategory.Serverless, AwsAdapterCapabilityLevel.RichManager, "cloudformation", "stack", "stackset", "type", "module"),

        // Application integration
        S("sqs", "Amazon Simple Queue Service", "SQS 訊息佇列", "Create queues and exchange durable application messages.", "建立佇列，同應用程式可靠咁交換訊息。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.RichManager, "sqs", "queue"),
        S("sns", "Amazon Simple Notification Service", "SNS 發佈訂閱通知", "Fan out messages through topics and subscriptions.", "經主題同訂閱將訊息廣播去多個目的地。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.RichManager, "sns", "topic"),
        S("events", "Amazon EventBridge", "EventBridge 事件匯流排", "Route events through buses, rules, targets and archives.", "經事件匯流排、規則、目標同封存路由事件。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.RichManager, "events", "event-bus", "rule", "archive", "connection", "api-destination"),
        S("scheduler", "EventBridge Scheduler", "EventBridge 排程器", "Invoke AWS targets at one-time or recurring schedules.", "按一次性或者重複時間表呼叫 AWS 目標。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.ResourceLifecycle, "scheduler", "schedule", "schedule-group"),
        S("stepfunctions", "AWS Step Functions", "Step Functions 工作流程", "Orchestrate distributed applications with visual state machines.", "用可視化狀態機編排分散式應用程式。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.RichManager, "states", "statemachine", "activity"),
        S("mq", "Amazon MQ", "Amazon MQ 訊息代理", "Operate managed ActiveMQ and RabbitMQ brokers.", "管理受管 ActiveMQ 同 RabbitMQ 訊息代理。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.ResourceLifecycle, "mq", "broker", "configuration"),
        S("appflow", "Amazon AppFlow", "AppFlow SaaS 資料整合", "Transfer data between SaaS applications and AWS services.", "喺 SaaS 應用程式同 AWS 服務之間傳輸資料。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.ResourceLifecycle, "appflow", "flow", "connector"),
        S("appsync", "AWS AppSync", "AppSync GraphQL API", "Build managed GraphQL and event APIs with real-time data.", "建立有即時資料嘅受管 GraphQL 同事件 API。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.ResourceLifecycle, "appsync", "graphqlapi", "datasource", "functionconfiguration", "channelnamespace"),
        S("sesv2", "Amazon Simple Email Service", "SES 電郵傳送", "Send email and manage identities, configuration sets and deliverability.", "傳送電郵，同管理寄件身分、設定集同送達狀況。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.RichManager, "ses", "emailidentity", "configurationset", "contactlist", "dedicatedippool"),
        S("connect", "Amazon Connect", "Amazon Connect 聯絡中心", "Operate cloud contact centres, queues, flows and routing profiles.", "管理雲端聯絡中心、佇列、流程同路由設定檔。", AwsConsoleCategory.ApplicationIntegration, AwsAdapterCapabilityLevel.ResourceLifecycle, "connect", "instance", "contactflow", "queue", "routingprofile"),

        // Management and governance
        S("cloudwatch", "Amazon CloudWatch", "CloudWatch 監察", "Explore metrics, alarms, dashboards and observability settings.", "睇指標、警報、儀表板同可觀察性設定。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.RichManager, "cloudwatch", "alarm", "dashboard", "insight-rule", "metric-stream"),
        S("logs", "Amazon CloudWatch Logs", "CloudWatch Logs 記錄", "Search log groups and streams, run queries and manage retention.", "搜尋記錄群組同串流、執行查詢同管理保留期。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.RichManager, "logs", "log-group", "destination", "query-definition", "delivery"),
        S("cloudtrail", "AWS CloudTrail", "CloudTrail 活動稽核", "Audit AWS API activity with trails, event data stores and queries.", "用 trail、事件資料存放區同查詢稽核 AWS API 活動。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.RichManager, "cloudtrail", "trail", "eventdatastore", "channel"),
        S("configservice", "AWS Config", "AWS Config 資源合規", "Record resource configuration and evaluate compliance rules.", "記錄資源設定，同規則逐項評估合規狀態。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.RichManager, "config", "config-rule", "configuration-recorder", "conformance-pack", "aggregation-authorization"),
        S("ssm", "AWS Systems Manager", "Systems Manager 系統管理", "Operate fleets, automation, parameters, patches, sessions and incidents.", "管理機群、自動化、參數、修補、工作階段同事故。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.RichManager, "ssm", "document", "parameter", "maintenancewindow", "patchbaseline", "resourcedatasync"),
        S("resource-explorer-2", "AWS Resource Explorer", "AWS 資源瀏覽器", "Search resource inventory across Regions and accounts.", "跨區域同帳戶搜尋 AWS 資源清單。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.RichManager, "resource-explorer-2", "index", "view"),
        S("resource-groups", "AWS Resource Groups", "AWS 資源群組", "Group resources using tags and Resource Groups queries.", "用標籤同查詢將 AWS 資源分組。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.ResourceLifecycle, "resource-groups", "group"),
        S("resourcegroupstaggingapi", "Resource Groups Tagging API", "AWS 資源標籤管理", "Find resources and apply or remove tags across services.", "跨服務搵資源，同一次過加或者移除標籤。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.ResourceDiscovery, "tagging"),
        S("servicecatalog", "AWS Service Catalog", "AWS Service Catalog", "Govern approved infrastructure products and portfolios.", "管理獲批准嘅基礎設施產品同產品組合。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.ResourceLifecycle, "servicecatalog", "portfolio", "product", "provisionedproduct"),
        S("service-quotas", "AWS Service Quotas", "AWS 服務配額", "Inspect usage limits and request quota increases.", "睇服務用量限制同申請提高配額。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.RichManager, "servicequotas", "service", "quota"),
        S("wellarchitected", "AWS Well-Architected Tool", "AWS Well-Architected 檢視", "Review workloads against AWS architecture best practices.", "按照 AWS 架構最佳做法檢視工作負載。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.ResourceLifecycle, "wellarchitected", "workload", "lens", "profile"),
        S("health", "AWS Health", "AWS Health 服務狀況", "View account-specific service events and affected resources.", "睇同你帳戶有關嘅服務事件同受影響資源。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.ResourceDiscovery, "health", "event"),
        S("support", "AWS Support", "AWS Support 支援個案", "Open and manage support cases and Trusted Advisor checks.", "開同管理支援個案，亦可以睇 Trusted Advisor 檢查。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.ResourceLifecycle, "support", "case"),
        S("controltower", "AWS Control Tower", "AWS Control Tower 多帳戶管治", "Set up and govern a secure multi-account landing zone.", "建立同管治安全嘅多帳戶 landing zone。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.ResourceLifecycle, "controltower", "landingzone", "enabledcontrol"),
        S("ram", "AWS Resource Access Manager", "AWS RAM 資源分享", "Share supported AWS resources between accounts and organizations.", "喺帳戶同組織之間分享支援嘅 AWS 資源。", AwsConsoleCategory.ManagementAndGovernance, AwsAdapterCapabilityLevel.ResourceLifecycle, "ram", "resourceshare"),

        // Analytics
        S("athena", "Amazon Athena", "Athena 無伺服器 SQL 分析", "Query data in S3 and federated sources using SQL.", "用 SQL 查詢 S3 同聯合資料來源。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.RichManager, "athena", "workgroup", "datacatalog", "capacity-reservation"),
        S("glue", "AWS Glue", "AWS Glue 資料整合", "Catalog, prepare and integrate analytics data.", "編目、整理同整合分析資料。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.RichManager, "glue", "database", "table", "crawler", "job", "workflow", "registry"),
        S("emr", "Amazon EMR", "EMR 大數據平台", "Run managed Spark, Hadoop and other big-data frameworks.", "執行受管 Spark、Hadoop 同其他大數據框架。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.ResourceLifecycle, "elasticmapreduce", "cluster", "studio", "editor"),
        S("emr-serverless", "Amazon EMR Serverless", "EMR 無伺服器大數據", "Run Spark and Hive jobs without managing clusters.", "唔使管理叢集都可以執行 Spark 同 Hive 工作。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.ResourceLifecycle, "emr-serverless", "application"),
        S("kinesis", "Amazon Kinesis Data Streams", "Kinesis 即時資料串流", "Ingest and process high-volume real-time data streams.", "擷取同處理大量即時資料串流。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.RichManager, "kinesis", "stream", "streamconsumer"),
        S("firehose", "Amazon Data Firehose", "Data Firehose 串流傳送", "Deliver streaming data into storage and analytics destinations.", "將串流資料送去儲存同分析目的地。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.ResourceLifecycle, "firehose", "deliverystream"),
        S("opensearch", "Amazon OpenSearch Service", "OpenSearch 搜尋同分析", "Operate search and observability domains and serverless collections.", "管理搜尋、可觀察性 domain 同無伺服器 collection。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.RichManager, "opensearch", "domain", "application"),
        S("lakeformation", "AWS Lake Formation", "Lake Formation 數據湖管治", "Build and govern secure analytics data lakes.", "建立同管治安全嘅分析數據湖。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.ResourceLifecycle, "lakeformation", "datalake-settings", "permissions"),
        S("quicksight", "Amazon QuickSight", "QuickSight 商業智能", "Manage dashboards, analyses, datasets and data sources.", "管理儀表板、分析、資料集同資料來源。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.RichManager, "quicksight", "dashboard", "analysis", "dataset", "datasource", "theme"),
        S("kafka", "Amazon Managed Streaming for Apache Kafka", "MSK 受管 Kafka", "Operate managed Apache Kafka and serverless clusters.", "管理 Apache Kafka 同無伺服器串流叢集。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.ResourceLifecycle, "kafka", "cluster", "cluster-v2", "configuration", "replicator"),
        S("redshift", "Amazon Redshift", "Redshift 數據倉庫", "Manage provisioned data warehouse clusters and snapshots.", "管理已配置嘅數據倉庫叢集同快照。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.RichManager, "redshift", "cluster", "cluster-snapshot", "cluster-parameter-group"),
        S("timestream-query", "Amazon Timestream Query", "Timestream 時間序列查詢", "Query time-series data using SQL.", "用 SQL 查詢時間序列資料。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.GenericCommands, "timestream", "database", "table"),
        S("timestream-write", "Amazon Timestream Write", "Timestream 時間序列寫入", "Manage and write time-series databases and tables.", "管理同寫入時間序列資料庫及資料表。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.ResourceLifecycle, "timestream", "database", "table"),
        S("datazone", "Amazon DataZone", "DataZone 資料目錄同管治", "Catalog, discover, share and govern organizational data.", "為機構資料做編目、搜尋、分享同管治。", AwsConsoleCategory.Analytics, AwsAdapterCapabilityLevel.ResourceLifecycle, "datazone", "domain", "project", "environment-profile", "datasource"),

        // Machine learning and AI
        S("sagemaker", "Amazon SageMaker AI", "SageMaker AI 機器學習平台", "Build, train, deploy and govern machine-learning models.", "建立、訓練、部署同管治機器學習模型。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.RichManager, "sagemaker", "endpoint", "model", "notebook-instance", "pipeline", "training-job", "space"),
        S("bedrock", "Amazon Bedrock", "Bedrock 生成式 AI", "Manage foundation models, agents, knowledge bases and guardrails.", "管理基礎模型、agent、知識庫同 guardrail。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.RichManager, "bedrock", "agent", "knowledge-base", "guardrail", "model-customization-job", "provisioned-model"),
        S("bedrock-runtime", "Amazon Bedrock Runtime", "Bedrock 模型推理", "Invoke foundation models and conversational inference APIs.", "呼叫基礎模型同對話推理 API。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.GenericCommands, "bedrock", "foundation-model"),
        S("comprehend", "Amazon Comprehend", "Comprehend 自然語言分析", "Extract entities, sentiment, topics and custom insights from text.", "由文字抽取實體、情緒、主題同自訂分析結果。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.ResourceLifecycle, "comprehend", "document-classifier", "entity-recognizer", "flywheel"),
        S("textract", "Amazon Textract", "Textract 文件資料擷取", "Extract text, forms, tables and identity data from documents.", "由文件抽取文字、表格、欄位同身分資料。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.GenericCommands, "textract", "adapter"),
        S("rekognition", "Amazon Rekognition", "Rekognition 圖像同影片分析", "Analyze images and videos for labels, faces, text and moderation.", "分析圖片同影片嘅標籤、人臉、文字同內容安全。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.ResourceLifecycle, "rekognition", "collection", "streamprocessor", "project"),
        S("transcribe", "Amazon Transcribe", "Transcribe 語音轉文字", "Convert live and recorded speech into text.", "將即時或者錄低嘅語音轉做文字。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.ResourceLifecycle, "transcribe", "transcription-job", "vocabulary", "language-model"),
        S("translate", "Amazon Translate", "Translate 機器翻譯", "Translate text and documents between languages.", "喺唔同語言之間翻譯文字同文件。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.ResourceLifecycle, "translate", "parallel-data", "terminology"),
        S("polly", "Amazon Polly", "Polly 文字轉語音", "Synthesize natural speech and manage pronunciation lexicons.", "將文字轉做自然語音，同管理發音詞典。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.GenericCommands, "polly", "lexicon"),
        S("lexv2-models", "Amazon Lex V2", "Lex V2 對話機械人", "Build conversational bots, intents, slots and locales.", "建立對話 bot、意圖、slot 同語言地區。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.ResourceLifecycle, "lex", "bot", "botalias"),
        S("kendra", "Amazon Kendra", "Kendra 智能搜尋", "Build intelligent enterprise search indexes and data sources.", "建立智能企業搜尋索引同資料來源。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.ResourceLifecycle, "kendra", "index", "datasource", "faq"),
        S("personalize", "Amazon Personalize", "Personalize 個人化推薦", "Train and deploy real-time recommendation systems.", "訓練同部署即時個人化推薦系統。", AwsConsoleCategory.MachineLearningAndAI, AwsAdapterCapabilityLevel.ResourceLifecycle, "personalize", "dataset-group", "solution", "campaign", "recommender"),

        // Developer tools
        S("codeartifact", "AWS CodeArtifact", "CodeArtifact 套件倉庫", "Host and govern private software package repositories.", "託管同管治私人軟件套件倉庫。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.ResourceLifecycle, "codeartifact", "domain", "repository", "package-group"),
        S("codebuild", "AWS CodeBuild", "CodeBuild 建置服務", "Run managed builds, tests and report groups.", "執行受管建置、測試同測試報告。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.RichManager, "codebuild", "project", "report-group", "fleet"),
        S("codecommit", "AWS CodeCommit", "CodeCommit Git 倉庫", "Manage private Git repositories hosted by AWS.", "管理由 AWS 託管嘅私人 Git 倉庫。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.ResourceLifecycle, "codecommit", "repository"),
        S("codedeploy", "AWS CodeDeploy", "CodeDeploy 部署自動化", "Automate deployments to compute services and on-premises hosts.", "自動部署去運算服務同本地主機。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.ResourceLifecycle, "codedeploy", "application", "deploymentgroup"),
        S("codepipeline", "AWS CodePipeline", "CodePipeline CI/CD 流水線", "Orchestrate continuous delivery pipelines and actions.", "編排持續交付流水線同動作。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.RichManager, "codepipeline", "pipeline", "webhook"),
        S("cloud9", "AWS Cloud9", "Cloud9 雲端開發環境", "Manage browser-based cloud development environments.", "管理用瀏覽器開嘅雲端開發環境。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.ResourceLifecycle, "cloud9", "environment"),
        S("devicefarm", "AWS Device Farm", "Device Farm 裝置測試", "Test mobile and web applications on hosted devices and browsers.", "喺託管裝置同瀏覽器測試流動及網站應用程式。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.ResourceLifecycle, "devicefarm", "project", "devicepool", "instanceprofile"),
        S("xray", "AWS X-Ray", "X-Ray 分散式追蹤", "Trace requests and analyze distributed application performance.", "追蹤請求，同分析分散式應用程式效能。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.ResourceDiscovery, "xray", "group", "samplingrule"),
        S("synthetics", "CloudWatch Synthetics", "CloudWatch Synthetics 模擬監察", "Run canaries that test endpoints and user journeys.", "用 canary 定時測試端點同使用者流程。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.ResourceLifecycle, "synthetics", "canary", "group"),
        S("amplify", "AWS Amplify", "Amplify 網頁同流動應用程式", "Host and deploy full-stack web and mobile applications.", "託管同部署全端網站及流動應用程式。", AwsConsoleCategory.DeveloperTools, AwsAdapterCapabilityLevel.ResourceLifecycle, "amplify", "app", "branch", "domain"),

        // Migration and transfer
        S("dms", "AWS Database Migration Service", "DMS 資料庫遷移", "Migrate and replicate databases and analytics stores.", "遷移同複製資料庫及分析資料存放區。", AwsConsoleCategory.MigrationAndTransfer, AwsAdapterCapabilityLevel.RichManager, "dms", "replication-instance", "replication-task", "endpoint", "replication-config"),
        S("datasync", "AWS DataSync", "DataSync 資料傳輸", "Automate high-speed transfers between storage systems and AWS.", "自動高速傳輸本地、其他雲端同 AWS 儲存資料。", AwsConsoleCategory.MigrationAndTransfer, AwsAdapterCapabilityLevel.RichManager, "datasync", "task", "location", "agent"),
        S("mgn", "AWS Application Migration Service", "Application Migration Service", "Lift and shift physical, virtual and cloud servers to AWS.", "將實體、虛擬同其他雲端伺服器搬去 AWS。", AwsConsoleCategory.MigrationAndTransfer, AwsAdapterCapabilityLevel.ResourceLifecycle, "mgn", "source-server", "launch-configuration-template"),
        S("transfer", "AWS Transfer Family", "Transfer Family 受管檔案傳輸", "Operate managed SFTP, FTPS, FTP and AS2 endpoints.", "管理受管 SFTP、FTPS、FTP 同 AS2 端點。", AwsConsoleCategory.MigrationAndTransfer, AwsAdapterCapabilityLevel.ResourceLifecycle, "transfer", "server", "connector", "workflow", "profile"),
        S("snowball", "AWS Snow Family", "AWS Snow 離線資料傳輸", "Move large datasets with AWS edge and offline transfer devices.", "用 AWS 邊緣同離線傳輸裝置搬大量資料。", AwsConsoleCategory.MigrationAndTransfer, AwsAdapterCapabilityLevel.ResourceLifecycle, "snowball", "job", "cluster"),
        S("migrationhub-config", "AWS Migration Hub", "Migration Hub 遷移追蹤", "Track migration home Regions and discovery settings.", "追蹤遷移主區域同探索設定。", AwsConsoleCategory.MigrationAndTransfer, AwsAdapterCapabilityLevel.ResourceDiscovery, "migrationhub", "home-region-control"),
        S("application-discovery", "AWS Application Discovery Service", "Application Discovery Service", "Inventory on-premises servers and application dependencies.", "盤點本地伺服器同應用程式相依關係。", AwsConsoleCategory.MigrationAndTransfer, AwsAdapterCapabilityLevel.ResourceDiscovery, "discovery", "application", "server"),

        // Billing and cost management
        S("ce", "AWS Cost Explorer", "AWS Cost Explorer 成本分析", "Analyze spend, usage, forecasts, anomalies and rightsizing recommendations.", "分析開支、用量、預測、異常同資源調整建議。", AwsConsoleCategory.BillingAndCostManagement, AwsAdapterCapabilityLevel.RichManager, "ce", "anomalymonitor", "anomalysubscription", "costcategory"),
        S("budgets", "AWS Budgets", "AWS Budgets 預算", "Track cost and usage budgets with alerts and automated actions.", "用警報同自動動作追蹤成本及用量預算。", AwsConsoleCategory.BillingAndCostManagement, AwsAdapterCapabilityLevel.RichManager, "budgets", "budget"),
        S("pricing", "AWS Price List", "AWS 價目表", "Query current AWS products, attributes and public prices.", "查詢 AWS 產品、屬性同公開價格。", AwsConsoleCategory.BillingAndCostManagement, AwsAdapterCapabilityLevel.GenericCommands, "pricing"),
        S("cost-optimization-hub", "AWS Cost Optimization Hub", "成本最佳化中心", "Aggregate and prioritize cost-saving recommendations.", "集中同排序節省成本建議。", AwsConsoleCategory.BillingAndCostManagement, AwsAdapterCapabilityLevel.ResourceDiscovery, "cost-optimization-hub"),
        S("bcm-data-exports", "AWS Billing and Cost Management Data Exports", "帳單同成本資料匯出", "Create recurring exports of billing and cost data.", "定時匯出帳單同成本資料。", AwsConsoleCategory.BillingAndCostManagement, AwsAdapterCapabilityLevel.ResourceLifecycle, "bcm-data-exports", "export"),
        S("cur", "AWS Cost and Usage Reports", "AWS 成本同用量報告", "Manage detailed cost and usage report definitions.", "管理詳細成本同用量報告定義。", AwsConsoleCategory.BillingAndCostManagement, AwsAdapterCapabilityLevel.ResourceLifecycle, "cur", "reportdefinition"),
        S("savingsplans", "AWS Savings Plans", "AWS Savings Plans", "Inspect savings plans, utilization, coverage and purchase recommendations.", "睇 Savings Plans、使用率、覆蓋率同購買建議。", AwsConsoleCategory.BillingAndCostManagement, AwsAdapterCapabilityLevel.ResourceDiscovery, "savingsplans", "savingsplan"),
        S("account", "AWS Account", "AWS 帳戶設定", "Inspect and update supported account contact and Region settings.", "睇同更新支援嘅帳戶聯絡資料及區域設定。", AwsConsoleCategory.BillingAndCostManagement, AwsAdapterCapabilityLevel.ResourceLifecycle, "account", "account"),
    ];

    private static AwsConsoleServiceDefinition S(
        string cliId,
        string nameEn,
        string nameZh,
        string descriptionEn,
        string descriptionZh,
        AwsConsoleCategory category,
        AwsAdapterCapabilityLevel adapterCapability,
        string resourceExplorerNamespace,
        params string[] resourceTypeHints) =>
        new(
            cliId,
            nameEn,
            nameZh,
            descriptionEn,
            descriptionZh,
            category,
            CategoryGlyph(category),
            resourceExplorerNamespace,
            resourceTypeHints,
            adapterCapability);

    private static AwsConsoleServiceDefinition CreateLiveFallback(string cliId)
    {
        var friendly = HumanizeCliId(cliId);
        return new AwsConsoleServiceDefinition(
            cliId,
            $"Amazon {friendly}",
            $"AWS {friendly}",
            $"Browse every operation exposed by the installed AWS CLI for {friendly}.",
            $"瀏覽已安裝 AWS CLI 為 {friendly} 提供嘅全部操作同參數。",
            AwsConsoleCategory.Other,
            OtherGlyph,
            InferResourceNamespace(cliId),
            Array.Empty<string>(),
            AwsAdapterCapabilityLevel.GenericCommands,
            IsCurated: false);
    }

    private static string NormalizeCliId(string? rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId)) return string.Empty;
        var id = rawId.Trim();
        if (id.StartsWith("aws ", StringComparison.OrdinalIgnoreCase))
            id = id[4..].Trim();
        var whitespace = id.IndexOfAny([' ', '\t', '\r', '\n']);
        if (whitespace >= 0) id = id[..whitespace];
        return id.Trim().ToLowerInvariant();
    }

    private static string HumanizeCliId(string cliId)
    {
        var words = cliId.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0) return cliId;
        return string.Join(" ", words.Select(HumanizeWord));
    }

    private static string HumanizeWord(string word)
    {
        if (Acronyms.TryGetValue(word, out var acronym)) return acronym;
        if (word.Length == 0) return word;

        var value = char.ToUpper(word[0], CultureInfo.InvariantCulture) + word[1..].ToLowerInvariant();
        if (value.Length > 2 && value.EndsWith("v2", StringComparison.OrdinalIgnoreCase))
            value = value[..^2] + " V2";
        return value;
    }

    private static string InferResourceNamespace(string cliId) => cliId switch
    {
        "bedrock-runtime" => "bedrock",
        "dynamodbstreams" => "dynamodb",
        "ecr-public" => "ecr",
        "lex-runtime" or "lex-models" or "lexv2-runtime" or "lexv2-models" => "lex",
        "rds-data" => "rds",
        "s3api" or "s3control" => "s3",
        "sso-admin" => "sso",
        "timestream-query" or "timestream-write" => "timestream",
        _ => cliId,
    };

    private static int CategoryOrder(AwsConsoleCategory category) =>
        Categories.First(definition => definition.Id == category).SortOrder;

    private static string CategoryGlyph(AwsConsoleCategory category) => category switch
    {
        AwsConsoleCategory.Compute => ComputeGlyph,
        AwsConsoleCategory.Storage => StorageGlyph,
        AwsConsoleCategory.Database => DatabaseGlyph,
        AwsConsoleCategory.NetworkingAndContentDelivery => NetworkGlyph,
        AwsConsoleCategory.SecurityIdentityAndCompliance => SecurityGlyph,
        AwsConsoleCategory.Containers => ContainersGlyph,
        AwsConsoleCategory.Serverless => ServerlessGlyph,
        AwsConsoleCategory.ApplicationIntegration => IntegrationGlyph,
        AwsConsoleCategory.ManagementAndGovernance => ManagementGlyph,
        AwsConsoleCategory.Analytics => AnalyticsGlyph,
        AwsConsoleCategory.MachineLearningAndAI => AiGlyph,
        AwsConsoleCategory.DeveloperTools => DeveloperGlyph,
        AwsConsoleCategory.MigrationAndTransfer => MigrationGlyph,
        AwsConsoleCategory.BillingAndCostManagement => CostGlyph,
        _ => OtherGlyph,
    };

    private static readonly IReadOnlyDictionary<string, string> Acronyms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["acm"] = "ACM",
            ["ai"] = "AI",
            ["api"] = "API",
            ["bcm"] = "BCM",
            ["cdn"] = "CDN",
            ["dms"] = "DMS",
            ["dns"] = "DNS",
            ["ec2"] = "EC2",
            ["ecr"] = "ECR",
            ["ecs"] = "ECS",
            ["efs"] = "EFS",
            ["eks"] = "EKS",
            ["emr"] = "EMR",
            ["fis"] = "FIS",
            ["fsx"] = "FSx",
            ["iam"] = "IAM",
            ["iot"] = "IoT",
            ["ivs"] = "IVS",
            ["kms"] = "KMS",
            ["mq"] = "MQ",
            ["neptune"] = "Neptune",
            ["nimble"] = "Nimble",
            ["odb"] = "ODB",
            ["omics"] = "Omics",
            ["pcs"] = "PCS",
            ["qldb"] = "QLDB",
            ["rds"] = "RDS",
            ["s3"] = "S3",
            ["ses"] = "SES",
            ["sms"] = "SMS",
            ["sns"] = "SNS",
            ["sqs"] = "SQS",
            ["ssm"] = "SSM",
            ["sso"] = "SSO",
            ["sts"] = "STS",
            ["waf"] = "WAF",
        };
}
