using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// CSV ⇄ JSON 轉換 · Pure-managed CSV/JSON converter. RFC 4180-correct CSV parsing
/// (quoted fields, escaped "" quotes, embedded delimiters &amp; newlines) and
/// System.Text.Json pretty output. No shelling out, no third-party libraries.
/// </summary>
public static class CsvJsonService
{
    /// <summary>Result of a conversion — text plus row/column counts (or an error message).</summary>
    public readonly struct Result
    {
        public readonly string Output;
        public readonly int Rows;
        public readonly int Cols;
        public readonly string? Error; // null on success

        private Result(string output, int rows, int cols, string? error)
        {
            Output = output; Rows = rows; Cols = cols; Error = error;
        }

        public static Result Ok(string output, int rows, int cols) => new(output, rows, cols, null);
        public static Result Fail(string error) => new(string.Empty, 0, 0, error);
    }

    // ---------------- CSV → JSON ----------------

    /// <summary>Parse CSV then emit pretty JSON. header=true → array of objects; false → array of arrays.</summary>
    public static Result CsvToJson(string csv, char delimiter, bool header)
    {
        try
        {
            var rows = ParseCsv(csv ?? string.Empty, delimiter);
            if (rows.Count == 0)
                return Result.Ok("[]", 0, 0);

            var opts = new JsonWriterOptions { Indented = true };
            using var ms = new System.IO.MemoryStream();
            using (var w = new Utf8JsonWriter(ms, opts))
            {
                w.WriteStartArray();
                if (header)
                {
                    var keys = rows[0];
                    for (int r = 1; r < rows.Count; r++)
                    {
                        var row = rows[r];
                        w.WriteStartObject();
                        for (int c = 0; c < keys.Count; c++)
                        {
                            string key = keys[c] ?? string.Empty;
                            string val = c < row.Count ? row[c] : string.Empty;
                            w.WriteString(key, val);
                        }
                        w.WriteEndObject();
                    }
                }
                else
                {
                    foreach (var row in rows)
                    {
                        w.WriteStartArray();
                        foreach (var cell in row) w.WriteStringValue(cell);
                        w.WriteEndArray();
                    }
                }
                w.WriteEndArray();
            }

            string json = Encoding.UTF8.GetString(ms.ToArray());
            int dataRows = header ? Math.Max(0, rows.Count - 1) : rows.Count;
            int cols = 0;
            foreach (var row in rows) if (row.Count > cols) cols = row.Count;
            return Result.Ok(json, dataRows, cols);
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    // ---------------- JSON → CSV ----------------

    /// <summary>Convert a JSON array (of objects or of arrays) into delimited CSV text.</summary>
    public static Result JsonToCsv(string json, char delimiter, bool header)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json))
                return Result.Ok(string.Empty, 0, 0);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array)
                return Result.Fail("root-not-array");

            var items = new List<JsonElement>();
            foreach (var el in root.EnumerateArray()) items.Add(el);
            if (items.Count == 0)
                return Result.Ok(string.Empty, 0, 0);

            var sb = new StringBuilder();

            // Array of arrays → straight rows.
            if (items[0].ValueKind == JsonValueKind.Array)
            {
                int cols = 0, rows = 0;
                foreach (var arr in items)
                {
                    if (arr.ValueKind != JsonValueKind.Array)
                        return Result.Fail("mixed-array-shapes");
                    var cells = new List<string>();
                    foreach (var v in arr.EnumerateArray()) cells.Add(Scalar(v));
                    WriteRow(sb, cells, delimiter);
                    rows++;
                    if (cells.Count > cols) cols = cells.Count;
                }
                return Result.Ok(sb.ToString(), rows, cols);
            }

            // Array of objects → union of keys (in first-seen order) as header.
            var keys = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var obj in items)
            {
                if (obj.ValueKind != JsonValueKind.Object)
                    return Result.Fail("mixed-array-shapes");
                foreach (var prop in obj.EnumerateObject())
                    if (seen.Add(prop.Name)) keys.Add(prop.Name);
            }

            if (header)
                WriteRow(sb, keys, delimiter);

            foreach (var obj in items)
            {
                var cells = new List<string>(keys.Count);
                foreach (var k in keys)
                    cells.Add(obj.TryGetProperty(k, out var v) ? Scalar(v) : string.Empty);
                WriteRow(sb, cells, delimiter);
            }

            return Result.Ok(sb.ToString(), items.Count, keys.Count);
        }
        catch (JsonException jx)
        {
            return Result.Fail(jx.Message);
        }
        catch (Exception ex)
        {
            return Result.Fail(ex.Message);
        }
    }

    // ---------------- helpers ----------------

    /// <summary>Best-effort delimiter sniff over the first non-empty line.</summary>
    public static char DetectDelimiter(string csv)
    {
        if (string.IsNullOrEmpty(csv)) return ',';
        // Look at the first line, ignoring anything inside quotes.
        var line = new StringBuilder();
        bool inQuotes = false;
        foreach (char ch in csv)
        {
            if (ch == '"') inQuotes = !inQuotes;
            if ((ch == '\n' || ch == '\r') && !inQuotes) { if (line.Length > 0) break; else continue; }
            line.Append(ch);
        }
        string s = line.ToString();
        char best = ','; int bestCount = -1;
        foreach (char d in new[] { ',', '\t', ';', '|' })
        {
            int count = 0; bool q = false;
            foreach (char ch in s)
            {
                if (ch == '"') q = !q;
                else if (ch == d && !q) count++;
            }
            if (count > bestCount) { bestCount = count; best = d; }
        }
        return best;
    }

    /// <summary>RFC 4180 CSV parser. Handles quoted fields, "" escapes and newlines inside quotes.</summary>
    private static List<List<string>> ParseCsv(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        bool fieldStarted = false; // did the current row start any field content?
        int i = 0;
        int n = text.Length;

        void EndField()
        {
            row.Add(field.ToString());
            field.Clear();
            fieldStarted = true;
        }
        void EndRow()
        {
            EndField();
            rows.Add(row);
            row = new List<string>();
            fieldStarted = false;
        }

        while (i < n)
        {
            char ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < n && text[i + 1] == '"') { field.Append('"'); i += 2; continue; }
                    inQuotes = false; i++; continue;
                }
                field.Append(ch); i++; continue;
            }

            if (ch == '"') { inQuotes = true; fieldStarted = true; i++; continue; }
            if (ch == delimiter) { EndField(); i++; continue; }
            if (ch == '\r')
            {
                // swallow CRLF / lone CR as a row terminator
                if (i + 1 < n && text[i + 1] == '\n') i++;
                EndRow(); i++; continue;
            }
            if (ch == '\n') { EndRow(); i++; continue; }
            field.Append(ch); fieldStarted = true; i++; continue;
        }

        // flush trailing content (avoid emitting a spurious empty final row)
        if (fieldStarted || field.Length > 0 || row.Count > 0)
            EndRow();

        return rows;
    }

    /// <summary>Render one CSV row, quoting cells that need it, terminated by CRLF.</summary>
    private static void WriteRow(StringBuilder sb, List<string> cells, char delimiter)
    {
        for (int c = 0; c < cells.Count; c++)
        {
            if (c > 0) sb.Append(delimiter);
            sb.Append(Quote(cells[c] ?? string.Empty, delimiter));
        }
        sb.Append("\r\n");
    }

    private static string Quote(string s, char delimiter)
    {
        bool needs = s.IndexOf('"') >= 0 || s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0 || s.IndexOf(delimiter) >= 0;
        if (!needs) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>Flatten a JSON scalar (or nested value) to a CSV cell string.</summary>
    private static string Scalar(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? string.Empty,
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Undefined => string.Empty,
        // objects / nested arrays → compact JSON so nothing is silently dropped
        _ => v.GetRawText(),
    };
}
