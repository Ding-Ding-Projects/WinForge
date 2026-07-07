using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// URL 及查詢字串工具 · URL &amp; query-string tools — pure managed parsing/encoding.
/// No System.Web (unavailable in .NET desktop) — manual query split + Uri.EscapeDataString/UnescapeDataString.
/// Never throws; every method degrades gracefully on relative/invalid input.
/// </summary>
public static class UrlToolsService
{
    /// <summary>One decoded key/value pair from a query string.</summary>
    public sealed class QueryParam
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public QueryParam() { }
        public QueryParam(string key, string value) { Key = key; Value = value; }
    }

    /// <summary>The broken-down components of a URL. All strings, never null.</summary>
    public sealed class UrlParts
    {
        public bool Valid { get; set; }
        public string Scheme { get; set; } = "";
        public string UserInfo { get; set; } = "";
        public string Host { get; set; } = "";
        public string Port { get; set; } = "";
        public string Path { get; set; } = "";
        public string Query { get; set; } = "";   // without leading '?'
        public string Fragment { get; set; } = ""; // without leading '#'
    }

    /// <summary>URL-encode a value (RFC 3986 style). Never throws.</summary>
    public static string Encode(string? s)
    {
        try { return Uri.EscapeDataString(s ?? ""); }
        catch { return s ?? ""; }
    }

    /// <summary>URL-decode a value. Never throws — returns the input unchanged if it can't decode.</summary>
    public static string Decode(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        try { return Uri.UnescapeDataString(s.Replace('+', ' ')); }
        catch { return s; }
    }

    /// <summary>
    /// Break a URL into parts. Uses System.Uri for absolute URIs; falls back to a manual split
    /// for relative or otherwise-rejected inputs so the operator always sees something useful.
    /// </summary>
    public static UrlParts Parse(string? url)
    {
        var p = new UrlParts();
        url ??= "";
        if (url.Trim().Length == 0) return p;

        try
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                p.Valid = true;
                p.Scheme = uri.Scheme ?? "";
                p.UserInfo = uri.UserInfo ?? "";
                p.Host = uri.Host ?? "";
                p.Port = uri.IsDefaultPort || uri.Port < 0 ? "" : uri.Port.ToString();
                p.Path = uri.AbsolutePath ?? "";
                p.Query = (uri.Query ?? "").TrimStart('?');
                p.Fragment = (uri.Fragment ?? "").TrimStart('#');
                return p;
            }
        }
        catch { /* fall through to manual */ }

        ManualSplit(url, p);
        return p;
    }

    /// <summary>Best-effort manual split for relative/invalid inputs. Never throws.</summary>
    private static void ManualSplit(string url, UrlParts p)
    {
        string rest = url;

        // fragment
        int hash = rest.IndexOf('#');
        if (hash >= 0) { p.Fragment = rest.Substring(hash + 1); rest = rest.Substring(0, hash); }

        // query
        int q = rest.IndexOf('?');
        if (q >= 0) { p.Query = rest.Substring(q + 1); rest = rest.Substring(0, q); }

        // scheme
        int scheme = rest.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0)
        {
            p.Scheme = rest.Substring(0, scheme);
            rest = rest.Substring(scheme + 3);

            // authority up to first '/'
            int slash = rest.IndexOf('/');
            string authority = slash >= 0 ? rest.Substring(0, slash) : rest;
            p.Path = slash >= 0 ? rest.Substring(slash) : "";

            // user-info
            int at = authority.IndexOf('@');
            if (at >= 0) { p.UserInfo = authority.Substring(0, at); authority = authority.Substring(at + 1); }

            // port (last colon, but not inside IPv6 brackets)
            int colon = authority.LastIndexOf(':');
            int bracket = authority.LastIndexOf(']');
            if (colon > bracket && colon >= 0)
            {
                p.Host = authority.Substring(0, colon);
                p.Port = authority.Substring(colon + 1);
            }
            else
            {
                p.Host = authority;
            }
        }
        else
        {
            // relative — everything left is the path
            p.Path = rest;
        }
    }

    /// <summary>Split a raw query string (no leading '?') into decoded key/value pairs. Never throws.</summary>
    public static List<QueryParam> ParseQuery(string? query)
    {
        var list = new List<QueryParam>();
        if (string.IsNullOrEmpty(query)) return list;
        query = query.TrimStart('?');
        foreach (var raw in query.Split('&'))
        {
            if (raw.Length == 0) continue;
            int eq = raw.IndexOf('=');
            string k, v;
            if (eq >= 0) { k = raw.Substring(0, eq); v = raw.Substring(eq + 1); }
            else { k = raw; v = ""; }
            list.Add(new QueryParam(Decode(k), Decode(v)));
        }
        return list;
    }

    /// <summary>Re-encode decoded pairs back into a properly-escaped query string (no leading '?').</summary>
    public static string BuildQuery(IEnumerable<QueryParam> pairs)
    {
        var sb = new StringBuilder();
        foreach (var pr in pairs)
        {
            if (pr == null || string.IsNullOrEmpty(pr.Key)) continue;
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Encode(pr.Key));
            sb.Append('=');
            sb.Append(Encode(pr.Value));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Reassemble a URL from parts + a decoded param list. Query is rebuilt from the pairs
    /// (properly encoded); other parts are used verbatim. Never throws.
    /// </summary>
    public static string Rebuild(UrlParts p, IEnumerable<QueryParam> pairs)
    {
        try
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(p.Scheme))
            {
                sb.Append(p.Scheme);
                sb.Append("://");
                if (!string.IsNullOrEmpty(p.UserInfo)) { sb.Append(p.UserInfo); sb.Append('@'); }
                sb.Append(p.Host);
                if (!string.IsNullOrEmpty(p.Port)) { sb.Append(':'); sb.Append(p.Port); }
            }
            sb.Append(p.Path);

            string query = BuildQuery(pairs);
            if (query.Length > 0) { sb.Append('?'); sb.Append(query); }
            if (!string.IsNullOrEmpty(p.Fragment)) { sb.Append('#'); sb.Append(p.Fragment); }
            return sb.ToString();
        }
        catch { return ""; }
    }
}
