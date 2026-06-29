using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生檔案及資料夾比對合併模組 · Native, pure-C# WinMerge-style diff/merge.
/// Side-by-side line diff (Myers O(ND)) with word-level highlighting and synchronized
/// scrolling; 2-way merge (copy a changed line left↔right) with save-back; and a recursive
/// folder comparison (size + content hash) whose differing pairs open in the text diff.
/// 全部用受管理 C# 計算，唔會啟動／呼叫任何外部 diff 工具。
/// </summary>
public sealed partial class DiffMergeModule : Page
{
    private string? _leftPath, _rightPath;
    private string? _leftDir, _rightDir;
    private List<string> _leftLines = new();
    private List<string> _rightLines = new();
    private List<DiffService.DiffRow> _rows = new();
    private readonly List<int> _diffIndices = new(); // row indices that are changes (for next/prev)
    private int _navPos = -1;
    private bool _ignoreWs;
    private CancellationTokenSource? _dirCts;
    private readonly ObservableCollection<FolderRowVM> _folderRows = new();
    private List<DiffService.FolderItem> _folderItems = new();
    private string _dirFilter = "all";
    private bool _suppressDirFilter;
    private bool _dirty; // any merge applied that isn't saved

    public DiffMergeModule()
    {
        InitializeComponent();
        FolderList.ItemsSource = _folderRows;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { BuildDirFilterCombo(); Render(); UpdateTextEmpty(); UpdateFolderEmpty(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "File & Folder Diff/Merge · 檔案及資料夾比對合併";
        HeaderBlurb.Text = P(
            "A native, in-app diff & merge — pick two files for a colour-coded side-by-side line diff with word highlighting, copy changed lines left↔right and save each side back, or compare two folders recursively and open any differing pair. Pure managed C#, no external tools.",
            "原生、app 內嘅比對及合併 — 揀兩個檔案做彩色並排逐行比對（含逐字標示），將改動嘅行左右互抄再各自存檔，又或者遞迴比對兩個資料夾、雙擊任何唔同嘅檔案對開比對。全部純受管理 C#，唔使任何外部工具。");

        TextPivot.Header = P("Text Diff & Merge · 文字比對合併", "文字比對合併");
        FolderPivot.Header = P("Folder Compare · 資料夾比對", "資料夾比對");

        LeftPathBox.Header = P("Left file · 左邊檔案", "左邊檔案");
        RightPathBox.Header = P("Right file · 右邊檔案", "右邊檔案");
        RecomputeLbl.Text = P("Compare · 比對", "比對");
        IgnoreWsChk.Content = P("Ignore whitespace · 忽略空白", "忽略空白");
        SaveLeftLbl.Text = P("Save left · 存左邊", "存左邊");
        SaveRightLbl.Text = P("Save right · 存右邊", "存右邊");
        TextEmptyLbl.Text = P("Pick a left and right file, then Compare to see a side-by-side diff.",
            "揀左、右兩個檔案，再撳「比對」就會見到並排差異。");

        LeftDirBox.Header = P("Left folder · 左邊資料夾", "左邊資料夾");
        RightDirBox.Header = P("Right folder · 右邊資料夾", "右邊資料夾");
        CompareDirLbl.Text = P("Compare · 比對", "比對");
        FolderEmptyLbl.Text = P("Pick two folders, then Compare. Double-click a differing file to open it in the text diff.",
            "揀兩個資料夾再撳「比對」。雙擊唔同嘅檔案就會喺文字比對打開。");
        ColName.Text = P("Item · 項目", "項目");
        ColStatus.Text = P("Status · 狀態", "狀態");
        ColLeftSize.Text = P("Left · 左", "左");
        ColRightSize.Text = P("Right · 右", "右");

        BuildDirFilterCombo();
        UpdateColHeaders();
        UpdateDiffSummary();
        foreach (var r in _folderRows) r.Refresh();
    }

    private void UpdateColHeaders()
    {
        LeftColHdr.Text = _leftPath is null ? P("Left · 左邊", "左邊") : Path.GetFileName(_leftPath);
        RightColHdr.Text = _rightPath is null ? P("Right · 右邊", "右邊") : Path.GetFileName(_rightPath);
    }

    // ── Brushes for diff rows · 差異行底色 ───────────────────────────────────────

    private static SolidColorBrush AddBrush => new(Color.FromArgb(46, 0x3F, 0xB9, 0x50));   // green
    private static SolidColorBrush DelBrush => new(Color.FromArgb(46, 0xE8, 0x11, 0x23));   // red
    private static SolidColorBrush ModBrush => new(Color.FromArgb(46, 0xE3, 0xA2, 0x1A));   // amber
    private static SolidColorBrush GutterBrush => new(Color.FromArgb(20, 0x80, 0x80, 0x80));

    // ── Text file pickers · 揀檔案 ───────────────────────────────────────────────

    private async void PickLeft_Click(object sender, RoutedEventArgs e)
    {
        var p = await FileDialogs.OpenFileAsync();
        if (p is not null) { _leftPath = p; LeftPathBox.Text = p; UpdateColHeaders(); await TryAutoCompute(); }
    }

    private async void PickRight_Click(object sender, RoutedEventArgs e)
    {
        var p = await FileDialogs.OpenFileAsync();
        if (p is not null) { _rightPath = p; RightPathBox.Text = p; UpdateColHeaders(); await TryAutoCompute(); }
    }

    private async void Swap_Click(object sender, RoutedEventArgs e)
    {
        (_leftPath, _rightPath) = (_rightPath, _leftPath);
        LeftPathBox.Text = _leftPath ?? ""; RightPathBox.Text = _rightPath ?? "";
        UpdateColHeaders();
        await TryAutoCompute();
    }

    private void IgnoreWs_Click(object sender, RoutedEventArgs e)
    {
        _ignoreWs = IgnoreWsChk.IsChecked == true;
        if (_leftPath is not null && _rightPath is not null) Recompute();
    }

    private async Task TryAutoCompute()
    {
        if (_leftPath is not null && _rightPath is not null) { await Task.Yield(); Recompute(); }
    }

    private void Recompute_Click(object sender, RoutedEventArgs e) => Recompute();

    private void Recompute()
    {
        if (_leftPath is null || _rightPath is null)
        {
            ShowText(InfoBarSeverity.Warning, P("Pick both a left and a right file first.", "請先揀左、右兩個檔案。"));
            return;
        }
        if (DiffService.LooksBinary(_leftPath) || DiffService.LooksBinary(_rightPath))
            ShowText(InfoBarSeverity.Warning, P("One of the files looks binary; showing a best-effort text diff.",
                "其中一個檔案似乎係二進位；只能盡力顯示文字差異。"));
        else TextInfo.IsOpen = false;

        _leftLines = DiffService.SplitLines(DiffService.ReadText(_leftPath)).ToList();
        _rightLines = DiffService.SplitLines(DiffService.ReadText(_rightPath)).ToList();
        _dirty = false;
        BuildDiff();
    }

    private void BuildDiff()
    {
        _rows = DiffService.DiffLines(_leftLines, _rightLines, _ignoreWs);
        RebuildDiffList();
    }

    private void RebuildDiffList()
    {
        DiffList.Items.Clear();
        _diffIndices.Clear();
        _navPos = -1;
        for (int idx = 0; idx < _rows.Count; idx++)
        {
            var row = _rows[idx];
            if (row.Changed) _diffIndices.Add(idx);
            DiffList.Items.Add(BuildRowControl(idx, row));
        }
        UpdateColHeaders();
        UpdateDiffSummary();
        UpdateTextEmpty();
    }

    /// <summary>
    /// 砌一行並排控件（行號、左／右文字、合併按鈕）· Build the visual for one aligned diff row,
    /// including a centre gutter that carries the copy-left / copy-right merge buttons.
    /// </summary>
    private FrameworkElement BuildRowControl(int index, DiffService.DiffRow row)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        Brush? leftBg = row.Kind switch
        {
            DiffService.LineKind.Delete => DelBrush,
            DiffService.LineKind.Modify => ModBrush,
            _ => null,
        };
        Brush? rightBg = row.Kind switch
        {
            DiffService.LineKind.Insert => AddBrush,
            DiffService.LineKind.Modify => ModBrush,
            _ => null,
        };
        if (row.Kind == DiffService.LineKind.Delete) rightBg = GutterBrush;   // gap on right
        if (row.Kind == DiffService.LineKind.Insert) leftBg = GutterBrush;    // gap on left

        // Word-level highlighting only makes sense for Modify rows with both sides present.
        List<DiffService.Span>? lSpans = null, rSpans = null;
        if (row.Kind == DiffService.LineKind.Modify && row.Left is not null && row.Right is not null)
            (lSpans, rSpans) = DiffService.DiffWords(row.Left, row.Right);

        grid.Children.Add(SideCell(row.LeftNo, row.Left, leftBg, lSpans, 0));
        grid.Children.Add(SideCell(row.RightNo, row.Right, rightBg, rSpans, 2));

        // Centre gutter: merge buttons for changed rows.
        var gutter = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 2,
        };
        Grid.SetColumn(gutter, 1);
        if (row.Changed)
        {
            var toRight = new Button
            {
                Padding = new Thickness(2),
                MinWidth = 0, MinHeight = 0,
                Content = new FontIcon { FontSize = 11, Glyph = "" }, // →
                Tag = index,
            };
            ToolTipService.SetToolTip(toRight, P("Copy line to right · 抄去右邊", "抄去右邊"));
            toRight.Click += (_, _) => ApplyMerge(index, toLeft: false);

            var toLeft = new Button
            {
                Padding = new Thickness(2),
                MinWidth = 0, MinHeight = 0,
                Content = new FontIcon { FontSize = 11, Glyph = "" }, // ←
                Tag = index,
            };
            ToolTipService.SetToolTip(toLeft, P("Copy line to left · 抄去左邊", "抄去左邊"));
            toLeft.Click += (_, _) => ApplyMerge(index, toLeft: true);

            gutter.Children.Add(toLeft);
            gutter.Children.Add(toRight);
        }
        grid.Children.Add(gutter);

        return grid;
    }

    private Border SideCell(int lineNo, string? text, Brush? bg, List<DiffService.Span>? spans, int column)
    {
        var inner = new Grid();
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var num = new TextBlock
        {
            Text = lineNo > 0 ? lineNo.ToString() : "",
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextAlignment = TextAlignment.Right,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(num, 0);
        inner.Children.Add(num);

        var body = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12.5,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            IsTextSelectionEnabled = true,
        };
        if (spans is not null)
        {
            foreach (var sp in spans)
            {
                var run = new Run { Text = sp.Text };
                if (sp.Changed)
                {
                    // WinUI Run has no Background; emulate intra-line highlight with bold + accent colour.
                    run.Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
                    run.FontWeight = Microsoft.UI.Text.FontWeights.Bold;
                }
                body.Inlines.Add(run);
            }
        }
        else
        {
            body.Text = text ?? "";
        }
        Grid.SetColumn(body, 1);
        inner.Children.Add(body);

        return new Border
        {
            Background = bg ?? (Brush?)null,
            Padding = new Thickness(6, 2, 6, 2),
            Child = inner,
            Margin = column == 0 ? new Thickness(0) : new Thickness(0),
        };
    }

    // ── Merge (2-way) · 雙向合併 ─────────────────────────────────────────────────

    private void ApplyMerge(int rowIndex, bool toLeft)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count) return;
        var row = _rows[rowIndex];

        // Re-derive the diff from current line lists after mutating, to keep numbering correct.
        // We map row → underlying line indices via LeftNo/RightNo (1-based).
        if (toLeft)
        {
            // Bring the RIGHT content onto the LEFT side.
            if (row.Kind == DiffService.LineKind.Insert)
            {
                // Right has a line the left lacks → insert it on the left at the right spot.
                int insertAt = NearestLeftIndex(rowIndex);
                _leftLines.Insert(insertAt, row.Right ?? "");
            }
            else if (row.Kind == DiffService.LineKind.Delete)
            {
                // Left has an extra line not on the right → remove it from the left.
                int li = row.LeftNo - 1;
                if (li >= 0 && li < _leftLines.Count) _leftLines.RemoveAt(li);
            }
            else if (row.Kind == DiffService.LineKind.Modify)
            {
                int li = row.LeftNo - 1;
                if (li >= 0 && li < _leftLines.Count) _leftLines[li] = row.Right ?? "";
            }
        }
        else
        {
            // Bring the LEFT content onto the RIGHT side.
            if (row.Kind == DiffService.LineKind.Delete)
            {
                int insertAt = NearestRightIndex(rowIndex);
                _rightLines.Insert(insertAt, row.Left ?? "");
            }
            else if (row.Kind == DiffService.LineKind.Insert)
            {
                int ri = row.RightNo - 1;
                if (ri >= 0 && ri < _rightLines.Count) _rightLines.RemoveAt(ri);
            }
            else if (row.Kind == DiffService.LineKind.Modify)
            {
                int ri = row.RightNo - 1;
                if (ri >= 0 && ri < _rightLines.Count) _rightLines[ri] = row.Left ?? "";
            }
        }

        _dirty = true;
        BuildDiff();
        ShowText(InfoBarSeverity.Informational,
            P("Line merged. Use Save left / Save right to write changes to disk.",
              "已合併呢一行。用「存左邊／存右邊」寫返落磁碟。"));
    }

    /// <summary>搵合併插入時，左邊應該插入嘅位置 · Where to insert on the left for a right-only row.</summary>
    private int NearestLeftIndex(int rowIndex)
    {
        for (int i = rowIndex - 1; i >= 0; i--)
            if (_rows[i].LeftNo > 0) return _rows[i].LeftNo; // after that line
        return 0;
    }

    private int NearestRightIndex(int rowIndex)
    {
        for (int i = rowIndex - 1; i >= 0; i--)
            if (_rows[i].RightNo > 0) return _rows[i].RightNo;
        return 0;
    }

    private async void SaveLeft_Click(object sender, RoutedEventArgs e) => await SaveSide(true);
    private async void SaveRight_Click(object sender, RoutedEventArgs e) => await SaveSide(false);

    private async Task SaveSide(bool left)
    {
        var path = left ? _leftPath : _rightPath;
        var lines = left ? _leftLines : _rightLines;
        if (path is null) { ShowText(InfoBarSeverity.Warning, P("No file on that side to save.", "嗰一邊冇檔案可以儲存。")); return; }
        try
        {
            // Preserve a trailing newline convention: join with the platform newline.
            var text = string.Join(Environment.NewLine, lines);
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
            ShowText(InfoBarSeverity.Success, P($"Saved {Path.GetFileName(path)}.", $"已儲存 {Path.GetFileName(path)}。"));
        }
        catch (Exception ex)
        {
            ShowText(InfoBarSeverity.Error, P($"Save failed: {ex.Message}", $"儲存失敗：{ex.Message}"));
        }
    }

    // ── Next / prev difference · 下一處／上一處差異 ───────────────────────────────

    private void NextDiff_Click(object sender, RoutedEventArgs e) => NavDiff(+1);
    private void PrevDiff_Click(object sender, RoutedEventArgs e) => NavDiff(-1);

    private void NavDiff(int dir)
    {
        if (_diffIndices.Count == 0) return;
        _navPos += dir;
        if (_navPos < 0) _navPos = _diffIndices.Count - 1;
        if (_navPos >= _diffIndices.Count) _navPos = 0;
        int target = _diffIndices[_navPos];
        if (target >= 0 && target < DiffList.Items.Count)
        {
            DiffList.SelectedIndex = target;
            DiffList.ScrollIntoView(DiffList.Items[target], ScrollIntoViewAlignment.Leading);
        }
    }

    private void UpdateDiffSummary()
    {
        if (_rows.Count == 0) { DiffSummary.Text = ""; return; }
        int add = _rows.Count(r => r.Kind == DiffService.LineKind.Insert);
        int del = _rows.Count(r => r.Kind == DiffService.LineKind.Delete);
        int mod = _rows.Count(r => r.Kind == DiffService.LineKind.Modify);
        DiffSummary.Text = P($"{mod} changed · {add} added · {del} removed{(_dirty ? "  (unsaved)" : "")}",
            $"{mod} 改動 · {add} 新增 · {del} 刪除{(_dirty ? "（未儲存）" : "")}");
    }

    private void UpdateTextEmpty()
    {
        bool empty = DiffList.Items.Count == 0;
        TextEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        DiffList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ShowText(InfoBarSeverity sev, string msg)
    {
        TextInfo.Severity = sev; TextInfo.Message = msg; TextInfo.IsOpen = true;
    }

    // ── Folder compare · 資料夾比對 ───────────────────────────────────────────────

    private async void PickLeftDir_Click(object sender, RoutedEventArgs e)
    {
        var p = await FileDialogs.OpenFolderAsync();
        if (p is not null) { _leftDir = p; LeftDirBox.Text = p; }
    }

    private async void PickRightDir_Click(object sender, RoutedEventArgs e)
    {
        var p = await FileDialogs.OpenFolderAsync();
        if (p is not null) { _rightDir = p; RightDirBox.Text = p; }
    }

    private void SwapDir_Click(object sender, RoutedEventArgs e)
    {
        (_leftDir, _rightDir) = (_rightDir, _leftDir);
        LeftDirBox.Text = _leftDir ?? ""; RightDirBox.Text = _rightDir ?? "";
        if (_folderItems.Count > 0) _ = RunFolderCompare();
    }

    private void BuildDirFilterCombo()
    {
        _suppressDirFilter = true;
        int sel = DirFilterBox.SelectedIndex < 0 ? 0 : DirFilterBox.SelectedIndex;
        DirFilterBox.Items.Clear();
        var filters = new (string code, string en, string zh)[]
        {
            ("all", "All items", "全部項目"),
            ("diff", "Differences only", "只顯示差異"),
            ("different", "Different", "內容不同"),
            ("left", "Only left", "只在左邊"),
            ("right", "Only right", "只在右邊"),
            ("identical", "Identical", "相同"),
        };
        foreach (var f in filters) DirFilterBox.Items.Add(new ComboBoxItem { Content = P($"{f.en} · {f.zh}", f.zh), Tag = f.code });
        DirFilterBox.SelectedIndex = Math.Min(sel, DirFilterBox.Items.Count - 1);
        _suppressDirFilter = false;
    }

    private void DirFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressDirFilter) return;
        _dirFilter = (DirFilterBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "all";
        ApplyDirFilter();
    }

    private async void CompareDir_Click(object sender, RoutedEventArgs e) => await RunFolderCompare();

    private async Task RunFolderCompare()
    {
        if (string.IsNullOrEmpty(_leftDir) || string.IsNullOrEmpty(_rightDir))
        {
            ShowDir(InfoBarSeverity.Warning, P("Pick both a left and a right folder first.", "請先揀左、右兩個資料夾。"));
            return;
        }
        DirInfo.IsOpen = false;
        _dirCts?.Cancel();
        _dirCts = new CancellationTokenSource();
        var ct = _dirCts.Token;
        DirBusy.IsActive = true;
        CompareDirBtn.IsEnabled = false;
        try
        {
            _folderItems = await DiffService.CompareFoldersAsync(_leftDir!, _rightDir!, _ignoreWs, ct);
            ApplyDirFilter();
            int diff = _folderItems.Count(i => i.Status != DiffService.ItemStatus.Identical);
            DirSummary.Text = P($"{_folderItems.Count} items · {diff} differ",
                $"{_folderItems.Count} 個項目 · {diff} 個有差異");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowDir(InfoBarSeverity.Error, ex.Message); }
        finally { DirBusy.IsActive = false; CompareDirBtn.IsEnabled = true; }
    }

    private void ApplyDirFilter()
    {
        _folderRows.Clear();
        foreach (var it in _folderItems)
        {
            bool show = _dirFilter switch
            {
                "all" => true,
                "diff" => it.Status != DiffService.ItemStatus.Identical,
                "different" => it.Status == DiffService.ItemStatus.Different,
                "left" => it.Status == DiffService.ItemStatus.OnlyLeft,
                "right" => it.Status == DiffService.ItemStatus.OnlyRight,
                "identical" => it.Status == DiffService.ItemStatus.Identical,
                _ => true,
            };
            if (show) _folderRows.Add(new FolderRowVM(it));
        }
        UpdateFolderEmpty();
    }

    private void UpdateFolderEmpty()
    {
        bool empty = _folderRows.Count == 0;
        FolderEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        FolderList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void FolderList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (FolderList.SelectedItem is not FolderRowVM vm) return;
        var it = vm.Item;
        if (it.IsDirectory) return;
        if (it.Status == DiffService.ItemStatus.OnlyLeft || it.Status == DiffService.ItemStatus.OnlyRight)
        {
            ShowDir(InfoBarSeverity.Informational, P("This file exists on only one side — nothing to diff.",
                "呢個檔案只係喺一邊有 — 冇嘢可以比對。"));
            return;
        }
        if (it.LeftPath is null || it.RightPath is null) return;
        // Load the pair into the text-diff view and switch to that tab.
        _leftPath = it.LeftPath; _rightPath = it.RightPath;
        LeftPathBox.Text = _leftPath; RightPathBox.Text = _rightPath;
        UpdateColHeaders();
        Recompute();
        RootPivot.SelectedItem = TextPivot;
    }

    private void ShowDir(InfoBarSeverity sev, string msg)
    {
        DirInfo.Severity = sev; DirInfo.Message = msg; DirInfo.IsOpen = true;
    }
}

/// <summary>資料夾比對一行嘅檢視模型 · View-model for one folder-compare row.</summary>
public sealed class FolderRowVM
{
    public DiffService.FolderItem Item { get; }
    public FolderRowVM(DiffService.FolderItem item) { Item = item; }

    public string Display => Item.RelativePath;
    public string Glyph => Item.IsDirectory ? "" : "";

    public string StatusText
    {
        get
        {
            var (en, zh) = Item.Status switch
            {
                DiffService.ItemStatus.Identical => ("Identical", "相同"),
                DiffService.ItemStatus.Different => ("Different", "內容不同"),
                DiffService.ItemStatus.OnlyLeft => ("Only left", "只在左邊"),
                _ => ("Only right", "只在右邊"),
            };
            return Loc.I.Pick($"{en} · {zh}", zh);
        }
    }

    public Brush StatusBrush
    {
        get
        {
            var key = Item.Status switch
            {
                DiffService.ItemStatus.Identical => "TextFillColorSecondaryBrush",
                DiffService.ItemStatus.Different => "SystemFillColorCautionBrush",
                DiffService.ItemStatus.OnlyLeft => "SystemFillColorCriticalBrush",
                _ => "SystemFillColorSuccessBrush",
            };
            return (Brush)Application.Current.Resources[key];
        }
    }

    public string LeftSizeText => Item.IsDirectory || Item.LeftPath is null ? "" : DiffService.HumanSize(Item.LeftSize);
    public string RightSizeText => Item.IsDirectory || Item.RightPath is null ? "" : DiffService.HumanSize(Item.RightSize);

    /// <summary>語言切換後重畫（ListView 會重新讀屬性）· No-op hook for re-render on language change.</summary>
    public void Refresh() { }
}
