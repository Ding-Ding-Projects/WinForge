using System;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 佔位圖產生器 · Placeholder-image generator. Pure managed C# — builds a self-contained SVG
/// (a coloured rectangle plus centred label) as a string, plus a base64 data URI and a
/// "picsum-style" reference URL. No remote fetch, no NuGet, no Process.Start. Never throws.
/// </summary>
public static class LoremImgService
{
    /// <summary>參數 · Inputs describing the placeholder to draw.</summary>
    public readonly record struct Spec(int Width, int Height, string BgHex, string FgHex, string Label, double FontSize);

    /// <summary>結果 · Everything a caller needs to display or copy.</summary>
    public readonly record struct Result(string Svg, string DataUri, string PicsumUrl, uint BgArgb, uint FgArgb);

    /// <summary>由 spec 產生所有輸出 · Build the SVG, data URI and reference URL for a spec. Never throws.</summary>
    public static Result Build(Spec spec)
    {
        int w = Clamp(spec.Width, 1, 10000, 640);
        int h = Clamp(spec.Height, 1, 10000, 480);
        string bg = NormalizeHex(spec.BgHex, "#DDDDDD");
        string fg = NormalizeHex(spec.FgHex, "#555555");
        double fontSize = spec.FontSize;
        if (double.IsNaN(fontSize) || double.IsInfinity(fontSize) || fontSize <= 0)
            fontSize = Math.Max(10, Math.Min(w, h) / 6.0);
        fontSize = Math.Clamp(fontSize, 1, 2000);

        string label = spec.Label;
        if (string.IsNullOrWhiteSpace(label)) label = w + "×" + h; // WxH
        label = label.Trim();

        string svg = BuildSvg(w, h, bg, fg, label, fontSize);
        string dataUri = BuildDataUri(svg);
        string picsum = "https://picsum.photos/" + w + "/" + h;

        return new Result(svg, dataUri, picsum, ToArgb(bg, 0xFFDDDDDD), ToArgb(fg, 0xFF555555));
    }

    /// <summary>SVG 原始碼 · Self-contained SVG source string.</summary>
    public static string BuildSvg(int w, int h, string bg, string fg, string label, double fontSize)
    {
        string fs = fontSize.ToString("0.##", CultureInfo.InvariantCulture);
        string safeLabel = XmlEscape(label);
        var sb = new StringBuilder(256);
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"").Append(w)
          .Append("\" height=\"").Append(h)
          .Append("\" viewBox=\"0 0 ").Append(w).Append(' ').Append(h).Append("\">\n");
        sb.Append("  <rect width=\"").Append(w).Append("\" height=\"").Append(h)
          .Append("\" fill=\"").Append(bg).Append("\"/>\n");
        sb.Append("  <text x=\"50%\" y=\"50%\" fill=\"").Append(fg)
          .Append("\" font-family=\"Segoe UI, Arial, sans-serif\" font-size=\"").Append(fs)
          .Append("\" font-weight=\"600\" text-anchor=\"middle\" dominant-baseline=\"central\">")
          .Append(safeLabel).Append("</text>\n");
        sb.Append("</svg>");
        return sb.ToString();
    }

    /// <summary>base64 data URI · data:image/svg+xml;base64,… encoding of an SVG string.</summary>
    public static string BuildDataUri(string svg)
    {
        try
        {
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(svg ?? string.Empty));
            return "data:image/svg+xml;base64," + b64;
        }
        catch { return "data:image/svg+xml;base64,"; }
    }

    // ===== helpers =====

    private static int Clamp(int v, int lo, int hi, int fallback)
    {
        if (v < lo || v > hi) return v < lo ? (v <= 0 ? fallback : lo) : hi;
        return v;
    }

    /// <summary>整理成 #RRGGBB · Normalize a user hex string to "#RRGGBB"; falls back on garbage.</summary>
    public static string NormalizeHex(string? hex, string fallback)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            var s = hex.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);
            if (s.Length == 3) // #abc -> #aabbcc
            {
                s = new string(new[] { s[0], s[0], s[1], s[1], s[2], s[2] });
            }
            if (s.Length == 8) s = s.Substring(2); // strip AA from AARRGGBB
            if (s.Length != 6) return fallback;
            foreach (var c in s)
                if (!Uri.IsHexDigit(c)) return fallback;
            return "#" + s.ToUpperInvariant();
        }
        catch { return fallback; }
    }

    /// <summary>#RRGGBB → 不透明 ARGB · Convert a normalized hex to an opaque ARGB uint for XAML brushes.</summary>
    public static uint ToArgb(string hex, uint fallback)
    {
        try
        {
            var s = (hex ?? "").TrimStart('#');
            if (s.Length != 6) return fallback;
            uint rgb = Convert.ToUInt32(s, 16);
            return 0xFF000000u | rgb;
        }
        catch { return fallback; }
    }

    private static string XmlEscape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&apos;"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
