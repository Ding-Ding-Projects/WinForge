using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace WinForge.Services;

public sealed record ArchiveCreateOptions(
    string Format,
    int Level,
    string? Password,
    bool EncryptHeader,
    bool Solid,
    bool Multithread,
    bool Sfx,
    string? VolumeSize,
    IReadOnlyList<string> IncludeMasks,
    IReadOnlyList<string> ExcludeMasks,
    bool PreserveNtfsTimes,
    bool MoveSourceAfterIntegrityTest);

public sealed record ArchiveCreatePlan(
    IReadOnlyList<string> CreateArguments,
    IReadOnlyList<string> IntegrityArguments,
    string ArchivePath,
    string SourcePath,
    bool MoveSourceAfterIntegrityTest);

/// <summary>Pure, argv-based planning for the bespoke Archives workflows.</summary>
public static class ArchiveWorkflowCore
{
    public const int MaximumMasks = 32;
    public const int MaximumMaskLength = 160;
    public const int MaximumPasswordLength = 256;

    private static readonly HashSet<string> Formats = new(StringComparer.OrdinalIgnoreCase)
        { "7z", "zip", "tar", "gzip", "bzip2", "xz", "wim" };
    private static readonly Regex VolumePattern = new(@"^[1-9]\d{0,6}[bkmg]$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static IReadOnlyList<string> ParseMasks(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        var masks = text.Replace("\r", string.Empty)
            .Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (masks.Length > MaximumMasks) throw new ArgumentException($"At most {MaximumMasks} masks are allowed.", nameof(text));
        return masks.Select(ValidateMask).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string ValidateMask(string? value)
    {
        var mask = (value ?? string.Empty).Trim();
        if (mask.Length is 0 or > MaximumMaskLength) throw new ArgumentException("Archive mask is empty or too long.", nameof(value));
        if (mask.StartsWith('-') || mask.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0 || Path.IsPathRooted(mask))
            throw new ArgumentException("Archive masks must be relative and cannot begin with '-'.", nameof(value));
        var segments = mask.Replace('/', '\\').Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == "..")) throw new ArgumentException("Archive masks cannot traverse parent folders.", nameof(value));
        return mask;
    }

    public static ArchiveCreatePlan BuildCreatePlan(string archivePath, string sourcePath, ArchiveCreateOptions options)
    {
        var archive = NormalizePath(archivePath, nameof(archivePath));
        var source = NormalizePath(sourcePath, nameof(sourcePath));
        if (!Formats.Contains(options.Format)) throw new ArgumentException("Unsupported writable archive format.", nameof(options));
        if (options.Level is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(options.Level));
        if (options.Password is { Length: > MaximumPasswordLength }) throw new ArgumentException("Password is too long.", nameof(options));
        if (options.Password?.IndexOf('\0') >= 0) throw new ArgumentException("Password contains a null character.", nameof(options));
        if (!string.IsNullOrWhiteSpace(options.VolumeSize) && !VolumePattern.IsMatch(options.VolumeSize.Trim()))
            throw new ArgumentException("Volume size must look like 100m, 700m, or 4g.", nameof(options));

        var is7z = options.Format.Equals("7z", StringComparison.OrdinalIgnoreCase);
        if (((options.EncryptHeader && !string.IsNullOrEmpty(options.Password)) || options.Solid || options.Sfx || options.PreserveNtfsTimes) && !is7z)
            throw new ArgumentException("Header encryption, solid mode, SFX, and NTFS timestamp storage require the 7z format.", nameof(options));

        var include = options.IncludeMasks.Select(ValidateMask).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var exclude = options.ExcludeMasks.Select(ValidateMask).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (include.Length > MaximumMasks || exclude.Length > MaximumMasks)
            throw new ArgumentException($"At most {MaximumMasks} include and exclude masks are allowed.", nameof(options));

        if (options.MoveSourceAfterIntegrityTest)
            ValidateMoveSafety(archive, source);

        var arguments = new List<string> { "a", $"-t{options.Format}", $"-mx={options.Level}" };
        if (!string.IsNullOrEmpty(options.Password))
        {
            arguments.Add($"-p{options.Password}");
            if (options.EncryptHeader) arguments.Add("-mhe=on");
        }
        if (options.Solid) arguments.Add("-ms=on");
        if (options.Multithread) arguments.Add("-mmt=on");
        if (options.Sfx) arguments.Add("-sfx");
        if (!string.IsNullOrWhiteSpace(options.VolumeSize)) arguments.Add($"-v{options.VolumeSize.Trim()}");
        if (options.PreserveNtfsTimes)
            arguments.AddRange(new[] { "-mtc=on", "-mta=on", "-mtm=on", "-ssp" });
        arguments.AddRange(include.Select(mask => $"-ir!{mask}"));
        arguments.AddRange(exclude.Select(mask => $"-xr!{mask}"));
        arguments.Add(archive);
        arguments.Add(source);

        var integrityTarget = string.IsNullOrWhiteSpace(options.VolumeSize) ? archive : archive + ".001";
        var integrityArguments = new List<string> { "t", integrityTarget };
        if (!string.IsNullOrEmpty(options.Password)) integrityArguments.Add($"-p{options.Password}");
        return new ArchiveCreatePlan(arguments, integrityArguments, archive, source, options.MoveSourceAfterIntegrityTest);
    }

    public static IReadOnlyList<string> BuildDeleteArguments(string archivePath, IReadOnlyList<string> masks, bool recursive)
    {
        var archive = NormalizePath(archivePath, nameof(archivePath));
        if (masks is null || masks.Count is 0) throw new ArgumentException("Enter at least one archive entry or mask.", nameof(masks));
        if (masks.Count > MaximumMasks) throw new ArgumentException($"At most {MaximumMasks} masks are allowed.", nameof(masks));
        var arguments = new List<string> { "d", archive };
        arguments.AddRange(masks.Select(ValidateMask));
        if (recursive) arguments.Add("-r");
        return arguments;
    }

    public static void ValidateMoveSafety(string archivePath, string sourcePath)
    {
        var archive = NormalizePath(archivePath, nameof(archivePath));
        var source = NormalizePath(sourcePath, nameof(sourcePath));
        if (string.Equals(archive, source, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Archive and source paths must differ.");

        var sourceAsDirectory = EnsureTrailingSeparator(source);
        if (archive.StartsWith(sourceAsDirectory, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The output archive cannot be inside a source folder that will be moved to the Recycle Bin.");
        var root = Path.GetPathRoot(source);
        if (string.Equals(source.TrimEnd('\\', '/'), (root ?? string.Empty).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A drive root cannot be moved after packing.");
    }

    private static string NormalizePath(string? path, string parameter)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", parameter);
        return Path.GetFullPath(path.Trim());
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;
}
