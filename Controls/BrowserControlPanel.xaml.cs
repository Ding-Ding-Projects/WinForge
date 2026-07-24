using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// Parameterized Browser Control workbench embedded above the legacy quick-action catalog.
/// Every launch uses <see cref="BrowserControlService"/> and a validated argument vector.
/// </summary>
public sealed partial class BrowserControlPanel : UserControl
{
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly ComboBoxItem _chromeItem = new() { Tag = ChromiumBrowser.Chrome };
    private readonly ComboBoxItem _edgeItem = new() { Tag = ChromiumBrowser.Edge };
    private readonly ComboBoxItem _enableItem = new() { Tag = BrowserFeatureMode.Enable };
    private readonly ComboBoxItem _disableItem = new() { Tag = BrowserFeatureMode.Disable };
    private bool _loading = true;
    private bool _languageSubscribed;
    private bool _busy;

    public BrowserControlPanel()
    {
        InitializeComponent();
        BrowserBox.Items.Add(_chromeItem);
        BrowserBox.Items.Add(_edgeItem);
        FeatureModeBox.Items.Add(_enableItem);
        FeatureModeBox.Items.Add(_disableItem);

        foreach (var control in new Control[]
        {
            BrowserBox, ProfileBox, UrlBox, PwaBox, ProxyBox, BypassBox,
            FeatureBox, FeatureModeBox, DebugPortBox,
        }) control.MinHeight = 48;

        BrowserBox.SelectedIndex = SettingsStore.Get("browserWorkbench.browser", "edge") == "chrome" ? 0 : 1;
        FeatureModeBox.SelectedIndex = SettingsStore.Get("browserWorkbench.featureMode", "enable") == "disable" ? 1 : 0;
        // URLs and proxy endpoints can carry sensitive tokens or credentials, so they are session-only.
        UrlBox.Text = "https://example.com/";
        ProxyBox.Text = "socks5://127.0.0.1:1080";
        BypassBox.Text = SettingsStore.Get("browserWorkbench.bypass", "*.local;127.0.0.1");
        FeatureBox.Text = SettingsStore.Get("browserWorkbench.features", "");
        if (!double.TryParse(SettingsStore.Get("browserWorkbench.debugPort", "9222"), out var port)
            || port is < BrowserControlCore.MinimumRemoteDebugPort or > BrowserControlCore.MaximumRemoteDebugPort)
            port = 9222;
        // Managed initialization avoids the current self-contained NumberBox XAML literal failure.
        DebugPortBox.Value = port;

        _statusTimer.Tick += StatusTimer_Tick;
        _loading = false;
        RenderText();
    }

    private ChromiumBrowser SelectedBrowser
        => (BrowserBox.SelectedItem as ComboBoxItem)?.Tag is ChromiumBrowser browser
            ? browser
            : ChromiumBrowser.Edge;

    private BrowserFeatureMode SelectedFeatureMode
        => (FeatureModeBox.SelectedItem as ComboBoxItem)?.Tag is BrowserFeatureMode mode
            ? mode
            : BrowserFeatureMode.Enable;

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private async void Panel_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_languageSubscribed)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            _languageSubscribed = true;
        }
        RenderText();
        ReloadProfiles();
        await ReloadPwasAsync();
        _ = Task.Run(() => BrowserControlService.CleanupStaleSessions());
    }

    private void Panel_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_languageSubscribed)
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _languageSubscribed = false;
        }
        _statusTimer.Stop();
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RenderText();

    private void RenderText()
    {
        WorkbenchTitle.Text = P("Browser launch workbench", "瀏覽器啟動工作台");
        WorkbenchDescription.Text = P(
            "Choose a browser, real on-disk profile, and URL. Launches use validated argument lists; isolated sessions are deleted after their owned browser exits.",
            "揀瀏覽器、磁碟上真實設定檔同網址。啟動只用驗證過嘅參數清單；隔離 session 會喺自家瀏覽器退出後刪除。");

        _chromeItem.Content = P("Google Chrome", "Google Chrome");
        _edgeItem.Content = P("Microsoft Edge", "Microsoft Edge");
        _enableItem.Content = P("Enable", "啟用");
        _disableItem.Content = P("Disable", "停用");

        BrowserBox.Header = P("Browser", "瀏覽器");
        ProfileBox.Header = P("Profile (from Local State)", "設定檔（來自 Local State）");
        UrlBox.Header = P("Website URL", "網站網址");
        UrlBox.PlaceholderText = "https://example.com/";

        LaunchExpander.Header = P("Launch modes", "啟動模式");
        SetButton(AppModeBtn, P("Open as desktop app", "用獨立 App 視窗開"));
        SetButton(KioskBtn, P("Open full-screen kiosk", "開全螢幕 Kiosk"));
        SetButton(ProfileBtn, P("Launch selected profile", "開揀選設定檔"));
        SetButton(ThrowawayBtn, P("Launch throwaway session", "開用完即棄 session"));
        SetButton(FlagsBtn, P("Open flags page", "開 flags 頁"));
        SetButton(PolicyBtn, P("Open policy page", "開 policy 頁"));

        PwaExpander.Header = P("Installed web apps (PWAs)", "已安裝網頁 App（PWA）");
        PwaBox.Header = P("PWA shortcut", "PWA 捷徑");
        SetButton(RefreshPwaBtn, P("Refresh installed PWAs", "重新掃描已安裝 PWA"));
        SetButton(LaunchPwaBtn, P("Launch selected PWA", "開揀選 PWA"));

        NetworkExpander.Header = P("Proxy, feature flags & remote debugging", "Proxy、功能旗標同遠端除錯");
        ProxyBox.Header = P("Proxy server", "Proxy 伺服器");
        ProxyBox.PlaceholderText = "socks5://127.0.0.1:1080";
        BypassBox.Header = P("Bypass list (semicolon-separated)", "略過清單（分號分隔）");
        BypassBox.PlaceholderText = "*.local;127.0.0.1";
        SetButton(ProxyBtn, P("Launch isolated proxy session", "開隔離 Proxy session"));
        FeatureBox.Header = P("Chromium feature names (comma-separated)", "Chromium 功能名稱（逗號分隔）");
        FeatureBox.PlaceholderText = "FeatureName,AnotherFeature";
        FeatureModeBox.Header = P("Mode", "模式");
        SetButton(FeatureBtn, P("Launch with feature switch", "用功能開關啟動"));
        DebugPortBox.Header = P("Loopback debugging port", "Loopback 除錯連接埠");
        SetButton(DebugBtn, P("Launch isolated remote-debug session", "開隔離遠端除錯 session"));
        DebugSafetyText.Text = P(
            "Remote debugging binds to 127.0.0.1 only and always uses a new isolated user-data directory.",
            "遠端除錯只綁定 127.0.0.1，而且一定用全新隔離 user-data 資料夾。");

        MaintenanceExpander.Header = P("Profile cache & browser packages", "設定檔快取同瀏覽器套件");
        CacheSafetyText.Text = P(
            "Cache cleanup deletes Cache and Code Cache only after verifying that the selected browser is fully closed.",
            "快取清理會先確認揀選瀏覽器完全關閉，先至刪除 Cache 同 Code Cache。");
        SetButton(ClearCacheBtn, P("Clear selected profile cache", "清除揀選設定檔快取"));
        SetButton(InstallBtn, P("Install with winget", "用 winget 安裝"));
        SetButton(UpgradeBtn, P("Update with winget", "用 winget 更新"));

        AutomationProperties.SetName(BrowserBox, P("Browser", "瀏覽器"));
        AutomationProperties.SetName(ProfileBox, P("Browser profile", "瀏覽器設定檔"));
        AutomationProperties.SetName(UrlBox, P("Website URL", "網站網址"));
        AutomationProperties.SetName(PwaBox, P("Installed PWA", "已安裝 PWA"));
        AutomationProperties.SetName(ProxyBox, P("Proxy server", "Proxy 伺服器"));
        AutomationProperties.SetName(BypassBox, P("Proxy bypass list", "Proxy 略過清單"));
        AutomationProperties.SetName(FeatureBox, P("Chromium feature names", "Chromium 功能名稱"));
        AutomationProperties.SetName(FeatureModeBox, P("Feature mode", "功能模式"));
        AutomationProperties.SetName(DebugPortBox, P("Remote debugging port", "遠端除錯連接埠"));
    }

    private static void SetButton(Button button, string text)
    {
        button.MinHeight = 48;
        button.Padding = new Thickness(12, 8, 12, 8);
        button.Content = new TextBlock
        {
            Text = text,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetName(button, text);
    }

    private async void BrowserBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        SettingsStore.Set("browserWorkbench.browser", SelectedBrowser == ChromiumBrowser.Chrome ? "chrome" : "edge");
        ReloadProfiles();
        await ReloadPwasAsync();
    }

    private void ProfileBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ProfileBox.SelectedItem as BrowserProfile;
        ProfileBtn.IsEnabled = ClearCacheBtn.IsEnabled = selected is not null && !_busy;
        if (!_loading && selected is not null)
            SettingsStore.Set($"browserWorkbench.profile.{SelectedBrowser}", selected.DirectoryName);
    }

    private void PwaBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => LaunchPwaBtn.IsEnabled = PwaBox.SelectedItem is BrowserPwa && !_busy;

    private void ReloadProfiles()
    {
        var preferred = SettingsStore.Get($"browserWorkbench.profile.{SelectedBrowser}", "Default");
        var profiles = BrowserControlService.Profiles(SelectedBrowser);
        ProfileBox.ItemsSource = profiles;
        ProfileBox.SelectedItem = profiles.FirstOrDefault(p => p.DirectoryName.Equals(preferred, StringComparison.OrdinalIgnoreCase))
            ?? profiles.FirstOrDefault();
        ProfileBox_SelectionChanged(ProfileBox, null!);
    }

    private async Task ReloadPwasAsync()
    {
        SetBusy(true);
        try
        {
            var browser = SelectedBrowser;
            var pwas = await Task.Run(() => BrowserControlService.Pwas(browser));
            PwaBox.ItemsSource = pwas;
            PwaBox.SelectedIndex = pwas.Count > 0 ? 0 : -1;
            if (pwas.Count == 0)
                ShowStatus(TweakResult.Ok("No installed PWA shortcuts were found for this browser.", "搵唔到呢個瀏覽器嘅已安裝 PWA 捷徑。"));
        }
        catch (Exception ex)
        {
            ShowException(ex);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void PersistedText_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        if (ReferenceEquals(sender, BypassBox)) SettingsStore.Set("browserWorkbench.bypass", BypassBox.Text ?? "");
        else if (ReferenceEquals(sender, FeatureBox)) SettingsStore.Set("browserWorkbench.features", FeatureBox.Text ?? "");
    }

    private void DebugPortBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue)) return;
        SettingsStore.Set("browserWorkbench.debugPort", Math.Round(args.NewValue).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private void AppMode_Click(object sender, RoutedEventArgs e)
        => Run(() => BrowserControlService.LaunchAppMode(SelectedBrowser, UrlBox.Text));

    private void Kiosk_Click(object sender, RoutedEventArgs e)
        => Run(() => BrowserControlService.LaunchKiosk(SelectedBrowser, UrlBox.Text));

    private void Profile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not BrowserProfile profile) return;
        Run(() => BrowserControlService.LaunchProfile(profile));
    }

    private void Throwaway_Click(object sender, RoutedEventArgs e)
        => Run(() => BrowserControlService.LaunchThrowaway(SelectedBrowser, UrlBox.Text));

    private void Flags_Click(object sender, RoutedEventArgs e)
        => Run(() => BrowserControlService.OpenInternalPage(SelectedBrowser, policyPage: false));

    private void Policy_Click(object sender, RoutedEventArgs e)
        => Run(() => BrowserControlService.OpenInternalPage(SelectedBrowser, policyPage: true));

    private async void RefreshPwa_Click(object sender, RoutedEventArgs e) => await ReloadPwasAsync();

    private void LaunchPwa_Click(object sender, RoutedEventArgs e)
    {
        if (PwaBox.SelectedItem is not BrowserPwa pwa) return;
        Run(() => BrowserControlService.LaunchPwa(pwa));
    }

    private void Proxy_Click(object sender, RoutedEventArgs e)
        => Run(() => BrowserControlService.LaunchProxy(SelectedBrowser, ProxyBox.Text, BypassBox.Text, UrlBox.Text));

    private void Feature_Click(object sender, RoutedEventArgs e)
    {
        SettingsStore.Set("browserWorkbench.featureMode", SelectedFeatureMode == BrowserFeatureMode.Enable ? "enable" : "disable");
        Run(() => BrowserControlService.LaunchFeature(SelectedBrowser, FeatureBox.Text, SelectedFeatureMode, UrlBox.Text));
    }

    private void Debug_Click(object sender, RoutedEventArgs e)
    {
        if (double.IsNaN(DebugPortBox.Value))
        {
            ShowStatus(TweakResult.Fail("Enter a remote debugging port.", "請輸入遠端除錯連接埠。"));
            return;
        }
        Run(() => BrowserControlService.LaunchRemoteDebug(SelectedBrowser, checked((int)Math.Round(DebugPortBox.Value)), UrlBox.Text));
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileBox.SelectedItem is not BrowserProfile profile) return;
        var confirmed = await ConfirmAsync(
            P("Clear profile cache?", "清除設定檔快取？"),
            P(
                $"Close every {BrowserControlCore.BrowserName(profile.Browser)} process first. WinForge will delete only Cache and Code Cache inside {profile.DisplayLabel}.",
                $"請先完全關閉 {BrowserControlCore.BrowserName(profile.Browser)}。WinForge 只會刪除 {profile.DisplayLabel} 入面嘅 Cache 同 Code Cache。"),
            P("Clear cache", "清除快取"));
        if (!confirmed) return;
        Run(() => BrowserControlService.ClearProfileCache(profile));
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
        => await RunWingetAsync(BrowserPackageAction.Install);

    private async void Upgrade_Click(object sender, RoutedEventArgs e)
        => await RunWingetAsync(BrowserPackageAction.Upgrade);

    private async Task RunWingetAsync(BrowserPackageAction action)
    {
        var install = action == BrowserPackageAction.Install;
        var title = install
            ? P("Install browser with winget?", "用 winget 安裝瀏覽器？")
            : P("Update browser with winget?", "用 winget 更新瀏覽器？");
        var message = install
            ? P(
                $"winget will install {BrowserControlCore.BrowserName(SelectedBrowser)} using the verified package ID {BrowserControlCore.PackageId(SelectedBrowser)}.",
                $"winget 會用已驗證套件 ID {BrowserControlCore.PackageId(SelectedBrowser)} 安裝 {BrowserControlCore.BrowserName(SelectedBrowser)}。")
            : P(
                $"winget will update {BrowserControlCore.BrowserName(SelectedBrowser)} using the verified package ID {BrowserControlCore.PackageId(SelectedBrowser)}.",
                $"winget 會用已驗證套件 ID {BrowserControlCore.PackageId(SelectedBrowser)} 更新 {BrowserControlCore.BrowserName(SelectedBrowser)}。");
        if (!await ConfirmAsync(
                title,
                message,
                P("Continue", "繼續")))
            return;

        SetBusy(true);
        try { ShowStatus(await BrowserControlService.RunWingetAsync(SelectedBrowser, action)); }
        catch (Exception ex) { ShowException(ex); }
        finally { SetBusy(false); }
    }

    private void Run(Func<TweakResult> action)
    {
        if (_busy) return;
        SetBusy(true);
        try { ShowStatus(action()); }
        catch (Exception ex) { ShowException(ex); }
        finally { SetBusy(false); }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string primary)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary,
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dialog.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    private void SetBusy(bool value)
    {
        _busy = value;
        BusyRing.IsActive = value;
        BusyRing.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in new[]
        {
            AppModeBtn, KioskBtn, ThrowawayBtn, FlagsBtn, PolicyBtn, RefreshPwaBtn,
            ProxyBtn, FeatureBtn, DebugBtn, InstallBtn, UpgradeBtn,
        }) button.IsEnabled = !value;
        ProfileBtn.IsEnabled = !value && ProfileBox.SelectedItem is BrowserProfile;
        ClearCacheBtn.IsEnabled = !value && ProfileBox.SelectedItem is BrowserProfile;
        LaunchPwaBtn.IsEnabled = !value && PwaBox.SelectedItem is BrowserPwa;
    }

    private void ShowException(Exception ex)
        => ShowStatus(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"));

    private void ShowStatus(TweakResult result)
    {
        _statusTimer.Stop();
        StatusBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        StatusBar.Title = result.Success ? P("Done", "完成") : P("Failed", "失敗");
        var message = result.Message?.Get(Loc.I.Language) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            var output = result.Output.Length > 600 ? result.Output[..600] + "…" : result.Output;
            message = string.IsNullOrWhiteSpace(message) ? output : message + "\n" + output;
        }
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
        if (result.Success) _statusTimer.Start();
    }

    private void StatusTimer_Tick(object? sender, object e)
    {
        _statusTimer.Stop();
        StatusBar.IsOpen = false;
    }
}
