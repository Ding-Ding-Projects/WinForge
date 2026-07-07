using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// Ping &amp; traceroute · Pure-managed ICMP echo helpers built on
/// <see cref="System.Net.NetworkInformation.Ping"/>. No shelling out; every call is async and
/// cancellable, and DNS-resolution failures surface as friendly results rather than throwing.
/// </summary>
public static class PingService
{
    /// <summary>Result of a single ping / traceroute probe.</summary>
    public sealed record Probe(
        int Sequence,
        string Address,
        long RoundtripMs,
        int Ttl,
        IPStatus Status,
        bool Success);

    private static readonly byte[] Payload = new byte[32];

    /// <summary>
    /// Send one ICMP echo to <paramref name="host"/>. Never throws for the common failure modes
    /// (bad host, timeout, cancel) — those come back as a <see cref="Probe"/> with Success=false.
    /// </summary>
    public static async Task<Probe> PingOnceAsync(string host, int seq, int timeoutMs = 4000, int? ttl = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            using var ping = new Ping();
            var opts = ttl.HasValue ? new PingOptions { Ttl = ttl.Value, DontFragment = false } : null;
            PingReply reply = opts is null
                ? await ping.SendPingAsync(host, timeoutMs, Payload).ConfigureAwait(false)
                : await ping.SendPingAsync(host, timeoutMs, Payload, opts).ConfigureAwait(false);

            string addr = reply.Address?.ToString() ?? "*";
            int replyTtl = reply.Options?.Ttl ?? 0;
            return new Probe(seq, addr, reply.RoundtripTime, replyTtl, reply.Status, reply.Status == IPStatus.Success);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PingException)
        {
            // Almost always DNS resolution failure or no route.
            return new Probe(seq, "*", 0, 0, IPStatus.DestinationHostUnreachable, false);
        }
        catch (Exception)
        {
            return new Probe(seq, "*", 0, 0, IPStatus.Unknown, false);
        }
    }

    /// <summary>Resolve a host to an IP string for display; returns null when it can't be resolved.</summary>
    public static async Task<string?> TryResolveAsync(string host, CancellationToken ct = default)
    {
        try
        {
            var addrs = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
            return addrs.Length > 0 ? addrs[0].ToString() : null;
        }
        catch
        {
            return null;
        }
    }
}
