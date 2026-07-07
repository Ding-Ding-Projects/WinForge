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
/// 電網調度中心 · Grid Dispatch Center — a reactor-powered load. Reads live reactor output via
/// <see cref="ReactorStatusApiService"/> and lets the operator sell a chosen slice of available MWe
/// into a simulated electricity market (deterministic demand, demand-linked spot price, grid frequency
/// around 60 Hz). Accrues revenue only while the reactor is actually generating; otherwise shows a
/// prominent "start the reactor" empty-state. Pure managed, never throws.
/// </summary>
public sealed partial class GridDispatchModule : Page
{
    private readonly GridDispatchService _grid = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastTick = DateTime.UtcNow;
    private bool _suppress;

    public GridDispatchModule()
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
        Render();
        _lastTick = DateTime.UtcNow;
        UpdateStep();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private void Render()
    {
        try
        {
            Header.Title = "Grid Dispatch Center · 電網調度中心";
            HeaderBlurb.Text = P(
                "Sell the reactor's spare electricity into a simulated grid. Set a dispatch level, arm selling, and watch revenue accrue as demand, spot price and grid frequency move.",
                "將反應堆嘅多餘電力賣落模擬電網。設定調度量、開始售電，睇住需求、電價同電網頻率浮動，收入慢慢累積。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            DispatchTitle.Text = P("Dispatch setpoint", "調度設定點");
            DispatchLabel.Text = P("How much power to sell (MWe)", "要賣幾多電（MWe）");
            MarketTitle.Text = P("Simulated grid market", "模擬電網市場");

            RevenueCaption.Text = P("Total revenue", "總收入");
            PriceCaption.Text = P("Spot price", "即時電價");
            DemandCaption.Text = P("Grid demand", "電網需求");
            DispatchedCaption.Text = P("Now dispatching", "現正調度");
            FrequencyCaption.Text = P("Grid frequency", "電網頻率");
            ResetButton.Content = P("Reset", "重設");

            UpdateSellButton();
            UpdateStep();
        }
        catch { }
    }

    private void UpdateSellButton()
    {
        SellButton.Content = _grid.Selling ? P("Stop selling", "停止售電") : P("Start selling", "開始售電");
    }

    private void DispatchSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        UpdateStep();
    }

    private void Sell_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_grid.Selling) _grid.StopSelling();
            else _grid.StartSelling();
            UpdateSellButton();
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _grid.Reset();
            _suppress = true;
            DispatchSlider.Value = 0;
            _suppress = false;
            UpdateSellButton();
            UpdateStep();
        }
        catch { }
    }

    private void OnTick(object? sender, object e) => UpdateStep();

    private void UpdateStep()
    {
        try
        {
            var now = DateTime.UtcNow;
            double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0.0, 2.0);
            _lastTick = now;

            ReactorStatusSnapshot snap;
            try { snap = ReactorStatusApiService.I.LastSnapshot; } catch { snap = default; }

            // ReactorStatusSnapshot is a value struct — LastSnapshot is always present (defaults to Offline).
            double available = double.IsNaN(snap.ElectricMW) || snap.ElectricMW < 0 ? 0 : snap.ElectricMW;
            string mode = string.IsNullOrWhiteSpace(snap.Mode) ? "?" : snap.Mode;
            bool scrammed = snap.IsScrammed;
            bool meltdown = snap.IsMeltdown;
            bool coldMode = mode.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0
                            || mode.IndexOf("cold", StringComparison.OrdinalIgnoreCase) >= 0;
            bool generating = snap.IsGenerating && available > 1.0 && !scrammed && !meltdown && !coldMode;

            // Keep the dispatch slider capped to available output.
            double sliderMax = Math.Max(50, Math.Round(available <= 1 ? 1150 : available));
            if (Math.Abs(DispatchSlider.Maximum - sliderMax) > 0.5)
            {
                _suppress = true;
                DispatchSlider.Maximum = sliderMax;
                if (DispatchSlider.Value > sliderMax) DispatchSlider.Value = sliderMax;
                _suppress = false;
            }

            double setpoint = double.IsNaN(DispatchSlider.Value) ? 0 : DispatchSlider.Value;

            _grid.Tick(dt, setpoint, available, generating);

            // Live output meter + colour.
            OutputBar.Value = Math.Clamp(available, 0, OutputBar.Maximum);
            OutputValue.Text = $"{available:0.0} MWe";
            ReactorModeText.Text = P($"Reactor mode: {mode}", $"反應堆模式：{mode}");

            Color meterColor = !generating
                ? Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A)   // grey — idle
                : available > 800
                    ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)   // green — strong
                    : available > 300
                        ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)   // amber — moderate
                        : Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A);  // orange — low
            OutputBar.Foreground = new SolidColorBrush(meterColor);
            OutputValue.Foreground = new SolidColorBrush(meterColor);

            // Empty-state gating.
            if (!generating)
            {
                NeedPowerBar.Severity = meltdown ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Needs nuclear power", "需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — dispatch is halted.", "反應堆處於熔毀狀態 — 調度已停止。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up to dispatch.", "反應堆已急停。復原並啟動先可以調度。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown) to sell power to the grid.",
                            "啟動反應堆（脫離 MODE 5 冷停機）先可以向電網售電。");
                NeedPowerBar.IsOpen = true;
                SellButton.IsEnabled = false;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
                SellButton.IsEnabled = true;
            }

            // Market readouts.
            RevenueValue.Text = $"${_grid.TotalRevenueUsd:N2}";
            PriceValue.Text = $"${_grid.PriceUsdPerMWh:0.0}/MWh";
            DemandValue.Text = $"{_grid.DemandMW:0} MW";
            DispatchedValue.Text = $"{_grid.DispatchedMW:0.0} MW";

            double freq = _grid.FrequencyHz;
            string balance = !generating || _grid.DispatchedMW <= 0
                ? P("no supply", "無供電")
                : freq > 60.05
                    ? P("over-supply", "供過於求")
                    : freq < 59.95
                        ? P("under-supply", "供不應求")
                        : P("balanced", "平衡");
            FrequencyValue.Text = $"{freq:0.00} Hz · {balance}";

            Color freqColor = Math.Abs(freq - 60.0) < 0.1
                ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)
                : Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A);
            FrequencyValue.Foreground = new SolidColorBrush(freqColor);
        }
        catch { }
    }
}
