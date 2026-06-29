using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 自寄存 Vaultwarden 容器生命週期（建立／啟動／停止／移除）· Self-hosted Vaultwarden container lifecycle.
///
/// 全部透過 managed <see cref="DockerService"/>（Docker.DotNet REST API）做 —— 唔會 shell out 去
/// <c>docker</c>／<c>docker compose</c> CLI。每個實例 = 一個 compose 專案名 + 一個具名資料卷（掛 /data）
/// + 一個自動揀嘅空閒主機埠，所以可以同時行任意多個而唔會撞。
///
/// All lifecycle is driven through the managed <see cref="DockerService"/> (Docker.DotNet over the Engine
/// REST API) — never the CLI. Each instance is its own compose project name + a named volume bind-mounted at
/// <c>/data</c> + an auto-picked free host port, so any number can run side by side without collision.
///
/// 安全 · Security: <c>ADMIN_TOKEN</c> 係機密，用 DPAPI 包住先存（從不寫明文設定／日誌）。
/// The Vaultwarden <c>ADMIN_TOKEN</c> is a secret — it is DPAPI-wrapped at rest, never written as plaintext.
/// </summary>
public sealed class BitwardenInstanceService
{
    public const string Image = "vaultwarden/server:latest";

    private const string KeyInstances = "bw.vw.instances"; // JSON list (no secrets)
    private const string KeyTokenPrefix = "bw.vw.token.";   // + projectName → DPAPI(base64) of ADMIN_TOKEN

    private readonly DockerService _docker;

    public BitwardenInstanceService(DockerService docker) => _docker = docker;

    /// <summary>一個自寄存 Vaultwarden 實例嘅持久化中繼資料（無機密）· Persisted metadata for one instance (no secrets).</summary>
    public sealed class Instance
    {
        public string ProjectName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Volume { get; set; } = "";
        public int HostPort { get; set; }
        public bool SignupsAllowed { get; set; } = true;
        public bool WebsocketEnabled { get; set; }
        public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;

        public string LocalUrl => $"http://localhost:{HostPort}";
    }

    /// <summary>實例的執行狀態（由 Docker 列舉得出）· Runtime state derived from a Docker container listing.</summary>
    public enum RunState { NotCreated, Stopped, Running, Unknown }

    // ===================== 中繼資料持久化 · Metadata persistence =====================

    public List<Instance> LoadInstances()
    {
        try
        {
            var json = SettingsStore.Get(KeyInstances, "");
            if (string.IsNullOrWhiteSpace(json)) return new List<Instance>();
            return JsonSerializer.Deserialize<List<Instance>>(json) ?? new List<Instance>();
        }
        catch (Exception ex) { CrashLogger.Log("BitwardenInstanceService.LoadInstances", ex); return new List<Instance>(); }
    }

    private void SaveInstances(List<Instance> list)
    {
        try { SettingsStore.Set(KeyInstances, JsonSerializer.Serialize(list)); }
        catch (Exception ex) { CrashLogger.Log("BitwardenInstanceService.SaveInstances", ex); }
    }

    /// <summary>讀取某實例嘅 ADMIN_TOKEN（DPAPI 解包；無就 null）· Read the DPAPI-wrapped ADMIN_TOKEN, or null.</summary>
    public string? GetAdminToken(string projectName)
    {
        try
        {
            var b64 = SettingsStore.Get(KeyTokenPrefix + projectName, "");
            if (string.IsNullOrEmpty(b64)) return null;
            var raw = ProtectedData.Unprotect(Convert.FromBase64String(b64), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(raw);
        }
        catch (Exception ex) { CrashLogger.Log("BitwardenInstanceService.GetAdminToken", ex); return null; }
    }

    private void SaveAdminToken(string projectName, string token)
    {
        try
        {
            var prot = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
            SettingsStore.Set(KeyTokenPrefix + projectName, Convert.ToBase64String(prot));
        }
        catch (Exception ex) { CrashLogger.Log("BitwardenInstanceService.SaveAdminToken", ex); }
    }

    private void DeleteAdminToken(string projectName)
    {
        try { SettingsStore.Set(KeyTokenPrefix + projectName, ""); } catch { }
    }

    // ===================== 揀埠 / 產生權杖 · Port picking & token generation =====================

    /// <summary>揀一個空閒嘅 TCP 主機埠（避開已用嘅實例埠）· Pick a free TCP host port, avoiding known instance ports.</summary>
    public int PickFreePort(IEnumerable<int>? avoid = null)
    {
        var taken = new HashSet<int>(avoid ?? Enumerable.Empty<int>());
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int port;
            try
            {
                var l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
            }
            catch { port = 11000 + RandomNumberGenerator.GetInt32(40000); }
            if (port >= 1024 && taken.Add(port)) return port;
        }
        return 8443; // last-ditch fallback
    }

    /// <summary>產生一個強 ADMIN_TOKEN（base64url, 32 bytes）· Generate a strong random ADMIN_TOKEN.</summary>
    public static string GenerateAdminToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    // ===================== 建立 / 生命週期 · Create & lifecycle =====================

    public sealed record CreateRequest(string DisplayName, bool SignupsAllowed, bool WebsocketEnabled);

    public sealed record CreateOutcome(bool Success, Instance? Instance, string? AdminToken, LocalizedText? Message);

    /// <summary>
    /// 建立並啟動一個新嘅 Vaultwarden 實例 · Create and start a new Vaultwarden instance: picks a free port,
    /// makes a named volume, generates an ADMIN_TOKEN, then ComposeUpAsync. ADMIN_TOKEN 回傳一次畀用戶複製。
    /// </summary>
    public async Task<CreateOutcome> CreateAsync(CreateRequest req, IProgress<string>? log, CancellationToken ct = default)
    {
        try
        {
            var existing = LoadInstances();
            int port = PickFreePort(existing.Select(i => i.HostPort));
            string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            string project = "winforge_vaultwarden_" + suffix;
            string volume = project + "_data";
            string adminToken = GenerateAdminToken();

            var inst = new Instance
            {
                ProjectName = project,
                DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? $"Vaultwarden :{port}" : req.DisplayName.Trim(),
                Volume = volume,
                HostPort = port,
                SignupsAllowed = req.SignupsAllowed,
                WebsocketEnabled = req.WebsocketEnabled,
                Created = DateTimeOffset.UtcNow,
            };

            log?.Report($"Creating volume {volume} · 建立資料卷 {volume}");
            try { await _docker.CreateVolumeAsync(volume, ct).ConfigureAwait(false); }
            catch (Exception ex) { log?.Report("  volume warning: " + ex.Message); }

            log?.Report($"Pulling {Image} · 拉取 {Image}");
            try { await _docker.PullImageAsync(Image, new Progress<string>(s => log?.Report("  " + s)), ct).ConfigureAwait(false); }
            catch (Exception ex) { log?.Report("  pull warning: " + ex.Message); }

            var compose = BuildProject(inst, adminToken);
            await _docker.ComposeUpAsync(compose, log, ct).ConfigureAwait(false);

            existing.Add(inst);
            SaveInstances(existing);
            SaveAdminToken(project, adminToken);

            log?.Report($"Ready at {inst.LocalUrl} · 已就緒：{inst.LocalUrl}");
            return new CreateOutcome(true, inst, adminToken,
                new($"Vaultwarden is running at {inst.LocalUrl}.", $"Vaultwarden 已喺 {inst.LocalUrl} 行緊。"));
        }
        catch (OperationCanceledException)
        {
            return new CreateOutcome(false, null, null, new("Creation cancelled.", "已取消建立。"));
        }
        catch (Exception ex)
        {
            CrashLogger.Log("BitwardenInstanceService.CreateAsync", ex);
            return new CreateOutcome(false, null, null,
                new("Could not start the Vaultwarden container: " + ex.Message,
                    "啟動 Vaultwarden 容器失敗：" + ex.Message));
        }
    }

    private ComposeProject BuildProject(Instance inst, string adminToken)
    {
        var project = new ComposeProject { Name = inst.ProjectName };
        var svc = new ComposeService { Name = "server", Image = Image };
        svc.Ports.Add($"{inst.HostPort}:80");
        svc.Volumes.Add($"{inst.Volume}:/data");
        svc.Env["DOMAIN"] = inst.LocalUrl;
        svc.Env["ADMIN_TOKEN"] = adminToken;
        svc.Env["SIGNUPS_ALLOWED"] = inst.SignupsAllowed ? "true" : "false";
        svc.Env["WEBSOCKET_ENABLED"] = inst.WebsocketEnabled ? "true" : "false";
        svc.Env["ROCKET_PORT"] = "80";
        project.Services.Add(svc);
        return project;
    }

    /// <summary>讀取某實例嘅執行狀態 · Read the runtime state of an instance via the Docker listing.</summary>
    public async Task<RunState> GetStateAsync(Instance inst, CancellationToken ct = default)
    {
        try
        {
            var all = await _docker.ListContainersAsync(true, ct).ConfigureAwait(false);
            var c = all.FirstOrDefault(x => x.Labels is not null &&
                x.Labels.TryGetValue("com.docker.compose.project", out var pn) && pn == inst.ProjectName);
            if (c is null) return RunState.NotCreated;
            return string.Equals(c.State, "running", StringComparison.OrdinalIgnoreCase) ? RunState.Running : RunState.Stopped;
        }
        catch (Exception ex) { CrashLogger.Log("BitwardenInstanceService.GetStateAsync", ex); return RunState.Unknown; }
    }

    private async Task<string?> FindContainerIdAsync(Instance inst, CancellationToken ct)
    {
        var all = await _docker.ListContainersAsync(true, ct).ConfigureAwait(false);
        var c = all.FirstOrDefault(x => x.Labels is not null &&
            x.Labels.TryGetValue("com.docker.compose.project", out var pn) && pn == inst.ProjectName);
        return c?.ID;
    }

    public async Task<TweakResult> StartAsync(Instance inst, IProgress<string>? log, CancellationToken ct = default)
    {
        try
        {
            var id = await FindContainerIdAsync(inst, ct).ConfigureAwait(false);
            if (id is null)
            {
                // Container gone — recreate from the saved token.
                var token = GetAdminToken(inst.ProjectName) ?? GenerateAdminToken();
                SaveAdminToken(inst.ProjectName, token);
                await _docker.ComposeUpAsync(BuildProject(inst, token), log, ct).ConfigureAwait(false);
                return TweakResult.Ok("Instance started.", "實例已啟動。");
            }
            await _docker.StartContainerAsync(id, ct).ConfigureAwait(false);
            return TweakResult.Ok("Instance started.", "實例已啟動。");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("BitwardenInstanceService.StartAsync", ex);
            return TweakResult.Fail("Could not start: " + ex.Message, "啟動失敗：" + ex.Message);
        }
    }

    public async Task<TweakResult> StopAsync(Instance inst, CancellationToken ct = default)
    {
        try
        {
            var id = await FindContainerIdAsync(inst, ct).ConfigureAwait(false);
            if (id is null) return TweakResult.Ok("Already stopped.", "已停止。");
            await _docker.StopContainerAsync(id, ct).ConfigureAwait(false);
            return TweakResult.Ok("Instance stopped.", "實例已停止。");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("BitwardenInstanceService.StopAsync", ex);
            return TweakResult.Fail("Could not stop: " + ex.Message, "停止失敗：" + ex.Message);
        }
    }

    /// <summary>移除實例（容器＋網路；選擇性刪資料卷）· Remove the instance (containers + network; optionally the data volume).</summary>
    public async Task<TweakResult> RemoveAsync(Instance inst, bool deleteData, IProgress<string>? log, CancellationToken ct = default)
    {
        try
        {
            await _docker.ComposeDownAsync(inst.ProjectName, log, ct).ConfigureAwait(false);
            if (deleteData)
            {
                log?.Report($"Removing volume {inst.Volume} · 移除資料卷 {inst.Volume}");
                try { await _docker.RemoveVolumeAsync(inst.Volume, true, ct).ConfigureAwait(false); }
                catch (Exception ex) { log?.Report("  volume warning: " + ex.Message); }
            }

            var list = LoadInstances();
            list.RemoveAll(i => i.ProjectName == inst.ProjectName);
            SaveInstances(list);
            DeleteAdminToken(inst.ProjectName);

            return TweakResult.Ok("Instance removed.", "實例已移除。");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("BitwardenInstanceService.RemoveAsync", ex);
            return TweakResult.Fail("Could not remove: " + ex.Message, "移除失敗：" + ex.Message);
        }
    }

    public void UpdateInstance(Instance inst)
    {
        var list = LoadInstances();
        var idx = list.FindIndex(i => i.ProjectName == inst.ProjectName);
        if (idx >= 0) { list[idx] = inst; SaveInstances(list); }
    }
}
