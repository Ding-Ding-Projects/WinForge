#include "PercentCalc.h"

#include "DesignTools.h"

#include <algorithm>
#include <cerrno>
#include <cmath>
#include <cstdint>
#include <cwchar>
#include <cwctype>
#include <limits>
#include <string>

namespace
{
    [[nodiscard]] bool IsTrimWhitespace(wchar_t value) noexcept
    {
        // String.Trim() accepts the usual Unicode whitespace set. Keep the
        // parser independent of the process C locale so core tests remain
        // deterministic and the shell can supply only the decimal separator.
        return std::iswspace(value) != 0 || value == L'\u00A0' || value == L'\u1680' ||
            (value >= L'\u2000' && value <= L'\u200A') || value == L'\u2028' ||
            value == L'\u2029' || value == L'\u202F' || value == L'\u205F' ||
            value == L'\u3000';
    }

    [[nodiscard]] std::wstring_view Trim(std::wstring_view value) noexcept
    {
        while (!value.empty() && IsTrimWhitespace(value.front())) value.remove_prefix(1);
        while (!value.empty() && IsTrimWhitespace(value.back())) value.remove_suffix(1);
        return value;
    }

    [[nodiscard]] bool IsStrictInvariantNumber(std::wstring_view value) noexcept
    {
        if (value.empty()) return false;
        std::size_t index{};
        if (value[index] == L'+' || value[index] == L'-') ++index;
        auto digits = false;
        while (index < value.size() && value[index] >= L'0' && value[index] <= L'9')
        {
            digits = true;
            ++index;
        }
        if (index < value.size() && value[index] == L'.')
        {
            ++index;
            while (index < value.size() && value[index] >= L'0' && value[index] <= L'9')
            {
                digits = true;
                ++index;
            }
        }
        if (!digits) return false;
        if (index < value.size() && (value[index] == L'e' || value[index] == L'E'))
        {
            ++index;
            if (index < value.size() && (value[index] == L'+' || value[index] == L'-')) ++index;
            auto exponentDigits = false;
            while (index < value.size() && value[index] >= L'0' && value[index] <= L'9')
            {
                exponentDigits = true;
                ++index;
            }
            if (!exponentDigits) return false;
        }
        return index == value.size();
    }

    [[nodiscard]] std::wstring NormalizeDecimalSeparator(
        std::wstring_view value,
        std::wstring_view currentDecimalSeparator)
    {
        if (currentDecimalSeparator.empty() || currentDecimalSeparator == L".")
        {
            return std::wstring(value);
        }

        std::wstring normalized;
        normalized.reserve(value.size());
        for (std::size_t index{}; index < value.size();)
        {
            if (index + currentDecimalSeparator.size() <= value.size() &&
                value.substr(index, currentDecimalSeparator.size()) == currentDecimalSeparator)
            {
                normalized.push_back(L'.');
                index += currentDecimalSeparator.size();
            }
            else
            {
                normalized.push_back(value[index]);
                ++index;
            }
        }
        return normalized;
    }

    [[nodiscard]] double RoundAwayFromZeroSix(double value) noexcept
    {
        constexpr auto factor = 1'000'000.0;
        if (!std::isfinite(value)) return value;
        auto const magnitude = std::abs(value);
        if (magnitude > std::numeric_limits<double>::max() / factor) return value;
        auto const scaled = magnitude * factor;
        if (!std::isfinite(scaled)) return value;
        auto const whole = std::floor(scaled);
        auto const fraction = scaled - whole;
        auto const rounded = whole + (fraction >= 0.5 ? 1.0 : 0.0);
        return std::copysign(rounded / factor, value);
    }

    [[nodiscard]] bool RoundToInt64ToEven(double value, std::int64_t& result) noexcept
    {
        result = 0;
        if (!std::isfinite(value)) return false;
        auto const magnitude = std::abs(value);
        // The positive bound is 2^63, which is not representable as int64.
        if (magnitude >= 9'223'372'036'854'775'808.0) return false;
        auto whole = std::floor(magnitude);
        auto const fraction = magnitude - whole;
        if (fraction > 0.5 || (fraction == 0.5 && std::fmod(whole, 2.0) != 0.0))
        {
            ++whole;
        }
        if (whole >= 9'223'372'036'854'775'808.0) return false;
        auto const signedValue = value < 0.0 ? -whole : whole;
        result = static_cast<std::int64_t>(signedValue);
        return true;
    }

    [[nodiscard]] int FractionalScale(double value) noexcept
    {
        auto decimals = 0;
        auto scaled = value;
        auto fraction = std::abs(scaled - std::trunc(scaled));
        while (decimals < 6 && fraction > 1.0e-9)
        {
            scaled *= 10.0;
            ++decimals;
            fraction = std::abs(scaled - std::trunc(scaled));
        }
        return decimals;
    }

    [[nodiscard]] double Pow10(int count) noexcept
    {
        auto result = 1.0;
        for (auto index = 0; index < count; ++index) result *= 10.0;
        return result;
    }

    [[nodiscard]] std::uint64_t Magnitude(std::int64_t value) noexcept
    {
        // SimplifyRatio rejects long.MinValue before reaching this helper.
        return value < 0 ? static_cast<std::uint64_t>(-value) : static_cast<std::uint64_t>(value);
    }

    [[nodiscard]] std::uint64_t Gcd(std::uint64_t a, std::uint64_t b) noexcept
    {
        while (b != 0)
        {
            auto const remainder = a % b;
            a = b;
            b = remainder;
        }
        return a == 0 ? 1 : a;
    }

    [[nodiscard]] winforge::core::percentcalc::CalcResult MakeNumberResult(
        double value,
        std::wstring_view decimalSeparator)
    {
        return { true, winforge::core::percentcalc::Format(value, decimalSeparator), value };
    }

    [[nodiscard]] winforge::core::percentcalc::CalcResult MakeTextResult(
        double value,
        std::wstring text)
    {
        return { true, std::move(text), value };
    }
}

namespace winforge::core::percentcalc
{
    bool TryParse(
        std::wstring_view raw,
        double& value,
        std::wstring_view currentDecimalSeparator)
    {
        value = std::numeric_limits<double>::quiet_NaN();
        auto text = Trim(raw);
        if (!text.empty() && text.back() == L'%')
        {
            text.remove_suffix(1);
            text = Trim(text);
        }
        if (text.empty()) return false;

        auto normalized = NormalizeDecimalSeparator(text, currentDecimalSeparator);
        if (!IsStrictInvariantNumber(normalized)) return false;

        errno = 0;
        wchar_t* end{};
        auto const parsed = std::wcstod(normalized.c_str(), &end);
        if (end == normalized.c_str() || *end != L'\0' || !std::isfinite(parsed)) return false;
        value = parsed;
        return true;
    }

    std::wstring Format(double value, std::wstring_view decimalSeparator)
    {
        if (!std::isfinite(value)) return L"—";
        auto rounded = RoundAwayFromZeroSix(value);
        // The C# service explicitly normalizes -0 after rounding.
        if (rounded == 0.0) rounded = 0.0;
        return aspectratio::FormatDisplayNumber(rounded, 6, decimalSeparator);
    }

    CalcResult PercentOf(
        std::wstring_view x,
        std::wstring_view y,
        std::wstring_view decimalSeparator)
    {
        double percent{};
        double value{};
        if (!TryParse(x, percent, decimalSeparator) || !TryParse(y, value, decimalSeparator)) return {};
        return MakeNumberResult(percent / 100.0 * value, decimalSeparator);
    }

    CalcResult WhatPercent(
        std::wstring_view x,
        std::wstring_view y,
        std::wstring_view decimalSeparator)
    {
        double value{};
        double whole{};
        if (!TryParse(x, value, decimalSeparator) || !TryParse(y, whole, decimalSeparator) || whole == 0.0) return {};
        auto const result = value / whole * 100.0;
        return MakeTextResult(result, Format(result, decimalSeparator) + L"%");
    }

    CalcResult PercentChange(
        std::wstring_view a,
        std::wstring_view b,
        std::wstring_view decimalSeparator)
    {
        double start{};
        double end{};
        if (!TryParse(a, start, decimalSeparator) || !TryParse(b, end, decimalSeparator) || start == 0.0) return {};
        auto const result = (end - start) / std::abs(start) * 100.0;
        auto text = result > 0.0 ? std::wstring{ L"+" } : std::wstring{};
        text += Format(result, decimalSeparator);
        text += L"%";
        return MakeTextResult(result, std::move(text));
    }

    CalcResult AdjustBy(
        std::wstring_view y,
        std::wstring_view x,
        bool increase,
        std::wstring_view decimalSeparator)
    {
        double base{};
        double percent{};
        if (!TryParse(y, base, decimalSeparator) || !TryParse(x, percent, decimalSeparator)) return {};
        auto const factor = increase ? 1.0 + percent / 100.0 : 1.0 - percent / 100.0;
        return MakeNumberResult(base * factor, decimalSeparator);
    }

    TipResult Tip(
        std::wstring_view bill,
        std::wstring_view tipPercent,
        std::wstring_view split,
        std::wstring_view currentDecimalSeparator)
    {
        double parsedBill{};
        double parsedPercent{};
        double parsedSplit{};
        if (!TryParse(bill, parsedBill, currentDecimalSeparator) ||
            !TryParse(tipPercent, parsedPercent, currentDecimalSeparator) ||
            !TryParse(split, parsedSplit, currentDecimalSeparator))
        {
            return {};
        }

        std::int64_t roundedSplit{};
        if (!RoundToInt64ToEven(parsedSplit, roundedSplit) || roundedSplit < 1 ||
            roundedSplit > static_cast<std::int64_t>(std::numeric_limits<int>::max()) ||
            parsedBill < 0.0 || parsedPercent < 0.0)
        {
            return {};
        }
        auto const tip = parsedBill * parsedPercent / 100.0;
        auto const total = parsedBill + tip;
        return { true, tip, total, total / static_cast<double>(roundedSplit) };
    }

    RatioResult SimplifyRatio(
        std::wstring_view a,
        std::wstring_view b,
        std::wstring_view currentDecimalSeparator)
    {
        double left{};
        double right{};
        if (!TryParse(a, left, currentDecimalSeparator) || !TryParse(b, right, currentDecimalSeparator)) return {};

        auto const scale = (std::max)(FractionalScale(left), FractionalScale(right));
        std::int64_t scaledLeft{};
        std::int64_t scaledRight{};
        if (!RoundToInt64ToEven(left * Pow10(scale), scaledLeft) ||
            !RoundToInt64ToEven(right * Pow10(scale), scaledRight) ||
            scaledLeft == std::numeric_limits<std::int64_t>::min() ||
            scaledRight == std::numeric_limits<std::int64_t>::min())
        {
            return {};
        }
        if (scaledLeft == 0 && scaledRight == 0) return {};
        auto const divisor = Gcd(Magnitude(scaledLeft), Magnitude(scaledRight));
        return {
            true,
            static_cast<std::int64_t>(scaledLeft / static_cast<std::int64_t>(divisor)),
            static_cast<std::int64_t>(scaledRight / static_cast<std::int64_t>(divisor)) };
    }
}
