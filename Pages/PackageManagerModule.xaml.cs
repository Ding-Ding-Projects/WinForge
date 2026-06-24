using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly Dictionary<string, PackageItem> _selectedPkgs = new(StringComparer.OrdinalIgnoreCase); // 已勾選套件 · checked packages keyed by "manager|id"

    private static string PkgKey(PackageItem i) => $"{i.ManagerKey}|{i.Id}";

    // 更新忽略／釘版改由 IgnoredUpdates 服務（UniGetUI 式釘版）處理 · ignore/pin now delegated to IgnoredUpdates.
    private const string IgnoreNotApplicableKey = "pkg.ignore.notapplicable";
    private static bool IgnoreNotApplicable =>
        SettingsStore.Get(IgnoreNotApplicableKey, "false") == "true";

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
            // 啟動背景更新排程器（按設定行；冇開 AutoCheck 就閒置）· start the background update scheduler.
            try { PackageUpdateScheduler.Start(); } catch { }
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
        ViewCombo.Items.Add(P("Settings", "設定")); // 背景／通知／系統匣設定（index 7）· background/notify/tray settings.
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
            case 7: // Settings · 設定（背景更新／通知／代理／備份）
                SearchBox.IsEnabled = false;
                PrimaryActionBtn.Content = P("Open settings", "開啟設定");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Visibility = Visibility.Collapsed;
                LoadSettingsView();
                break;
        }
    }

    /// <summary>背景更新／通知／系統匣設定簡介 · Blurb for the scheduler/notify/tray settings view.</summary>
    private void LoadSettingsView()
    {
        ResultsPanel.Children.Clear();
        ResultsHeader.Text = P("Background updates, notifications, tray & backup", "背景更新、通知、系統匣同備份");
        ResultsPanel.Children.Add(Card(new TextBlock
        {
            Text = P(
                "Open settings to configure scheduled background update checks, auto-install gates (metered / battery / battery saver), update-age & installer-host security, parallel operations, notifications (including per-manager mute), per-manager executable paths & arguments, proxy, vcpkg, and the daily local backup. App settings can be exported / imported / reset here too.",
                "開啟設定可以配置排程背景檢查更新、自動安裝閘（流量／電池／慳電）、更新年齡同安裝來源安全、同時操作數、通知（含每個管理器靜音）、各管理器可執行檔路徑同參數、代理、vcpkg，以及每日本地備份。亦可以喺度匯出／匯入／重設 App 設定。"),
            TextWrapping = TextWrapping.Wrap, FontSize = 13,
        }));
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
            case 7: await PackageSettingsDialog.ShowAsync(this.XamlRoot); break;
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
                (P("More ▾", "更多 ▾"), btn => { ShowMoreFlyout(btn, item, RowExtraScope.Discover); return Task.CompletedTask; }),
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
        bool hideNotApplicable = IgnoreNotApplicable;
        var shown = ups.Where(u => !IgnoredUpdates.IsIgnored(u)
            && !(hideNotApplicable && string.IsNullOrWhiteSpace(u.AvailableVersion))).ToList();
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
            var pkg = item;
            var extras = new List<(string, Func<Button, Task>)>
            {
                // 「忽略 ▾」彈出選單：跳過此版本／忽略所有版本／暫停更新…
                // "Ignore ▾" opens a flyout: skip this version / ignore all versions / pause for…
                (P("Ignore ▾", "忽略 ▾"), btn => { ShowIgnoreFlyout(btn, pkg); return Task.CompletedTask; }),
                (P("More ▾", "更多 ▾"), btn => { ShowMoreFlyout(btn, item, RowExtraScope.Updates); return Task.CompletedTask; }),
            };
            ResultsPanel.Children.Add(RowFor(item, label, async btn => await ActionUpdate(item, btn), extras));
        }
    }

    /// <summary>
    /// 「忽略 ▾」嘅彈出選單 · The "Ignore ▾" flyout offering UniGetUI-style pin choices:
    /// skip this version, ignore all versions, or pause updates for a chosen duration.
    /// </summary>
    private void ShowIgnoreFlyout(Button anchor, PackageItem item)
    {
        var flyout = new MenuFlyout();

        var skip = new MenuFlyoutItem { Text = P("Skip this version", "跳過此版本") };
        skip.Click += async (_, _) => { IgnoredUpdates.PinThisVersion(item); await LoadUpdates(); };
        flyout.Items.Add(skip);

        var all = new MenuFlyoutItem { Text = P("Ignore all versions", "忽略所有版本") };
        all.Click += async (_, _) => { IgnoredUpdates.PinAllVersions(item); await LoadUpdates(); };
        flyout.Items.Add(all);

        var pause = new MenuFlyoutSubItem { Text = P("Pause updates for…", "暫停更新…") };
        void AddPause(string en, string zh, TimeSpan d)
        {
            var mi = new MenuFlyoutItem { Text = P(en, zh) };
            mi.Click += async (_, _) => { IgnoredUpdates.Snooze(item, d); await LoadUpdates(); };
            pause.Items.Add(mi);
        }
        AddPause("1 day", "1 日", TimeSpan.FromDays(1));
        AddPause("1 week", "1 個星期", TimeSpan.FromDays(7));
        AddPause("1 month", "1 個月", TimeSpan.FromDays(30));
        AddPause("3 months", "3 個月", TimeSpan.FromDays(90));
        flyout.Items.Add(pause);

        flyout.ShowAt(anchor);
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
        {
            var extras = new List<(string, Func<Button, Task>)>
            {
                (P("More ▾", "更多 ▾"), btn => { ShowMoreFlyout(btn, item, RowExtraScope.Installed); return Task.CompletedTask; }),
            };
            ResultsPanel.Children.Add(RowFor(item, P("Uninstall", "解除安裝"), async btn => await ActionUninstall(item, btn), extras));
        }
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

    /// <summary>
    /// 開套件清單工作區（以已安裝套件作種子）· Gather installed packages, then open the editable
    /// bundle workspace (BundleWorkspaceDialog) where the user can save in JSON/YAML/XML/.ubundle,
    /// export a .ps1, and install. 由「匯出…」按鈕觸發。
    /// </summary>
    private async Task ExportBundle()
    {
        var keys = SelectedAvailable();
        Busy.IsActive = true;
        List<PackageItem> items;
        try { items = await PackageManagerRegistry.AllInstalledAsync(keys, CancellationToken.None); }
        catch { items = new(); }
        Busy.IsActive = false;
        await BundleWorkspaceDialog.ShowAsync(this.XamlRoot, items);
    }

    /// <summary>
    /// 將一組套件直接寫做清單（四種格式）· Write a set of packages to a bundle file, with the format
    /// chosen by the file extension (.json/.ubundle → JSON, .yaml/.yml → YAML, .xml → XML). Used by the
    /// batch "Export selected…" bar. Routes via BundleService.ToBundle + SaveAsync.
    /// </summary>
    private async Task ExportEntries(List<PackageItem> items)
    {
        if (items.Count == 0) { ResultsHeader.Text = P("Nothing to export.", "冇嘢可以匯出。"); return; }
        try
        {
            var path = await FileDialogs.SaveFileAsync("winforge-bundle",
                new[]
                {
                    new FileDialogs.Filter("JSON / UniGetUI bundle (*.json;*.ubundle)", "*.json;*.ubundle"),
                    new FileDialogs.Filter("YAML (*.yaml;*.yml)", "*.yaml;*.yml"),
                    new FileDialogs.Filter("XML (*.xml)", "*.xml"),
                },
                "json",
                P("Export bundle as…", "匯出清單…"));
            if (path is null) return;
            var bundle = BundleService.ToBundle(items);
            await BundleService.SaveAsync(bundle, path);
            int comp = bundle.packages.Count, inc = bundle.incompatible_packages.Count;
            ResultsHeader.Text = inc > 0
                ? P($"Exported {comp} package(s) ({inc} incompatible logged).", $"匯出咗 {comp} 個套件（記錄咗 {inc} 個不相容）。")
                : P($"Exported {comp} package(s).", $"匯出咗 {comp} 個套件。");
        }
        catch (Exception ex) { ResultsHeader.Text = ex.Message; }
    }

    /// <summary>
    /// 匯入清單並安裝（先做安全檢查）· Load a bundle in any of the four formats via BundleService.LoadAsync,
    /// show version-mismatch + a security report dialog (custom commands/args/kill-lists) before installing
    /// each compatible package; incompatibles are skipped. 由「匯入…」按鈕觸發。
    /// </summary>
    private async Task ImportBundle()
    {
        string? path;
        try { path = await FileDialogs.OpenFileAsync(".json", ".yaml", ".yml", ".xml", ".ubundle"); }
        catch (Exception ex) { ResultsHeader.Text = ex.Message; return; }
        if (path is null) return;

        Busy.IsActive = true;
        BundleLoadResult res;
        try { res = await BundleService.LoadAsync(path); }
        catch (Exception ex) { Busy.IsActive = false; ResultsHeader.Text = ex.Message; return; }
        Busy.IsActive = false;

        var bundle = res.Bundle;
        if (bundle.packages.Count == 0 && bundle.incompatible_packages.Count == 0)
        {
            ResultsHeader.Text = P("Bundle is empty.", "清單係空嘅。");
            return;
        }

        // 版本不符 + 安全檢查確認對話框 · version-mismatch + security review before installing.
        var lines = new List<string>();
        if (res.VersionMismatch)
            lines.Add(P($"Bundle export_version is {res.FoundVersion}, expected 3 — import may be imperfect.",
                        $"清單 export_version 係 {res.FoundVersion}，預期係 3 — 匯入可能唔完全正確。"));
        var report = BundleService.Inspect(bundle);
        if (report.HasWarnings)
        {
            lines.Add(P($"Security — {report.Warnings.Count} package(s) run custom commands/args/kill-lists:",
                        $"安全 — {report.Warnings.Count} 個套件會執行自訂指令／參數／kill-list："));
            foreach (var w in report.Warnings.Take(10)) lines.Add("• " + w.Primary + "  ·  " + w.Secondary);
        }
        lines.Add(P($"Install {bundle.packages.Count} compatible package(s)?{(bundle.incompatible_packages.Count > 0 ? $" ({bundle.incompatible_packages.Count} incompatible skipped)" : "")}",
                    $"安裝 {bundle.packages.Count} 個相容套件？{(bundle.incompatible_packages.Count > 0 ? $"（略過 {bundle.incompatible_packages.Count} 個不相容）" : "")}"));

        var body = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Text = string.Join("\n", lines) };
        var confirm = new ContentDialog
        {
            Title = report.HasWarnings ? P("Security review", "安全檢查") : P("Import bundle", "匯入清單"),
            Content = new ScrollViewer { MaxHeight = 340, MinWidth = 480, Content = body },
            PrimaryButtonText = P("Install", "安裝"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = report.HasWarnings ? ContentDialogButton.Close : ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        ResultsPanel.Children.Clear();
        int done = 0;
        var pkgs = bundle.packages;
        Busy.IsActive = true;
        foreach (var en in pkgs)
        {
            var mgr = PackageManagerRegistry.ByKey(en.ManagerName);
            if (mgr is null || !(_available.TryGetValue(en.ManagerName, out var a) && a)) continue;
            var label = string.IsNullOrEmpty(en.Name) ? en.Id : en.Name;
            ResultsHeader.Text = P($"Installing {label}… ({done + 1}/{pkgs.Count})", $"安裝緊 {label}…（{done + 1}/{pkgs.Count}）");
            try { var r = await mgr.InstallAsync(en.Id, CancellationToken.None); if (r.Success) done++; } catch { }
        }
        Busy.IsActive = false;
        ResultsHeader.Text = P($"Installed {done}/{pkgs.Count} from bundle.", $"由清單安裝咗 {done}/{pkgs.Count}。");
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
        var pins = IgnoredUpdates.All();
        ResultsHeader.Text = P($"Ignored updates — {pins.Count}", $"已忽略更新 — {pins.Count}");

        // 頂部控制：自動忽略「不適用」更新嘅切換 + 全部重設。
        // Top controls: "auto-ignore not-applicable" toggle + "Reset all".
        var controls = new StackPanel { Spacing = 8 };

        var naCheck = new CheckBox
        {
            Content = P("Automatically ignore updates that are not applicable",
                "自動忽略不適用嘅更新"),
            IsChecked = IgnoreNotApplicable,
        };
        naCheck.Checked += (_, _) => SettingsStore.Set(IgnoreNotApplicableKey, "true");
        naCheck.Unchecked += (_, _) => SettingsStore.Set(IgnoreNotApplicableKey, "false");
        controls.Children.Add(naCheck);

        var resetBtn = new Button { Content = P("Reset all", "全部重設"), Padding = new Thickness(12, 4, 12, 4) };
        resetBtn.Click += (_, _) => { IgnoredUpdates.ResetAll(); LoadIgnoredView(); };
        controls.Children.Add(resetBtn);

        ResultsPanel.Children.Add(Card(controls));

        if (pins.Count == 0)
        {
            ResultsPanel.Children.Add(new TextBlock
            {
                Text = P("Nothing ignored. Use “Ignore ▾” on an update to skip a version, ignore all versions, or pause updates.",
                    "冇忽略項目。喺更新撳「忽略 ▾」就可以跳過某版本、忽略所有版本，或者暫停更新。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], Margin = new Thickness(4, 8, 0, 0),
            });
            return;
        }

        foreach (var pin in pins)
        {
            var p = pin;
            var item = new PackageItem { ManagerKey = p.Manager, Id = p.Id, Name = p.Id, Source = "" };
            // 釘版種類說明 · describe the pin kind.
            string kind;
            if (!string.IsNullOrWhiteSpace(p.PauseUntil))
                kind = P($"paused until {p.PauseUntil}", $"暫停至 {p.PauseUntil}");
            else if (p.Version == "*")
                kind = P("all versions", "所有版本");
            else
                kind = P($"this version {p.Version}", $"此版本 {p.Version}");
            item.Version = kind;

            ResultsPanel.Children.Add(RowFor(item, P("Un-ignore", "取消忽略"),
                _ => { IgnoredUpdates.RemoveKey(p.Manager, p.Id, p.Version); LoadIgnoredView(); return Task.CompletedTask; }));
        }
    }

    // ===== Details / install options dialogs =====

    private async Task ShowDetails(PackageItem item)
        => await PackageDetailsDialog.ShowAsync(this.XamlRoot, item);

    // ===== Row "More ▾" flyout (per-package actions) =====

    /// <summary>邊個鏈式操作 · which chained ops the flyout exposes, per view.</summary>
    private enum RowExtraScope { Discover, Updates, Installed }

    /// <summary>
    /// 喺一個行內按鈕彈出「More ▾」flyout · Attach &amp; open a "More ▾" flyout on a row button: copy install
    /// command, download installer, and the chained ops relevant to the current view
    /// (reinstall / uninstall-then-X). Bilingual throughout.
    /// </summary>
    private void ShowMoreFlyout(Button btn, PackageItem item, RowExtraScope scope)
    {
        var flyout = new MenuFlyout();

        // Copy install command.
        var copy = new MenuFlyoutItem { Text = P("Copy install command", "複製安裝指令") };
        copy.Click += (_, _) =>
        {
            try
            {
                var cmd = PackageDetails.BuildInstallCommand(item);
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(cmd);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                ResultsHeader.Text = P($"Copied: {cmd}", $"已複製：{cmd}");
            }
            catch (Exception ex) { ResultsHeader.Text = ex.Message; }
        };
        flyout.Items.Add(copy);

        // Download installer…
        var download = new MenuFlyoutItem { Text = P("Download installer…", "下載安裝程式…") };
        download.Click += async (_, _) =>
        {
            try
            {
                var dir = await FileDialogs.OpenFolderAsync(P("Choose download folder", "揀下載資料夾"));
                if (dir is null) return;
                ResultsHeader.Text = P($"Downloading {item.Name}…", $"下載 {item.Name} 緊…");
                var r = await PackageDetails.DownloadInstallerAsync(item, dir, CancellationToken.None);
                ResultsHeader.Text = r.Message?.Primary ?? (r.Success ? P("Downloaded.", "已下載。") : P("Download failed.", "下載失敗。"));
            }
            catch (Exception ex) { ResultsHeader.Text = ex.Message; }
        };
        flyout.Items.Add(download);

        // Chained ops vary per view.
        if (scope is RowExtraScope.Installed)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            var reinstall = new MenuFlyoutItem { Text = P("Reinstall", "重新安裝") };
            reinstall.Click += async (_, _) => await RunChainedFromRow(item,
                P("Reinstall", "重新安裝"),
                new (string, string, Func<IPackageManager, CancellationToken, Task<TweakResult>>)[]
                {
                    ("Uninstall", "解除安裝", (m, c) => m.UninstallAsync(item.Id, c)),
                    ("Install", "安裝", (m, c) => m.InstallAsync(item.Id, c)),
                });
            flyout.Items.Add(reinstall);

            var unRe = new MenuFlyoutItem { Text = P("Uninstall then reinstall", "解除後重裝") };
            unRe.Click += async (_, _) => await RunChainedFromRow(item,
                P("Uninstall then reinstall", "解除後重裝"),
                new (string, string, Func<IPackageManager, CancellationToken, Task<TweakResult>>)[]
                {
                    ("Uninstall", "解除安裝", (m, c) => m.UninstallAsync(item.Id, c)),
                    ("Install", "安裝", (m, c) => m.InstallAsync(item.Id, c)),
                });
            flyout.Items.Add(unRe);
        }
        else if (scope is RowExtraScope.Updates)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            var unUp = new MenuFlyoutItem { Text = P("Uninstall then update", "解除後更新") };
            unUp.Click += async (_, _) => await RunChainedFromRow(item,
                P("Uninstall then update", "解除後更新"),
                new (string, string, Func<IPackageManager, CancellationToken, Task<TweakResult>>)[]
                {
                    ("Uninstall", "解除安裝", (m, c) => m.UninstallAsync(item.Id, c)),
                    ("Install", "安裝", (m, c) => m.InstallAsync(item.Id, c)),
                    ("Update", "更新", (m, c) => m.UpdateAsync(item.Id, c)),
                });
            flyout.Items.Add(unUp);
        }

        flyout.ShowAt(btn);
    }

    /// <summary>由行內鏈式操作執行並更新表頭 · Run a chained sequence of manager ops from a row, surfacing progress in the header.</summary>
    private async Task RunChainedFromRow(PackageItem item, string title,
        (string en, string zh, Func<IPackageManager, CancellationToken, Task<TweakResult>> op)[] steps)
    {
        var mgr = PackageManagerRegistry.ByKey(item.ManagerKey);
        if (mgr is null) { ResultsHeader.Text = P("Manager not available.", "管理器唔可用。"); return; }
        Busy.IsActive = true;
        try
        {
            int i = 0;
            foreach (var step in steps)
            {
                i++;
                ResultsHeader.Text = P($"{title}: [{i}/{steps.Length}] {step.en} {item.Name}…", $"{title}：[{i}/{steps.Length}] {step.zh} {item.Name}…");
                TweakResult r;
                try { r = await step.op(mgr, CancellationToken.None); }
                catch (Exception ex) { r = TweakResult.Fail(ex.Message, ex.Message); }
                if (!r.Success)
                {
                    ResultsHeader.Text = P($"{title}: '{step.en}' failed.", $"{title}：「{step.zh}」失敗。");
                    return;
                }
            }
            ResultsHeader.Text = P($"{title}: done for {item.Name}.", $"{title}：{item.Name} 完成。");
        }
        finally { Busy.IsActive = false; }
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
