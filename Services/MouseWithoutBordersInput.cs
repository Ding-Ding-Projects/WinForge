using System;
using System.Runtime.InteropServices;
using WinForge.Models;

namespace WinForge.Services;

// =====================================================================================
// 無界滑鼠 輸入捕捉 + 注入 · Mouse Without Borders input capture (low-level hooks) and
// injection (SendInput). This is the core of the feature.
//
// 捕捉 · Capture: while control is "remote", WH_MOUSE_LL + WH_KEYBOARD_LL hooks swallow local
// input and raise events the service forwards over the channel. We also watch the cursor for
// crossing a screen edge toward a neighbour (the edge transition).
//
// 注入 · Inject: incoming Mouse/Keyboard packets are replayed with SendInput on the receiver.
// =====================================================================================

/// <summary>低層滑鼠／鍵盤捕捉 · Low-level mouse + keyboard capture via global hooks.</summary>
public sealed class MwbInputCapture : IDisposable
{
    // ---- hooks ----
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;

    // mouse messages
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204, WM_RBUTTONUP = 0x0205;
    private const int WM_MBUTTONDOWN = 0x0207, WM_MBUTTONUP = 0x0208;
    private const int WM_MOUSEWHEEL = 0x020A, WM_MOUSEHWHEEL = 0x020E;
    private const int WM_XBUTTONDOWN = 0x020B, WM_XBUTTONUP = 0x020C;

    // keyboard messages
    private const int WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_UP = 0x80, LLKHF_EXTENDED = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT { public int x; public int y; public uint mouseData; public uint flags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    private IntPtr _mouseHook = IntPtr.Zero, _keyboardHook = IntPtr.Zero;
    private HookProc? _mouseProc, _keyboardProc;

    /// <summary>當控制係遠端時，吞掉本機輸入 · While true, local input is swallowed and forwarded.</summary>
    public bool Capturing { get; set; }

    /// <summary>本機滑鼠移動（dx, dy 相對量；正規化絕對位 nx, ny）· Local mouse move.</summary>
    public event Action<int, int, int, int>? MouseMoved; // dx, dy, normX, normY

    /// <summary>本機滑鼠事件（按鈕／滾輪）· Local mouse button / wheel event.</summary>
    public event Action<MwbMouseData>? MouseEvent;

    /// <summary>本機鍵盤事件 · Local keyboard event.</summary>
    public event Action<MwbKeyboardData>? KeyEvent;

    /// <summary>游標貼到螢幕邊（-1 左 / +1 右）· Cursor hit a screen edge toward a neighbour.</summary>
    public event Action<int>? EdgeHit; // -1 = left edge, +1 = right edge

    private int _lastX, _lastY;
    private bool _haveLast;

    /// <summary>裝鈎 · Install the hooks (idempotent).</summary>
    public void Install()
    {
        if (_mouseHook != IntPtr.Zero) return;
        _mouseProc = MouseHookProc;
        _keyboardProc = KeyboardHookProc;
        var hMod = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hMod, 0);
    }

    /// <summary>拆鈎 · Remove the hooks.</summary>
    public void Uninstall()
    {
        if (_mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(_mouseHook); _mouseHook = IntPtr.Zero; }
        if (_keyboardHook != IntPtr.Zero) { UnhookWindowsHookEx(_keyboardHook); _keyboardHook = IntPtr.Zero; }
        _mouseProc = null; _keyboardProc = null;
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;

            // 永遠監察邊界（即使未捕捉）· Always watch for an edge hit, even when not yet capturing.
            if (msg == WM_MOUSEMOVE && !Capturing)
            {
                CheckEdge(data.x, data.y);
            }

            if (Capturing)
            {
                ForwardMouse(msg, data);
                return (IntPtr)1; // swallow locally
            }
        }
        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void ForwardMouse(int msg, MSLLHOOKSTRUCT data)
    {
        switch (msg)
        {
            case WM_MOUSEMOVE:
                int dx = _haveLast ? data.x - _lastX : 0;
                int dy = _haveLast ? data.y - _lastY : 0;
                _lastX = data.x; _lastY = data.y; _haveLast = true;
                MwbScreen.ToNormalized(data.x, data.y, out int nx, out int ny);
                MouseMoved?.Invoke(dx, dy, nx, ny);
                break;
            case WM_LBUTTONDOWN: Mouse(MwbMouseFlags.LeftDown); break;
            case WM_LBUTTONUP: Mouse(MwbMouseFlags.LeftUp); break;
            case WM_RBUTTONDOWN: Mouse(MwbMouseFlags.RightDown); break;
            case WM_RBUTTONUP: Mouse(MwbMouseFlags.RightUp); break;
            case WM_MBUTTONDOWN: Mouse(MwbMouseFlags.MiddleDown); break;
            case WM_MBUTTONUP: Mouse(MwbMouseFlags.MiddleUp); break;
            case WM_MOUSEWHEEL:
                MouseEvent?.Invoke(new MwbMouseData { Flags = (int)MwbMouseFlags.Wheel, WheelDelta = (short)(data.mouseData >> 16) });
                break;
            case WM_MOUSEHWHEEL:
                MouseEvent?.Invoke(new MwbMouseData { Flags = (int)MwbMouseFlags.HWheel, WheelDelta = (short)(data.mouseData >> 16) });
                break;
            case WM_XBUTTONDOWN:
                Mouse((data.mouseData >> 16) == 1 ? MwbMouseFlags.XButton1Down : MwbMouseFlags.XButton2Down);
                break;
            case WM_XBUTTONUP:
                Mouse((data.mouseData >> 16) == 1 ? MwbMouseFlags.XButton1Up : MwbMouseFlags.XButton2Up);
                break;
        }
    }

    private void Mouse(MwbMouseFlags f) => MouseEvent?.Invoke(new MwbMouseData { Flags = (int)f });

    private bool _edgeArmed = true;

    private void CheckEdge(int x, int y)
    {
        var (left, _, right, _) = MwbScreen.VirtualBounds();
        if (x <= left)
        {
            if (_edgeArmed) { _edgeArmed = false; EdgeHit?.Invoke(-1); }
        }
        else if (x >= right - 1)
        {
            if (_edgeArmed) { _edgeArmed = false; EdgeHit?.Invoke(+1); }
        }
        else
        {
            _edgeArmed = true; // re-arm once the cursor leaves the edge
        }
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && Capturing)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int flags = 0;
            int msg = (int)wParam;
            if (msg is WM_KEYUP or WM_SYSKEYUP) flags |= (int)LLKHF_UP;
            if ((data.flags & LLKHF_EXTENDED) != 0) flags |= (int)LLKHF_EXTENDED;
            KeyEvent?.Invoke(new MwbKeyboardData { VirtualKey = (int)data.vkCode, ScanCode = (int)data.scanCode, Flags = flags });
            return (IntPtr)1; // swallow locally
        }
        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}

/// <summary>傳輸用滑鼠旗標 · Transport-level mouse flags (independent of OS constants).</summary>
[Flags]
public enum MwbMouseFlags
{
    LeftDown = 1 << 0,
    LeftUp = 1 << 1,
    RightDown = 1 << 2,
    RightUp = 1 << 3,
    MiddleDown = 1 << 4,
    MiddleUp = 1 << 5,
    Wheel = 1 << 6,
    HWheel = 1 << 7,
    XButton1Down = 1 << 8,
    XButton1Up = 1 << 9,
    XButton2Down = 1 << 10,
    XButton2Up = 1 << 11,
    Move = 1 << 12,
}

/// <summary>SendInput 注入 · Replay forwarded mouse / keyboard events on this machine.</summary>
public static class MwbInputInjector
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const int INPUT_MOUSE = 0, INPUT_KEYBOARD = 1;

    // MOUSEEVENTF
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020, MOUSEEVENTF_MIDDLEUP = 0x0040;
    private const uint MOUSEEVENTF_XDOWN = 0x0080, MOUSEEVENTF_XUP = 0x0100;
    private const uint MOUSEEVENTF_WHEEL = 0x0800, MOUSEEVENTF_HWHEEL = 0x1000;
    private const uint XBUTTON1 = 0x0001, XBUTTON2 = 0x0002;

    // KEYEVENTF
    private const uint KEYEVENTF_KEYUP = 0x0002, KEYEVENTF_EXTENDEDKEY = 0x0001, KEYEVENTF_SCANCODE = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public int type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    /// <summary>把游標移到正規化座標 · Move the cursor to a normalized absolute position.</summary>
    public static void MoveAbsolute(int normX, int normY)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    dx = normX,
                    dy = normY,
                    dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK,
                },
            },
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>注入一個滑鼠事件 · Inject one forwarded mouse event (button / wheel).</summary>
    public static void InjectMouse(MwbMouseData d)
    {
        var f = (MwbMouseFlags)d.Flags;
        uint flags = 0;
        uint data = 0;
        if (f.HasFlag(MwbMouseFlags.LeftDown)) flags |= MOUSEEVENTF_LEFTDOWN;
        if (f.HasFlag(MwbMouseFlags.LeftUp)) flags |= MOUSEEVENTF_LEFTUP;
        if (f.HasFlag(MwbMouseFlags.RightDown)) flags |= MOUSEEVENTF_RIGHTDOWN;
        if (f.HasFlag(MwbMouseFlags.RightUp)) flags |= MOUSEEVENTF_RIGHTUP;
        if (f.HasFlag(MwbMouseFlags.MiddleDown)) flags |= MOUSEEVENTF_MIDDLEDOWN;
        if (f.HasFlag(MwbMouseFlags.MiddleUp)) flags |= MOUSEEVENTF_MIDDLEUP;
        if (f.HasFlag(MwbMouseFlags.XButton1Down)) { flags |= MOUSEEVENTF_XDOWN; data = XBUTTON1; }
        if (f.HasFlag(MwbMouseFlags.XButton1Up)) { flags |= MOUSEEVENTF_XUP; data = XBUTTON1; }
        if (f.HasFlag(MwbMouseFlags.XButton2Down)) { flags |= MOUSEEVENTF_XDOWN; data = XBUTTON2; }
        if (f.HasFlag(MwbMouseFlags.XButton2Up)) { flags |= MOUSEEVENTF_XUP; data = XBUTTON2; }
        if (f.HasFlag(MwbMouseFlags.Wheel)) { flags |= MOUSEEVENTF_WHEEL; data = (uint)d.WheelDelta; }
        if (f.HasFlag(MwbMouseFlags.HWheel)) { flags |= MOUSEEVENTF_HWHEEL; data = (uint)d.WheelDelta; }
        if (flags == 0) return;

        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new InputUnion { mi = new MOUSEINPUT { mouseData = data, dwFlags = flags } },
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    /// <summary>注入一個鍵盤事件 · Inject one forwarded keyboard event.</summary>
    public static void InjectKey(MwbKeyboardData d)
    {
        uint flags = KEYEVENTF_SCANCODE;
        if ((d.Flags & 0x80) != 0) flags |= KEYEVENTF_KEYUP;
        if ((d.Flags & 0x01) != 0) flags |= KEYEVENTF_EXTENDEDKEY;

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = (ushort)d.ScanCode,
                    dwFlags = flags,
                },
            },
        };
        // Fallback to VK if no scan code provided (some synthetic keys).
        if (d.ScanCode == 0)
        {
            input.u.ki.wVk = (ushort)d.VirtualKey;
            input.u.ki.dwFlags = (d.Flags & 0x80) != 0 ? KEYEVENTF_KEYUP : 0;
        }
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}

/// <summary>螢幕度量／座標正規化 · Screen metrics + normalized-coordinate helpers.</summary>
public static class MwbScreen
{
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

    /// <summary>虛擬桌面邊界 (left, top, right, bottom) · Virtual desktop bounds in pixels.</summary>
    public static (int left, int top, int right, int bottom) VirtualBounds()
    {
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int w = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (w <= 0) w = 1920;
        if (h <= 0) h = 1080;
        return (x, y, x + w, y + h);
    }

    /// <summary>像素轉 0..65535 正規化 · Pixel → 0..65535 normalized over the virtual desktop.</summary>
    public static void ToNormalized(int px, int py, out int nx, out int ny)
    {
        var (l, t, r, b) = VirtualBounds();
        int w = Math.Max(1, r - l), h = Math.Max(1, b - t);
        nx = (int)((px - l) * 65535.0 / w);
        ny = (int)((py - t) * 65535.0 / h);
    }
}
