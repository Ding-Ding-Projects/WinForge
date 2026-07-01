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
/// 電弧爐煉鋼廠 · Electric Arc Furnace (EAF) steel mill — a HUGE intermittent reactor-powered load. A batch
/// process: charge scrap → strike the arc to melt (draws up to ~800 MW while the bath climbs toward the
/// ~1600 °C tapping temperature) → tap a heat of molten steel → repeat. Melt only progresses while the reactor
/// supplies enough MW; if power is short the melt stalls and the bath cools. Reads the live reactor snapshot
/// from <see cref="ReactorStatusApiService"/> every ~500 ms via an integer tick counter. When the reactor is not
/// generating a "furnace cold — needs nuclear power" empty-state shows and the bath falls. Tapped heats/tonnes
/// are sold into the shared reactor economy (⚡). Bilingual (粵語), anti-leak, never throws.
/// </summary>
public sealed partial class SteelMillModule : Page
{
    private readonly SteelMillService _mill = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick; // integer tick counter drives sim timing (not DateTime.Now)

    // --- Reactor-Bank economy: sell tapped steel for Watts (⚡) ---
    /// <summary>Watts (⚡) earned per whole tonne of tapped steel sold.</summary>
    private const double PricePerTonne = 4.0;
    /// <summary>Lifetime tonnes already deposited to the bank — prevents double counting.</summary>
    private double _tonnesDeposited;
    /// <summary>Watts credited to the mill this session (display only).</summary>
    private double _salesEarned;
    /// <summary>Tick of the last deposit so we bank at most ~once per 3 s, not every tick.</summary>
    private int _lastDepositTick;

    public SteelMillModule()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loc.I.LanguageChanged += OnLang;
        ReactorEconomyService.I.Changed += OnEconomyChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { ReactorStatusApiService.I.Start(); } catch { }
        _tick = 0;
        Render();
        UpdateStep();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Loc.I.LanguageChanged -= OnLang;
        ReactorEconomyService.I.Changed -= OnEconomyChanged;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private void OnEconomyChanged()
    {
        // The economy service raises Changed off arbitrary threads; marshal to the UI thread.
        try { DispatcherQueue?.TryEnqueue(UpdateEconomy); } catch { }
    }

    private void Render()
    {
        try
        {
            Header.Title = "Arc-Furnace Steel Mill · 電弧爐煉鋼廠";
            Header.Subtitle = P("A batch electric-arc furnace: charge scrap, melt it with a giant arc off the reactor, tap a heat of molten steel, repeat.",
                "分批電弧爐：裝入廢鋼，用反應堆供電嘅巨型電弧熔煉，出一爐鋼水，再重複。");
            HeaderBlurb.Text = P(
                "An electric-arc furnace (EAF) melts scrap steel with a giant electric arc — one of the most intermittent heavy loads on any grid, drawing up to ~800 MW straight off the reactor while a heat melts. Charge scrap and strike the arc: the bath climbs toward the ~1600 °C tapping temperature, but only while the reactor supplies enough MW — starve it and the melt stalls and the bath cools. When a heat is ready, tap it to pour molten steel, then charge the next.",
                "電弧爐（EAF）用巨型電弧熔煉廢鋼 — 係電網上最間歇嘅重負載之一，熔煉一爐時直接由反應堆抽高達 ~800 MW。裝入廢鋼並起弧：鋼水溫度朝 ~1600 °C 出鋼溫度上升，但淨係喺反應堆供夠 MW 嗰陣先得 — 缺電就會停頓，鋼水冷卻。爐爐煉好就出鋼倒出鋼水，然後再裝下一爐。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            FurnaceTitle.Text = P("Furnace", "電弧爐");
            PowerLabel.Text = P("Arc-power setpoint (% of full draw)", "電弧功率設定點（滿載百分比）");
            TelemetryTitle.Text = P("Furnace telemetry", "電弧爐遙測");

            TempCaption.Text = P("Furnace temperature", "爐溫");
            ProgressCaption.Text = P("Heat progress", "熔煉進度");
            DrawCaption.Text = P("Power drawn", "抽取功率");
            AmpsCaption.Text = P("Electrode current", "電極電流");
            HeatsCaption.Text = P("Heats tapped", "已出鋼爐數");
            TotalCaption.Text = P("Steel produced (lifetime)", "累計鋼產量（總計）");

            EconTitle.Text = P("Reactor Bank · steel sales", "反應堆銀行 · 鋼材銷售");
            SalesCaption.Text = P("Earned from sales (session)", "銷售收入（本次）");
            PriceCaption.Text = P("Sale price", "售價");

            ResetButton.Content = P("Reset", "重設");
            UpdateStep();
            UpdateEconomy();
        }
        catch { }
    }

    private void Power_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            _mill.SetPower(PowerSlider.Value / 100.0);
            UpdateStep();
        }
        catch { }
    }

    private void Charge_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_mill.Melting) _mill.Idle();
            else _mill.Charge();
            UpdateStep();
        }
        catch { }
    }

    private void Tap_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mill.Tap();
            SellSteel();
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _mill.Reset();
            PowerSlider.Value = 100;
            UpdateStep();
        }
        catch { }
    }

    private void OnTick(object? sender, object e)
    {
        _tick++;
        UpdateStep();
    }

    private void UpdateStep()
    {
        try
        {
            var snap = ReactorStatusApiService.I.LastSnapshot; // non-nullable value struct (defaults to Offline)

            double available = double.IsNaN(snap.ElectricMW) || snap.ElectricMW < 0 ? 0 : snap.ElectricMW;
            string mode = string.IsNullOrWhiteSpace(snap.Mode) ? "?" : snap.Mode;
            bool scrammed = snap.IsScrammed;
            bool meltdown = snap.IsMeltdown;
            bool coldMode = mode.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0
                            || mode.IndexOf("cold", StringComparison.OrdinalIgnoreCase) >= 0;
            bool generating = snap.IsGenerating && available > 1.0 && !scrammed && !meltdown && !coldMode;

            // Advance the mill simulation (uses the internal tick counter for ramps).
            _mill.Step(_tick, available, generating);

            // --- Live reactor output meter (MW, 0..1150) ---
            OutputBar.Value = Math.Clamp(available, 0, OutputBar.Maximum);
            OutputValue.Text = $"{available:0.0} MWe";
            ReactorModeText.Text = P($"Reactor mode: {mode}", $"反應堆模式：{mode}");

            Color meterColor = !generating
                ? Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A)      // grey — idle
                : available > 800
                    ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)  // green — strong
                    : available > 300
                        ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)  // amber
                        : Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A); // orange — low
            OutputBar.Foreground = new SolidColorBrush(meterColor);
            OutputValue.Foreground = new SolidColorBrush(meterColor);

            // --- Reactor empty-state gating ---
            if (!generating)
            {
                NeedPowerBar.Severity = meltdown ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Furnace cold — needs nuclear power", "電弧爐凍卻 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — the arc has dropped and the bath is cooling.",
                        "反應堆處於熔毀狀態 — 電弧已熄，鋼水正在冷卻。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up — no arc can strike without power.",
                            "反應堆已急停。復原並啟動佢 — 冇電就起唔到弧。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown). Without power the arc cannot melt scrap and the furnace cools.",
                            "啟動反應堆（脫離 MODE 5 冷停機）。冇電嘅話電弧熔唔到廢鋼，爐溫下降。");
                NeedPowerBar.IsOpen = true;
                ChargeButton.IsEnabled = false;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
                ChargeButton.IsEnabled = true;
            }

            ChargeButton.Content = _mill.Melting ? P("Idle", "停爐") : P("Charge & melt", "裝料熔煉");

            // --- Furnace temperature (coloured toward white-hot) ---
            double temp = _mill.BathTempC;
            TempBar.Value = Math.Clamp(temp, 0, TempBar.Maximum);
            TempValue.Text = $"{temp:0} °C";
            Color tempColor = TempHeatColor(temp);
            TempBar.Foreground = new SolidColorBrush(tempColor);
            TempValue.Foreground = new SolidColorBrush(tempColor);

            // --- Heat progress ---
            double pct = Math.Clamp(_mill.HeatProgress * 100.0, 0, 100);
            ProgressBarHeat.Value = pct;
            ProgressValue.Text = $"{pct:0}%";

            // --- Numeric readouts ---
            DrawValue.Text = $"{_mill.DrawnMW:0} MW";
            AmpsValue.Text = $"{_mill.ElectrodeCurrentKA:0} kA";
            HeatsValue.Text = $"{_mill.HeatsTapped:N0}";
            TotalValue.Text = $"{_mill.TonnesProduced:N0} t";

            // --- Electrode / power-factor note ---
            ElectrodeNote.Text = _mill.Powered
                ? P($"Graphite electrodes arcing · power factor ≈ {_mill.PowerFactor:0.00} · one heat ≈ {SteelMillService.HeatTonnes:0} t.",
                    $"石墨電極起弧中 · 功率因數 ≈ {_mill.PowerFactor:0.00} · 一爐 ≈ {SteelMillService.HeatTonnes:0} 噸。")
                : P($"Electrodes idle — no arc · one heat ≈ {SteelMillService.HeatTonnes:0} t at tapping temperature ({SteelMillService.TapTempC:0} °C).",
                    $"電極閒置 — 冇電弧 · 出鋼溫度（{SteelMillService.TapTempC:0} °C）時一爐 ≈ {SteelMillService.HeatTonnes:0} 噸。");

            // --- Furnace status line ---
            FurnaceStatus.Text = _mill.ReadyToTap
                ? P("Ready to tap — molten heat at tapping temperature.", "可以出鋼 — 鋼水已達出鋼溫度。")
                : !_mill.Melting
                    ? P("Idle — arc off, furnace cooling.", "停爐 — 冇電弧，爐溫下降。")
                    : _mill.Powered
                        ? P("Melting — the arc is charging into the scrap.", "熔煉中 — 電弧正熔化廢鋼。")
                        : P("Melt stalled — starved of reactor power.", "熔煉停頓 — 缺反應堆電力。");

            // --- Tap-ready banner + button state ---
            TapButton.IsEnabled = _mill.ReadyToTap;
            TapButton.Content = P("Tap", "出鋼");
            if (_mill.ReadyToTap)
            {
                TapBar.Severity = InfoBarSeverity.Success;
                TapBar.Title = P("Heat ready to tap", "一爐鋼可以出");
                TapBar.Message = P(
                    $"A ~{SteelMillService.HeatTonnes:0} t heat of molten steel is at tapping temperature. Tap it to pour and sell the steel, then charge the next heat.",
                    $"一爐約 {SteelMillService.HeatTonnes:0} 噸鋼水已達出鋼溫度。撳出鋼倒出並賣鋼，然後裝下一爐。");
                TapBar.IsOpen = true;
            }
            else
            {
                TapBar.IsOpen = false;
            }

            UpdateEconomy();
        }
        catch { }
    }

    /// <summary>Colour a temperature from dull red → orange → yellow → white-hot as it climbs toward tapping.</summary>
    private static Color TempHeatColor(double tempC)
    {
        try
        {
            if (tempC < 200) return Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A);      // cold — grey
            if (tempC < 700) return Color.FromArgb(0xFF, 0xC8, 0x3A, 0x2A);      // dull red
            if (tempC < 1100) return Color.FromArgb(0xFF, 0xE8, 0x6C, 0x1F);     // orange
            if (tempC < 1450) return Color.FromArgb(0xFF, 0xF4, 0xC4, 0x2A);     // yellow
            return Color.FromArgb(0xFF, 0xFF, 0xF3, 0xD6);                        // white-hot
        }
        catch { return Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A); }
    }

    /// <summary>
    /// Sell newly-tapped steel to the Reactor Bank. Banks in whole-tonne increments, throttled to at most once
    /// per ~3 s (6 ticks). <c>_tonnesDeposited</c> tracks the lifetime total already sold so re-selling never
    /// double-counts; called both on Tap and each tick to drain any pending remainder.
    /// </summary>
    private void SellSteel()
    {
        try
        {
            double produced = _mill.TonnesProduced;
            // Reset guard: if the mill was reset (lifetime total dropped), re-baseline the deposited counter.
            if (produced < _tonnesDeposited) _tonnesDeposited = produced;

            double pending = produced - _tonnesDeposited;
            bool throttleOk = (_tick - _lastDepositTick) >= 6; // ~3 s at 500 ms ticks
            if (pending >= 1.0 && throttleOk)
            {
                double tonnesToSell = Math.Floor(pending); // whole tonnes only; keep the fractional remainder
                double watts = tonnesToSell * PricePerTonne;
                ReactorEconomyService.I.Earn(watts, P("Steel sales", "鋼材銷售"));
                _tonnesDeposited += tonnesToSell;
                _salesEarned += watts;
                _lastDepositTick = _tick;
            }
        }
        catch { }
    }

    private void UpdateEconomy()
    {
        try
        {
            // Drain any pending tonnes on the regular tick too (Tap deposits immediately, but throttling may defer).
            SellSteel();
            EconBalance.Text = $"{ReactorEconomyService.I.Balance:N1} {ReactorEconomyService.Symbol}";
            SalesValue.Text = $"+{_salesEarned:N1} {ReactorEconomyService.Symbol}";
            PriceValue.Text = P($"{PricePerTonne:0} ⚡ / tonne", $"{PricePerTonne:0} ⚡ / 噸");
        }
        catch { }
    }
}
