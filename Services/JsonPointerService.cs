using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.Services;

/// <summary>
/// JSON Pointer (RFC 6901) · 指標 — pure-managed resolver over System.Text.Json.Nodes.
/// Parses a document, resolves an RFC 6901 pointer (with ~0/~1 unescaping, array indices,
/// "" = whole document), and can enumerate every valid pointer in a document. Never throws.
/// </summary>
public static class JsonPointerService
{
    /// <summary>Outcome of a resolve attempt. Exactly one of Value / (InvalidJson|NotFound|BadPointer) is meaningful.</summary>
    public sealed class ResolveResult
    {
        public bool Ok;
        public bool InvalidJson;   // the document itself did not parse
        public bool BadPointer;    // the pointer is syntactically invalid (e.g. missing leading '/', "-" for read)
        public bool NotFound;      // syntactically valid pointer but no such node
        public string? Pretty;     // pretty-printed resolved value when Ok
        public string? ValueType;  // resolved value's JSON type when Ok
        public string? Detail;     // extra English detail (parse error / offending token) — safe to show
    }

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    /// <summary>Parse <paramref name="json"/> and resolve <paramref name="pointer"/>. Never throws.</summary>
    public static ResolveResult Resolve(string? json, string? pointer)
    {
        var r = new ResolveResult();
        JsonNode? root;
        try
        {
            var opts = new JsonNodeOptions { PropertyNameCaseInsensitive = false };
            var docOpts = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
            root = JsonNode.Parse(json ?? string.Empty, opts, docOpts);
        }
        catch (JsonException ex)
        {
            r.InvalidJson = true;
            r.Detail = ex.Message;
            return r;
        }
        catch (Exception ex)
        {
            r.InvalidJson = true;
            r.Detail = ex.Message;
            return r;
        }

        // JsonNode.Parse can return null for a literal "null" document — that is still a valid document.
        // Represent it with a JsonValue-null wrapper by working directly with the raw node reference.
        if (!TryParseTokens(pointer, out var tokens, out var badReason))
        {
            r.BadPointer = true;
            r.Detail = badReason;
            return r;
        }

        JsonNode? cur = root;
        bool curIsExplicitNull = root is null; // "null" document
        foreach (var token in tokens)
        {
            if (curIsExplicitNull)
            {
                // Cannot descend into a null value.
                r.NotFound = true;
                r.Detail = $"cannot index into null at token \"{token}\"";
                return r;
            }
            switch (cur)
            {
                case JsonObject obj:
                    if (obj.TryGetPropertyValue(token, out var child))
                    {
                        cur = child;
                        curIsExplicitNull = child is null;
                    }
                    else
                    {
                        r.NotFound = true;
                        r.Detail = $"no member \"{token}\"";
                        return r;
                    }
                    break;

                case JsonArray arr:
                    if (token == "-")
                    {
                        r.BadPointer = true;
                        r.Detail = "\"-\" (end-of-array) is not resolvable for reads";
                        return r;
                    }
                    if (!IsArrayIndexToken(token, out int idx) || idx < 0 || idx >= arr.Count)
                    {
                        r.NotFound = true;
                        r.Detail = $"array index \"{token}\" out of range (count {arr.Count})";
                        return r;
                    }
                    cur = arr[idx];
                    curIsExplicitNull = cur is null;
                    break;

                default:
                    // Scalar/value: cannot descend further.
                    r.NotFound = true;
                    r.Detail = $"cannot descend into a scalar at token \"{token}\"";
                    return r;
            }
        }

        r.Ok = true;
        if (curIsExplicitNull || cur is null)
        {
            r.Pretty = "null";
            r.ValueType = "null";
        }
        else
        {
            r.ValueType = TypeName(cur);
            try { r.Pretty = cur.ToJsonString(Pretty); }
            catch (Exception ex) { r.Pretty = cur.ToString(); r.Detail = ex.Message; }
        }
        return r;
    }

    /// <summary>A discovered pointer plus its value type, for the "list all pointers" view.</summary>
    public sealed class PointerEntry
    {
        public string Pointer { get; set; } = "";
        public string ValueType { get; set; } = "";
        public string Preview { get; set; } = "";
    }

    /// <summary>Outcome of a walk. Entries is empty when the document is invalid.</summary>
    public sealed class WalkResult
    {
        public bool Ok;
        public bool InvalidJson;
        public string? Detail;
        public List<PointerEntry> Entries { get; } = new();
    }

    /// <summary>Parse <paramref name="json"/> and enumerate every valid JSON Pointer within. Never throws.</summary>
    public static WalkResult ListAllPointers(string? json)
    {
        var res = new WalkResult();
        JsonNode? root;
        try
        {
            var docOpts = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
            root = JsonNode.Parse(json ?? string.Empty, null, docOpts);
        }
        catch (Exception ex)
        {
            res.InvalidJson = true;
            res.Detail = ex.Message;
            return res;
        }

        // The whole document is always a valid pointer ("").
        res.Entries.Add(new PointerEntry { Pointer = "", ValueType = root is null ? "null" : TypeName(root), Preview = Preview(root) });
        try { Walk(root, "", res.Entries); }
        catch { /* never throw — return whatever we gathered */ }
        res.Ok = true;
        return res;
    }

    private static void Walk(JsonNode? node, string prefix, List<PointerEntry> sink)
    {
        if (sink.Count > 20000) return; // safety cap against pathological documents
        switch (node)
        {
            case JsonObject obj:
                foreach (var kvp in obj)
                {
                    string ptr = prefix + "/" + Escape(kvp.Key);
                    sink.Add(new PointerEntry { Pointer = ptr, ValueType = kvp.Value is null ? "null" : TypeName(kvp.Value), Preview = Preview(kvp.Value) });
                    Walk(kvp.Value, ptr, sink);
                }
                break;
            case JsonArray arr:
                for (int i = 0; i < arr.Count; i++)
                {
                    string ptr = prefix + "/" + i.ToString(CultureInfo.InvariantCulture);
                    sink.Add(new PointerEntry { Pointer = ptr, ValueType = arr[i] is null ? "null" : TypeName(arr[i]!), Preview = Preview(arr[i]) });
                    Walk(arr[i], ptr, sink);
                }
                break;
        }
    }

    // ---- RFC 6901 helpers ----

    /// <summary>Split a pointer string into reference tokens, applying ~1→/ and ~0→~ unescaping.</summary>
    private static bool TryParseTokens(string? pointer, out List<string> tokens, out string? badReason)
    {
        tokens = new List<string>();
        badReason = null;
        pointer ??= string.Empty;
        if (pointer.Length == 0) return true; // "" = whole document
        if (pointer[0] != '/')
        {
            badReason = "a non-empty pointer must start with '/'";
            return false;
        }
        // Split on '/' — note the leading '/' yields an empty first element which we skip.
        var parts = pointer.Split('/');
        for (int i = 1; i < parts.Length; i++)
        {
            if (!TryUnescape(parts[i], out var token, out var why))
            {
                badReason = why;
                return false;
            }
            tokens.Add(token);
        }
        return true;
    }

    /// <summary>Unescape a single reference token: ~1→/, ~0→~. A '~' not followed by 0/1 is invalid.</summary>
    private static bool TryUnescape(string raw, out string result, out string? why)
    {
        why = null;
        if (raw.IndexOf('~') < 0) { result = raw; return true; }
        var sb = new System.Text.StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '~')
            {
                if (i + 1 >= raw.Length) { result = ""; why = "dangling '~' escape in token"; return false; }
                char n = raw[++i];
                if (n == '0') sb.Append('~');
                else if (n == '1') sb.Append('/');
                else { result = ""; why = $"invalid escape \"~{n}\" (only ~0 and ~1 are allowed)"; return false; }
            }
            else sb.Append(c);
        }
        result = sb.ToString();
        return true;
    }

    /// <summary>Escape a member name for embedding into a pointer: ~→~0, /→~1.</summary>
    private static string Escape(string member) => member.Replace("~", "~0").Replace("/", "~1");

    /// <summary>RFC 6901 array index: "0" or a non-negative integer with no leading zeros.</summary>
    private static bool IsArrayIndexToken(string token, out int index)
    {
        index = 0;
        if (token.Length == 0) return false;
        if (token == "0") { index = 0; return true; }
        if (token[0] == '0') return false; // no leading zeros
        foreach (char c in token) if (c < '0' || c > '9') return false;
        return int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out index);
    }

    private static string TypeName(JsonNode node)
    {
        switch (node)
        {
            case JsonObject: return "object";
            case JsonArray: return "array";
            case JsonValue v:
                if (v.TryGetValue<bool>(out _)) return "boolean";
                if (v.TryGetValue<string>(out _)) return "string";
                // numbers
                return "number";
            default: return "value";
        }
    }

    private static string Preview(JsonNode? node)
    {
        try
        {
            if (node is null) return "null";
            string s = node switch
            {
                JsonObject o => "{ " + o.Count + " member(s) }",
                JsonArray a => "[ " + a.Count + " item(s) ]",
                _ => node.ToJsonString()
            };
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length > 80 ? s.Substring(0, 79) + "…" : s;
        }
        catch { return ""; }
    }
}
