#include "BaseConvertTests.h"

#include "BaseConvert.h"

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

    [[nodiscard]] bool ParsesTo(
        std::wstring_view input,
        int radix,
        std::wstring_view expected)
    {
        winforge::core::baseconvert::Integer value;
        return winforge::core::baseconvert::TryParse(input, radix, value) &&
            winforge::core::baseconvert::ToBase(value, 10) == expected;
    }

    [[nodiscard]] bool OperandTo(std::wstring_view input, std::wstring_view expected)
    {
        winforge::core::baseconvert::Integer value;
        return winforge::core::baseconvert::TryParseOperand(input, value) &&
            winforge::core::baseconvert::ToBase(value, 10) == expected;
    }
}

NativeTestCounts RunBaseConvertTests()
{
    using namespace winforge::core::baseconvert;
    Suite suite;

    suite.Expect(MinBase == 2 && MaxBase == 36 &&
        IsBlank(L"\u00A0\t\u3000") && !IsBlank(L" 0 ") &&
        TrimManagedWhitespace(L"\u00A0\u3000 2 \u202F") == L"2",
        "base converter exposes the managed radix bounds blank-value and diagnostic-trim contracts");

    suite.Expect(ParsesTo(L" +7_Ff f ", 16, L"32767") &&
        ParsesTo(L"-z", 36, L"-35") &&
        ParsesTo(L"1010", 2, L"10"),
        "base converter parses signed case-insensitive digits with space and underscore grouping");

    Integer invalid;
    suite.Expect(!TryParse(L"", 10, invalid) && !TryParse(L"-", 10, invalid) &&
        !TryParse(L"2", 2, invalid) && !TryParse(L"1\t0", 10, invalid) &&
        !TryParse(L"10", 1, invalid) && !TryParse(L"10", 37, invalid),
        "base converter rejects blank sign-only bad-digit internal-tab and out-of-range-base input");

    auto const veryLarge = std::wstring(
        L"12345678901234567890123456789012345678901234567890123456789012345678901234567890");
    Integer huge;
    suite.Expect(TryParse(veryLarge, 10, huge) && ToBase(huge, 10) == veryLarge &&
        ToBase(huge, 36).size() > 40,
        "base converter round-trips arbitrary precision values without a fixed-width overflow");

    Integer value;
    static_cast<void>(TryParse(L"255", 10, value));
    suite.Expect(ToBase(value, 2) == L"11111111" && ToBase(value, 8) == L"377" &&
        ToBase(value, 16) == L"ff" && ToBase(value, 1).empty(),
        "base converter renders lower-case binary octal hexadecimal and invalid-radix outputs like managed code");

    Integer negative;
    static_cast<void>(TryParse(L"-166", 10, negative));
    Integer zero;
    suite.Expect(ToGroupedBinary(value) == L"1111 1111" &&
        ToGroupedBinary(negative) == L"-1010 0110" && ToGroupedBinary(zero) == L"0000",
        "base converter groups absolute binary into managed nibble rows including zero and negatives");

    suite.Expect(ToHexPrefixed(value) == L"0xFF" && ToHexPrefixed(negative) == L"-0xA6" &&
        ToHexPrefixed(zero) == L"0x0",
        "base converter uses managed uppercase hexadecimal prefixes");

    Integer power;
    static_cast<void>(TryParse(L"100", 16, power));
    suite.Expect(BitLength(zero) == 0 && BitLength(value) == 8 && BitLength(power) == 9 &&
        BitLength(negative) == 8,
        "base converter reports magnitude significant-bit lengths");

    Integer min64;
    Integer max64;
    Integer overflow64;
    static_cast<void>(TryParse(L"-9223372036854775808", 10, min64));
    static_cast<void>(TryParse(L"9223372036854775807", 10, max64));
    static_cast<void>(TryParse(L"9223372036854775808", 10, overflow64));
    suite.Expect(FitsIn64Bits(min64) && FitsIn64Bits(max64) && !FitsIn64Bits(overflow64) &&
        To64BitBinary(value) == L"00000000 00000000 00000000 00000000 00000000 00000000 00000000 11111111" &&
        To64BitBinary(negative) == L"11111111 11111111 11111111 11111111 11111111 11111111 11111111 01011010" &&
        To64BitBinary(min64) == L"10000000 00000000 00000000 00000000 00000000 00000000 00000000 00000000" &&
        To64BitBinary(overflow64).empty(),
        "base converter preserves signed-64 fit boundaries and two's-complement display");

    suite.Expect(OperandTo(L"0xF0", L"240") && OperandTo(L" -0x0F ", L"-15") &&
        OperandTo(L"+42", L"42") && OperandTo(L"0x-1", L"-1") &&
        OperandTo(L"-0x-1", L"1") && OperandTo(L"--1", L"1"),
        "bitwise operands accept managed decimal hexadecimal and permissive nested-sign literals");
    suite.Expect(!TryParseOperand(L"0x", invalid) && !TryParseOperand(L"0b10", invalid),
        "bitwise operands reject incomplete and unsupported prefixes");

    Integer a;
    Integer b;
    static_cast<void>(TryParseOperand(L"0xF0", a));
    static_cast<void>(TryParseOperand(L"0x0F", b));
    suite.Expect(ToBase(Evaluate(BitOp::And, a, b, 0), 10) == L"0" &&
        ToBase(Evaluate(BitOp::Or, a, b, 0), 16) == L"ff" &&
        ToBase(Evaluate(BitOp::Xor, a, b, 0), 16) == L"ff" &&
        ToBase(Evaluate(BitOp::Nand, a, b, 0), 10) == L"-1" &&
        ToBase(Evaluate(BitOp::Nor, a, b, 0), 10) == L"-256",
        "base converter evaluates managed AND OR XOR NAND and NOR semantics");

    Integer five;
    Integer minusFive;
    Integer minusOne;
    static_cast<void>(TryParse(L"5", 10, five));
    static_cast<void>(TryParse(L"-5", 10, minusFive));
    static_cast<void>(TryParse(L"-1", 10, minusOne));
    suite.Expect(ToBase(Evaluate(BitOp::And, minusOne, zero, 0), 10) == L"0" &&
        ToBase(Evaluate(BitOp::Or, minusOne, zero, 0), 10) == L"-1" &&
        ToBase(Evaluate(BitOp::Xor, minusOne, zero, 0), 10) == L"-1" &&
        ToBase(Evaluate(BitOp::Nand, minusOne, five, 0), 10) == L"-6" &&
        ToBase(Evaluate(BitOp::Nor, minusOne, zero, 0), 10) == L"0",
        "base converter uses infinite signed two's-complement rules for negative bitwise operands");
    suite.Expect(ToBase(Evaluate(BitOp::LeftShift, five, zero, 3), 10) == L"40" &&
        ToBase(Evaluate(BitOp::RightShift, five, zero, 1), 10) == L"2" &&
        ToBase(Evaluate(BitOp::RightShift, minusFive, zero, 1), 10) == L"-3" &&
        ToBase(Evaluate(BitOp::RightShift, minusFive, zero, 99), 10) == L"-1" &&
        ToBase(Evaluate(BitOp::LeftShift, five, zero, -4), 10) == L"5",
        "base converter applies arithmetic signed shifts and clamps a negative shift to zero");

    Integer massiveOne;
    Integer massiveTwo;
    static_cast<void>(TryParse(L"100000000000000000000000000000000", 16, massiveOne));
    static_cast<void>(TryParse(L"ffffffffffffffffffffffffffffffff", 16, massiveTwo));
    suite.Expect(ToBase(Evaluate(BitOp::And, massiveOne, massiveTwo, 0), 16) == L"0" &&
        ToBase(Evaluate(BitOp::Or, massiveOne, massiveTwo, 0), 16) == L"1ffffffffffffffffffffffffffffffff" &&
        ToBase(Evaluate(BitOp::LeftShift, massiveOne, zero, 4), 16) == L"1000000000000000000000000000000000",
        "base converter keeps arbitrary-precision bitwise operands beyond 64 bits");

    std::cout << "\nbase converter tests: " << suite.counts.passed << " passed, "
              << suite.counts.failed << " failed\n";
    return suite.counts;
}
