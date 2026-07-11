using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 Proxmox VE 整合 · Native Proxmox VE integration over the REST API (pure managed HttpClient).
/// 連線到 Proxmox 主機，列出所有節點上嘅虛擬機同容器（即時狀態／CPU／記憶體／開機時間），
/// 用工具列控制電源（啟動／關機／停止／重新開機／暫停／繼續），睇選定客體嘅設定（核心／記憶體／開機磁碟／IP）。
/// Lists every QEMU VM &amp; LXC container across nodes with live status, power controls, and a config detail card.
/// API token（DPAPI 加密儲存）或者帳密 ticket 登入；自簽憑證需開啟信任開關。Bilingual, errors never crash.
/// </summary>
public sealed partial class ProxmoxModule : Page
{
    private readonly ProxmoxService _pve = new();
    private readonly ObservableCollection<GuestVM> _rows = new();
    private readonly Dictionary<string, GuestVM> _byKey = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };
    private CancellationTokenSource? _cts;
    private bool _refreshing;
    private GuestVM? _selected;
    private bool _suppress;

    public ProxmoxModule()
    {
        InitializeComponent();
        // Keep the default out of XAML: typed IsOn literals are unreliable here.
        AutoRefreshToggle.IsOn = true;
        GuestList.ItemsSource = _rows;
        _timer.Tick += async (_, _) => await RefreshTick(silent: true);
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private async void OnLoaded(object? s, RoutedEventArgs e)
    {
        _suppress = true;
        HostBox.Text = _pve.Host;
        PortBox.Value = _pve.Port;
        TokenIdBox.Text = _pve.TokenId;
        if (_pve.HasSavedTokenSecret) TokenSecretBox.Password = _pve.SavedTokenSecret;
        UserBox.Text = _pve.User;
        RealmBox.Text = _pve.Realm;
        TrustCertToggle.IsOn = _pve.TrustCert;
        RememberChk.IsChecked = _pve.HasSavedTokenSecret;
        if (_pve.AuthMode == "ticket") TicketModeRadio.IsChecked = true; else TokenModeRadio.IsChecked = true;
        UpdateAuthPanels();
        _suppress = false;

        Render();

        // Auto-connect if we have enough saved (token mode with a remembered secret).
        if (_pve.AuthMode == "token" && !string.IsNullOrWhiteSpace(_pve.Host)
            && !string.IsNullOrWhiteSpace(_pve.TokenId) && _pve.HasSavedTokenSecret)
        {
            await DoConnect(silent: true);
        }
    }

    private void OnUnloaded(object? s, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _timer.Stop();
        _cts?.Cancel();
    }

    private void Render()
    {
        Header.Title = "Proxmox VE · Proxmox VE 虛擬化";
        HeaderBlurb.Text = P("Power your Proxmox VE virtual machines and containers on and off — natively over the REST API. Lists every QEMU VM and LXC container across all nodes with live status, CPU, memory and uptime; start / shutdown / stop / reboot / suspend / resume the selected guest; and inspect its configuration. Everything runs in-app — no browser, no shelling out.",
            "原生經 REST API 開關你嘅 Proxmox VE 虛擬機同容器。列出所有節點上嘅 QEMU 虛擬機同 LXC 容器，顯示即時狀態、CPU、記憶體同開機時間；對選定客體做 啟動／關機／停止／重新開機／暫停／繼續；仲可以查看設定。全部喺 app 內運行 — 唔開瀏覽器、唔外呼程式。");

        ConnHeader.Text = P("Connection · 連線", "連線");
        AuthModeLbl.Text = P("Authentication · 驗證方式", "驗證方式");
        TokenModeRadio.Content = P("API token · API token", "API token");
        TicketModeRadio.Content = P("Username / password · 帳號／密碼", "帳號／密碼");
        TrustCertLbl.Text = P("Trust self-signed certificate (Proxmox uses one by default)", "信任自簽憑證（Proxmox 預設用自簽憑證）");
        RememberChk.Content = P("Remember token (encrypted) · 記住 token（已加密）", "記住 token（已加密）");
        ConnectBtn.Content = P("Connect · 連線", "連線");
        DisconnectBtn.Content = P("Disconnect · 中斷", "中斷");

        StartLbl.Text = P("Start", "啟動");
        ShutdownLbl.Text = P("Shutdown", "關機");
        StopLbl.Text = P("Stop", "停止");
        RebootLbl.Text = P("Reboot", "重新開機");
        SuspendLbl.Text = P("Suspend", "暫停");
        ResumeLbl.Text = P("Resume", "繼續");
        AutoRefreshLbl.Text = P("Auto", "自動");

        ColName.Text = P("Guest · 客體", "客體");
        ColType.Text = P("Type · 類型", "類型");
        ColStatus.Text = P("Status · 狀態", "狀態");
        ColCpu.Text = P("CPU", "CPU");
        ColMem.Text = P("Memory · 記憶體", "記憶體");
        ColUptime.Text = P("Uptime · 開機時間", "開機時間");

        EmptyText.Text = _pve.Connected
            ? P("No virtual machines or containers found on any node.", "喺所有節點都搵唔到虛擬機或容器。")
            : P("Connect to a Proxmox VE host above to list its VMs and containers.", "喺上面連去 Proxmox VE 主機就會列出佢嘅虛擬機同容器。");

        UpdateConnUi();
        UpdateActionButtons();
        if (_selected is not null) RenderSelected();
    }

    private void AuthMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        UpdateAuthPanels();
    }

    private void UpdateAuthPanels()
    {
        bool token = TokenModeRadio.IsChecked == true;
        TokenPanel.Visibility = token ? Visibility.Visible : Visibility.Collapsed;
        TicketPanel.Visibility = token ? Visibility.Collapsed : Visibility.Visible;
        RememberChk.Visibility = token ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── connect / disconnect ──────────────────────────────────────────────────────

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        string authMode = TokenModeRadio.IsChecked == true ? "token" : "ticket";
        _pve.SaveConnection(
            HostBox.Text,
            (int)(double.IsNaN(PortBox.Value) ? 8006 : PortBox.Value),
            authMode,
            TokenIdBox.Text,
            TokenSecretBox.Password,
            UserBox.Text,
            RealmBox.Text,
            TrustCertToggle.IsOn,
            RememberChk.IsChecked == true);
        await DoConnect(silent: false);
    }

    private async Task DoConnect(bool silent)
    {
        ConnBusy.IsActive = true;
        ConnectBtn.IsEnabled = false;
        try
        {
            var r = await _pve.Connect(TokenSecretBox.Password, PassBox.Password);
            if (!r.Ok)
            {
                _timer.Stop();
                _rows.Clear(); _byKey.Clear();
                UpdateConnUi();
                UpdateListVisibility();
                ShowConnState(r.State, r.Body);
                return;
            }
            ShowConnState(PveConnState.Connected, "");
            ConnExpander.IsExpanded = false;
            await RefreshTick(silent: true);
            if (AutoRefreshToggle.IsOn) _timer.Start();
        }
        finally
        {
            ConnBusy.IsActive = false;
            ConnectBtn.IsEnabled = true;
            UpdateConnUi();
        }
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _pve.Disconnect();
        _rows.Clear(); _byKey.Clear();
        _selected = null;
        DetailCard.Visibility = Visibility.Collapsed;
        UpdateConnUi();
        UpdateListVisibility();
        UpdateActionButtons();
        ShowConn(InfoBarSeverity.Informational, P("Disconnected · 已中斷", "已中斷"), "");
    }

    private void UpdateConnUi()
    {
        bool on = _pve.Connected;
        ConnState.Text = on ? P("Connected · 已連線", "已連線") : P("Not connected · 未連線", "未連線");
        ConnPill.Background = on
            ? new SolidColorBrush(Color.FromArgb(40, 0x6C, 0xCB, 0x5A))
            : (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
        DisconnectBtn.IsEnabled = on;
        RefreshBtn.IsEnabled = on;
        AutoRefreshToggle.IsEnabled = on;
    }

    private void ShowConnState(PveConnState state, string detail)
    {
        switch (state)
        {
            case PveConnState.Connected:
                ShowConn(InfoBarSeverity.Success, P("Connected to Proxmox VE.", "已連線到 Proxmox VE。"), "");
                break;
            case PveConnState.Unauthorized:
                ShowConn(InfoBarSeverity.Error,
                    P("Unauthorized — check the API token or username/password and realm.",
                      "未授權 — 請檢查 API token 或者帳號／密碼同領域。"), detail);
                break;
            case PveConnState.CertUntrusted:
                ShowConn(InfoBarSeverity.Error,
                    P("TLS certificate not trusted. Proxmox uses a self-signed certificate by default — turn on \"Trust self-signed certificate\" above, then reconnect.",
                      "TLS 憑證未受信任。Proxmox 預設用自簽憑證 — 請喺上面開啟「信任自簽憑證」再連線。"), detail);
                break;
            case PveConnState.Unreachable:
            default:
                ShowConn(InfoBarSeverity.Error,
                    P($"Could not reach {_pve.Host}:{_pve.Port}. Is the host up and the API reachable on port 8006?",
                      $"連唔到 {_pve.Host}:{_pve.Port}。主機有冇開住、8006 埠通唔通到 API？"), detail);
                break;
        }
    }

    // ── refresh ───────────────────────────────────────────────────────────────────

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshTick(silent: false);

    private async Task RefreshTick(bool silent)
    {
        if (_refreshing || !_pve.Connected) return;
        _refreshing = true;
        Busy.IsActive = true;
        _cts ??= new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            var guests = await _pve.GetAllGuests(ct);
            MergeRows(guests);
            UpdateListVisibility();
            SelectionInfo.Text = P($"{_rows.Count} guest(s)", $"{_rows.Count} 個客體");
            if (_selected is not null) RenderSelected();
            UpdateActionButtons();
        }
        catch (Exception ex)
        {
            if (!silent) ShowConn(InfoBarSeverity.Error, P("Refresh failed", "重新整理失敗"), ex.Message);
        }
        finally
        {
            _refreshing = false;
            Busy.IsActive = false;
        }
    }

    /// <summary>Diff-merge so selection and scroll survive periodic refresh.</summary>
    private void MergeRows(List<PveGuest> snapshot)
    {
        var seen = new HashSet<string>();
        foreach (var g in snapshot)
        {
            seen.Add(g.Key);
            if (_byKey.TryGetValue(g.Key, out var vm)) vm.Update(g);
            else
            {
                var nv = new GuestVM(g);
                _byKey[g.Key] = nv;
                _rows.Add(nv);
            }
        }
        for (int i = _rows.Count - 1; i >= 0; i--)
        {
            if (!seen.Contains(_rows[i].Key))
            {
                if (_selected == _rows[i]) { _selected = null; DetailCard.Visibility = Visibility.Collapsed; }
                _byKey.Remove(_rows[i].Key);
                _rows.RemoveAt(i);
            }
        }
    }

    private void UpdateListVisibility()
    {
        bool empty = _rows.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        GuestList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── selection / detail ──────────────────────────────────────────────────────────

    private async void GuestList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = GuestList.SelectedItem as GuestVM;
        UpdateActionButtons();
        if (_selected is null) { DetailCard.Visibility = Visibility.Collapsed; return; }
        DetailCard.Visibility = Visibility.Visible;
        RenderSelected();
        await LoadConfig();
    }

    private void RenderSelected()
    {
        if (_selected is null) return;
        var g = _selected.Guest;
        var (en, zh) = ProxmoxService.StatusLabel(g.Status);
        SelName.Text = $"{g.Name}  (#{g.VmId})";
        SelState.Text = P(
            $"{(g.IsContainer ? "Container" : "VM")} on node {g.Node} · {P(en, zh)} · {g.MaxCpu} vCPU · {ProxmoxService.HumanSize(g.MaxMemBytes)} RAM",
            $"節點 {g.Node} 上嘅{(g.IsContainer ? "容器" : "虛擬機")} · {P(en, zh)} · {g.MaxCpu} vCPU · {ProxmoxService.HumanSize(g.MaxMemBytes)} 記憶體");
    }

    private async void DetailRefresh_Click(object sender, RoutedEventArgs e) => await LoadConfig();

    private async Task LoadConfig()
    {
        if (_selected is null) return;
        var g = _selected.Guest;
        ConfigBox.Text = P("Loading…", "載入緊…");
        try
        {
            var cfg = await _pve.GetConfig(g);
            var lines = new List<string>();
            // Surface the most useful keys first.
            foreach (var key in new[] { "cores", "sockets", "cpu", "memory", "balloon", "boot", "bootdisk", "ostype", "arch", "hostname", "rootfs", "scsi0", "virtio0", "ide0", "sata0", "net0", "net1" })
                if (cfg.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                    lines.Add($"{key,-10} {v}");
            // Then any remaining keys we didn't already print.
            foreach (var kv in cfg.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                if (!lines.Any(l => l.StartsWith(kv.Key + " ", StringComparison.Ordinal) || l.StartsWith($"{kv.Key,-10}".TrimEnd())) && !string.IsNullOrEmpty(kv.Value))
                    if (!new[] { "cores", "sockets", "cpu", "memory", "balloon", "boot", "bootdisk", "ostype", "arch", "hostname", "rootfs", "scsi0", "virtio0", "ide0", "sata0", "net0", "net1" }.Contains(kv.Key))
                        lines.Add($"{kv.Key,-10} {kv.Value}");

            var ip = await _pve.TryGetGuestIp(g);
            if (!string.IsNullOrEmpty(ip)) lines.Insert(0, P($"IP         {ip}", $"IP         {ip}"));

            ConfigBox.Text = lines.Count == 0 ? P("(no configuration available)", "（無設定資料）") : string.Join("\n", lines);
        }
        catch (Exception ex) { ConfigBox.Text = ex.Message; }
    }

    private void UpdateActionButtons()
    {
        var g = _selected?.Guest;
        bool any = g is not null && _pve.Connected;
        bool running = any && g!.IsRunning;
        bool stopped = any && !g!.IsRunning;
        bool suspended = any && string.Equals(g!.Status, "paused", StringComparison.OrdinalIgnoreCase);

        StartBtn.IsEnabled = stopped;
        ShutdownBtn.IsEnabled = running;
        StopBtn.IsEnabled = running || suspended;
        RebootBtn.IsEnabled = running;
        SuspendBtn.IsEnabled = running;
        ResumeBtn.IsEnabled = suspended;
    }

    // ── power actions ───────────────────────────────────────────────────────────────

    private async Task DoPower(Func<PveGuest, CancellationToken, Task<PveResult>> op, string en, string zh, bool confirm = false,
        string? confirmBodyEn = null, string? confirmBodyZh = null)
    {
        var g = _selected?.Guest;
        if (g is null) return;
        if (confirm && !await Confirm(P(en + "?", zh + "？"),
            P(confirmBodyEn ?? "", confirmBodyZh ?? ""), P(en, zh))) return;

        Busy.IsActive = true;
        try
        {
            var r = await op(g, CancellationToken.None);
            if (r.Ok) ShowConn(InfoBarSeverity.Success, P($"{en}: {g.Name}", $"{zh}：{g.Name}"), "");
            else ShowConnState(r.State == PveConnState.Connected ? PveConnState.Unreachable : r.State,
                $"{P(en, zh)}: {r.Body}");
        }
        catch (Exception ex) { ShowConn(InfoBarSeverity.Error, P(en, zh), ex.Message); }
        finally { Busy.IsActive = false; }

        // Status takes a moment server-side; refresh shortly after.
        await Task.Delay(900);
        await RefreshTick(silent: true);
    }

    private async void Start_Click(object s, RoutedEventArgs e) => await DoPower((g, ct) => _pve.Start(g, ct), "Start", "啟動");
    private async void Reboot_Click(object s, RoutedEventArgs e) => await DoPower((g, ct) => _pve.Reboot(g, ct), "Reboot", "重新開機");
    private async void Suspend_Click(object s, RoutedEventArgs e) => await DoPower((g, ct) => _pve.Suspend(g, ct), "Suspend", "暫停");
    private async void Resume_Click(object s, RoutedEventArgs e) => await DoPower((g, ct) => _pve.Resume(g, ct), "Resume", "繼續");

    private async void Shutdown_Click(object s, RoutedEventArgs e) => await DoPower((g, ct) => _pve.Shutdown(g, ct), "Shutdown", "關機",
        confirm: true,
        confirmBodyEn: "Send a graceful (ACPI) shutdown to the guest. The OS shuts down cleanly.",
        confirmBodyZh: "向客體發送正常（ACPI）關機。作業系統會乾淨咁關閉。");

    private async void Stop_Click(object s, RoutedEventArgs e) => await DoPower((g, ct) => _pve.Stop(g, ct), "Stop", "停止",
        confirm: true,
        confirmBodyEn: "Hard stop — like pulling the power. Unsaved data in the guest may be lost.",
        confirmBodyZh: "強制停止 — 等於拔電源。客體未儲存嘅資料可能會遺失。");

    // ── auto-refresh toggle ──────────────────────────────────────────────────────────

    private void AutoRefresh_Toggled(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshToggle.IsOn && _pve.Connected) _timer.Start();
        else _timer.Stop();
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────

    private async Task<bool> Confirm(string title, string body, string primary)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = title,
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ShowConn(InfoBarSeverity sev, string title, string detail)
    {
        ConnResult.Severity = sev;
        ConnResult.Title = title;
        ConnResult.Message = detail ?? "";
        ConnResult.IsOpen = true;
    }
}

/// <summary>
/// 一行客體嘅可觀察檢視模型 · Observable view-model for one guest row; updated in place each tick so the
/// ListView keeps selection and scroll position across refreshes.
/// </summary>
public sealed class GuestVM : INotifyPropertyChanged
{
    public string Key { get; }
    private PveGuest _g;

    public GuestVM(PveGuest g) { Key = g.Key; _g = g; }

    public PveGuest Guest => _g;

    public void Update(PveGuest g)
    {
        _g = g;
        foreach (var p in new[]
        {
            nameof(Name), nameof(SubLine), nameof(TypeText), nameof(StatusText),
            nameof(StatusDotBrush), nameof(StatusBackground), nameof(StatusForeground),
            nameof(CpuText), nameof(MemText), nameof(UptimeText),
        })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public string Name => string.IsNullOrEmpty(_g.Name) ? $"#{_g.VmId}" : _g.Name;
    public string SubLine => $"{_g.Node} · #{_g.VmId}{(_g.Template ? " · template" : "")}{(string.IsNullOrEmpty(_g.Lock) ? "" : " · 🔒 " + _g.Lock)}";
    public string TypeText => _g.IsContainer ? Loc.I.Pick("Container", "容器") : Loc.I.Pick("VM", "虛擬機");

    public string StatusText { get { var (en, zh) = ProxmoxService.StatusLabel(_g.Status); return Loc.I.Pick(en, zh); } }

    public Brush StatusDotBrush => new SolidColorBrush(StatusColor());
    public Brush StatusForeground => new SolidColorBrush(StatusColor());
    public Brush StatusBackground
    {
        get
        {
            var c = StatusColor();
            return new SolidColorBrush(Color.FromArgb(36, c.R, c.G, c.B));
        }
    }

    private Color StatusColor() => _g.Status.ToLowerInvariant() switch
    {
        "running" => Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50),  // green
        "paused" or "suspended" => Color.FromArgb(0xFF, 0xE0, 0xA0, 0x30), // amber
        _ => Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A),          // grey (stopped)
    };

    public string CpuText => _g.IsRunning && _g.MaxCpu > 0
        ? $"{_g.CpuFraction * 100:0}% · {_g.MaxCpu}"
        : (_g.MaxCpu > 0 ? $"{_g.MaxCpu} vCPU" : "—");

    public string MemText => _g.MaxMemBytes > 0
        ? (_g.IsRunning
            ? $"{ProxmoxService.HumanSize(_g.MemBytes)} / {ProxmoxService.HumanSize(_g.MaxMemBytes)}"
            : ProxmoxService.HumanSize(_g.MaxMemBytes))
        : "—";

    public string UptimeText => _g.IsRunning ? ProxmoxService.HumanUptime(_g.UptimeSec) : "—";

    public event PropertyChangedEventHandler? PropertyChanged;
}
