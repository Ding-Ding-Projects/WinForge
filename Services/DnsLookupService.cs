using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// DNS 查詢 · Pure-managed DNS lookup. A/AAAA/PTR go through <see cref="System.Net.Dns"/>;
/// MX/TXT/NS/CNAME use Google's public DNS-over-HTTPS JSON API (dns.google/resolve).
/// No Process.Start, no new NuGet — HttpClient + System.Text.Json only.
/// </summary>
public static class DnsLookupService
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/dns-json"));
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge-DnsLookup/1.0");
        return c;
    }

    /// <summary>One answer row (value + type + TTL).</summary>
    public sealed class DnsAnswer
    {
        public string Value { get; set; } = "";
        public string Type { get; set; } = "";
        public string Ttl { get; set; } = "";
    }

    /// <summary>Full result of a lookup: answers, elapsed time, and a bilingual status/error.</summary>
    public sealed class DnsResult
    {
        public List<DnsAnswer> Answers { get; } = new();
        public long ElapsedMs { get; set; }
        public bool Ok { get; set; }
        public string StatusEn { get; set; } = "";
        public string StatusZh { get; set; } = "";
    }

    /// <summary>
    /// Look up <paramref name="name"/> for record <paramref name="type"/>
    /// (A, AAAA, MX, TXT, NS, CNAME, PTR). Never throws — errors land in the result status.
    /// </summary>
    public static async Task<DnsResult> LookupAsync(string name, string type)
    {
        var result = new DnsResult();
        var sw = Stopwatch.StartNew();
        name = (name ?? "").Trim();
        type = (type ?? "A").Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(name))
        {
            result.StatusEn = "Enter a host name or IP address.";
            result.StatusZh = "請輸入主機名或者 IP 位址。";
            sw.Stop();
            result.ElapsedMs = sw.ElapsedMilliseconds;
            return result;
        }

        try
        {
            switch (type)
            {
                case "A":
                case "AAAA":
                    await LookupAddressesAsync(name, type, result).ConfigureAwait(false);
                    break;
                case "PTR":
                    await LookupPointerAsync(name, result).ConfigureAwait(false);
                    break;
                default:
                    await LookupDohAsync(name, type, result).ConfigureAwait(false);
                    break;
            }
        }
        catch (SocketException)
        {
            result.Ok = false;
            result.StatusEn = $"No {type} record found for \"{name}\" (or the name does not exist).";
            result.StatusZh = $"搵唔到「{name}」嘅 {type} 記錄（或者個名唔存在）。";
        }
        catch (HttpRequestException)
        {
            result.Ok = false;
            result.StatusEn = "Network error reaching the DNS-over-HTTPS resolver.";
            result.StatusZh = "連接 DNS-over-HTTPS 解析器時發生網絡錯誤。";
        }
        catch (TaskCanceledException)
        {
            result.Ok = false;
            result.StatusEn = "The lookup timed out.";
            result.StatusZh = "查詢逾時。";
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.StatusEn = "Lookup failed: " + ex.Message;
            result.StatusZh = "查詢失敗：" + ex.Message;
        }

        sw.Stop();
        result.ElapsedMs = sw.ElapsedMilliseconds;

        if (result.Ok && result.Answers.Count == 0)
        {
            result.StatusEn = $"No {type} records for \"{name}\".";
            result.StatusZh = $"「{name}」冇 {type} 記錄。";
        }

        return result;
    }

    private static async Task LookupAddressesAsync(string name, string type, DnsResult result)
    {
        IPAddress[] addrs = await Dns.GetHostAddressesAsync(name).ConfigureAwait(false);
        AddressFamily want = type == "AAAA" ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
        foreach (var a in addrs.Where(a => a.AddressFamily == want))
            result.Answers.Add(new DnsAnswer { Value = a.ToString(), Type = type, Ttl = "—" });
        result.Ok = true;
    }

    private static async Task LookupPointerAsync(string name, DnsResult result)
    {
        if (!IPAddress.TryParse(name, out var ip))
        {
            result.Ok = false;
            result.StatusEn = "PTR (reverse) lookup needs an IP address (e.g. 8.8.8.8).";
            result.StatusZh = "PTR（反向）查詢需要一個 IP 位址（例如 8.8.8.8）。";
            return;
        }
        IPHostEntry entry = await Dns.GetHostEntryAsync(ip).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(entry.HostName))
            result.Answers.Add(new DnsAnswer { Value = entry.HostName, Type = "PTR", Ttl = "—" });
        foreach (var alias in entry.Aliases ?? Array.Empty<string>())
            result.Answers.Add(new DnsAnswer { Value = alias, Type = "PTR", Ttl = "—" });
        result.Ok = true;
    }

    // Numeric DNS record types -> friendly names for display.
    private static string TypeName(int t) => t switch
    {
        1 => "A",
        2 => "NS",
        5 => "CNAME",
        6 => "SOA",
        12 => "PTR",
        15 => "MX",
        16 => "TXT",
        28 => "AAAA",
        _ => "TYPE" + t
    };

    private static async Task LookupDohAsync(string name, string type, DnsResult result)
    {
        string url = $"https://dns.google/resolve?name={Uri.EscapeDataString(name)}&type={Uri.EscapeDataString(type)}";
        using var resp = await Http.GetAsync(url).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
        var root = doc.RootElement;

        // Status 3 = NXDOMAIN; anything non-zero (except 0) means no valid answer.
        int status = root.TryGetProperty("Status", out var st) && st.ValueKind == JsonValueKind.Number ? st.GetInt32() : -1;
        if (status == 3)
        {
            result.Ok = false;
            result.StatusEn = $"\"{name}\" does not exist (NXDOMAIN).";
            result.StatusZh = $"「{name}」唔存在（NXDOMAIN）。";
            return;
        }

        result.Ok = true;
        if (root.TryGetProperty("Answer", out var answers) && answers.ValueKind == JsonValueKind.Array)
        {
            foreach (var ans in answers.EnumerateArray())
            {
                string data = ans.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? "" : "";
                int t = ans.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.Number ? ty.GetInt32() : 0;
                string ttl = ans.TryGetProperty("TTL", out var tl) && tl.ValueKind == JsonValueKind.Number ? tl.GetInt32().ToString() : "—";
                if (string.IsNullOrEmpty(data)) continue;
                result.Answers.Add(new DnsAnswer { Value = data, Type = TypeName(t), Ttl = ttl });
            }
        }
    }
}
