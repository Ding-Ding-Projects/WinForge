using System;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WinForge.Converters;

/// <summary>
/// 運行狀態小圓點顏色 · Maps a VM's "is running" bool to a status-dot brush —
/// 綠色（運行中）或灰色（停止）· green when running, grey otherwise.
/// </summary>
public sealed class BoolToRunStateBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Running = new(Color.FromArgb(0xFF, 0x2E, 0xA0, 0x43));
    private static readonly SolidColorBrush Stopped = new(Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A));

    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Running : Stopped;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
