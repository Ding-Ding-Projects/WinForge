using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 模板渲染引擎 · Template renderer engine. Pure managed. Substitutes {{key}} and nested
/// {{a.b.c}} placeholders from a data source (a JSON object OR flat key=value lines), and
/// supports a simple {{#if key}}…{{/if}} block. Never throws — malformed input is reported.
/// </summary>
public static class TextTemplateService
{
    /// <summary>Result of a render pass. <see cref="Ok"/> false means the data could not be parsed.</summary>
    public sealed class RenderResult
    {
        public bool Ok;
        public string Output = "";
        public string? ErrorEn;
        public string? ErrorZh;
        public int Substituted;
        public int Missing;
    }

    /// <summary>
    /// Parse <paramref name="data"/> (JSON object OR key=value lines) and render <paramref name="template"/>.
    /// When <paramref name="passthrough"/> is true, an unknown {{key}} is left verbatim; otherwise it becomes "".
    /// </summary>
    public static RenderResult Render(string? template, string? data, bool passthrough)
    {
        var result = new RenderResult();
        template ??= "";
        data ??= "";

        Dictionary<string, string> map;
        try
        {
            map = ParseData(data);
        }
        catch (JsonException jx)
        {
            result.Ok = false;
            result.Output = "";
            result.ErrorEn = "Malformed JSON data: " + jx.Message;
            result.ErrorZh = "JSON 資料格式錯誤：" + jx.Message;
            return result;
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Output = "";
            result.ErrorEn = "Could not read data: " + ex.Message;
            result.ErrorZh = "讀唔到資料：" + ex.Message;
            return result;
        }

        try
        {
            result.Output = Substitute(template, map, passthrough, result);
            result.Ok = true;
        }
        catch (Exception ex)
        {
            // Defensive: rendering should never throw, but guarantee it here.
            result.Ok = false;
            result.Output = "";
            result.ErrorEn = "Render failed: " + ex.Message;
            result.ErrorZh = "渲染失敗：" + ex.Message;
        }
        return result;
    }

    /// <summary>Turn either a JSON object or key=value lines into a flat, dotted-path lookup map.</summary>
    private static Dictionary<string, string> ParseData(string data)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string trimmed = data.TrimStart();

        if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
        {
            using var doc = JsonDocument.Parse(data);
            Flatten("", doc.RootElement, map);
            return map;
        }

        // Fallback: key=value (or key: value) lines, one per line.
        foreach (var raw in data.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            int colon = line.IndexOf(':');
            int sep = eq >= 0 && (colon < 0 || eq < colon) ? eq : colon;
            if (sep <= 0) continue;
            string key = line.Substring(0, sep).Trim();
            string val = line.Substring(sep + 1).Trim();
            if (key.Length > 0) map[key] = val;
        }
        return map;
    }

    /// <summary>Recursively flatten a JSON element into dotted paths (a.b.c → value).</summary>
    private static void Flatten(string prefix, JsonElement el, Dictionary<string, string> map)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    string key = prefix.Length == 0 ? prop.Name : prefix + "." + prop.Name;
                    Flatten(key, prop.Value, map);
                }
                break;
            case JsonValueKind.Array:
                int i = 0;
                foreach (var item in el.EnumerateArray())
                {
                    string key = prefix.Length == 0 ? i.ToString() : prefix + "." + i;
                    Flatten(key, item, map);
                    i++;
                }
                break;
            case JsonValueKind.String:
                if (prefix.Length > 0) map[prefix] = el.GetString() ?? "";
                break;
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                if (prefix.Length > 0) map[prefix] = el.GetRawText();
                break;
            case JsonValueKind.Null:
                if (prefix.Length > 0) map[prefix] = "";
                break;
        }
    }

    private static bool Truthy(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return false;
        var t = v.Trim();
        if (t == "0") return false;
        if (t.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        if (t.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
        if (t.Equals("null", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>Handle {{#if key}}…{{/if}} blocks, then substitute {{placeholder}} tokens.</summary>
    private static string Substitute(string template, Dictionary<string, string> map, bool passthrough, RenderResult result)
    {
        string withIfs = ProcessIfBlocks(template, map);
        return ReplaceTokens(withIfs, map, passthrough, result);
    }

    /// <summary>Resolve {{#if key}}body{{/if}} — body kept only when the key is truthy. Innermost-first, robust to nesting.</summary>
    private static string ProcessIfBlocks(string template, Dictionary<string, string> map)
    {
        const string openStart = "{{#if";
        const string close = "{{/if}}";
        // Repeatedly resolve the innermost block (an open with no open before its matching close).
        for (int guard = 0; guard < 500; guard++)
        {
            int closeIdx = template.IndexOf(close, StringComparison.Ordinal);
            if (closeIdx < 0) break;

            // Find the nearest #if open before this close.
            int openIdx = template.LastIndexOf(openStart, closeIdx, StringComparison.Ordinal);
            if (openIdx < 0) break; // dangling {{/if}} — leave the rest alone.

            int openTagEnd = template.IndexOf("}}", openIdx, StringComparison.Ordinal);
            if (openTagEnd < 0 || openTagEnd > closeIdx) break; // malformed open — stop cleanly.

            string keyExpr = template.Substring(openIdx + openStart.Length, openTagEnd - (openIdx + openStart.Length)).Trim();
            string body = template.Substring(openTagEnd + 2, closeIdx - (openTagEnd + 2));

            map.TryGetValue(keyExpr, out var val);
            string replacement = Truthy(val) ? body : "";

            template = template.Substring(0, openIdx) + replacement + template.Substring(closeIdx + close.Length);
        }
        return template;
    }

    /// <summary>Replace {{key}} tokens (ignores {{#if}}/{{/if}} which are handled earlier).</summary>
    private static string ReplaceTokens(string template, Dictionary<string, string> map, bool passthrough, RenderResult result)
    {
        var sb = new StringBuilder(template.Length + 32);
        int i = 0;
        while (i < template.Length)
        {
            int open = template.IndexOf("{{", i, StringComparison.Ordinal);
            if (open < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }
            sb.Append(template, i, open - i);

            int end = template.IndexOf("}}", open + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                // No closing braces — emit the rest verbatim.
                sb.Append(template, open, template.Length - open);
                break;
            }

            string token = template.Substring(open + 2, end - (open + 2)).Trim();

            // Skip control tokens if any slipped through.
            if (token.StartsWith("#if") || token == "/if")
            {
                i = end + 2;
                continue;
            }

            if (map.TryGetValue(token, out var val))
            {
                sb.Append(val);
                result.Substituted++;
            }
            else
            {
                result.Missing++;
                if (passthrough) sb.Append("{{").Append(token).Append("}}");
                // else: empty string.
            }
            i = end + 2;
        }
        return sb.ToString();
    }
}
