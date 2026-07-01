using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 環境變數快照同差異 · Environment-variable snapshot &amp; diff.
/// Captures <see cref="Environment.GetEnvironmentVariables(EnvironmentVariableTarget)"/> into named,
/// timestamped JSON snapshots under %LOCALAPPDATA%\WinForge\envsnapshots, lists/deletes them, and
/// diffs two snapshots (or a snapshot vs the live environment). Pure managed; all IO guarded.
/// </summary>
public static class EnvDiffService
{
    /// <summary>A persisted, timestamped environment snapshot.</summary>
    public sealed class Snapshot
    {
        public string Name { get; set; } = "";
        public string Target { get; set; } = "Process";
        public DateTime CapturedUtc { get; set; }
        public Dictionary<string, string> Vars { get; set; } = new();
        // Set at load time; not serialised as a field the user edits.
        public string FileName { get; set; } = "";
    }

    /// <summary>One entry in a diff result.</summary>
    public sealed class DiffEntry
    {
        public string Key { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
        public string Display => OldValue.Length == 0 && NewValue.Length > 0 ? NewValue
            : NewValue.Length == 0 && OldValue.Length > 0 ? OldValue
            : $"{OldValue}  →  {NewValue}";
    }

    /// <summary>Result of comparing two variable sets: Added / Removed / Changed.</summary>
    public sealed class DiffResult
    {
        public List<DiffEntry> Added { get; } = new();
        public List<DiffEntry> Removed { get; } = new();
        public List<DiffEntry> Changed { get; } = new();
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    /// <summary>%LOCALAPPDATA%\WinForge\envsnapshots — created lazily, guarded.</summary>
    public static string Dir
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "WinForge", "envsnapshots");
        }
    }

    private static void EnsureDir()
    {
        try { Directory.CreateDirectory(Dir); } catch { /* best-effort */ }
    }

    /// <summary>Read the live environment for a target into a sorted dictionary. Never throws.</summary>
    public static Dictionary<string, string> ReadLive(EnvironmentVariableTarget target)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            IDictionary raw = Environment.GetEnvironmentVariables(target);
            foreach (DictionaryEntry e in raw)
            {
                string k = e.Key?.ToString() ?? "";
                if (k.Length == 0) continue;
                d[k] = e.Value?.ToString() ?? "";
            }
        }
        catch { /* some targets can throw under limited rights */ }
        return d;
    }

    /// <summary>Capture a named snapshot for a target and persist it as JSON. Off-thread; never throws.</summary>
    public static Task<Snapshot> CaptureAsync(string name, EnvironmentVariableTarget target) => Task.Run(() =>
    {
        var snap = new Snapshot
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"{target}-{DateTime.Now:yyyyMMdd-HHmmss}" : name.Trim(),
            Target = target.ToString(),
            CapturedUtc = DateTime.UtcNow,
            Vars = ReadLive(target),
        };
        try
        {
            EnsureDir();
            string file = Path.Combine(Dir, MakeFileName(snap.Name));
            string json = JsonSerializer.Serialize(snap, JsonOpts);
            File.WriteAllText(file, json, new UTF8Encoding(false));
            snap.FileName = Path.GetFileName(file);
        }
        catch { /* persistence best-effort; caller shows status */ }
        return snap;
    });

    /// <summary>Load every valid snapshot from disk, newest first. Corrupt files are skipped, never thrown.</summary>
    public static Task<List<Snapshot>> LoadAllAsync() => Task.Run(() =>
    {
        var list = new List<Snapshot>();
        try
        {
            EnsureDir();
            foreach (string path in Directory.EnumerateFiles(Dir, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var snap = JsonSerializer.Deserialize<Snapshot>(json);
                    if (snap == null) continue;
                    snap.Vars ??= new();
                    snap.FileName = Path.GetFileName(path);
                    list.Add(snap);
                }
                catch { /* skip corrupt file */ }
            }
        }
        catch { /* dir unreadable */ }
        return list.OrderByDescending(s => s.CapturedUtc).ToList();
    });

    /// <summary>Delete a snapshot file by name. Never throws.</summary>
    public static Task<bool> DeleteAsync(string fileName) => Task.Run(() =>
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            string path = Path.Combine(Dir, Path.GetFileName(fileName));
            if (File.Exists(path)) { File.Delete(path); return true; }
        }
        catch { /* best-effort */ }
        return false;
    });

    /// <summary>Compare two variable maps: Added (only in new), Removed (only in old), Changed (differing values).</summary>
    public static DiffResult Diff(IDictionary<string, string> oldVars, IDictionary<string, string> newVars)
    {
        var result = new DiffResult();
        oldVars ??= new Dictionary<string, string>();
        newVars ??= new Dictionary<string, string>();

        foreach (var kv in newVars.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!oldVars.TryGetValue(kv.Key, out var ov))
                result.Added.Add(new DiffEntry { Key = kv.Key, OldValue = "", NewValue = kv.Value ?? "" });
            else if (!string.Equals(ov ?? "", kv.Value ?? "", StringComparison.Ordinal))
                result.Changed.Add(new DiffEntry { Key = kv.Key, OldValue = ov ?? "", NewValue = kv.Value ?? "" });
        }
        foreach (var kv in oldVars.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!newVars.ContainsKey(kv.Key))
                result.Removed.Add(new DiffEntry { Key = kv.Key, OldValue = kv.Value ?? "", NewValue = "" });
        }
        return result;
    }

    /// <summary>Render a snapshot as plain text (sorted KEY=VALUE lines) for clipboard export.</summary>
    public static string ToPlainText(Snapshot snap)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# WinForge environment snapshot");
        sb.AppendLine($"# name:   {snap.Name}");
        sb.AppendLine($"# target: {snap.Target}");
        sb.AppendLine($"# time:   {snap.CapturedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"# count:  {snap.Vars.Count}");
        sb.AppendLine();
        foreach (var kv in snap.Vars.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"{kv.Key}={kv.Value}");
        return sb.ToString();
    }

    /// <summary>Render a diff as plain text (three grouped sections) for clipboard export.</summary>
    public static string ToPlainText(DiffResult diff, string leftLabel, string rightLabel)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# WinForge environment diff");
        sb.AppendLine($"# old: {leftLabel}");
        sb.AppendLine($"# new: {rightLabel}");
        sb.AppendLine();
        sb.AppendLine($"## Added ({diff.Added.Count})");
        foreach (var e in diff.Added) sb.AppendLine($"+ {e.Key}={e.NewValue}");
        sb.AppendLine();
        sb.AppendLine($"## Removed ({diff.Removed.Count})");
        foreach (var e in diff.Removed) sb.AppendLine($"- {e.Key}={e.OldValue}");
        sb.AppendLine();
        sb.AppendLine($"## Changed ({diff.Changed.Count})");
        foreach (var e in diff.Changed) sb.AppendLine($"~ {e.Key}: {e.OldValue}  =>  {e.NewValue}");
        return sb.ToString();
    }

    private static string MakeFileName(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name)
            sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
        string safe = sb.ToString().Trim();
        if (safe.Length == 0) safe = "snapshot";
        // Uniquify with a short timestamp so same-named captures don't clobber each other.
        return $"{safe}-{DateTime.Now:yyyyMMddHHmmssfff}.json";
    }
}
