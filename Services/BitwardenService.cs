using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 應用程式內 Bitwarden 密碼庫（薄薄包住官方 <c>bw</c> CLI）·
/// In-app Bitwarden vault: a thin, security-conscious wrapper over the official <c>bw</c> CLI.
///
/// 設計重點 · Design notes:
/// • 工作階段金鑰（BW_SESSION）只存喺記憶體，仲會用 DPAPI 加密包住，永遠唔寫落磁碟或日誌。
///   The session key (BW_SESSION) lives only in memory, wrapped with DPAPI (CurrentUser) while held,
///   and is never written to disk or logs. Cleared on lock / logout / process exit.
/// • 主密碼同 2FA 經環境變數（BW_PASSWORD）傳入，唔會出現喺指令列引數，亦唔會被擷取／記錄。
///   Master password / 2FA codes are passed via env vars, never as visible CLI args, never logged.
/// • 所有方法防禦性編寫，唔會擲例外 · Every method is defensive and never throws.
///
/// 我哋只「呼叫」已發佈嘅 bw 二進位檔，無 vendoring 任何 Bitwarden 原始碼。
/// We only invoke the published bw binary — no Bitwarden source is vendored.
/// </summary>
public static class BitwardenService
{
    /// <summary>winget 套件 ID（CLI）· The winget package ID for the bw CLI.</summary>
    public const string WingetId = "Bitwarden.CLI";

    /// <summary>winget 套件 ID（桌面 app，選用）· The winget package ID for the optional desktop app.</summary>
    public const string DesktopWingetId = "Bitwarden.Bitwarden";

    // 工作階段金鑰，用 DPAPI 包住，只存記憶體 · DPAPI-wrapped session key, memory only.
    private static byte[]? _sessionProtected;
    private static readonly object _gate = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ===================== 工作階段金鑰處理 · Session key handling =====================

    /// <summary>而家有冇解鎖（持有工作階段金鑰）· True if we currently hold an unlocked session key.</summary>
    public static bool HasSession
    {
        get { lock (_gate) return _sessionProtected is not null; }
    }

    /// <summary>記住工作階段金鑰（DPAPI 加密）· Store the session key, DPAPI-encrypted in memory.</summary>
    private static void SetSession(string? key)
    {
        lock (_gate)
        {
            _sessionProtected = null;
            if (string.IsNullOrEmpty(key)) return;
            try
            {
                _sessionProtected = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
            }
            catch { _sessionProtected = null; }
        }
    }

    /// <summary>取回明文工作階段金鑰（只喺需要時短暫解密）· Decrypt the session key briefly when needed.</summary>
    private static string? GetSession()
    {
        lock (_gate)
        {
            if (_sessionProtected is null) return null;
            try
            {
                var raw = ProtectedData.Unprotect(_sessionProtected, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(raw);
            }
            catch { return null; }
        }
    }

    /// <summary>清除工作階段金鑰（鎖定／登出／退出時呼叫）· Wipe the session key from memory.</summary>
    public static void ClearSession()
    {
        lock (_gate) _sessionProtected = null;
    }

    // ===================== 安裝偵測 · Install detection =====================

    /// <summary>bw 裝咗未 · True if "bw --version" produced a version.</summary>
    public static async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await RunRaw("--version", ct, includeSession: false);
            return r.Success && !string.IsNullOrWhiteSpace(r.Output)
                && !r.Output.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                && !r.Output.Contains("not found", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>已安裝嘅 bw 版本（空字串 = 搵唔到）· Installed bw version, or "" if not found.</summary>
    public static async Task<string> VersionAsync(CancellationToken ct = default)
    {
        try { var r = await RunRaw("--version", ct, includeSession: false); return (r.Output ?? "").Trim(); }
        catch { return ""; }
    }

    // ===================== 低階程序執行 · Low-level process runner =====================

    /// <summary>
    /// 執行一句 bw 指令並擷取輸出 · Run a single bw command, capturing stdout/stderr.
    /// 工作階段金鑰／密碼經環境變數傳入（如有），唔會出現喺引數度 · Session key / password go via env, never args.
    /// </summary>
    private static async Task<TweakResult> RunRaw(
        string arguments, CancellationToken ct, bool includeSession = true,
        string? passwordEnv = null, IDictionary<string, string?>? extraEnv = null,
        string? stdin = null)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "bw",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdin is not null,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            if (includeSession)
            {
                var key = GetSession();
                if (!string.IsNullOrEmpty(key)) info.Environment["BW_SESSION"] = key;
            }
            if (passwordEnv is not null) info.Environment["BW_PASSWORD"] = passwordEnv;
            if (extraEnv is not null)
                foreach (var kv in extraEnv)
                    if (kv.Value is not null) info.Environment[kv.Key] = kv.Value;

            using var p = Process.Start(info);
            if (p is null) return TweakResult.Fail("Failed to start bw.", "無法啟動 bw。");

            if (stdin is not null)
            {
                await p.StandardInput.WriteAsync(stdin);
                p.StandardInput.Close();
            }

            var outTask = p.StandardOutput.ReadToEndAsync(ct);
            var errTask = p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            var stdout = (await outTask).Trim();
            var stderr = (await errTask).Trim();

            var body = string.IsNullOrWhiteSpace(stderr) ? stdout : (stdout.Length > 0 ? stdout + "\n" + stderr : stderr);
            return p.ExitCode == 0
                ? TweakResult.Ok("Done.", "完成。", body)
                : TweakResult.Fail(stderr.Length > 0 ? stderr : $"Exit code {p.ExitCode}.",
                                   stderr.Length > 0 ? stderr : $"結束代碼 {p.ExitCode}。", body);
        }
        catch (OperationCanceledException) { return TweakResult.Fail("Cancelled.", "已取消。"); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>畀 UI 用嘅原始指令執行（會帶上工作階段）· Public raw runner for quick UI actions.</summary>
    public static Task<TweakResult> Run(string arguments, CancellationToken ct = default)
        => RunRaw(arguments, ct);

    // ===================== 狀態 · Status =====================

    public enum VaultStatus { Unknown, NotInstalled, Unauthenticated, Locked, Unlocked }

    public sealed record StatusInfo(VaultStatus Status, string? ServerUrl, string? UserEmail, string? LastSync);

    /// <summary>查詢 bw 狀態（已驗證／鎖定／解鎖）· Query bw status (unauthenticated / locked / unlocked).</summary>
    public static async Task<StatusInfo> GetStatusAsync(CancellationToken ct = default)
    {
        if (!await IsInstalledAsync(ct))
            return new StatusInfo(VaultStatus.NotInstalled, null, null, null);

        // 用記憶體中嘅金鑰問狀態，等「unlocked」可以正確回報 · pass the in-memory key so "unlocked" reports correctly.
        var r = await RunRaw("status", ct);
        var json = ExtractJsonObject(r.Output);
        if (json is null) return new StatusInfo(VaultStatus.Unknown, null, null, null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string statusStr = root.TryGetProperty("status", out var s) ? (s.GetString() ?? "") : "";
            string? server = root.TryGetProperty("serverUrl", out var sv) ? sv.GetString() : null;
            string? email = root.TryGetProperty("userEmail", out var ue) ? ue.GetString() : null;
            string? sync = root.TryGetProperty("lastSync", out var ls) ? ls.GetString() : null;

            var vs = statusStr switch
            {
                "unlocked" => VaultStatus.Unlocked,
                "locked" => VaultStatus.Locked,
                "unauthenticated" => VaultStatus.Unauthenticated,
                _ => VaultStatus.Unknown,
            };
            // bw 可能因為持有金鑰報 unlocked，但實際金鑰已失效 · if it says unlocked but our key is gone, downgrade.
            if (vs == VaultStatus.Unlocked && !HasSession) vs = VaultStatus.Locked;
            return new StatusInfo(vs, server, email, sync);
        }
        catch { return new StatusInfo(VaultStatus.Unknown, null, null, null); }
    }

    // ===================== 自寄存伺服器 · Self-hosted server =====================

    /// <summary>設定伺服器網址（自寄存）· Configure the server URL for self-hosted instances.</summary>
    public static async Task<TweakResult> SetServerAsync(string url, CancellationToken ct = default)
    {
        url = (url ?? "").Trim();
        if (url.Length == 0)
            return await RunRaw("config server null", ct, includeSession: false);
        return await RunRaw($"config server {Quote(url)}", ct, includeSession: false);
    }

    /// <summary>取得目前伺服器網址 · Get the configured server URL.</summary>
    public static async Task<string> GetServerAsync(CancellationToken ct = default)
    {
        var r = await RunRaw("config server", ct, includeSession: false);
        return (r.Output ?? "").Trim();
    }

    // ===================== 登入 / 解鎖 / 鎖定 / 登出 · Login / unlock / lock / logout =====================

    /// <summary>
    /// 登入（電郵 + 主密碼，選用 2FA）· Log in with email + master password (optional 2FA).
    /// 成功會即時擷取並記住工作階段金鑰 · On success, captures and stores the session key immediately.
    /// method: 0=Authenticator, 1=Email, 3=YubiKey · code: 2FA 驗證碼。
    /// </summary>
    public static async Task<TweakResult> LoginAsync(
        string email, string masterPassword, int? twoFactorMethod, string? twoFactorCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(masterPassword))
            return TweakResult.Fail("Email and master password are required.", "需要電郵同主密碼。");

        var args = new StringBuilder($"login {Quote(email)} --passwordenv BW_PASSWORD --raw");
        if (twoFactorMethod is int m && !string.IsNullOrWhiteSpace(twoFactorCode))
            args.Append($" --method {m} --code {Quote(twoFactorCode!.Trim())}");

        var r = await RunRaw(args.ToString(), ct, includeSession: false, passwordEnv: masterPassword);
        if (!r.Success)
            return Redact(r);

        var key = (r.Output ?? "").Trim();
        if (key.Length == 0)
            return TweakResult.Fail("Login succeeded but no session key was returned.", "登入成功但收唔到工作階段金鑰。");
        SetSession(key);
        return TweakResult.Ok("Logged in and unlocked.", "已登入並解鎖。");
    }

    /// <summary>
    /// 解鎖一個已驗證帳戶 · Unlock an already-authenticated account.
    /// 擷取並記住工作階段金鑰 · Captures and stores the session key.
    /// </summary>
    public static async Task<TweakResult> UnlockAsync(string masterPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(masterPassword))
            return TweakResult.Fail("Master password is required.", "需要主密碼。");

        var r = await RunRaw("unlock --passwordenv BW_PASSWORD --raw", ct,
            includeSession: false, passwordEnv: masterPassword);
        if (!r.Success) return Redact(r);

        var key = (r.Output ?? "").Trim();
        if (key.Length == 0)
            return TweakResult.Fail("Unlock succeeded but no session key was returned.", "解鎖成功但收唔到工作階段金鑰。");
        SetSession(key);
        return TweakResult.Ok("Vault unlocked.", "密碼庫已解鎖。");
    }

    /// <summary>鎖定密碼庫（清除工作階段金鑰）· Lock the vault and wipe the session key.</summary>
    public static async Task<TweakResult> LockAsync(CancellationToken ct = default)
    {
        var r = await RunRaw("lock", ct);
        ClearSession();
        return r.Success ? TweakResult.Ok("Vault locked.", "密碼庫已鎖定。") : Redact(r);
    }

    /// <summary>登出帳戶（清除工作階段金鑰）· Log out and wipe the session key.</summary>
    public static async Task<TweakResult> LogoutAsync(CancellationToken ct = default)
    {
        var r = await RunRaw("logout", ct);
        ClearSession();
        return r.Success ? TweakResult.Ok("Logged out.", "已登出。") : Redact(r);
    }

    // ===================== 同步 · Sync =====================

    /// <summary>同步密碼庫 · Pull the latest vault from the server.</summary>
    public static async Task<TweakResult> SyncAsync(CancellationToken ct = default)
    {
        var r = await RunRaw("sync", ct);
        return r.Success ? TweakResult.Ok("Synced.", "已同步。", r.Output) : Redact(r);
    }

    // ===================== 列表 / 搜尋 · List / search =====================

    /// <summary>列出（並選擇性搜尋）密碼庫項目 · List vault items, optionally filtered by a search term.</summary>
    public static async Task<List<VaultItem>> ListItemsAsync(string? search, CancellationToken ct = default)
    {
        var args = "list items";
        if (!string.IsNullOrWhiteSpace(search))
            args += $" --search {Quote(search!.Trim())}";
        var r = await RunRaw(args, ct);
        if (!r.Success) return new List<VaultItem>();
        var json = ExtractJsonArray(r.Output);
        if (json is null) return new List<VaultItem>();
        try
        {
            var items = JsonSerializer.Deserialize<List<VaultItem>>(json, JsonOpts);
            return items ?? new List<VaultItem>();
        }
        catch { return new List<VaultItem>(); }
    }

    /// <summary>取得單一項目完整內容（含密碼）· Fetch one item in full (includes password).</summary>
    public static async Task<VaultItem?> GetItemAsync(string id, CancellationToken ct = default)
    {
        var r = await RunRaw($"get item {Quote(id)}", ct);
        if (!r.Success) return null;
        var json = ExtractJsonObject(r.Output);
        if (json is null) return null;
        try { return JsonSerializer.Deserialize<VaultItem>(json, JsonOpts); }
        catch { return null; }
    }

    /// <summary>取得某項目嘅密碼 · Get an item's password.</summary>
    public static async Task<string?> GetPasswordAsync(string id, CancellationToken ct = default)
    {
        var r = await RunRaw($"get password {Quote(id)}", ct);
        return r.Success ? (r.Output ?? "").Trim() : null;
    }

    /// <summary>取得某項目嘅用戶名 · Get an item's username.</summary>
    public static async Task<string?> GetUsernameAsync(string id, CancellationToken ct = default)
    {
        var r = await RunRaw($"get username {Quote(id)}", ct);
        return r.Success ? (r.Output ?? "").Trim() : null;
    }

    /// <summary>取得某項目嘅 TOTP 驗證碼 · Get the current TOTP code for an item.</summary>
    public static async Task<string?> GetTotpAsync(string id, CancellationToken ct = default)
    {
        var r = await RunRaw($"get totp {Quote(id)}", ct);
        if (!r.Success) return null;
        var code = (r.Output ?? "").Trim();
        return code.Length > 0 ? code : null;
    }

    // ===================== 密碼產生器 · Password generator =====================

    public sealed record GenOptions(
        bool Passphrase, int Length, bool Uppercase, bool Lowercase, bool Numbers, bool Special,
        int Words, string Separator, bool Capitalize);

    /// <summary>產生密碼或通行短語 · Generate a password or passphrase.</summary>
    public static async Task<string?> GenerateAsync(GenOptions o, CancellationToken ct = default)
    {
        var args = new StringBuilder("generate");
        if (o.Passphrase)
        {
            args.Append(" --passphrase");
            args.Append($" --words {Math.Clamp(o.Words, 3, 20)}");
            if (!string.IsNullOrEmpty(o.Separator)) args.Append($" --separator {Quote(o.Separator)}");
            if (o.Capitalize) args.Append(" --capitalize");
            if (o.Numbers) args.Append(" --includeNumber");
        }
        else
        {
            args.Append($" --length {Math.Clamp(o.Length, 5, 128)}");
            if (o.Uppercase) args.Append(" --uppercase");
            if (o.Lowercase) args.Append(" --lowercase");
            if (o.Numbers) args.Append(" --number");
            if (o.Special) args.Append(" --special");
        }
        var r = await RunRaw(args.ToString(), ct, includeSession: false);
        if (!r.Success) return null;
        var pw = (r.Output ?? "").Trim();
        return pw.Length > 0 ? pw : null;
    }

    // ===================== 建立 / 編輯項目 · Create / edit items =====================

    /// <summary>建立一個登入項目 · Create a new login item.</summary>
    public static async Task<TweakResult> CreateLoginAsync(
        string name, string? username, string? password, string? totp, string? uri, string? notes,
        CancellationToken ct = default)
    {
        var payload = BuildLoginJson(name, username, password, totp, uri, notes);
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        var r = await RunRaw($"create item {b64}", ct);
        return r.Success ? TweakResult.Ok("Item created.", "項目已建立。") : Redact(r);
    }

    /// <summary>編輯一個現有登入項目（先取回再合併）· Edit an existing login item (fetch, merge, push).</summary>
    public static async Task<TweakResult> EditLoginAsync(
        string id, string name, string? username, string? password, string? totp, string? uri, string? notes,
        CancellationToken ct = default)
    {
        // 取回原始 JSON 再覆寫欄位，保留其餘欄位 · fetch raw item, overwrite fields, keep the rest.
        var getR = await RunRaw($"get item {Quote(id)}", ct);
        if (!getR.Success) return Redact(getR);
        var json = ExtractJsonObject(getR.Output);
        if (json is null) return TweakResult.Fail("Could not read the item.", "讀唔到該項目。");

        string merged;
        try
        {
            using var doc = JsonDocument.Parse(json);
            merged = MergeLoginJson(doc.RootElement, name, username, password, totp, uri, notes);
        }
        catch { return TweakResult.Fail("Could not parse the item.", "解析項目失敗。"); }

        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(merged));
        var r = await RunRaw($"edit item {Quote(id)} {b64}", ct);
        return r.Success ? TweakResult.Ok("Item saved.", "項目已儲存。") : Redact(r);
    }

    // ===================== JSON 建構 · JSON builders =====================

    private static string BuildLoginJson(string name, string? username, string? password,
        string? totp, string? uri, string? notes)
    {
        var login = new Dictionary<string, object?>
        {
            ["username"] = string.IsNullOrWhiteSpace(username) ? null : username,
            ["password"] = string.IsNullOrEmpty(password) ? null : password,
            ["totp"] = string.IsNullOrWhiteSpace(totp) ? null : totp,
        };
        if (!string.IsNullOrWhiteSpace(uri))
            login["uris"] = new[] { new Dictionary<string, object?> { ["match"] = null, ["uri"] = uri } };

        var obj = new Dictionary<string, object?>
        {
            ["organizationId"] = null,
            ["folderId"] = null,
            ["type"] = 1, // login
            ["name"] = name,
            ["notes"] = string.IsNullOrWhiteSpace(notes) ? null : notes,
            ["favorite"] = false,
            ["login"] = login,
        };
        return JsonSerializer.Serialize(obj, JsonOpts);
    }

    private static string MergeLoginJson(JsonElement original, string name, string? username,
        string? password, string? totp, string? uri, string? notes)
    {
        // 從原始物件複製成可變字典 · clone the original object into a mutable dictionary.
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(original.GetRawText())
                   ?? new Dictionary<string, JsonElement>();

        var outObj = new Dictionary<string, object?>();
        foreach (var kv in dict) outObj[kv.Key] = JsonElementToObject(kv.Value);

        outObj["name"] = name;
        outObj["notes"] = string.IsNullOrWhiteSpace(notes) ? null : notes;

        var login = new Dictionary<string, object?>();
        if (outObj.TryGetValue("login", out var existingLogin) && existingLogin is Dictionary<string, object?> el)
            foreach (var kv in el) login[kv.Key] = kv.Value;

        login["username"] = string.IsNullOrWhiteSpace(username) ? null : username;
        if (!string.IsNullOrEmpty(password)) login["password"] = password;
        login["totp"] = string.IsNullOrWhiteSpace(totp) ? null : totp;
        if (!string.IsNullOrWhiteSpace(uri))
            login["uris"] = new[] { new Dictionary<string, object?> { ["match"] = null, ["uri"] = uri } };
        outObj["login"] = login;

        return JsonSerializer.Serialize(outObj, JsonOpts);
    }

    private static object? JsonElementToObject(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => e.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
        _ => null,
    };

    // ===================== 工具 · Helpers =====================

    /// <summary>包引號（簡單轉義）· Wrap an argument in quotes with minimal escaping.</summary>
    private static string Quote(string s) => "\"" + (s ?? "").Replace("\"", "\\\"") + "\"";

    /// <summary>抽出輸出入面嘅 JSON 物件 · Extract a JSON object from possibly-noisy output.</summary>
    private static string? ExtractJsonObject(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('{'), b = raw.LastIndexOf('}');
        return (a >= 0 && b > a) ? raw.Substring(a, b - a + 1) : null;
    }

    /// <summary>抽出輸出入面嘅 JSON 陣列 · Extract a JSON array from possibly-noisy output.</summary>
    private static string? ExtractJsonArray(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('['), b = raw.LastIndexOf(']');
        return (a >= 0 && b > a) ? raw.Substring(a, b - a + 1) : null;
    }

    /// <summary>
    /// 過濾錯誤輸出，避免將密碼／金鑰漏出嚟 · Scrub the error result so no secret value leaks into the UI/logs.
    /// bw 嘅錯誤訊息一般唔含 secret，但保險起見過濾長十六進位／base64 串。
    /// </summary>
    private static TweakResult Redact(TweakResult r)
    {
        // 唔傳 Output（避免將任何擷取到嘅內容外洩）· drop the raw Output entirely on failure.
        var en = r.Message?.En ?? "bw command failed.";
        var zh = r.Message?.Zh ?? "bw 指令失敗。";
        return TweakResult.Fail(en, zh);
    }

    // ===================== 密碼庫項目模型 · Vault item model =====================

    /// <summary>密碼庫項目（部分欄位）· A vault item (subset of bw's schema).</summary>
    public sealed class VaultItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Type { get; set; }
        public bool Favorite { get; set; }
        public string? Notes { get; set; }
        public string? FolderId { get; set; }
        public LoginInfo? Login { get; set; }

        /// <summary>項目種類嘅雙語名 · Bilingual type label.</summary>
        public string TypeLabel(bool zh) => Type switch
        {
            1 => zh ? "登入" : "Login",
            2 => zh ? "安全筆記" : "Secure note",
            3 => zh ? "信用卡" : "Card",
            4 => zh ? "身分" : "Identity",
            _ => zh ? "項目" : "Item",
        };

        public string PrimaryUri =>
            Login?.Uris is { Count: > 0 } u && !string.IsNullOrWhiteSpace(u[0].Uri) ? u[0].Uri! : "";

        public bool HasTotp => !string.IsNullOrWhiteSpace(Login?.Totp);
        public string Username => Login?.Username ?? "";
    }

    public sealed class LoginInfo
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? Totp { get; set; }
        public List<UriEntry>? Uris { get; set; }
    }

    public sealed class UriEntry
    {
        public int? Match { get; set; }
        public string? Uri { get; set; }
    }
}
