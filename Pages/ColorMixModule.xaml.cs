using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI; // Windows.UI.Color (avoid bare Color ambiguity under Microsoft.UI)
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 混色器 · Colour Mixer — blend two colours across sRGB-linear / RGB-average / HSL spaces at a
/// chosen ratio, and build an N-step gradient of copyable swatches. Pure managed; never throws.
/// </summary>
public sealed partial class ColorMixModule : Page
{
    /// <summary>Row model for the gradient ListView (classic {Binding}, no x:Bind).</summary>
    public sealed class StepRow
    {
        public string Hex { get; set; } = "#000000";
        public string Detail { get; set; } = "";
        public Brush Brush { get; set; } = new SolidColorBrush();
    }

    private readonly ObservableCollection<StepRow> _rows = new();
    private bool _suppress;

    public ColorMixModule()
    {
        InitializeComponent();
        StepsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); RecomputeAll(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        RecomputeAll();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Colour Mixer · 混色器", "混色器 · Colour Mixer");
            HeaderBlurb.Text = P(
                "Blend two colours at any ratio across three colour spaces, then build a smooth gradient of copyable swatches. All maths runs locally — nothing leaves your PC.",
                "喺三種色彩空間、用任何比例溝埋兩隻色，再整條平滑漸變，每格色都可以㩒一下就複製。全部係本機計算，唔會傳出去。");

            InputsTitle.Text = P("Two colours", "兩隻色");
            LabelA.Text = P("Colour A (hex)", "色 A（十六進制）");
            LabelB.Text = P("Colour B (hex)", "色 B（十六進制）");
            RatioLabel.Text = RatioText();
            SpaceLabel.Text = P("Blend space", "混色空間");

            MixedTitle.Text = P("Mixed result", "混色結果");
            CopyMixBtn.Content = P("Copy hex", "複製十六進制");

            GradientTitle.Text = P("Gradient", "漸變色階");
            CopyCssBtn.Content = P("Copy CSS", "複製 CSS");
            StepsLabel.Text = P("Steps", "色階數");
            GradientHint.Text = P("Tap any swatch below to copy its hex.", "㩒下面任何一格就會複製佢嘅十六進制。");

            RebuildSpaceBox();
        }
        catch { /* never throw from UI text */ }
    }

    private string RatioText()
    {
        int pct = _suppress ? 50 : (int)Math.Round(SafeSlider());
        return P($"Mix ratio — {100 - pct}% A / {pct}% B", $"混色比例 — {100 - pct}% A ／ {pct}% B");
    }

    private double SafeSlider()
    {
        try { return double.IsNaN(RatioSlider.Value) ? 50 : RatioSlider.Value; }
        catch { return 50; }
    }

    private void RebuildSpaceBox()
    {
        try
        {
            int prev = SpaceBox.SelectedIndex;
            _suppress = true;
            SpaceBox.Items.Clear();
            SpaceBox.Items.Add(P("sRGB linear (gamma-correct)", "sRGB 線性（伽瑪校正）"));
            SpaceBox.Items.Add(P("Simple RGB average", "簡單 RGB 平均"));
            SpaceBox.Items.Add(P("HSL (hue / sat / light)", "HSL（色相／飽和／明度）"));
            SpaceBox.SelectedIndex = prev < 0 ? 0 : Math.Min(prev, 2);
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    private ColorMixService.BlendSpace CurrentSpace() => SpaceBox.SelectedIndex switch
    {
        1 => ColorMixService.BlendSpace.RgbAverage,
        2 => ColorMixService.BlendSpace.Hsl,
        _ => ColorMixService.BlendSpace.SrgbLinear
    };

    private static Windows.UI.Color ToColor(ColorMixService.Rgb c)
        => Color.FromArgb(255, c.R, c.G, c.B);

    private static SolidColorBrush Brush(ColorMixService.Rgb c) => new(ToColor(c));

    private void SetSwatch(Border b, ColorMixService.Rgb c)
    {
        try { b.Background = Brush(c); } catch { }
    }

    private static string Detail(ColorMixService.Rgb c) => $"{c.RgbCss}  ·  {c.HslCss}";

    // --- Event handlers -----------------------------------------------------

    private void Inputs_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        RecomputeAll();
    }

    private void Ratio_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        try { RatioLabel.Text = RatioText(); } catch { }
        RecomputeMixed();
    }

    private void Space_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        RecomputeAll();
    }

    private void Steps_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        RecomputeGradient();
    }

    private void Step_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StepRow row)
            CopyText(row.Hex, P($"Copied {row.Hex}", $"已複製 {row.Hex}"));
    }

    private void CopyMix_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mixed = ComputeMixed();
            CopyText(mixed.Hex, P($"Copied {mixed.Hex}", $"已複製 {mixed.Hex}"));
        }
        catch { }
    }

    private void CopyCss_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var stops = ComputeGradient();
            var css = ColorMixService.GradientCss(stops);
            CopyText("background: " + css + ";", P("Gradient CSS copied", "已複製漸變 CSS"));
        }
        catch { }
    }

    // --- Compute ------------------------------------------------------------

    private ColorMixService.Rgb ParseA() => ColorMixService.Parse(SafeText(HexA));
    private ColorMixService.Rgb ParseB() => ColorMixService.Parse(SafeText(HexB));

    private static string SafeText(TextBox tb)
    {
        try { return tb.Text ?? ""; } catch { return ""; }
    }

    private ColorMixService.Rgb ComputeMixed()
    {
        double t = SafeSlider() / 100.0;
        return ColorMixService.Mix(ParseA(), ParseB(), t, CurrentSpace());
    }

    private List<ColorMixService.Rgb> ComputeGradient()
    {
        int steps = 7;
        try
        {
            var v = StepsBox.Value;
            if (!double.IsNaN(v)) steps = (int)Math.Round(v);
        }
        catch { }
        if (steps < 3) steps = 3;
        if (steps > 20) steps = 20;
        return ColorMixService.Gradient(ParseA(), ParseB(), steps, CurrentSpace());
    }

    private void RecomputeAll()
    {
        RecomputeInputs();
        RecomputeMixed();
        RecomputeGradient();
    }

    private void RecomputeInputs()
    {
        try
        {
            SetSwatch(SwatchA, ParseA());
            SetSwatch(SwatchB, ParseB());
        }
        catch { }
    }

    private void RecomputeMixed()
    {
        try
        {
            var m = ComputeMixed();
            SetSwatch(MixedSwatch, m);
            MixedHex.Text = m.Hex;
            MixedRgb.Text = m.RgbCss;
            MixedHsl.Text = m.HslCss;
        }
        catch { }
    }

    private void RecomputeGradient()
    {
        try
        {
            var stops = ComputeGradient();

            // Paint the preview bar as a linear gradient brush.
            try
            {
                var lg = new LinearGradientBrush { StartPoint = new(0, 0.5), EndPoint = new(1, 0.5) };
                for (int i = 0; i < stops.Count; i++)
                {
                    double off = stops.Count == 1 ? 0 : i / (double)(stops.Count - 1);
                    lg.GradientStops.Add(new GradientStop { Color = ToColor(stops[i]), Offset = off });
                }
                GradientBar.Background = lg;
            }
            catch { }

            _rows.Clear();
            for (int i = 0; i < stops.Count; i++)
            {
                var c = stops[i];
                _rows.Add(new StepRow
                {
                    Hex = c.Hex,
                    Detail = Detail(c),
                    Brush = Brush(c)
                });
            }
        }
        catch { }
    }

    // --- Clipboard ----------------------------------------------------------

    private void CopyText(string text, string toast)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(text ?? "");
            Clipboard.SetContent(dp);
            ShowInfo(toast, InfoBarSeverity.Success);
        }
        catch
        {
            ShowInfo(P("Couldn't access the clipboard.", "暫時用唔到剪貼簿。"), InfoBarSeverity.Warning);
        }
    }

    private void ShowInfo(string msg, InfoBarSeverity sev)
    {
        try
        {
            Info.Severity = sev;
            Info.Message = msg;
            Info.IsOpen = true;
        }
        catch { }
    }
}
