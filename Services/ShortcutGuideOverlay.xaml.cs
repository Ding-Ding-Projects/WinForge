using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;

namespace WinForge.Services;

/// <summary>
/// 快捷鍵指南覆蓋層視窗 · The Shortcut Guide overlay window.
///
/// 一個無邊框、置頂、半透明嘅視窗，覆蓋成個工作區，列出常用 Win 鍵快捷鍵（分類 + 鍵帽）。
/// A borderless, topmost, semi-transparent window that fills the work area and lists the common Win-key
/// shortcuts grouped by category, each key drawn as a key-cap chip. Built once and reused; Show/Hide just
/// toggle visibility so popping the guide stays instant. Esc, a click on the dim backdrop, or losing
/// activation all dismiss it.
/// </summary>
public sealed partial class ShortcutGuideOverlay : Window
{
    private static ShortcutGuideOverlay? _instance;
    private bool _closeOnDeactivate;
    private bool _shown;

    private ShortcutGuideOverlay()
    {
        InitializeComponent();

        // Borderless, no title bar, topmost — a true overlay.
        if (AppWindow.Presenter is OverlappedPresenter p)
        {
            p.IsAlwaysOnTop = true;
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
            p.SetBorderAndTitleBar(false, false);
        }
        AppWindow.IsShownInSwitchers = false;

        BuildColumns();

        // Dismiss on backdrop click.
        RootBackdrop.PointerPressed += (_, _) => RequestClose();

        // Esc to dismiss; also dismiss when the window loses activation (preview mode).
        if (RootBackdrop is UIElement root)
        {
            root.KeyDown += OnKeyDown;
            root.IsTabStop = true;
        }
        Activated += OnActivated;
    }

    // ===================== public API (called on the UI thread) =====================

    /// <summary>顯示覆蓋層 · Show the overlay (creates it once, then reuses).</summary>
    public static void Show(bool autoCloseOnDeactivate = false)
    {
        _instance ??= new ShortcutGuideOverlay();
        _instance._closeOnDeactivate = autoCloseOnDeactivate;
        _instance.BuildColumns();      // refresh text in case the language changed
        _instance.ApplyAppearance();
        _instance.PositionToWorkArea();
        _instance.AppWindow.Show();
        _instance._shown = true;
        _instance.Activate();
        try { _instance.RootBackdrop.Focus(FocusState.Programmatic); } catch { }
    }

    /// <summary>收起覆蓋層 · Hide the overlay (kept alive for the next pop).</summary>
    public static void Hide()
    {
        if (_instance is null) return;
        _instance._shown = false;
        try { _instance.AppWindow.Hide(); } catch { }
    }

    private void RequestClose()
    {
        Hide();
        ShortcutGuideService.NotifyOverlayClosed();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            e.Handled = true;
            RequestClose();
        }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        // In preview mode, clicking elsewhere should dismiss. During real Win-hold the service hides it
        // on key-up, so we only self-close on deactivation when asked (preview) and once actually shown.
        if (_closeOnDeactivate && _shown && e.WindowActivationState == WindowActivationState.Deactivated)
            RequestClose();
    }

    // ===================== appearance & placement =====================

    private void ApplyAppearance()
    {
        // Theme
        var theme = ShortcutGuideService.Theme switch
        {
            "Light" => ElementTheme.Light,
            "Dark" => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
        if (RootBackdrop is FrameworkElement fe) fe.RequestedTheme = theme;

        // Opacity drives the dim backdrop alpha (the card stays readable).
        int op = ShortcutGuideService.Opacity;           // 30..100
        byte cardA = (byte)Math.Clamp((int)(op / 100.0 * 255), 90, 255);
        byte dimA = (byte)Math.Clamp((int)(op / 100.0 * 220), 60, 235);
        bool dark = theme != ElementTheme.Light;
        var dimColor = dark ? Windows.UI.Color.FromArgb(dimA, 0x10, 0x10, 0x14)
                            : Windows.UI.Color.FromArgb(dimA, 0xEC, 0xEC, 0xF0);
        RootBackdrop.Background = new SolidColorBrush(dimColor);
        Card.Opacity = cardA / 255.0;
    }

    private void PositionToWorkArea()
    {
        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var wa = area.WorkArea;
            AppWindow.MoveAndResize(new RectInt32(wa.X, wa.Y, wa.Width, wa.Height));
        }
        catch { /* fall back to whatever size it has */ }
    }

    // ===================== build the grouped key-cap columns =====================

    private void BuildColumns()
    {
        OverlayTitle.Text = Loc.I.Pick("Windows Shortcuts", "Windows 快捷鍵");
        OverlayHint.Text = Loc.I.Pick("Release Win or press Esc to close", "放開 Win 或揿 Esc 收起");

        var groups = ShortcutGuideService.OverlayGroups;

        // Lay the groups out across up to 3 balanced columns.
        ColumnsHost.Children.Clear();
        ColumnsHost.ColumnDefinitions.Clear();

        int columnCount = groups.Count >= 5 ? 3 : Math.Max(1, groups.Count);
        for (int c = 0; c < columnCount; c++)
            ColumnsHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var columns = new StackPanel[columnCount];
        for (int c = 0; c < columnCount; c++)
        {
            columns[c] = new StackPanel { Spacing = 18 };
            Grid.SetColumn(columns[c], c);
            ColumnsHost.Children.Add(columns[c]);
        }

        // Round-robin groups into the shortest column for a balanced look.
        var heights = new int[columnCount];
        foreach (var g in groups)
        {
            int target = 0;
            for (int c = 1; c < columnCount; c++)
                if (heights[c] < heights[target]) target = c;
            columns[target].Children.Add(BuildGroup(g));
            heights[target] += g.Items.Count + 2; // header + rows ≈ visual weight
        }
    }

    private FrameworkElement BuildGroup(ShortcutGroup g)
    {
        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = g.Title,
            FontWeight = FontWeights.SemiBold,
            FontSize = 15,
            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
        });

        foreach (var item in g.Items)
            panel.Children.Add(BuildRow(item));

        return panel;
    }

    private FrameworkElement BuildRow(ShortcutItem item)
    {
        var grid = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(186) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Key-cap chips
        var keys = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        var capBg = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"];
        var capBorder = (Brush)Application.Current.Resources["ControlElevationBorderBrush"];
        var capFg = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        for (int i = 0; i < item.Keys.Length; i++)
        {
            if (i > 0)
                keys.Children.Add(new TextBlock
                {
                    Text = "+",
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                    FontSize = 12,
                });
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
            FontSize = 13.5,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        Grid.SetColumn(desc, 1);
        grid.Children.Add(desc);

        return grid;
    }
}
