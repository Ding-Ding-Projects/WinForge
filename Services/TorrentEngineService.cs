using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Trackers;

namespace WinForge.Services;

/// <summary>
/// 原生 BitTorrent 引擎（單例）· Native in-process BitTorrent engine (singleton) built on MonoTorrent.
///
/// 重要：MonoTorrent 係一個「完全受控」（pure-managed C#）嘅 BitTorrent 協定實作。佢自己喺程序內
/// 行 DHT、本地 peer 探索（LSD）、PEX、UDP／HTTP 追蹤器、加密握手同 piece 交換 —— 並非包裝
/// qBittorrent、libtorrent 或任何外部執行檔；冇任何外部程式會被啟動或捆綁。
///
/// IMPORTANT: MonoTorrent is a FULLY MANAGED (pure C#) implementation of the BitTorrent protocol. It runs
/// DHT, Local Peer Discovery, Peer Exchange (PEX), UDP/HTTP trackers, the encrypted handshake and piece
/// exchange entirely in-process. It is NOT a wrapper around qBittorrent / libtorrent / any external binary —
/// nothing is shelled out, launched or bundled. The ClientEngine *is* the BitTorrent client.
///
/// State (DHT cache, fast-resume, magnet metadata) is auto-saved under %LOCALAPPDATA%\WinForge\torrent so
/// torrents and their progress are restored on next launch via <see cref="RestoreSessionAsync"/>.
/// </summary>
public sealed class TorrentEngineService
{
    public static TorrentEngineService I { get; } = new();

    // ── Settings keys (persisted via SettingsStore) ─────────────────────────────
    public const string KeySavePath = "torrent.savepath";
    public const string KeyMaxDown = "torrent.maxdown";   // KiB/s, 0 = unlimited
    public const string KeyMaxUp = "torrent.maxup";       // KiB/s, 0 = unlimited
    public const string KeyMaxConn = "torrent.maxconn";
    public const string KeyListenPort = "torrent.port";
    public const string KeyDht = "torrent.dht";           // "1"/"0"

    public string CacheRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "torrent");

    private string StatePath => Path.Combine(CacheRoot, "engine.state");

    private ClientEngine? _engine;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsRunning => _engine is { Disposed: false };
    public ClientEngine? Engine => _engine;

    public string DefaultSavePath
    {
        get
        {
            var v = SettingsStore.Get(KeySavePath, "");
            if (!string.IsNullOrWhiteSpace(v)) return v;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "WinForge");
        }
    }

    public int MaxDownloadKiB => int.TryParse(SettingsStore.Get(KeyMaxDown, "0"), out var v) ? v : 0;
    public int MaxUploadKiB => int.TryParse(SettingsStore.Get(KeyMaxUp, "0"), out var v) ? v : 0;
    public int MaxConnections => int.TryParse(SettingsStore.Get(KeyMaxConn, "200"), out var v) ? v : 200;
    public int ListenPort => int.TryParse(SettingsStore.Get(KeyListenPort, "55123"), out var v) ? v : 55123;
    public bool DhtEnabled => SettingsStore.Get(KeyDht, "1") != "0";

    // ── Engine lifecycle ────────────────────────────────────────────────────────

    /// <summary>建立／取得引擎 · Build (or return) the singleton ClientEngine with current settings.</summary>
    public async Task<ClientEngine> EnsureEngineAsync()
    {
        if (_engine is { Disposed: false }) return _engine;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_engine is { Disposed: false }) return _engine;
            Directory.CreateDirectory(CacheRoot);
            try { Directory.CreateDirectory(DefaultSavePath); } catch { }
            _engine = new ClientEngine(BuildSettings());
            return _engine;
        }
        finally { _gate.Release(); }
    }

    private EngineSettings BuildSettings()
    {
        int port = ListenPort;
        var b = new EngineSettingsBuilder
        {
            CacheDirectory = CacheRoot,
            // 自動儲存／載入：DHT 節點、fast-resume 進度、磁力中繼資料 → 下次開機可還原。
            // Auto-persist DHT nodes, fast-resume progress and magnet metadata so sessions restore.
            AutoSaveLoadDhtCache = true,
            AutoSaveLoadFastResume = true,
            AutoSaveLoadMagnetLinkMetadata = true,
            FastResumeMode = FastResumeMode.Accurate,
            // DHT（分散式雜湊表）· DHT (distributed hash table) for trackerless peer discovery.
            DhtEndPoint = DhtEnabled ? new IPEndPoint(IPAddress.Any, port) : null,
            // 本地 peer 探索 (LSD) + 連接埠轉送 (UPnP/NAT-PMP) — 全部受控實作。
            // Local Peer Discovery + automatic UPnP / NAT-PMP port forwarding (managed Mono.Nat).
            AllowLocalPeerDiscovery = true,
            AllowPortForwarding = true,
            MaximumConnections = MaxConnections,
            MaximumDownloadRate = MaxDownloadKiB * 1024,
            MaximumUploadRate = MaxUploadKiB * 1024,
            ListenEndPoints = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new IPEndPoint(IPAddress.Any, port),
                ["ipv6"] = new IPEndPoint(IPAddress.IPv6Any, port),
            },
        };
        return b.ToSettings();
    }

    /// <summary>套用全域設定（速度／連線／DHT／埠）· Apply changed global settings to the live engine.</summary>
    public async Task ApplyGlobalSettingsAsync()
    {
        var e = await EnsureEngineAsync().ConfigureAwait(false);
        await e.UpdateSettingsAsync(BuildSettings()).ConfigureAwait(false);
    }

    // ── Per-torrent settings (PEX/DHT/conn) ─────────────────────────────────────
    private static TorrentSettings DefaultTorrentSettings() => new TorrentSettingsBuilder
    {
        AllowDht = true,
        AllowPeerExchange = true,   // PEX
        CreateContainingDirectory = false,
    }.ToSettings();

    // ── Add torrents ────────────────────────────────────────────────────────────

    /// <summary>加 .torrent 檔 · Add a torrent from a local .torrent file.</summary>
    public async Task<TorrentManager> AddTorrentFileAsync(string torrentFilePath, string saveDirectory,
        bool startImmediately = true, bool sequential = false)
    {
        var e = await EnsureEngineAsync().ConfigureAwait(false);
        var torrent = await Torrent.LoadAsync(torrentFilePath).ConfigureAwait(false);
        if (e.Contains(torrent.InfoHashes))
            return e.Torrents.First(m => m.InfoHashes.Equals(torrent.InfoHashes));
        var mgr = await e.AddAsync(torrent, saveDirectory, DefaultTorrentSettings()).ConfigureAwait(false);
        if (startImmediately) await mgr.StartAsync().ConfigureAwait(false);
        await SaveStateAsync().ConfigureAwait(false);
        return mgr;
    }

    /// <summary>加磁力連結 · Add a torrent from a magnet URI.</summary>
    public async Task<TorrentManager> AddMagnetAsync(string magnetUri, string saveDirectory,
        bool startImmediately = true)
    {
        var e = await EnsureEngineAsync().ConfigureAwait(false);
        if (!MagnetLink.TryParse(magnetUri.Trim(), out var link) || link is null)
            throw new ArgumentException("Invalid magnet link · 磁力連結無效");
        if (link.InfoHashes is not null && e.Contains(link.InfoHashes))
            return e.Torrents.First(m => m.InfoHashes.Equals(link.InfoHashes));
        var mgr = await e.AddAsync(link, saveDirectory, DefaultTorrentSettings()).ConfigureAwait(false);
        if (startImmediately) await mgr.StartAsync().ConfigureAwait(false);
        await SaveStateAsync().ConfigureAwait(false);
        return mgr;
    }

    // ── Lifecycle helpers ───────────────────────────────────────────────────────

    public async Task StartAsync(TorrentManager m)
    {
        try { await m.StartAsync().ConfigureAwait(false); await SaveStateAsync().ConfigureAwait(false); } catch { }
    }

    public async Task PauseAsync(TorrentManager m)
    {
        try { await m.PauseAsync().ConfigureAwait(false); } catch { }
    }

    public async Task StopAsync(TorrentManager m)
    {
        try { await m.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); } catch { }
    }

    public async Task RecheckAsync(TorrentManager m)
    {
        try
        {
            if (m.State != TorrentState.Stopped) await m.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            await m.HashCheckAsync(autoStart: true).ConfigureAwait(false);
        }
        catch { }
    }

    /// <summary>移除種子（可連同資料刪除）· Remove a torrent, optionally deleting downloaded data.</summary>
    public async Task RemoveAsync(TorrentManager m, bool deleteData)
    {
        var e = _engine;
        if (e is null) return;
        try
        {
            if (m.State != TorrentState.Stopped) await m.StopAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch { }
        var mode = deleteData ? RemoveMode.CacheDataAndDownloadedData : RemoveMode.CacheDataOnly;
        try { await e.RemoveAsync(m, mode).ConfigureAwait(false); } catch { }
        await SaveStateAsync().ConfigureAwait(false);
    }

    // ── File priority ───────────────────────────────────────────────────────────

    public async Task SetFilePriorityAsync(TorrentManager m, ITorrentManagerFile file, Priority priority)
    {
        try { await m.SetFilePriorityAsync(file, priority).ConfigureAwait(false); } catch { }
    }

    // ── Session persistence ─────────────────────────────────────────────────────

    /// <summary>儲存引擎狀態（含已加種子）· Persist engine state (torrent list + fast-resume) to disk.</summary>
    public async Task SaveStateAsync()
    {
        var e = _engine;
        if (e is null || e.Disposed) return;
        try { Directory.CreateDirectory(CacheRoot); await e.SaveStateAsync(StatePath).ConfigureAwait(false); }
        catch { }
    }

    /// <summary>還原上次嘅工作階段 · Restore previously added torrents from the saved state file.</summary>
    public async Task<int> RestoreSessionAsync()
    {
        if (!File.Exists(StatePath)) { await EnsureEngineAsync().ConfigureAwait(false); return 0; }
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_engine is { Disposed: false } && _engine.Torrents.Count > 0) return _engine.Torrents.Count;
            try
            {
                _engine?.Dispose();
                _engine = await ClientEngine.RestoreStateAsync(StatePath).ConfigureAwait(false);
            }
            catch
            {
                // 損壞或版本不符就重建一個乾淨引擎 · corrupt/incompatible state → fresh engine.
                _engine = new ClientEngine(BuildSettings());
                return 0;
            }
            // 對齊目前嘅全域設定 · re-apply current global settings on top of the restored engine.
            try { await _engine.UpdateSettingsAsync(BuildSettings()).ConfigureAwait(false); } catch { }
            return _engine.Torrents.Count;
        }
        finally { _gate.Release(); }
    }

    /// <summary>關閉引擎（儲存後）· Gracefully stop everything and save state.</summary>
    public async Task ShutdownAsync()
    {
        var e = _engine;
        if (e is null || e.Disposed) return;
        try { await e.StopAllAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); } catch { }
        await SaveStateAsync().ConfigureAwait(false);
    }

    // ── Formatting helpers (UI-facing) ──────────────────────────────────────────

    public static string HumanSize(long bytes)
    {
        if (bytes < 0) return "—";
        double b = bytes;
        string[] u = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" };
        int i = 0;
        while (b >= 1024 && i < u.Length - 1) { b /= 1024; i++; }
        return i == 0 ? $"{bytes} {u[i]}" : $"{b:0.##} {u[i]}";
    }

    public static string HumanSpeed(long bytesPerSec)
        => bytesPerSec <= 0 ? "—" : HumanSize(bytesPerSec) + "/s";

    public static string HumanEta(TorrentManager m)
    {
        long rate = m.Monitor.DownloadRate;
        if (m.Complete || m.State == TorrentState.Seeding) return "—";
        if (rate <= 0 || m.Torrent is null) return "∞";
        long remaining = (long)(m.Torrent.Size * (1 - m.Progress / 100.0));
        if (remaining <= 0) return "—";
        var t = TimeSpan.FromSeconds(remaining / (double)rate);
        if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
        if (t.TotalHours >= 1) return $"{t.Hours}h {t.Minutes}m";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
        return $"{t.Seconds}s";
    }

    /// <summary>做種比率 · Upload/download ratio for a torrent.</summary>
    public static double Ratio(TorrentManager m)
    {
        long down = m.Monitor.DataBytesReceived;
        long up = m.Monitor.DataBytesSent;
        if (down <= 0) return up > 0 ? double.PositiveInfinity : 0;
        return up / (double)down;
    }

    /// <summary>把狀態變成雙語標籤 · Map a TorrentState to a bilingual label.</summary>
    public static (string En, string Zh) StateLabel(TorrentState s) => s switch
    {
        TorrentState.Downloading => ("Downloading", "下載中"),
        TorrentState.Seeding => ("Seeding", "做種"),
        TorrentState.Paused => ("Paused", "暫停"),
        TorrentState.Stopped => ("Stopped", "已停止"),
        TorrentState.Stopping => ("Stopping", "停止中"),
        TorrentState.Starting => ("Starting", "啟動中"),
        TorrentState.Hashing => ("Checking", "檢查中"),
        TorrentState.HashingPaused => ("Check paused", "檢查暫停"),
        TorrentState.Error => ("Error", "錯誤"),
        TorrentState.Metadata => ("Fetching metadata", "取中繼資料"),
        TorrentState.FetchingHashes => ("Fetching hashes", "取雜湊"),
        _ => (s.ToString(), s.ToString()),
    };

    public static (string En, string Zh) PriorityLabel(Priority p) => p switch
    {
        Priority.DoNotDownload => ("Skip", "略過"),
        Priority.High or Priority.Highest or Priority.Immediate => ("High", "高"),
        Priority.Low or Priority.Lowest => ("Low", "低"),
        _ => ("Normal", "正常"),
    };

    public static (string En, string Zh) TrackerStateLabel(TrackerState s) => s switch
    {
        TrackerState.Ok => ("Working", "正常"),
        TrackerState.Connecting => ("Connecting", "連線中"),
        TrackerState.Offline => ("Offline", "離線"),
        TrackerState.InvalidResponse => ("Invalid response", "回應無效"),
        _ => ("Unknown", "未知"),
    };
}
