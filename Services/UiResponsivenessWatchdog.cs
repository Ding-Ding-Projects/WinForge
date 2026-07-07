using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.UI.Dispatching;

namespace WinForge.Services;

/// <summary>
/// Lightweight dispatcher-lag probe. It does not fix hangs by itself; it leaves timestamps in the
/// normal WinForge logs when the UI thread stops draining work quickly.
/// </summary>
public static class UiResponsivenessWatchdog
{
    private const int ProbePeriodMs = 1000;
    private const int LagWarningMs = 750;
    private const int HungWarningMs = 5000;
    private const int MinLogGapMs = 30000;

    private static readonly object Gate = new();
    private static DispatcherQueue? _ui;
    private static Timer? _timer;
    private static int _pending;
    private static long _pendingSince;
    private static long _lastLogTick;

    public static void Start(DispatcherQueue ui)
    {
        lock (Gate)
        {
            if (_timer is not null) return;
            _ui = ui;
            _timer = new Timer(_ => Probe(), null, ProbePeriodMs, ProbePeriodMs);
        }
        CrashLogger.Mark("UI watchdog started");
    }

    private static void Probe()
    {
        var ui = _ui;
        if (ui is null) return;

        long now = Stopwatch.GetTimestamp();
        if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
        {
            double pendingMs = ElapsedMs(Volatile.Read(ref _pendingSince), now);
            if (pendingMs >= HungWarningMs)
                LogLag($"Dispatcher has not drained a watchdog probe for {pendingMs:0} ms.", pendingMs);
            return;
        }

        Volatile.Write(ref _pendingSince, now);
        if (!ui.TryEnqueue(() =>
            {
                long drained = Stopwatch.GetTimestamp();
                long started = Volatile.Read(ref _pendingSince);
                Interlocked.Exchange(ref _pending, 0);

                double lagMs = ElapsedMs(started, drained);
                if (lagMs >= LagWarningMs)
                    LogLag($"Dispatcher probe lag was {lagMs:0} ms.", lagMs);
            }))
        {
            Interlocked.Exchange(ref _pending, 0);
        }
    }

    private static double ElapsedMs(long startTicks, long endTicks)
        => (endTicks - startTicks) * 1000.0 / Stopwatch.Frequency;

    private static void LogLag(string message, double lagMs)
    {
        long now = Environment.TickCount64;
        long previous = Interlocked.Read(ref _lastLogTick);
        if (previous != 0 && now - previous < MinLogGapMs) return;
        Interlocked.Exchange(ref _lastLogTick, now);

        ThreadPool.QueueUserWorkItem(_ =>
        {
            CrashLogger.Mark($"UI lag {lagMs:0} ms");
            CrashLogger.Log("ui-responsiveness", new TimeoutException(message));
        });
    }
}
