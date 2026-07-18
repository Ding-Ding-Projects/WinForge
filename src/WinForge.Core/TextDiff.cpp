#include "TextDiff.h"

#include <algorithm>
#include <array>
#include <cstdint>
#include <string>
#include <utility>
#include <vector>

namespace
{
    struct UpperRange
    {
        std::uint32_t first;
        std::uint32_t last;
        std::uint32_t step;
        std::int32_t delta;
    };

    struct UpperPair
    {
        std::uint32_t from;
        std::uint32_t to;
    };

    // Compressed one-code-point invariant-uppercase mappings generated from the
    // shipping managed oracle. Invariant casing is intentionally independent of
    // the process locale; mappings that expand are absent because .NET's current
    // invariant simple casing does not expand any scalar value.
    constexpr std::array<UpperRange, 66> UpperRanges{
        UpperRange{ 0x61u, 0x7Au, 1u, -32 },
        UpperRange{ 0xE0u, 0xF6u, 1u, -32 },
        UpperRange{ 0xF8u, 0xFEu, 1u, -32 },
        UpperRange{ 0x101u, 0x12Fu, 2u, -1 },
        UpperRange{ 0x133u, 0x137u, 2u, -1 },
        UpperRange{ 0x13Au, 0x148u, 2u, -1 },
        UpperRange{ 0x14Bu, 0x177u, 2u, -1 },
        UpperRange{ 0x17Au, 0x17Eu, 2u, -1 },
        UpperRange{ 0x1A1u, 0x1A5u, 2u, -1 },
        UpperRange{ 0x1CEu, 0x1DCu, 2u, -1 },
        UpperRange{ 0x1DFu, 0x1EFu, 2u, -1 },
        UpperRange{ 0x1F9u, 0x21Fu, 2u, -1 },
        UpperRange{ 0x223u, 0x233u, 2u, -1 },
        UpperRange{ 0x247u, 0x24Fu, 2u, -1 },
        UpperRange{ 0x37Bu, 0x37Du, 1u, 130 },
        UpperRange{ 0x3ADu, 0x3AFu, 1u, -37 },
        UpperRange{ 0x3B1u, 0x3C1u, 1u, -32 },
        UpperRange{ 0x3C3u, 0x3CBu, 1u, -32 },
        UpperRange{ 0x3D9u, 0x3EFu, 2u, -1 },
        UpperRange{ 0x430u, 0x44Fu, 1u, -32 },
        UpperRange{ 0x450u, 0x45Fu, 1u, -80 },
        UpperRange{ 0x461u, 0x481u, 2u, -1 },
        UpperRange{ 0x48Bu, 0x4BFu, 2u, -1 },
        UpperRange{ 0x4C2u, 0x4CEu, 2u, -1 },
        UpperRange{ 0x4D1u, 0x52Fu, 2u, -1 },
        UpperRange{ 0x561u, 0x586u, 1u, -48 },
        UpperRange{ 0x10D0u, 0x10FAu, 1u, 3008 },
        UpperRange{ 0x10FDu, 0x10FFu, 1u, 3008 },
        UpperRange{ 0x13F8u, 0x13FDu, 1u, -8 },
        UpperRange{ 0x1E01u, 0x1E95u, 2u, -1 },
        UpperRange{ 0x1EA1u, 0x1EFFu, 2u, -1 },
        UpperRange{ 0x1F00u, 0x1F07u, 1u, 8 },
        UpperRange{ 0x1F10u, 0x1F15u, 1u, 8 },
        UpperRange{ 0x1F20u, 0x1F27u, 1u, 8 },
        UpperRange{ 0x1F30u, 0x1F37u, 1u, 8 },
        UpperRange{ 0x1F40u, 0x1F45u, 1u, 8 },
        UpperRange{ 0x1F51u, 0x1F57u, 2u, 8 },
        UpperRange{ 0x1F60u, 0x1F67u, 1u, 8 },
        UpperRange{ 0x1F72u, 0x1F75u, 1u, 86 },
        UpperRange{ 0x1F80u, 0x1F87u, 1u, 8 },
        UpperRange{ 0x1F90u, 0x1F97u, 1u, 8 },
        UpperRange{ 0x1FA0u, 0x1FA7u, 1u, 8 },
        UpperRange{ 0x2170u, 0x217Fu, 1u, -16 },
        UpperRange{ 0x24D0u, 0x24E9u, 1u, -26 },
        UpperRange{ 0x2C30u, 0x2C5Fu, 1u, -48 },
        UpperRange{ 0x2C68u, 0x2C6Cu, 2u, -1 },
        UpperRange{ 0x2C81u, 0x2CE3u, 2u, -1 },
        UpperRange{ 0x2D00u, 0x2D25u, 1u, -7264 },
        UpperRange{ 0xA641u, 0xA66Du, 2u, -1 },
        UpperRange{ 0xA681u, 0xA69Bu, 2u, -1 },
        UpperRange{ 0xA723u, 0xA72Fu, 2u, -1 },
        UpperRange{ 0xA733u, 0xA76Fu, 2u, -1 },
        UpperRange{ 0xA77Fu, 0xA787u, 2u, -1 },
        UpperRange{ 0xA797u, 0xA7A9u, 2u, -1 },
        UpperRange{ 0xA7B5u, 0xA7C3u, 2u, -1 },
        UpperRange{ 0xAB70u, 0xABBFu, 1u, -38864 },
        UpperRange{ 0xFF41u, 0xFF5Au, 1u, -32 },
        UpperRange{ 0x10428u, 0x1044Fu, 1u, -40 },
        UpperRange{ 0x104D8u, 0x104FBu, 1u, -40 },
        UpperRange{ 0x10597u, 0x105A1u, 1u, -39 },
        UpperRange{ 0x105A3u, 0x105B1u, 1u, -39 },
        UpperRange{ 0x105B3u, 0x105B9u, 1u, -39 },
        UpperRange{ 0x10CC0u, 0x10CF2u, 1u, -64 },
        UpperRange{ 0x118C0u, 0x118DFu, 1u, -32 },
        UpperRange{ 0x16E60u, 0x16E7Fu, 1u, -32 },
        UpperRange{ 0x1E922u, 0x1E943u, 1u, -34 },
    };

    constexpr std::array<UpperPair, 155> UpperPairs{
        UpperPair{ 0xB5u, 0x39Cu },
        UpperPair{ 0xFFu, 0x178u },
        UpperPair{ 0x17Fu, 0x53u },
        UpperPair{ 0x180u, 0x243u },
        UpperPair{ 0x183u, 0x182u },
        UpperPair{ 0x185u, 0x184u },
        UpperPair{ 0x188u, 0x187u },
        UpperPair{ 0x18Cu, 0x18Bu },
        UpperPair{ 0x192u, 0x191u },
        UpperPair{ 0x195u, 0x1F6u },
        UpperPair{ 0x199u, 0x198u },
        UpperPair{ 0x19Au, 0x23Du },
        UpperPair{ 0x19Eu, 0x220u },
        UpperPair{ 0x1A8u, 0x1A7u },
        UpperPair{ 0x1ADu, 0x1ACu },
        UpperPair{ 0x1B0u, 0x1AFu },
        UpperPair{ 0x1B4u, 0x1B3u },
        UpperPair{ 0x1B6u, 0x1B5u },
        UpperPair{ 0x1B9u, 0x1B8u },
        UpperPair{ 0x1BDu, 0x1BCu },
        UpperPair{ 0x1BFu, 0x1F7u },
        UpperPair{ 0x1C5u, 0x1C4u },
        UpperPair{ 0x1C6u, 0x1C4u },
        UpperPair{ 0x1C8u, 0x1C7u },
        UpperPair{ 0x1C9u, 0x1C7u },
        UpperPair{ 0x1CBu, 0x1CAu },
        UpperPair{ 0x1CCu, 0x1CAu },
        UpperPair{ 0x1DDu, 0x18Eu },
        UpperPair{ 0x1F2u, 0x1F1u },
        UpperPair{ 0x1F3u, 0x1F1u },
        UpperPair{ 0x1F5u, 0x1F4u },
        UpperPair{ 0x23Cu, 0x23Bu },
        UpperPair{ 0x23Fu, 0x2C7Eu },
        UpperPair{ 0x240u, 0x2C7Fu },
        UpperPair{ 0x242u, 0x241u },
        UpperPair{ 0x250u, 0x2C6Fu },
        UpperPair{ 0x251u, 0x2C6Du },
        UpperPair{ 0x252u, 0x2C70u },
        UpperPair{ 0x253u, 0x181u },
        UpperPair{ 0x254u, 0x186u },
        UpperPair{ 0x256u, 0x189u },
        UpperPair{ 0x257u, 0x18Au },
        UpperPair{ 0x259u, 0x18Fu },
        UpperPair{ 0x25Bu, 0x190u },
        UpperPair{ 0x25Cu, 0xA7ABu },
        UpperPair{ 0x260u, 0x193u },
        UpperPair{ 0x261u, 0xA7ACu },
        UpperPair{ 0x263u, 0x194u },
        UpperPair{ 0x265u, 0xA78Du },
        UpperPair{ 0x266u, 0xA7AAu },
        UpperPair{ 0x268u, 0x197u },
        UpperPair{ 0x269u, 0x196u },
        UpperPair{ 0x26Au, 0xA7AEu },
        UpperPair{ 0x26Bu, 0x2C62u },
        UpperPair{ 0x26Cu, 0xA7ADu },
        UpperPair{ 0x26Fu, 0x19Cu },
        UpperPair{ 0x271u, 0x2C6Eu },
        UpperPair{ 0x272u, 0x19Du },
        UpperPair{ 0x275u, 0x19Fu },
        UpperPair{ 0x27Du, 0x2C64u },
        UpperPair{ 0x280u, 0x1A6u },
        UpperPair{ 0x282u, 0xA7C5u },
        UpperPair{ 0x283u, 0x1A9u },
        UpperPair{ 0x287u, 0xA7B1u },
        UpperPair{ 0x288u, 0x1AEu },
        UpperPair{ 0x289u, 0x244u },
        UpperPair{ 0x28Au, 0x1B1u },
        UpperPair{ 0x28Bu, 0x1B2u },
        UpperPair{ 0x28Cu, 0x245u },
        UpperPair{ 0x292u, 0x1B7u },
        UpperPair{ 0x29Du, 0xA7B2u },
        UpperPair{ 0x29Eu, 0xA7B0u },
        UpperPair{ 0x345u, 0x399u },
        UpperPair{ 0x371u, 0x370u },
        UpperPair{ 0x373u, 0x372u },
        UpperPair{ 0x377u, 0x376u },
        UpperPair{ 0x3ACu, 0x386u },
        UpperPair{ 0x3C2u, 0x3A3u },
        UpperPair{ 0x3CCu, 0x38Cu },
        UpperPair{ 0x3CDu, 0x38Eu },
        UpperPair{ 0x3CEu, 0x38Fu },
        UpperPair{ 0x3D0u, 0x392u },
        UpperPair{ 0x3D1u, 0x398u },
        UpperPair{ 0x3D5u, 0x3A6u },
        UpperPair{ 0x3D6u, 0x3A0u },
        UpperPair{ 0x3D7u, 0x3CFu },
        UpperPair{ 0x3F0u, 0x39Au },
        UpperPair{ 0x3F1u, 0x3A1u },
        UpperPair{ 0x3F2u, 0x3F9u },
        UpperPair{ 0x3F3u, 0x37Fu },
        UpperPair{ 0x3F5u, 0x395u },
        UpperPair{ 0x3F8u, 0x3F7u },
        UpperPair{ 0x3FBu, 0x3FAu },
        UpperPair{ 0x4CFu, 0x4C0u },
        UpperPair{ 0x1C80u, 0x412u },
        UpperPair{ 0x1C81u, 0x414u },
        UpperPair{ 0x1C82u, 0x41Eu },
        UpperPair{ 0x1C83u, 0x421u },
        UpperPair{ 0x1C84u, 0x422u },
        UpperPair{ 0x1C85u, 0x422u },
        UpperPair{ 0x1C86u, 0x42Au },
        UpperPair{ 0x1C87u, 0x462u },
        UpperPair{ 0x1C88u, 0xA64Au },
        UpperPair{ 0x1D79u, 0xA77Du },
        UpperPair{ 0x1D7Du, 0x2C63u },
        UpperPair{ 0x1D8Eu, 0xA7C6u },
        UpperPair{ 0x1E9Bu, 0x1E60u },
        UpperPair{ 0x1F70u, 0x1FBAu },
        UpperPair{ 0x1F71u, 0x1FBBu },
        UpperPair{ 0x1F76u, 0x1FDAu },
        UpperPair{ 0x1F77u, 0x1FDBu },
        UpperPair{ 0x1F78u, 0x1FF8u },
        UpperPair{ 0x1F79u, 0x1FF9u },
        UpperPair{ 0x1F7Au, 0x1FEAu },
        UpperPair{ 0x1F7Bu, 0x1FEBu },
        UpperPair{ 0x1F7Cu, 0x1FFAu },
        UpperPair{ 0x1F7Du, 0x1FFBu },
        UpperPair{ 0x1FB0u, 0x1FB8u },
        UpperPair{ 0x1FB1u, 0x1FB9u },
        UpperPair{ 0x1FB3u, 0x1FBCu },
        UpperPair{ 0x1FBEu, 0x399u },
        UpperPair{ 0x1FC3u, 0x1FCCu },
        UpperPair{ 0x1FD0u, 0x1FD8u },
        UpperPair{ 0x1FD1u, 0x1FD9u },
        UpperPair{ 0x1FE0u, 0x1FE8u },
        UpperPair{ 0x1FE1u, 0x1FE9u },
        UpperPair{ 0x1FE5u, 0x1FECu },
        UpperPair{ 0x1FF3u, 0x1FFCu },
        UpperPair{ 0x214Eu, 0x2132u },
        UpperPair{ 0x2184u, 0x2183u },
        UpperPair{ 0x2C61u, 0x2C60u },
        UpperPair{ 0x2C65u, 0x23Au },
        UpperPair{ 0x2C66u, 0x23Eu },
        UpperPair{ 0x2C73u, 0x2C72u },
        UpperPair{ 0x2C76u, 0x2C75u },
        UpperPair{ 0x2CECu, 0x2CEBu },
        UpperPair{ 0x2CEEu, 0x2CEDu },
        UpperPair{ 0x2CF3u, 0x2CF2u },
        UpperPair{ 0x2D27u, 0x10C7u },
        UpperPair{ 0x2D2Du, 0x10CDu },
        UpperPair{ 0xA77Au, 0xA779u },
        UpperPair{ 0xA77Cu, 0xA77Bu },
        UpperPair{ 0xA78Cu, 0xA78Bu },
        UpperPair{ 0xA791u, 0xA790u },
        UpperPair{ 0xA793u, 0xA792u },
        UpperPair{ 0xA794u, 0xA7C4u },
        UpperPair{ 0xA7C8u, 0xA7C7u },
        UpperPair{ 0xA7CAu, 0xA7C9u },
        UpperPair{ 0xA7D1u, 0xA7D0u },
        UpperPair{ 0xA7D7u, 0xA7D6u },
        UpperPair{ 0xA7D9u, 0xA7D8u },
        UpperPair{ 0xA7F6u, 0xA7F5u },
        UpperPair{ 0xAB53u, 0xA7B3u },
        UpperPair{ 0x105BBu, 0x10594u },
        UpperPair{ 0x105BCu, 0x10595u },
    };

    [[nodiscard]] constexpr bool IsManagedWhitespace(std::uint32_t value) noexcept
    {
        return (value >= 0x0009u && value <= 0x000Du) ||
            value == 0x0020u || value == 0x0085u || value == 0x00A0u ||
            value == 0x1680u || (value >= 0x2000u && value <= 0x200Au) ||
            value == 0x2028u || value == 0x2029u || value == 0x202Fu ||
            value == 0x205Fu || value == 0x3000u;
    }

    [[nodiscard]] constexpr bool IsHighSurrogate(std::uint32_t value) noexcept
    {
        return value >= 0xD800u && value <= 0xDBFFu;
    }

    [[nodiscard]] constexpr bool IsLowSurrogate(std::uint32_t value) noexcept
    {
        return value >= 0xDC00u && value <= 0xDFFFu;
    }

    [[nodiscard]] std::uint32_t UpperInvariant(std::uint32_t codePoint) noexcept
    {
        for (auto const& range : UpperRanges)
        {
            if (codePoint >= range.first && codePoint <= range.last &&
                (codePoint - range.first) % range.step == 0)
            {
                auto const mapped = static_cast<std::int64_t>(codePoint) + range.delta;
                return static_cast<std::uint32_t>(mapped);
            }
        }

        auto const found = std::lower_bound(
            UpperPairs.begin(),
            UpperPairs.end(),
            codePoint,
            [](UpperPair const& pair, std::uint32_t value) { return pair.from < value; });
        return found != UpperPairs.end() && found->from == codePoint ? found->to : codePoint;
    }

    void AppendScalar(std::wstring& output, std::uint32_t codePoint)
    {
        if constexpr (sizeof(wchar_t) == 2)
        {
            if (codePoint > 0xFFFFu)
            {
                codePoint -= 0x10000u;
                output.push_back(static_cast<wchar_t>(0xD800u + (codePoint >> 10)));
                output.push_back(static_cast<wchar_t>(0xDC00u + (codePoint & 0x3FFu)));
            }
            else
            {
                output.push_back(static_cast<wchar_t>(codePoint));
            }
        }
        else
        {
            output.push_back(static_cast<wchar_t>(codePoint));
        }
    }

    [[nodiscard]] std::wstring ToUpperInvariant(std::wstring_view input)
    {
        std::wstring output;
        output.reserve(input.size());
        for (std::size_t index{}; index < input.size(); ++index)
        {
            auto codePoint = static_cast<std::uint32_t>(input[index]);
            if constexpr (sizeof(wchar_t) == 2)
            {
                if (IsHighSurrogate(codePoint) && index + 1 < input.size())
                {
                    auto const low = static_cast<std::uint32_t>(input[index + 1]);
                    if (IsLowSurrogate(low))
                    {
                        codePoint = 0x10000u + ((codePoint - 0xD800u) << 10) + (low - 0xDC00u);
                        ++index;
                    }
                }
            }
            AppendScalar(output, UpperInvariant(codePoint));
        }
        return output;
    }

    [[nodiscard]] std::vector<std::wstring> SplitLines(std::wstring_view text)
    {
        std::vector<std::wstring> lines;
        if (text.empty()) return lines;

        std::wstring current;
        for (std::size_t index{}; index < text.size(); ++index)
        {
            auto const value = text[index];
            if (value == L'\r' || value == L'\n')
            {
                lines.push_back(std::move(current));
                current.clear();
                if (value == L'\r' && index + 1 < text.size() && text[index + 1] == L'\n')
                {
                    ++index;
                }
            }
            else
            {
                current.push_back(value);
            }
        }
        lines.push_back(std::move(current));
        return lines;
    }

    [[nodiscard]] std::wstring Key(
        std::wstring_view line,
        bool ignoreWhitespace,
        bool ignoreCase)
    {
        std::wstring key;
        if (ignoreWhitespace)
        {
            key.reserve(line.size());
            for (auto const value : line)
            {
                if (!IsManagedWhitespace(static_cast<std::uint32_t>(value))) key.push_back(value);
            }
        }
        else
        {
            key.assign(line);
        }

        return ignoreCase ? ToUpperInvariant(key) : key;
    }

    void Emit(
        winforge::core::textdiff::DiffResult& result,
        winforge::core::textdiff::ChangeKind kind,
        std::wstring_view text)
    {
        using winforge::core::textdiff::ChangeKind;
        wchar_t prefix{};
        switch (kind)
        {
        case ChangeKind::Added:
            ++result.added;
            prefix = L'+';
            break;
        case ChangeKind::Removed:
            ++result.removed;
            prefix = L'-';
            break;
        default:
            ++result.unchanged;
            prefix = L' ';
            break;
        }
        result.lines.push_back({ kind, prefix, std::wstring(text) });
    }
}

namespace winforge::core::textdiff
{
    DiffResult Compute(
        std::wstring_view leftText,
        std::wstring_view rightText,
        bool ignoreWhitespace,
        bool ignoreCase)
    {
        DiffResult result;
        try
        {
            auto const left = SplitLines(leftText);
            auto const right = SplitLines(rightText);

            // This quotient form is equivalent to left * right > CellBudget,
            // while avoiding an unsigned multiplication overflow.
            if (!left.empty() && right.size() > CellBudget / left.size())
            {
                result.truncated = true;
                for (auto const& line : left) Emit(result, ChangeKind::Removed, line);
                for (auto const& line : right) Emit(result, ChangeKind::Added, line);
                return result;
            }

            std::vector<std::wstring> leftKeys;
            std::vector<std::wstring> rightKeys;
            leftKeys.reserve(left.size());
            rightKeys.reserve(right.size());
            for (auto const& line : left) leftKeys.push_back(Key(line, ignoreWhitespace, ignoreCase));
            for (auto const& line : right) rightKeys.push_back(Key(line, ignoreWhitespace, ignoreCase));

            auto const rowCount = left.size() + 1;
            auto const stride = right.size() + 1;
            std::vector<int> table(rowCount * stride);
            auto cell = [&table, stride](std::size_t row, std::size_t column) -> int&
            {
                return table[row * stride + column];
            };

            for (auto row = left.size(); row-- > 0;)
            {
                for (auto column = right.size(); column-- > 0;)
                {
                    cell(row, column) = leftKeys[row] == rightKeys[column]
                        ? cell(row + 1, column + 1) + 1
                        : std::max(cell(row + 1, column), cell(row, column + 1));
                }
            }

            std::size_t leftIndex{};
            std::size_t rightIndex{};
            while (leftIndex < left.size() && rightIndex < right.size())
            {
                if (leftKeys[leftIndex] == rightKeys[rightIndex])
                {
                    Emit(result, ChangeKind::Unchanged, left[leftIndex]);
                    ++leftIndex;
                    ++rightIndex;
                }
                else if (cell(leftIndex + 1, rightIndex) >= cell(leftIndex, rightIndex + 1))
                {
                    // The managed oracle deliberately resolves LCS ties as a removal.
                    Emit(result, ChangeKind::Removed, left[leftIndex]);
                    ++leftIndex;
                }
                else
                {
                    Emit(result, ChangeKind::Added, right[rightIndex]);
                    ++rightIndex;
                }
            }
            while (leftIndex < left.size()) Emit(result, ChangeKind::Removed, left[leftIndex++]);
            while (rightIndex < right.size()) Emit(result, ChangeKind::Added, right[rightIndex++]);
        }
        catch (...)
        {
            // Match the managed best-effort contract: keep accumulated output.
        }
        return result;
    }

    std::wstring ToUnifiedDiff(DiffResult const& result)
    {
        try
        {
            std::wstring output = L"--- A\n+++ B\n";
            for (auto const& line : result.lines)
            {
                output.push_back(line.prefix);
                output.append(line.text);
                output.push_back(L'\n');
            }
            return output;
        }
        catch (...)
        {
            return {};
        }
    }
}
