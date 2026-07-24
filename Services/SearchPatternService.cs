using System;
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
        bool ExplicitCapture = false);

    public sealed record MatchResult(bool Ok, bool IsMatch, string? Error = null);

    public static MatchResult Match(string? candidate, Spec? spec)
    {
        candidate ??= string.Empty;
        spec ??= new Spec(string.Empty);
        string query = spec.Query ?? string.Empty;

        if (query.Length > RegexTesterService.MaxPatternLength)
            return new MatchResult(false, false,
                $"Search pattern exceeds {RegexTesterService.MaxPatternLength:N0} characters.");
        if (candidate.Length > RegexTesterService.MaxInputLength)
            return new MatchResult(false, false,
                $"Search candidate exceeds {RegexTesterService.MaxInputLength:N0} characters.");
        if (query.Length == 0)
            return new MatchResult(true, true);

        if (!spec.UseRegex)
        {
            var comparison = spec.IgnoreCase
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return new MatchResult(true, candidate.Contains(query, comparison));
        }

        RegexOptions options = RegexOptions.CultureInvariant;
        if (spec.IgnoreCase) options |= RegexOptions.IgnoreCase;
        if (spec.Multiline) options |= RegexOptions.Multiline;
        if (spec.Singleline) options |= RegexOptions.Singleline;
        if (spec.ExplicitCapture) options |= RegexOptions.ExplicitCapture;

        try
        {
            return new MatchResult(true, Regex.IsMatch(candidate, query, options, Timeout));
        }
        catch (RegexParseException ex)
        {
            return new MatchResult(false, false, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return new MatchResult(false, false, ex.Message);
        }
        catch (RegexMatchTimeoutException)
        {
            return new MatchResult(false, false, "Search pattern timed out after 250 ms.");
        }
    }

    public static MatchResult Validate(Spec? spec) => Match(string.Empty, spec);
}
