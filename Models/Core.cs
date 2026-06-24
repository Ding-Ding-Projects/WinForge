namespace WinForge.Models;

/// <summary>應用程式語言 · Application language.</summary>
public enum AppLanguage
{
    English,
    Cantonese,
}

/// <summary>調校項目嘅種類 · The kind of tweak / control surface.</summary>
public enum TweakKind
{
    Toggle, // 開關 · on/off switch backed by a setting
    Action, // 一次性動作 · one-shot command/button
    Choice, // 多選一 · select one of several options
    Info,   // 唯讀資訊 · read-only information value
}

/// <summary>套用之後需要嘅重啟範圍 · Restart scope required for a change to take effect.</summary>
public enum RestartScope
{
    None,
    Explorer, // 重啟檔案總管 · restart explorer.exe
    SignOut,  // 登出 · sign out
    Reboot,   // 重新開機 · reboot
}

/// <summary>
/// 雙語文字：永遠同時持有英文同粵語。
/// Bilingual text holder: always carries both English and Cantonese.
/// </summary>
public sealed class LocalizedText
{
    public string En { get; }
    public string Zh { get; }

    public LocalizedText(string en, string zh)
    {
        En = en;
        Zh = zh;
    }

    public string Get(AppLanguage lang) => lang == AppLanguage.Cantonese ? Zh : En;

    /// <summary>主要語言（跟使用者選擇）· Primary language per the user's choice.</summary>
    public string Primary => Get(Services.Loc.I.Language);

    /// <summary>次要語言（另一種，永遠顯示）· Secondary language, always shown alongside.</summary>
    public string Secondary => Get(Services.Loc.I.Other);

    public static implicit operator LocalizedText((string en, string zh) t) => new(t.en, t.zh);

    public override string ToString() => $"{En} · {Zh}";
}

/// <summary>多選一其中一個選項 · A single option for a Choice tweak.</summary>
public sealed record TweakChoice(LocalizedText Label, string Value);

/// <summary>動作執行結果 · Result of running an action.</summary>
public sealed record TweakResult(bool Success, LocalizedText? Message = null, string? Output = null)
{
    public static TweakResult Ok(string en, string zh, string? output = null)
        => new(true, new LocalizedText(en, zh), output);

    public static TweakResult Fail(string en, string zh, string? output = null)
        => new(false, new LocalizedText(en, zh), output);
}
