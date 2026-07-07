using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace WinForge.Services;

/// <summary>
/// JSON Merge Patch (RFC 7386) · JSON 合併修補（RFC 7386）.
/// The simple "shallow-merge, null-deletes" patch format:
///   • Generate — diff a source vs. a target JSON and emit a merge patch (an object where
///     deleted keys map to null, unchanged nested objects are omitted, and arrays / scalars
///     that differ are replaced whole).
///   • Apply — apply a merge patch to a document (recursively merge objects; a null value
///     deletes the key; any non-object patch value replaces the whole document).
/// Pure managed System.Text.Json.Nodes. Never throws — every path returns a Result.
/// </summary>
public static class JsonMergePatchService
{
    /// <summary>Outcome of a Generate/Apply operation. <see cref="Ok"/> gates <see cref="Json"/> vs. <see cref="Error"/>.</summary>
    public readonly record struct Result(bool Ok, string Json, string Error)
    {
        public static Result Success(string json) => new(true, json, string.Empty);
        public static Result Fail(string error) => new(false, string.Empty, error);
    }

    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private static readonly JsonDocumentOptions ParseOpts = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static bool TryParse(string text, string label, out JsonNode? node, out string error)
    {
        node = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = Loc.I.Pick($"{label} is empty — please paste JSON.", $"{label}係空嘅 — 請貼上 JSON。");
            return false;
        }
        try
        {
            // JsonNode.Parse returns null only for the literal JSON `null`, which is valid here.
            node = JsonNode.Parse(text, documentOptions: ParseOpts);
            return true;
        }
        catch (JsonException ex)
        {
            error = Loc.I.Pick($"{label}: invalid JSON — {ex.Message}", $"{label}：JSON 格式錯誤 — {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            error = Loc.I.Pick($"{label}: could not read JSON — {ex.Message}", $"{label}：讀取 JSON 失敗 — {ex.Message}");
            return false;
        }
    }

    /// <summary>Generate an RFC 7386 merge patch that turns <paramref name="sourceText"/> into <paramref name="targetText"/>.</summary>
    public static Result Generate(string sourceText, string targetText)
    {
        var srcLabel = Loc.I.Pick("Source", "來源");
        var tgtLabel = Loc.I.Pick("Target", "目標");
        if (!TryParse(sourceText, srcLabel, out var src, out var e1)) return Result.Fail(e1);
        if (!TryParse(targetText, tgtLabel, out var tgt, out var e2)) return Result.Fail(e2);

        try
        {
            var patch = Diff(src, tgt);
            return Result.Success(patch is null ? "null" : patch.ToJsonString(Pretty));
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.I.Pick($"Could not generate patch — {ex.Message}", $"產生修補失敗 — {ex.Message}"));
        }
    }

    /// <summary>Apply an RFC 7386 merge <paramref name="patchText"/> to <paramref name="docText"/> and return the result.</summary>
    public static Result Apply(string docText, string patchText)
    {
        var docLabel = Loc.I.Pick("Document", "文件");
        var patchLabel = Loc.I.Pick("Patch", "修補");
        if (!TryParse(docText, docLabel, out var doc, out var e1)) return Result.Fail(e1);
        if (!TryParse(patchText, patchLabel, out var patch, out var e2)) return Result.Fail(e2);

        try
        {
            var merged = Merge(doc, patch);
            return Result.Success(merged is null ? "null" : merged.ToJsonString(Pretty));
        }
        catch (Exception ex)
        {
            return Result.Fail(Loc.I.Pick($"Could not apply patch — {ex.Message}", $"套用修補失敗 — {ex.Message}"));
        }
    }

    // RFC 7386 §2 — MergePatch(target, patch)
    private static JsonNode? Merge(JsonNode? target, JsonNode? patch)
    {
        if (patch is not JsonObject patchObj)
            return patch?.DeepClone(); // non-object patch replaces the whole document

        // A non-object target becomes {} before merging (RFC 7386 §2).
        var result = target is JsonObject to ? (JsonObject)to.DeepClone() : new JsonObject();

        foreach (var kv in patchObj)
        {
            if (kv.Value is null)
            {
                result.Remove(kv.Key); // null deletes the key
            }
            else
            {
                result.TryGetPropertyValue(kv.Key, out var existing);
                var child = Merge(existing, kv.Value);
                result[kv.Key] = child?.DeepClone();
            }
        }
        return result;
    }

    // RFC 7386 §-derived diff: smallest patch whose Merge(source, patch) == target.
    private static JsonNode? Diff(JsonNode? source, JsonNode? target)
    {
        // If both are objects, recurse key-by-key.
        if (source is JsonObject srcObj && target is JsonObject tgtObj)
        {
            var patch = new JsonObject();

            // Keys present in target: add/update where they differ.
            foreach (var kv in tgtObj)
            {
                srcObj.TryGetPropertyValue(kv.Key, out var srcChild);
                if (srcChild is null && !srcObj.ContainsKey(kv.Key))
                {
                    patch[kv.Key] = kv.Value?.DeepClone(); // new key
                }
                else if (!DeepEquals(srcChild, kv.Value))
                {
                    var sub = Diff(srcChild, kv.Value);
                    patch[kv.Key] = sub?.DeepClone();
                }
            }

            // Keys removed in target: emit null to delete.
            foreach (var kv in srcObj)
            {
                if (!tgtObj.ContainsKey(kv.Key))
                    patch[kv.Key] = null;
            }

            return patch; // may be empty {} when nothing changed
        }

        // Otherwise (arrays / scalars / type change / null) — replace whole.
        return target?.DeepClone();
    }

    private static bool DeepEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null || b is null) return a is null && b is null;
        return JsonNode.DeepEquals(a, b);
    }
}
