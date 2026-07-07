using System;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 科學／工程記數法轉換 · Scientific / engineering notation converter.
/// Pure managed. Parses plain ("12345.678"), E-notation ("1.2345e4") and
/// "1.2×10^4"-style input; produces standard decimal, scientific (a×10^b, 1≤|a|&lt;10),
/// engineering (exponent a multiple of 3), E-notation and SI-prefix forms. Never throws.
/// </summary>
public static class SciNotationService
{
    /// <summary>Result of a conversion; <see cref="Ok"/> is false when the input could not be parsed.</summary>
    public sealed class Result
    {
        public bool Ok { get; init; }
        public double Value { get; init; }
        public string Standard { get; init; } = "";
        public string Scientific { get; init; } = "";
        public string Engineering { get; init; } = "";
        public string ENotation { get; init; } = "";
        public string SiPrefix { get; init; } = "";
    }

    // SI prefixes keyed by the engineering exponent (multiple of 3).
    private static readonly (int Exp, string Symbol, string Name)[] SiPrefixes =
    {
        (30, "Q", "quetta"), (27, "R", "ronna"), (24, "Y", "yotta"), (21, "Z", "zetta"),
        (18, "E", "exa"),    (15, "P", "peta"),  (12, "T", "tera"),  (9,  "G", "giga"),
        (6,  "M", "mega"),   (3,  "k", "kilo"),  (0,  "",  ""),
        (-3, "m", "milli"),  (-6, "µ", "micro"), (-9, "n", "nano"), (-12, "p", "pico"),
        (-15, "f", "femto"), (-18, "a", "atto"), (-21, "z", "zepto"), (-24, "y", "yocto"),
        (-27, "r", "ronto"), (-30, "q", "quecto"),
    };

    /// <summary>
    /// Convert <paramref name="input"/> using <paramref name="sigFigs"/> (1–15) significant figures
    /// for the mantissa. Returns a <see cref="Result"/>; never throws.
    /// </summary>
    public static Result Convert(string? input, int sigFigs)
    {
        try
        {
            if (sigFigs < 1) sigFigs = 1;
            if (sigFigs > 15) sigFigs = 15;

            if (!TryParse(input, out double value))
                return new Result { Ok = false };

            if (double.IsNaN(value) || double.IsInfinity(value))
                return new Result { Ok = false };

            // Round the whole value to the requested significant figures.
            double rounded = RoundToSignificant(value, sigFigs);

            return new Result
            {
                Ok = true,
                Value = rounded,
                Standard = FormatStandard(rounded, sigFigs),
                Scientific = FormatScientific(rounded, sigFigs, "×10^"),
                Engineering = FormatEngineering(rounded, sigFigs, out _),
                ENotation = FormatScientific(rounded, sigFigs, "E"),
                SiPrefix = FormatSiPrefix(rounded, sigFigs),
            };
        }
        catch
        {
            return new Result { Ok = false };
        }
    }

    /// <summary>Robust parse: plain, E-notation, and "a×10^b" / "a x 10^b" / "a*10^b" styles.</summary>
    public static bool TryParse(string? raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        string s = raw.Trim();

        // Normalise unicode / thousands / typographic characters.
        s = s.Replace("−", "-")   // minus sign
             .Replace("–", "-")   // en dash
             .Replace(",", "")          // thousands separators
             .Replace(" ", "")     // nbsp
             .Replace(" ", "");

        // "a×10^b" family → convert to E-notation. Handle ×, x, X, *, · as the multiply sign.
        int mulIdx = IndexOfMul(s);
        if (mulIdx >= 0)
        {
            string left = s.Substring(0, mulIdx);
            string right = s.Substring(mulIdx + 1);

            // right should look like 10^b or 10b.
            if (right.StartsWith("10^")) right = right.Substring(3);
            else if (right.StartsWith("10")) right = right.Substring(2);
            else return false;

            if (left.Length == 0) left = "1";
            if (!double.TryParse(left, NumberStyles.Float, CultureInfo.InvariantCulture, out double mant))
                return false;
            if (!int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out int exp))
                return false;

            value = mant * Math.Pow(10, exp);
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        // Plain or E-notation.
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return !double.IsNaN(value) && !double.IsInfinity(value);

        return false;
    }

    private static int IndexOfMul(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '×' || c == 'x' || c == 'X' || c == '*' || c == '·')
                return i;
        }
        return -1;
    }

    private static double RoundToSignificant(double v, int sig)
    {
        if (v == 0) return 0;
        double d = Math.Ceiling(Math.Log10(Math.Abs(v)));
        int power = sig - (int)d;
        double magnitude = Math.Pow(10, power);
        double result = Math.Round(v * magnitude, MidpointRounding.AwayFromZero) / magnitude;
        return double.IsNaN(result) || double.IsInfinity(result) ? v : result;
    }

    // Break a value into a normalised mantissa (1≤|m|<10) and base-10 exponent.
    private static void Decompose(double v, out double mantissa, out int exponent)
    {
        if (v == 0) { mantissa = 0; exponent = 0; return; }
        exponent = (int)Math.Floor(Math.Log10(Math.Abs(v)));
        mantissa = v / Math.Pow(10, exponent);

        // Guard against floating rounding pushing mantissa to 10 / below 1.
        if (Math.Abs(mantissa) >= 10) { mantissa /= 10; exponent++; }
        else if (Math.Abs(mantissa) < 1) { mantissa *= 10; exponent--; }
    }

    private static string TrimMantissa(double m, int sig)
    {
        // Show up to (sig-1) decimals then trim trailing zeros.
        int decimals = Math.Max(0, sig - 1);
        string s = m.ToString("F" + decimals, CultureInfo.InvariantCulture);
        if (s.Contains('.'))
            s = s.TrimEnd('0').TrimEnd('.');
        return s;
    }

    private static string FormatStandard(double v, int sig)
    {
        if (v == 0) return "0";
        // "R"-style general formatting without scientific notation, honouring sig figs.
        // Use up to 15 decimals then trim; large magnitudes print in full.
        string s = v.ToString("0.###############", CultureInfo.InvariantCulture);
        return s;
    }

    private static string FormatScientific(double v, int sig, string sep)
    {
        if (v == 0) return sep == "E" ? "0E+0" : "0×10^0";
        Decompose(v, out double m, out int e);
        string mant = TrimMantissa(m, sig);
        if (sep == "E")
            return mant + "E" + (e >= 0 ? "+" : "") + e.ToString(CultureInfo.InvariantCulture);
        return mant + sep + e.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatEngineering(double v, int sig, out int engExp)
    {
        engExp = 0;
        if (v == 0) return "0×10^0";
        Decompose(v, out double m, out int e);

        // Shift exponent down to the nearest lower multiple of 3.
        int rem = ((e % 3) + 3) % 3;
        engExp = e - rem;
        double engMant = m * Math.Pow(10, rem);
        string mant = TrimMantissa(engMant, Math.Max(sig, sig + rem));
        return mant + "×10^" + engExp.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatSiPrefix(double v, int sig)
    {
        if (v == 0) return "0";
        FormatEngineering(v, sig, out int engExp);
        Decompose(v, out double m, out int e);
        int rem = ((e % 3) + 3) % 3;
        double engMant = m * Math.Pow(10, rem);

        foreach (var p in SiPrefixes)
        {
            if (p.Exp == engExp)
            {
                string mant = TrimMantissa(engMant, Math.Max(sig, sig + rem));
                if (p.Symbol.Length == 0)
                    return mant;
                return mant + " " + p.Symbol + " (" + p.Name + ")";
            }
        }

        // Outside the SI prefix range → fall back to engineering form.
        return FormatEngineering(v, sig, out _);
    }
}
