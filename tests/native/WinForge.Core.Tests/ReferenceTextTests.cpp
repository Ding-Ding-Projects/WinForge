#include "ReferenceTextTests.h"

#include "ReferenceText.h"

#include <array>
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

    [[nodiscard]] std::wstring CodeUnit(std::uint16_t value)
    {
        return std::wstring(1, static_cast<wchar_t>(value));
    }

    struct EntitySample
    {
        std::wstring_view name;
        std::uint32_t codePoint;
    };

    constexpr EntitySample NamedSamples[]{
        { L"amp", 38 }, { L"divide", 247 }, { L"AElig", 198 }, { L"thorn", 254 },
        { L"OElig", 338 }, { L"Omega", 937 }, { L"omega", 969 }, { L"zwnj", 8204 },
        { L"Dagger", 8225 }, { L"trade", 8482 }, { L"hArr", 8660 }, { L"forall", 8704 },
        { L"rfloor", 8971 }, { L"hearts", 9829 }, { L"starf", 9733 }, { L"cross", 10007 },
    };

    struct ReferenceSample
    {
        std::wstring_view name;
        std::uint32_t codePoint;
        std::wstring_view description;
    };

    constexpr ReferenceSample ReferenceSamples[]{
        { L"&amp;", 38, L"Ampersand" }, { L"&lt;", 60, L"Less-than" },
        { L"&gt;", 62, L"Greater-than" }, { L"&quot;", 34, L"Double quote" },
        { L"&apos;", 39, L"Apostrophe" }, { L"&nbsp;", 160, L"Non-breaking space" },
        { L"&copy;", 169, L"Copyright" }, { L"&reg;", 174, L"Registered" },
        { L"&trade;", 8482, L"Trademark" }, { L"&cent;", 162, L"Cent" },
        { L"&pound;", 163, L"Pound sterling" }, { L"&yen;", 165, L"Yen" },
        { L"&euro;", 8364, L"Euro" }, { L"&sect;", 167, L"Section" },
        { L"&para;", 182, L"Pilcrow" }, { L"&deg;", 176, L"Degree" },
        { L"&plusmn;", 177, L"Plus-minus" }, { L"&times;", 215, L"Multiplication" },
        { L"&divide;", 247, L"Division" }, { L"&frac12;", 189, L"One half" },
        { L"&frac14;", 188, L"One quarter" }, { L"&micro;", 181, L"Micro" },
        { L"&middot;", 183, L"Middle dot" }, { L"&bull;", 8226, L"Bullet" },
        { L"&hellip;", 8230, L"Ellipsis" }, { L"&ndash;", 8211, L"En dash" },
        { L"&mdash;", 8212, L"Em dash" }, { L"&lsquo;", 8216, L"Left single quote" },
        { L"&rsquo;", 8217, L"Right single quote" }, { L"&ldquo;", 8220, L"Left double quote" },
        { L"&rdquo;", 8221, L"Right double quote" }, { L"&dagger;", 8224, L"Dagger" },
        { L"&laquo;", 171, L"Left guillemet" }, { L"&raquo;", 187, L"Right guillemet" },
        { L"&larr;", 8592, L"Left arrow" }, { L"&rarr;", 8594, L"Right arrow" },
        { L"&uarr;", 8593, L"Up arrow" }, { L"&darr;", 8595, L"Down arrow" },
        { L"&hearts;", 9829, L"Heart" }, { L"&spades;", 9824, L"Spade" },
        { L"&clubs;", 9827, L"Club" }, { L"&diams;", 9830, L"Diamond" },
        { L"&star;", 9733, L"Star" }, { L"&check;", 10003, L"Check mark" },
        { L"&infin;", 8734, L"Infinity" }, { L"&ne;", 8800, L"Not equal" },
        { L"&le;", 8804, L"Less or equal" }, { L"&ge;", 8805, L"Greater or equal" },
        { L"&sum;", 8721, L"Summation" }, { L"&radic;", 8730, L"Square root" },
    };
}

NativeTestCounts RunReferenceTextTests()
{
    using namespace winforge::core::referencetext;
    Suite suite;

    suite.Expect(
        PhoneticAlphabetDisplayName(PhoneticAlphabet::Nato) == L"NATO / ICAO" &&
        PhoneticAlphabetDisplayName(PhoneticAlphabet::Police) == L"LAPD / Police" &&
        PhoneticAlphabetDisplayName(PhoneticAlphabet::Simple) == L"Simple words" &&
        PhoneticAlphabetDisplayName(static_cast<PhoneticAlphabet>(99)) == L"NATO / ICAO",
        "phonetic exposes managed display names and defaults unknown alphabets to NATO");

    auto nato = SpellPhonetic(L"ABCDEFGHIJKLMNOPQRSTUVWXYZ", PhoneticAlphabet::Nato, false, false);
    suite.Expect(nato.spoken ==
        L"Alpha Bravo Charlie Delta Echo Foxtrot Golf Hotel India Juliett Kilo Lima Mike November "
        L"Oscar Papa Quebec Romeo Sierra Tango Uniform Victor Whiskey X-ray Yankee Zulu" &&
        nato.characters.size() == 26 && nato.characters.front().character == L"A" &&
        nato.characters.back().code == L"Zulu",
        "phonetic preserves the complete NATO ICAO table and row order");

    auto police = SpellPhonetic(L"ABCDEFGHIJKLMNOPQRSTUVWXYZ", PhoneticAlphabet::Police, false, false);
    suite.Expect(police.spoken ==
        L"Adam Boy Charlie David Edward Frank George Henry Ida John King Lincoln Mary Nora Ocean Paul "
        L"Queen Robert Sam Tom Union Victor William X-ray Young Zebra" && police.characters.size() == 26,
        "phonetic preserves the complete LAPD police table");

    auto simple = SpellPhonetic(L"ABCDEFGHIJKLMNOPQRSTUVWXYZ", PhoneticAlphabet::Simple, false, false);
    suite.Expect(simple.spoken ==
        L"Apple Banana Cat Dog Egg Fish Goat House Ice Juice Kite Lion Moon Nose Orange Pig Queen Rabbit "
        L"Sun Tree Umbrella Violin Water Xylophone Yellow Zebra" && simple.characters.size() == 26,
        "phonetic preserves the complete simple-word table");

    auto digits = SpellPhonetic(L"0123456789", PhoneticAlphabet::Nato, false, false);
    suite.Expect(digits.spoken == L"Zero One Two Three Four Five Six Seven Eight Niner" &&
        digits.characters.size() == 10 && digits.characters[9].code == L"Niner",
        "phonetic preserves shared digit words including Niner");

    auto punctuation = SpellPhonetic(L"a- b!\t", PhoneticAlphabet::Nato, true, true);
    suite.Expect(punctuation.spoken == L"Alpha - (space) Bravo ! \t" &&
        punctuation.characters.size() == 6 && punctuation.characters[0].character == L"A" &&
        punctuation.characters[3].character == L"B",
        "phonetic uppercases shown ASCII while echoing kept punctuation and ASCII space");

    auto dropped = SpellPhonetic(L"a-\t b!\n", PhoneticAlphabet::Nato, false, false);
    suite.Expect(dropped.spoken == L"Alpha (space) Bravo" && dropped.characters.size() == 3 &&
        dropped.characters[0].character == L"a" && dropped.characters[2].character == L"b",
        "phonetic drops non-space punctuation while preserving original row casing");

    auto unicode = SpellPhonetic(L"\u017F\u0131\u00B5", PhoneticAlphabet::Nato, true, true);
    suite.Expect(unicode.spoken == L"Sierra \u0131 \u00B5" && unicode.characters.size() == 3 &&
        unicode.characters[0].character == L"S" && unicode.characters[1].character == L"\u0131" &&
        unicode.characters[2].character == L"\u039C" && unicode.characters[2].code == L"\u00B5",
        "phonetic matches invariant char casing for long-s dotless-i and micro-sign");
    auto unicodeDropped = SpellPhonetic(L"\u017F\u0131\u00B5", PhoneticAlphabet::Nato, false, false);
    suite.Expect(unicodeDropped.spoken == L"Sierra" && unicodeDropped.characters.size() == 1,
        "phonetic only admits non-ASCII code units whose invariant uppercase key is ASCII");

    auto const emoji = Scalar(0x1F600u);
    auto splitEmoji = SpellPhonetic(emoji, PhoneticAlphabet::Nato, true, true);
    auto splitEmojiExact = sizeof(wchar_t) != 2 ||
        (splitEmoji.characters.size() == 2 && splitEmoji.characters[0].character == emoji.substr(0, 1) &&
            splitEmoji.characters[1].character == emoji.substr(1, 1) &&
            splitEmoji.spoken == emoji.substr(0, 1) + L" " + emoji.substr(1, 1));
    suite.Expect(splitEmojiExact,
        "phonetic deliberately emits supplementary text as two UTF-16 rows");
    auto isolatedHigh = CodeUnit(0xD800u);
    auto keptSurrogate = SpellPhonetic(isolatedHigh, PhoneticAlphabet::Nato, true, true);
    suite.Expect(keptSurrogate.spoken == isolatedHigh && keptSurrogate.characters.size() == 1 &&
        SpellPhonetic(isolatedHigh, PhoneticAlphabet::Nato, true, false).characters.empty(),
        "phonetic preserves kept unpaired surrogates and drops them otherwise");
    suite.Expect(SpellPhonetic(L"", PhoneticAlphabet::Nato, true, true).spoken.empty(),
        "phonetic returns an empty result for empty input");

    struct StyleExpected
    {
        BoxBorderStyle style;
        std::wstring_view value;
    };
    constexpr StyleExpected styleExpected[]{
        { BoxBorderStyle::Ascii, L"+---+\n| x |\n+---+" },
        { BoxBorderStyle::Single, L"\u250C\u2500\u2500\u2500\u2510\n\u2502 x \u2502\n\u2514\u2500\u2500\u2500\u2518" },
        { BoxBorderStyle::Double, L"\u2554\u2550\u2550\u2550\u2557\n\u2551 x \u2551\n\u255A\u2550\u2550\u2550\u255D" },
        { BoxBorderStyle::Rounded, L"\u256D\u2500\u2500\u2500\u256E\n\u2502 x \u2502\n\u2570\u2500\u2500\u2500\u256F" },
        { BoxBorderStyle::Heavy, L"\u250F\u2501\u2501\u2501\u2513\n\u2503 x \u2503\n\u2517\u2501\u2501\u2501\u251B" },
        { BoxBorderStyle::Stars, L"*****\n* x *\n*****" },
    };
    bool allGridStyles = true;
    for (auto const& expected : styleExpected)
    {
        allGridStyles = allGridStyles &&
            RenderBoxText(L"x", expected.style, 1, BoxAlignment::Left, L"") == expected.value;
    }
    suite.Expect(allGridStyles, "box text renders all six grid border styles exactly");

    suite.Expect(RenderBoxText(L"a\nbbb", BoxBorderStyle::Ascii, 1, BoxAlignment::Left, L"") ==
        L"+-----+\n| a   |\n| bbb |\n+-----+" &&
        RenderBoxText(L"a\nbbb", BoxBorderStyle::Ascii, 1, BoxAlignment::Center, L"") ==
        L"+-----+\n|  a  |\n| bbb |\n+-----+" &&
        RenderBoxText(L"a\nbbb", BoxBorderStyle::Ascii, 1, BoxAlignment::Right, L"") ==
        L"+-----+\n|   a |\n| bbb |\n+-----+",
        "box text applies left center and right alignment with odd-slack bias");

    suite.Expect(RenderBoxText(L"x", BoxBorderStyle::Single, 2, BoxAlignment::Left,
        L" \r My\nTitle \t") ==
        L"\u250C\u2500 My Title \u2500\u2510\n\u2502  x         \u2502\n\u2514\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2518",
        "box text normalizes and trims titles before embedding the title bar");
    suite.Expect(RenderBoxText(L"", BoxBorderStyle::Ascii, 0, BoxAlignment::Left, L"X") ==
        L"+-+\n| |\n+-+",
        "box text replaces an over-wide title with a closed horizontal bar");

    suite.Expect(RenderBoxText(L"a\tb\r\nc\rd\n", BoxBorderStyle::Ascii, 0,
        BoxAlignment::Left, L"") ==
        L"+------+\n|a    b|\n|c     |\n|d     |\n|      |\n+------+",
        "box text normalizes CRLF CR and LF while expanding tabs and keeping trailing rows");

    suite.Expect(RenderBoxText(L"e\u0301\n\u20DD\n\u093E\n\u0897", BoxBorderStyle::Ascii, 0,
        BoxAlignment::Left, L"") ==
        L"+-+\n|e\u0301|\n|\u20DD |\n|\u093E|\n|\u0897 |\n+-+",
        "box width skips nonspacing and enclosing marks but counts spacing combining marks");
    suite.Expect(RenderBoxText(emoji, BoxBorderStyle::Ascii, 0, BoxAlignment::Left, L"") ==
        L"+--+\n|" + emoji + L"|\n+--+" &&
        RenderBoxText(isolatedHigh, BoxBorderStyle::Ascii, 0, BoxAlignment::Left, L"") ==
        L"+-+\n|" + isolatedHigh + L"|\n+-+",
        "box width counts supplementary pairs as two UTF-16 columns and lone surrogates as one");

    suite.Expect(RenderBoxText(L"a\n", BoxBorderStyle::CommentSlash, 2, BoxAlignment::Right, L"T") ==
        L"/*\n * T\n *\n *   a\n *   \n */",
        "box text renders slash comments with title separator padding and trailing records");
    suite.Expect(RenderBoxText(L"a\nbb", BoxBorderStyle::CommentHash, 1, BoxAlignment::Center, L"T") ==
        L"##########\n### T\n##########\n###  a\n###  bb\n##########" &&
        RenderBoxText(L"", BoxBorderStyle::CommentHash, 0, BoxAlignment::Left, L"") ==
        L"######\n### \n######",
        "box text renders hash comments with managed bar sizing and optional title");

    auto const zeroPadding = L"+-+\n|x|\n+-+";
    auto const fortyPadding = L"+" + std::wstring(81, L'-') + L"+\n|" +
        std::wstring(40, L' ') + L"x" + std::wstring(40, L' ') + L"|\n+" +
        std::wstring(81, L'-') + L"+";
    suite.Expect(RenderBoxText(L"x", BoxBorderStyle::Ascii, -10, BoxAlignment::Left, L"") == zeroPadding &&
        RenderBoxText(L"x", BoxBorderStyle::Ascii, 100, BoxAlignment::Left, L"") == fortyPadding,
        "box text clamps padding to the managed zero-through-forty range");
    suite.Expect(RenderBoxText(L"x", static_cast<BoxBorderStyle>(99), 0,
        static_cast<BoxAlignment>(99), L"") == zeroPadding,
        "box text defaults unknown grid style and alignment values to ASCII left");

    suite.Expect(EncodeHtmlEntities(L"&<>\"'", false) == L"&amp;&lt;&gt;&quot;&#39;",
        "HTML encode always escapes the five managed must-escape characters");
    auto const unescapedUnicode = std::wstring(L"\u00E9") + emoji + CodeUnit(0x001Fu);
    suite.Expect(EncodeHtmlEntities(unescapedUnicode, false) == unescapedUnicode,
        "HTML encode preserves valid Unicode and controls when non-ASCII escaping is off");
    auto const escapedUnicode = std::wstring(L"~\u007F\u00E9") + emoji +
        CodeUnit(0x001Fu) + L"\t\n\r";
    suite.Expect(EncodeHtmlEntities(escapedUnicode, true) ==
        L"~&#x7F;&#xE9;&#x1F600;&#x1F;\t\n\r",
        "HTML encode emits uppercase scalar hex while preserving tab LF and CR");

    auto const invalidInput = std::wstring(L"&") + isolatedHigh + L"<";
    suite.Expect(EncodeHtmlEntities(invalidInput, false) == invalidInput &&
        EncodeHtmlEntities(invalidInput, true) == L"&amp;&#xD800;&lt;",
        "HTML encode discards partial output on invalid UTF-16 unless numeric escaping preserves it");
    auto const isolatedLow = CodeUnit(0xDC00u);
    suite.Expect(EncodeHtmlEntities(isolatedLow, false) == isolatedLow &&
        EncodeHtmlEntities(isolatedLow, true) == L"&#xDC00;",
        "HTML encode preserves isolated low-surrogate behavior");

    suite.Expect(DecodeHtmlEntities(L"&amp;&lt;&gt;&quot;&apos;&nbsp;") == L"&<>\"'\u00A0" &&
        DecodeHtmlEntities(L"&AMP;&unknown;&copy") == L"&AMP;&unknown;&copy",
        "HTML decode is case-sensitive and leaves unknown or unterminated names untouched");
    bool namedSamplesExact = true;
    for (auto const& sample : NamedSamples)
    {
        auto encoded = std::wstring(L"&");
        encoded.append(sample.name).push_back(L';');
        namedSamplesExact = namedSamplesExact && DecodeHtmlEntities(encoded) == Scalar(sample.codePoint);
    }
    suite.Expect(namedSamplesExact,
        "HTML decode covers core Latin Greek typography arrow math and symbol entity groups");

    suite.Expect(DecodeHtmlEntities(L"&#65; &#  +65\t; &#-0;") ==
        std::wstring(L"A A ") + std::wstring(1, L'\0'),
        "HTML decode accepts invariant integer whitespace sign and negative zero");
    suite.Expect(DecodeHtmlEntities(L"&#x41; &#X1f600; &#x 41\r;") ==
        std::wstring(L"A ") + emoji + L" A",
        "HTML decode accepts lowercase uppercase and whitespace-padded hex scalars");
    auto const decimalTerminalNull = std::wstring(L"&#65 ") + std::wstring(1, L'\0') + L";";
    auto const hexTerminalNulls = std::wstring(L"&#x41\t") + std::wstring(2, L'\0') + L";";
    auto const decimalInteriorNull = std::wstring(L"&#65") + std::wstring(1, L'\0') + L" ;";
    auto const hexInteriorNull = std::wstring(L"&#x41") + std::wstring(1, L'\0') + L"\t;";
    suite.Expect(DecodeHtmlEntities(decimalTerminalNull) == L"A" &&
        DecodeHtmlEntities(hexTerminalNulls) == L"A" &&
        DecodeHtmlEntities(decimalInteriorNull) == decimalInteriorNull &&
        DecodeHtmlEntities(hexInteriorNull) == hexInteriorNull,
        "HTML decode mirrors CoreLib terminal-NUL ordering without accepting interior NULs");
    suite.Expect(DecodeHtmlEntities(L"&#x10FFFF;") == Scalar(0x10FFFFu) &&
        DecodeHtmlEntities(L"&#0;") == std::wstring(1, L'\0'),
        "HTML decode accepts the full valid Unicode scalar range including null");
    suite.Expect(DecodeHtmlEntities(L"&#-1; &#1114112; &#55296; &#xDFFF; &#2147483648; &#xFFFFFFFF;") ==
        L"&#-1; &#1114112; &#55296; &#xDFFF; &#2147483648; &#xFFFFFFFF;",
        "HTML decode preserves negative out-of-range surrogate and overflowing numeric entities");

    auto const acceptedWindow = std::wstring(L"&#") + std::wstring(28, L'0') + L"65;";
    auto const rejectedWindow = std::wstring(L"&#") + std::wstring(29, L'0') + L"65;";
    suite.Expect(DecodeHtmlEntities(acceptedWindow) == L"A" &&
        DecodeHtmlEntities(rejectedWindow) == rejectedWindow,
        "HTML decode accepts a semicolon exactly 32 UTF-16 units away and rejects 33");
    auto const nestedPastWindow = std::wstring(L"&") + std::wstring(28, L'x') + L"&amp;";
    suite.Expect(DecodeHtmlEntities(nestedPastWindow) ==
        std::wstring(L"&") + std::wstring(28, L'x') + L"&" &&
        DecodeHtmlEntities(L"&bogus&amp;") == L"&bogus&",
        "HTML decode resumes scanning after an over-window or unresolved ampersand");
    suite.Expect(DecodeHtmlEntities(std::wstring(L"x") + isolatedHigh + L"&amp;") ==
        std::wstring(L"x") + isolatedHigh + L"&",
        "HTML decode preserves raw invalid UTF-16 while resolving later entities");

    auto const& references = HtmlEntityReferenceList();
    bool referenceExact = references.size() == std::size(ReferenceSamples);
    for (std::size_t index{}; referenceExact && index < references.size(); ++index)
    {
        referenceExact = references[index].name == ReferenceSamples[index].name &&
            references[index].character == Scalar(ReferenceSamples[index].codePoint) &&
            references[index].description_en == ReferenceSamples[index].description;
    }
    suite.Expect(referenceExact,
        "HTML reference list preserves all fifty managed English descriptions and display order");
    bool cantoneseComplete = references.size() == std::size(ReferenceSamples);
    for (std::size_t index{}; cantoneseComplete && index < references.size(); ++index)
    {
        cantoneseComplete = !references[index].description_zh.empty() &&
            references[index].description_zh != references[index].description_en;
    }
    suite.Expect(cantoneseComplete,
        "HTML reference list provides distinct nonempty Cantonese descriptions for all fifty rows");
    suite.Expect(references.size() == 50 &&
        references[0].description_zh == L"and 符號" &&
        references[5].description_zh == L"不換行空格" &&
        references[41].description_zh == L"階磚" &&
        references[43].description_zh == L"剔號" &&
        references[49].description_zh == L"平方根",
        "HTML reference Cantonese sentinel translations remain stable and locally natural");
    suite.Expect(HtmlEntityUtf16Length(emoji) == emoji.size() &&
        HtmlEntityUtf16Length(isolatedHigh) == 1 && HtmlEntityUtf16Length(L"") == 0,
        "HTML length reports exact UTF-16 code units including invalid input");

    std::cout << "\nreference text tests: " << suite.counts.passed << " passed, "
        << suite.counts.failed << " failed\n";
    return suite.counts;
}

#if defined(WINFORGE_REFERENCE_TEXT_STANDALONE)
int wmain()
{
    auto const counts = RunReferenceTextTests();
    return counts.failed == 0 ? 0 : 1;
}
#endif
