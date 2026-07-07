using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Camoufox 設定檔管理器 · Camoufox profile manager — create / edit / delete / launch anti-detect
/// browser profiles (cookies + fingerprints stored as files), export one / selected / all, and manage
/// the local git repo that auto-commits every change. The only app it launches is Camoufox itself,
/// cloning + building it from source on first use. Fully in-app, fully bilingual.
///
/// Freeze-safe: the page renders instantly, then each tab lazy-loads its data the first time it is
/// shown, and every git/file call is pushed onto the thread pool with <see cref="Task.Run(Func{Task})"/>
/// so the synchronous <c>Process.Start</c> spawn never runs on the UI thread.
/// </summary>
public sealed partial class CamoufoxModule : Page
{
    private bool _busy;
    private bool _profilesLoaded, _commitsLoaded, _engineLoaded;

    public CamoufoxModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EasyToggle.IsOn = EasyMode;
        Render();
        BuildGuide();
        MainPivot.SelectionChanged += MainPivot_SelectionChanged;
        // Cheap engine probe for the header chip (file-existence checks) — still off the UI thread.
        _ = ProbeEngineChipAsync();
        // Load whatever tab we start on; the rest load lazily on first view.
        _ = EnsureTabLoadedAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        MainPivot.SelectionChanged -= MainPivot_SelectionChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private async void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => await EnsureTabLoadedAsync();

    /// <summary>第一次睇某個分頁先至載入佢嘅資料 · Lazy-load a tab's data the first time it becomes visible.</summary>
    private async Task EnsureTabLoadedAsync()
    {
        try
        {
            var tab = MainPivot.SelectedItem;
            if (ReferenceEquals(tab, TabProfiles) && !_profilesLoaded) { _profilesLoaded = true; await RefreshProfiles(); }
            else if (ReferenceEquals(tab, TabGit) && !_commitsLoaded) { _commitsLoaded = true; await RefreshCommits(); }
            else if (ReferenceEquals(tab, TabEngine) && !_engineLoaded) { _engineLoaded = true; await RefreshEngine(); }
        }
        catch (Exception ex) { CrashLogger.Log("camoufox.tabload", ex); }
    }

    // ───────────────────────── Easy Mode + guided checklists ─────────────────────────

    private const string KeyEasy = "camoufox.easymode";
    private bool EasyMode
    {
        get => SettingsStore.Get(KeyEasy, "true") == "true";   // guided by default
        set => SettingsStore.Set(KeyEasy, value ? "true" : "false");
    }

    private sealed record GuideStep(string Text, Action Go);

    private void EasyToggle_Toggled(object sender, RoutedEventArgs e)
    {
        EasyMode = EasyToggle.IsOn;
        BuildGuide();
    }

    /// <summary>深連結：跳去某個分頁再聚焦控制項 · Deep-link: switch to a tab and focus the control.</summary>
    private void DeepLink(PivotItem tab, Control? focus = null, Action? then = null)
    {
        MainPivot.SelectedItem = tab;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            try { focus?.Focus(FocusState.Programmatic); then?.Invoke(); } catch (Exception ex) { CrashLogger.Log("camoufox.deeplink", ex); }
        });
    }

    private void BuildGuide()
    {
        if (GuideHost is null) return;
        EasyTitle.Text = P("Easy Mode — guided checklists", "簡易模式 — 導引清單");
        EasyDesc.Text = EasyMode
            ? P("On: each step has a “Go →” button that jumps you straight to the right screen and control.",
                "開：每個步驟有「前往 →」掣，直接帶你去對應嘅畫面同控制項。")
            : P("Off: steps are a manual checklist you tick yourself. Turn on for one-click deep links.",
                "關：步驟係你自己剔嘅清單。開咗就有一鍵深連結。");

        GuideHost.Children.Clear();

        GuideHost.Children.Add(BuildChecklist(
            P("Set up Camoufox", "設定 Camoufox"),
            P("Get the engine, then make and launch your first profile.", "取得引擎，然後整並啟動你第一個設定檔。"),
            new List<GuideStep>
            {
                new(P("Install the Camoufox engine (clone & build from source).", "安裝 Camoufox 引擎（clone 並由原始碼建置）。"),
                    () => DeepLink(TabEngine, BuildBtn)),
                new(P("Create your first profile.", "建立你第一個設定檔。"),
                    () => DeepLink(TabProfiles, NewBtn, () => New_Click(NewBtn, new RoutedEventArgs()))),
                new(P("Launch a profile in Camoufox.", "用 Camoufox 啟動設定檔。"),
                    () => DeepLink(TabProfiles, LaunchBtn)),
            }));

        GuideHost.Children.Add(BuildChecklist(
            P("Create & sync a profile", "建立並同步設定檔"),
            P("Profiles store cookies + fingerprints as files; every change is committed.", "設定檔以檔案儲存 cookies 同指紋；每次改動都會 commit。"),
            new List<GuideStep>
            {
                new(P("New profile — name it.", "新增設定檔 — 改個名。"),
                    () => DeepLink(TabProfiles, NewBtn, () => New_Click(NewBtn, new RoutedEventArgs()))),
                new(P("Edit the fingerprint (UA, locale, timezone, proxy…).", "編輯指紋（UA、地區、時區、代理…）。"),
                    () => DeepLink(TabProfiles, EditBtn)),
                new(P("Sync now — commit pending changes to the local git store.", "立即同步 — 將待處理改動 commit 入本地 git 倉庫。"),
                    () => DeepLink(TabGit, SyncNowBtn)),
                new(P("Export the profile to a .zip to back it up or move it.", "將設定檔匯出做 .zip 備份或搬遷。"),
                    () => DeepLink(TabProfiles, ExportSelBtn)),
            }));

        GuideHost.Children.Add(BuildChecklist(
            P("Manage git commits", "管理 git commit"),
            P("Browse history, restore a point in time, and push off-machine.", "瀏覽歷史、還原到某時間點、push 去機外。"),
            new List<GuideStep>
            {
                new(P("Open the commit history.", "開啟 commit 歷史。"),
                    () => DeepLink(TabGit, RefreshCommitsBtn)),
                new(P("Sync now to commit anything pending.", "立即同步，commit 待處理嘅嘢。"),
                    () => DeepLink(TabGit, SyncNowBtn)),
                new(P("Set a remote URL and enable push-on-sync.", "設定遠端網址並開啟同步即 push。"),
                    () => DeepLink(TabGit, RemoteUrlBox)),
                new(P("Push the whole profile store to the remote now.", "立即將成個設定檔倉庫 push 上遠端。"),
                    () => DeepLink(TabGit, PushNowBtn)),
            }));
    }

    private UIElement BuildChecklist(string title, string desc, List<GuideStep> steps)
    {
        var card = new StackPanel { Spacing = 10 };
        var border = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 14, 16, 14),
            Child = card,
        };
        card.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 15 });
        card.Children.Add(new TextBlock { Text = desc, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

        int n = 1;
        foreach (var s in steps)
        {
            var row = new Grid { ColumnSpacing = 12, Margin = new Thickness(0, 4, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (EasyMode)
            {
                var badge = new Border
                {
                    Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                    Background = (Brush)Application.Current.Resources["WinForgeBrandBrush"],
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = n.ToString(), FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 6, 33, 15)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                };
                Grid.SetColumn(badge, 0);
                row.Children.Add(badge);
            }
            else
            {
                var cb = new CheckBox { MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(cb, 0);
                row.Children.Add(cb);
            }

            var text = new TextBlock { Text = s.Text, FontSize = 13, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1);
            row.Children.Add(text);

            if (EasyMode)
            {
                var go = new Button
                {
                    Content = P("Go →", "前往 →"),
                    Padding = new Thickness(12, 5, 12, 5),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var action = s.Go;
                go.Click += (_, _) => { try { action(); } catch (Exception ex) { CrashLogger.Log("camoufox.guide", ex); } };
                Grid.SetColumn(go, 2);
                row.Children.Add(go);
            }

            card.Children.Add(row);
            n++;
        }
        return border;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Camoufox Profiles · Camoufox 指紋設定檔";

        TabGuide.Header = P("Guide", "導引");
        TabProfiles.Header = P("Profiles", "設定檔");
        TabGit.Header = P("Git / History", "Git／歷史");
        TabEngine.Header = P("Engine", "引擎");
        HeaderBlurb.Text = P(
            "Anti-detect browser profiles — cookies & fingerprints as files in a local git repo that commits every change.",
            "指紋瀏覽器設定檔 — cookies 同指紋以檔案存喺本地 git 倉庫，每次改動都會 commit。");

        // Profiles toolbar: New has text; the rest are icon buttons with tooltips (set in XAML).
        NewBtnText.Text = P("New profile", "新增設定檔");
        EmptyNewBtnText.Text = P("New profile", "新增設定檔");
        ProfilesEmptyText.Text = P("No profiles yet. Create your first Camoufox profile to store its cookies and fingerprint.",
            "未有設定檔。整你第一個 Camoufox 設定檔，儲存佢嘅 cookies 同指紋。");
        ProfilesLoadingText.Text = P("Loading profiles…", "載入設定檔中…");

        GitTitle.Text = P("Local git store (auto-commits every change)", "本地 git 倉庫（每次改動自動 commit）");
        GitDesc.Text = P("Every create / edit / delete / launch / import is committed automatically. \"Sync\" commits any pending changes in one go; below is the full commit history for this feature.",
            "每次新增／編輯／刪除／啟動／匯入都會自動 commit。「同步」會將所有待處理改動一次過 commit；下面係呢個功能嘅完整 commit 歷史。");
        SyncNoteBox.PlaceholderText = P("Optional sync note…", "可填同步備註…");
        SyncNowBtn.Content = P("Sync now", "立即同步");
        RefreshCommitsBtn.Content = P("Refresh", "重新整理");
        RemoteTitle.Text = P("Remote (push the profile store off-machine)", "遠端（將設定檔倉庫 push 到機外）");
        RemoteUrlBox.PlaceholderText = P("https://… or git@…  (may embed a token)", "https://… 或 git@…（可含權杖）");
        SaveRemoteBtn.Content = P("Save remote", "儲存遠端");
        PushNowBtn.Content = P("Push now", "立即 push");
        PushOnSyncCheck.Content = P("Push to the remote on every Sync", "每次同步都 push 上遠端");
        RemoteUrlBox.Text = CamoufoxService.RemoteUrl;
        PushOnSyncCheck.IsChecked = CamoufoxService.PushOnSync;
        HistoryLabel.Text = P("Commit history", "Commit 歷史");
        GitOutputTitle.Text = P("Git output", "Git 輸出");
        CommitsEmpty.Text = P("No commits yet.", "未有 commit。");

        EngineTitle.Text = P("Camoufox engine", "Camoufox 引擎");
        EngineDesc.Text = P("Camoufox is required to launch profiles. If it isn't installed, WinForge clones the source and builds/fetches the engine for you. This is the only external program WinForge installs or launches.",
            "啟動設定檔需要 Camoufox。如果未安裝，WinForge 會幫你 clone 原始碼並建置／取得引擎。呢個係 WinForge 唯一會安裝或啟動嘅外部程式。");
        BuildBtn.Content = P("Clone & build from source", "Clone 並由原始碼建置");
        DetectBtn.Content = P("Re-detect", "重新偵測");
        BuildLogTitle.Text = P("Build log", "建置記錄");
        RepoPath.Text = CamoufoxService.StoreDir;

        // Chips reflect current known state (updated again after async probes).
        UpdateCountChip(ProfileList.ItemsSource is IReadOnlyCollection<CamoufoxProfile> c ? c.Count : (int?)null);
    }

    // ───────────────────────── header chips ─────────────────────────

    private void UpdateCountChip(int? count)
    {
        CountChipText.Text = count is null
            ? P("Profiles", "設定檔")
            : P($"{count} profile(s)", $"{count} 個設定檔");
    }

    private void SetEngineChip(bool installed)
    {
        EngineChipText.Text = installed ? P("Engine ready", "引擎就緒") : P("Engine not installed", "引擎未安裝");
        var key = installed ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush";
        try { EngineDot.Fill = (Brush)Application.Current.Resources[key]; } catch { }
    }

    private async Task ProbeEngineChipAsync()
    {
        try
        {
            var exe = await Task.Run(() => CamoufoxService.LocateExecutable());
            SetEngineChip(exe is not null);
        }
        catch (Exception ex) { CrashLogger.Log("camoufox.chip", ex); }
    }

    private void SetProfilesLoading(bool on)
    {
        ProfilesLoading.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) ProfilesEmpty.Visibility = Visibility.Collapsed;
    }

    // ───────────────────────── result/output plumbing ─────────────────────────

    private void Show(TweakResult r, string verb, TextBlock? outputTarget = null)
    {
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = r.Message is null ? verb : $"{verb} — {r.Message.Primary}";
        ResultBar.IsOpen = true;
        if (outputTarget is not null && !string.IsNullOrWhiteSpace(r.Output))
            outputTarget.Text = r.Output;
    }

    private async Task Run(Func<Task<TweakResult>> op, string verb, TextBlock? outputTarget = null)
    {
        if (_busy) return;
        _busy = true;
        try { Show(await Task.Run(op), verb, outputTarget); }
        catch (Exception ex)
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            ResultBar.Message = ex.Message;
            ResultBar.IsOpen = true;
            CrashLogger.Log("camoufox", ex);
        }
        finally { _busy = false; }
    }

    // ───────────────────────── profiles ─────────────────────────

    private async Task RefreshProfiles()
    {
        try
        {
            SetProfilesLoading(true);
            // Offload the git EnsureRepo + manifest reads to the pool so the UI thread stays responsive.
            var profiles = await Task.Run(() => CamoufoxService.ListProfilesAsync());
            ProfileList.ItemsSource = profiles;
            ProfilesEmpty.Visibility = profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateCountChip(profiles.Count);
            await RefreshPending();
        }
        catch (Exception ex) { CrashLogger.Log("camoufox.refresh", ex); }
        finally { SetProfilesLoading(false); }
    }

    private async void RefreshProfiles_Click(object sender, RoutedEventArgs e) => await RefreshProfiles();

    private List<CamoufoxProfile> Selected() =>
        ProfileList.SelectedItems.OfType<CamoufoxProfile>().ToList();

    /// <summary>Git 歷史過時就重載（或標記待重載）· Reload commit history now if the Git tab is showing, else on next view.</summary>
    private void InvalidateCommits()
    {
        _commitsLoaded = false;
        if (ReferenceEquals(MainPivot.SelectedItem, TabGit)) { _commitsLoaded = true; _ = RefreshCommits(); }
    }

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        var profile = CamoufoxService.NewProfile(P("New profile", "新設定檔"));
        var edited = await EditProfileDialog(profile, isNew: true);
        if (edited is null) return;
        await Run(() => CamoufoxService.SaveProfileAsync(edited), P("Create profile", "新增設定檔"));
        await RefreshProfiles();
        InvalidateCommits();
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel.Count != 1)
        {
            Warn(P("Select exactly one profile to edit.", "請揀一個設定檔嚟編輯。"));
            return;
        }
        await EditOne(sel[0]);
    }

    private async void RowEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CamoufoxProfile p) await EditOne(p);
    }

    private async Task EditOne(CamoufoxProfile profile)
    {
        var edited = await EditProfileDialog(profile, isNew: false);
        if (edited is null) return;
        await Run(() => CamoufoxService.SaveProfileAsync(edited), P("Edit profile", "編輯設定檔"));
        await RefreshProfiles();
        InvalidateCommits();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel.Count == 0) { Warn(P("Select one or more profiles to delete.", "請揀一個或多個設定檔嚟刪除。")); return; }

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete profiles?", "刪除設定檔？"),
            Content = P($"Permanently delete {sel.Count} profile(s) and their cookies/fingerprints? This is committed to the git store and can be restored from history.",
                       $"永久刪除 {sel.Count} 個設定檔連 cookies／指紋？呢個會 commit 入 git 倉庫，可由歷史還原。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await SafeShowAsync(dlg) != ContentDialogResult.Primary) return;

        foreach (var p in sel)
            await Run(() => CamoufoxService.DeleteProfileAsync(p), P("Delete profile", "刪除設定檔"));
        await RefreshProfiles();
        InvalidateCommits();
    }

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel.Count != 1) { Warn(P("Select exactly one profile to launch.", "請揀一個設定檔嚟啟動。")); return; }
        await LaunchOne(sel[0]);
    }

    private async void RowLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is CamoufoxProfile p) await LaunchOne(p);
    }

    private async Task LaunchOne(CamoufoxProfile profile)
    {
        await Run(() => CamoufoxService.LaunchProfileAsync(profile), P("Launch profile", "啟動設定檔"));
        InvalidateCommits();
    }

    private async void ExportSelected_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected();
        if (sel.Count == 0) { Warn(P("Select one or more profiles to export.", "請揀一個或多個設定檔嚟匯出。")); return; }
        var path = await FileDialogs.SaveFileAsync($"camoufox-profiles-{DateTime.Now:yyyyMMdd-HHmm}", ".zip");
        if (path is null) return;
        await Run(() => CamoufoxService.ExportProfilesAsync(sel, path), P("Export selected", "匯出已選"));
    }

    private async void ExportAll_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync($"camoufox-all-{DateTime.Now:yyyyMMdd-HHmm}", ".zip");
        if (path is null) return;
        await Run(() => CamoufoxService.ExportAllAsync(path), P("Export all", "全部匯出"));
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".zip");
        if (path is null) return;
        await Run(() => CamoufoxService.ImportAsync(path), P("Import profiles", "匯入設定檔"));
        await RefreshProfiles();
        InvalidateCommits();
    }

    // ───────────────────────── editor dialog ─────────────────────────

    private async Task<CamoufoxProfile?> EditProfileDialog(CamoufoxProfile src, bool isNew)
    {
        // Work on a copy so Cancel discards changes.
        var p = new CamoufoxProfile
        {
            Id = src.Id, Name = src.Name, Notes = src.Notes, Tags = src.Tags,
            CreatedUtc = src.CreatedUtc, UpdatedUtc = src.UpdatedUtc,
            UserAgent = src.UserAgent, Locale = src.Locale, Timezone = src.Timezone,
            OsName = src.OsName, ScreenWidth = src.ScreenWidth, ScreenHeight = src.ScreenHeight,
            Proxy = src.Proxy, ConfigJson = src.ConfigJson,
        };

        TextBox MakeBox(string val, string placeholder, bool multiline = false) => new()
        {
            Text = val ?? "",
            PlaceholderText = placeholder,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Height = multiline ? 90 : double.NaN,
            FontFamily = multiline ? new FontFamily("Consolas") : FontFamily,
        };

        var nameBox = MakeBox(p.Name, P("Profile name", "設定檔名稱"));
        var notesBox = MakeBox(p.Notes, P("Notes", "備註"), multiline: false);
        var uaBox = MakeBox(p.UserAgent, P("User agent (blank = Camoufox default)", "User agent（留空 = Camoufox 預設）"));
        var osBox = new ComboBox { ItemsSource = new[] { "windows", "macos", "linux" }, SelectedItem = string.IsNullOrWhiteSpace(p.OsName) ? "windows" : p.OsName, MinWidth = 160 };
        var localeBox = MakeBox(p.Locale, "en-US");
        var tzBox = MakeBox(p.Timezone, "America/New_York");
        var swBox = MakeBox(p.ScreenWidth, "1920");
        var shBox = MakeBox(p.ScreenHeight, "1080");
        var proxyBox = MakeBox(p.Proxy, P("http://user:pass@host:port or socks5://host:port", "http://user:pass@host:port 或 socks5://host:port"));
        var configBox = MakeBox(p.ConfigJson, P("Extra Camoufox config as JSON (overrides the fields above)", "額外 Camoufox JSON 設定（會覆寫以上欄位）"), multiline: true);

        Grid Row(string label, FrameworkElement input)
        {
            var g = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 0, 0, 2) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var t = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 13, TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(t, 0); Grid.SetColumn(input, 1);
            g.Children.Add(t); g.Children.Add(input);
            return g;
        }

        var screen = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        screen.Children.Add(swBox); screen.Children.Add(new TextBlock { Text = "×", VerticalAlignment = VerticalAlignment.Center }); screen.Children.Add(shBox);

        var panel = new StackPanel { Spacing = 8, MinWidth = 460 };
        panel.Children.Add(Row(P("Name", "名稱"), nameBox));
        panel.Children.Add(Row(P("Notes", "備註"), notesBox));
        panel.Children.Add(Row(P("OS", "作業系統"), osBox));
        panel.Children.Add(Row(P("User agent", "User agent"), uaBox));
        panel.Children.Add(Row(P("Locale", "地區"), localeBox));
        panel.Children.Add(Row(P("Timezone", "時區"), tzBox));
        panel.Children.Add(Row(P("Screen", "螢幕"), screen));
        panel.Children.Add(Row(P("Proxy", "代理"), proxyBox));
        panel.Children.Add(Row(P("Extra config", "額外設定"), configBox));

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isNew ? P("New Camoufox profile", "新增 Camoufox 設定檔") : P("Edit profile", "編輯設定檔"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 520, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        dlg.Resources["ContentDialogMaxWidth"] = 720.0;

        if (await SafeShowAsync(dlg) != ContentDialogResult.Primary) return null;

        p.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? P("Unnamed", "未命名") : nameBox.Text.Trim();
        p.Notes = notesBox.Text.Trim();
        p.UserAgent = uaBox.Text.Trim();
        p.OsName = osBox.SelectedItem as string ?? "windows";
        p.Locale = localeBox.Text.Trim();
        p.Timezone = tzBox.Text.Trim();
        p.ScreenWidth = swBox.Text.Trim();
        p.ScreenHeight = shBox.Text.Trim();
        p.Proxy = proxyBox.Text.Trim();
        p.ConfigJson = configBox.Text.Trim();
        return p;
    }

    // ───────────────────────── git history ─────────────────────────

    private async Task RefreshPending()
    {
        try
        {
            int pending = await Task.Run(() => CamoufoxService.PendingChangesAsync());
            PendingStatus.Text = pending == 0
                ? P("In sync — no pending changes.", "已同步 — 冇待處理改動。")
                : P($"{pending} pending change(s) not yet committed.", $"有 {pending} 項改動仲未 commit。");
        }
        catch { }
    }

    private async Task RefreshCommits()
    {
        try
        {
            var commits = await Task.Run(() => CamoufoxService.ListCommitsAsync());
            CommitList.ItemsSource = commits;
            CommitsEmpty.Visibility = commits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            await RefreshPending();
        }
        catch (Exception ex) { CrashLogger.Log("camoufox.commits", ex); }
    }

    private async void RefreshCommits_Click(object sender, RoutedEventArgs e) => await RefreshCommits();

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        var note = SyncNoteBox.Text?.Trim();
        await Run(() => CamoufoxService.SyncAsync(note), P("Sync", "同步"), GitOutput);
        SyncNoteBox.Text = "";
        await RefreshCommits();
    }

    private void SaveRemote_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            CamoufoxService.RemoteUrl = RemoteUrlBox.Text?.Trim() ?? "";
            Show(TweakResult.Ok("Remote saved.", "已儲存遠端。"), P("Save remote", "儲存遠端"));
        }
        catch (Exception ex) { CrashLogger.Log("camoufox.remote", ex); Warn(ex.Message); }
    }

    private void PushOnSync_Click(object sender, RoutedEventArgs e)
    {
        try { CamoufoxService.PushOnSync = PushOnSyncCheck.IsChecked == true; }
        catch (Exception ex) { CrashLogger.Log("camoufox.pushonsync", ex); }
    }

    private async void PushNow_Click(object sender, RoutedEventArgs e)
    {
        CamoufoxService.RemoteUrl = RemoteUrlBox.Text?.Trim() ?? "";
        await Run(() => CamoufoxService.PushAsync(), P("Push", "Push"), GitOutput);
    }

    private async void CommitDiff_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not CamoufoxCommit c) return;
        await Run(() => CamoufoxService.DiffCommitAsync(c.Hash), P("Diff", "差異"), GitOutput);
    }

    private async void CommitRestore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not CamoufoxCommit c) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Restore this commit?", "還原到呢個 commit？"),
            Content = P($"Restore all profiles to commit {c.ShortHash}? Your current state is committed first as a safety point.",
                       $"將所有設定檔還原到 commit {c.ShortHash}？而家嘅狀態會先 commit 做安全點。"),
            PrimaryButtonText = P("Restore", "還原"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await SafeShowAsync(dlg) != ContentDialogResult.Primary) return;
        await Run(() => CamoufoxService.RestoreCommitAsync(c.Hash), P("Restore", "還原"), GitOutput);
        await RefreshProfiles();
        await RefreshCommits();
    }

    // ───────────────────────── engine ─────────────────────────

    private async Task RefreshEngine()
    {
        try
        {
            var exe = await Task.Run(() => CamoufoxService.LocateExecutable());
            if (exe is not null)
            {
                EngineStatusBar.Severity = InfoBarSeverity.Success;
                EngineStatusBar.Title = P("Camoufox is installed", "Camoufox 已安裝");
                EngineStatusBar.Message = P("You can launch profiles.", "可以啟動設定檔。");
                ExePath.Text = exe;
            }
            else
            {
                EngineStatusBar.Severity = InfoBarSeverity.Warning;
                EngineStatusBar.Title = P("Camoufox not installed", "Camoufox 未安裝");
                EngineStatusBar.Message = P("Clone & build from source to enable launching.", "Clone 並由原始碼建置先可以啟動。");
                ExePath.Text = P("(not found)", "（搵唔到）");
            }
            SetEngineChip(exe is not null);
            RefreshGitPrereq(exe is not null);
        }
        catch (Exception ex) { CrashLogger.Log("camoufox.engine", ex); }
    }

    /// <summary>
    /// git 係「clone &amp; build」嘅前置條件 · git is a prerequisite for the clone-and-build step. When the
    /// engine isn't installed yet and git is missing, offer a one-click winget install with a rich progress
    /// control (bar + live status + % + Cancel + success/error animation); hide it once git is present.
    /// </summary>
    private void RefreshGitPrereq(bool engineInstalled)
    {
        bool hasGit = HasGit();
        if (engineInstalled || hasGit)
        {
            GitBar.IsOpen = false;
            GitInstallHost.Children.Clear();
            return;
        }
        GitBar.IsOpen = true;
        GitBar.Severity = InfoBarSeverity.Warning;
        GitBar.Title = P("Git not found", "搵唔到 Git");
        GitBar.Message = P("Building Camoufox from source needs Git. Install it automatically (winget) — live progress below.",
            "由原始碼建置 Camoufox 需要 Git。自動安裝（winget）— 下面有即時進度。");
        GitInstallHost.Children.Clear();
        GitInstallHost.Children.Add(EngineBars.AutoInstallProgress(
            "Git.Git", "Install Git", "安裝 Git",
            recheck: async () => { PackageService.RefreshProcessPath(); await RefreshEngine(); },
            rescan: PackageService.RefreshProcessPath));
    }

    /// <summary>PATH 上有冇 git · Whether a git executable is resolvable on PATH.</summary>
    private static bool HasGit()
    {
        try
        {
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = System.IO.Path.Combine(dir.Trim(), "git.exe");
                if (System.IO.File.Exists(p)) return true;
            }
        }
        catch { }
        return false;
    }

    private async void Detect_Click(object sender, RoutedEventArgs e) => await RefreshEngine();

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        BuildRing.IsActive = true;
        BuildBtn.IsEnabled = false;
        BuildLog.Text = "";
        try
        {
            void Log(string s)
            {
                if (string.IsNullOrEmpty(s)) return;
                DispatcherQueue.TryEnqueue(() =>
                {
                    BuildLog.Text += s + "\n";
                    BuildLogScroller.ChangeView(null, BuildLogScroller.ScrollableHeight, null);
                });
            }
            var r = await Task.Run(() => CamoufoxService.CloneAndBuildAsync(Log));
            Show(r, P("Clone & build", "Clone 並建置"));
        }
        catch (Exception ex)
        {
            CrashLogger.Log("camoufox.build", ex);
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            ResultBar.Message = ex.Message;
            ResultBar.IsOpen = true;
        }
        finally
        {
            BuildRing.IsActive = false;
            BuildBtn.IsEnabled = true;
            _busy = false;
            await RefreshEngine();
        }
    }

    // ───────────────────────── helpers ─────────────────────────

    private void Warn(string message)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "提提你");
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    /// <summary>包住 ShowAsync，避免「同時開兩個 dialog」會 crash · Guard ShowAsync (fail open).</summary>
    private static async Task<ContentDialogResult> SafeShowAsync(ContentDialog dlg)
    {
        try { return await dlg.ShowAsync(); }
        catch (Exception ex) { CrashLogger.Log("camoufox.dialog", ex); return ContentDialogResult.None; }
    }
}
