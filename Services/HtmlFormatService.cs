using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// HTML 格式化 / 壓縮 · Pure-managed HTML formatter/minifier. Own tokenizer — no Markdig,
/// no HtmlAgilityPack, no external NuGet. Best-effort on malformed HTML; never throws.
/// Preserves the raw content of &lt;pre&gt;, &lt;script&gt;, &lt;style&gt;, &lt;textarea&gt; verbatim.
/// </summary>
public static class HtmlFormatService
{
    // Elements that never have a closing tag / are self-contained.
    private static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "keygen",
        "link", "meta", "param", "source", "track", "wbr", "!doctype"
    };

    // Elements whose inner text must be preserved byte-for-byte (no re-indent, no collapse).
    private static readonly HashSet<string> Raw = new(StringComparer.OrdinalIgnoreCase)
    {
        "pre", "script", "style", "textarea"
    };

    // Inline elements — kept on the same line as adjacent text where practical.
    private static readonly HashSet<string> Inline = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "abbr", "b", "bdi", "bdo", "br", "cite", "code", "data", "dfn", "em",
        "i", "kbd", "mark", "q", "rp", "rt", "ruby", "s", "samp", "small", "span",
        "strong", "sub", "sup", "time", "u", "var", "wbr"
    };

    private enum Kind { Open, Close, SelfClose, Text, Comment, Doctype, Raw }

    private sealed class Token
    {
        public Kind Kind;
        public string Name = "";   // lowercased tag name (for Open/Close/SelfClose/Raw)
        public string Text = "";   // full literal text (tag markup, text run, comment, or raw block)
    }

    // ----------------------------------------------------------------------------------
    // Tokenizer
    // ----------------------------------------------------------------------------------
    private static List<Token> Tokenize(string html)
    {
        var tokens = new List<Token>();
        int i = 0, n = html.Length;

        while (i < n)
        {
            char c = html[i];
            if (c == '<' && i + 1 < n)
            {
                char next = html[i + 1];

                // Comment <!-- ... -->
                if (next == '!' && i + 3 < n && html[i + 2] == '-' && html[i + 3] == '-')
                {
                    int end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                    end = end < 0 ? n : end + 3;
                    tokens.Add(new Token { Kind = Kind.Comment, Text = html.Substring(i, end - i) });
                    i = end;
                    continue;
                }

                // Doctype / declaration <!doctype ...>
                if (next == '!')
                {
                    int end = html.IndexOf('>', i);
                    end = end < 0 ? n : end + 1;
                    tokens.Add(new Token { Kind = Kind.Doctype, Text = html.Substring(i, end - i) });
                    i = end;
                    continue;
                }

                // Closing tag </name>
                if (next == '/')
                {
                    int end = html.IndexOf('>', i);
                    end = end < 0 ? n : end + 1;
                    string markup = html.Substring(i, end - i);
                    tokens.Add(new Token { Kind = Kind.Close, Name = ExtractName(markup), Text = markup });
                    i = end;
                    continue;
                }

                // Opening / self-closing tag — but only if it looks like a real tag name.
                if (char.IsLetter(next))
                {
                    int end = FindTagEnd(html, i);
                    string markup = html.Substring(i, end - i);
                    string name = ExtractName(markup);
                    bool self = markup.EndsWith("/>", StringComparison.Ordinal) || Void.Contains(name);

                    if (Raw.Contains(name) && !self)
                    {
                        // Consume verbatim until the matching close tag (case-insensitive).
                        string closeTag = "</" + name;
                        int close = IndexOfIgnoreCase(html, closeTag, end);
                        int rawEnd;
                        if (close < 0) rawEnd = n;
                        else
                        {
                            int gt = html.IndexOf('>', close);
                            rawEnd = gt < 0 ? n : gt + 1;
                        }
                        tokens.Add(new Token { Kind = Kind.Raw, Name = name, Text = html.Substring(i, rawEnd - i) });
                        i = rawEnd;
                        continue;
                    }

                    tokens.Add(new Token
                    {
                        Kind = self ? Kind.SelfClose : Kind.Open,
                        Name = name,
                        Text = markup
                    });
                    i = end;
                    continue;
                }

                // A lone '<' that isn't a tag — treat as text.
            }

            // Text run up to the next '<'.
            int textEnd = html.IndexOf('<', i + 1);
            if (textEnd < 0) textEnd = n;
            tokens.Add(new Token { Kind = Kind.Text, Text = html.Substring(i, textEnd - i) });
            i = textEnd;
        }

        return tokens;
    }

    // Finds the '>' that closes a start tag, skipping quoted attribute values.
    private static int FindTagEnd(string html, int start)
    {
        int n = html.Length;
        char quote = '\0';
        for (int i = start; i < n; i++)
        {
            char c = html[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
            }
            else if (c == '"' || c == '\'') quote = c;
            else if (c == '>') return i + 1;
        }
        return n;
    }

    private static string ExtractName(string markup)
    {
        int i = 1; // skip '<'
        if (i < markup.Length && markup[i] == '/') i++;
        int startName = i;
        while (i < markup.Length)
        {
            char c = markup[i];
            if (char.IsWhiteSpace(c) || c == '>' || c == '/') break;
            i++;
        }
        return markup.Substring(startName, i - startName).ToLowerInvariant();
    }

    private static int IndexOfIgnoreCase(string s, string value, int start)
        => s.IndexOf(value, Math.Min(start, s.Length), StringComparison.OrdinalIgnoreCase);

    // ----------------------------------------------------------------------------------
    // Format (pretty-print)
    // ----------------------------------------------------------------------------------
    public static string Format(string html, int indentSize)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        try
        {
            if (indentSize < 0) indentSize = 0;
            if (indentSize > 16) indentSize = 16;

            var tokens = Tokenize(html);
            var sb = new StringBuilder(html.Length + 64);
            int depth = 0;
            string unit = new string(' ', indentSize);

            foreach (var t in tokens)
            {
                switch (t.Kind)
                {
                    case Kind.Text:
                        {
                            string trimmed = t.Text.Trim();
                            if (trimmed.Length == 0) break; // drop whitespace-only text between tags
                            AppendLine(sb, depth, unit, CollapseInner(trimmed));
                            break;
                        }
                    case Kind.Comment:
                    case Kind.Doctype:
                        AppendLine(sb, depth, unit, t.Text.Trim());
                        break;
                    case Kind.Raw:
                        // One tag per line but keep the raw body verbatim.
                        AppendRawBlock(sb, depth, unit, t.Text);
                        break;
                    case Kind.SelfClose:
                        AppendLine(sb, depth, unit, t.Text.Trim());
                        break;
                    case Kind.Open:
                        AppendLine(sb, depth, unit, t.Text.Trim());
                        depth++;
                        break;
                    case Kind.Close:
                        if (depth > 0) depth--;
                        AppendLine(sb, depth, unit, t.Text.Trim());
                        break;
                }
            }

            return sb.ToString().TrimEnd('\r', '\n');
        }
        catch
        {
            return html; // never throw — hand back the original on any unexpected fault
        }
    }

    private static void AppendLine(StringBuilder sb, int depth, string unit, string content)
    {
        for (int d = 0; d < depth; d++) sb.Append(unit);
        sb.Append(content).Append('\n');
    }

    // Emits a raw element (pre/script/style/textarea): open tag indented, body verbatim.
    private static void AppendRawBlock(StringBuilder sb, int depth, string unit, string full)
    {
        for (int d = 0; d < depth; d++) sb.Append(unit);
        sb.Append(full).Append('\n');
    }

    // Collapse runs of internal whitespace inside a text run to single spaces.
    private static string CollapseInner(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool prevWs = false;
        foreach (char c in s)
        {
            if (char.IsWhiteSpace(c))
            {
                if (!prevWs) sb.Append(' ');
                prevWs = true;
            }
            else
            {
                sb.Append(c);
                prevWs = false;
            }
        }
        return sb.ToString();
    }

    // ----------------------------------------------------------------------------------
    // Minify
    // ----------------------------------------------------------------------------------
    public static string Minify(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;
        try
        {
            var tokens = Tokenize(html);
            var sb = new StringBuilder(html.Length);

            foreach (var t in tokens)
            {
                switch (t.Kind)
                {
                    case Kind.Text:
                        {
                            // Collapse insignificant whitespace; drop whitespace-only runs.
                            string collapsed = CollapseInner(t.Text);
                            if (collapsed.Length == 0) break;
                            if (collapsed == " " && EndsWithTag(sb)) break;
                            sb.Append(collapsed);
                            break;
                        }
                    case Kind.Comment:
                        break; // strip comments
                    case Kind.Raw:
                        sb.Append(t.Text); // verbatim — do not touch pre/script/style/textarea
                        break;
                    default:
                        sb.Append(CollapseTagMarkup(t.Text));
                        break;
                }
            }

            return sb.ToString().Trim();
        }
        catch
        {
            return html;
        }
    }

    private static bool EndsWithTag(StringBuilder sb)
        => sb.Length > 0 && sb[sb.Length - 1] == '>';

    // Collapse redundant whitespace inside a tag's markup while respecting quotes.
    private static string CollapseTagMarkup(string markup)
    {
        var sb = new StringBuilder(markup.Length);
        char quote = '\0';
        bool prevWs = false;
        foreach (char c in markup)
        {
            if (quote != '\0')
            {
                sb.Append(c);
                if (c == quote) quote = '\0';
                prevWs = false;
                continue;
            }
            if (c == '"' || c == '\'') { quote = c; sb.Append(c); prevWs = false; continue; }
            if (char.IsWhiteSpace(c))
            {
                if (!prevWs) sb.Append(' ');
                prevWs = true;
            }
            else
            {
                sb.Append(c);
                prevWs = false;
            }
        }
        return sb.ToString();
    }
}
