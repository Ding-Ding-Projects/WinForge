#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::aspectratio
{
    // Mirrors AspectRatioService.Gcd. As in Math.Abs(long), passing the minimum
    // signed 64-bit value is an overflow error because its magnitude is not a long.
    [[nodiscard]] std::int64_t Gcd(std::int64_t a, std::int64_t b);

    // Rounds dimensions with .NET Math.Round's default midpoint-to-even policy,
    // then simplifies the resulting positive whole-number dimensions.
    [[nodiscard]] bool Simplify(
        double width,
        double height,
        std::int64_t& ratioWidth,
        std::int64_t& ratioHeight);

    [[nodiscard]] double DecimalRatio(double width, double height) noexcept;
    [[nodiscard]] double Megapixels(double width, double height) noexcept;
    [[nodiscard]] double HeightForWidth(double ratioWidth, double ratioHeight, double width) noexcept;
    [[nodiscard]] double WidthForHeight(double ratioWidth, double ratioHeight, double height) noexcept;
}

namespace winforge::core::cssunits
{
    inline constexpr double PxPerIn = 96.0;
    inline constexpr double PxPerPt = PxPerIn / 72.0;
    inline constexpr double PxPerPc = PxPerPt * 12.0;
    inline constexpr double PxPerCm = PxPerIn / 2.54;
    inline constexpr double PxPerMm = PxPerCm / 10.0;

    inline constexpr std::array<std::wstring_view, 11> Units{
        L"px", L"em", L"rem", L"pt", L"pc", L"%", L"vw", L"vh", L"cm", L"mm", L"in"
    };

    struct Context
    {
        double rootFontPx{ 16.0 };
        double elementFontPx{ 16.0 };
        double viewportWidthPx{ 1920.0 };
        double viewportHeightPx{ 1080.0 };
        double containerPx{ 1000.0 };
    };

    struct Result
    {
        std::wstring unit;
        std::wstring value;
        std::wstring combined;
    };

    // A null context mirrors a managed null reference: absolute conversions still
    // work, while a relative conversion returns NaN. ConvertAll alone substitutes
    // the managed default Context for null.
    [[nodiscard]] double FromPx(double px, std::wstring_view toUnit, Context const* context) noexcept;
    [[nodiscard]] double ToPx(double value, std::wstring_view fromUnit, Context const* context) noexcept;

    // Returns every unit in Units order except an exact, case-sensitive source
    // match. Empty source input returns an empty list; unknown input returns all
    // eleven unavailable rows, matching the managed oracle.
    [[nodiscard]] std::vector<Result> ConvertAll(
        double value,
        std::wstring_view fromUnit,
        Context const* context = nullptr);

    // Invariant NumberStyles.Float parsing. Blank and malformed input return NaN;
    // signed NaN/Infinity and exponent overflow/underflow follow modern .NET.
    [[nodiscard]] double Parse(std::wstring_view text) noexcept;

    // Math.Round(value, 4, AwayFromZero) followed by invariant "0.####".
    [[nodiscard]] std::wstring Format(double value);
}
