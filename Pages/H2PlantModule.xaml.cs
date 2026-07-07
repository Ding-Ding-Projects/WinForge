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
/// 氫電解廠 · Hydrogen Electrolysis Plant — a reactor-powered industrial load. Reads the live reactor
/// snapshot from <see cref="ReactorStatusApiService"/> every ~500 ms; the electrolyser stack warms up,
/// splits water into H2 (kg/h) and fills a storage tank. Idle whenever the reactor is not generating.
/// Bilingual, anti-leak, never throws.
/// </summary>
public sealed partial class H2PlantModule : Page
{
    private readonly H2PlantService _plant = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastTick = DateTime.UtcNow;
    private bool _suppress;

    // meter accents
    private static readonly Color GreenAccent = Color.FromArgb(255, 0x3F, 0xB9, 0x50);
    private static readonly Color AmberAccent = Color.FromArgb(255, 0xE0, 0x9F, 0x2A);
    private static readonly Color IdleAccent = Color.FromArgb(255, 0x7A, 0x7A, 0x7A);

    public H2PlantModule()
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
        RenderText();
        SyncFromState();
        _lastTick = DateTime.UtcNow;
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
            Header.Title = "Hydrogen Electrolysis · 氫電解制氫廠";
            Header.Subtitle = P("A reactor-powered load: split water into green hydrogen with the station's spare megawatts.",
                "反應堆供電嘅負載：用電站嘅剩餘兆瓦電解水，製造綠氫。");
            HeaderBlurb.Text = P(
                "High-temperature electrolysers draw megawatts from the nuclear station to split water into hydrogen. The stack must warm up before it runs efficiently, so keep the reactor generating.",
                "高溫電解槽由核電站抽取兆瓦電力，將水電解成氫氣。電解堆要先升溫先至有效率運行，所以要保持反應堆發電。");

            ReactorTitle.Text = P("Reactor output available", "反應堆可用電力");
            RunTitle.Text = P("Run the electrolysers", "運行電解槽");
            ReadoutTitle.Text = P("Plant readouts", "廠房讀數");
            VentButton.Content = P("Vent tank", "放空儲罐");
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
            RunSwitch.IsOn = _plant.Running;
            LoadSlider.Value = Math.Clamp(_plant.RequestedLoadMW, 0, H2PlantService.PlantCapacityMW);
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    private void Run_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        _plant.Running = RunSwitch.IsOn;
        UpdateUi();
    }

    private void Load_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        _plant.RequestedLoadMW = e.NewValue;
        UpdateUi();
    }

    private void Vent_Click(object sender, RoutedEventArgs e)
    {
        _plant.VentTank();
        UpdateUi();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _plant.Reset();
        SyncFromState();
        UpdateUi();
    }

    private void OnTick(object? sender, object e)
    {
        try
        {
            var now = DateTime.UtcNow;
            double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0.0, 1.0);
            _lastTick = now;
            _plant.Tick(dt, ReactorStatusApiService.I.LastSnapshot);
            UpdateUi();
        }
        catch { }
    }

    private void UpdateUi()
    {
        try
        {
            bool powerAvailable = _plant.PowerAvailable;

            // Reactor availability meter
            AvailableBar.Value = Math.Clamp(_plant.ReactorAvailableMW, 0, H2PlantService.ReactorMaxMWe);
            AvailableBar.Foreground = new SolidColorBrush(powerAvailable ? GreenAccent : IdleAccent);
            ReactorModeText.Text = P($"MODE {_plant.ReactorMode}", $"模式 {_plant.ReactorMode}");
            AvailableText.Text = P(
                $"{_plant.ReactorAvailableMW:0} MWe available of {H2PlantService.ReactorMaxMWe:0} MWe station output",
                $"可用 {_plant.ReactorAvailableMW:0} MWe（電站總輸出 {H2PlantService.ReactorMaxMWe:0} MWe）");

            // Empty-state InfoBar when no nuclear power
            if (!powerAvailable)
            {
                IdleBar.Severity = InfoBarSeverity.Warning;
                IdleBar.Title = P("Needs nuclear power", "需要核電");
                IdleBar.Message = P(
                    "The electrolysers are idle until the reactor is generating. Start up the reactor (leave MODE 5 cold shutdown) — the stack is cooling and production is paused.",
                    "電解槽會一直閒置，直到反應堆開始發電。請啟動反應堆（離開 MODE 5 冷停機）— 電解堆正在降溫，制氫已暫停。");
                IdleBar.IsOpen = true;
            }
            else
            {
                IdleBar.IsOpen = false;
            }

            // Run switch status line
            if (!_plant.Running)
                RunStatus.Text = P("Idle — electrolysers off.", "閒置 — 電解槽已關。");
            else if (!powerAvailable)
                RunStatus.Text = P("Armed — waiting for reactor power.", "已準備 — 等待反應堆供電。");
            else
                RunStatus.Text = P($"Running — drawing {_plant.DrawnMW:0} MW.", $"運行中 — 抽取 {_plant.DrawnMW:0} MW。");

            LoadLabel.Text = P(
                $"Requested load: {_plant.RequestedLoadMW:0} MW (max {H2PlantService.PlantCapacityMW:0} MW)",
                $"要求負載：{_plant.RequestedLoadMW:0} MW（上限 {H2PlantService.PlantCapacityMW:0} MW）");

            // Readouts
            DrawnValue.Text = P($"Drawn power: {_plant.DrawnMW:0.0} MW", $"抽取功率：{_plant.DrawnMW:0.0} MW");
            RateValue.Text = P($"Rate: {_plant.RateKgPerHour:0.0} kg/h", $"產率：{_plant.RateKgPerHour:0.0} kg/h");
            StackValue.Text = P($"Stack temp: {_plant.StackTempC:0} degC", $"電解堆溫度：{_plant.StackTempC:0} degC");
            EffValue.Text = P($"Efficiency: {_plant.EfficiencyKgPerMWh:0.0} kg/MWh", $"效率：{_plant.EfficiencyKgPerMWh:0.0} kg/MWh");

            double warmthPct = Math.Clamp(_plant.Warmth * 100.0, 0, 100);
            WarmthBar.Value = warmthPct;
            WarmthBar.Foreground = new SolidColorBrush(_plant.Warmth >= 0.9 ? GreenAccent : AmberAccent);
            StackTempLabel.Text = P($"Stack warm-up: {warmthPct:0}%", $"電解堆升溫：{warmthPct:0}%");

            double fillPct = Math.Clamp(_plant.TankFillFraction * 100.0, 0, 100);
            TankBar.Value = fillPct;
            TankBar.Foreground = new SolidColorBrush(fillPct >= 95 ? AmberAccent : GreenAccent);
            TankLabel.Text = P(
                $"Storage tank: {_plant.TankKg:0} / {H2PlantService.TankCapacityKg:0} kg ({fillPct:0}%)",
                $"儲氫罐：{_plant.TankKg:0} / {H2PlantService.TankCapacityKg:0} kg（{fillPct:0}%）");

            TotalValue.Text = P($"Total hydrogen produced: {_plant.TotalProducedKg:0} kg",
                $"累計制氫：{_plant.TotalProducedKg:0} kg");

            VentButton.IsEnabled = _plant.TankKg > 0.5;
        }
        catch { }
    }
}
