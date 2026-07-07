using System;

namespace WinForge.Services;

/// <summary>
/// 長寬比計算 · Aspect-ratio math — GCD-simplified ratios, ratio-preserving scaling,
/// megapixels. Pure managed, never throws (guards zero/negative). No redirect.
/// </summary>
public static class AspectRatioService
{
    /// <summary>Greatest common divisor (Euclid). Handles zero/negative gracefully.</summary>
    public static long Gcd(long a, long b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            long t = b;
            b = a % b;
            a = t;
        }
        return a == 0 ? 1 : a;
    }

    /// <summary>
    /// Simplify width×height to a whole-number ratio (e.g. 1920×1080 → 16:9).
    /// Returns false and 0:0 when either dimension is not positive.
    /// </summary>
    public static bool Simplify(double width, double height, out long rw, out long rh)
    {
        rw = 0;
        rh = 0;
        if (!IsPositive(width) || !IsPositive(height)) return false;

        long w = (long)Math.Round(width);
        long h = (long)Math.Round(height);
        if (w <= 0 || h <= 0) return false;

        long g = Gcd(w, h);
        rw = w / g;
        rh = h / g;
        return true;
    }

    /// <summary>Decimal ratio (width / height). Returns NaN when invalid.</summary>
    public static double DecimalRatio(double width, double height)
        => (!IsPositive(width) || !IsPositive(height)) ? double.NaN : width / height;

    /// <summary>Megapixels = width × height / 1,000,000. Returns NaN when invalid.</summary>
    public static double Megapixels(double width, double height)
        => (!IsPositive(width) || !IsPositive(height)) ? double.NaN : (width * height) / 1_000_000.0;

    /// <summary>Given a ratio (w:h) and a known width, compute the matching height. NaN when invalid.</summary>
    public static double HeightForWidth(double ratioW, double ratioH, double width)
        => (!IsPositive(ratioW) || !IsPositive(ratioH) || !IsPositive(width)) ? double.NaN : width * ratioH / ratioW;

    /// <summary>Given a ratio (w:h) and a known height, compute the matching width. NaN when invalid.</summary>
    public static double WidthForHeight(double ratioW, double ratioH, double height)
        => (!IsPositive(ratioW) || !IsPositive(ratioH) || !IsPositive(height)) ? double.NaN : height * ratioW / ratioH;

    private static bool IsPositive(double v) => !double.IsNaN(v) && !double.IsInfinity(v) && v > 0;
}
