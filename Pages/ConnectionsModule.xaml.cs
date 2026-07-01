using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內連線檢視 · In-app active-connections viewer (TCPView-style) over iphlpapi — live TCP/UDP
/// sockets with owning process, filter, drop-a-connection and end-process. No redirect. Bilingual.
/// </summary>
public sealed partial class ConnectionsModule : Page
{
    public sealed class ConnView : INotifyPropertyChanged
    {
        public string Key { get; }
        public string Proto { get; }
        public string Local { get; }
        public string Remote { get; }
        public string ProcPid { get; }
        private string _state = "";
        public string State { get => _state; private set { if (_state != value) { _state = value; OnPC(); } } }
        private Visibility _killVis;
        public Visibility KillVis { get => _killVis; private set { if (_killVis != value) { _killVis = value; OnPC(); } } }
        public ConnRow Row { get; private set; }

        public ConnView(ConnRow r)
        {
            Key = r.Key; Proto = r.Proto; Local = r.Local; Remote = r.Remote;
            ProcPid = $"{r.Process} · {r.Pid}";
            Row = r; _state = r.State; _killVis = r.CanKill ? Visibility.Visible : Visibility.Collapsed;
        }
        public void Update(ConnRow r) { State = r.State; KillVis = r.CanKill ? Visibility.Visible : Visibility.Collapsed; Row = r; }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPC([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly ObservableCollection<ConnView> _rows = new();
    private bool _rendering;
    private bool _busy;
    private string _filter = "";

    public ConnectionsModule()
    {
        InitializeComponent();
        List.ItemsSource = _rows;
        _timer.Tick += (_, _) => _ = Refresh();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { _timer.Stop(); Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? s, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        _rendering = true;
        Header.Title = "Connections · 連線";
        HeaderBlurb.Text = P("Every live TCP/UDP socket and the app that owns it. Drop a single connection or end the process — no resmon needed.",
            "每一條即時 TCP/UDP 連線同擁有佢嘅程式。可以單獨切斷一條連線或者結束程序 — 唔使開資源監視器。");
        FilterBox.PlaceholderText = P("Filter by process, address or port…", "用程序、位址或連接埠篩選…");
        AutoSwitch.OnContent = P("Auto", "自動");
        AutoSwitch.OffContent = P("Auto", "自動");
        ColProto.Text = P("Proto", "協定");
        ColLocal.Text = P("Local address", "本機位址");
        ColRemote.Text = P("Remote address", "遠端位址");
        ColState.Text = P("State", "狀態");
        ColProc.Text = _rows.Count == 0
            ? P("Process · PID — not scanned", "程序 · PID — 未掃描")
            : P($"Process · PID — {_rows.Count} shown", $"程序 · PID — 顯示 {_rows.Count} 條");

        int sel = ProtoBox.SelectedIndex < 0 ? 0 : ProtoBox.SelectedIndex;
        ProtoBox.Items.Clear();
        ProtoBox.Items.Add(P("All", "全部"));
        ProtoBox.Items.Add("TCP");
        ProtoBox.Items.Add("UDP");
        ProtoBox.SelectedIndex = sel;
        _rendering = false;
    }

    private void Auto_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (AutoSwitch.IsOn)
        {
            _ = Refresh();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = Refresh();

    private void Filter_Changed(object sender, object e)
    {
        if (_rendering) return;
        _filter = (FilterBox.Text ?? "").Trim();
        if (IsLoaded) _ = Refresh();
    }

    // ConnectionsService.Snapshot walks the iphlpapi TCP/UDP tables and resolves owning
    // processes — heavy enough to freeze the UI. Run it off the UI thread with a re-entrancy guard.
    private async System.Threading.Tasks.Task Refresh()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            bool tcp = ProtoBox.SelectedIndex != 2; // not UDP-only
            bool udp = ProtoBox.SelectedIndex != 1; // not TCP-only
            string filter = _filter;

            var snap = await System.Threading.Tasks.Task.Run(() =>
            {
                List<ConnRow> list;
                try { list = ConnectionsService.Snapshot(tcp, udp); }
                catch { return null; }

                if (filter.Length > 0)
                {
                    var f = filter;
                    list = list.Where(r =>
                        r.Process.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        r.Local.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        r.Remote.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        r.State.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        r.Pid.ToString().Contains(f)).ToList();
                }

                return list
                    .OrderBy(r => r.Process, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.Proto, StringComparer.Ordinal)
                    .ThenBy(r => r.Local, StringComparer.Ordinal)
                    .ToList();
            });

            // Back on the UI thread after the await.
            if (snap is null) return; // snapshot failed; leave the current view untouched

            Reconcile(snap);
            ColProc.Text = P($"Process · PID — {snap.Count} shown", $"程序 · PID — 顯示 {snap.Count} 條");
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

    private void Reconcile(List<ConnRow> snap)
    {
        var present = new HashSet<string>();
        foreach (var r in snap) present.Add(r.Key);
        for (int i = _rows.Count - 1; i >= 0; i--)
            if (!present.Contains(_rows[i].Key)) _rows.RemoveAt(i);

        for (int i = 0; i < snap.Count; i++)
        {
            var r = snap[i];
            int idx = -1;
            for (int j = i; j < _rows.Count; j++)
                if (_rows[j].Key == r.Key) { idx = j; break; }
            if (idx == -1) { _rows.Insert(i, new ConnView(r)); }
            else { _rows[idx].Update(r); if (idx != i) _rows.Move(idx, i); }
        }
        while (_rows.Count > snap.Count) _rows.RemoveAt(_rows.Count - 1);
    }

    private void Kill_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ConnView v) return;
        uint r = ConnectionsService.KillTcp(v.Row);
        if (r == 0)
        {
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Connection dropped", "已切斷連線");
            ResultBar.Message = $"{v.Local} → {v.Remote}";
        }
        else
        {
            ResultBar.Severity = InfoBarSeverity.Warning;
            ResultBar.Title = P("Could not drop it", "切唔到");
            ResultBar.Message = AdminHelper.IsElevated
                ? P($"iphlpapi returned {r}; the socket may have already closed.", $"iphlpapi 回傳 {r}；條連線可能已經閂咗。")
                : P("Dropping a connection needs administrator rights — relaunch WinForge as admin.", "切斷連線要管理員權限 — 請以管理員身分重開 WinForge。");
        }
        ResultBar.IsOpen = true;
        _ = Refresh();
    }

    private void EndProc_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ConnView v) return;
        if (v.Row.Pid <= 4) return; // never try to kill System / Idle
        bool ok = SystemMonitor.Kill(v.Row.Pid);
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Title = ok ? P("Process ended", "已結束程序") : P("Could not end it", "結束唔到");
        ResultBar.Message = ok ? v.ProcPid : P("Access denied — try running WinForge as admin.", "拒絕存取 — 試吓以管理員身分執行。");
        ResultBar.IsOpen = true;
        _ = Refresh();
    }
}
