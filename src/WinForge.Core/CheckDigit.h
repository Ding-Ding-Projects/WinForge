#pragma once

#include <string>
#include <string_view>

namespace winforge::core::checkdigit
{
    enum class Scheme
    {
        Luhn,
        Isbn10,
        Isbn13,
        Ean13,
        UpcA,
        Iban,
    };

    struct Result
    {
        bool ok{ false };
        bool valid{ false };
        std::wstring detail_en;
        std::wstring detail_zh;
        std::wstring computed;
    };

    [[nodiscard]] Result Validate(Scheme scheme, std::wstring_view input) noexcept;
    [[nodiscard]] std::wstring IbanRegistryCanonicalForTests();
}
