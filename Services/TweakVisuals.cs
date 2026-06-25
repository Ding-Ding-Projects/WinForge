using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using WinForge.Models;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;

namespace WinForge.Services;

/// <summary>
/// 生成式視覺工廠 · Factory for code-drawn "rich" visuals used by <see cref="TweakDefinition.VisualBuilder"/>.
///
/// 全部用 XAML 形狀／Canvas 即場畫，無任何外部圖片或工具；主題色由 ThemeResource 取，跟深淺色自動變。
/// Everything is drawn live with XAML shapes/Canvas — no external images or tools. Colours come from
/// ThemeResource so the visuals follow light/dark theme. Each builder returns a <see cref="FrameworkElement"/>.
/// </summary>
public static class TweakVisuals
{
    // ---- 主題刷／顏色小工具 · theme brush & colour helpers ----

    private static Brush Res(string key, Color fallback)
    {
        var r = Application.Current.Resources;
        return r.TryGetValue(key, out var v) && v is Brush b ? b : new SolidColorBrush(fallback);
    }

    private static Brush Secondary => Res("TextFillColorSecondaryBrush", Color.FromArgb(255, 120, 120, 120));
    private static Brush Tertiary => Res("TextFillColorTertiaryBrush", Color.FromArgb(255, 150, 150, 150));
    private static Brush Stroke => Res("CardStrokeColorDefaultBrush", Color.FromArgb(40, 0, 0, 0));
    private static Brush Track => Res("ControlFillColorSecondaryBrush", Color.FromArgb(40, 128, 128, 128));
    private static Brush Accent => Res("AccentFillColorDefaultBrush", Color.FromArgb(255, 0, 120, 215));

    private static Color GoodColor => Color.FromArgb(255, 0x6C, 0xCB, 0x5F);
    private static Color WarnColor => Color.FromArgb(255, 0xF2, 0xC2, 0x3E);
    private static Color BadColor => Color.FromArgb(255, 0xE8, 0x4C, 0x3D);

    private static TextBlock Caption(string text, Brush? brush = null, double size = 12, bool semi = false)
        => new()
        {
            Text = text,
            FontSize = size,
            Foreground = brush ?? Secondary,
            FontWeight = semi ? FontWeights.SemiBold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap,
        };

    private static Color StatusToColor(StatusColor c) => c switch
    {
        StatusColor.Good => GoodColor,
        StatusColor.Warn => WarnColor,
        StatusColor.Bad => BadColor,
        _ => ((SolidColorBrush)Accent).Color,
    };

    private static Color HexToColor(string hex)
    {
        var s = (hex ?? "").Trim().TrimStart('#');
        if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        if (s.Length != 6) return ((SolidColorBrush)Accent).Color;
        byte r = byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromArgb(255, r, g, b);
    }

    private static Color Mix(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(255,
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    // ======================================================================
    //  1) 主題預覽小視窗 · Light/dark + accent + transparency mini-window preview
    // ======================================================================

    /// <summary>
    /// 畫一個迷你「示範視窗」，反映目前深／淺色、主題色同透明設定 ·
    /// Draws a mini mock window reflecting current dark/light, accent colour and transparency.
    /// </summary>
    public static FrameworkElement ThemePreview(
        Func<bool> isDark, Func<bool> accentOnChrome, Func<bool> transparency)
    {
        bool dark = SafeBool(isDark);
        bool accent = SafeBool(accentOnChrome);
        bool glass = SafeBool(transparency);

        Color bg = dark ? Color.FromArgb(255, 32, 32, 32) : Color.FromArgb(255, 243, 243, 243);
        Color surface = dark ? Color.FromArgb(255, 44, 44, 44) : Colors.White;
        Color text = dark ? Color.FromArgb(255, 235, 235, 235) : Color.FromArgb(255, 30, 30, 30);
        Color subtext = dark ? Color.FromArgb(255, 150, 150, 150) : Color.FromArgb(255, 120, 120, 120);
        Color accentC = ((SolidColorBrush)Accent).Color;
        Color titleBar = accent ? accentC : surface;
        Color titleText = accent ? PickReadable(accentC) : text;

        var root = new Border
        {
            Width = 220,
            Height = 132,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(bg),
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Title bar
        var bar = new Grid
        {
            Background = new SolidColorBrush(glass ? Color.FromArgb(210, titleBar.R, titleBar.G, titleBar.B) : titleBar),
            Padding = new Thickness(8, 0, 8, 0),
        };
        var barStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        barStack.Children.Add(new TextBlock { Text = "WinForge", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(titleText) });
        bar.Children.Add(barStack);
        var dots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        foreach (var dc in new[] { Color.FromArgb(255, 0x2E, 0xCC, 0x71), WarnColor, BadColor })
            dots.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(dc) });
        bar.Children.Add(dots);
        Grid.SetRow(bar, 0);
        grid.Children.Add(bar);

        // Body
        var body = new StackPanel { Spacing = 6, Margin = new Thickness(12, 10, 12, 0) };
        body.Children.Add(new TextBlock { Text = "Aa  字", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(text) });
        body.Children.Add(new TextBlock { Text = "Preview · 預覽", FontSize = 11, Foreground = new SolidColorBrush(subtext) });
        Grid.SetRow(body, 1);
        grid.Children.Add(body);

        // Accent button + chip row
        var ctl = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(12, 8, 12, 10) };
        ctl.Children.Add(new Border
        {
            Background = new SolidColorBrush(accentC),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 4, 10, 4),
            Child = new TextBlock { Text = "Button", FontSize = 10, Foreground = new SolidColorBrush(PickReadable(accentC)) },
        });
        ctl.Children.Add(new Border
        {
            Width = 46, Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = new SolidColorBrush(accentC),
            Child = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(Colors.White), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 0) },
        });
        Grid.SetRow(ctl, 2);
        grid.Children.Add(ctl);

        root.Child = grid;

        return WithCaption(root,
            Loc.I.Pick(
                $"{(dark ? "Dark" : "Light")} · accent {(accent ? "on chrome" : "subtle")} · {(glass ? "transparent" : "opaque")}",
                $"{(dark ? "深色" : "淺色")} · 主題色{(accent ? "上框" : "淡")} · {(glass ? "透明" : "不透明")}"));
    }

    // ======================================================================
    //  2) 顏色預覽（色板＋色階）· Accent / colour swatch with tints & shades
    // ======================================================================

    /// <summary>畫一個大色板加色階帶，活動反映目前選色 · Big swatch + tint/shade strip of the chosen colour.</summary>
    public static FrameworkElement ColorSwatch(Func<string> getHex)
    {
        string hex;
        try { hex = getHex() ?? "#0078D7"; } catch { hex = "#0078D7"; }
        Color c = HexToColor(hex);

        var panel = new StackPanel { Spacing = 8 };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };

        var big = new Border
        {
            Width = 64, Height = 64,
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(c),
            BorderBrush = Stroke,
            BorderThickness = new Thickness(1),
        };
        row.Children.Add(big);

        var meta = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        meta.Children.Add(Caption($"#{c.R:X2}{c.G:X2}{c.B:X2}", null, 14, true));
        meta.Children.Add(Caption($"RGB {c.R}, {c.G}, {c.B}", Tertiary, 11));
        row.Children.Add(meta);
        panel.Children.Add(row);

        // Tint→shade strip
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 };
        double[] stops = { 0.66, 0.42, 0.20, 0.0, -0.22, -0.42 };
        foreach (var s in stops)
        {
            Color shade = s >= 0 ? Mix(c, Colors.White, s) : Mix(c, Colors.Black, -s);
            strip.Children.Add(new Border { Width = 38, Height = 22, Background = new SolidColorBrush(shade) });
        }
        var stripWrap = new Border { CornerRadius = new CornerRadius(6), BorderBrush = Stroke, BorderThickness = new Thickness(1), Child = strip };
        // Clip rounded corners on the strip.
        strip.CornerRadius = new CornerRadius(6);
        panel.Children.Add(stripWrap);

        return panel;
    }

    // ======================================================================
    //  3) 半圓量錶 · Semicircle gauge for a normalised 0–1 value
    // ======================================================================

    /// <summary>
    /// 畫一個半圓量錶（0→1），用主題色填，下面有大字數值 ·
    /// Semicircle gauge for a 0..1 fraction, themed arc + big value label.
    /// </summary>
    public static FrameworkElement Gauge(
        Func<double> getFraction, Func<string> valueLabel,
        StatusColor color = StatusColor.Neutral, string? captionEn = null, string? captionZh = null)
    {
        double frac;
        try { frac = Math.Clamp(getFraction(), 0, 1); } catch { frac = 0; }
        string label;
        try { label = valueLabel() ?? ""; } catch { label = ""; }

        const double w = 160, h = 92, cx = 80, cy = 84, r = 66;
        var canvas = new Canvas { Width = w, Height = h };

        // Background track (full semicircle)
        canvas.Children.Add(Arc(cx, cy, r, 180, 360, Track, 12));
        // Value arc
        double endAngle = 180 + 180 * frac;
        Color fill = StatusToColor(color);
        canvas.Children.Add(Arc(cx, cy, r, 180, endAngle, new SolidColorBrush(fill), 12));

        // Needle tip dot
        double rad = endAngle * Math.PI / 180.0;
        canvas.Children.Add(new Ellipse
        {
            Width = 14, Height = 14, Fill = new SolidColorBrush(fill),
            Stroke = Res("CardBackgroundFillColorSecondaryBrush", Colors.White), StrokeThickness = 2,
        });
        var tip = (Ellipse)canvas.Children[^1];
        Canvas.SetLeft(tip, cx + r * Math.Cos(rad) - 7);
        Canvas.SetTop(tip, cy + r * Math.Sin(rad) - 7);

        var big = new TextBlock
        {
            Text = label, FontSize = 18, FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(fill),
        };
        Canvas.SetLeft(big, 0); Canvas.SetTop(big, 58);
        var bigWrap = new Border { Width = w, Child = big };
        big.HorizontalAlignment = HorizontalAlignment.Center;
        Canvas.SetLeft(bigWrap, 0); Canvas.SetTop(bigWrap, 56);
        canvas.Children.Add(bigWrap);

        string cap = Loc.I.Pick(captionEn ?? "", captionZh ?? "");
        return string.IsNullOrEmpty(cap) ? canvas : WithCaption(canvas, cap);
    }

    private static XamlPath Arc(double cx, double cy, double r, double startDeg, double endDeg, Brush stroke, double thickness)
    {
        double s = startDeg * Math.PI / 180.0, e = endDeg * Math.PI / 180.0;
        var p1 = new Windows.Foundation.Point(cx + r * Math.Cos(s), cy + r * Math.Sin(s));
        var p2 = new Windows.Foundation.Point(cx + r * Math.Cos(e), cy + r * Math.Sin(e));
        bool large = (endDeg - startDeg) > 180;
        var fig = new PathFigure { StartPoint = p1, IsClosed = false };
        fig.Segments.Add(new ArcSegment
        {
            Point = p2,
            Size = new Windows.Foundation.Size(r, r),
            SweepDirection = SweepDirection.Clockwise,
            IsLargeArc = large,
        });
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return new XamlPath { Data = geo, Stroke = stroke, StrokeThickness = thickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    }

    // ======================================================================
    //  4) 長條圖 · Horizontal stacked bar (e.g. disk used vs free)
    // ======================================================================

    public sealed record BarSegment(double Value, StatusColor Color, string LabelEn, string LabelZh);

    /// <summary>畫一條水平堆疊長條加圖例 · Horizontal stacked bar with a legend.</summary>
    public static FrameworkElement StackedBar(Func<IReadOnlyList<BarSegment>> getSegments, string? titleEn = null, string? titleZh = null)
    {
        IReadOnlyList<BarSegment> segs;
        try { segs = getSegments() ?? Array.Empty<BarSegment>(); } catch { segs = Array.Empty<BarSegment>(); }

        double total = 0;
        foreach (var s in segs) total += Math.Max(0, s.Value);
        if (total <= 0) total = 1;

        var panel = new StackPanel { Spacing = 8 };
        if (titleEn is not null) panel.Children.Add(Caption(Loc.I.Pick(titleEn, titleZh ?? titleEn), null, 12, true));

        var bar = new Grid { Height = 22, MinWidth = 240, ColumnSpacing = 0 };
        bar.CornerRadius = new CornerRadius(6);
        bar.BorderBrush = Stroke;
        bar.BorderThickness = new Thickness(1);
        int col = 0;
        foreach (var s in segs)
        {
            double frac = Math.Max(0, s.Value) / total;
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(frac, GridUnitType.Star) });
            var cell = new Border { Background = new SolidColorBrush(StatusToColor(s.Color)) };
            Grid.SetColumn(cell, col++);
            bar.Children.Add(cell);
        }
        // Clip the row to rounded corners.
        var barWrap = new Border { CornerRadius = new CornerRadius(6), Child = bar };
        panel.Children.Add(barWrap);

        var legend = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var s in segs)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            item.Children.Add(new Rectangle { Width = 10, Height = 10, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(StatusToColor(s.Color)) });
            item.Children.Add(Caption($"{Loc.I.Pick(s.LabelEn, s.LabelZh)}", Secondary, 11));
            legend.Children.Add(item);
        }
        panel.Children.Add(legend);
        return panel;
    }

    // ======================================================================
    //  5) 走勢線 · Sparkline (e.g. ping latency samples)
    // ======================================================================

    /// <summary>畫一條走勢線（含面積填色），用嚟顯示一串數值（例如 ping 延遲）· Sparkline with soft area fill.</summary>
    public static FrameworkElement Sparkline(
        Func<IReadOnlyList<double>> getSamples, StatusColor color = StatusColor.Neutral,
        string? captionEn = null, string? captionZh = null)
    {
        IReadOnlyList<double> data;
        try { data = getSamples() ?? Array.Empty<double>(); } catch { data = Array.Empty<double>(); }

        const double w = 260, h = 64, pad = 4;
        var canvas = new Canvas { Width = w, Height = h };
        // Baseline
        canvas.Children.Add(new Line { X1 = 0, Y1 = h - pad, X2 = w, Y2 = h - pad, Stroke = Stroke, StrokeThickness = 1 });

        if (data.Count >= 2)
        {
            double min = double.MaxValue, max = double.MinValue;
            foreach (var v in data) { if (v < min) min = v; if (v > max) max = v; }
            if (Math.Abs(max - min) < 1e-9) { max = min + 1; }
            double range = max - min;
            double stepX = (w - pad * 2) / (data.Count - 1);

            Point P(int i) => new(pad + i * stepX, pad + (h - pad * 2) * (1 - (data[i] - min) / range));

            Color line = StatusToColor(color);
            // Area
            var area = new PathFigure { StartPoint = new Windows.Foundation.Point(P(0).X, h - pad), IsClosed = true };
            area.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(P(0).X, P(0).Y) });
            for (int i = 1; i < data.Count; i++) area.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(P(i).X, P(i).Y) });
            area.Segments.Add(new LineSegment { Point = new Windows.Foundation.Point(P(data.Count - 1).X, h - pad) });
            var ageo = new PathGeometry(); ageo.Figures.Add(area);
            canvas.Children.Add(new XamlPath { Data = ageo, Fill = new SolidColorBrush(Color.FromArgb(48, line.R, line.G, line.B)) });

            // Line
            var pts = new PointCollection();
            for (int i = 0; i < data.Count; i++) pts.Add(new Windows.Foundation.Point(P(i).X, P(i).Y));
            canvas.Children.Add(new Polyline { Points = pts, Stroke = new SolidColorBrush(line), StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round });

            // Last point dot
            var last = P(data.Count - 1);
            var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(line) };
            Canvas.SetLeft(dot, last.X - 4); Canvas.SetTop(dot, last.Y - 4);
            canvas.Children.Add(dot);
        }
        else
        {
            var none = Caption(Loc.I.Pick("Run to sample", "執行後取樣"), Tertiary, 12);
            Canvas.SetLeft(none, 6); Canvas.SetTop(none, h / 2 - 10);
            canvas.Children.Add(none);
        }

        string cap = Loc.I.Pick(captionEn ?? "", captionZh ?? "");
        return string.IsNullOrEmpty(cap) ? canvas : WithCaption(canvas, cap);
    }

    private readonly record struct Point(double X, double Y);

    // ---- 共用 · shared ----

    private static FrameworkElement WithCaption(FrameworkElement visual, string caption)
    {
        var sp = new StackPanel { Spacing = 6 };
        sp.Children.Add(visual);
        sp.Children.Add(Caption(caption, Secondary, 11));
        return sp;
    }

    private static bool SafeBool(Func<bool> f) { try { return f(); } catch { return false; } }

    /// <summary>揀黑或白做前景，確保喺指定背景上對比足夠 · Pick black/white text for contrast on a colour.</summary>
    private static Color PickReadable(Color bg)
    {
        double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
        return lum > 0.6 ? Color.FromArgb(255, 20, 20, 20) : Colors.White;
    }
}
