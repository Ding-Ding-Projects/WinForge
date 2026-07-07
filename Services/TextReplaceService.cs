using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// 多規則尋找及取代 · Multi-rule find &amp; replace engine. Pure managed; applies each rule in order,
/// either literal or Regex.Replace with a 1-second match timeout. Never throws — invalid regex or a
/// timeout is reported per rule via a bilingual error string. No I/O, no side-effects.
/// </summary>
public static class TextReplaceService
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);

    /// <summary>A single replace rule.</summary>
    public sealed class Rule
    {
        public string Find { get; set; } = "";
        public string Replace { get; set; } = "";
        public bool Regex { get; set; }
        public bool IgnoreCase { get; set; }

        /// <summary>Non-null when the last apply produced an error for this rule (bilingual).</summary>
        public string? ErrorEn { get; set; }
        public string? ErrorZh { get; set; }
        /// <summary>Replacements this rule contributed on the last apply.</summary>
        public int Hits { get; set; }
    }

    /// <summary>Outcome of applying all rules to some input text.</summary>
    public sealed class Result
    {
        public string Output { get; set; } = "";
        public int TotalReplacements { get; set; }
        public bool AnyError { get; set; }
    }

    /// <summary>
    /// Apply <paramref name="rules"/> to <paramref name="input"/> in order. Each rule's Find/Replace,
    /// Regex &amp; IgnoreCase flags decide the transform. Populates per-rule Hits/Error fields.
    /// Never throws.
    /// </summary>
    public static Result Apply(string? input, IReadOnlyList<Rule> rules)
    {
        var result = new Result();
        string text = input ?? "";

        if (rules is null)
        {
            result.Output = text;
            return result;
        }

        foreach (var rule in rules)
        {
            if (rule is null) continue;

            rule.ErrorEn = null;
            rule.ErrorZh = null;
            rule.Hits = 0;

            if (string.IsNullOrEmpty(rule.Find)) continue;

            try
            {
                if (rule.Regex)
                {
                    var options = RegexOptions.None;
                    if (rule.IgnoreCase) options |= RegexOptions.IgnoreCase;

                    var regex = new Regex(rule.Find, options, MatchTimeout);
                    int count = 0;
                    string replaced = regex.Replace(text, m =>
                    {
                        count++;
                        // Expand $1, ${name}, $$ etc. via the match's own expansion.
                        return m.Result(rule.Replace ?? "");
                    });
                    rule.Hits = count;
                    result.TotalReplacements += count;
                    text = replaced;
                }
                else
                {
                    int count = CountLiteral(text, rule.Find, rule.IgnoreCase);
                    if (count > 0)
                    {
                        text = ReplaceLiteral(text, rule.Find, rule.Replace ?? "", rule.IgnoreCase);
                        rule.Hits = count;
                        result.TotalReplacements += count;
                    }
                }
            }
            catch (RegexParseException ex)
            {
                result.AnyError = true;
                rule.ErrorEn = "Invalid regex: " + ex.Message;
                rule.ErrorZh = "正規表達式錯誤：" + ex.Message;
            }
            catch (RegexMatchTimeoutException)
            {
                result.AnyError = true;
                rule.ErrorEn = "Regex timed out (over 1s) — simplify the pattern.";
                rule.ErrorZh = "正規表達式超時（超過 1 秒）— 請簡化個式。";
            }
            catch (ArgumentException ex)
            {
                // e.g. a bad replacement reference like $9 that has no group.
                result.AnyError = true;
                rule.ErrorEn = "Rule error: " + ex.Message;
                rule.ErrorZh = "規則錯誤：" + ex.Message;
            }
            catch (Exception ex)
            {
                result.AnyError = true;
                rule.ErrorEn = "Rule error: " + ex.Message;
                rule.ErrorZh = "規則錯誤：" + ex.Message;
            }
        }

        result.Output = text;
        return result;
    }

    private static int CountLiteral(string haystack, string needle, bool ignoreCase)
    {
        if (string.IsNullOrEmpty(needle)) return 0;
        var cmp = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, cmp)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    private static string ReplaceLiteral(string haystack, string needle, string replacement, bool ignoreCase)
    {
        if (string.IsNullOrEmpty(needle)) return haystack;
        if (!ignoreCase) return haystack.Replace(needle, replacement);

        // Ordinal, case-insensitive literal replace.
        var sb = new System.Text.StringBuilder();
        int idx = 0;
        while (true)
        {
            int found = haystack.IndexOf(needle, idx, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                sb.Append(haystack, idx, haystack.Length - idx);
                break;
            }
            sb.Append(haystack, idx, found - idx);
            sb.Append(replacement);
            idx = found + needle.Length;
        }
        return sb.ToString();
    }
}
