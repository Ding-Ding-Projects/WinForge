using System;
using System.Linq;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 驗證 RustDesk 官方 release asset · Validated metadata for one official RustDesk release asset.
/// </summary>
public sealed record RustDeskReleaseAsset(
    string Version,
    Uri DownloadUri,
    string Sha256,
    long Size,
    string FileName);

/// <summary>
/// RustDesk release feed policy · Pure validation for the official fallback installer path.
/// </summary>
public static class RustDeskRelease
{
    public const string LatestReleaseApi = "https://api.github.com/repos/rustdesk/rustdesk/releases/latest";
    public const string OfficialRepositoryUrl = "https://github.com/rustdesk/rustdesk";
    public const long MaximumInstallerBytes = 100 * 1024 * 1024;

    /// <summary>
    /// Recognise the catalog-unavailable result that is safe to recover from with the official release.
    /// Other WinGet failures remain visible and do not silently switch download sources.
    /// </summary>
    public static bool IsPackageUnavailable(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return false;
        var text = output.Trim();
        return text.Contains("no package found matching input criteria", StringComparison.OrdinalIgnoreCase)
            || text.Contains("no package found", StringComparison.OrdinalIgnoreCase)
                && text.Contains("matching", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parse and validate the latest-release response before any bytes are downloaded.
    /// </summary>
    public static bool TryParseLatest(string json, out RustDeskReleaseAsset? asset, out string error)
    {
        asset = null;
        error = "";

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "The official RustDesk release response was empty.";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "The official RustDesk release response was not an object.";
                return false;
            }

            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True
                || root.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True)
            {
                error = "The latest RustDesk release was marked as a draft or pre-release.";
                return false;
            }

            var tag = root.TryGetProperty("tag_name", out var tagElement)
                ? tagElement.GetString()?.Trim() ?? ""
                : "";
            var version = tag.TrimStart('v', 'V');
            if (!IsSafeVersion(version))
            {
                error = "The official RustDesk release used an invalid version tag.";
                return false;
            }

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                error = "The official RustDesk release did not provide an asset list.";
                return false;
            }

            foreach (var candidate in assets.EnumerateArray())
            {
                if (candidate.ValueKind != JsonValueKind.Object) continue;
                var name = candidate.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()?.Trim() ?? ""
                    : "";
                var expectedName = $"rustdesk-{version}-x86_64.exe";
                if (!name.Equals(expectedName, StringComparison.OrdinalIgnoreCase)) continue;

                var urlText = candidate.TryGetProperty("browser_download_url", out var urlElement)
                    ? urlElement.GetString()?.Trim() ?? ""
                    : "";
                if (!TryValidateDownloadUrl(urlText, tag, name, out var downloadUri))
                {
                    error = "The official RustDesk release asset URL was not trusted.";
                    return false;
                }

                var digest = candidate.TryGetProperty("digest", out var digestElement)
                    ? digestElement.GetString()?.Trim() ?? ""
                    : "";
                if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                    || digest.Length != "sha256:".Length + 64
                    || !IsHex(digest["sha256:".Length..]))
                {
                    error = "The official RustDesk release did not provide a valid SHA-256 digest.";
                    return false;
                }

                if (!candidate.TryGetProperty("size", out var sizeElement)
                    || sizeElement.ValueKind != JsonValueKind.Number
                    || !sizeElement.TryGetInt64(out var size)
                    || size <= 0
                    || size > MaximumInstallerBytes)
                {
                    error = "The official RustDesk installer size was missing or unsafe.";
                    return false;
                }

                asset = new RustDeskReleaseAsset(version, downloadUri!, digest["sha256:".Length..], size, name);
                return true;
            }

            error = $"The official RustDesk release did not contain {version}'s Windows x64 installer.";
            return false;
        }
        catch (JsonException)
        {
            error = "The official RustDesk release response was malformed JSON.";
            return false;
        }
        catch (Exception ex)
        {
            error = $"The official RustDesk release response could not be read: {ex.Message}";
            return false;
        }
    }

    private static bool TryValidateDownloadUrl(string text, string tag, string name, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var candidate)
            || candidate.Scheme != Uri.UriSchemeHttps
            || !candidate.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return false;

        var segments = candidate.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 6
            || !segments[0].Equals("rustdesk", StringComparison.OrdinalIgnoreCase)
            || !segments[1].Equals("rustdesk", StringComparison.OrdinalIgnoreCase)
            || !segments[2].Equals("releases", StringComparison.OrdinalIgnoreCase)
            || !segments[3].Equals("download", StringComparison.OrdinalIgnoreCase)
            || !Uri.UnescapeDataString(segments[4]).Equals(tag, StringComparison.Ordinal))
            return false;

        if (!Uri.UnescapeDataString(segments[5]).Equals(name, StringComparison.OrdinalIgnoreCase))
            return false;

        uri = candidate;
        return true;
    }

    private static bool IsSafeVersion(string value)
        => value.Length is >= 5 and <= 40
            && value[0] != '.'
            && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '+');

    private static bool IsHex(string value)
        => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}
