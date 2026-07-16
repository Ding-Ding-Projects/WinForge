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

    [[nodiscard]] std::wstring BuildRegexGroup(
        std::wstring_view fragment,
        std::wstring_view capture_name = {});
    [[nodiscard]] std::wstring BuildRegexAlternation(
        std::span<std::wstring const> alternatives);
    [[nodiscard]] std::wstring ApplyRegexQuantifier(
        std::wstring_view fragment,
        RegexQuantifierSpec specification = {});
}
