using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 檔案鎖偵測 · File Locksmith — 一個 PowerToys FileLocksmith 嘅原生複製品。揀一個檔案或者資料夾
/// （或者打字輸入路徑），然後用 Windows 重新啟動管理員 API 列出所有鎖住佢嘅程序：程序名、PID、
/// 使用者、鎖住幾多個檔案、程式路徑、啟動時間。每行都可以「結束工作」或者「開啟檔案位置」。
/// A native clone of PowerToys FileLocksmith: pick a file or folder (or type a path) and the
/// Windows Restart Manager API lists every process holding a handle — name, PID, user, locked-file
/// count, image path, start time — with per-row End task and Open file location. Bilingual.
/// </summary>
public sealed partial class FileLocksmithModule : Page
{
    /// <summary>UI 用嘅一行 · A presentation row for one locking process.</summary>
    public sealed class Row
    {
        public string Name { get; }
        public int Pid { get; }
        public string User { get; }
        public int FileCount { get; }
        public string Path { get; }
        public string AppType { get; }
        public string StartedText { get; }
        public List<string> Files { get; }

        public Row(LockingProcess p, Func<string, string, string> pick)
        {
            Name = p.Name;
            Pid = p.Pid;
            User = string.IsNullOrEmpty(p.User) ? pick("(unknown)", "（未知）") : p.User;
            FileCount = p.FileCount;
            Path = string.IsNullOrEmpty(p.Path)
                ? (p.Restricted ? pick("(needs admin to read path)", "（要管理員權限先讀到路徑）") : "")
                : p.Path;
            AppType = p.AppType;
            Files = p.Files;
            StartedText = p.Started is { } dt
                ? pick($"Started {dt:yyyy-MM-dd HH:mm:ss}", $"於 {dt:yyyy-MM-dd HH:mm:ss} 啟動")
                : "";
        }
    }

    private bool _busy;
    private string? _lastPath;
    private CancellationTokenSource? _cts;

    public FileLocksmithModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { try { _cts?.Cancel(); } catch { } };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "File Locksmith · 檔案鎖偵測";
        HeaderBlurb.Text = P(
            "Find out which process is locking a file or folder. Powered by the Windows Restart Manager — pick a target (or type a path), then end the offending task or open its location.",
            "查出邊個程序鎖住緊一個檔案或者資料夾。用 Windows 重新啟動管理員 API — 揀一個目標（或者打字輸入路徑），然後可以結束嗰個程序或者開啟佢嘅位置。");
        PathBox.PlaceholderText = P("Path to a file or folder…", "檔案或資料夾路徑…");
        PickFileText.Text = P("Pick file", "揀檔案");
        PickFolderText.Text = P("Pick folder", "揀資料夾");
        ScanText.Text = P("Scan", "掃描");
        RefreshText.Text = P("Refresh", "重新整理");
        ColProc.Text = P("Process", "程序");
        ColPid.Text = P("PID", "PID");
        ColUser.Text = P("User", "使用者");
        ColFiles.Text = P("Files", "檔案");
        ColActions.Text = P("Actions", "操作");
        ElevateBtn.Content = P("Restart as admin", "以管理員身分重開");
        FooterNote.Text = P(
            "Some handles (other users' processes, services, or protected system processes) are only fully visible when WinForge runs as administrator. Folder scans enumerate up to 4000 files.",
            "有啲鎖（其他使用者嘅程序、服務、或者受保護嘅系統程序）要 WinForge 以管理員身分執行先睇得晒。資料夾掃描最多枚舉 4000 個檔案。");

        if (!AdminHelper.IsElevated && _lastPath is null)
        {
            // 一開始就溫和提示一次可以以管理員身分執行 · A gentle one-time elevation hint on first load.
            ElevationBar.Severity = InfoBarSeverity.Informational;
            ElevationBar.Title = P("Tip", "提示");
            ElevationBar.Message = P(
                "Run WinForge as administrator to reveal locks held by other users or system processes.",
                "以管理員身分執行 WinForge，先可以見到其他使用者或者系統程序持有嘅鎖。");
            ElevationBar.IsOpen = true;
        }
    }

    // ===== Pickers =====

    private async void PickFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync((IEnumerable<FileDialogs.Filter>?)null, P("Pick a file to inspect", "揀一個檔案檢查"));
        if (!string.IsNullOrEmpty(path))
        {
            PathBox.Text = path;
            await DoScan(path);
        }
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFolderAsync(P("Pick a folder to inspect", "揀一個資料夾檢查"));
        if (!string.IsNullOrEmpty(path))
        {
            PathBox.Text = path;
            await DoScan(path);
        }
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        var path = (PathBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(path))
        {
            Warn(P("Type or pick a file/folder path first.", "請先輸入或者揀一個檔案／資料夾路徑。"));
            return;
        }
        await DoScan(path);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_lastPath is { } p) await DoScan(p);
    }

    private void Elevate_Click(object sender, RoutedEventArgs e)
    {
        if (AdminHelper.RelaunchElevated())
            Application.Current.Exit();
        else
            Warn(P("Could not relaunch as administrator (UAC declined?).", "無法以管理員身分重開（UAC 被拒？）。"));
    }

    // ===== Scan =====

    private async Task DoScan(string path)
    {
        if (_busy) return;
        _busy = true;
        _lastPath = path;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        ScanBtn.IsEnabled = false;
        RefreshBtn.IsEnabled = false;
        Busy.IsActive = true;
        CountText.Text = P("Scanning…", "掃描緊…");
        List.ItemsSource = null;
        EmptyText.Visibility = Visibility.Collapsed;

        try
        {
            var result = await FileLocksmithService.ScanAsync(path, ct);
            if (ct.IsCancellationRequested) return;

            if (result.Error is not null)
            {
                CountText.Text = "";
                ShowEmpty(P(result.Error, result.ErrorZh ?? result.Error));
                ResultBar.Severity = InfoBarSeverity.Error;
                ResultBar.Title = P("Scan failed", "掃描失敗");
                ResultBar.Message = P(result.Error, result.ErrorZh ?? result.Error);
                ResultBar.IsOpen = true;
                return;
            }

            var rows = result.Processes.Select(p => new Row(p, P)).ToList();
            List.ItemsSource = rows;

            CountText.Text = rows.Count == 0
                ? P($"Nothing is locking this — scanned {result.FilesScanned} file(s).",
                    $"冇嘢鎖住緊 — 掃描咗 {result.FilesScanned} 個檔案。")
                : P($"{rows.Count} process(es) locking it — scanned {result.FilesScanned} file(s).",
                    $"{rows.Count} 個程序鎖住緊 — 掃描咗 {result.FilesScanned} 個檔案。");

            if (rows.Count == 0)
                ShowEmpty(P("Nothing is holding a handle on this path. It's free to move, rename or delete.",
                    "冇任何程序持有呢條路徑嘅控制代碼。可以自由移動、改名或者刪除。"));
            else
                EmptyText.Visibility = Visibility.Collapsed;

            // 權限提示 · Elevation hint when locks may be partly hidden.
            if (result.NeedsElevationHint)
            {
                ElevationBar.Severity = InfoBarSeverity.Warning;
                ElevationBar.Title = P("Some details need administrator rights", "部分資料需要管理員權限");
                ElevationBar.Message = P(
                    "One or more processes belong to another user or are protected. Restart WinForge as administrator to see their full details and to end them.",
                    "有一個或者多個程序屬於其他使用者或者受保護。以管理員身分重開 WinForge 先可以睇晒佢哋嘅資料同埋結束佢哋。");
                ElevationBar.IsOpen = true;
            }
            else if (AdminHelper.IsElevated)
            {
                ElevationBar.IsOpen = false;
            }
        }
        catch (OperationCanceledException) { /* superseded by a newer scan */ }
        catch (Exception ex)
        {
            ShowEmpty(ex.Message);
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Scan failed", "掃描失敗");
            ResultBar.Message = ex.Message;
            ResultBar.IsOpen = true;
        }
        finally
        {
            _busy = false;
            Busy.IsActive = false;
            ScanBtn.IsEnabled = true;
            RefreshBtn.IsEnabled = _lastPath is not null;
        }
    }

    private void ShowEmpty(string msg)
    {
        EmptyText.Text = msg;
        EmptyText.Visibility = Visibility.Visible;
    }

    // ===== Row actions =====

    private void OpenLoc_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Row r) return;
        // 優先開程式可執行檔嘅位置；若無，就退回去鎖住嘅第一個檔案 ·
        // Prefer the process image's location; fall back to the first locked file.
        var target = !string.IsNullOrEmpty(r.Path) && System.IO.File.Exists(r.Path)
            ? r.Path
            : r.Files.FirstOrDefault();
        if (string.IsNullOrEmpty(target))
        {
            Warn(P("No location available for this process.", "呢個程序冇可用嘅位置。"));
            return;
        }
        if (!FileLocksmithService.OpenLocation(target))
            Warn(P("Could not open the location.", "開唔到位置。"));
    }

    private async void Files_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Row r) return;
        var body = r.Files.Count == 0
            ? P("(folder lock — no individual file)", "（資料夾鎖 — 冇個別檔案）")
            : string.Join(Environment.NewLine, r.Files.Take(500));
        if (r.Files.Count > 500)
            body += Environment.NewLine + P($"… and {r.Files.Count - 500} more", $"… 仲有 {r.Files.Count - 500} 個");

        var scroll = new ScrollViewer
        {
            MaxHeight = 360,
            Content = new TextBlock
            {
                Text = body,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.Wrap,
            },
        };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Files locked by {r.Name} ({r.FileCount})", $"{r.Name} 鎖住嘅檔案（{r.FileCount}）"),
            Content = scroll,
            CloseButtonText = P("Close", "關閉"),
        };
        await dlg.ShowAsync();
    }

    private async void EndTask_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Row r) return;
        if (r.Pid <= 4)
        {
            Warn(P("This is a core system process and cannot be ended.", "呢個係核心系統程序，唔可以結束。"));
            return;
        }

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("End this process?", "結束呢個程序？"),
            Content = P(
                $"End “{r.Name}” (PID {r.Pid})? Any unsaved work in that program will be lost, and the file lock will be released.",
                $"結束「{r.Name}」（PID {r.Pid}）？嗰個程式入面任何未儲存嘅嘢都會冇咗，而且會釋放檔案鎖。"),
            PrimaryButtonText = P("End task", "結束工作"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var (ok, accessDenied) = FileLocksmithService.EndProcess(r.Pid);
        if (ok)
        {
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Process ended", "已結束程序");
            ResultBar.Message = $"{r.Name} · PID {r.Pid}";
            ResultBar.IsOpen = true;
        }
        else
        {
            ResultBar.Severity = InfoBarSeverity.Warning;
            ResultBar.Title = P("Could not end it", "結束唔到");
            ResultBar.Message = accessDenied && !AdminHelper.IsElevated
                ? P("Access denied — restart WinForge as administrator to end this process.",
                    "拒絕存取 — 以管理員身分重開 WinForge 先可以結束呢個程序。")
                : P("The process may have already exited or is protected.",
                    "程序可能已經結束，或者受保護。");
            ResultBar.IsOpen = true;
            if (accessDenied && !AdminHelper.IsElevated)
            {
                ElevationBar.Severity = InfoBarSeverity.Warning;
                ElevationBar.Title = P("Administrator rights needed", "需要管理員權限");
                ElevationBar.Message = P("Restart WinForge as administrator to end protected processes.",
                    "以管理員身分重開 WinForge 先可以結束受保護嘅程序。");
                ElevationBar.IsOpen = true;
            }
        }

        // 自動重新掃描 · auto-refresh after acting.
        if (_lastPath is { } p) await DoScan(p);
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Notice", "提示");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
