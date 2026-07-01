using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// JSON 分析器 · JSON analyzer — parses text with JsonDocument and computes structural statistics
/// (node counts, nesting depth, key inventory, value-type breakdown, sizes). Pure managed
/// System.Text.Json; never throws — parse failures surface as <see cref="JsonStatResult.Error"/>.
/// </summary>
public static class JsonStatService
{
    /// <summary>One unique key with how many times it occurs across the whole document.</summary>
    public sealed class KeyCount
    {
        public string Key { get; set; } = "";
        public int Count { get; set; }
    }

    /// <summary>Computed statistics for a JSON document (or an error when parsing failed).</summary>
    public sealed class JsonStatResult
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }

        public int TotalNodes { get; set; }
        public int ObjectCount { get; set; }
        public int ArrayCount { get; set; }
        public int MaxDepth { get; set; }
        public int TotalKeys { get; set; }
        public int UniqueKeys { get; set; }

        public int StringCount { get; set; }
        public int NumberCount { get; set; }
        public int BooleanCount { get; set; }
        public int NullCount { get; set; }

        public int LargestArray { get; set; }
        public long StringChars { get; set; }
        public long ByteSize { get; set; }

        public List<KeyCount> Keys { get; } = new();
    }

    /// <summary>
    /// Analyze <paramref name="json"/>. Returns a populated result on success, or a result with
    /// <see cref="JsonStatResult.Ok"/> == false and an <see cref="JsonStatResult.Error"/> message.
    /// Never throws.
    /// </summary>
    public static JsonStatResult Analyze(string? json)
    {
        var r = new JsonStatResult();

        if (string.IsNullOrWhiteSpace(json))
        {
            r.Error = "empty";
            return r;
        }

        try
        {
            r.ByteSize = Encoding.UTF8.GetByteCount(json);
        }
        catch
        {
            r.ByteSize = 0;
        }

        var keyTally = new Dictionary<string, int>(StringComparer.Ordinal);

        try
        {
            using var doc = JsonDocument.Parse(json);
            Walk(doc.RootElement, 1, r, keyTally);
            r.Ok = true;
        }
        catch (JsonException jx)
        {
            r.Ok = false;
            r.Error = jx.Message;
            return r;
        }
        catch (Exception ex)
        {
            r.Ok = false;
            r.Error = ex.Message;
            return r;
        }

        r.UniqueKeys = keyTally.Count;
        foreach (var kv in keyTally)
            r.Keys.Add(new KeyCount { Key = kv.Key, Count = kv.Value });
        r.Keys.Sort(static (a, b) => b.Count != a.Count
            ? b.Count.CompareTo(a.Count)
            : string.Compare(a.Key, b.Key, StringComparison.Ordinal));

        return r;
    }

    private static void Walk(JsonElement el, int depth, JsonStatResult r, Dictionary<string, int> keyTally)
    {
        if (depth > r.MaxDepth) r.MaxDepth = depth;
        r.TotalNodes++;

        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                r.ObjectCount++;
                foreach (var prop in el.EnumerateObject())
                {
                    r.TotalKeys++;
                    keyTally.TryGetValue(prop.Name, out int n);
                    keyTally[prop.Name] = n + 1;
                    Walk(prop.Value, depth + 1, r, keyTally);
                }
                break;

            case JsonValueKind.Array:
                r.ArrayCount++;
                int len = 0;
                foreach (var item in el.EnumerateArray())
                {
                    len++;
                    Walk(item, depth + 1, r, keyTally);
                }
                if (len > r.LargestArray) r.LargestArray = len;
                break;

            case JsonValueKind.String:
                r.StringCount++;
                var s = el.GetString();
                if (s != null) r.StringChars += s.Length;
                break;

            case JsonValueKind.Number:
                r.NumberCount++;
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                r.BooleanCount++;
                break;

            case JsonValueKind.Null:
                r.NullCount++;
                break;
        }
    }
}
