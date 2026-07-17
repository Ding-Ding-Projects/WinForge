#pragma once

#include <span>
#include <string_view>

namespace winforge::core::symbols
{
    // Immutable, local Symbols Palette catalog. It intentionally has no I/O,
    // process, network, or clipboard dependencies so it is directly testable.
    struct SymbolCategory
    {
        std::wstring_view key;
        std::wstring_view name_en;
        std::wstring_view name_zh;
    };

    struct SymbolEntry
    {
        std::wstring_view category_key;
        std::wstring_view category_en;
        std::wstring_view category_zh;
        std::wstring_view glyph;
        std::wstring_view name_en;
        std::wstring_view name_zh;
    };

    [[nodiscard]] std::span<SymbolCategory const> SymbolsCategories() noexcept;
    [[nodiscard]] std::span<SymbolEntry const> SymbolsEntries() noexcept;

    // Mirrors the managed palette filter: an empty or unknown category means
    // all categories; a trimmed case-insensitive literal query spans glyph,
    // English name, and Cantonese name.
    [[nodiscard]] bool SymbolsMatchesLiteral(
        SymbolEntry const& entry,
        std::wstring_view category_key,
        std::wstring_view query) noexcept;
}
