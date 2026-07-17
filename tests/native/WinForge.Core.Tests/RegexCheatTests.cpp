#include "RegexCheatTests.h"

#include "RegexCheat.h"
#include "RegexSearch.h"

#include <algorithm>
#include <array>
#include <iostream>
#include <set>
#include <string_view>

namespace
{
    template <typename Range, typename Predicate>
    auto FindFirst(Range const& range, Predicate predicate)
    {
        return std::find_if(range.begin(), range.end(), predicate);
    }
}

NativeTestCounts RunRegexCheatTests()
{
    using namespace winforge::core::regex;

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

    auto const categories = RegexCheatCategories();
    auto const entries = RegexCheatEntries();
    auto const recipes = RegexCheatRecipes();
    constexpr std::array<std::wstring_view, 9> expectedCategoryKeys{
        L"character-classes", L"anchors", L"quantifiers", L"groups-backreferences",
        L"named-groups", L"lookaround", L"alternation", L"flags-options", L"common-recipes",
    };
    constexpr std::array<std::size_t, 9> expectedCategoryCounts{ 15, 8, 11, 5, 5, 4, 3, 8, 8 };
    std::set<std::wstring_view> categoryKeys;
    bool categoryCatalogIsOrderedAndBilingual = categories.size() == expectedCategoryKeys.size();
    for (std::size_t index = 0; index < categories.size() && index < expectedCategoryKeys.size(); ++index)
    {
        auto const& category = categories[index];
        categoryCatalogIsOrderedAndBilingual = categoryCatalogIsOrderedAndBilingual &&
            category.key == expectedCategoryKeys[index] && !category.name_en.empty() && !category.name_zh.empty() &&
            categoryKeys.insert(category.key).second;
    }
    expect(entries.size() == 67 && categoryCatalogIsOrderedAndBilingual,
        "Regex Cheatsheet preserves all 67 ordered bilingual reference entries across nine categories");

    bool categoryCountsMatch = true;
    for (std::size_t index = 0; index < categories.size(); ++index)
    {
        auto const count = static_cast<std::size_t>(std::count_if(entries.begin(), entries.end(), [category = categories[index].key](RegexCheatEntry const& entry)
        {
            return entry.category_key == category;
        }));
        categoryCountsMatch = categoryCountsMatch && count == expectedCategoryCounts[index];
    }
    expect(categoryCountsMatch,
        "Regex Cheatsheet keeps the managed category distribution without dropping a reference row");

    auto const atomic = FindFirst(entries, [](RegexCheatEntry const& entry) { return entry.token == L"(?>a*)"; });
    auto const email = FindFirst(entries, [](RegexCheatEntry const& entry) { return entry.token == L"Email"; });
    expect(atomic != entries.end() &&
            RegexCheatMatchesLiteral(*atomic, L"quantifiers", L"atomic") &&
            RegexCheatMatchesLiteral(*atomic, L"quantifiers", L".NET") &&
            !RegexCheatMatchesLiteral(*atomic, L"anchors", L"atomic") &&
            email != entries.end() && RegexCheatMatchesLiteral(*email, L"", L"電郵"),
        "Regex Cheatsheet literal filtering intersects category and searches English Cantonese token description and examples");

    auto const atomicQuery = SafeRegex::Compile(L"^\\(\\?>a\\*\\)$");
    auto const invalidQuery = SafeRegex::Compile(L"[");
    expect(atomicQuery.Ok() && atomic != entries.end() && atomicQuery.expression->Search(atomic->token).matched &&
            !invalidQuery.Ok() && invalidQuery.diagnostic.code == RegexErrorCode::Syntax,
        "Regex Cheatsheet delegates explicit regex queries to bounded PCRE2 while invalid syntax remains non-executing");

    constexpr std::array<std::wstring_view, 8> expectedRecipeKeys{
        L"email", L"url", L"ipv4", L"hex-color", L"uuid", L"slug", L"iso-date", L"us-phone",
    };
    std::set<std::wstring_view> recipeKeys;
    bool recipeCatalogIsComplete = recipes.size() == expectedRecipeKeys.size();
    for (std::size_t index = 0; index < recipes.size() && index < expectedRecipeKeys.size(); ++index)
    {
        auto const& recipe = recipes[index];
        recipeCatalogIsComplete = recipeCatalogIsComplete && recipe.key == expectedRecipeKeys[index] &&
            !recipe.name_en.empty() && !recipe.name_zh.empty() && !recipe.pattern.empty() &&
            recipeKeys.insert(recipe.key).second;
    }
    expect(recipeCatalogIsComplete,
        "Regex Cheatsheet preserves eight ordered copy-only ready-made patterns");

    auto const dotNetOnly = FindFirst(entries, [](RegexCheatEntry const& entry)
    {
        return entry.token == L"RegexOptions.Compiled";
    });
    expect(dotNetOnly != entries.end() && dotNetOnly->description_en.find(L".NET") != std::wstring_view::npos,
        "Regex Cheatsheet retains .NET-only reference material as data instead of executing each documentation token");

    return counts;
}
