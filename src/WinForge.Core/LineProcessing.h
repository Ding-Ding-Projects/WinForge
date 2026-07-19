#pragma once

#include <cstddef>
#include <functional>
#include <string>
#include <string_view>

namespace winforge::core::lineprocessing
{
    struct LineCounts
    {
        std::size_t lines{};
        std::size_t characters{};
        std::size_t words{};
    };

    using RandomIndexProvider = std::function<std::size_t(std::size_t upperExclusive)>;

    [[nodiscard]] std::size_t SecureRandomIndex(std::size_t upperExclusive);
    [[nodiscard]] LineCounts Count(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring NumberLines(std::wstring_view text, bool parenthesized) noexcept;
    [[nodiscard]] std::wstring RemoveLineNumbers(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring AddPrefix(std::wstring_view text, std::wstring_view prefix) noexcept;
    [[nodiscard]] std::wstring AddSuffix(std::wstring_view text, std::wstring_view suffix) noexcept;
    [[nodiscard]] std::wstring WrapQuotes(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring JoinLines(std::wstring_view text, std::wstring_view delimiter) noexcept;
    [[nodiscard]] std::wstring SplitOn(std::wstring_view text, std::wstring_view delimiter) noexcept;
    [[nodiscard]] std::wstring ReverseCharacters(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring SortLines(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring ReverseOrder(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring Deduplicate(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring RemoveEmpty(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring TrimLines(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring ShuffleLines(
        std::wstring_view text,
        RandomIndexProvider const& randomIndex = {}) noexcept;

    enum class SortMode
    {
        None,
        Ascending,
        Descending,
        Natural,
    };

    struct TextSortOptions
    {
        SortMode mode{ SortMode::Ascending };
        bool caseInsensitive{};
        bool removeDuplicates{};
        bool trimBeforeCompare{};
        bool reverse{};
        bool shuffle{};
        bool removeBlank{};
        bool trimEach{};
    };

    struct TextSortResult
    {
        std::wstring text;
        std::size_t linesIn{};
        std::size_t linesOut{};
        std::size_t duplicatesRemoved{};
    };

    [[nodiscard]] TextSortResult TransformTextSort(
        std::wstring_view input,
        TextSortOptions const& options = {},
        RandomIndexProvider const& randomIndex = {}) noexcept;

    [[nodiscard]] std::wstring HardWrap(
        std::wstring_view text,
        int width,
        bool breakLongWords) noexcept;
    [[nodiscard]] std::wstring Unwrap(std::wstring_view text) noexcept;
    [[nodiscard]] std::wstring Reflow(
        std::wstring_view text,
        int width,
        bool breakLongWords) noexcept;
    [[nodiscard]] std::wstring AddPrefixEveryLine(
        std::wstring_view text,
        std::wstring_view prefix) noexcept;
    [[nodiscard]] std::wstring HangingIndent(std::wstring_view text, int spaces) noexcept;

    struct TextReadout
    {
        std::size_t longestLine{};
        std::size_t lines{};
        std::size_t characters{};
    };

    [[nodiscard]] TextReadout MeasureText(std::wstring_view text) noexcept;
}
