#include "DesignToolsTests.h"

#include "DesignTools.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <iostream>
#include <limits>
#include <stdexcept>
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
        auto const scale = std::max({ 1.0, std::abs(actual), std::abs(expected) });
        return std::abs(actual - expected) <= tolerance * scale;
    }
}

NativeTestCounts RunDesignToolsTests()
{
    Suite suite;

    using namespace winforge::core::aspectratio;
    suite.Expect(Gcd(1'920, 1'080) == 120, "Aspect Ratio computes a long Euclidean GCD");
    suite.Expect(Gcd(-54, 24) == 6 && Gcd(54, -24) == 6, "Aspect Ratio GCD normalizes negative operands");
    suite.Expect(Gcd(0, 0) == 1, "Aspect Ratio GCD preserves the managed zero-zero fallback");
    suite.Expect(Gcd(0, 37) == 37 && Gcd(37, 0) == 37, "Aspect Ratio GCD handles one zero operand");
    auto minimumOverflow = false;
    try
    {
        static_cast<void>(Gcd(std::numeric_limits<std::int64_t>::min(), 1));
    }
    catch (std::overflow_error const&)
    {
        minimumOverflow = true;
    }
    suite.Expect(minimumOverflow, "Aspect Ratio mirrors Math.Abs(long.MinValue) overflow");

    std::int64_t ratioWidth{ 99 };
    std::int64_t ratioHeight{ 99 };
    suite.Expect(Simplify(1'920.0, 1'080.0, ratioWidth, ratioHeight) && ratioWidth == 16 && ratioHeight == 9,
        "Aspect Ratio simplifies a Full HD resolution");
    suite.Expect(Simplify(3'440.0, 1'440.0, ratioWidth, ratioHeight) && ratioWidth == 43 && ratioHeight == 18,
        "Aspect Ratio preserves a non-preset ultrawide ratio");
    suite.Expect(Simplify(1.5, 2.5, ratioWidth, ratioHeight) && ratioWidth == 1 && ratioHeight == 1,
        "Aspect Ratio rounds both midpoint dimensions to even");
    suite.Expect(Simplify(2.5, 3.5, ratioWidth, ratioHeight) && ratioWidth == 1 && ratioHeight == 2,
        "Aspect Ratio rounds odd and even midpoint dimensions independently");
    suite.Expect(Simplify(0.6, 0.6, ratioWidth, ratioHeight) && ratioWidth == 1 && ratioHeight == 1,
        "Aspect Ratio accepts positive subpixel dimensions that round to one");
    suite.Expect(!Simplify(0.5, 2.0, ratioWidth, ratioHeight) && ratioWidth == 0 && ratioHeight == 0,
        "Aspect Ratio rejects a midpoint dimension that rounds to zero");
    suite.Expect(!Simplify(0.0, 100.0, ratioWidth, ratioHeight) && ratioWidth == 0 && ratioHeight == 0,
        "Aspect Ratio rejects zero dimensions and clears outputs");
    suite.Expect(!Simplify(-1.0, 100.0, ratioWidth, ratioHeight), "Aspect Ratio rejects negative dimensions");
    suite.Expect(!Simplify(std::numeric_limits<double>::quiet_NaN(), 100.0, ratioWidth, ratioHeight),
        "Aspect Ratio rejects NaN dimensions");
    suite.Expect(!Simplify(std::numeric_limits<double>::infinity(), 100.0, ratioWidth, ratioHeight),
        "Aspect Ratio rejects infinite dimensions");
    suite.Expect(!Simplify(9'223'372'036'854'775'808.0, 1.0, ratioWidth, ratioHeight),
        "Aspect Ratio rejects a rounded dimension outside signed long range");

    suite.Expect(Near(DecimalRatio(16.0, 9.0), 16.0 / 9.0), "Aspect Ratio returns the decimal width-height quotient");
    suite.Expect(std::isnan(DecimalRatio(-16.0, 9.0)) && std::isnan(DecimalRatio(16.0, 0.0)),
        "Aspect Ratio rejects invalid decimal-ratio inputs");
    suite.Expect(Near(Megapixels(1'920.0, 1'080.0), 2.0736), "Aspect Ratio reports megapixels");
    suite.Expect(std::isnan(Megapixels(std::numeric_limits<double>::infinity(), 1.0)),
        "Aspect Ratio rejects non-finite megapixel inputs");
    suite.Expect(Near(HeightForWidth(16.0, 9.0, 1'920.0), 1'080.0),
        "Aspect Ratio scales height from a locked ratio");
    suite.Expect(Near(WidthForHeight(16.0, 9.0, 1'080.0), 1'920.0),
        "Aspect Ratio scales width from a locked ratio");
    suite.Expect(std::isnan(HeightForWidth(0.0, 9.0, 1'920.0)) &&
        std::isnan(WidthForHeight(16.0, 9.0, -1.0)),
        "Aspect Ratio scaling rejects non-positive inputs");
    suite.Expect(std::isinf(HeightForWidth(1.0, std::numeric_limits<double>::max(), 2.0)),
        "Aspect Ratio preserves managed arithmetic overflow after valid guards");

    using namespace winforge::core::cssunits;
    constexpr std::array<std::wstring_view, 11> expectedUnits{
        L"px", L"em", L"rem", L"pt", L"pc", L"%", L"vw", L"vh", L"cm", L"mm", L"in"
    };
    suite.Expect(Units == expectedUnits,
        "CSS Units exposes all eleven units in managed display order");
    Context context;
    suite.Expect(context.rootFontPx == 16.0 && context.elementFontPx == 16.0 &&
        context.viewportWidthPx == 1'920.0 && context.viewportHeightPx == 1'080.0 && context.containerPx == 1'000.0,
        "CSS Units preserves every managed context default");
    suite.Expect(PxPerIn == 96.0 && Near(PxPerPt, 4.0 / 3.0) && PxPerPc == 16.0,
        "CSS Units uses the CSS 96-DPI inch point and pica constants");
    suite.Expect(Near(PxPerCm, 96.0 / 2.54) && Near(PxPerMm, 96.0 / 25.4),
        "CSS Units uses exact CSS centimetre and millimetre constants");

    suite.Expect(Near(FromPx(96.0, L"in", &context), 1.0), "CSS Units converts px to inches");
    suite.Expect(Near(FromPx(96.0, L"pt", &context), 72.0), "CSS Units converts px to points");
    suite.Expect(Near(FromPx(96.0, L"pc", &context), 6.0), "CSS Units converts px to picas");
    suite.Expect(Near(FromPx(96.0, L"cm", &context), 2.54), "CSS Units converts px to centimetres");
    suite.Expect(Near(FromPx(96.0, L"mm", &context), 25.4), "CSS Units converts px to millimetres");
    suite.Expect(Near(ToPx(1.0, L"in", &context), 96.0) && Near(ToPx(72.0, L"pt", &context), 96.0) &&
        Near(ToPx(6.0, L"pc", &context), 96.0),
        "CSS Units converts absolute inch point and pica inputs to px");
    suite.Expect(Near(ToPx(2.54, L"cm", &context), 96.0) && Near(ToPx(25.4, L"mm", &context), 96.0),
        "CSS Units converts metric absolute inputs to px");

    suite.Expect(Near(FromPx(32.0, L"em", &context), 2.0) && Near(FromPx(32.0, L"rem", &context), 2.0),
        "CSS Units resolves em and rem from their font contexts");
    suite.Expect(Near(FromPx(250.0, L"%", &context), 25.0), "CSS Units resolves container percentages");
    suite.Expect(Near(FromPx(192.0, L"vw", &context), 10.0) && Near(FromPx(108.0, L"vh", &context), 10.0),
        "CSS Units resolves viewport units");
    suite.Expect(Near(ToPx(2.0, L"em", &context), 32.0) && Near(ToPx(2.0, L"rem", &context), 32.0),
        "CSS Units expands em and rem to px");
    suite.Expect(Near(ToPx(25.0, L"%", &context), 250.0) && Near(ToPx(10.0, L"vw", &context), 192.0) &&
        Near(ToPx(10.0, L"vh", &context), 108.0),
        "CSS Units expands percent and viewport inputs to px");

    for (auto const unit : Units)
    {
        auto const px = ToPx(12.5, unit, &context);
        suite.Expect(Near(FromPx(px, unit, &context), 12.5), "CSS Units round-trips a supported unit");
    }

    Context zeroContext{};
    zeroContext.elementFontPx = 0.0;
    suite.Expect(std::isnan(FromPx(16.0, L"em", &zeroContext)), "CSS Units FromPx rejects a zero relative denominator");
    zeroContext.elementFontPx = -16.0;
    suite.Expect(std::isnan(FromPx(16.0, L"em", &zeroContext)), "CSS Units FromPx rejects a negative relative denominator");
    zeroContext.elementFontPx = std::numeric_limits<double>::quiet_NaN();
    suite.Expect(std::isnan(FromPx(16.0, L"em", &zeroContext)), "CSS Units FromPx rejects a NaN relative denominator");
    zeroContext.elementFontPx = std::numeric_limits<double>::infinity();
    suite.Expect(FromPx(16.0, L"em", &zeroContext) == 0.0,
        "CSS Units FromPx preserves the managed positive-infinity context quirk");
    zeroContext.elementFontPx = 0.0;
    suite.Expect(ToPx(2.0, L"em", &zeroContext) == 0.0, "CSS Units ToPx multiplies through a zero context");
    zeroContext.elementFontPx = -16.0;
    suite.Expect(ToPx(2.0, L"em", &zeroContext) == -32.0, "CSS Units ToPx multiplies through a negative context");
    zeroContext.elementFontPx = std::numeric_limits<double>::quiet_NaN();
    suite.Expect(std::isnan(ToPx(2.0, L"em", &zeroContext)), "CSS Units ToPx propagates a NaN context");
    suite.Expect(std::isnan(ToPx(std::numeric_limits<double>::infinity(), L"px", &context)) &&
        std::isnan(FromPx(std::numeric_limits<double>::infinity(), L"px", &context)),
        "CSS Units rejects non-finite source values before conversion");
    suite.Expect(Near(ToPx(1.0, L"in", nullptr), 96.0) && Near(FromPx(96.0, L"in", nullptr), 1.0),
        "CSS Units permits absolute conversion with a null context");
    suite.Expect(std::isnan(ToPx(1.0, L"em", nullptr)) && std::isnan(FromPx(16.0, L"em", nullptr)),
        "CSS Units returns NaN for relative conversion with a null context");
    suite.Expect(std::isnan(ToPx(1.0, L"PX", &context)) && std::isnan(FromPx(1.0, L"PX", &context)),
        "CSS Units keeps unit matching ordinal and case-sensitive");

    auto rows = ConvertAll(16.0, L"px", &context);
    suite.Expect(rows.size() == 10 && rows.front().unit == L"em" && rows.back().unit == L"in",
        "CSS Units ConvertAll excludes the source and preserves order");
    auto sourcePresent = false;
    for (auto const& row : rows) sourcePresent = sourcePresent || row.unit == L"px";
    suite.Expect(!sourcePresent, "CSS Units ConvertAll never returns the exact source unit");
    suite.Expect(rows[0].value == L"1" && rows[0].combined == L"1em",
        "CSS Units ConvertAll formats and combines a converted row");
    rows = ConvertAll(16.0, L"px", nullptr);
    suite.Expect(rows.size() == 10 && rows[0].combined == L"1em",
        "CSS Units ConvertAll substitutes the default context for null");
    rows = ConvertAll(1.0, L"PX", &context);
    auto allUnavailable = rows.size() == 11;
    for (auto const& row : rows) allUnavailable = allUnavailable && row.value == L"\u2014" && row.combined.empty();
    suite.Expect(allUnavailable, "CSS Units unknown source returns eleven unavailable rows");
    suite.Expect(ConvertAll(1.0, L"", &context).empty(), "CSS Units empty source returns no rows");
    rows = ConvertAll(std::numeric_limits<double>::quiet_NaN(), L"px", &context);
    auto tenUnavailable = rows.size() == 10;
    for (auto const& row : rows) tenUnavailable = tenUnavailable && row.value == L"\u2014" && row.combined.empty();
    suite.Expect(tenUnavailable, "CSS Units ConvertAll retains unavailable rows for invalid numeric input");

    Context tinyContext{};
    tinyContext.elementFontPx = std::numeric_limits<double>::denorm_min();
    rows = ConvertAll(1.0, L"px", &tinyContext);
    suite.Expect(rows[0].unit == L"em" && rows[0].value == L"\u2014" && rows[0].combined == L"\u2014em",
        "CSS Units preserves the managed infinity formatting and non-NaN Combined quirk");

    suite.Expect(Format(1.23445) == L"1.2345" && Format(-1.23445) == L"-1.2345",
        "CSS Units rounds four decimal midpoints away from zero");
    suite.Expect(Format(1.2300) == L"1.23" && Format(10.0) == L"10",
        "CSS Units removes optional trailing decimal zeros");
    suite.Expect(Format(-0.0) == L"-0" && Format(-0.00004) == L"-0",
        "CSS Units preserves managed negative-zero formatting");
    suite.Expect(Format(0.00005) == L"0.0001" && Format(-0.00005) == L"-0.0001",
        "CSS Units rounds signed half-units away from zero at four decimals");
    suite.Expect(Format(9.99996) == L"10", "CSS Units carries four-decimal rounding into the integer part");
    suite.Expect(Format(std::numeric_limits<double>::quiet_NaN()) == L"\u2014" &&
        Format(std::numeric_limits<double>::infinity()) == L"\u2014",
        "CSS Units formats non-finite results as an em dash");

    suite.Expect(Near(Parse(L"  +1.2e-3  "), 0.0012), "CSS Units parses invariant signed exponent input");
    suite.Expect(Near(Parse(L".5"), 0.5) && Near(Parse(L"5."), 5.0),
        "CSS Units accepts invariant leading or trailing decimal points");
    suite.Expect(std::isnan(Parse(L"")) && std::isnan(Parse(L" \t\r\n")), "CSS Units rejects blank parse input");
    suite.Expect(std::isnan(Parse(L"1,000")) && std::isnan(Parse(L"1_000")),
        "CSS Units NumberStyles.Float parsing rejects group separators and underscores");
    suite.Expect(std::isnan(Parse(L"1e")) && std::isnan(Parse(L".")) && std::isnan(Parse(L"+")),
        "CSS Units rejects incomplete invariant numeric forms");
    suite.Expect(std::isnan(Parse(L"NaN")) && std::isnan(Parse(L"-nan")),
        "CSS Units accepts case-insensitive signed NaN symbols");
    suite.Expect(std::isinf(Parse(L"Infinity")) && Parse(L"Infinity") > 0.0 &&
        std::isinf(Parse(L"-INFINITY")) && Parse(L"-INFINITY") < 0.0,
        "CSS Units accepts case-insensitive signed Infinity symbols");
    suite.Expect(std::isinf(Parse(L"1e309")) && Parse(L"1e309") > 0.0 &&
        std::isinf(Parse(L"-1e309")) && Parse(L"-1e309") < 0.0,
        "CSS Units saturates invariant parse overflow to signed infinity");
    suite.Expect(Parse(L"1e-400") == 0.0 && !std::signbit(Parse(L"1e-400")) &&
        Parse(L"-1e-400") == 0.0 && std::signbit(Parse(L"-1e-400")),
        "CSS Units saturates invariant parse underflow to signed zero");
    suite.Expect(Parse(L"-0") == 0.0 && std::signbit(Parse(L"-0")),
        "CSS Units parsing preserves explicit negative zero");
    suite.Expect(Near(Parse(L"\u00A0\u200312.5\u3000"), 12.5),
        "CSS Units parsing trims the managed Unicode whitespace set");
    suite.Expect(std::isnan(Parse(L"12 5")) && std::isnan(Parse(L"12px")),
        "CSS Units rejects internal whitespace and unit suffixes");

    std::cout << "\nDesign Tools tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
