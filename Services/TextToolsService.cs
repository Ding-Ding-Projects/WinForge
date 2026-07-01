using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 文字工具工作臺 · Text utilities workbench. Pure, side-effect-free string→string transforms plus
/// a tiny text-statistics helper. No I/O, no P/Invoke — just managed .NET. Bilingual UI lives in
/// <see cref="WinForge.Pages.TextToolsModule"/>. Every method is null-safe and never throws.
/// </summary>
public static class TextToolsService
{
    private static readonly string[] NewLines = { "\r\n", "\r", "\n" };

    private static string[] SplitLines(string s) =>
        (s ?? string.Empty).Split(NewLines, StringSplitOptions.None);

    private static string JoinLines(IEnumerable<string> lines) => string.Join("\n", lines);

    // ---- Case transforms ---------------------------------------------------

    public static string Upper(string s) => (s ?? string.Empty).ToUpperInvariant();

    public static string Lower(string s) => (s ?? string.Empty).ToLowerInvariant();

    /// <summary>Title Case each word (culture-invariant).</summary>
    public static string Title(string s)
    {
        s ??= string.Empty;
        var ti = CultureInfo.InvariantCulture.TextInfo;
        // TextInfo.ToTitleCase leaves ALL-CAPS words untouched, so lower first.
        return ti.ToTitleCase(s.ToLowerInvariant());
    }

    /// <summary>tOGGLE cASE — invert the case of every letter.</summary>
    public static string Toggle(string s)
    {
        s ??= string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(char.IsUpper(c) ? char.ToLowerInvariant(c)
                     : char.IsLower(c) ? char.ToUpperInvariant(c)
                     : c);
        return sb.ToString();
    }

    // ---- Whitespace / cleanup ---------------------------------------------

    public static string TrimEachLine(string s) =>
        JoinLines(SplitLines(s).Select(l => l.Trim()));

    public static string RemoveBlankLines(string s) =>
        JoinLines(SplitLines(s).Where(l => l.Trim().Length > 0));

    /// <summary>Collapse runs of spaces/tabs into a single space (keeps line breaks).</summary>
    public static string CollapseSpaces(string s) =>
        JoinLines(SplitLines(s).Select(CollapseRun));

    private static string CollapseRun(string line)
    {
        var sb = new StringBuilder(line.Length);
        bool prevWs = false;
        foreach (char c in line)
        {
            bool ws = c == ' ' || c == '\t';
            if (ws)
            {
                if (!prevWs) sb.Append(' ');
            }
            else sb.Append(c);
            prevWs = ws;
        }
        return sb.ToString();
    }

    // ---- Line ordering -----------------------------------------------------

    public static string SortAsc(string s) =>
        JoinLines(SplitLines(s).OrderBy(l => l, StringComparer.OrdinalIgnoreCase));

    public static string SortDesc(string s) =>
        JoinLines(SplitLines(s).OrderByDescending(l => l, StringComparer.OrdinalIgnoreCase));

    /// <summary>Sort lines by the leading number; non-numeric lines sink to the bottom (stable-ish).</summary>
    public static string SortNumeric(string s)
    {
        var lines = SplitLines(s);
        return JoinLines(lines
            .Select((l, i) => (l, i))
            .OrderBy(t => LeadingNumber(t.l) ?? double.PositiveInfinity)
            .ThenBy(t => t.i)
            .Select(t => t.l));
    }

    private static double? LeadingNumber(string line)
    {
        string t = (line ?? string.Empty).Trim();
        if (t.Length == 0) return null;
        int end = 0;
        if (t[0] == '+' || t[0] == '-') end = 1;
        bool dot = false;
        while (end < t.Length && (char.IsDigit(t[end]) || (t[end] == '.' && !dot)))
        {
            if (t[end] == '.') dot = true;
            end++;
        }
        string head = t.Substring(0, end);
        return double.TryParse(head, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : (double?)null;
    }

    public static string ReverseLines(string s)
    {
        var lines = SplitLines(s).ToArray();
        Array.Reverse(lines);
        return JoinLines(lines);
    }

    /// <summary>Deduplicate lines keeping first occurrence (case-sensitive), preserving order.</summary>
    public static string Deduplicate(string s)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return JoinLines(SplitLines(s).Where(l => seen.Add(l)));
    }

    /// <summary>Cryptographically-seeded Fisher–Yates shuffle of the lines.</summary>
    public static string ShuffleLines(string s)
    {
        var lines = SplitLines(s).ToArray();
        for (int i = lines.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (lines[i], lines[j]) = (lines[j], lines[i]);
        }
        return JoinLines(lines);
    }

    // ---- Character-level ---------------------------------------------------

    public static string ReverseChars(string s)
    {
        s ??= string.Empty;
        var arr = s.ToCharArray();
        Array.Reverse(arr);
        return new string(arr);
    }

    /// <summary>slugify: lowercase, non-alphanumerics → hyphen, collapse & trim hyphens.</summary>
    public static string Slugify(string s)
    {
        s ??= string.Empty;
        var sb = new StringBuilder(s.Length);
        bool prevHyphen = false;
        foreach (char c in s.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                prevHyphen = false;
            }
            else if (!prevHyphen)
            {
                sb.Append('-');
                prevHyphen = true;
            }
        }
        return sb.ToString().Trim('-');
    }

    // ---- Statistics --------------------------------------------------------

    /// <summary>Character, word, line and sentence counts for the live stats card.</summary>
    public readonly struct Stats
    {
        public int Characters { get; init; }
        public int CharactersNoSpaces { get; init; }
        public int Words { get; init; }
        public int Lines { get; init; }
        public int Sentences { get; init; }
        public double AvgWordLength { get; init; }
    }

    public static Stats Analyze(string s)
    {
        s ??= string.Empty;
        int chars = s.Length;
        int noSpace = s.Count(c => !char.IsWhiteSpace(c));

        var words = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        int wordCount = words.Length;
        long wordChars = words.Sum(w => (long)w.Length);

        int lines = s.Length == 0 ? 0 : SplitLines(s).Length;
        int sentences = s.Count(c => c == '.' || c == '!' || c == '?');
        double avg = wordCount == 0 ? 0 : Math.Round((double)wordChars / wordCount, 1);

        return new Stats
        {
            Characters = chars,
            CharactersNoSpaces = noSpace,
            Words = wordCount,
            Lines = lines,
            Sentences = sentences,
            AvgWordLength = avg,
        };
    }
}
