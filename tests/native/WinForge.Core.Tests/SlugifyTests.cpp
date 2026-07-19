#include "SlugifyTests.h"

#include "Slugify.h"

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
}

NativeTestCounts RunSlugifyTests()
{
    using namespace winforge::core::slugify;
    Suite suite;

    suite.Expect(SeparatorCharacter(Separator::Hyphen) == L'-' &&
        SeparatorCharacter(Separator::Underscore) == L'_' &&
        SeparatorCharacter(Separator::Dot) == L'.' &&
        SeparatorCharacter(static_cast<Separator>(99)) == L'-',
        "slug separator selection preserves the managed three modes and hyphen fallback");

    suite.Expect(Slugify(L"  Hello, World!  ") == L"hello-world",
        "slug default conversion lowercases ASCII and trims punctuation boundaries");

    suite.Expect(Slugify(L"Crème brûlée & Ångström") == L"creme-brulee-angstrom",
        "slug default NFD stripping removes nonspacing accents before NFC recomposition");

    Options accents;
    accents.stripDiacritics = false;
    accents.keepUnicodeLetters = true;
    suite.Expect(Slugify(L"Café déjà vu", accents) == L"café-déjà-vu",
        "slug can retain accented Unicode letters when accent stripping is disabled");

    Options upperUnderscore;
    upperUnderscore.separator = Separator::Underscore;
    upperUnderscore.letterCase = LetterCase::Upper;
    suite.Expect(Slugify(L"My File.Name", upperUnderscore) == L"MY_FILE_NAME",
        "slug applies underscore separation and invariant uppercase after conversion");

    Options keepDot;
    keepDot.separator = Separator::Dot;
    keepDot.letterCase = LetterCase::Keep;
    auto unknownCase = keepDot;
    unknownCase.letterCase = static_cast<LetterCase>(99);
    suite.Expect(Slugify(L"My_FILE name", keepDot) == L"My.FILE.name" &&
        Slugify(L"My_FILE name", unknownCase) == L"My.FILE.name",
        "slug keep-case and unknown case fallback preserve source letter case");

    Options unicode;
    unicode.keepUnicodeLetters = true;
    suite.Expect(Slugify(L"中文 café \u0967\u0968\u0969", unicode) == L"中文-cafe-\u0967\u0968\u0969" &&
        Slugify(L"中文 café \u0967\u0968\u0969") == L"cafe",
        "slug Unicode opt-in keeps only managed letters and decimal digits beyond ASCII");

    suite.Expect(Slugify(L"A\u2160\u00B2\u0660", unicode) == L"a-\u0660",
        "slug Unicode opt-in excludes letter-number and other-number categories like char.IsLetterOrDigit");

    Options noCollapse;
    noCollapse.collapseRepeats = false;
    suite.Expect(Slugify(L"a---___b", noCollapse) == L"a-b",
        "slug boundary emission stays single even when managed collapse repeats is disabled");

    Options length;
    length.maxLength = 8;
    suite.Expect(Slugify(L"One Two Three", length) == L"one-two",
        "slug length clipping removes a separator exposed at the managed UTF-16 boundary");
    length.maxLength = 6;
    suite.Expect(Slugify(L"One Two Three", length) == L"one-tw" &&
        Slugify(L"One Two Three") == L"one-two-three",
        "slug zero length option remains unlimited and positive lengths use UTF-16 units");

    Options dottedI;
    dottedI.keepUnicodeLetters = true;
    dottedI.stripDiacritics = false;
    suite.Expect(Slugify(L"\u0130 \u0131 \u00DF", dottedI) == L"\u0130-\u0131-\u00DF",
        "slug invariant simple lowercase retains dotted I dotless i and sharp s like the managed oracle");
    dottedI.stripDiacritics = true;
    suite.Expect(Slugify(L"\u0130", dottedI) == L"i",
        "slug stripping decomposes dotted capital I before invariant lowercase");

    auto const malformedHigh = std::wstring(1, static_cast<wchar_t>(0xD800u));
    suite.Expect(Slugify(malformedHigh).empty() &&
        Slugify(std::wstring(L"A") + malformedHigh + L"B", Options{ .stripDiacritics = false }) == L"a-b",
        "slug matches managed malformed UTF-16 behavior with and without normalization");

    auto const defaultBlock = SlugifyBlock(L" \r\nCafé\rfoo\n\n中文");
    suite.Expect(defaultBlock == L"cafe\nfoo\n",
        "slug blocks normalize mixed line endings and retain an empty converted nonblank row");
    suite.Expect(SlugifyBlock(L" \r\nCafé\rfoo\n\n中文", unicode) == L"cafe\nfoo\n中文" &&
        SlugifyBlock(L"\u00A0\u3000\r\n\t").empty(),
        "slug blocks honor Unicode opt-in and skip managed-whitespace-only lines");

    auto const preview = PreviewFirstLine(L"\r\n\u00A0\r  Café & Tea \r\nSecond");
    suite.Expect(preview.hasInput && preview.input == L"Café & Tea" && preview.slug == L"cafe-tea",
        "slug preview chooses the first trimmed nonblank managed line and its local result");
    auto const emptyPreview = PreviewFirstLine(L"\u00A0\r\n\t");
    suite.Expect(!emptyPreview.hasInput && emptyPreview.input.empty() && emptyPreview.slug.empty(),
        "slug preview reports no source for an all-whitespace block");

    std::cout << "\nslugify tests: " << suite.counts.passed << " passed, "
              << suite.counts.failed << " failed\n";
    return suite.counts;
}
