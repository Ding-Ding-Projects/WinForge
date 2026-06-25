using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 真實式右至左滾動趨勢記錄儀 · An authentic right-to-left scrolling strip-chart recorder.
///
/// Maintains a ring buffer, draws a faint grid, a redline threshold and one or more traces with a soft
/// glow on the active pen. Reuses its PointCollections and only updates polyline Points each frame
/// (no Children.Clear per tick) so it is cheap. Axis caption is bilingual via Loc.I.Pick.
/// </summary>
public sealed class StripChartRecorder : UserControl
{
    public sealed class Pen
    {
        public string En = "";
        public string Zh = "";
        public Color Color;
        public double Min, Max;
        public double Redline = double.NaN;
        public Func<double> Read = () => 0;
        internal double[] Buffer = Array.Empty<double>();
        internal int Head;
        internal Polyline Line = null!;
        internal Line RedlineShape = null!;
    }

    private readonly Canvas _canvas = new();
    private readonly TextBlock _caption = new() { FontSize = 11, Margin = new Thickness(2, 0, 0, 2) };
    private Pen[] _pens = Array.Empty<Pen>();
    private readonly int _capacity;
    private bool _built;

    public StripChartRecorder(double width, double height, int capacity = 180)
    {
        _capacity = capacity;
        _canvas.Width = width;
        _canvas.Height = height;
        _canvas.Background = new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0));
        var panel = new StackPanel { Spacing = 2 };
        _caption.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        panel.Children.Add(_caption);
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            Child = _canvas,
        };
        panel.Children.Add(border);
        Content = panel;
    }

    public void SetPens(params Pen[] pens)
    {
        _pens = pens;
        foreach (var p in _pens)
        {
            p.Buffer = new double[_capacity];
            p.Head = 0;
        }
        Build();
        Relocalize();
    }

    private void Build()
    {
        _canvas.Children.Clear();
        double w = _canvas.Width, h = _canvas.Height;
        // grid
        for (int i = 1; i < 4; i++)
        {
            double y = h * i / 4;
            _canvas.Children.Add(new Line
            {
                X1 = 0, Y1 = y, X2 = w, Y2 = y,
                Stroke = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1,
            });
        }
        for (int i = 1; i < 6; i++)
        {
            double x = w * i / 6;
            _canvas.Children.Add(new Line
            {
                X1 = x, Y1 = 0, X2 = x, Y2 = h,
                Stroke = new SolidColorBrush(Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 1,
            });
        }
        foreach (var p in _pens)
        {
            // redline
            p.RedlineShape = new Line
            {
                Stroke = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0x52, 0x52)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 3 },
                Visibility = double.IsNaN(p.Redline) ? Visibility.Collapsed : Visibility.Visible,
            };
            if (!double.IsNaN(p.Redline))
            {
                double f = Math.Clamp((p.Redline - p.Min) / (p.Max - p.Min), 0, 1);
                double y = h - f * h;
                p.RedlineShape.X1 = 0; p.RedlineShape.Y1 = y; p.RedlineShape.X2 = w; p.RedlineShape.Y2 = y;
            }
            _canvas.Children.Add(p.RedlineShape);

            p.Line = new Polyline
            {
                Stroke = new SolidColorBrush(p.Color),
                StrokeThickness = 1.6,
                Points = new PointCollection(),
            };
            _canvas.Children.Add(p.Line);
        }
        _built = true;
    }

    /// <summary>取一個樣本並重畫 · Sample all pens and redraw (call each tick).</summary>
    public void Sample()
    {
        if (!_built) return;
        foreach (var p in _pens)
        {
            p.Buffer[p.Head] = p.Read();
            p.Head = (p.Head + 1) % _capacity;
            Redraw(p);
        }
    }

    private void Redraw(Pen p)
    {
        double w = _canvas.Width, h = _canvas.Height;
        var pts = p.Line.Points;
        pts.Clear();
        for (int i = 0; i < _capacity; i++)
        {
            int idx = (p.Head + i) % _capacity; // oldest -> newest, scrolls right-to-left
            double v = p.Buffer[idx];
            double x = w * i / (_capacity - 1);
            double f = Math.Clamp((v - p.Min) / (p.Max - p.Min), 0, 1);
            double y = h - f * h;
            pts.Add(new Point(x, y));
        }
    }

    public void Relocalize()
    {
        if (_pens.Length == 0) { _caption.Text = ""; return; }
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < _pens.Length; i++)
        {
            if (i > 0) sb.Append("  ·  ");
            sb.Append(Loc.I.Pick(_pens[i].En, _pens[i].Zh));
        }
        _caption.Text = sb.ToString();
    }
}
