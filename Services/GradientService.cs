using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// CSS 漸變產生器 · CSS gradient generator — pure managed helpers to validate colour
/// stops, emit the CSS string and compute geometry. No side effects, never throws.
/// </summary>
public static class GradientService
{
    public enum GradientKind { Linear, Radial }

    /// <summary>A single colour stop: 6-digit hex (no #) plus a 0–100 position.</summary>
    public readonly record struct Stop(byte R, byte G, byte B, double Position);

    /// <summary>Parse a hex colour like "#ff0000", "ff0000", "#f00" or "f00". Returns false on junk.</summary>
    public static bool TryParseHex(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var s = hex.Trim();
        if (s.StartsWith('#')) s = s[1..];
        s = s.Trim();
        try
        {
            if (s.Length == 3)
            {
                if (!IsHex(s)) return false;
                r = (byte)(Nib(s[0]) * 17);
                g = (byte)(Nib(s[1]) * 17);
                b = (byte)(Nib(s[2]) * 17);
                return true;
            }
            if (s.Length == 6)
            {
                if (!IsHex(s)) return false;
                r = (byte)((Nib(s[0]) << 4) | Nib(s[1]));
                g = (byte)((Nib(s[2]) << 4) | Nib(s[3]));
                b = (byte)((Nib(s[4]) << 4) | Nib(s[5]));
                return true;
            }
        }
        catch { }
        return false;
    }

    private static bool IsHex(string s)
    {
        foreach (var c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    private static int Nib(char c) => Convert.ToInt32(c.ToString(), 16);

    /// <summary>Normalise a hex string to canonical lower-case "#rrggbb", or null if invalid.</summary>
    public static string? Canonical(string? hex)
        => TryParseHex(hex, out var r, out var g, out var b) ? ToHex(r, g, b) : null;

    public static string ToHex(byte r, byte g, byte b)
        => "#" + r.ToString("x2", CultureInfo.InvariantCulture)
               + g.ToString("x2", CultureInfo.InvariantCulture)
               + b.ToString("x2", CultureInfo.InvariantCulture);

    /// <summary>Build the CSS declaration string. Never throws; empty stops -> empty gradient.</summary>
    public static string BuildCss(GradientKind kind, double angleDeg, IReadOnlyList<Stop> stops)
    {
        try
        {
            var sb = new StringBuilder();
            sb.Append("background: ");
            if (kind == GradientKind.Linear)
            {
                var a = ((angleDeg % 360) + 360) % 360;
                sb.Append("linear-gradient(")
                  .Append(a.ToString("0.##", CultureInfo.InvariantCulture)).Append("deg");
            }
            else
            {
                sb.Append("radial-gradient(circle");
            }
            if (stops != null)
            {
                foreach (var s in stops)
                {
                    var pos = Math.Clamp(s.Position, 0, 100);
                    sb.Append(", ")
                      .Append(ToHex(s.R, s.G, s.B)).Append(' ')
                      .Append(pos.ToString("0.##", CultureInfo.InvariantCulture)).Append('%');
                }
            }
            sb.Append(");");
            return sb.ToString();
        }
        catch
        {
            return "background: none;";
        }
    }

    /// <summary>
    /// Convert a CSS angle (0deg = up, clockwise) into unit start/end points in the
    /// WinUI [0,1] gradient space (origin top-left, y down). Used for LinearGradientBrush.
    /// </summary>
    public static (double sx, double sy, double ex, double ey) AnglePoints(double angleDeg)
    {
        try
        {
            var a = (((angleDeg % 360) + 360) % 360) * Math.PI / 180.0;
            // CSS: 0deg points up; direction vector in y-down space.
            var dx = Math.Sin(a);
            var dy = -Math.Cos(a);
            var sx = 0.5 - dx * 0.5;
            var sy = 0.5 - dy * 0.5;
            var ex = 0.5 + dx * 0.5;
            var ey = 0.5 + dy * 0.5;
            return (sx, sy, ex, ey);
        }
        catch
        {
            return (0, 0.5, 1, 0.5);
        }
    }

    /// <summary>Cryptographically-strong random byte 0–255.</summary>
    public static byte RandomByte()
    {
        var buf = new byte[1];
        RandomNumberGenerator.Fill(buf);
        return buf[0];
    }

    /// <summary>Random integer in [min, max] inclusive.</summary>
    public static int RandomInt(int min, int max)
    {
        if (max <= min) return min;
        return RandomNumberGenerator.GetInt32(min, max + 1);
    }
}
