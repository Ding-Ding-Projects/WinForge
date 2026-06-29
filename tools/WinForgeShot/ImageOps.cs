using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

// ─────────────────────────────────────────────────────────────────────────────
//  ImageOps · 截圖後製（Wiki 截圖工具）
//  Post-processing for wiki screenshots: crop, highlight call-outs, numbered
//  step badges, text annotations, arrows, ellipses, and redaction (box / blur /
//  pixelate) of personal info.
//
//  All operations work on an in-memory System.Drawing.Bitmap "canvas". Geometry
//  fields accept either absolute pixels (e.g. 120) or a percentage of the image
//  dimension (e.g. 35%). x/width resolve against image width; y/height against
//  image height. Colors accept names (red/green/cyan/amber/yellow/white/black/
//  magenta/orange/blue) or hex (#RGB, #RRGGBB, #AARRGGBB).
// ─────────────────────────────────────────────────────────────────────────────

static class ImageOps
{
    // WinForge reactor-green accent, used as the default call-out colour.
    public static readonly Color Accent = ColorTranslator.FromHtml("#3DDC84");
    public static readonly Color CallOut = ColorTranslator.FromHtml("#FF5252"); // attention red

    // ── geometry parsing ─────────────────────────────────────────────────────

    // Resolve one token ("120" or "35%") against a reference dimension.
    public static int Len(string token, int dim)
    {
        token = token.Trim();
        if (token.EndsWith("%"))
        {
            var p = double.Parse(token[..^1], CultureInfo.InvariantCulture);
            return (int)Math.Round(p / 100.0 * dim);
        }
        return (int)Math.Round(double.Parse(token, CultureInfo.InvariantCulture));
    }

    // Parse "x|y|w|h" into a Rectangle resolved against the bitmap.
    public static Rectangle Rect(Bitmap bmp, string[] f)
    {
        int x = Len(f[0], bmp.Width);
        int y = Len(f[1], bmp.Height);
        int w = Len(f[2], bmp.Width);
        int h = Len(f[3], bmp.Height);
        // clamp to the canvas so ops never throw on slightly-oversized regions
        if (x < 0) { w += x; x = 0; }
        if (y < 0) { h += y; y = 0; }
        if (x + w > bmp.Width) w = bmp.Width - x;
        if (y + h > bmp.Height) h = bmp.Height - y;
        return new Rectangle(x, y, Math.Max(1, w), Math.Max(1, h));
    }

    public static Color ParseColor(string s, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        s = s.Trim();
        try
        {
            if (s.StartsWith("#")) return ColorTranslator.FromHtml(s);
            return s.ToLowerInvariant() switch
            {
                "green" or "reactor" => Accent,
                "red" => CallOut,
                "cyan" => ColorTranslator.FromHtml("#22D3EE"),
                "amber" or "orange" => ColorTranslator.FromHtml("#FFB300"),
                "yellow" => ColorTranslator.FromHtml("#FFEB3B"),
                "blue" => ColorTranslator.FromHtml("#2196F3"),
                "magenta" or "pink" => ColorTranslator.FromHtml("#E91E63"),
                "white" => Color.White,
                "black" => Color.Black,
                _ => ColorTranslator.FromHtml(s),
            };
        }
        catch { return fallback; }
    }

    static Graphics Hi(Bitmap b)
    {
        var g = Graphics.FromImage(b);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        return g;
    }

    // ── transforms ───────────────────────────────────────────────────────────

    public static Bitmap Crop(Bitmap src, string[] f)
    {
        var r = Rect(src, f);
        var dst = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
        using var g = Hi(dst);
        g.DrawImage(src, new Rectangle(0, 0, r.Width, r.Height), r, GraphicsUnit.Pixel);
        return dst;
    }

    public static Bitmap Scale(Bitmap src, string token)
    {
        // token: a percentage ("50" / "50%") or "WIDTHpx" via "w:1200"
        int w, h;
        token = token.Trim();
        if (token.StartsWith("w:"))
        {
            w = int.Parse(token[2..], CultureInfo.InvariantCulture);
            h = (int)Math.Round(src.Height * (w / (double)src.Width));
        }
        else
        {
            var p = double.Parse(token.TrimEnd('%'), CultureInfo.InvariantCulture) / 100.0;
            w = Math.Max(1, (int)Math.Round(src.Width * p));
            h = Math.Max(1, (int)Math.Round(src.Height * p));
        }
        var dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Hi(dst);
        g.DrawImage(src, new Rectangle(0, 0, w, h));
        return dst;
    }

    // ── call-outs ────────────────────────────────────────────────────────────

    // Rounded highlight box with a soft outer glow — the primary "look at this" mark.
    public static void Highlight(Bitmap b, string[] f)
    {
        var r = Rect(b, f);
        var col = ParseColor(f.Length > 4 ? f[4] : "", CallOut);
        int thick = f.Length > 5 ? (int)Len(f[5], b.Width) : Math.Max(3, b.Width / 320);
        using var g = Hi(b);
        int radius = Math.Min(24, Math.Min(r.Width, r.Height) / 4);
        using var path = Rounded(r, radius);
        // glow
        for (int i = 3; i >= 1; i--)
        {
            using var glow = new Pen(Color.FromArgb(40, col), thick + i * 3) { LineJoin = LineJoin.Round };
            g.DrawPath(glow, path);
        }
        using var pen = new Pen(col, thick) { LineJoin = LineJoin.Round };
        g.DrawPath(pen, path);
    }

    // Plain rectangle outline.
    public static void Box(Bitmap b, string[] f)
    {
        var r = Rect(b, f);
        var col = ParseColor(f.Length > 4 ? f[4] : "", CallOut);
        int thick = f.Length > 5 ? (int)Len(f[5], b.Width) : Math.Max(3, b.Width / 360);
        using var g = Hi(b);
        using var pen = new Pen(col, thick);
        g.DrawRectangle(pen, r);
    }

    public static void Ellipse(Bitmap b, string[] f)
    {
        var r = Rect(b, f);
        var col = ParseColor(f.Length > 4 ? f[4] : "", CallOut);
        int thick = f.Length > 5 ? (int)Len(f[5], b.Width) : Math.Max(3, b.Width / 360);
        using var g = Hi(b);
        using var pen = new Pen(col, thick);
        g.DrawEllipse(pen, r);
    }

    public static void Arrow(Bitmap b, string[] f)
    {
        int x1 = Len(f[0], b.Width), y1 = Len(f[1], b.Height);
        int x2 = Len(f[2], b.Width), y2 = Len(f[3], b.Height);
        var col = ParseColor(f.Length > 4 ? f[4] : "", CallOut);
        int thick = f.Length > 5 ? (int)Len(f[5], b.Width) : Math.Max(4, b.Width / 300);
        using var g = Hi(b);
        using var pen = new Pen(col, thick) { EndCap = LineCap.Custom };
        pen.CustomEndCap = new AdjustableArrowCap(4, 5, true);
        g.DrawLine(pen, x1, y1, x2, y2);
    }

    // ── text & step badges ──────────────────────────────────────────────────

    // --text "x|y|message[|color|size|bg]"
    public static void Text(Bitmap b, string[] f)
    {
        int x = Len(f[0], b.Width), y = Len(f[1], b.Height);
        string msg = f.Length > 2 ? f[2] : "";
        var col = ParseColor(f.Length > 3 ? f[3] : "", Color.White);
        float size = f.Length > 4 && f[4].Length > 0
            ? float.Parse(f[4].TrimEnd('%'), CultureInfo.InvariantCulture)
            : Math.Max(14, b.Width / 60f);
        bool hasBg = f.Length > 5 && f[5].Length > 0;
        var bg = hasBg ? ParseColor(f[5], Color.FromArgb(200, 17, 17, 17)) : Color.Empty;
        using var g = Hi(b);
        using var font = NewFont(size);
        var sz = g.MeasureString(msg, font);
        if (hasBg)
        {
            var pad = size * 0.4f;
            var box = new RectangleF(x - pad, y - pad, sz.Width + pad * 2, sz.Height + pad * 2);
            using var bgBrush = new SolidBrush(Color.FromArgb(bg.A == 255 ? 210 : bg.A, bg));
            using var rp = Rounded(Rectangle.Round(box), 8);
            g.FillPath(bgBrush, rp);
        }
        else
        {
            // legibility shadow when drawn straight onto the screenshot
            using var sh = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            g.DrawString(msg, font, sh, x + 1.5f, y + 1.5f);
        }
        using var brush = new SolidBrush(col);
        g.DrawString(msg, font, brush, x, y);
    }

    // --step "x|y|number[|color|diameter]"  — filled circle with a centred number
    public static void Step(Bitmap b, string[] f)
    {
        int cx = Len(f[0], b.Width), cy = Len(f[1], b.Height);
        string num = f.Length > 2 ? f[2] : "1";
        var col = ParseColor(f.Length > 3 ? f[3] : "", CallOut);
        int dia = f.Length > 4 && f[4].Length > 0 ? Len(f[4], b.Width) : Math.Max(34, b.Width / 30);
        using var g = Hi(b);
        var circle = new Rectangle(cx - dia / 2, cy - dia / 2, dia, dia);
        using (var halo = new SolidBrush(Color.FromArgb(70, Color.Black)))
            g.FillEllipse(halo, circle.X - 2, circle.Y - 2, dia + 4, dia + 4);
        using (var fill = new SolidBrush(col)) g.FillEllipse(fill, circle);
        using (var ring = new Pen(Color.White, Math.Max(2, dia / 14f))) g.DrawEllipse(ring, circle);
        using var font = NewFont(dia * 0.5f, bold: true);
        using var tb = new SolidBrush(Color.White);
        var sz = g.MeasureString(num, font);
        g.DrawString(num, font, tb, cx - sz.Width / 2, cy - sz.Height / 2);
    }

    // ── redaction ─────────────────────────────────────────────────────────────

    // --redact "x|y|w|h[|mode]"  mode = box (default) | blur | pixelate
    public static void Redact(Bitmap b, string[] f)
    {
        var r = Rect(b, f);
        string mode = (f.Length > 4 ? f[4] : "box").Trim().ToLowerInvariant();
        switch (mode)
        {
            case "blur": Blur(b, r); break;
            case "pixelate": case "pixel": case "mosaic": Pixelate(b, r); break;
            default: BlackBox(b, r); break;
        }
    }

    static void BlackBox(Bitmap b, Rectangle r)
    {
        using var g = Hi(b);
        using var fill = new SolidBrush(Color.FromArgb(255, 20, 20, 22));
        g.FillRectangle(fill, r);
        // subtle hatch so it reads as "intentionally redacted"
        using var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1);
        for (int x = r.Left; x < r.Right; x += 8) g.DrawLine(pen, x, r.Top, x - r.Height, r.Bottom);
    }

    static void Pixelate(Bitmap b, Rectangle r, int block = 0)
    {
        if (block <= 0) block = Math.Max(8, Math.Min(r.Width, r.Height) / 6);
        using var g = Hi(b);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        int sw = Math.Max(1, r.Width / block), sh = Math.Max(1, r.Height / block);
        using var small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
        using (var gs = Graphics.FromImage(small))
        {
            gs.InterpolationMode = InterpolationMode.HighQualityBilinear;
            gs.DrawImage(b, new Rectangle(0, 0, sw, sh), r, GraphicsUnit.Pixel);
        }
        g.DrawImage(small, r, new Rectangle(0, 0, sw, sh), GraphicsUnit.Pixel);
    }

    // Cheap, strong box blur via heavy down/up-sample (irreversible — good for redaction).
    static void Blur(Bitmap b, Rectangle r)
    {
        int sw = Math.Max(1, r.Width / 18), sh = Math.Max(1, r.Height / 18);
        using var small = new Bitmap(sw, sh, PixelFormat.Format32bppArgb);
        using (var gs = Graphics.FromImage(small))
        {
            gs.InterpolationMode = InterpolationMode.HighQualityBilinear;
            gs.DrawImage(b, new Rectangle(0, 0, sw, sh), r, GraphicsUnit.Pixel);
        }
        using var g = Hi(b);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(small, r, new Rectangle(0, 0, sw, sh), GraphicsUnit.Pixel);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    static GraphicsPath Rounded(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        if (radius <= 0) { p.AddRectangle(r); return p; }
        int d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static Font NewFont(float size, bool bold = false)
    {
        // Prefer the app's design font; fall back gracefully.
        foreach (var name in new[] { "Segoe UI", "Arial" })
        {
            try { return new Font(name, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel); }
            catch { }
        }
        return new Font(FontFamily.GenericSansSerif, size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    }
}
