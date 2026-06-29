using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// btop 風格系統監察 · btop-style in-app system monitor — per-core CPU bars, a CPU sparkline with
/// temperature, RAM + swap meters, down/up network sparklines, and a sortable, searchable process grid
/// with per-process CPU bars and right-click Kill / priority / efficiency / affinity. All bilingual.
/// </summary>
public sealed partial class SystemMonitorModule : Page
{
    public sealed class ProcRow : INotifyPropertyChanged
    {
        public int Pid { get; }
        public string Name { get; }
        public string PidText { get; }
        private string _cpu = "", _mem = "";
        private double _cpuPct;
        private double _cpuBarWidth;
        private Brush _cpuBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        public double CpuPct { get => _cpuPct; private set { _cpuPct = value; } }
        public long MemBytes { get; private set; }

        public string CpuText { get => _cpu; private set { if (_cpu != value) { _cpu = value; OnPC(); } } }
        public string MemText { get => _mem; private set { if (_mem != value) { _mem = value; OnPC(); } } }
        public double CpuBarWidth { get => _cpuBarWidth; private set { if (_cpuBarWidth != value) { _cpuBarWidth = value; OnPC(); } } }
        public Brush CpuBrush { get => _cpuBrush; private set { _cpuBrush = value; OnPC(); } }

        public ProcRow(ProcInfo s) { Pid = s.Pid; Name = s.Name; PidText = s.Pid.ToString(); Apply(s); }

        public void Apply(ProcInfo s)
        {
            CpuPct = s.CpuPercent;
            MemBytes = s.MemoryBytes;
            CpuText = $"{Math.Round(s.CpuPercent)}%";
            MemText = SystemMonitor.Bytes(s.MemoryBytes);
            // Cap the in-row CPU bar at 100% of one core's worth; max track width is 80px.
            CpuBarWidth = Math.Clamp(s.CpuPercent, 0, 100) / 100.0 * 80.0;
            CpuBrush = new SolidColorBrush(CoreBars.LoadColor(s.CpuPercent));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPC([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private enum SortKey { Cpu, Mem, Name, Pid }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<ProcRow> _rows = new();
    private const double RamTrack = 252; // matched by ActualWidth at runtime
    private const int TopN = 60;
    private SortKey _sort = SortKey.Cpu;
    private bool _sortDesc = true;
    private string _filter = "";
    private double _intervalSec = 1.0;
    private double _maxNetSeen = 1024 * 64; // adaptive network graph scale (start ~64 KB/s)

    public SystemMonitorModule()
    {
        InitializeComponent();
        ProcList.ItemsSource = _rows;
        _timer.Tick += (_, _) => Tick();
        Loc.I.LanguageChanged += (_, _) => Render();
        ActualThemeChanged += (_, _) => ApplyGraphTheme();
        Loaded += OnLoaded;
        Unloaded += (_, _) => _timer.Stop();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        CoreBarsCtl.Build(SystemMonitor.CoreCount, ColumnsForCores(SystemMonitor.CoreCount));
        CpuGraph.Capacity = 90;
        DownGraph.Capacity = 60;
        UpGraph.Capacity = 60;
        ApplyGraphTheme();

        Render();
        // Prime the delta-based samplers so the first visible tick has real values.
        SystemMonitor.CpuPercent();
        SystemMonitor.Network(_intervalSec);
        SystemMonitor.Sample(TopN, true);
        Tick();
        _timer.Start();
    }

    private static int ColumnsForCores(int n) => n <= 8 ? 2 : n <= 24 ? 3 : 4;

    private void ApplyGraphTheme()
    {
        bool dark = ActualTheme == ElementTheme.Dark;
        CpuGraph.ApplyTheme(dark);
        DownGraph.ApplyTheme(dark);
        UpGraph.ApplyTheme(dark);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "System Monitor · 系統監察";
        HeaderBlurb.Text = P("A btop-style live dashboard — per-core CPU bars, memory and swap meters, network graphs, and the busiest processes. Sort, search, set priority, efficiency mode or end any process.",
            "btop 風格即時儀表板 — 每核心 CPU 條、記憶體同 swap 計、網絡圖，再加最忙嘅程序。可排序、搜尋、設定優先權、效率模式或結束任何程序。");
        CpuLabel.Text = P("CPU", "CPU");
        RamLabel.Text = P("Memory", "記憶體");
        SwapLabel.Text = P("Swap (page file)", "Swap（頁面檔）");
        NetLabel.Text = P("Network", "網絡");
        UpLabel.Text = P("Uptime", "運行時間");
        ProcTitle.Text = P("Processes", "程序");
        IntervalLabel.Text = P("Refresh", "更新間隔");
        HdrActions.Text = P("Actions", "操作");
        SearchBox.PlaceholderText = P("Filter processes…", "篩選程序…");

        RenderSortHeaders();

        int sel = IntervalBox.SelectedIndex < 0 ? 1 : IntervalBox.SelectedIndex;
        IntervalBox.Items.Clear();
        IntervalBox.Items.Add(P("0.5 s", "0.5 秒"));
        IntervalBox.Items.Add(P("1 s", "1 秒"));
        IntervalBox.Items.Add(P("2 s", "2 秒"));
        IntervalBox.Items.Add(P("5 s", "5 秒"));
        IntervalBox.SelectedIndex = sel;
    }

    private void RenderSortHeaders()
    {
        string Arrow(SortKey k) => _sort == k ? (_sortDesc ? "  ▼" : "  ▲") : "";
        HdrPid.Content = MakeHeader(P("PID", "PID") + Arrow(SortKey.Pid));
        HdrName.Content = MakeHeader(P("Process", "程序") + Arrow(SortKey.Name));
        HdrCpu.Content = MakeHeader(P("CPU", "CPU") + Arrow(SortKey.Cpu));
        HdrMem.Content = MakeHeader(P("Memory", "記憶體") + Arrow(SortKey.Mem));
    }

    private static TextBlock MakeHeader(string text) => new()
    {
        Text = text,
        FontSize = 12,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
    };

    // ---------------- Interaction ----------------
    private void Interval_Changed(object sender, SelectionChangedEventArgs e)
    {
        _intervalSec = IntervalBox.SelectedIndex switch { 0 => 0.5, 2 => 2.0, 3 => 5.0, _ => 1.0 };
        _timer.Interval = TimeSpan.FromSeconds(_intervalSec);
    }

    private void SortHeader_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tag) return;
        var key = tag switch { "pid" => SortKey.Pid, "name" => SortKey.Name, "mem" => SortKey.Mem, _ => SortKey.Cpu };
        if (_sort == key) _sortDesc = !_sortDesc;
        else { _sort = key; _sortDesc = key is SortKey.Cpu or SortKey.Mem; } // numbers default high→low, names low→high
        RenderSortHeaders();
        if (IsLoaded) Tick();
    }

    private void Search_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _filter = sender.Text?.Trim() ?? "";
        if (IsLoaded) Tick();
    }

    // ---------------- Sampling tick ----------------
    private void Tick()
    {
        var cpu = SystemMonitor.CpuPercent();
        CpuValue.Text = $"{Math.Round(cpu)}%";
        CpuGraph.Push(cpu);

        var temp = SystemMonitor.CpuTemperature();
        CpuTemp.Text = temp is { } t ? $"{Math.Round(t)}°C" : "";

        CoreBarsCtl.Update(SystemMonitor.PerCoreLoad());

        var (memPct, used, total) = SystemMonitor.Memory();
        RamValue.Text = $"{Math.Round(memPct)}%";
        RamBar.Width = (RamTrackBg.ActualWidth > 0 ? RamTrackBg.ActualWidth : RamTrack) * memPct / 100.0;
        RamBar.Background = new SolidColorBrush(CoreBars.LoadColor(memPct));
        RamSub.Text = $"{SystemMonitor.Bytes(used)} / {SystemMonitor.Bytes(total)}";

        var (swPct, swUsed, swTotal) = SystemMonitor.Swap();
        if (swTotal > 0)
        {
            SwapValue.Text = $"{Math.Round(swPct)}%";
            SwapBar.Width = (RamTrackBg.ActualWidth > 0 ? RamTrackBg.ActualWidth : RamTrack) * swPct / 100.0;
            SwapBar.Background = new SolidColorBrush(CoreBars.LoadColor(swPct));
            SwapSub.Text = $"{SystemMonitor.Bytes(swUsed)} / {SystemMonitor.Bytes(swTotal)}";
        }
        else
        {
            SwapValue.Text = P("off", "關");
            SwapBar.Width = 0;
            SwapSub.Text = P("No page file", "冇頁面檔");
        }

        var (down, up) = SystemMonitor.Network(_intervalSec);
        _maxNetSeen = Math.Max(_maxNetSeen, Math.Max(down, up));
        DownGraph.Max = _maxNetSeen;
        UpGraph.Max = _maxNetSeen;
        DownGraph.Push(down);
        UpGraph.Push(up);
        NetDown.Text = $"↓ {SystemMonitor.Bytes(down)}/s";
        NetUp.Text = $"↑ {SystemMonitor.Bytes(up)}/s";

        UpValue.Text = SystemMonitor.Uptime();

        // Sample more than we show (TopN), then sort/filter for display.
        var sample = SystemMonitor.Sample(TopN, _sort == SortKey.Cpu);
        Reconcile(SortAndFilter(sample));
    }

    private List<ProcInfo> SortAndFilter(List<ProcInfo> sample)
    {
        IEnumerable<ProcInfo> q = sample;
        if (_filter.Length > 0)
            q = q.Where(p => p.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
                          || p.Pid.ToString().Contains(_filter));

        q = _sort switch
        {
            SortKey.Cpu => _sortDesc ? q.OrderByDescending(p => p.CpuPercent) : q.OrderBy(p => p.CpuPercent),
            SortKey.Mem => _sortDesc ? q.OrderByDescending(p => p.MemoryBytes) : q.OrderBy(p => p.MemoryBytes),
            SortKey.Name => _sortDesc ? q.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase) : q.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase),
            SortKey.Pid => _sortDesc ? q.OrderByDescending(p => p.Pid) : q.OrderBy(p => p.Pid),
            _ => q,
        };
        return q.Take(40).ToList();
    }

    /// <summary>Update the bound collection in place so open menus and item containers survive the refresh.</summary>
    private void Reconcile(List<ProcInfo> sample)
    {
        var present = new HashSet<int>();
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

    // ---------------- Process actions ----------------
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
