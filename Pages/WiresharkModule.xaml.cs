using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內封包擷取（包 Wireshark dumpcap / tshark）· In-app Packet Capture wrapping Wireshark's CLI:
/// list interfaces, live capture to a .pcapng with a scrolling summary grid, capture &amp; display filters,
/// packet detail, open saved files, protocol/conversation/endpoint statistics, follow TCP stream, export a
/// filtered subset, and open the full Wireshark GUI. No redirect. Bilingual.
/// </summary>
public sealed partial class WiresharkModule : Page
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _ui;

    private readonly ObservableCollection<PacketRow> _live = new();
    private readonly ObservableCollection<PacketRow> _fileRows = new();

    // Batch incoming live rows so a flood of packets doesn't drown the UI thread.
    private readonly object _bufLock = new();
    private readonly List<PacketRow> _buffer = new();
    private DispatcherTimer? _flushTimer;
    private const int MaxLiveRows = 5000;

    private string _openedFile = "";
    private List<TweakDefinition>? _ops;
    private bool _rowBusy; // guard so only one control-row action runs at a time

    public WiresharkModule()
    {
        InitializeComponent();
        _ui = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        PacketList.ItemsSource = _live;
        FilePacketList.ItemsSource = _fileRows;
        OutputFileBox.Text = WiresharkService.DefaultOutputFile();

        Loc.I.LanguageChanged += OnLang;
        Loaded += async (_, _) => { Render(); PopulateOps(string.Empty); await CheckEngine(); await RefreshInterfaces(); };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            WiresharkService.StopCapture();
            WiresharkService.StopReading();
            _flushTimer?.Stop();
        };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); PopulateOps(OpsFilter.Text ?? string.Empty); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Packet Capture · 封包擷取";
        HeaderBlurb.Text = P(
            "Capture network packets with Wireshark's engine: pick an interface, capture to a .pcapng with a live summary grid, apply capture (BPF) and display filters, inspect packet detail, read saved files, view statistics, and open the full Wireshark GUI. Capture needs administrator rights and Npcap.",
            "用 Wireshark 引擎擷取網絡封包：揀介面、擷取去 .pcapng 並即時顯示摘要、套用擷取（BPF）同顯示篩選器、查睇封包詳情、讀已存檔案、睇統計，仲可以打開完整 Wireshark GUI。擷取需要管理員權限同 Npcap。");

        CaptureTab.Header = P("Live capture", "即時擷取");
        FileTab.Header = P("Open file", "開啟檔案");
        DetailTab.Header = P("Packet detail", "封包詳情");
        StatsTab.Header = P("Statistics", "統計");
        OpsTab.Header = P("Operations", "操作");

        ChooseFileBtn.Content = P("Save to…", "儲存到…");
        PromiscuousCheck.Content = P("Promiscuous mode", "混雜模式");
        StopSecCap.Text = P("Stop after (sec)", "停止（秒）");
        StopPktCap.Text = P("Stop after (packets)", "停止（封包）");
        RingCheck.Content = P("Ring buffer", "環形緩衝");
        RingFilesCap.Text = P("files", "檔案數");
        RingSizeCap.Text = P("KB each", "每個 KB");
        StartBtn.Content = P("Start", "開始");
        StopBtn.Content = P("Stop", "停止");
        ClearBtn.Content = P("Clear", "清除");
        OpenWiresharkBtn.Content = P("Open in Wireshark", "喺 Wireshark 打開");

        SetHeaders(ColNo, ColTime, ColSrc, ColDst, ColProto, ColLen, ColInfo);
        SetHeaders(FColNo, FColTime, FColSrc, FColDst, FColProto, FColLen, FColInfo);
        UpdateCounter();

        DetailExpander.Header = P("Packet detail (selected)", "封包詳情（已選）");

        // Open file tab
        OpenFileBtn.Content = P("Open .pcap/.pcapng…", "開啟 .pcap/.pcapng…");
        ApplyFilterBtn.Content = P("Apply filter", "套用篩選");
        ExportBtn.Content = P("Export filtered…", "匯出已篩選…");

        // Detail tab
        FrameNoCap.Text = P("Frame #", "封包編號");
        LoadDetailBtn.Content = P("Show detail", "顯示詳情");
        StreamNoCap.Text = P("TCP stream #", "TCP 串流");
        FollowStreamBtn.Content = P("Follow TCP stream", "跟蹤 TCP 串流");

        // Stats tab
        ProtoStatsBtn.Content = P("Protocol hierarchy", "協定階層");
        ConvStatsBtn.Content = P("TCP conversations", "TCP 對話");
        EndpointStatsBtn.Content = P("IP endpoints", "IP 端點");

        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
    }

    private void SetHeaders(TextBlock no, TextBlock time, TextBlock src, TextBlock dst, TextBlock proto, TextBlock len, TextBlock info)
    {
        no.Text = P("No.", "編號");
        time.Text = P("Time", "時間");
        src.Text = P("Source", "來源");
        dst.Text = P("Destination", "目的地");
        proto.Text = P("Proto", "協定");
        len.Text = P("Len", "長度");
        info.Text = P("Info", "資訊");
    }

    // ── engine / npcap / admin ───────────────────────────────────────────────────────

    private async Task CheckEngine()
    {
        bool installed = WiresharkService.IsInstalled;
        EngineBar.IsOpen = !installed;
        if (!installed)
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Wireshark not found", "搵唔到 Wireshark");
            EngineBar.Message = P("Click to install Wireshark (with Npcap) automatically via winget — no restart needed.",
                "撳一下用 winget 自動安裝 Wireshark（含 Npcap）— 唔使重啟。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                WiresharkService.WingetId, "Install Wireshark (+ Npcap)", "安裝 Wireshark（含 Npcap）",
                async () => { await CheckEngine(); await RefreshInterfaces(); },
                WiresharkService.Rescan);
        }
        else EngineBar.ActionButton = null;

        // Npcap bar.
        bool npcap = WiresharkService.IsNpcapInstalled();
        NpcapBar.IsOpen = installed && !npcap;
        if (NpcapBar.IsOpen)
        {
            NpcapBar.Severity = InfoBarSeverity.Warning;
            NpcapBar.Title = P("Npcap driver missing", "缺少 Npcap 驅動");
            NpcapBar.Message = P("Live capture needs the Npcap driver. Reinstall Wireshark and enable Npcap during setup.",
                "即時擷取需要 Npcap 驅動。請重裝 Wireshark 並喺安裝時啟用 Npcap。");
            var btn = EngineBars.AutoInstallButton(
                WiresharkService.WingetId, "Reinstall Wireshark (+ Npcap)", "重裝 Wireshark（含 Npcap）",
                async () => { await CheckEngine(); await RefreshInterfaces(); }, WiresharkService.Rescan);
            NpcapBar.ActionButton = btn;
        }
        else NpcapBar.ActionButton = null;

        // Admin bar (only matters once installed).
        AdminBar.IsOpen = installed && !WiresharkService.IsElevated;
        if (AdminBar.IsOpen)
        {
            AdminBar.Title = P("Administrator required to capture", "擷取需要管理員權限");
            AdminBar.Message = P("Live capture needs elevation (Npcap kernel driver). Reading saved files still works.",
                "即時擷取需要提權（Npcap 核心驅動）。讀取已存檔案仍可使用。");
            var btn = new Button { Content = P("Relaunch as administrator", "以管理員重開") };
            btn.Click += (_, _) => { if (AdminHelper.RelaunchElevated()) Application.Current.Exit(); };
            AdminBar.ActionButton = btn;
        }
        else AdminBar.ActionButton = null;
    }

    // ── interfaces ─────────────────────────────────────────────────────────────────

    private async Task RefreshInterfaces()
    {
        if (!WiresharkService.IsInstalled) return;
        Busy.IsActive = true;
        var ifs = await WiresharkService.Interfaces();
        Busy.IsActive = false;
        InterfaceBox.Items.Clear();
        foreach (var i in ifs) InterfaceBox.Items.Add(i);
        if (InterfaceBox.Items.Count > 0) InterfaceBox.SelectedIndex = 0;
        else if (!EngineBar.IsOpen && !NpcapBar.IsOpen)
            Notify(InfoBarSeverity.Informational, P("No interfaces", "冇介面"),
                P("None found — capture needs administrator rights and Npcap.", "搵唔到 — 擷取需要管理員權限同 Npcap。"));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) { await CheckEngine(); await RefreshInterfaces(); }

    private async void ChooseFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync(
            $"WinForge-{DateTime.Now:yyyyMMdd-HHmmss}", ".pcapng", ".pcap");
        if (path is not null) OutputFileBox.Text = path;
    }

    // ── capture start / stop ─────────────────────────────────────────────────────────

    private CaptureOptions BuildOptions()
    {
        var iface = InterfaceBox.SelectedItem as CaptureInterface;
        return new CaptureOptions
        {
            InterfaceId = iface?.Id ?? "",
            OutputFile = OutputFileBox.Text ?? "",
            CaptureFilter = (CaptureFilterBox.Text ?? "").Trim(),
            DisplayFilter = (DisplayFilterBox.Text ?? "").Trim(),
            Promiscuous = PromiscuousCheck.IsChecked == true,
            StopAfterSeconds = (int)StopSecBox.Value,
            StopAfterPackets = (int)StopPktBox.Value,
            RingFiles = RingCheck.IsChecked == true ? (int)RingFilesBox.Value : 0,
            RingFileSizeKb = RingCheck.IsChecked == true ? (int)RingSizeBox.Value : 0,
        };
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        if (WiresharkService.IsCapturing) return;
        var opts = BuildOptions();
        if (string.IsNullOrEmpty(opts.InterfaceId))
        {
            Notify(InfoBarSeverity.Warning, P("Pick an interface first", "請先揀一個介面"), "");
            return;
        }
        if (string.IsNullOrEmpty(opts.OutputFile))
        {
            Notify(InfoBarSeverity.Warning, P("Choose an output file first", "請先揀輸出檔案"), "");
            return;
        }

        _live.Clear();
        lock (_bufLock) _buffer.Clear();
        StartFlushTimer();

        var r = WiresharkService.StartCapture(opts, OnLiveRow, OnLog);
        if (!r.Success)
        {
            _flushTimer?.Stop();
            Notify(InfoBarSeverity.Error, P("Could not start capture", "無法開始擷取"), Msg(r));
            return;
        }
        _openedFile = opts.OutputFile;
        StartBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;
        Notify(InfoBarSeverity.Success, P("Capturing…", "擷取緊…"), opts.OutputFile);
        UpdateCounter();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        WiresharkService.StopCapture();
        _flushTimer?.Stop();
        FlushBuffer();
        StartBtn.IsEnabled = true;
        StopBtn.IsEnabled = false;
        UpdateCounter();
        Notify(InfoBarSeverity.Informational, P("Capture stopped", "已停止擷取"),
            $"{P("Saved to", "已儲存到")}: {_openedFile}");
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _live.Clear();
        lock (_bufLock) _buffer.Clear();
        DetailText.Text = "";
        UpdateCounter();
    }

    private void OpenWireshark_Click(object sender, RoutedEventArgs e)
    {
        var file = !string.IsNullOrEmpty(_openedFile) ? _openedFile : (OutputFileBox.Text ?? "");
        var r = WiresharkService.OpenInWireshark(file);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Open in Wireshark", "喺 Wireshark 打開"), Msg(r));
    }

    // batched live-row plumbing ----------------------------------------------------------

    private void OnLiveRow(PacketRow row)
    {
        lock (_bufLock) _buffer.Add(row);
    }

    private void OnLog(string line)
    {
        _ui.TryEnqueue(() =>
        {
            // Surface only meaningful messages (dumpcap/tshark errors), not noise.
            if (line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("not found", StringComparison.OrdinalIgnoreCase))
                Notify(InfoBarSeverity.Warning, P("Capture message", "擷取訊息"), line);
        });
    }

    private void StartFlushTimer()
    {
        _flushTimer ??= CreateTimer();
        _flushTimer.Start();
    }

    private DispatcherTimer CreateTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        t.Tick += (_, _) => FlushBuffer();
        return t;
    }

    private void FlushBuffer()
    {
        List<PacketRow> batch;
        lock (_bufLock)
        {
            if (_buffer.Count == 0) { UpdateCounter(); return; }
            batch = new List<PacketRow>(_buffer);
            _buffer.Clear();
        }
        foreach (var r in batch) _live.Add(r);
        // Cap the in-memory grid so it stays responsive on long captures.
        while (_live.Count > MaxLiveRows) _live.RemoveAt(0);
        if (_live.Count > 0) PacketList.ScrollIntoView(_live[^1]);
        UpdateCounter();
    }

    private void UpdateCounter()
    {
        long total = WiresharkService.PacketCount;
        CounterText.Text = $"{P("Packets", "封包")}: {total}   ·   {P("Shown", "顯示")}: {_live.Count}";
    }

    private async void PacketList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PacketList.SelectedItem is not PacketRow row) return;
        if (string.IsNullOrEmpty(_openedFile)) return;
        if (WiresharkService.IsCapturing)
        {
            DetailExpander.IsExpanded = true;
            DetailText.Text = P("Stop the capture to inspect packet detail from the file.",
                "停止擷取後先可以由檔案查睇封包詳情。");
            return;
        }
        DetailExpander.IsExpanded = true;
        DetailText.Text = P("Loading…", "載入緊…");
        var detail = await WiresharkService.PacketDetail(_openedFile, row.No);
        DetailText.Text = string.IsNullOrWhiteSpace(detail) ? P("No detail available.", "冇詳情。") : detail;
    }

    // ── open file tab ────────────────────────────────────────────────────────────────

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".pcapng", ".pcap", ".cap");
        if (path is null) return;
        _openedFile = path;
        OpenedFileBox.Text = path;
        await LoadFileRows((FileFilterBox.Text ?? "").Trim());
    }

    private void FileFilter_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) { e.Handled = true; ApplyFilter_Click(sender, e); }
    }

    private async void ApplyFilter_Click(object sender, RoutedEventArgs e)
        => await LoadFileRows((FileFilterBox.Text ?? "").Trim());

    private async Task LoadFileRows(string filter)
    {
        if (string.IsNullOrEmpty(_openedFile))
        {
            Notify(InfoBarSeverity.Warning, P("Open a file first", "請先開啟檔案"), "");
            return;
        }
        Busy.IsActive = true;
        FileCounterText.Text = P("Reading…", "讀緊…");
        var rows = await WiresharkService.ReadFile(_openedFile, filter);
        Busy.IsActive = false;
        _fileRows.Clear();
        foreach (var r in rows) _fileRows.Add(r);
        FileCounterText.Text = $"{P("Packets", "封包")}: {rows.Count}";
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_openedFile))
        {
            Notify(InfoBarSeverity.Warning, P("Open a file first", "請先開啟檔案"), "");
            return;
        }
        var dest = await FileDialogs.SaveFileAsync(
            $"filtered-{DateTime.Now:yyyyMMdd-HHmmss}", ".pcapng", ".pcap");
        if (dest is null) return;
        Busy.IsActive = true;
        var r = await WiresharkService.ExportFiltered(_openedFile, (FileFilterBox.Text ?? "").Trim(), dest);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Export filtered", "匯出已篩選"), r.Success ? dest : Msg(r));
    }

    private void FilePacketList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilePacketList.SelectedItem is PacketRow row && int.TryParse(row.No, out var n))
            FrameNoBox.Value = n;
    }

    // ── packet detail tab ────────────────────────────────────────────────────────────

    private async void LoadDetail_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_openedFile))
        {
            Notify(InfoBarSeverity.Warning, P("Open or capture a file first", "請先開啟或擷取檔案"), "");
            return;
        }
        OfflineDetailText.Text = P("Loading…", "載入緊…");
        var detail = await WiresharkService.PacketDetail(_openedFile, ((int)FrameNoBox.Value).ToString());
        OfflineDetailText.Text = string.IsNullOrWhiteSpace(detail) ? P("No detail available.", "冇詳情。") : detail;
    }

    private async void FollowStream_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_openedFile))
        {
            Notify(InfoBarSeverity.Warning, P("Open or capture a file first", "請先開啟或擷取檔案"), "");
            return;
        }
        OfflineDetailText.Text = P("Loading…", "載入緊…");
        var content = await WiresharkService.FollowTcpStream(_openedFile, (int)StreamNoBox.Value);
        OfflineDetailText.Text = string.IsNullOrWhiteSpace(content) ? P("No stream content.", "冇串流內容。") : content;
    }

    // ── statistics tab ───────────────────────────────────────────────────────────────

    private async void ProtoStats_Click(object sender, RoutedEventArgs e) => await ShowStats(WiresharkService.ProtocolStats);
    private async void ConvStats_Click(object sender, RoutedEventArgs e) => await ShowStats(WiresharkService.ConversationStats);
    private async void EndpointStats_Click(object sender, RoutedEventArgs e) => await ShowStats(WiresharkService.EndpointStats);

    private async Task ShowStats(Func<string, CancellationToken, Task<string>> stat)
    {
        if (string.IsNullOrEmpty(_openedFile))
        {
            Notify(InfoBarSeverity.Warning, P("Open or capture a file first", "請先開啟或擷取檔案"), "");
            return;
        }
        StatsText.Text = P("Computing…", "計算緊…");
        var outp = await stat(_openedFile, CancellationToken.None);
        StatsText.Text = string.IsNullOrWhiteSpace(outp) ? P("No statistics available.", "冇統計。") : outp;
    }

    // ── operations tab ───────────────────────────────────────────────────────────────

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= WiresharkOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }
        bool first = true;
        foreach (var op in shown)
        {
            if (!first) OpsPanel.Children.Add(BuildDivider());
            first = false;
            OpsPanel.Children.Add(BuildRow(op));
        }
    }

    // ── hand-built control rows (no TweakCard) ───────────────────────────────────────

    // One clean row: bilingual title + description on the left, control on the right.
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        text.Children.Add(new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        // A cheap coloured status pill (e.g. "Installed / Not found"), if the op supplies one.
        var pill = BuildStatusPill(op);
        if (pill is not null) text.Children.Add(pill);

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Optional coloured status pill ----------------
    private FrameworkElement? BuildStatusPill(TweakDefinition op)
    {
        if (op.ColoredStatus is null) return null;
        string en, zh; StatusColor color;
        try { (en, zh, color) = op.ColoredStatus(); }
        catch { return null; }

        var (bg, fg) = StatusBrushes(color);
        var border = new Border
        {
            Background = bg,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 2, 8, 2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 4, 0, 0),
        };
        border.Child = new TextBlock { Text = P(en, zh), Foreground = fg, FontSize = 11, FontWeight = FontWeights.SemiBold };
        return border;
    }

    private static (Brush bg, Brush fg) StatusBrushes(StatusColor color)
    {
        string bgKey = color switch
        {
            StatusColor.Good => "SystemFillColorSuccessBackgroundBrush",
            StatusColor.Warn => "SystemFillColorCautionBackgroundBrush",
            StatusColor.Bad => "SystemFillColorCriticalBackgroundBrush",
            _ => "SystemFillColorNeutralBackgroundBrush",
        };
        string fgKey = color switch
        {
            StatusColor.Good => "SystemFillColorSuccessBrush",
            StatusColor.Warn => "SystemFillColorCautionBrush",
            StatusColor.Bad => "SystemFillColorCriticalBrush",
            _ => "TextFillColorPrimaryBrush",
        };
        var res = Application.Current.Resources;
        var bg = res.TryGetValue(bgKey, out var b) && b is Brush bb ? bb : new SolidColorBrush(Microsoft.UI.Colors.Gray);
        var fg = res.TryGetValue(fgKey, out var f) && f is Brush ff ? ff : new SolidColorBrush(Microsoft.UI.Colors.Black);
        return (bg, fg);
    }

    // ---------------- Action → Button awaiting RunAsync ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");
        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        btn.Click += async (_, _) =>
        {
            if (_rowBusy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _rowBusy = true;
            btn.IsEnabled = false;
            var restore = btn.Content;
            btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
            try
            {
                var result = await op.RunAsync(CancellationToken.None);
                ShowResult(result);
                // Rebuild rows so any coloured status pill (installed / not found) refreshes.
                PopulateOps(OpsFilter.Text ?? string.Empty);
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                btn.Content = restore;
                btn.IsEnabled = true;
                _rowBusy = false;
            }
        };
        return btn;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = op.GetCurrentChoice?.Invoke();
            if (cur is not null && op.Choices is not null)
                for (int i = 0; i < op.Choices.Count; i++)
                    if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { combo.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try
                    {
                        var cur = op.GetCurrentChoice?.Invoke();
                        if (cur is not null && op.Choices is not null)
                            for (int i = 0; i < op.Choices.Count; i++)
                                if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                                { combo.SelectedIndex = i; break; }
                    }
                    catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    // ---------------- Slider → Slider + value label ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        string Format(double v)
        {
            bool whole = op.Step >= 1 && Math.Abs(op.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(CultureInfo.InvariantCulture)
                               : v.ToString("0.###", CultureInfo.InvariantCulture);
            return op.Unit is null ? num : $"{num} {op.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = op.Min, Maximum = op.Max, StepFrequency = op.Step, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        var valueText = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        bool suppress = true;
        try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { slider.Value = op.Min; }
        suppress = false;
        valueText.Text = Format(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = Format(e.NewValue);
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        panel.Children.Add(slider);
        panel.Children.Add(valueText);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step,
            LargeChange = op.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        bool suppress = true;
        try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, e) =>
        {
            if (suppress || op.SetNumber is null || double.IsNaN(e.NewValue)) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        return box;
    }

    // ---------------- Info → refreshable TextBlock ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ---------------- Applied / result / error status (routes through the persistent ResultBar) ----------------
    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ShowOpStatus(P("Done", "完成"), P(en, zh), InfoBarSeverity.Success);
    }

    private void ShowResult(TweakResult r)
        => ShowOpStatus(
            r.Success ? P("Done", "完成") : P("Failed", "失敗"),
            r.Message is null ? "" : r.Message.Get(Loc.I.Language),
            r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        ShowOpStatus(P("Failed", "失敗"),
            needAdmin ? P("This change needs administrator rights.", "呢項更改需要管理員權限。") : ex.Message,
            InfoBarSeverity.Error);
    }

    private void ShowOpStatus(string title, string message, InfoBarSeverity severity)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = severity;
        ResultBar.Title = title;
        ResultBar.Message = message ?? "";
    }

    // ── shared helpers ───────────────────────────────────────────────────────────────

    private static string Msg(TweakResult r)
        => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev; ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
