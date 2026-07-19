#include "TextAnalysisTests.h"

#include "TextAnalysis.h"

#include <Windows.h>

#include <cmath>
#include <clocale>
#include <cstdint>
#include <iostream>
#include <limits>
#include <string>
#include <string_view>
#include <vector>

namespace
{
    using namespace winforge::core::textanalysis;

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

    [[nodiscard]] bool Near(double left, double right, double tolerance = 1e-12) noexcept
    {
        return std::abs(left - right) <= tolerance;
    }

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
            else result.push_back(static_cast<wchar_t>(codePoint));
        }
        else result.push_back(static_cast<wchar_t>(codePoint));
        return result;
    }

    [[nodiscard]] FrequencyRow const* FindRow(
        WordFrequencyResult const& result,
        std::wstring_view term) noexcept
    {
        for (auto const& row : result.rows)
        {
            if (row.term == term) return &row;
        }
        return nullptr;
    }

    [[nodiscard]] std::wstring JoinedTerms(WordFrequencyResult const& result)
    {
        std::wstring joined;
        for (std::size_t index{}; index < result.rows.size(); ++index)
        {
            if (index != 0) joined.push_back(L'|');
            joined.append(result.rows[index].term);
        }
        return joined;
    }

    [[nodiscard]] bool IsEnglishUserLocale() noexcept
    {
        wchar_t locale[LOCALE_NAME_MAX_LENGTH]{};
        if (GetUserDefaultLocaleName(locale, LOCALE_NAME_MAX_LENGTH) == 0) return false;
        return (locale[0] == L'e' || locale[0] == L'E') &&
            (locale[1] == L'n' || locale[1] == L'N') &&
            (locale[2] == L'-' || locale[2] == L'\0');
    }
}

NativeTestCounts RunTextAnalysisTests()
{
    using namespace winforge::core::textanalysis;
    Suite suite;

    auto const emptyStats = AnalyzeTextStats(L"", false);
    suite.Expect(emptyStats.characters == 0 && emptyStats.words == 0 &&
        emptyStats.sentences == 0 && emptyStats.paragraphs == 0 && emptyStats.topWords.empty(),
        "text stats returns a fully zeroed empty snapshot");

    auto const emoji = Scalar(0x1F600u);
    auto const mixedStats = AnalyzeTextStats(L"A" + emoji + L"\u4E2D\u3042\u30AB\uFA11", false);
    suite.Expect(mixedStats.characters == 7 && mixedStats.charactersNoSpaces == 7 &&
        mixedStats.words == 5 && mixedStats.uniqueWords == 5 && mixedStats.syllables == 5,
        "text stats preserves UTF-16 counts while treating managed CJK ranges individually");
    suite.Expect(mixedStats.sentences == 1 && mixedStats.paragraphs == 1 &&
        Near(mixedStats.avgWordLength, 1.0) && Near(mixedStats.avgSentenceLength, 5.0),
        "text stats computes the mixed scalar fixture averages exactly");
    suite.Expect(Near(mixedStats.fleschReadingEase, 117.2) &&
        Near(mixedStats.fleschKincaidGrade, 0.0),
        "text stats rounds and clamps Flesch scores like Math.Round");
    suite.Expect(mixedStats.topWords.size() == 5 && mixedStats.topWords[0].word == L"A" &&
        mixedStats.topWords[1].word == L"\u3042" && mixedStats.topWords[2].word == L"\u30AB" &&
        mixedStats.topWords[3].word == L"\u4E2D" && mixedStats.topWords[4].word == L"\uFA11",
        "text stats top-word ties use ordinal-ignore-case order");

    auto const whitespaceStats = AnalyzeTextStats(L"a\u00A0\u3000\u180E", false);
    suite.Expect(whitespaceStats.characters == 4 && whitespaceStats.charactersNoSpaces == 2,
        "text stats uses the .NET 11 whitespace set and retains obsolete U+180E");
    auto const apostropheStats = AnalyzeTextStats(L"don't can\u2019t", false);
    suite.Expect(apostropheStats.words == 2 && apostropheStats.topWords[0].word == L"can\u2019t" &&
        apostropheStats.topWords[1].word == L"don't",
        "text stats keeps straight and curly apostrophes inside tokens");
    suite.Expect(AnalyzeTextStats(L"well-known", false).words == 2,
        "text stats splits hyphens under its managed tokenizer");
    suite.Expect(AnalyzeTextStats(Scalar(0x10400u), false).words == 0,
        "text stats char tokenizer does not combine supplementary letters");
    suite.Expect(AnalyzeTextStats(L"\uA7DC", false).words == 1,
        "text stats recognizes the shipping .NET 11 BMP Unicode categories");
    suite.Expect(AnalyzeTextStats(L"\u4E00\u4E8C\u3042\u30AB", false).words == 4,
        "text stats counts each configured Han kana glyph as one word");

    suite.Expect(AnalyzeTextStats(L"One.!? Two\u3002\uFF01\uFF1F", false).sentences == 2,
        "text stats coalesces ASCII and full-width terminal runs");
    suite.Expect(AnalyzeTextStats(L"One. \t!? \u3000? Two.", false).sentences == 2,
        "text stats whitespace does not reset a terminal punctuation run");
    suite.Expect(AnalyzeTextStats(L"One.,? Two.", false).sentences == 3,
        "text stats non-whitespace punctuation resets a terminal run");
    suite.Expect(AnalyzeTextStats(L"words without punctuation", false).sentences == 1,
        "text stats guarantees one sentence when words exist");
    suite.Expect(AnalyzeTextStats(L"---", false).sentences == 0,
        "text stats leaves sentence count zero when no words exist");

    suite.Expect(AnalyzeTextStats(L"a\r\n\r\nb\r\r c", false).paragraphs == 3,
        "text stats normalizes CRLF and CR before blank-line paragraph splitting");
    suite.Expect(AnalyzeTextStats(L"a\nb", false).paragraphs == 1,
        "text stats keeps single-newline text in one paragraph");
    suite.Expect(AnalyzeTextStats(L"a\n\n\nb", false).paragraphs == 2,
        "text stats matches non-overlapping double-newline split semantics");
    suite.Expect(AnalyzeTextStats(L" \u00A0\n\n\u3000", false).paragraphs == 0,
        "text stats excludes managed-whitespace-only paragraph blocks");

    auto const ordinalStats = AnalyzeTextStats(L"s S \u017F \u03C2 \u03C3 \u00B5 \u03BC", false);
    suite.Expect(ordinalStats.uniqueWords == 4,
        "text stats unique words reproduce ordinal-ignore-case long-s sigma and micro folding");
    auto const firstSpelling = AnalyzeTextStats(L"Alpha alpha ALPHA", false);
    suite.Expect(firstSpelling.topWords.size() == 1 && firstSpelling.topWords[0].word == L"Alpha" &&
        firstSpelling.topWords[0].count == 3,
        "text stats frequency keeps the first ordinal-ignore-case spelling");
    auto const stopIncluded = AnalyzeTextStats(L"the cat the", false);
    auto const stopRemoved = AnalyzeTextStats(L"the cat the", true);
    suite.Expect(stopRemoved.words == stopIncluded.words && stopRemoved.uniqueWords == stopIncluded.uniqueWords &&
        stopRemoved.syllables == stopIncluded.syllables && stopRemoved.topWords.size() == 1 &&
        stopRemoved.topWords[0].word == L"cat",
        "text stats stop-word option affects only the frequency list");
    auto const longSStop = AnalyzeTextStats(L"THE the \u017Fhe she", true);
    suite.Expect(longSStop.topWords.size() == 1 && longSStop.topWords[0].word == L"\u017Fhe",
        "text stats folded stop set preserves ordinal-ignore-case long-s distinction");
    auto const rankedStats = AnalyzeTextStats(L"beta alpha beta gamma alpha alpha", false, 2);
    suite.Expect(rankedStats.topWords.size() == 2 && rankedStats.topWords[0].word == L"alpha" &&
        rankedStats.topWords[0].count == 3 && rankedStats.topWords[1].word == L"beta" &&
        rankedStats.topWords[1].count == 2,
        "text stats ranks counts descending before ordinal ties");
    suite.Expect(AnalyzeTextStats(L"b a", false, 0).topWords.size() == 1,
        "text stats clamps top-N to one");
    suite.Expect(AnalyzeTextStats(L"b a", false, 99).topWords.size() == 2,
        "text stats caps top-N at the available terms");

    auto const syllableStats = AnalyzeTextStats(L"make queue rhythm", false);
    suite.Expect(syllableStats.syllables == 3,
        "text stats syllable heuristic groups vowels and removes silent trailing e");
    suite.Expect(AnalyzeTextStats(L"123 \u00C9 \u4E2D", false).syllables == 3,
        "text stats assigns one syllable to digit non-ASCII and CJK tokens");
    auto const timingStats = AnalyzeTextStats(L"one two three four", false);
    suite.Expect(Near(timingStats.readingMinutes, 0.02) &&
        Near(timingStats.speakingMinutes, 4.0 / 130.0),
        "text stats reading and speaking estimates use exact oracle rates");
    auto const fleschMidpoint = AnalyzeTextStats(L"cat. make. banana. hello.", false);
    suite.Expect(fleschMidpoint.words == 4 && fleschMidpoint.sentences == 4 &&
        fleschMidpoint.syllables == 7 && Near(fleschMidpoint.fleschReadingEase, 57.8) &&
        Near(fleschMidpoint.fleschKincaidGrade, 5.5),
        "text stats reproduces managed Flesch arithmetic and midpoint rounding");

    suite.Expect(FormatDuration(0.0) == L"0s" && FormatDuration(-1.0) == L"0s",
        "duration formatter handles zero and negative values");
    suite.Expect(FormatDuration(0.5 / 60.0) == L"1s" &&
        FormatDuration(1.5 / 60.0) == L"2s" && FormatDuration(2.5 / 60.0) == L"2s",
        "duration formatter uses midpoint-to-even seconds with a one-second floor");
    suite.Expect(FormatDuration(59.5 / 60.0) == L"1m 00s" &&
        FormatDuration(60.5 / 60.0) == L"1m 00s" && FormatDuration(61.0 / 60.0) == L"1m 01s",
        "duration formatter carries rounded seconds and pads minute output");
    suite.Expect(FormatDuration((std::numeric_limits<double>::quiet_NaN)()) == L"1s" &&
        FormatDuration((std::numeric_limits<double>::infinity)()) == L"35791394m 07s" &&
        FormatDuration(1e300) == L"35791394m 07s",
        "duration formatter mirrors .NET 11 saturating double-to-int conversion edges");

    WordFrequencyOptions wordOptions;
    wordOptions.caseInsensitive = true;
    wordOptions.minLength = 1;
    wordOptions.stripPunctuation = true;
    auto const whitespaceFrequency = AnalyzeWordFrequency(L" \u00A0\u3000\r\n", wordOptions);
    suite.Expect(whitespaceFrequency.rows.empty() && whitespaceFrequency.totalTokens == 0,
        "word frequency returns an empty result for managed whitespace");
    auto const basicFrequency = AnalyzeWordFrequency(L"Alpha alpha beta", wordOptions);
    auto const alpha = FindRow(basicFrequency, L"alpha");
    auto const beta = FindRow(basicFrequency, L"beta");
    suite.Expect(basicFrequency.totalTokens == 3 && basicFrequency.uniqueTokens == 2 &&
        alpha && alpha->count == 2 && beta && beta->count == 1,
        "word frequency folds and counts word tokens");
    suite.Expect(Near(basicFrequency.diversity, 2.0 / 3.0) && alpha && Near(alpha->barWidth, 220.0) &&
        alpha->percent == L"66.7%" && beta && Near(beta->barWidth, 110.0) && beta->percent == L"33.3%",
        "word frequency computes diversity bars and invariant one-decimal percentages");
    auto const savedNumericLocale = std::string(std::setlocale(LC_NUMERIC, nullptr));
    (void)std::setlocale(LC_NUMERIC, "de-DE");
    auto const localeIndependentPercent = AnalyzeWordFrequency(L"a b", wordOptions);
    (void)std::setlocale(LC_NUMERIC, savedNumericLocale.c_str());
    suite.Expect(localeIndependentPercent.rows.size() == 2 &&
        localeIndependentPercent.rows[0].percent == L"50.0%" &&
        localeIndependentPercent.rows[1].percent == L"50.0%",
        "word frequency percentages stay invariant under the C numeric locale");
    std::wstring percentMidpointInput;
    for (int index{}; index < 79; ++index) percentMidpointInput.append(L"a ");
    percentMidpointInput.push_back(L'b');
    auto const percentMidpoint = AnalyzeWordFrequency(percentMidpointInput, wordOptions);
    auto const midpointMinority = FindRow(percentMidpoint, L"b");
    suite.Expect(midpointMinority && midpointMinority->percent == L"1.3%",
        "word frequency custom one-decimal format rounds exact midpoints away from zero");

    auto punctuationFrequency = AnalyzeWordFrequency(L"'well-known' don't ---", wordOptions);
    suite.Expect(FindRow(punctuationFrequency, L"well-known") &&
        FindRow(punctuationFrequency, L"don't") && punctuationFrequency.totalTokens == 2,
        "word frequency stripping keeps in-word straight apostrophes and hyphens then trims edges");
    auto curlyFrequency = AnalyzeWordFrequency(L"can\u2019t", wordOptions);
    suite.Expect(curlyFrequency.totalTokens == 2 && FindRow(curlyFrequency, L"can") &&
        FindRow(curlyFrequency, L"t"),
        "word frequency stripping splits curly apostrophes exactly like the char tokenizer");
    wordOptions.stripPunctuation = false;
    auto keptPunctuation = AnalyzeWordFrequency(L"hello,\u00A0world!", wordOptions);
    suite.Expect(FindRow(keptPunctuation, L"hello,") && FindRow(keptPunctuation, L"world!"),
        "word frequency no-strip mode splits only on managed whitespace");

    wordOptions.stripPunctuation = true;
    wordOptions.minLength = 0;
    suite.Expect(AnalyzeWordFrequency(L"a bb", wordOptions).totalTokens == 2,
        "word frequency clamps minimum length to one");
    wordOptions.stripPunctuation = false;
    wordOptions.minLength = 2;
    suite.Expect(AnalyzeWordFrequency(emoji + L" a", wordOptions).totalTokens == 1,
        "word frequency minimum length measures UTF-16 code units");
    wordOptions.minLength = 1;
    auto kelvinFrequency = AnalyzeWordFrequency(L"\u212A K k \u03A3 \u03C3", wordOptions);
    suite.Expect(FindRow(kelvinFrequency, L"k") && FindRow(kelvinFrequency, L"k")->count == 3 &&
        FindRow(kelvinFrequency, L"\u03C3") && FindRow(kelvinFrequency, L"\u03C3")->count == 2,
        "word frequency uses exact invariant lowercase mappings");
    auto const deseretUpper = Scalar(0x10400u);
    auto const deseretLower = Scalar(0x10428u);
    auto deseretFrequency = AnalyzeWordFrequency(deseretUpper + L" " + deseretLower, wordOptions);
    suite.Expect(deseretFrequency.rows.size() == 1 && deseretFrequency.rows[0].term == deseretLower &&
        deseretFrequency.rows[0].count == 2,
        "word frequency lowercase combines supplementary Deseret case pairs");

    wordOptions.caseInsensitive = false;
    auto caseSensitive = AnalyzeWordFrequency(L"A a", wordOptions);
    suite.Expect(caseSensitive.uniqueTokens == 2 && caseSensitive.rows[0].term == L"A" &&
        caseSensitive.rows[1].term == L"a",
        "word frequency case-sensitive counts stay distinct and culture-equal ties stay stable");
    wordOptions.removeStopWords = true;
    auto caseSensitiveStop = AnalyzeWordFrequency(L"THE she \u017Fhe cat", wordOptions);
    suite.Expect(caseSensitiveStop.totalTokens == 2 && FindRow(caseSensitiveStop, L"cat") &&
        FindRow(caseSensitiveStop, L"\u017Fhe"),
        "word frequency folded stop set is ordinal-ignore-case without folding long s");

    wordOptions.caseInsensitive = true;
    wordOptions.mode = FrequencyMode::Bigrams;
    wordOptions.removeStopWords = false;
    auto bigrams = AnalyzeWordFrequency(L"one two three", wordOptions);
    suite.Expect(bigrams.totalTokens == 2 && FindRow(bigrams, L"one two") &&
        FindRow(bigrams, L"two three"),
        "word frequency builds adjacent bigrams after word processing");
    wordOptions.removeStopWords = true;
    auto bridged = AnalyzeWordFrequency(L"alpha the beta", wordOptions);
    suite.Expect(bridged.totalTokens == 1 && bridged.rows[0].term == L"alpha beta",
        "word frequency bigrams bridge across removed stop words");
    wordOptions.removeStopWords = false;
    wordOptions.minLength = 5;
    auto minBridged = AnalyzeWordFrequency(L"alpha x gamma", wordOptions);
    suite.Expect(minBridged.totalTokens == 1 && minBridged.rows[0].term == L"alpha gamma",
        "word frequency bigrams bridge across minimum-length filtering");
    suite.Expect(AnalyzeWordFrequency(L"alone", wordOptions).rows.empty(),
        "word frequency emits no bigram for fewer than two retained words");

    WordFrequencyOptions characterOptions;
    characterOptions.mode = FrequencyMode::Characters;
    characterOptions.caseInsensitive = true;
    characterOptions.minLength = 20;
    characterOptions.stripPunctuation = true;
    characterOptions.removeStopWords = true;
    auto characterFrequency = AnalyzeWordFrequency(emoji + L" " + emoji + L"A", characterOptions);
    suite.Expect(characterFrequency.totalTokens == 3 && FindRow(characterFrequency, emoji) &&
        FindRow(characterFrequency, emoji)->count == 2 && FindRow(characterFrequency, L"a"),
        "word frequency character mode counts Unicode scalars and ignores word-only options");
    std::wstring invalidSurrogate(1, static_cast<wchar_t>(0xD800u));
    auto invalidFrequency = AnalyzeWordFrequency(invalidSurrogate, characterOptions);
    suite.Expect(invalidFrequency.totalTokens == 1 && invalidFrequency.rows[0].term == L"\uFFFD",
        "word frequency character mode replaces each invalid UTF-16 surrogate");
    auto characterWhitespace = AnalyzeWordFrequency(L"a\u00A0\u3000\r\n b", characterOptions);
    suite.Expect(characterWhitespace.totalTokens == 2 && FindRow(characterWhitespace, L"a") &&
        FindRow(characterWhitespace, L"b"),
        "word frequency character mode skips Rune.IsWhiteSpace scalars");
    auto deseretCharacters = AnalyzeWordFrequency(deseretUpper + deseretLower, characterOptions);
    suite.Expect(deseretCharacters.totalTokens == 2 && deseretCharacters.uniqueTokens == 1 &&
        deseretCharacters.rows[0].term == deseretLower,
        "word frequency character mode lowercases supplementary scalars");
    suite.Expect(AnalyzeWordFrequency(L"\u180E", characterOptions).totalTokens == 1,
        "word frequency character mode retains non-whitespace U+180E");

    wordOptions = {};
    wordOptions.stripPunctuation = false;
    auto cultureFrequency = AnalyzeWordFrequency(L"\u00E4 \u00E5 \u00E6 ae \u00DF ss a " + emoji, wordOptions);
    auto const cultureTermsPresent = cultureFrequency.rows.size() == 8 &&
        FindRow(cultureFrequency, L"\u00E4") && FindRow(cultureFrequency, L"\u00E5") &&
        FindRow(cultureFrequency, L"\u00E6") && FindRow(cultureFrequency, L"ae") &&
        FindRow(cultureFrequency, L"\u00DF") && FindRow(cultureFrequency, L"ss") &&
        FindRow(cultureFrequency, L"a") && FindRow(cultureFrequency, emoji);
    auto const englishIcuOrder = JoinedTerms(cultureFrequency) ==
        emoji + L"|a|\u00E5|\u00E4|ae|\u00E6|ss|\u00DF";
    suite.Expect(cultureTermsPresent && (!IsEnglishUserLocale() || englishIcuOrder),
        "word frequency uses ICU current-culture order and discriminates ICU from NLS on English locales");
    auto countPriority = AnalyzeWordFrequency(L"z z a", wordOptions);
    suite.Expect(countPriority.rows[0].term == L"z" && countPriority.rows[0].count == 2,
        "word frequency count ordering precedes culture ordering");

    auto const csv = ToWordFrequencyCsv(basicFrequency);
    suite.Expect(csv == L"Rank,Term,Count,Percent\r\n1,\"alpha\",2,66.7%\n2,\"beta\",1,33.3%\n",
        "word frequency CSV preserves the managed mixed CRLF header and LF row endings");
    WordFrequencyResult quoted;
    quoted.rows.push_back({ 1, L"a,\"b", 2, 220.0, L"100.0%" });
    suite.Expect(ToWordFrequencyCsv(quoted) ==
        L"Rank,Term,Count,Percent\r\n1,\"a,\"\"b\",2,100.0%\n",
        "word frequency CSV quotes every term and doubles embedded quotes");
    suite.Expect(ToWordFrequencyCsv({}) == L"Rank,Term,Count,Percent\r\n",
        "word frequency CSV emits its header for an empty result");

    std::wstring manyUnique;
    for (int index{}; index < 2'000; ++index)
    {
        if (index != 0) manyUnique.push_back(L' ');
        manyUnique.append(L"term");
        manyUnique.append(std::to_wstring(index));
    }
    auto manyUniqueFrequency = AnalyzeWordFrequency(manyUnique, wordOptions);
    suite.Expect(manyUniqueFrequency.totalTokens == 2'000 && manyUniqueFrequency.uniqueTokens == 2'000,
        "word frequency preserves near-linear dictionary behavior for many unique terms");

    std::wstring manyStops;
    manyStops.reserve(80'005);
    for (int index{}; index < 20'000; ++index) manyStops.append(L"THE ");
    manyStops.append(L"kept");
    auto const manyStopStats = AnalyzeTextStats(manyStops, true);
    WordFrequencyOptions manyStopOptions;
    manyStopOptions.stripPunctuation = true;
    manyStopOptions.removeStopWords = true;
    auto const manyStopFrequency = AnalyzeWordFrequency(manyStops, manyStopOptions);
    suite.Expect(manyStopStats.words == 20'001 && manyStopStats.uniqueWords == 2 &&
        manyStopStats.topWords.size() == 1 && manyStopStats.topWords[0].word == L"kept" &&
        manyStopFrequency.totalTokens == 1 && manyStopFrequency.rows.size() == 1 &&
        manyStopFrequency.rows[0].term == L"kept",
        "text analysis stop-word sets handle large repeated filtered input near-linearly");

    auto const emptyCompare = ComputeStringComparison(L"", L"", false, false);
    suite.Expect(emptyCompare.lenA == 0 && emptyCompare.lenB == 0 && !emptyCompare.truncated &&
        emptyCompare.levenshtein == 0 && Near(emptyCompare.similarityPct, 100.0) &&
        emptyCompare.damerau == 0 && emptyCompare.hamming == 0 &&
        Near(emptyCompare.jaroWinkler, 1.0) && emptyCompare.longestCommonSubstring == 0 &&
        emptyCompare.longestCommonSubsequence == 0,
        "string compare defines every empty-input metric");
    auto const kitten = ComputeStringComparison(L"kitten", L"sitting", false, false);
    suite.Expect(kitten.levenshtein == 3 && kitten.damerau == 3 && kitten.hamming == -1,
        "string compare computes Levenshtein Damerau and unequal-length Hamming");
    suite.Expect(Near(kitten.similarityPct, 57.14285714285714) &&
        Near(kitten.jaroWinkler, 0.746031746031746) &&
        kitten.longestCommonSubstring == 3 && kitten.longestCommonSubsequence == 4,
        "string compare computes similarity Jaro-Winkler and common-sequence metrics");
    auto const transposition = ComputeStringComparison(L"ca", L"ac", false, false);
    suite.Expect(transposition.levenshtein == 2 && transposition.damerau == 1 &&
        transposition.hamming == 2,
        "string compare OSA Damerau recognizes one adjacent transposition");
    auto const osaFixture = ComputeStringComparison(L"CA", L"ABC", false, false);
    suite.Expect(osaFixture.levenshtein == 3 && osaFixture.damerau == 3 &&
        osaFixture.longestCommonSubstring == 1 && osaFixture.longestCommonSubsequence == 1,
        "string compare reproduces the CA-to-ABC optimal-string-alignment fixture");
    auto const lowPrefix = ComputeStringComparison(L"abxxxx", L"abyyyy", false, false);
    suite.Expect(Near(lowPrefix.jaroWinkler, 0.6444444444444444),
        "string compare applies the managed prefix boost even at low Jaro scores");
    auto const emojiCompare = ComputeStringComparison(emoji + L"A", emoji + L"B", false, false);
    suite.Expect(emojiCompare.lenA == 3 && emojiCompare.lenB == 3 &&
        emojiCompare.levenshtein == 1 && emojiCompare.hamming == 1 &&
        Near(emojiCompare.jaroWinkler, 0.8222222222222222) &&
        emojiCompare.longestCommonSubstring == 2 && emojiCompare.longestCommonSubsequence == 2,
        "string compare operates on UTF-16 code units for astral symbols");
    auto const ignoreCase = ComputeStringComparison(deseretUpper + L"\u212A", deseretLower + L"k", true, false);
    suite.Expect(ignoreCase.lenA == 3 && ignoreCase.lenB == 3 && ignoreCase.levenshtein == 0 &&
        ignoreCase.damerau == 0 && ignoreCase.hamming == 0 && Near(ignoreCase.jaroWinkler, 1.0),
        "string compare invariant lowercase is scalar-aware and length-preserving");
    auto const ignoreWhitespace = ComputeStringComparison(L"a \u00A0\u3000b", L"ab", false, true);
    suite.Expect(ignoreWhitespace.lenA == 2 && ignoreWhitespace.levenshtein == 0,
        "string compare removes the exact managed whitespace set before metrics");
    suite.Expect(ComputeStringComparison(L"a\u180Eb", L"ab", false, true).levenshtein == 1,
        "string compare does not remove obsolete non-whitespace U+180E");

    auto exactLimit = ComputeStringComparison(std::wstring(2'000, L'a'), std::wstring(2'000, L'a'), false, false);
    suite.Expect(!exactLimit.truncated && exactLimit.levenshtein == 0 && exactLimit.damerau == 0,
        "string compare computes quadratic metrics at the inclusive 2000-unit limit");
    auto normalizedLimit = ComputeStringComparison(
        std::wstring(2'000, L'a') + L" ", std::wstring(2'000, L'a'), false, true);
    suite.Expect(!normalizedLimit.truncated && normalizedLimit.lenA == 2'000 &&
        normalizedLimit.levenshtein == 0,
        "string compare applies normalization before its 2000-unit guard");
    auto truncatedEqual = ComputeStringComparison(
        std::wstring(2'001, L'a'), std::wstring(2'001, L'a'), false, false);
    suite.Expect(truncatedEqual.truncated && truncatedEqual.lenA == 2'001 &&
        truncatedEqual.levenshtein == -1 && std::isnan(truncatedEqual.similarityPct) &&
        truncatedEqual.damerau == -1 && truncatedEqual.longestCommonSubstring == -1 &&
        truncatedEqual.longestCommonSubsequence == -1,
        "string compare 2001-unit guard emits all quadratic sentinel values");
    suite.Expect(truncatedEqual.hamming == 0 && Near(truncatedEqual.jaroWinkler, 1.0),
        "string compare still computes Hamming and Jaro-Winkler above the guard");
    auto truncatedDifferent = ComputeStringComparison(
        std::wstring(2'001, L'a'), std::wstring(2'002, L'a'), false, false);
    suite.Expect(truncatedDifferent.truncated && truncatedDifferent.hamming == -1 &&
        truncatedDifferent.jaroWinkler > 0.99,
        "string compare guarded unequal lengths retain Hamming n-a and live Jaro-Winkler");
    auto const adversarialJaro = ComputeStringComparison(
        std::wstring(100'000, L'a'), std::wstring(100'000, L'b'), false, false);
    suite.Expect(adversarialJaro.truncated && adversarialJaro.levenshtein == -1 &&
        std::isnan(adversarialJaro.similarityPct) && adversarialJaro.damerau == -1 &&
        adversarialJaro.hamming == 100'000 && Near(adversarialJaro.jaroWinkler, 0.0) &&
        adversarialJaro.longestCommonSubstring == -1 &&
        adversarialJaro.longestCommonSubsequence == -1,
        "string compare guarded adversarial input keeps exact sentinels and subquadratic Jaro-Winkler");

    suite.Expect(BuildStringComparisonReport({ { L"Length", L"3 / 4" }, { L"Similarity", L"75.0%" } }) ==
        L"Length: 3 / 4\r\nSimilarity: 75.0%",
        "string compare report uses Windows AppendLine separators and trims the final line ending");
    suite.Expect(BuildStringComparisonReport({ { L"Final", L"value \u00A0" } }) == L"Final: value",
        "string compare report TrimEnd removes the whole final managed-whitespace run");
    suite.Expect(BuildStringComparisonReport({}).empty(),
        "string compare report handles an empty row sequence");

    return suite.counts;
}
