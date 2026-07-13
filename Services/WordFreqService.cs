using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 詞頻／字頻統計 · Word-frequency analyzer. Pure managed, never throws.
/// Counts words, bigrams or characters from a block of text with options for
/// case-folding, minimum length, punctuation stripping and English stop-words.
/// </summary>
public static class WordFreqService
{
    public enum Mode { Words, Bigrams, Characters }

    /// <summary>One ranked row for the results ListView (classic {Binding}).</summary>
    public sealed class FreqRow
    {
        public int Rank { get; set; }
        public string Term { get; set; } = string.Empty;
        public int Count { get; set; }
        public double BarWidth { get; set; }      // 0..220 px relative to the top term
        public string Percent { get; set; } = string.Empty;
    }

    /// <summary>Whole-analysis result — rows plus headline totals.</summary>
    public sealed class Result
    {
        public List<FreqRow> Rows { get; } = new();
        public int TotalTokens { get; set; }
        public int UniqueTokens { get; set; }
        public double Diversity { get; set; }     // unique / total, 0..1
    }

    // A small, common English stop-word list.
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","and","are","as","at","be","been","but","by","for","from","had","has",
        "have","he","her","him","his","i","if","in","into","is","it","its","me","my","no",
        "not","of","on","or","our","she","so","that","the","their","them","then","there",
        "these","they","this","to","was","we","were","what","when","which","who","will",
        "with","you","your","would","could","should","been","being","do","does","did",
        "just","than","too","very","can","us","am"
    };

    private const double MaxBar = 220.0;

    /// <summary>
    /// Analyze <paramref name="text"/> and return ranked frequencies. Never throws.
    /// </summary>
    public static Result Analyze(string? text, Mode mode, bool caseInsensitive,
        int minLength, bool stripPunctuation, bool removeStopWords)
    {
        var result = new Result();
        try
        {
            if (string.IsNullOrWhiteSpace(text)) return result;
            if (minLength < 1) minLength = 1;

            List<string> tokens = mode switch
            {
                Mode.Characters => Characters(text!, caseInsensitive),
                Mode.Bigrams => Bigrams(text!, caseInsensitive, stripPunctuation, removeStopWords, minLength),
                _ => Words(text!, caseInsensitive, stripPunctuation, removeStopWords, minLength),
            };

            if (tokens.Count == 0) return result;

            var counts = new Dictionary<string, int>(mode == Mode.Bigrams
                ? StringComparer.Ordinal
                : StringComparer.Ordinal);
            foreach (var t in tokens)
            {
                counts.TryGetValue(t, out int c);
                counts[t] = c + 1;
            }

            result.TotalTokens = tokens.Count;
            result.UniqueTokens = counts.Count;
            result.Diversity = tokens.Count > 0 ? (double)counts.Count / tokens.Count : 0.0;

            var ordered = counts
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            int max = ordered[0].Value;
            int rank = 1;
            foreach (var kv in ordered)
            {
                double frac = max > 0 ? (double)kv.Value / max : 0.0;
                double pctOfTotal = result.TotalTokens > 0
                    ? (double)kv.Value * 100.0 / result.TotalTokens : 0.0;
                result.Rows.Add(new FreqRow
                {
                    Rank = rank++,
                    Term = kv.Key,
                    Count = kv.Value,
                    BarWidth = Math.Max(2.0, frac * MaxBar),
                    Percent = pctOfTotal.ToString("0.0", CultureInfo.InvariantCulture) + "%",
                });
            }
        }
        catch
        {
            // Never throw — return whatever we have.
        }
        return result;
    }

    private static List<string> Words(string text, bool caseInsensitive, bool stripPunctuation,
        bool removeStopWords, int minLength)
    {
        var list = new List<string>();
        foreach (var raw in Tokenize(text, stripPunctuation))
        {
            string w = caseInsensitive ? raw.ToLowerInvariant() : raw;
            if (w.Length < minLength) continue;
            if (removeStopWords && StopWords.Contains(w)) continue;
            list.Add(w);
        }
        return list;
    }

    private static List<string> Bigrams(string text, bool caseInsensitive, bool stripPunctuation,
        bool removeStopWords, int minLength)
    {
        var words = Words(text, caseInsensitive, stripPunctuation, removeStopWords, minLength);
        var list = new List<string>();
        for (int i = 0; i + 1 < words.Count; i++)
            list.Add(words[i] + " " + words[i + 1]);
        return list;
    }

    private static List<string> Characters(string text, bool caseInsensitive)
    {
        var list = new List<string>();
        // A C# char is a UTF-16 code unit, not necessarily a user-visible Unicode scalar.
        // Enumerating chars split astral symbols such as 😀 into two invalid surrogate rows.
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune)) continue;
            string s = rune.ToString();
            if (caseInsensitive) s = s.ToLowerInvariant();
            list.Add(s);
        }
        return list;
    }

    // Split into word tokens. When stripping punctuation, keep letters/digits (and
    // in-word apostrophes/hyphens); otherwise split on whitespace only.
    private static IEnumerable<string> Tokenize(string text, bool stripPunctuation)
    {
        if (!stripPunctuation)
        {
            foreach (var part in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                yield return part;
            yield break;
        }

        var sb = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch) || ch == '\'' || ch == '-')
            {
                sb.Append(ch);
            }
            else if (sb.Length > 0)
            {
                yield return Trim(sb.ToString());
                sb.Clear();
            }
        }
        if (sb.Length > 0) yield return Trim(sb.ToString());
    }

    private static string Trim(string s) => s.Trim('\'', '-');

    /// <summary>Render the ranked table as CSV text. Never throws.</summary>
    public static string ToCsv(Result result)
    {
        var sb = new StringBuilder();
        try
        {
            sb.AppendLine("Rank,Term,Count,Percent");
            foreach (var r in result.Rows)
                sb.Append(r.Rank).Append(',')
                  .Append(Escape(r.Term)).Append(',')
                  .Append(r.Count).Append(',')
                  .Append(r.Percent).Append('\n');
        }
        catch { /* never throw */ }
        return sb.ToString();
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
