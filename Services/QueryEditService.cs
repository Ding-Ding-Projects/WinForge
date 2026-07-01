using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 網址查詢字串編輯器引擎 · URL query-string editor engine. Pure managed C# — parses a full URL or a raw
/// query string into ordered key/value pairs (percent-decoded), and rebuilds a URL/query with proper
/// percent-encoding while preserving scheme/host/path/fragment. Robust for malformed input; never throws.
/// </summary>
public static class QueryEditService
{
    /// <summary>Decoded, non-query components of the original input (empty strings when absent).</summary>
    public sealed class UrlParts
    {
        public string Scheme = "";
        public string Authority = "";  // host[:port][userinfo] — kept verbatim
        public string Path = "";
        public string Fragment = "";
        /// <summary>True when the input parsed as an absolute URL (has scheme + authority); false for a bare query string.</summary>
        public bool HasUrl;
    }

    /// <summary>One parsed key/value pair (already percent-decoded).</summary>
    public sealed class Pair
    {
        public string Key = "";
        public string Value = "";
        public bool HasEquals = true; // distinguishes "a" from "a=" so round-trips are faithful
    }

    /// <summary>
    /// Split raw input into its non-query parts and its list of query pairs. Accepts a full URL,
    /// a "?...=..." fragment, or a bare "a=1&amp;b=2" string. Never throws.
    /// </summary>
    public static (UrlParts parts, List<Pair> pairs) Parse(string? input)
    {
        var parts = new UrlParts();
        var pairs = new List<Pair>();
        input ??= "";
        input = input.Trim();
        if (input.Length == 0) return (parts, pairs);

        string query = input;
        try
        {
            // Prefer System.Uri for well-formed absolute URLs so authority/path/fragment are precise.
            if (Uri.TryCreate(input, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Scheme) && uri.IsAbsoluteUri)
            {
                parts.HasUrl = true;
                parts.Scheme = uri.Scheme;
                parts.Authority = uri.GetComponents(UriComponents.UserInfo | UriComponents.Host | UriComponents.Port, UriFormat.UriEscaped);
                parts.Path = SafeDecode(uri.AbsolutePath);
                parts.Fragment = uri.Fragment.StartsWith('#') ? SafeDecode(uri.Fragment[1..]) : SafeDecode(uri.Fragment);
                query = uri.Query.StartsWith('?') ? uri.Query[1..] : uri.Query;
            }
            else
            {
                // Manual fallback: pull off fragment, then query, keeping any leading scheme://authority/path verbatim.
                string work = input;
                int hash = work.IndexOf('#');
                if (hash >= 0) { parts.Fragment = SafeDecode(work[(hash + 1)..]); work = work[..hash]; }

                int q = work.IndexOf('?');
                if (q >= 0)
                {
                    query = work[(q + 1)..];
                    string before = work[..q];
                    SplitSchemeHostPath(before, parts);
                }
                else if (LooksLikeQuery(work))
                {
                    // Whole thing is a bare query string.
                    query = work;
                }
                else
                {
                    // No '?' and not query-shaped: treat as a URL-ish path with no query.
                    query = "";
                    SplitSchemeHostPath(work, parts);
                }
            }
        }
        catch
        {
            // Absolute fallback: treat the untouched input as a raw query.
            query = input;
        }

        ParsePairs(query, pairs);
        return (parts, pairs);
    }

    private static bool LooksLikeQuery(string s) => s.Contains('=') || s.Contains('&');

    private static void SplitSchemeHostPath(string before, UrlParts parts)
    {
        if (string.IsNullOrEmpty(before)) return;
        int scheme = before.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            parts.HasUrl = true;
            parts.Scheme = before[..scheme];
            string rest = before[(scheme + 3)..];
            int slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                parts.Authority = rest[..slash];
                parts.Path = SafeDecode(rest[slash..]);
            }
            else
            {
                parts.Authority = rest;
                parts.Path = "";
            }
        }
        else
        {
            // No scheme — the leading text is a path.
            parts.Path = SafeDecode(before);
        }
    }

    private static void ParsePairs(string query, List<Pair> pairs)
    {
        if (string.IsNullOrEmpty(query)) return;
        foreach (var segment in query.Split('&'))
        {
            if (segment.Length == 0) continue;
            int eq = segment.IndexOf('=');
            if (eq < 0)
            {
                pairs.Add(new Pair { Key = SafeDecode(segment), Value = "", HasEquals = false });
            }
            else
            {
                pairs.Add(new Pair
                {
                    Key = SafeDecode(segment[..eq]),
                    Value = SafeDecode(segment[(eq + 1)..]),
                    HasEquals = true,
                });
            }
        }
    }

    /// <summary>Percent-decode, tolerant of malformed sequences and '+' as space. Never throws.</summary>
    public static string SafeDecode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        try
        {
            return Uri.UnescapeDataString(s.Replace('+', ' '));
        }
        catch
        {
            try { return s!.Replace('+', ' '); } catch { return s ?? ""; }
        }
    }

    /// <summary>Percent-encode a value for safe placement in a query. Never throws.</summary>
    public static string Encode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        try { return Uri.EscapeDataString(s); }
        catch { return s ?? ""; }
    }

    /// <summary>Build just the query string (no leading '?') from the enabled pairs, encoding each part.</summary>
    public static string BuildQuery(IEnumerable<Pair> pairs, IEnumerable<bool>? enabled = null)
    {
        var sb = new StringBuilder();
        bool first = true;
        using var flag = (enabled ?? System.Linq.Enumerable.Empty<bool>()).GetEnumerator();
        foreach (var p in pairs)
        {
            if (p is null) continue;
            if (!first) sb.Append('&');
            first = false;
            sb.Append(Encode(p.Key));
            if (p.HasEquals || p.Value.Length > 0)
            {
                sb.Append('=');
                sb.Append(Encode(p.Value));
            }
        }
        return sb.ToString();
    }

    /// <summary>Rebuild a full URL/query from parts + pairs. Preserves scheme/host/path/fragment. Never throws.</summary>
    public static string BuildUrl(UrlParts parts, IEnumerable<Pair> pairs)
    {
        try
        {
            string query = BuildQuery(pairs);
            var sb = new StringBuilder();
            if (parts.HasUrl && !string.IsNullOrEmpty(parts.Scheme))
            {
                sb.Append(parts.Scheme).Append("://").Append(parts.Authority);
                if (!string.IsNullOrEmpty(parts.Path))
                {
                    if (!parts.Path.StartsWith('/')) sb.Append('/');
                    sb.Append(EncodePath(parts.Path));
                }
            }
            else if (!string.IsNullOrEmpty(parts.Path))
            {
                sb.Append(EncodePath(parts.Path));
            }

            if (query.Length > 0) sb.Append('?').Append(query);
            if (!string.IsNullOrEmpty(parts.Fragment)) sb.Append('#').Append(Encode(parts.Fragment));
            return sb.ToString();
        }
        catch
        {
            return BuildQuery(pairs);
        }
    }

    /// <summary>Encode a path, keeping '/' separators intact.</summary>
    private static string EncodePath(string path)
    {
        try
        {
            var segs = path.Split('/');
            for (int i = 0; i < segs.Length; i++) segs[i] = Encode(segs[i]);
            return string.Join('/', segs);
        }
        catch { return path; }
    }
}
