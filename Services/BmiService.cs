using System;

namespace WinForge.Services;

/// <summary>
/// 健康計算器 · Health calculators — pure managed math for BMI, BMR (Mifflin–St Jeor),
/// daily calorie needs (TDEE) and US-Navy body-fat %. All unit conversions live here so the
/// page only passes raw numbers + a metric/imperial flag. Never throws — bad input yields a
/// null / NaN result the caller renders as a bilingual "enter valid values" status.
/// Estimates only — not medical advice.
/// </summary>
public static class BmiService
{
    public enum Sex { Male, Female }

    /// <summary>Activity multipliers for TDEE (Harris/Mifflin convention).</summary>
    public static readonly (double Factor, string En, string Zh)[] ActivityLevels =
    {
        (1.200, "Sedentary (little/no exercise)", "久坐（好少或者冇運動）"),
        (1.375, "Light (1–3 days/week)",          "輕度（每週 1–3 日）"),
        (1.550, "Moderate (3–5 days/week)",       "中度（每週 3–5 日）"),
        (1.725, "Active (6–7 days/week)",         "活躍（每週 6–7 日）"),
        (1.900, "Very active (physical job)",     "非常活躍（體力勞動）"),
    };

    // ---- unit conversion ----------------------------------------------------
    private const double CmPerInch = 2.54;
    private const double KgPerLb = 0.45359237;

    /// <summary>Inches → centimetres.</summary>
    public static double InToCm(double inches) => inches * CmPerInch;
    /// <summary>Pounds → kilograms.</summary>
    public static double LbToKg(double lb) => lb * KgPerLb;

    /// <summary>Normalise a length to cm given the unit flag (true = metric/cm, false = imperial/in).</summary>
    public static double LengthToCm(double value, bool metric) => metric ? value : InToCm(value);
    /// <summary>Normalise a mass to kg given the unit flag (true = metric/kg, false = imperial/lb).</summary>
    public static double MassToKg(double value, bool metric) => metric ? value : LbToKg(value);

    private static bool Ok(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;

    // ---- BMI ----------------------------------------------------------------
    /// <summary>BMI = kg / m². Returns null on invalid input.</summary>
    public static double? Bmi(double heightCm, double weightKg)
    {
        try
        {
            if (!Ok(heightCm) || !Ok(weightKg)) return null;
            double m = heightCm / 100.0;
            double bmi = weightKg / (m * m);
            return Ok(bmi) ? bmi : null;
        }
        catch { return null; }
    }

    /// <summary>WHO BMI category, bilingual.</summary>
    public static (string En, string Zh) BmiCategory(double bmi)
    {
        if (bmi < 18.5) return ("Underweight", "體重過輕");
        if (bmi < 25.0) return ("Normal weight", "正常體重");
        if (bmi < 30.0) return ("Overweight", "超重");
        if (bmi < 35.0) return ("Obese (class I)", "肥胖（一級）");
        if (bmi < 40.0) return ("Obese (class II)", "肥胖（二級）");
        return ("Obese (class III)", "肥胖（三級）");
    }

    // ---- BMR (Mifflin–St Jeor) ---------------------------------------------
    /// <summary>Basal metabolic rate (kcal/day). Returns null on invalid input.</summary>
    public static double? Bmr(Sex sex, int age, double heightCm, double weightKg)
    {
        try
        {
            if (age <= 0 || age > 130 || !Ok(heightCm) || !Ok(weightKg)) return null;
            double bmr = 10.0 * weightKg + 6.25 * heightCm - 5.0 * age + (sex == Sex.Male ? 5.0 : -161.0);
            return Ok(bmr) ? bmr : null;
        }
        catch { return null; }
    }

    /// <summary>Total daily energy expenditure = BMR × activity factor.</summary>
    public static double? Tdee(double? bmr, double activityFactor)
    {
        if (bmr is not double b || !Ok(b) || !Ok(activityFactor)) return null;
        double t = b * activityFactor;
        return Ok(t) ? t : null;
    }

    // ---- Body fat % (US Navy method) ---------------------------------------
    /// <summary>
    /// US-Navy body-fat estimate. Uses log10 (base-10) circumference formulae; all lengths in cm.
    /// Male needs neck+waist; female needs neck+waist+hips. Returns null on invalid input.
    /// </summary>
    public static double? BodyFatNavy(Sex sex, double heightCm, double neckCm, double waistCm, double hipsCm)
    {
        try
        {
            if (!Ok(heightCm) || !Ok(neckCm) || !Ok(waistCm)) return null;

            double bf;
            if (sex == Sex.Male)
            {
                double denom = waistCm - neckCm;
                if (denom <= 0) return null;
                bf = 495.0 / (1.0324 - 0.19077 * Math.Log10(denom) + 0.15456 * Math.Log10(heightCm)) - 450.0;
            }
            else
            {
                if (!Ok(hipsCm)) return null;
                double denom = waistCm + hipsCm - neckCm;
                if (denom <= 0) return null;
                bf = 495.0 / (1.29579 - 0.35004 * Math.Log10(denom) + 0.22100 * Math.Log10(heightCm)) - 450.0;
            }

            if (double.IsNaN(bf) || double.IsInfinity(bf)) return null;
            // clamp to a sane physiological band
            if (bf < 2.0) bf = 2.0;
            if (bf > 70.0) bf = 70.0;
            return bf;
        }
        catch { return null; }
    }
}
