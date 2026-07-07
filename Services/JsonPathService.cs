using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// JSONPath-lite（純受控）· A small, dependency-free JSONPath evaluator over System.Text.Json.
/// Supports: $ (root), .key / ['key'] member access, [n] index, [*] and .* wildcards,
/// and .. recursive descent (..key). Never throws — returns structured results/errors.
/// Also produces leaf-path listings and a flattened path=value view.
/// </summary>
public static class JsonPathService
{
    /// <summary>A single matched value with the concrete path that reached it.</summary>
    public sealed class Match
    {
        public string Path { get; set; } = "$";
        public string Value { get; set; } = "";
    }

    /// <summary>Result of a query / listing — either Matches (Ok) or an Error message.</summary>
    public sealed class QueryResult
    {
        public bool Ok { get; set; }
        public string? Error { get; set; }
        public List<Match> Matches { get; } = new();
    }

    // ---- token model for the query path -------------------------------------------------

    private enum StepKind { Child, Index, Wildcard, RecursiveKey, RecursiveWildcard }

    private readonly struct Step
    {
        public StepKind Kind { get; }
        public string Key { get; }
        public int Index { get; }
        public Step(StepKind kind, string key = "", int index = 0) { Kind = kind; Key = key; Index = index; }
    }

    /// <summary>
    /// Evaluate <paramref name="query"/> against <paramref name="json"/>. Both are parsed defensively;
    /// invalid JSON or an invalid query yields Ok=false with a bilingual-friendly Error string set by the caller.
    /// </summary>
    public static QueryResult Query(string json, string query)
    {
        var result = new QueryResult();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = "json:" + ex.Message;
            return result;
        }

        using (doc)
        {
            List<Step> steps;
            try
            {
                steps = Parse(query);
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = "query:" + ex.Message;
                return result;
            }

            try
            {
                var acc = new List<(string path, JsonElement el)> { ("$", doc.RootElement.Clone()) };
                foreach (var step in steps)
                {
                    var next = new List<(string, JsonElement)>();
                    foreach (var (path, el) in acc)
                        Apply(step, path, el, next);
                    acc = next;
                }

                foreach (var (path, el) in acc)
                    result.Matches.Add(new Match { Path = path, Value = Stringify(el) });
                result.Ok = true;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = "eval:" + ex.Message;
            }
        }
        return result;
    }

    /// <summary>List every leaf (scalar / empty container) as path=value pairs.</summary>
    public static QueryResult LeafPaths(string json)
    {
        var result = new QueryResult();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = "json:" + ex.Message;
            return result;
        }
        using (doc)
        {
            try
            {
                WalkLeaves("$", doc.RootElement, result.Matches);
                result.Ok = true;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = "eval:" + ex.Message;
            }
        }
        return result;
    }

    /// <summary>Flatten every node (containers included) into path=value entries.</summary>
    public static QueryResult Flatten(string json)
    {
        var result = new QueryResult();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "null" : json);
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Error = "json:" + ex.Message;
            return result;
        }
        using (doc)
        {
            try
            {
                WalkFlatten("$", doc.RootElement, result.Matches);
                result.Ok = true;
            }
            catch (Exception ex)
            {
                result.Ok = false;
                result.Error = "eval:" + ex.Message;
            }
        }
        return result;
    }

    // ---- evaluation ---------------------------------------------------------------------

    private static void Apply(Step step, string path, JsonElement el, List<(string, JsonElement)> outp)
    {
        switch (step.Kind)
        {
            case StepKind.Child:
                if (el.ValueKind == JsonValueKind.Object &&
                    el.TryGetProperty(step.Key, out var child))
                    outp.Add((path + Seg(step.Key), child));
                break;

            case StepKind.Index:
                if (el.ValueKind == JsonValueKind.Array)
                {
                    int len = el.GetArrayLength();
                    int idx = step.Index < 0 ? len + step.Index : step.Index;
                    if (idx >= 0 && idx < len)
                        outp.Add((path + "[" + idx + "]", el[idx]));
                }
                break;

            case StepKind.Wildcard:
                if (el.ValueKind == JsonValueKind.Object)
                    foreach (var p in el.EnumerateObject())
                        outp.Add((path + Seg(p.Name), p.Value));
                else if (el.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var item in el.EnumerateArray())
                        outp.Add((path + "[" + i++ + "]", item));
                }
                break;

            case StepKind.RecursiveKey:
                RecurseKey(step.Key, path, el, outp);
                break;

            case StepKind.RecursiveWildcard:
                RecurseAll(path, el, outp);
                break;
        }
    }

    private static void RecurseKey(string key, string path, JsonElement el, List<(string, JsonElement)> outp)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                if (p.Name == key)
                    outp.Add((path + Seg(p.Name), p.Value));
                RecurseKey(key, path + Seg(p.Name), p.Value, outp);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var item in el.EnumerateArray())
            {
                RecurseKey(key, path + "[" + i + "]", item, outp);
                i++;
            }
        }
    }

    private static void RecurseAll(string path, JsonElement el, List<(string, JsonElement)> outp)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                outp.Add((path + Seg(p.Name), p.Value));
                RecurseAll(path + Seg(p.Name), p.Value, outp);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var item in el.EnumerateArray())
            {
                outp.Add((path + "[" + i + "]", item));
                RecurseAll(path + "[" + i + "]", item, outp);
                i++;
            }
        }
    }

    private static void WalkLeaves(string path, JsonElement el, List<Match> outp)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                bool anyObj = false;
                foreach (var p in el.EnumerateObject()) { anyObj = true; WalkLeaves(path + Seg(p.Name), p.Value, outp); }
                if (!anyObj) outp.Add(new Match { Path = path, Value = "{}" });
                break;
            case JsonValueKind.Array:
                bool anyArr = false; int i = 0;
                foreach (var item in el.EnumerateArray()) { anyArr = true; WalkLeaves(path + "[" + i++ + "]", item, outp); }
                if (!anyArr) outp.Add(new Match { Path = path, Value = "[]" });
                break;
            default:
                outp.Add(new Match { Path = path, Value = Stringify(el) });
                break;
        }
    }

    private static void WalkFlatten(string path, JsonElement el, List<Match> outp)
    {
        outp.Add(new Match { Path = path, Value = Stringify(el) });
        if (el.ValueKind == JsonValueKind.Object)
            foreach (var p in el.EnumerateObject())
                WalkFlatten(path + Seg(p.Name), p.Value, outp);
        else if (el.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var item in el.EnumerateArray())
                WalkFlatten(path + "[" + i++ + "]", item, outp);
        }
    }

    // ---- query parsing ------------------------------------------------------------------

    private static List<Step> Parse(string query)
    {
        var steps = new List<Step>();
        if (string.IsNullOrWhiteSpace(query)) throw new FormatException("empty");
        string q = query.Trim();
        int i = 0;

        // optional leading $
        if (q[i] == '$') i++;

        while (i < q.Length)
        {
            char c = q[i];
            if (c == '.')
            {
                if (i + 1 < q.Length && q[i + 1] == '.')
                {
                    // recursive descent: ..key  or  ..*
                    i += 2;
                    if (i < q.Length && q[i] == '*')
                    {
                        steps.Add(new Step(StepKind.RecursiveWildcard));
                        i++;
                    }
                    else
                    {
                        string name = ReadName(q, ref i);
                        if (name.Length == 0) throw new FormatException("expected key after '..'");
                        steps.Add(new Step(StepKind.RecursiveKey, name));
                    }
                }
                else
                {
                    i++; // single '.'
                    if (i < q.Length && q[i] == '*') { steps.Add(new Step(StepKind.Wildcard)); i++; }
                    else
                    {
                        string name = ReadName(q, ref i);
                        if (name.Length == 0) throw new FormatException("expected key after '.'");
                        steps.Add(new Step(StepKind.Child, name));
                    }
                }
            }
            else if (c == '[')
            {
                int close = q.IndexOf(']', i);
                if (close < 0) throw new FormatException("unclosed '['");
                string inner = q.Substring(i + 1, close - i - 1).Trim();
                i = close + 1;
                if (inner == "*") steps.Add(new Step(StepKind.Wildcard));
                else if ((inner.StartsWith("'") && inner.EndsWith("'") && inner.Length >= 2) ||
                         (inner.StartsWith("\"") && inner.EndsWith("\"") && inner.Length >= 2))
                    steps.Add(new Step(StepKind.Child, inner.Substring(1, inner.Length - 2)));
                else if (int.TryParse(inner, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n))
                    steps.Add(new Step(StepKind.Index, index: n));
                else throw new FormatException("bad index '" + inner + "'");
            }
            else
            {
                // bare leading name (e.g. "a.b" without $)
                string name = ReadName(q, ref i);
                if (name.Length == 0) throw new FormatException("unexpected '" + c + "'");
                steps.Add(new Step(StepKind.Child, name));
            }
        }
        return steps;
    }

    private static string ReadName(string q, ref int i)
    {
        int start = i;
        while (i < q.Length)
        {
            char c = q[i];
            if (c == '.' || c == '[' || c == ']' || c == '*') break;
            i++;
        }
        return q.Substring(start, i - start).Trim();
    }

    // ---- helpers ------------------------------------------------------------------------

    /// <summary>Path segment: dotted for simple identifiers, bracketed-quoted otherwise.</summary>
    private static string Seg(string key)
    {
        bool simple = key.Length > 0;
        foreach (char c in key)
            if (!(char.IsLetterOrDigit(c) || c == '_')) { simple = false; break; }
        return simple ? "." + key : "['" + key.Replace("'", "\\'") + "']";
    }

    /// <summary>Compact string form of a JSON element for display.</summary>
    private static string Stringify(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String: return el.GetString() ?? "";
            case JsonValueKind.Number: return el.GetRawText();
            case JsonValueKind.True: return "true";
            case JsonValueKind.False: return "false";
            case JsonValueKind.Null: return "null";
            case JsonValueKind.Undefined: return "";
            default:
                try { return el.GetRawText(); }
                catch { return el.ToString(); }
        }
    }
}
