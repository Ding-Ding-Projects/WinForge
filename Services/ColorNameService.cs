using System;
using System.Collections.Generic;
using System.Globalization;

namespace WinForge.Services;

/// <summary>
/// 命名色彩（CSS/X11 具名色）· CSS/X11 named-colour catalogue + robust hex/rgb parsing and
/// nearest-named-colour lookup by RGB Euclidean distance. Pure managed, never throws. No redirect.
/// </summary>
public static class ColorNameService
{
    /// <summary>A single named colour (name + 0-255 RGB channels).</summary>
    public readonly struct NamedColor
    {
        public readonly string Name;
        public readonly byte R, G, B;
        public NamedColor(string name, byte r, byte g, byte b) { Name = name; R = r; G = g; B = b; }
        /// <summary>Uppercase #RRGGBB.</summary>
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>A parsed RGB triple result.</summary>
    public readonly struct Rgb
    {
        public readonly byte R, G, B;
        public Rgb(byte r, byte g, byte b) { R = r; G = g; B = b; }
        public string Hex => $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <summary>The full CSS/X11 named-colour list (148 entries), each name + hex.</summary>
    public static IReadOnlyList<NamedColor> All => _all;

    private static readonly NamedColor[] _all = Build();

    private static NamedColor[] Build()
    {
        // (name, "RRGGBB") — the CSS Color Module Level 4 / X11 extended set.
        var raw = new (string, string)[]
        {
            ("AliceBlue","F0F8FF"), ("AntiqueWhite","FAEBD7"), ("Aqua","00FFFF"), ("Aquamarine","7FFFD4"),
            ("Azure","F0FFFF"), ("Beige","F5F5DC"), ("Bisque","FFE4C4"), ("Black","000000"),
            ("BlanchedAlmond","FFEBCD"), ("Blue","0000FF"), ("BlueViolet","8A2BE2"), ("Brown","A52A2A"),
            ("BurlyWood","DEB887"), ("CadetBlue","5F9EA0"), ("Chartreuse","7FFF00"), ("Chocolate","D2691E"),
            ("Coral","FF7F50"), ("CornflowerBlue","6495ED"), ("Cornsilk","FFF8DC"), ("Crimson","DC143C"),
            ("Cyan","00FFFF"), ("DarkBlue","00008B"), ("DarkCyan","008B8B"), ("DarkGoldenrod","B8860B"),
            ("DarkGray","A9A9A9"), ("DarkGreen","006400"), ("DarkKhaki","BDB76B"), ("DarkMagenta","8B008B"),
            ("DarkOliveGreen","556B2F"), ("DarkOrange","FF8C00"), ("DarkOrchid","9932CC"), ("DarkRed","8B0000"),
            ("DarkSalmon","E9967A"), ("DarkSeaGreen","8FBC8F"), ("DarkSlateBlue","483D8B"), ("DarkSlateGray","2F4F4F"),
            ("DarkTurquoise","00CED1"), ("DarkViolet","9400D3"), ("DeepPink","FF1493"), ("DeepSkyBlue","00BFFF"),
            ("DimGray","696969"), ("DodgerBlue","1E90FF"), ("Firebrick","B22222"), ("FloralWhite","FFFAF0"),
            ("ForestGreen","228B22"), ("Fuchsia","FF00FF"), ("Gainsboro","DCDCDC"), ("GhostWhite","F8F8FF"),
            ("Gold","FFD700"), ("Goldenrod","DAA520"), ("Gray","808080"), ("Green","008000"),
            ("GreenYellow","ADFF2F"), ("Honeydew","F0FFF0"), ("HotPink","FF69B4"), ("IndianRed","CD5C5C"),
            ("Indigo","4B0082"), ("Ivory","FFFFF0"), ("Khaki","F0E68C"), ("Lavender","E6E6FA"),
            ("LavenderBlush","FFF0F5"), ("LawnGreen","7CFC00"), ("LemonChiffon","FFFACD"), ("LightBlue","ADD8E6"),
            ("LightCoral","F08080"), ("LightCyan","E0FFFF"), ("LightGoldenrodYellow","FAFAD2"), ("LightGray","D3D3D3"),
            ("LightGreen","90EE90"), ("LightPink","FFB6C1"), ("LightSalmon","FFA07A"), ("LightSeaGreen","20B2AA"),
            ("LightSkyBlue","87CEFA"), ("LightSlateGray","778899"), ("LightSteelBlue","B0C4DE"), ("LightYellow","FFFFE0"),
            ("Lime","00FF00"), ("LimeGreen","32CD32"), ("Linen","FAF0E6"), ("Magenta","FF00FF"),
            ("Maroon","800000"), ("MediumAquamarine","66CDAA"), ("MediumBlue","0000CD"), ("MediumOrchid","BA55D3"),
            ("MediumPurple","9370DB"), ("MediumSeaGreen","3CB371"), ("MediumSlateBlue","7B68EE"), ("MediumSpringGreen","00FA9A"),
            ("MediumTurquoise","48D1CC"), ("MediumVioletRed","C71585"), ("MidnightBlue","191970"), ("MintCream","F5FFFA"),
            ("MistyRose","FFE4E1"), ("Moccasin","FFE4B5"), ("NavajoWhite","FFDEAD"), ("Navy","000080"),
            ("OldLace","FDF5E6"), ("Olive","808000"), ("OliveDrab","6B8E23"), ("Orange","FFA500"),
            ("OrangeRed","FF4500"), ("Orchid","DA70D6"), ("PaleGoldenrod","EEE8AA"), ("PaleGreen","98FB98"),
            ("PaleTurquoise","AFEEEE"), ("PaleVioletRed","DB7093"), ("PapayaWhip","FFEFD5"), ("PeachPuff","FFDAB9"),
            ("Peru","CD853F"), ("Pink","FFC0CB"), ("Plum","DDA0DD"), ("PowderBlue","B0E0E6"),
            ("Purple","800080"), ("RebeccaPurple","663399"), ("Red","FF0000"), ("RosyBrown","BC8F8F"),
            ("RoyalBlue","4169E1"), ("SaddleBrown","8B4513"), ("Salmon","FA8072"), ("SandyBrown","F4A460"),
            ("SeaGreen","2E8B57"), ("SeaShell","FFF5EE"), ("Sienna","A0522D"), ("Silver","C0C0C0"),
            ("SkyBlue","87CEEB"), ("SlateBlue","6A5ACD"), ("SlateGray","708090"), ("Snow","FFFAFA"),
            ("SpringGreen","00FF7F"), ("SteelBlue","4682B4"), ("Tan","D2B48C"), ("Teal","008080"),
            ("Thistle","D8BFD8"), ("Tomato","FF6347"), ("Turquoise","40E0D0"), ("Violet","EE82EE"),
            ("Wheat","F5DEB3"), ("White","FFFFFF"), ("WhiteSmoke","F5F5F5"), ("Yellow","FFFF00"),
            ("YellowGreen","9ACD32"),
        };

        var list = new NamedColor[raw.Length];
        for (int i = 0; i < raw.Length; i++)
        {
            var (name, hex) = raw[i];
            byte r = ByteFrom(hex, 0), g = ByteFrom(hex, 2), b = ByteFrom(hex, 4);
            list[i] = new NamedColor(name, r, g, b);
        }
        return list;
    }

    private static byte ByteFrom(string sixHex, int at)
    {
        try { return byte.Parse(sixHex.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    /// <summary>
    /// Parse a colour from either "#RRGGBB", "RRGGBB", "#RGB", "RGB", or "rgb(r,g,b)" / "r,g,b".
    /// Never throws; returns false on anything unrecognizable.
    /// </summary>
    public static bool TryParse(string? input, out Rgb rgb)
    {
        rgb = default;
        try
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string s = input.Trim();

            // rgb(...) / functional or bare comma/space separated triple
            if (s.IndexOf(',') >= 0 || s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                string body = s;
                int lp = body.IndexOf('(');
                int rp = body.LastIndexOf(')');
                if (lp >= 0 && rp > lp) body = body.Substring(lp + 1, rp - lp - 1);
                var parts = body.Split(new[] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && TryChannel(parts[0], out byte rr)
                    && TryChannel(parts[1], out byte gg)
                    && TryChannel(parts[2], out byte bb))
                {
                    rgb = new Rgb(rr, gg, bb);
                    return true;
                }
                // fall through to hex attempts if the triple failed
            }

            // hex forms
            string hex = s.StartsWith("#", StringComparison.Ordinal) ? s.Substring(1) : s;
            hex = hex.Trim();
            if (hex.Length == 3 && IsHex(hex))
            {
                byte r = HexNibble(hex[0]), g = HexNibble(hex[1]), b = HexNibble(hex[2]);
                rgb = new Rgb((byte)(r * 17), (byte)(g * 17), (byte)(b * 17));
                return true;
            }
            if (hex.Length == 6 && IsHex(hex))
            {
                rgb = new Rgb(ByteFrom(hex, 0), ByteFrom(hex, 2), ByteFrom(hex, 4));
                return true;
            }
            if (hex.Length == 8 && IsHex(hex))
            {
                // #AARRGGBB or #RRGGBBAA — take the middle/leading RGB; assume RRGGBBAA (drop alpha).
                rgb = new Rgb(ByteFrom(hex, 0), ByteFrom(hex, 2), ByteFrom(hex, 4));
                return true;
            }

            // named colour by exact name (case/space-insensitive)
            string norm = Normalize(s);
            foreach (var c in _all)
                if (Normalize(c.Name) == norm) { rgb = new Rgb(c.R, c.G, c.B); return true; }

            return false;
        }
        catch { return false; }
    }

    private static bool TryChannel(string p, out byte value)
    {
        value = 0;
        if (int.TryParse(p.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
        {
            if (v < 0) v = 0; else if (v > 255) v = 255;
            value = (byte)v;
            return true;
        }
        return false;
    }

    private static bool IsHex(string s)
    {
        foreach (char ch in s) if (!Uri.IsHexDigit(ch)) return false;
        return true;
    }

    private static byte HexNibble(char c)
    {
        try { return byte.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    private static string Normalize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char ch in s) if (!char.IsWhiteSpace(ch) && ch != '-' && ch != '_') sb.Append(char.ToLowerInvariant(ch));
        return sb.ToString();
    }

    /// <summary>
    /// Find the nearest named colour to the given RGB by squared Euclidean distance.
    /// <paramref name="distance"/> is the straight-line RGB distance (0 = exact). Never throws.
    /// </summary>
    public static NamedColor Nearest(byte r, byte g, byte b, out double distance)
    {
        NamedColor best = _all.Length > 0 ? _all[0] : new NamedColor("—", 0, 0, 0);
        long bestSq = long.MaxValue;
        foreach (var c in _all)
        {
            long dr = c.R - r, dg = c.G - g, db = c.B - b;
            long sq = dr * dr + dg * dg + db * db;
            if (sq < bestSq) { bestSq = sq; best = c; if (sq == 0) break; }
        }
        distance = Math.Sqrt(bestSq < 0 ? 0 : bestSq);
        return best;
    }
}
