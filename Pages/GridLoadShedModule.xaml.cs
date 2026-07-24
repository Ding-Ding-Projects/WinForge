using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 電網卸載調度台 · Grid MW-Budget &amp; Load-Shed Dispatcher — the ⚛️ epic's "live MW-budget dashboard": a city
/// grid fed by the flagship reactor. Eight feeders (hospitals, water works, rail, heating, homes, commerce, EV
/// depots, industry) each declare a demand and a priority class. Every ~500 ms the dispatcher takes the
/// reactor's live MW, holds back an operator-set spinning reserve, and serves feeders in strict priority order —
/// the first feeder that no longer fits trips the cutoff and it plus everything below it sheds instantly, while
/// reclosing waits an anti-flap stability window. The feeder board is built from real controls in code (no
/// DataTemplates); operator breakers are ToggleSwitches. Reads <see cref="ReactorStatusApiService"/> snapshots;
/// when the reactor is not generating the whole board goes dark with a "needs nuclear power" empty-state.
/// Bilingual (粵語), anti-leak, never throws.
/// </summary>
public sealed partial class GridLoadShedModule : Page
{
    private readonly GridLoadShedService _grid = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _tick; // integer tick counter drives sim timing (not DateTime.Now)
    private bool _uiSync; // guards programmatic ToggleSwitch writes from re-entering Toggled

    private sealed class FeederRow
    {
        public GridLoadShedService.Feeder Feeder = null!;
        public TextBlock Chip = null!;
        public TextBlock Name = null!;
        public TextBlock Demand = null!;
        public TextBlock Status = null!;
        public ToggleSwitch Breaker = null!;
        public Grid Container = null!;
    }

    private readonly List<FeederRow> _rows = new();

    public GridLoadShedModule()
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
        BuildBoard();
        try { _grid.SetReservePct(ReserveSlider.Value); } catch { }
        Render();
        UpdateStep();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    /// <summary>Build one control row per feeder (chip · name · demand · status · breaker). Idempotent.</summary>
    private void BuildBoard()
    {
        try
        {
            if (_rows.Count > 0) return;
            foreach (var f in _grid.Feeders)
            {
                var row = new FeederRow { Feeder = f };

                var grid = new Grid { ColumnSpacing = 10, RowSpacing = 2, MinHeight = 52 };
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.Container = grid;

                row.Chip = new TextBlock
                {
                    Text = $"P{f.Priority}",
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    MinWidth = 26,
                };
                Grid.SetColumn(row.Chip, 0);
                grid.Children.Add(row.Chip);

                row.Name = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(row.Name, 1);
                grid.Children.Add(row.Name);

                row.Demand = new TextBlock
                {
                    Text = $"{f.DemandMW:0} MW",
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = ThemeBrush("TextFillColorSecondaryBrush"),
                };
                Grid.SetRow(row.Demand, 1);
                Grid.SetColumn(row.Demand, 0);
                grid.Children.Add(row.Demand);

                row.Status = new TextBlock
                {
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                };
                Grid.SetRow(row.Status, 1);
                Grid.SetColumn(row.Status, 1);
                grid.Children.Add(row.Status);

                row.Breaker = new ToggleSwitch
                {
                    IsOn = f.Enabled,
                    OnContent = null,
                    OffContent = null,
                    MinWidth = 44,
                    MinHeight = 44,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                string feederId = f.Id;
                row.Breaker.Toggled += (s, args) =>
                {
                    if (_uiSync) return;
                    try
                    {
                        _grid.SetFeederEnabled(feederId, ((ToggleSwitch)s).IsOn);
                        UpdateStep();
                    }
                    catch { }
                };
                Grid.SetRowSpan(row.Breaker, 2);
                Grid.SetColumn(row.Breaker, 2);
                grid.Children.Add(row.Breaker);

                FeederBoard.Children.Add(grid);
                _rows.Add(row);
            }
        }
        catch { }
    }

    private void Render()
    {
        try
        {
            Header.Title = P("Grid Load-Shed Dispatcher", "電網卸載調度台");
            Header.Subtitle = P("A live MW-budget board for the reactor's city grid: serve feeders by priority, shed the lowest when the budget runs out.",
                "反應堆城市電網嘅實時 MW 預算板：按優先次序供電，預算唔夠就卸走最低優先嘅饋線。");
            HeaderBlurb.Text = P(
                "A real grid never dies all at once — under-frequency load-shedding drops the least critical feeders first so hospitals and water pumps ride through. This board runs that scheme against the live ⚛️ reactor output: an operator-set spinning reserve is held back, then eight city feeders are dispatched in strict priority order. The first feeder that no longer fits the remaining budget trips the cutoff — it and everything below it sheds instantly, and a shed feeder only recloses after the budget has fitted it for a stability window (no breaker flapping). Watch the board reshuffle live as you throttle, trip or recover the reactor.",
                "真實電網唔會一次過冧晒 — 低頻卸載會先斬最唔緊要嘅饋線，等醫院同水泵捱得過去。呢塊板將呢套機制駁住 ⚛️ 反應堆實時輸出行：先扣起操作員設定嘅旋轉備用，再按嚴格優先次序調度八條城市饋線。第一條唔夠預算嘅饋線會觸發截斷 — 佢連同以下全部即刻卸載，而卸咗嘅饋線要等預算穩定容納到佢一段時間先會重合閘（斷路器唔會拍翼）。你節流、跳堆或者復原反應堆，塊板會實時重新洗牌。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            DispatchTitle.Text = P("Dispatch controls", "調度控制");
            ReserveLabel.Text = P("Spinning-reserve hold-back", "旋轉備用預留");
            BoardTitle.Text = P("Feeder board — priority ladder", "饋線板 — 優先階梯");
            BoardHint.Text = P("P1 sheds last. Toggles are operator breakers — an off feeder is intentional, not unserved.",
                "P1 最後先卸。開關係操作員斷路器 — 熄咗嘅饋線係刻意離線，唔算欠供。");
            TelemetryTitle.Text = P("Grid telemetry", "電網遙測");

            BudgetCaption.Text = P("Served vs dispatchable budget", "已供電對可調度預算");
            ShedCaption.Text = P("Shed load", "已卸負載");
            ReserveHeldCaption.Text = P("Reserve held", "預留備用");
            UnservedCaption.Text = P("Unserved energy (lifetime)", "欠供電量（總計）");
            EventsCaption.Text = P("Shed events", "卸載次數");

            ResetButton.Content = P("Reset board", "重設板面");
            AutomationProperties.SetName(ReserveSlider, ReserveLabel.Text);
            AutomationProperties.SetHelpText(ReserveSlider, P(
                "Choose how much reactor output to hold back as spinning reserve, from 0 to 30 percent.",
                "揀幾多反應堆輸出留做旋轉備用，可設 0 至 30%。"));
            AutomationProperties.SetName(OutputBar, ReactorTitle.Text);
            AutomationProperties.SetName(BudgetBar, BudgetCaption.Text);
            AutomationProperties.SetName(ResetButton, ResetButton.Content?.ToString() ?? "Reset board");

            foreach (var r in _rows)
            {
                r.Name.Text = P(r.Feeder.En, r.Feeder.Zh);
                string breakerName = P($"Operator breaker for {r.Feeder.En}", $"{r.Feeder.Zh}操作員斷路器");
                AutomationProperties.SetName(r.Breaker, breakerName);
                ToolTipService.SetToolTip(r.Breaker, breakerName);
            }

            UpdateStep();
        }
        catch { }
    }

    private void Reserve_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            _grid.SetReservePct(ReserveSlider.Value);
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _grid.Reset();
            _uiSync = true;
            try
            {
                ReserveSlider.Value = GridLoadShedService.DefaultReservePct;
                foreach (var r in _rows) r.Breaker.IsOn = true;
            }
            finally { _uiSync = false; }
            _grid.SetReservePct(ReserveSlider.Value);
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

            // Advance the dispatcher.
            _grid.Step(_tick, available, generating);

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
                NeedPowerBar.Title = P("Board dark — needs nuclear power", "板面停電 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — the bus is de-energised and every feeder is shed.",
                        "反應堆處於熔毀狀態 — 母線失電，所有饋線已卸載。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up — the city is dark until the bus re-energises.",
                            "反應堆已急停。復原並啟動佢 — 母線復電之前成個城市都冇電。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown). The dispatcher has nothing to dispatch without generation.",
                            "啟動反應堆（脫離 MODE 5 冷停機）。冇發電嘅話，調度台冇嘢可以調度。");
                NeedPowerBar.IsOpen = true;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
            }

            // --- Budget meter ---
            BudgetBar.Value = Math.Clamp(_grid.ServedMW, 0, BudgetBar.Maximum);
            BudgetValue.Text = $"{_grid.ServedMW:0} / {_grid.UsableMW:0} MW";
            Brush budgetBrush = _grid.ShedMW > 0
                ? ThemeBrush("SystemFillColorAttentionBrush")
                : generating
                    ? ThemeBrush("SystemFillColorSuccessBrush")
                    : ThemeBrush("TextFillColorSecondaryBrush");
            BudgetBar.Foreground = budgetBrush;
            BudgetValue.Foreground = budgetBrush;

            // --- Numeric readouts ---
            ShedValue.Text = $"{_grid.ShedMW:0} MW";
            ReserveHeldValue.Text = $"{_grid.AvailableMW - _grid.UsableMW:0} MW";
            UnservedValue.Text = $"{_grid.UnservedMWh:N2} MWh";
            EventsValue.Text = $"{_grid.ShedEvents:N0}";
            ReserveValue.Text = $"{_grid.ReservePct:0} %";

            // --- Feeder rows ---
            foreach (var r in _rows)
            {
                var f = r.Feeder;
                if (!f.Enabled)
                {
                    r.Status.Text = P("OFF", "離線");
                    r.Status.Foreground = ThemeBrush("TextFillColorSecondaryBrush");
                }
                else if (f.IsShed || f.ServedMW <= 0)
                {
                    r.Status.Text = P("SHED", "已卸載");
                    r.Status.Foreground = ThemeBrush("SystemFillColorCriticalBrush");
                }
                else
                {
                    r.Status.Text = P("SERVED", "供電中");
                    r.Status.Foreground = ThemeBrush("SystemFillColorSuccessBrush");
                }

                AutomationProperties.SetName(r.Container,
                    P($"Priority {f.Priority}, {f.En}, {f.DemandMW:0} megawatts, {r.Status.Text}",
                        $"優先級 {f.Priority}，{f.Zh}，{f.DemandMW:0} 兆瓦，{r.Status.Text}"));
                AutomationProperties.SetHelpText(r.Breaker,
                    P($"Priority {f.Priority}; demand {f.DemandMW:0} MW; status {r.Status.Text}.",
                        $"優先級 {f.Priority}；需求 {f.DemandMW:0} MW；狀態 {r.Status.Text}。"));
            }

            // --- Status + note lines ---
            DispatchStatus.Text = !generating
                ? P("Bus de-energised — the board is dark.", "母線失電 — 板面全黑。")
                : _grid.ShedMW > 0
                    ? P("Budget short — low-priority feeders shed, reclose pending stability.", "預算唔夠 — 低優先饋線已卸載，等穩定先重合閘。")
                    : P("All enabled feeders served within the budget.", "所有啟用饋線都喺預算內供電。");

            BoardNote.Text = P(
                $"Strict priority cutoff: the first feeder that no longer fits sheds together with everything below it. Reclose waits {GridLoadShedService.RecloseDelayTicks} stable ticks (~{GridLoadShedService.RecloseDelayTicks / 2} s) so breakers never flap.",
                $"嚴格優先截斷：第一條唔夠預算嘅饋線會連同以下全部一齊卸載。重合閘要等 {GridLoadShedService.RecloseDelayTicks} 個穩定 tick（約 {GridLoadShedService.RecloseDelayTicks / 2} 秒），斷路器唔會拍翼。");
            AutomationProperties.SetHelpText(OutputBar, $"{OutputValue.Text}. {ReactorModeText.Text}");
            AutomationProperties.SetHelpText(BudgetBar, $"{BudgetValue.Text}. {DispatchStatus.Text}");
            AutomationProperties.SetHelpText(ReserveSlider,
                P($"Reserve set to {ReserveValue.Text}; {ReserveHeldValue.Text} held.",
                    $"備用設為 {ReserveValue.Text}；已預留 {ReserveHeldValue.Text}。"));
        }
        catch { }
    }

    private static Brush ThemeBrush(string key) => (Brush)Application.Current.Resources[key];
}
