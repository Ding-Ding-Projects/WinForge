using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 字串相似度計算 · String similarity engine — pure managed C#. Computes Levenshtein edit distance,
/// similarity %, Damerau–Levenshtein, Hamming, Jaro–Winkler, longest common substring and longest
/// common subsequence. Guards huge inputs by capping the DP matrix; never throws.
/// </summary>
public static class StringCompareService
{
    /// <summary>Length above which the quadratic DP metrics are skipped to stay responsive.</summary>
    public const int MaxLen = 2000;

    public sealed record Metrics(
        int LenA,
        int LenB,
        bool Truncated,
        int Levenshtein,
        double SimilarityPct,
        int Damerau,
        int Hamming,          // -1 = n/a (different lengths)
        double JaroWinkler,
        int LongestCommonSubstring,
        int LongestCommonSubsequence);

    /// <summary>Prepare the two strings per the options, then compute every metric. Never throws.</summary>
    public static Metrics Compute(string? a, string? b, bool ignoreCase, bool ignoreWhitespace)
    {
        try
        {
            string sa = Normalize(a, ignoreCase, ignoreWhitespace);
            string sb = Normalize(b, ignoreCase, ignoreWhitespace);

            bool truncated = sa.Length > MaxLen || sb.Length > MaxLen;

            int lev, dam, lcSub, lcSeq;
            double sim;
            if (truncated)
            {
                lev = dam = lcSub = lcSeq = -1;
                sim = double.NaN;
            }
            else
            {
                lev = Levenshtein(sa, sb);
                int maxLen = Math.Max(sa.Length, sb.Length);
                sim = maxLen == 0 ? 100.0 : (1.0 - (double)lev / maxLen) * 100.0;
                dam = DamerauLevenshtein(sa, sb);
                lcSub = LongestCommonSubstring(sa, sb);
                lcSeq = LongestCommonSubsequence(sa, sb);
            }

            int ham = sa.Length == sb.Length ? Hamming(sa, sb) : -1;
            double jw = JaroWinkler(sa, sb);

            return new Metrics(sa.Length, sb.Length, truncated, lev, sim, dam, ham, jw, lcSub, lcSeq);
        }
        catch
        {
            return new Metrics(0, 0, false, -1, double.NaN, -1, -1, double.NaN, -1, -1);
        }
    }

    private static string Normalize(string? s, bool ignoreCase, bool ignoreWhitespace)
    {
        s ??= string.Empty;
        if (ignoreWhitespace)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) if (!char.IsWhiteSpace(c)) sb.Append(c);
            s = sb.ToString();
        }
        if (ignoreCase) s = s.ToLowerInvariant();
        return s;
    }

    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    private static int DamerauLevenshtein(string a, string b)
    {
        int la = a.Length, lb = b.Length;
        if (la == 0) return lb;
        if (lb == 0) return la;

        var d = new int[la + 1, lb + 1];
        for (int i = 0; i <= la; i++) d[i, 0] = i;
        for (int j = 0; j <= lb; j++) d[0, j] = j;

        for (int i = 1; i <= la; i++)
        {
            for (int j = 1; j <= lb; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                int min = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    min = Math.Min(min, d[i - 2, j - 2] + 1);
                d[i, j] = min;
            }
        }
        return d[la, lb];
    }

    private static int Hamming(string a, string b)
    {
        int dist = 0;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) dist++;
        return dist;
    }

    private static double JaroWinkler(string a, string b)
    {
        double jaro = Jaro(a, b);
        int prefix = 0;
        int max = Math.Min(4, Math.Min(a.Length, b.Length));
        for (int i = 0; i < max; i++)
        {
            if (a[i] == b[i]) prefix++;
            else break;
        }
        return jaro + prefix * 0.1 * (1.0 - jaro);
    }

    private static double Jaro(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        int matchDistance = Math.Max(a.Length, b.Length) / 2 - 1;
        if (matchDistance < 0) matchDistance = 0;

        var aMatched = new bool[a.Length];
        var bMatched = new bool[b.Length];
        int matches = 0;

        for (int i = 0; i < a.Length; i++)
        {
            int start = Math.Max(0, i - matchDistance);
            int end = Math.Min(i + matchDistance + 1, b.Length);
            for (int j = start; j < end; j++)
            {
                if (bMatched[j] || a[i] != b[j]) continue;
                aMatched[i] = true;
                bMatched[j] = true;
                matches++;
                break;
            }
        }
        if (matches == 0) return 0.0;

        double transpositions = 0;
        int k = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (!aMatched[i]) continue;
            while (!bMatched[k]) k++;
            if (a[i] != b[k]) transpositions++;
            k++;
        }
        transpositions /= 2.0;

        double m = matches;
        return (m / a.Length + m / b.Length + (m - transpositions) / m) / 3.0;
    }

    private static int LongestCommonSubstring(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        int best = 0;
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                if (a[i - 1] == b[j - 1])
                {
                    cur[j] = prev[j - 1] + 1;
                    if (cur[j] > best) best = cur[j];
                }
                else cur[j] = 0;
            }
            (prev, cur) = (cur, prev);
            Array.Clear(cur);
        }
        return best;
    }

    private static int LongestCommonSubsequence(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                cur[j] = a[i - 1] == b[j - 1]
                    ? prev[j - 1] + 1
                    : Math.Max(prev[j], cur[j - 1]);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    /// <summary>Build a copyable plain-text report of the label/value rows. Never throws.</summary>
    public static string BuildReport(IEnumerable<(string Label, string Value)> rows)
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var (label, value) in rows)
                sb.Append(label).Append(": ").AppendLine(value);
            return sb.ToString().TrimEnd();
        }
        catch
        {
            return string.Empty;
        }
    }
}
