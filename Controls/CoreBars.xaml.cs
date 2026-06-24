using System;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WinForge.Controls;

/// <summary>
/// 每核心負載條（btop 風格）· A btop-style per-core load strip.
/// 每個邏輯核心一條細長條，顏色按負載由綠轉黃轉紅。Builds one mini bar per logical core, laid out in a
/// responsive grid; each bar's fill width + colour follow the core's load (green→yellow→red).
/// </summary>
public sealed partial class CoreBars : UserControl
{
    private const double TrackWidth = 120;
    private TextBlock[] _labels = Array.Empty<TextBlock>();
    private Border[] _fills = Array.Empty<Border>();
    private TextBlock[] _values = Array.Empty<TextBlock>();
    private int _columns = 4;

    public CoreBars()
    {
        InitializeComponent();
    }

    /// <summary>建立 <paramref name="count"/> 個核心條 · Build bars for the given logical-core count.</summary>
    public void Build(int count, int columns = 4)
    {
        _columns = Math.Max(1, columns);
        Host.Children.Clear();
        Host.ColumnDefinitions.Clear();
        Host.RowDefinitions.Clear();

        _labels = new TextBlock[count];
        _fills = new Border[count];
        _values = new TextBlock[count];

        for (int c = 0; c < _columns; c++)
            Host.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        int rows = (count + _columns - 1) / _columns;
        for (int r = 0; r < rows; r++)
            Host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var trackBg = (Brush)Application.Current.Resources["ControlStrongFillColorDefaultBrush"];
        var secondary = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

        for (int i = 0; i < count; i++)
        {
            var cell = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

            var label = new TextBlock
            {
                Text = $"{i:00}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = secondary,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 20,
            };
            _labels[i] = label;

            var trackHost = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var trackBgBorder = new Border
            {
                Width = TrackWidth,
                Height = 8,
                CornerRadius = new CornerRadius(2),
                Background = trackBg,
                Opacity = 0.25,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            var fill = new Border
            {
                Width = 0,
                Height = 8,
                CornerRadius = new CornerRadius(2),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            _fills[i] = fill;
            trackHost.Children.Add(trackBgBorder);
            trackHost.Children.Add(fill);

            var value = new TextBlock
            {
                Text = "0%",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                MinWidth = 34,
                HorizontalTextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _values[i] = value;

            cell.Children.Add(label);
            cell.Children.Add(trackHost);
            cell.Children.Add(value);

            int rr = i / _columns, cc = i % _columns;
            Grid.SetRow(cell, rr);
            Grid.SetColumn(cell, cc);
            Host.Children.Add(cell);
        }
    }

    /// <summary>更新每核心負載（0–100）· Update each core's load (0–100).</summary>
    public void Update(double[] loads)
    {
        int n = Math.Min(loads.Length, _fills.Length);
        for (int i = 0; i < n; i++)
        {
            double v = Math.Clamp(loads[i], 0, 100);
            _fills[i].Width = TrackWidth * v / 100.0;
            _fills[i].Background = new SolidColorBrush(LoadColor(v));
            _values[i].Text = $"{Math.Round(v)}%";
        }
    }

    /// <summary>btop 漸層：綠 → 黃 → 紅 · btop gradient: green → yellow → red by load.</summary>
    public static Color LoadColor(double pct)
    {
        pct = Math.Clamp(pct, 0, 100);
        // 0–50%: green→yellow, 50–100%: yellow→red.
        Color green = Color.FromArgb(0xFF, 0x36, 0xC9, 0x5C);
        Color yellow = Color.FromArgb(0xFF, 0xE6, 0xC2, 0x29);
        Color red = Color.FromArgb(0xFF, 0xE2, 0x46, 0x46);
        if (pct <= 50)
            return Lerp(green, yellow, pct / 50.0);
        return Lerp(yellow, red, (pct - 50) / 50.0);
    }

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            0xFF,
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }
}
