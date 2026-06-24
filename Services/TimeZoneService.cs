using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 世界時鐘 / 時區換算 · World-clock & timezone conversion. Pure OS data — enumerates
/// TimeZoneInfo.GetSystemTimeZones(), converts with TimeZoneInfo.ConvertTimeBySystemTimeZoneId,
/// and reads the machine's current zone. No network — zones come from the OS ICU/registry data.
/// </summary>
public static class TimeZoneService
{
    /// <summary>Every system time zone, ordered by UTC offset then display name.</summary>
    public static IReadOnlyList<TimeZoneInfo> All { get; } =
        TimeZoneInfo.GetSystemTimeZones()
            .OrderBy(z => z.BaseUtcOffset)
            .ThenBy(z => z.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>The machine's current local time zone (same as <c>tzutil /g</c>).</summary>
    public static TimeZoneInfo Local => TimeZoneInfo.Local;

    /// <summary>Look up a zone by its system id; null if not found.</summary>
    public static TimeZoneInfo? Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return null; }
    }

    /// <summary>Current wall-clock time in the given zone.</summary>
    public static DateTimeOffset Now(TimeZoneInfo zone)
        => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);

    /// <summary>
    /// Convert <paramref name="source"/> (interpreted in <paramref name="fromId"/>) into
    /// <paramref name="toId"/> via <see cref="TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime,string,string)"/>.
    /// </summary>
    public static DateTime Convert(DateTime source, string fromId, string toId)
    {
        var unspecified = DateTime.SpecifyKind(source, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeBySystemTimeZoneId(unspecified, fromId, toId);
    }

    /// <summary>Short label like "UTC+08:00" for a zone's current offset (DST-aware).</summary>
    public static string OffsetLabel(TimeZoneInfo zone)
    {
        var off = zone.GetUtcOffset(DateTimeOffset.UtcNow);
        var sign = off < TimeSpan.Zero ? "-" : "+";
        var abs = off.Duration();
        return $"UTC{sign}{abs.Hours:00}:{abs.Minutes:00}";
    }

    /// <summary>True if the zone is currently observing daylight saving time.</summary>
    public static bool IsDst(TimeZoneInfo zone)
        => zone.IsDaylightSavingTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);

    /// <summary>A sensible starting set of world-clock cities, filtered to zones this OS actually has.</summary>
    public static IReadOnlyList<string> DefaultBoardIds { get; } = new[]
    {
        "Hawaiian Standard Time",       // Honolulu
        "Pacific Standard Time",        // Los Angeles
        "Eastern Standard Time",        // New York
        "GMT Standard Time",            // London
        "W. Europe Standard Time",      // Berlin / Paris
        "China Standard Time",          // Hong Kong / Beijing
        "Tokyo Standard Time",          // Tokyo
        "AUS Eastern Standard Time",    // Sydney
    }.Where(id => Find(id) is not null).ToList();
}
