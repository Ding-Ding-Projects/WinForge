using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 混色 / 漸變 · Colour mixer &amp; gradient engine. Pure managed, allocation-light, never throws.
/// Parses hex colours, blends two colours across three colour spaces (sRGB-linear, plain RGB
/// average, HSL) at an arbitrary ratio, and builds an N-step gradient. No side effects.
/// </summary>
public static class ColorMixService
{
    /// <summary>Simple RGB triple (0..255 each). Immutable-ish value holder.</summary>
    public readonly struct Rgb
    {
        public readonly byte R, G, B;
        public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }

        public string Hex => $"#{R:X2}{G:X2}{B:X2}";

        public (double H, double S, double L) ToHsl() => RgbToHsl(this);

        /// <summary>e.g. "rgb(255, 128, 0)".</summary>
        public string RgbCss => $"rgb({R}, {G}, {B})";

        /// <summary>e.g. "hsl(210, 50%, 40%)".</summary>
        public string HslCss
        {
            get
            {
                var (h, s, l) = ToHsl();
                return $"hsl({Math.Round(h)}, {Math.Round(s * 100)}%, {Math.Round(l * 100)}%)";
            }
        }
    }

    public enum BlendSpace
    {
        SrgbLinear, // gamma-correct linear-light interpolation
        RgbAverage, // naive per-channel average in sRGB space
        Hsl         // interpolate hue/sat/lightness
    }

    /// <summary>
    /// Parse "#rrggbb", "rrggbb", "#rgb" or "rgb" (with/without '#'), tolerant of whitespace.
    /// Returns black on any failure — never throws.
    /// </summary>
    public static Rgb Parse(string? text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text)) return new Rgb(0, 0, 0);
            var s = text.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);
            s = s.Trim();
            if (s.Length == 3)
            {
                // shorthand #rgb -> #rrggbb
                var r = HexPair(s[0], s[0]);
                var g = HexPair(s[1], s[1]);
                var b = HexPair(s[2], s[2]);
                if (r < 0 || g < 0 || b < 0) return new Rgb(0, 0, 0);
                return new Rgb((byte)r, (byte)g, (byte)b);
            }
            if (s.Length == 6)
            {
                var r = HexPair(s[0], s[1]);
                var g = HexPair(s[2], s[3]);
                var b = HexPair(s[4], s[5]);
                if (r < 0 || g < 0 || b < 0) return new Rgb(0, 0, 0);
                return new Rgb((byte)r, (byte)g, (byte)b);
            }
            return new Rgb(0, 0, 0);
        }
        catch { return new Rgb(0, 0, 0); }
    }

    private static int HexPair(char hi, char lo)
    {
        int h = HexDigit(hi), l = HexDigit(lo);
        if (h < 0 || l < 0) return -1;
        return (h << 4) | l;
    }

    private static int HexDigit(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }

    /// <summary>
    /// Blend <paramref name="a"/> toward <paramref name="b"/> by <paramref name="t"/> (0..1)
    /// in the chosen colour space. t is clamped. Never throws.
    /// </summary>
    public static Rgb Mix(Rgb a, Rgb b, double t, BlendSpace space)
    {
        try
        {
            if (double.IsNaN(t)) t = 0;
            t = Math.Max(0, Math.Min(1, t));
            switch (space)
            {
                case BlendSpace.RgbAverage:
                    return new Rgb(
                        Lerp8(a.R, b.R, t),
                        Lerp8(a.G, b.G, t),
                        Lerp8(a.B, b.B, t));

                case BlendSpace.Hsl:
                    return MixHsl(a, b, t);

                case BlendSpace.SrgbLinear:
                default:
                    return MixLinear(a, b, t);
            }
        }
        catch { return a; }
    }

    private static byte Lerp8(byte x, byte y, double t)
    {
        double v = x + (y - x) * t;
        if (v < 0) v = 0; else if (v > 255) v = 255;
        return (byte)Math.Round(v, MidpointRounding.AwayFromZero);
    }

    private static Rgb MixLinear(Rgb a, Rgb b, double t)
    {
        double lr = ToLinear(a.R) + (ToLinear(b.R) - ToLinear(a.R)) * t;
        double lg = ToLinear(a.G) + (ToLinear(b.G) - ToLinear(a.G)) * t;
        double lb = ToLinear(a.B) + (ToLinear(b.B) - ToLinear(a.B)) * t;
        return new Rgb(ToSrgb(lr), ToSrgb(lg), ToSrgb(lb));
    }

    private static double ToLinear(byte c)
    {
        double s = c / 255.0;
        return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
    }

    private static byte ToSrgb(double lin)
    {
        if (lin < 0) lin = 0; else if (lin > 1) lin = 1;
        double s = lin <= 0.0031308 ? lin * 12.92 : 1.055 * Math.Pow(lin, 1.0 / 2.4) - 0.055;
        double v = s * 255.0;
        if (v < 0) v = 0; else if (v > 255) v = 255;
        return (byte)Math.Round(v, MidpointRounding.AwayFromZero);
    }

    private static Rgb MixHsl(Rgb a, Rgb b, double t)
    {
        var (h1, s1, l1) = RgbToHsl(a);
        var (h2, s2, l2) = RgbToHsl(b);

        // Interpolate hue along the shorter arc.
        double dh = h2 - h1;
        if (dh > 180) dh -= 360;
        else if (dh < -180) dh += 360;
        double h = h1 + dh * t;
        h = ((h % 360) + 360) % 360;

        double s = s1 + (s2 - s1) * t;
        double l = l1 + (l2 - l1) * t;
        return HslToRgb(h, s, l);
    }

    /// <summary>RGB (0..255) → HSL with H in 0..360, S/L in 0..1.</summary>
    public static (double H, double S, double L) RgbToHsl(Rgb c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2.0;
        double h = 0, s = 0;
        double d = max - min;
        if (d > 1e-9)
        {
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6.0 : 0.0);
            else if (max == g) h = (b - r) / d + 2.0;
            else h = (r - g) / d + 4.0;
            h *= 60.0;
        }
        return (h, s, l);
    }

    /// <summary>HSL (H 0..360, S/L 0..1) → RGB.</summary>
    public static Rgb HslToRgb(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        if (s < 0) s = 0; else if (s > 1) s = 1;
        if (l < 0) l = 0; else if (l > 1) l = 1;

        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double hp = h / 60.0;
        double x = c * (1 - Math.Abs(hp % 2 - 1));
        double r1 = 0, g1 = 0, b1 = 0;
        if (hp < 1) { r1 = c; g1 = x; }
        else if (hp < 2) { r1 = x; g1 = c; }
        else if (hp < 3) { g1 = c; b1 = x; }
        else if (hp < 4) { g1 = x; b1 = c; }
        else if (hp < 5) { r1 = x; b1 = c; }
        else { r1 = c; b1 = x; }
        double m = l - c / 2.0;
        return new Rgb(To255(r1 + m), To255(g1 + m), To255(b1 + m));
    }

    private static byte To255(double v)
    {
        double x = v * 255.0;
        if (x < 0) x = 0; else if (x > 255) x = 255;
        return (byte)Math.Round(x, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Build an N-step gradient (inclusive of both ends) between two colours in the given space.
    /// steps is clamped to 2..64. Never throws.
    /// </summary>
    public static List<Rgb> Gradient(Rgb a, Rgb b, int steps, BlendSpace space)
    {
        var list = new List<Rgb>();
        try
        {
            if (steps < 2) steps = 2;
            if (steps > 64) steps = 64;
            for (int i = 0; i < steps; i++)
            {
                double t = i / (double)(steps - 1);
                list.Add(Mix(a, b, t, space));
            }
        }
        catch { /* return whatever we built */ }
        return list;
    }

    /// <summary>CSS "linear-gradient(...)" string for a list of stops. Never throws.</summary>
    public static string GradientCss(IReadOnlyList<Rgb> stops)
    {
        try
        {
            if (stops == null || stops.Count == 0) return "linear-gradient(90deg)";
            var sb = new StringBuilder();
            sb.Append("linear-gradient(90deg");
            for (int i = 0; i < stops.Count; i++)
            {
                double pct = stops.Count == 1 ? 0 : i * 100.0 / (stops.Count - 1);
                sb.Append(", ").Append(stops[i].Hex).Append(' ')
                  .Append(pct.ToString("0.#", CultureInfo.InvariantCulture)).Append('%');
            }
            sb.Append(')');
            return sb.ToString();
        }
        catch { return "linear-gradient(90deg)"; }
    }

    /// <summary>Choose black/white text that contrasts with the given background.</summary>
    public static bool IsDark(Rgb c)
    {
        // Rec. 601 luma.
        double y = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
        return y < 140;
    }
}
