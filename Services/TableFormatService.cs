using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 表格排版 · Table formatter. Pure managed C#. Parses a delimited / Markdown / ASCII table into rows,
/// then re-emits it as an aligned monospace ASCII table or a Markdown table. Never throws — bad input
/// yields an empty grid or best-effort parse. No I/O, no process launch, no redirect.
/// </summary>
public static class TableFormatService
{
    public enum Delimiter { Auto, Comma, Tab, Pipe }
    public enum OutputFormat { Ascii, Markdown }
    public enum Align { Left, Right, Center }

    /// <summary>Parse raw text into a rectangular grid of cells. Ragged rows are padded to the widest row.</summary>
    public static List<List<string>> Parse(string? input, Delimiter delim)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(input)) return rows;

        try
        {
            var lines = input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            char ch = ResolveDelimiter(input, delim);

            foreach (var raw in lines)
            {
                var line = raw;
                if (line.Length == 0) continue;

                // Skip a Markdown/ASCII separator line (e.g. ---|--- or +----+----+ or |:---:|).
                if (IsSeparatorLine(line)) continue;

                // Strip a leading/trailing pipe border if the whole line is pipe-delimited & bordered.
                if (ch == '|')
                {
                    var t = line.Trim();
                    if (t.StartsWith("|", StringComparison.Ordinal)) t = t.Substring(1);
                    if (t.EndsWith("|", StringComparison.Ordinal)) t = t.Substring(0, t.Length - 1);
                    line = t;
                }

                rows.Add(SplitLine(line, ch));
            }

            // Normalise to a rectangle.
            int cols = 0;
            foreach (var r in rows) if (r.Count > cols) cols = r.Count;
            foreach (var r in rows) while (r.Count < cols) r.Add(string.Empty);
        }
        catch
        {
            return rows;
        }
        return rows;
    }

    private static char ResolveDelimiter(string input, Delimiter delim) => delim switch
    {
        Delimiter.Comma => ',',
        Delimiter.Tab => '\t',
        Delimiter.Pipe => '|',
        _ => AutoDetect(input),
    };

    private static char AutoDetect(string input)
    {
        // First non-empty line drives detection; prefer tab, then pipe, then comma.
        foreach (var raw in input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            if (raw.Length == 0 || IsSeparatorLine(raw)) continue;
            if (raw.Contains('\t')) return '\t';
            if (raw.Contains('|')) return '|';
            if (raw.Contains(',')) return ',';
            return ','; // single-column fallback
        }
        return ',';
    }

    private static bool IsSeparatorLine(string line)
    {
        var t = line.Trim();
        if (t.Length == 0) return false;
        bool sawDash = false;
        foreach (var c in t)
        {
            if (c == '-' || c == '=') sawDash = true;
            else if (c != '|' && c != '+' && c != ':' && c != ' ') return false;
        }
        return sawDash;
    }

    /// <summary>Split one line on a delimiter, tolerating double-quoted fields (RFC-ish; "" escapes a quote).</summary>
    private static List<string> SplitLine(string line, char delim)
    {
        var cells = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delim) { cells.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(c);
        }
        cells.Add(sb.ToString().Trim());
        return cells;
    }

    /// <summary>Render a grid. Never throws. Returns "" for an empty grid.</summary>
    public static string Render(List<List<string>> rows, OutputFormat format, Align align, bool firstRowHeader)
    {
        try
        {
            if (rows == null || rows.Count == 0) return string.Empty;

            int cols = 0;
            foreach (var r in rows) if (r.Count > cols) cols = r.Count;
            if (cols == 0) return string.Empty;

            var widths = new int[cols];
            foreach (var r in rows)
                for (int c = 0; c < cols; c++)
                {
                    int w = DisplayWidth(Cell(r, c));
                    if (w > widths[c]) widths[c] = w;
                }
            for (int c = 0; c < cols; c++) if (widths[c] < 3) widths[c] = 3; // readable minimum

            return format == OutputFormat.Markdown
                ? RenderMarkdown(rows, cols, widths, align, firstRowHeader)
                : RenderAscii(rows, cols, widths, align, firstRowHeader);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string RenderAscii(List<List<string>> rows, int cols, int[] widths, Align align, bool header)
    {
        string border = BuildBorder(cols, widths);
        var sb = new StringBuilder();
        sb.Append(border).Append('\n');
        for (int r = 0; r < rows.Count; r++)
        {
            sb.Append(BuildRow(rows[r], cols, widths, align)).Append('\n');
            if (header && r == 0) sb.Append(border).Append('\n');
        }
        sb.Append(border);
        return sb.ToString();
    }

    private static string BuildBorder(int cols, int[] widths)
    {
        var sb = new StringBuilder();
        sb.Append('+');
        for (int c = 0; c < cols; c++) { sb.Append(new string('-', widths[c] + 2)).Append('+'); }
        return sb.ToString();
    }

    private static string BuildRow(List<string> row, int cols, int[] widths, Align align)
    {
        var sb = new StringBuilder();
        sb.Append('|');
        for (int c = 0; c < cols; c++) sb.Append(' ').Append(Pad(Cell(row, c), widths[c], align)).Append(" |");
        return sb.ToString();
    }

    private static string RenderMarkdown(List<List<string>> rows, int cols, int[] widths, Align align, bool header)
    {
        var sb = new StringBuilder();
        // Markdown wants a header row; if none requested, synthesise blank headers.
        int start = 0;
        List<string> headerRow;
        if (header && rows.Count > 0) { headerRow = rows[0]; start = 1; }
        else { headerRow = new List<string>(); for (int c = 0; c < cols; c++) headerRow.Add(string.Empty); }

        sb.Append(MdRow(headerRow, cols, widths, align)).Append('\n');
        sb.Append(MdSeparator(cols, widths, align)).Append('\n');
        for (int r = start; r < rows.Count; r++)
        {
            sb.Append(MdRow(rows[r], cols, widths, align));
            if (r < rows.Count - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string MdRow(List<string> row, int cols, int[] widths, Align align)
    {
        var sb = new StringBuilder("|");
        for (int c = 0; c < cols; c++)
            sb.Append(' ').Append(Pad(EscapeMd(Cell(row, c)), widths[c], align)).Append(" |");
        return sb.ToString();
    }

    private static string MdSeparator(int cols, int[] widths, Align align)
    {
        var sb = new StringBuilder("|");
        for (int c = 0; c < cols; c++)
        {
            int w = widths[c];
            string dashes;
            switch (align)
            {
                case Align.Right: dashes = new string('-', Math.Max(1, w + 1)) + ":"; break;
                case Align.Center: dashes = ":" + new string('-', Math.Max(1, w)) + ":"; break;
                default: dashes = ":" + new string('-', Math.Max(1, w + 1)); break;
            }
            sb.Append(' ').Append(dashes).Append(' ').Append('|');
        }
        return sb.ToString();
    }

    private static string EscapeMd(string s) => s.Replace("|", "\\|");

    private static string Cell(List<string> row, int c) => (row != null && c < row.Count) ? (row[c] ?? string.Empty) : string.Empty;

    private static string Pad(string s, int width, Align align)
    {
        s ??= string.Empty;
        int len = DisplayWidth(s);
        int gap = width - len;
        if (gap <= 0) return s;
        switch (align)
        {
            case Align.Right: return new string(' ', gap) + s;
            case Align.Center:
                int left = gap / 2, right = gap - left;
                return new string(' ', left) + s + new string(' ', right);
            default: return s + new string(' ', gap);
        }
    }

    /// <summary>Monospace display width: CJK / full-width code points count as 2 columns.</summary>
    private static int DisplayWidth(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        int w = 0;
        var en = StringInfo.GetTextElementEnumerator(s);
        while (en.MoveNext())
        {
            string el = (string)en.Current;
            if (el.Length == 0) continue;
            w += IsWide(char.ConvertToUtf32(el, 0)) ? 2 : 1;
        }
        return w;
    }

    private static bool IsWide(int cp) =>
        (cp >= 0x1100 && cp <= 0x115F) ||   // Hangul Jamo
        (cp >= 0x2E80 && cp <= 0x303E) ||   // CJK radicals / Kangxi
        (cp >= 0x3041 && cp <= 0x33FF) ||   // Hiragana … CJK symbols
        (cp >= 0x3400 && cp <= 0x4DBF) ||   // CJK Ext A
        (cp >= 0x4E00 && cp <= 0x9FFF) ||   // CJK Unified
        (cp >= 0xA000 && cp <= 0xA4CF) ||   // Yi
        (cp >= 0xAC00 && cp <= 0xD7A3) ||   // Hangul syllables
        (cp >= 0xF900 && cp <= 0xFAFF) ||   // CJK compatibility
        (cp >= 0xFE30 && cp <= 0xFE4F) ||   // CJK compatibility forms
        (cp >= 0xFF00 && cp <= 0xFF60) ||   // Full-width forms
        (cp >= 0xFFE0 && cp <= 0xFFE6) ||
        (cp >= 0x20000 && cp <= 0x3FFFD);   // CJK Ext B+
}
