using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.Services;

/// <summary>
/// TOML ↔ JSON 轉換器（純手寫，唔用 NuGet）· Hand-written TOML ↔ JSON converter.
/// Implements a practical TOML subset: comments, key = value, dotted keys, tables [a.b],
/// array-of-tables [[a]], basic/literal strings, integers (with _ / hex / oct / bin),
/// floats, booleans, arrays (nested/mixed → JSON arrays) and inline tables { a = 1 }.
/// Datetimes are preserved as strings. Never throws — returns (ok, output, error).
/// </summary>
public static class TomlJsonService
{
    public readonly record struct Result(bool Ok, string Output, string? Error);

    // ---------------------------------------------------------------- TOML → JSON

    public static Result TomlToJson(string toml)
    {
        try
        {
            JsonObject root = ParseToml(toml ?? string.Empty);
            var opts = new JsonSerializerOptions { WriteIndented = true };
            return new Result(true, root.ToJsonString(opts), null);
        }
        catch (TomlException tex)
        {
            return new Result(false, string.Empty, tex.Message);
        }
        catch (Exception ex)
        {
            return new Result(false, string.Empty, ex.Message);
        }
    }

    // ---------------------------------------------------------------- JSON → TOML

    public static Result JsonToToml(string json)
    {
        try
        {
            JsonNode? node;
            try
            {
                node = JsonNode.Parse(json ?? string.Empty,
                    documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            }
            catch (JsonException jex)
            {
                return new Result(false, string.Empty, Loc.I.Pick("Invalid JSON: ", "JSON 唔正確：") + jex.Message);
            }

            if (node is not JsonObject obj)
                return new Result(false, string.Empty,
                    Loc.I.Pick("Top-level JSON must be an object to become TOML.", "最上層 JSON 要係物件先可以變 TOML。"));

            var sb = new StringBuilder();
            WriteTable(sb, obj, string.Empty);
            return new Result(true, sb.ToString().TrimEnd() + "\n", null);
        }
        catch (Exception ex)
        {
            return new Result(false, string.Empty, ex.Message);
        }
    }

    // ================================================================ TOML parser

    private sealed class TomlException : Exception
    {
        public TomlException(string message) : base(message) { }
    }

    private static JsonObject ParseToml(string text)
    {
        var root = new JsonObject();
        JsonObject current = root;                       // current table for bare keys
        var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            var p = new Cursor(raw);
            p.SkipWs();
            if (p.Eol || p.Peek() == '#') continue;      // blank or comment line

            char c = p.Peek();
            if (c == '[')
            {
                bool arrayOfTables = p.Pos + 1 < raw.Length && raw[p.Pos + 1] == '[';
                p.Next(); if (arrayOfTables) p.Next();
                var path = ReadKeyPath(p, ']');
                p.SkipWs();
                if (p.Eol || p.Peek() != ']') throw Err(i, "unterminated table header");
                p.Next();
                if (arrayOfTables)
                {
                    p.SkipWs();
                    if (p.Eol || p.Peek() != ']') throw Err(i, "unterminated array-of-tables header");
                    p.Next();
                }
                EnsureTrailing(p, i);
                current = arrayOfTables ? DescendArrayOfTables(root, path, i) : DescendTable(root, path, i);
            }
            else
            {
                var path = ReadKeyPath(p, '=');
                p.SkipWs();
                if (p.Eol || p.Peek() != '=') throw Err(i, "expected '=' after key");
                p.Next();
                p.SkipWs();
                JsonNode? value = ReadValue(p, lines, ref i);
                EnsureTrailing(p, i);
                AssignDotted(current, path, value, i);
            }
        }
        return root;
    }

    private static void EnsureTrailing(Cursor p, int line)
    {
        p.SkipWs();
        if (!p.Eol && p.Peek() != '#') throw Err(line, "unexpected trailing content");
    }

    private static TomlException Err(int line, string msg) =>
        new(Loc.I.Pick($"TOML error (line {line + 1}): {msg}", $"TOML 錯誤（第 {line + 1} 行）：{msg}"));

    // ---- key paths (dotted, quoted segments allowed) ----

    private static List<string> ReadKeyPath(Cursor p, char terminator)
    {
        var parts = new List<string>();
        while (true)
        {
            p.SkipWs();
            if (p.Eol) throw new TomlException(Loc.I.Pick("TOML error: unexpected end of key.", "TOML 錯誤：鍵突然完咗。"));
            char c = p.Peek();
            string part;
            if (c == '"') part = ReadBasicString(p);
            else if (c == '\'') part = ReadLiteralString(p);
            else part = ReadBareKey(p);
            parts.Add(part);
            p.SkipWs();
            if (!p.Eol && p.Peek() == '.') { p.Next(); continue; }
            break;
        }
        return parts;
    }

    private static string ReadBareKey(Cursor p)
    {
        int start = p.Pos;
        while (!p.Eol)
        {
            char c = p.Peek();
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-') p.Next();
            else break;
        }
        if (p.Pos == start) throw new TomlException(Loc.I.Pick("TOML error: empty key.", "TOML 錯誤：鍵係空。"));
        return p.Slice(start);
    }

    // ---- table descent ----

    private static JsonObject DescendTable(JsonObject root, List<string> path, int line)
    {
        JsonObject cur = root;
        foreach (var key in path)
        {
            if (cur[key] is JsonObject o) cur = o;
            else if (cur[key] is JsonArray arr && arr.Count > 0 && arr[^1] is JsonObject last) cur = last;
            else if (cur[key] is null) { var n = new JsonObject(); cur[key] = n; cur = n; }
            else throw Err(line, $"key '{key}' already has a non-table value");
        }
        return cur;
    }

    private static JsonObject DescendArrayOfTables(JsonObject root, List<string> path, int line)
    {
        JsonObject cur = root;
        for (int k = 0; k < path.Count - 1; k++)
        {
            string key = path[k];
            if (cur[key] is JsonObject o) cur = o;
            else if (cur[key] is JsonArray arr && arr.Count > 0 && arr[^1] is JsonObject last) cur = last;
            else if (cur[key] is null) { var n = new JsonObject(); cur[key] = n; cur = n; }
            else throw Err(line, $"key '{key}' already has a non-table value");
        }
        string leaf = path[^1];
        if (cur[leaf] is null) cur[leaf] = new JsonArray();
        if (cur[leaf] is not JsonArray targetArr)
            throw Err(line, $"key '{leaf}' is not an array of tables");
        var entry = new JsonObject();
        targetArr.Add(entry);
        return entry;
    }

    private static void AssignDotted(JsonObject table, List<string> path, JsonNode? value, int line)
    {
        JsonObject cur = table;
        for (int k = 0; k < path.Count - 1; k++)
        {
            string key = path[k];
            if (cur[key] is JsonObject o) cur = o;
            else if (cur[key] is null) { var n = new JsonObject(); cur[key] = n; cur = n; }
            else throw Err(line, $"key '{key}' already has a non-table value");
        }
        string leaf = path[^1];
        if (cur.ContainsKey(leaf)) throw Err(line, $"duplicate key '{leaf}'");
        cur[leaf] = value;
    }

    // ---- values ----

    private static JsonNode? ReadValue(Cursor p, string[] lines, ref int lineIdx)
    {
        p.SkipWs();
        if (p.Eol) throw Err(lineIdx, "missing value");
        char c = p.Peek();

        if (c == '"')
        {
            if (p.StartsWith("\"\"\"")) return JsonValue.Create(ReadMultilineBasic(p, lines, ref lineIdx));
            return JsonValue.Create(ReadBasicString(p));
        }
        if (c == '\'')
        {
            if (p.StartsWith("'''")) return JsonValue.Create(ReadMultilineLiteral(p, lines, ref lineIdx));
            return JsonValue.Create(ReadLiteralString(p));
        }
        if (c == '[') return ReadArray(p, lines, ref lineIdx);
        if (c == '{') return ReadInlineTable(p, lines, ref lineIdx);
        return ReadScalar(p, lineIdx);
    }

    private static JsonNode? ReadScalar(Cursor p, int line)
    {
        int start = p.Pos;
        while (!p.Eol)
        {
            char c = p.Peek();
            if (c == ',' || c == ']' || c == '}' || c == '#') break;
            p.Next();
        }
        string tok = p.Slice(start).Trim();
        if (tok.Length == 0) throw Err(line, "empty value");

        if (tok == "true") return JsonValue.Create(true);
        if (tok == "false") return JsonValue.Create(false);

        // Datetime heuristic → keep as string. Contains a ':' time or a date dash pattern with 'T'/space.
        if (LooksLikeDateTime(tok)) return JsonValue.Create(tok);

        // floats: inf / nan
        if (tok is "inf" or "+inf") return JsonValue.Create(double.PositiveInfinity);
        if (tok is "-inf") return JsonValue.Create(double.NegativeInfinity);
        if (tok is "nan" or "+nan" or "-nan") return JsonValue.Create(double.NaN);

        string noUnderscore = tok.Replace("_", "");

        // radixed integers
        if (noUnderscore.StartsWith("0x") || noUnderscore.StartsWith("0X"))
            return ParseRadix(noUnderscore[2..], 16, line, tok);
        if (noUnderscore.StartsWith("0o") || noUnderscore.StartsWith("0O"))
            return ParseRadix(noUnderscore[2..], 8, line, tok);
        if (noUnderscore.StartsWith("0b") || noUnderscore.StartsWith("0B"))
            return ParseRadix(noUnderscore[2..], 2, line, tok);

        bool looksFloat = noUnderscore.IndexOf('.') >= 0 ||
                          noUnderscore.IndexOf('e') >= 0 || noUnderscore.IndexOf('E') >= 0;
        if (!looksFloat && long.TryParse(noUnderscore, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l))
            return JsonValue.Create(l);
        if (double.TryParse(noUnderscore, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return JsonValue.Create(d);

        throw Err(line, $"could not parse value '{tok}'");
    }

    private static JsonNode ParseRadix(string digits, int radix, int line, string tok)
    {
        try
        {
            long v = Convert.ToInt64(digits, radix);
            return JsonValue.Create(v);
        }
        catch
        {
            throw Err(line, $"invalid number '{tok}'");
        }
    }

    private static bool LooksLikeDateTime(string tok)
    {
        // Date: 1979-05-27 ; Time: 07:32:00 ; combined with T or space.
        // Guard: don't treat plain negative integers like -5 as dates.
        int dashes = 0, colons = 0;
        foreach (char c in tok) { if (c == '-') dashes++; else if (c == ':') colons++; }
        if (colons >= 1) return true;                                  // has a time component
        if (dashes >= 2 && tok.Length >= 8 && char.IsDigit(tok[0])) return true; // full date
        return false;
    }

    private static JsonArray ReadArray(Cursor p, string[] lines, ref int lineIdx)
    {
        var arr = new JsonArray();
        p.Next(); // '['
        while (true)
        {
            SkipArrayFiller(p, lines, ref lineIdx);
            if (p.Eol) throw Err(lineIdx, "unterminated array");
            if (p.Peek() == ']') { p.Next(); break; }
            JsonNode? v = ReadValue(p, lines, ref lineIdx);
            arr.Add(v);
            SkipArrayFiller(p, lines, ref lineIdx);
            if (p.Eol) throw Err(lineIdx, "unterminated array");
            char c = p.Peek();
            if (c == ',') { p.Next(); continue; }
            if (c == ']') { p.Next(); break; }
            throw Err(lineIdx, "expected ',' or ']' in array");
        }
        return arr;
    }

    // In arrays whitespace, newlines and comments may separate elements.
    private static void SkipArrayFiller(Cursor p, string[] lines, ref int lineIdx)
    {
        while (true)
        {
            p.SkipWs();
            if (!p.Eol && p.Peek() == '#') { p.ToEnd(); }
            if (p.Eol)
            {
                if (lineIdx + 1 >= lines.Length) return;
                lineIdx++;
                p.Reset(lines[lineIdx]);
                continue;
            }
            return;
        }
    }

    private static JsonObject ReadInlineTable(Cursor p, string[] lines, ref int lineIdx)
    {
        var obj = new JsonObject();
        p.Next(); // '{'
        p.SkipWs();
        if (!p.Eol && p.Peek() == '}') { p.Next(); return obj; }
        while (true)
        {
            p.SkipWs();
            var path = ReadKeyPath(p, '=');
            p.SkipWs();
            if (p.Eol || p.Peek() != '=') throw Err(lineIdx, "expected '=' in inline table");
            p.Next();
            p.SkipWs();
            JsonNode? v = ReadValue(p, lines, ref lineIdx);
            AssignDotted(obj, path, v, lineIdx);
            p.SkipWs();
            if (p.Eol) throw Err(lineIdx, "unterminated inline table");
            char c = p.Peek();
            if (c == ',') { p.Next(); continue; }
            if (c == '}') { p.Next(); break; }
            throw Err(lineIdx, "expected ',' or '}' in inline table");
        }
        return obj;
    }

    // ---- strings ----

    private static string ReadBasicString(Cursor p)
    {
        p.Next(); // opening "
        var sb = new StringBuilder();
        while (!p.Eol)
        {
            char c = p.Next();
            if (c == '"') return sb.ToString();
            if (c == '\\')
            {
                if (p.Eol) break;
                char e = p.Next();
                switch (e)
                {
                    case 'b': sb.Append('\b'); break;
                    case 't': sb.Append('\t'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'r': sb.Append('\r'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'u': sb.Append(ReadUnicode(p, 4)); break;
                    case 'U': sb.Append(ReadUnicode(p, 8)); break;
                    default: sb.Append(e); break;
                }
            }
            else sb.Append(c);
        }
        throw new TomlException(Loc.I.Pick("TOML error: unterminated string.", "TOML 錯誤：字串未閉合。"));
    }

    private static string ReadUnicode(Cursor p, int n)
    {
        var hex = new StringBuilder();
        for (int i = 0; i < n && !p.Eol; i++) hex.Append(p.Next());
        if (int.TryParse(hex.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
            return char.ConvertFromUtf32(code);
        return string.Empty;
    }

    private static string ReadLiteralString(Cursor p)
    {
        p.Next(); // opening '
        int start = p.Pos;
        while (!p.Eol)
        {
            if (p.Peek() == '\'') { string s = p.Slice(start); p.Next(); return s; }
            p.Next();
        }
        throw new TomlException(Loc.I.Pick("TOML error: unterminated literal string.", "TOML 錯誤：文字字串未閉合。"));
    }

    private static string ReadMultilineBasic(Cursor p, string[] lines, ref int lineIdx)
    {
        p.Skip(3); // """
        var sb = new StringBuilder();
        // A newline immediately after the opening delimiter is trimmed.
        bool firstChunk = true;
        while (true)
        {
            if (p.Eol)
            {
                if (lineIdx + 1 >= lines.Length) throw new TomlException(Loc.I.Pick("TOML error: unterminated multiline string.", "TOML 錯誤：多行字串未閉合。"));
                lineIdx++;
                p.Reset(lines[lineIdx]);
                if (!firstChunk) sb.Append('\n');
                firstChunk = false;
                continue;
            }
            firstChunk = false;
            if (p.StartsWith("\"\"\"")) { p.Skip(3); break; }
            char c = p.Next();
            if (c == '\\')
            {
                if (p.Eol) { /* line-ending backslash: trim following whitespace/newlines */
                    while (lineIdx + 1 < lines.Length) { lineIdx++; p.Reset(lines[lineIdx]); p.SkipWs(); if (!p.Eol) break; }
                    continue;
                }
                char e = p.Next();
                switch (e)
                {
                    case 'b': sb.Append('\b'); break;
                    case 't': sb.Append('\t'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'r': sb.Append('\r'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'u': sb.Append(ReadUnicode(p, 4)); break;
                    case 'U': sb.Append(ReadUnicode(p, 8)); break;
                    default: sb.Append(e); break;
                }
            }
            else sb.Append(c);
        }
        string result = sb.ToString();
        if (result.StartsWith("\n")) result = result[1..];
        return result;
    }

    private static string ReadMultilineLiteral(Cursor p, string[] lines, ref int lineIdx)
    {
        p.Skip(3); // '''
        var sb = new StringBuilder();
        bool firstChunk = true;
        while (true)
        {
            if (p.Eol)
            {
                if (lineIdx + 1 >= lines.Length) throw new TomlException(Loc.I.Pick("TOML error: unterminated multiline literal.", "TOML 錯誤：多行文字字串未閉合。"));
                lineIdx++;
                p.Reset(lines[lineIdx]);
                if (!firstChunk) sb.Append('\n');
                firstChunk = false;
                continue;
            }
            firstChunk = false;
            if (p.StartsWith("'''")) { p.Skip(3); break; }
            sb.Append(p.Next());
        }
        string result = sb.ToString();
        if (result.StartsWith("\n")) result = result[1..];
        return result;
    }

    // A tiny line cursor.
    private sealed class Cursor
    {
        private string _s;
        public int Pos;
        public Cursor(string s) { _s = s; Pos = 0; }
        public void Reset(string s) { _s = s; Pos = 0; }
        public bool Eol => Pos >= _s.Length;
        public char Peek() => _s[Pos];
        public char Next() => _s[Pos++];
        public void Skip(int n) => Pos += n;
        public void ToEnd() => Pos = _s.Length;
        public void SkipWs() { while (Pos < _s.Length && (_s[Pos] == ' ' || _s[Pos] == '\t')) Pos++; }
        public string Slice(int start) => _s.Substring(start, Pos - start);
        public bool StartsWith(string tok) => Pos + tok.Length <= _s.Length && _s.Substring(Pos, tok.Length) == tok;
    }

    // ================================================================ TOML writer

    private static void WriteTable(StringBuilder sb, JsonObject obj, string prefix)
    {
        // First emit scalar / inline values, then nested tables & arrays-of-tables.
        var subTables = new List<KeyValuePair<string, JsonObject>>();
        var tableArrays = new List<KeyValuePair<string, JsonArray>>();

        foreach (var kv in obj)
        {
            switch (kv.Value)
            {
                case JsonObject childObj:
                    subTables.Add(new(kv.Key, childObj));
                    break;
                case JsonArray arr when IsArrayOfTables(arr):
                    tableArrays.Add(new(kv.Key, arr));
                    break;
                default:
                    sb.Append(FormatKey(kv.Key)).Append(" = ").Append(FormatValue(kv.Value)).Append('\n');
                    break;
            }
        }

        foreach (var kv in subTables)
        {
            string path = prefix.Length == 0 ? FormatKey(kv.Key) : prefix + "." + FormatKey(kv.Key);
            sb.Append('\n').Append('[').Append(path).Append(']').Append('\n');
            WriteTable(sb, kv.Value, path);
        }

        foreach (var kv in tableArrays)
        {
            string path = prefix.Length == 0 ? FormatKey(kv.Key) : prefix + "." + FormatKey(kv.Key);
            foreach (var item in kv.Value)
            {
                sb.Append('\n').Append("[[").Append(path).Append("]]").Append('\n');
                WriteTable(sb, (JsonObject)item!, path);
            }
        }
    }

    private static bool IsArrayOfTables(JsonArray arr)
    {
        if (arr.Count == 0) return false;
        foreach (var item in arr) if (item is not JsonObject) return false;
        return true;
    }

    private static string FormatKey(string key)
    {
        bool bare = key.Length > 0;
        foreach (char c in key)
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) { bare = false; break; }
        return bare ? key : "\"" + EscapeBasic(key) + "\"";
    }

    private static string FormatValue(JsonNode? node)
    {
        switch (node)
        {
            case null:
                return "\"\""; // TOML has no null; represent as empty string
            case JsonArray arr:
            {
                var sb = new StringBuilder("[");
                for (int i = 0; i < arr.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    if (arr[i] is JsonObject o) sb.Append(FormatInlineTable(o));
                    else sb.Append(FormatValue(arr[i]));
                }
                sb.Append(']');
                return sb.ToString();
            }
            case JsonObject o:
                return FormatInlineTable(o);
            case JsonValue val:
                return FormatScalar(val);
            default:
                return "\"\"";
        }
    }

    private static string FormatInlineTable(JsonObject o)
    {
        var sb = new StringBuilder("{ ");
        bool first = true;
        foreach (var kv in o)
        {
            if (!first) sb.Append(", ");
            first = false;
            sb.Append(FormatKey(kv.Key)).Append(" = ").Append(FormatValue(kv.Value));
        }
        sb.Append(" }");
        if (first) return "{}";
        return sb.ToString();
    }

    private static string FormatScalar(JsonValue val)
    {
        if (val.TryGetValue<bool>(out bool b)) return b ? "true" : "false";
        if (val.TryGetValue<long>(out long l)) return l.ToString(CultureInfo.InvariantCulture);
        if (val.TryGetValue<int>(out int iv)) return iv.ToString(CultureInfo.InvariantCulture);
        if (val.TryGetValue<double>(out double d))
        {
            if (double.IsNaN(d)) return "nan";
            if (double.IsPositiveInfinity(d)) return "inf";
            if (double.IsNegativeInfinity(d)) return "-inf";
            string s = d.ToString("R", CultureInfo.InvariantCulture);
            if (s.IndexOf('.') < 0 && s.IndexOf('e') < 0 && s.IndexOf('E') < 0) s += ".0";
            return s;
        }
        if (val.TryGetValue<string>(out string? str)) return "\"" + EscapeBasic(str ?? string.Empty) + "\"";
        // Fallback: use the raw JSON text.
        return "\"" + EscapeBasic(val.ToJsonString().Trim('"')) + "\"";
    }

    private static string EscapeBasic(string s)
    {
        var sb = new StringBuilder(s.Length + 2);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\b': sb.Append("\\b"); break;
                case '\t': sb.Append("\\t"); break;
                case '\n': sb.Append("\\n"); break;
                case '\f': sb.Append("\\f"); break;
                case '\r': sb.Append("\\r"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }
}
