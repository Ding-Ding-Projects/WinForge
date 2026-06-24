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

    public ConfigBackupModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await RefreshSnaps(); await RefreshScheduleStatus(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Config & Backup · 設定與備份";
        HeaderBlurb.Text = P(
            "Snapshot, back up and restore your whole WinTune configuration — all in-app.",
            "影快照、備份同還原成個 WinTune 設定 — 全程喺 app 內。");

        BundleTitle.Text = P("Portable settings bundle", "可攜設定檔案");
        BundleDesc.Text = P("Export every WinTune setting to a single .zip (with a version manifest + SHA-256 checksums), or import one back and re-apply it.",
            "將每項 WinTune 設定匯出成一個 .zip（連版本資料同 SHA-256 雜湊），或者匯入返再套用。");
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
        CaptureDesc.Text = P("Export the registry keys WinTune touches to a .reg file, capture the installed-app list via winget, and back up taskbar pins + the Start layout.",
            "將 WinTune 改過嘅登錄機碼匯出做 .reg、用 winget 擷取裝咗嘅程式清單，並備份工作列固定捷徑同開始選單排版。");
        ExportRegBtn.Content = P("Export registry (.reg)…", "匯出登錄檔（.reg）…");
        ExportWingetBtn.Content = P("Capture app list…", "擷取程式清單…");
        BackupTaskbarBtn.Content = P("Back up taskbar / Start…", "備份工作列／開始選單…");

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
        var path = await FileDialogs.SaveFileAsync($"WinTune-config-{DateTime.Now:yyyyMMdd-HHmm}", ".zip");
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
        var path = await FileDialogs.SaveFileAsync($"wintune-config-{DateTime.Now:yyyyMMdd}", ".bundle");
        if (path is null) return;
        await Run(() => ConfigBackupService.CreateBundle(path), P("Create bundle", "建立 bundle"));
    }

    // ───────────────────────── capture & export ─────────────────────────

    private async void ExportReg_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync($"wintune-registry-{DateTime.Now:yyyyMMdd}", ".reg");
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

    // ───────────────────────── output ─────────────────────────

    private void CopyOutput_Click(object sender, RoutedEventArgs e)
    {
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText(OutputText.Text ?? "");
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
    }
}
