using System;

namespace WinForge.Services;

/// <summary>
/// Unix 時間戳 / 紀元轉換器 · Epoch (Unix timestamp) conversion helpers. Pure managed, no side-effects.
/// Wraps DateTimeOffset.FromUnixTime* for epoch→human and ToUnixTime* for human→epoch,
/// plus a relative-time phrase generator. All formatting/localization stays in the page.
/// </summary>
public static class EpochService
{
    /// <summary>Current Unix time in seconds.</summary>
    public static long NowSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>Current Unix time in milliseconds.</summary>
    public static long NowMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// <summary>
    /// Convert an epoch value (seconds or milliseconds) to a <see cref="DateTimeOffset"/> (UTC).
    /// Returns false on out-of-range or overflow rather than throwing.
    /// </summary>
    public static bool TryFromEpoch(long value, bool milliseconds, out DateTimeOffset result)
    {
        try
        {
            result = milliseconds
                ? DateTimeOffset.FromUnixTimeMilliseconds(value)
                : DateTimeOffset.FromUnixTimeSeconds(value);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            return false;
        }
    }

    /// <summary>Unix seconds for a given instant.</summary>
    public static long ToSeconds(DateTimeOffset when) => when.ToUnixTimeSeconds();

    /// <summary>Unix milliseconds for a given instant.</summary>
    public static long ToMilliseconds(DateTimeOffset when) => when.ToUnixTimeMilliseconds();

    /// <summary>
    /// A rough relative phrase between <paramref name="when"/> and now, in both languages.
    /// e.g. "3 days ago" / "3 日前" or "in 5 hours" / "5 小時後".
    /// </summary>
    public static (string en, string zh) Relative(DateTimeOffset when)
    {
        TimeSpan delta = when - DateTimeOffset.UtcNow;
        bool future = delta.Ticks >= 0;
        TimeSpan abs = delta.Duration();

        double seconds = abs.TotalSeconds;
        double minutes = abs.TotalMinutes;
        double hours = abs.TotalHours;
        double days = abs.TotalDays;

        string enUnit, zhUnit;
        long n;

        if (seconds < 60) { n = (long)Math.Round(seconds); enUnit = n == 1 ? "second" : "seconds"; zhUnit = "秒"; }
        else if (minutes < 60) { n = (long)Math.Round(minutes); enUnit = n == 1 ? "minute" : "minutes"; zhUnit = "分鐘"; }
        else if (hours < 24) { n = (long)Math.Round(hours); enUnit = n == 1 ? "hour" : "hours"; zhUnit = "小時"; }
        else if (days < 30) { n = (long)Math.Round(days); enUnit = n == 1 ? "day" : "days"; zhUnit = "日"; }
        else if (days < 365) { n = (long)Math.Round(days / 30); enUnit = n == 1 ? "month" : "months"; zhUnit = "個月"; }
        else { n = (long)Math.Round(days / 365); enUnit = n == 1 ? "year" : "years"; zhUnit = "年"; }

        if (n == 0)
            return ("just now", "啱啱");

        string en = future ? $"in {n} {enUnit}" : $"{n} {enUnit} ago";
        string zh = future ? $"{n} {zhUnit}後" : $"{n} {zhUnit}前";
        return (en, zh);
    }
}
