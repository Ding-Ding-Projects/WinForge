using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 變數代入（envsubst 式）· Variable substitution engine. Pure managed, never throws.
/// Expands <c>$VAR</c> and <c>${VAR}</c> placeholders — including <c>${VAR:-default}</c>
/// (default when unset/empty) and <c>${VAR:?}</c> (report missing) — from a supplied
/// dictionary, with optional process-environment fallback and <c>$$ → $</c> escaping.
/// </summary>
public static class EnvSubstService
{
    /// <summary>Outcome of a substitution run. Everything is best-effort; no exceptions escape.</summary>
    public sealed class Result
    {
        public string Output { get; set; } = string.Empty;
        /// <summary>Names that had no value anywhere (and no default supplied).</summary>
        public List<string> Unresolved { get; } = new();
        /// <summary>Names flagged with <c>${VAR:?}</c> that were unset/empty.</summary>
        public List<string> Missing { get; } = new();
        /// <summary>Every distinct variable name referenced by the template.</summary>
        public List<string> Referenced { get; } = new();
    }

    private static bool IsNameStart(char c) => c == '_' || char.IsLetter(c);
    private static bool IsNamePart(char c) => c == '_' || char.IsLetterOrDigit(c);

    /// <summary>Scan a template and return every distinct variable name it references, in order.</summary>
    public static List<string> DetectNames(string? template)
    {
        var seen = new List<string>();
        var set = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            string s = template ?? string.Empty;
            int i = 0, n = s.Length;
            while (i < n)
            {
                char c = s[i];
                if (c == '$' && i + 1 < n)
                {
                    char next = s[i + 1];
                    if (next == '$') { i += 2; continue; } // $$ escape — not a variable
                    if (next == '{')
                    {
                        int j = i + 2, start = j;
                        while (j < n && IsNamePart(s[j])) j++;
                        string name = s.Substring(start, j - start);
                        if (name.Length > 0 && set.Add(name)) seen.Add(name);
                        // skip to closing brace (or end)
                        int close = s.IndexOf('}', j);
                        i = close < 0 ? n : close + 1;
                        continue;
                    }
                    if (IsNameStart(next))
                    {
                        int j = i + 1, start = j;
                        while (j < n && IsNamePart(s[j])) j++;
                        string name = s.Substring(start, j - start);
                        if (name.Length > 0 && set.Add(name)) seen.Add(name);
                        i = j;
                        continue;
                    }
                }
                i++;
            }
        }
        catch { /* never throw */ }
        return seen;
    }

    /// <summary>
    /// Expand <paramref name="template"/> using <paramref name="values"/>. Robust — on any internal
    /// error it returns whatever was built so far.
    /// </summary>
    public static Result Substitute(
        string? template,
        IDictionary<string, string>? values,
        bool useEnvironmentFallback = false,
        bool escapeDoubleDollar = true)
    {
        var res = new Result();
        var refset = new HashSet<string>(StringComparer.Ordinal);
        var unresolvedSet = new HashSet<string>(StringComparer.Ordinal);
        var missingSet = new HashSet<string>(StringComparer.Ordinal);
        var sb = new StringBuilder();

        try
        {
            string s = template ?? string.Empty;
            var map = values ?? new Dictionary<string, string>();
            int i = 0, n = s.Length;

            while (i < n)
            {
                char c = s[i];
                if (c != '$') { sb.Append(c); i++; continue; }

                if (i + 1 >= n) { sb.Append('$'); i++; continue; }
                char next = s[i + 1];

                // $$ escape
                if (next == '$')
                {
                    sb.Append(escapeDoubleDollar ? "$" : "$$");
                    i += 2;
                    continue;
                }

                // ${...}
                if (next == '{')
                {
                    int close = s.IndexOf('}', i + 2);
                    if (close < 0) { sb.Append(c); i++; continue; } // unterminated — literal $
                    string inner = s.Substring(i + 2, close - (i + 2));
                    i = close + 1;

                    string name = inner;
                    string? defaultVal = null;
                    bool required = false;

                    int op = FindOperator(inner);
                    if (op >= 0)
                    {
                        name = inner.Substring(0, op);
                        // operator is 2 chars: ":-" or ":?"
                        char kind = inner[op + 1];
                        string rest = inner.Length > op + 2 ? inner.Substring(op + 2) : string.Empty;
                        if (kind == '-') defaultVal = rest;
                        else if (kind == '?') required = true;
                    }

                    name = name.Trim();
                    if (name.Length == 0) { sb.Append("${").Append(inner).Append('}'); continue; }
                    if (refset.Add(name)) res.Referenced.Add(name);

                    if (TryGet(map, name, useEnvironmentFallback, out string val) && val.Length > 0)
                    {
                        sb.Append(val);
                    }
                    else if (defaultVal != null)
                    {
                        sb.Append(defaultVal);
                    }
                    else if (required)
                    {
                        if (missingSet.Add(name)) res.Missing.Add(name);
                        if (unresolvedSet.Add(name)) res.Unresolved.Add(name);
                        sb.Append("${").Append(name).Append("}"); // leave marker
                    }
                    else
                    {
                        if (unresolvedSet.Add(name)) res.Unresolved.Add(name);
                        // leave placeholder visible so the user sees what's missing
                        sb.Append("${").Append(name).Append("}");
                    }
                    continue;
                }

                // $VAR (bare)
                if (IsNameStart(next))
                {
                    int j = i + 1, start = j;
                    while (j < n && IsNamePart(s[j])) j++;
                    string name = s.Substring(start, j - start);
                    i = j;
                    if (refset.Add(name)) res.Referenced.Add(name);

                    if (TryGet(map, name, useEnvironmentFallback, out string val) && val.Length > 0)
                    {
                        sb.Append(val);
                    }
                    else
                    {
                        if (unresolvedSet.Add(name)) res.Unresolved.Add(name);
                        sb.Append('$').Append(name);
                    }
                    continue;
                }

                // lone $ followed by something else — literal
                sb.Append('$');
                i++;
            }
        }
        catch { /* never throw — return partial */ }

        res.Output = sb.ToString();
        return res;
    }

    // Find the first ":-" or ":?" operator index inside a ${...} body, else -1.
    private static int FindOperator(string inner)
    {
        for (int k = 0; k + 1 < inner.Length; k++)
        {
            if (inner[k] == ':' && (inner[k + 1] == '-' || inner[k + 1] == '?'))
                return k;
        }
        return -1;
    }

    private static bool TryGet(IDictionary<string, string> map, string name, bool envFallback, out string value)
    {
        value = string.Empty;
        try
        {
            if (map != null && map.TryGetValue(name, out var v) && v != null)
            {
                value = v;
                if (value.Length > 0) return true;
            }
            if (envFallback)
            {
                string? ev = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(ev)) { value = ev; return true; }
            }
        }
        catch { /* never throw */ }
        return value.Length > 0;
    }
}
