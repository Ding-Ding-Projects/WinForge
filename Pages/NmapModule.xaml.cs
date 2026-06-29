using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 Nmap 掃描器（包真實 nmap.exe）· In-app Nmap scanner wrapping nmap.exe — enter targets,
/// pick a scan profile, toggle common flags, see a read-only command preview, run with -oX - and parse
/// the XML into a hosts / ports / services grid, watch a live log, cancel a running scan, and save the
/// raw XML or a flattened CSV. Install via winget (Insecure.Nmap, bundles Npcap). No redirect. Bilingual.
/// </summary>
public sealed partial class NmapModule : Page
{
    private readonly DispatcherQueue _ui;
    private readonly ObservableCollection<NmapPort> _rows = new();
    private readonly List<CheckBox> _flagChecks = new();
    private CancellationTokenSource? _cts;
    private NmapScanResult? _last;
    private List<TweakDefinition>? _ops;
    private bool _building;

    public NmapModule()
    {
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();
        List.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; try { _cts?.Cancel(); } catch { } };
        Loaded += async (_, _) => { Render(); PopulateOps(); UpdatePreview(); await CheckEngine(); };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); PopulateOps(); UpdatePreview(); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        _building = true;
        Header.Title = "Nmap Scanner · 網絡掃描";
        HeaderBlurb.Text = P(
            "Wrap the Nmap port scanner: enter a target (IP, hostname, CIDR like 192.168.1.0/24, or a range), pick a profile, toggle flags, run, and read the parsed hosts / ports / services. Only scan networks you own or are authorised to test.",
            "包裝 Nmap 連接埠掃描器：輸入目標（IP、主機名、CIDR 如 192.168.1.0/24，或範圍），揀設定檔、開關旗標、執行，然後睇解析好嘅主機／連接埠／服務。只可掃描你擁有或獲授權測試嘅網絡。");

        ScanTab.Header = P("Scan", "掃描");
        OpsTab.Header = P("Tools", "工具");
        LogTab.Header = P("Live log", "即時記錄");

        TargetLabel.Text = P("Target(s)", "目標");
        TargetBox.PlaceholderText = P("e.g. 192.168.1.0/24, scanme.nmap.org, 10.0.0.1-50",
            "例如 192.168.1.0/24、scanme.nmap.org、10.0.0.1-50");
        ProfileLabel.Text = P("Scan profile", "掃描設定檔");
        FlagsLabel.Text = P("Common flags", "常用旗標");
        ExtraLabel.Text = P("Extra flags (optional)", "額外旗標（可選）");
        ExtraBox.PlaceholderText = P("e.g. -p 80,443 --script vuln", "例如 -p 80,443 --script vuln");
        PreviewLabel.Text = P("Command preview", "命令預覽");

        RunBtn.Content = P("Run scan", "開始掃描");
        CancelBtn.Content = P("Cancel", "取消");
        SaveBtn.Content = P("Save results…", "儲存結果…");

        ColHost.Text = P("Host", "主機");
        ColPort.Text = P("Port", "連接埠");
        ColProto.Text = P("Proto", "協定");
        ColState.Text = P("State", "狀態");
        ColService.Text = P("Service", "服務");
        ColVersion.Text = P("Version / OS", "版本／作業系統");
        if (_rows.Count == 0)
        {
            EmptyHint.Visibility = Visibility.Visible;
            EmptyHint.Text = P("No results yet — enter a target and run a scan.",
                "暫時未有結果 — 輸入目標然後開始掃描。");
        }

        // Profiles
        int sel = ProfileBox.SelectedIndex < 0 ? 1 : ProfileBox.SelectedIndex;
        ProfileBox.Items.Clear();
        foreach (var pr in NmapService.Profiles)
            ProfileBox.Items.Add(P(pr.En, pr.Zh) + (pr.NeedsAdmin ? " ★" : ""));
        ProfileBox.SelectedIndex = Math.Min(sel, ProfileBox.Items.Count - 1);

        // Flag toggles — rebuilt with current language. Preserve checked state by flag.
        var wasOn = _flagChecks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToHashSet();
        FlagsHost.Children.Clear();
        FlagsHost2.Children.Clear();
        _flagChecks.Clear();
        int i = 0;
        foreach (var fo in NmapService.CommonFlags)
        {
            var cb = new CheckBox
            {
                Content = P(fo.En, fo.Zh) + (fo.NeedsAdmin ? " ★" : ""),
                Tag = fo.Flag,
                IsChecked = wasOn.Contains(fo.Flag),
                MinWidth = 0,
            };
            cb.Checked += Any_Changed;
            cb.Unchecked += Any_Changed;
            _flagChecks.Add(cb);
            (i++ < 4 ? FlagsHost : FlagsHost2).Children.Add(cb);
        }
        _building = false;
    }

    // ===== engine detection =====

    private async Task CheckEngine()
    {
        bool ok = await Task.Run(NmapService.IsAvailable);
        EngineBar.IsOpen = !ok;
        if (!ok)
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Nmap not found", "搵唔到 Nmap");
            EngineBar.Message = P("Click to install Nmap automatically (winget · Insecure.Nmap, bundles the Npcap driver) — no restart needed.",
                "撳一下自動安裝 Nmap（winget · Insecure.Nmap，附帶 Npcap 驅動）— 唔使重啟。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                NmapService.WingetId, "Install Nmap automatically", "自動安裝 Nmap",
                async () => { await CheckEngine(); }, NmapService.Rescan);
        }
        else EngineBar.ActionButton = null;

        RunBtn.IsEnabled = ok && _cts is null;
    }

    // ===== ops cards =====

    private void PopulateOps()
    {
        _ops = NmapOperations.All().ToList();
        OpsPanel.Children.Clear();
        foreach (var op in _ops)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }

    // ===== command building / preview =====

    private void Any_Changed(object sender, object e)
    {
        if (_building) return;
        UpdatePreview();
    }

    private IEnumerable<string> CheckedFlags()
        => _flagChecks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag);

    private string ProfileKey()
    {
        int idx = ProfileBox.SelectedIndex;
        if (idx < 0 || idx >= NmapService.Profiles.Count) idx = 1;
        return NmapService.Profiles[idx].Key;
    }

    private void UpdatePreview()
    {
        var target = (TargetBox.Text ?? "").Trim();
        var args = NmapService.BuildArgs(ProfileKey(), CheckedFlags(), ExtraBox.Text ?? "",
            target.Length == 0 ? "<target>" : target);
        PreviewText.Text = NmapService.PreviewCommand(args);

        // Surface an elevation hint for admin-needing options.
        bool needsAdmin = NmapService.NeedsAdmin(ProfileKey(), CheckedFlags());
        if (needsAdmin && !AdminHelper.IsElevated)
        {
            ConsentBar.IsOpen = true;
            ConsentBar.Severity = InfoBarSeverity.Warning;
            ConsentBar.Title = P("This scan needs administrator", "呢個掃描需要管理員權限");
            ConsentBar.Message = P("OS detection (-O), raw SYN, UDP and aggressive scans require admin + the Npcap driver. Relaunch WinForge as administrator, or the scan may fail or fall back.",
                "作業系統偵測（-O）、raw SYN、UDP 同進取掃描需要管理員權限同 Npcap 驅動。請以管理員身分重開 WinForge，否則掃描可能失敗或退回。");
        }
        else if (ConsentBar.Severity == InfoBarSeverity.Warning)
        {
            ConsentBar.IsOpen = false;
        }
    }

    // ===== run / cancel =====

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is not null) return;

        var target = (TargetBox.Text ?? "").Trim();
        if (!NmapService.IsValidTarget(target))
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Enter a valid target", "請輸入有效目標");
            ResultBar.Message = P("Provide an IP, hostname, CIDR (192.168.1.0/24) or range (10.0.0.1-50). No shell characters.",
                "請提供 IP、主機名、CIDR（192.168.1.0/24）或範圍（10.0.0.1-50）。不可有 shell 字元。");
            return;
        }

        if (!NmapService.IsAvailable()) { await CheckEngine(); return; }

        var args = NmapService.BuildArgs(ProfileKey(), CheckedFlags(), ExtraBox.Text ?? "", target);

        _cts = new CancellationTokenSource();
        SetRunning(true);
        LogText.Text = "";
        ResultBar.IsOpen = false;
        StatusText.Text = P("Scanning…", "掃描緊…");

        void OnProgress(string line) => _ui.TryEnqueue(() =>
        {
            LogText.Text += line + "\n";
            // Keep the live log from growing without bound.
            if (LogText.Text.Length > 60000) LogText.Text = LogText.Text[^40000..];
            LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
        });

        NmapScanResult result;
        try
        {
            result = await NmapService.RunScanAsync(args, OnProgress, _cts.Token);
        }
        catch (Exception ex)
        {
            result = new NmapScanResult { Error = ex.Message };
        }

        _last = result;
        FillResults(result);
        SetRunning(false);
        _cts?.Dispose();
        _cts = null;

        if (result.Cancelled)
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Informational;
            ResultBar.Title = P("Scan cancelled", "已取消掃描");
            ResultBar.Message = "";
            StatusText.Text = P("Cancelled.", "已取消。");
        }
        else if (result.Ok || result.Hosts.Count > 0)
        {
            int ports = result.Hosts.Sum(h => h.Ports.Count(p => p.State == "open"));
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Scan complete", "掃描完成");
            ResultBar.Message = P($"{result.Hosts.Count(h => h.Status == "up")} host(s) up · {ports} open port(s). {result.Summary}",
                $"{result.Hosts.Count(h => h.Status == "up")} 部主機上線 · {ports} 個開放連接埠。{result.Summary}");
            StatusText.Text = result.Summary;
            SaveBtn.IsEnabled = true;
        }
        else
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Scan failed", "掃描失敗");
            ResultBar.Message = string.IsNullOrWhiteSpace(result.Error)
                ? P("No output — check the target and your privileges.", "無輸出 — 檢查目標同權限。")
                : result.Error;
            StatusText.Text = "";
        }

        await CheckEngine();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        StatusText.Text = P("Cancelling…", "取消緊…");
    }

    private void SetRunning(bool running)
    {
        Busy.IsActive = running;
        RunBtn.IsEnabled = !running && NmapService.IsAvailable();
        CancelBtn.IsEnabled = running;
        TargetBox.IsEnabled = !running;
        ProfileBox.IsEnabled = !running;
        ExtraBox.IsEnabled = !running;
        foreach (var c in _flagChecks) c.IsEnabled = !running;
    }

    private void FillResults(NmapScanResult result)
    {
        _rows.Clear();
        foreach (var port in result.AllPorts) _rows.Add(port);

        // Show hosts with no ports as a synthetic row so a ping sweep still lists discovered hosts.
        foreach (var h in result.Hosts.Where(h => h.Ports.Count == 0 && h.Status == "up"))
        {
            _rows.Add(new NmapPort
            {
                HostAddress = h.Address,
                HostName = h.Hostname,
                State = "up",
                Service = string.IsNullOrEmpty(h.Os) ? (string.IsNullOrEmpty(h.Vendor) ? "" : h.Vendor) : "",
                Version = string.IsNullOrEmpty(h.Os) ? h.Latency : $"{h.Os} ({h.OsAccuracy}%)",
            });
        }

        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_rows.Count == 0)
            EmptyHint.Text = P("No hosts/ports returned.", "無主機／連接埠回傳。");
    }

    // ===== save =====

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_last is null) return;
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
        var path = await FileDialogs.SaveFileAsync(
            $"nmap-scan-{stamp}",
            new[]
            {
                new FileDialogs.Filter(P("Nmap XML", "Nmap XML"), "*.xml"),
                new FileDialogs.Filter(P("CSV (flattened)", "CSV（攤平）"), "*.csv"),
            },
            "xml",
            P("Save scan results", "儲存掃描結果"));
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            string content = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? NmapService.ToCsv(_last)
                : (_last.RawXml.Length > 0 ? _last.RawXml : NmapService.ToCsv(_last));
            await System.IO.File.WriteAllTextAsync(path, content);
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Saved", "已儲存");
            ResultBar.Message = path;
        }
        catch (Exception ex)
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Could not save", "儲存唔到");
            ResultBar.Message = ex.Message;
        }
    }
}
