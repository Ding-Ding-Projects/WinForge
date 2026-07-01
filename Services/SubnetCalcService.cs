using System;
using System.Collections.Generic;
using System.Net;

namespace WinForge.Services;

/// <summary>
/// IPv4 子網計算 · Pure-managed IPv4 subnet calculator. Parses with IPAddress.TryParse, converts to a
/// big-endian uint, and does all arithmetic on uint bit math — no shelling out, never throws.
/// </summary>
public static class SubnetCalcService
{
    public sealed record Result(
        uint Ip, int Prefix,
        uint Network, uint Broadcast, uint Mask, uint Wildcard,
        uint FirstHost, uint LastHost, bool HasHosts,
        ulong TotalAddresses, ulong UsableHosts,
        char Class, bool IsPrivate);

    public sealed record SplitSubnet(int Index, uint Network, int Prefix, uint Mask);

    /// <summary>Parse an IPv4 dotted-quad into a big-endian uint. Rejects IPv6 / anything non-v4.</summary>
    public static bool TryParseIPv4(string? text, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (!IPAddress.TryParse(text.Trim(), out var addr)) return false;
        if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
        byte[] b = addr.GetAddressBytes(); // network order (big-endian)
        if (b.Length != 4) return false;
        value = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
        return true;
    }

    /// <summary>Format a big-endian uint back to a dotted quad.</summary>
    public static string ToDotted(uint value) =>
        $"{(value >> 24) & 0xFF}.{(value >> 16) & 0xFF}.{(value >> 8) & 0xFF}.{value & 0xFF}";

    /// <summary>Build the network mask for a prefix length (0..32).</summary>
    public static uint MaskFromPrefix(int prefix)
    {
        if (prefix <= 0) return 0u;
        if (prefix >= 32) return 0xFFFFFFFFu;
        return 0xFFFFFFFFu << (32 - prefix);
    }

    /// <summary>Return the prefix length for a valid contiguous mask, or -1 if the mask is non-contiguous.</summary>
    public static int PrefixFromMask(uint mask)
    {
        // Must be a run of 1s followed by a run of 0s.
        uint inv = ~mask;
        // inv+1 must be a power of two (or zero) for a contiguous mask.
        if ((inv & (inv + 1)) != 0) return -1;
        int count = 0;
        while (mask != 0) { count += (int)(mask & 1u); mask >>= 1; }
        return count;
    }

    private static char ClassOf(uint ip)
    {
        byte first = (byte)((ip >> 24) & 0xFF);
        if (first < 128) return 'A';
        if (first < 192) return 'B';
        if (first < 224) return 'C';
        if (first < 240) return 'D'; // multicast
        return 'E';                    // experimental
    }

    private static bool IsPrivate(uint ip)
    {
        byte a = (byte)((ip >> 24) & 0xFF);
        byte b = (byte)((ip >> 16) & 0xFF);
        if (a == 10) return true;                          // 10.0.0.0/8
        if (a == 172 && b >= 16 && b <= 31) return true;   // 172.16.0.0/12
        if (a == 192 && b == 168) return true;             // 192.168.0.0/16
        return false;
    }

    /// <summary>Compute everything for the given IP + prefix. Returns null only on out-of-range prefix.</summary>
    public static Result? Compute(uint ip, int prefix)
    {
        if (prefix < 0 || prefix > 32) return null;
        uint mask = MaskFromPrefix(prefix);
        uint wildcard = ~mask;
        uint network = ip & mask;
        uint broadcast = network | wildcard;

        ulong total = 1UL << (32 - prefix);
        uint first, last;
        ulong usable;
        bool hasHosts;

        if (prefix >= 31)
        {
            // /31 (RFC 3021 point-to-point) and /32 have no separate net/broadcast host range.
            first = network;
            last = broadcast;
            usable = prefix == 32 ? 1UL : 2UL;
            hasHosts = true;
        }
        else
        {
            first = network + 1;
            last = broadcast - 1;
            usable = total - 2;
            hasHosts = true;
        }

        return new Result(ip, prefix, network, broadcast, mask, wildcard,
            first, last, hasHosts, total, usable, ClassOf(ip), IsPrivate(ip));
    }

    /// <summary>
    /// Split a base network (network address + current prefix) into equal subnets at the given
    /// new prefix. Capped to <paramref name="cap"/> results. Returns empty on invalid input.
    /// </summary>
    public static List<SplitSubnet> Split(uint network, int currentPrefix, int newPrefix, int cap = 256)
    {
        var list = new List<SplitSubnet>();
        if (currentPrefix < 0 || currentPrefix > 32) return list;
        if (newPrefix < currentPrefix || newPrefix > 32) return list;

        uint newMask = MaskFromPrefix(newPrefix);
        uint baseNet = network & MaskFromPrefix(currentPrefix);
        int bits = newPrefix - currentPrefix;
        ulong count = 1UL << bits;                 // number of resulting subnets
        ulong step = 1UL << (32 - newPrefix);      // address span of each subnet

        for (ulong i = 0; i < count && list.Count < cap; i++)
        {
            uint sub = (uint)(baseNet + i * step);
            list.Add(new SplitSubnet((int)i + 1, sub, newPrefix, newMask));
        }
        return list;
    }

    /// <summary>Given a desired subnet count, work out the smallest new prefix that yields that many.</summary>
    public static int PrefixForCount(int currentPrefix, int subnetCount)
    {
        if (subnetCount <= 1) return currentPrefix;
        int bits = 0;
        long n = 1;
        while (n < subnetCount) { n <<= 1; bits++; }
        int newPrefix = currentPrefix + bits;
        return newPrefix > 32 ? 32 : newPrefix;
    }
}
