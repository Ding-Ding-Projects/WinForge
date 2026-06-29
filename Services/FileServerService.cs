using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個對外分享嘅資料夾（FTP／SFTP 主機）· One hosted folder share (FTP/SFTP server).</summary>
public sealed class HostedShare
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string Protocol { get; set; } = "sftp";   // sftp | ftp
    public string Username { get; set; } = "user";
    public int Port { get; set; }                      // host control port (22→sftp, 21→ftp)
    public int PasvBase { get; set; } = 21000;         // ftp passive range base (PasvBase..PasvBase+10)
    public string SecretBlob { get; set; } = "";       // DPAPI-protected password (never plaintext)
    public string CreatedUtc { get; set; } = DateTime.UtcNow.ToString("o");

    public string ProjectName => $"wf_fileshare_{Id}";
    public string ServiceName => Protocol == "ftp" ? "ftpd" : "sftpd";

    /// <summary>顯示用端點摘要 · Display-only endpoint summary for the list UI.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string Endpoint => $"{Protocol.ToUpperInvariant()}  ·  {Username}@host:{Port}";
}

/// <summary>
/// 檔案伺服器引擎 · File-server engine — host one of your folders out over <b>SFTP</b> (atmoz/sftp) or
/// <b>FTP/FTPS</b> (delfer/alpine-ftp-server) by driving Docker through <see cref="DockerService"/>.
/// One compose project per share with an auto-picked free host port so many shares run at once. Passwords
/// are DPAPI-protected (never plaintext/logs). Fully managed; no shelling out to <c>docker</c>.
/// </summary>
public static class FileServerService
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");
    private static readonly string StoreFile = Path.Combine(AppDir, "fileshares.json");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.FileServer.v1");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // ───────────────────────── persistence (defs only; password DPAPI-protected) ─────────────────────────

    public static List<HostedShare> Load()
    {
        try
        {
            if (!File.Exists(StoreFile)) return new();
            return JsonSerializer.Deserialize<List<HostedShare>>(File.ReadAllText(StoreFile)) ?? new();
        }
        catch { return new(); }
    }

    private static void Save(List<HostedShare> shares)
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllText(StoreFile, JsonSerializer.Serialize(shares, JsonOpts));
    }

    /// <summary>新增／更新一個分享定義（連加密密碼）· Upsert a share definition with an encrypted password.</summary>
    public static HostedShare SaveShare(HostedShare share, string? password)
    {
        var shares = Load();
        if (password is not null) share.SecretBlob = string.IsNullOrEmpty(password) ? "" : Protect(password);
        var idx = shares.FindIndex(s => s.Id == share.Id);
        if (idx >= 0) shares[idx] = share; else shares.Add(share);
        Save(shares);
        return share;
    }

    public static string GetPassword(HostedShare share)
        => string.IsNullOrEmpty(share.SecretBlob) ? "" : Unprotect(share.SecretBlob);

    public static void DeleteShareDef(string id)
    {
        var shares = Load();
        shares.RemoveAll(s => s.Id == id);
        Save(shares);
    }

    private static string Protect(string plain)
    {
        try { return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }

    private static string Unprotect(string blob)
    {
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(blob), Entropy, DataProtectionScope.CurrentUser)); }
        catch { return ""; }
    }

    // ───────────────────────── networking helpers ─────────────────────────

    /// <summary>搵一個未用嘅 TCP 連接埠 · Find a free host TCP port at/after a starting point.</summary>
    public static int FindFreePort(int start = 2222)
    {
        var used = new HashSet<int>(IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners().Select(e => e.Port));
        for (int p = start; p < start + 2000; p++)
            if (!used.Contains(p)) return p;
        // Last resort: let the OS pick.
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start(); int port = ((IPEndPoint)l.LocalEndpoint).Port; l.Stop();
        return port;
    }

    /// <summary>本機區域網 IPv4 · The machine's LAN IPv4 address (for connecting from another machine).</summary>
    public static string LanIPv4()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork
                        && !IPAddress.IsLoopback(ua.Address))
                        return ua.Address.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    /// <summary>連線字串 · Build the connection string a client uses to reach a share.</summary>
    public static string ConnectionString(HostedShare s)
    {
        var host = LanIPv4();
        return s.Protocol == "ftp"
            ? $"ftp://{s.Username}@{host}:{s.Port}/"
            : $"sftp://{s.Username}@{host}:{s.Port}/";
    }

    // ───────────────────────── docker lifecycle ─────────────────────────

    private static ComposeProject BuildProject(HostedShare s, string password)
    {
        var project = new ComposeProject { Name = s.ProjectName };
        if (s.Protocol == "ftp")
        {
            var lan = LanIPv4();
            var svc = new ComposeService { Name = s.ServiceName, Image = "delfer/alpine-ftp-server:latest" };
            svc.Env["USERS"] = $"{s.Username}|{password}|/ftp/{s.Username}|1000";
            svc.Env["ADDRESS"] = lan;
            svc.Env["MIN_PORT"] = s.PasvBase.ToString();
            svc.Env["MAX_PORT"] = (s.PasvBase + 10).ToString();
            svc.Volumes.Add($"{s.FolderPath}:/ftp/{s.Username}");
            svc.Ports.Add($"{s.Port}:21");
            // Publish the passive range 1:1 so PASV transfers work through the port mapping.
            for (int p = s.PasvBase; p <= s.PasvBase + 10; p++)
                svc.Ports.Add($"{p}:{p}");
            project.Services.Add(svc);
        }
        else // sftp
        {
            var svc = new ComposeService { Name = s.ServiceName, Image = "atmoz/sftp:latest" };
            // atmoz/sftp user spec: user:pass[:e][:uid]  — mount the folder under the user's home.
            svc.Command = $"{s.Username}:{password}:1001";
            svc.Volumes.Add($"{s.FolderPath}:/home/{s.Username}/share");
            svc.Ports.Add($"{s.Port}:22");
            project.Services.Add(svc);
        }
        return project;
    }

    public static async Task<TweakResult> StartShareAsync(HostedShare s, IProgress<string>? log, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(s.FolderPath) || !Directory.Exists(s.FolderPath))
            return TweakResult.Fail("Pick an existing folder to host.", "請揀一個存在嘅資料夾嚟分享。");
        var pwd = GetPassword(s);
        if (string.IsNullOrEmpty(pwd))
            return TweakResult.Fail("Set a password for this share first.", "請先為呢個分享設定密碼。");
        try
        {
            using var docker = new DockerService();
            await docker.ConnectAsync(null, ct);
            if (!docker.Connected)
                return TweakResult.Fail("Docker engine is not reachable.", "連唔到 Docker 引擎。");
            await docker.ComposeUpAsync(BuildProject(s, pwd), log, ct);
            return TweakResult.Ok(
                $"{s.Protocol.ToUpperInvariant()} share \"{s.Name}\" is up at {ConnectionString(s)}",
                $"{s.Protocol.ToUpperInvariant()} 分享「{s.Name}」已啟動：{ConnectionString(s)}");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static async Task<TweakResult> StopShareAsync(HostedShare s, IProgress<string>? log, CancellationToken ct = default)
    {
        try
        {
            using var docker = new DockerService();
            await docker.ConnectAsync(null, ct);
            if (!docker.Connected) return TweakResult.Fail("Docker engine is not reachable.", "連唔到 Docker 引擎。");
            await docker.ComposeDownAsync(s.ProjectName, log, ct);
            return TweakResult.Ok($"Share \"{s.Name}\" stopped.", $"分享「{s.Name}」已停止。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static async Task<TweakResult> RemoveShareAsync(HostedShare s, IProgress<string>? log, CancellationToken ct = default)
    {
        var stop = await StopShareAsync(s, log, ct);
        DeleteShareDef(s.Id);
        return stop.Success
            ? TweakResult.Ok($"Share \"{s.Name}\" removed.", $"分享「{s.Name}」已移除。")
            : TweakResult.Ok($"Share \"{s.Name}\" definition removed (stop reported: {stop.Message?.Primary}).",
                             $"已移除分享「{s.Name}」定義。");
    }

    /// <summary>呢個分享而家運行緊嗎？· Is this share's container currently running?</summary>
    public static async Task<bool> IsRunningAsync(HostedShare s, CancellationToken ct = default)
    {
        try
        {
            using var docker = new DockerService();
            await docker.ConnectAsync(null, ct);
            if (!docker.Connected) return false;
            var all = await docker.ListContainersAsync(true, ct);
            return all.Any(c => c.Labels is not null
                && c.Labels.TryGetValue("com.docker.compose.project", out var pn) && pn == s.ProjectName
                && c.State == "running");
        }
        catch { return false; }
    }

    public static async Task<bool> DockerReachableAsync(CancellationToken ct = default)
    {
        try
        {
            using var docker = new DockerService();
            await docker.ConnectAsync(null, ct);
            return docker.Connected;
        }
        catch { return false; }
    }
}
