using System;

namespace WinForge.Models;

/// <summary>
/// 指令面板嘅單一搜尋結果 · One ranked result shown in the Command Palette launcher.
/// 每個結果都有圖示、雙語標題／副題、相關性分數，同一個觸發動作。
/// Each result carries an icon glyph, a bilingual title/subtitle, a relevance score and an action.
/// </summary>
public sealed class CommandPaletteResult
{
    /// <summary>主標題（單行；已經係雙語或本身就係結果名）· Primary title (already localized or a raw name).</summary>
    public string Title { get; init; } = "";

    /// <summary>副題（路徑、提示、來源）· Secondary line — path, hint or provider name.</summary>
    public string Subtitle { get; init; } = "";

    /// <summary>Segoe Fluent Icons 字形 · A Segoe Fluent Icons glyph for the result.</summary>
    public string Glyph { get; init; } = "";

    /// <summary>來源提供者標籤（雙語），顯示喺右邊 · The provider's short bilingual tag, shown on the right.</summary>
    public string ProviderTag { get; init; } = "";

    /// <summary>相關性分數（越高越前）· Relevance score; higher sorts first.</summary>
    public double Score { get; set; }

    /// <summary>選中後執行嘅動作；回傳 true = 之後關閉面板 · Action to run; returning true closes the palette.</summary>
    public Func<bool> Invoke { get; init; } = () => true;
}
