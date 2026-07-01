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
/// 電熱水泥迴轉窯 · Electric Cement Kiln — an ⚛️ reactor-powered rotary kiln that calcines limestone into
/// clinker → cement using electric heat instead of a fossil (coal/gas/petcoke) burner. The operator sets a power
/// draw (0..400 MW); the kiln shell temperature climbs toward the ~1450 °C calcination temperature while
/// adequately powered. Clinker forms only above a threshold temperature, and tonnes of cement per hour scale with
/// the delivered heat above that threshold — starve the kiln of power and it cools and production stalls. Reads
/// the live reactor snapshot from <see cref="ReactorStatusApiService"/> every ~500 ms via an integer tick
/// counter. When the reactor is not generating a "kiln cooling — needs nuclear power" empty-state shows. Cement
/// is sold into the shared reactor economy (⚡). Bilingual (粵語), anti-leak, never throws.
/// </summary>
public sealed partial class CementKilnModule : Page
{
    private readonly CementKilnService _kiln = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick; // integer tick counter drives sim timing (not DateTime.Now)

    // --- Reactor-Bank economy: sell cement for Watts (⚡) ---
    /// <summary>Watts (⚡) earned per whole tonne of cement sold.</summary>
    private const double PricePerTonne = 2.0;
    /// <summary>Lifetime tonnes already deposited to the bank — prevents double counting.</summary>
    private double _tonnesDeposited;
    /// <summary>Watts credited to the kiln this session (display only).</summary>
    private double _salesEarned;
    /// <summary>Tick of the last deposit so we bank at most ~once per 3 s, not every tick.</summary>
    private int _lastDepositTick;

    public CementKilnModule()
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
        try { _kiln.SetPowerFraction(PowerSlider.Value / CementKilnService.MaxDrawMW); } catch { }
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
            Header.Title = "Electric Cement Kiln · 電熱水泥迴轉窯";
            Header.Subtitle = P("A reactor-powered rotary kiln: calcine limestone into clinker with electric heat instead of a fossil burner, then grind it into cement.",
                "核電迴轉窯：用電熱（唔燒化石燃料）將石灰石煅燒成熟料，再磨成水泥。");
            HeaderBlurb.Text = P(
                "A cement rotary kiln calcines limestone (CaCO₃) into clinker at around 1450 °C — normally fired by burning coal, gas or petcoke. Here the heat comes straight off the ⚛️ reactor. Set a power draw and fire the kiln: the shell temperature climbs toward the calcination temperature, but clinker only forms above ~1300 °C, and tonnes of cement per hour scale with the delivered heat above that threshold. Drop the power and the kiln cools and production stalls. Every tonne made this way avoids the CO₂ a fossil-fired kiln would have emitted.",
                "水泥迴轉窯喺約 1450 °C 將石灰石（CaCO₃）煅燒成熟料 — 平時要燒煤、燒天然氣或石油焦。呢度啲熱直接嚟自 ⚛️ 反應堆。設定功率再點火：窯殼溫度朝煅燒溫度上升，但要過 ~1300 °C 先會結出熟料，而每個鐘水泥噸數同門檻以上嘅供熱成正比。功率一跌，窯就冷卻、產量停頓。用呢個方法做嘅每一噸，都慳返化石窯本來會排嘅 CO₂。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            KilnTitle.Text = P("Rotary kiln", "迴轉窯");
            PowerLabel.Text = P("Power-draw setpoint", "功率設定點");
            TelemetryTitle.Text = P("Kiln telemetry", "窯遙測");

            TempCaption.Text = P("Kiln temperature", "窯溫");
            DrawCaption.Text = P("Power drawn", "抽取功率");
            RateCaption.Text = P("Production rate", "生產速率");
            TotalCaption.Text = P("Cement produced (lifetime)", "累計水泥產量（總計）");
            Co2Caption.Text = P("CO₂ avoided vs fossil kiln", "相比化石窯慳咗嘅 CO₂");

            EconTitle.Text = P("Reactor Bank · cement sales", "反應堆銀行 · 水泥銷售");
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
            _kiln.SetPowerFraction(PowerSlider.Value / CementKilnService.MaxDrawMW);
            UpdateStep();
        }
        catch { }
    }

    private void Fire_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_kiln.Firing) _kiln.Idle();
            else _kiln.Fire();
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _kiln.Reset();
            PowerSlider.Value = 240;
            _kiln.SetPowerFraction(PowerSlider.Value / CementKilnService.MaxDrawMW);
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

            // Advance the kiln simulation (uses the internal tick counter for ramps).
            _kiln.Step(_tick, available, generating);

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
                NeedPowerBar.Title = P("Kiln cooling — needs nuclear power", "窯冷卻中 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — the heat has dropped and the kiln is cooling.",
                        "反應堆處於熔毀狀態 — 供熱已停，窯正在冷卻。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up — no clinker can form without heat.",
                            "反應堆已急停。復原並啟動佢 — 冇熱就結唔到熟料。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown). Without power the kiln cannot calcine limestone and it cools.",
                            "啟動反應堆（脫離 MODE 5 冷停機）。冇電嘅話窯煅燒唔到石灰石，會冷卻。");
                NeedPowerBar.IsOpen = true;
                FireButton.IsEnabled = false;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
                FireButton.IsEnabled = true;
            }

            FireButton.Content = _kiln.Firing ? P("Idle", "停窯") : P("Fire kiln", "點火");

            // --- Kiln temperature (coloured toward white-hot) ---
            double temp = _kiln.KilnTempC;
            TempBar.Value = Math.Clamp(temp, 0, TempBar.Maximum);
            TempValue.Text = $"{temp:0} °C";
            Color tempColor = TempHeatColor(temp);
            TempBar.Foreground = new SolidColorBrush(tempColor);
            TempValue.Foreground = new SolidColorBrush(tempColor);

            // --- Numeric readouts ---
            DrawValue.Text = $"{_kiln.DrawnMW:0} MW";
            RateValue.Text = $"{_kiln.TonnesPerHour:0} t/h";
            TotalValue.Text = $"{_kiln.TotalTonnes:N0} t";
            Co2Value.Text = $"{_kiln.Co2AvoidedTonnes:N0} t";

            // --- Set-point echo ---
            PowerValue.Text = $"{_kiln.SetpointMW:0} MW";

            // --- Kiln note ---
            KilnNote.Text = _kiln.Powered
                ? P($"Electric heating elements glowing · clinkering above {CementKilnService.ClinkerThresholdC:0} °C · saving ≈ {CementKilnService.FossilCo2PerTonne:0.00} t CO₂ per tonne of cement.",
                    $"電熱元件發熱中 · 超過 {CementKilnService.ClinkerThresholdC:0} °C 開始結熟料 · 每噸水泥慳約 {CementKilnService.FossilCo2PerTonne:0.00} 噸 CO₂。")
                : P($"Kiln idle — no heat · calcination temperature is {CementKilnService.CalcinationTempC:0} °C, clinker forms above {CementKilnService.ClinkerThresholdC:0} °C.",
                    $"窯閒置 — 冇供熱 · 煅燒溫度 {CementKilnService.CalcinationTempC:0} °C，超過 {CementKilnService.ClinkerThresholdC:0} °C 先結熟料。");

            // --- Kiln status line ---
            KilnStatus.Text = !_kiln.Firing
                ? P("Idle — heat off, kiln cooling.", "停窯 — 冇供熱，窯冷卻中。")
                : !_kiln.Powered
                    ? P("Heating stalled — starved of reactor power.", "加熱停頓 — 缺反應堆電力。")
                    : _kiln.Clinkering
                        ? P("Calcining — clinker forming, cement flowing.", "煅燒中 — 熟料生成，水泥出料。")
                        : P("Heating up — climbing toward clinkering temperature.", "升溫中 — 朝結熟料溫度上升。");

            UpdateEconomy();
        }
        catch { }
    }

    /// <summary>Colour a temperature from dull red → orange → yellow → white-hot as it climbs toward calcination.</summary>
    private static Color TempHeatColor(double tempC)
    {
        try
        {
            if (tempC < 200) return Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A);      // cold — grey
            if (tempC < 700) return Color.FromArgb(0xFF, 0xC8, 0x3A, 0x2A);      // dull red
            if (tempC < 1100) return Color.FromArgb(0xFF, 0xE8, 0x6C, 0x1F);     // orange
            if (tempC < 1400) return Color.FromArgb(0xFF, 0xF4, 0xC4, 0x2A);     // yellow
            return Color.FromArgb(0xFF, 0xFF, 0xF3, 0xD6);                        // white-hot
        }
        catch { return Color.FromArgb(0xFF, 0x8A, 0x8A, 0x8A); }
    }

    /// <summary>
    /// Sell newly-made cement to the Reactor Bank. Banks in whole-tonne increments, throttled to at most once per
    /// ~3 s (6 ticks). <c>_tonnesDeposited</c> tracks the lifetime total already sold so it never double-counts;
    /// called each tick to drain any pending remainder. Never throws.
    /// </summary>
    private void SellCement()
    {
        try
        {
            double produced = _kiln.TotalTonnes;
            // Reset guard: if the kiln was reset (lifetime total dropped), re-baseline the deposited counter.
            if (produced < _tonnesDeposited) _tonnesDeposited = produced;

            double pending = produced - _tonnesDeposited;
            bool throttleOk = (_tick - _lastDepositTick) >= 6; // ~3 s at 500 ms ticks
            if (pending >= 1.0 && throttleOk)
            {
                double tonnesToSell = Math.Floor(pending); // whole tonnes only; keep the fractional remainder
                double watts = tonnesToSell * PricePerTonne;
                ReactorEconomyService.I.Earn(watts, P("Cement sales", "水泥銷售"));
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
            // Drain any pending tonnes on the regular tick.
            SellCement();
            EconBalance.Text = $"{ReactorEconomyService.I.Balance:N1} {ReactorEconomyService.Symbol}";
            SalesValue.Text = $"+{_salesEarned:N1} {ReactorEconomyService.Symbol}";
            PriceValue.Text = P($"{PricePerTonne:0} ⚡ / tonne", $"{PricePerTonne:0} ⚡ / 噸");
        }
        catch { }
    }
}
