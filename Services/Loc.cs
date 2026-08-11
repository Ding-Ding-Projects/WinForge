using System;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 全域語言狀態 · Global language state.
/// 支援雙語、純粵語、純英文顯示 · Supports bilingual, Cantonese-only and English-only display.
/// </summary>
public sealed class Loc
{
    public static Loc I { get; } = new();

    private AppLanguage _language;
    private AppLanguage? _scheduledOverride;

    private Loc()
    {
        var saved = SettingsStore.Get("language", nameof(AppLanguage.Bilingual));
        _language = Enum.TryParse<AppLanguage>(saved, ignoreCase: true, out var parsed)
            ? parsed
            : AppLanguage.Bilingual;
    }

    /// <summary>語言顯示模式 · Language display mode.</summary>
    public AppLanguage Language
    {
        get => _scheduledOverride ?? _language;
        set
        {
            if (_language == value) return;
            _language = value;
            SettingsStore.Set("language", value.ToString());
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// The persisted user choice, without a temporary scheduled override.
    /// The schedule layer uses this so a temporary language never overwrites the base profile.
    /// </summary>
    public AppLanguage BaseLanguage => _language;

    /// <summary>
    /// Apply or clear a temporary scheduled language without writing the base setting.
    /// </summary>
    public void SetScheduledOverride(AppLanguage? language)
    {
        if (_scheduledOverride == language) return;
        _scheduledOverride = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>另一種語言；雙語模式用粵語做第二行 · The secondary language; bilingual mode uses Cantonese second.</summary>
    public AppLanguage Other => Language == AppLanguage.Cantonese ? AppLanguage.English : AppLanguage.Cantonese;

    public bool IsBilingual => Language == AppLanguage.Bilingual;
    public bool IsCantonesePrimary => Language == AppLanguage.Cantonese;

    /// <summary>語言改變時通知 UI 重繪 · Raised when the language mode changes.</summary>
    public event EventHandler? LanguageChanged;

    public void Toggle() =>
        Language = Language switch
        {
            AppLanguage.Bilingual => AppLanguage.Cantonese,
            AppLanguage.Cantonese => AppLanguage.English,
            _ => AppLanguage.Bilingual,
        };

    /// <summary>顯示目前語言模式嘅文字 · Format a string for the current language mode.</summary>
    public string Pick(string en, string zh) => Language switch
    {
        AppLanguage.Bilingual => Both(en, zh),
        AppLanguage.Cantonese => zh,
        _ => en,
    };

    public string Pick(LocalizedText text) => text.Get(Language);

    /// <summary>需要單一語言值時使用，例如 culture name · Use when an API needs one language value, such as a culture name.</summary>
    public string PickSingle(string en, string zh) => Language == AppLanguage.Cantonese ? zh : en;

    public static string Both(string en, string zh)
    {
        en ??= "";
        zh ??= "";
        if (string.IsNullOrWhiteSpace(en)) return zh;
        if (string.IsNullOrWhiteSpace(zh)) return en;
        if (string.Equals(en, zh, StringComparison.Ordinal)) return en;
        if (ContainsCjk(en)) return en;
        return $"{en} · {zh}";
    }

    private static bool ContainsCjk(string value)
    {
        foreach (var ch in value)
        {
            if ((ch >= '\u3400' && ch <= '\u4DBF') ||
                (ch >= '\u4E00' && ch <= '\u9FFF') ||
                (ch >= '\uF900' && ch <= '\uFAFF'))
                return true;
        }
        return false;
    }
}
