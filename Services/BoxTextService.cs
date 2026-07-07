using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 文字方框 / 橫幅 · Box &amp; banner text — wrap multiline text in a drawn border box using a
/// chosen style (ASCII, Single, Double, Rounded, Heavy, Stars, comment blocks). Pure managed,
/// never throws — bad input just yields an empty or best-effort result.
/// </summary>
public static class BoxTextService
{
    public enum BorderStyle { Ascii, Single, Double, Rounded, Heavy, Stars, CommentSlash, CommentHash }
    public enum Align { Left, Center, Right }

    private readonly struct Glyphs
    {
        public readonly char TL, TR, BL, BR, H, V;
        public Glyphs(char tl, char tr, char bl, char br, char h, char v)
        { TL = tl; TR = tr; BL = bl; BR = br; H = h; V = v; }
    }

    private static Glyphs GlyphsFor(BorderStyle s) => s switch
    {
        BorderStyle.Single => new Glyphs('┌', '┐', '└', '┘', '─', '│'),
        BorderStyle.Double => new Glyphs('╔', '╗', '╚', '╝', '═', '║'),
        BorderStyle.Rounded => new Glyphs('╭', '╮', '╰', '╯', '─', '│'),
        BorderStyle.Heavy => new Glyphs('┏', '┓', '┗', '┛', '━', '┃'),
        BorderStyle.Stars => new Glyphs('*', '*', '*', '*', '*', '*'),
        _ => new Glyphs('+', '+', '+', '+', '-', '|'), // Ascii
    };

    /// <summary>
    /// Render <paramref name="text"/> inside a box. Never throws; returns "" for a null/empty
    /// build failure only in truly exceptional cases.
    /// </summary>
    public static string Render(string? text, BorderStyle style, int padding, Align align, string? title)
    {
        try
        {
            padding = Math.Clamp(padding, 0, 40);
            title ??= string.Empty;
            title = title.Replace("\r", "").Replace("\n", " ").Trim();

            var lines = SplitLines(text);

            // Comment-block styles are handled separately (no box grid).
            if (style == BorderStyle.CommentSlash) return RenderCommentSlash(lines, title, padding);
            if (style == BorderStyle.CommentHash) return RenderCommentHash(lines, title, padding);

            var g = GlyphsFor(style);

            int longest = 0;
            foreach (var l in lines) longest = Math.Max(longest, DisplayWidth(l));
            // Ensure the title (if any) fits inside the top border too.
            int titleW = DisplayWidth(title);
            int inner = Math.Max(longest, titleW) + padding * 2;
            if (inner < 1) inner = 1;

            var sb = new StringBuilder();

            // Top border, optionally embedding a title: e.g. ┌─ Title ─────┐
            if (title.Length > 0)
                sb.Append(g.TL).Append(BuildTitleBar(g.H, inner, title)).Append(g.TR).Append('\n');
            else
                sb.Append(g.TL).Append(new string(g.H, inner)).Append(g.TR).Append('\n');

            // Body rows.
            foreach (var l in lines)
            {
                sb.Append(g.V);
                AppendAligned(sb, l, inner, padding, align);
                sb.Append(g.V).Append('\n');
            }

            // Bottom border.
            sb.Append(g.BL).Append(new string(g.H, inner)).Append(g.BR);
            return sb.ToString();
        }
        catch
        {
            return text ?? string.Empty;
        }
    }

    private static string BuildTitleBar(char h, int inner, string title)
    {
        // " Title " embedded after a single lead char, padded out with the horizontal glyph.
        string label = " " + title + " ";
        int labelW = DisplayWidth(label);
        if (labelW + 1 >= inner)
        {
            // Title too wide — just fill; the top border still closes correctly.
            return new string(h, Math.Max(inner, 1));
        }
        int lead = 1;
        int rest = inner - lead - labelW;
        if (rest < 0) rest = 0;
        return new string(h, lead) + label + new string(h, rest);
    }

    private static void AppendAligned(StringBuilder sb, string line, int inner, int padding, Align align)
    {
        int contentWidth = inner - padding * 2;
        if (contentWidth < 0) contentWidth = 0;
        int w = DisplayWidth(line);
        int slack = Math.Max(0, contentWidth - w);

        int left, right;
        switch (align)
        {
            case Align.Right: left = slack; right = 0; break;
            case Align.Center: left = slack / 2; right = slack - left; break;
            default: left = 0; right = slack; break;
        }

        sb.Append(' ', padding);
        sb.Append(' ', left);
        sb.Append(line);
        sb.Append(' ', right);
        sb.Append(' ', padding);
    }

    private static string RenderCommentSlash(List<string> lines, string title, int padding)
    {
        var sb = new StringBuilder();
        sb.Append("/*").Append('\n');
        if (title.Length > 0) sb.Append(" * ").Append(title).Append('\n').Append(" *").Append('\n');
        string pad = new string(' ', padding);
        foreach (var l in lines)
            sb.Append(" * ").Append(pad).Append(l).Append('\n');
        sb.Append(" */");
        return sb.ToString();
    }

    private static string RenderCommentHash(List<string> lines, string title, int padding)
    {
        int longest = DisplayWidth(title);
        foreach (var l in lines) longest = Math.Max(longest, DisplayWidth(l));
        int bar = Math.Max(3, longest + padding * 2 + 6);

        var sb = new StringBuilder();
        sb.Append(new string('#', bar)).Append('\n');
        if (title.Length > 0)
        {
            sb.Append("### ").Append(title).Append('\n');
            sb.Append(new string('#', bar)).Append('\n');
        }
        string pad = new string(' ', padding);
        foreach (var l in lines)
            sb.Append("### ").Append(pad).Append(l).Append('\n');
        sb.Append(new string('#', bar));
        return sb.ToString();
    }

    private static List<string> SplitLines(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(text)) { list.Add(""); return list; }
        string norm = text.Replace("\r\n", "\n").Replace("\r", "\n");
        foreach (var raw in norm.Split('\n'))
            list.Add(raw.Replace("\t", "    ")); // tabs → 4 spaces so width math stays sane
        if (list.Count == 0) list.Add("");
        return list;
    }

    /// <summary>
    /// Display width. Treats each char as width 1 (per spec), while skipping zero-width
    /// combining marks so accents don't over-count. Simple and never-throw.
    /// </summary>
    private static int DisplayWidth(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int w = 0;
        foreach (var ch in s)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark ||
                cat == System.Globalization.UnicodeCategory.EnclosingMark)
                continue; // zero-width
            w += 1;
        }
        return w;
    }
}
