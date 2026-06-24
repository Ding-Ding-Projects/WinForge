using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// 種類分類 · The kind of rich preview a file resolves to. Mirrors the file types that
/// PowerToys' File Explorer add-ons / Preview Pane handlers cover.
/// </summary>
public enum PreviewKind
{
    Svg,        // 向量圖（WebView2 渲染）· SVG vector image rendered in WebView2
    Markdown,   // Markdown（渲染成 HTML）· Markdown rendered to HTML
    Pdf,        // PDF（WebView2 內建檢視器）· PDF in the WebView2 built-in viewer
    Code,       // 原始碼／開發檔（等寬 + token 著色）· source / dev files, monospace token view
    Developer,  // .json/.xml/.yaml/.toml（美化）· developer data formats, pretty-printed
    Gcode,      // G-code（文字 + 統計 + 內嵌縮圖）· G-code text + stats + embedded thumbnail
    Qoi,        // QOI 影像（解碼成點陣圖）· QOI image decoded to a bitmap
    Image,      // 一般點陣圖（WinUI 原生支援）· ordinary raster images
    Unsupported // 唔支援 · not a previewable type
}

/// <summary>檔案類型描述 · Metadata about one preview-able file type.</summary>
public sealed record PreviewType(
    string Id,            // 設定鍵 · stable settings key, e.g. "svg"
    PreviewKind Kind,
    string En,
    string Zh,
    string Glyph,
    string[] Extensions);

/// <summary>
/// 豐富預覽服務 · The engine behind the Rich Preview module. Pure, UI-free logic:
/// file-type classification, per-type enable toggles (persisted in SettingsStore),
/// folder navigation (prev/next), QOI decoding, G-code stats + embedded-thumbnail
/// extraction, a dependency-free Markdown→HTML converter, and pretty-printers for the
/// developer data formats. Adapted from the PowerToys preview-pane handler set.
/// </summary>
public static class RichPreviewService
{
    // ===================== 類型登記 · type registry =====================

    public static readonly IReadOnlyList<PreviewType> Types = new List<PreviewType>
    {
        new("svg", PreviewKind.Svg, "SVG vector images", "SVG 向量圖", "",
            new[] { ".svg", ".svgz" }),
        new("markdown", PreviewKind.Markdown, "Markdown documents", "Markdown 文件", "",
            new[] { ".md", ".markdown", ".mdown", ".mkd", ".mdwn", ".mdtxt" }),
        new("pdf", PreviewKind.Pdf, "PDF documents", "PDF 文件", "",
            new[] { ".pdf" }),
        new("developer", PreviewKind.Developer, "Developer data (JSON / XML / YAML / TOML)", "開發者資料（JSON／XML／YAML／TOML）", "",
            new[] { ".json", ".jsonc", ".json5", ".xml", ".xaml", ".csproj", ".props", ".targets",
                    ".yaml", ".yml", ".toml" }),
        new("code", PreviewKind.Code, "Source code & dev files", "原始碼與開發檔", "",
            new[] { ".cs", ".c", ".h", ".cpp", ".hpp", ".cc", ".java", ".js", ".jsx", ".ts", ".tsx",
                    ".py", ".rb", ".go", ".rs", ".php", ".swift", ".kt", ".kts", ".scala", ".sh",
                    ".bash", ".zsh", ".ps1", ".psm1", ".bat", ".cmd", ".sql", ".lua", ".pl", ".r",
                    ".dart", ".vb", ".fs", ".fsx", ".clj", ".ex", ".exs", ".elm", ".hs", ".m", ".mm",
                    ".css", ".scss", ".sass", ".less", ".html", ".htm", ".vue", ".svelte", ".astro",
                    ".ini", ".cfg", ".conf", ".env", ".gitignore", ".dockerfile", ".makefile",
                    ".gradle", ".cmake", ".txt", ".log", ".csv", ".tsv", ".diff", ".patch" }),
        new("gcode", PreviewKind.Gcode, "G-code (3D printing)", "G-code（3D 列印）", "",
            new[] { ".gcode", ".gco", ".g", ".nc" }),
        new("qoi", PreviewKind.Qoi, "QOI images", "QOI 影像", "",
            new[] { ".qoi" }),
        new("image", PreviewKind.Image, "Raster images", "點陣圖", "",
            new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico", ".tif", ".tiff" }),
    };

    /// <summary>俾 FileDialogs 用嘅副檔名清單（只計已啟用嘅類型）· All enabled extensions, for the file picker.</summary>
    public static string[] EnabledExtensions() =>
        Types.Where(t => IsEnabled(t.Id))
             .SelectMany(t => t.Extensions)
             .Distinct(StringComparer.OrdinalIgnoreCase)
             .ToArray();

    /// <summary>所有已知副檔名 · Every extension we know about (regardless of toggle).</summary>
    public static string[] AllExtensions() =>
        Types.SelectMany(t => t.Extensions).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    // ===================== 設定切換 · per-type enable toggles =====================

    private static string Key(string id) => $"richpreview.enabled.{id}";

    public static bool IsEnabled(string id) =>
        SettingsStore.Get(Key(id), "true") != "false";

    public static void SetEnabled(string id, bool on) =>
        SettingsStore.Set(Key(id), on ? "true" : "false");

    // ===================== 類型解析 · classification =====================

    /// <summary>由路徑解析類型（已停用嘅類型會當作 Unsupported）· Resolve the preview type for a path, honouring toggles.</summary>
    public static PreviewType? Classify(string path, bool honourToggles = true)
    {
        var ext = Path.GetExtension(path);
        if (string.IsNullOrEmpty(ext))
        {
            // 冇副檔名嘅常見開發檔（Makefile、Dockerfile…）· extension-less dev files by name.
            var name = Path.GetFileName(path).ToLowerInvariant();
            if (name is "makefile" or "dockerfile" or "cmakelists.txt" or ".gitignore" or ".env"
                or ".editorconfig" or "license" or "readme")
                return Types.First(t => t.Kind == PreviewKind.Code);
        }
        var match = Types.FirstOrDefault(t => t.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
        if (match is null) return null;
        if (honourToggles && !IsEnabled(match.Id)) return null;
        return match;
    }

    public static PreviewKind KindOf(string path) => Classify(path)?.Kind ?? PreviewKind.Unsupported;

    // ===================== 資料夾導覽 · folder navigation (prev/next) =====================

    /// <summary>同一資料夾入面、所有可預覽嘅檔案（已排序）· Sibling previewable files in the same folder, sorted.</summary>
    public static List<string> SiblingFiles(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return new() { path };
            var enabledExts = EnabledExtensions();
            var files = Directory.EnumerateFiles(dir)
                .Where(f => enabledExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (files.Count == 0) files.Add(path);
            else if (!files.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase)))
                files.Insert(0, path);
            return files;
        }
        catch { return new() { path }; }
    }

    // ===================== 一般資料 · misc helpers =====================

    public static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes; int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return i == 0 ? $"{bytes} {u[0]}" : $"{s:0.##} {u[i]}";
    }

    /// <summary>讀文字（限制大小，UTF-8 容錯）· Read a text file, capped, UTF-8 tolerant.</summary>
    public static string ReadTextCapped(string path, out bool truncated, int maxBytes = 4 * 1024 * 1024)
    {
        truncated = false;
        using var fs = File.OpenRead(path);
        var len = (int)Math.Min(fs.Length, maxBytes);
        var buf = new byte[len];
        int read = fs.Read(buf, 0, len);
        if (fs.Length > maxBytes) truncated = true;
        // 去 BOM · strip a UTF-8 BOM if present.
        int start = (read >= 3 && buf[0] == 0xEF && buf[1] == 0xBB && buf[2] == 0xBF) ? 3 : 0;
        return Encoding.UTF8.GetString(buf, start, read - start);
    }

    // ===================== 開發者資料美化 · developer data pretty-print =====================

    /// <summary>美化 JSON／XML（YAML／TOML 原樣保留）· Pretty-print JSON and XML; pass YAML/TOML through.</summary>
    public static string PrettyPrint(string ext, string text, out string language)
    {
        ext = ext.ToLowerInvariant();
        if (ext is ".json" or ".jsonc" or ".json5")
        {
            language = "json";
            try
            {
                var opts = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
                using var doc = JsonDocument.Parse(text, opts);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch { return text; }
        }
        if (ext is ".xml" or ".xaml" or ".csproj" or ".props" or ".targets")
        {
            language = "xml";
            try
            {
                var xdoc = System.Xml.Linq.XDocument.Parse(text);
                return xdoc.ToString();
            }
            catch { return text; }
        }
        if (ext is ".yaml" or ".yml") { language = "yaml"; return text; }
        if (ext is ".toml") { language = "toml"; return text; }
        language = "text";
        return text;
    }

    // ===================== Markdown → HTML（自給自足，離線）=====================

    /// <summary>
    /// 輕量 Markdown → HTML 轉換（無第三方相依、離線可用）· A small, dependency-free Markdown→HTML
    /// converter covering the common subset: ATX headings, fenced + indented code, bold/italic/
    /// inline-code, links, images, blockquotes, ordered/unordered lists, horizontal rules,
    /// pipe tables, and paragraphs. HTML-escapes everything else so untrusted content can't inject.
    /// </summary>
    public static string MarkdownToHtmlFragment(string md)
    {
        md = md.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = md.Split('\n');
        var sb = new StringBuilder();
        bool inCode = false; string codeFence = "";
        var listStack = new Stack<string>(); // "ul" / "ol"

        void CloseLists()
        {
            while (listStack.Count > 0) sb.Append("</").Append(listStack.Pop()).Append(">\n");
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // fenced code blocks ``` or ~~~
            var fence = Regex.Match(line, @"^(\s*)(```+|~~~+)(.*)$");
            if (fence.Success && (!inCode || line.TrimStart().StartsWith(codeFence)))
            {
                if (!inCode)
                {
                    CloseLists();
                    inCode = true;
                    codeFence = fence.Groups[2].Value;
                    var lang = fence.Groups[3].Value.Trim();
                    sb.Append("<pre><code")
                      .Append(string.IsNullOrEmpty(lang) ? "" : $" class=\"language-{Esc(lang)}\"")
                      .Append('>');
                }
                else { inCode = false; sb.Append("</code></pre>\n"); }
                continue;
            }
            if (inCode) { sb.Append(Esc(line)).Append('\n'); continue; }

            // blank line
            if (line.Trim().Length == 0) { CloseLists(); continue; }

            // horizontal rule
            if (Regex.IsMatch(line, @"^\s*([-*_])(\s*\1){2,}\s*$")) { CloseLists(); sb.Append("<hr/>\n"); continue; }

            // ATX heading
            var h = Regex.Match(line, @"^(#{1,6})\s+(.*?)\s*#*\s*$");
            if (h.Success)
            {
                CloseLists();
                int lvl = h.Groups[1].Value.Length;
                sb.Append($"<h{lvl}>").Append(Inline(h.Groups[2].Value)).Append($"</h{lvl}>\n");
                continue;
            }

            // blockquote
            var bq = Regex.Match(line, @"^\s*>\s?(.*)$");
            if (bq.Success) { CloseLists(); sb.Append("<blockquote>").Append(Inline(bq.Groups[1].Value)).Append("</blockquote>\n"); continue; }

            // table (header row followed by a separator row of dashes/pipes)
            if (line.Contains('|') && i + 1 < lines.Length &&
                Regex.IsMatch(lines[i + 1], @"^\s*\|?\s*:?-{1,}:?\s*(\|\s*:?-{1,}:?\s*)+\|?\s*$"))
            {
                CloseLists();
                var headers = SplitRow(line);
                sb.Append("<table><thead><tr>");
                foreach (var c in headers) sb.Append("<th>").Append(Inline(c)).Append("</th>");
                sb.Append("</tr></thead><tbody>");
                i += 2; // skip header + separator
                while (i < lines.Length && lines[i].Contains('|') && lines[i].Trim().Length > 0)
                {
                    var cells = SplitRow(lines[i]);
                    sb.Append("<tr>");
                    foreach (var c in cells) sb.Append("<td>").Append(Inline(c)).Append("</td>");
                    sb.Append("</tr>");
                    i++;
                }
                i--; // for-loop will ++
                sb.Append("</tbody></table>\n");
                continue;
            }

            // lists
            var ul = Regex.Match(line, @"^(\s*)[-*+]\s+(.*)$");
            var ol = Regex.Match(line, @"^(\s*)\d+[.)]\s+(.*)$");
            if (ul.Success || ol.Success)
            {
                var want = ul.Success ? "ul" : "ol";
                if (listStack.Count == 0 || listStack.Peek() != want)
                {
                    // simple, non-nested handling: switch list type at top level
                    if (listStack.Count > 0 && listStack.Peek() != want) sb.Append("</").Append(listStack.Pop()).Append(">\n");
                    if (listStack.Count == 0) { listStack.Push(want); sb.Append('<').Append(want).Append(">\n"); }
                }
                var content = (ul.Success ? ul.Groups[2] : ol.Groups[2]).Value;
                sb.Append("<li>").Append(Inline(content)).Append("</li>\n");
                continue;
            }

            // paragraph
            CloseLists();
            sb.Append("<p>").Append(Inline(line)).Append("</p>\n");
        }
        if (inCode) sb.Append("</code></pre>\n");
        CloseLists();
        return sb.ToString();
    }

    private static string[] SplitRow(string row)
    {
        var t = row.Trim();
        if (t.StartsWith("|")) t = t.Substring(1);
        if (t.EndsWith("|")) t = t.Substring(0, t.Length - 1);
        return t.Split('|').Select(c => c.Trim()).ToArray();
    }

    private static string Esc(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    /// <summary>行內 Markdown → HTML（已先做 HTML 轉義）· Inline span conversion on top of HTML-escaped text.</summary>
    private static string Inline(string s)
    {
        s = Esc(s);
        // images ![alt](url)
        s = Regex.Replace(s, @"!\[(.*?)\]\((.*?)\)", m =>
            $"<img alt=\"{m.Groups[1].Value}\" src=\"{SafeUrl(m.Groups[2].Value)}\"/>");
        // links [text](url)
        s = Regex.Replace(s, @"\[(.*?)\]\((.*?)\)", m =>
            $"<a href=\"{SafeUrl(m.Groups[2].Value)}\">{m.Groups[1].Value}</a>");
        // inline code `code`
        s = Regex.Replace(s, @"`([^`]+)`", m => $"<code>{m.Groups[1].Value}</code>");
        // bold **x** / __x__
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        s = Regex.Replace(s, @"__(.+?)__", "<strong>$1</strong>");
        // italic *x* / _x_
        s = Regex.Replace(s, @"\*(.+?)\*", "<em>$1</em>");
        s = Regex.Replace(s, @"(?<!\w)_(.+?)_(?!\w)", "<em>$1</em>");
        // strikethrough ~~x~~
        s = Regex.Replace(s, @"~~(.+?)~~", "<del>$1</del>");
        return s;
    }

    private static string SafeUrl(string url)
    {
        url = url.Trim();
        // 只容許安全 scheme，擋 javascript: 等 · only allow safe schemes; block javascript:/data:html etc.
        if (Regex.IsMatch(url, @"^\s*(https?:|mailto:|/|\.|#|file:|data:image/)", RegexOptions.IgnoreCase))
            return url.Replace("\"", "%22");
        return "#";
    }

    // ===================== G-code 統計 + 縮圖 · G-code stats + embedded thumbnail =====================

    public sealed record GcodeStats(
        int LineCount, long ByteSize, int LayerCount, double? FilamentMm, double? FilamentG,
        double? PrintMinutes, string? Slicer, double? MaxZ, string? ThumbnailDataUri);

    /// <summary>
    /// 由 G-code 抽取統計同內嵌縮圖 · Parse common slicer comments out of a G-code file:
    /// layer count, filament use, estimated print time, slicer name, object height, plus the
    /// base64 thumbnail (PrusaSlicer / Cura "; thumbnail begin … end" PNG/QOI block).
    /// </summary>
    public static GcodeStats AnalyzeGcode(string path)
    {
        long size = 0;
        try { size = new FileInfo(path).Length; } catch { }
        int lineCount = 0, layerCount = 0;
        double? filamentMm = null, filamentG = null, printMin = null, maxZ = null;
        string? slicer = null;
        string? thumb = null;

        // thumbnail block accumulator
        var thumbB64 = new StringBuilder();
        bool inThumb = false; string thumbFmt = "png";

        try
        {
            foreach (var raw in File.ReadLines(path))
            {
                lineCount++;
                var line = raw.Trim();

                if (inThumb)
                {
                    if (line.StartsWith("; thumbnail end", StringComparison.OrdinalIgnoreCase) ||
                        line.StartsWith("; thumbnail_QOI end", StringComparison.OrdinalIgnoreCase))
                    {
                        inThumb = false;
                        if (thumb is null && thumbB64.Length > 0 && thumbFmt == "png")
                            thumb = "data:image/png;base64," + thumbB64.ToString();
                        thumbB64.Clear();
                    }
                    else if (line.StartsWith(";"))
                    {
                        // strip leading "; " then keep base64 chars
                        var chunk = line.TrimStart(';', ' ');
                        if (thumbFmt == "png")
                            thumbB64.Append(chunk);
                    }
                    continue;
                }

                // thumbnail start (only PNG supported as a data: URI; QOI noted but not embedded)
                if (line.StartsWith("; thumbnail begin", StringComparison.OrdinalIgnoreCase))
                {
                    inThumb = true; thumbFmt = "png"; thumbB64.Clear(); continue;
                }
                if (line.StartsWith("; thumbnail_QOI begin", StringComparison.OrdinalIgnoreCase))
                {
                    inThumb = true; thumbFmt = "qoi"; thumbB64.Clear(); continue;
                }

                // layer change markers
                if (line.StartsWith(";LAYER:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("; layer ", StringComparison.OrdinalIgnoreCase) ||
                    line.Equals(";LAYER_CHANGE", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith(";BEFORE_LAYER_CHANGE", StringComparison.OrdinalIgnoreCase))
                    layerCount++;

                // total layer count comment (overrides the counter if present)
                var lc = Regex.Match(line, @";\s*(LAYER_COUNT|total layers? count)\s*[:=]\s*(\d+)", RegexOptions.IgnoreCase);
                if (lc.Success && int.TryParse(lc.Groups[2].Value, out var lcv)) layerCount = Math.Max(layerCount, lcv);

                // slicer
                if (slicer is null)
                {
                    var sm = Regex.Match(line, @";\s*(generated by|Sliced by|FLAVOR|Slicer)\s*[:=]?\s*(.+)$", RegexOptions.IgnoreCase);
                    if (sm.Success) slicer = sm.Groups[2].Value.Trim();
                    else if (line.IndexOf("PrusaSlicer", StringComparison.OrdinalIgnoreCase) >= 0) slicer = "PrusaSlicer";
                    else if (line.IndexOf("Cura", StringComparison.OrdinalIgnoreCase) >= 0) slicer = "Cura";
                    else if (line.IndexOf("OrcaSlicer", StringComparison.OrdinalIgnoreCase) >= 0) slicer = "OrcaSlicer";
                    else if (line.IndexOf("SuperSlicer", StringComparison.OrdinalIgnoreCase) >= 0) slicer = "SuperSlicer";
                }

                // filament used (mm)
                var fmm = Regex.Match(line, @";\s*filament used\s*\[mm\]\s*=\s*([\d.]+)", RegexOptions.IgnoreCase);
                if (fmm.Success && double.TryParse(fmm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var fmmv)) filamentMm = fmmv;
                var fmm2 = Regex.Match(line, @";Filament used:\s*([\d.]+)m", RegexOptions.IgnoreCase);
                if (filamentMm is null && fmm2.Success && double.TryParse(fmm2.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var fmm2v)) filamentMm = fmm2v * 1000.0;

                // filament used (g)
                var fg = Regex.Match(line, @";\s*(total )?filament used\s*\[g\]\s*=\s*([\d.]+)", RegexOptions.IgnoreCase);
                if (fg.Success && double.TryParse(fg.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture, out var fgv)) filamentG = fgv;

                // estimated print time
                var pt = Regex.Match(line, @";\s*estimated printing time.*?=\s*(.+)$", RegexOptions.IgnoreCase);
                if (pt.Success) printMin = ParseTimeToMinutes(pt.Groups[1].Value);
                var pt2 = Regex.Match(line, @";TIME:\s*([\d.]+)", RegexOptions.IgnoreCase);
                if (printMin is null && pt2.Success && double.TryParse(pt2.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var secs)) printMin = secs / 60.0;

                // max Z (object height) — scan Z moves on G0/G1, cheaply
                if ((line.StartsWith("G0", StringComparison.OrdinalIgnoreCase) || line.StartsWith("G1", StringComparison.OrdinalIgnoreCase)))
                {
                    var zm = Regex.Match(line, @"\bZ([\d.]+)", RegexOptions.IgnoreCase);
                    if (zm.Success && double.TryParse(zm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture, out var z))
                        maxZ = maxZ is null ? z : Math.Max(maxZ.Value, z);
                }

                // safety cap on huge files for the heavy regex scan
                if (lineCount > 4_000_000) break;
            }
        }
        catch { }

        return new GcodeStats(lineCount, size, layerCount, filamentMm, filamentG, printMin, slicer, maxZ, thumb);
    }

    private static double? ParseTimeToMinutes(string s)
    {
        // forms like "2h 13m 5s", "1d 2h", "13m 5s"
        double total = 0; bool any = false;
        foreach (Match m in Regex.Matches(s, @"(\d+)\s*([dhms])", RegexOptions.IgnoreCase))
        {
            any = true;
            var v = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            switch (char.ToLowerInvariant(m.Groups[2].Value[0]))
            {
                case 'd': total += v * 24 * 60; break;
                case 'h': total += v * 60; break;
                case 'm': total += v; break;
                case 's': total += v / 60.0; break;
            }
        }
        return any ? total : null;
    }

    public static string FormatMinutes(double minutes)
    {
        var t = TimeSpan.FromMinutes(minutes);
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
        return $"{t.Seconds}s";
    }

    // ===================== QOI 解碼 · QOI decoder (→ BGRA8 for WriteableBitmap) =====================

    public sealed record QoiResult(int Width, int Height, byte[] Bgra, int Channels, string ColorSpace);

    private const byte QOI_OP_INDEX = 0x00, QOI_OP_DIFF = 0x40, QOI_OP_LUMA = 0x80,
                       QOI_OP_RUN = 0xc0, QOI_OP_RGB = 0xfe, QOI_OP_RGBA = 0xff, QOI_MASK_2 = 0xc0;

    /// <summary>
    /// 解碼 QOI 成 BGRA8（premultiply 由 WriteableBitmap 處理）· Decode a .qoi file to a top-down
    /// BGRA8 byte buffer suitable for a WinUI WriteableBitmap. Based on the reference qoi.h spec.
    /// </summary>
    public static QoiResult DecodeQoi(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length < 14 + 8) throw new InvalidDataException("Not a valid QOI file (too short).");
        if (!(bytes[0] == (byte)'q' && bytes[1] == (byte)'o' && bytes[2] == (byte)'i' && bytes[3] == (byte)'f'))
            throw new InvalidDataException("Invalid QOI header.");

        uint width = (uint)((bytes[4] << 24) | (bytes[5] << 16) | (bytes[6] << 8) | bytes[7]);
        uint height = (uint)((bytes[8] << 24) | (bytes[9] << 16) | (bytes[10] << 8) | bytes[11]);
        byte channels = bytes[12];
        byte colorSpace = bytes[13];
        if (width == 0 || height == 0 || channels < 3 || channels > 4 || colorSpace > 1)
            throw new InvalidDataException("Invalid QOI dimensions/channels.");
        if ((long)width * height > 400_000_000)
            throw new InvalidDataException("QOI image too large.");

        long pxCount = (long)width * height;
        var outBgra = new byte[pxCount * 4];

        var index = new (byte R, byte G, byte B, byte A)[64];
        byte r = 0, g = 0, b = 0, a = 255;
        int p = 14;
        int chunksLen = bytes.Length - 8;
        int run = 0;

        for (long px = 0; px < pxCount; px++)
        {
            if (run > 0) run--;
            else if (p < chunksLen)
            {
                byte b1 = bytes[p++];
                if (b1 == QOI_OP_RGB) { r = bytes[p++]; g = bytes[p++]; b = bytes[p++]; }
                else if (b1 == QOI_OP_RGBA) { r = bytes[p++]; g = bytes[p++]; b = bytes[p++]; a = bytes[p++]; }
                else if ((b1 & QOI_MASK_2) == QOI_OP_INDEX) { var ip = index[b1]; r = ip.R; g = ip.G; b = ip.B; a = ip.A; }
                else if ((b1 & QOI_MASK_2) == QOI_OP_DIFF)
                {
                    r += (byte)(((b1 >> 4) & 0x03) - 2);
                    g += (byte)(((b1 >> 2) & 0x03) - 2);
                    b += (byte)((b1 & 0x03) - 2);
                }
                else if ((b1 & QOI_MASK_2) == QOI_OP_LUMA)
                {
                    byte b2 = bytes[p++];
                    int vg = (b1 & 0x3f) - 32;
                    r += (byte)(vg - 8 + ((b2 >> 4) & 0x0f));
                    g += (byte)vg;
                    b += (byte)(vg - 8 + (b2 & 0x0f));
                }
                else if ((b1 & QOI_MASK_2) == QOI_OP_RUN) { run = b1 & 0x3f; }

                int hash = ((r * 3) + (g * 5) + (b * 7) + (a * 11)) % 64;
                index[hash] = (r, g, b, a);
            }

            long o = px * 4;
            outBgra[o + 0] = b;
            outBgra[o + 1] = g;
            outBgra[o + 2] = r;
            outBgra[o + 3] = (channels == 4) ? a : (byte)255;
        }

        return new QoiResult((int)width, (int)height, outBgra, channels, colorSpace == 0 ? "sRGB" : "Linear");
    }

    // ===================== 程式碼 → HTML（用 WebView2 顯示）=====================

    /// <summary>
    /// 將原始碼包成最小 HTML（等寬、行號、淺色／深色自適應）· Wrap source code in a minimal HTML page
    /// with line numbers and a light/dark adaptive theme, for rendering in WebView2. We HTML-escape
    /// the whole body (no script execution) and add a tiny keyword/string/comment highlighter.
    /// </summary>
    public static string CodeToHtml(string code, string language, bool dark)
    {
        var lines = code.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            sb.Append("<tr><td class=\"ln\">").Append(i + 1).Append("</td><td class=\"src\">")
              .Append(HighlightLine(lines[i], language)).Append("</td></tr>");
        }
        return sb.ToString();
    }

    private static readonly HashSet<string> CommonKeywords = new(StringComparer.Ordinal)
    {
        "abstract","async","await","base","bool","break","byte","case","catch","char","class","const",
        "continue","def","default","delegate","do","double","elif","else","enum","event","export","extends",
        "false","final","finally","float","fn","for","foreach","from","func","function","get","if","impl",
        "implements","import","in","int","interface","internal","is","let","lock","long","match","module",
        "mut","namespace","new","null","object","operator","out","override","package","params","private",
        "protected","public","ref","return","sealed","set","short","static","string","struct","super","switch",
        "this","throw","throws","trait","true","try","type","typeof","using","var","virtual","void","while",
        "with","yield","and","or","not","None","True","False","self","pub","use","where","as",
    };

    private static string HighlightLine(string line, string language)
    {
        // comments first (whole-line)
        var trimmed = line.TrimStart();
        string commentPrefix =
            (language is "py" or "rb" or "sh" or "yaml" or "toml") ? "#" :
            (language is "sql") ? "--" : "//";
        int ci = line.IndexOf(commentPrefix, StringComparison.Ordinal);
        if (trimmed.StartsWith(commentPrefix))
            return $"<span class=\"cm\">{Esc(line)}</span>";

        // tokenise on word boundaries, strings, numbers
        var sb = new StringBuilder();
        var tokenRx = new Regex("(\"(?:[^\"\\\\]|\\\\.)*\"|'(?:[^'\\\\]|\\\\.)*'|`(?:[^`\\\\]|\\\\.)*`|\\b\\w+\\b|\\s+|.)");
        foreach (Match m in tokenRx.Matches(line))
        {
            var tok = m.Value;
            if (tok.Length == 0) continue;
            char c0 = tok[0];
            if (c0 == '"' || c0 == '\'' || c0 == '`')
                sb.Append("<span class=\"st\">").Append(Esc(tok)).Append("</span>");
            else if (char.IsDigit(c0) && double.TryParse(tok, out _))
                sb.Append("<span class=\"nm\">").Append(Esc(tok)).Append("</span>");
            else if (CommonKeywords.Contains(tok))
                sb.Append("<span class=\"kw\">").Append(Esc(tok)).Append("</span>");
            else
                sb.Append(Esc(tok));
        }
        return sb.ToString();
    }

    /// <summary>整頁 HTML 殼（用喺 Markdown / 程式碼 / SVG）· Full HTML shell with adaptive theming.</summary>
    public static string HtmlShell(string innerBody, bool dark, bool isCode)
    {
        string bg = dark ? "#1e1e1e" : "#ffffff";
        string fg = dark ? "#d4d4d4" : "#1f1f1f";
        string sub = dark ? "#9d9d9d" : "#6e6e6e";
        string border = dark ? "#333" : "#e1e1e1";
        string codeBg = dark ? "#252526" : "#f6f8fa";
        string link = dark ? "#4da3ff" : "#0969da";
        string kw = dark ? "#569cd6" : "#0000ff";
        string st = dark ? "#ce9178" : "#a31515";
        string cm = dark ? "#6a9955" : "#008000";
        string nm = dark ? "#b5cea8" : "#098658";

        var css = $@"
            html,body{{margin:0;padding:0;background:{bg};color:{fg};
                font-family:'Segoe UI',system-ui,sans-serif;font-size:14px;line-height:1.6;}}
            .wrap{{padding:16px 20px;max-width:920px;margin:0 auto;}}
            a{{color:{link};text-decoration:none;}} a:hover{{text-decoration:underline;}}
            h1,h2,h3,h4,h5,h6{{line-height:1.25;margin:1.2em 0 .5em;}}
            h1{{border-bottom:1px solid {border};padding-bottom:.3em;}}
            h2{{border-bottom:1px solid {border};padding-bottom:.2em;}}
            code{{font-family:'Cascadia Code',Consolas,monospace;background:{codeBg};
                padding:.15em .35em;border-radius:4px;font-size:.92em;}}
            pre{{background:{codeBg};padding:12px 14px;border-radius:8px;overflow:auto;border:1px solid {border};}}
            pre code{{background:none;padding:0;}}
            blockquote{{margin:.6em 0;padding:.2em .9em;border-left:4px solid {border};color:{sub};}}
            table{{border-collapse:collapse;margin:.6em 0;width:auto;}}
            th,td{{border:1px solid {border};padding:6px 12px;text-align:left;}}
            th{{background:{codeBg};}}
            img{{max-width:100%;height:auto;}}
            hr{{border:none;border-top:1px solid {border};margin:1.2em 0;}}
            ul,ol{{padding-left:1.5em;}}
            /* code viewer */
            table.code{{border-collapse:collapse;width:100%;font-family:'Cascadia Code',Consolas,monospace;font-size:13px;}}
            table.code td{{border:none;padding:0 0 0 12px;vertical-align:top;white-space:pre;}}
            table.code td.ln{{text-align:right;color:{sub};user-select:none;padding:0 12px 0 0;
                border-right:1px solid {border};min-width:42px;}}
            .kw{{color:{kw};}} .st{{color:{st};}} .cm{{color:{cm};font-style:italic;}} .nm{{color:{nm};}}
            /* svg host */
            .svghost{{display:flex;align-items:center;justify-content:center;min-height:90vh;
                background-image:linear-gradient(45deg,{border} 25%,transparent 25%),
                linear-gradient(-45deg,{border} 25%,transparent 25%),
                linear-gradient(45deg,transparent 75%,{border} 75%),
                linear-gradient(-45deg,transparent 75%,{border} 75%);
                background-size:20px 20px;background-position:0 0,0 10px,10px -10px,-10px 0;}}
            .svghost svg,.svghost img{{max-width:96%;max-height:96vh;}}
        ";

        if (isCode)
            return $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><meta name=\"color-scheme\" content=\"{(dark ? "dark" : "light")}\"/><style>{css}</style></head>" +
                   $"<body><div class=\"wrap\"><table class=\"code\">{innerBody}</table></div></body></html>";
        return $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"/><meta name=\"color-scheme\" content=\"{(dark ? "dark" : "light")}\"/><style>{css}</style></head>" +
               $"<body><div class=\"wrap\">{innerBody}</div></body></html>";
    }

    /// <summary>SVG 寄存頁（清理潛在腳本）· Host an SVG inline, stripping <script> for safety.</summary>
    public static string SvgHostHtml(string svgText, bool dark)
    {
        // 移除 <script>…</script> 同 on* 事件屬性 · strip scripts + inline event handlers.
        svgText = Regex.Replace(svgText, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
        svgText = Regex.Replace(svgText, @"\son\w+\s*=\s*""[^""]*""", "", RegexOptions.IgnoreCase);
        svgText = Regex.Replace(svgText, @"\son\w+\s*=\s*'[^']*'", "", RegexOptions.IgnoreCase);
        return HtmlShell($"<div class=\"svghost\">{svgText}</div>", dark, isCode: false);
    }
}
