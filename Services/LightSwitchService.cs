using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>排程模式 · Theme schedule mode.</summary>
public enum LightSwitchMode
{
    Off,            // 停用 · disabled
    FixedHours,     // 固定時間 · fixed light/dark start times
    SunsetToSunrise // 跟日出日落 · sunrise/sunset from latitude/longitude
}

/// <summary>套用範圍 · Which theme values to flip.</summary>
public enum LightSwitchScope
{
    Both,       // 應用程式同系統 · apps + system
    AppsOnly,   // 淨係應用程式 · apps only
    SystemOnly  // 淨係系統 · system only
}

/// <summary>計算出嚟嘅日出日落 · A computed sunrise/sunset pair (local minutes-of-day; -1 = polar/none).</summary>
public readonly record struct SunTimes(int SunriseMinutes, int SunsetMinutes)
{
    public bool Valid => SunriseMinutes >= 0 && SunsetMinutes >= 0;
}

/// <summary>
/// LightSwitch（PowerToys LightSwitch 嘅原生克隆）· Automatic light/dark theme switching on a schedule.
///
/// 功能 · Features:
///  - 寫入 HKCU\…\Themes\Personalize 嘅 AppsUseLightTheme / SystemUsesLightTheme（0/1），
///    並廣播 WM_SETTINGCHANGE 令改動即時生效。
///    Writes AppsUseLightTheme / SystemUsesLightTheme under HKCU and broadcasts WM_SETTINGCHANGE
///    (+ WM_THEMECHANGED) so the change applies live.
///  - 固定時間模式同日出日落模式（用標準太陽演算法由緯度／經度計算）。
///    Fixed-hours mode and sunrise/sunset mode (standard solar algorithm from latitude/longitude).
///  - 設定持久化到 SettingsStore；schtasks 背景工作令 app 關咗都照樣切換。
///    Settings persisted to SettingsStore; a schtasks background job keeps switching when the app is closed.
///
/// 所有方法都係靜態，狀態存喺 SettingsStore（key 前綴 "lightswitch."）。
/// All methods are static; state lives in SettingsStore (keys prefixed "lightswitch.").
/// </summary>
public static class LightSwitchService
{
    private const string PersonalizePath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // ── settings keys ──
    private const string KEnabled = "lightswitch.enabled";
    private const string KMode = "lightswitch.mode";          // off|fixed|sun
    private const string KScope = "lightswitch.scope";        // both|apps|system
    private const string KLightTime = "lightswitch.lightTime"; // minutes-of-day
    private const string KDarkTime = "lightswitch.darkTime";   // minutes-of-day
    private const string KLat = "lightswitch.latitude";
    private const string KLon = "lightswitch.longitude";
    private const string KSunriseOff = "lightswitch.sunriseOffset"; // minutes
    private const string KSunsetOff = "lightswitch.sunsetOffset";   // minutes

    public const string TaskName = "WinForge LightSwitch";

    // ───────────────────────── interop ─────────────────────────

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);

    private static readonly IntPtr HWND_BROADCAST = new(0xffff);
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint WM_THEMECHANGED = 0x031A;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private static void BroadcastThemeChange()
    {
        // ImmersiveColorSet is the documented signal apps listen for to re-read the theme.
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet",
            SMTO_ABORTIFHUNG, 5000, out _);
        SendMessageTimeout(HWND_BROADCAST, WM_THEMECHANGED, IntPtr.Zero, "", SMTO_ABORTIFHUNG, 5000, out _);
    }

    // ───────────────────────── current theme (registry) ─────────────────────────

    /// <summary>而家應用程式係咪淺色 · Is the apps theme currently light? (default: light)</summary>
    public static bool AppsUseLight()
        => ReadDword("AppsUseLightTheme", 1) == 1;

    /// <summary>而家系統係咪淺色 · Is the system theme currently light? (default: light)</summary>
    public static bool SystemUsesLight()
        => ReadDword("SystemUsesLightTheme", 1) == 1;

    private static int ReadDword(string name, int fallback)
    {
        var v = RegistryHelper.GetValue(RegRoot.HKCU, PersonalizePath, name);
        return v is int i ? i : fallback;
    }

    /// <summary>
    /// 套用淺／深色主題（按範圍）· Apply light/dark theme to the chosen scope and broadcast the change live.
    /// </summary>
    public static void ApplyTheme(bool light, LightSwitchScope scope)
    {
        int val = light ? 1 : 0;
        bool wroteSystemLight = false;

        if (scope is LightSwitchScope.Both or LightSwitchScope.AppsOnly)
            RegistryHelper.SetValue(RegRoot.HKCU, PersonalizePath, "AppsUseLightTheme", val, RegistryValueKind.DWord);

        if (scope is LightSwitchScope.Both or LightSwitchScope.SystemOnly)
        {
            RegistryHelper.SetValue(RegRoot.HKCU, PersonalizePath, "SystemUsesLightTheme", val, RegistryValueKind.DWord);
            wroteSystemLight = light;
        }

        // Going light: reset ColorPrevalence to default, matching PowerToys behaviour (otherwise a
        // coloured taskbar/title-bar can linger after the switch to light).
        if (wroteSystemLight)
            RegistryHelper.SetValue(RegRoot.HKCU, PersonalizePath, "ColorPrevalence", 0, RegistryValueKind.DWord);

        BroadcastThemeChange();

        // Opt-in display profiles are applied after the theme transition.
        // 只會喺用家選咗設定檔之後先於主題轉換後套用螢幕設定。
        try { PowerDisplayService.ApplyThemeProfile(light); }
        catch { /* a DDC/CI failure must never block a theme transition */ }
    }

    /// <summary>即刻切去淺色 · Manually switch to light now (uses the saved scope).</summary>
    public static TweakResult SwitchToLightNow()
    {
        ApplyTheme(true, Scope);
        return TweakResult.Ok("Switched to light theme.", "已切換到淺色主題。");
    }

    /// <summary>即刻切去深色 · Manually switch to dark now (uses the saved scope).</summary>
    public static TweakResult SwitchToDarkNow()
    {
        ApplyTheme(false, Scope);
        return TweakResult.Ok("Switched to dark theme.", "已切換到深色主題。");
    }

    // ───────────────────────── persisted settings ─────────────────────────

    public static bool Enabled
    {
        get => SettingsStore.Get(KEnabled, "false") == "true";
        set => SettingsStore.Set(KEnabled, value ? "true" : "false");
    }

    public static LightSwitchMode Mode
    {
        get => SettingsStore.Get(KMode, "off") switch
        {
            "fixed" => LightSwitchMode.FixedHours,
            "sun" => LightSwitchMode.SunsetToSunrise,
            _ => LightSwitchMode.Off,
        };
        set => SettingsStore.Set(KMode, value switch
        {
            LightSwitchMode.FixedHours => "fixed",
            LightSwitchMode.SunsetToSunrise => "sun",
            _ => "off",
        });
    }

    public static LightSwitchScope Scope
    {
        get => SettingsStore.Get(KScope, "both") switch
        {
            "apps" => LightSwitchScope.AppsOnly,
            "system" => LightSwitchScope.SystemOnly,
            _ => LightSwitchScope.Both,
        };
        set => SettingsStore.Set(KScope, value switch
        {
            LightSwitchScope.AppsOnly => "apps",
            LightSwitchScope.SystemOnly => "system",
            _ => "both",
        });
    }

    /// <summary>淺色開始時間（自午夜起嘅分鐘，預設 07:00）· Light-start time, minutes-of-day (default 07:00).</summary>
    public static int LightTimeMinutes
    {
        get => GetInt(KLightTime, 7 * 60);
        set => SettingsStore.Set(KLightTime, Clamp(value, 0, 1439).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>深色開始時間（分鐘，預設 19:00）· Dark-start time, minutes-of-day (default 19:00).</summary>
    public static int DarkTimeMinutes
    {
        get => GetInt(KDarkTime, 19 * 60);
        set => SettingsStore.Set(KDarkTime, Clamp(value, 0, 1439).ToString(CultureInfo.InvariantCulture));
    }

    public static string Latitude
    {
        get => SettingsStore.Get(KLat, "");
        set => SettingsStore.Set(KLat, value.Trim());
    }

    public static string Longitude
    {
        get => SettingsStore.Get(KLon, "");
        set => SettingsStore.Set(KLon, value.Trim());
    }

    /// <summary>日出偏移（分鐘，可負）· Minutes to add to computed sunrise (may be negative).</summary>
    public static int SunriseOffset
    {
        get => GetInt(KSunriseOff, 0);
        set => SettingsStore.Set(KSunriseOff, Clamp(value, -720, 720).ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>日落偏移（分鐘，可負）· Minutes to add to computed sunset (may be negative).</summary>
    public static int SunsetOffset
    {
        get => GetInt(KSunsetOff, 0);
        set => SettingsStore.Set(KSunsetOff, Clamp(value, -720, 720).ToString(CultureInfo.InvariantCulture));
    }

    private static int GetInt(string key, int fallback)
        => int.TryParse(SettingsStore.Get(key, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

    // ───────────────────────── coordinates ─────────────────────────

    public static bool CoordinatesValid(string lat, string lon)
    {
        if (!double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var la)) return false;
        if (!double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)) return false;
        if (la == 0 && lo == 0) return false; // null-island guard, matches the reference
        return la is >= -90 and <= 90 && lo is >= -180 and <= 180;
    }

    /// <summary>有冇有效座標 · Are the saved coordinates usable?</summary>
    public static bool HasValidCoordinates() => CoordinatesValid(Latitude, Longitude);

    // ───────────────────────── sunrise / sunset (solar algorithm) ─────────────────────────

    private const double Deg2Rad = Math.PI / 180.0;
    private const double Rad2Deg = 180.0 / Math.PI;

    /// <summary>
    /// 計算指定日期、座標嘅本地日出日落時間（分鐘）· Sunrise/sunset (local minutes-of-day) for a date+location.
    /// 用同 PowerToys LightSwitch 一樣嘅「Sunrise/Sunset Algorithm」（Almanac for Computers, 1990），
    /// 用本機時區把 UTC 轉成本地時間。 Polar day/night returns -1 for the affected boundary.
    /// </summary>
    public static SunTimes CalculateSunriseSunset(double latitude, double longitude, DateTime localDate)
    {
        const double zenith = 90.833; // official sunrise/sunset (with refraction)
        int year = localDate.Year, month = localDate.Month, day = localDate.Day;

        int N1 = (int)Math.Floor(275.0 * month / 9.0);
        int N2 = (int)Math.Floor((month + 9.0) / 12.0);
        int N3 = (int)Math.Floor(1.0 + Math.Floor((year - 4.0 * Math.Floor(year / 4.0) + 2.0) / 3.0));
        int N = N1 - (N2 * N3) + day - 30;

        double CalcUt(bool sunrise)
        {
            double lngHour = longitude / 15.0;
            double t = sunrise ? N + ((6 - lngHour) / 24.0) : N + ((18 - lngHour) / 24.0);

            double M = (0.9856 * t) - 3.289;
            double L = M + (1.916 * Math.Sin(Deg2Rad * M)) + (0.020 * Math.Sin(2 * Deg2Rad * M)) + 282.634;
            L = Mod360(L);

            double RA = Rad2Deg * Math.Atan(0.91764 * Math.Tan(Deg2Rad * L));
            RA = Mod360(RA);

            double Lquadrant = Math.Floor(L / 90.0) * 90.0;
            double RAquadrant = Math.Floor(RA / 90.0) * 90.0;
            RA = (RA + (Lquadrant - RAquadrant)) / 15.0;

            double sinDec = 0.39782 * Math.Sin(Deg2Rad * L);
            double cosDec = Math.Cos(Math.Asin(sinDec));

            double cosH = (Math.Cos(Deg2Rad * zenith) - (sinDec * Math.Sin(Deg2Rad * latitude)))
                          / (cosDec * Math.Cos(Deg2Rad * latitude));
            if (cosH is > 1 or < -1) return -1; // sun never rises / never sets at this latitude on this day

            double H = sunrise ? 360 - (Rad2Deg * Math.Acos(cosH)) : Rad2Deg * Math.Acos(cosH);
            H /= 15.0;

            double T = H + RA - (0.06571 * t) - 6.622;
            double UT = T - lngHour;
            while (UT < 0) UT += 24;
            while (UT >= 24) UT -= 24;
            return UT;
        }

        double riseUt = CalcUt(true);
        double setUt = CalcUt(false);

        int rise = riseUt < 0 ? -1 : UtToLocalMinutes(riseUt, localDate);
        int set = setUt < 0 ? -1 : UtToLocalMinutes(setUt, localDate);
        return new SunTimes(rise, set);
    }

    private static double Mod360(double v)
    {
        while (v < 0) v += 360;
        while (v > 360) v -= 360;
        return v;
    }

    private static int UtToLocalMinutes(double ut, DateTime localDate)
    {
        // Convert the UT hour-of-day to local using the OS time-zone offset for that date
        // (TimeZoneInfo accounts for DST automatically).
        var offset = TimeZoneInfo.Local.GetUtcOffset(localDate.Date);
        double local = ut + offset.TotalHours;
        while (local < 0) local += 24;
        while (local >= 24) local -= 24;
        int hour = (int)local;
        int minute = (int)Math.Round((local - hour) * 60);
        if (minute == 60) { minute = 0; hour = (hour + 1) % 24; }
        return hour * 60 + minute;
    }

    /// <summary>今日嘅日出日落（用儲存咗嘅座標）· Today's sunrise/sunset using the saved coordinates.</summary>
    public static SunTimes? TodaySunTimes()
    {
        if (!CoordinatesValid(Latitude, Longitude)) return null;
        double la = double.Parse(Latitude, CultureInfo.InvariantCulture);
        double lo = double.Parse(Longitude, CultureInfo.InvariantCulture);
        return CalculateSunriseSunset(la, lo, DateTime.Now);
    }

    // ───────────────────────── schedule evaluation ─────────────────────────

    /// <summary>
    /// 依家應唔應該係淺色（按目前模式）· Given the current settings, should the theme be light right now?
    /// 回傳 null 表示無法決定（例如模式關閉，或日出日落座標無效）。
    /// Returns null when undecidable (mode off, or sun mode with invalid coords/polar day).
    /// </summary>
    public static bool? ShouldBeLightNow(DateTime now)
    {
        int lightStart, darkStart;
        switch (Mode)
        {
            case LightSwitchMode.FixedHours:
                lightStart = LightTimeMinutes;
                darkStart = DarkTimeMinutes;
                break;

            case LightSwitchMode.SunsetToSunrise:
                var sun = TodaySunTimes();
                if (sun is not { Valid: true } s) return null;
                lightStart = s.SunriseMinutes + SunriseOffset;
                darkStart = s.SunsetMinutes + SunsetOffset;
                break;

            default:
                return null;
        }

        return ShouldBeLight(now.Hour * 60 + now.Minute, Wrap(lightStart), Wrap(darkStart));
    }

    private static int Wrap(int m)
    {
        m %= 1440;
        return m < 0 ? m + 1440 : m;
    }

    /// <summary>
    /// 由「淺色開始」同「深色開始」分鐘決定而家係咪淺色（處理跨午夜）。
    /// Decide light vs dark from the light-start and dark-start minutes, handling midnight wrap.
    /// </summary>
    public static bool ShouldBeLight(int nowMinutes, int lightStart, int darkStart)
    {
        if (lightStart == darkStart) return true; // degenerate → treat as always light
        if (lightStart < darkStart)
            // light period sits within the same day: [light, dark)
            return nowMinutes >= lightStart && nowMinutes < darkStart;
        // light period wraps past midnight: light at >= lightStart OR < darkStart
        return nowMinutes >= lightStart || nowMinutes < darkStart;
    }

    /// <summary>
    /// 評估並（如需要）套用主題 · Evaluate the schedule and apply the theme if it differs. Returns true if applied.
    /// </summary>
    public static bool EvaluateAndApply(DateTime now)
    {
        if (!Enabled) return false;
        var should = ShouldBeLightNow(now);
        if (should is not bool light) return false;

        bool appsWrong = Scope is LightSwitchScope.Both or LightSwitchScope.AppsOnly && AppsUseLight() != light;
        bool sysWrong = Scope is LightSwitchScope.Both or LightSwitchScope.SystemOnly && SystemUsesLight() != light;
        if (!appsWrong && !sysWrong) return false;

        ApplyTheme(light, Scope);
        return true;
    }

    // ───────────────────────── headless apply (CLI / schtasks) ─────────────────────────

    /// <summary>
    /// 無頭套用：畀 "--apply-theme" 命令列同 schtasks 背景工作呼叫。
    /// Headless one-shot used by the "--apply-theme" CLI flag and the schtasks background job.
    /// Evaluates the saved schedule and writes the theme if needed; never throws.
    /// </summary>
    public static void RunHeadlessApply()
    {
        try { EvaluateAndApply(DateTime.Now); }
        catch { /* best effort — runs unattended */ }
    }

    // ───────────────────────── schtasks background job ─────────────────────────

    /// <summary>
    /// 排定背景切換工作（每分鐘檢查一次）· Register a background schtasks job that runs
    /// WinForge --apply-theme every minute so theme switching works even when the app is closed.
    /// Re-creates a single task, so scheduling is idempotent.
    /// </summary>
    public static async Task<TweakResult> ScheduleBackgroundJob(CancellationToken ct = default)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "WinForge.exe");
        var tr = $"\\\"{exe}\\\" --apply-theme";
        // /SC MINUTE /MO 1 → run every minute; /RL LIMITED → no elevation needed for HKCU writes.
        var args = $"/Create /SC MINUTE /MO 1 /TN \"{TaskName}\" /TR \"{tr}\" /RL LIMITED /F";
        var r = await ShellRunner.Run("schtasks.exe", args, elevated: false, ct);
        return r.Success
            ? TweakResult.Ok("Background theme switching scheduled (checks every minute).",
                "已排定背景主題切換（每分鐘檢查一次）。", r.Output)
            : TweakResult.Fail("Could not schedule the background task.", "背景排程失敗。", r.Output);
    }

    /// <summary>取消背景切換工作 · Remove the background theme-switching task.</summary>
    public static async Task<TweakResult> UnscheduleBackgroundJob(CancellationToken ct = default)
    {
        var r = await ShellRunner.Run("schtasks.exe", $"/Delete /TN \"{TaskName}\" /F", elevated: false, ct);
        return r.Success
            ? TweakResult.Ok("Background theme-switching task removed.", "已移除背景主題切換工作。", r.Output)
            : TweakResult.Fail("No background task to remove (or it failed).", "冇背景工作可以移除（或者失敗）。", r.Output);
    }

    public static async Task<bool> IsBackgroundJobScheduled(CancellationToken ct = default)
    {
        var r = await ShellRunner.Run("schtasks.exe", $"/Query /TN \"{TaskName}\"", elevated: false, ct);
        return r.Success;
    }

    // ───────────────────────── IP geolocation (best-effort) ─────────────────────────

    /// <summary>
    /// 用 IP 估算座標（盡力而為）· Best-effort latitude/longitude from IP geolocation (ip-api.com).
    /// 回傳 (lat, lon) 字串，失敗就回傳 null。 Returns (lat, lon) strings, or null on failure.
    /// </summary>
    public static async Task<(string lat, string lon, string place)?> DetectLocationByIpAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var json = await http.GetStringAsync("http://ip-api.com/json/?fields=status,lat,lon,city,country", ct);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("status", out var st) && st.GetString() != "success") return null;
            if (!root.TryGetProperty("lat", out var latEl) || !root.TryGetProperty("lon", out var lonEl)) return null;
            string lat = latEl.GetDouble().ToString(CultureInfo.InvariantCulture);
            string lon = lonEl.GetDouble().ToString(CultureInfo.InvariantCulture);
            string city = root.TryGetProperty("city", out var c) ? c.GetString() ?? "" : "";
            string country = root.TryGetProperty("country", out var co) ? co.GetString() ?? "" : "";
            string place = string.Join(", ", System.Linq.Enumerable.Where(
                new[] { city, country }, x => !string.IsNullOrWhiteSpace(x)));
            return (lat, lon, place);
        }
        catch { return null; }
    }
}
