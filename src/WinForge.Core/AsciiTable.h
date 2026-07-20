#pragma once

#include "Localization.h"

#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::asciitable
{
    // A presentation-independent row for the managed ASCII / Latin-1 table.
    // `copy_value` deliberately retains the raw code unit for control rows;
    // callers decide when an explicit user action may put it on the clipboard.
    struct Row
    {
        int code{};
        std::wstring decimal;
        std::wstring hexadecimal;
        std::wstring octal;
        std::wstring binary;
        std::wstring glyph;
        std::wstring name;
        std::wstring copy_value;
        std::wstring search_key;
    };

    // Builds codes 0..127, or 0..255 when the managed Latin-1 option is on.
    // The core has no UI, clipboard, file, registry, network, or process side
    // effects. Allocation failures are contained and return accumulated rows.
    [[nodiscard]] std::vector<Row> Build(bool include_latin1, LanguageMode language);

    // Mirrors the managed free-text search over decimal, hexadecimal, octal,
    // binary, glyph, mnemonic, and localized description. An empty/whitespace
    // query retains every row in source order.
    [[nodiscard]] std::vector<Row> Filter(
        std::vector<Row> const& rows,
        std::wstring_view query);

    [[nodiscard]] bool IsInvisibleOrControl(int code) noexcept;
}
