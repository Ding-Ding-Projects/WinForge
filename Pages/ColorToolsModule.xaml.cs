using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 色彩工具 · Color Tools — convert a colour between HEX / RGB / HSL / HSV / CMYK, adjust it with
/// R/G/B sliders, roll a random colour, generate a harmonious 5-swatch palette, and run a WCAG
/// contrast check (foreground vs background) with AA/AAA badges. Pure managed, no redirect. Bilingual.
/// </summary>
public sealed partial class ColorToolsModule : Page
{
    private ColorToolsService.Rgb _current = new(0x2E, 0x8B, 0x57); // sea green start
    private ColorToolsService.Rgb _background = new(0xFF, 0xFF, 0xFF);
    private bool _suppress;

    public ColorToolsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            Render();
            _suppress = true;
            InputBox.Text = ColorToolsService.ToHex(_current);
            BgBox.Text = ColorToolsService.ToHex(_background);
            _suppress = false;
            SyncSlidersFromCurrent();
            RefreshAll();
        };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private static SolidColorBrush Brush(ColorToolsService.Rgb c) =>
        new(Color.FromArgb(255, c.R, c.G, c.B));

    private void Render()
    {
        Header.Title = P("Color Tools", "色彩工具");
        HeaderBlurb.Text = P("Convert a colour between HEX, RGB, HSL, HSV and CMYK, nudge it with sliders, build a harmonious palette, and check WCAG contrast for accessible text.",
            "喺 HEX、RGB、HSL、HSV、CMYK 之間轉換顏色，用滑桿微調，砌出協調嘅配色，仲可以檢查 WCAG 對比度睇下文字夠唔夠清。");
        RandomBtn.Content = P("Random", "隨機");
        RLabel.Text = "R"; GLabel.Text = "G"; BLabel.Text = "B";
        OutputsTitle.Text = P("Values (click to copy)", "數值（撳一下複製）");
        CopyHex.Content = CopyRgb.Content = CopyHsl.Content = CopyHsv.Content = CopyCmyk.Content = P("Copy", "複製");
        PaletteTitle.Text = P("Harmonious palette", "協調配色");
        PaletteHint.Text = P("Analogous ±60° / ±30° and the complement. Click a swatch to load it.",
            "類比色 ±60° / ±30° 加上互補色。撳一格就會載入。");
        ContrastTitle.Text = P("WCAG contrast", "WCAG 對比度");
        BgLabel.Text = P("Background colour", "背景顏色");
        SwapBtn.Content = P("Swap", "對調");
        PreviewText.Text = P("The quick brown fox", "示範文字 Sample text");
        RefreshAll();
    }

    // ---- input wiring ---------------------------------------------------

    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        if (ColorToolsService.TryParse(InputBox.Text, out var rgb))
        {
            _current = rgb;
            StatusText.Text = string.Empty;
            SyncSlidersFromCurrent();
            RefreshAll();
        }
        else
        {
            StatusText.Text = P("Not a valid colour. Try #RRGGBB, #RGB or rgb(r, g, b).",
                "唔係有效嘅顏色。可以試 #RRGGBB、#RGB 或者 rgb(r, g, b)。");
        }
    }

    private void Slider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        _current = new ColorToolsService.Rgb(
            (byte)RSlider.Value, (byte)GSlider.Value, (byte)BSlider.Value);
        _suppress = true;
        InputBox.Text = ColorToolsService.ToHex(_current);
        _suppress = false;
        StatusText.Text = string.Empty;
        RefreshAll();
    }

    private void Random_Click(object sender, RoutedEventArgs e)
    {
        _current = ColorToolsService.RandomRgb();
        _suppress = true;
        InputBox.Text = ColorToolsService.ToHex(_current);
        _suppress = false;
        StatusText.Text = string.Empty;
        SyncSlidersFromCurrent();
        RefreshAll();
    }

    private void Bg_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        if (ColorToolsService.TryParse(BgBox.Text, out var rgb))
        {
            _background = rgb;
            RefreshContrast();
        }
    }

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        var tmp = _current;
        _current = _background;
        _background = tmp;
        _suppress = true;
        InputBox.Text = ColorToolsService.ToHex(_current);
        BgBox.Text = ColorToolsService.ToHex(_background);
        _suppress = false;
        SyncSlidersFromCurrent();
        RefreshAll();
    }

    private void SyncSlidersFromCurrent()
    {
        _suppress = true;
        RSlider.Value = _current.R;
        GSlider.Value = _current.G;
        BSlider.Value = _current.B;
        _suppress = false;
    }

    // ---- rendering ------------------------------------------------------

    private void RefreshAll()
    {
        Swatch.Background = Brush(_current);
        RVal.Text = _current.R.ToString();
        GVal.Text = _current.G.ToString();
        BVal.Text = _current.B.ToString();

        HexOut.Text = ColorToolsService.ToHex(_current);
        RgbOut.Text = ColorToolsService.ToRgbString(_current);
        HslOut.Text = ColorToolsService.ToHslString(_current);
        HsvOut.Text = ColorToolsService.ToHsvString(_current);
        CmykOut.Text = ColorToolsService.ToCmykString(_current);

        BuildPalette();
        RefreshContrast();
    }

    private void BuildPalette()
    {
        PaletteRow.Children.Clear();
        foreach (var c in ColorToolsService.Palette(_current))
        {
            var local = c;
            var stack = new StackPanel { Spacing = 4, Width = 84 };
            var chip = new Border
            {
                Height = 56,
                CornerRadius = new CornerRadius(6),
                Background = Brush(local),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
            };
            chip.Tapped += (_, __) =>
            {
                _current = local;
                _suppress = true;
                InputBox.Text = ColorToolsService.ToHex(local);
                _suppress = false;
                StatusText.Text = string.Empty;
                SyncSlidersFromCurrent();
                RefreshAll();
            };
            var label = new TextBlock
            {
                Text = ColorToolsService.ToHex(local),
                FontSize = 11,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            stack.Children.Add(chip);
            stack.Children.Add(label);
            PaletteRow.Children.Add(stack);
        }
    }

    private void RefreshContrast()
    {
        PreviewBorder.Background = Brush(_background);
        PreviewText.Foreground = Brush(_current);

        double ratio = ColorToolsService.ContrastRatio(_current, _background);
        RatioText.Text = P($"Contrast ratio: {ratio:0.00} : 1", $"對比度：{ratio:0.00} : 1");

        BadgeRow.Children.Clear();
        AddBadge(P("AA normal", "AA 正常"), ratio >= 4.5);
        AddBadge(P("AAA normal", "AAA 正常"), ratio >= 7.0);
        AddBadge(P("AA large", "AA 大字"), ratio >= 3.0);
        AddBadge(P("AAA large", "AAA 大字"), ratio >= 4.5);
    }

    private void AddBadge(string text, bool pass)
    {
        var color = pass ? Color.FromArgb(255, 0x2E, 0x7D, 0x32) : Color.FromArgb(255, 0xB3, 0x26, 0x1A);
        var badge = new Border
        {
            Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
        };
        badge.Child = new TextBlock
        {
            Text = $"{text}  {(pass ? P("Pass", "通過") : P("Fail", "唔過"))}",
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.White),
        };
        BadgeRow.Children.Add(badge);
    }

    // ---- clipboard ------------------------------------------------------

    private void Copy(string text)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage
            {
                RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy,
            };
            dp.SetText(text ?? string.Empty);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            StatusText.Text = P($"Copied: {text}", $"已複製：{text}");
        }
        catch
        {
            StatusText.Text = P("Could not copy to the clipboard.", "無法複製到剪貼簿。");
        }
    }

    private void CopyHex_Click(object sender, RoutedEventArgs e) => Copy(HexOut.Text);
    private void CopyRgb_Click(object sender, RoutedEventArgs e) => Copy(RgbOut.Text);
    private void CopyHsl_Click(object sender, RoutedEventArgs e) => Copy(HslOut.Text);
    private void CopyHsv_Click(object sender, RoutedEventArgs e) => Copy(HsvOut.Text);
    private void CopyCmyk_Click(object sender, RoutedEventArgs e) => Copy(CmykOut.Text);
}
