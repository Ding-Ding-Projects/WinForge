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
/// 鋁冶煉廠 · Aluminium Smelter — a HEAVY reactor-powered load. A Hall-Héroult pot-line that draws up
/// to ~700 MW continuously and makes aluminium in proportion to the power actually drawn (Faraday). The
/// pots must stay molten: they heat while adequately powered and cool otherwise — if the bath drops
/// past its freeze point the pots "freeze" and production collapses (recoverable by restoring power).
/// Reads the live reactor via <see cref="ReactorStatusApiService"/>; when the reactor is not generating
/// a prominent "needs nuclear power" empty-state shows and the pots start cooling. All simulation ramps
/// use an internal tick counter (never wall-clock). Pure managed, never throws.
/// </summary>
public sealed partial class SmelterModule : Page
{
    private readonly SmelterService _smelter = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick;
    private bool _suppress;

    // --- Reactor-Bank economy: sell aluminium output for Watts (⚡) ---
    private const string PreheatPerkId = "perk.smelter.preheat";
    /// <summary>Watts (⚡) earned per whole tonne of aluminium sold.</summary>
    private const double PricePerTonne = 6.0;
    /// <summary>Lifetime tonnes we've already deposited to the bank — prevents double counting.</summary>
    private double _tonnesDeposited;
    /// <summary>Watts credited to the smelter this session (display only).</summary>
    private double _salesEarned;
    /// <summary>Tick of the last deposit so we bank at most ~once per 3 s, not every tick.</summary>
    private int _lastDepositTick;

    public SmelterModule()
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
            Header.Title = "Aluminium Smelter · 鋁冶煉廠";
            HeaderBlurb.Text = P(
                "A Hall-Héroult aluminium pot-line — one of the hungriest industrial loads there is, drawing up to ~700 MW straight off the reactor. The pots must stay molten near 960 °C: keep them powered and aluminium pours; let the power fail and the bath cools and can freeze solid.",
                "霍爾—埃魯鋁電解槽線 — 全世界最食電嘅工業負載之一，直接由反應堆抽高達 ~700 MW。電解槽要維持喺 ~960 °C 熔融狀態：夠電就出鋁，冇電就會冷卻，甚至凍結固化。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            LineTitle.Text = P("Pot-line", "電解槽線");
            LoadLabel.Text = P("Line load setpoint (% of full draw)", "線負載設定點（滿載百分比）");
            TelemetryTitle.Text = P("Pot-line telemetry", "電解槽線遙測");

            DrawCaption.Text = P("Power drawn", "抽取功率");
            AmpsCaption.Text = P("Line amperage", "線電流");
            RateCaption.Text = P("Production rate", "生產率");
            TotalCaption.Text = P("Total produced (lifetime)", "累計產量（總計）");

            EconTitle.Text = P("Reactor Bank · aluminium sales", "反應堆銀行 · 鋁材銷售");
            SalesCaption.Text = P("Earned from sales (session)", "銷售收入（本次）");
            PriceCaption.Text = P("Sale price", "售價");

            UpdateRunButton();
            ResetButton.Content = P("Reset", "重設");

            UpdateStep();
            UpdateEconomy();
        }
        catch { }
    }

    private void UpdateRunButton()
    {
        try
        {
            RunButton.Content = _smelter.LineRunning ? P("Bank the line", "停爐") : P("Run the line", "開爐");
        }
        catch { }
    }

    private void Load_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            _smelter.SetLoad(LoadSlider.Value / 100.0);
            UpdateStep();
        }
        catch { }
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_smelter.LineRunning) _smelter.Bank();
            else _smelter.Run();
            UpdateRunButton();
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _smelter.Reset();
            _suppress = true;
            LoadSlider.Value = 100;
            _suppress = false;
            UpdateRunButton();
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

            // --- Reactor-Bank perk: pre-heaters make the pots freeze-resistant (spend-gated) ---
            bool preheat = false;
            try { preheat = ReactorEconomyService.I.IsUnlocked(PreheatPerkId); } catch { }
            _smelter.PreheatersActive = preheat;

            // Advance the smelter simulation (uses the internal tick counter for ramps).
            _smelter.Step(_tick, available, generating);

            // --- EARN: sell newly-produced aluminium to the Reactor Bank for Watts (⚡). ---
            // Accumulate produced tonnes and deposit in whole-tonne increments, at most ~once per 3 s,
            // tracking the deposited total so we never double-count.
            SellAluminium();

            // --- Live reactor output meter ---
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
                NeedPowerBar.Title = P("Pot-line cooling — needs nuclear power", "電解槽線冷卻中 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — the pot-line has lost power and is cooling toward a freeze.",
                        "反應堆處於熔毀狀態 — 電解槽線已失電，正朝凍結方向冷卻。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up before the pots freeze.",
                            "反應堆已急停。喺電解槽凍結之前復原並啟動佢。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown). Without power the bath cools and the pots will freeze.",
                            "啟動反應堆（脫離 MODE 5 冷停機）。冇電嘅話電解質會冷卻，電解槽會凍結。");
                NeedPowerBar.IsOpen = true;
                RunButton.IsEnabled = false;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
                RunButton.IsEnabled = true;
            }

            // --- Pot temperature (coloured) ---
            double temp = _smelter.PotTempC;
            PotTempBar.Value = Math.Clamp(temp, 0, PotTempBar.Maximum);
            PotTempCaption.Text = P("Pot temperature", "電解槽溫度");
            PotTempValue.Text = $"{temp:0} °C";

            Color tempColor = _smelter.Frozen
                ? Color.FromArgb(0xFF, 0x4F, 0x9B, 0xFF)          // blue — frozen
                : temp >= SmelterService.OperatingTempC - 20
                    ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)      // green — molten & at temp
                    : temp >= SmelterService.FreezeTempC + 40
                        ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)  // amber — warm but below target
                        : Color.FromArgb(0xFF, 0xE0, 0x4A, 0x3A); // red — near/at freeze
            PotTempBar.Foreground = new SolidColorBrush(tempColor);
            PotTempValue.Foreground = new SolidColorBrush(tempColor);

            // --- Numeric readouts ---
            DrawValue.Text = $"{_smelter.DrawnMW:0} MW";
            AmpsValue.Text = $"{_smelter.LineCurrentKA:0} kA";
            RateValue.Text = $"{_smelter.TonnesPerDay:0.0} t/day";
            TotalValue.Text = $"{_smelter.TonnesProduced:0.000} t";

            // --- Line status line ---
            string status = _smelter.Frozen
                ? P("FROZEN — bath solidified", "已凍結 — 電解質固化")
                : !_smelter.LineRunning
                    ? P("Banked (idle)", "已停爐（閒置）")
                    : _smelter.Powered
                        ? P("Running — pouring aluminium", "運行中 — 出鋁")
                        : P("Running but starved of power", "運行中但缺電");
            LineStatus.Text = status;

            // --- Freeze warning (red) ---
            if (_smelter.Frozen)
            {
                FreezeBar.Title = P("Pots frozen", "電解槽已凍結");
                FreezeBar.Message = generating
                    ? P("The cryolite bath solidified. Power restored — the pots are slowly re-melting; production resumes once molten.",
                        "冰晶石電解質已固化。電力已恢復 — 電解槽正緩慢重新熔化，熔融後恢復生產。")
                    : P("The cryolite bath solidified and production has collapsed. Restore reactor power to slowly re-melt the pots.",
                        "冰晶石電解質已固化，生產已崩潰。恢復反應堆電力先可以慢慢重新熔化電解槽。");
                FreezeBar.IsOpen = true;
            }
            else
            {
                FreezeBar.IsOpen = false;
            }

            UpdateEconomy();
        }
        catch { }
    }

    /// <summary>
    /// Sell newly-produced aluminium to the Reactor Bank. We bank in whole-tonne increments and throttle
    /// to at most once per ~3 s (6 ticks) so we don't call <see cref="ReactorEconomyService.Earn"/> every
    /// tick. <c>_tonnesDeposited</c> tracks the lifetime total already sold, so re-selling never double-counts.
    /// </summary>
    private void SellAluminium()
    {
        try
        {
            double produced = _smelter.TonnesProduced;
            // Reset guard: if the line was reset (lifetime total dropped), re-baseline the deposited counter.
            if (produced < _tonnesDeposited) _tonnesDeposited = produced;

            double pending = produced - _tonnesDeposited;
            bool throttleOk = (_tick - _lastDepositTick) >= 6; // ~3 s at 500 ms ticks
            // Deposit once we have at least a whole tonne pending AND the throttle window has elapsed.
            if (pending >= 1.0 && throttleOk)
            {
                double tonnesToSell = Math.Floor(pending); // whole tonnes only; keep the fractional remainder
                double watts = tonnesToSell * PricePerTonne;
                ReactorEconomyService.I.Earn(watts, P("Aluminium sales", "鋁材銷售"));
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
            EconBalance.Text = $"{ReactorEconomyService.I.Balance:N1} {ReactorEconomyService.Symbol}";
            SalesValue.Text = $"+{_salesEarned:N1} {ReactorEconomyService.Symbol}";
            PriceValue.Text = P($"{PricePerTonne:0} ⚡ / tonne", $"{PricePerTonne:0} ⚡ / 噸");

            bool owned = false;
            try { owned = ReactorEconomyService.I.IsUnlocked(PreheatPerkId); } catch { }
            if (owned)
            {
                PreheatBar.Severity = InfoBarSeverity.Success;
                PreheatBar.Title = P("⚡ Pre-heaters active (freeze-proof)", "⚡ 預熱器已啟動（防凍結）");
                PreheatBar.Message = P(
                    "Reactor-Bank pre-heaters keep the pot bath warm when power runs short — the pots resist freezing within reason.",
                    "反應堆銀行預熱器喺缺電時保持電解槽溫暖 — 電解槽喺合理範圍內抗凍結。");
            }
            else
            {
                PreheatBar.Severity = InfoBarSeverity.Informational;
                PreheatBar.Title = P("Pre-heaters available in the Reactor Bank", "反應堆銀行有預熱器可解鎖");
                PreheatBar.Message = P(
                    "Unlock the ⚡ pre-heater perk in the Reactor Bank to make the pots freeze-proof when power is short.",
                    "喺反應堆銀行解鎖 ⚡ 預熱器，令電解槽喺缺電時防凍結。");
            }
            PreheatBar.IsOpen = true;
        }
        catch { }
    }
}
