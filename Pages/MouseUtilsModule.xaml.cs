using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

using Svc = WinForge.Services.MouseUtilsService;

/// <summary>
/// 滑鼠工具（PowerToys Mouse Utilities 原生複製）· Native clone of PowerToys Mouse Utilities.
/// Enable toggles + live settings (colour / size / opacity / activation) for four overlay utilities:
/// Find My Mouse, Mouse Highlighter, Mouse Crosshairs and Mouse Jump. Every change is applied
/// instantly through <see cref="MouseUtilsService"/> (global hooks + per-pixel-alpha overlays) and persisted.
/// Bilingual throughout.
/// </summary>
public sealed partial class MouseUtilsModule : Page
{
    private bool _suppress;

    public MouseUtilsModule()
    {
        InitializeComponent();
        Svc.LoadSettings();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => Build();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Build();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Apply()
    {
        Svc.SaveSettings();
        Svc.Sync();
    }

    private void Build()
    {
        Header.Title = "Mouse Utilities · 滑鼠工具";
        HeaderBlurb.Text = P(
            "Native PowerToys-style mouse helpers — find the cursor, highlight clicks, draw crosshairs, and jump the pointer across displays. Each runs on a global hook with a transparent click-through overlay.",
            "原生 PowerToys 式滑鼠小工具 — 搵游標、標示點擊、畫十字線、喺多個螢幕之間跳轉指標。每個都用全域掛鈎加透明穿透覆蓋層運作。");
        Root.Children.Clear();

        Root.Children.Add(BuildFindMyMouse());
        Root.Children.Add(BuildHighlighter());
        Root.Children.Add(BuildCrosshairs());
        Root.Children.Add(BuildMouseJump());
    }

    // ============================================================ Find My Mouse
    private UIElement BuildFindMyMouse()
    {
        var body = new StackPanel { Spacing = 10 };

        body.Children.Add(EnableRow(
            Svc.FindMyMouse.Enabled,
            on => { Svc.FindMyMouse.Enabled = on; Apply(); RefreshSection(body, BuildFindMyMouseSettings, on); }));

        var settings = new StackPanel { Spacing = 10, Visibility = Svc.FindMyMouse.Enabled ? Visibility.Visible : Visibility.Collapsed };
        BuildFindMyMouseSettings(settings);
        body.Children.Add(settings);

        return Section(
            "",
            P("Find My Mouse", "搵我滑鼠"),
            P("Double-tap Left Ctrl (or shake the mouse) to dim the screen and spotlight the cursor.",
              "連按兩下左 Ctrl（或者擰下滑鼠）就會暗化畫面並用聚光燈圈住游標。"),
            body);
    }

    private void BuildFindMyMouseSettings(StackPanel s)
    {
        s.Children.Clear();
        s.Children.Add(Combo(
            P("Activation method", "啟動方式"),
            P("How to summon the spotlight.", "點樣召喚聚光燈。"),
            new[] { P("Double-tap Left Ctrl", "連按兩下左 Ctrl"), P("Shake the mouse", "擰動滑鼠") },
            Svc.FindMyMouse.ActivationMethod,
            i => { Svc.FindMyMouse.ActivationMethod = i; Apply(); }));

        s.Children.Add(Slider(
            P("Spotlight radius", "聚光燈半徑"),
            P("Size of the bright circle around the cursor.", "游標周圍光圈嘅大細。"),
            20, 400, Svc.FindMyMouse.SpotlightRadius, v => { Svc.FindMyMouse.SpotlightRadius = v; Apply(); }, v => $"{v} px"));

        s.Children.Add(Slider(
            P("Backdrop dim", "背景變暗"),
            P("How dark the rest of the screen goes (0–255).", "畫面其餘部分有幾暗（0–255）。"),
            0, 255, Svc.FindMyMouse.BackdropOpacity, v => { Svc.FindMyMouse.BackdropOpacity = (byte)v; Apply(); }, v => $"{v}"));

        s.Children.Add(Slider(
            P("Fade duration", "淡入淡出時間"),
            P("How long the spotlight takes to fade in/out.", "聚光燈淡入淡出需要幾耐。"),
            50, 1500, Svc.FindMyMouse.FadeMs, v => { Svc.FindMyMouse.FadeMs = v; Apply(); }, v => $"{v} ms"));

        s.Children.Add(ColorRow(
            P("Spotlight colour", "聚光燈顏色"),
            P("Colour of the highlight ring and fill.", "光圈同填色嘅顏色。"),
            Svc.FindMyMouse.SpotlightColor, c => { Svc.FindMyMouse.SpotlightColor = c; Apply(); }));

        s.Children.Add(ColorRow(
            P("Backdrop colour", "背景顏色"),
            P("Colour used to dim the rest of the screen.", "用嚟暗化畫面其餘部分嘅顏色。"),
            Svc.FindMyMouse.BackgroundColor, c => { Svc.FindMyMouse.BackgroundColor = c; Apply(); }));
    }

    // ============================================================ Mouse Highlighter
    private UIElement BuildHighlighter()
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(EnableRow(
            Svc.Highlighter.Enabled,
            on => { Svc.Highlighter.Enabled = on; Apply(); RefreshSection(body, BuildHighlighterSettings, on); }));

        var settings = new StackPanel { Spacing = 10, Visibility = Svc.Highlighter.Enabled ? Visibility.Visible : Visibility.Collapsed };
        BuildHighlighterSettings(settings);
        body.Children.Add(settings);

        return Section(
            "",
            P("Mouse Highlighter", "滑鼠標示"),
            P("Press Win+Shift+H to toggle. While on, every left/right click leaves a fading coloured circle.",
              "按 Win+Shift+H 開關。開咗之後，每次左／右擊都會留低一個漸隱嘅彩色圓圈。"),
            body);
    }

    private void BuildHighlighterSettings(StackPanel s)
    {
        s.Children.Clear();
        s.Children.Add(Hotkey("Win + Shift + H"));

        s.Children.Add(Slider(
            P("Circle radius", "圓圈半徑"),
            P("Size of the click highlight.", "點擊標示嘅大細。"),
            5, 120, Svc.Highlighter.Radius, v => { Svc.Highlighter.Radius = v; Apply(); }, v => $"{v} px"));

        s.Children.Add(Slider(
            P("Opacity", "不透明度"),
            P("How solid the highlight is (0–255).", "標示有幾實淨（0–255）。"),
            0, 255, Svc.Highlighter.Opacity, v => { Svc.Highlighter.Opacity = (byte)v; Apply(); }, v => $"{v}"));

        s.Children.Add(Slider(
            P("Fade duration", "淡出時間"),
            P("How long each circle lingers before fading.", "每個圓圈淡走前停留幾耐。"),
            100, 2000, Svc.Highlighter.FadeMs, v => { Svc.Highlighter.FadeMs = v; Apply(); }, v => $"{v} ms"));

        s.Children.Add(ColorRow(
            P("Left-click colour", "左擊顏色"),
            P("Highlight colour for left clicks.", "左擊嘅標示顏色。"),
            Svc.Highlighter.LeftColor, c => { Svc.Highlighter.LeftColor = c; Apply(); }));

        s.Children.Add(ColorRow(
            P("Right-click colour", "右擊顏色"),
            P("Highlight colour for right clicks.", "右擊嘅標示顏色。"),
            Svc.Highlighter.RightColor, c => { Svc.Highlighter.RightColor = c; Apply(); }));
    }

    // ============================================================ Mouse Crosshairs
    private UIElement BuildCrosshairs()
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(EnableRow(
            Svc.Crosshairs.Enabled,
            on => { Svc.Crosshairs.Enabled = on; Apply(); RefreshSection(body, BuildCrosshairsSettings, on); }));

        var settings = new StackPanel { Spacing = 10, Visibility = Svc.Crosshairs.Enabled ? Visibility.Visible : Visibility.Collapsed };
        BuildCrosshairsSettings(settings);
        body.Children.Add(settings);

        return Section(
            "",
            P("Mouse Crosshairs", "滑鼠十字線"),
            P("Press Win+Shift+X to toggle full-length horizontal and vertical lines that follow the cursor.",
              "按 Win+Shift+X 開關跟住游標嘅全長橫直十字線。"),
            body);
    }

    private void BuildCrosshairsSettings(StackPanel s)
    {
        s.Children.Clear();
        s.Children.Add(Hotkey("Win + Shift + X"));

        s.Children.Add(Slider(
            P("Line thickness", "線條粗幼"),
            P("Width of the crosshair lines.", "十字線嘅闊度。"),
            1, 30, Svc.Crosshairs.Thickness, v => { Svc.Crosshairs.Thickness = v; Apply(); }, v => $"{v} px"));

        s.Children.Add(Slider(
            P("Centre gap", "中心空隙"),
            P("Radius of the clear gap around the cursor (0 = none).", "游標周圍留空嘅半徑（0 = 冇）。"),
            0, 100, Svc.Crosshairs.Radius, v => { Svc.Crosshairs.Radius = v; Apply(); }, v => $"{v} px"));

        s.Children.Add(Slider(
            P("Opacity", "不透明度"),
            P("How solid the lines are (0–255).", "線條有幾實淨（0–255）。"),
            0, 255, Svc.Crosshairs.Opacity, v => { Svc.Crosshairs.Opacity = (byte)v; Apply(); }, v => $"{v}"));

        s.Children.Add(ColorRow(
            P("Line colour", "線條顏色"),
            P("Colour of the crosshair lines.", "十字線嘅顏色。"),
            Svc.Crosshairs.Color, c => { Svc.Crosshairs.Color = c; Apply(); }));
    }

    // ============================================================ Mouse Jump
    private UIElement BuildMouseJump()
    {
        var body = new StackPanel { Spacing = 10 };
        body.Children.Add(EnableRow(
            Svc.MouseJump.Enabled,
            on => { Svc.MouseJump.Enabled = on; Apply(); RefreshSection(body, BuildMouseJumpSettings, on); }));

        var settings = new StackPanel { Spacing = 10, Visibility = Svc.MouseJump.Enabled ? Visibility.Visible : Visibility.Collapsed };
        BuildMouseJumpSettings(settings);
        body.Children.Add(settings);

        return Section(
            "",
            P("Mouse Jump", "滑鼠跳轉"),
            P("Press Win+Shift+D to show a shrunken snapshot of every display; click anywhere on it to teleport the cursor there.",
              "按 Win+Shift+D 顯示所有螢幕嘅縮細快照；喺上面任何位置一撳，游標就會即刻跳過去。"),
            body);
    }

    private void BuildMouseJumpSettings(StackPanel s)
    {
        s.Children.Clear();
        s.Children.Add(Hotkey("Win + Shift + D"));
        s.Children.Add(new TextBlock
        {
            Text = P("Click on the preview to teleport · right-click or Esc to cancel.",
                     "撳預覽圖跳轉 · 右擊或 Esc 取消。"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
    }

    // ============================================================ helpers
    private void RefreshSection(StackPanel body, Action<StackPanel> rebuild, bool on)
    {
        // the settings panel is always the 2nd child of the section body
        if (body.Children.Count >= 2 && body.Children[1] is StackPanel settings)
        {
            settings.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
            if (on) rebuild(settings);
        }
    }

    private Border EnableRow(bool current, Action<bool> set)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Heading(P("Enable", "啟用"), P("Turn this utility on or off.", "開啟或關閉呢個工具。")));

        var sw = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄", VerticalAlignment = VerticalAlignment.Center };
        _suppress = true; sw.IsOn = current; _suppress = false;
        sw.Toggled += (_, _) => { if (!_suppress) try { set(sw.IsOn); } catch { } };
        Grid.SetColumn(sw, 1);
        grid.Children.Add(sw);
        return InnerCard(grid);
    }

    private Border Slider(string title, string desc, int min, int max, int current, Action<int> set, Func<int, string> fmt)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(Heading(title, desc));

        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });

        var slider = new Microsoft.UI.Xaml.Controls.Slider { Minimum = min, Maximum = max, StepFrequency = 1 };
        var val = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalTextAlignment = TextAlignment.Right };
        _suppress = true;
        slider.Value = Math.Clamp(current, min, max);
        val.Text = fmt((int)slider.Value);
        _suppress = false;
        slider.ValueChanged += (_, e) =>
        {
            val.Text = fmt((int)e.NewValue);
            if (!_suppress) try { set((int)e.NewValue); } catch { }
        };
        Grid.SetColumn(slider, 0);
        Grid.SetColumn(val, 1);
        row.Children.Add(slider);
        row.Children.Add(val);
        panel.Children.Add(row);
        return InnerCard(panel);
    }

    private Border Combo(string title, string desc, string[] options, int current, Action<int> set)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(Heading(title, desc));
        var cb = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var o in options) cb.Items.Add(new ComboBoxItem { Content = o });
        _suppress = true; cb.SelectedIndex = Math.Clamp(current, 0, options.Length - 1); _suppress = false;
        cb.SelectionChanged += (_, _) => { if (!_suppress && cb.SelectedIndex >= 0) try { set(cb.SelectedIndex); } catch { } };
        panel.Children.Add(cb);
        return InnerCard(panel);
    }

    private Border ColorRow(string title, string desc, uint rgb, Action<uint> set)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Heading(title, desc));

        var swatch = new Border
        {
            Width = 44, Height = 28, CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = new SolidColorBrush(ToColor(rgb)),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var picker = new ColorPicker
        {
            IsAlphaEnabled = false,
            IsHexInputVisible = true,
            ColorSpectrumShape = ColorSpectrumShape.Box,
            Color = ToColor(rgb),
            Width = 280,
        };
        picker.ColorChanged += (_, e) =>
        {
            uint v = ((uint)e.NewColor.R << 16) | ((uint)e.NewColor.G << 8) | e.NewColor.B;
            swatch.Background = new SolidColorBrush(e.NewColor);
            try { set(v); } catch { }
        };

        // preset swatches for quick choice
        var presets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var pc in new uint[] { 0xFFFFFF, 0x000000, 0xFF0000, 0x00FF00, 0x0080FF, 0xFFFF00, 0xFF00FF, 0xFF8000 })
        {
            var b = new Button
            {
                Width = 26, Height = 26, Padding = new Thickness(0), CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(ToColor(pc)),
                BorderThickness = new Thickness(1),
            };
            uint captured = pc;
            b.Click += (_, _) => { picker.Color = ToColor(captured); };
            presets.Children.Add(b);
        }

        var flyoutContent = new StackPanel { Spacing = 4 };
        flyoutContent.Children.Add(picker);
        flyoutContent.Children.Add(presets);

        var btn = new Button { Content = swatch, Padding = new Thickness(4), VerticalAlignment = VerticalAlignment.Center };
        btn.Flyout = new Flyout { Content = flyoutContent };
        Grid.SetColumn(btn, 1);
        grid.Children.Add(btn);
        return InnerCard(grid);
    }

    private Border Hotkey(string keys)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        panel.Children.Add(new FontIcon { Glyph = "", FontSize = 14, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(new TextBlock
        {
            Text = P("Toggle hotkey:", "切換熱鍵：") + " " + keys,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        return new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            CornerRadius = new CornerRadius(6),
            Child = panel,
        };
    }

    private static Color ToColor(uint rgb) =>
        Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    private static StackPanel Heading(string title, string desc)
    {
        var p = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        p.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap });
        p.Children.Add(new TextBlock { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        return p;
    }

    private Border InnerCard(UIElement content) => new()
    {
        Padding = new Thickness(14, 10, 14, 10),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
    };

    private Border Section(string glyph, string title, string desc, UIElement body)
    {
        var panel = new StackPanel { Spacing = 12 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        head.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) });
        head.Children.Add(Heading(title, desc));
        panel.Children.Add(head);
        panel.Children.Add(body);

        return new Border
        {
            Padding = new Thickness(18, 16, 18, 16),
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = panel,
        };
    }
}
