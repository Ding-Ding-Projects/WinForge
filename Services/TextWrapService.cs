using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 文字換行／重排 · Text wrap / reflow — pure-managed, never-throwing string transforms.
/// Paragraphs are separated by blank lines; hard-wrap breaks lines at a column on word
/// boundaries; unwrap joins wrapped lines within a paragraph; reflow = unwrap then wrap.
/// Also prefix-each-line and hanging-indent helpers. No I/O, no side-effects.
/// </summary>
public static class TextWrapService
{
    private const int MinWidth = 1;
    private const int MaxWidth = 2000;

    private static int Clamp(int width) => width < MinWidth ? MinWidth : (width > MaxWidth ? MaxWidth : width);

    private static string[] SplitLines(string text)
        => (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

    /// <summary>Group the raw lines into paragraphs (runs of non-blank lines), preserving blank separators.</summary>
    private static List<List<string>> Paragraphs(string text)
    {
        var result = new List<List<string>>();
        var current = new List<string>();
        foreach (var line in SplitLines(text))
        {
            if (line.Trim().Length == 0)
            {
                result.Add(current);
                current = new List<string>();
            }
            else
            {
                current.Add(line);
            }
        }
        result.Add(current);
        return result;
    }

    /// <summary>Wrap a single already-joined line of text to <paramref name="width"/> columns on word boundaries.</summary>
    private static List<string> WrapOne(string text, int width, bool breakLongWords)
    {
        width = Clamp(width);
        var lines = new List<string>();
        if (text is null) return lines;

        var words = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        foreach (var raw in words)
        {
            var word = raw;
            // A word longer than the width: optionally chop it into width-sized pieces.
            if (breakLongWords && word.Length > width)
            {
                if (sb.Length > 0) { lines.Add(sb.ToString()); sb.Clear(); }
                while (word.Length > width)
                {
                    lines.Add(word.Substring(0, width));
                    word = word.Substring(width);
                }
                if (word.Length > 0) sb.Append(word);
                continue;
            }

            if (sb.Length == 0)
            {
                sb.Append(word);
            }
            else if (sb.Length + 1 + word.Length <= width)
            {
                sb.Append(' ').Append(word);
            }
            else
            {
                lines.Add(sb.ToString());
                sb.Clear();
                sb.Append(word);
            }
        }
        if (sb.Length > 0) lines.Add(sb.ToString());
        return lines;
    }

    /// <summary>Hard-wrap: break every paragraph's lines at <paramref name="width"/> columns, keeping paragraph breaks.</summary>
    public static string HardWrap(string text, int width, bool breakLongWords)
    {
        try
        {
            width = Clamp(width);
            var outParas = new List<string>();
            foreach (var para in Paragraphs(text))
            {
                if (para.Count == 0) { outParas.Add(string.Empty); continue; }
                var joined = string.Join(" ", TrimAll(para));
                outParas.Add(string.Join("\n", WrapOne(joined, width, breakLongWords)));
            }
            return string.Join("\n\n", outParas);
        }
        catch { return text ?? string.Empty; }
    }

    /// <summary>Unwrap: join the wrapped lines inside each paragraph into a single line; keep blank separators.</summary>
    public static string Unwrap(string text)
    {
        try
        {
            var outParas = new List<string>();
            foreach (var para in Paragraphs(text))
            {
                outParas.Add(para.Count == 0 ? string.Empty : string.Join(" ", TrimAll(para)));
            }
            return string.Join("\n\n", outParas);
        }
        catch { return text ?? string.Empty; }
    }

    /// <summary>Reflow: unwrap each paragraph then hard-wrap it to <paramref name="width"/>.</summary>
    public static string Reflow(string text, int width, bool breakLongWords)
        => HardWrap(Unwrap(text), width, breakLongWords);

    /// <summary>Prepend <paramref name="prefix"/> to every line (including blank lines).</summary>
    public static string AddPrefix(string text, string prefix)
    {
        try
        {
            prefix ??= string.Empty;
            var lines = SplitLines(text);
            for (int i = 0; i < lines.Length; i++) lines[i] = prefix + lines[i];
            return string.Join("\n", lines);
        }
        catch { return text ?? string.Empty; }
    }

    /// <summary>
    /// Hanging indent: first line of each paragraph flush left, continuation lines
    /// indented by <paramref name="spaces"/> spaces. Blank separators are preserved.
    /// </summary>
    public static string HangingIndent(string text, int spaces)
    {
        try
        {
            if (spaces < 0) spaces = 0;
            if (spaces > MaxWidth) spaces = MaxWidth;
            var pad = new string(' ', spaces);
            var outParas = new List<string>();
            foreach (var para in Paragraphs(text))
            {
                if (para.Count == 0) { outParas.Add(string.Empty); continue; }
                var lines = new List<string>();
                for (int i = 0; i < para.Count; i++)
                    lines.Add(i == 0 ? para[i] : pad + para[i]);
                outParas.Add(string.Join("\n", lines));
            }
            return string.Join("\n\n", outParas);
        }
        catch { return text ?? string.Empty; }
    }

    private static IEnumerable<string> TrimAll(List<string> lines)
    {
        foreach (var l in lines) yield return l.Trim();
    }
}
