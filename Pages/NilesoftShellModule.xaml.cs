using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Controls;
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
        HeaderTitle.Text = "Nilesoft Shell · Nilesoft 右鍵選單";
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
        if (ok) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; SetLifecycleEnabled(true); return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Nilesoft Shell not found", "搵唔到 Nilesoft Shell");
        EngineBar.Message = P("Click to install Nilesoft Shell automatically (winget) — no app restart needed.",
            "撳一下自動安裝 Nilesoft Shell（winget）— 唔使重開程式。");
        EngineBar.ActionButton = EngineBars.AutoInstallButton(
            NilesoftShellService.WingetId, "Install Nilesoft Shell automatically", "自動安裝 Nilesoft Shell",
            async () => { await CheckEngine(); LoadConfigIntoEditor(); RefreshStatus(); });
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
            ConfigBox.Text = File.ReadAllText(path);
            _loadedPath = path;
            ConfigPathLine.Text = P($"Editing: {path}", $"正在編輯：{path}");
            Show(InfoBarSeverity.Informational, P("Opened (not the live config)", "已開啟（並非生效中設定）"),
                P("Save writes back to this file. To affect menus, edit the live shell.nss.", "儲存會寫回呢個檔案。要影響選單，請編輯生效中嘅 shell.nss。"));
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Open failed", "開啟失敗"), ex.Message); }
    }

    private void ReloadFile_Click(object sender, RoutedEventArgs e) => LoadConfigIntoEditor();

    private void Save_Click(object sender, RoutedEventArgs e) => DoSave(reload: false);

    private async void SaveReload_Click(object sender, RoutedEventArgs e)
    {
        if (!DoSave(reload: false)) return;
        var r = await NilesoftShellService.ReloadAsync();
        ShowResult(r, P("Reloaded", "已重新載入"));
        RefreshStatus();
    }

    private bool DoSave(bool reload)
    {
        // If the user opened a different file, save there as plain text; otherwise use the service
        // (which backs up the live shell.nss and handles elevation messaging).
        var live = NilesoftShellService.FindConfigPath();
        if (_loadedPath is not null && live is not null &&
            !string.Equals(Path.GetFullPath(_loadedPath), Path.GetFullPath(live), StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.WriteAllText(_loadedPath, ConfigBox.Text);
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
        foreach (var op in shown)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }

    // ===================== helpers =====================

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
