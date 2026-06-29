using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 防崩潰／關機自動儲存與還原服務（單例）。
/// Crash- and shutdown-safe state store (singleton).
///
/// 任何模組都可以登記一個「狀態提供者」（一個 id、一個回傳可序列化快照嘅 Func、同埋一個由快照還原嘅
/// Action）。本服務會：
///  - 用「先寫臨時檔再原子換檔（File.Replace/Move）」嘅方式寫盤，所以寫到一半就算崩潰都唔會整爛已儲存狀態；
///    每次成功寫盤都會保留上一個好版本嘅 .bak；載入時若主檔損壞會自動回退去 .bak，再唔得就用乾淨預設。
///  - 定時自動儲存（每 4 秒）＋ 有顯著改動時去抖動儲存；<see cref="Flush"/> 係即時同步儲存。
///  - 喺 app 退出／崩潰／Windows 關機（工作階段結束）前 Flush()，確保資料喺程序死亡／電腦熄機之前已落盤。
///
/// Any module registers a state provider (an id + a Func returning a serializable snapshot + an
/// Action restoring from a snapshot). State is written atomically (temp file then File.Replace/Move)
/// so a crash mid-write never corrupts the good copy; a .bak of the previous good file is kept; on
/// load a corrupt primary falls back to .bak (then to a clean default). Saving/loading NEVER throws
/// into the UI. Periodic autosave (every 4 s) + debounced save on change; <see cref="Flush"/> is an
/// immediate synchronous save used by the death hooks (app exit, crash, Windows session-ending) and
/// by the reactor BEFORE it triggers a real PC shutdown.
/// </summary>
public sealed class PersistenceService
{
    public static PersistenceService I { get; } = new();

    private sealed class Provider
    {
        public required string Id;
        public required Func<object?> Snapshot;   // returns a serializable snapshot object (or null)
        public required Action<JsonElement> Restore; // restore from the parsed JSON for this id
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Provider> _providers = new(StringComparer.Ordinal);

    private static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "state");
    private static readonly string FilePath = Path.Combine(Dir, "state.json");
    private static readonly string BakPath = FilePath + ".bak";
    private static readonly string TmpPath = FilePath + ".tmp";

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    // The last document loaded from disk on startup. Providers registering later can restore from it
    // even if they come online after launch (e.g. a page opened by the user).
    private JsonDocument? _loaded;

    private Timer? _autosaveTimer;
    private const int AutosaveIntervalMs = 4000; // periodic autosave cadence
    private const int DebounceMs = 800;          // coalesce bursts of "something changed"
    private Timer? _debounceTimer;

    private volatile bool _hooksInstalled;

    /// <summary>最後一次成功儲存嘅時間（本地）· Local time of the last successful save (null = never).</summary>
    public DateTime? LastSaved { get; private set; }

    /// <summary>儲存成功時通知 UI 更新指示器 · Raised on the saving thread after a successful save.</summary>
    public event Action<DateTime>? Saved;

    /// <summary>自動儲存開關（預設開）· Autosave enabled (default ON), backed by SettingsStore.</summary>
    public bool AutosaveEnabled
    {
        get => SettingsStore.Get("persistence.autosave", "true") != "false";
        set
        {
            SettingsStore.Set("persistence.autosave", value ? "true" : "false");
            if (value) StartAutosaveTimer();
            else StopAutosaveTimer();
        }
    }

    private PersistenceService() { }

    // ----------------------------------------------------------------- lifecycle ----

    /// <summary>
    /// 啟動時呼叫一次：載入已儲存狀態、掛上死亡鈎、啟動定時自動儲存。
    /// Call once at startup: load saved state into memory, install death hooks, start the autosave
    /// timer. Safe to call from App.OnLaunched. Never throws.
    /// </summary>
    public void Initialize()
    {
        try
        {
            LoadDocument();
            InstallDeathHooks();
            if (AutosaveEnabled) StartAutosaveTimer();
        }
        catch (Exception ex) { CrashLogger.Log("persistence:init", ex); }
    }

    /// <summary>
    /// 登記一個狀態提供者；若啟動時有已儲存狀態，會即刻嘗試還原。
    /// Register a state provider. If saved state for this id was loaded at startup, the provider is
    /// restored immediately (so a page that opens after launch still gets its state back).
    /// </summary>
    public void Register(string id, Func<object?> snapshot, Action<JsonElement> restore)
    {
        if (string.IsNullOrEmpty(id) || snapshot is null || restore is null) return;
        lock (_gate)
            _providers[id] = new Provider { Id = id, Snapshot = snapshot, Restore = restore };
        // Best-effort immediate restore from whatever we loaded at startup.
        TryRestoreOne(id, restore);
    }

    /// <summary>取消登記 · Unregister a provider (e.g. page unloaded). Optionally flush first.</summary>
    public void Unregister(string id)
    {
        lock (_gate) _providers.Remove(id);
    }

    /// <summary>有顯著改動時呼叫：去抖動之後儲存 · Note a significant change; saves after a short debounce.</summary>
    public void NoteChanged()
    {
        if (!AutosaveEnabled) return;
        try
        {
            _debounceTimer ??= new Timer(_ => SaveAll(), null, Timeout.Infinite, Timeout.Infinite);
            _debounceTimer.Change(DebounceMs, Timeout.Infinite);
        }
        catch (Exception ex) { CrashLogger.Log("persistence:notechanged", ex); }
    }

    /// <summary>即時同步儲存（畀死亡鈎同關機前用）· Immediate synchronous save (used by death hooks).</summary>
    public void Flush() => SaveAll();

    /// <summary>非同步儲存（畀 UI 互動路徑用）· Save without blocking UI interactions.</summary>
    public Task FlushAsync() => Task.Run(SaveAll);

    // ----------------------------------------------------------------- save ----

    private void StartAutosaveTimer()
    {
        try
        {
            _autosaveTimer ??= new Timer(_ => { try { SaveAll(); } catch { } },
                null, Timeout.Infinite, Timeout.Infinite);
            _autosaveTimer.Change(AutosaveIntervalMs, AutosaveIntervalMs);
        }
        catch (Exception ex) { CrashLogger.Log("persistence:starttimer", ex); }
    }

    private void StopAutosaveTimer()
    {
        try { _autosaveTimer?.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
    }

    /// <summary>
    /// 序列化所有已登記提供者並原子寫盤 · Serialize every registered provider and write atomically.
    /// Never throws.
    /// </summary>
    public void SaveAll()
    {
        // Build the document under the lock, but do disk I/O without holding it long.
        Dictionary<string, object?> doc;
        lock (_gate)
        {
            doc = new Dictionary<string, object?>(_providers.Count + 1, StringComparer.Ordinal)
            {
                ["_meta"] = new { savedUtc = DateTime.UtcNow, version = 1 },
            };
            // Preserve state for providers that are not currently loaded. Without this merge, the
            // background timer can erase a saved page snapshot before that page is opened again.
            if (_loaded is not null && _loaded.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in _loaded.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "_meta", StringComparison.Ordinal)) continue;
                    doc[prop.Name] = prop.Value.Clone();
                }
            }
            foreach (var kv in _providers)
            {
                try { doc[kv.Key] = kv.Value.Snapshot(); }
                catch (Exception ex) { CrashLogger.Log($"persistence:snapshot:{kv.Key}", ex); }
            }
        }

        try
        {
            string json = JsonSerializer.Serialize(doc, JsonOpts);
            WriteAtomic(json);
            RefreshLoadedDocument(json);
            var now = DateTime.Now;
            LastSaved = now;
            try { Saved?.Invoke(now); } catch { }
        }
        catch (Exception ex) { CrashLogger.Log("persistence:saveall", ex); }
    }

    /// <summary>
    /// 原子寫檔：寫臨時檔 → fsync → File.Replace（保留 .bak）；目標唔存在就直接 Move。
    /// Atomic write: write a temp file, flush to disk, then File.Replace the target (keeping a .bak
    /// of the previous good file). If the target doesn't exist yet, just Move the temp into place.
    /// </summary>
    private static readonly object _writeLock = new();

    private static void WriteAtomic(string json)
    {
        Directory.CreateDirectory(Dir);

        // Serialize all writers: the autosave timer, page-driven saves and the shutdown flush can
        // otherwise overlap and collide on the temp file (FileShare.None) — which made every save
        // throw "used by another process" and silently lose state. A per-write unique temp name also
        // means a lingering/locked temp from a prior write can never block the next one.
        lock (_writeLock)
        {
            string tmp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // 1) Write fully to the temp file and flush it to the physical disk.
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var sw = new StreamWriter(fs))
                {
                    sw.Write(json);
                    sw.Flush();
                    fs.Flush(flushToDisk: true);
                }

                // 2) Swap into place, retrying briefly for transient AV/search-indexer locks.
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    try
                    {
                        if (File.Exists(FilePath))
                            File.Replace(tmp, FilePath, BakPath, ignoreMetadataErrors: true);  // atomic on NTFS, keeps a .bak
                        else
                            File.Move(tmp, FilePath);
                        return;
                    }
                    catch (IOException) { System.Threading.Thread.Sleep(50 * (attempt + 1)); }
                    catch (UnauthorizedAccessException) { System.Threading.Thread.Sleep(50 * (attempt + 1)); }
                }

                // 3) Swap kept failing — overwrite in place so state is never lost (non-atomic last resort).
                File.Copy(tmp, FilePath, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                // Tidy up any stale fixed-name temp from older builds.
                try { if (File.Exists(TmpPath)) File.Delete(TmpPath); } catch { }
            }
        }
    }

    // ----------------------------------------------------------------- load / restore ----

    /// <summary>載入文件到記憶體（主檔損壞就回退 .bak）· Load the saved doc; fall back to .bak on corruption.</summary>
    private void LoadDocument()
    {
        var loaded = TryParse(FilePath) ?? TryParse(BakPath);
        if (loaded is not null)
        {
            try
            {
                if (loaded.RootElement.TryGetProperty("_meta", out var meta) &&
                    meta.TryGetProperty("savedUtc", out var su) &&
                    su.TryGetDateTime(out var dt))
                    LastSaved = dt.ToLocalTime();
            }
            catch { }
        }

        JsonDocument? old;
        lock (_gate)
        {
            old = _loaded;
            _loaded = loaded;
        }
        old?.Dispose();
    }

    private static JsonDocument? TryParse(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return JsonDocument.Parse(text);
        }
        catch (Exception ex) { CrashLogger.Log($"persistence:parse:{Path.GetFileName(path)}", ex); return null; }
    }

    private void TryRestoreOne(string id, Action<JsonElement> restore)
    {
        try
        {
            var el = LoadedElement(id);
            if (el is JsonElement saved)
                restore(saved);
        }
        catch (Exception ex) { CrashLogger.Log($"persistence:restore:{id}", ex); }
    }

    /// <summary>是否有某 id 嘅已儲存狀態可還原 · True if saved state exists for the given id.</summary>
    public bool HasSavedState(string id)
    {
        try { return LoadedElement(id) is not null; }
        catch { return false; }
    }

    private JsonElement? LoadedElement(string id)
    {
        lock (_gate)
        {
            if (_loaded is null) return null;
            if (_loaded.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (_loaded.RootElement.TryGetProperty(id, out var el) && el.ValueKind != JsonValueKind.Null)
                return el.Clone();
            return null;
        }
    }

    private void RefreshLoadedDocument(string json)
    {
        JsonDocument? parsed = null;
        try { parsed = JsonDocument.Parse(json); }
        catch (Exception ex) { CrashLogger.Log("persistence:refresh-loaded", ex); return; }

        JsonDocument? old;
        lock (_gate)
        {
            old = _loaded;
            _loaded = parsed;
        }
        old?.Dispose();
    }

    // ----------------------------------------------------------------- death hooks ----

    /// <summary>
    /// 掛上所有「死亡」來源，喺程序死亡／機器熄機之前 Flush()。
    /// Wire every "death" source so state is flushed before the process dies / the machine powers off:
    ///  - AppDomain.ProcessExit (normal process teardown / Application.Exit)
    ///  - AppDomain.UnhandledException (fatal managed crash)
    ///  - TaskScheduler.UnobservedTaskException (faulted task finalized)
    ///  - SystemEvents.SessionEnding (Windows logoff / shutdown / restart)
    ///  - SystemEvents.PowerModeChanged → Suspend (sleep)
    /// </summary>
    private void InstallDeathHooks()
    {
        if (_hooksInstalled) return;
        _hooksInstalled = true;

        try { AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushFinal("process-exit"); } catch { }
        try { AppDomain.CurrentDomain.UnhandledException += (_, _) => FlushFinal("unhandled"); } catch { }
        try { System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, _) => FlushFinal("task"); } catch { }

        // Windows session ending = logoff / shutdown / restart. This is our best-effort hook for the
        // user shutting the PC down (or the reactor triggering a real shutdown from another path):
        // flush synchronously before the session tears the process down.
        try { SystemEvents.SessionEnding += (_, _) => FlushFinal("session-ending"); } catch (Exception ex) { CrashLogger.Log("persistence:hook:sessionending", ex); }
        // Save on sleep too — a machine that never wakes still has the last state on disk.
        try
        {
            SystemEvents.PowerModeChanged += (_, e) =>
            {
                if (e.Mode == PowerModes.Suspend) FlushFinal("suspend");
            };
        }
        catch (Exception ex) { CrashLogger.Log("persistence:hook:powermode", ex); }
    }

    private void FlushFinal(string reason)
    {
        // Always flush on death regardless of the autosave toggle — losing data at the moment of
        // death is exactly what we are guarding against. (The toggle only governs background saving.)
        try { SaveAll(); }
        catch (Exception ex) { CrashLogger.Log($"persistence:flushfinal:{reason}", ex); }
    }
}
