using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace WinForge.Services;

/// <summary>一個螢幕區域 · A screen rectangle (virtual-screen coordinates).</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height);

/// <summary>
/// 螢幕文字擷取／OCR（PowerToys Text Extractor 式）· Capture a screen region with GDI and run
/// Windows.Media.Ocr on it to pull out text. No external tool, no redirect.
/// </summary>
public static class TextExtractorService
{
    // ---- screen capture (GDI) ----
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dest, int dx, int dy, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] private static extern bool StretchBlt(IntPtr dest, int dx, int dy, int dw, int dh, IntPtr src, int sx, int sy, int sw, int sh, uint rop);
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr hdc, int mode);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines, byte[]? bits, ref BITMAPINFO bmi, uint usage);

    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth, biHeight;
        public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)] public uint[] bmiColors;
    }

    /// <summary>整個虛擬桌面範圍 · The whole virtual desktop bounds.</summary>
    public static ScreenRect VirtualScreen() => new(
        GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN),
        GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN));

    /// <summary>
    /// 擷取一個螢幕區域成 SoftwareBitmap · Capture a screen rect to a SoftwareBitmap (BGRA8).
    /// maxDim &gt; 0 會將過大嘅擷取按比例縮細（OCR 引擎有最大尺寸限制，過大會令 RecognizeAsync 崩潰）。
    /// When maxDim &gt; 0, oversized captures are scaled down so they stay within the OCR engine's limit.
    /// </summary>
    public static SoftwareBitmap CaptureRegion(ScreenRect r, int maxDim = 0)
    {
        if (r.Width <= 0 || r.Height <= 0) throw new ArgumentException("Capture region is empty.");

        int tw = r.Width, th = r.Height;
        if (maxDim > 0 && (r.Width > maxDim || r.Height > maxDim))
        {
            double scale = Math.Min((double)maxDim / r.Width, (double)maxDim / r.Height);
            tw = Math.Max(1, (int)Math.Round(r.Width * scale));
            th = Math.Max(1, (int)Math.Round(r.Height * scale));
        }

        IntPtr screenDC = GetDC(IntPtr.Zero);
        IntPtr memDC = CreateCompatibleDC(screenDC);
        IntPtr hbmp = CreateCompatibleBitmap(screenDC, tw, th);
        IntPtr old = SelectObject(memDC, hbmp);
        try
        {
            if (tw == r.Width && th == r.Height)
            {
                BitBlt(memDC, 0, 0, tw, th, screenDC, r.X, r.Y, SRCCOPY);
            }
            else
            {
                SetStretchBltMode(memDC, 4 /* HALFTONE */);
                StretchBlt(memDC, 0, 0, tw, th, screenDC, r.X, r.Y, r.Width, r.Height, SRCCOPY);
            }

            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = tw,
                    biHeight = -th, // top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0, // BI_RGB
                },
                bmiColors = new uint[256],
            };
            var bytes = new byte[tw * th * 4];
            GetDIBits(memDC, hbmp, 0, (uint)th, bytes, ref bmi, DIB_RGB_COLORS);

            // GDI gives BGRA already (32-bit BI_RGB on Windows). Build a SoftwareBitmap.
            var sb = new SoftwareBitmap(BitmapPixelFormat.Bgra8, tw, th, BitmapAlphaMode.Premultiplied);
            sb.CopyFromBuffer(bytes.AsBuffer());
            return sb;
        }
        finally
        {
            SelectObject(memDC, old);
            DeleteObject(hbmp);
            DeleteDC(memDC);
            ReleaseDC(IntPtr.Zero, screenDC);
        }
    }

    /// <summary>可用 OCR 語言 · Available OCR languages on this machine.</summary>
    public static IReadOnlyList<Language> AvailableLanguages()
    {
        try { return OcrEngine.AvailableRecognizerLanguages; }
        catch { return Array.Empty<Language>(); }
    }

    /// <summary>對一個區域做 OCR · Run OCR over a region, returning recognised text.</summary>
    public static async Task<string> ExtractTextAsync(ScreenRect r, Language? lang = null)
    {
        OcrEngine? engine = null;
        if (lang is not null) engine = OcrEngine.TryCreateFromLanguage(lang);
        engine ??= OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
            throw new InvalidOperationException(
                "No OCR language pack is installed. Add one in Windows Settings → Time & language → Language & region → (a language) → Optional features → handwriting/OCR.");

        // The OCR engine rejects images larger than MaxImageDimension — downscale the capture to fit.
        using var bmp = CaptureRegion(r, (int)OcrEngine.MaxImageDimension);
        var result = await engine.RecognizeAsync(bmp);
        return result.Text ?? "";
    }
}

internal static class SoftwareBitmapBufferExtensions
{
    public static Windows.Storage.Streams.IBuffer AsBuffer(this byte[] bytes)
        => System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions.AsBuffer(bytes);
}
