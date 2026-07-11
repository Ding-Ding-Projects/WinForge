using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace WinForge.Services;

/// <summary>
/// CursorWrap (PowerToys-style) keeps the pointer moving by sending it to the opposite
/// edge of the current display. It is intentionally timer-driven rather than sharing a
/// low-level hook with the overlay utilities, so it stays compatible with Mouse Utilities,
/// Mouse Without Borders, and other global input features.
/// </summary>
public enum CursorWrapActivation
{
    Always = 0,
    CtrlHeld = 1,
    ShiftHeld = 2,
}

public enum CursorWrapMode
{
    Both = 0,
    Horizontal = 1,
    Vertical = 2,
}

public static class CursorWrapService
{
    private const string KeyEnabled = "cursorwrap.enabled";
    private const string KeyActivation = "cursorwrap.activation";
    private const string KeyMode = "cursorwrap.mode";
    private const string KeyDisableSingleMonitor = "cursorwrap.disableSingleMonitor";

    private const int SmCmonitors = 80;
    private const uint MonitorDefaultToNull = 0;
    private const uint MonitorDefaultToNearest = 2;
    private const int VkControl = 0x11;
    private const int VkShift = 0x10;

    private static readonly object Gate = new();
    private static Timer? _timer;
    private static bool _loaded;
    private static bool _enabled;
    private static CursorWrapActivation _activation;
    private static CursorWrapMode _mode;
    private static bool _disableSingleMonitor;
    private static bool _hasLastPoint;
    private static bool _skipNextPoint;
    private static Point _lastPoint;
    private static int _tickInProgress;

    public static bool Enabled
    {
        get { EnsureLoaded(); lock (Gate) return _enabled; }
        set
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (_enabled == value) return;
                _enabled = value;
                SaveLocked();
            }
            Sync();
        }
    }

    public static CursorWrapActivation Activation
    {
        get { EnsureLoaded(); lock (Gate) return _activation; }
        set
        {
            EnsureLoaded();
            lock (Gate)
            {
                var next = value is CursorWrapActivation.CtrlHeld or CursorWrapActivation.ShiftHeld
                    ? value
                    : CursorWrapActivation.Always;
                if (_activation == next) return;
                _activation = next;
                SaveLocked();
            }
        }
    }

    public static CursorWrapMode Mode
    {
        get { EnsureLoaded(); lock (Gate) return _mode; }
        set
        {
            EnsureLoaded();
            lock (Gate)
            {
                var next = value is CursorWrapMode.Horizontal or CursorWrapMode.Vertical
                    ? value
                    : CursorWrapMode.Both;
                if (_mode == next) return;
                _mode = next;
                SaveLocked();
            }
        }
    }

    public static bool DisableWhenSingleMonitor
    {
        get { EnsureLoaded(); lock (Gate) return _disableSingleMonitor; }
        set
        {
            EnsureLoaded();
            lock (Gate)
            {
                if (_disableSingleMonitor == value) return;
                _disableSingleMonitor = value;
                SaveLocked();
            }
        }
    }

    public static int MonitorCount => Math.Max(1, GetSystemMetrics(SmCmonitors));

    /// <summary>Loads persisted settings and starts or stops the background sampler.</summary>
    public static void LoadAndSync()
    {
        EnsureLoaded();
        Sync();
    }

    public static void Sync()
    {
        EnsureLoaded();
        lock (Gate)
        {
            _timer ??= new Timer(OnTick, null, Timeout.Infinite, Timeout.Infinite);
            _hasLastPoint = false;
            _skipNextPoint = false;
            _timer.Change(_enabled ? 0 : Timeout.Infinite, _enabled ? 12 : Timeout.Infinite);
        }
    }

    public static void Stop()
    {
        lock (Gate)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _hasLastPoint = false;
            _skipNextPoint = false;
        }
    }

    private static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_loaded) return;
            _loaded = true;
            _enabled = ReadBool(SettingsStore.Get(KeyEnabled, "false"), false);
            _activation = (CursorWrapActivation)ReadChoice(SettingsStore.Get(KeyActivation, "0"), 0, 2);
            _mode = (CursorWrapMode)ReadChoice(SettingsStore.Get(KeyMode, "0"), 0, 2);
            _disableSingleMonitor = ReadBool(SettingsStore.Get(KeyDisableSingleMonitor, "false"), false);
        }
    }

    private static void SaveLocked()
    {
        SettingsStore.Set(KeyEnabled, _enabled ? "true" : "false");
        SettingsStore.Set(KeyActivation, ((int)_activation).ToString());
        SettingsStore.Set(KeyMode, ((int)_mode).ToString());
        SettingsStore.Set(KeyDisableSingleMonitor, _disableSingleMonitor ? "true" : "false");
    }

    private static bool ReadBool(string value, bool fallback) =>
        bool.TryParse(value, out var parsed) ? parsed : fallback;

    private static int ReadChoice(string value, int minimum, int maximum) =>
        int.TryParse(value, out var parsed) ? Math.Clamp(parsed, minimum, maximum) : minimum;

    private static void OnTick(object? _)
    {
        if (Interlocked.Exchange(ref _tickInProgress, 1) != 0) return;

        try
        {
            lock (Gate)
            {
                if (!_enabled || !GetCursorPos(out var current)) return;

                if (_skipNextPoint)
                {
                    _skipNextPoint = false;
                    Remember(current);
                    return;
                }

                if ((_disableSingleMonitor && MonitorCount <= 1) || !IsActivationSatisfied())
                {
                    Remember(current);
                    return;
                }

                if (!_hasLastPoint)
                {
                    Remember(current);
                    return;
                }

                var monitor = MonitorFromPoint(current, MonitorDefaultToNearest);
                var info = new MonitorInfo { cbSize = Marshal.SizeOf<MonitorInfo>() };
                if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
                {
                    Remember(current);
                    return;
                }

                int dx = current.X - _lastPoint.X;
                int dy = current.Y - _lastPoint.Y;
                var target = current;
                bool moved = false;

                if (_mode is CursorWrapMode.Both or CursorWrapMode.Horizontal)
                {
                    if (current.X <= info.rcMonitor.Left && dx < 0 && IsExposedEdge(info.rcMonitor.Left - 1, current.Y))
                    {
                        target.X = Math.Max(info.rcMonitor.Left, info.rcMonitor.Right - 2);
                        moved = true;
                    }
                    else if (current.X >= info.rcMonitor.Right - 1 && dx > 0 && IsExposedEdge(info.rcMonitor.Right, current.Y))
                    {
                        target.X = Math.Min(info.rcMonitor.Right - 1, info.rcMonitor.Left + 1);
                        moved = true;
                    }
                }

                if (_mode is CursorWrapMode.Both or CursorWrapMode.Vertical)
                {
                    if (current.Y <= info.rcMonitor.Top && dy < 0 && IsExposedEdge(current.X, info.rcMonitor.Top - 1))
                    {
                        target.Y = Math.Max(info.rcMonitor.Top, info.rcMonitor.Bottom - 2);
                        moved = true;
                    }
                    else if (current.Y >= info.rcMonitor.Bottom - 1 && dy > 0 && IsExposedEdge(current.X, info.rcMonitor.Bottom))
                    {
                        target.Y = Math.Min(info.rcMonitor.Bottom - 1, info.rcMonitor.Top + 1);
                        moved = true;
                    }
                }

                if (moved && SetCursorPos(target.X, target.Y))
                {
                    _skipNextPoint = true;
                    Remember(target);
                }
                else
                {
                    Remember(current);
                }
            }
        }
        catch
        {
            // Cursor wrapping must never destabilize the host app if a display disappears mid-sample.
        }
        finally
        {
            Volatile.Write(ref _tickInProgress, 0);
        }
    }

    private static bool IsActivationSatisfied() => _activation switch
    {
        CursorWrapActivation.CtrlHeld => IsKeyDown(VkControl),
        CursorWrapActivation.ShiftHeld => IsKeyDown(VkShift),
        _ => true,
    };

    private static bool IsKeyDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;

    private static bool IsExposedEdge(int x, int y) =>
        MonitorFromPoint(new Point { X = x, Y = y }, MonitorDefaultToNull) == IntPtr.Zero;

    private static void Remember(Point point)
    {
        _lastPoint = point;
        _hasLastPoint = true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(Point point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
