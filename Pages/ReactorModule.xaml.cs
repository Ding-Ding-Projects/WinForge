using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading;
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
    // Shared fuel factory so the HTML control room and this page see one fuel inventory.
    private readonly FuelFactoryService _fuel = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) }; // 10 Hz
    private DateTime _last = DateTime.UtcNow;
    private double _simClock; // seconds since start

    // Real-shutdown safety state. The ARM flag now lives in ReactorRealShutdownArm (set on the Reactor
    // Settings page; DEFAULT OFF, in-memory only). 真實關機致動旗標已搬去反應堆設定頁（預設關閉）。
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
    private readonly List<(Border border, TextBlock label, int idx)> _csfCells = new();
    // Preallocated status brushes indexed by CsfStatus ordinal (0=Invalid..4=Red) — no per-frame allocation.
    private readonly SolidColorBrush[] _csfBrush =
    {
        new(Color.FromArgb(255, 0x55, 0x55, 0x55)), // Invalid — grey (insufficient data)
        new(Color.FromArgb(255, 0x2E, 0x7D, 0x32)), // Green   — satisfied
        new(Color.FromArgb(255, 0xFB, 0xC0, 0x2D)), // Yellow  — degraded
        new(Color.FromArgb(255, 0xF5, 0x7C, 0x00)), // Orange  — severe
        new(Color.FromArgb(255, 0xD3, 0x2F, 0x2F)), // Red     — immediate restoration
    };
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
    private bool _silenced;   // SCRAM-klaxon silence (legacy global silence)

    // ---- ISA-18.1 sequence "R-F" annunciator state machine (Ringback + First-out) ----
    // The reactor's alarms are a plain bool[] source (ReactorSimService.Alarm). Real control-board
    // annunciator windows are NOT plain on/off lamps — they follow an ISA-18.1-1979 (R2004) sequence:
    // a new point FAST-FLASHES (120 fpm / 2 Hz) with the horn, ACKNOWLEDGE converts it to STEADY and
    // silences the horn, and when the process clears the window does NOT go dark — it SLOW-FLASHES
    // (60 fpm / 1 Hz) in "ringback" with a soft chime until the operator presses RESET. The FIRST point
    // to come in after a quiescent panel latches as the "first-out" so the trip initiator is identifiable
    // in a cascade. This is a pure HMI/state-machine layer over the existing alarm booleans — it touches
    // no physics, no reactivity, no energy balance, and the meltdown/ARM path is unaffected.
    private enum AnnState : byte { Normal, FastFlash, FastFlashFirstOut, Acked, Ringback }
    private readonly List<ReactorAlarm> _annKeys = new();               // stable iteration order (defs order)
    private readonly Dictionary<ReactorAlarm, AnnState> _annState = new();
    private readonly Dictionary<ReactorAlarm, bool> _annPrevRaw = new();// edge memory
    private ReactorAlarm? _firstOut;                                    // single global first-out latch
    private bool _silenceHorn;                                         // SILENCE — horn off, lamps keep flashing
    private bool _ackPending, _resetPending;                           // operator action edges (consumed next tick)
    private bool _lampTestHeld;                                        // TEST — render override, never mutates state
    private double _annPhase;                                          // wall-clock-free shared flash accumulator
    private const double AnnFastPeriod = 0.5;  // s — 2.0 Hz / 120 fpm fast (unacked) flash
    private const double AnnSlowPeriod = 1.0;  // s — 1.0 Hz /  60 fpm slow (ringback) flash; LCM = 1.0 s
    private bool AnnFastOn => (_annPhase % AnnFastPeriod) < AnnFastPeriod * 0.5;
    private bool AnnSlowOn => (_annPhase % AnnSlowPeriod) < AnnSlowPeriod * 0.5;

    // Keep-awake (real OS) state driven by the simulated generator output.
    // 由模擬發電機輸出驅動嘅真實作業系統保持喚醒狀態。
    private bool _keepingAwake;                 // tracks the live OS hold so calls stay idempotent
    // User toggle (DEFAULT ON) now lives on the Reactor Settings page via ReactorKeepAwakeSetting.
    private static bool _keepAwakeEnabled => ReactorKeepAwakeSetting.Enabled;
    private const double KeepAwakeMinMWe = 1.0;  // generator must deliver > 1 MWe to the grid

    // Cached control text blocks needing re-localization.
    private readonly List<Action> _relocalizers = new();
    // Native controls mirror the shared sim state so the HTML control room and embedded control room stay in sync.
    private readonly List<Action> _controlSyncers = new();
    private bool _syncingControlValues;
    private readonly Dictionary<string, FrameworkElement> _startupControlAnchors = new(StringComparer.OrdinalIgnoreCase);
    private TextBlock? _startupPressureGaugeText;
    private TextBlock? _startupPressureStepText;
    private ProgressBar? _startupPressureGauge;

    // Public status-API card live element (the enable/disable toggle moved to the Reactor Settings page).
    private TextBlock? _apiStateText;
    private ReactorHtmlWindow? _controlRoomWindow;
    private ReactorStartupChecklistWindow? _startupChecklistWindow;
    // 防崩潰自動儲存：本反應堆喺 PersistenceService 嘅提供者 id。
    // Persistence: this reactor's provider id in PersistenceService.
    private const string PersistId = "reactor";
    private bool _persistenceRegistered;
    private FrameworkElement? _startupChecklistAnchor;
    private string? _pendingDeepLink;
    private bool _startupChecklistRequested;
    private DateTime _lastHardSaveUtc = DateTime.MinValue;
    private int _hardSaveInFlight;
    private static readonly TimeSpan HardSaveInterval = TimeSpan.FromSeconds(1);
    private DateTime _lastStripChartUiUtc = DateTime.MinValue;
    private DateTime _lastInstrumentPanelUiUtc = DateTime.MinValue;
    private DateTime _lastControlSyncUiUtc = DateTime.MinValue;
    private static readonly TimeSpan StripChartUiInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan InstrumentPanelUiInterval = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan ControlSyncUiInterval = TimeSpan.FromMilliseconds(500);
    private bool _autoStartApplied;

    public ReactorModule()
    {
        InitializeComponent();
        _sim.MeltdownOccurred += OnMeltdown;
        // Audible "pop" cue each time a pressurizer code safety valve lifts (uses the existing relay-click voice).
        _sim.PzrCodeSafetyLifted += () => ReactorAudioEngine.I.RelayClick();
        // Same audible pop each time a Main Steam Safety Valve lifts on the secondary side.
        _sim.MssvValveLifted += () => ReactorAudioEngine.I.RelayClick();
        Loc.I.LanguageChanged += OnLanguageChanged;

        Loaded += async (_, _) =>
        {
            // 還原已儲存狀態（如有）· Restore saved reactor state (if any) BEFORE building the UI so
            // gauges/controls reflect the resumed state. The provider's restore Action runs here.
            RegisterPersistence();
            if (!App.AutoStartReactor && _sim.AutoStartMode)
            {
                _sim.AutoStartMode = false;
                PersistenceService.I.NoteChanged();
            }
            ApplyCommandLineAutoStart();

            // Build each section under its own guard so a single control failure can never blank the
            // whole control room (Render + the live timer must still run). 逐段建構並各自防護，
            // 任何一段失敗都唔會令整個控制室空白。
            CrashLogger.Guard("Reactor.BuildControls", BuildControls);
            CrashLogger.Guard("Reactor.BuildGauges", BuildGauges);
            CrashLogger.Guard("Reactor.BuildAlarmTiles", BuildAlarmTiles);
            CrashLogger.Guard("Reactor.BuildStripCharts", BuildStripCharts);
            CrashLogger.Guard("Reactor.BuildCsfPanel", BuildCsfPanel);
            CrashLogger.Guard("Reactor.BuildRpsPanel", BuildRpsPanel);
            CrashLogger.Guard("Reactor.BuildScenarioCombo", BuildScenarioCombo);
            CrashLogger.Guard("Reactor.DrawMimicStatic", DrawMimicStatic);
            CrashLogger.Guard("Reactor.InitFx", InitFx);
            CrashLogger.Guard("Reactor.Render", Render);

            UpdateSaveIndicator(PersistenceService.I.LastSaved);
            PersistenceService.I.Saved += OnStateSaved;

            // Reactor↔system-settings linkage: the configuration toggle now lives on the Reactor Settings
            // page (DEFAULT OFF). If the persisted setting was left on, re-arm it here (snapshots fresh
            // originals) so the linkage keeps working while the main page is open. 系統連動設定已搬去設定頁。
            if (ReactorSystemLinkService.EnabledSetting) ReactorSystemLinkService.I.Enable();
            UpdateSysLinkPill();


            _last = DateTime.UtcNow;
            _timer.Tick += Tick;
            _timer.Start();

            // 綁定對外狀態 API 到呢個實時模擬實例，並建立狀態卡 · Bind the public status API to this live
            // sim and build the bilingual status card. The API auto-started in App.OnLaunched; binding
            // here makes it serve REAL data while the page is open. Publish() is called each tick.
            try { ReactorStatusApiService.I.Bind(_sim); } catch { }
            BuildStatusApiCard();
            UpdateStatusApiCard();

            // Audio: lazily start the AudioGraph, then begin the ambient hum (respects mute).
            try
            {
                await ReactorAudioEngine.I.EnsureStartedAsync();
                _audioStarted = true;
                if (ReactorAudioEngine.I.Enabled) ReactorAudioEngine.I.Hum(true);
            }
            catch { /* degrade silently */ }

            TryApplyPendingDeepLink();
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _timer.Tick -= Tick;
            _countdownTimer?.Stop();
            _renderClock.Stop();
            // Persist current reactor state one last time, then stop listening. This must be
            // synchronous so navigating away and immediately back restores the state just left.
            // We keep the provider registered so a final app-exit/crash flush still captures it.
            PersistenceService.I.Saved -= OnStateSaved;
            CrashLogger.Guard("reactor:unload-flush", () => PersistenceService.I.Flush());
            Loc.I.LanguageChanged -= OnLanguageChanged;
            // Stop the synthesized voices but keep the graph alive (singleton, reused by the windows).
            try { ReactorAudioEngine.I.StopVoices(); } catch { }
            // SAFETY: always release the keep-awake hold when leaving the page so navigating
            // away never leaves the system pinned awake. 離開頁面時務必釋放保持喚醒。
            ReleaseKeepAwake();
            // 解除狀態 API 綁定並發佈「離線」快照 · Unbind the status API and publish an offline snapshot
            // so dependents see isGenerating=false once the reactor page is closed. The server stays up.
            try { ReactorStatusApiService.I.Unbind(); ReactorStatusApiService.I.PublishOffline(); } catch { }
            // SAFETY: restore all real Windows settings (power plan / accent / brightness / volume)
            // when leaving the page so the user is never stranded on a red accent or power-saver.
            // The persisted toggle is left as-is, so it re-arms next time the page opens.
            // 離開頁面時務必還原所有真實 Windows 設定，免得使用者卡喺紅色強調色或省電模式。
            try { _ = ReactorSystemLinkService.I.RestoreAllAsync(); } catch { }
            try { _controlRoomWindow?.Close(); } catch { }
            _controlRoomWindow = null;
        };
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string target && IsStartupDeepLink(target))
        {
            _pendingDeepLink = target;
            TryApplyPendingDeepLink();
        }
    }

    private static bool IsStartupDeepLink(string? target)
        => !string.IsNullOrWhiteSpace(target)
           && (string.Equals(target, "startup", StringComparison.OrdinalIgnoreCase)
               || string.Equals(target, "startup-checklist", StringComparison.OrdinalIgnoreCase));

    private void TryApplyPendingDeepLink()
    {
        if (!IsStartupDeepLink(_pendingDeepLink) || _startupChecklistAnchor is null) return;
        _pendingDeepLink = null;
        _startupChecklistRequested = true;
        _controlRoomWindow?.NavigateRoom("startup");
        DispatcherQueue.TryEnqueue(ScrollToStartupChecklist);
    }

    private void ApplyCommandLineAutoStart()
    {
        if (_autoStartApplied || !App.AutoStartReactor) return;
        _autoStartApplied = true;
        _sim.ApplyAutoStartPreset();
        _pendingDeepLink = "startup";
        _startupChecklistRequested = true;
        PersistenceService.I.NoteChanged();
    }

    private void ScrollToStartupChecklist()
    {
        try
        {
            if (_startupChecklistAnchor is not null) ScrollToElement(_startupChecklistAnchor);
        }
        catch (Exception ex) { CrashLogger.Log("reactor:startup-deeplink", ex); }
    }

    private void ScrollToStartupTarget(string? target)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                ScrollToStartupChecklist();
                return;
            }

            if (_startupControlAnchors.TryGetValue(target.Trim(), out var anchor))
                ScrollToElement(anchor);
            else
                ScrollToStartupChecklist();
        }
        catch (Exception ex) { CrashLogger.Log("reactor:startup-control-link", ex); }
    }

    private void ScrollToElement(FrameworkElement element)
    {
        if (RootScroll.Content is not UIElement content) return;
        RootScroll.UpdateLayout();
        element.UpdateLayout();
        var point = element.TransformToVisual(content).TransformPoint(new Point(0, 0));
        RootScroll.ChangeView(null, Math.Max(0, point.Y - 12), null);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    // RCP flow-mode annotation: shows whether the indicated flow is buoyancy-driven natural circulation,
    // a tripped pump still coasting down on its flywheel, or normal forced (pumped) flow.
    private string FlowModeTag()
    {
        if (_sim.RcpLockedRotor) return P($" · LOCKED LOOP {_sim.LockedRotorLoop + 1}", $" · 卡軸 {_sim.LockedRotorLoop + 1}號環");
        if (_sim.OnNaturalCirc) return P(" · NAT CIRC", " · 自然循環");
        if (_sim.RcpCoasting) return P(" · COASTDOWN", " · 惰轉");
        return "";
    }

    private void OnLanguageChanged(object? s, EventArgs e)
    {
        Render();
        foreach (var r in _relocalizers) r();
        UpdateSaveIndicator(PersistenceService.I.LastSaved);
    }

    // ============================================================ persistence ====
    /// <summary>
    /// 登記反應堆做 PersistenceService 嘅狀態提供者，並還原已儲存狀態（如有）。
    /// Register the reactor as a PersistenceService provider and restore saved state if present.
    /// The snapshot Func captures the full sim state + sim clock; the restore Action deserializes the
    /// saved JSON back into the engine. Auto-resumes (no prompt) — the saved run simply continues.
    /// </summary>
    private void RegisterPersistence()
    {
        if (_persistenceRegistered) return;
        _persistenceRegistered = true;
        try
        {
            PersistenceService.I.Register(
                PersistId,
                snapshot: () => _sim.CaptureSnapshot(_simClock),
                restore: el =>
                {
                    // Deserialize on whatever thread Register runs on (the UI thread here, during Loaded).
                    var snap = System.Text.Json.JsonSerializer.Deserialize<ReactorSimService.PersistSnapshot>(el.GetRawText());
                    if (snap is null) return;
                    _sim.RestoreSnapshot(snap);
                    _simClock = snap.SimClockSeconds;
                    // If we restored straight into a meltdown, surface the overlay (simulated only).
                    if (_sim.Mode == ReactorMode.Meltdown) { _meltdownHandled = false; OnMeltdown(); }
                });
        }
        catch (Exception ex) { CrashLogger.Log("reactor:register-persistence", ex); }
    }

    private void OnStateSaved(DateTime when)
    {
        // Saved fires on a background timer thread — marshal to the UI.
        DispatcherQueue.TryEnqueue(() => UpdateSaveIndicator(when));
    }

    private void UpdateSaveIndicator(DateTime? when)
    {
        bool on = PersistenceService.I.AutosaveEnabled;

        if (!on)
        {
            SaveStatusText.Text = P("Autosave off · 自動儲存關閉", "自動儲存關閉 · Autosave off");
            SaveDot.Background = new SolidColorBrush(Color.FromArgb(255, 0x75, 0x75, 0x75));
            return;
        }
        if (when is DateTime t)
        {
            SaveStatusText.Text = P($"State saved · 狀態已儲存 ({t:HH:mm:ss})",
                                    $"狀態已儲存 · State saved ({t:HH:mm:ss})");
            SaveDot.Background = new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xAF, 0x50));
        }
        else
        {
            SaveStatusText.Text = P("Autosave on · 自動儲存開啟", "自動儲存開啟 · Autosave on");
            SaveDot.Background = new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xAF, 0x50));
        }
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
        StartupChecklistButton.Content = P("Startup checklist", "啟動程序清單");
        ChecklistWidgetButton.Content = P("Checklist + gauges", "程序清單＋儀表");
        OpenWidgetsButton.Content = P("Mini widgets", "桌面小工具");
        MuteToggle.Content = P("Mute audio", "靜音");
        MuteToggle.IsChecked = !ReactorAudioEngine.I.Enabled;
        ScenarioLabel.Text = P("Scenario:", "情景：");
        IsolateSgToggle.Content = P("Isolate affected SG", "隔離受影響蒸發器");
        NisTitle.Text = P("Nuclear instrumentation (NIS) & critical safety functions (SPDS)", "核儀表（NIS）與關鍵安全功能（SPDS）");
        NisLabel.Text = P("NIS — source / intermediate / power range", "核儀表 — 起動／中間／功率量程");
        OneOverMLabel.Text = P("1/M — approach to criticality", "1/M — 趨近臨界");
        CsfTitle.Text = P("Critical safety functions", "關鍵安全功能");
        RevMeterTitle.Text = P("Reactivity computer · 反應性計算機", "反應性計算機 · Reactivity computer");
        RevMeterMarkButton.Content = P("Mark", "標記");
        RevMeterClearButton.Content = P("Clear", "清除");
        CalMeterTitle.Text = P("Secondary calorimetric · NIS calibration", "二次側熱平衡 · 核儀表校準");
        CalMeterCalibrateButton.Content = P("Calibrate PR", "校準功率量程");
        CalMeterLefmToggle.OnContent = P("LEFM", "超聲流量計");
        CalMeterLefmToggle.OffContent = P("Venturi", "文丘里");
        CalMeterLefmToggle.IsOn = _sim.UseLefm;
        AckButton.Content = P("ACK", "確認");
        SilenceButton.Content = P("SILENCE", "靜音警報");
        ResetAlarmButton.Content = P("RESET", "重置");
        LampTestButton.Content = P("LAMP TEST", "燈測");
        ReactorSettingsButtonText.Text = P("⚙ Reactor Settings · 反應堆設定", "⚙ 反應堆設定 · Reactor Settings");
        UpdateSysLinkPill();
        MimicTitle.Text = P("Plant Mimic Diagram · 機組流程圖", "機組流程圖 · Plant Mimic Diagram");
        RpsTitle.Text = P("Reactor Protection System · 反應堆保護系統", "反應堆保護系統 · Reactor Protection System");
        RpsSubtitle.Text = P(
            "4-channel 2-of-4 coincidence logic with Westinghouse trip setpoints. A single tripped channel is a partial trip (amber) — the reactor trips only when ≥2 of 4 channels of a function trip. Permissives P-6/P-7/P-8/P-9/P-10 block low-power trips.",
            "四通道四取二符合邏輯，採用西屋跳脫定值。單一通道跳脫只屬部分跳脫（琥珀色）— 須同一功能四取二（≥2 通道）方會觸發停堆。允許訊號 P-6／P-7／P-8／P-9／P-10 會封鎖低功率跳脫。");
        GaugesTitle.Text = P("Instrument Gauges · 儀表", "儀表 · Instrument Gauges");
        TrendTitle.Text = P("Strip-Chart Recorders · 趨勢記錄儀", "趨勢記錄儀 · Strip-Chart Recorders");
        AlarmTitle.Text = P(
            "Annunciator Panel (ISA-18.1 R-F) · 警報盤 — fast-flash=new+horn · magenta=first-out · steady=ack'd · slow teal=ringback (RESET to clear)",
            "警報盤（ISA-18.1 R-F 序列）· 快閃＋喇叭＝新警報 · 洋紅＝首發（First-out）· 長亮＝已確認 · 慢藍綠閃＝回響（Ringback，按重置清除）");
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

        if (_sim.AutoStartMode)
            _sim.DriveAutoStart(dt);
        _sim.Update(dt);

        // 每個反應堆 tick 向對外 API 發佈狀態快照 · Publish a fresh status snapshot to the public API
        // (MMF + named-pipe subscribers) every reactor tick. Exception-safe; never affects the sim.
        try { ReactorStatusApiService.I.Publish(); } catch { }

        UpdateAnnunciator(dt);   // ISA-18.1 R-F sequence pass — must run before tile render + audio
        UpdateKeepAwake();
        UpdateSysLink(dt);
        DriveHomeAssistant();
        UpdateStatusBanner();
        UpdateGauges();
        UpdateAlarmTiles();
        UpdateMimic();
        UpdateAudio();
        UpdateAutoStartOverlay();

        // The simulator still ticks at 10 Hz, but these panels rebuild/measure lots of XAML. Redrawing
        // them every frame can starve pointer/keyboard input, especially with the full control room open.
        if (now - _lastStripChartUiUtc >= StripChartUiInterval)
        {
            _lastStripChartUiUtc = now;
            UpdateStripCharts();
        }
        if (now - _lastInstrumentPanelUiUtc >= InstrumentPanelUiInterval)
        {
            _lastInstrumentPanelUiUtc = now;
            UpdateNisPanels();
            UpdateCsfPanel();
            UpdateRpsPanel();
        }
        if (now - _lastControlSyncUiUtc >= ControlSyncUiInterval)
        {
            _lastControlSyncUiUtc = now;
            UpdateControlsLive();
            UpdateStatusApiCard();
        }
        MaybeHardSave(now);

        if (_sim.Mode == ReactorMode.Meltdown)
            AnimateMeltdown(dt);
    }

    private void MaybeHardSave(DateTime nowUtc)
    {
        if (!_persistenceRegistered || !PersistenceService.I.AutosaveEnabled) return;
        if (nowUtc - _lastHardSaveUtc < HardSaveInterval) return;
        if (Interlocked.CompareExchange(ref _hardSaveInFlight, 1, 0) != 0) return;

        _lastHardSaveUtc = nowUtc;
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try { PersistenceService.I.Flush(); }
            catch (Exception ex) { CrashLogger.Log("reactor:hard-save", ex); }
            finally { Interlocked.Exchange(ref _hardSaveInFlight, 0); }
        });
    }

    private void UpdateAutoStartOverlay()
    {
        if (!_sim.AutoStartMode)
        {
            AutoStartOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        AutoStartOverlay.Visibility = Visibility.Visible;
        double progress = Math.Clamp(_sim.AutoStartProgressFraction, 0, 1);
        AutoStartProgress.Value = progress * 100.0;
        string stageEn = string.IsNullOrWhiteSpace(_sim.AutoStartStageEn) ? "Auto-start: arming startup sequence" : _sim.AutoStartStageEn;
        string stageZh = string.IsNullOrWhiteSpace(_sim.AutoStartStageZh) ? "自動啟動：準備啟動程序" : _sim.AutoStartStageZh;
        AutoStartText.Text = P(stageEn, stageZh);
        AutoStartSubText.Text = P(
            $"Staged startup in progress · auto-SCRAM suppressed · fuel x{_sim.FuelConsumptionMultiplier:0.##} · waste x{_sim.WasteProductionMultiplier:0.##}",
            $"分階段啟動中 · 自動 SCRAM 已抑制 · 燃料 x{_sim.FuelConsumptionMultiplier:0.##} · 廢料 x{_sim.WasteProductionMultiplier:0.##}");

        double wave = 0.5 + 0.5 * Math.Sin(_flashPhase * 5.0);
        byte alpha = (byte)(55 + 90 * wave);
        AutoStartPulse.Fill = new SolidColorBrush(Color.FromArgb(alpha, 0x42, 0xA5, 0xF5));
        AutoStartPulse.Stroke = new SolidColorBrush(progress >= 1.0
            ? Color.FromArgb(255, 0x4C, 0xAF, 0x50)
            : Color.FromArgb(255, 0x90, 0xCA, 0xF9));
    }

    // ============================================================ audio ====
    private void UpdateAudio()
    {
        if (!_audioStarted) return;
        var a = ReactorAudioEngine.I;
        a.Power = (float)_sim.NeutronPowerFraction;
        a.Flow = (float)_sim.CoolantFlowFraction;
        a.TurbineRpm = (float)_sim.TurbineRPM;
        a.Scram = _sim.IsScrammed;
        a.Meltdown = _sim.Mode == ReactorMode.Meltdown;
        a.SteamRelief = _sim.ReliefValveOpen || _sim.AnyPzrCodeSafetyOpen || _sim.MssvLifted;
        a.Radiation = (float)Math.Min(50.0,
            _sim.RadiationLevel
            + _sim.SecondaryRadiation / 100.0
            + _sim.ParticulateMonitorRatio * 2.0
            + _sim.GaseousMonitorRatio * 2.0
            + Math.Max(0.0, _sim.DamageAccumulation) / 20.0);

        bool enabled = a.Enabled;
        a.Hum(enabled);
        // Klaxon while scrammed (unless silenced). The annunciator HORN sounds while any window is in a
        // fast-flash (active + unacknowledged) state and not SILENCEd — exactly the ISA-18.1 sequence. A
        // distinct, softer RINGBACK chime sounds while any window is cleared-but-not-reset (slow flash).
        a.Klaxon(enabled && _sim.IsScrammed && !_silenced);
        a.Buzzer(enabled && AnnHornAudible() && !_sim.IsScrammed);
        a.Ringback(enabled && AnnAnyRingback() && !_silenceHorn);
        a.EvacTone(enabled && _sim.Mode == ReactorMode.Meltdown);

        // Relay click on fresh SCRAM edge; a fresh trip re-sounds the horn/klaxon (clears any prior silence).
        if (_sim.IsScrammed && !_lastScram) { a.RelayClick(); _silenced = false; _silenceHorn = false; }
        _lastScram = _sim.IsScrammed;
    }

    // ============================================================== status banner ====
    private void UpdateStatusBanner()
    {
        StatusText.Text = P(_sim.StatusEn, _sim.StatusZh);
        // Lifecycle mode + the formal Tech-Spec OPERATIONAL MODE (NUREG-1431) + Keff + elapsed minutes.
        ModeText.Text = P($"Mode: {ModeEn(_sim.Mode)}", $"模式：{ModeZh(_sim.Mode)}")
                        + "  ·  " + P(_sim.TsModeStatusEn, _sim.TsModeStatusZh)
                        + $" (Keff {_sim.CoreKeff:F4})"
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

    // ====================================================== ANNUNCIATOR SEQUENCE (ISA-18.1 R-F) ====
    // One deterministic Update(dt) is the sole mutator of the per-window state; tile render and audio
    // are pure reads. All timing is dt-accumulated + modulo (no DateTime / frame counts), so the flash
    // cadence is identical at any frame rate. Zero per-frame heap allocation.
    private void UpdateAnnunciator(double dt)
    {
        // Shared flash phase, bounded to the LCM of the fast/slow periods (1.0 s) so it never grows.
        _annPhase += dt;
        if (_annPhase >= AnnSlowPeriod) _annPhase -= AnnSlowPeriod;

        // Consume operator action edges latched by the ACK / RESET buttons.
        bool ack = _ackPending; _ackPending = false;
        bool reset = _resetPending; _resetPending = false;

        // Is the panel quiescent? (no window currently active + unacknowledged) — the first-out arming gate.
        bool quiescent = true;
        foreach (var k in _annKeys)
        {
            var s = _annState[k];
            if (s == AnnState.FastFlash || s == AnnState.FastFlashFirstOut) { quiescent = false; break; }
        }

        bool anyRising = false;
        foreach (var k in _annKeys)
        {
            bool on = _sim.Alarm(k);
            bool was = _annPrevRaw[k];
            bool rising = on && !was;
            bool falling = !on && was;
            var s = _annState[k];

            // RESET (global): ringback → normal; cleared-acked → normal; a still-live alarm cannot be
            // reset away — it demotes to steady Acked so the operator can never clear a real condition.
            if (reset)
            {
                if (s == AnnState.Ringback) s = AnnState.Normal;
                else if (s == AnnState.Acked && !on) s = AnnState.Normal;
                else if ((s == AnnState.FastFlash || s == AnnState.FastFlashFirstOut) && on) s = AnnState.Acked;
            }

            // Incoming alarm: the first point in after a quiescent panel latches as first-out.
            if (rising)
            {
                anyRising = true;
                if (s == AnnState.Normal || s == AnnState.Ringback)
                {
                    if (quiescent && _firstOut == null) { s = AnnState.FastFlashFirstOut; _firstOut = k; }
                    else s = AnnState.FastFlash;
                }
            }
            else if (falling)
            {
                // Process cleared but not yet reset → ringback slow-flash.
                if (s == AnnState.FastFlash || s == AnnState.FastFlashFirstOut || s == AnnState.Acked)
                    s = AnnState.Ringback;
            }

            // ACKNOWLEDGE: every active + unacked window → steady Acked (horn drops). First-out latch held.
            if (ack && (s == AnnState.FastFlash || s == AnnState.FastFlashFirstOut))
                s = AnnState.Acked;

            _annState[k] = s;
            _annPrevRaw[k] = on;
        }

        // First-out latch clears on RESET, or automatically once the panel returns to fully quiescent
        // (no active-unacked window remains), re-arming for the next quiescent→alarm burst.
        if (reset) _firstOut = null;
        else if (_firstOut != null && _annState[_firstOut.Value] != AnnState.FastFlashFirstOut)
        {
            bool anyActive = false;
            foreach (var k in _annKeys)
            {
                var s = _annState[k];
                if (s == AnnState.FastFlash || s == AnnState.FastFlashFirstOut) { anyActive = true; break; }
            }
            if (!anyActive) _firstOut = null;
        }

        // SILENCE auto-cancels on the next NEW alarm so a fresh excursion re-sounds the horn (reflash).
        if (anyRising) { _silenceHorn = false; _silenced = false; }
    }

    // Horn sounds while any window is active + unacknowledged and SILENCE is not engaged.
    private bool AnnHornAudible()
    {
        if (_silenceHorn) return false;
        foreach (var k in _annKeys)
        {
            var s = _annState[k];
            if (s == AnnState.FastFlash || s == AnnState.FastFlashFirstOut) return true;
        }
        return false;
    }

    // Ringback chime sounds while any window is cleared-but-not-reset (slow flash).
    private bool AnnAnyRingback()
    {
        foreach (var k in _annKeys)
            if (_annState[k] == AnnState.Ringback) return true;
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

    // ============================================================ public status API card ====
    // A small bilingual card on the reactor page that surfaces the public local status API: the
    // named-pipe + memory-mapped-file names other apps connect to, an Enable/Disable toggle (persisted
    // in SettingsStore, default ON), and a live running/offline indicator.
    private void BuildStatusApiCard()
    {
        if (StatusApiHost is null) return;
        StatusApiHost.Children.Clear();

        var title = new TextBlock { FontWeight = FontWeights.SemiBold };
        _relocalizers.Add(() => title.Text = P(
            "Public status API · 對外狀態 API",
            "對外狀態 API · Public status API"));
        title.Text = P("Public status API · 對外狀態 API", "對外狀態 API · Public status API");

        var blurb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        _relocalizers.Add(() => blurb.Text = P(
            "Other local apps can read this reactor's power/status in real time and depend on it (e.g. run only while generating). Copy Sdk/ReactorStatusClient.cs into your app — no WinForge reference needed.",
            "其他本機 app 可即時讀取本反應堆嘅功率／狀態並依賴佢（例如只喺發電時運行）。將 Sdk/ReactorStatusClient.cs 複製入你嘅 app 即可，無需引用 WinForge。"));
        blurb.Text = P(
            "Other local apps can read this reactor's power/status in real time and depend on it (e.g. run only while generating). Copy Sdk/ReactorStatusClient.cs into your app — no WinForge reference needed.",
            "其他本機 app 可即時讀取本反應堆嘅功率／狀態並依賴佢（例如只喺發電時運行）。將 Sdk/ReactorStatusClient.cs 複製入你嘅 app 即可，無需引用 WinForge。");

        var names = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(230, 0x90, 0xCA, 0xF9)),
            Text =
                $"Pipe   \\\\.\\pipe\\{ReactorStatusApiService.PipeName}\n" +
                $"MMF    {ReactorStatusApiService.MmfName}\n" +
                $"Mutex  {ReactorStatusApiService.MutexName}",
        };

        _apiStateText = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold };

        // The Enable/Disable CONFIGURATION toggle now lives on the Reactor Settings page; this card keeps
        // the live names + running indicator on the main reactor page. 開放狀態 API 嘅開關已搬去設定頁。
        StatusApiHost.Children.Add(title);
        StatusApiHost.Children.Add(blurb);
        StatusApiHost.Children.Add(names);
        StatusApiHost.Children.Add(_apiStateText);
    }

    private void UpdateStatusApiCard()
    {
        if (_apiStateText is null) return;
        bool enabled = ReactorStatusApiService.I.Enabled;
        bool running = ReactorStatusApiService.I.IsRunning;
        if (!enabled)
        {
            _apiStateText.Text = P("● Disabled · 已停用", "● 已停用 · Disabled");
            _apiStateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0x75, 0x75, 0x75));
        }
        else if (running)
        {
            _apiStateText.Text = P(
                $"● Live — seq {ReactorStatusApiService.I.LastSnapshot.Sequence} · 運行中",
                $"● 運行中 — 序號 {ReactorStatusApiService.I.LastSnapshot.Sequence} · Live");
            _apiStateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xAF, 0x50));
        }
        else
        {
            _apiStateText.Text = P("● Enabled (starting…) · 啟用中", "● 啟用中（啟動中…） · Enabled");
            _apiStateText.Foreground = new SolidColorBrush(Color.FromArgb(255, 0xFF, 0xB3, 0x00));
        }
    }

    // ============================================================ system linkage ====
    // The reactor's state drives REAL Windows settings (power plan / accent colour / brightness /
    // volume) so the simulated plant visibly affects the PC it "powers". OPT-IN, default OFF, and
    // fully reversible — every original is snapshotted on enable and restored on disable/unload/exit.
    // 反應堆狀態驅動真實 Windows 設定（電源計劃／強調色／亮度／音量），令模擬機組真實影響此電腦。
    private double _sysLinkAccum; // throttle: only push settings ~1.5 Hz, not every 100 ms tick.

    private void UpdateSysLink(double dt)
    {
        if (!ReactorSystemLinkService.I.Active)
        {
            _sysLinkAccum = 0;
            return;
        }
        _sysLinkAccum += dt;
        // Meltdown wants a snappier pulse for accent/brightness; otherwise ~1.5 Hz is plenty.
        double period = _sim.Mode == ReactorMode.Meltdown ? 0.2 : 0.66;
        if (_sysLinkAccum < period) return;
        ReactorSystemLinkService.I.Apply(_sim, _sysLinkAccum);
        _sysLinkAccum = 0;
        UpdateSysLinkPill();
    }

    private void UpdateSysLinkPill()
    {
        bool on = ReactorSystemLinkService.I.Active;
        if (on)
        {
            SysLinkText.Text = P(
                "⚙ Reactor is driving Windows — power plan, accent colour & brightness follow the plant · 反應堆正連動 Windows — 電源計劃、強調色同亮度跟隨機組",
                "⚙ 反應堆正連動 Windows — 電源計劃、強調色同亮度跟隨機組 · Reactor is driving Windows — power plan, accent colour & brightness follow the plant");
            SysLinkDot.Background = new SolidColorBrush(Color.FromArgb(255, 0x4C, 0xAF, 0x50)); // green
        }
        else
        {
            SysLinkText.Text = P(
                "Reactor is not linked to Windows settings · 反應堆未連動 Windows 設定",
                "反應堆未連動 Windows 設定 · Reactor is not linked to Windows settings");
            SysLinkDot.Background = new SolidColorBrush(Color.FromArgb(255, 0x75, 0x75, 0x75)); // grey
        }
    }

    // ============================================================ Home Assistant mirror ====
    // Drive the optional HA mirror from the live reactor tick. OPT-IN (default OFF) and exception-safe:
    // the mirror itself short-circuits when disabled / unconfigured and never throws into the tick.
    // 反應堆連動 Home Assistant：由實時 tick 驅動，預設關閉、全程防護，唔會影響模擬。
    private void DriveHomeAssistant()
    {
        // ALARM condition: SCRAM or meltdown → light on (red). GENERATING: on-load output to the grid.
        bool alarmActive = _sim.IsScrammed || _sim.Mode == ReactorMode.Meltdown;
        bool generating =
            _sim.GeneratorBreakerClosed
            && _sim.ElectricPowerMW > KeepAwakeMinMWe
            && !_sim.IsScrammed
            && _sim.Mode != ReactorMode.Meltdown
            && _sim.Mode != ReactorMode.Tripped;
        try { ReactorHomeAssistantMirror.I.Drive(alarmActive, generating); } catch { }
    }

    // Navigate to the dedicated Reactor Settings page (real-world / external controls).
    private void ReactorSettings_Click(object sender, RoutedEventArgs e)
    {
        try { Navigator.GoToModule?.Invoke("module.reactorsettings"); } catch { }
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
        AddGauge("Peak fuel enthalpy", "峰值燃料焓", 0, 300, () => _sim.PeakFuelEnthalpyCalPerG,
            () => $"{_sim.PeakFuelEnthalpyCalPerG:F0} cal/g · Δ{_sim.PeakFuelEnthalpyRiseCalPerG:F0}"
                + (_sim.RodEjectionActive ? $" · eject {_sim.EjectedRodWorthPcm:F0} pcm" : "")
                + (_sim.RiaFuelMelt ? $" · {P("MELT", "熔化")}" : _sim.RiaCoolabilityViolated ? $" · {P("COOLABILITY", "可冷卻性")}" : _sim.RiaCladdingFailure ? $" · {P("CLAD FAIL", "包殼失效")}" : ""),
            warnFrac: 230.0 / 300.0, id: "fuelEnth");
        AddGauge("Fuel centerline", "燃料中心溫", 0, 2800, () => _sim.FuelCenterlineTempC,
            () => $"{_sim.FuelCenterlineTempC:F0}°C · {P("surf", "表面")} {_sim.PelletSurfaceTempC:F0}°C · {P("peak", "峰棒")} {_sim.HotRodCenterlineTempC:F0}°C",
            warnFrac: 2000.0 / 2800.0, id: "fuelCl");
        AddGauge("Centerline-melt margin", "中心熔化裕度", 0, 2000, () => Math.Max(0, _sim.FuelCenterlineMeltMarginC),
            () => $"{_sim.FuelCenterlineMeltMarginC:F0}°C → {_sim.FuelMeltTempBurnupC:F0}°C ({P("melt@BU", "燃耗熔點")})"
                + (_sim.FuelCenterlineMeltMarginC < 0 ? $" · {P("MELT", "熔化")}" : ""),
            warnFrac: 1.0, id: "fcmMargin");
        AddGauge("Linear heat rate", "線功率密度", 0, 24, () => _sim.PeakLinearHeatRateKwPerFt,
            () => $"{P("avg", "均")} {_sim.LinearHeatRateKwPerFt:F1} · {P("peak", "峰")} {_sim.PeakLinearHeatRateKwPerFt:F1} kW/ft · {P("mgn", "裕")} {_sim.LhrMarginKwPerFt:+0.0;-0.0;0.0}",
            warnFrac: 21.0 / 24.0, id: "lhr");
        AddGauge("Gap conductance", "間隙熱導", 0, 4, () => _sim.GapConductanceWcm2K,
            () => $"h_gap {_sim.GapConductanceWcm2K:F2} W/cm²K · ΔT_gap {_sim.DeltaTGapC:F0}°C · FGR {_sim.FissionGasReleaseFraction * 100:F0}%",
            warnFrac: 1.1, id: "hgap");
        AddGauge("Coolant Tavg", "冷卻劑平均溫", 530, 620, () => _sim.Tavg * 1.8 + 32, () => $"{_sim.Tavg * 1.8 + 32:F0}°F", id: "tavg");
        AddGauge("Reference Tref", "參考溫度 Tref", 530, 620, () => _sim.Tref * 1.8 + 32, () => $"{_sim.Tref * 1.8 + 32:F0}°F", id: "tref");
        AddGauge("Coolant Thot", "熱腿溫度", 530, 660, () => _sim.Thot * 1.8 + 32, () => $"{_sim.Thot * 1.8 + 32:F0}°F", id: "thot");
        AddGauge("Coolant Tcold", "冷腿溫度", 520, 600, () => _sim.Tcold * 1.8 + 32, () => $"{_sim.Tcold * 1.8 + 32:F0}°F", id: "tcold");
        AddGauge("Subcooling", "過冷度", -20, 120, () => _sim.SubcoolingMarginC, () => $"{_sim.SubcoolingMarginC:F0}°C", id: "subcool");
        AddGauge("Min DNBR (W-3)", "最小 DNBR", 1.0, 4.0, () => _sim.MinDnbr,
            () => (_sim.MinDnbr >= 9.95 ? ">10" : $"{_sim.MinDnbr:F2}")
                + (_sim.RodsInDnbPercent > 0.1 ? P($" · {_sim.RodsInDnbPercent:F1}% rods DNB", $" · {_sim.RodsInDnbPercent:F1}% 棒DNB") : ""),
            id: "dnbr");
        AddGauge("Primary pressure", "一迴路壓力", 0, 3000, () => _sim.PrimaryPressure * 145.038, () => $"{_sim.PrimaryPressure * 145.038:F0} psia", id: "pzrPress");
        // 10 CFR 50 Appendix G P/T limit + LTOP gauges (representative aged-vessel curve).
        AddGauge("P/T limit margin", "P/T 限值裕量", -2, 13, () => _sim.PtMarginMPa,
            () => $"{_sim.PtMarginMPa:+0.0;-0.0;0.0} MPa · max {_sim.MaxAllowablePressureMPa * 145.038 - 14.7:F0} psig{(_sim.PtViolation ? " · VIOLATION" : "")}", id: "ptMargin");
        AddGauge("RCS heat/cool rate", "RCS 升降溫率", -60, 60, () => _sim.RcsRateCperHr,
            () => $"{_sim.RcsRateFperHr:+0;-0;0}°F/hr · {_sim.RcsRateCperHr:+0.0;-0.0;0.0}°C/hr (lim ±100°F/hr)", warnFrac: (50.0 + 60.0) / 120.0, id: "rcsRate");
        AddGauge("LTOP / COMS", "低溫超壓保護", 0, 2, () => _sim.LtopPorvOpen ? 2 : _sim.LtopArmed ? 1 : 0,
            () => _sim.LtopPorvOpen ? P("RELIEVING", "洩放中") : _sim.LtopArmed ? P("ARMED", "已致動") : P("Disarmed", "未致動"), warnFrac: 0.5, id: "ltop");
        // Pressurized Thermal Shock (PTS) — 10 CFR 50.61 vessel-embrittlement + transient fracture-mechanics monitor (display-only).
        AddGauge("RT_PTS embrittlement", "承壓熱衝擊參考溫度", 0, 320, () => _sim.RtPtsF,
            () => $"RT_PTS {_sim.RtPtsF:F0}°F · {P("screen", "篩選")} {_sim.PtsScreeningMarginF:+0;-0;0}°F @ {_sim.VesselEfpy:F0} EFPY{(_sim.PtsScreeningMarginF < 0 ? " · " + P("OVER", "越限") : "")}",
            warnFrac: ReactorSimService.PtsScreeningLimitF / 320.0);
        AddGauge("PTS Kᵢ / Kɪc", "承壓熱衝擊 Kᵢ/Kɪc", 0, 1.2, () => _sim.PtsKiTotalKsi / Math.Max(_sim.PtsKicAtWallKsi, 1e-6),
            () => $"K_I {_sim.PtsKiTotalKsi:F0} / K_IC {_sim.PtsKicAtWallKsi:F0} · {P("margin", "裕度")} {_sim.PtsMargin:F2}", warnFrac: 1.0 / 1.2);
        AddGauge("Vessel wall temp", "容器壁溫", 0, 600, () => _sim.VesselWallTempF,
            () => $"{_sim.VesselWallTempF:F0}°F{(_sim.PtsSusceptibleCondition ? " · " + P("PTS WATCH", "承壓熱衝擊警戒") : "")}");
        AddGauge("Pressurizer level", "穩壓器水位", 0, 100, () => _sim.PressurizerLevel, () => $"{_sim.PressurizerLevel:F0}%", id: "pzrLevel");
        AddGauge("Pressurizer temp", "穩壓器溫度", 80, 700, () => _sim.PressurizerLiquidTemp * 1.8 + 32, () => $"{_sim.PressurizerLiquidTemp * 1.8 + 32:F0}°F", id: "pzrTemp");
        AddGauge("Steam pressure", "蒸汽壓力", 0, 1300, () => _sim.SteamPressure * 145.038, () => $"{_sim.SteamPressure * 145.038:F0} psia", id: "sgPress");
        AddGauge("Containment press", "安全殼壓力", 0, 50, () => _sim.ContainmentPressurePsig, () => $"{_sim.ContainmentPressurePsig:F1} psig", warnFrac: 4.0 / 50.0, id: "ctmtPress");
        AddGauge("Containment temp", "安全殼溫度", 100, 300, () => _sim.ContainmentTempC * 1.8 + 32, () => $"{_sim.ContainmentTempC * 1.8 + 32:F0}°F", warnFrac: (200.0 - 100.0) / (300.0 - 100.0), id: "ctmtTemp");
        // Pressurizer Relief Tank (PRT / quench tank) — a hot, rising-pressure PRT is the stuck-open-relief cue (TMI-2).
        AddGauge("PRT pressure", "釋壓缸壓力", 0, 120, () => _sim.PrtPressurePsig,
            () => $"{_sim.PrtPressurePsig:F1} psig"
                + (_sim.PrtRuptureDiscBurst ? P(" · DISC BURST", " · 爆破片爆") : _sim.PrtDischarging ? P(" · discharging", " · 排放中") : ""),
            warnFrac: 8.0 / 120.0, id: "prtPress");
        AddGauge("PRT temp", "釋壓缸溫度", 80, 360, () => _sim.PrtWaterTempF, () => $"{_sim.PrtWaterTempF:F0}°F", warnFrac: (140.0 - 80.0) / (360.0 - 80.0), id: "prtTemp");
        AddGauge("PRT level", "釋壓缸水位", 0, 100, () => _sim.PrtWaterLevelPct, () => $"{_sim.PrtWaterLevelPct:F0}%", id: "prtLevel");
        AddGauge("SG level", "蒸發器水位", 0, 100, () => _sim.IndicatedSgLevel, () => $"{_sim.IndicatedSgLevel:F0}%", id: "sgLevel");
        AddGauge("Final feedwater temp", "最終給水溫度", 80, 480, () => _sim.FinalFeedwaterTempC * 1.8 + 32,
            () => $"{_sim.FinalFeedwaterTempC * 1.8 + 32:F0}°F"
                + (_sim.FeedwaterTempDeficitC > 1.0 ? P($" · −{_sim.FeedwaterTempDeficitC * 1.8:F0}°F", $" · 低{_sim.FeedwaterTempDeficitC * 1.8:F0}°F") : ""),
            id: "fwTemp");
        AddGauge("Secondary radiation", "二次側輻射", 0, 300, () => _sim.SecondaryRadiation, () => $"{_sim.SecondaryRadiation:F0} µSv/h", warnFrac: 100.0 / 300.0, id: "secRad");
        AddGauge("Atmospheric release", "累計大氣排放", 0, 100, () => _sim.AtmosphericRelease * 10, () => $"{_sim.AtmosphericRelease:F2}", id: "atmRel");
        // RCS coolant radiochemistry (LCO 3.4.16 / ANS-18.1 / RG 1.183): Dose-Equivalent I-131 & Xe-133 + rad monitors.
        AddGauge("RCS Dose-Equiv I-131", "一次側碘-131當量", 0, 70, () => _sim.RcsDeI131uCiPerG,
            () => $"{_sim.RcsDeI131uCiPerG:F2} µCi/g (LCO 1.0)" + (_sim.IodineSpikeActive ? $" · {P("SPIKE", "尖峰")} ×{_sim.IodineSpikeFactor:F0}" : ""),
            warnFrac: 1.0 / 70.0, id: "deI131");
        AddGauge("RCS Dose-Equiv Xe-133", "一次側氙-133當量", 0, 320, () => _sim.RcsDeXe133uCiPerG,
            () => $"{_sim.RcsDeXe133uCiPerG:F0} µCi/g (LCO 280)", warnFrac: 280.0 / 320.0, id: "deXe133");
        AddGauge("Main steam N-16 monitor", "主蒸汽管 N-16 監測", 0, 50000, () => _sim.N16MonitorUSvPerH,
            () => $"{_sim.N16MonitorUSvPerH / 1000.0:F1} mSv/h", id: "n16mon");
        AddGauge("Letdown rad monitor", "淨化排水輻射監測", 0, 600, () => _sim.LetdownMonitorUSvPerH,
            () => $"{_sim.LetdownMonitorUSvPerH:F1} µSv/h", warnFrac: 10.0 / 600.0, id: "letdownMon");
        // LOCA core-uncovery → Peak Cladding Temperature + 10 CFR 50.46(b) acceptance criteria.
        AddGauge("Peak clad temp (PCT)", "峰值包殼溫度", 300, 2500, () => _sim.PeakCladTempC, () => $"{_sim.PeakCladTempC:F0}°C · {_sim.PeakCladTempF:F0}°F (now {_sim.CladTempC:F0}°C)", id: "cladTemp");
        AddGauge("Core collapsed level", "堆芯塌陷水位", 0, 100, () => _sim.CollapsedLevelFrac * 100, () => $"{_sim.CollapsedLevelFrac * 100:F0}% · {_sim.CoreExposedFrac * 100:F0}% dry{(_sim.CladQuenching ? " · QUENCH" : "")}", id: "coreLevel");
        // RVLIS — post-TMI (NUREG-0737 II.F.2) reactor-vessel level instrumentation. Validity flips with RCP state.
        AddGauge("RVLIS full range", "RVLIS 全量程水位", 0, 100, () => _sim.RvlisFullRangePct, () => _sim.RvlisRange == RvlisValidRange.FullRange ? $"{_sim.RvlisFullRangePct:F0}% · TAF≈62%" : "— (pumps on)", id: "rvlisFull");
        AddGauge("RVLIS dynamic head", "RVLIS 動壓頭", 0, 120, () => _sim.RvlisDynamicHeadPct, () => _sim.RvlisRange == RvlisValidRange.DynamicHead ? $"{_sim.RvlisDynamicHeadPct:F0}% ΔP" : "— (pumps off)", id: "rvlisDh");
        AddGauge("RVLIS upper range", "RVLIS 上量程水位", 0, 100, () => _sim.RvlisUpperRangePct, () => _sim.RvlisRange == RvlisValidRange.FullRange ? $"{_sim.RvlisUpperRangePct:F0}%" : "— (pumps on)", id: "rvlisUpper");
        // ICC instrumentation (NUREG-0737 II.F.2): Core Exit Thermocouples + Subcooling Margin Monitor.
        // CET dial spans the qualified Type-K range (200–2300 °F); warnFrac marks the ICC ORANGE 700 °F entry.
        AddGauge("Core exit TC (CET)", "堆芯出口熱電偶 CET", 200, 2300, () => _sim.CoreExitTempF,
            () => $"{_sim.CoreExitTempF:F0}°F · {_sim.CoreExitTempC:F0}°C{(_sim.IccRed ? " · ICC RED" : _sim.IccOrange ? " · ICC ORG" : "")}",
            warnFrac: (700.0 - 200.0) / (2300.0 - 200.0), id: "cet");
        AddGauge("Subcooling margin (SMM)", "過冷度監測 SMM", -30, 120, () => _sim.CetSubcoolingMarginC,
            () => _sim.CetSubcoolingMarginC <= 0
                ? $"{-_sim.CetSubcoolingMarginC:F0}°C " + P("SUPERHEAT", "過熱")
                : $"{_sim.CetSubcoolingMarginC:F0}°C " + P("subcooled", "過冷"),
            warnFrac: (15.0 + 30.0) / (120.0 + 30.0), id: "smm");
        AddGauge("RCP seal leakoff", "主泵軸封洩漏", 0, 1920, () => _sim.SealLeakGpmTotal, () => $"{_sim.SealLeakGpmTotal:F0} gpm · {_sim.SealCavityMaxTempC:F0}°C{(_sim.SealCoolingAvailable ? "" : " · NO COOL")}", id: "sealLeak");
        // Component Cooling Water supply (cold-leg) temperature — the support-cooling chain (UHS → CCW → loads).
        // Warns at the 46.1 °C (115 °F) header-hi annunciator; the readout shows cold/hot legs, served flow and load.
        AddGauge("Component cooling water", "設備冷卻水 CCW", 0, 70, () => _sim.CcwColdTempC,
            () => $"{_sim.CcwColdTempC:F0}/{_sim.CcwHotTempC:F0}°C · {_sim.CcwFlowFrac * 100:F0}% · {_sim.CcwHeatLoadMw:F0} MW"
                  + (_sim.CcwAvailable ? "" : P(" · NO CCW", " · 喪失CCW")) + (_sim.LetdownIsolated ? P(" · LTDN ISO", " · 下泄隔離") : ""),
            warnFrac: 46.1 / 70.0, id: "ccwTemp");
        // Chemical & Volume Control System (CVCS): charging / letdown inventory balance, RCP seal injection,
        // the Volume Control Tank and the boric-acid / reactor-makeup-water blender. Charging is the revealed
        // mechanism behind the pressurizer-level loop; letdown reads ISO when the CCW latch isolates it.
        AddGauge("Charging flow", "上充流量", 0, 150, () => _sim.ChargingFlowGpm,
            () => $"{_sim.ChargingFlowGpm:F0} gpm" + P($" (chg {_sim.NormalChargingFlowGpm:F0}+seal {_sim.SealInjectionFlowGpm:F0})",
                                                       $"（充水 {_sim.NormalChargingFlowGpm:F0}＋軸封 {_sim.SealInjectionFlowGpm:F0}）"),
            id: "charging");
        AddGauge("Letdown flow", "下泄流量", 0, 165, () => _sim.LetdownFlowGpm,
            () => _sim.LetdownIsolated ? P("ISOLATED", "已隔離") : $"{_sim.LetdownFlowGpm:F0} gpm"
                  + (_sim.VctDivertFlowGpm > 0.5 ? P($" · divert {_sim.VctDivertFlowGpm:F0}", $" · 分流 {_sim.VctDivertFlowGpm:F0}") : ""),
            id: "letdown");
        AddGauge("VCT level", "容積控制缸液位", 0, 100, () => _sim.VctLevelPct,
            () => $"{_sim.VctLevelPct:F0}%" + (_sim.ChargingSuctionOnRwst ? P(" · RWST", " · 換料水缸") : ""),
            warnFrac: 0.85, id: "vctLevel");
        AddGauge("VCT pressure", "容積控制缸壓力", 0, 700, () => _sim.VctPressureKpa,
            () => $"{_sim.VctPressureKpa / 6.895:F0} psig · {_sim.VctPressureKpa:F0} kPa", warnFrac: 600.0 / 700.0, id: "vctPress");
        AddGauge("Makeup / blend", "補水／混合", 0, 120, () => _sim.MakeupBlendFlowGpm,
            () => _sim.MakeupBlendFlowGpm < 0.5 ? P("idle", "待命")
                  : $"{_sim.MakeupBlendFlowGpm:F0} gpm @ {_sim.MakeupBlendBoronPpm:F0} ppm"
                    + P($" (BA {_sim.BoricAcidFlowGpm:F0}/RMW {_sim.ReactorMakeupWaterFlowGpm:F0})",
                        $"（硼酸 {_sim.BoricAcidFlowGpm:F0}／淡水 {_sim.ReactorMakeupWaterFlowGpm:F0}）"),
            warnFrac: 0.9, id: "cvcsMakeup");
        // RCS operational LEAKAGE (LCO 3.4.13) + RG 1.45 / LCO 3.4.15 leak-detection instrumentation.
        // Unidentified LEAKAGE — limit 1 gpm; band warns at the LCO setpoint. Inferred-rate is the RG 1.45 sump channel.
        AddGauge("Unidentified leak", "未辨識洩漏", 0, 3, () => _sim.UnidentifiedLeakGpm,
            () => $"{_sim.UnidentifiedLeakGpm:F2} gpm (LCO 1.0) · " + P($"sump {_sim.SumpInferredLeakGpm:F2}", $"集水坑 {_sim.SumpInferredLeakGpm:F2}"),
            warnFrac: 1.0 / 3.0, id: "unidLeak");
        // Identified LEAKAGE — limit 10 gpm; degraded RCP-seal leakoff above the recovered #1-seal bleed-off.
        AddGauge("Identified leak", "已辨識洩漏", 0, 20, () => _sim.IdentifiedLeakGpm,
            () => $"{_sim.IdentifiedLeakGpm:F1} gpm (LCO 10)" + (_sim.PressureBoundaryLeakGpm > 0 ? P(" · PB LEAK!", " · 邊界洩漏！") : ""),
            warnFrac: 10.0 / 20.0, id: "identLeak");
        // Containment normal sump — integrated unidentified-leak inventory; auto-pumps at the hi setpoint.
        AddGauge("Containment sump", "安全殼集水坑", 0, 1200, () => _sim.ContainmentSumpGal,
            () => $"{_sim.ContainmentSumpGal:F0} gal · " + P($"bal {_sim.RcsInventoryBalanceLeakGpm:F2} gpm", $"平衡 {_sim.RcsInventoryBalanceLeakGpm:F2} gpm"),
            warnFrac: 1000.0 / 1200.0, id: "ctmtSump");
        // RG 1.45 containment-atmosphere radiation monitors (ratio; 1.0 = alarm setpoint), driven by live coolant activity.
        AddGauge("Particulate monitor", "顆粒物輻射監測", 0, 5, () => _sim.ParticulateMonitorRatio,
            () => $"×{_sim.ParticulateMonitorRatio:F2} " + P("(I-131)", "（碘-131）"), warnFrac: 1.0 / 5.0, id: "partMon");
        AddGauge("Gaseous monitor", "氣體輻射監測", 0, 5, () => _sim.GaseousMonitorRatio,
            () => $"×{_sim.GaseousMonitorRatio:F2} " + P("(Xe-133)", "（氙-133）"), warnFrac: 1.0 / 5.0, id: "gasMon");
        AddGauge("Clad oxidation (ECR)", "包殼氧化 ECR", 0, 30, () => _sim.MaxLocalOxidationPct, () => $"{_sim.MaxLocalOxidationPct:F1}% ECR", id: "ecr");
        AddGauge("Core hydrogen", "堆芯氫氣", 0, 3, () => _sim.CoreWideHydrogenPct, () => $"{_sim.CoreWideHydrogenPct:F2}% · {_sim.HydrogenMassKg:F0} kg", id: "h2");
        // Post-LOCA boric-acid precipitation (long-term core cooling, 10 CFR 50.46(b)(5)): core-mixing-region
        // boron concentrates by decay-heat boil-off of borated ECCS makeup; the danger band tracks the live
        // solubility limit Cs(T). Hot-leg recirc (ES-1.4) flushes the core. Idle ≈ well-mixed RCS boron.
        AddGauge("Core boron (precip)", "堆芯硼濃度（析出）", 0, 60000, () => _sim.CoreBoronPpm,
            () => _sim.Precipitated
                ? P($"PRECIPITATED · {_sim.CoreBoronPpm:F0} ppm B", $"已析出 · {_sim.CoreBoronPpm:F0} ppm B")
                : _sim.BoricConcentrationActive
                    ? $"{_sim.CoreBoronPpm:F0} ppm B · Cs {_sim.BoricSolubilityLimitPpm:F0}" + (_sim.HotLegRecircActive ? P(" · HL recirc", " · 熱段再循環") : "")
                    : $"{_sim.CoreBoronPpm:F0} ppm B", id: "coreBoron");
        // Operator-action window: hours to boric-acid precipitation (drives the ES-1.4 hot-leg-recirc decision).
        AddGauge("Time to precip", "距析出時間", 0, 8,
            () => _sim.BoricConcentrationActive && !_sim.Precipitated ? Math.Min(8, _sim.TimeToPrecipHours) : 8,
            () => _sim.Precipitated
                ? P("precipitated", "已析出")
                : _sim.BoricConcentrationActive
                    ? (double.IsInfinity(_sim.TimeToPrecipSeconds)
                        ? P("stable (recirc)", "穩定（再循環）")
                        : $"{_sim.TimeToPrecipHours:F1} h → {P("precip", "析出")}")
                    : P("—", "—"),
            warnFrac: 2.0 / 8.0, id: "timeToPrecip");
        // Containment combustible-gas control (10 CFR 50.44): wide-range H₂ monitor 0–10 vol% (RG 1.97 Cat 1),
        // O₂ for inerting/depletion, the passive-recombiner bank rate, and the igniter-system state.
        AddGauge("Containment H₂", "安全殼氫氣", 0, 10, () => _sim.ContainmentH2Pct,
            () => $"{_sim.ContainmentH2Pct:F1} vol%" + (_sim.ContainmentDetonable ? P(" · DETONABLE", " · 可爆炸") : _sim.ContainmentFlammable ? P(" · FLAMMABLE", " · 可燃") : ""),
            warnFrac: 4.0 / 10.0, id: "ctmtH2");
        AddGauge("Containment O₂", "安全殼氧氣", 0, 22, () => _sim.ContainmentO2Pct, () => $"{_sim.ContainmentO2Pct:F1} vol%", id: "ctmtO2");
        AddGauge("PAR recombiner rate", "被動複合器速率", 0, 260, () => _sim.ParRemovalKgPerHr, () => $"{_sim.ParRemovalKgPerHr:F0} kg/h" + P(" (passive)", "（被動）"), id: "parRate");
        AddGauge("H₂ igniters", "氫氣點火器", 0, 2, () => _sim.IgnitersEnergized ? 2 : _sim.IgniterSystemArmed ? 1 : 0,
            () => _sim.IgnitersEnergized ? $"{P("ENERGIZED", "已通電")} · {_sim.IgniterSurfaceTempC:F0}°C" : _sim.IgniterSystemArmed ? P("ARMED (no power)", "已備妥（無電）") : P("Off", "關"),
            warnFrac: 0.5, id: "igniters");
        AddGauge("Last H₂ burn peak", "上次氫燃峰值", 0, 350, () => _sim.LastBurnPeakKpa / 6.895,
            () => _sim.DeflagrationOccurred ? $"{_sim.LastBurnPeakKpa / 6.895:F0} psig · {_sim.LastBurnPeakTempC:F0}°C" : P("no burn", "未燃燒"), id: "h2Burn");
        AddGauge("RCP flow", "主泵流量", 0, 100, () => _sim.CoolantFlowFraction * 100, () => $"{_sim.CoolantFlowFraction * 100:F0}%{FlowModeTag()}", id: "flow");
        AddGauge("Boron", "硼濃度", 0, 2500, () => _sim.BoronPpm,
            () => $"{_sim.BoronPpm:F0} ppm · {P("DBW", "硼微分價值")} {_sim.DifferentialBoronWorthPcmPerPpm:F2} pcm/ppm"
                + (_sim.DilutionFlowGpm > 0 ? $" · {P("dilute", "稀釋")} {_sim.DilutionFlowGpm:F0} gpm" : ""), id: "boron");
        // Boron-dilution operator-action window (FSAR 15.4.6): minutes to loss of shutdown margin; the
        // gauge bands flag the 15-min (Modes 1–5) / 30-min (Mode 6) SRP criteria. Reads 60 (off-scale clean) when idle.
        AddGauge("Dilution window", "稀釋裕度時間", 0, 60,
            () => _sim.DilutionFlowGpm > 0 ? Math.Min(60, _sim.TimeToCriticalityMinutes) : 60,
            () => _sim.DilutionFlowGpm > 0
                ? (double.IsInfinity(_sim.TimeToCriticalitySeconds)
                    ? P("no criticality", "不會臨界")
                    : $"{_sim.TimeToCriticalityMinutes:F1} min → {P("crit", "臨界")} · SDM {_sim.ShutdownMarginPcm:F0} pcm")
                : P("—", "—"),
            id: "dilutionWindow");
        AddGauge("Xenon worth", "氙毒", 0, 100, () => _sim.Xenon * 100, () => $"{-_sim.XenonReactivityPcm:F0} pcm", id: "xenon");
        AddGauge("Samarium worth", "釤毒", 0, 300, () => _sim.Samarium * 100, () => $"{-_sim.SamariumReactivityPcm:F0} pcm", id: "samarium");
        // ---- Fuel-cycle core depletion (burnup) ----
        AddGauge("Core burnup", "堆芯燃耗", 0, 18, () => _sim.BurnupMwdPerTonne / 1000.0,
            () => $"{_sim.BurnupMwdPerTonne / 1000.0:F2} GWd/tU · {_sim.CycleEfpd:F0} EFPD · {P(_sim.CoreLifePhaseEn, _sim.CoreLifePhaseZh)}" +
                  (_sim.EasyStartupMode ? P(" · EASY burn ×1.75", " · EASY 燃耗 ×1.75") : "") +
                  (_sim.WasteProductionMultiplier > 1.01 ? P($" · waste ×{_sim.WasteProductionMultiplier:0.##}", $" · 廢料 ×{_sim.WasteProductionMultiplier:0.##}") : ""),
            warnFrac: 1.2, id: "burnup");
        // Boron letdown target + the cycle drift of MTC and the dollar (β_eff): all anchored to today's BOL values.
        AddGauge("Boron letdown", "降硼曲線", 0, 1400, () => _sim.CriticalBoronPpm,
            () => $"{_sim.CriticalBoronPpm:F0} ppm tgt · MTC {_sim.EffectiveMtcPcmPerC:F0} · 1$={_sim.BetaEffectivePcm:F0} pcm",
            warnFrac: 1.2, id: "boronLetdown");
        AddGauge("Axial flux diff", "軸向通量差", -30, 30, () => _sim.AxialFluxDifferencePercent, () => $"ΔI {_sim.AxialFluxDifferencePercent:+0.0;-0.0;0.0}% · AO {_sim.AxialOffsetPercent:+0.0;-0.0;0.0}%", id: "afd");
        // QPTR (Quadrant Power Tilt Ratio, LCO 3.2.4): ex-core N-41…44 azimuthal tilt. 1.00 flat, limit 1.02.
        AddGauge("Quad tilt (QPTR)", "象限傾斜 QPTR", 0.95, 1.15, () => _sim.Qptr,
            () => _sim.QptrOutOfLimit
                ? $"{_sim.Qptr:F3} · " + P($"REDUCE −{_sim.QptrRequiredPowerReductionPct:F0}%", $"降功率 −{_sim.QptrRequiredPowerReductionPct:F0}%")
                : $"{_sim.Qptr:F3}" + (_sim.DroppedRodActive ? " · " + P($"Q{_sim.DroppedRodQuadrant + 1} rod", $"Q{_sim.DroppedRodQuadrant + 1} 落棒") : ""),
            warnFrac: (1.02 - 0.95) / (1.15 - 0.95), id: "qptr");
        AddGauge("Turbine speed", "汽輪機轉速", 0, 2000, () => _sim.TurbineRPM, () => $"{_sim.TurbineRPM:F0} rpm");
        // EHC turbine — first-stage (impulse) pressure is the calibrated load signal; governor-valve position.
        AddGauge("First-stage press", "第一級壓力", 0, 750, () => _sim.FirstStagePressure * 690.0, () => $"{_sim.FirstStagePressure * 690.0:F0} psia");
        AddGauge("Governor valve", "調速汽門", 0, 100, () => _sim.GovernorValve * 100, () => $"{_sim.GovernorValve * 100:F0}%");
        // Main generator electrical — excitation/AVR reactive side. Governor sets MW; the AVR sets MVAR/voltage.
        // At 1150 MWe / 0.90 PF the machine puts ~557 MVAR lagging onto the grid at 24 kV, 60.0 Hz.
        AddGauge("Reactive power", "無功功率", -400, 600, () => _sim.ReactiveMVAR,
            () => $"{_sim.ReactiveMVAR:F0} MVAR {(_sim.PowerFactorLeading ? P("lead", "超前") : P("lag", "滯後"))}", id: "genMvar");
        AddGauge("Terminal voltage", "機端電壓", 20, 27, () => _sim.TerminalKV, () => $"{_sim.TerminalKV:F1} kV", id: "genKv");
        AddGauge("Power factor", "功率因數", 0.80, 1.00, () => _sim.PowerFactor,
            () => $"{_sim.PowerFactor:F3} {(_sim.PowerFactorLeading ? P("lead", "超前") : P("lag", "滯後"))}", id: "genPf");
        AddGauge("Grid frequency", "電網頻率", 57, 63, () => _sim.GridFrequencyHz,
            () => _sim.GeneratorBreakerClosed ? $"{_sim.GridFrequencyHz:F2} Hz" : $"{_sim.GridFrequencyHz:F2} Hz · {_sim.SyncPhaseAngleDeg:F0}°", id: "genHz");
        AddGauge("Field current", "勵磁電流", 0, 2.6, () => _sim.FieldCurrentPu, () => $"{_sim.FieldCurrentPu:F2} pu", id: "genIfd");
        // Steam dump (turbine bypass) — 40% condenser dump that rides out a load rejection / turbine trip
        // without a reactor trip. Shows % of dump capacity and the active controller mode.
        AddGauge("Steam dump", "蒸汽旁路", 0, 100, () => _sim.SteamDumpPercent,
            () => $"{_sim.SteamDumpPercent:F0}% · {SteamDumpModeStr()}", id: "steamDump");
        // Main condenser backpressure — absolute exhaust pressure in inHgA (lower = deeper vacuum = more output).
        // Shows the output-correction factor; warns as vacuum degrades toward the dump-inhibit / turbine-trip band.
        AddGauge("Condenser vacuum", "凝汽器真空", 0, 10, () => _sim.CondenserPressureInHg,
            () => $"{_sim.CondenserPressureInHg:F1} inHgA · {_sim.CondenserPressureKpa:F1} kPa · ×{_sim.CondenserVacuumOutputFactor:F3}", id: "condvac");
        // Moisture Separator Reheater (MSR) — the saturated-cycle wet-steam conditioner between the HP and LP
        // turbines. Hot-reheat temperature, LP last-stage exhaust moisture (the blade-erosion / Baumann driver)
        // and the resulting gross-output credit factor. Warn bands: low reheat and >13% LP exhaust moisture.
        AddGauge("Hot reheat temp", "熱再熱溫度", 150, 300, () => _sim.HotReheatTempC,
            () => $"{_sim.HotReheatTempC:F0} °C · ΔTsh {_sim.LpInletSuperheatC:F0} °C" + (_sim.MsrAvailable ? "" : P(" · BYPASS", " · 旁通")),
            warnFrac: (245.0 - 150.0) / (300.0 - 150.0), id: "msrReheat");
        AddGauge("LP exhaust moisture", "低壓排汽濕度", 0, 16, () => _sim.LpExhaustMoisturePct,
            () => $"{_sim.LpExhaustMoisturePct:F1}% · HP {_sim.HpExhaustMoisturePct:F1}% → sep {_sim.SeparatorOutMoisturePct:F2}%",
            warnFrac: 13.0 / 16.0, id: "msrLpMoist");
        AddGauge("MSR output credit", "MSR 出力修正", 0.96, 1.02, () => _sim.MsrOutputFactor,
            () => $"×{_sim.MsrOutputFactor:F3} · " + P("drain ", "疏水 ") + $"{_sim.ReheaterDrainPct:F0}%", id: "msrFactor");
        // Class 1E 125 VDC station battery — only depletes during a station blackout (no AC source).
        AddGauge("Vital DC battery", "1E 直流電池", 0, 100, () => _sim.Electrical.BatterySoc * 100,
            () => $"{_sim.Electrical.BatterySoc * 100:F0}% · {_sim.Electrical.BatteryVoltage:F0} VDC", id: "battery");
    }

    private string PeriodStr()
    {
        double p = _sim.ReactorPeriodSeconds;
        if (Math.Abs(p) >= 999) return "∞";
        return $"{p:F0}s";
    }

    // Bilingual label for the steam-dump controller mode (sim emits a fixed English token).
    private string SteamDumpModeStr() => _sim.SteamDumpModeEn switch
    {
        "LoadReject" => P("Load Reject", "甩負荷"),
        "TripOpen"   => P("Trip Open", "跳機全開"),
        "Blocked"    => P("Lo-Tavg Block", "低溫閉鎖"),
        "NoCondenser"=> P("No Condenser", "凝汽器不可用"),
        "Armed"      => P("Armed", "待命"),
        _            => P("Off", "關"),
    };

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
            (ReactorAlarm.SteamSafetyValveOpen, "MAIN STEAM SAFETY OPEN", "主蒸汽安全閥起跳"),
            (ReactorAlarm.EccsActive, "ECCS ACTIVE", "應急堆芯冷卻"),
            (ReactorAlarm.TurbineTrip, "TURBINE TRIP", "汽輪機跳機"),
            (ReactorAlarm.CondenserVacuumLow, "CONDENSER VACUUM LOW", "凝汽器真空低"),
            (ReactorAlarm.MsrHighLpMoisture, "HIGH LP EXHAUST MOISTURE", "低壓缸排汽濕度高"),
            (ReactorAlarm.MsrLowReheat, "MSR LOW REHEAT", "再熱蒸汽溫度低"),
            (ReactorAlarm.LowFeedwaterTemp, "LOW FEEDWATER TEMP (15.1.1)", "給水低溫（15.1.1）"),
            (ReactorAlarm.LowSubcooling, "LOW SUBCOOLING", "過冷度不足"),
            (ReactorAlarm.DecayHeatHigh, "DECAY HEAT", "衰變熱高"),
            (ReactorAlarm.AtwsActive, "ATWS — RODS STUCK", "ATWS 控制棒卡住"),
            (ReactorAlarm.AmsacActuated, "AMSAC ACTUATED", "AMSAC 致動"),
            (ReactorAlarm.AccumulatorInject, "ACCUM INJECT", "蓄壓器注入"),
            (ReactorAlarm.AuxFeedwater, "AUX FEEDWATER", "輔助給水"),
            (ReactorAlarm.NaturalCirc, "NATURAL CIRC", "自然循環"),
            (ReactorAlarm.SgtrLeak, "SGTR LEAK", "蒸發器爆管洩漏"),
            (ReactorAlarm.SecondaryRadiationHi, "2NDARY RAD HI", "二次側輻射高"),
            (ReactorAlarm.RcsDeI131LcoExceeded, "RCS DEI-131 > 1.0 (LCO 3.4.16)", "一次側碘-131當量 >1.0（LCO 3.4.16）"),
            (ReactorAlarm.RcsDeI131SpikeLimit, "DEI-131 SPIKE > 60 µCi/g", "碘當量尖峰 >60 µCi/g"),
            (ReactorAlarm.RcsDeXe133LcoExceeded, "NOBLE GAS DEX-133 > 280", "惰性氣體氙當量 >280"),
            (ReactorAlarm.IodineSpikeInProgress, "IODINE SPIKE (RG 1.183)", "碘尖峰進行中（RG 1.183）"),
            (ReactorAlarm.BoronDilution, "BORON DILUTION (15.4.6)", "硼稀釋偵測（15.4.6）"),
            (ReactorAlarm.BoronDilutionActionWindow, "DILUTION < 15-MIN WINDOW", "稀釋 <15分鐘裕度"),
            (ReactorAlarm.BoricAcidPrecipApproach, "BORIC ACID PRECIP — GO ES-1.4", "硼酸即將析出 — 轉熱段再循環"),
            (ReactorAlarm.BoricAcidPrecipitated, "BORIC ACID PRECIPITATED", "硼酸已析出 堆芯流道阻塞"),
            (ReactorAlarm.SgReliefLift, "SG RELIEF — RELEASE", "蒸發器釋壓閥洩放"),
            (ReactorAlarm.SteamlineBreak, "STEAMLINE BREAK", "主蒸汽管爆裂"),
            (ReactorAlarm.SafetyInjection, "SAFETY INJECTION", "安全注入 SI"),
            (ReactorAlarm.ContainmentPressureHi, "CTMT PRESS HI", "安全殼壓力高"),
            (ReactorAlarm.ContainmentIsolation, "CTMT ISOLATION", "安全殼隔離"),
            (ReactorAlarm.ContainmentSpray, "CTMT SPRAY", "安全殼噴淋"),
            (ReactorAlarm.PzrCodeSafetyOpen, "PZR SAFETY OPEN", "穩壓器安全閥起跳"),
            (ReactorAlarm.PrtPressureHi, "PRT PRESS HI", "釋壓缸壓力高"),
            (ReactorAlarm.PrtTempHi, "PRT TEMP HI", "釋壓缸溫度高"),
            (ReactorAlarm.PrtLevelAbnormal, "PRT LEVEL ABNORMAL", "釋壓缸水位異常"),
            (ReactorAlarm.PrtRuptureDisc, "PRT RUPTURE DISC BURST", "釋壓缸爆破片爆裂"),
            (ReactorAlarm.RodInsertionLimitLo, "ROD INS LIMIT LO", "控制棒插入限值 低"),
            (ReactorAlarm.RodInsertionLimitLoLo, "ROD INS LIMIT LO-LO", "控制棒插入限值 低低"),
            (ReactorAlarm.RodDeviation, "ROD DEVIATION", "控制棒偏差"),
            (ReactorAlarm.AxialFluxDiffOutOfBand, "AFD OUT OF BAND", "軸向通量差超限"),
            (ReactorAlarm.QuadrantPowerTiltHi, "QPTR > 1.02 (LCO 3.2.4)", "象限傾斜 >1.02"),
            (ReactorAlarm.DroppedRcca, "DROPPED RCCA — ROD BOTTOM", "落棒 — 控制棒到底"),
            (ReactorAlarm.RodEjectionAccident, "ROD EJECTION (RIA)", "彈棒事故 RIA"),
            (ReactorAlarm.RccaWithdrawalAccident, "RCCA WITHDRAWAL (15.4.1/2)", "失控提棒（15.4.1/2）"),
            (ReactorAlarm.FuelEnthalpyLimit, "FUEL ENTHALPY > 230 cal/g", "燃料焓 >230 cal/g"),
            (ReactorAlarm.RiaCladFailure, "RIA FUEL FAILURE", "彈棒燃料失效"),
            (ReactorAlarm.CoreDamage, "CORE DAMAGE", "爐心受損"),
            (ReactorAlarm.LossOfOffsitePower, "LOSS OF OFFSITE PWR", "喪失廠外電源"),
            (ReactorAlarm.StationBlackout, "STATION BLACKOUT", "全廠斷電 SBO"),
            (ReactorAlarm.EdgSupplyingBus, "EDG ON BUS", "應急柴油發電機供電"),
            (ReactorAlarm.TurbineDrivenAfw, "TURBINE-DRIVEN AFW", "汽動輔助給水"),
            (ReactorAlarm.DcBusDepleted, "DC BUS DEPLETED", "直流電源耗盡"),
            (ReactorAlarm.CoreUncovered, "CORE UNCOVERY", "堆芯裸露"),
            (ReactorAlarm.PeakCladTempLimit, "PCT > 2200°F (50.46)", "峰值包殼溫度超限 50.46"),
            (ReactorAlarm.CladOxidationLimit, "CLAD OXID > 17% ECR", "包殼氧化 >17% ECR"),
            (ReactorAlarm.HydrogenGenerationLimit, "CORE H₂ > 1%", "堆芯氫氣 >1%"),
            (ReactorAlarm.ContainmentH2Flammable, "CTMT H₂ FLAMMABLE", "安全殼氫氣可燃"),
            (ReactorAlarm.ContainmentH2RegLimit, "CTMT H₂ > 10% (50.44)", "安全殼氫氣 >10% 50.44"),
            (ReactorAlarm.ContainmentH2Detonable, "CTMT H₂ DETONABLE", "安全殼氫氣可爆炸"),
            (ReactorAlarm.IgnitersEnergized, "H₂ IGNITERS ON", "氫氣點火器通電"),
            (ReactorAlarm.ContainmentDeflagration, "H₂ DEFLAGRATION", "氫氣爆燃"),
            (ReactorAlarm.DnbrLowMargin, "DNBR LOW MARGIN", "DNBR 餘裕偏低"),
            (ReactorAlarm.DnbrSafetyLimit, "DNBR < 1.30 (S.L.)", "DNBR <1.30 安全限值"),
            (ReactorAlarm.RcpLockedRotor, "RCP LOCKED ROTOR (15.3.3)", "主泵卡軸（15.3.3）"),
            (ReactorAlarm.RodsInDnbHi, "RODS IN DNB > 5%", "DNB 燃料棒 >5%"),
            (ReactorAlarm.RcpSealLoca, "RCP SEAL LOCA", "主泵軸封失水"),
            (ReactorAlarm.RvlisBelowTopOfFuel, "RVLIS < TOP OF FUEL", "RVLIS 低於燃料頂"),
            (ReactorAlarm.RvlisFullRangeLoLo, "RVLIS LEVEL LO-LO", "RVLIS 水位 低低"),
            (ReactorAlarm.IccOrange, "ICC ORANGE (FR-C.2)", "堆芯冷卻 橙 FR-C.2"),
            (ReactorAlarm.IccRed, "ICC RED — CET >1200°F", "堆芯冷卻 紅 CET>1200°F"),
            (ReactorAlarm.PtApproach, "P/T LIMIT APPROACH", "P/T 限值接近"),
            (ReactorAlarm.PtViolation, "APP G P/T VIOLATION", "附錄G P/T 越限"),
            (ReactorAlarm.RcsRateExceeded, "RCS HEAT/COOL RATE HI", "RCS 升降溫率過高"),
            (ReactorAlarm.LtopActive, "LTOP/COMS RELIEVING", "低溫超壓保護洩放"),
            (ReactorAlarm.PtsSusceptible, "PTS SUSCEPTIBLE COND.", "承壓熱衝擊敏感工況"),
            (ReactorAlarm.PtsFlawInitiation, "PTS FLAW INITIATION", "承壓熱衝擊裂紋起裂"),
            (ReactorAlarm.RcsUnidentifiedLeakHi, "UNID LEAK > 1 GPM", "未辨識洩漏 >1 GPM"),
            (ReactorAlarm.RcsIdentifiedLeakHi, "IDENT LEAK > 10 GPM", "已辨識洩漏 >10 GPM"),
            (ReactorAlarm.RcsPressureBoundaryLeak, "PRESS BDY LEAKAGE", "壓力邊界洩漏"),
            (ReactorAlarm.ContainmentParticulateRadHi, "CTMT PART. RAD HI", "安全殼顆粒輻射高"),
            (ReactorAlarm.ContainmentGaseousRadHi, "CTMT GAS RAD HI", "安全殼氣體輻射高"),
            (ReactorAlarm.CcwHeaderTempHi, "CCW HEADER TEMP HI", "設備冷卻水母管高溫"),
            (ReactorAlarm.CcwLowFlow, "CCW LOW FLOW", "設備冷卻水低流量"),
            (ReactorAlarm.CcwSurgeTankAbnormal, "CCW SURGE TANK ABNORMAL", "設備冷卻水穩壓缸水位異常"),
            (ReactorAlarm.UhsTempHi, "UHS TEMP HI (SR 3.7.9.1)", "最終熱阱高溫（SR 3.7.9.1）"),
            (ReactorAlarm.LetdownIsolatedCcw, "LETDOWN ISOLATED (CCW)", "淨化下泄已隔離（設備冷卻水）"),
            (ReactorAlarm.VctLowLevelMakeup, "VCT LO LVL · MAKEUP", "容積控制缸低液位·補水"),
            (ReactorAlarm.VctHighLevelDivert, "VCT HI LVL · DIVERT", "容積控制缸高液位·分流"),
            (ReactorAlarm.ChargingSuctionRwst, "CHG SUCTION → RWST", "上充吸入切換換料水缸"),
            (ReactorAlarm.NisCalorimetricDeviation, "PR NIS vs CALORIMETRIC > 2%", "功率量程偏離熱平衡 >2%"),
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
            // Register the window with the ISA-18.1 sequencer (defs order = first-out priority order).
            _annKeys.Add(a);
            _annState[a] = AnnState.Normal;
            _annPrevRaw[a] = false;
            _relocalizers.Add(() => label.Text = P(en, zh));
            label.Text = P(en, zh);
            AlarmPanel.Children.Add(border);
        }
    }

    private void UpdateAlarmTiles()
    {
        bool fast = AnnFastOn;   // 2 Hz — new (unacknowledged) alarm
        bool slow = AnnSlowOn;   // 1 Hz — ringback (cleared, awaiting reset)
        foreach (var kv in _alarmTiles)
        {
            var t = kv.Value;
            // TEST: every lamp asserted (both colors) so the operator can spot a dead tile — never alters state.
            if (_lampTestHeld)
            {
                t.Border.Background = new SolidColorBrush(Color.FromArgb(255, 0xF5, 0x7C, 0x00));
                t.Border.BorderBrush = new SolidColorBrush(Colors.White);
                t.Border.BorderThickness = new Thickness(1);
                t.Label.Foreground = new SolidColorBrush(Colors.White);
                continue;
            }

            var s = _annState.TryGetValue(kv.Key, out var st) ? st : AnnState.Normal;
            bool critical = kv.Key is ReactorAlarm.CoreDamage or ReactorAlarm.Scram or ReactorAlarm.HighFuelTemp or ReactorAlarm.HighPressure;
            Color bright = critical ? Color.FromArgb(255, 0xD3, 0x2F, 0x2F) : Color.FromArgb(255, 0xF5, 0x7C, 0x00);
            Color dim    = critical ? Color.FromArgb(255, 0x7F, 0x1D, 0x1D) : Color.FromArgb(255, 0x8A, 0x46, 0x00);

            switch (s)
            {
                case AnnState.FastFlashFirstOut:
                    // First-out: distinct magenta fast-flash + thick white border marks the trip initiator.
                    t.Border.Background = new SolidColorBrush(fast ? Color.FromArgb(255, 0xE0, 0x40, 0xFF) : Color.FromArgb(255, 0x55, 0x10, 0x66));
                    t.Border.BorderBrush = new SolidColorBrush(Colors.White);
                    t.Border.BorderThickness = new Thickness(3);
                    t.Label.Foreground = new SolidColorBrush(Colors.White);
                    break;

                case AnnState.FastFlash:
                    // New alarm: bright fast-flash + horn.
                    t.Border.Background = new SolidColorBrush(fast ? bright : dim);
                    t.Border.BorderBrush = new SolidColorBrush(Colors.White);
                    t.Border.BorderThickness = new Thickness(1);
                    t.Label.Foreground = new SolidColorBrush(Colors.White);
                    break;

                case AnnState.Acked:
                    // Acknowledged: steady-on, horn off. First-out window keeps its white border as the initiator.
                    t.Border.Background = new SolidColorBrush(bright);
                    t.Border.BorderBrush = new SolidColorBrush(_firstOut == kv.Key ? Colors.White : Color.FromArgb(180, 0xDD, 0xDD, 0xDD));
                    t.Border.BorderThickness = new Thickness(_firstOut == kv.Key ? 3 : 1);
                    t.Label.Foreground = new SolidColorBrush(Colors.White);
                    break;

                case AnnState.Ringback:
                    // Cleared but not reset: slow teal flash + soft ringback chime until RESET.
                    t.Border.Background = new SolidColorBrush(slow ? Color.FromArgb(255, 0x00, 0x6B, 0x6B) : Color.FromArgb(255, 0x06, 0x24, 0x24));
                    t.Border.BorderBrush = new SolidColorBrush(slow ? Color.FromArgb(220, 0x4D, 0xD0, 0xC8) : Color.FromArgb(120, 0x2E, 0x6E, 0x6A));
                    t.Border.BorderThickness = new Thickness(1);
                    t.Label.Foreground = new SolidColorBrush(Color.FromArgb(230, 0xCC, 0xF7, 0xF4));
                    break;

                default: // Normal
                    t.Border.Background = new SolidColorBrush(Color.FromArgb(40, 0x88, 0x88, 0x88));
                    t.Border.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 0xAA, 0xAA, 0xAA));
                    t.Border.BorderThickness = new Thickness(1);
                    t.Label.Foreground = new SolidColorBrush(Color.FromArgb(160, 0xCC, 0xCC, 0xCC));
                    break;
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

        // Abstract containment boundary: conceptual training graphic, not a physical layout.
        var ctmt = new Rectangle
        {
            Width = 415,
            Height = 300,
            RadiusX = 26,
            RadiusY = 26,
            Fill = new SolidColorBrush(Color.FromArgb(18, 0x77, 0xAA, 0xFF)),
            Stroke = new SolidColorBrush(Color.FromArgb(170, 0x6D, 0x9E, 0xCC)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 8, 5 },
        };
        Canvas.SetLeft(ctmt, 20);
        Canvas.SetTop(ctmt, 24);
        c.Children.Add(ctmt);
        AddStaticText(c, 34, 32, "Containment boundary · 安全殼邊界");

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
        // Cooling water / heat sink and safety support blocks.
        AddBox(c, 500, 300, 215, 34, "Cooling water / heat sink · 冷卻水／熱沉", Color.FromArgb(32, 0x22, 0x77, 0x99));
        AddBox(c, 30, 292, 165, 36, "SI / AFW support · 安全注入／輔助給水", Color.FromArgb(30, 0x22, 0x88, 0x55));

        // Primary loop pipes (will be recolored dynamically) — store references via tags through dynamic layer instead.
        // Secondary steam line
        AddPipe(c, 420, 110, 560, 110, stroke);   // SG -> turbine (steam)
        AddPipe(c, 670, 125, 708, 125, stroke);   // turbine -> generator
        AddPipe(c, 615, 160, 615, 220, stroke);   // turbine -> condenser
        AddPipe(c, 560, 250, 420, 250, stroke);   // condenser -> SG (feedwater)
        AddPipe(c, 420, 250, 420, 230, stroke);
        AddPipe(c, 560, 280, 560, 300, stroke);   // condenser -> cooling water
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
        // Safety injection / auxiliary feedwater conceptual support paths.
        if (_sim.EccsInjecting || _sim.AccumulatorInjecting)
            AddDynPipe(c, 110, 292, 110, 250, Color.FromArgb(255, 0x35, 0xD0, 0x7F), 4);
        if (_sim.AuxFeedwaterRunning)
            AddDynPipe(c, 195, 310, 330, 230, Color.FromArgb(255, 0x4C, 0xC2, 0xFF), 4);
        if (_sim.ContainmentSprayActive)
        {
            for (int i = 0; i < 6; i++)
            {
                var drop = new Ellipse { Width = 5, Height = 9, Fill = new SolidColorBrush(Color.FromArgb(180, 0x4C, 0xC2, 0xFF)) };
                Canvas.SetLeft(drop, 70 + i * 45);
                Canvas.SetTop(drop, 58 + ((_flashPhase * 60 + i * 11) % 34));
                c.Children.Add(drop); _mimicDynamic.Add(drop);
            }
        }

        // Flow particles (animate when pumps running)
        double phase = (_flashPhase * 1.5) % 1.0;
        if (_sim.CoolantFlowFraction > 0.02)
        {
            DrawFlowDot(c, 160, 160, 330, 160, phase, hot);
            DrawFlowDot(c, 330, 240, 234, 200, phase, cold);
        }
        // Steam flow when steaming — gate on the REAL governor-valve flow, not the operator demand.
        if (_sim.SteamPressure > 3.0 && _sim.GovernorValve > 0.02 && !_sim.TurbineTripped)
            DrawFlowDot(c, 420, 110, 560, 110, phase, Color.FromArgb(255, 0xCC, 0xDD, 0xEE));
        if (_sim.FeedwaterFlow > 0.02)
            DrawFlowDot(c, 560, 250, 420, 250, phase, Color.FromArgb(255, 0x90, 0xCA, 0xF9));
        if (_sim.CondenserAvailable)
            DrawFlowDot(c, 560, 280, 560, 300, phase, Color.FromArgb(255, 0x4C, 0xC2, 0xFF));
        if (_sim.ReliefValveOpen || _sim.AnyPzrCodeSafetyOpen || _sim.MssvLifted)
            AddDynLabel(c, 430, 72, P("Steam relief / safety lift", "蒸汽釋放／安全閥起跳"));

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
        // Generator electrical (reactive side + synchroscope) under the MWe readout.
        AddDynLabel(c, 715, 180, $"{_sim.ReactiveMVAR:F0} MVAR · {_sim.PowerFactor:F2} PF");
        AddDynLabel(c, 715, 200, $"{_sim.TerminalKV:F1} kV · {_sim.GridFrequencyHz:F1} Hz");
        AddDynLabel(c, 560, 185, _sim.GeneratorLockout86 ? "86 LOCKOUT"
            : _sim.GeneratorBreakerClosed ? "SYNCED"
            : $"{_sim.SyncPhaseAngleDeg:F0}° · {_sim.SlipHz:+0.00;-0.00} Hz");

        // Concept status cards keep the mimic useful at a glance without exposing operating procedures.
        AddDynStatusCard(c, 805, 38, P("Core heat", "爐心熱量"), CoreHeatText(), CoreHeatColor());
        AddDynStatusCard(c, 805, 94, P("Heat removal", "移熱能力"), HeatRemovalText(), HeatRemovalColor());
        AddDynStatusCard(c, 805, 150, P("Containment", "安全殼"), ContainmentText(), ContainmentColor());
        AddDynStatusCard(c, 805, 206, P("Electrical", "電力支援"), ElectricalText(), ElectricalColor());
        AddDynStatusCard(c, 805, 262, P("Chemistry", "化學／水質"), ChemistryText(), ChemistryColor());
    }

    private string CoreHeatText()
    {
        if (_sim.Mode == ReactorMode.Meltdown) return P("Damage / melt", "受損／熔毀");
        if (_sim.IsScrammed) return P("Trip heat", "跳堆餘熱");
        if (_sim.NeutronPowerFraction > 1.0) return P("High power", "高功率");
        if (_sim.NeutronPowerFraction > 0.05) return P("Generating", "發電中");
        return P("Low / source", "低功率／源區");
    }

    private Color CoreHeatColor()
        => _sim.Mode == ReactorMode.Meltdown || _sim.DamageAccumulation > 1.0 ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
         : _sim.IsScrammed || _sim.NeutronPowerFraction > 1.0 ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
         : Color.FromArgb(255, 0x35, 0xD0, 0x7F);

    private string HeatRemovalText()
    {
        bool lowDemand = _sim.NeutronPowerFraction < 0.02 && _sim.FuelTemp < 120.0;
        if (lowDemand) return P("Standby / low demand", "備用／低熱量");
        if (_sim.CoolantFlowFraction < 0.05 && _sim.FeedwaterFlow < 0.05 && !_sim.AuxFeedwaterRunning) return P("Lost / challenged", "喪失／受挑戰");
        if (_sim.AuxFeedwaterRunning || _sim.EccsInjecting || _sim.CoolantFlowFraction < 0.4) return P("Mitigating", "緩解中");
        return P("Available", "可用");
    }

    private Color HeatRemovalColor()
        => _sim.NeutronPowerFraction < 0.02 && _sim.FuelTemp < 120.0 ? Color.FromArgb(255, 0x35, 0xD0, 0x7F)
         : _sim.CoolantFlowFraction < 0.05 && _sim.FeedwaterFlow < 0.05 && !_sim.AuxFeedwaterRunning ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
         : _sim.AuxFeedwaterRunning || _sim.EccsInjecting || _sim.CoolantFlowFraction < 0.4 ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
         : Color.FromArgb(255, 0x35, 0xD0, 0x7F);

    private string ContainmentText()
    {
        if (_sim.ContainmentSprayActive) return P("Spray active", "噴淋啟動");
        if (_sim.ContainmentIsolationPhaseA || _sim.ContainmentIsolationPhaseB) return P("Isolated", "已隔離");
        if (_sim.ContainmentPressureKpa > 0.5 || _sim.ContainmentSumpGal > 100) return P("Watch", "監察");
        return P("Normal", "正常");
    }

    private Color ContainmentColor()
        => _sim.ContainmentSprayActive || _sim.ContainmentPressureKpa > 186.0 ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
         : _sim.ContainmentIsolationPhaseA || _sim.ContainmentIsolationPhaseB || _sim.ContainmentPressureKpa > 0.5 ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
         : Color.FromArgb(255, 0x35, 0xD0, 0x7F);

    private string ElectricalText()
    {
        if (_sim.Electrical.InSbo) return P("SBO / DC coping", "全廠斷電／直流維持");
        if (_sim.Electrical.OnEdgPower) return P("EDG support", "柴油機支援");
        return _sim.GeneratorBreakerClosed ? P("Grid synced", "已併網") : P("Offsite available", "廠外電可用");
    }

    private Color ElectricalColor()
        => _sim.Electrical.InSbo ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
         : _sim.Electrical.OnEdgPower ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
         : Color.FromArgb(255, 0x35, 0xD0, 0x7F);

    private string ChemistryText()
    {
        if (_sim.ChemistryAlarm) return P("Out of spec", "水質越限");
        if (!_sim.MakeupWaterInSpec || _sim.LowMakeupAlarm) return P("Watch", "監察");
        return P("In spec", "合格");
    }

    private Color ChemistryColor()
        => _sim.ChemistryAlarm ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
         : (!_sim.MakeupWaterInSpec || _sim.LowMakeupAlarm) ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
         : Color.FromArgb(255, 0x35, 0xD0, 0x7F);

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

    private static void AddStaticText(Canvas c, double x, double y, string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(190, 0x9E, 0xCF, 0xFF)),
        };
        Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y); c.Children.Add(tb);
    }

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

    private void AddDynStatusCard(Canvas c, double x, double y, string title, string value, Color color)
    {
        var bg = new Border
        {
            Width = 155,
            Height = 46,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(38, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(210, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
        };
        var stack = new StackPanel { Margin = new Thickness(9, 5, 9, 5) };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromArgb(210, 0xDD, 0xE7, 0xF4)),
        });
        stack.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        bg.Child = stack;
        Canvas.SetLeft(bg, x); Canvas.SetTop(bg, y);
        c.Children.Add(bg); _mimicDynamic.Add(bg);
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

        UpdateReactimeter();
        UpdateCalorimetricPanel();
    }

    // Digital reactivity computer (reactimeter) readout — the INDEPENDENT inverse-kinetics ρ reconstructed
    // from the measured flux alone, shown alongside (not derived from) the engine's true reactivity.
    private void UpdateReactimeter()
    {
        double mPcm = _sim.MeasuredReactivityPcm;
        double mDollars = _sim.MeasuredReactivityDollars;
        RevMeterRho.Text = $"ρ {mPcm,7:+0;-0;0} pcm  ({mDollars:+0.000;-0.000;0.000} $)";
        Color rc = mPcm > 50  ? Color.FromArgb(255, 0xFF, 0x52, 0x52)   // brisk positive — caution
                 : mPcm > 5   ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)   // slightly positive
                 : mPcm < -50 ? Color.FromArgb(255, 0x4C, 0xAF, 0x50)   // negative / shut down
                 :              Color.FromArgb(255, 0xB0, 0xBE, 0xC5);  // ~critical
        RevMeterRho.Foreground = new SolidColorBrush(rc);
        RevMeterPeriod.Text = P($"Period  {MeasuredPeriodStr()}", $"週期  {MeasuredPeriodStr()}");
        string alarm = _sim.ReactimeterPositiveRateAlarm ? P("  ⚠ +RATE", "  ⚠ 正速率") : "";
        RevMeterSur.Text = $"SUR  {_sim.MeasuredStartupRateDpm,6:F2} DPM{alarm}";
        if (_sim.ReactimeterHasMark)
            RevMeterWorth.Text = P(
                $"Worth  {_sim.MeasuredWorthPcm:+0;-0;0} pcm ({_sim.MeasuredWorthDollars:+0.000;-0.000;0.000} $)",
                $"量度價值  {_sim.MeasuredWorthPcm:+0;-0;0} pcm ({_sim.MeasuredWorthDollars:+0.000;-0.000;0.000} $)");
        else
            RevMeterWorth.Text = P("Worth  — mark a reference first", "量度價值  — 先標記參考");
    }

    private string MeasuredPeriodStr()
    {
        double p = _sim.MeasuredPeriodSeconds;
        if (Math.Abs(p) >= 1e5) return "∞";
        if (Math.Abs(p) >= 999) return $"{p:F0}s";
        return $"{p:+0;-0;0}s";
    }

    private void OnReactimeterMark(object sender, RoutedEventArgs e) => _sim.MarkReactimeter();
    private void OnReactimeterClearMark(object sender, RoutedEventArgs e) => _sim.ClearReactimeterMark();

    private void OnCalibratePowerRange(object sender, RoutedEventArgs e) => _sim.CalibratePowerRangeToCalorimetric();
    private void OnLefmToggled(object sender, RoutedEventArgs e) => _sim.UseLefm = CalMeterLefmToggle.IsOn;

    // Secondary calorimetric heat-balance power + Power-Range NIS calibration deviation.
    private void UpdateCalorimetricPanel()
    {
        if (_sim.CalorimetricValid)
            CalMeterCal.Text = $"CAL {_sim.CalorimetricPowerPct,6:F1} % ±{_sim.CalorimetricUncertaintyPct:F1}";
        else
            CalMeterCal.Text = P($"CAL  — < 15 % RTP", $"熱平衡  — < 15 % 額定");
        CalMeterCal.Foreground = new SolidColorBrush(_sim.CalorimetricValid
            ? Color.FromArgb(255, 0x90, 0xCA, 0xF9) : Color.FromArgb(150, 0xB0, 0xBE, 0xC5));
        CalMeterPr.Text = $"PR  {_sim.PowerRangePercent,6:F1} %   gain {_sim.NisCalibrationGain:F3}";
        double dev = _sim.NisCalorimetricDeviationPct;
        CalMeterDev.Text = _sim.CalorimetricValid
            ? P($"Δ  {dev,6:+0.0;-0.0;0.0} % RTP{(_sim.NisCalorimetricDeviationOob ? "  ⚠ RECAL" : "")}",
                $"偏差  {dev,6:+0.0;-0.0;0.0} % 額定{(_sim.NisCalorimetricDeviationOob ? "  ⚠ 需校準" : "")}")
            : P("Δ  — (invalid)", "偏差  — （無效）");
        CalMeterDev.Foreground = new SolidColorBrush(
            _sim.NisCalorimetricDeviationOob ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
            : Color.FromArgb(255, 0x4C, 0xAF, 0x50));
        CalMeterUncert.Text = _sim.UseLefm
            ? P($"LEFM ultrasonic FW flow · licensed {_sim.LicensedPowerPct:F1}% (MUR)",
                $"超聲波給水流量 · 許可 {_sim.LicensedPowerPct:F1}%（MUR 提升）")
            : P($"FW venturi · licensed {_sim.LicensedPowerPct:F0}% RTP",
                $"給水文丘里 · 許可 {_sim.LicensedPowerPct:F0}% 額定");
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
        // One tile per Critical Safety Function Status Tree, built ONCE from the engine's
        // CriticalSafetyFunctions list. The tile binds by INDEX — its colour and tooltip are refreshed
        // each tick in UpdateCsfPanel() from _sim.CriticalSafetyFunctions[idx]; no closure over signals.
        var fns = _sim.CriticalSafetyFunctions;
        for (int i = 0; i < fns.Count; i++)
        {
            int idx = i; // capture
            var label = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Colors.White) };
            var border = new Border
            {
                Width = 96, Height = 52, CornerRadius = new CornerRadius(5),
                Background = _csfBrush[(int)CsfStatus.Green],
                Child = new Viewbox { Child = label, MaxHeight = 40, Margin = new Thickness(3) },
            };
            void SetLabel() { var f = _sim.CriticalSafetyFunctions[idx]; label.Text = $"{f.Mnemonic} {f.Name}"; }
            _relocalizers.Add(SetLabel);
            SetLabel();
            _csfCells.Add((border, label, idx));
            CsfPanel.Children.Add(border);
        }
    }

    private void UpdateCsfPanel()
    {
        var fns = _sim.CriticalSafetyFunctions;
        foreach (var (border, _, idx) in _csfCells)
        {
            var f = fns[idx];
            border.Background = _csfBrush[(int)f.Status];
            // Tooltip = entry Function Restoration Guideline + one-line cause (e.g. "FR-C.1 · CET 1240°C ≥ 649°C").
            ToolTipService.SetToolTip(border, f.IsGreen ? f.Cause : $"{f.Frg} · {f.Cause}");
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
            // (was: a stray Viewbox that re-parented `name` — a UIElement can only have one
            // parent, so adding it here AND to `inner` below threw COMException 0x800F1000 and
            // broke the whole reactor render. `name` is hosted by `inner` only.)
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
        ScenarioCombo.Items.Add(P("MSLB — main steam line break", "主蒸汽管爆裂 MSLB"));
        ScenarioCombo.Items.Add(P("RCP seal LOCA — loss of seal cooling", "主泵軸封失水 — 喪失軸封冷卻"));
        ScenarioCombo.Items.Add(P("Rod ejection — RIA (Ch 15.4.8)", "彈棒事故 — RIA（15.4.8）"));
        ScenarioCombo.Items.Add(P("Boron dilution (Ch 15.4.6)", "失控硼稀釋（15.4.6）"));
        ScenarioCombo.Items.Add(P("Complete loss of flow (Ch 15.3.2)", "全喪失強制流量（15.3.2）"));
        ScenarioCombo.Items.Add(P("RCP locked rotor (Ch 15.3.3)", "主泵卡軸（15.3.3）"));
        ScenarioCombo.Items.Add(P("Loss of feedwater heating (Ch 15.1.1)", "喪失給水加熱（15.1.1）"));
        ScenarioCombo.Items.Add(P("Loss of component cooling water (LCO 3.7.7)", "喪失設備冷卻水（LCO 3.7.7）"));
        ScenarioCombo.Items.Add(P("Uncontrolled RCCA withdrawal (Ch 15.4.1/2)", "失控提棒（15.4.1/2）"));
        ScenarioCombo.SelectedIndex = 0;
    }

    private void IsolateSg_Click(object sender, RoutedEventArgs e)
    {
        bool on = IsolateSgToggle.IsChecked == true;
        // The same operator action means "isolate the affected SG": MSIV + feedwater closure for an SGTR,
        // MSIV closure (terminating the blowdown) for an MSLB. Route it to whichever transient is active.
        if (ScenarioCombo.SelectedIndex == 7) _sim.MslbIsolated = on;
        else _sim.SgtrIsolated = on;
    }

    private void AmsacDefeat_Click(object sender, RoutedEventArgs e)
    {
        // Operator demo switch: defeat AMSAC to show the UNMITIGATED ATWS (turbine stays on, AFW does not
        // auto-start on the diverse path, peak RCS pressure climbs toward the ASME Level C limit). Default OFF.
        _sim.AmsacDefeated = AmsacDefeatToggle.IsChecked == true;
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
            7 => ReactorScenario.MainSteamLineBreak,
            8 => ReactorScenario.RcpSealLoca,
            9 => ReactorScenario.RodEjection,
            10 => ReactorScenario.BoronDilution,
            11 => ReactorScenario.CompleteLossOfFlow,
            12 => ReactorScenario.LockedRotor,
            13 => ReactorScenario.LossOfFeedwaterHeating,
            14 => ReactorScenario.LossOfComponentCoolingWater,
            15 => ReactorScenario.RccaWithdrawal,
            _ => ReactorScenario.Normal,
        });
        // The isolate control is meaningful during an SGTR (isolate affected SG) or an MSLB (close MSIVs).
        if (IsolateSgToggle is not null)
        {
            IsolateSgToggle.IsChecked = false;
            int idx = ScenarioCombo.SelectedIndex;
            IsolateSgToggle.Visibility = idx is 6 or 7 ? Visibility.Visible : Visibility.Collapsed;
            IsolateSgToggle.Content = idx == 7
                ? P("Close MSIVs (isolate break)", "關閉主蒸汽隔離閥（隔離破口）")
                : P("Isolate affected SG", "隔離受影響蒸發器");
        }
        // The AMSAC-defeat demo switch is meaningful only during an ATWS (index 4): defeat it to compare the
        // mitigated vs unmitigated transient. Reset it to OFF (AMSAC enabled) whenever the scenario changes.
        if (AmsacDefeatToggle is not null)
        {
            AmsacDefeatToggle.IsChecked = false;
            _sim.AmsacDefeated = false;
            AmsacDefeatToggle.Visibility = ScenarioCombo.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;
            AmsacDefeatToggle.Content = P("Defeat AMSAC (unmitigated ATWS)", "停用 AMSAC（未緩解 ATWS）");
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
        try
        {
            if (_controlRoomWindow is not null)
            {
                _controlRoomWindow.RestoreInteractive();
                return;
            }

            var w = new ReactorHtmlWindow(_sim, _fuel, _startupChecklistRequested ? "startup" : null);
            _controlRoomWindow = w;
            w.Closed += (_, _) => _controlRoomWindow = null;
            w.Activate();
        }
        catch (Exception ex)
        {
            CrashLogger.Log("reactor:open-control-room", ex);
        }
    }

    private void StartupChecklist_Click(object sender, RoutedEventArgs e)
    {
        _pendingDeepLink = "startup";
        TryApplyPendingDeepLink();
    }

    private void OpenChecklistWidget_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_startupChecklistWindow is not null)
            {
                _startupChecklistWindow.RestoreInteractive();
                return;
            }

            var w = new ReactorStartupChecklistWindow(_sim, target => DispatcherQueue.TryEnqueue(() => ScrollToStartupTarget(target)));
            _startupChecklistWindow = w;
            w.Closed += (_, _) => _startupChecklistWindow = null;
            w.Activate();
            new ReactorWidgetWindow(_sim, WidgetKind.StartupGauges).Activate();
            SettingsStore.Set("reactor.widgets", "CorePower,Status,Scram,StartupChecklist,StartupGauges");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("reactor:open-startup-checklist-widget", ex);
        }
    }

    private void OpenWidgets_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            new ReactorWidgetWindow(_sim, WidgetKind.CorePower).Activate();
            new ReactorWidgetWindow(_sim, WidgetKind.Status).Activate();
            new ReactorWidgetWindow(_sim, WidgetKind.Scram).Activate();
            new ReactorWidgetWindow(_sim, WidgetKind.StartupGauges).Activate();
            SettingsStore.Set("reactor.widgets", "CorePower,Status,Scram,StartupGauges");
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
    // ISA-18.1 pushbutton semantics. The actions are latched here and applied in the next UpdateAnnunciator
    // tick so the sequence state machine remains the single mutator (no UI/tick races).
    private void Ack_Click(object sender, RoutedEventArgs e)
    {
        // ACKNOWLEDGE: fast-flashing windows → steady; horn silenced. First-out latch is preserved.
        _ackPending = true;
        ReactorAudioEngine.I.Beep(accept: true);
    }

    private void Silence_Click(object sender, RoutedEventArgs e)
    {
        // SILENCE: horn (and SCRAM klaxon) off only — lamps keep flashing, windows stay unacknowledged.
        _silenceHorn = true;
        _silenced = true;
        ReactorAudioEngine.I.Klaxon(false);
        ReactorAudioEngine.I.Buzzer(false);
        ReactorAudioEngine.I.Ringback(false);
        ReactorAudioEngine.I.Beep(accept: false);
    }

    private void ResetAlarms_Click(object sender, RoutedEventArgs e)
    {
        // RESET: ringback (cleared) windows → dark; clears the first-out latch. A still-live alarm cannot
        // be reset away (it demotes to steady Acked inside the sequencer).
        _resetPending = true;
        _silenced = false;
        _silenceHorn = false;
        ReactorAudioEngine.I.Beep(accept: true);
    }

    private void LampTest_Click(object sender, RoutedEventArgs e)
    {
        // TEST: assert every lamp for ~700 ms so the operator can spot a dead tile. Pure render override —
        // it never mutates the latched sequence/first-out state (UpdateAlarmTiles honours _lampTestHeld).
        _lampTestHeld = true;
        UpdateAlarmTiles();
        ReactorAudioEngine.I.Beep(accept: true);
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        t.Tick += (_, _) => { t.Stop(); _lampTestHeld = false; UpdateAlarmTiles(); };
        t.Start();
    }

    // ================================================================ CONTROLS ====
    private void BuildControls()
    {
        var host = ControlsHost;
        host.Children.Clear();
        _startupChecklistAnchor = null;
        _startupPressureGaugeText = null;
        _startupPressureStepText = null;
        _startupPressureGauge = null;
        _controlSyncers.Clear();
        _startupControlAnchors.Clear();
        _startupControlAnchors["nis"] = NisTitle;

        var reactorControlsHeader = SectionHeader("Reactor controls · 反應堆控制", "反應堆控制 · Reactor controls");
        _startupControlAnchors["reactor-controls"] = reactorControlsHeader;
        host.Children.Add(reactorControlsHeader);

        // Rod banks
        for (int b = 0; b < 4; b++)
        {
            int bank = b;
            char name = (char)('A' + b);
            host.Children.Add(StartupControlFrame("reactor-controls", 5, IsReactivityStepReady, LabeledSlider(
                $"Control rod bank {name} (% inserted)", $"控制棒組 {name}（插入 %）",
                0, 100, _sim.RodBankInsertion[bank], 1,
                v => _sim.SetRodBank(bank, v),
                () => _sim.RodBankInsertion[bank], "%")));
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

        // Dropped-RCCA fault → quadrant power tilt (QPTR, LCO 3.2.4). Drop a single full-length rod into one of
        // the four core quadrants: that quadrant's ex-core detector reads LOW, the other three read HIGH, QPTR
        // climbs to ~1.08, ~−200 pcm is inserted and DNBR margin erodes. "Retrieve" re-latches the rod (reversible).
        var dropPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        for (int q = 0; q < 4; q++)
        {
            int quad = q;
            var db = new Button { Content = P($"Drop Q{quad + 1}", $"落棒 Q{quad + 1}") };
            db.Click += (_, _) => _sim.DropRod(quad);
            _relocalizers.Add(() => db.Content = P($"Drop Q{quad + 1}", $"落棒 Q{quad + 1}"));
            dropPanel.Children.Add(db);
        }
        var retrieveBtn = new Button { Content = P("Retrieve rod", "復位落棒") };
        retrieveBtn.Click += (_, _) => _sim.RecoverDroppedRod();
        _relocalizers.Add(() => retrieveBtn.Content = P("Retrieve rod", "復位落棒"));
        dropPanel.Children.Add(retrieveBtn);
        host.Children.Add(WrapLabel("Dropped RCCA → quadrant tilt (QPTR) · 落棒→象限傾斜",
            "落棒→象限傾斜（QPTR）· Dropped RCCA → quadrant tilt", dropPanel));

        // Boron
        host.Children.Add(StartupControlFrame("reactor-controls", 5, IsReactivityStepReady, LabeledSlider(
            "Soluble boron target (ppm) — charging ↑ / dilution ↓", "硼濃度目標（ppm）— 加硼 ↑／稀釋 ↓",
            0, 2500, _sim.TargetBoronPpm, 10,
            v => _sim.TargetBoronPpm = v, () => _sim.TargetBoronPpm, " ppm")));

        var primaryHeader = SectionHeader("Primary system · 一迴路系統", "一迴路系統 · Primary system");
        _startupControlAnchors["primary-system"] = primaryHeader;
        host.Children.Add(primaryHeader);

        // RCP pumps
        var pumpPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        for (int p = 0; p < 4; p++)
        {
            int pump = p;
            var tg = new ToggleButton { Content = P($"RCP {pump + 1}", $"主泵 {pump + 1}") };
            tg.Checked += (_, _) =>
            {
                if (_syncingControlValues) return;
                _sim.StartRcp(pump);
                PersistenceService.I.NoteChanged();
            };
            tg.Unchecked += (_, _) =>
            {
                if (_syncingControlValues) return;
                _sim.StopRcp(pump);
                PersistenceService.I.NoteChanged();
            };
            _controlSyncers.Add(() =>
            {
                if (tg.IsChecked != _sim.RcpRunning[pump])
                    tg.IsChecked = _sim.RcpRunning[pump];
            });
            _relocalizers.Add(() => tg.Content = P($"RCP {pump + 1}", $"主泵 {pump + 1}"));
            pumpPanel.Children.Add(tg);
        }
        host.Children.Add(StartupControlFrame("primary-system", 2, HasStartupPumpCount,
            WrapLabel("Reactor coolant pumps · 反應堆冷卻劑泵", "反應堆冷卻劑泵 · Reactor coolant pumps", pumpPanel)));

        host.Children.Add(StartupControlFrame("primary-system", 3, IsStartupFlowReady, LabeledSlider(
            "RCP flow demand (%)", "主泵流量需求（%）",
            0, 100, _sim.RcpFlowDemand * 100, 1,
            v => _sim.RcpFlowDemand = v / 100.0, () => _sim.RcpFlowDemand * 100, "%")));

        // Pressurizer toggles + relief + ECCS
        var pzrPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var autoPzr = MakeToggle("Auto press ctrl · 自動壓力", "自動壓力 · Auto press ctrl",
            v => _sim.PzrAutoPressureControl = v, () => _sim.PzrAutoPressureControl);
        pzrPanel.Children.Add(autoPzr);
        pzrPanel.Children.Add(MakeToggle("Heater · 加熱器", "加熱器 · Heater",
            v => _sim.PressurizerHeater = v, () => _sim.PressurizerHeater));
        pzrPanel.Children.Add(MakeToggle("Spray · 噴淋", "噴淋 · Spray",
            v => _sim.PressurizerSpray = v, () => _sim.PressurizerSpray));
        pzrPanel.Children.Add(MakeToggle("Relief valve · 釋壓閥", "釋壓閥 · Relief valve",
            v => _sim.ReliefValveOpen = v, () => _sim.ReliefValveOpen));
        host.Children.Add(StartupControlFrame("primary-system", 4, IsStartupPressureStepReady,
            WrapLabel("Pressurizer & relief · 穩壓器與釋壓", "穩壓器與釋壓 · Pressurizer & relief", pzrPanel)));

        // CVCS boric-acid / reactor-makeup-water blender mode. Only AUTOMATIC is auto-driven by the VCT level
        // controller (and matches current RCS boron → zero net reactivity); the others are operator-intent
        // selections shown in the makeup readout. None of them writes BoronPpm — boron stays single-authored.
        var blenderCombo = new ComboBox { MinWidth = 220 };
        blenderCombo.Items.Add(P("Auto (match RCS) · 自動（匹配一次側）", "自動（匹配一次側）· Auto (match RCS)"));
        blenderCombo.Items.Add(P("Borate · 加硼", "加硼 · Borate"));
        blenderCombo.Items.Add(P("Dilute · 稀釋", "稀釋 · Dilute"));
        blenderCombo.Items.Add(P("Alternate dilute · 交替稀釋", "交替稀釋 · Alternate dilute"));
        blenderCombo.SelectedIndex = (int)_sim.BlenderMode;
        blenderCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingControlValues) return;
            _sim.BlenderMode = blenderCombo.SelectedIndex switch
            {
                1 => CvcsBlenderMode.Borate,
                2 => CvcsBlenderMode.Dilute,
                3 => CvcsBlenderMode.AlternateDilute,
                _ => CvcsBlenderMode.Automatic,
            };
            PersistenceService.I.NoteChanged();
        };
        _controlSyncers.Add(() =>
        {
            int idx = Math.Clamp((int)_sim.BlenderMode, 0, 3);
            if (blenderCombo.SelectedIndex != idx) blenderCombo.SelectedIndex = idx;
        });
        host.Children.Add(StartupControlFrame("reactor-controls", 5, IsReactivityStepReady,
            WrapLabel("CVCS makeup blender mode · 化容系統補水混合器模式",
                "化容系統補水混合器模式 · CVCS makeup blender mode", blenderCombo)));

        var safetyPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        safetyPanel.Children.Add(MakeToggle("Arm ECCS · 啟用應急冷卻", "啟用應急冷卻 · Arm ECCS",
            v => _sim.EccsArmed = v, () => _sim.EccsArmed));
        var injBtn = new Button { Content = P("Force ECCS inject · 強制注入", "強制注入 · Force ECCS inject") };
        injBtn.Click += (_, _) => { _sim.EccsArmed = true; };
        _relocalizers.Add(() => injBtn.Content = P("Force ECCS inject · 強制注入", "強制注入 · Force ECCS inject"));
        safetyPanel.Children.Add(injBtn);
        // ES-1.4 hot-leg recirculation switchover (post-LOCA boric-acid-precipitation prevention, ~5.5 h credited
        // time). Defaults OFF — the operator elects the transfer to establish the through-core flush.
        safetyPanel.Children.Add(MakeToggle("ES-1.4 hot-leg recirc · 熱段再循環", "熱段再循環 · ES-1.4 hot-leg recirc",
            v => _sim.HotLegRecircActive = v, () => _sim.HotLegRecircActive));
        host.Children.Add(WrapLabel("Safety injection · 安全注入", "安全注入 · Safety injection", safetyPanel));

        // Containment combustible-gas control (10 CFR 50.44): PARs are passive (always on, no control); the
        // Distributed Ignition System is operator-armed and defaults OFF — arm it on a core-damage entry to
        // burn H₂ off lean before it reaches the detonable band (arming late, above 13 vol%, risks a detonation).
        var h2Panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        h2Panel.Children.Add(MakeToggle("Arm H₂ igniters · 啟用氫氣點火器", "啟用氫氣點火器 · Arm H₂ igniters",
            v => _sim.IgniterSystemArmed = v, () => _sim.IgniterSystemArmed));
        host.Children.Add(WrapLabel("Combustible-gas control · 可燃氣體控制", "可燃氣體控制 · Combustible-gas control", h2Panel));

        // RCS Leakage Detection (LCO 3.4.13 / RG 1.45) — demo inputs. Both default OFF/zero so the subsystem is
        // quiescent at startup; inject an unidentified leak to watch the sump + atmosphere monitors respond.
        host.Children.Add(InfoNote(
            "RCS leakage detection (LCO 3.4.13 / RG 1.45): unid limit 1 gpm, ident limit 10 gpm, pressure-boundary leakage NONE allowed.",
            "反應堆冷卻劑洩漏偵測（LCO 3.4.13／RG 1.45）：未辨識上限 1 gpm，已辨識上限 10 gpm，壓力邊界洩漏一律不容許。"));
        host.Children.Add(LeakSlider(
            "Unidentified leak — demo (gpm)", "未辨識洩漏－示範（gpm）",
            0, 3, _sim.DemoUnidentifiedLeakGpm, 0.05,
            v => _sim.DemoUnidentifiedLeakGpm = v, () => _sim.DemoUnidentifiedLeakGpm));
        var leakPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        leakPanel.Children.Add(MakeToggle("Pressure-boundary leak · 壓力邊界洩漏", "壓力邊界洩漏 · Pressure-boundary leak",
            v => _sim.PressureBoundaryLeak = v, () => _sim.PressureBoundaryLeak));
        host.Children.Add(WrapLabel("RCS pressure boundary · 一次側壓力邊界", "一次側壓力邊界 · RCS pressure boundary", leakPanel));

        var secondaryHeader = SectionHeader("Secondary & turbine · 二迴路與汽輪機", "二迴路與汽輪機 · Secondary & turbine");
        _startupControlAnchors["secondary-turbine"] = secondaryHeader;
        host.Children.Add(secondaryHeader);

        var fwPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var fwAuto = MakeToggle("Feedwater AUTO · 給水自動", "給水自動 · Feedwater AUTO",
            v => _sim.FeedwaterAuto = v, () => _sim.FeedwaterAuto);
        fwPanel.Children.Add(fwAuto);
        host.Children.Add(WrapLabel("Three-element feed control · 三元給水控制", "三元給水控制 · Three-element feed control", fwPanel));

        host.Children.Add(LabeledSlider(
            "Feedwater flow — manual (%)", "給水流量－手動（%）",
            0, 100, _sim.FeedwaterFlow * 100, 1,
            v => _sim.FeedwaterFlow = v / 100.0, () => _sim.FeedwaterFlow * 100, "%"));
        host.Children.Add(StartupControlFrame("secondary-turbine", 8, IsGeneratorStepReady, LabeledSlider(
            "Turbine load setpoint (%)", "汽輪機負載設定（%）",
            0, 100, _sim.TurbineLoadSetpoint * 100, 1,
            v => _sim.TurbineLoadSetpoint = v / 100.0, () => _sim.TurbineLoadSetpoint * 100, "%")));

        // MSR (汽水分離再熱器) fault demo: a reheater tube leak / bypass that degrades the reheat — hot reheat
        // falls, LP exhaust moisture climbs toward the blade-erosion limit, and gross output drops. Default OFF.
        var msrPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        msrPanel.Children.Add(MakeToggle("Reheater tube leak · 再熱器傳熱管洩漏", "再熱器傳熱管洩漏 · Reheater tube leak",
            v => _sim.ReheaterTubeLeak = v, () => _sim.ReheaterTubeLeak));
        host.Children.Add(WrapLabel("Moisture Separator Reheater · 汽水分離再熱器", "汽水分離再熱器 · Moisture Separator Reheater", msrPanel));

        var grdPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        // Generator breaker: with the sync interlock OFF (default) the toggle closes unconditionally, exactly
        // as before; with it ON, the ANSI-25 sync-check window must be satisfied to close onto the grid.
        grdPanel.Children.Add(MakeToggle("Generator breaker · 發電機開關", "發電機開關 · Generator breaker",
            v => { if (v && _sim.SyncInterlock && !_sim.SyncCheckPermissive) return; _sim.GeneratorBreakerClosed = v; },
            () => _sim.GeneratorBreakerClosed));
        grdPanel.Children.Add(MakeToggle("Sync interlock (25) · 同步聯鎖", "同步聯鎖 · Sync interlock (25)",
            v => _sim.SyncInterlock = v, () => _sim.SyncInterlock)); // DEFAULT OFF
        host.Children.Add(StartupControlFrame("secondary-turbine", 8, IsGeneratorStepReady,
            WrapLabel("Grid synchronization · 併網", "併網 · Grid synchronization", grdPanel)));

        // ---- Main generator excitation / AVR (reactive-power control) ----
        var avrCombo = new ComboBox { MinWidth = 220 };
        avrCombo.Items.Add(P("AVR Auto-voltage · 自動電壓", "自動電壓 · AVR Auto-voltage"));
        avrCombo.Items.Add(P("Constant PF · 固定功率因數", "固定功率因數 · Constant PF"));
        avrCombo.Items.Add(P("Constant MVAR · 固定無功", "固定無功 · Constant MVAR"));
        avrCombo.Items.Add(P("Manual field · 手動勵磁", "手動勵磁 · Manual field"));
        avrCombo.SelectedIndex = _sim.GenAvrMode switch
        {
            ReactorSimService.AvrMode.ConstantPf => 1,
            ReactorSimService.AvrMode.ConstantMvar => 2,
            ReactorSimService.AvrMode.Manual => 3,
            _ => 0,
        };
        avrCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingControlValues) return;
            _sim.GenAvrMode = avrCombo.SelectedIndex switch
            {
                1 => ReactorSimService.AvrMode.ConstantPf,
                2 => ReactorSimService.AvrMode.ConstantMvar,
                3 => ReactorSimService.AvrMode.Manual,
                _ => ReactorSimService.AvrMode.AutoVoltage,
            };
            PersistenceService.I.NoteChanged();
        };
        _controlSyncers.Add(() =>
        {
            int idx = _sim.GenAvrMode switch
            {
                ReactorSimService.AvrMode.ConstantPf => 1,
                ReactorSimService.AvrMode.ConstantMvar => 2,
                ReactorSimService.AvrMode.Manual => 3,
                _ => 0,
            };
            if (avrCombo.SelectedIndex != idx) avrCombo.SelectedIndex = idx;
        });
        host.Children.Add(WrapLabel("Excitation / AVR mode · 勵磁／自動電壓調節模式",
            "勵磁／自動電壓調節模式 · Excitation / AVR mode", avrCombo));
        host.Children.Add(LabeledSlider("AVR voltage setpoint (%)", "自動電壓設定（%）",
            95, 105, _sim.VoltageSetpointPu * 100, 1,
            v => _sim.VoltageSetpointPu = v / 100.0, () => _sim.VoltageSetpointPu * 100, "%"));
        host.Children.Add(LabeledSlider("Constant-PF setpoint (%)", "固定功率因數設定（%）",
            80, 100, _sim.PfSetpoint * 100, 1,
            v => _sim.PfSetpoint = v / 100.0, () => _sim.PfSetpoint * 100, "%"));
        host.Children.Add(LabeledSlider("Constant-MVAR setpoint", "固定無功設定",
            -350, 567, _sim.MvarSetpoint, 5,
            v => _sim.MvarSetpoint = v, () => _sim.MvarSetpoint, " MVAR"));
        host.Children.Add(LabeledSlider("Manual field current (%)", "手動勵磁電流（%）",
            0, 260, _sim.ManualFieldPu * 100, 5,
            v => _sim.ManualFieldPu = v / 100.0, () => _sim.ManualFieldPu * 100, "%"));

        // Generator protection (ANSI 32/40/24/27/59/81) + hand-reset of the 86 lockout.
        var genProtPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var genProtToggle = MakeToggle("Gen protection armed · 發電機保護啟用",
            "發電機保護啟用 · Gen protection armed", v => _sim.GenProtectionArmed = v, () => _sim.GenProtectionArmed);
        genProtPanel.Children.Add(genProtToggle);
        var genResetBtn = new Button { Content = P("Reset gen lockout (86) · 重置發電機閉鎖", "重置發電機閉鎖 · Reset gen lockout (86)") };
        genResetBtn.Click += (_, _) => _sim.ResetGeneratorTrip();
        genProtPanel.Children.Add(genResetBtn);
        host.Children.Add(WrapLabel("Generator protection · 發電機保護繼電器",
            "發電機保護繼電器 · Generator protection", genProtPanel));

        // EHC turbine trip / reset — manual stop-valve trip and a latch reset (only below ~90 % speed).
        var turbPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var turbTripBtn = new Button { Content = P("Manual turbine trip · 手動汽輪機跳脫", "手動汽輪機跳脫 · Manual turbine trip") };
        turbTripBtn.Click += (_, _) => _sim.TripTurbine();
        var turbResetBtn = new Button { Content = P("Reset turbine trip · 重置汽輪機跳脫", "重置汽輪機跳脫 · Reset turbine trip") };
        turbResetBtn.Click += (_, _) => _sim.ResetTurbineTrip();
        turbPanel.Children.Add(turbTripBtn);
        turbPanel.Children.Add(turbResetBtn);
        host.Children.Add(WrapLabel("Turbine EHC trip · 汽輪機電液跳脫", "汽輪機電液跳脫 · Turbine EHC trip", turbPanel));

        host.Children.Add(SectionHeader("Class 1E electrical · 1E 級廠用電", "1E 級廠用電 · Class 1E electrical"));

        // DC load shedding stretches the station-battery coping time during a blackout (×1.67 here).
        var dcPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        dcPanel.Children.Add(MakeToggle("Shed non-vital DC loads · 卸除非必要直流負載",
            "卸除非必要直流負載 · Shed non-vital DC loads", v => _sim.Electrical.LoadShed = v,
            () => _sim.Electrical.LoadShed));
        host.Children.Add(WrapLabel("Station battery (125 VDC) · 蓄電池（125 VDC）",
            "蓄電池（125 VDC）· Station battery (125 VDC)", dcPanel));

        var modeHeader = SectionHeader("Mode & automation · 模式與自動化", "模式與自動化 · Mode & automation");
        _startupControlAnchors["mode-automation"] = modeHeader;
        host.Children.Add(modeHeader);

        // Mode selector
        var modeCombo = new ComboBox { MinWidth = 220 };
        modeCombo.Items.Add(P("Shutdown · 停機", "停機 · Shutdown"));
        modeCombo.Items.Add(P("Startup · 啟動", "啟動 · Startup"));
        modeCombo.Items.Add(P("Run · 運轉", "運轉 · Run"));
        modeCombo.SelectedIndex = _sim.Mode switch
        {
            ReactorMode.Startup => 1,
            ReactorMode.Run => 2,
            _ => 0,
        };
        modeCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingControlValues) return;
            _sim.SetMode(modeCombo.SelectedIndex switch { 1 => ReactorMode.Startup, 2 => ReactorMode.Run, _ => ReactorMode.Shutdown });
            PersistenceService.I.NoteChanged();
        };
        _controlSyncers.Add(() =>
        {
            int idx = _sim.Mode switch
            {
                ReactorMode.Startup => 1,
                ReactorMode.Run => 2,
                _ => 0,
            };
            if (modeCombo.SelectedIndex != idx) modeCombo.SelectedIndex = idx;
        });
        host.Children.Add(StartupControlFrame("mode-automation", 1, IsStartupModeSelected,
            WrapLabel("Reactor mode · 反應堆模式", "反應堆模式 · Reactor mode", modeCombo)));

        var easyStartup = MakeToggle(
            "Easy startup assist · 50% easier / fuel costs 75% more",
            "簡易啟動輔助 · 易啟動 50% / 燃料多耗 75%",
            v => { _sim.EasyStartupMode = v; DispatcherQueue.TryEnqueue(UpdateControlsLive); },
            () => _sim.EasyStartupMode);
        host.Children.Add(StartupControlFrame("mode-automation", 1, () => _sim.EasyStartupMode,
            WrapLabel("Beginner startup assist · 新手啟動輔助",
                "新手啟動輔助 · Beginner startup assist", easyStartup)));
        host.Children.Add(InfoNote(
            "Easy mode adds about +500 pcm only below 5% power, suppresses automatic simulator SCRAMs, and allows skipping the pressure, rod/boron, and 1/M checklist waits. Manual SCRAM remains available. It costs 1.75x fuel while enabled, and each skipped startup step adds 25% more nuclear waste.",
            "簡易模式只喺低於 5% 功率時加入約 +500 pcm，會抑制模擬器自動 SCRAM，並容許跳過壓力、控制棒／硼、1/M 等待步驟。手動 SCRAM 仍可使用。開啟期間燃料成本為 1.75 倍，每跳過一個啟動步驟會再增加 25% 核廢料。"));

        host.Children.Add(InfoNote(
            "AUTO rod control regulates Tavg to the turbine-load-programmed Tref (Westinghouse §8.1): " +
            "raise turbine load and the rods withdraw to follow. The reference rises 557°F (no-load) → 581°F (full).",
            "自動棒控將 Tavg 調節至按汽輪機負荷編程嘅 Tref（西屋 §8.1）：加大汽輪機負荷，控制棒就會抽出跟隨。" +
            "參考溫度由 557°F（零負荷）升至 581°F（滿載）。"));

        // ---- Fuel-cycle core depletion (burnup) ----
        host.Children.Add(SectionHeader("Fuel cycle / core depletion · 燃料循環／堆芯燃耗",
                                        "燃料循環／堆芯燃耗 · Fuel cycle / core depletion"));
        host.Children.Add(InfoNote(
            "Burnup advances with power: the core ages BOL→EOL over ~18 GWd/tU (~528 EFPD). As fuel depletes the " +
            "MTC trends from −20 to −40 pcm/°C, β_eff (one dollar) shrinks ~0.0065→0.00585 so transients sharpen, " +
            "and critical boron lets down ~1200→10 ppm — dilute boron to the displayed target to stay critical. " +
            "Real time it is glacial; the accelerator (default OFF) fast-forwards the cycle to watch it evolve.",
            "燃耗隨功率累積：堆芯由壽期初到壽期末約經歷 18 GWd/tU（約 528 滿功率日）。燃料消耗時，慢化劑溫度係數由 " +
            "−20 趨向 −40 pcm/°C，有效緩發中子分數（一美元）由約 0.0065 縮至 0.00585，瞬變更敏感；臨界硼濃度由約 " +
            "1200 降至 10 ppm — 請按顯示嘅目標稀釋硼以維持臨界。實時演進極慢；加速器（預設關閉）可快進整個循環觀察。"));
        var deplCombo = new ComboBox { MinWidth = 260 };
        deplCombo.Items.Add(P("Real time (off) · 即時（關）", "即時（關）· Real time (off)"));
        deplCombo.Items.Add(P("Accelerate ×1000 · 加速 ×1000", "加速 ×1000 · Accelerate ×1000"));
        deplCombo.Items.Add(P("Accelerate ×10000 · 加速 ×10000", "加速 ×10000 · Accelerate ×10000"));
        deplCombo.Items.Add(P("Accelerate ×50000 · 加速 ×50000", "加速 ×50000 · Accelerate ×50000"));
        deplCombo.SelectedIndex = _sim.DepletionAccel >= 50000.0 ? 3
            : _sim.DepletionAccel >= 10000.0 ? 2
            : _sim.DepletionAccel >= 1000.0 ? 1
            : 0;
        deplCombo.SelectionChanged += (_, _) =>
        {
            if (_syncingControlValues) return;
            _sim.DepletionAccel = deplCombo.SelectedIndex switch { 1 => 1000.0, 2 => 10000.0, 3 => 50000.0, _ => 1.0 };
            PersistenceService.I.NoteChanged();
        };
        _controlSyncers.Add(() =>
        {
            int idx = _sim.DepletionAccel >= 50000.0 ? 3
                : _sim.DepletionAccel >= 10000.0 ? 2
                : _sim.DepletionAccel >= 1000.0 ? 1
                : 0;
            if (deplCombo.SelectedIndex != idx) deplCombo.SelectedIndex = idx;
        });
        host.Children.Add(WrapLabel("Cycle depletion accelerator (demo, default OFF) · 循環燃耗加速器（演示，預設關閉）",
                                    "循環燃耗加速器（演示，預設關閉）· Cycle depletion accelerator (demo, default OFF)", deplCombo));

        // ---- Startup-sequence checklist (approach to criticality) ----
        var startupHeader = SectionHeader("Startup sequence (approach to criticality) · 啟動程序（趨近臨界）",
                                          "啟動程序（趨近臨界）· Startup sequence");
        _startupChecklistAnchor = startupHeader;
        host.Children.Add(startupHeader);
        var startupIntro = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 0, 0, 2) };
        startupIntro.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        startupIntro.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var startupNote = InfoNote(
            "Beginner path: complete one row at a time. Press Control on the active row, use the highlighted Easy Mode controls, then wait for the named measured gauge before moving on. In Easy Mode, automatic simulator SCRAMs are suppressed and steps 4-6 can be skipped if they stall; each skip adds 25% more waste. Gauges still read live and the manual SCRAM button still works.",
            "新手流程：逐行完成。按目前行嘅「控制」，使用簡易模式高亮嘅控制，然後等指定實際儀表到位先下一步。簡易模式會抑制模擬器自動 SCRAM；第 4-6 步如果卡住可以跳過，每跳一步增加 25% 廢料。儀表仍然即時讀數，手動 SCRAM 按鈕仍可使用。");
        Grid.SetColumn(startupNote, 0);
        startupIntro.Children.Add(startupNote);
        var pressureGauge = BuildStartupPressureGauge();
        Grid.SetColumn(pressureGauge, 1);
        startupIntro.Children.Add(pressureGauge);
        host.Children.Add(startupIntro);
        BuildStartupChecklist(host);

        // ---- Always-on reactor persistence (opt-in, default OFF, easy off switch) ----
        host.Children.Add(SectionHeader("⚛ Always-on reactor · 常駐反應堆", "⚛ 常駐反應堆 · Always-on reactor"));
        BuildKeepAliveSection(host);

        // NOTE: the "ARM real shutdown on meltdown" toggle (and its Windows-linkage / keep-awake / status-API /
        // autosave / Home Assistant siblings) now live on the dedicated Reactor Settings page. The reactor still
        // honours ReactorRealShutdownArm.Armed when a meltdown occurs. 真實關機等對外設定已搬去反應堆設定頁。

        ResetTripButton.IsEnabled = true;
        SyncControlValues();
    }

    private readonly List<(StartupStep step, TextBlock check, Border frame, Button? skip, TextBlock? skipNote)> _startupSteps = new();
    private TextBlock? _keepAliveStatus;
    private Border? _keepAliveDot;

    private void SyncControlValues()
    {
        _syncingControlValues = true;
        try
        {
            SyncTopLevelControls();
            foreach (var sync in _controlSyncers)
            {
                try { sync(); }
                catch (Exception ex) { CrashLogger.Log("reactor:control-sync", ex); }
            }
        }
        finally
        {
            _syncingControlValues = false;
        }
    }

    private void SyncTopLevelControls()
    {
        if (AutoRunToggle is null) return;

        if (AutoRunToggle.IsOn != _sim.AutoRodControl)
            AutoRunToggle.IsOn = _sim.AutoRodControl;
    }

    private void UpdateControlsLive()
    {
        SyncControlValues();

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
            string autoLine;
            if (_sim.AutoRodControl)
            {
                double speed = _sim.RodSpeedDemandSpm;
                string motion = Math.Abs(speed) < 0.5
                    ? P("hold (in deadband)", "保持（死區內）")
                    : (speed > 0 ? P($"withdraw {Math.Abs(speed):F0} spm", $"抽出 {Math.Abs(speed):F0} 步/分")
                                 : P($"insert {Math.Abs(speed):F0} spm", $"插入 {Math.Abs(speed):F0} 步/分"));
                autoLine = "\n" + P(
                    $"AUTO Tavg/Tref:  Tref {_sim.Tref * 1.8 + 32:F1}°F · ΔT {_sim.TavgTrefError * 1.8:+0.0;-0.0}°F · {motion}",
                    $"自動 Tavg/Tref：  Tref {_sim.Tref * 1.8 + 32:F1}°F · ΔT {_sim.TavgTrefError * 1.8:+0.0;-0.0}°F · {motion}");
            }
            else
            {
                autoLine = "\n" + P("AUTO rod control off (manual rod positioning)", "自動棒控關閉（手動定位）");
            }
            _rodStatusText.Text =
                P($"Rod steps withdrawn (0–228):  {steps}", $"控制棒抽出步數（0–228）：  {steps}") + "\n" +
                P($"Lead-bank D limit @ {_sim.NeutronPowerFraction * 100:F0}% pwr: {lowLim:F0} steps · {lim}",
                  $"領先 D 棒插入限值 @ {_sim.NeutronPowerFraction * 100:F0}% 功率：{lowLim:F0} 步 · {lim}") +
                autoLine;
            _rodStatusText.Foreground = new SolidColorBrush(
                _sim.RilLowLowAlarm ? Color.FromArgb(255, 0xFF, 0x52, 0x52)
                : _sim.RilLowAlarm ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
                : Color.FromArgb(200, 0xCF, 0xD8, 0xDC));
        }

        UpdateStartupPressureGauge();

        // Live-update the startup checklist marks in order. Later steps stay pending until previous steps are done.
        int orderedDone = ReactorScenarios.CompletedStartupSteps(_startupSteps.Select(x => x.step).ToArray(), _sim);
        for (int i = 0; i < _startupSteps.Count; i++)
        {
            var (step, check, frame, skip, skipNote) = _startupSteps[i];
            bool ok = i < orderedDone;
            bool active = i == orderedDone && orderedDone < _startupSteps.Count;
            bool skipped = step.IsSkipped(_sim);
            check.Text = skipped ? "↷" : ok ? "✓" : active ? "→" : "○";
            check.Foreground = new SolidColorBrush(ok
                ? skipped ? Color.FromArgb(255, 0x90, 0xCA, 0xF9) : Color.FromArgb(255, 0x4C, 0xAF, 0x50)
                : active ? Color.FromArgb(255, 0xFF, 0xB3, 0x00)
                : Color.FromArgb(160, 0xAA, 0xAA, 0xAA));
            frame.BorderBrush = new SolidColorBrush(active
                ? Color.FromArgb(210, 0xFF, 0xB3, 0x00)
                : ok ? skipped ? Color.FromArgb(170, 0x42, 0xA5, 0xF5) : Color.FromArgb(150, 0x4C, 0xAF, 0x50)
                : Color.FromArgb(80, 0x99, 0x99, 0x99));
            frame.Background = new SolidColorBrush(ok
                ? skipped ? Color.FromArgb(28, 0x42, 0xA5, 0xF5) : Color.FromArgb(25, 0x4C, 0xAF, 0x50)
                : active ? Color.FromArgb(25, 0xFF, 0xB3, 0x00)
                : Color.FromArgb(0, 0, 0, 0));
            frame.Opacity = i > orderedDone ? 0.72 : 1.0;
            if (skip is not null)
            {
                bool canShow = step.EasyModeSkippable && _sim.EasyStartupMode && !step.IsSatisfied(_sim);
                skip.Visibility = canShow || skipped ? Visibility.Visible : Visibility.Collapsed;
                skip.IsEnabled = canShow && !skipped;
                skip.Content = skipped ? P("Skipped", "已跳過") : P($"Skip step {i + 1}", $"跳過第 {i + 1} 步");
            }
            if (skipNote is not null)
            {
                skipNote.Visibility = skipped ? Visibility.Visible : Visibility.Collapsed;
                skipNote.Text = P("Skipped in Easy Mode; gauges live, manual SCRAM available.", "已於簡易模式跳過；儀表即時，手動 SCRAM 可用。");
            }
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
            var controlText = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(190, 0x9E, 0xA7, 0xB0)),
                Opacity = 0.78,
                Margin = new Thickness(0, 2, 0, 0),
            };
            var detailText = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromArgb(220, 0xFF, 0xD1, 0x80)),
                Margin = new Thickness(0, 2, 0, 0),
                Visibility = string.IsNullOrWhiteSpace(step.DetailEn) ? Visibility.Collapsed : Visibility.Visible,
            };
            int n = i;
            var stp = step;
            _relocalizers.Add(() =>
            {
                text.Text = $"{n}. " + P(stp.En, stp.Zh);
                controlText.Text = P($"Use: {stp.ControlEn}", $"使用：{stp.ControlZh}");
                detailText.Text = P(stp.DetailEn, stp.DetailZh);
            });
            text.Text = $"{n}. " + P(stp.En, stp.Zh);
            controlText.Text = P($"Use: {stp.ControlEn}", $"使用：{stp.ControlZh}");
            detailText.Text = P(stp.DetailEn, stp.DetailZh);
            var copy = new StackPanel { Spacing = 0 };
            copy.Children.Add(text);
            copy.Children.Add(controlText);
            copy.Children.Add(detailText);
            var go = new Button
            {
                MinWidth = 72,
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
            };
            _relocalizers.Add(() => go.Content = P("Control", "控制"));
            go.Content = P("Control", "控制");
            go.Click += (_, _) => ScrollToStartupTarget(stp.ControlTarget);
            Button? skip = null;
            TextBlock? skipNote = null;
            TextBlock? pressureStepText = null;
            var actions = new StackPanel
            {
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            if (stp.EasyModeSkippable)
            {
                if (n == 4)
                {
                    pressureStepText = new TextBlock
                    {
                        FontSize = 11,
                        FontFamily = new FontFamily("Consolas"),
                        TextAlignment = TextAlignment.Right,
                        Foreground = new SolidColorBrush(Color.FromArgb(220, 0x90, 0xCA, 0xF9)),
                    };
                    _startupPressureStepText = pressureStepText;
                    actions.Children.Add(pressureStepText);
                }

                skip = new Button
                {
                    MinWidth = 92,
                    Padding = new Thickness(8, 2, 8, 2),
                    Visibility = Visibility.Collapsed,
                };
                _relocalizers.Add(() => skip.Content = stp.IsSkipped(_sim) ? P("Skipped", "已跳過") : P($"Skip step {n}", $"跳過第 {n} 步"));
                skip.Content = P($"Skip step {n}", $"跳過第 {n} 步");
                skip.Click += (_, _) =>
                {
                    if (!_sim.EasyStartupMode) return;
                    if (n == 4) _sim.EasyStartupSkipPressureStep = true;
                    else if (n == 5) _sim.EasyStartupSkipReactivityStep = true;
                    else if (n == 6) _sim.EasyStartupSkipNisStep = true;
                    PersistenceService.I.NoteChanged();
                    DispatcherQueue.TryEnqueue(UpdateControlsLive);
                };
                actions.Children.Add(skip);

                skipNote = new TextBlock
                {
                    FontSize = 10,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Right,
                    Foreground = new SolidColorBrush(Color.FromArgb(220, 0x90, 0xCA, 0xF9)),
                    Visibility = Visibility.Collapsed,
                    MaxWidth = 160,
                };
                actions.Children.Add(skipNote);
            }
            actions.Children.Add(go);

            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(check, 0);
            Grid.SetColumn(copy, 1);
            Grid.SetColumn(actions, 2);
            row.Children.Add(check);
            row.Children.Add(copy);
            row.Children.Add(actions);
            var frame = new Border
            {
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 5, 6, 5),
                Margin = new Thickness(0, 1, 0, 1),
                Child = row,
            };
            host.Children.Add(frame);
            _startupSteps.Add((step, check, frame, skip, skipNote));
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

    private TextBlock InfoNote(string en, string zh)
    {
        var tb = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.75,
            Margin = new Thickness(0, 2, 0, 4),
        };
        _relocalizers.Add(() => tb.Text = P(en, zh));
        tb.Text = P(en, zh);
        return tb;
    }

    private FrameworkElement BuildStartupPressureGauge()
    {
        var title = new TextBlock
        {
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        _relocalizers.Add(() => title.Text = P("Live primary pressure", "即時一迴路壓力"));
        title.Text = P("Live primary pressure", "即時一迴路壓力");

        _startupPressureGaugeText = new TextBlock
        {
            FontSize = 12,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
        };
        _startupPressureGauge = new ProgressBar
        {
            Minimum = 0,
            Maximum = 2235,
            Width = 185,
            Height = 8,
        };
        var target = new TextBlock
        {
            FontSize = 10,
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
        };
        _relocalizers.Add(() => target.Text = P("Step 4 target: >=2235 psia", "第 4 步目標：>=2235 psia"));
        target.Text = P("Step 4 target: >=2235 psia", "第 4 步目標：>=2235 psia");

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(title);
        panel.Children.Add(_startupPressureGaugeText);
        panel.Children.Add(_startupPressureGauge);
        panel.Children.Add(target);

        var frame = new Border
        {
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 0x42, 0xA5, 0xF5)),
            Background = new SolidColorBrush(Color.FromArgb(22, 0x42, 0xA5, 0xF5)),
            Padding = new Thickness(9, 7, 9, 7),
            MinWidth = 210,
            Child = panel,
        };
        UpdateStartupPressureGauge();
        return frame;
    }

    private void UpdateStartupPressureGauge()
    {
        double psia = _sim.PrimaryPressure * 145.038;
        bool ok = _sim.PrimaryPressure >= 14.5;
        if (_startupPressureGauge is not null)
            _startupPressureGauge.Value = Math.Clamp(psia, 0, 2235);
        var brush = new SolidColorBrush(ok
            ? Color.FromArgb(255, 0x4C, 0xAF, 0x50)
            : Color.FromArgb(255, 0xFF, 0xB3, 0x00));
        if (_startupPressureGaugeText is not null)
        {
            _startupPressureGaugeText.Text = P(
                $"{psia:F0} / 2235 psia  ({_sim.PrimaryPressure:F2} MPa)",
                $"{psia:F0} / 2235 psia  ({_sim.PrimaryPressure:F2} MPa)");
            _startupPressureGaugeText.Foreground = brush;
        }
        if (_startupPressureStepText is not null)
        {
            _startupPressureStepText.Text = P(
                $"PZR {psia:F0}/2235 psia",
                $"穩壓 {psia:F0}/2235 psia");
            _startupPressureStepText.Foreground = brush;
        }
    }

    private FrameworkElement StartupControlFrame(string target, int stepNumber, Func<bool> isCorrect, FrameworkElement content)
    {
        var frame = new Border
        {
            CornerRadius = new CornerRadius(7),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6, 4, 6, 4),
            Child = content,
        };
        void Sync() => UpdateStartupControlHighlight(frame, target, stepNumber, isCorrect);
        _controlSyncers.Add(Sync);
        Sync();
        return frame;
    }

    private void UpdateStartupControlHighlight(Border frame, string target, int stepNumber, Func<bool> isCorrect)
    {
        bool easy = _sim.EasyStartupMode;
        bool active = easy
                      && CurrentStartupStepNumber() == stepNumber
                      && string.Equals(CurrentStartupTarget(), target, StringComparison.OrdinalIgnoreCase);
        bool correct = easy && isCorrect();

        var border = correct
            ? Color.FromArgb(210, 0x4C, 0xAF, 0x50)
            : active ? Color.FromArgb(220, 0xFF, 0xB3, 0x00)
            : Color.FromArgb(45, 0x99, 0x99, 0x99);
        var fill = correct
            ? Color.FromArgb(30, 0x4C, 0xAF, 0x50)
            : active ? Color.FromArgb(30, 0xFF, 0xB3, 0x00)
            : Color.FromArgb(0, 0, 0, 0);
        frame.BorderBrush = new SolidColorBrush(border);
        frame.Background = new SolidColorBrush(fill);
    }

    private int CurrentStartupStepNumber()
    {
        var steps = ReactorScenarios.StartupSequence();
        int done = ReactorScenarios.CompletedStartupSteps(steps, _sim);
        return done >= 0 && done < steps.Length ? done + 1 : 0;
    }

    private string CurrentStartupTarget()
    {
        var steps = ReactorScenarios.StartupSequence();
        int done = ReactorScenarios.CompletedStartupSteps(steps, _sim);
        return done >= 0 && done < steps.Length ? steps[done].ControlTarget : "";
    }

    private bool IsStartupModeSelected()
        => _sim.Mode == ReactorMode.Startup || _sim.Mode == ReactorMode.Run;

    private bool HasStartupPumpCount()
    {
        int count = 0;
        foreach (var running in _sim.RcpRunning) if (running) count++;
        return count >= 3;
    }

    private bool IsStartupFlowReady()
        => _sim.CoolantFlowFraction > 0.85;

    private bool IsStartupPressureStepReady()
        => _sim.PzrAutoPressureControl
           && _sim.PressurizerHeater
           && !_sim.PressurizerSpray
           && !_sim.ReliefValveOpen
           && _sim.PrimaryPressure > 14.5;

    private bool IsReactivityStepReady()
    {
        double avg = 0;
        foreach (var p in _sim.RodBankInsertion) avg += p;
        avg /= Math.Max(1, _sim.RodBankInsertion.Length);
        return avg < 60 || _sim.BoronPpm < 1000;
    }

    private bool IsGeneratorStepReady()
        => _sim.GeneratorBreakerClosed && _sim.ElectricPowerMW > 1.0;

    private FrameworkElement LabeledSlider(string en, string zh, double min, double max, double init, double step,
        Action<double> set, Func<double> read, string unit)
    {
        var label = new TextBlock { FontSize = 12 };
        var slider = new Slider { Minimum = min, Maximum = max, Value = init, StepFrequency = step, Width = 380 };
        void UpdateLabel() => label.Text = P(en, zh) + $"  ·  {read():F0}{unit}";
        void Sync()
        {
            var value = Math.Clamp(read(), min, max);
            if (Math.Abs(slider.Value - value) > Math.Max(step * 0.5, 0.01))
                slider.Value = value;
            UpdateLabel();
        }
        slider.ValueChanged += (_, ev) =>
        {
            if (!_syncingControlValues)
            {
                set(ev.NewValue);
                PersistenceService.I.NoteChanged();
            }
            UpdateLabel();
        };
        _controlSyncers.Add(Sync);
        _relocalizers.Add(UpdateLabel);
        Sync();
        return new StackPanel { Spacing = 2, Children = { label, slider } };
    }

    // Like LabeledSlider but formats the value with 2 decimals (for small gpm leak rates).
    private FrameworkElement LeakSlider(string en, string zh, double min, double max, double init, double step,
        Action<double> set, Func<double> read)
    {
        var label = new TextBlock { FontSize = 12 };
        var slider = new Slider { Minimum = min, Maximum = max, Value = init, StepFrequency = step, Width = 380 };
        void UpdateLabel() => label.Text = P(en, zh) + $"  ·  {read():F2} gpm";
        void Sync()
        {
            var value = Math.Clamp(read(), min, max);
            if (Math.Abs(slider.Value - value) > Math.Max(step * 0.5, 0.001))
                slider.Value = value;
            UpdateLabel();
        }
        slider.ValueChanged += (_, ev) =>
        {
            if (!_syncingControlValues)
            {
                set(ev.NewValue);
                PersistenceService.I.NoteChanged();
            }
            UpdateLabel();
        };
        _controlSyncers.Add(Sync);
        _relocalizers.Add(UpdateLabel);
        Sync();
        return new StackPanel { Spacing = 2, Children = { label, slider } };
    }

    private FrameworkElement WrapLabel(string en, string zh, UIElement content)
    {
        var label = new TextBlock { FontSize = 12 };
        _relocalizers.Add(() => label.Text = P(en, zh));
        label.Text = P(en, zh);
        return new StackPanel { Spacing = 4, Children = { label, content } };
    }

    private ToggleButton MakeToggle(string en, string zh, Action<bool> set, Func<bool>? read = null)
    {
        var tg = new ToggleButton { Content = P(en, zh) };
        tg.Checked += (_, _) =>
        {
            if (_syncingControlValues) return;
            set(true);
            PersistenceService.I.NoteChanged();
        };
        tg.Unchecked += (_, _) =>
        {
            if (_syncingControlValues) return;
            set(false);
            PersistenceService.I.NoteChanged();
        };
        if (read is not null)
        {
            _controlSyncers.Add(() =>
            {
                var value = read();
                if (tg.IsChecked != value) tg.IsChecked = value;
            });
            bool wasSyncing = _syncingControlValues;
            _syncingControlValues = true;
            try { tg.IsChecked = read(); }
            finally { _syncingControlValues = wasSyncing; }
        }
        _relocalizers.Add(() => tg.Content = P(en, zh));
        return tg;
    }

    // ================================================================ buttons ====
    private void Scram_Click(object sender, RoutedEventArgs e)
    {
        _sim.Scram();
        PersistenceService.I.NoteChanged(); // capture this deliberate safety action promptly
    }

    private void ResetTrip_Click(object sender, RoutedEventArgs e)
    {
        if (_sim.Mode == ReactorMode.Meltdown) return;
        _sim.ResetTrip();
        PersistenceService.I.NoteChanged();
    }

    private void AutoRun_Toggled(object sender, RoutedEventArgs e)
    {
        if (_syncingControlValues) return;
        _sim.AutoRodControl = AutoRunToggle.IsOn;
        PersistenceService.I.NoteChanged();
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

            if (ReactorRealShutdownArm.Armed)
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
                    // CRITICAL: flush ALL persisted state to disk SYNCHRONOUSLY before we power the
                    // PC off, so nothing is lost when the machine shuts down. 關機前先同步保存全部狀態。
                    CrashLogger.Guard("reactor:pre-shutdown-flush", () => PersistenceService.I.Flush());
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
        // Persist the clean cold-shutdown state immediately so a restart doesn't resume a meltdown.
        _ = PersistenceService.I.FlushAsync();
    }
}
