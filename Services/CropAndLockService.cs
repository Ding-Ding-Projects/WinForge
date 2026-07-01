using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 一個活躍嘅裁切／縮圖視窗 · One live cropped / thumbnail host window spawned by Crop And Lock.
/// </summary>
public sealed class CropLockEntry
{
    public IntPtr HostHandle { get; init; }       // our floating host window (HWND)
    public IntPtr SourceHandle { get; init; }      // the window being mirrored
    public string SourceTitle { get; init; } = ""; // title of the source window (for the list)
    public bool Thumbnail { get; init; }           // true = live thumbnail, false = reparent/crop view
}

/// <summary>
/// 裁切與鎖定引擎 · Crop And Lock engine — a native clone of PowerToys Crop And Lock.
///
/// Two modes, both driven by DWM thumbnails:
///  • Thumbnail mode — a small always-on-top host window live-mirrors a chosen region of a target
///    window (DwmRegisterThumbnail + DwmUpdateThumbnailProperties with rcSource = the cropped rect),
///    so you can watch part of a window while it sits behind others.
///  • Reparent / crop mode — a host window shows ONLY the selected region of a target window. True
///    cross-process reparenting (SetParent of another process's HWND) is fragile and intentionally
///    NOT attempted; instead we present the same "cropped view" through a DWM thumbnail sized exactly
///    to the crop rect. (See limitations note in the module page.)
///
/// Each host window is a real Win32 popup (CreateWindowExW) — movable, resizable, closable, and kept
/// topmost — created on a dedicated background message-pump thread so it lives independently of the
/// WinUI dispatcher. A configurable global hotkey (RegisterHotKey) can trigger either flow. All state
/// is in-process; nothing is launched or redirected. Bilingual throughout the UI that drives it.
/// </summary>
public static class CropAndLockService
{
    // ===================== active windows (observable for the UI) =====================

    /// <summary>所有活躍嘅裁切／縮圖視窗 · Every live cropped/thumbnail host window.</summary>
    public static ObservableCollection<CropLockEntry> Active { get; } = new();

    /// <summary>有變動（新增／關閉／熱鍵）時通知 UI · Raised when the active set or hotkeys change.</summary>
    public static event Action? Changed;

    /// <summary>提示用嘅最近事件文字 · Last status line for the UI (e.g. "Created a thumbnail").</summary>
    public static string LastEvent { get; private set; } = "";

    // ===================== Win32 interop =====================

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize, style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DWM_THUMBNAIL_PROPERTIES
    {
        public uint dwFlags;
        public RECT rcDestination;
        public RECT rcSource;
        public byte opacity;
        public bool fVisible;
        public bool fSourceClientAreaOnly;
    }

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string? windowName,
        uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
    [DllImport("user32.dll")] private static extern bool UpdateWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG msg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG msg, IntPtr hWnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRectEx(ref RECT rect, uint style, bool menu, uint exStyle);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr inst, int id);
    [DllImport("gdi32.dll")] private static extern IntPtr GetStockObject(int obj);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumb);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES props);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);

    private const uint WS_OVERLAPPEDWINDOW = 0x00CF0000;
    private const uint WS_CLIPCHILDREN = 0x02000000;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_TOOLWINDOW = 0x00000080;
    private const int SW_SHOW = 5;
    private const int IDC_ARROW = 32512;
    private const int BLACK_BRUSH = 4;

    private const uint WM_DESTROY = 0x0002, WM_SIZE = 0x0005, WM_NCDESTROY = 0x0082, WM_HOTKEY = 0x0312;
    private const uint WM_APP = 0x8000;
    private const uint WM_APP_CREATE = WM_APP + 1;     // spawn a host window on the pump thread
    private const uint WM_APP_CLOSE = WM_APP + 2;      // close one host window
    private const uint WM_APP_QUIT = WM_APP + 3;       // tear the pump down
    private const uint WM_APP_RELOAD_HK = WM_APP + 4;  // re-register the global hotkeys

    private const uint DWM_TNP_RECTDESTINATION = 0x00000001;
    private const uint DWM_TNP_RECTSOURCE = 0x00000002;
    private const uint DWM_TNP_OPACITY = 0x00000004;
    private const uint DWM_TNP_VISIBLE = 0x00000008;
    private const uint DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;

    private const string ClassName = "WinForgeCropAndLockHost";

    // ===================== pump thread state =====================

    private static Thread? _pumpThread;
    private static uint _pumpThreadId;
    // Signalled by the pump thread once _pumpThreadId is populated, so callers can wait for
    // readiness WITHOUT a Thread.Sleep spin on the UI thread.
    private static readonly ManualResetEventSlim _pumpReady = new(false);
    private static bool _running;
    private static WndProc? _wndProc;             // keep the delegate alive
    private static bool _classRegistered;

    // per-host bookkeeping (touched only on the pump thread)
    private sealed class HostState
    {
        public IntPtr Thumb;       // HTHUMBNAIL
        public RECT Source;        // crop rect in source-window space
        public IntPtr SourceHwnd;
    }
    private static readonly Dictionary<IntPtr, HostState> _hosts = new();

    // a request to create a host, marshalled to the pump thread
    private sealed class CreateRequest
    {
        public IntPtr Source;
        public string Title = "";
        public RECT Crop;          // in source client-area space (physical px relative to client origin)
        public bool Thumbnail;
        public IntPtr Result;       // filled in by the pump thread
        // Completed by the pump thread once the host is created (or failed). Awaited by the caller
        // instead of blocking, so the UI thread never stalls waiting for the STA pump. Runs
        // continuations asynchronously to avoid ever resuming the awaiter on the pump thread.
        public readonly TaskCompletionSource<IntPtr> Done =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private static readonly object _reqLock = new();
    private static readonly Queue<CreateRequest> _pending = new();
    private static readonly Queue<IntPtr> _closeRequests = new();

    // ===================== public API =====================

    /// <summary>引擎係咪行緊（泵已啟動）· Whether the background pump is running.</summary>
    public static bool IsRunning => _running;

    /// <summary>確保背景泵已啟動（idempotent）· Make sure the background message pump is up.</summary>
    public static void EnsureStarted()
    {
        if (_running) return;
        _running = true;
        _pumpReady.Reset();
        _pumpThread = new Thread(PumpLoop) { IsBackground = true, Name = "WinForge-CropAndLock" };
        _pumpThread.SetApartmentState(ApartmentState.STA);
        _pumpThread.Start();
        // Wait for the pump thread to publish its id, then (re)apply hotkeys — but do it OFF the
        // calling (UI) thread so we never Thread.Sleep-spin the dispatcher waiting for the STA pump
        // to spin up. ReloadHotkeys() just PostThreadMessages the (now-ready) pump, so it is safe
        // to run from a background task.
        Task.Run(() =>
        {
            _pumpReady.Wait(TimeSpan.FromMilliseconds(250));
            ReloadHotkeys();
        });
    }

    /// <summary>
    /// 由視窗 + 螢幕區域整一個裁切／縮圖視窗 · Create a cropped/thumbnail host window for a source window
    /// and a chosen region. <paramref name="screenRect"/> is in PHYSICAL screen pixels (as returned by
    /// RegionSelector). Returns true on success.
    /// </summary>
    public static async Task<bool> CreateFromScreenRectAsync(IntPtr source, string title, (int x, int y, int w, int h) screenRect, bool thumbnail)
    {
        try
        {
            if (source == IntPtr.Zero || !IsWindow(source)) { Note("Invalid window.", "視窗無效。"); return false; }
            if (screenRect.w < 8 || screenRect.h < 8) { Note("Region too small.", "區域太細。"); return false; }
            EnsureStarted();

            // DWM's rcSource (with fSourceClientAreaOnly = false) is interpreted relative to the source
            // window's DWM-composited surface — i.e. its EXTENDED FRAME BOUNDS, the rectangle DWM actually
            // renders. Translate the picked physical-screen rect into that space by subtracting the frame's
            // top-left. Fall back to GetWindowRect if the DWM attribute is unavailable.
            RECT frame;
            if (DwmGetWindowAttribute(source, DWMWA_EXTENDED_FRAME_BOUNDS, out frame, Marshal.SizeOf<RECT>()) != 0
                || frame.Width <= 0 || frame.Height <= 0)
            {
                if (!GetWindowRect(source, out frame)) { Note("Could not read the window bounds.", "讀唔到視窗邊界。"); return false; }
            }

            var crop = new RECT
            {
                Left = screenRect.x - frame.Left,
                Top = screenRect.y - frame.Top,
                Right = screenRect.x - frame.Left + screenRect.w,
                Bottom = screenRect.y - frame.Top + screenRect.h,
            };
            // Clamp into the frame so we never feed DWM a source rect outside the composited surface.
            int fw = frame.Width, fh = frame.Height;
            crop.Left = Math.Clamp(crop.Left, 0, Math.Max(0, fw));
            crop.Top = Math.Clamp(crop.Top, 0, Math.Max(0, fh));
            crop.Right = Math.Clamp(crop.Right, crop.Left + 1, Math.Max(crop.Left + 1, fw));
            crop.Bottom = Math.Clamp(crop.Bottom, crop.Top + 1, Math.Max(crop.Top + 1, fh));
            // Explicitly guard against an inverted / zero crop before it ever reaches DWM. A degenerate
            // source rect (Right<=Left or Bottom<=Top) makes DwmRegisterThumbnail/UpdateThumbnailProperties crash.
            if (crop.Right <= crop.Left || crop.Bottom <= crop.Top
                || crop.Width < 8 || crop.Height < 8)
            { Note("Region is outside the window.", "區域喺視窗範圍以外。"); return false; }

            var req = new CreateRequest { Source = source, Title = title, Crop = crop, Thumbnail = thumbnail };
            lock (_reqLock) _pending.Enqueue(req);
            PostThreadMessage(_pumpThreadId, WM_APP_CREATE, IntPtr.Zero, IntPtr.Zero);

            // Await (with a 4s guard) for the host to be created on the pump thread — never block the
            // calling (UI) thread. Same timeout semantics as before: on timeout we treat it as a failure.
            // No ConfigureAwait(false): the continuation must resume on the caller's (UI) context so the
            // post-await mutation of the UI-bound Active collection stays on the UI thread, matching the
            // original synchronous behaviour.
            var completed = await Task.WhenAny(req.Done.Task, Task.Delay(4000));
            IntPtr result = completed == req.Done.Task ? req.Done.Task.Result : IntPtr.Zero;

            if (result == IntPtr.Zero)
            {
                Note("Could not create the window.", "無法建立視窗。");
                return false;
            }

            var entry = new CropLockEntry
            {
                HostHandle = result,
                SourceHandle = source,
                SourceTitle = string.IsNullOrWhiteSpace(title) ? Loc.I.Pick("(untitled)", "（無標題）") : title,
                Thumbnail = thumbnail,
            };
            Active.Add(entry);
            Note(thumbnail ? "Created a live thumbnail." : "Created a cropped view.",
                 thumbnail ? "已建立即時縮圖。" : "已建立裁切檢視。");
            Changed?.Invoke();
            return true;
        }
        catch
        {
            // Never throw to the UI — surface a friendly note and report failure.
            Note("Could not create the window.", "無法建立視窗。");
            return false;
        }
    }

    /// <summary>關閉一個裁切視窗 · Close one host window and drop it from the active set.</summary>
    public static void Close(CropLockEntry entry)
    {
        if (entry is null) return;
        CloseHost(entry.HostHandle);
        Active.Remove(entry);
        Changed?.Invoke();
    }

    /// <summary>全部關閉 · Close every active host window.</summary>
    public static void CloseAll()
    {
        foreach (var e in Active.ToList()) CloseHost(e.HostHandle);
        Active.Clear();
        Changed?.Invoke();
    }

    private static void CloseHost(IntPtr host)
    {
        if (host == IntPtr.Zero || _pumpThreadId == 0) return;
        lock (_reqLock) _closeRequests.Enqueue(host);
        PostThreadMessage(_pumpThreadId, WM_APP_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    /// <summary>停止引擎，關晒所有視窗同熱鍵 · Shut the engine down (all windows + hotkeys).</summary>
    public static void Stop()
    {
        CloseAll();
        if (_running && _pumpThreadId != 0)
            PostThreadMessage(_pumpThreadId, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
        _running = false;
    }

    // ===================== the message pump =====================

    private static void PumpLoop()
    {
        _pumpThreadId = GetCurrentThreadId();
        _pumpReady.Set(); // let EnsureStarted's waiter proceed without a UI-thread sleep-spin
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0); // force a message queue
        EnsureClassRegistered();
        RegisterHotkeysOnThread();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            switch (msg.message)
            {
                case WM_APP_CREATE:
                    DrainCreate();
                    break;
                case WM_APP_CLOSE:
                    DrainClose();
                    break;
                case WM_HOTKEY:
                    OnHotkey((int)msg.wParam);
                    break;
                case WM_APP_RELOAD_HK:
                    RegisterHotkeysOnThread();
                    break;
                case WM_APP_QUIT:
                    UnregisterHotkeysOnThread();
                    foreach (var h in _hosts.Keys.ToList()) DestroyHost(h);
                    return;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private static void EnsureClassRegistered()
    {
        if (_classRegistered) return;
        var inst = GetModuleHandle(null);
        _wndProc = HostWndProc;
        var cls = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            style = 0x0002 | 0x0001, // CS_HREDRAW | CS_VREDRAW
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = inst,
            hCursor = LoadCursor(IntPtr.Zero, IDC_ARROW),
            hbrBackground = GetStockObject(BLACK_BRUSH),
            lpszClassName = ClassName,
        };
        RegisterClassEx(ref cls);
        _classRegistered = true;
    }

    private static void DrainCreate()
    {
        while (true)
        {
            CreateRequest? req;
            lock (_reqLock) req = _pending.Count > 0 ? _pending.Dequeue() : null;
            if (req is null) break;
            IntPtr result;
            try { result = SpawnHost(req); }
            catch { result = IntPtr.Zero; }
            req.Result = result;
            // Signal the awaiting caller. TrySetResult so a timed-out (already-abandoned) request
            // never faults the pump thread.
            req.Done.TrySetResult(result);
        }
    }

    private static void DrainClose()
    {
        while (true)
        {
            IntPtr host = IntPtr.Zero;
            lock (_reqLock) if (_closeRequests.Count > 0) host = _closeRequests.Dequeue();
            if (host == IntPtr.Zero) break;
            DestroyHost(host);
        }
    }

    private static IntPtr SpawnHost(CreateRequest req)
    {
        int w = req.Crop.Width, h = req.Crop.Height;
        // Cap an over-large view so the floating window stays usable; the thumbnail scales to fit.
        int capW = Math.Min(w, 1200), capH = Math.Min(h, 900);

        uint style = WS_OVERLAPPEDWINDOW | WS_CLIPCHILDREN;
        var rect = new RECT { Left = 0, Top = 0, Right = capW, Bottom = capH };
        AdjustWindowRectEx(ref rect, style, false, WS_EX_TOPMOST | WS_EX_TOOLWINDOW);
        int adjW = rect.Width, adjH = rect.Height;

        var inst = GetModuleHandle(null);
        var title = req.Thumbnail
            ? $"{Loc.I.Pick("Thumbnail", "縮圖")} · {req.Title}"
            : $"{Loc.I.Pick("Cropped", "裁切")} · {req.Title}";

        IntPtr host = CreateWindowEx(
            WS_EX_TOPMOST | WS_EX_TOOLWINDOW, ClassName, title, style,
            unchecked((int)0x80000000), unchecked((int)0x80000000), // CW_USEDEFAULT
            adjW, adjH, IntPtr.Zero, IntPtr.Zero, inst, IntPtr.Zero);
        if (host == IntPtr.Zero) return IntPtr.Zero;

        // Register a DWM thumbnail of the source into our host, sourced to the crop rect.
        if (DwmRegisterThumbnail(host, req.Source, out var thumb) != 0 || thumb == IntPtr.Zero)
        {
            DestroyWindow(host);
            return IntPtr.Zero;
        }

        var state = new HostState { Thumb = thumb, Source = req.Crop, SourceHwnd = req.Source };
        _hosts[host] = state;
        ApplyThumbnail(host, state);

        SetWindowPos(host, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        ShowWindow(host, SW_SHOW);
        UpdateWindow(host);
        SetForegroundWindow(host);
        return host;
    }

    /// <summary>更新縮圖嘅來源／目的矩形（按 host 客戶區比例縮放）· Recompute and push thumbnail props.</summary>
    private static void ApplyThumbnail(IntPtr host, HostState state)
    {
        if (state.Thumb == IntPtr.Zero) return;
        GetClientRect(host, out var clientRect);
        var dest = ComputeDestRect(clientRect, state.Source);

        var props = new DWM_THUMBNAIL_PROPERTIES
        {
            dwFlags = DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_RECTDESTINATION | DWM_TNP_RECTSOURCE | DWM_TNP_SOURCECLIENTAREAONLY,
            fSourceClientAreaOnly = false,
            fVisible = true,
            opacity = 255,
            rcDestination = dest,
            rcSource = state.Source,
        };
        DwmUpdateThumbnailProperties(state.Thumb, ref props);
    }

    private static void DestroyHost(IntPtr host)
    {
        if (_hosts.TryGetValue(host, out var st))
        {
            if (st.Thumb != IntPtr.Zero) DwmUnregisterThumbnail(st.Thumb);
            _hosts.Remove(host);
        }
        if (host != IntPtr.Zero && IsWindow(host)) DestroyWindow(host);
    }

    private static IntPtr HostWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_SIZE:
                if (_hosts.TryGetValue(hWnd, out var st))
                    ApplyThumbnail(hWnd, st);
                return IntPtr.Zero;

            case WM_DESTROY:
                // User closed the host window (X / Alt-F4): clean up the thumbnail + active list entry.
                if (_hosts.TryGetValue(hWnd, out var s2))
                {
                    if (s2.Thumb != IntPtr.Zero) DwmUnregisterThumbnail(s2.Thumb);
                    _hosts.Remove(hWnd);
                    NotifyClosedFromWndProc(hWnd);
                }
                return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    /// <summary>由 WndProc（泵執行緒）通知 UI 一個視窗畀人手動關咗 · Sync the active list after a manual close.</summary>
    private static void NotifyClosedFromWndProc(IntPtr host)
    {
        // remove from the observable set; marshal to the app's UI thread for collection safety.
        void Remove()
        {
            var entry = Active.FirstOrDefault(e => e.HostHandle == host);
            if (entry is not null) { Active.Remove(entry); Changed?.Invoke(); }
        }
        var dq = App.Shell?.DispatcherQueue;
        if (dq is not null) dq.TryEnqueue(Remove);
        else Remove();
    }

    // ===================== geometry =====================

    /// <summary>等比例縮放，將來源矩形置中入 host 客戶區（同 PowerToys 一樣）· Letterbox the source into the host.</summary>
    private static RECT ComputeDestRect(RECT windowRect, RECT contentRect)
    {
        float ww = Math.Max(1, windowRect.Width), wh = Math.Max(1, windowRect.Height);
        float cw = Math.Max(1, contentRect.Width), ch = Math.Max(1, contentRect.Height);
        float scale = ww / cw;
        if (ww / wh > cw / ch) scale = wh / ch;
        float dw = cw * scale, dh = ch * scale;
        // Fractional scaled dims can floor to 0 → a zero-size / inverted rcDestination crashes DWM
        // (DwmUpdateThumbnailProperties). Clamp to at least 1px and build the rect from the clamped ints.
        int dwInt = Math.Max(1, (int)dw), dhInt = Math.Max(1, (int)dh);
        int left = (int)((ww - dwInt) / 2f), top = (int)((wh - dhInt) / 2f);
        return new RECT { Left = left, Top = top, Right = left + dwInt, Bottom = top + dhInt };
    }

    // ===================== global hotkeys =====================

    public const string EnabledKey = "cropandlock.enabled";
    public const string ThumbHotkeyKey = "cropandlock.hotkey.thumbnail";
    public const string CropHotkeyKey = "cropandlock.hotkey.crop";

    /// <summary>一個熱鍵綁定（修飾鍵 + 按鍵）· One stored hotkey chord (modifiers + virtual-key).</summary>
    public sealed class Chord
    {
        public uint Modifiers { get; set; }   // MOD_* flags (Alt=1, Ctrl=2, Shift=4, Win=8)
        public uint VirtualKey { get; set; }
        public string KeyName { get; set; } = "";

        public bool IsSet => VirtualKey != 0;

        public string Text()
        {
            if (!IsSet) return Loc.I.Pick("(none)", "（無）");
            var parts = new List<string>();
            if ((Modifiers & 0x0002) != 0) parts.Add("Ctrl");
            if ((Modifiers & 0x0001) != 0) parts.Add("Alt");
            if ((Modifiers & 0x0004) != 0) parts.Add("Shift");
            if ((Modifiers & 0x0008) != 0) parts.Add("Win");
            parts.Add(string.IsNullOrEmpty(KeyName) ? $"0x{VirtualKey:X2}" : KeyName);
            return string.Join(" + ", parts);
        }
    }

    private const int HOTKEY_THUMB = 0xC10D;
    private const int HOTKEY_CROP = 0xC10E;
    private const uint MOD_NOREPEAT = 0x4000;

    /// <summary>讀取已儲存嘅縮圖熱鍵 · The saved thumbnail-mode hotkey (default Ctrl+Shift+T).</summary>
    public static Chord ThumbHotkey { get; private set; } = LoadChord(ThumbHotkeyKey, defMods: 0x0002 | 0x0004, defVk: 0x54, defName: "T");
    /// <summary>讀取已儲存嘅裁切熱鍵 · The saved crop-mode hotkey (default Ctrl+Shift+C).</summary>
    public static Chord CropHotkey { get; private set; } = LoadChord(CropHotkeyKey, defMods: 0x0002 | 0x0004, defVk: 0x43, defName: "C");

    private static Chord LoadChord(string key, uint defMods, uint defVk, string defName)
    {
        try
        {
            var json = SettingsStore.Get(key, "");
            if (!string.IsNullOrWhiteSpace(json))
            {
                var c = JsonSerializer.Deserialize<Chord>(json);
                if (c is not null) return c;
            }
        }
        catch { }
        return new Chord { Modifiers = defMods, VirtualKey = defVk, KeyName = defName };
    }

    /// <summary>儲存並重新登記熱鍵 · Persist a chord and re-register the hotkeys.</summary>
    public static void SetHotkey(bool thumbnail, Chord chord)
    {
        if (thumbnail) { ThumbHotkey = chord; SettingsStore.Set(ThumbHotkeyKey, JsonSerializer.Serialize(chord)); }
        else { CropHotkey = chord; SettingsStore.Set(CropHotkeyKey, JsonSerializer.Serialize(chord)); }
        ReloadHotkeys();
        Changed?.Invoke();
    }

    /// <summary>由設定讀啟用狀態 · Whether the module is enabled (persisted; default on).</summary>
    public static bool Enabled
    {
        get => SettingsStore.Get(EnabledKey, "true") != "false";
        set { SettingsStore.Set(EnabledKey, value ? "true" : "false"); ReloadHotkeys(); Changed?.Invoke(); }
    }

    /// <summary>叫泵重新登記熱鍵 · Ask the pump thread to re-register the hotkeys.</summary>
    public static void ReloadHotkeys()
    {
        if (!_running || _pumpThreadId == 0) return;
        // Hotkey (un)registration is thread-affine, so it must run on the pump thread.
        PostThreadMessage(_pumpThreadId, WM_APP_RELOAD_HK, IntPtr.Zero, IntPtr.Zero);
    }

    private static void RegisterHotkeysOnThread()
    {
        UnregisterHotkeysOnThread();
        if (!Enabled) return;
        if (ThumbHotkey.IsSet)
            RegisterHotKey(IntPtr.Zero, HOTKEY_THUMB, ThumbHotkey.Modifiers | MOD_NOREPEAT, ThumbHotkey.VirtualKey);
        if (CropHotkey.IsSet)
            RegisterHotKey(IntPtr.Zero, HOTKEY_CROP, CropHotkey.Modifiers | MOD_NOREPEAT, CropHotkey.VirtualKey);
    }

    private static void UnregisterHotkeysOnThread()
    {
        UnregisterHotKey(IntPtr.Zero, HOTKEY_THUMB);
        UnregisterHotKey(IntPtr.Zero, HOTKEY_CROP);
    }

    private static void OnHotkey(int id)
    {
        bool thumbnail = id == HOTKEY_THUMB;
        // Bounce to the UI thread: the pick flow (window pick + region drag) needs a UI message loop.
        var dq = App.Shell?.DispatcherQueue;
        if (dq is not null)
            dq.TryEnqueue(() => HotkeyPickRequested?.Invoke(thumbnail));
        else
            HotkeyPickRequested?.Invoke(thumbnail);
    }

    /// <summary>熱鍵觸發時要求 UI 開始「揀視窗 + 區域」流程 · A hotkey asked the UI to start a pick flow.</summary>
    public static event Action<bool>? HotkeyPickRequested;

    private static void Note(string en, string zh)
    {
        LastEvent = Loc.I.Pick(en, zh);
    }
}
