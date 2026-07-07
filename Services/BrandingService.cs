using System;

namespace WinForge.Services;

/// <summary>
/// 應用程式品牌（自訂名稱）· App branding — lets the user rename the app to their own name instead of
/// the hard-coded "WinForge". Only the <b>displayed</b> brand changes; internal identifiers (the
/// %LOCALAPPDATA%\WinForge data folder, git commit text, process name, etc.) stay "WinForge" so nothing
/// breaks. Persisted via <see cref="SettingsStore"/>; raises <see cref="Changed"/> for live UI updates.
/// </summary>
public static class BrandingService
{
    public const string DefaultEn = "WinForge";
    public const string DefaultZh = "視窗調校";

    private const string KeyEn = "app.name.en";
    private const string KeyZh = "app.name.zh";

    /// <summary>品牌改變時觸發 · Raised whenever the custom name changes (for live re-render).</summary>
    public static event EventHandler? Changed;

    /// <summary>英文名稱（預設 WinForge）· The English app name.</summary>
    public static string NameEn
    {
        get => SettingsStore.Get(KeyEn, DefaultEn);
        set
        {
            SettingsStore.Set(KeyEn, string.IsNullOrWhiteSpace(value) ? DefaultEn : value.Trim());
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>中文名稱（預設 視窗調校）· The Chinese app name.</summary>
    public static string NameZh
    {
        get => SettingsStore.Get(KeyZh, DefaultZh);
        set
        {
            SettingsStore.Set(KeyZh, string.IsNullOrWhiteSpace(value) ? DefaultZh : value.Trim());
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    /// <summary>有冇自訂過 · Whether the user has set a custom name.</summary>
    public static bool IsCustom => NameEn != DefaultEn || NameZh != DefaultZh;

    /// <summary>跟語言模式嘅顯示名 · The display name for the current language mode (bilingual = "En · Zh").</summary>
    public static string Display => Loc.I.Pick(NameEn, NameZh);

    /// <summary>一次過設定兩個名再通知 · Set both names then notify once.</summary>
    public static void Set(string? en, string? zh)
    {
        SettingsStore.Set(KeyEn, string.IsNullOrWhiteSpace(en) ? DefaultEn : en!.Trim());
        SettingsStore.Set(KeyZh, string.IsNullOrWhiteSpace(zh) ? DefaultZh : zh!.Trim());
        Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>還原預設品牌 · Reset to the WinForge defaults.</summary>
    public static void Reset() => Set(DefaultEn, DefaultZh);
}
