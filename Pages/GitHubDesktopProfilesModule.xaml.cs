using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// GitHub Desktop 多帳戶設定檔 · Isolated GitHub Desktop identities with per-profile Electron data,
/// Git configuration, named shortcuts, callback routing, and GitHub CLI account management.
/// </summary>
public sealed partial class GitHubDesktopProfilesModule : Page
{
    private readonly ObservableCollection<ProfileRow> _profiles = [];
    private readonly ObservableCollection<GhAccountRow> _ghAccounts = [];
    private GitHubDesktopProfilesStatus? _status;
    private GitHubCliStatus? _ghStatus;
    private bool _busy;
    private bool _languageSubscribed;
    private CancellationTokenSource? _unloadCts;

    public GitHubDesktopProfilesModule()
    {
        InitializeComponent();
        // Keep the defaults out of XAML: typed IsOn literals are unreliable here.
        StartMenuShortcutsToggle.IsOn = true;
        DesktopShortcutsToggle.IsOn = true;
        ProfileList.ItemsSource = _profiles;
        GhAccountCombo.ItemsSource = _ghAccounts;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _unloadCts?.Cancel();
        _unloadCts?.Dispose();
        _unloadCts = new CancellationTokenSource();
        if (!_languageSubscribed)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            _languageSubscribed = true;
        }
        Render();
        await Task.WhenAll(RefreshStatusAsync(), RefreshGhStatusAsync(_unloadCts.Token));
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _unloadCts?.Cancel();
        _unloadCts?.Dispose();
        _unloadCts = null;
        if (!_languageSubscribed) return;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _languageSubscribed = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            Render();
            if (_status is not null) BindStatus(_status);
            if (_ghStatus is not null) BindGhStatus(_ghStatus);
        });
    }

    private void Render()
    {
        Header.Title = "GitHub Desktop Profiles · GitHub Desktop 多帳戶設定檔";
        Header.ActionContent = HeaderActions.NativeWindowButton("githubdesktop");
        HeaderBlurb.Text = P(
            "Run as many genuinely isolated copies of the official GitHub Desktop as you need—each with its own sign-in, app data, Git identity, shortcut, and OAuth callback routing.",
            "按需要開任意數量真正隔離嘅官方 GitHub Desktop——每個都有自己嘅登入、程式資料、Git 身份、捷徑同 OAuth 回呼路由。");

        SetupTitle.Text = P("One-time profile setup", "一次性設定多帳戶");
        RefreshButtonText.Text = P("Refresh", "重新整理");
        StartMenuShortcutsToggle.Header = P("Start Menu shortcuts", "開始功能表捷徑");
        StartMenuShortcutsToggle.OnContent = P("Create", "建立");
        StartMenuShortcutsToggle.OffContent = P("Do not create", "唔建立");
        DesktopShortcutsToggle.Header = P("Desktop shortcuts", "桌面捷徑");
        DesktopShortcutsToggle.OnContent = P("Create", "建立");
        DesktopShortcutsToggle.OffContent = P("Do not create", "唔建立");
        ConfigureButton.Content = P("Configure profiles", "設定帳戶");
        RepairButton.Content = P("Repair routing", "修復路由");

        ProfilesTitle.Text = P("Profile identities", "帳戶設定檔身份");
        ProfilesBlurb.Text = P(
            "Add or remove profiles at any time. Edit a name and leave the field to rename its shortcuts. Launch starts that profile without closing any others; Activate chooses where the next GitHub Desktop sign-in callback goes.",
            "你可以隨時新增或移除設定檔。改名後離開欄位就會一併更新捷徑。開啟設定檔唔會關閉其他視窗；設為使用中就會決定下一個 GitHub Desktop 登入回呼送去邊個帳戶。");
        AddProfileButtonText.Text = P("Add profile", "新增設定檔");

        GhTitle.Text = P("GitHub CLI account manager", "GitHub CLI 帳戶管理員");
        GhBlurb.Text = P(
            "Inspect every account known to gh, switch the active token without exposing it, or open an interactive terminal for a new browser login.",
            "檢視 gh 已知嘅所有帳戶、切換使用中憑證而唔會顯示 token，或者開互動終端新增瀏覽器登入。");
        GhRefreshButtonText.Text = P("Refresh accounts", "重新整理帳戶");
        GhAccountCombo.Header = P("Known accounts", "已知帳戶");
        GhLoginButton.Content = P("Login in terminal", "喺終端登入");
        GhSwitchButton.Content = P("Switch", "切換");
        GhLogoutButton.Content = P("Logout", "登出");

        RoutingTitle.Text = P("Safe callback routing and clean removal", "安全回呼路由同乾淨移除");
        RoutingBlurb.Text = P(
            "WinForge registers only GitHub Desktop's three URL schemes and forwards each callback to the active profile. Callback URLs and credentials are never logged.",
            "WinForge 只會登記 GitHub Desktop 嘅三個網址協定，並將回呼轉交畀使用中嘅設定檔；回呼網址同登入憑證永遠唔會寫入記錄。");
        SafetyBlurb.Text = P(
            "The Native window button opens the official app launcher. For correct account isolation, use the profile Launch buttons above after setup.",
            "「原生視窗」按鈕會開官方應用程式啟動器。設定完成後，請用上面每個設定檔嘅「開啟」按鈕，先可以保持帳戶隔離。");
        UninstallButton.Content = P("Remove profile setup", "移除多帳戶設定");
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            var status = await Task.Run(GitHubDesktopProfilesService.GetStatus);
            _status = status;
            BindStatus(status);
        }
        catch (Exception ex)
        {
            ShowResult(TweakResult.Fail(
                $"Could not inspect the GitHub Desktop profiles: {ex.Message}",
                $"無法檢查 GitHub Desktop 多帳戶設定：{ex.Message}"));
        }
    }

    private async Task RefreshGhStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var status = await GitHubCliAccountService.GetStatusAsync(ct);
            _ghStatus = status;
            BindGhStatus(status);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            ShowResult(TweakResult.Fail(
                $"Could not inspect GitHub CLI accounts: {ex.Message}",
                $"無法檢查 GitHub CLI 帳戶：{ex.Message}"),
                P("GitHub CLI accounts", "GitHub CLI 帳戶"));
        }
    }

    private void BindGhStatus(GitHubCliStatus status)
    {
        string? selectedLogin = (GhAccountCombo.SelectedItem as GhAccountRow)?.Login;

        GhEngineBar.IsOpen = !status.Installed;
        if (!status.Installed)
        {
            GhEngineBar.Title = P("GitHub CLI is not installed", "未安裝 GitHub CLI");
            GhEngineBar.Message = P(
                "Install the official gh command-line client with winget to manage its accounts here.",
                "用 winget 安裝官方 gh 命令列工具，就可以喺度管理帳戶。");
            GhEngineBar.ActionButton = null;
            GhEngineBar.Content = EngineBars.AutoInstallProgress(
                "GitHub.cli",
                "Install GitHub CLI",
                "安裝 GitHub CLI",
                recheck: () => RefreshGhStatusAsync());
        }
        else
        {
            GhEngineBar.ActionButton = null;
            GhEngineBar.Content = null;
        }

        GhVersionText.Text = status.Installed
            ? (!string.IsNullOrWhiteSpace(status.Version) ? status.Version : P("installed", "已安裝"))
            : P("not installed", "未安裝");

        GhEnvironmentText.Text = status.EnvironmentOverride
            ? P("Environment override active: GH_TOKEN or GITHUB_TOKEN takes priority. Clear it before login, switch, or logout.",
                "環境覆寫生效中：GH_TOKEN 或 GITHUB_TOKEN 會優先；登入、切換或者登出之前請先清除。")
            : P("Configuration: default gh credential storage", "設定：預設 gh 憑證儲存位置");

        _ghAccounts.Clear();
        foreach (var account in status.Accounts)
            _ghAccounts.Add(GhAccountRow.From(account, P));

        GhAccountRow? selected = _ghAccounts.FirstOrDefault(a =>
                string.Equals(a.Login, selectedLogin, StringComparison.OrdinalIgnoreCase))
            ?? _ghAccounts.FirstOrDefault(a => a.Active)
            ?? _ghAccounts.FirstOrDefault();
        GhAccountCombo.SelectedItem = selected;

        if (!string.IsNullOrWhiteSpace(status.Error))
        {
            GhStatusText.Text = P($"gh reported: {status.Error}", $"gh 回報：{status.Error}");
        }
        else if (!status.Installed)
        {
            GhStatusText.Text = P("Install GitHub CLI to discover and manage accounts.", "安裝 GitHub CLI 先可以搵到同管理帳戶。");
        }
        else if (_ghAccounts.Count == 0)
        {
            GhStatusText.Text = P("No authenticated accounts. Open a terminal login to add one.", "未有已驗證帳戶；請開終端登入新增一個。");
        }
        else
        {
            var active = _ghAccounts.FirstOrDefault(a => a.Active);
            GhStatusText.Text = active is null
                ? P($"{_ghAccounts.Count} account(s) found; none is marked active.", $"搵到 {_ghAccounts.Count} 個帳戶；未有帳戶標示為使用中。")
                : P($"{_ghAccounts.Count} account(s) found. Active: {active.Login}@{active.Host}",
                    $"搵到 {_ghAccounts.Count} 個帳戶。使用中：{active.Login}@{active.Host}");
        }

        UpdateGhSelection();
    }

    private void BindStatus(GitHubDesktopProfilesStatus status)
    {
        StartMenuShortcutsToggle.IsOn = status.CreateStartMenuShortcuts;
        DesktopShortcutsToggle.IsOn = status.CreateDesktopShortcuts;
        EngineBar.IsOpen = !status.DesktopInstalled;
        if (!status.DesktopInstalled)
        {
            EngineBar.Title = P("GitHub Desktop is not installed", "未安裝 GitHub Desktop");
            EngineBar.Message = P(
                "Install the official app with winget, then configure your profiles here.",
                "用 winget 安裝官方應用程式，之後返嚟設定你嘅帳戶。");
            EngineBar.ActionButton = null;
            EngineBar.Content = EngineBars.AutoInstallProgress(
                "GitHub.GitHubDesktop",
                "Install GitHub Desktop",
                "安裝 GitHub Desktop",
                recheck: RefreshStatusAsync);
        }
        else
        {
            EngineBar.ActionButton = null;
            EngineBar.Content = null;
        }

        if (!status.DesktopInstalled)
        {
            SetupStatusText.Text = P("Waiting for the official GitHub Desktop app.", "等緊官方 GitHub Desktop 應用程式。");
        }
        else if (!status.IsConfigured)
        {
            SetupStatusText.Text = status.Profiles.Any(p => p.DataExists)
                ? P("Existing profile data was detected. Configure profiles to connect it to WinForge.",
                    "偵測到現有設定檔資料；請按「設定帳戶」連接到 WinForge。")
                : P("Ready to create isolated profiles.", "已準備好建立隔離帳戶。");
        }
        else if (!status.BrokerReady)
        {
            SetupStatusText.Text = P("The profile launcher is missing. Run Repair routing.", "設定檔啟動器唔見咗，請執行「修復路由」。");
        }
        else if (!status.HandlersOwned)
        {
            SetupStatusText.Text = P("Profiles exist, but callback routing needs repair.", "設定檔已存在，但回呼路由需要修復。");
        }
        else if (!status.ShortcutsReady)
        {
            SetupStatusText.Text = P(
                "The selected shortcut locations need repair. Run Repair routing.",
                "所選捷徑位置需要修復；請執行「修復路由」。");
        }
        else
        {
            SetupStatusText.Text = P(
                "All profiles, selected shortcuts, and callback routes are ready.",
                "全部設定檔、所選捷徑同回呼路由都已就緒。");
        }

        var active = status.Profiles.FirstOrDefault(p => p.IsActive)
            ?? status.Profiles.FirstOrDefault(p => string.Equals(p.Id, status.ActiveProfileId, StringComparison.OrdinalIgnoreCase));
        ActiveProfileText.Text = active is null
            ? P("Active callback profile: not selected", "使用中回呼設定檔：未選擇")
            : P($"Active callback profile: {active.Name}", $"使用中回呼設定檔：{active.Name}");

        _profiles.Clear();
        foreach (var profile in status.Profiles)
        {
            _profiles.Add(ProfileRow.From(
                profile,
                P,
                status.Profiles.Count > 1,
                status.IsConfigured,
                profile.ShortcutsReady,
                status.CreateStartMenuShortcuts || status.CreateDesktopShortcuts));
        }

        int ready = status.Profiles.Count(p => p.DataExists);
        ProfilesSummaryText.Text = P(
            $"{ready}/{status.Profiles.Count} data folders ready",
            $"{ready}/{status.Profiles.Count} 個資料夾已就緒");

        ConfigureButton.IsEnabled = status.DesktopInstalled && !_busy;
        RepairButton.IsEnabled = status.DesktopInstalled && !_busy;
        AddProfileButton.IsEnabled = status.DesktopInstalled && !_busy;
    }

    private IReadOnlyList<string> CurrentNames()
        => _profiles.Select(p => p.Name.Trim()).ToArray();

    private async void AddProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var nameBox = new TextBox
        {
            Header = P("Profile name", "設定檔名稱"),
            PlaceholderText = P("For example: Client work", "例如：客戶工作"),
            MaxLength = 48,
        };
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Add a GitHub Desktop profile", "新增 GitHub Desktop 設定檔"),
            Content = nameBox,
            PrimaryButtonText = P("Add profile", "新增設定檔"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };

        try
        {
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            string name = nameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowResult(TweakResult.Fail("A profile name cannot be empty.", "設定檔名稱唔可以留空。"));
                return;
            }

            await RunActionAsync(() => GitHubDesktopProfilesService.Add(
                name, StartMenuShortcutsToggle.IsOn, DesktopShortcutsToggle.IsOn));
        }
        catch (Exception ex)
        {
            ShowResult(TweakResult.Fail(ex.Message, $"無法顯示新增視窗：{ex.Message}"));
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        try { await Task.WhenAll(RefreshStatusAsync(), RefreshGhStatusAsync()); }
        finally { SetBusy(false); }
    }

    private async void Configure_Click(object sender, RoutedEventArgs e)
        => await RunActionAsync(() => GitHubDesktopProfilesService.Configure(
            CurrentNames(), StartMenuShortcutsToggle.IsOn, DesktopShortcutsToggle.IsOn));

    private async void Repair_Click(object sender, RoutedEventArgs e)
        => await RunActionAsync(() => GitHubDesktopProfilesService.Repair(
            StartMenuShortcutsToggle.IsOn, DesktopShortcutsToggle.IsOn));

    private async void LaunchProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
            await RunActionAsync(() => GitHubDesktopProfilesService.Launch(id));
    }

    private async void ActivateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
            await RunActionAsync(() => GitHubDesktopProfilesService.Activate(id));
    }

    private async void OpenProfileFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
            await RunActionAsync(() => GitHubDesktopProfilesService.OpenFolder(id));
    }

    private async void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button { Tag: string id }) return;
        var row = _profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (row is null) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Remove “{row.Name}”?", $"移除「{row.Name}」？"),
            Content = P(
                "WinForge will remove this profile from its launcher and delete its shortcuts. Its existing app-data folder is kept so local data is not destroyed.",
                "WinForge 會由啟動器移除呢個設定檔並刪除佢嘅捷徑；現有程式資料夾會保留，唔會破壞本機資料。"),
            PrimaryButtonText = P("Remove profile", "移除設定檔"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };

        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await RunActionAsync(() => GitHubDesktopProfilesService.Remove(
                    id, StartMenuShortcutsToggle.IsOn, DesktopShortcutsToggle.IsOn));
        }
        catch (Exception ex)
        {
            ShowResult(TweakResult.Fail(ex.Message, $"無法顯示確認視窗：{ex.Message}"));
        }
    }

    private async void ProfileName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not TextBox { Tag: string id } box) return;
        var row = _profiles.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        var next = box.Text.Trim();
        if (row is null || string.Equals(next, row.Name, StringComparison.Ordinal)) return;
        if (string.IsNullOrWhiteSpace(next))
        {
            box.Text = row.Name;
            ShowResult(TweakResult.Fail("A profile name cannot be empty.", "設定檔名稱唔可以留空。"));
            return;
        }

        // Before the first setup, keep edits in the view model so Configure can
        // submit every chosen name together.
        if (_status is { IsConfigured: false })
        {
            row.Name = next;
            return;
        }

        await RunActionAsync(() => GitHubDesktopProfilesService.Rename(
            id, next, StartMenuShortcutsToggle.IsOn, DesktopShortcutsToggle.IsOn));
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Remove GitHub Desktop profile setup?", "移除 GitHub Desktop 多帳戶設定？"),
            Content = P(
                "This removes WinForge's shortcuts and callback routing. Existing profile data folders are kept so your local app data is not destroyed.",
                "呢個動作會移除 WinForge 捷徑同回呼路由，但會保留現有設定檔資料夾，唔會刪除你嘅本機應用程式資料。"),
            PrimaryButtonText = P("Remove setup", "移除設定"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await RunActionAsync(GitHubDesktopProfilesService.Uninstall);
        }
        catch (Exception ex)
        {
            ShowResult(TweakResult.Fail(ex.Message, $"無法顯示確認視窗：{ex.Message}"));
        }
    }

    private async void GhRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        try { await RefreshGhStatusAsync(); }
        finally { SetBusy(false); }
    }

    private void GhAccountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateGhSelection();

    private async void GhLogin_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        await RunGhActionAsync(() => Task.FromResult(GitHubCliAccountService.OpenLoginTerminal()));
    }

    private async void GhSwitch_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || GhAccountCombo.SelectedItem is not GhAccountRow account) return;
        await RunGhActionAsync(() => GitHubCliAccountService.SwitchAsync(account.Login));
    }

    private async void GhLogout_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || GhAccountCombo.SelectedItem is not GhAccountRow account) return;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Log out {account.Login}?", $"登出 {account.Login}？"),
            Content = P(
                $"GitHub CLI will remove the stored authentication for {account.Login} on {account.Host}. This does not remove the GitHub Desktop profile.",
                $"GitHub CLI 會移除 {account.Login} 喺 {account.Host} 嘅已儲存驗證；GitHub Desktop 設定檔唔會被移除。"),
            PrimaryButtonText = P("Logout", "登出"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };

        try
        {
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                await RunGhActionAsync(() => GitHubCliAccountService.LogoutAsync(account.Login));
        }
        catch (Exception ex)
        {
            ShowResult(TweakResult.Fail(ex.Message, $"無法顯示確認視窗：{ex.Message}"),
                P("GitHub CLI accounts", "GitHub CLI 帳戶"));
        }
    }

    private void UpdateGhSelection()
    {
        var account = GhAccountCombo.SelectedItem as GhAccountRow;
        GhAccountDetailsText.Text = account?.Details ?? P("Select an account to inspect it.", "揀一個帳戶查看詳細資料。");
        bool available = !_busy && (_ghStatus?.Installed ?? false) && account is not null;
        GhSwitchButton.IsEnabled = available && !account!.Active && !(_ghStatus?.EnvironmentOverride ?? false);
        GhLogoutButton.IsEnabled = available && !(_ghStatus?.EnvironmentOverride ?? false);
        GhLoginButton.IsEnabled = !_busy && (_ghStatus?.Installed ?? false) && !(_ghStatus?.EnvironmentOverride ?? false);
        GhAccountCombo.IsEnabled = !_busy && (_ghStatus?.Installed ?? false) && _ghAccounts.Count > 0;
    }

    private async Task RunGhActionAsync(Func<Task<TweakResult>> action)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            TweakResult result;
            try { result = await action(); }
            catch (Exception ex)
            {
                result = TweakResult.Fail(ex.Message, $"GitHub CLI 操作失敗：{ex.Message}");
            }

            ShowResult(result, P("GitHub CLI accounts", "GitHub CLI 帳戶"));
            await RefreshGhStatusAsync();
        }
        finally
        {
            SetBusy(false);
            if (_ghStatus is not null) BindGhStatus(_ghStatus);
        }
    }

    private async Task RunActionAsync(Func<TweakResult> action)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            TweakResult result;
            try { result = await Task.Run(action); }
            catch (Exception ex)
            {
                result = TweakResult.Fail(ex.Message, $"操作失敗：{ex.Message}");
            }

            ShowResult(result);
            await RefreshStatusAsync();
        }
        finally
        {
            SetBusy(false);
            if (_status is not null) BindStatus(_status);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        BusyRing.IsActive = busy;
        RefreshButton.IsEnabled = !busy;
        ConfigureButton.IsEnabled = !busy && (_status?.DesktopInstalled ?? false);
        RepairButton.IsEnabled = !busy && (_status?.DesktopInstalled ?? false);
        AddProfileButton.IsEnabled = !busy && (_status?.DesktopInstalled ?? false);
        ProfileList.IsEnabled = !busy;
        StartMenuShortcutsToggle.IsEnabled = !busy;
        DesktopShortcutsToggle.IsEnabled = !busy;
        UninstallButton.IsEnabled = !busy;
        GhRefreshButton.IsEnabled = !busy;
        GhLoginButton.IsEnabled = !busy
            && (_ghStatus?.Installed ?? false)
            && !(_ghStatus?.EnvironmentOverride ?? false);
        GhAccountCombo.IsEnabled = !busy && (_ghStatus?.Installed ?? false) && _ghAccounts.Count > 0;
        GhSwitchButton.IsEnabled = !busy
            && !(_ghStatus?.EnvironmentOverride ?? false)
            && GhAccountCombo.SelectedItem is GhAccountRow { Active: false };
        GhLogoutButton.IsEnabled = !busy
            && !(_ghStatus?.EnvironmentOverride ?? false)
            && GhAccountCombo.SelectedItem is GhAccountRow;
    }

    private void ShowResult(TweakResult result, string? title = null)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = title ?? P("GitHub Desktop profiles", "GitHub Desktop 多帳戶設定檔");
        ResultBar.Message = result.Message?.Get(Loc.I.Language) ?? P("Done.", "完成。");
    }

    private static string ValueText(object? value)
    {
        if (value is null) return "";
        if (value is string text) return text;
        if (value is IEnumerable<string> values) return string.Join(", ", values);
        return Convert.ToString(value) ?? "";
    }

    private sealed class ProfileRow
    {
        public required string Id { get; init; }
        public required string Name { get; set; }
        public required string Initial { get; init; }
        public required string DataPath { get; init; }
        public required string GitConfigPath { get; init; }
        public required string DataPathText { get; init; }
        public required string GitConfigText { get; init; }
        public required string StateGlyph { get; init; }
        public required string StateText { get; init; }
        public required string LaunchLabel { get; init; }
        public required string ActivateLabel { get; init; }
        public required string OpenFolderLabel { get; init; }
        public required string RemoveLabel { get; init; }
        public bool CanActivate { get; init; }
        public bool CanRemove { get; init; }

        public static ProfileRow From(
            GitHubDesktopProfileStatus profile,
            Func<string, string, string> pick,
            bool canRemove,
            bool configured,
            bool selectedShortcutsReady,
            bool shortcutsRequested)
        {
            string initial = string.IsNullOrWhiteSpace(profile.Name)
                ? "?"
                : char.ToUpperInvariant(profile.Name.Trim()[0]).ToString();
            string stateText = !configured
                ? pick("Not configured", "未設定")
                : !selectedShortcutsReady
                    ? profile.IsActive
                        ? pick("Active; shortcut repair needed", "回呼使用中；捷徑要修復")
                        : pick("Shortcut repair needed", "捷徑需要修復")
                    : !shortcutsRequested
                        ? profile.IsActive
                            ? pick("Active; no shortcuts selected", "回呼使用中；未有選擇捷徑")
                            : pick("Managed without shortcuts", "已管理但冇捷徑")
                        : profile.IsActive
                            ? pick("Active for callbacks", "回呼使用中")
                            : pick("Selected shortcuts ready", "所選捷徑已就緒");
            return new ProfileRow
            {
                Id = profile.Id,
                Name = profile.Name,
                Initial = initial,
                DataPath = profile.DataPath,
                GitConfigPath = profile.GitConfigPath,
                DataPathText = pick($"App data  {profile.DataPath}", $"程式資料  {profile.DataPath}"),
                GitConfigText = pick($"Git config  {profile.GitConfigPath}", $"Git 設定  {profile.GitConfigPath}"),
                StateGlyph = profile.IsActive ? ((char)0xE73E).ToString() : ((char)0xE8B7).ToString(),
                StateText = stateText,
                LaunchLabel = pick("Launch", "開啟"),
                ActivateLabel = profile.IsActive ? pick("Active", "使用中") : pick("Activate", "設為使用中"),
                OpenFolderLabel = pick("Folder", "資料夾"),
                RemoveLabel = pick("Remove", "移除"),
                CanActivate = !profile.IsActive,
                CanRemove = canRemove && !profile.IsDefault,
            };
        }
    }

    private sealed class GhAccountRow
    {
        public required string Login { get; init; }
        public required string Host { get; init; }
        public required string Glyph { get; init; }
        public required string Details { get; init; }
        public bool Active { get; init; }

        public static GhAccountRow From(GitHubCliAccount account, Func<string, string, string> pick)
        {
            string state = ValueText(account.State);
            string scopes = ValueText(account.Scopes);
            string tokenSource = ValueText(account.TokenSource);
            string gitProtocol = ValueText(account.GitProtocol);
            string error = ValueText(account.Error);

            var details = new List<string>
            {
                pick($"state={Fallback(state)}", $"狀態={Fallback(state)}"),
                pick($"protocol={Fallback(gitProtocol)}", $"協定={Fallback(gitProtocol)}"),
                pick($"token={Fallback(tokenSource)}", $"憑證來源={Fallback(tokenSource)}"),
                pick($"scopes={Fallback(scopes)}", $"權限範圍={Fallback(scopes)}"),
            };
            if (!string.IsNullOrWhiteSpace(error))
                details.Add(pick($"error={error}", $"錯誤={error}"));

            return new GhAccountRow
            {
                Login = account.Login,
                Host = account.Host,
                Active = account.Active,
                Glyph = account.Active ? ((char)0xE73E).ToString() : ((char)0xE77B).ToString(),
                Details = string.Join("   ·   ", details),
            };
        }

        private static string Fallback(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    }
}
