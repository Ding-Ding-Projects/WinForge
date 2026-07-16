#pragma once

#include <cstdint>
#include <span>
#include <string>
#include <string_view>

namespace winforge::core::regex
{
    enum class RegexQuantifier : std::uint8_t
    {
        Once = 0,
        ZeroOrMore,
        OneOrMore,
        Optional,
        Range,
    };

    struct RegexQuantifierSpec
    {
        RegexQuantifier kind{ RegexQuantifier::Once };
        std::uint32_t minimum{};
        std::uint32_t maximum{};
        bool unbounded{ false };
    };

    // Recipes are deliberately small, composable building blocks. They do
    // not broaden the accepted PCRE2 dialect; every result still flows
    // through SafeRegex::Compile before it can be previewed or applied.
    enum class RegexRecipe : std::uint8_t
    {
        LiteralContains = 0,
        LiteralExact,
        LiteralStartsWith,
        LiteralEndsWith,
        WholeWordLiteral,
        NativeRouteId,
        PackageId,
        SemanticVersion,
    };

    struct RegexRecipeDescriptor
    {
        RegexRecipe recipe;
        std::wstring_view name_en;
        std::wstring_view name_zh;
        bool requires_literal{ false };
    };

    enum class RegexAssertion : std::uint8_t
    {
        WordBoundary = 0,
        NotWordBoundary,
        PositiveLookahead,
        NegativeLookahead,
        PositiveLookbehind,
        NegativeLookbehind,
    };

    [[nodiscard]] std::wstring BuildRegexGroup(
        std::wstring_view fragment,
        std::wstring_view capture_name = {});
    [[nodiscard]] std::wstring BuildRegexAlternation(
        std::span<std::wstring const> alternatives);
    [[nodiscard]] std::wstring ApplyRegexQuantifier(
        std::wstring_view fragment,
        RegexQuantifierSpec specification = {});
    [[nodiscard]] std::span<RegexRecipeDescriptor const> RegexRecipes() noexcept;
    [[nodiscard]] std::wstring BuildRegexRecipe(
        RegexRecipe recipe,
        std::wstring_view literal = {});
    // Lookaround fragments are advanced PCRE2 content, intentionally not
    // escaped. The builder previews and compiles them under the same bounded
    // SafeRegex policy as every other pattern before applying them.
    [[nodiscard]] std::wstring BuildRegexAssertion(
        RegexAssertion assertion,
        std::wstring_view fragment = {});
}
