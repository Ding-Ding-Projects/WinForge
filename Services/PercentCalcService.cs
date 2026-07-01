using System;
using System.Globalization;

namespace WinForge.Services;

/// <summary>
/// 百分比 / 比例計算器 · Percentage & ratio calculator engine. Pure managed, never-throw.
/// Each method returns a small result struct with Ok + a formatted value; callers surface
/// bilingual status. No I/O, no side-effects.
/// </summary>
public static class PercentCalcService
{
    /// <summary>Outcome of one calculation. <see cref="Ok"/> false means the inputs were bad.</summary>
    public readonly struct CalcResult
    {
        public bool Ok { get; }
        /// <summary>Formatted primary value for display / copy (empty when not Ok).</summary>
        public string Value { get; }
        /// <summary>Raw numeric value (NaN when not Ok).</summary>
        public double Number { get; }

        private CalcResult(bool ok, string value, double number)
        {
            Ok = ok; Value = value; Number = number;
        }

        public static CalcResult Fail() => new(false, "", double.NaN);
        public static CalcResult FromNumber(double n) => new(true, Fmt(n), n);
        public static CalcResult FromText(double n, string text) => new(true, text, n);
    }

    /// <summary>Parse a user string as a number. Accepts leading/trailing spaces, a trailing '%'.
    /// Returns false on empty / non-numeric / infinite input.</summary>
    public static bool TryParse(string? raw, out double value)
    {
        value = double.NaN;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        string s = raw.Trim();
        if (s.EndsWith("%", StringComparison.Ordinal)) s = s[..^1].Trim();
        if (!double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value) &&
            !double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            value = double.NaN;
            return false;
        }
        if (double.IsNaN(value) || double.IsInfinity(value)) { value = double.NaN; return false; }
        return true;
    }

    /// <summary>Format a number trimly (up to 6 decimals, no trailing zeros).</summary>
    public static string Fmt(double n)
    {
        if (double.IsNaN(n) || double.IsInfinity(n)) return "—";
        double r = Math.Round(n, 6, MidpointRounding.AwayFromZero);
        if (r == 0d) r = 0d; // normalize -0
        return r.ToString("0.######", CultureInfo.CurrentCulture);
    }

    // ---- X% of Y ----
    public static CalcResult PercentOf(string? x, string? y)
    {
        if (!TryParse(x, out double px) || !TryParse(y, out double vy)) return CalcResult.Fail();
        return CalcResult.FromNumber(px / 100d * vy);
    }

    // ---- X is what % of Y ----
    public static CalcResult WhatPercent(string? x, string? y)
    {
        if (!TryParse(x, out double vx) || !TryParse(y, out double vy)) return CalcResult.Fail();
        if (vy == 0d) return CalcResult.Fail(); // div-by-zero
        return CalcResult.FromText(vx / vy * 100d, Fmt(vx / vy * 100d) + "%");
    }

    // ---- % change from A to B ----
    public static CalcResult PercentChange(string? a, string? b)
    {
        if (!TryParse(a, out double va) || !TryParse(b, out double vb)) return CalcResult.Fail();
        if (va == 0d) return CalcResult.Fail(); // undefined base
        double pct = (vb - va) / Math.Abs(va) * 100d;
        string sign = pct > 0 ? "+" : "";
        return CalcResult.FromText(pct, sign + Fmt(pct) + "%");
    }

    // ---- increase / decrease Y by X% ----
    public static CalcResult AdjustBy(string? y, string? x, bool increase)
    {
        if (!TryParse(y, out double vy) || !TryParse(x, out double px)) return CalcResult.Fail();
        double factor = increase ? 1d + px / 100d : 1d - px / 100d;
        return CalcResult.FromNumber(vy * factor);
    }

    /// <summary>Tip calculator result.</summary>
    public readonly struct TipResult
    {
        public bool Ok { get; }
        public double TipAmount { get; }
        public double Total { get; }
        public double PerPerson { get; }
        private TipResult(bool ok, double tip, double total, double per)
        { Ok = ok; TipAmount = tip; Total = total; PerPerson = per; }
        public static TipResult Fail() => new(false, 0, 0, 0);
        public static TipResult From(double tip, double total, double per) => new(true, tip, total, per);
    }

    // ---- tip: bill + tip% + split N → per-person ----
    public static TipResult Tip(string? bill, string? tipPct, string? split)
    {
        if (!TryParse(bill, out double vb) || !TryParse(tipPct, out double vt)) return TipResult.Fail();
        if (!TryParse(split, out double vn)) return TipResult.Fail();
        int n = (int)Math.Round(vn);
        if (n < 1) return TipResult.Fail(); // must split among ≥1
        if (vb < 0 || vt < 0) return TipResult.Fail();
        double tip = vb * vt / 100d;
        double total = vb + tip;
        return TipResult.From(tip, total, total / n);
    }

    /// <summary>Ratio-simplify result.</summary>
    public readonly struct RatioResult
    {
        public bool Ok { get; }
        public long A { get; }
        public long B { get; }
        private RatioResult(bool ok, long a, long b) { Ok = ok; A = a; B = b; }
        public static RatioResult Fail() => new(false, 0, 0);
        public static RatioResult From(long a, long b) => new(true, a, b);
    }

    // ---- ratio simplify a:b → simplest form via GCD ----
    public static RatioResult SimplifyRatio(string? a, string? b)
    {
        if (!TryParse(a, out double da) || !TryParse(b, out double db)) return RatioResult.Fail();
        // Scale decimals to integers (up to 6 places), then reduce.
        long la = ToScaledLong(da, out int sa);
        long lb = ToScaledLong(db, out int sb);
        int scale = Math.Max(sa, sb);
        la = (long)Math.Round(da * Pow10(scale));
        lb = (long)Math.Round(db * Pow10(scale));
        if (la == 0 && lb == 0) return RatioResult.Fail();
        long g = Gcd(Math.Abs(la), Math.Abs(lb));
        if (g == 0) g = 1;
        return RatioResult.From(la / g, lb / g);
    }

    private static long ToScaledLong(double d, out int decimals)
    {
        decimals = 0;
        double frac = Math.Abs(d - Math.Truncate(d));
        while (decimals < 6 && frac > 1e-9)
        {
            d *= 10; decimals++;
            frac = Math.Abs(d - Math.Truncate(d));
        }
        return (long)Math.Round(d);
    }

    private static double Pow10(int n)
    {
        double r = 1; for (int i = 0; i < n; i++) r *= 10; return r;
    }

    private static long Gcd(long a, long b)
    {
        while (b != 0) { (a, b) = (b, a % b); }
        return a;
    }
}
