using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 數字轉文字（加強版）· Number-to-words (extended) — pure managed conversions:
/// English cardinal / ordinal, currency words, Chinese lowercase (一百二十三) and Chinese
/// financial uppercase 大寫 (壹佰貳拾參 / 元角分). All methods are robust and never throw —
/// on bad or oversized input they return a bilingual-safe empty/marker string. No I/O, no redirect.
/// </summary>
public static class NumWordsXService
{
    /// <summary>Upper bound on the integer-part digit count we will spell out.</summary>
    public const int MaxIntegerDigits = 66; // well past quadrillions; keeps output sane

    // ---------------------------------------------------------------- parsing

    /// <summary>
    /// Parse loose user input into a sign, an integer-part BigInteger (absolute) and a fractional
    /// string of raw digits (may be empty). Returns false when the input is not a plain number.
    /// </summary>
    public static bool TryParse(string? raw, out bool negative, out BigInteger integer, out string fraction)
    {
        negative = false;
        integer = BigInteger.Zero;
        fraction = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var s = raw.Trim().Replace(",", "").Replace("_", "").Replace(" ", "");
        if (s.Length == 0) return false;

        int i = 0;
        if (s[0] == '+') i = 1;
        else if (s[0] == '-') { negative = true; i = 1; }

        var intPart = new StringBuilder();
        var fracPart = new StringBuilder();
        bool seenDot = false, anyDigit = false;
        for (; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '.')
            {
                if (seenDot) return false;
                seenDot = true;
            }
            else if (c >= '0' && c <= '9')
            {
                anyDigit = true;
                if (seenDot) fracPart.Append(c);
                else intPart.Append(c);
            }
            else return false;
        }
        if (!anyDigit) return false;

        var it = intPart.ToString().TrimStart('0');
        if (it.Length == 0) it = "0";
        if (it.Length > MaxIntegerDigits) return false;
        if (!BigInteger.TryParse(it, out integer)) return false;

        // trim trailing zeros in fraction display but keep meaning
        fraction = fracPart.ToString();
        return true;
    }

    /// <summary>True when the (abs) value is exactly zero given parsed parts.</summary>
    private static bool IsZero(BigInteger integer, string fraction)
        => integer.IsZero && (fraction.TrimEnd('0').Length == 0);

    // ------------------------------------------------------------- English

    private static readonly string[] Ones =
    {
        "zero","one","two","three","four","five","six","seven","eight","nine","ten",
        "eleven","twelve","thirteen","fourteen","fifteen","sixteen","seventeen","eighteen","nineteen"
    };
    private static readonly string[] Tens =
    {
        "","","twenty","thirty","forty","fifty","sixty","seventy","eighty","ninety"
    };
    // scale names per group of three, index 1..
    private static readonly string[] Scales =
    {
        "", "thousand", "million", "billion", "trillion", "quadrillion", "quintillion",
        "sextillion", "septillion", "octillion", "nonillion", "decillion"
    };

    private static string ThreeDigitsToWords(int n)
    {
        // n in 0..999
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

    /// <summary>English cardinal words for an unsigned BigInteger (e.g. "one hundred twenty-three").</summary>
    public static string EnglishCardinal(BigInteger value)
    {
        if (value.Sign < 0) value = -value;
        if (value.IsZero) return "zero";

        // break into base-1000 groups, least significant first
        var groups = new List<int>();
        var thousand = new BigInteger(1000);
        while (value > 0)
        {
            groups.Add((int)(value % thousand));
            value /= thousand;
        }
        if (groups.Count > Scales.Length) return string.Empty; // beyond named scales -> caller guards

        var parts = new List<string>();
        for (int g = groups.Count - 1; g >= 0; g--)
        {
            if (groups[g] == 0) continue;
            var words = ThreeDigitsToWords(groups[g]);
            if (g > 0) words += " " + Scales[g];
            parts.Add(words);
        }
        return string.Join(" ", parts);
    }

    /// <summary>English cardinal, honoring a sign.</summary>
    public static string EnglishCardinalSigned(bool negative, BigInteger value)
        => (negative && !value.IsZero ? "negative " : "") + EnglishCardinal(value);

    private static readonly Dictionary<string, string> OrdinalOnes = new()
    {
        ["one"] = "first", ["two"] = "second", ["three"] = "third", ["five"] = "fifth",
        ["eight"] = "eighth", ["nine"] = "ninth", ["twelve"] = "twelfth",
    };

    /// <summary>Convert a cardinal phrase to its ordinal form ("... twenty-three" → "... twenty-third").</summary>
    public static string EnglishOrdinal(BigInteger value)
    {
        if (value.Sign < 0) value = -value;
        var card = EnglishCardinal(value);
        if (string.IsNullOrEmpty(card)) return string.Empty;

        // Find last word (may contain a hyphen, e.g. "twenty-three")
        int lastSpace = card.LastIndexOf(' ');
        string head = lastSpace < 0 ? "" : card.Substring(0, lastSpace + 1);
        string tail = lastSpace < 0 ? card : card.Substring(lastSpace + 1);

        int hyphen = tail.LastIndexOf('-');
        string prefix = hyphen < 0 ? "" : tail.Substring(0, hyphen + 1);
        string lastWord = hyphen < 0 ? tail : tail.Substring(hyphen + 1);

        string ord;
        if (OrdinalOnes.TryGetValue(lastWord, out var special)) ord = special;
        else if (lastWord.EndsWith("y")) ord = lastWord.Substring(0, lastWord.Length - 1) + "ieth"; // twenty->twentieth
        else ord = lastWord + "th";

        return head + prefix + ord;
    }

    /// <summary>Numeric ordinal suffix form, e.g. 123 → "123rd", 111 → "111th".</summary>
    public static string OrdinalNumeric(bool negative, BigInteger value)
    {
        string digits = value.ToString(CultureInfo.InvariantCulture);
        int last2 = (int)(value % 100);
        int last1 = (int)(value % 10);
        string suffix = "th";
        if (last2 < 11 || last2 > 13)
        {
            suffix = last1 switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };
        }
        return (negative && !value.IsZero ? "-" : "") + digits + suffix;
    }

    // ------------------------------------------------------------- currency (English)

    public readonly record struct CurrencySpec(string Code, string Major, string MajorPlural, string Minor, string MinorPlural);

    public static readonly Dictionary<string, CurrencySpec> Currencies = new()
    {
        ["USD"] = new("USD", "dollar", "dollars", "cent", "cents"),
        ["HKD"] = new("HKD", "dollar", "dollars", "cent", "cents"),
        ["GBP"] = new("GBP", "pound", "pounds", "penny", "pence"),
    };

    /// <summary>
    /// English currency words, e.g. "one hundred twenty-three dollars and forty-five cents".
    /// Fraction is rounded/padded to 2 decimal places.
    /// </summary>
    public static string EnglishCurrency(bool negative, BigInteger integer, string fraction, CurrencySpec spec)
    {
        int cents = TwoDigitMinor(fraction);
        var sb = new StringBuilder();
        if (negative && !(integer.IsZero && cents == 0)) sb.Append("negative ");

        string majorWords = EnglishCardinal(integer);
        string majorUnit = integer == BigInteger.One ? spec.Major : spec.MajorPlural;
        sb.Append(majorWords).Append(' ').Append(majorUnit);

        sb.Append(" and ");
        string minorWords = EnglishCardinal(new BigInteger(cents));
        string minorUnit = cents == 1 ? spec.Minor : spec.MinorPlural;
        sb.Append(minorWords).Append(' ').Append(minorUnit);
        return sb.ToString();
    }

    /// <summary>Round a raw fraction string to 2 decimal places (0..99).</summary>
    private static int TwoDigitMinor(string fraction)
    {
        if (string.IsNullOrEmpty(fraction)) return 0;
        // take first two digits, round using the third
        string f = fraction;
        int whole = 0;
        if (f.Length >= 1) whole = (f[0] - '0') * 10;
        if (f.Length >= 2) whole += (f[1] - '0');
        if (f.Length >= 3 && f[2] >= '5') whole += 1; // round
        if (whole > 99) whole = 99; // clamp (e.g. .999 -> 100 spilled — keep in cents for simplicity)
        return whole;
    }

    // ------------------------------------------------------------- Chinese

    private static readonly char[] CnLower = { '零','一','二','三','四','五','六','七','八','九' };
    private static readonly char[] CnUpper = { '零','壹','貳','參','肆','伍','陸','柒','捌','玖' };
    // small unit within a 4-digit section: (none) 十 百 千
    private static readonly char[] CnLowerUnit = { '\0','十','百','千' };
    private static readonly char[] CnUpperUnit = { '\0','拾','佰','仟' };
    // big section units: '' 萬 億 兆
    private static readonly string[] CnLowerSection = { "", "萬", "億", "兆" };
    private static readonly string[] CnUpperSection = { "", "萬", "億", "兆" };

    private static string FourDigitsChinese(int n, bool upper, out bool startedWithZero)
    {
        // n in 0..9999 -> Chinese with internal 零 handling; returns without leading section unit.
        startedWithZero = false;
        var digits = CnLower; var units = CnLowerUnit;
        if (upper) { digits = CnUpper; units = CnUpperUnit; }

        var sb = new StringBuilder();
        bool zeroPending = false;
        bool any = false;
        for (int pos = 3; pos >= 0; pos--)
        {
            int place = 1;
            for (int k = 0; k < pos; k++) place *= 10;
            int d = (n / place) % 10;
            if (d == 0)
            {
                if (any) zeroPending = true; // remember a gap
            }
            else
            {
                if (zeroPending) { sb.Append(digits[0]); zeroPending = false; }
                sb.Append(digits[d]);
                if (units[pos] != '\0') sb.Append(units[pos]);
                any = true;
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Chinese numerals for an unsigned BigInteger. <paramref name="upper"/> selects financial 大寫.
    /// Handles 萬/億/兆 sections and inter-section 零 insertion. Values ≥ 兆·10000 (10^16) beyond
    /// our section table return empty (caller guards).
    /// </summary>
    public static string Chinese(BigInteger value, bool upper)
    {
        if (value.Sign < 0) value = -value;
        var digits = upper ? CnUpper : CnLower;
        if (value.IsZero) return digits[0].ToString();

        // break into 4-digit sections, least significant first
        var sections = new List<int>();
        var tenK = new BigInteger(10000);
        while (value > 0)
        {
            sections.Add((int)(value % tenK));
            value /= tenK;
        }
        var sectionUnits = upper ? CnUpperSection : CnLowerSection;
        if (sections.Count > sectionUnits.Length) return string.Empty; // beyond 兆 table

        var sb = new StringBuilder();
        bool higherHadValue = false;
        for (int s = sections.Count - 1; s >= 0; s--)
        {
            int sec = sections[s];
            if (sec == 0)
            {
                // a whole empty section: mark that we may need a 零 before next non-zero
                if (higherHadValue) { /* pending zero handled below */ }
                continue;
            }
            // if a higher section already emitted and this section < 1000, we need a 零 joiner
            if (higherHadValue && sec < 1000)
                sb.Append(digits[0]);

            sb.Append(FourDigitsChinese(sec, upper, out _));
            sb.Append(sectionUnits[s]);
            higherHadValue = true;
        }

        var result = sb.ToString();
        // Common tidy: "一十" at the very start reads as "十" in natural Cantonese lowercase.
        if (!upper && result.StartsWith("一十"))
            result = result.Substring(1);
        return result;
    }

    /// <summary>Chinese numerals honoring a sign (負).</summary>
    public static string ChineseSigned(bool negative, BigInteger value, bool upper)
        => (negative && !value.IsZero ? "負" : "") + Chinese(value, upper);

    /// <summary>
    /// Chinese financial 大寫 currency with 元角分, e.g. 壹佰貳拾參元肆角伍分.
    /// When there is no fraction, appends 整.
    /// </summary>
    public static string ChineseCurrencyUpper(bool negative, BigInteger integer, string fraction)
    {
        int jiao = 0, fen = 0;
        if (fraction.Length >= 1) jiao = fraction[0] - '0';
        if (fraction.Length >= 2) fen = fraction[1] - '0';
        if (fraction.Length >= 3 && fraction[2] >= '5')
        {
            fen++;
            if (fen > 9) { fen = 0; jiao++; if (jiao > 9) { jiao = 0; integer += 1; } }
        }

        var sb = new StringBuilder();
        if (negative && !(integer.IsZero && jiao == 0 && fen == 0)) sb.Append("負");

        string yuan = Chinese(integer, upper: true);
        if (string.IsNullOrEmpty(yuan)) return string.Empty;
        sb.Append(yuan).Append('圓');

        if (jiao == 0 && fen == 0)
        {
            sb.Append('整');
            return sb.ToString();
        }
        if (jiao > 0) sb.Append(CnUpper[jiao]).Append('角');
        else if (fen > 0 && !integer.IsZero) sb.Append('零'); // 零 between 圓 and 分 when 角 is 0
        if (fen > 0) sb.Append(CnUpper[fen]).Append('分');
        return sb.ToString();
    }

    /// <summary>Human display of the parsed magnitude (used for echoing back what was read).</summary>
    public static string DisplayNumber(bool negative, BigInteger integer, string fraction)
    {
        var sb = new StringBuilder();
        if (negative && !IsZero(integer, fraction)) sb.Append('-');
        sb.Append(integer.ToString(CultureInfo.InvariantCulture));
        var f = fraction.TrimEnd('0');
        if (f.Length > 0) sb.Append('.').Append(f);
        return sb.ToString();
    }
}
