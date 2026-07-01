using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內滑鼠與指標設定 · In-app mouse &amp; pointer settings — native toggles/sliders that apply
/// live via SystemParametersInfo (no ms-settings redirect). Bilingual.
/// </summary>
public sealed partial class MouseModule : Page
{
    private bool _suppress;

    public MouseModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => Build();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Build();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Build()
    {
        Header.Title = "Mouse & Pointer · 滑鼠與指標";
        HeaderBlurb.Text = P("Native mouse settings that apply instantly and persist — no Settings app needed.",
            "原生滑鼠設定，即時生效又會記住 — 唔使開設定 app。");
        Root.Children.Clear();

        Root.Children.Add(Toggle(
            P("Swap primary & secondary buttons", "交換左右鍵"),
            P("Make the right button the primary click (for left-handers).", "將右鍵變做主要點擊（左撇子啱用）。"),
            MouseSettings.GetSwap(), MouseSettings.SetSwap));

        Root.Children.Add(Slider(
            P("Pointer speed", "指標速度"),
            P("How fast the cursor moves (1–20, default 10).", "游標移動有幾快（1–20，預設 10）。"),
            1, 20, MouseSettings.GetSpeed(), MouseSettings.SetSpeed, v => $"{v} / 20"));

        Root.Children.Add(Toggle(
            P("Enhance pointer precision (acceleration)", "增強指標精確度（加速）"),
            P("Off gives 1:1 movement — gamers usually want this OFF.", "熄咗就 1:1 移動 — 打機通常想熄。"),
            MouseSettings.GetAccel(), MouseSettings.SetAccel));

        Root.Children.Add(Slider(
            P("Double-click speed", "雙擊速度"),
            P("Max time between clicks to count as a double-click.", "兩下點擊之間最長幾耐先算雙擊。"),
            100, 900, MouseSettings.GetDoubleClick(), MouseSettings.SetDoubleClick, v => $"{v} ms"));

        Root.Children.Add(Slider(
            P("Wheel scroll lines", "滾輪捲動行數"),
            P("Lines scrolled per wheel notch.", "滾輪每格捲幾多行。"),
            1, 15, MouseSettings.GetWheelLines(), MouseSettings.SetWheelLines, v => $"{v}"));

        Root.Children.Add(Toggle(
            P("Hide pointer while typing", "打字時隱藏指標"),
            P("The cursor vanishes while you type.", "打字時游標會消失。"),
            MouseSettings.GetVanish(), MouseSettings.SetVanish));

        Root.Children.Add(Toggle(
            P("Snap to the default button in dialogs", "對話框自動跳去預設按鈕"),
            P("Auto-move the pointer onto the default button when a dialog opens.", "開對話框時自動將指標移去預設按鈕。"),
            MouseSettings.GetSnap(), MouseSettings.SetSnap));
    }

    private Border Toggle(string title, string desc, bool current, Action<bool> set)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(Heading(title, desc));

        var sw = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄", VerticalAlignment = VerticalAlignment.Center };
        _suppress = true;
        sw.IsOn = current;
        _suppress = false;
        sw.Toggled += (_, _) => { if (!_suppress) try { set(sw.IsOn); } catch { } };
        Grid.SetColumn(sw, 1);
        grid.Children.Add(sw);
        return Card(grid);
    }

    private Border Slider(string title, string desc, int min, int max, int current, Action<int> set, Func<int, string> fmt)
    {
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(Heading(title, desc));

        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

        var slider = new Slider { Minimum = min, Maximum = max, StepFrequency = 1 };
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
        return Card(panel);
    }

    private static StackPanel Heading(string title, string desc)
    {
        var p = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        p.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap });
        p.Children.Add(new TextBlock { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        return p;
    }

    private static Border Card(UIElement content) => new()
    {
        Padding = new Thickness(16, 12, 16, 12),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = content,
    };
}
