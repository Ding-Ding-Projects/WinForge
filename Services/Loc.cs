using System;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 全域語言狀態 · Global language state.
/// 兩種語言永遠同時顯示喺介面上；呢度只係決定邊個係「主要」。
/// Both languages are always shown in the UI; this only decides which is "primary".
/// </summary>
public sealed class Loc
{
    public static Loc I { get; } = new();

    private AppLanguage _language;

    private Loc()
    {
        _language = SettingsStore.Get("language", "English") == "Cantonese"
            ? AppLanguage.Cantonese
            : AppLanguage.English;
    }

    /// <summary>主要語言 · The primary language.</summary>
    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            SettingsStore.Set("language", value.ToString());
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>另一種語言 · The other (secondary) language.</summary>
    public AppLanguage Other => _language == AppLanguage.English ? AppLanguage.Cantonese : AppLanguage.English;

    public bool IsCantonesePrimary => _language == AppLanguage.Cantonese;

    /// <summary>語言改變時通知 UI 重繪 · Raised when the primary language changes.</summary>
    public event EventHandler? LanguageChanged;

    public void Toggle() =>
        Language = _language == AppLanguage.English ? AppLanguage.Cantonese : AppLanguage.English;

    /// <summary>快捷雙語選擇 · Pick a string in the current primary language.</summary>
    public string Pick(string en, string zh) => _language == AppLanguage.Cantonese ? zh : en;
}
