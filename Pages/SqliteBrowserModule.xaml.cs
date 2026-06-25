using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 SQLite 資料庫瀏覽器（DB Browser for SQLite 風格）· A native "DB Browser for SQLite"-style module.
/// 三個分頁：結構描述（表／檢視／索引樹 + 欄位）、瀏覽資料（分頁 + 可編輯／插入／刪除）、執行 SQL（編輯器 + 結果 + 錯誤）。
/// CSV 匯出（表或查詢結果）。全部雙語（粵語 + English）。
/// Three tabs: Database Structure (tables/views/indexes tree + columns), Browse Data (paged, editable
/// cells, insert/delete row), and Execute SQL (editor + result grid + errors). CSV export.
/// MANAGED ENGINE: built entirely on Microsoft.Data.Sqlite, which bundles the SQLite native library via
/// SQLitePCLRaw. No external tool (DB Browser, sqlite3.exe) is launched, shelled, or bundled.
/// </summary>
public sealed partial class SqliteBrowserModule : Page
{
    private string? _dbPath;
    private CancellationTokenSource? _queryCts;

    // Browse-data state
    private string? _browseTable;
    private int _pageIndex;
    private int _pageSize = 50;
    private SqlitePage? _page;
    private readonly Dictionary<(int row, int col), TextBox> _cellBoxes = new();
    private int _selectedRow = -1;

    // SQL result state
    private SqliteQueryResult? _lastResult;

    public SqliteBrowserModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; _queryCts?.Cancel(); };
        Loaded += (_, _) => Render();
    }

    private void OnLang(object? sender, EventArgs e) => Render();
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "SQLite Browser · SQLite 資料庫瀏覽器";
        HeaderBlurb.Text = P(
            "A native SQLite database browser: open or create a .db file, inspect the structure, browse and edit data, run SQL, and export to CSV — all in pure managed C#.",
            "原生 SQLite 資料庫瀏覽器：開啟或建立 .db 檔、檢視結構、瀏覽同編輯資料、跑 SQL、匯出 CSV — 全部純託管 C#。");

        EngineBar.Title = P("Managed SQLite engine", "託管 SQLite 引擎");
        EngineBar.Message = P(
            "Powered by Microsoft.Data.Sqlite (bundled SQLite via SQLitePCLRaw). No external app is launched — this is a managed wrapper around the in-process SQLite engine.",
            "由 Microsoft.Data.Sqlite 提供（透過 SQLitePCLRaw 內附 SQLite）。唔會啟動任何外部程式 — 呢個係對行程內 SQLite 引擎嘅託管包裝。");

        OpenBtn.Content = P("Open Database…", "開啟資料庫…");
        NewBtn.Content = P("New Database…", "新建資料庫…");
        CloseBtn.Content = P("Close", "關閉");
        RefreshBtn.Content = P("Refresh", "重新整理");

        StructureTab.Header = P("Database Structure", "資料庫結構");
        BrowseTab.Header = P("Browse Data", "瀏覽資料");
        SqlTab.Header = P("Execute SQL", "執行 SQL");

        DdlLabel.Text = P("Definition (DDL)", "定義（DDL）");

        BrowseTableLabel.Text = P("Table:", "表：");
        InsertRowBtn.Content = P("Insert Row", "插入列");
        DeleteRowBtn.Content = P("Delete Row", "刪除列");
        SaveCellBtn.Content = P("Save Edits", "儲存修改");
        ExportTableBtn.Content = P("Export CSV", "匯出 CSV");
        PageSizeLabel.Text = P("Page size:", "每頁列數：");

        RunBtn.Content = P("Run ▶", "執行 ▶");
        CancelBtn.Content = P("Cancel", "取消");
        ClearBtn.Content = P("Clear", "清除");
        ExportResultBtn.Content = P("Export CSV", "匯出 CSV");
        SqlBox.PlaceholderText = P("Write SQL here, then press Run (or Ctrl+Enter)…", "喺度寫 SQL，跟住撳執行（或 Ctrl+Enter）…");

        UpdatePathLabel();
        UpdatePageLabel();
        if (_browseTable is null) BrowseTableLabel.Text = P("Table:", "表：");
    }

    private void UpdatePathLabel()
    {
        PathLabel.Text = _dbPath is null
            ? P("No database open. Open an existing .db / .sqlite file, or create a new one.",
                "未開啟資料庫。開啟現有 .db／.sqlite 檔，或建立一個新嘅。")
            : P($"Open: {_dbPath}", $"已開啟：{_dbPath}");
    }

    // ===================== Open / New / Close =====================

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var filters = new List<FileDialogs.Filter>
        {
            new("SQLite databases", "*.db;*.sqlite;*.sqlite3;*.db3"),
            new("All files", "*.*"),
        };
        var path = await FileDialogs.OpenFileAsync(filters, P("Open SQLite database", "開啟 SQLite 資料庫"));
        if (string.IsNullOrEmpty(path)) return;

        var (ok, ver, errEn, errZh) = await SqliteService.OpenDatabaseAsync(path);
        if (!ok)
        {
            ShowBrowse(P(errEn ?? "Could not open database.", errZh ?? "無法開啟資料庫。"), InfoBarSeverity.Error);
            return;
        }
        _dbPath = path;
        AfterOpen();
        ShowBrowse(P($"Opened — SQLite {ver}.", $"已開啟 — SQLite {ver}。"), InfoBarSeverity.Success);
    }

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        var filters = new List<FileDialogs.Filter>
        {
            new("SQLite database", "*.db"),
            new("All files", "*.*"),
        };
        var path = await FileDialogs.SaveFileAsync("new_database.db", filters, "db", P("Create new SQLite database", "建立新 SQLite 資料庫"));
        if (string.IsNullOrEmpty(path)) return;

        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }
        var (ok, errEn, errZh) = await SqliteService.CreateDatabaseAsync(path);
        if (!ok)
        {
            ShowBrowse(P(errEn ?? "Could not create database.", errZh ?? "無法建立資料庫。"), InfoBarSeverity.Error);
            return;
        }
        _dbPath = path;
        AfterOpen();
        ShowBrowse(P("New empty database created.", "已建立新空白資料庫。"), InfoBarSeverity.Success);
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _dbPath = null;
        _browseTable = null;
        _page = null;
        _lastResult = null;
        StructureTree.RootNodes.Clear();
        ColumnsHost.Children.Clear();
        StructureSelLabel.Text = "";
        DdlBox.Text = "";
        BrowseTableCombo.Items.Clear();
        BrowseGridHost.Children.Clear();
        ResultsHost.Children.Clear();
        ResultSummary.Text = "";
        SetOpenState(false);
        UpdatePathLabel();
        UpdatePageLabel();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_dbPath is null) return;
        await ReloadStructure();
        await ReloadBrowseTables();
    }

    private void SetOpenState(bool open)
    {
        CloseBtn.IsEnabled = open;
        RefreshBtn.IsEnabled = open;
        RunBtn.IsEnabled = open;
    }

    private async void AfterOpen()
    {
        SetOpenState(true);
        UpdatePathLabel();
        await ReloadStructure();
        await ReloadBrowseTables();
    }

    // ===================== Structure tab =====================

    private async Task ReloadStructure()
    {
        StructureTree.RootNodes.Clear();
        ColumnsHost.Children.Clear();
        StructureSelLabel.Text = "";
        DdlBox.Text = "";
        if (_dbPath is null) return;

        var objects = await SqliteService.ListObjectsAsync(_dbPath);

        AddGroup(P("Tables", "表"), objects.Where(o => o.Kind == SqliteObjectKind.Table));
        AddGroup(P("Views", "檢視"), objects.Where(o => o.Kind == SqliteObjectKind.View));
        AddGroup(P("Indexes", "索引"), objects.Where(o => o.Kind == SqliteObjectKind.Index));
        AddGroup(P("Triggers", "觸發器"), objects.Where(o => o.Kind == SqliteObjectKind.Trigger));

        void AddGroup(string label, IEnumerable<SqliteObject> items)
        {
            var list = items.ToList();
            var group = new TreeViewNode { Content = $"{label} ({list.Count})", IsExpanded = true };
            StructureTree.RootNodes.Add(group);
            foreach (var o in list)
            {
                var n = new TreeViewNode { Content = o };
                group.Children.Add(n);
            }
        }
    }

    private async void StructureTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is not TreeViewNode node) return;
        if (node.Content is not SqliteObject obj) return;
        await ShowObjectDetail(obj);
    }

    private async Task ShowObjectDetail(SqliteObject obj)
    {
        if (_dbPath is null) return;
        StructureSelLabel.Text = obj.Kind switch
        {
            SqliteObjectKind.Table => P($"Table: {obj.Name}", $"表：{obj.Name}"),
            SqliteObjectKind.View => P($"View: {obj.Name}", $"檢視：{obj.Name}"),
            SqliteObjectKind.Index => P($"Index: {obj.Name}", $"索引：{obj.Name}"),
            _ => P($"Trigger: {obj.Name}", $"觸發器：{obj.Name}"),
        };
        DdlBox.Text = obj.Sql;

        ColumnsHost.Children.Clear();
        if (obj.Kind is SqliteObjectKind.Table or SqliteObjectKind.View)
        {
            var cols = await SqliteService.ListColumnsAsync(_dbPath, obj.Name);
            BuildColumnsTable(cols);
        }
    }

    private void BuildColumnsTable(List<SqliteColumn> cols)
    {
        var headers = new[]
        {
            P("Name", "名稱"), P("Type", "型別"), P("PK", "主鍵"),
            P("Not Null", "非空"), P("Default", "預設值"),
        };
        var grid = new Grid();
        for (int c = 0; c < headers.Length; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 60 });

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < headers.Length; c++)
            grid.Children.Add(HeaderCell(headers[c], 0, c));

        var altBrush = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        for (int r = 0; r < cols.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var col = cols[r];
            var vals = new[]
            {
                col.Name, string.IsNullOrEmpty(col.Type) ? "—" : col.Type,
                col.Pk ? "✓" : "", col.NotNull ? "✓" : "", col.DefaultValue ?? "",
            };
            for (int c = 0; c < vals.Length; c++)
                grid.Children.Add(DataCell(vals[c], r + 1, c, (r % 2 == 1) ? altBrush : null));
        }
        ColumnsHost.Children.Add(grid);
    }

    // ===================== Browse Data tab =====================

    private async Task ReloadBrowseTables()
    {
        var prev = _browseTable;
        BrowseTableCombo.Items.Clear();
        _browseTable = null;
        _page = null;
        BrowseGridHost.Children.Clear();
        if (_dbPath is null) { UpdateBrowseButtons(); return; }

        var objects = await SqliteService.ListObjectsAsync(_dbPath);
        var tables = objects.Where(o => o.Kind == SqliteObjectKind.Table).Select(o => o.Name).ToList();
        foreach (var t in tables) BrowseTableCombo.Items.Add(new ComboBoxItem { Content = t, Tag = t });

        if (tables.Count > 0)
        {
            int idx = prev is not null ? tables.IndexOf(prev) : 0;
            BrowseTableCombo.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            UpdateBrowseButtons();
            UpdatePageLabel();
        }
    }

    private async void BrowseTableCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BrowseTableCombo.SelectedItem is not ComboBoxItem item) return;
        _browseTable = item.Tag as string;
        _pageIndex = 0;
        await LoadPage();
    }

    private async void PageSize_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageSizeCombo.SelectedItem is not ComboBoxItem item) return;
        if (int.TryParse(item.Tag as string, out var size)) _pageSize = size;
        _pageIndex = 0;
        if (_browseTable is not null) await LoadPage();
    }

    private async void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex > 0) { _pageIndex--; await LoadPage(); }
    }

    private async void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_page is not null && (long)(_pageIndex + 1) * _pageSize < _page.TotalRows)
        {
            _pageIndex++;
            await LoadPage();
        }
    }

    private async Task LoadPage()
    {
        if (_dbPath is null || _browseTable is null) return;
        _selectedRow = -1;
        SaveCellBtn.IsEnabled = false;
        try
        {
            _page = await SqliteService.BrowsePageAsync(_dbPath, _browseTable, _pageSize, _pageIndex);
            BuildBrowseGrid(_page);
        }
        catch (Exception ex)
        {
            BrowseGridHost.Children.Clear();
            ShowBrowse(ex.Message, InfoBarSeverity.Error);
        }
        UpdateBrowseButtons();
        UpdatePageLabel();
    }

    private void BuildBrowseGrid(SqlitePage page)
    {
        BrowseGridHost.Children.Clear();
        _cellBoxes.Clear();
        int cols = page.Columns.Count;
        if (cols == 0) return;

        bool editable = page.HasRowId || page.PrimaryKeys.Count > 0;

        var grid = new Grid();
        // first column: row selector
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 80 });

        // header row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.Children.Add(HeaderCell("#", 0, 0));
        for (int c = 0; c < cols; c++)
        {
            bool isPk = page.PrimaryKeys.Contains(page.Columns[c]);
            grid.Children.Add(HeaderCell(isPk ? page.Columns[c] + " 🔑" : page.Columns[c], 0, c + 1));
        }

        var altBrush = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        for (int r = 0; r < page.Rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = page.Rows[r];

            // row selector button
            int capturedRow = r;
            var selBtn = new Button
            {
                Content = ((long)_pageIndex * _pageSize + r + 1).ToString(),
                Padding = new Thickness(8, 2, 8, 2),
                MinWidth = 44,
                FontSize = 11,
            };
            selBtn.Click += (_, _) => SelectRow(capturedRow);
            var selBorder = new Border
            {
                Padding = new Thickness(4, 2, 4, 2),
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Child = selBtn,
            };
            Grid.SetRow(selBorder, r + 1);
            Grid.SetColumn(selBorder, 0);
            grid.Children.Add(selBorder);

            for (int c = 0; c < cols; c++)
            {
                var val = c < row.Length ? row[c] : null;
                var box = new TextBox
                {
                    Text = val ?? "",
                    PlaceholderText = val is null ? "NULL" : "",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    BorderThickness = new Thickness(0),
                    Background = null,
                    MinWidth = 80,
                    IsReadOnly = !editable,
                    Tag = (r, c),
                };
                _cellBoxes[(r, c)] = box;
                var cell = new Border
                {
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    Background = (r % 2 == 1) ? altBrush : null,
                    Child = box,
                };
                Grid.SetRow(cell, r + 1);
                Grid.SetColumn(cell, c + 1);
                grid.Children.Add(cell);
            }
        }

        BrowseGridHost.Children.Add(grid);
        SaveCellBtn.IsEnabled = editable && page.Rows.Count > 0;
        if (!editable && page.Rows.Count > 0)
            ShowBrowse(P("This table has no primary key or rowid — rows are read-only.",
                "呢個表無主鍵亦無 rowid — 資料唯讀。"), InfoBarSeverity.Warning);
    }

    private void SelectRow(int row)
    {
        _selectedRow = row;
        DeleteRowBtn.IsEnabled = _page is not null && (_page.HasRowId || _page.PrimaryKeys.Count > 0);
        ShowBrowse(P($"Row {row + 1} selected.", $"已選第 {row + 1} 列。"), InfoBarSeverity.Informational);
    }

    /// <summary>儲存所有被改過嘅單元格 · Persist every cell whose text differs from the loaded value.</summary>
    private async void SaveCell_Click(object sender, RoutedEventArgs e)
    {
        if (_dbPath is null || _browseTable is null || _page is null) return;
        int saved = 0;
        for (int r = 0; r < _page.Rows.Count; r++)
        {
            for (int c = 0; c < _page.Columns.Count; c++)
            {
                if (!_cellBoxes.TryGetValue((r, c), out var box)) continue;
                var original = _page.Rows[r][c];
                var current = box.Text;
                // Treat empty text as "" (not NULL) once edited; original null shown as empty placeholder.
                if (string.Equals(current ?? "", original ?? "", StringComparison.Ordinal)) continue;

                var (pkCols, pkVals) = RowLocator(r);
                long rowId = _page.HasRowId && r < _page.RowIds.Count ? _page.RowIds[r] : 0L;
                var (ok, errEn, errZh) = await SqliteService.UpdateCellAsync(
                    _dbPath, _browseTable, _page.Columns[c], current,
                    _page.HasRowId, rowId, pkCols, pkVals);
                if (!ok)
                {
                    ShowBrowse(P(errEn ?? "Update failed.", errZh ?? "更新失敗。"), InfoBarSeverity.Error);
                    return;
                }
                saved++;
            }
        }
        if (saved == 0)
            ShowBrowse(P("No changes to save.", "無修改可儲存。"), InfoBarSeverity.Informational);
        else
        {
            await LoadPage();
            ShowBrowse(P($"Saved {saved} cell edit(s).", $"已儲存 {saved} 個單元格修改。"), InfoBarSeverity.Success);
        }
    }

    private (IReadOnlyList<string> cols, IReadOnlyList<string?> vals) RowLocator(int row)
    {
        if (_page is null || _page.PrimaryKeys.Count == 0) return (Array.Empty<string>(), Array.Empty<string?>());
        var cols = _page.PrimaryKeys;
        var vals = new List<string?>();
        foreach (var pk in cols)
        {
            int idx = _page.Columns.IndexOf(pk);
            vals.Add(idx >= 0 && row < _page.Rows.Count ? _page.Rows[row][idx] : null);
        }
        return (cols, vals);
    }

    private async void InsertRow_Click(object sender, RoutedEventArgs e)
    {
        if (_dbPath is null || _browseTable is null) return;
        var cols = await SqliteService.ListColumnsAsync(_dbPath, _browseTable);
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P($"Insert row into {_browseTable}", $"插入列到 {_browseTable}"),
            PrimaryButtonText = P("Insert", "插入"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        var panel = new StackPanel { Spacing = 8 };
        var boxes = new Dictionary<string, (TextBox box, CheckBox nullChk)>();
        foreach (var col in cols)
        {
            var label = new TextBlock
            {
                Text = $"{col.Name}  ({(string.IsNullOrEmpty(col.Type) ? "—" : col.Type)}{(col.Pk ? ", PK" : "")}{(col.NotNull ? ", NOT NULL" : "")})",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            var tb = new TextBox { PlaceholderText = col.DefaultValue ?? "", FontFamily = new FontFamily("Consolas") };
            var nullChk = new CheckBox { Content = "NULL", IsChecked = !col.NotNull && col.DefaultValue is null };
            boxes[col.Name] = (tb, nullChk);
            panel.Children.Add(label);
            var rowPanel = new Grid { ColumnSpacing = 8 };
            rowPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(tb, 0); Grid.SetColumn(nullChk, 1);
            nullChk.VerticalAlignment = VerticalAlignment.Center;
            rowPanel.Children.Add(tb); rowPanel.Children.Add(nullChk);
            panel.Children.Add(rowPanel);
        }
        dialog.Content = new ScrollViewer { Content = panel, MaxHeight = 400 };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var colNames = new List<string>();
        var values = new List<string?>();
        foreach (var col in cols)
        {
            var (box, nullChk) = boxes[col.Name];
            colNames.Add(col.Name);
            values.Add(nullChk.IsChecked == true ? null : box.Text);
        }
        var (ok, errEn, errZh) = await SqliteService.InsertRowAsync(_dbPath, _browseTable, colNames, values);
        if (!ok) { ShowBrowse(P(errEn ?? "Insert failed.", errZh ?? "插入失敗。"), InfoBarSeverity.Error); return; }
        await LoadPage();
        ShowBrowse(P("Row inserted.", "已插入一列。"), InfoBarSeverity.Success);
    }

    private async void DeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (_dbPath is null || _browseTable is null || _page is null) return;
        if (_selectedRow < 0 || _selectedRow >= _page.Rows.Count)
        {
            ShowBrowse(P("Select a row first (click its number).", "請先選一列（撳列號）。"), InfoBarSeverity.Warning);
            return;
        }
        var confirm = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Delete row?", "刪除呢一列？"),
            Content = P($"Permanently delete row {_selectedRow + 1}? This cannot be undone.",
                $"永久刪除第 {_selectedRow + 1} 列？呢個動作無法復原。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        var (pkCols, pkVals) = RowLocator(_selectedRow);
        long rowId = _page.HasRowId && _selectedRow < _page.RowIds.Count ? _page.RowIds[_selectedRow] : 0L;
        var (ok, errEn, errZh) = await SqliteService.DeleteRowAsync(_dbPath, _browseTable, _page.HasRowId, rowId, pkCols, pkVals);
        if (!ok) { ShowBrowse(P(errEn ?? "Delete failed.", errZh ?? "刪除失敗。"), InfoBarSeverity.Error); return; }
        _selectedRow = -1;
        await LoadPage();
        ShowBrowse(P("Row deleted.", "已刪除一列。"), InfoBarSeverity.Success);
    }

    private async void ExportTable_Click(object sender, RoutedEventArgs e)
    {
        if (_dbPath is null || _browseTable is null) return;
        var path = await FileDialogs.SaveFileAsync($"{_browseTable}.csv", ".csv");
        if (string.IsNullOrEmpty(path)) return;
        var (ok, rows, errEn, errZh) = await SqliteService.ExportTableCsvAsync(_dbPath, _browseTable, path);
        if (!ok) { ShowBrowse(P(errEn ?? "Export failed.", errZh ?? "匯出失敗。"), InfoBarSeverity.Error); return; }
        ShowBrowse(P($"Exported {rows} row(s) to CSV.", $"已匯出 {rows} 列到 CSV。"), InfoBarSeverity.Success);
    }

    private void UpdateBrowseButtons()
    {
        bool hasTable = _browseTable is not null && _page is not null;
        bool editable = _page is not null && (_page.HasRowId || _page.PrimaryKeys.Count > 0);
        InsertRowBtn.IsEnabled = hasTable;
        ExportTableBtn.IsEnabled = hasTable;
        DeleteRowBtn.IsEnabled = false; // enabled when a row is selected
        SaveCellBtn.IsEnabled = hasTable && editable && _page!.Rows.Count > 0;
        bool hasPrev = _pageIndex > 0;
        bool hasNext = _page is not null && (long)(_pageIndex + 1) * _pageSize < _page.TotalRows;
        PrevPageBtn.IsEnabled = hasPrev;
        NextPageBtn.IsEnabled = hasNext;
    }

    private void UpdatePageLabel()
    {
        if (_page is null || _browseTable is null)
        {
            PageLabel.Text = "";
            RowCountLabel.Text = "";
            return;
        }
        long from = (long)_pageIndex * _pageSize + 1;
        long to = Math.Min(from + _page.Rows.Count - 1, _page.TotalRows);
        if (_page.Rows.Count == 0) { from = 0; to = 0; }
        PageLabel.Text = P($"Rows {from}–{to}", $"第 {from}–{to} 列");
        RowCountLabel.Text = P($"Total: {_page.TotalRows} row(s)", $"總計：{_page.TotalRows} 列");
    }

    // ===================== Execute SQL tab =====================

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var sql = (SqlBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sql)) { ShowResult(P("Nothing to run.", "無嘢可執行。"), InfoBarSeverity.Error); return; }
        await ExecuteSql(sql);
    }

    private async void RunAccel_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (!RunBtn.IsEnabled) return;
        var sql = (SqlBox.Text ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(sql)) await ExecuteSql(sql);
    }

    private async Task ExecuteSql(string sql)
    {
        if (_dbPath is null) { ShowResult(P("Open a database first.", "請先開啟資料庫。"), InfoBarSeverity.Error); return; }
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        RunBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        ResultBar.IsOpen = false;
        ResultSummary.Text = P("Running…", "執行中…");

        var result = await SqliteService.RunSqlAsync(_dbPath, sql, SqliteService.DefaultRowCap, _queryCts.Token);
        _lastResult = result;

        RunBtn.IsEnabled = _dbPath is not null;
        CancelBtn.IsEnabled = false;

        if (!result.Success)
        {
            ResultsHost.Children.Clear();
            ResultSummary.Text = P($"Failed in {result.ElapsedMs} ms", $"失敗，用咗 {result.ElapsedMs} 毫秒");
            ShowResult(P(result.ErrorEn ?? "Query failed.", result.ErrorZh ?? "查詢失敗。"), InfoBarSeverity.Error);
            ExportResultBtn.IsEnabled = false;
            return;
        }

        if (result.HasResultSet)
        {
            BuildResultsGrid(result);
            var trunc = result.Truncated
                ? P($" (capped at {SqliteService.DefaultRowCap})", $"（已限制至 {SqliteService.DefaultRowCap}）")
                : "";
            ResultSummary.Text = P(
                $"{result.Rows.Count} row(s), {result.Columns.Count} column(s) — {result.ElapsedMs} ms{trunc}",
                $"{result.Rows.Count} 列、{result.Columns.Count} 欄 — {result.ElapsedMs} 毫秒{trunc}");
            ExportResultBtn.IsEnabled = result.Rows.Count > 0;
            if (result.Truncated)
                ShowResult(P($"Result capped at {SqliteService.DefaultRowCap} rows. Add a LIMIT for more control.",
                    $"結果已限制至 {SqliteService.DefaultRowCap} 列。可加 LIMIT 控制。"), InfoBarSeverity.Warning);
        }
        else
        {
            ResultsHost.Children.Clear();
            ResultSummary.Text = P(result.StatusEn ?? "OK", result.StatusZh ?? "完成") + $" — {result.ElapsedMs} ms";
            ShowResult(P(result.StatusEn ?? "Command OK.", result.StatusZh ?? "指令完成。"), InfoBarSeverity.Success);
            ExportResultBtn.IsEnabled = false;
            // DML may have changed structure/data — refresh the other tabs.
            await ReloadStructure();
            await ReloadBrowseTables();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _queryCts?.Cancel();
        CancelBtn.IsEnabled = false;
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SqlBox.Text = "";
        ResultsHost.Children.Clear();
        ResultSummary.Text = "";
        ResultBar.IsOpen = false;
        _lastResult = null;
        ExportResultBtn.IsEnabled = false;
    }

    private async void ExportResult_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null || !_lastResult.HasResultSet || _lastResult.Rows.Count == 0)
        {
            ShowResult(P("No result to export.", "無結果可匯出。"), InfoBarSeverity.Error);
            return;
        }
        var path = await FileDialogs.SaveFileAsync("query_result.csv", ".csv");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var csv = SqliteService.ToCsv(_lastResult.Columns, _lastResult.Rows);
            await System.IO.File.WriteAllTextAsync(path, csv, new System.Text.UTF8Encoding(true));
            ShowResult(P($"Exported {_lastResult.Rows.Count} row(s) to CSV.", $"已匯出 {_lastResult.Rows.Count} 列到 CSV。"), InfoBarSeverity.Success);
        }
        catch (Exception ex) { ShowResult(ex.Message, InfoBarSeverity.Error); }
    }

    private void BuildResultsGrid(SqliteQueryResult r)
    {
        ResultsHost.Children.Clear();
        int cols = r.Columns.Count;
        if (cols == 0) return;

        var grid = new Grid();
        for (int c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 60 });

        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < cols; c++)
            grid.Children.Add(HeaderCell(r.Columns[c], 0, c));

        var altBrush = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        for (int rIdx = 0; rIdx < r.Rows.Count; rIdx++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = r.Rows[rIdx];
            for (int c = 0; c < cols; c++)
            {
                var val = c < row.Length ? row[c] : null;
                grid.Children.Add(DataCell(val ?? "(null)", rIdx + 1, c, (rIdx % 2 == 1) ? altBrush : null, isNull: val is null));
            }
        }
        ResultsHost.Children.Add(grid);
    }

    // ===================== Shared cell builders =====================

    private static Border HeaderCell(string text, int row, int col)
    {
        var b = new Border
        {
            Padding = new Thickness(10, 6, 10, 6),
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            Child = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 12 },
        };
        Grid.SetRow(b, row);
        Grid.SetColumn(b, col);
        return b;
    }

    private static Border DataCell(string text, int row, int col, Brush? bg, bool isNull = false)
    {
        var b = new Border
        {
            Padding = new Thickness(10, 4, 10, 4),
            BorderThickness = new Thickness(0, 0, 1, 1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = bg,
            Child = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsTextSelectionEnabled = true,
                TextWrapping = TextWrapping.NoWrap,
                Foreground = isNull
                    ? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
            },
        };
        Grid.SetRow(b, row);
        Grid.SetColumn(b, col);
        return b;
    }

    // ===================== InfoBar helpers =====================

    private void ShowBrowse(string msg, InfoBarSeverity sev) => SetBar(BrowseBar, msg, sev);
    private void ShowResult(string msg, InfoBarSeverity sev) => SetBar(ResultBar, msg, sev);

    private void SetBar(InfoBar bar, string msg, InfoBarSeverity sev)
    {
        bar.Severity = sev;
        bar.Title = sev switch
        {
            InfoBarSeverity.Success => P("Success", "成功"),
            InfoBarSeverity.Warning => P("Notice", "注意"),
            InfoBarSeverity.Error => P("Error", "錯誤"),
            _ => P("Info", "資訊"),
        };
        bar.Message = msg;
        bar.IsOpen = true;
    }
}
