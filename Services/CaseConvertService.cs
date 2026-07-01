using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 大小寫／命名轉換 · Case &amp; naming converter — pure managed tokenizer + formatters.
/// Splits input into words on spaces, _, -, ., / and camelCase/PascalCase + digit
/// boundaries, then renders every common programming/writing case. Never throws.
/// </summary>
public static class CaseConvertService
{
    /// <summary>Break arbitrary text into lowercase word tokens. Robust against any input.</summary>
    public static List<string> Tokenize(string? input)
    {
        var words = new List<string>();
        if (string.IsNullOrEmpty(input)) return words;

        try
        {
            var cur = new StringBuilder();
            char prev = '\0';

            void Flush()
            {
                if (cur.Length > 0) { words.Add(cur.ToString().ToLowerInvariant()); cur.Clear(); }
            }

            foreach (char c in input)
            {
                bool sep = c == ' ' || c == '\t' || c == '\r' || c == '\n'
                           || c == '_' || c == '-' || c == '.' || c == '/'
                           || c == '\\' || c == ':';
                if (sep) { Flush(); prev = c; continue; }

                if (!char.IsLetterOrDigit(c))
                {
                    // Drop other punctuation as a boundary too.
                    Flush(); prev = c; continue;
                }

                if (cur.Length > 0)
                {
                    bool prevLower = char.IsLower(prev);
                    bool prevDigit = char.IsDigit(prev);
                    bool curUpper = char.IsUpper(c);
                    bool curDigit = char.IsDigit(c);

                    // camelCase / PascalCase boundary: lower|digit -> Upper
                    if (curUpper && (prevLower || prevDigit)) Flush();
                    // digit boundary: letter <-> digit
                    else if (curDigit != prevDigit) Flush();
                }

                cur.Append(c);
                prev = c;
            }
            Flush();
        }
        catch
        {
            // Fall back to a whitespace split if anything unexpected happens.
            words.Clear();
            foreach (var w in (input ?? string.Empty).Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                words.Add(w.ToLowerInvariant());
        }
        return words;
    }

    private static string Cap(string w)
        => w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w.Substring(1) : string.Empty);

    public static string CamelCase(IReadOnlyList<string> w)
    {
        if (w.Count == 0) return string.Empty;
        var sb = new StringBuilder(w[0]);
        for (int i = 1; i < w.Count; i++) sb.Append(Cap(w[i]));
        return sb.ToString();
    }

    public static string PascalCase(IReadOnlyList<string> w)
    {
        var sb = new StringBuilder();
        foreach (var x in w) sb.Append(Cap(x));
        return sb.ToString();
    }

    public static string SnakeCase(IReadOnlyList<string> w) => string.Join("_", w);
    public static string KebabCase(IReadOnlyList<string> w) => string.Join("-", w);
    public static string ConstantCase(IReadOnlyList<string> w) => string.Join("_", w).ToUpperInvariant();
    public static string DotCase(IReadOnlyList<string> w) => string.Join(".", w);
    public static string PathCase(IReadOnlyList<string> w) => string.Join("/", w);

    public static string TitleCase(IReadOnlyList<string> w)
    {
        var parts = new List<string>(w.Count);
        foreach (var x in w) parts.Add(Cap(x));
        return string.Join(" ", parts);
    }

    public static string TrainCase(IReadOnlyList<string> w)
    {
        var parts = new List<string>(w.Count);
        foreach (var x in w) parts.Add(Cap(x));
        return string.Join("-", parts);
    }

    public static string SentenceCase(IReadOnlyList<string> w)
    {
        if (w.Count == 0) return string.Empty;
        var sb = new StringBuilder(Cap(w[0]));
        for (int i = 1; i < w.Count; i++) { sb.Append(' '); sb.Append(w[i]); }
        return sb.ToString();
    }

    /// <summary>Compute every supported form for the given input. Returns (label-en, label-zh, value).</summary>
    public static List<(string En, string Zh, string Value)> AllForms(string? input)
    {
        var w = Tokenize(input);
        var list = new List<(string, string, string)>
        {
            ("camelCase",      "駝峰式 camelCase",   CamelCase(w)),
            ("PascalCase",     "帕斯卡式 PascalCase", PascalCase(w)),
            ("snake_case",     "蛇形 snake_case",    SnakeCase(w)),
            ("kebab-case",     "烤串 kebab-case",    KebabCase(w)),
            ("CONSTANT_CASE",  "常數式 CONSTANT_CASE", ConstantCase(w)),
            ("Title Case",     "標題式 Title Case",  TitleCase(w)),
            ("Sentence case",  "句子式 Sentence case", SentenceCase(w)),
            ("dot.case",       "點式 dot.case",      DotCase(w)),
            ("path/case",      "路徑式 path/case",   PathCase(w)),
            ("Train-Case",     "火車式 Train-Case",  TrainCase(w)),
        };
        return list;
    }
}
