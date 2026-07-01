using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;

namespace WinForge.Services;

/// <summary>
/// 純文字貼上（PowerToys Paste as Plain Text 式）· Strips all formatting from the clipboard so the next
/// paste is plain text. Provides a one-shot "strip now" and an optional global hotkey
/// (Ctrl+Shift+V by default) that, when pressed anywhere, strips the clipboard then re-injects Ctrl+V.
/// Pure WinRT clipboard + a low-level keyboard hook. No external tool, no redirect.
/// </summary>
public static class PlainTextPasteService
{
    // ---- keyboard hook ----
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    private static IntPtr _hook = IntPtr.Zero;
    private static LowLevelKeyboardProc? _proc;
    private static DispatcherQueue? _ui;
    private static readonly NativeMessagePump HookPump = new("WinForge-PlainTextPasteHook");
    private static volatile bool _injecting;

    public static bool HotkeyActive => _hook != IntPtr.Zero;

    /// <summary>The hotkey description shown in the UI.</summary>
    public const string HotkeyText = "Ctrl + Shift + V";

    /// <summary>
    /// 將剪貼簿淨返純文字 · Replace the clipboard with a plain-text-only copy of its text.
    /// Returns false if the clipboard has no text.
    /// </summary>
    public static bool StripToPlainText()
    {
        var view = Clipboard.GetContent();
        if (view is null || !view.Contains(StandardDataFormats.Text)) return false;
        string text;
        try
        {
            // Bound the wait so a locked clipboard can't hang the hotkey thread.
            var t = view.GetTextAsync().AsTask();
            if (!t.Wait(TimeSpan.FromMilliseconds(600))) return false;
            text = t.Result;
        }
        catch { return false; }
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
        return true;
    }

    /// <summary>啟用全域熱鍵 · Enable the Ctrl+Shift+V global hotkey. UI queue is needed for clipboard work.</summary>
    public static void EnableHotkey(DispatcherQueue uiQueue)
    {
        _ui = uiQueue;
        HookPump.Post(InstallHotkey);
    }

    private static void InstallHotkey()
    {
        if (_hook != IntPtr.Zero || _ui is null) return;
        _proc = HookProc;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public static void DisableHotkey()
    {
        HookPump.Post(() =>
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            _proc = null;
            _ui = null;
        });
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_injecting)
        {
            int msg = (int)wParam;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                if (data.vkCode == VK_V && ctrl && shift)
                {
                    // Strip on the UI thread, then synthesise a normal Ctrl+V.
                    _ui?.TryEnqueue(() =>
                    {
                        try { StripToPlainText(); } catch { }
                        InjectCtrlV();
                    });
                    return (IntPtr)1; // swallow the original Ctrl+Shift+V
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static void InjectCtrlV()
    {
        _injecting = true;
        try
        {
            // Release Shift so the target sees a clean Ctrl+V.
            keybd_event((byte)VK_SHIFT, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_V, 0, 0, UIntPtr.Zero);
            keybd_event((byte)VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event((byte)VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
        finally { _injecting = false; }
    }
}
