using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Nilesoft Shell 模組 · Installs Nilesoft Shell (winget Nilesoft.Shell), registers/unregisters/reloads
/// the native context-menu extension, and edits its shell.nss config with a templated editor, curated
/// snippet gallery, automatic timestamped backups, and restore-default. Bilingual; FileDialogs only.
/// </summary>
public sealed partial class NilesoftShellModule : Page
{
    public sealed class SnippetRow
    {
        public string Title { get; init; } = "";
        public string Desc { get; init; } = "";
        public string Code { get; init; } = "";
        public string InsertLabel { get; init; } = "";
    }

    private List<TweakDefinition>? _ops;
    private bool _rowBusy; // guard so only one action row runs at a time

    public NilesoftShellModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += async (_, _) =>
        {
            Render();
            BuildSnippets();
            PopulateOps(string.Empty);
            await CheckEngine();
            LoadConfigIntoEditor();
            RefreshStatus();
        };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildSnippets();
        PopulateOps(OpsFilter.Text ?? string.Empty);
        RefreshStatus();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Nilesoft Shell · Nilesoft 右鍵選單";
        HeaderBlurb.Text = P(
            "Install and control Nilesoft Shell — a native replacement for the Windows right-click menu — and edit its shell.nss configuration with templates, snippets and automatic backups.",
            "安裝同控制 Nilesoft Shell（Windows 右鍵選單嘅原生替代品），並用範本、片語同自動備份去編輯佢嘅 shell.nss 設定檔。");

        LifecycleHeader.Text = P("Install & lifecycle", "安裝與生命週期");
        RegisterBtn.Content = P("Register", "註冊");
        UnregisterBtn.Content = P("Unregister", "取消註冊");
        ReloadBtn.Content = P("Reload config", "重新載入設定");
        RestartExplorerBtn.Content = P("Restart Explorer", "重新啟動 Explorer");
        RecheckBtn.Content = P("Re-check", "重新檢查");

        EditorHeader.Text = P("Configuration editor (shell.nss)", "設定編輯器（shell.nss）");
        OpenFileBtn.Content = P("Open a .nss…", "開啟其他 .nss…");
        ReloadFileBtn.Content = P("Revert", "回復");
        SaveBtn.Content = P("Save", "儲存");
        SaveReloadBtn.Content = P("Save & reload", "儲存並重新載入");
        BackupBtn.Content = P("Backup", "備份");
        RestoreBackupBtn.Content = P("Restore backup…", "還原備份…");
        RestoreDefaultBtn.Content = P("Restore default", "還原預設");

        SnippetsHeader.Text = P("Snippet & template gallery", "片語與範本庫");
        SnippetsHint.Text = P(
            "Click Insert to drop a snippet at the cursor in the editor above, then Save & reload to apply.",
            "撳「插入」會喺上面編輯器嘅游標位置加入片語，再撳「儲存並重新載入」即可套用。");

        OpsHeader.Text = P("Operations", "操作");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");

        if (!AdminHelper.IsElevated)
        {
            ElevationBar.IsOpen = true;
            ElevationBar.Severity = InfoBarSeverity.Informational;
            ElevationBar.Title = P("Some actions need administrator rights", "部分操作需要管理員權限");
            ElevationBar.Message = P(
                "Register / Unregister / Reload and saving shell.nss live under Program Files — Windows will prompt for elevation (UAC) when needed.",
                "註冊／取消註冊／重新載入，以及儲存位於 Program Files 嘅 shell.nss，會喺需要時彈出 UAC 提權提示。");
        }
        else ElevationBar.IsOpen = false;
    }

    private async Task CheckEngine()
    {
        bool ok = await NilesoftShellService.IsInstalledAsync();
        if (ok) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; EngineBar.Content = null; SetLifecycleEnabled(true); return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Nilesoft Shell not found", "搵唔到 Nilesoft Shell");
        EngineBar.Message = P("Click to install Nilesoft Shell automatically (winget) — no app restart needed.",
            "撳一下自動安裝 Nilesoft Shell（winget）— 唔使重開程式。");
        // Rich install control: real progress bar + live streamed status + % + Cancel + success/error animation.
        EngineBar.ActionButton = null;
        var install = EngineBars.AutoInstallProgress(
            NilesoftShellService.WingetId, "Install Nilesoft Shell automatically", "自動安裝 Nilesoft Shell",
            async () => { await CheckEngine(); LoadConfigIntoEditor(); RefreshStatus(); });
        install.Margin = new Thickness(0, 4, 0, 8);
        EngineBar.Content = install;
        SetLifecycleEnabled(false);
    }

    private void SetLifecycleEnabled(bool installed)
    {
        RegisterBtn.IsEnabled = installed;
        UnregisterBtn.IsEnabled = installed;
        ReloadBtn.IsEnabled = installed;
        SaveBtn.IsEnabled = installed;
        SaveReloadBtn.IsEnabled = installed;
        BackupBtn.IsEnabled = installed;
        RestoreBackupBtn.IsEnabled = installed;
        RestoreDefaultBtn.IsEnabled = installed;
    }

    private void RefreshStatus()
    {
        var dir = NilesoftShellService.FindInstallDir();
        var exe = NilesoftShellService.FindShellExe();
        if (exe is null)
        {
            StatusLine.Text = P("Status: not installed.", "狀態：未安裝。");
            return;
        }
        bool reg = NilesoftShellService.IsRegistered();
        StatusLine.Text = P(
            $"Installed at {dir}. Registered with Explorer: {(reg ? "yes" : "no / unknown")}.",
            $"已安裝於 {dir}。已註冊到 Explorer：{(reg ? "係" : "否／未知")}。");
    }

    // ===================== config editor =====================

    private string? _loadedPath;

    private void LoadConfigIntoEditor()
    {
        _loadedPath = NilesoftShellService.FindConfigPath();
        ConfigPathLine.Text = _loadedPath is null
            ? P("shell.nss path: (install location not found)", "shell.nss 路徑：（搵唔到安裝位置）")
            : P($"Editing: {_loadedPath}", $"正在編輯：{_loadedPath}");
        var text = NilesoftShellService.ReadConfig();
        if (string.IsNullOrEmpty(text) && _loadedPath is not null && !File.Exists(_loadedPath))
            text = NilesoftShellService.DefaultConfig;
        ConfigBox.Text = text;
    }

    private async void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(
            new[] { new FileDialogs.Filter("Nilesoft Shell config (*.nss)", "*.nss"), new FileDialogs.Filter("All files", "*.*") },
            P("Open a .nss file", "開啟 .nss 檔案"));
        if (path is null) return;
        try
        {
            ConfigBox.Text = await File.ReadAllTextAsync(path);
            _loadedPath = path;
            ConfigPathLine.Text = P($"Editing: {path}", $"正在編輯：{path}");
            Show(InfoBarSeverity.Informational, P("Opened (not the live config)", "已開啟（並非生效中設定）"),
                P("Save writes back to this file. To affect menus, edit the live shell.nss.", "儲存會寫回呢個檔案。要影響選單，請編輯生效中嘅 shell.nss。"));
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Open failed", "開啟失敗"), ex.Message); }
    }

    private void ReloadFile_Click(object sender, RoutedEventArgs e) => LoadConfigIntoEditor();

    private async void Save_Click(object sender, RoutedEventArgs e) => await DoSave(reload: false);

    private async void SaveReload_Click(object sender, RoutedEventArgs e)
    {
        if (!await DoSave(reload: false)) return;
        var r = await NilesoftShellService.ReloadAsync();
        ShowResult(r, P("Reloaded", "已重新載入"));
        RefreshStatus();
    }

    private async Task<bool> DoSave(bool reload)
    {
        // If the user opened a different file, save there as plain text; otherwise use the service
        // (which backs up the live shell.nss and handles elevation messaging).
        var live = NilesoftShellService.FindConfigPath();
        if (_loadedPath is not null && live is not null &&
            !string.Equals(Path.GetFullPath(_loadedPath), Path.GetFullPath(live), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await File.WriteAllTextAsync(_loadedPath, ConfigBox.Text);
                Show(InfoBarSeverity.Success, P("Saved", "已儲存"), _loadedPath);
                return true;
            }
            catch (Exception ex) { Show(InfoBarSeverity.Error, P("Save failed", "儲存失敗"), ex.Message); return false; }
        }

        var res = NilesoftShellService.WriteConfig(ConfigBox.Text);
        ShowResult(res, P("Saved", "已儲存"));
        return res.Success;
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = NilesoftShellService.BackupConfig();
            if (path is null) Show(InfoBarSeverity.Warning, P("Nothing to back up", "冇嘢可備份"), P("shell.nss not found.", "搵唔到 shell.nss。"));
            else Show(InfoBarSeverity.Success, P("Backup created", "已建立備份"), path);
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Backup failed", "備份失敗"), ex.Message); }
    }

    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var backups = NilesoftShellService.ListBackups();
        string? pick;
        if (backups.Count > 0)
        {
            pick = await PickBackupDialog(backups);
        }
        else
        {
            pick = await FileDialogs.OpenFileAsync(
                new[] { new FileDialogs.Filter("Backup (*.bak;*.nss)", "*.bak;*.nss"), new FileDialogs.Filter("All files", "*.*") },
                P("Pick a backup to restore", "揀一個備份還原"));
        }
        if (pick is null) return;
        var r = NilesoftShellService.RestoreBackup(pick);
        ShowResult(r, P("Restored from backup", "已由備份還原"));
        if (r.Success) LoadConfigIntoEditor();
    }

    private async Task<string?> PickBackupDialog(IReadOnlyList<string> backups)
    {
        var combo = new ComboBox { MinWidth = 360 };
        foreach (var b in backups) combo.Items.Add(Path.GetFileName(b));
        combo.SelectedIndex = 0;
        var browse = new Button { Content = P("Browse for another file…", "瀏覽其他檔案…"), Margin = new Thickness(0, 8, 0, 0) };
        string? browsed = null;
        browse.Click += async (_, _) =>
        {
            browsed = await FileDialogs.OpenFileAsync(
                new[] { new FileDialogs.Filter("Backup (*.bak;*.nss)", "*.bak;*.nss"), new FileDialogs.Filter("All files", "*.*") },
                P("Pick a backup file", "揀備份檔"));
            if (browsed is not null) combo.Items.Add(browsed);
        };
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = P("Choose a backup to restore. The current shell.nss is backed up first.", "揀一個備份還原。現有 shell.nss 會先被備份。"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(combo);
        panel.Children.Add(browse);

        var dlg = new ContentDialog
        {
            Title = P("Restore backup", "還原備份"),
            Content = panel,
            PrimaryButtonText = P("Restore", "還原"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };
        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary) return null;
        if (browsed is not null && combo.SelectedItem as string == browsed) return browsed;
        int idx = combo.SelectedIndex;
        return idx >= 0 && idx < backups.Count ? backups[idx] : (combo.SelectedItem as string);
    }

    private async void RestoreDefault_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            Title = P("Restore default config?", "還原預設設定？"),
            Content = P("This overwrites shell.nss with WinForge's clean default template. A timestamped backup is taken first.",
                "呢個會用 WinForge 嘅乾淨預設範本覆蓋 shell.nss。會先做一個有時間戳記嘅備份。"),
            PrimaryButtonText = P("Restore default", "還原預設"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var r = NilesoftShellService.RestoreDefault();
        ShowResult(r, P("Default restored", "已還原預設"));
        if (r.Success) LoadConfigIntoEditor();
    }

    // ===================== snippets =====================

    private void BuildSnippets()
    {
        var rows = NilesoftShellService.Snippets.Select(s => new SnippetRow
        {
            Title = P(s.En, s.Zh),
            Desc = P(s.DescEn, s.DescZh),
            Code = s.Code,
            InsertLabel = P("Insert", "插入"),
        }).ToList();
        SnippetsList.ItemsSource = rows;
    }

    private void InsertSnippet_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string code) return;
        int pos = ConfigBox.SelectionStart;
        var text = ConfigBox.Text ?? "";
        if (pos < 0 || pos > text.Length) pos = text.Length;
        string block = (pos > 0 && text[pos - 1] != '\n' ? "\n" : "") + code + "\n";
        ConfigBox.Text = text.Insert(pos, block);
        ConfigBox.SelectionStart = pos + block.Length;
        ConfigBox.Focus(FocusState.Programmatic);
    }

    // ===================== lifecycle buttons =====================

    private async void Register_Click(object sender, RoutedEventArgs e)
        => await RunLifecycle(sender as Button, NilesoftShellService.RegisterAsync, P("Registered with Explorer", "已註冊到 Explorer"));

    private async void Unregister_Click(object sender, RoutedEventArgs e)
        => await RunLifecycle(sender as Button, NilesoftShellService.UnregisterAsync, P("Unregistered", "已取消註冊"));

    private async void Reload_Click(object sender, RoutedEventArgs e)
        => await RunLifecycle(sender as Button, NilesoftShellService.ReloadAsync, P("Configuration reloaded", "已重新載入設定"));

    private async void RestartExplorer_Click(object sender, RoutedEventArgs e)
        => await RunLifecycle(sender as Button, NilesoftShellService.RestartExplorerAsync, P("Explorer restarted", "已重新啟動 Explorer"));

    private async void Recheck_Click(object sender, RoutedEventArgs e)
    {
        await CheckEngine();
        LoadConfigIntoEditor();
        RefreshStatus();
        Show(InfoBarSeverity.Informational, P("Re-checked", "已重新檢查"), "");
    }

    private async Task RunLifecycle(Button? btn, Func<System.Threading.CancellationToken, Task<TweakResult>> run, string okTitle)
    {
        if (btn is not null) btn.IsEnabled = false;
        try
        {
            var r = await run(default);
            ShowResult(r, okTitle);
            RefreshStatus();
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
        finally { if (btn is not null) btn.IsEnabled = true; }
    }

    // ===================== operations cards =====================

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= NilesoftShellOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }

        bool first = true;
        foreach (var op in shown)
        {
            if (!first) OpsPanel.Children.Add(BuildDivider());
            first = false;
            OpsPanel.Children.Add(BuildRow(op));
        }
    }

    // ---- One clean row: bilingual title + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        text.Children.Add(new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Action → Button awaiting RunAsync ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");
        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        btn.Click += async (_, _) =>
        {
            if (_rowBusy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _rowBusy = true;
            btn.IsEnabled = false;
            var restore = btn.Content;
            btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
            try
            {
                var result = await op.RunAsync(CancellationToken.None);
                ShowResult(result);
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                btn.Content = restore;
                btn.IsEnabled = true;
                _rowBusy = false;
            }
        };
        return btn;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = op.GetCurrentChoice?.Invoke();
            if (cur is not null && op.Choices is not null)
                for (int i = 0; i < op.Choices.Count; i++)
                    if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { combo.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try
                    {
                        var cur = op.GetCurrentChoice?.Invoke();
                        if (cur is not null && op.Choices is not null)
                            for (int i = 0; i < op.Choices.Count; i++)
                                if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                                { combo.SelectedIndex = i; break; }
                    }
                    catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    // ---------------- Slider → Slider + value label ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        string Format(double v)
        {
            bool whole = op.Step >= 1 && Math.Abs(op.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                               : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return op.Unit is null ? num : $"{num} {op.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = op.Min, Maximum = op.Max, StepFrequency = op.Step, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        var valueText = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        bool suppress = true;
        try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { slider.Value = op.Min; }
        suppress = false;
        valueText.Text = Format(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = Format(e.NewValue);
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        panel.Children.Add(slider);
        panel.Children.Add(valueText);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step,
            LargeChange = op.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        bool suppress = true;
        try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, e) =>
        {
            if (suppress || op.SetNumber is null || double.IsNaN(e.NewValue)) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        return box;
    }

    // ---------------- Info → refreshable TextBlock ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ===================== helpers =====================

    /// <summary>套用後經持久 InfoBar 顯示 · Report an apply through the persistent InfoBar.</summary>
    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        Show(InfoBarSeverity.Success, P("Done", "完成"), P(en, zh));
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        Show(InfoBarSeverity.Error, P("Failed", "失敗"),
            needAdmin ? P("This change needs administrator rights.", "呢項更改需要管理員權限。") : ex.Message);
    }

    private void ShowResult(TweakResult r)
    {
        var msg = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        if (!string.IsNullOrWhiteSpace(r.Output)) msg = string.IsNullOrWhiteSpace(msg) ? r.Output! : $"{msg}\n{r.Output}";
        Show(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Done", "完成") : P("Failed", "失敗"), msg);
    }

    private void ShowResult(TweakResult r, string okTitle)
    {
        var msg = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        if (!string.IsNullOrWhiteSpace(r.Output)) msg = string.IsNullOrWhiteSpace(msg) ? r.Output! : $"{msg}\n{r.Output}";
        Show(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? okTitle : P("Failed", "失敗"), msg);
    }

    private void Show(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
