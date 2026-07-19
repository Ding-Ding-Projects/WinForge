#pragma once

#include <cstddef>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::referencetext
{
    enum class PhoneticAlphabet
    {
        Nato,
        Police,
        Simple,
    };

    struct PhoneticSpelledChar
    {
        std::wstring character;
        std::wstring code;
    };

    struct PhoneticSpellResult
    {
        std::wstring spoken;
        std::vector<PhoneticSpelledChar> characters;
    };

    [[nodiscard]] std::wstring_view PhoneticAlphabetDisplayName(
        PhoneticAlphabet alphabet) noexcept;

    // Mirrors PhoneticService.Spell. Input is deliberately enumerated one
    // UTF-16 code unit at a time, including unpaired surrogates and the two
    // halves of supplementary characters.
    [[nodiscard]] PhoneticSpellResult SpellPhonetic(
        std::wstring_view input,
        PhoneticAlphabet alphabet,
        bool upper,
        bool keepPunctuation) noexcept;

    enum class BoxBorderStyle
    {
        Ascii,
        Single,
        Double,
        Rounded,
        Heavy,
        Stars,
        CommentSlash,
        CommentHash,
    };

    enum class BoxAlignment
    {
        Left,
        Center,
        Right,
    };

    // Mirrors BoxTextService.Render, including mixed-newline normalization,
    // tab expansion, title cleanup, padding clamping, and UTF-16 display width.
    [[nodiscard]] std::wstring RenderBoxText(
        std::wstring_view text,
        BoxBorderStyle style,
        int padding,
        BoxAlignment alignment,
        std::wstring_view title) noexcept;

    struct HtmlEntityReference
    {
        std::wstring name;
        std::wstring character;
        std::wstring description_en;
        std::wstring description_zh;
    };

    [[nodiscard]] std::wstring EncodeHtmlEntities(
        std::wstring_view input,
        bool escapeNonAscii) noexcept;

    [[nodiscard]] std::wstring DecodeHtmlEntities(std::wstring_view input) noexcept;

    [[nodiscard]] std::vector<HtmlEntityReference> const&
        HtmlEntityReferenceList() noexcept;

    // HtmlEntitiesService.Length is System.String.Length: UTF-16 code units.
    [[nodiscard]] std::size_t HtmlEntityUtf16Length(std::wstring_view input) noexcept;
}
