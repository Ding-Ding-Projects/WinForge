using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 隨機工具箱 · Randomizer toolkit — all randomness comes from a cryptographically strong,
/// unbiased source (<see cref="RandomNumberGenerator.GetInt32"/>). No System.Random anywhere.
/// Pure managed; never throws for the callers (they guard input), returns simple results.
/// </summary>
public static class RandomizerService
{
    /// <summary>Unbiased integer in [minInclusive, maxInclusive].</summary>
    public static int Next(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive) (minInclusive, maxInclusive) = (maxInclusive, minInclusive);
        long span = (long)maxInclusive - minInclusive + 1;
        if (span <= 1) return minInclusive;
        long offset = NextOffset(span);
        return (int)((long)minInclusive + offset);
    }

    /// <summary>Generate <paramref name="count"/> integers in [min,max]. When <paramref name="unique"/>,
    /// no value repeats (requires the range to be wide enough).</summary>
    public static List<int> Integers(int min, int max, int count, bool unique)
    {
        if (max < min) (min, max) = (max, min);
        var result = new List<int>(Math.Max(0, count));
        if (count <= 0) return result;

        long span = (long)max - min + 1;
        if (unique && span >= count)
        {
            // Partial Fisher–Yates over a virtual [min..max] range using a dictionary swap map.
            var swap = new Dictionary<long, long>();
            for (int i = 0; i < count; i++)
            {
                long j = i + NextOffset(span - i);
                long vi = swap.TryGetValue(i, out var a) ? a : i;
                long vj = swap.TryGetValue(j, out var b) ? b : j;
                result.Add((int)((long)min + vj));
                swap[j] = vi;
            }
        }
        else
        {
            for (int i = 0; i < count; i++) result.Add(Next(min, max));
        }
        return result;
    }

    /// <summary>Uniform offset in [0, exclusiveUpperBound) for every inclusive Int32 span.</summary>
    private static long NextOffset(long exclusiveUpperBound)
    {
        if (exclusiveUpperBound <= 1) return 0;

        // Int32 can span 2^32 distinct values, while GetInt32 only exposes an Int32-sized
        // exclusive upper bound. Sample a UInt32 and use rejection sampling to keep every
        // reachable range unbiased (including [int.MinValue, int.MaxValue]).
        const ulong UInt32Range = 1UL << 32;
        ulong bound = (ulong)exclusiveUpperBound;
        ulong limit = UInt32Range - UInt32Range % bound;
        Span<byte> bytes = stackalloc byte[sizeof(uint)];
        ulong sample;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            sample = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }
        while (sample >= limit);

        return (long)(sample % bound);
    }

    /// <summary>Flip a fair coin. true = heads.</summary>
    public static bool CoinFlip() => RandomNumberGenerator.GetInt32(0, 2) == 0;

    public sealed class DiceResult
    {
        public bool Ok;
        public string? Error;          // localization key hint: "empty" or "bad"
        public int Count;
        public int Sides;
        public int Modifier;
        public List<int> Rolls = new();
        public int Total;
    }

    /// <summary>Parse and roll dice notation like "2d6", "1d20+3", "4d8-2", "d20".
    /// On bad input returns <see cref="DiceResult.Ok"/> = false with an <see cref="DiceResult.Error"/> hint.</summary>
    public static DiceResult RollDice(string? spec)
    {
        var r = new DiceResult();
        if (string.IsNullOrWhiteSpace(spec)) { r.Error = "empty"; return r; }

        string s = spec.Trim().ToLowerInvariant().Replace(" ", "");
        int dIdx = s.IndexOf('d');
        if (dIdx < 0) { r.Error = "bad"; return r; }

        string countPart = s.Substring(0, dIdx);
        string rest = s.Substring(dIdx + 1);
        if (rest.Length == 0) { r.Error = "bad"; return r; }

        int count = 1;
        if (countPart.Length > 0 && !int.TryParse(countPart, out count)) { r.Error = "bad"; return r; }

        int modifier = 0;
        int signIdx = rest.IndexOfAny(new[] { '+', '-' });
        string sidesPart = rest;
        if (signIdx >= 0)
        {
            sidesPart = rest.Substring(0, signIdx);
            string modPart = rest.Substring(signIdx);
            if (!int.TryParse(modPart, out modifier)) { r.Error = "bad"; return r; }
        }

        if (!int.TryParse(sidesPart, out int sides)) { r.Error = "bad"; return r; }
        if (count <= 0 || count > 1000 || sides <= 0 || sides > 1_000_000) { r.Error = "bad"; return r; }

        // Dice totals are stored as Int32. Reject a modifier whose possible result range would
        // overflow instead of occasionally wrapping negative for an otherwise valid notation.
        long minTotal = (long)count + modifier;
        long maxTotal = (long)count * sides + modifier;
        if (minTotal < int.MinValue || maxTotal > int.MaxValue) { r.Error = "bad"; return r; }

        r.Ok = true;
        r.Count = count;
        r.Sides = sides;
        r.Modifier = modifier;
        long total = 0;
        for (int i = 0; i < count; i++)
        {
            int roll = Next(1, sides);
            r.Rolls.Add(roll);
            total += roll;
        }
        r.Total = (int)(total + modifier);
        return r;
    }

    /// <summary>Pick one item from the list (unbiased). Returns null if empty.</summary>
    public static string? PickOne(IReadOnlyList<string> items)
    {
        if (items == null || items.Count == 0) return null;
        return items[RandomNumberGenerator.GetInt32(0, items.Count)];
    }

    /// <summary>Fisher–Yates shuffle (unbiased) — returns a new shuffled copy.</summary>
    public static List<string> Shuffle(IReadOnlyList<string> items)
    {
        var a = new List<string>(items ?? Array.Empty<string>());
        for (int i = a.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(0, i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }
        return a;
    }

    /// <summary>Split multiline text into trimmed, non-empty lines.</summary>
    public static List<string> SplitLines(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return list;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string t = raw.Trim();
            if (t.Length > 0) list.Add(t);
        }
        return list;
    }

    public static string Join(IEnumerable<int> values)
    {
        var sb = new StringBuilder();
        foreach (var v in values)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(v);
        }
        return sb.ToString();
    }
}
