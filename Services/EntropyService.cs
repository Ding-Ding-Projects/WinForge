using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 熵值分析（隨機度分析）· Shannon-entropy / randomness analyzer. Pure managed, never throws.
/// Interprets an input string as UTF-8 bytes, raw characters, or hex-encoded bytes, then computes
/// Shannon entropy (bits/symbol), total information, theoretical max entropy, a "% of max" ratio,
/// a chi-square uniformity statistic, and a frequency table. No side effects, no I/O.
/// </summary>
public static class EntropyService
{
    /// <summary>How the input string is turned into a stream of symbols.</summary>
    public enum Interpretation
    {
        Utf8Bytes = 0,
        RawChars = 1,
        HexBytes = 2,
    }

    /// <summary>One row of the frequency histogram.</summary>
    public sealed class SymbolFreq
    {
        public string Symbol { get; set; } = "";
        public long Count { get; set; }
        public double Percent { get; set; }
        public string PercentText => Percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
        public string Bar { get; set; } = "";
    }

    /// <summary>Full analysis result. <see cref="Ok"/> is false when the input could not be parsed.</summary>
    public sealed class Report
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }

        public long Count { get; set; }
        public int Unique { get; set; }
        public int AlphabetSize { get; set; }

        public double Entropy { get; set; }        // bits per symbol
        public double TotalInfo { get; set; }       // entropy * count (bits)
        public double MaxEntropy { get; set; }      // log2(alphabet size)
        public double PercentOfMax { get; set; }    // 0..100
        public double ChiSquare { get; set; }       // uniformity statistic

        public List<SymbolFreq> Top { get; set; } = new();
    }

    /// <summary>
    /// Analyze <paramref name="input"/> under the given interpretation. Never throws; on bad input
    /// (e.g. malformed hex) returns a Report with <c>Ok = false</c> and an <c>Error</c> key.
    /// </summary>
    public static Report Analyze(string? input, Interpretation mode, int topN = 24)
    {
        var r = new Report();
        try
        {
            input ??= "";
            IReadOnlyList<string> symbols;

            switch (mode)
            {
                case Interpretation.Utf8Bytes:
                    var bytes = Encoding.UTF8.GetBytes(input);
                    symbols = bytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)).ToArray();
                    break;

                case Interpretation.HexBytes:
                    if (!TryParseHex(input, out var hexBytes, out var hexErr))
                    {
                        r.Ok = false;
                        r.Error = hexErr;
                        return r;
                    }
                    symbols = hexBytes.Select(b => b.ToString("X2", CultureInfo.InvariantCulture)).ToArray();
                    break;

                case Interpretation.RawChars:
                default:
                    // Iterate Unicode text elements (grapheme-safe-ish, at least surrogate-safe).
                    var list = new List<string>(input.Length);
                    var en = System.Globalization.StringInfo.GetTextElementEnumerator(input);
                    while (en.MoveNext()) list.Add((string)en.Current);
                    symbols = list;
                    break;
            }

            long n = symbols.Count;
            if (n == 0)
            {
                r.Ok = false;
                r.Error = "__EMPTY__"; // sentinel; the page localizes this.
                return r;
            }

            var freq = new Dictionary<string, long>();
            foreach (var s in symbols)
            {
                freq.TryGetValue(s, out var c);
                freq[s] = c + 1;
            }

            int unique = freq.Count;
            double entropy = 0.0;
            foreach (var kv in freq)
            {
                double p = (double)kv.Value / n;
                if (p > 0) entropy -= p * Math.Log2(p);
            }

            int alphabet = mode == Interpretation.RawChars ? unique : 256;
            double maxEntropy = alphabet > 1 ? Math.Log2(alphabet) : 0.0;

            // Chi-square against a uniform distribution over the observed alphabet.
            double expected = (double)n / unique;
            double chi = 0.0;
            if (expected > 0)
            {
                foreach (var kv in freq)
                {
                    double diff = kv.Value - expected;
                    chi += diff * diff / expected;
                }
            }

            r.Ok = true;
            r.Count = n;
            r.Unique = unique;
            r.AlphabetSize = alphabet;
            r.Entropy = entropy;
            r.TotalInfo = entropy * n;
            r.MaxEntropy = maxEntropy;
            r.PercentOfMax = maxEntropy > 0 ? Math.Min(100.0, entropy / maxEntropy * 100.0) : 0.0;
            r.ChiSquare = chi;

            long maxCount = freq.Values.Max();
            r.Top = freq
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Take(Math.Max(1, topN))
                .Select(kv => new SymbolFreq
                {
                    Symbol = Display(kv.Key, mode),
                    Count = kv.Value,
                    Percent = (double)kv.Value / n * 100.0,
                    Bar = MakeBar(kv.Value, maxCount),
                })
                .ToList();

            return r;
        }
        catch (Exception ex)
        {
            r.Ok = false;
            r.Error = ex.Message;
            return r;
        }
    }

    /// <summary>Turn a stored symbol key into a human-readable label.</summary>
    private static string Display(string key, Interpretation mode)
    {
        if (mode == Interpretation.RawChars)
        {
            if (key.Length == 1)
            {
                char c = key[0];
                if (c == ' ') return "␠ (space)";
                if (c == '\t') return "\\t";
                if (c == '\n') return "\\n";
                if (c == '\r') return "\\r";
                if (char.IsControl(c)) return "U+" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
            }
            return key;
        }
        // byte modes: key is already a 2-char hex string
        return "0x" + key;
    }

    private static string MakeBar(long count, long max)
    {
        if (max <= 0) return "";
        int len = (int)Math.Round((double)count / max * 20.0);
        if (len < 1) len = 1;
        return new string('█', len);
    }

    /// <summary>Parse a hex string (spaces/commas/0x prefixes tolerated). Never throws.</summary>
    private static bool TryParseHex(string input, out byte[] bytes, out string error)
    {
        bytes = Array.Empty<byte>();
        error = "";
        var sb = new StringBuilder(input.Length);
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (char.IsWhiteSpace(c) || c == ',' || c == ':' || c == '-' || c == '_') continue;
            if (c == '0' && i + 1 < input.Length && (input[i + 1] == 'x' || input[i + 1] == 'X')) { i++; continue; }
            if (Uri.IsHexDigit(c)) { sb.Append(c); continue; }
            error = "__BADHEX__"; // sentinel localized by the page
            return false;
        }
        string hex = sb.ToString();
        if (hex.Length == 0) { error = "__EMPTY__"; return false; }
        if (hex.Length % 2 != 0) { error = "__ODDHEX__"; return false; }

        var outBytes = new byte[hex.Length / 2];
        for (int i = 0; i < outBytes.Length; i++)
        {
            outBytes[i] = (byte)((HexVal(hex[i * 2]) << 4) | HexVal(hex[i * 2 + 1]));
        }
        bytes = outBytes;
        return true;
    }

    private static int HexVal(char c)
        => c <= '9' ? c - '0' : (char.ToUpperInvariant(c) - 'A' + 10);
}
