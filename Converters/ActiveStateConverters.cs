using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace WinForge.Converters;

/// <summary>
/// 載入狀態 → 圖示字元 · A bool (active/loaded) → status glyph (full circle vs circle ring).
/// 畀 Rainmeter 皮膚清單用 · used by the Rainmeter skin list to show loaded vs not-loaded at a glance.
/// </summary>
public sealed class ActiveGlyphConverter : IValueConverter
{
    //  = FullCircleMask (loaded),  = CircleRing (not loaded).
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? "" : "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>
/// 載入狀態 → 顏色 · A bool (active/loaded) → brush (accent when loaded, muted otherwise).
/// </summary>
public sealed class ActiveBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is true && Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var accent) && accent is Brush a)
            return a;
        if (Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var muted) && muted is Brush m)
            return m;
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
