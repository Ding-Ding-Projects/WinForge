using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內系統監察 · In-app system monitor — live CPU/RAM/network plus a Process Explorer-style list:
/// per-process CPU%, sort by CPU or memory, set priority (High→Idle) and end task. No redirect. Bilingual.
/// </summary>
public sealed partial class SystemMonitorModule : Page
{
    public sealed class ProcRow : INotifyPropertyChanged
    {
        public int Pid { get; }
        public string Name { get; }
        private string _cpu = "", _mem = "";
        public string CpuText { get => _cpu; private set { if (_cpu != value) { _cpu = value; OnPC(); } } }
        public string MemText { get => _mem; private set { if (_mem != value) { _mem = value; OnPC(); } } }

        public ProcRow(ProcInfo s) { Pid = s.Pid; Name = s.Name; Apply(s); }
        public void Apply(ProcInfo s)
        {
            CpuText = $"{Math.Round(s.CpuPercent)}%";
            MemText = SystemMonitor.Bytes(s.MemoryBytes);
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPC([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<ProcRow> _rows = new();
    private const double CpuTrack = 188, RamTrack = 208;
    private const int TopN = 14;
    private bool _byCpu;

    public SystemMonitorModule()
    {
        InitializeComponent();
        ProcList.ItemsSource = _rows;
        _timer.Tick += (_, _) => Tick();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); SystemMonitor.CpuPercent(); SystemMonitor.Network(1); SystemMonitor.Sample(TopN, _byCpu); Tick(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "System Monitor · 系統監察";
        HeaderBlurb.Text = P("Live CPU, memory and network, plus the busiest processes — set a priority or end any with one click. Updates every second.",
            "即時 CPU、記憶體同網絡，再加最忙嘅程序 — 一撳就設定優先權或者結束。每秒更新。");
        CpuLabel.Text = P("CPU", "CPU");
        RamLabel.Text = P("Memory", "記憶體");
        NetLabel.Text = P("Network", "網絡");
        UpLabel.Text = P("Uptime", "運行時間");
        SortLabel.Text = P("Sort by", "排序");
        ColName.Text = P("Process", "程序");
        ColCpu.Text = P("CPU", "CPU");
        ColMem.Text = P("Memory", "記憶體");

        int sel = SortBox.SelectedIndex < 0 ? 0 : SortBox.SelectedIndex;
        SortBox.Items.Clear();
        SortBox.Items.Add(P("Memory", "記憶體"));
        SortBox.Items.Add(P("CPU", "CPU"));
        SortBox.SelectedIndex = sel;
    }

    private void Sort_Changed(object sender, SelectionChangedEventArgs e)
    {
        _byCpu = SortBox.SelectedIndex == 1;
        if (IsLoaded) Tick();
    }

    private void Tick()
    {
        var cpu = SystemMonitor.CpuPercent();
        CpuValue.Text = $"{Math.Round(cpu)}%";
        CpuBar.Width = CpuTrack * cpu / 100.0;

        var (memPct, used, total) = SystemMonitor.Memory();
        RamValue.Text = $"{Math.Round(memPct)}%";
        RamBar.Width = RamTrack * memPct / 100.0;
        RamSub.Text = $"{SystemMonitor.Bytes(used)} / {SystemMonitor.Bytes(total)}";

        var (down, up) = SystemMonitor.Network(1);
        NetDown.Text = $"↓ {SystemMonitor.Bytes(down)}/s";
        NetUp.Text = $"↑ {SystemMonitor.Bytes(up)}/s";

        UpValue.Text = SystemMonitor.Uptime();

        Reconcile(SystemMonitor.Sample(TopN, _byCpu));
    }

    /// <summary>Update the bound collection in place so open menus and item containers survive the refresh.</summary>
    private void Reconcile(System.Collections.Generic.List<ProcInfo> sample)
    {
        var present = new System.Collections.Generic.HashSet<int>();
        foreach (var s in sample) present.Add(s.Pid);
        for (int i = _rows.Count - 1; i >= 0; i--)
            if (!present.Contains(_rows[i].Pid)) _rows.RemoveAt(i);

        for (int i = 0; i < sample.Count; i++)
        {
            var s = sample[i];
            int idx = -1;
            for (int j = i; j < _rows.Count; j++)
                if (_rows[j].Pid == s.Pid) { idx = j; break; }

            if (idx == -1) { _rows.Insert(i, new ProcRow(s)); }
            else { _rows[idx].Apply(s); if (idx != i) _rows.Move(idx, i); }
        }
        while (_rows.Count > sample.Count) _rows.RemoveAt(_rows.Count - 1);
    }

    private void Priority_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not ProcRow row) return;
        var mf = new MenuFlyout();
        foreach (var (label, cls) in Priorities())
        {
            var item = new MenuFlyoutItem { Text = label };
            item.Click += (_, _) => SystemMonitor.SetPriority(row.Pid, cls);
            mf.Items.Add(item);
        }

        mf.Items.Add(new MenuFlyoutSeparator());
        var ecoOn = new MenuFlyoutItem { Text = P("Efficiency mode: on", "效率模式：開") };
        ecoOn.Click += (_, _) => SystemMonitor.SetEfficiency(row.Pid, true);
        mf.Items.Add(ecoOn);
        var ecoOff = new MenuFlyoutItem { Text = P("Efficiency mode: off", "效率模式：關") };
        ecoOff.Click += (_, _) => SystemMonitor.SetEfficiency(row.Pid, false);
        mf.Items.Add(ecoOff);

        mf.Items.Add(new MenuFlyoutSeparator());
        var aff = new MenuFlyoutItem { Text = P("CPU affinity…", "CPU 親和性…") };
        aff.Click += async (_, _) => await ShowAffinityDialog(row);
        mf.Items.Add(aff);

        mf.ShowAt(b);
    }

    private async System.Threading.Tasks.Task ShowAffinityDialog(ProcRow row)
    {
        int n = SystemMonitor.CoreCount;
        long cur = SystemMonitor.GetAffinity(row.Pid);

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = P($"Choose which logical cores \"{row.Name}\" may run on.", $"揀「{row.Name}」可以喺邊幾個邏輯核心上面行。"),
            TextWrapping = TextWrapping.Wrap,
        });

        const int cols = 4;
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 4 };
        for (int c = 0; c < cols; c++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        var boxes = new CheckBox[n];
        for (int i = 0; i < n; i++)
        {
            var cb = new CheckBox { Content = $"CPU {i}", IsChecked = cur == 0 || (cur & (1L << i)) != 0 };
            boxes[i] = cb;
            int r = i / cols, c = i % cols;
            while (grid.RowDefinitions.Count <= r) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(cb, r); Grid.SetColumn(cb, c);
            grid.Children.Add(cb);
        }
        panel.Children.Add(new ScrollViewer { Content = grid, MaxHeight = 280, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });

        var dlg = new ContentDialog
        {
            Title = P("CPU affinity", "CPU 親和性"),
            Content = panel,
            PrimaryButtonText = P("Apply", "套用"),
            SecondaryButtonText = P("All cores", "全部核心"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        var res = await dlg.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            long mask = 0;
            for (int i = 0; i < n; i++) if (boxes[i].IsChecked == true) mask |= 1L << i;
            if (mask != 0) SystemMonitor.SetAffinity(row.Pid, mask);
        }
        else if (res == ContentDialogResult.Secondary)
        {
            long all = n >= 64 ? -1L : (1L << n) - 1;
            SystemMonitor.SetAffinity(row.Pid, all);
        }
    }

    private (string, ProcessPriorityClass)[] Priorities() => new[]
    {
        (P("High", "高"), ProcessPriorityClass.High),
        (P("Above normal", "高於正常"), ProcessPriorityClass.AboveNormal),
        (P("Normal", "正常"), ProcessPriorityClass.Normal),
        (P("Below normal", "低於正常"), ProcessPriorityClass.BelowNormal),
        (P("Idle (low)", "閒置（低）"), ProcessPriorityClass.Idle),
    };

    private void Kill_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ProcRow row)
        {
            SystemMonitor.Kill(row.Pid);
            Tick();
        }
    }
}
