using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內環境變數編輯器（PowerToys 式）· In-app Environment Variables editor — view/add/edit/delete
/// User and System variables. System scope needs admin. No redirect. Bilingual.
/// </summary>
public sealed partial class EnvVarsModule : Page
{
    public sealed class VarRow
    {
        public string Name { get; init; } = "";
        public string Value { get; init; } = "";
    }

    private readonly ObservableCollection<VarRow> _rows = new();

    public EnvVarsModule()
    {
        InitializeComponent();
        List.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); RefreshList(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private bool Machine => TargetBox.SelectedIndex == 1;

    private void Render()
    {
        Header.Title = "Environment Variables · 環境變數";
        HeaderBlurb.Text = P("View, add, edit and delete environment variables. User variables apply without admin; System variables need WinForge running as administrator.",
            "檢視、新增、編輯同刪除環境變數。使用者變數唔使管理員；系統變數要 WinForge 以管理員身分執行。");
        NameBox.PlaceholderText = P("Variable name", "變數名稱");
        ValueBox.PlaceholderText = P("Value", "值");
        AddBtn.Content = P("Set", "設定");
        ColName.Text = P("Name", "名稱");
        ColValue.Text = P("Value", "值");

        int sel = TargetBox.SelectedIndex < 0 ? 0 : TargetBox.SelectedIndex;
        TargetBox.Items.Clear();
        TargetBox.Items.Add(P("User variables", "使用者變數"));
        TargetBox.Items.Add(P("System variables (admin)", "系統變數（管理員）"));
        TargetBox.SelectedIndex = sel;
    }

    private void RefreshList()
    {
        _rows.Clear();
        foreach (var v in EnvVarService.List(Machine))
            _rows.Add(new VarRow { Name = v.Name, Value = v.Value });
        ColValue.Text = P($"Value — {_rows.Count}", $"值 — {_rows.Count} 個");
    }

    private void Target_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) RefreshList(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string name = (NameBox.Text ?? "").Trim();
        string value = ValueBox.Text ?? "";
        if (name.Length == 0) { Show(InfoBarSeverity.Warning, P("Enter a variable name", "請輸入變數名稱"), ""); return; }
        try
        {
            EnvVarService.Set(name, value, Machine);
            Show(InfoBarSeverity.Success, P("Saved", "已儲存"), $"{name} = {value}");
            NameBox.Text = ""; ValueBox.Text = "";
            RefreshList();
        }
        catch (Exception ex)
        {
            Show(InfoBarSeverity.Error, P("Could not save", "儲存唔到"),
                Machine ? P("System variables need administrator rights — relaunch WinForge as admin.", "系統變數要管理員權限 — 請以管理員身分重開 WinForge。") : ex.Message);
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VarRow row) return;
        // PATH-style multi-entry variables open a friendly per-entry editor; simple ones edit inline.
        if (row.Value.Contains(';') && row.Value.Split(';', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            await ShowListEditor(row);
        else
        {
            NameBox.Text = row.Name;
            ValueBox.Text = row.Value;
        }
    }

    /// <summary>Edit a semicolon-list variable (PATH, PATHEXT…) as one entry per row — add, remove, reorder.</summary>
    private async Task ShowListEditor(VarRow row)
    {
        var entries = row.Value.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        var rowsHost = new StackPanel { Spacing = 4 };
        void Rebuild()
        {
            rowsHost.Children.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                int idx = i;
                var g = new Grid { ColumnSpacing = 6 };
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var tb = new TextBox { Text = entries[idx], FontFamily = new FontFamily("Consolas"), FontSize = 12 };
                tb.TextChanged += (_, _) => { if (idx < entries.Count) entries[idx] = tb.Text; };
                Grid.SetColumn(tb, 0);

                var up = IconBtn(((char)0xE70E).ToString(), () => { if (idx > 0) { (entries[idx - 1], entries[idx]) = (entries[idx], entries[idx - 1]); Rebuild(); } });
                Grid.SetColumn(up, 1);
                var down = IconBtn(((char)0xE70D).ToString(), () => { if (idx < entries.Count - 1) { (entries[idx + 1], entries[idx]) = (entries[idx], entries[idx + 1]); Rebuild(); } });
                Grid.SetColumn(down, 2);
                var del = IconBtn(((char)0xE74D).ToString(), () => { entries.RemoveAt(idx); Rebuild(); });
                Grid.SetColumn(del, 3);

                g.Children.Add(tb); g.Children.Add(up); g.Children.Add(down); g.Children.Add(del);
                rowsHost.Children.Add(g);
            }
        }
        Rebuild();

        var addBox = new TextBox { PlaceholderText = P("Add an entry…", "新增一個項目…"), FontFamily = new FontFamily("Consolas") };
        var addBtn = new Button { Content = P("Add", "新增") };
        addBtn.Click += (_, _) => { var v = addBox.Text?.Trim(); if (!string.IsNullOrEmpty(v)) { entries.Add(v); addBox.Text = ""; Rebuild(); } };
        var browse = new Button { Content = P("Browse…", "瀏覽…") };
        browse.Click += async (_, _) =>
        {
            var folder = await FileDialogs.OpenFolderAsync();
            if (folder is not null) { entries.Add(folder); Rebuild(); }
        };
        var addG = new Grid { ColumnSpacing = 6 };
        addG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        addG.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(addBox, 0); Grid.SetColumn(addBtn, 1); Grid.SetColumn(browse, 2);
        addG.Children.Add(addBox); addG.Children.Add(addBtn); addG.Children.Add(browse);

        var panel = new StackPanel { Spacing = 10, MinWidth = 640 };
        panel.Children.Add(new TextBlock
        {
            Text = P("One entry per row — reorder with the arrows, remove with the bin.", "一行一個項目 — 用箭咀調次序、用垃圾桶移除。"),
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new ScrollViewer { Content = rowsHost, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        panel.Children.Add(addG);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Edit {row.Name}", $"編輯 {row.Name}"),
            Content = panel,
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            var joined = string.Join(";", entries.Select(s => s.Trim()).Where(s => s.Length > 0));
            try { EnvVarService.Set(row.Name, joined, Machine); Show(InfoBarSeverity.Success, P("Saved", "已儲存"), row.Name); RefreshList(); }
            catch (Exception ex)
            {
                Show(InfoBarSeverity.Error, P("Could not save", "儲存唔到"),
                    Machine ? P("System variables need administrator rights.", "系統變數要管理員權限。") : ex.Message);
            }
        }
    }

    private static Button IconBtn(string glyph, Action onClick)
    {
        var b = new Button { Padding = new Thickness(7, 3, 7, 3), Content = new FontIcon { Glyph = glyph, FontSize = 11 } };
        b.Click += (_, _) => onClick();
        return b;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not VarRow row) return;
        try { EnvVarService.Delete(row.Name, Machine); RefreshList(); }
        catch (Exception ex)
        {
            Show(InfoBarSeverity.Error, P("Could not delete", "刪唔到"),
                Machine ? P("System variables need administrator rights.", "系統變數要管理員權限。") : ex.Message);
        }
    }

    private void Show(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev; ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
