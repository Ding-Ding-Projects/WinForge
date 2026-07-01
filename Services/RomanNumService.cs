using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 羅馬數字轉換 · Roman-numeral converter. Pure managed, never throws — every public method
/// returns a small result struct describing success/failure plus a bilingual reason key.
///
/// Standard range: 1..3999 with the classic subtractive forms (IV, IX, XL, XC, CD, CM).
///
/// Extended (vinculum) scheme for 4000..3,999,999:
///   A bar over a numeral multiplies it by 1000. We emit the overline using the Unicode
///   COMBINING OVERLINE (U+0305) after each barred letter, e.g. M̄ = 1000×1000 = 1,000,000.
///   So a value is split into (millions-thousands part) and (0..999 remainder): the high part
///   is written with barred letters (up to M̄M̄M̄ = 3,000,000, plus C̄..) and the low part with
///   ordinary letters. Parsing accepts either U+0305 overlines OR the ASCII fallback where a
///   barred letter is written as the letter followed by an apostrophe-like '_' is NOT used —
///   we accept only the real overline on input for round-trip fidelity, but also tolerate the
///   common "(x)" parenthetical ×1000 convention: e.g. (I)V, (MMM)CMXCIX.
/// </summary>
public static class RomanNumService
{
    public const int StandardMax = 3999;
    public const int ExtendedMax = 3_999_999;

    public const char Overline = '̅'; // COMBINING OVERLINE

    public readonly struct ToRomanResult
    {
        public bool Ok { get; init; }
        public string Roman { get; init; }
        public string Breakdown { get; init; } // e.g. "M + CM + XC + IV"
        public string ReasonEn { get; init; }
        public string ReasonZh { get; init; }
    }

    public readonly struct ToNumberResult
    {
        public bool Ok { get; init; }
        public long Value { get; init; }
        public string Breakdown { get; init; }
        public string ReasonEn { get; init; }
        public string ReasonZh { get; init; }
    }

    private static readonly (int Value, string Sym)[] StdTable =
    {
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I"),
    };

    /// <summary>Convert an integer to Roman. When <paramref name="allowExtended"/> is false the range
    /// is 1..3999; when true, 1..3,999,999 using vinculum (overline ×1000). Never throws.</summary>
    public static ToRomanResult ToRoman(long n, bool allowExtended)
    {
        try
        {
            long max = allowExtended ? ExtendedMax : StandardMax;
            if (n < 1 || n > max)
            {
                return new ToRomanResult
                {
                    Ok = false,
                    ReasonEn = $"Enter a whole number from 1 to {max:N0}.",
                    ReasonZh = $"請輸入 1 至 {max:N0} 之間嘅整數。",
                };
            }

            var parts = new List<string>();
            var sb = new StringBuilder();

            if (allowExtended && n >= 4000)
            {
                long high = n / 1000;         // thousands and above
                long low = n % 1000;          // 0..999
                // Build the "high" part with the standard table, then bar every letter (×1000).
                string highRoman = BuildStandard(high, parts, barred: true);
                sb.Append(highRoman);
                if (low > 0)
                    sb.Append(BuildStandard(low, parts, barred: false));
            }
            else
            {
                sb.Append(BuildStandard(n, parts, barred: false));
            }

            return new ToRomanResult
            {
                Ok = true,
                Roman = sb.ToString(),
                Breakdown = string.Join(" + ", parts),
            };
        }
        catch (Exception ex)
        {
            return new ToRomanResult
            {
                Ok = false,
                ReasonEn = "Could not convert: " + ex.Message,
                ReasonZh = "轉換失敗：" + ex.Message,
            };
        }
    }

    // Greedy build using the standard subtractive table. When barred, each emitted letter gets
    // an overline appended (multiplying its value ×1000) and breakdown entries reflect that.
    private static string BuildStandard(long value, List<string> breakdown, bool barred)
    {
        var sb = new StringBuilder();
        foreach (var (v, sym) in StdTable)
        {
            while (value >= v)
            {
                value -= v;
                string piece = barred ? Bar(sym) : sym;
                sb.Append(piece);
                breakdown.Add(piece);
            }
        }
        return sb.ToString();
    }

    private static string Bar(string sym)
    {
        var sb = new StringBuilder(sym.Length * 2);
        foreach (char c in sym)
        {
            sb.Append(c);
            sb.Append(Overline);
        }
        return sb.ToString();
    }

    /// <summary>Parse Roman → number. Validates well-formedness strictly (rejects IIII, VV, IC, IL,
    /// XM, etc.) by re-encoding and requiring an exact canonical round-trip. Accepts overline
    /// vinculum (U+0305) and the "(x)" ×1000 parenthetical convention. Never throws.</summary>
    public static ToNumberResult ToNumber(string? input, bool allowExtended)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input))
                return Fail("Type a Roman numeral to convert.", "輸入羅馬數字嚟轉換。");

            string raw = input.Trim();

            // 1) Normalise the "(x)" ×1000 convention into overlined form so we have one code path.
            string normalized = ExpandParentheses(raw, out bool parenErr);
            if (parenErr)
                return Fail("Unbalanced or empty parentheses.", "括號唔對稱或者係空嘅。");

            // 2) Split into barred (letter+overline) and plain letters, compute value.
            //    We accumulate a canonical numeric value then re-encode and compare for validity.
            if (!TryScan(normalized, out long value, out string whyEn, out string whyZh))
                return Fail(whyEn, whyZh);

            if (value < 1)
                return Fail("Result is not a positive number.", "結果唔係正整數。");

            long max = allowExtended ? ExtendedMax : StandardMax;
            if (value > max)
                return Fail(
                    allowExtended ? $"Above the extended maximum ({ExtendedMax:N0})." : $"Above {StandardMax} — enable Extended for larger values.",
                    allowExtended ? $"超過擴充上限（{ExtendedMax:N0}）。" : $"超過 {StandardMax} — 開啟「擴充」先可以更大。");

            // 3) Canonical round-trip check: the ONLY way a numeral is well-formed is if it re-encodes
            //    to exactly the same (normalised, overline) form. This rejects IIII, VV, IC, LC, ...
            var re = ToRoman(value, allowExtended: true);
            if (!re.Ok || !string.Equals(re.Roman, normalized, StringComparison.Ordinal))
            {
                return Fail(
                    $"Malformed Roman numeral — the canonical form of {value:N0} is \"{(re.Ok ? re.Roman : "?")}\".",
                    $"羅馬數字寫法唔正確 — {value:N0} 嘅標準寫法係「{(re.Ok ? re.Roman : "?")}」。");
            }

            return new ToNumberResult
            {
                Ok = true,
                Value = value,
                Breakdown = string.Join(" + ", re.Breakdown.Split(new[] { " + " }, StringSplitOptions.None)),
            };
        }
        catch (Exception ex)
        {
            return Fail("Could not parse: " + ex.Message, "解析失敗：" + ex.Message);
        }
    }

    // Expand "(X)Y" → each char in X barred (×1000), then Y appended plainly.
    private static string ExpandParentheses(string s, out bool error)
    {
        error = false;
        if (s.IndexOf('(') < 0 && s.IndexOf(')') < 0) return s;

        var sb = new StringBuilder();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '(')
            {
                int close = s.IndexOf(')', i + 1);
                if (close < 0) { error = true; return s; }
                string inner = s.Substring(i + 1, close - i - 1);
                if (inner.Length == 0) { error = true; return s; }
                sb.Append(Bar(inner.ToUpperInvariant()));
                i = close + 1;
            }
            else if (c == ')')
            {
                error = true; return s;
            }
            else
            {
                sb.Append(char.ToUpperInvariant(c));
                i++;
            }
        }
        return sb.ToString();
    }

    // Scan a normalised (uppercase, overline-marked) string char by char, mapping each barred or
    // plain letter to its value and summing. Rejects unknown characters.
    private static bool TryScan(string s, out long value, out string whyEn, out string whyZh)
    {
        value = 0; whyEn = ""; whyZh = "";
        var tokens = new List<int>(); // signed values in reading order, barred×1000

        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            bool barred = (i + 1 < s.Length) && s[i + 1] == Overline;
            if (!TryLetter(c, out int baseVal))
            {
                whyEn = $"Unexpected character '{c}'.";
                whyZh = $"出現無效字元「{c}」。";
                return false;
            }
            tokens.Add(barred ? baseVal * 1000 : baseVal);
            i += barred ? 2 : 1;
        }

        if (tokens.Count == 0)
        {
            whyEn = "No Roman letters found."; whyZh = "搵唔到羅馬字母。";
            return false;
        }

        // Additive/subtractive fold: if a token is less than the one after it, subtract it.
        long total = 0;
        for (int k = 0; k < tokens.Count; k++)
        {
            if (k + 1 < tokens.Count && tokens[k] < tokens[k + 1]) total -= tokens[k];
            else total += tokens[k];
        }
        value = total;
        return true;
    }

    private static bool TryLetter(char c, out int val)
    {
        switch (char.ToUpperInvariant(c))
        {
            case 'I': val = 1; return true;
            case 'V': val = 5; return true;
            case 'X': val = 10; return true;
            case 'L': val = 50; return true;
            case 'C': val = 100; return true;
            case 'D': val = 500; return true;
            case 'M': val = 1000; return true;
            default: val = 0; return false;
        }
    }

    private static ToNumberResult Fail(string en, string zh) =>
        new() { Ok = false, ReasonEn = en, ReasonZh = zh };

    /// <summary>True if a candidate string parses to a valid whole number.</summary>
    public static bool TryParseInt(string? s, out long value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim().Replace(",", "");
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
