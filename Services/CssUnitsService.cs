using System;
using System.Collections.Generic;
using System.Globalization;

namespace WinForge.Services;

/// <summary>
/// CSS 單位換算 · CSS unit converter — pure managed maths, never throws.
/// Absolute units resolve against the CSS 96-DPI reference (1in = 96px, 1pt = 1/72in,
/// 1pc = 12pt, 1cm = 96/2.54px, 1mm = 1/10cm). Relative units (em/rem/%/vw/vh) resolve
/// against the supplied context (root font-size, element font-size, viewport, container).
/// </summary>
public static class CssUnitsService
{
    /// <summary>The units this converter understands, in display order.</summary>
    public static readonly string[] Units = { "px", "em", "rem", "pt", "pc", "%", "vw", "vh", "cm", "mm", "in" };

    // CSS reference: absolute units expressed in px at 96 DPI.
    private const double PxPerIn = 96.0;
    private const double PxPerPt = PxPerIn / 72.0;      // 1pt = 1/72 in
    private const double PxPerPc = PxPerPt * 12.0;      // 1pc = 12 pt
    private const double PxPerCm = PxPerIn / 2.54;      // 2.54 cm = 1 in
    private const double PxPerMm = PxPerCm / 10.0;      // 10 mm = 1 cm

    /// <summary>Context values (all in px) used to resolve relative units. Safe defaults.</summary>
    public sealed class Context
    {
        public double RootFontPx = 16;      // for rem / %-of-font fallbacks
        public double ElementFontPx = 16;   // for em
        public double ViewportWidthPx = 1920;   // for vw
        public double ViewportHeightPx = 1080;  // for vh
        public double ContainerPx = 1000;   // for % (of container)
    }

    /// <summary>A single converted result row (bound in the ListView).</summary>
    public sealed class Result
    {
        public string Unit { get; set; } = "";
        public string Value { get; set; } = "";
        public string Combined { get; set; } = ""; // e.g. "12.5rem" — copied on click
    }

    /// <summary>Convert a raw px length into <paramref name="toUnit"/> using the context.
    /// Returns NaN when the target is meaningless (e.g. a zero-sized context).</summary>
    public static double FromPx(double px, string toUnit, Context ctx)
    {
        if (double.IsNaN(px) || double.IsInfinity(px)) return double.NaN;
        try
        {
            switch (toUnit)
            {
                case "px": return px;
                case "in": return px / PxPerIn;
                case "pt": return px / PxPerPt;
                case "pc": return px / PxPerPc;
                case "cm": return px / PxPerCm;
                case "mm": return px / PxPerMm;
                case "em": return Safe(px, ctx.ElementFontPx);
                case "rem": return Safe(px, ctx.RootFontPx);
                case "%": return Safe(px, ctx.ContainerPx) * 100.0;
                case "vw": return Safe(px, ctx.ViewportWidthPx) * 100.0;
                case "vh": return Safe(px, ctx.ViewportHeightPx) * 100.0;
                default: return double.NaN;
            }
        }
        catch { return double.NaN; }
    }

    /// <summary>Convert a value in <paramref name="fromUnit"/> into an absolute px length.</summary>
    public static double ToPx(double value, string fromUnit, Context ctx)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return double.NaN;
        try
        {
            switch (fromUnit)
            {
                case "px": return value;
                case "in": return value * PxPerIn;
                case "pt": return value * PxPerPt;
                case "pc": return value * PxPerPc;
                case "cm": return value * PxPerCm;
                case "mm": return value * PxPerMm;
                case "em": return value * ctx.ElementFontPx;
                case "rem": return value * ctx.RootFontPx;
                case "%": return value / 100.0 * ctx.ContainerPx;
                case "vw": return value / 100.0 * ctx.ViewportWidthPx;
                case "vh": return value / 100.0 * ctx.ViewportHeightPx;
                default: return double.NaN;
            }
        }
        catch { return double.NaN; }
    }

    private static double Safe(double px, double denom) =>
        (denom > 0 && !double.IsNaN(denom)) ? px / denom : double.NaN;

    /// <summary>Full conversion: value+unit → every other unit. Never throws; returns an
    /// empty list on bad input. Excludes the source unit from the results.</summary>
    public static List<Result> ConvertAll(double value, string fromUnit, Context ctx)
    {
        var list = new List<Result>();
        try
        {
            if (ctx == null) ctx = new Context();
            if (string.IsNullOrEmpty(fromUnit)) return list;
            double px = ToPx(value, fromUnit, ctx);
            foreach (var u in Units)
            {
                if (u == fromUnit) continue;
                double converted = FromPx(px, u, ctx);
                string shown = Format(converted);
                list.Add(new Result
                {
                    Unit = u,
                    Value = double.IsNaN(converted) ? "—" : shown,
                    Combined = double.IsNaN(converted) ? "" : shown + u,
                });
            }
        }
        catch { /* never throws */ }
        return list;
    }

    /// <summary>Parse a user string into a double; NaN when blank/invalid.</summary>
    public static double Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return double.NaN;
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : double.NaN;
    }

    /// <summary>Trim a double to at most 4 decimals, no trailing zeros.</summary>
    private static string Format(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return "—";
        double rounded = Math.Round(v, 4, MidpointRounding.AwayFromZero);
        return rounded.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
