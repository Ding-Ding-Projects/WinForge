using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 Android ADB 主控台 · In-app Android ADB console (wraps adb) — devices, install APK, shell,
/// logcat, screenshot, reboot, wireless connect, a file push/pull browser, APK backup, a live streaming
/// logcat, and scrcpy screen mirroring. No redirect. Bilingual.
/// </summary>
public sealed partial class AndroidAdbModule : Page
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _ui;
    private string _cwd = "/sdcard";

    public AndroidAdbModule()
    {
        InitializeComponent();
        _ui = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await CheckEngine(); await RefreshDevices(); };
        Unloaded += (_, _) => { AdbService.StopLogcatStream(); ScrcpyService.Stop(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private string? SelectedSerial => (DeviceBox.SelectedItem as AdbDevice)?.Serial;

    private void Render()
    {
        HeaderTitle.Text = "Android (ADB) · Android（ADB）";
        HeaderBlurb.Text = P("Manage Android devices over adb — console, a file push/pull browser, APK backup, live logcat, and screen mirroring (scrcpy). Enable USB debugging on the phone first.",
            "用 adb 管理 Android 裝置 — 主控台、檔案推送／拉取瀏覽器、APK 備份、即時 logcat，同螢幕鏡像（scrcpy）。手機要先開 USB 偵錯。");
        ConnectBtn.Content = P("Connect", "連接");

        ConsoleTab.Header = P("Console", "主控台");
        FilesTab.Header = P("Files", "檔案");
        ApkTab.Header = P("APK backup", "APK 備份");
        LiveTab.Header = P("Live logcat", "即時 logcat");
        MirrorTab.Header = P("Screen mirror", "螢幕鏡像");

        InstallBtn.Content = P("Install APK", "安裝 APK");
        ShotBtn.Content = P("Screenshot", "截圖");
        LogcatBtn.Content = P("Logcat", "Logcat");
        PackagesBtn.Content = P("Packages", "已裝套件");
        RebootBtn.Content = P("Reboot", "重啟");
        RebootSystem.Text = P("Reboot to system", "重啟入系統");
        RebootBootloader.Text = P("Reboot to bootloader", "重啟入 bootloader");
        RebootRecovery.Text = P("Reboot to recovery", "重啟入 recovery");
        ShellRunBtn.Content = P("Run", "執行");

        // Files tab
        FilesGoBtn.Content = P("Go", "前往");
        PushBtn.Content = P("Push file →", "推送檔案 →");
        PullBtn.Content = P("← Pull selected", "← 拉取所選");
        FileDeleteBtn.Content = P("Delete", "刪除");
        if (string.IsNullOrEmpty(PathBox.Text)) PathBox.Text = _cwd;

        // APK tab
        ApkLoadBtn.Content = P("List installed apps", "列出已裝程式");
        ApkSystemCheck.Content = P("include system apps", "包埋系統程式");
        ApkBackupBtn.Content = P("Back up selected APK", "備份所選 APK");

        // Live logcat tab
        if (LogLevelBox.Items.Count == 0)
        {
            foreach (var lvl in new[] { "Verbose *:V", "Debug *:D", "Info *:I", "Warn *:W", "Error *:E" })
                LogLevelBox.Items.Add(lvl);
            LogLevelBox.SelectedIndex = 2;
        }
        LiveStartBtn.Content = P("Start", "開始");
        LiveStopBtn.Content = P("Stop", "停止");
        LiveClearBtn.Content = P("Clear", "清除");

        // Mirror tab
        MaxSizeCap.Text = P("Resolution cap", "解像度上限");
        BitrateCap.Text = P("Bitrate", "位元率");
        if (MaxSizeBox.Items.Count == 0)
        {
            MaxSizeBox.Items.Add(P("Native", "原生"));
            foreach (var s in new[] { "1920", "1280", "1024", "800" }) MaxSizeBox.Items.Add(s + " px");
            MaxSizeBox.SelectedIndex = 0;
        }
        if (BitrateBox.Items.Count == 0)
        {
            foreach (var b in new[] { "2", "4", "8", "12", "16" }) BitrateBox.Items.Add(b + " Mbps");
            BitrateBox.SelectedIndex = 2;
        }
        StayAwakeCheck.Content = P("Keep device awake", "保持裝置喚醒");
        ScreenOffCheck.Content = P("Turn phone screen off while mirroring", "鏡像時熄手機屏幕");
        ShowTouchesCheck.Content = P("Show touches", "顯示觸控點");
        MirrorStartBtn.Content = P("Start mirroring", "開始鏡像");
        MirrorRecordBtn.Content = P("Record to file…", "錄影到檔案…");
        MirrorStopBtn.Content = P("Stop", "停止");
        SyncMirrorButtons();
    }

    private async Task CheckEngine()
    {
        bool ok = await AdbService.IsAvailable();
        EngineBar.IsOpen = !ok;
        if (!ok)
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("adb not found", "搵唔到 adb");
            EngineBar.Message = P("Click to install it automatically (Google Platform Tools via winget) — no restart needed.",
                "撳一下自動安裝（用 winget 裝 Google Platform Tools）— 唔使重啟。");
            var btn = new Button { Content = P("Install adb automatically", "自動安裝 adb") };
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false;
                btn.Content = P("Installing…", "安裝緊…");
                await PackageService.AutoInstall("Google.PlatformTools");
                await CheckEngine();
                if (await AdbService.IsAvailable()) await RefreshDevices();
            };
            EngineBar.ActionButton = btn;
        }
        else EngineBar.ActionButton = null;
        await CheckScrcpy();
    }

    private async Task CheckScrcpy()
    {
        bool ok = await ScrcpyService.IsAvailable();
        ScrcpyBar.IsOpen = !ok;
        if (!ok)
        {
            ScrcpyBar.Severity = InfoBarSeverity.Warning;
            ScrcpyBar.Title = P("scrcpy not found", "搵唔到 scrcpy");
            ScrcpyBar.Message = P("Screen mirroring needs scrcpy. Click to install it automatically (Genymobile.scrcpy via winget).",
                "螢幕鏡像需要 scrcpy。撳一下自動安裝（用 winget 裝 Genymobile.scrcpy）。");
            var btn = new Button { Content = P("Install scrcpy automatically", "自動安裝 scrcpy") };
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false;
                btn.Content = P("Installing…", "安裝緊…");
                await PackageService.AutoInstall(ScrcpyService.WingetId);
                await CheckScrcpy();
            };
            ScrcpyBar.ActionButton = btn;
        }
        else ScrcpyBar.ActionButton = null;
    }

    private async Task RefreshDevices()
    {
        Busy.IsActive = true;
        var devices = await AdbService.Devices();
        Busy.IsActive = false;
        DeviceBox.Items.Clear();
        foreach (var d in devices) DeviceBox.Items.Add(d);
        if (DeviceBox.Items.Count > 0) DeviceBox.SelectedIndex = 0;
        if (devices.Count == 0 && !EngineBar.IsOpen)
            Notify(InfoBarSeverity.Informational, P("No devices", "冇裝置"), P("Plug in a phone with USB debugging on, or connect wirelessly.", "插一部開咗 USB 偵錯嘅手機，或者無線連接。"));
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) { await CheckEngine(); await RefreshDevices(); }

    private void DeviceBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { /* serial used on demand */ }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var ip = (IpBox.Text ?? "").Trim();
        if (ip.Length == 0) return;
        Busy.IsActive = true;
        var r = await AdbService.Connect(ip);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning, P("adb connect", "adb 連接"), Msg(r));
        await RefreshDevices();
    }

    // ── Console tab ─────────────────────────────────────────────────────────────────

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        var path = await FileDialogs.OpenFileAsync(".apk");
        if (path is null) return;
        Busy.IsActive = true;
        var r = await AdbService.Install(serial, path);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Install APK", "安裝 APK"), Msg(r));
    }

    private async void Shot_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        Busy.IsActive = true;
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), $"adb-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        var r = await AdbService.Screenshot(serial, local);
        Busy.IsActive = false;
        if (r.Success && File.Exists(local))
        {
            ShotImage.Source = new BitmapImage(new Uri(local));
            ShotImage.Visibility = Visibility.Visible;
            Notify(InfoBarSeverity.Success, P("Screenshot saved", "已儲存截圖"), local);
        }
        else Notify(InfoBarSeverity.Error, P("Screenshot failed", "截圖失敗"), Msg(r));
    }

    private async void Logcat_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        Busy.IsActive = true;
        Console.Text = await AdbService.Logcat(serial, 400);
        Busy.IsActive = false;
    }

    private async void Packages_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        Busy.IsActive = true;
        Console.Text = await AdbService.Packages(serial);
        Busy.IsActive = false;
    }

    private async void RebootSystem_Click(object sender, RoutedEventArgs e) => await DoReboot("");
    private async void RebootBootloader_Click(object sender, RoutedEventArgs e) => await DoReboot("bootloader");
    private async void RebootRecovery_Click(object sender, RoutedEventArgs e) => await DoReboot("recovery");

    private async Task DoReboot(string mode)
    {
        if (!Require(out var serial)) return;
        var r = await AdbService.Reboot(serial, mode);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Reboot", "重啟"), Msg(r));
    }

    private void ShellBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) { e.Handled = true; ShellRun_Click(sender, e); }
    }

    private async void ShellRun_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        var cmd = (ShellBox.Text ?? "").Trim();
        if (cmd.Length == 0) return;
        Busy.IsActive = true;
        Console.Text = await AdbService.Shell(serial, cmd);
        Busy.IsActive = false;
    }

    // ── Files tab (push / pull) ─────────────────────────────────────────────────────

    private async Task LoadDir(string path)
    {
        if (!Require(out var serial)) return;
        _cwd = string.IsNullOrEmpty(path) ? "/" : path;
        PathBox.Text = _cwd;
        Busy.IsActive = true;
        var entries = await AdbService.ListDir(serial, _cwd);
        Busy.IsActive = false;
        FileList.ItemsSource = entries;
    }

    private async void FilesGo_Click(object sender, RoutedEventArgs e) => await LoadDir((PathBox.Text ?? "").Trim());

    private void PathBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) { e.Handled = true; FilesGo_Click(sender, e); }
    }

    private async void FilesUp_Click(object sender, RoutedEventArgs e)
    {
        var p = _cwd.TrimEnd('/');
        int i = p.LastIndexOf('/');
        await LoadDir(i <= 0 ? "/" : p.Substring(0, i));
    }

    private async void FileList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (FileList.SelectedItem is AdbFileEntry { IsDirectory: true } d)
            await LoadDir(Combine(_cwd, d.Name));
    }

    private static string Combine(string dir, string name)
        => (dir.EndsWith("/") ? dir : dir + "/") + name;

    private async void Pull_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        if (FileList.SelectedItem is not AdbFileEntry entry)
        {
            Notify(InfoBarSeverity.Warning, P("Pick a file first", "請先揀一個檔案"), "");
            return;
        }
        var remote = Combine(_cwd, entry.Name);
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), entry.Name);
        Busy.IsActive = true;
        var r = await AdbService.Pull(serial, remote, local);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Pull", "拉取"), r.Success ? local : Msg(r));
    }

    private async void Push_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        var path = await FileDialogs.OpenFileAsync();
        if (path is null) return;
        var remote = Combine(_cwd, Path.GetFileName(path));
        Busy.IsActive = true;
        var r = await AdbService.Push(serial, path, remote);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Push", "推送"), r.Success ? remote : Msg(r));
        if (r.Success) await LoadDir(_cwd);
    }

    private async void FileDelete_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        if (FileList.SelectedItem is not AdbFileEntry entry)
        {
            Notify(InfoBarSeverity.Warning, P("Pick a file first", "請先揀一個檔案"), "");
            return;
        }
        var remote = Combine(_cwd, entry.Name);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete on device?", "喺裝置刪除？"),
            Content = remote + "\n\n" + P("This permanently removes the file/folder from the device (rm -rf).",
                "呢個會永久喺裝置刪除檔案／資料夾（rm -rf）。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        Busy.IsActive = true;
        var r = await AdbService.Delete(serial, remote);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Delete", "刪除"), Msg(r));
        if (r.Success) await LoadDir(_cwd);
    }

    // ── APK backup tab ──────────────────────────────────────────────────────────────

    private async void ApkLoad_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        Busy.IsActive = true;
        var apks = await AdbService.InstalledApks(serial, ApkSystemCheck.IsChecked == true);
        Busy.IsActive = false;
        ApkList.ItemsSource = apks;
        if (apks.Count == 0)
            Notify(InfoBarSeverity.Informational, P("No apps", "冇程式"), "");
    }

    private async void ApkBackup_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        if (ApkList.SelectedItem is not AdbPackage pkg)
        {
            Notify(InfoBarSeverity.Warning, P("Pick an app first", "請先揀一個程式"), "");
            return;
        }
        var path = await FileDialogs.SaveFileAsync(pkg.Package, ".apk");
        if (path is null) return;
        Busy.IsActive = true;
        var r = await AdbService.BackupApk(serial, pkg.Package, path);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("APK backup", "APK 備份"), r.Success ? path : Msg(r));
    }

    // ── Live logcat tab ─────────────────────────────────────────────────────────────

    private void LiveStart_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out var serial)) return;
        if (AdbService.IsStreamingLogcat) return;

        // Build a logcat filter: optional tag, then a level spec.
        var level = (LogLevelBox.SelectedItem as string ?? "*:I").Split(' ')[^1]; // e.g. "*:I"
        var tag = (LogTagBox.Text ?? "").Trim();
        string filter = tag.Length > 0 ? $"-s {tag}:{level[^1]}" : level;

        LiveConsole.Text = "";
        bool ok = AdbService.StartLogcatStream(serial, filter, OnLogLine);
        if (!ok)
        {
            Notify(InfoBarSeverity.Error, P("Could not start logcat", "無法開始 logcat"), "");
            return;
        }
        LiveStartBtn.IsEnabled = false;
        LiveStopBtn.IsEnabled = true;
    }

    private void OnLogLine(string line)
    {
        _ui.TryEnqueue(() =>
        {
            // keep the live console bounded (~4000 lines) so it stays responsive.
            if (LiveConsole.Text.Length > 400_000)
                LiveConsole.Text = LiveConsole.Text.Substring(LiveConsole.Text.Length - 200_000);
            LiveConsole.Text += line + "\n";
            LiveConsole.Select(LiveConsole.Text.Length, 0);
        });
    }

    private void LiveStop_Click(object sender, RoutedEventArgs e)
    {
        AdbService.StopLogcatStream();
        LiveStartBtn.IsEnabled = true;
        LiveStopBtn.IsEnabled = false;
    }

    private void LiveClear_Click(object sender, RoutedEventArgs e) => LiveConsole.Text = "";

    // ── Screen mirror (scrcpy) tab ──────────────────────────────────────────────────

    private ScrcpyOptions BuildScrcpy(bool record, string recordPath)
    {
        int max = MaxSizeBox.SelectedIndex <= 0 ? 0 : int.Parse((MaxSizeBox.SelectedItem as string ?? "0 px").Split(' ')[0]);
        int bitrate = int.Parse((BitrateBox.SelectedItem as string ?? "8 Mbps").Split(' ')[0]);
        return new ScrcpyOptions
        {
            Serial = SelectedSerial ?? "",
            MaxSize = max,
            Bitrate = bitrate,
            StayAwake = StayAwakeCheck.IsChecked == true,
            TurnScreenOff = ScreenOffCheck.IsChecked == true,
            ShowTouches = ShowTouchesCheck.IsChecked == true,
            Record = record,
            RecordPath = recordPath,
        };
    }

    private void MirrorStart_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out _)) return;
        var r = ScrcpyService.Start(BuildScrcpy(false, ""));
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Screen mirror", "螢幕鏡像"), Msg(r));
        SyncMirrorButtons();
    }

    private async void MirrorRecord_Click(object sender, RoutedEventArgs e)
    {
        if (!Require(out _)) return;
        var path = await FileDialogs.SaveFileAsync($"scrcpy-{DateTime.Now:yyyyMMdd-HHmmss}", ".mp4", ".mkv");
        if (path is null) return;
        var r = ScrcpyService.Start(BuildScrcpy(true, path));
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Screen mirror + record", "螢幕鏡像＋錄影"), r.Success ? path : Msg(r));
        SyncMirrorButtons();
    }

    private void MirrorStop_Click(object sender, RoutedEventArgs e)
    {
        ScrcpyService.Stop();
        SyncMirrorButtons();
    }

    private void SyncMirrorButtons()
    {
        bool running = ScrcpyService.IsRunning;
        MirrorStartBtn.IsEnabled = !running;
        MirrorRecordBtn.IsEnabled = !running;
        MirrorStopBtn.IsEnabled = running;
    }

    // ── shared helpers ──────────────────────────────────────────────────────────────

    private bool Require(out string serial)
    {
        serial = SelectedSerial ?? "";
        if (serial.Length == 0)
        {
            Notify(InfoBarSeverity.Warning, P("Pick a device first", "請先揀一部裝置"), "");
            return false;
        }
        return true;
    }

    private static string Msg(WinTune.Models.TweakResult r)
        => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev; ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
