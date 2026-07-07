using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// 文字遮蔽 / 個資遮罩 · Text redactor / PII masker. Pure-managed detection with
/// <see cref="Regex"/> patterns each bounded by a 1s match timeout. Best-effort only —
/// heuristic patterns are not a guarantee of complete PII removal. Never throws.
/// </summary>
public static class TextRedactService
{
    /// <summary>Categories that can be detected & masked.</summary>
    public enum Category { Email, Phone, CreditCard, Ipv4, LongDigits }

    /// <summary>How matched text is replaced.</summary>
    public enum MaskStyle { Asterisks, Redacted, KeepLast4 }

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    // Compiled once; each carries its own 1s match timeout so a pathological input can't hang.
    private static readonly Regex EmailRx = new(
        @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}",
        RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex PhoneRx = new(
        @"(?<!\d)(?:\+?\d{1,3}[\s.\-]?)?(?:\(\d{2,4}\)[\s.\-]?)?\d{2,4}(?:[\s.\-]\d{2,4}){1,4}(?!\d)",
        RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex CreditCardRx = new(
        @"(?<!\d)(?:\d[ \-]?){13,16}(?<!\-)(?<! )(?<=\d)",
        RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex Ipv4Rx = new(
        @"(?<!\d)(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)(?!\d)",
        RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex LongDigitsRx = new(
        @"(?<!\d)\d{7,}(?!\d)",
        RegexOptions.CultureInvariant, Timeout);

    /// <summary>Outcome of a redaction pass.</summary>
    public sealed class Result
    {
        public string Output { get; init; } = string.Empty;
        public Dictionary<Category, int> Counts { get; init; } = new();
        public bool TimedOut { get; init; }
        public bool Failed { get; init; }
        public int Total
        {
            get
            {
                int t = 0;
                foreach (var v in Counts.Values) t += v;
                return t;
            }
        }
    }

    /// <summary>
    /// Mask every enabled category in <paramref name="input"/>. Order matters: broad
    /// patterns (email, IPv4) run before digit-run patterns so their digits are already
    /// masked and won't be double-counted. Never throws — a regex timeout or any other
    /// failure returns a flagged <see cref="Result"/> with the best output produced so far.
    /// </summary>
    public static Result Redact(string? input, IReadOnlyCollection<Category> enabled, MaskStyle style)
    {
        var counts = new Dictionary<Category, int>();
        if (string.IsNullOrEmpty(input) || enabled == null || enabled.Count == 0)
            return new Result { Output = input ?? string.Empty, Counts = counts };

        string text = input;
        bool timedOut = false;
        bool failed = false;

        // Deterministic order; each guarded independently so one bad category can't lose the rest.
        foreach (var cat in new[] { Category.Email, Category.Ipv4, Category.CreditCard, Category.Phone, Category.LongDigits })
        {
            if (!Contains(enabled, cat)) continue;
            try
            {
                int n = 0;
                Regex rx = RegexFor(cat);
                text = rx.Replace(text, m => { n++; return Mask(m.Value, style); });
                if (n > 0) counts[cat] = n;
            }
            catch (RegexMatchTimeoutException)
            {
                timedOut = true;
            }
            catch
            {
                failed = true;
            }
        }

        return new Result { Output = text, Counts = counts, TimedOut = timedOut, Failed = failed };
    }

    private static bool Contains(IReadOnlyCollection<Category> set, Category c)
    {
        foreach (var v in set) if (v == c) return true;
        return false;
    }

    private static Regex RegexFor(Category cat) => cat switch
    {
        Category.Email => EmailRx,
        Category.Phone => PhoneRx,
        Category.CreditCard => CreditCardRx,
        Category.Ipv4 => Ipv4Rx,
        Category.LongDigits => LongDigitsRx,
        _ => LongDigitsRx,
    };

    private static string Mask(string value, MaskStyle style)
    {
        switch (style)
        {
            case MaskStyle.Redacted:
                return "[REDACTED]";
            case MaskStyle.KeepLast4:
                if (value.Length <= 4) return value;
                var sb = new System.Text.StringBuilder(value.Length);
                int keepFrom = value.Length - 4;
                for (int i = 0; i < value.Length; i++)
                {
                    char ch = value[i];
                    if (i >= keepFrom) sb.Append(ch);
                    else sb.Append(char.IsWhiteSpace(ch) ? ch : '*');
                }
                return sb.ToString();
            case MaskStyle.Asterisks:
            default:
                var sb2 = new System.Text.StringBuilder(value.Length);
                foreach (char ch in value)
                    sb2.Append(char.IsWhiteSpace(ch) ? ch : '*');
                return sb2.ToString();
        }
    }
}
