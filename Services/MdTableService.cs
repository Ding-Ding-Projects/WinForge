using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// Markdown 表格產生器 · Markdown table generator — pure managed, never throws.
/// Parses a delimited grid (CSV/TSV/pipe/auto) into rows/cells, and renders a
/// GitHub-flavored Markdown table with per-column alignment and optional
/// space-padding for pretty raw source. Also parses an existing Markdown table
/// (reverse mode) so it can be re-aligned/reformatted or emitted as CSV/TSV.
/// </summary>
public static class MdTableService
{
    public enum Delimiter { Comma, Tab, Pipe, Auto }

    /// <summary>Per-column alignment for the Markdown separator row.</summary>
    public enum Align { None, Left, Center, Right }

    /// <summary>A parsed grid: a list of rows, each row a list of string cells.</summary>
    public sealed class Grid
    {
        public List<List<string>> Rows { get; } = new();
        public int RowCount => Rows.Count;
        public int ColCount => Rows.Count == 0 ? 0 : Rows.Max(r => r.Count);
    }

    // ---- Parsing -----------------------------------------------------------

    /// <summary>Resolve an <see cref="Delimiter"/> against a sample of text.</summary>
    public static char ResolveDelimiter(Delimiter d, string sample)
    {
        switch (d)
        {
            case Delimiter.Comma: return ',';
            case Delimiter.Tab: return '\t';
            case Delimiter.Pipe: return '|';
            default:
                // Auto: pick the delimiter with the most occurrences on the first non-empty line.
                string first = (sample ?? string.Empty)
                    .Replace("\r\n", "\n").Replace('\r', '\n')
                    .Split('\n').FirstOrDefault(l => l.Trim().Length > 0) ?? string.Empty;
                int tabs = first.Count(c => c == '\t');
                int pipes = first.Count(c => c == '|');
                int commas = first.Count(c => c == ',');
                if (tabs >= pipes && tabs >= commas && tabs > 0) return '\t';
                if (pipes >= commas && pipes > 0) return '|';
                return ',';
        }
    }

    /// <summary>
    /// Parse a delimited grid. Comma delimiter honours RFC-4180 quoting;
    /// other delimiters split plainly and trim. Never throws.
    /// </summary>
    public static Grid ParseDelimited(string text, char delimiter)
    {
        var grid = new Grid();
        if (string.IsNullOrEmpty(text)) return grid;
        try
        {
            string norm = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (delimiter == ',')
            {
                foreach (var line in norm.Split('\n'))
                {
                    if (line.Length == 0) continue;
                    grid.Rows.Add(SplitCsvLine(line, delimiter));
                }
            }
            else
            {
                foreach (var line in norm.Split('\n'))
                {
                    if (line.Trim().Length == 0) continue;
                    var cells = line.Split(delimiter).Select(c => c.Trim()).ToList();
                    // A leading/trailing pipe (markdown-ish paste) yields empty edge cells — drop them.
                    if (delimiter == '|')
                    {
                        if (cells.Count > 0 && cells[0].Length == 0) cells.RemoveAt(0);
                        if (cells.Count > 0 && cells[^1].Length == 0) cells.RemoveAt(cells.Count - 1);
                    }
                    grid.Rows.Add(cells);
                }
            }
        }
        catch { /* never throw */ }
        return grid;
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
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
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == delimiter) { cells.Add(sb.ToString().Trim()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        cells.Add(sb.ToString().Trim());
        return cells;
    }

    /// <summary>
    /// Detect whether text looks like an existing Markdown table (has a
    /// separator row of dashes/colons under the header). Never throws.
    /// </summary>
    public static bool LooksLikeMarkdownTable(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        try
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n').Where(l => l.Trim().Length > 0).ToList();
            if (lines.Count < 2) return false;
            return IsSeparatorRow(lines[1]);
        }
        catch { return false; }
    }

    private static bool IsSeparatorRow(string line)
    {
        string t = line.Trim().Trim('|').Trim();
        if (t.Length == 0) return false;
        foreach (var cell in t.Split('|'))
        {
            string s = cell.Trim();
            if (s.Length == 0) return false;
            foreach (char c in s)
                if (c != '-' && c != ':' && c != ' ') return false;
            if (!s.Contains('-')) return false;
        }
        return true;
    }

    /// <summary>
    /// Parse an existing Markdown table into a <see cref="Grid"/> (header + body,
    /// separator row dropped) plus the per-column alignments it declared.
    /// Never throws.
    /// </summary>
    public static (Grid grid, List<Align> aligns) ParseMarkdown(string text)
    {
        var grid = new Grid();
        var aligns = new List<Align>();
        if (string.IsNullOrWhiteSpace(text)) return (grid, aligns);
        try
        {
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n')
                .Split('\n').Where(l => l.Trim().Length > 0).ToList();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i == 1 && IsSeparatorRow(lines[i]))
                {
                    aligns = ParseAlignRow(lines[i]);
                    continue;
                }
                grid.Rows.Add(SplitMarkdownRow(lines[i]));
            }
        }
        catch { /* never throw */ }
        return (grid, aligns);
    }

    private static List<string> SplitMarkdownRow(string line)
    {
        string t = line.Trim();
        if (t.StartsWith("|")) t = t.Substring(1);
        if (t.EndsWith("|")) t = t.Substring(0, t.Length - 1);
        // Split on unescaped pipes.
        var cells = new List<string>();
        var sb = new StringBuilder();
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (c == '\\' && i + 1 < t.Length && t[i + 1] == '|') { sb.Append('|'); i++; }
            else if (c == '|') { cells.Add(sb.ToString().Trim()); sb.Clear(); }
            else sb.Append(c);
        }
        cells.Add(sb.ToString().Trim());
        return cells;
    }

    private static List<Align> ParseAlignRow(string line)
    {
        var aligns = new List<Align>();
        string t = line.Trim().Trim('|');
        foreach (var raw in t.Split('|'))
        {
            string s = raw.Trim();
            bool l = s.StartsWith(":");
            bool r = s.EndsWith(":");
            aligns.Add(l && r ? Align.Center : r ? Align.Right : l ? Align.Left : Align.None);
        }
        return aligns;
    }

    // ---- Rendering ---------------------------------------------------------

    /// <summary>
    /// Render a Grid as a GitHub-flavored Markdown table. When
    /// <paramref name="firstRowHeader"/> is false a synthetic "Column N" header is
    /// generated. <paramref name="pad"/> space-pads cells for pretty raw source.
    /// Never throws.
    /// </summary>
    public static string RenderMarkdown(Grid grid, IList<Align> aligns, bool firstRowHeader, bool pad)
    {
        try
        {
            if (grid == null || grid.RowCount == 0) return string.Empty;
            int cols = grid.ColCount;
            if (cols == 0) return string.Empty;

            // Normalize rows to equal column count and escape pipes.
            var rows = grid.Rows.Select(r =>
            {
                var cells = new List<string>(r.Select(Escape));
                while (cells.Count < cols) cells.Add(string.Empty);
                return cells;
            }).ToList();

            List<string> header;
            List<List<string>> body;
            if (firstRowHeader)
            {
                header = rows[0];
                body = rows.Skip(1).ToList();
            }
            else
            {
                header = Enumerable.Range(1, cols)
                    .Select(i => "Column " + i.ToString(CultureInfo.InvariantCulture)).ToList();
                body = rows;
            }

            Align AlignAt(int i) => aligns != null && i < aligns.Count ? aligns[i] : Align.None;

            var sb = new StringBuilder();
            if (pad)
            {
                // Compute a display width per column across header + body + separator minimum (3).
                int[] w = new int[cols];
                for (int i = 0; i < cols; i++) w[i] = Math.Max(3, i < header.Count ? header[i].Length : 0);
                foreach (var row in body)
                    for (int i = 0; i < cols && i < row.Count; i++)
                        w[i] = Math.Max(w[i], row[i].Length);

                sb.Append(RenderPaddedRow(header, w, AlignAt, cols));
                sb.Append(RenderPaddedSeparator(w, AlignAt, cols));
                foreach (var row in body) sb.Append(RenderPaddedRow(row, w, AlignAt, cols));
            }
            else
            {
                sb.Append(RenderPlainRow(header, cols));
                sb.Append(RenderPlainSeparator(AlignAt, cols));
                foreach (var row in body) sb.Append(RenderPlainRow(row, cols));
            }
            return sb.ToString().TrimEnd('\n');
        }
        catch { return string.Empty; }
    }

    private static string Escape(string s) => (s ?? string.Empty).Replace("|", "\\|").Replace("\n", " ").Trim();

    private static string RenderPlainRow(IList<string> row, int cols)
    {
        var sb = new StringBuilder("|");
        for (int i = 0; i < cols; i++)
        {
            sb.Append(' ').Append(i < row.Count ? row[i] : string.Empty).Append(" |");
        }
        return sb.Append('\n').ToString();
    }

    private static string RenderPlainSeparator(Func<int, Align> alignAt, int cols)
    {
        var sb = new StringBuilder("|");
        for (int i = 0; i < cols; i++) sb.Append(' ').Append(SepToken(alignAt(i), 3)).Append(" |");
        return sb.Append('\n').ToString();
    }

    private static string RenderPaddedRow(IList<string> row, int[] w, Func<int, Align> alignAt, int cols)
    {
        var sb = new StringBuilder("|");
        for (int i = 0; i < cols; i++)
        {
            string cell = i < row.Count ? row[i] : string.Empty;
            sb.Append(' ').Append(PadCell(cell, w[i], alignAt(i))).Append(" |");
        }
        return sb.Append('\n').ToString();
    }

    private static string RenderPaddedSeparator(int[] w, Func<int, Align> alignAt, int cols)
    {
        var sb = new StringBuilder("|");
        for (int i = 0; i < cols; i++) sb.Append(' ').Append(SepToken(alignAt(i), w[i])).Append(" |");
        return sb.Append('\n').ToString();
    }

    private static string PadCell(string cell, int width, Align a)
    {
        cell ??= string.Empty;
        int gap = width - cell.Length;
        if (gap <= 0) return cell;
        switch (a)
        {
            case Align.Right: return new string(' ', gap) + cell;
            case Align.Center:
                int left = gap / 2;
                return new string(' ', left) + cell + new string(' ', gap - left);
            default: return cell + new string(' ', gap);
        }
    }

    private static string SepToken(Align a, int width)
    {
        width = Math.Max(3, width);
        switch (a)
        {
            case Align.Left: return ":" + new string('-', Math.Max(2, width - 1));
            case Align.Right: return new string('-', Math.Max(2, width - 1)) + ":";
            case Align.Center: return ":" + new string('-', Math.Max(1, width - 2)) + ":";
            default: return new string('-', width);
        }
    }

    /// <summary>Render a Grid as CSV or TSV. Never throws.</summary>
    public static string RenderDelimited(Grid grid, char delimiter)
    {
        try
        {
            if (grid == null || grid.RowCount == 0) return string.Empty;
            var sb = new StringBuilder();
            foreach (var row in grid.Rows)
                sb.Append(string.Join(delimiter, row.Select(c => QuoteIfNeeded(c, delimiter)))).Append('\n');
            return sb.ToString().TrimEnd('\n');
        }
        catch { return string.Empty; }
    }

    private static string QuoteIfNeeded(string s, char delimiter)
    {
        s ??= string.Empty;
        if (delimiter == ',' && (s.Contains(',') || s.Contains('"') || s.Contains('\n')))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
