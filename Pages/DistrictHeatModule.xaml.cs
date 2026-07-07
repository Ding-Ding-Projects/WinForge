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
/// 區域供熱（熱電聯產）· District Heating (cogeneration / CHP) — a reactor-powered thermal load. Reads the live
/// reactor snapshot from <see cref="ReactorStatusApiService"/> every ~500 ms; the station taps electrical
/// power plus waste heat to supply hot water to a city network. The operator sets a heat demand / power draw
/// and a target supply temperature; an outdoor-temperature setting raises demand when cold. Delivered MW-th,
/// homes heated, and network supply temperature are shown, with a "cold homes" warning when the reactor
/// cannot meet demand. Sells heat into the shared reactor economy (⚡). Idle whenever the reactor is not
/// generating. Bilingual (粵語), anti-leak, never throws.
/// </summary>
public sealed partial class DistrictHeatModule : Page
{
    private readonly DistrictHeatService _plant = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick; // integer tick counter drives sim timing (not DateTime.Now)
    private bool _suppress;

    // meter accents
    private static readonly Color GreenAccent = Color.FromArgb(255, 0x3F, 0xB9, 0x50);
    private static readonly Color AmberAccent = Color.FromArgb(255, 0xE0, 0x9F, 0x2A);
    private static readonly Color RedAccent = Color.FromArgb(255, 0xD1, 0x3B, 0x3B);
    private static readonly Color IdleAccent = Color.FromArgb(255, 0x7A, 0x7A, 0x7A);

    public DistrictHeatModule()
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
            Header.Title = "District Heating · 區域供熱";
            Header.Subtitle = P("A reactor-powered cogeneration load: pipe the station's power and waste heat into a city hot-water network.",
                "反應堆供電嘅熱電聯產負載：將電站嘅電力同廢熱送入城市熱水管網。");
            HeaderBlurb.Text = P(
                "This combined heat-and-power (CHP) plant taps electrical power plus recovered waste heat from the nuclear station to supply hot water to a district network. Set a heat demand and a target supply temperature — colder outdoor weather raises demand. It only delivers heat while the reactor is generating.",
                "呢個熱電聯產（CHP）廠房抽取核電站嘅電力同回收廢熱，向區域管網供應熱水。設定熱負荷同目標供水溫度 — 室外越凍需求越大。淨係喺反應堆發電嗰陣先送熱。");

            ReactorTitle.Text = P("Reactor output available", "反應堆可用電力");
            RunTitle.Text = P("Run the heat network", "運行供熱管網");
            ReadoutTitle.Text = P("Network readouts", "管網讀數");
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
            LoadSlider.Value = Math.Clamp(_plant.RequestedDrawMW, 0, DistrictHeatService.PlantMaxDrawMW);
            SupplySlider.Value = Math.Clamp(_plant.TargetSupplyC, DistrictHeatService.MinSupplyC, DistrictHeatService.MaxSupplyC);
            OutdoorSlider.Value = Math.Clamp(_plant.OutdoorC, DistrictHeatService.MinOutdoorC, DistrictHeatService.MaxOutdoorC);
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

    private void RunToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _plant.Running = !_plant.Running;
            _suppress = true; RunSwitch.IsOn = _plant.Running; _suppress = false;
            UpdateUi();
        }
        catch { }
    }

    private void Load_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        _plant.RequestedDrawMW = e.NewValue;
        UpdateUi();
    }

    private void Supply_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        _plant.TargetSupplyC = e.NewValue;
        UpdateUi();
    }

    private void Outdoor_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        _plant.OutdoorC = e.NewValue;
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
            AvailableBar.Value = Math.Clamp(_plant.ReactorAvailableMW, 0, DistrictHeatService.ReactorMaxMWe);
            AvailableBar.Foreground = new SolidColorBrush(powerAvailable ? GreenAccent : IdleAccent);
            ReactorModeText.Text = P($"MODE {_plant.ReactorMode}", $"模式 {_plant.ReactorMode}");
            AvailableText.Text = P(
                $"{_plant.ReactorAvailableMW:0} MWe available of {DistrictHeatService.ReactorMaxMWe:0} MWe station output",
                $"可用 {_plant.ReactorAvailableMW:0} MWe（電站總輸出 {DistrictHeatService.ReactorMaxMWe:0} MWe）");

            // Empty-state InfoBar when no nuclear power
            if (!powerAvailable)
            {
                IdleBar.Severity = InfoBarSeverity.Warning;
                IdleBar.Title = P("District cold — needs nuclear power", "區域供熱停頓 — 需要核電");
                IdleBar.Message = P(
                    "The district heat network is cold until the reactor is generating. Start up the reactor (leave MODE 5 cold shutdown) — no hot water is reaching homes.",
                    "反應堆一日未發電，區域供熱管網就一直凍。請啟動反應堆（離開 MODE 5 冷停機）— 目前冇熱水送到住戶。");
                IdleBar.IsOpen = true;
            }
            else
            {
                IdleBar.IsOpen = false;
            }

            // Run switch status line
            if (!_plant.Running)
                RunStatus.Text = P("Idle — heat network off.", "閒置 — 供熱管網已關。");
            else if (!powerAvailable)
                RunStatus.Text = P("Armed — waiting for reactor power.", "已準備 — 等待反應堆供電。");
            else
                RunStatus.Text = P($"Running — delivering {_plant.DeliveredMWth:0} MW-th.", $"運行中 — 供熱 {_plant.DeliveredMWth:0} MW-th。");

            RunButton.Content = _plant.Running ? P("Idle", "閒置") : P("Run", "運行");

            LoadLabel.Text = P(
                $"Heat demand / power draw: {_plant.RequestedDrawMW:0} MW (max {DistrictHeatService.PlantMaxDrawMW:0} MW)",
                $"熱負荷／抽取功率：{_plant.RequestedDrawMW:0} MW（上限 {DistrictHeatService.PlantMaxDrawMW:0} MW）");
            SupplyLabel.Text = P(
                $"Target supply temperature: {_plant.TargetSupplyC:0} °C",
                $"目標供水溫度：{_plant.TargetSupplyC:0} °C");
            OutdoorLabel.Text = P(
                $"Outdoor temperature: {_plant.OutdoorC:0} °C (colder ⇒ more demand)",
                $"室外氣溫：{_plant.OutdoorC:0} °C（越凍需求越大）");

            // Readouts
            DeliveredValue.Text = P(
                $"Heat delivered: {_plant.DeliveredMWth:0.0} MW-th  (demand {_plant.DemandMWth:0.0} MW-th)",
                $"供熱量：{_plant.DeliveredMWth:0.0} MW-th（需求 {_plant.DemandMWth:0.0} MW-th）");
            HomesValue.Text = P(
                $"Homes heated: {_plant.HomesHeated:N0}",
                $"供暖住戶：{_plant.HomesHeated:N0}");
            SupplyValue.Text = P(
                $"Network supply temperature: {_plant.SupplyTempC:0.0} °C",
                $"管網供水溫度：{_plant.SupplyTempC:0.0} °C");

            double covPct = Math.Clamp(_plant.DemandCoverage * 100.0, 0, 100);
            CoverageBar.Value = covPct;
            Color covColor = !powerAvailable || !_plant.Running ? IdleAccent
                : _plant.ColdHomes ? (covPct < 50 ? RedAccent : AmberAccent)
                : GreenAccent;
            CoverageBar.Foreground = new SolidColorBrush(covColor);
            CoverageLabel.Text = P(
                $"Demand met: {covPct:0}%",
                $"需求覆蓋率：{covPct:0}%");

            // Cold-homes warning
            if (_plant.Running && _plant.ColdHomes)
            {
                ColdBar.Severity = powerAvailable ? InfoBarSeverity.Warning : InfoBarSeverity.Error;
                ColdBar.Title = P("Cold homes", "住戶受凍");
                ColdBar.Message = powerAvailable
                    ? P("The reactor can't meet full heat demand — raise the power draw or the reactor's output, or homes stay cold.",
                        "反應堆滿足唔到全部熱需求 — 加大抽取功率或者反應堆輸出，否則住戶會凍。")
                    : P("No reactor power — the network is cold and homes are losing heat.",
                        "冇反應堆供電 — 管網已凍，住戶正在失溫。");
                ColdBar.IsOpen = true;
            }
            else
            {
                ColdBar.IsOpen = false;
            }

            TotalValue.Text = P(
                $"Total heat delivered: {_plant.TotalDeliveredMWhTh:0} MWh-th",
                $"累計供熱量：{_plant.TotalDeliveredMWhTh:0} MWh-th");
        }
        catch { }
    }
}
