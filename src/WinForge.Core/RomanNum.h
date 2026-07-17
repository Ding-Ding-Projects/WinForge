#pragma once

#include <cstdint>
#include <string>
#include <string_view>

namespace winforge::core::romannum
{
    inline constexpr std::int64_t StandardMax = 3'999;
    inline constexpr std::int64_t ExtendedMax = 3'999'999;
    inline constexpr wchar_t Overline = L'\x0305';

    struct ToRomanResult
    {
        bool ok{ false };
        std::wstring roman;
        std::wstring breakdown;
        std::wstring reason_en;
        std::wstring reason_zh;
    };

    struct ToNumberResult
    {
        bool ok{ false };
        std::int64_t value{ 0 };
        std::wstring breakdown;
        std::wstring reason_en;
        std::wstring reason_zh;
    };

    // Converts an integer to a canonical Roman numeral. Standard mode covers
    // 1..3,999; extended mode uses U+0305 COMBINING OVERLINE for 1..3,999,999.
    [[nodiscard]] ToRomanResult ToRoman(std::int64_t value, bool allowExtended);

    // Strictly parses a canonical Roman numeral. It preserves the managed page's
    // canonical-round-trip quirk: direct lowercase is rejected, while the (X)
    // parenthetical ×1000 input notation is accepted in extended mode.
    [[nodiscard]] ToNumberResult ToNumber(std::wstring_view input, bool allowExtended);

    // Mirrors the managed integer editor: comma separators and a leading sign
    // are accepted, while non-integral and overflowing input is rejected.
    [[nodiscard]] bool TryParseInteger(std::wstring_view input, std::int64_t& value);

    [[nodiscard]] std::wstring FormatGrouped(std::int64_t value);
}
