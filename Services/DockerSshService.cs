using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 遠端 Docker 容器一行 · One remote Docker container row, parsed from `docker ps -a`.
/// </summary>
public sealed class DockerContainer
{
    public string Id { get; init; } = string.Empty;       // short id
    public string Name { get; init; } = string.Empty;
    public string Image { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;   // e.g. "Up 3 hours" / "Exited (0) 2 days ago"
    public string State { get; init; } = string.Empty;    // running / exited / paused / created …
    public string Ports { get; init; } = string.Empty;

    /// <summary>State 係咪「執行中」· Whether the container is currently running.</summary>
    public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);

    /// <summary>State 係咪「暫停」· Whether the container is currently paused.</summary>
    public bool IsPaused => State.Equals("paused", StringComparison.OrdinalIgnoreCase);
}

/// <summary>遠端 Docker 主機摘要 · A header summary from `docker version` / `docker info`.</summary>
public sealed class DockerHostInfo
{
    public bool DockerPresent { get; init; }
    public string ServerVersion { get; init; } = string.Empty;
    public int Containers { get; init; }
    public int Running { get; init; }
    public string OsArch { get; init; } = string.Empty;
    public string RawError { get; init; } = string.Empty;
}

/// <summary>
/// 透過 SSH 遠端控制 Docker · Remote Docker control over SSH.
/// 用 SSH.NET 連去遠端主機，喺嗰度跑 docker CLI 指令，唔需要本機安裝 docker。
/// Uses SSH.NET to connect to a remote host and run the docker CLI there; nothing is run locally.
/// 重用現有嘅 <see cref="SshProfileStore"/> / <see cref="SshService"/>（包括 DPAPI 秘密、TOFU 主機金鑰）。
/// Reuses the existing SSH profile store / SshService (DPAPI secrets, TOFU host-key check).
/// 所有方法都喺背景執行緒跑（透過 Task.Run），絕不阻塞 UI 執行緒。
/// All methods run off the UI thread (via Task.Run) and never block the UI.
/// </summary>
public sealed class DockerSshService
{
    // 用嚟分隔 docker ps 欄位嘅穩定符號（容器名／鏡像名都唔會含到呢個）。
    // A stable delimiter for `docker ps` columns (unlikely to appear in any field).
    private const string Delim = ""; // ASCII unit separator

    /// <summary>
    /// 跑一句遠端 docker 指令 · Run one remote command, returning (exit code, stdout, stderr).
    /// 每次都開一條新嘅 SSH 連線、跑完即斷，簡單又穩陣。
    /// Opens a fresh SSH connection per call, runs, then disconnects — simple and robust.
    /// </summary>
    public Task<(int code, string stdout, string stderr)> RunAsync(
        SshProfile profile, string secret, string command, CancellationToken ct = default)
        => Task.Run(() =>
        {
            using var client = SshService.Connect(profile, secret);
            using var cmd = client.CreateCommand(command);
            cmd.CommandTimeout = TimeSpan.FromSeconds(60);
            var stdout = cmd.Execute() ?? string.Empty;
            var stderr = cmd.Error ?? string.Empty;
            int code = cmd.ExitStatus ?? 0;
            client.Disconnect();
            return (code, stdout, stderr);
        }, ct);

    /// <summary>
    /// 檢查遠端有冇 docker，順手攞版本／容器數做 header 摘要。
    /// Probe whether docker exists on the remote and gather a small header summary.
    /// </summary>
    public async Task<DockerHostInfo> ProbeAsync(SshProfile profile, string secret, CancellationToken ct = default)
    {
        try
        {
            // -f templates avoid needing jq; `docker info` gives counts; `docker version` the server version.
            const string cmd =
                "docker version --format '{{.Server.Version}}|{{.Server.Os}}/{{.Server.Arch}}' 2>/dev/null; " +
                "echo '###'; " +
                "docker info --format '{{.Containers}}|{{.ContainersRunning}}' 2>/dev/null; " +
                "echo '###'; command -v docker >/dev/null 2>&1 && echo HAVE || echo NONE";
            var (_, stdout, stderr) = await RunAsync(profile, secret, cmd, ct);
            var parts = stdout.Split("###", StringSplitOptions.None);
            string verLine = parts.Length > 0 ? parts[0].Trim() : string.Empty;
            string infoLine = parts.Length > 1 ? parts[1].Trim() : string.Empty;
            string have = parts.Length > 2 ? parts[2].Trim() : string.Empty;

            bool present = have.Contains("HAVE", StringComparison.Ordinal);
            if (!present && string.IsNullOrEmpty(verLine))
                return new DockerHostInfo { DockerPresent = false, RawError = stderr.Trim() };

            string ver = string.Empty, osArch = string.Empty;
            var vbits = verLine.Split('|');
            if (vbits.Length >= 1) ver = vbits[0].Trim();
            if (vbits.Length >= 2) osArch = vbits[1].Trim();

            int containers = 0, running = 0;
            var ibits = infoLine.Split('|');
            if (ibits.Length >= 1) int.TryParse(ibits[0].Trim(), out containers);
            if (ibits.Length >= 2) int.TryParse(ibits[1].Trim(), out running);

            // If `docker version` server section failed (daemon down / no perms), reflect that.
            bool daemonOk = !string.IsNullOrEmpty(ver);
            return new DockerHostInfo
            {
                DockerPresent = present || daemonOk,
                ServerVersion = ver,
                OsArch = osArch,
                Containers = containers,
                Running = running,
                RawError = daemonOk ? string.Empty : stderr.Trim(),
            };
        }
        catch (Exception ex)
        {
            return new DockerHostInfo { DockerPresent = false, RawError = ex.Message };
        }
    }

    /// <summary>
    /// 列出所有容器（包括已停止）· List all containers (including stopped) via `docker ps -a`.
    /// 用穩定分隔符嘅 -f 範本輸出再逐行解析，避免 JSON 喺唔同 docker 版本嘅差異。
    /// Uses a delimiter-separated -f template and parses line-by-line.
    /// </summary>
    public async Task<(bool ok, string error, List<DockerContainer> rows)> ListAsync(
        SshProfile profile, string secret, CancellationToken ct = default)
    {
        var rows = new List<DockerContainer>();
        try
        {
            // {{.ID}}␟{{.Names}}␟{{.Image}}␟{{.State}}␟{{.Status}}␟{{.Ports}}
            string fmt = $"{{{{.ID}}}}{Delim}{{{{.Names}}}}{Delim}{{{{.Image}}}}{Delim}{{{{.State}}}}{Delim}{{{{.Status}}}}{Delim}{{{{.Ports}}}}";
            string cmd = $"docker ps -a --no-trunc --format \"{fmt}\"";
            var (code, stdout, stderr) = await RunAsync(profile, secret, cmd, ct);
            if (code != 0)
                return (false, string.IsNullOrWhiteSpace(stderr) ? $"exit {code}" : stderr.Trim(), rows);

            foreach (var line in stdout.Split('\n'))
            {
                var l = line.TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(l)) continue;
                var f = l.Split(Delim);
                if (f.Length < 6) continue;
                rows.Add(new DockerContainer
                {
                    Id = ShortId(f[0]),
                    Name = f[1].Trim(),
                    Image = f[2].Trim(),
                    State = f[3].Trim(),
                    Status = f[4].Trim(),
                    Ports = f[5].Trim(),
                });
            }
            return (true, string.Empty, rows);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, rows);
        }
    }

    // ---- power actions -----------------------------------------------------

    public Task<(int code, string stdout, string stderr)> StartAsync(SshProfile p, string s, string id, CancellationToken ct = default)
        => RunAsync(p, s, $"docker start {Q(id)}", ct);

    public Task<(int code, string stdout, string stderr)> StopAsync(SshProfile p, string s, string id, CancellationToken ct = default)
        => RunAsync(p, s, $"docker stop {Q(id)}", ct);

    public Task<(int code, string stdout, string stderr)> RestartAsync(SshProfile p, string s, string id, CancellationToken ct = default)
        => RunAsync(p, s, $"docker restart {Q(id)}", ct);

    public Task<(int code, string stdout, string stderr)> PauseAsync(SshProfile p, string s, string id, CancellationToken ct = default)
        => RunAsync(p, s, $"docker pause {Q(id)}", ct);

    public Task<(int code, string stdout, string stderr)> UnpauseAsync(SshProfile p, string s, string id, CancellationToken ct = default)
        => RunAsync(p, s, $"docker unpause {Q(id)}", ct);

    /// <summary>強制移除容器（連執行中）· Force-remove a container (even if running).</summary>
    public Task<(int code, string stdout, string stderr)> RemoveAsync(SshProfile p, string s, string id, CancellationToken ct = default)
        => RunAsync(p, s, $"docker rm -f {Q(id)}", ct);

    /// <summary>攞容器日誌（最後 N 行）· Fetch the last N lines of a container's logs.</summary>
    public async Task<string> LogsAsync(SshProfile p, string secret, string id, int tail, CancellationToken ct = default)
    {
        int n = tail <= 0 ? 200 : tail;
        // logs writes to stderr for many images; merge both streams.
        var (_, stdout, stderr) = await RunAsync(p, secret, $"docker logs --tail {n} {Q(id)} 2>&1", ct);
        var body = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        return body.Replace("\r\n", "\n").TrimEnd();
    }

    /// <summary>
    /// 喺容器入面執行一句命令 · Exec a command inside a container via `docker exec … sh -c '…'`.
    /// </summary>
    public async Task<string> ExecAsync(SshProfile p, string secret, string id, string shellCommand, CancellationToken ct = default)
    {
        // Single-quote the inner command for the remote shell, escaping embedded single quotes.
        var safe = shellCommand.Replace("'", "'\\''");
        var (_, stdout, stderr) = await RunAsync(p, secret, $"docker exec {Q(id)} sh -c '{safe}' 2>&1", ct);
        var body = string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
        return body.Replace("\r\n", "\n").TrimEnd();
    }

    // ---- helpers -----------------------------------------------------------

    /// <summary>POSIX 單引號包裹一個 token · Single-quote a token for the remote POSIX shell.</summary>
    private static string Q(string token)
    {
        token ??= string.Empty;
        return "'" + token.Replace("'", "'\\''") + "'";
    }

    private static string ShortId(string id)
    {
        id = (id ?? string.Empty).Trim();
        return id.Length > 12 ? id[..12] : id;
    }
}
