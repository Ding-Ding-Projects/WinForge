using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 行工具 · Line Tools — pure-managed text/line transforms operating on multiline input.
/// Every method is robust (never throws), returns a new string, and treats null as empty.
/// Randomness (shuffle) uses <see cref="RandomNumberGenerator"/>. No I/O, no side-effects.
/// </summary>
public static class LineToolsService
{
    // Split the input into logical lines, tolerant of \r\n / \r / \n.
    private static List<string> Split(string? text)
    {
        if (string.IsNullOrEmpty(text)) return new List<string>();
        try { return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList(); }
        catch { return new List<string>(); }
    }

    private static string Join(IEnumerable<string> lines)
    {
        try { return string.Join("\n", lines); } catch { return string.Empty; }
    }

    /// <summary>Live counts for the status line: lines, characters (incl. newlines) and words.</summary>
    public static (int Lines, int Chars, int Words) Count(string? text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return (0, 0, 0);
            int lines = Split(text).Count;
            int chars = text.Length;
            int words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
            return (lines, chars, words);
        }
        catch { return (0, 0, 0); }
    }

    /// <summary>Number each line. <paramref name="paren"/> true → "1) ", false → "1. ".</summary>
    public static string NumberLines(string? text, bool paren)
    {
        try
        {
            var lines = Split(text);
            for (int i = 0; i < lines.Count; i++)
                lines[i] = paren ? $"{i + 1}) {lines[i]}" : $"{i + 1}. {lines[i]}";
            return Join(lines);
        }
        catch { return text ?? string.Empty; }
    }

    /// <summary>Strip a leading "12. ", "12) ", "12 " or "12\t" style number from each line.</summary>
    public static string RemoveLineNumbers(string? text)
    {
        try
        {
            var lines = Split(text);
            for (int i = 0; i < lines.Count; i++)
            {
                string s = lines[i];
                int p = 0;
                while (p < s.Length && (s[p] == ' ' || s[p] == '\t')) p++;
                int d = p;
                while (d < s.Length && char.IsDigit(s[d])) d++;
                if (d > p) // at least one digit
                {
                    int q = d;
                    if (q < s.Length && (s[q] == '.' || s[q] == ')' || s[q] == ':')) q++;
                    // require a separator (punctuation and/or whitespace) so we don't eat plain numbers
                    if (q < s.Length && (s[q] == ' ' || s[q] == '\t'))
                    {
                        while (q < s.Length && (s[q] == ' ' || s[q] == '\t')) q++;
                        lines[i] = s.Substring(q);
                    }
                    else if (q > d) // had punctuation but no trailing space (e.g. "12.")
                    {
                        lines[i] = s.Substring(q);
                    }
                }
            }
            return Join(lines);
        }
        catch { return text ?? string.Empty; }
    }

    public static string AddPrefix(string? text, string? prefix)
    {
        try
        {
            prefix ??= string.Empty;
            return Join(Split(text).Select(l => prefix + l));
        }
        catch { return text ?? string.Empty; }
    }

    public static string AddSuffix(string? text, string? suffix)
    {
        try
        {
            suffix ??= string.Empty;
            return Join(Split(text).Select(l => l + suffix));
        }
        catch { return text ?? string.Empty; }
    }

    public static string WrapQuotes(string? text)
    {
        try { return Join(Split(text).Select(l => "\"" + l + "\"")); }
        catch { return text ?? string.Empty; }
    }

    /// <summary>Join all lines into one line using the delimiter (defaults to ", ").</summary>
    public static string JoinLines(string? text, string? delimiter)
    {
        try
        {
            string d = delimiter ?? string.Empty;
            return string.Join(d, Split(text));
        }
        catch { return text ?? string.Empty; }
    }

    /// <summary>Split the whole input on the delimiter, one piece per output line.</summary>
    public static string SplitOn(string? text, string? delimiter)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (string.IsNullOrEmpty(delimiter)) return text; // nothing to split on
            return Join(text.Split(new[] { delimiter }, StringSplitOptions.None));
        }
        catch { return text ?? string.Empty; }
    }

    public static string ReverseChars(string? text)
    {
        try
        {
            return Join(Split(text).Select(l =>
            {
                var arr = l.ToCharArray();
                Array.Reverse(arr);
                return new string(arr);
            }));
        }
        catch { return text ?? string.Empty; }
    }

    public static string Sort(string? text)
    {
        try
        {
            var lines = Split(text);
            lines.Sort(StringComparer.OrdinalIgnoreCase);
            return Join(lines);
        }
        catch { return text ?? string.Empty; }
    }

    public static string ReverseOrder(string? text)
    {
        try
        {
            var lines = Split(text);
            lines.Reverse();
            return Join(lines);
        }
        catch { return text ?? string.Empty; }
    }

    /// <summary>Remove duplicate lines, keeping first occurrence (case-insensitive).</summary>
    public static string Deduplicate(string? text)
    {
        try
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var outp = new List<string>();
            foreach (var l in Split(text))
                if (seen.Add(l)) outp.Add(l);
            return Join(outp);
        }
        catch { return text ?? string.Empty; }
    }

    public static string RemoveEmpty(string? text)
    {
        try { return Join(Split(text).Where(l => l.Trim().Length > 0)); }
        catch { return text ?? string.Empty; }
    }

    public static string TrimLines(string? text)
    {
        try { return Join(Split(text).Select(l => l.Trim())); }
        catch { return text ?? string.Empty; }
    }

    /// <summary>Fisher–Yates shuffle using a cryptographically strong RNG.</summary>
    public static string Shuffle(string? text)
    {
        try
        {
            var lines = Split(text);
            for (int i = lines.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (lines[i], lines[j]) = (lines[j], lines[i]);
            }
            return Join(lines);
        }
        catch { return text ?? string.Empty; }
    }
}
