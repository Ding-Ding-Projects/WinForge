using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 內置登入／瀏覽器 · In-App Login / browser. A standalone shell over <see cref="WebLoginService"/>:
/// an embedded WebView2 you can navigate, provider presets that open the reusable login dialog and
/// capture the OAuth redirect or named cookies, plus per-profile cookie-jar management (clear/sign-out).
/// Fully bilingual (English + 粵語).
/// </summary>
public sealed partial class WebLoginModule : Page
{
    private bool _coreReady;

    /// <summary>登入提供者預設 · A provider preset that the capture dialog can drive.</summary>
    private sealed record Preset(
        string En, string Zh, string StartUrl,
        string? RedirectPrefix, string[] CookieNames, string? CookieDomain, bool CompleteOnCookies);

    private static readonly List<Preset> Presets = new()
    {
        new("Manual browser (no capture)", "手動瀏覽（唔捕捉）",
            "https://www.bing.com", null, Array.Empty<string>(), null, false),
        new("GitHub — sign in (cookie)", "GitHub — 登入（cookie）",
            "https://github.com/login", null, new[] { "user_session", "logged_in" }, "https://github.com", true),
        new("Cloudflare — dashboard (cookie)", "Cloudflare — 控制台（cookie）",
            "https://dash.cloudflare.com/login", null, new[] { "CF_Authorization", "__cf_logged_in" }, "https://dash.cloudflare.com", false),
        new("OpenAI — platform (cookie)", "OpenAI — 平台（cookie）",
            "https://platform.openai.com/login", null, new[] { "__Secure-next-auth.session-token" }, "https://platform.openai.com", false),
        new("Anthropic — console (cookie)", "Anthropic — 控制台（cookie）",
            "https://console.anthropic.com/login", null, new[] { "sessionKey", "__session" }, "https://console.anthropic.com", false),
        new("Bitwarden — web vault (cookie)", "Bitwarden — 網頁保險箱（cookie）",
            "https://vault.bitwarden.com", null, new[] { "user" }, "https://vault.bitwarden.com", false),
        new("OAuth redirect demo (localhost)", "OAuth 重新導向示範（localhost）",
            "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
            "http://localhost", Array.Empty<string>(), null, false),
    };

    public WebLoginModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            try { Web.Close(); } catch { }
        };
        Loaded += OnLoaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        BuildProviders();
        RefreshProfiles();
        await EnsureWebAsync();
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildProviders();
    }

    private void Render()
    {
        Header.Title = "In-App Login · 內置登入";
        HeaderBlurb.Text = P(
            "Sign in to web services (GitHub, Cloudflare, OpenAI, Anthropic, Bitwarden…) inside WinForge instead of an external browser. Pick a provider and Capture to drive an OAuth redirect or grab session cookies; the same plumbing is reused by the Git, Cloudflare and AI modules.",
            "喺 WinForge 內置登入各種網頁服務（GitHub、Cloudflare、OpenAI、Anthropic、Bitwarden…），唔使彈出外置瀏覽器。揀一個提供者再撳「捕捉」去跑 OAuth 重新導向或者攞 session cookie；Git、Cloudflare 同 AI 模組都共用同一套底層。");
        AddressBox.PlaceholderText = P("Enter a URL and press Enter…", "輸入網址再撳 Enter…");
        GoBtn.Content = P("Go", "前往");
        ProviderLabel.Text = P("Provider", "提供者");
        CaptureBtn.Content = P("Capture login", "捕捉登入");
        ProfileLabel.Text = P("Profile", "設定檔");
        ClearBtn.Content = P("Sign out / clear", "登出／清除");
        ToolTipService.SetToolTip(BackBtn, P("Back", "返回"));
        ToolTipService.SetToolTip(FwdBtn, P("Forward", "前進"));
        ToolTipService.SetToolTip(ReloadBtn, P("Reload", "重新載入"));
    }

    private void BuildProviders()
    {
        int sel = ProviderCombo.SelectedIndex;
        ProviderCombo.Items.Clear();
        foreach (var p in Presets)
            ProviderCombo.Items.Add(P(p.En, p.Zh));
        ProviderCombo.SelectedIndex = sel >= 0 ? sel : 0;
    }

    private void RefreshProfiles()
    {
        var current = ProfileCombo.Text;
        ProfileCombo.Items.Clear();
        ProfileCombo.Items.Add("default");
        foreach (var name in WebLoginService.ListProfiles())
            if (!string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
                ProfileCombo.Items.Add(name);
        ProfileCombo.Text = string.IsNullOrWhiteSpace(current) ? "default" : current;
    }

    private string CurrentProfile =>
        string.IsNullOrWhiteSpace(ProfileCombo.Text) ? "default" : ProfileCombo.Text.Trim();

    // ---- embedded browser ----------------------------------------------------

    private async Task EnsureWebAsync()
    {
        try
        {
            var ver = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(ver))
            {
                ShowRuntimeMissing();
                return;
            }
        }
        catch
        {
            ShowRuntimeMissing();
            return;
        }

        try
        {
            Spinner.IsActive = true;
            var folder = WebLoginService.ProfileFolder(CurrentProfile);
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                string.Empty, folder, new CoreWebView2EnvironmentOptions());
            await Web.EnsureCoreWebView2Async(env);
            _coreReady = true;

            var core = Web.CoreWebView2;
            core.SourceChanged += (s, _) => AddressBox.Text = s.Source;
            core.NavigationStarting += (_, _) => Spinner.IsActive = true;
            core.NavigationCompleted += (_, _) => Spinner.IsActive = false;
            core.HistoryChanged += (s, _) =>
            {
                BackBtn.IsEnabled = s.CanGoBack;
                FwdBtn.IsEnabled = s.CanGoForward;
            };

            EngineBar.IsOpen = false;
            Web.Source = new Uri("https://www.bing.com");
        }
        catch (Exception ex)
        {
            Spinner.IsActive = false;
            EngineBar.Severity = InfoBarSeverity.Error;
            EngineBar.Title = P("Could not start the embedded browser", "無法啟動內置瀏覽器");
            EngineBar.Message = ex.Message;
            EngineBar.IsOpen = true;
        }
    }

    private void ShowRuntimeMissing()
    {
        Spinner.IsActive = false;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("WebView2 Runtime not found", "搵唔到 WebView2 執行階段");
        EngineBar.Message = P(
            "The Microsoft Edge WebView2 Runtime is required for in-app login. It ships with Windows 11; on older images install the Evergreen Runtime.",
            "內置登入需要 Microsoft Edge WebView2 執行階段。Windows 11 已內附；舊版系統請安裝 Evergreen Runtime。");

        // Rich one-click auto-install of the WebView2 Runtime (winget: Microsoft.EdgeWebView2Runtime).
        // On success re-detect + start the embedded browser without an app restart.
        try
        {
            EngineBar.Content = EngineBars.AutoInstallProgress(
                "Microsoft.EdgeWebView2Runtime", "Install WebView2 Runtime", "安裝 WebView2 執行階段",
                recheck: async () => { await EnsureWebAsync(); });
        }
        catch { /* never throw from prerequisite UI */ }

        // Keep the manual link as a fallback action.
        var btn = new Button
        {
            Content = P("Copy download URL", "複製下載網址"),
        };
        btn.Click += (_, _) => CopyText("https://developer.microsoft.com/microsoft-edge/webview2/");
        EngineBar.ActionButton = btn;
        EngineBar.IsOpen = true;
    }

    private static void CopyText(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_coreReady && Web.CoreWebView2.CanGoBack) Web.CoreWebView2.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_coreReady && Web.CoreWebView2.CanGoForward) Web.CoreWebView2.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_coreReady) Web.CoreWebView2.Reload();
    }

    private void Address_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) { Navigate(); e.Handled = true; }
    }

    private void Go_Click(object sender, RoutedEventArgs e) => Navigate();

    private void Navigate()
    {
        if (!_coreReady) return;
        var text = (AddressBox.Text ?? "").Trim();
        if (text.Length == 0) return;
        if (!text.Contains("://")) text = "https://" + text;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
            Web.Source = uri;
    }

    private void Provider_Changed(object sender, SelectionChangedEventArgs e)
    {
        int i = ProviderCombo.SelectedIndex;
        if (i < 0 || i >= Presets.Count || !_coreReady) return;
        var preset = Presets[i];
        if (Uri.TryCreate(preset.StartUrl, UriKind.Absolute, out var uri))
            Web.Source = uri;
    }

    // ---- capture & profile ---------------------------------------------------

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        int i = ProviderCombo.SelectedIndex;
        if (i < 0 || i >= Presets.Count) return;
        var preset = Presets[i];

        var startUrl = preset.StartUrl;
        // For the manual preset use whatever's in the address bar.
        if (preset.RedirectPrefix is null && preset.CookieNames.Length == 0)
        {
            var typed = (AddressBox.Text ?? "").Trim();
            if (typed.Length > 0)
            {
                if (!typed.Contains("://")) typed = "https://" + typed;
                startUrl = typed;
            }
        }

        var request = new LoginRequest
        {
            StartUrl = startUrl,
            RedirectUriPrefix = preset.RedirectPrefix,
            CookieNames = preset.CookieNames,
            CookieDomain = preset.CookieDomain,
            CompleteOnCookies = preset.CompleteOnCookies,
            Profile = CurrentProfile,
            TitleEn = preset.En,
            TitleZh = preset.Zh,
        };

        CaptureBtn.IsEnabled = false;
        try
        {
            var result = await WebLoginService.CaptureAsync(request, XamlRoot);
            ShowResult(result);
            RefreshProfiles();
        }
        catch (Exception ex)
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Capture failed", "捕捉失敗");
            ResultBar.Message = ex.Message;
            ResultBar.IsOpen = true;
        }
        finally { CaptureBtn.IsEnabled = true; }
    }

    private void ShowResult(LoginResult r)
    {
        if (!r.Success)
        {
            ResultBar.Severity = InfoBarSeverity.Informational;
            ResultBar.Title = P("No login captured", "未捕捉到登入");
            ResultBar.Message = r.Error ?? P("Cancelled.", "已取消。");
            ResultBar.IsOpen = true;
            return;
        }

        // Never log the actual secret values — only report counts and key names.
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(r.RedirectUri))
            sb.Append(P("Redirect matched. ", "已命中重新導向。"));
        if (r.QueryParams.Count > 0)
            sb.Append(P($"Params: {string.Join(", ", r.QueryParams.Keys)}. ",
                       $"參數：{string.Join("、", r.QueryParams.Keys)}。"));
        if (r.Cookies.Count > 0)
            sb.Append(P($"Cookies captured: {string.Join(", ", r.Cookies.Keys)}.",
                       $"已捕捉 cookie：{string.Join("、", r.Cookies.Keys)}。"));
        if (sb.Length == 0)
            sb.Append(P("Login completed.", "登入完成。"));

        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Login captured", "已捕捉登入");
        ResultBar.Message = sb.ToString();
        ResultBar.IsOpen = true;
    }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        var profile = CurrentProfile;
        // The page's own WebView holds a lock on the default folder; rebuild after clearing.
        bool clearingActive = string.Equals(profile, "default", StringComparison.OrdinalIgnoreCase);
        if (clearingActive)
        {
            try { Web.Close(); } catch { }
            _coreReady = false;
        }

        bool ok = WebLoginService.ClearProfile(profile);
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Title = ok ? P("Profile cleared", "已清除設定檔")
                             : P("Could not clear profile", "無法清除設定檔");
        ResultBar.Message = ok
            ? P($"Cookies and session for '{profile}' were wiped.", $"已清除「{profile}」嘅 cookie 同 session。")
            : P("The profile folder is in use. Close any open login and retry.", "設定檔資料夾正在使用。請關閉登入再試。");
        ResultBar.IsOpen = true;

        RefreshProfiles();
        if (clearingActive) await EnsureWebAsync();
    }
}
