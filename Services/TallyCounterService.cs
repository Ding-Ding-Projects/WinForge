using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 點數計數器 · Tally Counter store — a list of named counters persisted to
/// %LOCALAPPDATA%\WinForge\tally\counters.json. Pure managed C# (System.IO + System.Text.Json).
/// All disk access is off-thread (Task.Run) and fully guarded — never throws to callers.
/// </summary>
public static class TallyCounterService
{
    /// <summary>A single persisted counter (name + value).</summary>
    public sealed class Counter
    {
        public string Name { get; set; } = "";
        public long Value { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinForge", "tally");

    private static string FilePath => Path.Combine(Dir, "counters.json");

    /// <summary>Load counters from disk. Returns an empty list on any missing/corrupt/error condition — never throws.</summary>
    public static async Task<List<Counter>> LoadAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return new List<Counter>();
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return new List<Counter>();
                var list = JsonSerializer.Deserialize<List<Counter>>(json);
                if (list == null) return new List<Counter>();
                // Sanitise: drop nulls, coalesce names.
                var clean = new List<Counter>();
                foreach (var c in list)
                {
                    if (c == null) continue;
                    clean.Add(new Counter { Name = c.Name ?? "", Value = c.Value });
                }
                return clean;
            }
            catch
            {
                return new List<Counter>();
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Save counters to disk (atomic-ish via temp + replace). Returns true on success; never throws.</summary>
    public static async Task<bool> SaveAsync(IEnumerable<Counter> counters)
    {
        var snapshot = new List<Counter>();
        try
        {
            foreach (var c in counters)
            {
                if (c == null) continue;
                snapshot.Add(new Counter { Name = c.Name ?? "", Value = c.Value });
            }
        }
        catch
        {
            return false;
        }

        return await Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(Dir);
                string path = FilePath;
                string tmp = path + ".tmp";
                string json = JsonSerializer.Serialize(snapshot, JsonOpts);
                File.WriteAllText(tmp, json);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return true;
            }
            catch
            {
                try { File.Delete(FilePath + ".tmp"); } catch { }
                return false;
            }
        }).ConfigureAwait(false);
    }
}
