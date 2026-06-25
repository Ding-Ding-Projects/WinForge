using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace WinForge.Services;

/// <summary>
/// 應用程式內 Docker Engine 客戶端 · In-app Docker Engine client.
///
/// 呢個係同 qBittorrent 模組一樣嘅「原生客戶端對本機 API」模式：用 MANAGED .NET 程式庫
/// <c>Docker.DotNet</c> 直接打 Docker Engine 嘅 REST API（行 Windows 具名管道
/// npipe://./pipe/docker_engine，或者落 tcp://localhost:2375）。
/// 我哋【唔會】shell out 去 <c>docker</c> / <c>docker compose</c> CLI、唔會啟動 Docker Desktop、
/// 亦【唔會】夾帶任何安裝程式 — 一定要本機有 Docker daemon 行緊先用得。
///
/// This is the SAME "native client over a local API" pattern as the qBittorrent module: it uses the
/// MANAGED .NET library <c>Docker.DotNet</c> to talk to the Docker Engine REST API directly over the
/// Windows named pipe (npipe://./pipe/docker_engine) or, if configured, tcp://localhost:2375.
/// We NEVER shell out to the <c>docker</c>/<c>docker compose</c> CLI, never launch Docker Desktop, and
/// bundle no installer — a running local Docker daemon is required.
/// </summary>
public sealed class DockerService : IDisposable
{
    public const string KeyEndpoint = "docker.endpoint";

    private DockerClient? _client;

    /// <summary>目前使用緊嘅 endpoint URI · The endpoint URI currently in use.</summary>
    public string Endpoint { get; private set; } = DefaultEndpoint;

    /// <summary>連線成功最近一次取得嘅 daemon 版本 · Last daemon version obtained on a successful connect.</summary>
    public string? Version { get; private set; }

    public bool Connected => _client is not null;

    /// <summary>Windows 預設用具名管道 · Default to the Windows named pipe.</summary>
    public static string DefaultEndpoint => "npipe://./pipe/docker_engine";

    public DockerService()
    {
        Endpoint = SettingsStore.Get(KeyEndpoint, DefaultEndpoint);
        if (string.IsNullOrWhiteSpace(Endpoint)) Endpoint = DefaultEndpoint;
    }

    public void SaveEndpoint(string endpoint)
    {
        Endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim();
        SettingsStore.Set(KeyEndpoint, Endpoint);
    }

    /// <summary>
    /// 嘗試連去本機 daemon；成功會記住版本。連唔到就拋例外（畀 UI 顯示 InfoBar）。
    /// Try to connect to the local daemon; stores the version on success. Throws on failure
    /// so the UI can surface the "daemon not reachable" InfoBar.
    /// </summary>
    public async Task<string> ConnectAsync(string? endpoint = null, CancellationToken ct = default)
    {
        var ep = string.IsNullOrWhiteSpace(endpoint) ? Endpoint : endpoint!.Trim();
        Dispose();
        // npipe:// / tcp:// 兩種都係合法 Docker.DotNet endpoint。
        var cfg = new DockerClientConfiguration(new Uri(ep), defaultTimeout: TimeSpan.FromSeconds(20));
        var client = cfg.CreateClient();
        // Ping 係最平嘅連通性測試；version 攞嚟做 header 摘要。
        var ver = await client.System.GetVersionAsync(ct).ConfigureAwait(false);
        _client = client;
        Endpoint = ep;
        Version = ver.Version;
        return ver.Version;
    }

    public void Disconnect() => Dispose();

    public void Dispose()
    {
        try { _client?.Dispose(); } catch { }
        _client = null;
        Version = null;
    }

    private DockerClient C => _client ?? throw new InvalidOperationException("Docker not connected · Docker 未連線");

    // ── System / summary ────────────────────────────────────────────────────────

    public async Task<VersionResponse> GetVersionAsync(CancellationToken ct = default)
        => await C.System.GetVersionAsync(ct).ConfigureAwait(false);

    public async Task<SystemInfoResponse> GetInfoAsync(CancellationToken ct = default)
        => await C.System.GetSystemInfoAsync(ct).ConfigureAwait(false);

    // ── Containers ───────────────────────────────────────────────────────────────

    public async Task<IList<ContainerListResponse>> ListContainersAsync(bool all = true, CancellationToken ct = default)
        => await C.Containers.ListContainersAsync(new ContainersListParameters { All = all }, ct).ConfigureAwait(false);

    public Task StartContainerAsync(string id, CancellationToken ct = default)
        => C.Containers.StartContainerAsync(id, new ContainerStartParameters(), ct);

    public Task StopContainerAsync(string id, CancellationToken ct = default)
        => C.Containers.StopContainerAsync(id, new ContainerStopParameters { WaitBeforeKillSeconds = 10 }, ct);

    public Task RestartContainerAsync(string id, CancellationToken ct = default)
        => C.Containers.RestartContainerAsync(id, new ContainerRestartParameters { WaitBeforeKillSeconds = 10 }, ct);

    public Task PauseContainerAsync(string id, CancellationToken ct = default)
        => C.Containers.PauseContainerAsync(id, ct);

    public Task UnpauseContainerAsync(string id, CancellationToken ct = default)
        => C.Containers.UnpauseContainerAsync(id, ct);

    public Task RemoveContainerAsync(string id, bool force, CancellationToken ct = default)
        => C.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = force, RemoveVolumes = false }, ct);

    public async Task<ContainerInspectResponse> InspectContainerAsync(string id, CancellationToken ct = default)
        => await C.Containers.InspectContainerAsync(id, ct).ConfigureAwait(false);

    /// <summary>攞容器 log 尾段（stdout + stderr 合併）· Fetch the tail of a container's logs (stdout+stderr).</summary>
    public async Task<string> GetLogsAsync(string id, int tail = 400, CancellationToken ct = default)
    {
        var p = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Timestamps = false,
            Tail = tail.ToString(),
        };
        // 用 multiplexed 版本，TTY=false 嘅容器先解得正確 frame；多數情況都安全。
        using var stream = await C.Containers.GetContainerLogsAsync(id, false, p, ct).ConfigureAwait(false);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(stdout)) sb.Append(stdout);
        if (!string.IsNullOrEmpty(stderr)) sb.Append(stderr);
        return sb.ToString();
    }

    /// <summary>
    /// 喺執行中嘅容器 exec 一條指令並收齊輸出 · Exec a command in a running container and collect output.
    /// </summary>
    public async Task<string> ExecAsync(string id, string command, CancellationToken ct = default)
    {
        // 用 sh -c 包住，等用戶可以打成個指令行（管道、引號等照用）。
        var exec = await C.Exec.ExecCreateContainerAsync(id, new ContainerExecCreateParameters
        {
            AttachStdout = true,
            AttachStderr = true,
            Tty = false,
            Cmd = new List<string> { "/bin/sh", "-c", command },
        }, ct).ConfigureAwait(false);

        using var stream = await C.Exec.StartAndAttachContainerExecAsync(exec.ID, false, ct).ConfigureAwait(false);
        var (stdout, stderr) = await stream.ReadOutputToEndAsync(ct).ConfigureAwait(false);
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(stdout)) sb.Append(stdout);
        if (!string.IsNullOrEmpty(stderr)) { if (sb.Length > 0) sb.AppendLine(); sb.Append(stderr); }
        return sb.ToString();
    }

    /// <summary>
    /// 攞容器一次性嘅統計（CPU% / 記憶體）· Read a single stats sample for a container (CPU% / memory).
    /// </summary>
    public async Task<DockerStatsSample?> GetStatsOnceAsync(string id, CancellationToken ct = default)
    {
        DockerStatsSample? result = null;
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new Progress<ContainerStatsResponse>(s =>
        {
            if (result is not null) return;
            result = Compute(s);
            done.TrySetResult(true);
        });
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            // stream:false → daemon 出一條 sample 就完。
            var task = C.Containers.GetContainerStatsAsync(id, new ContainerStatsParameters { Stream = false }, progress, linked.Token);
            var finished = await Task.WhenAny(done.Task, task, Task.Delay(5000, linked.Token)).ConfigureAwait(false);
            linked.Cancel();
            try { await task.ConfigureAwait(false); } catch { }
        }
        catch { }
        return result;
    }

    private static DockerStatsSample Compute(ContainerStatsResponse s)
    {
        double cpuPct = 0;
        try
        {
            // Linux 容器嘅標準 CPU% 公式。
            double cpuDelta = s.CPUStats.CPUUsage.TotalUsage - s.PreCPUStats.CPUUsage.TotalUsage;
            double sysDelta = s.CPUStats.SystemUsage - s.PreCPUStats.SystemUsage;
            ulong cpus = s.CPUStats.OnlineCPUs;
            if (cpus == 0 && s.CPUStats.CPUUsage.PercpuUsage is { Count: > 0 } pc) cpus = (ulong)pc.Count;
            if (cpus == 0) cpus = 1;
            if (sysDelta > 0 && cpuDelta > 0) cpuPct = (cpuDelta / sysDelta) * cpus * 100.0;
        }
        catch { }
        ulong memUsage = 0, memLimit = 0;
        try
        {
            memUsage = s.MemoryStats.Usage;
            // 減去 cache 會更貼近 `docker stats`；冇就直接用 Usage。
            if (s.MemoryStats.Stats is not null && s.MemoryStats.Stats.TryGetValue("cache", out var cache) && cache <= memUsage)
                memUsage -= cache;
            memLimit = s.MemoryStats.Limit;
        }
        catch { }
        return new DockerStatsSample(Math.Round(cpuPct, 2), memUsage, memLimit);
    }

    // ── Images ───────────────────────────────────────────────────────────────────

    public async Task<IList<ImagesListResponse>> ListImagesAsync(CancellationToken ct = default)
        => await C.Images.ListImagesAsync(new ImagesListParameters { All = false }, ct).ConfigureAwait(false);

    /// <summary>
    /// 由名拉取映像，progress 回呼俾 UI 顯示進度 · Pull an image by name, reporting progress to the UI.
    /// </summary>
    public async Task PullImageAsync(string image, IProgress<string>? progress, CancellationToken ct = default)
    {
        string repo = image.Trim();
        string tag = "latest";
        // 處理 "repo:tag"（但小心 registry:port/repo 入面嘅冒號）。
        int lastSlash = repo.LastIndexOf('/');
        int lastColon = repo.LastIndexOf(':');
        if (lastColon > lastSlash && lastColon >= 0)
        {
            tag = repo[(lastColon + 1)..];
            repo = repo[..lastColon];
        }
        var prog = new Progress<JSONMessage>(m =>
        {
            var line = m.ProgressMessage ?? m.Status ?? "";
            if (!string.IsNullOrWhiteSpace(line)) progress?.Report($"{m.Status} {m.Progress?.ToString() ?? ""}".Trim());
            if (!string.IsNullOrWhiteSpace(m.ErrorMessage)) progress?.Report("ERROR: " + m.ErrorMessage);
        });
        await C.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = repo, Tag = tag },
            null, prog, ct).ConfigureAwait(false);
    }

    public Task RemoveImageAsync(string id, bool force, CancellationToken ct = default)
        => C.Images.DeleteImageAsync(id, new ImageDeleteParameters { Force = force, NoPrune = false }, ct);

    /// <summary>清走 dangling 映像 · Prune dangling images.</summary>
    public async Task<ulong> PruneImagesAsync(CancellationToken ct = default)
    {
        var r = await C.Images.PruneImagesAsync(new ImagesPruneParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["dangling"] = new Dictionary<string, bool> { ["true"] = true },
            },
        }, ct).ConfigureAwait(false);
        return r.SpaceReclaimed;
    }

    // ── Volumes ────────────────────────────────────────────────────────────────────

    public async Task<IList<VolumeResponse>> ListVolumesAsync(CancellationToken ct = default)
    {
        var r = await C.Volumes.ListAsync(ct).ConfigureAwait(false);
        return r.Volumes ?? new List<VolumeResponse>();
    }

    public Task CreateVolumeAsync(string name, CancellationToken ct = default)
        => C.Volumes.CreateAsync(new VolumesCreateParameters { Name = name }, ct);

    public Task RemoveVolumeAsync(string name, bool force, CancellationToken ct = default)
        => C.Volumes.RemoveAsync(name, force, ct);

    public async Task<ulong> PruneVolumesAsync(CancellationToken ct = default)
    {
        var r = await C.Volumes.PruneAsync(null, ct).ConfigureAwait(false);
        return r.SpaceReclaimed;
    }

    // ── Networks ────────────────────────────────────────────────────────────────────

    public async Task<IList<NetworkResponse>> ListNetworksAsync(CancellationToken ct = default)
        => await C.Networks.ListNetworksAsync(new NetworksListParameters(), ct).ConfigureAwait(false);

    public async Task<string> CreateNetworkAsync(string name, string driver, CancellationToken ct = default)
    {
        var r = await C.Networks.CreateNetworkAsync(new NetworksCreateParameters
        {
            Name = name,
            Driver = string.IsNullOrWhiteSpace(driver) ? "bridge" : driver,
        }, ct).ConfigureAwait(false);
        return r.ID;
    }

    public Task RemoveNetworkAsync(string id, CancellationToken ct = default)
        => C.Networks.DeleteNetworkAsync(id, ct);

    public async Task<ulong> PruneNetworksAsync(CancellationToken ct = default)
    {
        var r = await C.Networks.PruneNetworksAsync(new NetworksDeleteUnusedParameters(), ct).ConfigureAwait(false);
        return (ulong)(r.NetworksDeleted?.Count ?? 0);
    }

    // ── Compose (managed YAML → Docker.DotNet API; NO `docker compose` CLI) ──────────

    /// <summary>
    /// 起一個 compose stack：建立網路、拉映像、按 depends_on 次序建立並啟動每個服務容器。
    /// Bring up a compose stack: create the stack network, pull images, then create &amp; start each
    /// service container in depends_on order — all via the Docker.DotNet API, no CLI.
    /// </summary>
    public async Task ComposeUpAsync(ComposeProject project, IProgress<string>? log, CancellationToken ct = default)
    {
        string netName = project.Name + "_default";
        // 確保有一個 stack 網路。
        var nets = await ListNetworksAsync(ct).ConfigureAwait(false);
        if (!nets.Any(n => n.Name == netName))
        {
            log?.Report($"Creating network {netName} · 建立網路 {netName}");
            await CreateNetworkAsync(netName, "bridge", ct).ConfigureAwait(false);
        }

        foreach (var svc in project.OrderedServices())
        {
            ct.ThrowIfCancellationRequested();
            string containerName = $"{project.Name}_{svc.Name}_1";

            // 拉映像（如本機已有 daemon 會略過層）。
            log?.Report($"Pulling {svc.Image} · 拉取 {svc.Image}");
            try { await PullImageAsync(svc.Image, null, ct).ConfigureAwait(false); }
            catch (Exception ex) { log?.Report($"  pull warning: {ex.Message}"); }

            // 移除同名舊容器，等可以重入。
            try
            {
                var existing = (await ListContainersAsync(true, ct).ConfigureAwait(false))
                    .FirstOrDefault(c => c.Names.Any(n => n.TrimStart('/') == containerName));
                if (existing is not null)
                {
                    log?.Report($"Removing old container {containerName} · 移除舊容器");
                    await RemoveContainerAsync(existing.ID, true, ct).ConfigureAwait(false);
                }
            }
            catch { }

            var create = new CreateContainerParameters
            {
                Name = containerName,
                Image = svc.Image,
                Env = svc.Env.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
                Labels = new Dictionary<string, string>
                {
                    ["com.docker.compose.project"] = project.Name,
                    ["com.docker.compose.service"] = svc.Name,
                    ["winforge.compose"] = "1",
                },
                HostConfig = new HostConfig
                {
                    NetworkMode = netName,
                    PortBindings = new Dictionary<string, IList<PortBinding>>(),
                    Binds = new List<string>(),
                    RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.No },
                },
                ExposedPorts = new Dictionary<string, EmptyStruct>(),
            };

            // Ports: "hostPort:containerPort" 或 "containerPort"。
            foreach (var p in svc.Ports)
            {
                var parts = p.Split(':');
                string containerPort = parts[^1];
                string hostPort = parts.Length >= 2 ? parts[^2] : "";
                if (!containerPort.Contains('/')) containerPort += "/tcp";
                create.ExposedPorts[containerPort] = default;
                create.HostConfig.PortBindings[containerPort] = new List<PortBinding>
                {
                    new PortBinding { HostPort = hostPort },
                };
            }

            // Volumes: "host:container" 或 "namedVolume:container"。
            foreach (var v in svc.Volumes)
                create.HostConfig.Binds.Add(v);

            log?.Report($"Creating {containerName} · 建立 {containerName}");
            var created = await C.Containers.CreateContainerAsync(create, ct).ConfigureAwait(false);
            log?.Report($"Starting {containerName} · 啟動 {containerName}");
            await StartContainerAsync(created.ID, ct).ConfigureAwait(false);
        }
        log?.Report("Stack is up · 堆疊已啟動");
    }

    /// <summary>
    /// 停低同移除一個 compose stack 嘅所有容器（按 label 找）· Stop &amp; remove all containers belonging
    /// to a compose project (found by label).
    /// </summary>
    public async Task ComposeDownAsync(string projectName, IProgress<string>? log, CancellationToken ct = default)
    {
        var all = await ListContainersAsync(true, ct).ConfigureAwait(false);
        var mine = all.Where(c => c.Labels is not null &&
            c.Labels.TryGetValue("com.docker.compose.project", out var pn) && pn == projectName).ToList();
        if (mine.Count == 0)
        {
            // Fallback: 按命名前綴。
            mine = all.Where(c => c.Names.Any(n => n.TrimStart('/').StartsWith(projectName + "_"))).ToList();
        }
        foreach (var c in mine)
        {
            ct.ThrowIfCancellationRequested();
            var name = c.Names.FirstOrDefault()?.TrimStart('/') ?? c.ID[..12];
            log?.Report($"Removing {name} · 移除 {name}");
            try { await RemoveContainerAsync(c.ID, true, ct).ConfigureAwait(false); }
            catch (Exception ex) { log?.Report($"  {ex.Message}"); }
        }
        // 嘗試刪走 stack 網路。
        try
        {
            var nets = await ListNetworksAsync(ct).ConfigureAwait(false);
            var net = nets.FirstOrDefault(n => n.Name == projectName + "_default");
            if (net is not null) { await RemoveNetworkAsync(net.ID, ct).ConfigureAwait(false); log?.Report($"Removed network {net.Name}"); }
        }
        catch { }
        log?.Report("Stack is down · 堆疊已停止");
    }

    // ── Formatting helpers (UI-facing) ─────────────────────────────────────────────

    public static string HumanSize(long bytes) => HumanSize((ulong)Math.Max(0, bytes));

    public static string HumanSize(ulong bytes)
    {
        double b = bytes;
        string[] u = { "B", "KB", "MB", "GB", "TB", "PB" };
        int i = 0;
        while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
        return i == 0 ? $"{bytes} {u[i]}" : $"{b:0.##} {u[i]}";
    }

    public static string ShortId(string id)
    {
        if (string.IsNullOrEmpty(id)) return "";
        var s = id;
        int at = s.IndexOf(':');
        if (at >= 0) s = s[(at + 1)..];
        return s.Length > 12 ? s[..12] : s;
    }

    /// <summary>把 ContainerListResponse 嘅 ports 變成可讀字串 · Render container ports compactly.</summary>
    public static string FormatPorts(IList<Port>? ports)
    {
        if (ports is null || ports.Count == 0) return "";
        var parts = ports.Select(p =>
            p.PublicPort > 0
                ? $"{(string.IsNullOrEmpty(p.IP) ? "" : p.IP + ":")}{p.PublicPort}→{p.PrivatePort}/{p.Type}"
                : $"{p.PrivatePort}/{p.Type}");
        return string.Join(", ", parts.Distinct());
    }
}

/// <summary>一次容器統計取樣 · One container stats sample (CPU% and memory).</summary>
public sealed record DockerStatsSample(double CpuPercent, ulong MemUsage, ulong MemLimit)
{
    public double MemPercent => MemLimit > 0 ? Math.Round((double)MemUsage / MemLimit * 100, 1) : 0;
}
