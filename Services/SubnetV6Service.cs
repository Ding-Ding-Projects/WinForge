using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace WinForge.Services;

/// <summary>
/// IPv6 位址與子網工具 · Pure-managed IPv6 address &amp; subnet helpers.
/// Parsing/formatting via <see cref="IPAddress"/> (InterNetworkV6) and <see cref="BigInteger"/>.
/// Every method is defensive: it never throws — callers get an ok flag + message.
/// </summary>
public static class SubnetV6Service
{
    /// <summary>Result of parsing an IPv6 input (optionally with a /prefix).</summary>
    public sealed class ParseResult
    {
        public bool Ok;
        public string? Error;          // null when Ok
        public IPAddress? Address;     // canonical parsed address
        public int? Prefix;            // 0..128 when supplied inline, else null
    }

    /// <summary>Details computed for a prefix over an address.</summary>
    public sealed class PrefixResult
    {
        public bool Ok;
        public string? Error;
        public string NetworkCompressed = "";
        public string NetworkExpanded = "";
        public string MaskCompressed = "";
        public string FirstAddress = "";
        public string LastAddress = "";
        public string CountPow = "";       // e.g. "2^64"
        public string CountBig = "";       // full decimal count
    }

    /// <summary>
    /// Parse text as an IPv6 address, tolerating an optional "/prefix" suffix.
    /// Rejects IPv4 (verifies AddressFamily is InterNetworkV6). Never throws.
    /// </summary>
    public static ParseResult Parse(string? input)
    {
        var r = new ParseResult();
        try
        {
            var text = (input ?? "").Trim();
            if (text.Length == 0) { r.Error = "empty"; return r; }

            int? prefix = null;
            int slash = text.IndexOf('/');
            if (slash >= 0)
            {
                var pfxPart = text[(slash + 1)..].Trim();
                text = text[..slash].Trim();
                if (!int.TryParse(pfxPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p)
                    || p < 0 || p > 128)
                { r.Error = "badprefix"; return r; }
                prefix = p;
            }

            // Strip an optional zone id (fe80::1%eth0) — not part of the numeric address.
            int pct = text.IndexOf('%');
            if (pct >= 0) text = text[..pct].Trim();

            if (!IPAddress.TryParse(text, out var addr) || addr is null)
            { r.Error = "badaddr"; return r; }
            if (addr.AddressFamily != AddressFamily.InterNetworkV6)
            { r.Error = "notv6"; return r; }

            r.Ok = true;
            r.Address = addr;
            r.Prefix = prefix;
            return r;
        }
        catch
        {
            r.Ok = false;
            r.Error = "badaddr";
            r.Address = null;
            return r;
        }
    }

    /// <summary>Fully-expanded form: 8 groups of 4 hex digits, colon-separated.</summary>
    public static string Expand(IPAddress addr)
    {
        try
        {
            var b = addr.GetAddressBytes(); // 16 bytes, network order
            if (b.Length != 16) return addr.ToString();
            var parts = new string[8];
            for (int i = 0; i < 8; i++)
            {
                int hi = b[i * 2], lo = b[i * 2 + 1];
                parts[i] = ((hi << 8) | lo).ToString("x4", CultureInfo.InvariantCulture);
            }
            return string.Join(":", parts);
        }
        catch { return addr.ToString(); }
    }

    /// <summary>Compressed canonical form (:: collapsing) via the framework.</summary>
    public static string Compress(IPAddress addr)
    {
        try { return addr.ToString(); } catch { return ""; }
    }

    /// <summary>
    /// Classify the address by leading bits: loopback / link-local / unique-local /
    /// multicast / unspecified / global. Returns an (en, zh) pair.
    /// </summary>
    public static (string En, string Zh) Classify(IPAddress addr)
    {
        try
        {
            var b = addr.GetAddressBytes();
            if (b.Length != 16) return ("Unknown", "未知");

            if (IPAddress.IPv6Loopback.Equals(addr)) return ("Loopback (::1)", "回送位址 (::1)");
            if (IPAddress.IPv6None.Equals(addr)) return ("Unspecified (::)", "未指定位址 (::)");

            // ff00::/8 multicast
            if (b[0] == 0xff) return ("Multicast (ff00::/8)", "多播 (ff00::/8)");
            // fe80::/10 link-local
            if (b[0] == 0xfe && (b[1] & 0xc0) == 0x80) return ("Link-local (fe80::/10)", "鏈路本地 (fe80::/10)");
            // fc00::/7 unique local
            if ((b[0] & 0xfe) == 0xfc) return ("Unique-local (fc00::/7)", "唯一本地 (fc00::/7)");

            return ("Global unicast", "全球單播");
        }
        catch { return ("Unknown", "未知"); }
    }

    /// <summary>
    /// Compute the network prefix, mask, address count and first/last address for
    /// <paramref name="addr"/> under a prefix length of <paramref name="prefix"/> (0..128).
    /// </summary>
    public static PrefixResult ComputePrefix(IPAddress addr, int prefix)
    {
        var r = new PrefixResult();
        try
        {
            if (prefix < 0 || prefix > 128) { r.Error = "badprefix"; return r; }
            var addrBytes = addr.GetAddressBytes();
            if (addrBytes.Length != 16) { r.Error = "badaddr"; return r; }

            // Build a 16-byte prefix mask with `prefix` leading 1-bits.
            var mask = new byte[16];
            int full = prefix / 8, rem = prefix % 8;
            for (int i = 0; i < full && i < 16; i++) mask[i] = 0xff;
            if (rem > 0 && full < 16) mask[full] = (byte)(0xff << (8 - rem) & 0xff);

            var netBytes = new byte[16];
            var lastBytes = new byte[16];
            for (int i = 0; i < 16; i++)
            {
                netBytes[i] = (byte)(addrBytes[i] & mask[i]);
                lastBytes[i] = (byte)(netBytes[i] | (byte)~mask[i]);
            }

            var network = new IPAddress(netBytes);
            var last = new IPAddress(lastBytes);
            var maskAddr = new IPAddress(mask);

            r.NetworkCompressed = network.ToString();
            r.NetworkExpanded = Expand(network);
            r.MaskCompressed = maskAddr.ToString();
            r.FirstAddress = network.ToString();
            r.LastAddress = last.ToString();

            int hostBits = 128 - prefix;
            r.CountPow = "2^" + hostBits.ToString(CultureInfo.InvariantCulture);
            r.CountBig = (BigInteger.One << hostBits).ToString(CultureInfo.InvariantCulture);

            r.Ok = true;
            return r;
        }
        catch
        {
            r.Ok = false;
            r.Error = "badaddr";
            return r;
        }
    }

    /// <summary>
    /// Convert a 48-bit MAC (EUI-48) into the 64-bit EUI-64 interface identifier:
    /// insert ff:fe in the middle and flip the U/L bit (bit 1 of the first octet).
    /// Accepts separators of ':', '-', '.', or none. Returns null on bad input.
    /// </summary>
    public static string? MacToEui64(string? mac)
    {
        try
        {
            var text = (mac ?? "");
            Span<char> hexBuf = stackalloc char[12];
            int n = 0;
            foreach (char c in text)
            {
                if (c is ':' or '-' or '.' or ' ') continue;
                if (!Uri.IsHexDigit(c)) return null;
                if (n >= 12) return null;
                hexBuf[n++] = c;
            }
            if (n != 12) return null;

            var b = new byte[6];
            for (int i = 0; i < 6; i++)
                b[i] = byte.Parse(hexBuf.Slice(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            // Flip the universal/local bit.
            byte first = (byte)(b[0] ^ 0x02);

            var eui = new byte[8]
            {
                first, b[1], b[2], 0xff, 0xfe, b[3], b[4], b[5]
            };

            var groups = new string[4];
            for (int i = 0; i < 4; i++)
                groups[i] = ((eui[i * 2] << 8) | eui[i * 2 + 1]).ToString("x4", CultureInfo.InvariantCulture);
            return string.Join(":", groups);
        }
        catch { return null; }
    }
}
