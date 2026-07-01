using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// RFC 4180 CSV 檢查同修復 · Pure-managed RFC 4180 CSV linter + repairer. Detects ragged
/// rows, unbalanced quotes, stray CR/LF in unquoted fields, a UTF-8 BOM, trailing
/// whitespace, empty trailing lines and mixed line endings; then rewrites a compliant
/// document. Never throws — all public methods swallow and report via a status string.
/// </summary>
public static class CsvLintService
{
    public enum Severity { Info, Warning, Error }

    /// <summary>A single lint finding. Line is 1-based (0 = whole-document).</summary>
    public sealed class Issue
    {
        public Severity Level { get; init; }
        public int Line { get; init; }
        public string Message { get; init; } = string.Empty;

        // {Binding}-friendly (no x:Bind in DataTemplates).
        public string LevelText => Level.ToString();
        public string LineText => Line <= 0 ? "—" : Line.ToString();
    }

    public sealed class LintResult
    {
        public List<Issue> Issues { get; } = new();
        public int Rows { get; set; }
        public int Cols { get; set; }
        public bool HadBom { get; set; }
        public string? Error { get; set; }
        public int IssueCount => Issues.Count;
    }

    public sealed class RepairResult
    {
        public string Output { get; set; } = string.Empty;
        public int Rows { get; set; }
        public int Cols { get; set; }
        public int PaddedRows { get; set; }
        public int TruncatedRows { get; set; }
        public string? Error { get; set; }
    }

    private const char Bom = '﻿';

    /// <summary>Guess the most likely delimiter from a sample of the text.</summary>
    public static char DetectDelimiter(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return ',';
            // Count candidates on the first non-empty line only, ignoring quoted spans.
            char[] candidates = { ',', ';', '\t', '|' };
            var counts = new int[candidates.Length];
            bool inQuotes = false;
            foreach (char c in text)
            {
                if (c == '"') inQuotes = !inQuotes;
                else if (!inQuotes && (c == '\n' || c == '\r'))
                {
                    // Stop at the end of the first line if we already saw something.
                    bool any = false;
                    for (int i = 0; i < counts.Length; i++) if (counts[i] > 0) any = true;
                    if (any) break;
                }
                else if (!inQuotes)
                {
                    for (int i = 0; i < candidates.Length; i++)
                        if (c == candidates[i]) counts[i]++;
                }
            }
            int best = 0;
            for (int i = 1; i < counts.Length; i++) if (counts[i] > counts[best]) best = i;
            return counts[best] > 0 ? candidates[best] : ',';
        }
        catch { return ','; }
    }

    /// <summary>
    /// Parse into rows of fields, honouring RFC 4180 quoting. Returns false on a
    /// hard structural problem (unterminated quote) but still fills what it parsed.
    /// </summary>
    private static List<List<string>> Parse(string text, char delim, out bool unterminatedQuote)
    {
        unterminatedQuote = false;
        var rows = new List<List<string>>();
        var field = new StringBuilder();
        var row = new List<string>();
        bool inQuotes = false;
        bool fieldStarted = false;

        int n = text.Length;
        for (int i = 0; i < n; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < n && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    fieldStarted = true;
                }
                else if (c == delim)
                {
                    row.Add(field.ToString());
                    field.Clear();
                    fieldStarted = true;
                }
                else if (c == '\r')
                {
                    if (i + 1 < n && text[i + 1] == '\n') i++;
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    fieldStarted = false;
                }
                else if (c == '\n')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    rows.Add(row);
                    row = new List<string>();
                    fieldStarted = false;
                }
                else
                {
                    field.Append(c);
                    fieldStarted = true;
                }
            }
        }

        if (inQuotes) unterminatedQuote = true;

        // Flush the last field/row unless the file ended cleanly on a newline.
        if (fieldStarted || field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>Run all checks. Never throws; a caught exception lands in Error.</summary>
    public static LintResult Lint(string input, char delim)
    {
        var r = new LintResult();
        try
        {
            if (string.IsNullOrEmpty(input)) return r;

            // BOM.
            string text = input;
            if (text.Length > 0 && text[0] == Bom)
            {
                r.HadBom = true;
                r.Issues.Add(new Issue { Level = Severity.Warning, Line = 1,
                    Message = "UTF-8 BOM detected at the start of the file · 檔案開頭有 UTF-8 BOM" });
                text = text.Substring(1);
            }

            // Mixed line endings.
            int crlf = 0, lfOnly = 0, crOnly = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') { crlf++; i++; }
                    else crOnly++;
                }
                else if (text[i] == '\n') lfOnly++;
            }
            int styles = (crlf > 0 ? 1 : 0) + (lfOnly > 0 ? 1 : 0) + (crOnly > 0 ? 1 : 0);
            if (styles > 1)
                r.Issues.Add(new Issue { Level = Severity.Warning, Line = 0,
                    Message = $"Mixed line endings (CRLF×{crlf}, LF×{lfOnly}, CR×{crOnly}) · 換行符號唔一致" });

            // Line-level checks over raw physical lines.
            string[] lines = SplitPhysicalLines(text);
            int lastNonEmpty = -1;
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Length > 0) lastNonEmpty = i;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int lineNo = i + 1;

                // Trailing whitespace.
                if (line.Length > 0 && (line[line.Length - 1] == ' ' || line[line.Length - 1] == '\t'))
                    r.Issues.Add(new Issue { Level = Severity.Info, Line = lineNo,
                        Message = "Trailing whitespace · 尾部有多餘空白" });

                // Empty trailing lines (blank lines after the last content line).
                if (line.Length == 0 && i > lastNonEmpty && lastNonEmpty >= 0)
                    r.Issues.Add(new Issue { Level = Severity.Info, Line = lineNo,
                        Message = "Empty trailing line · 尾部空白行" });
            }

            // Unbalanced / unescaped quotes across the whole document.
            bool inQuotes = false;
            int quoteLine = 1;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\n') quoteLine++;
                if (c == '"')
                {
                    if (inQuotes && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
                    inQuotes = !inQuotes;
                }
            }
            if (inQuotes)
                r.Issues.Add(new Issue { Level = Severity.Error, Line = 0,
                    Message = "Unbalanced quotes — a \" is never closed · 引號唔對稱，有 \" 冇收尾" });

            // Structural parse for ragged rows + stray CR/LF in unquoted fields.
            var rows = Parse(text, delim, out bool unterminated);
            if (unterminated && !inQuotes)
                r.Issues.Add(new Issue { Level = Severity.Error, Line = 0,
                    Message = "Unterminated quoted field · 引號欄位未收尾" });

            if (rows.Count > 0)
            {
                int headerWidth = rows[0].Count;
                r.Cols = headerWidth;
                r.Rows = rows.Count;

                for (int ri = 0; ri < rows.Count; ri++)
                {
                    var fields = rows[ri];
                    if (fields.Count != headerWidth)
                        r.Issues.Add(new Issue { Level = Severity.Error, Line = ri + 1,
                            Message = $"Ragged row: {fields.Count} field(s), header has {headerWidth} · 欄位數目唔啱（{fields.Count} vs {headerWidth}）" });

                    foreach (var f in fields)
                        if (f.IndexOf('\r') >= 0 || (f.IndexOf('\n') >= 0))
                        {
                            // Only flagged when it survived parsing outside quotes; parser
                            // treats real newlines as row breaks, so any here means a bare
                            // CR embedded oddly.
                            r.Issues.Add(new Issue { Level = Severity.Warning, Line = ri + 1,
                                Message = "Stray CR/LF inside an unquoted field · 未加引號欄位入面有 CR/LF" });
                            break;
                        }
                }
            }

            return r;
        }
        catch (Exception ex)
        {
            r.Error = ex.Message;
            return r;
        }
    }

    /// <summary>
    /// Rewrite the document to be RFC 4180-compliant: strip BOM, quote fields that
    /// need it, double embedded quotes, normalise to CRLF, pad/truncate ragged rows
    /// to the header width. Never throws.
    /// </summary>
    public static RepairResult Repair(string input, char delim)
    {
        var res = new RepairResult();
        try
        {
            if (string.IsNullOrEmpty(input)) { res.Output = string.Empty; return res; }

            string text = input;
            if (text.Length > 0 && text[0] == Bom) text = text.Substring(1);

            var rows = Parse(text, delim, out _);

            // Drop empty trailing rows (a single empty field row is treated as blank).
            while (rows.Count > 0)
            {
                var last = rows[rows.Count - 1];
                if (last.Count == 1 && last[0].Length == 0) rows.RemoveAt(rows.Count - 1);
                else break;
            }

            if (rows.Count == 0) { res.Output = string.Empty; return res; }

            int width = rows[0].Count;
            res.Cols = width;
            var sb = new StringBuilder();

            for (int ri = 0; ri < rows.Count; ri++)
            {
                var fields = rows[ri];
                if (fields.Count < width)
                {
                    while (fields.Count < width) fields.Add(string.Empty);
                    res.PaddedRows++;
                }
                else if (fields.Count > width)
                {
                    fields = fields.GetRange(0, width);
                    res.TruncatedRows++;
                }

                for (int fi = 0; fi < fields.Count; fi++)
                {
                    if (fi > 0) sb.Append(delim);
                    sb.Append(EscapeField(fields[fi], delim));
                }
                sb.Append("\r\n");
            }

            res.Output = sb.ToString();
            res.Rows = rows.Count;
            return res;
        }
        catch (Exception ex)
        {
            res.Error = ex.Message;
            return res;
        }
    }

    private static string EscapeField(string field, char delim)
    {
        field ??= string.Empty;
        bool needsQuote = field.IndexOf('"') >= 0
                          || field.IndexOf(delim) >= 0
                          || field.IndexOf('\n') >= 0
                          || field.IndexOf('\r') >= 0;
        if (!needsQuote) return field;
        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    // Split into physical lines on CRLF / LF / lone CR, preserving no terminators.
    private static string[] SplitPhysicalLines(string text)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                list.Add(sb.ToString());
                sb.Clear();
            }
            else if (c == '\n')
            {
                list.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }
        list.Add(sb.ToString());
        return list.ToArray();
    }
}
