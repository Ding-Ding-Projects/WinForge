using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// HAR（HTTP Archive）分析器 · HAR file analyzer. Pure managed parsing of the HTTP Archive JSON
/// format (log.entries) with System.Text.Json. 全部用 System.Text.Json 解析，唔會拋例外 — 出錯
///會回傳一個帶錯誤訊息嘅結果。Everything is guarded: malformed / huge / truncated HAR never
/// throws; the caller gets a result carrying an <see cref="Error"/> instead.
/// </summary>
public static class HarAnalyzerService
{
    /// <summary>一個 HAR 請求記錄 · One parsed HAR entry (a single HTTP request/response).</summary>
    public sealed class HarEntry
    {
        public string Method { get; set; } = "";
        public int Status { get; set; }
        public string Url { get; set; } = "";
        public string Mime { get; set; } = "";
        public long Size { get; set; }          // response body bytes (best effort)
        public double TimeMs { get; set; }       // total time for the request
        public string StatusText { get; set; } = "";

        // ---- bindable display helpers (classic {Binding}) ----
        public string StatusDisplay => Status <= 0 ? "—" : Status.ToString(CultureInfo.InvariantCulture);
        public string SizeDisplay => HumanBytes(Size);
        public string TimeDisplay => TimeMs <= 0 ? "—" : TimeMs.ToString("0", CultureInfo.InvariantCulture) + " ms";
        public string MimeDisplay => string.IsNullOrWhiteSpace(Mime) ? "—" : Mime;

        /// <summary>Colour bucket for the status cell — "2","3","4","5" or "" (unknown).</summary>
        public string StatusClass
        {
            get
            {
                if (Status >= 200 && Status < 300) return "2";
                if (Status >= 300 && Status < 400) return "3";
                if (Status >= 400 && Status < 500) return "4";
                if (Status >= 500 && Status < 600) return "5";
                return "";
            }
        }

        /// <summary>綁定用嘅狀態顏色 · Brush for the status cell so the DataTemplate can bind it directly.</summary>
        public Microsoft.UI.Xaml.Media.SolidColorBrush StatusBrush
            => new(StatusClass switch
            {
                "2" => Windows.UI.Color.FromArgb(0xFF, 0x4C, 0xC2, 0x6B), // green
                "3" => Windows.UI.Color.FromArgb(0xFF, 0x5B, 0x9B, 0xE8), // blue
                "4" => Windows.UI.Color.FromArgb(0xFF, 0xE8, 0xA5, 0x3B), // amber
                "5" => Windows.UI.Color.FromArgb(0xFF, 0xE6, 0x53, 0x53), // red
                _   => Windows.UI.Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A), // grey
            });
    }

    /// <summary>整體分析結果 · The full analysis result. When <see cref="Error"/> is set, the rest is empty.</summary>
    public sealed class HarResult
    {
        public List<HarEntry> Entries { get; } = new();
        public int TotalRequests { get; set; }
        public long TotalBytes { get; set; }
        public double TotalTimeMs { get; set; }
        public double PageLoadMs { get; set; }   // onLoad from pages[0].pageTimings, if present
        public int C2xx { get; set; }
        public int C3xx { get; set; }
        public int C4xx { get; set; }
        public int C5xx { get; set; }
        public int COther { get; set; }
        public Dictionary<string, int> ByType { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HarEntry? Slowest { get; set; }
        public HarEntry? Largest { get; set; }
        public string? Error { get; set; }

        public bool Ok => Error is null;
    }

    /// <summary>由檔案讀取並分析 · Read a .har file and analyze it. Never throws.</summary>
    public static async Task<HarResult> AnalyzeFileAsync(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return Fail("File not found · 揾唔到檔案");

            string text;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true))
            using (var sr = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                text = await sr.ReadToEndAsync().ConfigureAwait(false);

            return Analyze(text);
        }
        catch (Exception ex)
        {
            return Fail(ex.Message);
        }
    }

    /// <summary>由 JSON 字串分析 · Analyze a HAR JSON string. Never throws.</summary>
    public static HarResult Analyze(string? json)
    {
        var r = new HarResult();
        if (string.IsNullOrWhiteSpace(json))
            return Fail("Empty input · 內容係空嘅");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            return Fail("Not valid JSON · 唔係有效 JSON：" + ex.Message);
        }

        using (doc)
        {
            try
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                    !doc.RootElement.TryGetProperty("log", out var log) ||
                    log.ValueKind != JsonValueKind.Object)
                    return Fail("Missing \"log\" object — is this a HAR file? · 唔見到 \"log\"，係咪 HAR 檔？");

                // Optional page load time from the first page.
                if (log.TryGetProperty("pages", out var pages) && pages.ValueKind == JsonValueKind.Array)
                {
                    foreach (var page in pages.EnumerateArray())
                    {
                        if (page.ValueKind == JsonValueKind.Object &&
                            page.TryGetProperty("pageTimings", out var pt) && pt.ValueKind == JsonValueKind.Object &&
                            TryNum(pt, "onLoad", out var onLoad) && onLoad > 0)
                        {
                            r.PageLoadMs = onLoad;
                            break;
                        }
                    }
                }

                if (!log.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                    return Fail("No \"log.entries\" array · 冇 \"log.entries\" 陣列");

                foreach (var e in entries.EnumerateArray())
                {
                    if (e.ValueKind != JsonValueKind.Object) continue;
                    var entry = ParseEntry(e);
                    if (entry is null) continue;

                    r.Entries.Add(entry);
                    r.TotalRequests++;
                    if (entry.Size > 0) r.TotalBytes += entry.Size;
                    if (entry.TimeMs > 0) r.TotalTimeMs += entry.TimeMs;

                    switch (entry.StatusClass)
                    {
                        case "2": r.C2xx++; break;
                        case "3": r.C3xx++; break;
                        case "4": r.C4xx++; break;
                        case "5": r.C5xx++; break;
                        default: r.COther++; break;
                    }

                    var bucket = TypeBucket(entry.Mime);
                    r.ByType[bucket] = r.ByType.TryGetValue(bucket, out var c) ? c + 1 : 1;

                    if (r.Slowest is null || entry.TimeMs > r.Slowest.TimeMs) r.Slowest = entry;
                    if (r.Largest is null || entry.Size > r.Largest.Size) r.Largest = entry;
                }

                if (r.TotalRequests == 0)
                    return Fail("HAR parsed, but it has 0 entries · HAR 讀到喇，但係冇任何請求");

                return r;
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }
    }

    private static HarEntry? ParseEntry(JsonElement e)
    {
        try
        {
            var entry = new HarEntry();

            if (e.TryGetProperty("request", out var req) && req.ValueKind == JsonValueKind.Object)
            {
                entry.Method = Str(req, "method");
                entry.Url = Str(req, "url");
            }

            if (e.TryGetProperty("response", out var res) && res.ValueKind == JsonValueKind.Object)
            {
                if (TryNum(res, "status", out var st)) entry.Status = (int)st;
                entry.StatusText = Str(res, "statusText");

                // Prefer content.mimeType / content.size, fall back to bodySize/_transferSize.
                if (res.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object)
                {
                    entry.Mime = Str(content, "mimeType");
                    if (TryNum(content, "size", out var sz) && sz > 0) entry.Size = (long)sz;
                }
                if (entry.Size <= 0 && TryNum(res, "bodySize", out var body) && body > 0) entry.Size = (long)body;
                if (entry.Size <= 0 && TryNum(res, "_transferSize", out var xfer) && xfer > 0) entry.Size = (long)xfer;
            }

            if (entry.Size <= 0 && TryNum(e, "_transferSize", out var xfer2) && xfer2 > 0) entry.Size = (long)xfer2;
            if (TryNum(e, "time", out var t) && t > 0) entry.TimeMs = t;

            // Trim a query-less mime like "text/html; charset=utf-8" → "text/html".
            int semi = entry.Mime.IndexOf(';');
            if (semi > 0) entry.Mime = entry.Mime[..semi].Trim();

            return entry;
        }
        catch
        {
            return null; // skip a single malformed entry rather than aborting the whole file
        }
    }

    /// <summary>整理一份純文字報告 · Build a plain-text report for the clipboard.</summary>
    public static string BuildReport(HarResult r, Func<string, string, string> pick)
    {
        var sb = new StringBuilder();
        sb.AppendLine(pick("HAR Analysis Report", "HAR 分析報告"));
        sb.AppendLine(new string('=', 40));
        sb.AppendLine(pick("Total requests", "請求總數") + ": " + r.TotalRequests.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine(pick("Total transfer", "傳輸總量") + ": " + HumanBytes(r.TotalBytes));
        sb.AppendLine(pick("Total time", "總時間") + ": " + r.TotalTimeMs.ToString("0", CultureInfo.InvariantCulture) + " ms");
        if (r.PageLoadMs > 0)
            sb.AppendLine(pick("Page load (onLoad)", "頁面載入 (onLoad)") + ": " + r.PageLoadMs.ToString("0", CultureInfo.InvariantCulture) + " ms");
        sb.AppendLine();
        sb.AppendLine(pick("By status class", "按狀態分類") + ":");
        sb.AppendLine("  2xx: " + r.C2xx + "   3xx: " + r.C3xx + "   4xx: " + r.C4xx + "   5xx: " + r.C5xx +
            (r.COther > 0 ? "   " + pick("other", "其他") + ": " + r.COther : ""));
        sb.AppendLine();
        sb.AppendLine(pick("By type", "按類型") + ":");
        foreach (var kv in r.ByType.OrderByDescending(k => k.Value))
            sb.AppendLine("  " + kv.Key + ": " + kv.Value);
        sb.AppendLine();
        if (r.Slowest is not null)
            sb.AppendLine(pick("Slowest", "最慢") + ": " + r.Slowest.TimeDisplay + "  " + r.Slowest.Url);
        if (r.Largest is not null)
            sb.AppendLine(pick("Largest", "最大") + ": " + r.Largest.SizeDisplay + "  " + r.Largest.Url);
        sb.AppendLine();
        sb.AppendLine(pick("Entries", "請求列表") + ":");
        foreach (var e in r.Entries)
            sb.AppendLine($"  {e.Method,-6} {e.StatusDisplay,4}  {e.SizeDisplay,10}  {e.TimeDisplay,9}  {e.Url}");
        return sb.ToString();
    }

    // ===== small helpers =====

    private static HarResult Fail(string msg)
    {
        var r = new HarResult { Error = msg };
        return r;
    }

    private static string Str(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "") : "";

    private static bool TryNum(JsonElement obj, string name, out double value)
    {
        value = 0;
        if (!obj.TryGetProperty(name, out var v)) return false;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) { value = d; return true; }
        if (v.ValueKind == JsonValueKind.String &&
            double.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s)) { value = s; return true; }
        return false;
    }

    /// <summary>由 MIME 歸類 · Bucket a mime type into a coarse resource type.</summary>
    private static string TypeBucket(string mime)
    {
        if (string.IsNullOrWhiteSpace(mime)) return "other";
        mime = mime.ToLowerInvariant();
        if (mime.Contains("html")) return "html";
        if (mime.Contains("css")) return "css";
        if (mime.Contains("javascript") || mime.Contains("ecmascript") || mime.EndsWith("/json") || mime.Contains("+json")) return "script/json";
        if (mime.StartsWith("image/")) return "image";
        if (mime.StartsWith("font/") || mime.Contains("font")) return "font";
        if (mime.StartsWith("video/") || mime.StartsWith("audio/")) return "media";
        if (mime.StartsWith("text/")) return "text";
        return "other";
    }

    /// <summary>人類可讀嘅位元組 · Human-readable byte count.</summary>
    public static string HumanBytes(long bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return (i == 0 ? v.ToString("0", CultureInfo.InvariantCulture) : v.ToString("0.0", CultureInfo.InvariantCulture)) + " " + units[i];
    }
}
