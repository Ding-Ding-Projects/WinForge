using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// Markdown 目錄產生器 · Markdown table-of-contents generator. Pure managed C#: parses ATX headings
/// (# … ######) from Markdown, ignoring anything inside fenced ``` / ~~~ code blocks, and emits a nested
/// TOC of GitHub-style anchor links. Never throws — malformed input yields best-effort output.
/// </summary>
public static class MarkdownTocService
{
    /// <summary>A parsed ATX heading.</summary>
    public sealed class Heading
    {
        public int Level;       // 1..6
        public string Title = "";
        public string Slug = "";
    }

    public sealed class TocOptions
    {
        public int MinLevel = 1;         // clamp 1..6
        public int MaxLevel = 6;         // clamp 1..6
        public bool IncludeH1 = true;    // when false, H1 headings are dropped
        public bool Ordered;             // ordered list (1.) vs bullets (-)
    }

    /// <summary>Result of a generation pass.</summary>
    public sealed class TocResult
    {
        public string Markdown = "";
        public int HeadingCount;
    }

    /// <summary>
    /// Parse ATX headings from <paramref name="markdown"/>, skipping fenced code blocks. Robust: returns an
    /// empty list on null/blank input and swallows any unexpected error.
    /// </summary>
    public static List<Heading> ParseHeadings(string? markdown)
    {
        var result = new List<Heading>();
        if (string.IsNullOrEmpty(markdown)) return result;

        try
        {
            var used = new Dictionary<string, int>(StringComparer.Ordinal);
            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            bool inFence = false;
            char fenceChar = '`';
            int fenceLen = 0;

            foreach (string raw in lines)
            {
                string line = raw ?? "";
                string trimmed = line.TrimStart();

                // Detect fenced code-block delimiters (``` or ~~~, 3+ of the same char).
                if (IsFence(trimmed, out char fc, out int flen))
                {
                    if (!inFence)
                    {
                        inFence = true; fenceChar = fc; fenceLen = flen;
                    }
                    else if (fc == fenceChar && flen >= fenceLen)
                    {
                        inFence = false;
                    }
                    continue;
                }

                if (inFence) continue;

                // ATX heading: 0-3 leading spaces, 1-6 '#', then a space (or end of line).
                int lead = line.Length - trimmed.Length;
                if (lead > 3) continue;
                if (trimmed.Length == 0 || trimmed[0] != '#') continue;

                int hashes = 0;
                while (hashes < trimmed.Length && trimmed[hashes] == '#') hashes++;
                if (hashes < 1 || hashes > 6) continue;
                if (hashes < trimmed.Length && trimmed[hashes] != ' ' && trimmed[hashes] != '\t') continue;

                string title = trimmed.Substring(hashes).Trim();
                // Strip an optional closing run of '#'.
                title = title.TrimEnd();
                int end = title.Length;
                while (end > 0 && title[end - 1] == '#') end--;
                if (end < title.Length && (end == 0 || title[end - 1] == ' '))
                    title = title.Substring(0, end).TrimEnd();

                if (title.Length == 0) title = "";
                string plain = StripInlineMarkdown(title);

                var h = new Heading
                {
                    Level = hashes,
                    Title = plain,
                    Slug = MakeSlug(plain, used)
                };
                result.Add(h);
            }
        }
        catch
        {
            // best-effort — return whatever we gathered
        }

        return result;
    }

    /// <summary>Build the TOC Markdown from raw input + options. Never throws.</summary>
    public static TocResult Generate(string? markdown, TocOptions? options)
    {
        var res = new TocResult();
        try
        {
            var opts = options ?? new TocOptions();
            int min = Clamp(opts.MinLevel, 1, 6);
            int max = Clamp(opts.MaxLevel, 1, 6);
            if (min > max) (min, max) = (max, min);

            var headings = ParseHeadings(markdown);
            var sb = new StringBuilder();
            int count = 0;

            foreach (var h in headings)
            {
                if (h.Level < min || h.Level > max) continue;
                if (!opts.IncludeH1 && h.Level == 1) continue;

                int indentLevels = h.Level - min;
                if (indentLevels < 0) indentLevels = 0;
                string indent = new string(' ', indentLevels * 2);
                string marker = opts.Ordered ? "1." : "-";
                string title = string.IsNullOrEmpty(h.Title) ? "(untitled)" : h.Title;

                sb.Append(indent).Append(marker).Append(' ')
                  .Append('[').Append(EscapeLinkText(title)).Append("](#").Append(h.Slug).Append(')')
                  .Append('\n');
                count++;
            }

            res.Markdown = sb.ToString().TrimEnd('\n');
            res.HeadingCount = count;
        }
        catch
        {
            res.Markdown = "";
            res.HeadingCount = 0;
        }
        return res;
    }

    // --- helpers ---

    private static bool IsFence(string trimmed, out char fenceChar, out int len)
    {
        fenceChar = '`';
        len = 0;
        if (trimmed.Length < 3) return false;
        char c = trimmed[0];
        if (c != '`' && c != '~') return false;
        int n = 0;
        while (n < trimmed.Length && trimmed[n] == c) n++;
        if (n < 3) return false;
        fenceChar = c;
        len = n;
        return true;
    }

    /// <summary>GitHub-style slug: lowercase, spaces→hyphens, punctuation stripped, de-duplicated.</summary>
    public static string MakeSlug(string title, Dictionary<string, int> used)
    {
        var sb = new StringBuilder(title.Length);
        foreach (char raw in title.ToLowerInvariant())
        {
            char ch = raw;
            if (ch == ' ' || ch == '\t')
                sb.Append('-');
            else if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                sb.Append(ch);
            // else: punctuation stripped
        }
        string baseSlug = sb.ToString();

        if (used == null) return baseSlug;
        if (!used.TryGetValue(baseSlug, out int seen))
        {
            used[baseSlug] = 0;
            return baseSlug;
        }
        seen++;
        used[baseSlug] = seen;
        return baseSlug + "-" + seen;
    }

    /// <summary>Strip a small set of inline Markdown emphasis/code/link syntax for the visible title.</summary>
    private static string StripInlineMarkdown(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        // Links [text](url) -> text
        var sb = new StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '[')
            {
                int close = s.IndexOf(']', i + 1);
                if (close > i && close + 1 < s.Length && s[close + 1] == '(')
                {
                    int paren = s.IndexOf(')', close + 1);
                    if (paren > close)
                    {
                        sb.Append(s, i + 1, close - i - 1);
                        i = paren + 1;
                        continue;
                    }
                }
            }
            if (c == '*' || c == '_' || c == '`')
            {
                i++;
                continue;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString().Trim();
    }

    private static string EscapeLinkText(string s)
        => (s ?? "").Replace("]", "\\]").Replace("[", "\\[");

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
}
