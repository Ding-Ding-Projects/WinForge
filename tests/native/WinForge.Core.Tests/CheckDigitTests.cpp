#include "CheckDigitTests.h"

#include "CheckDigit.h"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <string>
#include <string_view>

namespace
{
    using winforge::core::checkdigit::Result;
    using winforge::core::checkdigit::Scheme;
    using winforge::core::checkdigit::Validate;

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

    struct RegistryFixture
    {
        std::wstring canonical;
        std::size_t rows{};
    };

    std::filesystem::path FindRegistryFixture()
    {
        auto const sourceRelative = std::filesystem::path(__FILE__).parent_path()
            / L".." / L".." / L"fixtures" / L"iban-registry-release-102.txt";
        if (std::filesystem::exists(sourceRelative))
        {
            return sourceRelative;
        }

        auto current = std::filesystem::current_path();
        for (int depth = 0; depth < 8; ++depth)
        {
            auto const candidate = current / L"tests" / L"fixtures" / L"iban-registry-release-102.txt";
            if (std::filesystem::exists(candidate))
            {
                return candidate;
            }
            if (!current.has_parent_path() || current.parent_path() == current) break;
            current = current.parent_path();
        }
        throw std::runtime_error("Release 102 IBAN registry fixture was not found");
    }

    RegistryFixture LoadRegistryFixture()
    {
        std::ifstream stream(FindRegistryFixture(), std::ios::binary);
        if (!stream) throw std::runtime_error("Release 102 IBAN registry fixture could not be opened");

        RegistryFixture fixture;
        std::string line;
        while (std::getline(stream, line))
        {
            if (!line.empty() && line.back() == '\r') line.pop_back();
            if (line.empty() || line.front() == '#') continue;
            if (std::count(line.begin(), line.end(), '|') != 2)
                throw std::runtime_error("Malformed Release 102 IBAN registry fixture row");
            for (char character : line)
            {
                if (static_cast<unsigned char>(character) > 0x7F)
                    throw std::runtime_error("Non-ASCII Release 102 IBAN registry fixture row");
                fixture.canonical.push_back(character == '|' ? L'\t' : static_cast<wchar_t>(character));
            }
            fixture.canonical.push_back(L'\n');
            ++fixture.rows;
        }
        return fixture;
    }
}

NativeTestCounts RunCheckDigitTests()
{
    Suite suite;

    auto result = Validate(Scheme::Luhn, L"4111 1111-1111 1111");
    suite.Expect(result.ok && result.valid && result.computed == L"1", "Luhn validates separators and computes the check digit");
    suite.Expect(result.detail_en.find(L"Visa") != std::wstring::npos, "Luhn detects Visa");

    result = Validate(Scheme::Luhn, L"4111111111111112");
    suite.Expect(result.ok && !result.valid && result.computed == L"1", "Luhn rejects an altered check digit");

    result = Validate(Scheme::Luhn, L"378282246310005");
    suite.Expect(result.ok && result.valid && result.detail_en.find(L"Amex") != std::wstring::npos, "Luhn detects Amex");

    result = Validate(Scheme::Luhn, L"5555555555554444");
    suite.Expect(result.ok && result.valid && result.detail_en.find(L"Mastercard") != std::wstring::npos, "Luhn detects Mastercard");

    result = Validate(Scheme::Luhn, L"6011111111111117");
    suite.Expect(result.ok && result.valid && result.detail_en.find(L"Discover") != std::wstring::npos, "Luhn detects Discover");

    auto const discover6011 = Validate(Scheme::Luhn, L"6011000000000000001");
    auto const discover6589 = Validate(Scheme::Luhn, L"6589000000000000006");
    suite.Expect(discover6011.ok && discover6011.valid && discover6011.detail_en.find(L"Discover") != std::wstring::npos
        && discover6589.ok && discover6589.valid && discover6589.detail_en.find(L"Discover") != std::wstring::npos,
        "Luhn detects official 19-digit Discover range boundaries");

    result = Validate(Scheme::Luhn, L"6590000000000000004");
    suite.Expect(result.ok && result.valid && result.detail_en.find(L"Discover") == std::wstring::npos,
        "Luhn does not mislabel the adjacent 6590 range as Discover");

    suite.Expect(!Validate(Scheme::Luhn, L"").ok, "Luhn rejects empty input");
    suite.Expect(!Validate(Scheme::Luhn, L"7").ok, "Luhn rejects one digit");
    suite.Expect(!Validate(Scheme::Luhn, L"4111A").ok, "Luhn rejects non-digits");
    suite.Expect(!Validate(Scheme::Luhn, L"４１１１").ok, "Luhn rejects non-ASCII numerals");
    suite.Expect(Validate(Scheme::Luhn, std::wstring(10'000, L'0')).valid,
        "Luhn keeps arithmetic bounded for very long numeric input");

    result = Validate(Scheme::Isbn10, L"0-306-40615-2");
    suite.Expect(result.ok && result.valid && result.computed == L"2", "ISBN-10 validates a standard sample");

    result = Validate(Scheme::Isbn10, L"0-8044-2957-x");
    suite.Expect(result.ok && result.valid && result.computed == L"X", "ISBN-10 accepts a lowercase X check digit");

    result = Validate(Scheme::Isbn10, L"0-306-40615-3");
    suite.Expect(result.ok && !result.valid && result.computed == L"2", "ISBN-10 rejects an altered check digit");

    suite.Expect(!Validate(Scheme::Isbn10, L"030640615").ok, "ISBN-10 rejects a bad length");
    suite.Expect(!Validate(Scheme::Isbn10, L"X306406152").ok, "ISBN-10 permits X only in the final position");

    result = Validate(Scheme::Isbn13, L"978-0-306-40615-7");
    suite.Expect(result.ok && result.valid && result.computed == L"7", "ISBN-13 validates separators and check digit");

    result = Validate(Scheme::Isbn13, L"9780306406158");
    suite.Expect(result.ok && !result.valid && result.computed == L"7", "ISBN-13 rejects an altered check digit");

    suite.Expect(!Validate(Scheme::Isbn13, L"978030640615A").ok, "ISBN-13 rejects non-digits");
    suite.Expect(!Validate(Scheme::Isbn13, L"978030640615").ok, "ISBN-13 rejects a bad length");
    suite.Expect(!Validate(Scheme::Isbn13, L"4006381333931").ok,
        "ISBN-13 rejects a valid non-book EAN-13 prefix");

    result = Validate(Scheme::Isbn13, L"979-10-90636-07-1");
    suite.Expect(result.ok && result.valid && result.computed == L"1", "ISBN-13 accepts a valid 979 prefix");

    result = Validate(Scheme::Ean13, L"4006381333931");
    suite.Expect(result.ok && result.valid && result.computed == L"1", "EAN-13 validates the GS1 sample");

    result = Validate(Scheme::Ean13, L"4006381333932");
    suite.Expect(result.ok && !result.valid && result.computed == L"1", "EAN-13 rejects an altered check digit");

    suite.Expect(!Validate(Scheme::Ean13, L"400638133393").ok, "EAN-13 rejects a bad length");

    result = Validate(Scheme::UpcA, L"036000291452");
    suite.Expect(result.ok && result.valid && result.computed == L"2", "UPC-A validates a standard sample");

    result = Validate(Scheme::UpcA, L"036000291453");
    suite.Expect(result.ok && !result.valid && result.computed == L"2", "UPC-A rejects an altered check digit");

    suite.Expect(!Validate(Scheme::UpcA, L"03600029145").ok, "UPC-A rejects a bad length");

    result = Validate(Scheme::Iban, L"GB82 WEST 1234 5698 7654 32");
    suite.Expect(result.ok && result.valid && result.computed == L"82", "IBAN validates the UK mod-97 sample");
    suite.Expect(result.detail_en.find(L"mod-97 = 1") != std::wstring::npos, "IBAN reports its incremental mod-97 remainder");

    result = Validate(Scheme::Iban, L"GB00 WEST 1234 5698 7654 32");
    suite.Expect(result.ok && !result.valid && result.computed == L"82", "IBAN computes corrected check digits");

    result = Validate(Scheme::Iban, L"DE89 3704 0044 0532 0130 00");
    suite.Expect(result.ok && result.valid && result.computed == L"89", "IBAN validates a second country sample");

    result = Validate(Scheme::Iban, L"fr14 2004 1010 0505 0001 3m02 606");
    suite.Expect(result.ok && result.valid && result.computed == L"14",
        "IBAN validates and normalizes the registered alphanumeric FR format");

    result = Validate(Scheme::Iban, L"MT84 MALT 0110 0001 2345 MTLC AST0 01S");
    suite.Expect(result.ok && result.valid && result.computed == L"84",
        "IBAN validates the registered alphanumeric MT format");

    suite.Expect(!Validate(Scheme::Iban, L"GB1").ok, "IBAN enforces its lower length bound");
    suite.Expect(!Validate(Scheme::Iban, L"GB82$WEST12345698765432").ok, "IBAN rejects punctuation");
    suite.Expect(!Validate(Scheme::Iban, L"1282WEST12345698765432").ok, "IBAN requires a two-letter country");
    suite.Expect(!Validate(Scheme::Iban, L"GB82WEST123456987654321234567890123").ok, "IBAN enforces its upper length bound");
    suite.Expect(!Validate(Scheme::Iban, L"GBAHU").ok, "IBAN requires two ASCII numeric check digits");
    suite.Expect(!Validate(Scheme::Iban, L"GB39A").ok, "IBAN enforces the registered country length");
    suite.Expect(!Validate(Scheme::Iban, L"ZZ6600000000000").ok, "IBAN rejects an unregistered country prefix");
    suite.Expect(!Validate(Scheme::Iban, L"GB85ABCDEFGHIJKLMNOPQR").ok,
        "IBAN enforces the registered country BBAN structure before mod-97");
    suite.Expect(!Validate(Scheme::Iban, L"GB８２WEST12345698765432").ok,
        "IBAN rejects non-ASCII check digits");
    suite.Expect(!Validate(Scheme::Iban, L"MT84MALT011000012345MTLCAST001ſ").ok,
        "IBAN rejects Unicode letters that case-fold to ASCII");

    try
    {
        auto const fixture = LoadRegistryFixture();
        suite.Expect(fixture.rows == 89 && fixture.canonical == winforge::core::checkdigit::IbanRegistryCanonicalForTests(),
            "IBAN registry exactly matches the independent SWIFT Release 102 fixture");
    }
    catch (std::exception const& error)
    {
        std::cerr << "Release 102 fixture error: " << error.what() << '\n';
        suite.Expect(false, "IBAN registry exactly matches the independent SWIFT Release 102 fixture");
    }

    result = Validate(static_cast<Scheme>(999), L"123");
    suite.Expect(!result.ok && result.detail_en == L"Unknown scheme.", "unknown check-digit scheme fails closed");

    std::cout << "\nCheck-digit tests: " << suite.counts.passed << " passed, "
        << suite.counts.failed << " failed\n";
    return suite.counts;
}
