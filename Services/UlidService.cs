using System;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// ULID + Snowflake toolkit · ULID 同 Snowflake 工具 — pure managed generation/decoding.
/// ULID = 48-bit ms Unix timestamp + 80-bit randomness, encoded as 26 chars of Crockford Base32.
/// Snowflake = 64-bit id: [timestamp | worker/process bits | sequence]. All never-throw helpers.
/// </summary>
public static class UlidService
{
    // Crockford Base32 alphabet (excludes I, L, O, U to avoid ambiguity).
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly int[] Lookup = BuildLookup();

    private static int[] BuildLookup()
    {
        var map = new int[128];
        for (int i = 0; i < map.Length; i++) map[i] = -1;
        for (int i = 0; i < Alphabet.Length; i++)
        {
            char c = Alphabet[i];
            map[char.ToUpperInvariant(c)] = i;
        }
        // Crockford aliases: I/L -> 1, O -> 0. (U is intentionally invalid.)
        map['I'] = 1; map['i'] = 1;
        map['L'] = 1; map['l'] = 1;
        map['O'] = 0; map['o'] = 0;
        return map;
    }

    // --- ULID generation -----------------------------------------------------

    private static byte[]? _lastRandom;   // 10 bytes of randomness from the last monotonic gen
    private static long _lastMs = -1;

    /// <summary>
    /// Generate <paramref name="count"/> ULIDs (one per returned array entry). When
    /// <paramref name="monotonic"/> is true, ids created within the same millisecond
    /// increment the previous randomness by 1 so they sort after each other.
    /// </summary>
    public static string[] Generate(int count, bool monotonic)
    {
        if (count < 1) count = 1;
        if (count > 100000) count = 100000;
        var result = new string[count];
        for (int i = 0; i < count; i++)
        {
            long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (ms < 0) ms = 0;
            byte[] rand = new byte[10];

            if (monotonic && ms == _lastMs && _lastRandom != null)
            {
                Array.Copy(_lastRandom, rand, 10);
                IncrementBigEndian(rand); // may overflow-wrap; acceptable for a dev tool
            }
            else
            {
                RandomNumberGenerator.Fill(rand);
            }

            _lastMs = ms;
            _lastRandom = (byte[])rand.Clone();
            result[i] = Encode(ms, rand);
        }
        return result;
    }

    private static void IncrementBigEndian(byte[] b)
    {
        for (int i = b.Length - 1; i >= 0; i--)
        {
            if (b[i] != 0xFF) { b[i]++; return; }
            b[i] = 0;
        }
    }

    /// <summary>Encode a 48-bit ms timestamp + 10 random bytes as a 26-char Crockford Base32 ULID.</summary>
    private static string Encode(long ms, byte[] random)
    {
        // Assemble the 128-bit value as 16 bytes: 6 bytes timestamp + 10 bytes randomness.
        byte[] bytes = new byte[16];
        for (int i = 0; i < 6; i++)
            bytes[5 - i] = (byte)((ms >> (8 * i)) & 0xFF);
        Array.Copy(random, 0, bytes, 6, 10);

        // 128 bits -> 26 base32 chars (130 bits, top 2 bits zero-padded).
        var chars = new char[26];
        int bitBuffer = 0, bitCount = 0, outIdx = 0;
        // Pad to the left with 2 zero bits so the first char uses only the low bits.
        bitBuffer = 0; bitCount = 2;
        foreach (byte by in bytes)
        {
            bitBuffer = (bitBuffer << 8) | by;
            bitCount += 8;
            while (bitCount >= 5)
            {
                bitCount -= 5;
                int idx = (bitBuffer >> bitCount) & 0x1F;
                chars[outIdx++] = Alphabet[idx];
            }
        }
        return new string(chars);
    }

    // --- ULID decoding -------------------------------------------------------

    public readonly struct UlidParts
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public long TimestampMs { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public string RandomnessHex { get; init; }
    }

    /// <summary>Decode a 26-char ULID into its timestamp and 80-bit randomness. Never throws.</summary>
    public static UlidParts DecodeUlid(string? input)
    {
        try
        {
            string s = (input ?? string.Empty).Trim();
            if (s.Length != 26)
                return new UlidParts { Ok = false, Error = "len" };

            // Decode 26 chars -> 130 bits; keep the low 128 bits.
            byte[] bytes = new byte[16];
            int bitBuffer = 0, bitCount = 0, outIdx = 0;
            // The first char only contributes 3 meaningful bits (top 2 padding).
            foreach (char c in s)
            {
                if (c >= 128) return new UlidParts { Ok = false, Error = "char" };
                int v = Lookup[c];
                if (v < 0) return new UlidParts { Ok = false, Error = "char" };
                bitBuffer = (bitBuffer << 5) | v;
                bitCount += 5;
                if (bitCount >= 8)
                {
                    bitCount -= 8;
                    if (outIdx < 16)
                        bytes[outIdx++] = (byte)((bitBuffer >> bitCount) & 0xFF);
                }
            }

            long ms = 0;
            for (int i = 0; i < 6; i++)
                ms = (ms << 8) | bytes[i];

            var sb = new StringBuilder(20);
            for (int i = 6; i < 16; i++) sb.Append(bytes[i].ToString("X2"));

            return new UlidParts
            {
                Ok = true,
                TimestampMs = ms,
                Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ms),
                RandomnessHex = sb.ToString(),
            };
        }
        catch (Exception ex)
        {
            return new UlidParts { Ok = false, Error = ex.Message };
        }
    }

    // --- Snowflake decoding --------------------------------------------------

    public readonly struct SnowflakeParts
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public long TimestampMs { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public long WorkerId { get; init; }
        public long ProcessId { get; init; }
        public long Sequence { get; init; }
    }

    /// <summary>
    /// Decode a 64-bit Snowflake id relative to <paramref name="epochMs"/> (a Unix-ms epoch).
    /// Layout: bits 63..22 = timestamp offset, 21..17 = worker, 16..12 = process, 11..0 = sequence.
    /// Never throws.
    /// </summary>
    public static SnowflakeParts DecodeSnowflake(string? input, long epochMs)
    {
        try
        {
            string s = (input ?? string.Empty).Trim();
            if (s.Length == 0) return new SnowflakeParts { Ok = false, Error = "empty" };
            if (!ulong.TryParse(s, out ulong id))
                return new SnowflakeParts { Ok = false, Error = "parse" };

            long timePart = (long)(id >> 22);
            long worker = (long)((id & 0x3E0000UL) >> 17);
            long process = (long)((id & 0x1F000UL) >> 12);
            long sequence = (long)(id & 0xFFFUL);

            long ms = epochMs + timePart;
            DateTimeOffset ts;
            try { ts = DateTimeOffset.FromUnixTimeMilliseconds(ms); }
            catch { return new SnowflakeParts { Ok = false, Error = "range" }; }

            return new SnowflakeParts
            {
                Ok = true,
                TimestampMs = ms,
                Timestamp = ts,
                WorkerId = worker,
                ProcessId = process,
                Sequence = sequence,
            };
        }
        catch (Exception ex)
        {
            return new SnowflakeParts { Ok = false, Error = ex.Message };
        }
    }
}
