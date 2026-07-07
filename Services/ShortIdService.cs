using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 短碼編碼器 · Short ID encoder — pure managed base-N (Base62/58/36/Crockford32) for
/// non-negative integers, plus NanoID-style random IDs via a cryptographic RNG.
/// Never throws: callers get a clear ok/error result. No processes, no NuGet.
/// </summary>
public static class ShortIdService
{
    // Alphabets. Order matters — index = digit value.
    public const string Base62 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    public const string Base58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz"; // Bitcoin (no 0 O I l)
    public const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";
    public const string Crockford32 = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // no I L O U
    public const string UrlSafe = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    public readonly struct Result
    {
        public bool Ok { get; init; }
        public string Value { get; init; }
        public string? Error { get; init; }
        public static Result Good(string v) => new() { Ok = true, Value = v };
        public static Result Bad(string err) => new() { Ok = false, Value = "", Error = err };
    }

    /// <summary>Encode a non-negative integer into a compact string using <paramref name="alphabet"/>.</summary>
    public static Result Encode(BigInteger value, string alphabet)
    {
        try
        {
            if (string.IsNullOrEmpty(alphabet) || alphabet.Length < 2)
                return Result.Bad("bad-alphabet");
            if (value.Sign < 0)
                return Result.Bad("negative");

            int b = alphabet.Length;
            if (value.IsZero) return Result.Good(alphabet[0].ToString());

            var sb = new StringBuilder();
            var n = value;
            var big = new BigInteger(b);
            while (n > 0)
            {
                n = BigInteger.DivRem(n, big, out var rem);
                sb.Insert(0, alphabet[(int)rem]);
            }
            return Result.Good(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result.Bad("encode:" + ex.GetType().Name);
        }
    }

    /// <summary>Decode a code (in <paramref name="alphabet"/>) back to its non-negative integer.</summary>
    public static Result Decode(string code, string alphabet)
    {
        try
        {
            if (string.IsNullOrEmpty(alphabet) || alphabet.Length < 2)
                return Result.Bad("bad-alphabet");
            code = (code ?? "").Trim();
            if (code.Length == 0) return Result.Bad("empty");

            int b = alphabet.Length;
            var big = new BigInteger(b);
            BigInteger acc = BigInteger.Zero;
            foreach (char c in code)
            {
                int d = alphabet.IndexOf(c);
                if (d < 0) return Result.Bad("digit:" + c);
                acc = acc * big + d;
            }
            return Result.Good(acc.ToString());
        }
        catch (Exception ex)
        {
            return Result.Bad("decode:" + ex.GetType().Name);
        }
    }

    /// <summary>Generate <paramref name="count"/> NanoID-style random IDs, one per element,
    /// each <paramref name="length"/> chars drawn uniformly (unbiased) from <paramref name="alphabet"/>.</summary>
    public static Result GenerateMany(int length, int count, string alphabet)
    {
        try
        {
            if (string.IsNullOrEmpty(alphabet) || alphabet.Length < 2)
                return Result.Bad("bad-alphabet");
            if (length < 1) length = 1;
            if (length > 512) length = 512;
            if (count < 1) count = 1;
            if (count > 1000) count = 1000;

            var sb = new StringBuilder(count * (length + 2));
            int b = alphabet.Length;
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append('\n');
                for (int j = 0; j < length; j++)
                    sb.Append(alphabet[RandomNumberGenerator.GetInt32(b)]); // unbiased
            }
            return Result.Good(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result.Bad("random:" + ex.GetType().Name);
        }
    }

    /// <summary>Resolve a friendly alphabet name to its character set. Falls back to Base62.</summary>
    public static string Resolve(string? name) => name switch
    {
        "Base62" => Base62,
        "Base58" => Base58,
        "Base36" => Base36,
        "Crockford32" => Crockford32,
        "UrlSafe" => UrlSafe,
        _ => Base62,
    };
}
