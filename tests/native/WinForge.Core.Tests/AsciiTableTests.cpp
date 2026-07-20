#include "AsciiTableTests.h"

#include "AsciiTable.h"

#include <iostream>
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

    winforge::core::asciitable::Row const* ByCode(
        std::vector<winforge::core::asciitable::Row> const& rows,
        int code)
    {
        for (auto const& row : rows)
        {
            if (row.code == code) return &row;
        }
        return nullptr;
    }

    bool HasOnlyCode(
        std::vector<winforge::core::asciitable::Row> const& rows,
        int code)
    {
        return rows.size() == 1 && rows.front().code == code;
    }

    bool HasCode(
        std::vector<winforge::core::asciitable::Row> const& rows,
        int code)
    {
        return ByCode(rows, code) != nullptr;
    }
}

NativeTestCounts RunAsciiTableTests()
{
    using namespace winforge::core;
    using namespace winforge::core::asciitable;
    Suite suite;

    auto const ascii = Build(false, LanguageMode::English);
    suite.Expect(ascii.size() == 128 && ascii.front().code == 0 && ascii.back().code == 127,
        "ASCII table builds the managed inclusive 0 through 127 range");

    auto const latin1 = Build(true, LanguageMode::English);
    suite.Expect(latin1.size() == 256 && latin1.front().code == 0 && latin1.back().code == 255,
        "ASCII table Latin-1 option extends the managed range through 255");

    auto const* nul = ByCode(ascii, 0);
    suite.Expect(nul && nul->decimal == L"0" && nul->hexadecimal == L"0x00" &&
        nul->octal == L"0o0" && nul->binary == L"00000000" && nul->glyph == L"NUL" &&
        nul->name == L"NUL — Null" && nul->copy_value == std::wstring(1, L'\0'),
        "ASCII table formats NUL with managed numeric columns mnemonic description and raw copy value");

    auto const* lineFeed = ByCode(ascii, 10);
    suite.Expect(lineFeed && lineFeed->glyph == L"LF" && lineFeed->name == L"LF — Line Feed" &&
        lineFeed->copy_value == std::wstring(1, L'\n'),
        "ASCII table retains C0 mnemonic descriptions and raw control copies");

    auto const* space = ByCode(ascii, 32);
    suite.Expect(space && space->glyph == L"SP" && space->name == L"SP — Space" &&
        space->copy_value == L" ",
        "ASCII table labels space as SP while retaining its raw copy character");

    auto const* capitalA = ByCode(ascii, 65);
    suite.Expect(capitalA && capitalA->hexadecimal == L"0x41" && capitalA->octal == L"0o101" &&
        capitalA->binary == L"01000001" && capitalA->glyph == L"A" &&
        capitalA->name == L"Printable" && capitalA->copy_value == L"A",
        "ASCII table formats printable ASCII with exact hexadecimal octal and binary values");

    auto const* del = ByCode(ascii, 127);
    suite.Expect(del && del->glyph == L"DEL" && del->name == L"DEL — Delete" &&
        del->copy_value == std::wstring(1, static_cast<wchar_t>(127)),
        "ASCII table preserves the DEL mnemonic and raw delete code unit");

    auto const* c1 = ByCode(latin1, 128);
    auto const* nbsp = ByCode(latin1, 160);
    suite.Expect(c1 && c1->glyph == L"CTRL" && c1->name == L"C1 control" &&
        nbsp && nbsp->glyph == L"NBSP" && nbsp->name == L"NBSP — No-Break Space",
        "ASCII table distinguishes C1 controls from the Latin-1 NBSP boundary");

    auto const* eAcute = ByCode(latin1, 233);
    suite.Expect(eAcute && eAcute->glyph == L"é" && eAcute->hexadecimal == L"0xE9" &&
        eAcute->octal == L"0o351" && eAcute->binary == L"11101001" &&
        eAcute->name == L"Printable",
        "ASCII table preserves printable Latin-1 glyphs and 8-bit numeric formatting");

    auto const cantonese = Build(false, LanguageMode::Cantonese);
    auto const bilingual = Build(false, LanguageMode::Bilingual);
    auto const* cantoneseNul = ByCode(cantonese, 0);
    auto const* bilingualSpace = ByCode(bilingual, 32);
    suite.Expect(cantoneseNul && cantoneseNul->name == L"NUL — 空字元" &&
        bilingualSpace && bilingualSpace->name == L"SP — Space · 空格",
        "ASCII table applies Cantonese and bilingual descriptions in the pure core");

    auto const aGrave = Filter(latin1, L"\u00C0");
    auto const sharpS = Filter(latin1, L"\u1E9E");
    auto const angstrom = Filter(latin1, L"\u212B");
    suite.Expect(HasCode(aGrave, 192) && HasCode(aGrave, 224) &&
        HasOnlyCode(sharpS, 223) && HasCode(angstrom, 197) && HasCode(angstrom, 229),
        "ASCII table search follows managed invariant casing for Latin-1 glyphs and query singletons");

    suite.Expect(Filter(ascii, L"").size() == ascii.size() && Filter(ascii, L" \t\u3000 ").size() == ascii.size(),
        "ASCII table empty and managed-whitespace search retains every row");

    auto const decimal65 = Filter(ascii, L"65");
    suite.Expect(HasCode(decimal65, 53) && HasCode(decimal65, 65) && HasCode(decimal65, 101),
        "ASCII table searches every numeric text field, including octal 0o65 and hexadecimal 0x65");
    suite.Expect(HasOnlyCode(Filter(ascii, L"0x41"), 65),
        "ASCII table searches its hexadecimal column");
    suite.Expect(HasOnlyCode(Filter(ascii, L"0o101"), 65),
        "ASCII table searches its octal column");
    suite.Expect(HasOnlyCode(Filter(ascii, L"01000001"), 65),
        "ASCII table searches its binary column");

    suite.Expect(HasOnlyCode(Filter(ascii, L"nUl"), 0) &&
        HasOnlyCode(Filter(ascii, L"line feed"), 10) &&
        HasOnlyCode(Filter(ascii, L"dEl"), 127),
        "ASCII table search is ASCII case-insensitive across mnemonics and descriptions");

    suite.Expect(HasOnlyCode(Filter(cantonese, L"空字元"), 0) &&
        Filter(latin1, L"C1 control").size() == 32 &&
        HasOnlyCode(Filter(latin1, L"no-break"), 160),
        "ASCII table search includes localized descriptions C1 controls and NBSP");

    suite.Expect(Filter(ascii, L"not-a-character-code").empty(),
        "ASCII table returns no rows for an unmatched local filter");

    auto const printable = Filter(ascii, L"printable");
    suite.Expect(!printable.empty() && printable.front().code == 33 && printable.back().code == 126,
        "ASCII table filtering preserves source ordering for matching printable rows");

    suite.Expect(IsInvisibleOrControl(0) && IsInvisibleOrControl(32) && IsInvisibleOrControl(127) &&
        IsInvisibleOrControl(128) && IsInvisibleOrControl(160) && !IsInvisibleOrControl(65) &&
        !IsInvisibleOrControl(161) && !IsInvisibleOrControl(-1) && !IsInvisibleOrControl(256),
        "ASCII table control classification matches managed copy-status boundaries");

    std::cout << "\nascii table tests: " << suite.counts.passed << " passed, "
              << suite.counts.failed << " failed\n";
    return suite.counts;
}
