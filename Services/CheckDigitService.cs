using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 檢查碼／校驗碼驗證器 · Check-digit / checksum validator. Pure managed C#.
/// Supports Luhn (credit cards), ISBN-10, ISBN-13, EAN-13, UPC-A and IBAN (mod-97, BigInteger).
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
        foreach (char c in d) if (!char.IsDigit(c)) return Result.Fail("Digits only for Luhn.", "Luhn 只接受數字。");
        if (d.Length < 2) return Result.Fail("Too short.", "太短。");

        int sum = 0; bool dbl = false;
        for (int i = d.Length - 1; i >= 0; i--)
        {
            int n = d[i] - '0';
            if (dbl) { n *= 2; if (n > 9) n -= 9; }
            sum += n; dbl = !dbl;
        }
        bool valid = sum % 10 == 0;

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
            sum += n; dbl = !dbl;
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
            if (d.StartsWith("6011", StringComparison.Ordinal) || d.StartsWith("65", StringComparison.Ordinal)) return "Discover";
            int p3 = int.Parse(d.Substring(0, 3), CultureInfo.InvariantCulture);
            if (p3 >= 644 && p3 <= 649) return "Discover";
        }
        return "";
    }

    // ---- ISBN-10 -------------------------------------------------------------
    private static Result Isbn10(string input)
    {
        string d = CleanDigits(input).ToUpperInvariant();
        if (d.Length == 0) return Result.Fail("Enter an ISBN-10.", "請輸入 ISBN-10。");
        if (d.Length != 10) return Result.Fail("ISBN-10 needs 10 characters.", "ISBN-10 要 10 個字元。");
        int sum = 0;
        for (int i = 0; i < 10; i++)
        {
            char c = d[i];
            int v;
            if (c == 'X' && i == 9) v = 10;
            else if (char.IsDigit(c)) v = c - '0';
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
        foreach (char c in d) if (!char.IsDigit(c)) return Result.Fail("Digits only.", "只可用數字。");
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
        foreach (char c in d) if (!char.IsDigit(c)) return Result.Fail("Digits only.", "只可用數字。");
        if (d.Length != 13) return Result.Fail($"{label} needs 13 digits.", $"{labelZh} 要 13 個數字。");
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
        string raw = (input ?? "").Replace(" ", "").Replace("-", "").Trim().ToUpperInvariant();
        if (raw.Length == 0) return Result.Fail("Enter an IBAN.", "請輸入 IBAN。");
        if (raw.Length < 5 || raw.Length > 34) return Result.Fail("IBAN length must be 5–34.", "IBAN 長度要 5–34。");
        foreach (char c in raw)
            if (!(char.IsDigit(c) || (c >= 'A' && c <= 'Z')))
                return Result.Fail("IBAN uses letters & digits only.", "IBAN 只可用字母同數字。");
        if (!char.IsLetter(raw[0]) || !char.IsLetter(raw[1]))
            return Result.Fail("IBAN must start with a 2-letter country.", "IBAN 開頭要兩個國家字母。");

        // Move first 4 chars to the end, convert letters A=10..Z=35, mod 97 must == 1.
        string rearranged = raw.Substring(4) + raw.Substring(0, 4);
        var sb = new StringBuilder(rearranged.Length * 2);
        foreach (char c in rearranged)
        {
            if (char.IsDigit(c)) sb.Append(c);
            else sb.Append((c - 'A' + 10).ToString(CultureInfo.InvariantCulture));
        }
        if (!BigInteger.TryParse(sb.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger big))
            return Result.Fail("Could not parse IBAN.", "無法解析 IBAN。");
        int mod = (int)(big % 97);
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
        try
        {
            string country = raw.Substring(0, 2);
            string bban = raw.Substring(4);
            string rearranged = bban + country + "00";
            var sb = new StringBuilder();
            foreach (char c in rearranged)
            {
                if (char.IsDigit(c)) sb.Append(c);
                else if (c >= 'A' && c <= 'Z') sb.Append((c - 'A' + 10).ToString(CultureInfo.InvariantCulture));
                else return "??";
            }
            if (!BigInteger.TryParse(sb.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger big)) return "??";
            int mod = (int)(big % 97);
            int chk = 98 - mod;
            return chk.ToString("00", CultureInfo.InvariantCulture);
        }
        catch { return "??"; }
    }
}
