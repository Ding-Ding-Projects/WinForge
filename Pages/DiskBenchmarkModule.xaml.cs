using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生硬碟速度測試（CrystalDiskMark 風格）· Native CrystalDiskMark-style disk benchmark.
/// 揀目標磁碟／資料夾、測試檔大細同次數，跑經典測試組合（SEQ1M / RND4K，各種佇列深度），
/// 用 FILE_FLAG_NO_BUFFERING + WRITE_THROUGH 繞過快取攞準數，報 MB/s（隨機測試連 IOPS）。
/// Pick a target drive/folder, file size and pass count, run the classic SEQ1M/RND4K test set with
/// cache-bypassing direct I/O, and report MB/s (plus IOPS for random tests). Pure managed C# —
/// nothing external is launched. The large temp file is always deleted, even on cancel/error.
/// </summary>
public sealed partial class DiskBenchmarkModule : Page
{
    private readonly DiskBenchmarkService _svc = new();
    private string _folder = "";
    private CancellationTokenSource? _cts;
    private bool _busy;
    private bool _suppressDrive;

    public DiskBenchmarkModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        ResultsList.Header = ""; // header bound via Render
        Loaded += OnLoaded;
        Unloaded += (_, _) => _cts?.Cancel();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_folder))
        {
            var sys = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System)) ?? "C:\\";
            _folder = sys;
        }
        FolderBox.Text = _folder;
        BuildDrives();
        BuildSizes();
        BuildPasses();
        Render();
        UpdateEmptyHint();
    }

    private void Render()
    {
        Header.Title = "Disk Benchmark · 硬碟速度測試";
        HeaderBlurb.Text = P("Measure real read/write speed of any drive or folder with a CrystalDiskMark-style test set — sequential and random, at several queue depths. Pure in-app C# direct I/O; no external tool.",
            "用 CrystalDiskMark 風格嘅測試組合量度任何磁碟或資料夾嘅真實讀寫速度 — 循序同隨機、多種佇列深度。純 app 內 C# 直接 I/O，唔使任何外部工具。");

        WarnBar.Title = P("This writes a large temp file", "會寫一個大嘅暫存檔");
        WarnBar.Message = P("The benchmark creates a temporary test file of the chosen size on the target, then deletes it automatically (even if you cancel). Make sure there is enough free space. Avoid testing the drive your OS is paging to under heavy load.",
            "測試會喺目標位置建立一個你揀嘅大細嘅暫存測試檔，跑完會自動刪除（就算你取消都會）。請確保有足夠剩餘空間。");

        BrowseBtn.Content = P("Browse…", "瀏覽…");
        RunBtn.Content = _busy ? P("Running…", "測試緊…") : P("Run benchmark", "開始測試");
        CancelBtn.Content = P("Cancel", "取消");

        BuildDrives();
        BuildSizes();
        BuildPasses();

        // Results header row labels.
        ResultsList.Header = P("Test", "測試") + "   ·   MB/s   ·   IOPS";
        UpdateEmptyHint();
    }

    private void UpdateEmptyHint()
    {
        bool empty = ResultsList.Items.Count == 0;
        EmptyHint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Text = P("Pick a target and press Run benchmark to see SEQ1M and RND4K read/write speeds.",
            "揀好目標，撳「開始測試」就會見到 SEQ1M 同 RND4K 嘅讀寫速度。");
    }

    // ── Drive / target ─────────────────────────────────────────────────────────

    private void BuildDrives()
    {
        _suppressDrive = true;
        string? prev = (DriveBox.SelectedItem as ComboBoxItem)?.Tag as string;
        DriveBox.Items.Clear();
        foreach (var d in EnumDrives())
        {
            string free = HumanSize(d.free);
            string label = $"{d.root}  ({free} {P("free", "可用")})";
            DriveBox.Items.Add(new ComboBoxItem { Content = label, Tag = d.root });
        }
        // Select the drive matching the current folder.
        string curRoot = (Path.GetPathRoot(_folder) ?? "").ToUpperInvariant();
        int sel = -1;
        for (int i = 0; i < DriveBox.Items.Count; i++)
        {
            var tag = ((ComboBoxItem)DriveBox.Items[i]).Tag as string ?? "";
            if (string.Equals(tag, prev, StringComparison.OrdinalIgnoreCase)) { sel = i; break; }
            if (sel < 0 && string.Equals(tag, curRoot, StringComparison.OrdinalIgnoreCase)) sel = i;
        }
        if (sel < 0 && DriveBox.Items.Count > 0) sel = 0;
        DriveBox.SelectedIndex = sel;
        _suppressDrive = false;
    }

    private static IEnumerable<(string root, long free)> EnumDrives()
    {
        foreach (var d in DriveInfo.GetDrives())
        {
            bool ready;
            try { ready = d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable; }
            catch { ready = false; }
            if (!ready) continue;
            long free = 0;
            try { free = d.AvailableFreeSpace; } catch { }
            yield return (d.RootDirectory.FullName.ToUpperInvariant(), free);
        }
    }

    private void BuildSizes()
    {
        int sel = SizeBox.SelectedIndex < 0 ? 1 : SizeBox.SelectedIndex;
        SizeBox.Items.Clear();
        foreach (var (label, _) in SizeChoices())
            SizeBox.Items.Add(new ComboBoxItem { Content = P("Test size: ", "測試大細：") + label });
        SizeBox.SelectedIndex = Math.Min(sel, SizeBox.Items.Count - 1);
    }

    private static (string label, long bytes)[] SizeChoices() => new[]
    {
        ("256 MB", 256L << 20),
        ("1 GB", 1L << 30),
        ("4 GB", 4L << 30),
    };

    private void BuildPasses()
    {
        int sel = PassBox.SelectedIndex < 0 ? 2 : PassBox.SelectedIndex;
        PassBox.Items.Clear();
        for (int n = 1; n <= 5; n++)
            PassBox.Items.Add(new ComboBoxItem { Content = P($"{n} pass{(n == 1 ? "" : "es")}", $"{n} 次"), Tag = n });
        PassBox.SelectedIndex = Math.Min(sel, PassBox.Items.Count - 1);
    }

    private void Drive_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDrive) return;
        if ((DriveBox.SelectedItem as ComboBoxItem)?.Tag is string root && Directory.Exists(root))
        {
            _folder = root;
            FolderBox.Text = _folder;
        }
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose a folder on the drive to test", "揀要測試嘅磁碟上嘅資料夾"));
        if (folder is not null)
        {
            _folder = folder;
            FolderBox.Text = _folder;
            BuildDrives();
        }
    }

    // ── Run / cancel ───────────────────────────────────────────────────────────

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder))
        {
            Warn(P("Pick a valid target folder or drive first.", "請先揀一個有效嘅目標資料夾或磁碟。"));
            return;
        }

        long fileBytes = SizeChoices()[Math.Max(0, SizeBox.SelectedIndex)].bytes;
        int passes = (PassBox.SelectedItem as ComboBoxItem)?.Tag as int? ?? 3;

        // Free-space sanity check.
        try
        {
            var di = new DriveInfo(Path.GetPathRoot(_folder) ?? _folder);
            if (di.IsReady && di.AvailableFreeSpace < fileBytes + (64L << 20))
            {
                Warn(P($"Not enough free space — need at least {HumanSize(fileBytes)} free on {di.Name}.",
                    $"剩餘空間唔夠 — {di.Name} 至少要有 {HumanSize(fileBytes)} 可用。"));
                return;
            }
        }
        catch { }

        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Start benchmark?", "開始測試？"),
            Content = P($"This will write a {HumanSize(fileBytes)} temporary file to:\n\n{_folder}\n\nIt will be deleted automatically when the test finishes or is cancelled.",
                       $"會喺以下位置寫一個 {HumanSize(fileBytes)} 嘅暫存檔：\n\n{_folder}\n\n測試完成或取消時會自動刪除。"),
            PrimaryButtonText = P("Run · 開始", "開始"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        DiskBenchmarkService.CleanupStale(_folder);

        _busy = true;
        _cts = new CancellationTokenSource();
        SetRunningUi(true);
        ResultsList.ItemsSource = null;
        UpdateEmptyHint();
        ResultBar.IsOpen = false;

        var progress = new Progress<DiskBenchmarkService.Progress>(pr =>
        {
            ProgressLabel.Text = P($"{Loc.I.Pick(pr.Spec.En, pr.Spec.Zh)}  ·  pass {pr.Pass}/{pr.TotalPasses}",
                                   $"{Loc.I.Pick(pr.Spec.En, pr.Spec.Zh)}  ·  第 {pr.Pass}/{pr.TotalPasses} 次");
            ProgressBarCtl.Value = pr.Fraction;
        });

        try
        {
            var results = await _svc.RunAsync(_folder, fileBytes, passes, progress, _cts.Token);
            ResultsList.ItemsSource = results;
            ShowResult(InfoBarSeverity.Success, P("Benchmark complete.", "測試完成。"));
        }
        catch (OperationCanceledException)
        {
            ShowResult(InfoBarSeverity.Informational, P("Benchmark cancelled. Temp file removed.", "已取消測試，暫存檔已刪除。"));
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, P($"Benchmark failed: {ex.Message}", $"測試失敗：{ex.Message}"));
        }
        finally
        {
            _busy = false;
            _cts?.Dispose();
            _cts = null;
            SetRunningUi(false);
            UpdateEmptyHint();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void SetRunningUi(bool running)
    {
        ProgressRow.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        Ring.IsActive = running;
        RunBtn.IsEnabled = !running;
        BrowseBtn.IsEnabled = !running;
        DriveBox.IsEnabled = !running;
        SizeBox.IsEnabled = !running;
        PassBox.IsEnabled = !running;
        RunBtn.Content = running ? P("Running…", "測試緊…") : P("Run benchmark", "開始測試");
        if (!running) { ProgressLabel.Text = ""; ProgressBarCtl.Value = 0; }
    }

    private void Warn(string msg) => ShowResult(InfoBarSeverity.Warning, msg, P("Heads up", "注意"));

    private void ShowResult(InfoBarSeverity sev, string msg, string? title = null)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title ?? (sev == InfoBarSeverity.Error ? P("Failed", "失敗")
            : sev == InfoBarSeverity.Success ? P("Done", "完成") : P("Info", "資訊"));
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }
}
