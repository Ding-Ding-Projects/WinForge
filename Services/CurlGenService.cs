using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// cURL / fetch / PowerShell 片段產生器 · Snippet generator. Pure managed, offline — turns
/// a request description (URL, method, headers, body, auth) into copy-paste-ready
/// <c>curl</c>, JavaScript <c>fetch()</c> and PowerShell <c>Invoke-RestMethod</c> code.
/// Never performs a network call; every method is robust and never throws.
/// </summary>
public static class CurlGenService
{
    /// <summary>An immutable description of the request to render.</summary>
    public sealed class Request
    {
        public string Url = "";
        public string Method = "GET";
        public List<KeyValuePair<string, string>> Headers = new();
        public string Body = "";
        public string ContentType = "";
        /// <summary>none | bearer | basic</summary>
        public string AuthKind = "none";
        public string BearerToken = "";
        public string BasicUser = "";
        public string BasicPass = "";
    }

    private static bool HasBody(string method) =>
        method is "POST" or "PUT" or "PATCH" or "DELETE";

    // Effective headers = user headers + content-type (if body) + auth, without duplicating.
    private static List<KeyValuePair<string, string>> Effective(Request r)
    {
        var list = new List<KeyValuePair<string, string>>();
        bool hasCt = false, hasAuth = false;
        foreach (var h in r.Headers)
        {
            var k = (h.Key ?? "").Trim();
            if (k.Length == 0) continue;
            if (k.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) hasCt = true;
            if (k.Equals("Authorization", StringComparison.OrdinalIgnoreCase)) hasAuth = true;
            list.Add(new(k, h.Value ?? ""));
        }

        bool bodyPresent = HasBody(r.Method) && !string.IsNullOrEmpty(r.Body);
        if (bodyPresent && !hasCt && !string.IsNullOrWhiteSpace(r.ContentType))
            list.Add(new("Content-Type", r.ContentType.Trim()));

        if (!hasAuth)
        {
            if (r.AuthKind == "bearer" && !string.IsNullOrWhiteSpace(r.BearerToken))
                list.Add(new("Authorization", "Bearer " + r.BearerToken.Trim()));
            else if (r.AuthKind == "basic" &&
                     (!string.IsNullOrEmpty(r.BasicUser) || !string.IsNullOrEmpty(r.BasicPass)))
            {
                var raw = (r.BasicUser ?? "") + ":" + (r.BasicPass ?? "");
                var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                list.Add(new("Authorization", "Basic " + b64));
            }
        }
        return list;
    }

    // ---- quoting helpers ----

    // Single-quote for POSIX shells (curl); '\'' escapes an embedded quote.
    private static string ShQuote(string s) =>
        "'" + (s ?? "").Replace("'", "'\\''") + "'";

    // JS single-quoted string literal.
    private static string JsQuote(string s)
    {
        var sb = new StringBuilder("'");
        foreach (var c in s ?? "")
            sb.Append(c switch
            {
                '\\' => "\\\\",
                '\'' => "\\'",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => c.ToString()
            });
        return sb.Append('\'').ToString();
    }

    // PowerShell single-quoted string literal ('' escapes a quote).
    private static string PsQuote(string s) =>
        "'" + (s ?? "").Replace("'", "''") + "'";

    public static string Curl(Request r)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(r.Url) ? "https://example.com" : r.Url.Trim();
            var sb = new StringBuilder("curl ");
            var method = (r.Method ?? "GET").ToUpperInvariant();
            if (method != "GET") sb.Append("-X ").Append(method).Append(" \\\n     ");
            sb.Append(ShQuote(url));

            foreach (var h in Effective(r))
                sb.Append(" \\\n     -H ").Append(ShQuote(h.Key + ": " + h.Value));

            if (HasBody(method) && !string.IsNullOrEmpty(r.Body))
                sb.Append(" \\\n     --data ").Append(ShQuote(r.Body));

            return sb.ToString();
        }
        catch (Exception ex) { return "# error: " + ex.Message; }
    }

    public static string Fetch(Request r)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(r.Url) ? "https://example.com" : r.Url.Trim();
            var method = (r.Method ?? "GET").ToUpperInvariant();
            var headers = Effective(r);
            var sb = new StringBuilder();
            sb.Append("const res = await fetch(").Append(JsQuote(url)).Append(", {\n");
            sb.Append("  method: ").Append(JsQuote(method)).Append(",\n");

            if (headers.Count > 0)
            {
                sb.Append("  headers: {\n");
                for (int i = 0; i < headers.Count; i++)
                {
                    sb.Append("    ").Append(JsQuote(headers[i].Key)).Append(": ")
                      .Append(JsQuote(headers[i].Value));
                    sb.Append(i < headers.Count - 1 ? ",\n" : "\n");
                }
                sb.Append("  },\n");
            }

            if (HasBody(method) && !string.IsNullOrEmpty(r.Body))
                sb.Append("  body: ").Append(JsQuote(r.Body)).Append(",\n");

            sb.Append("});\n");
            sb.Append("const data = await res.text();\n");
            sb.Append("console.log(res.status, data);");
            return sb.ToString();
        }
        catch (Exception ex) { return "// error: " + ex.Message; }
    }

    public static string PowerShell(Request r)
    {
        try
        {
            var url = string.IsNullOrWhiteSpace(r.Url) ? "https://example.com" : r.Url.Trim();
            var method = (r.Method ?? "GET").ToUpperInvariant();
            var headers = Effective(r);
            var sb = new StringBuilder();

            if (headers.Count > 0)
            {
                sb.Append("$headers = @{\n");
                foreach (var h in headers)
                    sb.Append("  ").Append(PsQuote(h.Key)).Append(" = ")
                      .Append(PsQuote(h.Value)).Append('\n');
                sb.Append("}\n");
            }

            bool bodyPresent = HasBody(method) && !string.IsNullOrEmpty(r.Body);
            if (bodyPresent)
                sb.Append("$body = ").Append(PsQuote(r.Body)).Append('\n');

            sb.Append("Invoke-RestMethod ")
              .Append("-Uri ").Append(PsQuote(url))
              .Append(" -Method ").Append(method);
            if (headers.Count > 0) sb.Append(" `\n  -Headers $headers");
            if (bodyPresent) sb.Append(" `\n  -Body $body");
            return sb.ToString();
        }
        catch (Exception ex) { return "# error: " + ex.Message; }
    }
}
