using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 色彩調色板產生器 · Color-palette generator. Pure-managed HSL colour maths — parse a base colour,
/// derive schemes (complementary, analogous, triadic, …) and export as CSS variables / JSON.
/// No process launch, no pickers, no network. Randomness via <see cref="RandomNumberGenerator"/>.
/// </summary>
public static class ColorPaletteService
{
    /// <summary>An 8-bit-per-channel RGB colour (alpha assumed opaque).</summary>
    public readonly struct Rgb
    {
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }

        /// <summary>Uppercase #RRGGBB.</summary>
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>Supported palette schemes.</summary>
    public enum Scheme
    {
        Complementary,
        Analogous,
        Triadic,
        Tetradic,
        SplitComplementary,
        Monochromatic,
        Shades,
        Tints
    }

    // ---- Parsing ----------------------------------------------------------

    /// <summary>
    /// Robustly parse "#hex" (3/4/6/8 digit), "rgb(r,g,b)" or "r,g,b" / "r g b". Returns false, never throws.
    /// </summary>
    public static bool TryParse(string? text, out Rgb rgb)
    {
        rgb = new Rgb(0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            string s = text.Trim();

            // rgb( ... ) wrapper
            int lp = s.IndexOf('(');
            int rp = s.IndexOf(')');
            if (lp >= 0 && rp > lp)
                s = s.Substring(lp + 1, rp - lp - 1);

            if (s.StartsWith("#", StringComparison.Ordinal) || LooksLikeBareHex(s))
                return TryParseHex(s.TrimStart('#'), out rgb);

            // Comma / whitespace separated numbers
            var parts = s.Split(new[] { ',', ' ', '\t', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && ByteFromComponent(parts[0], out byte r)
                && ByteFromComponent(parts[1], out byte g)
                && ByteFromComponent(parts[2], out byte b))
            {
                rgb = new Rgb(r, g, b);
                return true;
            }
        }
        catch { /* never throw */ }
        return false;
    }

    private static bool LooksLikeBareHex(string s)
    {
        if (s.Length is not (3 or 4 or 6 or 8)) return false;
        foreach (char c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    private static bool ByteFromComponent(string p, out byte val)
    {
        val = 0;
        p = p.Trim();
        if (p.EndsWith("%", StringComparison.Ordinal))
        {
            if (double.TryParse(p.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out double pct))
            {
                val = ClampByte((int)Math.Round(pct / 100.0 * 255.0));
                return true;
            }
            return false;
        }
        if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
        {
            val = ClampByte((int)Math.Round(d));
            return true;
        }
        return false;
    }

    private static bool TryParseHex(string h, out Rgb rgb)
    {
        rgb = new Rgb(0, 0, 0);
        h = h.Trim();
        try
        {
            if (h.Length == 3 || h.Length == 4) // shorthand #RGB / #RGBA
            {
                byte r = (byte)(HexVal(h[0]) * 17);
                byte g = (byte)(HexVal(h[1]) * 17);
                byte b = (byte)(HexVal(h[2]) * 17);
                rgb = new Rgb(r, g, b);
                return true;
            }
            if (h.Length == 6 || h.Length == 8) // #RRGGBB / #RRGGBBAA
            {
                byte r = (byte)((HexVal(h[0]) << 4) | HexVal(h[1]));
                byte g = (byte)((HexVal(h[2]) << 4) | HexVal(h[3]));
                byte b = (byte)((HexVal(h[4]) << 4) | HexVal(h[5]));
                rgb = new Rgb(r, g, b);
                return true;
            }
        }
        catch { /* fall through */ }
        return false;
    }

    private static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        throw new FormatException("bad hex digit");
    }

    private static byte ClampByte(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);
    private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;

    // ---- HSL conversion ---------------------------------------------------

    /// <summary>RGB (0-255) → HSL where H is degrees [0,360), S/L are [0,1].</summary>
    public static (double H, double S, double L) ToHsl(Rgb c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double h = 0, s, l = (max + min) / 2.0;
        double d = max - min;
        if (d < 1e-9)
        {
            s = 0; h = 0;
        }
        else
        {
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60.0;
        }
        return (h, s, l);
    }

    /// <summary>HSL (H degrees, S/L [0,1]) → RGB (0-255).</summary>
    public static Rgb FromHsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360;
        s = Clamp01(s);
        l = Clamp01(l);
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = l - c / 2.0;
        double r1, g1, b1;
        if (h < 60) { r1 = c; g1 = x; b1 = 0; }
        else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
        else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
        else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
        else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
        else { r1 = c; g1 = 0; b1 = x; }
        return new Rgb(
            ClampByte((int)Math.Round((r1 + m) * 255)),
            ClampByte((int)Math.Round((g1 + m) * 255)),
            ClampByte((int)Math.Round((b1 + m) * 255)));
    }

    // ---- Scheme generation ------------------------------------------------

    /// <summary>Build a palette (base first) for the given scheme. Never throws; returns at least the base.</summary>
    public static List<Rgb> Generate(Rgb baseColor, Scheme scheme)
    {
        var list = new List<Rgb>();
        try
        {
            var (h, s, l) = ToHsl(baseColor);
            switch (scheme)
            {
                case Scheme.Complementary:
                    list.Add(baseColor);
                    list.Add(FromHsl(h + 180, s, l));
                    break;
                case Scheme.Analogous:
                    list.Add(FromHsl(h - 30, s, l));
                    list.Add(baseColor);
                    list.Add(FromHsl(h + 30, s, l));
                    list.Add(FromHsl(h + 60, s, l));
                    break;
                case Scheme.Triadic:
                    list.Add(baseColor);
                    list.Add(FromHsl(h + 120, s, l));
                    list.Add(FromHsl(h + 240, s, l));
                    break;
                case Scheme.Tetradic:
                    list.Add(baseColor);
                    list.Add(FromHsl(h + 90, s, l));
                    list.Add(FromHsl(h + 180, s, l));
                    list.Add(FromHsl(h + 270, s, l));
                    break;
                case Scheme.SplitComplementary:
                    list.Add(baseColor);
                    list.Add(FromHsl(h + 150, s, l));
                    list.Add(FromHsl(h + 210, s, l));
                    break;
                case Scheme.Monochromatic:
                    for (int i = 0; i < 5; i++)
                    {
                        double ll = 0.15 + i * 0.175; // spread lightness
                        list.Add(FromHsl(h, s, ll));
                    }
                    break;
                case Scheme.Shades: // toward black
                    for (int i = 0; i < 6; i++)
                        list.Add(FromHsl(h, s, l * (1 - i / 6.0)));
                    break;
                case Scheme.Tints: // toward white
                    for (int i = 0; i < 6; i++)
                        list.Add(FromHsl(h, s, l + (1 - l) * (i / 6.0)));
                    break;
                default:
                    list.Add(baseColor);
                    break;
            }
        }
        catch
        {
            list.Clear();
            list.Add(baseColor);
        }
        if (list.Count == 0) list.Add(baseColor);
        return list;
    }

    // ---- Random -----------------------------------------------------------

    /// <summary>A cryptographically-random opaque colour.</summary>
    public static Rgb RandomColor()
    {
        Span<byte> buf = stackalloc byte[3];
        RandomNumberGenerator.Fill(buf);
        return new Rgb(buf[0], buf[1], buf[2]);
    }

    // ---- Export -----------------------------------------------------------

    /// <summary>Palette as CSS custom properties: <c>:root { --c1:#..; --c2:#..; }</c></summary>
    public static string ToCss(IReadOnlyList<Rgb> palette)
    {
        var sb = new StringBuilder();
        sb.Append(":root {");
        sb.Append('\n');
        for (int i = 0; i < palette.Count; i++)
            sb.Append("  --c").Append(i + 1).Append(": ").Append(palette[i].Hex).Append(';').Append('\n');
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>Palette as a JSON array of hex strings.</summary>
    public static string ToJson(IReadOnlyList<Rgb> palette)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        for (int i = 0; i < palette.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append('"').Append(palette[i].Hex).Append('"');
        }
        sb.Append(']');
        return sb.ToString();
    }
}
