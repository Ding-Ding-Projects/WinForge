#include "DesignTools.h"

#include <algorithm>
#include <charconv>
#include <cmath>
#include <iomanip>
#include <limits>
#include <locale>
#include <sstream>
#include <stdexcept>
#include <string>
#include <system_error>

namespace
{
    [[nodiscard]] bool IsPositiveFinite(double value) noexcept
    {
        return std::isfinite(value) && value > 0.0;
    }

    [[nodiscard]] bool TryRoundToEvenInt64(double value, std::int64_t& result) noexcept
    {
        // Simplify calls this only for positive finite values. Keeping the guard
        // here makes the helper self-contained and avoids undefined float-to-int
        // conversion at and above +2^63.
        if (!IsPositiveFinite(value)) return false;

        auto const lower = std::floor(value);
        auto const fraction = value - lower;
        double rounded{};
        if (fraction < 0.5)
        {
            rounded = lower;
        }
        else if (fraction > 0.5)
        {
            rounded = lower + 1.0;
        }
        else
        {
            // Every binary64 value at or above 2^53 is already integral, so the
            // parity check is meaningful for every possible midpoint here.
            rounded = std::fmod(lower, 2.0) == 0.0 ? lower : lower + 1.0;
        }

        constexpr auto Int64Limit = 9'223'372'036'854'775'808.0; // +2^63, exclusive
        if (rounded <= 0.0 || rounded >= Int64Limit) return false;
        result = static_cast<std::int64_t>(rounded);
        return result > 0;
    }

    [[nodiscard]] double NaN() noexcept
    {
        return std::numeric_limits<double>::quiet_NaN();
    }

    [[nodiscard]] double SafeDivide(double px, double denominator) noexcept
    {
        // This intentionally admits +Infinity, exactly like `denom > 0 &&
        // !double.IsNaN(denom)` in the managed implementation.
        return denominator > 0.0 && !std::isnan(denominator) ? px / denominator : NaN();
    }

    [[nodiscard]] bool IsDotNetWhitespace(wchar_t value) noexcept
    {
        return (value >= L'\u0009' && value <= L'\u000D') ||
            value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] std::wstring_view Trim(std::wstring_view value) noexcept
    {
        std::size_t first{};
        auto last = value.size();
        while (first < last && IsDotNetWhitespace(value[first])) ++first;
        while (last > first && IsDotNetWhitespace(value[last - 1])) --last;
        return value.substr(first, last - first);
    }

    [[nodiscard]] char LowerAscii(char value) noexcept
    {
        return value >= 'A' && value <= 'Z' ? static_cast<char>(value - 'A' + 'a') : value;
    }

    [[nodiscard]] bool EqualsAsciiInsensitive(std::string_view value, std::string_view expected) noexcept
    {
        if (value.size() != expected.size()) return false;
        for (std::size_t index{}; index < value.size(); ++index)
        {
            if (LowerAscii(value[index]) != LowerAscii(expected[index])) return false;
        }
        return true;
    }

    struct DecimalSyntax
    {
        bool valid{ false };
        bool allZero{ true };
        int firstSignificantExponent{};
    };

    [[nodiscard]] DecimalSyntax ValidateDecimal(std::string_view text) noexcept
    {
        DecimalSyntax syntax;
        std::size_t position{};
        if (position < text.size() && (text[position] == '+' || text[position] == '-')) ++position;
        if (position == text.size()) return syntax;

        std::size_t digitsBefore{};
        std::size_t totalDigits{};
        std::size_t firstNonZero = std::string_view::npos;
        while (position < text.size() && text[position] >= '0' && text[position] <= '9')
        {
            if (text[position] != '0' && firstNonZero == std::string_view::npos) firstNonZero = totalDigits;
            ++digitsBefore;
            ++totalDigits;
            ++position;
        }
        if (position < text.size() && text[position] == '.')
        {
            ++position;
            while (position < text.size() && text[position] >= '0' && text[position] <= '9')
            {
                if (text[position] != '0' && firstNonZero == std::string_view::npos) firstNonZero = totalDigits;
                ++totalDigits;
                ++position;
            }
        }
        if (totalDigits == 0) return syntax;

        int explicitExponent{};
        if (position < text.size() && (text[position] == 'e' || text[position] == 'E'))
        {
            ++position;
            auto exponentNegative = false;
            if (position < text.size() && (text[position] == '+' || text[position] == '-'))
            {
                exponentNegative = text[position] == '-';
                ++position;
            }
            if (position == text.size() || text[position] < '0' || text[position] > '9') return syntax;
            constexpr int ExponentCap = 100'000;
            while (position < text.size() && text[position] >= '0' && text[position] <= '9')
            {
                auto const digit = text[position] - '0';
                if (explicitExponent < ExponentCap)
                {
                    explicitExponent = explicitExponent > (ExponentCap - digit) / 10
                        ? ExponentCap
                        : explicitExponent * 10 + digit;
                }
                ++position;
            }
            if (exponentNegative) explicitExponent = -explicitExponent;
        }
        if (position != text.size()) return syntax;

        syntax.valid = true;
        syntax.allZero = firstNonZero == std::string_view::npos;
        if (!syntax.allZero)
        {
            auto exponent = static_cast<long long>(explicitExponent) +
                static_cast<long long>(digitsBefore) -
                static_cast<long long>(firstNonZero) - 1;
            if (exponent > std::numeric_limits<int>::max()) exponent = std::numeric_limits<int>::max();
            if (exponent < std::numeric_limits<int>::min()) exponent = std::numeric_limits<int>::min();
            syntax.firstSignificantExponent = static_cast<int>(exponent);
        }
        return syntax;
    }

    [[nodiscard]] double RoundFourAwayFromZero(double value) noexcept
    {
        // CoreLib skips decimal scaling once binary64 no longer carries four
        // fractional decimal places. This also prevents an avoidable overflow.
        if (std::abs(value) >= 1.0e16) return value;
        return std::round(value * 10'000.0) / 10'000.0;
    }
}

namespace winforge::core::aspectratio
{
    std::int64_t Gcd(std::int64_t a, std::int64_t b)
    {
        if (a == std::numeric_limits<std::int64_t>::min() ||
            b == std::numeric_limits<std::int64_t>::min())
        {
            throw std::overflow_error("Math.Abs(long.MinValue) overflow");
        }

        if (a < 0) a = -a;
        if (b < 0) b = -b;
        while (b != 0)
        {
            auto const next = a % b;
            a = b;
            b = next;
        }
        return a == 0 ? 1 : a;
    }

    bool Simplify(
        double width,
        double height,
        std::int64_t& ratioWidth,
        std::int64_t& ratioHeight)
    {
        ratioWidth = 0;
        ratioHeight = 0;
        if (!IsPositiveFinite(width) || !IsPositiveFinite(height)) return false;

        std::int64_t roundedWidth{};
        std::int64_t roundedHeight{};
        if (!TryRoundToEvenInt64(width, roundedWidth) ||
            !TryRoundToEvenInt64(height, roundedHeight)) return false;

        auto const divisor = Gcd(roundedWidth, roundedHeight);
        ratioWidth = roundedWidth / divisor;
        ratioHeight = roundedHeight / divisor;
        return true;
    }

    double DecimalRatio(double width, double height) noexcept
    {
        return !IsPositiveFinite(width) || !IsPositiveFinite(height) ? NaN() : width / height;
    }

    double Megapixels(double width, double height) noexcept
    {
        return !IsPositiveFinite(width) || !IsPositiveFinite(height)
            ? NaN()
            : width * height / 1'000'000.0;
    }

    double HeightForWidth(double ratioWidth, double ratioHeight, double width) noexcept
    {
        return !IsPositiveFinite(ratioWidth) || !IsPositiveFinite(ratioHeight) || !IsPositiveFinite(width)
            ? NaN()
            : width * ratioHeight / ratioWidth;
    }

    double WidthForHeight(double ratioWidth, double ratioHeight, double height) noexcept
    {
        return !IsPositiveFinite(ratioWidth) || !IsPositiveFinite(ratioHeight) || !IsPositiveFinite(height)
            ? NaN()
            : height * ratioWidth / ratioHeight;
    }

    std::wstring FormatDisplayNumber(
        double value,
        int fractionalDigits,
        std::wstring_view decimalSeparator)
    {
        if (!std::isfinite(value)) throw std::invalid_argument("Aspect display value must be finite");
        fractionalDigits = std::clamp(fractionalDigits, 0, 8);

        std::array<char, 64> buffer{};
        auto const magnitude = std::abs(value);
        auto const converted = std::to_chars(
            buffer.data(),
            buffer.data() + buffer.size(),
            magnitude,
            std::chars_format::scientific,
            14);
        if (converted.ec != std::errc{}) throw std::runtime_error("Aspect display formatting failed");

        std::string significant;
        significant.reserve(15);
        auto exponentMarker = converted.ptr;
        for (auto cursor = buffer.data(); cursor != converted.ptr; ++cursor)
        {
            if (*cursor == 'e' || *cursor == 'E')
            {
                exponentMarker = cursor;
                break;
            }
            if (*cursor >= '0' && *cursor <= '9') significant.push_back(*cursor);
        }
        if (significant.empty() || exponentMarker == converted.ptr)
        {
            throw std::runtime_error("Aspect display formatter produced an invalid decimal buffer");
        }

        auto exponentCursor = exponentMarker + 1;
        auto exponentNegative = false;
        if (exponentCursor != converted.ptr && (*exponentCursor == '+' || *exponentCursor == '-'))
        {
            exponentNegative = *exponentCursor == '-';
            ++exponentCursor;
        }
        int exponentMagnitude{};
        auto const parsedExponent = std::from_chars(exponentCursor, converted.ptr, exponentMagnitude);
        if (parsedExponent.ec != std::errc{} || parsedExponent.ptr != converted.ptr)
        {
            throw std::runtime_error("Aspect display formatter produced an invalid exponent");
        }
        auto const exponent = exponentNegative ? -exponentMagnitude : exponentMagnitude;

        // The integer below is abs(value) * 10^fractionalDigits after decimal
        // rounding. `keep` is how many significant digits lie at or left of
        // the custom format's rounding boundary.
        auto const keep = exponent + 1 + fractionalDigits;
        std::string scaled;
        if (keep > 0)
        {
            auto const retained = std::min<std::size_t>(
                static_cast<std::size_t>(keep), significant.size());
            scaled.assign(significant.data(), retained);
            if (static_cast<std::size_t>(keep) > significant.size())
            {
                scaled.append(static_cast<std::size_t>(keep) - significant.size(), '0');
            }
        }
        else
        {
            scaled = "0";
        }

        auto const roundUp = keep >= 0 &&
            static_cast<std::size_t>(keep) < significant.size() &&
            significant[static_cast<std::size_t>(keep)] >= '5';
        if (roundUp)
        {
            auto cursor = scaled.size();
            while (cursor > 0 && scaled[cursor - 1] == '9')
            {
                scaled[cursor - 1] = '0';
                --cursor;
            }
            if (cursor == 0) scaled.insert(scaled.begin(), '1');
            else ++scaled[cursor - 1];
        }

        auto const firstNonZero = scaled.find_first_not_of('0');
        if (firstNonZero == std::string::npos) scaled = "0";
        else if (firstNonZero > 0) scaled.erase(0, firstNonZero);

        if (fractionalDigits > 0 && scaled.size() <= static_cast<std::size_t>(fractionalDigits))
        {
            scaled.insert(0, static_cast<std::size_t>(fractionalDigits) + 1 - scaled.size(), '0');
        }

        std::wstring output;
        if (std::signbit(value)) output.push_back(L'-');
        if (fractionalDigits == 0)
        {
            output.append(scaled.begin(), scaled.end());
            return output;
        }

        auto const decimalIndex = scaled.size() - static_cast<std::size_t>(fractionalDigits);
        output.append(scaled.begin(), scaled.begin() + static_cast<std::ptrdiff_t>(decimalIndex));
        output.append(decimalSeparator);
        output.append(scaled.begin() + static_cast<std::ptrdiff_t>(decimalIndex), scaled.end());

        while (!output.empty() && output.back() == L'0') output.pop_back();
        if (!decimalSeparator.empty() && output.size() >= decimalSeparator.size() &&
            output.compare(output.size() - decimalSeparator.size(), decimalSeparator.size(), decimalSeparator) == 0)
        {
            output.erase(output.size() - decimalSeparator.size());
        }
        return output;
    }
}

namespace winforge::core::cssunits
{
    double FromPx(double px, std::wstring_view toUnit, Context const* context) noexcept
    {
        if (!std::isfinite(px)) return NaN();
        if (toUnit == L"px") return px;
        if (toUnit == L"in") return px / PxPerIn;
        if (toUnit == L"pt") return px / PxPerPt;
        if (toUnit == L"pc") return px / PxPerPc;
        if (toUnit == L"cm") return px / PxPerCm;
        if (toUnit == L"mm") return px / PxPerMm;
        if (context == nullptr) return NaN();
        if (toUnit == L"em") return SafeDivide(px, context->elementFontPx);
        if (toUnit == L"rem") return SafeDivide(px, context->rootFontPx);
        if (toUnit == L"%") return SafeDivide(px, context->containerPx) * 100.0;
        if (toUnit == L"vw") return SafeDivide(px, context->viewportWidthPx) * 100.0;
        if (toUnit == L"vh") return SafeDivide(px, context->viewportHeightPx) * 100.0;
        return NaN();
    }

    double ToPx(double value, std::wstring_view fromUnit, Context const* context) noexcept
    {
        if (!std::isfinite(value)) return NaN();
        if (fromUnit == L"px") return value;
        if (fromUnit == L"in") return value * PxPerIn;
        if (fromUnit == L"pt") return value * PxPerPt;
        if (fromUnit == L"pc") return value * PxPerPc;
        if (fromUnit == L"cm") return value * PxPerCm;
        if (fromUnit == L"mm") return value * PxPerMm;
        if (context == nullptr) return NaN();
        if (fromUnit == L"em") return value * context->elementFontPx;
        if (fromUnit == L"rem") return value * context->rootFontPx;
        if (fromUnit == L"%") return value / 100.0 * context->containerPx;
        if (fromUnit == L"vw") return value / 100.0 * context->viewportWidthPx;
        if (fromUnit == L"vh") return value / 100.0 * context->viewportHeightPx;
        return NaN();
    }

    std::vector<Result> ConvertAll(
        double value,
        std::wstring_view fromUnit,
        Context const* context)
    {
        std::vector<Result> results;
        if (fromUnit.empty()) return results;

        Context defaultContext;
        auto const* resolvedContext = context == nullptr ? &defaultContext : context;
        auto const px = ToPx(value, fromUnit, resolvedContext);
        results.reserve(Units.size());
        for (auto const unit : Units)
        {
            if (unit == fromUnit) continue;
            auto const converted = FromPx(px, unit, resolvedContext);
            auto const shown = Format(converted);
            auto const unavailable = std::isnan(converted);
            results.push_back(Result{
                std::wstring(unit),
                unavailable ? L"\u2014" : shown,
                unavailable ? std::wstring{} : shown + std::wstring(unit),
            });
        }
        return results;
    }

    double Parse(std::wstring_view text) noexcept
    {
        text = Trim(text);
        if (text.empty()) return NaN();

        std::string ascii;
        ascii.reserve(text.size());
        for (auto const character : text)
        {
            if (character > 0x7F) return NaN();
            ascii.push_back(static_cast<char>(character));
        }

        auto body = std::string_view(ascii);
        auto const negative = !body.empty() && body.front() == '-';
        auto const signedSpecial = !body.empty() && (body.front() == '+' || body.front() == '-')
            ? body.substr(1)
            : body;
        if (EqualsAsciiInsensitive(signedSpecial, "nan"))
        {
            return std::copysign(std::numeric_limits<double>::quiet_NaN(), negative ? -1.0 : 1.0);
        }
        if (EqualsAsciiInsensitive(signedSpecial, "infinity"))
        {
            return std::copysign(std::numeric_limits<double>::infinity(), negative ? -1.0 : 1.0);
        }

        auto const syntax = ValidateDecimal(body);
        if (!syntax.valid) return NaN();
        if (syntax.allZero) return std::copysign(0.0, negative ? -1.0 : 1.0);

        // std::from_chars is locale-independent. Unlike NumberStyles.Float it
        // does not accept a leading '+', so remove that one validated character.
        auto parseText = body;
        if (!parseText.empty() && parseText.front() == '+') parseText.remove_prefix(1);
        double parsed{};
        auto const [end, error] = std::from_chars(
            parseText.data(),
            parseText.data() + parseText.size(),
            parsed,
            std::chars_format::general);
        if (error == std::errc{} && end == parseText.data() + parseText.size()) return parsed;
        if (error == std::errc::result_out_of_range)
        {
            // Modern .NET saturates Float parsing to signed infinity on overflow
            // and signed zero on underflow.
            return syntax.firstSignificantExponent < 0
                ? std::copysign(0.0, negative ? -1.0 : 1.0)
                : std::copysign(std::numeric_limits<double>::infinity(), negative ? -1.0 : 1.0);
        }
        return NaN();
    }

    std::wstring Format(double value)
    {
        if (!std::isfinite(value)) return L"\u2014";
        auto const rounded = RoundFourAwayFromZero(value);

        std::wostringstream stream;
        stream.imbue(std::locale::classic());
        stream << std::fixed << std::setprecision(4) << rounded;
        auto text = stream.str();
        auto const decimal = text.find(L'.');
        if (decimal != std::wstring::npos)
        {
            while (!text.empty() && text.back() == L'0') text.pop_back();
            if (!text.empty() && text.back() == L'.') text.pop_back();
        }
        return text;
    }
}
