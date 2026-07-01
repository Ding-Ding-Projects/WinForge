using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 時區會議規劃 · Timezone meeting planner — pure managed <see cref="TimeZoneInfo"/> helpers.
/// Every lookup is guarded so a bad/renamed zone id never throws. No redirect, no external process.
/// </summary>
public static class TzPlannerService
{
    /// <summary>All system time zones, ordered by UTC offset then display name. Never throws.</summary>
    public static IReadOnlyList<TimeZoneInfo> AllZones()
    {
        try
        {
            return TimeZoneInfo.GetSystemTimeZones()
                .OrderBy(z => z.BaseUtcOffset)
                .ThenBy(z => z.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            // Fall back to at least UTC + local so the UI still works.
            var list = new List<TimeZoneInfo> { TimeZoneInfo.Utc };
            try { if (TimeZoneInfo.Local.Id != TimeZoneInfo.Utc.Id) list.Add(TimeZoneInfo.Local); } catch { }
            return list;
        }
    }

    /// <summary>Best-effort zone lookup by id; returns null instead of throwing.</summary>
    public static TimeZoneInfo? FindZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return null; }
    }

    /// <summary>The local <see cref="DateTime"/> in <paramref name="zone"/> for a reference instant.</summary>
    /// <param name="referenceLocal">The reference wall-clock time as entered by the user.</param>
    /// <param name="referenceZone">The zone that <paramref name="referenceLocal"/> is expressed in.</param>
    public static DateTime LocalTimeIn(DateTime referenceLocal, TimeZoneInfo referenceZone, TimeZoneInfo zone)
    {
        try
        {
            var unspecified = DateTime.SpecifyKind(referenceLocal, DateTimeKind.Unspecified);
            var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, referenceZone);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        }
        catch
        {
            return referenceLocal; // degrade gracefully
        }
    }

    /// <summary>UTC offset for a zone at a given local instant (honours DST). Never throws.</summary>
    public static TimeSpan OffsetAt(DateTime localInZone, TimeZoneInfo zone)
    {
        try { return zone.GetUtcOffset(DateTime.SpecifyKind(localInZone, DateTimeKind.Unspecified)); }
        catch { try { return zone.BaseUtcOffset; } catch { return TimeSpan.Zero; } }
    }

    /// <summary>Format a UTC offset like "UTC+08:00" / "UTC-05:30".</summary>
    public static string FormatOffset(TimeSpan offset)
    {
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var abs = offset.Duration();
        return $"UTC{sign}{abs.Hours:00}:{abs.Minutes:00}";
    }

    public enum HoursState { InHours, EdgeHours, Night }

    /// <summary>
    /// Classify a local hour against working hours. In-hours = [start,end); one hour either side = edge;
    /// otherwise night. Handles start&gt;=end defensively by treating it as a full day in-hours.
    /// </summary>
    public static HoursState Classify(DateTime local, int startHour, int endHour)
    {
        int s = Math.Clamp(startHour, 0, 23);
        int e = Math.Clamp(endHour, 1, 24);
        if (s >= e) return HoursState.InHours;
        double h = local.Hour + local.Minute / 60.0;
        if (h >= s && h < e) return HoursState.InHours;
        if (h >= s - 1 && h < e + 1) return HoursState.EdgeHours;
        return HoursState.Night;
    }
}
