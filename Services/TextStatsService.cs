using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 文字統計同可讀性 · Text statistics &amp; readability — pure managed analysis of arbitrary text.
/// Counts characters/words/sentences/paragraphs, estimates reading &amp; speaking time, and computes
/// Flesch Reading Ease + Flesch–Kincaid grade level via a syllable-estimation heuristic. No redirect,
/// no side-effects, never throws. Also builds a top-N word-frequency list (stop-words optional).
/// </summary>
public static class TextStatsService
{
    /// <summary>One word + its occurrence count, for the frequency ListView (classic binding).</summary>
    public sealed class WordCount
    {
        public string Word { get; set; } = "";
        public int Count { get; set; }
    }

    /// <summary>Immutable snapshot of every computed metric. All fields are always populated.</summary>
    public sealed class Stats
    {
        public int Characters { get; set; }
        public int CharactersNoSpaces { get; set; }
        public int Words { get; set; }
        public int UniqueWords { get; set; }
        public int Sentences { get; set; }
        public int Paragraphs { get; set; }
        public int Syllables { get; set; }
        public double AvgWordLength { get; set; }
        public double AvgSentenceLength { get; set; }
        public double ReadingMinutes { get; set; }   // ~200 wpm
        public double SpeakingMinutes { get; set; }   // ~130 wpm
        public double FleschReadingEase { get; set; }
        public double FleschKincaidGrade { get; set; }
        public List<WordCount> TopWords { get; set; } = new();
    }

    private const double ReadWpm = 200.0;
    private const double SpeakWpm = 130.0;

    // Common English stop-words, kept small &amp; self-contained (no external data).
    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","and","or","but","if","of","to","in","on","at","by","for","with","as","is",
        "are","was","were","be","been","being","it","its","this","that","these","those","i","you",
        "he","she","they","we","me","him","her","them","us","my","your","his","their","our","not",
        "no","do","does","did","so","than","then","too","very","can","will","just","from","up","out",
        "about","into","over","after","under","again","once","here","there","when","where","why","how",
        "all","any","both","each","few","more","most","other","some","such","only","own","same"
    };

    /// <summary>
    /// Analyse <paramref name="text"/> and return a fully-populated <see cref="Stats"/>. Never throws —
    /// any unexpected failure yields an empty (all-zero) result so the UI stays responsive.
    /// </summary>
    public static Stats Analyze(string? text, bool ignoreStopWords, int topN = 10)
    {
        var s = new Stats();
        try
        {
            text ??= "";
            if (topN < 1) topN = 1;

            s.Characters = text.Length;

            int noSpace = 0;
            foreach (char c in text) if (!char.IsWhiteSpace(c)) noSpace++;
            s.CharactersNoSpaces = noSpace;

            // Words: runs of letters/digits/apostrophes/CJK. CJK chars count individually.
            var words = Tokenize(text);
            s.Words = words.Count;

            // Sentences: count terminal punctuation runs; guarantee ≥1 when any word exists.
            s.Sentences = CountSentences(text);
            if (s.Sentences == 0 && s.Words > 0) s.Sentences = 1;

            // Paragraphs: blank-line separated blocks; ≥1 when any non-whitespace exists.
            s.Paragraphs = CountParagraphs(text);

            int totalWordChars = 0;
            int totalSyllables = 0;
            var freq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var uniq = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var w in words)
            {
                totalWordChars += w.Length;
                totalSyllables += SyllablesIn(w);
                uniq.Add(w);
                if (ignoreStopWords && Stop.Contains(w)) continue;
                freq[w] = freq.TryGetValue(w, out int n) ? n + 1 : 1;
            }
            s.UniqueWords = uniq.Count;
            s.Syllables = totalSyllables;

            s.AvgWordLength = s.Words > 0 ? (double)totalWordChars / s.Words : 0;
            s.AvgSentenceLength = s.Sentences > 0 ? (double)s.Words / s.Sentences : 0;

            s.ReadingMinutes = s.Words / ReadWpm;
            s.SpeakingMinutes = s.Words / SpeakWpm;

            // Flesch Reading Ease / Flesch–Kincaid grade (only meaningful with real text).
            if (s.Words > 0 && s.Sentences > 0)
            {
                double wps = (double)s.Words / s.Sentences;
                double spw = (double)totalSyllables / s.Words;
                s.FleschReadingEase = 206.835 - 1.015 * wps - 84.6 * spw;
                s.FleschKincaidGrade = 0.39 * wps + 11.8 * spw - 15.59;
                s.FleschReadingEase = Math.Round(Clamp(s.FleschReadingEase, -100, 121), 1);
                s.FleschKincaidGrade = Math.Round(Math.Max(0, s.FleschKincaidGrade), 1);
            }

            s.TopWords = freq
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Take(topN)
                .Select(kv => new WordCount { Word = kv.Key, Count = kv.Value })
                .ToList();
        }
        catch
        {
            return new Stats();
        }
        return s;
    }

    /// <summary>Format a fractional-minute duration as a friendly "Xm Ys" / "Ys" string.</summary>
    public static string FormatDuration(double minutes)
    {
        try
        {
            if (minutes <= 0) return "0s";
            int totalSec = (int)Math.Round(minutes * 60);
            if (totalSec < 1) totalSec = 1;
            int m = totalSec / 60;
            int sec = totalSec % 60;
            return m > 0 ? $"{m}m {sec:00}s" : $"{sec}s";
        }
        catch { return "0s"; }
    }

    private static List<string> Tokenize(string text)
    {
        var list = new List<string>();
        try
        {
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (IsCjk(c))
                {
                    if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
                    list.Add(c.ToString()); // each CJK glyph is a "word"
                }
                else if (char.IsLetterOrDigit(c) || c == '\'' || c == '’')
                {
                    sb.Append(c);
                }
                else
                {
                    if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
                }
            }
            if (sb.Length > 0) list.Add(sb.ToString());
        }
        catch { }
        return list;
    }

    private static bool IsCjk(char c) =>
        (c >= 0x4E00 && c <= 0x9FFF) ||   // CJK Unified Ideographs
        (c >= 0x3400 && c <= 0x4DBF) ||   // Extension A
        (c >= 0x3040 && c <= 0x30FF) ||   // Hiragana + Katakana
        (c >= 0xF900 && c <= 0xFAFF);     // CJK Compatibility Ideographs

    private static int CountSentences(string text)
    {
        int count = 0;
        bool inRun = false;
        foreach (char c in text)
        {
            bool term = c == '.' || c == '!' || c == '?' ||
                        c == '。' || c == '！' || c == '？'; // 。！？
            if (term)
            {
                if (!inRun) { count++; inRun = true; }
            }
            else if (!char.IsWhiteSpace(c))
            {
                inRun = false;
            }
        }
        return count;
    }

    private static int CountParagraphs(string text)
    {
        var blocks = text.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split(new[] { "\n\n" }, StringSplitOptions.None);
        int count = 0;
        foreach (var b in blocks)
            if (!string.IsNullOrWhiteSpace(b)) count++;
        return count;
    }

    /// <summary>Heuristic English syllable estimate; CJK glyphs count as one syllable each.</summary>
    private static int SyllablesIn(string word)
    {
        if (string.IsNullOrEmpty(word)) return 0;
        if (word.Length == 1 && IsCjk(word[0])) return 1;

        string w = word.ToLowerInvariant();
        var letters = new StringBuilder();
        foreach (char c in w) if (c >= 'a' && c <= 'z') letters.Append(c);
        w = letters.ToString();
        if (w.Length == 0) return 1; // pure-digit / symbol token → count as 1

        int count = 0;
        bool prevVowel = false;
        for (int i = 0; i < w.Length; i++)
        {
            bool vowel = "aeiouy".IndexOf(w[i]) >= 0;
            if (vowel && !prevVowel) count++;
            prevVowel = vowel;
        }
        // Silent trailing 'e'.
        if (w.EndsWith("e") && count > 1) count--;
        return Math.Max(1, count);
    }

    private static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
}
