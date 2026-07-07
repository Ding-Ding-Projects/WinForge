using System;
using System.Collections.Generic;
using System.Numerics;

namespace WinForge.Services;

/// <summary>
/// 數字序列產生器 · Number-sequence generator — pure managed C# (System.Numerics.BigInteger where
/// helpful) producing arithmetic / geometric / Fibonacci / prime / range / squares / cubes /
/// triangular / powers sequences. Never throws; a hard cap keeps memory sane.
/// </summary>
public static class NumSeqService
{
    /// <summary>Hard upper bound on how many terms any generator will emit.</summary>
    public const int MaxCount = 100000;

    private static int Clamp(long n) => n < 0 ? 0 : (n > MaxCount ? MaxCount : (int)n);

    /// <summary>a, a+d, a+2d, … (count terms).</summary>
    public static List<BigInteger> Arithmetic(BigInteger start, BigInteger step, long count)
    {
        var list = new List<BigInteger>();
        int c = Clamp(count);
        BigInteger v = start;
        for (int i = 0; i < c; i++) { list.Add(v); v += step; }
        return list;
    }

    /// <summary>a, a·r, a·r², … (count terms).</summary>
    public static List<BigInteger> Geometric(BigInteger start, BigInteger ratio, long count)
    {
        var list = new List<BigInteger>();
        int c = Clamp(count);
        BigInteger v = start;
        for (int i = 0; i < c; i++) { list.Add(v); v *= ratio; }
        return list;
    }

    /// <summary>0, 1, 1, 2, 3, 5, … (n terms).</summary>
    public static List<BigInteger> Fibonacci(long count)
    {
        var list = new List<BigInteger>();
        int c = Clamp(count);
        BigInteger a = 0, b = 1;
        for (int i = 0; i < c; i++) { list.Add(a); (a, b) = (b, a + b); }
        return list;
    }

    /// <summary>First <paramref name="count"/> prime numbers.</summary>
    public static List<BigInteger> PrimesFirst(long count)
    {
        var list = new List<BigInteger>();
        int c = Clamp(count);
        if (c == 0) return list;
        long candidate = 2;
        while (list.Count < c)
        {
            if (IsPrime(candidate)) list.Add(candidate);
            candidate++;
            if (candidate < 2) break; // overflow guard
        }
        return list;
    }

    /// <summary>All primes ≤ <paramref name="limit"/> (also capped by MaxCount).</summary>
    public static List<BigInteger> PrimesUpTo(long limit)
    {
        var list = new List<BigInteger>();
        if (limit < 2) return list;
        for (long n = 2; n <= limit; n++)
        {
            if (IsPrime(n)) list.Add(n);
            if (list.Count >= MaxCount) break;
        }
        return list;
    }

    private static bool IsPrime(long n)
    {
        if (n < 2) return false;
        if (n < 4) return true;
        if (n % 2 == 0) return false;
        for (long i = 3; i <= (long)Math.Sqrt(n) + 1; i += 2)
            if (n % i == 0) return false;
        return true;
    }

    /// <summary>start, start±step, … up to (and including) end.</summary>
    public static List<BigInteger> Range(BigInteger start, BigInteger end, BigInteger step)
    {
        var list = new List<BigInteger>();
        if (step == 0) return list;
        if (start <= end && step < 0) return list;
        if (start > end && step > 0) return list;
        BigInteger v = start;
        while ((step > 0 && v <= end) || (step < 0 && v >= end))
        {
            list.Add(v);
            v += step;
            if (list.Count >= MaxCount) break;
        }
        return list;
    }

    /// <summary>1, 4, 9, 16, … (n terms).</summary>
    public static List<BigInteger> Squares(long count)
    {
        var list = new List<BigInteger>();
        int c = Clamp(count);
        for (int i = 1; i <= c; i++) list.Add((BigInteger)i * i);
        return list;
    }

    /// <summary>1, 8, 27, 64, … (n terms).</summary>
    public static List<BigInteger> Cubes(long count)
    {
        var list = new List<BigInteger>();
        int c = Clamp(count);
        for (int i = 1; i <= c; i++) list.Add((BigInteger)i * i * i);
        return list;
    }

    /// <summary>1, 3, 6, 10, 15, … (n terms).</summary>
    public static List<BigInteger> Triangular(long count)
    {
        var list = new List<BigInteger>();
        int c = Clamp(count);
        BigInteger t = 0;
        for (int i = 1; i <= c; i++) { t += i; list.Add(t); }
        return list;
    }

    /// <summary>base⁰, base¹, base², … (count terms).</summary>
    public static List<BigInteger> Powers(BigInteger baseValue, long count)
    {
        var list = new List<BigInteger>();
        int c = Clamp(count);
        BigInteger v = 1;
        for (int i = 0; i < c; i++) { list.Add(v); v *= baseValue; }
        return list;
    }

    /// <summary>Join a sequence with the chosen separator.</summary>
    public static string Format(IReadOnlyList<BigInteger> seq, string separator)
    {
        if (seq == null || seq.Count == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < seq.Count; i++)
        {
            if (i > 0) sb.Append(separator);
            sb.Append(seq[i].ToString());
        }
        return sb.ToString();
    }
}
