using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 摩斯電碼 · International Morse code encode/decode. Pure managed, never-throws helpers.
/// Text→Morse (A–Z, 0–9 and common punctuation) and Morse→Text (tolerant parsing),
/// plus a timing expansion used by the UI flash preview.
/// </summary>
public static class MorseService
{
    // International Morse (ITU) — letters, digits, common punctuation/prosigns.
    private static readonly Dictionary<char, string> Map = new()
    {
        ['A'] = ".-",   ['B'] = "-...", ['C'] = "-.-.", ['D'] = "-..",  ['E'] = ".",
        ['F'] = "..-.", ['G'] = "--.",  ['H'] = "....", ['I'] = "..",   ['J'] = ".---",
        ['K'] = "-.-",  ['L'] = ".-..", ['M'] = "--",   ['N'] = "-.",   ['O'] = "---",
        ['P'] = ".--.", ['Q'] = "--.-", ['R'] = ".-.",  ['S'] = "...",  ['T'] = "-",
        ['U'] = "..-",  ['V'] = "...-", ['W'] = ".--",  ['X'] = "-..-", ['Y'] = "-.--",
        ['Z'] = "--..",
        ['0'] = "-----", ['1'] = ".----", ['2'] = "..---", ['3'] = "...--", ['4'] = "....-",
        ['5'] = ".....", ['6'] = "-....", ['7'] = "--...", ['8'] = "---..", ['9'] = "----.",
        ['.'] = ".-.-.-", [','] = "--..--", ['?'] = "..--..", ['\''] = ".----.",
        ['!'] = "-.-.--", ['/'] = "-..-.", ['('] = "-.--.", [')'] = "-.--.-",
        ['&'] = ".-...", [':'] = "---...", [';'] = "-.-.-.", ['='] = "-...-",
        ['+'] = ".-.-.", ['-'] = "-....-", ['_'] = "..--.-", ['"'] = ".-..-.",
        ['$'] = "...-..-", ['@'] = ".--.-.",
    };

    private static readonly Dictionary<string, char> Reverse = BuildReverse();

    private static Dictionary<string, char> BuildReverse()
    {
        var d = new Dictionary<string, char>();
        foreach (var kv in Map)
            d[kv.Value] = kv.Key; // '(' / ')' etc. all distinct
        return d;
    }

    /// <summary>Encode plain text to Morse. Letters joined by <paramref name="letterSep"/>,
    /// words by <paramref name="wordSep"/>. Unknown chars are collected into
    /// <paramref name="unknown"/> and rendered as '#'. Never throws.</summary>
    public static string ToMorse(string? text, string letterSep, string wordSep, out List<char> unknown)
    {
        unknown = new List<char>();
        var sb = new StringBuilder();
        try
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (string.IsNullOrEmpty(letterSep)) letterSep = " ";
            if (string.IsNullOrEmpty(wordSep)) wordSep = " / ";

            // Split on whitespace runs into words; preserve word boundaries.
            var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int w = 0; w < words.Length; w++)
            {
                if (w > 0) sb.Append(wordSep);
                var letters = new List<string>();
                foreach (var raw in words[w])
                {
                    char c = char.ToUpperInvariant(raw);
                    if (Map.TryGetValue(c, out var code)) letters.Add(code);
                    else { if (!unknown.Contains(raw)) unknown.Add(raw); letters.Add("#"); }
                }
                sb.Append(string.Join(letterSep, letters));
            }
        }
        catch { /* never throw */ }
        return sb.ToString();
    }

    /// <summary>Decode Morse to text. Tolerant of spacing: treats "/" or multiple spaces
    /// as word gaps, single spaces as letter gaps. Accepts common dot/dash aliases. Never throws.</summary>
    public static string FromMorse(string? morse)
    {
        var sb = new StringBuilder();
        try
        {
            if (string.IsNullOrWhiteSpace(morse)) return string.Empty;

            // Normalise unicode dot/dash variants to '.' and '-'.
            var norm = new StringBuilder(morse.Length);
            foreach (var ch in morse)
            {
                switch (ch)
                {
                    case '·': case '•': case '.': norm.Append('.'); break;
                    case '–': case '—': case '_': case '-': norm.Append('-'); break;
                    case '|': norm.Append('/'); break; // common word-gap glyph
                    default: norm.Append(ch); break;
                }
            }
            var s = norm.ToString();

            // Word boundaries: '/' or 3+ spaces. First split on '/'.
            var words = s.Split('/');
            bool firstWord = true;
            foreach (var word in words)
            {
                var trimmed = word.Trim();
                if (trimmed.Length == 0)
                {
                    // A blank segment (e.g. "a // b") still means a word gap; skip emitting a space run.
                    continue;
                }
                if (!firstWord) sb.Append(' ');
                firstWord = false;

                var letters = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var token in letters)
                {
                    if (token == "#") { sb.Append('#'); continue; }
                    if (Reverse.TryGetValue(token, out var c)) sb.Append(c);
                    else sb.Append('�'); // replacement char for unrecognised token
                }
            }
        }
        catch { /* never throw */ }
        return sb.ToString();
    }

    /// <summary>A single on/off flash segment. <see cref="On"/> = light lit; <see cref="Units"/> = duration in Morse units.</summary>
    public readonly record struct Flash(bool On, int Units);

    /// <summary>Expand text into a flash timeline (dot=1, dash=3, intra-char gap=1, letter gap=3, word gap=7).
    /// Unknown characters are skipped. Never throws.</summary>
    public static List<Flash> BuildTimeline(string? text)
    {
        var list = new List<Flash>();
        try
        {
            if (string.IsNullOrEmpty(text)) return list;
            var words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int w = 0; w < words.Length; w++)
            {
                if (w > 0) AddGap(list, 7); // word gap
                bool firstLetter = true;
                foreach (var raw in words[w])
                {
                    char c = char.ToUpperInvariant(raw);
                    if (!Map.TryGetValue(c, out var code)) continue;
                    if (!firstLetter) AddGap(list, 3); // letter gap
                    firstLetter = false;

                    for (int i = 0; i < code.Length; i++)
                    {
                        if (i > 0) AddGap(list, 1); // intra-char gap
                        list.Add(new Flash(true, code[i] == '-' ? 3 : 1));
                    }
                }
            }
        }
        catch { /* never throw */ }
        return list;
    }

    private static void AddGap(List<Flash> list, int units) => list.Add(new Flash(false, units));

    /// <summary>Duration of one Morse "unit" in milliseconds for a given words-per-minute
    /// (PARIS standard: 1 unit = 1200 / WPM ms). Clamped to a sane range. Never throws.</summary>
    public static double UnitMsForWpm(double wpm)
    {
        try
        {
            if (double.IsNaN(wpm) || wpm < 1) wpm = 1;
            if (wpm > 60) wpm = 60;
            return 1200.0 / wpm;
        }
        catch { return 200.0; }
    }
}
