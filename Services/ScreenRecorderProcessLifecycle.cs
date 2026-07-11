using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// Process boundary for Screen Recorder. The page only asks ffmpeg to record; this seam owns the
/// redirected-stream drain and makes the stop path bounded and regression-testable without launching ffmpeg.
/// 螢幕錄影嘅程序邊界：呢個 seam 會處理 redirected stream 同有時限嘅停止流程，測試毋須啟動 ffmpeg。
/// </summary>
internal interface IScreenRecorderProcess : IDisposable
{
    bool HasExited { get; }
    void BeginErrorDrain();
    Task<bool> TrySendQuitAsync(TimeSpan timeout);
    Task<bool> WaitForExitAsync(TimeSpan timeout);
    bool TryKill();
}

/// <summary>Concrete managed adapter for the ffmpeg process; stderr is deliberately consumed and discarded.</summary>
internal sealed class ProcessScreenRecorderProcess : IScreenRecorderProcess
{
    private readonly Process _process;

    public ProcessScreenRecorderProcess(Process process) => _process = process;

    public bool HasExited
    {
        get
        {
            try { return _process.HasExited; }
            catch { return true; }
        }
    }

    public void BeginErrorDrain()
    {
        // ffmpeg writes progress and diagnostics continuously to stderr. Leaving the redirected pipe
        // unread can fill its buffer, stop ffmpeg from receiving "q", and deadlock Stop().
        _process.ErrorDataReceived += static (_, _) => { };
        _process.BeginErrorReadLine();
    }

    public async Task<bool> TrySendQuitAsync(TimeSpan timeout)
    {
        try
        {
            await _process.StandardInput.WriteLineAsync("q").WaitAsync(timeout).ConfigureAwait(false);
            await _process.StandardInput.FlushAsync().WaitAsync(timeout).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { _process.StandardInput.Close(); } catch { }
        }
    }

    public async Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        if (HasExited) return true;
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            await _process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return HasExited;
        }
        catch
        {
            return HasExited;
        }
    }

    public bool TryKill()
    {
        try
        {
            if (!HasExited) _process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return HasExited;
        }
    }

    public void Dispose() => _process.Dispose();
}

internal enum ScreenRecorderStopStatus
{
    Saved,
    StopCommandFailed,
    ForcedStop,
    StillRunning,
}

internal readonly record struct ScreenRecorderStopResult(ScreenRecorderStopStatus Status, bool Exited)
{
    public bool Success => Status == ScreenRecorderStopStatus.Saved;
}

/// <summary>
/// Bounded Stop lifecycle. Every awaited stage has an explicit deadline so a stalled encoder cannot hold
/// the UI forever. A forced termination is deliberately not reported as a saved recording.
/// </summary>
internal static class ScreenRecorderProcessLifecycle
{
    internal static readonly TimeSpan StopCommandTimeout = TimeSpan.FromSeconds(2);
    internal static readonly TimeSpan GracefulExitTimeout = TimeSpan.FromSeconds(8);
    internal static readonly TimeSpan ForcedExitTimeout = TimeSpan.FromSeconds(2);

    public static void Begin(IScreenRecorderProcess process)
    {
        ArgumentNullException.ThrowIfNull(process);
        process.BeginErrorDrain();
    }

    public static Task<ScreenRecorderStopResult> StopAsync(IScreenRecorderProcess process)
        => StopAsync(process, StopCommandTimeout, GracefulExitTimeout, ForcedExitTimeout);

    // Internal timeout injection keeps the regression harness process-free while production always uses
    // the conservative user-facing deadlines above.
    internal static async Task<ScreenRecorderStopResult> StopAsync(
        IScreenRecorderProcess process,
        TimeSpan stopCommandTimeout,
        TimeSpan gracefulExitTimeout,
        TimeSpan forcedExitTimeout)
    {
        ArgumentNullException.ThrowIfNull(process);

        bool sentQuit = await CompleteWithin(process.TrySendQuitAsync(stopCommandTimeout), stopCommandTimeout)
            .ConfigureAwait(false);
        if (sentQuit && await CompleteWithin(process.WaitForExitAsync(gracefulExitTimeout), gracefulExitTimeout)
            .ConfigureAwait(false))
        {
            return new ScreenRecorderStopResult(ScreenRecorderStopStatus.Saved, true);
        }

        // A successful q plus an exit observed just after the bounded wait is still a clean save.
        if (sentQuit && process.HasExited)
            return new ScreenRecorderStopResult(ScreenRecorderStopStatus.Saved, true);

        bool terminated = process.TryKill();
        bool exited = (terminated && await CompleteWithin(process.WaitForExitAsync(forcedExitTimeout), forcedExitTimeout)
            .ConfigureAwait(false)) || process.HasExited;

        if (!sentQuit)
            return new ScreenRecorderStopResult(ScreenRecorderStopStatus.StopCommandFailed, exited);
        return new ScreenRecorderStopResult(
            exited ? ScreenRecorderStopStatus.ForcedStop : ScreenRecorderStopStatus.StillRunning,
            exited);
    }

    private static async Task<bool> CompleteWithin(Task<bool> operation, TimeSpan timeout)
    {
        try { return await operation.WaitAsync(timeout).ConfigureAwait(false); }
        catch { return false; }
    }
}
