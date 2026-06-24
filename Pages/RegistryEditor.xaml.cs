using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內嘅登錄編輯器（唔係叫出 regedit）· An in-app Registry Editor (no redirect to regedit.exe):
/// lazy tree browse + view/add/edit/delete values, fully bilingual.
/// </summary>
public sealed partial class RegistryEditor : Page
{
    private sealed record RegNode(RegRoot Root, string Path);

    private sealed class ValueRow
    {
        public string Name { get; init; } = "";
        public string RealName { get; init; } = "";
        public RegistryValueKind Kind { get; init; }
        public object? Data { get; init; }
        public string TypeText { get; init; } = "";
        public string DataText { get; init; } = "";
    }

    private readonly Dictionary<TreeViewNode, RegNode> _info = new();
    private RegNode? _current;

    public RegistryEditor()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) =>
        {
            Render();
            BuildRoots();
            // 預設載入一個有值嘅機碼 · preload a populated key so values are visible immediately.
            LoadValues(new RegNode(RegRoot.HKCU, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"));
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Registry Editor · 登錄編輯器";
        NewBtn.Content = P("New value", "新增值");
        EditBtn.Content = P("Edit", "編輯");
        DeleteBtn.Content = P("Delete", "刪除");
        RefreshBtn.Content = P("Refresh", "重新整理");
        if (_current is null)
            PathBar.Text = P("Select a key on the left to view its values.", "喺左邊揀一個機碼睇佢嘅值。");
    }

    private static string Prefix(RegRoot r) => r switch
    {
        RegRoot.HKCU => "HKEY_CURRENT_USER",
        RegRoot.HKLM => "HKEY_LOCAL_MACHINE",
        RegRoot.HKCR => "HKEY_CLASSES_ROOT",
        RegRoot.HKU => "HKEY_USERS",
        _ => r.ToString(),
    };

    private void BuildRoots()
    {
        if (Tree.RootNodes.Count > 0) return;
        foreach (var r in new[] { RegRoot.HKCU, RegRoot.HKLM, RegRoot.HKCR, RegRoot.HKU })
        {
            var node = new TreeViewNode { Content = Prefix(r), HasUnrealizedChildren = true };
            _info[node] = new RegNode(r, "");
            Tree.RootNodes.Add(node);
        }
    }

    private void Tree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
    {
        var node = args.Node;
        if (!node.HasUnrealizedChildren || node.Children.Count > 0) return;
        if (!_info.TryGetValue(node, out var info)) return;

        foreach (var name in RegistryHelper.GetSubKeyNames(info.Root, info.Path).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            var childPath = string.IsNullOrEmpty(info.Path) ? name : info.Path + "\\" + name;
            var child = new TreeViewNode { Content = name, HasUnrealizedChildren = true };
            _info[child] = new RegNode(info.Root, childPath);
            node.Children.Add(child);
        }
        node.HasUnrealizedChildren = false;
    }

    private void Tree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode node && _info.TryGetValue(node, out var info))
            LoadValues(info);
    }

    private void LoadValues(RegNode info)
    {
        _current = info;
        PathBar.Text = Prefix(info.Root) + (string.IsNullOrEmpty(info.Path) ? "" : "\\" + info.Path);
        var rows = new List<ValueRow>();
        foreach (var (name, kind, data) in RegistryHelper.GetValues(info.Root, info.Path))
        {
            rows.Add(new ValueRow
            {
                RealName = name,
                Name = string.IsNullOrEmpty(name) ? "(Default · 預設)" : name,
                Kind = kind,
                Data = data,
                TypeText = TypeText(kind),
                DataText = DataText(kind, data),
            });
        }
        ValuesList.ItemsSource = rows;
        EditBtn.IsEnabled = false;
        DeleteBtn.IsEnabled = false;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_current is not null) LoadValues(_current);
    }

    private void ValuesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool has = ValuesList.SelectedItem is ValueRow;
        EditBtn.IsEnabled = has;
        DeleteBtn.IsEnabled = has;
    }

    private static string TypeText(RegistryValueKind k) => k switch
    {
        RegistryValueKind.String => "REG_SZ",
        RegistryValueKind.ExpandString => "REG_EXPAND_SZ",
        RegistryValueKind.DWord => "REG_DWORD",
        RegistryValueKind.QWord => "REG_QWORD",
        RegistryValueKind.MultiString => "REG_MULTI_SZ",
        RegistryValueKind.Binary => "REG_BINARY",
        _ => k.ToString(),
    };

    private static string DataText(RegistryValueKind kind, object? data)
    {
        if (data is null) return "";
        return kind switch
        {
            RegistryValueKind.DWord => $"0x{Convert.ToInt32(data):X8} ({Convert.ToInt32(data)})",
            RegistryValueKind.QWord => $"0x{Convert.ToInt64(data):X16} ({Convert.ToInt64(data)})",
            RegistryValueKind.MultiString => string.Join(" | ", (string[])data),
            RegistryValueKind.Binary => string.Join(" ", ((byte[])data).Select(b => b.ToString("X2"))),
            _ => data.ToString() ?? "",
        };
    }

    private static object ParseData(RegistryValueKind kind, string text) => kind switch
    {
        RegistryValueKind.DWord => text.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt32(text.Trim()[2..], 16) : int.Parse(text.Trim(), CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => text.Trim().StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt64(text.Trim()[2..], 16) : long.Parse(text.Trim(), CultureInfo.InvariantCulture),
        RegistryValueKind.MultiString => text.Replace("\r", "").Split('\n'),
        RegistryValueKind.Binary => text.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(h => Convert.ToByte(h, 16)).ToArray(),
        _ => text,
    };

    private static string EditableText(RegistryValueKind kind, object? data) => kind switch
    {
        RegistryValueKind.DWord => Convert.ToInt32(data ?? 0).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => Convert.ToInt64(data ?? 0L).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.MultiString => data is string[] a ? string.Join("\n", a) : "",
        RegistryValueKind.Binary => data is byte[] b ? string.Join(" ", b.Select(x => x.ToString("X2"))) : "",
        _ => data?.ToString() ?? "",
    };

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) { ShowErr(P("Select a key first.", "請先揀一個機碼。")); return; }

        var nameBox = new TextBox { Header = P("Value name", "值名稱"), PlaceholderText = "MyValue" };
        var typeBox = new ComboBox { Header = P("Type", "類型"), SelectedIndex = 0 };
        foreach (var k in new[] { RegistryValueKind.String, RegistryValueKind.ExpandString, RegistryValueKind.DWord, RegistryValueKind.QWord, RegistryValueKind.MultiString, RegistryValueKind.Binary })
            typeBox.Items.Add(new ComboBoxItem { Content = TypeText(k), Tag = k });
        typeBox.SelectedIndex = 0;
        var dataBox = new TextBox { Header = P("Data", "資料"), AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(nameBox); panel.Children.Add(typeBox); panel.Children.Add(dataBox);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("New value", "新增值"),
            Content = panel,
            PrimaryButtonText = P("Create", "建立"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var kind = (RegistryValueKind)((ComboBoxItem)typeBox.SelectedItem).Tag;
            var val = ParseData(kind, dataBox.Text ?? "");
            RegistryHelper.SetValue(_current.Root, _current.Path, nameBox.Text ?? "", val, kind);
            LoadValues(_current);
            ShowOk(P("Value created.", "已建立值。"));
        }
        catch (Exception ex) { ShowErr(ex); }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || ValuesList.SelectedItem is not ValueRow row) return;

        var dataBox = new TextBox
        {
            Header = $"{row.Name} · {row.TypeText}",
            Text = EditableText(row.Kind, row.Data),
            AcceptsReturn = row.Kind is RegistryValueKind.MultiString or RegistryValueKind.Binary,
            TextWrapping = TextWrapping.Wrap,
        };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Edit value", "編輯值"),
            Content = dataBox,
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            var val = ParseData(row.Kind, dataBox.Text ?? "");
            RegistryHelper.SetValue(_current.Root, _current.Path, row.RealName, val, row.Kind);
            LoadValues(_current);
            ShowOk(P("Value saved.", "已儲存值。"));
        }
        catch (Exception ex) { ShowErr(ex); }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || ValuesList.SelectedItem is not ValueRow row) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete value?", "刪除值？"),
            Content = $"{row.Name}\n\n" + P("This cannot be undone.", "呢個動作無法復原。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        try
        {
            RegistryHelper.DeleteValue(_current.Root, _current.Path, row.RealName);
            LoadValues(_current);
            ShowOk(P("Value deleted.", "已刪除值。"));
        }
        catch (Exception ex) { ShowErr(ex); }
    }

    private void ShowOk(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private void ShowErr(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private void ShowErr(Exception ex)
    {
        var hint = ex is UnauthorizedAccessException
            ? P("Access denied — relaunch WinForge as administrator to edit this key.", "存取被拒 — 以管理員身分重開 WinForge 先可以改呢個機碼。")
            : ex.Message;
        ShowErr(hint);
    }
}
