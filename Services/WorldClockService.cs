using System;
using System.Collections.Generic;

namespace WinForge.Services;

/// <summary>
/// 世界時鐘 · World-clock helpers — pure managed <see cref="TimeZoneInfo"/>. Every lookup is guarded so
/// a missing/renamed zone id can never throw. No redirect, no shelling out. Bilingual UI lives in the page.
/// </summary>
public static class WorldClockService
{
    /// <summary>Common seed zones by IANA-ish Windows id. Missing ones are skipped silently.</summary>
    public static readonly string[] SeedZoneIds =
    {
        "UTC",
        "Eastern Standard Time",   // New York
        "GMT Standard Time",       // London
        "Tokyo Standard Time",     // Tokyo
        "China Standard Time",     // Hong Kong / Beijing
    };

    /// <summary>Resolve a zone id, returning null (never throwing) when it isn't installed.</summary>
    public static TimeZoneInfo? Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return null; }
    }

    /// <summary>The machine local zone, or null if it can't be read.</summary>
    public static TimeZoneInfo? Local()
    {
        try { return TimeZoneInfo.Local; }
        catch { return null; }
    }

    /// <summary>All installed system zones, sorted by base UTC offset then display name. Never throws.</summary>
    public static IReadOnlyList<TimeZoneInfo> AllZones()
    {
        try
        {
            var list = new List<TimeZoneInfo>(TimeZoneInfo.GetSystemTimeZones());
            list.Sort((a, b) =>
            {
                int c = a.BaseUtcOffset.CompareTo(b.BaseUtcOffset);
                return c != 0 ? c : string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
            return list;
        }
        catch { return Array.Empty<TimeZoneInfo>(); }
    }

    /// <summary>Convert a UTC instant into the given zone, guarded. Falls back to the raw instant on error.</summary>
    public static DateTime InZone(DateTime utc, TimeZoneInfo? zone)
    {
        if (zone == null) return utc;
        try { return TimeZoneInfo.ConvertTimeFromUtc(utc, zone); }
        catch { return utc; }
    }

    /// <summary>Effective UTC offset for a zone at a given UTC instant (accounts for DST). Guarded.</summary>
    public static TimeSpan OffsetAt(DateTime utc, TimeZoneInfo? zone)
    {
        if (zone == null) return TimeSpan.Zero;
        try { return zone.GetUtcOffset(utc); }
        catch { return zone.BaseUtcOffset; }
    }

    /// <summary>Format an offset like "UTC+08:00" / "UTC-05:00".</summary>
    public static string FormatOffset(TimeSpan off)
    {
        string sign = off < TimeSpan.Zero ? "-" : "+";
        var a = off.Duration();
        return $"UTC{sign}{a.Hours:00}:{a.Minutes:00}";
    }
}
