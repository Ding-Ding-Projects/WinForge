using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 花式文字 / Unicode 風格轉換器 · Fancy-text / Unicode styler. Pure managed C#: maps ASCII text into
/// many Unicode "font" styles (Mathematical Bold, Fraktur, Circled, Fullwidth, combining strikethrough,
/// leetspeak, upside-down, …) using per-style offset tables + <see cref="char.ConvertFromUtf32"/> so
/// astral-plane codepoints render correctly. Every method is robust and never throws; non-mappable
/// characters pass through unchanged. No redirect, no external process.
/// </summary>
public static class LeetService
{
    /// <summary>A named output style.</summary>
    public sealed class Style
    {
        public string Name { get; }
        private readonly Func<string, string> _fn;
        public Style(string name, Func<string, string> fn) { Name = name; _fn = fn; }
        public string Apply(string input)
        {
            try { return _fn(input ?? string.Empty) ?? string.Empty; }
            catch { return input ?? string.Empty; }
        }
    }

    /// <summary>Build the ordered list of styles, localized names via a caller-supplied picker.</summary>
    public static IReadOnlyList<Style> BuildStyles(Func<string, string, string> pick)
    {
        // pick(en, zh) — never throws in practice; guarded anyway.
        string P(string en, string zh)
        {
            try { return pick != null ? (pick(en, zh) ?? en) : en; }
            catch { return en; }
        }

        return new List<Style>
        {
            new(P("Bold", "粗體"),            OffsetAlnum(0x1D400, 0x1D41A, 0x1D7CE)),
            new(P("Italic", "斜體"),          OffsetAlpha(0x1D434, 0x1D44E)),
            new(P("Bold Italic", "粗斜體"),    OffsetAlpha(0x1D468, 0x1D482)),
            new(P("Monospace", "等寬"),        OffsetAlnum(0x1D670, 0x1D68A, 0x1D7F6)),
            new(P("Sans-serif", "無襯線"),     OffsetAlnum(0x1D5A0, 0x1D5BA, 0x1D7E2)),
            new(P("Double-struck", "空心"),    OffsetAlnum(0x1D538, 0x1D552, 0x1D7D8)),
            new(P("Script", "花體"),          OffsetAlpha(0x1D49C, 0x1D4B6)),
            new(P("Fraktur", "哥德體"),        OffsetAlpha(0x1D504, 0x1D51E)),
            new(P("Circled", "圓圈"),          OffsetAlpha(0x24B6, 0x24D0)),
            new(P("Fullwidth", "全形"),        Fullwidth),
            new(P("Small caps", "小型大寫"),    Table(SmallCaps)),
            new(P("Upside-down", "倒轉"),      UpsideDown),
            new(P("Strikethrough", "刪除線"),  Combining('̶')),
            new(P("Underline", "底線"),        Combining('̲')),
            new(P("Leetspeak", "火星文 leet"), Leet),
        };
    }

    // --- Offset-based styles (contiguous Unicode blocks) --------------------------------------

    /// <summary>A-Z and a-z each map to a contiguous block starting at the given base codepoints.</summary>
    private static Func<string, string> OffsetAlpha(int upperBase, int lowerBase) =>
        input => MapRunes(input, r =>
        {
            if (r >= 'A' && r <= 'Z') return upperBase + (r - 'A');
            if (r >= 'a' && r <= 'z') return lowerBase + (r - 'a');
            return -1;
        });

    /// <summary>Letters + digits contiguous blocks.</summary>
    private static Func<string, string> OffsetAlnum(int upperBase, int lowerBase, int digitBase) =>
        input => MapRunes(input, r =>
        {
            if (r >= 'A' && r <= 'Z') return upperBase + (r - 'A');
            if (r >= 'a' && r <= 'z') return lowerBase + (r - 'a');
            if (r >= '0' && r <= '9') return digitBase + (r - '0');
            return -1;
        });

    /// <summary>Fullwidth forms: ASCII 0x21..0x7E → 0xFF01.. ; space → ideographic space.</summary>
    private static string Fullwidth(string input) => MapRunes(input, r =>
    {
        if (r == ' ') return 0x3000;
        if (r >= 0x21 && r <= 0x7E) return 0xFF01 + (r - 0x21);
        return -1;
    });

    // --- Table-based styles --------------------------------------------------------------------

    private static Func<string, string> Table(IReadOnlyDictionary<char, string> map) =>
        input =>
        {
            var sb = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (map.TryGetValue(char.ToLowerInvariant(c), out var s)) sb.Append(s);
                else sb.Append(c);
            }
            return sb.ToString();
        };

    /// <summary>Insert a combining mark after each character (strikethrough / underline).</summary>
    private static Func<string, string> Combining(char mark) =>
        input =>
        {
            var sb = new StringBuilder(input.Length * 2);
            foreach (char c in input)
            {
                sb.Append(c);
                if (!char.IsWhiteSpace(c)) sb.Append(mark);
            }
            return sb.ToString();
        };

    private static string UpsideDown(string input)
    {
        var sb = new StringBuilder(input.Length);
        // Reverse so the result reads correctly when flipped.
        for (int i = input.Length - 1; i >= 0; i--)
        {
            char c = input[i];
            if (Flip.TryGetValue(char.ToLowerInvariant(c), out var s)) sb.Append(s);
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static string Leet(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            char lc = char.ToLowerInvariant(c);
            char? rep = lc switch
            {
                'a' => '4',
                'e' => '3',
                'i' => '1',
                'o' => '0',
                's' => '5',
                't' => '7',
                _ => null,
            };
            sb.Append(rep ?? c);
        }
        return sb.ToString();
    }

    // --- Core mapper ---------------------------------------------------------------------------

    /// <summary>
    /// Walk the input as Unicode scalar runes; <paramref name="map"/> returns a target codepoint or -1
    /// to pass the original rune through. Astral targets are emitted via <see cref="char.ConvertFromUtf32"/>.
    /// </summary>
    private static string MapRunes(string input, Func<int, int> map)
    {
        var sb = new StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            int cp;
            char c = input[i];
            if (char.IsHighSurrogate(c) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
            {
                cp = char.ConvertToUtf32(c, input[i + 1]);
                i++;
            }
            else cp = c;

            int mapped = -1;
            try { mapped = map(cp); } catch { mapped = -1; }

            if (mapped >= 0)
            {
                try { sb.Append(char.ConvertFromUtf32(mapped)); }
                catch { sb.Append(char.ConvertFromUtf32(cp)); }
            }
            else sb.Append(char.ConvertFromUtf32(cp));
        }
        return sb.ToString();
    }

    // --- Data tables ---------------------------------------------------------------------------

    private static readonly Dictionary<char, string> SmallCaps = new()
    {
        ['a'] = "ᴀ", ['b'] = "ʙ", ['c'] = "ᴄ", ['d'] = "ᴅ", ['e'] = "ᴇ", ['f'] = "ꜰ",
        ['g'] = "ɢ", ['h'] = "ʜ", ['i'] = "ɪ", ['j'] = "ᴊ", ['k'] = "ᴋ", ['l'] = "ʟ",
        ['m'] = "ᴍ", ['n'] = "ɴ", ['o'] = "ᴏ", ['p'] = "ᴘ", ['q'] = " q", ['r'] = "ʀ",
        ['s'] = "s", ['t'] = "ᴛ", ['u'] = "ᴜ", ['v'] = "ᴠ", ['w'] = "ᴡ", ['x'] = "x",
        ['y'] = "ʏ", ['z'] = "ᴢ",
    };

    private static readonly Dictionary<char, string> Flip = new()
    {
        ['a'] = "ɐ", ['b'] = "q", ['c'] = "ɔ", ['d'] = "p", ['e'] = "ǝ", ['f'] = "ɟ",
        ['g'] = "ƃ", ['h'] = "ɥ", ['i'] = "ᴉ", ['j'] = "ɾ", ['k'] = "ʞ", ['l'] = "l",
        ['m'] = "ɯ", ['n'] = "u", ['o'] = "o", ['p'] = "d", ['q'] = "b", ['r'] = "ɹ",
        ['s'] = "s", ['t'] = "ʇ", ['u'] = "n", ['v'] = "ʌ", ['w'] = "ʍ", ['x'] = "x",
        ['y'] = "ʎ", ['z'] = "z",
        ['0'] = "0", ['1'] = "Ɩ", ['2'] = "ᄅ", ['3'] = "Ɛ", ['4'] = "ㄣ", ['5'] = "ϛ",
        ['6'] = "9", ['7'] = "ㄥ", ['8'] = "8", ['9'] = "6",
        ['.'] = "˙", [','] = "'", ['?'] = "¿", ['!'] = "¡", ['\''] = ",", ['"'] = ",,",
        ['('] = ")", [')'] = "(", ['['] = "]", [']'] = "[", ['{'] = "}", ['}'] = "{",
        ['<'] = ">", ['>'] = "<", ['&'] = "⅋", ['_'] = "‾",
    };
}
