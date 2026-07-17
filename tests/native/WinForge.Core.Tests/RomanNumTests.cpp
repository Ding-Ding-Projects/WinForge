#include "RomanNumTests.h"

#include "RomanNum.h"

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
}

NativeTestCounts RunRomanNumTests()
{
    using namespace winforge::core::romannum;
    Suite suite;

    auto numeral = ToRoman(1, false);
    suite.Expect(numeral.ok && numeral.roman == L"I", "Roman Numerals encodes one");

    numeral = ToRoman(1'994, false);
    suite.Expect(numeral.ok && numeral.roman == L"MCMXCIV" && numeral.breakdown == L"M + CM + XC + IV",
        "Roman Numerals uses canonical subtractive standard notation");

    numeral = ToRoman(3'999, false);
    suite.Expect(numeral.ok && numeral.roman == L"MMMCMXCIX", "Roman Numerals encodes the standard maximum");

    numeral = ToRoman(4'000, false);
    suite.Expect(!numeral.ok && numeral.reason_en.find(L"3,999") != std::wstring::npos,
        "Roman Numerals rejects extended values while standard mode is selected");

    numeral = ToRoman(3'999'999, true);
    suite.Expect(numeral.ok && numeral.roman == L"M̅M̅M̅C̅M̅X̅C̅I̅X̅CMXCIX",
        "Roman Numerals encodes the extended vinculum maximum");

    auto number = ToNumber(L"MCMXCIV", false);
    suite.Expect(number.ok && number.value == 1'994 && number.breakdown == L"M + CM + XC + IV",
        "Roman Numerals parses canonical standard input");

    number = ToNumber(L"(IV)", true);
    suite.Expect(number.ok && number.value == 4'000, "Roman Numerals accepts canonical parenthetical thousand notation");

    number = ToNumber(L"M\x0305", true);
    suite.Expect(number.ok && number.value == 1'000'000, "Roman Numerals parses canonical combining-overline vinculum notation");

    number = ToNumber(L"M\x0305", false);
    suite.Expect(!number.ok && number.reason_en.find(L"3999") != std::wstring::npos,
        "Roman Numerals requires Extended for vinculum values");

    number = ToNumber(L"mcmxciv", false);
    suite.Expect(!number.ok && number.reason_en.starts_with(L"Malformed"),
        "Roman Numerals preserves the managed canonical-case requirement");

    number = ToNumber(L"(I)V", true);
    suite.Expect(!number.ok && number.reason_en.starts_with(L"Malformed"),
        "Roman Numerals rejects noncanonical mixed parenthetical notation");

    number = ToNumber(L"IIII", false);
    suite.Expect(!number.ok && number.reason_en.starts_with(L"Malformed"),
        "Roman Numerals rejects repeated noncanonical symbols");

    number = ToNumber(L"IC", false);
    suite.Expect(!number.ok && number.reason_en.starts_with(L"Malformed"),
        "Roman Numerals rejects invalid subtractive pairs");

    number = ToNumber(L"(I", true);
    suite.Expect(!number.ok && number.reason_en.starts_with(L"Unbalanced"),
        "Roman Numerals rejects unbalanced parentheses");

    std::int64_t parsed{};
    suite.Expect(TryParseInteger(L"3,999,999", parsed) && parsed == 3'999'999,
        "Roman Numerals accepts grouped integer input");
    suite.Expect(!TryParseInteger(L"3.14", parsed), "Roman Numerals rejects non-integral input");
    suite.Expect(FormatGrouped(3'999'999) == L"3,999,999", "Roman Numerals formats grouped output");

    std::cout << "\nRoman Numerals tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
