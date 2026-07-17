#include "BinaryTextTests.h"

#include "BinaryText.h"

#include <iostream>
#include <string>
#include <string_view>

namespace
{
    using winforge::core::binarytext::Decode;
    using winforge::core::binarytext::Encode;
    using winforge::core::binarytext::Error;
    using winforge::core::binarytext::NumericBase;

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

    std::wstring SmilingFace()
    {
        return std::wstring{ static_cast<wchar_t>(0xD83D), static_cast<wchar_t>(0xDE42) };
    }
}

NativeTestCounts RunBinaryTextTests()
{
    Suite suite;

    auto result = Encode(L"Hi", NumericBase::Binary);
    suite.Expect(result.ok && result.text == L"01001000 01101001", "Binary Text encodes ASCII in padded binary");

    result = Encode(L"Hi", NumericBase::Decimal);
    suite.Expect(result.ok && result.text == L"72 105", "Binary Text encodes decimal bytes");

    result = Encode(L"Hi", NumericBase::Octal);
    suite.Expect(result.ok && result.text == L"110 151", "Binary Text encodes octal bytes");

    result = Encode(L"Hi", NumericBase::Hex);
    suite.Expect(result.ok && result.text == L"48 69", "Binary Text encodes uppercase two-digit hex bytes");

    auto unicode = std::wstring{ L'\u00E9' } + SmilingFace();
    result = Encode(unicode, NumericBase::Decimal);
    suite.Expect(result.ok && result.text == L"195 169 240 159 153 130", "Binary Text encodes UTF-8 multibyte characters");

    result = Decode(L"01001000, 01101001\n00100001", NumericBase::Binary);
    suite.Expect(result.ok && result.text == L"Hi!", "Binary Text decodes mixed supported separators");

    result = Decode(L"0x48 0X69", NumericBase::Hex);
    suite.Expect(result.ok && result.text == L"Hi", "Binary Text accepts matching uppercase and lowercase prefixes");

    result = Decode(L"0b11000011 0b10101001", NumericBase::Binary);
    suite.Expect(result.ok && result.text == std::wstring{ L'\u00E9' }, "Binary Text decodes prefixed multibyte UTF-8");

    result = Decode(L"240 159 153 130", NumericBase::Decimal);
    suite.Expect(result.ok && result.text == SmilingFace(), "Binary Text decodes supplementary UTF-8 as a surrogate pair");

    result = Decode(L"  \t,\r\n", NumericBase::Hex);
    auto const unicodeWhitespaceOnly = Decode(
        std::wstring{
            L'\u0009', L'\u000A', L'\u000B', L'\u000C', L'\u000D', L'\u0020',
            L'\u0085', L'\u00A0', L'\u1680', L'\u2000', L'\u2001', L'\u2002',
            L'\u2003', L'\u2004', L'\u2005', L'\u2006', L'\u2007', L'\u2008',
            L'\u2009', L'\u200A', L'\u2028', L'\u2029', L'\u202F', L'\u205F', L'\u3000' },
        NumericBase::Hex);
    auto const unicodeWhitespaceAroundToken = Decode(L"\u00A048\u00A0", NumericBase::Hex);
    suite.Expect(
        result.ok && result.text.empty() &&
        unicodeWhitespaceOnly.ok && unicodeWhitespaceOnly.text.empty() &&
        unicodeWhitespaceAroundToken.ok && unicodeWhitespaceAroundToken.text == L"H",
        "Binary Text matches managed Unicode whitespace handling");

    result = Encode(L"", NumericBase::Hex);
    suite.Expect(result.ok && result.text.empty(), "Binary Text encodes an empty string");

    result = Decode(L"0x48", NumericBase::Decimal);
    suite.Expect(!result.ok && result.error == Error::InvalidToken && result.text.empty(), "Binary Text rejects a prefix for the wrong base atomically");

    result = Decode(L"00000002", NumericBase::Binary);
    suite.Expect(!result.ok && result.error == Error::InvalidToken && result.text.empty(), "Binary Text rejects invalid digits for the selected base");

    result = Decode(L"256", NumericBase::Decimal);
    suite.Expect(!result.ok && result.error == Error::OutOfRange && result.text.empty(), "Binary Text rejects byte values above 255");

    result = Decode(L"48 GG 69", NumericBase::Hex);
    suite.Expect(!result.ok && result.text.empty(), "Binary Text leaves no partial output after a malformed token");

    result = Decode(L"11000011 00101000", NumericBase::Binary);
    auto const malformedThreeBytePrefix = Decode(
        L"11100001 10000000 01000001", NumericBase::Binary);
    auto const malformedFourBytePrefix = Decode(
        L"11110000 10011111 01000001", NumericBase::Binary);
    auto const malformedFourByteLongPrefix = Decode(
        L"11110000 10011111 10010010 01000001", NumericBase::Binary);
    auto const semanticallyInvalidPrefix = Decode(
        L"11100000 10000000 01000001", NumericBase::Binary);
    auto const semanticallyInvalidTruncatedThreeByte = Decode(
        L"11100000 10000000", NumericBase::Binary);
    auto const surrogateTruncatedPrefix = Decode(
        L"11101101 10100000", NumericBase::Binary);
    auto const lowFourByteTruncatedPrefix = Decode(
        L"11110000 10000000", NumericBase::Binary);
    auto const highFourByteTruncatedPrefix = Decode(
        L"11110100 10010000", NumericBase::Binary);
    suite.Expect(
        result.ok && result.text == std::wstring{ L'\uFFFD', L'(' } &&
        malformedThreeBytePrefix.ok && malformedThreeBytePrefix.text == std::wstring{ L'\uFFFD', L'A' } &&
        malformedFourBytePrefix.ok && malformedFourBytePrefix.text == std::wstring{ L'\uFFFD', L'A' } &&
        malformedFourByteLongPrefix.ok && malformedFourByteLongPrefix.text == std::wstring{ L'\uFFFD', L'A' } &&
        semanticallyInvalidPrefix.ok && semanticallyInvalidPrefix.text == std::wstring{ L'\uFFFD', L'\uFFFD', L'A' } &&
        semanticallyInvalidTruncatedThreeByte.ok && semanticallyInvalidTruncatedThreeByte.text == std::wstring{ L'\uFFFD', L'\uFFFD' } &&
        surrogateTruncatedPrefix.ok && surrogateTruncatedPrefix.text == std::wstring{ L'\uFFFD', L'\uFFFD' } &&
        lowFourByteTruncatedPrefix.ok && lowFourByteTruncatedPrefix.text == std::wstring{ L'\uFFFD', L'\uFFFD' } &&
        highFourByteTruncatedPrefix.ok && highFourByteTruncatedPrefix.text == std::wstring{ L'\uFFFD', L'\uFFFD' },
        "Binary Text matches managed grouped and isolated UTF-8 replacement behavior");

    result = Decode(L"11110000 10011111 10010010", NumericBase::Binary);
    suite.Expect(result.ok && result.text == std::wstring{ L'\uFFFD' }, "Binary Text coalesces a truncated UTF-8 sequence into one replacement");

    result = Decode(L"11100000 10000000 10000000", NumericBase::Binary);
    suite.Expect(result.ok && result.text == std::wstring{ L'\uFFFD', L'\uFFFD', L'\uFFFD' }, "Binary Text matches managed replacement for an overlong three-byte sequence");

    result = Decode(L"11101101 10100000 10000000", NumericBase::Binary);
    suite.Expect(result.ok && result.text == std::wstring{ L'\uFFFD', L'\uFFFD', L'\uFFFD' }, "Binary Text matches managed replacement for UTF-8 surrogate bytes");

    result = Decode(L"11110100 10010000 10000000 10000000", NumericBase::Binary);
    suite.Expect(result.ok && result.text == std::wstring{ L'\uFFFD', L'\uFFFD', L'\uFFFD', L'\uFFFD' }, "Binary Text matches managed replacement for an out-of-range scalar");

    std::wstring invalidUtf16{ static_cast<wchar_t>(0xD800) };
    result = Encode(invalidUtf16, NumericBase::Hex);
    suite.Expect(result.ok && result.text == L"EF BF BD", "Binary Text replacement-encodes malformed UTF-16 input");

    for (auto const base : { NumericBase::Binary, NumericBase::Octal, NumericBase::Decimal, NumericBase::Hex })
    {
        auto const encoded = Encode(std::wstring{ L'\u00E9' } + SmilingFace(), base);
        auto const decoded = encoded.ok ? Decode(encoded.text, base) : decltype(Decode(L"", base)){};
        suite.Expect(encoded.ok && decoded.ok && decoded.text == unicode, "Binary Text round-trips every numeric base");
    }

    std::cout << "\nBinary Text tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
