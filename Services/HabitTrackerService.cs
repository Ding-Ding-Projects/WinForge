using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 習慣追蹤器 · Habit Tracker store — a list of named habits, each keeping the set of dates it was
/// completed (ISO "yyyy-MM-dd"), persisted to %LOCALAPPDATA%\WinForge\habits\habits.json.
/// Pure managed C# (System.IO + System.Text.Json). All disk access is off-thread (Task.Run) and
/// fully guarded — never throws to callers.
/// </summary>
public static class HabitTrackerService
{
    /// <summary>A single persisted habit: a name plus the list of ISO dates it was completed.</summary>
    public sealed class Habit
    {
        public string Name { get; set; } = "";
        public List<string> Done { get; set; } = new();
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinForge", "habits");

    private static string FilePath => Path.Combine(Dir, "habits.json");

    /// <summary>Load habits from disk. Returns an empty list on any missing/corrupt/error condition — never throws.</summary>
    public static async Task<List<Habit>> LoadAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return new List<Habit>();
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return new List<Habit>();
                var list = JsonSerializer.Deserialize<List<Habit>>(json);
                if (list == null) return new List<Habit>();
                // Sanitise: drop nulls, coalesce names, de-dupe dates.
                var clean = new List<Habit>();
                foreach (var h in list)
                {
                    if (h == null) continue;
                    var dates = new List<string>();
                    var seen = new HashSet<string>(StringComparer.Ordinal);
                    if (h.Done != null)
                    {
                        foreach (var d in h.Done)
                        {
                            if (string.IsNullOrWhiteSpace(d)) continue;
                            if (seen.Add(d)) dates.Add(d);
                        }
                    }
                    clean.Add(new Habit { Name = h.Name ?? "", Done = dates });
                }
                return clean;
            }
            catch
            {
                return new List<Habit>();
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Save habits to disk (atomic-ish via temp + replace). Returns true on success; never throws.</summary>
    public static async Task<bool> SaveAsync(IEnumerable<Habit> habits)
    {
        var snapshot = new List<Habit>();
        try
        {
            foreach (var h in habits)
            {
                if (h == null) continue;
                var dates = new List<string>();
                if (h.Done != null)
                {
                    foreach (var d in h.Done)
                    {
                        if (string.IsNullOrWhiteSpace(d)) continue;
                        dates.Add(d);
                    }
                }
                snapshot.Add(new Habit { Name = h.Name ?? "", Done = dates });
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
