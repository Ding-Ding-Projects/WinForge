using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// iCalendar (.ics) 產生器與解析器 · Pure-managed builder and light parser for RFC 5545 iCalendar
/// files. Produces a valid VCALENDAR/VEVENT (with optional RRULE and VALARM), folding lines at 75
/// octets, escaping text properties, and emitting UTC UID/DTSTAMP. Also a forgiving VEVENT scanner
/// that lists each event's SUMMARY and DTSTART. Everything is best-effort and never throws.
/// </summary>
public static class ICalendarService
{
    public enum Recur { None, Daily, Weekly, Monthly, Yearly }

    /// <summary>一個活動嘅所有欄位 · All the fields describing one event.</summary>
    public sealed class EventSpec
    {
        public string Summary = "";
        public string Location = "";
        public string Description = "";
        public DateTimeOffset Start = DateTimeOffset.Now;
        public bool AllDay;
        public int DurationMinutes = 60;   // ignored when AllDay
        public Recur Recurrence = Recur.None;
        public int Interval = 1;           // RRULE INTERVAL
        public int Count;                  // RRULE COUNT; 0 = forever
        public int ReminderMinutes = -1;   // -1 = none; else minutes before start
    }

    /// <summary>解析出嚟嘅活動摘要 · One parsed event's summary line.</summary>
    public readonly record struct ParsedEvent(string Summary, string Start);

    // ===== build =====

    /// <summary>由 spec 產生完整 .ics 文字 · Build a complete .ics document. Never throws.</summary>
    public static string Build(EventSpec e)
    {
        try
        {
            e ??= new EventSpec();
            var sb = new StringBuilder();
            var raw = new List<string>();

            raw.Add("BEGIN:VCALENDAR");
            raw.Add("VERSION:2.0");
            raw.Add("PRODID:-//WinForge//iCalendar Builder//EN");
            raw.Add("CALSCALE:GREGORIAN");
            raw.Add("METHOD:PUBLISH");
            raw.Add("BEGIN:VEVENT");
            raw.Add("UID:" + Guid.NewGuid().ToString("N") + "@winforge");
            raw.Add("DTSTAMP:" + Utc(DateTimeOffset.UtcNow));

            if (e.AllDay)
            {
                var d = e.Start.Date;
                raw.Add("DTSTART;VALUE=DATE:" + d.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                raw.Add("DTEND;VALUE=DATE:" + d.AddDays(1).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            }
            else
            {
                var start = e.Start;
                int dur = e.DurationMinutes > 0 ? e.DurationMinutes : 60;
                var end = start.AddMinutes(dur);
                raw.Add("DTSTART:" + Local(start));
                raw.Add("DTEND:" + Local(end));
            }

            var rrule = BuildRRule(e);
            if (rrule is not null) raw.Add(rrule);

            if (!string.IsNullOrWhiteSpace(e.Summary)) raw.Add("SUMMARY:" + Escape(e.Summary));
            if (!string.IsNullOrWhiteSpace(e.Location)) raw.Add("LOCATION:" + Escape(e.Location));
            if (!string.IsNullOrWhiteSpace(e.Description)) raw.Add("DESCRIPTION:" + Escape(e.Description));

            if (e.ReminderMinutes >= 0)
            {
                raw.Add("BEGIN:VALARM");
                raw.Add("ACTION:DISPLAY");
                raw.Add("DESCRIPTION:" + Escape(string.IsNullOrWhiteSpace(e.Summary) ? "Reminder" : e.Summary));
                raw.Add("TRIGGER:-PT" + e.ReminderMinutes + "M");
                raw.Add("END:VALARM");
            }

            raw.Add("END:VEVENT");
            raw.Add("END:VCALENDAR");

            foreach (var line in raw)
                sb.Append(Fold(line)).Append("\r\n");
            return sb.ToString();
        }
        catch
        {
            return "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nEND:VCALENDAR\r\n";
        }
    }

    private static string? BuildRRule(EventSpec e)
    {
        if (e.Recurrence == Recur.None) return null;
        string freq = e.Recurrence switch
        {
            Recur.Daily => "DAILY",
            Recur.Weekly => "WEEKLY",
            Recur.Monthly => "MONTHLY",
            Recur.Yearly => "YEARLY",
            _ => "DAILY",
        };
        var sb = new StringBuilder("RRULE:FREQ=" + freq);
        int interval = e.Interval > 0 ? e.Interval : 1;
        if (interval > 1) sb.Append(";INTERVAL=").Append(interval);
        if (e.Count > 0) sb.Append(";COUNT=").Append(e.Count);
        return sb.ToString();
    }

    // Floating local time (no Z) — DTSTART/DTEND in the wall-clock the user picked.
    private static string Local(DateTimeOffset dt)
        => dt.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);

    private static string Utc(DateTimeOffset dt)
        => dt.ToUniversalTime().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    /// <summary>轉義 ,;\ 同換行 · Escape text-value special chars per RFC 5545.</summary>
    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case ';': sb.Append("\\;"); break;
                case ',': sb.Append("\\,"); break;
                case '\r': break;
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    /// <summary>將一行摺到 75 octet（UTF-8），續行以一個空格開頭 · Fold at 75 octets.</summary>
    private static string Fold(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        // Fast path: pure ASCII under limit.
        if (line.Length <= 75)
        {
            bool ascii = true;
            foreach (char c in line) if (c > 127) { ascii = false; break; }
            if (ascii) return line;
        }

        var sb = new StringBuilder();
        int octets = 0;           // octets used on the current physical line (excludes the CRLF)
        bool continuation = false; // are we on a folded continuation line (which begins with a space)?
        const int limit = 75;
        foreach (char c in line)
        {
            int w = Utf8Width(c);
            // Continuation lines start with a leading space, so they can hold one fewer content octet.
            int effLimit = continuation ? limit - 1 : limit;
            if (octets + w > effLimit)
            {
                sb.Append("\r\n ");
                continuation = true;
                octets = 0; // count only the content octets after the leading space
            }
            sb.Append(c);
            octets += w;
        }
        return sb.ToString();
    }

    private static int Utf8Width(char c)
    {
        if (c <= 0x7F) return 1;
        if (c <= 0x7FF) return 2;
        // Surrogate halves each report as part of a 4-byte sequence; approximate at 2 to stay safe.
        if (char.IsSurrogate(c)) return 2;
        return 3;
    }

    // ===== parse =====

    /// <summary>掃描 .ics 文字，列出每個 VEVENT 嘅 SUMMARY/DTSTART · Forgiving scan; never throws.</summary>
    public static IReadOnlyList<ParsedEvent> Parse(string? ics)
    {
        var results = new List<ParsedEvent>();
        if (string.IsNullOrWhiteSpace(ics)) return results;
        try
        {
            var lines = Unfold(ics);
            bool inEvent = false;
            string summary = "";
            string start = "";
            foreach (var line in lines)
            {
                var t = line.Trim();
                if (t.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    inEvent = true; summary = ""; start = "";
                    continue;
                }
                if (t.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase))
                {
                    if (inEvent)
                        results.Add(new ParsedEvent(
                            summary.Length == 0 ? "(no title)" : summary,
                            start.Length == 0 ? "(no start)" : start));
                    inEvent = false;
                    continue;
                }
                if (!inEvent) continue;

                var (name, value) = SplitProp(t);
                if (name.Equals("SUMMARY", StringComparison.OrdinalIgnoreCase))
                    summary = Unescape(value);
                else if (name.Equals("DTSTART", StringComparison.OrdinalIgnoreCase))
                    start = PrettyDate(value);
            }
        }
        catch { /* return whatever we gathered */ }
        return results;
    }

    // Split "NAME;PARAM=x:VALUE" → ("NAME", "VALUE"); params before the first ':' are dropped,
    // but the property name stops at the first ';'.
    private static (string name, string value) SplitProp(string line)
    {
        int colon = line.IndexOf(':');
        if (colon < 0) return (line, "");
        string left = line.Substring(0, colon);
        string value = line.Substring(colon + 1);
        int semi = left.IndexOf(';');
        string name = semi >= 0 ? left.Substring(0, semi) : left;
        return (name, value);
    }

    // Turn 20260701T093000Z / 20260701 into something readable; leave anything odd as-is.
    private static string PrettyDate(string raw)
    {
        try
        {
            string v = raw.Trim();
            string[] fmts = { "yyyyMMdd'T'HHmmss'Z'", "yyyyMMdd'T'HHmmss", "yyyyMMdd" };
            foreach (var f in fmts)
            {
                if (DateTime.TryParseExact(v, f, CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces, out var dt))
                {
                    return f == "yyyyMMdd"
                        ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                          (f.EndsWith("'Z'") ? " UTC" : "");
                }
            }
            return v;
        }
        catch { return raw; }
    }

    private static string Unescape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                switch (n)
                {
                    case 'n': case 'N': sb.Append('\n'); break;
                    case '\\': sb.Append('\\'); break;
                    case ';': sb.Append(';'); break;
                    case ',': sb.Append(','); break;
                    default: sb.Append(n); break;
                }
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // Unfold RFC 5545 continuation lines (a line starting with space/tab joins the previous line).
    private static List<string> Unfold(string ics)
    {
        var rawLines = ics.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var outLines = new List<string>();
        foreach (var l in rawLines)
        {
            if (l.Length > 0 && (l[0] == ' ' || l[0] == '\t') && outLines.Count > 0)
                outLines[^1] += l.Substring(1);
            else
                outLines.Add(l);
        }
        return outLines;
    }
}
