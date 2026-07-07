using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// HTML meta-tag 產生器 · Meta-tag generator — pure managed. Builds a &lt;title&gt;, &lt;meta&gt; and
/// &lt;link rel="canonical"&gt; block from a set of optional fields, HTML-encoding every attribute value
/// (System.Net.WebUtility) and emitting only tags whose fields are non-empty. Never throws.
/// </summary>
public static class MetaTagsService
{
    /// <summary>All the inputs the generator understands. Empty fields are simply skipped.</summary>
    public sealed class Input
    {
        public string? Title;
        public string? Description;
        public string? Keywords;
        public string? Author;
        public string? Canonical;
        public string? Viewport;
        public string? ThemeColor;
        public string? Charset;

        public string? OgTitle;
        public string? OgDescription;
        public string? OgImage;
        public string? OgUrl;
        public string? OgType;

        public string? TwitterCard;
        public string? TwitterSite;
        public string? TwitterCreator;
    }

    private static string Enc(string? s) => WebUtility.HtmlEncode((s ?? string.Empty).Trim());

    private static bool Has(string? s) => !string.IsNullOrWhiteSpace(s);

    /// <summary>Build the HTML head block. Always returns a string; never throws.</summary>
    public static string Build(Input i)
    {
        var lines = new List<string>();
        try
        {
            if (i == null) return string.Empty;

            if (Has(i.Charset))
                lines.Add($"<meta charset=\"{Enc(i.Charset)}\">");

            if (Has(i.Title))
                lines.Add($"<title>{Enc(i.Title)}</title>");

            if (Has(i.Viewport))
                lines.Add($"<meta name=\"viewport\" content=\"{Enc(i.Viewport)}\">");

            if (Has(i.Description))
                lines.Add($"<meta name=\"description\" content=\"{Enc(i.Description)}\">");

            if (Has(i.Keywords))
                lines.Add($"<meta name=\"keywords\" content=\"{Enc(i.Keywords)}\">");

            if (Has(i.Author))
                lines.Add($"<meta name=\"author\" content=\"{Enc(i.Author)}\">");

            if (Has(i.ThemeColor))
                lines.Add($"<meta name=\"theme-color\" content=\"{Enc(i.ThemeColor)}\">");

            if (Has(i.Canonical))
                lines.Add($"<link rel=\"canonical\" href=\"{Enc(i.Canonical)}\">");

            // Open Graph
            if (Has(i.OgTitle))
                lines.Add($"<meta property=\"og:title\" content=\"{Enc(i.OgTitle)}\">");
            if (Has(i.OgDescription))
                lines.Add($"<meta property=\"og:description\" content=\"{Enc(i.OgDescription)}\">");
            if (Has(i.OgType))
                lines.Add($"<meta property=\"og:type\" content=\"{Enc(i.OgType)}\">");
            if (Has(i.OgUrl))
                lines.Add($"<meta property=\"og:url\" content=\"{Enc(i.OgUrl)}\">");
            if (Has(i.OgImage))
                lines.Add($"<meta property=\"og:image\" content=\"{Enc(i.OgImage)}\">");

            // Twitter
            if (Has(i.TwitterCard))
                lines.Add($"<meta name=\"twitter:card\" content=\"{Enc(i.TwitterCard)}\">");
            if (Has(i.TwitterSite))
                lines.Add($"<meta name=\"twitter:site\" content=\"{Enc(i.TwitterSite)}\">");
            if (Has(i.TwitterCreator))
                lines.Add($"<meta name=\"twitter:creator\" content=\"{Enc(i.TwitterCreator)}\">");
        }
        catch
        {
            // Never throw — return whatever we managed to build.
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Count of tags that would be emitted for the given input.</summary>
    public static int Count(Input i)
    {
        try
        {
            string s = Build(i);
            if (string.IsNullOrEmpty(s)) return 0;
            return s.Split('\n').Length;
        }
        catch { return 0; }
    }
}
