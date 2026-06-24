using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;

namespace WinForge.Controls;

/// <summary>
/// 輕量歷史折線圖（btop 風格）· A lightweight btop-style history sparkline.
/// 用 <see cref="Polyline"/> + 漸層填充畫一段固定長度嘅歷史，每 tick 推入一個原始值；
/// 唔用 Win2D，避免每格重排版。Push a raw value each tick; renders a capped-length history as a filled
/// polyline with a green→yellow→red gradient. No Win2D, cheap redraws.
/// </summary>
public sealed class SparklineGraph : ContentControl
{
    private readonly Canvas _canvas = new();
    private readonly Polyline _line = new();
    private readonly Polygon _fill = new();
    private readonly List<double> _history = new();
    private int _capacity = 60;

    /// <summary>最大值（用嚟把原始值正規化）· The value treated as full-scale (top of the graph).</summary>
    public double Max { get; set; } = 1.0;

    /// <summary>歷史長度（取樣點數）· How many samples to keep / draw.</summary>
    public int Capacity
    {
        get => _capacity;
        set { _capacity = Math.Max(8, value); Trim(); Redraw(); }
    }

    public SparklineGraph()
    {
        IsTabStop = false;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        MinHeight = 36;

        _line.StrokeThickness = 1.4;
        _line.StrokeLineJoin = PenLineJoin.Round;
        _fill.Opacity = 0.22;

        _canvas.Children.Add(_fill);
        _canvas.Children.Add(_line);
        Content = _canvas;

        SizeChanged += (_, _) => Redraw();
    }

    /// <summary>btop-style 三色漸層（綠→黃→紅）· btop-style green→yellow→red gradient.</summary>
    public void ApplyTheme(bool dark)
    {
        var start = Color.FromArgb(0xFF, 0x36, 0xC9, 0x5C); // green
        var mid = Color.FromArgb(0xFF, 0xE6, 0xC2, 0x29);   // yellow
        var end = Color.FromArgb(0xFF, 0xE2, 0x46, 0x46);   // red
        var grad = new LinearGradientBrush { StartPoint = new Point(0, 1), EndPoint = new Point(0, 0) };
        grad.GradientStops.Add(new GradientStop { Color = start, Offset = 0.0 });
        grad.GradientStops.Add(new GradientStop { Color = mid, Offset = 0.6 });
        grad.GradientStops.Add(new GradientStop { Color = end, Offset = 1.0 });
        _line.Stroke = grad;
        _fill.Fill = grad;
        Redraw();
    }

    /// <summary>推入最新讀數（原始單位，會除以 <see cref="Max"/> 正規化）· Push the newest raw reading.</summary>
    public void Push(double raw)
    {
        _history.Add(raw);
        Trim();
        Redraw();
    }

    public void Clear() { _history.Clear(); Redraw(); }

    private void Trim()
    {
        while (_history.Count > _capacity) _history.RemoveAt(0);
    }

    private void Redraw()
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 1 || h <= 1 || _history.Count < 2)
        {
            _line.Points.Clear();
            _fill.Points.Clear();
            return;
        }

        double max = Max <= 0 ? 1 : Max;
        int count = _history.Count;
        double step = w / Math.Max(1, _capacity - 1);
        // Right-align the most recent sample at the right edge.
        double x0 = w - (count - 1) * step;

        var pts = new PointCollection();
        for (int i = 0; i < count; i++)
        {
            double norm = Math.Clamp(_history[i] / max, 0, 1);
            double x = x0 + i * step;
            double y = h - norm * (h - 2) - 1;
            pts.Add(new Point(x, y));
        }
        _line.Points = pts;

        var fillPts = new PointCollection();
        foreach (var p in pts) fillPts.Add(p);
        fillPts.Add(new Point(x0 + (count - 1) * step, h));
        fillPts.Add(new Point(x0, h));
        _fill.Points = fillPts;
    }
}
