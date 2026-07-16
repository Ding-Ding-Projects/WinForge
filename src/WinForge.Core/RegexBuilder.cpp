#include "RegexBuilder.h"

#include <algorithm>

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
}
