using System;
using System.Globalization;

namespace WinForge.Services;

/// <summary>
/// 日期計算器 · Date Calculator — pure managed date arithmetic: difference between two dates,
/// add/subtract offsets, age &amp; next-birthday countdown, and calendar info (ISO week, day-of-year,
/// leap year). No side-effects, never throws for callers that guard nulls; all methods are static.
/// </summary>
public static class DateCalcService
{
    /// <summary>Difference between two dates (order-independent for the magnitude fields).</summary>
    public readonly record struct Difference(
        long TotalDays, long Weeks, long RemDays, int BusinessDays,
        int Years, int Months, int Days, bool Negative);

    /// <summary>Age breakdown plus days lived and a countdown to the next birthday.</summary>
    public readonly record struct AgeInfo(
        int Years, int Months, int Days, long TotalDays,
        int DaysToNextBirthday, DateTime NextBirthday, bool NotYetBorn);

    /// <summary>Calendar facts about a single date.</summary>
    public readonly record struct DateFacts(
        DayOfWeek Weekday, int IsoWeek, int IsoYear, int DayOfYear, bool LeapYear);

    /// <summary>Whole days/weeks (+remainder) and a Y/M/D breakdown between two dates.</summary>
    public static Difference Diff(DateTime a, DateTime b)
    {
        DateTime lo = a.Date <= b.Date ? a.Date : b.Date;
        DateTime hi = a.Date <= b.Date ? b.Date : a.Date;
        bool neg = b.Date < a.Date;

        long totalDays = (long)(hi - lo).TotalDays;
        long weeks = totalDays / 7;
        long remDays = totalDays % 7;

        // Business days between lo (inclusive) and hi (exclusive) — Mon..Fri only.
        int business = 0;
        for (DateTime d = lo; d < hi; d = d.AddDays(1))
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                business++;

        // Calendar Y/M/D breakdown (borrowing from the higher date's month).
        int years = hi.Year - lo.Year;
        int months = hi.Month - lo.Month;
        int days = hi.Day - lo.Day;
        if (days < 0)
        {
            months--;
            int daysInPrevMonth = DateTime.DaysInMonth(
                hi.Month == 1 ? hi.Year - 1 : hi.Year,
                hi.Month == 1 ? 12 : hi.Month - 1);
            days += daysInPrevMonth;
        }
        if (months < 0) { years--; months += 12; }

        return new Difference(totalDays, weeks, remDays, business, years, months, days, neg);
    }

    /// <summary>Add (or subtract) a signed offset of years/months/weeks/days to a base date.</summary>
    public static DateTime Offset(DateTime baseDate, int years, int months, int weeks, int days, bool subtract)
    {
        int sign = subtract ? -1 : 1;
        try
        {
            return baseDate.Date
                .AddYears(sign * years)
                .AddMonths(sign * months)
                .AddDays(sign * (weeks * 7L + days));
        }
        catch (ArgumentOutOfRangeException)
        {
            // Clamp to representable range rather than throwing.
            return subtract ? DateTime.MinValue.Date : DateTime.MaxValue.Date;
        }
    }

    /// <summary>Age (Y/M/D) as of <paramref name="asOf"/>, days lived, and next-birthday countdown.</summary>
    public static AgeInfo Age(DateTime birth, DateTime asOf)
    {
        birth = birth.Date;
        asOf = asOf.Date;
        if (birth > asOf)
        {
            int toBorn = (int)(birth - asOf).TotalDays;
            return new AgeInfo(0, 0, 0, 0, toBorn, birth, true);
        }

        Difference d = Diff(birth, asOf);
        long lived = (long)(asOf - birth).TotalDays;

        // Next birthday on/after asOf.
        DateTime next = SafeAnniversary(birth, asOf.Year);
        if (next <= asOf) next = SafeAnniversary(birth, asOf.Year + 1);
        int toNext = (int)(next - asOf).TotalDays;

        return new AgeInfo(d.Years, d.Months, d.Days, lived, toNext, next, false);
    }

    /// <summary>Facts about a single date: weekday, ISO-8601 week/year, day-of-year, leap year.</summary>
    public static DateFacts Facts(DateTime date)
    {
        date = date.Date;
        int isoWeek = ISOWeek.GetWeekOfYear(date);
        int isoYear = ISOWeek.GetYear(date);
        return new DateFacts(date.DayOfWeek, isoWeek, isoYear, date.DayOfYear, DateTime.IsLeapYear(date.Year));
    }

    /// <summary>The birthday in <paramref name="year"/>, clamping Feb-29 to Feb-28 in common years.</summary>
    private static DateTime SafeAnniversary(DateTime birth, int year)
    {
        int day = birth.Day;
        if (birth.Month == 2 && birth.Day == 29 && !DateTime.IsLeapYear(year)) day = 28;
        return new DateTime(year, birth.Month, day);
    }
}
