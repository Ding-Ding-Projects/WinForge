using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 燒錄與遊戲工具 · Imaging &amp; game tools. Two tabs: a Raspberry Pi / SD-card imager (raw disk write
/// with a strong drive picker, size guard and heavy confirmation, plus a boot-partition pre-seed for
/// ssh / Wi-Fi / first user), and a Minecraft world downloader that integrates the local GitHub repo
/// (locate/build the jar with a located JDK, then start/stop the headless proxy with live output).
/// All in-app, no redirect. Bilingual.
/// </summary>
public sealed partial class ImagingGameModule : Page
{
    private List<PhysicalDisk> _disks = new();
    private bool _busy;
    private CancellationTokenSource? _writeCts;

    public ImagingGameModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += async (_, _) =>
        {
            Render();
            await RefreshDisks();
            await RefreshUsbDisks();
            await RefreshBoot();
            RefreshMcEngine();
            RefreshMcRunState();
        };
        Unloaded += (_, _) => { try { _writeCts?.Cancel(); } catch { } Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private static string Msg(TweakResult r) => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Render()
    {
        Header.Title = "Imaging & game tools · 燒錄與遊戲工具";
        HeaderBlurb.Text = P("Write an OS image to an SD card (Raspberry Pi style) with strong guards, and run the Minecraft world downloader.",
            "用強力保護將 OS 映像燒落 SD 卡（樹莓派式），同埋執行 Minecraft 世界下載器。");

        PiTab.Header = P("Raspberry Pi Imager", "樹莓派燒錄");
        UsbTab.Header = P("USB imager (Rufus)", "USB 燒錄（Rufus）");
        McTab.Header = P("Minecraft world downloader", "Minecraft 世界下載");

        // ── Pi tab ──
        PiDangerBar.Title = P("DANGER — raw disk write", "危險 — 原始磁碟寫入");
        PiDangerBar.Message = P("Writing an image ERASES the entire selected disk. Double-check you picked the SD card and not a real drive. The system disk is never offered.",
            "燒錄會抹掉成個所選磁碟。請再三確認你揀咗 SD 卡而唔係真正硬碟。系統磁碟永遠唔會出現喺清單。");

        PiStep1.Text = P("1. Choose the OS image (.img / .iso)", "1. 揀 OS 映像（.img／.iso）");
        PickImageBtn.Content = P("Choose image…", "揀映像…");
        PiStep2.Text = P("2. Choose the target SD card", "2. 揀目標 SD 卡");
        RefreshDisksBtn.Content = P("Refresh", "重新整理");
        ShowAllDisksChk.Content = P("Show all disks (including fixed) — advanced", "顯示全部磁碟（包括固定碟）— 進階");
        WriteBtn.Content = P("Write image…", "燒錄映像…");

        PiStep3.Text = P("3. Pre-seed Pi boot config (after flashing)", "3. 預設樹莓派啟動設定（燒完之後）");
        PiSeedBlurb.Text = P("After flashing, the FAT boot partition appears as a drive letter. Pick it to enable SSH, set up Wi-Fi, and create the first user.",
            "燒完之後，FAT boot 分割區會以一個磁碟機代號出現。揀佢去開啟 SSH、設定 Wi-Fi 同建立第一個使用者。");
        RefreshBootBtn.Content = P("Refresh", "重新整理");
        EnableSshChk.Content = P("Enable SSH (create empty 'ssh' file)", "開啟 SSH（建立空白 'ssh' 檔）");
        WifiSsidLbl.Text = P("Wi-Fi SSID", "Wi-Fi 名稱");
        WifiPwLbl.Text = P("Wi-Fi password", "Wi-Fi 密碼");
        WifiCountryLbl.Text = P("Wi-Fi country", "Wi-Fi 國家");
        UserLbl.Text = P("First user", "第一個使用者");
        UserPwLbl.Text = P("User password", "使用者密碼");
        SeedBtn.Content = P("Write boot config", "寫入啟動設定");

        // ── USB imager (Rufus) tab ──
        UsbDangerBar.Title = P("DANGER — raw disk write", "危險 — 原始磁碟寫入");
        UsbDangerBar.Message = P("Writing an image ERASES the entire selected USB drive. Only removable USB drives are listed; the system/boot disk is never offered. You must type the disk number to confirm.",
            "燒錄會抹掉成個所選 USB 手指。清單只列出可移除嘅 USB 手指；系統／開機磁碟永遠唔會出現。你要輸入磁碟編號先可以確認。");

        UsbStep1.Text = P("1. Choose an ISO / IMG to flash (.iso / .img / .bin)", "1. 揀要燒嘅 ISO／IMG（.iso／.img／.bin）");
        UsbPickImageBtn.Content = P("Choose image…", "揀映像…");
        UsbStep2.Text = P("2. Choose the target USB drive", "2. 揀目標 USB 手指");
        UsbRefreshDisksBtn.Content = P("Refresh", "重新整理");
        UsbShowAllDisksChk.Content = P("Show all disks (including fixed) — advanced & risky", "顯示全部磁碟（包括固定碟）— 進階兼危險");
        UsbStep3.Text = P("3. Write (and verify) the image", "3. 燒錄（並校驗）映像");
        UsbVerifyChk.Content = P("Verify after writing (read back & compare, computes SHA-256)", "燒完之後校驗（讀回比對，計 SHA-256）");
        UsbModeHint.Text = P("DD-mode raw write: the bytes of the file are written 1:1 to the drive — works for already-bootable ISO/IMG images (most Linux & Raspberry Pi images). For Windows install media, persistence, or ISO→USB filesystem builds, use 'Launch Rufus' below.",
            "DD 模式原始寫入：檔案嘅位元組原封不動 1:1 寫入手指 — 適用於已可開機嘅 ISO／IMG（大部分 Linux 同樹莓派映像）。如果要 Windows 安裝媒體、持久化分割區，或者 ISO→USB 檔案系統建置，請用下面嘅「啟動 Rufus」。");
        UsbWriteBtn.Content = P("Write image…", "燒錄映像…");
        UsbCancelBtn.Content = P("Cancel", "取消");

        UsbAdvancedLbl.Text = P("Advanced boot options — Rufus", "進階開機選項 — Rufus");
        UsbAdvancedBlurb.Text = P("For UEFI/MBR partition scheme & target system selection, FAT32/NTFS bootloaders, Windows install.wim splitting, Windows-To-Go and persistence, launch Rufus. Rufus is a GUI tool with no automation flags, so it opens without preselecting your drive/ISO — pick them inside Rufus.",
            "如要 UEFI／MBR 分割區配置同目標系統選擇、FAT32／NTFS 啟動載入器、Windows install.wim 分割、Windows-To-Go 同持久化，請啟動 Rufus。Rufus 係 GUI 工具冇自動化參數，所以開啟時唔會預先揀好磁碟／ISO — 請喺 Rufus 入面自己揀。");
        UsbLaunchRufusBtn.Content = P("Launch Rufus", "啟動 Rufus");
        RefreshRufusEngine();

        // ── Minecraft tab ──
        McEngineLbl.Text = P("Engine — repo + JDK", "引擎 — repo + JDK");
        LocateRepoBtn.Content = P("Locate repo…", "指定 repo…");
        BuildJarBtn.Content = P("Build jar", "建置 jar");
        InstallJdkBtn.Content = P("Install JDK", "安裝 JDK");
        McRunLbl.Text = P("Run the downloader (proxy)", "執行下載器（代理）");
        McServerLbl.Text = P("Server", "伺服器");
        McPortLbl.Text = P("Local port", "本機 port");
        McOutLbl.Text = P("World output", "世界輸出");
        McOutPickBtn.Content = P("Choose…", "揀…");
        McOutOpenBtn.Content = P("Open folder", "開資料夾");
        McRenderLbl.Text = P("Extended render", "延伸視距");
        McAutoOpenChk.Content = P("Auto-open containers (experimental)", "自動開啟容器（實驗性）");
        McStartBtn.Content = P("Start", "開始");
        McStopBtn.Content = P("Stop", "停止");
        McLogLbl.Text = P("Live output — connect Minecraft to localhost:<port>", "即時輸出 — 用 Minecraft 連去 localhost:<port>");

        UpdateImageSizeText();
    }

    // ════════════════════ Raspberry Pi Imager ════════════════════

    private async void PickImage_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".img", ".iso", ".bin", ".raw", ".wic");
        if (path is null) return;
        ImagePathBox.Text = path;
        UpdateImageSizeText();
    }

    private void UpdateImageSizeText()
    {
        var path = ImagePathBox.Text;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            var size = new FileInfo(path).Length;
            ImageSizeText.Text = $"{P("Size", "大細")}: {ImagingService.HumanSize(size)}";
        }
        else ImageSizeText.Text = "";
    }

    private async void RefreshDisks_Click(object sender, RoutedEventArgs e) => await RefreshDisks();
    private async void ShowAllDisks_Click(object sender, RoutedEventArgs e) => await RefreshDisks();

    private async Task RefreshDisks()
    {
        _disks = await ImagingService.ListDisks();
        bool showAll = ShowAllDisksChk.IsChecked == true;
        var shown = showAll ? _disks : _disks.Where(d => d.LooksSafeTarget).ToList();

        DiskBox.Items.Clear();
        foreach (var d in shown)
            DiskBox.Items.Add(new ComboBoxItem { Content = d.Display, Tag = d });

        if (DiskBox.Items.Count > 0) DiskBox.SelectedIndex = 0;

        DiskWarnText.Text = shown.Count == 0
            ? P("No removable disks found. Insert an SD card / USB stick and Refresh. (Tick 'Show all disks' only if you know what you're doing.)",
                "搵唔到可移除磁碟。請插入 SD 卡／USB 手指再重新整理。（除非你好清楚自己做緊乜，否則唔好剔「顯示全部磁碟」。）")
            : (showAll
                ? P("All disks shown. Disks marked ⚠SYSTEM cannot be written to.", "已顯示全部磁碟。標咗 ⚠SYSTEM 嘅磁碟唔可以寫入。")
                : "");
    }

    private PhysicalDisk? SelectedDisk => (DiskBox.SelectedItem as ComboBoxItem)?.Tag as PhysicalDisk;

    private async void Write_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var imagePath = ImagePathBox.Text;
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            PiNotify(InfoBarSeverity.Warning, P("Pick an image first", "請先揀映像"), "");
            return;
        }
        var disk = SelectedDisk;
        if (disk is null)
        {
            PiNotify(InfoBarSeverity.Warning, P("Pick a target disk first", "請先揀目標磁碟"), "");
            return;
        }
        if (disk.IsSystem || disk.IsBoot)
        {
            PiNotify(InfoBarSeverity.Error, P("Refused", "拒絕"), P("That is the system/boot disk.", "嗰個係系統／開機磁碟。"));
            return;
        }
        if (!AdminHelper.IsElevated)
        {
            await ShowAdminDialog();
            return;
        }

        var imageSize = new FileInfo(imagePath).Length;
        if (imageSize > disk.Size)
        {
            PiNotify(InfoBarSeverity.Error, P("Image too big", "映像太大"),
                $"{ImagingService.HumanSize(imageSize)} > {ImagingService.HumanSize(disk.Size)}");
            return;
        }

        // ── Heavy confirmation: must type the disk number ──
        if (!await ConfirmWrite(disk, imagePath, imageSize)) return;

        _busy = true;
        _writeCts = new CancellationTokenSource();
        WriteBtn.IsEnabled = false;
        WriteProgress.Visibility = Visibility.Visible;
        WriteProgress.Value = 0;
        WriteProgressText.Text = P("Writing…", "燒錄緊…");

        var r = await ImagingService.WriteImage(disk, imagePath, (written, total) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                double pct = total > 0 ? written * 100.0 / total : 0;
                WriteProgress.Value = pct;
                WriteProgressText.Text = $"{ImagingService.HumanSize(written)} / {ImagingService.HumanSize(total)} ({pct:0.0}%)";
            });
        }, _writeCts.Token);

        WriteBtn.IsEnabled = true;
        WriteProgress.Visibility = Visibility.Collapsed;
        _busy = false;
        PiNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Write complete", "燒錄完成") : P("Write failed", "燒錄失敗"), Msg(r));
        if (r.Success)
            WriteProgressText.Text = P("Done. Re-insert the card to pre-seed the boot config below.", "完成。重新插卡就可以喺下面預設啟動設定。");
        await RefreshBoot();
    }

    private async Task<bool> ConfirmWrite(PhysicalDisk disk, string imagePath, long imageSize)
    {
        var input = new TextBox { PlaceholderText = disk.Number.ToString() };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = P($"This will PERMANENTLY ERASE Disk {disk.Number} ({disk.Model}, {disk.HumanSize}) and write {Path.GetFileName(imagePath)} ({ImagingService.HumanSize(imageSize)}).",
                $"呢個動作會永久抹掉 磁碟 {disk.Number}（{disk.Model}，{disk.HumanSize}）並寫入 {Path.GetFileName(imagePath)}（{ImagingService.HumanSize(imageSize)}）。"),
        });
        if (disk.Letters.Count > 0)
            panel.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                Text = P($"Drive(s) {string.Join(", ", disk.Letters)} on this disk will be dismounted and lost.",
                    $"呢個磁碟上面嘅 {string.Join("、", disk.Letters)} 會被卸載並遺失。"),
            });
        panel.Children.Add(new TextBlock
        {
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Text = P($"Type the disk number ({disk.Number}) to confirm:", $"輸入磁碟編號（{disk.Number}）以確認："),
        });
        panel.Children.Add(input);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Confirm raw disk write", "確認原始磁碟寫入"),
            Content = panel,
            PrimaryButtonText = P("Erase and write", "抹掉並燒錄"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        bool ok = false;
        input.TextChanged += (_, _) => dlg.IsPrimaryButtonEnabled = input.Text.Trim() == disk.Number.ToString();
        dlg.IsPrimaryButtonEnabled = false;
        dlg.PrimaryButtonClick += (_, _) => ok = input.Text.Trim() == disk.Number.ToString();
        await dlg.ShowAsync();
        return ok;
    }

    private async Task ShowAdminDialog()
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Administrator required", "需要管理員權限"),
            Content = P("Writing to a raw disk needs administrator rights. Relaunch WinForge as administrator?",
                "原始寫入磁碟需要管理員權限。要唔要以管理員身分重新啟動 WinForge？"),
            PrimaryButtonText = P("Relaunch as admin", "以管理員重新啟動"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            if (AdminHelper.RelaunchElevated()) Application.Current.Exit();
        }
    }

    private async void RefreshBoot_Click(object sender, RoutedEventArgs e) => await RefreshBoot();

    private async Task RefreshBoot()
    {
        await Task.Yield();
        BootDriveBox.Items.Clear();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady) continue;
                // boot partitions are small FAT/FAT32 removable volumes
                bool fat = d.DriveFormat.StartsWith("FAT", StringComparison.OrdinalIgnoreCase);
                if (d.DriveType == DriveType.Removable || fat)
                    BootDriveBox.Items.Add(new ComboBoxItem { Content = $"{d.Name}  {d.VolumeLabel} ({d.DriveFormat})", Tag = d.Name });
            }
            catch { }
        }
        if (BootDriveBox.Items.Count > 0) BootDriveBox.SelectedIndex = 0;
    }

    private void Seed_Click(object sender, RoutedEventArgs e)
    {
        var letter = (BootDriveBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if (string.IsNullOrWhiteSpace(letter))
        {
            PiNotify(InfoBarSeverity.Warning, P("Pick the boot drive first", "請先揀 boot 磁碟機"), "");
            return;
        }
        var r = ImagingService.SeedBootConfig(
            letter,
            EnableSshChk.IsChecked == true,
            WifiSsidBox.Text, WifiPwBox.Password, WifiCountryBox.Text,
            UserBox.Text, UserPwBox.Password);
        PiNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Boot config written", "已寫入啟動設定") : P("Failed", "失敗"), Msg(r));
    }

    private void PiNotify(InfoBarSeverity sev, string title, string msg)
    {
        PiResultBar.Severity = sev; PiResultBar.Title = title; PiResultBar.Message = msg; PiResultBar.IsOpen = true;
    }

    // ════════════════════ USB imager (Rufus) ════════════════════

    private List<PhysicalDisk> _usbDisks = new();
    private CancellationTokenSource? _usbCts;

    private void RefreshRufusEngine()
    {
        bool installed = RufusService.IsInstalled();
        UsbLaunchRufusBtn.IsEnabled = installed;

        if (installed)
        {
            RufusEngineBar.Severity = InfoBarSeverity.Success;
            RufusEngineBar.Title = P("Rufus is installed", "已安裝 Rufus");
            RufusEngineBar.Message = P("The native write/verify below needs no binary. Use 'Launch Rufus' for advanced boot options.",
                "下面嘅原生燒錄／校驗唔需要任何程式。要進階開機選項就撳「啟動 Rufus」。");
            RufusEngineBar.ActionButton = null;
        }
        else
        {
            RufusEngineBar.Severity = InfoBarSeverity.Informational;
            RufusEngineBar.Title = P("Rufus not installed (optional)", "未安裝 Rufus（選用）");
            RufusEngineBar.Message = P("Native write/verify works without it. Install Rufus only for advanced bootable-media options.",
                "原生燒錄／校驗唔使佢都用得。淨係要進階可開機媒體選項先需要安裝 Rufus。");
            RufusEngineBar.ActionButton = EngineBars.AutoInstallButton(
                RufusService.WingetId, "Install Rufus", "安裝 Rufus",
                async () => { await Task.Yield(); RefreshRufusEngine(); },
                () => PackageService.RefreshProcessPath());
        }
    }

    private async void UsbPickImage_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".iso", ".img", ".bin", ".raw");
        if (path is null) return;
        UsbImagePathBox.Text = path;
        UpdateUsbImageSizeText();
    }

    private void UpdateUsbImageSizeText()
    {
        var path = UsbImagePathBox.Text;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            UsbImageSizeText.Text = $"{P("Size", "大細")}: {ImagingService.HumanSize(new FileInfo(path).Length)}";
        else UsbImageSizeText.Text = "";
    }

    private async void UsbRefreshDisks_Click(object sender, RoutedEventArgs e) => await RefreshUsbDisks();
    private async void UsbShowAllDisks_Click(object sender, RoutedEventArgs e) => await RefreshUsbDisks();

    private async Task RefreshUsbDisks()
    {
        _usbDisks = await ImagingService.ListDisks();
        bool showAll = UsbShowAllDisksChk.IsChecked == true;
        var shown = showAll ? _usbDisks : _usbDisks.Where(d => d.LooksSafeTarget).ToList();

        UsbDiskBox.Items.Clear();
        foreach (var d in shown)
            UsbDiskBox.Items.Add(new ComboBoxItem { Content = d.Display, Tag = d });
        if (UsbDiskBox.Items.Count > 0) UsbDiskBox.SelectedIndex = 0;

        UsbDiskWarnText.Text = shown.Count == 0
            ? P("No removable USB drives found. Insert a USB stick and Refresh. (Tick 'Show all disks' only if you know what you're doing.)",
                "搵唔到可移除 USB 手指。請插入 USB 手指再重新整理。（除非你好清楚自己做緊乜，否則唔好剔「顯示全部磁碟」。）")
            : (showAll
                ? P("All disks shown. Disks marked ⚠SYSTEM cannot be written to.", "已顯示全部磁碟。標咗 ⚠SYSTEM 嘅磁碟唔可以寫入。")
                : "");
    }

    private PhysicalDisk? SelectedUsbDisk => (UsbDiskBox.SelectedItem as ComboBoxItem)?.Tag as PhysicalDisk;

    private async void UsbWrite_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var imagePath = UsbImagePathBox.Text;
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            UsbNotify(InfoBarSeverity.Warning, P("Pick an image first", "請先揀映像"), "");
            return;
        }
        var disk = SelectedUsbDisk;
        if (disk is null)
        {
            UsbNotify(InfoBarSeverity.Warning, P("Pick a target USB drive first", "請先揀目標 USB 手指"), "");
            return;
        }
        if (disk.IsSystem || disk.IsBoot)
        {
            UsbNotify(InfoBarSeverity.Error, P("Refused", "拒絕"), P("That is the system/boot disk.", "嗰個係系統／開機磁碟。"));
            return;
        }
        if (!AdminHelper.IsElevated)
        {
            await ShowAdminDialog();
            return;
        }

        var imageSize = new FileInfo(imagePath).Length;
        if (imageSize > disk.Size)
        {
            UsbNotify(InfoBarSeverity.Error, P("Image too big", "映像太大"),
                $"{ImagingService.HumanSize(imageSize)} > {ImagingService.HumanSize(disk.Size)}");
            return;
        }

        if (!await ConfirmWrite(disk, imagePath, imageSize)) return;

        bool doVerify = UsbVerifyChk.IsChecked == true;
        _busy = true;
        _usbCts = new CancellationTokenSource();
        UsbWriteBtn.IsEnabled = false;
        UsbCancelBtn.IsEnabled = true;
        UsbProgress.Visibility = Visibility.Visible;
        UsbProgress.Value = 0;
        UsbProgressText.Text = P("Writing…", "燒錄緊…");

        var wr = await ImagingService.WriteImage(disk, imagePath, (written, total) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                double pct = total > 0 ? written * 100.0 / total : 0;
                UsbProgress.Value = pct;
                UsbProgressText.Text = $"{P("Writing", "燒錄")}: {ImagingService.HumanSize(written)} / {ImagingService.HumanSize(total)} ({pct:0.0}%)";
            });
        }, _usbCts.Token);

        if (!wr.Success)
        {
            FinishUsb();
            UsbNotify(InfoBarSeverity.Error, P("Write failed", "燒錄失敗"), Msg(wr));
            return;
        }

        if (doVerify && !_usbCts.IsCancellationRequested)
        {
            UsbProgress.Value = 0;
            UsbProgressText.Text = P("Verifying…", "校驗緊…");
            var vr = await ImagingService.VerifyImage(disk, imagePath, (read, total) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    double pct = total > 0 ? read * 100.0 / total : 0;
                    UsbProgress.Value = pct;
                    UsbProgressText.Text = $"{P("Verifying", "校驗")}: {ImagingService.HumanSize(read)} / {ImagingService.HumanSize(total)} ({pct:0.0}%)";
                });
            }, _usbCts.Token);

            FinishUsb();
            UsbNotify(vr.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                vr.Success ? P("Write + verify complete", "燒錄＋校驗完成") : P("Verify failed", "校驗失敗"), Msg(vr));
            UsbProgressText.Text = vr.Success
                ? P("Done — the USB drive matches the image. You can safely remove it.", "完成 — USB 手指同映像一致。你可以安全移除佢。")
                : Msg(vr);
        }
        else
        {
            FinishUsb();
            UsbNotify(InfoBarSeverity.Success, P("Write complete", "燒錄完成"), Msg(wr));
            UsbProgressText.Text = P("Done (no verify). You can safely remove the USB drive.", "完成（冇校驗）。你可以安全移除 USB 手指。");
        }
    }

    private void FinishUsb()
    {
        _busy = false;
        UsbWriteBtn.IsEnabled = true;
        UsbCancelBtn.IsEnabled = false;
        UsbProgress.Visibility = Visibility.Collapsed;
    }

    private void UsbCancel_Click(object sender, RoutedEventArgs e) => _usbCts?.Cancel();

    private async void UsbLaunchRufus_Click(object sender, RoutedEventArgs e)
    {
        var r = await RufusService.Launch();
        UsbNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Rufus launched", "已啟動 Rufus") : P("Could not launch Rufus", "啟動唔到 Rufus"), Msg(r));
    }

    private void UsbNotify(InfoBarSeverity sev, string title, string msg)
    {
        UsbResultBar.Severity = sev; UsbResultBar.Title = title; UsbResultBar.Message = msg; UsbResultBar.IsOpen = true;
    }

    // ════════════════════ Minecraft world downloader ════════════════════

    private string? _repo;
    private string? _jar;

    private void RefreshMcEngine()
    {
        _repo = MinecraftService.FindRepo();
        _jar = _repo is not null ? MinecraftService.FindJar(_repo) : null;
        var java = MinecraftService.FindJava();

        if (_repo is null)
        {
            McRepoBar.Severity = InfoBarSeverity.Warning;
            McRepoBar.Title = P("Repo not found", "搵唔到 repo");
            McRepoBar.Message = P($"This module needs the minecraft-world-downloader repo. Expected at: {MinecraftService.ExpectedRepoPath}. Clone it there, or click 'Locate repo…' to point to it.",
                $"呢個模組需要 minecraft-world-downloader repo。預期位置：{MinecraftService.ExpectedRepoPath}。請 clone 去嗰度，或者撳「指定 repo…」。");
            McRepoBar.IsOpen = true;
        }
        else McRepoBar.IsOpen = false;

        var parts = new List<string>();
        parts.Add(_repo is null ? P("Repo: not found", "Repo：搵唔到") : $"{P("Repo", "Repo")}: {_repo}");
        parts.Add(_jar is null ? P("Jar: not built", "Jar：未建置") : $"{P("Jar", "Jar")}: {Path.GetFileName(_jar)}");
        parts.Add(java is null ? P("Java: not found", "Java：搵唔到") : $"Java: {java}");
        if (MinecraftService.HasMaven()) parts.Add("Maven: ok");
        McEngineStatus.Text = string.Join("\n", parts);

        BuildJarBtn.IsEnabled = _repo is not null;
        InstallJdkBtn.IsEnabled = java is null;
        McStartBtn.IsEnabled = _jar is not null && java is not null && !MinecraftService.IsRunning;

        if (string.IsNullOrWhiteSpace(McOutBox.Text) && _jar is not null)
            McOutBox.Text = Path.Combine(Path.GetDirectoryName(_jar)!, "world");
    }

    private async void LocateRepo_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync();
        if (folder is null) return;
        if (!File.Exists(Path.Combine(folder, "pom.xml")))
        {
            McNotify(InfoBarSeverity.Error, P("Not the repo", "唔係嗰個 repo"),
                P("That folder has no pom.xml. Pick the minecraft-world-downloader folder.", "嗰個資料夾冇 pom.xml。請揀 minecraft-world-downloader 資料夾。"));
            return;
        }
        MinecraftService.SetRepo(folder);
        RefreshMcEngine();
        McNotify(InfoBarSeverity.Success, P("Repo set", "已設定 repo"), folder);
    }

    private async void BuildJar_Click(object sender, RoutedEventArgs e)
    {
        if (_repo is null || _busy) return;
        _busy = true;
        BuildJarBtn.IsEnabled = false;
        McRunning.IsActive = true;
        AppendLog(P("Building jar with Maven… (this can take a few minutes)", "用 Maven 建置 jar 緊…（可能要幾分鐘）"));
        var (ok, jar, log) = await MinecraftService.BuildJar(_repo);
        McRunning.IsActive = false;
        _busy = false;
        if (!string.IsNullOrEmpty(log)) AppendLog(log);
        if (ok)
        {
            _jar = jar;
            McNotify(InfoBarSeverity.Success, P("Build succeeded", "建置成功"), jar);
        }
        else
        {
            McNotify(InfoBarSeverity.Error, P("Build failed", "建置失敗"),
                P("See the log. You can also drop a prebuilt world-downloader.jar into the repo's target folder.",
                  "請睇記錄。你都可以將建置好嘅 world-downloader.jar 放入 repo 嘅 target 資料夾。"));
        }
        RefreshMcEngine();
    }

    private async void InstallJdk_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        InstallJdkBtn.IsEnabled = false;
        InstallJdkBtn.Content = P("Installing…", "安裝緊…");
        var ok = await MinecraftService.AutoInstallJdk();
        InstallJdkBtn.Content = P("Install JDK", "安裝 JDK");
        _busy = false;
        McNotify(ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
            ok ? P("JDK installed", "已安裝 JDK") : P("Install incomplete", "未完成安裝"),
            ok ? "" : P("Could not auto-install. Install a JDK 21+ manually.", "自動安裝唔到。請手動安裝 JDK 21+。"));
        RefreshMcEngine();
    }

    private async void McPickOut_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync();
        if (folder is not null) McOutBox.Text = folder;
    }

    private void McOpenOut_Click(object sender, RoutedEventArgs e)
    {
        var dir = McOutBox.Text;
        if (string.IsNullOrWhiteSpace(dir)) return;
        try { Directory.CreateDirectory(dir); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = dir, UseShellExecute = true }); }
        catch { }
    }

    private void McStart_Click(object sender, RoutedEventArgs e)
    {
        if (_jar is null) { McNotify(InfoBarSeverity.Warning, P("Build the jar first", "請先建置 jar"), ""); return; }
        var opt = new MinecraftService.RunOptions
        {
            Server = McServerBox.Text,
            LocalPort = (int)McPortBox.Value,
            OutputDir = McOutBox.Text,
            ExtendedRenderDistance = (int)McRenderBox.Value,
            AutoOpenContainers = McAutoOpenChk.IsChecked == true,
        };
        var r = MinecraftService.Start(_jar, opt,
            line => DispatcherQueue.TryEnqueue(() => AppendLog(line)),
            () => DispatcherQueue.TryEnqueue(() => { AppendLog(P("[downloader stopped]", "[下載器已停止]")); RefreshMcRunState(); }));
        McNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Started", "已開始") : P("Failed to start", "啟動失敗"), Msg(r));
        RefreshMcRunState();
    }

    private void McStop_Click(object sender, RoutedEventArgs e)
    {
        var r = MinecraftService.Stop();
        McNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning, P("Stop", "停止"), Msg(r));
        RefreshMcRunState();
    }

    private void RefreshMcRunState()
    {
        bool running = MinecraftService.IsRunning;
        McStartBtn.IsEnabled = !running && _jar is not null && MinecraftService.FindJava() is not null;
        McStopBtn.IsEnabled = running;
        McRunning.IsActive = running;
    }

    private void AppendLog(string line)
    {
        // keep the log bounded
        if (McLog.Text.Length > 60000) McLog.Text = McLog.Text.Substring(McLog.Text.Length - 40000);
        McLog.Text += (McLog.Text.Length == 0 ? "" : "\n") + line;
        McLog.Select(McLog.Text.Length, 0);
    }

    private void McNotify(InfoBarSeverity sev, string title, string msg)
    {
        McResultBar.Severity = sev; McResultBar.Title = title; McResultBar.Message = msg; McResultBar.IsOpen = true;
    }
}
