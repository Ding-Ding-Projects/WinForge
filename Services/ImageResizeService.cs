using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WinForge.Services;

/// <summary>一個尺寸預設 · One resize preset.</summary>
public sealed class ResizePreset
{
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";
    public int Width { get; init; }
    public int Height { get; init; }
    /// <summary>true = 只縮唔放（唔會放大細圖）· true = shrink only, never enlarge.</summary>
    public bool ShrinkOnly { get; init; } = true;

    public string Label(bool zhFirst) => zhFirst ? $"{Zh}（{Width}×{Height}）" : $"{En} ({Width}×{Height})";
}

/// <summary>一個批次縮放結果 · Result of one file in the batch.</summary>
public sealed class ResizeResult
{
    public string Source { get; init; } = "";
    public string Output { get; init; } = "";
    public bool Ok { get; init; }
    public string Message { get; init; } = "";
}

/// <summary>
/// 圖片批次縮放（PowerToys Image Resizer 式）· Bulk image resizer. Decodes each picture with
/// Windows.Graphics.Imaging, scales it (fit-within, preserving aspect ratio), and re-encodes to a chosen
/// output folder. Pure WinRT imaging — no external tool, no redirect.
/// </summary>
public static class ImageResizeService
{
    public static readonly IReadOnlyList<ResizePreset> Presets = new List<ResizePreset>
    {
        new() { En = "Small",  Zh = "細",  Width = 854,  Height = 480  },
        new() { En = "Medium", Zh = "中",  Width = 1366, Height = 768  },
        new() { En = "Large",  Zh = "大",  Width = 1920, Height = 1080 },
        new() { En = "Phone",  Zh = "手機", Width = 1080, Height = 1920 },
        new() { En = "Thumbnail", Zh = "縮圖", Width = 256, Height = 256 },
    };

    public static readonly string[] SupportedExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp" };

    public static bool IsSupported(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return Array.IndexOf(SupportedExtensions, ext) >= 0;
    }

    private static Guid EncoderFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => BitmapEncoder.JpegEncoderId,
        ".bmp" => BitmapEncoder.BmpEncoderId,
        ".gif" => BitmapEncoder.GifEncoderId,
        ".tif" or ".tiff" => BitmapEncoder.TiffEncoderId,
        _ => BitmapEncoder.PngEncoderId,
    };

    /// <summary>
    /// 縮放一張圖到 maxW×maxH（保持比例）· Resize one image to fit within maxW×maxH (aspect kept).
    /// Returns the output path.
    /// </summary>
    public static async Task<string> ResizeOneAsync(
        string sourcePath, string outputFolder, int maxW, int maxH, bool shrinkOnly,
        int jpegQuality, string suffix)
    {
        if (maxW <= 0 || maxH <= 0) throw new ArgumentException("Target size must be positive.");

        var srcFile = await StorageFile.GetFileFromPathAsync(sourcePath);
        using var inStream = await srcFile.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(inStream);

        uint ow = decoder.PixelWidth, oh = decoder.PixelHeight;
        double scale = Math.Min(maxW / (double)ow, maxH / (double)oh);
        if (shrinkOnly && scale >= 1.0) scale = 1.0;
        uint nw = (uint)Math.Max(1, Math.Round(ow * scale));
        uint nh = (uint)Math.Max(1, Math.Round(oh * scale));

        var ext = Path.GetExtension(sourcePath);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        var outName = $"{baseName}{suffix}{ext}";
        Directory.CreateDirectory(outputFolder);
        var outPath = Path.Combine(outputFolder, outName);

        var pixels = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            new BitmapTransform { ScaledWidth = nw, ScaledHeight = nh, InterpolationMode = BitmapInterpolationMode.Fant },
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);

        var outFolder = await StorageFolder.GetFolderFromPathAsync(outputFolder);
        var outFile = await outFolder.CreateFileAsync(outName, CreationCollisionOption.ReplaceExisting);
        using (var outStream = await outFile.OpenAsync(FileAccessMode.ReadWrite))
        {
            var encoderId = EncoderFor(ext);
            var propSet = new BitmapPropertySet();
            if (encoderId == BitmapEncoder.JpegEncoderId)
            {
                double q = Math.Clamp(jpegQuality, 1, 100) / 100.0;
                propSet.Add("ImageQuality", new BitmapTypedValue(q, Windows.Foundation.PropertyType.Single));
            }
            var encoder = await BitmapEncoder.CreateAsync(encoderId, outStream, propSet);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                nw, nh, decoder.DpiX, decoder.DpiY, pixels.DetachPixelData());
            await encoder.FlushAsync();
        }
        return outPath;
    }

    /// <summary>批次縮放 · Resize a batch, reporting progress per file.</summary>
    public static async Task<List<ResizeResult>> ResizeBatchAsync(
        IEnumerable<string> sources, string outputFolder, int maxW, int maxH, bool shrinkOnly,
        int jpegQuality, string suffix, Action<int, int, string>? progress = null)
    {
        var results = new List<ResizeResult>();
        var list = new List<string>(sources);
        for (int i = 0; i < list.Count; i++)
        {
            var src = list[i];
            progress?.Invoke(i + 1, list.Count, src);
            try
            {
                var outPath = await ResizeOneAsync(src, outputFolder, maxW, maxH, shrinkOnly, jpegQuality, suffix);
                results.Add(new ResizeResult { Source = src, Output = outPath, Ok = true });
            }
            catch (Exception ex)
            {
                results.Add(new ResizeResult { Source = src, Ok = false, Message = ex.Message });
            }
        }
        return results;
    }
}
