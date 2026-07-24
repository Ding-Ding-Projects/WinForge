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
        var requestedPath = Environment.GetEnvironmentVariable("WINFORGE_CAPTURE_PATH")?.Trim();
        if (!AutomationCapturePolicy.TryGetSupportedPath(requestedPath, out var path)) return;

        string? partialPath = null;
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
            var encodedPixels = pixels.ToArray();
            var background = GetCaptureBackground(root);
            AutomationCapturePolicy.FlattenPremultipliedPixels(
                encodedPixels,
                background.B,
                background.G,
                background.R);

            var folderPath = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(folderPath)) return;
            Directory.CreateDirectory(folderPath);
            var folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            partialPath = Path.Combine(
                folderPath,
                $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.partial.png");
            var file = await folder.CreateFileAsync(Path.GetFileName(partialPath), CreationCollisionOption.FailIfExists);
            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    (uint)bitmap.PixelWidth,
                    (uint)bitmap.PixelHeight,
                    96,
                    96,
                    encodedPixels);
                await encoder.FlushAsync();
            }

            // Promote only a fully flushed PNG. A crash or encoder failure cannot leave a
            // partially written file at the requested evidence path.
            File.Move(partialPath, path, overwrite: true);
            partialPath = null;
            CrashLogger.Mark($"automation-capture: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
        }
        catch (Exception ex)
        {
            // Evidence tooling must never destabilize the product it is inspecting or log
            // a user-selected path. Preserve only a type/HRESULT diagnostic.
            CrashLogger.Mark($"automation-capture-failed: {ex.GetType().Name} (0x{ex.HResult:X8})");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(partialPath))
            {
                try { File.Delete(partialPath); } catch { }
            }
        }
#else
        await Task.CompletedTask;
#endif
    }

#if DEBUG
    private static bool TryReadSize(out int width, out int height)
    {
        width = ReadBoundedInt("WINFORGE_CAPTURE_WIDTH", 0, 640, 3_840);
        height = ReadBoundedInt("WINFORGE_CAPTURE_HEIGHT", 0, 480, 2_160);
        return width > 0 && height > 0;
    }

    private static int ReadBoundedInt(string name, int fallback, int minimum, int maximum)
    {
        return AutomationCapturePolicy.ReadBoundedInt(
            Environment.GetEnvironmentVariable(name),
            fallback,
            minimum,
            maximum);
    }

    private static Windows.UI.Color GetCaptureBackground(FrameworkElement root)
    {
        // Application-level theme-dictionary lookup can resolve against the OS theme even
        // when WinForge has explicitly applied the opposite theme to this window. The root's
        // ActualTheme is the authoritative visual state being captured.
        return root.ActualTheme == ElementTheme.Light
            ? Windows.UI.Color.FromArgb(255, 243, 243, 243)
            : Windows.UI.Color.FromArgb(255, 10, 13, 11);
    }

#endif
}
