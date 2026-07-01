using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 Android 模擬器 + SDK 套件管理 · In-app Android emulator control AND SDK package manager,
/// wrapping the SDK's emulator + avdmanager + sdkmanager. Two tabs: AVDs (list/create/launch/wipe/delete)
/// and SDK Packages (list installed/available, install/update/uninstall, accept licenses, channel select).
/// No redirect — drives the real SDK tools.
/// </summary>
public sealed partial class EmulatorModule : Page
{
    private List<SdkPackage> _allPackages = new();

    public EmulatorModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
        Loaded += async (_, _) => { Render(); InitChannel(); InitQuickInstall(); CheckEngine(); await RefreshAvds(); RenderSdkRoot(); };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private string? SelectedAvd => (AvdList.SelectedItem as Avd)?.Name;
    private SdkPackage? SelectedPkg => PkgList.SelectedItem as SdkPackage;

    // ===================== rendering =====================

    private void Render()
    {
        Header.Title = "Android Emulator & SDK · Android 模擬器與 SDK";
        HeaderBlurb.Text = P("Control Android Virtual Devices and manage the SDK from one place — list/create/launch/wipe AVDs, and list/install/update/uninstall SDK packages and accept licenses. Needs the Android SDK (emulator + cmdline-tools).",
            "喺一個地方控制 Android 虛擬裝置同管理 SDK — 列出／建立／啟動／清資料 AVD，又可以列出／安裝／更新／移除 SDK 套件同接受授權。需要 Android SDK（emulator + cmdline-tools）。");

        AvdTab.Header = P("Virtual Devices (AVDs)", "虛擬裝置（AVD）");
        SdkTab.Header = P("SDK Packages", "SDK 套件");

        // AVD tab
        CreateBtn.Content = P("Create AVD…", "建立 AVD…");
        ColdBootCheck.Content = P("Cold boot (no snapshot)", "冷開機（唔用快照）");
        LaunchBtn.Content = P("Launch", "啟動");
        StopBtn.Content = P("Stop", "停止");
        WipeBtn.Content = P("Wipe data", "清空資料");
        DeleteBtn.Content = P("Delete", "刪除");

        // SDK tab
        OpenSdkBtn.Content = P("Open SDK folder", "開 SDK 資料夾");
        ChannelLabel.Text = P("Channel", "頻道");
        AcceptLicensesBtn.Content = P("Accept all SDK licenses", "接受所有 SDK 授權");
        UpdateAllBtn.Content = P("Update all", "全部更新");
        QuickInstallBtn.Content = P("Install", "安裝");
        FilterBox.PlaceholderText = P("Filter packages (e.g. platforms, ndk, system-images)…", "篩選套件（例如 platforms、ndk、system-images）…");
        InstalledOnlyToggle.Content = P("Installed only", "只睇已安裝");
        InstallSelBtn.Content = P("Install / Update", "安裝／更新");
        UninstallSelBtn.Content = P("Uninstall", "移除");

        RenderSdkRoot();
        // Re-render channel labels keeping selection.
        var sel = ChannelBox.SelectedIndex;
        FillChannel();
        if (sel >= 0 && sel < ChannelBox.Items.Count) ChannelBox.SelectedIndex = sel;
        FillQuickInstall();
    }

    private void RenderSdkRoot()
    {
        var root = EmulatorService.SdkRoot();
        SdkRootLabel.Text = P("Android SDK location", "Android SDK 位置");
        SdkRootPath.Text = root.Length > 0 ? root : P("(not found — set ANDROID_SDK_ROOT or install the SDK)", "（搵唔到 — 請設定 ANDROID_SDK_ROOT 或安裝 SDK）");
        OpenSdkBtn.IsEnabled = root.Length > 0;
    }

    private void InitChannel() { FillChannel(); ChannelBox.SelectedIndex = EmulatorService.Channel; }

    private void FillChannel()
    {
        ChannelBox.Items.Clear();
        ChannelBox.Items.Add(P("Stable (0)", "穩定版（0）"));
        ChannelBox.Items.Add(P("Beta (1)", "測試版（1）"));
        ChannelBox.Items.Add(P("Dev (2)", "開發版（2）"));
        ChannelBox.Items.Add(P("Canary (3)", "金絲雀版（3）"));
    }

    private void InitQuickInstall() { FillQuickInstall(); if (QuickInstallBox.Items.Count > 0) QuickInstallBox.SelectedIndex = 0; }

    private void FillQuickInstall()
    {
        var sel = QuickInstallBox.SelectedIndex;
        QuickInstallBox.Items.Clear();
        foreach (var s in SdkOperations.QuickInstall)
            QuickInstallBox.Items.Add(new ComboBoxItem { Content = P(s.En, s.Zh), Tag = s.Id });
        if (sel >= 0 && sel < QuickInstallBox.Items.Count) QuickInstallBox.SelectedIndex = sel;
        else if (QuickInstallBox.Items.Count > 0) QuickInstallBox.SelectedIndex = 0;
    }

    // ===================== engine =====================

    private void CheckEngine()
    {
        var (ok, en, zh) = EmulatorService.Health();
        EngineBar.IsOpen = !ok || !EmulatorService.HasJava();
        EngineBar.ActionButton = null;

        if (!ok)
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Android SDK not ready", "Android SDK 未準備好");
            EngineBar.Message = P(en, zh);
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                "Google.AndroidStudio",
                "Install Android Studio / SDK automatically", "自動安裝 Android Studio／SDK",
                async () => { CheckEngine(); RenderSdkRoot(); await RefreshAvds(); });
            return;
        }

        if (!EmulatorService.HasJava())
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Java (JDK) not found", "搵唔到 Java（JDK）");
            EngineBar.Message = P("sdkmanager / avdmanager are .bat wrappers that need a JDK. Set JAVA_HOME or install a JDK, otherwise package operations will fail.",
                "sdkmanager／avdmanager 係 .bat 包裝，需要 JDK。請設定 JAVA_HOME 或安裝 JDK，否則套件操作會失敗。");
            return;
        }

        EngineBar.Severity = InfoBarSeverity.Success;
        EngineBar.Title = P("Android SDK found", "搵到 Android SDK");
        EngineBar.Message = P(en, zh);
    }

    // ===================== AVD tab =====================

    private async Task RefreshAvds()
    {
        Busy.IsActive = true;
        var avds = await EmulatorService.ListAvds();
        Busy.IsActive = false;
        AvdList.ItemsSource = avds;
        if (avds.Count > 0) AvdList.SelectedIndex = 0;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) { CheckEngine(); RenderSdkRoot(); await RefreshAvds(); }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var images = await EmulatorService.ListSystemImages();
        if (images.Count == 0)
        {
            // Deep-link: offer to jump to the SDK Packages tab to install one.
            var prompt = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = P("No system images", "冇系統映像"),
                Content = P("An AVD needs an installed system image. Open the SDK Packages tab to install one (e.g. system-images;android-34;google_apis;x86_64)?",
                    "建立 AVD 需要已安裝嘅系統映像。要唔要去「SDK 套件」分頁安裝一個（例如 system-images;android-34;google_apis;x86_64）？"),
                PrimaryButtonText = P("Open SDK Packages", "去 SDK 套件"),
                CloseButtonText = P("Cancel", "取消"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await prompt.ShowAsync() == ContentDialogResult.Primary)
            {
                Tabs.SelectedItem = SdkTab;
                FilterBox.Text = "system-images";
                await SdkRefresh();
            }
            return;
        }

        var nameBox = new TextBox { PlaceholderText = "my_pixel", Header = P("AVD name", "AVD 名") };
        var imgBox = new ComboBox { Header = P("System image", "系統映像"), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var im in images) imgBox.Items.Add(im.Package);
        imgBox.SelectedIndex = 0;
        var devBox = new TextBox { PlaceholderText = "pixel_7  (optional)", Header = P("Device profile (optional)", "裝置設定檔（可選）") };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(nameBox);
        panel.Children.Add(imgBox);
        panel.Children.Add(devBox);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Create AVD", "建立 AVD"),
            Content = panel,
            PrimaryButtonText = P("Create", "建立"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var name = (nameBox.Text ?? "").Trim();
        if (name.Length == 0) { Notify(InfoBarSeverity.Warning, P("Name required", "要填名"), ""); return; }

        Busy.IsActive = true;
        var r = await EmulatorService.CreateAvd(name, imgBox.SelectedItem as string ?? "", (devBox.Text ?? "").Trim());
        Busy.IsActive = false;
        Report(P("Create AVD", "建立 AVD"), r);
        if (r.Success) await RefreshAvds();
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAvd is not { } name) { NeedPick(); return; }
        var r = EmulatorService.Launch(name, wipeData: false, coldBoot: ColdBootCheck.IsChecked == true);
        Report(P("Launch", "啟動"), r);
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        var r = await EmulatorService.Stop();
        Busy.IsActive = false;
        Report(P("Stop", "停止"), r);
    }

    private async void Wipe_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAvd is not { } name) { NeedPick(); return; }
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Wipe AVD data?", "清空 AVD 資料？"),
            Content = name + "\n\n" + P("This erases the AVD's user data and cold-boots it fresh.", "呢個會抹走 AVD 嘅使用者資料，並冷開機重來。"),
            PrimaryButtonText = P("Wipe", "清空"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var r = EmulatorService.Wipe(name);
        Report(P("Wipe data", "清空資料"), r);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedAvd is not { } name) { NeedPick(); return; }
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete AVD?", "刪除 AVD？"),
            Content = name + "\n\n" + P("This permanently removes the AVD and its data.", "呢個會永久刪除 AVD 同佢嘅資料。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        Busy.IsActive = true;
        var r = await EmulatorService.DeleteAvd(name);
        Busy.IsActive = false;
        Report(P("Delete", "刪除"), r);
        if (r.Success) await RefreshAvds();
    }

    private void NeedPick() => Notify(InfoBarSeverity.Warning, P("Pick an AVD first", "請先揀一個 AVD"), "");

    // ===================== SDK Packages tab =====================

    private bool _sdkLoaded;
    private async void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(Tabs.SelectedItem, SdkTab) && !_sdkLoaded && EmulatorService.SdkManager.Length > 0)
        {
            _sdkLoaded = true;
            await SdkRefresh();
        }
    }

    private void Channel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ChannelBox.SelectedIndex >= 0) EmulatorService.Channel = ChannelBox.SelectedIndex;
    }

    private void OpenSdk_Click(object sender, RoutedEventArgs e)
    {
        if (!EmulatorService.OpenSdkFolder())
            Notify(InfoBarSeverity.Warning, P("No SDK folder", "冇 SDK 資料夾"),
                P("Set ANDROID_SDK_ROOT or install the SDK first.", "請先設定 ANDROID_SDK_ROOT 或安裝 SDK。"));
    }

    private void AppendSdk(string text)
    {
        SdkConsole.Text += (SdkConsole.Text.Length > 0 ? "\n" : "") + text;
        SdkConsole.Select(SdkConsole.Text.Length, 0);
    }

    private void SdkBusyOn(string action)
    {
        SdkBusy.IsActive = true;
        AppendSdk($"$ {action}");
    }

    private void SdkBusyOff(TweakResult r)
    {
        SdkBusy.IsActive = false;
        var msg = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        if (!string.IsNullOrWhiteSpace(r.Output)) AppendSdk(r.Output!.Trim());
        AppendSdk((r.Success ? "✓ " : "✗ ") + msg);
    }

    private async void SdkRefresh_Click(object sender, RoutedEventArgs e) => await SdkRefresh();

    private async Task SdkRefresh()
    {
        if (EmulatorService.SdkManager.Length == 0)
        {
            AppendSdk(P("sdkmanager not found — install the SDK command-line tools first.", "搵唔到 sdkmanager — 請先安裝 SDK 命令列工具。"));
            return;
        }
        SdkBusy.IsActive = true;
        AppendSdk(P("Loading packages…", "載入緊套件…"));
        _allPackages = await EmulatorService.ListAvailablePackages();
        SdkBusy.IsActive = false;
        ApplyFilter();
        int installed = _allPackages.Count(p => p.Installed);
        AppendSdk(P($"{_allPackages.Count} packages ({installed} installed).", $"{_allPackages.Count} 個套件（已安裝 {installed} 個）。"));
    }

    private void Filter_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();
    private void InstalledOnly_Click(object sender, RoutedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = (FilterBox.Text ?? "").Trim().ToLowerInvariant();
        bool installedOnly = InstalledOnlyToggle.IsChecked == true;
        IEnumerable<SdkPackage> items = _allPackages;
        if (installedOnly) items = items.Where(p => p.Installed);
        if (q.Length > 0) items = items.Where(p => p.Path.ToLowerInvariant().Contains(q) || p.Description.ToLowerInvariant().Contains(q) || p.Category.Contains(q));
        // Group order follows SdkOperations.Categories, installed first within a group.
        var order = SdkOperations.Categories.Select((c, i) => (c.Key, i)).ToDictionary(t => t.Key, t => t.i);
        var sorted = items
            .OrderBy(p => order.TryGetValue(p.Category, out var idx) ? idx : 999)
            .ThenByDescending(p => p.Installed)
            .ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
        PkgList.ItemsSource = sorted;
    }

    private async void AcceptLicenses_Click(object sender, RoutedEventArgs e)
    {
        SdkBusyOn("sdkmanager --licenses (auto-accept)");
        var r = await EmulatorService.AcceptLicenses();
        SdkBusyOff(r);
    }

    private async void UpdateAll_Click(object sender, RoutedEventArgs e)
    {
        SdkBusyOn("sdkmanager --update");
        var r = await EmulatorService.UpdatePackages();
        SdkBusyOff(r);
        await SdkRefresh();
    }

    private async void QuickInstall_Click(object sender, RoutedEventArgs e)
    {
        if (QuickInstallBox.SelectedItem is not ComboBoxItem item || item.Tag is not string id || id.Length == 0)
        {
            Notify(InfoBarSeverity.Warning, P("Pick a package", "請揀一個套件"), "");
            return;
        }
        await DoInstall(id);
    }

    private async void InstallSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPkg is not { } pkg) { NeedPickPkg(); return; }
        await DoInstall(pkg.Path);
    }

    private async Task DoInstall(string id)
    {
        SdkBusyOn($"sdkmanager \"{id}\"");
        var r = await EmulatorService.InstallPackage(id);
        SdkBusyOff(r);
        await SdkRefresh();
    }

    private async void UninstallSelected_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPkg is not { } pkg) { NeedPickPkg(); return; }
        if (!pkg.Installed)
        {
            Notify(InfoBarSeverity.Warning, P("Not installed", "未安裝"),
                P("That package is not installed.", "嗰個套件未安裝。"));
            return;
        }
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Uninstall package?", "移除套件？"),
            Content = pkg.Path + "\n\n" + P("This removes the package from the SDK.", "呢個會由 SDK 移除呢個套件。"),
            PrimaryButtonText = P("Uninstall", "移除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        SdkBusyOn($"sdkmanager --uninstall \"{pkg.Path}\"");
        var r = await EmulatorService.Uninstall(pkg.Path);
        SdkBusyOff(r);
        await SdkRefresh();
    }

    private void NeedPickPkg() => Notify(InfoBarSeverity.Warning, P("Pick a package first", "請先揀一個套件"), "");

    // ===================== shared =====================

    private void Report(string title, TweakResult r)
        => Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, title,
            (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "");

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        EngineBar.IsOpen = true; EngineBar.Severity = sev; EngineBar.Title = title; EngineBar.Message = msg; EngineBar.ActionButton = null;
    }
}
