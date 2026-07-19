#pragma once

#include <cstddef>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace winforge::core::textanalysis
{
    struct WordCount
    {
        std::wstring word;
        int count{};
    };

    struct TextStats
    {
        int characters{};
        int charactersNoSpaces{};
        int words{};
        int uniqueWords{};
        int sentences{};
        int paragraphs{};
        int syllables{};
        double avgWordLength{};
        double avgSentenceLength{};
        double readingMinutes{};
        double speakingMinutes{};
        double fleschReadingEase{};
        double fleschKincaidGrade{};
        std::vector<WordCount> topWords;
    };

    [[nodiscard]] TextStats AnalyzeTextStats(
        std::wstring_view text,
        bool ignoreStopWords,
        int topN = 10) noexcept;
    [[nodiscard]] std::wstring FormatDuration(double minutes) noexcept;

    enum class FrequencyMode
    {
        Words,
        Bigrams,
        Characters,
    };

    struct WordFrequencyOptions
    {
        FrequencyMode mode{ FrequencyMode::Words };
        bool caseInsensitive{};
        int minLength{ 1 };
        bool stripPunctuation{};
        bool removeStopWords{};
    };

    struct FrequencyRow
    {
        int rank{};
        std::wstring term;
        int count{};
        double barWidth{};
        std::wstring percent;
    };

    struct WordFrequencyResult
    {
        std::vector<FrequencyRow> rows;
        int totalTokens{};
        int uniqueTokens{};
        double diversity{};
    };

    [[nodiscard]] WordFrequencyResult AnalyzeWordFrequency(
        std::wstring_view text,
        WordFrequencyOptions const& options = {}) noexcept;
    [[nodiscard]] std::wstring ToWordFrequencyCsv(
        WordFrequencyResult const& result) noexcept;

    inline constexpr std::size_t MaxStringCompareLength = 2'000;

    struct StringComparisonMetrics
    {
        int lenA{};
        int lenB{};
        bool truncated{};
        int levenshtein{};
        double similarityPct{};
        int damerau{};
        int hamming{};
        double jaroWinkler{};
        int longestCommonSubstring{};
        int longestCommonSubsequence{};
    };

    [[nodiscard]] StringComparisonMetrics ComputeStringComparison(
        std::wstring_view a,
        std::wstring_view b,
        bool ignoreCase,
        bool ignoreWhitespace) noexcept;

    [[nodiscard]] std::wstring BuildStringComparisonReport(
        std::vector<std::pair<std::wstring, std::wstring>> const& rows) noexcept;
}
