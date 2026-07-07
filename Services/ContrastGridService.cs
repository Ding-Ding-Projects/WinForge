using System;
using System.Globalization;

namespace WinForge.Services;

/// <summary>
/// 對比度網格 · WCAG contrast-grid helpers — robust hex/rgb parsing (never throws) and the
/// WCAG 2.x relative-luminance contrast ratio. Pure managed, no side-effects.
/// </summary>
public static class ContrastGridService
{
    /// <summary>An sRGB colour parsed from user text. <see cref="Ok"/> is false when parsing failed.</summary>
    public readonly struct Rgb
    {
        public readonly byte R, G, B;
        public readonly bool Ok;
        public Rgb(byte r, byte g, byte b, bool ok) { R = r; G = g; B = b; Ok = ok; }
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>
    /// Parse "#RGB", "#RRGGBB", "RRGGBB", "rgb(r,g,b)" or "r,g,b". Never throws; returns
    /// <c>Ok = false</c> on anything unparseable.
    /// </summary>
    public static Rgb Parse(string? input)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input)) return new Rgb(0, 0, 0, false);
            string s = input.Trim();

            // rgb(...) or r,g,b
            string body = s;
            if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                int lp = s.IndexOf('(');
                int rp = s.IndexOf(')');
                if (lp >= 0 && rp > lp) body = s.Substring(lp + 1, rp - lp - 1);
            }
            if (body.Contains(','))
            {
                var parts = body.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int g)
                    && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int b)
                    && InByte(r) && InByte(g) && InByte(b))
                    return new Rgb((byte)r, (byte)g, (byte)b, true);
                return new Rgb(0, 0, 0, false);
            }

            // hex
            string hex = s.TrimStart('#').Trim();
            if (hex.Length == 3)
                hex = string.Concat(hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]);
            if (hex.Length == 6
                && byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte hr)
                && byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte hg)
                && byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte hb))
                return new Rgb(hr, hg, hb, true);

            return new Rgb(0, 0, 0, false);
        }
        catch
        {
            return new Rgb(0, 0, 0, false);
        }
    }

    private static bool InByte(int v) => v >= 0 && v <= 255;

    /// <summary>WCAG relative luminance of an sRGB colour (0..1).</summary>
    public static double RelativeLuminance(Rgb c)
    {
        double rl = Lin(c.R), gl = Lin(c.G), bl = Lin(c.B);
        return 0.2126 * rl + 0.7152 * gl + 0.0722 * bl;
    }

    private static double Lin(byte channel)
    {
        double cs = channel / 255.0;
        return cs <= 0.03928 ? cs / 12.92 : Math.Pow((cs + 0.055) / 1.055, 2.4);
    }

    /// <summary>WCAG contrast ratio between two colours (1..21). Never throws.</summary>
    public static double ContrastRatio(Rgb a, Rgb b)
    {
        try
        {
            double la = RelativeLuminance(a), lb = RelativeLuminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }
        catch
        {
            return 1.0;
        }
    }

    // WCAG thresholds.
    public const double AaNormal = 4.5;
    public const double AaaNormal = 7.0;
    public const double AaLarge = 3.0;
    public const double AaaLarge = 4.5;
}
