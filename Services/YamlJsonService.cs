using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.Services;

/// <summary>
/// YAML ↔ JSON · A pure-managed, hand-written YAML subset converter. No NuGet.
/// Supports: nested mappings (2-space indent), sequences (- item), scalars (string / int / float /
/// bool true|false / null ~), single- and double-quoted strings, inline # comments, and (for
/// YAML→JSON) simple "- key: val" style inline maps. Emits pretty JSON one way, indented YAML the
/// other. Every entry point is robust and never throws — failures come back as a friendly message.
/// </summary>
public static class YamlJsonService
{
    /// <summary>Result of a conversion: either <see cref="Output"/> text or an <see cref="Error"/>.</summary>
    public sealed class ConvertResult
    {
        public bool Ok { get; init; }
        public string Output { get; init; } = "";
        public string? Error { get; init; }
        public static ConvertResult Success(string o) => new() { Ok = true, Output = o };
        public static ConvertResult Fail(string e) => new() { Ok = false, Error = e };
    }

    // ── JSON → YAML ─────────────────────────────────────────────────────────

    public static ConvertResult JsonToYaml(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ConvertResult.Fail("empty-json");
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = false },
                new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception ex)
        {
            return ConvertResult.Fail("invalid-json:" + ex.Message);
        }
        try
        {
            var sb = new StringBuilder();
            EmitYaml(root, sb, 0, isRoot: true);
            var text = sb.ToString().TrimEnd('\n');
            return ConvertResult.Success(text.Length == 0 ? "null" : text);
        }
        catch (Exception ex)
        {
            return ConvertResult.Fail("emit-failed:" + ex.Message);
        }
    }

    private static void EmitYaml(JsonNode? node, StringBuilder sb, int indent, bool isRoot)
    {
        string pad = new string(' ', indent * 2);
        switch (node)
        {
            case null:
                sb.Append(pad).Append("null\n");
                break;
            case JsonObject obj:
                if (Count(obj) == 0) { sb.Append(pad).Append("{}\n"); break; }
                foreach (var kv in obj)
                {
                    string key = EmitKey(kv.Key);
                    var child = kv.Value;
                    if (child is JsonObject co && Count(co) > 0)
                    {
                        sb.Append(pad).Append(key).Append(":\n");
                        EmitYaml(co, sb, indent + 1, false);
                    }
                    else if (child is JsonArray ca && ca.Count > 0)
                    {
                        sb.Append(pad).Append(key).Append(":\n");
                        EmitYaml(ca, sb, indent, false); // sequences align under key at same indent
                    }
                    else
                    {
                        sb.Append(pad).Append(key).Append(": ").Append(EmitScalar(child)).Append('\n');
                    }
                }
                break;
            case JsonArray arr:
                if (arr.Count == 0) { sb.Append(pad).Append("[]\n"); break; }
                foreach (var item in arr)
                {
                    if (item is JsonObject io && Count(io) > 0)
                    {
                        // "- key: val" first line then indented rest
                        var tmp = new StringBuilder();
                        EmitYaml(io, tmp, indent + 1, false);
                        var lines = tmp.ToString().TrimEnd('\n').Split('\n');
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (i == 0)
                                sb.Append(pad).Append("- ").Append(lines[i].TrimStart()).Append('\n');
                            else
                                sb.Append(lines[i]).Append('\n');
                        }
                    }
                    else if (item is JsonArray ia && ia.Count > 0)
                    {
                        sb.Append(pad).Append("-\n");
                        EmitYaml(ia, sb, indent + 1, false);
                    }
                    else
                    {
                        sb.Append(pad).Append("- ").Append(EmitScalar(item)).Append('\n');
                    }
                }
                break;
            default: // JsonValue scalar
                sb.Append(pad).Append(EmitScalar(node)).Append('\n');
                break;
        }
    }

    private static int Count(JsonObject o) { int n = 0; foreach (var _ in o) n++; return n; }

    private static string EmitKey(string key)
        => NeedsQuote(key) ? Quote(key) : key;

    private static string EmitScalar(JsonNode? node)
    {
        if (node is null) return "null";
        if (node is JsonObject) return "{}";
        if (node is JsonArray) return "[]";
        var val = node.AsValue();
        if (val.TryGetValue<bool>(out var b)) return b ? "true" : "false";
        if (val.TryGetValue<long>(out var l)) return l.ToString(CultureInfo.InvariantCulture);
        if (val.TryGetValue<double>(out var d) && node.GetValue<JsonElement>().ValueKind == JsonValueKind.Number)
            return d.ToString("R", CultureInfo.InvariantCulture);
        // Fall through as string
        string s;
        try { s = node.GetValue<string>(); }
        catch { s = node.ToJsonString().Trim('"'); }
        return NeedsQuote(s) ? Quote(s) : s;
    }

    private static bool NeedsQuote(string s)
    {
        if (s.Length == 0) return true;
        if (s != s.Trim()) return true;
        // Reserved / ambiguous scalars must be quoted so re-parse keeps them as strings
        string lower = s.ToLowerInvariant();
        if (lower is "true" or "false" or "null" or "~" or "yes" or "no") return true;
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) return true;
        foreach (char c in s)
            if (c is ':' or '#' or '-' or '{' or '}' or '[' or ']' or ',' or '&' or '*' or '!' or '|' or '>' or '\'' or '"' or '%' or '@' or '`' or '\n' or '\t')
                return true;
        char first = s[0];
        if (first is ' ' or '?' or '\'' or '"') return true;
        return false;
    }

    private static string Quote(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\r': sb.Append("\\r"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    // ── YAML → JSON ─────────────────────────────────────────────────────────

    public static ConvertResult YamlToJson(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
            return ConvertResult.Fail("empty-yaml");
        List<Line> lines;
        try
        {
            lines = Tokenize(yaml);
        }
        catch (Exception ex)
        {
            return ConvertResult.Fail("yaml-lex:" + ex.Message);
        }
        if (lines.Count == 0)
            return ConvertResult.Fail("empty-yaml");
        try
        {
            int idx = 0;
            var node = ParseBlock(lines, ref idx, lines[0].Indent);
            var opts = new JsonSerializerOptions { WriteIndented = true };
            string json = node is null ? "null" : node.ToJsonString(opts);
            return ConvertResult.Success(json);
        }
        catch (YamlError ye)
        {
            return ConvertResult.Fail("yaml-parse:" + ye.Message);
        }
        catch (Exception ex)
        {
            return ConvertResult.Fail("yaml-parse:" + ex.Message);
        }
    }

    private sealed class YamlError : Exception { public YamlError(string m) : base(m) { } }

    private readonly struct Line
    {
        public Line(int indent, string content, int number) { Indent = indent; Content = content; Number = number; }
        public int Indent { get; }
        public string Content { get; } // trimmed of leading indent + trailing space, comments stripped
        public int Number { get; }
    }

    private static List<Line> Tokenize(string yaml)
    {
        var result = new List<Line>();
        var raw = yaml.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < raw.Length; i++)
        {
            string line = raw[i];
            // strip document markers
            string t = line.TrimEnd();
            if (t.Trim() is "---" or "...") continue;
            int indent = 0;
            while (indent < line.Length && line[indent] == ' ') indent++;
            string body = line.Substring(indent);
            body = StripComment(body).TrimEnd();
            if (body.Length == 0) continue; // blank or comment-only
            result.Add(new Line(indent, body, i + 1));
        }
        return result;
    }

    // Remove an inline # comment not inside quotes.
    private static string StripComment(string s)
    {
        bool inS = false, inD = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\'' && !inD)
            {
                // YAML escapes a single quote inside a single-quoted scalar as ''.
                if (inS && i + 1 < s.Length && s[i + 1] == '\'') { i++; continue; }
                inS = !inS;
            }
            else if (c == '"' && !inS && !IsEscapedDoubleQuote(s, i)) inD = !inD;
            else if (c == '#' && !inS && !inD)
            {
                // '#' starts a comment only at start or after whitespace
                if (i == 0 || s[i - 1] == ' ' || s[i - 1] == '\t')
                    return s.Substring(0, i);
            }
        }
        return s;
    }

    private static JsonNode? ParseBlock(List<Line> lines, ref int idx, int indent)
    {
        if (idx >= lines.Count) return null;
        var line = lines[idx];
        if (line.Content.StartsWith("- ") || line.Content == "-")
            return ParseSequence(lines, ref idx, indent);
        if (FindColon(line.Content) >= 0)
            return ParseMapping(lines, ref idx, indent);

        // JsonToYaml emits a bare scalar for a root JSON scalar and {} / [] for
        // empty root collections. Treat that single line as a document value so
        // the converter can round-trip its own output instead of forcing it
        // through the mapping parser.
        idx++;
        var scalar = ParseScalar(line.Content);
        if (idx < lines.Count && lines[idx].Indent >= indent)
            throw new YamlError($"line {lines[idx].Number}: unexpected content after scalar value");
        return scalar;
    }

    private static JsonNode ParseMapping(List<Line> lines, ref int idx, int indent)
    {
        var obj = new JsonObject();
        while (idx < lines.Count)
        {
            var line = lines[idx];
            if (line.Indent < indent) break;
            if (line.Indent > indent)
                throw new YamlError($"line {line.Number}: unexpected extra indent");
            if (line.Content.StartsWith("- ") || line.Content == "-")
                throw new YamlError($"line {line.Number}: sequence item inside a mapping");

            var (key, rest) = SplitKey(line, line.Number);
            idx++;
            if (rest.Length > 0)
            {
                obj[key] = ParseScalar(rest);
            }
            else
            {
                // nested block on following deeper lines (mapping or sequence)
                if (idx < lines.Count && lines[idx].Indent > indent)
                {
                    obj[key] = ParseBlock(lines, ref idx, lines[idx].Indent);
                }
                else if (idx < lines.Count && lines[idx].Indent == indent &&
                         (lines[idx].Content.StartsWith("- ") || lines[idx].Content == "-"))
                {
                    // sequence aligned at same indent as the key (common YAML style)
                    obj[key] = ParseSequence(lines, ref idx, indent);
                }
                else
                {
                    obj[key] = null; // empty value
                }
            }
        }
        return obj;
    }

    private static JsonNode ParseSequence(List<Line> lines, ref int idx, int indent)
    {
        var arr = new JsonArray();
        while (idx < lines.Count)
        {
            var line = lines[idx];
            if (line.Indent < indent) break;
            if (line.Indent > indent) throw new YamlError($"line {line.Number}: unexpected extra indent in sequence");
            if (!(line.Content.StartsWith("- ") || line.Content == "-")) break;

            string after = line.Content == "-" ? "" : line.Content.Substring(2).Trim();
            if (after.Length == 0)
            {
                // nested block belongs to this item
                idx++;
                if (idx < lines.Count && lines[idx].Indent > indent)
                    arr.Add(ParseBlock(lines, ref idx, lines[idx].Indent));
                else
                    arr.Add((JsonNode?)null);
            }
            else if (LooksLikeInlineKey(after, out _))
            {
                // "- key: value" — synthesize a one-or-more-key mapping for this item
                int itemIndent = indent + 2; // conceptual indent of the map under the dash
                var obj = new JsonObject();
                // first key from the same physical line
                var firstLine = new Line(itemIndent, after, line.Number);
                var (k, rest) = SplitKey(firstLine, line.Number);
                idx++;
                if (rest.Length > 0)
                {
                    obj[k] = ParseScalar(rest);
                }
                else if (idx < lines.Count && lines[idx].Indent > indent)
                {
                    obj[k] = ParseBlock(lines, ref idx, lines[idx].Indent);
                }
                else obj[k] = null;

                // subsequent keys of the same inline map are indented deeper than the dash
                while (idx < lines.Count && lines[idx].Indent > indent &&
                       !(lines[idx].Content.StartsWith("- ") || lines[idx].Content == "-"))
                {
                    var kl = lines[idx];
                    var (k2, rest2) = SplitKey(kl, kl.Number);
                    idx++;
                    if (rest2.Length > 0) obj[k2] = ParseScalar(rest2);
                    else if (idx < lines.Count && lines[idx].Indent > kl.Indent)
                        obj[k2] = ParseBlock(lines, ref idx, lines[idx].Indent);
                    else obj[k2] = null;
                }
                arr.Add(obj);
            }
            else
            {
                arr.Add(ParseScalar(after));
                idx++;
            }
        }
        return arr;
    }

    private static bool LooksLikeInlineKey(string s, out string key)
    {
        key = "";
        int p = FindColon(s);
        if (p < 0) return false;
        // must be "key:" or "key: value"
        if (p + 1 < s.Length && s[p + 1] != ' ') return false;
        key = s.Substring(0, p).Trim();
        return key.Length > 0;
    }

    private static (string key, string rest) SplitKey(Line line, int number)
    {
        int p = FindColon(line.Content);
        if (p < 0) throw new YamlError($"line {number}: expected 'key: value'");
        string keyRaw = line.Content.Substring(0, p).Trim();
        string rest = line.Content.Substring(p + 1).Trim();
        if (keyRaw.Length == 0) throw new YamlError($"line {number}: empty key");
        string key = Unquote(keyRaw);
        return (key, rest);
    }

    // Colon that separates a mapping key (first ": " or trailing ":") outside quotes.
    private static int FindColon(string s)
    {
        bool inS = false, inD = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\'' && !inD)
            {
                if (inS && i + 1 < s.Length && s[i + 1] == '\'') { i++; continue; }
                inS = !inS;
            }
            else if (c == '"' && !inS && !IsEscapedDoubleQuote(s, i)) inD = !inD;
            else if (c == ':' && !inS && !inD)
            {
                if (i + 1 == s.Length || s[i + 1] == ' ') return i;
            }
        }
        return -1;
    }

    private static bool IsEscapedDoubleQuote(string s, int quoteIndex)
    {
        int slashCount = 0;
        for (int i = quoteIndex - 1; i >= 0 && s[i] == '\\'; i--) slashCount++;
        return (slashCount & 1) != 0;
    }

    private static string Unquote(string s)
    {
        if (s.Length >= 2 && s[0] == '"' && s[^1] == '"')
            return UnescapeDouble(s.Substring(1, s.Length - 2));
        if (s.Length >= 2 && s[0] == '\'' && s[^1] == '\'')
            return s.Substring(1, s.Length - 2).Replace("''", "'");
        return s;
    }

    private static string UnescapeDouble(string s)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n switch
                {
                    'n' => '\n', 't' => '\t', 'r' => '\r',
                    '"' => '"', '\\' => '\\', '0' => '\0', _ => n
                });
            }
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static JsonNode? ParseScalar(string raw)
    {
        string s = raw.Trim();
        if (s.Length == 0) return null;

        // Quoted → always string
        if ((s[0] == '"' && s.EndsWith("\"")) || (s[0] == '\'' && s.EndsWith("'")))
            return JsonValue.Create(Unquote(s));

        // Inline flow collections (best-effort): treat as JSON if it parses
        if ((s[0] == '[' && s.EndsWith("]")) || (s[0] == '{' && s.EndsWith("}")))
        {
            try { var n = JsonNode.Parse(s); if (n is not null) return n; } catch { /* fall through as string */ }
        }

        string lower = s.ToLowerInvariant();
        if (lower is "null" or "~") return null;
        if (lower is "true") return JsonValue.Create(true);
        if (lower is "false") return JsonValue.Create(false);

        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
            return JsonValue.Create(l);
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            return JsonValue.Create(d);

        return JsonValue.Create(Unquote(s));
    }

    /// <summary>Localized-neutral note describing subset limitations (UI joins with Loc).</summary>
    public static string LimitationsEn =>
        "Subset only: 2-space indent, block mappings & sequences, quoted/plain scalars, inline # comments, simple flow [a,b] / {k:v}. Anchors/aliases, tags, multi-doc & block scalars (| >) are not supported.";

    public static string LimitationsZh =>
        "只支援子集：2 格縮排、區塊映射同序列、有引號/無引號純量、行內 # 註解、簡單 flow [a,b] / {k:v}。唔支援 anchor/alias、tag、多文件同 block scalar（| >）。";
}
