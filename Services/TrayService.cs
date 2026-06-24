using System;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 系統匣圖示（純 P/Invoke，唔使第三方）· System-tray icon via Shell_NotifyIcon + a message-only window
/// whose WndProc runs on the UI thread (it created the window). Lets WinForge keep running when closed.
/// </summary>
public static class TrayService
{
    private const int WM_APP = 0x8000;
    private const int TRAY_CALLBACK = WM_APP + 1;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 0x1, NIF_ICON = 0x2, NIF_TIP = 0x4;
    private const int IDM_OPEN = 1, IDM_QUIT = 2;
    private const uint TPM_RIGHTBUTTON = 0x0002, TPM_RETURNCMD = 0x0100;
    private const uint MF_STRING = 0x0;

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WNDCLASSEX
    {
        public uint cbSize, style;
        public WndProc lpfnWndProc;
        public int cbClsExtra, cbWndExtra;
        public IntPtr hInstance, hIcon, hCursor, hbrBackground;
        public string? lpszMenuName, lpszClassName;
        public IntPtr hIconSm;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID, uFlags, uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }

    [DllImport("user32.dll", SetLastError = true)] private static extern ushort RegisterClassEx(ref WNDCLASSEX c);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint ex, string cls, string name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr p);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? n);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint msg, ref NOTIFYICONDATA d);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr ExtractIcon(IntPtr inst, string exe, int idx);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr m, uint f, int id, string item);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr m);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] private static extern int TrackPopupMenu(IntPtr m, uint f, int x, int y, int r, IntPtr h, IntPtr rect);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);

    private static readonly IntPtr HWND_MESSAGE = new(-3);
    private const string ClassName = "WinForgeTrayWnd";

    private static WndProc? _proc;           // kept alive (or the CLR GCs it -> crash)
    private static IntPtr _hwnd = IntPtr.Zero;
    private static bool _registered;
    private static Action? _onOpen, _onQuit;
    private static bool _installed;

    public static bool IsInstalled => _installed;

    public static void Install(Action onOpen, Action onQuit, string tooltip)
    {
        if (_installed) return;
        _onOpen = onOpen; _onQuit = onQuit;
        var hInst = GetModuleHandle(null);

        if (!_registered)
        {
            _proc = WndProcImpl;
            var wc = new WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
                lpfnWndProc = _proc,
                hInstance = hInst,
                lpszClassName = ClassName,
            };
            RegisterClassEx(ref wc);
            _registered = true;
        }

        _hwnd = CreateWindowEx(0, ClassName, "WinForgeTray", 0, 0, 0, 0, 0, HWND_MESSAGE, IntPtr.Zero, hInst, IntPtr.Zero);
        if (_hwnd == IntPtr.Zero) return;

        IntPtr hIcon = IntPtr.Zero;
        try { var exe = Environment.ProcessPath; if (!string.IsNullOrEmpty(exe)) hIcon = ExtractIcon(hInst, exe, 0); } catch { }

        var nid = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = TRAY_CALLBACK,
            hIcon = hIcon,
            szTip = tooltip.Length > 127 ? tooltip.Substring(0, 127) : tooltip,
        };
        Shell_NotifyIcon(NIM_ADD, ref nid);
        _installed = true;
    }

    public static void Remove()
    {
        if (!_installed) return;
        var nid = new NOTIFYICONDATA { cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hwnd, uID = 1 };
        Shell_NotifyIcon(NIM_DELETE, ref nid);
        if (_hwnd != IntPtr.Zero) { DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
        _installed = false;
    }

    private static IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == TRAY_CALLBACK)
        {
            int mouse = (int)(lParam.ToInt64() & 0xFFFF);
            if (mouse == WM_LBUTTONUP || mouse == WM_LBUTTONDBLCLK) _onOpen?.Invoke();
            else if (mouse == WM_RBUTTONUP) ShowMenu(hWnd);
            return IntPtr.Zero;
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private static void ShowMenu(IntPtr hWnd)
    {
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, IDM_OPEN, "Open WinForge · 開啟 WinForge");
        AppendMenu(menu, MF_STRING, IDM_QUIT, "Quit · 結束");
        GetCursorPos(out var pt);
        SetForegroundWindow(hWnd);
        int cmd = TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.x, pt.y, 0, hWnd, IntPtr.Zero);
        PostMessage(hWnd, 0, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);
        if (cmd == IDM_OPEN) _onOpen?.Invoke();
        else if (cmd == IDM_QUIT) _onQuit?.Invoke();
    }
}
