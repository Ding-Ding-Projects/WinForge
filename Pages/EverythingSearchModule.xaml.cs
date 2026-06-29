using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 NTFS 即時檔案搜尋 · Native, Everything (voidtools)-style instant file search — but 100%
/// managed/PInvoke C#. 唔會啟動、shell 或者綁定 Everything 或任何外部搜尋引擎；索引由自己讀
/// NTFS 主檔案表（MFT，經 USN journal）重建，冇管理員權限就退回快速遞迴列舉。Bilingual.
/// This launches/shells/bundles no external search engine. See FileIndexService for the index.
/// </summary>
public sealed partial class EverythingSearchModule : Page
{
    public sealed class Row
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string Size { get; init; } = "";
        public string Modified { get; init; } = "";
        public bool IsDir { get; init; }
        public string Glyph => IsDir ? "" : ""; // folder vs document
    }

    private List<FileIndexService.Entry> _index = new();
    private bool _building;
    private bool _indexReady;
    private CancellationTokenSource? _searchCts;
    private DispatcherTimer? _debounce;

    public EverythingSearchModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) =>
        {
            Render();
            if (!_indexReady && !_building) await BuildIndex();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Instant File Search · 即時檔案搜尋";
        HeaderBlurb.Text = P("Type to find any file or folder by name across your local NTFS drives. The index is built natively (NTFS Master File Table via the USN journal), no external search engine.",
            "打字即搵本機 NTFS 磁碟上嘅任何檔案或資料夾。索引係原生建立（讀 NTFS 主檔案表 MFT，經 USN journal），冇用任何外部搜尋引擎。");
        SearchBox.PlaceholderText = P("Search files…  (use * ? wildcards, or toggle Regex)", "搜尋檔案…（可用 * ? 萬用字元，或切換正規表示式）");
        RegexCheck.Content = P("Regex", "正規表示式");
        RefreshBtn.Content = P("Rebuild index", "重建索引");
        OpenFolderBtn.Content = P("Copy containing path", "複製所在路徑");
        CopyPathBtn.Content = P("Copy path", "複製路徑");
        ColName.Text = P("Name", "名稱");
        ColPath.Text = P("Path", "路徑");
        ColSize.Text = P("Size", "大細");
        ColModified.Text = P("Modified", "修改時間");
    }

    // ===== index build =====

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await BuildIndex();

    private async Task BuildIndex()
    {
        if (_building) return;
        _building = true;
        _indexReady = false;
        RefreshBtn.IsEnabled = false;
        Busy.IsActive = true;
        ModeBar.IsOpen = false;
        List.ItemsSource = null;
        StatusText.Text = P("Building index…", "建立索引緊…");

        var progress = new Progress<string>(s => StatusText.Text = P($"Indexing… {s}", $"索引緊… {s}"));

        FileIndexService.IndexResult res;
        try
        {
            res = await Task.Run(() => FileIndexService.Build(progress, CancellationToken.None));
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            _building = false; RefreshBtn.IsEnabled = true; Busy.IsActive = false;
            return;
        }

        _index = res.Entries;
        _indexReady = true;
        _building = false;
        RefreshBtn.IsEnabled = true;
        Busy.IsActive = false;

        ShowModeBar(res);
        StatusText.Text = P($"Indexed {_index.Count:N0} items. Start typing to search.",
            $"已索引 {_index.Count:N0} 項。開始打字即搵。");

        // Run any query already in the box.
        if (!string.IsNullOrWhiteSpace(SearchBox.Text)) RunSearch();
    }

    private void ShowModeBar(FileIndexService.IndexResult res)
    {
        if (res.Mode == FileIndexService.IndexMode.Mft && res.FallbackRoots.Count == 0)
        {
            ModeBar.Severity = InfoBarSeverity.Success;
            ModeBar.Title = P("Fast MFT mode", "高速 MFT 模式");
            ModeBar.Message = P($"Read the NTFS Master File Table directly for {string.Join(", ", res.Volumes)}.",
                $"已直接讀取 {string.Join("、", res.Volumes)} 嘅 NTFS 主檔案表。");
            ModeBar.IsOpen = true;
        }
        else
        {
            ModeBar.Severity = InfoBarSeverity.Informational;
            ModeBar.Title = P("Directory-scan mode", "目錄掃描模式");
            string detail = res.Volumes.Count > 0
                ? P($"MFT used for {string.Join(", ", res.Volumes)}; other drives scanned recursively. ",
                    $"{string.Join("、", res.Volumes)} 用咗 MFT；其餘磁碟用遞迴掃描。")
                : "";
            ModeBar.Message = detail + P("Run WinForge as Administrator to enable the faster NTFS MFT index for all drives.",
                "以管理員身分執行 WinForge，就可以對所有磁碟啟用更快嘅 NTFS MFT 索引。");
            ModeBar.IsOpen = true;
        }
    }

    // ===== search =====

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Debounce so we don't re-filter the whole index on every keystroke.
        _debounce ??= CreateDebounce();
        _debounce.Stop();
        _debounce.Start();
    }

    private DispatcherTimer CreateDebounce()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
        t.Tick += (_, _) => { t.Stop(); RunSearch(); };
        return t;
    }

    private void Regex_Click(object sender, RoutedEventArgs e) => RunSearch();

    private async void RunSearch()
    {
        if (!_indexReady) return;
        string query = SearchBox.Text ?? "";
        bool useRegex = RegexCheck.IsChecked == true;

        if (string.IsNullOrWhiteSpace(query))
        {
            List.ItemsSource = null;
            StatusText.Text = P($"Indexed {_index.Count:N0} items. Start typing to search.",
                $"已索引 {_index.Count:N0} 項。開始打字即搵。");
            return;
        }

        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        var idx = _index;
        const int limit = 1000;

        List<FileIndexService.Entry> hits;
        try
        {
            hits = await Task.Run(() => FileIndexService.Search(idx, query, useRegex, limit, cts.Token), cts.Token);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { StatusText.Text = ex.Message; return; }

        if (cts.IsCancellationRequested) return;

        var rows = new List<Row>(hits.Count);
        foreach (var h in hits)
        {
            // Size/time are omitted in MFT mode for speed — fetch lazily for the visible hits only.
            string sizeText = FileIndexService.HumanSize(h.Size);
            string modText = h.Modified == default ? "" : h.Modified.ToString("yyyy-MM-dd HH:mm");
            if ((h.Size < 0 || h.Modified == default) && !h.IsDirectory)
            {
                try
                {
                    var fi = new FileInfo(h.Path);
                    if (fi.Exists)
                    {
                        if (h.Size < 0) sizeText = FileIndexService.HumanSize(fi.Length);
                        modText = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                    }
                }
                catch { }
            }

            rows.Add(new Row
            {
                Name = h.Name,
                Path = h.Path,
                Size = h.IsDirectory ? "" : sizeText,
                Modified = modText,
                IsDir = h.IsDirectory,
            });
        }

        List.ItemsSource = rows;
        bool capped = hits.Count >= limit;
        StatusText.Text = capped
            ? P($"Showing first {limit:N0} matches", $"顯示頭 {limit:N0} 個結果")
            : P($"{rows.Count:N0} match(es)", $"{rows.Count:N0} 個結果");

        if (useRegex && rows.Count == 0 && !IsValidRegex(query))
        {
            ResultBar.Severity = InfoBarSeverity.Warning;
            ResultBar.Title = P("Invalid regex", "正規表示式無效");
            ResultBar.Message = P("The regular expression could not be parsed.", "解析唔到呢個正規表示式。");
            ResultBar.IsOpen = true;
        }
        else
        {
            ResultBar.IsOpen = false;
        }
    }

    private static bool IsValidRegex(string pattern)
    {
        try { _ = System.Text.RegularExpressions.Regex.Match("", pattern); return true; }
        catch { return false; }
    }

    // ===== result actions =====

    private void List_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Row r) List.SelectedItem = r;
    }

    private void List_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (List.SelectedItem is Row r) OpenContainingFolder(r);
    }

    private void List_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement fe && fe.DataContext is Row r)
        {
            List.SelectedItem = r;
            var menu = new MenuFlyout();
            var openItem = new MenuFlyoutItem { Text = P("Copy containing path", "複製所在路徑") };
            openItem.Click += (_, _) => OpenContainingFolder(r);
            var copyItem = new MenuFlyoutItem { Text = P("Copy path", "複製路徑") };
            copyItem.Click += (_, _) => CopyPath(r);
            menu.Items.Add(openItem);
            menu.Items.Add(copyItem);
            menu.ShowAt(fe, e.GetPosition(fe));
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is Row r) OpenContainingFolder(r);
        else Warn(P("Select a result first.", "請先揀一個結果。"));
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is Row r) CopyPath(r);
        else Warn(P("Select a result first.", "請先揀一個結果。"));
    }

    /// <summary>
    /// 複製結果所在位置，避免跳去檔案總管 · Copy the containing path without opening Explorer.
    /// </summary>
    private void OpenContainingFolder(Row r)
    {
        try
        {
            var path = r.IsDir ? r.Path : Path.GetDirectoryName(r.Path);
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                Warn(P("That path no longer exists. Rebuild the index.", "呢個路徑已經唔存在。請重建索引。"));
            else
                CopyPath(path);
        }
        catch (Exception ex) { Warn(ex.Message); }
    }

    private void CopyPath(Row r)
        => CopyPath(r.Path);

    private void CopyPath(string path)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(path);
            Clipboard.SetContent(dp);
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Copied", "已複製");
            ResultBar.Message = path;
            ResultBar.IsOpen = true;
        }
        catch (Exception ex) { Warn(ex.Message); }
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
