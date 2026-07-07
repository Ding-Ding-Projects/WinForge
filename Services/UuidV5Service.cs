using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 具名空間 UUID（RFC 4122 v3/v5）· Namespaced name-based UUID generator.
/// v3 = MD5, v5 = SHA-1 over (namespace bytes big-endian + name UTF-8), with
/// version + variant bits set per RFC 4122. Pure managed; never throws.
/// </summary>
public static class UuidV5Service
{
    // Standard predefined namespaces (RFC 4122 Appendix C).
    public static readonly Guid NamespaceDns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");
    public static readonly Guid NamespaceUrl = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
    public static readonly Guid NamespaceOid = new("6ba7b812-9dad-11d1-80b4-00c04fd430c8");
    public static readonly Guid NamespaceX500 = new("6ba7b814-9dad-11d1-80b4-00c04fd430c8");

    /// <summary>Try to parse a custom namespace GUID string. Returns false on any bad input.</summary>
    public static bool TryParseNamespace(string? text, out Guid ns)
    {
        ns = Guid.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;
        return Guid.TryParse(text.Trim(), out ns);
    }

    /// <summary>
    /// Compute the RFC 4122 name-based UUID. <paramref name="version"/> is 3 (MD5) or 5 (SHA-1).
    /// Never throws — returns Guid.Empty if something goes wrong.
    /// </summary>
    public static Guid Compute(Guid ns, string? name, int version)
    {
        try
        {
            byte[] nsBytes = ToBigEndianBytes(ns);
            byte[] nameBytes = Encoding.UTF8.GetBytes(name ?? string.Empty);
            byte[] input = new byte[nsBytes.Length + nameBytes.Length];
            Buffer.BlockCopy(nsBytes, 0, input, 0, nsBytes.Length);
            Buffer.BlockCopy(nameBytes, 0, input, nsBytes.Length, nameBytes.Length);

            byte[] hash = version == 3 ? MD5.HashData(input) : SHA1.HashData(input);

            // Take the first 16 bytes as the UUID.
            byte[] u = new byte[16];
            Buffer.BlockCopy(hash, 0, u, 0, 16);

            // Set version (high nibble of byte 6).
            u[6] = (byte)((u[6] & 0x0F) | (version << 4));
            // Set variant (top two bits of byte 8 -> 10).
            u[8] = (byte)((u[8] & 0x3F) | 0x80);

            return FromBigEndianBytes(u);
        }
        catch
        {
            return Guid.Empty;
        }
    }

    /// <summary>Bulk compute: one UUID per non-blank input line. Never throws.</summary>
    public static List<string> ComputeBulk(Guid ns, string? multiline, int version)
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(multiline)) return results;
        try
        {
            string[] lines = multiline.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                results.Add($"{line}  →  {Compute(ns, line, version):D}");
            }
        }
        catch { /* never-throw */ }
        return results;
    }

    /// <summary>.NET stores Guid fields little-endian for the first three groups; emit true RFC big-endian.</summary>
    private static byte[] ToBigEndianBytes(Guid g)
    {
        byte[] b = g.ToByteArray();
        SwapEndianness(b);
        return b;
    }

    private static Guid FromBigEndianBytes(byte[] b)
    {
        byte[] copy = (byte[])b.Clone();
        SwapEndianness(copy);
        return new Guid(copy);
    }

    private static void SwapEndianness(byte[] b)
    {
        (b[0], b[3]) = (b[3], b[0]);
        (b[1], b[2]) = (b[2], b[1]);
        (b[4], b[5]) = (b[5], b[4]);
        (b[6], b[7]) = (b[7], b[6]);
    }
}
