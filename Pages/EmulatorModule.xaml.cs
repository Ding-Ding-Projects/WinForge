using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 Android 模擬器控制 · In-app Android emulator control wrapping the SDK's emulator + avdmanager +
/// sdkmanager — list AVDs, create, launch (optionally cold-boot), stop, wipe and delete. No redirect.
/// </summary>
public sealed partial class EmulatorModule : Page
{
    public EmulatorModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); CheckEngine(); await RefreshAvds(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private string? SelectedAvd => (AvdList.SelectedItem as Avd)?.Name;

    private void Render()
    {
        HeaderTitle.Text = "Android Emulator · Android 模擬器";
        HeaderBlurb.Text = P("Control Android Virtual Devices from the SDK — list, create, launch (cold-boot/wipe), stop and delete AVDs. Needs the Android SDK (emulator + cmdline-tools).",
            "用 Android SDK 控制虛擬裝置 — 列出、建立、啟動（冷開機／清資料）、停止同刪除 AVD。需要 Android SDK（emulator + cmdline-tools）。");
        CreateBtn.Content = P("Create AVD…", "建立 AVD…");
        ColdBootCheck.Content = P("Cold boot (no snapshot)", "冷開機（唔用快照）");
        LaunchBtn.Content = P("Launch", "啟動");
        StopBtn.Content = P("Stop", "停止");
        WipeBtn.Content = P("Wipe data", "清空資料");
        DeleteBtn.Content = P("Delete", "刪除");
    }

    private void CheckEngine()
    {
        var (ok, en, zh) = EmulatorService.Health();
        EngineBar.IsOpen = !ok;
        EngineBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        EngineBar.Title = ok ? P("Android SDK found", "搵到 Android SDK") : P("Android SDK not ready", "Android SDK 未準備好");
        EngineBar.Message = P(en, zh);
    }

    private async Task RefreshAvds()
    {
        Busy.IsActive = true;
        var avds = await EmulatorService.ListAvds();
        Busy.IsActive = false;
        AvdList.ItemsSource = avds;
        if (avds.Count > 0) AvdList.SelectedIndex = 0;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) { CheckEngine(); await RefreshAvds(); }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var images = await EmulatorService.ListSystemImages();
        if (images.Count == 0)
        {
            Notify(InfoBarSeverity.Warning, P("No system images", "冇系統映像"),
                P("Install one first, e.g. sdkmanager \"system-images;android-34;google_apis;x86_64\".",
                  "請先安裝一個，例如 sdkmanager \"system-images;android-34;google_apis;x86_64\"。"));
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

    private void Report(string title, TweakResult r)
        => Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, title,
            (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "");

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        EngineBar.IsOpen = true; EngineBar.Severity = sev; EngineBar.Title = title; EngineBar.Message = msg;
    }
}
