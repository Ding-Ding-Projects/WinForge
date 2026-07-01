using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 程式員進位轉換 · Programmer number-base converter. Pure managed BigInteger maths — parse a value in an
/// arbitrary base (2–36, with optional leading '-'), render it back in binary/octal/decimal/hex/custom,
/// and evaluate bitwise ops. Every entry point is guarded (TryParse) so callers never see an exception.
/// </summary>
public static class BaseConvertService
{
    /// <summary>Lowest / highest supported radix.</summary>
    public const int MinBase = 2;
    public const int MaxBase = 36;

    /// <summary>
    /// Parse <paramref name="text"/> as a signed integer in <paramref name="radix"/> (2–36).
    /// Digits 0-9a-z (case-insensitive), one optional leading '+'/'-', and grouping via spaces/underscores
    /// are accepted. Returns false (never throws) for empty input, a bad radix, or any out-of-range digit.
    /// </summary>
    public static bool TryParse(string? text, int radix, out BigInteger value)
    {
        value = BigInteger.Zero;
        if (radix < MinBase || radix > MaxBase) return false;
        if (string.IsNullOrWhiteSpace(text)) return false;

        text = text.Trim();
        bool negative = false;
        int i = 0;
        if (text[0] == '+' || text[0] == '-')
        {
            negative = text[0] == '-';
            i = 1;
        }

        bool sawDigit = false;
        BigInteger big = BigInteger.Zero;
        BigInteger b = radix;
        for (; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '_' || c == ' ') continue; // grouping separators
            int digit = DigitValue(c);
            if (digit < 0 || digit >= radix) return false; // invalid digit for this base
            big = big * b + digit;
            sawDigit = true;
        }

        if (!sawDigit) return false;
        value = negative ? -big : big;
        return true;
    }

    private static int DigitValue(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'z') return c - 'a' + 10;
        if (c >= 'A' && c <= 'Z') return c - 'A' + 10;
        return -1;
    }

    /// <summary>Render <paramref name="value"/> in <paramref name="radix"/> (2–36), lowercase, no prefix.</summary>
    public static string ToBase(BigInteger value, int radix)
    {
        if (radix < MinBase || radix > MaxBase) return string.Empty;
        if (value.IsZero) return "0";

        bool negative = value.Sign < 0;
        BigInteger n = BigInteger.Abs(value);
        BigInteger b = radix;
        var sb = new StringBuilder();
        while (n > 0)
        {
            int d = (int)(n % b);
            sb.Insert(0, (char)(d < 10 ? '0' + d : 'a' + (d - 10)));
            n /= b;
        }
        if (negative) sb.Insert(0, '-');
        return sb.ToString();
    }

    /// <summary>Binary rendering grouped into nibbles of four bits, e.g. "1010 0110".</summary>
    public static string ToGroupedBinary(BigInteger value)
    {
        bool negative = value.Sign < 0;
        string bits = ToBase(BigInteger.Abs(value), 2);
        int pad = (4 - bits.Length % 4) % 4;
        if (pad > 0) bits = new string('0', pad) + bits;

        var sb = new StringBuilder();
        for (int i = 0; i < bits.Length; i++)
        {
            if (i > 0 && i % 4 == 0) sb.Append(' ');
            sb.Append(bits[i]);
        }
        return negative ? "-" + sb : sb.ToString();
    }

    /// <summary>Uppercase hexadecimal with a leading "0x" (or "-0x" for negatives).</summary>
    public static string ToHexPrefixed(BigInteger value)
    {
        if (value.Sign < 0) return "-0x" + ToBase(BigInteger.Abs(value), 16).ToUpperInvariant();
        return "0x" + ToBase(value, 16).ToUpperInvariant();
    }

    /// <summary>Number of significant bits in |value| (0 → 0, 1 → 1, 255 → 8, 256 → 9).</summary>
    public static long BitLength(BigInteger value)
    {
        BigInteger n = BigInteger.Abs(value);
        if (n.IsZero) return 0;
        long bits = 0;
        while (n > 0) { bits++; n >>= 1; }
        return bits;
    }

    /// <summary>True when the value fits in a signed 64-bit range (long.MinValue..long.MaxValue).</summary>
    public static bool FitsIn64Bits(BigInteger value) =>
        value >= long.MinValue && value <= long.MaxValue;

    /// <summary>Full 64-bit two's-complement binary, grouped into bytes, e.g. "00000000 ... 11111111".</summary>
    public static string To64BitBinary(BigInteger value)
    {
        ulong u = unchecked((ulong)(long)value);
        var sb = new StringBuilder(64 + 7);
        for (int bit = 63; bit >= 0; bit--)
        {
            sb.Append(((u >> bit) & 1) == 1 ? '1' : '0');
            if (bit % 8 == 0 && bit != 0) sb.Append(' ');
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parse an operand for the bitwise section — a plain decimal, or a hex literal when it starts with
    /// "0x"/"-0x". Guarded; false on any bad input.
    /// </summary>
    public static bool TryParseOperand(string? text, out BigInteger value)
    {
        value = BigInteger.Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;
        string t = text.Trim();

        bool negative = false;
        if (t.StartsWith("+")) t = t[1..];
        else if (t.StartsWith("-")) { negative = true; t = t[1..]; }

        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParse(t[2..], 16, out var hex)) return false;
            value = negative ? -hex : hex;
            return true;
        }

        if (!TryParse(t, 10, out var dec)) return false;
        value = negative ? -dec : dec;
        return true;
    }

    /// <summary>Supported bitwise operations.</summary>
    public enum BitOp { And, Or, Xor, Nand, Nor, LeftShift, RightShift }

    /// <summary>
    /// Evaluate a bitwise op on two operands (BigInteger semantics; NAND/NOR use the bitwise complement of
    /// the AND/OR result). Shift ops use <paramref name="shift"/> (clamped ≥ 0) instead of <paramref name="b"/>.
    /// </summary>
    public static BigInteger Evaluate(BitOp op, BigInteger a, BigInteger b, int shift)
    {
        if (shift < 0) shift = 0;
        return op switch
        {
            BitOp.And => a & b,
            BitOp.Or => a | b,
            BitOp.Xor => a ^ b,
            BitOp.Nand => ~(a & b),
            BitOp.Nor => ~(a | b),
            BitOp.LeftShift => a << shift,
            BitOp.RightShift => a >> shift,
            _ => BigInteger.Zero,
        };
    }
}
