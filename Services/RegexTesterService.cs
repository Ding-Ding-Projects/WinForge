using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// 正則表達式測試器 · Live .NET regex tester — pure managed <see cref="System.Text.RegularExpressions"/>.
/// Compiles patterns with a 1-second match-timeout so a runaway pattern can never freeze the UI, and
/// surfaces parse/timeout failures as friendly bilingual messages instead of crashing.
/// </summary>
public static class RegexTesterService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    // Keep every evaluation local and bounded. These limits are deliberately public so the UI,
    // search integrations, and focused tests all enforce the exact same contract.
    public const int MaxPatternLength = 4_096;
    public const int MaxInputLength = 1_000_000;
    public const int MaxReplacementLength = 65_536;
    public const int MaxMatches = 2_000;
    public const int MaxReplacementWork = 8_000_000;

    /// <summary>A single captured group within a match (named groups keep their name).</summary>
    public sealed record GroupHit(string Name, int Index, int Length, string Value);

    /// <summary>A single regex match plus its captured groups.</summary>
    public sealed record MatchHit(int Number, int Index, int Length, string Value, IReadOnlyList<GroupHit> Groups);

    /// <summary>Outcome of an evaluation — either matches, or an error message to show in red.</summary>
    public sealed record EvalResult(
        bool Ok,
        string? Error,
        IReadOnlyList<MatchHit> Matches,
        string Replacement,
        bool MatchesTruncated = false);

    /// <summary>Build the <see cref="RegexOptions"/> from the individual toggles.</summary>
    public static RegexOptions BuildOptions(bool ignoreCase, bool multiline, bool singleline,
        bool ignorePatternWhitespace, bool explicitCapture)
    {
        var options = RegexOptions.None;
        if (ignoreCase) options |= RegexOptions.IgnoreCase;
        if (multiline) options |= RegexOptions.Multiline;
        if (singleline) options |= RegexOptions.Singleline;
        if (ignorePatternWhitespace) options |= RegexOptions.IgnorePatternWhitespace;
        if (explicitCapture) options |= RegexOptions.ExplicitCapture;
        return options;
    }

    /// <summary>
    /// Run the pattern against <paramref name="input"/> and (optionally) compute a replacement. Never throws:
    /// a bad pattern or a timeout comes back as <see cref="EvalResult.Ok"/> == false with a message.
    /// </summary>
    public static EvalResult Evaluate(string? pattern, string? input, string? replacement, RegexOptions options)
    {
        pattern ??= string.Empty;
        input ??= string.Empty;
        replacement ??= string.Empty;

        if (pattern.Length > MaxPatternLength)
            return Fail($"Pattern is longer than the {MaxPatternLength:N0}-character safety limit.",
                $"表達式超過 {MaxPatternLength:N0} 個字元安全上限。");
        if (input.Length > MaxInputLength)
            return Fail($"Test input is longer than the {MaxInputLength:N0}-character safety limit.",
                $"測試文字超過 {MaxInputLength:N0} 個字元安全上限。");
        if (replacement.Length > MaxReplacementLength)
            return Fail($"Replacement is longer than the {MaxReplacementLength:N0}-character safety limit.",
                $"替換文字超過 {MaxReplacementLength:N0} 個字元安全上限。");

        if (pattern.Length == 0)
            return new EvalResult(true, null, Array.Empty<MatchHit>(), string.Empty);

        Regex regex;
        try
        {
            regex = new Regex(pattern, options, Timeout);
        }
        catch (RegexParseException ex)
        {
            return Fail($"Invalid pattern: {ex.Message}", $"表達式錯誤：{ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return Fail($"Invalid pattern: {ex.Message}", $"表達式錯誤：{ex.Message}");
        }

        var hits = new List<MatchHit>();
        bool matchesTruncated = false;
        try
        {
            int number = 0;
            Match m = regex.Match(input);
            while (m.Success)
            {
                if (hits.Count >= MaxMatches)
                {
                    matchesTruncated = true;
                    break;
                }

                var groups = new List<GroupHit>();
                // Skip group[0] (the whole match) — that's already the match value.
                for (int gi = 1; gi < m.Groups.Count; gi++)
                {
                    var g = m.Groups[gi];
                    if (!g.Success) continue;
                    string name = regex.GroupNameFromNumber(gi);
                    groups.Add(new GroupHit(name, g.Index, g.Length, g.Value));
                }
                hits.Add(new MatchHit(++number, m.Index, m.Length, m.Value, groups));

                // Match.NextMatch() contains the runtime's zero-width advancement rule, avoiding the
                // common infinite-loop bug while preserving valid zero-width matches.
                m = m.NextMatch();
            }
        }
        catch (RegexMatchTimeoutException)
        {
            return Fail("Pattern timed out (over 1s) — it may be catastrophically backtracking.",
                "表達式超時（超過 1 秒）— 可能發生災難性回溯。");
        }

        string replaced = string.Empty;
        try
        {
            long conservativeWork = (long)Math.Max(1, input.Length) * Math.Max(1, replacement.Length + 1);
            if (conservativeWork > MaxReplacementWork)
                return Fail("Replacement preview exceeds the bounded-work safety limit. Shorten the input or replacement.",
                    "替換預覽超過有限工作量安全上限；請縮短輸入或者替換文字。");

            replaced = regex.Replace(input, replacement);
        }
        catch (RegexMatchTimeoutException)
        {
            return Fail("Replace timed out (over 1s).", "替換超時（超過 1 秒）。");
        }
        catch (ArgumentException)
        {
            // Bad substitution token; keep matches but leave the replacement empty.
            replaced = string.Empty;
        }

        return new EvalResult(true, null, hits, replaced, matchesTruncated);

        static EvalResult Fail(string en, string zh) =>
            new(false, Loc.I.Pick(en, zh), Array.Empty<MatchHit>(), string.Empty);
    }
}
