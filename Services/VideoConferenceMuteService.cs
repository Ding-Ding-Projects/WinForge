using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// Native Video Conference Mute equivalent. It controls the default communications microphone through
/// Core Audio and, only after explicit consent, a reversible per-user camera privacy gate.
/// 原生視像會議靜音對應功能：用 Core Audio 控制預設通訊咪，並只喺明確同意後用可還原嘅每用戶鏡頭私隱閘。
/// </summary>
public static class VideoConferenceMuteService
{
    private const string Prefix = "videoconferencemute.";
    private const string KEnabled = Prefix + "enabled";
    private const string KAllowCamera = Prefix + "allowCameraPrivacy";
    private const string KShowTray = Prefix + "showTray";
    private const string KAllHotkey = Prefix + "hotkey.all";
    private const string KMicrophoneHotkey = Prefix + "hotkey.microphone";
    private const string KCameraHotkey = Prefix + "hotkey.camera";
    private const string CameraConsentPath =
        @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam";

    private const int eCapture = 1;
    private const int eConsole = 0;
    private const int eCommunications = 2;
    private const uint CLSCTX_ALL = 23;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_QUIT = 0x0012;
    private const int HotkeyAllId = 0xC211;
    private const int HotkeyMicrophoneId = 0xC212;
    private const int HotkeyCameraId = 0xC213;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private static readonly object HotkeyGate = new();
    private static DispatcherQueue? _dispatcher;
    private static Action<ConferenceMuteState>? _showOverlay;
    private static Thread? _hotkeyThread;
    private static uint _hotkeyThreadId;
    private static int _hotkeyGeneration;

    public static event Action<ConferenceMuteState>? StateChanged;
    public static string LastEvent { get; private set; } = "";

    public static bool Enabled => SettingsStore.Get(KEnabled, "false") == "true";
    public static bool AllowCameraPrivacyToggle => SettingsStore.Get(KAllowCamera, "false") == "true";
    public static bool ShowTrayMenuItem => SettingsStore.Get(KShowTray, "true") != "false";

    public static ConferenceMuteHotkey AllHotkey { get; private set; } =
        LoadHotkey(KAllHotkey, MOD_WIN | MOD_SHIFT, 0x41, "A");
    public static ConferenceMuteHotkey MicrophoneHotkey { get; private set; } =
        LoadHotkey(KMicrophoneHotkey, MOD_WIN | MOD_SHIFT, 0x4F, "O");
    public static ConferenceMuteHotkey CameraHotkey { get; private set; } =
        LoadHotkey(KCameraHotkey, MOD_WIN | MOD_SHIFT, 0x49, "I");

    public static void Initialize(DispatcherQueue dispatcher, Action<ConferenceMuteState> showOverlay)
    {
        _dispatcher = dispatcher;
        _showOverlay = showOverlay;
        RestartHotkeys();
    }

    public static ConferenceMuteState GetState()
    {
        bool microphoneAvailable = TryGetMicrophoneMuted(out bool microphoneMuted);
        return new ConferenceMuteState
        {
            Enabled = Enabled,
            MicrophoneAvailable = microphoneAvailable,
            MicrophoneMuted = microphoneAvailable && microphoneMuted,
            CameraPrivacyControlEnabled = AllowCameraPrivacyToggle,
            CameraMuted = IsCameraMuted(),
        };
    }

    public static void SetEnabled(bool enabled)
    {
        SettingsStore.Set(KEnabled, enabled ? "true" : "false");
        RestartHotkeys();
        Publish(enabled
            ? Note("Video Conference Mute is enabled.", "視像會議靜音已開啟。")
            : Note("Video Conference Mute is disabled.", "視像會議靜音已關閉。"));
    }

    public static void SetAllowCameraPrivacyToggle(bool enabled)
    {
        SettingsStore.Set(KAllowCamera, enabled ? "true" : "false");
        Publish(Note(enabled
            ? "Camera privacy control is enabled. It can be toggled from this module or the global shortcuts."
            : "Camera privacy control is disabled. Existing camera privacy state was left unchanged.",
            enabled
                ? "鏡頭私隱控制已開啟。可以由呢個模組或者全域快捷鍵切換。"
                : "鏡頭私隱控制已關閉。現有鏡頭私隱狀態保持不變。"));
    }

    public static void SetShowTrayMenuItem(bool enabled)
        => SettingsStore.Set(KShowTray, enabled ? "true" : "false");

    public static ConferenceMuteState ToggleAll()
    {
        var current = GetState();
        bool targetMuted = !(current.MicrophoneMuted &&
                             (!current.CameraPrivacyControlEnabled || current.CameraMuted));
        bool microphoneOk = !current.MicrophoneAvailable || SetMicrophoneMuted(targetMuted);
        bool cameraOk = !current.CameraPrivacyControlEnabled || SetCameraMuted(targetMuted);
        return Publish(Note(
            targetMuted
                ? (cameraOk && microphoneOk ? "Conference controls muted." : "Some conference controls could not be muted.")
                : (cameraOk && microphoneOk ? "Conference controls unmuted." : "Some conference controls could not be unmuted."),
            targetMuted
                ? (cameraOk && microphoneOk ? "視像會議控制已靜音。" : "部分視像會議控制未能靜音。")
                : (cameraOk && microphoneOk ? "視像會議控制已解除靜音。" : "部分視像會議控制未能解除靜音。")));
    }

    public static ConferenceMuteState ToggleMicrophone()
    {
        if (!TryGetMicrophoneMuted(out bool muted))
            return Publish(Note("No default communications microphone is available.", "未有可用嘅預設通訊咪。"));
        bool target = !muted;
        bool ok = SetMicrophoneMuted(target);
        return Publish(Note(
            ok ? (target ? "Microphone muted." : "Microphone unmuted.") : "Could not change the microphone mute state.",
            ok ? (target ? "咪已靜音。" : "咪已解除靜音。") : "未能更改咪嘅靜音狀態。"));
    }

    public static ConferenceMuteState ToggleCamera()
    {
        if (!AllowCameraPrivacyToggle)
            return Publish(Note(
                "Enable the camera privacy gate in the module before changing the camera state.",
                "請先喺模組開啟鏡頭私隱閘先可以更改鏡頭狀態。"));
        bool target = !IsCameraMuted();
        bool ok = SetCameraMuted(target);
        return Publish(Note(
            ok ? (target ? "Camera privacy is blocked." : "Camera privacy is allowed.") : "Could not change camera privacy.",
            ok ? (target ? "鏡頭私隱已封鎖。" : "鏡頭私隱已允許。") : "未能更改鏡頭私隱。"));
    }

    public static ConferenceMuteState ToggleAllFromTray() => ToggleAll();

    public static void SetHotkey(ConferenceMuteAction action, ConferenceMuteHotkey hotkey)
    {
        switch (action)
        {
            case ConferenceMuteAction.All:
                AllHotkey = hotkey;
                SettingsStore.Set(KAllHotkey, JsonSerializer.Serialize(hotkey));
                break;
            case ConferenceMuteAction.Microphone:
                MicrophoneHotkey = hotkey;
                SettingsStore.Set(KMicrophoneHotkey, JsonSerializer.Serialize(hotkey));
                break;
            case ConferenceMuteAction.Camera:
                CameraHotkey = hotkey;
                SettingsStore.Set(KCameraHotkey, JsonSerializer.Serialize(hotkey));
                break;
        }
        RestartHotkeys();
        Publish(Note("Video Conference Mute shortcut updated.", "視像會議靜音快捷鍵已更新。"));
    }

    private static ConferenceMuteState Note(string en, string zh)
    {
        LastEvent = Loc.I.Pick(en, zh);
        return GetState();
    }

    private static ConferenceMuteState Publish(ConferenceMuteState state)
    {
        var changed = StateChanged;
        var overlay = _showOverlay;
        void Notify()
        {
            changed?.Invoke(state);
            overlay?.Invoke(state);
        }
        if (_dispatcher is not null) _dispatcher.TryEnqueue(() => Notify());
        else Notify();
        return state;
    }

    private static bool IsCameraMuted()
    {
        try
        {
            var value = RegistryHelper.GetValue(RegRoot.HKCU, CameraConsentPath, "Value")?.ToString();
            return string.Equals(value, "Deny", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool SetCameraMuted(bool muted)
    {
        if (muted && !AllowCameraPrivacyToggle) return false;
        try
        {
            RegistryHelper.SetValue(
                RegRoot.HKCU,
                CameraConsentPath,
                "Value",
                muted ? "Deny" : "Allow",
                RegistryValueKind.String);
            return true;
        }
        catch { return false; }
    }

    private static bool TryGetMicrophoneMuted(out bool muted)
    {
        muted = false;
        object? enumeratorObject = null;
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        object? endpointObject = null;
        IAudioEndpointVolume? endpoint = null;
        try
        {
            enumeratorObject = new MMDeviceEnumeratorComObject();
            enumerator = (IMMDeviceEnumerator)enumeratorObject;
            int hr = enumerator.GetDefaultAudioEndpoint(eCapture, eCommunications, out device);
            if (hr < 0) hr = enumerator.GetDefaultAudioEndpoint(eCapture, eConsole, out device);
            if (hr < 0 || device is null) return false;

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            ThrowIfFailed(device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out endpointObject));
            endpoint = (IAudioEndpointVolume)endpointObject;
            ThrowIfFailed(endpoint.GetMute(out int value));
            muted = value != 0;
            return true;
        }
        catch { return false; }
        finally
        {
            Release(endpoint);
            Release(endpointObject);
            Release(device);
            Release(enumerator);
            Release(enumeratorObject);
        }
    }

    private static bool SetMicrophoneMuted(bool muted)
    {
        object? enumeratorObject = null;
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        object? endpointObject = null;
        IAudioEndpointVolume? endpoint = null;
        try
        {
            enumeratorObject = new MMDeviceEnumeratorComObject();
            enumerator = (IMMDeviceEnumerator)enumeratorObject;
            int hr = enumerator.GetDefaultAudioEndpoint(eCapture, eCommunications, out device);
            if (hr < 0) hr = enumerator.GetDefaultAudioEndpoint(eCapture, eConsole, out device);
            if (hr < 0 || device is null) return false;

            Guid iid = typeof(IAudioEndpointVolume).GUID;
            ThrowIfFailed(device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out endpointObject));
            endpoint = (IAudioEndpointVolume)endpointObject;
            Guid context = Guid.Empty;
            ThrowIfFailed(endpoint.SetMute(muted ? 1 : 0, ref context));
            return true;
        }
        catch { return false; }
        finally
        {
            Release(endpoint);
            Release(endpointObject);
            Release(device);
            Release(enumerator);
            Release(enumeratorObject);
        }
    }

    private static void ThrowIfFailed(int hr)
    {
        if (hr < 0) Marshal.ThrowExceptionForHR(hr);
    }

    private static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.ReleaseComObject(value); }
            catch { }
        }
    }

    private static ConferenceMuteHotkey LoadHotkey(string key, uint modifiers, uint virtualKey, string keyName)
    {
        try
        {
            var stored = SettingsStore.Get(key, "");
            if (!string.IsNullOrWhiteSpace(stored))
            {
                var parsed = JsonSerializer.Deserialize<ConferenceMuteHotkey>(stored);
                if (parsed is not null) return parsed;
            }
        }
        catch { }
        return new ConferenceMuteHotkey { Modifiers = modifiers, VirtualKey = virtualKey, KeyName = keyName };
    }

    private static void RestartHotkeys()
    {
        StopHotkeys();
        if (!Enabled) return;
        int generation;
        lock (HotkeyGate)
        {
            generation = ++_hotkeyGeneration;
            _hotkeyThread = new Thread(() => HotkeyLoop(generation))
            {
                IsBackground = true,
                Name = "WinForge.VideoConferenceMute.Hotkeys",
            };
            _hotkeyThread.Start();
        }
    }

    private static void StopHotkeys()
    {
        Thread? thread;
        uint threadId;
        lock (HotkeyGate)
        {
            _hotkeyGeneration++;
            thread = _hotkeyThread;
            threadId = _hotkeyThreadId;
            _hotkeyThread = null;
            _hotkeyThreadId = 0;
        }
        if (threadId != 0) PostThreadMessage(threadId, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        if (thread is not null && thread != Thread.CurrentThread) thread.Join(300);
    }

    private static void HotkeyLoop(int generation)
    {
        uint threadId = GetCurrentThreadId();
        lock (HotkeyGate)
        {
            if (generation != _hotkeyGeneration) return;
            _hotkeyThreadId = threadId;
        }

        if (AllHotkey.IsSet)
            RegisterHotKey(IntPtr.Zero, HotkeyAllId, AllHotkey.Modifiers | MOD_NOREPEAT, AllHotkey.VirtualKey);
        if (MicrophoneHotkey.IsSet)
            RegisterHotKey(IntPtr.Zero, HotkeyMicrophoneId, MicrophoneHotkey.Modifiers | MOD_NOREPEAT, MicrophoneHotkey.VirtualKey);
        if (CameraHotkey.IsSet)
            RegisterHotKey(IntPtr.Zero, HotkeyCameraId, CameraHotkey.Modifiers | MOD_NOREPEAT, CameraHotkey.VirtualKey);

        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.message != WM_HOTKEY) continue;
            switch ((int)message.wParam.ToUInt64())
            {
                case HotkeyAllId: ToggleAll(); break;
                case HotkeyMicrophoneId: ToggleMicrophone(); break;
                case HotkeyCameraId: ToggleCamera(); break;
            }
        }
        UnregisterHotKey(IntPtr.Zero, HotkeyAllId);
        UnregisterHotKey(IntPtr.Zero, HotkeyMicrophoneId);
        UnregisterHotKey(IntPtr.Zero, HotkeyCameraId);
        lock (HotkeyGate)
        {
            if (generation == _hotkeyGeneration) _hotkeyThreadId = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public UIntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG message, IntPtr hWnd, uint minimum, uint maximum);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint threadId, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}

public enum ConferenceMuteAction
{
    All,
    Microphone,
    Camera,
}

public sealed class ConferenceMuteState
{
    public bool Enabled { get; init; }
    public bool MicrophoneAvailable { get; init; }
    public bool MicrophoneMuted { get; init; }
    public bool CameraPrivacyControlEnabled { get; init; }
    public bool CameraMuted { get; init; }
}

public sealed class ConferenceMuteHotkey
{
    public uint Modifiers { get; set; }
    public uint VirtualKey { get; set; }
    public string KeyName { get; set; } = "";
    public bool IsSet => Modifiers != 0 && VirtualKey != 0;

    public string Text()
    {
        if (!IsSet) return Loc.I.Pick("(none)", "（無）");
        var parts = new List<string>();
        if ((Modifiers & 0x0002) != 0) parts.Add("Ctrl");
        if ((Modifiers & 0x0001) != 0) parts.Add("Alt");
        if ((Modifiers & 0x0004) != 0) parts.Add("Shift");
        if ((Modifiers & 0x0008) != 0) parts.Add("Win");
        parts.Add(string.IsNullOrWhiteSpace(KeyName) ? $"0x{VirtualKey:X2}" : KeyName);
        return string.Join(" + ", parts);
    }
}
