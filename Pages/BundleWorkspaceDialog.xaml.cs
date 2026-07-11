using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 套件清單工作區 · Editable bundle "cart" dialog. Holds an in-memory
/// <see cref="SerializableBundle"/> and lets the user build it up, open/save in any of
/// four formats (JSON / YAML / XML / .ubundle), export a runnable .ps1, and install
/// every compatible package sequentially. Tracks an unsaved-changes flag and confirms
/// on New/Open while dirty. Matches the app's in-code dialog style. Bilingual throughout.
/// </summary>
public sealed partial class BundleWorkspaceDialog : ContentDialog
{
    private SerializableBundle _bundle = new();
    private readonly List<PackageItem> _seed;
    private bool _dirty;
    private string? _currentPath;

    private BundleWorkspaceDialog(List<PackageItem> seed)
    {
        InitializeComponent();
        _seed = seed ?? new List<PackageItem>();
        Render();
        RebuildRows();
    }

    /// <summary>
    /// 開啟工作區 · Show the workspace, seeded with the given installed/selected packages.
    /// </summary>
    public static async Task ShowAsync(XamlRoot root, List<PackageItem> seedFromInstalledOrSelected)
    {
        try
        {
            var dlg = new BundleWorkspaceDialog(seedFromInstalledOrSelected) { XamlRoot = root };
            await dlg.ShowAsync();
        }
        catch { /* never throw out of UI */ }
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Title = P("Bundle workspace · 套件清單工作區", "套件清單工作區 · Bundle workspace");
        CloseButtonText = P("Close", "關閉");
        DefaultButton = ContentDialogButton.Close;

        NewBtn.Content = P("New", "新建");
        OpenBtn.Content = P("Open…", "開啟…");
        SaveBtn.Content = P("Save as…", "另存…");
        AddBtn.Content = _seed.Count > 0
            ? P($"Add selected ({_seed.Count})", $"加入所選（{_seed.Count}）")
            : P("Add selected", "加入所選");
        ScriptBtn.Content = P("Export .ps1…", "匯出 .ps1…");
        InstallAllBtn.Content = P("Install all from bundle", "由清單全部安裝");

        ToolTipService.SetToolTip(OpenBtn, P("Open a bundle (.json / .yaml / .yml / .xml / .ubundle)", "開啟清單（.json／.yaml／.yml／.xml／.ubundle）"));
        ToolTipService.SetToolTip(SaveBtn, P("Save — the format follows the file extension you choose", "儲存 — 格式跟你揀嘅副檔名"));
        ToolTipService.SetToolTip(ScriptBtn, P("Export a standalone PowerShell install script", "匯出獨立 PowerShell 安裝指令稿"));
    }

    // ===== status & dirty =====

    private void SetDirty(bool dirty)
    {
        _dirty = dirty;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        int comp = _bundle.packages.Count;
        int inc = _bundle.incompatible_packages.Count;
        var name = _currentPath is null ? P("(unsaved)", "（未儲存）") : System.IO.Path.GetFileName(_currentPath);
        var dirtyTag = _dirty ? P("  •  unsaved changes", "  •  有未儲存變更") : "";
        StatusText.Text = inc > 0
            ? P($"{name} — {comp} package(s), {inc} incompatible{dirtyTag}", $"{name} — {comp} 個套件、{inc} 個不相容{dirtyTag}")
            : P($"{name} — {comp} package(s){dirtyTag}", $"{name} — {comp} 個套件{dirtyTag}");
    }

    // ===== rows =====

    private void RebuildRows()
    {
        RowsPanel.Children.Clear();

        if (_bundle.packages.Count == 0 && _bundle.incompatible_packages.Count == 0)
        {
            RowsPanel.Children.Add(new TextBlock
            {
                Text = P("Bundle is empty. Use “Add selected”, or “Open…” a bundle file.",
                         "清單係空嘅。撳「加入所選」，或者「開啟…」一個清單檔。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(2, 6, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
            UpdateStatus();
            return;
        }

        foreach (var p in _bundle.packages.ToList())
            RowsPanel.Children.Add(PackageRow(p));

        if (_bundle.incompatible_packages.Count > 0)
        {
            RowsPanel.Children.Add(new TextBlock
            {
                Text = P("Incompatible (logged only, not installable):", "不相容（僅記錄，不能安裝）："),
                FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(2, 8, 0, 0),
            });
            foreach (var p in _bundle.incompatible_packages.ToList())
                RowsPanel.Children.Add(IncompatibleRow(p));
        }

        UpdateStatus();
    }

    private Border PackageRow(SerializablePackage p)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = Badge(p.ManagerName);
        Grid.SetColumn(badge, 0);
        grid.Children.Add(badge);

        var texts = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var ver = string.IsNullOrEmpty(p.Version) ? "" : $"  ({p.Version})";
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        head.Children.Add(new TextBlock { Text = $"{(string.IsNullOrEmpty(p.Name) ? p.Id : p.Name)}{ver}", FontWeight = FontWeights.SemiBold, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis });
        if (BundleService.HasOptions(p))
            head.Children.Add(OptionsIndicator());
        texts.Children.Add(head);
        var sub = string.IsNullOrEmpty(p.Source) ? p.Id : $"{p.Id}  ·  {p.Source}";
        texts.Children.Add(new TextBlock { Text = sub, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(texts, 1);
        grid.Children.Add(texts);

        var remove = new Button { Content = P("Remove", "移除"), Padding = new Thickness(10, 4, 10, 4) };
        remove.Click += (_, _) => { _bundle.packages.Remove(p); SetDirty(true); RebuildRows(); };
        Grid.SetColumn(remove, 2);
        grid.Children.Add(remove);

        return Card(grid);
    }

    private Border IncompatibleRow(SerializableIncompatiblePackage p)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var ver = string.IsNullOrEmpty(p.Version) ? "" : $"  ({p.Version})";
        texts.Children.Add(new TextBlock { Text = $"{(string.IsNullOrEmpty(p.Name) ? p.Id : p.Name)}{ver}", FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        var sub = string.IsNullOrEmpty(p.Source) ? p.Id : $"{p.Id}  ·  {p.Source}";
        texts.Children.Add(new TextBlock { Text = sub, FontSize = 11, FontFamily = new FontFamily("Consolas"), Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"], TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(texts, 0);
        grid.Children.Add(texts);

        var remove = new Button { Content = P("Remove", "移除"), Padding = new Thickness(10, 4, 10, 4) };
        remove.Click += (_, _) => { _bundle.incompatible_packages.Remove(p); SetDirty(true); RebuildRows(); };
        Grid.SetColumn(remove, 1);
        grid.Children.Add(remove);

        return Card(grid);
    }

    private static Border OptionsIndicator() => new()
    {
        Background = (Brush)Application.Current.Resources["SystemFillColorCautionBackgroundBrush"],
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(5, 1, 5, 1),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = Loc.I.Pick("options", "選項"), FontSize = 10, FontWeight = FontWeights.SemiBold },
    };

    private static Border Badge(string key) => new()
    {
        Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(6, 2, 6, 2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock { Text = string.IsNullOrEmpty(key) ? "?" : key, FontSize = 10, FontWeight = FontWeights.SemiBold },
    };

    private static Border Card(UIElement child) => new()
    {
        Padding = new Thickness(12, 8, 12, 8),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(8),
        Child = child,
    };

    // ===== confirm-on-dirty =====

    private async Task<bool> ConfirmLoseChangesAsync()
    {
        if (!_dirty) return true;
        var confirm = new ContentDialog
        {
            Title = P("Discard unsaved changes?", "捨棄未儲存變更？"),
            Content = P("The current bundle has unsaved changes. Continue and lose them?",
                        "目前清單有未儲存變更。繼續就會失去呢啲變更，繼續？"),
            PrimaryButtonText = P("Discard", "捨棄"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        return await confirm.ShowAsync() == ContentDialogResult.Primary;
    }

    // ===== toolbar handlers =====

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmLoseChangesAsync()) return;
        _bundle = new SerializableBundle();
        _currentPath = null;
        WarnBar.IsOpen = false;
        SetDirty(false);
        RebuildRows();
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmLoseChangesAsync()) return;
        string? path;
        try { path = await FileDialogs.OpenFileAsync(".json", ".yaml", ".yml", ".xml", ".ubundle"); }
        catch { path = null; }
        if (path is null) return;

        Busy.IsActive = true;
        BundleLoadResult res;
        try { res = await BundleService.LoadAsync(path); }
        catch { res = new BundleLoadResult(); }
        Busy.IsActive = false;

        _bundle = res.Bundle;
        _currentPath = path;
        SetDirty(false);
        RebuildRows();

        // 版本不符 + 安全警告 · version mismatch + security warnings.
        var msgs = new List<string>();
        if (res.VersionMismatch)
            msgs.Add(P($"Bundle export_version is {res.FoundVersion}, expected 3 — it may not import perfectly.",
                       $"清單 export_version 係 {res.FoundVersion}，預期係 3 — 匯入可能唔完全正確。"));
        var report = BundleService.Inspect(_bundle);
        if (report.HasWarnings)
        {
            msgs.Add(P($"Security: {report.Warnings.Count} package(s) carry custom commands/args/kill-lists:",
                       $"安全：{report.Warnings.Count} 個套件帶有自訂指令／參數／kill-list："));
            foreach (var w in report.Warnings.Take(8))
                msgs.Add("  • " + w.Display);
            if (report.Warnings.Count > 8)
                msgs.Add(P($"  …and {report.Warnings.Count - 8} more.", $"  …仲有 {report.Warnings.Count - 8} 個。"));
        }
        if (msgs.Count > 0)
        {
            WarnBar.Title = P("Review before installing", "安裝前請檢查");
            WarnBar.Message = string.Join("\n", msgs);
            WarnBar.Severity = report.HasWarnings ? InfoBarSeverity.Warning : InfoBarSeverity.Informational;
            WarnBar.IsOpen = true;
        }
        else WarnBar.IsOpen = false;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        string? path;
        try
        {
            path = await FileDialogs.SaveFileAsync("winforge-bundle",
                new[]
                {
                    new FileDialogs.Filter("JSON / UniGetUI bundle (*.json;*.ubundle)", "*.json;*.ubundle"),
                    new FileDialogs.Filter("YAML (*.yaml;*.yml)", "*.yaml;*.yml"),
                    new FileDialogs.Filter("XML (*.xml)", "*.xml"),
                },
                "json",
                P("Save bundle as…", "另存清單…"));
        }
        catch { path = null; }
        if (path is null) return;

        Busy.IsActive = true;
        try { await BundleService.SaveAsync(_bundle, path); }
        catch { }
        Busy.IsActive = false;

        _currentPath = path;
        SetDirty(false);
        StatusText.Text = P($"Saved to {System.IO.Path.GetFileName(path)}.", $"已儲存到 {System.IO.Path.GetFileName(path)}。");
    }

    private void AddSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_seed.Count == 0)
        {
            StatusText.Text = P("Nothing selected to add.", "冇所選項目可以加入。");
            return;
        }
        // 只附加真正儲存咗嘅逐套件覆寫；冇覆寫時唔好將全域預設冒充為套件設定。
        // Attach only a real saved per-package override; do not serialize global defaults as package options.
        var add = BundleService.ToBundle(_seed, SavedPackageOptions);
        // 去重（按 manager|id|source）· de-dupe by manager|id|source. A same-ID package
        // from a second registered source is a distinct, intentional bundle entry.
        var existing = new HashSet<string>(_bundle.packages.Select(BundleKey), StringComparer.OrdinalIgnoreCase);
        var existingInc = new HashSet<string>(_bundle.incompatible_packages.Select(p => $"{p.Source}|{p.Id}"), StringComparer.OrdinalIgnoreCase);
        int added = 0;
        foreach (var p in add.packages)
            if (existing.Add(BundleKey(p))) { _bundle.packages.Add(p); added++; }
        foreach (var p in add.incompatible_packages)
            if (existingInc.Add($"{p.Source}|{p.Id}")) { _bundle.incompatible_packages.Add(p); added++; }
        if (added > 0) SetDirty(true);
        RebuildRows();
        StatusText.Text = P($"Added {added} new package(s).", $"加入咗 {added} 個新套件。");
    }

    private static string BundleKey(SerializablePackage package)
        => PackageSourcePolicy.IdentityKey(package?.ManagerName, package?.Id, package?.Source,
            PackageOperations.Op.Install);

    /// <summary>讀已儲存嘅逐套件覆寫，畀清單匯出用 · Load a saved per-package override for bundle export.</summary>
    private static InstallOptions? SavedPackageOptions(PackageItem item)
    {
        try
        {
            if (!InstallOptions.HasOverride(item.ManagerKey, item.Id)) return null;
            return InstallOptions.Load(item.ManagerKey, item.Id).Clone();
        }
        catch { return null; }
    }

    private async void ExportScript_Click(object sender, RoutedEventArgs e)
    {
        if (_bundle.packages.Count == 0)
        {
            StatusText.Text = P("No compatible packages to script.", "冇相容套件可以寫入指令稿。");
            return;
        }
        string? path;
        try { path = await FileDialogs.SaveFileAsync("winforge-install", ".ps1"); }
        catch { path = null; }
        if (path is null) return;

        Busy.IsActive = true;
        try
        {
            var script = BundleService.GenerateInstallScript(_bundle);
            await System.IO.File.WriteAllTextAsync(path, script, new System.Text.UTF8Encoding(false));
            StatusText.Text = P($"Install script saved to {System.IO.Path.GetFileName(path)}.", $"安裝指令稿已儲存到 {System.IO.Path.GetFileName(path)}。");
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
        finally { Busy.IsActive = false; }
    }

    private async void InstallAll_Click(object sender, RoutedEventArgs e)
    {
        if (_bundle.packages.Count == 0)
        {
            StatusText.Text = P("No compatible packages to install.", "冇相容套件可以安裝。");
            return;
        }

        // 安全檢查確認 · security confirmation before running anything.
        var report = BundleService.Inspect(_bundle);
        if (report.HasWarnings)
        {
            var body = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
            body.Text = P("This bundle runs custom commands / arguments / kill-lists. Install anyway?",
                          "呢個清單會執行自訂指令／參數／kill-list。仍然要安裝？")
                        + "\n\n" + string.Join("\n", report.Warnings.Take(10).Select(w => "• " + w.Display));
            var confirm = new ContentDialog
            {
                Title = P("Security review", "安全檢查"),
                Content = new ScrollViewer { MaxHeight = 320, Content = body },
                PrimaryButtonText = P("Install anyway", "仍然安裝"),
                CloseButtonText = P("Cancel", "取消"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
        }

        InstallAllBtn.IsEnabled = false;
        Busy.IsActive = true;
        int done = 0, fail = 0, skipped = _bundle.incompatible_packages.Count, processed = 0;
        var pkgs = _bundle.packages.ToList();
        foreach (var p in pkgs)
        {
            var displayName = string.IsNullOrEmpty(p.Name) ? p.Id : p.Name;
            StatusText.Text = P($"Installing {displayName}… ({processed + 1}/{pkgs.Count})",
                                $"安裝緊 {displayName}…（{processed + 1}/{pkgs.Count}）");

            // 清單設定係今次匯入嘅權威來源；明確傳入選項，避免 coordinator 讀本機舊覆寫。
            // Bundle options are authoritative for this import. Pass them explicitly so the coordinator
            // never substitutes an unrelated local saved override. A package Version is the fallback pin.
            var options = (p.InstallationOptions ?? new InstallOptions()).Clone();
            if (string.IsNullOrWhiteSpace(options.Version) && !string.IsNullOrWhiteSpace(p.Version))
                options.Version = p.Version.Trim();

            var item = new PackageItem
            {
                ManagerKey = p.ManagerName ?? "",
                Id = p.Id ?? "",
                Name = displayName ?? "",
                Version = p.Version ?? "",
                Source = p.Source ?? "",
            };

            try
            {
                var result = await PackageOperationCoordinator.RunAsync(
                    item, PackageOperations.Op.Install, options, CancellationToken.None);
                switch (result.Status)
                {
                    case PackageOperationStatus.Succeeded: done++; break;
                    case PackageOperationStatus.Skipped:
                    case PackageOperationStatus.Cancelled: skipped++; break;
                    default: fail++; break;
                }
            }
            catch { fail++; }
            processed++;
        }
        Busy.IsActive = false;
        InstallAllBtn.IsEnabled = true;
        StatusText.Text = skipped > 0
            ? P($"Done — {done} ok, {fail} failed, {skipped} skipped or incompatible.", $"完成 — {done} 成功、{fail} 失敗、{skipped} 個已略過或不相容。")
            : P($"Done — {done} ok, {fail} failed.", $"完成 — {done} 成功、{fail} 失敗。");
    }
}
