using System;
using System.Globalization;
using System.Security.Cryptography;

namespace WinForge.Services;

/// <summary>
/// 色彩工具 · Color Tools — pure-managed colour parsing, conversions (HEX / RGB / HSL / HSV / CMYK),
/// harmonious palette generation and WCAG relative-luminance + contrast-ratio maths. No redirect,
/// no external deps. Everything is deterministic and side-effect free (except <see cref="RandomRgb"/>).
/// </summary>
public static class ColorToolsService
{
    /// <summary>Simple RGB byte triple.</summary>
    public readonly record struct Rgb(byte R, byte G, byte B);

    /// <summary>
    /// Parse "#RRGGBB", "#RGB", "RRGGBB", "rgb(r,g,b)" (spaces optional, 0–255 clamped).
    /// Returns false on anything unparseable — never throws.
    /// </summary>
    public static bool TryParse(string? text, out Rgb rgb)
    {
        rgb = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();

        try
        {
            // rgb(r, g, b) form
            if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int open = s.IndexOf('(');
                int close = s.IndexOf(')');
                if (open < 0 || close < 0 || close < open) return false;
                var inner = s.Substring(open + 1, close - open - 1);
                var parts = inner.Split(new[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) return false;
                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) return false;
                if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int g)) return false;
                if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b)) return false;
                rgb = new Rgb(Clamp(r), Clamp(g), Clamp(b));
                return true;
            }

            // hex form
            var hex = s.StartsWith("#") ? s.Substring(1) : s;
            if (hex.Length == 3)
            {
                if (!byte.TryParse(new string(hex[0], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)) return false;
                if (!byte.TryParse(new string(hex[1], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)) return false;
                if (!byte.TryParse(new string(hex[2], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b)) return false;
                rgb = new Rgb(r, g, b);
                return true;
            }
            if (hex.Length == 6)
            {
                if (!byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r)) return false;
                if (!byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g)) return false;
                if (!byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b)) return false;
                rgb = new Rgb(r, g, b);
                return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    private static byte Clamp(int v) => (byte)Math.Max(0, Math.Min(255, v));

    /// <summary>"#RRGGBB" (upper-case).</summary>
    public static string ToHex(Rgb c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>"rgb(r, g, b)".</summary>
    public static string ToRgbString(Rgb c) => $"rgb({c.R}, {c.G}, {c.B})";

    /// <summary>"hsl(h, s%, l%)" — h in degrees 0–360, s/l in percent.</summary>
    public static string ToHslString(Rgb c)
    {
        var (h, s, l) = ToHsl(c);
        return $"hsl({Math.Round(h)}, {Math.Round(s * 100)}%, {Math.Round(l * 100)}%)";
    }

    /// <summary>"hsv(h, s%, v%)".</summary>
    public static string ToHsvString(Rgb c)
    {
        var (h, s, v) = ToHsv(c);
        return $"hsv({Math.Round(h)}, {Math.Round(s * 100)}%, {Math.Round(v * 100)}%)";
    }

    /// <summary>"cmyk(c%, m%, y%, k%)".</summary>
    public static string ToCmykString(Rgb c)
    {
        var (cy, m, y, k) = ToCmyk(c);
        return $"cmyk({Math.Round(cy * 100)}%, {Math.Round(m * 100)}%, {Math.Round(y * 100)}%, {Math.Round(k * 100)}%)";
    }

    /// <summary>RGB → HSL. h: 0–360, s/l: 0–1.</summary>
    public static (double H, double S, double L) ToHsl(Rgb c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double h = 0, s, l = (max + min) / 2.0;
        double d = max - min;

        if (d == 0)
        {
            h = 0; s = 0;
        }
        else
        {
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }
        return (h, s, l);
    }

    /// <summary>RGB → HSV. h: 0–360, s/v: 0–1.</summary>
    public static (double H, double S, double V) ToHsv(Rgb c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        double h = 0;
        double s = max == 0 ? 0 : d / max;
        double v = max;

        if (d != 0)
        {
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
        }
        return (h, s, v);
    }

    /// <summary>RGB → CMYK, each channel 0–1.</summary>
    public static (double C, double M, double Y, double K) ToCmyk(Rgb c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double k = 1 - Math.Max(r, Math.Max(g, b));
        if (Math.Abs(1 - k) < 1e-9) return (0, 0, 0, 1); // pure black
        double cy = (1 - r - k) / (1 - k);
        double m = (1 - g - k) / (1 - k);
        double y = (1 - b - k) / (1 - k);
        return (cy, m, y, k);
    }

    /// <summary>HSL → RGB. h in degrees, s/l 0–1.</summary>
    public static Rgb FromHsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        s = Math.Max(0, Math.Min(1, s));
        l = Math.Max(0, Math.Min(1, l));

        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = l - c / 2;
        double r1 = 0, g1 = 0, b1 = 0;

        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }

        return new Rgb(
            (byte)Math.Round((r1 + m) * 255),
            (byte)Math.Round((g1 + m) * 255),
            (byte)Math.Round((b1 + m) * 255));
    }

    /// <summary>
    /// 5 harmonious swatches from the current hue: analogous −60°/−30°, the colour itself,
    /// analogous +30°, and the complementary +180°. Saturation/lightness preserved.
    /// </summary>
    public static Rgb[] Palette(Rgb c)
    {
        var (h, s, l) = ToHsl(c);
        return new[]
        {
            FromHsl(h - 60, s, l),
            FromHsl(h - 30, s, l),
            c,
            FromHsl(h + 30, s, l),
            FromHsl(h + 180, s, l), // complementary
        };
    }

    /// <summary>WCAG relative luminance (sRGB), 0–1.</summary>
    public static double RelativeLuminance(Rgb c)
    {
        double Lin(double ch)
        {
            ch /= 255.0;
            return ch <= 0.03928 ? ch / 12.92 : Math.Pow((ch + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Lin(c.R) + 0.7152 * Lin(c.G) + 0.0722 * Lin(c.B);
    }

    /// <summary>WCAG contrast ratio between two colours (1.0–21.0).</summary>
    public static double ContrastRatio(Rgb a, Rgb b)
    {
        double la = RelativeLuminance(a);
        double lb = RelativeLuminance(b);
        double hi = Math.Max(la, lb);
        double lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>Cryptographically-seeded random opaque colour.</summary>
    public static Rgb RandomRgb()
    {
        Span<byte> buf = stackalloc byte[3];
        RandomNumberGenerator.Fill(buf);
        return new Rgb(buf[0], buf[1], buf[2]);
    }
}
