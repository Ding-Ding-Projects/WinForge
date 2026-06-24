using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 一段前景活動時段 · One captured foreground-activity segment: which app/window was in the
/// foreground, from when to when (local time). Title is stored for the day view but never leaves
/// the device.
/// </summary>
public sealed class ActivitySegment
{
    public string Process { get; set; } = "";   // executable / process name, e.g. "chrome"
    public string Title { get; set; } = "";      // foreground window title (may be empty)
    public long StartUnix { get; set; }          // start, Unix seconds (local-derived UTC)
    public long EndUnix { get; set; }            // end, Unix seconds

    public DateTime Start => DateTimeOffset.FromUnixTimeSeconds(StartUnix).LocalDateTime;
    public DateTime End => DateTimeOffset.FromUnixTimeSeconds(EndUnix).LocalDateTime;
    public double Seconds => Math.Max(0, EndUnix - StartUnix);
}

/// <summary>
/// 活動資料庫（本機 JSONL，永不離開裝置）· Local activity store.
/// <para>
/// 規格原本建議用 SQLite，但為咗喺 .NET 11 / WinUI 3 x64 下 100% build 得乾淨、唔加任何原生 NuGet
/// 相依（避免 trim／self-contained 風險），呢度改用一個極簡嘅、每日一個 JSON-lines（.jsonl）檔嘅
/// append-only store，放喺 <c>%LOCALAPPDATA%\WinForge\activity\</c>。功能上完全等價：時段持久化、跨重啟
/// 仍在、可按日期讀取、可匯出 CSV、可清除歷史。The spec suggested SQLite; to keep the build 100% clean
/// under .NET 11 / WinUI 3 x64 with zero native NuGet dependency (no trim/self-contained risk), this uses a
/// tiny append-only JSON-lines store, one file per day, under %LOCALAPPDATA%\WinForge\activity\. Functionally
/// equivalent: segments persist across restarts, are queryable by date, exportable to CSV, and clearable.
/// </para>
/// </summary>
public static class ActivityStore
{
    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "activity");

    private static readonly object Gate = new();

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private static string FileFor(DateOnly day) =>
        Path.Combine(Dir, $"{day:yyyy-MM-dd}.jsonl");

    /// <summary>追加一段時段 · Append one finished segment to its day's file.</summary>
    public static void Append(ActivitySegment seg)
    {
        if (seg is null || seg.Seconds <= 0) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                var day = DateOnly.FromDateTime(seg.Start);
                var line = JsonSerializer.Serialize(seg, JsonOpts);
                File.AppendAllText(FileFor(day), line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch { /* best effort — never crash tracking on a write error */ }
    }

    /// <summary>讀取某一日嘅全部時段 · Load every segment recorded for a given local day.</summary>
    public static List<ActivitySegment> Load(DateOnly day)
    {
        var list = new List<ActivitySegment>();
        try
        {
            lock (Gate)
            {
                var path = FileFor(day);
                if (!File.Exists(path)) return list;
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var seg = JsonSerializer.Deserialize<ActivitySegment>(line);
                        if (seg is not null && seg.Seconds > 0) list.Add(seg);
                    }
                    catch { /* skip a corrupt line */ }
                }
            }
        }
        catch { }
        return list.OrderBy(s => s.StartUnix).ToList();
    }

    /// <summary>有記錄嘅日子（有降序排列）· Dates that have any recorded data, newest first.</summary>
    public static List<DateOnly> RecordedDays()
    {
        var days = new List<DateOnly>();
        try
        {
            if (!Directory.Exists(Dir)) return days;
            foreach (var f in Directory.EnumerateFiles(Dir, "*.jsonl"))
            {
                var name = Path.GetFileNameWithoutExtension(f);
                if (DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var d))
                    days.Add(d);
            }
        }
        catch { }
        return days.OrderByDescending(d => d).ToList();
    }

    /// <summary>清除全部歷史 · Delete every recorded day. Returns how many day-files were removed.</summary>
    public static int ClearAll()
    {
        int n = 0;
        try
        {
            lock (Gate)
            {
                if (!Directory.Exists(Dir)) return 0;
                foreach (var f in Directory.EnumerateFiles(Dir, "*.jsonl").ToList())
                {
                    try { File.Delete(f); n++; } catch { }
                }
            }
        }
        catch { }
        return n;
    }

    /// <summary>清除單一日 · Delete one day's file. Returns true if removed.</summary>
    public static bool ClearDay(DateOnly day)
    {
        try
        {
            lock (Gate)
            {
                var path = FileFor(day);
                if (File.Exists(path)) { File.Delete(path); return true; }
            }
        }
        catch { }
        return false;
    }

    /// <summary>個資料夾路徑（畀「開啟資料夾」用）· Folder path for an "open folder" action.</summary>
    public static string Folder
    {
        get { try { Directory.CreateDirectory(Dir); } catch { } return Dir; }
    }

    // ===== aggregation helpers =====

    /// <summary>每個 app 嘅總時間（秒），降序 · Per-process totals (seconds) for a set of segments, descending.</summary>
    public static List<(string Process, double Seconds)> TotalsByProcess(IEnumerable<ActivitySegment> segs)
        => segs.GroupBy(s => string.IsNullOrWhiteSpace(s.Process) ? "Unknown" : s.Process)
               .Select(g => (g.Key, g.Sum(x => x.Seconds)))
               .OrderByDescending(t => t.Item2)
               .ToList();

    /// <summary>匯出 CSV 文字 · Build CSV text for a set of segments (UTF-8 with header).</summary>
    public static string ToCsv(IEnumerable<ActivitySegment> segs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("date,start,end,seconds,process,title");
        foreach (var s in segs.OrderBy(s => s.StartUnix))
        {
            sb.Append(s.Start.ToString("yyyy-MM-dd")).Append(',')
              .Append(s.Start.ToString("HH:mm:ss")).Append(',')
              .Append(s.End.ToString("HH:mm:ss")).Append(',')
              .Append(((long)s.Seconds).ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Csv(s.Process)).Append(',')
              .Append(Csv(s.Title))
              .Append('\n');
        }
        return sb.ToString();
    }

    private static string Csv(string? v)
    {
        v ??= "";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
