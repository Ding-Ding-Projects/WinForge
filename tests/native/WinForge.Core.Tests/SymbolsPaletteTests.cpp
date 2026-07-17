#include "SymbolsPaletteTests.h"

#include "SymbolsPalette.h"

#include <algorithm>
#include <array>
#include <iostream>
#include <string_view>

namespace
{
    template <typename Predicate>
    std::size_t CountMatches(Predicate predicate)
    {
        auto const entries = winforge::core::symbols::SymbolsEntries();
        return static_cast<std::size_t>(std::count_if(entries.begin(), entries.end(), predicate));
    }

    template <typename Predicate>
    auto FindFirst(Predicate predicate)
    {
        auto const entries = winforge::core::symbols::SymbolsEntries();
        return std::find_if(entries.begin(), entries.end(), predicate);
    }
}

NativeTestCounts RunSymbolsPaletteTests()
{
    using namespace winforge::core::symbols;

    NativeTestCounts counts;
    auto expect = [&](bool condition, std::string_view name)
    {
        if (condition)
        {
            ++counts.passed;
            std::cout << "PASS " << name << '\n';
        }
        else
        {
            ++counts.failed;
            std::cerr << "FAIL " << name << '\n';
        }
    };

    auto const categories = SymbolsCategories();
    auto const entries = SymbolsEntries();
    constexpr std::array<std::wstring_view, 9> expectedCategoryKeys{
        L"arrows", L"math", L"currency", L"punctuation", L"greek",
        L"box-drawing", L"stars-bullets", L"fractions", L"super-subscript",
    };
    constexpr std::array<std::size_t, 9> expectedCategoryCounts{
        24, 32, 20, 24, 31, 26, 26, 18, 25,
    };

    bool categoriesAreOrderedAndBilingual = categories.size() == expectedCategoryKeys.size();
    bool categoryCountsMatch = categoriesAreOrderedAndBilingual;
    for (std::size_t index = 0; index < categories.size() && index < expectedCategoryKeys.size(); ++index)
    {
        auto const& category = categories[index];
        categoriesAreOrderedAndBilingual = categoriesAreOrderedAndBilingual &&
            category.key == expectedCategoryKeys[index] &&
            !category.name_en.empty() && !category.name_zh.empty();
        categoryCountsMatch = categoryCountsMatch &&
            CountMatches([key = category.key](SymbolEntry const& entry)
            {
                return entry.category_key == key;
            }) == expectedCategoryCounts[index];
    }
    expect(entries.size() == 226 && categoriesAreOrderedAndBilingual,
        "Symbols Palette preserves nine ordered bilingual categories and all 226 managed glyph rows");
    expect(categoryCountsMatch,
        "Symbols Palette preserves the managed category distribution without dropping a glyph");

    auto const leftArrow = FindFirst([](SymbolEntry const& entry) { return entry.glyph == L"\u2190"; });
    auto const notEqual = FindFirst([](SymbolEntry const& entry) { return entry.glyph == L"\u2260"; });
    auto const yen = FindFirst([](SymbolEntry const& entry) { return entry.glyph == L"\u00A5"; });
    auto const heart = FindFirst([](SymbolEntry const& entry) { return entry.glyph == L"\u2764"; });
    auto const oneTenth = FindFirst([](SymbolEntry const& entry) { return entry.glyph == L"\u2152"; });
    auto const subscriptMinus = FindFirst([](SymbolEntry const& entry) { return entry.glyph == L"\u208B"; });
    expect(leftArrow != entries.end() && leftArrow->name_en == L"Left arrow" &&
            notEqual != entries.end() && notEqual->name_en == L"Not equal" &&
            yen != entries.end() && yen->name_en == L"Yen / Yuan" &&
            heart != entries.end() && heart->name_en == L"Heart" &&
            oneTenth != entries.end() && oneTenth->name_en == L"One tenth" &&
            subscriptMinus != entries.end() && subscriptMinus->name_en == L"Subscript -",
        "Symbols Palette retains sentinel glyphs and English labels across every managed category");

    expect(leftArrow != entries.end() &&
            SymbolsMatchesLiteral(*leftArrow, L"arrows", L"\u5DE6\u7BAD\u5634") &&
            !SymbolsMatchesLiteral(*leftArrow, L"math", L"left") &&
            CountMatches([](SymbolEntry const& entry) { return entry.glyph == L"\u03C0"; }) == 2,
        "Symbols Palette keeps bilingual name search, category intersection, and the intentional duplicate pi glyph");

    auto const mathCount = CountMatches([](SymbolEntry const& entry)
    {
        return SymbolsMatchesLiteral(entry, L"math", L"");
    });
    auto const arrowsCount = CountMatches([](SymbolEntry const& entry)
    {
        return SymbolsMatchesLiteral(entry, L"arrows", L"");
    });
    expect(mathCount == 32 && arrowsCount == 24,
        "Symbols Palette literal filtering selects exactly one local category");

    auto const caseInsensitiveNotEqual = CountMatches([](SymbolEntry const& entry)
    {
        return SymbolsMatchesLiteral(entry, L"math", L"  nOt EqUaL  ");
    });
    auto const glyphNotEqual = CountMatches([](SymbolEntry const& entry)
    {
        return SymbolsMatchesLiteral(entry, L"math", L"\u2260");
    });
    expect(caseInsensitiveNotEqual == 1 && glyphNotEqual == 1 && notEqual != entries.end(),
        "Symbols Palette trims and case-folds English text while matching the glyph itself");

    auto const whitespaceMath = CountMatches([](SymbolEntry const& entry)
    {
        return SymbolsMatchesLiteral(entry, L"math", L" \t ");
    });
    auto const unknownCategory = CountMatches([](SymbolEntry const& entry)
    {
        return SymbolsMatchesLiteral(entry, L"unknown-category", L"");
    });
    auto const noMatch = CountMatches([](SymbolEntry const& entry)
    {
        return SymbolsMatchesLiteral(entry, L"", L"definitely-not-a-symbol-name");
    });
    expect(whitespaceMath == 32 && unknownCategory == 226 && noMatch == 0,
        "Symbols Palette keeps whitespace and unknown-category fallback behavior while returning empty for a true miss");

    expect(entries.front().glyph == L"\u2190" && entries.back().glyph == L"\u208B" &&
            !leftArrow->name_zh.empty() && !notEqual->name_zh.empty(),
        "Symbols Palette preserves managed source order and bilingual display fields for the native UI");

    return counts;
}
