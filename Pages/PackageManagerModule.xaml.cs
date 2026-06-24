using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 套件管理（UniGetUI 式，多引擎）· In-app UniGetUI clone — one front-end over winget, Scoop, Chocolatey,
/// pip, npm, .NET tools, PowerShell Gallery and Cargo. Discover / Updates / Installed / Bundles / Setup,
/// with a per-manager filter, batch update, and bundle export/import. No redirects — wraps the real engines.
/// </summary>
public sealed partial class PackageManagerModule : Page
{
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _available = new(StringComparer.OrdinalIgnoreCase);
    private int _view; // 0 Discover, 1 Updates, 2 Installed, 3 Bundles, 4 Sources, 5 Ignored, 6 Setup
    private HashSet<string> _wingetInstalled = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _ignored = LoadIgnored();   // "key|id" of updates the user chose to ignore
    private readonly Dictionary<string, PackageItem> _selectedPkgs = new(StringComparer.OrdinalIgnoreCase); // 已勾選套件 · checked packages keyed by "manager|id"

    private static string PkgKey(PackageItem i) => $"{i.ManagerKey}|{i.Id}";

    private static HashSet<string> LoadIgnored()
    {
        var raw = SettingsStore.Get("pkg.ignored", "");
        return new HashSet<string>(raw.Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
    }
    private void SaveIgnored() => SettingsStore.Set("pkg.ignored", string.Join('\n', _ignored));
    private static string IgnKey(PackageItem i) => $"{i.ManagerKey}|{i.Id}";

    private sealed class BundleEntry
    {
        public string Manager { get; set; } = "";
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
    }

    public PackageManagerModule()
    {
        InitializeComponent();
        foreach (var m in PackageManagerRegistry.All) _selected.Add(m.Key);
        Loc.I.LanguageChanged += (_, _) => { Render(); BuildManagerFilters(); BuildViewCombo(); UpdateBatchBar(); };
        Loaded += async (_, _) =>
        {
            Render();
            BuildManagerFilters();
            BuildViewCombo();
            UpdateBatchBar();
            ViewCombo.SelectedIndex = 0;
            await CheckAvailability();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Package Manager · 套件管理";
        HeaderBlurb.Text = P(
            "A UniGetUI-style hub over winget, Scoop, Chocolatey, pip, npm, .NET tools, PowerShell Gallery, PowerShell 7, Cargo, Bun and vcpkg — discover, multi-select, batch install/update/uninstall, and export/import bundles, all in-app.",
            "UniGetUI 式總管，統一 winget、Scoop、Chocolatey、pip、npm、.NET 工具、PowerShell Gallery、PowerShell 7、Cargo、Bun 同 vcpkg — 搜尋、多選、批次安裝／更新／解除安裝，仲可以匯出／匯入清單，全部喺 app 內。");
        ManagersLabel.Text = P("Package managers", "套件管理器");
        SearchBox.PlaceholderText = P("Search packages (e.g. vscode, vlc, obs)…", "搜尋套件（例如 vscode、vlc、obs）…");
        TerminalBtnText.Text = P("Terminal", "終端機");
        ToolTipService.SetToolTip(TerminalBtn, P("Open a shell for manual winget / scoop / choco / pip / npm",
            "開一個 shell 手動執行 winget／scoop／choco／pip／npm"));
    }

    /// <summary>
    /// 開一個內嵌 shell · Open an embedded ConPTY shell so the user can drive winget / scoop / choco /
    /// pip / npm etc. by hand — a real terminal in-app, no external window. 喺呢度開終端機。
    /// </summary>
    private async void Terminal_Click(object sender, RoutedEventArgs e)
        => await TerminalLauncher.OpenEmbeddedAsync(this.XamlRoot,
            P("Package shell · 套件 shell", "套件 shell · Package shell"),
            commandLine: null,
            workingDir: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private void BuildViewCombo()
    {
        int sel = ViewCombo.SelectedIndex;
        ViewCombo.Items.Clear();
        ViewCombo.Items.Add(P("Discover", "搜尋安裝"));
        ViewCombo.Items.Add(P("Updates", "可更新"));
        ViewCombo.Items.Add(P("Installed", "已安裝"));
        ViewCombo.Items.Add(P("Bundles", "套件清單"));
        ViewCombo.Items.Add(P("Sources", "來源"));
        ViewCombo.Items.Add(P("Ignored", "已忽略"));
        ViewCombo.Items.Add(P("Setup", "設定引擎"));
        ViewCombo.SelectedIndex = sel < 0 ? 0 : sel;
    }

    private void BuildManagerFilters()
    {
        ManagerFilters.Children.Clear();
        foreach (var m in PackageManagerRegistry.All)
        {
            string key = m.Key;
            bool avail = _available.TryGetValue(key, out var a) && a;
            bool known = _available.ContainsKey(key);
            var cb = new CheckBox
            {
                Content = known && !avail ? $"{m.NameEn} · {m.NameZh}  {P("(not found)", "（搵唔到）")}" : $"{m.NameEn} · {m.NameZh}",
                IsChecked = _selected.Contains(key),
                IsEnabled = !known || avail,
                Tag = key,
            };
            cb.Checked += (_, _) => _selected.Add(key);
            cb.Unchecked += (_, _) => _selected.Remove(key);
            ManagerFilters.Children.Add(cb);
        }
    }

    private async Task CheckAvailability()
    {
        Busy.IsActive = true;
        try
        {
            foreach (var m in PackageManagerRegistry.All)
            {
                bool ok;
                try { ok = await m.IsAvailableAsync(CancellationToken.None); }
                catch { ok = false; }
                _available[m.Key] = ok;
                if (!ok) _selected.Remove(m.Key);
            }
        }
        finally { Busy.IsActive = false; }
        BuildManagerFilters();
        await LoadView();
    }

    private List<string> SelectedAvailable() =>
        PackageManagerRegistry.All
            .Where(m => _selected.Contains(m.Key) && _available.TryGetValue(m.Key, out var a) && a)
            .Select(m => m.Key).ToList();

    private async void View_Changed(object sender, SelectionChangedEventArgs e)
    {
        _view = ViewCombo.SelectedIndex < 0 ? 0 : ViewCombo.SelectedIndex;
        await LoadView();
    }

    private async Task LoadView()
    {
        ResultsPanel.Children.Clear();
        ResultsHeader.Text = "";
        ClearSelection(); // 切換檢視就清空多選 · switching views clears the multi-select set
        switch (_view)
        {
            case 0: // Discover
                SearchBox.IsEnabled = true;
                PrimaryActionBtn.Content = P("Search", "搜尋");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Visibility = Visibility.Collapsed;
                ResultsHeader.Text = P("Type a query and press Enter, or click Search.", "輸入關鍵字撳 Enter，或者撳搜尋。");
                break;
            case 1: // Updates
                SearchBox.IsEnabled = false;
                PrimaryActionBtn.Content = P("Refresh", "重新整理");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Content = P("Update all", "全部更新");
                SecondaryActionBtn.Visibility = Visibility.Visible;
                await LoadUpdates();
                break;
            case 2: // Installed
                SearchBox.IsEnabled = false;
                PrimaryActionBtn.Content = P("Refresh", "重新整理");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Visibility = Visibility.Collapsed;
                await LoadInstalled();
                break;
            case 3: // Bundles
                SearchBox.IsEnabled = false;
                PrimaryActionBtn.Content = P("Export…", "匯出…");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Content = P("Import…", "匯入…");
                SecondaryActionBtn.Visibility = Visibility.Visible;
                ResultsHeader.Text = P("Export your installed packages to a JSON bundle, or import one to reinstall.",
                    "將已安裝套件匯出做 JSON 清單，或者匯入一個嚟重新安裝。");
                break;
            case 4: // Sources
                SearchBox.IsEnabled = false;
                PrimaryActionBtn.Content = P("Refresh", "重新整理");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Visibility = Visibility.Collapsed;
                await LoadSources();
                break;
            case 5: // Ignored
                SearchBox.IsEnabled = false;
                PrimaryActionBtn.Content = P("Refresh", "重新整理");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Visibility = Visibility.Collapsed;
                LoadIgnoredView();
                break;
            case 6: // Setup
                SearchBox.IsEnabled = false;
                PrimaryActionBtn.Content = P("Install all deps", "安裝全部相依");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Visibility = Visibility.Collapsed;
                await LoadSetup();
                break;
        }
    }

    private async void Search_Submitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (_view == 0) await DoSearch((args.QueryText ?? "").Trim());
    }

    private async void PrimaryAction_Click(object sender, RoutedEventArgs e)
    {
        switch (_view)
        {
            case 0: await DoSearch((SearchBox.Text ?? "").Trim()); break;
            case 1: await LoadUpdates(); break;
            case 2: await LoadInstalled(); break;
            case 3: await ExportBundle(); break;
            case 4: await LoadSources(); break;
            case 5: LoadIgnoredView(); break;
            case 6: await InstallAllDeps(); break;
        }
    }

    private async void SecondaryAction_Click(object sender, RoutedEventArgs e)
    {
        switch (_view)
        {
            case 1: await UpdateAll(); break;
            case 3: await ImportBundle(); break;
        }
    }

    // ===== Discover =====

    private async Task DoSearch(string query)
    {
        if (query.Length < 2)
        {
            ResultsHeader.Text = P("Enter at least 2 characters.", "請輸入最少 2 個字元。");
            return;
        }
        var keys = SelectedAvailable();
        if (keys.Count == 0) { ResultsHeader.Text = P("No managers selected/available.", "未揀／冇可用嘅管理器。"); return; }

        ResultsPanel.Children.Clear();
        ResultsHeader.Text = P("Searching…", "搜尋緊…");
        Busy.IsActive = true;
        List<PackageItem> results;
        try { results = await PackageManagerRegistry.SearchAllAsync(query, keys, CancellationToken.None); }
        catch { results = new(); }
        Busy.IsActive = false;

        ResultsHeader.Text = P($"Results — {results.Count}", $"結果 — {results.Count}");
        foreach (var item in results)
        {
            var extras = new List<(string, Func<Button, Task>)>
            {
                (P("Options…", "選項…"), async _ => await ShowOptionsDialog(item)),
            };
            ResultsPanel.Children.Add(RowFor(item, P("Install", "安裝"), async btn => await ActionInstall(item, btn), extras));
        }
    }

    // ===== Updates =====

    private async Task LoadUpdates()
    {
        var keys = SelectedAvailable();
        ResultsPanel.Children.Clear();
        ResultsHeader.Text = P("Checking for updates…", "檢查更新緊…");
        Busy.IsActive = true;
        List<PackageItem> ups;
        try { ups = await PackageManagerRegistry.AllUpdatesAsync(keys, CancellationToken.None); }
        catch { ups = new(); }
        Busy.IsActive = false;
        var shown = ups.Where(u => !_ignored.Contains(IgnKey(u))).ToList();
        int hidden = ups.Count - shown.Count;
        ResultsHeader.Text = hidden > 0
            ? P($"Updatable — {shown.Count} ({hidden} ignored)", $"可更新 — {shown.Count}（已忽略 {hidden}）")
            : P($"Updatable — {shown.Count}", $"可更新 — {shown.Count}");

        // 每個管理器一個「全部更新」捷徑 · per-manager "update all" shortcuts (UniGetUI parity).
        var byMgr = shown.GroupBy(u => u.ManagerKey).Where(g => g.Count() > 1).ToList();
        if (byMgr.Count > 0)
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            bar.Children.Add(new TextBlock { Text = P("Update all:", "全部更新："), VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
            foreach (var g in byMgr)
            {
                string mk = g.Key;
                var m = PackageManagerRegistry.ByKey(mk);
                var b = new Button { Content = $"{m?.NameEn ?? mk} ({g.Count()})", Padding = new Thickness(10, 4, 10, 4) };
                b.Click += async (_, _) =>
                {
                    b.IsEnabled = false; b.Content = P("Updating…", "更新緊…");
                    var (d, t) = await PackageManagerRegistry.UpdateAllForManagerAsync(mk, null, CancellationToken.None);
                    ResultsHeader.Text = P($"{mk}: updated {d}/{t}.", $"{mk}：更新咗 {d}/{t}。");
                    await LoadUpdates();
                };
                bar.Children.Add(b);
            }
            ResultsPanel.Children.Add(Card(bar));
        }

        foreach (var item in shown)
        {
            var label = string.IsNullOrEmpty(item.AvailableVersion) ? P("Update", "更新") : $"{P("Update", "更新")} → {item.AvailableVersion}";
            var extras = new List<(string, Func<Button, Task>)>
            {
                (P("Ignore", "忽略"), _ => { _ignored.Add(IgnKey(item)); SaveIgnored(); return LoadUpdates(); }),
            };
            ResultsPanel.Children.Add(RowFor(item, label, async btn => await ActionUpdate(item, btn), extras));
        }
    }

    private async Task UpdateAll()
    {
        var keys = SelectedAvailable();
        Busy.IsActive = true;
        List<PackageItem> ups;
        try { ups = await PackageManagerRegistry.AllUpdatesAsync(keys, CancellationToken.None); }
        catch { ups = new(); }
        Busy.IsActive = false;
        int done = 0;
        foreach (var item in ups)
        {
            var mgr = PackageManagerRegistry.ByKey(item.ManagerKey);
            if (mgr is null) continue;
            ResultsHeader.Text = P($"Updating {item.Name}… ({done + 1}/{ups.Count})", $"更新緊 {item.Name}…（{done + 1}/{ups.Count}）");
            try { var r = await mgr.UpdateAsync(item.Id, CancellationToken.None); if (r.Success) done++; } catch { }
        }
        ResultsHeader.Text = P($"Updated {done}/{ups.Count}.", $"更新咗 {done}/{ups.Count}。");
        await LoadUpdates();
    }

    // ===== Installed =====

    private async Task LoadInstalled()
    {
        var keys = SelectedAvailable();
        ResultsPanel.Children.Clear();
        ResultsHeader.Text = P("Listing installed packages…", "列出已安裝套件緊…");
        Busy.IsActive = true;
        List<PackageItem> items;
        try { items = await PackageManagerRegistry.AllInstalledAsync(keys, CancellationToken.None); }
        catch { items = new(); }
        Busy.IsActive = false;
        ResultsHeader.Text = P($"Installed — {items.Count}", $"已安裝 — {items.Count}");
        foreach (var item in items)
            ResultsPanel.Children.Add(RowFor(item, P("Uninstall", "解除安裝"), async btn => await ActionUninstall(item, btn)));
    }

    // ===== Batch (multi-select) operations — UniGetUI signature =====

    /// <summary>更新批次列嘅標籤同顯示／隱藏 · Refresh the batch bar's button labels and visibility.</summary>
    private void UpdateBatchBar()
    {
        int n = _selectedPkgs.Count;
        BatchBar.Visibility = n > 0 ? Visibility.Visible : Visibility.Collapsed;
        BatchLabel.Text = P($"{n} selected", $"已選 {n} 個");
        BatchInstallBtn.Content = P("Install selected", "安裝所選");
        BatchUpdateBtn.Content = P("Update selected", "更新所選");
        BatchUninstallBtn.Content = P("Uninstall selected", "解除所選");
        BatchExportBtn.Content = P("Export selected…", "匯出所選…");
        BatchClearBtn.Content = P("Clear", "清除");
    }

    private void ClearSelection()
    {
        _selectedPkgs.Clear();
        UpdateBatchBar();
    }

    private void BatchClear_Click(object sender, RoutedEventArgs e)
    {
        ClearSelection();
        // 取消畫面上所有勾選 · uncheck every visible row
        foreach (var cb in EnumerateCheckBoxes(ResultsPanel)) cb.IsChecked = false;
    }

    private static IEnumerable<CheckBox> EnumerateCheckBoxes(DependencyObject root)
    {
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is CheckBox cb) yield return cb;
            foreach (var inner in EnumerateCheckBoxes(child)) yield return inner;
        }
    }

    private async void BatchInstall_Click(object sender, RoutedEventArgs e)
        => await RunBatchWithLog(P("Install selected", "安裝所選"),
            _selectedPkgs.Values.ToList(), (m, id, ct) => m.InstallAsync(id, ct));

    private async void BatchUpdate_Click(object sender, RoutedEventArgs e)
        => await RunBatchWithLog(P("Update selected", "更新所選"),
            _selectedPkgs.Values.ToList(), (m, id, ct) => m.UpdateAsync(id, ct));

    private async void BatchUninstall_Click(object sender, RoutedEventArgs e)
        => await RunBatchWithLog(P("Uninstall selected", "解除所選"),
            _selectedPkgs.Values.ToList(), (m, id, ct) => m.UninstallAsync(id, ct));

    private async void BatchExport_Click(object sender, RoutedEventArgs e)
        => await ExportEntries(_selectedPkgs.Values.ToList());

    /// <summary>
    /// 跑一批操作並即時顯示進度同輸出 · Run a batch op over the selected packages, streaming progress and
    /// the CLI output into a live log dialog (UniGetUI-style operation feed). Cancellable.
    /// </summary>
    private async Task RunBatchWithLog(string title, List<PackageItem> items,
        Func<IPackageManager, string, CancellationToken, Task<TweakResult>> op)
    {
        if (items.Count == 0) return;

        var cts = new CancellationTokenSource();
        var log = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"), FontSize = 12,
            TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
        };
        var scroll = new ScrollViewer { MaxHeight = 420, MinWidth = 520, Content = log };
        var dlg = new ContentDialog
        {
            Title = title,
            Content = scroll,
            PrimaryButtonText = P("Run in background", "背景執行"),
            CloseButtonText = P("Cancel", "取消"),
            XamlRoot = this.XamlRoot,
        };
        dlg.CloseButtonClick += (_, _) => cts.Cancel();

        void Append(string line)
        {
            log.Text += (log.Text.Length == 0 ? "" : "\n") + line;
            scroll.ChangeView(null, scroll.ScrollableHeight + 400, null, true);
        }

        var run = Task.Run(async () =>
        {
            int done = 0, fail = 0, idx = 0;
            foreach (var item in items)
            {
                if (cts.IsCancellationRequested) break;
                idx++;
                var mgr = PackageManagerRegistry.ByKey(item.ManagerKey);
                DispatcherQueue.TryEnqueue(() => Append(P($"[{idx}/{items.Count}] {item.Name} ({item.ManagerKey})…", $"[{idx}/{items.Count}] {item.Name}（{item.ManagerKey}）…")));
                if (mgr is null) { fail++; continue; }
                TweakResult r;
                try { r = await op(mgr, item.Id, cts.Token); }
                catch (Exception ex) { r = TweakResult.Fail(ex.Message, ex.Message); }
                if (r.Success) done++; else fail++;
                var tail = string.IsNullOrWhiteSpace(r.Output) ? "" : "\n    " + r.Output.Replace("\n", "\n    ");
                DispatcherQueue.TryEnqueue(() => Append((r.Success ? P("  ✓ OK", "  ✓ 完成") : P("  ✗ failed", "  ✗ 失敗")) + tail));
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                Append(P($"Done — {done} ok, {fail} failed.", $"完成 — {done} 成功、{fail} 失敗。"));
                ResultsHeader.Text = P($"Batch: {done} ok, {fail} failed.", $"批次：{done} 成功、{fail} 失敗。");
            });
        }, cts.Token);

        await dlg.ShowAsync();
        // 對話框關閉後唔阻塞操作（PrimaryButton = 背景執行）· keep running after dialog closes.
        _ = run;
    }

    // ===== Bundles =====

    private async Task ExportBundle()
    {
        var keys = SelectedAvailable();
        Busy.IsActive = true;
        List<PackageItem> items;
        try { items = await PackageManagerRegistry.AllInstalledAsync(keys, CancellationToken.None); }
        catch { items = new(); }
        Busy.IsActive = false;
        await ExportEntries(items);
    }

    /// <summary>將一組套件寫做 JSON 清單（已安裝或所選共用）· Write a set of packages to a JSON bundle (shared by export-all and export-selected).</summary>
    private async Task ExportEntries(List<PackageItem> items)
    {
        if (items.Count == 0) { ResultsHeader.Text = P("Nothing to export.", "冇嘢可以匯出。"); return; }
        var entries = items.Select(i => new BundleEntry { Manager = i.ManagerKey, Id = i.Id, Name = i.Name, Version = i.Version }).ToList();
        try
        {
            var path = await FileDialogs.SaveFileAsync("winforge-packages", ".json");
            if (path is null) return;
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
            ResultsHeader.Text = P($"Exported {entries.Count} package(s).", $"匯出咗 {entries.Count} 個套件。");
        }
        catch (Exception ex) { ResultsHeader.Text = ex.Message; }
    }

    private async Task ImportBundle()
    {
        List<BundleEntry>? entries = null;
        try
        {
            var path = await FileDialogs.OpenFileAsync(".json");
            if (path is null) return;
            var json = await File.ReadAllTextAsync(path);
            entries = JsonSerializer.Deserialize<List<BundleEntry>>(json);
        }
        catch (Exception ex) { ResultsHeader.Text = ex.Message; return; }
        if (entries is null || entries.Count == 0) { ResultsHeader.Text = P("Bundle is empty.", "清單係空嘅。"); return; }

        ResultsPanel.Children.Clear();
        int done = 0;
        Busy.IsActive = true;
        foreach (var en in entries)
        {
            var mgr = PackageManagerRegistry.ByKey(en.Manager);
            if (mgr is null || !(_available.TryGetValue(en.Manager, out var a) && a)) continue;
            ResultsHeader.Text = P($"Installing {en.Name}… ({done + 1}/{entries.Count})", $"安裝緊 {en.Name}…（{done + 1}/{entries.Count}）");
            try { var r = await mgr.InstallAsync(en.Id, CancellationToken.None); if (r.Success) done++; } catch { }
        }
        Busy.IsActive = false;
        ResultsHeader.Text = P($"Installed {done}/{entries.Count} from bundle.", $"由清單安裝咗 {done}/{entries.Count}。");
    }

    // ===== Setup =====

    private async Task LoadSetup()
    {
        ResultsPanel.Children.Clear();
        ResultsHeader.Text = P("Engines & common dependencies", "引擎同常用相依");

        // Manager availability + bootstrap helpers.
        ResultsPanel.Children.Add(SectionLabel(P("Package managers", "套件管理器")));
        foreach (var m in PackageManagerRegistry.All)
        {
            bool avail = _available.TryGetValue(m.Key, out var a) && a;
            string status = avail ? P("Available", "可用") : P("Not installed", "未安裝");
            var (canBootstrap, bootstrap) = Bootstrap(m.Key);
            ResultsPanel.Children.Add(StatusRow($"{m.NameEn} · {m.NameZh}", m.Key, status, avail,
                avail || !canBootstrap ? null : (P("Install", "安裝"), bootstrap)));
        }

        // Curated common dependencies via winget (kept from the classic view).
        ResultsPanel.Children.Add(SectionLabel(P("Common dependencies (winget)", "常用相依（winget）")));
        try { _wingetInstalled = await PackageService.InstalledIds(); } catch { }
        foreach (var dep in PackageService.Deps)
        {
            bool installed = _wingetInstalled.Contains(dep.Id);
            ResultsPanel.Children.Add(StatusRow($"{dep.En} · {dep.Zh}", dep.Id,
                installed ? P("Installed", "已安裝") : P("Missing", "欠缺"), installed,
                installed ? null : (P("Install", "安裝"), async () => { await PackageService.Install(dep.Id); _wingetInstalled.Add(dep.Id); })));
        }

        // UniGetUI 後備：原生包唔到嘅進階功能可以開返 UniGetUI 本體 · Fallback to UniGetUI itself for power features.
        ResultsPanel.Children.Add(SectionLabel(P("UniGetUI (power features)", "UniGetUI（進階功能）")));
        ResultsPanel.Children.Add(StatusRow(
            "UniGetUI · 統一套件管理 GUI", "MartiCliment.UniGetUI",
            P("Open or install the full UniGetUI app", "開啟或安裝完整 UniGetUI 應用程式"), false,
            (P("Launch / Install", "啟動／安裝"), async () =>
            {
                var r = await PackageManagerRegistry.LaunchUniGetUIAsync(CancellationToken.None);
                ResultsHeader.Text = r.Message?.Primary ?? (r.Success ? P("Done.", "完成。") : P("Failed.", "失敗。"));
            })));
    }

    private async Task InstallAllDeps()
    {
        PrimaryActionBtn.IsEnabled = false;
        Busy.IsActive = true;
        try
        {
            foreach (var dep in PackageService.Deps)
            {
                if (_wingetInstalled.Contains(dep.Id)) continue;
                ResultsHeader.Text = P($"Installing {dep.En}…", $"安裝緊 {dep.En}…");
                var r = await PackageService.Install(dep.Id);
                if (r.Success) _wingetInstalled.Add(dep.Id);
            }
        }
        finally { Busy.IsActive = false; PrimaryActionBtn.IsEnabled = true; }
        await LoadSetup();
    }

    /// <summary>Per-manager bootstrap so users can install a missing engine in one click.</summary>
    private (bool, Func<Task>) Bootstrap(string key) => key switch
    {
        "scoop" => (true, async () => await ShellRunner.RunPowershell(
            "Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force; Invoke-RestMethod -Uri https://get.scoop.sh | Invoke-Expression", false)),
        "choco" => (true, async () => await ShellRunner.RunPowershell(
            "Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = 3072; Invoke-Expression ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))", true)),
        "pip" => (true, async () => await PackageService.Install("Python.Python.3.12")),
        "npm" => (true, async () => await PackageService.Install("OpenJS.NodeJS.LTS")),
        "dotnet" => (true, async () => await PackageService.Install("Microsoft.DotNet.SDK.9")),
        "cargo" => (true, async () => await PackageService.Install("Rustlang.Rustup")),
        "bun" => (true, async () => await PackageService.Install("Oven-sh.Bun")),
        "pwsh7" => (true, async () => await PackageService.Install("Microsoft.PowerShell")),
        _ => (false, () => Task.CompletedTask),
    };

    // ===== Sources =====

    private async Task LoadSources()
    {
        var keys = SelectedAvailable();
        ResultsPanel.Children.Clear();
        if (keys.Count == 0) { ResultsHeader.Text = P("No managers selected/available.", "未揀／冇可用嘅管理器。"); return; }
        ResultsHeader.Text = P("Sources / buckets / feeds per manager — add, remove or refresh", "各管理器嘅來源／bucket／feed — 可加、移除或重新整理");
        Busy.IsActive = true;
        foreach (var key in keys)
        {
            var m = PackageManagerRegistry.ByKey(key);
            ResultsPanel.Children.Add(SectionLabel($"{m?.NameEn} · {m?.NameZh}"));

            // 每個管理器嘅工具列：加來源… / 重新整理 · per-manager toolbar: Add source… / Refresh.
            bool canAdd = SourceManager.CanAddRemove(key);
            bool canRefresh = SourceManager.CanRefresh(key);
            if (canAdd || canRefresh)
            {
                var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 2) };
                if (canAdd)
                {
                    var addBtn = new Button { Content = P("Add source…", "加來源…"), Padding = new Thickness(12, 4, 12, 4) };
                    addBtn.Click += async (_, _) => await AddSourceFor(key);
                    bar.Children.Add(addBtn);
                }
                if (canRefresh)
                {
                    var refreshBtn = new Button { Content = P("Refresh", "重新整理"), Padding = new Thickness(12, 4, 12, 4) };
                    refreshBtn.Click += async (_, _) => await RefreshSourcesFor(key, refreshBtn);
                    bar.Children.Add(refreshBtn);
                }
                if (SourceManager.RequiresAdmin(key))
                    bar.Children.Add(new TextBlock
                    {
                        Text = P("(needs admin)", "（需要管理員）"),
                        VerticalAlignment = VerticalAlignment.Center, FontSize = 11,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    });
                ResultsPanel.Children.Add(bar);
            }

            // 列出結構化來源 · list structured sources.
            List<SourceManager.SourceInfo> sources;
            try { sources = await SourceManager.ListAsync(key, CancellationToken.None); }
            catch { sources = new(); }

            if (sources.Count == 0)
            {
                ResultsPanel.Children.Add(Card(new TextBlock
                {
                    Text = P("(no sources)", "（冇來源）"),
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    FontSize = 12,
                }));
                continue;
            }

            foreach (var src in sources)
                ResultsPanel.Children.Add(SourceCard(key, src, canAdd));
        }
        Busy.IsActive = false;
    }

    /// <summary>一張來源卡：名、URL、套件數／更新日期，連「移除」掣 · One source card: name, URL, count/date and a Remove button.</summary>
    private Border SourceCard(string managerKey, SourceManager.SourceInfo src, bool canRemove)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock
        {
            Text = src.Name, FontWeight = FontWeights.SemiBold, FontSize = 13,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(src.Url))
            texts.Children.Add(new TextBlock
            {
                Text = src.Url, FontSize = 11, FontFamily = new FontFamily("Consolas"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
            });

        // 套件數／更新日期（有先顯示）· package count / updated date when present.
        var meta = new List<string>();
        if (!string.IsNullOrWhiteSpace(src.PackageCount)) meta.Add(P($"{src.PackageCount} packages", $"{src.PackageCount} 個套件"));
        if (!string.IsNullOrWhiteSpace(src.UpdatedDate)) meta.Add(P($"updated {src.UpdatedDate}", $"更新於 {src.UpdatedDate}"));
        if (meta.Count > 0)
            texts.Children.Add(new TextBlock
            {
                Text = string.Join("  ·  ", meta), FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
        Grid.SetColumn(texts, 0);
        grid.Children.Add(texts);

        if (canRemove)
        {
            var removeBtn = new Button { Content = P("Remove", "移除"), Padding = new Thickness(12, 4, 12, 4), VerticalAlignment = VerticalAlignment.Center };
            removeBtn.Click += async (_, _) =>
            {
                removeBtn.IsEnabled = false; removeBtn.Content = P("Removing…", "移除緊…");
                ResultsHeader.Text = P($"Removing {src.Name}…", $"移除緊 {src.Name}…");
                TweakResult r;
                try { r = await SourceManager.RemoveAsync(managerKey, src.Name, CancellationToken.None); }
                catch (Exception ex) { r = TweakResult.Fail(ex.Message, ex.Message); }
                ResultsHeader.Text = r.Success
                    ? P($"Removed {src.Name}.", $"已移除 {src.Name}。")
                    : P($"Could not remove {src.Name}.", $"無法移除 {src.Name}。");
                await LoadSources();
            };
            Grid.SetColumn(removeBtn, 1);
            grid.Children.Add(removeBtn);
        }

        return Card(grid);
    }

    /// <summary>開「加來源」對話框並執行 · Open the Add-source dialog and run the add.</summary>
    private async Task AddSourceFor(string managerKey)
    {
        var picked = await AddSourceDialog.ShowAsync(this.XamlRoot, managerKey);
        if (picked is null) return;
        var (name, url) = picked.Value;
        ResultsHeader.Text = P($"Adding {name}…", $"加入緊 {name}…");
        Busy.IsActive = true;
        try
        {
            var r = await SourceManager.AddAsync(managerKey, name, url, CancellationToken.None);
            ResultsHeader.Text = r.Success
                ? P($"Added {name}.", $"已加入 {name}。")
                : P($"Could not add {name}.", $"無法加入 {name}。");
        }
        catch (Exception ex) { ResultsHeader.Text = ex.Message; }
        finally { Busy.IsActive = false; }
        await LoadSources();
    }

    /// <summary>重新整理一個管理器嘅來源索引 · Refresh one manager's source indexes.</summary>
    private async Task RefreshSourcesFor(string managerKey, Button btn)
    {
        btn.IsEnabled = false; btn.Content = P("Refreshing…", "整理緊…");
        ResultsHeader.Text = P("Refreshing sources…", "重新整理來源緊…");
        try
        {
            var r = await SourceManager.RefreshAsync(managerKey, CancellationToken.None);
            ResultsHeader.Text = r.Success
                ? P("Sources refreshed.", "來源已重新整理。")
                : P("Refresh failed.", "重新整理失敗。");
        }
        catch (Exception ex) { ResultsHeader.Text = ex.Message; }
        await LoadSources();
    }

    // ===== Ignored updates =====

    private void LoadIgnoredView()
    {
        ResultsPanel.Children.Clear();
        ResultsHeader.Text = P($"Ignored updates — {_ignored.Count}", $"已忽略更新 — {_ignored.Count}");
        if (_ignored.Count == 0)
        {
            ResultsPanel.Children.Add(new TextBlock
            {
                Text = P("Nothing ignored. Use “Ignore” on an update to hide it here.", "冇忽略項目。喺更新撳「忽略」就會收喺呢度。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], Margin = new Thickness(4, 8, 0, 0),
            });
            return;
        }
        foreach (var key in _ignored.ToList())
        {
            var parts = key.Split('|', 2);
            var item = new PackageItem { ManagerKey = parts.Length > 0 ? parts[0] : "", Id = parts.Length > 1 ? parts[1] : key, Name = parts.Length > 1 ? parts[1] : key };
            ResultsPanel.Children.Add(RowFor(item, P("Un-ignore", "取消忽略"),
                _ => { _ignored.Remove(key); SaveIgnored(); LoadIgnoredView(); return Task.CompletedTask; }));
        }
    }

    // ===== Details / install options dialogs =====

    private async Task ShowDetails(PackageItem item)
    {
        var body = new TextBlock
        {
            Text = P("Loading…", "載入緊…"), FontFamily = new FontFamily("Consolas"),
            FontSize = 12, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
        };
        var dlg = new ContentDialog
        {
            Title = $"{item.Name} · {item.ManagerKey}",
            Content = new ScrollViewer { MaxHeight = 460, Content = body },
            CloseButtonText = P("Close", "關閉"),
            XamlRoot = this.XamlRoot,
        };
        _ = dlg.ShowAsync();
        try
        {
            var text = await PackageManagerRegistry.DetailsAsync(item);
            body.Text = string.IsNullOrWhiteSpace(text) ? P("No details available.", "冇詳情。") : text.Trim();
        }
        catch (Exception ex) { body.Text = ex.Message; }
    }

    private async Task ShowOptionsDialog(PackageItem item)
    {
        // 載入每個套件嘅選項（無覆寫就跟全域）· Load per-package options (follows global if no override).
        var opts = InstallOptions.Load(item.ManagerKey, item.Id);
        var confirmed = await InstallOptionsDialog.ShowAsync(
            this.XamlRoot, item, opts, PackageOperations.Op.Install);
        if (!confirmed) return;

        // 確認後重新載入（對話框已經寫咗覆寫或重設）· Reload after confirm; dialog persisted the choice.
        var effective = InstallOptions.Load(item.ManagerKey, item.Id);
        ResultsHeader.Text = P($"Installing {item.Name}…", $"安裝緊 {item.Name}…");
        Busy.IsActive = true;
        try
        {
            var r = await PackageOperations.RunAsync(
                item.ManagerKey, item.Id, PackageOperations.Op.Install, effective, CancellationToken.None);
            ResultsHeader.Text = r.Success
                ? P($"Installed {item.Name}.", $"已安裝 {item.Name}。")
                : P($"Install failed for {item.Name}.", $"{item.Name} 安裝失敗。");
        }
        catch (Exception ex) { ResultsHeader.Text = ex.Message; }
        finally { Busy.IsActive = false; }
    }

    // ===== Row builders =====

    private async Task ActionInstall(PackageItem item, Button btn)
    {
        var mgr = PackageManagerRegistry.ByKey(item.ManagerKey);
        if (mgr is null) return;
        btn.IsEnabled = false; btn.Content = P("Installing…", "安裝緊…");
        var r = await mgr.InstallAsync(item.Id, CancellationToken.None);
        btn.Content = r.Success ? P("Installed", "已安裝") : P("Retry", "重試");
        btn.IsEnabled = !r.Success;
    }

    private async Task ActionUpdate(PackageItem item, Button btn)
    {
        var mgr = PackageManagerRegistry.ByKey(item.ManagerKey);
        if (mgr is null) return;
        btn.IsEnabled = false; btn.Content = P("Updating…", "更新緊…");
        var r = await mgr.UpdateAsync(item.Id, CancellationToken.None);
        btn.Content = r.Success ? P("Updated", "已更新") : P("Retry", "重試");
        btn.IsEnabled = !r.Success;
    }

    private async Task ActionUninstall(PackageItem item, Button btn)
    {
        var mgr = PackageManagerRegistry.ByKey(item.ManagerKey);
        if (mgr is null) return;
        btn.IsEnabled = false; btn.Content = P("Removing…", "移除緊…");
        var r = await mgr.UninstallAsync(item.Id, CancellationToken.None);
        btn.Content = r.Success ? P("Removed", "已移除") : P("Retry", "重試");
        btn.IsEnabled = !r.Success;
    }

    private TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        FontSize = 14,
        Margin = new Thickness(0, 10, 0, 2),
    };

    private Border RowFor(PackageItem item, string actionLabel, Func<Button, Task> action,
        List<(string label, Func<Button, Task> run)>? extras = null)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // checkbox
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // badge
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // 多選勾選框 · multi-select checkbox driving batch operations
        var check = new CheckBox
        {
            MinWidth = 0,
            Margin = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = _selectedPkgs.ContainsKey(PkgKey(item)),
        };
        check.Checked += (_, _) => { _selectedPkgs[PkgKey(item)] = item; UpdateBatchBar(); };
        check.Unchecked += (_, _) => { _selectedPkgs.Remove(PkgKey(item)); UpdateBatchBar(); };
        Grid.SetColumn(check, 0);
        grid.Children.Add(check);

        var badge = ManagerBadge(item.ManagerKey);
        Grid.SetColumn(badge, 1);
        grid.Children.Add(badge);

        var texts = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var ver = string.IsNullOrEmpty(item.Version) ? "" : $"  ({item.Version})";
        texts.Children.Add(new TextBlock { Text = $"{item.Name}{ver}", FontWeight = FontWeights.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
        var sub = string.IsNullOrEmpty(item.Source) ? item.Id : $"{item.Id}  ·  {item.Source}";
        texts.Children.Add(new TextBlock { Text = sub, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(texts, 2);
        grid.Children.Add(texts);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        // Details (every row)
        var details = new Button { Content = P("Details", "詳情"), Padding = new Thickness(10, 4, 10, 4) };
        details.Click += async (_, _) => await ShowDetails(item);
        buttons.Children.Add(details);

        if (extras is not null)
            foreach (var ex in extras)
            {
                var e2 = ex;
                var b2 = new Button { Content = e2.label, Padding = new Thickness(10, 4, 10, 4) };
                b2.Click += async (_, _) => await e2.run(b2);
                buttons.Children.Add(b2);
            }

        var btn = new Button { Content = actionLabel, Padding = new Thickness(12, 4, 12, 4) };
        btn.Click += async (_, _) => await action(btn);
        buttons.Children.Add(btn);

        Grid.SetColumn(buttons, 3);
        grid.Children.Add(buttons);

        return Card(grid);
    }

    private Border StatusRow(string title, string id, string status, bool ok, (string label, Func<Task> action)? action)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 13, TextWrapping = TextWrapping.Wrap });
        texts.Children.Add(new TextBlock { Text = id, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });
        Grid.SetColumn(texts, 0);
        grid.Children.Add(texts);

        var st = new TextBlock
        {
            Text = status,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources[ok ? "SystemFillColorSuccessBrush" : "TextFillColorSecondaryBrush"],
        };
        Grid.SetColumn(st, 1);
        grid.Children.Add(st);

        if (action is { } act)
        {
            var btn = new Button { Content = act.label, Padding = new Thickness(12, 4, 12, 4) };
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false; btn.Content = P("Working…", "處理緊…");
                try { await act.action(); btn.Content = P("Done", "完成"); }
                catch { btn.Content = P("Retry", "重試"); btn.IsEnabled = true; }
            };
            Grid.SetColumn(btn, 2);
            grid.Children.Add(btn);
        }

        return Card(grid);
    }

    private Border ManagerBadge(string key)
    {
        var badge = new Border
        {
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = key, FontSize = 10, FontWeight = FontWeights.SemiBold },
        };
        Grid.SetColumn(badge, 0);
        return badge;
    }

    private static Border Card(UIElement child) => new()
    {
        Padding = new Thickness(14, 10, 14, 10),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = child,
    };
}
