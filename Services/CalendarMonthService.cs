using System;
using System.Globalization;

namespace WinForge.Services;

/// <summary>
/// 月曆計算 · Pure managed month-calendar maths — no side effects, never throws.
/// Provides the grid layout for a given month plus per-day facts (ISO week, day-of-year,
/// days-from-today) and bilingual month / weekday names. All computation is System.DateTime /
/// System.Globalization only.
/// </summary>
public static class CalendarMonthService
{
    /// <summary>One cell in the month grid.</summary>
    public readonly struct DayCell
    {
        public DayCell(DateTime date, bool inMonth)
        {
            Date = date;
            InCurrentMonth = inMonth;
        }

        public DateTime Date { get; }
        public bool InCurrentMonth { get; }
        public int Day => Date.Day;
    }

    /// <summary>Bilingual month names (index 1..12); English + 繁體中文/粵語.</summary>
    private static readonly string[] MonthsEn =
    {
        "", "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    };

    private static readonly string[] MonthsZh =
    {
        "", "一月", "二月", "三月", "四月", "五月", "六月",
        "七月", "八月", "九月", "十月", "十一月", "十二月"
    };

    // index 0 = Sunday .. 6 = Saturday (matches DayOfWeek enum values)
    private static readonly string[] WeekdayShortEn = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
    private static readonly string[] WeekdayShortZh = { "日", "一", "二", "三", "四", "五", "六" };
    private static readonly string[] WeekdayLongEn =
    {
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
    };
    private static readonly string[] WeekdayLongZh =
    {
        "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"
    };

    /// <summary>Bilingual month name for month number 1..12; safe for out-of-range input.</summary>
    public static string MonthName(int month, bool cantonese)
    {
        try
        {
            if (month < 1 || month > 12) return string.Empty;
            return cantonese ? MonthsZh[month] : MonthsEn[month];
        }
        catch { return string.Empty; }
    }

    /// <summary>Short bilingual weekday name for a <see cref="DayOfWeek"/>.</summary>
    public static string WeekdayShort(DayOfWeek dow, bool cantonese)
    {
        try
        {
            int i = (int)dow;
            if (i < 0 || i > 6) return string.Empty;
            return cantonese ? WeekdayShortZh[i] : WeekdayShortEn[i];
        }
        catch { return string.Empty; }
    }

    /// <summary>Long bilingual weekday name for a <see cref="DayOfWeek"/>.</summary>
    public static string WeekdayLong(DayOfWeek dow, bool cantonese)
    {
        try
        {
            int i = (int)dow;
            if (i < 0 || i > 6) return string.Empty;
            return cantonese ? WeekdayLongZh[i] : WeekdayLongEn[i];
        }
        catch { return string.Empty; }
    }

    /// <summary>Ordered weekday headers (7) starting on the chosen first day.</summary>
    public static DayOfWeek[] WeekdayOrder(DayOfWeek firstDay)
    {
        var order = new DayOfWeek[7];
        for (int i = 0; i < 7; i++)
            order[i] = (DayOfWeek)(((int)firstDay + i) % 7);
        return order;
    }

    /// <summary>
    /// Build the 6×7 grid of <see cref="DayCell"/> for the given year/month, with leading and
    /// trailing days pulled from the adjacent months. Always 42 cells; never throws.
    /// </summary>
    public static DayCell[] BuildGrid(int year, int month, DayOfWeek firstDay)
    {
        var cells = new DayCell[42];
        try
        {
            if (year < 1) year = 1;
            if (year > 9999) year = 9999;
            if (month < 1) month = 1;
            if (month > 12) month = 12;

            var first = new DateTime(year, month, 1);
            int offset = ((int)first.DayOfWeek - (int)firstDay + 7) % 7;
            DateTime start = first.AddDays(-offset);

            for (int i = 0; i < 42; i++)
            {
                DateTime d;
                try { d = start.AddDays(i); }
                catch { d = DateTime.MinValue; }
                cells[i] = new DayCell(d, d != DateTime.MinValue && d.Month == month && d.Year == year);
            }
        }
        catch
        {
            for (int i = 0; i < 42; i++)
                cells[i] = new DayCell(DateTime.MinValue, false);
        }
        return cells;
    }

    /// <summary>ISO-8601 week number (1..53) for a date; never throws.</summary>
    public static int IsoWeek(DateTime date)
    {
        try
        {
            return ISOWeek.GetWeekOfYear(date);
        }
        catch
        {
            try
            {
                var cal = CultureInfo.InvariantCulture.Calendar;
                DayOfWeek day = cal.GetDayOfWeek(date);
                if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
                    date = date.AddDays(3);
                return cal.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            }
            catch { return 0; }
        }
    }

    /// <summary>Day of the year (1..366); never throws.</summary>
    public static int DayOfYear(DateTime date)
    {
        try { return date.DayOfYear; }
        catch { return 0; }
    }

    /// <summary>Whole days from today (today's midnight) to <paramref name="date"/>'s midnight.</summary>
    public static int DaysFromToday(DateTime date)
    {
        try { return (int)(date.Date - DateTime.Today).TotalDays; }
        catch { return 0; }
    }
}
