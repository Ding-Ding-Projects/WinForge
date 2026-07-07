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
/// 運算礦場 · Compute Mine — a deliberately HEAVY, reactor-powered compute/crypto load. The whole point:
/// this rig will NOT run without the nuclear reactor. It reads live reactor output via
/// <see cref="ReactorStatusApiService"/>; the operator sets a power draw (0..600 MW) and arms mining.
/// Hashrate, earnings and efficiency only accrue while the reactor is actually generating — otherwise a
/// prominent "mining halted — needs nuclear power" empty-state shows and everything drops to zero.
/// Pure managed, deterministic (integer tick counter), never throws.
/// </summary>
public sealed partial class ComputeMineModule : Page
{
    private readonly ComputeMineService _mine = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastTick = DateTime.UtcNow;
    private bool _suppress;

    public ComputeMineModule()
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
            Header.Title = "Compute Mine · 運算礦場";
            HeaderBlurb.Text = P(
                "A reactor-powered compute mine. This is a heavy load by design — it draws hundreds of megawatts and will not run a single hash without the nuclear reactor generating. Set a power draw, arm the rigs, and watch hashrate and earnings climb.",
                "核電運算礦場。呢個係刻意設計嘅重負載 — 佢要食幾百兆瓦電，冇核反應堆發電就一個 hash 都行唔到。設定功率、開機開礦，睇住算力同收入慢慢上升。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            MineTitle.Text = P("Mining rigs", "礦機");
            MineHint.Text = P("These rigs run only on nuclear power. If the reactor stops generating, mining halts instantly — no hashrate, no earnings.",
                "呢啲礦機淨係食核電。反應堆一停止發電，開礦即刻停 — 冇算力、冇收入。");
            DrawLabel.Text = P("Power draw (MW)", "功率消耗（MW）");

            StatsTitle.Text = P("Mine status", "礦場狀態");
            HashCaption.Text = P("Hashrate", "算力");
            EarnedCaption.Text = P("Total earned", "總收入");
            PowerCaption.Text = P("Power drawn", "耗電量");
            EffCaption.Text = P("Efficiency", "能效");
            PriceCaption.Text = P("Yield & difficulty", "收益率同難度");
            SellButton.Content = P("Sell", "賣出");
            ResetButton.Content = P("Reset", "重設");

            UpdateMineButton();
            UpdateStep();
        }
        catch { }
    }

    private void UpdateMineButton()
    {
        MineButton.Content = _mine.Mining ? P("Stop mining", "停止開礦") : P("Start mining", "開始開礦");
    }

    private void DrawSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        UpdateStep();
    }

    private void Mine_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_mine.Mining) _mine.StopMining();
            else _mine.StartMining();
            UpdateMineButton();
            UpdateStep();
        }
        catch { }
    }

    private void Sell_Click(object sender, RoutedEventArgs e)
    {
        try { _mine.Sell(); UpdateStep(); } catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mine.Reset();
            _suppress = true;
            DrawSlider.Value = 0;
            _suppress = false;
            UpdateMineButton();
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

            double setpoint = double.IsNaN(DrawSlider.Value) ? 0 : DrawSlider.Value;
            _mine.Tick(dt, setpoint, available, generating);

            // Live reactor output meter + colour.
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
                NeedPowerBar.Title = P("Mining halted — needs nuclear power", "開礦已停 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — the mine is powered down.", "反應堆處於熔毀狀態 — 礦場已斷電。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up before the rigs can mine.", "反應堆已急停。復原並啟動反應堆，礦機先可以開工。")
                        : P("These rigs will not run without the reactor. Bring it out of MODE 5 cold shutdown and start it up to begin mining.",
                            "冇反應堆呢啲礦機唔會行。脫離 MODE 5 冷停機並啟動反應堆先可以開礦。");
                NeedPowerBar.IsOpen = true;
                MineButton.IsEnabled = false;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
                MineButton.IsEnabled = true;
            }

            // Mine readouts.
            HashValue.Text = $"{_mine.HashrateThs:0.0} TH/s";
            EarnedValue.Text = $"${_mine.TotalEarnedUsd:N2}";
            PowerValue.Text = $"{_mine.DrawnMW:0.0} MW";

            double jth = _mine.JoulesPerTh;
            EffValue.Text = jth <= 0.0001 ? P("idle", "閒置") : $"{jth / 1_000_000_000.0:0.00} GJ/TH";

            PriceValue.Text = $"${_mine.PriceUsdPerThHour:0.000}/TH·h · " +
                              P($"difficulty {_mine.Difficulty:0.00}", $"難度 {_mine.Difficulty:0.00}");

            Color hashColor = _mine.Running
                ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)
                : Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A);
            HashValue.Foreground = new SolidColorBrush(hashColor);

            UpdateTurboNote();
        }
        catch { }
    }

    /// <summary>
    /// Reflects the spend-gated Reactor-Bank turbo perk: a green "active" badge when owned (rigs get a
    /// permanent +25% hashrate in the service), otherwise a hint to unlock it. Refreshed each tick since the
    /// perk can be bought while this page is open.
    /// </summary>
    private void UpdateTurboNote()
    {
        try
        {
            if (_mine.TurboActive)
            {
                TurboNote.Text = P("⚡ Turbo rigs active (+25%)", "⚡ 渦輪機組運作中（+25%）");
                TurboNote.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A));
            }
            else
            {
                TurboNote.Text = P("⚡ Unlock the turbo-rigs perk in the Reactor Bank for a permanent +25% hashrate.",
                                   "⚡ 喺反應堆銀行解鎖渦輪機組特權，永久 +25% 算力。");
                TurboNote.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A));
            }
        }
        catch { }
    }
}
