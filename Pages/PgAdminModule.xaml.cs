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
/// Postgres 工具 / pgAdmin · A native lightweight PostgreSQL client built on Npgsql:
/// 連線管理（DPAPI 加密密碼）、物件樹（資料庫 → 結構描述 → 表／檢視）、SQL 編輯器 + 結果格、CSV 匯出，
/// 同埋偵測／啟動完整 pgAdmin 4 桌面版作後備。全部雙語（粵語 + English）。
/// Connection manager (DPAPI-encrypted passwords), object tree (databases → schemas → tables/views),
/// a SQL editor with a results grid, CSV export, and detection / launch of the full pgAdmin 4 desktop
/// app as a fallback. Fully bilingual (Cantonese + English).
/// </summary>
public sealed partial class PgAdminModule : Page
{
    private readonly Dictionary<TreeViewNode, PgTreeTag> _tags = new();
    private List<PgConnection> _saved = new();
    private PgConnection _current = new();
    private bool _connected;
    private bool _suppressComboEvent;
    private CancellationTokenSource? _queryCts;
    private PgQueryResult? _lastResult;

    /// <summary>樹節點附帶資料 · Per-node tag describing what this tree node represents.</summary>
    private sealed class PgTreeTag
    {
        public PgNodeKind Kind;
        public string Database = "";
        public string Schema = "";
        public string Name = "";
    }

    public PgAdminModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; _queryCts?.Cancel(); };
        Loaded += async (_, _) =>
        {
            Render();
            LoadSavedIntoCombo();
            await CheckEngine();
        };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Postgres Tool · Postgres 工具 / pgAdmin";
        HeaderBlurb.Text = P(
            "A native PostgreSQL client: save connections, browse databases / schemas / tables, run SQL and view results. Launch the full pgAdmin 4 desktop app for advanced administration.",
            "原生 PostgreSQL 用戶端：儲存連線、瀏覽資料庫／結構描述／表、跑 SQL 睇結果。需要進階管理時可啟動完整 pgAdmin 4 桌面版。");

        NewConnBtn.Content = P("New", "新增");
        DeleteConnBtn.Content = P("Delete", "刪除");
        ConnectBtn.Content = P("Connect", "連線");
        TestBtn.Content = P("Test", "測試");
        DisconnectBtn.Content = P("Disconnect", "中斷");
        LaunchPgAdminBtn.Content = P("Launch pgAdmin 4", "啟動 pgAdmin 4");
        SaveConnBtn.Content = P("Save", "儲存");

        NameBox.PlaceholderText = P("Connection name (optional)", "連線名稱（選填）");
        HostBox.PlaceholderText = P("Host", "主機");
        DbBox.PlaceholderText = P("Database", "資料庫");
        UserBox.PlaceholderText = P("User", "使用者");
        PassBox.PlaceholderText = P("Password", "密碼");
        SavePassCheck.Content = P("Save password", "記住密碼");

        TreeHeader.Text = P("Object tree", "物件樹");
        RunBtn.Content = P("Run ▶", "執行 ▶");
        CancelBtn.Content = P("Cancel", "取消");
        ClearBtn.Content = P("Clear", "清除");
        ExportCsvBtn.Content = P("Export CSV", "匯出 CSV");
        SqlBox.PlaceholderText = P("Write SQL here, then press Run (or Ctrl+Enter)…", "喺度寫 SQL，跟住撳執行（或 Ctrl+Enter）…");

        UpdateDbContextLabel();
    }

    // ===================== Engine / pgAdmin fallback =====================

    private async Task CheckEngine()
    {
        // Probe (off the UI thread) for a local PostgreSQL server and for the pgAdmin 4 desktop app.
        var (localPg, pgAdmin) = await Task.Run(() =>
            (PostgresService.IsLocalPostgresInstalled(), PostgresService.IsPgAdminInstalled()));

        LaunchPgAdminBtn.Visibility = pgAdmin ? Visibility.Visible : Visibility.Collapsed;

        // No local Postgres server detected → offer the one-click prerequisite install
        // (PostgreSQL.PostgreSQL — this winget package bundles the server + pgAdmin 4).
        if (!localPg)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("No local PostgreSQL found", "搵唔到本機 PostgreSQL");
            EngineBar.Message = P(
                "WinForge can connect to any Postgres server natively (Npgsql), but none was detected on this PC (no psql, no service). Install PostgreSQL + pgAdmin locally to get started — no restart needed. You can still connect to a remote server using the fields below.",
                "WinForge 可以用原生 Npgsql 連任何 Postgres 伺服器，但喺呢部電腦搵唔到（冇 psql、冇服務）。撳一下喺本機裝 PostgreSQL + pgAdmin 就用得 — 唔使重啟。你亦可以用下面嘅欄位連遠端伺服器。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                PostgresService.PostgresWingetId, "Install PostgreSQL + pgAdmin", "安裝 PostgreSQL + pgAdmin",
                async () => { await CheckEngine(); },
                PostgresService.RescanLocal);
            return;
        }

        // Local Postgres present. If the full pgAdmin 4 desktop is installed we're done; otherwise
        // offer the (optional) pgAdmin-only install for advanced administration.
        if (pgAdmin)
        {
            EngineBar.IsOpen = false;
            EngineBar.ActionButton = null;
            EngineBar.Content = null;
            return;
        }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Informational;
        EngineBar.Title = P("Native client ready", "原生用戶端已就緒");
        EngineBar.Message = P(
            "WinForge talks to Postgres natively (Npgsql) — no install needed. For full administration (roles, backups, dashboards) you can install the pgAdmin 4 desktop app.",
            "WinForge 用原生 Npgsql 直接連 Postgres — 唔使裝嘢。如需完整管理（角色、備份、儀表板），可安裝 pgAdmin 4 桌面版。");
        EngineBar.ActionButton = null;
        EngineBar.Content = EngineBars.AutoInstallProgress(
            PostgresService.PgAdminWingetId, "Install pgAdmin 4 (optional)", "安裝 pgAdmin 4（選用）",
            async () => { await CheckEngine(); },
            null);
    }

    private async void LaunchPgAdmin_Click(object sender, RoutedEventArgs e)
    {
        LaunchPgAdminBtn.IsEnabled = false;
        bool ok = await PostgresService.LaunchPgAdminAsync();
        if (!ok) ShowError(P("Could not launch pgAdmin 4.", "無法啟動 pgAdmin 4。"));
        LaunchPgAdminBtn.IsEnabled = true;
    }

    // ===================== Saved connections =====================

    private void LoadSavedIntoCombo()
    {
        _saved = PostgresService.LoadConnections();
        _suppressComboEvent = true;
        SavedCombo.Items.Clear();
        SavedCombo.Items.Add(new ComboBoxItem { Content = P("— New connection —", "— 新連線 —"), Tag = "" });
        foreach (var c in _saved)
            SavedCombo.Items.Add(new ComboBoxItem { Content = c.Display, Tag = c.Id });
        SavedCombo.SelectedIndex = 0;
        _suppressComboEvent = false;
        ApplyToFields(_current);
    }

    private void SavedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressComboEvent) return;
        if (SavedCombo.SelectedItem is not ComboBoxItem item) return;
        var id = item.Tag as string ?? "";
        if (string.IsNullOrEmpty(id)) { _current = new PgConnection(); ApplyToFields(_current); return; }
        var c = _saved.FirstOrDefault(x => x.Id == id);
        if (c is null) return;
        _current = c;
        ApplyToFields(c);
    }

    private void ApplyToFields(PgConnection c)
    {
        NameBox.Text = c.Name;
        HostBox.Text = c.Host;
        PortBox.Value = c.Port;
        DbBox.Text = c.Database;
        UserBox.Text = c.Username;
        SavePassCheck.IsChecked = c.SavePassword;
        var pw = PostgresService.DecryptPassword(c.EncryptedPassword);
        PassBox.Password = pw ?? "";
        for (int i = 0; i < SslCombo.Items.Count; i++)
            if (SslCombo.Items[i] is ComboBoxItem ci && (ci.Tag as string) == c.SslMode) { SslCombo.SelectedIndex = i; break; }
        if (SslCombo.SelectedIndex < 0) SslCombo.SelectedIndex = 0;
    }

    private PgConnection ReadFields()
    {
        _current.Name = NameBox.Text?.Trim() ?? "";
        _current.Host = string.IsNullOrWhiteSpace(HostBox.Text) ? "localhost" : HostBox.Text.Trim();
        _current.Port = double.IsNaN(PortBox.Value) ? 5432 : (int)PortBox.Value;
        _current.Database = string.IsNullOrWhiteSpace(DbBox.Text) ? "postgres" : DbBox.Text.Trim();
        _current.Username = string.IsNullOrWhiteSpace(UserBox.Text) ? "postgres" : UserBox.Text.Trim();
        _current.SavePassword = SavePassCheck.IsChecked == true;
        _current.SslMode = (SslCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "Prefer";
        return _current;
    }

    private string CurrentPassword() => PassBox.Password ?? "";

    private void NewConn_Click(object sender, RoutedEventArgs e)
    {
        _current = new PgConnection();
        _suppressComboEvent = true;
        SavedCombo.SelectedIndex = 0;
        _suppressComboEvent = false;
        ApplyToFields(_current);
        PassBox.Password = "";
    }

    private void SaveConn_Click(object sender, RoutedEventArgs e)
    {
        var c = ReadFields();
        c.EncryptedPassword = c.SavePassword ? PostgresService.EncryptPassword(CurrentPassword()) : null;
        PostgresService.Upsert(c);
        var keepId = c.Id;
        LoadSavedIntoCombo();
        SelectSavedById(keepId);
        ShowInfo(P("Connection saved.", "連線已儲存。"));
    }

    private void DeleteConn_Click(object sender, RoutedEventArgs e)
    {
        var id = (SavedCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        if (string.IsNullOrEmpty(id)) return;
        PostgresService.Delete(id);
        _current = new PgConnection();
        LoadSavedIntoCombo();
        ShowInfo(P("Connection deleted.", "連線已刪除。"));
    }

    private void SelectSavedById(string id)
    {
        for (int i = 0; i < SavedCombo.Items.Count; i++)
            if (SavedCombo.Items[i] is ComboBoxItem ci && (ci.Tag as string) == id)
            { _suppressComboEvent = true; SavedCombo.SelectedIndex = i; _suppressComboEvent = false; break; }
    }

    // ===================== Connect / test / disconnect =====================

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        var c = ReadFields();
        TestBtn.IsEnabled = false;
        ShowInfo(P("Testing connection…", "正在測試連線…"), InfoBarSeverity.Informational);
        var (ok, ver, errEn, errZh) = await PostgresService.TestConnectionAsync(c, CurrentPassword());
        if (ok)
            ShowInfo(P($"Connection OK — server version {ver}.", $"連線成功 — 伺服器版本 {ver}。"), InfoBarSeverity.Success);
        else
            ShowError(P(errEn ?? "Connection failed.", errZh ?? "連線失敗。"));
        TestBtn.IsEnabled = true;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var c = ReadFields();
        ConnectBtn.IsEnabled = false;
        ShowInfo(P("Connecting…", "正在連線…"), InfoBarSeverity.Informational);
        var (ok, ver, errEn, errZh) = await PostgresService.TestConnectionAsync(c, CurrentPassword());
        if (!ok)
        {
            ShowError(P(errEn ?? "Connection failed.", errZh ?? "連線失敗。"));
            ConnectBtn.IsEnabled = true;
            return;
        }
        _connected = true;
        DisconnectBtn.IsEnabled = true;
        RunBtn.IsEnabled = true;
        ConnectBtn.IsEnabled = true;
        ShowInfo(P($"Connected — server version {ver}.", $"已連線 — 伺服器版本 {ver}。"), InfoBarSeverity.Success);
        UpdateDbContextLabel();
        await BuildTreeRoot();
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        _connected = false;
        DisconnectBtn.IsEnabled = false;
        RunBtn.IsEnabled = false;
        Tree.RootNodes.Clear();
        _tags.Clear();
        ResultsHost.Children.Clear();
        ResultSummary.Text = "";
        _lastResult = null;
        ExportCsvBtn.IsEnabled = false;
        ShowInfo(P("Disconnected.", "已中斷連線。"), InfoBarSeverity.Informational);
    }

    private void UpdateDbContextLabel()
    {
        DbContextLabel.Text = _connected
            ? P($"DB context: {_current.Database}", $"資料庫內容：{_current.Database}")
            : P("Not connected", "未連線");
    }

    // ===================== Object tree =====================

    private async Task BuildTreeRoot()
    {
        Tree.RootNodes.Clear();
        _tags.Clear();
        var serverNode = new TreeViewNode { Content = $"{_current.Host}:{_current.Port}", IsExpanded = true };
        _tags[serverNode] = new PgTreeTag { Kind = PgNodeKind.Server };
        Tree.RootNodes.Add(serverNode);

        var dbs = await PostgresService.ListDatabasesAsync(_current, CurrentPassword());
        foreach (var db in dbs)
        {
            var dbNode = new TreeViewNode { Content = $"  {db}", HasUnrealizedChildren = true };
            _tags[dbNode] = new PgTreeTag { Kind = PgNodeKind.Database, Database = db };
            serverNode.Children.Add(dbNode);
        }
        serverNode.HasUnrealizedChildren = false;
    }

    private async void Tree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        var node = args.Node;
        if (!node.HasUnrealizedChildren || node.Children.Count > 0) return;
        if (!_tags.TryGetValue(node, out var tag)) return;

        try
        {
            switch (tag.Kind)
            {
                case PgNodeKind.Database:
                {
                    var schemas = await PostgresService.ListSchemasAsync(_current, CurrentPassword(), tag.Database);
                    foreach (var s in schemas)
                    {
                        var n = new TreeViewNode { Content = $"  {s}", HasUnrealizedChildren = true };
                        _tags[n] = new PgTreeTag { Kind = PgNodeKind.Schema, Database = tag.Database, Schema = s };
                        node.Children.Add(n);
                    }
                    break;
                }
                case PgNodeKind.Schema:
                {
                    var tablesFolder = new TreeViewNode { Content = P("Tables", "表"), HasUnrealizedChildren = true };
                    _tags[tablesFolder] = new PgTreeTag { Kind = PgNodeKind.TableFolder, Database = tag.Database, Schema = tag.Schema };
                    node.Children.Add(tablesFolder);
                    var viewsFolder = new TreeViewNode { Content = P("Views", "檢視"), HasUnrealizedChildren = true };
                    _tags[viewsFolder] = new PgTreeTag { Kind = PgNodeKind.ViewFolder, Database = tag.Database, Schema = tag.Schema };
                    node.Children.Add(viewsFolder);
                    break;
                }
                case PgNodeKind.TableFolder:
                {
                    var tables = await PostgresService.ListTablesAsync(_current, CurrentPassword(), tag.Database, tag.Schema);
                    foreach (var t in tables)
                    {
                        var n = new TreeViewNode { Content = $"  {t}" };
                        _tags[n] = new PgTreeTag { Kind = PgNodeKind.Table, Database = tag.Database, Schema = tag.Schema, Name = t };
                        node.Children.Add(n);
                    }
                    break;
                }
                case PgNodeKind.ViewFolder:
                {
                    var views = await PostgresService.ListViewsAsync(_current, CurrentPassword(), tag.Database, tag.Schema);
                    foreach (var v in views)
                    {
                        var n = new TreeViewNode { Content = $"  {v}" };
                        _tags[n] = new PgTreeTag { Kind = PgNodeKind.View, Database = tag.Database, Schema = tag.Schema, Name = v };
                        node.Children.Add(n);
                    }
                    break;
                }
            }
        }
        catch (Exception ex) { ShowError(ex.Message); }
        node.HasUnrealizedChildren = false;
    }

    private async void Tree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is not TreeViewNode node) return;
        if (!_tags.TryGetValue(node, out var tag)) return;

        // Double-click-style browse: clicking a table/view runs a quick SELECT.
        if (tag.Kind is PgNodeKind.Table or PgNodeKind.View && _connected)
        {
            // Switch DB context to the table's database for the query.
            _current.Database = tag.Database;
            UpdateDbContextLabel();
            var sql = PostgresService.BrowseTableSql(tag.Schema, tag.Name);
            SqlBox.Text = sql;
            await ExecuteSql(sql, tag.Database);
        }
        else if (tag.Kind == PgNodeKind.Database)
        {
            _current.Database = tag.Database;
            UpdateDbContextLabel();
        }
    }

    private async void RefreshTree_Click(object sender, RoutedEventArgs e)
    {
        if (_connected) await BuildTreeRoot();
    }

    // ===================== Run SQL =====================

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var sql = (SqlBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sql)) { ShowError(P("Nothing to run.", "無嘢可執行。")); return; }
        await ExecuteSql(sql, _current.Database);
    }

    private async Task ExecuteSql(string sql, string database)
    {
        if (!_connected) { ShowError(P("Connect first.", "請先連線。")); return; }
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        RunBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        ResultBar.IsOpen = false;
        ResultSummary.Text = P("Running…", "執行中…");

        var result = await PostgresService.RunQueryAsync(_current, CurrentPassword(), database, sql, PostgresService.DefaultRowCap, _queryCts.Token);
        _lastResult = result;

        RunBtn.IsEnabled = true;
        CancelBtn.IsEnabled = false;

        if (!result.Success)
        {
            ResultsHost.Children.Clear();
            ResultSummary.Text = P($"Failed in {result.ElapsedMs} ms", $"失敗，用咗 {result.ElapsedMs} 毫秒");
            ShowError(P(result.ErrorEn ?? "Query failed.", result.ErrorZh ?? "查詢失敗。"));
            ExportCsvBtn.IsEnabled = false;
            return;
        }

        if (result.HasResultSet)
        {
            BuildResultsGrid(result);
            var truncNote = result.Truncated
                ? P($" (capped at {PostgresService.DefaultRowCap})", $"（已限制至 {PostgresService.DefaultRowCap}）")
                : "";
            ResultSummary.Text = P(
                $"{result.Rows.Count} row(s), {result.Columns.Count} column(s) — {result.ElapsedMs} ms{truncNote}",
                $"{result.Rows.Count} 列、{result.Columns.Count} 欄 — {result.ElapsedMs} 毫秒{truncNote}");
            ExportCsvBtn.IsEnabled = result.Rows.Count > 0;
            if (result.Truncated)
                ShowInfo(P($"Result capped at {PostgresService.DefaultRowCap} rows. Add a LIMIT for more control.",
                    $"結果已限制至 {PostgresService.DefaultRowCap} 列。可加 LIMIT 控制。"), InfoBarSeverity.Warning);
        }
        else
        {
            ResultsHost.Children.Clear();
            ResultSummary.Text = P(result.StatusEn ?? "OK", result.StatusZh ?? "完成") + $" — {result.ElapsedMs} ms";
            ShowInfo(P(result.StatusEn ?? "Command OK.", result.StatusZh ?? "指令完成。"), InfoBarSeverity.Success);
            ExportCsvBtn.IsEnabled = false;
        }
    }

    private async void RunAccel_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (!RunBtn.IsEnabled) return;
        var sql = (SqlBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(sql)) return;
        await ExecuteSql(sql, _current.Database);
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
        ExportCsvBtn.IsEnabled = false;
    }

    // ===================== Results grid (custom) =====================

    private void BuildResultsGrid(PgQueryResult r)
    {
        ResultsHost.Children.Clear();
        int cols = r.Columns.Count;
        if (cols == 0) return;

        var grid = new Grid();
        for (int c = 0; c < cols; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 60 });

        // header row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (int c = 0; c < cols; c++)
        {
            var header = new Border
            {
                Padding = new Thickness(10, 6, 10, 6),
                BorderThickness = new Thickness(0, 0, 1, 1),
                BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
                Child = new TextBlock { Text = r.Columns[c], FontWeight = FontWeights.SemiBold, FontSize = 12 },
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, c);
            grid.Children.Add(header);
        }

        // data rows
        var altBrush = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        for (int rIdx = 0; rIdx < r.Rows.Count; rIdx++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var row = r.Rows[rIdx];
            for (int c = 0; c < cols; c++)
            {
                var val = c < row.Length ? row[c] : "";
                var cell = new Border
                {
                    Padding = new Thickness(10, 4, 10, 4),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                    Background = (rIdx % 2 == 1) ? altBrush : null,
                    Child = new TextBlock
                    {
                        Text = val,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        IsTextSelectionEnabled = true,
                        TextWrapping = TextWrapping.NoWrap,
                        Foreground = val == "(null)"
                            ? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
                    },
                };
                Grid.SetRow(cell, rIdx + 1);
                Grid.SetColumn(cell, c);
                grid.Children.Add(cell);
            }
        }

        ResultsHost.Children.Add(grid);
    }

    // ===================== CSV export =====================

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null || !_lastResult.HasResultSet || _lastResult.Rows.Count == 0)
        {
            ShowError(P("No result to export.", "無結果可匯出。"));
            return;
        }
        var path = await FileDialogs.SaveFileAsync("query_result.csv", ".csv");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            var csv = PostgresService.ToCsv(_lastResult);
            await System.IO.File.WriteAllTextAsync(path, csv, new System.Text.UTF8Encoding(true));
            ShowInfo(P($"Exported {_lastResult.Rows.Count} row(s) to CSV.", $"已匯出 {_lastResult.Rows.Count} 列到 CSV。"), InfoBarSeverity.Success);
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    // ===================== InfoBar helpers =====================

    private void ShowInfo(string msg, InfoBarSeverity sev = InfoBarSeverity.Informational)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = sev switch
        {
            InfoBarSeverity.Success => P("Success", "成功"),
            InfoBarSeverity.Warning => P("Notice", "注意"),
            InfoBarSeverity.Error => P("Error", "錯誤"),
            _ => P("Info", "資訊"),
        };
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private void ShowError(string msg) => ShowInfo(msg, InfoBarSeverity.Error);
}
