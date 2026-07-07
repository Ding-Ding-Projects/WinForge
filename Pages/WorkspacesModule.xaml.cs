using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Text;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 工作區（PowerToys Workspaces 原生複製）· Workspaces — a native clone of PowerToys Workspaces.
/// 擷取目前桌面上嘅一組應用程式視窗、存做有名工作區，再可以一鍵重新啟動所有 app 並還原佢哋嘅
/// 位置同大細。亦可以重新命名、刪除、重新擷取、匯入／匯出，以及逐個 app 編輯（啟用／停用、改範圍、移除）。
/// Captures a set of app windows on the current desktop, saves them as a named workspace, and can
/// relaunch every app one-click while restoring its position/size. Also rename, delete, re-capture,
/// import/export, and per-app editing (enable/disable, adjust bounds, remove). Bilingual.
/// </summary>
public sealed partial class WorkspacesModule : Page
{
    private CancellationTokenSource? _launchCts;

    public WorkspacesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        WorkspacesService.Changed += OnStoreChanged;
        Loaded += (_, _) => { Render(); Reload(); };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            WorkspacesService.Changed -= OnStoreChanged;
            _launchCts?.Cancel();
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Reload(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnStoreChanged(object? sender, EventArgs e)
        => DispatcherQueue.TryEnqueue(Reload);

    // -------------------------------------------------------------- view-model row

    private sealed class WsRow
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public string Subtitle { get; init; } = "";
    }

    // -------------------------------------------------------------- render

    private void Render()
    {
        Header.Title = "Workspaces · 工作區";
        HeaderBlurb.Text = P(
            "Capture the apps open on your desktop as a named workspace, then relaunch them all at once with their windows restored to where they were. Edit each workspace to enable/disable apps, tweak window bounds or remove apps.",
            "將桌面上開住嘅應用程式擷取成一個有名嘅工作區，之後一鍵重新開晒所有 app，仲會還原返視窗嘅位置同大細。可以編輯每個工作區去啟用／停用 app、調整視窗範圍或者移除 app。");
        CaptureBtn.Content = P("Capture current desktop", "擷取目前桌面");
        ImportBtn.Content = P("Import…", "匯入…");
        RefreshBtn.Content = P("Refresh", "重新整理");
        EmptyText.Text = P("No workspaces yet. Open a few apps, then press \"Capture current desktop\".",
            "未有工作區。開幾個 app，再撳「擷取目前桌面」。");

        LaunchBtn.Content = P("Launch", "啟動");
        RecaptureBtn.Content = P("Re-capture", "重新擷取");
        RenameBtn.Content = P("Rename", "重新命名");
        ExportBtn.Content = P("Export…", "匯出…");
        DeleteBtn.Content = P("Delete", "刪除");
    }

    private void Reload()
    {
        var all = WorkspacesService.All;
        var selectedId = (WsList.SelectedItem as WsRow)?.Id ?? CurrentId;

        var rows = all.Select(w => new WsRow
        {
            Id = w.Id,
            Name = w.Name,
            Subtitle = Subtitle(w),
        }).ToList();

        WsList.ItemsSource = rows;
        CountText.Text = P($"{rows.Count} workspaces", $"{rows.Count} 個工作區");
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // re-select previously selected workspace if it still exists
        if (selectedId is not null)
        {
            var match = rows.FirstOrDefault(r => r.Id == selectedId);
            if (match is not null) { WsList.SelectedItem = match; return; }
        }
        if (rows.Count == 0) { CurrentId = null; DetailRoot.Visibility = Visibility.Collapsed; }
    }

    private string Subtitle(Workspace w)
    {
        int n = w.Apps.Count;
        string apps = P($"{n} apps", $"{n} 個 app");
        if (w.LastLaunchedTicks > 0)
        {
            var when = new DateTime(w.LastLaunchedTicks, DateTimeKind.Utc).ToLocalTime();
            return $"{apps} · " + P($"launched {when:g}", $"啟動於 {when:g}");
        }
        return apps;
    }

    // -------------------------------------------------------------- selection / detail

    private string? CurrentId { get; set; }

    private void WsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WsList.SelectedItem is WsRow row)
        {
            CurrentId = row.Id;
            ShowDetail(row.Id);
        }
        else
        {
            CurrentId = null;
            DetailRoot.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowDetail(string id)
    {
        var w = WorkspacesService.Get(id);
        if (w is null) { DetailRoot.Visibility = Visibility.Collapsed; return; }

        DetailRoot.Visibility = Visibility.Visible;
        DetailName.Text = w.Name;
        int enabled = w.Apps.Count(a => a.Enabled);
        DetailMeta.Text = P($"{w.Apps.Count} apps · {enabled} enabled", $"{w.Apps.Count} 個 app · 啟用 {enabled} 個");

        BuildAppsPanel(w);
    }

    private void BuildAppsPanel(Workspace w)
    {
        AppsPanel.Children.Clear();
        if (w.Apps.Count == 0)
        {
            AppsPanel.Children.Add(new TextBlock
            {
                Text = P("This workspace has no apps. Use Re-capture while the apps you want are open.",
                    "呢個工作區冇 app。當你想要嘅 app 開住時撳「重新擷取」。"),
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }

        for (int i = 0; i < w.Apps.Count; i++)
        {
            var app = w.Apps[i];
            AppsPanel.Children.Add(BuildAppCard(w, app));
        }
    }

    private Border BuildAppCard(Workspace w, WorkspaceApp app)
    {
        var card = new Border
        {
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
        };

        var root = new Grid { ColumnSpacing = 10 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // enable toggle
        var toggle = new CheckBox { IsChecked = app.Enabled, VerticalAlignment = VerticalAlignment.Top, MinWidth = 0 };
        toggle.Checked += (_, _) => { app.Enabled = true; WorkspacesService.Save(w); RefreshMetaOnly(w); };
        toggle.Unchecked += (_, _) => { app.Enabled = false; WorkspacesService.Save(w); RefreshMetaOnly(w); };
        Grid.SetColumn(toggle, 0);
        root.Children.Add(toggle);

        // info column
        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(app.DisplayName) ? app.ProcessName : app.DisplayName,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        info.Children.Add(new TextBlock
        {
            Text = app.Title,
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        info.Children.Add(new TextBlock
        {
            Text = app.ExePath,
            FontSize = 11,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(app.Args))
        {
            info.Children.Add(new TextBlock
            {
                Text = P("args: ", "引數：") + app.Args,
                FontSize = 11,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        // editable bounds row: X Y W H + state + monitor
        var boundsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        boundsRow.Children.Add(NumBox("X", app.X, v => { app.X = v; WorkspacesService.Save(w); }));
        boundsRow.Children.Add(NumBox("Y", app.Y, v => { app.Y = v; WorkspacesService.Save(w); }));
        boundsRow.Children.Add(NumBox("W", app.W, v => { app.W = Math.Max(1, v); WorkspacesService.Save(w); }));
        boundsRow.Children.Add(NumBox("H", app.H, v => { app.H = Math.Max(1, v); WorkspacesService.Save(w); }));

        var stateCombo = new ComboBox { MinWidth = 110, VerticalAlignment = VerticalAlignment.Bottom };
        stateCombo.Items.Add(P("Normal", "正常"));
        stateCombo.Items.Add(P("Maximized", "最大化"));
        stateCombo.Items.Add(P("Minimized", "最小化"));
        stateCombo.SelectedIndex = app.State switch { "maximized" => 1, "minimized" => 2, _ => 0 };
        stateCombo.SelectionChanged += (_, _) =>
        {
            app.State = stateCombo.SelectedIndex switch { 1 => "maximized", 2 => "minimized", _ => "normal" };
            WorkspacesService.Save(w);
        };
        boundsRow.Children.Add(stateCombo);

        boundsRow.Children.Add(new TextBlock
        {
            Text = P($"mon {app.Monitor}", $"螢幕 {app.Monitor}"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(2, 0, 0, 6),
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });

        info.Children.Add(boundsRow);
        Grid.SetColumn(info, 1);
        root.Children.Add(info);

        // remove button
        var remove = new Button
        {
            Content = new FontIcon { Glyph = ((char)0xE74D).ToString(), FontSize = 14 },
            VerticalAlignment = VerticalAlignment.Top,
        };
        ToolTipService.SetToolTip(remove, P("Remove this app", "移除呢個 app"));
        remove.Click += (_, _) =>
        {
            w.Apps.Remove(app);
            WorkspacesService.Save(w);
            BuildAppsPanel(w);
            RefreshMetaOnly(w);
        };
        Grid.SetColumn(remove, 2);
        root.Children.Add(remove);

        card.Child = root;
        return card;
    }

    private StackPanel NumBox(string label, int value, Action<int> onChanged)
    {
        var sp = new StackPanel { Spacing = 1 };
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
        var tb = new TextBox { Text = value.ToString(CultureInfo.InvariantCulture), Width = 64 };
        tb.LostFocus += (_, _) =>
        {
            if (int.TryParse(tb.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                onChanged(v);
            else
                tb.Text = value.ToString(CultureInfo.InvariantCulture);
        };
        sp.Children.Add(tb);
        return sp;
    }

    private void RefreshMetaOnly(Workspace w)
    {
        var fresh = WorkspacesService.Get(w.Id);
        if (fresh is null) return;
        int enabled = fresh.Apps.Count(a => a.Enabled);
        DetailMeta.Text = P($"{fresh.Apps.Count} apps · {enabled} enabled", $"{fresh.Apps.Count} 個 app · 啟用 {enabled} 個");
    }

    // -------------------------------------------------------------- actions

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptText(P("Name this workspace", "為工作區命名"),
            P("A snapshot of every visible app window will be saved.", "會儲存每個可見應用程式視窗嘅快照。"),
            P("My workspace", "我的工作區"));
        if (name is null) return; // cancelled

        try
        {
            var ws = WorkspacesService.CaptureNew(name);
            Info(InfoBarSeverity.Success, P("Captured", "已擷取"),
                P($"\"{ws.Name}\" with {ws.Apps.Count} apps.", $"已擷取「{ws.Name}」，共 {ws.Apps.Count} 個 app。"));
            CurrentId = ws.Id;
        }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Capture failed", "擷取失敗"), ex.Message);
        }
    }

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentId is not string id) return;
        var w = WorkspacesService.Get(id);
        if (w is null) return;
        if (w.Apps.Count(a => a.Enabled) == 0)
        {
            Info(InfoBarSeverity.Warning, P("Nothing to launch", "冇嘢可啟動"),
                P("All apps in this workspace are disabled.", "呢個工作區所有 app 都停用咗。"));
            return;
        }

        _launchCts?.Cancel();
        _launchCts = new CancellationTokenSource();
        SetLaunching(true);
        Info(InfoBarSeverity.Informational, P("Launching…", "啟動中…"),
            P("Starting apps and restoring window positions.", "正在啟動 app 並還原視窗位置。"));

        try
        {
            var (launched, positioned, errors) = await WorkspacesService.LaunchAsync(id, _launchCts.Token);
            if (errors.Count == 0)
                Info(InfoBarSeverity.Success, P("Launched", "已啟動"),
                    P($"Started {launched} apps; positioned {positioned}.", $"啟動咗 {launched} 個 app；定位咗 {positioned} 個。"));
            else
                Info(InfoBarSeverity.Warning, P("Launched with issues", "已啟動（有問題）"),
                    P($"Started {launched}; positioned {positioned}. ", $"啟動咗 {launched}；定位咗 {positioned}。") +
                    string.Join("  ·  ", errors.Take(4)));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Launch failed", "啟動失敗"), ex.Message);
        }
        finally
        {
            SetLaunching(false);
        }
    }

    private void SetLaunching(bool on)
    {
        LaunchBar.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        LaunchBtn.IsEnabled = !on;
        RecaptureBtn.IsEnabled = !on;
    }

    private void Recapture_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentId is not string id) return;
        try
        {
            WorkspacesService.Recapture(id);
            var w = WorkspacesService.Get(id);
            Info(InfoBarSeverity.Success, P("Re-captured", "已重新擷取"),
                P($"Now has {w?.Apps.Count ?? 0} apps.", $"而家有 {w?.Apps.Count ?? 0} 個 app。"));
            ShowDetail(id);
        }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Re-capture failed", "重新擷取失敗"), ex.Message);
        }
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentId is not string id) return;
        var w = WorkspacesService.Get(id);
        if (w is null) return;
        var name = await PromptText(P("Rename workspace", "重新命名工作區"), "", w.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        WorkspacesService.Rename(id, name);
        ShowDetail(id);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentId is not string id) return;
        var w = WorkspacesService.Get(id);
        if (w is null) return;

        var dlg = new ContentDialog
        {
            Title = P("Delete workspace?", "刪除工作區？"),
            Content = P($"\"{w.Name}\" will be permanently removed. This does not close any running apps.",
                $"「{w.Name}」會被永久移除。唔會關閉任何執行中嘅 app。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            WorkspacesService.Delete(id);
            CurrentId = null;
            DetailRoot.Visibility = Visibility.Collapsed;
            Info(InfoBarSeverity.Success, P("Deleted", "已刪除"), w.Name);
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentId is not string id) return;
        var w = WorkspacesService.Get(id);
        if (w is null) return;
        var path = await FileDialogs.SaveFileAsync(SafeFile(w.Name) + ".json", ".json");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            WorkspacesService.ExportTo(id, path);
            Info(InfoBarSeverity.Success, P("Exported", "已匯出"), path);
        }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Export failed", "匯出失敗"), ex.Message);
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".json");
        if (string.IsNullOrEmpty(path)) return;
        var ws = WorkspacesService.ImportFrom(path);
        if (ws is null)
        {
            Info(InfoBarSeverity.Error, P("Import failed", "匯入失敗"),
                P("That file is not a valid workspace.", "嗰個檔案唔係有效嘅工作區。"));
            return;
        }
        CurrentId = ws.Id;
        Info(InfoBarSeverity.Success, P("Imported", "已匯入"),
            P($"\"{ws.Name}\" with {ws.Apps.Count} apps.", $"已匯入「{ws.Name}」，共 {ws.Apps.Count} 個 app。"));
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    // -------------------------------------------------------------- helpers

    private static string SafeFile(string name)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "workspace" : name;
    }

    /// <summary>彈出一個輸入文字嘅對話框 · A simple single-line text-input dialog. Returns null if cancelled.</summary>
    private async System.Threading.Tasks.Task<string?> PromptText(string title, string subtitle, string initial)
    {
        var stack = new StackPanel { Spacing = 8 };
        if (!string.IsNullOrEmpty(subtitle))
            stack.Children.Add(new TextBlock { Text = subtitle, TextWrapping = TextWrapping.Wrap });
        var box = new TextBox { Text = initial, PlaceholderText = P("Workspace name", "工作區名稱"), SelectionStart = initial.Length };
        stack.Children.Add(box);

        var dlg = new ContentDialog
        {
            Title = title,
            Content = stack,
            PrimaryButtonText = P("OK", "確定"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };
        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        return box.Text?.Trim() ?? "";
    }

    private void Info(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
