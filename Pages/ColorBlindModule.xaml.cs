using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 色盲模擬 · Colour-blindness simulator. Enter a hex/rgb colour (or drive R/G/B sliders) and see the same
/// colour as it appears under Protanopia/-anomaly, Deuteranopia/-anomaly, Tritanopia/-anomaly and
/// Achromatopsia. Pure managed; swatch brushes built in code. Bilingual (粵語). Never throws.
/// </summary>
public sealed partial class ColorBlindModule : Page
{
    /// <summary>Row shown in the simulated-swatch list. Colours are supplied as a ready-made brush from code.</summary>
    public sealed class SimRow
    {
        public string Name { get; set; } = "";
        public string Hex { get; set; } = "";
        public string CopyLabel { get; set; } = "";
        public Brush? Brush { get; set; }
    }

    private static readonly Random _rng = new();
    private byte _r = 0x4C, _g = 0xAF, _b = 0x50;
    private bool _suppress;

    public ColorBlindModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        SyncSlidersFromColor();
        Recompute(fromParse: false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Render();
        Recompute(fromParse: false);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Colour-blindness Sim · 色盲模擬";
        HeaderBlurb.Text = P("Enter a colour and see how it looks to people with different types of colour-vision deficiency. Useful for checking whether your UI, charts or labels stay readable.",
            "輸入一隻顏色，睇下唔同色盲人士眼中係點樣。方便檢查你嘅介面、圖表或者標籤色會唔會撞到分唔到。");
        InputLabel.Text = P("Colour (hex or r,g,b)", "顏色（十六進或 r,g,b）");
        ApplyBtn.Content = P("Apply", "套用");
        RandomBtn.Content = P("Random", "隨機");
        BaseTitle.Text = P("Original", "原色");
        ApproxNote.Text = P("Note: these are approximations of colour-vision deficiency, generated with standard simulation matrices — not a medical diagnosis.",
            "注意：以上係用標準模擬矩陣做出嚟嘅色盲近似效果，僅供參考，並非醫學診斷。");
    }

    private void SyncSlidersFromColor()
    {
        _suppress = true;
        RSlider.Value = _r; GSlider.Value = _g; BSlider.Value = _b;
        _suppress = false;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (ColorBlindService.TryParse(InputBox.Text, out byte r, out byte g, out byte b))
        {
            _r = r; _g = g; _b = b;
            SyncSlidersFromColor();
            Recompute(fromParse: true);
        }
        else
        {
            StatusText.Text = P("Could not read that colour. Try #4CAF50, 4CAF50, or 76,175,80.",
                "讀唔到嗰隻顏色。試下 #4CAF50、4CAF50 或者 76,175,80。");
        }
    }

    private void Random_Click(object sender, RoutedEventArgs e)
    {
        _r = (byte)_rng.Next(256);
        _g = (byte)_rng.Next(256);
        _b = (byte)_rng.Next(256);
        InputBox.Text = ColorBlindService.ToHex(_r, _g, _b);
        SyncSlidersFromColor();
        Recompute(fromParse: true);
    }

    private void Slider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        _r = (byte)Math.Clamp((int)Math.Round(RSlider.Value), 0, 255);
        _g = (byte)Math.Clamp((int)Math.Round(GSlider.Value), 0, 255);
        _b = (byte)Math.Clamp((int)Math.Round(BSlider.Value), 0, 255);
        InputBox.Text = ColorBlindService.ToHex(_r, _g, _b);
        Recompute(fromParse: true);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.Tag is string hex && !string.IsNullOrEmpty(hex))
            {
                var pkg = new DataPackage();
                pkg.SetText(hex);
                Clipboard.SetContent(pkg);
                StatusText.Text = P($"Copied {hex} to the clipboard.", $"已複製 {hex} 到剪貼簿。");
            }
        }
        catch
        {
            StatusText.Text = P("Could not copy to the clipboard.", "複製到剪貼簿失敗。");
        }
    }

    private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
        => new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));

    private void Recompute(bool fromParse)
    {
        try
        {
            RVal.Text = _r.ToString();
            GVal.Text = _g.ToString();
            BVal.Text = _b.ToString();

            BaseSwatch.Background = MakeBrush(_r, _g, _b);
            BaseHex.Text = ColorBlindService.ToHex(_r, _g, _b);

            string copy = P("Copy", "複製");
            var rows = new List<SimRow>();
            foreach (var (type, en, zh) in Types)
            {
                var (sr, sg, sb) = ColorBlindService.Simulate(_r, _g, _b, type);
                rows.Add(new SimRow
                {
                    Name = P(en, zh),
                    Hex = ColorBlindService.ToHex(sr, sg, sb),
                    CopyLabel = copy,
                    Brush = MakeBrush(sr, sg, sb),
                });
            }
            SimList.ItemsSource = rows;

            if (fromParse)
                StatusText.Text = P($"Simulating {ColorBlindService.ToHex(_r, _g, _b)} across 7 deficiency types.",
                    $"正模擬 {ColorBlindService.ToHex(_r, _g, _b)}，涵蓋 7 種色覺缺陷。");
        }
        catch
        {
            StatusText.Text = P("Something went wrong while simulating that colour.", "模擬嗰隻顏色嗰陣出咗問題。");
        }
    }

    private static readonly (ColorBlindService.Cvd Type, string En, string Zh)[] Types =
    {
        (ColorBlindService.Cvd.Protanopia, "Protanopia (no red)", "紅色盲（缺紅）"),
        (ColorBlindService.Cvd.Protanomaly, "Protanomaly (weak red)", "紅色弱（弱紅）"),
        (ColorBlindService.Cvd.Deuteranopia, "Deuteranopia (no green)", "綠色盲（缺綠）"),
        (ColorBlindService.Cvd.Deuteranomaly, "Deuteranomaly (weak green)", "綠色弱（弱綠）"),
        (ColorBlindService.Cvd.Tritanopia, "Tritanopia (no blue)", "藍色盲（缺藍）"),
        (ColorBlindService.Cvd.Tritanomaly, "Tritanomaly (weak blue)", "藍色弱（弱藍）"),
        (ColorBlindService.Cvd.Achromatopsia, "Achromatopsia (grayscale)", "全色盲（灰階）"),
    };
}
