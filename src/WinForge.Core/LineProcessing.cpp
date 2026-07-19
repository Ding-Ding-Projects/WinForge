#include "LineProcessing.h"

#include "TextDiff.h"

#include <Windows.h>
#include <bcrypt.h>

#include <algorithm>
#include <array>
#include <bit>
#include <cstdint>
#include <limits>
#include <set>
#include <stdexcept>
#include <utility>
#include <vector>

#pragma comment(lib, "Bcrypt.lib")

namespace
{
    using winforge::core::lineprocessing::RandomIndexProvider;

    constexpr int MinimumWrapWidth = 1;
    constexpr int MaximumWrapWidth = 2000;

    [[nodiscard]] bool IsManagedWhitespace(wchar_t value) noexcept
    {
        return (value >= L'\u0009' && value <= L'\u000D') ||
            value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] bool IsManagedDigit(wchar_t value) noexcept
    {
        // Exact .NET 11 char.IsDigit(char) / DecimalDigitNumber BMP ranges.
        // The managed API operates on one UTF-16 code unit, so supplementary
        // digit scalars intentionally do not combine here.
        constexpr std::array<std::pair<wchar_t, wchar_t>, 37> ranges{
            std::pair{ L'\u0030', L'\u0039' }, std::pair{ L'\u0660', L'\u0669' },
            std::pair{ L'\u06F0', L'\u06F9' }, std::pair{ L'\u07C0', L'\u07C9' },
            std::pair{ L'\u0966', L'\u096F' }, std::pair{ L'\u09E6', L'\u09EF' },
            std::pair{ L'\u0A66', L'\u0A6F' }, std::pair{ L'\u0AE6', L'\u0AEF' },
            std::pair{ L'\u0B66', L'\u0B6F' }, std::pair{ L'\u0BE6', L'\u0BEF' },
            std::pair{ L'\u0C66', L'\u0C6F' }, std::pair{ L'\u0CE6', L'\u0CEF' },
            std::pair{ L'\u0D66', L'\u0D6F' }, std::pair{ L'\u0DE6', L'\u0DEF' },
            std::pair{ L'\u0E50', L'\u0E59' }, std::pair{ L'\u0ED0', L'\u0ED9' },
            std::pair{ L'\u0F20', L'\u0F29' }, std::pair{ L'\u1040', L'\u1049' },
            std::pair{ L'\u1090', L'\u1099' }, std::pair{ L'\u17E0', L'\u17E9' },
            std::pair{ L'\u1810', L'\u1819' }, std::pair{ L'\u1946', L'\u194F' },
            std::pair{ L'\u19D0', L'\u19D9' }, std::pair{ L'\u1A80', L'\u1A89' },
            std::pair{ L'\u1A90', L'\u1A99' }, std::pair{ L'\u1B50', L'\u1B59' },
            std::pair{ L'\u1BB0', L'\u1BB9' }, std::pair{ L'\u1C40', L'\u1C49' },
            std::pair{ L'\u1C50', L'\u1C59' }, std::pair{ L'\uA620', L'\uA629' },
            std::pair{ L'\uA8D0', L'\uA8D9' }, std::pair{ L'\uA900', L'\uA909' },
            std::pair{ L'\uA9D0', L'\uA9D9' }, std::pair{ L'\uA9F0', L'\uA9F9' },
            std::pair{ L'\uAA50', L'\uAA59' }, std::pair{ L'\uABF0', L'\uABF9' },
            std::pair{ L'\uFF10', L'\uFF19' }
        };
        for (auto const [first, last] : ranges)
        {
            if (value >= first && value <= last) return true;
        }
        return false;
    }

    [[nodiscard]] std::wstring_view TrimView(std::wstring_view value) noexcept
    {
        while (!value.empty() && IsManagedWhitespace(value.front())) value.remove_prefix(1);
        while (!value.empty() && IsManagedWhitespace(value.back())) value.remove_suffix(1);
        return value;
    }

    [[nodiscard]] std::wstring TrimCopy(std::wstring_view value)
    {
        return std::wstring(TrimView(value));
    }

    [[nodiscard]] std::vector<std::wstring> SplitLogicalLines(
        std::wstring_view text,
        bool emptyIsOneLine,
        bool dropOneTrailingEmpty)
    {
        std::vector<std::wstring> lines;
        if (text.empty())
        {
            if (emptyIsOneLine) lines.emplace_back();
            return lines;
        }

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
        if (dropOneTrailingEmpty && !lines.empty() && lines.back().empty()) lines.pop_back();
        return lines;
    }

    [[nodiscard]] std::wstring Join(
        std::vector<std::wstring> const& lines,
        std::wstring_view delimiter)
    {
        if (lines.empty()) return {};
        std::size_t capacity = delimiter.size() * (lines.size() - 1);
        for (auto const& line : lines) capacity += line.size();
        std::wstring result;
        result.reserve(capacity);
        for (std::size_t index{}; index < lines.size(); ++index)
        {
            if (index != 0) result.append(delimiter);
            result.append(lines[index]);
        }
        return result;
    }

    [[nodiscard]] std::wstring Fold(std::wstring_view value)
    {
        return winforge::core::textdiff::FoldOrdinalIgnoreCase(value);
    }

    [[nodiscard]] bool IsHighSurrogate(wchar_t value) noexcept
    {
        return value >= static_cast<wchar_t>(0xD800u) &&
            value <= static_cast<wchar_t>(0xDBFFu);
    }

    [[nodiscard]] bool IsLowSurrogate(wchar_t value) noexcept
    {
        return value >= static_cast<wchar_t>(0xDC00u) &&
            value <= static_cast<wchar_t>(0xDFFFu);
    }

    [[nodiscard]] std::uint32_t DecodeSurrogatePair(wchar_t high, wchar_t low) noexcept
    {
        return 0x10000u +
            ((static_cast<std::uint32_t>(high) - 0xD800u) << 10) +
            (static_cast<std::uint32_t>(low) - 0xDC00u);
    }

    [[nodiscard]] int CompareFoldedOrdinalIgnoreCase(
        std::wstring_view left,
        std::wstring_view right) noexcept
    {
        auto const commonLength = (std::min)(left.size(), right.size());
        std::size_t index{};
        while (index < commonLength)
        {
            auto const leftPair = IsHighSurrogate(left[index]) &&
                index + 1 < left.size() && IsLowSurrogate(left[index + 1]);
            auto const rightPair = IsHighSurrogate(right[index]) &&
                index + 1 < right.size() && IsLowSurrogate(right[index + 1]);
            if (!leftPair)
            {
                if (rightPair) return -1;
                if (left[index] != right[index])
                {
                    return static_cast<std::uint16_t>(left[index]) <
                        static_cast<std::uint16_t>(right[index]) ? -1 : 1;
                }
                ++index;
                continue;
            }
            if (!rightPair) return 1;

            auto const leftScalar = DecodeSurrogatePair(left[index], left[index + 1]);
            auto const rightScalar = DecodeSurrogatePair(right[index], right[index + 1]);
            if (leftScalar != rightScalar) return leftScalar < rightScalar ? -1 : 1;
            index += 2;
        }
        if (left.size() == right.size()) return 0;
        return left.size() < right.size() ? -1 : 1;
    }

    [[nodiscard]] wchar_t UpperInvariantCodeUnit(wchar_t value) noexcept
    {
        return winforge::core::textdiff::ToUpperInvariantCodeUnit(value);
    }

    [[nodiscard]] int NaturalCompare(
        std::wstring_view left,
        std::wstring_view right,
        bool caseInsensitive) noexcept
    {
        std::size_t leftIndex{};
        std::size_t rightIndex{};
        while (leftIndex < left.size() && rightIndex < right.size())
        {
            auto const leftCharacter = left[leftIndex];
            auto const rightCharacter = right[rightIndex];
            auto const leftDigit = IsManagedDigit(leftCharacter);
            auto const rightDigit = IsManagedDigit(rightCharacter);
            if (leftDigit && rightDigit)
            {
                auto const leftRunStart = leftIndex;
                auto const rightRunStart = rightIndex;
                while (leftIndex < left.size() && left[leftIndex] == L'0') ++leftIndex;
                while (rightIndex < right.size() && right[rightIndex] == L'0') ++rightIndex;

                auto leftRunEnd = leftIndex;
                auto rightRunEnd = rightIndex;
                while (leftRunEnd < left.size() && IsManagedDigit(left[leftRunEnd])) ++leftRunEnd;
                while (rightRunEnd < right.size() && IsManagedDigit(right[rightRunEnd])) ++rightRunEnd;

                auto const leftLength = leftRunEnd - leftIndex;
                auto const rightLength = rightRunEnd - rightIndex;
                if (leftLength != rightLength) return leftLength < rightLength ? -1 : 1;
                for (std::size_t offset{}; offset < leftLength; ++offset)
                {
                    if (left[leftIndex + offset] != right[rightIndex + offset])
                    {
                        return left[leftIndex + offset] < right[rightIndex + offset] ? -1 : 1;
                    }
                }

                auto const leftZeros = leftIndex - leftRunStart;
                auto const rightZeros = rightIndex - rightRunStart;
                if (leftZeros != rightZeros) return leftZeros < rightZeros ? -1 : 1;
                leftIndex = leftRunEnd;
                rightIndex = rightRunEnd;
            }
            else
            {
                auto const foldedLeft = caseInsensitive
                    ? UpperInvariantCodeUnit(leftCharacter)
                    : leftCharacter;
                auto const foldedRight = caseInsensitive
                    ? UpperInvariantCodeUnit(rightCharacter)
                    : rightCharacter;
                if (foldedLeft != foldedRight) return foldedLeft < foldedRight ? -1 : 1;
                ++leftIndex;
                ++rightIndex;
            }
        }

        auto const leftRemaining = left.size() - leftIndex;
        auto const rightRemaining = right.size() - rightIndex;
        if (leftRemaining == rightRemaining) return 0;
        return leftRemaining < rightRemaining ? -1 : 1;
    }

    template<typename T>
    void Shuffle(std::vector<T>& values, RandomIndexProvider const& randomIndex)
    {
        for (std::size_t count = values.size(); count > 1; --count)
        {
            auto index = randomIndex
                ? randomIndex(count)
                : winforge::core::lineprocessing::SecureRandomIndex(count);
            if (index >= count) index %= count;
            std::swap(values[count - 1], values[index]);
        }
    }

    struct FoldedLine
    {
        std::wstring text;
        std::wstring key;
    };

    [[nodiscard]] std::vector<FoldedLine> Decorate(
        std::vector<std::wstring> lines,
        bool caseInsensitive)
    {
        std::vector<FoldedLine> decorated;
        decorated.reserve(lines.size());
        for (auto& line : lines)
        {
            auto key = caseInsensitive ? Fold(line) : line;
            decorated.push_back({ std::move(line), std::move(key) });
        }
        return decorated;
    }

    [[nodiscard]] std::vector<std::wstring> Undecorate(std::vector<FoldedLine> decorated)
    {
        std::vector<std::wstring> lines;
        lines.reserve(decorated.size());
        for (auto& item : decorated) lines.push_back(std::move(item.text));
        return lines;
    }

    // List<T>.Sort currently delegates to CoreLib's ArraySortHelper introsort.
    // Reproduce that algorithm so comparator-equal lines have the same observable
    // ordering as the managed oracle instead of inheriting an STL-specific order.
    constexpr int CoreLibIntrosortThreshold = 16;

    template<typename T, typename Compare>
    void SwapIfGreater(
        std::vector<T>& values,
        std::size_t offset,
        Compare const& compare,
        int left,
        int right)
    {
        if (compare(values[offset + left], values[offset + right]) > 0)
        {
            std::swap(values[offset + left], values[offset + right]);
        }
    }

    template<typename T, typename Compare>
    void InsertionSort(
        std::vector<T>& values,
        std::size_t offset,
        int length,
        Compare const& compare)
    {
        for (int index = 0; index < length - 1; ++index)
        {
            T candidate = std::move(values[offset + index + 1]);
            int insertion = index;
            while (insertion >= 0 && compare(candidate, values[offset + insertion]) < 0)
            {
                values[offset + insertion + 1] = std::move(values[offset + insertion]);
                --insertion;
            }
            values[offset + insertion + 1] = std::move(candidate);
        }
    }

    template<typename T, typename Compare>
    void DownHeap(
        std::vector<T>& values,
        std::size_t offset,
        int index,
        int length,
        Compare const& compare)
    {
        T candidate = std::move(values[offset + index - 1]);
        while (index <= length >> 1)
        {
            int child = 2 * index;
            if (child < length &&
                compare(values[offset + child - 1], values[offset + child]) < 0)
            {
                ++child;
            }
            if (!(compare(candidate, values[offset + child - 1]) < 0)) break;
            values[offset + index - 1] = std::move(values[offset + child - 1]);
            index = child;
        }
        values[offset + index - 1] = std::move(candidate);
    }

    template<typename T, typename Compare>
    void HeapSort(
        std::vector<T>& values,
        std::size_t offset,
        int length,
        Compare const& compare)
    {
        for (int index = length >> 1; index >= 1; --index)
        {
            DownHeap(values, offset, index, length, compare);
        }
        for (int index = length; index > 1; --index)
        {
            std::swap(values[offset], values[offset + index - 1]);
            DownHeap(values, offset, 1, index - 1, compare);
        }
    }

    template<typename T, typename Compare>
    [[nodiscard]] int PickPivotAndPartition(
        std::vector<T>& values,
        std::size_t offset,
        int length,
        Compare const& compare)
    {
        int const high = length - 1;
        int const middle = high >> 1;
        SwapIfGreater(values, offset, compare, 0, middle);
        SwapIfGreater(values, offset, compare, 0, high);
        SwapIfGreater(values, offset, compare, middle, high);
        T pivot = values[offset + middle];
        std::swap(values[offset + middle], values[offset + high - 1]);
        int left = 0;
        int right = high - 1;
        while (left < right)
        {
            while (compare(values[offset + ++left], pivot) < 0) {}
            while (compare(pivot, values[offset + --right]) < 0) {}
            if (left >= right) break;
            std::swap(values[offset + left], values[offset + right]);
        }
        if (left != high - 1)
        {
            std::swap(values[offset + left], values[offset + high - 1]);
        }
        return left;
    }

    template<typename T, typename Compare>
    void IntroSort(
        std::vector<T>& values,
        std::size_t offset,
        int length,
        int depthLimit,
        Compare const& compare)
    {
        int partitionSize = length;
        while (partitionSize > 1)
        {
            if (partitionSize <= CoreLibIntrosortThreshold)
            {
                if (partitionSize == 2)
                {
                    SwapIfGreater(values, offset, compare, 0, 1);
                    return;
                }
                if (partitionSize == 3)
                {
                    SwapIfGreater(values, offset, compare, 0, 1);
                    SwapIfGreater(values, offset, compare, 0, 2);
                    SwapIfGreater(values, offset, compare, 1, 2);
                    return;
                }
                InsertionSort(values, offset, partitionSize, compare);
                return;
            }
            if (depthLimit == 0)
            {
                HeapSort(values, offset, partitionSize, compare);
                return;
            }
            --depthLimit;
            int const pivot = PickPivotAndPartition(values, offset, partitionSize, compare);
            IntroSort(
                values,
                offset + static_cast<std::size_t>(pivot + 1),
                partitionSize - pivot - 1,
                depthLimit,
                compare);
            partitionSize = pivot;
        }
    }

    template<typename T, typename Compare>
    void CoreLibSort(std::vector<T>& values, Compare const& compare)
    {
        if (values.size() <= 1) return;
        if (values.size() > static_cast<std::size_t>((std::numeric_limits<int>::max)()))
        {
            throw std::length_error("CoreLib-compatible sort exceeds Int32 indexing");
        }
        auto const length = static_cast<int>(values.size());
        int const depthLimit = 2 * static_cast<int>(std::bit_width(static_cast<unsigned int>(length)));
        IntroSort(values, 0, length, depthLimit, compare);
    }

    using Paragraph = std::vector<std::wstring>;

    [[nodiscard]] std::vector<Paragraph> Paragraphs(std::wstring_view text)
    {
        std::vector<Paragraph> result;
        Paragraph current;
        for (auto& line : SplitLogicalLines(text, true, false))
        {
            if (TrimView(line).empty())
            {
                result.push_back(std::move(current));
                current.clear();
            }
            else
            {
                current.push_back(std::move(line));
            }
        }
        result.push_back(std::move(current));
        return result;
    }

    [[nodiscard]] int ClampWidth(int width) noexcept
    {
        return std::clamp(width, MinimumWrapWidth, MaximumWrapWidth);
    }

    [[nodiscard]] std::vector<std::wstring> SplitWords(std::wstring_view text)
    {
        std::vector<std::wstring> words;
        std::size_t index{};
        while (index < text.size())
        {
            while (index < text.size() && (text[index] == L' ' || text[index] == L'\t')) ++index;
            auto const start = index;
            while (index < text.size() && text[index] != L' ' && text[index] != L'\t') ++index;
            if (start != index) words.emplace_back(text.substr(start, index - start));
        }
        return words;
    }

    [[nodiscard]] std::vector<std::wstring> WrapOne(
        std::wstring_view text,
        int width,
        bool breakLongWords)
    {
        width = ClampWidth(width);
        auto const widthValue = static_cast<std::size_t>(width);
        std::vector<std::wstring> lines;
        std::wstring current;
        for (auto word : SplitWords(text))
        {
            if (breakLongWords && word.size() > widthValue)
            {
                if (!current.empty())
                {
                    lines.push_back(std::move(current));
                    current.clear();
                }
                while (word.size() > widthValue)
                {
                    lines.emplace_back(word.substr(0, widthValue));
                    word.erase(0, widthValue);
                }
                if (!word.empty()) current.append(word);
                continue;
            }

            if (current.empty())
            {
                current.append(word);
            }
            else if (current.size() + 1 + word.size() <= widthValue)
            {
                current.push_back(L' ');
                current.append(word);
            }
            else
            {
                lines.push_back(std::move(current));
                current = std::move(word);
            }
        }
        if (!current.empty()) lines.push_back(std::move(current));
        return lines;
    }

    [[nodiscard]] std::wstring JoinTrimmedParagraph(Paragraph const& paragraph)
    {
        std::vector<std::wstring> trimmed;
        trimmed.reserve(paragraph.size());
        for (auto const& line : paragraph) trimmed.push_back(TrimCopy(line));
        return Join(trimmed, L" ");
    }
}

namespace winforge::core::lineprocessing
{
    std::size_t SecureRandomIndex(std::size_t upperExclusive)
    {
        if (upperExclusive <= 1) return 0;
        auto const bound = static_cast<std::uint64_t>(upperExclusive);
        auto const maximum = (std::numeric_limits<std::uint64_t>::max)();
        auto const acceptedLimit = maximum - (maximum % bound);
        std::uint64_t value{};
        do
        {
            if (BCryptGenRandom(
                nullptr,
                reinterpret_cast<PUCHAR>(&value),
                static_cast<ULONG>(sizeof(value)),
                BCRYPT_USE_SYSTEM_PREFERRED_RNG) < 0)
            {
                throw std::runtime_error("BCryptGenRandom failed");
            }
        } while (value >= acceptedLimit);
        return static_cast<std::size_t>(value % bound);
    }

    LineCounts Count(std::wstring_view text) noexcept
    {
        try
        {
            if (text.empty()) return {};
            LineCounts result;
            result.lines = SplitLogicalLines(text, false, false).size();
            result.characters = text.size();
            bool insideWord = false;
            for (auto const value : text)
            {
                auto const separator = value == L' ' || value == L'\t' || value == L'\r' || value == L'\n';
                if (separator)
                {
                    insideWord = false;
                }
                else if (!insideWord)
                {
                    insideWord = true;
                    ++result.words;
                }
            }
            return result;
        }
        catch (...)
        {
            return {};
        }
    }

    std::wstring NumberLines(std::wstring_view text, bool parenthesized) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            for (std::size_t index{}; index < lines.size(); ++index)
            {
                lines[index] = std::to_wstring(index + 1) +
                    (parenthesized ? L") " : L". ") + lines[index];
            }
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring RemoveLineNumbers(std::wstring_view text) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            for (auto& line : lines)
            {
                std::size_t prefix{};
                while (prefix < line.size() && (line[prefix] == L' ' || line[prefix] == L'\t')) ++prefix;
                auto digitEnd = prefix;
                while (digitEnd < line.size() && IsManagedDigit(line[digitEnd])) ++digitEnd;
                if (digitEnd == prefix) continue;

                auto separatorEnd = digitEnd;
                if (separatorEnd < line.size() &&
                    (line[separatorEnd] == L'.' || line[separatorEnd] == L')' || line[separatorEnd] == L':'))
                {
                    ++separatorEnd;
                }
                if (separatorEnd < line.size() &&
                    (line[separatorEnd] == L' ' || line[separatorEnd] == L'\t'))
                {
                    while (separatorEnd < line.size() &&
                        (line[separatorEnd] == L' ' || line[separatorEnd] == L'\t')) ++separatorEnd;
                    line.erase(0, separatorEnd);
                }
                else if (separatorEnd > digitEnd)
                {
                    line.erase(0, separatorEnd);
                }
            }
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring AddPrefix(std::wstring_view text, std::wstring_view prefix) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            for (auto& line : lines) line.insert(0, prefix);
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring AddSuffix(std::wstring_view text, std::wstring_view suffix) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            for (auto& line : lines) line.append(suffix);
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring WrapQuotes(std::wstring_view text) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            for (auto& line : lines) line = L"\"" + line + L"\"";
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring JoinLines(std::wstring_view text, std::wstring_view delimiter) noexcept
    {
        try
        {
            return Join(SplitLogicalLines(text, false, false), delimiter);
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring SplitOn(std::wstring_view text, std::wstring_view delimiter) noexcept
    {
        try
        {
            if (text.empty()) return {};
            if (delimiter.empty()) return std::wstring(text);
            std::vector<std::wstring> pieces;
            std::size_t start{};
            while (start <= text.size())
            {
                auto const separator = text.find(delimiter, start);
                if (separator == std::wstring_view::npos)
                {
                    pieces.emplace_back(text.substr(start));
                    break;
                }
                pieces.emplace_back(text.substr(start, separator - start));
                start = separator + delimiter.size();
            }
            return Join(pieces, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring ReverseCharacters(std::wstring_view text) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            for (auto& line : lines) std::reverse(line.begin(), line.end());
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring SortLines(std::wstring_view text) noexcept
    {
        try
        {
            auto decorated = Decorate(SplitLogicalLines(text, false, false), true);
            CoreLibSort(decorated, [](FoldedLine const& left, FoldedLine const& right)
            {
                return CompareFoldedOrdinalIgnoreCase(left.key, right.key);
            });
            return Join(Undecorate(std::move(decorated)), L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring ReverseOrder(std::wstring_view text) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            std::reverse(lines.begin(), lines.end());
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring Deduplicate(std::wstring_view text) noexcept
    {
        try
        {
            std::set<std::wstring> seen;
            std::vector<std::wstring> output;
            for (auto& line : SplitLogicalLines(text, false, false))
            {
                if (seen.insert(Fold(line)).second) output.push_back(std::move(line));
            }
            return Join(output, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring RemoveEmpty(std::wstring_view text) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            std::erase_if(lines, [](std::wstring const& line) { return TrimView(line).empty(); });
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring TrimLines(std::wstring_view text) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            for (auto& line : lines) line = TrimCopy(line);
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring ShuffleLines(
        std::wstring_view text,
        RandomIndexProvider const& randomIndex) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, false, false);
            Shuffle(lines, randomIndex);
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    TextSortResult TransformTextSort(
        std::wstring_view input,
        TextSortOptions const& options,
        RandomIndexProvider const& randomIndex) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(input, false, true);
            TextSortResult result;
            result.linesIn = lines.size();

            if (options.trimEach)
            {
                for (auto& line : lines) line = TrimCopy(line);
            }
            if (options.removeBlank)
            {
                std::erase_if(lines, [](std::wstring const& line) { return TrimView(line).empty(); });
            }
            if (options.removeDuplicates)
            {
                std::set<std::wstring> seen;
                std::vector<std::wstring> kept;
                kept.reserve(lines.size());
                for (auto& line : lines)
                {
                    auto const rawKey = options.trimBeforeCompare ? TrimView(line) : std::wstring_view(line);
                    auto key = options.caseInsensitive ? Fold(rawKey) : std::wstring(rawKey);
                    if (seen.insert(std::move(key)).second)
                    {
                        kept.push_back(std::move(line));
                    }
                    else
                    {
                        ++result.duplicatesRemoved;
                    }
                }
                lines = std::move(kept);
            }

            if (options.mode == SortMode::Natural)
            {
                CoreLibSort(lines, [&](std::wstring const& left, std::wstring const& right)
                {
                    return NaturalCompare(left, right, options.caseInsensitive);
                });
            }
            else if (options.mode == SortMode::Ascending || options.mode == SortMode::Descending)
            {
                auto decorated = Decorate(std::move(lines), options.caseInsensitive);
                CoreLibSort(decorated, [&](FoldedLine const& left, FoldedLine const& right)
                {
                    if (options.mode == SortMode::Ascending)
                    {
                        return options.caseInsensitive
                            ? CompareFoldedOrdinalIgnoreCase(left.key, right.key)
                            : left.key.compare(right.key);
                    }
                    return options.caseInsensitive
                        ? CompareFoldedOrdinalIgnoreCase(right.key, left.key)
                        : right.key.compare(left.key);
                });
                lines = Undecorate(std::move(decorated));
            }

            if (options.reverse) std::reverse(lines.begin(), lines.end());
            if (options.shuffle)
            {
                try
                {
                    Shuffle(lines, randomIndex);
                }
                catch (...)
                {
                    // The managed oracle keeps the transformed list, including any
                    // Fisher-Yates swaps completed before its RNG failed.
                }
            }

            result.linesOut = lines.size();
            result.text = Join(lines, L"\r\n");
            return result;
        }
        catch (...)
        {
            return { std::wstring(input), 0, 0, 0 };
        }
    }

    std::wstring HardWrap(std::wstring_view text, int width, bool breakLongWords) noexcept
    {
        try
        {
            width = ClampWidth(width);
            std::vector<std::wstring> output;
            for (auto const& paragraph : Paragraphs(text))
            {
                if (paragraph.empty())
                {
                    output.emplace_back();
                    continue;
                }
                output.push_back(Join(WrapOne(JoinTrimmedParagraph(paragraph), width, breakLongWords), L"\n"));
            }
            return Join(output, L"\n\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring Unwrap(std::wstring_view text) noexcept
    {
        try
        {
            std::vector<std::wstring> output;
            for (auto const& paragraph : Paragraphs(text))
            {
                output.push_back(paragraph.empty() ? std::wstring{} : JoinTrimmedParagraph(paragraph));
            }
            return Join(output, L"\n\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring Reflow(std::wstring_view text, int width, bool breakLongWords) noexcept
    {
        try
        {
            return HardWrap(Unwrap(text), width, breakLongWords);
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring AddPrefixEveryLine(std::wstring_view text, std::wstring_view prefix) noexcept
    {
        try
        {
            auto lines = SplitLogicalLines(text, true, false);
            for (auto& line : lines) line.insert(0, prefix);
            return Join(lines, L"\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    std::wstring HangingIndent(std::wstring_view text, int spaces) noexcept
    {
        try
        {
            spaces = std::clamp(spaces, 0, MaximumWrapWidth);
            std::wstring const padding(static_cast<std::size_t>(spaces), L' ');
            std::vector<std::wstring> output;
            for (auto const& paragraph : Paragraphs(text))
            {
                if (paragraph.empty())
                {
                    output.emplace_back();
                    continue;
                }
                std::vector<std::wstring> lines;
                lines.reserve(paragraph.size());
                for (std::size_t index{}; index < paragraph.size(); ++index)
                {
                    lines.push_back(index == 0 ? paragraph[index] : padding + paragraph[index]);
                }
                output.push_back(Join(lines, L"\n"));
            }
            return Join(output, L"\n\n");
        }
        catch (...)
        {
            return std::wstring(text);
        }
    }

    TextReadout MeasureText(std::wstring_view text) noexcept
    {
        try
        {
            TextReadout result;
            result.characters = text.size();
            auto const lines = SplitLogicalLines(text, true, false);
            result.lines = lines.size();
            for (auto const& line : lines) result.longestLine = (std::max)(result.longestLine, line.size());
            return result;
        }
        catch (...)
        {
            return {};
        }
    }
}
