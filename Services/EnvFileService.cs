using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// .env 檔案編輯／轉換（純受控 C#）· Dotenv parsing / conversion helper — pure managed C#.
/// Parses KEY=VALUE lines with "# comments", quoted "values"/'values' and an optional leading
/// `export `. Emits shell/JSON/docker/canonical output. Never throws — all IO returns a result.
/// </summary>
public static class EnvFileService
{
    /// <summary>一對鍵值 · One parsed key/value pair.</summary>
    public sealed class EnvPair
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }

    private static readonly Regex KeyRe = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    // ===== parse =====

    /// <summary>由原始文字解析成鍵值對 · Parse raw .env text into pairs. Comments/blank lines dropped.</summary>
    public static List<EnvPair> Parse(string? raw)
    {
        var list = new List<EnvPair>();
        if (string.IsNullOrEmpty(raw)) return list;
        try
        {
            foreach (var line0 in raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = line0.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;

                if (line.StartsWith("export ", StringComparison.Ordinal))
                    line = line.Substring(7).TrimStart();

                int eq = line.IndexOf('=');
                if (eq <= 0) continue; // no key, skip
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                val = Unquote(val);
                list.Add(new EnvPair { Key = key, Value = val });
            }
        }
        catch { /* never throw */ }
        return list;
    }

    private static string Unquote(string v)
    {
        if (v.Length >= 2 &&
            ((v[0] == '"' && v[^1] == '"') || (v[0] == '\'' && v[^1] == '\'')))
        {
            var inner = v.Substring(1, v.Length - 2);
            if (v[0] == '"')
                inner = inner.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
            return inner;
        }
        // strip a trailing inline comment on an unquoted value ( value # note )
        int hash = v.IndexOf(" #", StringComparison.Ordinal);
        if (hash >= 0) v = v.Substring(0, hash).TrimEnd();
        return v;
    }

    // ===== validation =====

    /// <summary>檢查重複鍵、無效鍵名、未加引號嘅空格值 · Warnings for dup keys, bad names, unquoted spaces.</summary>
    public static List<string> Validate(IEnumerable<EnvPair> pairs, Func<string, string, string> pick)
    {
        var warnings = new List<string>();
        try
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in pairs)
            {
                var k = p.Key ?? "";
                if (k.Length == 0)
                {
                    warnings.Add(pick("Empty key name.", "有一個鍵名係空嘅。"));
                    continue;
                }
                if (!KeyRe.IsMatch(k))
                    warnings.Add(pick($"Invalid key name \"{k}\" — must match [A-Za-z_][A-Za-z0-9_]*.",
                        $"無效鍵名「{k}」— 要符合 [A-Za-z_][A-Za-z0-9_]*。"));
                if (!seen.Add(k))
                    warnings.Add(pick($"Duplicate key \"{k}\" — later value wins.",
                        $"重複鍵「{k}」— 會用返後面嗰個值。"));
                if ((p.Value ?? "").Any(char.IsWhiteSpace))
                    warnings.Add(pick($"Value for \"{k}\" has whitespace — it will be quoted on export.",
                        $"「{k}」嘅值有空格 — 匯出時會自動加引號。"));
            }
        }
        catch { /* never throw */ }
        return warnings;
    }

    // ===== convert =====

    /// <summary>De-dupe keeping the last value, preserving first-seen order.</summary>
    private static List<EnvPair> Canonicalize(IEnumerable<EnvPair> pairs)
    {
        var order = new List<string>();
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in pairs)
        {
            var k = p.Key ?? "";
            if (k.Length == 0) continue;
            if (!map.ContainsKey(k)) order.Add(k);
            map[k] = p.Value ?? "";
        }
        return order.Select(k => new EnvPair { Key = k, Value = map[k] }).ToList();
    }

    private static bool NeedsQuote(string v) =>
        v.Length == 0 || v.Any(c => char.IsWhiteSpace(c) || c == '"' || c == '\'' || c == '#' || c == '$');

    private static string DoubleQuote(string v) =>
        "\"" + v.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    /// <summary>→ shell `export KEY="value"` 行 · Shell export lines.</summary>
    public static string ToShell(IEnumerable<EnvPair> pairs)
    {
        var sb = new StringBuilder();
        foreach (var p in Canonicalize(pairs))
            sb.Append("export ").Append(p.Key).Append('=').Append(DoubleQuote(p.Value)).Append('\n');
        return sb.ToString();
    }

    /// <summary>→ JSON 物件 · A JSON object.</summary>
    public static string ToJson(IEnumerable<EnvPair> pairs)
    {
        try
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in Canonicalize(pairs)) dict[p.Key] = p.Value;
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };
            return JsonSerializer.Serialize(dict, opts);
        }
        catch { return "{}"; }
    }

    /// <summary>→ docker `--env KEY=value` 引數 · Docker --env args (one per line).</summary>
    public static string ToDocker(IEnumerable<EnvPair> pairs)
    {
        var sb = new StringBuilder();
        foreach (var p in Canonicalize(pairs))
        {
            var v = NeedsQuote(p.Value) ? DoubleQuote(p.Value) : p.Value;
            sb.Append("--env ").Append(p.Key).Append('=').Append(v).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>→ 標準 .env 文字 · Canonical .env text (dup-collapsed, quoted when needed).</summary>
    public static string ToEnv(IEnumerable<EnvPair> pairs)
    {
        var sb = new StringBuilder();
        foreach (var p in Canonicalize(pairs))
        {
            var v = NeedsQuote(p.Value) ? DoubleQuote(p.Value) : p.Value;
            sb.Append(p.Key).Append('=').Append(v).Append('\n');
        }
        return sb.ToString();
    }

    // ===== IO (async, guarded) =====

    /// <summary>讀檔 · Read a file's text. Returns (ok, text, error).</summary>
    public static async Task<(bool ok, string text, string? error)> LoadAsync(string path)
    {
        try
        {
            var text = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
            return (true, text, null);
        }
        catch (Exception ex) { return (false, "", ex.Message); }
    }

    /// <summary>寫檔 · Write text to a file. Returns (ok, error).</summary>
    public static async Task<(bool ok, string? error)> SaveAsync(string path, string text)
    {
        try
        {
            await System.IO.File.WriteAllTextAsync(path, text).ConfigureAwait(false);
            return (true, null);
        }
        catch (Exception ex) { return (false, ex.Message); }
    }
}
