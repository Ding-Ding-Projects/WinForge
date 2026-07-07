using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 字元地圖 · Unicode character explorer. Pure-managed helper: builds lists of
/// <see cref="CharInfo"/> for a named block, resolves a codepoint from a search
/// string, and computes the per-glyph encodings (UTF-8 / UTF-16 / HTML entity).
/// Robust: never throws; skips the surrogate range U+D800–U+DFFF; caps lists.
/// </summary>
public static class CharMapService
{
    /// <summary>Hard cap on how many rows any populated list may contain.</summary>
    public const int MaxItems = 512;

    /// <summary>One row in the character grid / details card.</summary>
    public sealed class CharInfo
    {
        public string Glyph { get; init; } = string.Empty;   // the rendered character(s)
        public string Code { get; init; } = string.Empty;    // "U+XXXX"
        public string Dec { get; init; } = string.Empty;     // decimal codepoint
        public string Utf8 { get; init; } = string.Empty;    // space-separated hex bytes
        public string Utf16 { get; init; } = string.Empty;   // space-separated hex code units
        public string Html { get; init; } = string.Empty;    // "&#xXXXX;"
        public string Name { get; init; } = string.Empty;    // best-effort name / category label

        public int CodePoint { get; init; }
    }

    /// <summary>A named Unicode range shown in the block picker.</summary>
    public sealed class Block
    {
        public string En { get; init; } = string.Empty;
        public string Zh { get; init; } = string.Empty;
        public int Start { get; init; }
        public int End { get; init; }
    }

    /// <summary>The blocks offered by the module, in display order.</summary>
    public static IReadOnlyList<Block> Blocks { get; } = new List<Block>
    {
        new() { En = "Basic Latin",           Zh = "基本拉丁字母",   Start = 0x0020, End = 0x007E },
        new() { En = "Latin-1 Supplement",    Zh = "拉丁字母補充",   Start = 0x00A0, End = 0x00FF },
        new() { En = "General Punctuation",   Zh = "一般標點",       Start = 0x2000, End = 0x206F },
        new() { En = "Currency Symbols",      Zh = "貨幣符號",       Start = 0x20A0, End = 0x20BF },
        new() { En = "Arrows",                Zh = "箭嘴",           Start = 0x2190, End = 0x21FF },
        new() { En = "Math Operators",        Zh = "數學運算符",     Start = 0x2200, End = 0x22FF },
        new() { En = "Box Drawing",           Zh = "製表符",         Start = 0x2500, End = 0x257F },
        new() { En = "Geometric Shapes",      Zh = "幾何圖形",       Start = 0x25A0, End = 0x25FF },
        new() { En = "Emoji (sample)",        Zh = "表情符號（樣本）", Start = 0x1F600, End = 0x1F64F },
        new() { En = "CJK (sample)",          Zh = "中日韓（樣本）", Start = 0x4E00, End = 0x4E80 },
    };

    /// <summary>Build the rows for a codepoint range. Skips surrogates; caps at <see cref="MaxItems"/>.</summary>
    public static List<CharInfo> BuildRange(int start, int end)
    {
        var list = new List<CharInfo>();
        try
        {
            if (end < start) (start, end) = (end, start);
            for (int cp = start; cp <= end && list.Count < MaxItems; cp++)
            {
                var info = Describe(cp);
                if (info != null) list.Add(info);
            }
        }
        catch { /* never throw */ }
        return list;
    }

    /// <summary>
    /// Build a <see cref="CharInfo"/> for one codepoint, or null when it cannot be
    /// represented (surrogate range, out of Unicode range, or conversion failure).
    /// </summary>
    public static CharInfo? Describe(int cp)
    {
        try
        {
            if (cp < 0 || cp > 0x10FFFF) return null;
            if (cp >= 0xD800 && cp <= 0xDFFF) return null; // lone surrogate — skip

            string glyph = char.ConvertFromUtf32(cp);

            byte[] utf8 = Encoding.UTF8.GetBytes(glyph);
            var utf8Hex = new StringBuilder();
            for (int i = 0; i < utf8.Length; i++)
            {
                if (i > 0) utf8Hex.Append(' ');
                utf8Hex.Append(utf8[i].ToString("X2", CultureInfo.InvariantCulture));
            }

            var utf16Hex = new StringBuilder();
            for (int i = 0; i < glyph.Length; i++)
            {
                if (i > 0) utf16Hex.Append(' ');
                utf16Hex.Append(((int)glyph[i]).ToString("X4", CultureInfo.InvariantCulture));
            }

            return new CharInfo
            {
                CodePoint = cp,
                Glyph = glyph,
                Code = "U+" + cp.ToString(cp <= 0xFFFF ? "X4" : "X6", CultureInfo.InvariantCulture),
                Dec = cp.ToString(CultureInfo.InvariantCulture),
                Utf8 = utf8Hex.ToString(),
                Utf16 = utf16Hex.ToString(),
                Html = "&#x" + cp.ToString(cp <= 0xFFFF ? "X4" : "X6", CultureInfo.InvariantCulture) + ";",
                Name = BestEffortName(cp, glyph),
            };
        }
        catch { return null; }
    }

    /// <summary>
    /// Best-effort human label. No full Unicode Character Database is bundled, so we
    /// fall back to the Unicode general category (e.g. "Uppercase Letter") plus the code.
    /// </summary>
    public static string BestEffortName(int cp, string glyph)
    {
        try
        {
            UnicodeCategory cat = char.IsHighSurrogate(glyph, 0) || glyph.Length > 1
                ? CharUnicodeInfo.GetUnicodeCategory(glyph, 0)
                : CharUnicodeInfo.GetUnicodeCategory((char)cp);
            return CategoryLabel(cat);
        }
        catch
        {
            return "U+" + cp.ToString("X4", CultureInfo.InvariantCulture);
        }
    }

    private static string CategoryLabel(UnicodeCategory cat) => cat switch
    {
        UnicodeCategory.UppercaseLetter => "Uppercase Letter",
        UnicodeCategory.LowercaseLetter => "Lowercase Letter",
        UnicodeCategory.TitlecaseLetter => "Titlecase Letter",
        UnicodeCategory.ModifierLetter => "Modifier Letter",
        UnicodeCategory.OtherLetter => "Letter",
        UnicodeCategory.NonSpacingMark => "Non-spacing Mark",
        UnicodeCategory.SpacingCombiningMark => "Combining Mark",
        UnicodeCategory.EnclosingMark => "Enclosing Mark",
        UnicodeCategory.DecimalDigitNumber => "Digit",
        UnicodeCategory.LetterNumber => "Letter Number",
        UnicodeCategory.OtherNumber => "Number",
        UnicodeCategory.SpaceSeparator => "Space",
        UnicodeCategory.LineSeparator => "Line Separator",
        UnicodeCategory.ParagraphSeparator => "Paragraph Separator",
        UnicodeCategory.Control => "Control",
        UnicodeCategory.Format => "Format",
        UnicodeCategory.Surrogate => "Surrogate",
        UnicodeCategory.PrivateUse => "Private Use",
        UnicodeCategory.ConnectorPunctuation => "Connector Punctuation",
        UnicodeCategory.DashPunctuation => "Dash Punctuation",
        UnicodeCategory.OpenPunctuation => "Open Punctuation",
        UnicodeCategory.ClosePunctuation => "Close Punctuation",
        UnicodeCategory.InitialQuotePunctuation => "Quote Punctuation",
        UnicodeCategory.FinalQuotePunctuation => "Quote Punctuation",
        UnicodeCategory.OtherPunctuation => "Punctuation",
        UnicodeCategory.MathSymbol => "Math Symbol",
        UnicodeCategory.CurrencySymbol => "Currency Symbol",
        UnicodeCategory.ModifierSymbol => "Modifier Symbol",
        UnicodeCategory.OtherSymbol => "Symbol",
        _ => "Other",
    };

    /// <summary>
    /// Parse a search string as a codepoint. Accepts "U+2764", "2764", "0x2764"
    /// (hex) or "#10084" (decimal). Returns -1 when the input is plain text.
    /// </summary>
    public static int ParseCodePoint(string? query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query)) return -1;
            string s = query.Trim();

            if (s.StartsWith("#", StringComparison.Ordinal))
            {
                string dec = s.Substring(1).Trim();
                return int.TryParse(dec, NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) && InRange(d) ? d : -1;
            }

            if (s.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            else if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(2);
            else if (s.StartsWith("&#x", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(3).TrimEnd(';');

            s = s.Trim();
            if (s.Length == 0) return -1;

            // Pure-hex string => treat as a hex codepoint.
            if (IsHex(s) &&
                int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int h) && InRange(h))
                return h;

            return -1;
        }
        catch { return -1; }
    }

    private static bool IsHex(string s)
    {
        foreach (char c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return s.Length > 0;
    }

    private static bool InRange(int cp) => cp >= 0 && cp <= 0x10FFFF && !(cp >= 0xD800 && cp <= 0xDFFF);
}
