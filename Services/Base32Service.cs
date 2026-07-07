using System;
using System.Numerics;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// Base32 / Base58 / Ascii85 編解碼 · From-scratch codecs over UTF-8 bytes.
/// Base32: RFC 4648 (with / without padding). Base58: Bitcoin alphabet via BigInteger.
/// Ascii85: Adobe, 5-char/4-byte groups with the 'z' all-zero shortcut and optional &lt;~ ~&gt; markers.
/// Pure managed; never launches anything. Decode throws <see cref="FormatException"/> on malformed input.
/// </summary>
public static class Base32Service
{
    public enum Codec { Base32, Base32NoPad, Base58, Ascii85 }

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    /// <summary>Encode the UTF-8 bytes of <paramref name="text"/> with the selected codec.</summary>
    public static string Encode(string text, Codec codec)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        return codec switch
        {
            Codec.Base32 => Base32Encode(bytes, pad: true),
            Codec.Base32NoPad => Base32Encode(bytes, pad: false),
            Codec.Base58 => Base58Encode(bytes),
            Codec.Ascii85 => Ascii85Encode(bytes),
            _ => string.Empty,
        };
    }

    /// <summary>Decode <paramref name="text"/> with the selected codec back to a UTF-8 string.</summary>
    public static string Decode(string text, Codec codec)
    {
        byte[] bytes = codec switch
        {
            Codec.Base32 or Codec.Base32NoPad => Base32Decode(text ?? string.Empty),
            Codec.Base58 => Base58Decode(text ?? string.Empty),
            Codec.Ascii85 => Ascii85Decode(text ?? string.Empty),
            _ => Array.Empty<byte>(),
        };
        return Encoding.UTF8.GetString(bytes);
    }

    // ---------------- Base32 (RFC 4648) ----------------

    private static string Base32Encode(byte[] data, bool pad)
    {
        if (data.Length == 0) return string.Empty;
        var sb = new StringBuilder((data.Length + 4) / 5 * 8);
        int buffer = 0, bitsLeft = 0;
        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
            sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        if (pad)
            while (sb.Length % 8 != 0) sb.Append('=');
        return sb.ToString();
    }

    private static byte[] Base32Decode(string s)
    {
        if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
        var bytes = new System.Collections.Generic.List<byte>(s.Length * 5 / 8 + 1);
        int buffer = 0, bitsLeft = 0;
        foreach (char raw in s)
        {
            char c = char.ToUpperInvariant(raw);
            if (c == '=' || char.IsWhiteSpace(c) || c == '-') continue;
            int val = Base32Alphabet.IndexOf(c);
            if (val < 0) throw new FormatException($"Invalid Base32 character '{raw}'.");
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return bytes.ToArray();
    }

    // ---------------- Base58 (Bitcoin) ----------------

    private static string Base58Encode(byte[] data)
    {
        if (data.Length == 0) return string.Empty;
        int zeros = 0;
        while (zeros < data.Length && data[zeros] == 0) zeros++;

        // Interpret big-endian bytes as a non-negative BigInteger.
        var num = new BigInteger(0);
        foreach (byte b in data) num = num * 256 + b;

        var sb = new StringBuilder();
        while (num > 0)
        {
            num = BigInteger.DivRem(num, 58, out BigInteger rem);
            sb.Insert(0, Base58Alphabet[(int)rem]);
        }
        for (int i = 0; i < zeros; i++) sb.Insert(0, Base58Alphabet[0]); // '1'
        return sb.ToString();
    }

    private static byte[] Base58Decode(string s)
    {
        if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
        var trimmed = s.Trim();
        var num = new BigInteger(0);
        int leading = 0;
        bool sawNonLeading = false;
        foreach (char c in trimmed)
        {
            if (char.IsWhiteSpace(c)) continue;
            int val = Base58Alphabet.IndexOf(c);
            if (val < 0) throw new FormatException($"Invalid Base58 character '{c}'.");
            if (!sawNonLeading && val == 0) leading++;
            else sawNonLeading = true;
            num = num * 58 + val;
        }

        var bytes = new System.Collections.Generic.List<byte>();
        while (num > 0)
        {
            num = BigInteger.DivRem(num, 256, out BigInteger rem);
            bytes.Insert(0, (byte)rem);
        }
        for (int i = 0; i < leading; i++) bytes.Insert(0, 0);
        return bytes.ToArray();
    }

    // ---------------- Ascii85 (Adobe) ----------------

    private static string Ascii85Encode(byte[] data)
    {
        if (data.Length == 0) return "<~~>";
        var sb = new StringBuilder();
        sb.Append("<~");
        int i = 0;
        while (i < data.Length)
        {
            int count = Math.Min(4, data.Length - i);
            uint tuple = 0;
            for (int j = 0; j < 4; j++)
                tuple = (tuple << 8) | (j < count ? data[i + j] : (uint)0);

            if (count == 4 && tuple == 0)
            {
                sb.Append('z');
            }
            else
            {
                var group = new char[5];
                for (int k = 4; k >= 0; k--)
                {
                    group[k] = (char)('!' + (int)(tuple % 85));
                    tuple /= 85;
                }
                sb.Append(group, 0, count + 1);
            }
            i += 4;
        }
        sb.Append("~>");
        return sb.ToString();
    }

    private static byte[] Ascii85Decode(string s)
    {
        if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
        string body = s.Trim();
        if (body.StartsWith("<~")) body = body.Substring(2);
        int end = body.IndexOf("~>", StringComparison.Ordinal);
        if (end >= 0) body = body.Substring(0, end);

        var bytes = new System.Collections.Generic.List<byte>();
        uint tuple = 0;
        int count = 0;
        foreach (char c in body)
        {
            if (char.IsWhiteSpace(c)) continue;
            if (c == 'z')
            {
                if (count != 0) throw new FormatException("Ascii85 'z' inside a group.");
                bytes.Add(0); bytes.Add(0); bytes.Add(0); bytes.Add(0);
                continue;
            }
            if (c < '!' || c > 'u') throw new FormatException($"Invalid Ascii85 character '{c}'.");
            tuple = tuple * 85 + (uint)(c - '!');
            if (++count == 5)
            {
                bytes.Add((byte)(tuple >> 24));
                bytes.Add((byte)(tuple >> 16));
                bytes.Add((byte)(tuple >> 8));
                bytes.Add((byte)tuple);
                tuple = 0;
                count = 0;
            }
        }
        if (count == 1) throw new FormatException("Ascii85 group has a single trailing character.");
        if (count > 0)
        {
            for (int k = count; k < 5; k++) tuple = tuple * 85 + 84; // pad with 'u'
            for (int k = 0; k < count - 1; k++)
                bytes.Add((byte)(tuple >> (24 - k * 8)));
        }
        return bytes.ToArray();
    }
}
