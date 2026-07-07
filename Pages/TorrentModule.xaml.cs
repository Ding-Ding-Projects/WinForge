using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MonoTorrent;
using MonoTorrent.Client;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 BitTorrent 用戶端 · Native in-process BitTorrent client powered by MonoTorrent.
///
/// 重要：呢個模組唔係包裝 qBittorrent。MonoTorrent 係一個「完全受控」（pure C#）嘅 BitTorrent 協定
/// 實作，喺程序內直接行 DHT、本地 peer 探索、PEX、UDP／HTTP 追蹤器 —— 冇任何外部程式被啟動或捆綁。
/// IMPORTANT: this is NOT a qBittorrent wrapper. MonoTorrent is a fully managed (pure C#) BitTorrent
/// protocol engine running DHT / LSD / PEX / UDP+HTTP trackers entirely in-process — nothing is shelled out.
///
/// Add via magnet or .torrent file; live grid (progress · speeds · ETA · ratio · peers/seeds · state);
/// per-torrent start/pause/remove (±data)/recheck/sequential; per-file priority (skip/normal/high);
/// detail panel (trackers · peers · pieces); persisted global settings; session restore on next launch.
/// </summary>
public sealed partial class TorrentModule : Page
{
    private readonly TorrentEngineService _eng = TorrentEngineService.I;
    private readonly ObservableCollection<TorrentRowVM> _rows = new();
    private readonly Dictionary<InfoHash, TorrentRowVM> _byHash = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(1200) };
    private string _filter = "all";
    private string _search = "";

    public TorrentModule()
    {
        InitializeComponent();
        TorrentList.ItemsSource = _rows;
        _timer.Tick += (_, _) => RefreshTick();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private async void OnLoaded(object? s, RoutedEventArgs e)
    {
        BuildFilterCombo();
        Render();
        try
        {
            // 還原上次嘅工作階段（種子 + 進度）· restore last session (torrents + fast-resume).
            int restored = await _eng.RestoreSessionAsync();
            if (restored > 0)
                ShowStatus(InfoBarSeverity.Success, P($"Restored {restored} torrent(s) from the last session.",
                    $"已由上次工作階段還原 {restored} 個種子。"));
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, ex.Message); }
        RefreshTick();
        _timer.Start();
    }

    private async void OnUnloaded(object? s, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _timer.Stop();
        try { await _eng.SaveStateAsync(); } catch { }
    }

    private void Render()
    {
        Header.Title = P("Native Torrent · 原生種子下載", "原生種子下載");
        HeaderBlurb.Text = P("A full BitTorrent client built right into WinForge with the managed MonoTorrent engine — the protocol (DHT, local peer discovery, PEX, UDP/HTTP trackers) runs in-process. No qBittorrent, no external program. Add magnets or .torrent files, watch live progress, set per-file priority, and control each torrent.",
            "用受控 MonoTorrent 引擎內建喺 WinForge 嘅完整 BitTorrent 用戶端 — 協定（DHT、本地 peer 探索、PEX、UDP／HTTP 追蹤器）全部喺程序內運行。唔使 qBittorrent，亦冇外部程式。加磁力或 .torrent 檔、睇實時進度、設定每個檔案優先次序、控制每個種子。");

        AddMagnetLbl.Text = P("Add magnet · 加磁力", "加磁力");
        AddFileLbl.Text = P("Add .torrent · 加檔案", "加 .torrent");
        SettingsBtnLbl.Text = P("Settings · 設定", "設定");
        DetailBtnLbl.Text = P("Details · 詳情", "詳情");
        SearchBox.PlaceholderText = P("Filter by name · 按名稱篩選", "按名稱篩選");

        EmptyText.Text = P("No torrents yet. Click \"Add magnet\" or \"Add .torrent\" to start downloading.",
            "未有種子。撳「加磁力」或者「加 .torrent」開始下載。");

        BuildFilterCombo();
        UpdateActionButtons();
        RefreshTick();
    }

    private void BuildFilterCombo()
    {
        int sel = FilterBox.SelectedIndex < 0 ? 0 : FilterBox.SelectedIndex;
        FilterBox.SelectionChanged -= Filter_Changed;
        FilterBox.Items.Clear();
        var filters = new (string code, string en, string zh)[]
        {
            ("all", "All", "全部"), ("downloading", "Downloading", "下載中"),
            ("seeding", "Seeding", "做種"), ("paused", "Paused/Stopped", "暫停／停止"),
            ("checking", "Checking", "檢查中"), ("error", "Errored", "出錯"),
        };
        foreach (var f in filters) FilterBox.Items.Add(new ComboBoxItem { Content = P($"{f.en} · {f.zh}", f.zh), Tag = f.code });
        FilterBox.SelectedIndex = Math.Min(sel, FilterBox.Items.Count - 1);
        FilterBox.SelectionChanged += Filter_Changed;
    }

    // ── Live refresh ────────────────────────────────────────────────────────────

    private void RefreshTick()
    {
        var e = _eng.Engine;
        if (e is null || e.Disposed)
        {
            FooterEngine.Text = P("Engine not started", "引擎未啟動");
            UpdateListVisibility();
            return;
        }

        var managers = e.Torrents.ToList();
        var seen = new HashSet<InfoHash>();
        foreach (var m in managers)
        {
            if (!PassesFilter(m)) continue;
            var ih = m.InfoHashes.V1OrV2.Truncate();
            seen.Add(ih);
            if (_byHash.TryGetValue(ih, out var vm)) vm.Refresh();
            else
            {
                var nv = new TorrentRowVM(m, ih);
                _byHash[ih] = nv;
                _rows.Add(nv);
            }
        }
        for (int i = _rows.Count - 1; i >= 0; i--)
            if (!seen.Contains(_rows[i].Hash)) { _byHash.Remove(_rows[i].Hash); _rows.RemoveAt(i); }

        long totalDown = e.TotalDownloadRate;
        long totalUp = e.TotalUploadRate;
        int active = managers.Count(m => m.State is TorrentState.Downloading or TorrentState.Seeding);
        FooterDl.Text = TorrentEngineService.HumanSpeed(totalDown);
        FooterUl.Text = TorrentEngineService.HumanSpeed(totalUp);
        FooterEngine.Text = P($"DHT {(_eng.DhtEnabled ? "on" : "off")} · port {_eng.ListenPort} · {active} active",
            $"DHT {(_eng.DhtEnabled ? "開" : "關")} · 連接埠 {_eng.ListenPort} · {active} 個活躍");
        FooterCount.Text = P($"{_rows.Count} of {managers.Count} shown", $"顯示 {_rows.Count} / {managers.Count} 個");
        UpdateListVisibility();
    }

    private bool PassesFilter(TorrentManager m)
    {
        if (!string.IsNullOrWhiteSpace(_search) &&
            m.Name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) return false;
        return _filter switch
        {
            "downloading" => m.State is TorrentState.Downloading or TorrentState.Metadata or TorrentState.FetchingHashes,
            "seeding" => m.State == TorrentState.Seeding,
            "paused" => m.State is TorrentState.Paused or TorrentState.Stopped or TorrentState.Stopping,
            "checking" => m.State is TorrentState.Hashing or TorrentState.HashingPaused,
            "error" => m.State == TorrentState.Error,
            _ => true,
        };
    }

    private void UpdateListVisibility()
    {
        bool empty = _rows.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        TorrentList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── Filters / search ────────────────────────────────────────────────────────

    private void Filter_Changed(object sender, SelectionChangedEventArgs e)
    {
        _filter = (FilterBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        // force full rebuild so dropped rows disappear immediately
        _rows.Clear(); _byHash.Clear();
        RefreshTick();
    }

    private void Search_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _search = sender.Text ?? "";
        _rows.Clear(); _byHash.Clear();
        RefreshTick();
    }

    // ── Selection / actions ─────────────────────────────────────────────────────

    private IEnumerable<TorrentManager> Selected() =>
        TorrentList.SelectedItems.OfType<TorrentRowVM>().Select(v => v.Manager);

    private void TorrentList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionButtons();

    private void UpdateActionButtons()
    {
        int n = TorrentList.SelectedItems.Count;
        bool any = n > 0;
        ResumeBtn.IsEnabled = PauseBtn.IsEnabled = DeleteBtn.IsEnabled = RecheckBtn.IsEnabled = any;
        DetailBtn.IsEnabled = n == 1;
        SelectionInfo.Text = n == 0 ? "" : P($"{n} selected", $"已選 {n} 個");
    }

    private async void Resume_Click(object sender, RoutedEventArgs e)
    {
        foreach (var m in Selected().ToList()) await _eng.StartAsync(m);
        RefreshTick();
    }

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        foreach (var m in Selected().ToList()) await _eng.PauseAsync(m);
        RefreshTick();
    }

    private async void Recheck_Click(object sender, RoutedEventArgs e)
    {
        foreach (var m in Selected().ToList()) await _eng.RecheckAsync(m);
        RefreshTick();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var managers = Selected().ToList();
        if (managers.Count == 0) return;
        var chk = new CheckBox { Content = P("Also delete the downloaded files from disk", "連同已下載嘅檔案一齊刪除") };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Remove {managers.Count} torrent(s)?", $"移除 {managers.Count} 個種子？"),
            Content = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new TextBlock { Text = P("The torrent(s) will be removed from the client.", "種子會由用戶端移除。"), TextWrapping = TextWrapping.Wrap },
                    chk,
                },
            },
            PrimaryButtonText = P("Remove · 移除", "移除"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        bool delData = chk.IsChecked == true;
        foreach (var m in managers) await _eng.RemoveAsync(m, delData);
        RefreshTick();
    }

    // ── Add magnet / file ───────────────────────────────────────────────────────

    private async void AddMagnet_Click(object sender, RoutedEventArgs e)
    {
        var box = new TextBox
        {
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 90,
            PlaceholderText = P("magnet:?xt=urn:btih:… (one per line)", "magnet:?xt=urn:btih:…（每行一個）"),
        };
        var (panel, save, start, seq) = BuildAddOptions();
        var content = new StackPanel { Spacing = 10, MinWidth = 480 };
        content.Children.Add(box);
        content.Children.Add(panel);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P("Add magnet link", "加磁力連結"), Content = content,
            PrimaryButtonText = P("Add · 加入", "加入"), CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrWhiteSpace(box.Text)) return;
        var dir = string.IsNullOrWhiteSpace(save.Text) ? _eng.DefaultSavePath : save.Text.Trim();
        int ok = 0, fail = 0;
        foreach (var line in box.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try { await _eng.AddMagnetAsync(line, dir, start.IsChecked == true); ok++; }
            catch { fail++; }
        }
        ShowStatus(fail == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
            P($"Added {ok} magnet(s){(fail > 0 ? $", {fail} failed" : "")}.", $"已加入 {ok} 個磁力{(fail > 0 ? $"，{fail} 個失敗" : "")}。"));
        RefreshTick();
    }

    private async void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var files = await FileDialogs.OpenFilesAsync(".torrent");
        if (files.Count == 0) return;
        var (panel, save, start, seq) = BuildAddOptions();
        var content = new StackPanel { Spacing = 10, MinWidth = 480 };
        content.Children.Add(new TextBlock
        {
            Text = P($"{files.Count} file(s): ", $"{files.Count} 個檔案：") + string.Join(", ", files.Select(Path.GetFileName)),
            TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        content.Children.Add(panel);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P("Add .torrent file(s)", "加 .torrent 檔"), Content = content,
            PrimaryButtonText = P("Add · 加入", "加入"), CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var dir = string.IsNullOrWhiteSpace(save.Text) ? _eng.DefaultSavePath : save.Text.Trim();
        int ok = 0, fail = 0;
        foreach (var f in files)
        {
            try { await _eng.AddTorrentFileAsync(f, dir, start.IsChecked == true); ok++; }
            catch { fail++; }
        }
        ShowStatus(fail == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
            P($"Added {ok} torrent(s){(fail > 0 ? $", {fail} failed" : "")}.", $"已加入 {ok} 個種子{(fail > 0 ? $"，{fail} 個失敗" : "")}。"));
        RefreshTick();
    }

    private (StackPanel panel, TextBox save, CheckBox start, CheckBox seq) BuildAddOptions()
    {
        var save = new TextBox
        {
            Header = P("Save folder · 儲存資料夾", "儲存資料夾"),
            Text = _eng.DefaultSavePath,
            PlaceholderText = _eng.DefaultSavePath,
        };
        var browse = new Button { Content = P("Browse… · 瀏覽…", "瀏覽…"), VerticalAlignment = VerticalAlignment.Bottom };
        browse.Click += async (_, _) => { var d = await FileDialogs.OpenFolderAsync(); if (d is not null) save.Text = d; };
        var saveRow = new Grid { ColumnSpacing = 8 };
        saveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        saveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(save, 0); Grid.SetColumn(browse, 1);
        saveRow.Children.Add(save); saveRow.Children.Add(browse);
        var start = new CheckBox { Content = P("Start downloading immediately", "立即開始下載"), IsChecked = true };
        var seq = new CheckBox { Content = P("Sequential download (note: best-effort)", "順序下載（盡力而為）") };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(saveRow); panel.Children.Add(start); panel.Children.Add(seq);
        return (panel, save, start, seq);
    }

    // ── Per-torrent detail (double-click / Details button) ──────────────────────

    private async void TorrentList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (TorrentList.SelectedItem is TorrentRowVM vm) await ShowDetail(vm.Manager);
    }

    private async void Detail_Click(object sender, RoutedEventArgs e)
    {
        if (TorrentList.SelectedItem is TorrentRowVM vm) await ShowDetail(vm.Manager);
    }

    private async Task ShowDetail(TorrentManager m)
    {
        var pivot = new Pivot { MinWidth = 600, MinHeight = 420 };

        // ── Files + per-file priority ──
        var filesPanel = new StackPanel { Spacing = 6 };
        var filesList = new ListView { SelectionMode = ListViewSelectionMode.None, MaxHeight = 360 };
        void BuildFiles()
        {
            filesList.Items.Clear();
            if (m.Files is null || m.Files.Count == 0)
            {
                filesList.Items.Add(new TextBlock { Text = P("(metadata not yet available)", "（中繼資料未到）"),
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
                return;
            }
            foreach (var f in m.Files)
            {
                var row = new Grid { ColumnSpacing = 10 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock
                {
                    Text = $"{f.Path}  ({TorrentEngineService.HumanSize(f.Length)})",
                    TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
                };
                ToolTipService.SetToolTip(name, f.Path);
                var combo = new ComboBox { MinWidth = 120 };
                combo.Items.Add(new ComboBoxItem { Content = P("Skip · 略過", "略過"), Tag = Priority.DoNotDownload });
                combo.Items.Add(new ComboBoxItem { Content = P("Normal · 正常", "正常"), Tag = Priority.Normal });
                combo.Items.Add(new ComboBoxItem { Content = P("High · 高", "高"), Tag = Priority.High });
                combo.SelectedIndex = f.Priority switch
                {
                    Priority.DoNotDownload => 0,
                    Priority.High or Priority.Highest or Priority.Immediate => 2,
                    _ => 1,
                };
                var captured = f;
                combo.SelectionChanged += async (_, _) =>
                {
                    if ((combo.SelectedItem as ComboBoxItem)?.Tag is Priority pr)
                        await _eng.SetFilePriorityAsync(m, captured, pr);
                };
                Grid.SetColumn(name, 0); Grid.SetColumn(combo, 1);
                row.Children.Add(name); row.Children.Add(combo);
                filesList.Items.Add(row);
            }
        }
        BuildFiles();
        filesPanel.Children.Add(filesList);
        pivot.Items.Add(new PivotItem { Header = P("Files · 檔案", "檔案"), Content = filesPanel });

        // ── Trackers ──
        var trkList = new ListView { SelectionMode = ListViewSelectionMode.None, MaxHeight = 360 };
        var trkItems = new List<string>();
        try
        {
            foreach (var tier in m.TrackerManager?.Tiers ?? new List<MonoTorrent.Trackers.TrackerTier>())
            foreach (var t in tier.Trackers)
            {
                var (en, zh) = TorrentEngineService.TrackerStateLabel(t.Status);
                var msg = string.IsNullOrEmpty(t.FailureMessage) ? "" : "  — " + t.FailureMessage;
                trkItems.Add($"[{P(en, zh)}] {t.Uri}{msg}");
            }
        }
        catch { }
        trkList.ItemsSource = trkItems.Count == 0 ? new List<string> { P("(DHT/LSD only — no trackers)", "（只用 DHT／LSD — 冇追蹤器）") } : trkItems;
        pivot.Items.Add(new PivotItem { Header = P("Trackers · 追蹤器", "追蹤器"), Content = trkList });

        // ── Peers ──
        var peerList = new ListView { SelectionMode = ListViewSelectionMode.None, MaxHeight = 360 };
        try
        {
            var peers = await m.GetPeersAsync();
            peerList.ItemsSource = peers.Count == 0
                ? new List<string> { P("(no connected peers)", "（冇連線嘅 peer）") }
                : peers.Select(p =>
                {
                    string client = "";
                    try { client = p.ClientApp.ShortId ?? p.ClientApp.Client.ToString(); } catch { }
                    string ip = p.Uri?.ToString() ?? "?";
                    return $"{ip}  {client}  ↓{TorrentEngineService.HumanSpeed(p.Monitor.DownloadRate)} ↑{TorrentEngineService.HumanSpeed(p.Monitor.UploadRate)}{(p.IsSeeder ? "  [seed]" : "")}";
                }).ToList();
        }
        catch { peerList.ItemsSource = new List<string> { P("(peers unavailable)", "（peer 無法取得）") }; }
        pivot.Items.Add(new PivotItem { Header = P("Peers · 對等", "對等"), Content = peerList });

        // ── Info / pieces ──
        var info = new StackPanel { Spacing = 8 };
        void Row(string label, string value) => info.Children.Add(new TextBlock { Text = $"{label}:  {value}", TextWrapping = TextWrapping.Wrap });
        Row(P("State", "狀態"), P(TorrentEngineService.StateLabel(m.State).En, TorrentEngineService.StateLabel(m.State).Zh));
        Row(P("Progress", "進度"), $"{m.Progress:0.0}%");
        Row(P("Size", "大細"), m.Torrent is null ? "—" : TorrentEngineService.HumanSize(m.Torrent.Size));
        Row(P("Pieces", "片段"), m.Torrent is null ? "—" : $"{m.Torrent.PieceCount} × {TorrentEngineService.HumanSize(m.Torrent.PieceLength)}");
        Row(P("Save path", "儲存路徑"), m.SavePath);
        Row(P("Downloaded", "已下載"), TorrentEngineService.HumanSize(m.Monitor.DataBytesReceived));
        Row(P("Uploaded", "已上傳"), TorrentEngineService.HumanSize(m.Monitor.DataBytesSent));
        Row(P("Ratio", "比率"), $"{TorrentEngineService.Ratio(m):0.00}");
        Row(P("Seeds / Peers", "種子／對等"), $"{m.Peers.Seeds} / {m.Peers.Available}");
        Row(P("Open connections", "開啟連線"), m.OpenConnections.ToString());
        Row("InfoHash", m.InfoHashes.V1OrV2.ToHex());

        var seqHint = new TextBlock
        {
            Text = P("Tip: skip files you don't want via the Files tab (priority Skip).", "提示：可喺「檔案」分頁將唔要嘅檔案設為「略過」。"),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], TextWrapping = TextWrapping.Wrap, FontSize = 12,
        };
        info.Children.Add(seqHint);
        pivot.Items.Add(new PivotItem { Header = P("Info · 資訊", "資訊"), Content = new ScrollViewer { Content = info } });

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = m.Name, Content = pivot,
            CloseButtonText = P("Close · 關閉", "關閉"),
        };
        await dlg.ShowAsync();
    }

    // ── Global settings ─────────────────────────────────────────────────────────

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var save = new TextBox { Header = P("Default save folder · 預設儲存資料夾", "預設儲存資料夾"), Text = _eng.DefaultSavePath };
        var browse = new Button { Content = P("Browse… · 瀏覽…", "瀏覽…"), VerticalAlignment = VerticalAlignment.Bottom };
        browse.Click += async (_, _) => { var d = await FileDialogs.OpenFolderAsync(); if (d is not null) save.Text = d; };
        var saveRow = new Grid { ColumnSpacing = 8 };
        saveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        saveRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(save, 0); Grid.SetColumn(browse, 1);
        saveRow.Children.Add(save); saveRow.Children.Add(browse);

        var dl = new NumberBox { Header = P("Max download (KiB/s, 0 = ∞) · 最高下載", "最高下載（KiB/s，0 = 無限）"), Value = _eng.MaxDownloadKiB, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var ul = new NumberBox { Header = P("Max upload (KiB/s, 0 = ∞) · 最高上傳", "最高上傳（KiB/s，0 = 無限）"), Value = _eng.MaxUploadKiB, Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var conn = new NumberBox { Header = P("Max connections · 最多連線", "最多連線"), Value = _eng.MaxConnections, Minimum = 10, Maximum = 2000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var port = new NumberBox { Header = P("Listen port · 監聽連接埠", "監聽連接埠"), Value = _eng.ListenPort, Minimum = 1, Maximum = 65535, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var dht = new ToggleSwitch { Header = P("DHT (trackerless peer discovery) · DHT", "DHT（無追蹤器搵 peer）"), IsOn = _eng.DhtEnabled };

        var panel = new StackPanel { Spacing = 12, MinWidth = 440 };
        panel.Children.Add(saveRow);
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(dl, 0); Grid.SetColumn(dl, 0);
        Grid.SetRow(ul, 0); Grid.SetColumn(ul, 1);
        Grid.SetRow(conn, 1); Grid.SetColumn(conn, 0);
        Grid.SetRow(port, 1); Grid.SetColumn(port, 1);
        grid.Children.Add(dl); grid.Children.Add(ul); grid.Children.Add(conn); grid.Children.Add(port);
        panel.Children.Add(grid);
        panel.Children.Add(dht);
        panel.Children.Add(new TextBlock
        {
            Text = P("Changing the port or DHT restarts the listeners. The engine, DHT, LSD, PEX and trackers are all part of the managed MonoTorrent library running inside WinForge.",
                "更改連接埠或 DHT 會重啟監聽器。引擎、DHT、LSD、PEX 同追蹤器全部係 WinForge 內運行嘅受控 MonoTorrent 程式庫一部分。"),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], TextWrapping = TextWrapping.Wrap, FontSize = 12,
        });

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P("Torrent settings · 種子設定", "種子設定"),
            Content = new ScrollViewer { Content = panel },
            PrimaryButtonText = P("Save · 儲存", "儲存"), CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        SettingsStore.Set(TorrentEngineService.KeySavePath, save.Text.Trim());
        SettingsStore.Set(TorrentEngineService.KeyMaxDown, ((int)Math.Max(0, dl.Value)).ToString());
        SettingsStore.Set(TorrentEngineService.KeyMaxUp, ((int)Math.Max(0, ul.Value)).ToString());
        SettingsStore.Set(TorrentEngineService.KeyMaxConn, ((int)Math.Max(10, conn.Value)).ToString());
        SettingsStore.Set(TorrentEngineService.KeyListenPort, ((int)Math.Max(1, port.Value)).ToString());
        SettingsStore.Set(TorrentEngineService.KeyDht, dht.IsOn ? "1" : "0");
        try
        {
            await _eng.ApplyGlobalSettingsAsync();
            ShowStatus(InfoBarSeverity.Success, P("Settings applied.", "已套用設定。"));
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, ex.Message); }
        RefreshTick();
    }

    private void ShowStatus(InfoBarSeverity sev, string message)
    {
        StatusBar.Severity = sev;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}

/// <summary>
/// 一行種子嘅檢視模型 · Observable view-model for one torrent row, refreshed in place each tick so the
/// ListView keeps selection and scroll position. Reads live values straight off the MonoTorrent manager.
/// </summary>
public sealed class TorrentRowVM : INotifyPropertyChanged
{
    public InfoHash Hash { get; }
    public TorrentManager Manager { get; }

    public TorrentRowVM(TorrentManager m, InfoHash hash) { Manager = m; Hash = hash; }

    public void Refresh()
    {
        foreach (var p in new[]
        {
            nameof(Name), nameof(SubLine), nameof(ProgressPct), nameof(ProgressText), nameof(StateText),
            nameof(StateBrush), nameof(DlText), nameof(UlText), nameof(EtaText), nameof(RatioText),
            nameof(SizeText), nameof(PeersText),
        })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public string Name => string.IsNullOrEmpty(Manager.Name) ? "(metadata…)" : Manager.Name;

    public string SubLine
    {
        get
        {
            var (en, zh) = TorrentEngineService.StateLabel(Manager.State);
            var bits = new List<string> { $"S {Manager.Peers.Seeds} / P {Manager.Peers.Available}" };
            if (Manager.OpenConnections > 0) bits.Add($"{Manager.OpenConnections} conn");
            return string.Join("   ", bits);
        }
    }

    public double ProgressPct => Math.Round(Manager.Progress, 1);
    public string ProgressText => $"{Manager.Progress:0.0}%  ·  {TorrentEngineService.HumanSize(Manager.Monitor.DataBytesReceived)}";
    public string StateText { get { var (en, zh) = TorrentEngineService.StateLabel(Manager.State); return Loc.I.Pick(en, zh); } }

    public Brush StateBrush
    {
        get
        {
            var key = Manager.State switch
            {
                TorrentState.Error => "SystemFillColorCriticalBrush",
                TorrentState.Downloading or TorrentState.Metadata or TorrentState.FetchingHashes => "SystemFillColorSuccessBrush",
                TorrentState.Seeding => "SystemFillColorAttentionBrush",
                TorrentState.Hashing or TorrentState.HashingPaused => "SystemFillColorCautionBrush",
                _ => "TextFillColorSecondaryBrush",
            };
            return (Brush)Application.Current.Resources[key];
        }
    }

    public string DlText => "↓ " + TorrentEngineService.HumanSpeed(Manager.Monitor.DownloadRate);
    public string UlText => "↑ " + TorrentEngineService.HumanSpeed(Manager.Monitor.UploadRate);
    public string EtaText => TorrentEngineService.HumanEta(Manager);
    public string RatioText
    {
        get { var r = TorrentEngineService.Ratio(Manager); return double.IsInfinity(r) ? "R ∞" : $"R {r:0.00}"; }
    }
    public string SizeText => Manager.Torrent is null ? "—" : TorrentEngineService.HumanSize(Manager.Torrent.Size);
    public string PeersText => $"{Manager.Peers.Seeds}↑ {Manager.Peers.Available}↓";

    public event PropertyChangedEventHandler? PropertyChanged;
}
