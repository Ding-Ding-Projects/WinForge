using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>一個核廢料垃圾檔 · One on-disk nuclear-waste junk file.</summary>
public sealed record WasteFile(string Id, string Path, long Bytes, DateTime CreatedUtc);

/// <summary>廢料倉狀態 · Waste-store status snapshot for the UI.</summary>
public sealed record WasteStatus(
    IReadOnlyList<WasteFile> Files, long TotalBytes, int Count,
    long DriveFreeBytes, long SafetyFloorBytes, bool StorageFull,
    bool Generating, double GenProgressPct, long GenTargetBytes, string GenId,
    long CapBytes, double CapUsedPct, bool CapReached, bool RunbackZone);

/// <summary>
/// 核廢料服務 · NUCLEAR WASTE service. Burning fuel MANDATORILY produces real, incompressible junk
/// files of 100 MB–2000 MB each, written to %LOCALAPPDATA%\WinForge\reactor\waste\&lt;id&gt;.waste.
///
/// SAFETY FLOOR: before a write, the target drive's free space is checked. If writing would drop
/// free space below the configurable floor (default 10 GB) OR the file won't fit, the write is
/// ABORTED (no partial file) and a bilingual "waste storage full" warning is raised. Writes are
/// atomic: data goes to a .tmp file then is moved into place; a .tmp left by a failed/cancelled
/// write is deleted. Generation runs on a background thread and never blocks the UI.
///
/// All file I/O is wrapped in try/catch; the disk is never filled.
/// </summary>
public sealed class NuclearWasteService
{
    public const long MinWasteBytes = 100L * 1024 * 1024;   // 100 MB
    public const long MaxWasteBytes = 2000L * 1024 * 1024;  // 2000 MB
    public const long DefaultSafetyFloorBytes = 10L * 1024 * 1024 * 1024; // 10 GB
    private const string SafetyFloorKey = "reactor.waste.safetyFloorBytes";

    // TOTAL WASTE STORAGE CAP — the PRIMARY limit (spent-fuel pool/dry-cask capacity). Custom-
    // configurable. Default 50 GB. The disk free-space floor above still applies independently.
    public const long DefaultCapBytes = 50L * 1024 * 1024 * 1024; // 50 GB
    public const long MinCapBytes = 1L * 1024 * 1024 * 1024;      // 1 GB sane minimum
    private const string CapKey = "reactor.waste.capBytes";
    // Begin controlled runback once used >= this fraction of the cap; mandate shutdown at the cap.
    public const double RunbackFraction = 0.90;

    private readonly string _wasteDir;
    private readonly object _lock = new();

    // Live generation state (single concurrent waste write; further triggers are skipped while busy).
    private volatile bool _generating;
    private long _genTarget;
    private long _genWritten;
    private string _genId = "";
    private CancellationTokenSource? _cts;

    /// <summary>雙語警告事件 · Raised (en, zh) when a write is aborted by the safety floor.</summary>
    public event Action<string, string>? StorageWarning;
    /// <summary>廢料狀態改變 · Raised when waste inventory / generation progress changes.</summary>
    public event Action? Changed;

    public NuclearWasteService()
    {
        _wasteDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinForge", "reactor", "waste");
        try { Directory.CreateDirectory(_wasteDir); } catch { }
    }

    public string WasteDir => _wasteDir;

    public long SafetyFloorBytes
    {
        get
        {
            var s = SettingsStore.Get(SafetyFloorKey, DefaultSafetyFloorBytes.ToString());
            return long.TryParse(s, out var v) && v >= 0 ? v : DefaultSafetyFloorBytes;
        }
        set => SettingsStore.Set(SafetyFloorKey, Math.Max(0, value).ToString());
    }

    public long CapBytes
    {
        get
        {
            var s = SettingsStore.Get(CapKey, DefaultCapBytes.ToString());
            return long.TryParse(s, out var v) && v >= MinCapBytes ? v : DefaultCapBytes;
        }
        set => SettingsStore.Set(CapKey, Math.Max(MinCapBytes, value).ToString());
    }

    /// <summary>已達上限 · True when total on-disk waste has reached the configured cap.</summary>
    public bool CapReached => TotalBytes() >= CapBytes;
    /// <summary>進入功率回降區（≥90% 上限）· True in the runback zone (≥ RunbackFraction of the cap).</summary>
    public bool RunbackZone => TotalBytes() >= (long)(CapBytes * RunbackFraction);

    public bool IsGenerating => _generating;

    // ------------------------------------------------------------------ listing ----
    public IReadOnlyList<WasteFile> List()
    {
        var list = new List<WasteFile>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(_wasteDir, "*.waste"))
            {
                try
                {
                    var fi = new FileInfo(path);
                    list.Add(new WasteFile(Path.GetFileNameWithoutExtension(path), path, fi.Length, fi.CreationTimeUtc));
                }
                catch { }
            }
        }
        catch { }
        return list.OrderByDescending(w => w.CreatedUtc).ToList();
    }

    public long TotalBytes()
    {
        long sum = 0;
        foreach (var w in List()) sum += w.Bytes;
        return sum;
    }

    private long DriveFreeBytes()
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(_wasteDir));
            if (string.IsNullOrEmpty(root)) return long.MaxValue;
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch { return long.MaxValue; }
    }

    public WasteStatus Status()
    {
        var files = List();
        long total = files.Sum(f => f.Bytes);
        long free = DriveFreeBytes();
        long floor = SafetyFloorBytes;
        long cap = CapBytes;
        bool diskFull = free - MinWasteBytes < floor; // can't even fit the smallest waste file (disk floor)
        bool capReached = total >= cap;
        bool full = diskFull || capReached; // EITHER the cap OR the disk floor counts as "full"
        double capPct = cap > 0 ? Math.Clamp(100.0 * total / cap, 0, 999) : 0;
        bool runback = total >= (long)(cap * RunbackFraction);
        double pct = _generating && _genTarget > 0
            ? Math.Clamp(100.0 * _genWritten / _genTarget, 0, 100) : 0;
        return new WasteStatus(files, total, files.Count, free, floor, full,
            _generating, pct, _genTarget, _genId,
            cap, capPct, capReached, runback);
    }

    // ------------------------------------------------------------------ generate ----
    /// <summary>
    /// 產生核廢料 · Produce one nuclear-waste junk file. Size defaults to a random value in
    /// [100MB, 2000MB]; pass a target to scale by energy produced (it is clamped into that range).
    /// Returns false immediately if a write is already in progress or the safety floor blocks it;
    /// otherwise the write proceeds in the background. SAFE: free-space checked, atomic .tmp→move.
    /// </summary>
    public bool GenerateWaste(long? targetBytes = null)
    {
        lock (_lock)
        {
            if (_generating) return false; // one waste write at a time

            long size = targetBytes ?? RandomNumberGenerator.GetInt32(0, 1901) * (1024L * 1024) + MinWasteBytes;
            size = Math.Clamp(size, MinWasteBytes, MaxWasteBytes);

            // CAP: never write past the configured total-waste cap (spent-fuel storage capacity).
            long cap = CapBytes;
            long totalNow = TotalBytes();
            if (totalNow + size > cap)
            {
                StorageWarning?.Invoke(
                    $"Spent fuel storage FULL — waste {totalNow / (1024 * 1024 * 1024)} GB at/over cap {cap / (1024 * 1024 * 1024)} GB. Dispose waste before operating.",
                    $"乏燃料貯存已滿 — 廢料 {totalNow / (1024 * 1024 * 1024)} GB 已達／超過上限 {cap / (1024 * 1024 * 1024)} GB。運轉前請先處置廢料。");
                return false;
            }

            long free = DriveFreeBytes();
            long floor = SafetyFloorBytes;
            // Abort if the write would not fit, or would drop free space below the safety floor.
            if (free == long.MaxValue) { /* unknown drive — proceed cautiously */ }
            else if (size > free || (free - size) < floor)
            {
                StorageWarning?.Invoke(
                    $"Waste storage full — cannot write {size / (1024 * 1024)} MB (free {free / (1024 * 1024)} MB, floor {floor / (1024 * 1024)} MB). Dispose of waste.",
                    $"廢料倉已滿 — 無法寫入 {size / (1024 * 1024)} MB（剩餘 {free / (1024 * 1024)} MB，安全下限 {floor / (1024 * 1024)} MB）。請處置核廢料。");
                return false;
            }

            _generating = true;
            _genTarget = size;
            _genWritten = 0;
            _genId = $"WASTE-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{RandomNumberGenerator.GetInt32(1000, 9999)}";
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;
            var id = _genId;
            Changed?.Invoke();

            _ = Task.Run(() => WriteWasteFile(id, size, ct));
            return true;
        }
    }

    private void WriteWasteFile(string id, long size, CancellationToken ct)
    {
        string finalPath = Path.Combine(_wasteDir, id + ".waste");
        string tmpPath = finalPath + ".tmp";
        bool ok = false;
        try
        {
            const int bufSize = 4 * 1024 * 1024; // 4 MB buffer
            var buf = new byte[bufSize];
            long remaining = size;

            // Re-check free space one last time right before opening the stream.
            long free = DriveFreeBytes();
            if (free != long.MaxValue && (free - size) < SafetyFloorBytes)
            {
                StorageWarning?.Invoke(
                    "Waste storage full — dispose of waste.", "廢料倉已滿 — 請處置核廢料。");
                return;
            }

            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufSize, FileOptions.SequentialScan))
            {
                while (remaining > 0)
                {
                    if (ct.IsCancellationRequested) throw new OperationCanceledException();
                    int chunk = (int)Math.Min(bufSize, remaining);
                    RandomNumberGenerator.Fill(buf.AsSpan(0, chunk)); // incompressible random bytes
                    fs.Write(buf, 0, chunk);
                    remaining -= chunk;
                    Interlocked.Add(ref _genWritten, chunk);
                    if ((_genWritten & ((8L * 1024 * 1024) - 1)) < bufSize) Changed?.Invoke(); // ~every 8 MB
                }
                fs.Flush(true);
            }

            // Atomic publish.
            try { if (File.Exists(finalPath)) File.Delete(finalPath); } catch { }
            File.Move(tmpPath, finalPath);
            ok = true;
        }
        catch
        {
            ok = false;
        }
        finally
        {
            if (!ok) TryDelete(tmpPath); // clean up partial .tmp on failure/cancellation
            lock (_lock)
            {
                _generating = false;
                _genWritten = 0;
                _genTarget = 0;
                _genId = "";
            }
            Changed?.Invoke();
        }
    }

    // ------------------------------------------------------------------ dispose ----
    /// <summary>深地質處置 · "Deep geological repository" — delete one waste file.</summary>
    public bool Dispose(string id)
    {
        var path = Path.Combine(_wasteDir, Path.GetFileName(id));
        if (!path.EndsWith(".waste", StringComparison.OrdinalIgnoreCase)) path += ".waste";
        bool ok = TryDelete(path);
        if (ok) Changed?.Invoke();
        return ok;
    }

    /// <summary>處置全部廢料 · Delete ALL waste files. Returns count removed.</summary>
    public int DisposeAll()
    {
        int n = 0;
        try
        {
            foreach (var path in Directory.EnumerateFiles(_wasteDir, "*.waste").ToList())
                if (TryDelete(path)) n++;
        }
        catch { }
        if (n > 0) Changed?.Invoke();
        return n;
    }

    public void CancelGeneration()
    {
        try { _cts?.Cancel(); } catch { }
    }

    private static bool TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); return true; } catch { return false; } }
}
