using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// 時長計算器 · Duration calculator — pure managed parsing/formatting of TimeSpan values from flexible
/// text formats ("1:30:00", "90m", "1h30m", "1d 2h", "45s", "2.5h"). Never throws; callers get a bool + reason.
/// </summary>
public static class DurationCalcService
{
    // e.g. "1d 2h 30m 15s", "2.5h", "90m", "45s" — one or more <number><unit> tokens.
    private static readonly Regex UnitToken = new(
        @"(?<num>[0-9]*\.?[0-9]+)\s*(?<unit>d|day|days|h|hr|hrs|hour|hours|m|min|mins|minute|minutes|s|sec|secs|second|seconds)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parse a flexible duration string into a <see cref="TimeSpan"/>. Supports colon clock form
    /// ("1:30:00" = h:m:s, "30:00" = m:s) and unit form ("1h30m", "2.5h", "90m", "1d 2h"). Never throws.
    /// </summary>
    public static bool TryParse(string? input, out TimeSpan result, out string errorEn, out string errorZh)
    {
        result = TimeSpan.Zero;
        errorEn = errorZh = string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            errorEn = "Empty duration.";
            errorZh = "時長係空嘅。";
            return false;
        }

        var text = input.Trim();
        bool negative = false;
        if (text.StartsWith('-')) { negative = true; text = text[1..].TrimStart(); }
        else if (text.StartsWith('+')) { text = text[1..].TrimStart(); }

        try
        {
            // Colon clock form: 2+ numeric segments separated by ':'.
            if (text.Contains(':') && Regex.IsMatch(text, @"^[0-9]+(\.[0-9]+)?(:[0-9]+(\.[0-9]+)?){1,3}$"))
            {
                if (TryParseColon(text, out result))
                {
                    if (negative) result = result.Negate();
                    return true;
                }
                errorEn = "Could not read clock format (use h:mm:ss).";
                errorZh = "睇唔明時鐘格式（用 h:mm:ss）。";
                return false;
            }

            // Bare number = minutes (matches the NumberBox convention elsewhere).
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var bareMinutes))
            {
                result = TimeSpan.FromMinutes(bareMinutes);
                if (negative) result = result.Negate();
                return true;
            }

            // Unit form: sum every <number><unit> token; reject leftover junk.
            double totalSeconds = 0;
            bool matchedAny = false;
            var consumed = new StringBuilder(text);
            foreach (Match mm in UnitToken.Matches(text))
            {
                matchedAny = true;
                var num = double.Parse(mm.Groups["num"].Value, CultureInfo.InvariantCulture);
                totalSeconds += num * UnitSeconds(mm.Groups["unit"].Value);
                consumed.Replace(mm.Value, string.Empty);
            }

            if (!matchedAny)
            {
                errorEn = "Unrecognized duration format.";
                errorZh = "認唔到嘅時長格式。";
                return false;
            }

            // Anything left that isn't whitespace/separators means the input was malformed.
            var leftover = Regex.Replace(consumed.ToString(), @"[\s,;]+", string.Empty);
            if (leftover.Length > 0)
            {
                errorEn = $"Couldn't understand \"{leftover}\".";
                errorZh = $"睇唔明「{leftover}」。";
                return false;
            }

            result = TimeSpan.FromSeconds(totalSeconds);
            if (negative) result = result.Negate();
            return true;
        }
        catch (Exception ex)
        {
            errorEn = "Duration out of range or invalid.";
            errorZh = "時長超出範圍或者無效。";
            _ = ex;
            return false;
        }
    }

    private static bool TryParseColon(string text, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        var parts = text.Split(':');
        double seconds = 0;
        try
        {
            switch (parts.Length)
            {
                case 2: // m:s
                    seconds = double.Parse(parts[0], CultureInfo.InvariantCulture) * 60
                            + double.Parse(parts[1], CultureInfo.InvariantCulture);
                    break;
                case 3: // h:m:s
                    seconds = double.Parse(parts[0], CultureInfo.InvariantCulture) * 3600
                            + double.Parse(parts[1], CultureInfo.InvariantCulture) * 60
                            + double.Parse(parts[2], CultureInfo.InvariantCulture);
                    break;
                case 4: // d:h:m:s
                    seconds = double.Parse(parts[0], CultureInfo.InvariantCulture) * 86400
                            + double.Parse(parts[1], CultureInfo.InvariantCulture) * 3600
                            + double.Parse(parts[2], CultureInfo.InvariantCulture) * 60
                            + double.Parse(parts[3], CultureInfo.InvariantCulture);
                    break;
                default:
                    return false;
            }
        }
        catch
        {
            return false;
        }
        result = TimeSpan.FromSeconds(seconds);
        return true;
    }

    private static double UnitSeconds(string unit) => unit.ToLowerInvariant() switch
    {
        "d" or "day" or "days" => 86400,
        "h" or "hr" or "hrs" or "hour" or "hours" => 3600,
        "m" or "min" or "mins" or "minute" or "minutes" => 60,
        _ => 1, // seconds
    };

    /// <summary>Format as d:hh:mm:ss (days shown only when non-zero). Handles negative spans.</summary>
    public static string FormatClock(TimeSpan ts)
    {
        var sign = ts < TimeSpan.Zero ? "-" : string.Empty;
        var a = ts < TimeSpan.Zero ? ts.Negate() : ts;
        int days = (int)a.TotalDays;
        return days > 0
            ? $"{sign}{days}:{a.Hours:00}:{a.Minutes:00}:{a.Seconds:00}"
            : $"{sign}{a.Hours:00}:{a.Minutes:00}:{a.Seconds:00}";
    }

    /// <summary>Compact unit summary, e.g. "1d 2h 30m 15s". Returns "0s" for zero.</summary>
    public static string FormatUnits(TimeSpan ts)
    {
        var sign = ts < TimeSpan.Zero ? "-" : string.Empty;
        var a = ts < TimeSpan.Zero ? ts.Negate() : ts;
        var parts = new List<string>();
        int days = (int)a.TotalDays;
        if (days > 0) parts.Add($"{days}d");
        if (a.Hours > 0) parts.Add($"{a.Hours}h");
        if (a.Minutes > 0) parts.Add($"{a.Minutes}m");
        if (a.Seconds > 0 || parts.Count == 0) parts.Add($"{a.Seconds}s");
        return sign + string.Join(" ", parts);
    }

    private static string Dec(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    public static string DecimalSeconds(TimeSpan ts) => Dec(ts.TotalSeconds);
    public static string DecimalMinutes(TimeSpan ts) => Dec(ts.TotalMinutes);
    public static string DecimalHours(TimeSpan ts) => Dec(ts.TotalHours);
    public static string DecimalDays(TimeSpan ts) => Dec(ts.TotalDays);
}
