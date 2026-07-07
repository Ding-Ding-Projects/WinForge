using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// MIME 類型查詢 · MIME type lookup — a pure-managed, embedded table of ~150 common file-extension ↔
/// MIME-type mappings, plus filename detection. No I/O, no shelling out, never throws.
/// </summary>
public static class MimeTypesService
{
    /// <summary>One extension↔MIME row for display/binding.</summary>
    public sealed class MimeRow
    {
        public string Ext { get; init; } = "";   // includes leading dot, e.g. ".png"
        public string Mime { get; init; } = "";  // e.g. "image/png"
    }

    // Extension (with leading dot, lower-case) → MIME type. Curated common set (~150).
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        // Text & code
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".htm"] = "text/html",
        [".html"] = "text/html",
        [".xhtml"] = "application/xhtml+xml",
        [".css"] = "text/css",
        [".js"] = "text/javascript",
        [".mjs"] = "text/javascript",
        [".cjs"] = "text/javascript",
        [".json"] = "application/json",
        [".jsonld"] = "application/ld+json",
        [".map"] = "application/json",
        [".xml"] = "application/xml",
        [".rss"] = "application/rss+xml",
        [".atom"] = "application/atom+xml",
        [".yaml"] = "application/yaml",
        [".yml"] = "application/yaml",
        [".toml"] = "application/toml",
        [".ini"] = "text/plain",
        [".md"] = "text/markdown",
        [".markdown"] = "text/markdown",
        [".rtf"] = "application/rtf",
        [".vtt"] = "text/vtt",
        [".ics"] = "text/calendar",
        [".vcf"] = "text/vcard",
        [".tsv"] = "text/tab-separated-values",
        [".log"] = "text/plain",
        [".cs"] = "text/plain",
        [".c"] = "text/plain",
        [".h"] = "text/plain",
        [".cpp"] = "text/plain",
        [".py"] = "text/x-python",
        [".rb"] = "text/plain",
        [".go"] = "text/plain",
        [".rs"] = "text/plain",
        [".java"] = "text/plain",
        [".php"] = "application/x-httpd-php",
        [".sh"] = "application/x-sh",
        [".ps1"] = "text/plain",
        [".bat"] = "text/plain",
        [".sql"] = "application/sql",

        // Images
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".jpe"] = "image/jpeg",
        [".jfif"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
        [".svgz"] = "image/svg+xml",
        [".ico"] = "image/vnd.microsoft.icon",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff",
        [".heic"] = "image/heic",
        [".heif"] = "image/heif",
        [".avif"] = "image/avif",
        [".jxl"] = "image/jxl",
        [".psd"] = "image/vnd.adobe.photoshop",
        [".apng"] = "image/apng",
        [".cur"] = "image/x-icon",
        [".dds"] = "image/vnd-ms.dds",

        // Audio
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".oga"] = "audio/ogg",
        [".ogg"] = "audio/ogg",
        [".opus"] = "audio/opus",
        [".wav"] = "audio/wav",
        [".weba"] = "audio/webm",
        [".flac"] = "audio/flac",
        [".mid"] = "audio/midi",
        [".midi"] = "audio/midi",
        [".aiff"] = "audio/aiff",
        [".wma"] = "audio/x-ms-wma",
        [".amr"] = "audio/amr",

        // Video
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".webm"] = "video/webm",
        [".ogv"] = "video/ogg",
        [".mov"] = "video/quicktime",
        [".avi"] = "video/x-msvideo",
        [".wmv"] = "video/x-ms-wmv",
        [".mkv"] = "video/x-matroska",
        [".mpeg"] = "video/mpeg",
        [".mpg"] = "video/mpeg",
        [".3gp"] = "video/3gpp",
        [".3g2"] = "video/3gpp2",
        [".flv"] = "video/x-flv",
        [".ts"] = "video/mp2t",

        // Fonts
        [".woff"] = "font/woff",
        [".woff2"] = "font/woff2",
        [".ttf"] = "font/ttf",
        [".otf"] = "font/otf",
        [".eot"] = "application/vnd.ms-fontobject",

        // Documents
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".odt"] = "application/vnd.oasis.opendocument.text",
        [".ods"] = "application/vnd.oasis.opendocument.spreadsheet",
        [".odp"] = "application/vnd.oasis.opendocument.presentation",
        [".epub"] = "application/epub+zip",
        [".mobi"] = "application/x-mobipocket-ebook",
        [".azw"] = "application/vnd.amazon.ebook",

        // Archives & packages
        [".zip"] = "application/zip",
        [".gz"] = "application/gzip",
        [".tgz"] = "application/gzip",
        [".bz2"] = "application/x-bzip2",
        [".tar"] = "application/x-tar",
        [".7z"] = "application/x-7z-compressed",
        [".rar"] = "application/vnd.rar",
        [".xz"] = "application/x-xz",
        [".zst"] = "application/zstd",
        [".jar"] = "application/java-archive",
        [".apk"] = "application/vnd.android.package-archive",
        [".deb"] = "application/vnd.debian.binary-package",
        [".rpm"] = "application/x-rpm",
        [".cab"] = "application/vnd.ms-cab-compressed",
        [".iso"] = "application/x-iso9660-image",

        // Windows / executables
        [".exe"] = "application/vnd.microsoft.portable-executable",
        [".dll"] = "application/vnd.microsoft.portable-executable",
        [".msi"] = "application/x-msi",
        [".msix"] = "application/msix",
        [".appx"] = "application/appx",
        [".lnk"] = "application/x-ms-shortcut",
        [".reg"] = "text/plain",
        [".wasm"] = "application/wasm",

        // Data / misc
        [".bin"] = "application/octet-stream",
        [".dat"] = "application/octet-stream",
        [".pfx"] = "application/x-pkcs12",
        [".p12"] = "application/x-pkcs12",
        [".cer"] = "application/pkix-cert",
        [".crt"] = "application/x-x509-ca-cert",
        [".pem"] = "application/x-pem-file",
        [".der"] = "application/x-x509-ca-cert",
        [".torrent"] = "application/x-bittorrent",
        [".gpx"] = "application/gpx+xml",
        [".kml"] = "application/vnd.google-earth.kml+xml",
        [".webmanifest"] = "application/manifest+json",
        [".sqlite"] = "application/vnd.sqlite3",
        [".db"] = "application/octet-stream",
    };

    /// <summary>Total number of extension↔MIME rows in the embedded table.</summary>
    public static int Count => Map.Count;

    /// <summary>All rows, sorted by extension. Never throws.</summary>
    public static List<MimeRow> All()
    {
        try
        {
            return Map.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                      .Select(kv => new MimeRow { Ext = kv.Key, Mime = kv.Value })
                      .ToList();
        }
        catch { return new List<MimeRow>(); }
    }

    /// <summary>
    /// Filter rows by a query. Matches extension (with or without the leading dot) or MIME substring.
    /// An empty query returns everything. Never throws.
    /// </summary>
    public static List<MimeRow> Search(string? query)
    {
        try
        {
            string q = (query ?? "").Trim();
            var all = All();
            if (q.Length == 0) return all;

            string ql = q.ToLowerInvariant();
            // normalise a leading-dot / bare-extension query, e.g. "png" or ".png"
            string qNoDot = ql.TrimStart('.');

            return all.Where(r =>
            {
                string extNoDot = r.Ext.TrimStart('.').ToLowerInvariant();
                string mime = r.Mime.ToLowerInvariant();
                return r.Ext.ToLowerInvariant().Contains(ql)
                    || extNoDot.Contains(qNoDot)
                    || mime.Contains(ql);
            }).ToList();
        }
        catch { return new List<MimeRow>(); }
    }

    /// <summary>Look up the MIME type for a bare extension (".png" or "png"). Null if unknown. Never throws.</summary>
    public static string? ForExtension(string? ext)
    {
        try
        {
            string e = (ext ?? "").Trim().ToLowerInvariant();
            if (e.Length == 0) return null;
            if (!e.StartsWith(".")) e = "." + e;
            return Map.TryGetValue(e, out var m) ? m : null;
        }
        catch { return null; }
    }

    /// <summary>
    /// Detect the MIME type from a filename or path. Falls back to "application/octet-stream" for a
    /// known-but-unmapped extension, or null when there's no usable extension. Never throws.
    /// </summary>
    public static string? DetectFromFilename(string? filename)
    {
        try
        {
            string f = (filename ?? "").Trim();
            if (f.Length == 0) return null;
            string ext;
            try { ext = Path.GetExtension(f); }
            catch { ext = ""; }
            if (string.IsNullOrEmpty(ext) || ext == ".") return null;
            return ForExtension(ext) ?? "application/octet-stream";
        }
        catch { return null; }
    }
}
