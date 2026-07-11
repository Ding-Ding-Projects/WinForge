using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 KDBX 密碼保險庫 · Native KeePass-style local password vault (in-app KDBX 3.1 / 4 engine).
/// 開啟／建立 .kdbx → 群組樹 + 項目清單 → 檢視／編輯（標題、用戶名、密碼、URL、備註、自訂欄位）→
/// 新增／刪除群組同項目 → 儲存返 .kdbx → 內建密碼產生器 → 複製（12 秒自動清除剪貼簿）→ 搜尋 → 鎖定。
/// Open/create .kdbx, browse the group tree and entry list, view/edit entries, add/delete groups and
/// entries, save back to .kdbx, a built-in generator, copy with ~12s clipboard auto-clear, search,
/// and lock (which clears all decrypted data from memory). Bilingual throughout. No external program is
/// ever launched — KDBX is read and written natively via Services/KeePassService.cs.
/// </summary>
public sealed partial class KeePassModule : Page
{
    private KeePassDatabase? _db;
    private string? _password;
    private byte[]? _keyFileBytes;
    private KpGroup? _selectedGroup;
    private KpEntry? _selectedEntry;
    private string _search = "";

    private readonly ObservableCollection<EntryRow> _entryRows = new();

    // Clipboard auto-clear (~12s).
    private readonly DispatcherTimer _clipTimer = new() { Interval = TimeSpan.FromSeconds(12) };
    private string? _lastCopied;
    private long _clipboardGeneration;

    public KeePassModule()
    {
        InitializeComponent();
        EntryList.ItemsSource = _entryRows;
        Loc.I.LanguageChanged += OnLang;
        _clipTimer.Tick += async (_, _) => await ClearClipboardIfOursAsync();
        Loaded += (_, _) => Render();
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private void OnLang(object? s, EventArgs e) => Render();

    private void OnUnloaded(object? s, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        _clipTimer.Stop();
        // 離開頁面即鎖，唔留解密資料喺記憶體 · lock on navigate-away so no decrypted data lingers.
        LockNow();
    }

    private void Render()
    {
        Header.Title = "KeePass Vault · 密碼保險庫";
        HeaderBlurb.Text = P(
            "A native local password vault. Open or create a KeePass .kdbx database with a master password (and optional key file), browse groups and entries, generate strong passwords, and copy credentials (the clipboard clears itself after ~12 seconds). The KDBX file is read and written entirely in-app — nothing is launched or uploaded.",
            "原生本機密碼保險庫。用主密碼（同可選鎖匙檔）開啟或者建立 KeePass .kdbx 資料庫，瀏覽群組同項目、產生強密碼、複製帳密（剪貼簿約 12 秒後自動清除）。KDBX 檔完全喺 app 內讀寫 — 唔會啟動或上載任何嘢。");

        VaultHeader.Text = P("Open / create vault · 開啟／建立保險庫", "開啟／建立保險庫");
        BrowseBtn.Content = P("Browse… · 瀏覽…", "瀏覽…");
        KeyFileBtn.Content = P("Key file… · 鎖匙檔…", "鎖匙檔…");
        KeyFileClearBtn.Content = P("Clear · 清除", "清除");
        OpenBtn.Content = P("Open · 開啟", "開啟");
        CreateBtn.Content = P("Create new… · 新建…", "新建…");

        AddEntryLbl.Text = P("Add entry · 新增項目", "新增項目");
        AddGroupLbl.Text = P("Add group · 新增群組", "新增群組");
        GenLbl.Text = P("Generator · 產生器", "密碼產生器");
        SearchBox.PlaceholderText = P("Search entries · 搜尋項目", "搜尋項目");
        SaveLbl.Text = P("Save · 儲存", "儲存");
        SaveAsLbl.Text = P("Save as… · 另存…", "另存…");
        LockLbl.Text = P("Lock · 鎖定", "鎖定");

        GroupsHeader.Text = P("Groups · 群組", "群組");
        EntriesHeader.Text = P("Entries · 項目", "項目");
        EditLbl.Text = P("Edit · 編輯", "編輯");
        EmptyEntriesText.Text = P("No entries in this group.", "呢個群組冇項目。");

        UpdateLockUi();
        if (_selectedEntry is null) RenderDetailPlaceholder();
    }

    private void UpdateLockUi()
    {
        bool open = _db is not null;
        LockState.Text = open ? P("Unlocked · 已解鎖", "已解鎖") : P("Locked · 已鎖定", "已鎖定");
        LockPill.Background = open
            ? new SolidColorBrush(Color.FromArgb(40, 0x6C, 0xCB, 0x5A))
            : (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
        Toolbar.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        MainArea.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        VaultExpander.IsExpanded = !open;
    }

    private void Toast(InfoBarSeverity sev, string message)
    {
        OpenResult.Severity = sev;
        OpenResult.Message = message;
        OpenResult.IsOpen = true;
    }

    // ── File pickers ─────────────────────────────────────────────────────────

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".kdbx");
        if (path is not null) PathBox.Text = path;
    }

    private async void PickKeyFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync();
        if (path is null) return;
        try { _keyFileBytes = await File.ReadAllBytesAsync(path); KeyFileBox.Text = path; }
        catch (Exception ex) { Toast(InfoBarSeverity.Error, ex.Message); }
    }

    private void ClearKeyFile_Click(object sender, RoutedEventArgs e)
    {
        _keyFileBytes = null;
        KeyFileBox.Text = "";
    }

    // ── Open / create ────────────────────────────────────────────────────────

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var path = PathBox.Text;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Toast(InfoBarSeverity.Warning, P("Choose a .kdbx file first.", "請先揀一個 .kdbx 檔。"));
            return;
        }
        if (string.IsNullOrEmpty(MasterBox.Password) && _keyFileBytes is null)
        {
            Toast(InfoBarSeverity.Warning, P("Enter a master password or pick a key file.", "輸入主密碼或者揀鎖匙檔。"));
            return;
        }
        OpenBusy.IsActive = true; OpenBtn.IsEnabled = false;
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var pw = MasterBox.Password;
            var kf = _keyFileBytes;
            var db = await Task.Run(() => KeePassDatabase.Load(bytes, pw, kf));
            db.FilePath = path;
            _db = db; _password = pw; // keep credentials in memory until lock
            MasterBox.Password = "";
            Toast(InfoBarSeverity.Success, P($"Opened. KDBX {db.Major}.{db.Minor}.", $"已開啟。KDBX {db.Major}.{db.Minor}。"));
            LoadTree();
        }
        catch (Exception ex)
        {
            Toast(InfoBarSeverity.Error, ex.Message);
        }
        finally { OpenBusy.IsActive = false; OpenBtn.IsEnabled = true; UpdateLockUi(); }
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Header = P("Database name · 資料庫名稱", "資料庫名稱"), Text = "My Vault" };
        var pw1 = new PasswordBox { Header = P("Master password · 主密碼", "主密碼") };
        var pw2 = new PasswordBox { Header = P("Confirm password · 確認密碼", "確認密碼") };
        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(nameBox); panel.Children.Add(pw1); panel.Children.Add(pw2);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Create new vault", "新建保險庫"),
            Content = panel,
            PrimaryButtonText = P("Create · 建立", "建立"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        if (string.IsNullOrEmpty(pw1.Password))
        {
            Toast(InfoBarSeverity.Warning, P("A master password is required.", "需要主密碼。"));
            return;
        }
        if (pw1.Password != pw2.Password)
        {
            Toast(InfoBarSeverity.Error, P("Passwords do not match.", "兩次密碼唔一樣。"));
            return;
        }
        var savePath = await FileDialogs.SaveFileAsync(
            (string.IsNullOrWhiteSpace(nameBox.Text) ? "vault" : nameBox.Text.Trim()) + ".kdbx", ".kdbx");
        if (savePath is null) return;
        try
        {
            var db = KeePassDatabase.CreateNew(nameBox.Text);
            db.FilePath = savePath;
            var pw = pw1.Password;
            var bytes = await Task.Run(() => db.Save(pw, null));
            await File.WriteAllBytesAsync(savePath, bytes);
            _db = db; _password = pw; _keyFileBytes = null; KeyFileBox.Text = "";
            PathBox.Text = savePath;
            Toast(InfoBarSeverity.Success, P("Vault created and saved.", "保險庫已建立並儲存。"));
            LoadTree();
        }
        catch (Exception ex) { Toast(InfoBarSeverity.Error, ex.Message); }
        finally { UpdateLockUi(); }
    }

    // ── Lock ───────────────────────────────────────────────────────────────────

    private void Lock_Click(object sender, RoutedEventArgs e) { LockNow(); UpdateLockUi(); }

    private void LockNow()
    {
        _ = ClearClipboardIfOursAsync();
        _db = null;
        _password = null;
        _keyFileBytes = null;
        KeyFileBox.Text = "";
        _selectedGroup = null;
        _selectedEntry = null;
        _entryRows.Clear();
        GroupsTree.RootNodes.Clear();
        DetailPanel.Children.Clear();
        EditBtn.IsEnabled = DeleteEntryBtn.IsEnabled = false;
    }

    // ── Tree ───────────────────────────────────────────────────────────────────

    private void LoadTree()
    {
        if (_db is null) return;
        GroupsTree.RootNodes.Clear();
        var rootNode = BuildNode(_db.Root);
        GroupsTree.RootNodes.Add(rootNode);
        rootNode.IsExpanded = true;
        _selectedGroup = _db.Root;
        RefreshEntryList();
        UpdateLockUi();
    }

    private TreeViewNode BuildNode(KpGroup g)
    {
        var node = new TreeViewNode { Content = g, IsExpanded = true };
        foreach (var sub in g.Groups) node.Children.Add(BuildNode(sub));
        return node;
    }

    private void GroupsTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode { Content: KpGroup g })
        {
            _selectedGroup = g;
            RefreshEntryList();
        }
    }

    private void RefreshEntryList()
    {
        _entryRows.Clear();
        if (_selectedGroup is null) return;
        IEnumerable<KpEntry> entries;
        if (!string.IsNullOrWhiteSpace(_search))
            entries = AllEntries(_db!.Root).Where(Match);
        else
            entries = _selectedGroup.Entries;
        foreach (var en in entries.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
            _entryRows.Add(new EntryRow(en));
        EmptyEntries.Visibility = _entryRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EntryList.Visibility = _entryRows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        EntriesHeader.Text = string.IsNullOrWhiteSpace(_search)
            ? P($"Entries · 項目 ({_entryRows.Count})", $"項目 ({_entryRows.Count})")
            : P($"Search results · 搜尋結果 ({_entryRows.Count})", $"搜尋結果 ({_entryRows.Count})");
    }

    private bool Match(KpEntry e)
    {
        var q = _search.Trim();
        return e.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
            || e.UserName.Contains(q, StringComparison.OrdinalIgnoreCase)
            || e.Url.Contains(q, StringComparison.OrdinalIgnoreCase)
            || e.Notes.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<KpEntry> AllEntries(KpGroup g)
    {
        foreach (var e in g.Entries) yield return e;
        foreach (var sub in g.Groups)
            foreach (var e in AllEntries(sub)) yield return e;
    }

    private void Search_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _search = sender.Text ?? "";
        RefreshEntryList();
    }

    // ── Entry selection / detail ─────────────────────────────────────────────

    private void EntryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedEntry = (EntryList.SelectedItem as EntryRow)?.Entry;
        EditBtn.IsEnabled = DeleteEntryBtn.IsEnabled = _selectedEntry is not null;
        if (_selectedEntry is null) { RenderDetailPlaceholder(); return; }
        RenderDetail(_selectedEntry);
    }

    private void RenderDetailPlaceholder()
    {
        DetailHeader.Text = P("Details · 詳情", "詳情");
        DetailPanel.Children.Clear();
        DetailPanel.Children.Add(new TextBlock
        {
            Text = P("Select an entry to view its details.", "揀一個項目睇詳情。"),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private void RenderDetail(KpEntry e)
    {
        DetailHeader.Text = string.IsNullOrEmpty(e.Title) ? P("(untitled) · （無標題）", "（無標題）") : e.Title;
        DetailPanel.Children.Clear();

        AddField(P("Username · 用戶名", "用戶名"), e.UserName, copy: e.UserName,
            copyToast: P("Username copied (clears in 12s)", "已複製用戶名（12 秒後清除）"));
        AddPasswordField(P("Password · 密碼", "密碼"), e.Password);
        AddField(P("URL · 網址", "網址"), e.Url, copy: e.Url, isLink: true,
            copyToast: P("URL copied", "已複製網址"));
        if (!string.IsNullOrEmpty(e.Notes))
            AddField(P("Notes · 備註", "備註"), e.Notes, multiline: true);
        foreach (var kv in e.CustomFields)
            AddField(kv.Key, kv.Value.Protected ? "••••••••" : kv.Value.Value,
                copy: kv.Value.Value, copyToast: P("Field copied", "已複製欄位"), masked: kv.Value.Protected);
    }

    private void AddField(string label, string value, string? copy = null, bool multiline = false,
        bool isLink = false, bool masked = false, string? copyToast = null)
    {
        if (string.IsNullOrEmpty(value) && copy is null) return;
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var tb = new TextBlock
        {
            Text = string.IsNullOrEmpty(value) ? "—" : value,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            TextTrimming = multiline ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            IsTextSelectionEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(tb, 0);
        row.Children.Add(tb);
        if (copy is { Length: > 0 })
        {
            var btn = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 } };
            ToolTipService.SetToolTip(btn, P("Copy · 複製", "複製"));
            btn.Click += (_, _) => CopyToClipboard(copy, copyToast ?? P("Copied", "已複製"), persistent: !masked && !IsSecret(label));
            Grid.SetColumn(btn, 1);
            row.Children.Add(btn);
        }
        panel.Children.Add(row);
        DetailPanel.Children.Add(panel);
    }

    private void AddPasswordField(string label, string password)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = label, FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var tb = new TextBlock
        {
            Text = string.IsNullOrEmpty(password) ? "—" : new string('•', Math.Min(password.Length, 16)),
            FontFamily = new FontFamily("Consolas"),
            IsTextSelectionEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        bool revealed = false;
        Grid.SetColumn(tb, 0);
        var reveal = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 } };
        ToolTipService.SetToolTip(reveal, P("Show / hide · 顯示／隱藏", "顯示／隱藏"));
        reveal.Click += (_, _) =>
        {
            revealed = !revealed;
            tb.Text = revealed
                ? (string.IsNullOrEmpty(password) ? "—" : password)
                : (string.IsNullOrEmpty(password) ? "—" : new string('•', Math.Min(password.Length, 16)));
        };
        Grid.SetColumn(reveal, 1);
        var copy = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 } };
        ToolTipService.SetToolTip(copy, P("Copy · 複製", "複製"));
        copy.Click += (_, _) => CopyToClipboard(password, P("Password copied (clears in 12s)", "已複製密碼（12 秒後清除）"), persistent: false);
        Grid.SetColumn(copy, 2);
        row.Children.Add(tb); row.Children.Add(reveal); row.Children.Add(copy);
        panel.Children.Add(row);
        DetailPanel.Children.Add(panel);
    }

    private static bool IsSecret(string label) =>
        label.Contains("Password", StringComparison.OrdinalIgnoreCase) || label.Contains("密碼");

    // ── Clipboard ──────────────────────────────────────────────────────────────

    private void CopyToClipboard(string value, string toast, bool persistent)
    {
        if (string.IsNullOrEmpty(value)) return;
        try
        {
            var dp = new DataPackage();
            dp.SetText(value);
            Clipboard.SetContent(dp);
            _clipboardGeneration++;
            _clipTimer.Stop();
            if (!persistent)
            {
                _lastCopied = value;
                _clipTimer.Start();
            }
            else _lastCopied = null;
            Toast(InfoBarSeverity.Success, toast);
        }
        catch (Exception ex) { Toast(InfoBarSeverity.Error, ex.Message); }
    }

    private async Task ClearClipboardIfOursAsync()
    {
        _clipTimer.Stop();
        var ownedText = _lastCopied;
        var generation = _clipboardGeneration;
        if (string.IsNullOrEmpty(ownedText)) return;
        try
        {
            var view = Clipboard.GetContent();
            if (view is not null && view.Contains(StandardDataFormats.Text))
            {
                var currentText = await view.GetTextAsync();
                // The text and generation must still be ours. This preserves
                // clipboard content copied by the user after a secret copy.
                if (generation == _clipboardGeneration && ClipboardOwnership.CanClearText(ownedText, currentText))
                    Clipboard.Clear();
            }
        }
        catch { }
        finally
        {
            if (generation == _clipboardGeneration) _lastCopied = null;
        }
    }

    // ── Add / edit / delete groups & entries ─────────────────────────────────

    private async void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_db is null || _selectedGroup is null) return;
        var nameBox = new TextBox { Header = P("Group name · 群組名稱", "群組名稱") };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P("Add group", "新增群組"), Content = nameBox,
            PrimaryButtonText = P("Add · 加入", "加入"), CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(nameBox.Text)) return;
        _selectedGroup.Groups.Add(new KpGroup { Name = nameBox.Text.Trim() });
        LoadTree();
        Toast(InfoBarSeverity.Informational, P("Group added. Remember to Save.", "已新增群組。記得儲存。"));
    }

    private async void AddEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_db is null || _selectedGroup is null) return;
        var entry = new KpEntry { Title = "" };
        if (await EditEntryDialog(entry, isNew: true))
        {
            _selectedGroup.Entries.Add(entry);
            RefreshEntryList();
            Toast(InfoBarSeverity.Informational, P("Entry added. Remember to Save.", "已新增項目。記得儲存。"));
        }
    }

    private async void EditEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null) return;
        if (await EditEntryDialog(_selectedEntry, isNew: false))
        {
            RefreshEntryList();
            RenderDetail(_selectedEntry);
            Toast(InfoBarSeverity.Informational, P("Entry updated. Remember to Save.", "已更新項目。記得儲存。"));
        }
    }

    private async Task<bool> EditEntryDialog(KpEntry e, bool isNew)
    {
        var title = new TextBox { Header = P("Title · 標題", "標題"), Text = e.Title };
        var user = new TextBox { Header = P("Username · 用戶名", "用戶名"), Text = e.UserName };
        var pass = new PasswordBox { Header = P("Password · 密碼", "密碼"), Password = e.Password };
        var gen = new Button { Content = P("Generate · 產生", "產生"), VerticalAlignment = VerticalAlignment.Bottom };
        gen.Click += (_, _) => pass.Password = GeneratePassword(20, true, true, true, true);
        var passRow = new Grid { ColumnSpacing = 8 };
        passRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        passRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(pass, 0); Grid.SetColumn(gen, 1);
        passRow.Children.Add(pass); passRow.Children.Add(gen);
        var url = new TextBox { Header = P("URL · 網址", "網址"), Text = e.Url };
        var notes = new TextBox { Header = P("Notes · 備註", "備註"), Text = e.Notes, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70 };

        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
        panel.Children.Add(title); panel.Children.Add(user); panel.Children.Add(passRow);
        panel.Children.Add(url); panel.Children.Add(notes);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isNew ? P("Add entry", "新增項目") : P("Edit entry", "編輯項目"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
            PrimaryButtonText = P("Save · 儲存", "儲存"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return false;
        e.Title = title.Text.Trim();
        e.UserName = user.Text;
        e.Password = pass.Password;
        e.PasswordProtected = true;
        e.Url = url.Text;
        e.Notes = notes.Text;
        return true;
    }

    private async void DeleteEntry_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is null || _selectedGroup is null) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete entry?", "刪除項目？"),
            Content = new TextBlock
            {
                Text = P($"Delete \"{_selectedEntry.Title}\"? This cannot be undone (until you discard changes by locking without saving).",
                    $"刪除「{_selectedEntry.Title}」？除非鎖定時唔儲存，否則無法復原。"),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = P("Delete · 刪除", "刪除"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        // Remove from whichever group actually owns it (search mode shows entries from any group).
        RemoveEntry(_db!.Root, _selectedEntry);
        _selectedEntry = null;
        EditBtn.IsEnabled = DeleteEntryBtn.IsEnabled = false;
        RenderDetailPlaceholder();
        RefreshEntryList();
        Toast(InfoBarSeverity.Informational, P("Entry deleted. Remember to Save.", "已刪除項目。記得儲存。"));
    }

    private static bool RemoveEntry(KpGroup g, KpEntry e)
    {
        if (g.Entries.Remove(e)) return true;
        return g.Groups.Any(sub => RemoveEntry(sub, e));
    }

    // ── Save ───────────────────────────────────────────────────────────────────

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_db is null) return;
        if (string.IsNullOrEmpty(_db.FilePath)) { SaveAs_Click(sender, e); return; }
        await SaveTo(_db.FilePath);
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_db is null) return;
        var name = Path.GetFileName(_db.FilePath) ?? (_db.Root.Name + ".kdbx");
        var path = await FileDialogs.SaveFileAsync(name, ".kdbx");
        if (path is null) return;
        _db.FilePath = path;
        PathBox.Text = path;
        await SaveTo(path);
    }

    private async Task SaveTo(string path)
    {
        if (_db is null) return;
        try
        {
            var pw = _password;
            var kf = _keyFileBytes;
            var db = _db;
            var bytes = await Task.Run(() => db.Save(pw, kf));
            await File.WriteAllBytesAsync(path, bytes);
            Toast(InfoBarSeverity.Success, P("Saved.", "已儲存。"));
        }
        catch (Exception ex) { Toast(InfoBarSeverity.Error, ex.Message); }
    }

    // ── Password generator ───────────────────────────────────────────────────

    private async void Generator_Click(object sender, RoutedEventArgs e)
    {
        var len = new NumberBox { Header = P("Length · 長度", "長度"), Value = 20, Minimum = 4, Maximum = 128,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var upper = new CheckBox { Content = P("Uppercase (A-Z)", "大寫字母 (A-Z)"), IsChecked = true };
        var lower = new CheckBox { Content = P("Lowercase (a-z)", "細寫字母 (a-z)"), IsChecked = true };
        var digits = new CheckBox { Content = P("Digits (0-9)", "數字 (0-9)"), IsChecked = true };
        var symbols = new CheckBox { Content = P("Symbols (!@#…)", "符號 (!@#…)"), IsChecked = true };
        var output = new TextBox { Header = P("Generated · 已產生", "已產生"), IsReadOnly = true, FontFamily = new FontFamily("Consolas") };
        var regen = new Button { Content = P("Regenerate · 重新產生", "重新產生") };
        var copyBtn = new Button { Content = P("Copy · 複製", "複製") };

        void Make()
        {
            output.Text = GeneratePassword((int)len.Value,
                upper.IsChecked == true, lower.IsChecked == true,
                digits.IsChecked == true, symbols.IsChecked == true);
        }
        Make();
        regen.Click += (_, _) => Make();
        copyBtn.Click += (_, _) => CopyToClipboard(output.Text, P("Password copied (clears in 12s)", "已複製密碼（12 秒後清除）"), persistent: false);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        btnRow.Children.Add(regen); btnRow.Children.Add(copyBtn);
        var panel = new StackPanel { Spacing = 10, MinWidth = 380 };
        panel.Children.Add(len); panel.Children.Add(upper); panel.Children.Add(lower);
        panel.Children.Add(digits); panel.Children.Add(symbols); panel.Children.Add(output); panel.Children.Add(btnRow);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P("Password generator", "密碼產生器"), Content = panel,
            CloseButtonText = P("Close · 關閉", "關閉"),
        };
        await dlg.ShowAsync();
    }

    /// <summary>用 RandomNumberGenerator 產生密碼，保證每個揀選嘅類別至少有一個字元。</summary>
    public static string GeneratePassword(int length, bool upper, bool lower, bool digits, bool symbols)
    {
        const string U = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string L = "abcdefghijkmnopqrstuvwxyz";
        const string D = "23456789";
        const string S = "!@#$%^&*()-_=+[]{};:,.?/";
        var pools = new List<string>();
        if (upper) pools.Add(U);
        if (lower) pools.Add(L);
        if (digits) pools.Add(D);
        if (symbols) pools.Add(S);
        if (pools.Count == 0) pools.Add(L);
        if (length < pools.Count) length = pools.Count;

        var all = string.Concat(pools);
        var chars = new char[length];
        // Guarantee one of each selected class.
        for (int i = 0; i < pools.Count; i++)
            chars[i] = pools[i][RandomNumberGenerator.GetInt32(pools[i].Length)];
        for (int i = pools.Count; i < length; i++)
            chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        // Fisher-Yates shuffle with a CSPRNG.
        for (int i = length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }
}

/// <summary>項目清單嘅檢視列 · A row in the entry ListView.</summary>
public sealed class EntryRow
{
    public KpEntry Entry { get; }
    public EntryRow(KpEntry e) { Entry = e; }
    public string Title => string.IsNullOrEmpty(Entry.Title) ? "(untitled)" : Entry.Title;
    public string UserName => Entry.UserName;
}
