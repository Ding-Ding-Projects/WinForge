#include "LineProcessingTests.h"

#include "LineProcessing.h"

#include <algorithm>
#include <climits>
#include <cstdint>
#include <iostream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace
{
    struct Suite
    {
        NativeTestCounts counts;

        void Expect(bool condition, std::string_view name)
        {
            if (condition)
            {
                ++counts.passed;
                std::cout << "PASS " << name << '\n';
            }
            else
            {
                ++counts.failed;
                std::cerr << "FAIL " << name << '\n';
            }
        }
    };

    [[nodiscard]] std::wstring Scalar(std::uint32_t codePoint)
    {
        std::wstring result;
        if constexpr (sizeof(wchar_t) == 2)
        {
            if (codePoint > 0xFFFFu)
            {
                codePoint -= 0x10000u;
                result.push_back(static_cast<wchar_t>(0xD800u + (codePoint >> 10)));
                result.push_back(static_cast<wchar_t>(0xDC00u + (codePoint & 0x3FFu)));
            }
            else
            {
                result.push_back(static_cast<wchar_t>(codePoint));
            }
        }
        else
        {
            result.push_back(static_cast<wchar_t>(codePoint));
        }
        return result;
    }

    [[nodiscard]] winforge::core::lineprocessing::RandomIndexProvider Scripted(
        std::vector<std::size_t> choices)
    {
        return [choices = std::move(choices), index = std::size_t{}](std::size_t bound) mutable
        {
            auto const choice = index < choices.size() ? choices[index++] : 0;
            return bound == 0 ? 0 : choice % bound;
        };
    }
}

NativeTestCounts RunLineProcessingTests()
{
    using namespace winforge::core::lineprocessing;
    Suite suite;

    auto const emptyCount = Count(L"");
    suite.Expect(emptyCount.lines == 0 && emptyCount.characters == 0 && emptyCount.words == 0,
        "line tools count treats empty input as zero lines");
    auto const mixedCount = Count(L"a\r\nb\rc\n");
    suite.Expect(mixedCount.lines == 4 && mixedCount.characters == 7 && mixedCount.words == 3,
        "line tools count preserves raw CRLF character width and trailing records");
    auto const emoji = Scalar(0x1F600u);
    auto const emojiCount = Count(emoji + L" x");
    suite.Expect(emojiCount.lines == 1 && emojiCount.characters == 4 && emojiCount.words == 2,
        "line tools count uses UTF-16 code units and ASCII word separators");
    auto const unicodeSeparatorCount = Count(L"a\u00A0b\vc");
    suite.Expect(unicodeSeparatorCount.words == 1,
        "line tools word count keeps non-ASCII managed whitespace inside a word");

    suite.Expect(NumberLines(L"a\n", false) == L"1. a\n2. ",
        "line tools dot numbering includes a trailing empty line");
    suite.Expect(NumberLines(L"\r\nb", true) == L"1) \n2) b",
        "line tools parenthesized numbering normalizes mixed newlines");
    suite.Expect(RemoveLineNumbers(
        L"  12. alpha\n3)beta\n4: gamma\n5 delta\n123\n  7\tz") ==
        L"alpha\nbeta\ngamma\ndelta\n123\nz",
        "line tools number removal preserves the oracle punctuation and whitespace rules");
    suite.Expect(RemoveLineNumbers(L"1.23\n12:30\n12abc\nabc12") == L"23\n30\n12abc\nabc12",
        "line tools number removal retains its punctuation-without-space quirks");
    suite.Expect(RemoveLineNumbers(L"\u00B2. x\n\u0DEC. y\n\u0661. z\n\uFF11.w") ==
        L"\u00B2. x\ny\nz\nw",
        "line tools uses the exact managed DecimalDigitNumber set");

    suite.Expect(AddPrefix(L"a\n", L"x") == L"xa\nx",
        "line tools prefix applies to trailing blank records");
    suite.Expect(AddSuffix(L"a\n", L"x") == L"ax\nx",
        "line tools suffix applies to trailing blank records");
    suite.Expect(WrapQuotes(L"a\n") == L"\"a\"\n\"\"",
        "line tools quotes every logical line without escaping");
    suite.Expect(JoinLines(L"a\r\nb\n", L", ") == L"a, b, ",
        "line tools joins normalized lines with the literal delimiter");
    suite.Expect(JoinLines(L"a\nb", L"") == L"ab",
        "line tools accepts an empty join delimiter");
    suite.Expect(SplitOn(L"a,,b,", L",") == L"a\n\nb\n",
        "line tools split preserves consecutive and trailing empty pieces");
    suite.Expect(SplitOn(L"a\r\nb", L"") == L"a\r\nb" && SplitOn(L"", L",").empty(),
        "line tools empty-delimiter and empty-input split paths are exact passthroughs");

    std::wstring surrogateLine = emoji + L"A";
    std::wstring reversedSurrogate{ L'A', emoji[1], emoji[0] };
    suite.Expect(ReverseCharacters(surrogateLine) == reversedSurrogate,
        "line tools reverse operates on UTF-16 code units like Array.Reverse(char[])");
    suite.Expect(ReverseCharacters(L"ab\r\ncd") == L"ba\ndc",
        "line tools reverse is per-line and normalizes newlines");
    suite.Expect(SortLines(L"b\nA\na") == L"A\na\nb",
        "line tools performs ordinal ignore-case ascending sort");
    auto const introsortInput =
        L"aaaaa\nAaaaa\naAaaa\nAAaaa\naaAaa\nAaAaa\naAAaa\nAAAaa\naaaAa\nAaaAa\naAaAa\n"
        L"AAaAa\naaAAa\nAaAAa\naAAAa\nAAAAa\naaaaA";
    auto const introsortOutput =
        L"aaaaa\naAAAa\nAaAAa\naaAAa\nAAaAa\naAaAa\nAaaAa\nAAAAa\naaaAa\naAAaa\nAaAaa\n"
        L"aaAaa\nAAaaa\naAaaa\nAaaaa\nAAAaa\naaaaA";
    suite.Expect(SortLines(introsortInput) == introsortOutput,
        "line tools reproduces CoreLib introsort ordering at the sixteen-line boundary");
    suite.Expect(SortLines(L"\uE000\n" + Scalar(0x10000u)) == L"\uE000\n" + Scalar(0x10000u),
        "line tools ordinal ignore-case sorts valid surrogate pairs after non-pairs");
    suite.Expect(ReverseOrder(L"a\r\nb\n") == L"\nb\na",
        "line tools reverse order retains the trailing empty record");
    suite.Expect(Deduplicate(L"Alpha\nalpha\n ALPHA\n\n") == L"Alpha\n ALPHA\n",
        "line tools dedupe keeps the first ordinal ignore-case spelling");
    suite.Expect(Deduplicate(L"\u017F\ns\nS") == L"\u017F\ns",
        "line tools ordinal ignore-case keeps long s distinct from s");
    suite.Expect(Deduplicate(L"\u03C2\n\u03C3\n\u00B5\n\u03BC") == L"\u03C2\n\u00B5",
        "line tools ordinal ignore-case folds final sigma and micro sign exactly");
    auto const deseretUpper = Scalar(0x10400u);
    auto const deseretLower = Scalar(0x10428u);
    suite.Expect(Deduplicate(deseretLower + L"\n" + deseretUpper) == deseretLower,
        "line tools ordinal ignore-case folds supplementary Deseret case pairs");
    suite.Expect(RemoveEmpty(L"a\n\u00A0\n\u180E\nb") == L"a\n\u180E\nb",
        "line tools removes full managed whitespace but retains obsolete U+180E");
    suite.Expect(TrimLines(L"\u00A0a\u3000\n\u200Bb\u200B") == L"a\n\u200Bb\u200B",
        "line tools trim uses managed Unicode whitespace and retains zero-width space");
    suite.Expect(ShuffleLines(L"a\nb\n", Scripted({ 0, 0 })) == L"b\n\na",
        "line tools exposes deterministic Fisher-Yates bounds for tests");
    bool secureBounds = true;
    for (std::size_t attempt{}; attempt < 128; ++attempt)
    {
        secureBounds = secureBounds && SecureRandomIndex(7) < 7;
    }
    suite.Expect(secureBounds && SecureRandomIndex(0) == 0 && SecureRandomIndex(1) == 0,
        "line tools production random provider stays within every requested bound");

    TextSortOptions options;
    auto sorted = TransformTextSort(L"b\na", options);
    suite.Expect(sorted.text == L"a\r\nb" && sorted.linesIn == 2 && sorted.linesOut == 2 &&
        sorted.duplicatesRemoved == 0,
        "text sort defaults to ascending CRLF output");
    options.mode = SortMode::None;
    auto trailingSort = TransformTextSort(L"a\n\n", options);
    suite.Expect(trailingSort.text == L"a\r\n" && trailingSort.linesIn == 2 && trailingSort.linesOut == 2,
        "text sort drops exactly one trailing empty record");
    auto loneNewline = TransformTextSort(L"\n", options);
    suite.Expect(loneNewline.text.empty() && loneNewline.linesIn == 1 && loneNewline.linesOut == 1,
        "text sort distinguishes one empty logical line from empty input");
    auto emptySort = TransformTextSort(L"", options);
    suite.Expect(emptySort.text.empty() && emptySort.linesIn == 0 && emptySort.linesOut == 0,
        "text sort empty input contains no logical lines");
    auto mixedSort = TransformTextSort(L"a\r\nb\rc\n", options);
    suite.Expect(mixedSort.text == L"a\r\nb\r\nc" && mixedSort.linesIn == 3,
        "text sort accepts CRLF lone CR and LF while dropping the final empty line");

    options.mode = SortMode::Descending;
    suite.Expect(TransformTextSort(L"b\na\nc", options).text == L"c\r\nb\r\na",
        "text sort descending reverses comparator operands");
    options = {};
    options.caseInsensitive = true;
    auto const introsortTextSort = TransformTextSort(introsortInput, options);
    std::wstring expectedTextSort = introsortOutput;
    for (std::size_t position{}; (position = expectedTextSort.find(L'\n', position)) != std::wstring::npos; position += 2)
    {
        expectedTextSort.replace(position, 1, L"\r\n");
    }
    suite.Expect(introsortTextSort.text == expectedTextSort,
        "text sort reproduces managed comparator introsort tie ordering");
    suite.Expect(TransformTextSort(L"\uE000\n" + Scalar(0x10000u), options).text ==
        L"\uE000\r\n" + Scalar(0x10000u),
        "text sort ordinal ignore-case preserves scalar-aware surrogate ordering");
    options.mode = SortMode::Descending;
    suite.Expect(TransformTextSort(L"\uE000\n" + Scalar(0x10000u), options).text ==
        Scalar(0x10000u) + L"\r\n\uE000",
        "text sort descending reverses scalar-aware ordinal comparison");
    options.mode = SortMode::Natural;
    suite.Expect(TransformTextSort(L"file002\nfile2\nfile02\nfile10", options).text ==
        L"file2\r\nfile02\r\nfile002\r\nfile10",
        "text sort natural mode orders fewer ASCII leading zeros first");
    suite.Expect(TransformTextSort(
        L"file99999999999999999999\nfile100000000000000000000", options).text ==
        L"file99999999999999999999\r\nfile100000000000000000000",
        "text sort natural mode compares arbitrary-length digit runs without conversion");
    options.caseInsensitive = true;
    suite.Expect(TransformTextSort(L"\u03C2-2\n\u03C3-10", options).text == L"\u03C2-2\r\n\u03C3-10",
        "text sort natural mode uses exact char-level invariant casing");
    suite.Expect(TransformTextSort(L"S10\n\u017F2", options).text == L"\u017F2\r\nS10" &&
        TransformTextSort(deseretLower + L"1\n" + deseretUpper + L"2", options).text ==
        deseretUpper + L"2\r\n" + deseretLower + L"1",
        "text sort natural mode folds long s but compares supplementary casing by UTF-16 code unit");

    options = {};
    options.mode = SortMode::None;
    options.removeDuplicates = true;
    options.trimBeforeCompare = true;
    auto trimKey = TransformTextSort(L" x \r\nx\r\n x", options);
    suite.Expect(trimKey.text == L" x " && trimKey.linesIn == 3 && trimKey.linesOut == 1 &&
        trimKey.duplicatesRemoved == 2,
        "text sort trim-before-compare preserves the first retained line text");
    options.trimBeforeCompare = false;
    options.caseInsensitive = true;
    auto ordinalDedupe = TransformTextSort(L"\u017F\ns\nS\n\u03C2\n\u03C3", options);
    suite.Expect(ordinalDedupe.text == L"\u017F\r\ns\r\n\u03C2" && ordinalDedupe.duplicatesRemoved == 2,
        "text sort dedupe matches ordinal ignore-case Unicode exceptions");
    auto deseretDedupe = TransformTextSort(deseretLower + L"\n" + deseretUpper, options);
    suite.Expect(deseretDedupe.text == deseretLower && deseretDedupe.duplicatesRemoved == 1,
        "text sort dedupe folds supplementary Deseret case pairs");

    options = {};
    options.caseInsensitive = true;
    options.removeDuplicates = true;
    options.trimEach = true;
    options.removeBlank = true;
    options.reverse = true;
    auto orderedPipeline = TransformTextSort(L" B \n\nb\n A ", options);
    suite.Expect(orderedPipeline.text == L"B\r\nA" && orderedPipeline.linesIn == 4 &&
        orderedPipeline.linesOut == 2 && orderedPipeline.duplicatesRemoved == 1,
        "text sort applies trim blank dedupe sort then reverse in managed order");
    options = {};
    options.mode = SortMode::None;
    options.removeBlank = true;
    auto blankCounts = TransformTextSort(L"a\n \nb", options);
    suite.Expect(blankCounts.text == L"a\r\nb" && blankCounts.linesIn == 3 &&
        blankCounts.linesOut == 2 && blankCounts.duplicatesRemoved == 0,
        "text sort blank removal does not inflate duplicate counts");
    options = {};
    options.reverse = true;
    options.shuffle = true;
    auto shuffledPipeline = TransformTextSort(L"c\na\nb", options, Scripted({ 0, 0 }));
    suite.Expect(shuffledPipeline.text == L"b\r\na\r\nc",
        "text sort runs ascending sort then reverse then scripted shuffle");
    options = {};
    options.shuffle = true;
    std::size_t failingShuffleCall{};
    auto partialShuffle = TransformTextSort(L"c\na\nb", options,
        [&failingShuffleCall](std::size_t) -> std::size_t
        {
            if (failingShuffleCall++ == 0) return 0;
            throw std::runtime_error("injected RNG failure");
        });
    suite.Expect(partialShuffle.text == L"c\r\nb\r\na" && partialShuffle.linesIn == 3 &&
        partialShuffle.linesOut == 3 && partialShuffle.duplicatesRemoved == 0,
        "text sort preserves transformed lines and completed swaps when shuffle RNG fails");

    suite.Expect(HardWrap(L"one two three", 7, false) == L"one two\nthree",
        "text wrap greedily hard-wraps at the UTF-16 width");
    suite.Expect(HardWrap(L"ab cd", 0, false) == L"ab\ncd",
        "text wrap clamps width below one while retaining oversized words");
    suite.Expect(HardWrap(L"abcdefgh ij", 3, true) == L"abc\ndef\ngh\nij",
        "text wrap chops long words and carries the remainder into normal wrapping");
    suite.Expect(HardWrap(L"one\ttwo   three", 7, false) == L"one two\nthree",
        "text wrap collapses ASCII space and tab word separators");
    suite.Expect(HardWrap(L"a\u00A0b c", 3, false) == L"a\u00A0b\nc",
        "text wrap retains internal non-ASCII whitespace as part of a word");
    auto longWord = std::wstring(2001, L'x');
    suite.Expect(HardWrap(longWord, INT_MAX, true) == std::wstring(2000, L'x') + L"\n" + L"x",
        "text wrap clamps extreme width values to 2000");
    suite.Expect(HardWrap(L"", 72, false) == L"\n\n" &&
        Unwrap(L"") == L"\n\n" && HangingIndent(L"", 4) == L"\n\n",
        "text wrap preserves the oracle empty-paragraph reconstruction quirk");
    suite.Expect(Reflow(L"", 72, false) == std::wstring(6, L'\n'),
        "text wrap reflow preserves the double-processed empty quirk");
    suite.Expect(Unwrap(L"a\n\n\nb") == L"a\n\n\n\nb" &&
        Reflow(L"a\n\n\nb", 72, false) == L"a\n\n\n\n\n\nb",
        "text wrap preserves and reprocesses consecutive blank paragraph quirks");
    suite.Expect(Unwrap(L" one  two \n three\tfour \n\n five ") ==
        L"one  two three\tfour\n\nfive",
        "text wrap unwrap trims edges but preserves intra-line spacing");
    suite.Expect(HardWrap(L" one  two \n three\tfour ", 40, false) == L"one two three four",
        "text wrap hard-wrap normalizes all paragraph word spacing");
    suite.Expect(AddPrefixEveryLine(L"a\n", L"> ") == L"> a\n> " &&
        AddPrefixEveryLine(L"", L"> ") == L"> ",
        "text wrap prefix includes blank trailing and empty logical lines");
    suite.Expect(AddPrefixEveryLine(L"a\r\nb", L"") == L"a\nb" &&
        AddPrefixEveryLine(L"a", L"x\n") == L"x\na",
        "text wrap prefix normalizes input while preserving a literal prefix");
    suite.Expect(HangingIndent(L"a\nb\n\nc\nd", 2) == L"a\n  b\n\nc\n  d",
        "text wrap hanging indent leaves each paragraph first line flush");
    suite.Expect(HangingIndent(L"a\nb", -20) == L"a\nb" &&
        HangingIndent(L"a\nb", INT_MAX) == L"a\n" + std::wstring(2000, L' ') + L"b",
        "text wrap clamps hanging indentation to zero through 2000");
    auto const surrogateWord = emoji + L"A";
    auto const surrogateWrapped = std::wstring(1, emoji[0]) + L"\n" +
        std::wstring(1, emoji[1]) + L"\nA";
    suite.Expect(HardWrap(surrogateWord, 1, true) == surrogateWrapped,
        "text wrap deliberately splits surrogate pairs at UTF-16 width boundaries");
    suite.Expect(HardWrap(L"\u200B", 1, false) == L"\u200B" && Unwrap(L" \t ") == L"\n\n",
        "text wrap distinguishes zero-width space from managed whitespace paragraphs");

    auto const metrics = MeasureText(L"a\r\nbc\rd\n");
    suite.Expect(metrics.lines == 4 && metrics.longestLine == 2 && metrics.characters == 8,
        "text wrap metrics normalize line breaks but count raw UTF-16 characters");
    auto const emptyMetrics = MeasureText(L"");
    suite.Expect(emptyMetrics.lines == 1 && emptyMetrics.longestLine == 0 && emptyMetrics.characters == 0,
        "text wrap empty readout reports one logical line");
    auto const emojiMetrics = MeasureText(emoji);
    suite.Expect(emojiMetrics.lines == 1 && emojiMetrics.longestLine == 2 && emojiMetrics.characters == 2,
        "text wrap metrics count supplementary text as two UTF-16 columns");

    std::cout << "\nline processing tests: " << suite.counts.passed << " passed, "
        << suite.counts.failed << " failed\n";
    return suite.counts;
}
