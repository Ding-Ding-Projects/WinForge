using System;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// Ascii85 / Base85 · 純託管 C# 編碼器/解碼器 (managed, never-throws). Supports three variants:
///   • Adobe Ascii85 — 5 chars per 4 bytes over '!'..'u', optional &lt;~ ~&gt; wrapping, 'z' zero-run shortcut.
///   • Z85 (ZeroMQ RFC 32) — 85-char alphabet, requires input length %4 == 0.
///   • RFC 1924 (IPv6) — its own 85-char alphabet, big-endian, no padding shortcuts.
/// All public methods return a <see cref="Result"/>; they set Ok=false + a bilingual-friendly key
/// instead of throwing. Bytes in / string out and vice-versa are the caller's job (UTF-8 / hex).
/// </summary>
public static class Ascii85Service
{
    public enum Variant { Adobe, Z85, Rfc1924 }
    public enum InputKind { Utf8Text, HexBytes }

    /// <summary>Outcome of an encode/decode. On failure <see cref="Error"/> is a bilingual message.</summary>
    public readonly struct Result
    {
        public bool Ok { get; }
        public string? Text { get; }
        public byte[]? Bytes { get; }
        public string? Error { get; }
        private Result(bool ok, string? text, byte[]? bytes, string? error)
        { Ok = ok; Text = text; Bytes = bytes; Error = error; }

        public static Result Success(string text) => new(true, text, null, null);
        public static Result Success(byte[] bytes) => new(true, null, bytes, null);
        public static Result Fail(string error) => new(false, null, null, error);
    }

    // Z85 alphabet (RFC 32 / ZeroMQ).
    private const string Z85Alphabet =
        "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ.-:+=^!/*?&<>()[]{}@%$#";
    // RFC 1924 alphabet (IPv6 addresses / general 85-radix).
    private const string Rfc1924Alphabet =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz!#$%&()*+-;<=>?@^_`{|}~";

    private static readonly int[] Z85Decode = BuildDecode(Z85Alphabet);
    private static readonly int[] Rfc1924Decode = BuildDecode(Rfc1924Alphabet);

    private static int[] BuildDecode(string alphabet)
    {
        var map = new int[128];
        for (int i = 0; i < map.Length; i++) map[i] = -1;
        for (int i = 0; i < alphabet.Length; i++) map[alphabet[i]] = i;
        return map;
    }

    // ---------------------------------------------------------------- bytes <-> input string

    /// <summary>Turn UTF-8 text or a hex string into a byte buffer. Whitespace in hex is ignored.</summary>
    public static Result InputToBytes(string? input, InputKind kind)
    {
        input ??= string.Empty;
        try
        {
            if (kind == InputKind.Utf8Text)
                return Result.Success(Encoding.UTF8.GetBytes(input));

            // Hex: strip whitespace, 0x prefixes, and common separators.
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsWhiteSpace(c) || c == ':' || c == '-' || c == ',') continue;
                if ((c == 'x' || c == 'X') && sb.Length > 0 && sb[sb.Length - 1] == '0') { sb.Length -= 1; continue; }
                sb.Append(c);
            }
            string hex = sb.ToString();
            if ((hex.Length & 1) != 0)
                return Result.Fail("Hex input must have an even number of digits. · 十六進位輸入位數要成雙。");
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int hi = HexVal(hex[i * 2]), lo = HexVal(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0)
                    return Result.Fail("Hex input has a non-hex character. · 十六進位輸入有非法字元。");
                bytes[i] = (byte)((hi << 4) | lo);
            }
            return Result.Success(bytes);
        }
        catch (Exception ex)
        {
            return Result.Fail("Could not read the input. · 讀唔到輸入。 (" + ex.Message + ")");
        }
    }

    private static int HexVal(char c)
        => c >= '0' && c <= '9' ? c - '0'
         : c >= 'a' && c <= 'f' ? c - 'a' + 10
         : c >= 'A' && c <= 'F' ? c - 'A' + 10
         : -1;

    /// <summary>Render bytes as a spaced, upper-case hex string (e.g. "48 65 6C").</summary>
    public static string BytesToHex(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return string.Empty;
        var sb = new StringBuilder(bytes.Length * 3);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- encode

    /// <summary>Encode a byte buffer to a base85 string for the chosen variant.</summary>
    public static Result Encode(byte[]? bytes, Variant variant, bool adobeWrap, bool adobeZ)
    {
        bytes ??= Array.Empty<byte>();
        try
        {
            return variant switch
            {
                Variant.Adobe => Result.Success(EncodeAdobe(bytes, adobeWrap, adobeZ)),
                Variant.Z85 => EncodeZ85(bytes),
                Variant.Rfc1924 => Result.Success(EncodeAlphabet(bytes, Rfc1924Alphabet)),
                _ => Result.Fail("Unknown variant. · 未知變體。"),
            };
        }
        catch (Exception ex)
        {
            return Result.Fail("Encoding failed. · 編碼失敗。 (" + ex.Message + ")");
        }
    }

    private static string EncodeAdobe(byte[] data, bool wrap, bool useZ)
    {
        var sb = new StringBuilder();
        if (wrap) sb.Append("<~");
        int i = 0;
        while (i < data.Length)
        {
            int n = Math.Min(4, data.Length - i);
            uint tuple = 0;
            for (int k = 0; k < 4; k++)
                tuple = (tuple << 8) | (k < n ? data[i + k] : 0u);

            if (n == 4 && useZ && tuple == 0)
            {
                sb.Append('z');
            }
            else
            {
                Span<char> five = stackalloc char[5];
                for (int k = 4; k >= 0; k--) { five[k] = (char)('!' + (int)(tuple % 85)); tuple /= 85; }
                // For a partial group, emit n+1 chars only.
                sb.Append(five.Slice(0, n + 1).ToString());
            }
            i += 4;
        }
        if (wrap) sb.Append("~>");
        return sb.ToString();
    }

    private static Result EncodeZ85(byte[] data)
    {
        if ((data.Length & 3) != 0)
            return Result.Fail("Z85 needs the byte length to be a multiple of 4. · Z85 要求位元組長度係 4 嘅倍數。");
        var sb = new StringBuilder(data.Length / 4 * 5);
        for (int i = 0; i < data.Length; i += 4)
        {
            uint tuple = ((uint)data[i] << 24) | ((uint)data[i + 1] << 16) | ((uint)data[i + 2] << 8) | data[i + 3];
            Span<char> five = stackalloc char[5];
            for (int k = 4; k >= 0; k--) { five[k] = Z85Alphabet[(int)(tuple % 85)]; tuple /= 85; }
            sb.Append(five);
        }
        return Result.Success(sb.ToString());
    }

    // Generic 4-byte -> 5-char big-endian encoder with tail padding (used for RFC 1924).
    private static string EncodeAlphabet(byte[] data, string alphabet)
    {
        var sb = new StringBuilder();
        int i = 0;
        while (i < data.Length)
        {
            int n = Math.Min(4, data.Length - i);
            uint tuple = 0;
            for (int k = 0; k < 4; k++)
                tuple = (tuple << 8) | (k < n ? data[i + k] : 0u);
            Span<char> five = stackalloc char[5];
            for (int k = 4; k >= 0; k--) { five[k] = alphabet[(int)(tuple % 85)]; tuple /= 85; }
            sb.Append(five.Slice(0, n + 1).ToString());
            i += 4;
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- decode

    /// <summary>Decode a base85 string of the chosen variant back to raw bytes.</summary>
    public static Result Decode(string? text, Variant variant)
    {
        text ??= string.Empty;
        try
        {
            return variant switch
            {
                Variant.Adobe => DecodeAdobe(text),
                Variant.Z85 => DecodeStrict(text, Z85Decode, "Z85"),
                Variant.Rfc1924 => DecodeStrict(text, Rfc1924Decode, "RFC 1924"),
                _ => Result.Fail("Unknown variant. · 未知變體。"),
            };
        }
        catch (Exception ex)
        {
            return Result.Fail("Decoding failed. · 解碼失敗。 (" + ex.Message + ")");
        }
    }

    private static Result DecodeAdobe(string text)
    {
        // Trim optional <~ ~> wrapper (either or both may be present / partial).
        int start = 0, end = text.Length;
        int lt = text.IndexOf("<~", StringComparison.Ordinal);
        if (lt >= 0) start = lt + 2;
        int gt = text.IndexOf("~>", start, StringComparison.Ordinal);
        if (gt >= 0) end = gt;

        var outBytes = new System.Collections.Generic.List<byte>();
        Span<int> group = stackalloc int[5];
        int count = 0;

        for (int idx = start; idx < end; idx++)
        {
            char c = text[idx];
            if (char.IsWhiteSpace(c)) continue;
            if (c == 'z')
            {
                if (count != 0)
                    return Result.Fail("'z' shortcut must sit on a group boundary. · 'z' 縮寫要喺分組邊界。");
                outBytes.Add(0); outBytes.Add(0); outBytes.Add(0); outBytes.Add(0);
                continue;
            }
            if (c < '!' || c > 'u')
                return Result.Fail("Ascii85 has a character outside '!'..'u'. · Ascii85 有超出 '!'..'u' 嘅字元。");
            group[count++] = c - '!';
            if (count == 5)
            {
                EmitTuple(group, 5, outBytes);
                count = 0;
            }
        }

        if (count == 1)
            return Result.Fail("Ascii85 group of length 1 is invalid. · Ascii85 只剩一個字元嘅分組無效。");
        if (count > 0)
        {
            for (int k = count; k < 5; k++) group[k] = 84; // pad with 'u'
            EmitTuple(group, count, outBytes);
        }
        return Result.Success(outBytes.ToArray());
    }

    private static void EmitTuple(Span<int> group, int count, System.Collections.Generic.List<byte> sink)
    {
        uint tuple = 0;
        for (int k = 0; k < 5; k++) tuple = tuple * 85 + (uint)group[k];
        int bytesOut = count - 1; // full group -> 4 bytes; partial -> count-1
        Span<byte> b = stackalloc byte[4];
        b[0] = (byte)(tuple >> 24); b[1] = (byte)(tuple >> 16); b[2] = (byte)(tuple >> 8); b[3] = (byte)tuple;
        for (int k = 0; k < bytesOut; k++) sink.Add(b[k]);
    }

    // Strict fixed-alphabet decoder: length must be a multiple of 5, every char in alphabet.
    private static Result DecodeStrict(string text, int[] decode, string label)
    {
        // Allow surrounding whitespace but not interior separators for these strict variants.
        var sb = new StringBuilder(text.Length);
        foreach (char c in text) if (!char.IsWhiteSpace(c)) sb.Append(c);
        string s = sb.ToString();

        if (s.Length % 5 != 0)
            return Result.Fail($"{label} length must be a multiple of 5. · {label} 長度要係 5 嘅倍數。");

        var outBytes = new byte[s.Length / 5 * 4];
        int oi = 0;
        for (int i = 0; i < s.Length; i += 5)
        {
            ulong tuple = 0;
            for (int k = 0; k < 5; k++)
            {
                char c = s[i + k];
                int v = c < 128 ? decode[c] : -1;
                if (v < 0)
                    return Result.Fail($"{label} has an invalid character. · {label} 有非法字元。");
                tuple = tuple * 85 + (ulong)v;
            }
            if (tuple > 0xFFFFFFFFUL)
                return Result.Fail($"{label} group overflows 32 bits. · {label} 分組超出 32 位。");
            outBytes[oi++] = (byte)(tuple >> 24);
            outBytes[oi++] = (byte)(tuple >> 16);
            outBytes[oi++] = (byte)(tuple >> 8);
            outBytes[oi++] = (byte)tuple;
        }
        return Result.Success(outBytes);
    }

    /// <summary>Best-effort UTF-8 rendering of decoded bytes for the "text" output box.</summary>
    public static string BytesToUtf8(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0) return string.Empty;
        try { return new UTF8Encoding(false, false).GetString(bytes); }
        catch { return string.Empty; }
    }
}
