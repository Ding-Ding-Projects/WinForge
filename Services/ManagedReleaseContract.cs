using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace WinForge.Services;

public sealed record ManagedReleaseAsset(
    string Name,
    string BrowserDownloadUrl,
    string? Digest,
    long Size);

public sealed record ManagedReleaseMetadata(
    string TagName,
    bool Draft,
    bool Prerelease,
    IReadOnlyList<ManagedReleaseAsset> Assets);

public sealed record ManagedReleaseSelection(
    string Version,
    ManagedReleaseAsset Installer,
    ManagedReleaseAsset Releases,
    ManagedReleaseAsset FullPackage,
    ManagedReleaseAsset Portable,
    string InstallerSha256,
    string ReleasesSha256,
    string FullPackageSha256,
    string PortableSha256);

public sealed record ManagedInstallLayout(
    string InstallDirectory,
    string LauncherPath,
    string ExecutablePath);

/// <summary>
/// Pure delivery contract shared by the app, visible updater, single-file launcher, release workflow,
/// and process-free tests. Changing the release owner, version line, asset names, or runtime footprint
/// is one intentional migration instead of a collection of unrelated string edits.
/// </summary>
public static class ManagedReleaseContract
{
    public const string RepositoryOwner = "Ding-Ding-Projects";
    public const string RepositoryName = "WinForge";
    public const string RepositorySlug = RepositoryOwner + "/" + RepositoryName;
    public const string RepositoryUrl = "https://github.com/" + RepositorySlug;
    public const string LatestReleaseApi = "https://api.github.com/repos/" + RepositorySlug + "/releases/latest";
    public const string InstallerAssetName = "Setup.exe";
    public const string SquirrelReleasesAssetName = "RELEASES";
    public const string SquirrelFullPackagePrefix = "WinForge-";
    public const string SquirrelFullPackageSuffix = "-full.nupkg";
    public const string SquirrelDeltaPackageSuffix = "-delta.nupkg";
    public const string PortableAssetPrefix = "WinForge-portable-x64-";
    public const string ReleaseManifestName = "WinForge.release.json";
    public const string LauncherFileName = "WinForgeLauncher.exe";
    public const string ExecutableFileName = "WinForge.exe";
    public const string UpdaterDirectoryName = "updater-runtime";
    public const string UpdaterFileName = "WinForgeUpdater.exe";
    public const int ReleaseMajor = 1;
    public const int ReleaseMinor = 1;
    public const int MaximumWindowsBuildComponent = 65_535;
    public const long MaximumInstallerBytes = 512L * 1024 * 1024;
    public const long MaximumSquirrelPackageBytes = 1024L * 1024 * 1024;
    public const long MaximumSquirrelReleasesBytes = 16L * 1024 * 1024;
    public const long MaximumPortableBytes = 1024L * 1024 * 1024;

    public static string PortableAssetName(string versionOrTag)
    {
        if (!TryParseReleaseVersion(versionOrTag, out Version? version))
            throw new ArgumentException("Version must use the managed v1.1.<1..65535> release line.", nameof(versionOrTag));
        return PortableAssetPrefix + FormatVersion(version) + ".zip";
    }

    public static string SquirrelFullPackageName(string versionOrTag)
    {
        if (!TryParseReleaseVersion(versionOrTag, out Version? version))
            throw new ArgumentException("Version must use the managed v1.1.<1..65535> release line.", nameof(versionOrTag));
        return SquirrelFullPackagePrefix + FormatVersion(version) + SquirrelFullPackageSuffix;
    }

    public static bool IsSquirrelDeltaPackageName(string? name, string versionOrTag)
    {
        if (!TryParseReleaseVersion(versionOrTag, out Version? version) || string.IsNullOrWhiteSpace(name)) return false;
        string prefix = SquirrelFullPackagePrefix;
        string suffix = SquirrelDeltaPackageSuffix;
        if (!name.StartsWith(prefix, StringComparison.Ordinal) ||
            !name.EndsWith(suffix, StringComparison.Ordinal) ||
            name.Length <= prefix.Length + suffix.Length)
            return false;

        string deltaVersion = name[prefix.Length..^suffix.Length];
        return TryParseReleaseVersion(deltaVersion, out Version? parsed) && parsed.Equals(version);
    }

    public static bool TryParseReleaseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        string normalized = NormalizeTag(value);
        string[] parts = normalized.Split('.', StringSplitOptions.None);
        if (parts.Length != 3) return false;
        if (!TryCanonicalComponent(parts[0], out int major) ||
            !TryCanonicalComponent(parts[1], out int minor) ||
            !TryCanonicalComponent(parts[2], out int build)) return false;
        if (major != ReleaseMajor || minor != ReleaseMinor || build is < 1 or > MaximumWindowsBuildComponent)
            return false;
        version = new Version(major, minor, build);
        return true;
    }

    public static bool IsNewerRelease(string? latestTag, string? currentVersion)
    {
        if (!TryParseReleaseVersion(latestTag, out Version? latest)) return false;
        if (!Version.TryParse(NormalizeTag(currentVersion), out Version? current)) return false;
        return latest > current;
    }

    public static string NormalizeTag(string? value)
    {
        string normalized = (value ?? string.Empty).Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V')) normalized = normalized[1..];
        return normalized;
    }

    public static string NormalizeSha256(string? value)
    {
        string digest = (value ?? string.Empty).Trim();
        if (digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) digest = digest[7..];
        return digest.Length == 64 && digest.All(Uri.IsHexDigit) ? digest.ToUpperInvariant() : string.Empty;
    }

    public static bool FixedTimeSha256Equals(string? expected, string? actual)
    {
        string normalizedExpected = NormalizeSha256(expected);
        string normalizedActual = NormalizeSha256(actual);
        if (normalizedExpected.Length != 64 || normalizedActual.Length != 64) return false;
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(normalizedExpected),
            Convert.FromHexString(normalizedActual));
    }

    public static bool TryResolveRelease(
        ManagedReleaseMetadata release,
        out ManagedReleaseSelection? selection,
        out string reason)
    {
        selection = null;
        reason = string.Empty;
        if (release.Draft || release.Prerelease)
        {
            reason = "The release is not a stable published release.";
            return false;
        }
        if (!TryParseReleaseVersion(release.TagName, out Version? version))
        {
            reason = "The release tag is outside the managed v1.1 version contract.";
            return false;
        }

        string normalizedVersion = FormatVersion(version);
        string portableName = PortableAssetName(normalizedVersion);
        ManagedReleaseAsset[] assets = release.Assets?.ToArray() ?? [];
        string fullPackageName = SquirrelFullPackageName(normalizedVersion);
        string[] requiredNames = [InstallerAssetName, SquirrelReleasesAssetName, fullPackageName, portableName];
        string[] actualNames = assets.Select(asset => asset.Name).Order(StringComparer.Ordinal).ToArray();
        bool duplicateAssetName = assets
            .GroupBy(asset => asset.Name, StringComparer.Ordinal)
            .Any(group => group.Count() != 1);
        if (duplicateAssetName ||
            requiredNames.Any(name => !actualNames.Contains(name, StringComparer.Ordinal)) ||
            assets.Any(asset => !requiredNames.Contains(asset.Name, StringComparer.Ordinal) &&
                                !IsSquirrelDeltaPackageName(asset.Name, normalizedVersion)))
        {
            reason = "The stable release must contain one Setup.exe, one RELEASES index, the versioned full Squirrel package, and the x64 portable archive; current-version delta packages are optional when a prior release exists.";
            return false;
        }

        ManagedReleaseAsset installer = assets.Single(asset => asset.Name == InstallerAssetName);
        ManagedReleaseAsset releases = assets.Single(asset => asset.Name == SquirrelReleasesAssetName);
        ManagedReleaseAsset fullPackage = assets.Single(asset => asset.Name == fullPackageName);
        ManagedReleaseAsset portable = assets.Single(asset => asset.Name == portableName);
        string installerDigest = NormalizeSha256(installer.Digest);
        string releasesDigest = NormalizeSha256(releases.Digest);
        string fullPackageDigest = NormalizeSha256(fullPackage.Digest);
        string portableDigest = NormalizeSha256(portable.Digest);
        if (installerDigest.Length != 64 || releasesDigest.Length != 64 || fullPackageDigest.Length != 64 || portableDigest.Length != 64)
        {
            reason = "One or more release assets do not carry a valid GitHub SHA-256 digest.";
            return false;
        }
        if (installer.Size is <= 0 or > MaximumInstallerBytes ||
            releases.Size is <= 0 or > MaximumSquirrelReleasesBytes ||
            fullPackage.Size is <= 0 or > MaximumSquirrelPackageBytes ||
            portable.Size is <= 0 or > MaximumPortableBytes)
        {
            reason = "One or more release assets are empty or exceed their bounded size contract.";
            return false;
        }
        if (!IsCanonicalReleaseDownload(installer.BrowserDownloadUrl, release.TagName, InstallerAssetName) ||
            !IsCanonicalReleaseDownload(releases.BrowserDownloadUrl, release.TagName, SquirrelReleasesAssetName) ||
            !IsCanonicalReleaseDownload(fullPackage.BrowserDownloadUrl, release.TagName, fullPackageName) ||
            !IsCanonicalReleaseDownload(portable.BrowserDownloadUrl, release.TagName, portableName))
        {
            reason = "A release asset URL is outside the canonical HTTPS repository/tag path.";
            return false;
        }

        selection = new ManagedReleaseSelection(
            normalizedVersion, installer, releases, fullPackage, portable,
            installerDigest, releasesDigest, fullPackageDigest, portableDigest);
        return true;
    }

    public static bool IsCanonicalReleaseDownload(string? value, string versionOrTag, string assetName)
    {
        if (!TryParseReleaseVersion(versionOrTag, out Version? version)) return false;
        string normalizedVersion = FormatVersion(version);
        if (assetName != InstallerAssetName && assetName != SquirrelReleasesAssetName &&
            assetName != SquirrelFullPackageName(normalizedVersion) &&
            assetName != PortableAssetName(normalizedVersion) &&
            !IsSquirrelDeltaPackageName(assetName, normalizedVersion)) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)) return false;
        if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.Host != "github.com" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return false;
        string expectedPath = $"/{RepositorySlug}/releases/download/v{FormatVersion(version)}/{assetName}";
        return string.Equals(uri.AbsolutePath, expectedPath, StringComparison.Ordinal);
    }

    public static ManagedInstallLayout ValidateInstallLayout(
        string installDirectory,
        string launcherPath,
        string executablePath)
    {
        string root = RequireAbsoluteDirectory(installDirectory);
        string launcher = Path.GetFullPath(launcherPath);
        string executable = Path.GetFullPath(executablePath);
        string expectedLauncher = Path.Combine(root, LauncherFileName);
        string expectedExecutable = Path.Combine(root, ExecutableFileName);
        if (!string.Equals(launcher, expectedLauncher, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(executable, expectedExecutable, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Updater launcher/executable paths must be direct children of the installation folder.");
        return new ManagedInstallLayout(root, launcher, executable);
    }

    public static string ValidateStagedInstallerPath(string installerPath, string updateDirectory)
    {
        string root = RequireAbsoluteDirectory(updateDirectory);
        string installer = Path.GetFullPath(installerPath);
        if (!string.Equals(Path.GetDirectoryName(installer)?.TrimEnd(Path.DirectorySeparatorChar), root,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The staged installer must be a direct child of WinForge's update directory.");
        string name = Path.GetFileName(installer);
        if (!(string.Equals(name, InstallerAssetName, StringComparison.OrdinalIgnoreCase) ||
              (name.StartsWith("Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException("The staged installer filename is outside the WinForge setup contract.");
        return installer;
    }

    public static string ValidateUpdateLogPath(string logPath, string updateDirectory)
    {
        string root = RequireAbsoluteDirectory(updateDirectory);
        string log = Path.GetFullPath(logPath);
        if (!string.Equals(Path.GetDirectoryName(log)?.TrimEnd(Path.DirectorySeparatorChar), root,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetExtension(log), ".log", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Update logs must be .log files directly inside WinForge's update directory.");
        return log;
    }

    public static void ValidatePortableEntries(IEnumerable<string> entries, string versionOrTag)
    {
        var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string entry in entries)
        {
            string value = (entry ?? string.Empty).Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (Path.IsPathRooted(entry) || value.Split('/').Any(part => part is ".." or "."))
                throw new InvalidDataException("Portable archive entries must be relative and traversal-free.");
            normalized.Add(value);
        }

        foreach (string required in new[]
                 {
                     ExecutableFileName,
                     LauncherFileName,
                     ReleaseManifestName,
                     $"{UpdaterDirectoryName}/{UpdaterFileName}"
                 })
        {
            if (!normalized.Contains(required))
                throw new InvalidDataException($"Portable archive is missing required runtime entry: {required}");
        }

        _ = PortableAssetName(versionOrTag);
    }

    private static string RequireAbsoluteDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException("Path must be an absolute local directory.");
        string root = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string driveRoot = Path.GetPathRoot(root)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(root) || string.Equals(root, driveRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A drive root cannot be used as the WinForge installation/update directory.");
        return root;
    }

    private static bool TryCanonicalComponent(string value, out int component)
    {
        component = 0;
        if (value.Length == 0 || (value.Length > 1 && value[0] == '0')) return false;
        return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out component);
    }

    private static string FormatVersion(Version version) =>
        $"{version.Major}.{version.Minor}.{version.Build}";
}
