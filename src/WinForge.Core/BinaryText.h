#pragma once

#include <string>
#include <string_view>

namespace winforge::core::binarytext
{
    enum class NumericBase
    {
        Binary = 2,
        Octal = 8,
        Decimal = 10,
        Hex = 16
    };

    enum class Error
    {
        None,
        InvalidToken,
        OutOfRange,
        Unexpected
    };

    struct Result
    {
        bool ok{ false };
        std::wstring text;
        Error error{ Error::None };
        std::wstring token;
    };

    // Converts a UTF-16 string to its UTF-8 byte codes. Invalid UTF-16 units are
    // replacement-encoded, matching Encoding.UTF8's default replacement fallback.
    [[nodiscard]] Result Encode(std::wstring_view input, NumericBase base);

    // Converts delimiter-separated byte codes to UTF-16. Matching 0b/0o/0x prefixes
    // are accepted; malformed UTF-8 bytes decode using replacement characters.
    [[nodiscard]] Result Decode(std::wstring_view input, NumericBase base);
}
