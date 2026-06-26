using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 網站複製器 · Website cloner — fetches a live page, downloads referenced assets, rewrites their
/// URLs to local relative paths and writes a browsable <c>index.html</c> + <c>/assets</c> tree.
/// 核心係原生 C#（HttpClient）；預覽用 app 內 WebView2，不會啟動外部瀏覽器、檔案總管或終端機代理。
/// Core is native C# (HttpClient). Preview stays inside the app via WebView2 and never launches
/// an external browser, folder, or terminal agent.
/// 全部防禦性寫法，唔會擲未捕捉嘅例外。Defensive throughout — never throws to the caller.
/// </summary>
public static class WebsiteClonerService
{
    /// <summary>下載上限（防止失控）· Hard caps so a runaway page can't fill the disk.</summary>
    public const int MaxAssets = 200;
    public const long MaxAssetBytes = 25 * 1024 * 1024;   // 25 MB per asset
    public const long MaxTotalBytes = 250 * 1024 * 1024;  // 250 MB total

    /// <summary>進度行嘅嚴重程度 · Severity for a progress line, used to colour the log.</summary>
    public enum LogLevel { Info, Ok, Warn, Error }

    /// <summary>一行進度 · One progress line passed to the UI as work proceeds.</summary>
    public sealed record Progress(string En, string Zh, LogLevel Level = LogLevel.Info);

    /// <summary>複製結果 · Outcome of a clone run.</summary>
    public sealed record CloneResult(
        bool Success,
        string? IndexPath,
        int AssetsSaved,
        int AssetsFailed,
        long TotalBytes,
        LocalizedText Message,
        IReadOnlyDictionary<string, string>? DesignTokens = null);

    private static readonly HttpClient Http = BuildClient();

    private static HttpClient BuildClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        };
        var c = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        // 用真實瀏覽器 UA，避免被簡單封鎖 · Real-browser UA so trivial bot blocks don't reject us.
        c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        c.DefaultRequestHeaders.TryAddWithoutValidation("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        return c;
    }

    // ===================== Public API =====================

    /// <summary>
    /// 用原生 fetch 複製一個頁面 · Clone a single page natively (no AI).
    /// <paramref name="rawHtml"/> 唔為空時會直接用佢做 DOM（例如 WebView2 dump），唔再 HttpClient 攞 HTML。
    /// When <paramref name="rawHtml"/> is provided it is used directly as the DOM (e.g. a WebView2 dump)
    /// instead of fetching the HTML over HttpClient — assets are still downloaded over HttpClient.
    /// </summary>
    public static async Task<CloneResult> CloneAsync(
        string url,
        string destFolder,
        bool downloadAssets,
        IProgress<Progress>? progress,
        CancellationToken ct,
        string? rawHtml = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url))
                return Failed("Enter a URL first.", "請先輸入網址。");
            if (string.IsNullOrWhiteSpace(destFolder))
                return Failed("Choose a destination folder first.", "請先揀目的資料夾。");

            if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
                return Failed("That doesn't look like a valid http(s) URL.", "呢個唔似有效嘅 http(s) 網址。");

            Directory.CreateDirectory(destFolder);
            var assetsDir = Path.Combine(destFolder, "assets");
            if (downloadAssets) Directory.CreateDirectory(assetsDir);

            string html;
            if (!string.IsNullOrEmpty(rawHtml))
            {
                Report(progress, "Using captured (rendered) DOM.", "用咗已擷取嘅（已渲染）DOM。", LogLevel.Ok);
                html = rawHtml!;
            }
            else
            {
                Report(progress, $"Fetching {baseUri} …", $"下載緊 {baseUri} …");
                using var resp = await Http.GetAsync(baseUri, HttpCompletionOption.ResponseContentRead, ct);
                if (!resp.IsSuccessStatusCode)
                    return Failed($"Server returned {(int)resp.StatusCode} {resp.StatusCode}.",
                        $"伺服器回應 {(int)resp.StatusCode} {resp.StatusCode}。");
                html = await resp.Content.ReadAsStringAsync(ct);
                Report(progress, $"Got {html.Length:N0} characters of HTML.", $"攞到 {html.Length:N0} 個字元嘅 HTML。", LogLevel.Ok);
            }

            // 抽設計 token（顏色／字型）· Extract design tokens (colours / fonts) for the summary.
            var tokens = ExtractDesignTokens(html);

            int saved = 0, failed = 0;
            long total = 0;
            var urlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (downloadAssets)
            {
                var refs = CollectAssetUrls(html, baseUri).Take(MaxAssets).ToList();
                Report(progress, $"Found {refs.Count} asset reference(s).", $"搵到 {refs.Count} 個資源引用。");

                int i = 0;
                foreach (var assetUrl in refs)
                {
                    ct.ThrowIfCancellationRequested();
                    if (total >= MaxTotalBytes)
                    {
                        Report(progress, "Total size cap reached — stopping asset download.",
                            "已達總大小上限 — 停止下載資源。", LogLevel.Warn);
                        break;
                    }
                    i++;
                    var (ok, localRel, bytes) = await DownloadAssetAsync(assetUrl, baseUri, assetsDir, ct);
                    if (ok && localRel is not null)
                    {
                        urlMap[assetUrl.AbsoluteUri] = localRel;
                        saved++; total += bytes;
                        Report(progress, $"[{i}/{refs.Count}] saved {Trunc(assetUrl.AbsoluteUri)}",
                            $"[{i}/{refs.Count}] 已存 {Trunc(assetUrl.AbsoluteUri)}", LogLevel.Ok);
                    }
                    else
                    {
                        failed++;
                        Report(progress, $"[{i}/{refs.Count}] skipped {Trunc(assetUrl.AbsoluteUri)}",
                            $"[{i}/{refs.Count}] 略過 {Trunc(assetUrl.AbsoluteUri)}", LogLevel.Warn);
                    }
                }
            }
            else
            {
                Report(progress, "Asset download disabled — saving HTML only.", "已停用資源下載 — 只儲存 HTML。", LogLevel.Warn);
            }

            // 改寫連結到本地相對路徑 · Rewrite links to local relative paths.
            var rewritten = RewriteHtml(html, baseUri, urlMap);

            var indexPath = Path.Combine(destFolder, "index.html");
            await File.WriteAllTextAsync(indexPath, rewritten, new UTF8Encoding(false), ct);
            Report(progress, "Wrote index.html.", "已寫入 index.html。", LogLevel.Ok);

            // 寫設計 token 摘要 · Write a design-token summary the user (and AI) can read.
            try
            {
                await File.WriteAllTextAsync(Path.Combine(destFolder, "design-tokens.txt"),
                    RenderTokens(baseUri, tokens), new UTF8Encoding(false), ct);
            }
            catch { }

            return new CloneResult(true, indexPath, saved, failed, total,
                new LocalizedText(
                    $"Clone complete — {saved} asset(s) saved, {failed} skipped.",
                    $"複製完成 — 已存 {saved} 個資源，略過 {failed} 個。"),
                tokens);
        }
        catch (OperationCanceledException)
        {
            return Failed("Cancelled.", "已取消。");
        }
        catch (Exception ex)
        {
            return Failed($"Clone failed: {ex.Message}", $"複製失敗：{ex.Message}");
        }
    }

    // ===================== Asset collection & download =====================

    /// <summary>由 HTML 抽出所有資源 URL（img/css/js/font/srcset/CSS url()）· Collect asset URLs.</summary>
    private static IEnumerable<Uri> CollectAssetUrls(string html, Uri baseUri)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<Uri>();

        void Add(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            raw = raw.Trim().Trim('"', '\'');
            if (raw.Length == 0 || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("#") || raw.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || raw.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)) return;
            if (!Uri.TryCreate(baseUri, raw, out var abs)) return;
            if (abs.Scheme != Uri.UriSchemeHttp && abs.Scheme != Uri.UriSchemeHttps) return;
            if (seen.Add(abs.AbsoluteUri)) result.Add(abs);
        }

        // src / href / data-src
        foreach (Match m in Regex.Matches(html,
            @"(?:src|href|data-src|data-srcset|poster)\s*=\s*(?:""([^""]*)""|'([^']*)')",
            RegexOptions.IgnoreCase))
        {
            var v = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            Add(v);
        }

        // srcset (comma-separated "url 1x, url 2x")
        foreach (Match m in Regex.Matches(html, @"srcset\s*=\s*(?:""([^""]*)""|'([^']*)')", RegexOptions.IgnoreCase))
        {
            var v = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            foreach (var part in v.Split(','))
            {
                var u = part.Trim().Split(' ', '\t').FirstOrDefault();
                Add(u);
            }
        }

        // CSS url(...) in inline <style> and style="" attributes
        foreach (Match m in Regex.Matches(html, @"url\(\s*(?:""([^""]*)""|'([^']*)'|([^)]*))\s*\)", RegexOptions.IgnoreCase))
        {
            var v = m.Groups[1].Success ? m.Groups[1].Value
                  : m.Groups[2].Success ? m.Groups[2].Value
                  : m.Groups[3].Value;
            Add(v);
        }

        return result;
    }

    private static async Task<(bool ok, string? localRel, long bytes)> DownloadAssetAsync(
        Uri assetUrl, Uri baseUri, string assetsDir, CancellationToken ct)
    {
        try
        {
            using var resp = await Http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return (false, null, 0);

            var len = resp.Content.Headers.ContentLength ?? 0;
            if (len > MaxAssetBytes) return (false, null, 0);

            var data = await resp.Content.ReadAsByteArrayAsync(ct);
            if (data.LongLength > MaxAssetBytes) return (false, null, 0);

            var fileName = SafeFileName(assetUrl, resp.Content.Headers.ContentType?.MediaType);
            var full = Path.Combine(assetsDir, fileName);

            // 避免覆蓋同名檔 · Avoid clobbering different files that share a name.
            full = Unique(full);
            await File.WriteAllBytesAsync(full, data, ct);

            return (true, "assets/" + Path.GetFileName(full), data.LongLength);
        }
        catch { return (false, null, 0); }
    }

    private static string SafeFileName(Uri url, string? mediaType)
    {
        var name = Path.GetFileName(url.LocalPath);
        if (string.IsNullOrWhiteSpace(name)) name = "asset";
        // 去除查詢字串／非法字元 · Strip query and illegal chars.
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        name = name.Trim('.', '_', ' ');
        if (string.IsNullOrWhiteSpace(name)) name = "asset";

        if (!Path.HasExtension(name))
        {
            var ext = ExtForMedia(mediaType);
            if (ext is not null) name += ext;
        }
        if (name.Length > 120) name = name[^120..];
        return name;
    }

    private static string? ExtForMedia(string? media) => media?.ToLowerInvariant() switch
    {
        "text/css" => ".css",
        "text/javascript" or "application/javascript" => ".js",
        "image/png" => ".png",
        "image/jpeg" => ".jpg",
        "image/gif" => ".gif",
        "image/svg+xml" => ".svg",
        "image/webp" => ".webp",
        "font/woff2" or "application/font-woff2" => ".woff2",
        "font/woff" => ".woff",
        "font/ttf" => ".ttf",
        _ => null,
    };

    private static string Unique(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; i < 10000; i++)
        {
            var cand = Path.Combine(dir, $"{stem}_{i}{ext}");
            if (!File.Exists(cand)) return cand;
        }
        return path;
    }

    // ===================== HTML rewriting =====================

    /// <summary>改寫 HTML 入面嘅資源連結到本地路徑 · Rewrite asset links in the HTML to local paths.</summary>
    private static string RewriteHtml(string html, Uri baseUri, IReadOnlyDictionary<string, string> urlMap)
    {
        if (urlMap.Count == 0) return EnsureUtf8Meta(html);

        string ReplaceUrl(string raw)
        {
            var v = raw.Trim().Trim('"', '\'');
            if (Uri.TryCreate(baseUri, v, out var abs) && urlMap.TryGetValue(abs.AbsoluteUri, out var local))
                return local;
            return raw;
        }

        // src/href/poster/data-src
        html = Regex.Replace(html,
            @"(?<attr>\b(?:src|href|data-src|poster)\s*=\s*)(?:""(?<u>[^""]*)""|'(?<u2>[^']*)')",
            m =>
            {
                var u = m.Groups["u"].Success ? m.Groups["u"].Value : m.Groups["u2"].Value;
                return $"{m.Groups["attr"].Value}\"{ReplaceUrl(u)}\"";
            }, RegexOptions.IgnoreCase);

        // srcset
        html = Regex.Replace(html, @"(?<attr>\bsrcset\s*=\s*)(?:""(?<u>[^""]*)""|'(?<u2>[^']*)')",
            m =>
            {
                var u = m.Groups["u"].Success ? m.Groups["u"].Value : m.Groups["u2"].Value;
                var parts = u.Split(',').Select(p =>
                {
                    var seg = p.Trim();
                    var bits = seg.Split(' ', '\t');
                    if (bits.Length == 0) return seg;
                    bits[0] = ReplaceUrl(bits[0]);
                    return string.Join(' ', bits);
                });
                return $"{m.Groups["attr"].Value}\"{string.Join(", ", parts)}\"";
            }, RegexOptions.IgnoreCase);

        // CSS url()
        html = Regex.Replace(html, @"url\(\s*(?:""([^""]*)""|'([^']*)'|([^)]*))\s*\)",
            m =>
            {
                var u = m.Groups[1].Success ? m.Groups[1].Value
                      : m.Groups[2].Success ? m.Groups[2].Value
                      : m.Groups[3].Value;
                return $"url(\"{ReplaceUrl(u)}\")";
            }, RegexOptions.IgnoreCase);

        return EnsureUtf8Meta(html);
    }

    /// <summary>確保有 UTF-8 meta，令本地開啟時中文唔會亂碼 · Ensure a UTF-8 meta tag.</summary>
    private static string EnsureUtf8Meta(string html)
    {
        if (Regex.IsMatch(html, @"<meta[^>]+charset", RegexOptions.IgnoreCase)) return html;
        return Regex.Replace(html, "<head[^>]*>",
            m => m.Value + "\n  <meta charset=\"utf-8\">", RegexOptions.IgnoreCase);
    }

    // ===================== Design tokens =====================

    /// <summary>抽顏色同字型 token · Extract colour and font-family tokens for a quick summary.</summary>
    private static Dictionary<string, string> ExtractDesignTokens(string html)
    {
        var tokens = new Dictionary<string, string>();
        try
        {
            var colours = new List<string>();
            foreach (Match m in Regex.Matches(html, @"#[0-9a-fA-F]{6}\b|#[0-9a-fA-F]{3}\b|rgba?\([^)]*\)"))
                colours.Add(m.Value);
            var topColours = colours.GroupBy(c => c.ToLowerInvariant())
                .OrderByDescending(g => g.Count()).Take(8).Select(g => g.Key);
            tokens["colors"] = string.Join(", ", topColours);

            var fonts = new List<string>();
            foreach (Match m in Regex.Matches(html, @"font-family\s*:\s*([^;""}]+)", RegexOptions.IgnoreCase))
                fonts.Add(m.Groups[1].Value.Trim());
            var topFonts = fonts.GroupBy(f => f.ToLowerInvariant())
                .OrderByDescending(g => g.Count()).Take(5).Select(g => g.Key);
            tokens["fonts"] = string.Join(" | ", topFonts);

            var title = Regex.Match(html, @"<title[^>]*>(.*?)</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (title.Success) tokens["title"] = WebUtility.HtmlDecode(title.Groups[1].Value.Trim());
        }
        catch { }
        return tokens;
    }

    private static string RenderTokens(Uri url, IReadOnlyDictionary<string, string> tokens)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Design tokens · 設計符記");
        sb.AppendLine($"Source · 來源: {url}");
        sb.AppendLine();
        foreach (var kv in tokens)
            sb.AppendLine($"{kv.Key}: {kv.Value}");
        return sb.ToString();
    }

    // ===================== helpers =====================

    private static void Report(IProgress<Progress>? p, string en, string zh, LogLevel level = LogLevel.Info)
        => p?.Report(new Progress(en, zh, level));

    private static CloneResult Failed(string en, string zh)
        => new(false, null, 0, 0, 0, new LocalizedText(en, zh));

    private static string Trunc(string s, int max = 70)
        => s.Length <= max ? s : s[..(max - 1)] + "…";
}
