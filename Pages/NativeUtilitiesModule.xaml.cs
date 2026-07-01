using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生工具（System32 P/Invoke）· Native utilities — a suite of in-app tools each built on a documented
/// Win32 API: saved/nearby Wi-Fi (wlanapi), SMB shares+sessions (netapi32), monitor brightness (dxva2),
/// user sessions (wtsapi32), certificates (crypt32), live disk/GPU counters (pdh), process modules
/// (psapi) and paired Bluetooth (bluetoothapis). All in-app, bilingual, no redirect.
/// </summary>
public sealed partial class NativeUtilitiesModule : Page
{
    // ---- view-models -------------------------------------------------------
    public sealed class WifiSavedVm
    {
        public string Name { get; init; } = "";
        public string AuthEnc { get; init; } = "";
        public string KeyShown { get; init; } = "";
        public string RawKey { get; init; } = "";
    }
    public sealed class WifiScanVm
    {
        public string Ssid { get; init; } = "";
        public string Security { get; init; } = "";
        public int SignalQuality { get; init; }
        public string SignalText { get; init; } = "";
        public string ProfileBadge { get; init; } = "";
    }
    public sealed class ShareVm { public string Name { get; init; } = ""; public string Path { get; init; } = ""; public string Type { get; init; } = ""; }
    public sealed class SessionRowVm { public string Computer { get; init; } = ""; public string User { get; init; } = ""; public string Timing { get; init; } = ""; }
    public sealed class UserSessionVm
    {
        public string User { get; init; } = "";
        public string Station { get; init; } = "";
        public string State { get; init; } = "";
        public string IdTag { get; init; } = "";
        public Visibility ActionVis { get; init; }
        public uint SessionId { get; init; }
    }
    public sealed class CertVm
    {
        public string Subject { get; init; } = "";
        public string Issuer { get; init; } = "";
        public string Thumbprint { get; init; } = "";
        public string Validity { get; init; } = "";
        public Brush ValidityBrush { get; init; } = new SolidColorBrush(Colors.Gray);
    }
    public sealed class ModuleVm { public string Name { get; init; } = ""; public string Path { get; init; } = ""; public string SizeText { get; init; } = ""; }
    public sealed class BtVm
    {
        public string Name { get; init; } = "";
        public string Status { get; init; } = "";
        public string LastSeenText { get; init; } = "";
        public BtDevice Device { get; init; } = null!;
    }
    private sealed class ProcItem { public int Pid { get; init; } public string Display { get; init; } = ""; public override string ToString() => Display; }

    // ---- collections -------------------------------------------------------
    private readonly ObservableCollection<WifiSavedVm> _wifiSaved = new();
    private readonly ObservableCollection<WifiScanVm> _wifiScan = new();
    private readonly ObservableCollection<ShareVm> _shares = new();
    private readonly ObservableCollection<SessionRowVm> _smbSessions = new();
    private readonly ObservableCollection<UserSessionVm> _userSessions = new();
    private readonly ObservableCollection<CertVm> _certs = new();
    private readonly ObservableCollection<ModuleVm> _modules = new();
    private readonly ObservableCollection<BtVm> _bt = new();

    private List<MonitorBrightness> _monitors = new();
    private PdhCounters? _pdh;
    private readonly DispatcherTimer _countersTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _busy; // reentrancy guard for the (async) counters tick

    public NativeUtilitiesModule()
    {
        InitializeComponent();
        WifiSavedList.ItemsSource = _wifiSaved;
        WifiScanList.ItemsSource = _wifiScan;
        SmbSharesList.ItemsSource = _shares;
        SmbSessionsList.ItemsSource = _smbSessions;
        SessList.ItemsSource = _userSessions;
        CertList.ItemsSource = _certs;
        ModList.ItemsSource = _modules;
        BtList.ItemsSource = _bt;

        _countersTimer.Tick += CountersTimer_Tick;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); WifiSaved_Refresh(null!, null!); };
        Unloaded += (_, _) => Cleanup();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        Header.Title = "Native Utilities · 原生工具";
        HeaderBlurb.Text = P("In-app tools built straight on documented Windows APIs — no external tools, no redirects.",
            "直接建喺有文件記載嘅 Windows API 之上嘅應用程式內工具 — 唔使外部工具、唔會跳走。");

        PiWifiSaved.Header = P("Saved Wi-Fi", "已儲存 Wi-Fi");
        PiWifiScan.Header = P("Nearby Wi-Fi", "附近 Wi-Fi");
        PiSmb.Header = P("SMB shares", "SMB 共享");
        PiBrightness.Header = P("Brightness", "亮度");
        PiSessions.Header = P("User sessions", "使用者工作階段");
        PiCerts.Header = P("Certificates", "憑證");
        PiCounters.Header = P("Live counters", "即時計數");
        PiModules.Header = P("Process modules", "程序模組");
        PiBluetooth.Header = P("Bluetooth", "藍牙");

        string refresh = P("Refresh", "重新整理");
        WifiSavedRefreshTxt.Text = refresh; SmbRefreshTxt.Text = refresh; BrightRefreshTxt.Text = refresh;
        SessRefreshTxt.Text = refresh; CertRefreshTxt.Text = refresh; BtRefreshTxt.Text = refresh;
        WifiScanTxt.Text = P("Scan", "掃描");

        BrightHint.Text = P("DDC/CI monitors only (external displays). Internal laptop panels are not controllable here.",
            "只限 DDC/CI 顯示器（外接螢幕）。手提電腦內建螢幕喺度控制唔到。");
        CountersHint.Text = P("Live disk & GPU counters via PDH", "用 PDH 嘅即時磁碟同 GPU 計數");
        CountersSwitch.OnContent = P("Live", "即時");
        CountersSwitch.OffContent = P("Paused", "暫停");
        SmbSharesHdr.Text = P("Published shares", "已發佈共享");
        SmbSessionsHdr.Text = P("Inbound sessions", "連入工作階段");

        if (CertStoreBox.Items.Count == 0)
        {
            CertStoreBox.Items.Add("Personal · 個人 (My)");
            CertStoreBox.Items.Add("Trusted Roots · 受信任根 (Root)");
            CertStoreBox.Items.Add("Intermediate · 中繼 (CA)");
            CertStoreBox.SelectedIndex = 0;
        }
    }

    private void Section_Changed(object sender, SelectionChangedEventArgs e)
    {
        var sel = Sections.SelectedItem as PivotItem;
        // Stop the live-counter timer whenever we leave that tab.
        if (!ReferenceEquals(sel, PiCounters)) _countersTimer.Stop();

        if (ReferenceEquals(sel, PiWifiSaved) && _wifiSaved.Count == 0) WifiSaved_Refresh(null!, null!);
        else if (ReferenceEquals(sel, PiWifiScan) && _wifiScan.Count == 0) WifiScan_Refresh(null!, null!);
        else if (ReferenceEquals(sel, PiSmb) && _shares.Count == 0 && _smbSessions.Count == 0) Smb_Refresh(null!, null!);
        else if (ReferenceEquals(sel, PiBrightness) && _monitors.Count == 0) Bright_Refresh(null!, null!);
        else if (ReferenceEquals(sel, PiSessions) && _userSessions.Count == 0) Sess_Refresh(null!, null!);
        else if (ReferenceEquals(sel, PiCerts) && _certs.Count == 0) LoadCerts();
        else if (ReferenceEquals(sel, PiModules)) { if (ProcBox.Items.Count == 0) PopulateProcesses(); }
        else if (ReferenceEquals(sel, PiBluetooth) && _bt.Count == 0) Bt_Refresh(null!, null!);
        else if (ReferenceEquals(sel, PiCounters)) StartCounters();
    }

    private void Info(bool ok, string okEn, string okZh, string warnEn, string warnZh)
    {
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Title = ok ? P(okEn, okZh) : P(warnEn, warnZh);
        ResultBar.Message = "";
        ResultBar.IsOpen = true;
    }

    // ===== 1. Saved Wi-Fi ===================================================
    private void WifiSaved_Refresh(object sender, RoutedEventArgs e)
    {
        _wifiSaved.Clear();
        List<WifiProfile> list;
        try { list = WifiService.SavedProfiles(); }
        catch { WifiSavedCount.Text = P("Wi-Fi unavailable", "Wi-Fi 唔可用"); return; }
        foreach (var p in list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            _wifiSaved.Add(new WifiSavedVm
            {
                Name = p.Name,
                AuthEnc = $"{p.Authentication}/{p.Encryption}",
                KeyShown = p.HasKey ? p.Key : P("(no password)", "（無密碼）"),
                RawKey = p.Key,
            });
        }
        WifiSavedCount.Text = P($"{_wifiSaved.Count} profiles", $"{_wifiSaved.Count} 個設定檔");
    }

    private void WifiCopy_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WifiSavedVm v || v.RawKey.Length == 0)
        { Info(false, "", "", "No password to copy", "冇密碼可以複製"); return; }
        var dp = new DataPackage();
        dp.SetText(v.RawKey);
        Clipboard.SetContent(dp);
        Info(true, "Password copied", "已複製密碼", "", "");
    }

    private void WifiDelete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not WifiSavedVm v) return;
        bool ok = false;
        try { ok = WifiService.DeleteProfile(v.Name); } catch { }
        Info(ok, "Network forgotten", "已移除網絡", "Could not remove it", "移除唔到");
        if (ok) WifiSaved_Refresh(null!, null!);
    }

    // ===== 2. Nearby Wi-Fi =================================================
    private void WifiScan_Refresh(object sender, RoutedEventArgs e)
    {
        _wifiScan.Clear();
        List<WifiNetwork> list;
        try { list = WifiService.ScanNearby(); }
        catch { WifiScanCount.Text = P("Scan unavailable", "掃描唔可用"); return; }
        foreach (var n in list.OrderByDescending(n => n.SignalQuality))
        {
            _wifiScan.Add(new WifiScanVm
            {
                Ssid = n.Ssid,
                Security = $"{n.Auth} · {n.Cipher}",
                SignalQuality = n.SignalQuality,
                SignalText = $"{n.SignalQuality}%",
                ProfileBadge = n.HasProfile ? P("saved", "已儲存") : "",
            });
        }
        WifiScanCount.Text = P($"{_wifiScan.Count} networks", $"{_wifiScan.Count} 個網絡");
    }

    // ===== 3. SMB =========================================================
    private void Smb_Refresh(object sender, RoutedEventArgs e)
    {
        _shares.Clear();
        _smbSessions.Clear();
        try
        {
            foreach (var s in SmbService.Shares())
                _shares.Add(new ShareVm { Name = s.Name, Path = s.Path, Type = s.Type });
        }
        catch { }
        try
        {
            foreach (var s in SmbService.Sessions())
                _smbSessions.Add(new SessionRowVm
                {
                    Computer = s.Computer,
                    User = s.User,
                    Timing = P($"{s.SecondsActive}s active, {s.SecondsIdle}s idle", $"活躍 {s.SecondsActive} 秒、閒置 {s.SecondsIdle} 秒"),
                });
        }
        catch { }
        SmbSharesHdr.Text = P($"Published shares — {_shares.Count}", $"已發佈共享 — {_shares.Count}");
        SmbSessionsHdr.Text = _smbSessions.Count > 0
            ? P($"Inbound sessions — {_smbSessions.Count}", $"連入工作階段 — {_smbSessions.Count}")
            : P("Inbound sessions — none (admin needed to list)", "連入工作階段 — 冇（列出需要管理員）");
    }

    // ===== 4. Brightness ==================================================
    private void Bright_Refresh(object sender, RoutedEventArgs e)
    {
        ReleaseMonitors();
        BrightPanel.Children.Clear();
        try { _monitors = MonitorService.Enumerate(); } catch { _monitors = new(); }
        if (_monitors.Count == 0)
        {
            BrightPanel.Children.Add(new TextBlock
            {
                Text = P("No DDC/CI-capable monitors found.", "搵唔到支援 DDC/CI 嘅顯示器。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            return;
        }
        foreach (var m in _monitors)
        {
            var card = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
            };
            var sp = new StackPanel { Spacing = 6 };
            sp.Children.Add(new TextBlock { Text = m.Description, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            var valueLabel = new TextBlock
            {
                Text = $"{m.Current}",
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            var slider = new Slider { Minimum = m.Min, Maximum = m.Max, Value = m.Current, StepFrequency = 1 };
            var captured = m;
            slider.ValueChanged += (_, args) =>
            {
                uint v = (uint)Math.Round(args.NewValue);
                valueLabel.Text = $"{v}";
                try { MonitorService.SetBrightness(captured, v); } catch { }
            };
            sp.Children.Add(slider);
            sp.Children.Add(valueLabel);
            card.Child = sp;
            BrightPanel.Children.Add(card);
        }
    }

    // ===== 5. User sessions ===============================================
    private void Sess_Refresh(object sender, RoutedEventArgs e)
    {
        _userSessions.Clear();
        List<UserSession> list;
        try { list = SessionService.Enumerate(); } catch { return; }
        foreach (var s in list)
        {
            _userSessions.Add(new UserSessionVm
            {
                User = s.IsCurrent ? P($"{s.User} (you)", $"{s.User}（你）") : s.User,
                Station = s.Station,
                State = s.State,
                IdTag = P($"id {s.SessionId}", $"編號 {s.SessionId}"),
                SessionId = s.SessionId,
                ActionVis = s.IsCurrent ? Visibility.Collapsed : Visibility.Visible, // never log yourself off here
            });
        }
    }

    private void SessLogoff_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not UserSessionVm v) return;
        bool ok = false;
        try { ok = SessionService.Logoff(v.SessionId); } catch { }
        Info(ok, "User logged off", "已登出使用者", AdminHelper.IsElevated ? "Could not log off" : "Logging off others needs admin", AdminHelper.IsElevated ? "登出唔到" : "登出其他人需要管理員");
        if (ok) Sess_Refresh(null!, null!);
    }

    private void SessDisconnect_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not UserSessionVm v) return;
        bool ok = false;
        try { ok = SessionService.Disconnect(v.SessionId); } catch { }
        Info(ok, "Session disconnected", "已中斷工作階段", AdminHelper.IsElevated ? "Could not disconnect" : "Disconnecting others needs admin", AdminHelper.IsElevated ? "中斷唔到" : "中斷其他人需要管理員");
        if (ok) Sess_Refresh(null!, null!);
    }

    // ===== 6. Certificates ================================================
    private void Cert_Refresh(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadCerts(); }
    private void Cert_RefreshClick(object sender, RoutedEventArgs e) => LoadCerts();

    private void LoadCerts()
    {
        _certs.Clear();
        string store = CertStoreBox.SelectedIndex switch { 1 => "Root", 2 => "CA", _ => "My" };
        List<CertInfo> list;
        try { list = CertificateService.Enumerate(store); } catch { return; }
        var expiredBrush = new SolidColorBrush(Colors.IndianRed);
        var okBrush = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        foreach (var c in list.OrderBy(c => c.Subject, StringComparer.OrdinalIgnoreCase))
        {
            _certs.Add(new CertVm
            {
                Subject = c.Subject,
                Issuer = c.Issuer,
                Thumbprint = c.Thumbprint,
                Validity = c.Expired
                    ? P($"expired {c.NotAfter:yyyy-MM-dd}", $"已過期 {c.NotAfter:yyyy-MM-dd}")
                    : P($"valid to {c.NotAfter:yyyy-MM-dd}", $"有效至 {c.NotAfter:yyyy-MM-dd}"),
                ValidityBrush = c.Expired ? expiredBrush : okBrush,
            });
        }
        CertCount.Text = P($"{_certs.Count} certs", $"{_certs.Count} 張憑證");
    }

    // ===== 7. Live counters ===============================================
    private void StartCounters()
    {
        if (_pdh == null)
        {
            _pdh = new PdhCounters();
            if (!_pdh.Open()) { _pdh.Dispose(); _pdh = null; CountersHint.Text = P("Counters unavailable", "計數唔可用"); return; }
            _pdh.Collect(); // prime — rate counters need a baseline
        }
        if (CountersSwitch.IsOn) _countersTimer.Start();
    }

    private void Counters_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        if (CountersSwitch.IsOn && ReferenceEquals(Sections.SelectedItem, PiCounters)) { StartCounters(); }
        else _countersTimer.Stop();
    }

    private async void CountersTimer_Tick(object? sender, object e)
    {
        // Never let a tick throw (unhandled DispatcherTimer exceptions crash the app),
        // and never let two collections overlap — PDH .Collect()/.Read() are done off-thread.
        if (_busy || _pdh == null) return;
        _busy = true;
        try
        {
            var pdh = _pdh;
            var samples = await Task.Run(() =>
            {
                try { pdh.Collect(); return pdh.Read(); }
                catch { return null; }
            });

            // Bail if torn down / disposed while awaiting.
            if (samples == null || _pdh == null || !IsLoaded) return;
            RenderCounters(samples);
        }
        catch { /* swallow — a tick must never surface an exception */ }
        finally { _busy = false; }
    }

    private void RenderCounters(IEnumerable<CounterSample> samples)
    {
        CountersPanel.Children.Clear();
        foreach (var s in samples)
        {
            var sp = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 4) };
            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock { Text = s.Label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            var value = new TextBlock
            {
                Text = s.Unit == "B/s" ? $"{HumanRate(s.Value)}" : $"{s.Value:0.0}{s.Unit}",
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            Grid.SetColumn(value, 1);
            head.Children.Add(label);
            head.Children.Add(value);
            sp.Children.Add(head);
            if (s.Unit == "%")
                sp.Children.Add(new ProgressBar { Value = Math.Min(s.Value, 100), Maximum = 100, Height = 4 });
            CountersPanel.Children.Add(sp);
        }
    }

    private static string HumanRate(double bytesPerSec)
    {
        string[] u = { "B/s", "KB/s", "MB/s", "GB/s" };
        double v = bytesPerSec; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.0} {u[i]}";
    }

    // ===== 8. Process modules =============================================
    private async void PopulateProcesses(int? keepPid = null)
    {
        // Enumerating every process (and touching each one's name/id) can block the
        // UI thread for a noticeable beat, so do the walk off-thread and bind after.
        List<ProcItem> items;
        try
        {
            items = await Task.Run(() =>
            {
                var list = new List<ProcItem>();
                foreach (var p in Process.GetProcesses())
                {
                    try { list.Add(new ProcItem { Pid = p.Id, Display = $"{p.ProcessName} · {p.Id}" }); }
                    catch { }
                    finally { p.Dispose(); }
                }
                list.Sort((a, b) => string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase));
                return list;
            });
        }
        catch { items = new List<ProcItem>(); }

        // If we were torn down (or navigated away) while awaiting, bail out.
        if (!IsLoaded) return;

        ProcBox.Items.Clear();
        foreach (var it in items)
            ProcBox.Items.Add(it);

        if (keepPid is int pid)
        {
            var match = ProcBox.Items.OfType<ProcItem>().FirstOrDefault(i => i.Pid == pid);
            if (match != null) { ProcBox.SelectedItem = match; return; }
        }
        if (ProcBox.Items.Count > 0) ProcBox.SelectedIndex = 0;
    }

    private void Proc_Reload(object sender, RoutedEventArgs e)
    {
        int? keep = (ProcBox.SelectedItem as ProcItem)?.Pid;
        PopulateProcesses(keep);
    }

    private void ProcBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        _modules.Clear();
        if (ProcBox.SelectedItem is not ProcItem it) { ModCount.Text = ""; return; }
        List<ProcModule> mods;
        try { mods = ProcessModuleService.Modules(it.Pid); } catch { mods = new(); }
        foreach (var m in mods)
            _modules.Add(new ModuleVm { Name = m.Name, Path = m.Path, SizeText = HumanBytes(m.SizeBytes) });
        ModCount.Text = mods.Count > 0
            ? P($"{mods.Count} modules", $"{mods.Count} 個模組")
            : P("none / access denied", "冇／拒絕存取");
    }

    private static string HumanBytes(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] u = { "B", "KB", "MB", "GB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }

    // ===== 9. Bluetooth ===================================================
    private void Bt_Refresh(object sender, RoutedEventArgs e)
    {
        _bt.Clear();
        List<BtDevice> list;
        try { list = BluetoothService.Paired(); }
        catch { BtCount.Text = P("Bluetooth unavailable", "藍牙唔可用"); return; }
        foreach (var d in list.OrderByDescending(d => d.Connected).ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            _bt.Add(new BtVm
            {
                Name = d.Name,
                Status = d.Connected ? P("connected", "已連線") : (d.Authenticated ? P("paired", "已配對") : P("remembered", "已記住")),
                LastSeenText = d.LastSeen > DateTime.MinValue ? P($"last seen {d.LastSeen:yyyy-MM-dd HH:mm}", $"上次見 {d.LastSeen:yyyy-MM-dd HH:mm}") : "",
                Device = d,
            });
        }
        BtCount.Text = P($"{_bt.Count} devices", $"{_bt.Count} 個裝置");
    }

    private void BtRemove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not BtVm v) return;
        bool ok = false;
        try { ok = BluetoothService.Remove(v.Device); } catch { }
        Info(ok, "Device unpaired", "已解除配對", "Could not unpair", "解除唔到");
        if (ok) Bt_Refresh(null!, null!);
    }

    // ---- lifecycle ---------------------------------------------------------
    private void ReleaseMonitors()
    {
        if (_monitors.Count > 0) { try { MonitorService.Release(_monitors); } catch { } _monitors = new(); }
    }

    private void Cleanup()
    {
        _countersTimer.Stop();
        _countersTimer.Tick -= CountersTimer_Tick;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _pdh?.Dispose(); _pdh = null;
        ReleaseMonitors();
    }
}
