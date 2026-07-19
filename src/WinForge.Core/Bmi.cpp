#include "Bmi.h"

#include <algorithm>
#include <cmath>
#include <limits>

namespace
{
    [[nodiscard]] double RoundToEven(double value) noexcept
    {
        auto const lower = std::floor(value);
        auto const fraction = value - lower;
        if (fraction < 0.5) return lower;
        if (fraction > 0.5) return lower + 1.0;

        // floor() makes this work for negative midpoint values too:
        // -1.5 -> -2 (even), -2.5 -> -2 (even), matching Math.Round.
        auto const lowerAsInteger = static_cast<long long>(lower);
        return lowerAsInteger % 2 == 0 ? lower : lower + 1.0;
    }
}

namespace winforge::core::bmi
{
    double InchesToCm(double inches) noexcept
    {
        return inches * CmPerInch;
    }

    double PoundsToKg(double pounds) noexcept
    {
        return pounds * KgPerPound;
    }

    double LengthToCm(double value, bool metric) noexcept
    {
        return metric ? value : InchesToCm(value);
    }

    double MassToKg(double value, bool metric) noexcept
    {
        return metric ? value : PoundsToKg(value);
    }

    bool IsPositiveFinite(double value) noexcept
    {
        return std::isfinite(value) && value > 0.0;
    }

    std::optional<double> CalculateBmi(double heightCm, double weightKg) noexcept
    {
        if (!IsPositiveFinite(heightCm) || !IsPositiveFinite(weightKg)) return std::nullopt;
        auto const metres = heightCm / 100.0;
        auto const result = weightKg / (metres * metres);
        return IsPositiveFinite(result) ? std::optional<double>{ result } : std::nullopt;
    }

    Category ClassifyBmi(double value) noexcept
    {
        if (value < 18.5) return Category::Underweight;
        if (value < 25.0) return Category::NormalWeight;
        if (value < 30.0) return Category::Overweight;
        if (value < 35.0) return Category::ObeseClassI;
        if (value < 40.0) return Category::ObeseClassII;
        return Category::ObeseClassIII;
    }

    std::optional<int> RoundAge(double value) noexcept
    {
        if (!std::isfinite(value)) return std::nullopt;

        // Keep the cast below defined even when UI Automation supplies an
        // extreme numeric literal rather than a normal NumberBox value.
        constexpr auto Min = static_cast<double>(std::numeric_limits<int>::min()) - 0.5;
        constexpr auto Max = static_cast<double>(std::numeric_limits<int>::max()) + 0.5;
        if (value < Min || value > Max) return std::nullopt;

        auto const rounded = RoundToEven(value);
        if (rounded < static_cast<double>(std::numeric_limits<int>::min()) ||
            rounded > static_cast<double>(std::numeric_limits<int>::max()))
        {
            return std::nullopt;
        }
        return static_cast<int>(rounded);
    }

    std::optional<double> CalculateBmr(
        Sex sex,
        int age,
        double heightCm,
        double weightKg) noexcept
    {
        if (age <= 0 || age > 130 || !IsPositiveFinite(heightCm) || !IsPositiveFinite(weightKg))
        {
            return std::nullopt;
        }

        auto const result = 10.0 * weightKg + 6.25 * heightCm - 5.0 * static_cast<double>(age) +
            (sex == Sex::Male ? 5.0 : -161.0);
        return IsPositiveFinite(result) ? std::optional<double>{ result } : std::nullopt;
    }

    std::optional<double> CalculateTdee(
        std::optional<double> basalMetabolicRate,
        double activityFactor) noexcept
    {
        if (!basalMetabolicRate || !IsPositiveFinite(*basalMetabolicRate) || !IsPositiveFinite(activityFactor))
        {
            return std::nullopt;
        }
        auto const result = *basalMetabolicRate * activityFactor;
        return IsPositiveFinite(result) ? std::optional<double>{ result } : std::nullopt;
    }

    std::optional<double> CalculateBodyFatNavy(
        Sex sex,
        double heightCm,
        double neckCm,
        double waistCm,
        double hipsCm) noexcept
    {
        if (!IsPositiveFinite(heightCm) || !IsPositiveFinite(neckCm) || !IsPositiveFinite(waistCm))
        {
            return std::nullopt;
        }

        double denominator{};
        if (sex == Sex::Male)
        {
            auto const circumference = waistCm - neckCm;
            if (!(circumference > 0.0)) return std::nullopt;
            denominator = 1.0324 - 0.19077 * std::log10(circumference) +
                0.15456 * std::log10(heightCm);
        }
        else
        {
            if (!IsPositiveFinite(hipsCm)) return std::nullopt;
            auto const circumference = waistCm + hipsCm - neckCm;
            if (!(circumference > 0.0)) return std::nullopt;
            denominator = 1.29579 - 0.35004 * std::log10(circumference) +
                0.22100 * std::log10(heightCm);
        }

        auto const result = 495.0 / denominator - 450.0;
        if (!std::isfinite(result)) return std::nullopt;
        return std::clamp(result, 2.0, 70.0);
    }
}
