using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// JSON 扁平化／還原 · JSON flatten / unflatten. Pure managed System.Text.Json.
/// Flatten: nested JSON → single flat object with dotted paths + array indices
/// (e.g. {"a":{"b":1},"arr":[10,20]} → {"a.b":1,"arr[0]":10,"arr[1]":20}).
/// Unflatten: flat dotted/bracketed keys → nested JSON. Never throws — callers get
/// (ok, output, error) so the UI can show a distinct bilingual message.
/// </summary>
public static class JsonFlattenService
{
    public readonly record struct Result(bool Ok, string Output, string? ErrorEn, string? ErrorZh);

    private static Result Fail(string en, string zh) => new(false, string.Empty, en, zh);
    private static Result Good(string output) => new(true, output, null, null);

    /// <summary>Flatten nested JSON into a single object of dotted/bracketed leaf paths.</summary>
    public static Result Flatten(string? input, string separator)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Fail("Enter some JSON to flatten.", "輸入啲 JSON 嚟扁平化。");
        if (string.IsNullOrEmpty(separator)) separator = ".";

        JsonDocument doc;
        try { doc = JsonDocument.Parse(input); }
        catch (JsonException ex)
        {
            return Fail("Invalid JSON — " + ex.Message, "JSON 格式錯誤 — " + ex.Message);
        }
        catch (Exception ex)
        {
            return Fail("Could not read the JSON — " + ex.Message, "讀唔到 JSON — " + ex.Message);
        }

        try
        {
            using (doc)
            {
                var leaves = new List<KeyValuePair<string, JsonElement>>();
                Walk(doc.RootElement, string.Empty, separator, leaves);

                var buffer = new System.IO.MemoryStream();
                using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
                {
                    w.WriteStartObject();
                    foreach (var kv in leaves)
                    {
                        w.WritePropertyName(kv.Key);
                        kv.Value.WriteTo(w);
                    }
                    w.WriteEndObject();
                }
                return Good(Encoding.UTF8.GetString(buffer.ToArray()));
            }
        }
        catch (Exception ex)
        {
            return Fail("Could not flatten — " + ex.Message, "扁平化失敗 — " + ex.Message);
        }
    }

    private static void Walk(JsonElement el, string prefix, string sep, List<KeyValuePair<string, JsonElement>> outp)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                bool anyProp = false;
                foreach (var prop in el.EnumerateObject())
                {
                    anyProp = true;
                    string key = prefix.Length == 0 ? prop.Name : prefix + sep + prop.Name;
                    Walk(prop.Value, key, sep, outp);
                }
                if (!anyProp && prefix.Length > 0)
                    outp.Add(new(prefix, el)); // preserve empty object as a leaf
                break;

            case JsonValueKind.Array:
                int i = 0;
                bool anyItem = false;
                foreach (var item in el.EnumerateArray())
                {
                    anyItem = true;
                    string key = prefix + "[" + i.ToString(CultureInfo.InvariantCulture) + "]";
                    Walk(item, key, sep, outp);
                    i++;
                }
                if (!anyItem && prefix.Length > 0)
                    outp.Add(new(prefix, el)); // preserve empty array as a leaf
                break;

            default:
                // A scalar at the very root (no prefix) still needs a key.
                outp.Add(new(prefix.Length == 0 ? "value" : prefix, el));
                break;
        }
    }

    // ---- Unflatten -------------------------------------------------------

    private abstract class Node { }
    private sealed class ObjNode : Node { public readonly Dictionary<string, Node> Map = new(StringComparer.Ordinal); }
    private sealed class ArrNode : Node { public readonly SortedDictionary<int, Node> Items = new(); }
    private sealed class LeafNode : Node { public JsonElement Value; }

    /// <summary>Rebuild nested JSON from a flat object of dotted/bracketed keys.</summary>
    public static Result Unflatten(string? input, string separator)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Fail("Enter a flat JSON object to unflatten.", "輸入一個扁平 JSON 物件嚟還原。");
        if (string.IsNullOrEmpty(separator)) separator = ".";

        JsonDocument doc;
        try { doc = JsonDocument.Parse(input); }
        catch (JsonException ex)
        {
            return Fail("Invalid JSON — " + ex.Message, "JSON 格式錯誤 — " + ex.Message);
        }
        catch (Exception ex)
        {
            return Fail("Could not read the JSON — " + ex.Message, "讀唔到 JSON — " + ex.Message);
        }

        try
        {
            using (doc)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return Fail("Unflatten expects a flat JSON object of dotted keys.",
                               "還原需要一個由點分隔鍵組成嘅扁平 JSON 物件。");

                Node? root = null;
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var tokens = Tokenize(prop.Name, separator);
                    if (tokens.Count == 0)
                        return Fail("A key was empty and cannot be unflattened.", "有個鍵係空嘅，無法還原。");
                    if (!Insert(ref root, tokens, 0, prop.Value, out string? en, out string? zh))
                        return Fail(en!, zh!);
                }

                root ??= new ObjNode();
                var buffer = new System.IO.MemoryStream();
                using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
                {
                    WriteNode(w, root);
                }
                return Good(Encoding.UTF8.GetString(buffer.ToArray()));
            }
        }
        catch (Exception ex)
        {
            return Fail("Could not unflatten — " + ex.Message, "還原失敗 — " + ex.Message);
        }
    }

    private readonly record struct Token(bool IsIndex, int Index, string Name);

    private static List<Token> Tokenize(string key, string sep)
    {
        var result = new List<Token>();
        // Split on the separator first, then peel [n] array indices off each segment.
        foreach (var rawSeg in key.Split(sep))
        {
            string seg = rawSeg;
            int bracket = seg.IndexOf('[');
            string namePart = bracket < 0 ? seg : seg.Substring(0, bracket);
            if (namePart.Length > 0)
                result.Add(new Token(false, 0, namePart));

            // Parse any trailing [n][m]... groups.
            int p = bracket;
            while (p >= 0 && p < seg.Length && seg[p] == '[')
            {
                int close = seg.IndexOf(']', p);
                if (close < 0) break; // malformed → treat remainder as name
                string inner = seg.Substring(p + 1, close - p - 1);
                if (int.TryParse(inner, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx) && idx >= 0)
                    result.Add(new Token(true, idx, string.Empty));
                else
                    result.Add(new Token(false, 0, inner)); // non-numeric bracket → object key
                p = close + 1;
            }
        }
        return result;
    }

    private static bool Insert(ref Node? node, List<Token> tokens, int depth, JsonElement value,
                               out string? errEn, out string? errZh)
    {
        errEn = null; errZh = null;
        Token t = tokens[depth];
        bool last = depth == tokens.Count - 1;

        if (t.IsIndex)
        {
            if (node is null) node = new ArrNode();
            if (node is not ArrNode arr)
            {
                errEn = "Key structure conflicts (array vs object).";
                errZh = "鍵結構有衝突（陣列同物件撞咗）。";
                return false;
            }
            if (last)
            {
                arr.Items[t.Index] = new LeafNode { Value = value };
                return true;
            }
            Node? child = arr.Items.TryGetValue(t.Index, out var ex) ? ex : null;
            if (!Insert(ref child, tokens, depth + 1, value, out errEn, out errZh)) return false;
            arr.Items[t.Index] = child!;
            return true;
        }
        else
        {
            if (node is null) node = new ObjNode();
            if (node is not ObjNode obj)
            {
                errEn = "Key structure conflicts (object vs array).";
                errZh = "鍵結構有衝突（物件同陣列撞咗）。";
                return false;
            }
            if (last)
            {
                obj.Map[t.Name] = new LeafNode { Value = value };
                return true;
            }
            Node? child = obj.Map.TryGetValue(t.Name, out var ex) ? ex : null;
            if (!Insert(ref child, tokens, depth + 1, value, out errEn, out errZh)) return false;
            obj.Map[t.Name] = child!;
            return true;
        }
    }

    private static void WriteNode(Utf8JsonWriter w, Node node)
    {
        switch (node)
        {
            case LeafNode leaf:
                leaf.Value.WriteTo(w);
                break;
            case ObjNode obj:
                w.WriteStartObject();
                foreach (var kv in obj.Map)
                {
                    w.WritePropertyName(kv.Key);
                    WriteNode(w, kv.Value);
                }
                w.WriteEndObject();
                break;
            case ArrNode arr:
                w.WriteStartArray();
                int expected = 0;
                foreach (var kv in arr.Items)
                {
                    // Fill any gaps (sparse indices) with null so output stays valid.
                    while (expected < kv.Key) { w.WriteNullValue(); expected++; }
                    WriteNode(w, kv.Value);
                    expected++;
                }
                w.WriteEndArray();
                break;
        }
    }
}
