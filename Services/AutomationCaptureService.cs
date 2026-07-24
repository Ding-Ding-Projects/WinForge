using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace WinForge.Services;

/// <summary>
/// Debug-only, explicitly requested in-process screenshot capture. This renders the live WinUI
/// visual tree, which remains available on agent desktops where screen-DC and PrintWindow capture
/// return black frames. Production builds and launches without WINFORGE_CAPTURE_PATH do nothing.
/// </summary>
internal static class AutomationCaptureService
{
    internal static async Task TryCaptureShellAsync(Window window, FrameworkElement root)
    {
#if DEBUG
        var path = Environment.GetEnvironmentVariable("WINFORGE_CAPTURE_PATH")?.Trim();
        if (!IsSupportedPath(path)) return;

        try
        {
            if (TryReadSize(out var width, out var height))
                window.AppWindow.Resize(new Windows.Graphics.SizeInt32(width, height));

            var delayMs = ReadBoundedInt("WINFORGE_CAPTURE_DELAY_MS", 3_000, 1_000, 30_000);
            await Task.Delay(delayMs);

            root.UpdateLayout();
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(root);
            if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
                throw new InvalidOperationException("The WinUI visual tree rendered an empty automation capture.");

            var pixels = await bitmap.GetPixelsAsync();
            var folderPath = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(folderPath);
            var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.ReplaceExisting);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                // RenderTargetBitmap has already composited the live tree. Encoding it as
                // premultiplied alpha makes translucent M3 fills appear falsely opaque.
                BitmapAlphaMode.Ignore,
                (uint)bitmap.PixelWidth,
                (uint)bitmap.PixelHeight,
                96,
                96,
                pixels.ToArray());
            await encoder.FlushAsync();
            CrashLogger.Mark($"automation-capture: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
        }
        catch (Exception ex)
        {
            // Evidence tooling must never destabilize the product it is inspecting.
            CrashLogger.Log("automation-capture", ex);
        }
#else
        await Task.CompletedTask;
#endif
    }

#if DEBUG
    private static bool IsSupportedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return false;
        if (!string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase)) return false;
        return !string.IsNullOrWhiteSpace(Path.GetDirectoryName(path));
    }

    private static bool TryReadSize(out int width, out int height)
    {
        width = ReadBoundedInt("WINFORGE_CAPTURE_WIDTH", 0, 640, 3_840);
        height = ReadBoundedInt("WINFORGE_CAPTURE_HEIGHT", 0, 480, 2_160);
        return width > 0 && height > 0;
    }

    private static int ReadBoundedInt(string name, int fallback, int minimum, int maximum)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        return int.TryParse(raw, out var parsed) && parsed >= minimum && parsed <= maximum
            ? parsed
            : fallback;
    }
#endif
}
