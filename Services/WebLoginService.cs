using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using WinForge.Controls;

namespace WinForge.Services;

/// <summary>
/// 內置登入服務 · In-app login / web-auth plumbing built on the embedded WebView2 control.
///
/// 其他模組（Git、Cloudflare、AI）可以叫 <see cref="CaptureAsync"/>，喺 WinForge 內置嘅
/// Edge/Chromium 視窗完成 OAuth 或者網頁登入，然後攞返 redirect 嘅 query/fragment 參數
/// 同埋指定嘅 cookie — 唔使彈出外置瀏覽器。
///
/// Other modules (Git, Cloudflare, AI providers) call <see cref="CaptureAsync"/> to run an OAuth
/// or web sign-in inside WinForge's embedded Edge/Chromium window and get back the redirect's
/// query/fragment params plus named cookies — no external browser bounce.
/// </summary>
public static class WebLoginService
{
    /// <summary>每個服務獨立嘅 WebView2 資料夾根 · Root for per-profile WebView2 user-data folders.</summary>
    public static string ProfilesRoot
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "WebView2");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    /// <summary>指定 profile 嘅 user-data 資料夾路徑 · Resolve the user-data folder for a profile.</summary>
    public static string ProfileFolder(string? profile)
    {
        var safe = Sanitize(string.IsNullOrWhiteSpace(profile) ? "default" : profile!);
        var dir = Path.Combine(ProfilesRoot, safe);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>列出已有嘅 profile 名 · List existing profile names (cookie jars on disk).</summary>
    public static IReadOnlyList<string> ListProfiles()
    {
        var list = new List<string>();
        try
        {
            foreach (var d in Directory.EnumerateDirectories(ProfilesRoot))
                list.Add(Path.GetFileName(d));
        }
        catch { /* ignore */ }
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }

    /// <summary>
    /// 清除（登出）一個 profile：刪走佢嘅 WebView2 資料夾，連 cookie 同 session 一齊清。
    /// Sign-out / clear: delete a profile's WebView2 folder (cookies + session). The WebView using
    /// the folder must be closed first — the folder is locked while in use.
    /// </summary>
    public static bool ClearProfile(string? profile)
    {
        try
        {
            var dir = ProfileFolder(profile);
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// 核心入口：開出登入對話框，跑流程，攞返結果。
    /// Core entry point: open the login dialog, drive the flow, return the captured result.
    /// MUST be called on the UI thread (it shows a ContentDialog). The result is marshalled back
    /// to the caller on that same thread.
    /// </summary>
    public static async Task<LoginResult> CaptureAsync(LoginRequest request, XamlRoot xamlRoot)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.StartUrl))
            return LoginResult.Failed("No start URL was provided. · 未提供起始網址。");

        var dialog = new LoginDialog(request) { XamlRoot = xamlRoot };
        return await dialog.RunAsync();
    }

    private static string Sanitize(string name)
    {
        var chars = name.ToCharArray();
        foreach (var bad in Path.GetInvalidFileNameChars())
            for (int i = 0; i < chars.Length; i++)
                if (chars[i] == bad) chars[i] = '_';
        var clean = new string(chars).Trim();
        return clean.Length == 0 ? "default" : clean;
    }
}

/// <summary>
/// 一次登入請求嘅規則 · Rules for one login capture: where to start and when it is "done".
/// </summary>
public sealed class LoginRequest
{
    /// <summary>起始網址（例如 OAuth authorize URL）· Start URL (e.g. an OAuth authorize URL).</summary>
    public string StartUrl { get; init; } = "";

    /// <summary>
    /// 完成判定：當瀏覽到呢個前綴嘅 redirect URI 就算成功（捉 query/fragment 入面嘅 code/token）。
    /// Completion rule: navigating to a URL starting with this redirect-URI prefix completes the flow
    /// and captures the query/fragment params (e.g. ?code=… or #access_token=…).
    /// </summary>
    public string? RedirectUriPrefix { get; init; }

    /// <summary>
    /// 要捉嘅 cookie 名（喺 <see cref="CookieDomain"/> 上）· Cookie names to capture from
    /// <see cref="CookieDomain"/>. When all are present the flow may complete (see CompleteOnCookies).
    /// </summary>
    public IReadOnlyList<string> CookieNames { get; init; } = Array.Empty<string>();

    /// <summary>抓 cookie 嘅來源網址／網域 · The URL/domain whose cookies are read.</summary>
    public string? CookieDomain { get; init; }

    /// <summary>當所有指定 cookie 都齊就自動完成 · Auto-complete once every named cookie is present.</summary>
    public bool CompleteOnCookies { get; init; }

    /// <summary>每個服務獨立嘅 cookie jar · Per-service profile (separate cookie jar). Null = "default".</summary>
    public string? Profile { get; init; }

    /// <summary>對話框標題 · Dialog title (English).</summary>
    public string TitleEn { get; init; } = "Sign in";

    /// <summary>對話框標題（粵語）· Dialog title (Cantonese).</summary>
    public string TitleZh { get; init; } = "登入";

    /// <summary>自訂 user-agent（可選）· Optional custom user-agent string.</summary>
    public string? UserAgent { get; init; }
}

/// <summary>
/// 一次登入嘅結果 · The outcome of one capture. Cancel/close yields Success = false without throwing.
/// </summary>
public sealed class LoginResult
{
    public bool Success { get; init; }

    /// <summary>命中嘅完整 redirect URI · The full redirect URI that matched (if any).</summary>
    public string? RedirectUri { get; init; }

    /// <summary>由 query + fragment 解析出嚟嘅參數 · Params parsed from the query and fragment.</summary>
    public IReadOnlyDictionary<string, string> QueryParams { get; init; } =
        new Dictionary<string, string>();

    /// <summary>捉到嘅 cookie（名 → 值）· Captured cookies (name → value).</summary>
    public IReadOnlyDictionary<string, string> Cookies { get; init; } =
        new Dictionary<string, string>();

    /// <summary>完成時嘅原始網址 · The raw URL at completion.</summary>
    public string? RawUrl { get; init; }

    /// <summary>失敗／取消時嘅訊息 · Message on failure/cancel (never contains secrets).</summary>
    public string? Error { get; init; }

    public static LoginResult Failed(string error) => new() { Success = false, Error = error };
    public static LoginResult Cancelled() => new() { Success = false, Error = "Cancelled · 已取消" };
}
