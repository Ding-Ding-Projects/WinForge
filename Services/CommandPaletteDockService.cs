using System;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace WinForge.Services;

/// <summary>
/// Command Palette Dock · 指令面板 Dock。A persistent, topmost edge launcher for saved palette results.
/// Pins are resolved by CommandPaletteService so dynamic actions remain current instead of serializing delegates.
/// </summary>
public enum CommandPaletteDockSide { Top, Bottom, Left, Right }

public static class CommandPaletteDockService
{
    private const string KeyEnabled = "cmdpal.dock.enabled";
    private const string KeySide = "cmdpal.dock.side";
    private static DispatcherQueue? _ui;
    private static CommandPaletteDockWindow? _window;

    public static bool Enabled
    {
        get => SettingsStore.Get(KeyEnabled, "False") == "True";
        set => SettingsStore.Set(KeyEnabled, value.ToString());
    }

    public static CommandPaletteDockSide Side
    {
        get => Enum.TryParse<CommandPaletteDockSide>(SettingsStore.Get(KeySide, CommandPaletteDockSide.Bottom.ToString()), true, out var side)
            ? side : CommandPaletteDockSide.Bottom;
        set => SettingsStore.Set(KeySide, value.ToString());
    }

    public static void Initialize(DispatcherQueue uiQueue)
    {
        _ui = uiQueue;
        Reapply();
    }

    public static void Reapply()
    {
        if (_ui is null) return;
        if (_ui.HasThreadAccess) Apply();
        else _ui.TryEnqueue(Apply);
    }

    public static void Refresh()
    {
        if (_ui is null) return;
        if (_ui.HasThreadAccess) _window?.Refresh();
        else _ui.TryEnqueue(() => _window?.Refresh());
    }

    private static void Apply()
    {
        if (!Enabled || !CommandPaletteService.Enabled)
        {
            _window?.Hide();
            return;
        }
        _window ??= new CommandPaletteDockWindow();
        _window.ShowOrRefresh();
    }
}

internal sealed class CommandPaletteDockWindow
{
    private readonly Window _window;
    private readonly Grid _root;
    private readonly StackPanel _actions;

    public CommandPaletteDockWindow()
    {
        _window = new Window { Title = "WinForge Command Palette Dock · WinForge 指令面板 Dock" };
        var appWindow = _window.AppWindow;
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(false, false);
        appWindow.SetPresenter(presenter);
        try { appWindow.IsShownInSwitchers = false; } catch { }

        _root = new Grid
        {
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(12),
            Background = ResBrush("AcrylicInAppFillColorDefaultBrush", Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2B)),
            BorderBrush = ResBrush("CardStrokeColorDefaultBrush", Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
        };
        try { if (App.Shell?.Content is FrameworkElement shell) _root.RequestedTheme = shell.RequestedTheme; } catch { }

        _actions = new StackPanel { Spacing = 4 };
        _root.Children.Add(_actions);
        _window.Content = _root;
        Loc.I.LanguageChanged += OnLanguageChanged;
    }

    public void ShowOrRefresh()
    {
        Refresh();
        try { _window.AppWindow.Show(); } catch { }
    }

    public void Hide()
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        try { _window?.AppWindow.Hide(); } catch { }
    }

    public void Refresh()
    {
        bool vertical = CommandPaletteDockService.Side is CommandPaletteDockSide.Left or CommandPaletteDockSide.Right;
        _actions.Orientation = vertical ? Orientation.Vertical : Orientation.Horizontal;
        _actions.Children.Clear();
        AddAction(
            Loc.I.Pick("Open", "打開"),
            ((char)0xE721).ToString(),
            Loc.I.Pick("Open Command Palette", "打開指令面板"),
            CommandPaletteWindow.Open,
            vertical);

        foreach (var pin in CommandPaletteService.EffectiveDockPins)
        {
            string label = ShortLabel(pin.Title, vertical ? 18 : 12);
            AddAction(label, string.IsNullOrWhiteSpace(pin.Glyph) ? ((char)0xE756).ToString() : pin.Glyph,
                string.IsNullOrWhiteSpace(pin.Subtitle) ? pin.Title : $"{pin.Title}\n{pin.Subtitle}",
                () => CommandPaletteService.InvokeDockPin(pin), vertical);
        }
        MoveToEdge(vertical, _actions.Children.Count);
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    private void AddAction(string label, string glyph, string toolTip, Action action, bool vertical)
    {
        var button = new Button
        {
            Padding = vertical ? new Thickness(9, 7, 12, 7) : new Thickness(10, 5, 10, 5),
            MinHeight = 42,
            MinWidth = vertical ? 146 : 66,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var panel = new StackPanel
        {
            Orientation = vertical ? Orientation.Horizontal : Orientation.Vertical,
            Spacing = vertical ? 8 : 1,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        panel.Children.Add(new FontIcon { Glyph = glyph, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = vertical ? 104 : 80,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        });
        button.Content = panel;
        ToolTipService.SetToolTip(button, toolTip);
        button.Click += (_, _) => { try { action(); } catch { } };
        _actions.Children.Add(button);
    }

    private void MoveToEdge(bool vertical, int count)
    {
        try
        {
            var appWindow = _window.AppWindow;
            var area = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;
            int width = vertical ? 166 : Math.Min(Math.Max(86, count * 96 + 16), Math.Max(120, area.Width - 24));
            int height = vertical ? Math.Min(Math.Max(58, count * 48 + 16), Math.Max(120, area.Height - 24)) : 66;
            appWindow.Resize(new SizeInt32(width, height));
            int x = CommandPaletteDockService.Side switch
            {
                CommandPaletteDockSide.Left => area.X + 8,
                CommandPaletteDockSide.Right => area.X + area.Width - width - 8,
                _ => area.X + (area.Width - width) / 2,
            };
            int y = CommandPaletteDockService.Side switch
            {
                CommandPaletteDockSide.Top => area.Y + 8,
                CommandPaletteDockSide.Bottom => area.Y + area.Height - height - 8,
                _ => area.Y + (area.Height - height) / 2,
            };
            appWindow.Move(new PointInt32(x, y));
        }
        catch { }
    }

    private static string ShortLabel(string value, int limit)
    {
        value = (value ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
        return value.Length > limit ? value.Substring(0, Math.Max(1, limit - 1)) + "..." : value;
    }

    private static Brush ResBrush(string key, Color fallback)
    {
        try
        {
            if (Application.Current.Resources.TryGetValue(key, out var value) && value is Brush brush) return brush;
        }
        catch { }
        return new SolidColorBrush(fallback);
    }
}
