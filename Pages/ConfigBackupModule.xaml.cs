using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 設定與備份 · Config &amp; Backup — export the whole suite's settings to a portable .zip bundle,
/// import &amp; re-apply, keep a local git snapshot history, restore/diff snapshots, schedule a daily
/// backup, export touched registry keys + the winget app list, back up taskbar/Start, mirror, bundle,
/// prune and verify integrity. Fully in-app, fully bilingual.
/// </summary>
public sealed partial class ConfigBackupModule : Page
{
    private bool _busy;
    private bool _loading;                 // guards control-init from firing persistence handlers
    private DateTime _lastTickRun = DateTime.MinValue;
    private readonly DispatcherTimer _syncTimer = new();

    // SettingsStore keys (all stored as strings — SettingsStore is Dictionary<string,string>).
    private const string KeyEnabled = "backup.autosync.enabled";
    private const string KeyUnit = "backup.autosync.unit";
    private const string KeyCount = "backup.autosync.count";
    private const string KeyRemote = "backup.autosync.remote";
    private const string KeyLastRun = "backup.autosync.lastrun";
    private const string KeyBg = "backup.autosync.background";

    public ConfigBackupModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        _syncTimer.Tick += async (_, _) => await OnTimerTick();
        Loaded += async (_, _) =>
        {
            Render();
            await RefreshSnaps();
            await RefreshScheduleStatus();
            await LoadAutoSyncState();
        };
        Unloaded += (_, _) => _syncTimer.Stop();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Config & Backup · 設定與備份";
        HeaderBlurb.Text = P(
            "Snapshot, back up and restore your whole WinForge configuration — all in-app.",
            "影快照、備份同還原成個 WinForge 設定 — 全程喺 app 內。");

        BundleTitle.Text = P("Portable settings bundle", "可攜設定檔案");
        BundleDesc.Text = P("Export every WinForge setting to a single .zip (with a version manifest + SHA-256 checksums), or import one back and re-apply it.",
            "將每項 WinForge 設定匯出成一個 .zip（連版本資料同 SHA-256 雜湊），或者匯入返再套用。");
        ExportBundleBtn.Content = P("Export bundle…", "匯出檔案…");
        ImportBundleBtn.Content = P("Import bundle…", "匯入檔案…");

        SnapTitle.Text = P("Config snapshots (local git history)", "設定快照（本地 git 歷史）");
        SnapDesc.Text = P("Each snapshot commits your settings (plus a winget app list) to a local git repo so you can browse, diff and restore any point in time.",
            "每個快照會將設定（連 winget 程式清單）commit 入本地 git 倉庫，可以瀏覽、比較同還原任何時間點。");
        SnapMessageBox.PlaceholderText = P("Optional note…", "可填備註…");
        TakeSnapshotBtn.Content = P("Take snapshot", "影快照");
        RefreshSnapsBtn.Content = P("Refresh", "重新整理");
        VerifyBtn.Content = P("Verify integrity", "驗證完整性");
        PruneBtn.Content = P("Prune history", "清理歷史");
        BundleFileBtn.Content = P("Save as .bundle…", "存做 .bundle…");

        CaptureTitle.Text = P("Capture & export", "擷取與匯出");
        CaptureDesc.Text = P("Export the registry keys WinForge touches to a .reg file, capture the installed-app list via winget, and back up taskbar pins + the Start layout.",
            "將 WinForge 改過嘅登錄機碼匯出做 .reg、用 winget 擷取裝咗嘅程式清單，並備份工作列固定捷徑同開始選單排版。");
        ExportRegBtn.Content = P("Export registry (.reg)…", "匯出登錄檔（.reg）…");
        ExportWingetBtn.Content = P("Capture app list…", "擷取程式清單…");
        BackupTaskbarBtn.Content = P("Back up taskbar / Start…", "備份工作列／開始選單…");

        SyncTitle.Text = P("Auto-sync schedule (settings → local git)", "自動同步排程（設定 → 本地 git）");
        SyncDesc.Text = P(
            "Commit your current settings into the local git snapshot repo on a fixed interval. While WinForge is open an in-app timer handles it; tick a background task to keep syncing when the app is closed. Identical snapshots are skipped automatically, so short intervals are safe.",
            "按固定間隔將而家嘅設定 commit 入本地 git 快照倉庫。WinForge 開住嘅時候由 app 內計時器處理；剔背景工作可以喺 app 閂咗都繼續同步。完全一樣嘅快照會自動略過，所以短間隔都安全。");

        EveryLabel.Text = P("Every", "每");
        // Rebuild the unit ComboBox bilingually, preserving the current selection.
        int unitIdx = IntervalUnit.SelectedIndex < 0 ? 0 : IntervalUnit.SelectedIndex;
        IntervalUnit.Items.Clear();
        IntervalUnit.Items.Add(P("minute(s)", "分鐘"));
        IntervalUnit.Items.Add(P("hour(s)", "小時"));
        IntervalUnit.Items.Add(P("day(s)", "日"));
        IntervalUnit.SelectedIndex = unitIdx;
        AutoSyncToggle.OnContent = P("On", "開");
        AutoSyncToggle.OffContent = P("Off", "關");

        RemoteLabel.Text = P("Optional remote URL", "可選遠端網址");
        RemoteUrlBox.PlaceholderText = P("https://… or git@…  (push after each sync)", "https://… 或 git@…（每次同步後 push）");
        PushNowBtn.Content = P("Push now", "立即推送");

        SyncNowBtn.Content = P("Sync now", "立即同步");
        BackgroundTaskCheck.Content = P("Also run in background (Task Scheduler) when WinForge is closed",
            "WinForge 閂咗都喺背景執行（工作排程器）");
        UpdateLastSyncLabel();

        AutoTitle.Text = P("Automate & mirror", "自動化與鏡像");
        AutoDesc.Text = P("Schedule a daily snapshot, and mirror the snapshot repo to a folder or network share with robocopy /MIR.",
            "排定每日快照，並用 robocopy /MIR 將快照倉庫鏡像去資料夾或網絡共享。");
        TimeLabel.Text = P("Daily at", "每日");
        ScheduleBtn.Content = P("Schedule daily backup", "排定每日備份");
        UnscheduleBtn.Content = P("Remove schedule", "移除排程");
        MirrorBtn.Content = P("Mirror to folder…", "鏡像去資料夾…");

        OutputTitle.Text = P("Output", "輸出");
        CopyOutputBtn.Content = P("Copy", "複製");
    }

    // ───────────────────────── result/output plumbing ─────────────────────────

    private void Show(TweakResult r, string verb)
    {
        bool needAdmin = !r.Success && !AdminHelper.IsElevated;
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = r.Message is null
            ? verb
            : $"{verb} — {r.Message.Primary}";
        ResultBar.IsOpen = true;
        if (!string.IsNullOrWhiteSpace(r.Output))
            OutputText.Text = r.Output;
    }

    private async Task Run(Func<Task<TweakResult>> op, string verb)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var r = await op();
            Show(r, verb);
        }
        catch (Exception ex)
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            ResultBar.Message = ex.Message;
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
    }

    // ───────────────────────── bundle ─────────────────────────

    private async void ExportBundle_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync($"WinForge-config-{DateTime.Now:yyyyMMdd-HHmm}", ".zip");
        if (path is null) return;
        await Run(() => ConfigBackupService.ExportBundle(path), P("Export bundle", "匯出檔案"));
    }

    private async void ImportBundle_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".zip");
        if (path is null) return;
        await Run(() => ConfigBackupService.ImportBundle(path), P("Import bundle", "匯入檔案"));
    }

    // ───────────────────────── snapshots ─────────────────────────

    private async void TakeSnapshot_Click(object sender, RoutedEventArgs e)
    {
        await Run(() => ConfigBackupService.TakeSnapshot(SnapMessageBox.Text), P("Take snapshot", "影快照"));
        SnapMessageBox.Text = "";
        await RefreshSnaps();
    }

    private async void RefreshSnaps_Click(object sender, RoutedEventArgs e) => await RefreshSnaps();

    private async Task RefreshSnaps()
    {
        var snaps = await ConfigBackupService.ListSnapshots();
        SnapList.ItemsSource = snaps;
        bool empty = snaps.Count == 0;
        SnapEmpty.Text = P("No snapshots yet — take one to start a config history.",
            "未有快照 — 影一個開始記錄設定歷史。");
        SnapEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SnapActions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not SnapshotInfo snap) return;
        var mf = new MenuFlyout();

        void Add(string en, string zh, string glyph, Func<Task> op)
        {
            var it = new MenuFlyoutItem { Text = $"{en} · {zh}", Icon = new FontIcon { Glyph = glyph } };
            it.Click += async (_, _) => await op();
            mf.Items.Add(it);
        }

        Add("Restore to this snapshot", "還原到呢個快照", ((char)0xE7A7).ToString(), async () =>
        {
            await Run(() => ConfigBackupService.RestoreSnapshot(snap.Hash), P("Restore", "還原"));
            await RefreshSnaps();
        });
        Add("Diff vs current", "同而家比較", ((char)0xE8AB).ToString(), async () =>
            await Run(() => ConfigBackupService.DiffSnapshot(snap.Hash), P("Diff", "比較")));

        mf.ShowAt(b);
    }

    private async void Verify_Click(object sender, RoutedEventArgs e)
        => await Run(() => ConfigBackupService.VerifyIntegrity(), P("Verify integrity", "驗證完整性"));

    private async void Prune_Click(object sender, RoutedEventArgs e)
    {
        await Run(() => ConfigBackupService.PruneHistory(), P("Prune history", "清理歷史"));
        await RefreshSnaps();
    }

    private async void BundleFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync($"winforge-config-{DateTime.Now:yyyyMMdd}", ".bundle");
        if (path is null) return;
        await Run(() => ConfigBackupService.CreateBundle(path), P("Create bundle", "建立 bundle"));
    }

    // ───────────────────────── capture & export ─────────────────────────

    private async void ExportReg_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync($"winforge-registry-{DateTime.Now:yyyyMMdd}", ".reg");
        if (path is null) return;
        await Run(() => ConfigBackupService.ExportRegistry(path), P("Export registry", "匯出登錄檔"));
    }

    private async void ExportWinget_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("apps", ".json");
        if (path is null) return;
        await Run(() => ConfigBackupService.ExportWingetApps(path), P("Capture app list", "擷取程式清單"));
    }

    private async void BackupTaskbar_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFolderAsync();
        if (path is null) return;
        await Run(() => ConfigBackupService.BackupTaskbarAndStart(path), P("Back up taskbar / Start", "備份工作列／開始選單"));
    }

    // ───────────────────────── automate & mirror ─────────────────────────

    private async void Schedule_Click(object sender, RoutedEventArgs e)
    {
        var time = string.IsNullOrWhiteSpace(TimeBox.Text) ? "03:00" : TimeBox.Text.Trim();
        await Run(() => ConfigBackupService.ScheduleDailyBackup(time), P("Schedule daily backup", "排定每日備份"));
        await RefreshScheduleStatus();
    }

    private async void Unschedule_Click(object sender, RoutedEventArgs e)
    {
        await Run(() => ConfigBackupService.UnscheduleDailyBackup(), P("Remove schedule", "移除排程"));
        await RefreshScheduleStatus();
    }

    private async Task RefreshScheduleStatus()
    {
        bool on = await ConfigBackupService.IsDailyBackupScheduled();
        ScheduleStatus.Text = on
            ? P("● Scheduled", "● 已排程")
            : P("○ Not scheduled", "○ 未排程");
    }

    private async void Mirror_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFolderAsync();
        if (path is null) return;
        MirrorDest.Text = path;
        await Run(() => ConfigBackupService.MirrorTo(path), P("Mirror", "鏡像"));
    }

    // ───────────────────────── auto-sync schedule ─────────────────────────

    private static readonly string[] Units = { "minute", "hour", "day" };

    private async Task LoadAutoSyncState()
    {
        _loading = true;
        try
        {
            // git gating
            bool gitOk = await ConfigBackupService.IsGitAvailable();
            if (!gitOk)
            {
                GitMissingBar.Title = P("Git not found", "搵唔到 Git");
                GitMissingBar.Message = P("Auto-sync needs the git CLI on PATH. Install it to enable scheduling.",
                    "自動同步需要 PATH 上有 git CLI。安裝後即可排程。");
                GitMissingBar.IsOpen = true;
                // Offer a one-click winget install that re-checks afterwards.
                GitMissingBar.ActionButton = EngineBars.AutoInstallButton(
                    "Git.Git", "Install Git", "安裝 Git",
                    async () => { GitMissingBar.IsOpen = false; await LoadAutoSyncState(); });
            }
            else
            {
                GitMissingBar.IsOpen = false;
                GitMissingBar.ActionButton = null;
            }
            bool controlsEnabled = gitOk;
            AutoSyncToggle.IsEnabled = controlsEnabled;
            IntervalCount.IsEnabled = controlsEnabled;
            IntervalUnit.IsEnabled = controlsEnabled;
            SyncNowBtn.IsEnabled = controlsEnabled;
            PushNowBtn.IsEnabled = controlsEnabled;
            BackgroundTaskCheck.IsEnabled = controlsEnabled;

            // restore persisted schedule
            int count = int.TryParse(SettingsStore.Get(KeyCount, "15"), out var c) ? Math.Max(1, c) : 15;
            IntervalCount.Value = count;
            var unit = SettingsStore.Get(KeyUnit, "minute");
            int ui = Array.IndexOf(Units, unit);
            IntervalUnit.SelectedIndex = ui < 0 ? 0 : ui;
            RemoteUrlBox.Text = SettingsStore.Get(KeyRemote, "");
            bool enabled = SettingsStore.Get(KeyEnabled, "false") == "true";
            AutoSyncToggle.IsOn = enabled && gitOk;
            BackgroundTaskCheck.IsChecked = await ConfigBackupService.IsAutoSyncScheduled();

            UpdateLastSyncLabel();
            if (enabled && gitOk) StartTimer();
        }
        finally { _loading = false; }
    }

    private string SelectedUnit() =>
        Units[IntervalUnit.SelectedIndex < 0 ? 0 : IntervalUnit.SelectedIndex];

    private int SelectedCount() =>
        (int)Math.Max(1, double.IsNaN(IntervalCount.Value) ? 1 : IntervalCount.Value);

    private TimeSpan CurrentInterval()
    {
        int n = SelectedCount();
        var span = SelectedUnit() switch
        {
            "minute" => TimeSpan.FromMinutes(n),
            "hour" => TimeSpan.FromHours(n),
            _ => TimeSpan.FromDays(n),
        };
        // Clamp the minimum to ~1 min to avoid commit storms.
        return span < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : span;
    }

    private void StartTimer()
    {
        _syncTimer.Stop();
        _syncTimer.Interval = CurrentInterval();
        _syncTimer.Start();
    }

    private async Task OnTimerTick()
    {
        // Coalesce: never run two ticks at once, and never more than once a minute.
        if (_busy) return;
        if ((DateTime.Now - _lastTickRun) < TimeSpan.FromSeconds(55)) return;
        _lastTickRun = DateTime.Now;
        var remote = RemoteUrlBox.Text?.Trim();
        await Run(() => ConfigBackupService.SyncNow(string.IsNullOrWhiteSpace(remote) ? null : remote),
            P("Auto-sync", "自動同步"));
        SettingsStore.Set(KeyLastRun, DateTime.Now.ToString("o"));
        UpdateLastSyncLabel();
        await RefreshSnaps();
    }

    private void UpdateLastSyncLabel()
    {
        var raw = SettingsStore.Get(KeyLastRun, "");
        if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            LastSyncStatus.Text = P($"Last synced: {dt:yyyy-MM-dd HH:mm:ss}",
                $"上次同步：{dt:yyyy-MM-dd HH:mm:ss}");
        else
            LastSyncStatus.Text = P("Last synced: never", "上次同步：未試過");
    }

    private async void AutoSyncToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        SettingsStore.Set(KeyEnabled, AutoSyncToggle.IsOn ? "true" : "false");
        SettingsStore.Set(KeyUnit, SelectedUnit());
        SettingsStore.Set(KeyCount, SelectedCount().ToString());
        if (AutoSyncToggle.IsOn)
        {
            StartTimer();
            // Take an immediate baseline snapshot so the schedule has a visible effect right away.
            await OnTimerTick();
        }
        else
        {
            _syncTimer.Stop();
        }
    }

    private async void SyncNow_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Set(KeyUnit, SelectedUnit());
        SettingsStore.Set(KeyCount, SelectedCount().ToString());
        var remote = RemoteUrlBox.Text?.Trim();
        SettingsStore.Set(KeyRemote, remote ?? "");
        await Run(() => ConfigBackupService.SyncNow(string.IsNullOrWhiteSpace(remote) ? null : remote),
            P("Sync now", "立即同步"));
        SettingsStore.Set(KeyLastRun, DateTime.Now.ToString("o"));
        _lastTickRun = DateTime.Now;
        UpdateLastSyncLabel();
        await RefreshSnaps();
    }

    private async void PushNow_Click(object sender, RoutedEventArgs e)
    {
        var remote = RemoteUrlBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(remote))
        {
            ResultBar.Severity = InfoBarSeverity.Warning;
            ResultBar.Title = P("Remote URL needed", "需要遠端網址");
            ResultBar.Message = P("Enter a git remote URL first.", "請先輸入 git 遠端網址。");
            ResultBar.IsOpen = true;
            return;
        }
        SettingsStore.Set(KeyRemote, remote);
        await Run(() => ConfigBackupService.PushToRemote(remote!), P("Push to remote", "推送到遠端"));
    }

    private async void BackgroundTask_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (BackgroundTaskCheck.IsChecked == true)
        {
            SettingsStore.Set(KeyBg, "true");
            await Run(() => ConfigBackupService.ScheduleAutoSync(SelectedUnit(), SelectedCount()),
                P("Schedule background auto-sync", "排定背景自動同步"));
        }
        else
        {
            SettingsStore.Set(KeyBg, "false");
            await Run(() => ConfigBackupService.UnscheduleAutoSync(), P("Remove background auto-sync", "移除背景自動同步"));
        }
    }

    // ───────────────────────── output ─────────────────────────

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText(OutputText.Text ?? "");
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
    }
}
