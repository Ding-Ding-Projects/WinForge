using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace WinForge.Converters;

/// <summary>
/// 將圖示路徑／URI 字串轉成圖片來源 · Converts a logo path/URI string into an ImageSource for binding.
/// 失敗時回傳 null，等後面嘅後備圖示顯示 · returns null on failure so a fallback glyph shows through.
/// </summary>
public sealed class UriToImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            try { return new BitmapImage(new Uri(s)); }
            catch { return null; }
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
