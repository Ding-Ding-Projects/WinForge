using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個可下載格式 · One downloadable format row parsed from `yt-dlp -F`.</summary>
public sealed class YtDlpFormat
{
    public string Id { get; set; } = "";
    public string Ext { get; set; } = "";
    public string Resolution { get; set; } = "";
    public string Note { get; set; } = "";
    public string Raw { get; set; } = "";

    /// <summary>List display: "137  mp4  1920x1080  …" · 格式列顯示。</summary>
    public string Display => Raw;
}

/// <summary>下載進度快照 · A single live-progress snapshot parsed from yt-dlp stdout.</summary>
public sealed class YtDlpProgress
{
    public double Percent { get; set; }     // 0..100
    public string Speed { get; set; } = "";
    public string Eta { get; set; } = "";
    public string TotalSize { get; set; } = "";
    public string Stage { get; set; } = ""; // download / processing / merging …
}

/// <summary>
/// 包住 yt-dlp 命令列 · A thin wrapper over the yt-dlp CLI (github.com/yt-dlp/yt-dlp).
/// 建立參數、串流即時進度、解析 -F 格式表、定位 ffmpeg、處理更新／清快取。
/// Builds arg lists, streams live progress, parses the -F format table, locates ffmpeg,
/// and handles update / clear-cache. No redirect — WinForge wraps the real engine. Bilingual.
/// </summary>
public static class YtDlpService
{
    public const string WingetId = "yt-dlp.yt-dlp";
    public const string FfmpegWingetId = "Gyan.FFmpeg";

    private static string? _exe;
    private static Process? _proc;

    // ───────────────────────── engine discovery ─────────────────────────

    private static string Which(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(';'))
        {
            try
            {
                var full = Path.Combine(dir.Trim(), exe);
                if (File.Exists(full)) return full;
            }
            catch { /* bad PATH entry */ }
        }
        return exe; // let the OS resolve at run time
    }

    public static string Exe => _exe ??= Which("yt-dlp.exe");

    /// <summary>清快取，等啱啱裝完嘅 yt-dlp 即刻搵到 · Clear the cached path after a fresh install.</summary>
    public static void Rescan() { _exe = null; }

    public static async Task<bool> IsAvailable(CancellationToken ct = default)
    {
        try { return !string.IsNullOrWhiteSpace(await Version(ct)); }
        catch { return false; }
    }

    public static async Task<string> Version(CancellationToken ct = default)
    {
        var r = await ShellRunner.Run(Exe, "--version", false, ct);
        return r.Success ? (r.Output ?? "").Trim() : "";
    }

    /// <summary>定位 ffmpeg.exe（與 Media 模組共用）· Locate ffmpeg (shared with the Media module).</summary>
    public static string FfmpegPath => MediaService.FFmpeg;

    public static bool HasFfmpeg => File.Exists(FfmpegPath);

    private static string Q(string s) => $"\"{s}\"";

    /// <summary>若搵到 ffmpeg 就傳 --ffmpeg-location · Append --ffmpeg-location when ffmpeg is on disk.</summary>
    private static string FfmpegArg()
    {
        var f = FfmpegPath;
        if (File.Exists(f))
        {
            var dir = Path.GetDirectoryName(f);
            if (!string.IsNullOrEmpty(dir)) return $" --ffmpeg-location {Q(dir)}";
        }
        return "";
    }

    // ───────────────────────── format listing (-F) ─────────────────────────

    /// <summary>跑 `yt-dlp -F` 並解析格式表 · Run `yt-dlp -F <url>` and parse the format table.</summary>
    public static async Task<(List<YtDlpFormat> Formats, string Raw)> ListFormats(string url, CancellationToken ct = default)
    {
        var r = await ShellRunner.Run(Exe, $"--no-color -F {Q(url)}", false, ct);
        var raw = r.Output ?? "";
        return (ParseFormats(raw), raw);
    }

    /// <summary>解析 -F 表 · Parse the `-F` table defensively (format can shift between versions).</summary>
    public static List<YtDlpFormat> ParseFormats(string raw)
    {
        var list = new List<YtDlpFormat>();
        if (string.IsNullOrWhiteSpace(raw)) return list;

        bool started = false;
        foreach (var rawLine in raw.Replace("\r", "").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0) continue;
            // The table header begins with "ID" and a separator row of dashes follows.
            if (!started)
            {
                if (Regex.IsMatch(line, @"^\s*ID\s+EXT", RegexOptions.IgnoreCase)) { started = true; }
                continue;
            }
            if (line.TrimStart().StartsWith("---") || line.TrimStart().StartsWith("[")) continue;

            var cols = line.TrimStart().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (cols.Length < 2) continue;

            var id = cols[0];
            var ext = cols[1];
            // skip lines that clearly aren't format rows
            if (id.Length == 0 || id.Contains('=')) continue;

            var res = cols.Length > 2 ? cols[2] : "";
            // everything after the first 3 cols is a human note (codecs, size, etc.)
            var note = cols.Length > 3 ? string.Join(" ", cols, 3, cols.Length - 3) : "";

            list.Add(new YtDlpFormat
            {
                Id = id,
                Ext = ext,
                Resolution = res,
                Note = note,
                Raw = line.TrimStart(),
            });
        }
        return list;
    }

    // ───────────────────────── download (streamed) ─────────────────────────

    public sealed class DownloadOptions
    {
        public string Url { get; set; } = "";
        public string FormatSelector { get; set; } = "";   // -f value, e.g. "bv*+ba/b" or "137+140" or ""
        public string OutputDir { get; set; } = "";         // -P
        public string Template { get; set; } = "%(title)s [%(id)s].%(ext)s"; // -o
        public bool AudioOnly { get; set; }
        public string AudioFormat { get; set; } = "mp3";    // -x --audio-format
        public bool Subtitles { get; set; }
        public string SubLangs { get; set; } = "en,zh";
        public bool EmbedThumbnail { get; set; }
        public bool EmbedMetadata { get; set; }
        public string PlaylistItems { get; set; } = "";     // --playlist-items
        public bool UseArchive { get; set; }                // --download-archive
        public string ArchivePath { get; set; } = "";
        public bool SponsorBlock { get; set; }              // --sponsorblock-remove all
        public string CookiesBrowser { get; set; } = "";    // --cookies-from-browser
    }

    public static bool IsRunning => _proc is { HasExited: false };

    /// <summary>組裝下載參數 · Build the full yt-dlp argument string for a download.</summary>
    public static string BuildArgs(DownloadOptions o)
    {
        var sb = new StringBuilder();
        sb.Append("--no-color --newline --progress");

        // format selection
        if (o.AudioOnly)
        {
            sb.Append(" -x --audio-format ").Append(string.IsNullOrWhiteSpace(o.AudioFormat) ? "mp3" : o.AudioFormat);
        }
        else if (!string.IsNullOrWhiteSpace(o.FormatSelector))
        {
            sb.Append(" -f ").Append(Q(o.FormatSelector));
        }

        // output template + folder
        if (!string.IsNullOrWhiteSpace(o.Template)) sb.Append(" -o ").Append(Q(o.Template));
        if (!string.IsNullOrWhiteSpace(o.OutputDir)) sb.Append(" -P ").Append(Q(o.OutputDir));

        if (o.Subtitles)
        {
            sb.Append(" --write-subs --write-auto-subs");
            if (!string.IsNullOrWhiteSpace(o.SubLangs)) sb.Append(" --sub-langs ").Append(Q(o.SubLangs));
            sb.Append(" --embed-subs");
        }
        if (o.EmbedThumbnail) sb.Append(" --embed-thumbnail");
        if (o.EmbedMetadata) sb.Append(" --embed-metadata");

        if (!string.IsNullOrWhiteSpace(o.PlaylistItems)) sb.Append(" --playlist-items ").Append(o.PlaylistItems.Replace(" ", ""));
        if (o.UseArchive && !string.IsNullOrWhiteSpace(o.ArchivePath)) sb.Append(" --download-archive ").Append(Q(o.ArchivePath));
        if (o.SponsorBlock) sb.Append(" --sponsorblock-remove all");
        if (!string.IsNullOrWhiteSpace(o.CookiesBrowser)) sb.Append(" --cookies-from-browser ").Append(o.CookiesBrowser);

        sb.Append(FfmpegArg());
        sb.Append(' ').Append(Q(o.Url));
        return sb.ToString();
    }

    private static readonly Regex ProgressRx = new(
        @"\[download\]\s+(?<pct>\d{1,3}(?:\.\d+)?)%\s+of\s+~?\s*(?<size>[\d.]+\s*\w+)?(?:.*?at\s+(?<speed>[\d.]+\s*\w+/s|Unknown\s*B/s))?(?:.*?ETA\s+(?<eta>[\d:]+|Unknown))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 串流下載 · Run a download streaming live progress. <paramref name="onProgress"/> fires on parsed
    /// `[download] xx.x%` lines; <paramref name="onLine"/> gets every raw line for the log pane; both on a
    /// background thread (caller marshals to UI). Returns the final result.
    /// </summary>
    public static async Task<TweakResult> Download(DownloadOptions o, Action<YtDlpProgress> onProgress,
        Action<string> onLine, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(o.Url))
            return TweakResult.Fail("No URL provided.", "未輸入網址。");

        var args = BuildArgs(o);
        return await RunStreamed(args, onProgress, onLine, ct);
    }

    private static async Task<TweakResult> RunStreamed(string args, Action<YtDlpProgress>? onProgress,
        Action<string> onLine, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Exe,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proc = p;

            void Handle(string? data)
            {
                if (data is null) return;
                onLine(data);
                if (onProgress is not null)
                {
                    var m = ProgressRx.Match(data);
                    if (m.Success && double.TryParse(m.Groups["pct"].Value, out var pct))
                    {
                        onProgress(new YtDlpProgress
                        {
                            Percent = pct,
                            Speed = m.Groups["speed"].Value.Trim(),
                            Eta = m.Groups["eta"].Value.Trim(),
                            TotalSize = m.Groups["size"].Value.Trim(),
                            Stage = "download",
                        });
                    }
                    else if (data.Contains("[Merger]") || data.Contains("Merging"))
                        onProgress(new YtDlpProgress { Percent = 100, Stage = "merging" });
                    else if (data.Contains("[ExtractAudio]") || data.Contains("[EmbedThumbnail]") ||
                             data.Contains("[Metadata]") || data.Contains("[FixupM"))
                        onProgress(new YtDlpProgress { Percent = 100, Stage = "processing" });
                }
            }

            p.OutputDataReceived += (_, e) => Handle(e.Data);
            p.ErrorDataReceived += (_, e) => Handle(e.Data);

            if (!p.Start()) { _proc = null; return TweakResult.Fail("Failed to start yt-dlp.", "無法啟動 yt-dlp。"); }
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            using (ct.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } }))
            {
                await p.WaitForExitAsync(CancellationToken.None);
            }

            int code = p.ExitCode;
            _proc = null;
            try { p.Dispose(); } catch { }

            if (ct.IsCancellationRequested) return TweakResult.Fail("Cancelled.", "已取消。");
            return code == 0
                ? TweakResult.Ok("Download complete.", "下載完成。")
                : TweakResult.Fail($"yt-dlp exited with code {code}.", $"yt-dlp 結束代碼 {code}。");
        }
        catch (Exception ex)
        {
            _proc = null;
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>取消進行中嘅下載 · Cancel/kill the in-flight download.</summary>
    public static void Cancel()
    {
        var p = _proc;
        _proc = null;
        try { if (p is { HasExited: false }) p.Kill(true); } catch { }
        try { p?.Dispose(); } catch { }
    }

    // ───────────────────────── maintenance ─────────────────────────

    /// <summary>更新 yt-dlp 自己 · Self-update yt-dlp (`-U`).</summary>
    public static Task<TweakResult> Update(CancellationToken ct = default)
        => ShellRunner.Run(Exe, "-U", false, ct);

    /// <summary>清除 yt-dlp 快取 · Clear yt-dlp's cache (`--rm-cache-dir`).</summary>
    public static Task<TweakResult> ClearCache(CancellationToken ct = default)
        => ShellRunner.Run(Exe, "--rm-cache-dir", false, ct);

    /// <summary>取得標題（畀預覽用）· Fetch the title for a quick probe/preview.</summary>
    public static async Task<string> GetTitle(string url, CancellationToken ct = default)
    {
        var r = await ShellRunner.Run(Exe, $"--no-color --no-warnings --print title {Q(url)}", false, ct);
        return r.Success ? (r.Output ?? "").Trim() : "";
    }
}
