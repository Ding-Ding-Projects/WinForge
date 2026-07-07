using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// JWT inspector &amp; verifier · JWT 檢查同驗證. Pure-managed: split on '.', base64url-decode header/payload,
/// pretty-print JSON, extract standard claims, and verify HMAC (HS256/384/512) signatures locally.
/// Never throws — every method returns a result the UI can render as status. No network, no process launch.
/// </summary>
public static class JwtInspectService
{
    public sealed class DecodedJwt
    {
        public bool Ok { get; init; }
        public string? Error { get; init; }
        public string HeaderJson { get; init; } = "";
        public string PayloadJson { get; init; } = "";
        public string Signature { get; init; } = "";
        public string HeaderRaw { get; init; } = "";
        public string PayloadRaw { get; init; } = "";
        public string? Alg { get; init; }

        public string? Iss { get; init; }
        public string? Sub { get; init; }
        public string? Aud { get; init; }
        public string? Jti { get; init; }
        public long? Exp { get; init; }
        public long? Iat { get; init; }
        public long? Nbf { get; init; }
    }

    /// <summary>Decode a compact JWS. Never throws; sets Ok/Error.</summary>
    public static DecodedJwt Decode(string? token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return new DecodedJwt { Ok = false, Error = "empty" };

            var t = token.Trim();
            var parts = t.Split('.');
            if (parts.Length != 3)
                return new DecodedJwt { Ok = false, Error = "parts" };

            byte[] headerBytes, payloadBytes;
            try
            {
                headerBytes = FromBase64Url(parts[0]);
                payloadBytes = FromBase64Url(parts[1]);
            }
            catch
            {
                return new DecodedJwt { Ok = false, Error = "b64" };
            }

            string headerJson, payloadJson;
            string? alg = null;
            try
            {
                headerJson = Pretty(headerBytes);
                var hdrText = Encoding.UTF8.GetString(headerBytes);
                using var hdr = JsonDocument.Parse(hdrText);
                if (hdr.RootElement.ValueKind == JsonValueKind.Object &&
                    hdr.RootElement.TryGetProperty("alg", out var a) && a.ValueKind == JsonValueKind.String)
                    alg = a.GetString();
            }
            catch
            {
                return new DecodedJwt { Ok = false, Error = "hdrjson" };
            }

            string? iss = null, sub = null, aud = null, jti = null;
            long? exp = null, iat = null, nbf = null;
            try
            {
                payloadJson = Pretty(payloadBytes);
                var payText = Encoding.UTF8.GetString(payloadBytes);
                using var pay = JsonDocument.Parse(payText);
                var root = pay.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    iss = GetStr(root, "iss");
                    sub = GetStr(root, "sub");
                    aud = GetAud(root);
                    jti = GetStr(root, "jti");
                    exp = GetNum(root, "exp");
                    iat = GetNum(root, "iat");
                    nbf = GetNum(root, "nbf");
                }
            }
            catch
            {
                return new DecodedJwt { Ok = false, Error = "payjson" };
            }

            return new DecodedJwt
            {
                Ok = true,
                HeaderJson = headerJson,
                PayloadJson = payloadJson,
                Signature = parts[2],
                HeaderRaw = parts[0],
                PayloadRaw = parts[1],
                Alg = alg,
                Iss = iss,
                Sub = sub,
                Aud = aud,
                Jti = jti,
                Exp = exp,
                Iat = iat,
                Nbf = nbf,
            };
        }
        catch
        {
            return new DecodedJwt { Ok = false, Error = "unknown" };
        }
    }

    public enum TimeState { Unknown, Valid, Expired, NotYetValid }

    /// <summary>Validity state from exp/nbf vs now (UTC).</summary>
    public static TimeState EvaluateTime(DecodedJwt d)
    {
        try
        {
            var nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (d.Nbf is long nbf && nowSec < nbf) return TimeState.NotYetValid;
            if (d.Exp is long exp && nowSec >= exp) return TimeState.Expired;
            if (d.Exp is null && d.Nbf is null) return TimeState.Unknown;
            return TimeState.Valid;
        }
        catch
        {
            return TimeState.Unknown;
        }
    }

    public enum HmacAlg { HS256, HS384, HS512 }

    public enum VerifyResult { Valid, Invalid, Error }

    /// <summary>Recompute HMAC over "{header}.{payload}" with the secret, base64url-encode, compare to token signature.</summary>
    public static VerifyResult VerifyHmac(DecodedJwt d, string? secret, HmacAlg alg)
    {
        try
        {
            if (!d.Ok || secret is null) return VerifyResult.Error;
            var signingInput = Encoding.ASCII.GetBytes(d.HeaderRaw + "." + d.PayloadRaw);
            var key = Encoding.UTF8.GetBytes(secret);

            byte[] mac;
            switch (alg)
            {
                case HmacAlg.HS384:
                    using (var h = new HMACSHA384(key)) mac = h.ComputeHash(signingInput);
                    break;
                case HmacAlg.HS512:
                    using (var h = new HMACSHA512(key)) mac = h.ComputeHash(signingInput);
                    break;
                default:
                    using (var h = new HMACSHA256(key)) mac = h.ComputeHash(signingInput);
                    break;
            }

            var computed = ToBase64Url(mac);
            // constant-time-ish compare on the base64url strings
            var provided = d.Signature ?? "";
            if (computed.Length != provided.Length) return VerifyResult.Invalid;
            int diff = 0;
            for (int i = 0; i < computed.Length; i++) diff |= computed[i] ^ provided[i];
            return diff == 0 ? VerifyResult.Valid : VerifyResult.Invalid;
        }
        catch
        {
            return VerifyResult.Error;
        }
    }

    // ---- helpers ----

    private static string? GetStr(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static long? GetNum(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var l))
            return l;
        return null;
    }

    private static string? GetAud(JsonElement root)
    {
        if (!root.TryGetProperty("aud", out var v)) return null;
        if (v.ValueKind == JsonValueKind.String) return v.GetString();
        if (v.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var e in v.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.String) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(e.GetString());
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }
        return null;
    }

    private static string Pretty(byte[] utf8Json)
    {
        var text = Encoding.UTF8.GetString(utf8Json);
        using var doc = JsonDocument.Parse(text);
        return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static byte[] FromBase64Url(string s)
    {
        var t = s.Replace('-', '+').Replace('_', '/');
        switch (t.Length % 4)
        {
            case 2: t += "=="; break;
            case 3: t += "="; break;
            case 0: break;
            default: throw new FormatException("bad base64url length");
        }
        return Convert.FromBase64String(t);
    }

    private static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
