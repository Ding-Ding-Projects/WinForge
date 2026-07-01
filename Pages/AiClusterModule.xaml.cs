using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// AI 訓練叢集 · AI Training Cluster — a ⚛️ heavy, reactor-powered GPU load. The cluster ONLY trains
/// while the nuclear reactor feeds it big MW. The operator sets a target power draw (clamped to the
/// reactor's live available output); throughput scales with the drawn MW and accumulates PFLOP-days
/// toward a chosen model size. If the reactor cannot supply the demand — or is not generating (cold
/// MODE 5, scrammed, meltdown) — the run checkpoints and PAUSES behind a prominent "needs nuclear
/// power" empty-state, and progress freezes. Pure managed, deterministic, never throws.
/// </summary>
public sealed partial class AiClusterModule : Page
{
    private readonly AiClusterService _cluster = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _suppress;

    public AiClusterModule()
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
        BuildModelCombo();
        Render();
        UpdateStep();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        BuildModelCombo();
        Render();
    }

    private void BuildModelCombo()
    {
        try
        {
            _suppress = true;
            int prev = ModelCombo.SelectedIndex;
            ModelCombo.Items.Clear();
            ModelCombo.Items.Add(P("Small — 8 PFLOP-days", "細型 — 8 PFLOP-日"));
            ModelCombo.Items.Add(P("Medium — 40 PFLOP-days", "中型 — 40 PFLOP-日"));
            ModelCombo.Items.Add(P("Large — 150 PFLOP-days", "大型 — 150 PFLOP-日"));
            ModelCombo.Items.Add(P("Frontier — 600 PFLOP-days", "前沿 — 600 PFLOP-日"));
            ModelCombo.SelectedIndex = prev >= 0 ? prev : SizeToIndex(_cluster.Size);
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    private static int SizeToIndex(AiClusterService.ModelSize s) => s switch
    {
        AiClusterService.ModelSize.Small => 0,
        AiClusterService.ModelSize.Medium => 1,
        AiClusterService.ModelSize.Large => 2,
        AiClusterService.ModelSize.Frontier => 3,
        _ => 1,
    };

    private static AiClusterService.ModelSize IndexToSize(int i) => i switch
    {
        0 => AiClusterService.ModelSize.Small,
        1 => AiClusterService.ModelSize.Medium,
        2 => AiClusterService.ModelSize.Large,
        3 => AiClusterService.ModelSize.Frontier,
        _ => AiClusterService.ModelSize.Medium,
    };

    private void Render()
    {
        try
        {
            Header.Title = "AI Training Cluster · AI 訓練叢集";
            HeaderBlurb.Text = P(
                "A GPU training cluster that runs on nuclear power. Set how many megawatts to draw — it trains only while the reactor feeds it that much power. Lose the power and the run checkpoints and stalls.",
                "一個靠核電運行嘅 GPU 訓練叢集。設定要抽幾多兆瓦 — 只有反應堆餵到咁多電先會訓練。冇電就即刻儲存進度並暫停。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            ClusterTitle.Text = P("Cluster power & run", "叢集電力同訓練");
            DrawLabel.Text = P("Target power draw (MW)", "目標抽電量（MW）");
            ModelLabel.Text = P("Model size", "模型大小");
            ProgressTitle.Text = P("Training progress", "訓練進度");
            ProgressCaption.Text = P("Model progress", "模型進度");
            ThroughputCaption.Text = P("Throughput", "運算吞吐");
            ComputeCaption.Text = P("Compute done", "已完成運算");
            GpuCaption.Text = P("GPU utilisation", "GPU 使用率");
            TempCaption.Text = P("Rack temperature", "機架溫度");
            DrawnCaption.Text = P("Now drawing", "現正抽電");
            CheckpointCaption.Text = P("Checkpoints", "檢查點");

            ResetButton.Content = P("Reset", "重設");
            NewRunButton.Content = P("New run", "新訓練");
            UpdateRunButton();
            UpdateStep();
        }
        catch { }
    }

    private void UpdateRunButton()
    {
        RunButton.Content = _cluster.Running ? P("Pause run", "暫停訓練") : P("Start run", "開始訓練");
    }

    private void DrawSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        UpdateStep();
    }

    private void ModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            _cluster.NewRun(IndexToSize(ModelCombo.SelectedIndex));
            UpdateRunButton();
            UpdateStep();
        }
        catch { }
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_cluster.Running) _cluster.Pause();
            else _cluster.Start();
            UpdateRunButton();
            UpdateStep();
        }
        catch { }
    }

    private void NewRun_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _cluster.NewRun(IndexToSize(ModelCombo.SelectedIndex));
            UpdateRunButton();
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _cluster.Reset();
            _suppress = true;
            DrawSlider.Value = 0;
            ModelCombo.SelectedIndex = SizeToIndex(_cluster.Size);
            _suppress = false;
            UpdateRunButton();
            UpdateStep();
        }
        catch { }
    }

    private void OnTick(object? sender, object e) => UpdateStep();

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

            // Clamp the draw slider to the available output (never above the service ceiling).
            double sliderMax = Math.Min(AiClusterService.MaxDrawMW,
                Math.Max(50, Math.Round(available <= 1 ? AiClusterService.MaxDrawMW : available)));
            if (Math.Abs(DrawSlider.Maximum - sliderMax) > 0.5)
            {
                _suppress = true;
                DrawSlider.Maximum = sliderMax;
                if (DrawSlider.Value > sliderMax) DrawSlider.Value = sliderMax;
                _suppress = false;
            }

            double requested = double.IsNaN(DrawSlider.Value) ? 0 : DrawSlider.Value;

            _cluster.Tick(requested, available, generating);

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

            // Empty-state gating: prominent "needs nuclear power" whenever the run can't be fed.
            bool starved = _cluster.Stalled || (!generating) || (generating && available < requested && requested > 0);
            if (meltdown || scrammed || !generating)
            {
                NeedPowerBar.Severity = meltdown ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Training stalled — needs nuclear power", "訓練暫停 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state. The run is checkpointed and paused.", "反應堆處於熔毀狀態。訓練已儲存進度並暫停。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up to resume training.", "反應堆已急停。復原並啟動先可以繼續訓練。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown) to power the GPU cluster.",
                            "啟動反應堆（脫離 MODE 5 冷停機）先可以供電俾 GPU 叢集。");
                NeedPowerBar.IsOpen = true;
            }
            else if (_cluster.Running && starved)
            {
                NeedPowerBar.Severity = InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Training stalled — needs nuclear power", "訓練暫停 — 需要核電");
                NeedPowerBar.Message = P(
                    $"Reactor is delivering {available:0.0} MW but the cluster wants {requested:0.0} MW. Lower the draw or raise reactor output. Progress is frozen.",
                    $"反應堆供緊 {available:0.0} MW，但叢集想要 {requested:0.0} MW。調低抽電量或者加大反應堆輸出。進度已凍結。");
                NeedPowerBar.IsOpen = true;
            }
            else if (_cluster.Complete)
            {
                NeedPowerBar.Severity = InfoBarSeverity.Success;
                NeedPowerBar.Title = P("Training run complete", "訓練完成");
                NeedPowerBar.Message = P("The model finished training. Start a new run to keep the cluster busy.",
                    "模型已完成訓練。開始新訓練令叢集繼續運作。");
                NeedPowerBar.IsOpen = true;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
            }

            // Draw setpoint readout.
            DrawValue.Text = $"{requested:0} MW";

            // Progress + telemetry.
            ProgressBarRun.Value = Math.Clamp(_cluster.ProgressPct, 0, 100);
            ProgressValue.Text = $"{_cluster.ProgressPct:0.0}%";
            ThroughputValue.Text = $"{_cluster.PflopsNow:0.00} PFLOP/s";
            ComputeValue.Text = P($"{_cluster.PflopDaysDone:0.000} / {_cluster.TargetPflopDaysCurrent:0} PFLOP-days",
                $"{_cluster.PflopDaysDone:0.000} / {_cluster.TargetPflopDaysCurrent:0} PFLOP-日");

            GpuBar.Value = Math.Clamp(_cluster.GpuUtilPct, 0, 100);
            GpuValue.Text = $"{_cluster.GpuUtilPct:0}%";

            TempBar.Value = Math.Clamp(_cluster.RackTempC, TempBar.Minimum, TempBar.Maximum);
            TempValue.Text = $"{_cluster.RackTempC:0.0} °C";
            Color tempColor = _cluster.RackTempC > 78
                ? Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A)
                : _cluster.RackTempC > 60
                    ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)
                    : Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A);
            TempBar.Foreground = new SolidColorBrush(tempColor);

            DrawnValue.Text = $"{_cluster.DrawnMW:0.0} MW";
            CheckpointValue.Text = $"{_cluster.Checkpoints}";
        }
        catch { }
    }
}
