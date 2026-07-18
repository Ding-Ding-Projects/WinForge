#include "TextDiffTests.h"

#include "TextDiff.h"

#include <algorithm>
#include <cstdint>
#include <iostream>
#include <string>
#include <string_view>

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

    [[nodiscard]] bool IsLine(
        winforge::core::textdiff::DiffResult const& result,
        std::size_t index,
        winforge::core::textdiff::ChangeKind kind,
        wchar_t prefix,
        std::wstring_view text)
    {
        if (index >= result.lines.size()) return false;
        auto const& line = result.lines[index];
        return line.kind == kind && line.prefix == prefix &&
            line.text.size() == text.size() &&
            std::equal(line.text.begin(), line.text.end(), text.begin());
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

    [[nodiscard]] std::wstring RepeatLines(std::size_t count, wchar_t value)
    {
        if (count == 0) return {};
        std::wstring result;
        result.reserve(count * 2 - 1);
        for (std::size_t index{}; index < count; ++index)
        {
            if (index != 0) result.push_back(L'\n');
            result.push_back(value);
        }
        return result;
    }
}

NativeTestCounts RunTextDiffTests()
{
    using namespace winforge::core::textdiff;
    Suite suite;

    suite.Expect(CellBudget == 6'000'000, "text diff exposes the managed six-million-cell guard");

    auto const empty = Compute(L"", L"");
    suite.Expect(empty.lines.empty() && empty.added == 0 && empty.removed == 0 &&
        empty.unchanged == 0 && !empty.truncated,
        "text diff treats null-equivalent empty inputs as zero lines");
    suite.Expect(ToUnifiedDiff(empty) == L"--- A\n+++ B\n",
        "text diff emits the exact fixed header for an empty result");

    auto const added = Compute(L"", L"new");
    suite.Expect(added.lines.size() == 1 && added.added == 1 &&
        IsLine(added, 0, ChangeKind::Added, L'+', L"new"),
        "text diff emits a right-only line as an addition");

    auto const removed = Compute(L"old", L"");
    suite.Expect(removed.lines.size() == 1 && removed.removed == 1 &&
        IsLine(removed, 0, ChangeKind::Removed, L'-', L"old"),
        "text diff emits a left-only line as a removal");

    auto const normalizedBreaks = Compute(L"a\r\nb\rc\n", L"a\nb\nc\n");
    suite.Expect(normalizedBreaks.lines.size() == 4 && normalizedBreaks.unchanged == 4 &&
        IsLine(normalizedBreaks, 0, ChangeKind::Unchanged, L' ', L"a") &&
        IsLine(normalizedBreaks, 1, ChangeKind::Unchanged, L' ', L"b") &&
        IsLine(normalizedBreaks, 2, ChangeKind::Unchanged, L' ', L"c") &&
        IsLine(normalizedBreaks, 3, ChangeKind::Unchanged, L' ', L""),
        "text diff normalizes CRLF and lone CR while retaining a trailing empty line");

    auto const trailing = Compute(L"a\n", L"a");
    suite.Expect(trailing.lines.size() == 2 && trailing.unchanged == 1 && trailing.removed == 1 &&
        IsLine(trailing, 0, ChangeKind::Unchanged, L' ', L"a") &&
        IsLine(trailing, 1, ChangeKind::Removed, L'-', L""),
        "text diff preserves the oracle's trailing-split empty line");

    auto const loneBreak = Compute(L"", L"\n");
    suite.Expect(loneBreak.lines.size() == 2 && loneBreak.added == 2 &&
        IsLine(loneBreak, 0, ChangeKind::Added, L'+', L"") &&
        IsLine(loneBreak, 1, ChangeKind::Added, L'+', L""),
        "text diff distinguishes empty input from the two empty lines around a newline");

    auto const tie = Compute(L"A\nB", L"B\nA");
    suite.Expect(tie.lines.size() == 3 &&
        IsLine(tie, 0, ChangeKind::Removed, L'-', L"A") &&
        IsLine(tie, 1, ChangeKind::Unchanged, L' ', L"B") &&
        IsLine(tie, 2, ChangeKind::Added, L'+', L"A"),
        "text diff resolves equal LCS choices removal-first");

    auto const originalDisplay = Compute(L"left  value", L"leftvalue", true, false);
    suite.Expect(originalDisplay.lines.size() == 1 && originalDisplay.unchanged == 1 &&
        IsLine(originalDisplay, 0, ChangeKind::Unchanged, L' ', L"left  value"),
        "text diff displays the original left line when normalized keys match");

    std::wstring managedWhitespace = L"A";
    for (auto const codePoint : {
        0x0009u, 0x000Bu, 0x000Cu, 0x0020u, 0x0085u, 0x00A0u, 0x1680u,
        0x2000u, 0x2001u, 0x2002u, 0x2003u, 0x2004u, 0x2005u, 0x2006u,
        0x2007u, 0x2008u, 0x2009u, 0x200Au, 0x2028u, 0x2029u, 0x202Fu,
        0x205Fu, 0x3000u })
    {
        managedWhitespace += Scalar(codePoint);
    }
    managedWhitespace += L"B";
    auto const ignoredWhitespace = Compute(managedWhitespace, L"AB", true, false);
    suite.Expect(ignoredWhitespace.lines.size() == 1 && ignoredWhitespace.unchanged == 1,
        "text diff ignores the complete managed intra-line Unicode whitespace set");

    auto const obsoleteMongolianSeparator = Compute(L"A\x180E" L"B", L"AB", true, false);
    suite.Expect(obsoleteMongolianSeparator.removed == 1 && obsoleteMongolianSeparator.added == 1,
        "text diff does not treat U+180E as managed whitespace");

    auto const whitespaceSignificant = Compute(L"A B", L"AB", false, false);
    suite.Expect(whitespaceSignificant.removed == 1 && whitespaceSignificant.added == 1,
        "text diff keeps whitespace significant when the option is disabled");

    auto const asciiCase = Compute(L"WinForge", L"WINFORGE", false, true);
    suite.Expect(asciiCase.lines.size() == 1 && asciiCase.unchanged == 1 &&
        IsLine(asciiCase, 0, ChangeKind::Unchanged, L' ', L"WinForge"),
        "text diff applies invariant ASCII case folding and retains left display text");

    auto const caseSignificant = Compute(L"WinForge", L"WINFORGE", false, false);
    suite.Expect(caseSignificant.removed == 1 && caseSignificant.added == 1,
        "text diff keeps case significant when the option is disabled");

    std::wstring unicodeLower;
    unicodeLower += Scalar(0x017Fu); // long s, a singleton invariant mapping
    unicodeLower += Scalar(0x03C2u); // Greek final sigma
    unicodeLower += Scalar(0x00E9u); // range mapping
    unicodeLower += Scalar(0x00B5u); // micro sign to Greek capital mu
    std::wstring unicodeUpper;
    unicodeUpper += Scalar(0x0053u);
    unicodeUpper += Scalar(0x03A3u);
    unicodeUpper += Scalar(0x00C9u);
    unicodeUpper += Scalar(0x039Cu);
    auto const unicodeCase = Compute(unicodeLower, unicodeUpper, false, true);
    suite.Expect(unicodeCase.lines.size() == 1 && unicodeCase.unchanged == 1,
        "text diff matches managed invariant uppercase range and singleton mappings");

    auto const dotlessI = Compute(Scalar(0x0131u), L"I", false, true);
    suite.Expect(dotlessI.removed == 1 && dotlessI.added == 1,
        "text diff preserves managed invariant dotless-i behavior");

    auto const sharpS = Compute(Scalar(0x00DFu), L"SS", false, true);
    suite.Expect(sharpS.removed == 1 && sharpS.added == 1,
        "text diff preserves managed non-expanding sharp-s behavior");

    auto const deseretCase = Compute(Scalar(0x10428u), Scalar(0x10400u), false, true);
    suite.Expect(deseretCase.lines.size() == 1 && deseretCase.unchanged == 1,
        "text diff invariant casing handles supplementary UTF-16 scalar mappings");

    std::wstring unpairedHigh(1, static_cast<wchar_t>(0xD801u));
    auto const unpaired = Compute(unpairedHigh, unpairedHigh, false, true);
    suite.Expect(unpaired.lines.size() == 1 && unpaired.unchanged == 1 &&
        IsLine(unpaired, 0, ChangeKind::Unchanged, L' ', unpairedHigh),
        "text diff preserves an unpaired UTF-16 surrogate during invariant casing");

    auto const bothOptions = Compute(L" mixed\tCase ", L"MIXEDCASE", true, true);
    suite.Expect(bothOptions.lines.size() == 1 && bothOptions.unchanged == 1,
        "text diff composes whitespace removal before invariant uppercase");

    auto const mixed = Compute(L"keep\nold\nlast", L"keep\nnew\nlast\nextra");
    suite.Expect(mixed.lines.size() == 5 && mixed.added == 2 && mixed.removed == 1 &&
        mixed.unchanged == 2 && !mixed.truncated &&
        IsLine(mixed, 0, ChangeKind::Unchanged, L' ', L"keep") &&
        IsLine(mixed, 1, ChangeKind::Removed, L'-', L"old") &&
        IsLine(mixed, 2, ChangeKind::Added, L'+', L"new") &&
        IsLine(mixed, 3, ChangeKind::Unchanged, L' ', L"last") &&
        IsLine(mixed, 4, ChangeKind::Added, L'+', L"extra"),
        "text diff reports exact mixed order, prefixes, and counters");
    suite.Expect(ToUnifiedDiff(mixed) ==
        L"--- A\n+++ B\n keep\n-old\n+new\n last\n+extra\n",
        "text diff emits the exact managed unified representation");

    DiffResult customPrefix;
    customPrefix.lines.push_back({ ChangeKind::Added, L'!', L"stored prefix" });
    suite.Expect(ToUnifiedDiff(customPrefix) == L"--- A\n+++ B\n!stored prefix\n",
        "text diff unified output uses the stored prefix exactly like the oracle");

    auto const atBudget = Compute(RepeatLines(2000, L'X'), RepeatLines(3000, L'X'));
    suite.Expect(!atBudget.truncated && atBudget.lines.size() == 3000 &&
        atBudget.unchanged == 2000 && atBudget.added == 1000 && atBudget.removed == 0 &&
        IsLine(atBudget, 1999, ChangeKind::Unchanged, L' ', L"X") &&
        IsLine(atBudget, 2000, ChangeKind::Added, L'+', L"X"),
        "text diff runs LCS at exactly six million key cells");

    auto const overBudget = Compute(RepeatLines(2000, L'L'), RepeatLines(3001, L'R'));
    suite.Expect(overBudget.truncated && overBudget.lines.size() == 5001 &&
        overBudget.removed == 2000 && overBudget.added == 3001 && overBudget.unchanged == 0,
        "text diff falls back immediately above six million key cells");
    suite.Expect(IsLine(overBudget, 0, ChangeKind::Removed, L'-', L"L") &&
        IsLine(overBudget, 1999, ChangeKind::Removed, L'-', L"L") &&
        IsLine(overBudget, 2000, ChangeKind::Added, L'+', L"R") &&
        IsLine(overBudget, 5000, ChangeKind::Added, L'+', L"R"),
        "text diff fallback emits every removal before every addition");

    std::cout << "\ntext diff tests: " << suite.counts.passed << " passed, "
        << suite.counts.failed << " failed\n";
    return suite.counts;
}
