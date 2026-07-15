#pragma once

#include <string>
#include <string_view>

namespace winforge::core::codec
{
    enum class Encoding
    {
        Base32,
        Base32NoPad,
        Base58,
        Ascii85
    };

    struct Result
    {
        bool ok{ false };
        std::wstring text;
        std::wstring error;
    };

    // Encodes UTF-16 text as UTF-8 bytes using the selected textual codec.
    // Malformed UTF-16 units use the same U+FFFD replacement policy as the
    // managed UTF-8 oracle.
    [[nodiscard]] Result Encode(std::wstring_view text, Encoding encoding);

    // Decodes a codec string into UTF-16 text. Malformed codec input fails
    // atomically with an empty result and a bilingual-agnostic error token.
    [[nodiscard]] Result Decode(std::wstring_view text, Encoding encoding);
}
