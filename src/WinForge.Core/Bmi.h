#pragma once

#include <array>
#include <optional>

namespace winforge::core::bmi
{
    enum class Sex
    {
        Male,
        Female,
    };

    enum class Category
    {
        Underweight,
        NormalWeight,
        Overweight,
        ObeseClassI,
        ObeseClassII,
        ObeseClassIII,
    };

    struct ActivityLevel
    {
        double factor{};
    };

    inline constexpr std::array<ActivityLevel, 5> ActivityLevels{
        ActivityLevel{ 1.200 },
        ActivityLevel{ 1.375 },
        ActivityLevel{ 1.550 },
        ActivityLevel{ 1.725 },
        ActivityLevel{ 1.900 },
    };

    inline constexpr double CmPerInch = 2.54;
    inline constexpr double KgPerPound = 0.45359237;

    // Unit conversions intentionally do not validate their input: the managed
    // page converts the editor value first and lets the specific calculator
    // decide whether it is usable.
    [[nodiscard]] double InchesToCm(double inches) noexcept;
    [[nodiscard]] double PoundsToKg(double pounds) noexcept;
    [[nodiscard]] double LengthToCm(double value, bool metric) noexcept;
    [[nodiscard]] double MassToKg(double value, bool metric) noexcept;

    // Mirrors the managed calculator's finite, positive input guard.
    [[nodiscard]] bool IsPositiveFinite(double value) noexcept;

    // Body-mass index: kg / m². Invalid or non-finite inputs have no result.
    [[nodiscard]] std::optional<double> CalculateBmi(double heightCm, double weightKg) noexcept;
    [[nodiscard]] Category ClassifyBmi(double value) noexcept;

    // WinUI NumberBox returns a floating value while the managed page rounds
    // age with Math.Round's midpoint-to-even rule before passing it to BMR.
    // A non-finite or out-of-range editor value is rejected without throwing.
    [[nodiscard]] std::optional<int> RoundAge(double value) noexcept;

    // Mifflin–St Jeor basal metabolic rate and activity-adjusted TDEE.
    [[nodiscard]] std::optional<double> CalculateBmr(
        Sex sex,
        int age,
        double heightCm,
        double weightKg) noexcept;
    [[nodiscard]] std::optional<double> CalculateTdee(
        std::optional<double> basalMetabolicRate,
        double activityFactor) noexcept;

    // US Navy circumference estimate. Measurements are centimetres. The
    // result follows the managed 2–70 physiological display clamp; a female
    // result requires hips, while a male result ignores it.
    [[nodiscard]] std::optional<double> CalculateBodyFatNavy(
        Sex sex,
        double heightCm,
        double neckCm,
        double waistCm,
        double hipsCm) noexcept;
}
