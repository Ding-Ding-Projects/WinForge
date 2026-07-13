using System;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 檢查碼／校驗碼驗證器 · Check-digit / checksum validator. Pure managed C#.
/// Supports Luhn (credit cards), ISBN-10, ISBN-13, EAN-13, UPC-A and IBAN (incremental mod-97).
/// Everything is guarded — methods never throw; they return a Result with a status message key.
/// </summary>
public static class CheckDigitService
{
    public enum Scheme { Luhn, Isbn10, Isbn13, Ean13, UpcA, Iban }

    public readonly struct Result
    {
        public bool Ok { get; init; }          // input parsed & validation ran
        public bool Valid { get; init; }        // the check digit / checksum matched
        public string Detail { get; init; }     // computed check digit / brand / error, already localized-ish (english+zh split handled by caller)
        public string DetailZh { get; init; }
        public string Computed { get; init; }   // computed expected check digit / checksum, plain

        public static Result Fail(string en, string zh) => new() { Ok = false, Valid = false, Detail = en, DetailZh = zh, Computed = "" };
    }

    /// <summary>Strip spaces / dashes for digit-oriented schemes.</summary>
    private static string CleanDigits(string s) => (s ?? "").Replace(" ", "").Replace("-", "").Trim();

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private readonly record struct IbanFormat(string Country, int Length, string BbanPattern);

    // SWIFT ISO 13616 IBAN Registry, Release 102 (June 2026). Pattern symbols:
    // n = ASCII digit, a = ASCII uppercase letter, c = ASCII alphanumeric.
    private static readonly IbanFormat[] IbanFormats =
    [
        new("AD", 24, "4!n4!n12!c"),
        new("AE", 23, "3!n16!n"),
        new("AL", 28, "8!n16!c"),
        new("AT", 20, "5!n11!n"),
        new("AZ", 28, "4!a20!c"),
        new("BA", 20, "3!n3!n8!n2!n"),
        new("BE", 16, "3!n7!n2!n"),
        new("BG", 22, "4!a4!n2!n8!c"),
        new("BH", 22, "4!a14!c"),
        new("BI", 27, "5!n5!n11!n2!n"),
        new("BR", 29, "8!n5!n10!n1!a1!c"),
        new("BY", 28, "4!c4!n16!c"),
        new("CH", 21, "5!n12!c"),
        new("CR", 22, "4!n14!n"),
        new("CY", 28, "3!n5!n16!c"),
        new("CZ", 24, "4!n16!n"),
        new("DE", 22, "8!n10!n"),
        new("DJ", 27, "5!n5!n11!n2!n"),
        new("DK", 18, "4!n9!n1!n"),
        new("DO", 28, "4!c20!n"),
        new("EE", 20, "2!n14!n"),
        new("EG", 29, "4!n4!n17!n"),
        new("ES", 24, "4!n4!n1!n1!n10!n"),
        new("FI", 18, "3!n11!n"),
        new("FK", 18, "2!a12!n"),
        new("FO", 18, "4!n9!n1!n"),
        new("FR", 27, "5!n5!n11!c2!n"),
        new("GB", 22, "4!a6!n8!n"),
        new("GE", 22, "2!a16!n"),
        new("GI", 23, "4!a15!c"),
        new("GL", 18, "4!n9!n1!n"),
        new("GR", 27, "3!n4!n16!c"),
        new("GT", 28, "4!c20!c"),
        new("HN", 28, "4!a20!n"),
        new("HR", 21, "7!n10!n"),
        new("HU", 28, "3!n4!n1!n15!n1!n"),
        new("IE", 22, "4!a6!n8!n"),
        new("IL", 23, "3!n3!n13!n"),
        new("IQ", 23, "4!a3!n12!n"),
        new("IS", 26, "4!n2!n6!n10!n"),
        new("IT", 27, "1!a5!n5!n12!c"),
        new("JO", 30, "4!a4!n18!c"),
        new("KW", 30, "4!a22!c"),
        new("KZ", 20, "3!n13!c"),
        new("LB", 28, "4!n20!c"),
        new("LC", 32, "4!a24!c"),
        new("LI", 21, "5!n12!c"),
        new("LT", 20, "5!n11!n"),
        new("LU", 20, "3!n13!c"),
        new("LV", 21, "4!a13!c"),
        new("LY", 25, "3!n3!n15!n"),
        new("MC", 27, "5!n5!n11!c2!n"),
        new("MD", 24, "2!c18!c"),
        new("ME", 22, "3!n13!n2!n"),
        new("MK", 19, "3!n10!c2!n"),
        new("MN", 20, "4!n12!n"),
        new("MR", 27, "5!n5!n11!n2!n"),
        new("MT", 31, "4!a5!n18!c"),
        new("MU", 30, "4!a2!n2!n12!n3!n3!a"),
        new("NI", 28, "4!a20!n"),
        new("NL", 18, "4!a10!n"),
        new("NO", 15, "4!n6!n1!n"),
        new("OM", 23, "3!n16!c"),
        new("PK", 24, "4!a16!c"),
        new("PL", 28, "8!n16!n"),
        new("PS", 29, "4!a21!c"),
        new("PT", 25, "4!n4!n11!n2!n"),
        new("QA", 29, "4!a21!c"),
        new("RO", 24, "4!a16!c"),
        new("RS", 22, "3!n13!n2!n"),
        new("RU", 33, "9!n5!n15!c"),
        new("SA", 24, "2!n18!c"),
        new("SC", 31, "4!a2!n2!n16!n3!a"),
        new("SD", 18, "2!n12!n"),
        new("SE", 24, "3!n16!n1!n"),
        new("SI", 19, "5!n8!n2!n"),
        new("SK", 24, "4!n6!n10!n"),
        new("SM", 27, "1!a5!n5!n12!c"),
        new("SO", 23, "4!n3!n12!n"),
        new("ST", 25, "4!n4!n11!n2!n"),
        new("SV", 28, "4!a20!n"),
        new("TL", 23, "3!n14!n2!n"),
        new("TN", 24, "2!n3!n13!n2!n"),
        new("TR", 26, "5!n1!n16!c"),
        new("UA", 29, "6!n19!c"),
        new("VA", 22, "3!n15!n"),
        new("VG", 24, "4!a16!n"),
        new("XK", 20, "4!n10!n2!n"),
        new("YE", 30, "4!a4!n18!c"),
    ];

    static CheckDigitService()
    {
        for (int index = 0; index < IbanFormats.Length; index++)
        {
            IbanFormat format = IbanFormats[index];
            if (format.Country.Length != 2 || format.Length > 34 ||
                IbanPatternLength(format.BbanPattern) + 4 != format.Length ||
                (index > 0 && string.CompareOrdinal(IbanFormats[index - 1].Country, format.Country) >= 0))
                throw new InvalidOperationException("IBAN registry table is not sorted, unique, and structurally consistent.");
        }
    }

    // Exact canonical form consumed by the independent Release 102 fixture regression.
    // This source is linked into the managed oracle test assembly, so internal is sufficient.
    internal static string IbanRegistryCanonicalForTests()
    {
        var builder = new StringBuilder(IbanFormats.Length * 32);
        foreach (IbanFormat format in IbanFormats)
            builder.Append(format.Country).Append('\t')
                .Append(format.Length.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(format.BbanPattern).Append('\n');
        return builder.ToString();
    }

    private static int IbanPatternLength(string pattern)
    {
        int result = 0;
        int index = 0;
        while (index < pattern.Length)
        {
            int count = 0;
            while (index < pattern.Length && IsAsciiDigit(pattern[index]))
            {
                count = checked(count * 10 + pattern[index] - '0');
                index++;
            }
            if (count == 0 || index + 1 >= pattern.Length || pattern[index] != '!') return 0;
            char kind = pattern[++index];
            index++;
            if (kind is not ('n' or 'a' or 'c')) return 0;
            result = checked(result + count);
        }
        return result;
    }

    private static IbanFormat? FindIbanFormat(string country)
    {
        foreach (IbanFormat format in IbanFormats)
            if (string.Equals(format.Country, country, StringComparison.Ordinal))
                return format;
        return null;
    }

    private static string UpperAscii(string value)
    {
        char[] characters = value.ToCharArray();
        for (int index = 0; index < characters.Length; index++)
            if (characters[index] is >= 'a' and <= 'z')
                characters[index] = (char)(characters[index] - 'a' + 'A');
        return new string(characters);
    }

    private static bool MatchesIbanPattern(string value, string pattern)
    {
        int valueIndex = 0;
        int patternIndex = 0;
        while (patternIndex < pattern.Length)
        {
            int count = 0;
            while (patternIndex < pattern.Length && IsAsciiDigit(pattern[patternIndex]))
            {
                count = checked(count * 10 + pattern[patternIndex] - '0');
                patternIndex++;
            }
            if (count == 0 || patternIndex + 1 >= pattern.Length || pattern[patternIndex] != '!')
                return false;
            char kind = pattern[++patternIndex];
            patternIndex++;
            if (valueIndex + count > value.Length)
                return false;
            for (int offset = 0; offset < count; offset++)
            {
                char character = value[valueIndex++];
                bool matches = kind switch
                {
                    'n' => IsAsciiDigit(character),
                    'a' => character is >= 'A' and <= 'Z',
                    'c' => IsAsciiDigit(character) || character is >= 'A' and <= 'Z',
                    _ => false,
                };
                if (!matches) return false;
            }
        }
        return valueIndex == value.Length;
    }

    public static Result Validate(Scheme scheme, string input)
    {
        try
        {
            return scheme switch
            {
                Scheme.Luhn => Luhn(input),
                Scheme.Isbn10 => Isbn10(input),
                Scheme.Isbn13 => Isbn13(input),
                Scheme.Ean13 => EanUpc(input, 13),
                Scheme.UpcA => EanUpc(input, 12),
                Scheme.Iban => Iban(input),
                _ => Result.Fail("Unknown scheme.", "未知格式。"),
            };
        }
        catch (Exception ex)
        {
            return Result.Fail("Could not validate: " + ex.Message, "無法驗證：" + ex.Message);
        }
    }

    // ---- Luhn / credit cards -------------------------------------------------
    private static Result Luhn(string input)
    {
        string d = CleanDigits(input);
        if (d.Length == 0) return Result.Fail("Enter a card / number.", "請輸入卡號或數字。");
        foreach (char c in d) if (!IsAsciiDigit(c)) return Result.Fail("Digits only for Luhn.", "Luhn 只接受數字。");
        if (d.Length < 2) return Result.Fail("Too short.", "太短。");

        int sum = 0; bool dbl = false;
        for (int i = d.Length - 1; i >= 0; i--)
        {
            int n = d[i] - '0';
            if (dbl) { n *= 2; if (n > 9) n -= 9; }
            sum = (sum + n) % 10; dbl = !dbl;
        }
        bool valid = sum == 0;

        // check digit that WOULD make it valid (over the body, i.e. all but the last)
        int check = LuhnCheckDigit(d.Substring(0, d.Length - 1));
        string brand = DetectBrand(d);
        string brandEn = brand.Length > 0 ? $" · brand: {brand}" : "";
        string brandZh = brand.Length > 0 ? $" · 卡種：{brand}" : "";
        return new Result
        {
            Ok = true,
            Valid = valid,
            Computed = check.ToString(CultureInfo.InvariantCulture),
            Detail = $"Expected last digit: {check}{brandEn}",
            DetailZh = $"應有嘅尾數：{check}{brandZh}",
        };
    }

    private static int LuhnCheckDigit(string body)
    {
        int sum = 0; bool dbl = true; // position of the (future) check digit is even from right, so body's last is doubled
        for (int i = body.Length - 1; i >= 0; i--)
        {
            int n = body[i] - '0';
            if (dbl) { n *= 2; if (n > 9) n -= 9; }
            sum = (sum + n) % 10; dbl = !dbl;
        }
        return (10 - sum % 10) % 10;
    }

    private static string DetectBrand(string d)
    {
        int len = d.Length;
        if (d.StartsWith("4", StringComparison.Ordinal) && (len == 13 || len == 16 || len == 19)) return "Visa";
        if (len == 15 && (d.StartsWith("34", StringComparison.Ordinal) || d.StartsWith("37", StringComparison.Ordinal))) return "Amex";
        if (len == 16)
        {
            int p2 = int.Parse(d.Substring(0, 2), CultureInfo.InvariantCulture);
            int p4 = int.Parse(d.Substring(0, 4), CultureInfo.InvariantCulture);
            if (p2 >= 51 && p2 <= 55) return "Mastercard";
            if (p4 >= 2221 && p4 <= 2720) return "Mastercard";
        }
        if (len is >= 16 and <= 19)
        {
            int p8 = int.Parse(d.Substring(0, 8), CultureInfo.InvariantCulture);
            if (p8 is >= 60110000 and <= 60119999 or >= 64400000 and <= 65899999) return "Discover";
        }
        return "";
    }

    // ---- ISBN-10 -------------------------------------------------------------
    private static Result Isbn10(string input)
    {
        string d = UpperAscii(CleanDigits(input));
        if (d.Length == 0) return Result.Fail("Enter an ISBN-10.", "請輸入 ISBN-10。");
        if (d.Length != 10) return Result.Fail("ISBN-10 needs 10 characters.", "ISBN-10 要 10 個字元。");
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            char c = d[i];
            int v;
            if (c == 'X' && i == 9) v = 10;
            else if (IsAsciiDigit(c)) v = c - '0';
            else return Result.Fail("Only digits, plus X as the last check.", "只可用數字，尾位可用 X。");
            sum += v * (10 - i);
        }
        bool valid = sum % 11 == 0;
        int chk = (11 - (Weighted10(d.Substring(0, 9)) % 11)) % 11;
        string chkStr = chk == 10 ? "X" : chk.ToString(CultureInfo.InvariantCulture);
        return new Result { Ok = true, Valid = valid, Computed = chkStr, Detail = $"Expected check: {chkStr}", DetailZh = $"應有檢查碼：{chkStr}" };
    }

    private static int Weighted10(string first9)
    {
        int sum = 0;
        for (int i = 0; i < 9; i++) sum += (first9[i] - '0') * (10 - i);
        return sum;
    }

    // ---- ISBN-13 -------------------------------------------------------------
    private static Result Isbn13(string input) => Ean13Core(input, isbn: true);

    // ---- EAN-13 / UPC-A ------------------------------------------------------
    private static Result EanUpc(string input, int len)
    {
        if (len == 13) return Ean13Core(input, isbn: false);
        // UPC-A: 12 digits, same alternating 3/1 weighting but from the right
        string d = CleanDigits(input);
        if (d.Length == 0) return Result.Fail("Enter a UPC-A.", "請輸入 UPC-A。");
        foreach (char c in d) if (!IsAsciiDigit(c)) return Result.Fail("Digits only.", "只可用數字。");
        if (d.Length != 12) return Result.Fail("UPC-A needs 12 digits.", "UPC-A 要 12 個數字。");
        int chk = Mod10Weighted(d.Substring(0, 11), oddWeight3FromLeft: true);
        bool valid = (d[11] - '0') == chk;
        return new Result { Ok = true, Valid = valid, Computed = chk.ToString(CultureInfo.InvariantCulture), Detail = $"Expected check digit: {chk}", DetailZh = $"應有檢查碼：{chk}" };
    }

    private static Result Ean13Core(string input, bool isbn)
    {
        string label = isbn ? "ISBN-13" : "EAN-13";
        string labelZh = isbn ? "ISBN-13" : "EAN-13";
        string d = CleanDigits(input);
        if (d.Length == 0) return Result.Fail($"Enter an {label}.", $"請輸入 {labelZh}。");
        foreach (char c in d) if (!IsAsciiDigit(c)) return Result.Fail("Digits only.", "只可用數字。");
        if (d.Length != 13) return Result.Fail($"{label} needs 13 digits.", $"{labelZh} 要 13 個數字。");
        if (isbn && !(d.StartsWith("978", StringComparison.Ordinal) || d.StartsWith("979", StringComparison.Ordinal)))
            return Result.Fail("ISBN-13 must start with 978 or 979.", "ISBN-13 開頭一定要係 978 或 979。");
        int chk = Mod10Weighted(d.Substring(0, 12), oddWeight3FromLeft: false);
        bool valid = (d[12] - '0') == chk;
        return new Result { Ok = true, Valid = valid, Computed = chk.ToString(CultureInfo.InvariantCulture), Detail = $"Expected check digit: {chk}", DetailZh = $"應有檢查碼：{chk}" };
    }

    /// <summary>
    /// GS1 mod-10. For EAN-13 body (12) digits get weights 1,3,1,3… from the left.
    /// For UPC-A body (11) digits get weights 3,1,3,1… from the left. `oddWeight3FromLeft`
    /// selects which pattern (true = first digit weighted 3).
    /// </summary>
    private static int Mod10Weighted(string body, bool oddWeight3FromLeft)
    {
        int sum = 0;
        for (int i = 0; i < body.Length; i++)
        {
            int n = body[i] - '0';
            bool weight3 = oddWeight3FromLeft ? (i % 2 == 0) : (i % 2 == 1);
            sum += n * (weight3 ? 3 : 1);
        }
        return (10 - sum % 10) % 10;
    }

    // ---- IBAN (mod-97) -------------------------------------------------------
    private static Result Iban(string input)
    {
        string raw = UpperAscii((input ?? "").Replace(" ", "").Replace("-", "").Trim());
        if (raw.Length == 0) return Result.Fail("Enter an IBAN.", "請輸入 IBAN。");
        if (raw.Length < 4 || raw.Length > 34)
            return Result.Fail("IBAN must include a country and two check digits.", "IBAN 要包含國家代碼同兩個檢查碼。");
        foreach (char c in raw)
            if (!(IsAsciiDigit(c) || (c >= 'A' && c <= 'Z')))
                return Result.Fail("IBAN uses letters & digits only.", "IBAN 只可用字母同數字。");
        if (raw[0] is < 'A' or > 'Z' || raw[1] is < 'A' or > 'Z')
            return Result.Fail("IBAN must start with a 2-letter country.", "IBAN 開頭要兩個國家字母。");
        if (!IsAsciiDigit(raw[2]) || !IsAsciiDigit(raw[3]))
            return Result.Fail("IBAN check digits must be numeric.", "IBAN 檢查碼一定要係數字。");

        string country = raw[..2];
        IbanFormat? candidate = FindIbanFormat(country);
        if (candidate is null)
            return Result.Fail($"Unsupported IBAN country: {country}.", $"未支援嘅 IBAN 國家代碼：{country}。");
        IbanFormat format = candidate.Value;
        if (raw.Length != format.Length)
            return Result.Fail(
                $"{country} IBAN needs {format.Length} characters.",
                $"{country} IBAN 要 {format.Length} 個字元。");
        if (!MatchesIbanPattern(raw[4..], format.BbanPattern))
            return Result.Fail(
                $"BBAN does not match the registered {country} format.",
                $"BBAN 唔符合已登記嘅 {country} 格式。");

        // Move first 4 chars to the end, convert letters A=10..Z=35, mod 97 must == 1.
        string rearranged = raw.Substring(4) + raw.Substring(0, 4);
        int mod = IbanRemainder(rearranged);
        bool valid = mod == 1;

        // Compute the correct 2-digit check ("kk") for the given country + BBAN.
        string computedCheck = ComputeIbanCheck(raw);
        return new Result
        {
            Ok = true,
            Valid = valid,
            Computed = computedCheck,
            Detail = $"mod-97 = {mod} (valid = 1) · correct check digits: {computedCheck}",
            DetailZh = $"mod-97 = {mod}（正確係 1）· 正確檢查碼：{computedCheck}",
        };
    }

    private static string ComputeIbanCheck(string raw)
    {
        string rearranged = raw[4..] + raw[..2] + "00";
        int chk = 98 - IbanRemainder(rearranged);
        return chk.ToString("00", CultureInfo.InvariantCulture);
    }

    private static int IbanRemainder(string value)
    {
        int remainder = 0;
        foreach (char character in value)
        {
            remainder = IsAsciiDigit(character)
                ? ((remainder * 10) + character - '0') % 97
                : ((remainder * 100) + character - 'A' + 10) % 97;
        }
        return remainder;
    }
}
