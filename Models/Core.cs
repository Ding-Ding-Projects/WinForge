using System;
using System.Collections.Generic;

namespace WinForge.Models;

/// <summary>應用程式語言 · Application language.</summary>
public enum AppLanguage
{
    Bilingual,
    Cantonese,
    English,
}

/// <summary>調校項目嘅種類 · The kind of tweak / control surface.</summary>
public enum TweakKind
{
    Toggle,     // 開關 · on/off switch backed by a setting
    Action,     // 一次性動作 · one-shot command/button
    Choice,     // 多選一 · select one of several options
    Info,       // 唯讀資訊 · read-only information value

    // ---- Rich interactive kinds (foundation upgrade) · 進階互動種類（基礎升級） ----
    Slider,     // 數值滑桿（含 Min/Max/Step/單位）· numeric Slider with min/max/step/unit + live label
    Number,     // 數字輸入框 · NumberBox numeric entry
    RadioGroup, // 單選按鈕組（重用 Choices）· RadioButtons group (reuses Choices)
    MultiCheck, // 多個獨立勾選 · vertical list of independent CheckBoxes
    Color,      // 顏色（色板＋ColorPicker＋hex）· colour swatch + ColorPicker flyout + hex box
    DateKind,   // 日期／時間 · DatePicker (+ optional TimePicker)
    Wizard,     // 多步驟精靈對話框 · multi-step ContentDialog wizard
}

/// <summary>
/// 彩色狀態標籤嘅顏色 · Colour bucket for a coloured status pill.
/// </summary>
public enum StatusColor
{
    Neutral, // 中性（灰）· neutral / informational (grey)
    Good,    // 良好（綠）· good / healthy (green)
    Warn,    // 警告（黃）· warning (yellow/amber)
    Bad,     // 不良（紅）· bad / error (red)
}

/// <summary>
/// 精靈每一步可帶嘅輸入種類 · The kind of input a wizard step collects.
/// </summary>
public enum WizardInputKind
{
    None,   // 純說明，無輸入 · description only, no input
    Text,   // 文字 · free text
    Number, // 數字 · numeric
    Choice, // 多選一 · pick one option
    Toggle, // 開關 · boolean on/off
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

    public string Get(AppLanguage lang) => lang switch
    {
        AppLanguage.Bilingual => Services.Loc.Both(En, Zh),
        AppLanguage.Cantonese => Zh,
        _ => En,
    };

    /// <summary>主要文字 · Primary text for the user's display mode.</summary>
    public string Primary => Services.Loc.I.Language == AppLanguage.Cantonese ? Zh : En;

    /// <summary>次要文字；只喺雙語模式顯示 · Secondary text, shown only in bilingual mode.</summary>
    public string Secondary => Services.Loc.I.IsBilingual ? Zh : "";

    public static implicit operator LocalizedText((string en, string zh) t) => new(t.en, t.zh);

    public override string ToString() => $"{En} · {Zh}";
}

/// <summary>多選一其中一個選項 · A single option for a Choice tweak.</summary>
public sealed record TweakChoice(LocalizedText Label, string Value);

/// <summary>
/// 多重勾選清單入面嘅一個子項 · One sub-option in a MultiCheck tweak.
/// 每個子項自帶讀／寫一個布林值嘅行為 · each item carries its own get/set for one boolean.
/// </summary>
public sealed record TweakToggleItem(LocalizedText Label, Func<bool> Get, Action<bool> Set);

/// <summary>
/// 精靈嘅其中一步 · A single step in a <see cref="TweakKind.Wizard"/>.
/// 帶雙語標題／說明，可選一個輸入控件；收集到嘅值會用 <see cref="Key"/> 放入字典。
/// Carries a bilingual title/description and an optional input; collected values are keyed by <see cref="Key"/>.
/// </summary>
public sealed record WizardStep
{
    /// <summary>步驟標題 · Step title (bilingual).</summary>
    public required LocalizedText Title { get; init; }

    /// <summary>步驟說明 · Step description (bilingual).</summary>
    public required LocalizedText Description { get; init; }

    /// <summary>輸入種類 · Which input control (if any) this step shows.</summary>
    public WizardInputKind Input { get; init; } = WizardInputKind.None;

    /// <summary>結果字典入面嘅鍵 · Key under which this step's value is stored in the finish dictionary.</summary>
    public string Key { get; init; } = "";

    /// <summary>輸入預設值（字串形式）· Default value for the input, as a string.</summary>
    public string? Default { get; init; }

    /// <summary>當 <see cref="Input"/> 係 Choice 時嘅選項 · Options when <see cref="Input"/> is Choice.</summary>
    public IReadOnlyList<TweakChoice>? Choices { get; init; }
}

/// <summary>動作執行結果 · Result of running an action.</summary>
public sealed record TweakResult(bool Success, LocalizedText? Message = null, string? Output = null)
{
    public static TweakResult Ok(string en, string zh, string? output = null)
        => new(true, new LocalizedText(en, zh), output);

    public static TweakResult Fail(string en, string zh, string? output = null)
        => new(false, new LocalizedText(en, zh), output);
}
