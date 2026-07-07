using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace WinForge.Services;

/// <summary>一行 OCR 結果（文字 + 行／字數）· One OCR result with text plus line/word counts.</summary>
public readonly record struct OcrResultInfo(string Text, int LineCount, int WordCount);

/// <summary>
/// 原生 Windows OCR 包裝（NormCap 式）· A richer wrapper over the built-in <see cref="OcrEngine"/>
/// (Windows.Media.Ocr, WinRT). Pure managed — NO external tool, NO Tesseract, NO shelling out.
/// 重用 <see cref="TextExtractorService"/> 嘅螢幕擷取／OCR；再加埋圖片檔 OCR 同語言列舉。
/// Re-uses <see cref="TextExtractorService"/> for the screen-grab path and adds image-file OCR
/// plus structured (line/word) layout and language enumeration.
/// </summary>
public static class OcrService
{
    /// <summary>本機安裝咗嘅 OCR 語言 · OCR languages installed on this machine.</summary>
    public static IReadOnlyList<Language> AvailableLanguages() => TextExtractorService.AvailableLanguages();

    /// <summary>有冇至少一個可用 OCR 引擎 · True if at least one OCR engine can be created.</summary>
    public static bool IsAvailable()
    {
        try
        {
            return OcrEngine.AvailableRecognizerLanguages.Count > 0
                || OcrEngine.TryCreateFromUserProfileLanguages() is not null;
        }
        catch { return false; }
    }

    /// <summary>揀一個 OCR 引擎（指定語言，否則用設定檔語言）· Resolve an engine for the given language.</summary>
    private static OcrEngine ResolveEngine(Language? lang)
    {
        OcrEngine? engine = null;
        if (lang is not null) engine = OcrEngine.TryCreateFromLanguage(lang);
        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
            throw new InvalidOperationException(
                "No OCR language pack is installed. Add one in Windows Settings → Time & language → "
              + "Language & region → (a language) → Language options → install the optional OCR feature.");
        return engine;
    }

    /// <summary>對一個 SoftwareBitmap 做 OCR，連行／字統計 · OCR a bitmap, returning text + line/word counts.</summary>
    public static async Task<OcrResultInfo> RecognizeAsync(SoftwareBitmap bmp, Language? lang = null)
    {
        var engine = ResolveEngine(lang);

        // 引擎拒絕超過 MaxImageDimension 嘅圖；過大就按比例縮細。
        // The engine rejects images larger than MaxImageDimension — downscale to fit.
        SoftwareBitmap toScan = bmp;
        SoftwareBitmap? scaled = null;
        try
        {
            uint max = OcrEngine.MaxImageDimension;
            if (bmp.PixelWidth > max || bmp.PixelHeight > max)
            {
                double s = Math.Min((double)max / bmp.PixelWidth, (double)max / bmp.PixelHeight);
                scaled = await ScaleAsync(bmp, Math.Max(1, (int)(bmp.PixelWidth * s)), Math.Max(1, (int)(bmp.PixelHeight * s)));
                toScan = scaled;
            }

            var result = await engine.RecognizeAsync(toScan);
            // Build text line-by-line so the on-screen layout matches NormCap's line preservation.
            var lines = result.Lines.Select(l => l.Text).ToList();
            string text = lines.Count > 0 ? string.Join(Environment.NewLine, lines) : (result.Text ?? "");
            int words = result.Lines.Sum(l => l.Words.Count);
            return new OcrResultInfo(text, lines.Count, words);
        }
        finally
        {
            scaled?.Dispose();
        }
    }

    /// <summary>對一個螢幕區域做 OCR · OCR a screen region (re-uses the shared GDI capture).</summary>
    public static async Task<OcrResultInfo> RecognizeRegionAsync(ScreenRect r, Language? lang = null)
    {
        using var bmp = TextExtractorService.CaptureRegion(r, (int)OcrEngine.MaxImageDimension);
        return await RecognizeAsync(bmp, lang);
    }

    /// <summary>對一個圖片檔（PNG/JPG/BMP…）做 OCR · OCR an image file from disk.</summary>
    public static async Task<OcrResultInfo> RecognizeFileAsync(string path, Language? lang = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException("Image not found.", path);

        var bytes = await File.ReadAllBytesAsync(path);
        using var ras = new InMemoryRandomAccessStream();
        await ras.WriteAsync(
            System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(bytes));
        ras.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(ras);
        using var bmp = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        return await RecognizeAsync(bmp, lang);
    }

    /// <summary>把 SoftwareBitmap 縮放到指定尺寸 · Scale a SoftwareBitmap to a target size (BGRA8).</summary>
    private static async Task<SoftwareBitmap> ScaleAsync(SoftwareBitmap src, int w, int h)
    {
        using var ras = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ras);
        encoder.SetSoftwareBitmap(SoftwareBitmap.Convert(src, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied));
        encoder.BitmapTransform.ScaledWidth = (uint)w;
        encoder.BitmapTransform.ScaledHeight = (uint)h;
        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
        await encoder.FlushAsync();
        ras.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(ras);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }
}
