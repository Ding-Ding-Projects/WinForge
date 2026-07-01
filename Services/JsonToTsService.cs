using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// JSON → 型別（TypeScript interface / C# class）· Walks a JsonDocument and emits type
/// definitions. Pure managed (System.Text.Json). Robust: never throws — returns a bilingual
/// error message on invalid input. No redirect, no external tools.
/// </summary>
public static class JsonToTsService
{
    public enum Lang { TypeScript, CSharp }

    /// <summary>Result of a generate pass.</summary>
    public sealed class Result
    {
        public bool Ok { get; init; }
        public string Code { get; init; } = "";
        public string? ErrorEn { get; init; }
        public string? ErrorZh { get; init; }
        public int TypeCount { get; init; }
    }

    /// <summary>Generate type definitions from a JSON sample. Never throws.</summary>
    public static Result Generate(string? json, string? rootName, Lang lang)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new Result { Ok = false, ErrorEn = "Paste a JSON sample to begin.", ErrorZh = "貼一段 JSON 樣本先開始。" };

        rootName = Pascal(string.IsNullOrWhiteSpace(rootName) ? "Root" : rootName!);
        if (string.IsNullOrEmpty(rootName)) rootName = "Root";

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException jx)
        {
            return new Result
            {
                Ok = false,
                ErrorEn = "Invalid JSON: " + jx.Message,
                ErrorZh = "JSON 格式唔啱：" + jx.Message
            };
        }
        catch (Exception ex)
        {
            return new Result { Ok = false, ErrorEn = "Could not parse JSON: " + ex.Message, ErrorZh = "解析 JSON 失敗：" + ex.Message };
        }

        try
        {
            using (doc)
            {
                var root = doc.RootElement;
                // Unwrap a top-level array so a sample list still gives a useful element type.
                if (root.ValueKind == JsonValueKind.Array)
                    root = FirstObjectOrSelf(root);

                if (root.ValueKind != JsonValueKind.Object)
                    return new Result
                    {
                        Ok = false,
                        ErrorEn = "The sample must be a JSON object (or an array of objects) to generate types.",
                        ErrorZh = "樣本要係一個 JSON 物件（或者物件陣列）先可以生成型別。"
                    };

                var types = new List<(string Name, JsonElement Shape)>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var sb = new StringBuilder();

                // Emit the root, collecting nested object shapes as we go.
                var queue = new Queue<(string Name, JsonElement Shape)>();
                Enqueue(queue, seen, rootName, root);

                int count = 0;
                while (queue.Count > 0)
                {
                    var (name, shape) = queue.Dequeue();
                    if (count > 0) sb.Append('\n');
                    EmitType(sb, name, shape, lang, queue, seen);
                    count++;
                    if (count > 500) break; // pathological-input guard
                }

                return new Result { Ok = true, Code = sb.ToString().TrimEnd() + "\n", TypeCount = count };
            }
        }
        catch (Exception ex)
        {
            return new Result { Ok = false, ErrorEn = "Generation failed: " + ex.Message, ErrorZh = "生成失敗：" + ex.Message };
        }
    }

    private static void Enqueue(Queue<(string, JsonElement)> queue, HashSet<string> seen, string name, JsonElement shape)
    {
        if (seen.Add(name)) queue.Enqueue((name, shape));
    }

    private static void EmitType(StringBuilder sb, string name, JsonElement shape, Lang lang, Queue<(string, JsonElement)> queue, HashSet<string> seen)
    {
        if (lang == Lang.TypeScript)
        {
            sb.Append("export interface ").Append(name).Append(" {\n");
            foreach (var prop in shape.EnumerateObject())
            {
                bool optional = prop.Value.ValueKind == JsonValueKind.Null;
                string type = TypeOf(prop.Value, Pascal(prop.Name), lang, queue, seen);
                sb.Append("  ").Append(SafeMember(prop.Name, lang)).Append(optional ? "?: " : ": ").Append(type).Append(";\n");
            }
            sb.Append("}\n");
        }
        else
        {
            sb.Append("public class ").Append(name).Append("\n{\n");
            foreach (var prop in shape.EnumerateObject())
            {
                bool nullable = prop.Value.ValueKind == JsonValueKind.Null;
                string type = TypeOf(prop.Value, Pascal(prop.Name), lang, queue, seen);
                if (nullable && !type.EndsWith("?") && !type.EndsWith(">") && !type.EndsWith("[]")) type += "?";
                sb.Append("    public ").Append(type).Append(' ').Append(Pascal(prop.Name)).Append(" { get; set; }\n");
            }
            sb.Append("}\n");
        }
    }

    /// <summary>Map a value to its type name; enqueue nested object shapes for later emission.</summary>
    private static string TypeOf(JsonElement value, string suggestedName, Lang lang, Queue<(string, JsonElement)> queue, HashSet<string> seen)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                return lang == Lang.TypeScript ? "string" : "string";
            case JsonValueKind.Number:
                if (lang == Lang.TypeScript) return "number";
                return value.TryGetInt64(out _) ? "long" : "double";
            case JsonValueKind.True:
            case JsonValueKind.False:
                return lang == Lang.TypeScript ? "boolean" : "bool";
            case JsonValueKind.Null:
                return lang == Lang.TypeScript ? "any" : "object";
            case JsonValueKind.Object:
            {
                string tName = UniqueName(suggestedName, seen, queue, value);
                return tName;
            }
            case JsonValueKind.Array:
            {
                var elem = FirstOrDefault(value, out bool has);
                if (!has)
                    return lang == Lang.TypeScript ? "any[]" : "List<object>";
                string elemType;
                if (elem.ValueKind == JsonValueKind.Object)
                {
                    string singular = Singularize(suggestedName);
                    elemType = UniqueName(singular, seen, queue, elem);
                }
                else
                {
                    elemType = TypeOf(elem, Singularize(suggestedName), lang, queue, seen);
                }
                return lang == Lang.TypeScript ? elemType + "[]" : "List<" + elemType + ">";
            }
            default:
                return lang == Lang.TypeScript ? "any" : "object";
        }
    }

    private static string UniqueName(string baseName, HashSet<string> seen, Queue<(string, JsonElement)> queue, JsonElement shape)
    {
        string name = string.IsNullOrEmpty(baseName) ? "Item" : baseName;
        string candidate = name;
        int i = 2;
        while (seen.Contains(candidate)) candidate = name + i++;
        seen.Add(candidate);
        queue.Enqueue((candidate, shape));
        return candidate;
    }

    private static JsonElement FirstOrDefault(JsonElement array, out bool has)
    {
        foreach (var e in array.EnumerateArray()) { has = true; return e; }
        has = false;
        return default;
    }

    private static JsonElement FirstObjectOrSelf(JsonElement array)
    {
        foreach (var e in array.EnumerateArray())
            if (e.ValueKind == JsonValueKind.Object) return e;
        return array;
    }

    private static string SafeMember(string name, Lang lang)
    {
        if (lang != Lang.TypeScript) return name;
        bool simple = name.Length > 0 && (char.IsLetter(name[0]) || name[0] == '_' || name[0] == '$');
        if (simple)
            foreach (char c in name)
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '$') { simple = false; break; }
        return simple ? name : "\"" + name.Replace("\"", "\\\"") + "\"";
    }

    private static string Pascal(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        bool upper = true;
        foreach (char c in s)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            else upper = true;
        }
        if (sb.Length == 0) return "";
        if (char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    private static string Singularize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Item";
        if (s.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && s.Length > 3)
            return s.Substring(0, s.Length - 3) + "y";
        if (s.EndsWith("ses", StringComparison.OrdinalIgnoreCase) && s.Length > 3)
            return s.Substring(0, s.Length - 2);
        if (s.EndsWith("s", StringComparison.OrdinalIgnoreCase) && s.Length > 1 && !s.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
            return s.Substring(0, s.Length - 1);
        return s + "Item";
    }
}
