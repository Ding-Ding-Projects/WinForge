using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 數字 ⇄ 文字 · 羅馬數字 · Number ⇄ words &amp; Roman numerals.
/// Pure managed C# (BigInteger). Every parse is guarded — the service never throws;
/// on bad input it returns a friendly message via the caller-supplied language picker.
/// </summary>
public static class NumberWordsService
{
    private static readonly string[] Ones =
    {
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
        "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen",
        "seventeen", "eighteen", "nineteen"
    };

    private static readonly string[] Tens =
    {
        "", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"
    };

    // Short-scale group names, index = group of three (0 = units).
    private static readonly string[] Scales =
    {
        "", "thousand", "million", "billion", "trillion", "quadrillion", "quintillion",
        "sextillion", "septillion", "octillion", "nonillion", "decillion"
    };

    // ---- 3-digit group ----------------------------------------------------

    private static string ThreeDigits(int n)
    {
        // n is 0..999
        var sb = new StringBuilder();
        if (n >= 100)
        {
            sb.Append(Ones[n / 100]).Append(" hundred");
            n %= 100;
            if (n > 0) sb.Append(' ');
        }
        if (n >= 20)
        {
            sb.Append(Tens[n / 10]);
            if (n % 10 > 0) sb.Append('-').Append(Ones[n % 10]);
        }
        else if (n > 0)
        {
            sb.Append(Ones[n]);
        }
        return sb.ToString();
    }

    /// <summary>Whole non-negative BigInteger to English words. Empty string means zero handled by caller.</summary>
    private static string WholeToWords(BigInteger n)
    {
        if (n.Sign == 0) return "zero";

        // Split into groups of three (base 1000), least-significant first.
        var groups = new List<int>();
        BigInteger thousand = 1000;
        while (n > 0)
        {
            groups.Add((int)(n % thousand));
            n /= thousand;
        }

        var parts = new List<string>();
        for (int i = groups.Count - 1; i >= 0; i--)
        {
            int g = groups[i];
            if (g == 0) continue;
            string words = ThreeDigits(g);
            string scale = i < Scales.Length ? Scales[i] : ThousandPower(i);
            parts.Add(scale.Length > 0 ? $"{words} {scale}" : words);
        }
        return string.Join(" ", parts);
    }

    // Beyond the named scales, fall back to "thousand^N" so we never throw.
    private static string ThousandPower(int i) => $"(thousand^{i})";

    // ---- Number -> words --------------------------------------------------

    /// <summary>Full number (with optional sign and decimal part) to English words.</summary>
    public static bool TryNumberToWords(string input, out string words)
    {
        words = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        string s = input.Trim().Replace(",", "");

        bool negative = false;
        if (s.StartsWith("-")) { negative = true; s = s[1..]; }
        else if (s.StartsWith("+")) { s = s[1..]; }

        string intPart, fracPart = "";
        int dot = s.IndexOf('.');
        if (dot >= 0)
        {
            intPart = s[..dot];
            fracPart = s[(dot + 1)..];
        }
        else
        {
            intPart = s;
        }

        if (intPart.Length == 0) intPart = "0";
        foreach (char c in intPart) if (!char.IsDigit(c)) return false;
        foreach (char c in fracPart) if (!char.IsDigit(c)) return false;

        if (!BigInteger.TryParse(intPart, out var whole)) return false;

        var sb = new StringBuilder();
        if (negative && (whole.Sign != 0 || fracPart.TrimEnd('0').Length > 0))
            sb.Append("negative ");
        sb.Append(WholeToWords(whole));

        // trim trailing zeros of the fractional part for a natural reading
        string frac = fracPart.TrimEnd('0');
        if (frac.Length > 0)
        {
            sb.Append(" point");
            foreach (char c in frac)
                sb.Append(' ').Append(Ones[c - '0']);
        }

        words = Capitalize(sb.ToString());
        return true;
    }

    // ---- Currency --------------------------------------------------------

    /// <summary>Amount to currency words: "... dollars and NN cents".</summary>
    public static bool TryCurrencyToWords(string input, out string words)
    {
        words = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        string s = input.Trim().Replace(",", "").Replace("$", "");

        bool negative = false;
        if (s.StartsWith("-")) { negative = true; s = s[1..]; }
        else if (s.StartsWith("+")) { s = s[1..]; }

        string dollarsStr, centsStr = "00";
        int dot = s.IndexOf('.');
        if (dot >= 0)
        {
            dollarsStr = s[..dot];
            centsStr = s[(dot + 1)..];
        }
        else dollarsStr = s;

        if (dollarsStr.Length == 0) dollarsStr = "0";
        foreach (char c in dollarsStr) if (!char.IsDigit(c)) return false;
        foreach (char c in centsStr) if (!char.IsDigit(c)) return false;

        // Round/normalise cents to exactly two digits.
        if (centsStr.Length == 1) centsStr += "0";
        else if (centsStr.Length > 2) centsStr = centsStr[..2];
        if (centsStr.Length == 0) centsStr = "00";

        if (!BigInteger.TryParse(dollarsStr, out var dollars)) return false;
        if (!int.TryParse(centsStr, out int cents)) return false;

        var sb = new StringBuilder();
        if (negative) sb.Append("negative ");
        sb.Append(WholeToWords(dollars));
        sb.Append(dollars == BigInteger.One ? " dollar" : " dollars");
        sb.Append(" and ");
        sb.Append(WholeToWords(cents));
        sb.Append(cents == 1 ? " cent" : " cents");

        words = Capitalize(sb.ToString());
        return true;
    }

    // ---- Ordinal ---------------------------------------------------------

    private static readonly Dictionary<string, string> OrdinalOnes = new()
    {
        ["one"] = "first", ["two"] = "second", ["three"] = "third", ["five"] = "fifth",
        ["eight"] = "eighth", ["nine"] = "ninth", ["twelve"] = "twelfth"
    };

    /// <summary>Non-negative integer to its ordinal words (e.g. 21 -> "twenty-first").</summary>
    public static bool TryOrdinal(string input, out string words)
    {
        words = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        string s = input.Trim().Replace(",", "");
        if (s.StartsWith("+")) s = s[1..];
        foreach (char c in s) if (!char.IsDigit(c)) return false;
        if (!BigInteger.TryParse(s, out var n) || n.Sign < 0) return false;

        string cardinal = WholeToWords(n);
        // Ordinalise the final word only.
        int lastSpace = cardinal.LastIndexOf(' ');
        string head = lastSpace >= 0 ? cardinal[..(lastSpace + 1)] : "";
        string tail = lastSpace >= 0 ? cardinal[(lastSpace + 1)..] : cardinal;

        int hyphen = tail.LastIndexOf('-');
        string tHead = hyphen >= 0 ? tail[..(hyphen + 1)] : "";
        string tWord = hyphen >= 0 ? tail[(hyphen + 1)..] : tail;

        string ord;
        if (OrdinalOnes.TryGetValue(tWord, out var special))
            ord = special;
        else if (tWord.EndsWith("y"))
            ord = tWord[..^1] + "ieth";       // twenty -> twentieth
        else
            ord = tWord + "th";

        words = Capitalize(head + tHead + ord);
        return true;
    }

    // ---- Roman numerals --------------------------------------------------

    private static readonly (int Value, string Symbol)[] RomanTable =
    {
        (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"),
        (100, "C"), (90, "XC"), (50, "L"), (40, "XL"),
        (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I")
    };

    /// <summary>Integer 1..3999 to a Roman numeral.</summary>
    public static bool TryToRoman(string input, out string roman)
    {
        roman = "";
        if (string.IsNullOrWhiteSpace(input)) return false;
        string s = input.Trim().Replace(",", "");
        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) return false;
        if (n < 1 || n > 3999) return false;

        var sb = new StringBuilder();
        foreach (var (value, symbol) in RomanTable)
            while (n >= value) { sb.Append(symbol); n -= value; }
        roman = sb.ToString();
        return true;
    }

    private static readonly Dictionary<char, int> RomanValue = new()
    {
        ['I'] = 1, ['V'] = 5, ['X'] = 10, ['L'] = 50, ['C'] = 100, ['D'] = 500, ['M'] = 1000
    };

    /// <summary>Roman numeral to integer, validated by round-trip so only canonical forms pass.</summary>
    public static bool TryFromRoman(string input, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;
        string s = input.Trim().ToUpperInvariant();
        foreach (char c in s) if (!RomanValue.ContainsKey(c)) return false;

        int total = 0, prev = 0;
        for (int i = s.Length - 1; i >= 0; i--)
        {
            int cur = RomanValue[s[i]];
            if (cur < prev) total -= cur; else total += cur;
            prev = cur;
        }
        if (total < 1 || total > 3999) return false;

        // Canonical-form check: re-encode and compare.
        return TryToRoman(total.ToString(CultureInfo.InvariantCulture), out var back)
               && back == s && (value = total) == total;
    }

    // ---- Words -> number (best effort) -----------------------------------

    private static readonly Dictionary<string, long> WordUnits = new()
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
        ["five"] = 5, ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9,
        ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13,
        ["fourteen"] = 14, ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17,
        ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20, ["thirty"] = 30,
        ["forty"] = 40, ["fifty"] = 50, ["sixty"] = 60, ["seventy"] = 70,
        ["eighty"] = 80, ["ninety"] = 90
    };

    private static readonly Dictionary<string, BigInteger> WordMagnitudes = new()
    {
        ["hundred"] = 100, ["thousand"] = 1000, ["million"] = 1_000_000,
        ["billion"] = 1_000_000_000, ["trillion"] = 1_000_000_000_000,
        ["quadrillion"] = BigInteger.Parse("1000000000000000"),
        ["quintillion"] = BigInteger.Parse("1000000000000000000")
    };

    /// <summary>Parse simple English number words back to a value. Best-effort; returns false on anything unexpected.</summary>
    public static bool TryWordsToNumber(string input, out BigInteger value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input)) return false;

        bool negative = false;
        string cleaned = input.Trim().ToLowerInvariant().Replace("-", " ");
        var tokens = cleaned.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;

        BigInteger current = 0;   // running sub-thousand group
        BigInteger result = 0;    // accumulated total
        bool sawAny = false;

        foreach (string raw in tokens)
        {
            string t = raw;
            if (t == "negative" || t == "minus") { negative = true; continue; }
            if (t == "and") continue;

            if (WordUnits.TryGetValue(t, out long unit))
            {
                current += unit;
                sawAny = true;
            }
            else if (t == "hundred")
            {
                current = (current == 0 ? 1 : current) * 100;
                sawAny = true;
            }
            else if (WordMagnitudes.TryGetValue(t, out var mag))
            {
                current = (current == 0 ? 1 : current) * mag;
                result += current;
                current = 0;
                sawAny = true;
            }
            else
            {
                return false; // unknown token -> friendly failure upstream
            }
        }

        if (!sawAny) return false;
        result += current;
        value = negative ? -result : result;
        return true;
    }

    // ---- helpers ---------------------------------------------------------

    private static string Capitalize(string s)
        => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
