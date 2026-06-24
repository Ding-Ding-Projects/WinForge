using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 預覽檔案類別 · The broad preview kind a file maps to. Drives which previewer the
/// <c>PeekModule</c> renders. Mirrors PowerToys Peek's previewer families.
/// </summary>
public enum PeekKind
{
    Image,
    Text,
    Markdown,
    Pdf,
    Audio,
    Video,
    Archive,
    Web,
    Unknown,
}

/// <summary>
/// 一個預覽項目嘅資料 · Metadata + classification for one file the user is peeking at.
/// </summary>
public sealed class PeekItem
{
    public string Path { get; init; } = string.Empty;
    public string Name => System.IO.Path.GetFileName(Path);
    public string Extension => System.IO.Path.GetExtension(Path).TrimStart('.').ToLowerInvariant();
    public PeekKind Kind { get; init; } = PeekKind.Unknown;
    public long SizeBytes { get; init; }
    public DateTime Modified { get; init; }
    public DateTime Created { get; init; }
    public bool Exists { get; init; }

    /// <summary>人類可讀大小（例如 1.4 MB）· Human-readable size string.</summary>
    public string SizeText => PeekService.HumanSize(SizeBytes);
}

/// <summary>
/// 一條壓縮檔內容 · One row when listing the contents of an archive.
/// </summary>
public sealed class PeekArchiveEntry
{
    public string Name { get; init; } = string.Empty;
    public long Size { get; init; }
    public string Modified { get; init; } = string.Empty;
    public string SizeText => PeekService.HumanSize(Size);
}

/// <summary>
/// 快速預覽核心服務 · The brains behind WinForge's native clone of PowerToys "Peek": fast,
/// read-only file preview. 負責分類檔案類型、抽取中繼資料、列舉同一資料夾以支援上一個／下一個、
/// 列出壓縮檔內容、把 Markdown 轉成 HTML，以及開啟／用其他程式開啟／開資料夾。
/// Classifies files by extension, extracts metadata, enumerates the sibling files in a folder for
/// Prev/Next navigation, lists archive contents (via 7-Zip when available), converts Markdown to a
/// themable HTML document for the WebView2 previewer, and shells out to Open / Open-with / Open-folder.
/// Pure helpers — owns no UI; the page binds to it.
/// </summary>
public static class PeekService
{
    // ===================== file-type tables =====================

    public static readonly string[] ImageExts =
        { "png", "jpg", "jpeg", "gif", "bmp", "webp", "ico", "tif", "tiff", "svg", "heic", "avif", "jfif", "dib", "wdp" };

    public static readonly string[] VectorImageExts = { "svg" };

    public static readonly string[] AudioExts =
        { "mp3", "wav", "flac", "aac", "m4a", "ogg", "oga", "opus", "wma", "aiff", "aif", "ape", "alac" };

    public static readonly string[] VideoExts =
        { "mp4", "m4v", "mkv", "webm", "mov", "avi", "wmv", "flv", "mpg", "mpeg", "3gp", "ts", "m2ts", "ogv" };

    public static readonly string[] ArchiveExts =
        { "zip", "7z", "rar", "tar", "gz", "tgz", "bz2", "tbz", "xz", "txz", "lz", "lzma", "cab", "iso", "wim", "jar", "apk", "war", "zst", "zstd" };

    public static readonly string[] MarkdownExts = { "md", "markdown", "mdown", "mkd", "mdx" };

    public static readonly string[] WebExts = { "html", "htm", "xhtml", "mht", "mhtml" };

    public static readonly string[] PdfExts = { "pdf" };

    /// <summary>
    /// 文字／程式碼副檔名 · Extensions treated as plain text / source code. Anything not matched
    /// here but detected as mostly-text by a byte sniff is still shown in the text previewer.
    /// </summary>
    public static readonly string[] TextExts =
    {
        "txt", "log", "ini", "cfg", "conf", "config", "csv", "tsv", "json", "json5", "jsonc", "xml", "yaml", "yml",
        "toml", "properties", "env", "gitignore", "gitattributes", "editorconfig", "lock", "sln", "props", "targets",
        "cs", "vb", "fs", "c", "h", "hpp", "hh", "cpp", "cc", "cxx", "m", "mm", "java", "kt", "kts", "scala", "groovy",
        "go", "rs", "swift", "py", "pyw", "rb", "php", "pl", "pm", "lua", "tcl", "r", "jl", "dart", "ex", "exs", "erl",
        "hs", "elm", "clj", "cljs", "edn", "nim", "zig", "v", "sv", "vhd", "asm", "s",
        "js", "mjs", "cjs", "jsx", "ts", "tsx", "vue", "svelte", "css", "scss", "sass", "less", "styl",
        "sh", "bash", "zsh", "fish", "ps1", "psm1", "psd1", "bat", "cmd", "vbs", "reg", "diff", "patch",
        "sql", "graphql", "gql", "proto", "thrift", "dockerfile", "makefile", "mk", "cmake", "gradle", "bazel",
        "tex", "bib", "rst", "adoc", "org", "srt", "vtt", "ass", "nfo", "me", "license", "authors", "readme", "changelog",
    };

    /// <summary>分類一個路徑 · Classify a path into a <see cref="PeekKind"/> by extension.</summary>
    public static PeekKind Classify(string path)
    {
        var ext = System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var bare = System.IO.Path.GetFileName(path).ToLowerInvariant();

        // extension-less well-known files (Dockerfile, Makefile, LICENSE …)
        if (string.IsNullOrEmpty(ext))
        {
            if (bare is "dockerfile" or "makefile" or "license" or "licence" or "authors" or "readme" or "changelog"
                or "copying" or "notice" or "todo" or "install" or ".gitignore" or ".gitattributes" or ".editorconfig"
                or ".env")
                return PeekKind.Text;
        }

        if (ImageExts.Contains(ext)) return PeekKind.Image;
        if (MarkdownExts.Contains(ext)) return PeekKind.Markdown;
        if (PdfExts.Contains(ext)) return PeekKind.Pdf;
        if (AudioExts.Contains(ext)) return PeekKind.Audio;
        if (VideoExts.Contains(ext)) return PeekKind.Video;
        if (ArchiveExts.Contains(ext)) return PeekKind.Archive;
        if (WebExts.Contains(ext)) return PeekKind.Web;
        if (TextExts.Contains(ext)) return PeekKind.Text;
        return PeekKind.Unknown;
    }

    /// <summary>true 表示用 SVG 預覽（WebView2 而非 BitmapImage）· Is this a vector image (SVG)?</summary>
    public static bool IsVector(string path) =>
        VectorImageExts.Contains(System.IO.Path.GetExtension(path).TrimStart('.').ToLowerInvariant());

    // ===================== item construction =====================

    /// <summary>由路徑建立預覽項目（讀檔案系統中繼資料）· Build a <see cref="PeekItem"/> from a path.</summary>
    public static PeekItem Describe(string path)
    {
        var kind = Classify(path);
        try
        {
            var fi = new FileInfo(path);
            if (fi.Exists)
                return new PeekItem
                {
                    Path = path,
                    Kind = kind,
                    SizeBytes = fi.Length,
                    Modified = fi.LastWriteTime,
                    Created = fi.CreationTime,
                    Exists = true,
                };
        }
        catch { }
        return new PeekItem { Path = path, Kind = kind, Exists = File.Exists(path) };
    }

    // ===================== folder enumeration for Prev/Next =====================

    /// <summary>
    /// 列出同一資料夾入面所有檔案（排序）· List every file in the same folder as <paramref name="path"/>,
    /// natural-sorted by name, so the page can step Prev/Next through siblings. Hidden/system files are
    /// kept (Explorer shows them too); folders are excluded.
    /// </summary>
    public static List<string> SiblingFiles(string path)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return new() { path };
            var files = Directory.EnumerateFiles(dir)
                .OrderBy(f => System.IO.Path.GetFileName(f), NaturalComparer.Instance)
                .ToList();
            return files.Count == 0 ? new() { path } : files;
        }
        catch { return new() { path }; }
    }

    // ===================== text reading =====================

    /// <summary>
    /// 讀取文字內容（限制大小，避免卡死）· Read up to <paramref name="maxBytes"/> of a file as text,
    /// detecting UTF-8/UTF-16 BOMs and falling back to UTF-8. Returns the text plus a flag telling the
    /// caller the file was truncated, so the UI can show a "… (truncated)" note.
    /// </summary>
    public static async Task<(string text, bool truncated, bool isBinary)> ReadTextAsync(string path, int maxBytes = 1024 * 1024)
    {
        try
        {
            var info = new FileInfo(path);
            var truncated = info.Length > maxBytes;
            var toRead = (int)Math.Min(info.Length, maxBytes);
            var buffer = new byte[toRead];
            await using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int read = 0;
                while (read < toRead)
                {
                    int n = await fs.ReadAsync(buffer.AsMemory(read, toRead - read));
                    if (n == 0) break;
                    read += n;
                }
            }

            // crude binary sniff: a NUL byte in the first 8 KB strongly implies binary.
            var sniff = Math.Min(buffer.Length, 8192);
            bool isBinary = false;
            for (int i = 0; i < sniff; i++) { if (buffer[i] == 0) { isBinary = true; break; } }

            var enc = DetectEncoding(buffer);
            var text = enc.GetString(buffer);
            // strip a leading BOM char if present
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            return (text, truncated, isBinary);
        }
        catch (Exception ex)
        {
            return ($"[{ex.Message}]", false, false);
        }
    }

    private static Encoding DetectEncoding(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8;
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode;
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode;
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    }

    /// <summary>計行數 · Count lines in a string (for the text-preview footer).</summary>
    public static int CountLines(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int n = 1;
        foreach (var c in s) if (c == '\n') n++;
        return n;
    }

    // ===================== archive listing =====================

    /// <summary>
    /// 列出壓縮檔內容 · List the entries of an archive using 7-Zip's <c>l -slt</c> output when 7-Zip is
    /// installed. Returns an empty list (and sets <paramref name="note"/>) when 7-Zip is missing or the
    /// archive can't be read, so the UI can show a friendly note rather than fail.
    /// </summary>
    public static async Task<(List<PeekArchiveEntry> entries, string? note)> ListArchiveAsync(string path)
    {
        if (!ArchiveService.IsInstalled)
            return (new(), "7zip-missing");

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ArchiveService.SevenZip,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add("l");
            psi.ArgumentList.Add("-slt");
            psi.ArgumentList.Add(path);

            using var p = Process.Start(psi);
            if (p is null) return (new(), "7zip-failed");
            var stdout = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();

            var entries = new List<PeekArchiveEntry>();
            string? name = null; long size = 0; string modified = ""; bool isDir = false;
            foreach (var raw in stdout.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.StartsWith("Path = ", StringComparison.Ordinal))
                {
                    // flush previous
                    if (name is not null && !isDir)
                        entries.Add(new PeekArchiveEntry { Name = name, Size = size, Modified = modified });
                    name = line.Substring(7); size = 0; modified = ""; isDir = false;
                }
                else if (line.StartsWith("Size = ", StringComparison.Ordinal))
                    long.TryParse(line.Substring(7), out size);
                else if (line.StartsWith("Modified = ", StringComparison.Ordinal))
                    modified = line.Substring(11);
                else if (line.StartsWith("Folder = ", StringComparison.Ordinal))
                    isDir = line.EndsWith("+", StringComparison.Ordinal);
                else if (line.StartsWith("Attributes = ", StringComparison.Ordinal) && line.Contains('D'))
                    isDir = true;
            }
            if (name is not null && !isDir)
                entries.Add(new PeekArchiveEntry { Name = name, Size = size, Modified = modified });

            // The first "Path = <archive itself>" header block has no Size/Folder pairing the same way;
            // drop a leading entry that equals the archive path.
            if (entries.Count > 0 && string.Equals(entries[0].Name, path, StringComparison.OrdinalIgnoreCase))
                entries.RemoveAt(0);

            return (entries, entries.Count == 0 ? "empty" : null);
        }
        catch (Exception ex)
        {
            return (new(), ex.Message);
        }
    }

    // ===================== markdown -> HTML =====================

    /// <summary>
    /// 把 Markdown 轉成可主題化嘅 HTML 文件 · Convert Markdown source into a self-contained HTML document
    /// for the WebView2 previewer. 一個輕量、零相依嘅轉換器（標題、粗斜體、行內碼、區塊碼、清單、引用、
    /// 連結、水平線、表格）。Lightweight, dependency-free converter covering headings, bold/italic, inline
    /// and fenced code, lists, blockquotes, links, rules and pipe tables — enough for a faithful preview.
    /// <paramref name="dark"/> picks a light or dark stylesheet to match the app theme.
    /// </summary>
    public static string MarkdownToHtml(string md, bool dark)
    {
        var body = RenderMarkdownBody(md ?? string.Empty);
        var fg = dark ? "#e6e6e6" : "#1b1b1b";
        var bg = dark ? "#1f1f1f" : "#ffffff";
        var muted = dark ? "#9a9a9a" : "#666";
        var codeBg = dark ? "#2b2b2b" : "#f3f3f3";
        var border = dark ? "#3a3a3a" : "#e0e0e0";
        var link = dark ? "#5aa0ff" : "#0a66c2";
        var quote = dark ? "#3a3a3a" : "#dcdcdc";

        return $@"<!DOCTYPE html><html><head><meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<style>
 html,body{{margin:0;padding:0;background:{bg};}}
 body{{color:{fg};font-family:'Segoe UI',system-ui,sans-serif;line-height:1.6;padding:20px 28px;font-size:15px;}}
 h1,h2,h3,h4{{line-height:1.25;margin:1.2em 0 .5em;}}
 h1{{font-size:1.9em;border-bottom:1px solid {border};padding-bottom:.2em;}}
 h2{{font-size:1.5em;border-bottom:1px solid {border};padding-bottom:.2em;}}
 h3{{font-size:1.25em;}} h4{{font-size:1.05em;}}
 a{{color:{link};text-decoration:none;}} a:hover{{text-decoration:underline;}}
 code{{background:{codeBg};padding:.15em .4em;border-radius:4px;font-family:Consolas,'Cascadia Mono',monospace;font-size:.9em;}}
 pre{{background:{codeBg};padding:12px 14px;border-radius:8px;overflow:auto;}}
 pre code{{background:none;padding:0;}}
 blockquote{{margin:.6em 0;padding:.2em 1em;border-left:4px solid {quote};color:{muted};}}
 table{{border-collapse:collapse;margin:.8em 0;}}
 th,td{{border:1px solid {border};padding:6px 12px;}}
 th{{background:{codeBg};}}
 hr{{border:none;border-top:1px solid {border};margin:1.4em 0;}}
 img{{max-width:100%;}}
 ul,ol{{padding-left:1.6em;}}
</style></head><body>{body}</body></html>";
    }

    private static string RenderMarkdownBody(string md)
    {
        var lines = md.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder();
        bool inCode = false; string codeLang = "";
        bool inUl = false, inOl = false;
        var tableBuf = new List<string>();

        void CloseLists()
        {
            if (inUl) { sb.Append("</ul>"); inUl = false; }
            if (inOl) { sb.Append("</ol>"); inOl = false; }
        }
        void FlushTable()
        {
            if (tableBuf.Count == 0) return;
            sb.Append(RenderTable(tableBuf));
            tableBuf.Clear();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // fenced code blocks
            if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                FlushTable();
                if (!inCode) { CloseLists(); inCode = true; codeLang = line.Trim().TrimStart('`').Trim(); sb.Append("<pre><code>"); }
                else { inCode = false; sb.Append("</code></pre>"); }
                continue;
            }
            if (inCode) { sb.Append(Escape(line)).Append('\n'); continue; }

            // table rows (pipe tables) — buffer consecutive lines containing a pipe
            if (line.Contains('|') && line.Trim().Length > 0)
            {
                CloseLists();
                tableBuf.Add(line);
                continue;
            }
            FlushTable();

            var trimmed = line.TrimStart();

            if (trimmed.Length == 0) { CloseLists(); continue; }

            // horizontal rule
            if (Regex.IsMatch(trimmed, @"^([-*_])\1{2,}\s*$")) { CloseLists(); sb.Append("<hr>"); continue; }

            // headings
            var h = Regex.Match(trimmed, @"^(#{1,6})\s+(.*)$");
            if (h.Success)
            {
                CloseLists();
                int lvl = h.Groups[1].Value.Length;
                sb.Append($"<h{lvl}>").Append(Inline(h.Groups[2].Value)).Append($"</h{lvl}>");
                continue;
            }

            // blockquote
            if (trimmed.StartsWith(">", StringComparison.Ordinal))
            {
                CloseLists();
                sb.Append("<blockquote>").Append(Inline(trimmed.TrimStart('>').Trim())).Append("</blockquote>");
                continue;
            }

            // unordered list
            var ul = Regex.Match(line, @"^\s*[-*+]\s+(.*)$");
            if (ul.Success)
            {
                if (inOl) { sb.Append("</ol>"); inOl = false; }
                if (!inUl) { sb.Append("<ul>"); inUl = true; }
                sb.Append("<li>").Append(Inline(ul.Groups[1].Value)).Append("</li>");
                continue;
            }
            // ordered list
            var ol = Regex.Match(line, @"^\s*\d+[.)]\s+(.*)$");
            if (ol.Success)
            {
                if (inUl) { sb.Append("</ul>"); inUl = false; }
                if (!inOl) { sb.Append("<ol>"); inOl = true; }
                sb.Append("<li>").Append(Inline(ol.Groups[1].Value)).Append("</li>");
                continue;
            }

            CloseLists();
            sb.Append("<p>").Append(Inline(line)).Append("</p>");
        }
        if (inCode) sb.Append("</code></pre>");
        FlushTable();
        CloseLists();
        return sb.ToString();
    }

    private static string RenderTable(List<string> rows)
    {
        // require a separator row like |---|---| as the 2nd line; otherwise treat as plain paragraphs.
        if (rows.Count >= 2 && Regex.IsMatch(rows[1], @"^\s*\|?[\s:|-]+\|?\s*$") && rows[1].Contains('-'))
        {
            string[] Cells(string r) => r.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            var sb = new StringBuilder("<table>");
            var head = Cells(rows[0]);
            sb.Append("<thead><tr>");
            foreach (var c in head) sb.Append("<th>").Append(Inline(c)).Append("</th>");
            sb.Append("</tr></thead><tbody>");
            for (int r = 2; r < rows.Count; r++)
            {
                sb.Append("<tr>");
                foreach (var c in Cells(rows[r])) sb.Append("<td>").Append(Inline(c)).Append("</td>");
                sb.Append("</tr>");
            }
            sb.Append("</tbody></table>");
            return sb.ToString();
        }
        // not a real table — render each line as a paragraph
        var fallback = new StringBuilder();
        foreach (var r in rows) fallback.Append("<p>").Append(Inline(r)).Append("</p>");
        return fallback.ToString();
    }

    private static string Inline(string s)
    {
        s = Escape(s);
        // inline code first so its content isn't further processed
        s = Regex.Replace(s, @"`([^`]+)`", m => "<code>" + m.Groups[1].Value + "</code>");
        // images ![alt](url)
        s = Regex.Replace(s, @"!\[([^\]]*)\]\(([^)\s]+)[^)]*\)", "<img alt=\"$1\" src=\"$2\">");
        // links [text](url)
        s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)\s]+)[^)]*\)", "<a href=\"$2\">$1</a>");
        // bold then italic
        s = Regex.Replace(s, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        s = Regex.Replace(s, @"__([^_]+)__", "<strong>$1</strong>");
        s = Regex.Replace(s, @"\*([^*]+)\*", "<em>$1</em>");
        s = Regex.Replace(s, @"(?<!\w)_([^_]+)_(?!\w)", "<em>$1</em>");
        // strikethrough
        s = Regex.Replace(s, @"~~([^~]+)~~", "<del>$1</del>");
        return s;
    }

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    // ===================== shell actions =====================

    /// <summary>用預設程式開啟檔案 · Open the file with its default associated program.</summary>
    public static void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
    }

    /// <summary>「用…開啟」對話框 · Show the Windows "Open with" dialog for the file.</summary>
    public static void OpenWith(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {path}")
            { UseShellExecute = true });
        }
        catch { }
    }

    /// <summary>喺檔案總管揀出檔案 · Open Explorer with the file selected.</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else
            {
                var dir = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
        }
        catch { }
    }

    // ===================== Explorer selection (for the hotkey flow) =====================

    /// <summary>
    /// 嘗試攞檔案總管目前選取嘅檔案 · Best-effort: read the file currently selected in the foreground
    /// Explorer window via the Shell.Application COM automation object. Returns null if Explorer isn't
    /// frontmost, nothing is selected, or COM automation is unavailable (e.g. running elevated, where
    /// the medium-IL Explorer can't be reached). The page falls back to the in-app picker.
    /// </summary>
    public static string? TryGetExplorerSelection()
    {
        try
        {
            var fgWin = GetForegroundWindow();
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null) return null;
            dynamic? shell = Activator.CreateInstance(shellType);
            if (shell is null) return null;
            try
            {
                dynamic windows = shell.Windows();
                int count = windows.Count;
                string? firstAny = null;
                for (int i = 0; i < count; i++)
                {
                    dynamic w = windows.Item(i);
                    if (w is null) continue;
                    long hwnd;
                    try { hwnd = (long)w.HWND; } catch { continue; }

                    dynamic sel;
                    try { sel = w.Document.SelectedItems(); } catch { continue; }
                    if (sel is null || sel.Count == 0) continue;
                    string? p = null;
                    try { object? raw = sel.Item(0).Path; p = raw as string; } catch { }
                    if (string.IsNullOrEmpty(p) || !File.Exists(p)) continue;

                    // prefer the selection in the foreground Explorer window
                    if ((nint)hwnd == fgWin) return p;
                    firstAny ??= p;
                }
                return firstAny;
            }
            finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell); }
        }
        catch { return null; }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    // ===================== formatting helpers =====================

    /// <summary>位元組轉人類可讀 · Format a byte count as B / KB / MB / GB / TB.</summary>
    public static string HumanSize(long bytes)
    {
        if (bytes < 0) return "—";
        string[] u = { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0
            ? $"{bytes:N0} {u[i]}"
            : $"{v.ToString(v >= 100 ? "N0" : "N1", CultureInfo.InvariantCulture)} {u[i]}";
    }

    /// <summary>HTML-escape 一段純文字（畀 WebView2 用）· HTML-escape arbitrary text.</summary>
    public static string HtmlEncode(string s) => WebUtility.HtmlEncode(s);
}

/// <summary>
/// 自然排序比較器 · Natural (Explorer-style) string comparer so "file2" sorts before "file10".
/// </summary>
internal sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        x ??= ""; y ??= "";
        int ix = 0, iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            if (char.IsDigit(x[ix]) && char.IsDigit(y[iy]))
            {
                int sx = ix, sy = iy;
                while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                while (iy < y.Length && char.IsDigit(y[iy])) iy++;
                var nx = x.Substring(sx, ix - sx).TrimStart('0');
                var ny = y.Substring(sy, iy - sy).TrimStart('0');
                if (nx.Length != ny.Length) return nx.Length - ny.Length;
                int cmp = string.CompareOrdinal(nx, ny);
                if (cmp != 0) return cmp;
            }
            else
            {
                int cmp = char.ToLowerInvariant(x[ix]).CompareTo(char.ToLowerInvariant(y[iy]));
                if (cmp != 0) return cmp;
                ix++; iy++;
            }
        }
        return (x.Length - ix) - (y.Length - iy);
    }
}
