using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 文字差異比對 · Line-level text diff via a bounded LCS. Pure managed, never throws.
/// Produces a sequence of diff lines (context / added / removed) and a unified-diff string.
/// </summary>
public static class TextDiffService
{
    public enum ChangeKind { Unchanged, Added, Removed }

    public sealed class DiffLine
    {
        public ChangeKind Kind { get; init; }
        public string Prefix { get; init; } = " ";
        public string Text { get; init; } = "";
    }

    public sealed class DiffResult
    {
        public List<DiffLine> Lines { get; } = new();
        public int Added { get; set; }
        public int Removed { get; set; }
        public int Unchanged { get; set; }
        public bool Truncated { get; set; }
    }

    // Guard against pathological O(n*m) blow-ups on huge inputs.
    private const long CellBudget = 6_000_000; // ~ up to a few thousand lines each side

    private static string[] SplitLines(string? s)
    {
        if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
        // Normalise CRLF / CR to LF, then split.
        string norm = s.Replace("\r\n", "\n").Replace('\r', '\n');
        return norm.Split('\n');
    }

    private static string Key(string line, bool ignoreWhitespace, bool ignoreCase)
    {
        string k = line;
        if (ignoreWhitespace)
        {
            var sb = new StringBuilder(k.Length);
            foreach (char c in k) if (!char.IsWhiteSpace(c)) sb.Append(c);
            k = sb.ToString();
        }
        if (ignoreCase) k = k.ToUpperInvariant();
        return k;
    }

    /// <summary>Compute a line-level diff. Never throws; returns a best-effort result on error.</summary>
    public static DiffResult Compute(string? a, string? b, bool ignoreWhitespace, bool ignoreCase)
    {
        var result = new DiffResult();
        try
        {
            string[] left = SplitLines(a);
            string[] right = SplitLines(b);

            // If the LCS table would be too large, fall back to a simple all-removed/all-added diff.
            if ((long)left.Length * right.Length > CellBudget)
            {
                result.Truncated = true;
                foreach (var l in left) Emit(result, ChangeKind.Removed, l);
                foreach (var r in right) Emit(result, ChangeKind.Added, r);
                return result;
            }

            string[] lk = new string[left.Length];
            string[] rk = new string[right.Length];
            for (int i = 0; i < left.Length; i++) lk[i] = Key(left[i], ignoreWhitespace, ignoreCase);
            for (int j = 0; j < right.Length; j++) rk[j] = Key(right[j], ignoreWhitespace, ignoreCase);

            int n = left.Length, m = right.Length;
            // LCS length table.
            var dp = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    dp[i, j] = lk[i] == rk[j]
                        ? dp[i + 1, j + 1] + 1
                        : Math.Max(dp[i + 1, j], dp[i, j + 1]);

            int x = 0, y = 0;
            while (x < n && y < m)
            {
                if (lk[x] == rk[y]) { Emit(result, ChangeKind.Unchanged, left[x]); x++; y++; }
                else if (dp[x + 1, y] >= dp[x, y + 1]) { Emit(result, ChangeKind.Removed, left[x]); x++; }
                else { Emit(result, ChangeKind.Added, right[y]); y++; }
            }
            while (x < n) { Emit(result, ChangeKind.Removed, left[x]); x++; }
            while (y < m) { Emit(result, ChangeKind.Added, right[y]); y++; }
        }
        catch
        {
            // Best-effort: leave whatever was accumulated.
        }
        return result;
    }

    private static void Emit(DiffResult r, ChangeKind kind, string text)
    {
        switch (kind)
        {
            case ChangeKind.Added: r.Added++; r.Lines.Add(new DiffLine { Kind = kind, Prefix = "+", Text = text }); break;
            case ChangeKind.Removed: r.Removed++; r.Lines.Add(new DiffLine { Kind = kind, Prefix = "-", Text = text }); break;
            default: r.Unchanged++; r.Lines.Add(new DiffLine { Kind = kind, Prefix = " ", Text = text }); break;
        }
    }

    /// <summary>Emit a standard unified-style diff (space/+/- prefixes). Never throws.</summary>
    public static string ToUnifiedDiff(DiffResult result)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append("--- A\n+++ B\n");
            foreach (var line in result.Lines)
                sb.Append(line.Prefix).Append(line.Text).Append('\n');
            return sb.ToString();
        }
        catch { return ""; }
    }
}
