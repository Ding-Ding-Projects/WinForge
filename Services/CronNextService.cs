using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// Cron 下次執行時間計算 · Pure-managed 5-field cron parser + next-run calculator.
/// Fields: minute hour day-of-month month day-of-week. Supports * , - / lists ranges steps,
/// plus @hourly/@daily/@weekly/@monthly/@yearly macros. Never throws — parse errors surface via
/// <see cref="CronParseResult"/>. Enumeration is minute-stepped with a hard cap so a never-matching
/// expression can't loop forever.
/// </summary>
public static class CronNextService
{
    /// <summary>Parsed schedule — one bit-set per field.</summary>
    public sealed class CronSchedule
    {
        public required bool[] Minutes;     // 0..59
        public required bool[] Hours;       // 0..23
        public required bool[] DaysOfMonth; // 1..31
        public required bool[] Months;      // 1..12
        public required bool[] DaysOfWeek;  // 0..6 (Sun=0)
        // Distinguish "*" (unrestricted) from an explicit list — matters for the DOM/DOW OR-rule.
        public bool DomRestricted;
        public bool DowRestricted;
    }

    public sealed class CronParseResult
    {
        public CronSchedule? Schedule;
        public bool Ok => Schedule != null && ErrorEn == null;
        public string? ErrorEn;
        public string? ErrorZh;
    }

    private static readonly Dictionary<string, string> Macros = new(StringComparer.OrdinalIgnoreCase)
    {
        ["@yearly"]  = "0 0 1 1 *",
        ["@annually"]= "0 0 1 1 *",
        ["@monthly"] = "0 0 1 * *",
        ["@weekly"]  = "0 0 * * 0",
        ["@daily"]   = "0 0 * * *",
        ["@midnight"]= "0 0 * * *",
        ["@hourly"]  = "0 * * * *",
    };

    private static readonly string[] MonthNames =
        { "JAN","FEB","MAR","APR","MAY","JUN","JUL","AUG","SEP","OCT","NOV","DEC" };
    private static readonly string[] DowNames =
        { "SUN","MON","TUE","WED","THU","FRI","SAT" };

    /// <summary>Parse a 5-field cron expression (or macro). Never throws.</summary>
    public static CronParseResult Parse(string? expression)
    {
        var r = new CronParseResult();
        try
        {
            var expr = (expression ?? string.Empty).Trim();
            if (expr.Length == 0)
            {
                r.ErrorEn = "Enter a cron expression.";
                r.ErrorZh = "請輸入 cron 運算式。";
                return r;
            }

            if (expr.StartsWith('@'))
            {
                if (!Macros.TryGetValue(expr, out var mapped))
                {
                    r.ErrorEn = $"Unknown macro '{expr}'. Try @hourly, @daily, @weekly, @monthly, @yearly.";
                    r.ErrorZh = $"唔識得個巨集 '{expr}'。試下 @hourly、@daily、@weekly、@monthly、@yearly。";
                    return r;
                }
                expr = mapped;
            }

            var parts = expr.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
            {
                r.ErrorEn = $"Expected 5 fields (min hour day-of-month month day-of-week); got {parts.Length}.";
                r.ErrorZh = $"要 5 個欄位（分 時 日 月 星期）；而家有 {parts.Length} 個。";
                return r;
            }

            var min = ParseField(parts[0], 0, 59, null, out var e0);
            if (e0 != null) { Fail(r, parts[0], e0.Value); return r; }
            var hour = ParseField(parts[1], 0, 23, null, out var e1);
            if (e1 != null) { Fail(r, parts[1], e1.Value); return r; }
            var dom = ParseField(parts[2], 1, 31, null, out var e2);
            if (e2 != null) { Fail(r, parts[2], e2.Value); return r; }
            var mon = ParseField(parts[3], 1, 12, MonthNames, out var e3);
            if (e3 != null) { Fail(r, parts[3], e3.Value); return r; }
            // day-of-week: accept 0..7 (0 and 7 both = Sunday) plus names.
            var dow = ParseDow(parts[4], out var e4);
            if (e4 != null) { Fail(r, parts[4], e4.Value); return r; }

            r.Schedule = new CronSchedule
            {
                Minutes = min,
                Hours = hour,
                DaysOfMonth = dom,
                Months = mon,
                DaysOfWeek = dow,
                DomRestricted = !IsStar(parts[2]),
                DowRestricted = !IsStar(parts[4]),
            };
            return r;
        }
        catch (Exception ex)
        {
            r.Schedule = null;
            r.ErrorEn = "Could not parse expression: " + ex.Message;
            r.ErrorZh = "無法解析運算式：" + ex.Message;
            return r;
        }
    }

    private static bool IsStar(string field) => field.Trim() == "*" || field.Trim().StartsWith("*/");

    private static void Fail(CronParseResult r, string field, (string en, string zh) e)
    {
        r.Schedule = null;
        r.ErrorEn = $"Field '{field}': {e.en}";
        r.ErrorZh = $"欄位 '{field}'：{e.zh}";
    }

    private static bool[] ParseDow(string field, out (string en, string zh)? err)
    {
        // Normalise 7 -> 0 (both Sunday) after parsing on a 0..7 range, then fold.
        var raw = ParseField(field, 0, 7, DowNames7(), out err);
        if (err != null) return new bool[7];
        var set = new bool[7];
        for (int i = 0; i <= 7; i++)
            if (raw[i]) set[i % 7] = true;
        return set;
    }

    // Names table sized to 0..7 so "SUN" can map to index 0 (7 also folds to Sunday).
    private static string[] DowNames7()
        => new[] { "SUN","MON","TUE","WED","THU","FRI","SAT","SUN" };

    /// <summary>Parse one field into a bitset [min..max]. Names optional (index 0 == min).</summary>
    private static bool[] ParseField(string field, int min, int max, string[]? names, out (string en, string zh)? err)
    {
        err = null;
        int size = max - min + 1;
        var set = new bool[size];
        field = field.Trim();
        if (field.Length == 0)
        {
            err = ("empty.", "空白。");
            return set;
        }

        foreach (var itemRaw in field.Split(','))
        {
            var item = itemRaw.Trim();
            if (item.Length == 0)
            {
                err = ("empty list entry.", "清單有空項。");
                return set;
            }

            int step = 1;
            var body = item;
            var slash = item.IndexOf('/');
            if (slash >= 0)
            {
                body = item[..slash].Trim();
                var stepStr = item[(slash + 1)..].Trim();
                if (!int.TryParse(stepStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out step) || step <= 0)
                {
                    err = ($"bad step '{stepStr}'.", $"步進 '{stepStr}' 有問題。");
                    return set;
                }
                if (body.Length == 0) body = "*";
            }

            int lo, hi;
            if (body == "*")
            {
                lo = min; hi = max;
            }
            else
            {
                var dash = body.IndexOf('-');
                if (dash > 0)
                {
                    if (!TryVal(body[..dash], names, min, max, out lo) ||
                        !TryVal(body[(dash + 1)..], names, min, max, out hi))
                    {
                        err = ($"bad range '{body}'.", $"範圍 '{body}' 有問題。");
                        return set;
                    }
                }
                else
                {
                    if (!TryVal(body, names, min, max, out lo))
                    {
                        err = ($"bad value '{body}'.", $"數值 '{body}' 有問題。");
                        return set;
                    }
                    hi = slash >= 0 ? max : lo; // "5/10" means from 5 to max stepping 10
                }
            }

            if (lo < min || hi > max || lo > hi)
            {
                err = ($"value out of {min}-{max} range.", $"數值超出 {min}-{max} 範圍。");
                return set;
            }

            for (int v = lo; v <= hi; v += step)
                set[v - min] = true;
        }

        return set;
    }

    private static bool TryVal(string token, string[]? names, int min, int max, out int val)
    {
        token = token.Trim();
        if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out val))
            return true;
        if (names != null)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], token, StringComparison.OrdinalIgnoreCase))
                {
                    val = min + i;
                    return true;
                }
            }
        }
        val = 0;
        return false;
    }

    /// <summary>Does the given local wall-clock time fire under this schedule?</summary>
    public static bool Matches(CronSchedule s, DateTime t)
    {
        if (!s.Minutes[t.Minute]) return false;
        if (!s.Hours[t.Hour]) return false;
        if (!s.Months[t.Month - 1]) return false;

        bool domOk = s.DaysOfMonth[t.Day - 1];
        int dow = (int)t.DayOfWeek; // Sun=0
        bool dowOk = s.DaysOfWeek[dow];

        // Vixie-cron rule: if BOTH day-of-month and day-of-week are restricted, match either.
        if (s.DomRestricted && s.DowRestricted)
            return domOk || dowOk;
        if (s.DomRestricted) return domOk;
        if (s.DowRestricted) return dowOk;
        return true; // both wildcards
    }

    /// <summary>
    /// Next <paramref name="count"/> fire times at/after <paramref name="fromLocal"/> (wall-clock in
    /// <paramref name="tz"/>). Returns <see cref="DateTimeOffset"/> in that zone. Minute-stepped with a
    /// hard cap (~4 years) so a never-matching expression terminates. Never throws.
    /// </summary>
    public static List<DateTimeOffset> NextRuns(CronSchedule s, DateTimeOffset fromLocal, TimeZoneInfo tz, int count)
    {
        var results = new List<DateTimeOffset>();
        if (s == null || count <= 0) return results;
        count = Math.Min(count, 500);

        // Work in the target zone's wall-clock. Start at the next whole minute.
        DateTime wall;
        try
        {
            wall = TimeZoneInfo.ConvertTime(fromLocal, tz).DateTime;
        }
        catch
        {
            wall = fromLocal.DateTime;
        }
        wall = new DateTime(wall.Year, wall.Month, wall.Day, wall.Hour, wall.Minute, 0, DateTimeKind.Unspecified)
                   .AddMinutes(1);

        const long MaxMinutes = 366L * 4 * 24 * 60; // ~4 years of minutes
        long stepped = 0;
        while (results.Count < count && stepped < MaxMinutes)
        {
            if (Matches(s, wall))
            {
                DateTimeOffset dto;
                try
                {
                    // Resolve wall-clock -> offset, tolerating DST gaps/folds.
                    if (tz.IsInvalidTime(wall))
                    {
                        wall = wall.AddMinutes(1);
                        stepped++;
                        continue;
                    }
                    var off = tz.GetUtcOffset(wall);
                    dto = new DateTimeOffset(wall, off);
                }
                catch
                {
                    dto = new DateTimeOffset(wall, TimeSpan.Zero);
                }
                results.Add(dto);
            }
            wall = wall.AddMinutes(1);
            stepped++;
        }
        return results;
    }

    /// <summary>Plain-English / 粵語 description of a parsed schedule field-set.</summary>
    public static (string en, string zh) Describe(CronSchedule s)
    {
        var en = new StringBuilder("Runs ");
        var zh = new StringBuilder("");

        // Time-of-day.
        var minutes = SetValues(s.Minutes, 0);
        var hours = SetValues(s.Hours, 0);
        bool everyMin = minutes.Count == 60;
        bool everyHour = hours.Count == 24;

        if (everyMin && everyHour)
        {
            en.Append("every minute");
            zh.Append("每分鐘");
        }
        else if (everyMin && !everyHour)
        {
            en.Append("every minute during " + JoinHours(hours));
            zh.Append("喺 " + JoinHoursZh(hours) + " 每分鐘");
        }
        else if (minutes.Count == 1 && everyHour)
        {
            en.Append($"at :{minutes[0]:00} of every hour");
            zh.Append($"每個鐘嘅 {minutes[0]:00} 分");
        }
        else if (minutes.Count == 1 && hours.Count == 1)
        {
            en.Append($"at {hours[0]:00}:{minutes[0]:00}");
            zh.Append($"喺 {hours[0]:00}:{minutes[0]:00}");
        }
        else
        {
            en.Append("at minute(s) " + string.Join(",", minutes) + " of hour(s) " + string.Join(",", hours));
            zh.Append("喺 " + string.Join(",", hours) + " 時嘅 " + string.Join(",", minutes) + " 分");
        }

        // Day constraints.
        var days = DescribeDays(s);
        if (days.en.Length > 0)
        {
            en.Append(' ').Append(days.en);
            zh.Append(' ').Append(days.zh);
        }

        // Month constraints.
        var months = SetValues(s.Months, 1);
        if (months.Count != 12)
        {
            en.Append(" in " + string.Join(", ", months.Select(m => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m))));
            zh.Append("，只喺 " + string.Join("、", months.Select(m => m + "月")));
        }

        en.Append('.');
        zh.Append("。");
        return (en.ToString(), zh.ToString());
    }

    private static (string en, string zh) DescribeDays(CronSchedule s)
    {
        var dom = SetValues(s.DaysOfMonth, 1);
        var dow = SetValues(s.DaysOfWeek, 0);
        bool everyDom = dom.Count == 31;
        bool everyDow = dow.Count == 7;

        string[] enDow = { "Sun","Mon","Tue","Wed","Thu","Fri","Sat" };
        string[] zhDow = { "日","一","二","三","四","五","六" };

        if (!s.DomRestricted && !s.DowRestricted)
            return ("every day", "每日");

        var en = new StringBuilder();
        var zh = new StringBuilder();

        if (s.DomRestricted && !everyDom)
        {
            en.Append("on day-of-month " + string.Join(",", dom));
            zh.Append("每月 " + string.Join("、", dom) + " 號");
        }

        if (s.DowRestricted && !everyDow)
        {
            if (en.Length > 0) { en.Append(" or "); zh.Append(" 或 "); }
            en.Append("on " + string.Join(",", dow.Select(d => enDow[d % 7])));
            zh.Append("逢星期" + string.Join("、", dow.Select(d => zhDow[d % 7])));
        }

        if (en.Length == 0) return ("every day", "每日");
        return (en.ToString(), zh.ToString());
    }

    private static string JoinHours(List<int> hours)
        => string.Join(",", hours.Select(h => $"{h:00}:00"));
    private static string JoinHoursZh(List<int> hours)
        => string.Join("、", hours.Select(h => $"{h:00} 時"));

    private static List<int> SetValues(bool[] set, int offset)
    {
        var list = new List<int>();
        for (int i = 0; i < set.Length; i++)
            if (set[i]) list.Add(i + offset);
        return list;
    }

    /// <summary>Human "in X" relative string, bilingual.</summary>
    public static (string en, string zh) Relative(DateTimeOffset when, DateTimeOffset now)
    {
        var d = when - now;
        if (d < TimeSpan.Zero) return ("now", "而家");
        if (d.TotalSeconds < 60) return ("in <1 min", "少過 1 分鐘");

        int mins = (int)d.TotalMinutes;
        if (mins < 60) return ($"in {mins} min", $"{mins} 分鐘後");

        if (d.TotalHours < 24)
        {
            int h = (int)d.TotalHours, m = mins % 60;
            var en = m > 0 ? $"in {h}h {m}m" : $"in {h}h";
            var zh = m > 0 ? $"{h} 小時 {m} 分後" : $"{h} 小時後";
            return (en, zh);
        }

        int days = (int)d.TotalDays;
        int hrs = (int)(d.TotalHours - days * 24);
        var enD = hrs > 0 ? $"in {days}d {hrs}h" : $"in {days}d";
        var zhD = hrs > 0 ? $"{days} 日 {hrs} 小時後" : $"{days} 日後";
        return (enD, zhD);
    }
}
