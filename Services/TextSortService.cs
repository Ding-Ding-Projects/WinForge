using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace WinForge.Services;

/// <summary>
/// 行排序同去重 · Line sort &amp; dedupe engine — pure managed, never-throw text line operations:
/// A→Z / Z→A / natural sort, case-insensitive compare, dedupe (optional trim-before-compare),
/// reverse, cryptographic shuffle, remove blank lines, trim each. No side-effects, no I/O.
/// </summary>
public static class TextSortService
{
    /// <summary>How lines should be ordered.</summary>
    public enum SortMode { None, Ascending, Descending, Natural }

    /// <summary>Result of a transform: the output text plus counts for the UI.</summary>
    public readonly record struct Result(string Text, int LinesIn, int LinesOut, int DuplicatesRemoved);

    /// <summary>Split text into lines, tolerant of \r\n, \r and \n. Never throws.</summary>
    public static List<string> SplitLines(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(text)) return list;
        try
        {
            int start = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n' || c == '\r')
                {
                    list.Add(text.Substring(start, i - start));
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    start = i + 1;
                }
            }
            if (start <= text.Length) list.Add(text.Substring(start));
            // A trailing newline yields a final empty string; keep behaviour simple by
            // dropping a single trailing empty entry so round-tripping is intuitive.
            if (list.Count > 0 && list[^1].Length == 0) list.RemoveAt(list.Count - 1);
        }
        catch { /* never throw */ }
        return list;
    }

    /// <summary>
    /// Apply the requested operations to <paramref name="input"/> and return the joined output
    /// (using "\r\n") together with in/out line counts and how many duplicates were removed.
    /// Order of operations: trim → drop-blank → dedupe → sort/reverse/shuffle. Never throws.
    /// </summary>
    public static Result Transform(
        string? input,
        SortMode sort,
        bool caseInsensitive,
        bool removeDuplicates,
        bool trimBeforeCompare,
        bool reverse,
        bool shuffle,
        bool removeBlank,
        bool trimEach)
    {
        try
        {
            var lines = SplitLines(input);
            int linesIn = lines.Count;

            if (trimEach)
                for (int i = 0; i < lines.Count; i++)
                    lines[i] = (lines[i] ?? string.Empty).Trim();

            if (removeBlank)
                lines.RemoveAll(l => string.IsNullOrWhiteSpace(l));

            int dupsRemoved = 0;
            if (removeDuplicates)
            {
                var cmp = caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
                var seen = new HashSet<string>(cmp);
                var kept = new List<string>(lines.Count);
                foreach (var l in lines)
                {
                    string key = trimBeforeCompare ? (l ?? string.Empty).Trim() : (l ?? string.Empty);
                    if (seen.Add(key)) kept.Add(l ?? string.Empty);
                    else dupsRemoved++;
                }
                lines = kept;
            }

            switch (sort)
            {
                case SortMode.Ascending:
                    lines.Sort((a, b) => Compare(a, b, caseInsensitive));
                    break;
                case SortMode.Descending:
                    lines.Sort((a, b) => Compare(b, a, caseInsensitive));
                    break;
                case SortMode.Natural:
                    lines.Sort((a, b) => NaturalCompare(a, b, caseInsensitive));
                    break;
            }

            if (reverse) lines.Reverse();
            if (shuffle) Shuffle(lines);

            return new Result(string.Join("\r\n", lines), linesIn, lines.Count, dupsRemoved);
        }
        catch
        {
            // Absolute fallback: echo input unchanged.
            return new Result(input ?? string.Empty, 0, 0, 0);
        }
    }

    private static int Compare(string? a, string? b, bool ci) =>
        string.Compare(a ?? string.Empty, b ?? string.Empty,
            ci ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Natural / human sort: runs of digits compare numerically, so "file2" &lt; "file10".
    /// Leading zeros and arbitrary-length numbers handled. Never throws.
    /// </summary>
    private static int NaturalCompare(string? sa, string? sb, bool ci)
    {
        string a = sa ?? string.Empty, b = sb ?? string.Empty;
        int ia = 0, ib = 0;
        while (ia < a.Length && ib < b.Length)
        {
            char ca = a[ia], cb = b[ib];
            bool da = char.IsDigit(ca), db = char.IsDigit(cb);
            if (da && db)
            {
                // Compare two digit-runs numerically (skip leading zeros, then by length, then digits).
                int sa0 = ia, sb0 = ib;
                while (ia < a.Length && a[ia] == '0') ia++;
                while (ib < b.Length && b[ib] == '0') ib++;
                int na = ia, nb = ib;
                while (na < a.Length && char.IsDigit(a[na])) na++;
                while (nb < b.Length && char.IsDigit(b[nb])) nb++;
                int lenA = na - ia, lenB = nb - ib;
                if (lenA != lenB) return lenA - lenB;
                for (int k = 0; k < lenA; k++)
                {
                    int d = a[ia + k] - b[ib + k];
                    if (d != 0) return d;
                }
                // Equal numeric value — more leading zeros sorts first for stability.
                int zerosA = ia - sa0, zerosB = ib - sb0;
                if (zerosA != zerosB) return zerosA - zerosB;
                ia = na; ib = nb;
            }
            else
            {
                char xa = ci ? char.ToUpperInvariant(ca) : ca;
                char xb = ci ? char.ToUpperInvariant(cb) : cb;
                if (xa != xb) return xa - xb;
                ia++; ib++;
            }
        }
        return (a.Length - ia) - (b.Length - ib);
    }

    /// <summary>Cryptographically-random Fisher–Yates shuffle (RandomNumberGenerator). Never throws.</summary>
    private static void Shuffle(List<string> list)
    {
        try
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
        catch { /* never throw */ }
    }
}
