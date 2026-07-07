using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 前景視窗取樣器（單例）· Foreground-window sampler (singleton). While tracking is on it polls the
/// active window every few seconds, records process name + window title, detects user idle via
/// GetLastInputInfo, and writes finished time-segments to <see cref="ActivityStore"/>. Everything stays
/// on-device. Tracking is ON by default, but can be turned off from the TimeLens page.
/// </summary>
public sealed class ActivityTrackerService
{
    public static ActivityTrackerService I { get; } = new();

    private const string PrefTracking = "timelens.tracking";   // "true"/"false"
    private const string PrefIdleMins = "timelens.idleminutes"; // int, minutes
    private const string PrefPollSecs = "timelens.pollseconds"; // int, seconds

    private readonly object _gate = new();
    private Timer? _timer;

    // The segment currently being accumulated (one per continuous app focus span).
    private string _curProc = "";
    private string _curTitle = "";
    private long _curStartUnix;
    private bool _hasCurrent;
    private bool _wasIdle;
    private bool _hooksInstalled;

    private ActivityTrackerService() => InstallDeathHooks();

    // ===== state / preferences =====

    /// <summary>追蹤緊？ · Is tracking active right now?</summary>
    public bool IsTracking { get; private set; }

    /// <summary>暫停咗？（追蹤開住但用戶撳咗暫停）· Paused by the privacy button (tracking on, sampling held).</summary>
    public bool IsPaused { get; private set; }

    /// <summary>而家係咪閒置 · Is the user currently idle (used by the UI status line)?</summary>
    public bool IsIdle { get; private set; }

    public int IdleMinutes
    {
        get => Math.Clamp(int.TryParse(SettingsStore.Get(PrefIdleMins, "5"), out var v) ? v : 5, 1, 120);
        set { SettingsStore.Set(PrefIdleMins, Math.Clamp(value, 1, 120).ToString()); }
    }

    public int PollSeconds
    {
        get => Math.Clamp(int.TryParse(SettingsStore.Get(PrefPollSecs, "3"), out var v) ? v : 3, 1, 30);
        set { SettingsStore.Set(PrefPollSecs, Math.Clamp(value, 1, 30).ToString()); RestartIfRunning(); }
    }

    /// <summary>狀態改變（開／停／暫停／閒置）· Raised on any state change so the UI can refresh.</summary>
    public event EventHandler? StateChanged;

    /// <summary>剛剛寫低咗一段（畀即時 UI 更新用）· Raised after a segment is flushed to the store.</summary>
    public event EventHandler? SegmentRecorded;

    private void Raise(EventHandler? h) => h?.Invoke(this, EventArgs.Empty);

    /// <summary>App 啟動時按偏好恢復追蹤 · Restore tracking on app start. Default is ON unless disabled.</summary>
    public void InitFromPrefs()
    {
        InstallDeathHooks();
        if (ActivityStore.RecoverOpenCheckpoint(NowUnix()))
            Raise(SegmentRecorded);
        if (SettingsStore.Get(PrefTracking, "true") == "true")
            Start();
    }

    // ===== control =====

    public void Start()
    {
        InstallDeathHooks();
        lock (_gate)
        {
            if (IsTracking) return;
            IsTracking = true;
            IsPaused = false;
            _hasCurrent = false;
            _wasIdle = false;
            SettingsStore.Set(PrefTracking, "true");
            var period = TimeSpan.FromSeconds(PollSeconds);
            _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, period);
        }
        Raise(StateChanged);
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!IsTracking) { return; }
            FlushLocked(NowUnix());
            ActivityStore.ClearOpenCheckpoint();
            _timer?.Dispose();
            _timer = null;
            IsTracking = false;
            IsPaused = false;
            IsIdle = false;
            SettingsStore.Set(PrefTracking, "false");
        }
        Raise(StateChanged);
    }

    public void Toggle() { if (IsTracking) Stop(); else Start(); }

    /// <summary>
    /// App 退出／崩潰／關機前嘅同步落盤 · Synchronously flush the open segment without changing
    /// the user's saved tracking preference, so tracking resumes next launch.
    /// </summary>
    public void FlushForShutdown()
    {
        lock (_gate)
        {
            if (_hasCurrent) FlushLocked(NowUnix());
            _timer?.Dispose();
            _timer = null;
            IsTracking = false;
            IsPaused = false;
            IsIdle = false;
        }
    }

    /// <summary>暫停／繼續取樣（私隱按鈕）· Pause/resume sampling without losing the tracking-on state.</summary>
    public void SetPaused(bool paused)
    {
        lock (_gate)
        {
            if (!IsTracking || IsPaused == paused) return;
            IsPaused = paused;
            if (paused) FlushLocked(NowUnix()); // close the open segment so the gap is honest
            if (paused) ActivityStore.ClearOpenCheckpoint();
        }
        Raise(StateChanged);
    }

    private void RestartIfRunning()
    {
        lock (_gate)
        {
            if (!IsTracking) return;
            _timer?.Dispose();
            _timer = new Timer(_ => Tick(), null, TimeSpan.Zero, TimeSpan.FromSeconds(PollSeconds));
        }
    }

    // ===== poll loop =====

    private void Tick()
    {
        try
        {
            lock (_gate)
            {
                if (!IsTracking || IsPaused) return;
                long now = NowUnix();

                // Idle detection: if the user has been inactive past the threshold, close any open
                // segment and stop accumulating until they return.
                double idleSecs = GetIdleSeconds();
                bool idle = idleSecs >= IdleMinutes * 60;
                if (idle)
                {
                    if (!_wasIdle) { FlushLocked(now); _wasIdle = true; }
                    if (!IsIdle) { IsIdle = true; }
                    return;
                }
                if (_wasIdle) { _wasIdle = false; }
                if (IsIdle) IsIdle = false;

                var (proc, title) = GetForeground();
                if (string.IsNullOrEmpty(proc)) { proc = "Unknown"; title ??= ""; }

                if (!_hasCurrent)
                {
                    BeginLocked(proc, title ?? "", now);
                }
                else if (!string.Equals(proc, _curProc, StringComparison.OrdinalIgnoreCase)
                         || !string.Equals(title ?? "", _curTitle, StringComparison.Ordinal))
                {
                    // Focus changed → close the previous span and open a new one.
                    FlushLocked(now);
                    BeginLocked(proc, title ?? "", now);
                }
                else
                {
                    ActivityStore.SaveOpenCheckpoint(_curProc, _curTitle, _curStartUnix, now);
                }
                // else: same app+title, keep accumulating (end advances at next flush).
            }
        }
        catch { /* never let a poll error kill the timer */ }
    }

    private void BeginLocked(string proc, string title, long now)
    {
        _curProc = proc;
        _curTitle = title;
        _curStartUnix = now;
        _hasCurrent = true;
        ActivityStore.SaveOpenCheckpoint(_curProc, _curTitle, _curStartUnix, now);
    }

    private void FlushLocked(long endUnix)
    {
        if (!_hasCurrent) { ActivityStore.ClearOpenCheckpoint(); return; }
        _hasCurrent = false;
        long start = _curStartUnix;
        if (endUnix <= start) { ActivityStore.ClearOpenCheckpoint(); return; }
        var seg = new ActivitySegment
        {
            Process = _curProc,
            Title = _curTitle,
            StartUnix = start,
            EndUnix = endUnix,
        };
        ActivityStore.Append(seg);
        ActivityStore.ClearOpenCheckpoint();
        Raise(SegmentRecorded);
    }

    private void InstallDeathHooks()
    {
        if (_hooksInstalled) return;
        _hooksInstalled = true;
        try { AppDomain.CurrentDomain.ProcessExit += (_, _) => FlushFinal("process-exit"); } catch { }
        try { AppDomain.CurrentDomain.UnhandledException += (_, _) => FlushFinal("unhandled"); } catch { }
        try { System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, _) => FlushFinal("task"); } catch { }
        try { SystemEvents.SessionEnding += (_, _) => FlushFinal("session-ending"); } catch { }
        try
        {
            SystemEvents.PowerModeChanged += (_, e) =>
            {
                if (e.Mode == PowerModes.Suspend) FlushFinal("suspend");
            };
        }
        catch { }
    }

    private void FlushFinal(string reason)
    {
        try { FlushForShutdown(); }
        catch (Exception ex) { CrashLogger.Log($"activity:flushfinal:{reason}", ex); }
    }

    private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // ===== Win32 =====

    private static (string proc, string? title) GetForeground()
    {
        try
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return ("", null);

            string title = GetTitle(hwnd);

            GetWindowThreadProcessId(hwnd, out uint pid);
            string proc = "";
            if (pid != 0)
            {
                try
                {
                    using var p = Process.GetProcessById((int)pid);
                    proc = p.ProcessName ?? "";
                }
                catch { proc = ""; }
            }
            return (proc, title);
        }
        catch { return ("", null); }
    }

    private static string GetTitle(IntPtr hwnd)
    {
        try
        {
            int len = GetWindowTextLength(hwnd);
            if (len <= 0) return "";
            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }
        catch { return ""; }
    }

    /// <summary>距上次輸入幾耐（秒）· Seconds since the last keyboard/mouse input system-wide.</summary>
    private static double GetIdleSeconds()
    {
        try
        {
            var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (!GetLastInputInfo(ref info)) return 0;
            uint tick = (uint)Environment.TickCount;
            uint last = info.dwTime;
            uint diff = tick >= last ? tick - last : (uint.MaxValue - last + tick);
            return diff / 1000.0;
        }
        catch { return 0; }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }
}
