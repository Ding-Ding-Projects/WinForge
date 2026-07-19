#pragma once

#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::morse
{
    struct EncodeResult
    {
        std::wstring text;
        std::vector<wchar_t> unknown;
    };

    struct Flash
    {
        bool on{ false };
        int units{};
    };

    // Mirrors MorseService.ToMorse: input is split only on ASCII space, tab,
    // CR, and LF; each UTF-16 code unit is invariant-uppercased before lookup.
    // Unsupported code units are emitted as '#' and retained once, in encounter
    // order, for the caller's warning surface.
    [[nodiscard]] EncodeResult ToMorse(
        std::wstring_view text,
        std::wstring_view letterSeparator,
        std::wstring_view wordSeparator) noexcept;

    // Mirrors MorseService.FromMorse, including its deliberately tolerant dot
    // and dash aliases. '/' and '|' are word boundaries; spaces and tabs split
    // letters. Internal CR/LF are intentionally not token separators.
    [[nodiscard]] std::wstring FromMorse(std::wstring_view morse) noexcept;

    // Expands plain text into the managed visual timing sequence: dot=1,
    // dash=3, intra-character gap=1, letter gap=3, word gap=7. Unsupported
    // input is skipped, but its source word still retains its word-gap slot.
    [[nodiscard]] std::vector<Flash> BuildTimeline(std::wstring_view text) noexcept;

    // PARIS timing, clamped exactly to 1..60 WPM. NaN follows the managed
    // service's lower-bound fallback and resolves to 1 WPM.
    [[nodiscard]] double UnitMsForWpm(double wpm) noexcept;
}
