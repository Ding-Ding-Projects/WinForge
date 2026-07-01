using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace WinForge.Services;

/// <summary>
/// 一個已登入嘅 Minecraft 帳戶 · A signed-in Minecraft account: the in-memory bearer token plus the
/// public profile (UUID / name / skin). Access tokens live ONLY in memory; the long-lived MSA refresh
/// token is persisted DPAPI-encrypted by <see cref="MinecraftAccountStore"/>. Tokens are never logged.
/// </summary>
public sealed class MinecraftAccount
{
    /// <summary>Minecraft 玩家 UUID（無連字號）· The player UUID (no dashes).</summary>
    public string Uuid { get; set; } = "";

    /// <summary>遊戲內顯示名稱 · The in-game player name.</summary>
    public string Name { get; set; } = "";

    /// <summary>面板 URL（如有）· Active skin texture URL, if any.</summary>
    public string SkinUrl { get; set; } = "";

    /// <summary>Minecraft bearer token（只喺記憶體）· Minecraft bearer access token (memory only).</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>access token 到期時間 · When the access token expires.</summary>
    public DateTimeOffset Expiry { get; set; }

    /// <summary>係咪擁有遊戲 · True when entitlements confirm game ownership.</summary>
    public bool OwnsGame { get; set; }

    /// <summary>係咪離線帳戶（無 Mojang 驗證）· True for an offline account (no Mojang auth; local/LAN play).</summary>
    public bool IsOffline { get; set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= Expiry.AddMinutes(-2);

    /// <summary>
    /// 建立一個離線帳戶 · Build an offline account for a username. The UUID matches the vanilla server's
    /// offline scheme — <c>UUID.nameUUIDFromBytes(("OfflinePlayer:"+name).getBytes(UTF_8))</c> (an MD5-based
    /// v3 UUID) — so the same name maps to the same player/world data as any other offline launcher.
    /// No Mojang sign-in; access token is "0". Intended for accounts you own / offline &amp; LAN play.
    /// </summary>
    public static MinecraftAccount Offline(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("OfflinePlayer:" + name));
        hash[6] = (byte)((hash[6] & 0x0f) | 0x30); // version 3 (name-based, MD5)
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80); // RFC 4122 variant
        return new MinecraftAccount
        {
            Name = name,
            Uuid = Convert.ToHexString(hash).ToLowerInvariant(), // 32 hex, no dashes (as the launcher expects)
            AccessToken = "0",
            OwnsGame = true,      // offline accounts bypass the entitlement gate
            IsOffline = true,
            Expiry = DateTimeOffset.MaxValue,
        };
    }

    /// <summary>合法離線名（3–16 個 [A-Za-z0-9_]）· Whether a name is a valid offline username.</summary>
    public static bool IsValidOfflineName(string name)
        => !string.IsNullOrWhiteSpace(name)
           && name.Trim().Length is >= 3 and <= 16
           && name.Trim().All(c => char.IsLetterOrDigit(c) || c == '_');
}

/// <summary>登入結果 · The outcome of a sign-in / refresh attempt.</summary>
public sealed record MinecraftAuthResult(
    bool Success,
    MinecraftAccount? Account = null,
    string RefreshToken = "",
    string Error = "",
    bool Cancelled = false);

/// <summary>裝置碼流程嘅顯示資訊 · Device-code prompt info shown to the user while they authorize.</summary>
public sealed record DeviceCodePrompt(string UserCode, string VerificationUri, string Message);

/// <summary>
/// 完整 Microsoft → Xbox Live → XSTS → Minecraft 驗證鏈 · The full MSA→XBL→XSTS→Minecraft auth chain,
/// pure managed C# (HttpClient + System.Text.Json). Two front-ends: an embedded WebView2 auth-code flow
/// and an OAuth device-code flow (show a code + microsoft.com/link). The MSA refresh token is returned to
/// the caller for DPAPI storage; access/XSTS tokens stay in memory. Child / no-Xbox XSTS error codes get
/// clear bilingual messages. Tokens are never written to the log.
///
/// PREREQUISITE — Azure app registration: Minecraft sign-in only works with an Azure AD application that
/// Mojang has approved for Minecraft. The implementer MUST supply their own client ID (Azure portal →
/// App registrations → public client / "Mobile and desktop", redirect URI for the auth-code flow, scopes
/// XboxLive.signin + offline_access). The public official-launcher id is NOT licensed for third-party use,
/// so this class ships no default id — set <see cref="ClientId"/> before calling.
/// </summary>
public static class MinecraftAuthService
{
    // The Azure AD client id is user-supplied (see class remarks). Empty until the user sets it.
    public static string ClientId { get; set; } = "";

    // The redirect URI registered for the auth-code flow. Mojang's approved native redirect.
    public const string DefaultRedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";

    private const string MsAuth = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
    private const string MsToken = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string MsDeviceCode = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string Scope = "XboxLive.signin offline_access";

    private const string XblAuth = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsAuth = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string McLogin = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string McEntitlements = "https://api.minecraftservices.com/entitlements/mcstore";
    private const string McProfile = "https://api.minecraftservices.com/minecraft/profile";

    public static bool HasClientId => !string.IsNullOrWhiteSpace(ClientId);

    // ---------------------------------------------------------------- auth-code (WebView2) flow

    /// <summary>互動式登入（彈出 WebView2）· Interactive sign-in via an embedded WebView2 dialog.</summary>
    public static async Task<MinecraftAuthResult> SignInInteractiveAsync(XamlRoot xamlRoot, string instanceId = "")
    {
        if (!HasClientId)
            return new MinecraftAuthResult(false, Error: "no-client-id");

        try
        {
            var redirect = DefaultRedirectUri;
            var state = RandUrl(16);
            var url = $"{MsAuth}?client_id={Uri.EscapeDataString(ClientId)}" +
                      $"&response_type=code&redirect_uri={Uri.EscapeDataString(redirect)}" +
                      $"&scope={Uri.EscapeDataString(Scope)}&state={state}&prompt=select_account";

            var code = await CaptureCodeAsync(url, redirect, state, xamlRoot, instanceId);
            if (code is null) return new MinecraftAuthResult(false, Cancelled: true, Error: "Cancelled");

            var form = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirect,
                ["scope"] = Scope,
            };
            var (ok, msAccess, msRefresh, err) = await PostMsTokenAsync(form);
            if (!ok) return new MinecraftAuthResult(false, Error: err);
            return await CompleteChainAsync(msAccess, msRefresh);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftAuth interactive", ex);
            return new MinecraftAuthResult(false, Error: ex.Message);
        }
    }

    // ---------------------------------------------------------------- device-code flow

    /// <summary>
    /// 裝置碼流程 · Device-code flow: ask Microsoft for a user code, surface it via <paramref name="onPrompt"/>
    /// (show "go to microsoft.com/link and enter CODE"), then poll until the user authorizes or the code
    /// expires / is cancelled.
    /// </summary>
    public static async Task<MinecraftAuthResult> SignInDeviceCodeAsync(
        Action<DeviceCodePrompt> onPrompt, CancellationToken ct)
    {
        if (!HasClientId)
            return new MinecraftAuthResult(false, Error: "no-client-id");

        try
        {
            using var http = NewHttp();
            var startForm = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["scope"] = Scope,
            });
            using var startResp = await http.PostAsync(MsDeviceCode, startForm, ct);
            var startBody = await startResp.Content.ReadAsStringAsync(ct);
            if (!startResp.IsSuccessStatusCode)
                return new MinecraftAuthResult(false, Error: $"devicecode {(int)startResp.StatusCode}");

            using var startDoc = JsonDocument.Parse(startBody);
            var root = startDoc.RootElement;
            var deviceCode = Str(root, "device_code");
            var userCode = Str(root, "user_code");
            var verUri = Str(root, "verification_uri");
            var message = Str(root, "message");
            int interval = root.TryGetProperty("interval", out var iv) ? iv.GetInt32() : 5;
            int expiresIn = root.TryGetProperty("expires_in", out var ex) ? ex.GetInt32() : 900;

            try { onPrompt(new DeviceCodePrompt(userCode, verUri, message)); } catch { }

            var deadline = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            while (DateTimeOffset.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, interval)), ct);

                var pollForm = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = ClientId,
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
                    ["device_code"] = deviceCode,
                });
                using var pollResp = await http.PostAsync(MsToken, pollForm, ct);
                var pollBody = await pollResp.Content.ReadAsStringAsync(ct);
                using var pollDoc = JsonDocument.Parse(pollBody);
                var pr = pollDoc.RootElement;

                if (pollResp.IsSuccessStatusCode)
                {
                    var msAccess = Str(pr, "access_token");
                    var msRefresh = Str(pr, "refresh_token");
                    return await CompleteChainAsync(msAccess, msRefresh);
                }

                var error = Str(pr, "error");
                if (error == "authorization_pending") continue;
                if (error == "slow_down") { interval += 5; continue; }
                if (error == "authorization_declined" || error == "expired_token" || error == "bad_verification_code")
                    return new MinecraftAuthResult(false, Error: error);
                return new MinecraftAuthResult(false, Error: error);
            }
            return new MinecraftAuthResult(false, Error: "expired_token");
        }
        catch (OperationCanceledException)
        {
            return new MinecraftAuthResult(false, Cancelled: true, Error: "Cancelled");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftAuth devicecode", ex);
            return new MinecraftAuthResult(false, Error: ex.Message);
        }
    }

    // ---------------------------------------------------------------- silent refresh

    /// <summary>用 MSA refresh token 靜默重新登入 · Refresh silently from a stored MSA refresh token.</summary>
    public static async Task<MinecraftAuthResult> RefreshAsync(string refreshToken)
    {
        if (!HasClientId) return new MinecraftAuthResult(false, Error: "no-client-id");
        if (string.IsNullOrEmpty(refreshToken)) return new MinecraftAuthResult(false, Error: "no-refresh-token");
        try
        {
            var form = new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["scope"] = Scope,
            };
            var (ok, msAccess, msRefresh, err) = await PostMsTokenAsync(form);
            if (!ok) return new MinecraftAuthResult(false, Error: err);
            return await CompleteChainAsync(msAccess, string.IsNullOrEmpty(msRefresh) ? refreshToken : msRefresh);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftAuth refresh", ex);
            return new MinecraftAuthResult(false, Error: ex.Message);
        }
    }

    /// <summary>翻譯 XSTS / 鏈錯誤碼成雙語訊息 · Translate a chain error code into a bilingual message.</summary>
    public static string DescribeError(string error) => error switch
    {
        "no-client-id" => Loc.I.Pick(
            "No Azure client ID set. Enter your Mojang-approved Azure app client ID first (see the prerequisite note).",
            "未設定 Azure client ID。請先填你經 Mojang 批核嘅 Azure 應用程式 client ID（睇先決條件說明）。"),
        "2148916233" => Loc.I.Pick(
            "This Microsoft account has no Xbox profile. Create one at xbox.com, then sign in again.",
            "呢個 Microsoft 帳戶冇 Xbox 個人檔案。去 xbox.com 建立一個，再重新登入。"),
        "2148916235" => Loc.I.Pick(
            "Xbox Live is not available in this account's country/region.",
            "Xbox Live 喺呢個帳戶嘅國家／地區唔提供。"),
        "2148916236" or "2148916237" => Loc.I.Pick(
            "This account needs adult verification to use Xbox Live.",
            "呢個帳戶要做成人驗證先可以用 Xbox Live。"),
        "2148916238" => Loc.I.Pick(
            "This is a child account. An adult must add it to a Microsoft Family group before it can sign in.",
            "呢個係兒童帳戶。要由成人喺 Microsoft 家庭群組加入佢先可以登入。"),
        "no-game" => Loc.I.Pick(
            "This Microsoft account does not own Minecraft: Java Edition.",
            "呢個 Microsoft 帳戶冇擁有 Minecraft: Java 版。"),
        "Cancelled" => Loc.I.Pick("Sign-in cancelled.", "已取消登入。"),
        _ => Loc.I.Pick($"Sign-in failed: {error}", $"登入失敗：{error}"),
    };

    // ---------------------------------------------------------------- chain internals

    private static async Task<MinecraftAuthResult> CompleteChainAsync(string msAccessToken, string msRefreshToken)
    {
        using var http = NewHttp();

        // 2) Xbox Live
        var xblReq = new
        {
            Properties = new { AuthMethod = "RPS", SiteName = "user.auth.xboxlive.com", RpsTicket = $"d={msAccessToken}" },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT",
        };
        var (xblOk, xblToken, _, xblErr) = await PostXboxAsync(http, XblAuth, xblReq);
        if (!xblOk) return new MinecraftAuthResult(false, Error: xblErr);

        // 3) XSTS
        var xstsReq = new
        {
            Properties = new { SandboxId = "RETAIL", UserTokens = new[] { xblToken } },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT",
        };
        var (xstsOk, xstsToken, uhs, xstsErr) = await PostXboxAsync(http, XstsAuth, xstsReq);
        if (!xstsOk) return new MinecraftAuthResult(false, Error: xstsErr);

        // 4) Minecraft
        var mcReq = new { identityToken = $"XBL3.0 x={uhs};{xstsToken}" };
        string mcToken; int mcExpires;
        try
        {
            using var mcResp = await http.PostAsync(McLogin, JsonContent(mcReq));
            var mcBody = await mcResp.Content.ReadAsStringAsync();
            if (!mcResp.IsSuccessStatusCode)
                return new MinecraftAuthResult(false, Error: $"mc-login {(int)mcResp.StatusCode}");
            using var mcDoc = JsonDocument.Parse(mcBody);
            mcToken = Str(mcDoc.RootElement, "access_token");
            mcExpires = mcDoc.RootElement.TryGetProperty("expires_in", out var e) ? e.GetInt32() : 86400;
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftAuth mc-login", ex);
            return new MinecraftAuthResult(false, Error: "mc-login");
        }

        var account = new MinecraftAccount
        {
            AccessToken = mcToken,
            Expiry = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, mcExpires - 60)),
        };

        // 5a) Entitlements
        try
        {
            using var entReq = new HttpRequestMessage(HttpMethod.Get, McEntitlements);
            entReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcToken);
            using var entResp = await http.SendAsync(entReq);
            var entBody = await entResp.Content.ReadAsStringAsync();
            if (entResp.IsSuccessStatusCode)
            {
                using var entDoc = JsonDocument.Parse(entBody);
                account.OwnsGame = entDoc.RootElement.TryGetProperty("items", out var items)
                    && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0;
            }
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftAuth entitlements", ex); }

        // 5b) Profile
        try
        {
            using var pReq = new HttpRequestMessage(HttpMethod.Get, McProfile);
            pReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcToken);
            using var pResp = await http.SendAsync(pReq);
            var pBody = await pResp.Content.ReadAsStringAsync();
            if (pResp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new MinecraftAuthResult(false, Error: "no-game"); // 404 = profile/ownership absent
            if (pResp.IsSuccessStatusCode)
            {
                using var pDoc = JsonDocument.Parse(pBody);
                var pr = pDoc.RootElement;
                account.Uuid = Str(pr, "id");
                account.Name = Str(pr, "name");
                account.OwnsGame = true;
                if (pr.TryGetProperty("skins", out var skins) && skins.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in skins.EnumerateArray())
                    {
                        if (Str(s, "state").Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
                        {
                            account.SkinUrl = Str(s, "url");
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftAuth profile", ex); }

        if (!account.OwnsGame || string.IsNullOrEmpty(account.Uuid))
            return new MinecraftAuthResult(false, Error: "no-game");

        return new MinecraftAuthResult(true, account, msRefreshToken);
    }

    private static async Task<(bool ok, string token, string uhs, string error)> PostXboxAsync(
        HttpClient http, string endpoint, object body)
    {
        try
        {
            using var resp = await http.PostAsync(endpoint, JsonContent(body));
            var text = await resp.Content.ReadAsStringAsync();
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // XSTS reports child / no-account via XErr in the 401 body.
                try
                {
                    using var errDoc = JsonDocument.Parse(text);
                    if (errDoc.RootElement.TryGetProperty("XErr", out var xerr))
                        return (false, "", "", xerr.GetRawText().Trim());
                }
                catch { }
                return (false, "", "", "401");
            }
            if (!resp.IsSuccessStatusCode) return (false, "", "", $"{(int)resp.StatusCode}");

            using var doc = JsonDocument.Parse(text);
            var token = Str(doc.RootElement, "Token");
            string uhs = "";
            if (doc.RootElement.TryGetProperty("DisplayClaims", out var dc)
                && dc.TryGetProperty("xui", out var xui) && xui.ValueKind == JsonValueKind.Array
                && xui.GetArrayLength() > 0)
            {
                uhs = Str(xui[0], "uhs");
            }
            return (true, token, uhs, "");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftAuth xbox", ex);
            return (false, "", "", ex.Message);
        }
    }

    private static async Task<(bool ok, string access, string refresh, string error)> PostMsTokenAsync(
        Dictionary<string, string> form)
    {
        try
        {
            using var http = NewHttp();
            using var resp = await http.PostAsync(MsToken, new FormUrlEncodedContent(form));
            var body = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(body);
            if (!resp.IsSuccessStatusCode)
                return (false, "", "", Str(doc.RootElement, "error") is { Length: > 0 } e ? e : $"token {(int)resp.StatusCode}");
            return (true, Str(doc.RootElement, "access_token"), Str(doc.RootElement, "refresh_token"), "");
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftAuth ms-token", ex);
            return (false, "", "", ex.Message);
        }
    }

    private static async Task<string?> CaptureCodeAsync(
        string url, string redirect, string state, XamlRoot xamlRoot, string instanceId)
    {
        var tcs = new TaskCompletionSource<string?>();
        var web = new WebView2 { MinHeight = 540, MinWidth = 480 };
        var dlg = new ContentDialog
        {
            Title = Loc.I.Pick("Sign in with Microsoft", "用 Microsoft 登入"),
            CloseButtonText = Loc.I.Pick("Cancel", "取消"),
            XamlRoot = xamlRoot,
            Content = web,
        };
        try
        {
            // Per-instance WebView2 profile so independent accounts never share a session.
            var leaf = string.IsNullOrWhiteSpace(instanceId) ? "default" : Sanitize(instanceId);
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "WebView2", "mc-auth", leaf);
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
                    web.DispatcherQueue.TryEnqueue(() => { try { dlg.Hide(); } catch { } });
                }
            }
            web.CoreWebView2.NavigationStarting += OnNav;
            web.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            tcs.TrySetResult(null);
            CrashLogger.Log("MinecraftAuth WebView2", ex);
        }

        dlg.Closed += (_, _) => tcs.TrySetResult(null);
        try { _ = dlg.ShowAsync(); } catch { tcs.TrySetResult(null); }
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

    private static HttpClient NewHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    private static StringContent JsonContent(object body) =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string RandUrl(int bytes)
    {
        var buf = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(buf).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_');
        return sb.ToString();
    }
}

/// <summary>
/// MSA refresh token 嘅 DPAPI 儲存 · DPAPI-backed store for per-instance MSA refresh tokens, persisted to
/// %LOCALAPPDATA%\WinForge\minecraft-accounts.json. Only the long-lived refresh token is kept (encrypted);
/// access tokens are never stored. Keyed by launcher instance id so independent instances keep independent
/// accounts. Tokens are never logged.
/// </summary>
public static class MinecraftAccountStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");
    private static readonly string FilePath = Path.Combine(Dir, "minecraft-accounts.json");
    private static readonly object Gate = new();
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.MinecraftAccountStore.v1");

    private static Dictionary<string, string> _cache = Load();

    private static Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath))
                       ?? new();
        }
        catch { }
        return new();
    }

    private static void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_cache));
        }
        catch { }
    }

    /// <summary>儲存（DPAPI 加密）一個 instance 嘅 refresh token · Save an instance's refresh token, DPAPI-encrypted.</summary>
    public static void SaveRefreshToken(string instanceId, string? refreshToken)
    {
        if (string.IsNullOrEmpty(instanceId)) return;
        lock (Gate)
        {
            if (string.IsNullOrEmpty(refreshToken)) _cache.Remove(instanceId);
            else _cache[instanceId] = Protect(refreshToken);
            SaveLocked();
        }
    }

    /// <summary>讀取（DPAPI 解密）一個 instance 嘅 refresh token · Load an instance's refresh token. "" if none.</summary>
    public static string LoadRefreshToken(string instanceId)
    {
        lock (Gate)
        {
            return _cache.TryGetValue(instanceId, out var enc) ? Unprotect(enc) : "";
        }
    }

    /// <summary>清除一個 instance 嘅 token（登出）· Remove an instance's token (sign out).</summary>
    public static void Clear(string instanceId)
    {
        lock (Gate) { if (_cache.Remove(instanceId)) SaveLocked(); }
    }

    public static bool Has(string instanceId)
    {
        lock (Gate) return _cache.ContainsKey(instanceId);
    }

    private static string Protect(string secret)
    {
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(secret), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch { return ""; }
    }

    private static string Unprotect(string enc)
    {
        if (string.IsNullOrEmpty(enc)) return "";
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(enc), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }
}
