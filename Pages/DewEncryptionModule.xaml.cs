using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Dew Encryption control surface. The underlying service owns the safety-critical copy,
/// Git, archive, and watcher work; this page keeps user selection, confirmation, progress,
/// and bilingual status presentation on the UI thread.
/// </summary>
public sealed partial class DewEncryptionModule : Page
{
    private const string LastArchiveSetting = "dew.lastArchive";

    private readonly ObservableCollection<string> _selectedPaths = new();
    private readonly ObservableCollection<DewHistoryEntry> _history = new();
    private readonly ObservableCollection<DewFileChange> _changes = new();
    private DewProjectContext? _project;
    private DewProjectContext? _openedProject;
    private DewHistoryWatcher? _watcher;
    private CancellationTokenSource? _operationCts;
    private string _lastArchivePath = SettingsStore.Get(LastArchiveSetting, "");
    private bool _busy;
    private bool _cancellationRequested;
    private bool _initializing = true;
    private bool _languageSubscribed;
    private int _detailVersion;

    public DewEncryptionModule()
    {
        InitializeComponent();
        EncryptArchiveToggle.IsOn = true;
        _initializing = false;
        SelectedPathsList.ItemsSource = _selectedPaths;
        HistoryList.ItemsSource = _history;
        ChangedFilesList.ItemsSource = _changes;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Render();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private static void SetButtonLabel(Button button, string label, string? toolTip = null)
    {
        if (button.Content is TextBlock text)
            text.Text = label;
        else
            button.Content = new TextBlock
            {
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
            };
        AutomationProperties.SetName(button, label);
        ToolTipService.SetToolTip(button, toolTip ?? label);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_languageSubscribed)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            _languageSubscribed = true;
        }
        Render();
        _ = RefreshToolAvailabilityAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_languageSubscribed)
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _languageSubscribed = false;
        }
        _operationCts?.Cancel();
        DisposeWatcher();
        ArchivePasswordBox.Password = "";
        ArchivePasswordConfirmBox.Password = "";
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(Render);
    }

    private void Render()
    {
        Header.Title = "Dew Encryption · Dew 加密歷史";
        AutomationProperties.SetName(Header, Header.Title);
        HeaderBlurb.Text = P(
            "Create local, Git-backed file history and optional encrypted 7z exports without sending a byte to the cloud. Restores are staged and rollback-safe.",
            "建立本機、Git 支援嘅檔案歷史，同埋可選加密 7z 匯出，完全唔會上傳任何資料去雲端。還原會先暫存，失敗時可以安全回復。" );

        SelectionTitle.Text = P("Selection and snapshots", "選取項目同快照");
        SelectionDescription.Text = P(
            "Choose files or folders. Dew stores its repository beside a selected file, or inside a selected/common folder under “Dew Encryption Archives”. Unsupported nested Git metadata, symlinks and junctions are rejected instead of being silently lost.",
            "揀檔案或者資料夾。Dew 會將儲存庫放喺所選檔案旁邊，或者所選／共用資料夾入面嘅「Dew Encryption Archives」。唔支援嘅巢狀 Git metadata、符號連結同 junction 會直接拒絕，唔會靜靜漏失。" );
        SetButtonLabel(AddFilesBtn, P("Add files", "加入檔案"));
        SetButtonLabel(AddFolderBtn, P("Add folder", "加入資料夾"));
        SetButtonLabel(OpenExistingHistoryBtn, P("Open existing history", "開啟現有歷史"));
        SetButtonLabel(RemoveSelectionBtn, P("Remove selected", "移除選取"));
        SetButtonLabel(ClearSelectionBtn, P("Clear", "清除"));
        SnapshotLabelBox.PlaceholderText = P("Snapshot label (optional)", "快照標籤（可選）");
        SetButtonLabel(SnapshotBtn, P("Take snapshot", "影快照"));
        SetButtonLabel(RefreshHistoryBtn, P("Refresh history", "重新整理歷史"));
        SetButtonLabel(VerifyRepositoryBtn, P("Verify repository", "驗證儲存庫"));

        ArchiveTitle.Text = P("Encrypted archive export", "加密壓縮檔匯出");
        ArchiveDescription.Text = P(
            "Export the complete Dew repository as a 7z archive. Encryption runs through the installed 7z.dll in-process so the secret never enters a command line, environment variable, or response file.",
            "將完整 Dew 儲存庫匯出成 7z 壓縮檔。加密會喺程序內經已安裝嘅 7z.dll 執行，所以密碼唔會出現喺指令列、環境變數或者回應檔。" );
        EncryptArchiveToggle.Header = P("Encrypt archive and filenames", "加密壓縮檔同檔名");
        ArchivePasswordBox.PlaceholderText = P("Archive password", "壓縮檔密碼");
        ArchivePasswordConfirmBox.PlaceholderText = P("Confirm password", "確認密碼");
        PasswordHintText.Text = P(
            "Encrypted archives require at least 12 characters. Leave encryption off for an unencrypted local export.",
            "加密壓縮檔需要最少 12 個字元。想要未加密嘅本機匯出，就關閉加密。" );
        SetButtonLabel(CreateArchiveBtn, P("Create archive", "建立壓縮檔"));
        SetButtonLabel(TestArchiveBtn, P("Test archive", "測試壓縮檔"));

        HistoryTitle.Text = P("History and restore", "歷史同還原");
        HistoryDescription.Text = P(
            "Inspect committed snapshots before restoring. Choose one target in the selection list when a repository has several sources. Current managed content is safety-snapshotted immediately before replacement.",
            "還原之前可以檢查已提交快照。如果倉庫有多個來源，請喺選取清單揀一個目標。目前受管理內容會喺替換前即刻影安全快照。" );
        SetButtonLabel(RestoreCommitBtn, P("Restore selected snapshot", "還原選取快照"));

        AutomationTitle.Text = P("Automatic history", "自動歷史");
        AutomationDescription.Text = P(
            "Watch one file or folder and create a debounced snapshot after changes. Dew ignores its own archive directory to avoid feedback loops.",
            "監察一個檔案或者資料夾；有改動之後延遲影快照。Dew 會忽略自己嘅壓縮檔資料夾，避免循環觸發。" );
        DebounceSecondsBox.Header = P("Delay (seconds)", "延遲（秒）");
        SetButtonLabel(StartWatcherBtn, P("Start watcher", "開始監察"));
        SetButtonLabel(StopWatcherBtn, P("Stop watcher", "停止監察"));

        VaultTitle.Text = P("Need an encrypted working volume?", "需要加密工作磁碟區？");
        VaultDescription.Text = P(
            "Dew history is version control, not an encrypted workspace. Use WinForge Vault when files must stay encrypted at rest while you work.",
            "Dew 歷史係版本控制，唔係加密工作空間。工作期間檔案都要保持靜態加密，就用 WinForge 保險庫。" );
        SetButtonLabel(OpenVaultBtn, P("Open WinForge Vault", "開啟 WinForge 保險庫"));
        PlaintextWarning.Title = P("Local history is plaintext", "本機歷史係明文");
        PlaintextWarning.Message = P(
            "The adjacent .dew-encryption-repo is an ordinary plaintext copy. Encrypting an exported 7z does not encrypt that local Git history. ACLs, alternate data streams, EFS state and hard-link identity are not preserved.",
            "旁邊嘅 .dew-encryption-repo 係普通明文副本。加密匯出 7z 唔會加密本機 Git 歷史。ACL、替代資料流、EFS 狀態同硬連結身份唔會保留。" );

        AutomationProperties.SetName(SelectedPathsList, SelectionTitle.Text);
        AutomationProperties.SetName(RepositoryPathText, P("Dew repository location", "Dew 儲存庫位置"));
        AutomationProperties.SetName(SnapshotLabelBox, P("Optional snapshot label", "可選快照標籤"));
        AutomationProperties.SetName(EncryptArchiveToggle, EncryptArchiveToggle.Header?.ToString() ?? ArchiveTitle.Text);
        AutomationProperties.SetName(ArchivePasswordBox, P("Archive password", "壓縮檔密碼"));
        AutomationProperties.SetName(ArchivePasswordConfirmBox, P("Confirm archive password", "確認壓縮檔密碼"));
        AutomationProperties.SetName(HistoryList, P("Snapshot history", "快照歷史"));
        AutomationProperties.SetName(CommitDetailsBox, P("Selected snapshot details", "所選快照詳情"));
        AutomationProperties.SetName(ChangedFilesList, P("Changed files", "已變更檔案"));
        AutomationProperties.SetName(DebounceSecondsBox, DebounceSecondsBox.Header?.ToString() ?? AutomationTitle.Text);
        AutomationProperties.SetName(WatcherStatusText, P("Automatic history status", "自動歷史狀態"));
        ToolTipService.SetToolTip(EncryptArchiveToggle, PasswordHintText.Text);
        ToolTipService.SetToolTip(DebounceSecondsBox, AutomationDescription.Text);

        RefreshProjectUi();
    }

    private async Task RefreshToolAvailabilityAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var gitTask = DewEncryptionService.IsGitAvailableAsync(cts.Token);
            var sevenZipTask = DewEncryptionService.IsSevenZipAvailableAsync(cts.Token);
            await Task.WhenAll(gitTask, sevenZipTask);
            bool git = await gitTask;
            bool sevenZip = await sevenZipTask;
            if (!IsLoaded) return;

            GitBar.IsOpen = !git;
            GitBar.Title = P("Git is required", "需要 Git");
            GitBar.Message = P("Install Git for Windows before taking or restoring Dew snapshots.", "影快照或者還原 Dew 資料之前，請先安裝 Git for Windows。" );
            GitBar.Content = git ? null : EngineBars.AutoInstallProgress(
                "Git.Git", "Install Git", "安裝 Git", recheck: RefreshToolAvailabilityAsync);
            SevenZipBar.IsOpen = !sevenZip;
            SevenZipBar.Title = P("7-Zip is required for archives", "壓縮檔需要 7-Zip");
            SevenZipBar.Message = P("Install 7-Zip before creating or testing encrypted archive exports.", "建立或者測試加密壓縮檔匯出之前，請先安裝 7-Zip。" );
            SevenZipBar.Content = sevenZip ? null : EngineBars.AutoInstallProgress(
                "7zip.7zip", "Install 7-Zip", "安裝 7-Zip",
                recheck: RefreshToolAvailabilityAsync, rescan: ArchiveService.Rescan);
        }
        catch
        {
            GitBar.IsOpen = true;
            GitBar.Title = P("Tool check unavailable", "工具檢查未能完成");
            GitBar.Message = P("WinForge could not verify Git and 7-Zip right now.", "WinForge 暫時未能驗證 Git 同 7-Zip。" );
        }
    }

    private void RefreshProjectUi()
    {
        _project = null;
        if (_openedProject is not null
            && _selectedPaths.SequenceEqual(_openedProject.RestoreTargets, StringComparer.OrdinalIgnoreCase))
            _project = _openedProject;
        else if (_selectedPaths.Count > 0)
        {
            _openedProject = null;
            try { _project = DewSnapshotCore.CreateProject(_selectedPaths); }
            catch (Exception ex) { ShowResult(InfoBarSeverity.Warning, P("Selection needs attention", "選取項目要處理"), FriendlyError(ex)); }
        }

        var project = _project;
        RepositoryPathText.Text = project is null
            ? P("Choose one or more existing files or folders to create a local Dew repository.", "揀一個或者多個現有檔案／資料夾，就可以建立本機 Dew 儲存庫。")
            : P("Repository: ", "儲存庫：") + project.RepositoryDirectory
                + (project.IsReadOnlyImport
                    ? P(" (extracted import: history/restore only)", "（解壓匯入：只限歷史／還原）") : "")
                + (project.RequiresExplicitRestoreTargetConfirmation
                    ? P(" (multi-source restore paths are inferred)", "（多來源還原路徑係推斷結果）") : "");
        LastArchivePathText.Text = string.IsNullOrWhiteSpace(_lastArchivePath)
            ? P("No archive export yet.", "仲未建立壓縮檔匯出。")
            : P("Last archive: ", "最近壓縮檔：") + _lastArchivePath;

        ArchivePasswordPanel.Visibility = EncryptArchiveToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        UpdateInteractiveState();
    }

    private void UpdateInteractiveState()
    {
        bool hasProject = _project is not null;
        bool canUse = hasProject && !_busy;
        bool canMutate = canUse && _project?.IsReadOnlyImport != true;
        SnapshotBtn.IsEnabled = canMutate;
        RefreshHistoryBtn.IsEnabled = canUse;
        VerifyRepositoryBtn.IsEnabled = canUse;
        CreateArchiveBtn.IsEnabled = canMutate;
        TestArchiveBtn.IsEnabled = !_busy;
        RestoreCommitBtn.IsEnabled = canUse && RestoreTarget() is not null && HistoryList.SelectedItem is DewHistoryEntry;
        StartWatcherBtn.IsEnabled = canMutate && _project?.Paths.Count == 1 && _watcher is null;
        StopWatcherBtn.IsEnabled = !_busy && _watcher is not null;
        OpenVaultBtn.IsEnabled = !_busy;
        AddFilesBtn.IsEnabled = !_busy;
        AddFolderBtn.IsEnabled = !_busy;
        OpenExistingHistoryBtn.IsEnabled = !_busy;
        RemoveSelectionBtn.IsEnabled = !_busy && SelectedPathsList.SelectedItems.Count > 0;
        ClearSelectionBtn.IsEnabled = !_busy && _selectedPaths.Count > 0;
        CancelOperationBtn.IsEnabled = _busy && !_cancellationRequested;
        SetButtonLabel(CancelOperationBtn,
            _cancellationRequested
                ? P("Cancellation requested", "已要求取消")
                : P("Request cancellation", "要求取消操作"),
            P("Dew stops at the next safe checkpoint; an in-process archive step may finish first.",
                "Dew 會喺下一個安全檢查點停止；程序內壓縮步驟可能會先完成。"));

        WatcherStatusText.Text = _watcher is null
            ? P("Watcher is stopped. Select exactly one file or folder to enable automatic history.", "監察器已停止。揀啱一個檔案或者資料夾先可以開自動歷史。")
            : P($"Watching with a {Math.Max(0.5, _watcher.Debounce.TotalSeconds):0.#}-second debounce.", $"而家監察緊，延遲 {_watcher.Debounce.TotalSeconds:0.#} 秒先影快照。" );
    }

    private DewProjectContext? RequireProject()
    {
        if (_project is not null) return _project;
        ShowResult(InfoBarSeverity.Warning, P("Choose a file or folder first", "請先揀檔案或者資料夾"),
            P("Dew needs an existing selection before it can create history.", "Dew 需要一個現有選取項目先可以建立歷史。" ));
        return null;
    }

    private async Task<bool> RunBusyAsync(string title, Func<CancellationToken, Task> action)
    {
        if (_busy) return false;
        _busy = true;
        _cancellationRequested = false;
        _operationCts = new CancellationTokenSource();
        UpdateInteractiveState();
        try
        {
            await action(_operationCts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            ShowResult(InfoBarSeverity.Informational, P("Stopped at a safe checkpoint", "已喺安全檢查點停止"),
                P("Dew observed the cancellation request. A native archive step may have finished before cancellation was observed.",
                    "Dew 已收到取消要求。程序內壓縮步驟可能喺系統收到取消之前已經完成。"));
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, title, FriendlyError(ex));
        }
        finally
        {
            _operationCts?.Dispose();
            _operationCts = null;
            _busy = false;
            _cancellationRequested = false;
            UpdateInteractiveState();
        }
        return false;
    }

    private async Task<bool> RunProjectOperationAsync(string title, Func<DewProjectContext, CancellationToken, Task> action)
    {
        var project = RequireProject();
        return project is not null && await RunBusyAsync(title, ct => action(project, ct));
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var paths = await FileDialogs.OpenFilesAsync(FileDialogs.BuildFilters(null), P("Choose files for Dew history", "揀 Dew 歷史檔案"));
            AddPaths(paths);
        }
        catch (Exception ex) { ShowResult(InfoBarSeverity.Error, P("Could not choose files", "揀檔案失敗"), FriendlyError(ex)); }
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenFolderAsync(P("Choose a folder for Dew history", "揀 Dew 歷史資料夾"));
            if (!string.IsNullOrWhiteSpace(path)) AddPaths([path]);
        }
        catch (Exception ex) { ShowResult(InfoBarSeverity.Error, P("Could not choose folder", "揀資料夾失敗"), FriendlyError(ex)); }
    }

    private async void OpenExistingHistory_Click(object sender, RoutedEventArgs e)
    {
        string? chosen;
        try
        {
            chosen = await FileDialogs.OpenFolderAsync(P(
                $"Choose {DewSnapshotCore.RepositoryDirectoryName} or {DewSnapshotCore.ArchiveDirectoryName}",
                $"揀 {DewSnapshotCore.RepositoryDirectoryName} 或 {DewSnapshotCore.ArchiveDirectoryName}"));
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, P("Could not choose history", "揀歷史失敗"), FriendlyError(ex));
            return;
        }
        if (string.IsNullOrWhiteSpace(chosen)) return;

        DewProjectContext? opened = null;
        bool done = await RunBusyAsync(P("Could not open Dew history", "未能開啟 Dew 歷史"), async ct =>
            opened = await DewEncryptionService.OpenExistingProjectAsync(chosen, ct));
        if (!done || opened is null) return;
        _openedProject = opened;
        _selectedPaths.Clear();
        foreach (var target in opened.RestoreTargets) _selectedPaths.Add(target);
        ClearHistoryUi();
        RefreshProjectUi();
        await RefreshHistoryAsync();
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        _openedProject = null;
        int added = 0;
        foreach (var path in paths)
        {
            try
            {
                var full = Path.GetFullPath(path);
                if ((File.Exists(full) || Directory.Exists(full)) && !_selectedPaths.Contains(full, StringComparer.OrdinalIgnoreCase))
                {
                    _selectedPaths.Add(full);
                    added++;
                }
            }
            catch { }
        }
        if (added == 0) return;
        DisposeWatcher();
        ClearHistoryUi();
        RefreshProjectUi();
        ShowResult(InfoBarSeverity.Success, P("Selection updated", "選取項目已更新"), P($"Added {added} item(s).", $"已加入 {added} 個項目。" ));
    }

    private void RemoveSelection_Click(object sender, RoutedEventArgs e)
    {
        var remove = SelectedPathsList.SelectedItems.OfType<string>().ToList();
        if (remove.Count == 0) return;
        _openedProject = null;
        foreach (var path in remove) _selectedPaths.Remove(path);
        DisposeWatcher();
        ClearHistoryUi();
        RefreshProjectUi();
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedPaths.Count == 0) return;
        _openedProject = null;
        _selectedPaths.Clear();
        DisposeWatcher();
        ClearHistoryUi();
        RefreshProjectUi();
    }

    private void SelectedPaths_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateInteractiveState();

    private async void Snapshot_Click(object sender, RoutedEventArgs e)
    {
        var project = RequireProject();
        if (project is null || !await ConfirmSelectionReplacementAsync(project)) return;
        DewSnapshotResult? result = null;
        bool done = await RunProjectOperationAsync(P("Snapshot failed", "影快照失敗"), async (p, ct) =>
            result = await DewEncryptionService.TakeSnapshotAsync(p, SnapshotLabelBox.Text, DewEncryptionService.HasSelectionConflict(p), ct));
        if (!done || result is null) return;

        SnapshotLabelBox.Text = "";
        var snapshotMessage = string.IsNullOrEmpty(result.Commit)
            ? P("The empty-folder working tree is ready; Git will create its first commit when content exists.",
                "空資料夾工作樹已準備好；有內容時 Git 先會建立第一個 commit。")
            : P($"Commit {result.Commit}: {result.Copy.Files} file(s), {result.Copy.Directories} folder(s).",
                $"提交 {result.Commit}：{result.Copy.Files} 個檔案、{result.Copy.Directories} 個資料夾。" );
        ShowResult(InfoBarSeverity.Success, P("Snapshot complete", "快照完成"), snapshotMessage);
        await RefreshHistoryAsync();
    }

    private async void RefreshHistory_Click(object sender, RoutedEventArgs e) => await RefreshHistoryAsync();

    private async Task RefreshHistoryAsync()
    {
        IReadOnlyList<DewHistoryEntry>? entries = null;
        bool done = await RunProjectOperationAsync(P("History refresh failed", "重新整理歷史失敗"), async (p, ct) =>
            entries = await DewEncryptionService.ListHistoryAsync(p, ct: ct));
        if (!done || entries is null) return;

        _history.Clear();
        foreach (var entry in entries) _history.Add(entry);
        _changes.Clear();
        CommitDetailsBox.Text = entries.Count == 0
            ? P("No snapshots exist for this selection yet.", "呢個選取項目仲未有快照。")
            : P($"Loaded {entries.Count} snapshot(s). Select one to inspect it.", $"已載入 {entries.Count} 個快照。揀一個可以檢查內容。" );
        UpdateInteractiveState();
    }

    private async void VerifyRepository_Click(object sender, RoutedEventArgs e)
    {
        bool done = await RunProjectOperationAsync(P("Repository verification failed", "儲存庫驗證失敗"),
            (p, ct) => DewEncryptionService.VerifyRepositoryAsync(p, ct));
        if (done) ShowResult(InfoBarSeverity.Success, P("Repository verified", "儲存庫已驗證"), P("Git fsck completed without errors.", "Git fsck 完成，冇發現錯誤。" ));
    }

    private void EncryptArchive_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        ArchivePasswordPanel.Visibility = EncryptArchiveToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void CreateArchive_Click(object sender, RoutedEventArgs e)
    {
        var project = RequireProject();
        if (project is null || !await ConfirmSelectionReplacementAsync(project)) return;
        var password = GetArchivePassword();
        if (password is null) return;

        DewArchiveResult? result = null;
        bool done;
        try
        {
            done = await RunProjectOperationAsync(P("Archive export failed", "壓縮檔匯出失敗"), async (p, ct) =>
                result = await DewEncryptionService.CreateArchiveAsync(p, password, SnapshotLabelBox.Text,
                    DewEncryptionService.HasSelectionConflict(p), ct));
        }
        finally
        {
            ArchivePasswordBox.Password = "";
            ArchivePasswordConfirmBox.Password = "";
        }
        if (!done || result is null) return;

        _lastArchivePath = result.ArchivePath;
        SettingsStore.Set(LastArchiveSetting, _lastArchivePath);
        RefreshProjectUi();
        ShowResult(InfoBarSeverity.Success, P("Archive created", "壓縮檔已建立"), _lastArchivePath);
        await RefreshHistoryAsync();
    }

    private async void TestArchive_Click(object sender, RoutedEventArgs e)
    {
        string? archive = File.Exists(_lastArchivePath) ? _lastArchivePath : null;
        if (archive is null)
        {
            try { archive = await FileDialogs.OpenFileAsync(new[] { ".7z" }); }
            catch (Exception ex) { ShowResult(InfoBarSeverity.Error, P("Could not choose archive", "揀壓縮檔失敗"), FriendlyError(ex)); return; }
        }
        if (string.IsNullOrWhiteSpace(archive)) return;
        var passwordBox = new PasswordBox
        {
            PasswordRevealMode = PasswordRevealMode.Peek,
            PlaceholderText = P("Leave empty only for an unencrypted archive", "只係未加密封存先留空"),
        };
        var prompt = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Archive password", "封存密碼"),
            Content = passwordBox,
            PrimaryButtonText = P("Test", "測試"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await prompt.ShowAsync() != ContentDialogResult.Primary) return;
        var password = passwordBox.Password ?? "";
        passwordBox.Password = "";
        bool done = await RunBusyAsync(P("Archive test failed", "壓縮檔測試失敗"), ct => DewEncryptionService.TestArchiveAsync(archive, password, ct));
        if (done) ShowResult(InfoBarSeverity.Success, P("Archive verified", "壓縮檔已驗證"), archive);
    }

    private async void History_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateInteractiveState();
        if (HistoryList.SelectedItem is not DewHistoryEntry entry || _project is null) return;
        int request = ++_detailVersion;
        try
        {
            var details = await DewEncryptionService.GetCommitDetailsAsync(_project, entry.Hash);
            if (request != _detailVersion) return;
            CommitDetailsBox.Text = details.Summary;
            _changes.Clear();
            foreach (var item in details.Changes) _changes.Add(item);
        }
        catch (Exception ex)
        {
            if (request == _detailVersion) ShowResult(InfoBarSeverity.Error, P("Could not read snapshot", "讀取快照失敗"), FriendlyError(ex));
        }
    }

    private async void RestoreCommit_Click(object sender, RoutedEventArgs e)
    {
        var project = RequireProject();
        if (project is null || RestoreTarget() is not string target || HistoryList.SelectedItem is not DewHistoryEntry entry) return;
        bool confirmed = await ConfirmAsync(
            P("Restore selected snapshot?", "還原選取快照？"),
            project.RequiresExplicitRestoreTargetConfirmation
                ? P($"Restore {entry.ShortHash} to:\n{target}\n\nImportant: upstream Dew multi-source history does not store original absolute paths. This target is inferred from its top-level name. Confirm only after checking the exact path above. Current managed content is safety-snapshotted immediately before replacement.",
                    $"將 {entry.ShortHash} 還原到：\n{target}\n\n重要：上游 Dew 多來源歷史唔會儲存原本絕對路徑。呢個目標係按頂層名稱推斷。請核對上面完整路徑後先確認。目前受管理內容會喺替換前即刻影安全快照。")
                : P($"Restore {entry.ShortHash} to:\n{target}\n\nCurrent managed content is safety-snapshotted immediately before replacement. A historical-only target with live data is automatically included in that safety snapshot.",
                    $"將 {entry.ShortHash} 還原到：\n{target}\n\n目前受管理內容會喺替換前即刻影安全快照。只屬歷史但而家有資料嘅目標，會自動加入安全快照。"),
            P("Restore", "還原"));
        if (!confirmed) return;

        DewProjectContext? restoredProject = null;
        bool done = await RunProjectOperationAsync(P("Restore failed", "還原失敗"), async (p, ct) =>
            restoredProject = await DewEncryptionService.RestoreAsync(p, target, entry.Hash, ct,
                confirmedInferredTarget: p.RequiresExplicitRestoreTargetConfirmation));
        if (done)
        {
            if (restoredProject is not null)
            {
                _openedProject = restoredProject;
                _selectedPaths.Clear();
                foreach (var path in restoredProject.RestoreTargets) _selectedPaths.Add(path);
                RefreshProjectUi();
            }
            ShowResult(InfoBarSeverity.Success, P("Snapshot restored", "快照已還原"), entry.DisplayTitle);
            await RefreshHistoryAsync();
        }
    }

    private string? RestoreTarget()
    {
        if (_project is null) return null;
        if (_project.RestoreTargets.Count == 1) return _project.RestoreTargets[0];
        return SelectedPathsList.SelectedItems.Count == 1
            ? SelectedPathsList.SelectedItems[0] as string
            : null;
    }

    private async void StartWatcher_Click(object sender, RoutedEventArgs e)
    {
        var project = RequireProject();
        if (project is null || project.Paths.Count != 1) return;
        if (!await ConfirmSelectionReplacementAsync(project)) return;
        bool baseline = await RunProjectOperationAsync(P("Could not create watcher baseline", "未能建立監察基線"),
            (p, ct) => DewEncryptionService.TakeSnapshotAsync(p, SnapshotLabelBox.Text,
                allowSelectionReplacement: DewEncryptionService.HasSelectionConflict(p), ct: ct));
        if (!baseline) return;
        try
        {
            DisposeWatcher();
            double seconds = double.IsFinite(DebounceSecondsBox.Value)
                ? Math.Clamp(DebounceSecondsBox.Value, 0.5, 60)
                : 2;
            _watcher = new DewHistoryWatcher(project, TimeSpan.FromSeconds(seconds));
            _watcher.SnapshotCompleted += Watcher_SnapshotCompleted;
            _watcher.Start();
            UpdateInteractiveState();
            ShowResult(InfoBarSeverity.Success, P("Watcher started", "監察器已開始"), P("Dew will create snapshots after source changes settle.", "來源改動穩定之後，Dew 就會影快照。" ));
        }
        catch (Exception ex) { ShowResult(InfoBarSeverity.Error, P("Could not start watcher", "未能開始監察"), FriendlyError(ex)); }
    }

    private void StopWatcher_Click(object sender, RoutedEventArgs e)
    {
        DisposeWatcher();
        UpdateInteractiveState();
        ShowResult(InfoBarSeverity.Informational, P("Watcher stopped", "監察器已停止"), P("No further automatic snapshots will be scheduled.", "唔會再安排自動快照。" ));
    }

    private void Watcher_SnapshotCompleted(object? sender, DewAutoSnapshotEventArgs e)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            if (e.Error is not null)
            {
                ShowResult(InfoBarSeverity.Error, P("Automatic snapshot failed", "自動快照失敗"), FriendlyError(e.Error));
                return;
            }
            if (e.Result is not null)
            {
                ShowResult(InfoBarSeverity.Success, P("Automatic snapshot complete", "自動快照完成"),
                    string.IsNullOrEmpty(e.Result.Commit)
                        ? P("No Git commit exists yet because the selection is empty.", "選取項目係空，所以仲未有 Git commit。")
                        : e.Result.Commit);
                await RefreshHistoryAsync();
            }
        });
    }

    private void OpenVault_Click(object sender, RoutedEventArgs e)
    {
        Navigator.GoToModule?.Invoke("module.vault-volumes");
    }

    private void CancelOperation_Click(object sender, RoutedEventArgs e)
    {
        if (_operationCts is null || _cancellationRequested) return;
        _cancellationRequested = true;
        _operationCts.Cancel();
        UpdateInteractiveState();
        ShowResult(InfoBarSeverity.Informational, P("Cancellation requested", "已要求取消"),
            P("Dew will stop at the next safe checkpoint. An in-process 7-Zip create or integrity check cannot be interrupted and may finish first.",
                "Dew 會喺下一個安全檢查點停止。程序內 7-Zip 建立或完整性檢查唔可以中途截停，可能會先完成。"));
    }

    private string? GetArchivePassword()
    {
        if (!EncryptArchiveToggle.IsOn) return "";
        var password = ArchivePasswordBox.Password ?? "";
        if (password != (ArchivePasswordConfirmBox.Password ?? ""))
        {
            ShowResult(InfoBarSeverity.Warning, P("Passwords do not match", "密碼唔一致"), P("Enter the same archive password twice.", "請輸入兩次一樣嘅壓縮檔密碼。" ));
            return null;
        }
        if (password.Length < 12)
        {
            ShowResult(InfoBarSeverity.Warning, P("Use a stronger password", "請用更強密碼"), P("Encrypted archives require at least 12 characters.", "加密壓縮檔需要最少 12 個字元。" ));
            return null;
        }
        return password;
    }

    private async Task<bool> ConfirmSelectionReplacementAsync(DewProjectContext project)
    {
        if (!DewEncryptionService.HasSelectionConflict(project)) return true;
        return await ConfirmAsync(
            P("Replace the Dew working tree?", "取代 Dew 工作樹？"),
            P("This adjacent repository belongs to a different selection. Continue only if you intend to replace its managed files with the current selection.", "呢個旁邊儲存庫屬於另一個選取項目。只應該喺你真係想用而家選取項目取代佢管理檔案時先繼續。"),
            P("Replace", "取代"));
    }

    private async Task<bool> ConfirmAsync(string title, string message, string primary)
    {
        if (XamlRoot is null) return false;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primary,
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ClearHistoryUi()
    {
        _detailVersion++;
        _history.Clear();
        _changes.Clear();
        CommitDetailsBox.Text = "";
    }

    private void DisposeWatcher()
    {
        if (_watcher is null) return;
        _watcher.SnapshotCompleted -= Watcher_SnapshotCompleted;
        _watcher.Dispose();
        _watcher = null;
    }

    private void ShowResult(InfoBarSeverity severity, string title, string message)
    {
        ResultBar.Severity = severity;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private string FriendlyError(Exception error)
    {
        var detail = string.IsNullOrWhiteSpace(error.Message)
            ? error.GetType().Name
            : error.Message.Trim();
        string zh = error switch
        {
            UnauthorizedAccessException => "Windows 拒絕存取。請檢查檔案權限，同埋確認冇其他程式鎖住目標。",
            FileNotFoundException or DirectoryNotFoundException => "指定嘅檔案或者資料夾已經唔存在。請重新揀選後再試。",
            InvalidDataException => "Dew 因為資料或者路徑唔安全而停止，原有內容未有被取代。",
            IOException => "Dew 未能安全讀寫檔案。請檢查可用空間、權限同檔案鎖定狀態。",
            ArgumentException or InvalidOperationException => "目前選取或者儲存庫版面無法安全處理。請按提示修正後再試。",
            DewOperationException when detail.Contains("Git", StringComparison.OrdinalIgnoreCase)
                => "Git 歷史操作失敗。請確認 Git 已安裝，同埋儲存庫通過安全檢查。",
            DewOperationException when detail.Contains("7-Zip", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("archive", StringComparison.OrdinalIgnoreCase)
                => "壓縮檔操作失敗。請確認 7-Zip 已安裝、密碼正確，而且壓縮檔冇損壞。",
            DewOperationException when detail.Contains("symbolic", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("junction", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("reparse", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("unsafe", StringComparison.OrdinalIgnoreCase)
                => "Dew 偵測到唔安全嘅連結或者路徑，所以已停止而且冇改動來源。",
            DewOperationException => "Dew 無法安全完成呢個操作。請檢查下面技術詳情。",
            _ => "WinForge 未能完成呢個 Dew 操作。請檢查下面技術詳情。",
        };
        return P(detail, $"{zh}\n\n技術詳情：{detail}");
    }
}
