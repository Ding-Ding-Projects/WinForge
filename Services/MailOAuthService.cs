using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace WinForge.Services;

/// <summary>OAuth2 結果 · Result of an OAuth2 sign-in.</summary>
public sealed record MailOAuthResult(bool Success, string AccessToken = "", string RefreshToken = "",
    DateTimeOffset Expiry = default, string Email = "", string Error = "");

/// <summary>
/// Gmail / Outlook 嘅 OAuth2（PKCE，loopback 重新導向）· OAuth2 (PKCE, loopback redirect) for
/// Gmail and Outlook. 喺內嵌 WebView2 入面登入，攞授權碼，再換 access/refresh token；refresh token
/// 由 <see cref="MailAccountStore"/> DPAPI 加密保存。Tokens 永遠唔會 log。
/// Signs in inside an embedded WebView2, captures the auth code on a loopback redirect, then exchanges
/// it for access/refresh tokens. The refresh token is DPAPI-encrypted by the account store. Never logged.
///
/// 用公開（native）client，PKCE，唔需要 client secret。These are the public "installed app" client
/// IDs Thunderbird itself ships with, used here in the same loopback-PKCE manner.
/// </summary>
public static class MailOAuthService
{
    // Public installed-app client IDs (PKCE, no secret). These are the same public IDs Thunderbird uses.
    private const string GoogleClientId = "406964657835-aq8ln0j95rb6r6n.apps.googleusercontent.com";
    private const string GoogleAuth = "https://accounts.google.com/o/oauth2/auth";
    private const string GoogleToken = "https://www.googleapis.com/oauth2/v3/token";
    private const string GoogleScope = "https://mail.google.com/ email";

    // Microsoft "common" tenant, public client (Thunderbird's registered app id).
    private const string MsClientId = "9e5f94bc-e8a4-4e73-b8be-63364c29d753";
    private const string MsAuth = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    private const string MsToken = "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    private const string MsScope = "https://outlook.office365.com/IMAP.AccessAsUser.All " +
                                   "https://outlook.office365.com/SMTP.Send offline_access email openid";

    /// <summary>互動式登入（彈出 WebView2）· Interactive sign-in (pops a WebView2 dialog).</summary>
    public static async Task<MailOAuthResult> SignInAsync(string provider, XamlRoot xamlRoot)
    {
        bool google = provider == "google";
        var clientId = google ? GoogleClientId : MsClientId;
        var authEp = google ? GoogleAuth : MsAuth;
        var scope = google ? GoogleScope : MsScope;

        // Loopback redirect on a random free port (the canonical desktop OAuth pattern).
        int port = FreePort();
        var redirect = $"http://127.0.0.1:{port}/";

        var (verifier, challenge) = Pkce();
        var state = RandUrl(16);
        var url = $"{authEp}?client_id={Uri.EscapeDataString(clientId)}" +
                  $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirect)}" +
                  $"&scope={Uri.EscapeDataString(scope)}" +
                  $"&code_challenge={challenge}&code_challenge_method=S256" +
                  $"&state={state}&prompt=consent&access_type=offline";

        var code = await CaptureCodeAsync(url, redirect, state, xamlRoot);
        if (code is null) return new MailOAuthResult(false, Error: "Cancelled");

        return await ExchangeAsync(google, clientId, redirect, verifier, code);
    }

    /// <summary>用 refresh token 攞新 access token · Refresh an access token silently.</summary>
    public static async Task<MailOAuthResult> RefreshAsync(string provider, string refreshToken)
    {
        bool google = provider == "google";
        var clientId = google ? GoogleClientId : MsClientId;
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };
        if (!google) form["scope"] = MsScope;
        return await PostToken(google ? GoogleToken : MsToken, form, refreshToken);
    }

    // ----- internals -----

    private static async Task<string?> CaptureCodeAsync(string url, string redirect, string state, XamlRoot xamlRoot)
    {
        var tcs = new TaskCompletionSource<string?>();
        var web = new WebView2 { MinHeight = 520, MinWidth = 460 };
        var dlg = new ContentDialog
        {
            Title = Loc.I.Pick("Sign in", "登入"),
            CloseButtonText = Loc.I.Pick("Cancel", "取消"),
            XamlRoot = xamlRoot,
            Content = web,
        };

        try
        {
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "WebView2", "mail-oauth");
            Directory.CreateDirectory(userData);
            var env = await CoreWebView2Environment.CreateWithOptionsAsync("", userData, new CoreWebView2EnvironmentOptions());
            await web.EnsureCoreWebView2Async(env);

            void OnNav(CoreWebView2 s, CoreWebView2NavigationStartingEventArgs e)
            {
                if (e.Uri.StartsWith(redirect, StringComparison.OrdinalIgnoreCase))
                {
                    e.Cancel = true;
                    var (c, st, err) = ParseRedirect(e.Uri);
                    if (!string.IsNullOrEmpty(err) || st != state) tcs.TrySetResult(null);
                    else tcs.TrySetResult(c);
                    web.DispatcherQueue.TryEnqueue(() => dlg.Hide());
                }
            }
            web.CoreWebView2.NavigationStarting += OnNav;
            web.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            tcs.TrySetResult(null);
            CrashLogger.Log("MailOAuth WebView2", ex);
        }

        dlg.Closed += (_, _) => tcs.TrySetResult(null);
        _ = dlg.ShowAsync();
        var result = await tcs.Task;
        try { dlg.Hide(); } catch { }
        try { web.Close(); } catch { }
        return result;
    }

    private static (string? code, string? state, string? error) ParseRedirect(string uri)
    {
        try
        {
            var u = new Uri(uri);
            var q = u.Query.TrimStart('?');
            string? code = null, state = null, error = null;
            foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                var k = eq < 0 ? pair : pair[..eq];
                var v = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..]);
                switch (k) { case "code": code = v; break; case "state": state = v; break; case "error": error = v; break; }
            }
            return (code, state, error);
        }
        catch { return (null, null, "parse"); }
    }

    private static async Task<MailOAuthResult> ExchangeAsync(bool google, string clientId, string redirect,
        string verifier, string code)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirect,
            ["code_verifier"] = verifier,
        };
        return await PostToken(google ? GoogleToken : MsToken, form, "");
    }

    private static async Task<MailOAuthResult> PostToken(string endpoint, Dictionary<string, string> form,
        string fallbackRefresh)
    {
        try
        {
            using var http = new HttpClient();
            using var resp = await http.PostAsync(endpoint, new FormUrlEncodedContent(form));
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
                return new MailOAuthResult(false, Error: $"Token endpoint {(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var access = root.TryGetProperty("access_token", out var a) ? a.GetString() ?? "" : "";
            var refresh = root.TryGetProperty("refresh_token", out var r) ? r.GetString() ?? "" : fallbackRefresh;
            var expires = root.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 3600;
            var email = ExtractEmail(root);
            return new MailOAuthResult(true, access, refresh,
                DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expires - 60)), email);
        }
        catch (Exception ex)
        {
            return new MailOAuthResult(false, Error: ex.Message);
        }
    }

    private static string ExtractEmail(JsonElement root)
    {
        // Google returns "email"; Microsoft puts upn/email inside the id_token JWT.
        if (root.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String)
            return em.GetString() ?? "";
        if (root.TryGetProperty("id_token", out var idt) && idt.ValueKind == JsonValueKind.String)
        {
            try
            {
                var parts = (idt.GetString() ?? "").Split('.');
                if (parts.Length >= 2)
                {
                    var payload = Encoding.UTF8.GetString(B64Url(parts[1]));
                    using var d = JsonDocument.Parse(payload);
                    foreach (var k in new[] { "email", "preferred_username", "upn" })
                        if (d.RootElement.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String)
                            return v.GetString() ?? "";
                }
            }
            catch { }
        }
        return "";
    }

    private static (string verifier, string challenge) Pkce()
    {
        var verifier = RandUrl(64);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        var challenge = Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return (verifier, challenge);
    }

    private static string RandUrl(int bytes)
    {
        var buf = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(buf).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] B64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }

    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        int port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }
}
