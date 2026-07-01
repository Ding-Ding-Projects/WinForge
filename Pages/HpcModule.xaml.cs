using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 超級電腦（HPC）· Supercomputer (HPC) center — a heavy, reactor-powered compute load. Reads the
/// live reactor output via <see cref="ReactorStatusApiService"/>: compute nodes online scale with
/// available MWe (1 node per ~2 MW, capped 5000) and drain a job queue while the reactor is
/// generating. When the reactor is not generating, nodes go offline and jobs park behind a prominent
/// "needs nuclear power" empty-state. Everything is computed in <see cref="HpcService"/>; the page
/// only renders. Pure managed, never throws, bilingual (English + 粵語).
/// </summary>
public sealed partial class HpcModule : Page
{
    private readonly HpcService _hpc = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _ticks;           // internal tick counter for deterministic sim ramps.
    private int _submitCount;      // auto-name counter for user-submitted jobs.

    // Size presets (label, node-hours). Chosen in Render() for locale.
    private static readonly double[] PresetSizes = { 60, 250, 900, 1200, 4800 };

    public HpcModule()
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
        try { if (JobList.ItemsSource == null) JobList.ItemsSource = _hpc.Jobs; } catch { }
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
            Header.Title = "Supercomputer (HPC) · 超級電腦（HPC）";
            HeaderBlurb.Text = P(
                "A reactor-powered high-performance computing centre. Compute nodes come online in step with the reactor's spare electricity (about one node per 2 MW). Submit jobs and watch the queue drain — but only while the reactor is generating. Cut the power and the whole cluster parks.",
                "由反應堆供電嘅高效能運算中心。運算節點會跟住反應堆嘅多餘電力上線（大約每 2 MW 一個節點）。提交作業，睇住佇列慢慢清 — 但淨係反應堆發緊電先得。冇電成個叢集就會停低。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            MetricsTitle.Text = P("Cluster status", "叢集狀態");
            NodesCaption.Text = P("Nodes online", "上線節點");
            PflopsCaption.Text = P("Compute (PFLOPS)", "運算能力（PFLOPS）");
            DoneCaption.Text = P("Jobs completed", "完成作業");
            QueueCaption.Text = P("Jobs queued", "佇列作業");
            BacklogCaption.Text = P("Backlog", "積壓工作量");

            SubmitTitle.Text = P("Submit a compute job", "提交運算作業");
            NameBox.PlaceholderText = P("Job name (optional)", "作業名稱（可選）");
            SubmitButton.Content = P("Submit job", "提交作業");
            SampleButton.Content = P("Add sample jobs", "加入範例作業");
            ResetButton.Content = P("Reset", "重設");
            QueueTitle.Text = P("Job queue", "作業佇列");
            QueueEmptyText.Text = P("Queue is empty. Submit a job or add the sample jobs to get started.",
                "佇列係空嘅。提交一個作業或者加入範例作業就可以開始。");

            RebuildSizeCombo();
            UpdateStep();
        }
        catch { }
    }

    private void RebuildSizeCombo()
    {
        try
        {
            int keep = SizeCombo.SelectedIndex;
            SizeCombo.Items.Clear();
            foreach (var s in PresetSizes)
            {
                string label = P($"{s:0} node-hours", $"{s:0} 節點·小時");
                SizeCombo.Items.Add(new ComboBoxItem { Content = label, Tag = s });
            }
            SizeCombo.SelectedIndex = keep >= 0 && keep < PresetSizes.Length ? keep : 1;
        }
        catch { }
    }

    private double SelectedSize()
    {
        try
        {
            if (SizeCombo.SelectedItem is ComboBoxItem cbi && cbi.Tag is double d) return d;
        }
        catch { }
        return 250;
    }

    private void Submit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _submitCount++;
            string name = NameBox.Text;
            if (string.IsNullOrWhiteSpace(name)) name = P($"batch-{_submitCount:0000}", $"批次-{_submitCount:0000}");
            _hpc.SubmitJob(name, SelectedSize());
            NameBox.Text = "";
            UpdateStep();
        }
        catch { }
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _hpc.AddSampleJobs(n => P($"sample-{n:0000}", $"範例-{n:0000}"));
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _hpc.Reset();
            _submitCount = 0;
            UpdateStep();
        }
        catch { }
    }

    private void OnTick(object? sender, object e)
    {
        _ticks++;
        UpdateStep();
    }

    private void UpdateStep()
    {
        try
        {
            // dt derived from the fixed 500 ms cadence (deterministic; no wall-clock in the sim).
            const double dt = 0.5;

            ReactorStatusSnapshot snap = ReactorStatusApiService.I.LastSnapshot;

            double available = double.IsNaN(snap.ElectricMW) || snap.ElectricMW < 0 ? 0 : snap.ElectricMW;
            string mode = string.IsNullOrWhiteSpace(snap.Mode) ? "?" : snap.Mode;
            bool coldMode = mode.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0
                            || mode.IndexOf("cold", StringComparison.OrdinalIgnoreCase) >= 0;
            bool generating = snap.IsGenerating && available > 1.0 && !snap.IsScrammed && !snap.IsMeltdown && !coldMode;

            _hpc.Tick(dt, available, generating);

            // Live reactor supply meter.
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
            var brush = new SolidColorBrush(meterColor);
            OutputBar.Foreground = brush;
            OutputValue.Foreground = brush;

            // Cluster metrics.
            NodesValue.Text = $"{_hpc.NodesOnline:N0}";
            PflopsValue.Text = $"{_hpc.Pflops:0.00}";
            DoneValue.Text = $"{_hpc.JobsCompleted:N0}";
            QueueValue.Text = $"{_hpc.QueuedJobCount:N0}";
            BacklogValue.Text = P($"{_hpc.QueueDepthNodeHours:N0} node-h", $"{_hpc.QueueDepthNodeHours:N0} 節點·小時");

            // Empty-state gating.
            if (!generating)
            {
                NeedPowerBar.Severity = snap.IsMeltdown ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Needs nuclear power", "需要核電");
                NeedPowerBar.Message = snap.IsMeltdown
                    ? P("Reactor is in a meltdown state — all compute nodes are offline and jobs are parked.",
                        "反應堆處於熔毀狀態 — 所有運算節點離線，作業已暫停。")
                    : snap.IsScrammed
                        ? P("Reactor is scrammed. Recover and start it up to bring the cluster online.",
                            "反應堆已急停。復原並啟動先可以令叢集上線。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown) to power the HPC cluster. Jobs stay queued until then.",
                            "啟動反應堆（脫離 MODE 5 冷停機）先可以供電畀 HPC 叢集。到時之前作業會一直排隊等。");
                NeedPowerBar.IsOpen = true;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
            }

            // Queue empty hint.
            bool empty = _hpc.QueuedJobCount == 0;
            QueueEmptyText.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            JobList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }
        catch { }
    }
}
