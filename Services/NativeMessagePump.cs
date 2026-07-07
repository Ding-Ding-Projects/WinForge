using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// Small STA Win32 message pump for native hooks, timers, and lightweight hidden windows.
/// Keeps that work off the WinUI dispatcher while still giving SetWindowsHookEx/SetTimer a real pump.
/// </summary>
internal sealed class NativeMessagePump : IDisposable
{
    private const uint WM_APP_ACTION = 0x8000 + 0x301;
    private const uint WM_APP_QUIT = 0x8000 + 0x302;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private readonly string _name;
    private readonly object _gate = new();
    private readonly ConcurrentQueue<Action> _actions = new();
    private ManualResetEventSlim? _ready;
    private Thread? _thread;
    private uint _threadId;
    private bool _disposed;

    public NativeMessagePump(string name) => _name = name;

    public bool IsRunning => Volatile.Read(ref _threadId) != 0;
    public bool IsCurrentThread => GetCurrentThreadId() == Volatile.Read(ref _threadId);

    public void Post(Action action)
    {
        if (action is null) return;
        if (IsCurrentThread)
        {
            CrashLogger.Guard($"native-pump:{_name}", action);
            return;
        }

        EnsureStarted();
        _actions.Enqueue(action);
        uint id = Volatile.Read(ref _threadId);
        if (id == 0 || !PostThreadMessage(id, WM_APP_ACTION, IntPtr.Zero, IntPtr.Zero))
            CrashLogger.Log($"native-pump:{_name}", new InvalidOperationException("Failed to post action to native message pump."));
    }

    public Task InvokeAsync(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Post(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    private void EnsureStarted()
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true } && _threadId != 0) return;
            if (_disposed) throw new ObjectDisposedException(nameof(NativeMessagePump));

            _ready?.Dispose();
            _ready = new ManualResetEventSlim(false);
            _thread = new Thread(() => Pump(_ready))
            {
                IsBackground = true,
                Name = _name,
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();

            if (!_ready.Wait(1500))
                CrashLogger.Log($"native-pump:{_name}", new TimeoutException("Native message pump did not become ready."));
        }
    }

    private void Pump(ManualResetEventSlim ready)
    {
        Volatile.Write(ref _threadId, GetCurrentThreadId());
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // force the thread message queue into existence
        ready.Set();

        try
        {
            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                if (msg.message == WM_APP_ACTION)
                {
                    DrainActions();
                    continue;
                }
                if (msg.message == WM_APP_QUIT)
                    break;

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            DrainActions();
            Volatile.Write(ref _threadId, 0);
        }
    }

    private void DrainActions()
    {
        while (_actions.TryDequeue(out var action))
            CrashLogger.Guard($"native-pump:{_name}", action);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            uint id = Volatile.Read(ref _threadId);
            if (id != 0) PostThreadMessage(id, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
            _ready?.Dispose();
            _ready = null;
        }
    }
}
