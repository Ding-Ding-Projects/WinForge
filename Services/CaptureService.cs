using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 擷取工作室引擎 · The Capture Studio engine. Region screen-record to MP4/GIF (ffmpeg gdigrab with
/// -offset_x/-offset_y/-video_size + two-pass GIF palette), instant rectangular snip to the clipboard,
/// and OCR (Windows.Media.Ocr) over any image file or screen region. Everything runs in-app — no redirect.
/// </summary>
public static class CaptureService
{
    // ---- GDI screen grab (for snip + OCR region) ----
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int dx, int dy, int w, int h, IntPtr src, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint start, uint lines, byte[]? bits, ref BITMAPINFO bmi, uint usage);

    private const uint SRCCOPY = 0x00CC0020, CAPTUREBLT = 0x40000000;
    private const uint BI_RGB = 0, DIB_RGB_COLORS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight;
        public ushort biPlanes, biBitCount; public uint biCompression, biSizeImage;
        public int biXPelsPerMeter, biYPelsPerMeter; public uint biClrUsed, biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO { public BITMAPINFOHEADER bmiHeader; public uint bmiColors; }

    /// <summary>
    /// 影低螢幕一忽 → BGRA8 SoftwareBitmap · Grab a physical-pixel screen rect into a BGRA8 SoftwareBitmap.
    /// </summary>
    private static SoftwareBitmap? GrabRegion(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return null;
        IntPtr screenDc = GetDC(IntPtr.Zero);
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr hbmp = CreateCompatibleBitmap(screenDc, w, h);
        IntPtr oldBmp = SelectObject(memDc, hbmp);
        try
        {
            if (!BitBlt(memDc, 0, 0, w, h, screenDc, x, y, SRCCOPY | CAPTUREBLT)) return null;

            var bmi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = w,
                    biHeight = -h,            // top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB,
                },
            };
            var bytes = new byte[w * h * 4];
            if (GetDIBits(memDc, hbmp, 0, (uint)h, bytes, ref bmi, DIB_RGB_COLORS) == 0) return null;

            var sb = new SoftwareBitmap(BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
            // GDI returns BGRA with alpha=0; force opaque so the image isn't fully transparent.
            for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 0xFF;
            sb.CopyFromBuffer(bytes.AsBuffer());
            return sb;
        }
        finally
        {
            SelectObject(memDc, oldBmp);
            DeleteObject(hbmp);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    private static async Task<byte[]> EncodePng(SoftwareBitmap sb)
    {
        using var ms = new InMemoryRandomAccessStream();
        var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ms);
        enc.SetSoftwareBitmap(sb);
        await enc.FlushAsync();
        var bytes = new byte[ms.Size];
        ms.Seek(0);
        await ms.ReadAsync(bytes.AsBuffer(), (uint)ms.Size, InputStreamOptions.None);
        return bytes;
    }

    // =========================================================================
    //  SNIP — capture a screen region straight to the clipboard (and optional file)
    // =========================================================================

    /// <summary>
    /// 截圖區域入剪貼簿 · Snip a screen rect to the clipboard as a PNG image; also returns the PNG bytes
    /// so the caller can preview/save. Region must be in physical screen pixels.
    /// </summary>
    public static async Task<(TweakResult result, byte[]? png)> SnipToClipboard(int x, int y, int w, int h)
    {
        try
        {
            var sb = GrabRegion(x, y, w, h);
            if (sb is null) return (TweakResult.Fail("Could not capture the region.", "影唔到嗰忽螢幕。"), null);

            var png = await EncodePng(sb);
            sb.Dispose();

            // clipboard image via a temp PNG so any app (incl. classic) can paste it
            var tmp = Path.Combine(Path.GetTempPath(), $"WinTune-snip-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            await File.WriteAllBytesAsync(tmp, png);

            var dp = new DataPackage();
            dp.SetBitmap(RandomAccessStreamReference.CreateFromUri(new Uri(tmp)));
            Clipboard.SetContent(dp);
            Clipboard.Flush(); // keep the image after the temp file is gone

            return (TweakResult.Ok("Copied the snip to the clipboard.", "已將截圖複製到剪貼簿。"), png);
        }
        catch (Exception ex)
        {
            return (TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"), null);
        }
    }

    /// <summary>儲存 PNG 到檔案 · Save a PNG byte buffer to a file path.</summary>
    public static async Task<TweakResult> SavePng(byte[] png, string path)
    {
        try { await File.WriteAllBytesAsync(path, png); return TweakResult.Ok("Saved.", "已儲存。"); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // =========================================================================
    //  OCR — recognise text from a screen region or an image file
    // =========================================================================

    /// <summary>有冇中文（繁／簡）辨識器 · Whether a zh-Hant / zh-Hans OCR recognizer is installed.</summary>
    public static bool HasChineseRecognizer =>
        OcrEngine.AvailableRecognizerLanguages.Any(l =>
            l.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase));

    /// <summary>已安裝嘅 OCR 語言（顯示用）· Installed OCR languages, for display.</summary>
    public static string AvailableLanguagesSummary =>
        string.Join(", ", OcrEngine.AvailableRecognizerLanguages.Select(l => l.DisplayName));

    private static OcrEngine? MakeEngine()
    {
        // prefer Traditional Chinese, then Simplified, then the user profile default
        var langs = OcrEngine.AvailableRecognizerLanguages.ToList();
        var zh = langs.FirstOrDefault(l => l.LanguageTag.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase))
              ?? langs.FirstOrDefault(l => l.LanguageTag.StartsWith("zh", StringComparison.OrdinalIgnoreCase));
        if (zh is not null)
        {
            var e = OcrEngine.TryCreateFromLanguage(new Language(zh.LanguageTag));
            if (e is not null) return e;
        }
        return OcrEngine.TryCreateFromUserProfileLanguages();
    }

    private static async Task<(TweakResult result, string? text)> RunOcr(SoftwareBitmap sb)
    {
        var engine = MakeEngine();
        if (engine is null)
        {
            sb.Dispose();
            return (TweakResult.Fail(
                "No OCR language pack is installed. Add one in Settings › Time & language › Language.",
                "未安裝 OCR 語言套件。請喺「設定 › 時間與語言 › 語言」加入。"), null);
        }

        try
        {
            var ocr = await engine.RecognizeAsync(sb);
            var text = string.Join(Environment.NewLine, ocr.Lines.Select(l => l.Text));
            if (string.IsNullOrWhiteSpace(text))
                return (TweakResult.Ok("No text was found.", "搵唔到任何文字。"), "");

            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            return (TweakResult.Ok("Recognised text copied to the clipboard.", "已將辨識文字複製到剪貼簿。"), text);
        }
        catch (Exception ex)
        {
            return (TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"), null);
        }
        finally { sb.Dispose(); }
    }

    /// <summary>由螢幕區域認字 · OCR a screen region (physical pixels); copies result to the clipboard.</summary>
    public static async Task<(TweakResult result, string? text)> OcrRegion(int x, int y, int w, int h)
    {
        var sb = GrabRegion(x, y, w, h);
        if (sb is null) return (TweakResult.Fail("Could not capture the region.", "影唔到嗰忽螢幕。"), null);
        return await RunOcr(sb);
    }

    /// <summary>由圖檔認字 · OCR an image file; copies result to the clipboard.</summary>
    public static async Task<(TweakResult result, string? text)> OcrFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return (TweakResult.Fail("File not found.", "搵唔到檔案。"), null);
            using var fs = File.OpenRead(path);
            var decoder = await BitmapDecoder.CreateAsync(fs.AsRandomAccessStream());
            var sb = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            return await RunOcr(sb);
        }
        catch (Exception ex)
        {
            return (TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"), null);
        }
    }

    // =========================================================================
    //  REGION RECORDING — ffmpeg gdigrab to MP4, optional two-pass GIF
    // =========================================================================

    private static Process? _proc;
    private static string _activeOutput = "";
    private static bool _makeGif;
    private static int _gifFps;

    public static bool IsRecording => _proc is { HasExited: false };

    /// <summary>
    /// 開始錄一忽螢幕 · Start recording a screen region to MP4 via ffmpeg gdigrab. The region is in
    /// physical pixels. If makeGif is set, Stop() will additionally produce a high-quality GIF.
    /// </summary>
    public static TweakResult StartRegionRecording(int x, int y, int w, int h, int fps, string mp4Path, bool makeGif, int gifFps)
    {
        if (IsRecording) return TweakResult.Fail("Already recording.", "已經喺度錄緊。");
        if (!MediaService.IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");
        if (w <= 0 || h <= 0) return TweakResult.Fail("No region selected.", "未揀區域。");

        _activeOutput = mp4Path;
        _makeGif = makeGif;
        _gifFps = Math.Clamp(gifFps, 5, 50);

        var args = $"-y -f gdigrab -framerate {Math.Clamp(fps, 5, 60)} " +
                   $"-offset_x {x} -offset_y {y} -video_size {w}x{h} -i desktop " +
                   $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{mp4Path}\"";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = MediaService.FFmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            _proc = Process.Start(psi);
            if (_proc is null) return TweakResult.Fail("Failed to start ffmpeg.", "無法啟動 ffmpeg。");
            return TweakResult.Ok("Recording…", "錄緊…");
        }
        catch (Exception ex)
        {
            _proc = null;
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>停止錄影（順手出 GIF）· Stop recording cleanly and, if requested, build the GIF.</summary>
    public static async Task<TweakResult> StopRegionRecording()
    {
        var p = _proc;
        _proc = null;
        if (p is null || p.HasExited) return TweakResult.Fail("Not recording.", "冇喺度錄。");
        try
        {
            await p.StandardInput.WriteLineAsync("q"); // graceful finish
            await p.StandardInput.FlushAsync();
            p.StandardInput.Close();
            await p.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }

        if (!_makeGif) return TweakResult.Ok("Saved the recording.", "已儲存錄影。");

        var gifPath = Path.ChangeExtension(_activeOutput, ".gif");
        var gif = await MakeGif(_activeOutput, gifPath, _gifFps);
        return gif.Success
            ? TweakResult.Ok($"Saved MP4 + GIF.", "已儲存 MP4 同 GIF。", gifPath)
            : TweakResult.Fail("MP4 saved, but GIF conversion failed.", "MP4 已存，但 GIF 轉換失敗。",
                (gif.Message?.En ?? ""));
    }

    /// <summary>
    /// 兩步調色板整 GIF · Two-pass palettegen/paletteuse to make a clean, high-quality GIF from a video.
    /// </summary>
    public static async Task<TweakResult> MakeGif(string inputVideo, string gifPath, int fps)
    {
        if (!MediaService.IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");
        if (!File.Exists(inputVideo)) return TweakResult.Fail("Source video not found.", "搵唔到來源影片。");

        fps = Math.Clamp(fps, 5, 50);
        var pal = Path.Combine(Path.GetTempPath(), $"WinTune-pal-{Guid.NewGuid():N}.png");
        try
        {
            // pass 1: generate an optimised palette
            var p1 = $"-y -i \"{inputVideo}\" -vf \"fps={fps},scale=720:-1:flags=lanczos,palettegen=stats_mode=diff\" \"{pal}\"";
            var r1 = await ShellRunner.Run(MediaService.FFmpeg, p1);
            if (!r1.Success || !File.Exists(pal)) return TweakResult.Fail("Palette generation failed.", "調色板生成失敗。", r1.Output);

            // pass 2: apply the palette
            var p2 = $"-y -i \"{inputVideo}\" -i \"{pal}\" -lavfi \"fps={fps},scale=720:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3\" \"{gifPath}\"";
            var r2 = await ShellRunner.Run(MediaService.FFmpeg, p2);
            return r2.Success
                ? TweakResult.Ok("Made the GIF.", "已整好 GIF。", gifPath)
                : TweakResult.Fail("GIF conversion failed.", "GIF 轉換失敗。", r2.Output);
        }
        finally
        {
            try { if (File.Exists(pal)) File.Delete(pal); } catch { }
        }
    }
}
