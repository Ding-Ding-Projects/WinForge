using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// TCP 連接埠掃描器 · Pure-managed async TCP port scanner. For each port a fresh
/// <see cref="TcpClient"/> ConnectAsync races a per-port timeout (Task.WhenAny + Task.Delay);
/// concurrency is bounded by a <see cref="SemaphoreSlim"/>. No Process.Start, no external tools.
/// Only ever scan hosts you own or are authorised to test.
/// </summary>
public static class PortScanService
{
    /// <summary>An open TCP port plus a best-guess well-known service name.</summary>
    public sealed class OpenPort
    {
        public int Port { get; init; }
        public string Service { get; init; } = "";
        public string Display => $"{Port}  ·  {Service}";
    }

    /// <summary>20–40 well-known TCP ports → service name.</summary>
    private static readonly IReadOnlyDictionary<int, string> WellKnown = new Dictionary<int, string>
    {
        [20] = "FTP-Data", [21] = "FTP", [22] = "SSH", [23] = "Telnet", [25] = "SMTP",
        [53] = "DNS", [67] = "DHCP", [69] = "TFTP", [80] = "HTTP", [110] = "POP3",
        [111] = "RPC", [123] = "NTP", [135] = "MS-RPC", [139] = "NetBIOS", [143] = "IMAP",
        [161] = "SNMP", [389] = "LDAP", [443] = "HTTPS", [445] = "SMB", [465] = "SMTPS",
        [587] = "SMTP-Sub", [993] = "IMAPS", [995] = "POP3S", [1433] = "MSSQL",
        [1521] = "Oracle", [1723] = "PPTP", [2049] = "NFS", [3306] = "MySQL",
        [3389] = "RDP", [5432] = "PostgreSQL", [5900] = "VNC", [5985] = "WinRM",
        [6379] = "Redis", [8080] = "HTTP-Alt", [8443] = "HTTPS-Alt", [9200] = "Elasticsearch",
        [11211] = "Memcached", [27017] = "MongoDB",
    };

    /// <summary>Look up a friendly service name for a port (empty string if unknown).</summary>
    public static string ServiceName(int port) => WellKnown.TryGetValue(port, out var s) ? s : "";

    /// <summary>
    /// Resolve <paramref name="host"/> to an IP. Throws on failure so the UI can report it.
    /// </summary>
    public static async Task<IPAddress> ResolveAsync(string host, CancellationToken ct)
    {
        if (IPAddress.TryParse(host, out var literal)) return literal;
        var entries = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
        if (entries.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
        return entries[0];
    }

    /// <summary>
    /// Probe a single TCP port. Returns true if a connection completes within
    /// <paramref name="timeoutMs"/>. Never throws for ordinary connection refusals/timeouts.
    /// </summary>
    public static async Task<bool> ProbeAsync(IPAddress ip, int port, int timeoutMs, CancellationToken ct)
    {
        using var client = new TcpClient(ip.AddressFamily);
        try
        {
            var connect = client.ConnectAsync(ip, port, ct).AsTask();
            var delay = Task.Delay(timeoutMs, ct);
            var done = await Task.WhenAny(connect, delay).ConfigureAwait(false);
            if (done == connect && connect.IsCompletedSuccessfully && client.Connected)
                return true;
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Refused / unreachable / reset → treated as closed.
            return false;
        }
    }

    /// <summary>
    /// Scan <paramref name="startPort"/>..<paramref name="endPort"/> on <paramref name="host"/>,
    /// bounded by <paramref name="maxConcurrency"/>. <paramref name="onProgress"/> fires once per
    /// port probed (scanned count); <paramref name="onOpen"/> fires per open port found. Both callbacks
    /// are invoked from worker tasks — marshal to the UI thread inside them.
    /// </summary>
    public static async Task ScanAsync(
        string host, int startPort, int endPort, int timeoutMs, int maxConcurrency,
        Action<int> onProgress, Action<OpenPort> onOpen, CancellationToken ct)
    {
        if (startPort < 1) startPort = 1;
        if (endPort > 65535) endPort = 65535;
        if (endPort < startPort) (startPort, endPort) = (endPort, startPort);
        if (timeoutMs < 25) timeoutMs = 25;
        if (maxConcurrency < 1) maxConcurrency = 1;

        var ip = await ResolveAsync(host, ct).ConfigureAwait(false);

        using var gate = new SemaphoreSlim(maxConcurrency);
        var tasks = new List<Task>(endPort - startPort + 1);

        for (int port = startPort; port <= endPort; port++)
        {
            ct.ThrowIfCancellationRequested();
            await gate.WaitAsync(ct).ConfigureAwait(false);
            int p = port;
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    bool open = await ProbeAsync(ip, p, timeoutMs, ct).ConfigureAwait(false);
                    if (open) onOpen(new OpenPort { Port = p, Service = ServiceName(p) });
                }
                catch (OperationCanceledException) { /* cancelled — stop quietly */ }
                finally
                {
                    onProgress(p);
                    gate.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }
}
