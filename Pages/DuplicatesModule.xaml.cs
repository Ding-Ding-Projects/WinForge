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
/// 應用程式內重複檔案搜尋 · In-app duplicate finder — content-hash (SHA-256) grouping, one-click
/// recycle of redundant copies. Pure C#, no external tool, no redirect. Bilingual.
/// </summary>
public sealed partial class DuplicatesModule : Page
{
    public sealed class DupRow
    {
        public string Path { get; init; } = "";
        public string Size { get; init; } = "";
        public string Group { get; init; } = "";
        public bool Checked { get; set; }
    }

    private string _folder = "";
    private bool _busy;

    public DuplicatesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) =>
        {
            Render();
            if (string.IsNullOrEmpty(_folder))
            {
                _folder = AppContext.BaseDirectory;
                FolderBox.Text = _folder;
            }
            await DoScan();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Duplicate Finder · 重複檔案搜尋";
        HeaderBlurb.Text = P("Find byte-identical files (size + SHA-256, no false positives), then recycle the redundant copies.",
            "搵出內容完全一樣嘅檔案（用大細 + SHA-256，唔會誤判），再將多餘嘅副本放入回收筒。");
        SrcCap.Text = P("Folder", "資料夾");
        BrowseBtn.Content = P("Browse…", "瀏覽…");
        RecurseCheck.Content = P("Subfolders", "子資料夾");
        ScanBtn.Content = P("Scan", "掃描");
        RecycleBtn.Content = P("Recycle checked", "回收已勾選");
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync();
        if (folder is not null) { _folder = folder; FolderBox.Text = _folder; }
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await DoScan();

    private async Task DoScan()
    {
        if (_busy || string.IsNullOrEmpty(_folder)) return;
        _busy = true;
        ScanBtn.IsEnabled = false;
        StatusText.Text = P("Scanning…", "掃描緊…");
        List.ItemsSource = null;

        bool recursive = RecurseCheck.IsChecked == true;
        var progress = new Progress<int>(n => StatusText.Text = P($"Hashing… {n}", $"計緊雜湊… {n}"));

        List<DupGroup> groups;
        try
        {
            groups = await Task.Run(() => DuplicateFinder.Scan(_folder, recursive, progress, CancellationToken.None));
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            _busy = false; ScanBtn.IsEnabled = true;
            return;
        }

        var rows = new List<DupRow>();
        long wasted = 0;
        int redundant = 0;
        for (int gi = 0; gi < groups.Count; gi++)
        {
            var g = groups[gi];
            wasted += g.Wasted;
            for (int fi = 0; fi < g.Files.Count; fi++)
            {
                bool isKeeper = fi == 0;
                if (!isKeeper) redundant++;
                rows.Add(new DupRow
                {
                    Path = g.Files[fi],
                    Size = DuplicateFinder.HumanSize(g.Size),
                    Group = P($"Group {gi + 1}{(isKeeper ? " (keep)" : "")}", $"組 {gi + 1}{(isKeeper ? "（留）" : "")}"),
                    Checked = !isKeeper, // pre-check the redundant copies
                });
            }
        }

        List.ItemsSource = rows;
        StatusText.Text = P($"{groups.Count} groups · {redundant} redundant · {DuplicateFinder.HumanSize(wasted)} reclaimable",
            $"{groups.Count} 組 · {redundant} 個多餘 · 可回收 {DuplicateFinder.HumanSize(wasted)}");
        _busy = false;
        ScanBtn.IsEnabled = true;
    }

    private async void Recycle_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || List.ItemsSource is not List<DupRow> rows) return;
        var toRecycle = rows.Where(r => r.Checked).Select(r => r.Path).ToList();
        if (toRecycle.Count == 0) { Warn(P("Nothing checked.", "冇勾選嘢。")); return; }

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Recycle duplicates?", "回收重複檔案？"),
            Content = P($"Send {toRecycle.Count} file(s) to the Recycle Bin?", $"將 {toRecycle.Count} 個檔案放入回收筒？"),
            PrimaryButtonText = P("Recycle", "回收"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var (ok, fail) = BulkFileOps.Recycle(toRecycle);
        ResultBar.Severity = fail == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Title = P("Done", "完成");
        ResultBar.Message = P($"Recycled {ok}, failed {fail}.", $"已回收 {ok}，失敗 {fail}。");
        ResultBar.IsOpen = true;
        await DoScan();
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
