using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// Unicode 檢查器 · Unicode inspector engine — pure managed. Enumerates text by <see cref="Rune"/>
/// (correct surrogate-pair handling) and reports per-code-point facts plus a length summary.
/// Robust: every method swallows exceptions and returns sane defaults; never throws.
/// </summary>
public static class UnicodeInspectService
{
    /// <summary>One inspected code point. Plain fields for classic {Binding} in DataTemplates.</summary>
    public sealed class CodePointInfo
    {
        public int Index { get; set; }          // 1-based row number
        public string Glyph { get; set; } = "";  // safe-to-render display glyph
        public string CodePoint { get; set; } = ""; // U+XXXX
        public string Escape { get; set; } = "";  // \uXXXX / \UXXXXXXXX form to copy
        public string Decimal { get; set; } = ""; // decimal scalar value
        public string Utf8 { get; set; } = "";    // UTF-8 bytes as hex
        public string Utf16 { get; set; } = "";   // UTF-16 code units as hex
        public string Category { get; set; } = ""; // Unicode general category
        public string Flags { get; set; } = "";    // combining / control / whitespace / emoji-ish etc.
        public string Name { get; set; } = "";     // localized short note
        public bool Hidden { get; set; }            // hidden / confusable → highlight in UI
    }

    /// <summary>Aggregate length figures that explain why "length" differs.</summary>
    public sealed class Summary
    {
        public int CodePoints { get; set; }
        public int Utf16Units { get; set; }   // string.Length
        public int Utf8Bytes { get; set; }
        public int HiddenCount { get; set; }
    }

    /// <summary>Result bundle for one Inspect() call.</summary>
    public sealed class Result
    {
        public List<CodePointInfo> Rows { get; } = new();
        public Summary Totals { get; } = new();
        public List<string> HiddenNotes { get; } = new(); // human-readable hidden-char warnings
    }

    /// <summary>Inspect <paramref name="text"/>. Never throws — returns an empty result on error.</summary>
    public static Result Inspect(string? text, Func<string, string, string> pick)
    {
        var result = new Result();
        try
        {
            text ??= "";
            var seenHidden = new HashSet<string>();

            foreach (Rune rune in text.EnumerateRunes())
            {
                CodePointInfo info;
                try { info = Describe(rune, result.Rows.Count + 1, pick); }
                catch { continue; }

                result.Rows.Add(info);
                if (info.Hidden && seenHidden.Add(info.CodePoint))
                    result.HiddenNotes.Add($"{info.CodePoint} — {info.Name}");
            }

            result.Totals.CodePoints = result.Rows.Count;
            result.Totals.Utf16Units = text.Length;
            result.Totals.Utf8Bytes = SafeUtf8ByteCount(text);
            foreach (var r in result.Rows) if (r.Hidden) result.Totals.HiddenCount++;
        }
        catch
        {
            // never throw
        }
        return result;
    }

    private static CodePointInfo Describe(Rune rune, int index, Func<string, string, string> pick)
    {
        int value = rune.Value;
        var info = new CodePointInfo { Index = index };

        // U+XXXX (min 4 hex digits, more for astral)
        info.CodePoint = "U+" + value.ToString(value > 0xFFFF ? "X6" : "X4", CultureInfo.InvariantCulture);
        info.Decimal = value.ToString(CultureInfo.InvariantCulture);
        info.Escape = value <= 0xFFFF
            ? "\\u" + value.ToString("X4", CultureInfo.InvariantCulture)
            : "\\U" + value.ToString("X8", CultureInfo.InvariantCulture);

        // UTF-8 bytes
        try
        {
            Span<byte> buf = stackalloc byte[4];
            int n = rune.EncodeToUtf8(buf);
            var sb = new StringBuilder(n * 3);
            for (int i = 0; i < n; i++) { if (i > 0) sb.Append(' '); sb.Append(buf[i].ToString("X2", CultureInfo.InvariantCulture)); }
            info.Utf8 = sb.ToString();
        }
        catch { info.Utf8 = "?"; }

        // UTF-16 code units
        try
        {
            Span<char> u = stackalloc char[2];
            int n = rune.EncodeToUtf16(u);
            var sb = new StringBuilder(n * 5);
            for (int i = 0; i < n; i++) { if (i > 0) sb.Append(' '); sb.Append(((int)u[i]).ToString("X4", CultureInfo.InvariantCulture)); }
            info.Utf16 = sb.ToString();
        }
        catch { info.Utf16 = "?"; }

        // Unicode general category
        UnicodeCategory cat;
        try { cat = CharUnicodeInfo.GetUnicodeCategory(rune.Value); }
        catch { cat = UnicodeCategory.OtherNotAssigned; }
        info.Category = cat.ToString();

        // Classification flags
        var flags = new List<string>();
        bool combining = cat is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;
        bool control = cat is UnicodeCategory.Control or UnicodeCategory.Format;
        bool whitespace = false;
        try { whitespace = Rune.IsWhiteSpace(rune); } catch { }
        bool emojiish = IsEmojiish(value);

        if (combining) flags.Add(pick("combining", "組合"));
        if (control) flags.Add(pick("control", "控制"));
        if (whitespace) flags.Add(pick("whitespace", "空白"));
        if (emojiish) flags.Add(pick("emoji", "表情符號"));
        if (value > 0xFFFF) flags.Add(pick("astral (surrogate pair)", "輔助平面（代理對）"));
        info.Flags = string.Join(", ", flags);

        // Hidden / confusable detection
        string? hiddenNote = HiddenName(value, pick);
        if (hiddenNote != null)
        {
            info.Hidden = true;
            info.Name = hiddenNote;
        }
        else
        {
            info.Name = combining ? pick("combining mark", "組合符號")
                : control ? pick("control / format", "控制／格式")
                : cat.ToString();
        }

        // Display glyph — never render a bare control/hidden char (it breaks the ListView)
        if (info.Hidden || control || combining)
            info.Glyph = pick("(hidden)", "（隱藏）");
        else
        {
            try { info.Glyph = rune.ToString(); } catch { info.Glyph = "?"; }
            if (string.IsNullOrEmpty(info.Glyph)) info.Glyph = "?";
        }

        return info;
    }

    /// <summary>Names common hidden / confusable code points; null when the char is ordinary.</summary>
    private static string? HiddenName(int v, Func<string, string, string> pick) => v switch
    {
        0x0000 => pick("NULL", "空字元 NULL"),
        0x00A0 => pick("no-break space (looks like a space)", "不換行空格（似普通空格）"),
        0x00AD => pick("soft hyphen (invisible)", "軟連字號（隱形）"),
        0x200B => pick("zero-width space", "零寬空格"),
        0x200C => pick("zero-width non-joiner", "零寬不連字"),
        0x200D => pick("zero-width joiner", "零寬連字"),
        0x200E => pick("left-to-right mark", "由左至右標記"),
        0x200F => pick("right-to-left mark", "由右至左標記"),
        0x202A => pick("left-to-right embedding (RTL control)", "由左至右嵌入（方向控制）"),
        0x202B => pick("right-to-left embedding (RTL control)", "由右至左嵌入（方向控制）"),
        0x202C => pick("pop directional formatting", "彈出方向格式"),
        0x202D => pick("left-to-right override (RTL control)", "由左至右覆寫（方向控制）"),
        0x202E => pick("right-to-left override (RTL control)", "由右至左覆寫（方向控制）"),
        0x2060 => pick("word joiner (invisible)", "文字連接符（隱形）"),
        0x2066 => pick("left-to-right isolate", "由左至右隔離"),
        0x2067 => pick("right-to-left isolate", "由右至左隔離"),
        0x2068 => pick("first strong isolate", "首個強方向隔離"),
        0x2069 => pick("pop directional isolate", "彈出方向隔離"),
        0xFEFF => pick("byte-order mark / zero-width no-break space", "位元組順序記號 BOM／零寬不換行空格"),
        0xFFFC => pick("object replacement character", "物件替換字元"),
        0xFFFD => pick("replacement character (decode error)", "替換字元（解碼錯誤）"),
        0x115F => pick("Hangul filler (invisible)", "諺文填充（隱形）"),
        0x3164 => pick("Hangul filler (invisible)", "諺文填充（隱形）"),
        0x180E => pick("Mongolian vowel separator (invisible)", "蒙古元音分隔（隱形）"),
        _ => null,
    };

    /// <summary>Loose "emoji-ish" heuristic covering the common pictographic ranges.</summary>
    private static bool IsEmojiish(int v) =>
        (v >= 0x1F300 && v <= 0x1FAFF) ||   // misc symbols/pictographs, emoji, supplemental
        (v >= 0x2600 && v <= 0x27BF) ||     // misc symbols + dingbats
        (v >= 0x1F000 && v <= 0x1F2FF) ||   // mahjong/dominoes/playing cards/enclosed
        v == 0x2764 || v == 0x2B50 ||       // heart, star
        (v >= 0xFE00 && v <= 0xFE0F) ||     // variation selectors (emoji presentation)
        (v >= 0x1F1E6 && v <= 0x1F1FF);     // regional indicators (flags)

    private static int SafeUtf8ByteCount(string s)
    {
        try { return Encoding.UTF8.GetByteCount(s); }
        catch { return 0; }
    }
}
