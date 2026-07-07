using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// INI 解析／編輯（純受控）· INI parser / editor — pure managed, never-throws.
/// 解析 [section]、key=value、註解（; 或 #），可以來回轉換（清單 ⇄ 原文），
/// 保留分區分組。全部方法都防禦性寫法，唔會擲出例外。
/// Parses [sections], key=value pairs and comments (; or #). Round-trips list ⇄ raw
/// text, preserving section grouping. Every method is guarded and never throws.
/// </summary>
public static class IniEditService
{
    /// <summary>一條記錄 · One parsed entry (a key/value under a section, or an annotated skip).</summary>
    public sealed class IniEntry
    {
        public string Section { get; set; } = "";
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        /// <summary>備註（例如「malformed line」）· Note for malformed/skipped lines; empty for normal entries.</summary>
        public string Note { get; set; } = "";
    }

    /// <summary>
    /// 解析原文 · Parse raw INI text into a flat entry list. Malformed lines are annotated (Note set)
    /// but still surfaced so nothing is silently lost. Never throws.
    /// </summary>
    public static List<IniEntry> Parse(string? raw)
    {
        var list = new List<IniEntry>();
        if (string.IsNullOrEmpty(raw)) return list;
        try
        {
            string section = "";
            var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                if (line[0] == ';' || line[0] == '#') continue; // comment — dropped on round-trip by design

                if (line[0] == '[')
                {
                    int close = line.IndexOf(']');
                    if (close > 1)
                    {
                        section = line.Substring(1, close - 1).Trim();
                    }
                    else
                    {
                        list.Add(new IniEntry { Section = section, Key = line, Value = "", Note = "malformed section · 分區格式錯誤" });
                    }
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    list.Add(new IniEntry { Section = section, Key = line, Value = "", Note = "no key=value · 唔係 key=value" });
                    continue;
                }
                var key = line.Substring(0, eq).Trim();
                var val = line.Substring(eq + 1).Trim();
                if (key.Length == 0)
                {
                    list.Add(new IniEntry { Section = section, Key = line, Value = "", Note = "empty key · 空白鍵" });
                    continue;
                }
                list.Add(new IniEntry { Section = section, Key = key, Value = val });
            }
        }
        catch { /* never throw — return whatever parsed so far */ }
        return list;
    }

    /// <summary>
    /// 序列化 · Serialize entries back to canonical INI, grouping by section (order of first appearance).
    /// Malformed / annotated entries are skipped. Never throws.
    /// </summary>
    public static string Serialize(IEnumerable<IniEntry>? entries)
    {
        var sb = new StringBuilder();
        if (entries is null) return "";
        try
        {
            var good = entries.Where(e => e is not null && string.IsNullOrEmpty(e.Note) && !string.IsNullOrEmpty(e.Key)).ToList();
            // Preserve first-seen section order.
            var order = new List<string>();
            foreach (var e in good)
                if (!order.Contains(e.Section)) order.Add(e.Section);

            bool first = true;
            foreach (var sec in order)
            {
                if (!first) sb.Append('\n');
                first = false;
                if (!string.IsNullOrEmpty(sec)) sb.Append('[').Append(sec).Append("]\n");
                foreach (var e in good.Where(x => x.Section == sec))
                    sb.Append(e.Key).Append('=').Append(e.Value).Append('\n');
            }
        }
        catch { /* never throw */ }
        return sb.ToString();
    }

    /// <summary>取值 · Get the value for section+key (case-insensitive). Returns null if absent. Never throws.</summary>
    public static string? GetValue(IEnumerable<IniEntry>? entries, string? section, string? key)
    {
        if (entries is null || string.IsNullOrEmpty(key)) return null;
        try
        {
            section ??= "";
            foreach (var e in entries)
            {
                if (e is null || !string.IsNullOrEmpty(e.Note)) continue;
                if (string.Equals(e.Section, section, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
                    return e.Value;
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// 設定值 · Add or update section+key in the list (mutates it). Case-insensitive match.
    /// Returns true if updated an existing key, false if a new one was added. Never throws.
    /// </summary>
    public static bool SetValue(List<IniEntry>? entries, string? section, string? key, string? value)
    {
        if (entries is null || string.IsNullOrWhiteSpace(key)) return false;
        try
        {
            section = (section ?? "").Trim();
            key = key.Trim();
            value ??= "";
            foreach (var e in entries)
            {
                if (e is null || !string.IsNullOrEmpty(e.Note)) continue;
                if (string.Equals(e.Section, section, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    e.Value = value;
                    return true;
                }
            }
            entries.Add(new IniEntry { Section = section, Key = key, Value = value });
        }
        catch { }
        return false;
    }

    /// <summary>移除鍵 · Remove section+key. Returns count removed. Never throws.</summary>
    public static int Remove(List<IniEntry>? entries, string? section, string? key)
    {
        if (entries is null || string.IsNullOrEmpty(key)) return 0;
        try
        {
            section ??= "";
            return entries.RemoveAll(e => e is not null && string.IsNullOrEmpty(e.Note)
                && string.Equals(e.Section, section, StringComparison.OrdinalIgnoreCase)
                && string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
        }
        catch { return 0; }
    }

    /// <summary>非同步讀檔 · Read a file's text. Returns (text, error) — error null on success. Never throws.</summary>
    public static async Task<(string? Text, string? Error)> ReadFileAsync(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.File.Exists(path))
                return (null, "file not found · 搵唔到檔案");
            var text = await System.IO.File.ReadAllTextAsync(path).ConfigureAwait(false);
            return (text, null);
        }
        catch (Exception ex) { return (null, ex.Message); }
    }

    /// <summary>非同步寫檔 · Write text to a file. Returns error string, null on success. Never throws.</summary>
    public static async Task<string?> WriteFileAsync(string path, string? text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return "no path · 冇路徑";
            await System.IO.File.WriteAllTextAsync(path, text ?? "").ConfigureAwait(false);
            return null;
        }
        catch (Exception ex) { return ex.Message; }
    }
}
