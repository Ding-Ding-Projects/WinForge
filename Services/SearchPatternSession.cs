using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// Synchronized state for a search surface and its progressive regex builder. The raw query/pattern, mode,
/// flags and validation result have one owner so the visible builder and the real search cannot drift apart.
/// Nothing is persisted or transmitted.
/// </summary>
public sealed class SearchPatternSession
{
    private string _query = string.Empty;
    private bool _useRegex;
    private bool _ignoreCase = true;
    private bool _multiline;
    private bool _singleline;
    private bool _ignorePatternWhitespace;
    private bool _explicitCapture;

    public event EventHandler? Changed;

    public string Query
    {
        get => _query;
        set => Set(ref _query, value ?? string.Empty);
    }

    public bool UseRegex
    {
        get => _useRegex;
        set => Set(ref _useRegex, value);
    }

    public bool IgnoreCase
    {
        get => _ignoreCase;
        set => Set(ref _ignoreCase, value);
    }

    public bool Multiline
    {
        get => _multiline;
        set => Set(ref _multiline, value);
    }

    public bool Singleline
    {
        get => _singleline;
        set => Set(ref _singleline, value);
    }

    public bool IgnorePatternWhitespace
    {
        get => _ignorePatternWhitespace;
        set => Set(ref _ignorePatternWhitespace, value);
    }

    public bool ExplicitCapture
    {
        get => _explicitCapture;
        set => Set(ref _explicitCapture, value);
    }

    public SearchPatternService.Spec Spec => new(
        Query,
        UseRegex,
        IgnoreCase,
        Multiline,
        Singleline,
        IgnorePatternWhitespace,
        ExplicitCapture);

    public SearchPatternService.Matcher Compile() => SearchPatternService.Compile(Spec);

    public SearchPatternService.MatchResult Match(string? candidate) => Compile().Match(candidate);

    public SearchPatternService.MatchResult MatchAny(IEnumerable<string?> candidates)
        => Compile().MatchAny(candidates);

    public RegexTesterService.EvalResult Preview(string? sample)
        => RegexTesterService.Evaluate(Query, sample, string.Empty, SearchPatternService.BuildOptions(Spec));

    public void Apply(SearchPatternService.Spec? spec)
    {
        spec ??= new SearchPatternService.Spec(string.Empty);
        bool changed = _query != spec.Query
            || _useRegex != spec.UseRegex
            || _ignoreCase != spec.IgnoreCase
            || _multiline != spec.Multiline
            || _singleline != spec.Singleline
            || _ignorePatternWhitespace != spec.IgnorePatternWhitespace
            || _explicitCapture != spec.ExplicitCapture;

        _query = spec.Query ?? string.Empty;
        _useRegex = spec.UseRegex;
        _ignoreCase = spec.IgnoreCase;
        _multiline = spec.Multiline;
        _singleline = spec.Singleline;
        _ignorePatternWhitespace = spec.IgnorePatternWhitespace;
        _explicitCapture = spec.ExplicitCapture;
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Set<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
