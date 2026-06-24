using System;
using System.IO;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinForge.Converters;

/// <summary>
/// 將圖示路徑／URI 字串轉成圖片來源 · Converts a logo path/URI string into an ImageSource for binding.
/// 失敗時回傳 null，等後面嘅後備圖示顯示 · returns null on failure so a fallback glyph shows through.
/// </summary>
/// <remarks>
/// WinForge 以「未封裝」(unpackaged) 模式執行，所以 <c>ms-appx:///</c> 喺執行階段未必靠得住，
/// 而 PNG 來源好多時係淨係一個本機絕對路徑（例如解除安裝模組嘅 logo、剪貼簿圖片）。
/// WinForge runs unpackaged, so <c>ms-appx:///</c> can 404 at runtime and many PNG sources are
/// bare local file paths (uninstaller logos, clipboard/capture images). This converter normalises
/// every shape into a real URI a <see cref="BitmapImage"/> can decode, and sets
/// <see cref="BitmapImage.UriSource"/> after construction so load errors are not swallowed silently.
/// </remarks>
public sealed class UriToImageConverter : IValueConverter
{
    // 為清單縮圖封頂解碼尺寸，慳記憶體 · cap decode size for list thumbnails to save memory.
    private const int MaxDecodePixelWidth = 256;

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return null;

        try
        {
            var uri = Normalize(s.Trim());
            if (uri is null)
                return null;

            // 先建立空 BitmapImage 再設定 UriSource，避免 new BitmapImage(uri) 靜靜吞咗載入錯誤。
            // Build the BitmapImage first, then assign UriSource — the (uri) constructor can swallow
            // decode/load failures silently, which is exactly the "PNG not showing" symptom we hit.
            var bmp = new BitmapImage { DecodePixelWidth = MaxDecodePixelWidth, UriSource = uri };
            return bmp;
        }
        catch
        {
            // 永遠唔好掟例外（XAML 綁定錯誤好難查）· never throw — XAML binding errors are hard to diagnose.
            return null;
        }
    }

    /// <summary>
    /// 將任意字串正規化成 BitmapImage 用得嘅 URI · normalise any string into a URI BitmapImage can load.
    /// </summary>
    private static Uri? Normalize(string s)
    {
        // (1)/(2) 已經有 scheme：http(s)、ms-appx、ms-appdata、file 等直接通過。
        // Already a well-formed absolute URI (http(s), ms-appx, ms-appdata, file, …) → pass through.
        if (Uri.TryCreate(s, UriKind.Absolute, out var abs)
            && !string.IsNullOrEmpty(abs.Scheme)
            && abs.Scheme.Length > 1) // 排除 Windows 磁碟機代號被誤當 scheme（"C:\…"）· exclude drive-letter false positives.
        {
            return abs;
        }

        // (3) 絕對本機路徑（例如 C:\Users\…\icon.png）→ 包成 file:/// URI。
        // Absolute local path → wrap as file:/// so BitmapImage can decode it.
        if (Path.IsPathRooted(s) && File.Exists(s))
            return new Uri(s);

        // (4) 相對路徑 → 相對於 app 基底目錄解析（隨身資產用）。
        // Relative path → resolve against the app base directory (for shipped/loose assets).
        var full = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, s));
        if (File.Exists(full))
            return new Uri(full);

        // 即使檔案唔存在，若係絕對路徑都照試一次（可能權限問題，交畀 ImageFailed 處理）。
        // As a last resort, if it is a rooted path return a file:/// URI anyway so ImageFailed can log it.
        if (Path.IsPathRooted(s))
            return new Uri(s);

        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
