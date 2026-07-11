using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// 簡單嘅 JSON 設定儲存（適用於未封裝嘅 app）。
/// Lightweight JSON settings store written to %LOCALAPPDATA%\WinForge\settings.json,
/// so it works for unpackaged WinUI apps (no package identity required).
/// </summary>
public static class SettingsStore
{
    private static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge");

    private static readonly string FilePath = Path.Combine(Dir, "settings.json");
    private static readonly SettingsStoreSession Session = new(FilePath);

    public static string Get(string key, string fallback) => Session.Get(key, fallback);

    public static void Set(string key, string value) => Session.Set(key, value);

    /// <summary>匯出所有設定到檔案 · Export all settings to a JSON file.</summary>
    public static void ExportTo(string path) => Session.ExportTo(path);

    /// <summary>由檔案匯入設定（合併）· Import settings from a JSON file (merge). Returns count imported.</summary>
    public static int ImportFrom(string path) => Session.ImportFrom(path);
}

/// <summary>
/// Testable per-file settings session. The public store owns one session for the user profile;
/// focused tests create sessions only against disposable temporary fixture directories.
/// </summary>
internal sealed class SettingsStoreSession
{
    private readonly string _filePath;
    private readonly object _gate = new();
    private readonly Dictionary<string, string> _cache;
    private bool _persistenceEnabled;

    internal SettingsStoreSession(string filePath)
    {
        _filePath = filePath;
        var loaded = SettingsStorePersistence.Load(filePath);
        _cache = loaded.Values;
        _persistenceEnabled = loaded.CanPersist;
    }

    internal bool PersistenceEnabled
    {
        get
        {
            lock (_gate) return _persistenceEnabled;
        }
    }

    internal string Get(string key, string fallback)
    {
        lock (_gate)
        {
            return _cache.TryGetValue(key, out var value) ? value : fallback;
        }
    }

    internal void Set(string key, string value)
    {
        lock (_gate)
        {
            _cache[key] = value;
            SaveLocked();
        }
    }

    internal void ExportTo(string path)
    {
        lock (_gate)
            File.WriteAllText(path, JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true }));
    }

    internal int ImportFrom(string path)
    {
        var imported = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
        if (imported is null) return 0;

        lock (_gate)
        {
            foreach (var pair in imported) _cache[pair.Key] = pair.Value;

            // A valid, explicit import is a deliberate recovery action. Ordinary Set calls remain
            // fail-closed after an unrecoverable load so they cannot silently erase damaged data.
            if (_persistenceEnabled)
            {
                SaveLocked();
            }
            else if (SettingsStorePersistence.TryRepairFromExplicitImport(_filePath, _cache))
            {
                _persistenceEnabled = true;
            }
        }

        return imported.Count;
    }

    private void SaveLocked()
    {
        if (_persistenceEnabled)
            SettingsStorePersistence.TryWriteAtomically(_filePath, _cache);
    }
}
