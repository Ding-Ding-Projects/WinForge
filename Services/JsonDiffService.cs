using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// JSON diff · JSON 比對 — parse two JSON documents with System.Text.Json and recursively
/// compare them, producing a flat list of path-keyed differences. Pure managed, never throws:
/// invalid input is reported via <see cref="DiffOutcome"/> rather than exceptions. No redirect.
/// </summary>
public static class JsonDiffService
{
    public enum DiffKind { Added, Removed, Changed, TypeChanged }

    public sealed class DiffRow
    {
        public string Path { get; init; } = "";
        public DiffKind Kind { get; init; }
        public string Status { get; init; } = "";
        public string Detail { get; init; } = "";
    }

    public sealed class DiffOutcome
    {
        public bool ParsedA { get; init; }
        public bool ParsedB { get; init; }
        public string? ErrorA { get; init; }
        public string? ErrorB { get; init; }
        public IReadOnlyList<DiffRow> Rows { get; init; } = Array.Empty<DiffRow>();
        public int Added { get; init; }
        public int Removed { get; init; }
        public int Changed { get; init; }
        public bool Ok => ParsedA && ParsedB;
    }

    /// <summary>Compare two JSON strings. Never throws; parse failures surface in the outcome.</summary>
    public static DiffOutcome Compare(string? a, string? b, bool ignoreArrayOrder)
    {
        JsonDocument? docA = null, docB = null;
        string? errA = null, errB = null;
        try { docA = JsonDocument.Parse(a ?? ""); }
        catch (Exception ex) { errA = ex is JsonException je ? je.Message : ex.Message; }
        try { docB = JsonDocument.Parse(b ?? ""); }
        catch (Exception ex) { errB = ex is JsonException je ? je.Message : ex.Message; }

        if (docA is null || docB is null)
        {
            docA?.Dispose(); docB?.Dispose();
            return new DiffOutcome { ParsedA = docA is not null, ParsedB = docB is not null, ErrorA = errA, ErrorB = errB };
        }

        var rows = new List<DiffRow>();
        try { Walk("$", docA.RootElement, docB.RootElement, ignoreArrayOrder, rows); }
        catch { /* never throw to the UI */ }
        finally { docA.Dispose(); docB.Dispose(); }

        int added = rows.Count(r => r.Kind == DiffKind.Added);
        int removed = rows.Count(r => r.Kind == DiffKind.Removed);
        int changed = rows.Count(r => r.Kind is DiffKind.Changed or DiffKind.TypeChanged);
        return new DiffOutcome
        {
            ParsedA = true,
            ParsedB = true,
            Rows = rows,
            Added = added,
            Removed = removed,
            Changed = changed,
        };
    }

    private static void Walk(string path, JsonElement a, JsonElement b, bool ignoreArrayOrder, List<DiffRow> rows)
    {
        if (a.ValueKind != b.ValueKind)
        {
            rows.Add(new DiffRow
            {
                Path = path,
                Kind = DiffKind.TypeChanged,
                Status = "Type changed · 型別改變",
                Detail = $"{Kind(a)} → {Kind(b)}",
            });
            return;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Object:
                WalkObject(path, a, b, ignoreArrayOrder, rows);
                break;
            case JsonValueKind.Array:
                if (ignoreArrayOrder) WalkArrayMultiset(path, a, b, rows);
                else WalkArrayOrdered(path, a, b, ignoreArrayOrder, rows);
                break;
            default:
                if (!ScalarEqual(a, b))
                {
                    rows.Add(new DiffRow
                    {
                        Path = path,
                        Kind = DiffKind.Changed,
                        Status = "Changed · 改變",
                        Detail = $"{Scalar(a)} → {Scalar(b)}",
                    });
                }
                break;
        }
    }

    private static void WalkObject(string path, JsonElement a, JsonElement b, bool ignoreArrayOrder, List<DiffRow> rows)
    {
        var bProps = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var p in b.EnumerateObject()) bProps[p.Name] = p.Value;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var p in a.EnumerateObject())
        {
            seen.Add(p.Name);
            string child = path + "." + p.Name;
            if (bProps.TryGetValue(p.Name, out var bv))
                Walk(child, p.Value, bv, ignoreArrayOrder, rows);
            else
                rows.Add(new DiffRow
                {
                    Path = child,
                    Kind = DiffKind.Removed,
                    Status = "Removed from B · B 缺少",
                    Detail = Preview(p.Value),
                });
        }

        foreach (var kv in bProps)
        {
            if (seen.Contains(kv.Key)) continue;
            rows.Add(new DiffRow
            {
                Path = path + "." + kv.Key,
                Kind = DiffKind.Added,
                Status = "Added in B · B 新增",
                Detail = Preview(kv.Value),
            });
        }
    }

    private static void WalkArrayOrdered(string path, JsonElement a, JsonElement b, bool ignoreArrayOrder, List<DiffRow> rows)
    {
        var av = a.EnumerateArray().ToList();
        var bv = b.EnumerateArray().ToList();
        int n = Math.Max(av.Count, bv.Count);
        for (int i = 0; i < n; i++)
        {
            string child = $"{path}[{i}]";
            if (i >= av.Count)
                rows.Add(new DiffRow { Path = child, Kind = DiffKind.Added, Status = "Added in B · B 新增", Detail = Preview(bv[i]) });
            else if (i >= bv.Count)
                rows.Add(new DiffRow { Path = child, Kind = DiffKind.Removed, Status = "Removed from B · B 缺少", Detail = Preview(av[i]) });
            else
                Walk(child, av[i], bv[i], ignoreArrayOrder, rows);
        }
    }

    private static void WalkArrayMultiset(string path, JsonElement a, JsonElement b, List<DiffRow> rows)
    {
        // Compare arrays as multisets: match equal elements regardless of position.
        var bItems = b.EnumerateArray().Select(Canonical).ToList();
        var used = new bool[bItems.Count];
        int idx = 0;
        foreach (var av in a.EnumerateArray())
        {
            string ac = Canonical(av);
            int match = -1;
            for (int j = 0; j < bItems.Count; j++)
            {
                if (!used[j] && bItems[j] == ac) { match = j; break; }
            }
            if (match >= 0) used[match] = true;
            else rows.Add(new DiffRow { Path = $"{path}[{idx}]", Kind = DiffKind.Removed, Status = "Removed from B · B 缺少", Detail = Preview(av) });
            idx++;
        }
        int bIdx = 0;
        foreach (var bv in b.EnumerateArray())
        {
            if (!used[bIdx])
                rows.Add(new DiffRow { Path = $"{path}[+{bIdx}]", Kind = DiffKind.Added, Status = "Added in B · B 新增", Detail = Preview(bv) });
            bIdx++;
        }
    }

    private static bool ScalarEqual(JsonElement a, JsonElement b)
    {
        switch (a.ValueKind)
        {
            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return a.GetRawText() == b.GetRawText();
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                return true;
            default:
                return a.GetRawText() == b.GetRawText();
        }
    }

    /// <summary>Order-independent canonical form for multiset array matching.</summary>
    private static string Canonical(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                var parts = e.EnumerateObject()
                    .OrderBy(p => p.Name, StringComparer.Ordinal)
                    .Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value));
                return "{" + string.Join(",", parts) + "}";
            case JsonValueKind.Array:
                return "[" + string.Join(",", e.EnumerateArray().Select(Canonical)) + "]";
            default:
                return e.GetRawText();
        }
    }

    private static string Kind(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Null => "null",
        _ => e.ValueKind.ToString().ToLowerInvariant(),
    };

    private static string Scalar(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => "\"" + (e.GetString() ?? "") + "\"",
        JsonValueKind.Null => "null",
        _ => e.GetRawText(),
    };

    private static string Preview(JsonElement e)
    {
        try
        {
            string raw = e.ValueKind is JsonValueKind.Object or JsonValueKind.Array
                ? e.GetRawText()
                : Scalar(e);
            raw = raw.Replace("\r", " ").Replace("\n", " ");
            const int max = 120;
            return raw.Length > max ? raw.Substring(0, max) + "…" : raw;
        }
        catch { return ""; }
    }
}
