using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using WinForge.Services;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace WinForge.Pages;

/// <summary>
/// 全模擬壓水式核反應堆模組 · Fully-simulated Pressurized Water Reactor (PWR) control room.
///
/// A self-contained educational simulation: point-kinetics + thermal-hydraulics engine
/// (<see cref="ReactorSimService"/>) driving a generated mimic diagram, analog gauges, scrolling
/// trend charts, an annunciator alarm panel and an exhaustive control surface. On meltdown a
/// dramatic overlay appears. Optional REAL PC shutdown is DEFAULT-OFF and gated behind an
/// abortable 10-second countdown (Win32 API, not shutdown.exe). Fully bilingual (English + 粵語).
/// </summary>
public sealed partial class ReactorModule : Page
{
    private readonly ReactorSimService _sim = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) }; // 10 Hz
    private DateTime _last = DateTime.UtcNow;
    private double _simClock; // seconds since start

    // Real-shutdown safety state.
    private bool _armRealShutdown; // DEFAULT OFF
    private DispatcherTimer? _countdownTimer;
    private int _countdownRemaining;
    private bool _aborted;
    private bool _shutdownIssued;
    private bool _meltdownHandled;

    // Trend buffers.
    private readonly Queue<double> _powHist = new();
    private readonly Queue<double> _tempHist = new();
    private readonly Queue<double> _pressHist = new();
    private const int HistMax = 150;

    // Gauge registry (built once; updated each tick).
    private readonly List<GaugeView> _gauges = new();
    // Alarm tile registry.
    private readonly Dictionary<ReactorAlarm, AlarmTile> _alarmTiles = new();

    // Klaxon flash phase.
    private double _flashPhase;

    // Cached control text blocks needing re-localization.
    private readonly List<Action> _relocalizers = new();

    public ReactorModule()
    {
        InitializeComponent();
        _sim.MeltdownOccurred += OnMeltdown;
        Loc.I.LanguageChanged += OnLanguageChanged;

        Loaded += (_, _) =>
        {
            BuildControls();
            BuildGauges();
            BuildAlarmTiles();
            DrawMimicStatic();
            Render();
            _last = DateTime.UtcNow;
            _timer.Tick += Tick;
            _timer.Start();
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _timer.Tick -= Tick;
            _countdownTimer?.Stop();
            Loc.I.LanguageChanged -= OnLanguageChanged;
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? s, EventArgs e)
    {
        Render();
        foreach (var r in _relocalizers) r();
    }

    // ============================================================ static labels ====
    private void Render()
    {
        HeaderTitle.Text = "Nuclear Reactor · 核反應堆";
        HeaderBlurb.Text = P(
            "A fully-simulated Pressurized Water Reactor: point-kinetics + thermal-hydraulics, with mimic diagram, analog gauges, trend charts, alarms and a full control surface. Educational simulation only — controls nothing real.",
            "全模擬壓水式核反應堆：點堆動力學＋熱工水力，配流程圖、指針儀表、趨勢圖、警報同完整控制台。純教育模擬 — 唔會控制任何真實硬件。");
        MimicTitle.Text = P("Plant Mimic Diagram · 機組流程圖", "機組流程圖 · Plant Mimic Diagram");
        GaugesTitle.Text = P("Instrument Gauges · 儀表", "儀表 · Instrument Gauges");
        TrendTitle.Text = P("Trend Charts · 趨勢圖", "趨勢圖 · Trend Charts");
        TrendPowerLabel.Text = P("Reactor power (%)", "反應堆功率（%）");
        TrendTempLabel.Text = P("Fuel temp (°C)", "燃料溫度（°C）");
        TrendPressLabel.Text = P("Primary pressure (MPa)", "一迴路壓力（MPa）");
        AlarmTitle.Text = P("Annunciator Panel · 警報盤", "警報盤 · Annunciator Panel");
        ScramText.Text = P("⚠ SCRAM — EMERGENCY SHUTDOWN ⚠ · 緊急停堆", "⚠ 緊急停堆 SCRAM ⚠ · EMERGENCY SHUTDOWN");
        ResetTripButton.Content = P("Reset trip · 重置跳機", "重置跳機 · Reset trip");
        AutoRunToggle.Header = P("Auto rod control · 自動棒控", "自動棒控 · Auto rod control");
        AutoRunToggle.OnContent = P("On", "開");
        AutoRunToggle.OffContent = P("Off", "關");
        MeltdownTitle.Text = P("CORE MELTDOWN", "爐心熔毀");
        AbortText.Text = P("ABORT SHUTDOWN · 中止關機", "中止關機 · ABORT SHUTDOWN");
        MeltdownCloseText.Text = P("Dismiss & reset simulation · 關閉並重設模擬", "關閉並重設模擬 · Dismiss & reset");
    }

    // ================================================================ main loop ====
    private void Tick(object? sender, object e)
    {
        var now = DateTime.UtcNow;
        double dt = (now - _last).TotalSeconds;
        _last = now;
        if (dt <= 0 || dt > 1.0) dt = 0.1;
        _simClock += dt;
        _flashPhase += dt;

        _sim.Update(dt);

        UpdateStatusBanner();
        UpdateGauges();
        UpdateAlarmTiles();
        UpdateMimic();
        PushTrends();
        DrawTrends();
        UpdateControlsLive();

        if (_sim.Mode == ReactorMode.Meltdown)
            AnimateMeltdown(dt);
    }

    // ============================================================== status banner ====
    private void UpdateStatusBanner()
    {
        StatusText.Text = P(_sim.StatusEn, _sim.StatusZh);
        ModeText.Text = P($"Mode: {ModeEn(_sim.Mode)}", $"模式：{ModeZh(_sim.Mode)}")
                        + $"  ·  {_simClock / 60:F1} min";
        ClockText.Text = $"T+{_simClock:000.0}s";

        Color dot;
        string warnEn = "", warnZh = "";
        if (_sim.Mode == ReactorMode.Meltdown) { dot = Color.FromArgb(255, 0xFF, 0x17, 0x44); warnEn = "CORE DAMAGE — meltdown in progress"; warnZh = "爐心受損 — 熔毀進行中"; }
        else if (_sim.IsScrammed) { dot = Color.FromArgb(255, 0xFF, 0xB3, 0x00); warnEn = "Reactor tripped (SCRAM)"; warnZh = "已緊急停堆（SCRAM）"; }
        else if (_sim.DamageAccumulation > 1.0) { dot = Color.FromArgb(255, 0xFF, 0x52, 0x52); warnEn = $"Core damage accruing ({_sim.DamageAccumulation:F0}%)"; warnZh = $"爐心受損中（{_sim.DamageAccumulation:F0}%）"; }
        else if (AnyAlarm()) { dot = Color.FromArgb(255, 0xFF, 0xB3, 0x00); warnEn = "Active alarms — check annunciator"; warnZh = "有警報 — 請檢查警報盤"; }
        else { dot = Color.FromArgb(255, 0x4C, 0xAF, 0x50); }

        StatusDot.Background = new SolidColorBrush(dot);
        BannerWarn.Text = (warnEn.Length > 0) ? P(warnEn, warnZh) : "";
    }

    private bool AnyAlarm()
    {
        foreach (ReactorAlarm a in Enum.GetValues(typeof(ReactorAlarm)))
            if (_sim.Alarm(a)) return true;
        return false;
    }

    private static string ModeEn(ReactorMode m) => m switch
    {
        ReactorMode.Shutdown => "Shutdown", ReactorMode.Startup => "Startup",
        ReactorMode.Run => "Run", ReactorMode.Tripped => "Tripped", _ => "Meltdown"
    };
    private static string ModeZh(ReactorMode m) => m switch
    {
        ReactorMode.Shutdown => "停機", ReactorMode.Startup => "啟動",
        ReactorMode.Run => "運轉", ReactorMode.Tripped => "跳機", _ => "熔毀"
    };

    // ================================================================ GAUGES ====
    private sealed class GaugeView
    {
        public Canvas Canvas = null!;
        public Path Needle = null!;
        public TextBlock Value = null!;
        public TextBlock Caption = null!;
        public double Min, Max;
        public Func<double> Read = null!;
        public Func<string> Format = null!;
        public double WarnFrac = 1.1;
    }

    private void AddGauge(string en, string zh, double min, double max, Func<double> read, Func<string> fmt, double warnFrac = 1.1)
    {
        const double w = 150, h = 150, cx = 75, cy = 88, r = 56;
        var canvas = new Canvas { Width = w, Height = h };

        // dial arc background (240° sweep from 150° to 30° going through bottom)
        var arc = MakeArc(cx, cy, r, 150, 390, Color.FromArgb(90, 0x88, 0x88, 0x88), 8);
        canvas.Children.Add(arc);

        // tick marks
        for (int i = 0; i <= 8; i++)
        {
            double ang = 150 + 240.0 * i / 8.0;
            double a = ang * Math.PI / 180.0;
            double x1 = cx + Math.Cos(a) * (r - 2), y1 = cy + Math.Sin(a) * (r - 2);
            double x2 = cx + Math.Cos(a) * (r - 9), y2 = cy + Math.Sin(a) * (r - 9);
            canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, StrokeThickness = 1.5, Stroke = new SolidColorBrush(Color.FromArgb(140, 0xAA, 0xAA, 0xAA)) });
        }

        var needle = new Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(255, 0x42, 0xA5, 0xF5)),
            Data = new PathGeometry()
        };
        canvas.Children.Add(needle);
        canvas.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Colors.White) });
        Canvas.SetLeft(canvas.Children[canvas.Children.Count - 1], cx - 4);
        Canvas.SetTop(canvas.Children[canvas.Children.Count - 1], cy - 4);

        var value = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 16, TextAlignment = TextAlignment.Center, Width = w };
        Canvas.SetTop(value, 104);
        var caption = new TextBlock { FontSize = 11, TextAlignment = TextAlignment.Center, Width = w, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        Canvas.SetTop(caption, 126);
        canvas.Children.Add(value);
        canvas.Children.Add(caption);

        var gv = new GaugeView { Canvas = canvas, Needle = needle, Value = value, Caption = caption, Min = min, Max = max, Read = read, Format = fmt, WarnFrac = warnFrac };
        _gauges.Add(gv);
        _relocalizers.Add(() => caption.Text = P(en, zh));
        caption.Text = P(en, zh);

        var border = new Border
        {
            Width = 150, Height = 150, CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(20, 0xFF, 0xFF, 0xFF)),
            Child = canvas,
        };
        GaugePanel.Children.Add(border);
    }

    private void BuildGauges()
    {
        AddGauge("Reactor power", "反應堆功率", 0, 120, () => _sim.NeutronPowerFraction * 100, () => $"{_sim.NeutronPowerFraction * 100:F1}%");
        AddGauge("Thermal power", "熱功率", 0, ReactorSimService.RatedThermalMW * 1.2, () => _sim.ThermalPowerMW, () => $"{_sim.ThermalPowerMW:F0} MWt");
        AddGauge("Electrical output", "電功率", 0, ReactorSimService.RatedElectricMW * 1.1, () => _sim.ElectricPowerMW, () => $"{_sim.ElectricPowerMW:F0} MWe");
        AddGauge("Neutron flux", "中子通量", 0, 120, () => _sim.NeutronPowerFraction * 100, () => $"{_sim.NeutronPowerFraction * 100:F1}%");
        AddGauge("Reactor period", "反應堆週期", 0, 100, () => Math.Min(100, Math.Abs(_sim.ReactorPeriodSeconds)), () => PeriodStr());
        AddGauge("Reactivity", "反應性", -2000, 2000, () => _sim.ReactivityPcm, () => $"{_sim.ReactivityPcm:F0} pcm");
        AddGauge("Fuel temp", "燃料溫度", 0, 3000, () => _sim.FuelTemp, () => $"{_sim.FuelTemp:F0}°C", warnFrac: ReactorSimService.FuelDamageTemp / 3000.0);
        AddGauge("Coolant Tavg", "冷卻劑平均溫", 0, 360, () => _sim.Tavg, () => $"{_sim.Tavg:F0}°C");
        AddGauge("Coolant Thot", "熱腿溫度", 0, 360, () => _sim.Thot, () => $"{_sim.Thot:F0}°C", warnFrac: 345.0 / 360.0);
        AddGauge("Coolant Tcold", "冷腿溫度", 0, 360, () => _sim.Tcold, () => $"{_sim.Tcold:F0}°C");
        AddGauge("Primary pressure", "一迴路壓力", 0, 20, () => _sim.PrimaryPressure, () => $"{_sim.PrimaryPressure:F1} MPa", warnFrac: ReactorSimService.VesselPressureLimit / 20.0);
        AddGauge("Pressurizer level", "穩壓器水位", 0, 100, () => _sim.PressurizerLevel, () => $"{_sim.PressurizerLevel:F0}%");
        AddGauge("Steam pressure", "蒸汽壓力", 0, 9, () => _sim.SteamPressure, () => $"{_sim.SteamPressure:F1} MPa");
        AddGauge("RCP flow", "主泵流量", 0, 100, () => _sim.CoolantFlowFraction * 100, () => $"{_sim.CoolantFlowFraction * 100:F0}%");
        AddGauge("Boron", "硼濃度", 0, 2500, () => _sim.BoronPpm, () => $"{_sim.BoronPpm:F0} ppm");
        AddGauge("Xenon worth", "氙毒", 0, 100, () => _sim.Xenon * 100, () => $"{-_sim.XenonReactivityPcm:F0} pcm");
        AddGauge("Turbine speed", "汽輪機轉速", 0, 3800, () => _sim.TurbineRPM, () => $"{_sim.TurbineRPM:F0} rpm");
    }

    private string PeriodStr()
    {
        double p = _sim.ReactorPeriodSeconds;
        if (Math.Abs(p) >= 999) return "∞";
        return $"{p:F0}s";
    }

    private void UpdateGauges()
    {
        foreach (var g in _gauges)
        {
            double v = g.Read();
            double frac = (v - g.Min) / (g.Max - g.Min);
            frac = Math.Clamp(frac, 0, 1);
            double ang = 150 + 240.0 * frac;
            DrawNeedle(g.Needle, 75, 88, 50, ang);
            g.Value.Text = g.Format();
            double warnAt = g.Min + (g.Max - g.Min) * g.WarnFrac;
            bool warn = v >= warnAt;
            ((SolidColorBrush)g.Needle.Fill).Color = warn
                ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
                : Color.FromArgb(255, 0x42, 0xA5, 0xF5);
            g.Value.Foreground = warn
                ? new SolidColorBrush(Color.FromArgb(255, 0xFF, 0x6E, 0x6E))
                : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        }
    }

    private static void DrawNeedle(Path needle, double cx, double cy, double len, double angleDeg)
    {
        double a = angleDeg * Math.PI / 180.0;
        double tipX = cx + Math.Cos(a) * len, tipY = cy + Math.Sin(a) * len;
        double bx = cx + Math.Cos(a + Math.PI / 2) * 3, by = cy + Math.Sin(a + Math.PI / 2) * 3;
        double bx2 = cx + Math.Cos(a - Math.PI / 2) * 3, by2 = cy + Math.Sin(a - Math.PI / 2) * 3;
        var fig = new PathFigure { StartPoint = new Point(tipX, tipY), IsClosed = true };
        fig.Segments.Add(new LineSegment { Point = new Point(bx, by) });
        fig.Segments.Add(new LineSegment { Point = new Point(bx2, by2) });
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        needle.Data = geo;
    }

    private static Path MakeArc(double cx, double cy, double r, double startDeg, double endDeg, Color color, double thickness)
    {
        double a0 = startDeg * Math.PI / 180.0, a1 = endDeg * Math.PI / 180.0;
        var start = new Point(cx + Math.Cos(a0) * r, cy + Math.Sin(a0) * r);
        var end = new Point(cx + Math.Cos(a1) * r, cy + Math.Sin(a1) * r);
        var fig = new PathFigure { StartPoint = start };
        fig.Segments.Add(new ArcSegment
        {
            Point = end, Size = new Size(r, r),
            IsLargeArc = (endDeg - startDeg) > 180, SweepDirection = SweepDirection.Clockwise
        });
        var geo = new PathGeometry();
        geo.Figures.Add(fig);
        return new Path { Data = geo, Stroke = new SolidColorBrush(color), StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round };
    }

    // ================================================================ ALARMS ====
    private sealed class AlarmTile
    {
        public Border Border = null!;
        public TextBlock Label = null!;
        public string En = "", Zh = "";
    }

    private void BuildAlarmTiles()
    {
        (ReactorAlarm a, string en, string zh)[] defs =
        {
            (ReactorAlarm.Scram, "SCRAM", "緊急停堆"),
            (ReactorAlarm.HighPower, "HIGH POWER", "高功率"),
            (ReactorAlarm.HighNeutronFlux, "HIGH FLUX", "高中子通量"),
            (ReactorAlarm.ShortPeriod, "SHORT PERIOD", "週期過短"),
            (ReactorAlarm.HighFuelTemp, "HIGH FUEL TEMP", "燃料高溫"),
            (ReactorAlarm.HighCoolantTemp, "HIGH COOLANT T", "冷卻劑高溫"),
            (ReactorAlarm.HighPressure, "HIGH PRESSURE", "高壓"),
            (ReactorAlarm.LowPressure, "LOW PRESSURE", "低壓"),
            (ReactorAlarm.LowFlow, "LOW RCS FLOW", "低流量"),
            (ReactorAlarm.LowPzrLevel, "LOW PZR LEVEL", "穩壓器低水位"),
            (ReactorAlarm.HighPzrLevel, "HIGH PZR LEVEL", "穩壓器高水位"),
            (ReactorAlarm.SteamPressureHigh, "HIGH STEAM P", "蒸汽高壓"),
            (ReactorAlarm.EccsActive, "ECCS ACTIVE", "應急堆芯冷卻"),
            (ReactorAlarm.TurbineTrip, "TURBINE TRIP", "汽輪機跳機"),
            (ReactorAlarm.CoreDamage, "CORE DAMAGE", "爐心受損"),
        };
        foreach (var (a, en, zh) in defs)
        {
            var label = new TextBlock { FontWeight = FontWeights.SemiBold, FontSize = 12, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.White) };
            var border = new Border
            {
                Width = 182, Height = 46, CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(40, 0x88, 0x88, 0x88)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0xAA, 0xAA, 0xAA)),
                BorderThickness = new Thickness(1),
                Child = new Viewbox { Child = label, MaxHeight = 38, Margin = new Thickness(4) },
            };
            var tile = new AlarmTile { Border = border, Label = label, En = en, Zh = zh };
            _alarmTiles[a] = tile;
            _relocalizers.Add(() => label.Text = P(en, zh));
            label.Text = P(en, zh);
            AlarmPanel.Children.Add(border);
        }
    }

    private void UpdateAlarmTiles()
    {
        bool flashOn = (int)(_flashPhase * 2) % 2 == 0;
        foreach (var kv in _alarmTiles)
        {
            bool on = _sim.Alarm(kv.Key);
            var t = kv.Value;
            if (on)
            {
                bool critical = kv.Key is ReactorAlarm.CoreDamage or ReactorAlarm.Scram or ReactorAlarm.HighFuelTemp or ReactorAlarm.HighPressure;
                Color c = critical
                    ? (flashOn ? Color.FromArgb(255, 0xD3, 0x2F, 0x2F) : Color.FromArgb(255, 0x7F, 0x1D, 0x1D))
                    : Color.FromArgb(255, 0xF5, 0x7C, 0x00);
                t.Border.Background = new SolidColorBrush(c);
                t.Border.BorderBrush = new SolidColorBrush(Colors.White);
                t.Label.Foreground = new SolidColorBrush(Colors.White);
            }
            else
            {
                t.Border.Background = new SolidColorBrush(Color.FromArgb(40, 0x88, 0x88, 0x88));
                t.Border.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0xAA, 0xAA, 0xAA));
                t.Label.Foreground = new SolidColorBrush(Color.FromArgb(160, 0xCC, 0xCC, 0xCC));
            }
        }
    }

    // ================================================================ MIMIC ====
    // Static plant diagram + dynamic colored overlays drawn each tick.
    private readonly List<UIElement> _mimicDynamic = new();

    private void DrawMimicStatic()
    {
        var c = MimicCanvas;
        c.Children.Clear();
        var stroke = new SolidColorBrush(Color.FromArgb(200, 0x90, 0x90, 0x90));

        // Reactor vessel
        AddBox(c, 40, 120, 120, 160, "Reactor\n反應堆", Color.FromArgb(40, 0x33, 0x66, 0x99));
        // Pressurizer
        AddBox(c, 200, 40, 70, 90, "PZR\n穩壓器", Color.FromArgb(40, 0x66, 0x44, 0x88));
        // Steam generator
        AddBox(c, 330, 80, 90, 200, "Steam Gen\n蒸汽發生器", Color.FromArgb(40, 0x44, 0x88, 0x66));
        // RCP pump (circle)
        AddCircle(c, 200, 200, 34, "RCP\n主泵");
        // Turbine
        AddBox(c, 560, 90, 110, 70, "Turbine\n汽輪機", Color.FromArgb(40, 0x88, 0x66, 0x33));
        // Generator
        AddCircle(c, 740, 125, 32, "Gen\n發電機");
        // Condenser
        AddBox(c, 560, 220, 110, 60, "Condenser\n冷凝器", Color.FromArgb(40, 0x33, 0x66, 0x88));

        // Primary loop pipes (will be recolored dynamically) — store references via tags through dynamic layer instead.
        // Secondary steam line
        AddPipe(c, 420, 110, 560, 110, stroke);   // SG -> turbine (steam)
        AddPipe(c, 670, 125, 708, 125, stroke);   // turbine -> generator
        AddPipe(c, 615, 160, 615, 220, stroke);   // turbine -> condenser
        AddPipe(c, 560, 250, 420, 250, stroke);   // condenser -> SG (feedwater)
        AddPipe(c, 420, 250, 420, 230, stroke);
    }

    private void UpdateMimic()
    {
        var c = MimicCanvas;
        foreach (var e in _mimicDynamic) c.Children.Remove(e);
        _mimicDynamic.Clear();

        // Primary loop hot/cold colored by temperature.
        Color hot = TempColor(_sim.Thot);
        Color cold = TempColor(_sim.Tcold);
        // vessel -> SG hot leg
        AddDynPipe(c, 160, 160, 330, 160, hot, 5);
        AddDynPipe(c, 330, 160, 330, 130, hot, 5);
        // SG -> pump -> vessel cold leg
        AddDynPipe(c, 330, 240, 234, 200, cold, 5);
        AddDynPipe(c, 166, 200, 160, 200, cold, 5);
        // vessel <-> PZR surge line
        AddDynPipe(c, 130, 120, 235, 130, TempColor(_sim.Tavg), 3);

        // Flow particles (animate when pumps running)
        double phase = (_flashPhase * 1.5) % 1.0;
        if (_sim.CoolantFlowFraction > 0.02)
        {
            DrawFlowDot(c, 160, 160, 330, 160, phase, hot);
            DrawFlowDot(c, 330, 240, 234, 200, phase, cold);
        }
        // Steam flow when steaming
        if (_sim.SteamPressure > 3.0 && _sim.TurbineLoadSetpoint > 0.02)
            DrawFlowDot(c, 420, 110, 560, 110, phase, Color.FromArgb(255, 0xCC, 0xDD, 0xEE));

        // Core glow by fuel temp
        Color core = _sim.Mode == ReactorMode.Meltdown
            ? Color.FromArgb(220, 0xFF, 0x30, 0x00)
            : TempColor(_sim.FuelTemp);
        var glow = new Ellipse { Width = 70, Height = 110, Fill = new SolidColorBrush(Color.FromArgb((byte)(80 + Math.Min(120, _sim.FuelTemp / 25)), core.R, core.G, core.B)) };
        Canvas.SetLeft(glow, 65); Canvas.SetTop(glow, 145);
        c.Children.Add(glow); _mimicDynamic.Add(glow);

        // RCP spin indicator
        bool anyPump = false; foreach (var r in _sim.RcpRunning) if (r) anyPump = true;
        var pumpDot = new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(anyPump ? Colors.LimeGreen : Colors.Gray) };
        Canvas.SetLeft(pumpDot, 195); Canvas.SetTop(pumpDot, 195);
        c.Children.Add(pumpDot); _mimicDynamic.Add(pumpDot);

        // Generator output color
        var genDot = new Ellipse { Width = 12, Height = 12, Fill = new SolidColorBrush(_sim.ElectricPowerMW > 10 ? Colors.Gold : Colors.Gray) };
        Canvas.SetLeft(genDot, 734); Canvas.SetTop(genDot, 119);
        c.Children.Add(genDot); _mimicDynamic.Add(genDot);

        // Readout labels near components
        AddDynLabel(c, 40, 285, $"{_sim.FuelTemp:F0}°C / {_sim.NeutronPowerFraction * 100:F0}%");
        AddDynLabel(c, 200, 135, $"{_sim.PrimaryPressure:F1} MPa");
        AddDynLabel(c, 330, 285, $"{_sim.SteamPressure:F1} MPa");
        AddDynLabel(c, 560, 165, $"{_sim.TurbineRPM:F0} rpm");
        AddDynLabel(c, 715, 160, $"{_sim.ElectricPowerMW:F0} MWe");
    }

    private static Color TempColor(double t)
    {
        // blue (cold) -> green -> yellow -> red (hot)
        t = Math.Clamp(t, 30, 1200);
        double f = (t - 30) / (1200 - 30);
        byte r = (byte)(Math.Clamp(f * 2, 0, 1) * 255);
        byte g = (byte)(Math.Clamp((f < 0.5 ? f * 2 : (1 - f) * 2 + 0.4), 0, 1) * 200);
        byte b = (byte)((1 - Math.Clamp(f * 1.5, 0, 1)) * 220);
        return Color.FromArgb(255, r, g, b);
    }

    private void AddBox(Canvas c, double x, double y, double w, double h, string text, Color fill)
    {
        var rect = new Rectangle { Width = w, Height = h, RadiusX = 6, RadiusY = 6, Fill = new SolidColorBrush(fill), Stroke = new SolidColorBrush(Color.FromArgb(220, 0xAA, 0xAA, 0xAA)), StrokeThickness = 1.5 };
        Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y); c.Children.Add(rect);
        var tb = new TextBlock { Text = text, FontSize = 11, TextAlignment = TextAlignment.Center, Width = w, Foreground = new SolidColorBrush(Color.FromArgb(230, 0xEE, 0xEE, 0xEE)) };
        Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y + h / 2 - 14); c.Children.Add(tb);
    }

    private void AddCircle(Canvas c, double cx, double cy, double r, string text)
    {
        var el = new Ellipse { Width = r * 2, Height = r * 2, Fill = new SolidColorBrush(Color.FromArgb(40, 0x55, 0x55, 0x55)), Stroke = new SolidColorBrush(Color.FromArgb(220, 0xAA, 0xAA, 0xAA)), StrokeThickness = 1.5 };
        Canvas.SetLeft(el, cx - r); Canvas.SetTop(el, cy - r); c.Children.Add(el);
        var tb = new TextBlock { Text = text, FontSize = 10, TextAlignment = TextAlignment.Center, Width = r * 2, Foreground = new SolidColorBrush(Color.FromArgb(230, 0xEE, 0xEE, 0xEE)) };
        Canvas.SetLeft(tb, cx - r); Canvas.SetTop(tb, cy - 12); c.Children.Add(tb);
    }

    private static void AddPipe(Canvas c, double x1, double y1, double x2, double y2, Brush stroke)
        => c.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = stroke, StrokeThickness = 3 });

    private void AddDynPipe(Canvas c, double x1, double y1, double x2, double y2, Color color, double th)
    {
        var l = new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(color), StrokeThickness = th };
        c.Children.Add(l); _mimicDynamic.Add(l);
    }

    private void DrawFlowDot(Canvas c, double x1, double y1, double x2, double y2, double t, Color color)
    {
        double x = x1 + (x2 - x1) * t, y = y1 + (y2 - y1) * t;
        var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(color) };
        Canvas.SetLeft(dot, x - 4); Canvas.SetTop(dot, y - 4);
        c.Children.Add(dot); _mimicDynamic.Add(dot);
    }

    private void AddDynLabel(Canvas c, double x, double y, string text)
    {
        var tb = new TextBlock { Text = text, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = new SolidColorBrush(Color.FromArgb(230, 0x90, 0xCA, 0xF9)) };
        Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y); c.Children.Add(tb); _mimicDynamic.Add(tb);
    }

    // ================================================================ TRENDS ====
    private void PushTrends()
    {
        void Push(Queue<double> q, double v) { q.Enqueue(v); while (q.Count > HistMax) q.Dequeue(); }
        Push(_powHist, _sim.NeutronPowerFraction * 100);
        Push(_tempHist, _sim.FuelTemp);
        Push(_pressHist, _sim.PrimaryPressure);
    }

    private void DrawTrends()
    {
        DrawTrend(TrendPower, _powHist, 0, 130, Color.FromArgb(255, 0x42, 0xA5, 0xF5));
        DrawTrend(TrendTemp, _tempHist, 0, 1500, Color.FromArgb(255, 0xFF, 0x70, 0x43));
        DrawTrend(TrendPress, _pressHist, 0, 20, Color.FromArgb(255, 0x66, 0xBB, 0x6A));
    }

    private static void DrawTrend(Canvas c, Queue<double> data, double min, double max, Color color)
    {
        c.Children.Clear();
        double w = c.Width, h = c.Height;
        // gridlines
        for (int i = 1; i < 4; i++)
        {
            double y = h * i / 4;
            c.Children.Add(new Line { X1 = 0, Y1 = y, X2 = w, Y2 = y, Stroke = new SolidColorBrush(Color.FromArgb(30, 0xFF, 0xFF, 0xFF)), StrokeThickness = 1 });
        }
        if (data.Count < 2) return;
        var pts = new PointCollection();
        var arr = data.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            double x = w * i / (HistMax - 1);
            double f = (arr[i] - min) / (max - min);
            f = Math.Clamp(f, 0, 1);
            double y = h - f * h;
            pts.Add(new Point(x, y));
        }
        c.Children.Add(new Polyline { Points = pts, Stroke = new SolidColorBrush(color), StrokeThickness = 2 });
    }

    // ================================================================ CONTROLS ====
    private void BuildControls()
    {
        var host = ControlsHost;
        host.Children.Clear();

        host.Children.Add(SectionHeader("Reactor controls · 反應堆控制", "反應堆控制 · Reactor controls"));

        // Rod banks
        for (int b = 0; b < 4; b++)
        {
            int bank = b;
            char name = (char)('A' + b);
            host.Children.Add(LabeledSlider(
                $"Control rod bank {name} (% inserted)", $"控制棒組 {name}（插入 %）",
                0, 100, _sim.RodBankInsertion[bank], 1,
                v => _sim.SetRodBank(bank, v),
                () => _sim.RodBankInsertion[bank], "%"));
        }

        // Boron
        host.Children.Add(LabeledSlider(
            "Soluble boron target (ppm) — charging ↑ / dilution ↓", "硼濃度目標（ppm）— 加硼 ↑／稀釋 ↓",
            0, 2500, _sim.TargetBoronPpm, 10,
            v => _sim.TargetBoronPpm = v, () => _sim.TargetBoronPpm, " ppm"));

        host.Children.Add(SectionHeader("Primary system · 一迴路系統", "一迴路系統 · Primary system"));

        // RCP pumps
        var pumpPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        for (int p = 0; p < 4; p++)
        {
            int pump = p;
            var tg = new ToggleButton { Content = P($"RCP {pump + 1}", $"主泵 {pump + 1}") };
            tg.Checked += (_, _) => _sim.StartRcp(pump);
            tg.Unchecked += (_, _) => _sim.StopRcp(pump);
            _relocalizers.Add(() => tg.Content = P($"RCP {pump + 1}", $"主泵 {pump + 1}"));
            pumpPanel.Children.Add(tg);
        }
        host.Children.Add(WrapLabel("Reactor coolant pumps · 反應堆冷卻劑泵", "反應堆冷卻劑泵 · Reactor coolant pumps", pumpPanel));

        host.Children.Add(LabeledSlider(
            "RCP flow demand (%)", "主泵流量需求（%）",
            0, 100, _sim.RcpFlowDemand * 100, 1,
            v => _sim.RcpFlowDemand = v / 100.0, () => _sim.RcpFlowDemand * 100, "%"));

        // Pressurizer toggles + relief + ECCS
        var pzrPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        pzrPanel.Children.Add(MakeToggle("Heater · 加熱器", "加熱器 · Heater", v => _sim.PressurizerHeater = v));
        pzrPanel.Children.Add(MakeToggle("Spray · 噴淋", "噴淋 · Spray", v => _sim.PressurizerSpray = v));
        pzrPanel.Children.Add(MakeToggle("Relief valve · 釋壓閥", "釋壓閥 · Relief valve", v => _sim.ReliefValveOpen = v));
        host.Children.Add(WrapLabel("Pressurizer & relief · 穩壓器與釋壓", "穩壓器與釋壓 · Pressurizer & relief", pzrPanel));

        var safetyPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        safetyPanel.Children.Add(MakeToggle("Arm ECCS · 啟用應急冷卻", "啟用應急冷卻 · Arm ECCS", v => _sim.EccsArmed = v));
        var injBtn = new Button { Content = P("Force ECCS inject · 強制注入", "強制注入 · Force ECCS inject") };
        injBtn.Click += (_, _) => { _sim.EccsArmed = true; };
        _relocalizers.Add(() => injBtn.Content = P("Force ECCS inject · 強制注入", "強制注入 · Force ECCS inject"));
        safetyPanel.Children.Add(injBtn);
        host.Children.Add(WrapLabel("Safety injection · 安全注入", "安全注入 · Safety injection", safetyPanel));

        host.Children.Add(SectionHeader("Secondary & turbine · 二迴路與汽輪機", "二迴路與汽輪機 · Secondary & turbine"));

        host.Children.Add(LabeledSlider(
            "Feedwater flow (%)", "給水流量（%）",
            0, 100, _sim.FeedwaterFlow * 100, 1,
            v => _sim.FeedwaterFlow = v / 100.0, () => _sim.FeedwaterFlow * 100, "%"));
        host.Children.Add(LabeledSlider(
            "Turbine load setpoint (%)", "汽輪機負載設定（%）",
            0, 100, _sim.TurbineLoadSetpoint * 100, 1,
            v => _sim.TurbineLoadSetpoint = v / 100.0, () => _sim.TurbineLoadSetpoint * 100, "%"));

        var grdPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        grdPanel.Children.Add(MakeToggle("Generator breaker · 發電機開關", "發電機開關 · Generator breaker", v => _sim.GeneratorBreakerClosed = v));
        host.Children.Add(WrapLabel("Grid synchronization · 併網", "併網 · Grid synchronization", grdPanel));

        host.Children.Add(SectionHeader("Mode & automation · 模式與自動化", "模式與自動化 · Mode & automation"));

        // Mode selector
        var modeCombo = new ComboBox { MinWidth = 220 };
        modeCombo.Items.Add(P("Shutdown · 停機", "停機 · Shutdown"));
        modeCombo.Items.Add(P("Startup · 啟動", "啟動 · Startup"));
        modeCombo.Items.Add(P("Run · 運轉", "運轉 · Run"));
        modeCombo.SelectedIndex = 0;
        modeCombo.SelectionChanged += (_, _) =>
        {
            _sim.SetMode(modeCombo.SelectedIndex switch { 1 => ReactorMode.Startup, 2 => ReactorMode.Run, _ => ReactorMode.Shutdown });
        };
        host.Children.Add(WrapLabel("Reactor mode · 反應堆模式", "反應堆模式 · Reactor mode", modeCombo));

        host.Children.Add(LabeledSlider(
            "Auto power setpoint (%)", "自動功率設定（%）",
            0, 110, _sim.AutoPowerSetpoint * 100, 1,
            v => _sim.AutoPowerSetpoint = v / 100.0, () => _sim.AutoPowerSetpoint * 100, "%"));

        host.Children.Add(SectionHeader("⚠ Real shutdown on meltdown · 熔毀時真實關機", "⚠ 熔毀時真實關機 · Real shutdown on meltdown"));

        var armToggle = new ToggleSwitch
        {
            Header = P("ARM REAL SHUTDOWN ON MELTDOWN · 啟用熔毀時真實關機", "啟用熔毀時真實關機 · ARM REAL SHUTDOWN ON MELTDOWN"),
            IsOn = false, // DEFAULT OFF
            OnContent = P("Armed", "已啟用"),
            OffContent = P("Safe (off)", "安全（關）"),
        };
        armToggle.Toggled += (_, _) => _armRealShutdown = armToggle.IsOn;
        _relocalizers.Add(() =>
        {
            armToggle.Header = P("ARM REAL SHUTDOWN ON MELTDOWN · 啟用熔毀時真實關機", "啟用熔毀時真實關機 · ARM REAL SHUTDOWN ON MELTDOWN");
            armToggle.OnContent = P("Armed", "已啟用");
            armToggle.OffContent = P("Safe (off)", "安全（關）");
        });
        host.Children.Add(armToggle);

        var warn = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xB3, 0x00)),
        };
        _relocalizers.Add(() => warn.Text = P(
            "⚠ WARNING: When ON, a meltdown starts a 10-second abortable countdown and then REALLY shuts down this PC (normal shutdown via Win32 API — unsaved work in other apps could be lost). Default is OFF: meltdown only shows a simulated screen and never powers off your PC.",
            "⚠ 警告：開啟後，熔毀會開始 10 秒可中止倒數，然後真實關閉呢部電腦（用 Win32 API 嘅正常關機 — 其他程式未儲存嘅工作可能會遺失）。預設為關閉：熔毀只會顯示模擬畫面，唔會關機。"));
        warn.Text = P(
            "⚠ WARNING: When ON, a meltdown starts a 10-second abortable countdown and then REALLY shuts down this PC (normal shutdown via Win32 API — unsaved work in other apps could be lost). Default is OFF: meltdown only shows a simulated screen and never powers off your PC.",
            "⚠ 警告：開啟後，熔毀會開始 10 秒可中止倒數，然後真實關閉呢部電腦（用 Win32 API 嘅正常關機 — 其他程式未儲存嘅工作可能會遺失）。預設為關閉：熔毀只會顯示模擬畫面，唔會關機。");
        host.Children.Add(warn);

        ResetTripButton.IsEnabled = true;
    }

    private void UpdateControlsLive()
    {
        // Reflect auto-control rod motion back into nothing heavy; sliders are not two-way bound to
        // avoid feedback loops. (Auto control changes engine state directly.)
    }

    private TextBlock SectionHeader(string en, string zh)
    {
        var tb = new TextBlock { FontWeight = FontWeights.Bold, FontSize = 15, Margin = new Thickness(0, 6, 0, 0) };
        _relocalizers.Add(() => tb.Text = P(en, zh));
        tb.Text = P(en, zh);
        return tb;
    }

    private FrameworkElement LabeledSlider(string en, string zh, double min, double max, double init, double step,
        Action<double> set, Func<double> read, string unit)
    {
        var label = new TextBlock { FontSize = 12 };
        var slider = new Slider { Minimum = min, Maximum = max, Value = init, StepFrequency = step, Width = 380 };
        void Upd() => label.Text = P(en, zh) + $"  ·  {read():F0}{unit}";
        slider.ValueChanged += (_, ev) => { set(ev.NewValue); Upd(); };
        _relocalizers.Add(Upd);
        Upd();
        return new StackPanel { Spacing = 2, Children = { label, slider } };
    }

    private FrameworkElement WrapLabel(string en, string zh, UIElement content)
    {
        var label = new TextBlock { FontSize = 12 };
        _relocalizers.Add(() => label.Text = P(en, zh));
        label.Text = P(en, zh);
        return new StackPanel { Spacing = 4, Children = { label, content } };
    }

    private ToggleButton MakeToggle(string en, string zh, Action<bool> set)
    {
        var tg = new ToggleButton { Content = P(en, zh) };
        tg.Checked += (_, _) => set(true);
        tg.Unchecked += (_, _) => set(false);
        _relocalizers.Add(() => tg.Content = P(en, zh));
        return tg;
    }

    // ================================================================ buttons ====
    private void Scram_Click(object sender, RoutedEventArgs e) => _sim.Scram();

    private void ResetTrip_Click(object sender, RoutedEventArgs e)
    {
        if (_sim.Mode == ReactorMode.Meltdown) return;
        _sim.ResetTrip();
    }

    private void AutoRun_Toggled(object sender, RoutedEventArgs e)
    {
        _sim.AutoRodControl = AutoRunToggle.IsOn;
    }

    // ================================================================ MELTDOWN ====
    private void OnMeltdown()
    {
        if (_meltdownHandled) return;
        _meltdownHandled = true;
        DispatcherQueue.TryEnqueue(() =>
        {
            MeltdownOverlay.Visibility = Visibility.Visible;
            MeltdownSub.Text = P(
                "Fuel temperature exceeded structural limits. Fission products released. This is a SIMULATION.",
                "燃料溫度超出結構極限，裂變產物已釋放。呢個係模擬。");

            if (_armRealShutdown)
            {
                // SAFETY GATE: abortable 10-second countdown before any real shutdown.
                StartShutdownCountdown();
            }
            else
            {
                // Default-off path: simulated only. Never powers off the PC.
                CountdownBox.Visibility = Visibility.Collapsed;
                MeltdownCloseButton.Visibility = Visibility.Visible;
                MeltdownSub.Text += P(
                    "\n\nReal shutdown is OFF — your PC is safe.",
                    "\n\n真實關機已關閉 — 你部電腦安全。");
            }
        });
    }

    private void StartShutdownCountdown()
    {
        _aborted = false;
        _shutdownIssued = false;
        _countdownRemaining = 10;
        CountdownBox.Visibility = Visibility.Visible;
        MeltdownCloseButton.Visibility = Visibility.Collapsed;
        CountdownText.Text = P(
            "REAL PC SHUTDOWN ARMED. This computer will shut down (normal, apps can save) when the timer reaches zero. Press ABORT to cancel.",
            "已啟用真實關機。倒數到零時呢部電腦會關機（正常關機，程式可儲存）。揿「中止」可取消。");
        CountdownNumber.Text = _countdownRemaining.ToString();

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            if (_aborted) { _countdownTimer!.Stop(); return; }
            _countdownRemaining--;
            CountdownNumber.Text = Math.Max(0, _countdownRemaining).ToString();
            if (_countdownRemaining <= 0)
            {
                _countdownTimer!.Stop();
                if (!_aborted && !_shutdownIssued)
                {
                    _shutdownIssued = true;
                    string msg = "WinForge nuclear reactor simulation: meltdown — initiating shutdown.";
                    bool ok = ReactorSimService.InitiateRealShutdown(msg);
                    CountdownText.Text = ok
                        ? P("Shutdown initiated by the operating system…", "作業系統已開始關機…")
                        : P("Shutdown request was refused by Windows (insufficient privilege?).", "Windows 拒絕咗關機要求（權限不足？）。");
                    CountdownNumber.Text = "0";
                    if (!ok) MeltdownCloseButton.Visibility = Visibility.Visible;
                }
            }
        };
        _countdownTimer.Start();
    }

    private void Abort_Click(object sender, RoutedEventArgs e)
    {
        _aborted = true;
        _countdownTimer?.Stop();
        CountdownText.Text = P("Shutdown ABORTED. Your PC is safe.", "已中止關機。你部電腦安全。");
        CountdownNumber.Text = "✓";
        MeltdownCloseButton.Visibility = Visibility.Visible;
    }

    private void MeltdownClose_Click(object sender, RoutedEventArgs e)
    {
        // Reset the whole simulation to a clean cold-shutdown state.
        MeltdownOverlay.Visibility = Visibility.Collapsed;
        ResetSimulation();
    }

    private void AnimateMeltdown(double dt)
    {
        // Pulsing klaxon icon + flashing background.
        double pulse = 0.5 + 0.5 * Math.Sin(_flashPhase * 6);
        KlaxonIcon.Opacity = 0.3 + 0.7 * pulse;
        MeltdownTitle.Opacity = 0.6 + 0.4 * pulse;
    }

    private void ResetSimulation()
    {
        _meltdownHandled = false;
        _shutdownIssued = false;
        _aborted = false;
        _countdownTimer?.Stop();
        _powHist.Clear(); _tempHist.Clear(); _pressHist.Clear();
        _simClock = 0;
        _sim.Reset();
        // Rebuild the control surface so toggles/sliders reflect the reset engine state.
        _relocalizers.Clear();
        BuildControls();
        AutoRunToggle.IsOn = false;
        Render();
    }
}
