using System;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// GUID / ULID / 隨機 ID 產生器 · Pure-managed identifier generator.
/// GUIDs (via <see cref="Guid"/>), time-sortable ULIDs (Crockford base32) and
/// nano-style URL-safe random strings — all random material from
/// <see cref="RandomNumberGenerator"/> (never System.Random). No shelling out.
/// </summary>
public static class GuidGenService
{
    /// <summary>Crockford's base32 alphabet used by ULID (excludes I, L, O, U).</summary>
    private const string CrockfordAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    /// <summary>URL-safe alphabet for nano-style ids (64 chars → unbiased with GetInt32).</summary>
    private const string NanoAlphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    /// <summary>
    /// Produce a GUID string in the requested <see cref="Guid.ToString(string)"/> format
    /// ("N", "D", "B", "P" or "X"), optionally upper-cased.
    /// </summary>
    public static string NewGuid(string format, bool upper)
    {
        string f = string.IsNullOrWhiteSpace(format) ? "D" : format.Trim();
        string s = Guid.NewGuid().ToString(f);
        return upper ? s.ToUpperInvariant() : s;
    }

    /// <summary>Generate <paramref name="count"/> GUIDs, one per line (clamped 1–1000).</summary>
    public static string BulkGuids(int count, string format, bool upper)
    {
        if (count < 1) count = 1;
        if (count > 1000) count = 1000;
        var sb = new StringBuilder(count * 40);
        for (int i = 0; i < count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(NewGuid(format, upper));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Generate a 26-char ULID: 48-bit millisecond timestamp + 80 bits of CSPRNG
    /// randomness, Crockford-base32 encoded. Lexicographically sortable by time.
    /// </summary>
    public static string NewUlid()
    {
        long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (ms < 0) ms = 0;

        // 16 bytes total: 6 timestamp + 10 random = 128 bits, of which we encode 130 → 26 chars.
        byte[] rand = new byte[10];
        RandomNumberGenerator.Fill(rand);

        var chars = new char[26];

        // --- Timestamp: 48 bits → 10 base32 chars (indices 0..9). ---
        chars[0] = CrockfordAlphabet[(int)((ms >> 45) & 0x1F)];
        chars[1] = CrockfordAlphabet[(int)((ms >> 40) & 0x1F)];
        chars[2] = CrockfordAlphabet[(int)((ms >> 35) & 0x1F)];
        chars[3] = CrockfordAlphabet[(int)((ms >> 30) & 0x1F)];
        chars[4] = CrockfordAlphabet[(int)((ms >> 25) & 0x1F)];
        chars[5] = CrockfordAlphabet[(int)((ms >> 20) & 0x1F)];
        chars[6] = CrockfordAlphabet[(int)((ms >> 15) & 0x1F)];
        chars[7] = CrockfordAlphabet[(int)((ms >> 10) & 0x1F)];
        chars[8] = CrockfordAlphabet[(int)((ms >> 5) & 0x1F)];
        chars[9] = CrockfordAlphabet[(int)(ms & 0x1F)];

        // --- Randomness: 80 bits → 16 base32 chars (indices 10..25). ---
        chars[10] = CrockfordAlphabet[(rand[0] & 0xFF) >> 3];
        chars[11] = CrockfordAlphabet[((rand[0] << 2) | (rand[1] >> 6)) & 0x1F];
        chars[12] = CrockfordAlphabet[(rand[1] >> 1) & 0x1F];
        chars[13] = CrockfordAlphabet[((rand[1] << 4) | (rand[2] >> 4)) & 0x1F];
        chars[14] = CrockfordAlphabet[((rand[2] << 1) | (rand[3] >> 7)) & 0x1F];
        chars[15] = CrockfordAlphabet[(rand[3] >> 2) & 0x1F];
        chars[16] = CrockfordAlphabet[((rand[3] << 3) | (rand[4] >> 5)) & 0x1F];
        chars[17] = CrockfordAlphabet[rand[4] & 0x1F];
        chars[18] = CrockfordAlphabet[(rand[5] & 0xFF) >> 3];
        chars[19] = CrockfordAlphabet[((rand[5] << 2) | (rand[6] >> 6)) & 0x1F];
        chars[20] = CrockfordAlphabet[(rand[6] >> 1) & 0x1F];
        chars[21] = CrockfordAlphabet[((rand[6] << 4) | (rand[7] >> 4)) & 0x1F];
        chars[22] = CrockfordAlphabet[((rand[7] << 1) | (rand[8] >> 7)) & 0x1F];
        chars[23] = CrockfordAlphabet[(rand[8] >> 2) & 0x1F];
        chars[24] = CrockfordAlphabet[((rand[8] << 3) | (rand[9] >> 5)) & 0x1F];
        chars[25] = CrockfordAlphabet[rand[9] & 0x1F];

        return new string(chars);
    }

    /// <summary>
    /// Generate a nano-style random string of <paramref name="length"/> chars (clamped 4–64)
    /// from a URL-safe 64-char alphabet, using unbiased <see cref="RandomNumberGenerator.GetInt32(int)"/>.
    /// </summary>
    public static string NewNanoId(int length)
    {
        if (length < 4) length = 4;
        if (length > 64) length = 64;
        var chars = new char[length];
        for (int i = 0; i < length; i++)
            chars[i] = NanoAlphabet[RandomNumberGenerator.GetInt32(NanoAlphabet.Length)];
        return new string(chars);
    }

    /// <summary>Result of inspecting a GUID string.</summary>
    public readonly record struct GuidInfo(string Hex, int Version, string Variant);

    /// <summary>
    /// Parse any accepted GUID form and describe it: the 16 bytes as hex (big-endian /
    /// RFC-4122 field order), the version nibble and the variant. Throws
    /// <see cref="FormatException"/> on invalid input (caller surfaces the error).
    /// </summary>
    public static GuidInfo Inspect(string text)
    {
        var g = Guid.Parse((text ?? string.Empty).Trim());

        // Guid.ToByteArray() is little-endian in the first three fields; reorder to
        // canonical RFC-4122 byte order so the hex matches the printed GUID.
        byte[] le = g.ToByteArray();
        byte[] be =
        {
            le[3], le[2], le[1], le[0],
            le[5], le[4],
            le[7], le[6],
            le[8], le[9], le[10], le[11], le[12], le[13], le[14], le[15],
        };

        var hex = new StringBuilder(47);
        for (int i = 0; i < be.Length; i++)
        {
            if (i > 0) hex.Append(' ');
            hex.Append(be[i].ToString("X2"));
        }

        int version = (be[6] >> 4) & 0x0F;

        // Variant is read from the top bits of byte 8 (the "N" nibble).
        int variantBits = (be[8] >> 5) & 0x07;
        string variant =
            (variantBits & 0b100) == 0 ? "NCS (0xxx)" :
            (variantBits & 0b110) == 0b100 ? "RFC 4122 (10xx)" :
            (variantBits & 0b111) == 0b110 ? "Microsoft (110x)" :
            "Reserved (111x)";

        return new GuidInfo(hex.ToString(), version, variant);
    }
}
