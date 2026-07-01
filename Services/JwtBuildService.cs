using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// JWT 建立同驗證（HMAC）· Pure-managed JWT builder + verifier for HS256/HS384/HS512.
/// All local, never throws — every entry point returns a small result record with a
/// bilingual-ready error string instead of raising. No network, no external processes.
/// </summary>
public static class JwtBuildService
{
    /// <summary>Supported HMAC algorithm identifiers (the "alg" header value).</summary>
    public static readonly string[] Algorithms = { "HS256", "HS384", "HS512" };

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    // ---- Base64Url ---------------------------------------------------------

    public static string B64UrlEncode(byte[] bytes)
    {
        string s = Convert.ToBase64String(bytes);
        return s.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static byte[] B64UrlDecode(string s)
    {
        string t = (s ?? string.Empty).Replace('-', '+').Replace('_', '/');
        switch (t.Length % 4)
        {
            case 2: t += "=="; break;
            case 3: t += "="; break;
        }
        return Convert.FromBase64String(t);
    }

    private static byte[] Hmac(string alg, byte[] key, byte[] data)
    {
        switch ((alg ?? string.Empty).ToUpperInvariant())
        {
            case "HS384": using (var h = new HMACSHA384(key)) return h.ComputeHash(data);
            case "HS512": using (var h = new HMACSHA512(key)) return h.ComputeHash(data);
            default: using (var h = new HMACSHA256(key)) return h.ComputeHash(data);
        }
    }

    /// <summary>Reformat a JSON string with indentation; returns null on invalid JSON.</summary>
    public static string? TryPretty(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json ?? string.Empty);
            return JsonSerializer.Serialize(doc.RootElement, Pretty);
        }
        catch { return null; }
    }

    /// <summary>Validate + minify a JSON string; returns null (compact via out) on invalid JSON.</summary>
    private static bool TryMinify(string json, out string compact)
    {
        compact = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json ?? string.Empty);
            compact = JsonSerializer.Serialize(doc.RootElement);
            return true;
        }
        catch { return false; }
    }

    // ---- Sign --------------------------------------------------------------

    public readonly record struct SignResult(bool Ok, string Token, string? BadField);

    /// <summary>
    /// Build a compact JWT from a header + payload JSON and a secret. On JSON/secret error
    /// returns Ok=false with BadField = "header" | "payload" | "secret".
    /// </summary>
    public static SignResult Sign(string headerJson, string payloadJson, string alg, string secret)
    {
        try
        {
            if (string.IsNullOrEmpty(secret)) return new SignResult(false, string.Empty, "secret");
            if (!TryMinify(headerJson, out var hMin)) return new SignResult(false, string.Empty, "header");
            if (!TryMinify(payloadJson, out var pMin)) return new SignResult(false, string.Empty, "payload");

            string encHeader = B64UrlEncode(Encoding.UTF8.GetBytes(hMin));
            string encPayload = B64UrlEncode(Encoding.UTF8.GetBytes(pMin));
            string signingInput = encHeader + "." + encPayload;

            byte[] sig = Hmac(alg, Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signingInput));
            string encSig = B64UrlEncode(sig);
            return new SignResult(true, signingInput + "." + encSig, null);
        }
        catch
        {
            return new SignResult(false, string.Empty, "payload");
        }
    }

    // ---- Verify ------------------------------------------------------------

    /// <summary>Outcome of verifying a token. Error is a stable key for the UI to localize.</summary>
    public readonly record struct VerifyResult(
        bool Ok,
        bool SignatureValid,
        string HeaderPretty,
        string PayloadPretty,
        string? Error,      // "format" | "header" | "payload" | "secret" | null
        bool? Expired,      // null = no exp claim
        bool? NotYetValid,  // null = no nbf claim
        DateTimeOffset? Exp,
        DateTimeOffset? Nbf);

    /// <summary>
    /// Recompute the HMAC over the token and compare, and pretty-print header + payload.
    /// Also flags exp/nbf against the current time. Never throws.
    /// </summary>
    public static VerifyResult Verify(string token, string alg, string secret)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return new VerifyResult(false, false, string.Empty, string.Empty, "format", null, null, null, null);
            if (string.IsNullOrEmpty(secret))
                return new VerifyResult(false, false, string.Empty, string.Empty, "secret", null, null, null, null);

            string[] parts = token.Trim().Split('.');
            if (parts.Length != 3)
                return new VerifyResult(false, false, string.Empty, string.Empty, "format", null, null, null, null);

            string headerPretty, payloadPretty;
            try
            {
                string headerJson = Encoding.UTF8.GetString(B64UrlDecode(parts[0]));
                headerPretty = TryPretty(headerJson) ?? throw new FormatException();
            }
            catch
            {
                return new VerifyResult(false, false, string.Empty, string.Empty, "header", null, null, null, null);
            }

            JsonDocument payloadDoc;
            try
            {
                string payloadJson = Encoding.UTF8.GetString(B64UrlDecode(parts[1]));
                payloadDoc = JsonDocument.Parse(payloadJson);
                payloadPretty = JsonSerializer.Serialize(payloadDoc.RootElement, Pretty);
            }
            catch
            {
                return new VerifyResult(false, false, headerPretty, string.Empty, "payload", null, null, null, null);
            }

            // Recompute signature over the exact signing input from the token.
            byte[] expected = Hmac(alg, Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]));
            byte[] actual;
            try { actual = B64UrlDecode(parts[2]); }
            catch { actual = Array.Empty<byte>(); }

            bool sigValid = expected.Length == actual.Length &&
                            CryptographicOperations.FixedTimeEquals(expected, actual);

            // exp / nbf checks
            bool? expired = null, notYet = null;
            DateTimeOffset? expAt = null, nbfAt = null;
            var now = DateTimeOffset.UtcNow;
            var root = payloadDoc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (TryEpoch(root, "exp", out var e)) { expAt = e; expired = now > e; }
                if (TryEpoch(root, "nbf", out var n)) { nbfAt = n; notYet = now < n; }
            }
            payloadDoc.Dispose();

            return new VerifyResult(true, sigValid, headerPretty, payloadPretty, null, expired, notYet, expAt, nbfAt);
        }
        catch
        {
            return new VerifyResult(false, false, string.Empty, string.Empty, "format", null, null, null, null);
        }
    }

    private static bool TryEpoch(JsonElement obj, string name, out DateTimeOffset value)
    {
        value = default;
        if (obj.TryGetProperty(name, out var el) &&
            el.ValueKind == JsonValueKind.Number &&
            el.TryGetInt64(out var secs))
        {
            try { value = DateTimeOffset.FromUnixTimeSeconds(secs); return true; }
            catch { return false; }
        }
        return false;
    }

    // ---- Claim quick-add ---------------------------------------------------

    /// <summary>
    /// Patch a payload JSON with a standard claim (adds/overwrites the key). Returns null on
    /// invalid JSON so the caller can surface a bilingual error. Times are Unix seconds.
    /// </summary>
    public static string? PatchClaim(string payloadJson, string claim)
    {
        try
        {
            System.Text.Json.Nodes.JsonNode? node;
            try
            {
                node = System.Text.Json.Nodes.JsonNode.Parse(
                    string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson);
            }
            catch { return null; }

            if (node is not System.Text.Json.Nodes.JsonObject obj) return null;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            switch (claim)
            {
                case "iat": obj["iat"] = now; break;
                case "exp": obj["exp"] = now + 3600; break;
                case "nbf": obj["nbf"] = now; break;
                case "sub": obj["sub"] = "1234567890"; break;
                case "iss": obj["iss"] = "winforge"; break;
                case "aud": obj["aud"] = "winforge-app"; break;
                default: return null;
            }
            return obj.ToJsonString(Pretty);
        }
        catch { return null; }
    }
}
