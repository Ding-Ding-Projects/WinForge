using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace WinForge.Services;

/// <summary>
/// JSON 同 XML 格式化 / 驗證工具 · JSON &amp; XML formatter / validator.
/// Pure managed (System.Text.Json + System.Xml.Linq); no external processes, no NuGet.
/// Every public method returns a <see cref="ToolResult"/> — it never throws to the UI.
/// </summary>
public static class JsonToolsService
{
    /// <summary>Outcome of a tool operation. Ok = success; Output holds the produced text (or a message).</summary>
    public readonly record struct ToolResult(bool Ok, string Output, string Message, int NodeCount)
    {
        public static ToolResult Success(string output, string message, int nodes)
            => new(true, output, message, nodes);
        public static ToolResult Fail(string message)
            => new(false, string.Empty, message, 0);
    }

    // ── JSON ────────────────────────────────────────────────────────────────

    /// <summary>Pretty-print JSON (2-space indent). Optionally sorts object keys alphabetically.</summary>
    public static ToolResult FormatJson(string input, bool sortKeys, Func<string, string, string> pick)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ToolResult.Fail(EmptyMsg(pick));
        try
        {
            using var doc = JsonDocument.Parse(input, ReaderOptions);
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                WriteElement(doc.RootElement, writer, sortKeys);
            }
            var output = Encoding.UTF8.GetString(buffer.WrittenSpan);
            output = ReindentTwoSpaces(output);
            int nodes = CountJsonNodes(doc.RootElement);
            return ToolResult.Success(output, pick($"Formatted — {nodes} node(s).", $"已格式化 — {nodes} 個節點。"), nodes);
        }
        catch (JsonException jx)
        {
            return ToolResult.Fail(JsonError(jx, pick));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    /// <summary>Minify JSON to a single compact line. Optionally sorts object keys.</summary>
    public static ToolResult MinifyJson(string input, bool sortKeys, Func<string, string, string> pick)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ToolResult.Fail(EmptyMsg(pick));
        try
        {
            using var doc = JsonDocument.Parse(input, ReaderOptions);
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
            {
                WriteElement(doc.RootElement, writer, sortKeys);
            }
            var output = Encoding.UTF8.GetString(buffer.WrittenSpan);
            int nodes = CountJsonNodes(doc.RootElement);
            return ToolResult.Success(output, pick($"Minified — {output.Length} char(s).", $"已壓縮 — {output.Length} 個字元。"), nodes);
        }
        catch (JsonException jx)
        {
            return ToolResult.Fail(JsonError(jx, pick));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    /// <summary>Validate JSON. Output echoes the input on success.</summary>
    public static ToolResult ValidateJson(string input, Func<string, string, string> pick)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ToolResult.Fail(EmptyMsg(pick));
        try
        {
            using var doc = JsonDocument.Parse(input, ReaderOptions);
            int nodes = CountJsonNodes(doc.RootElement);
            return ToolResult.Success(input, pick($"✓ Valid JSON — {nodes} node(s).", $"✓ 有效嘅 JSON — {nodes} 個節點。"), nodes);
        }
        catch (JsonException jx)
        {
            return ToolResult.Fail(JsonError(jx, pick));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    /// <summary>Wrap the raw input as a JSON string literal (escape).</summary>
    public static ToolResult EscapeJson(string input, Func<string, string, string> pick)
    {
        if (input is null || input.Length == 0)
            return ToolResult.Fail(EmptyMsg(pick));
        try
        {
            var output = JsonSerializer.Serialize(input);
            return ToolResult.Success(output, pick($"Escaped — {output.Length} char(s).", $"已轉義 — {output.Length} 個字元。"), 1);
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    /// <summary>Unwrap a JSON string literal back to its raw text (unescape).</summary>
    public static ToolResult UnescapeJson(string input, Func<string, string, string> pick)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ToolResult.Fail(EmptyMsg(pick));
        try
        {
            var text = input.Trim();
            // Accept a bare (unquoted) string too by quoting it if needed.
            if (!(text.StartsWith('"') && text.EndsWith('"')))
                text = JsonSerializer.Serialize(text);
            var output = JsonSerializer.Deserialize<string>(text) ?? string.Empty;
            return ToolResult.Success(output, pick($"Unescaped — {output.Length} char(s).", $"已還原 — {output.Length} 個字元。"), 1);
        }
        catch (JsonException jx)
        {
            return ToolResult.Fail(JsonError(jx, pick));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    // ── XML ─────────────────────────────────────────────────────────────────

    /// <summary>Pretty-print XML via XDocument.ToString().</summary>
    public static ToolResult FormatXml(string input, Func<string, string, string> pick)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ToolResult.Fail(EmptyMsg(pick));
        try
        {
            var doc = XDocument.Parse(input, LoadOptions.None);
            var output = doc.ToString(SaveOptions.None);
            int nodes = doc.Descendants().Count();
            return ToolResult.Success(output, pick($"Formatted — {nodes} element(s).", $"已格式化 — {nodes} 個元素。"), nodes);
        }
        catch (XmlException xx)
        {
            return ToolResult.Fail(XmlError(xx, pick));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    /// <summary>Minify XML via SaveOptions.DisableFormatting.</summary>
    public static ToolResult MinifyXml(string input, Func<string, string, string> pick)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ToolResult.Fail(EmptyMsg(pick));
        try
        {
            var doc = XDocument.Parse(input, LoadOptions.None);
            var output = doc.ToString(SaveOptions.DisableFormatting);
            int nodes = doc.Descendants().Count();
            return ToolResult.Success(output, pick($"Minified — {output.Length} char(s).", $"已壓縮 — {output.Length} 個字元。"), nodes);
        }
        catch (XmlException xx)
        {
            return ToolResult.Fail(XmlError(xx, pick));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    /// <summary>Validate XML. Output echoes the input on success.</summary>
    public static ToolResult ValidateXml(string input, Func<string, string, string> pick)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ToolResult.Fail(EmptyMsg(pick));
        try
        {
            var doc = XDocument.Parse(input, LoadOptions.None);
            int nodes = doc.Descendants().Count();
            return ToolResult.Success(input, pick($"✓ Valid XML — {nodes} element(s).", $"✓ 有效嘅 XML — {nodes} 個元素。"), nodes);
        }
        catch (XmlException xx)
        {
            return ToolResult.Fail(XmlError(xx, pick));
        }
        catch (Exception ex)
        {
            return ToolResult.Fail(ex.Message);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static readonly JsonDocumentOptions ReaderOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private static string EmptyMsg(Func<string, string, string> pick)
        => pick("Nothing to process — paste some text into the input box first.",
                "冇嘢可以處理 — 請先喺輸入框貼啲文字。");

    private static string JsonError(JsonException jx, Func<string, string, string> pick)
    {
        var pos = jx.LineNumber is { } ln && jx.BytePositionInLine is { } bp
            ? pick($" (line {ln + 1}, position {bp})", $"（第 {ln + 1} 行，位置 {bp}）")
            : string.Empty;
        return pick("✗ Invalid JSON: ", "✗ JSON 無效：") + jx.Message + pos;
    }

    private static string XmlError(XmlException xx, Func<string, string, string> pick)
    {
        var pos = xx.LineNumber > 0
            ? pick($" (line {xx.LineNumber}, position {xx.LinePosition})", $"（第 {xx.LineNumber} 行，位置 {xx.LinePosition}）")
            : string.Empty;
        return pick("✗ Invalid XML: ", "✗ XML 無效：") + xx.Message + pos;
    }

    /// <summary>
    /// Utf8JsonWriter indents with 4 spaces by default on this runtime; the spec here asks
    /// for 2. Re-indent by halving the leading run of spaces on each line (structure only).
    /// </summary>
    private static string ReindentTwoSpaces(string json)
    {
        var lines = json.Split('\n');
        var sb = new StringBuilder(json.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            int spaces = 0;
            while (spaces < line.Length && line[spaces] == ' ') spaces++;
            if (spaces > 0)
                sb.Append(new string(' ', spaces / 2));
            sb.Append(line, spaces, line.Length - spaces);
            if (i < lines.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Recursively copy a JsonElement into a writer, optionally sorting object keys.</summary>
    private static void WriteElement(JsonElement element, Utf8JsonWriter writer, bool sortKeys)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                IEnumerable<JsonProperty> props = element.EnumerateObject();
                if (sortKeys)
                    props = props.OrderBy(p => p.Name, StringComparer.Ordinal);
                foreach (var prop in props)
                {
                    writer.WritePropertyName(prop.Name);
                    WriteElement(prop.Value, writer, sortKeys);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteElement(item, writer, sortKeys);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(element.GetBoolean());
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                writer.WriteRawValue(element.GetRawText());
                break;
        }
    }

    /// <summary>Count every value node (objects, arrays, and scalars) in a JSON tree.</summary>
    private static int CountJsonNodes(JsonElement element)
    {
        int count = 1;
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                    count += CountJsonNodes(prop.Value);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    count += CountJsonNodes(item);
                break;
        }
        return count;
    }
}
