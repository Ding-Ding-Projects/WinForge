using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Pages;
using WinForge.Services;

namespace WinForge;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // 視窗模式（預設，約 82% 螢幕）＋ F11 切換全螢幕，會記住。
        // Windowed by default (~82% of the screen); F11 toggles full screen and the choice is remembered.
        ApplyWindowMode(SettingsStore.Get("fullscreen", "False") == "True");
        var f11 = new Microsoft.UI.Xaml.Input.KeyboardAccelerator { Key = Windows.System.VirtualKey.F11 };
        f11.Invoked += (_, e) => { ToggleFullScreen(); e.Handled = true; };
        RootGrid.KeyboardAccelerators.Add(f11);

        BuildCategoryMenu();
        BuildTitleMap();
        WireNavigator();

        RestoreSessionOrDefault();
        ApplyStartPage();

        // Ctrl+T 開新分頁、Ctrl+W 關閉分頁 · Ctrl+T new tab, Ctrl+W close tab.
        AddAccel(Windows.System.VirtualKey.T, () => AddTab("dashboard"));
        AddAccel(Windows.System.VirtualKey.W, CloseActiveTab);

        // 背景運行：關窗收入系統匣，剪貼簿監察繼續運行。
        // Keep running when closed: close hides to the tray; the clipboard monitor keeps going.
        ClipboardService.Start(DispatcherQueue);
        // 全域熱鍵泵：開機就跑，收入系統匣都繼續響。
        // Global hotkey pump: starts now so registered chords fire even while WinForge sits in the tray.
        HotkeyMacroService.StartHotkeys();
        TrayService.Install(ShowFromTray, QuitFromTray, "WinForge · 視窗調校");
        AppWindow.Closing += OnAppWindowClosing;
    }

    private bool _reallyQuit;

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_reallyQuit || !TrayService.IsInstalled) return;
        args.Cancel = true;       // don't exit — hide to the tray so background work continues
        AppWindow.Hide();
    }

    private void ShowFromTray()
    {
        AppWindow.Show();
        Activate();
    }

    /// <summary>開機自啟動：唔顯示視窗，淨係坐喺系統匣（背景服務照跑）· Login startup: stay hidden in the tray.</summary>
    public void StartHiddenInTray()
    {
        try { AppWindow.Hide(); } catch { /* tray icon already installed; services already running */ }
    }

    private void QuitFromTray()
    {
        _reallyQuit = true;
        TrayService.Remove();
        Application.Current.Exit();
    }

    private void ApplyWindowMode(bool fullscreen)
    {
        if (fullscreen)
        {
            AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
            return;
        }
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            int w = (int)(area.WorkArea.Width * 0.82);
            int h = (int)(area.WorkArea.Height * 0.86);
            AppWindow.Resize(new Windows.Graphics.SizeInt32(w, h));
            AppWindow.Move(new Windows.Graphics.PointInt32(
                area.WorkArea.X + (area.WorkArea.Width - w) / 2,
                area.WorkArea.Y + (area.WorkArea.Height - h) / 2));
        }
        catch { }
    }

    private void ToggleFullScreen()
    {
        bool full = AppWindow.Presenter.Kind == AppWindowPresenterKind.FullScreen;
        ApplyWindowMode(!full);
        SettingsStore.Set("fullscreen", (!full).ToString());
    }

    private void ApplyStartPage()
    {
        if (App.StartPage is string sp && sp.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
        {
            var query = sp.Substring("search:".Length);
            NavView.Loaded += (_, _) => DispatcherQueue.TryEnqueue(() =>
            {
                NavView.SelectedItem = null;
                NavigateActive("search:" + query);
            });
            return;
        }
        // Deep-link form: --page "weblogin?url=https://…" opens the in-app login on that URL.
        if (App.StartPage is string wl && wl.StartsWith("weblogin?", StringComparison.OrdinalIgnoreCase))
        {
            Navigator.GoToModule?.Invoke("module.weblogin");
            return;
        }
        switch (App.StartPage)
        {
            case "git":
            case "github":
                Navigator.GoToModule?.Invoke("module.git");
                break;
            case "ai":
            case "aiagents":
            case "claude":
            case "codex":
                Navigator.GoToModule?.Invoke("module.aiagents");
                break;
            case "resume":
            case "cv":
            case "coverletter":
                Navigator.GoToModule?.Invoke("module.resume");
                break;
            case "cloudflare":
            case "tunnel":
            case "cloudflared":
            case "warp":
                Navigator.GoToModule?.Invoke("module.cloudflare");
                break;
            case "weblogin":
            case "login":
            case "webview":
            case "signin":
                Navigator.GoToModule?.Invoke("module.weblogin");
                break;
            case "archives":
            case "archive":
                Navigator.GoToModule?.Invoke("module.archives");
                break;
            case "media":
                Navigator.GoToModule?.Invoke("module.media");
                break;
            case "regedit":
            case "registry":
                Navigator.GoToModule?.Invoke("module.regedit");
                break;
            case "doctors":
            case "systemdoctors":
            case "doctor":
                Navigator.GoToModule?.Invoke("module.doctors");
                break;
            case "services":
                Navigator.GoToModule?.Invoke("module.services");
                break;
            case "tasks":
            case "scheduledtasks":
                Navigator.GoToModule?.Invoke("module.tasks");
                break;
            case "devices":
                Navigator.GoToModule?.Invoke("module.devices");
                break;
            case "vivetool":
            case "vive":
            case "featureflags":
                Navigator.GoToModule?.Invoke("module.vivetool");
                break;
            case "startup":
                Navigator.GoToModule?.Invoke("module.startup");
                break;
            case "rename":
                Navigator.GoToModule?.Invoke("module.rename");
                break;
            case "bulkops":
            case "bulk":
                Navigator.GoToModule?.Invoke("module.bulkops");
                break;
            case "duplicates":
            case "dupes":
                Navigator.GoToModule?.Invoke("module.duplicates");
                break;
            case "disk":
            case "diskanalyzer":
                Navigator.GoToModule?.Invoke("module.disk");
                break;
            case "drives":
                Navigator.GoToModule?.Invoke("module.drives");
                break;
            case "uninstall":
            case "apps":
                Navigator.GoToModule?.Invoke("module.uninstall");
                break;
            case "windows":
            case "windowmanager":
                Navigator.GoToModule?.Invoke("module.windows");
                break;
            case "keyboard":
            case "remap":
                Navigator.GoToModule?.Invoke("module.keyboard");
                break;
            case "hotkeys":
            case "hotkey":
            case "macro":
            case "expander":
                Navigator.GoToModule?.Invoke("module.hotkeys");
                break;
            case "hosts":
                Navigator.GoToModule?.Invoke("module.hosts");
                break;
            case "mouse":
                Navigator.GoToModule?.Invoke("module.mouse");
                break;
            case "recorder":
            case "record":
                Navigator.GoToModule?.Invoke("module.recorder");
                break;
            case "capture":
            case "snip":
            case "screenshot":
                Navigator.GoToModule?.Invoke("module.capture");
                break;
            case "monitor":
            case "sysmon":
                Navigator.GoToModule?.Invoke("module.monitor");
                break;
            case "connections":
            case "netstat":
            case "tcp":
                Navigator.GoToModule?.Invoke("module.connections");
                break;
            case "events":
            case "eventlog":
            case "eventviewer":
                Navigator.GoToModule?.Invoke("module.events");
                break;
            case "mixer":
            case "volume":
            case "audio":
                Navigator.GoToModule?.Invoke("module.mixer");
                break;
            case "contextmenu":
            case "rightclick":
                Navigator.GoToModule?.Invoke("module.contextmenu");
                break;
            case "awake":
                Navigator.GoToModule?.Invoke("module.awake");
                break;
            case "colorpicker":
            case "color":
                Navigator.GoToModule?.Invoke("module.colorpicker");
                break;
            case "envvars":
            case "env":
                Navigator.GoToModule?.Invoke("module.envvars");
                break;
            case "clipboard":
            case "clip":
                Navigator.GoToModule?.Invoke("module.clipboard");
                break;
            case "packages":
            case "winget":
            case "install":
                Navigator.GoToModule?.Invoke("module.packages");
                break;
            case "adb":
            case "android":
                Navigator.GoToModule?.Invoke("module.adb");
                break;
            case "fastboot":
            case "flasher":
                Navigator.GoToModule?.Invoke("module.fastboot");
                break;
            case "emulator":
            case "avd":
                Navigator.GoToModule?.Invoke("module.emulator");
                break;
            case "vpn":
            case "nordvpn":
            case "tailscale":
                Navigator.GoToModule?.Invoke("module.vpn");
                break;
            case "comms":
            case "communications":
            case "mail":
            case "email":
            case "outlook":
            case "teams":
            case "discord":
            case "telegram":
            case "slack":
                Navigator.GoToModule?.Invoke("module.comms");
                break;
            case "configbackup":
            case "backup":
            case "config":
                Navigator.GoToModule?.Invoke("module.configbackup");
                break;
            case "native":
            case "pinvoke":
            case "system32":
                Navigator.GoToModule?.Invoke("module.native");
                break;
            case "powertoys":
            case "extras":
            case "ocr":
            case "imageresizer":
                Navigator.GoToModule?.Invoke("module.powertoys");
                break;
            case "wsl":
            case "vm":
            case "sandbox":
                Navigator.GoToModule?.Invoke("module.wslvm");
                break;
            case "onedrive":
                Navigator.GoToModule?.Invoke("module.onedrive");
                break;
            case "time":
            case "timezone":
            case "clock":
            case "unit":
                Navigator.GoToModule?.Invoke("module.timeunit");
                break;
            case "settingshub":
            case "controlpanel":
            case "mssettings":
                Navigator.GoToModule?.Invoke("module.settingshub");
                break;
            case "imaging":
            case "rpi":
            case "raspberrypi":
            case "minecraft":
                Navigator.GoToModule?.Invoke("module.imaging");
                break;
            case "voice":
            case "tts":
            case "speak":
                Navigator.GoToModule?.Invoke("module.voice");
                break;
            case "fonts":
            case "font":
                Navigator.GoToModule?.Invoke("module.fonts");
                break;
            case "homeassistant":
            case "ha":
            case "smarthome":
                Navigator.GoToModule?.Invoke("module.homeassistant");
                break;
            case "vault":
            case "vault-volumes":
            case "vaultvolumes":
            case "encrypt":
                Navigator.GoToModule?.Invoke("module.vault-volumes");
                break;
            case null:
            case "":
            case "dashboard":
                break;
            case "about":
                NavigateActive("about");
                break;
            case "settings":
                NavigateActive("settings");
                break;
            default:
                var cat = Categories.All.FirstOrDefault(c => c.Id == App.StartPage);
                if (cat is not null)
                    Navigator.GoToCategory?.Invoke(cat);
                break;
        }
    }

    private void BuildCategoryMenu()
    {
        // 將分類收納入可摺疊嘅分組，令導覽唔會太逼。
        // Nest tweak categories under collapsible groups so the pane stays tidy.
        foreach (var cat in Categories.All)
        {
            var parent = cat.Group switch
            {
                "recipes" => RecipesGroup,
                "tools" => ToolsGroup,
                _ => TweaksGroup,
            };
            parent.MenuItems.Add(new NavigationViewItem
            {
                Content = $"{cat.Name.En} · {cat.Name.Zh}",
                Tag = cat.Id,
                Icon = new FontIcon { Glyph = cat.Glyph },
            });
        }
    }

    private void WireNavigator()
    {
        Navigator.GoToCategory = cat =>
        {
            var item = FindByTag(cat.Id);
            if (item is not null) NavView.SelectedItem = item;
        };

        Navigator.GoToSettings = () => NavigateActive("settings");

        Navigator.GoToModule = key =>
        {
            var item = FindByTag(key);
            if (item is not null && ReferenceEquals(NavView.SelectedItem, item)) NavigateActive(key); // already selected → re-navigate active tab
            else if (item is not null) NavView.SelectedItem = item;
            else NavigateActive(key); // fall back to direct navigation if not in the pane
        };
    }

    /// <summary>Resolve a nav item by Tag, searching nested groups recursively (pane + footer).</summary>
    private NavigationViewItem? FindByTag(string tag)
        => FindByTag(NavView.MenuItems, tag) ?? FindByTag(NavView.FooterMenuItems, tag);

    private static NavigationViewItem? FindByTag(System.Collections.Generic.IList<object> items, string tag)
    {
        foreach (var o in items)
        {
            if (o is NavigationViewItem nvi)
            {
                if ((nvi.Tag as string) == tag) return nvi;
                var child = FindByTag(nvi.MenuItems, tag);
                if (child is not null) return child;
            }
        }
        return null;
    }

    private static Type MapType(string key) => key switch
    {
        "module.git" => typeof(GitHubModule),
        "module.aiagents" => typeof(AiAgentsModule),
        "module.resume" => typeof(ResumeWriterModule),
        "module.cloudflare" => typeof(CloudflareModule),
        "module.weblogin" => typeof(WebLoginModule),
        "module.ssh" => typeof(SshModule),
        "module.archives" => typeof(ArchivesModule),
        "module.media" => typeof(MediaModule),
        "module.regedit" => typeof(RegistryEditor),
        "module.doctors" => typeof(SystemDoctorsModule),
        "module.services" => typeof(ServicesModule),
        "module.tasks" => typeof(ScheduledTasksModule),
        "module.devices" => typeof(DevicesModule),
        "module.vivetool" => typeof(ViveToolModule),
        "module.startup" => typeof(StartupModule),
        "module.rename" => typeof(RenameModule),
        "module.bulkops" => typeof(BulkOpsModule),
        "module.duplicates" => typeof(DuplicatesModule),
        "module.disk" => typeof(DiskAnalyzerModule),
        "module.drives" => typeof(DrivesModule),
        "module.uninstall" => typeof(AppUninstallerModule),
        "module.windows" => typeof(WindowManagerModule),
        "module.keyboard" => typeof(KeyboardModule),
        "module.hotkeys" => typeof(HotkeyMacroModule),
        "module.hosts" => typeof(HostsEditorModule),
        "module.mouse" => typeof(MouseModule),
        "module.recorder" => typeof(ScreenRecorderModule),
        "module.capture" => typeof(CaptureStudioModule),
        "module.monitor" => typeof(SystemMonitorModule),
        "module.battery" => typeof(BatteryThermalModule),
        "module.connections" => typeof(ConnectionsModule),
        "module.events" => typeof(EventViewerModule),
        "module.mixer" => typeof(VolumeMixerModule),
        "module.contextmenu" => typeof(ContextMenuModule),
        "module.awake" => typeof(AwakeModule),
        "module.colorpicker" => typeof(ColorPickerModule),
        "module.envvars" => typeof(EnvVarsModule),
        "module.clipboard" => typeof(ClipboardModule),
        "module.packages" => typeof(PackageManagerModule),
        "module.adb" => typeof(AndroidAdbModule),
        "module.fastboot" => typeof(FastbootModule),
        "module.emulator" => typeof(EmulatorModule),
        "module.vpn" => typeof(VpnMeshModule),
        "module.homeassistant" => typeof(HomeAssistantModule),
        "module.comms" => typeof(CommunicationsModule),
        "module.configbackup" => typeof(ConfigBackupModule),
        "module.native" => typeof(NativeUtilitiesModule),
        "module.powertoys" => typeof(PowerToysExtrasModule),
        "module.wslvm" => typeof(WslVmModule),
        "module.fonts" => typeof(FontManagerModule),
        "module.onedrive" => typeof(OneDriveModule),
        "module.timeunit" => typeof(TimeUnitModule),
        "module.settingshub" => typeof(SettingsHubModule),
        "module.imaging" => typeof(ImagingGameModule),
        "module.voice" => typeof(VoiceModule),
        "module.vault-volumes" => typeof(VaultVolumesModule),
        _ => typeof(DashboardPage),
    };

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var q = sender.Text ?? "";
        if (q.Trim().Length == 0) { sender.ItemsSource = null; return; }
        var sugg = ModuleRegistry.Search(q).Select(m => $"{m.En} · {m.Zh}")
            .Concat(TweakCatalog.Search(q).Take(6).Select(t => $"{t.Title.En} · {t.Title.Zh}"))
            .Take(10).ToList();
        sender.ItemsSource = sugg;
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var q = args.QueryText;
        if (!string.IsNullOrWhiteSpace(q)) NavigateActive("search:" + q);
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (ActiveFrame is { CanGoBack: true } f) { f.GoBack(); UpdateBackButton(); }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_syncingTabs) return;
        if (args.IsSettingsSelected) { NavigateActive("settings"); return; }
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag) NavigateActive(tag);
    }

    // ===================== Browser-style tabs · 瀏覽器式分頁 =====================
    // Each tab owns its own navigation Frame. The tab title is the page you're on,
    // a new tab opens the Dashboard, and the open tabs are mirrored to a local git
    // repo (TabSessionService) so the whole session can be exported / restored.

    private bool _syncingTabs;
    private bool _restoring;
    private readonly Dictionary<Type, string> _titles = new();

    private Frame? ActiveFrame => (Tabs?.SelectedItem as TabViewItem)?.Content as Frame;

    private void AddAccel(Windows.System.VirtualKey key, Action action)
    {
        var a = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = key,
            Modifiers = Windows.System.VirtualKeyModifiers.Control,
        };
        a.Invoked += (_, e) => { action(); e.Handled = true; };
        RootGrid.KeyboardAccelerators.Add(a);
    }

    private void BuildTitleMap()
    {
        _titles[typeof(DashboardPage)] = "Dashboard · 概覽";
        _titles[typeof(AboutPage)] = "About · 關於";
        _titles[typeof(SettingsPage)] = "Settings · 設定";
        _titles[typeof(SearchResultsPage)] = "Search · 搜尋";
        foreach (var m in ModuleRegistry.All)
            _titles[MapType(m.Tag)] = $"{m.En} · {m.Zh}";
    }

    /// <summary>Resolve a tab/nav key into a page type + parameter.</summary>
    private (Type type, object? param) Resolve(string key)
    {
        switch (key)
        {
            case "dashboard": return (typeof(DashboardPage), null);
            case "about": return (typeof(AboutPage), null);
            case "settings": return (typeof(SettingsPage), null);
        }
        if (key.StartsWith("search:", StringComparison.OrdinalIgnoreCase))
            return (typeof(SearchResultsPage), key.Substring("search:".Length));
        if (key.StartsWith("module.", StringComparison.Ordinal))
            return (MapType(key), null);
        var cat = Categories.All.FirstOrDefault(c => c.Id == key);
        if (cat is not null) return (typeof(CategoryPage), cat);
        return (typeof(DashboardPage), null);
    }

    private string TitleFor(string key, Type type, object? param)
    {
        if (type == typeof(CategoryPage) && param is AppCategory c) return $"{c.Name.En} · {c.Name.Zh}";
        if (type == typeof(SearchResultsPage)) return param is string q && q.Length > 0 ? $"Search: {q}" : "Search · 搜尋";
        return _titles.TryGetValue(type, out var t) ? t : "WinForge";
    }

    /// <summary>Navigate the active tab to a key; opens a tab if none exist.</summary>
    private void NavigateActive(string key)
    {
        if (Tabs.TabItems.Count == 0) { AddTab(key); return; }
        if (Tabs.SelectedItem is not TabViewItem tab || tab.Content is not Frame frame) { AddTab(key); return; }
        var (type, param) = Resolve(key);
        frame.Navigate(type, param);
        tab.Tag = key;
        tab.Header = TitleFor(key, type, param);
        UpdateBackButton();
        SaveSession();
    }

    private TabViewItem AddTab(string key = "dashboard", bool select = true)
    {
        var (type, param) = Resolve(key);
        var frame = new Frame();
        frame.Navigated += (_, _) => UpdateBackButton();
        var tab = new TabViewItem
        {
            Tag = key,
            Header = TitleFor(key, type, param),
            Content = frame,
            IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource { Symbol = Symbol.Document },
        };
        Tabs.TabItems.Add(tab);
        if (select) Tabs.SelectedItem = tab;
        frame.Navigate(type, param);
        UpdateBackButton();
        SaveSession();
        return tab;
    }

    private void CloseActiveTab()
    {
        if (Tabs.SelectedItem is TabViewItem tab) CloseTab(tab);
    }

    private void CloseTab(TabViewItem tab)
    {
        Tabs.TabItems.Remove(tab);
        if (Tabs.TabItems.Count == 0) AddTab("dashboard");
        SaveSession();
    }

    private void UpdateBackButton()
    {
        try { AppTitleBar.IsBackButtonVisible = ActiveFrame?.CanGoBack == true; } catch { }
    }

    private void Tabs_AddTabButtonClick(TabView sender, object args) => AddTab("dashboard");

    private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab is TabViewItem tab) CloseTab(tab);
    }

    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Tabs.SelectedItem is not TabViewItem tab) return;
        UpdateBackButton();
        var key = tab.Tag as string;
        if (string.IsNullOrEmpty(key)) return;
        var item = FindByTag(key);
        if (item is not null && !ReferenceEquals(NavView.SelectedItem, item))
        {
            _syncingTabs = true;
            try { NavView.SelectedItem = item; } finally { _syncingTabs = false; }
        }
        SaveSession();
    }

    // ===================== Session: persist / export / import =====================

    private void SaveSession()
    {
        if (Tabs is null || _restoring) return;
        var keys = Tabs.TabItems.OfType<TabViewItem>().Select(t => (t.Tag as string) ?? "dashboard");
        TabSessionService.Save(keys, Tabs.SelectedIndex < 0 ? 0 : Tabs.SelectedIndex);
    }

    private void RestoreSessionOrDefault()
    {
        var data = TabSessionService.Load();
        if (data is null || data.Tabs.Count == 0) { AddTab("dashboard"); return; }
        ReloadTabs(data);
    }

    private void ReloadTabs(TabSessionService.SessionData data)
    {
        _restoring = true;
        try
        {
            Tabs.TabItems.Clear();
            foreach (var key in data.Tabs) AddTab(key, select: false);
            if (Tabs.TabItems.Count == 0) AddTab("dashboard", select: false);
            var active = (data.Active >= 0 && data.Active < Tabs.TabItems.Count) ? data.Active : 0;
            Tabs.SelectedItem = Tabs.TabItems[active];
        }
        finally { _restoring = false; }
        SaveSession();
    }

    private void Session_NewTab(object sender, RoutedEventArgs e) => AddTab("dashboard");

    private async void Session_Export(object sender, RoutedEventArgs e)
    {
        SaveSession();
        var path = await FileDialogs.SaveFileAsync($"winforge-tabs-{DateTime.Now:yyyyMMdd-HHmm}", ".json");
        if (!string.IsNullOrEmpty(path)) { try { TabSessionService.ExportTo(path); } catch { } }
    }

    private async void Session_Import(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".json");
        if (string.IsNullOrEmpty(path)) return;
        var data = TabSessionService.ImportFrom(path);
        if (data is not null) ReloadTabs(data);
    }

    private void Session_Restore(object sender, RoutedEventArgs e)
    {
        var data = TabSessionService.Load();
        if (data is not null) ReloadTabs(data);
    }

    private void Session_OpenFolder(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{TabSessionService.Folder}\"") { UseShellExecute = true }); } catch { }
    }
}
