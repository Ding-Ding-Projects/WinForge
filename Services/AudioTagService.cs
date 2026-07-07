using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TagLib;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 音訊標籤服務（TagLib#）· Audio tag service powered by TagLib# (TagLibSharp).
/// 純託管讀寫 ID3v1/v2、APE、Xiph（FLAC/OGG）、MP4/iTunes 標籤同封面圖 — 全部喺程序內，
/// 唔會啟動／呼叫任何外部工具（例如 Mp3tag）。
/// Fully-managed read/write of audio metadata + cover art — no external tool is ever launched or shelled.
/// </summary>
public static class AudioTagService
{
    /// <summary>支援嘅音訊副檔名 · Supported audio extensions.</summary>
    public static readonly string[] Extensions =
        { ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wav", ".wma", ".aiff", ".aif", ".ape", ".wv", ".mpc" };

    public static bool IsAudio(string path)
        => Extensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>一個音訊檔嘅標籤快照 · A snapshot of one audio file's tags + technical info.</summary>
    public sealed class TrackTags
    {
        public string Path { get; set; } = "";
        public string FileName => System.IO.Path.GetFileName(Path);
        public string? Title { get; set; }
        public string? Artist { get; set; }       // joined performers
        public string? Album { get; set; }
        public string? AlbumArtist { get; set; }
        public uint Track { get; set; }
        public uint TrackCount { get; set; }
        public uint Disc { get; set; }
        public uint Year { get; set; }
        public string? Genre { get; set; }
        public string? Comment { get; set; }
        public string? Composer { get; set; }
        public TimeSpan Duration { get; set; }
        public int Bitrate { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public byte[]? CoverData { get; set; }
        public string? CoverMime { get; set; }

        // ── derived display strings (bilingual handled at call sites) ──
        public string DurationText => Duration.TotalHours >= 1
            ? Duration.ToString(@"h\:mm\:ss")
            : Duration.ToString(@"m\:ss");
        public string BitrateText => Bitrate > 0 ? $"{Bitrate} kbps" : "—";
        public string TrackText => Track > 0 ? (TrackCount > 0 ? $"{Track}/{TrackCount}" : Track.ToString()) : "";
        public string YearText => Year > 0 ? Year.ToString() : "";
    }

    /// <summary>讀一個檔嘅標籤 · Read one file's tags (returns null on failure).</summary>
    public static TrackTags? Read(string path)
    {
        try
        {
            using var f = TagLib.File.Create(path);
            var tag = f.Tag;
            var pic = tag.Pictures?.FirstOrDefault();
            return new TrackTags
            {
                Path = path,
                Title = NullIfEmpty(tag.Title),
                Artist = NullIfEmpty(tag.JoinedPerformers),
                Album = NullIfEmpty(tag.Album),
                AlbumArtist = NullIfEmpty(tag.JoinedAlbumArtists),
                Track = tag.Track,
                TrackCount = tag.TrackCount,
                Disc = tag.Disc,
                Year = tag.Year,
                Genre = NullIfEmpty(tag.JoinedGenres),
                Comment = NullIfEmpty(tag.Comment),
                Composer = NullIfEmpty(tag.JoinedComposers),
                Duration = f.Properties?.Duration ?? TimeSpan.Zero,
                Bitrate = f.Properties?.AudioBitrate ?? 0,
                SampleRate = f.Properties?.AudioSampleRate ?? 0,
                Channels = f.Properties?.AudioChannels ?? 0,
                CoverData = pic?.Data?.Data is { Length: > 0 } d ? d : null,
                CoverMime = pic?.MimeType,
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>非同步讀一個資料夾入面所有音訊檔嘅標籤 · Read all audio files in a folder (non-recursive by default).</summary>
    public static Task<List<TrackTags>> ReadFolderAsync(string folder, bool recursive = false)
        => Task.Run(() =>
        {
            var list = new List<TrackTags>();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folder, "*.*",
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch { return list; }
            foreach (var p in files.Where(IsAudio).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var t = Read(p);
                if (t is not null) list.Add(t);
            }
            return list;
        });

    public static Task<List<TrackTags>> ReadFilesAsync(IEnumerable<string> paths)
        => Task.Run(() =>
        {
            var list = new List<TrackTags>();
            foreach (var p in paths.Where(IsAudio))
            {
                var t = Read(p);
                if (t is not null) list.Add(t);
            }
            return list;
        });

    /// <summary>
    /// 要寫入嘅一組欄位 · The set of fields to write. null 表示「唔郁呢個欄位」（畀批次編輯用）；
    /// A null property means "leave this field untouched" — used by batch edit so only set fields apply.
    /// </summary>
    public sealed class TagEdit
    {
        public string? Title;
        public string? Artist;
        public string? Album;
        public string? AlbumArtist;
        public uint? Track;
        public uint? TrackCount;
        public uint? Disc;
        public uint? Year;
        public string? Genre;
        public string? Comment;
        public string? Composer;

        // Cover handling: 0 = leave, 1 = set new, 2 = remove.
        public int CoverAction;          // 0 leave / 1 set / 2 remove
        public byte[]? CoverData;
        public string? CoverMime;
    }

    /// <summary>把一組欄位寫入單一檔 · Apply an edit to a single file; throws on hard failure.</summary>
    public static void Write(string path, TagEdit edit)
    {
        using var f = TagLib.File.Create(path);
        var tag = f.Tag;

        if (edit.Title is not null) tag.Title = NullIfEmpty(edit.Title);
        if (edit.Artist is not null)
        {
            var arr = SplitMulti(edit.Artist);
            tag.Performers = arr;
        }
        if (edit.Album is not null) tag.Album = NullIfEmpty(edit.Album);
        if (edit.AlbumArtist is not null) tag.AlbumArtists = SplitMulti(edit.AlbumArtist);
        if (edit.Track is not null) tag.Track = edit.Track.Value;
        if (edit.TrackCount is not null) tag.TrackCount = edit.TrackCount.Value;
        if (edit.Disc is not null) tag.Disc = edit.Disc.Value;
        if (edit.Year is not null) tag.Year = edit.Year.Value;
        if (edit.Genre is not null) tag.Genres = SplitMulti(edit.Genre);
        if (edit.Comment is not null) tag.Comment = NullIfEmpty(edit.Comment);
        if (edit.Composer is not null) tag.Composers = SplitMulti(edit.Composer);

        if (edit.CoverAction == 2)
        {
            tag.Pictures = Array.Empty<IPicture>();
        }
        else if (edit.CoverAction == 1 && edit.CoverData is { Length: > 0 })
        {
            var pic = new Picture(new ByteVector(edit.CoverData))
            {
                Type = PictureType.FrontCover,
                MimeType = string.IsNullOrEmpty(edit.CoverMime) ? "image/jpeg" : edit.CoverMime,
                Description = "Cover",
            };
            tag.Pictures = new IPicture[] { pic };
        }

        f.Save();
    }

    /// <summary>批次寫入；回傳成功／失敗計數同錯誤訊息 · Batch write; returns counts + error list.</summary>
    public static Task<(int ok, int fail, List<string> errors)> WriteManyAsync(
        IEnumerable<string> paths, TagEdit edit)
        => Task.Run(() =>
        {
            int ok = 0, fail = 0;
            var errors = new List<string>();
            foreach (var p in paths)
            {
                try { Write(p, edit); ok++; }
                catch (Exception ex) { fail++; errors.Add($"{Path.GetFileName(p)}: {ex.Message}"); }
            }
            return (ok, fail, errors);
        });

    // ───────────────────────── filename ↔ tags tools ─────────────────────────

    /// <summary>
    /// 由檔名按樣式解析標籤 · Parse tag fields out of a filename using a pattern such as
    /// "%artist% - %title%" or "%track%. %title%". Returns a TagEdit with only matched fields set.
    /// </summary>
    public static TagEdit? ParseFromFileName(string path, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return null;
        var name = Path.GetFileNameWithoutExtension(path);

        // Build a regex from the pattern: %field% → named capture; literals escaped.
        var tokens = new List<string>();
        var regex = new System.Text.StringBuilder("^");
        int i = 0;
        while (i < pattern.Length)
        {
            if (pattern[i] == '%')
            {
                int end = pattern.IndexOf('%', i + 1);
                if (end < 0) { regex.Append(System.Text.RegularExpressions.Regex.Escape("%")); i++; continue; }
                var field = pattern.Substring(i + 1, end - i - 1).ToLowerInvariant();
                var grp = "f" + tokens.Count;
                tokens.Add(field);
                // numeric fields capture digits; others capture lazily.
                regex.Append(field is "track" or "year" or "disc" or "trackcount"
                    ? $"(?<{grp}>\\d+)"
                    : $"(?<{grp}>.+?)");
                i = end + 1;
            }
            else
            {
                regex.Append(System.Text.RegularExpressions.Regex.Escape(pattern[i].ToString()));
                i++;
            }
        }
        regex.Append("$");

        System.Text.RegularExpressions.Match m;
        try
        {
            m = System.Text.RegularExpressions.Regex.Match(name, regex.ToString());
        }
        catch { return null; }
        if (!m.Success) return null;

        var edit = new TagEdit();
        for (int t = 0; t < tokens.Count; t++)
        {
            var val = m.Groups["f" + t].Value.Trim();
            if (val.Length == 0) continue;
            ApplyField(edit, tokens[t], val);
        }
        return edit;
    }

    private static void ApplyField(TagEdit edit, string field, string val)
    {
        switch (field)
        {
            case "title": edit.Title = val; break;
            case "artist": edit.Artist = val; break;
            case "album": edit.Album = val; break;
            case "albumartist": case "album_artist": edit.AlbumArtist = val; break;
            case "genre": edit.Genre = val; break;
            case "comment": edit.Comment = val; break;
            case "composer": edit.Composer = val; break;
            case "track": if (uint.TryParse(val, out var tr)) edit.Track = tr; break;
            case "trackcount": if (uint.TryParse(val, out var tc)) edit.TrackCount = tc; break;
            case "year": if (uint.TryParse(val, out var yr)) edit.Year = yr; break;
            case "disc": if (uint.TryParse(val, out var ds)) edit.Disc = ds; break;
        }
    }

    /// <summary>
    /// 由標籤砌新檔名（唔含副檔名）· Build a new filename (without extension) from a pattern + a track's tags.
    /// Tokens: %artist% %title% %album% %albumartist% %track% %year% %genre% %disc% %composer%.
    /// </summary>
    public static string BuildFileName(TrackTags t, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return t.FileName;
        string Token(string f) => f switch
        {
            "title" => t.Title ?? "",
            "artist" => t.Artist ?? "",
            "album" => t.Album ?? "",
            "albumartist" or "album_artist" => t.AlbumArtist ?? "",
            "genre" => t.Genre ?? "",
            "composer" => t.Composer ?? "",
            "comment" => t.Comment ?? "",
            "track" => t.Track > 0 ? t.Track.ToString("00") : "",
            "year" => t.YearText,
            "disc" => t.Disc > 0 ? t.Disc.ToString() : "",
            _ => "",
        };

        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < pattern.Length)
        {
            if (pattern[i] == '%')
            {
                int end = pattern.IndexOf('%', i + 1);
                if (end < 0) { sb.Append(pattern[i]); i++; continue; }
                sb.Append(Token(pattern.Substring(i + 1, end - i - 1).ToLowerInvariant()));
                i = end + 1;
            }
            else { sb.Append(pattern[i]); i++; }
        }
        return Sanitize(sb.ToString());
    }

    /// <summary>實際改名（保留副檔名同所在資料夾）· Rename on disk, keeping extension + folder; returns new path.</summary>
    public static string Rename(string path, string newBaseName)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var ext = Path.GetExtension(path);
        var target = Path.Combine(dir, newBaseName + ext);
        if (string.Equals(target, path, StringComparison.OrdinalIgnoreCase)) return path;
        // avoid clobbering an existing different file
        if (System.IO.File.Exists(target))
        {
            int n = 2;
            string candidate;
            do { candidate = Path.Combine(dir, $"{newBaseName} ({n++}){ext}"); }
            while (System.IO.File.Exists(candidate));
            target = candidate;
        }
        System.IO.File.Move(path, target);
        return target;
    }

    private static string Sanitize(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        name = name.Trim().Trim('.');
        return name.Length == 0 ? "untitled" : name;
    }

    private static string[] SplitMulti(string s)
        => string.IsNullOrWhiteSpace(s)
            ? Array.Empty<string>()
            : s.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
