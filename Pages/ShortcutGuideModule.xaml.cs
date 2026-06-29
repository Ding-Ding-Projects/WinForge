using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 快捷鍵指南控制頁 · Shortcut Guide control page.
///
/// 開關「揿住 Win 彈出指南」、調揿住時間、覆蓋層不透明度同主題，仲有一個可搜尋嘅完整 Windows
/// 快捷鍵參考表（即使無觸發覆蓋層都用得着）。全部雙語、喺 app 內。
///
/// Enable toggle for hold-Win-to-show, activation hold duration, overlay opacity & theme, plus a
/// searchable full reference table of Windows shortcuts (useful even without triggering the overlay).
/// </summary>
public sealed partial class ShortcutGuideModule : Page
{
    public ShortcutGuideModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        ShortcutGuideService.StateChanged += OnState;
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            ShortcutGuideService.StateChanged -= OnState;
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        FillThemeBox();
        EnableSwitch.IsOn = ShortcutGuideService.Enabled;
        HoldSlider.Value = ShortcutGuideService.HoldMs;
        OpacitySlider.Value = ShortcutGuideService.Opacity;
        Render();
        BuildReference(null);
    }

    private void OnLang(object? s, EventArgs e)
    {
        FillThemeBox();
        Render();
        BuildReference(SearchBox.Text);
    }

    private void OnState()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            EnableSwitch.IsOn = ShortcutGuideService.Enabled;
            UpdateEnableStatus();
            PreviewBtn.Content = ShortcutGuideService.OverlayShowing
                ? P("Hide preview", "收起預覽")
                : P("Preview overlay", "預覽覆蓋層");
        });
    }

    private void Render()
    {
        Header.Title = "Shortcut Guide · 快捷鍵指南";
        HeaderBlurb.Text = P(
            "Hold the Windows key for a moment to pop up a topmost overlay of the common Win-key shortcuts, grouped by category. Release Win or press Esc to dismiss. Below is a searchable reference of Windows shortcuts you can browse any time.",
            "揿住 Windows 鍵一陣，就會彈出一個置頂覆蓋層，按分類列出常用嘅 Win 鍵快捷鍵。放開 Win 或者揿 Esc 收起。下面係一個可搜尋嘅 Windows 快捷鍵參考表，幾時都查得到。");

        EnableTitle.Text = P("Hold Win to show the guide", "揿住 Win 顯示指南");
        HoldLabel.Text = P("Activation hold duration", "觸發揿住時間");
        OpacityLabel.Text = P("Overlay opacity", "覆蓋層不透明度");
        ThemeLabel.Text = P("Overlay theme", "覆蓋層主題");
        ReferenceHeader.Text = P("Shortcut reference · 快捷鍵參考", "快捷鍵參考 · Shortcut reference");
        SearchBox.PlaceholderText = P("Search shortcuts…", "搜尋快捷鍵…");

        PreviewBtn.Content = ShortcutGuideService.OverlayShowing
            ? P("Hide preview", "收起預覽")
            : P("Preview overlay", "預覽覆蓋層");

        UpdateEnableStatus();
        UpdateHoldText();
        UpdateOpacityText();
        SelectTheme(ShortcutGuideService.Theme);
    }

    private void UpdateEnableStatus()
    {
        EnableStatus.Text = ShortcutGuideService.Enabled
            ? P("On — a low-level keyboard hook watches for a Windows-key hold anywhere in Windows.",
                "已開 — 低階鍵盤掛鈎喺 Windows 任何地方監察 Windows 鍵嘅揿住。")
            : P("Off — turn on to install the hold-to-show keyboard hook.",
                "已關 — 開咗會裝「揿住顯示」鍵盤掛鈎。");
    }

    private void UpdateHoldText() => HoldValue.Text = $"{(int)HoldSlider.Value} ms";

    private void UpdateOpacityText() => OpacityValue.Text = $"{(int)OpacitySlider.Value} %";

    // ===================== settings handlers =====================

    private void Enable_Toggled(object sender, RoutedEventArgs e)
    {
        ShortcutGuideService.SetEnabled(EnableSwitch.IsOn);
        UpdateEnableStatus();
    }

    private void Hold_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ShortcutGuideService.HoldMs = (int)HoldSlider.Value;
        UpdateHoldText();
    }

    private void Opacity_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ShortcutGuideService.Opacity = (int)OpacitySlider.Value;
        UpdateOpacityText();
    }

    private void FillThemeBox()
    {
        var sel = ShortcutGuideService.Theme;
        ThemeBox.Items.Clear();
        ThemeBox.Items.Add(new ComboBoxItem { Content = P("Dark", "深色"), Tag = "Dark" });
        ThemeBox.Items.Add(new ComboBoxItem { Content = P("Light", "淺色"), Tag = "Light" });
        ThemeBox.Items.Add(new ComboBoxItem { Content = P("Match app theme", "跟隨應用程式主題"), Tag = "Default" });
        SelectTheme(sel);
    }

    private void SelectTheme(string tag)
    {
        for (int i = 0; i < ThemeBox.Items.Count; i++)
            if (ThemeBox.Items[i] is ComboBoxItem ci && (ci.Tag as string) == tag)
            {
                ThemeBox.SelectedIndex = i;
                return;
            }
        ThemeBox.SelectedIndex = 0;
    }

    private void Theme_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        if ((ThemeBox.SelectedItem as ComboBoxItem)?.Tag is string tag)
            ShortcutGuideService.Theme = tag;
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        ShortcutGuideService.PreviewOverlay();
        if (!ShortcutGuideService.Enabled)
            Info(P("Preview", "預覽"), P("Showing the overlay. It also pops up when you hold Win once enabled.",
                "顯示緊覆蓋層。開咗功能後揿住 Win 都會彈出。"));
    }

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        => BuildReference(sender.Text);

    // ===================== searchable reference table =====================

    private void BuildReference(string? query)
    {
        ReferenceHost.Items.Clear();
        var q = (query ?? "").Trim().ToLowerInvariant();

        int shown = 0;
        foreach (var g in ShortcutGuideService.Groups)
        {
            var items = string.IsNullOrEmpty(q)
                ? g.Items
                : g.Items.Where(i => i.Haystack.Contains(q)).ToList();
            if (items.Count == 0) continue;

            ReferenceHost.Items.Add(BuildGroupCard(g.Title, items));
            shown += items.Count;
        }

        bool empty = shown == 0;
        NoResults.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (empty)
            NoResults.Text = P($"No shortcuts match “{query}”.", $"無快捷鍵符合「{query}」。");
    }

    private FrameworkElement BuildGroupCard(string title, IList<ShortcutItem> items)
    {
        var outer = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 16) };
        outer.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 16,
            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
            Margin = new Thickness(2, 0, 0, 2),
        });

        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(4, 4, 4, 4),
        };
        var rows = new StackPanel();
        for (int i = 0; i < items.Count; i++)
        {
            rows.Children.Add(BuildRow(items[i]));
            if (i < items.Count - 1)
                rows.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                    Margin = new Thickness(12, 1, 12, 1),
                    Opacity = 0.6,
                });
        }
        card.Child = rows;
        outer.Children.Add(card);
        return outer;
    }

    private FrameworkElement BuildRow(ShortcutItem item)
    {
        var grid = new Grid { ColumnSpacing = 14, Padding = new Thickness(12, 8, 12, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        var capBg = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
        var capBorder = (Brush)Application.Current.Resources["ControlElevationBorderBrush"];
        var capFg = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        var plusFg = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];
        for (int i = 0; i < item.Keys.Length; i++)
        {
            if (i > 0)
                keys.Children.Add(new TextBlock { Text = "+", VerticalAlignment = VerticalAlignment.Center, Foreground = plusFg, FontSize = 12 });
            keys.Children.Add(new Border
            {
                Background = capBg,
                BorderBrush = capBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 3, 8, 4),
                MinWidth = 26,
                Child = new TextBlock
                {
                    Text = item.Keys[i],
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = capFg,
                },
            });
        }
        Grid.SetColumn(keys, 0);
        grid.Children.Add(keys);

        var desc = new TextBlock
        {
            Text = item.Desc,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        Grid.SetColumn(desc, 1);
        grid.Children.Add(desc);

        return grid;
    }

    // ===================== InfoBar helper =====================

    private void Info(string title, string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Informational;
        ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
