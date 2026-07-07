using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 編碼解碼工具箱 · Encoding/decoding toolkit — pure managed conversions for Base64, Base64URL,
/// URL percent-encoding, HTML entities, hex bytes and JWT decoding. No shelling out, no pickers.
/// Every method returns a value or throws a plain <see cref="FormatException"/> the UI can catch.
/// </summary>
public static class EncoderService
{
    /// <summary>Byte encodings offered for the byte-based modes.</summary>
    public enum TextEncoding { Utf8, Ascii }

    private static Encoding Enc(TextEncoding e) => e switch
    {
        TextEncoding.Ascii => Encoding.ASCII,
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    // ---- Base64 ---------------------------------------------------------
    public static string Base64Encode(string text, TextEncoding enc) =>
        Convert.ToBase64String(Enc(enc).GetBytes(text ?? string.Empty));

    public static string Base64Decode(string text, TextEncoding enc) =>
        Enc(enc).GetString(Convert.FromBase64String((text ?? string.Empty).Trim()));

    // ---- Base64URL ------------------------------------------------------
    public static string Base64UrlEncode(string text, TextEncoding enc)
    {
        string b64 = Convert.ToBase64String(Enc(enc).GetBytes(text ?? string.Empty));
        return b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string Base64UrlDecode(string text, TextEncoding enc) =>
        Enc(enc).GetString(Base64UrlToBytes(text));

    /// <summary>Base64URL string → raw bytes, restoring '+'/'/' and '=' padding.</summary>
    public static byte[] Base64UrlToBytes(string text)
    {
        string s = (text ?? string.Empty).Trim().Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
            case 1: throw new FormatException("Invalid Base64URL length.");
        }
        return Convert.FromBase64String(s);
    }

    // ---- URL percent-encoding ------------------------------------------
    public static string UrlEncode(string text) => Uri.EscapeDataString(text ?? string.Empty);
    public static string UrlDecode(string text) => Uri.UnescapeDataString(text ?? string.Empty);

    // ---- HTML entities --------------------------------------------------
    public static string HtmlEncode(string text) => System.Net.WebUtility.HtmlEncode(text ?? string.Empty);
    public static string HtmlDecode(string text) => System.Net.WebUtility.HtmlDecode(text ?? string.Empty);

    // ---- Hex bytes ------------------------------------------------------
    public static string HexEncode(string text, TextEncoding enc)
    {
        byte[] bytes = Enc(enc).GetBytes(text ?? string.Empty);
        var sb = new StringBuilder(bytes.Length * 3);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(bytes[i].ToString("X2"));
        }
        return sb.ToString();
    }

    public static string HexDecode(string text, TextEncoding enc) =>
        Enc(enc).GetString(HexToBytes(text));

    /// <summary>Parse a hex string, tolerating spaces, newlines and 0x prefixes.</summary>
    public static byte[] HexToBytes(string text)
    {
        var sb = new StringBuilder((text ?? string.Empty).Length);
        foreach (char c in text ?? string.Empty)
        {
            if (char.IsWhiteSpace(c)) continue;
            sb.Append(c);
        }
        string clean = sb.ToString();
        // Strip 0x / 0X prefixes anywhere they appear as byte separators.
        clean = clean.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        if (clean.Length % 2 != 0)
            throw new FormatException("Hex needs an even number of digits.");
        var bytes = new byte[clean.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            string pair = clean.Substring(i * 2, 2);
            if (!byte.TryParse(pair, System.Globalization.NumberStyles.HexNumber, null, out byte b))
                throw new FormatException($"'{pair}' is not a valid hex byte.");
            bytes[i] = b;
        }
        return bytes;
    }

    // ---- JWT ------------------------------------------------------------
    /// <summary>Result of decoding a JWT: pretty-printed header/payload + raw signature segment.</summary>
    public readonly record struct JwtParts(string Header, string Payload, string Signature, int SegmentCount);

    /// <summary>
    /// Decode a JWT (2- or 3-segment). Header and payload are base64url-decoded and pretty-printed as
    /// indented JSON; the signature is returned as its raw base64url segment (not verified).
    /// </summary>
    public static JwtParts DecodeJwt(string token)
    {
        string t = (token ?? string.Empty).Trim();
        if (t.Length == 0) throw new FormatException("Empty token.");
        string[] parts = t.Split('.');
        if (parts.Length < 2 || parts.Length > 3)
            throw new FormatException("A JWT has 2 or 3 dot-separated segments.");

        string header = PrettyJsonFromB64Url(parts[0]);
        string payload = PrettyJsonFromB64Url(parts[1]);
        string sig = parts.Length == 3 ? parts[2] : string.Empty;
        return new JwtParts(header, payload, sig, parts.Length);
    }

    private static string PrettyJsonFromB64Url(string segment)
    {
        byte[] raw = Base64UrlToBytes(segment);
        string json = new UTF8Encoding(false).GetString(raw);
        try
        {
            using var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            // Not JSON — return the decoded text verbatim rather than failing the whole decode.
            return json;
        }
    }
}
