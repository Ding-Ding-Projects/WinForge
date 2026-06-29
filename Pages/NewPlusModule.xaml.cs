using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// New+ · 由範本建立檔案／資料夾 · Create files and folders from user-defined templates
/// (a native clone of PowerToys New+). 列出範本、加入／新建／改名／刪除範本、開啟範本資料夾，
/// 以及「由範本建立」（揀目標 + 範本 → 複製，支援改名同日期／變數替換）。
/// Lists templates; add/create/rename/delete templates; open the templates folder; and
/// "create from template" (pick a destination + template → copy, with optional rename and
/// date/variable substitution). Best-effort Explorer "New" menu registration. Bilingual.
/// </summary>
public sealed partial class NewPlusModule : Page
{
    /// <summary>列表項目嘅顯示包裝 · Display wrapper bound to the templates ListView.</summary>
    public sealed class Row
    {
        public NewPlusService.TemplateItem Item { get; init; } = null!;
        public string DisplayName => Item.DisplayName;
        public string Glyph => Item.IsFolder ? "" : "";
        public string KindLabel => Item.IsFolder
            ? Loc.I.Pick("Folder", "資料夾")
            : (string.IsNullOrEmpty(Item.Extension) ? Loc.I.Pick("File", "檔案") : Item.Extension);
        public string SubTitle
        {
            get
            {
                var size = NewPlusService.FormatSize(Item.SizeBytes);
                var when = Item.Modified == DateTime.MinValue ? "" : Item.Modified.ToString("yyyy-MM-dd HH:mm");
                return $"{Item.FileName}  ·  {size}  ·  {when}";
            }
        }
    }

    private List<Row> _rows = new();
    private string _dest = "";

    public NewPlusModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); Reload(); };
        Loaded += (_, _) =>
        {
            Render();
            if (string.IsNullOrEmpty(_dest))
            {
                _dest = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                DestBox.Text = _dest;
            }
            VarsCheck.IsChecked = NewPlusService.ReplaceVariables;
            SeedDefaultTemplatesIfEmpty();
            Reload();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "New+ · 範本新增";
        HeaderBlurb.Text = P("Define templates (files or whole folders) once, then create copies anywhere — with optional rename and date/variable substitution like $YYYY-$MM-$DD.",
            "一次過定義範本（檔案或者成個資料夾），之後喺任何地方複製建立 — 可以改名同做日期／變數替換，例如 $YYYY-$MM-$DD。");

        AddBtnText.Text = P("Add template…", "加入範本…");
        NewBlankBtnText.Text = P("New blank…", "新建空白…");
        RenameBtnText.Text = P("Rename", "改名");
        DeleteBtnText.Text = P("Delete", "刪除");
        OpenFolderBtnText.Text = P("Open templates folder", "開啟範本資料夾");
        RefreshBtnText.Text = P("Refresh", "重新整理");
        SettingsBtnText.Text = P("Settings", "設定");

        CreateTitle.Text = P("Create from template", "由範本建立");
        SelectedCap.Text = P("Selected template", "已選範本");
        DestCap.Text = P("Destination folder", "目標資料夾");
        DestBtn.Content = P("Browse…", "瀏覽…");
        NameCap.Text = P("New name (optional — variables allowed)", "新名稱（可選 — 可用變數）");
        VarsCheck.Content = P("Substitute date/variables in name", "替換名稱中嘅日期／變數");
        CreateBtn.Content = P("Create", "建立");
        VarsHelpBtn.Content = P("Variable reference…", "變數說明…");

        ExplorerCap.Text = P("Explorer \"New\" menu", "檔案總管「新增」選單");
        ExplorerBlurb.Text = P("Best-effort: copies the selected file template into the legacy Windows Templates folder and registers it under HKCU ShellNew, so it appears in Explorer's classic New submenu (\"Show more options\" on Windows 11). Modern Win11 shell integration needs a packaged shell extension and is not available unpackaged.",
            "盡力而為：將已選嘅檔案範本複製去舊式 Windows Templates 資料夾，並喺 HKCU ShellNew 註冊，等佢出現喺檔案總管嘅傳統「新增」子選單（Windows 11 要按「顯示更多選項」）。Win11 新式整合需要已封裝嘅 shell 擴充功能，未封裝做唔到。");
        RegisterBtn.Content = P("Register selected in New menu", "將已選範本加入「新增」選單");

        UpdateSelectedUi();
        UpdatePreview();
    }

    private void Reload()
    {
        var items = NewPlusService.ListTemplates();
        _rows = items.Select(i => new Row { Item = i }).ToList();
        var keepPath = SelectedItem?.Path;
        TemplatesList.ItemsSource = _rows;

        if (_rows.Count == 0)
        {
            EmptyHint.Visibility = Visibility.Visible;
            EmptyHint.Text = P("No templates yet. Use “Add template…” to copy in an existing file or folder, or “New blank…” to create one.",
                "未有範本。用「加入範本…」複製現有檔案或資料夾，或者用「新建空白…」建立一個。");
        }
        else
        {
            EmptyHint.Visibility = Visibility.Collapsed;
            // Restore previous selection if still present.
            var restore = _rows.FirstOrDefault(r => r.Item.Path == keepPath);
            TemplatesList.SelectedItem = restore;
        }
        UpdateSelectedUi();
        UpdatePreview();
    }

    private NewPlusService.TemplateItem? SelectedItem => (TemplatesList.SelectedItem as Row)?.Item;

    private void TemplatesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedUi();
        UpdatePreview();
    }

    private void UpdateSelectedUi()
    {
        var sel = SelectedItem;
        var has = sel is not null;
        RenameBtn.IsEnabled = has;
        DeleteBtn.IsEnabled = has;
        CreateBtn.IsEnabled = has;
        RegisterBtn.IsEnabled = has && !sel!.IsFolder;
        SelectedName.Text = has
            ? $"{sel!.DisplayName}  ({(sel.IsFolder ? P("folder", "資料夾") : sel.FileName)})"
            : P("— none —", "— 冇 —");
        if (has && string.IsNullOrEmpty(NameBox.Text))
        {
            // Suggest the template's display name (stripped of leading digits) as the default.
            NameBox.PlaceholderText = NewPlusService.RemoveStartingDigits(sel!.FileName, sel.IsFolder, NewPlusService.HideStartingDigits);
        }
    }

    private void NameBox_Changed(object sender, TextChangedEventArgs e) => UpdatePreview();
    private void Vars_Click(object sender, RoutedEventArgs e)
    {
        NewPlusService.ReplaceVariables = VarsCheck.IsChecked == true;
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var sel = SelectedItem;
        if (sel is null) { PreviewText.Text = ""; return; }
        var raw = string.IsNullOrWhiteSpace(NameBox.Text)
            ? NewPlusService.RemoveStartingDigits(sel.FileName, sel.IsFolder, NewPlusService.HideStartingDigits)
            : NameBox.Text;
        var parent = Directory.Exists(_dest) ? new DirectoryInfo(_dest).Name : "";
        var resolved = (VarsCheck.IsChecked == true)
            ? NewPlusService.ResolveVariables(raw, parent)
            : raw;
        resolved = NewPlusService.SanitizeName(resolved);
        PreviewText.Text = P($"Will create: {resolved}", $"將會建立：{resolved}");
    }

    // ===== Template management =====

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        // Offer file or folder.
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Add template", "加入範本"),
            Content = P("Copy an existing file or folder into your templates?", "將現有檔案或資料夾複製入範本？"),
            PrimaryButtonText = P("Pick a file", "揀檔案"),
            SecondaryButtonText = P("Pick a folder", "揀資料夾"),
            CloseButtonText = P("Cancel", "取消"),
        };
        var r = await dlg.ShowAsync();
        string? src = r switch
        {
            ContentDialogResult.Primary => await FileDialogs.OpenFileAsync(),
            ContentDialogResult.Secondary => await FileDialogs.OpenFolderAsync(),
            _ => null,
        };
        if (string.IsNullOrEmpty(src)) return;
        var (ok, msg) = NewPlusService.AddTemplateFromPath(src);
        if (ok) Info(P("Template added.", "範本已加入。"), InfoBarSeverity.Success);
        else Info(P($"Could not add template: {msg}", $"加入範本失敗：{msg}"), InfoBarSeverity.Error);
        Reload();
    }

    private async void NewBlank_Click(object sender, RoutedEventArgs e)
    {
        var input = new TextBox { PlaceholderText = P("e.g. Notes.txt or My Folder", "例如 Notes.txt 或 我的資料夾") };
        var combo = new ComboBox { MinWidth = 140 };
        combo.Items.Add(new ComboBoxItem { Content = P("File", "檔案"), Tag = "file" });
        combo.Items.Add(new ComboBoxItem { Content = P("Folder", "資料夾"), Tag = "folder" });
        combo.SelectedIndex = 0;
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = P("Name (include an extension for files)", "名稱（檔案請加副檔名）") });
        panel.Children.Add(input);
        panel.Children.Add(combo);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("New blank template", "新建空白範本"),
            Content = panel,
            PrimaryButtonText = P("Create", "建立"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var name = input.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name)) return;
        var isFolder = (combo.SelectedItem as ComboBoxItem)?.Tag as string == "folder";
        var (ok, msg) = isFolder
            ? NewPlusService.CreateBlankFolderTemplate(name)
            : NewPlusService.CreateBlankFileTemplate(name);
        if (ok) Info(P("Blank template created.", "已建立空白範本。"), InfoBarSeverity.Success);
        else Info(P($"Could not create template: {msg}", $"建立範本失敗：{msg}"), InfoBarSeverity.Error);
        Reload();
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        var sel = SelectedItem;
        if (sel is null) return;
        var input = new TextBox { Text = sel.FileName, SelectionStart = 0, SelectionLength = sel.FileName.Length };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Rename template", "重新命名範本"),
            Content = input,
            PrimaryButtonText = P("Rename", "改名"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var (ok, msg) = NewPlusService.RenameTemplate(sel.Path, input.Text?.Trim() ?? "");
        if (ok) Info(P("Renamed.", "已改名。"), InfoBarSeverity.Success);
        else Info(P($"Could not rename: {msg}", $"改名失敗：{msg}"), InfoBarSeverity.Error);
        Reload();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var sel = SelectedItem;
        if (sel is null) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete template?", "刪除範本？"),
            Content = P($"Permanently delete “{sel.DisplayName}”?", $"永久刪除「{sel.DisplayName}」？"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var (ok, msg) = NewPlusService.DeleteTemplate(sel.Path);
        if (ok) Info(P("Deleted.", "已刪除。"), InfoBarSeverity.Success);
        else Info(P($"Could not delete: {msg}", $"刪除失敗：{msg}"), InfoBarSeverity.Error);
        Reload();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = NewPlusService.EnsureTemplatesFolder();
        try { Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true }); }
        catch (Exception ex) { Info(P($"Could not open folder: {ex.Message}", $"開唔到資料夾：{ex.Message}"), InfoBarSeverity.Error); }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    // ===== Create from template =====

    private async void BrowseDest_Click(object sender, RoutedEventArgs e)
    {
        var f = await FileDialogs.OpenFolderAsync(P("Pick a destination folder", "揀目標資料夾"));
        if (f is not null) { _dest = f; DestBox.Text = f; UpdatePreview(); }
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        var sel = SelectedItem;
        if (sel is null) { Info(P("Pick a template first.", "請先揀範本。"), InfoBarSeverity.Warning); return; }
        if (string.IsNullOrEmpty(_dest) || !Directory.Exists(_dest))
        {
            Info(P("Pick a destination folder first.", "請先揀目標資料夾。"), InfoBarSeverity.Warning);
            return;
        }
        var name = string.IsNullOrWhiteSpace(NameBox.Text) ? null : NameBox.Text.Trim();
        var (ok, msg, created) = NewPlusService.CreateFromTemplate(sel, _dest, name, VarsCheck.IsChecked == true);
        if (ok)
        {
            Info(P($"Created: {Path.GetFileName(created)}", $"已建立：{Path.GetFileName(created)}"), InfoBarSeverity.Success);
            ResultBar.ActionButton = new Button
            {
                Content = P("Show in folder", "喺資料夾顯示"),
            };
            ((Button)ResultBar.ActionButton).Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = $"/select,\"{created}\"", UseShellExecute = true }); } catch { }
            };
        }
        else Info(P($"Could not create: {msg}", $"建立失敗：{msg}"), InfoBarSeverity.Error);
    }

    private async void VarsHelp_Click(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock
        {
            Text = P("Tokens are replaced at create time (case-sensitive). Use $$ for a literal $.",
                "建立時會替換以下變數（區分大小寫）。用 $$ 表示一個字面 $。"),
            TextWrapping = TextWrapping.Wrap,
        });
        var grid = new Grid { ColumnSpacing = 14, RowSpacing = 2 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int r = 0;
        foreach (var (token, en, zh) in NewPlusService.SupportedVariables)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var t = new TextBlock { Text = token, FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") };
            Grid.SetRow(t, r); Grid.SetColumn(t, 0);
            var m = new TextBlock { Text = P(en, zh), TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(m, r); Grid.SetColumn(m, 1);
            grid.Children.Add(t); grid.Children.Add(m);
            r++;
        }
        panel.Children.Add(new ScrollViewer { Content = grid, MaxHeight = 360 });

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Name variables", "名稱變數"),
            Content = panel,
            CloseButtonText = P("Close", "關閉"),
        };
        await dlg.ShowAsync();
    }

    // ===== Settings =====

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var pathBox = new TextBox { Text = NewPlusService.TemplatesFolder, MinWidth = 360 };
        var browse = new Button { Content = P("Browse…", "瀏覽…") };
        browse.Click += async (_, _) =>
        {
            var f = await FileDialogs.OpenFolderAsync(P("Pick a templates folder", "揀範本資料夾"));
            if (f is not null) pathBox.Text = f;
        };
        var pathRow = new Grid { ColumnSpacing = 8 };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(pathBox, 0); Grid.SetColumn(browse, 1);
        pathRow.Children.Add(pathBox); pathRow.Children.Add(browse);

        var hideExt = new CheckBox { Content = P("Hide file extension in list", "列表隱藏副檔名"), IsChecked = NewPlusService.HideExtension };
        var hideDigits = new CheckBox { Content = P("Hide leading sort digits (e.g. \"01. Notes\")", "隱藏開頭排序數字（例如「01. Notes」）"), IsChecked = NewPlusService.HideStartingDigits };
        var resetBtn = new Button { Content = P("Reset to default location", "重設為預設位置") };
        resetBtn.Click += (_, _) => pathBox.Text = NewPlusService.DefaultTemplatesFolder;

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = P("Templates folder", "範本資料夾") });
        panel.Children.Add(pathRow);
        panel.Children.Add(resetBtn);
        panel.Children.Add(hideExt);
        panel.Children.Add(hideDigits);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("New+ settings", "New+ 設定"),
            Content = panel,
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var newPath = pathBox.Text?.Trim() ?? "";
        if (!string.IsNullOrEmpty(newPath)) NewPlusService.TemplatesFolder = newPath;
        NewPlusService.HideExtension = hideExt.IsChecked == true;
        NewPlusService.HideStartingDigits = hideDigits.IsChecked == true;
        NewPlusService.EnsureTemplatesFolder();
        Reload();
    }

    // ===== Explorer "New" menu =====

    private async void Register_Click(object sender, RoutedEventArgs e)
    {
        var sel = SelectedItem;
        if (sel is null || sel.IsFolder)
        {
            Info(P("Select a file template to register.", "請揀一個檔案範本嚟註冊。"), InfoBarSeverity.Warning);
            return;
        }
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Register in Explorer New menu?", "加入檔案總管「新增」選單？"),
            Content = P($"This copies “{sel.FileName}” into the legacy Windows Templates folder and registers the {sel.Extension} ShellNew handler. It affects all {sel.Extension} files in the New menu.",
                $"呢個會將「{sel.FileName}」複製去舊式 Windows Templates 資料夾，並註冊 {sel.Extension} 嘅 ShellNew 處理常式。會影響「新增」選單入面所有 {sel.Extension} 檔案。"),
            PrimaryButtonText = P("Register", "註冊"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var (ok, msg) = NewPlusService.RegisterInExplorerNewMenu(sel);
        if (ok) Info(P($"Registered ({msg}). Open Explorer's New menu (Show more options on Win11) to see it.",
            $"已註冊（{msg}）。喺檔案總管「新增」選單（Win11 按「顯示更多選項」）就見到。"), InfoBarSeverity.Success);
        else Info(P($"Could not register: {msg}", $"註冊失敗：{msg}"), InfoBarSeverity.Error);
    }

    // ===== Helpers =====

    private void SeedDefaultTemplatesIfEmpty()
    {
        try
        {
            var root = NewPlusService.EnsureTemplatesFolder();
            var hasAny = Directory.EnumerateFileSystemEntries(root).Any();
            if (hasAny) return;
            // Seed a couple of useful starter templates so the list isn't empty on first run.
            NewPlusService.CreateBlankFileTemplate("Text Document.txt");
            NewPlusService.CreateBlankFileTemplate("Markdown.md");
            NewPlusService.CreateBlankFolderTemplate("Project $YYYY-$MM-$DD");
        }
        catch { /* best effort */ }
    }

    private void Info(string message, InfoBarSeverity severity)
    {
        ResultBar.ActionButton = null;
        ResultBar.Severity = severity;
        ResultBar.Title = severity switch
        {
            InfoBarSeverity.Success => P("Done", "完成"),
            InfoBarSeverity.Error => P("Error", "錯誤"),
            InfoBarSeverity.Warning => P("Heads up", "注意"),
            _ => P("Info", "資訊"),
        };
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }
}
