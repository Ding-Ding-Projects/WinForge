#pragma once

#include <span>
#include <string_view>

namespace winforge::core::regex
{
    // Immutable, local-only reference data for the native Regex Cheatsheet.
    // It deliberately describes .NET syntax as documentation where needed;
    // callers must use SafeRegex separately before evaluating a PCRE2 query.
    struct RegexCheatCategory
    {
        std::wstring_view key;
        std::wstring_view name_en;
        std::wstring_view name_zh;
    };

    struct RegexCheatEntry
    {
        std::wstring_view category_key;
        std::wstring_view category_en;
        std::wstring_view category_zh;
        std::wstring_view token;
        std::wstring_view description_en;
        std::wstring_view description_zh;
        std::wstring_view example;
    };

    struct RegexCheatRecipe
    {
        std::wstring_view key;
        std::wstring_view name_en;
        std::wstring_view name_zh;
        std::wstring_view pattern;
    };

    [[nodiscard]] std::span<RegexCheatCategory const> RegexCheatCategories() noexcept;
    [[nodiscard]] std::span<RegexCheatEntry const> RegexCheatEntries() noexcept;
    [[nodiscard]] std::span<RegexCheatRecipe const> RegexCheatRecipes() noexcept;

    // Matches a selected category (empty = All) and a case-insensitive
    // literal query across every readable reference field. This has no regex
    // evaluation or side effects; the WinUI renderer supplies SafeRegex only
    // when the user explicitly enables its PCRE2 mode.
    [[nodiscard]] bool RegexCheatMatchesLiteral(
        RegexCheatEntry const& entry,
        std::wstring_view category_key,
        std::wstring_view query) noexcept;
}
