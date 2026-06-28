using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Close full control-room windows, leaving small desktop widgets alone.</summary>
    public static void CloseControlRooms()
    {
        foreach (var w in Open.ToArray())
        {
            if (w is ReactorHtmlWindow or ReactorControlRoomWindow)
            {
                try { w.Close(); } catch { /* best effort */ }
            }
        }
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
            $"{P("CET", "出口熱偶")}{_sim.CoreExitTempC,7:F0} °C{(_sim.IccRed ? " R" : _sim.IccOrange ? " O" : "")}\n" +
            $"{P("Decay", "衰變熱")}  {_sim.DecayHeatFraction * 100,7:F1} %\n" +
            $"{P("Turbine", "汽輪機")} {_sim.TurbineRPM,7:F0} rpm";
    }
}

/// <summary>桌面小工具種類 · Desktop widget kinds.</summary>
public enum WidgetKind { CorePower, Status, Scram, StartupGauges }

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
    private readonly List<(TextBlock label, TextBlock value, Border fill)> _startupGaugeRows = new();

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
        AppWindow.Resize(kind == WidgetKind.StartupGauges ? new SizeInt32(320, 340) : new SizeInt32(220, 220));

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
        else if (_kind == WidgetKind.StartupGauges)
        {
            panel.Width = 270;
            panel.Spacing = 8;
            _label = new TextBlock
            {
                Text = P("Startup gauges", "啟動儀表"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            panel.Children.Add(_label);
            AddStartupGaugeRow(panel);
            AddStartupGaugeRow(panel);
            AddStartupGaugeRow(panel);
            AddStartupGaugeRow(panel);
            AddStartupGaugeRow(panel);
            AddStartupGaugeRow(panel);
            _value = new TextBlock
            {
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(Color.FromArgb(210, 0x90, 0xCA, 0xF9)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
            };
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

    private void AddStartupGaugeRow(StackPanel panel)
    {
        var row = new StackPanel { Spacing = 3 };
        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromArgb(220, 0xDD, 0xE7, 0xF4)),
        };
        var value = new TextBlock
        {
            FontSize = 11,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Colors.White),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(value, 1);
        top.Children.Add(label);
        top.Children.Add(value);

        var bar = new Grid();
        bar.Children.Add(new Border
        {
            Height = 6,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromArgb(55, 0xFF, 0xFF, 0xFF)),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        });
        var fill = new Border
        {
            Height = 6,
            Width = 0,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xB3, 0x00)),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        bar.Children.Add(fill);
        row.Children.Add(top);
        row.Children.Add(bar);
        panel.Children.Add(row);
        _startupGaugeRows.Add((label, value, fill));
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
            case WidgetKind.StartupGauges:
                TickStartupGauges();
                break;
        }
    }

    private void TickStartupGauges()
    {
        int pumps = 0;
        foreach (var running in _sim.RcpRunning) if (running) pumps++;
        double avgRodIn = 0;
        foreach (var p in _sim.RodBankInsertion) avgRodIn += p;
        avgRodIn /= Math.Max(1, _sim.RodBankInsertion.Length);

        int done = ReactorScenarios.CompletedStartupSteps(ReactorScenarios.StartupSequence(), _sim);
        _label.Text = P("Startup gauges", "啟動儀表");
        _value.Text = P($"Checklist {done}/8{(_sim.EasyStartupMode ? " · EASY ×1.5 burn" : "")}",
                        $"程序 {done}/8{(_sim.EasyStartupMode ? " · EASY 燃耗 ×1.5" : "")}");

        SetStartupGauge(0, P("RCP pumps", "主泵"), $"{pumps}/4", pumps / 3.0, pumps >= 3);
        SetStartupGauge(1, P("RCP flow", "主泵流量"), $"{_sim.CoolantFlowFraction * 100:F0}%", _sim.CoolantFlowFraction / 0.85, _sim.CoolantFlowFraction > 0.85);
        SetStartupGauge(2, P("Primary pressure", "一迴路壓力"), $"{_sim.PrimaryPressure * 145.038:F0} psia", (_sim.PrimaryPressure * 145.038) / 2235.0, _sim.PrimaryPressure > 14.5);
        SetStartupGauge(3, P("Rods / boron", "棒位／硼"), $"{avgRodIn:F0}% in / {_sim.BoronPpm:F0} ppm", Math.Max((100 - avgRodIn) / 40.0, (1500 - _sim.BoronPpm) / 500.0), avgRodIn < 60 || _sim.BoronPpm < 1000);
        SetStartupGauge(4, "1/M", $"{_sim.OneOverM:F3}", 1.0 - _sim.OneOverM / 0.25, _sim.OneOverM < 0.25);
        SetStartupGauge(5, P("Period / power", "週期／功率"), $"{PeriodLabel()} / {_sim.NeutronPowerFraction * 100:F2}%", _sim.NeutronPowerFraction / 0.001, _sim.NeutronPowerFraction > 1e-3 && _sim.ReactorPeriodSeconds > 30 && _sim.ReactorPeriodSeconds < 1e8);
    }

    private void SetStartupGauge(int index, string label, string value, double fraction, bool ok)
    {
        if (index < 0 || index >= _startupGaugeRows.Count) return;
        var row = _startupGaugeRows[index];
        double pct = Math.Clamp(fraction, 0, 1);
        row.label.Text = label;
        row.value.Text = value;
        row.fill.Width = 270 * pct;
        Color color = ok ? Color.FromArgb(255, 0x4C, 0xAF, 0x50)
            : pct > 0.7 ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
            : Color.FromArgb(255, 0x42, 0xA5, 0xF5);
        row.fill.Background = new SolidColorBrush(color);
    }

    private string PeriodLabel()
    {
        double p = _sim.ReactorPeriodSeconds;
        if (Math.Abs(p) >= 1e5) return "∞";
        if (Math.Abs(p) >= 999) return $"{p:F0}s";
        return $"{p:+0;-0;0}s";
    }
}

/// <summary>Startup-checklist widget · 獨立啟動程序清單小工具.</summary>
public sealed class ReactorStartupChecklistWindow : Window
{
    private readonly ReactorSimService _sim;
    private readonly Action<string>? _navigateTarget;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly OverlappedPresenter _presenter = OverlappedPresenter.Create();
    private readonly List<(StartupStep step, TextBlock mark, TextBlock title, TextBlock control, TextBlock detail, Button go, Button? skip, Border row)> _rows = new();

    private TextBlock _title = null!;
    private TextBlock _progress = null!;
    private TextBlock _status = null!;
    private TextBlock _power = null!;
    private TextBlock _pressure = null!;
    private TextBlock _oneOverM = null!;

    public ReactorStartupChecklistWindow(ReactorSimService sim, Action<string>? navigateTarget = null)
    {
        _sim = sim;
        _navigateTarget = navigateTarget;
        Title = "Reactor startup checklist";
        ExtendsContentIntoTitleBar = true;

        _presenter.SetBorderAndTitleBar(false, false);
        _presenter.IsAlwaysOnTop = true;
        _presenter.IsResizable = false;
        _presenter.IsMaximizable = false;
        _presenter.IsMinimizable = false;
        AppWindow.SetPresenter(_presenter);
        AppWindow.Resize(new SizeInt32(460, 640));

        Content = BuildContent();

        try
        {
            string kx = "reactor.widget.StartupChecklist.x";
            string ky = "reactor.widget.StartupChecklist.y";
            int x = int.TryParse(SettingsStore.Get(kx, ""), out var px) ? px : -1;
            int y = int.TryParse(SettingsStore.Get(ky, ""), out var py) ? py : -1;
            if (x >= 0 && y >= 0) AppWindow.Move(new PointInt32(x, y));
            else
            {
                var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
                AppWindow.Move(new PointInt32(
                    area.WorkArea.X + area.WorkArea.Width - 500,
                    area.WorkArea.Y + 40));
            }
        }
        catch { }

        ReactorWindowManager.Track(this);
        AppWindow.Changed += (_, args) =>
        {
            if (!args.DidPositionChange) return;
            SettingsStore.Set("reactor.widget.StartupChecklist.x", AppWindow.Position.X.ToString());
            SettingsStore.Set("reactor.widget.StartupChecklist.y", AppWindow.Position.Y.ToString());
        };

        _timer.Tick += Tick;
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
        Tick(null, EventArgs.Empty);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private FrameworkElement BuildContent()
    {
        var root = new Grid
        {
            Background = new SolidColorBrush(Color.FromArgb(238, 0x10, 0x12, 0x16)),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            RowSpacing = 10,
        };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.PointerPressed += (_, e) =>
        {
            var pp = e.GetCurrentPoint(root);
            if (pp.Properties.IsLeftButtonPressed) BeginDrag();
        };

        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerCopy = new StackPanel { Spacing = 2 };
        _title = new TextBlock
        {
            FontSize = 17,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.White),
            TextWrapping = TextWrapping.Wrap,
        };
        _progress = new TextBlock
        {
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(210, 0xCF, 0xD8, 0xDC)),
        };
        headerCopy.Children.Add(_title);
        headerCopy.Children.Add(_progress);
        Grid.SetColumn(headerCopy, 0);
        header.Children.Add(headerCopy);

        var close = new Button
        {
            Content = "✕",
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Color.FromArgb(60, 0xFF, 0xFF, 0xFF)),
            Foreground = new SolidColorBrush(Colors.White),
        };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var metrics = new Grid { ColumnSpacing = 8, RowSpacing = 4 };
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        metrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _status = MetricText();
        _power = MetricText();
        _pressure = MetricText();
        _oneOverM = MetricText();
        metrics.Children.Add(_status);
        Grid.SetColumn(_power, 1);
        metrics.Children.Add(_power);
        Grid.SetRow(_pressure, 1);
        metrics.Children.Add(_pressure);
        Grid.SetRow(_oneOverM, 1);
        Grid.SetColumn(_oneOverM, 1);
        metrics.Children.Add(_oneOverM);
        Grid.SetRow(metrics, 1);
        root.Children.Add(metrics);

        var list = new StackPanel { Spacing = 6 };
        foreach (var step in ReactorScenarios.StartupSequence())
        {
            var mark = new TextBlock
            {
                Text = "○",
                FontSize = 18,
                Width = 24,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromArgb(170, 0xAA, 0xAA, 0xAA)),
            };
            var title = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White),
            };
            var control = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(190, 0x9E, 0xA7, 0xB0)),
                Margin = new Thickness(0, 2, 0, 0),
            };
            var detail = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 0xFF, 0xD1, 0x80)),
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = string.IsNullOrWhiteSpace(step.DetailEn) ? Visibility.Collapsed : Visibility.Visible,
            };
            var copy = new StackPanel { Spacing = 0 };
            copy.Children.Add(title);
            copy.Children.Add(control);
            copy.Children.Add(detail);

            var go = new Button
            {
                Content = P("Control", "控制"),
                MinWidth = 70,
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(70, 0x1B, 0x6C, 0xFF)),
                Foreground = new SolidColorBrush(Colors.White),
            };
            go.Click += (_, _) =>
            {
                RestoreInteractive();
                _navigateTarget?.Invoke(step.ControlTarget);
            };
            Button? skip = null;
            var actions = new StackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            if (step.EasyModeSkippable)
            {
                skip = new Button
                {
                    Content = P("Skip step 4", "跳過第 4 步"),
                    MinWidth = 92,
                    Padding = new Thickness(8, 2, 8, 2),
                    Background = new SolidColorBrush(Color.FromArgb(75, 0x42, 0xA5, 0xF5)),
                    Foreground = new SolidColorBrush(Colors.White),
                    Visibility = Visibility.Collapsed,
                };
                skip.Click += (_, _) =>
                {
                    if (!_sim.EasyStartupMode) return;
                    _sim.EasyStartupSkipPressureStep = true;
                    PersistenceService.I.NoteChanged();
                    Tick(null, EventArgs.Empty);
                };
                actions.Children.Add(skip);
            }
            actions.Children.Add(go);

            var rowGrid = new Grid { ColumnSpacing = 6 };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(mark, 0);
            Grid.SetColumn(copy, 1);
            Grid.SetColumn(actions, 2);
            rowGrid.Children.Add(mark);
            rowGrid.Children.Add(copy);
            rowGrid.Children.Add(actions);

            var row = new Border
            {
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(8, 7, 8, 7),
                Background = new SolidColorBrush(Color.FromArgb(55, 0xFF, 0xFF, 0xFF)),
                Child = rowGrid,
            };
            list.Children.Add(row);
            _rows.Add((step, mark, title, control, detail, go, skip, row));
        }

        var scroller = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Grid.SetRow(scroller, 2);
        root.Children.Add(scroller);

        return root;
    }

    private static TextBlock MetricText() => new()
    {
        FontSize = 12,
        FontFamily = new FontFamily("Consolas"),
        Foreground = new SolidColorBrush(Color.FromArgb(220, 0x90, 0xCA, 0xF9)),
        TextWrapping = TextWrapping.Wrap,
    };

    private void Tick(object? sender, object e)
    {
        _title.Text = P("Startup checklist", "啟動程序清單");

        int done = ReactorScenarios.CompletedStartupSteps(_rows.Select(x => x.step).ToArray(), _sim);
        for (int i = 0; i < _rows.Count; i++)
        {
            var (step, mark, title, control, detail, go, skip, row) = _rows[i];
            bool ok = i < done;
            bool active = i == done && done < _rows.Count;
            bool skipped = step.IsSkipped(_sim);

            mark.Text = skipped ? "↷" : ok ? "✓" : active ? "→" : "○";
            mark.Foreground = new SolidColorBrush(ok
                ? skipped ? Color.FromArgb(255, 0x90, 0xCA, 0xF9) : Color.FromArgb(255, 0x4C, 0xAF, 0x50)
                : active ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
                : Color.FromArgb(170, 0xAA, 0xAA, 0xAA));
            row.Background = new SolidColorBrush(ok
                ? skipped ? Color.FromArgb(75, 0x15, 0x4D, 0x78) : Color.FromArgb(75, 0x2E, 0x7D, 0x32)
                : active ? Color.FromArgb(75, 0x7A, 0x55, 0x12)
                : Color.FromArgb(55, 0xFF, 0xFF, 0xFF));
            row.Opacity = i > done ? 0.68 : 1.0;
            title.Text = $"{i + 1}. " + P(step.En, step.Zh);
            control.Text = P($"Use: {step.ControlEn}", $"使用：{step.ControlZh}");
            var detailText = P(step.DetailEn, step.DetailZh);
            if (skipped)
                detailText += "\n" + P("Skipped in Easy Mode; pressure and trips remain live.", "已於簡易模式跳過；壓力同跳脫仍然即時生效。");
            detail.Text = detailText;
            go.Content = P("Control", "控制");
            if (skip is not null)
            {
                bool canShow = step.EasyModeSkippable && _sim.EasyStartupMode && !step.IsSatisfied(_sim);
                skip.Visibility = canShow || skipped ? Visibility.Visible : Visibility.Collapsed;
                skip.IsEnabled = canShow && !skipped;
                skip.Content = skipped ? P("Skipped", "已跳過") : P("Skip step 4", "跳過第 4 步");
            }
        }

        _progress.Text = P($"Checklist progress: {done}/{_rows.Count}",
                           $"程序進度：{done}/{_rows.Count}");
        _status.Text = P("Mode: ", "模式：") + P(_sim.StatusEn, _sim.StatusZh);
        _power.Text = P("Power: ", "功率：") + $"{_sim.NeutronPowerFraction * 100:F1}% / {_sim.ElectricPowerMW:F0} MWe";
        _pressure.Text = P("Primary: ", "一迴路：") + $"{_sim.PrimaryPressure:F1} MPa / {_sim.PrimaryPressure * 145.038:F0} psia";
        _oneOverM.Text = "1/M: " + $"{_sim.OneOverM:F3}";
    }

    public void RestoreInteractive()
    {
        try { Activate(); } catch { }
    }

    private void BeginDrag()
    {
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
                    SettingsStore.Set("reactor.widget.StartupChecklist.x", AppWindow.Position.X.ToString());
                    SettingsStore.Set("reactor.widget.StartupChecklist.y", AppWindow.Position.Y.ToString());
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
}
