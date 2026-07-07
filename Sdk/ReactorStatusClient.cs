// =====================================================================================
//  WinForge Reactor Status Client SDK  ·  WinForge 反應堆狀態用戶端 SDK
//  -------------------------------------------------------------------------------------
//  DROP-IN, DEPENDENCY-FREE client for the WinForge reactor's public local status API.
//  Copy THIS SINGLE FILE into any C# project (net6.0+ recommended) — it does NOT reference
//  WinForge. It uses only the BCL: System.IO.Pipes, System.IO.MemoryMappedFiles,
//  System.Text.Json. No NuGet packages required.
//
//  把呢一個檔案複製去你嘅 C# 專案就用得，唔使引用 WinForge，亦唔使任何 NuGet。
//
//  See Sdk/README.md for a copy-paste console example (read status + gate an app on
//  isGenerating so it refuses to run unless the reactor is generating).
// =====================================================================================

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Reactor.Sdk;

/// <summary>
/// 反應堆狀態快照（用戶端 DTO）· Reactor status snapshot as seen by a consuming app.
/// Mirrors the server's stable, versioned wire schema (schemaVersion 1).
/// </summary>
public sealed class ReactorStatus
{
    /// <summary>結構版本 · Wire schema version (currently 1).</summary>
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }

    /// <summary>單調遞增序號（用嚟偵測更新）· Monotonically increasing sequence; bumps every publish.</summary>
    [JsonPropertyName("sequence")] public long Sequence { get; set; }

    /// <summary>發佈時間（UTC, ISO-8601）· Publish time (UTC, ISO-8601 round-trip).</summary>
    [JsonPropertyName("timestampUtc")] public string TimestampUtc { get; set; } = "";

    /// <summary>運行模式 · Operating mode: Shutdown/Startup/Run/Tripped/Meltdown/Offline.</summary>
    [JsonPropertyName("mode")] public string Mode { get; set; } = "Offline";

    /// <summary>反應堆功率（額定百分比）· Reactor neutron power as a percentage of rated.</summary>
    [JsonPropertyName("powerPercent")] public double PowerPercent { get; set; }

    /// <summary>熱功率（MWt）· Thermal power, megawatts thermal.</summary>
    [JsonPropertyName("thermalMW")] public double ThermalMW { get; set; }

    /// <summary>電功率（MWe）· Gross electrical output, megawatts electric.</summary>
    [JsonPropertyName("electricMW")] public double ElectricMW { get; set; }

    /// <summary>是否正在發電（已併網並向電網供電）· True when synchronized and delivering power to the grid.</summary>
    [JsonPropertyName("isGenerating")] public bool IsGenerating { get; set; }

    /// <summary>是否已緊急停堆 · True when the reactor has tripped (SCRAM).</summary>
    [JsonPropertyName("isScrammed")] public bool IsScrammed { get; set; }

    /// <summary>是否爐心熔毀 · True when the core has melted down.</summary>
    [JsonPropertyName("isMeltdown")] public bool IsMeltdown { get; set; }

    /// <summary>一迴路壓力（MPa）· Primary-loop pressure, megapascals.</summary>
    [JsonPropertyName("primaryPressureMPa")] public double PrimaryPressureMPa { get; set; }

    /// <summary>冷卻劑平均溫度（°C）· Average coolant temperature, Celsius.</summary>
    [JsonPropertyName("coolantAvgC")] public double CoolantAvgC { get; set; }

    /// <summary>反應堆週期（秒）· Reactor period, seconds (large = stable).</summary>
    [JsonPropertyName("reactorPeriodS")] public double ReactorPeriodS { get; set; }

    /// <summary>當前活躍警報 · Names of currently-active annunciator alarms.</summary>
    [JsonPropertyName("activeAlarms")] public string[] ActiveAlarms { get; set; } = Array.Empty<string>();
}

/// <summary>
/// 反應堆狀態用戶端 · A dependency-free client for the WinForge reactor's public local status API.
///
/// Three ways to read, all returning the SAME versioned <see cref="ReactorStatus"/>:
///  • <see cref="TryReadShared(out ReactorStatus)"/> — zero-latency memory-mapped-file fast path.
///  • <see cref="RequestAsync(int, CancellationToken)"/> — one snapshot via the named-pipe GET command.
///  • <see cref="SubscribeAsync(CancellationToken)"/> — a streamed snapshot every ~500 ms (pipe SUBSCRIBE).
///
/// Plus convenience helpers to "depend on the reactor":
///  • <see cref="IsReactorGeneratingAsync(CancellationToken)"/> — non-blocking one-shot boolean (prefer this).
///  • <see cref="IsReactorGenerating"/> — synchronous one-shot boolean (background threads only).
///  • <see cref="WaitForGeneratingAsync(CancellationToken, int)"/> — await until the reactor is generating.
///
/// The client is thread-safe and holds no unmanaged state between calls (the MMF/pipe are opened and
/// disposed per operation), so a single instance can be shared or created ad-hoc.
/// </summary>
public sealed class ReactorStatusClient
{
    /// <summary>記憶體映射檔名 · Well-known MMF name.</summary>
    public const string MmfName = "Local\\WinForge.Reactor.Status";
    /// <summary>保護 MMF 嘅互斥鎖名 · Well-known mutex name guarding the MMF.</summary>
    public const string MutexName = "Local\\WinForge.Reactor.Status.Mutex";
    /// <summary>具名管道名稱 · Well-known pipe name.</summary>
    public const string PipeName = "WinForge.Reactor.Status";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ---------------------------------------------------------------- MMF fast path ----
    /// <summary>
    /// 嘗試由記憶體映射檔讀取最新快照（零延遲，無交握）· Try to read the latest snapshot from the
    /// memory-mapped file. This is the fast path: no connection, no handshake — it simply maps the
    /// shared memory and parses the length-prefixed UTF-8 JSON the server rewrites every tick.
    /// </summary>
    /// <param name="status">收到嘅快照（成功時）· The parsed snapshot on success.</param>
    /// <returns>True 表示讀到有效快照 · True if a valid snapshot was read.</returns>
    public bool TryReadShared(out ReactorStatus status)
    {
        status = null!;
        Mutex? mtx = null;
        bool held = false;
        try
        {
            try { Mutex.TryOpenExisting(MutexName, out mtx); } catch { mtx = null; }
            using var mmf = MemoryMappedFile.OpenExisting(MmfName, MemoryMappedFileRights.Read);

            if (mtx is not null)
            {
                try { held = mtx.WaitOne(200); }
                catch (AbandonedMutexException) { held = true; }
            }

            using var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            int len = view.ReadInt32(0);
            if (len <= 0 || len > view.Capacity - 4) return false;
            var buf = new byte[len];
            view.ReadArray(4, buf, 0, len);

            var parsed = JsonSerializer.Deserialize<ReactorStatus>(buf, JsonOpts);
            if (parsed is null) return false;
            status = parsed;
            return true;
        }
        catch
        {
            return false; // MMF not present (reactor never started) or transient read error
        }
        finally
        {
            if (held && mtx is not null) { try { mtx.ReleaseMutex(); } catch { } }
            mtx?.Dispose();
        }
    }

    // ---------------------------------------------------------------- pipe GET ----
    /// <summary>
    /// 透過具名管道請求一個快照（GET）· Request a single snapshot over the named pipe (GET command).
    /// Use this when the MMF fast path is unavailable or you want a guaranteed-fresh value.
    /// </summary>
    /// <param name="timeoutMs">連線＋讀取逾時（毫秒）· Connect + read timeout in milliseconds.</param>
    /// <param name="ct">取消權杖 · Cancellation token.</param>
    /// <returns>快照，或 null（反應堆離線／逾時）· The snapshot, or null if offline/timed out.</returns>
    public async Task<ReactorStatus?> RequestAsync(int timeoutMs = 2000, CancellationToken ct = default)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(timeoutMs);
            await client.ConnectAsync(connectCts.Token).ConfigureAwait(false);

            using var writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            using var reader = new StreamReader(client, Encoding.UTF8, false, 1024, leaveOpen: true);
            await writer.WriteLineAsync("GET".AsMemory(), connectCts.Token).ConfigureAwait(false);

            string? line = await reader.ReadLineAsync().WaitAsync(connectCts.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line)) return null;
            return JsonSerializer.Deserialize<ReactorStatus>(line, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    // ---------------------------------------------------------------- pipe SUBSCRIBE ----
    /// <summary>
    /// 訂閱實時快照串流（每約 500 毫秒一個）· Subscribe to a streamed snapshot every ~500 ms over the
    /// named pipe (SUBSCRIBE command). The stream ends when <paramref name="ct"/> is cancelled or the
    /// server disconnects. Reconnection is the caller's responsibility (wrap in a retry loop if needed).
    /// </summary>
    /// <param name="ct">取消權杖（用嚟結束訂閱）· Cancellation token to end the subscription.</param>
    /// <returns>反應堆狀態嘅非同步串流 · An async stream of reactor-status snapshots.</returns>
    public async IAsyncEnumerable<ReactorStatus> SubscribeAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        NamedPipeClientStream client;
        StreamReader reader;
        StreamWriter writer;
        try
        {
            client = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await client.ConnectAsync(2000, ct).ConfigureAwait(false);
            writer = new StreamWriter(client, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
            reader = new StreamReader(client, Encoding.UTF8, false, 1024, leaveOpen: true);
            await writer.WriteLineAsync("SUBSCRIBE".AsMemory(), ct).ConfigureAwait(false);
        }
        catch
        {
            yield break; // cannot connect — reactor offline
        }

        try
        {
            while (!ct.IsCancellationRequested)
            {
                string? line;
                try { line = await reader.ReadLineAsync().WaitAsync(ct).ConfigureAwait(false); }
                catch { break; } // disconnect / cancel
                if (line is null) break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                ReactorStatus? snap = null;
                try { snap = JsonSerializer.Deserialize<ReactorStatus>(line, JsonOpts); }
                catch { /* skip a malformed line */ }
                if (snap is not null) yield return snap;
            }
        }
        finally
        {
            try { writer.Dispose(); } catch { }
            try { reader.Dispose(); } catch { }
            try { client.Dispose(); } catch { }
        }
    }

    // ---------------------------------------------------------------- convenience ----
    /// <summary>
    /// 反應堆是否正在發電（非同步一次性）· Is the reactor currently generating? Tries the MMF fast path
    /// first, then falls back to a short pipe GET. Returns false if the reactor is offline.
    /// Prefer this over <see cref="IsReactorGenerating"/> — it never blocks a thread on the pipe GET.
    /// </summary>
    public async Task<bool> IsReactorGeneratingAsync(CancellationToken ct = default)
    {
        if (TryReadShared(out var s)) return s.IsGenerating;
        var snap = await RequestAsync(1000, ct).ConfigureAwait(false);
        return snap?.IsGenerating ?? false;
    }

    /// <summary>
    /// 反應堆是否正在發電（同步一次性）· Is the reactor currently generating? Tries the MMF fast path first,
    /// then falls back to a short pipe GET. Returns false if the reactor is offline.
    /// WARNING · 警告: the fallback blocks on an async pipe GET, so this MUST be called from a background
    /// thread — never from the UI thread (it will deadlock the dispatcher). UI callers should
    /// <c>await</c> <see cref="IsReactorGeneratingAsync"/> instead.
    /// </summary>
    public bool IsReactorGenerating()
    {
        if (TryReadShared(out var s)) return s.IsGenerating;
        try { return RequestAsync(1000).GetAwaiter().GetResult()?.IsGenerating ?? false; }
        catch { return false; }
    }

    /// <summary>
    /// 等到反應堆開始發電 · Await until the reactor is generating (the "depend on the reactor" helper).
    /// Polls the MMF fast path (falling back to a pipe GET) on <paramref name="pollMs"/> intervals.
    /// Completes immediately if the reactor is already generating; throws if cancelled.
    /// </summary>
    /// <param name="ct">取消權杖 · Cancellation token.</param>
    /// <param name="pollMs">輪詢間隔（毫秒）· Poll interval in milliseconds.</param>
    public async Task WaitForGeneratingAsync(CancellationToken ct = default, int pollMs = 1000)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (TryReadShared(out var s) ? s.IsGenerating
                : (await RequestAsync(1000, ct).ConfigureAwait(false))?.IsGenerating == true)
                return;
            await Task.Delay(pollMs, ct).ConfigureAwait(false);
        }
    }
}
