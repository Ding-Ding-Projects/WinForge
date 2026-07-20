#include "PercentCalcTests.h"

#include "PercentCalc.h"

#include <algorithm>
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

    [[nodiscard]] bool Near(double actual, double expected, double tolerance = 1.0e-12)
    {
        if (std::isnan(actual) || std::isnan(expected)) return false;
        auto const scale = (std::max)({ 1.0, std::abs(actual), std::abs(expected) });
        return std::abs(actual - expected) <= tolerance * scale;
    }
}

NativeTestCounts RunPercentCalcTests()
{
    using namespace winforge::core::percentcalc;
    Suite suite;

    double parsed{};
    suite.Expect(TryParse(L"  12.5%  ", parsed) && Near(parsed, 12.5),
        "Percent Calculator parses trimmed invariant percentages");
    suite.Expect(TryParse(L"+.5e2", parsed) && Near(parsed, 50.0) &&
        TryParse(L"5.", parsed) && Near(parsed, 5.0),
        "Percent Calculator accepts managed float exponent and decimal forms");
    suite.Expect(TryParse(L" 1,25% ", parsed, L",") && Near(parsed, 1.25) &&
        TryParse(L"1.25", parsed, L",") && Near(parsed, 1.25),
        "Percent Calculator accepts current-culture commas and invariant decimal fallback");
    suite.Expect(TryParse(L"\u0085\u00A0\u200312.5\u3000", parsed) && Near(parsed, 12.5),
        "Percent Calculator trims managed Unicode whitespace");
    suite.Expect(!TryParse(L"", parsed) && !TryParse(L"%", parsed) && !TryParse(L"12%%", parsed) &&
        !TryParse(L"1,000", parsed) && !TryParse(L"1e", parsed) && !TryParse(L"\uFEFF12", parsed),
        "Percent Calculator rejects blank malformed and grouped input");
    suite.Expect(!TryParse(L"NaN", parsed) && !TryParse(L"Infinity", parsed) && !TryParse(L"1e309", parsed),
        "Percent Calculator rejects non-finite parsed input");
    suite.Expect(TryParse(L"-0", parsed) && parsed == 0.0 && std::signbit(parsed),
        "Percent Calculator preserves parsed negative zero before calculation");

    suite.Expect(Format(1.2345678) == L"1.234568" && Format(1.230000) == L"1.23" && Format(10.0) == L"10",
        "Percent Calculator formats six decimal places without trailing zeros");
    suite.Expect(Format(0.0000005) == L"0.000001" && Format(-0.0000005) == L"-0.000001" &&
        Format(-0.0000004) == L"0",
        "Percent Calculator rounds away from zero and normalizes rounded negative zero");
    suite.Expect(Format(1.25, L",") == L"1,25" &&
        Format(std::numeric_limits<double>::infinity()) == L"—" &&
        Format(std::numeric_limits<double>::quiet_NaN()) == L"—",
        "Percent Calculator localizes finite display separators and masks non-finite results");

    auto result = PercentOf(L"15", L"80");
    suite.Expect(result.ok && result.value == L"12" && Near(result.number, 12.0),
        "Percent Calculator computes X percent of Y");
    result = PercentOf(L"12.5%", L"8");
    suite.Expect(result.ok && result.value == L"1", "Percent Calculator accepts percentage suffixes for percent-of");
    suite.Expect(!PercentOf(L"x", L"8").ok && !PercentOf(L"1", L"x").ok,
        "Percent Calculator fails percent-of only for invalid input");

    result = WhatPercent(L"5", L"20");
    suite.Expect(result.ok && result.value == L"25%" && Near(result.number, 25.0),
        "Percent Calculator computes what percentage X is of Y");
    result = WhatPercent(L"-5", L"20");
    suite.Expect(result.ok && result.value == L"-25%", "Percent Calculator preserves negative what-percent values");
    suite.Expect(!WhatPercent(L"5", L"0").ok, "Percent Calculator rejects a zero what-percent denominator");

    result = PercentChange(L"80", L"100");
    suite.Expect(result.ok && result.value == L"+25%" && Near(result.number, 25.0),
        "Percent Calculator prefixes positive percent change");
    result = PercentChange(L"100", L"80");
    suite.Expect(result.ok && result.value == L"-20%", "Percent Calculator reports negative percent change");
    result = PercentChange(L"-100", L"-80");
    suite.Expect(result.ok && result.value == L"+20%", "Percent Calculator uses absolute starting value for percent change");
    suite.Expect(!PercentChange(L"0", L"10").ok, "Percent Calculator rejects a zero starting value");

    result = AdjustBy(L"200", L"10", true);
    suite.Expect(result.ok && result.value == L"220", "Percent Calculator increases a value by a percentage");
    result = AdjustBy(L"200", L"10", false);
    suite.Expect(result.ok && result.value == L"180", "Percent Calculator decreases a value by a percentage");
    result = AdjustBy(L"100", L"150", false);
    suite.Expect(result.ok && result.value == L"-50", "Percent Calculator permits a decrease past zero");
    result = PercentOf(L"1e308", L"1e308");
    suite.Expect(result.ok && result.value == L"—" && std::isinf(result.number),
        "Percent Calculator preserves valid managed arithmetic overflow as a display em dash");

    auto tip = Tip(L"100", L"15", L"4");
    suite.Expect(tip.ok && Near(tip.tipAmount, 15.0) && Near(tip.total, 115.0) && Near(tip.perPerson, 28.75),
        "Percent Calculator computes tip total and per-person share");
    tip = Tip(L"10", L"0", L"2.5");
    suite.Expect(tip.ok && Near(tip.perPerson, 5.0),
        "Percent Calculator rounds the split count to even like managed Math.Round");
    tip = Tip(L"10", L"0", L"1.5");
    suite.Expect(tip.ok && Near(tip.perPerson, 5.0),
        "Percent Calculator rounds an odd half split up to the even count");
    suite.Expect(!Tip(L"10", L"20", L"0").ok && !Tip(L"-10", L"20", L"2").ok &&
        !Tip(L"10", L"-20", L"2").ok,
        "Percent Calculator rejects invalid people negative bills and negative tips");
    tip = Tip(L"12,5", L"10", L"2", L",");
    suite.Expect(tip.ok && Near(tip.total, 13.75) && Near(tip.perPerson, 6.875),
        "Percent Calculator applies the active decimal separator to tip inputs");

    auto ratio = SimplifyRatio(L"1920", L"1080");
    suite.Expect(ratio.ok && ratio.a == 16 && ratio.b == 9, "Percent Calculator simplifies integer ratios with GCD");
    ratio = SimplifyRatio(L"1.5", L"2.25");
    suite.Expect(ratio.ok && ratio.a == 2 && ratio.b == 3, "Percent Calculator scales and simplifies decimal ratios");
    ratio = SimplifyRatio(L"-4", L"6");
    suite.Expect(ratio.ok && ratio.a == -2 && ratio.b == 3, "Percent Calculator preserves ratio signs after reduction");
    ratio = SimplifyRatio(L"0", L"6");
    suite.Expect(ratio.ok && ratio.a == 0 && ratio.b == 1, "Percent Calculator simplifies a ratio with one zero term");
    ratio = SimplifyRatio(L"0.000001", L"0.000003");
    suite.Expect(ratio.ok && ratio.a == 1 && ratio.b == 3, "Percent Calculator retains up to six decimal places for ratio scaling");
    ratio = SimplifyRatio(L"1,5", L"2,25", L",");
    suite.Expect(ratio.ok && ratio.a == 2 && ratio.b == 3, "Percent Calculator accepts locale decimal ratios");
    suite.Expect(!SimplifyRatio(L"0", L"0").ok && !SimplifyRatio(L"a", L"1").ok,
        "Percent Calculator rejects all-zero and invalid ratios");
    suite.Expect(!SimplifyRatio(L"1e30", L"1").ok,
        "Percent Calculator fails closed when a scaled ratio cannot fit a managed long");

    std::cout << "\nPercent Calculator tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
