using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// HTML → Markdown 轉換器 · Pure-managed, best-effort HTML→Markdown converter. Own tiny tokenizer
/// (regex over tags), no NuGet (no Markdig / HtmlAgilityPack). Robust: never throws on malformed
/// input — on any failure it degrades to a decoded, tag-stripped plain-text rendering.
/// </summary>
public static class HtmlToMdService
{
    private static readonly Regex TagRx = new(@"<[^>]+>", RegexOptions.Compiled | RegexOptions.Singleline);
    private static readonly Regex AttrRx = new("([a-zA-Z-]+)\\s*=\\s*(\"[^\"]*\"|'[^']*'|[^\\s>]+)", RegexOptions.Compiled);
    private static readonly Regex WsRx = new(@"[ \t\f\v]+", RegexOptions.Compiled);
    private static readonly Regex BlankLinesRx = new(@"\n{3,}", RegexOptions.Compiled);

    /// <summary>Convert an HTML fragment/document to Markdown. Never throws.</summary>
    public static string Convert(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        try
        {
            return ConvertCore(html!);
        }
        catch
        {
            // Absolute fallback: strip tags + decode entities so the user still gets usable text.
            try
            {
                string bare = TagRx.Replace(html!, " ");
                return WebUtility.HtmlDecode(bare).Trim();
            }
            catch { return html ?? string.Empty; }
        }
    }

    private sealed class ListCtx
    {
        public bool Ordered;
        public int Index;
    }

    private static string ConvertCore(string html)
    {
        // Drop content of script/style/head entirely — noise, not prose.
        html = Regex.Replace(html, @"<script\b[^>]*>.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<style\b[^>]*>.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        html = Regex.Replace(html, @"<!--.*?-->", " ", RegexOptions.Singleline);

        var sb = new StringBuilder();
        var listStack = new Stack<ListCtx>();
        bool inPre = false;
        int i = 0;
        int n = html.Length;

        while (i < n)
        {
            char c = html[i];
            if (c == '<')
            {
                int end = html.IndexOf('>', i);
                if (end < 0)
                {
                    // Malformed trailing '<' — treat rest as text.
                    AppendText(sb, html.Substring(i), inPre);
                    break;
                }
                string tag = html.Substring(i, end - i + 1);
                HandleTag(tag, sb, listStack, ref inPre);
                i = end + 1;
            }
            else
            {
                int next = html.IndexOf('<', i);
                if (next < 0) next = n;
                AppendText(sb, html.Substring(i, next - i), inPre);
                i = next;
            }
        }

        string md = sb.ToString();
        md = BlankLinesRx.Replace(md, "\n\n");
        return md.Trim();
    }

    private static void HandleTag(string tag, StringBuilder sb, Stack<ListCtx> listStack, ref bool inPre)
    {
        bool closing = tag.Length > 1 && tag[1] == '/';
        // Extract bare tag name.
        string inner = tag.Substring(closing ? 2 : 1, tag.Length - (closing ? 2 : 1) - 1).Trim();
        if (inner.EndsWith("/")) inner = inner.Substring(0, inner.Length - 1).Trim();
        int sp = inner.IndexOfAny(new[] { ' ', '\t', '\r', '\n' });
        string name = (sp < 0 ? inner : inner.Substring(0, sp)).ToLowerInvariant();

        switch (name)
        {
            case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                if (closing) { sb.Append("\n\n"); }
                else { EnsureBlankLine(sb); sb.Append(new string('#', name[1] - '0')).Append(' '); }
                break;

            case "strong": case "b":
                sb.Append("**");
                break;

            case "em": case "i":
                sb.Append('*');
                break;

            case "code":
                if (!inPre) sb.Append('`');
                break;

            case "pre":
                if (closing) { inPre = false; EnsureNewLine(sb); sb.Append("```\n\n"); }
                else { EnsureBlankLine(sb); sb.Append("```\n"); inPre = true; }
                break;

            case "a":
                if (closing) sb.Append(']').Append(_pendingHref ?? "()");
                else { sb.Append('['); _pendingHref = ExtractHref(inner); }
                break;

            case "ul": case "ol":
                if (closing) { if (listStack.Count > 0) listStack.Pop(); if (listStack.Count == 0) sb.Append('\n'); }
                else { EnsureNewLine(sb); listStack.Push(new ListCtx { Ordered = name == "ol", Index = 0 }); }
                break;

            case "li":
                if (!closing)
                {
                    EnsureNewLine(sb);
                    int depth = Math.Max(0, listStack.Count - 1);
                    sb.Append(new string(' ', depth * 2));
                    if (listStack.Count > 0)
                    {
                        var ctx = listStack.Peek();
                        if (ctx.Ordered) { ctx.Index++; sb.Append(ctx.Index).Append(". "); }
                        else sb.Append("- ");
                    }
                    else sb.Append("- ");
                }
                else sb.Append('\n');
                break;

            case "blockquote":
                if (closing) sb.Append("\n\n");
                else { EnsureBlankLine(sb); sb.Append("> "); }
                break;

            case "hr":
                EnsureBlankLine(sb); sb.Append("---\n\n");
                break;

            case "br":
                sb.Append("  \n");
                break;

            case "p": case "div":
                if (closing) sb.Append("\n\n");
                else EnsureBlankLine(sb);
                break;

            default:
                // Unknown tag: strip, keep inner text (handled by the text pass).
                break;
        }
    }

    // Single-slot href carry between <a> open and close (converter is single-pass, non-nested links).
    private static string? _pendingHref;

    private static string ExtractHref(string inner)
    {
        foreach (Match m in AttrRx.Matches(inner))
        {
            if (string.Equals(m.Groups[1].Value, "href", StringComparison.OrdinalIgnoreCase))
            {
                string v = m.Groups[2].Value.Trim();
                if (v.Length >= 2 && (v[0] == '"' || v[0] == '\'')) v = v.Substring(1, v.Length - 2);
                return "(" + WebUtility.HtmlDecode(v).Trim() + ")";
            }
        }
        return "()";
    }

    private static void AppendText(StringBuilder sb, string raw, bool inPre)
    {
        if (raw.Length == 0) return;
        string text = WebUtility.HtmlDecode(raw);
        if (inPre)
        {
            sb.Append(text);
            return;
        }
        text = text.Replace("\r", " ").Replace("\n", " ");
        text = WsRx.Replace(text, " ");
        if (text.Length == 0) return;
        // Avoid a leading space right after a fresh block boundary.
        if (text == " " && (sb.Length == 0 || sb[sb.Length - 1] == '\n')) return;
        sb.Append(text);
    }

    private static void EnsureNewLine(StringBuilder sb)
    {
        if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');
    }

    private static void EnsureBlankLine(StringBuilder sb)
    {
        if (sb.Length == 0) return;
        if (sb[sb.Length - 1] != '\n') sb.Append("\n\n");
        else if (sb.Length >= 2 && sb[sb.Length - 2] != '\n') sb.Append('\n');
    }
}
