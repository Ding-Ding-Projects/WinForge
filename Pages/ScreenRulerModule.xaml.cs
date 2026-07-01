using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 螢幕間尺（PowerToys Screen Ruler 式）· Screen Ruler — on-screen pixel measurement. Launch a
/// borderless transparent topmost overlay in one of five modes (Distance, Horizontal, Vertical,
/// Cross, Bounds), pick the line colour and thickness, and read live pixel counts + coordinates.
/// Left-click copies the measurement to the clipboard; Esc or right-click closes. Units are pixels.
/// A native clone of PowerToys' Measure Tool, all in-app. Bilingual.
/// </summary>
public sealed partial class ScreenRulerModule : Page
{
    // colour stored as 0x00RRGGBB (display) — converted to GDI BGR when launching.
    private uint _rgb = 0x00FFA500; // amber, like PowerToys' default ruler

    private static readonly (string name, uint rgb)[] Presets =
    {
        ("Amber · 琥珀", 0x00FFA500),
        ("Red · 紅", 0x00FF3B30),
        ("Green · 綠", 0x0034C759),
        ("Cyan · 青", 0x0032ADE6),
        ("Magenta · 洋紅", 0x00FF2D95),
        ("Yellow · 黃", 0x00FFE000),
        ("White · 白", 0x00FFFFFF),
    };

    public ScreenRulerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { LoadSettings(); BuildSwatches(); Render(); UpdateCurrentSwatch(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void LoadSettings()
    {
        var hex = SettingsStore.Get("screenruler.color", "#FFA500");
        if (TryParseHex(hex, out var rgb)) _rgb = rgb;
        if (int.TryParse(SettingsStore.Get("screenruler.thickness", "2"), out var t))
            ThicknessSlider.Value = Math.Clamp(t, 1, 10);
        ThicknessSlider.ValueChanged += (_, _) =>
            SettingsStore.Set("screenruler.thickness", ((int)ThicknessSlider.Value).ToString());
    }

    private void Render()
    {
        Header.Title = "Screen Ruler · 螢幕間尺";
        HeaderBlurb.Text = P(
            "Measure anything on screen in pixels — a native clone of PowerToys' Measure Tool. Pick a mode below, then move/drag on screen. Live pixel counts and coordinates show on the overlay; left-click copies the measurement to the clipboard. Esc or right-click closes the overlay.",
            "喺螢幕度度任何嘢嘅像素 — PowerToys Measure Tool 嘅原生複製版。揀下面其中一個模式，然後喺螢幕度移動／拖曳。覆蓋層會即時顯示像素數同座標；撳左鍵會將量度結果複製到剪貼簿。Esc 或右鍵關閉覆蓋層。");

        ModesLabel.Text = P("Measurement modes", "量度模式");

        DistanceTitle.Text = P("Distance", "距離");
        DistanceSub.Text = P("Click-drag a line; shows px length + angle", "拖出一條線；顯示像素長度同角度");
        HorizontalTitle.Text = P("Horizontal", "水平");
        HorizontalSub.Text = P("Full-width ruler line; counts px across", "整闊嘅尺線；數橫向像素");
        VerticalTitle.Text = P("Vertical", "垂直");
        VerticalSub.Text = P("Full-height ruler line; counts px down", "整高嘅尺線；數縱向像素");
        CrossTitle.Text = P("Cross", "十字");
        CrossSub.Text = P("Live crosshair with px coordinates", "即時十字線 + 像素座標");
        BoundsTitle.Text = P("Bounds", "邊界");
        BoundsSub.Text = P("Auto-detect the region under the cursor", "自動偵測游標下嘅區域");

        AppearanceLabel.Text = P("Appearance", "外觀");
        ColorLabel.Text = P("Line colour", "線條顏色");
        ApplyHexBtn.Content = P("Apply", "套用");
        ThicknessLabel.Text = P("Line thickness (px)", "線條粗幼 (px)");
        UnitsNote.Text = P("Units: pixels (px). The app is per-monitor DPI-aware, so counts are physical screen pixels.",
            "單位：像素 (px)。本程式支援每螢幕 DPI，故此數值為實際螢幕像素。");

        HowToLabel.Text = P("How to use", "用法");
        HowToBody.Text = P(
            "• Distance: press and drag from one point to another; release to copy. Hold Shift on release to keep measuring.\n" +
            "• Horizontal / Vertical: move the mouse to position the ruler line; left-click copies the count.\n" +
            "• Cross: the crosshair follows the cursor and shows its exact (x, y) pixel coordinate.\n" +
            "• Bounds: hover a UI element — the overlay outlines the detected rectangle. Scroll the mouse wheel to adjust the edge tolerance. Left-click once to lock + copy, again to close.\n" +
            "• Esc or right-click closes the overlay at any time.",
            "• 距離：由一點㩒住拖去另一點；放手即複製。放手時㩒住 Shift 可繼續度。\n" +
            "• 水平／垂直：移動滑鼠去定位尺線；撳左鍵複製數值。\n" +
            "• 十字：十字線跟住游標，顯示準確嘅 (x, y) 像素座標。\n" +
            "• 邊界：移到介面元素上 — 覆蓋層會框出偵測到嘅矩形。轉滑鼠滾輪調整邊緣容差。撳左鍵一次鎖定並複製，再撳一次關閉。\n" +
            "• 隨時可按 Esc 或右鍵關閉覆蓋層。");
    }

    private void BuildSwatches()
    {
        Swatches.Items.Clear();
        foreach (var (name, rgb) in Presets)
        {
            var b = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Background = BrushFromRgb(rgb),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
            };
            ToolTipService.SetToolTip(b, name);
            uint captured = rgb;
            b.Tapped += (_, _) => { _rgb = captured; HexInput.Text = HexOf(captured); SaveColor(); UpdateCurrentSwatch(); };
            Swatches.Items.Add(b);
        }
        HexInput.Text = HexOf(_rgb);
    }

    private void ApplyHex_Click(object sender, RoutedEventArgs e)
    {
        if (TryParseHex(HexInput.Text ?? "", out var rgb))
        {
            _rgb = rgb;
            SaveColor();
            UpdateCurrentSwatch();
        }
        else
        {
            ShowStatus(P("Invalid colour", "顏色無效"), P("Enter a colour like #FFA500.", "請輸入類似 #FFA500 嘅顏色。"), InfoBarSeverity.Warning);
        }
    }

    private void SaveColor() => SettingsStore.Set("screenruler.color", HexOf(_rgb));

    private void UpdateCurrentSwatch() => CurrentSwatch.Background = BrushFromRgb(_rgb);

    // ---- launching the overlay ----
    private void Launch(ScreenRulerService.RulerMode mode)
    {
        if (ScreenRulerService.IsActive) return;
        int thickness = (int)ThicknessSlider.Value;
        uint bgr = ToBgr(_rgb);
        try
        {
            // Synchronous: runs its own modal message loop (pumps this window too), like RegionSelector.
            ScreenRulerService.Start(mode, bgr, thickness);
            ShowStatus(P("Done", "完成"),
                P("The overlay closed. Any measurement was copied to the clipboard.", "覆蓋層已關閉。量度結果已複製到剪貼簿。"),
                InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus(P("Could not open the overlay", "無法開啟覆蓋層"), ex.Message, InfoBarSeverity.Error);
        }
    }

    private void Distance_Click(object sender, RoutedEventArgs e) => Launch(ScreenRulerService.RulerMode.Distance);
    private void Horizontal_Click(object sender, RoutedEventArgs e) => Launch(ScreenRulerService.RulerMode.Horizontal);
    private void Vertical_Click(object sender, RoutedEventArgs e) => Launch(ScreenRulerService.RulerMode.Vertical);
    private void Cross_Click(object sender, RoutedEventArgs e) => Launch(ScreenRulerService.RulerMode.Cross);
    private void Bounds_Click(object sender, RoutedEventArgs e) => Launch(ScreenRulerService.RulerMode.Bounds);

    private void ShowStatus(string title, string msg, InfoBarSeverity sev)
    {
        StatusBar.Title = title;
        StatusBar.Message = msg;
        StatusBar.Severity = sev;
        StatusBar.IsOpen = true;
    }

    // ---- colour helpers ----
    private static SolidColorBrush BrushFromRgb(uint rgb) =>
        new(Color.FromArgb(255, (byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF)));

    private static string HexOf(uint rgb) => $"#{(rgb & 0xFFFFFF):X6}";

    /// <summary>Convert 0x00RRGGBB → GDI COLORREF 0x00BBGGRR. Avoid the overlay's transparency
    /// colour-key (0x010101) by nudging it by one if a user picks it exactly.</summary>
    private static uint ToBgr(uint rgb)
    {
        byte r = (byte)((rgb >> 16) & 0xFF), g = (byte)((rgb >> 8) & 0xFF), b = (byte)(rgb & 0xFF);
        uint bgr = (uint)(r | (g << 8) | (b << 16));
        if (bgr == 0x00010101) bgr = 0x00020202;
        return bgr;
    }

    private static bool TryParseHex(string s, out uint rgb)
    {
        rgb = 0;
        s = s.Trim().TrimStart('#');
        if (s.Length == 6 && uint.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
        {
            rgb = v & 0xFFFFFF;
            return true;
        }
        return false;
    }
}
