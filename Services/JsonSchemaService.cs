using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// JSON Schema 驗證器（draft-07 實用子集）· Hand-written JSON Schema validator — no NuGet.
/// Validates a JSON document against a draft-07 practical subset and reports each violation
/// with a JSON-Pointer path. Pure managed C# (System.Text.Json + Nodes). Never throws from
/// <see cref="Validate"/>; malformed schema/document JSON is surfaced as a structured result.
/// </summary>
public static class JsonSchemaService
{
    /// <summary>One reported problem: a JSON-Pointer path + a bilingual-ready message.</summary>
    public sealed class Violation
    {
        public string Path { get; init; } = "/";
        public string Message { get; init; } = "";
    }

    /// <summary>Outcome of a validation run.</summary>
    public sealed class Result
    {
        public bool SchemaOk { get; init; }
        public bool DocumentOk { get; init; }
        public bool Valid { get; init; }
        public string? SchemaError { get; init; }
        public string? DocumentError { get; init; }
        public List<Violation> Violations { get; } = new();
    }

    /// <summary>
    /// Validate <paramref name="documentJson"/> against <paramref name="schemaJson"/>.
    /// Returns a structured result; never throws.
    /// </summary>
    public static Result Validate(string schemaJson, string documentJson, Func<string, string, string> pick)
    {
        var result = new Result_Builder();

        JsonNode? schema = null;
        JsonNode? doc = null;

        try
        {
            schema = ParseNode(schemaJson);
            if (schema is null && !LooksLikeNullLiteral(schemaJson))
                result.SchemaError = pick("Schema is empty.", "結構描述係空白嘅。");
            else
                result.SchemaOk = true;
        }
        catch (JsonException ex)
        {
            result.SchemaError = pick($"Schema is not valid JSON: {ex.Message}", $"結構描述唔係有效嘅 JSON：{ex.Message}");
        }
        catch (Exception ex)
        {
            result.SchemaError = pick($"Could not read schema: {ex.Message}", $"讀唔到結構描述：{ex.Message}");
        }

        try
        {
            doc = ParseNode(documentJson);
            result.DocumentOk = true;
        }
        catch (JsonException ex)
        {
            result.DocumentError = pick($"Document is not valid JSON: {ex.Message}", $"文件唔係有效嘅 JSON：{ex.Message}");
        }
        catch (Exception ex)
        {
            result.DocumentError = pick($"Could not read document: {ex.Message}", $"讀唔到文件：{ex.Message}");
        }

        if (!result.SchemaOk || !result.DocumentOk)
            return result.Build(valid: false);

        var ctx = new Context(schema!, pick);
        try
        {
            ValidateNode(doc, schema!, "", ctx);
        }
        catch (Exception ex)
        {
            // Defensive: any unexpected condition becomes a violation rather than a crash.
            ctx.Add("", pick($"Internal validation error: {ex.Message}", $"驗證期間發生內部錯誤：{ex.Message}"));
        }

        foreach (var v in ctx.Violations) result.Violations.Add(v);
        return result.Build(valid: ctx.Violations.Count == 0);
    }

    // Mutable builder so init-only Result stays immutable to callers.
    private sealed class Result_Builder
    {
        public bool SchemaOk;
        public bool DocumentOk;
        public string? SchemaError;
        public string? DocumentError;
        public List<Violation> Violations = new();

        public Result Build(bool valid)
        {
            var r = new Result
            {
                SchemaOk = SchemaOk,
                DocumentOk = DocumentOk,
                Valid = valid && SchemaOk && DocumentOk,
                SchemaError = SchemaError,
                DocumentError = DocumentError,
            };
            r.Violations.AddRange(Violations);
            return r;
        }
    }

    private sealed class Context
    {
        public readonly JsonNode Root;
        public readonly Func<string, string, string> Pick;
        public readonly List<Violation> Violations = new();
        private int _depth;

        public Context(JsonNode root, Func<string, string, string> pick) { Root = root; Pick = pick; }

        public string P(string en, string zh) => Pick(en, zh);

        public void Add(string path, string message)
            => Violations.Add(new Violation { Path = string.IsNullOrEmpty(path) ? "/" : path, Message = message });

        public IDisposable Enter() { _depth++; if (_depth > 128) throw new InvalidOperationException("schema too deeply nested"); return new Pop(this); }
        private sealed class Pop : IDisposable { private readonly Context _c; public Pop(Context c) { _c = c; } public void Dispose() => _c._depth--; }
    }

    private static JsonNode? ParseNode(string json)
    {
        json ??= "";
        return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
    }

    private static bool LooksLikeNullLiteral(string s) => s?.Trim() == "null";

    // ---- core recursive validation ------------------------------------------------

    private static void ValidateNode(JsonNode? node, JsonNode? schema, string path, Context ctx)
    {
        using var _ = ctx.Enter();

        // Boolean schemas: true = anything, false = nothing.
        if (schema is JsonValue bv && bv.TryGetValue(out bool allow))
        {
            if (!allow) ctx.Add(path, ctx.P("No value is allowed here (schema is 'false').", "呢度唔容許任何值（結構描述為 false）。"));
            return;
        }

        if (schema is not JsonObject sObj)
            return; // unknown schema form → treat as permissive

        // $ref resolution (to #/definitions or #/$defs).
        if (sObj["$ref"] is JsonValue refv && refv.TryGetValue(out string? refStr) && !string.IsNullOrWhiteSpace(refStr))
        {
            var resolved = ResolveRef(refStr!, ctx.Root);
            if (resolved is null)
            {
                ctx.Add(path, ctx.P($"Unresolved $ref '{refStr}'.", $"解析唔到 $ref「{refStr}」。"));
                return;
            }
            ValidateNode(node, resolved, path, ctx);
            return;
        }

        // const
        if (sObj.ContainsKey("const"))
        {
            if (!JsonEquals(node, sObj["const"]))
                ctx.Add(path, ctx.P($"Value must equal the const {Render(sObj["const"])}.", $"個值必須等於常數 {Render(sObj["const"])}。"));
        }

        // enum
        if (sObj["enum"] is JsonArray enumArr)
        {
            bool matched = enumArr.Any(e => JsonEquals(node, e));
            if (!matched)
                ctx.Add(path, ctx.P($"Value must be one of the allowed enum values: {RenderList(enumArr)}.", $"個值必須係列舉之一：{RenderList(enumArr)}。"));
        }

        // type
        string? actual = TypeName(node);
        var declared = ReadTypes(sObj["type"]);
        if (declared.Count > 0)
        {
            bool ok = declared.Any(t => TypeMatches(t, node, actual));
            if (!ok)
            {
                string want = string.Join(" / ", declared);
                ctx.Add(path, ctx.P($"Expected type {want} but got {actual}.", $"預期類型 {want}，但實際係 {actual}。"));
                // type mismatch — keyword checks below would be noisy, so stop here.
                return;
            }
        }

        switch (node)
        {
            case JsonObject obj: ValidateObject(obj, sObj, path, ctx); break;
            case JsonArray arr: ValidateArray(arr, sObj, path, ctx); break;
            case JsonValue val: ValidateScalar(val, sObj, path, ctx); break;
        }
    }

    private static void ValidateObject(JsonObject obj, JsonObject sObj, string path, Context ctx)
    {
        var props = sObj["properties"] as JsonObject;

        // required
        if (sObj["required"] is JsonArray req)
        {
            foreach (var rn in req)
            {
                if (rn is JsonValue rv && rv.TryGetValue(out string? name) && name is not null && !obj.ContainsKey(name))
                    ctx.Add(Join(path, name), ctx.P($"Required property '{name}' is missing.", $"缺少必填屬性「{name}」。"));
            }
        }

        // minProperties / maxProperties
        int count = obj.Count;
        if (TryInt(sObj["minProperties"], out int minP) && count < minP)
            ctx.Add(path, ctx.P($"Object has {count} properties but at least {minP} are required.", $"物件得 {count} 個屬性，最少要 {minP} 個。"));
        if (TryInt(sObj["maxProperties"], out int maxP) && count > maxP)
            ctx.Add(path, ctx.P($"Object has {count} properties but at most {maxP} are allowed.", $"物件有 {count} 個屬性，最多只准 {maxP} 個。"));

        // properties
        if (props is not null)
        {
            foreach (var kv in obj)
            {
                if (props[kv.Key] is JsonNode pSchema)
                    ValidateNode(kv.Value, pSchema, Join(path, kv.Key), ctx);
            }
        }

        // additionalProperties: false (or a schema)
        var ap = sObj["additionalProperties"];
        if (ap is JsonValue apv && apv.TryGetValue(out bool apAllowed) && !apAllowed)
        {
            foreach (var kv in obj)
            {
                bool known = props is not null && props.ContainsKey(kv.Key);
                if (!known)
                    ctx.Add(Join(path, kv.Key), ctx.P($"Additional property '{kv.Key}' is not allowed.", $"唔准有額外屬性「{kv.Key}」。"));
            }
        }
        else if (ap is JsonObject apSchema)
        {
            foreach (var kv in obj)
            {
                bool known = props is not null && props.ContainsKey(kv.Key);
                if (!known)
                    ValidateNode(kv.Value, apSchema, Join(path, kv.Key), ctx);
            }
        }
    }

    private static void ValidateArray(JsonArray arr, JsonObject sObj, string path, Context ctx)
    {
        int count = arr.Count;
        if (TryInt(sObj["minItems"], out int mn) && count < mn)
            ctx.Add(path, ctx.P($"Array has {count} items but at least {mn} are required.", $"陣列得 {count} 個項目，最少要 {mn} 個。"));
        if (TryInt(sObj["maxItems"], out int mx) && count > mx)
            ctx.Add(path, ctx.P($"Array has {count} items but at most {mx} are allowed.", $"陣列有 {count} 個項目，最多只准 {mx} 個。"));

        if (sObj["uniqueItems"] is JsonValue uv && uv.TryGetValue(out bool unique) && unique)
        {
            for (int i = 0; i < arr.Count; i++)
                for (int j = i + 1; j < arr.Count; j++)
                    if (JsonEquals(arr[i], arr[j]))
                    {
                        ctx.Add(Join(path, j.ToString(CultureInfo.InvariantCulture)),
                            ctx.P($"Duplicate item — items must be unique (matches index {i}).", $"重複項目 — 各項必須唯一（同索引 {i} 相同）。"));
                    }
        }

        // items: single schema applied to every element (tuple form not supported in this subset).
        if (sObj["items"] is JsonNode itemsSchema && itemsSchema is not JsonArray)
        {
            for (int i = 0; i < arr.Count; i++)
                ValidateNode(arr[i], itemsSchema, Join(path, i.ToString(CultureInfo.InvariantCulture)), ctx);
        }
    }

    private static void ValidateScalar(JsonValue val, JsonObject sObj, string path, Context ctx)
    {
        // string constraints
        if (val.TryGetValue(out string? s) && s is not null && !IsNumericJson(val))
        {
            int len = s.Length;
            if (TryInt(sObj["minLength"], out int mn) && len < mn)
                ctx.Add(path, ctx.P($"String length {len} is below the minimum of {mn}.", $"字串長度 {len} 少過最小值 {mn}。"));
            if (TryInt(sObj["maxLength"], out int mx) && len > mx)
                ctx.Add(path, ctx.P($"String length {len} exceeds the maximum of {mx}.", $"字串長度 {len} 超過最大值 {mx}。"));

            if (sObj["pattern"] is JsonValue pv && pv.TryGetValue(out string? pat) && !string.IsNullOrEmpty(pat))
            {
                try
                {
                    if (!Regex.IsMatch(s, pat!, RegexOptions.None, TimeSpan.FromSeconds(2)))
                        ctx.Add(path, ctx.P($"String does not match pattern /{pat}/.", $"字串唔符合規則式 /{pat}/。"));
                }
                catch (RegexParseException)
                {
                    ctx.Add(path, ctx.P($"Schema pattern /{pat}/ is not a valid regular expression.", $"結構描述嘅規則式 /{pat}/ 唔係有效嘅正規表示式。"));
                }
                catch (RegexMatchTimeoutException)
                {
                    ctx.Add(path, ctx.P("Pattern match timed out.", "規則式比對逾時。"));
                }
            }
        }

        // numeric constraints
        if (TryNum(val, out double num))
        {
            if (TryNum(sObj["minimum"], out double min) && num < min)
                ctx.Add(path, ctx.P($"Value {Trim(num)} is below the minimum of {Trim(min)}.", $"個值 {Trim(num)} 細過最小值 {Trim(min)}。"));
            if (TryNum(sObj["maximum"], out double max) && num > max)
                ctx.Add(path, ctx.P($"Value {Trim(num)} exceeds the maximum of {Trim(max)}.", $"個值 {Trim(num)} 大過最大值 {Trim(max)}。"));
            if (TryNum(sObj["exclusiveMinimum"], out double exmin) && num <= exmin)
                ctx.Add(path, ctx.P($"Value {Trim(num)} must be greater than {Trim(exmin)}.", $"個值 {Trim(num)} 必須大過 {Trim(exmin)}。"));
            if (TryNum(sObj["exclusiveMaximum"], out double exmax) && num >= exmax)
                ctx.Add(path, ctx.P($"Value {Trim(num)} must be less than {Trim(exmax)}.", $"個值 {Trim(num)} 必須細過 {Trim(exmax)}。"));
        }
    }

    // ---- helpers ------------------------------------------------------------------

    private static JsonNode? ResolveRef(string reference, JsonNode root)
    {
        // Only local pointers like "#", "#/definitions/X", "#/$defs/X".
        if (reference == "#") return root;
        if (!reference.StartsWith("#/", StringComparison.Ordinal)) return null;
        var parts = reference.Substring(2).Split('/');
        JsonNode? cur = root;
        foreach (var raw in parts)
        {
            var token = raw.Replace("~1", "/").Replace("~0", "~");
            if (cur is JsonObject o && o[token] is JsonNode next) cur = next;
            else return null;
        }
        return cur;
    }

    private static List<string> ReadTypes(JsonNode? typeNode)
    {
        var list = new List<string>();
        if (typeNode is JsonValue tv && tv.TryGetValue(out string? t) && t is not null) list.Add(t);
        else if (typeNode is JsonArray ta)
            foreach (var e in ta)
                if (e is JsonValue ev && ev.TryGetValue(out string? et) && et is not null) list.Add(et);
        return list;
    }

    private static bool TypeMatches(string declared, JsonNode? node, string? actual)
    {
        switch (declared)
        {
            case "integer":
                return node is JsonValue jv && TryNum(jv, out double d) && Math.Floor(d) == d && !double.IsInfinity(d);
            case "number":
                return actual == "integer" || actual == "number";
            default:
                return declared == actual;
        }
    }

    private static string TypeName(JsonNode? node)
    {
        if (node is null) return "null";
        if (node is JsonObject) return "object";
        if (node is JsonArray) return "array";
        if (node is JsonValue v)
        {
            if (v.TryGetValue(out bool _)) return "boolean";
            if (IsNumericJson(v)) return (TryNum(v, out double d) && Math.Floor(d) == d && !double.IsInfinity(d)) ? "integer" : "number";
            if (v.TryGetValue(out string? _)) return "string";
        }
        return "unknown";
    }

    private static bool IsNumericJson(JsonValue v)
    {
        var el = v.GetValue<JsonElement>();
        return el.ValueKind == JsonValueKind.Number;
    }

    private static bool TryNum(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue v) return false;
        try
        {
            var el = v.GetValue<JsonElement>();
            if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out value)) return true;
        }
        catch { }
        return false;
    }

    private static bool TryInt(JsonNode? node, out int value)
    {
        value = 0;
        if (TryNum(node, out double d)) { value = (int)d; return true; }
        return false;
    }

    private static bool JsonEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        if (a is JsonArray aa && b is JsonArray ba)
        {
            if (aa.Count != ba.Count) return false;
            for (int i = 0; i < aa.Count; i++) if (!JsonEquals(aa[i], ba[i])) return false;
            return true;
        }
        if (a is JsonObject ao && b is JsonObject bo)
        {
            if (ao.Count != bo.Count) return false;
            foreach (var kv in ao)
            {
                if (!bo.ContainsKey(kv.Key)) return false;
                if (!JsonEquals(kv.Value, bo[kv.Key])) return false;
            }
            return true;
        }
        if (a is JsonValue av && b is JsonValue bv)
        {
            if (TryNum(av, out double da) && TryNum(bv, out double db)) return da == db;
            var ea = av.GetValue<JsonElement>();
            var eb = bv.GetValue<JsonElement>();
            if (ea.ValueKind != eb.ValueKind) return false;
            return ea.ToString() == eb.ToString();
        }
        return false;
    }

    private static string Render(JsonNode? node)
    {
        if (node is null) return "null";
        try { return node.ToJsonString(); } catch { return node.ToString(); }
    }

    private static string RenderList(JsonArray arr)
    {
        var items = arr.Select(Render);
        return string.Join(", ", items);
    }

    private static string Trim(double d) => d.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Join(string basePath, string token)
    {
        // JSON-Pointer escaping.
        var escaped = token.Replace("~", "~0").Replace("/", "~1");
        return basePath + "/" + escaped;
    }

    /// <summary>A ready-made sample schema + document pair (the document is intentionally partly invalid).</summary>
    public static (string Schema, string Document) Sample()
    {
        string schema = """
        {
          "$schema": "http://json-schema.org/draft-07/schema#",
          "type": "object",
          "required": ["name", "age", "role"],
          "additionalProperties": false,
          "properties": {
            "name":  { "type": "string", "minLength": 2, "maxLength": 40 },
            "age":   { "type": "integer", "minimum": 0, "maximum": 130 },
            "email": { "type": "string", "pattern": "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$" },
            "role":  { "enum": ["admin", "user", "guest"] },
            "tags":  { "type": "array", "items": { "type": "string" }, "uniqueItems": true, "maxItems": 5 },
            "manager": { "$ref": "#/definitions/person" }
          },
          "definitions": {
            "person": {
              "type": "object",
              "required": ["name"],
              "properties": { "name": { "type": "string" } }
            }
          }
        }
        """;

        string doc = """
        {
          "name": "A",
          "age": 200,
          "email": "not-an-email",
          "role": "superuser",
          "tags": ["x", "x"],
          "nickname": "oops",
          "manager": { "age": 40 }
        }
        """;

        return (schema, doc);
    }
}
