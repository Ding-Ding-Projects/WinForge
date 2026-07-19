#pragma once

#include <string>
#include <string_view>

namespace winforge::core::slugify
{
    enum class Separator
    {
        Hyphen,
        Underscore,
        Dot,
    };

    enum class LetterCase
    {
        Lower,
        Upper,
        Keep,
    };

    // Mirrors SlugifyService.Options. The core deliberately accepts values
    // beyond the UI NumberBox's 0–500 range so callers retain the managed
    // service contract.
    struct Options
    {
        Separator separator{ Separator::Hyphen };
        LetterCase letterCase{ LetterCase::Lower };
        bool stripDiacritics{ true };
        int maxLength{};
        bool collapseRepeats{ true };
        bool keepUnicodeLetters{};
    };

    struct Preview
    {
        std::wstring input;
        std::wstring slug;
        bool hasInput{};
    };

    [[nodiscard]] wchar_t SeparatorCharacter(Separator separator) noexcept;

    // Converts one UTF-16 line with the managed service's NFD/nonspacing-mark
    // handling, boundary rules, invariant casing, and UTF-16 length limit.
    // Like the managed oracle, malformed UTF-16 returns an empty slug only
    // when diacritic stripping asks the normalizer to inspect it.
    [[nodiscard]] std::wstring Slugify(std::wstring_view input, Options const& options = {}) noexcept;

    // Converts each nonblank managed line and joins the results with LF. A
    // nonblank line that converts to an empty slug remains an empty output row.
    [[nodiscard]] std::wstring SlugifyBlock(std::wstring_view input, Options const& options = {}) noexcept;

    // Returns the same first trimmed nonblank line shown by the managed live
    // before→after preview, together with its local slug.
    [[nodiscard]] Preview PreviewFirstLine(std::wstring_view input, Options const& options = {}) noexcept;
}
