using System;

namespace WinForge.Services;

/// <summary>
/// Validates structured package-manager settings before they are persisted or appended to a command.
/// Free-form advanced arguments remain an explicit user-authored command surface; proxy endpoints and
/// vcpkg triplets are structured values and therefore fail closed.
/// </summary>
public static class PackageManagerInputPolicy
{
    public const int MaximumProxyUrlLength = 2048;
    public const int MaximumVcpkgTripletLength = 64;

    /// <summary>
    /// Accept a credential-free HTTP(S) proxy authority. Proxy paths, queries, fragments, raw percent
    /// expansion, quotes, and control characters are rejected because the normalized result is passed to
    /// command-line clients. An empty value is valid and means "no proxy".
    /// </summary>
    public static bool TryNormalizeProxyUrl(string? value, out string normalized)
    {
        normalized = "";
        var candidate = (value ?? "").Trim();
        if (candidate.Length == 0) return true;
        if (candidate.Length > MaximumProxyUrlLength) return false;

        foreach (var c in candidate)
            if (char.IsControl(c) || c is '"' or '%') return false;

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || (uri.AbsolutePath.Length > 0 && uri.AbsolutePath != "/"))
            return false;

        normalized = uri.GetLeftPart(UriPartial.Authority);
        return normalized.Length <= MaximumProxyUrlLength;
    }

    /// <summary>
    /// Accept a conventional vcpkg triplet token. Custom triplets remain supported, but shell syntax,
    /// whitespace, paths, quotes, and control characters cannot become part of the command line.
    /// An empty value is valid and selects vcpkg's default triplet.
    /// </summary>
    public static bool TryNormalizeVcpkgTriplet(string? value, out string normalized)
    {
        normalized = "";
        var candidate = (value ?? "").Trim();
        if (candidate.Length == 0) return true;
        if (candidate.Length > MaximumVcpkgTripletLength || !IsAsciiAlphaNumeric(candidate[0]))
            return false;

        foreach (var c in candidate)
            if (!(IsAsciiAlphaNumeric(c) || c is '.' or '_' or '-')) return false;

        normalized = candidate;
        return true;
    }

    private static bool IsAsciiAlphaNumeric(char c)
        => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}
