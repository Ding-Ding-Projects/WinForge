using System;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// CSS 格式化 / 壓縮 · CSS beautifier + minifier. Pure managed, own tokenizer that understands
/// strings, /* comments */ and nested { } blocks (@media / @keyframes). Never throws — on any
/// failure it returns the original text unchanged so the UI can surface a friendly status.
/// </summary>
public static class CssFormatService
{
    /// <summary>Beautify CSS: one selector line, declarations indented one-per-line, blank line between rules.</summary>
    public static string Format(string? css, int indentSize)
    {
        if (string.IsNullOrWhiteSpace(css)) return string.Empty;
        if (indentSize < 0) indentSize = 0;
        if (indentSize > 16) indentSize = 16;
        try { return FormatCore(css!, indentSize); }
        catch { return css ?? string.Empty; }
    }

    /// <summary>Minify CSS: strip comments + insignificant whitespace, collapse to compact form.</summary>
    public static string Minify(string? css)
    {
        if (string.IsNullOrWhiteSpace(css)) return string.Empty;
        try { return MinifyCore(css!); }
        catch { return css ?? string.Empty; }
    }

    private static string Indent(int depth, int size) => new string(' ', Math.Max(0, depth) * size);

    private static string FormatCore(string css, int indentSize)
    {
        var sb = new StringBuilder(css.Length + 64);
        int depth = 0;
        int i = 0, n = css.Length;
        var buffer = new StringBuilder(); // accumulates the current selector / declaration text

        bool atLineStart = true; // nothing meaningful emitted on current output line yet
        bool needBlankBeforeRule = false; // insert a blank line before the next top-level-ish rule

        void TrimBuffer()
        {
            // collapse internal runs of whitespace to single spaces and trim ends
            var s = buffer.ToString();
            buffer.Clear();
            var outSb = new StringBuilder(s.Length);
            bool prevWs = false;
            foreach (char c in s)
            {
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
                {
                    if (!prevWs) outSb.Append(' ');
                    prevWs = true;
                }
                else { outSb.Append(c); prevWs = false; }
            }
            buffer.Append(outSb.ToString().Trim());
        }

        while (i < n)
        {
            char c = css[i];

            // ---- comment ----
            if (c == '/' && i + 1 < n && css[i + 1] == '*')
            {
                int end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                string comment = end < 0 ? css.Substring(i) : css.Substring(i, end - i + 2);
                i = end < 0 ? n : end + 2;

                TrimBuffer();
                if (buffer.Length > 0)
                {
                    // comment trailing on a selector/declaration fragment — keep it inline
                    buffer.Append(' ').Append(comment);
                }
                else
                {
                    if (needBlankBeforeRule) { sb.Append('\n'); needBlankBeforeRule = false; }
                    if (!atLineStart) sb.Append('\n');
                    sb.Append(Indent(depth, indentSize)).Append(comment).Append('\n');
                    atLineStart = true;
                }
                continue;
            }

            // ---- string literal (single or double quoted) ----
            if (c == '"' || c == '\'')
            {
                buffer.Append(c);
                i++;
                while (i < n)
                {
                    char sc = css[i];
                    buffer.Append(sc);
                    if (sc == '\\' && i + 1 < n) { buffer.Append(css[i + 1]); i += 2; continue; }
                    i++;
                    if (sc == c) break;
                }
                continue;
            }

            // ---- open block ----
            if (c == '{')
            {
                TrimBuffer();
                if (needBlankBeforeRule) { sb.Append('\n'); needBlankBeforeRule = false; }
                sb.Append(Indent(depth, indentSize)).Append(buffer.ToString());
                buffer.Clear();
                sb.Append(" {\n");
                depth++;
                atLineStart = true;
                i++;
                continue;
            }

            // ---- close block ----
            if (c == '}')
            {
                // flush any dangling declaration without a trailing semicolon
                TrimBuffer();
                if (buffer.Length > 0)
                {
                    sb.Append(Indent(depth, indentSize)).Append(buffer.ToString());
                    if (!buffer.ToString().EndsWith(";", StringComparison.Ordinal)) sb.Append(';');
                    sb.Append('\n');
                    buffer.Clear();
                }
                depth = Math.Max(0, depth - 1);
                sb.Append(Indent(depth, indentSize)).Append("}\n");
                atLineStart = true;
                needBlankBeforeRule = true; // blank line before the following rule
                i++;
                continue;
            }

            // ---- end of declaration ----
            if (c == ';')
            {
                TrimBuffer();
                if (buffer.Length > 0)
                {
                    string decl = NormalizeDeclaration(buffer.ToString());
                    if (needBlankBeforeRule) { sb.Append('\n'); needBlankBeforeRule = false; }
                    sb.Append(Indent(depth, indentSize)).Append(decl).Append(";\n");
                    buffer.Clear();
                    atLineStart = true;
                }
                i++;
                continue;
            }

            buffer.Append(c);
            i++;
        }

        // trailing content (e.g. an at-rule without a block, like @import ...;)
        TrimBuffer();
        if (buffer.Length > 0)
        {
            if (needBlankBeforeRule) { sb.Append('\n'); }
            sb.Append(Indent(depth, indentSize)).Append(buffer.ToString()).Append('\n');
        }

        // tidy up: no trailing whitespace, single trailing newline stripped
        return sb.ToString().Replace("\r\n", "\n").TrimEnd('\n');
    }

    // Put exactly one space after each top-level ':' in a declaration ("color:red" -> "color: red").
    private static string NormalizeDeclaration(string decl)
    {
        int idx = -1;
        bool inStr = false; char q = '\0';
        for (int k = 0; k < decl.Length; k++)
        {
            char c = decl[k];
            if (inStr) { if (c == '\\') { k++; continue; } if (c == q) inStr = false; continue; }
            if (c == '"' || c == '\'') { inStr = true; q = c; continue; }
            if (c == ':') { idx = k; break; }
        }
        if (idx < 0) return decl;
        string prop = decl.Substring(0, idx).TrimEnd();
        string val = decl.Substring(idx + 1).TrimStart();
        return prop + ": " + val;
    }

    private static string MinifyCore(string css)
    {
        var sb = new StringBuilder(css.Length);
        int i = 0, n = css.Length;
        bool pendingSpace = false; // a run of whitespace we may or may not emit

        bool IsSep(char c) => c == '{' || c == '}' || c == ';' || c == ':' || c == ',' || c == '>' || c == '~' || c == '(' || c == ')';

        while (i < n)
        {
            char c = css[i];

            // comment — drop entirely
            if (c == '/' && i + 1 < n && css[i + 1] == '*')
            {
                int end = css.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? n : end + 2;
                pendingSpace = true; // treat as whitespace boundary
                continue;
            }

            // string literal — copy verbatim
            if (c == '"' || c == '\'')
            {
                if (pendingSpace) { if (sb.Length > 0 && !IsSep(sb[sb.Length - 1])) sb.Append(' '); pendingSpace = false; }
                sb.Append(c);
                i++;
                while (i < n)
                {
                    char sc = css[i];
                    sb.Append(sc);
                    if (sc == '\\' && i + 1 < n) { sb.Append(css[i + 1]); i += 2; continue; }
                    i++;
                    if (sc == c) break;
                }
                continue;
            }

            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                pendingSpace = true;
                i++;
                continue;
            }

            // emit a single collapsed space only when it is significant (between two non-separators)
            if (pendingSpace)
            {
                if (sb.Length > 0 && !IsSep(sb[sb.Length - 1]) && !IsSep(c))
                    sb.Append(' ');
                pendingSpace = false;
            }

            // no space before a separator we just emitted; also strip space we may have queued
            if (IsSep(c) && sb.Length > 0 && sb[sb.Length - 1] == ' ')
                sb.Length--;

            sb.Append(c);
            i++;
        }

        // drop redundant last semicolon before a closing brace: "…;}" -> "…}"
        var result = sb.ToString().Replace(";}", "}");
        return result.Trim();
    }
}
