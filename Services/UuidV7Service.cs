using System;
using System.Security.Cryptography;

namespace WinForge.Services;

/// <summary>
/// UUID v7（時間排序）· RFC 9562 UUIDv7 generator + decoder. Pure managed C#
/// (RandomNumberGenerator + DateTimeOffset). Time-ordered / sortable identifiers:
/// 48-bit Unix-ms timestamp, version 7, 12 random bits, variant (10), 62 random bits.
/// Robust — decoding never throws; generation is deterministic-shaped and safe.
/// </summary>
public static class UuidV7Service
{
    private static long _lastMs = -1;      // monotonic guard
    private static ushort _lastRandA;      // 12-bit counter within the same ms
    private static readonly object _gate = new();

    /// <summary>
    /// Produce one RFC 9562 UUIDv7 as a canonical 8-4-4-4-12 lowercase string.
    /// When <paramref name="monotonic"/> is true, IDs generated within the same
    /// millisecond strictly increase (the 12-bit rand_a field is used as a counter).
    /// </summary>
    public static string NewV7(bool monotonic)
    {
        byte[] b = new byte[16];
        RandomNumberGenerator.Fill(b);

        long ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (ms < 0) ms = 0;

        ushort randA;
        if (monotonic)
        {
            lock (_gate)
            {
                if (ms == _lastMs)
                {
                    // Same millisecond: increment the 12-bit counter. If it would
                    // overflow, nudge the timestamp forward by 1 ms to stay ordered.
                    if (_lastRandA >= 0x0FFF)
                    {
                        ms = _lastMs + 1;
                        randA = (ushort)((b[6] << 8 | b[7]) & 0x0FFF);
                    }
                    else
                    {
                        randA = (ushort)(_lastRandA + 1);
                    }
                }
                else if (ms < _lastMs)
                {
                    // Clock moved backwards: pin to last ms and bump the counter.
                    ms = _lastMs;
                    randA = (ushort)((_lastRandA + 1) & 0x0FFF);
                }
                else
                {
                    randA = (ushort)((b[6] << 8 | b[7]) & 0x0FFF);
                }
                _lastMs = ms;
                _lastRandA = randA;
            }
        }
        else
        {
            randA = (ushort)((b[6] << 8 | b[7]) & 0x0FFF);
        }

        // 48-bit big-endian timestamp
        b[0] = (byte)(ms >> 40);
        b[1] = (byte)(ms >> 32);
        b[2] = (byte)(ms >> 24);
        b[3] = (byte)(ms >> 16);
        b[4] = (byte)(ms >> 8);
        b[5] = (byte)ms;

        // version 7 in high nibble of byte 6, low nibble + byte 7 = 12-bit rand_a
        b[6] = (byte)(0x70 | ((randA >> 8) & 0x0F));
        b[7] = (byte)(randA & 0xFF);

        // variant 10xx in top two bits of byte 8
        b[8] = (byte)(0x80 | (b[8] & 0x3F));

        return Format(b);
    }

    /// <summary>Format 16 bytes as canonical lowercase 8-4-4-4-12.</summary>
    public static string Format(byte[] b)
    {
        string h = Convert.ToHexString(b).ToLowerInvariant();
        return $"{h.Substring(0, 8)}-{h.Substring(8, 4)}-{h.Substring(12, 4)}-{h.Substring(16, 4)}-{h.Substring(20, 12)}";
    }

    /// <summary>Result of decoding a UUID string. <see cref="Ok"/> false = parse failed.</summary>
    public sealed class DecodeResult
    {
        public bool Ok { get; init; }
        public string Error { get; init; } = "";
        public int Version { get; init; }
        public int Variant { get; init; }        // top variant bits value (e.g. 2 = RFC 10xx)
        public string VariantName { get; init; } = "";
        public bool HasTimestamp { get; init; }
        public DateTimeOffset Timestamp { get; init; }
        public string Canonical { get; init; } = "";
    }

    // 100-ns intervals between 1582-10-15 (UUID v1 epoch) and 1970-01-01 (Unix epoch).
    private const long V1EpochTicks = 0x01B21DD213814000;

    /// <summary>
    /// Parse and decode a UUID string. Extracts version/variant, and for v7 the
    /// embedded 48-bit Unix-ms timestamp (v1 timestamps decoded best-effort too).
    /// Never throws — returns Ok=false with a reason on any bad input.
    /// </summary>
    public static DecodeResult Decode(string? input)
    {
        try
        {
            string s = (input ?? string.Empty).Trim();
            if (s.Length == 0)
                return new DecodeResult { Ok = false, Error = "empty" };

            // Accept optional urn: prefix and surrounding braces.
            if (s.StartsWith("urn:uuid:", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(9);
            s = s.Trim().Trim('{', '}').Trim();

            if (!Guid.TryParse(s, out Guid g))
                return new DecodeResult { Ok = false, Error = "notguid" };

            byte[] b = GuidToRfcBytes(g);

            int version = (b[6] >> 4) & 0x0F;
            int variantBits = (b[8] >> 4) & 0x0F; // examine top nibble of the variant byte

            int variantVal;
            string variantName;
            if ((variantBits & 0x8) == 0) { variantVal = 0; variantName = "NCS (0xxx)"; }
            else if ((variantBits & 0x4) == 0) { variantVal = 2; variantName = "RFC 4122/9562 (10xx)"; }
            else if ((variantBits & 0x2) == 0) { variantVal = 6; variantName = "Microsoft (110x)"; }
            else { variantVal = 7; variantName = "Reserved (111x)"; }

            bool hasTs = false;
            DateTimeOffset ts = default;

            if (version == 7)
            {
                long ms = ((long)b[0] << 40) | ((long)b[1] << 32) | ((long)b[2] << 24)
                          | ((long)b[3] << 16) | ((long)b[4] << 8) | b[5];
                try { ts = DateTimeOffset.FromUnixTimeMilliseconds(ms); hasTs = true; }
                catch { hasTs = false; }
            }
            else if (version == 1)
            {
                // v1: 60-bit timestamp in 100-ns since 1582-10-15, split time_low/mid/hi.
                long timeLow = ((long)b[0] << 24) | ((long)b[1] << 16) | ((long)b[2] << 8) | b[3];
                long timeMid = ((long)b[4] << 8) | b[5];
                long timeHi = (((long)b[6] & 0x0F) << 8) | b[7];
                long ticks100 = (timeHi << 48) | (timeMid << 32) | timeLow;
                long unixTicks = (ticks100 - V1EpochTicks) * 100; // .NET ticks = 100 ns
                try { ts = new DateTimeOffset(DateTime.UnixEpoch.Ticks + unixTicks, TimeSpan.Zero); hasTs = true; }
                catch { hasTs = false; }
            }

            return new DecodeResult
            {
                Ok = true,
                Version = version,
                Variant = variantVal,
                VariantName = variantName,
                HasTimestamp = hasTs,
                Timestamp = ts,
                Canonical = Format(b),
            };
        }
        catch (Exception ex)
        {
            return new DecodeResult { Ok = false, Error = ex.GetType().Name };
        }
    }

    /// <summary>
    /// Convert a <see cref="Guid"/> to RFC 4122 network byte order (big-endian for the
    /// first three groups) — .NET stores those fields little-endian in <c>ToByteArray()</c>.
    /// </summary>
    private static byte[] GuidToRfcBytes(Guid g)
    {
        byte[] le = g.ToByteArray();
        return new byte[]
        {
            le[3], le[2], le[1], le[0], // time_low
            le[5], le[4],               // time_mid
            le[7], le[6],               // time_hi_and_version
            le[8], le[9],               // clock_seq
            le[10], le[11], le[12], le[13], le[14], le[15],
        };
    }
}
