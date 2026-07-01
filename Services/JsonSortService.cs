using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.Services;

/// <summary>
/// JSON 鍵排序 / 正規化 · JSON key sorter / normaliser. Recursively sorts every object's
/// keys (A→Z or Z→A, optional case-insensitive), optionally sorts arrays of primitives,
/// and re-serialises pretty (2/4/tab indent) or minified. Pure managed System.Text.Json.
/// Never throws — all failures surface as <see cref="JsonSortResult"/> with an error flag.
/// </summary>
public static class JsonSortService
{
    public enum IndentKind { TwoSpaces, FourSpaces, Tab }

    public sealed class JsonSortOptions
    {
        public bool Descending { get; set; }
        public bool CaseInsensitive { get; set; } = true;
        public bool Minify { get; set; }
        public IndentKind Indent { get; set; } = IndentKind.TwoSpaces;
        public bool SortArrays { get; set; }
    }

    public sealed class JsonSortResult
    {
        public bool Ok { get; set; }
        public string Output { get; set; } = string.Empty;
        public string? ErrorEn { get; set; }
        public string? ErrorZh { get; set; }
        /// <summary>True if any object in the input contained duplicate keys (last wins).</summary>
        public bool HadDuplicateKeys { get; set; }
    }

    /// <summary>
    /// Parse, sort and re-serialise. Robust: returns a result with an error flag rather than throwing.
    /// </summary>
    public static JsonSortResult Sort(string? input, JsonSortOptions options)
    {
        options ??= new JsonSortOptions();
        var result = new JsonSortResult();

        if (string.IsNullOrWhiteSpace(input))
        {
            result.ErrorEn = "Nothing to sort — paste some JSON first.";
            result.ErrorZh = "冇嘢排 — 請先貼入 JSON。";
            return result;
        }

        JsonNode? root;
        try
        {
            var docOpts = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            // Detect duplicate keys up-front (JsonNode.Parse throws on dupes; JsonDocument keeps last).
            using (var doc = JsonDocument.Parse(input, docOpts))
            {
                result.HadDuplicateKeys = HasDuplicateKeys(doc.RootElement);
                root = JsonElementToNode(doc.RootElement);
            }
        }
        catch (JsonException jx)
        {
            result.ErrorEn = $"Invalid JSON — {jx.Message}";
            result.ErrorZh = $"JSON 格式錯誤 — {jx.Message}";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorEn = $"Could not read the JSON — {ex.Message}";
            result.ErrorZh = $"讀唔到呢段 JSON — {ex.Message}";
            return result;
        }

        JsonNode? sorted;
        try
        {
            sorted = SortNode(root, options);
        }
        catch (Exception ex)
        {
            result.ErrorEn = $"Could not sort the JSON — {ex.Message}";
            result.ErrorZh = $"排唔到呢段 JSON — {ex.Message}";
            return result;
        }

        try
        {
            var writerOpts = new JsonSerializerOptions { WriteIndented = !options.Minify };
            if (!options.Minify)
                writerOpts.IndentCharacter = options.Indent == IndentKind.Tab ? '\t' : ' ';
            if (!options.Minify)
                writerOpts.IndentSize = options.Indent switch
                {
                    IndentKind.FourSpaces => 4,
                    IndentKind.Tab => 1,
                    _ => 2,
                };
            result.Output = sorted?.ToJsonString(writerOpts) ?? "null";
            result.Ok = true;
        }
        catch (Exception ex)
        {
            result.ErrorEn = $"Could not write the result — {ex.Message}";
            result.ErrorZh = $"寫唔到結果 — {ex.Message}";
            result.Ok = false;
        }

        return result;
    }

    private static bool HasDuplicateKeys(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var prop in element.EnumerateObject())
                {
                    if (!seen.Add(prop.Name)) return true;
                    if (HasDuplicateKeys(prop.Value)) return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (HasDuplicateKeys(item)) return true;
                return false;
            default:
                return false;
        }
    }

    // Deep-clone a JsonElement into a mutable JsonNode tree (avoids JsonNode.Parse dupe-throw).
    private static JsonNode? JsonElementToNode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var prop in element.EnumerateObject())
                    obj[prop.Name] = JsonElementToNode(prop.Value); // last duplicate wins
                return obj;
            case JsonValueKind.Array:
                var arr = new JsonArray();
                foreach (var item in element.EnumerateArray())
                    arr.Add(JsonElementToNode(item));
                return arr;
            case JsonValueKind.String:
                return JsonValue.Create(element.GetString());
            case JsonValueKind.Number:
                return JsonNode.Parse(element.GetRawText());
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }

    private static JsonNode? SortNode(JsonNode? node, JsonSortOptions options)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var comparer = options.CaseInsensitive
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;
                var pairs = obj.ToList(); // detach before mutating
                IEnumerable<KeyValuePair<string, JsonNode?>> ordered = options.Descending
                    ? pairs.OrderByDescending(p => p.Key, comparer)
                    : pairs.OrderBy(p => p.Key, comparer);

                var result = new JsonObject();
                foreach (var pair in ordered)
                {
                    var child = pair.Value;
                    obj.Remove(pair.Key);       // detach child from old parent
                    result[pair.Key] = SortNode(child, options);
                }
                return result;
            }
            case JsonArray arr:
            {
                var items = arr.ToList();
                foreach (var item in items) arr.Remove(item); // detach children first
                var newArr = new JsonArray();
                foreach (var item in items) newArr.Add(SortNode(item, options));

                if (options.SortArrays && IsAllPrimitives(newArr))
                    SortPrimitiveArray(newArr, options);

                return newArr;
            }
            case JsonValue val:
            {
                // Detach so it can be re-parented cleanly.
                var clone = val.GetValue<object?>();
                return JsonValue.Create(clone);
            }
            default:
                return null;
        }
    }

    private static bool IsAllPrimitives(JsonArray arr)
    {
        foreach (var item in arr)
        {
            if (item is JsonObject || item is JsonArray) return false;
        }
        return true;
    }

    private static void SortPrimitiveArray(JsonArray arr, JsonSortOptions options)
    {
        var items = arr.ToList();
        foreach (var item in items) arr.Remove(item);

        var comparer = options.CaseInsensitive
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        // Sort by a stable string key derived from each primitive; numbers compared numerically when possible.
        var sorted = items.OrderBy(n => PrimitiveKey(n), new PrimitiveComparer(comparer, options.Descending)).ToList();

        foreach (var n in sorted) arr.Add(n);
    }

    private static string PrimitiveKey(JsonNode? node)
    {
        if (node is null) return string.Empty;
        try { return node.ToJsonString(); }
        catch { return node.ToString(); }
    }

    private sealed class PrimitiveComparer : IComparer<string>
    {
        private readonly StringComparer _text;
        private readonly bool _descending;
        public PrimitiveComparer(StringComparer text, bool descending) { _text = text; _descending = descending; }

        public int Compare(string? x, string? y)
        {
            x ??= string.Empty; y ??= string.Empty;
            int cmp;
            // Prefer numeric compare when both look numeric.
            if (double.TryParse(x.Trim('"'), out var dx) && double.TryParse(y.Trim('"'), out var dy))
                cmp = dx.CompareTo(dy);
            else
                cmp = _text.Compare(x, y);
            return _descending ? -cmp : cmp;
        }
    }
}
