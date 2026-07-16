#include "PasswordStrengthTests.h"

#include "PasswordStrength.h"

#include <cmath>
#include <iostream>
#include <limits>
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

    bool CloseEnough(double actual, double expected)
    {
        return std::abs(actual - expected) < 0.000000001;
    }
}

NativeTestCounts RunPasswordStrengthTests()
{
    using namespace winforge::core::passwordstrength;
    Suite suite;

    auto const empty = Analyze(L"");
    suite.Expect(
        empty.length == 0 && empty.pool_size == 0 && empty.entropy_bits == 0.0 && empty.fraction == 0.0 &&
            !empty.has_lower && !empty.no_repeats && !empty.no_sequences && !empty.is_common,
        "Password Strength preserves empty managed defaults");

    auto const mixed = Analyze(L"aA1!");
    suite.Expect(
        mixed.length == 4 && mixed.pool_size == 95 && mixed.has_lower && mixed.has_upper && mixed.has_digit && mixed.has_symbol &&
            !mixed.has_space && CloseEnough(mixed.entropy_bits, 4.0 * std::log2(95.0)) && mixed.no_repeats && mixed.no_sequences,
        "Password Strength classifies ASCII pools and entropy exactly");

    auto const space = Analyze(L"a ");
    auto const unicode = Analyze(L"\u03A9");
    suite.Expect(
        space.pool_size == 27 && space.has_lower && space.has_space && !space.has_symbol &&
            unicode.pool_size == 33 && unicode.has_symbol && !unicode.has_lower && !unicode.has_space,
        "Password Strength treats only ASCII space specially and Unicode as a symbol");

    auto const common = Analyze(L" password ");
    auto const commonCase = Analyze(L"PASSWORD");
    suite.Expect(
        common.is_common && commonCase.is_common && common.band == 0 && common.fraction == 0.02 &&
            CloseEnough(common.online_seconds, std::pow(2.0, 7.0) / 1e4),
        "Password Strength applies trimmed case-insensitive common-password caps");

    auto const repeated = Analyze(L"AAA!!!");
    auto const notRepeated = Analyze(L"aAa");
    suite.Expect(
        !repeated.no_repeats && repeated.no_sequences && repeated.band == 1 &&
            notRepeated.no_repeats,
        "Password Strength applies only contiguous case-sensitive repeat penalties");

    auto const sequence = Analyze(L"Abc123!");
    auto const reversed = Analyze(L"ZYX9!");
    auto const keyboard = Analyze(L"QaZwSx1!");
    suite.Expect(
        !sequence.no_sequences && !reversed.no_sequences && !keyboard.no_sequences &&
            sequence.no_repeats && sequence.band == 1,
        "Password Strength detects alphabetic numeric reverse and keyboard sequences");

    constexpr std::wstring_view sequenceSamples[] = {
        L"abc", L"zyx", L"012", L"987", L"qwe", L"asd", L"zxc", L"qwe", L"qaz", L"1q2"
    };
    auto allSequences = true;
    for (auto const sample : sequenceSamples)
    {
        allSequences = allSequences && !Analyze(sample).no_sequences;
    }
    suite.Expect(allSequences, "Password Strength covers every managed sequence fixture");

    auto const fair = Analyze(L"abcABC123!");
    auto const strong = Analyze(L"Ab1!Ab1!Ab1!");
    auto const veryStrong = Analyze(L"Ab1!Ab1!Ab1!Ab1!");
    suite.Expect(
        fair.band == 2 && CloseEnough(fair.entropy_bits, 10.0 * std::log2(95.0)) &&
            CloseEnough(fair.entropy_bits - 8.0, 57.6985560832854) &&
            strong.band == 3 && veryStrong.band == 4,
        "Password Strength preserves effective-entropy band thresholds");

    suite.Expect(
        CloseEnough(mixed.online_seconds / mixed.offline_gpu_seconds, 1e6) &&
            CloseEnough(mixed.online_seconds / mixed.fast_seconds, 1e8),
        "Password Strength preserves brute-force crack-rate ratios");

    suite.Expect(
        HumanTime(0.0, HumanTimeLanguage::English) == L"instantly" &&
            HumanTime(2.5, HumanTimeLanguage::English) == L"2 seconds" &&
            HumanTime(60.0, HumanTimeLanguage::English) == L"1 minute" &&
            HumanTime(90.0, HumanTimeLanguage::English) == L"2 minutes" &&
            HumanTime(60.0, HumanTimeLanguage::Cantonese) == L"1 分鐘" &&
            HumanTime(std::numeric_limits<double>::infinity(), HumanTimeLanguage::Cantonese) == L"即刻破解",
        "Password Strength preserves human-time thresholds banker's rounding and localization");

    auto const huge = Analyze(std::wstring(10000, L'a'));
    suite.Expect(
        huge.length == 10000 && huge.pool_size == 26 && !huge.no_repeats && huge.no_sequences &&
            std::isfinite(huge.entropy_bits),
        "Password Strength remains local and nonthrowing for long input");

    return suite.counts;
}
