using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 核電合成氨（肥料）廠 · Ammonia / Fertilizer Plant — an ⚛️ reactor-powered green-ammonia plant. Electrolyzers
/// split water into H₂, an air-separation unit supplies N₂, and the Haber-Bosch synthesis loop
/// (N₂ + 3H₂ → 2NH₃, ~450 °C, ~200 bar) converts them into ammonia — the feedstock of nitrogen fertilizer. The
/// operator sets a power draw (0..350 MW); the synthesis-loop pressure climbs toward ~200 bar while adequately
/// powered. Ammonia forms only above a threshold pressure, and tonnes of NH₃ per hour scale with the loop
/// pressure above that threshold — starve the plant of power and the loop depressurises and production stalls.
/// Reads the live reactor snapshot from <see cref="ReactorStatusApiService"/> every ~500 ms via an integer tick
/// counter. When the reactor is not generating a "loop depressurising — needs nuclear power" empty-state shows.
/// Ammonia is sold into the shared reactor economy (⚡). Bilingual (粵語), anti-leak, never throws.
/// </summary>
public sealed partial class AmmoniaPlantModule : Page
{
    private readonly AmmoniaPlantService _plant = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick; // integer tick counter drives sim timing (not DateTime.Now)

    // --- Reactor-Bank economy: sell ammonia for Watts (⚡) ---
    /// <summary>Watts (⚡) earned per whole tonne of ammonia sold (a high-value chemical vs bulk cement).</summary>
    private const double PricePerTonne = 9.0;
    /// <summary>Lifetime tonnes already deposited to the bank — prevents double counting.</summary>
    private double _tonnesDeposited;
    /// <summary>Watts credited to the plant this session (display only).</summary>
    private double _salesEarned;
    /// <summary>Tick of the last deposit so we bank at most ~once per 3 s, not every tick.</summary>
    private int _lastDepositTick;

    public AmmoniaPlantModule()
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
        try { _plant.SetPowerFraction(PowerSlider.Value / AmmoniaPlantService.MaxDrawMW); } catch { }
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
            Header.Title = P("Ammonia / Fertilizer Plant", "核電合成氨（肥料）廠");
            Header.Subtitle = P("A reactor-powered Haber-Bosch plant: electrolytic H₂ + air-separated N₂ become green ammonia — the feedstock of nitrogen fertilizer.",
                "核電哈柏法工廠：電解氫加空分氮，合成綠氨 — 氮肥嘅原料。");
            HeaderBlurb.Text = P(
                "Ammonia (NH₃) feeds roughly half the world — nearly every nitrogen fertilizer starts as it — and it is normally made from natural gas, emitting about 1.9 tonnes of CO₂ per tonne. Here the whole chain runs off the ⚛️ reactor: electrolyzers split water into hydrogen, an air-separation unit supplies nitrogen, and the Haber-Bosch synthesis loop (N₂ + 3H₂ → 2NH₃, iron catalyst, ~450 °C) reacts them at high pressure. Set a power draw and start the plant: the loop pressure climbs toward 200 bar, but ammonia only forms above ~150 bar, and tonnes per hour scale with the pressure above that threshold. Drop the power and the loop depressurises and production stalls.",
                "氨（NH₃）養活全球一半人口 — 幾乎所有氮肥都由佢開始 — 平時用天然氣製造，每噸排大約 1.9 噸 CO₂。呢度成條鏈都由 ⚛️ 反應堆供電：電解槽將水裂解成氫，空分裝置供應氮，哈柏法合成迴路（N₂ + 3H₂ → 2NH₃，鐵催化劑，~450 °C）喺高壓下反應。設定功率再啟動工廠：迴路壓力朝 200 bar 上升，但要過 ~150 bar 先會合成氨，而每個鐘產量同門檻以上嘅壓力成正比。功率一跌，迴路就洩壓、產量停頓。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            PlantTitle.Text = P("Haber-Bosch synthesis plant", "哈柏法合成廠");
            PowerLabel.Text = P("Power-draw setpoint", "功率設定點");
            TelemetryTitle.Text = P("Plant telemetry", "工廠遙測");

            PressureCaption.Text = P("Synthesis-loop pressure", "合成迴路壓力");
            DrawCaption.Text = P("Power drawn", "抽取功率");
            H2Caption.Text = P("Electrolyzer H₂ output", "電解氫產量");
            RateCaption.Text = P("Production rate", "生產速率");
            TotalCaption.Text = P("Ammonia produced (lifetime)", "累計氨產量（總計）");
            Co2Caption.Text = P("CO₂ avoided vs grey ammonia", "相比灰氨慳咗嘅 CO₂");

            EconTitle.Text = P("Reactor Bank · ammonia sales", "反應堆銀行 · 氨銷售");
            SalesCaption.Text = P("Earned from sales (session)", "銷售收入（本次）");
            PriceCaption.Text = P("Sale price", "售價");

            ResetButton.Content = P("Reset", "重設");
            AutomationProperties.SetName(PowerSlider, PowerLabel.Text);
            AutomationProperties.SetHelpText(PowerSlider, P(
                $"Choose 0 to {AmmoniaPlantService.MaxDrawMW:0} MW. Steady synthesis needs about {Math.Ceiling(AmmoniaPlantService.MinimumSynthesisDrawMW):0} MW or more.",
                $"揀 0 至 {AmmoniaPlantService.MaxDrawMW:0} MW；穩定合成大約要 {Math.Ceiling(AmmoniaPlantService.MinimumSynthesisDrawMW):0} MW 或以上。"));
            AutomationProperties.SetName(OutputBar, ReactorTitle.Text);
            AutomationProperties.SetName(PressureBar, PressureCaption.Text);
            AutomationProperties.SetName(ResetButton, ResetButton.Content?.ToString() ?? "Reset");
            UpdateStep();
            UpdateEconomy();
        }
        catch { }
    }

    private void Power_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            _plant.SetPowerFraction(PowerSlider.Value / AmmoniaPlantService.MaxDrawMW);
            UpdateStep();
        }
        catch { }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_plant.Running) _plant.Stop();
            else _plant.Start();
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _plant.Reset();
            PowerSlider.Value = AmmoniaPlantService.DefaultDrawMW;
            _plant.SetPowerFraction(PowerSlider.Value / AmmoniaPlantService.MaxDrawMW);
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

            double available = !double.IsFinite(snap.ElectricMW) || snap.ElectricMW < 0 ? 0 : snap.ElectricMW;
            string mode = string.IsNullOrWhiteSpace(snap.Mode) ? "?" : snap.Mode;
            bool scrammed = snap.IsScrammed;
            bool meltdown = snap.IsMeltdown;
            bool coldMode = mode.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0
                            || mode.IndexOf("cold", StringComparison.OrdinalIgnoreCase) >= 0;
            bool generating = snap.IsGenerating && available > 1.0 && !scrammed && !meltdown && !coldMode;

            // Advance the plant simulation (uses the internal tick counter for ramps).
            _plant.Step(_tick, available, generating);

            // --- Live reactor output meter (MW, 0..1150) ---
            OutputBar.Value = Math.Clamp(available, 0, OutputBar.Maximum);
            OutputValue.Text = $"{available:0.0} MWe";
            ReactorModeText.Text = P($"Reactor mode: {mode}", $"反應堆模式：{mode}");

            Brush meterBrush = !generating
                ? ThemeBrush("TextFillColorSecondaryBrush")
                : available > 800
                    ? ThemeBrush("SystemFillColorSuccessBrush")
                    : available > 300
                        ? ThemeBrush("SystemFillColorCautionBrush")
                        : ThemeBrush("SystemFillColorAttentionBrush");
            OutputBar.Foreground = meterBrush;
            OutputValue.Foreground = meterBrush;

            // --- Reactor empty-state gating ---
            if (!generating)
            {
                NeedPowerBar.Severity = meltdown ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Loop depressurising — needs nuclear power", "迴路洩壓中 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — the compressors have stopped and the synthesis loop is bleeding down.",
                        "反應堆處於熔毀狀態 — 壓縮機已停，合成迴路洩緊壓。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up — no ammonia can form without power.",
                            "反應堆已急停。復原並啟動佢 — 冇電就合成唔到氨。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown). Without power the electrolyzers stop and the loop cannot hold synthesis pressure.",
                            "啟動反應堆（脫離 MODE 5 冷停機）。冇電嘅話電解槽會停，迴路亦頂唔住合成壓力。");
                NeedPowerBar.IsOpen = true;
                StartButton.IsEnabled = false;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
                StartButton.IsEnabled = true;
            }

            StartButton.Content = _plant.Running ? P("Stop plant", "停廠") : P("Start plant", "開廠");
            AutomationProperties.SetName(StartButton, StartButton.Content?.ToString() ?? "Start plant");

            // --- Loop pressure (coloured toward synthesis-ready) ---
            double pressure = _plant.LoopPressureBar;
            PressureBar.Value = Math.Clamp(pressure, 0, PressureBar.Maximum);
            PressureValue.Text = $"{pressure:0} bar";
            Brush pressureBrush = PressureBrush(pressure);
            PressureBar.Foreground = pressureBrush;
            PressureValue.Foreground = pressureBrush;

            // --- Numeric readouts ---
            DrawValue.Text = $"{_plant.DrawnMW:0} MW";
            H2Value.Text = $"{_plant.H2RateKgPerHour:N0} kg/h";
            RateValue.Text = $"{_plant.TonnesPerHour:0.0} t/h";
            TotalValue.Text = $"{_plant.TotalTonnes:N0} t";
            Co2Value.Text = $"{_plant.Co2AvoidedTonnes:N0} t";

            // --- Set-point echo ---
            PowerValue.Text = $"{_plant.SetpointMW:0} MW";

            // --- Plant note ---
            PlantNote.Text = _plant.Powered
                ? P($"Electrolyzers + compressors energised · synthesising above {AmmoniaPlantService.SynthesisThresholdBar:0} bar · avoiding ≈ {AmmoniaPlantService.GreyCo2PerTonne:0.0} t CO₂ per tonne of NH₃.",
                    $"電解槽同壓縮機通電中 · 超過 {AmmoniaPlantService.SynthesisThresholdBar:0} bar 開始合成 · 每噸 NH₃ 慳約 {AmmoniaPlantService.GreyCo2PerTonne:0.0} 噸 CO₂。")
                : P($"Plant idle — no power · ammonia forms above {AmmoniaPlantService.SynthesisThresholdBar:0} bar; the present pressure model needs about {Math.Ceiling(AmmoniaPlantService.MinimumSynthesisDrawMW):0} MW to hold that threshold.",
                    $"工廠閒置 — 冇供電 · 超過 {AmmoniaPlantService.SynthesisThresholdBar:0} bar 先合成到氨；目前壓力模型大約要 {Math.Ceiling(AmmoniaPlantService.MinimumSynthesisDrawMW):0} MW 先頂得住門檻。");

            // --- Plant status line ---
            PlantStatus.Text = !_plant.Running
                ? P("Stopped — compressors off, loop bleeding down.", "已停廠 — 壓縮機熄咗，迴路洩緊壓。")
                : !_plant.Powered
                    ? P("Stalled — starved of reactor power.", "停頓 — 缺反應堆電力。")
                    : _plant.Synthesizing
                        ? P("Synthesising — ammonia flowing to storage.", "合成中 — 氨流入儲罐。")
                        : P("Pressurising — climbing toward synthesis pressure.", "加壓中 — 朝合成壓力上升。");

            AutomationProperties.SetHelpText(OutputBar, $"{OutputValue.Text}. {ReactorModeText.Text}");
            AutomationProperties.SetHelpText(PressureBar, $"{PressureValue.Text}. {PlantStatus.Text}");
            AutomationProperties.SetHelpText(PowerSlider, P(
                $"Setpoint {PowerValue.Text}. Steady synthesis needs about {Math.Ceiling(AmmoniaPlantService.MinimumSynthesisDrawMW):0} MW or more.",
                $"設定點 {PowerValue.Text}；穩定合成大約要 {Math.Ceiling(AmmoniaPlantService.MinimumSynthesisDrawMW):0} MW 或以上。"));

            UpdateEconomy();
        }
        catch { }
    }

    /// <summary>Colour the loop pressure from grey (cold) → orange (pressurising) → green (synthesis-ready).</summary>
    private static Brush PressureBrush(double bar)
    {
        if (bar < 30) return ThemeBrush("TextFillColorSecondaryBrush");
        if (bar < 100) return ThemeBrush("SystemFillColorAttentionBrush");
        if (bar < AmmoniaPlantService.SynthesisThresholdBar)
            return ThemeBrush("SystemFillColorCautionBrush");
        return ThemeBrush("SystemFillColorSuccessBrush");
    }

    private static Brush ThemeBrush(string key) => (Brush)Application.Current.Resources[key];

    /// <summary>
    /// Sell newly-made ammonia to the Reactor Bank. Banks in whole-tonne increments, throttled to at most once per
    /// ~3 s (6 ticks). <c>_tonnesDeposited</c> tracks the lifetime total already sold so it never double-counts;
    /// called each tick to drain any pending remainder. Never throws.
    /// </summary>
    private void SellAmmonia()
    {
        try
        {
            double produced = _plant.TotalTonnes;
            // Reset guard: if the plant was reset (lifetime total dropped), re-baseline the deposited counter.
            if (produced < _tonnesDeposited) _tonnesDeposited = produced;

            double pending = produced - _tonnesDeposited;
            bool throttleOk = (_tick - _lastDepositTick) >= 6; // ~3 s at 500 ms ticks
            if (pending >= 1.0 && throttleOk)
            {
                double tonnesToSell = Math.Floor(pending); // whole tonnes only; keep the fractional remainder
                double watts = tonnesToSell * PricePerTonne;
                ReactorEconomyService.I.Earn(watts, P("Ammonia sales", "氨銷售"));
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
            SellAmmonia();
            EconBalance.Text = $"{ReactorEconomyService.I.Balance:N1} {ReactorEconomyService.Symbol}";
            SalesValue.Text = $"+{_salesEarned:N1} {ReactorEconomyService.Symbol}";
            PriceValue.Text = P($"{PricePerTonne:0} ⚡ / tonne", $"{PricePerTonne:0} ⚡ / 噸");
        }
        catch { }
    }
}
