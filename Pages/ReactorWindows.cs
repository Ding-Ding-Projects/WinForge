using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 反應堆獨立視窗 · Dedicated reactor windows (control-room view + always-on-top desktop widgets).
/// Both share the SAME <see cref="ReactorSimService"/> passed in the constructor — no second sim —
/// and are tracked in a static list so the main window can close them all on exit. Fully bilingual.
/// </summary>
public static class ReactorWindowManager
{
    private static readonly List<Window> Open = new();

    public static void Track(Window w)
    {
        Open.Add(w);
        w.Closed += (_, _) => Open.Remove(w);
    }

    /// <summary>主視窗關閉時收埋所有反應堆視窗 · Close every reactor window when the main window exits.</summary>
    public static void CloseAll()
    {
        foreach (var w in Open.ToArray())
        {
            try { w.Close(); } catch { /* best effort */ }
        }
        Open.Clear();
    }
}

/// <summary>完整控制室視窗 · Full control-room window in its own AppWindow.</summary>
public sealed class ReactorControlRoomWindow : Window
{
    private readonly ReactorSimService _sim;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly OverlappedPresenter _presenter = OverlappedPresenter.Create();
    private bool _full;

    private TextBlock _status = null!, _readout = null!;
    private Border _statusDot = null!;
    private SpriteVisual? _glow;
    private Canvas _coreHost = null!;
    private readonly ReactorFx.RenderClock _clock = new();
    private CompositionPropertySet? _props;

    public ReactorControlRoomWindow(ReactorSimService sim)
    {
        _sim = sim;
        Title = "Reactor Control Room · 反應堆控制室";
        try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { }
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetPresenter(_presenter);

        var root = BuildContent();
        Content = root;

        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            AppWindow.Resize(new SizeInt32(980, 760));
            AppWindow.Move(new PointInt32(area.WorkArea.X + 60, area.WorkArea.Y + 40));
        }
        catch { }

        ReactorWindowManager.Track(this);

        root.Loaded += (_, _) =>
        {
            try
            {
                var (c, container) = ReactorFx.Bind(_coreHost);
                _props = c.CreatePropertySet();
                _props.InsertScalar("power", 0);
                _glow = ReactorFx.CherenkovGlow(c, 110);
                _glow.Offset = new Vector3(110, 110, 0);
                container.Children.InsertAtTop(_glow);
            }
            catch { }
            _clock.Start(OnFrame);
            _timer.Tick += Tick;
            _timer.Start();
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _clock.Stop();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private FrameworkElement BuildContent()
    {
        var grid = new Grid { Background = new SolidColorBrush(Color.FromArgb(255, 0x15, 0x17, 0x1A)) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // toolbar
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Padding = new Thickness(48, 8, 12, 8) };
        var aot = new ToggleButton { Content = P("Always on top · 永遠置頂", "永遠置頂 · Always on top") };
        aot.Checked += (_, _) => { _presenter.IsAlwaysOnTop = true; };
        aot.Unchecked += (_, _) => { _presenter.IsAlwaysOnTop = false; };
        var fs = new Button { Content = P("Full screen · 全螢幕", "全螢幕 · Full screen") };
        fs.Click += (_, _) => ToggleFull();
        var scram = new Button
        {
            Content = P("SCRAM", "緊急停堆 SCRAM"),
            Background = new SolidColorBrush(Color.FromArgb(255, 0xC6, 0x28, 0x28)),
            Foreground = new SolidColorBrush(Colors.White),
        };
        scram.Click += (_, _) => { _sim.Scram(); ReactorAudioEngine.I.Klaxon(true); };
        var mute = new ToggleButton { Content = P("Mute audio · 靜音", "靜音 · Mute audio"), IsChecked = !ReactorAudioEngine.I.Enabled };
        mute.Checked += (_, _) => ReactorAudioEngine.I.SetEnabled(false);
        mute.Unchecked += (_, _) => ReactorAudioEngine.I.SetEnabled(true);
        bar.Children.Add(aot); bar.Children.Add(fs); bar.Children.Add(mute); bar.Children.Add(scram);
        Grid.SetRow(bar, 0);
        grid.Children.Add(bar);

        // status row
        var statusPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Padding = new Thickness(16, 4, 16, 8) };
        _statusDot = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xAF, 0x50)), VerticalAlignment = VerticalAlignment.Center };
        _status = new TextBlock { FontSize = 20, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center };
        statusPanel.Children.Add(_statusDot);
        statusPanel.Children.Add(_status);
        Grid.SetRow(statusPanel, 1);
        grid.Children.Add(statusPanel);

        // core + readouts
        var content = new Grid { Padding = new Thickness(16) };
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _coreHost = new Canvas { Width = 220, Height = 220 };
        // draw a static vessel ring behind the glow
        _coreHost.Children.Add(new Ellipse
        {
            Width = 200, Height = 200,
            Stroke = new SolidColorBrush(Color.FromArgb(180, 0x55, 0x88, 0xCC)),
            StrokeThickness = 3,
        });
        Canvas.SetLeft(_coreHost.Children[0], 10);
        Canvas.SetTop(_coreHost.Children[0], 10);
        Grid.SetColumn(_coreHost, 0);
        content.Children.Add(_coreHost);

        _readout = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 16,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 0x90, 0xCA, 0xF9)),
            Margin = new Thickness(24, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_readout, 1);
        content.Children.Add(_readout);
        Grid.SetRow(content, 2);
        grid.Children.Add(content);

        return grid;
    }

    private void ToggleFull()
    {
        _full = !_full;
        if (_full) AppWindow.SetPresenter(FullScreenPresenter.Create());
        else AppWindow.SetPresenter(_presenter);
    }

    private void OnFrame(double dt)
    {
        if (_glow is null || _props is null) return;
        var snap = _sim.Capture();
        float p = (float)Math.Clamp(snap.Power, 0, 1.2);
        _glow.Opacity = Math.Clamp(p * 1.1f, 0f, 1f);
        float scale = 0.4f + p * 0.9f;
        _glow.Scale = new Vector3(scale, scale, 1);
        if (snap.Mode == ReactorMode.Meltdown)
        {
            var b = (CompositionRadialGradientBrush)_glow.Brush;
            // tint toward red on meltdown
        }
    }

    private void Tick(object? sender, object e)
    {
        _status.Text = P(_sim.StatusEn, _sim.StatusZh);
        Color dot = _sim.Mode == ReactorMode.Meltdown ? Color.FromArgb(255, 0xFF, 0x17, 0x44)
            : _sim.IsScrammed ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
            : Color.FromArgb(255, 0x4C, 0xAF, 0x50);
        _statusDot.Background = new SolidColorBrush(dot);
        _readout.Text =
            $"{P("Power", "功率")}   {_sim.NeutronPowerFraction * 100,7:F1} %\n" +
            $"{P("Thermal", "熱功率")} {_sim.ThermalPowerMW,7:F0} MWt\n" +
            $"{P("Electric", "電功率")}{_sim.ElectricPowerMW,7:F0} MWe\n" +
            $"{P("Fuel T", "燃料溫")} {_sim.FuelTemp,7:F0} °C\n" +
            $"{P("Tavg", "平均溫")}   {_sim.Tavg,7:F0} °C\n" +
            $"{P("Primary", "一迴路")}{_sim.PrimaryPressure * 145.038,7:F0} psia\n" +
            $"{P("Subcool", "過冷度")} {_sim.SubcoolingMarginC,7:F0} °C\n" +
            $"{P("Decay", "衰變熱")}  {_sim.DecayHeatFraction * 100,7:F1} %\n" +
            $"{P("Turbine", "汽輪機")} {_sim.TurbineRPM,7:F0} rpm";
    }
}

/// <summary>桌面小工具種類 · Desktop widget kinds.</summary>
public enum WidgetKind { CorePower, Status, Scram }

/// <summary>常駐置頂桌面小工具 · A borderless always-on-top desktop widget mini-window.</summary>
public sealed class ReactorWidgetWindow : Window
{
    private readonly ReactorSimService _sim;
    private readonly WidgetKind _kind;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly OverlappedPresenter _presenter = OverlappedPresenter.Create();

    private TextBlock _value = null!, _label = null!;
    private Ellipse _ring = null!;
    private Border _pill = null!;

    public ReactorWidgetWindow(ReactorSimService sim, WidgetKind kind)
    {
        _sim = sim; _kind = kind;
        Title = "Reactor widget";
        ExtendsContentIntoTitleBar = true;

        _presenter.SetBorderAndTitleBar(false, false);
        _presenter.IsAlwaysOnTop = true;
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;
        _presenter.IsMinimizable = false;
        AppWindow.SetPresenter(_presenter);
        AppWindow.Resize(new SizeInt32(220, 220));

        Content = BuildContent();

        // Restore persisted position.
        try
        {
            string kx = $"reactor.widget.{kind}.x";
            string ky = $"reactor.widget.{kind}.y";
            int x = int.TryParse(SettingsStore.Get(kx, ""), out var px) ? px : -1;
            int y = int.TryParse(SettingsStore.Get(ky, ""), out var py) ? py : -1;
            if (x >= 0 && y >= 0) AppWindow.Move(new PointInt32(x, y));
            else
            {
                var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
                AppWindow.Move(new PointInt32(area.WorkArea.X + area.WorkArea.Width - 260,
                    area.WorkArea.Y + 40 + 240 * (int)kind));
            }
        }
        catch { }

        ReactorWindowManager.Track(this);
        AppWindow.Changed += (s, args) =>
        {
            if (args.DidPositionChange)
            {
                SettingsStore.Set($"reactor.widget.{kind}.x", AppWindow.Position.X.ToString());
                SettingsStore.Set($"reactor.widget.{kind}.y", AppWindow.Position.Y.ToString());
            }
        };

        _timer.Tick += Tick;
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private FrameworkElement BuildContent()
    {
        var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(235, 0x10, 0x12, 0x16)), CornerRadius = new CornerRadius(12) };
        // drag handle: whole surface drags the window
        root.PointerPressed += (s, e) =>
        {
            var pp = e.GetCurrentPoint(root);
            if (pp.Properties.IsLeftButtonPressed) BeginDrag();
        };

        // close affordance
        var close = new Button
        {
            Content = "✕",
            Width = 24, Height = 24, Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 6, 6, 0),
            Background = new SolidColorBrush(Color.FromArgb(60, 0xFF, 0xFF, 0xFF)),
            Foreground = new SolidColorBrush(Colors.White),
        };
        close.Click += (_, _) => Close();

        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6 };

        if (_kind == WidgetKind.Scram)
        {
            var btn = new Button
            {
                Content = P("SCRAM", "緊急停堆"),
                Width = 150, Height = 150,
                CornerRadius = new CornerRadius(75),
                FontSize = 22, FontWeight = FontWeights.Bold,
                Background = new SolidColorBrush(Color.FromArgb(255, 0xC6, 0x28, 0x28)),
                Foreground = new SolidColorBrush(Colors.White),
            };
            btn.Click += (_, _) => { _sim.Scram(); ReactorAudioEngine.I.Klaxon(true); };
            panel.Children.Add(btn);
            _label = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(180, 0xFF, 0xFF, 0xFF)), HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(_label);
        }
        else if (_kind == WidgetKind.Status)
        {
            _pill = new Border { Width = 150, Height = 60, CornerRadius = new CornerRadius(30), Background = new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xAF, 0x50)), HorizontalAlignment = HorizontalAlignment.Center };
            _label = new TextBlock { Foreground = new SolidColorBrush(Colors.White), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
            _pill.Child = _label;
            panel.Children.Add(_pill);
            _value = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(200, 0x90, 0xCA, 0xF9)), FontFamily = new FontFamily("Consolas"), HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(_value);
        }
        else // CorePower
        {
            var canvas = new Canvas { Width = 150, Height = 150 };
            _ring = new Ellipse { Width = 130, Height = 130, Stroke = new SolidColorBrush(Color.FromArgb(255, 0x1B, 0x6C, 0xFF)), StrokeThickness = 10 };
            Canvas.SetLeft(_ring, 10); Canvas.SetTop(_ring, 10);
            canvas.Children.Add(_ring);
            _value = new TextBlock { FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Colors.White), Width = 150, TextAlignment = TextAlignment.Center };
            Canvas.SetTop(_value, 58);
            canvas.Children.Add(_value);
            panel.Children.Add(canvas);
            _label = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(180, 0xFF, 0xFF, 0xFF)), HorizontalAlignment = HorizontalAlignment.Center };
            panel.Children.Add(_label);
        }

        root.Children.Add(panel);
        root.Children.Add(close);
        return root;
    }

    private void BeginDrag()
    {
        // Lightweight drag: move window by following the cursor while the button is held.
        // (AppWindow has no native drag for borderless; we nudge via the global cursor delta.)
        try
        {
            var startCursor = GetCursor();
            var startPos = AppWindow.Position;
            var dt = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
            dt.Tick += (_, _) =>
            {
                if (!LeftButtonDown())
                {
                    dt.Stop();
                    SettingsStore.Set($"reactor.widget.{_kind}.x", AppWindow.Position.X.ToString());
                    SettingsStore.Set($"reactor.widget.{_kind}.y", AppWindow.Position.Y.ToString());
                    return;
                }
                var now = GetCursor();
                AppWindow.Move(new PointInt32(startPos.X + (now.X - startCursor.X), startPos.Y + (now.Y - startCursor.Y)));
            };
            dt.Start();
        }
        catch { }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT p);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    private static PointInt32 GetCursor() { GetCursorPos(out var p); return new PointInt32(p.X, p.Y); }
    private static bool LeftButtonDown() => (GetAsyncKeyState(0x01) & 0x8000) != 0;

    private void Tick(object? sender, object e)
    {
        switch (_kind)
        {
            case WidgetKind.CorePower:
                double pct = _sim.NeutronPowerFraction * 100;
                _value.Text = $"{pct:F0}%";
                Color rc = pct > 109 ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
                    : pct > 100 ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
                    : Color.FromArgb(255, 0x1B, 0x6C, 0xFF);
                _ring.Stroke = new SolidColorBrush(rc);
                _label.Text = P("Core power · 核功率", "核功率 · Core power");
                break;
            case WidgetKind.Status:
                _label.Text = P(_sim.StatusEn, _sim.StatusZh);
                Color sc = _sim.Mode == ReactorMode.Meltdown ? Color.FromArgb(255, 0xC6, 0x28, 0x28)
                    : _sim.IsScrammed ? Color.FromArgb(255, 0xF5, 0x7C, 0x00)
                    : Color.FromArgb(255, 0x4C, 0xAF, 0x50);
                _pill.Background = new SolidColorBrush(sc);
                _value.Text = $"{_sim.ElectricPowerMW:F0} MWe";
                break;
            case WidgetKind.Scram:
                _label.Text = _sim.IsScrammed ? P("TRIPPED · 已跳機", "已跳機 · TRIPPED") : P("Armed · 待命", "待命 · Armed");
                break;
        }
    }
}
