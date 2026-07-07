using System;

namespace WinForge.Services;

/// <summary>
/// 計時器 / 碼錶 / 番茄鐘純輔助函數 · Pure helpers for the Timer, Stopwatch &amp; Pomodoro module.
/// Stateless formatting only; all live state lives in the page. No side-effects, never throws.
/// </summary>
public static class TimerService
{
    /// <summary>Format a duration as hh:mm:ss.ff (centiseconds) for the stopwatch display.</summary>
    public static string FormatStopwatch(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        long totalCs = (long)Math.Floor(t.TotalMilliseconds / 10.0);
        long cs = totalCs % 100;
        long totalSec = totalCs / 100;
        long s = totalSec % 60;
        long m = (totalSec / 60) % 60;
        long h = totalSec / 3600;
        return $"{h:00}:{m:00}:{s:00}.{cs:00}";
    }

    /// <summary>Format a whole-second remaining count as mm:ss (or hh:mm:ss past an hour).</summary>
    public static string FormatCountdown(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        int s = totalSeconds % 60;
        int m = (totalSeconds / 60) % 60;
        int h = totalSeconds / 3600;
        return h > 0 ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    /// <summary>Clamp an arbitrary (possibly NaN) NumberBox value into an int within [min,max].</summary>
    public static int ClampBox(double value, int min, int max)
    {
        if (double.IsNaN(value)) return min;
        int v = (int)Math.Round(value);
        if (v < min) v = min;
        if (v > max) v = max;
        return v;
    }
}
