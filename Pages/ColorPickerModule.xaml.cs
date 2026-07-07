using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 螢幕取色（PowerToys Color Picker 式）· Screen colour picker — click anywhere to grab a pixel's colour,
/// see HEX / RGB / HSL, copy any format, with a small history. No redirect. Bilingual.
/// </summary>
public sealed partial class ColorPickerModule : Page
{
    private byte _r, _g, _b;

    public ColorPickerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); SetColor(0x2D, 0x7D, 0x46); };
        Unloaded += (_, _) => { ColorPickService.StopPick(); Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Color Picker · 螢幕取色";
        HeaderBlurb.Text = P("Click \"Pick\" then click anywhere on screen to grab that pixel's colour. Right-click cancels. Copy HEX, RGB or HSL.",
            "撳「取色」之後喺螢幕任何位置撳一下，就攞到嗰點嘅顏色。右鍵取消。可以複製 HEX、RGB 或 HSL。");
        PickBtn.Content = P("Pick from screen", "螢幕取色");
        ApplyHexBtn.Content = P("Apply", "套用");
        HistoryLabel.Text = P("Recent", "最近");
        if (!ColorPickService.IsPicking) LiveHint.Text = "";
    }

    private void Pick_Click(object sender, RoutedEventArgs e)
    {
        if (ColorPickService.IsPicking) return;
        LiveHint.Text = P("Click anywhere to pick · right-click to cancel", "撳任何位置取色 · 右鍵取消");
        ColorPickService.StartPick(
            onMove: (x, y, r, g, b) => DispatcherQueue.TryEnqueue(() => LivePreview.Background = Brush(r, g, b)),
            onPick: (x, y, r, g, b) => DispatcherQueue.TryEnqueue(() => { LiveHint.Text = ""; SetColor(r, g, b); AddHistory(r, g, b); }),
            onCancel: () => DispatcherQueue.TryEnqueue(() => LiveHint.Text = P("Cancelled", "已取消")));
    }

    private void SetColor(byte r, byte g, byte b)
    {
        _r = r; _g = g; _b = b;
        Swatch.Background = Brush(r, g, b);
        HexText.Text = ColorPickService.Hex(r, g, b);
        RgbText.Text = $"rgb({r}, {g}, {b})";
        HslText.Text = ColorPickService.Hsl(r, g, b);
    }

    private void AddHistory(byte r, byte g, byte b)
    {
        var sw = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Background = Brush(r, g, b),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
        };
        ToolTipService.SetToolTip(sw, ColorPickService.Hex(r, g, b));
        byte rr = r, gg = g, bb = b;
        sw.Tapped += (_, _) => SetColor(rr, gg, bb);
        History.Items.Insert(0, sw);
        if (History.Items.Count > 16) History.Items.RemoveAt(History.Items.Count - 1);
    }

    private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Color.FromArgb(255, r, g, b));

    private void ApplyHex_Click(object sender, RoutedEventArgs e)
    {
        string s = (HexInput.Text ?? "").Trim().TrimStart('#');
        if (s.Length == 6 &&
            byte.TryParse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
            byte.TryParse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
            byte.TryParse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            SetColor(r, g, b);
            AddHistory(r, g, b);
        }
    }

    private void Copy(string text)
    {
        var dp = new DataPackage();
        dp.SetText(text);
        Clipboard.SetContent(dp);
    }

    private void CopyHex_Click(object sender, RoutedEventArgs e) => Copy(ColorPickService.Hex(_r, _g, _b));
    private void CopyRgb_Click(object sender, RoutedEventArgs e) => Copy($"rgb({_r}, {_g}, {_b})");
    private void CopyHsl_Click(object sender, RoutedEventArgs e) => Copy(ColorPickService.Hsl(_r, _g, _b));
}
