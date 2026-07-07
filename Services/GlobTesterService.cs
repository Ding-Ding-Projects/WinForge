using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// Glob 樣式測試器 · Glob-to-regex compiler and matcher — pure managed. Supports
/// * (not crossing /), ** (crossing /), ?, character classes [abc]/[a-z]/[!abc],
/// and brace alternation {a,b,c}. Converts a glob to a .NET regex by hand and runs it.
/// Never throws — <see cref="Compile"/> returns an error message on invalid input.
/// </summary>
public static class GlobTesterService
{
    public sealed class CompileResult
    {
        public bool Ok { get; init; }
        public string Regex { get; init; } = "";        // regex body (no anchors) for display
        public string? ErrorEn { get; init; }
        public string? ErrorZh { get; init; }
        public Regex? Matcher { get; init; }
    }

    /// <summary>
    /// Convert a glob pattern into an anchored regex and compile a matcher.
    /// <paramref name="caseInsensitive"/> adds IgnoreCase; when <paramref name="dotFilesMatch"/>
    /// is false, a leading dot in a path segment is NOT matched by a leading * / ? / ** wildcard.
    /// </summary>
    public static CompileResult Compile(string glob, bool caseInsensitive, bool dotFilesMatch)
    {
        glob ??= "";
        try
        {
            string body = Translate(glob, dotFilesMatch, out string? errEn, out string? errZh);
            if (errEn != null)
                return new CompileResult { Ok = false, ErrorEn = errEn, ErrorZh = errZh };

            var opts = RegexOptions.CultureInvariant;
            if (caseInsensitive) opts |= RegexOptions.IgnoreCase;
            var rx = new Regex("^" + body + "$", opts, TimeSpan.FromSeconds(1));
            return new CompileResult { Ok = true, Regex = "^" + body + "$", Matcher = rx };
        }
        catch (Exception ex)
        {
            return new CompileResult
            {
                Ok = false,
                ErrorEn = "Could not compile pattern: " + ex.Message,
                ErrorZh = "無法編譯樣式：" + ex.Message
            };
        }
    }

    /// <summary>Test a single path against a compiled matcher. Never throws.</summary>
    public static bool IsMatch(Regex? matcher, string path)
    {
        if (matcher == null) return false;
        try { return matcher.IsMatch(path ?? ""); }
        catch { return false; }
    }

    // A leading-wildcard guard so hidden dot-files aren't matched unless opted in.
    private const string NoLeadingDot = "(?![.])";

    private static string Translate(string glob, bool dotFilesMatch, out string? errEn, out string? errZh)
    {
        errEn = null; errZh = null;
        var sb = new StringBuilder();
        int i = 0, n = glob.Length;

        // Tracks whether we're at the start of a path segment (start of string or just after '/').
        bool atSegmentStart = true;

        while (i < n)
        {
            char c = glob[i];
            switch (c)
            {
                case '*':
                    bool doubleStar = i + 1 < n && glob[i + 1] == '*';
                    if (!dotFilesMatch && atSegmentStart) sb.Append(NoLeadingDot);
                    if (doubleStar)
                    {
                        sb.Append(".*");        // ** crosses '/'
                        i += 2;
                    }
                    else
                    {
                        sb.Append("[^/]*");     // * does not cross '/'
                        i++;
                    }
                    atSegmentStart = false;
                    break;

                case '?':
                    if (!dotFilesMatch && atSegmentStart) sb.Append(NoLeadingDot);
                    sb.Append("[^/]");
                    atSegmentStart = false;
                    i++;
                    break;

                case '[':
                    if (!TranslateClass(glob, ref i, sb, out errEn, out errZh))
                        return "";
                    atSegmentStart = false;
                    break;

                case '{':
                    if (!TranslateBrace(glob, ref i, sb, dotFilesMatch, out errEn, out errZh))
                        return "";
                    atSegmentStart = false;
                    break;

                case '}':
                    errEn = "Unbalanced '}' — no matching '{'.";
                    errZh = "'}' 唔對稱 — 搵唔到對應嘅 '{'。";
                    return "";

                case ']':
                    errEn = "Unbalanced ']' — no matching '['.";
                    errZh = "']' 唔對稱 — 搵唔到對應嘅 '['。";
                    return "";

                case '/':
                    sb.Append('/');
                    atSegmentStart = true;
                    i++;
                    break;

                default:
                    sb.Append(EscapeLiteral(c));
                    atSegmentStart = false;
                    i++;
                    break;
            }
        }
        return sb.ToString();
    }

    // [abc] [a-z] [!abc] / [^abc]. Advances i past the closing ']'.
    private static bool TranslateClass(string glob, ref int i, StringBuilder sb, out string? errEn, out string? errZh)
    {
        errEn = null; errZh = null;
        int n = glob.Length;
        int j = i + 1;
        var cls = new StringBuilder("[");

        if (j < n && (glob[j] == '!' || glob[j] == '^')) { cls.Append('^'); j++; }
        // A ']' immediately after the (optional) negation is a literal ].
        if (j < n && glob[j] == ']') { cls.Append("\\]"); j++; }

        bool closed = false;
        for (; j < n; j++)
        {
            char c = glob[j];
            if (c == ']') { closed = true; break; }
            if (c == '\\') { cls.Append("\\\\"); continue; }
            if (c == '[') { cls.Append("\\["); continue; }
            // '-' is kept as-is to allow ranges; other regex-special chars inside a class are escaped.
            if (c == '^') { cls.Append("\\^"); continue; }
            cls.Append(c);
        }

        if (!closed)
        {
            errEn = "Unclosed character class — missing ']'.";
            errZh = "字元類別冇閂 — 唔見咗 ']'。";
            return false;
        }

        cls.Append(']');
        sb.Append(cls);
        i = j + 1;
        return true;
    }

    // {a,b,c} alternation → (?:a|b|c). Nested braces are supported. Commas at depth 0 split.
    private static bool TranslateBrace(string glob, ref int i, StringBuilder sb, bool dotFilesMatch, out string? errEn, out string? errZh)
    {
        errEn = null; errZh = null;
        int n = glob.Length;
        int depth = 0;
        int start = i + 1;
        int j = i;
        var parts = new List<string>();
        var cur = new StringBuilder();

        for (; j < n; j++)
        {
            char c = glob[j];
            if (c == '{') { depth++; if (depth > 1) cur.Append(c); continue; }
            if (c == '}')
            {
                depth--;
                if (depth == 0) { parts.Add(cur.ToString()); break; }
                cur.Append(c);
                continue;
            }
            if (c == ',' && depth == 1) { parts.Add(cur.ToString()); cur.Clear(); continue; }
            cur.Append(c);
        }

        if (depth != 0 || j >= n)
        {
            errEn = "Unbalanced '{' — missing '}'.";
            errZh = "'{' 唔對稱 — 唔見咗 '}'。";
            return false;
        }

        sb.Append("(?:");
        for (int p = 0; p < parts.Count; p++)
        {
            if (p > 0) sb.Append('|');
            // Recursively translate each alternative (they may contain globs).
            string sub = Translate(parts[p], dotFilesMatch, out errEn, out errZh);
            if (errEn != null) return false;
            sb.Append(sub);
        }
        sb.Append(')');
        i = j + 1;
        _ = start;
        return true;
    }

    // Escape a single literal character for regex.
    private static string EscapeLiteral(char c)
    {
        // Regex metacharacters that must be escaped when meant literally.
        if (".$^{[(|)*+?\\".IndexOf(c) >= 0) return "\\" + c;
        return c.ToString();
    }
}
