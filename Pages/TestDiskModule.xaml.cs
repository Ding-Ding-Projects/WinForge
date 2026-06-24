using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// TestDisk / PhotoRec 資料救援 · TestDisk / PhotoRec data recovery — a full WinUI front-end wrapping the
/// cgsecurity photorec_win.exe / testdisk_win.exe CLIs. Download/extract the tools in-app; pick a physical
/// disk or an image file; pick an output folder (blocked if it is on the SAME disk being recovered); choose
/// file families; carve with PhotoRec (live log + recovered count); run a TestDisk read-only partition scan;
/// open the output folder. The interactive TUI is never launched. Fully bilingual (English + 粵語).
/// </summary>
public sealed partial class TestDiskModule : Page
{
    private List<RecoverySource> _disks = new();
    private List<RecoveryFileType> _types = new();
    private RecoverySource? _imageSource;
    private string _outputFolder = "";
    private bool _busy;
    private CancellationTokenSource? _cts;

    public TestDiskModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await Init(); };
        Unloaded += (_, _) => { try { _cts?.Cancel(); } catch { } };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "TestDisk / PhotoRec · 資料救援";
        HeaderSub.Text = P(
            "Carve lost files (PhotoRec) and scan partitions read-only (TestDisk). Recover to a DIFFERENT disk.",
            "碳化救回遺失檔案（PhotoRec），唯讀掃描分割區（TestDisk）。請救援到另一個磁碟。");

        SourceLabel.Text = P("Source disk / image · 來源磁碟／映像", "來源磁碟／映像");
        OutputLabel.Text = P("Output folder · 輸出資料夾", "輸出資料夾");
        OutputBox.PlaceholderText = P("Pick a folder on a DIFFERENT disk…", "請揀另一個磁碟上嘅資料夾…");
        ImageBtn.Content = P("Image file…", "映像檔…");
        RefreshDisksBtn.Content = P("Rescan", "重新掃描");
        OutputBtn.Content = P("Browse…", "瀏覽…");

        TypesLabel.Text = P("File types to recover", "要救援嘅檔案類型");
        FreeSpaceCheck.Content = P("Free space only (faster)", "只掃描可用空間（較快）");
        SelectAllBtn.Content = P("All", "全選");
        SelectNoneBtn.Content = P("None", "全不選");

        CarveBtn.Content = P("Recover files (PhotoRec)", "救援檔案（PhotoRec）");
        ScanBtn.Content = P("Scan partitions (TestDisk)", "掃描分割區（TestDisk）");
        CancelBtn.Content = P("Cancel", "取消");
        OpenFolderBtn.Content = P("Open output", "開啟輸出");

        LogTitle.Text = P("Live log", "即時記錄");
        ClearLogBtn.Content = P("Clear", "清除");

        DownloadBtn.Content = P("Download recovery tools", "下載救援工具");
    }

    private async Task Init()
    {
        await RefreshEngine();
        await LoadDisks();
        await LoadTypes();
        UpdateButtons();
    }

    // ───────────────────────── engine ─────────────────────────

    private async Task RefreshEngine()
    {
        bool ok = await Task.Run(TestDiskService.IsAvailable);
        if (ok)
        {
            EngineBar.IsOpen = false;
        }
        else
        {
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Recovery tools not installed", "未安裝救援工具");
            EngineBar.Message = P(
                $"PhotoRec / TestDisk v{TestDiskService.Version} (GPLv2, cgsecurity.org) are downloaded on demand — not bundled. Click to fetch + extract (~7 MB).",
                $"PhotoRec / TestDisk v{TestDiskService.Version}（GPLv2，cgsecurity.org）按需下載，唔會內附。撳一下即可下載＋解壓（約 7 MB）。");
            EngineBar.IsOpen = true;
        }
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        DownloadBtn.IsEnabled = false;
        var progress = new Progress<string>(s => { EngineBar.Message = s; AppendLog(s); });
        try
        {
            var r = await TestDiskService.DownloadBinaries(progress);
            ShowResult(r);
            await RefreshEngine();
            await LoadTypes();
        }
        finally { _busy = false; DownloadBtn.IsEnabled = true; UpdateButtons(); }
    }

    // ───────────────────────── disks / image ─────────────────────────

    private async Task LoadDisks()
    {
        try { _disks = await TestDiskService.ListDisks(); }
        catch { _disks = new(); }
        RebuildSourceCombo();
    }

    private void RebuildSourceCombo()
    {
        var prev = SelectedSource;
        SourceCombo.Items.Clear();
        foreach (var d in _disks)
            SourceCombo.Items.Add(new ComboBoxItem { Content = d.Display, Tag = d });
        if (_imageSource is not null)
            SourceCombo.Items.Add(new ComboBoxItem { Content = _imageSource.Display, Tag = _imageSource });

        // Re-select previous, else first item.
        if (prev is not null)
        {
            foreach (ComboBoxItem it in SourceCombo.Items)
                if (it.Tag is RecoverySource s && s.DevicePath == prev.DevicePath) { SourceCombo.SelectedItem = it; break; }
        }
        if (SourceCombo.SelectedItem is null && SourceCombo.Items.Count > 0)
            SourceCombo.SelectedIndex = 0;
    }

    private RecoverySource? SelectedSource =>
        (SourceCombo.SelectedItem as ComboBoxItem)?.Tag as RecoverySource;

    private async void RefreshDisks_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        await LoadDisks();
        await ValidateSameDisk();
        UpdateButtons();
    }

    private async void PickImage_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(
            new[] { new FileDialogs.Filter("Disk images", "*.dd;*.img;*.raw;*.e01;*.iso;*.bin"), new FileDialogs.Filter("All files", "*.*") },
            P("Pick a disk image to recover from", "揀一個要救援嘅磁碟映像"));
        if (path is null) return;
        _imageSource = TestDiskService.ImageSource(path);
        RebuildSourceCombo();
        // Select the image we just added.
        foreach (ComboBoxItem it in SourceCombo.Items)
            if (it.Tag is RecoverySource s && !s.IsDisk) SourceCombo.SelectedItem = it;
        await ValidateSameDisk();
        UpdateButtons();
    }

    private async void Source_Changed(object sender, SelectionChangedEventArgs e)
    {
        await ValidateSameDisk();
        UpdateButtons();
    }

    // ───────────────────────── output folder ─────────────────────────

    private async void PickOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFolderAsync(P("Pick the output folder (on a DIFFERENT disk)", "揀輸出資料夾（喺另一個磁碟）"));
        if (path is null) return;
        _outputFolder = path;
        OutputBox.Text = path;
        await ValidateSameDisk();
        UpdateButtons();
    }

    /// <summary>強制「救援到唔同磁碟」· Enforce recover-to-a-different-disk; blocks Carve when violated.</summary>
    private async Task<bool> ValidateSameDisk()
    {
        SameDiskBar.IsOpen = false;
        var src = SelectedSource;
        if (src is null || string.IsNullOrEmpty(_outputFolder)) return false;
        bool same;
        try { same = await TestDiskService.IsSameDisk(src, _outputFolder); }
        catch { same = false; }
        if (same)
        {
            SameDiskBar.Title = P("Output is on the SAME disk", "輸出喺同一個磁碟");
            SameDiskBar.Message = P(
                "Recovering onto the disk you are reading can overwrite the very files you are trying to save. Choose a folder on a DIFFERENT physical disk.",
                "救援到你正在讀取嘅磁碟，會覆蓋你想救嘅檔案。請揀另一個實體磁碟上嘅資料夾。");
            SameDiskBar.IsOpen = true;
        }
        return same;
    }

    // ───────────────────────── file types ─────────────────────────

    private async Task LoadTypes()
    {
        try { _types = await TestDiskService.ListFileTypes(); }
        catch { _types = TestDiskService.DefaultFileTypes(); }
        TypesList.ItemsSource = _types;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => SetAll(true);
    private void SelectNone_Click(object sender, RoutedEventArgs e) => SetAll(false);

    private void SetAll(bool on)
    {
        foreach (var t in _types) t.Selected = on;
        TypesList.ItemsSource = null;
        TypesList.ItemsSource = _types;
    }

    // ───────────────────────── run: carve ─────────────────────────

    private async void Carve_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var src = SelectedSource;
        if (src is null) { ShowResult(TweakResult.Fail("Pick a source disk or image first.", "請先揀來源磁碟或映像。")); return; }
        if (string.IsNullOrEmpty(_outputFolder)) { ShowResult(TweakResult.Fail("Pick an output folder first.", "請先揀輸出資料夾。")); return; }
        if (await ValidateSameDisk())
        {
            ShowResult(TweakResult.Fail("Output is on the same disk — blocked for safety.", "輸出喺同一個磁碟 — 為安全已封鎖。"));
            return;
        }

        // Admin warning for raw physical disks.
        if (src.IsDisk && !AdminHelper.IsElevated)
        {
            var go = await ConfirmAsync(
                P("Administrator may be required", "可能需要管理員權限"),
                P("Reading a raw physical disk (\\\\.\\PhysicalDrive) usually needs elevation. PhotoRec may fail to open the device without admin rights. Continue anyway?",
                  "讀取原始實體磁碟（\\\\.\\PhysicalDrive）通常需要提升權限。冇管理員權限 PhotoRec 可能開唔到裝置。仍然繼續？"),
                P("Continue", "繼續"));
            if (!go) return;
        }

        var selected = _types.Where(t => t.Selected).ToList();
        var typeNote = selected.Count == 0
            ? P("ALL file types (PhotoRec defaults)", "所有檔案類型（PhotoRec 預設）")
            : P($"{selected.Count} selected types", $"已選 {selected.Count} 種類型");
        var confirm = await ConfirmAsync(
            P("Start PhotoRec recovery?", "開始 PhotoRec 救援？"),
            P($"Source: {src.Display}\nOutput: {_outputFolder}\nTypes: {typeNote}\n\nPhotoRec reads the source and writes carved files to the output folder. It never writes to the source.",
              $"來源：{src.Display}\n輸出：{_outputFolder}\n類型：{typeNote}\n\nPhotoRec 會讀取來源並把碳化檔案寫到輸出資料夾，永不寫入來源。"),
            P("Recover", "救援"));
        if (!confirm) return;

        await RunJob(async ct =>
        {
            var progress = new Progress<RecoveryProgress>(rp =>
            {
                AppendLog(rp.Line);
                ProgressText.Text = P($"Recovered {rp.RecoveredCount} file(s)…", $"已救回 {rp.RecoveredCount} 個檔案…");
            });
            return await TestDiskService.RunPhotoRec(src, _outputFolder, _types, FreeSpaceCheck.IsChecked == true, progress, ct);
        }, P("Recovering with PhotoRec…", "PhotoRec 救援緊…"));

        OpenFolderBtn.IsEnabled = true;
    }

    // ───────────────────────── run: testdisk scan ─────────────────────────

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var src = SelectedSource;
        if (src is { IsDisk: true } && !AdminHelper.IsElevated)
        {
            var go = await ConfirmAsync(
                P("Administrator may be required", "可能需要管理員權限"),
                P("TestDisk reads raw disks and usually needs elevation. Continue anyway?",
                  "TestDisk 讀取原始磁碟通常需要提升權限。仍然繼續？"),
                P("Continue", "繼續"));
            if (!go) return;
        }

        await RunJob(async ct =>
        {
            // Whole-system list first, then a per-source read-only structure scan if one is selected.
            var listResult = await TestDiskService.RunTestDiskList(ct);
            AppendLog(listResult.Output ?? "");
            if (src is not null)
            {
                var scan = await TestDiskService.RunTestDiskScan(src, ct);
                AppendLog(scan.Output ?? "");
                return scan.Success
                    ? TweakResult.Ok("TestDisk read-only scan complete.", "TestDisk 唯讀掃描完成。")
                    : scan;
            }
            return listResult;
        }, P("Scanning partitions (read-only)…", "唯讀掃描分割區緊…"));
    }

    // ───────────────────────── job runner ─────────────────────────

    private async Task RunJob(Func<CancellationToken, Task<TweakResult>> job, string runningText)
    {
        _busy = true;
        _cts = new CancellationTokenSource();
        ProgressPanel.Visibility = Visibility.Visible;
        Progress.IsIndeterminate = true;
        ProgressText.Text = runningText;
        UpdateButtons();
        CancelBtn.IsEnabled = true;
        try
        {
            var r = await job(_cts.Token);
            ShowResult(r);
        }
        catch (OperationCanceledException)
        {
            ShowResult(TweakResult.Fail("Cancelled.", "已取消。"));
        }
        catch (Exception ex)
        {
            ShowResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"));
        }
        finally
        {
            _busy = false;
            ProgressPanel.Visibility = Visibility.Collapsed;
            CancelBtn.IsEnabled = false;
            _cts?.Dispose();
            _cts = null;
            UpdateButtons();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        AppendLog(P("— cancel requested —", "— 已要求取消 —"));
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_outputFolder)) TestDiskService.OpenFolder(_outputFolder);
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogText.Text = "";

    // ───────────────────────── helpers ─────────────────────────

    private void UpdateButtons()
    {
        bool installed = TestDiskService.IsAvailable();
        bool idle = !_busy;
        CarveBtn.IsEnabled = installed && idle;
        ScanBtn.IsEnabled = installed && idle;
        ImageBtn.IsEnabled = idle;
        RefreshDisksBtn.IsEnabled = idle;
        OutputBtn.IsEnabled = idle;
        SourceCombo.IsEnabled = idle;
        SelectAllBtn.IsEnabled = idle;
        SelectNoneBtn.IsEnabled = idle;
        FreeSpaceCheck.IsEnabled = idle;
        OpenFolderBtn.IsEnabled = !string.IsNullOrEmpty(_outputFolder) && idle;
    }

    private void AppendLog(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (LogText.Text.Length > 200_000)
            LogText.Text = LogText.Text.Substring(LogText.Text.Length - 150_000);
        LogText.Text += (LogText.Text.Length == 0 ? "" : "\n") + text;
        LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null, disableAnimation: true);
    }

    private async Task<bool> ConfirmAsync(string title, string body, string primary)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new ScrollViewer
            {
                MaxHeight = 320,
                Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
            },
            PrimaryButtonText = primary,
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ShowResult(TweakResult r)
    {
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = r.Message is null ? "" : Loc.I.Pick(r.Message.En, r.Message.Zh);
        ResultBar.IsOpen = true;
    }
}
