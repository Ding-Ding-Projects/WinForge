using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 一棵程序樹節點（可綁定）· One bindable node in the process tree. Holds the live process
/// stats and its child processes; updated in place so the tree's expand state survives refresh.
/// </summary>
public sealed class ProcNode : INotifyPropertyChanged
{
    public int Pid { get; }
    public string PidText { get; }
    public ObservableCollection<ProcNode> Children { get; } = new();

    private string _name = "", _desc = "", _cpu = "", _mem = "", _threads = "", _owner = "";
    public string Name { get => _name; private set { if (_name != value) { _name = value; OnPC(); } } }
    public string Description { get => _desc; private set { if (_desc != value) { _desc = value; OnPC(); } } }
    public string CpuText { get => _cpu; private set { if (_cpu != value) { _cpu = value; OnPC(); } } }
    public string MemText { get => _mem; private set { if (_mem != value) { _mem = value; OnPC(); } } }
    public string ThreadText { get => _threads; private set { if (_threads != value) { _threads = value; OnPC(); } } }
    public string Owner { get => _owner; private set { if (_owner != value) { _owner = value; OnPC(); } } }

    public ProcEntry Entry { get; private set; }

    public ProcNode(ProcEntry e) { Pid = e.Pid; PidText = e.Pid.ToString(); Entry = e; Apply(e); }

    public void Apply(ProcEntry e)
    {
        Entry = e;
        Name = e.Name;
        Description = e.Description;
        CpuText = e.CpuPercent >= 0.05 ? $"{Math.Round(e.CpuPercent)}%" : "—";
        MemText = ProcessExplorerService.Bytes(e.WorkingSetBytes);
        ThreadText = e.ThreadCount > 0 ? e.ThreadCount.ToString() : "—";
        Owner = e.AccessDenied && string.IsNullOrEmpty(e.Owner) ? "" : e.Owner;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPC([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>
/// 原生程序總管 · Native Process Explorer — a parent/child process tree built from PID + parent PID
/// (WMI Win32_Process + System.Diagnostics), with live CPU%, working set, threads, owner and
/// description. Select a process to End / End Tree / set Priority / open its file location / copy
/// its PID or path, or open a details flyout. Pure managed + P/Invoke + WMI; no external tools.
/// </summary>
public sealed partial class ProcessExplorerModule : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly ObservableCollection<ProcNode> _roots = new();
    private readonly Dictionary<int, ProcNode> _index = new();
    private double _intervalSec = 2.0;
    private string _filter = "";
    private bool _firstFill = true;
    private bool _busy;

    public ProcessExplorerModule()
    {
        InitializeComponent();
        Tree.ItemsSource = _roots;
        _timer.Tick += (_, _) => _ = Tick();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += (_, _) => { _timer.Stop(); Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? s, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        // Prime the CPU delta sampler (off the UI thread) so the first visible tick has real percentages.
        try { await System.Threading.Tasks.Task.Run(() => ProcessExplorerService.Snapshot()); }
        catch { /* ignore priming failures */ }
        await Tick();
        _timer.Start();
    }

    private void Render()
    {
        Header.Title = "Process Explorer · 程序總管";
        HeaderBlurb.Text = P(
            "A System Informer-style process tree built natively from WMI and the OS — parent/child hierarchy, live CPU %, working set, threads, owner and description. Select a process to end it (or its whole tree), set priority, open its file location, copy its PID or path, or view full details.",
            "原生用 WMI 同作業系統砌出嚟、System Informer 風格嘅程序樹 — 父／子層級、即時 CPU %、工作集、執行緒、擁有者同描述。揀一個程序就可以結束佢（或成棵樹）、設定優先權、開啟檔案位置、複製 PID 或路徑，或者睇晒詳細資料。");

        IntervalLabel.Text = P("Refresh", "更新間隔");
        SumProcLabel.Text = P("Processes", "程序總數");
        SumCpuLabel.Text = P("Total CPU", "總 CPU");
        SumMemLabel.Text = P("Memory in use", "記憶體使用");
        SearchBox.PlaceholderText = P("Search by name, PID or command line…", "用名稱、PID 或命令列搜尋…");

        ColName.Text = P("Process", "程序");
        ColPid.Text = P("PID", "PID");
        ColCpu.Text = P("CPU", "CPU");
        ColMem.Text = P("Working set", "工作集");
        ColThreads.Text = P("Thr", "執行緒");
        ColUser.Text = P("User", "使用者");

        DetailsBtn.Content = P("Details", "詳細");
        LocationBtn.Content = P("Open location", "開啟位置");
        CopyBtn.Content = P("Copy", "複製");
        PriorityBtn.Content = P("Priority", "優先權");
        EndBtn.Content = P("End process", "結束程序");
        EndTreeBtn.Content = P("End tree", "結束程序樹");

        if (!ProcessExplorerService.IsElevated())
        {
            ElevNote.IsOpen = true;
            ElevNote.Message = P(
                "Running without administrator rights. Some system processes show limited details (owner, command line, memory). Relaunch WinForge as administrator to reveal more.",
                "而家冇系統管理員權限運行。部分系統程序只會顯示有限資料（擁有者、命令列、記憶體）。以系統管理員身份重新啟動 WinForge 就會睇到更多。");
        }
        else
        {
            ElevNote.IsOpen = false;
        }

        int sel = IntervalBox.SelectedIndex < 0 ? 1 : IntervalBox.SelectedIndex;
        IntervalBox.Items.Clear();
        IntervalBox.Items.Add(P("1 s", "1 秒"));
        IntervalBox.Items.Add(P("2 s", "2 秒"));
        IntervalBox.Items.Add(P("5 s", "5 秒"));
        IntervalBox.SelectedIndex = sel;
    }

    private void Interval_Changed(object sender, SelectionChangedEventArgs e)
    {
        _intervalSec = IntervalBox.SelectedIndex switch { 0 => 1.0, 2 => 5.0, _ => 2.0 };
        _timer.Interval = TimeSpan.FromSeconds(_intervalSec);
    }

    private void Search_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        _filter = sender.Text?.Trim() ?? "";
        if (IsLoaded) _ = Tick();
    }

    // ---------------- Sampling ----------------
    // The WMI + per-process GetOwner snapshot is heavy; run it off the UI thread and
    // guard against re-entrancy so slow snapshots don't pile up.
    private async System.Threading.Tasks.Task Tick()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var snapshot = await System.Threading.Tasks.Task.Run(() => ProcessExplorerService.Snapshot());

            // Back on the UI thread after the await — safe to touch the tree/controls.
            // Header summary.
            double totalCpu = snapshot.Sum(p => p.CpuPercent);
            long totalMem = snapshot.Sum(p => p.WorkingSetBytes);
            SumProcValue.Text = snapshot.Count.ToString();
            SumCpuValue.Text = $"{Math.Round(Math.Clamp(totalCpu, 0, 100))}%";
            SumMemValue.Text = ProcessExplorerService.Bytes(totalMem);

            if (_filter.Length > 0) RenderFlat(snapshot);
            else RenderTree(snapshot);
        }
        catch
        {
            // Never let a sampling failure crash the tick / take down the page.
        }
        finally
        {
            _busy = false;
        }
    }

    private bool Matches(ProcEntry e) =>
        e.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)
        || e.Pid.ToString().Contains(_filter)
        || (e.CommandLine?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false)
        || (e.Owner?.Contains(_filter, StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>When filtering, show a flat matching list (no hierarchy) so results aren't hidden.</summary>
    private void RenderFlat(List<ProcEntry> snapshot)
    {
        var matches = snapshot.Where(Matches).OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var present = new HashSet<int>(matches.Select(m => m.Pid));

        for (int i = _roots.Count - 1; i >= 0; i--)
            if (!present.Contains(_roots[i].Pid)) { _index.Remove(_roots[i].Pid); _roots.RemoveAt(i); }

        for (int i = 0; i < matches.Count; i++)
        {
            var e = matches[i];
            if (_index.TryGetValue(e.Pid, out var node) && _roots.Contains(node))
            {
                node.Apply(e);
                node.Children.Clear();
                int cur = _roots.IndexOf(node);
                if (cur != i) _roots.Move(cur, i);
            }
            else
            {
                var n = new ProcNode(e);
                _index[e.Pid] = n;
                _roots.Insert(Math.Min(i, _roots.Count), n);
            }
        }
        while (_roots.Count > matches.Count) { _index.Remove(_roots[^1].Pid); _roots.RemoveAt(_roots.Count - 1); }
    }

    /// <summary>Build/refresh the parent→child tree in place, preserving node identity and expansion.</summary>
    private void RenderTree(List<ProcEntry> snapshot)
    {
        var byPid = snapshot.ToDictionary(e => e.Pid, e => e);
        var alive = new HashSet<int>(byPid.Keys);

        // Drop nodes whose process is gone.
        foreach (var pid in _index.Keys.Where(k => !alive.Contains(k)).ToList())
            _index.Remove(pid);

        // Ensure a node exists for each process; update stats.
        foreach (var e in snapshot)
        {
            if (_index.TryGetValue(e.Pid, out var node)) node.Apply(e);
            else _index[e.Pid] = new ProcNode(e);
        }

        // A process is a root if its parent is missing/itself/PID0.
        bool IsRoot(ProcEntry e) =>
            e.ParentPid == 0 || e.ParentPid == e.Pid || !byPid.ContainsKey(e.ParentPid);

        // Build desired children sets.
        var desiredChildren = new Dictionary<int, List<int>>();
        var rootPids = new List<int>();
        foreach (var e in snapshot.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (IsRoot(e)) { rootPids.Add(e.Pid); continue; }
            if (!desiredChildren.TryGetValue(e.ParentPid, out var l)) desiredChildren[e.ParentPid] = l = new();
            l.Add(e.Pid);
        }

        // Reconcile each node's children collection.
        foreach (var node in _index.Values)
        {
            desiredChildren.TryGetValue(node.Pid, out var want);
            ReconcileChildren(node.Children, want);
        }

        // Reconcile roots.
        ReconcileRoots(rootPids);

        if (_firstFill) _firstFill = false;
    }

    private void ReconcileChildren(ObservableCollection<ProcNode> coll, List<int>? wantPids)
    {
        wantPids ??= new List<int>();
        var want = new HashSet<int>(wantPids);
        for (int i = coll.Count - 1; i >= 0; i--)
            if (!want.Contains(coll[i].Pid)) coll.RemoveAt(i);

        for (int i = 0; i < wantPids.Count; i++)
        {
            if (!_index.TryGetValue(wantPids[i], out var node)) continue;
            int cur = IndexOf(coll, node.Pid);
            if (cur == -1) coll.Insert(Math.Min(i, coll.Count), node);
            else if (cur != i && i < coll.Count) coll.Move(cur, i);
        }
    }

    private void ReconcileRoots(List<int> rootPids)
    {
        var want = new HashSet<int>(rootPids);
        for (int i = _roots.Count - 1; i >= 0; i--)
            if (!want.Contains(_roots[i].Pid)) _roots.RemoveAt(i);

        for (int i = 0; i < rootPids.Count; i++)
        {
            if (!_index.TryGetValue(rootPids[i], out var node)) continue;
            int cur = IndexOf(_roots, node.Pid);
            if (cur == -1) _roots.Insert(Math.Min(i, _roots.Count), node);
            else if (cur != i && i < _roots.Count) _roots.Move(cur, i);
        }
    }

    private static int IndexOf(ObservableCollection<ProcNode> coll, int pid)
    {
        for (int i = 0; i < coll.Count; i++) if (coll[i].Pid == pid) return i;
        return -1;
    }

    // ---------------- Selection ----------------
    private ProcNode? _selected;

    private void Tree_SelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs args)
    {
        _selected = Tree.SelectedItem as ProcNode;
    }

    private void Tree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is ProcNode n) _selected = n;
    }

    private ProcNode? Sel => _selected ?? Tree.SelectedItem as ProcNode;

    // ---------------- Actions ----------------
    private async void End_Click(object sender, RoutedEventArgs e)
    {
        var n = Sel;
        if (n is null) { await Toast(P("Select a process first.", "請先揀一個程序。")); return; }
        if (!ProcessExplorerService.Kill(n.Pid))
            await Toast(P($"Could not end {n.Name} (PID {n.Pid}). Access may be denied.", $"無法結束 {n.Name}（PID {n.Pid}）。可能權限不足。"));
        await Tick();
    }

    private async void EndTree_Click(object sender, RoutedEventArgs e)
    {
        var n = Sel;
        if (n is null) { await Toast(P("Select a process first.", "請先揀一個程序。")); return; }

        var dlg = new ContentDialog
        {
            Title = P("End process tree", "結束程序樹"),
            Content = P($"End \"{n.Name}\" (PID {n.Pid}) and all of its child processes? Children are ended first.",
                        $"結束「{n.Name}」（PID {n.Pid}）以及佢所有子程序？會先結束子程序。"),
            PrimaryButtonText = P("End tree", "結束程序樹"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        int killed = ProcessExplorerService.KillTree(n.Pid);
        await Tick();
        await Toast(P($"Ended {killed} process(es).", $"已結束 {killed} 個程序。"));
    }

    private void Priority_Click(object sender, RoutedEventArgs e)
    {
        var n = Sel;
        if (n is null || sender is not Button b) return;

        var mf = new MenuFlyout();
        foreach (var (label, cls) in Priorities())
        {
            var item = new MenuFlyoutItem { Text = label };
            int pid = n.Pid;
            item.Click += (_, _) => { ProcessExplorerService.SetPriority(pid, cls); _ = Tick(); };
            mf.Items.Add(item);
        }
        mf.ShowAt(b);
    }

    private (string, ProcessPriorityClass)[] Priorities() => new[]
    {
        (P("Realtime", "即時"), ProcessPriorityClass.RealTime),
        (P("High", "高"), ProcessPriorityClass.High),
        (P("Above normal", "高於正常"), ProcessPriorityClass.AboveNormal),
        (P("Normal", "正常"), ProcessPriorityClass.Normal),
        (P("Below normal", "低於正常"), ProcessPriorityClass.BelowNormal),
        (P("Idle (low)", "閒置（低）"), ProcessPriorityClass.Idle),
    };

    private async void Location_Click(object sender, RoutedEventArgs e)
    {
        var n = Sel;
        if (n is null) { await Toast(P("Select a process first.", "請先揀一個程序。")); return; }
        if (string.IsNullOrEmpty(n.Entry.ExecutablePath) || !ProcessExplorerService.OpenFileLocation(n.Entry.ExecutablePath))
            await Toast(P("No file location available for this process.", "呢個程序冇可用嘅檔案位置。"));
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var n = Sel;
        if (n is null || sender is not Button b) return;

        var mf = new MenuFlyout();
        var pidItem = new MenuFlyoutItem { Text = P($"Copy PID ({n.Pid})", $"複製 PID（{n.Pid}）") };
        pidItem.Click += (_, _) => ProcessExplorerService.CopyToClipboard(n.Pid.ToString());
        mf.Items.Add(pidItem);

        var pathItem = new MenuFlyoutItem { Text = P("Copy path", "複製路徑"), IsEnabled = !string.IsNullOrEmpty(n.Entry.ExecutablePath) };
        pathItem.Click += (_, _) => ProcessExplorerService.CopyToClipboard(n.Entry.ExecutablePath);
        mf.Items.Add(pathItem);

        var cmdItem = new MenuFlyoutItem { Text = P("Copy command line", "複製命令列"), IsEnabled = !string.IsNullOrEmpty(n.Entry.CommandLine) };
        cmdItem.Click += (_, _) => ProcessExplorerService.CopyToClipboard(n.Entry.CommandLine);
        mf.Items.Add(cmdItem);

        mf.ShowAt(b);
    }

    private async void Details_Click(object sender, RoutedEventArgs e)
    {
        var n = Sel;
        if (n is null) { await Toast(P("Select a process first.", "請先揀一個程序。")); return; }
        var en = n.Entry;

        var panel = new StackPanel { Spacing = 8 };
        void Row(string label, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) value = "—";
            var g = new Grid { ColumnSpacing = 12 };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var l = new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
            var v = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
            Grid.SetColumn(l, 0); Grid.SetColumn(v, 1);
            g.Children.Add(l); g.Children.Add(v);
            panel.Children.Add(g);
        }

        var prio = ProcessExplorerService.GetPriority(en.Pid);
        Row(P("Name", "名稱"), en.Name);
        Row(P("Description", "描述"), en.Description);
        Row(P("PID", "PID"), en.Pid.ToString());
        Row(P("Parent PID", "父程序 PID"), en.ParentPid.ToString());
        Row(P("Owner", "擁有者"), en.Owner);
        Row(P("CPU", "CPU"), $"{Math.Round(en.CpuPercent, 1)}%");
        Row(P("Working set", "工作集"), ProcessExplorerService.Bytes(en.WorkingSetBytes));
        Row(P("Private bytes", "私用位元組"), ProcessExplorerService.Bytes(en.PrivateBytes));
        Row(P("Threads", "執行緒"), en.ThreadCount.ToString());
        Row(P("Modules", "模組"), en.ModuleCount > 0 ? en.ModuleCount.ToString() : "—");
        Row(P("Priority", "優先權"), prio?.ToString() ?? "—");
        Row(P("Start time", "啟動時間"), en.StartTime is { } st ? st.ToString("yyyy-MM-dd HH:mm:ss") : "—");
        Row(P("Executable", "執行檔"), en.ExecutablePath);
        Row(P("Command line", "命令列"), en.CommandLine);
        if (en.AccessDenied)
            Row(P("Note", "備註"), P("Some details unavailable (access denied).", "部分資料無法取得（權限不足）。"));

        var dlg = new ContentDialog
        {
            Title = P($"Details · {en.Name}", $"詳細資料 · {en.Name}"),
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 460 },
            CloseButtonText = P("Close", "關閉"),
            XamlRoot = XamlRoot,
        };
        await dlg.ShowAsync();
    }

    private async System.Threading.Tasks.Task Toast(string msg)
    {
        var dlg = new ContentDialog
        {
            Content = msg,
            CloseButtonText = P("OK", "好"),
            XamlRoot = XamlRoot,
        };
        await dlg.ShowAsync();
    }
}
