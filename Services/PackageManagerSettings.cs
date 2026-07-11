using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 套件管理器嘅強型別設定 · Strongly-typed settings wrappers for the in-app package manager
/// (UniGetUI-style background updates / notifications / proxy / backup). 全部都係包住
/// <see cref="SettingsStore"/> 嘅 "pkg.set.*" 鍵，永遠唔擲例外，讀壞值就回預設。
/// All values live under "pkg.set.*" keys in <see cref="SettingsStore"/>; every getter is defensive —
/// a corrupt/missing value falls back to a sane default and never throws.
/// </summary>
public static class PackageManagerSettings
{
    private const string Prefix = "pkg.set.";

    // 容許嘅檢查間隔（分鐘）· Allowed auto-check interval presets, in minutes.
    public static readonly int[] IntervalPresets = { 10, 30, 60, 180, 360, 720, 1440, 10080 };

    // 容許嘅「最少更新年齡」（日）· Allowed "minimum update age" presets, in days (+ custom).
    public static readonly int[] MinimumAgePresets = { 0, 1, 3, 7, 14, 30 };

    // ===== low-level helpers (never throw) =====

    private static string GetRaw(string key, string fallback)
    {
        try { return SettingsStore.Get(Prefix + key, fallback); }
        catch { return fallback; }
    }

    private static void SetRaw(string key, string value)
    {
        try { SettingsStore.Set(Prefix + key, value ?? ""); }
        catch { /* best effort */ }
    }

    private static bool GetBool(string key, bool fallback)
    {
        var s = GetRaw(key, fallback ? "True" : "False");
        return bool.TryParse(s, out var b) ? b : fallback;
    }

    private static void SetBool(string key, bool value) => SetRaw(key, value ? "True" : "False");

    private static int GetInt(string key, int fallback)
    {
        var s = GetRaw(key, fallback.ToString(CultureInfo.InvariantCulture));
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;
    }

    private static void SetInt(string key, int value) => SetRaw(key, value.ToString(CultureInfo.InvariantCulture));

    private static int Clamp(int value, int min, int max) => value < min ? min : (value > max ? max : value);

    private static HashSet<string> GetSet(string key)
    {
        var raw = GetRaw(key, "");
        return new HashSet<string>(
            raw.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void SetSet(string key, IEnumerable<string> items)
        => SetRaw(key, string.Join("\n", items.Where(s => !string.IsNullOrWhiteSpace(s))));

    private static Dictionary<string, string> GetMap(string key)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in GetRaw(key, "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = line.IndexOf('\t');
            if (eq <= 0) continue;
            var k = line.Substring(0, eq).Trim();
            var v = line.Substring(eq + 1);
            if (k.Length > 0) map[k] = v;
        }
        return map;
    }

    private static void SetMap(string key, IReadOnlyDictionary<string, string> map)
        => SetRaw(key, string.Join("\n", map.Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
            .Select(kv => $"{kv.Key}\t{kv.Value?.Replace("\n", " ").Replace("\r", " ")}")));

    // ===== 1) Scheduled updates · 排程更新 =====

    /// <summary>自動檢查更新 · Periodically check for updates in the background.</summary>
    public static bool AutoCheckEnabled
    {
        get => GetBool("autoCheck", false);
        set => SetBool("autoCheck", value);
    }

    /// <summary>檢查間隔（分鐘，已夾喺合理範圍）· Check interval in minutes (clamped to a sane range).</summary>
    public static int CheckIntervalMinutes
    {
        get => Clamp(GetInt("checkIntervalMin", 60), 5, 100000);
        set => SetInt("checkIntervalMin", Clamp(value, 5, 100000));
    }

    /// <summary>自動安裝更新 · Automatically install found updates.</summary>
    public static bool AutoInstallUpdates
    {
        get => GetBool("autoInstall", false);
        set => SetBool("autoInstall", value);
    }

    /// <summary>用流量計費網絡時停用自動操作 · Skip auto-operations on a metered connection.</summary>
    public static bool DisableOnMetered
    {
        get => GetBool("disableOnMetered", true);
        set => SetBool("disableOnMetered", value);
    }

    /// <summary>用電池時停用自動操作 · Skip auto-operations while on battery.</summary>
    public static bool DisableOnBattery
    {
        get => GetBool("disableOnBattery", true);
        set => SetBool("disableOnBattery", value);
    }

    /// <summary>慳電模式時停用自動操作 · Skip auto-operations while in Battery Saver.</summary>
    public static bool DisableOnBatterySaver
    {
        get => GetBool("disableOnBatterySaver", true);
        set => SetBool("disableOnBatterySaver", value);
    }

    // ===== 2) Update security · 更新安全 =====

    /// <summary>最少更新年齡（日）· Only surface updates whose release is at least N days old (0 = no delay).</summary>
    public static int MinimumUpdateAgeDays
    {
        get => Clamp(GetInt("minUpdateAgeDays", 0), 0, 3650);
        set => SetInt("minUpdateAgeDays", Clamp(value, 0, 3650));
    }

    /// <summary>安裝程式來源主機改變時警告（winget）· Warn when the installer host/URL changes (winget security check).</summary>
    public static bool WarnInstallerHostChange
    {
        get => GetBool("warnInstallerHost", true);
        set => SetBool("warnInstallerHost", value);
    }

    // ===== 3) Operations · 操作 =====

    /// <summary>同時操作數（1..10）· Maximum concurrent operations (1..10).</summary>
    public static int ParallelOperationCount
    {
        get => Clamp(GetInt("parallelOps", 2), 1, 10);
        set => SetInt("parallelOps", Clamp(value, 1, 10));
    }

    // ===== 4) Notifications · 通知 =====

    /// <summary>通知總開關 · Master notifications switch.</summary>
    public static bool NotificationsEnabled
    {
        get => GetBool("notifyMaster", true);
        set => SetBool("notifyMaster", value);
    }

    /// <summary>停用「進度」通知 · Suppress progress notifications.</summary>
    public static bool DisableProgressNotifications
    {
        get => GetBool("notifyDisableProgress", false);
        set => SetBool("notifyDisableProgress", value);
    }

    /// <summary>停用「成功」通知 · Suppress success notifications.</summary>
    public static bool DisableSuccessNotifications
    {
        get => GetBool("notifyDisableSuccess", false);
        set => SetBool("notifyDisableSuccess", value);
    }

    /// <summary>停用「錯誤」通知 · Suppress error notifications.</summary>
    public static bool DisableErrorNotifications
    {
        get => GetBool("notifyDisableError", false);
        set => SetBool("notifyDisableError", value);
    }

    /// <summary>停用「有更新」通知 · Suppress "updates available" notifications.</summary>
    public static bool DisableUpdatesAvailableNotifications
    {
        get => GetBool("notifyDisableUpdates", false);
        set => SetBool("notifyDisableUpdates", value);
    }

    /// <summary>已靜音通知嘅管理器（鍵集合）· Per-manager mute set (manager keys whose notifications are muted).</summary>
    public static HashSet<string> MutedManagers => GetSet("notifyMutedManagers");

    /// <summary>某管理器嘅通知有冇被靜音 · Is a manager's notifications muted?</summary>
    public static bool IsManagerMuted(string managerKey)
        => !string.IsNullOrEmpty(managerKey) && MutedManagers.Contains(managerKey);

    /// <summary>設定某管理器嘅靜音狀態 · Set a manager's mute state.</summary>
    public static void SetManagerMuted(string managerKey, bool muted)
    {
        if (string.IsNullOrEmpty(managerKey)) return;
        var set = MutedManagers;
        if (muted) set.Add(managerKey); else set.Remove(managerKey);
        SetSet("notifyMutedManagers", set);
    }

    // ===== 5) Per-manager executable / args · 各管理器可執行檔／參數 =====

    /// <summary>各管理器自訂可執行檔路徑 · Per-manager custom executable path (key -> path).</summary>
    public static Dictionary<string, string> ManagerExecutablePaths => GetMap("mgrExePaths");

    /// <summary>讀某管理器嘅可執行檔路徑 · Get a manager's custom executable path ("" if none).</summary>
    public static string GetManagerExecutablePath(string managerKey)
        => ManagerExecutablePaths.TryGetValue(managerKey ?? "", out var v) ? v : "";

    /// <summary>設某管理器嘅可執行檔路徑（空白＝清除）· Set a manager's custom executable path (blank clears it).</summary>
    public static void SetManagerExecutablePath(string managerKey, string path)
    {
        if (string.IsNullOrEmpty(managerKey)) return;
        var map = ManagerExecutablePaths;
        if (string.IsNullOrWhiteSpace(path)) map.Remove(managerKey);
        else map[managerKey] = path.Trim();
        SetMap("mgrExePaths", map);
    }

    /// <summary>各管理器額外呼叫參數 · Per-manager extra call arguments (key -> args).</summary>
    public static Dictionary<string, string> ManagerExecutableArgs => GetMap("mgrExeArgs");

    /// <summary>讀某管理器嘅額外參數 · Get a manager's extra call args ("" if none).</summary>
    public static string GetManagerExecutableArgs(string managerKey)
        => ManagerExecutableArgs.TryGetValue(managerKey ?? "", out var v) ? v : "";

    /// <summary>設某管理器嘅額外參數（空白＝清除）· Set a manager's extra call args (blank clears it).</summary>
    public static void SetManagerExecutableArgs(string managerKey, string args)
    {
        if (string.IsNullOrEmpty(managerKey)) return;
        var map = ManagerExecutableArgs;
        if (string.IsNullOrWhiteSpace(args)) map.Remove(managerKey);
        else map[managerKey] = args.Trim();
        SetMap("mgrExeArgs", map);
    }

    // ===== 6) Proxy · 代理 =====

    /// <summary>代理 URL · Proxy URL (e.g. http://host:port). Empty = no proxy.</summary>
    public static string ProxyUrl
    {
        get => GetRaw("proxyUrl", "");
        set => SetRaw("proxyUrl", (value ?? "").Trim());
    }

    /// <summary>代理使用者名稱 · Proxy username.</summary>
    public static string ProxyUser
    {
        get => GetRaw("proxyUser", "");
        set => SetRaw("proxyUser", (value ?? "").Trim());
    }

    /// <summary>代理密碼（DPAPI 加密儲存）· Proxy password, stored DPAPI-encrypted at rest.</summary>
    public static string ProxyPassword
    {
        get => Unprotect(GetRaw("proxyPassEnc", ""));
        set => SetRaw("proxyPassEnc", Protect(value ?? ""));
    }

    // ===== DPAPI helpers for the proxy password (CurrentUser scope) · DPAPI 加密代理密碼 =====
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WinForge.PackageManager.Proxy.v1");

    private static string Protect(string plain)
    {
        try
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }
        catch { return ""; }
    }

    private static string Unprotect(string enc)
    {
        try
        {
            if (string.IsNullOrEmpty(enc)) return "";
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(enc), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }

    /// <summary>有冇設定代理 · Whether a proxy URL is configured.</summary>
    public static bool HasProxy => !string.IsNullOrWhiteSpace(ProxyUrl);

    /// <summary>
    /// 回傳某管理器明白嘅代理旗標字串 · Build the CLI proxy flag string a given manager understands,
    /// so callers can append it to a command line. winget 用 --proxy；其餘盡力而為（未必支援嘅就回空）。
    /// winget uses --proxy; others are best-effort (returns "" where the manager has no proxy flag).
    /// </summary>
    public static string ProxyArgsFor(string managerKey)
    {
        var url = ProxyUrl;
        if (string.IsNullOrWhiteSpace(url)) return "";

        // 將帳密塞入 URL（如有）· Fold credentials into the URL when provided, for managers that
        // Security note: credentials are intentionally never folded into CLI arguments. Command previews,
        // diagnostics, and OS process listings must not disclose the DPAPI-protected proxy password.
        return (managerKey ?? "").ToLowerInvariant() switch
        {
            // winget 有第一方 --proxy 旗標 · winget has a first-party --proxy flag.
            "winget" => $"--proxy \"{url}\"",
            // npm / bun（食 npm 設定）· npm and Bun (which reads npm config) accept --proxy/--https-proxy.
            // Never put proxy credentials on a command line: previews, diagnostics, and process listings
            // must remain secret-free. Authenticated proxies use the manager or OS credential store.
            "npm" or "bun" => $"--proxy \"{url}\" --https-proxy \"{url}\"",
            // pip 用 --proxy · pip uses --proxy.
            "pip" => $"--proxy \"{url}\"",
            // cargo / choco / scoop / dotnet / cargo / psgallery 多數靠環境變數，CLI 旗標不一，留空。
            // The rest mostly rely on env vars; no reliable inline flag — return "".
            _ => "",
        };
    }

    // ===== 7) vcpkg =====

    /// <summary>VCPKG_ROOT 路徑 · vcpkg root directory (VCPKG_ROOT).</summary>
    public static string VcpkgRoot
    {
        get => GetRaw("vcpkgRoot", "");
        set => SetRaw("vcpkgRoot", (value ?? "").Trim());
    }

    /// <summary>vcpkg triplet（例如 x64-windows）· vcpkg triplet (e.g. x64-windows).</summary>
    public static string VcpkgTriplet
    {
        get => GetRaw("vcpkgTriplet", "");
        set => SetRaw("vcpkgTriplet", (value ?? "").Trim());
    }

    // ===== 8) Local backup · 本地備份 =====

    /// <summary>啟用本地備份 · Enable the daily local backup of the installed-package list.</summary>
    public static bool LocalBackupEnabled
    {
        get => GetBool("backupEnabled", false);
        set => SetBool("backupEnabled", value);
    }

    /// <summary>備份資料夾 · Local backup directory.</summary>
    public static string LocalBackupDir
    {
        get => GetRaw("backupDir", "");
        set => SetRaw("backupDir", (value ?? "").Trim());
    }

    /// <summary>備份檔名（不含時間戳）· Base backup file name (without timestamp).</summary>
    public static string LocalBackupFileName
    {
        get
        {
            var name = GetRaw("backupFileName", "winforge-packages");
            return string.IsNullOrWhiteSpace(name) ? "winforge-packages" : name.Trim();
        }
        set => SetRaw("backupFileName", string.IsNullOrWhiteSpace(value) ? "winforge-packages" : value.Trim());
    }

    /// <summary>備份檔名加時間戳 · Append a timestamp to the backup file name.</summary>
    public static bool BackupTimestamping
    {
        get => GetBool("backupTimestamp", true);
        set => SetBool("backupTimestamp", value);
    }
}
