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

    /// <summary>A single captured group within a match (named groups keep their name).</summary>
    public sealed record GroupHit(string Name, int Index, int Length, string Value);

    /// <summary>A single regex match plus its captured groups.</summary>
    public sealed record MatchHit(int Number, int Index, int Length, string Value, IReadOnlyList<GroupHit> Groups);

    /// <summary>Outcome of an evaluation — either matches, or an error message to show in red.</summary>
    public sealed record EvalResult(bool Ok, string? Error, IReadOnlyList<MatchHit> Matches, string Replacement);

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
        try
        {
            int number = 0;
            foreach (Match m in regex.Matches(input))
            {
                if (!m.Success) continue;
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

        return new EvalResult(true, null, hits, replaced);

        static EvalResult Fail(string en, string zh) =>
            new(false, Loc.I.Pick(en, zh), Array.Empty<MatchHit>(), string.Empty);
    }
}
