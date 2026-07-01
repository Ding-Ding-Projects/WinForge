using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.Services;

/// <summary>
/// JSON Patch (RFC 6902) · JSON 修補 — pure-managed engine that (a) diffs source→target into a
/// standard RFC 6902 patch array and (b) applies such a patch to a document via JsonNode navigation.
/// JSON-Pointer paths per RFC 6901 (escape ~ → ~0, / → ~1). Never throws — returns Ok=false + message.
/// </summary>
public static class JsonPatchService
{
    public readonly record struct Result(bool Ok, string Output, string? Error)
    {
        public static Result Good(string output) => new(true, output, null);
        public static Result Bad(string error) => new(false, "", error);
    }

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private static readonly JsonDocumentOptions DocOpts = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly JsonNodeOptions NodeOpts = new() { PropertyNameCaseInsensitive = false };

    // ---- JSON-Pointer helpers (RFC 6901) ---------------------------------

    private static string EscapeToken(string token) => token.Replace("~", "~0").Replace("/", "~1");
    private static string UnescapeToken(string token) => token.Replace("~1", "/").Replace("~0", "~");

    private static string[] SplitPointer(string pointer)
    {
        if (string.IsNullOrEmpty(pointer)) return Array.Empty<string>();
        var parts = pointer.Split('/');
        // pointer starts with '/', so parts[0] is empty
        var tokens = new string[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++) tokens[i - 1] = UnescapeToken(parts[i]);
        return tokens;
    }

    // ================= DIFF =================

    /// <summary>Generate an RFC 6902 patch array transforming <paramref name="sourceJson"/> into <paramref name="targetJson"/>.</summary>
    public static Result Diff(string sourceJson, string targetJson, Func<string, string, string> pick)
    {
        string P(string en, string zh) => pick(en, zh);
        JsonNode? src, tgt;
        try { src = JsonNode.Parse(sourceJson, NodeOpts, DocOpts); }
        catch (JsonException ex) { return Result.Bad(P("Source JSON is not valid: ", "來源 JSON 無效：") + ex.Message); }
        try { tgt = JsonNode.Parse(targetJson, NodeOpts, DocOpts); }
        catch (JsonException ex) { return Result.Bad(P("Target JSON is not valid: ", "目標 JSON 無效：") + ex.Message); }

        try
        {
            var ops = new JsonArray();
            BuildDiff("", src, tgt, ops);
            return Result.Good(ops.ToJsonString(Pretty));
        }
        catch (Exception ex)
        {
            return Result.Bad(P("Could not build a patch: ", "無法產生修補：") + ex.Message);
        }
    }

    private static void BuildDiff(string path, JsonNode? from, JsonNode? to, JsonArray ops)
    {
        if (JsonNode.DeepEquals(from, to)) return;

        var fromKind = KindOf(from);
        var toKind = KindOf(to);

        if (fromKind == NodeKind.Object && toKind == NodeKind.Object)
        {
            var fo = from!.AsObject();
            var too = to!.AsObject();
            // removals & replacements/recursion
            foreach (var kv in fo)
            {
                string child = path + "/" + EscapeToken(kv.Key);
                if (too.TryGetPropertyValue(kv.Key, out var tval))
                    BuildDiff(child, kv.Value, tval, ops);
                else
                    ops.Add(MakeOp("remove", child, null));
            }
            // additions
            foreach (var kv in too)
            {
                if (!fo.ContainsKey(kv.Key))
                    ops.Add(MakeOp("add", path + "/" + EscapeToken(kv.Key), Clone(kv.Value)));
            }
        }
        else if (fromKind == NodeKind.Array && toKind == NodeKind.Array)
        {
            var fa = from!.AsArray();
            var ta = to!.AsArray();
            int min = Math.Min(fa.Count, ta.Count);
            for (int i = 0; i < min; i++)
                BuildDiff(path + "/" + i, fa[i], ta[i], ops);
            if (ta.Count > fa.Count)
            {
                for (int i = fa.Count; i < ta.Count; i++)
                    ops.Add(MakeOp("add", path + "/-", Clone(ta[i])));
            }
            else if (fa.Count > ta.Count)
            {
                // remove from the tail backwards so indices stay valid
                for (int i = fa.Count - 1; i >= ta.Count; i--)
                    ops.Add(MakeOp("remove", path + "/" + i, null));
            }
        }
        else
        {
            // scalars, or type changed → replace (or add if the root/source was absent)
            ops.Add(MakeOp("replace", path.Length == 0 ? "" : path, Clone(to)));
        }
    }

    private static JsonObject MakeOp(string op, string path, JsonNode? value)
    {
        var o = new JsonObject { ["op"] = op, ["path"] = path };
        if (op is "add" or "replace" or "test") o["value"] = value;
        return o;
    }

    // ================= APPLY =================

    /// <summary>Apply an RFC 6902 <paramref name="patchJson"/> array to <paramref name="docJson"/>.</summary>
    public static Result Apply(string docJson, string patchJson, Func<string, string, string> pick)
    {
        string P(string en, string zh) => pick(en, zh);
        JsonNode? doc;
        JsonNode? patchNode;
        try { doc = JsonNode.Parse(docJson, NodeOpts, DocOpts); }
        catch (JsonException ex) { return Result.Bad(P("Document JSON is not valid: ", "文件 JSON 無效：") + ex.Message); }
        try { patchNode = JsonNode.Parse(patchJson, NodeOpts, DocOpts); }
        catch (JsonException ex) { return Result.Bad(P("Patch JSON is not valid: ", "修補 JSON 無效：") + ex.Message); }

        if (patchNode is not JsonArray patch)
            return Result.Bad(P("A patch must be a JSON array of operations.", "修補必須係一個運算陣列（JSON array）。"));

        int index = 0;
        foreach (var item in patch)
        {
            index++;
            if (item is not JsonObject op)
                return Result.Bad(P($"Operation #{index} is not an object.", $"第 {index} 個運算唔係物件（object）。"));

            string? kind = (op.TryGetPropertyValue("op", out var opn) ? opn?.GetValue<string>() : null)?.Trim();
            if (string.IsNullOrEmpty(kind))
                return Result.Bad(P($"Operation #{index} is missing an \"op\".", $"第 {index} 個運算冇 \"op\"。"));

            string? pathVal = op.TryGetPropertyValue("path", out var pn) ? pn?.GetValue<string>() : null;
            if (pathVal is null)
                return Result.Bad(P($"Operation #{index} is missing a \"path\".", $"第 {index} 個運算冇 \"path\"。"));
            if (pathVal.Length != 0 && pathVal[0] != '/')
                return Result.Bad(P($"Operation #{index}: path \"{pathVal}\" must start with '/'.", $"第 {index} 個運算：路徑 \"{pathVal}\" 要以 '/' 開頭。"));

            string err;
            switch (kind)
            {
                case "add":
                    if (!TryGetValueField(op, out var addVal, out err, index, P)) return Result.Bad(err);
                    if (!DoAdd(ref doc, pathVal, addVal, out err, index, P)) return Result.Bad(err);
                    break;
                case "remove":
                    if (!DoRemove(ref doc, pathVal, out err, index, P, out _)) return Result.Bad(err);
                    break;
                case "replace":
                    if (!TryGetValueField(op, out var repVal, out err, index, P)) return Result.Bad(err);
                    if (!DoReplace(ref doc, pathVal, repVal, out err, index, P)) return Result.Bad(err);
                    break;
                case "test":
                    if (!TryGetValueField(op, out var testVal, out err, index, P)) return Result.Bad(err);
                    if (!DoTest(doc, pathVal, testVal, out err, index, P)) return Result.Bad(err);
                    break;
                case "copy":
                case "move":
                    string? fromVal = op.TryGetPropertyValue("from", out var fn) ? fn?.GetValue<string>() : null;
                    if (fromVal is null)
                        return Result.Bad(P($"Operation #{index} ({kind}) is missing a \"from\".", $"第 {index} 個運算（{kind}）冇 \"from\"。"));
                    if (fromVal.Length != 0 && fromVal[0] != '/')
                        return Result.Bad(P($"Operation #{index}: from \"{fromVal}\" must start with '/'.", $"第 {index} 個運算：from \"{fromVal}\" 要以 '/' 開頭。"));
                    if (!Resolve(doc, fromVal, out var moved, out err, index, P)) return Result.Bad(err);
                    var copyOfMoved = Clone(moved);
                    if (kind == "move")
                    {
                        if (fromVal.Length != 0 && (pathVal == fromVal || pathVal.StartsWith(fromVal + "/", StringComparison.Ordinal)))
                            return Result.Bad(P($"Operation #{index}: cannot move a location into itself.", $"第 {index} 個運算：唔可以將位置移入自己入面。"));
                        if (!DoRemove(ref doc, fromVal, out err, index, P, out _)) return Result.Bad(err);
                    }
                    if (!DoAdd(ref doc, pathVal, copyOfMoved, out err, index, P)) return Result.Bad(err);
                    break;
                default:
                    return Result.Bad(P($"Operation #{index}: unknown op \"{kind}\".", $"第 {index} 個運算：唔識嘅 op \"{kind}\"。"));
            }
        }

        return Result.Good(doc?.ToJsonString(Pretty) ?? "null");
    }

    private static bool TryGetValueField(JsonObject op, out JsonNode? value, out string err, int index, Func<string, string, string> P)
    {
        err = "";
        if (!op.ContainsKey("value"))
        {
            value = null;
            err = P($"Operation #{index} is missing a \"value\".", $"第 {index} 個運算冇 \"value\"。");
            return false;
        }
        value = op["value"];
        return true;
    }

    private static bool DoAdd(ref JsonNode? doc, string path, JsonNode? value, out string err, int index, Func<string, string, string> P)
    {
        err = "";
        var tokens = SplitPointer(path);
        if (tokens.Length == 0) { doc = Clone(value); return true; }

        if (!ResolveParent(doc, tokens, out var parent, out err, index, P)) return false;
        string last = tokens[^1];

        if (parent is JsonObject obj)
        {
            obj[last] = Clone(value);
            return true;
        }
        if (parent is JsonArray arr)
        {
            if (last == "-") { arr.Add(Clone(value)); return true; }
            if (!int.TryParse(last, out int i) || i < 0 || i > arr.Count)
            {
                err = P($"Operation #{index}: array index \"{last}\" is out of range for add.", $"第 {index} 個運算：陣列索引 \"{last}\" 超出範圍（add）。");
                return false;
            }
            arr.Insert(i, Clone(value));
            return true;
        }
        err = P($"Operation #{index}: cannot add under a non-container at \"{path}\".", $"第 {index} 個運算：唔可以喺非容器 \"{path}\" 加嘢。");
        return false;
    }

    private static bool DoRemove(ref JsonNode? doc, string path, out string err, int index, Func<string, string, string> P, out JsonNode? removed)
    {
        err = "";
        removed = null;
        var tokens = SplitPointer(path);
        if (tokens.Length == 0) { removed = doc; doc = null; return true; }

        if (!ResolveParent(doc, tokens, out var parent, out err, index, P)) return false;
        string last = tokens[^1];

        if (parent is JsonObject obj)
        {
            if (!obj.ContainsKey(last))
            {
                err = P($"Operation #{index}: nothing to remove at \"{path}\".", $"第 {index} 個運算：\"{path}\" 冇嘢可以移除。");
                return false;
            }
            removed = obj[last];
            obj.Remove(last);
            return true;
        }
        if (parent is JsonArray arr)
        {
            if (!int.TryParse(last, out int i) || i < 0 || i >= arr.Count)
            {
                err = P($"Operation #{index}: array index \"{last}\" is out of range for remove.", $"第 {index} 個運算：陣列索引 \"{last}\" 超出範圍（remove）。");
                return false;
            }
            removed = arr[i];
            arr.RemoveAt(i);
            return true;
        }
        err = P($"Operation #{index}: cannot remove from a non-container at \"{path}\".", $"第 {index} 個運算：唔可以喺非容器 \"{path}\" 移除。");
        return false;
    }

    private static bool DoReplace(ref JsonNode? doc, string path, JsonNode? value, out string err, int index, Func<string, string, string> P)
    {
        err = "";
        var tokens = SplitPointer(path);
        if (tokens.Length == 0) { doc = Clone(value); return true; }

        if (!ResolveParent(doc, tokens, out var parent, out err, index, P)) return false;
        string last = tokens[^1];

        if (parent is JsonObject obj)
        {
            if (!obj.ContainsKey(last))
            {
                err = P($"Operation #{index}: nothing to replace at \"{path}\".", $"第 {index} 個運算：\"{path}\" 冇嘢可以取代。");
                return false;
            }
            obj[last] = Clone(value);
            return true;
        }
        if (parent is JsonArray arr)
        {
            if (!int.TryParse(last, out int i) || i < 0 || i >= arr.Count)
            {
                err = P($"Operation #{index}: array index \"{last}\" is out of range for replace.", $"第 {index} 個運算：陣列索引 \"{last}\" 超出範圍（replace）。");
                return false;
            }
            arr[i] = Clone(value);
            return true;
        }
        err = P($"Operation #{index}: cannot replace inside a non-container at \"{path}\".", $"第 {index} 個運算：唔可以喺非容器 \"{path}\" 取代。");
        return false;
    }

    private static bool DoTest(JsonNode? doc, string path, JsonNode? value, out string err, int index, Func<string, string, string> P)
    {
        if (!Resolve(doc, path, out var actual, out err, index, P)) return false;
        if (!JsonNode.DeepEquals(actual, value))
        {
            err = P($"Operation #{index}: test failed at \"{path}\" — value did not match.", $"第 {index} 個運算：\"{path}\" 測試失敗 — 值唔一致。");
            return false;
        }
        return true;
    }

    private static bool ResolveParent(JsonNode? doc, string[] tokens, out JsonNode? parent, out string err, int index, Func<string, string, string> P)
    {
        err = "";
        parent = doc;
        for (int t = 0; t < tokens.Length - 1; t++)
        {
            if (parent is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(tokens[t], out parent))
                {
                    err = P($"Operation #{index}: path segment \"{tokens[t]}\" does not exist.", $"第 {index} 個運算：路徑段 \"{tokens[t]}\" 唔存在。");
                    return false;
                }
            }
            else if (parent is JsonArray arr)
            {
                if (!int.TryParse(tokens[t], out int i) || i < 0 || i >= arr.Count)
                {
                    err = P($"Operation #{index}: array index \"{tokens[t]}\" is out of range.", $"第 {index} 個運算：陣列索引 \"{tokens[t]}\" 超出範圍。");
                    return false;
                }
                parent = arr[i];
            }
            else
            {
                err = P($"Operation #{index}: cannot navigate into a non-container at \"{tokens[t]}\".", $"第 {index} 個運算：唔可以進入非容器 \"{tokens[t]}\"。");
                return false;
            }
        }
        return true;
    }

    private static bool Resolve(JsonNode? doc, string path, out JsonNode? node, out string err, int index, Func<string, string, string> P)
    {
        err = "";
        node = doc;
        var tokens = SplitPointer(path);
        foreach (var tok in tokens)
        {
            if (node is JsonObject obj)
            {
                if (!obj.TryGetPropertyValue(tok, out node))
                {
                    err = P($"Operation #{index}: path \"{path}\" does not exist.", $"第 {index} 個運算：路徑 \"{path}\" 唔存在。");
                    return false;
                }
            }
            else if (node is JsonArray arr)
            {
                if (!int.TryParse(tok, out int i) || i < 0 || i >= arr.Count)
                {
                    err = P($"Operation #{index}: path \"{path}\" does not exist.", $"第 {index} 個運算：路徑 \"{path}\" 唔存在。");
                    return false;
                }
                node = arr[i];
            }
            else
            {
                err = P($"Operation #{index}: path \"{path}\" does not exist.", $"第 {index} 個運算：路徑 \"{path}\" 唔存在。");
                return false;
            }
        }
        return true;
    }

    // ---- utilities -------------------------------------------------------

    private enum NodeKind { Null, Object, Array, Value }

    private static NodeKind KindOf(JsonNode? n) => n switch
    {
        null => NodeKind.Null,
        JsonObject => NodeKind.Object,
        JsonArray => NodeKind.Array,
        _ => NodeKind.Value,
    };

    private static JsonNode? Clone(JsonNode? n) => n?.DeepClone();
}
