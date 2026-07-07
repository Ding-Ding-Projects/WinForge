using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 色彩調色板產生器 · Color-palette generator — base colour (#hex / rgb) + R/G/B sliders + random,
/// pick a scheme (complementary, analogous, triadic, …) and get click-to-copy swatches, plus
/// export the palette as CSS variables or a JSON array. Pure-managed, bilingual (粵語). Never throws.
/// </summary>
public sealed partial class ColorPaletteModule : Page
{
    private bool _suppress;
    private ColorPaletteService.Rgb _base = new(0x3E, 0xB4, 0x89);
    private List<ColorPaletteService.Rgb> _palette = new();

    public ColorPaletteModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildSchemeItems();
        SyncSlidersFromBase();
        Render();
        Regenerate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        int idx = SchemeBox.SelectedIndex;
        BuildSchemeItems();
        if (idx >= 0 && idx < SchemeBox.Items.Count) { _suppress = true; SchemeBox.SelectedIndex = idx; _suppress = false; }
        Render();
        Regenerate();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Color Palette · 色彩調色板";
            HeaderBlurb.Text = P("Pick a base colour by hex, rgb or the R/G/B sliders, choose a colour scheme and get a ready-to-use palette. Click any swatch to copy its hex; export the whole palette as CSS variables or JSON.",
                "用 hex、rgb 或者 R/G/B 滑桿揀個底色，再揀種配色方案，就即刻整到一組色板。撳任何一格就複製佢個 hex；成組色板可以匯出做 CSS 變數或者 JSON。");
            BaseLabel.Text = P("Base colour (#hex or r,g,b)", "底色（#hex 或者 r,g,b）");
            RandomBtn.Content = P("Random", "隨機");
            RLabel.Text = "R";
            GLabel.Text = "G";
            BLabel.Text = "B";
            SchemeLabel.Text = P("Scheme", "配色方案");
            CopyCssBtn.Content = P("Copy CSS", "複製 CSS");
            CopyJsonBtn.Content = P("Copy JSON", "複製 JSON");
        }
        catch { /* never throw from UI */ }
    }

    private void BuildSchemeItems()
    {
        _suppress = true;
        int keep = SchemeBox.SelectedIndex;
        SchemeBox.Items.Clear();
        SchemeBox.Items.Add(P("Complementary", "互補色"));
        SchemeBox.Items.Add(P("Analogous", "類似色"));
        SchemeBox.Items.Add(P("Triadic", "三等分色"));
        SchemeBox.Items.Add(P("Tetradic", "四等分色"));
        SchemeBox.Items.Add(P("Split-Complementary", "分裂互補色"));
        SchemeBox.Items.Add(P("Monochromatic", "單色系"));
        SchemeBox.Items.Add(P("Shades", "暗調"));
        SchemeBox.Items.Add(P("Tints", "淺調"));
        SchemeBox.SelectedIndex = keep >= 0 && keep < SchemeBox.Items.Count ? keep : 0;
        _suppress = false;
    }

    private ColorPaletteService.Scheme SelectedScheme() => SchemeBox.SelectedIndex switch
    {
        1 => ColorPaletteService.Scheme.Analogous,
        2 => ColorPaletteService.Scheme.Triadic,
        3 => ColorPaletteService.Scheme.Tetradic,
        4 => ColorPaletteService.Scheme.SplitComplementary,
        5 => ColorPaletteService.Scheme.Monochromatic,
        6 => ColorPaletteService.Scheme.Shades,
        7 => ColorPaletteService.Scheme.Tints,
        _ => ColorPaletteService.Scheme.Complementary
    };

    // ---- Event handlers ---------------------------------------------------

    private void Base_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        if (ColorPaletteService.TryParse(BaseBox.Text, out var rgb))
        {
            _base = rgb;
            SyncSlidersFromBase();
            Regenerate();
        }
        else
        {
            StatusText.Text = P("Couldn't read that colour — try #3EB489 or 62,180,137.",
                "睇唔明呢個色 — 試下 #3EB489 或者 62,180,137。");
        }
    }

    private void Slider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        _base = new ColorPaletteService.Rgb(
            (byte)Math.Round(RSlider.Value),
            (byte)Math.Round(GSlider.Value),
            (byte)Math.Round(BSlider.Value));
        _suppress = true;
        BaseBox.Text = _base.Hex;
        _suppress = false;
        Regenerate();
    }

    private void Scheme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Regenerate();
    }

    private void Random_Click(object sender, RoutedEventArgs e)
    {
        _base = ColorPaletteService.RandomColor();
        _suppress = true;
        BaseBox.Text = _base.Hex;
        _suppress = false;
        SyncSlidersFromBase();
        Regenerate();
    }

    private void CopyCss_Click(object sender, RoutedEventArgs e)
        => CopyToClipboard(ColorPaletteService.ToCss(_palette), P("CSS variables copied.", "已複製 CSS 變數。"));

    private void CopyJson_Click(object sender, RoutedEventArgs e)
        => CopyToClipboard(ColorPaletteService.ToJson(_palette), P("JSON array copied.", "已複製 JSON 陣列。"));

    // ---- Core -------------------------------------------------------------

    private void SyncSlidersFromBase()
    {
        _suppress = true;
        RSlider.Value = _base.R;
        GSlider.Value = _base.G;
        BSlider.Value = _base.B;
        _suppress = false;
        try { BasePreview.Background = BrushOf(_base); } catch { }
    }

    private void Regenerate()
    {
        try
        {
            _palette = ColorPaletteService.Generate(_base, SelectedScheme());
            try { BasePreview.Background = BrushOf(_base); } catch { }
            SwatchRow.Children.Clear();
            foreach (var c in _palette)
                SwatchRow.Children.Add(BuildSwatch(c));
            StatusText.Text = P($"{_palette.Count} colours — click a swatch to copy its hex.",
                $"{_palette.Count} 隻色 — 撳一格就複製佢個 hex。");
        }
        catch
        {
            StatusText.Text = P("Something went wrong generating the palette.", "產生色板嗰陣出咗問題。");
        }
    }

    private FrameworkElement BuildSwatch(ColorPaletteService.Rgb c)
    {
        var panel = new StackPanel { Spacing = 4, Width = 88 };

        var border = new Border
        {
            Width = 88,
            Height = 64,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            Tag = c.Hex
        };
        try { border.Background = BrushOf(c); } catch { }
        try { border.BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"]; } catch { }
        ToolTipService.SetToolTip(border, P("Click to copy", "撳一下複製"));
        border.PointerPressed += Swatch_Pressed;

        var lbl = new TextBlock
        {
            Text = c.Hex,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        panel.Children.Add(border);
        panel.Children.Add(lbl);
        return panel;
    }

    private void Swatch_Pressed(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            if (sender is Border b && b.Tag is string hex)
                CopyToClipboard(hex, P($"Copied {hex}", $"已複製 {hex}"));
        }
        catch { /* never throw */ }
    }

    private void CopyToClipboard(string text, string okMessage)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(text ?? string.Empty);
            Clipboard.SetContent(dp);
            StatusText.Text = okMessage;
        }
        catch
        {
            StatusText.Text = P("Couldn't reach the clipboard.", "用唔到剪貼簿。");
        }
    }

    private static Brush BrushOf(ColorPaletteService.Rgb c)
        => new SolidColorBrush(Windows.UI.Color.FromArgb(255, c.R, c.G, c.B));
}
