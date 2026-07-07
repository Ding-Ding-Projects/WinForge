using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// Cron 建構器 · Pure-managed 5-field cron (minute hour day-of-month month day-of-week)
/// parser, plain-English/Cantonese explainer, and next-fire-time projector. No external deps,
/// no shelling out. Tokens supported: * , - / and numeric values. Bilingual descriptions.
/// </summary>
public static class CronBuilderService
{
    /// <summary>Parsed result: either a valid schedule (allowed value sets per field) or an error.</summary>
    public sealed class CronResult
    {
        public bool Ok { get; init; }
        public string ErrorEn { get; init; } = "";
        public string ErrorZh { get; init; } = "";
        public string DescriptionEn { get; init; } = "";
        public string DescriptionZh { get; init; } = "";

        public HashSet<int> Minutes { get; init; } = new();
        public HashSet<int> Hours { get; init; } = new();
        public HashSet<int> DaysOfMonth { get; init; } = new();
        public HashSet<int> Months { get; init; } = new();
        public HashSet<int> DaysOfWeek { get; init; } = new(); // 0..6, Sunday = 0

        // Whether each of the day fields is a wildcard ('*'); matters for cron's day-of-month/day-of-week OR rule.
        public bool DomWildcard { get; init; }
        public bool DowWildcard { get; init; }
    }

    private static readonly string[] MonthNamesEn =
    {
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };
    private static readonly string[] MonthNamesZh =
    {
        "一月", "二月", "三月", "四月", "五月", "六月",
        "七月", "八月", "九月", "十月", "十一月", "十二月"
    };
    private static readonly string[] DayNamesEn =
    {
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
    };
    private static readonly string[] DayNamesZh =
    {
        "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"
    };

    /// <summary>Parse a 5-field cron expression. Never throws; failures come back as Ok=false.</summary>
    public static CronResult Parse(string? expression)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(expression))
                return Fail("Expression is empty.", "運算式係空嘅。");

            string[] parts = expression.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
                return Fail(
                    $"Expected 5 fields (minute hour day-of-month month day-of-week), got {parts.Length}.",
                    $"要有 5 個欄位（分 時 日 月 星期），而家有 {parts.Length} 個。");

            if (!TryField(parts[0], 0, 59, out var minutes, out var e1))
                return Fail("Minute field: " + e1.Item1, "分鐘欄位：" + e1.Item2);
            if (!TryField(parts[1], 0, 23, out var hours, out var e2))
                return Fail("Hour field: " + e2.Item1, "小時欄位：" + e2.Item2);
            if (!TryField(parts[2], 1, 31, out var dom, out var e3))
                return Fail("Day-of-month field: " + e3.Item1, "日欄位：" + e3.Item2);
            if (!TryField(parts[3], 1, 12, out var months, out var e4))
                return Fail("Month field: " + e4.Item1, "月欄位：" + e4.Item2);
            if (!TryField(parts[4], 0, 6, out var dow, out var e5, allowSeven: true))
                return Fail("Day-of-week field: " + e5.Item1, "星期欄位：" + e5.Item2);

            // 7 == Sunday in many cron dialects; fold it to 0.
            if (dow.Remove(7)) dow.Add(0);

            bool domStar = parts[2].Trim() == "*";
            bool dowStar = parts[4].Trim() == "*";

            var result = new CronResult
            {
                Ok = true,
                Minutes = minutes,
                Hours = hours,
                DaysOfMonth = dom,
                Months = months,
                DaysOfWeek = dow,
                DomWildcard = domStar,
                DowWildcard = dowStar,
            };

            var (en, zh) = Describe(result);
            return new CronResult
            {
                Ok = true,
                Minutes = minutes,
                Hours = hours,
                DaysOfMonth = dom,
                Months = months,
                DaysOfWeek = dow,
                DomWildcard = domStar,
                DowWildcard = dowStar,
                DescriptionEn = en,
                DescriptionZh = zh,
            };
        }
        catch (Exception ex)
        {
            return Fail("Could not parse: " + ex.Message, "解析唔到：" + ex.Message);
        }
    }

    private static CronResult Fail(string en, string zh) =>
        new() { Ok = false, ErrorEn = en, ErrorZh = zh };

    /// <summary>Parse one cron field into the set of allowed values in [min,max].</summary>
    private static bool TryField(string field, int min, int max, out HashSet<int> values,
        out (string, string) error, bool allowSeven = false)
    {
        values = new HashSet<int>();
        error = ("", "");
        field = field.Trim();
        if (field.Length == 0)
        {
            error = ("empty.", "係空嘅。");
            return false;
        }

        int upper = allowSeven ? 7 : max;

        foreach (var raw in field.Split(','))
        {
            string chunk = raw.Trim();
            if (chunk.Length == 0)
            {
                error = ("stray comma.", "多咗個逗號。");
                return false;
            }

            int step = 1;
            string rangePart = chunk;
            int slash = chunk.IndexOf('/');
            if (slash >= 0)
            {
                rangePart = chunk[..slash];
                string stepStr = chunk[(slash + 1)..];
                if (!int.TryParse(stepStr, NumberStyles.None, CultureInfo.InvariantCulture, out step) || step <= 0)
                {
                    error = ($"'{stepStr}' is not a valid step.", $"'{stepStr}' 唔係有效嘅間隔。");
                    return false;
                }
                if (rangePart.Length == 0) rangePart = "*";
            }

            int lo, hi;
            if (rangePart == "*")
            {
                lo = min; hi = max;
            }
            else if (rangePart.Contains('-'))
            {
                var bounds = rangePart.Split('-');
                if (bounds.Length != 2 ||
                    !int.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out lo) ||
                    !int.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out hi))
                {
                    error = ($"'{rangePart}' is not a valid range.", $"'{rangePart}' 唔係有效嘅範圍。");
                    return false;
                }
            }
            else
            {
                if (!int.TryParse(rangePart, NumberStyles.None, CultureInfo.InvariantCulture, out lo))
                {
                    error = ($"'{rangePart}' is not a number.", $"'{rangePart}' 唔係數字。");
                    return false;
                }
                hi = lo;
            }

            if (lo < min || lo > upper || hi < min || hi > upper)
            {
                error = ($"value out of range {min}-{max}.", $"數值超出範圍 {min}-{max}。");
                return false;
            }
            if (lo > hi)
            {
                error = ($"range start {lo} is after end {hi}.", $"範圍起點 {lo} 大過終點 {hi}。");
                return false;
            }

            for (int v = lo; v <= hi; v += step)
                values.Add(v);
        }

        if (values.Count == 0)
        {
            error = ("no values matched.", "冇任何數值符合。");
            return false;
        }
        return true;
    }

    /// <summary>Build a bilingual plain-language description of a valid schedule.</summary>
    private static (string en, string zh) Describe(CronResult r)
    {
        bool minAll = r.Minutes.Count == 60;
        bool hourAll = r.Hours.Count == 24;

        string timeEn, timeZh;
        if (minAll && hourAll)
        {
            timeEn = "Every minute";
            timeZh = "每分鐘";
        }
        else if (r.Minutes.Count == 1 && r.Hours.Count == 1)
        {
            int h = r.Hours.First(), m = r.Minutes.First();
            timeEn = $"At {h:00}:{m:00}";
            timeZh = $"喺 {h:00}:{m:00}";
        }
        else if (hourAll && r.Minutes.Count == 1)
        {
            int m = r.Minutes.First();
            timeEn = $"At minute {m} of every hour";
            timeZh = $"每個鐘嘅第 {m} 分鐘";
        }
        else if (minAll)
        {
            timeEn = "Every minute of hour(s) " + JoinInts(r.Hours);
            timeZh = "喺 " + JoinInts(r.Hours) + " 點嘅每分鐘";
        }
        else
        {
            timeEn = "At minute(s) " + JoinInts(r.Minutes) + " past hour(s) " + JoinInts(r.Hours);
            timeZh = "喺 " + JoinInts(r.Hours) + " 點嘅第 " + JoinInts(r.Minutes) + " 分";
        }

        var clausesEn = new List<string>();
        var clausesZh = new List<string>();

        if (!r.DomWildcard)
        {
            clausesEn.Add("on day-of-month " + JoinInts(r.DaysOfMonth));
            clausesZh.Add("喺每月 " + JoinInts(r.DaysOfMonth) + " 號");
        }

        if (r.Months.Count != 12)
        {
            clausesEn.Add("in " + JoinNames(r.Months, MonthNamesEn, 1));
            clausesZh.Add("喺 " + JoinNames(r.Months, MonthNamesZh, 1));
        }

        if (!r.DowWildcard)
        {
            clausesEn.Add("only on " + JoinNames(r.DaysOfWeek, DayNamesEn, 0));
            clausesZh.Add("淨係喺 " + JoinNames(r.DaysOfWeek, DayNamesZh, 0));
        }

        string en = timeEn;
        string zh = timeZh;
        if (clausesEn.Count > 0)
        {
            en += ", " + string.Join(", ", clausesEn);
            zh += "，" + string.Join("、", clausesZh);
        }
        en += ".";
        zh += "。";

        // Cron OR-semantics note when both day fields are restricted.
        if (!r.DomWildcard && !r.DowWildcard)
        {
            en += " (Note: a day matches if EITHER the day-of-month OR the day-of-week matches.)";
            zh += "（注意：只要日子或者星期其中一個啱就會執行。）";
        }

        return (en, zh);
    }

    private static string JoinInts(IEnumerable<int> vals)
    {
        var s = vals.OrderBy(v => v).ToList();
        return string.Join(", ", s.Select(v => v.ToString(CultureInfo.InvariantCulture)));
    }

    private static string JoinNames(IEnumerable<int> vals, string[] names, int offset)
    {
        var s = vals.OrderBy(v => v)
                    .Where(v => v - offset >= 0 && v - offset < names.Length)
                    .Select(v => names[v - offset]);
        return string.Join(", ", s);
    }

    /// <summary>Whether the given moment (to minute precision) matches the schedule.</summary>
    private static bool Matches(CronResult r, DateTime t)
    {
        if (!r.Minutes.Contains(t.Minute)) return false;
        if (!r.Hours.Contains(t.Hour)) return false;
        if (!r.Months.Contains(t.Month)) return false;

        int dow = (int)t.DayOfWeek; // Sunday = 0
        bool domMatch = r.DaysOfMonth.Contains(t.Day);
        bool dowMatch = r.DaysOfWeek.Contains(dow);

        // Standard cron rule: if both day fields are restricted, match on OR;
        // if one is '*', it's ignored and the other must match.
        if (r.DomWildcard && r.DowWildcard) return true;
        if (r.DomWildcard) return dowMatch;
        if (r.DowWildcard) return domMatch;
        return domMatch || dowMatch;
    }

    /// <summary>
    /// Project the next <paramref name="count"/> fire times strictly after <paramref name="from"/>,
    /// iterating minute-by-minute up to a 4-year horizon. Returns fewer (or none) if the horizon is hit.
    /// </summary>
    public static List<DateTime> NextFireTimes(CronResult r, DateTime from, int count = 10)
    {
        var results = new List<DateTime>();
        if (!r.Ok || count <= 0) return results;

        // Start at the next whole minute after 'from'.
        DateTime t = new DateTime(from.Year, from.Month, from.Day, from.Hour, from.Minute, 0, from.Kind)
                        .AddMinutes(1);
        DateTime horizon = from.AddYears(4);

        while (t <= horizon && results.Count < count)
        {
            if (Matches(r, t))
                results.Add(t);
            t = t.AddMinutes(1);
        }
        return results;
    }
}
