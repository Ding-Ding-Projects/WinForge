using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// IP 同網絡資訊 · IP &amp; network info — pure-managed enumeration of local adapters
/// (System.Net.NetworkInformation) plus an optional public-IP lookup over HTTPS.
/// No redirect, no Process.Start; never throws — callers get results or empty lists.
/// </summary>
public static class IpInfoService
{
    /// <summary>One "up" network adapter, flattened for {Binding} in a ListView.</summary>
    public sealed class AdapterInfo
    {
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string Type { get; init; } = "";
        public string Mac { get; init; } = "";
        public string Speed { get; init; } = "";
        public string IPv4 { get; init; } = "";
        public string IPv6 { get; init; } = "";
        public string Gateways { get; init; } = "";
        public string Dns { get; init; } = "";
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    /// <summary>Enumerate all operational, non-loopback adapters. Labels are localized at
    /// call time via Loc; re-call on a language change to relabel. Never throws.</summary>
    public static List<AdapterInfo> GetAdapters()
    {
        var list = new List<AdapterInfo>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var props = ni.GetIPProperties();
                    var v4 = props.UnicastAddresses
                        .Where(u => u.Address.AddressFamily == AddressFamily.InterNetwork)
                        .Select(u => u.Address.ToString()).ToList();
                    var v6 = props.UnicastAddresses
                        .Where(u => u.Address.AddressFamily == AddressFamily.InterNetworkV6)
                        .Select(u => u.Address.ToString()).ToList();
                    var gw = props.GatewayAddresses
                        .Select(g => g.Address?.ToString())
                        .Where(s => !string.IsNullOrEmpty(s)).Cast<string>().ToList();
                    var dns = props.DnsAddresses.Select(d => d.ToString()).ToList();

                    string none = P("none", "冇");
                    list.Add(new AdapterInfo
                    {
                        Name = ni.Name,
                        Description = ni.Description,
                        Type = P("Type", "類型") + ": " + ni.NetworkInterfaceType,
                        Mac = "MAC · " + P("hardware address", "硬件地址") + ": " + FormatMac(ni.GetPhysicalAddress()),
                        Speed = P("Link speed", "連線速度") + ": " + FormatSpeed(ni.Speed),
                        IPv4 = "IPv4: " + (v4.Count > 0 ? string.Join(", ", v4) : none),
                        IPv6 = "IPv6: " + (v6.Count > 0 ? string.Join(", ", v6) : none),
                        Gateways = P("Gateway", "閘道") + ": " + (gw.Count > 0 ? string.Join(", ", gw) : none),
                        Dns = "DNS: " + (dns.Count > 0 ? string.Join(", ", dns) : none),
                    });
                }
                catch { /* skip a single problematic adapter */ }
            }
        }
        catch { /* enumeration failed entirely — return whatever we have */ }
        return list;
    }

    private static string FormatMac(PhysicalAddress mac)
    {
        try
        {
            var bytes = mac?.GetAddressBytes();
            if (bytes == null || bytes.Length == 0) return "—";
            return string.Join(":", bytes.Select(b => b.ToString("X2")));
        }
        catch { return "—"; }
    }

    private static string FormatSpeed(long bitsPerSecond)
    {
        if (bitsPerSecond <= 0) return "—";
        double mbps = bitsPerSecond / 1_000_000d;
        if (mbps >= 1000) return $"{mbps / 1000d:0.#} Gbps";
        return $"{mbps:0.#} Mbps";
    }

    /// <summary>
    /// Fetch the public IP address over HTTPS. Makes a network request; returns null on any
    /// failure (offline, timeout, non-success). Never throws.
    /// </summary>
    public static async Task<string?> GetPublicIpAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge");
            string text = await http.GetStringAsync("https://api.ipify.org", ct).ConfigureAwait(false);
            text = text?.Trim() ?? "";
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }
}
