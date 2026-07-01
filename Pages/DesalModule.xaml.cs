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
/// 海水淡化廠 · Seawater Desalination Plant — a reactor-powered industrial load. Reads the live reactor
/// snapshot from <see cref="ReactorStatusApiService"/> every ~500 ms; a reverse-osmosis train draws
/// megawatts and turns seawater into potable fresh water (m³/h), filling a storage tank. Sells water into
/// the shared reactor economy (⚡). Idle whenever the reactor is not generating. Bilingual (粵語), anti-leak,
/// never throws.
/// </summary>
public sealed partial class DesalModule : Page
{
    private readonly DesalService _plant = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick; // integer tick counter drives sim timing (not DateTime.Now)
    private bool _suppress;

    // meter accents
    private static readonly Color GreenAccent = Color.FromArgb(255, 0x3F, 0xB9, 0x50);
    private static readonly Color AmberAccent = Color.FromArgb(255, 0xE0, 0x9F, 0x2A);
    private static readonly Color IdleAccent = Color.FromArgb(255, 0x7A, 0x7A, 0x7A);

    public DesalModule()
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
            Header.Title = "Seawater Desalination · 海水淡化廠";
            Header.Subtitle = P("A reactor-powered load: turn seawater into fresh drinking water with the station's spare megawatts.",
                "反應堆供電嘅負載：用電站嘅剩餘兆瓦將海水變成食水。");
            HeaderBlurb.Text = P(
                "A reverse-osmosis train draws megawatts from the nuclear station to push seawater through membranes and produce potable fresh water. It only runs while the reactor is generating.",
                "反滲透機組由核電站抽取兆瓦電力，將海水壓過薄膜，製造可飲用嘅淡水。淨係喺反應堆發電嗰陣先運行。");

            ReactorTitle.Text = P("Reactor output available", "反應堆可用電力");
            RunTitle.Text = P("Run the RO train", "運行反滲透機組");
            ReadoutTitle.Text = P("Plant readouts", "廠房讀數");
            EmptyButton.Content = P("Empty tank", "倒空儲水缸");
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
            LoadSlider.Value = Math.Clamp(_plant.RequestedDrawMW, 0, DesalService.PlantMaxDrawMW);
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
        _plant.RequestedDrawMW = e.NewValue;
        UpdateUi();
    }

    private void Empty_Click(object sender, RoutedEventArgs e)
    {
        _plant.EmptyTank();
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
            _tick++;
            // 500 ms interval ⇒ 0.5 s of sim per tick (integer-counter driven, no wall clock).
            _plant.Tick(0.5, ReactorStatusApiService.I.LastSnapshot);
            UpdateUi();
        }
        catch { }
    }

    private void UpdateUi()
    {
        try
        {
            bool powerAvailable = _plant.PowerAvailable;

            // Reactor availability meter (MW, 0..1150)
            AvailableBar.Value = Math.Clamp(_plant.ReactorAvailableMW, 0, DesalService.ReactorMaxMWe);
            AvailableBar.Foreground = new SolidColorBrush(powerAvailable ? GreenAccent : IdleAccent);
            ReactorModeText.Text = P($"MODE {_plant.ReactorMode}", $"模式 {_plant.ReactorMode}");
            AvailableText.Text = P(
                $"{_plant.ReactorAvailableMW:0} MWe available of {DesalService.ReactorMaxMWe:0} MWe station output",
                $"可用 {_plant.ReactorAvailableMW:0} MWe（電站總輸出 {DesalService.ReactorMaxMWe:0} MWe）");

            // Empty-state InfoBar when no nuclear power
            if (!powerAvailable)
            {
                IdleBar.Severity = InfoBarSeverity.Warning;
                IdleBar.Title = P("Needs nuclear power", "需要核電");
                IdleBar.Message = P(
                    "The desalination plant is idle until the reactor is generating. Start up the reactor (leave MODE 5 cold shutdown) — no seawater is being processed.",
                    "海水淡化廠會一直閒置，直到反應堆開始發電。請啟動反應堆（離開 MODE 5 冷停機）— 目前冇處理海水。");
                IdleBar.IsOpen = true;
            }
            else
            {
                IdleBar.IsOpen = false;
            }

            // Run switch status line
            if (!_plant.Running)
                RunStatus.Text = P("Idle — RO train off.", "閒置 — 反滲透機組已關。");
            else if (!powerAvailable)
                RunStatus.Text = P("Armed — waiting for reactor power.", "已準備 — 等待反應堆供電。");
            else
                RunStatus.Text = P($"Running — drawing {_plant.DrawnMW:0} MW.", $"運行中 — 抽取 {_plant.DrawnMW:0} MW。");

            LoadLabel.Text = P(
                $"Power draw: {_plant.RequestedDrawMW:0} MW (max {DesalService.PlantMaxDrawMW:0} MW)",
                $"抽取功率：{_plant.RequestedDrawMW:0} MW（上限 {DesalService.PlantMaxDrawMW:0} MW）");

            // Readouts
            DrawnValue.Text = P($"Drawn power: {_plant.DrawnMW:0.0} MW", $"抽取功率：{_plant.DrawnMW:0.0} MW");
            RateValue.Text = P($"Fresh water: {_plant.RateM3PerHour:0} m³/h", $"淡水產量：{_plant.RateM3PerHour:0} m³/h");
            EnergyValue.Text = P($"Specific energy: {_plant.SpecificEnergyKWhPerM3:0.00} kWh/m³",
                $"比能耗：{_plant.SpecificEnergyKWhPerM3:0.00} kWh/m³");

            double fillPct = Math.Clamp(_plant.TankFillFraction * 100.0, 0, 100);
            TankBar.Value = fillPct;
            TankBar.Foreground = new SolidColorBrush(fillPct >= 95 ? AmberAccent : GreenAccent);
            TankLabel.Text = P(
                $"Storage tank: {_plant.TankM3:0} / {DesalService.TankCapacityM3:0} m³ ({fillPct:0}%)",
                $"儲水缸：{_plant.TankM3:0} / {DesalService.TankCapacityM3:0} m³（{fillPct:0}%）");

            TotalValue.Text = P($"Total fresh water produced: {_plant.TotalProducedM3:0} m³",
                $"累計淡水產量：{_plant.TotalProducedM3:0} m³");

            EmptyButton.IsEnabled = _plant.TankM3 > 0.5;
        }
        catch { }
    }
}
