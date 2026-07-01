using System;
using System.Net;

namespace WinForge.Services;

/// <summary>
/// HTML 即時預覽小幫手 · Small guarded helpers for the HTML live-preview module.
/// Pure managed C#; no NuGet, no Process.Start. Every method swallows failures and
/// returns something safe — the UI never has to worry about an exception here.
/// </summary>
public static class HtmlPreviewService
{
    /// <summary>A friendly bilingual sample document shown when the module opens.</summary>
    public const string SampleHtml =
@"<!DOCTYPE html>
<html>
  <head>
    <meta charset=""utf-8"" />
    <style>
      body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 24px; line-height: 1.55; }
      h1 { color: #2f9e44; }
      code { background: #f1f3f5; padding: 2px 6px; border-radius: 4px; }
      .card { border: 1px solid #ced4da; border-radius: 8px; padding: 12px 16px; margin-top: 12px; }
    </style>
  </head>
  <body>
    <h1>HTML Preview · HTML 預覽</h1>
    <p>Edit the HTML on the <strong>left</strong> — see it render live on the <strong>right</strong>.</p>
    <p>喺左邊改 <code>HTML</code>，右邊即時見到效果。</p>
    <div class=""card"">
      <p>Try a list:</p>
      <ul>
        <li>Headings, <em>emphasis</em> and <code>&lt;code&gt;</code></li>
        <li>Tables, links and images</li>
        <li>Inline <span style=""color:#2f9e44;"">styles</span></li>
      </ul>
    </div>
    <p>Happy hacking! 玩得開心！</p>
  </body>
</html>
";

    /// <summary>
    /// Escape an HTML source string so it can be shown as literal text (entities preserved).
    /// Never throws — falls back to the input on the off-chance encoding blows up.
    /// </summary>
    public static string Escape(string? source)
    {
        try { return WebUtility.HtmlEncode(source ?? string.Empty); }
        catch { return source ?? string.Empty; }
    }

    /// <summary>
    /// If the source looks like a bare fragment (no &lt;html&gt;/&lt;!doctype&gt;), wrap it in a
    /// minimal HTML5 document so the previewer always has a valid page to navigate to.
    /// Already-complete documents are returned unchanged.
    /// </summary>
    public static string WrapFragment(string? source)
    {
        string html = source ?? string.Empty;
        try
        {
            string probe = html.TrimStart();
            if (probe.StartsWith("<!doctype", StringComparison.OrdinalIgnoreCase) ||
                probe.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
                return html;

            return "<!DOCTYPE html><html><head><meta charset=\"utf-8\" /></head><body>" + html + "</body></html>";
        }
        catch { return html; }
    }
}
