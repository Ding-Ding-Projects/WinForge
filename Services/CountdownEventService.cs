using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 事件倒數 · Event-countdown persistence. Stores named events (name + target datetime) as JSON under
/// %LOCALAPPDATA%\WinForge\countdowns\events.json. Pure managed I/O; every call is guarded and never throws.
/// </summary>
public static class CountdownEventService
{
    /// <summary>A single saved countdown event.</summary>
    public sealed class EventEntry
    {
        public string Name { get; set; } = "";
        // Stored as an ISO-8601 round-trip string so the file is stable and culture-independent.
        public DateTimeOffset Target { get; set; }
    }

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "countdowns");

    private static string FilePath => Path.Combine(Dir, "events.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>Load all saved events. Returns an empty list on any error (missing file, bad JSON, IO fault).</summary>
    public static async Task<List<EventEntry>> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<EventEntry>();
            string json = await Task.Run(() => File.ReadAllText(FilePath)).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json)) return new List<EventEntry>();
            var list = JsonSerializer.Deserialize<List<EventEntry>>(json, JsonOpts);
            return list ?? new List<EventEntry>();
        }
        catch
        {
            return new List<EventEntry>();
        }
    }

    /// <summary>Save all events. Returns false on any error; never throws.</summary>
    public static async Task<bool> SaveAsync(IReadOnlyList<EventEntry> events)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            string json = JsonSerializer.Serialize(events, JsonOpts);
            string tmp = FilePath + ".tmp";
            await Task.Run(() =>
            {
                File.WriteAllText(tmp, json);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
