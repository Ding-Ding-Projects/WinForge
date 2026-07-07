using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 欄位文字工具 · Column text tools — split delimited text into a rectangular grid, then extract / delete /
/// reorder / align / transpose / trim columns and re-join. Pure managed, ragged rows are padded, never throws.
/// </summary>
public static class TextColumnsService
{
    public enum Delimiter { Tab, Comma, Pipe, Semicolon, Whitespace }

    private static char SepChar(Delimiter d) => d switch
    {
        Delimiter.Tab => '\t',
        Delimiter.Comma => ',',
        Delimiter.Pipe => '|',
        Delimiter.Semicolon => ';',
        _ => ' '
    };

    /// <summary>Join a row back with the chosen delimiter (Whitespace joins with a single space).</summary>
    public static string JoinSep(Delimiter d) => d == Delimiter.Whitespace ? " " : SepChar(d).ToString();

    /// <summary>Split input into a rectangular (padded) grid of cells. Never throws.</summary>
    public static List<List<string>> Parse(string? input, Delimiter d)
    {
        var rows = new List<List<string>>();
        if (string.IsNullOrEmpty(input)) return rows;

        string[] lines = input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        foreach (var line in lines)
        {
            List<string> cells;
            if (d == Delimiter.Whitespace)
                cells = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
            else
                cells = line.Split(SepChar(d)).ToList();
            rows.Add(cells);
        }

        // Drop a single trailing empty line (common when text ends with a newline).
        if (rows.Count > 1 && rows[^1].Count == 1 && rows[^1][0].Length == 0)
            rows.RemoveAt(rows.Count - 1);

        return Pad(rows);
    }

    private static List<List<string>> Pad(List<List<string>> rows)
    {
        int width = 0;
        foreach (var r in rows) width = Math.Max(width, r.Count);
        foreach (var r in rows)
            while (r.Count < width) r.Add(string.Empty);
        return rows;
    }

    private static int Width(List<List<string>> rows) => rows.Count == 0 ? 0 : rows[0].Count;

    /// <summary>Join a grid back into text using the output delimiter.</summary>
    public static string Render(List<List<string>> rows, Delimiter outDelim)
    {
        string sep = JoinSep(outDelim);
        var sb = new StringBuilder();
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) sb.Append('\n');
            sb.Append(string.Join(sep, rows[i]));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Parse a 1-based index/range spec like "1,3,5-7" into distinct 0-based indices, in the given order.
    /// Returns false with a reason when a token is malformed or out of range.
    /// </summary>
    public static bool ParseIndexSpec(string? spec, int width, out List<int> indices, out string? bad)
    {
        indices = new List<int>();
        bad = null;
        if (string.IsNullOrWhiteSpace(spec)) { bad = ""; return false; }

        foreach (var raw in spec.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.Contains('-'))
            {
                var parts = token.Split('-');
                if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out int a) || !int.TryParse(parts[1].Trim(), out int b))
                { bad = token; return false; }
                if (a < 1 || b < 1 || a > width || b > width) { bad = token; return false; }
                if (a <= b) for (int i = a; i <= b; i++) indices.Add(i - 1);
                else for (int i = a; i >= b; i--) indices.Add(i - 1);
            }
            else
            {
                if (!int.TryParse(token, out int n)) { bad = token; return false; }
                if (n < 1 || n > width) { bad = token; return false; }
                indices.Add(n - 1);
            }
        }
        return indices.Count > 0;
    }

    public static List<List<string>> ExtractColumns(List<List<string>> rows, List<int> cols)
    {
        var outRows = new List<List<string>>();
        foreach (var r in rows)
        {
            var nr = new List<string>(cols.Count);
            foreach (int c in cols) nr.Add(c >= 0 && c < r.Count ? r[c] : string.Empty);
            outRows.Add(nr);
        }
        return outRows;
    }

    public static List<List<string>> DeleteColumns(List<List<string>> rows, List<int> cols)
    {
        var drop = new HashSet<int>(cols);
        int width = Width(rows);
        var keep = new List<int>();
        for (int c = 0; c < width; c++) if (!drop.Contains(c)) keep.Add(c);
        return ExtractColumns(rows, keep);
    }

    /// <summary>Reorder columns; the spec is the new order. Columns omitted from the spec are dropped.</summary>
    public static List<List<string>> Reorder(List<List<string>> rows, List<int> order) => ExtractColumns(rows, order);

    /// <summary>Pad every cell to its column's max display width so columns line up (left-aligned).</summary>
    public static List<List<string>> Align(List<List<string>> rows)
    {
        int width = Width(rows);
        var maxes = new int[width];
        foreach (var r in rows)
            for (int c = 0; c < width; c++) maxes[c] = Math.Max(maxes[c], r[c]?.Length ?? 0);

        var outRows = new List<List<string>>();
        foreach (var r in rows)
        {
            var nr = new List<string>(width);
            for (int c = 0; c < width; c++)
            {
                string cell = r[c] ?? string.Empty;
                nr.Add(cell.PadRight(maxes[c]));
            }
            outRows.Add(nr);
        }
        return outRows;
    }

    /// <summary>Swap rows and columns.</summary>
    public static List<List<string>> Transpose(List<List<string>> rows)
    {
        int width = Width(rows);
        var outRows = new List<List<string>>();
        for (int c = 0; c < width; c++)
        {
            var nr = new List<string>(rows.Count);
            foreach (var r in rows) nr.Add(r[c] ?? string.Empty);
            outRows.Add(nr);
        }
        return outRows;
    }

    /// <summary>Trim leading/trailing whitespace from every cell.</summary>
    public static List<List<string>> TrimCells(List<List<string>> rows)
    {
        foreach (var r in rows)
            for (int c = 0; c < r.Count; c++) r[c] = (r[c] ?? string.Empty).Trim();
        return rows;
    }
}
