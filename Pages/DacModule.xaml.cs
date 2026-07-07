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
/// 直接空氣捕集廠 · Direct Air Capture (DAC) plant — a reactor-powered carbon-scrubbing load. Reads the live
/// reactor snapshot from <see cref="ReactorStatusApiService"/> every ~500 ms; contactor fans draw megawatts and
/// scrub CO₂ from ambient air (tonnes/hour), which accumulates into permanent storage and earns carbon credits.
/// Credits are sold into the shared reactor economy (⚡). Idle whenever the reactor is not generating — fans spin
/// down. Bilingual (粵語), anti-leak, never throws.
/// </summary>
public sealed partial class DacModule : Page
{
    private readonly DacService _plant = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick; // integer tick counter drives sim timing (not DateTime.Now)
    private bool _suppress;

    // meter accents
    private static readonly Color GreenAccent = Color.FromArgb(255, 0x3F, 0xB9, 0x50);
    private static readonly Color IdleAccent = Color.FromArgb(255, 0x7A, 0x7A, 0x7A);

    public DacModule()
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
            Header.Title = "Carbon Capture (DAC) · 碳捕集";
            Header.Subtitle = P("A reactor-powered load: scrub CO₂ straight out of the air with the station's spare megawatts.",
                "反應堆供電嘅負載：用電站嘅剩餘兆瓦直接喺空氣抽走二氧化碳。");
            HeaderBlurb.Text = P(
                "Giant contactor fans pull ambient air across a sorbent that binds atmospheric CO₂, then the nuclear station's megawatts regenerate the sorbent to release captured carbon for permanent storage. Direct air capture is very energy-intensive and only runs while the reactor is generating.",
                "巨型接觸器風扇將空氣抽過吸附劑，令大氣中嘅二氧化碳被吸住，然後核電站嘅兆瓦電力再生吸附劑，釋放捕集到嘅碳去永久封存。直接空氣捕集非常耗電，淨係喺反應堆發電嗰陣先運行。");

            ReactorTitle.Text = P("Reactor output available", "反應堆可用電力");
            RunTitle.Text = P("Run the capture fans", "運行捕集風扇");
            ReadoutTitle.Text = P("Plant readouts", "廠房讀數");
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
            LoadSlider.Value = Math.Clamp(_plant.RequestedDrawMW, 0, DacService.PlantMaxDrawMW);
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
            AvailableBar.Value = Math.Clamp(_plant.ReactorAvailableMW, 0, DacService.ReactorMaxMWe);
            AvailableBar.Foreground = new SolidColorBrush(powerAvailable ? GreenAccent : IdleAccent);
            ReactorModeText.Text = P($"MODE {_plant.ReactorMode}", $"模式 {_plant.ReactorMode}");
            AvailableText.Text = P(
                $"{_plant.ReactorAvailableMW:0} MWe available of {DacService.ReactorMaxMWe:0} MWe station output",
                $"可用 {_plant.ReactorAvailableMW:0} MWe（電站總輸出 {DacService.ReactorMaxMWe:0} MWe）");

            // Empty-state InfoBar when no nuclear power
            if (!powerAvailable)
            {
                IdleBar.Severity = InfoBarSeverity.Warning;
                IdleBar.Title = P("Capture halted — needs nuclear power", "捕集已停 — 需要核電");
                IdleBar.Message = P(
                    "The DAC plant is idle and the fans have spun down until the reactor is generating. Start up the reactor (leave MODE 5 cold shutdown) — no CO₂ is being captured.",
                    "直接空氣捕集廠會一直閒置、風扇停轉，直到反應堆開始發電。請啟動反應堆（離開 MODE 5 冷停機）— 目前冇捕集二氧化碳。");
                IdleBar.IsOpen = true;
            }
            else
            {
                IdleBar.IsOpen = false;
            }

            // Run switch status line
            if (!_plant.Running)
                RunStatus.Text = P("Idle — capture fans off.", "閒置 — 捕集風扇已關。");
            else if (!powerAvailable)
                RunStatus.Text = P("Armed — waiting for reactor power.", "已準備 — 等待反應堆供電。");
            else
                RunStatus.Text = P($"Capturing — drawing {_plant.DrawnMW:0} MW.", $"捕集中 — 抽取 {_plant.DrawnMW:0} MW。");

            LoadLabel.Text = P(
                $"Power draw: {_plant.RequestedDrawMW:0} MW (max {DacService.PlantMaxDrawMW:0} MW)",
                $"抽取功率：{_plant.RequestedDrawMW:0} MW（上限 {DacService.PlantMaxDrawMW:0} MW）");

            // Readouts
            DrawnValue.Text = P($"Drawn power: {_plant.DrawnMW:0.0} MW", $"抽取功率：{_plant.DrawnMW:0.0} MW");
            RateValue.Text = P($"CO₂ captured: {_plant.RateTonnesPerHour:0.0} t/h", $"二氧化碳捕集：{_plant.RateTonnesPerHour:0.0} t/h");
            EnergyValue.Text = P($"Specific energy: {_plant.SpecificEnergyMWhPerT:0.00} MWh/t",
                $"比能耗：{_plant.SpecificEnergyMWhPerT:0.00} MWh/t");

            double spin = Math.Clamp(_plant.FanSpin * 100.0, 0, 100);
            FanValue.Text = P($"Contactor fans: {spin:0}% speed", $"接觸器風扇：{spin:0}% 轉速");

            TotalValue.Text = P($"Total CO₂ captured: {_plant.TotalCapturedTonnes:0.0} t",
                $"累計捕集二氧化碳：{_plant.TotalCapturedTonnes:0.0} t");

            EquivValue.Text = P($"= {_plant.CarsOffsetPerYear:0.0} cars offset / year",
                $"= 抵銷 {_plant.CarsOffsetPerYear:0.0} 架車一年嘅排放");

            CreditsValue.Text = P($"Carbon credits: {_plant.CarbonCredits:0.0}",
                $"碳信用：{_plant.CarbonCredits:0.0}");
        }
        catch { }
    }
}
