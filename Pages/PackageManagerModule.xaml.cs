using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
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
    private int _view; // 0 Discover, 1 Updates, 2 Installed, 3 Bundles, 4 Sources, 5 Ignored, 6 Setup, 7 Settings, 8 Operations
    private HashSet<string> _wingetInstalled = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PackageItem> _selectedPkgs = new(StringComparer.OrdinalIgnoreCase); // 已勾選套件 · checked packages keyed by "manager|id|source"
    private readonly List<PackageItem> _lastDiscoverResults = new();
    private string _lastDiscoverQuery = "";
    private bool _syncingSearchOptions;
    private bool _eventsSubscribed;
    private int _operationRefreshPending;
    private int? _pendingNavigationView;

    /// <summary>Selection identity includes the validated source so two same-ID search results do not collapse.</summary>
    private static string PkgKey(PackageItem i)
        => PackageSourcePolicy.IdentityKey(i?.ManagerKey, i?.Id, i?.Source,
            PackageOperations.Op.Install);

    // 更新忽略／釘版改由 IgnoredUpdates 服務（UniGetUI 式釘版）處理 · ignore/pin now delegated to IgnoredUpdates.
    private const string IgnoreNotApplicableKey = "pkg.ignore.notapplicable";
    private static bool IgnoreNotApplicable =>
        SettingsStore.Get(IgnoreNotApplicableKey, "false") == "true";
    private const string SearchModeKey = "pkg.search.mode";
    private const string SearchCaseSensitiveKey = "pkg.search.caseSensitive";
    private const string SearchIgnoreSpecialKey = "pkg.search.ignoreSpecial";

    private enum PackageSearchMode { Both, Name, Id, Exact, Similar }

    public PackageManagerModule()
    {
        InitializeComponent();
        foreach (var m in PackageManagerRegistry.All) _selected.Add(m.Key);
        // 具名處理器＋Unloaded 退訂，唔好用內嵌 lambda（會漏，令每次切語言都重跑重活）·
        // named handler + unsubscribe on Unloaded (inline lambda leaks and re-runs heavy work per switch).
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 接收 <c>module.packages#discover|updates|installed</c> 呢類深層連結。
    /// Accept Package Manager deep links such as <c>module.packages#discover|updates|installed</c>.
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (!PackageManagerViewRouting.TryGetViewIndex(e.Parameter as string, out var view)) return;

        _pendingNavigationView = view;
        ApplyPendingNavigationView();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_eventsSubscribed)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            PackageOperationCoordinator.Changed += OnOperationChanged;
            _eventsSubscribed = true;
        }
        Render();
        BuildManagerFilters();
        BuildViewCombo();
        ApplyPendingNavigationView();
        UpdateBatchBar();
        UpdateOperationsButton();
        await CheckAvailability();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_eventsSubscribed) return;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        PackageOperationCoordinator.Changed -= OnOperationChanged;
        _eventsSubscribed = false;
    }

    private void OnOperationChanged(object? sender, PackageOperationChangedEventArgs e)
    {
        if (Interlocked.Exchange(ref _operationRefreshPending, 1) != 0) return;
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(100);
            Interlocked.Exchange(ref _operationRefreshPending, 0);
            UpdateOperationsButton();
            if (_view == 8)
                LoadOperationsView();
        }))
            Interlocked.Exchange(ref _operationRefreshPending, 0);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Render();
        BuildManagerFilters();
        BuildViewCombo();
        UpdateBatchBar();
        UpdateOperationsButton();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Package Manager · 套件管理";
        HeaderBlurb.Text = P(
            "A UniGetUI-style hub over winget, Scoop, Chocolatey, pip, npm, .NET tools, PowerShell Gallery, PowerShell 7, Cargo, Bun and vcpkg — discover, multi-select, batch install/update/uninstall, and export/import bundles, all in-app.",
            "UniGetUI 式總管，統一 winget、Scoop、Chocolatey、pip、npm、.NET 工具、PowerShell Gallery、PowerShell 7、Cargo、Bun 同 vcpkg — 搜尋、多選、批次安裝／更新／解除安裝，仲可以匯出／匯入清單，全部喺 app 內。");
        ManagersLabel.Text = P("Package managers", "套件管理器");
        SearchBox.PlaceholderText = P("Search packages (e.g. vscode, vlc, obs)…", "搜尋套件（例如 vscode、vlc、obs）…");
        SearchOptionsText.Text = P("Filters", "篩選");
        SearchOptionsTitle.Text = P("Search filters", "搜尋篩選");
        SearchModeLabel.Text = P("Search mode", "搜尋模式");
        SearchModeBoth.Content = P("Both", "兩者");
        SearchModeName.Content = P("Package Name", "套件名稱");
        SearchModeId.Content = P("Package ID", "套件 ID");
        SearchModeExact.Content = P("Exact match", "完全符合");
        SearchModeSimilar.Content = P("Show similar packages", "顯示相似套件");
        SearchCaseToggle.Header = P("Distinguish between uppercase and lowercase", "區分大小寫");
        SearchIgnoreSpecialToggle.Header = P("Ignore special characters", "忽略特殊字元");
        TerminalBtnText.Text = P("Terminal", "終端機");
        UpdateOperationsButton();
        ToolTipService.SetToolTip(SearchOptionsBtn, P("UniGetUI-style search mode and filter options.",
            "UniGetUI 式搜尋模式同篩選選項。"));
        ToolTipService.SetToolTip(TerminalBtn, P("Open a shell for manual winget / scoop / choco / pip / npm",
            "開一個 shell 手動執行 winget／scoop／choco／pip／npm"));
        RefreshSearchOptionControls();
    }

    private PackageSearchMode CurrentSearchMode()
        => Enum.TryParse<PackageSearchMode>(SettingsStore.Get(SearchModeKey, PackageSearchMode.Both.ToString()), true, out var mode)
            ? mode
            : PackageSearchMode.Both;

    private bool SearchCaseSensitive => SettingsStore.Get(SearchCaseSensitiveKey, "false") == "true";
    private bool SearchIgnoreSpecial => SettingsStore.Get(SearchIgnoreSpecialKey, "false") == "true";

    private void RefreshSearchOptionControls()
    {
        _syncingSearchOptions = true;
        try
        {
            var mode = CurrentSearchMode();
            SearchModeBoth.IsChecked = mode == PackageSearchMode.Both;
            SearchModeName.IsChecked = mode == PackageSearchMode.Name;
            SearchModeId.IsChecked = mode == PackageSearchMode.Id;
            SearchModeExact.IsChecked = mode == PackageSearchMode.Exact;
            SearchModeSimilar.IsChecked = mode == PackageSearchMode.Similar;
            SearchCaseToggle.IsOn = SearchCaseSensitive;
            SearchIgnoreSpecialToggle.IsOn = SearchIgnoreSpecial;
        }
        finally { _syncingSearchOptions = false; }
    }

    private void SearchMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_syncingSearchOptions) return;
        if (sender is RadioButton { Tag: string tag }
            && Enum.TryParse<PackageSearchMode>(tag, true, out var mode))
        {
            SettingsStore.Set(SearchModeKey, mode.ToString());
            RenderCachedDiscoverResults();
        }
    }

    private void SearchOption_Toggled(object sender, RoutedEventArgs e)
    {
        if (_syncingSearchOptions) return;
        SettingsStore.Set(SearchCaseSensitiveKey, SearchCaseToggle.IsOn ? "true" : "false");
        SettingsStore.Set(SearchIgnoreSpecialKey, SearchIgnoreSpecialToggle.IsOn ? "true" : "false");
        RenderCachedDiscoverResults();
    }

    private void RenderCachedDiscoverResults()
    {
        if (_view != 0 || _lastDiscoverResults.Count == 0 || string.IsNullOrWhiteSpace(_lastDiscoverQuery))
            return;
        RenderDiscoverResults(_lastDiscoverQuery, _lastDiscoverResults);
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

    private void Operations_Click(object sender, RoutedEventArgs e)
        => ViewCombo.SelectedIndex = 8;

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
        ViewCombo.Items.Add(P("Operations", "操作佇列"));
        ViewCombo.SelectedIndex = sel < 0 ? 0 : sel;
    }

    private void ApplyPendingNavigationView()
    {
        if (_pendingNavigationView is not int view || ViewCombo.Items.Count <= view) return;

        _pendingNavigationView = null;
        _view = view;
        if (ViewCombo.SelectedIndex == view)
        {
            if (IsLoaded) _ = LoadView();
            return;
        }

        ViewCombo.SelectedIndex = view;
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
        SearchOptionsBtn.Visibility = _view == 0 ? Visibility.Visible : Visibility.Collapsed;
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
            case 8: // Operations · 操作佇列／歷史
                SearchBox.IsEnabled = false;
                PrimaryActionBtn.Content = P("Refresh", "重新整理");
                PrimaryActionBtn.Visibility = Visibility.Visible;
                SecondaryActionBtn.Content = P("Clear completed", "清除已完成");
                SecondaryActionBtn.Visibility = Visibility.Visible;
                LoadOperationsView();
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

    private void UpdateOperationsButton()
    {
        int active = PackageOperationCoordinator.GetActiveSnapshots().Count;
        OperationsBtnText.Text = active > 0
            ? P($"Operations ({active})", $"操作（{active}）")
            : P("Operations", "操作");
        ToolTipService.SetToolTip(OperationsBtn, P(
            "Open the package operation queue, output and history.",
            "開啟套件操作佇列、輸出同歷史。"));
    }

    private void LoadOperationsView()
    {
        ResultsPanel.Children.Clear();
        var (active, history) = PackageOperationCoordinator.GetSnapshotSet();
        ResultsHeader.Text = P(
            $"Operations — {active.Count} active, {history.Count} completed",
            $"操作 — {active.Count} 個進行中、{history.Count} 個已完成");
        UpdateOperationsButton();

        if (active.Count == 0 && history.Count == 0)
        {
            ResultsPanel.Children.Add(Card(new TextBlock
            {
                Text = P("No package operations yet.", "未有套件操作。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            }));
            return;
        }

        if (active.Count > 0)
        {
            ResultsPanel.Children.Add(SectionLabel(P("Active & queued", "進行中同排隊")));
            foreach (var snapshot in active) ResultsPanel.Children.Add(OperationCard(snapshot));
        }

        if (history.Count > 0)
        {
            ResultsPanel.Children.Add(SectionLabel(P("Completed history", "已完成歷史")));
            foreach (var snapshot in history) ResultsPanel.Children.Add(OperationCard(snapshot));
        }
    }

    private Border OperationCard(PackageOperationSnapshot snapshot)
    {
        var root = new StackPanel { Spacing = 7 };
        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        FrameworkElement stateIcon = snapshot.Status == PackageOperationStatus.Running
            ? new ProgressRing { Width = 18, Height = 18, IsActive = true, VerticalAlignment = VerticalAlignment.Center }
            : new FontIcon
            {
                Glyph = snapshot.Status switch
                {
                    PackageOperationStatus.Succeeded => "\uE73E",
                    PackageOperationStatus.Failed => "\uEA39",
                    PackageOperationStatus.Cancelled => "\uE711",
                    PackageOperationStatus.Skipped => "\uE72A",
                    _ => "\uE823",
                },
                FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center,
            };
        Grid.SetColumn(stateIcon, 0);
        header.Children.Add(stateIcon);

        var text = new StackPanel { Spacing = 1 };
        text.Children.Add(new TextBlock
        {
            Text = $"{OperationVerb(snapshot.Operation)} · {snapshot.PackageName}",
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        text.Children.Add(new TextBlock
        {
            Text = $"{snapshot.ManagerKey} · {snapshot.PackageId} · {OperationStatusLabel(snapshot.Status)}",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(text, 1);
        header.Children.Add(text);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        if (snapshot.Status is PackageOperationStatus.Queued or PackageOperationStatus.Running)
        {
            var cancel = new Button { Content = P("Cancel", "取消"), Padding = new Thickness(10, 4, 10, 4) };
            cancel.Click += (_, _) =>
            {
                cancel.IsEnabled = false;
                PackageOperationCoordinator.Cancel(snapshot.OperationId);
            };
            actions.Children.Add(cancel);
        }
        else if (snapshot.Status is PackageOperationStatus.Failed or PackageOperationStatus.Cancelled)
        {
            var retry = new Button { Content = P("Retry", "重試"), Padding = new Thickness(10, 4, 10, 4) };
            retry.Click += (_, _) =>
            {
                retry.IsEnabled = false;
                PackageOperationCoordinator.Retry(snapshot.OperationId);
                ViewCombo.SelectedIndex = 8;
            };
            actions.Children.Add(retry);
        }
        Grid.SetColumn(actions, 2);
        header.Children.Add(actions);
        root.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(snapshot.OutputTail))
        {
            var output = new TextBox
            {
                Text = snapshot.OutputTail,
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                MaxHeight = 220,
            };
            root.Children.Add(new Expander
            {
                Header = P("Output", "輸出"),
                Content = output,
                IsExpanded = snapshot.Status == PackageOperationStatus.Failed,
            });
        }

        return Card(root);
    }

    private string OperationVerb(PackageOperations.Op op) => op switch
    {
        PackageOperations.Op.Install => P("Install", "安裝"),
        PackageOperations.Op.Update => P("Update", "更新"),
        PackageOperations.Op.Uninstall => P("Uninstall", "解除安裝"),
        _ => P("Operation", "操作"),
    };

    private string OperationStatusLabel(PackageOperationStatus status) => status switch
    {
        PackageOperationStatus.Queued => P("Queued", "排隊中"),
        PackageOperationStatus.Running => P("Running", "執行中"),
        PackageOperationStatus.Succeeded => P("Succeeded", "成功"),
        PackageOperationStatus.Failed => P("Failed", "失敗"),
        PackageOperationStatus.Cancelled => P("Cancelled", "已取消"),
        PackageOperationStatus.Skipped => P("Skipped", "已略過"),
        _ => status.ToString(),
    };

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
            case 8: LoadOperationsView(); break;
        }
    }

    private async void SecondaryAction_Click(object sender, RoutedEventArgs e)
    {
        switch (_view)
        {
            case 1: await UpdateAll(); break;
            case 3: await ImportBundle(); break;
            case 8:
                PackageOperationCoordinator.ClearHistory();
                LoadOperationsView();
                break;
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

        _lastDiscoverQuery = query;
        _lastDiscoverResults.Clear();
        _lastDiscoverResults.AddRange(results);
        RenderDiscoverResults(query, _lastDiscoverResults);
    }

    private void RenderDiscoverResults(string query, IReadOnlyList<PackageItem> rawResults)
    {
        ResultsPanel.Children.Clear();
        var results = FilterDiscoverResults(query, rawResults);
        var mode = CurrentSearchMode();
        var modeLabel = SearchModeDisplay(mode);
        var extra = SearchIgnoreSpecial
            ? P(" · ignoring special characters", " · 忽略特殊字元")
            : "";
        extra += SearchCaseSensitive
            ? P(" · case-sensitive", " · 區分大小寫")
            : "";
        ResultsHeader.Text = results.Count == rawResults.Count
            ? P($"Results — {results.Count} · {modeLabel}{extra}", $"結果 — {results.Count} · {modeLabel}{extra}")
            : P($"Results — {results.Count}/{rawResults.Count} · {modeLabel}{extra}", $"結果 — {results.Count}/{rawResults.Count} · {modeLabel}{extra}");

        if (results.Count == 0)
        {
            ResultsPanel.Children.Add(Card(new TextBlock
            {
                Text = P("No packages match the current search filters.", "冇套件符合目前搜尋篩選。"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            }));
            return;
        }

        foreach (var item in results)
        {
            var extras = new List<(string, Func<Button, Task>)>
            {
                (P("Options…", "選項…"), async _ => await ShowOptionsDialog(item)),
                (P("More ▾", "更多 ▾"), btn => { ShowMoreFlyout(btn, item, RowExtraScope.Discover); return Task.CompletedTask; }),
            };
            ResultsPanel.Children.Add(RowFor(item, P("Install", "安裝"), async btn => await ActionInstall(item, btn),
                extras, RowExtraScope.Discover, PackageOperations.Op.Install));
        }
    }

    private List<PackageItem> FilterDiscoverResults(string query, IReadOnlyList<PackageItem> rawResults)
    {
        var mode = CurrentSearchMode();
        if (mode == PackageSearchMode.Similar) return rawResults.ToList();

        var q = NormalizeSearchText(query, SearchIgnoreSpecial, SearchCaseSensitive);
        if (string.IsNullOrWhiteSpace(q)) return rawResults.ToList();

        bool Contains(string value)
            => NormalizeSearchText(value, SearchIgnoreSpecial, SearchCaseSensitive).Contains(q, StringComparison.Ordinal);
        bool Exact(string value)
            => string.Equals(NormalizeSearchText(value, SearchIgnoreSpecial, SearchCaseSensitive), q, StringComparison.Ordinal);

        return rawResults.Where(item => mode switch
        {
            PackageSearchMode.Name => Contains(item.Name),
            PackageSearchMode.Id => Contains(item.Id),
            PackageSearchMode.Exact => Exact(item.Name) || Exact(item.Id),
            _ => Contains(item.Name) || Contains(item.Id),
        }).ToList();
    }

    private static string NormalizeSearchText(string text, bool ignoreSpecial, bool caseSensitive)
    {
        var value = text ?? "";
        if (ignoreSpecial)
            value = new string(value.Where(char.IsLetterOrDigit).ToArray());
        return caseSensitive ? value : value.ToUpperInvariant();
    }

    private string SearchModeDisplay(PackageSearchMode mode) => mode switch
    {
        PackageSearchMode.Name => P("Package Name", "套件名稱"),
        PackageSearchMode.Id => P("Package ID", "套件 ID"),
        PackageSearchMode.Exact => P("Exact match", "完全符合"),
        PackageSearchMode.Similar => P("Show similar packages", "顯示相似套件"),
        _ => P("Both", "兩者"),
    };

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
            && !PackageOperationCoordinator.IsMinorUpdateSuppressed(u)
            && !(hideNotApplicable && string.IsNullOrWhiteSpace(u.AvailableVersion))).ToList();
        int hidden = ups.Count - shown.Count;
        ResultsHeader.Text = hidden > 0
            ? P($"Updatable — {shown.Count} ({hidden} hidden by policy)", $"可更新 — {shown.Count}（政策隱藏 {hidden}）")
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
                var managerItems = g.ToList();
                var m = PackageManagerRegistry.ByKey(mk);
                var b = new Button { Content = $"{m?.NameEn ?? mk} ({g.Count()})", Padding = new Thickness(10, 4, 10, 4) };
                b.Click += async (_, _) =>
                {
                    b.IsEnabled = false; b.Content = P("Updating…", "更新緊…");
                    var results = await PackageOperationCoordinator.RunManyAsync(
                        managerItems, PackageOperations.Op.Update, ct: CancellationToken.None);
                    int d = results.Count(s => s.Status == PackageOperationStatus.Succeeded);
                    int skipped = results.Count(s => s.Status == PackageOperationStatus.Skipped);
                    ResultsHeader.Text = skipped > 0
                        ? P($"{mk}: updated {d}/{results.Count}; {skipped} skipped.", $"{mk}：更新咗 {d}/{results.Count}；略過 {skipped} 個。")
                        : P($"{mk}: updated {d}/{results.Count}.", $"{mk}：更新咗 {d}/{results.Count}。");
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
            ResultsPanel.Children.Add(RowFor(item, label, async btn => await ActionUpdate(item, btn),
                extras, RowExtraScope.Updates, PackageOperations.Op.Update));
        }
    }

    /// <summary>
    /// 「忽略 ▾」嘅彈出選單 · The "Ignore ▾" flyout offering UniGetUI-style pin choices:
    /// skip this version, ignore all versions, or pause updates for a chosen duration.
    /// </summary>
    private void ShowIgnoreFlyout(Button anchor, PackageItem item)
    {
        var flyout = new MenuFlyout();
        AddIgnoreMenuItems(flyout.Items, item);
        flyout.ShowAt(anchor);
    }

    private void AddIgnoreMenuItems(IList<MenuFlyoutItemBase> items, PackageItem item)
    {
        var skip = new MenuFlyoutItem { Text = P("Skip this version", "跳過此版本") };
        skip.Click += async (_, _) => { IgnoredUpdates.PinThisVersion(item); await LoadUpdates(); };
        items.Add(skip);

        var all = new MenuFlyoutItem { Text = P("Ignore all versions", "忽略所有版本") };
        all.Click += async (_, _) => { IgnoredUpdates.PinAllVersions(item); await LoadUpdates(); };
        items.Add(all);

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
        items.Add(pause);
    }

    private async Task UpdateAll()
    {
        var keys = SelectedAvailable();
        Busy.IsActive = true;
        List<PackageItem> ups;
        try { ups = await PackageManagerRegistry.AllUpdatesAsync(keys, CancellationToken.None); }
        catch { ups = new(); }
        Busy.IsActive = false;
        var eligible = ups.Where(u => !IgnoredUpdates.IsIgnored(u)).ToList();
        ResultsHeader.Text = P($"Queued {eligible.Count} update(s)…", $"已排隊 {eligible.Count} 個更新…");
        var results = await PackageOperationCoordinator.RunManyAsync(
            eligible, PackageOperations.Op.Update, ct: CancellationToken.None);
        int done = results.Count(s => s.Status == PackageOperationStatus.Succeeded);
        int skipped = results.Count(s => s.Status == PackageOperationStatus.Skipped);
        ResultsHeader.Text = skipped > 0
            ? P($"Updated {done}/{results.Count}; {skipped} skipped.", $"更新咗 {done}/{results.Count}；略過 {skipped} 個。")
            : P($"Updated {done}/{results.Count}.", $"更新咗 {done}/{results.Count}。");
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
            ResultsPanel.Children.Add(RowFor(item, P("Uninstall", "解除安裝"), async btn => await ActionUninstall(item, btn),
                extras, RowExtraScope.Installed, PackageOperations.Op.Uninstall));
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
            _selectedPkgs.Values.ToList(), PackageOperations.Op.Install);

    private async void BatchUpdate_Click(object sender, RoutedEventArgs e)
        => await RunBatchWithLog(P("Update selected", "更新所選"),
            _selectedPkgs.Values.ToList(), PackageOperations.Op.Update);

    private async void BatchUninstall_Click(object sender, RoutedEventArgs e)
        => await RunBatchWithLog(P("Uninstall selected", "解除所選"),
            _selectedPkgs.Values.ToList(), PackageOperations.Op.Uninstall);

    private async void BatchExport_Click(object sender, RoutedEventArgs e)
        => await ExportEntries(_selectedPkgs.Values.ToList());

    /// <summary>
    /// 跑一批操作並即時顯示進度同輸出 · Run a batch op over the selected packages, streaming progress and
    /// the CLI output into a live log dialog (UniGetUI-style operation feed). Cancellable.
    /// </summary>
    private async Task RunBatchWithLog(string title, List<PackageItem> items, PackageOperations.Op op)
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
            int done = 0, fail = 0, skipped = 0;
            var pending = items.Select((item, index) =>
            {
                int displayIndex = index + 1;
                DispatcherQueue.TryEnqueue(() => Append(P(
                    $"[{displayIndex}/{items.Count}] queued {item.Name} ({item.ManagerKey})",
                    $"[{displayIndex}/{items.Count}] 已排隊 {item.Name}（{item.ManagerKey}）")));
                var ticket = PackageOperationCoordinator.Enqueue(item, op, ct: cts.Token);
                return (item, ticket.Completion);
            }).ToList();

            while (pending.Count > 0)
            {
                var finishedTask = await Task.WhenAny(pending.Select(p => p.Completion));
                int found = pending.FindIndex(p => ReferenceEquals(p.Completion, finishedTask));
                if (found < 0) break;
                var current = pending[found];
                pending.RemoveAt(found);
                PackageOperationSnapshot snap;
                try { snap = await current.Completion; }
                catch (Exception ex)
                {
                    fail++;
                    DispatcherQueue.TryEnqueue(() => Append(P($"  ✗ failed: {ex.Message}", $"  ✗ 失敗：{ex.Message}")));
                    continue;
                }

                if (snap.Status == PackageOperationStatus.Succeeded) done++;
                else if (snap.Status == PackageOperationStatus.Skipped) skipped++;
                else fail++;
                var tail = string.IsNullOrWhiteSpace(snap.OutputTail) ? "" : "\n    " + snap.OutputTail.Replace("\n", "\n    ");
                var status = snap.Status switch
                {
                    PackageOperationStatus.Succeeded => P("  ✓ OK", "  ✓ 完成"),
                    PackageOperationStatus.Skipped => P("  ↷ skipped", "  ↷ 已略過"),
                    PackageOperationStatus.Cancelled => P("  ■ cancelled", "  ■ 已取消"),
                    _ => P("  ✗ failed", "  ✗ 失敗"),
                };
                DispatcherQueue.TryEnqueue(() => Append($"{status} — {snap.PackageName}{tail}"));
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                Append(P($"Done — {done} ok, {fail} failed, {skipped} skipped.",
                    $"完成 — {done} 成功、{fail} 失敗、{skipped} 略過。"));
                ResultsHeader.Text = P($"Batch: {done} ok, {fail} failed, {skipped} skipped.",
                    $"批次：{done} 成功、{fail} 失敗、{skipped} 略過。");
            });
        });

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
            var bundle = BundleService.ToBundle(items, item =>
                InstallOptions.HasOverride(item.ManagerKey, item.Id)
                    ? InstallOptions.Load(item.ManagerKey, item.Id)
                    : null);
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
            foreach (var w in report.Warnings.Take(10)) lines.Add("• " + w.Display);
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
            if (PackageManagerRegistry.ByKey(en.ManagerName) is null
                || !(_available.TryGetValue(en.ManagerName, out var a) && a)) continue;
            var label = string.IsNullOrEmpty(en.Name) ? en.Id : en.Name;
            ResultsHeader.Text = P($"Installing {label}… ({done + 1}/{pkgs.Count})", $"安裝緊 {label}…（{done + 1}/{pkgs.Count}）");
            var item = new PackageItem
            {
                ManagerKey = en.ManagerName,
                Id = en.Id,
                Name = en.Name,
                Version = en.Version,
                Source = en.Source,
            };
            var opts = en.InstallationOptions?.Clone() ?? InstallOptions.Load(en.ManagerName, en.Id);
            if (string.IsNullOrWhiteSpace(opts.Version) && !string.IsNullOrWhiteSpace(en.Version))
                opts.Version = en.Version;
            try
            {
                var snap = await PackageOperationCoordinator.RunAsync(
                    item, PackageOperations.Op.Install, opts, CancellationToken.None);
                if (snap.Status == PackageOperationStatus.Succeeded) done++;
            }
            catch { }
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
                installed ? null : (P("Install", "安裝"),
                    async () => await InstallSetupPackageOrThrowAsync(dep.Id, dep.En))));
        }

        // 完整上游原始碼已隨附作審核／移植基準；WinForge 唔會啟動外部 UniGetUI 程式。
        // The complete upstream source is vendored for audit/porting; WinForge never launches UniGetUI.
        ResultsPanel.Children.Add(SectionLabel(P("UniGetUI source parity", "UniGetUI 原始碼對等")));
        ResultsPanel.Children.Add(StatusRow(
            "Devolutions/UniGetUI · 21116375", "ThirdParty/UniGetUI",
            P("Complete pinned source included for provenance; WinForge's package features run natively without launching upstream.",
                "已收錄完整釘選原始碼作來源依據；WinForge 套件功能原生運行，唔會啟動上游程式。"), true, null));
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
                await InstallSetupPackageAsync(dep.Id, dep.En);
            }
        }
        finally { Busy.IsActive = false; PrimaryActionBtn.IsEnabled = true; }
        await LoadSetup();
    }

    /// <summary>Per-manager bootstrap so users can install a missing engine in one click.</summary>
    private (bool, Func<Task>) Bootstrap(string key) => key switch
    {
        "scoop" => (true, async () => await RunBootstrapScriptOrThrowAsync(
            "scoop", "Scoop",
            "Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force; Invoke-RestMethod -Uri https://get.scoop.sh | Invoke-Expression",
            elevated: false)),
        "choco" => (true, async () => await InstallSetupPackageOrThrowAsync("Chocolatey.Chocolatey", "Chocolatey")),
        "pip" => (true, async () => await InstallSetupPackageOrThrowAsync("Python.Python.3.12", "Python 3")),
        "npm" => (true, async () => await InstallSetupPackageOrThrowAsync("OpenJS.NodeJS.LTS", "Node.js LTS")),
        "dotnet" => (true, async () => await InstallSetupPackageOrThrowAsync("Microsoft.DotNet.SDK.9", ".NET SDK 9")),
        "cargo" => (true, async () => await InstallSetupPackageOrThrowAsync("Rustlang.Rustup", "Rustup")),
        "bun" => (true, async () => await InstallSetupPackageOrThrowAsync("Oven-sh.Bun", "Bun")),
        "pwsh7" => (true, async () => await InstallSetupPackageOrThrowAsync("Microsoft.PowerShell", "PowerShell 7")),
        _ => (false, () => Task.CompletedTask),
    };

    private async Task RunBootstrapScriptOrThrowAsync(
        string managerKey, string displayName, string script, bool elevated)
    {
        var snapshot = await PackageOperationCoordinator.RunCustomAsync(new PackageItem
        {
            ManagerKey = managerKey,
            Id = managerKey,
            Name = displayName,
            Source = "official-bootstrap",
        }, $"bootstrap-{managerKey}-v1",
            (progress, ct) => ShellRunner.RunPowershellStreaming(script, progress, elevated, ct),
            CancellationToken.None);

        if (snapshot.Status != PackageOperationStatus.Succeeded)
            throw new InvalidOperationException(snapshot.Result?.Message?.Display
                ?? P($"Could not install {displayName}.", $"無法安裝 {displayName}。"));
        PackageService.RefreshProcessPath();
    }

    /// <summary>Install a Setup-page winget dependency through the shared operation queue.</summary>
    private async Task<bool> InstallSetupPackageAsync(string id, string displayName)
    {
        var snapshot = await PackageOperationCoordinator.RunAsync(new PackageItem
        {
            ManagerKey = "winget",
            Id = id,
            Name = displayName,
            Source = "winget",
        }, PackageOperations.Op.Install, ct: CancellationToken.None);

        if (snapshot.Status == PackageOperationStatus.Succeeded)
        {
            _wingetInstalled.Add(id);
            PackageService.RefreshProcessPath();
            return true;
        }

        ResultsHeader.Text = snapshot.Result?.Message?.Display
            ?? P($"Could not install {displayName}.", $"無法安裝 {displayName}。");
        return false;
    }

    private async Task InstallSetupPackageOrThrowAsync(string id, string displayName)
    {
        if (!await InstallSetupPackageAsync(id, displayName))
            throw new InvalidOperationException(P($"Could not install {displayName}.", $"無法安裝 {displayName}。"));
    }

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
                bool known = SourceManager.KnownSourcesFor(managerKey)
                    .Any(k => string.Equals(k.Name, src.Name, StringComparison.OrdinalIgnoreCase));
                var confirm = new ContentDialog
                {
                    Title = P($"Remove source '{src.Name}'?", $"移除來源「{src.Name}」？"),
                    Content = new TextBlock
                    {
                        Text = known
                            ? P("This is a standard source. Packages from it will disappear until the source is restored.",
                                "呢個係標準來源；還原來源之前，當中嘅套件會消失。")
                            : P("Packages from this source will no longer be discoverable or updateable.",
                                "之後唔可以再搜尋或更新呢個來源嘅套件。"),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    PrimaryButtonText = P("Remove source", "移除來源"),
                    CloseButtonText = P("Cancel", "取消"),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot,
                };
                if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

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
        => BuildMoreFlyout(item, scope).ShowAt(btn);

    private MenuFlyout BuildMoreFlyout(PackageItem item, RowExtraScope scope)
    {
        var flyout = new MenuFlyout();

        // Copy install command.
        AddCopyInstallCommandItem(flyout.Items, item);

        // Download installer…
        AddDownloadInstallerItem(flyout.Items, item);

        // Chained ops vary per view.
        AddChainedOperationItems(flyout.Items, item, scope);

        return flyout;
    }

    private MenuFlyout BuildPackageContextFlyout(PackageItem item, RowExtraScope scope, string actionLabel, PackageOperations.Op op)
    {
        var flyout = new MenuFlyout();

        var details = new MenuFlyoutItem { Text = P("Details", "詳情") };
        details.Click += async (_, _) => await ShowDetails(item);
        flyout.Items.Add(details);

        var run = new MenuFlyoutItem { Text = actionLabel };
        run.Click += async (_, _) => await RunOperationFromMenu(item, op);
        flyout.Items.Add(run);

        var options = new MenuFlyoutItem
        {
            Text = op switch
            {
                PackageOperations.Op.Update => P("Update options…", "更新選項…"),
                PackageOperations.Op.Uninstall => P("Uninstall options…", "解除安裝選項…"),
                _ => P("Install options…", "安裝選項…"),
            },
        };
        options.Click += async (_, _) => await ShowOperationOptionsDialog(item, op);
        flyout.Items.Add(options);

        if (scope is RowExtraScope.Updates)
        {
            var ignore = new MenuFlyoutSubItem { Text = P("Ignore updates", "忽略更新") };
            AddIgnoreMenuItems(ignore.Items, item);
            flyout.Items.Add(ignore);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        AddCopyOperationCommandItem(flyout.Items, item, op);
        AddCopyInstallCommandItem(flyout.Items, item);

        var copyId = new MenuFlyoutItem { Text = P("Copy package ID", "複製套件 ID") };
        copyId.Click += (_, _) => CopyText(item.Id, P("package ID", "套件 ID"));
        flyout.Items.Add(copyId);

        var copyName = new MenuFlyoutItem { Text = P("Copy package name", "複製套件名稱") };
        copyName.Click += (_, _) => CopyText(item.Name, P("package name", "套件名稱"));
        flyout.Items.Add(copyName);

        var copyRef = new MenuFlyoutItem { Text = P("Copy package reference", "複製套件參照") };
        copyRef.Click += (_, _) => CopyText(
            $"manager={item.ManagerKey}\nid={item.Id}\nname={item.Name}\nversion={item.Version}\nsource={item.Source}",
            P("package reference", "套件參照"));
        flyout.Items.Add(copyRef);

        AddDownloadInstallerItem(flyout.Items, item);
        AddChainedOperationItems(flyout.Items, item, scope);

        return flyout;
    }

    private void AddCopyInstallCommandItem(IList<MenuFlyoutItemBase> items, PackageItem item)
    {
        var copy = new MenuFlyoutItem { Text = P("Copy install command", "複製安裝指令") };
        copy.Click += (_, _) =>
        {
            try
            {
                var options = InstallOptions.Load(item.ManagerKey, item.Id);
                CopyText(PackageOperations.BuildCommandPreview(
                    item.ManagerKey, item.Id, item.Source, PackageOperations.Op.Install, options),
                    P("install command", "安裝指令"));
            }
            catch (Exception ex) { ResultsHeader.Text = ex.Message; }
        };
        items.Add(copy);
    }

    private void AddCopyOperationCommandItem(IList<MenuFlyoutItemBase> items, PackageItem item, PackageOperations.Op op)
    {
        var label = op switch
        {
            PackageOperations.Op.Update => P("Copy update command", "複製更新指令"),
            PackageOperations.Op.Uninstall => P("Copy uninstall command", "複製解除安裝指令"),
            _ => P("Copy install command with options", "複製含選項安裝指令"),
        };
        var copy = new MenuFlyoutItem { Text = label };
        copy.Click += (_, _) =>
        {
            var opts = InstallOptions.Load(item.ManagerKey, item.Id);
            var cmd = PackageOperations.BuildCommandPreview(item.ManagerKey, item.Id, item.Source, op, opts);
            CopyText(cmd, P("operation command", "操作指令"));
        };
        items.Add(copy);
    }

    private void AddDownloadInstallerItem(IList<MenuFlyoutItemBase> items, PackageItem item)
    {
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
        items.Add(download);
    }

    private void AddChainedOperationItems(IList<MenuFlyoutItemBase> items, PackageItem item, RowExtraScope scope)
    {
        if (scope is RowExtraScope.Installed)
        {
            items.Add(new MenuFlyoutSeparator());
            var reinstall = new MenuFlyoutItem { Text = P("Reinstall", "重新安裝") };
            reinstall.Click += async (_, _) => await RunChainedFromRow(item,
                P("Reinstall", "重新安裝"),
                new (string, string, PackageOperations.Op)[]
                {
                    ("Uninstall", "解除安裝", PackageOperations.Op.Uninstall),
                    ("Install", "安裝", PackageOperations.Op.Install),
                });
            items.Add(reinstall);

            var unRe = new MenuFlyoutItem { Text = P("Uninstall then reinstall", "解除後重裝") };
            unRe.Click += async (_, _) => await RunChainedFromRow(item,
                P("Uninstall then reinstall", "解除後重裝"),
                new (string, string, PackageOperations.Op)[]
                {
                    ("Uninstall", "解除安裝", PackageOperations.Op.Uninstall),
                    ("Install", "安裝", PackageOperations.Op.Install),
                });
            items.Add(unRe);
        }
        else if (scope is RowExtraScope.Updates)
        {
            items.Add(new MenuFlyoutSeparator());
            var unUp = new MenuFlyoutItem { Text = P("Uninstall then update", "解除後更新") };
            unUp.Click += async (_, _) => await RunChainedFromRow(item,
                P("Uninstall then update", "解除後更新"),
                new (string, string, PackageOperations.Op)[]
                {
                    ("Uninstall", "解除安裝", PackageOperations.Op.Uninstall),
                    ("Install", "安裝", PackageOperations.Op.Install),
                    ("Update", "更新", PackageOperations.Op.Update),
                });
            items.Add(unUp);
        }
    }

    /// <summary>由行內鏈式操作執行並更新表頭 · Run a chained sequence of manager ops from a row, surfacing progress in the header.</summary>
    private async Task RunChainedFromRow(PackageItem item, string title,
        (string en, string zh, PackageOperations.Op op)[] steps)
    {
        if (PackageManagerRegistry.ByKey(item.ManagerKey) is null)
        { ResultsHeader.Text = P("Manager not available.", "管理器唔可用。"); return; }
        Busy.IsActive = true;
        try
        {
            int i = 0;
            foreach (var step in steps)
            {
                i++;
                ResultsHeader.Text = P($"{title}: [{i}/{steps.Length}] {step.en} {item.Name}…", $"{title}：[{i}/{steps.Length}] {step.zh} {item.Name}…");
                PackageOperationSnapshot snap;
                try { snap = await PackageOperationCoordinator.RunAsync(item, step.op, ct: CancellationToken.None); }
                catch (Exception ex)
                {
                    ResultsHeader.Text = ex.Message;
                    return;
                }
                if (snap.Status != PackageOperationStatus.Succeeded)
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
        => await ShowOperationOptionsDialog(item, PackageOperations.Op.Install);

    private async Task ShowOperationOptionsDialog(PackageItem item, PackageOperations.Op op)
    {
        // 載入每個套件嘅選項（無覆寫就跟全域）· Load per-package options (follows global if no override).
        var opts = InstallOptions.Load(item.ManagerKey, item.Id);
        var confirmed = await InstallOptionsDialog.ShowAsync(
            this.XamlRoot, item, opts, op);
        if (!confirmed) return;

        // 確認後重新載入（對話框已經寫咗覆寫或重設）· Reload after confirm; dialog persisted the choice.
        await RunOperationFromMenu(item, op);
    }

    private async Task RunOperationFromMenu(PackageItem item, PackageOperations.Op op)
    {
        var action = op switch
        {
            PackageOperations.Op.Update => P("Updating", "更新緊"),
            PackageOperations.Op.Uninstall => P("Uninstalling", "解除安裝緊"),
            _ => P("Installing", "安裝緊"),
        };
        var effective = InstallOptions.Load(item.ManagerKey, item.Id);
        ResultsHeader.Text = $"{action} {item.Name}…";
        Busy.IsActive = true;
        try
        {
            var snap = await PackageOperationCoordinator.RunAsync(
                item, op, effective, CancellationToken.None);
            var r = snap.Result;
            ResultsHeader.Text = snap.Status == PackageOperationStatus.Succeeded
                ? op switch
                {
                    PackageOperations.Op.Update => P($"Updated {item.Name}.", $"已更新 {item.Name}。"),
                    PackageOperations.Op.Uninstall => P($"Uninstalled {item.Name}.", $"已解除安裝 {item.Name}。"),
                    _ => P($"Installed {item.Name}.", $"已安裝 {item.Name}。"),
                }
                : r?.Message?.Primary ?? op switch
                {
                    PackageOperations.Op.Update => P($"Update failed for {item.Name}.", $"{item.Name} 更新失敗。"),
                    PackageOperations.Op.Uninstall => P($"Uninstall failed for {item.Name}.", $"{item.Name} 解除安裝失敗。"),
                    _ => P($"Install failed for {item.Name}.", $"{item.Name} 安裝失敗。"),
                };
        }
        catch (Exception ex) { ResultsHeader.Text = ex.Message; }
        finally { Busy.IsActive = false; }
    }

    private void CopyText(string text, string label)
    {
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText(text ?? "");
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
        ResultsHeader.Text = P($"Copied {label}.", $"已複製{label}。");
    }

    // ===== Row builders =====

    private async Task ActionInstall(PackageItem item, Button btn)
    {
        btn.IsEnabled = false; btn.Content = P("Installing…", "安裝緊…");
        var snap = await PackageOperationCoordinator.RunAsync(item, PackageOperations.Op.Install, ct: CancellationToken.None);
        bool ok = snap.Status == PackageOperationStatus.Succeeded;
        btn.Content = ok ? P("Installed", "已安裝") : P("Retry", "重試");
        btn.IsEnabled = !ok;
    }

    private async Task ActionUpdate(PackageItem item, Button btn)
    {
        btn.IsEnabled = false; btn.Content = P("Updating…", "更新緊…");
        var snap = await PackageOperationCoordinator.RunAsync(item, PackageOperations.Op.Update, ct: CancellationToken.None);
        bool ok = snap.Status == PackageOperationStatus.Succeeded;
        btn.Content = ok ? P("Updated", "已更新") : snap.Status == PackageOperationStatus.Skipped
            ? P("Skipped", "已略過") : P("Retry", "重試");
        btn.IsEnabled = !ok && snap.Status != PackageOperationStatus.Skipped;
    }

    private async Task ActionUninstall(PackageItem item, Button btn)
    {
        btn.IsEnabled = false; btn.Content = P("Removing…", "移除緊…");
        var snap = await PackageOperationCoordinator.RunAsync(item, PackageOperations.Op.Uninstall, ct: CancellationToken.None);
        bool ok = snap.Status == PackageOperationStatus.Succeeded;
        btn.Content = ok ? P("Removed", "已移除") : P("Retry", "重試");
        btn.IsEnabled = !ok;
    }

    private TextBlock SectionLabel(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        FontSize = 14,
        Margin = new Thickness(0, 10, 0, 2),
    };

    private Border RowFor(PackageItem item, string actionLabel, Func<Button, Task> action,
        List<(string label, Func<Button, Task> run)>? extras = null,
        RowExtraScope? contextScope = null,
        PackageOperations.Op? contextOperation = null)
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

        var card = Card(grid);
        if (contextScope is { } scope && contextOperation is { } op)
            card.ContextFlyout = BuildPackageContextFlyout(item, scope, actionLabel, op);
        return card;
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
