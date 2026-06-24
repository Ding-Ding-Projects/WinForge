using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Windows.Foundation;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 內置登入對話框 · A reusable ContentDialog hosting a WebView2 that drives an OAuth / web sign-in
/// inside WinForge. Watches navigation for a redirect-URI prefix and/or named cookies, then returns
/// a populated <see cref="LoginResult"/>. All strings are bilingual. Tokens/cookies stay in memory
/// and are never logged.
/// </summary>
public sealed partial class LoginDialog : ContentDialog
{
    private readonly LoginRequest _request;
    private readonly TaskCompletionSource<LoginResult> _tcs = new();
    private bool _completed;
    private bool _coreReady;

    public LoginDialog(LoginRequest request)
    {
        _request = request ?? throw new ArgumentNullException(nameof(request));
        InitializeComponent();

        Title = Loc.I.Pick(_request.TitleEn, _request.TitleZh);
        CloseButtonText = Loc.I.Pick("Cancel", "取消");
        AddressBox.Text = _request.StartUrl;

        Loaded += OnLoaded;
        CloseButtonClick += OnCloseButtonClick;
    }

    /// <summary>顯示對話框並等結果 · Show the dialog and await the captured result.</summary>
    public async Task<LoginResult> RunAsync()
    {
        _ = await ShowAsync();
        // If the user dismissed the dialog (Esc / close) without a capture, return cancelled.
        if (!_completed) Finish(LoginResult.Cancelled());
        return await _tcs.Task;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Defensive: the WebView2 Runtime ships with Win11 but may be missing on stripped images.
        try
        {
            var ver = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrWhiteSpace(ver))
            {
                ShowError(Loc.I.Pick(
                    "Microsoft Edge WebView2 Runtime is not installed. Install the Evergreen Runtime from https://developer.microsoft.com/microsoft-edge/webview2/ and try again.",
                    "未安裝 Microsoft Edge WebView2 執行階段。請由 https://developer.microsoft.com/microsoft-edge/webview2/ 安裝 Evergreen Runtime 再試。"));
                return;
            }
        }
        catch (Exception ex)
        {
            ShowError(Loc.I.Pick(
                "WebView2 Runtime check failed: " + ex.Message,
                "WebView2 執行階段檢查失敗：" + ex.Message));
            return;
        }

        try
        {
            var folder = WebLoginService.ProfileFolder(_request.Profile);
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: string.Empty,
                userDataFolder: folder,
                options: new CoreWebView2EnvironmentOptions());
            await Web.EnsureCoreWebView2Async(env);
            _coreReady = true;

            var core = Web.CoreWebView2;
            if (!string.IsNullOrWhiteSpace(_request.UserAgent))
                core.Settings.UserAgent = _request.UserAgent;

            core.NavigationStarting += OnNavigationStarting;
            core.SourceChanged += OnSourceChanged;
            core.NavigationCompleted += OnNavigationCompleted;
            core.HistoryChanged += (_, _) => BackButton.IsEnabled = core.CanGoBack;

            Web.Source = new Uri(_request.StartUrl);
        }
        catch (Exception ex)
        {
            ShowError(Loc.I.Pick(
                "Could not start the embedded browser: " + ex.Message,
                "無法啟動內置瀏覽器：" + ex.Message));
        }
    }

    // ---- navigation handling -------------------------------------------------

    private async void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        Spinner.IsActive = true;
        await TryCompleteByUrl(args.Uri);
    }

    private async void OnSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        var uri = sender.Source;
        AddressBox.Text = uri;
        await TryCompleteByUrl(uri);
    }

    private async void OnNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        Spinner.IsActive = false;
        if (!args.IsSuccess && args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled
            && args.WebErrorStatus != CoreWebView2WebErrorStatus.ConnectionAborted)
        {
            ShowError(Loc.I.Pick(
                $"Navigation failed ({args.WebErrorStatus}).",
                $"瀏覽失敗（{args.WebErrorStatus}）。"));
        }

        // Cookie-only completion: re-check after each page settles.
        if (!_completed && _request.CompleteOnCookies && _request.CookieNames.Count > 0)
            await TryCompleteByCookies(sender.Source);
    }

    private async Task TryCompleteByUrl(string uri)
    {
        if (_completed || string.IsNullOrEmpty(uri)) return;
        var prefix = _request.RedirectUriPrefix;
        if (string.IsNullOrWhiteSpace(prefix)) return;
        if (!uri.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return;

        var query = ParseParams(uri);
        var cookies = await ReadCookies(_request.CookieDomain ?? uri);
        Finish(new LoginResult
        {
            Success = true,
            RedirectUri = uri,
            RawUrl = uri,
            QueryParams = query,
            Cookies = cookies,
        });
    }

    private async Task TryCompleteByCookies(string rawUrl)
    {
        var domain = _request.CookieDomain ?? rawUrl;
        var cookies = await ReadCookies(domain);
        foreach (var name in _request.CookieNames)
            if (!cookies.ContainsKey(name)) return; // not all present yet

        Finish(new LoginResult
        {
            Success = true,
            RedirectUri = null,
            RawUrl = rawUrl,
            QueryParams = ParseParams(rawUrl),
            Cookies = cookies,
        });
    }

    private async Task<Dictionary<string, string>> ReadCookies(string urlOrDomain)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!_coreReady || _request.CookieNames.Count == 0) return result;

        string uri = urlOrDomain;
        if (!uri.Contains("://")) uri = "https://" + uri.TrimStart('.', '/');
        try
        {
            var list = await Web.CoreWebView2.CookieManager.GetCookiesAsync(uri);
            foreach (var c in list)
                foreach (var want in _request.CookieNames)
                    if (string.Equals(c.Name, want, StringComparison.Ordinal))
                        result[c.Name] = c.Value;
        }
        catch { /* cookie read is best-effort */ }
        return result;
    }

    /// <summary>解析 query + fragment 入面嘅參數 · Parse params from both query and fragment.</summary>
    private static Dictionary<string, string> ParseParams(string url)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return map;

        void Decode(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            s = s.TrimStart('?', '#');
            if (s.Length == 0) return;
            try
            {
                var dec = new WwwFormUrlDecoder(s);
                foreach (var entry in dec) map[entry.Name] = entry.Value;
            }
            catch { /* malformed fragment — ignore */ }
        }

        Decode(uri.Query);
        Decode(uri.Fragment);
        return map;
    }

    // ---- UI events -----------------------------------------------------------

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_coreReady && Web.CoreWebView2.CanGoBack) Web.CoreWebView2.GoBack();
    }

    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_coreReady) Web.CoreWebView2.Reload();
    }

    private void Address_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter || !_coreReady) return;
        var text = (AddressBox.Text ?? "").Trim();
        if (text.Length == 0) return;
        if (!text.Contains("://")) text = "https://" + text;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            Web.Source = uri;
            e.Handled = true;
        }
    }

    private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        Finish(LoginResult.Cancelled());
    }

    private void ShowError(string message)
    {
        Spinner.IsActive = false;
        ErrorBar.Title = Loc.I.Pick("Error", "錯誤");
        ErrorBar.Message = message;
        ErrorBar.IsOpen = true;
    }

    private void Finish(LoginResult result)
    {
        if (_completed) return;
        _completed = true;
        _tcs.TrySetResult(result);
        // Tear down the WebView before the dialog (and its UserDataFolder lock) closes.
        try { Web.Close(); } catch { /* ignore */ }
        try { Hide(); } catch { /* already hidden */ }
    }
}
