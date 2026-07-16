#include "RegexBuilder.h"
#include "RegexSearch.h"

#include <algorithm>
#include <array>

namespace
{
    [[nodiscard]] bool IsCaptureNameCharacter(wchar_t character, bool first) noexcept
    {
        if ((character >= L'A' && character <= L'Z')
            || (character >= L'a' && character <= L'z')
            || character == L'_')
        {
            return true;
        }
        return !first && character >= L'0' && character <= L'9';
    }

    [[nodiscard]] bool IsValidCaptureName(std::wstring_view value) noexcept
    {
        if (value.empty()) return false;
        for (std::size_t index = 0; index < value.size(); ++index)
        {
            if (!IsCaptureNameCharacter(value[index], index == 0)) return false;
        }
        return true;
    }
}

namespace winforge::core::regex
{
    namespace
    {
        constexpr std::array<RegexRecipeDescriptor, 8> kRecipes{
            RegexRecipeDescriptor{ RegexRecipe::LiteralContains,
                L"Literal contains", L"literal 包含", true },
            RegexRecipeDescriptor{ RegexRecipe::LiteralExact,
                L"Literal exact match", L"literal 完全符合", true },
            RegexRecipeDescriptor{ RegexRecipe::LiteralStartsWith,
                L"Literal starts with", L"literal 開頭符合", true },
            RegexRecipeDescriptor{ RegexRecipe::LiteralEndsWith,
                L"Literal ends with", L"literal 結尾符合", true },
            RegexRecipeDescriptor{ RegexRecipe::WholeWordLiteral,
                L"Whole literal word", L"完整 literal word", true },
            RegexRecipeDescriptor{ RegexRecipe::NativeRouteId,
                L"Native route id", L"原生路線 id", false },
            RegexRecipeDescriptor{ RegexRecipe::PackageId,
                L"Package id", L"套件 id", false },
            RegexRecipeDescriptor{ RegexRecipe::SemanticVersion,
                L"Semantic version", L"語義版本", false },
        };
    }

    std::wstring BuildRegexGroup(std::wstring_view fragment, std::wstring_view capture_name)
    {
        if (IsValidCaptureName(capture_name))
        {
            return L"(?<" + std::wstring(capture_name) + L">" + std::wstring(fragment) + L")";
        }
        return L"(?:" + std::wstring(fragment) + L")";
    }

    std::wstring BuildRegexAlternation(std::span<std::wstring const> alternatives)
    {
        std::wstring result = L"(?:";
        bool first = true;
        for (auto const& alternative : alternatives)
        {
            if (!first) result += L"|";
            result += alternative;
            first = false;
        }
        result += L")";
        return result;
    }

    std::wstring ApplyRegexQuantifier(std::wstring_view fragment, RegexQuantifierSpec specification)
    {
        auto const grouped = BuildRegexGroup(fragment);
        switch (specification.kind)
        {
        case RegexQuantifier::ZeroOrMore:
            return grouped + L"*";
        case RegexQuantifier::OneOrMore:
            return grouped + L"+";
        case RegexQuantifier::Optional:
            return grouped + L"?";
        case RegexQuantifier::Range:
        {
            auto const minimum = specification.minimum;
            auto const maximum = std::max(minimum, specification.maximum);
            if (specification.unbounded)
            {
                return grouped + L"{" + std::to_wstring(minimum) + L",}";
            }
            return grouped + L"{" + std::to_wstring(minimum) + L"," + std::to_wstring(maximum) + L"}";
        }
        case RegexQuantifier::Once:
        default:
            return std::wstring(fragment);
        }
    }

    std::span<RegexRecipeDescriptor const> RegexRecipes() noexcept
    {
        return kRecipes;
    }

    std::wstring BuildRegexRecipe(RegexRecipe recipe, std::wstring_view literal)
    {
        auto const escaped = EscapeRegexLiteral(literal);
        switch (recipe)
        {
        case RegexRecipe::LiteralExact:
            return L"^(?:" + escaped + L")$";
        case RegexRecipe::LiteralStartsWith:
            return L"^" + escaped;
        case RegexRecipe::LiteralEndsWith:
            return escaped + L"$";
        case RegexRecipe::WholeWordLiteral:
            return L"\\b" + escaped + L"\\b";
        case RegexRecipe::NativeRouteId:
            return L"^module\\.[A-Za-z0-9._-]+$";
        case RegexRecipe::PackageId:
            return L"^[A-Za-z0-9][A-Za-z0-9._-]*$";
        case RegexRecipe::SemanticVersion:
            return L"\\bv?\\d+(?:\\.\\d+){1,3}(?:[-+][A-Za-z0-9.-]+)?\\b";
        case RegexRecipe::LiteralContains:
        default:
            return escaped;
        }
    }

    std::wstring BuildRegexAssertion(RegexAssertion assertion, std::wstring_view fragment)
    {
        switch (assertion)
        {
        case RegexAssertion::WordBoundary:
            return L"\\b";
        case RegexAssertion::NotWordBoundary:
            return L"\\B";
        case RegexAssertion::PositiveLookahead:
            return fragment.empty() ? std::wstring{} : L"(?=" + std::wstring(fragment) + L")";
        case RegexAssertion::NegativeLookahead:
            return fragment.empty() ? std::wstring{} : L"(?!" + std::wstring(fragment) + L")";
        case RegexAssertion::PositiveLookbehind:
            return fragment.empty() ? std::wstring{} : L"(?<=" + std::wstring(fragment) + L")";
        case RegexAssertion::NegativeLookbehind:
            return fragment.empty() ? std::wstring{} : L"(?<!" + std::wstring(fragment) + L")";
        default:
            return {};
        }
    }
}
