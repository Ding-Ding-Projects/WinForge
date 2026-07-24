using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// One bounded plain-text/regex matching contract for WinForge search surfaces. Plain text remains the
/// default; a surface opts into the .NET regex dialect deliberately and can surface validation failures.
/// </summary>
public static class SearchPatternService
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

    public sealed record Spec(
        string Query,
        bool UseRegex = false,
        bool IgnoreCase = true,
        bool Multiline = false,
        bool Singleline = false,
        bool IgnorePatternWhitespace = false,
        bool ExplicitCapture = false);

    public sealed record MatchResult(bool Ok, bool IsMatch, string? Error = null);

    /// <summary>
    /// A validated, reusable matcher for one search refresh. Regex construction happens once rather than once
    /// per candidate, while every individual match still uses the bounded runtime timeout.
    /// </summary>
    public sealed class Matcher
    {
        private readonly string _query;
        private readonly StringComparison _comparison;
        private readonly Regex? _regex;
        private readonly string? _compileError;
        private string? _runtimeError;

        internal Matcher(Spec spec)
        {
            Spec = spec;
            _query = spec.UseRegex ? spec.Query ?? string.Empty : (spec.Query ?? string.Empty).Trim();
            _comparison = spec.IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            if (_query.Length > RegexTesterService.MaxPatternLength)
            {
                _compileError = $"Search pattern exceeds {RegexTesterService.MaxPatternLength:N0} characters.";
                return;
            }

            if (!spec.UseRegex || _query.Length == 0) return;

            try
            {
                _regex = new Regex(_query, BuildOptions(spec), Timeout);
            }
            catch (RegexParseException ex)
            {
                _compileError = ex.Message;
            }
            catch (ArgumentException ex)
            {
                _compileError = ex.Message;
            }
        }

        public Spec Spec { get; }
        public string? Error => _compileError ?? _runtimeError;
        public bool Ok => Error is null;

        public MatchResult Match(string? candidate)
        {
            if (Error is not null) return new MatchResult(false, false, Error);
            candidate ??= string.Empty;
            if (candidate.Length > RegexTesterService.MaxInputLength)
                return new MatchResult(false, false,
                    $"Search candidate exceeds {RegexTesterService.MaxInputLength:N0} characters.");
            if (_query.Length == 0) return new MatchResult(true, true);

            try
            {
                bool matched = Spec.UseRegex
                    ? _regex!.IsMatch(candidate)
                    : candidate.Contains(_query, _comparison);
                return new MatchResult(true, matched);
            }
            catch (RegexMatchTimeoutException)
            {
                _runtimeError = "Search pattern timed out after 250 ms.";
                return new MatchResult(false, false, _runtimeError);
            }
        }

        public MatchResult MatchAny(IEnumerable<string?> candidates)
        {
            if (Error is not null) return new MatchResult(false, false, Error);
            foreach (string? candidate in candidates)
            {
                MatchResult result = Match(candidate);
                if (!result.Ok || result.IsMatch) return result;
            }
            return new MatchResult(true, false);
        }
    }

    public static Matcher Compile(Spec? spec)
        => new(spec ?? new Spec(string.Empty));

    public static MatchResult Match(string? candidate, Spec? spec)
        => Compile(spec).Match(candidate);

    public static MatchResult MatchAny(IEnumerable<string?> candidates, Spec? spec)
        => Compile(spec).MatchAny(candidates);

    public static IEnumerable<T> Filter<T>(IEnumerable<T> source, Func<T, string?> candidate, Spec? spec)
    {
        Matcher matcher = Compile(spec);
        if (!matcher.Ok) yield break;
        foreach (T item in source)
        {
            MatchResult result = matcher.Match(candidate(item));
            if (!result.Ok) yield break;
            if (result.IsMatch) yield return item;
        }
    }

    public static MatchResult Validate(Spec? spec)
    {
        Matcher matcher = Compile(spec);
        return matcher.Ok
            ? new MatchResult(true, false)
            : new MatchResult(false, false, matcher.Error);
    }

    public static RegexOptions BuildOptions(Spec spec)
    {
        RegexOptions options = RegexOptions.CultureInvariant;
        if (spec.IgnoreCase) options |= RegexOptions.IgnoreCase;
        if (spec.Multiline) options |= RegexOptions.Multiline;
        if (spec.Singleline) options |= RegexOptions.Singleline;
        if (spec.IgnorePatternWhitespace) options |= RegexOptions.IgnorePatternWhitespace;
        if (spec.ExplicitCapture) options |= RegexOptions.ExplicitCapture;
        return options;
    }
}
