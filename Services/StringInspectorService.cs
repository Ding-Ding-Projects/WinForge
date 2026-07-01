using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 字串檢查器 · String Inspector — pure-managed text analysis and transforms.
/// Every method is robust (never throws): on bad input it returns the original
/// text or an empty result. No I/O, no process launch, no external deps.
/// </summary>
public static class StringInspectorService
{
    /// <summary>One code-point row for the ListView (classic {Binding}, so public props).</summary>
    public sealed class CodePointRow
    {
        public string CodePoint { get; set; } = "";   // U+XXXX
        public string Display { get; set; } = "";      // the char (or a caret label)
        public string Category { get; set; } = "";     // Unicode general category
        public string Name { get; set; } = "";         // best-effort description
    }

    /// <summary>Aggregate statistics for the summary card.</summary>
    public sealed class Stats
    {
        public int Chars { get; set; }
        public int Utf8Bytes { get; set; }
        public int Utf16Bytes { get; set; }
        public int Utf32Bytes { get; set; }
        public int CodePoints { get; set; }
        public int Graphemes { get; set; }
        public int Words { get; set; }
        public int Lines { get; set; }
    }

    /// <summary>Compute all statistics. Never throws.</summary>
    public static Stats Analyze(string? text)
    {
        var s = new Stats();
        try
        {
            text ??= "";
            s.Chars = text.Length;
            s.Utf8Bytes = Encoding.UTF8.GetByteCount(text);
            s.Utf16Bytes = Encoding.Unicode.GetByteCount(text);
            s.Utf32Bytes = Encoding.UTF32.GetByteCount(text);
            s.CodePoints = CountCodePoints(text);
            s.Graphemes = CountGraphemes(text);
            s.Words = CountWords(text);
            s.Lines = CountLines(text);
        }
        catch { /* leave partial/zeroed stats */ }
        return s;
    }

    private static int CountCodePoints(string text)
    {
        int n = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1])) i++;
            n++;
        }
        return n;
    }

    private static int CountGraphemes(string text)
    {
        try
        {
            int n = 0;
            var e = StringInfo.GetTextElementEnumerator(text);
            while (e.MoveNext()) n++;
            return n;
        }
        catch { return text.Length; }
    }

    private static int CountWords(string text)
    {
        int n = 0; bool inWord = false;
        foreach (char c in text)
        {
            if (char.IsWhiteSpace(c)) inWord = false;
            else if (!inWord) { inWord = true; n++; }
        }
        return n;
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0) return 0;
        int n = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n') n++;
            else if (text[i] == '\r')
            {
                n++;
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
            }
        }
        return n;
    }

    /// <summary>Enumerate code points (capped) for the ListView. Never throws.</summary>
    public static List<CodePointRow> CodePoints(string? text, int max = 4096)
    {
        var rows = new List<CodePointRow>();
        try
        {
            text ??= "";
            for (int i = 0; i < text.Length && rows.Count < max; i++)
            {
                int cp;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                else cp = text[i];

                rows.Add(new CodePointRow
                {
                    CodePoint = "U+" + cp.ToString(cp > 0xFFFF ? "X6" : "X4", CultureInfo.InvariantCulture),
                    Display = Describe(cp),
                    Category = CategoryOf(cp),
                    Name = ""
                });
            }
        }
        catch { /* return whatever we collected */ }
        return rows;
    }

    private static string Describe(int cp)
    {
        if (cp == '\n') return "\\n";
        if (cp == '\r') return "\\r";
        if (cp == '\t') return "\\t";
        if (cp == ' ') return "␠";
        if (cp < 0x20 || cp == 0x7F) return "⟨ctrl⟩";
        try { return char.ConvertFromUtf32(cp); } catch { return "?"; }
    }

    private static string CategoryOf(int cp)
    {
        try
        {
            UnicodeCategory cat = cp <= 0xFFFF
                ? CharUnicodeInfo.GetUnicodeCategory((char)cp)
                : CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(cp), 0);
            return cat.ToString();
        }
        catch { return "Unknown"; }
    }

    // ---- Transforms (all robust; return original on failure) --------------

    public static string Reverse(string? text)
    {
        try
        {
            text ??= "";
            var elements = new List<string>();
            var e = StringInfo.GetTextElementEnumerator(text);
            while (e.MoveNext()) elements.Add((string)e.Current);
            elements.Reverse();
            return string.Concat(elements);
        }
        catch { return text ?? ""; }
    }

    public static string Normalize(string? text, NormalizationForm form)
    {
        try { return (text ?? "").Normalize(form); }
        catch { return text ?? ""; }
    }

    /// <summary>Escape control chars and non-ASCII as \n \t \uXXXX / \UXXXXXXXX plus \\ \".</summary>
    public static string Escape(string? text)
    {
        try
        {
            text ??= "";
            var sb = new StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\0': sb.Append("\\0"); break;
                    default:
                        if (c < 0x20 || c > 0x7E)
                            sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
        catch { return text ?? ""; }
    }

    /// <summary>Reverse <see cref="Escape"/>: handle \n \t \r \0 \\ \" \uXXXX \UXXXXXXXX.</summary>
    public static string Unescape(string? text)
    {
        try
        {
            text ??= "";
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\\' || i + 1 >= text.Length) { sb.Append(c); continue; }
                char n = text[++i];
                switch (n)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '0': sb.Append('\0'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    case '\'': sb.Append('\''); break;
                    case 'u':
                        if (i + 4 < text.Length && int.TryParse(text.Substring(i + 1, 4),
                                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int u))
                        { sb.Append((char)u); i += 4; }
                        else { sb.Append('\\').Append(n); }
                        break;
                    case 'U':
                        if (i + 8 < text.Length && int.TryParse(text.Substring(i + 1, 8),
                                NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int U))
                        {
                            try { sb.Append(char.ConvertFromUtf32(U)); } catch { sb.Append('�'); }
                            i += 8;
                        }
                        else { sb.Append('\\').Append(n); }
                        break;
                    default: sb.Append('\\').Append(n); break;
                }
            }
            return sb.ToString();
        }
        catch { return text ?? ""; }
    }

    /// <summary>Remove combining marks (é → e). NFD, drop NonSpacingMark, NFC.</summary>
    public static string StripDiacritics(string? text)
    {
        try
        {
            text ??= "";
            string d = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (char c in d)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
        catch { return text ?? ""; }
    }

    /// <summary>Drop every char outside U+0000–U+007F.</summary>
    public static string RemoveNonAscii(string? text)
    {
        try
        {
            text ??= "";
            var sb = new StringBuilder(text.Length);
            foreach (char c in text) if (c <= 0x7F) sb.Append(c);
            return sb.ToString();
        }
        catch { return text ?? ""; }
    }
}
