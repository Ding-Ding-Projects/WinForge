using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using WinForge.Controls;
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

    // Strip-chart recorders (replace the old static trend polylines).
    private StripChartRecorder? _stripPower, _stripTemp, _stripPress;

    // 1/M approach-to-criticality history.
    private readonly Queue<double> _oneOverMHist = new();
    private const int HistMax = 150;

    // Gauge registry (built once; updated each tick).
    private readonly List<GaugeView> _gauges = new();
    // Alarm tile registry.
    private readonly Dictionary<ReactorAlarm, AlarmTile> _alarmTiles = new();
    // Live rod-position / insertion-limit readout under the rod-bank sliders.
    private TextBlock? _rodStatusText;
    // CSF (critical safety function) cells: S,C,H,P,Z,I.
    private readonly List<(Border border, TextBlock label, string en, string zh, Func<int> sev)> _csfCells = new();
    // RPS function tiles: one per protection function, each owning its border, status text + channel LEDs.
    private sealed class RpsTile
    {
        public RpsFunction Fn = null!;
        public Border Border = null!;
        public TextBlock Name = null!;
        public TextBlock Status = null!;
        public Microsoft.UI.Xaml.Shapes.Ellipse[] Leds = Array.Empty<Microsoft.UI.Xaml.Shapes.Ellipse>();
    }
    private readonly List<RpsTile> _rpsTiles = new();
    // Permissive indicator pills P-6…P-10.
    private readonly List<(Border border, TextBlock label, Func<bool> on)> _permPills = new();

    // Klaxon flash phase.
    private double _flashPhase;

    // Composition FX state.
    private SpriteVisual? _glow;
    private ContainerVisual? _fxRoot;
    private Compositor? _compositor;
    private CompositionPropertySet? _fxProps;
    private ReactorFx.SteamPool? _steam;
    private readonly ReactorFx.RenderClock _renderClock = new();
    private Visual? _scrollVisual;
    private bool _meltdownFxStarted;

    // Audio.
    private bool _audioStarted;
    private bool _lastScram;
    private bool _alarmsAcked;
    private bool _silenced;

    // Keep-awake (real OS) state driven by the simulated generator output.
    // 由模擬發電機輸出驅動嘅真實作業系統保持喚醒狀態。
    private bool _keepingAwake;                 // tracks the live OS hold so calls stay idempotent
    private bool _keepAwakeEnabled = true;      // user toggle; DEFAULT ON
    private const double KeepAwakeMinMWe = 1.0;  // generator must deliver > 1 MWe to the grid

    // Cached control text blocks needing re-localization.
    private readonly List<Action> _relocalizers = new();

    public ReactorModule()
    {
        InitializeComponent();
        _sim.MeltdownOccurred += OnMeltdown;
        Loc.I.LanguageChanged += OnLanguageChanged;

        Loaded += async (_, _) =>
        {
            BuildControls();
            BuildGauges();
            BuildAlarmTiles();
            BuildStripCharts();
            BuildCsfPanel();
            BuildRpsPanel();
            BuildScenarioCombo();
            DrawMimicStatic();
            InitFx();
            Render();
            _last = DateTime.UtcNow;
            _timer.Tick += Tick;
            _timer.Start();

            // Audio: lazily start the AudioGraph, then begin the ambient hum (respects mute).
            try
            {
                await ReactorAudioEngine.I.EnsureStartedAsync();
                _audioStarted = true;
                if (ReactorAudioEngine.I.Enabled) ReactorAudioEngine.I.Hum(true);
            }
            catch { /* degrade silently */ }
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _timer.Tick -= Tick;
            _countdownTimer?.Stop();
            _renderClock.Stop();
            Loc.I.LanguageChanged -= OnLanguageChanged;
            // Stop the synthesized voices but keep the graph alive (singleton, reused by the windows).
            try { ReactorAudioEngine.I.StopVoices(); } catch { }
            // SAFETY: always release the keep-awake hold when leaving the page so navigating
            // away never leaves the system pinned awake. 離開頁面時務必釋放保持喚醒。
            ReleaseKeepAwake();
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
        HeaderTitle.Text = "★ Nuclear Reactor · 核反應堆";
        HeaderBlurb.Text = P(
            "WinForge's flagship: a hyper-realistic PWR control room — point-kinetics + thermal-hydraulics, Cherenkov core glow, synthesized control-room audio, strip-chart recorders, NIS/SPDS panels, accident scenarios, a dedicated control-room window and desktop widgets. Educational simulation only — controls nothing real.",
            "WinForge 旗艦：超寫實壓水堆控制室 — 點堆動力學＋熱工水力、切連科夫核芯輝光、合成控制室音效、趨勢記錄儀、NIS／SPDS 面板、事故情景、獨立控制室視窗同桌面小工具。純教育模擬 — 唔會控制任何真實硬件。");
        // toolbar
        OpenControlRoomButton.Content = P("Open full control room ⤢", "開啟完整控制室 ⤢");
        OpenWidgetsButton.Content = P("Mini widgets", "桌面小工具");
        MuteToggle.Content = P("Mute audio", "靜音");
        MuteToggle.IsChecked = !ReactorAudioEngine.I.Enabled;
        ScenarioLabel.Text = P("Scenario:", "情景：");
        IsolateSgToggle.Content = P("Isolate affected SG", "隔離受影響蒸發器");
        NisTitle.Text = P("Nuclear instrumentation (NIS) & critical safety functions (SPDS)", "核儀表（NIS）與關鍵安全功能（SPDS）");
        NisLabel.Text = P("NIS — source / intermediate / power range", "核儀表 — 起動／中間／功率量程");
        OneOverMLabel.Text = P("1/M — approach to criticality", "1/M — 趨近臨界");
        CsfTitle.Text = P("Critical safety functions", "關鍵安全功能");
        AckButton.Content = P("ACK", "確認");
        SilenceButton.Content = P("SILENCE", "靜音警報");
        ResetAlarmButton.Content = P("RESET", "重置");
        LampTestButton.Content = P("LAMP TEST", "燈測");
        KeepAwakeToggle.Header = P("Keep PC awake while generating · 發電時保持電腦喚醒", "發電時保持電腦喚醒 · Keep PC awake while generating");
        KeepAwakeToggle.OnContent = P("On", "開");
        KeepAwakeToggle.OffContent = P("Off", "關");
        MimicTitle.Text = P("Plant Mimic Diagram · 機組流程圖", "機組流程圖 · Plant Mimic Diagram");
        RpsTitle.Text = P("Reactor Protection System · 反應堆保護系統", "反應堆保護系統 · Reactor Protection System");
        RpsSubtitle.Text = P(
            "4-channel 2-of-4 coincidence logic with Westinghouse trip setpoints. A single tripped channel is a partial trip (amber) — the reactor trips only when ≥2 of 4 channels of a function trip. Permissives P-6/P-7/P-8/P-9/P-10 block low-power trips.",
            "四通道四取二符合邏輯，採用西屋跳脫定值。單一通道跳脫只屬部分跳脫（琥珀色）— 須同一功能四取二（≥2 通道）方會觸發停堆。允許訊號 P-6／P-7／P-8／P-9／P-10 會封鎖低功率跳脫。");
        GaugesTitle.Text = P("Instrument Gauges · 儀表", "儀表 · Instrument Gauges");
        TrendTitle.Text = P("Strip-Chart Recorders · 趨勢記錄儀", "趨勢記錄儀 · Strip-Chart Recorders");
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

        UpdateKeepAwake();
        UpdateStatusBanner();
        UpdateGauges();
        UpdateAlarmTiles();
        UpdateMimic();
        UpdateStripCharts();
        UpdateNisPanels();
        UpdateCsfPanel();
        UpdateRpsPanel();
        UpdateAudio();
        UpdateControlsLive();

        if (_sim.Mode == ReactorMode.Meltdown)
            AnimateMeltdown(dt);
    }

    // ============================================================ audio ====
    private void UpdateAudio()
    {
        if (!_audioStarted) return;
        var a = ReactorAudioEngine.I;
        a.Power = (float)_sim.NeutronPowerFraction;
        a.Scram = _sim.IsScrammed;
        a.Meltdown = _sim.Mode == ReactorMode.Meltdown;

        bool enabled = a.Enabled;
        a.Hum(enabled);
        // Klaxon while scrammed (unless silenced); annunciator buzzer while any alarm active & unacked.
        a.Klaxon(enabled && _sim.IsScrammed && !_silenced);
        a.Buzzer(enabled && AnyAlarm() && !_alarmsAcked && !_silenced && !_sim.IsScrammed);
        a.EvacTone(enabled && _sim.Mode == ReactorMode.Meltdown);

        // Relay click on fresh SCRAM edge.
        if (_sim.IsScrammed && !_lastScram) { a.RelayClick(); _silenced = false; _alarmsAcked = false; }
        _lastScram = _sim.IsScrammed;
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

    // ============================================================ keep-awake ====
    // The SIMULATED generator output drives a REAL OS keep-awake (Win32 SetThreadExecutionState
    // via AwakeService). When the generator breaker is CLOSED and electrical output exceeds a
    // small threshold (the plant is actually delivering power to the grid) we tell Windows the
    // system + display are required. Otherwise — output ~0, SCRAM, turbine trip, breaker open,
    // reactor offline or meltdown — we release the hold so the PC may sleep normally.
    //
    // 模擬發電機輸出驅動真實作業系統保持喚醒：當發電機開關閉合且電功率超過細小門檻（真正向電網供電）時，
    // 通知 Windows 需要保持系統與顯示器；否則（輸出近零、SCRAM、汽輪機跳機、開關斷開、反應堆停機或熔毀）
    // 釋放鎖定，令電腦可正常睡眠。
    private void UpdateKeepAwake()
    {
        // Generator is delivering real power to the grid only when synchronized (breaker closed),
        // not tripped/melted down, and actually producing more than the small threshold.
        bool generating =
            _keepAwakeEnabled
            && _sim.GeneratorBreakerClosed
            && _sim.ElectricPowerMW > KeepAwakeMinMWe
            && !_sim.IsScrammed
            && _sim.Mode != ReactorMode.Meltdown
            && _sim.Mode != ReactorMode.Tripped;

        // Idempotent: only touch the OS state when it actually changes between ticks.
        if (generating && !_keepingAwake)
        {
            if (AwakeService.KeepAwake(keepDisplay: true)) _keepingAwake = true;
        }
        else if (!generating && _keepingAwake)
        {
            ReleaseKeepAwake();
        }

        UpdateKeepAwakePill(generating);
    }

    private void ReleaseKeepAwake()
    {
        if (!_keepingAwake) return;
        AwakeService.AllowSleep();
        _keepingAwake = false;
    }

    private void UpdateKeepAwakePill(bool generating)
    {
        if (generating)
        {
            int mwe = (int)Math.Round(_sim.ElectricPowerMW);
            KeepAwakeText.Text = P(
                $"⚡ Grid online — this PC is kept awake ({mwe} MWe) · 電網供電中 — 正供電令此電腦保持喚醒（{mwe} MWe）",
                $"⚡ 電網供電中 — 正供電令此電腦保持喚醒（{mwe} MWe） · Grid online — this PC is kept awake ({mwe} MWe)");
            KeepAwakeDot.Background = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xD7, 0x00)); // gold
        }
        else
        {
            string suffix = _keepAwakeEnabled ? "" : P(" (keep-awake disabled)", "（保持喚醒已停用）");
            KeepAwakeText.Text = P(
                "Generator offline — normal sleep allowed · 發電機離線 — 正常允許睡眠",
                "發電機離線 — 正常允許睡眠 · Generator offline — normal sleep allowed") + suffix;
            KeepAwakeDot.Background = new SolidColorBrush(Color.FromArgb(255, 0x75, 0x75, 0x75)); // grey
        }
    }

    private void KeepAwakeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        _keepAwakeEnabled = KeepAwakeToggle.IsOn;
        // When turned OFF mid-generation, release the real hold immediately (sim keeps running).
        if (!_keepAwakeEnabled) ReleaseKeepAwake();
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

    private void AddGauge(string en, string zh, double min, double max, Func<double> read, Func<string> fmt, double warnFrac = 1.1, string? id = null)
    {
        const double w = 150, h = 150, cx = 75, cy = 88, r = 56;
        var canvas = new Canvas { Width = w, Height = h };

        // dial arc background (240° sweep from 150° to 30° going through bottom)
        var arc = MakeArc(cx, cy, r, 150, 390, Color.FromArgb(90, 0x88, 0x88, 0x88), 8);
        canvas.Children.Add(arc);

        // coloured limit bands + setpoint ticks from the engineering spec.
        if (id is not null)
        {
            var (sMin, sMax, bands, sets) = ReactorScenarios.Spec(id);
            if (sMax > sMin)
            {
                foreach (var b in bands)
                {
                    double f0 = Math.Clamp((b.Lo - sMin) / (sMax - sMin), 0, 1);
                    double f1 = Math.Clamp((b.Hi - sMin) / (sMax - sMin), 0, 1);
                    if (f1 <= f0) continue;
                    Color bc = b.Kind switch
                    {
                        "danger" => Color.FromArgb(200, 0xE5, 0x39, 0x35),
                        "warn" => Color.FromArgb(200, 0xFB, 0xC0, 0x2D),
                        _ => Color.FromArgb(160, 0x4C, 0xAF, 0x50),
                    };
                    canvas.Children.Add(MakeArc(cx, cy, r, 150 + 240 * f0, 150 + 240 * f1, bc, 4));
                }
                foreach (var sp in sets)
                {
                    double f = Math.Clamp((sp.V - sMin) / (sMax - sMin), 0, 1);
                    double ang = (150 + 240 * f) * Math.PI / 180.0;
                    double x1 = cx + Math.Cos(ang) * (r + 1), y1 = cy + Math.Sin(ang) * (r + 1);
                    double x2 = cx + Math.Cos(ang) * (r + 7), y2 = cy + Math.Sin(ang) * (r + 7);
                    canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, StrokeThickness = 2, Stroke = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0x52, 0x52)) });
                }
            }
        }

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
        AddGauge("Reactor power", "反應堆功率", 0, 120, () => _sim.NeutronPowerFraction * 100, () => $"{_sim.NeutronPowerFraction * 100:F1}%", id: "power");
        AddGauge("Thermal power", "熱功率", 0, ReactorSimService.RatedThermalMW * 1.2, () => _sim.ThermalPowerMW, () => $"{_sim.ThermalPowerMW:F0} MWt");
        AddGauge("Electrical output", "電功率", 0, ReactorSimService.RatedElectricMW * 1.1, () => _sim.ElectricPowerMW, () => $"{_sim.ElectricPowerMW:F0} MWe");
        AddGauge("Decay heat", "衰變熱", 0, 8, () => _sim.DecayHeatFraction * 100, () => $"{_sim.DecayHeatFraction * 100:F1}%");
        AddGauge("Reactor period", "反應堆週期", 0, 100, () => Math.Min(100, Math.Abs(_sim.ReactorPeriodSeconds)), () => PeriodStr(), id: "period");
        AddGauge("Reactivity", "反應性", -2000, 2000, () => _sim.ReactivityPcm, () => $"{_sim.ReactivityPcm:F0} pcm");
        AddGauge("Fuel temp", "燃料溫度", 0, 3000, () => _sim.FuelTemp, () => $"{_sim.FuelTemp:F0}°C", warnFrac: ReactorSimService.FuelDamageTemp / 3000.0, id: "fuelTemp");
        AddGauge("Coolant Tavg", "冷卻劑平均溫", 530, 620, () => _sim.Tavg * 1.8 + 32, () => $"{_sim.Tavg * 1.8 + 32:F0}°F", id: "tavg");
        AddGauge("Coolant Thot", "熱腿溫度", 530, 660, () => _sim.Thot * 1.8 + 32, () => $"{_sim.Thot * 1.8 + 32:F0}°F", id: "thot");
        AddGauge("Coolant Tcold", "冷腿溫度", 520, 600, () => _sim.Tcold * 1.8 + 32, () => $"{_sim.Tcold * 1.8 + 32:F0}°F", id: "tcold");
        AddGauge("Subcooling", "過冷度", -20, 120, () => _sim.SubcoolingMarginC, () => $"{_sim.SubcoolingMarginC:F0}°C", id: "subcool");
        AddGauge("Primary pressure", "一迴路壓力", 0, 3000, () => _sim.PrimaryPressure * 145.038, () => $"{_sim.PrimaryPressure * 145.038:F0} psia", id: "pzrPress");
        AddGauge("Pressurizer level", "穩壓器水位", 0, 100, () => _sim.PressurizerLevel, () => $"{_sim.PressurizerLevel:F0}%", id: "pzrLevel");
        AddGauge("Pressurizer temp", "穩壓器溫度", 80, 700, () => _sim.PressurizerLiquidTemp * 1.8 + 32, () => $"{_sim.PressurizerLiquidTemp * 1.8 + 32:F0}°F", id: "pzrTemp");
        AddGauge("Steam pressure", "蒸汽壓力", 0, 1300, () => _sim.SteamPressure * 145.038, () => $"{_sim.SteamPressure * 145.038:F0} psia", id: "sgPress");
        AddGauge("SG level", "蒸發器水位", 0, 100, () => _sim.SteamGenLevel, () => $"{_sim.SteamGenLevel:F0}%", id: "sgLevel");
        AddGauge("Secondary radiation", "二次側輻射", 0, 300, () => _sim.SecondaryRadiation, () => $"{_sim.SecondaryRadiation:F0} µSv/h", warnFrac: 100.0 / 300.0, id: "secRad");
        AddGauge("Atmospheric release", "累計大氣排放", 0, 100, () => _sim.AtmosphericRelease * 10, () => $"{_sim.AtmosphericRelease:F2}", id: "atmRel");
        AddGauge("RCP flow", "主泵流量", 0, 100, () => _sim.CoolantFlowFraction * 100, () => $"{_sim.CoolantFlowFraction * 100:F0}%", id: "flow");
        AddGauge("Boron", "硼濃度", 0, 2500, () => _sim.BoronPpm, () => $"{_sim.BoronPpm:F0} ppm", id: "boron");
        AddGauge("Xenon worth", "氙毒", 0, 100, () => _sim.Xenon * 100, () => $"{-_sim.XenonReactivityPcm:F0} pcm", id: "xenon");
        AddGauge("Turbine speed", "汽輪機轉速", 0, 2000, () => _sim.TurbineRPM, () => $"{_sim.TurbineRPM:F0} rpm");
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
            (ReactorAlarm.LowSubcooling, "LOW SUBCOOLING", "過冷度不足"),
            (ReactorAlarm.DecayHeatHigh, "DECAY HEAT", "衰變熱高"),
            (ReactorAlarm.AtwsActive, "ATWS — RODS STUCK", "ATWS 控制棒卡住"),
            (ReactorAlarm.AccumulatorInject, "ACCUM INJECT", "蓄壓器注入"),
            (ReactorAlarm.AuxFeedwater, "AUX FEEDWATER", "輔助給水"),
            (ReactorAlarm.NaturalCirc, "NATURAL CIRC", "自然循環"),
            (ReactorAlarm.SgtrLeak, "SGTR LEAK", "蒸發器爆管洩漏"),
            (ReactorAlarm.SecondaryRadiationHi, "2NDARY RAD HI", "二次側輻射高"),
            (ReactorAlarm.SgReliefLift, "SG RELIEF — RELEASE", "蒸發器釋壓閥洩放"),
            (ReactorAlarm.RodInsertionLimitLo, "ROD INS LIMIT LO", "控制棒插入限值 低"),
            (ReactorAlarm.RodInsertionLimitLoLo, "ROD INS LIMIT LO-LO", "控制棒插入限值 低低"),
            (ReactorAlarm.RodDeviation, "ROD DEVIATION", "控制棒偏差"),
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

    // ================================================================ STRIP CHARTS ====
    private void BuildStripCharts()
    {
        StripChartHost.Children.Clear();
        var (pMin, pMax, _, pSet) = ReactorScenarios.Spec("power");
        _stripPower = new StripChartRecorder(300, 120);
        _stripPower.SetPens(new StripChartRecorder.Pen
        {
            En = "Power (%)", Zh = "功率（%）",
            Color = Color.FromArgb(255, 0x42, 0xA5, 0xF5),
            Min = pMin, Max = pMax,
            Redline = pSet.Length > 0 ? pSet[0].V : double.NaN,
            Read = () => _sim.NeutronPowerFraction * 100,
        });

        var (fMin, fMax, _, fSet) = ReactorScenarios.Spec("fuelTemp");
        _stripTemp = new StripChartRecorder(300, 120);
        _stripTemp.SetPens(new StripChartRecorder.Pen
        {
            En = "Fuel T (°C)", Zh = "燃料溫（°C）",
            Color = Color.FromArgb(255, 0xFF, 0x70, 0x43),
            Min = 0, Max = 1500,
            Redline = fSet.Length > 0 ? fSet[0].V : double.NaN,
            Read = () => _sim.FuelTemp,
        });

        var (prMin, prMax, _, prSet) = ReactorScenarios.Spec("pzrPress");
        _stripPress = new StripChartRecorder(300, 120);
        _stripPress.SetPens(new StripChartRecorder.Pen
        {
            En = "Primary (psia)", Zh = "一迴路（psia）",
            Color = Color.FromArgb(255, 0x66, 0xBB, 0x6A),
            Min = prMin, Max = prMax,
            Redline = 2485,
            Read = () => _sim.PrimaryPressure * 145.038,
        });

        StripChartHost.Children.Add(_stripPower);
        StripChartHost.Children.Add(_stripTemp);
        StripChartHost.Children.Add(_stripPress);
        _relocalizers.Add(() => { _stripPower?.Relocalize(); _stripTemp?.Relocalize(); _stripPress?.Relocalize(); });
    }

    private void UpdateStripCharts()
    {
        _stripPower?.Sample();
        _stripTemp?.Sample();
        _stripPress?.Sample();
    }

    // ================================================================ NIS / 1-over-M ====
    private void UpdateNisPanels()
    {
        // NIS three-range meter: each bar is filled from the REAL calibrated instrument output, not a
        // single heuristic — SRM by log count rate (1..1e6 cps), IRM by IR decades (1e-11..1e-3 A),
        // PRM by linear % rated power (0..120 %). The three windows overlap as the real NIS does.
        var c = NisCanvas;
        c.Children.Clear();
        double w = c.Width, h = c.Height;
        double srFrac = Math.Clamp(Math.Log10(Math.Max(_sim.SourceRangeCps, 1.0)) / 6.0, 0, 1); // 1..1e6 cps
        double irFrac = Math.Clamp(_sim.IntermediateRangeDecades / 8.0, 0, 1);                  // 0..8 decades
        double prFrac = Math.Clamp(_sim.PowerRangePercent / 120.0, 0, 1);                        // 0..120 %
        // SR bar dims when its detectors are de-energized (HV removed above P-6 / P-10).
        var srColor = _sim.SourceRangeEnergized
            ? Color.FromArgb(255, 0x42, 0xA5, 0xF5) : Color.FromArgb(120, 0x42, 0xA5, 0xF5);
        DrawNisBar(c, 20, "SRM", srFrac, srColor);
        DrawNisBar(c, 70, "IRM", irFrac, Color.FromArgb(255, 0x66, 0xBB, 0x6A));
        DrawNisBar(c, 120, "PRM", prFrac, Color.FromArgb(255, 0xFF, 0xB3, 0x00));

        // Engineering readout: actual SR cps / IR amps / PR %, plus startup rate and SR HV state.
        var readout = new TextBlock
        {
            Text = $"SR {_sim.SourceRangeCps:0.0E+0} cps   IR {_sim.IntermediateRangeAmps:0.0E+0} A   PR {_sim.PowerRangePercent:F1} %",
            FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(230, 0xB0, 0xBE, 0xC5)),
        };
        Canvas.SetLeft(readout, 170); Canvas.SetTop(readout, h - 44);
        c.Children.Add(readout);
        string srState = _sim.SourceRangeEnergized
            ? P("SR energized", "起動範圍通電") : P("SR de-energized (P-6/P-10)", "起動範圍斷電（P-6／P-10）");
        var dpm = new TextBlock
        {
            Text = $"SUR {_sim.StartupRateDpm:F1} DPM   T {PeriodStr()}   {srState}",
            FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromArgb(230, 0x90, 0xCA, 0xF9)),
        };
        Canvas.SetLeft(dpm, 170); Canvas.SetTop(dpm, h - 24);
        c.Children.Add(dpm);

        // 1/M plot — pushes down toward zero as the core approaches criticality.
        _oneOverMHist.Enqueue(_sim.OneOverM);
        while (_oneOverMHist.Count > HistMax) _oneOverMHist.Dequeue();
        var oc = OneOverMCanvas;
        oc.Children.Clear();
        double ow = oc.Width, oh = oc.Height;
        oc.Children.Add(new Line { X1 = 0, Y1 = oh - 2, X2 = ow, Y2 = oh - 2, Stroke = new SolidColorBrush(Color.FromArgb(120, 0xFF, 0x52, 0x52)), StrokeThickness = 1 });
        var pts = new PointCollection();
        var arr = _oneOverMHist.ToArray();
        for (int i = 0; i < arr.Length; i++)
        {
            double x = ow * i / (HistMax - 1);
            double y = oh - Math.Clamp(arr[i], 0, 1) * oh;
            pts.Add(new Point(x, y));
        }
        if (pts.Count >= 2)
            oc.Children.Add(new Polyline { Points = pts, Stroke = new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xAF, 0x50)), StrokeThickness = 2 });
    }

    private static void DrawNisBar(Canvas c, double x, string label, double frac, Color color)
    {
        double h = c.Height, barH = h - 30;
        c.Children.Add(new Rectangle { Width = 36, Height = barH, Fill = new SolidColorBrush(Color.FromArgb(40, 0xFF, 0xFF, 0xFF)) });
        Canvas.SetLeft(c.Children[c.Children.Count - 1], x); Canvas.SetTop(c.Children[c.Children.Count - 1], 6);
        double fh = Math.Clamp(frac, 0, 1) * barH;
        var fill = new Rectangle { Width = 36, Height = fh, Fill = new SolidColorBrush(color) };
        Canvas.SetLeft(fill, x); Canvas.SetTop(fill, 6 + barH - fh);
        c.Children.Add(fill);
        var tb = new TextBlock { Text = label, FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(220, 0xCC, 0xCC, 0xCC)), Width = 36, TextAlignment = TextAlignment.Center };
        Canvas.SetLeft(tb, x); Canvas.SetTop(tb, h - 22);
        c.Children.Add(tb);
    }

    // ================================================================ CSF / SPDS ====
    private void BuildCsfPanel()
    {
        CsfPanel.Children.Clear();
        _csfCells.Clear();
        // (id, en, zh, severity 0=green..3=red)
        (string en, string zh, Func<int> sev)[] defs =
        {
            ("S Subcrit", "S 次臨界", () => _sim.IsScrammed && _sim.NeutronPowerFraction > 0.02 ? 3 : (_sim.NeutronPowerFraction > 1.05 ? 2 : 0)),
            ("C Cooling", "C 堆芯冷卻", () => _sim.SubcoolingMarginC < 0 ? 3 : _sim.SubcoolingMarginC < 15 ? 2 : 0),
            ("H Heat sink", "H 熱阱", () => _sim.SteamGenLevel < 17 ? 3 : _sim.SteamGenLevel < 30 ? 2 : 0),
            ("P Integrity", "P 完整性", () => _sim.PrimaryPressure > ReactorSimService.VesselPressureLimit ? 3 : _sim.PrimaryPressure > ReactorSimService.VesselPressureLimit - 1 ? 2 : 0),
            ("Z Containment", "Z 安全殼", () => _sim.Mode == ReactorMode.Meltdown ? 3 : _sim.DamageAccumulation > 1 ? 2 : 0),
            ("I Inventory", "I 存量", () => _sim.PressurizerLevel < 17 ? 3 : _sim.PressurizerLevel < 30 ? 2 : 0),
        };
        foreach (var (en, zh, sev) in defs)
        {
            var label = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.White) };
            var border = new Border
            {
                Width = 96, Height = 52, CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(Color.FromArgb(255, 0x2E, 0x7D, 0x32)),
                Child = new Viewbox { Child = label, MaxHeight = 40, Margin = new Thickness(3) },
            };
            _relocalizers.Add(() => label.Text = P(en, zh));
            label.Text = P(en, zh);
            _csfCells.Add((border, label, en, zh, sev));
            CsfPanel.Children.Add(border);
        }
    }

    private void UpdateCsfPanel()
    {
        foreach (var (border, _, _, _, sev) in _csfCells)
        {
            Color c = sev() switch
            {
                3 => Color.FromArgb(255, 0xD3, 0x2F, 0x2F),
                2 => Color.FromArgb(255, 0xF5, 0x7C, 0x00),
                1 => Color.FromArgb(255, 0xFB, 0xC0, 0x2D),
                _ => Color.FromArgb(255, 0x2E, 0x7D, 0x32),
            };
            border.Background = new SolidColorBrush(c);
        }
    }

    // ================================================================ RPS PANEL ====
    private void BuildRpsPanel()
    {
        RpsPanel.Children.Clear();
        _rpsTiles.Clear();
        foreach (var fn in _sim.Rps.Functions)
        {
            var name = new TextBlock
            {
                FontSize = 12, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.NoWrap,
                Foreground = new SolidColorBrush(Colors.White),
            };
            var status = new TextBlock
            {
                FontFamily = new FontFamily("Consolas"), FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 0xCC, 0xCC, 0xCC)),
            };
            // One LED per instrument channel (3 or 4).
            var leds = new Microsoft.UI.Xaml.Shapes.Ellipse[fn.ChannelCount];
            var ledRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < fn.ChannelCount; i++)
            {
                var led = new Microsoft.UI.Xaml.Shapes.Ellipse
                {
                    Width = 13, Height = 13,
                    Stroke = new SolidColorBrush(Color.FromArgb(160, 0x88, 0x88, 0x88)), StrokeThickness = 1,
                    Fill = new SolidColorBrush(Color.FromArgb(255, 0x2E, 0x7D, 0x32)),
                };
                ToolTipService.SetToolTip(led, P($"Channel {(char)('I' + i)}", $"通道 {(char)('I' + i)}"));
                leds[i] = led;
                ledRow.Children.Add(led);
            }
            var fnLocal = fn; // capture
            var topRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            topRow.Children.Add(new Viewbox { Child = name, MaxHeight = 16, HorizontalAlignment = HorizontalAlignment.Left });
            var inner = new StackPanel { Spacing = 4, Margin = new Thickness(8, 6, 8, 6) };
            inner.Children.Add(name);
            var midRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            midRow.Children.Add(ledRow);
            midRow.Children.Add(status);
            inner.Children.Add(midRow);
            var border = new Border
            {
                Width = 224, Height = 62, CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(40, 0x88, 0x88, 0x88)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0xAA, 0xAA, 0xAA)),
                BorderThickness = new Thickness(1),
                Child = inner,
            };
            _relocalizers.Add(() => name.Text = P(fnLocal.NameEn, fnLocal.NameZh));
            name.Text = P(fnLocal.NameEn, fnLocal.NameZh);
            _rpsTiles.Add(new RpsTile { Fn = fnLocal, Border = border, Name = name, Status = status, Leds = leds });
            RpsPanel.Children.Add(border);
        }

        // Permissive interlock pills.
        PermissivePanel.Children.Clear();
        _permPills.Clear();
        (string label, Func<bool> on)[] perms =
        {
            ("P-6", () => _sim.Rps.P6),
            ("P-7", () => _sim.Rps.P7),
            ("P-8", () => _sim.Rps.P8),
            ("P-9", () => _sim.Rps.P9),
            ("P-10", () => _sim.Rps.P10),
        };
        foreach (var (label, on) in perms)
        {
            var tb = new TextBlock { Text = label, FontFamily = new FontFamily("Consolas"), FontSize = 11, Foreground = new SolidColorBrush(Colors.White) };
            var b = new Border
            {
                CornerRadius = new CornerRadius(4), Padding = new Thickness(7, 2, 7, 2),
                Background = new SolidColorBrush(Color.FromArgb(40, 0x88, 0x88, 0x88)), Child = tb,
            };
            _permPills.Add((b, tb, on));
            PermissivePanel.Children.Add(b);
        }
    }

    private void UpdateRpsPanel()
    {
        var green = Color.FromArgb(255, 0x2E, 0x7D, 0x32);
        var amber = Color.FromArgb(255, 0xF5, 0x7C, 0x00);
        var red = Color.FromArgb(255, 0xD3, 0x2F, 0x2F);
        var grey = Color.FromArgb(255, 0x55, 0x5B, 0x62);
        bool flashOn = (int)(_flashPhase * 2) % 2 == 0;

        foreach (var t in _rpsTiles)
        {
            var fn = t.Fn;
            for (int i = 0; i < t.Leds.Length; i++)
            {
                Color c;
                if (fn.Bypass[i]) c = grey;
                else if (fn.ChannelTripped[i]) c = fn.FunctionTrip ? red : amber;
                else c = green;
                t.Leds[i].Fill = new SolidColorBrush(c);
            }

            Color border;
            string en, zh;
            if (fn.Blocked)
            {
                border = grey;
                en = "BLOCKED (permissive)"; zh = "已封鎖（允許訊號）";
            }
            else if (fn.FunctionTrip)
            {
                border = flashOn ? red : Color.FromArgb(120, 0xD3, 0x2F, 0x2F);
                en = $"TRIP {fn.TrippedCount}/{fn.ChannelCount}"; zh = $"跳脫 {fn.TrippedCount}/{fn.ChannelCount}";
            }
            else if (fn.PartialTrip)
            {
                border = amber;
                en = $"PARTIAL 1/{fn.ChannelCount}"; zh = $"部分 1/{fn.ChannelCount}";
            }
            else
            {
                border = green;
                en = "NORMAL"; zh = "正常";
            }
            t.Border.BorderBrush = new SolidColorBrush(border);
            t.Border.BorderThickness = new Thickness(fn.FunctionTrip || fn.PartialTrip ? 2 : 1);
            t.Status.Text = P(en, zh);
        }

        foreach (var (b, _, on) in _permPills)
        {
            bool active = on();
            b.Background = new SolidColorBrush(active
                ? Color.FromArgb(255, 0x2E, 0x7D, 0x32)
                : Color.FromArgb(40, 0x88, 0x88, 0x88));
        }
    }

    // ================================================================ SCENARIO COMBO ====
    private void BuildScenarioCombo()
    {
        ScenarioCombo.Items.Clear();
        ScenarioCombo.Items.Add(P("Normal", "正常"));
        ScenarioCombo.Items.Add(P("LOCA (loss of coolant)", "失水事故 LOCA"));
        ScenarioCombo.Items.Add(P("Station blackout", "全廠斷電"));
        ScenarioCombo.Items.Add(P("Loss of feedwater", "喪失給水"));
        ScenarioCombo.Items.Add(P("ATWS (no scram)", "ATWS（未能停堆）"));
        ScenarioCombo.Items.Add(P("Xenon restart", "氙毒重啟"));
        ScenarioCombo.Items.Add(P("SGTR — tube rupture", "蒸發器爆管 SGTR"));
        ScenarioCombo.SelectedIndex = 0;
    }

    private void IsolateSg_Click(object sender, RoutedEventArgs e)
    {
        _sim.SgtrIsolated = IsolateSgToggle.IsChecked == true;
    }

    private void Scenario_Changed(object sender, SelectionChangedEventArgs e)
    {
        _sim.TriggerScenario(ScenarioCombo.SelectedIndex switch
        {
            1 => ReactorScenario.Loca,
            2 => ReactorScenario.StationBlackout,
            3 => ReactorScenario.LossOfFeedwater,
            4 => ReactorScenario.Atws,
            5 => ReactorScenario.XenonRestart,
            6 => ReactorScenario.SgTubeRupture,
            _ => ReactorScenario.Normal,
        });
        // The isolate-SG control is only meaningful during an SGTR.
        if (IsolateSgToggle is not null)
        {
            IsolateSgToggle.IsChecked = false;
            IsolateSgToggle.Visibility = ScenarioCombo.SelectedIndex == 6 ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ================================================================ COMPOSITION FX ====
    private void InitFx()
    {
        try
        {
            var (c, root) = ReactorFx.Bind(FxLayer);
            _compositor = c;
            _fxRoot = root;
            _fxProps = c.CreatePropertySet();
            _fxProps.InsertScalar("power", 0);

            // Cherenkov glow centred over the reactor vessel in the mimic (vessel box at ~x40,y120,w120,h160).
            _glow = ReactorFx.CherenkovGlow(c, 95);
            _glow.Offset = new Vector3(100, 200, 0); // vessel centre
            root.Children.InsertAtTop(_glow);

            // Steam pool above the steam generator (box at ~x330,y80,w90).
            _steam = new ReactorFx.SteamPool(c, root, 24);

            // Bind the whole scroll content for screen-shake on meltdown.
            _scrollVisual = ElementCompositionPreview.GetElementVisual(RootScroll);

            _renderClock.Start(OnFxFrame);
        }
        catch { /* FX are optional; never break the page */ }
    }

    private void OnFxFrame(double dt)
    {
        if (_glow is null) return;
        var snap = _sim.Capture();
        float p = (float)Math.Clamp(snap.Power, 0, 1.2);
        _glow.Opacity = Math.Clamp(p * 1.15f, 0f, 1f);
        float scale = 0.35f + p * 1.0f;
        _glow.Scale = new Vector3(scale, scale, 1);

        // Steam rises from the SG proportionally to steam pressure (normalized to ~8.5 MPa).
        _steam?.Spawn(Math.Clamp(snap.SteamPressure / 8.5, 0, 1) * (snap.FlowFraction > 0.05 ? 1 : 0.2), 375, 90, dt);

        // Meltdown screen-shake + strobe (started once on meltdown).
        if (snap.Mode == ReactorMode.Meltdown && !_meltdownFxStarted)
        {
            _meltdownFxStarted = true;
            if (_scrollVisual is not null) ReactorFx.ScreenShake(_scrollVisual, 6f);
            var strobeVisual = ElementCompositionPreview.GetElementVisual(MeltdownStrobe);
            ReactorFx.RedStrobe(strobeVisual, true);
        }
    }

    // ================================================================ TOOLBAR ====
    private void OpenControlRoom_Click(object sender, RoutedEventArgs e)
    {
        try { var w = new ReactorControlRoomWindow(_sim); w.Activate(); } catch { }
    }

    private void OpenWidgets_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            new ReactorWidgetWindow(_sim, WidgetKind.CorePower).Activate();
            new ReactorWidgetWindow(_sim, WidgetKind.Status).Activate();
            new ReactorWidgetWindow(_sim, WidgetKind.Scram).Activate();
            SettingsStore.Set("reactor.widgets", "CorePower,Status,Scram");
        }
        catch { }
    }

    private void MuteToggle_Click(object sender, RoutedEventArgs e)
    {
        bool muted = MuteToggle.IsChecked == true;
        ReactorAudioEngine.I.SetEnabled(!muted);
        if (muted) ReactorAudioEngine.I.StopVoices();
        else if (_audioStarted) ReactorAudioEngine.I.Hum(true);
    }

    // ================================================================ ANNUNCIATOR BUTTONS ====
    private void Ack_Click(object sender, RoutedEventArgs e)
    {
        _alarmsAcked = true;
        ReactorAudioEngine.I.Beep(accept: true);
    }

    private void Silence_Click(object sender, RoutedEventArgs e)
    {
        _silenced = true;
        ReactorAudioEngine.I.Klaxon(false);
        ReactorAudioEngine.I.Buzzer(false);
        ReactorAudioEngine.I.Beep(accept: false);
    }

    private void ResetAlarms_Click(object sender, RoutedEventArgs e)
    {
        _alarmsAcked = false;
        _silenced = false;
        ReactorAudioEngine.I.Beep(accept: true);
    }

    private void LampTest_Click(object sender, RoutedEventArgs e)
    {
        // Flash every annunciator tile + CSF cell amber for a moment (lamp test).
        foreach (var kv in _alarmTiles)
        {
            kv.Value.Border.Background = new SolidColorBrush(Color.FromArgb(255, 0xF5, 0x7C, 0x00));
            kv.Value.Label.Foreground = new SolidColorBrush(Colors.White);
        }
        ReactorAudioEngine.I.Beep(accept: true);
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        t.Tick += (_, _) => { t.Stop(); UpdateAlarmTiles(); };
        t.Start();
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

        // Live rod-position indication (steps withdrawn, 0–228) + lead-bank insertion-limit status.
        _rodStatusText = new TextBlock
        {
            FontSize = 12,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(200, 0xCF, 0xD8, 0xDC)),
            Margin = new Thickness(2, -4, 2, 6),
        };
        host.Children.Add(_rodStatusText);

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
        var autoPzr = MakeToggle("Auto press ctrl · 自動壓力", "自動壓力 · Auto press ctrl", v => _sim.PzrAutoPressureControl = v);
        autoPzr.IsChecked = true; // defaults to the automatic pressure-control program
        pzrPanel.Children.Add(autoPzr);
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

        // ---- Startup-sequence checklist (approach to criticality) ----
        host.Children.Add(SectionHeader("Startup sequence (approach to criticality) · 啟動程序（趨近臨界）",
                                        "啟動程序（趨近臨界）· Startup sequence"));
        BuildStartupChecklist(host);

        // ---- Always-on reactor persistence (opt-in, default OFF, easy off switch) ----
        host.Children.Add(SectionHeader("⚛ Always-on reactor · 常駐反應堆", "⚛ 常駐反應堆 · Always-on reactor"));
        BuildKeepAliveSection(host);

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

    private readonly List<(StartupStep step, TextBlock check)> _startupSteps = new();
    private TextBlock? _keepAliveStatus;
    private Border? _keepAliveDot;

    private void UpdateControlsLive()
    {
        // Live rod-position indication: per-bank steps withdrawn (0–228) + lead-bank insertion limit.
        if (_rodStatusText is not null)
        {
            string steps = "";
            for (int b = 0; b < 4; b++)
                steps += $"{(char)('A' + b)}:{_sim.RodStepsWithdrawn(b),3}  ";
            double lowLim = _sim.RilLowLimitSteps(_sim.NeutronPowerFraction);
            string lim = _sim.RilLowLowAlarm
                ? P("LO-LO — bank D too deep", "低低 — D 棒插得太深")
                : _sim.RilLowAlarm
                    ? P("LO — bank D below limit", "低 — D 棒低於限值")
                    : P("within limit", "在限值內");
            _rodStatusText.Text =
                P($"Rod steps withdrawn (0–228):  {steps}", $"控制棒抽出步數（0–228）：  {steps}") + "\n" +
                P($"Lead-bank D limit @ {_sim.NeutronPowerFraction * 100:F0}% pwr: {lowLim:F0} steps · {lim}",
                  $"領先 D 棒插入限值 @ {_sim.NeutronPowerFraction * 100:F0}% 功率：{lowLim:F0} 步 · {lim}");
            _rodStatusText.Foreground = new SolidColorBrush(
                _sim.RilLowLowAlarm ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
                : _sim.RilLowAlarm ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
                : Color.FromArgb(200, 0xCF, 0xD8, 0xDC));
        }

        // Live-update the startup checklist marks.
        foreach (var (step, check) in _startupSteps)
        {
            bool ok = step.IsSatisfied(_sim);
            check.Text = ok ? "✓" : "○";
            check.Foreground = new SolidColorBrush(ok
                ? Color.FromArgb(255, 0x4C, 0xAF, 0x50)
                : Color.FromArgb(160, 0xAA, 0xAA, 0xAA));
        }
        // Live-update keep-alive status pill.
        if (_keepAliveStatus is not null && _keepAliveDot is not null)
        {
            string st = ReactorPersistence.Status();
            _keepAliveStatus.Text = P($"Status: {st}", $"狀態：{KeepAliveStatusZh(st)}");
            Color c = ReactorPersistence.Enabled
                ? Color.FromArgb(255, 0x4C, 0xAF, 0x50)
                : Color.FromArgb(255, 0x75, 0x75, 0x75);
            _keepAliveDot.Background = new SolidColorBrush(c);
        }
    }

    private static string KeepAliveStatusZh(string en) => en switch
    {
        "Enabled" => "已啟用",
        "Enabled (entry removed)" => "已啟用（項目已被移除）",
        "Disabling…" => "停用中…",
        _ => "已停用",
    };

    private void BuildStartupChecklist(StackPanel host)
    {
        _startupSteps.Clear();
        var steps = ReactorScenarios.StartupSequence();
        int i = 1;
        foreach (var step in steps)
        {
            var check = new TextBlock { Text = "○", FontSize = 16, Width = 24, Foreground = new SolidColorBrush(Color.FromArgb(160, 0xAA, 0xAA, 0xAA)) };
            var text = new TextBlock { FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            int n = i;
            var stp = step;
            _relocalizers.Add(() => text.Text = $"{n}. " + P(stp.En, stp.Zh));
            text.Text = $"{n}. " + P(stp.En, stp.Zh);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { check, text } };
            host.Children.Add(row);
            _startupSteps.Add((step, check));
            i++;
        }
    }

    private void BuildKeepAliveSection(StackPanel host)
    {
        var toggle = new ToggleSwitch
        {
            Header = P("Keep the reactor running (start at login, restart if it stops) · 保持反應堆常駐（開機自動啟動、停咗會自動重開）",
                       "保持反應堆常駐（開機自動啟動、停咗會自動重開）· Keep the reactor running"),
            IsOn = ReactorPersistence.Enabled, // DEFAULT OFF unless previously enabled
            OnContent = P("On", "開"),
            OffContent = P("Off", "關"),
        };
        toggle.Toggled += (_, _) => { ReactorPersistence.SetEnabled(toggle.IsOn); UpdateControlsLive(); };
        _relocalizers.Add(() =>
        {
            toggle.Header = P("Keep the reactor running (start at login, restart if it stops) · 保持反應堆常駐（開機自動啟動、停咗會自動重開）",
                              "保持反應堆常駐（開機自動啟動、停咗會自動重開）· Keep the reactor running");
            toggle.OnContent = P("On", "開");
            toggle.OffContent = P("Off", "關");
        });
        host.Children.Add(toggle);

        // status pill
        var pill = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _keepAliveDot = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromArgb(255, 0x75, 0x75, 0x75)), VerticalAlignment = VerticalAlignment.Center };
        _keepAliveStatus = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        pill.Children.Add(_keepAliveDot);
        pill.Children.Add(_keepAliveStatus);
        host.Children.Add(pill);

        var warn = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xB3, 0x00)) };
        _relocalizers.Add(() => warn.Text = P(
            "This adds a visible startup entry (Task Manager → Startup and the Startup Apps module) and a small watchdog that restarts the reactor if it closes. Turn it off here anytime.",
            "呢個會加一個可見嘅開機項目（工作管理員→啟動 同 開機程式模組）同一個細小守護程式，反應堆關咗會自動重開。隨時可以喺度關閉。"));
        warn.Text = P(
            "This adds a visible startup entry (Task Manager → Startup and the Startup Apps module) and a small watchdog that restarts the reactor if it closes. Turn it off here anytime.",
            "呢個會加一個可見嘅開機項目（工作管理員→啟動 同 開機程式模組）同一個細小守護程式，反應堆關咗會自動重開。隨時可以喺度關閉。");
        host.Children.Add(warn);

        var removeBtn = new Button();
        _relocalizers.Add(() => removeBtn.Content = P("Remove all reactor persistence · 移除全部常駐設定", "移除全部常駐設定 · Remove all reactor persistence"));
        removeBtn.Content = P("Remove all reactor persistence · 移除全部常駐設定", "移除全部常駐設定 · Remove all reactor persistence");
        removeBtn.Click += (_, _) =>
        {
            ReactorPersistence.RemoveAll();
            toggle.IsOn = false;
            UpdateControlsLive();
        };
        host.Children.Add(removeBtn);
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
        _oneOverMHist.Clear();
        _simClock = 0;
        // Stop meltdown FX.
        _meltdownFxStarted = false;
        try
        {
            if (_scrollVisual is not null) ReactorFx.ScreenShake(_scrollVisual, 0);
            var strobeVisual = ElementCompositionPreview.GetElementVisual(MeltdownStrobe);
            ReactorFx.RedStrobe(strobeVisual, false);
        }
        catch { }
        try { ReactorAudioEngine.I.EvacTone(false); } catch { }
        _sim.Reset();
        if (ScenarioCombo is not null) ScenarioCombo.SelectedIndex = 0;
        // Rebuild the control surface so toggles/sliders reflect the reset engine state.
        _relocalizers.Clear();
        BuildControls();
        AutoRunToggle.IsOn = false;
        Render();
    }
}
