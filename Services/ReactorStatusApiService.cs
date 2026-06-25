using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 對外反應堆狀態 API（伺服器端）· Public, cross-process reactor-status API (server side).
///
/// Publishes the SAME versioned status snapshot over two zero-dependency local transports so that
/// OTHER C# apps (and any local process) can read the simulated reactor's power/status in real time
/// and depend on it (e.g. an app that only runs while the reactor is generating):
///
///  a. <b>Memory-mapped file</b> — a small MMF named <c>Local\WinForge.Reactor.Status</c> holding a
///     length-prefixed UTF-8 JSON snapshot, rewritten every reactor tick. Concurrent access is
///     guarded by a named mutex <c>Local\WinForge.Reactor.Status.Mutex</c>; a monotonically
///     increasing <c>sequence</c> in the payload lets readers detect updates without polling timestamps.
///     This is the zero-latency fast path: a reader opens the MMF and reads the latest snapshot with
///     no handshake.
///
///  b. <b>Named pipe server</b> — <c>NamedPipeServerStream("WinForge.Reactor.Status")</c> with many
///     concurrent async server instances. Line protocol: a client connects and sends ONE command line
///     (<c>GET</c> = one snapshot then disconnect; <c>SUBSCRIBE</c> = a snapshot line every ~500 ms
///     until the client disconnects). The server replies with one JSON object per line
///     (newline-delimited). A fresh server instance is recycled after each accept so many clients can
///     connect at once; client churn / disconnects never crash the server.
///
/// The service starts automatically (so dependents always have it) and can be toggled on/off via a
/// setting persisted in <see cref="SettingsStore"/> (default ON). It binds to the live sim through a
/// snapshot provider supplied by the reactor module/window; when the reactor page is closed the last
/// snapshot is served, or a "reactor offline" snapshot with <c>isGenerating=false</c>.
/// </summary>
public sealed class ReactorStatusApiService
{
    // ---------------------------------------------------------------- well-known names ----
    /// <summary>記憶體映射檔名 · The well-known memory-mapped file name (Local session namespace).</summary>
    public const string MmfName = "Local\\WinForge.Reactor.Status";
    /// <summary>保護 MMF 讀寫嘅具名互斥鎖 · Named mutex guarding MMF reads/writes.</summary>
    public const string MutexName = "Local\\WinForge.Reactor.Status.Mutex";
    /// <summary>具名管道名稱 · The named-pipe name (no leading "\\.\pipe\" — that's implicit).</summary>
    public const string PipeName = "WinForge.Reactor.Status";

    /// <summary>啟用／停用設定鍵（預設開）· SettingsStore key for the enable toggle (default ON).</summary>
    public const string EnableSettingKey = "reactor.statusapi.enabled";

    private const int MmfCapacity = 64 * 1024;        // 64 KiB — far larger than any snapshot
    private const int MaxPipeServerInstances = 16;    // concurrent named-pipe server instances
    private const int SubscribeIntervalMs = 500;      // SUBSCRIBE push cadence

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static ReactorStatusApiService I { get; } = new();

    // ---------------------------------------------------------------- state ----
    private readonly object _gate = new();
    private Func<ReactorStatusSnapshot>? _provider;   // live-sim binding (supplied by the module)
    private ReactorStatusSnapshot _last = ReactorStatusSnapshot.Offline(0);
    private long _sequence;

    private bool _enabled;
    private bool _started;

    private MemoryMappedFile? _mmf;
    private Mutex? _mmfMutex;

    private CancellationTokenSource? _pipeCts;
    private readonly List<Task> _pipeLoops = new();

    private ReactorStatusApiService()
    {
        _enabled = SettingsStore.Get(EnableSettingKey, "true") != "false";
        // Clean up the server + MMF on process exit so we never leak the kernel objects.
        try { AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown(); } catch { }
    }

    /// <summary>服務是否啟用 · Whether the API is enabled (persisted, default ON).</summary>
    public bool Enabled => _enabled;

    /// <summary>服務是否正在運行（管道＋MMF 已開）· Whether the transports are currently live.</summary>
    public bool IsRunning => _started;

    // ---------------------------------------------------------------- binding ----
    /// <summary>
    /// 綁定到實時模擬 · Bind the API to the live <see cref="ReactorSimService"/>. Call this once from the
    /// reactor module/window on load. After binding, call <see cref="Publish"/> each tick.
    /// </summary>
    public void Bind(ReactorSimService sim)
    {
        if (sim is null) return;
        Bind(() => ReactorStatusSnapshot.FromSim(sim, 0)); // sequence is stamped at publish time
    }

    /// <summary>
    /// 綁定到自訂狀態提供者 · Bind to a custom snapshot provider. The provider's <c>sequence</c> field is
    /// ignored — the service stamps a monotonically increasing sequence at publish time.
    /// </summary>
    public void Bind(Func<ReactorStatusSnapshot> provider)
    {
        lock (_gate) _provider = provider;
    }

    /// <summary>
    /// 解除綁定（反應堆頁面關閉時）· Unbind when the reactor page closes. The service keeps serving the
    /// LAST snapshot; if you prefer an explicit "offline" snapshot, call <see cref="PublishOffline"/>.
    /// </summary>
    public void Unbind()
    {
        lock (_gate) _provider = null;
    }

    // ---------------------------------------------------------------- lifecycle ----
    /// <summary>
    /// 啟動服務（自動啟動用）· Start the transports if enabled. Idempotent and exception-safe — a failure
    /// to open one transport never throws to the caller and never prevents the other.
    /// </summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_started) return;
            if (!_enabled)
            {
                // Still publish an offline snapshot to memory so a future enable has a value, but do
                // not open any kernel objects while disabled.
                return;
            }
            _started = true;
        }

        TryOpenMmf();
        StartPipeServer();
        // Seed an initial snapshot so early readers see something coherent.
        PublishOffline();
    }

    /// <summary>
    /// 設定啟用狀態並持久化 · Enable or disable the API and persist the choice. Disabling tears down the
    /// transports; enabling (re)starts them.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        bool changed;
        lock (_gate)
        {
            changed = _enabled != enabled;
            _enabled = enabled;
        }
        if (!changed) return;
        SettingsStore.Set(EnableSettingKey, enabled ? "true" : "false");
        if (enabled) Start();
        else Shutdown();
    }

    // ---------------------------------------------------------------- publish ----
    /// <summary>
    /// 發佈一個快照（每個反應堆 tick 呼叫）· Publish a fresh snapshot from the bound provider. Call this
    /// every reactor tick. Stamps a new sequence + UTC timestamp, writes the MMF and caches it for the
    /// named-pipe servers. Safe to call even when disabled (it just caches).
    /// </summary>
    public void Publish()
    {
        Func<ReactorStatusSnapshot>? p;
        lock (_gate) p = _provider;
        ReactorStatusSnapshot snap;
        if (p is null) { PublishOffline(); return; }
        try { snap = p(); }
        catch { PublishOffline(); return; }
        Stamp(ref snap);
        Store(snap);
    }

    /// <summary>反應堆離線快照 · Publish a "reactor offline" snapshot (isGenerating=false).</summary>
    public void PublishOffline()
    {
        var snap = ReactorStatusSnapshot.Offline(0);
        Stamp(ref snap);
        Store(snap);
    }

    private void Stamp(ref ReactorStatusSnapshot snap)
    {
        snap.SchemaVersion = ReactorStatusSnapshot.CurrentSchemaVersion;
        snap.Sequence = Interlocked.Increment(ref _sequence);
        snap.TimestampUtc = DateTime.UtcNow.ToString("o");
    }

    private void Store(ReactorStatusSnapshot snap)
    {
        lock (_gate) _last = snap;
        WriteMmf(snap);
    }

    /// <summary>取得最近一次快照（伺服器內部用）· Get the most recent cached snapshot.</summary>
    public ReactorStatusSnapshot LastSnapshot { get { lock (_gate) return _last; } }

    // ---------------------------------------------------------------- MMF ----
    private void TryOpenMmf()
    {
        try
        {
            _mmfMutex = new Mutex(false, MutexName);
        }
        catch { _mmfMutex = null; }
        try
        {
            _mmf = MemoryMappedFile.CreateOrOpen(MmfName, MmfCapacity, MemoryMappedFileAccess.ReadWrite);
        }
        catch { _mmf = null; }
    }

    private void WriteMmf(ReactorStatusSnapshot snap)
    {
        MemoryMappedFile? mmf;
        Mutex? mtx;
        lock (_gate) { mmf = _mmf; mtx = _mmfMutex; }
        if (mmf is null) return;

        byte[] payload;
        try { payload = JsonSerializer.SerializeToUtf8Bytes(snap, JsonOpts); }
        catch { return; }
        if (payload.Length + 4 > MmfCapacity) return; // never overflow the view

        bool held = false;
        try
        {
            if (mtx is not null)
            {
                try { held = mtx.WaitOne(200); }
                catch (AbandonedMutexException) { held = true; } // previous owner died; we now own it
            }
            using var view = mmf.CreateViewAccessor(0, payload.Length + 4, MemoryMappedFileAccess.Write);
            view.Write(0, payload.Length);            // 4-byte length prefix (little-endian Int32)
            view.WriteArray(4, payload, 0, payload.Length);
        }
        catch { /* best effort — never throw from a publish */ }
        finally
        {
            if (held && mtx is not null) { try { mtx.ReleaseMutex(); } catch { } }
        }
    }

    // ---------------------------------------------------------------- named pipe ----
    private void StartPipeServer()
    {
        _pipeCts = new CancellationTokenSource();
        var ct = _pipeCts.Token;
        for (int i = 0; i < MaxPipeServerInstances; i++)
            _pipeLoops.Add(Task.Run(() => PipeAcceptLoopAsync(ct)));
    }

    private async Task PipeAcceptLoopAsync(CancellationToken ct)
    {
        // Each loop owns at most one connected client at a time, recycling a fresh server instance
        // after every accept so many clients can connect concurrently across the loops.
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.InOut,
                    MaxPipeServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                await ServeClientAsync(server, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (IOException) { /* client churn / broken pipe — recycle */ }
            catch (ObjectDisposedException) { /* shutting down */ }
            catch { /* never crash the accept loop on any client error */ }
            finally
            {
                try { server?.Dispose(); } catch { }
            }
        }
    }

    private async Task ServeClientAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, leaveOpen: true);
        using var writer = new StreamWriter(server, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };

        string? cmd = await reader.ReadLineAsync().ConfigureAwait(false);
        cmd = (cmd ?? "GET").Trim().ToUpperInvariant();

        if (cmd == "SUBSCRIBE")
        {
            while (!ct.IsCancellationRequested && server.IsConnected)
            {
                string line = SnapshotJson();
                await writer.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
                try { await Task.Delay(SubscribeIntervalMs, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
        else // "GET" (and any unknown command degrades to a single snapshot)
        {
            await writer.WriteLineAsync(SnapshotJson().AsMemory(), ct).ConfigureAwait(false);
        }
    }

    private string SnapshotJson()
    {
        ReactorStatusSnapshot snap;
        lock (_gate) snap = _last;
        try { return JsonSerializer.Serialize(snap, JsonOpts); }
        catch { return "{}"; }
    }

    // ---------------------------------------------------------------- shutdown ----
    /// <summary>
    /// 關閉服務並清理核心物件 · Tear down the pipe servers, MMF and mutex. Safe to call multiple times and
    /// from <see cref="AppDomain.ProcessExit"/>.
    /// </summary>
    public void Shutdown()
    {
        CancellationTokenSource? cts;
        Task[] loops;
        MemoryMappedFile? mmf;
        Mutex? mtx;
        lock (_gate)
        {
            if (!_started) { return; }
            _started = false;
            cts = _pipeCts; _pipeCts = null;
            loops = _pipeLoops.ToArray(); _pipeLoops.Clear();
            mmf = _mmf; _mmf = null;
            mtx = _mmfMutex; _mmfMutex = null;
        }

        try { cts?.Cancel(); } catch { }
        // Nudge any servers stuck in WaitForConnection by briefly connecting as a client.
        try
        {
            using var nudge = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            nudge.Connect(50);
        }
        catch { }
        try { Task.WaitAll(loops, 1000); } catch { }
        try { cts?.Dispose(); } catch { }
        try { mmf?.Dispose(); } catch { }
        try { mtx?.Dispose(); } catch { }
    }
}

/// <summary>
/// 反應堆狀態快照 DTO（穩定、有版本）· The stable, versioned reactor-status snapshot DTO published over
/// both transports. Property names map 1:1 to the wire JSON (camelCase via attributes) so the
/// drop-in client SDK can deserialize without referencing WinForge.
/// </summary>
public struct ReactorStatusSnapshot
{
    /// <summary>目前結構版本 · The current wire schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("sequence")] public long Sequence { get; set; }
    [JsonPropertyName("timestampUtc")] public string TimestampUtc { get; set; }
    [JsonPropertyName("mode")] public string Mode { get; set; }
    [JsonPropertyName("powerPercent")] public double PowerPercent { get; set; }
    [JsonPropertyName("thermalMW")] public double ThermalMW { get; set; }
    [JsonPropertyName("electricMW")] public double ElectricMW { get; set; }
    [JsonPropertyName("isGenerating")] public bool IsGenerating { get; set; }
    [JsonPropertyName("isScrammed")] public bool IsScrammed { get; set; }
    [JsonPropertyName("isMeltdown")] public bool IsMeltdown { get; set; }
    [JsonPropertyName("primaryPressureMPa")] public double PrimaryPressureMPa { get; set; }
    [JsonPropertyName("coolantAvgC")] public double CoolantAvgC { get; set; }
    [JsonPropertyName("reactorPeriodS")] public double ReactorPeriodS { get; set; }
    [JsonPropertyName("activeAlarms")] public string[] ActiveAlarms { get; set; }

    /// <summary>由實時模擬建構快照 · Build a snapshot from the live sim.</summary>
    public static ReactorStatusSnapshot FromSim(ReactorSimService sim, long sequence)
    {
        var alarms = new List<string>();
        foreach (ReactorAlarm a in Enum.GetValues(typeof(ReactorAlarm)))
            if (sim.Alarm(a)) alarms.Add(a.ToString());

        // "Generating" means the plant is actually delivering electrical power to the grid:
        // synchronized, not tripped / melted down, and producing more than a trivial amount.
        bool generating =
            sim.GeneratorBreakerClosed
            && sim.ElectricPowerMW > 1.0
            && !sim.IsScrammed
            && sim.Mode != ReactorMode.Meltdown
            && sim.Mode != ReactorMode.Tripped;

        return new ReactorStatusSnapshot
        {
            SchemaVersion = CurrentSchemaVersion,
            Sequence = sequence,
            TimestampUtc = DateTime.UtcNow.ToString("o"),
            Mode = sim.Mode.ToString(),
            PowerPercent = sim.NeutronPowerFraction * 100.0,
            ThermalMW = sim.ThermalPowerMW,
            ElectricMW = sim.ElectricPowerMW,
            IsGenerating = generating,
            IsScrammed = sim.IsScrammed,
            IsMeltdown = sim.Mode == ReactorMode.Meltdown,
            PrimaryPressureMPa = sim.PrimaryPressure,
            CoolantAvgC = sim.Tavg,
            ReactorPeriodS = sim.ReactorPeriodSeconds,
            ActiveAlarms = alarms.ToArray(),
        };
    }

    /// <summary>反應堆離線快照 · An "offline" snapshot (reactor page closed / API just started).</summary>
    public static ReactorStatusSnapshot Offline(long sequence) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Sequence = sequence,
        TimestampUtc = DateTime.UtcNow.ToString("o"),
        Mode = "Offline",
        PowerPercent = 0,
        ThermalMW = 0,
        ElectricMW = 0,
        IsGenerating = false,
        IsScrammed = false,
        IsMeltdown = false,
        PrimaryPressureMPa = 0,
        CoolantAvgC = 0,
        ReactorPeriodS = 0,
        ActiveAlarms = Array.Empty<string>(),
    };
}
