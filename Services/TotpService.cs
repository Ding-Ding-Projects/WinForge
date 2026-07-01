using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// TOTP/HOTP 驗證器 · RFC 6238 (TOTP) / RFC 4226 (HOTP) authenticator — pure managed C#.
/// Decodes a Base32 secret, computes the HMAC-based one-time code and parses
/// <c>otpauth://totp/...</c> URIs. Never throws; all parsing returns a success flag.
/// </summary>
public static class TotpService
{
    /// <summary>Supported HMAC algorithms.</summary>
    public enum HashAlgo { Sha1, Sha256, Sha512 }

    /// <summary>Parsed <c>otpauth://</c> fields; only the ones present are filled.</summary>
    public sealed class OtpAuth
    {
        public string Secret = "";
        public string? Label;
        public string? Issuer;
        public int Digits = 6;
        public int Period = 30;
        public HashAlgo Algorithm = HashAlgo.Sha1;
    }

    /// <summary>
    /// Compute the current TOTP code for <paramref name="unixSeconds"/>. Returns the zero-padded
    /// N-digit code, or null when the secret / parameters are invalid. Never throws.
    /// </summary>
    public static string? Compute(string base32Secret, int digits, int period, HashAlgo algo, long unixSeconds)
    {
        try
        {
            if (period <= 0) return null;
            if (digits < 1 || digits > 10) return null;
            var key = DecodeBase32(base32Secret);
            if (key is null || key.Length == 0) return null;

            long counter = unixSeconds / period;
            return Hotp(key, counter, digits, algo);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Seconds remaining in the current step (1..period).</summary>
    public static int SecondsRemaining(int period, long unixSeconds)
    {
        if (period <= 0) return 0;
        int used = (int)(unixSeconds % period);
        return period - used;
    }

    /// <summary>Current unix time in seconds (UTC).</summary>
    public static long UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>RFC 4226 HOTP: HMAC of the 8-byte counter, dynamic truncation → N digits.</summary>
    private static string Hotp(byte[] key, long counter, int digits, HashAlgo algo)
    {
        Span<byte> msg = stackalloc byte[8];
        for (int i = 7; i >= 0; i--)
        {
            msg[i] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        byte[] hash = algo switch
        {
            HashAlgo.Sha256 => HMACSHA256.HashData(key, msg),
            HashAlgo.Sha512 => HMACSHA512.HashData(key, msg),
            _ => HMACSHA1.HashData(key, msg),
        };

        int offset = hash[^1] & 0x0f;
        int binary =
            ((hash[offset] & 0x7f) << 24) |
            ((hash[offset + 1] & 0xff) << 16) |
            ((hash[offset + 2] & 0xff) << 8) |
            (hash[offset + 3] & 0xff);

        int mod = 1;
        for (int i = 0; i < digits; i++) mod *= 10;
        int otp = binary % mod;
        return otp.ToString().PadLeft(digits, '0');
    }

    /// <summary>Decode an RFC 4648 Base32 string (case-insensitive; ignores spaces, dashes and '=' padding). Null on invalid chars.</summary>
    public static byte[]? DecodeBase32(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var bits = new List<byte>(input.Length * 5 / 8 + 1);
        int buffer = 0, bitsLeft = 0;
        foreach (char raw in input)
        {
            char c = char.ToUpperInvariant(raw);
            if (c == '=' || c == ' ' || c == '-' || c == '\t' || c == '\r' || c == '\n') continue;

            int val = Base32Value(c);
            if (val < 0) return null;

            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bits.Add((byte)((buffer >> bitsLeft) & 0xff));
            }
        }
        return bits.ToArray();
    }

    private static int Base32Value(char c)
    {
        if (c >= 'A' && c <= 'Z') return c - 'A';       // 0..25
        if (c >= '2' && c <= '7') return c - '2' + 26;  // 26..31
        return -1;
    }

    /// <summary>
    /// Parse an <c>otpauth://totp/Label?secret=..&amp;issuer=..&amp;digits=..&amp;period=..&amp;algorithm=..</c> URI.
    /// Returns null when it is not a valid totp URI or has no secret. Never throws.
    /// </summary>
    public static OtpAuth? ParseUri(string? uri)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uri)) return null;
            if (!Uri.TryCreate(uri.Trim(), UriKind.Absolute, out var u)) return null;
            if (!string.Equals(u.Scheme, "otpauth", StringComparison.OrdinalIgnoreCase)) return null;
            if (!string.Equals(u.Host, "totp", StringComparison.OrdinalIgnoreCase)) return null;

            var result = new OtpAuth();

            string label = Uri.UnescapeDataString(u.AbsolutePath.TrimStart('/'));
            if (!string.IsNullOrEmpty(label)) result.Label = label;

            foreach (var kv in ParseQuery(u.Query))
            {
                switch (kv.Key.ToLowerInvariant())
                {
                    case "secret": result.Secret = kv.Value; break;
                    case "issuer": result.Issuer = kv.Value; break;
                    case "digits":
                        if (int.TryParse(kv.Value, out var d) && d >= 1 && d <= 10) result.Digits = d;
                        break;
                    case "period":
                        if (int.TryParse(kv.Value, out var p) && p > 0) result.Period = p;
                        break;
                    case "algorithm":
                        result.Algorithm = kv.Value.ToUpperInvariant() switch
                        {
                            "SHA256" => HashAlgo.Sha256,
                            "SHA512" => HashAlgo.Sha512,
                            _ => HashAlgo.Sha1,
                        };
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(result.Secret)) return null;
            return result;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        if (string.IsNullOrEmpty(query)) yield break;
        string q = query.StartsWith("?") ? query.Substring(1) : query;
        foreach (var part in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=');
            if (eq < 0)
                yield return new KeyValuePair<string, string>(Uri.UnescapeDataString(part), "");
            else
                yield return new KeyValuePair<string, string>(
                    Uri.UnescapeDataString(part.Substring(0, eq)),
                    Uri.UnescapeDataString(part.Substring(eq + 1)));
        }
    }
}
