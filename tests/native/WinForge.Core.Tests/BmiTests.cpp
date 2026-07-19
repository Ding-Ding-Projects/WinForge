#include "BmiTests.h"

#include "Bmi.h"

#include <cmath>
#include <iostream>
#include <limits>
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

    [[nodiscard]] bool Near(double actual, double expected, double epsilon = 1.0e-9)
    {
        return std::fabs(actual - expected) <= epsilon;
    }
}

NativeTestCounts RunBmiTests()
{
    using namespace winforge::core::bmi;
    Suite suite;

    suite.Expect(
        Near(InchesToCm(70.0), 177.8) &&
        Near(PoundsToKg(150.0), 68.0388555) &&
        Near(LengthToCm(170.0, true), 170.0) &&
        Near(LengthToCm(70.0, false), 177.8) &&
        Near(MassToKg(65.0, true), 65.0) &&
        Near(MassToKg(150.0, false), 68.0388555),
        "BMI conversions preserve the managed cm/kg and inch/lb constants");

    suite.Expect(
        IsPositiveFinite(0.0001) && !IsPositiveFinite(0.0) && !IsPositiveFinite(-1.0) &&
        !IsPositiveFinite(std::numeric_limits<double>::quiet_NaN()) &&
        !IsPositiveFinite(std::numeric_limits<double>::infinity()),
        "BMI input guard accepts only finite positive values");

    auto const defaultBmi = CalculateBmi(170.0, 65.0);
    suite.Expect(defaultBmi && Near(*defaultBmi, 22.49134948096886),
        "BMI uses kg divided by metres squared for the managed defaults");

    suite.Expect(
        !CalculateBmi(0.0, 65.0) && !CalculateBmi(170.0, 0.0) &&
        !CalculateBmi(-170.0, 65.0) &&
        !CalculateBmi(std::numeric_limits<double>::infinity(), 65.0),
        "BMI rejects zero negative and non-finite dimensions without throwing");

    suite.Expect(
        ClassifyBmi(18.49) == Category::Underweight &&
        ClassifyBmi(18.5) == Category::NormalWeight &&
        ClassifyBmi(24.999) == Category::NormalWeight &&
        ClassifyBmi(25.0) == Category::Overweight &&
        ClassifyBmi(30.0) == Category::ObeseClassI &&
        ClassifyBmi(35.0) == Category::ObeseClassII &&
        ClassifyBmi(40.0) == Category::ObeseClassIII,
        "BMI categories use the managed WHO boundary inclusivity");

    suite.Expect(
        RoundAge(30.4) == 30 && RoundAge(30.5) == 30 && RoundAge(31.5) == 32 &&
        RoundAge(-1.5) == -2 && RoundAge(-2.5) == -2,
        "BMI age conversion mirrors Math.Round midpoint-to-even semantics");

    suite.Expect(
        !RoundAge(std::numeric_limits<double>::quiet_NaN()) &&
        !RoundAge(std::numeric_limits<double>::infinity()) &&
        !RoundAge(1.0e30),
        "BMI age conversion fails closed for non-finite and out-of-range editors");

    auto const maleBmr = CalculateBmr(Sex::Male, 30, 170.0, 65.0);
    auto const femaleBmr = CalculateBmr(Sex::Female, 30, 170.0, 65.0);
    suite.Expect(
        maleBmr && Near(*maleBmr, 1567.5) &&
        femaleBmr && Near(*femaleBmr, 1401.5),
        "Mifflin–St Jeor BMR preserves male and female managed offsets");

    suite.Expect(
        !CalculateBmr(Sex::Male, 0, 170.0, 65.0) &&
        !CalculateBmr(Sex::Female, 131, 170.0, 65.0) &&
        !CalculateBmr(Sex::Male, 30, 0.0, 65.0) &&
        !CalculateBmr(Sex::Male, 30, 170.0, -1.0),
        "BMR enforces the managed age height and weight bounds");

    auto const tdee = CalculateTdee(maleBmr, ActivityLevels[2].factor);
    suite.Expect(tdee && Near(*tdee, 2429.625) &&
        !CalculateTdee(std::nullopt, ActivityLevels[0].factor) &&
        !CalculateTdee(maleBmr, 0.0),
        "TDEE multiplies a valid BMR by the selected activity factor only");

    suite.Expect(
        Near(ActivityLevels[0].factor, 1.2) && Near(ActivityLevels[1].factor, 1.375) &&
        Near(ActivityLevels[2].factor, 1.55) && Near(ActivityLevels[3].factor, 1.725) &&
        Near(ActivityLevels[4].factor, 1.9),
        "TDEE exposes all five managed activity factors in source order");

    auto const maleFat = CalculateBodyFatNavy(Sex::Male, 170.0, 38.0, 85.0, 95.0);
    auto const femaleFat = CalculateBodyFatNavy(Sex::Female, 165.0, 32.0, 75.0, 100.0);
    suite.Expect(
        maleFat && Near(*maleFat, 17.796653044632) &&
        femaleFat && Near(*femaleFat, 29.9301337527438),
        "US Navy body-fat equations preserve male and female circumference formulas");

    suite.Expect(
        !CalculateBodyFatNavy(Sex::Male, 170.0, 85.0, 85.0, 0.0) &&
        !CalculateBodyFatNavy(Sex::Female, 165.0, 32.0, 75.0, 0.0) &&
        !CalculateBodyFatNavy(Sex::Female, 165.0, 200.0, 75.0, 100.0) &&
        !CalculateBodyFatNavy(Sex::Male, 0.0, 38.0, 85.0, 0.0),
        "US Navy body-fat rejects missing measurements and non-positive circumference differences");

    auto const lowClamped = CalculateBodyFatNavy(Sex::Male, 170.0, 40.0, 41.0, 0.0);
    auto const highClamped = CalculateBodyFatNavy(Sex::Male, 170.0, 38.0, 260.0, 0.0);
    suite.Expect(lowClamped && *lowClamped == 2.0 && highClamped && *highClamped == 70.0,
        "US Navy body-fat clamps finite estimates to the managed 2 to 70 percent band");

    std::cout << "\nBMI Calculator tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
