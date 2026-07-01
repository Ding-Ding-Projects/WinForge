using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// HTML 表格轉換 · HTML table converter. Pure managed, tolerant, never-throws.
/// Data⇆HTML: CSV/TSV/pipe → clean &lt;table&gt; with thead/tbody; and an HTML &lt;table&gt;
/// → CSV / TSV / Markdown via a small regex/string parser (strip tags, decode a few entities).
/// </summary>
public static class HtmlTableService
{
    public enum Delimiter { Auto, Comma, Tab, Pipe }
    public enum OutFormat { Csv, Tsv, Markdown }

    public sealed class Grid
    {
        public List<string[]> Rows { get; } = new();
        public int RowCount => Rows.Count;
        public int ColCount
        {
            get
            {
                int max = 0;
                foreach (var r in Rows) if (r.Length > max) max = r.Length;
                return max;
            }
        }
    }

    // ---------- Data → HTML ----------

    /// <summary>Parse delimited text into a grid. Never throws.</summary>
    public static Grid ParseDelimited(string? text, Delimiter delim)
    {
        var g = new Grid();
        if (string.IsNullOrEmpty(text)) return g;
        try
        {
            var lines = SplitLines(text);
            char sep = ResolveDelimiter(lines, delim);
            foreach (var line in lines)
            {
                if (line.Length == 0) continue;
                g.Rows.Add(sep == ',' ? SplitCsvLine(line) : SplitSimple(line, sep));
            }
        }
        catch { /* tolerant */ }
        return g;
    }

    /// <summary>Build a clean HTML &lt;table&gt; from a grid.</summary>
    public static string ToHtml(Grid g, bool firstRowHeader, bool escapeCells, string? cssClass)
    {
        var sb = new StringBuilder();
        try
        {
            string cls = string.IsNullOrWhiteSpace(cssClass) ? "" : $" class=\"{EscAttr(cssClass!.Trim())}\"";
            sb.Append("<table").Append(cls).Append(">\n");

            int start = 0;
            if (firstRowHeader && g.Rows.Count > 0)
            {
                sb.Append("  <thead>\n    <tr>\n");
                foreach (var cell in g.Rows[0])
                    sb.Append("      <th>").Append(Cell(cell, escapeCells)).Append("</th>\n");
                sb.Append("    </tr>\n  </thead>\n");
                start = 1;
            }

            sb.Append("  <tbody>\n");
            for (int i = start; i < g.Rows.Count; i++)
            {
                sb.Append("    <tr>\n");
                foreach (var cell in g.Rows[i])
                    sb.Append("      <td>").Append(Cell(cell, escapeCells)).Append("</td>\n");
                sb.Append("    </tr>\n");
            }
            sb.Append("  </tbody>\n");
            sb.Append("</table>\n");
        }
        catch { /* tolerant */ }
        return sb.ToString();
    }

    // ---------- HTML → Data ----------

    private static readonly Regex RxTable = new(@"<table[^>]*>(?<body>.*?)</table>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex RxRow = new(@"<tr[^>]*>(?<row>.*?)</tr>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex RxCell = new(@"<(?<tag>th|td)[^>]*>(?<cell>.*?)</\k<tag>>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex RxAnyTag = new(@"<[^>]+>", RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>Parse an HTML fragment containing a &lt;table&gt; into a grid. Tolerant of malformed input.</summary>
    public static Grid ParseHtml(string? html)
    {
        var g = new Grid();
        if (string.IsNullOrEmpty(html)) return g;
        try
        {
            // Prefer the first <table>…</table>; otherwise fall back to scanning raw <tr> blocks.
            var tm = RxTable.Match(html);
            string scope = tm.Success ? tm.Groups["body"].Value : html;

            foreach (Match rm in RxRow.Matches(scope))
            {
                var cells = new List<string>();
                foreach (Match cm in RxCell.Matches(rm.Groups["row"].Value))
                    cells.Add(CleanCell(cm.Groups["cell"].Value));
                if (cells.Count > 0) g.Rows.Add(cells.ToArray());
            }
        }
        catch { /* tolerant */ }
        return g;
    }

    /// <summary>Render a grid as CSV / TSV / Markdown. Never throws.</summary>
    public static string ToData(Grid g, OutFormat fmt)
    {
        var sb = new StringBuilder();
        try
        {
            switch (fmt)
            {
                case OutFormat.Markdown:
                    RenderMarkdown(g, sb);
                    break;
                case OutFormat.Tsv:
                    RenderSeparated(g, '\t', sb);
                    break;
                default:
                    RenderCsv(g, sb);
                    break;
            }
        }
        catch { /* tolerant */ }
        return sb.ToString();
    }

    // ---------- helpers ----------

    private static void RenderCsv(Grid g, StringBuilder sb)
    {
        foreach (var row in g.Rows)
        {
            for (int i = 0; i < row.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(CsvField(row[i]));
            }
            sb.Append('\n');
        }
    }

    private static void RenderSeparated(Grid g, char sep, StringBuilder sb)
    {
        foreach (var row in g.Rows)
        {
            for (int i = 0; i < row.Length; i++)
            {
                if (i > 0) sb.Append(sep);
                sb.Append((row[i] ?? "").Replace('\t', ' ').Replace("\r", "").Replace("\n", " "));
            }
            sb.Append('\n');
        }
    }

    private static void RenderMarkdown(Grid g, StringBuilder sb)
    {
        if (g.Rows.Count == 0) return;
        int cols = g.ColCount;
        if (cols == 0) return;

        // header = first row, padded
        AppendMdRow(g.Rows[0], cols, sb);
        sb.Append('|');
        for (int c = 0; c < cols; c++) sb.Append(" --- |");
        sb.Append('\n');
        for (int r = 1; r < g.Rows.Count; r++)
            AppendMdRow(g.Rows[r], cols, sb);
    }

    private static void AppendMdRow(string[] row, int cols, StringBuilder sb)
    {
        sb.Append('|');
        for (int c = 0; c < cols; c++)
        {
            string v = c < row.Length ? (row[c] ?? "") : "";
            v = v.Replace("\r", "").Replace("\n", " ").Replace("|", "\\|");
            sb.Append(' ').Append(v).Append(" |");
        }
        sb.Append('\n');
    }

    private static string Cell(string? raw, bool escape)
        => escape ? EscHtml(raw ?? "") : (raw ?? "");

    private static string EscHtml(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
        .Replace("\"", "&quot;").Replace("'", "&#39;");

    private static string EscAttr(string s) => s.Replace("&", "&amp;").Replace("\"", "&quot;");

    private static string CsvField(string? s)
    {
        s ??= "";
        bool need = s.IndexOf(',') >= 0 || s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0;
        if (!need) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string CleanCell(string s)
    {
        // strip tags, collapse whitespace, decode a few common entities
        string noTags = RxAnyTag.Replace(s, " ");
        noTags = DecodeEntities(noTags);
        noTags = Regex.Replace(noTags, @"\s+", " ").Trim();
        return noTags;
    }

    private static string DecodeEntities(string s)
    {
        if (s.IndexOf('&') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '&')
            {
                int semi = s.IndexOf(';', i + 1, Math.Min(10, s.Length - i - 1));
                if (semi > i)
                {
                    string ent = s.Substring(i + 1, semi - i - 1);
                    string? rep = MapEntity(ent);
                    if (rep != null) { sb.Append(rep); i = semi; continue; }
                }
            }
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    private static string? MapEntity(string ent)
    {
        switch (ent)
        {
            case "amp": return "&";
            case "lt": return "<";
            case "gt": return ">";
            case "quot": return "\"";
            case "apos": return "'";
            case "nbsp": return " ";
            case "#39": return "'";
        }
        if (ent.Length > 1 && ent[0] == '#')
        {
            try
            {
                int code;
                if (ent[1] == 'x' || ent[1] == 'X')
                    code = int.Parse(ent.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                else
                    code = int.Parse(ent.Substring(1), CultureInfo.InvariantCulture);
                if (code > 0 && code <= 0x10FFFF) return char.ConvertFromUtf32(code);
            }
            catch { /* ignore bad numeric entity */ }
        }
        return null;
    }

    private static string[] SplitLines(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static char ResolveDelimiter(string[] lines, Delimiter delim)
    {
        switch (delim)
        {
            case Delimiter.Comma: return ',';
            case Delimiter.Tab: return '\t';
            case Delimiter.Pipe: return '|';
        }
        // Auto — count candidates on the first non-empty line, prefer the most frequent.
        string sample = "";
        foreach (var l in lines) { if (l.Length > 0) { sample = l; break; } }
        int tabs = Count(sample, '\t'), pipes = Count(sample, '|'), commas = Count(sample, ',');
        if (tabs >= pipes && tabs >= commas && tabs > 0) return '\t';
        if (pipes >= commas && pipes > 0) return '|';
        if (commas > 0) return ',';
        return tabs > 0 ? '\t' : ',';
    }

    private static int Count(string s, char c)
    {
        int n = 0;
        foreach (var ch in s) if (ch == c) n++;
        return n;
    }

    private static string[] SplitSimple(string line, char sep)
    {
        var parts = line.Split(sep);
        for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();
        return parts;
    }

    // Minimal RFC-4180-ish CSV line splitter (handles quotes and escaped quotes).
    private static string[] SplitCsvLine(string line)
    {
        var fields = new List<string>();
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
                else if (c == ',') { fields.Add(sb.ToString().Trim()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        fields.Add(sb.ToString().Trim());
        return fields.ToArray();
    }
}
