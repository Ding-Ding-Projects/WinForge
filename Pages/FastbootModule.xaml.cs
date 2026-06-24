using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 fastboot 刷機面板（PixelFlasher 式，原生重寫）· In-app fastboot / flasher panel (a native
/// PixelFlasher-style workflow) wrapping fastboot.exe — read bootloader state, unlock/lock, flash boot.img,
/// boot a patched image once, flash a factory zip, and sideload an OTA.
///
/// ⚠ DANGEROUS · 危險：every mutating action is guarded by a dry-run preview (default ON) AND a typed
/// confirmation dialog. No redirect; WinTune drives the real fastboot/adb binaries.
/// </summary>
public sealed partial class FastbootModule : Page
{
    public FastbootModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await CheckEngine(); await RefreshDevices(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private string? Serial => (DeviceBox.SelectedItem as FastbootDevice)?.Serial;
    private bool DryRun => DryRunCheck.IsChecked == true;

    private void Render()
    {
        HeaderTitle.Text = "Fastboot / Flasher · Fastboot／刷機";
        HeaderBlurb.Text = P("A native PixelFlasher-style workflow over fastboot — unlock/lock the bootloader, flash boot.img, test a patched image with 'boot', flash a factory image, or sideload an OTA. Put the phone in bootloader mode first (Reboot → bootloader on the ADB page).",
            "原生 PixelFlasher 式 fastboot 流程 — 解鎖／上鎖 bootloader、flash boot.img、用「boot」試一張 patched image、刷原廠 image，或者 sideload OTA。先將手機入 bootloader 模式（ADB 頁 → 重啟入 bootloader）。");

        DangerBar.Title = P("Flashing can wipe data or brick the device", "刷機可能清空資料或者整壞部機");
        DangerBar.Message = P("These are real fastboot operations. Keep Dry-run on to preview the exact command first. Each action also needs a typed confirmation. Use a matching factory/boot image for YOUR exact model.",
            "呢啲係真實 fastboot 操作。保持開住「試行」可以先睇實際指令。每個動作仲要打字確認。請用啱你型號嘅原廠／boot image。");

        DryRunCheck.Content = P("Dry-run (preview only)", "試行（淨係預覽）");
        StatusBtn.Content = P("Bootloader status", "Bootloader 狀態");
        UnlockBtn.Content = P("Unlock", "解鎖");
        LockBtn.Content = P("Lock", "上鎖");
        FlashBootBtn.Content = P("Flash boot.img…", "Flash boot.img…");
        BootBtn.Content = P("Boot image once…", "暫時 boot 一張 image…");
        FactoryBtn.Content = P("Flash factory zip…", "刷原廠 zip…");
        SideloadBtn.Content = P("Sideload OTA…", "Sideload OTA…");
        RebootBtn.Content = P("Reboot to system", "重啟入系統");
    }

    private async Task CheckEngine()
    {
        bool ok = await FastbootService.IsAvailable();
        EngineBar.IsOpen = !ok;
        if (!ok)
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("fastboot not found", "搵唔到 fastboot");
            EngineBar.Message = P("fastboot ships with Google Platform Tools. Click to install it automatically (no restart).",
                "fastboot 隨 Google Platform Tools 一齊嚟。撳一下自動安裝（唔使重啟）。");
            var btn = new Button { Content = P("Install Platform Tools automatically", "自動安裝 Platform Tools") };
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false;
                btn.Content = P("Installing…", "安裝緊…");
                await PackageService.AutoInstall("Google.PlatformTools");
                await CheckEngine();
                if (await FastbootService.IsAvailable()) await RefreshDevices();
            };
            EngineBar.ActionButton = btn;
        }
        else EngineBar.ActionButton = null;
    }

    private async Task RefreshDevices()
    {
        Busy.IsActive = true;
        var devices = await FastbootService.Devices();
        Busy.IsActive = false;
        DeviceBox.Items.Clear();
        foreach (var d in devices) DeviceBox.Items.Add(d);
        if (DeviceBox.Items.Count > 0) DeviceBox.SelectedIndex = 0;
        else if (!EngineBar.IsOpen)
            Append(P("No fastboot devices. Reboot the phone to bootloader and reconnect.", "冇 fastboot 裝置。將手機重啟入 bootloader 再連接。"));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) { await CheckEngine(); await RefreshDevices(); }

    private async void Status_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        var s = await FastbootService.Summary(Serial ?? "");
        Busy.IsActive = false;
        Append("── " + P("bootloader status", "bootloader 狀態") + " ──\n" + s);
    }

    private async void Unlock_Click(object sender, RoutedEventArgs e)
        => await Guarded(P("Unlock the bootloader", "解鎖 bootloader"),
            P("Unlocking WIPES ALL DATA on the device and lowers its security. Continue?", "解鎖會清空裝置上所有資料並降低保安。繼續？"),
            "UNLOCK", ct => FastbootService.Unlock(Serial ?? "", DryRun, ct));

    private async void Lock_Click(object sender, RoutedEventArgs e)
        => await Guarded(P("Lock the bootloader", "上鎖 bootloader"),
            P("Locking usually WIPES DATA and can brick a device running unofficial firmware. Continue?", "上鎖通常會清空資料；如果裝住非官方韌體可能整壞部機。繼續？"),
            "LOCK", ct => FastbootService.Lock(Serial ?? "", DryRun, ct));

    private async void FlashBoot_Click(object sender, RoutedEventArgs e)
    {
        var img = await PickFile(".img");
        if (img is null) return;
        await Guarded(P("Flash boot partition", "Flash boot 分割區"),
            P($"Flash this image to the 'boot' partition?\n\n{img}", $"將呢張 image flash 入「boot」分割區？\n\n{img}"),
            "FLASH", ct => FastbootService.Flash(Serial ?? "", "boot", img, DryRun, ct));
    }

    private async void BootImg_Click(object sender, RoutedEventArgs e)
    {
        var img = await PickFile(".img");
        if (img is null) return;
        // 'boot' (temporary) is the safe way to test a patched boot image — no typed keyword required, still previews.
        await Guarded(P("Boot image once", "暫時 boot 一張 image"),
            P($"Temporarily boot this image without flashing? Safe way to test a patched boot.img.\n\n{img}", $"唔 flash，暫時 boot 呢張 image？係測試 patched boot.img 嘅安全做法。\n\n{img}"),
            "", ct => FastbootService.BootImage(Serial ?? "", img, DryRun, ct));
    }

    private async void Factory_Click(object sender, RoutedEventArgs e)
    {
        var zip = await PickFile(".zip");
        if (zip is null) return;
        await Guarded(P("Flash factory image", "刷原廠 image"),
            P($"Flash this full factory update package? This reinstalls the OS and may wipe data.\n\n{zip}", $"刷呢個完整原廠更新包？會重裝作業系統，可能清空資料。\n\n{zip}"),
            "FLASH", ct => FastbootService.FlashFactoryZip(Serial ?? "", zip, wipe: false, DryRun, ct));
    }

    private async void Sideload_Click(object sender, RoutedEventArgs e)
    {
        var zip = await PickFile(".zip");
        if (zip is null) return;
        await Guarded(P("Sideload OTA", "Sideload OTA"),
            P($"Sideload this OTA zip via recovery? The phone must be in 'Apply update from ADB' (recovery sideload) mode.\n\n{zip}", $"經 recovery sideload 呢個 OTA zip？手機要喺「Apply update from ADB」（recovery sideload）模式。\n\n{zip}"),
            "SIDELOAD", ct => FastbootService.SideloadOta(Serial ?? "", zip, DryRun, ct));
    }

    private async void Reboot_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        var r = await FastbootService.Reboot(Serial ?? "", DryRun);
        Busy.IsActive = false;
        Report(r);
    }

    /// <summary>Shared guard: confirm dialog (with an optional typed keyword) → run the op → report output.
    /// Dry-run skips the typed keyword but still shows the preview.</summary>
    private async Task Guarded(string title, string body, string keyword, Func<System.Threading.CancellationToken, Task<TweakResult>> op)
    {
        if (string.IsNullOrEmpty(Serial))
        {
            Append(P("Pick a fastboot device first.", "請先揀一部 fastboot 裝置。"));
            return;
        }

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap });
        TextBox? confirmBox = null;
        bool needKeyword = keyword.Length > 0 && !DryRun;
        if (needKeyword)
        {
            panel.Children.Add(new TextBlock { Text = P($"Type {keyword} to confirm:", $"打 {keyword} 確認："), TextWrapping = TextWrapping.Wrap });
            confirmBox = new TextBox { PlaceholderText = keyword };
            panel.Children.Add(confirmBox);
        }
        if (DryRun)
            panel.Children.Add(new TextBlock { Text = P("Dry-run is ON — this only previews the command.", "「試行」開咗 — 淨係預覽指令。"),
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = (DryRun ? P("Preview · ", "預覽 · ") : "⚠ ") + title,
            Content = panel,
            PrimaryButtonText = DryRun ? P("Preview", "預覽") : P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        if (needKeyword && (confirmBox?.Text ?? "").Trim() != keyword)
        {
            Append(P($"Confirmation text did not match '{keyword}'. Aborted.", $"確認文字唔等於「{keyword}」。已取消。"));
            return;
        }

        Busy.IsActive = true;
        var r = await op(System.Threading.CancellationToken.None);
        Busy.IsActive = false;
        Report(r);
    }

    private async Task<string?> PickFile(string ext)
        => await FileDialogs.OpenFileAsync(ext);

    private void Report(TweakResult r)
    {
        var msg = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        Append((r.Success ? "✓ " : "✗ ") + msg + (string.IsNullOrEmpty(r.Output) ? "" : "\n" + r.Output));
    }

    private void Append(string text)
    {
        Console.Text += (Console.Text.Length > 0 ? "\n" : "") + text + "\n";
        Console.Select(Console.Text.Length, 0);
    }
}
