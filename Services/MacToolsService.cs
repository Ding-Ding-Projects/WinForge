using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// MAC 位址工具 · MAC address tools — pure-managed formatting, validation, OUI vendor lookup and
/// random locally-administered address generation. No P/Invoke, no process launch. Never throws.
/// </summary>
public static class MacToolsService
{
    /// <summary>Parse any common MAC form into 6 bytes. Returns null on bad input (never throws).</summary>
    public static byte[]? Parse(string? input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var sb = new StringBuilder(12);
            foreach (char c in input)
                if (Uri.IsHexDigit(c)) sb.Append(c);
            if (sb.Length != 12) return null;
            var bytes = new byte[6];
            for (int i = 0; i < 6; i++)
                bytes[i] = Convert.ToByte(sb.ToString(i * 2, 2), 16);
            return bytes;
        }
        catch { return null; }
    }

    public static bool IsValid(string? input) => Parse(input) != null;

    private static string Hex(byte b, bool upper) => upper ? b.ToString("X2") : b.ToString("x2");

    /// <summary>Colon form aa:bb:cc:dd:ee:ff.</summary>
    public static string ToColon(byte[] b, bool upper) => Join(b, ":", upper);
    /// <summary>Hyphen form aa-bb-cc-dd-ee-ff.</summary>
    public static string ToHyphen(byte[] b, bool upper) => Join(b, "-", upper);
    /// <summary>Bare form aabbccddeeff.</summary>
    public static string ToBare(byte[] b, bool upper) => Join(b, "", upper);

    private static string Join(byte[] b, string sep, bool upper) =>
        string.Join(sep, b.Select(x => Hex(x, upper)));

    /// <summary>Cisco dotted form aabb.ccdd.eeff.</summary>
    public static string ToDot(byte[] b, bool upper)
    {
        try
        {
            return string.Join(".",
                Hex(b[0], upper) + Hex(b[1], upper),
                Hex(b[2], upper) + Hex(b[3], upper),
                Hex(b[4], upper) + Hex(b[5], upper));
        }
        catch { return string.Empty; }
    }

    /// <summary>True when the I/G (least-significant bit of first octet) marks a multicast address.</summary>
    public static bool IsMulticast(byte[] b) => (b[0] & 0x01) != 0;

    /// <summary>True when the U/L bit (bit 1 of first octet) marks a locally-administered address.</summary>
    public static bool IsLocallyAdministered(byte[] b) => (b[0] & 0x02) != 0;

    /// <summary>True for the all-ones broadcast address.</summary>
    public static bool IsBroadcast(byte[] b) => b.All(x => x == 0xFF);

    /// <summary>Look up the vendor for the first three octets (OUI). Returns null when unknown.</summary>
    public static string? LookupVendor(byte[] b)
    {
        string oui = (Hex(b[0], true) + Hex(b[1], true) + Hex(b[2], true));
        return Ouis.TryGetValue(oui, out var v) ? v : null;
    }

    /// <summary>Generate a random locally-administered unicast MAC (U/L set, I/G clear) via RNG.</summary>
    public static byte[] GenerateLocalUnicast()
    {
        var b = new byte[6];
        RandomNumberGenerator.Fill(b);
        b[0] = (byte)((b[0] & 0xFC) | 0x02); // clear I/G (unicast), set U/L (local)
        return b;
    }

    /// <summary>~50 common OUI prefixes (upper-case, no separators) → vendor name.</summary>
    private static readonly Dictionary<string, string> Ouis = new(StringComparer.OrdinalIgnoreCase)
    {
        ["001A2B"] = "Ayecom Technology",
        ["001B63"] = "Apple",
        ["001CB3"] = "Apple",
        ["0017F2"] = "Apple",
        ["002500"] = "Apple",
        ["3C0754"] = "Apple",
        ["A45E60"] = "Apple",
        ["F0DBF8"] = "Apple",
        ["001D0F"] = "TP-Link",
        ["50C7BF"] = "TP-Link",
        ["A42BB0"] = "TP-Link",
        ["001B21"] = "Intel",
        ["001517"] = "Intel",
        ["00A0C9"] = "Intel",
        ["3CA9F4"] = "Intel",
        ["B0359F"] = "Intel",
        ["001560"] = "Hewlett-Packard",
        ["001321"] = "Hewlett-Packard",
        ["3C4A92"] = "Hewlett-Packard",
        ["001279"] = "Cisco",
        ["00000C"] = "Cisco",
        ["0010A4"] = "Cisco",
        ["001A2F"] = "Cisco",
        ["00248C"] = "Cisco",
        ["001143"] = "Dell",
        ["00188B"] = "Dell",
        ["002170"] = "Dell",
        ["B8CA3A"] = "Dell",
        ["F8BC12"] = "Dell",
        ["001377"] = "Samsung",
        ["0016DB"] = "Samsung",
        ["0023D6"] = "Samsung",
        ["5CE8EB"] = "Samsung",
        ["001AA0"] = "Sony",
        ["001DBA"] = "Sony",
        ["0024BE"] = "Sony",
        ["00037F"] = "Atheros",
        ["001B11"] = "D-Link",
        ["00179A"] = "D-Link",
        ["1C7EE5"] = "D-Link",
        ["000FB5"] = "Netgear",
        ["00223F"] = "Netgear",
        ["A040A0"] = "Netgear",
        ["001018"] = "Broadcom",
        ["00104B"] = "3Com",
        ["001438"] = "Hewlett-Packard",
        ["00E04C"] = "Realtek",
        ["525400"] = "QEMU/KVM virtual",
        ["000C29"] = "VMware",
        ["005056"] = "VMware",
        ["0003FF"] = "Microsoft (Hyper-V)",
        ["00155D"] = "Microsoft (Hyper-V)",
        ["080027"] = "VirtualBox",
        ["0016CB"] = "Apple",
        ["001EC2"] = "Apple",
        ["FCFBFB"] = "Cisco",
        ["001E58"] = "D-Link",
        ["00259C"] = "Cisco-Linksys",
        ["000625"] = "Linksys",
    };
}
