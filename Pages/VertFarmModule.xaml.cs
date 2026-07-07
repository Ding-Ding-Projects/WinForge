using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 垂直農場（補光燈陣列）· Vertical Farm (grow-light array) — a reactor-powered indoor farm. Reads the live
/// reactor snapshot from <see cref="ReactorStatusApiService"/> every ~500 ms; the operator sets a power draw
/// (MW) that runs LED grow-lights + HVAC over a canopy. Crops accrue growth while adequately lit and powered
/// (a photoperiod day/night cycle is driven off an integer tick counter). Drawn MW, active grow-lights, canopy
/// area lit, crop growth toward harvest, harvests completed and yield (kg) are shown, with a "lights out —
/// needs nuclear power" empty-state when the reactor cannot power the lights. Sells produce into the shared
/// reactor economy (⚡). Bilingual (粵語), anti-leak, never throws.
/// </summary>
public sealed partial class VertFarmModule : Page
{
    private readonly VertFarmService _farm = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick; // integer tick counter drives sim/photoperiod timing (not DateTime.Now)
    private bool _suppress;

    // meter accents
    private static readonly Color GreenAccent = Color.FromArgb(255, 0x3F, 0xB9, 0x50);
    private static readonly Color AmberAccent = Color.FromArgb(255, 0xE0, 0x9F, 0x2A);
    private static readonly Color IdleAccent = Color.FromArgb(255, 0x7A, 0x7A, 0x7A);

    public VertFarmModule()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { ReactorStatusApiService.I.Start(); } catch { }
        _tick = 0;
        RenderText();
        SyncFromState();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        RenderText();
        UpdateUi();
    }

    private void RenderText()
    {
        try
        {
            Header.Title = "Vertical Farm · 垂直農場";
            Header.Subtitle = P("A reactor-powered indoor farm: LED grow-lights and HVAC over a stacked canopy, running on nuclear watts.",
                "反應堆供電嘅室內農場：核電瓦特驅動多層苗床上嘅 LED 補光燈同空調。");
            HeaderBlurb.Text = P(
                "This vertical farm runs banks of LED grow-lights and climate control from the nuclear station. Dial the power draw to light more canopy — crops grow while adequately lit through the daily photoperiod, then you harvest and sell the produce for watts (⚡). It only grows while the reactor is generating.",
                "呢個垂直農場靠核電站嚟開一排排 LED 補光燈同環境控制。調校抽取功率去照亮更多苗床 — 喺每日光週期內夠光就會生長，之後你收成再賣農產換瓦特（⚡）。淨係反應堆發電嗰陣先會生長。");

            ReactorTitle.Text = P("Reactor output available", "反應堆可用電力");
            RunTitle.Text = P("Run the grow-light array", "運行補光燈陣列");
            ReadoutTitle.Text = P("Farm readouts", "農場讀數");
            ResetButton.Content = P("Reset", "重設");
            UpdateUi();
        }
        catch { }
    }

    private void SyncFromState()
    {
        try
        {
            _suppress = true;
            RunSwitch.IsOn = _farm.Running;
            DrawSlider.Value = Math.Clamp(_farm.RequestedDrawMW, VertFarmService.MinDrawMW, VertFarmService.FarmMaxDrawMW);
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    private void Run_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        _farm.Running = RunSwitch.IsOn;
        UpdateUi();
    }

    private void RunToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _farm.Running = !_farm.Running;
            _suppress = true; RunSwitch.IsOn = _farm.Running; _suppress = false;
            UpdateUi();
        }
        catch { }
    }

    private void Draw_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        _farm.RequestedDrawMW = e.NewValue;
        UpdateUi();
    }

    private void Harvest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _farm.Harvest();
            UpdateUi();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _farm.Reset();
        SyncFromState();
        UpdateUi();
    }

    private void OnTick(object? sender, object e)
    {
        try
        {
            _tick++;
            // 500 ms interval ⇒ 0.5 s of sim per tick (integer-counter driven, no wall clock).
            _farm.Tick(0.5, ReactorStatusApiService.I.LastSnapshot, _tick);
            UpdateUi();
        }
        catch { }
    }

    private void UpdateUi()
    {
        try
        {
            bool powerAvailable = _farm.PowerAvailable;

            // Reactor availability meter (MW, 0..1150)
            AvailableBar.Value = Math.Clamp(_farm.ReactorAvailableMW, 0, VertFarmService.ReactorMaxMWe);
            AvailableBar.Foreground = new SolidColorBrush(powerAvailable ? GreenAccent : IdleAccent);
            ReactorModeText.Text = P($"MODE {_farm.ReactorMode}", $"模式 {_farm.ReactorMode}");
            AvailableText.Text = P(
                $"{_farm.ReactorAvailableMW:0} MWe available of {VertFarmService.ReactorMaxMWe:0} MWe station output",
                $"可用 {_farm.ReactorAvailableMW:0} MWe（電站總輸出 {VertFarmService.ReactorMaxMWe:0} MWe）");

            // Empty-state InfoBar when no nuclear power
            if (!powerAvailable)
            {
                IdleBar.Severity = InfoBarSeverity.Warning;
                IdleBar.Title = P("Lights out — needs nuclear power", "熄燈 — 需要核電");
                IdleBar.Message = P(
                    "The grow-lights are dark until the reactor is generating. Start up the reactor (leave MODE 5 cold shutdown) — crops are stalled and may slowly spoil.",
                    "反應堆一日未發電，補光燈就一直熄。請啟動反應堆（離開 MODE 5 冷停機）— 農作物已停止生長，或會慢慢腐爛。");
                IdleBar.IsOpen = true;
            }
            else
            {
                IdleBar.IsOpen = false;
            }

            // Run switch status line
            if (!_farm.Running)
                RunStatus.Text = P("Idle — grow-light array off.", "閒置 — 補光燈陣列已關。");
            else if (!powerAvailable)
                RunStatus.Text = P("Armed — waiting for reactor power.", "已準備 — 等待反應堆供電。");
            else if (!_farm.DayPhase)
                RunStatus.Text = P("Running — night cycle (lights resting).", "運行中 — 夜間週期（補光燈休息）。");
            else
                RunStatus.Text = P($"Running — lighting {_farm.CanopyAreaM2:0} m² of canopy.", $"運行中 — 照亮 {_farm.CanopyAreaM2:0} m² 苗床。");

            RunButton.Content = _farm.Running ? P("Idle", "閒置") : P("Run", "運行");

            DrawLabel.Text = P(
                $"Grow-light power draw: {_farm.RequestedDrawMW:0} MW (max {VertFarmService.FarmMaxDrawMW:0} MW)",
                $"補光燈抽取功率：{_farm.RequestedDrawMW:0} MW（上限 {VertFarmService.FarmMaxDrawMW:0} MW）");

            // Readouts
            DrawValue.Text = P(
                $"Power drawn: {_farm.DrawnMW:0.0} MW  (grow-lights {_farm.GrowLightMW:0.0} MW)",
                $"抽取功率：{_farm.DrawnMW:0.0} MW（補光燈 {_farm.GrowLightMW:0.0} MW）");
            LightsValue.Text = P(
                $"Active grow-lights: {_farm.ActiveLights:N0}" + (_farm.LightsOn ? "" : P("  (off)", "（熄）")),
                $"啟用補光燈：{_farm.ActiveLights:N0}" + (_farm.LightsOn ? "" : "（熄）"));
            CanopyValue.Text = P(
                $"Canopy area lit: {_farm.CanopyAreaM2:N0} m²",
                $"照亮苗床面積：{_farm.CanopyAreaM2:N0} m²");

            double growth = Math.Clamp(_farm.GrowthPct, 0, 100);
            GrowthBar.Value = growth;
            Color growColor = !powerAvailable || !_farm.Running ? IdleAccent
                : _farm.ReadyToHarvest ? GreenAccent
                : _farm.LightsOn ? GreenAccent : AmberAccent;
            GrowthBar.Foreground = new SolidColorBrush(growColor);
            GrowthLabel.Text = _farm.ReadyToHarvest
                ? P("Crop growth: ready to harvest", "作物生長：可以收成")
                : P($"Crop growth: {growth:0}% toward harvest", $"作物生長：{growth:0}% 至收成");

            // Lights-out warning while running without power
            if (_farm.Running && !powerAvailable)
            {
                LightsOutBar.Severity = InfoBarSeverity.Error;
                LightsOutBar.Title = P("Lights out — crops stalled", "熄燈 — 作物停頓");
                LightsOutBar.Message = P(
                    "No reactor power — the grow-lights are dark and growth has paused. Produce may slowly spoil until power returns.",
                    "冇反應堆供電 — 補光燈熄咗，生長已暫停。回電之前農產或會慢慢腐爛。");
                LightsOutBar.IsOpen = true;
            }
            else
            {
                LightsOutBar.IsOpen = false;
            }

            HarvestButton.Content = P("Harvest", "收成");
            HarvestButton.IsEnabled = _farm.ReadyToHarvest;

            HarvestsValue.Text = P(
                $"Harvests completed: {_farm.HarvestsCompleted:N0}",
                $"完成收成：{_farm.HarvestsCompleted:N0}");
            YieldValue.Text = P(
                $"Total yield: {_farm.TotalYieldKg:N0} kg",
                $"總產量：{_farm.TotalYieldKg:N0} kg");
            EarnedValue.Text = P(
                $"Produce sold: {_farm.DepositedWatts:0.0} ⚡ earned",
                $"已售農產：賺得 {_farm.DepositedWatts:0.0} ⚡");
        }
        catch { }
    }
}
