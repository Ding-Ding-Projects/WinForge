using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// JSONL / NDJSON 工具 · JSONL / NDJSON tools. Pure managed System.Text.Json.
/// Every method is robust: a bad line is reported in the result, never throws.
/// </summary>
public static class JsonlToolsService
{
    /// <summary>Result of a JSONL operation. <see cref="Ok"/> false means a hard failure (e.g. not an array).</summary>
    public sealed class JsonlResult
    {
        public bool Ok { get; init; }
        public string Output { get; init; } = string.Empty;
        public int Records { get; init; }
        public int ValidLines { get; init; }
        public int InvalidLines { get; init; }
        public List<string> Errors { get; init; } = new();
    }

    private static readonly JsonWriterOptions PrettyOpts = new() { Indented = true };
    private static readonly JsonWriterOptions CompactOpts = new() { Indented = false };

    /// <summary>Parse each non-blank line; report how many are valid/invalid, with the line number + error of each bad one.</summary>
    public static JsonlResult Validate(string? input)
    {
        var errors = new List<string>();
        int valid = 0, invalid = 0;
        try
        {
            string[] lines = SplitLines(input);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    valid++;
                }
                catch (Exception ex)
                {
                    invalid++;
                    errors.Add($"Line {i + 1}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
        return new JsonlResult { Ok = true, Records = valid, ValidLines = valid, InvalidLines = invalid, Errors = errors };
    }

    /// <summary>Wrap every non-blank line into a single pretty-printed JSON array. Bad lines are skipped and reported.</summary>
    public static JsonlResult ToArray(string? input)
    {
        var errors = new List<string>();
        int valid = 0, invalid = 0;
        try
        {
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, PrettyOpts))
            {
                writer.WriteStartArray();
                string[] lines = SplitLines(input);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        doc.RootElement.WriteTo(writer);
                        valid++;
                    }
                    catch (Exception ex)
                    {
                        invalid++;
                        errors.Add($"Line {i + 1}: {ex.Message}");
                    }
                }
                writer.WriteEndArray();
            }
            string outText = Encoding.UTF8.GetString(stream.ToArray());
            return new JsonlResult { Ok = true, Output = outText, Records = valid, ValidLines = valid, InvalidLines = invalid, Errors = errors };
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    /// <summary>Take a JSON array and emit one compact JSON value per line.</summary>
    public static JsonlResult FromArray(string? input)
    {
        try
        {
            string text = (input ?? string.Empty).Trim();
            if (text.Length == 0)
                return new JsonlResult { Ok = false, Errors = { "Input is empty — expected a JSON array." } };

            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new JsonlResult { Ok = false, Errors = { $"Root is {doc.RootElement.ValueKind}, expected a JSON array." } };

            var sb = new StringBuilder();
            int count = 0;
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                using var stream = new System.IO.MemoryStream();
                using (var writer = new Utf8JsonWriter(stream, CompactOpts))
                {
                    item.WriteTo(writer);
                }
                sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
                sb.Append('\n');
                count++;
            }
            return new JsonlResult { Ok = true, Output = sb.ToString(), Records = count, ValidLines = count };
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    /// <summary>Pretty-print each non-blank line onto its own indented block. Bad lines are passed through and reported.</summary>
    public static JsonlResult PrettyEach(string? input) => TransformEach(input, PrettyOpts);

    /// <summary>Minify each non-blank line to a single compact line. Bad lines are passed through and reported.</summary>
    public static JsonlResult MinifyEach(string? input) => TransformEach(input, CompactOpts);

    private static JsonlResult TransformEach(string? input, JsonWriterOptions opts)
    {
        var errors = new List<string>();
        int valid = 0, invalid = 0;
        try
        {
            var sb = new StringBuilder();
            string[] lines = SplitLines(input);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    using var stream = new System.IO.MemoryStream();
                    using (var writer = new Utf8JsonWriter(stream, opts))
                    {
                        doc.RootElement.WriteTo(writer);
                    }
                    sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
                    sb.Append('\n');
                    valid++;
                }
                catch (Exception ex)
                {
                    invalid++;
                    errors.Add($"Line {i + 1}: {ex.Message}");
                    sb.Append(lines[i]);
                    sb.Append('\n');
                }
            }
            return new JsonlResult { Ok = true, Output = sb.ToString(), Records = valid, ValidLines = valid, InvalidLines = invalid, Errors = errors };
        }
        catch (Exception ex)
        {
            return Fail(ex);
        }
    }

    private static string[] SplitLines(string? input)
        => (input ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static JsonlResult Fail(Exception ex)
        => new() { Ok = false, Errors = { ex.Message } };
}
