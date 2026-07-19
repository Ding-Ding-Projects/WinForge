#pragma once

#include <cstddef>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::textdiff
{
    inline constexpr std::uint64_t CellBudget = 6'000'000;

    enum class ChangeKind
    {
        Unchanged,
        Added,
        Removed,
    };

    struct DiffLine
    {
        ChangeKind kind{ ChangeKind::Unchanged };
        wchar_t prefix{ L' ' };
        std::wstring text;
    };

    struct DiffResult
    {
        std::vector<DiffLine> lines;
        std::size_t added{};
        std::size_t removed{};
        std::size_t unchanged{};
        bool truncated{};
    };

    // Computes the same bounded, line-level LCS as the managed WinForge oracle.
    // Errors are contained and return whatever output was accumulated.
    [[nodiscard]] DiffResult Compute(
        std::wstring_view leftText,
        std::wstring_view rightText,
        bool ignoreWhitespace = false,
        bool ignoreCase = false);

    // Shared invariant, non-expanding UTF-16 uppercase operations. Text Diff's
    // oracle uses ToUpperInvariant directly. The OrdinalIgnoreCase equality key
    // differs only for U+017F; sibling sorters additionally apply CoreLib's
    // scalar-aware valid-surrogate ordering to these folded keys.
    [[nodiscard]] std::wstring ToUpperInvariantKey(std::wstring_view input);
    [[nodiscard]] wchar_t ToUpperInvariantCodeUnit(wchar_t input) noexcept;
    [[nodiscard]] std::wstring FoldOrdinalIgnoreCase(std::wstring_view input);

    // Emits the oracle's unified-style representation, including its fixed header.
    // Errors are contained and return an empty string.
    [[nodiscard]] std::wstring ToUnifiedDiff(DiffResult const& result);
}
