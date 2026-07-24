using System;
using System.IO;

namespace WinForge.Services;

/// <summary>
/// Pure validation and pixel helpers for the debug-only WinUI automation capture path.
/// Kept independent of WinUI so its safety boundaries can be regression-tested directly.
/// </summary>
internal static class AutomationCapturePolicy
{
    internal static bool TryGetSupportedPath(string? candidate, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 1_024) return false;

        try
        {
            if (!Path.IsPathFullyQualified(candidate)) return false;
            var fullPath = Path.GetFullPath(candidate);
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal)) return false;
            if (!string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(Path.GetFileName(fullPath)) ||
                string.IsNullOrWhiteSpace(Path.GetDirectoryName(fullPath))) return false;

            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root)) return false;
            var driveType = new DriveInfo(root).DriveType;
            if (driveType is not (DriveType.Fixed or DriveType.Removable)) return false;

            path = fullPath;
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static int ReadBoundedInt(string? raw, int fallback, int minimum, int maximum)
        => int.TryParse(raw, out var parsed) && parsed >= minimum && parsed <= maximum
            ? parsed
            : fallback;

    internal static void FlattenPremultipliedPixels(
        byte[] pixels,
        byte backgroundBlue,
        byte backgroundGreen,
        byte backgroundRed)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length % 4 != 0)
            throw new InvalidOperationException("The WinUI capture returned a malformed BGRA8 buffer.");

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var alpha = pixels[i + 3];
            if (alpha == byte.MaxValue) continue;

            var inverseAlpha = byte.MaxValue - alpha;
            pixels[i] = CompositeChannel(pixels[i], backgroundBlue, inverseAlpha);
            pixels[i + 1] = CompositeChannel(pixels[i + 1], backgroundGreen, inverseAlpha);
            pixels[i + 2] = CompositeChannel(pixels[i + 2], backgroundRed, inverseAlpha);
            pixels[i + 3] = byte.MaxValue;
        }
    }

    private static byte CompositeChannel(byte premultiplied, byte background, int inverseAlpha)
    {
        var value = premultiplied + ((background * inverseAlpha + 127) / 255);
        return (byte)Math.Min(byte.MaxValue, value);
    }
}
