using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 語意化版本範圍測試器 · Semantic Versioning (SemVer 2.0.0) parser + comparator + node-semver-style
/// range matcher, implemented by hand — pure managed, never throws. Supports exact / comparators
/// (&gt;= &gt; &lt;= &lt; =), caret (^), tilde (~), x-ranges (1.2.x, 1.*), hyphen ranges (a - b),
/// OR (||), AND (space), and prerelease precedence.
/// </summary>
public static class SemverRangeService
{
    // ---------- Version model ----------

    /// <summary>A parsed semantic version. Immutable.</summary>
    public sealed class SemVer : IComparable<SemVer>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        /// <summary>Dot-separated prerelease identifiers (empty = stable release).</summary>
        public IReadOnlyList<string> Prerelease { get; }
        /// <summary>Build metadata (ignored in precedence).</summary>
        public string Build { get; }
        public string Raw { get; }

        public bool IsPrerelease => Prerelease.Count > 0;

        public SemVer(int major, int minor, int patch, IReadOnlyList<string> prerelease, string build, string raw)
        {
            Major = major; Minor = minor; Patch = patch;
            Prerelease = prerelease; Build = build; Raw = raw;
        }

        public int CompareTo(SemVer? other)
        {
            if (other is null) return 1;
            int c = Major.CompareTo(other.Major); if (c != 0) return c;
            c = Minor.CompareTo(other.Minor); if (c != 0) return c;
            c = Patch.CompareTo(other.Patch); if (c != 0) return c;
            return ComparePrerelease(Prerelease, other.Prerelease);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Major).Append('.').Append(Minor).Append('.').Append(Patch);
            if (Prerelease.Count > 0) sb.Append('-').Append(string.Join('.', Prerelease));
            if (Build.Length > 0) sb.Append('+').Append(Build);
            return sb.ToString();
        }
    }

    /// <summary>Compare two prerelease identifier lists per SemVer §11.4. Empty (release) &gt; non-empty (prerelease).</summary>
    private static int ComparePrerelease(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 0;
        if (a.Count == 0) return 1;   // release outranks prerelease
        if (b.Count == 0) return -1;
        int n = Math.Min(a.Count, b.Count);
        for (int i = 0; i < n; i++)
        {
            string x = a[i], y = b[i];
            bool xn = IsNumericId(x), yn = IsNumericId(y);
            int c;
            if (xn && yn)
            {
                // Numeric identifiers compared numerically (use long to avoid overflow).
                long xv = ParseLongSafe(x), yv = ParseLongSafe(y);
                c = xv.CompareTo(yv);
            }
            else if (xn) c = -1;      // numeric has lower precedence than alphanumeric
            else if (yn) c = 1;
            else c = string.CompareOrdinal(x, y);
            if (c != 0) return c;
        }
        return a.Count.CompareTo(b.Count); // larger set of fields wins if all preceding equal
    }

    private static bool IsNumericId(string s)
    {
        if (s.Length == 0) return false;
        foreach (char ch in s) if (ch < '0' || ch > '9') return false;
        return true;
    }

    private static long ParseLongSafe(string s)
        => long.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : 0;

    // ---------- Version parsing ----------

    /// <summary>Try to parse a full semantic version. Leading 'v'/'=' and surrounding whitespace tolerated.</summary>
    public static bool TryParse(string? input, out SemVer version, out string error)
    {
        version = default!;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) { error = "Empty version"; return false; }
        string s = input.Trim();
        if (s.StartsWith('=')) s = s[1..].Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s[1..];
        if (s.Length == 0) { error = "Empty version"; return false; }

        string build = string.Empty;
        int plus = s.IndexOf('+');
        if (plus >= 0) { build = s[(plus + 1)..]; s = s[..plus]; }

        string preStr = string.Empty;
        int dash = s.IndexOf('-');
        if (dash >= 0) { preStr = s[(dash + 1)..]; s = s[..dash]; }

        var core = s.Split('.');
        if (core.Length != 3) { error = "Expected MAJOR.MINOR.PATCH"; return false; }
        if (!TryCorePart(core[0], out int major)) { error = "Invalid major"; return false; }
        if (!TryCorePart(core[1], out int minor)) { error = "Invalid minor"; return false; }
        if (!TryCorePart(core[2], out int patch)) { error = "Invalid patch"; return false; }

        var pre = new List<string>();
        if (preStr.Length > 0)
        {
            foreach (var id in preStr.Split('.'))
            {
                if (id.Length == 0) { error = "Empty prerelease identifier"; return false; }
                if (!IsValidPreId(id)) { error = "Invalid prerelease identifier"; return false; }
                if (IsNumericId(id) && id.Length > 1 && id[0] == '0') { error = "Leading zero in numeric prerelease"; return false; }
                pre.Add(id);
            }
        }
        if (build.Length > 0 && !IsValidBuild(build)) { error = "Invalid build metadata"; return false; }

        version = new SemVer(major, minor, patch, pre, build, input.Trim());
        return true;
    }

    private static bool TryCorePart(string s, out int value)
    {
        value = 0;
        if (s.Length == 0) return false;
        if (s.Length > 1 && s[0] == '0') return false; // no leading zeros
        foreach (char ch in s) if (ch < '0' || ch > '9') return false;
        return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsValidPreId(string s)
    {
        foreach (char ch in s)
            if (!(char.IsLetterOrDigit(ch) && ch < 128) && ch != '-') return false;
        return true;
    }

    private static bool IsValidBuild(string s)
    {
        foreach (var id in s.Split('.'))
        {
            if (id.Length == 0) return false;
            foreach (char ch in id)
                if (!(char.IsLetterOrDigit(ch) && ch < 128) && ch != '-') return false;
        }
        return true;
    }

    // ---------- Range model ----------

    internal enum Op { Eq, Lt, Lte, Gt, Gte }

    internal readonly struct Comparator
    {
        public readonly Op Operator;
        public readonly SemVer Version;
        public Comparator(Op op, SemVer v) { Operator = op; Version = v; }

        public bool Matches(SemVer v)
        {
            int c = v.CompareTo(Version);
            return Operator switch
            {
                Op.Eq => c == 0,
                Op.Lt => c < 0,
                Op.Lte => c <= 0,
                Op.Gt => c > 0,
                Op.Gte => c >= 0,
                _ => false,
            };
        }

        public override string ToString()
        {
            string s = Operator switch
            {
                Op.Eq => "=", Op.Lt => "<", Op.Lte => "<=", Op.Gt => ">", Op.Gte => ">=", _ => "?"
            };
            return s + Version;
        }
    }

    /// <summary>A parsed range: a disjunction (||) of conjunction sets (space-joined comparators).</summary>
    public sealed class Range
    {
        private readonly List<List<Comparator>> _orSets; // OR of ( AND of comparators )
        public string Normalized { get; }
        public string Raw { get; }

        internal Range(List<List<Comparator>> orSets, string raw)
        {
            _orSets = orSets;
            Raw = raw;
            Normalized = string.Join(" || ",
                orSets.Select(set => set.Count == 0 ? "*" : string.Join(" ", set.Select(c => c.ToString()))));
        }

        /// <summary>Does <paramref name="v"/> satisfy this range? By node-semver convention a prerelease only
        /// matches a comparator set if that set contains a comparator on the same [major,minor,patch] tuple.</summary>
        public bool Satisfies(SemVer v)
        {
            foreach (var set in _orSets)
                if (SetMatches(set, v)) return true;
            return false;
        }

        private static bool SetMatches(List<Comparator> set, SemVer v)
        {
            if (set.Count == 0) return true; // wildcard "*" (any release)... but block prereleases w/o allowance below
            foreach (var c in set)
                if (!c.Matches(v)) return false;

            if (v.IsPrerelease)
            {
                // Only allow the prerelease if some comparator was written against the same core tuple.
                foreach (var c in set)
                {
                    var cv = c.Version;
                    if (cv.IsPrerelease && cv.Major == v.Major && cv.Minor == v.Minor && cv.Patch == v.Patch)
                        return true;
                }
                return false;
            }
            return true;
        }
    }

    /// <summary>Parse a node-semver range expression. Never throws.</summary>
    public static bool TryParseRange(string? input, out Range range, out string error)
    {
        range = default!;
        error = string.Empty;
        if (input is null) { error = "Empty range"; return false; }
        string trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            // Empty range means "any version" in node-semver.
            range = new Range(new List<List<Comparator>> { new() }, input);
            return true;
        }

        var orSets = new List<List<Comparator>>();
        foreach (var orPart in trimmed.Split("||"))
        {
            if (!TryParseComparatorSet(orPart.Trim(), out var set, out error))
                return false;
            orSets.Add(set);
        }
        range = new Range(orSets, input);
        return true;
    }

    private static bool TryParseComparatorSet(string part, out List<Comparator> comparators, out string error)
    {
        comparators = new List<Comparator>();
        error = string.Empty;
        if (part.Length == 0) { comparators.Add(default); return true; } // wildcard handled by Range but keep [] semantics

        // Hyphen range:  A - B  (spaces required around the dash per spec).
        int hy = FindHyphen(part);
        if (hy >= 0)
        {
            string lo = part[..hy].Trim();
            string hi = part[(hy + 1)..].Trim();
            if (!TryHyphenBound(lo, isLower: true, comparators, out error)) return false;
            if (!TryHyphenBound(hi, isLower: false, comparators, out error)) return false;
            return true;
        }

        var tokens = part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        foreach (var tok in tokens)
        {
            if (!TryParseComparator(tok, comparators, out error)) return false;
        }
        if (comparators.Count == 0) comparators.Add(default); // becomes "*"
        return true;
    }

    /// <summary>Locate a " - " hyphen (space-dash-space), not a prerelease dash.</summary>
    private static int FindHyphen(string s)
    {
        for (int i = 1; i < s.Length - 1; i++)
            if (s[i] == '-' && char.IsWhiteSpace(s[i - 1]) && char.IsWhiteSpace(s[i + 1]))
                return i;
        return -1;
    }

    private static bool TryHyphenBound(string token, bool isLower, List<Comparator> outList, out string error)
    {
        error = string.Empty;
        var (wildMajor, wildMinor, wildPatch, core, err) = ParsePartial(token);
        if (err != null) { error = err; return false; }
        if (isLower)
        {
            // Lower bound: >= with wildcards filled by zero.
            var v = MakeVersion(core.Major, wildMinor ? 0 : core.Minor, (wildMinor || wildPatch) ? 0 : core.Patch, core.Prerelease, core.Build, token);
            outList.Add(new Comparator(Op.Gte, v));
        }
        else
        {
            if (wildMajor) { /* x on upper bound => no upper limit */ return true; }
            if (wildMinor)
            {
                // "1" as upper => < 2.0.0
                outList.Add(new Comparator(Op.Lt, MakeVersion(core.Major + 1, 0, 0, Array.Empty<string>(), string.Empty, token)));
            }
            else if (wildPatch)
            {
                // "1.2" as upper => < 1.3.0
                outList.Add(new Comparator(Op.Lt, MakeVersion(core.Major, core.Minor + 1, 0, Array.Empty<string>(), string.Empty, token)));
            }
            else
            {
                // exact upper => <= it
                outList.Add(new Comparator(Op.Lte, core));
            }
        }
        return true;
    }

    private static bool TryParseComparator(string token, List<Comparator> outList, out string error)
    {
        error = string.Empty;
        string t = token.Trim();
        if (t.Length == 0) return true;

        // Operator prefix.
        Op? explicitOp = null;
        if (t.StartsWith(">=")) { explicitOp = Op.Gte; t = t[2..]; }
        else if (t.StartsWith("<=")) { explicitOp = Op.Lte; t = t[2..]; }
        else if (t.StartsWith('>')) { explicitOp = Op.Gt; t = t[1..]; }
        else if (t.StartsWith('<')) { explicitOp = Op.Lt; t = t[1..]; }
        else if (t.StartsWith('=')) { explicitOp = Op.Eq; t = t[1..]; }
        t = t.Trim();

        if (t.Length == 0) { error = "Missing version after operator"; return false; }

        // Caret / tilde.
        if (t.StartsWith('^')) return ExpandCaret(t[1..], outList, out error);
        if (t.StartsWith('~')) return ExpandTilde(t[1..], outList, out error);

        // Plain / partial / x-range version, possibly with an explicit operator.
        var (wildMajor, wildMinor, wildPatch, core, err) = ParsePartial(t);
        if (err != null) { error = err; return false; }

        if (explicitOp is Op op)
        {
            // With an explicit operator, wildcards resolve to concrete zeros for that bound.
            var v = MakeVersion(core.Major, wildMinor ? 0 : core.Minor, (wildMinor || wildPatch) ? 0 : core.Patch, core.Prerelease, core.Build, t);
            outList.Add(new Comparator(op, v));
            return true;
        }

        // No operator → x-range semantics.
        if (wildMajor) { outList.Add(default); return true; } // "*" → any (empty comparator)
        if (wildMinor)
        {
            outList.Add(new Comparator(Op.Gte, MakeVersion(core.Major, 0, 0, Array.Empty<string>(), string.Empty, t)));
            outList.Add(new Comparator(Op.Lt, MakeVersion(core.Major + 1, 0, 0, Array.Empty<string>(), string.Empty, t)));
            return true;
        }
        if (wildPatch)
        {
            outList.Add(new Comparator(Op.Gte, MakeVersion(core.Major, core.Minor, 0, Array.Empty<string>(), string.Empty, t)));
            outList.Add(new Comparator(Op.Lt, MakeVersion(core.Major, core.Minor + 1, 0, Array.Empty<string>(), string.Empty, t)));
            return true;
        }
        // Fully-specified with no operator → exact equality.
        outList.Add(new Comparator(Op.Eq, core));
        return true;
    }

    private static bool ExpandCaret(string t, List<Comparator> outList, out string error)
    {
        error = string.Empty;
        var (wildMajor, wildMinor, wildPatch, core, err) = ParsePartial(t);
        if (err != null) { error = err; return false; }
        if (wildMajor) { outList.Add(default); return true; } // ^* → any

        outList.Add(new Comparator(Op.Gte, MakeVersion(core.Major, wildMinor ? 0 : core.Minor, (wildMinor || wildPatch) ? 0 : core.Patch, core.Prerelease, core.Build, t)));

        // Upper bound: keep left-most non-zero fixed.
        SemVer upper;
        if (core.Major > 0 || wildMinor)
            upper = MakeVersion(core.Major + 1, 0, 0, Array.Empty<string>(), string.Empty, t);
        else if (core.Minor > 0 || wildPatch)
            upper = MakeVersion(0, core.Minor + 1, 0, Array.Empty<string>(), string.Empty, t);
        else
            upper = MakeVersion(0, 0, core.Patch + 1, Array.Empty<string>(), string.Empty, t);
        outList.Add(new Comparator(Op.Lt, upper));
        return true;
    }

    private static bool ExpandTilde(string t, List<Comparator> outList, out string error)
    {
        error = string.Empty;
        var (wildMajor, wildMinor, wildPatch, core, err) = ParsePartial(t);
        if (err != null) { error = err; return false; }
        if (wildMajor) { outList.Add(default); return true; }

        outList.Add(new Comparator(Op.Gte, MakeVersion(core.Major, wildMinor ? 0 : core.Minor, (wildMinor || wildPatch) ? 0 : core.Patch, core.Prerelease, core.Build, t)));

        SemVer upper;
        if (wildMinor)
            // ~1 → >=1.0.0 <2.0.0
            upper = MakeVersion(core.Major + 1, 0, 0, Array.Empty<string>(), string.Empty, t);
        else
            // ~1.2 or ~1.2.3 → >= ... < 1.(minor+1).0
            upper = MakeVersion(core.Major, core.Minor + 1, 0, Array.Empty<string>(), string.Empty, t);
        outList.Add(new Comparator(Op.Lt, upper));
        return true;
    }

    // ---------- Partial / x-range parsing ----------

    /// <summary>Parse a possibly-partial version (1, 1.2, 1.2.3, 1.x, 1.*, 1.2.x). Returns wildcard flags,
    /// a filled SemVer (wildcards → 0), and an error string (null = ok).</summary>
    private static (bool wildMajor, bool wildMinor, bool wildPatch, SemVer core, string? error) ParsePartial(string token)
    {
        string s = token.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V')) s = s[1..];
        if (s.Length == 0) return (false, false, false, default!, "Empty version");

        string build = string.Empty;
        int plus = s.IndexOf('+');
        if (plus >= 0) { build = s[(plus + 1)..]; s = s[..plus]; }

        string preStr = string.Empty;
        int dash = s.IndexOf('-');
        if (dash >= 0) { preStr = s[(dash + 1)..]; s = s[..dash]; }

        var parts = s.Split('.');
        if (parts.Length == 0 || parts.Length > 3) return (false, false, false, default!, "Bad version shape");

        bool wildMajor = parts.Length < 1 || IsWild(parts[0]);
        int major = 0, minor = 0, patch = 0;
        bool wildMinor, wildPatch;

        if (wildMajor)
        {
            return (true, true, true, MakeVersion(0, 0, 0, Array.Empty<string>(), string.Empty, token), null);
        }
        if (!TryNum(parts[0], out major)) return (false, false, false, default!, "Invalid major");

        if (parts.Length >= 2 && !IsWild(parts[1]))
        {
            if (!TryNum(parts[1], out minor)) return (false, false, false, default!, "Invalid minor");
            wildMinor = false;
        }
        else wildMinor = true;

        if (!wildMinor && parts.Length >= 3 && !IsWild(parts[2]))
        {
            if (!TryNum(parts[2], out patch)) return (false, false, false, default!, "Invalid patch");
            wildPatch = false;
        }
        else wildPatch = true;

        var pre = new List<string>();
        if (preStr.Length > 0 && !wildMinor && !wildPatch)
        {
            foreach (var id in preStr.Split('.'))
            {
                if (id.Length == 0) return (false, false, false, default!, "Empty prerelease id");
                if (!IsValidPreId(id)) return (false, false, false, default!, "Invalid prerelease id");
                pre.Add(id);
            }
        }

        var core = MakeVersion(major, minor, patch, pre, build, token);
        return (false, wildMinor, wildPatch, core, null);
    }

    private static bool IsWild(string s)
        => s == "x" || s == "X" || s == "*";

    private static bool TryNum(string s, out int value)
    {
        value = 0;
        if (s.Length == 0) return false;
        foreach (char ch in s) if (ch < '0' || ch > '9') return false;
        return int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private static SemVer MakeVersion(int major, int minor, int patch, IReadOnlyList<string> pre, string build, string raw)
        => new(Math.Max(0, major), Math.Max(0, minor), Math.Max(0, patch), pre, build ?? string.Empty, raw);

    // ---------- Public helpers ----------

    /// <summary>Result of testing one version line against a range.</summary>
    public sealed class MatchResult
    {
        public string Input { get; set; } = string.Empty;
        public bool Valid { get; set; }
        public bool Satisfies { get; set; }
        public string Reason { get; set; } = string.Empty;
        public SemVer? Version { get; set; }
    }

    /// <summary>Parse and precedence-sort a set of version lines (ascending). Invalid lines are dropped.</summary>
    public static List<SemVer> SortVersions(IEnumerable<string> lines, bool descending = false)
    {
        var list = new List<SemVer>();
        foreach (var line in lines)
        {
            if (TryParse(line, out var v, out _)) list.Add(v);
        }
        list.Sort();
        if (descending) list.Reverse();
        return list;
    }
}
