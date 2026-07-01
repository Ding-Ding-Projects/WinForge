using System;

namespace WinForge.Services;

/// <summary>
/// 色盲模擬（CVD simulation）· Pure-managed colour-vision-deficiency simulator. Converts sRGB → linear,
/// applies the well-known CVD transformation matrices (Machado/Brettel-style approximations) per type,
/// then converts back to sRGB. Anomalous trichromacies are ~50% blends with the original colour.
/// No I/O, no redirect, never throws. These are approximations, not medical instruments.
/// </summary>
public static class ColorBlindService
{
    public enum Cvd
    {
        Protanopia,
        Protanomaly,
        Deuteranopia,
        Deuteranomaly,
        Tritanopia,
        Tritanomaly,
        Achromatopsia,
    }

    // sRGB companding -------------------------------------------------------
    private static double ToLinear(double c)
    {
        c = Math.Clamp(c, 0.0, 1.0);
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    private static double ToSrgb(double c)
    {
        c = Math.Clamp(c, 0.0, 1.0);
        return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1.0 / 2.4) - 0.055;
    }

    // Dichromacy matrices operate on linear RGB (row-major 3x3).
    // Values are the commonly-cited Brettel/Vienot approximations.
    private static readonly double[][] Protan =
    {
        new[] { 0.152286, 1.052583, -0.204868 },
        new[] { 0.114503, 0.786281, 0.099216 },
        new[] { -0.003882, -0.048116, 1.051998 },
    };

    private static readonly double[][] Deutan =
    {
        new[] { 0.367322, 0.860646, -0.227968 },
        new[] { 0.280085, 0.672501, 0.047413 },
        new[] { -0.011820, 0.042940, 0.968881 },
    };

    private static readonly double[][] Tritan =
    {
        new[] { 1.255528, -0.076749, -0.178779 },
        new[] { -0.078411, 0.930809, 0.147602 },
        new[] { 0.004733, 0.691367, 0.303900 },
    };

    private static void Apply(double[][] m, double r, double g, double b, out double or, out double og, out double ob)
    {
        or = m[0][0] * r + m[0][1] * g + m[0][2] * b;
        og = m[1][0] * r + m[1][1] * g + m[1][2] * b;
        ob = m[2][0] * r + m[2][1] * g + m[2][2] * b;
    }

    /// <summary>
    /// Simulate how the given 0-255 sRGB colour appears under the chosen deficiency.
    /// Returns clamped 0-255 bytes. Never throws.
    /// </summary>
    public static (byte R, byte G, byte B) Simulate(byte r, byte g, byte b, Cvd type)
    {
        try
        {
            if (type == Cvd.Achromatopsia)
            {
                // Luminance-preserving grayscale in linear space.
                double lr = ToLinear(r / 255.0), lg = ToLinear(g / 255.0), lb = ToLinear(b / 255.0);
                double y = 0.2126 * lr + 0.7152 * lg + 0.0722 * lb;
                byte v = ToByte(ToSrgb(y));
                return (v, v, v);
            }

            double[][] matrix;
            double blend; // 0 = full dichromat, 0.5 = anomalous
            switch (type)
            {
                case Cvd.Protanopia: matrix = Protan; blend = 0.0; break;
                case Cvd.Protanomaly: matrix = Protan; blend = 0.5; break;
                case Cvd.Deuteranopia: matrix = Deutan; blend = 0.0; break;
                case Cvd.Deuteranomaly: matrix = Deutan; blend = 0.5; break;
                case Cvd.Tritanopia: matrix = Tritan; blend = 0.0; break;
                case Cvd.Tritanomaly: matrix = Tritan; blend = 0.5; break;
                default: return (r, g, b);
            }

            double slr = ToLinear(r / 255.0), slg = ToLinear(g / 255.0), slb = ToLinear(b / 255.0);
            Apply(matrix, slr, slg, slb, out double dr, out double dg, out double db);

            // Anomalous = partial blend between original (linear) and full dichromat sim.
            if (blend > 0.0)
            {
                dr = slr * blend + dr * (1.0 - blend);
                dg = slg * blend + dg * (1.0 - blend);
                db = slb * blend + db * (1.0 - blend);
            }

            return (ToByte(ToSrgb(dr)), ToByte(ToSrgb(dg)), ToByte(ToSrgb(db)));
        }
        catch
        {
            return (r, g, b);
        }
    }

    private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v * 255.0), 0, 255);

    /// <summary>#RRGGBB uppercase.</summary>
    public static string ToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    /// <summary>
    /// Parse "#RGB", "#RRGGBB", "rgb(r,g,b)" or "r,g,b". Returns false on any bad input; never throws.
    /// </summary>
    public static bool TryParse(string? text, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            string s = text.Trim();

            if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int lp = s.IndexOf('('), rp = s.IndexOf(')');
                if (lp < 0 || rp < lp) return false;
                s = s.Substring(lp + 1, rp - lp - 1);
            }

            if (s.StartsWith("#")) s = s.Substring(1);

            // Hex forms
            if (s.Length == 6 && IsHex(s))
            {
                r = Convert.ToByte(s.Substring(0, 2), 16);
                g = Convert.ToByte(s.Substring(2, 2), 16);
                b = Convert.ToByte(s.Substring(4, 2), 16);
                return true;
            }
            if (s.Length == 3 && IsHex(s))
            {
                r = Convert.ToByte(new string(s[0], 2), 16);
                g = Convert.ToByte(new string(s[1], 2), 16);
                b = Convert.ToByte(new string(s[2], 2), 16);
                return true;
            }

            // Comma / whitespace separated triple
            var parts = s.Split(new[] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3
                && int.TryParse(parts[0], out int ri)
                && int.TryParse(parts[1], out int gi)
                && int.TryParse(parts[2], out int bi))
            {
                r = (byte)Math.Clamp(ri, 0, 255);
                g = (byte)Math.Clamp(gi, 0, 255);
                b = (byte)Math.Clamp(bi, 0, 255);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHex(string s)
    {
        foreach (char c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
