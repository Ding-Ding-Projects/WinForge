using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 便箋（持久筆記）· Scratchpad / persistent notes storage. All file IO lives here and is
/// resilient — a missing, locked or corrupt store never throws; callers get a best-effort
/// result plus an optional error string they can surface in the UI. Notes are stored as one
/// notes.json under %LOCALAPPDATA%\WinForge\notes. Pure managed (System.IO + System.Text.Json).
/// </summary>
public static class NotesService
{
    /// <summary>A single scratchpad note.</summary>
    public sealed class Note
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public DateTime Modified { get; set; } = DateTime.UtcNow;
    }

    private static readonly object _gate = new();
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    /// <summary>%LOCALAPPDATA%\WinForge\notes — created on demand; empty string if it can't be resolved.</summary>
    public static string Dir
    {
        get
        {
            try
            {
                var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(root)) return "";
                return Path.Combine(root, "WinForge", "notes");
            }
            catch { return ""; }
        }
    }

    private static string FilePath
    {
        get
        {
            var d = Dir;
            return string.IsNullOrEmpty(d) ? "" : Path.Combine(d, "notes.json");
        }
    }

    private static bool EnsureDir()
    {
        try
        {
            var d = Dir;
            if (string.IsNullOrEmpty(d)) return false;
            Directory.CreateDirectory(d);
            return true;
        }
        catch { return false; }
    }

    /// <summary>
    /// Load all notes from disk. Never throws — on any failure returns an empty list and an error
    /// message. Runs the actual IO on a background thread so callers can await without blocking UI.
    /// </summary>
    public static Task<(List<Note> notes, string? error)> LoadAsync() => Task.Run(() =>
    {
        try
        {
            var path = FilePath;
            if (string.IsNullOrEmpty(path)) return (new List<Note>(), (string?)"No storage location available.");
            if (!File.Exists(path)) return (new List<Note>(), (string?)null);

            string text;
            lock (_gate) { text = File.ReadAllText(path, Encoding.UTF8); }
            if (string.IsNullOrWhiteSpace(text)) return (new List<Note>(), (string?)null);

            var list = JsonSerializer.Deserialize<List<Note>>(text) ?? new List<Note>();
            // Defensive: drop nulls, backfill ids, order by most-recently-modified.
            var clean = list.Where(n => n != null).ToList();
            foreach (var n in clean)
                if (string.IsNullOrEmpty(n.Id)) n.Id = Guid.NewGuid().ToString("N");
            return (clean.OrderByDescending(n => n.Modified).ToList(), (string?)null);
        }
        catch (Exception ex)
        {
            return (new List<Note>(), (string?)("Could not load notes: " + ex.Message));
        }
    });

    /// <summary>
    /// Persist all notes atomically (write to a temp file, then replace). Never throws — returns an
    /// error string on failure. Runs on a background thread.
    /// </summary>
    public static Task<string?> SaveAsync(IEnumerable<Note> notes) => Task.Run(() =>
    {
        try
        {
            if (!EnsureDir()) return (string?)"Could not create the notes folder.";
            var path = FilePath;
            if (string.IsNullOrEmpty(path)) return (string?)"No storage location available.";

            var snapshot = (notes ?? Enumerable.Empty<Note>()).Where(n => n != null).ToList();
            var text = JsonSerializer.Serialize(snapshot, _json);

            lock (_gate)
            {
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                // Atomic-ish replace so a crash mid-write can't corrupt the store.
                if (File.Exists(path))
                {
                    try { File.Replace(tmp, path, null); }
                    catch { File.Copy(tmp, path, true); if (File.Exists(tmp)) File.Delete(tmp); }
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            return (string?)null;
        }
        catch (Exception ex)
        {
            return (string?)("Could not save notes: " + ex.Message);
        }
    });
}
