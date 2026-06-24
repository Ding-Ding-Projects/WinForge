using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Pages;
using WinForge.Services;

namespace WinTune;

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
        WireNavigator();

        NavFrame.Navigate(typeof(DashboardPage));
        ApplyStartPage();

        // 背景運行：關窗收入系統匣，剪貼簿監察繼續運行。
        // Keep running when closed: close hides to the tray; the clipboard monitor keeps going.
        ClipboardService.Start(DispatcherQueue);
        // 全域熱鍵泵：開機就跑，收入系統匣都繼續響。
        // Global hotkey pump: starts now so registered chords fire even while WinTune sits in the tray.
        HotkeyMacroService.StartHotkeys();
        TrayService.Install(ShowFromTray, QuitFromTray, "WinTune · 視窗調校");
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
                NavFrame.Navigate(typeof(SearchResultsPage), query);
            });
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
            case "cloudflare":
            case "tunnel":
            case "cloudflared":
            case "warp":
                Navigator.GoToModule?.Invoke("module.cloudflare");
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
            case null:
            case "":
            case "dashboard":
                break;
            case "about":
                NavFrame.Navigate(typeof(AboutPage));
                break;
            case "settings":
                NavFrame.Navigate(typeof(SettingsPage));
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

        Navigator.GoToSettings = () => NavFrame.Navigate(typeof(SettingsPage));

        Navigator.GoToModule = key =>
        {
            var item = FindByTag(key);
            if (item is not null) NavView.SelectedItem = item;
            else NavFrame.Navigate(MapType(key)); // fall back to direct navigation if not in the pane
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
        "module.cloudflare" => typeof(CloudflareModule),
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
        if (!string.IsNullOrWhiteSpace(q)) NavFrame.Navigate(typeof(SearchResultsPage), q);
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void TitleBar_BackRequested(TitleBar sender, object args)
    {
        if (NavFrame.CanGoBack) NavFrame.GoBack();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            NavFrame.Navigate(typeof(SettingsPage));
            return;
        }

        if (args.SelectedItem is not NavigationViewItem item) return;
        var tag = item.Tag as string;

        switch (tag)
        {
            case "dashboard":
                NavFrame.Navigate(typeof(DashboardPage));
                break;
            case "about":
                NavFrame.Navigate(typeof(AboutPage));
                break;
            case "module.git":
                NavFrame.Navigate(typeof(GitHubModule));
                break;
            case "module.aiagents":
                NavFrame.Navigate(typeof(AiAgentsModule));
                break;
            case "module.cloudflare":
                NavFrame.Navigate(typeof(CloudflareModule));
                break;
            case "module.archives":
                NavFrame.Navigate(typeof(ArchivesModule));
                break;
            case "module.media":
                NavFrame.Navigate(typeof(MediaModule));
                break;
            case "module.regedit":
                NavFrame.Navigate(typeof(RegistryEditor));
                break;
            case "module.doctors":
                NavFrame.Navigate(typeof(SystemDoctorsModule));
                break;
            case "module.services":
                NavFrame.Navigate(typeof(ServicesModule));
                break;
            case "module.tasks":
                NavFrame.Navigate(typeof(ScheduledTasksModule));
                break;
            case "module.devices":
                NavFrame.Navigate(typeof(DevicesModule));
                break;
            case "module.vivetool":
                NavFrame.Navigate(typeof(ViveToolModule));
                break;
            case "module.startup":
                NavFrame.Navigate(typeof(StartupModule));
                break;
            case "module.rename":
                NavFrame.Navigate(typeof(RenameModule));
                break;
            case "module.bulkops":
                NavFrame.Navigate(typeof(BulkOpsModule));
                break;
            case "module.duplicates":
                NavFrame.Navigate(typeof(DuplicatesModule));
                break;
            case "module.disk":
                NavFrame.Navigate(typeof(DiskAnalyzerModule));
                break;
            case "module.drives":
                NavFrame.Navigate(typeof(DrivesModule));
                break;
            case "module.uninstall":
                NavFrame.Navigate(typeof(AppUninstallerModule));
                break;
            case "module.windows":
                NavFrame.Navigate(typeof(WindowManagerModule));
                break;
            case "module.keyboard":
                NavFrame.Navigate(typeof(KeyboardModule));
                break;
            case "module.hotkeys":
                NavFrame.Navigate(typeof(HotkeyMacroModule));
                break;
            case "module.hosts":
                NavFrame.Navigate(typeof(HostsEditorModule));
                break;
            case "module.mouse":
                NavFrame.Navigate(typeof(MouseModule));
                break;
            case "module.recorder":
                NavFrame.Navigate(typeof(ScreenRecorderModule));
                break;
            case "module.capture":
                NavFrame.Navigate(typeof(CaptureStudioModule));
                break;
            case "module.monitor":
                NavFrame.Navigate(typeof(SystemMonitorModule));
                break;
            case "module.battery":
                NavFrame.Navigate(typeof(BatteryThermalModule));
                break;
            case "module.connections":
                NavFrame.Navigate(typeof(ConnectionsModule));
                break;
            case "module.events":
                NavFrame.Navigate(typeof(EventViewerModule));
                break;
            case "module.mixer":
                NavFrame.Navigate(typeof(VolumeMixerModule));
                break;
            case "module.contextmenu":
                NavFrame.Navigate(typeof(ContextMenuModule));
                break;
            case "module.awake":
                NavFrame.Navigate(typeof(AwakeModule));
                break;
            case "module.colorpicker":
                NavFrame.Navigate(typeof(ColorPickerModule));
                break;
            case "module.envvars":
                NavFrame.Navigate(typeof(EnvVarsModule));
                break;
            case "module.clipboard":
                NavFrame.Navigate(typeof(ClipboardModule));
                break;
            case "module.packages":
                NavFrame.Navigate(typeof(PackageManagerModule));
                break;
            case "module.adb":
                NavFrame.Navigate(typeof(AndroidAdbModule));
                break;
            case "module.fastboot":
                NavFrame.Navigate(typeof(FastbootModule));
                break;
            case "module.emulator":
                NavFrame.Navigate(typeof(EmulatorModule));
                break;
            case "module.vpn":
                NavFrame.Navigate(typeof(VpnMeshModule));
                break;
            case "module.homeassistant":
                NavFrame.Navigate(typeof(HomeAssistantModule));
                break;
            case "module.comms":
                NavFrame.Navigate(typeof(CommunicationsModule));
                break;
            case "module.configbackup":
                NavFrame.Navigate(typeof(ConfigBackupModule));
                break;
            case "module.native":
                NavFrame.Navigate(typeof(NativeUtilitiesModule));
                break;
            case "module.powertoys":
                NavFrame.Navigate(typeof(PowerToysExtrasModule));
                break;
            case "module.wslvm":
                NavFrame.Navigate(typeof(WslVmModule));
                break;
            case "module.fonts":
                NavFrame.Navigate(typeof(FontManagerModule));
                break;
            case "module.onedrive":
                NavFrame.Navigate(typeof(OneDriveModule));
                break;
            case "module.timeunit":
                NavFrame.Navigate(typeof(TimeUnitModule));
                break;
            case "module.settingshub":
                NavFrame.Navigate(typeof(SettingsHubModule));
                break;
            case "module.imaging":
                NavFrame.Navigate(typeof(ImagingGameModule));
                break;
            case "module.voice":
                NavFrame.Navigate(typeof(VoiceModule));
                break;
            default:
                var cat = Categories.All.FirstOrDefault(c => c.Id == tag);
                if (cat is not null)
                    NavFrame.Navigate(typeof(CategoryPage), cat);
                break;
        }
    }
}
