using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 全自訂 Minecraft 啟動器 · A fully custom Minecraft launcher (our own UI + logic, not a wrapper around the
/// official launcher). Signs in via the full Microsoft → Xbox Live → XSTS → Minecraft chain
/// (<see cref="MinecraftAuthService"/>), downloads the chosen version + assets + a matching JRE and launches
/// the game (<see cref="MinecraftLauncherService"/>). Supports multiple independent instances/profiles, each
/// with its own version, JVM, memory, game directory and account — no shared/static current-profile state.
///
/// Per handoff 54 §4b: every handler is guarded, long work runs off the UI thread via Task.Run /
/// CancellationToken, dialogs are guarded, we fail open and never crash or hang; tokens are never logged.
/// </summary>
public sealed partial class MinecraftLauncherModule : Page
{
    private const string ClientIdSetting = "minecraft.launcher.azureClientId";

    private List<MinecraftInstance> _instances = new();
    private MinecraftInstance? _editing;
    private MinecraftAccount? _account;          // in-memory only; never persisted
    private IReadOnlyList<MinecraftVersionRef> _versions = Array.Empty<MinecraftVersionRef>();
    private bool _showSnapshots;
    private bool _busy;
    private CancellationTokenSource? _downloadCts;
    private bool _loadingForm;

    public MinecraftLauncherModule()
    {
        InitializeComponent();
        MinecraftAuthService.ClientId = SettingsStore.Get(ClientIdSetting, "");
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); LoadInstances(); UpdateAccountUi(); };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            try { _downloadCts?.Cancel(); } catch { }
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Minecraft Launcher · Minecraft 啟動器";
        HeaderBlurb.Text = P(
            "A fully custom Minecraft launcher: sign in with your Microsoft account, download any version with verified files, and launch the game. Create multiple independent instances, each with its own version, memory, Java and game folder.",
            "全自訂 Minecraft 啟動器：用 Microsoft 帳戶登入、下載任何版本（檔案經校驗），再啟動遊戲。可建立多個獨立 instance，各自有自己嘅版本、記憶體、Java 同遊戲資料夾。");

        ClientIdHeader.Text = P("Azure application (client ID) — required", "Azure 應用程式（client ID）— 必填");
        ClientIdNote.Text = P(
            "Prerequisite: Microsoft sign-in for Minecraft only works with an Azure AD application that Mojang has approved. Create one in the Azure portal (App registrations → public client / \"Mobile and desktop applications\", redirect URI https://login.microsoftonline.com/common/oauth2/nativeclient, scopes XboxLive.signin + offline_access), then paste its client ID below. The official launcher's id is not licensed for third-party use.",
            "先決條件：Minecraft 嘅 Microsoft 登入只可以用經 Mojang 批核嘅 Azure AD 應用程式。喺 Azure 入面建立一個（App registrations → 公開用戶端／「行動裝置及桌面應用程式」，重新導向 URI https://login.microsoftonline.com/common/oauth2/nativeclient，範圍 XboxLive.signin + offline_access），再喺下面貼上佢嘅 client ID。官方啟動器嘅 id 唔可以畀第三方用。");
        ClientIdBox.PlaceholderText = "00000000-0000-0000-0000-000000000000";
        ClientIdBox.Text = MinecraftAuthService.ClientId;
        SaveClientIdBtn.Content = P("Save", "儲存");

        SignInText.Text = P("Sign in (browser)", "登入（瀏覽器）");
        DeviceCodeBtn.Content = P("Sign in (device code)", "登入（裝置碼）");
        SignOutBtn.Content = P("Sign out", "登出");

        InstancesHeader.Text = P("Instances", "Instance");
        ToolTipService.SetToolTip(AddInstanceBtn, P("New instance", "新增 instance"));

        NameBox.Header = P("Instance name", "Instance 名稱");
        VersionBox.Header = P("Game version", "遊戲版本");
        SnapshotsCheck.Content = P("Show snapshots", "顯示快照版本");
        RefreshVersionsBtn.Content = P("Refresh list", "重新整理清單");
        GameDirBox.Header = P("Game directory (isolated saves/config)", "遊戲資料夾（隔離存檔／設定）");
        PickGameDirBtn.Content = P("Pick…", "揀…");
        JavaPathBox.Header = P("Java (javaw.exe) — leave empty to use a downloaded JRE", "Java（javaw.exe）— 留空就用下載嘅 JRE");
        PickJavaBtn.Content = P("Pick…", "揀…");
        MaxMemBox.Header = P("Max memory (MB)", "最大記憶體（MB）");
        MinMemBox.Header = P("Min memory (MB)", "最小記憶體（MB）");
        ExtraArgsBox.Header = P("Extra JVM arguments", "額外 JVM 參數");

        SaveInstanceBtn.Content = P("Save instance", "儲存 instance");
        EnsureInstallControl();
        PlayBtn.Content = P("Play", "遊玩");
        DeleteInstanceBtn.Content = P("Delete", "刪除");

        EulaNote.Text = P(
            "No Mojang files are shipped — everything is downloaded from official Mojang endpoints at runtime. Your use of Minecraft is subject to the Minecraft EULA.",
            "唔會附帶任何 Mojang 檔案 — 全部喺執行時由官方 Mojang 端點下載。你使用 Minecraft 受 Minecraft EULA 約束。");

        UpdatePrereqBar();
        UpdateAccountUi();
    }

    private void UpdatePrereqBar()
    {
        if (MinecraftAuthService.HasClientId)
        {
            PrereqBar.IsOpen = false;
        }
        else
        {
            PrereqBar.IsOpen = true;
            PrereqBar.Severity = InfoBarSeverity.Warning;
            PrereqBar.Title = P("Azure client ID required", "需要 Azure client ID");
            PrereqBar.Message = P(
                "Set your Mojang-approved Azure application client ID before signing in (expand the section below).",
                "登入前要設定你經 Mojang 批核嘅 Azure 應用程式 client ID（展開下面嘅區段）。");
            ClientIdExpander.IsExpanded = true;
        }
    }

    // ----------------------------------------------------------------- client id

    private void SaveClientId_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var id = (ClientIdBox.Text ?? "").Trim();
            MinecraftAuthService.ClientId = id;
            SettingsStore.Set(ClientIdSetting, id);
            UpdatePrereqBar();
            ShowStatus(InfoBarSeverity.Success, P("Saved", "已儲存"),
                P("Azure client ID saved.", "已儲存 Azure client ID。"));
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher save-clientid", ex); }
    }

    // ----------------------------------------------------------------- auth

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        try
        {
            if (!MinecraftAuthService.HasClientId) { UpdatePrereqBar(); return; }
            _busy = true;
            ShowStatus(InfoBarSeverity.Informational, P("Signing in…", "登入中…"), "");
            var instanceId = _editing?.AccountInstanceId is { Length: > 0 } a ? a : (_editing?.Id ?? "shared");
            var result = await MinecraftAuthService.SignInInteractiveAsync(XamlRoot, instanceId);
            await HandleAuthResultAsync(result, instanceId);
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher signin", ex); }
        finally { _busy = false; }
    }

    private async void DeviceCode_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!MinecraftAuthService.HasClientId) { UpdatePrereqBar(); return; }
        _busy = true;
        using var cts = new CancellationTokenSource();
        var dlg = new ContentDialog
        {
            Title = P("Sign in with Microsoft", "用 Microsoft 登入"),
            CloseButtonText = P("Cancel", "取消"),
            XamlRoot = XamlRoot,
            Content = new TextBlock { Text = P("Requesting a code…", "正在取得代碼…"), TextWrapping = TextWrapping.Wrap },
        };
        dlg.Closed += (_, _) => { try { cts.Cancel(); } catch { } };
        try
        {
            var instanceId = _editing?.AccountInstanceId is { Length: > 0 } a ? a : (_editing?.Id ?? "shared");
            var authTask = MinecraftAuthService.SignInDeviceCodeAsync(prompt =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    dlg.Content = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Text = P($"Go to {prompt.VerificationUri} and enter code: {prompt.UserCode}",
                                 $"去 {prompt.VerificationUri} 輸入代碼：{prompt.UserCode}"),
                    };
                });
            }, cts.Token);

            _ = dlg.ShowAsync();
            var result = await authTask;
            try { dlg.Hide(); } catch { }
            await HandleAuthResultAsync(result, instanceId);
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher devicecode", ex); }
        finally { _busy = false; }
    }

    private async Task HandleAuthResultAsync(MinecraftAuthResult result, string instanceId)
    {
        if (!result.Success || result.Account is null)
        {
            if (result.Cancelled) { StatusBar.IsOpen = false; return; }
            ShowStatus(InfoBarSeverity.Error, P("Sign-in failed", "登入失敗"),
                MinecraftAuthService.DescribeError(result.Error));
            return;
        }
        _account = result.Account;
        // Persist ONLY the refresh token, DPAPI-encrypted. Access tokens stay in memory.
        MinecraftAccountStore.SaveRefreshToken(instanceId, result.RefreshToken);
        if (_editing is not null) { _editing.AccountInstanceId = instanceId; MinecraftInstanceStore.Save(_editing); }
        ShowStatus(InfoBarSeverity.Success, P("Signed in", "已登入"),
            P($"Welcome, {_account.Name}.", $"歡迎，{_account.Name}。"));
        await Task.CompletedTask;
        UpdateAccountUi();
    }

    private void SignOut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var instanceId = _editing?.AccountInstanceId is { Length: > 0 } a ? a : (_editing?.Id ?? "shared");
            MinecraftAccountStore.Clear(instanceId);
            _account = null;
            UpdateAccountUi();
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher signout", ex); }
    }

    private void UpdateAccountUi()
    {
        bool signedIn = _account is { OwnsGame: true };
        if (signedIn)
            AccountText.Text = P($"Signed in as {_account!.Name}", $"已以 {_account!.Name} 登入");
        else
            AccountText.Text = P("Not signed in", "未登入");
        SignOutBtn.IsEnabled = signedIn;
        if (PlayBtn is not null) PlayBtn.IsEnabled = signedIn && _editing is not null;
    }

    // ----------------------------------------------------------------- instances

    private void LoadInstances()
    {
        try
        {
            _instances = MinecraftInstanceStore.All();
            RebuildInstanceList();
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher load-instances", ex); }
    }

    private void RebuildInstanceList()
    {
        InstanceList.Items.Clear();
        foreach (var inst in _instances)
            InstanceList.Items.Add(new ListViewItem { Content = string.IsNullOrWhiteSpace(inst.Name) ? inst.Id : inst.Name, Tag = inst.Id });
    }

    private void AddInstance_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var inst = new MinecraftInstance { Name = P("New Instance", "新 Instance") };
            inst.GameDirectory = System.IO.Path.Combine(MinecraftLauncherService.DefaultRoot, "instances", inst.Id);
            MinecraftInstanceStore.Save(inst);
            _instances = MinecraftInstanceStore.All();
            RebuildInstanceList();
            var match = InstanceList.Items.Cast<ListViewItem>().FirstOrDefault(i => (string)i.Tag == inst.Id);
            if (match is not null) InstanceList.SelectedItem = match;
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher add-instance", ex); }
    }

    private void InstanceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (InstanceList.SelectedItem is not ListViewItem item) { _editing = null; EditorPanel.Visibility = Visibility.Collapsed; return; }
            var id = (string)item.Tag;
            _editing = _instances.FirstOrDefault(i => i.Id == id);
            if (_editing is null) { EditorPanel.Visibility = Visibility.Collapsed; return; }
            EditorPanel.Visibility = Visibility.Visible;
            LoadFormFromInstance();
            // Try a silent refresh from a stored token for this instance's account.
            _ = TrySilentSignInAsync(_editing.AccountInstanceId is { Length: > 0 } a ? a : _editing.Id);
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher select-instance", ex); }
    }

    private async Task TrySilentSignInAsync(string instanceId)
    {
        try
        {
            if (!MinecraftAuthService.HasClientId) return;
            var token = MinecraftAccountStore.LoadRefreshToken(instanceId);
            if (string.IsNullOrEmpty(token)) { _account = null; UpdateAccountUi(); return; }
            var result = await MinecraftAuthService.RefreshAsync(token);
            if (result.Success && result.Account is not null)
            {
                _account = result.Account;
                MinecraftAccountStore.SaveRefreshToken(instanceId, result.RefreshToken);
            }
            else _account = null;
            UpdateAccountUi();
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher silent-signin", ex); }
    }

    private void LoadFormFromInstance()
    {
        if (_editing is null) return;
        _loadingForm = true;
        try
        {
            NameBox.Text = _editing.Name;
            GameDirBox.Text = _editing.GameDirectory;
            JavaPathBox.Text = _editing.JavaPath;
            MaxMemBox.Value = _editing.MaxMemoryMb;
            MinMemBox.Value = _editing.MinMemoryMb;
            ExtraArgsBox.Text = _editing.ExtraJvmArgs;
            EnsureVersionItems();
            SelectVersionInBox(_editing.VersionId);
        }
        finally { _loadingForm = false; }
    }

    private void EnsureVersionItems()
    {
        if (VersionBox.Items.Count == 0 && _versions.Count == 0)
            _ = RefreshVersionsAsync();
        else
            RebuildVersionBox();
    }

    private void RebuildVersionBox()
    {
        VersionBox.Items.Clear();
        foreach (var v in _versions)
        {
            if (!_showSnapshots && !string.Equals(v.Type, "release", StringComparison.OrdinalIgnoreCase)) continue;
            VersionBox.Items.Add(new ComboBoxItem { Content = $"{v.Id}  ({v.Type})", Tag = v.Id });
        }
        if (_editing is not null) SelectVersionInBox(_editing.VersionId);
    }

    private void SelectVersionInBox(string versionId)
    {
        var match = VersionBox.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == versionId);
        VersionBox.SelectedItem = match;
    }

    private void Snapshots_Click(object sender, RoutedEventArgs e)
    {
        _showSnapshots = SnapshotsCheck.IsChecked == true;
        RebuildVersionBox();
    }

    private async void RefreshVersions_Click(object sender, RoutedEventArgs e) => await RefreshVersionsAsync();

    private async Task RefreshVersionsAsync()
    {
        try
        {
            ShowStatus(InfoBarSeverity.Informational, P("Loading versions…", "載入版本中…"), "");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _versions = await MinecraftLauncherService.GetVersionsAsync(cts.Token);
            RebuildVersionBox();
            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftLauncher refresh-versions", ex);
            ShowStatus(InfoBarSeverity.Error, P("Failed to load versions", "載入版本失敗"), ex.Message);
        }
    }

    private async void PickGameDir_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = await FileDialogs.OpenFolderAsync(P("Pick game directory", "揀遊戲資料夾"));
            if (!string.IsNullOrEmpty(dir)) GameDirBox.Text = dir;
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher pick-gamedir", ex); }
    }

    private async void PickJava_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenFileAsync("exe");
            if (!string.IsNullOrEmpty(path)) JavaPathBox.Text = path;
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher pick-java", ex); }
    }

    private void SaveInstance_Click(object sender, RoutedEventArgs e) => SaveEditing();

    private void SaveEditing()
    {
        if (_editing is null) return;
        try
        {
            _editing.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? _editing.Id : NameBox.Text.Trim();
            _editing.GameDirectory = GameDirBox.Text ?? "";
            _editing.JavaPath = JavaPathBox.Text ?? "";
            _editing.MaxMemoryMb = (int)Math.Max(512, double.IsNaN(MaxMemBox.Value) ? 2048 : MaxMemBox.Value);
            _editing.MinMemoryMb = (int)Math.Max(256, double.IsNaN(MinMemBox.Value) ? 512 : MinMemBox.Value);
            _editing.ExtraJvmArgs = ExtraArgsBox.Text ?? "";
            if (VersionBox.SelectedItem is ComboBoxItem vi) _editing.VersionId = (string)vi.Tag;
            MinecraftInstanceStore.Save(_editing);
            _instances = MinecraftInstanceStore.All();
            RebuildInstanceList();
            var match = InstanceList.Items.Cast<ListViewItem>().FirstOrDefault(i => (string)i.Tag == _editing.Id);
            if (match is not null) InstanceList.SelectedItem = match;
            if (!_loadingForm)
                ShowStatus(InfoBarSeverity.Success, P("Saved", "已儲存"), "");
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher save-instance", ex); }
    }

    private void DeleteInstance_Click(object sender, RoutedEventArgs e)
    {
        if (_editing is null) return;
        try
        {
            MinecraftInstanceStore.Delete(_editing.Id);
            _editing = null;
            _instances = MinecraftInstanceStore.All();
            RebuildInstanceList();
            EditorPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher delete-instance", ex); }
    }

    // ----------------------------------------------------------------- install / play

    private InstallProgress? _installControl;

    /// <summary>建立「下載／校驗檔案」嘅豐富進度控件 · Build the rich install-progress control for the game-file download.</summary>
    private void EnsureInstallControl()
    {
        if (_installControl is not null)
        {
            _installControl.SetAction(P("Download / verify files", "下載／校驗檔案"), P("Download / verify files", "下載／校驗檔案"), InstallDownloadAsync);
            return;
        }
        _installControl = InstallProgress.Create(P("Download / verify files", "下載／校驗檔案"), P("Download / verify files", "下載／校驗檔案"), InstallDownloadAsync);
        if (InstallHost is not null) InstallHost.Content = _installControl;
    }

    /// <summary>InstallProgress 委派：下載並校驗遊戲檔案 · InstallProgress delegate: download + verify game files with live progress.</summary>
    private async Task<TweakResult> InstallDownloadAsync(IProgress<InstallProgressReport> progress, CancellationToken ct)
    {
        if (_editing is null)
            return TweakResult.Fail("Select or create an instance first.", "請先選擇或建立一個 instance。");
        SaveEditing();
        if (string.IsNullOrWhiteSpace(_editing.VersionId))
            return TweakResult.Fail("Pick a version first.", "請先揀版本。");

        var verRef = _versions.FirstOrDefault(v => v.Id == _editing.VersionId);
        if (verRef is null)
        {
            await RefreshVersionsAsync();
            verRef = _versions.FirstOrDefault(v => v.Id == _editing.VersionId);
            if (verRef is null) return TweakResult.Fail("Version not found.", "搵唔到版本。");
        }

        var root = MinecraftLauncherService.DefaultRoot;
        var launcherProgress = new Progress<LauncherProgress>(p =>
        {
            if (p.Total > 0)
                progress.Report(InstallProgressReport.Progress(p.Fraction * 100.0,
                    $"Downloading {p.Done}/{p.Total}", $"下載中 {p.Done}/{p.Total}"));
            else
                progress.Report(InstallProgressReport.Status("Preparing…", "準備中…"));
        });
        progress.Report(InstallProgressReport.Status("Downloading game files (verified by SHA1)…", "下載遊戲檔案中（用 SHA1 校驗）…"));

        bool ok = await Task.Run(() => MinecraftLauncherService.InstallVersionAsync(root, verRef!, launcherProgress, ct), ct);
        if (!ok) return TweakResult.Fail("Download failed.", "下載失敗。");
        var note = NoteJreIfNeeded(root, verRef!.Id);
        return TweakResult.Ok(note.Length > 0 ? note : "Download complete.", note.Length > 0 ? note : "下載完成。");
    }

    /// <summary>Play 路徑用嘅靜默安裝（用狀態列進度）· Silent install used by the Play path (drives the shared status progress bar).</summary>
    private async Task<bool> InstallAsync()
    {
        if (_busy || _editing is null) return false;
        SaveEditing();
        if (string.IsNullOrWhiteSpace(_editing.VersionId))
        {
            ShowStatus(InfoBarSeverity.Warning, P("Pick a version first", "請先揀版本"), "");
            return false;
        }
        var verRef = _versions.FirstOrDefault(v => v.Id == _editing.VersionId);
        if (verRef is null)
        {
            await RefreshVersionsAsync();
            verRef = _versions.FirstOrDefault(v => v.Id == _editing.VersionId);
            if (verRef is null) { ShowStatus(InfoBarSeverity.Error, P("Version not found", "搵唔到版本"), ""); return false; }
        }

        _busy = true;
        _downloadCts = new CancellationTokenSource();
        DownloadProgress.Visibility = Visibility.Visible;
        ProgressText.Visibility = Visibility.Visible;
        var progress = new Progress<LauncherProgress>(p =>
        {
            DownloadProgress.IsIndeterminate = p.Total <= 0;
            DownloadProgress.Value = p.Fraction;
            ProgressText.Text = P($"Downloading {p.Done}/{p.Total}", $"下載中 {p.Done}/{p.Total}");
        });
        try
        {
            ShowStatus(InfoBarSeverity.Informational, P("Downloading game files…", "下載遊戲檔案中…"),
                P("Files are verified by SHA1.", "檔案會用 SHA1 校驗。"));
            var root = MinecraftLauncherService.DefaultRoot;
            var token = _downloadCts.Token;
            bool ok = await Task.Run(() => MinecraftLauncherService.InstallVersionAsync(root, verRef!, progress, token), token);
            if (ok)
            {
                ShowStatus(InfoBarSeverity.Success, P("Download complete", "下載完成"),
                    NoteJreIfNeeded(root, verRef!.Id));
            }
            else
                ShowStatus(InfoBarSeverity.Error, P("Download failed", "下載失敗"), "");
            return ok;
        }
        catch (OperationCanceledException)
        {
            ShowStatus(InfoBarSeverity.Warning, P("Download cancelled", "已取消下載"), "");
            return false;
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftLauncher install", ex);
            ShowStatus(InfoBarSeverity.Error, P("Download failed", "下載失敗"), ex.Message);
            return false;
        }
        finally
        {
            _busy = false;
            DownloadProgress.Visibility = Visibility.Collapsed;
            ProgressText.Visibility = Visibility.Collapsed;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private string NoteJreIfNeeded(string root, string versionId)
    {
        try
        {
            var versionJson = System.IO.Path.Combine(root, "versions", versionId, versionId + ".json");
            var major = MinecraftLauncherService.RequiredJavaMajor(versionJson);
            var java = MinecraftLauncherService.ResolveJavaPath(root, versionJson, _editing?.JavaPath ?? "");
            if (string.IsNullOrEmpty(java))
                return P($"This version needs Java {major}. No JRE found — install a Temurin/Adoptium JRE {major} and point this instance's Java at its javaw.exe.",
                         $"呢個版本需要 Java {major}。搵唔到 JRE — 安裝 Temurin/Adoptium JRE {major}，再將呢個 instance 嘅 Java 指向佢嘅 javaw.exe。");
            return P("Ready to play.", "可以開始遊玩。");
        }
        catch { return ""; }
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _editing is null) return;
        try
        {
            if (_account is not { OwnsGame: true })
            {
                ShowStatus(InfoBarSeverity.Warning, P("Sign in first", "請先登入"), "");
                return;
            }
            SaveEditing();
            var root = MinecraftLauncherService.DefaultRoot;
            var versionJson = System.IO.Path.Combine(root, "versions", _editing.VersionId, _editing.VersionId + ".json");
            if (!System.IO.File.Exists(versionJson))
            {
                if (!await InstallAsync()) return;
            }

            // Refresh the access token if it's about to expire.
            if (_account.IsExpired)
            {
                var instanceId = _editing.AccountInstanceId is { Length: > 0 } a ? a : _editing.Id;
                var refreshed = await MinecraftAuthService.RefreshAsync(MinecraftAccountStore.LoadRefreshToken(instanceId));
                if (refreshed.Success && refreshed.Account is not null)
                {
                    _account = refreshed.Account;
                    MinecraftAccountStore.SaveRefreshToken(instanceId, refreshed.RefreshToken);
                }
            }

            var instanceCopy = _editing;
            var accountCopy = _account;
            ShowStatus(InfoBarSeverity.Informational, P("Launching…", "啟動中…"), "");
            var (proc, error) = await Task.Run(() =>
            {
                var p = MinecraftLauncherService.Launch(root, instanceCopy, accountCopy, out var err);
                return (p, err);
            });

            if (proc is null)
            {
                ShowStatus(InfoBarSeverity.Error, P("Launch failed", "啟動失敗"),
                    error == "no-java"
                        ? P("No Java found. Set this instance's Java path or download a matching JRE.",
                            "搵唔到 Java。設定呢個 instance 嘅 Java 路徑或者下載對應嘅 JRE。")
                        : error == "version-not-installed"
                            ? P("Version not installed. Download it first.", "版本未安裝。請先下載。")
                            : error);
                return;
            }
            ShowStatus(InfoBarSeverity.Success, P("Game launched", "遊戲已啟動"), "");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftLauncher play", ex);
            ShowStatus(InfoBarSeverity.Error, P("Launch failed", "啟動失敗"), ex.Message);
        }
    }

    // ----------------------------------------------------------------- ui helpers

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
