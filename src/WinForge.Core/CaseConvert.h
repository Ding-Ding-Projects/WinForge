#pragma once

#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::caseconvert
{
    struct Form
    {
        std::wstring en;
        std::wstring zh;
        std::wstring value;
    };

    // Splits arbitrary UTF-16 input into lowercase tokens using the same
    // separator and camelCase / digit boundary rules as the managed feature.
    [[nodiscard]] std::vector<std::wstring> Tokenize(std::wstring_view input);

    // Returns the full ordered set of case conversions for the given input.
    [[nodiscard]] std::vector<Form> AllForms(std::wstring_view input);
}
