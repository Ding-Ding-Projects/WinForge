using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 文字 ↔ 二進位／編碼 · Text ↔ binary / numeric-code converter. Pure managed, never throws.
/// Encodes a string's UTF-8 bytes as space-separated codes in a chosen base (binary padded to 8
/// bits), and parses such codes back to a UTF-8 string. No redirect, no external process.
/// </summary>
public static class BinaryTextService
{
    /// <summary>Numeric base used for each byte code.</summary>
    public enum NumBase { Binary = 2, Octal = 8, Decimal = 10, Hex = 16 }

    /// <summary>Result of a convert operation. <see cref="Ok"/> false means <see cref="Error"/> is set.</summary>
    public readonly struct Result
    {
        public bool Ok { get; }
        public string Text { get; }
        public string Error { get; }
        private Result(bool ok, string text, string error) { Ok = ok; Text = text; Error = error; }
        public static Result Good(string text) => new(true, text, string.Empty);
        public static Result Bad(string error) => new(false, string.Empty, error);
    }

    /// <summary>Encode <paramref name="input"/>'s UTF-8 bytes as space-separated codes in <paramref name="baseKind"/>.</summary>
    public static Result Encode(string? input, NumBase baseKind)
    {
        try
        {
            if (string.IsNullOrEmpty(input)) return Result.Good(string.Empty);
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            var sb = new StringBuilder(bytes.Length * 9);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Format(bytes[i], baseKind));
            }
            return Result.Good(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result.Bad(ex.Message);
        }
    }

    /// <summary>Parse space/line-separated codes in <paramref name="baseKind"/> back to a UTF-8 string.</summary>
    public static Result Decode(string? input, NumBase baseKind)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input)) return Result.Good(string.Empty);
            string[] tokens = input.Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var bytes = new List<byte>(tokens.Length);
            foreach (string raw in tokens)
            {
                string tok = Strip(raw, baseKind);
                if (tok.Length == 0) continue;
                if (!TryParse(tok, baseKind, out int value))
                    return Result.Bad($"tok:{raw}");
                if (value < 0 || value > 255)
                    return Result.Bad($"range:{raw}");
                bytes.Add((byte)value);
            }
            if (bytes.Count == 0) return Result.Good(string.Empty);
            // UTF-8 decode. Bad byte sequences fall back to replacement chars rather than throwing.
            return Result.Good(Encoding.UTF8.GetString(bytes.ToArray()));
        }
        catch (Exception ex)
        {
            return Result.Bad(ex.Message);
        }
    }

    private static string Format(byte b, NumBase baseKind) => baseKind switch
    {
        NumBase.Binary => Convert.ToString(b, 2).PadLeft(8, '0'),
        NumBase.Octal => Convert.ToString(b, 8),
        NumBase.Hex => b.ToString("X2"),
        _ => b.ToString(),
    };

    // Strip common prefixes (0x, 0b, 0o) so pasted codes still parse.
    private static string Strip(string tok, NumBase baseKind)
    {
        tok = tok.Trim();
        if (tok.Length > 2 && tok[0] == '0')
        {
            char p = char.ToLowerInvariant(tok[1]);
            if ((baseKind == NumBase.Hex && p == 'x') ||
                (baseKind == NumBase.Binary && p == 'b') ||
                (baseKind == NumBase.Octal && p == 'o'))
                return tok.Substring(2);
        }
        return tok;
    }

    private static bool TryParse(string tok, NumBase baseKind, out int value)
    {
        value = 0;
        try
        {
            foreach (char c in tok)
                if (!IsDigitFor(c, baseKind)) return false;
            value = Convert.ToInt32(tok, (int)baseKind);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsDigitFor(char c, NumBase baseKind)
    {
        int d;
        if (c >= '0' && c <= '9') d = c - '0';
        else if (c >= 'a' && c <= 'f') d = 10 + (c - 'a');
        else if (c >= 'A' && c <= 'F') d = 10 + (c - 'A');
        else return false;
        return d < (int)baseKind;
    }
}
