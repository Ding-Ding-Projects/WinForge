using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

// 快速重音符：字元對應資料 · Quick Accent character-mapping data.
// 由 PowerToys Quick Accent (poweraccent) 移植，純 C# · Ported from PowerToys Quick Accent, pure C#.

/// <summary>
/// 一個語言／符號集嘅分組 · The kind of accent set (used to group the settings list).
/// </summary>
public enum AccentGroup
{
    Language, // 口語語言 · spoken language
    Special,  // 符號／非語言集 · symbols / non-language sets
}

/// <summary>
/// 一個重音符語言集（識別碼、群組、字元對應） · One accent set: id, group and its letter → variants map.
/// </summary>
public sealed class AccentLanguage
{
    public string Id { get; }            // 穩定識別碼（存設定用）· stable id (persisted)
    public string En { get; }            // 英文名 · English display name
    public string Zh { get; }            // 中文名 · Chinese display name
    public AccentGroup Group { get; }
    public IReadOnlyDictionary<int, string[]> Map { get; } // VK code → variants

    public AccentLanguage(string id, string en, string zh, AccentGroup group, Dictionary<int, string[]> map)
    {
        Id = id; En = en; Zh = zh; Group = group; Map = map;
    }
}

/// <summary>
/// 重音符資料嘅唯一來源 · The single source of truth for all Quick Accent character data.
/// 提供：每個語言集嘅字元對應、揀字邏輯、按使用排序快取。
/// Provides per-set letter maps plus a combined lookup across the user's selected sets.
/// </summary>
public static class QuickAccentData
{
    // 虛擬鍵碼（同 PowerToys LetterKey 對齊）· Virtual-key codes (aligned to PowerToys LetterKey).
    public const int VK_0 = 0x30, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33, VK_4 = 0x34;
    public const int VK_5 = 0x35, VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39;
    public const int VK_A = 0x41, VK_B = 0x42, VK_C = 0x43, VK_D = 0x44, VK_E = 0x45, VK_F = 0x46;
    public const int VK_G = 0x47, VK_H = 0x48, VK_I = 0x49, VK_J = 0x4A, VK_K = 0x4B, VK_L = 0x4C;
    public const int VK_M = 0x4D, VK_N = 0x4E, VK_O = 0x4F, VK_P = 0x50, VK_Q = 0x51, VK_R = 0x52;
    public const int VK_S = 0x53, VK_T = 0x54, VK_U = 0x55, VK_V = 0x56, VK_W = 0x57, VK_X = 0x58;
    public const int VK_Y = 0x59, VK_Z = 0x5A;
    public const int VK_PLUS = 0xBB, VK_COMMA = 0xBC, VK_PERIOD = 0xBE, VK_MINUS = 0xBD;
    public const int VK_MULTIPLY = 0x6A, VK_SLASH = 0xBF, VK_DIVIDE = 0x6F, VK_BACKSLASH = 0xDC;

    /// <summary>所有可以觸發重音符嘅基底鍵 · Every base key that can trigger Quick Accent.</summary>
    public static readonly int[] Letters =
    {
        VK_0, VK_1, VK_2, VK_3, VK_4, VK_5, VK_6, VK_7, VK_8, VK_9,
        VK_A, VK_B, VK_C, VK_D, VK_E, VK_F, VK_G, VK_H, VK_I, VK_J, VK_K, VK_L, VK_M,
        VK_N, VK_O, VK_P, VK_Q, VK_R, VK_S, VK_T, VK_U, VK_V, VK_W, VK_X, VK_Y, VK_Z,
        VK_PLUS, VK_COMMA, VK_PERIOD, VK_MINUS, VK_SLASH, VK_DIVIDE, VK_MULTIPLY, VK_BACKSLASH,
    };

    private static readonly HashSet<int> _letterSet = new(Letters);

    public static bool IsLetterKey(int vk) => _letterSet.Contains(vk);

    /// <summary>
    /// 所有語言／符號集 · Canonical registry of every accent set (ported from PowerToys).
    /// </summary>
    public static readonly IReadOnlyList<AccentLanguage> All = new List<AccentLanguage>
    {
        new("SPECIAL", "Special / Symbols", "特殊符號", AccentGroup.Special, new()
        {
            [VK_0] = new[] { "₀", "⁰", "°", "↉", "₎", "⁾" },
            [VK_1] = new[] { "₁", "¹", "½", "⅓", "¼", "⅕", "⅙", "⅐", "⅛", "⅑", "⅒" },
            [VK_2] = new[] { "₂", "²", "⅔", "⅖" },
            [VK_3] = new[] { "₃", "³", "¾", "⅗", "⅜" },
            [VK_4] = new[] { "₄", "⁴", "⅘" },
            [VK_5] = new[] { "₅", "⁵", "⅚", "⅝" },
            [VK_6] = new[] { "₆", "⁶" },
            [VK_7] = new[] { "₇", "⁷", "⅞" },
            [VK_8] = new[] { "₈", "⁸", "∞" },
            [VK_9] = new[] { "₉", "⁹", "₍", "⁽" },
            [VK_A] = new[] { "ȧ", "ǽ", "∀", "ᵃ", "ₐ" },
            [VK_B] = new[] { "ḃ", "ᵇ" },
            [VK_C] = new[] { "ċ", "°C", "©", "ℂ", "∁", "ᶜ" },
            [VK_D] = new[] { "ḍ", "ḋ", "∂", "ᵈ" },
            [VK_E] = new[] { "∈", "∃", "∄", "∉", "ĕ", "ᵉ", "ₑ" },
            [VK_F] = new[] { "ḟ", "°F", "ᶠ" },
            [VK_G] = new[] { "ģ", "ǧ", "ġ", "ĝ", "ǥ", "ᵍ" },
            [VK_H] = new[] { "ḣ", "ĥ", "ħ", "ʰ", "ₕ" },
            [VK_I] = new[] { "ⁱ", "ᵢ" },
            [VK_J] = new[] { "ĵ", "ʲ", "ⱼ" },
            [VK_K] = new[] { "ķ", "ǩ", "ᵏ", "ₖ" },
            [VK_L] = new[] { "ļ", "₺", "ˡ", "ₗ" },
            [VK_M] = new[] { "ṁ", "ᵐ", "ₘ" },
            [VK_N] = new[] { "ņ", "ṅ", "ⁿ", "ℕ", "№", "ₙ" },
            [VK_O] = new[] { "ȯ", "∅", "⌀", "ᵒ", "ₒ" },
            [VK_P] = new[] { "ṗ", "℗", "∏", "¶", "ᵖ", "ₚ" },
            [VK_Q] = new[] { "ℚ", "𐞥" },
            [VK_R] = new[] { "ṙ", "®", "ℝ", "ʳ", "ᵣ" },
            [VK_S] = new[] { "ṡ", "§", "∑", "∫", "ˢ", "ₛ" },
            [VK_T] = new[] { "ţ", "ṫ", "ŧ", "™", "ᵗ", "ₜ" },
            [VK_U] = new[] { "ŭ", "ᵘ", "ᵤ" },
            [VK_V] = new[] { "V̇", "ᵛ", "ᵥ" },
            [VK_W] = new[] { "ẇ", "ʷ" },
            [VK_X] = new[] { "ẋ", "×", "ˣ", "ₓ" },
            [VK_Y] = new[] { "ẏ", "ꝡ", "ʸ" },
            [VK_Z] = new[] { "ʒ", "ǯ", "ℤ", "ᶻ" },
            [VK_COMMA] = new[] { "∙", "₋", "⁻", "–", "√", "‟", "《", "》", "‛", "〈", "〉", "″", "‴", "⁗" },
            [VK_PERIOD] = new[] { "…", "⁝", "̀", "́", "̂", "̃", "̄", "̈", "̋", "̌" },
            [VK_MINUS] = new[] { "~", "‐", "‑", "‒", "–", "—", "―", "⁓", "−", "⸺", "⸻", "∓", "₋", "⁻" },
            [VK_SLASH] = new[] { "÷", "√" },
            [VK_DIVIDE] = new[] { "÷", "√" },
            [VK_MULTIPLY] = new[] { "×", "⋅", "ˣ", "ₓ" },
            [VK_PLUS] = new[] { "≤", "≥", "≠", "≈", "≙", "⊕", "⊗", "±", "≅", "≡", "₊", "⁺", "₌", "⁼" },
            [VK_BACKSLASH] = new[] { "`", "~" },
        }),

        new("CUR", "Currency", "貨幣符號", AccentGroup.Special, new()
        {
            [VK_B] = new[] { "฿", "в" },
            [VK_C] = new[] { "¢", "₡", "č" },
            [VK_D] = new[] { "₫" },
            [VK_E] = new[] { "€" },
            [VK_F] = new[] { "ƒ" },
            [VK_H] = new[] { "₴" },
            [VK_K] = new[] { "₭" },
            [VK_L] = new[] { "ł" },
            [VK_N] = new[] { "л" },
            [VK_M] = new[] { "₼" },
            [VK_P] = new[] { "£", "₽", "₱" },
            [VK_R] = new[] { "₹", "៛", "﷼" },
            [VK_S] = new[] { "$", "₪" },
            [VK_T] = new[] { "₮", "₺", "₸" },
            [VK_W] = new[] { "₩" },
            [VK_Y] = new[] { "¥" },
            [VK_Z] = new[] { "z" },
        }),

        new("IPA", "IPA (Phonetic)", "國際音標", AccentGroup.Special, new()
        {
            [VK_A] = new[] { "ɐ", "ɑ", "ɒ", "ǎ" },
            [VK_B] = new[] { "ʙ" },
            [VK_E] = new[] { "ɘ", "ɵ", "ə", "ɛ", "ɜ", "ɞ" },
            [VK_F] = new[] { "ɟ", "ɸ" },
            [VK_G] = new[] { "ɢ", "ɣ" },
            [VK_H] = new[] { "ɦ", "ʜ" },
            [VK_I] = new[] { "ɨ", "ɪ" },
            [VK_J] = new[] { "ʝ" },
            [VK_L] = new[] { "ɬ", "ɮ", "ꞎ", "ɭ", "ʎ", "ʟ", "ɺ" },
            [VK_N] = new[] { "ɳ", "ɲ", "ŋ", "ɴ" },
            [VK_O] = new[] { "ɤ", "ɔ", "ɶ", "ǒ" },
            [VK_R] = new[] { "ʁ", "ɹ", "ɻ", "ɾ", "ɽ", "ʀ" },
            [VK_S] = new[] { "ʃ", "ʂ", "ɕ" },
            [VK_U] = new[] { "ʉ", "ʊ", "ǔ" },
            [VK_V] = new[] { "ʋ", "ⱱ", "ʌ" },
            [VK_W] = new[] { "ɰ", "ɯ" },
            [VK_Y] = new[] { "ʏ" },
            [VK_Z] = new[] { "ʒ", "ʐ", "ʑ" },
            [VK_COMMA] = new[] { "ʡ", "ʔ", "ʕ", "ʢ" },
        }),

        new("CA", "Catalan", "加泰隆尼亞文", AccentGroup.Language, new()
        {
            [VK_1] = new[] { "¡" },
            [VK_A] = new[] { "à", "á" },
            [VK_C] = new[] { "ç" },
            [VK_E] = new[] { "è", "é", "€" },
            [VK_I] = new[] { "ì", "í", "ï" },
            [VK_N] = new[] { "ñ" },
            [VK_O] = new[] { "ò", "ó" },
            [VK_U] = new[] { "ù", "ú", "ü" },
            [VK_L] = new[] { "·" },
            [VK_COMMA] = new[] { "¿", "?", "¡", "!", "«", "»", "“", "”", "‘", "’" },
            [VK_SLASH] = new[] { "¿" },
        }),

        new("HR", "Croatian", "克羅地亞文", AccentGroup.Language, new()
        {
            [VK_C] = new[] { "ć", "č" },
            [VK_D] = new[] { "đ" },
            [VK_E] = new[] { "€" },
            [VK_S] = new[] { "š" },
            [VK_Z] = new[] { "ž" },
            [VK_COMMA] = new[] { "„", "“", "»", "«" },
        }),

        new("CZ", "Czech", "捷克文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "á" },
            [VK_C] = new[] { "č" },
            [VK_D] = new[] { "ď" },
            [VK_E] = new[] { "ě", "é" },
            [VK_I] = new[] { "í" },
            [VK_N] = new[] { "ň" },
            [VK_O] = new[] { "ó" },
            [VK_R] = new[] { "ř" },
            [VK_S] = new[] { "š" },
            [VK_T] = new[] { "ť" },
            [VK_U] = new[] { "ů", "ú" },
            [VK_Y] = new[] { "ý" },
            [VK_Z] = new[] { "ž" },
            [VK_COMMA] = new[] { "„", "“", "‚", "‘", "»", "«", "›", "‹" },
        }),

        new("DK", "Danish", "丹麥文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "å", "æ" },
            [VK_E] = new[] { "€" },
            [VK_O] = new[] { "ø" },
            [VK_COMMA] = new[] { "»", "«", "“", "”", "›", "‹", "‘", "’" },
        }),

        new("NL", "Dutch", "荷蘭文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "á", "à", "ä" },
            [VK_C] = new[] { "ç" },
            [VK_E] = new[] { "é", "è", "ë", "ê", "€" },
            [VK_I] = new[] { "í", "ï", "î" },
            [VK_N] = new[] { "ñ" },
            [VK_O] = new[] { "ó", "ö", "ô" },
            [VK_U] = new[] { "ú", "ü", "û" },
            [VK_COMMA] = new[] { "“", "„", "”", "‘", ",", "’" },
        }),

        new("EPO", "Esperanto", "世界語", AccentGroup.Language, new()
        {
            [VK_C] = new[] { "ĉ" },
            [VK_G] = new[] { "ĝ" },
            [VK_H] = new[] { "ĥ" },
            [VK_J] = new[] { "ĵ" },
            [VK_S] = new[] { "ŝ" },
            [VK_U] = new[] { "ŭ" },
        }),

        new("EST", "Estonian", "愛沙尼亞文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "ä" },
            [VK_E] = new[] { "€" },
            [VK_O] = new[] { "ö", "õ" },
            [VK_U] = new[] { "ü" },
            [VK_Z] = new[] { "ž" },
            [VK_S] = new[] { "š" },
            [VK_COMMA] = new[] { "„", "“", "«", "»" },
        }),

        new("FI", "Finnish", "芬蘭文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "ä", "å" },
            [VK_E] = new[] { "€" },
            [VK_O] = new[] { "ö" },
            [VK_COMMA] = new[] { "”", "’", "»" },
        }),

        new("FR", "French", "法文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "à", "â", "á", "ä", "ã", "æ" },
            [VK_C] = new[] { "ç" },
            [VK_E] = new[] { "é", "è", "ê", "ë", "€" },
            [VK_I] = new[] { "î", "ï", "í", "ì" },
            [VK_O] = new[] { "ô", "ö", "ó", "ò", "õ", "œ" },
            [VK_U] = new[] { "û", "ù", "ü", "ú" },
            [VK_Y] = new[] { "ÿ", "ý" },
            [VK_COMMA] = new[] { "«", "»", "‹", "›", "“", "”", "‘", "’" },
        }),

        new("DE", "German", "德文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "ä" },
            [VK_E] = new[] { "€" },
            [VK_O] = new[] { "ö" },
            [VK_S] = new[] { "ß" },
            [VK_U] = new[] { "ü" },
            [VK_COMMA] = new[] { "„", "“", "‚", "‘", "»", "«", "›", "‹" },
        }),

        new("EL", "Greek", "希臘文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "α", "ά" },
            [VK_B] = new[] { "β" },
            [VK_C] = new[] { "χ" },
            [VK_D] = new[] { "δ" },
            [VK_E] = new[] { "ε", "έ", "η", "ή" },
            [VK_F] = new[] { "φ" },
            [VK_G] = new[] { "γ" },
            [VK_I] = new[] { "ι", "ί" },
            [VK_K] = new[] { "κ" },
            [VK_L] = new[] { "λ" },
            [VK_M] = new[] { "μ" },
            [VK_N] = new[] { "ν" },
            [VK_O] = new[] { "ο", "ό", "ω", "ώ" },
            [VK_P] = new[] { "π", "φ", "ψ" },
            [VK_R] = new[] { "ρ" },
            [VK_S] = new[] { "σ", "ς" },
            [VK_T] = new[] { "τ", "θ", "ϑ" },
            [VK_U] = new[] { "υ", "ύ" },
            [VK_X] = new[] { "ξ" },
            [VK_Y] = new[] { "υ" },
            [VK_Z] = new[] { "ζ" },
            [VK_COMMA] = new[] { "“", "”", "«", "»" },
        }),

        new("HU", "Hungarian", "匈牙利文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "á" },
            [VK_E] = new[] { "é" },
            [VK_I] = new[] { "í" },
            [VK_O] = new[] { "ó", "ő", "ö" },
            [VK_U] = new[] { "ú", "ű", "ü" },
            [VK_Y] = new[] { "ÿ", "ý" },
            [VK_COMMA] = new[] { "„", "”", "»", "«" },
        }),

        new("IS", "Icelandic", "冰島文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "á", "æ" },
            [VK_D] = new[] { "ð" },
            [VK_E] = new[] { "é" },
            [VK_I] = new[] { "í" },
            [VK_O] = new[] { "ó", "ö" },
            [VK_U] = new[] { "ú" },
            [VK_Y] = new[] { "ý" },
            [VK_T] = new[] { "þ" },
            [VK_COMMA] = new[] { "„", "“", "‚", "‘" },
        }),

        new("IT", "Italian", "意大利文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "à" },
            [VK_E] = new[] { "è", "é", "ə", "€" },
            [VK_I] = new[] { "ì", "í" },
            [VK_O] = new[] { "ò", "ó" },
            [VK_U] = new[] { "ù", "ú" },
            [VK_COMMA] = new[] { "«", "»", "“", "”", "‘", "’" },
        }),

        new("LT", "Lithuanian", "立陶宛文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "ą" },
            [VK_C] = new[] { "č" },
            [VK_E] = new[] { "ę", "ė", "€" },
            [VK_I] = new[] { "į" },
            [VK_S] = new[] { "š" },
            [VK_U] = new[] { "ų", "ū" },
            [VK_Z] = new[] { "ž" },
            [VK_COMMA] = new[] { "„", "“", "‚", "‘" },
        }),

        new("MI", "Maori", "毛利文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "ā" },
            [VK_E] = new[] { "ē" },
            [VK_I] = new[] { "ī" },
            [VK_O] = new[] { "ō" },
            [VK_S] = new[] { "$" },
            [VK_U] = new[] { "ū" },
            [VK_COMMA] = new[] { "“", "”", "‘", "’" },
        }),

        new("NO", "Norwegian", "挪威文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "å", "æ" },
            [VK_E] = new[] { "€", "é" },
            [VK_O] = new[] { "ø" },
            [VK_S] = new[] { "$" },
            [VK_COMMA] = new[] { "«", "»", ",", "‘", "’", "„", "“" },
        }),

        new("PI", "Pinyin", "拼音", AccentGroup.Language, new()
        {
            [VK_1] = new[] { "̄", "ˉ" },
            [VK_2] = new[] { "́", "ˊ" },
            [VK_3] = new[] { "̌", "ˇ" },
            [VK_4] = new[] { "̀", "ˋ" },
            [VK_5] = new[] { "·" },
            [VK_A] = new[] { "ā", "á", "ǎ", "à", "ɑ" },
            [VK_C] = new[] { "ĉ" },
            [VK_E] = new[] { "ē", "é", "ě", "è", "ê" },
            [VK_I] = new[] { "ī", "í", "ǐ", "ì" },
            [VK_O] = new[] { "ō", "ó", "ǒ", "ò" },
            [VK_S] = new[] { "ŝ" },
            [VK_U] = new[] { "ū", "ú", "ǔ", "ù", "ü", "ǖ", "ǘ", "ǚ", "ǜ" },
            [VK_V] = new[] { "ü", "ǖ", "ǘ", "ǚ", "ǜ" },
            [VK_Y] = new[] { "¥" },
            [VK_Z] = new[] { "ẑ" },
            [VK_COMMA] = new[] { "“", "”", "‘", "’", "「", "」", "『", "』" },
        }),

        new("PL", "Polish", "波蘭文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "ą" },
            [VK_C] = new[] { "ć" },
            [VK_E] = new[] { "ę", "€" },
            [VK_L] = new[] { "ł" },
            [VK_N] = new[] { "ń" },
            [VK_O] = new[] { "ó" },
            [VK_S] = new[] { "ś" },
            [VK_Z] = new[] { "ż", "ź" },
            [VK_COMMA] = new[] { "„", "”", "‘", "’", "»", "«" },
        }),

        new("PT", "Portuguese", "葡萄牙文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "á", "à", "â", "ã", "ª" },
            [VK_C] = new[] { "ç" },
            [VK_E] = new[] { "é", "ê", "€" },
            [VK_I] = new[] { "í" },
            [VK_O] = new[] { "ô", "ó", "õ", "º" },
            [VK_S] = new[] { "$" },
            [VK_U] = new[] { "ú" },
            [VK_COMMA] = new[] { "“", "”", "‘", "’", "«", "»" },
        }),

        new("RO", "Romanian", "羅馬尼亞文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "ă", "â" },
            [VK_I] = new[] { "î" },
            [VK_S] = new[] { "ș" },
            [VK_T] = new[] { "ț" },
            [VK_COMMA] = new[] { "„", "”", "«", "»" },
        }),

        new("SK", "Slovak", "斯洛伐克文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "á", "ä" },
            [VK_C] = new[] { "č" },
            [VK_D] = new[] { "ď" },
            [VK_E] = new[] { "é", "€" },
            [VK_I] = new[] { "í" },
            [VK_L] = new[] { "ľ", "ĺ" },
            [VK_N] = new[] { "ň" },
            [VK_O] = new[] { "ó", "ô" },
            [VK_R] = new[] { "ŕ" },
            [VK_S] = new[] { "š" },
            [VK_T] = new[] { "ť" },
            [VK_U] = new[] { "ú" },
            [VK_Y] = new[] { "ý" },
            [VK_Z] = new[] { "ž" },
            [VK_COMMA] = new[] { "„", "“", "‚", "‘", "»", "«", "›", "‹" },
        }),

        new("SL", "Slovenian", "斯洛文尼亞文", AccentGroup.Language, new()
        {
            [VK_C] = new[] { "č", "ć" },
            [VK_E] = new[] { "€" },
            [VK_S] = new[] { "š" },
            [VK_Z] = new[] { "ž" },
            [VK_COMMA] = new[] { "„", "“", "»", "«" },
        }),

        new("SP", "Spanish", "西班牙文", AccentGroup.Language, new()
        {
            [VK_1] = new[] { "¡" },
            [VK_A] = new[] { "á" },
            [VK_E] = new[] { "é", "€" },
            [VK_H] = new[] { "ḥ" },
            [VK_I] = new[] { "í" },
            [VK_L] = new[] { "ḷ" },
            [VK_N] = new[] { "ñ" },
            [VK_O] = new[] { "ó" },
            [VK_U] = new[] { "ú", "ü" },
            [VK_COMMA] = new[] { "¿", "?", "¡", "!", "«", "»", "“", "”", "‘", "’" },
            [VK_SLASH] = new[] { "¿" },
        }),

        new("SV", "Swedish", "瑞典文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "å", "ä" },
            [VK_E] = new[] { "é" },
            [VK_O] = new[] { "ö" },
            [VK_COMMA] = new[] { "”", "’", "»", "«" },
        }),

        new("TK", "Turkish", "土耳其文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "â" },
            [VK_C] = new[] { "ç" },
            [VK_E] = new[] { "ë", "€" },
            [VK_G] = new[] { "ğ" },
            [VK_I] = new[] { "ı", "İ", "î" },
            [VK_O] = new[] { "ö", "ô" },
            [VK_S] = new[] { "ş" },
            [VK_T] = new[] { "₺" },
            [VK_U] = new[] { "ü", "û" },
            [VK_COMMA] = new[] { "“", "”", "‘", "’", "«", "»", "‹", "›" },
        }),

        new("VI", "Vietnamese", "越南文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "à", "ả", "ã", "á", "ạ", "ă", "ằ", "ẳ", "ẵ", "ắ", "ặ", "â", "ầ", "ẩ", "ẫ", "ấ", "ậ" },
            [VK_D] = new[] { "đ" },
            [VK_E] = new[] { "è", "ẻ", "ẽ", "é", "ẹ", "ê", "ề", "ể", "ễ", "ế", "ệ" },
            [VK_I] = new[] { "ì", "ỉ", "ĩ", "í", "ị" },
            [VK_O] = new[] { "ò", "ỏ", "õ", "ó", "ọ", "ô", "ồ", "ổ", "ỗ", "ố", "ộ", "ơ", "ờ", "ở", "ỡ", "ớ", "ợ" },
            [VK_U] = new[] { "ù", "ủ", "ũ", "ú", "ụ", "ư", "ừ", "ử", "ữ", "ứ", "ự" },
            [VK_Y] = new[] { "ỳ", "ỷ", "ỹ", "ý", "ỵ" },
        }),

        new("WELSH", "Welsh", "威爾斯文", AccentGroup.Language, new()
        {
            [VK_A] = new[] { "â", "ä", "à", "á" },
            [VK_E] = new[] { "ê", "ë", "è", "é" },
            [VK_I] = new[] { "î", "ï", "ì", "í" },
            [VK_O] = new[] { "ô", "ö", "ò", "ó" },
            [VK_P] = new[] { "£" },
            [VK_U] = new[] { "û", "ü", "ù", "ú" },
            [VK_Y] = new[] { "ŷ", "ÿ", "ỳ", "ý" },
            [VK_W] = new[] { "ŵ", "ẅ", "ẁ", "ẃ" },
            [VK_COMMA] = new[] { "‘", "’", "“", "”" },
        }),
    };

    /// <summary>O(1) 由識別碼搵語言集 · O(1) lookup from id to its <see cref="AccentLanguage"/>.</summary>
    public static readonly IReadOnlyDictionary<string, AccentLanguage> ById =
        All.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    // 群組顯示次序：語言先，符號集後 · Group order: spoken languages first, then symbol sets.
    private static readonly Dictionary<AccentGroup, int> _groupOrder = new()
    {
        [AccentGroup.Language] = 0,
        [AccentGroup.Special] = 1,
    };

    private static readonly ConcurrentDictionary<(string sig, int vk), string[]> _cache = new();

    /// <summary>
    /// 攞畀定基底鍵、跨選定語言集嘅去重字元清單 · The deduplicated variants for a base key across the
    /// selected sets, ordered by group then registry order. Empty array when no set provides any.
    /// </summary>
    public static string[] GetCharacters(int vk, IReadOnlyCollection<string> selectedIds)
    {
        if (selectedIds.Count == 0) return Array.Empty<string>();

        var sig = string.Join(",", selectedIds.OrderBy(x => x, StringComparer.Ordinal));
        return _cache.GetOrAdd((sig, vk), key =>
        {
            var idSet = new HashSet<string>(selectedIds, StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (var lang in All
                .Where(l => idSet.Contains(l.Id))
                .OrderBy(l => _groupOrder[l.Group]))
            {
                if (lang.Map.TryGetValue(vk, out var chars))
                {
                    result.AddRange(chars);
                }
            }

            return result.Distinct().ToArray();
        });
    }

    /// <summary>清除字元快取（選定語言改變時）· Clear the combined-lookup cache (when the selection changes).</summary>
    public static void InvalidateCache() => _cache.Clear();
}
