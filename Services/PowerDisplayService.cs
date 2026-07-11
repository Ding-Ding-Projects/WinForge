using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using Microsoft.UI.Dispatching;

namespace WinForge.Services;

/// <summary>
/// Managed DDC/CI monitor control for Power Display. It never probes or writes a monitor before
/// the user enables it, and every physical-monitor handle is released in the same operation.
/// 受管 Power Display DDC/CI 螢幕控制。用家未開啟前唔會偵測或者寫入，所有實體螢幕控制代碼會即時釋放。
/// </summary>
public static class PowerDisplayService
{
    private const string Prefix = "powerdisplay.";
    private const string KEnabled = Prefix + "enabled";
    private const string KShortcut = Prefix + "shortcut";
    private const string KRefreshDelay = Prefix + "refreshDelay";
    private const string KRestore = Prefix + "restore";
    private const string KTray = Prefix + "tray";
    private const string KCompatibility = Prefix + "compatibility";
    private const string KProfiles = Prefix + "profiles";
    private const string KStartupProfile = Prefix + "startupProfile";
    private const string KLightProfile = Prefix + "lightProfile";
    private const string KDarkProfile = Prefix + "darkProfile";
    private const string KQuickMonitors = Prefix + "quickMonitors";
    private const string KCustomVcp = Prefix + "customVcp";

    private const byte Brightness = 0x10;
    private const byte Contrast = 0x12;
    private const byte ColorTemperature = 0x14;
    private const byte InputSource = 0x60;
    private const byte Volume = 0x62;
    private const byte Rotation = 0xAA;
    private const byte PowerState = 0xD6;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_QUIT = 0x0012;
    private const int HotkeyId = 0x5044;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    private static readonly object HotkeyGate = new();
    private static DispatcherQueue? _dispatcher;
    private static Action? _showCompactPanel;
    private static Thread? _hotkeyThread;
    private static uint _hotkeyThreadId;

    public static bool Enabled => GetBool(KEnabled, false);
    public static string ActivationShortcut => SettingsStore.Get(KShortcut, "Ctrl+Alt+D");
    public static int RefreshDelaySeconds => Clamp(GetInt(KRefreshDelay, 2), 1, 20);
    public static bool RestoreAtStartup => GetBool(KRestore, true);
    public static bool ShowTrayMenuItem => GetBool(KTray, true);
    public static bool MaximumCompatibilityMode => GetBool(KCompatibility, false);
    public static string StartupProfileId => SettingsStore.Get(KStartupProfile, "");
    public static string LightProfileId => SettingsStore.Get(KLightProfile, "");
    public static string DarkProfileId => SettingsStore.Get(KDarkProfile, "");
    public static string CustomVcpText => SettingsStore.Get(KCustomVcp, "");
    public static IReadOnlyList<PowerDisplayProfile> Profiles => LoadProfiles();

    public static void Initialize(DispatcherQueue dispatcher, Action showCompactPanel)
    {
        _dispatcher = dispatcher;
        _showCompactPanel = showCompactPanel;
        RestartHotkey();
        if (Enabled && RestoreAtStartup && !string.IsNullOrWhiteSpace(StartupProfileId))
            ThreadPool.QueueUserWorkItem(_ => ApplyProfile(StartupProfileId));
    }

    public static void SetEnabled(bool enabled)
    {
        SettingsStore.Set(KEnabled, enabled ? "true" : "false");
        RestartHotkey();
    }

    public static bool SetActivationShortcut(string? shortcut)
    {
        var normalized = string.IsNullOrWhiteSpace(shortcut) ? "Ctrl+Alt+D" : shortcut.Trim();
        if (!TryParseShortcut(normalized, out _, out _)) return false;
        SettingsStore.Set(KShortcut, normalized);
        RestartHotkey();
        return true;
    }

    public static void SetRefreshDelay(int seconds)
        => SettingsStore.Set(KRefreshDelay, Clamp(seconds, 1, 20).ToString(CultureInfo.InvariantCulture));

    public static void SetRestoreAtStartup(bool enabled)
        => SettingsStore.Set(KRestore, enabled ? "true" : "false");

    public static void SetShowTrayMenuItem(bool enabled)
        => SettingsStore.Set(KTray, enabled ? "true" : "false");

    public static void SetMaximumCompatibilityMode(bool enabled)
        => SettingsStore.Set(KCompatibility, enabled ? "true" : "false");

    public static void SetCustomVcpText(string? text)
        => SettingsStore.Set(KCustomVcp, text?.Trim() ?? "");

    public static void SetProfileAssignments(string? startup, string? light, string? dark)
    {
        SettingsStore.Set(KStartupProfile, startup ?? "");
        SettingsStore.Set(KLightProfile, light ?? "");
        SettingsStore.Set(KDarkProfile, dark ?? "");
    }

    public static void ShowCompactPanel()
    {
        var show = _showCompactPanel;
        if (show is null) return;
        if (_dispatcher is not null) _dispatcher.TryEnqueue(() => show());
        else show();
    }

    /// <summary>Called after Light Switch changes the app/system theme. It remains entirely opt-in.</summary>
    public static int ApplyThemeProfile(bool light)
    {
        if (!Enabled) return 0;
        var id = light ? LightProfileId : DarkProfileId;
        return string.IsNullOrWhiteSpace(id) ? 0 : ApplyProfile(id);
    }

    public static List<PowerDisplayMonitor> Discover()
    {
        var result = new List<PowerDisplayMonitor>();
        if (!Enabled) return result;
        var custom = ParseCustomMappings(CustomVcpText);
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var info = new MONITORINFOEX
                {
                    cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>(),
                    szDevice = string.Empty,
                };
                if (!GetMonitorInfo(hMonitor, ref info)) return true;
                VisitPhysicalMonitors(hMonitor, (handle, index, description) =>
                {
                    var values = new Dictionary<byte, PowerDisplayValue>();
                    AddBrightness(handle, values);
                    AddVcp(handle, Contrast, values);
                    AddVcp(handle, PowerState, values);
                    if (!MaximumCompatibilityMode)
                    {
                        AddVcp(handle, Volume, values);
                        AddVcp(handle, InputSource, values);
                        AddVcp(handle, Rotation, values);
                        AddVcp(handle, ColorTemperature, values);
                        foreach (var mapping in custom) AddVcp(handle, mapping.Code, values);
                    }
                    if (values.Count == 0) return;
                    result.Add(new PowerDisplayMonitor
                    {
                        Id = $"{info.szDevice}:{index}",
                        DeviceName = info.szDevice,
                        Description = string.IsNullOrWhiteSpace(description) ? "Display" : description,
                        Left = info.rcMonitor.left,
                        Top = info.rcMonitor.top,
                        Width = Math.Max(1, info.rcMonitor.right - info.rcMonitor.left),
                        Height = Math.Max(1, info.rcMonitor.bottom - info.rcMonitor.top),
                        Values = values,
                    });
                });
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // A failing monitor or driver is omitted rather than affecting the rest of the desktop.
        }
        return result;
    }

    public static bool SetValue(string monitorId, byte code, uint value)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(monitorId)) return false;
        bool applied = false;
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
            {
                var info = new MONITORINFOEX
                {
                    cbSize = (uint)Marshal.SizeOf<MONITORINFOEX>(),
                    szDevice = string.Empty,
                };
                if (!GetMonitorInfo(hMonitor, ref info)) return true;
                VisitPhysicalMonitors(hMonitor, (handle, index, description) =>
                {
                    if (!string.Equals($"{info.szDevice}:{index}", monitorId, StringComparison.Ordinal)) return;
                    if (code == Brightness && GetMonitorBrightness(handle, out var min, out _, out var max))
                        applied |= SetMonitorBrightness(handle, Clamp(value, min, max));
                    else
                        applied |= SetVCPFeature(handle, code, value);
                });
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            return false;
        }
        return applied;
    }

    public static PowerDisplayProfile? CaptureProfile(string? name)
    {
        if (!Enabled) return null;
        var profile = new PowerDisplayProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(name) ? $"Profile {DateTime.Now:yyyy-MM-dd HH:mm}" : name.Trim(),
            Monitors = Discover().Select(m => new PowerDisplayProfileMonitor
            {
                MonitorId = m.Id,
                Values = m.Values.ToDictionary(v => v.Key.ToString("X2", CultureInfo.InvariantCulture), v => v.Value.Current),
            }).ToList(),
        };
        var all = LoadProfiles();
        all.Add(profile);
        SaveProfiles(all);
        return profile;
    }

    public static int ApplyProfile(string? profileId)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(profileId)) return 0;
        var profile = LoadProfiles().FirstOrDefault(p => p.Id == profileId);
        if (profile is null) return 0;
        int applied = 0;
        foreach (var monitor in profile.Monitors)
        {
            foreach (var entry in monitor.Values)
            {
                if (byte.TryParse(entry.Key, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) &&
                    SetValue(monitor.MonitorId, code, entry.Value))
                    applied++;
            }
        }
        return applied;
    }

    public static bool DeleteProfile(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return false;
        var all = LoadProfiles();
        if (all.RemoveAll(p => p.Id == profileId) == 0) return false;
        SaveProfiles(all);
        SetProfileAssignments(
            StartupProfileId == profileId ? "" : StartupProfileId,
            LightProfileId == profileId ? "" : LightProfileId,
            DarkProfileId == profileId ? "" : DarkProfileId);
        return true;
    }

    public static bool ShowInCompactPanel(string monitorId)
        => GetQuickMonitorIds().Contains(monitorId, StringComparer.Ordinal);

    public static void SetShowInCompactPanel(string monitorId, bool show)
    {
        var ids = GetQuickMonitorIds();
        if (show && !ids.Contains(monitorId, StringComparer.Ordinal)) ids.Add(monitorId);
        if (!show) ids.RemoveAll(id => id == monitorId);
        SettingsStore.Set(KQuickMonitors, string.Join("|", ids));
    }

    public static string DisplayNameFor(byte code)
    {
        return code switch
        {
            Brightness => Loc.I.Pick("Brightness", "亮度"),
            Contrast => Loc.I.Pick("Contrast", "對比"),
            Volume => Loc.I.Pick("Monitor volume", "螢幕音量"),
            InputSource => Loc.I.Pick("Input source", "輸入來源"),
            Rotation => Loc.I.Pick("Display rotation", "螢幕旋轉"),
            ColorTemperature => Loc.I.Pick("Colour temperature", "色溫"),
            PowerState => Loc.I.Pick("Monitor power", "螢幕電源"),
            _ => ParseCustomMappings(CustomVcpText).FirstOrDefault(m => m.Code == code)?.Name ?? $"VCP 0x{code:X2}",
        };
    }

    private static void RestartHotkey()
    {
        StopHotkey();
        if (!Enabled || !TryParseShortcut(ActivationShortcut, out _, out _)) return;
        var thread = new Thread(HotkeyLoop) { IsBackground = true, Name = "WinForge.PowerDisplay.Hotkey" };
        lock (HotkeyGate) _hotkeyThread = thread;
        thread.Start();
    }

    private static void StopHotkey()
    {
        Thread? thread;
        uint id;
        lock (HotkeyGate)
        {
            thread = _hotkeyThread;
            id = _hotkeyThreadId;
            _hotkeyThread = null;
            _hotkeyThreadId = 0;
        }
        if (id != 0) PostThreadMessage(id, WM_QUIT, UIntPtr.Zero, IntPtr.Zero);
        if (thread is not null && thread != Thread.CurrentThread) thread.Join(300);
    }

    private static void HotkeyLoop()
    {
        if (!TryParseShortcut(ActivationShortcut, out var modifiers, out var key)) return;
        var id = GetCurrentThreadId();
        lock (HotkeyGate) _hotkeyThreadId = id;
        if (!RegisterHotKey(IntPtr.Zero, HotkeyId, modifiers | MOD_NOREPEAT, key)) return;
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.message == WM_HOTKEY) ShowCompactPanel();
        }
        UnregisterHotKey(IntPtr.Zero, HotkeyId);
    }

    private static bool TryParseShortcut(string shortcut, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        foreach (var part in shortcut.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals("ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("control", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_CONTROL;
            else if (part.Equals("alt", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_ALT;
            else if (part.Equals("shift", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_SHIFT;
            else if (part.Equals("win", StringComparison.OrdinalIgnoreCase) || part.Equals("windows", StringComparison.OrdinalIgnoreCase))
                modifiers |= MOD_WIN;
            else if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                key = char.ToUpperInvariant(part[0]);
            else if (part.StartsWith("F", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(part[1..], out var function) && function is >= 1 and <= 24)
                key = (uint)(0x70 + function - 1);
            else return false;
        }
        return modifiers != 0 && key != 0;
    }

    private static void AddBrightness(IntPtr handle, Dictionary<byte, PowerDisplayValue> values)
    {
        if (GetMonitorBrightness(handle, out var min, out var current, out var max))
            values[Brightness] = new PowerDisplayValue { Minimum = min, Current = current, Maximum = max };
        else AddVcp(handle, Brightness, values);
    }

    private static void AddVcp(IntPtr handle, byte code, Dictionary<byte, PowerDisplayValue> values)
    {
        if (GetVCPFeatureAndVCPFeatureReply(handle, code, IntPtr.Zero, out var current, out var max))
            values[code] = new PowerDisplayValue { Current = current, Maximum = max, Minimum = 0 };
    }

    private static void VisitPhysicalMonitors(IntPtr hMonitor, Action<IntPtr, int, string?> visit)
    {
        if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count == 0) return;
        var monitors = new PHYSICAL_MONITOR[count];
        if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, monitors)) return;
        try
        {
            for (int i = 0; i < monitors.Length; i++)
                visit(monitors[i].hPhysicalMonitor, i, monitors[i].szPhysicalMonitorDescription);
        }
        finally
        {
            foreach (var monitor in monitors)
                if (monitor.hPhysicalMonitor != IntPtr.Zero) DestroyPhysicalMonitor(monitor.hPhysicalMonitor);
        }
    }

    private static List<PowerDisplayProfile> LoadProfiles()
    {
        try
        {
            return JsonSerializer.Deserialize<List<PowerDisplayProfile>>(SettingsStore.Get(KProfiles, "[]"))
                   ?? new List<PowerDisplayProfile>();
        }
        catch { return new List<PowerDisplayProfile>(); }
    }

    private static void SaveProfiles(List<PowerDisplayProfile> profiles)
        => SettingsStore.Set(KProfiles, JsonSerializer.Serialize(profiles));

    private static List<string> GetQuickMonitorIds()
        => SettingsStore.Get(KQuickMonitors, "").Split('|', StringSplitOptions.RemoveEmptyEntries).ToList();

    private static List<PowerDisplayCustomMapping> ParseCustomMappings(string text)
    {
        var result = new List<PowerDisplayCustomMapping>();
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split(';', StringSplitOptions.TrimEntries);
            if (parts.Length < 2) continue;
            var codeText = parts[0].StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? parts[0][2..] : parts[0];
            if (byte.TryParse(codeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                result.Add(new PowerDisplayCustomMapping { Code = code, Name = parts[1] });
        }
        return result;
    }

    private static bool GetBool(string key, bool fallback)
        => bool.TryParse(SettingsStore.Get(key, fallback ? "true" : "false"), out var value) ? value : fallback;

    private static int GetInt(string key, int fallback)
        => int.TryParse(SettingsStore.Get(key, fallback.ToString(CultureInfo.InvariantCulture)), out var value) ? value : fallback;

    private static int Clamp(int value, int minimum, int maximum)
        => value < minimum ? minimum : value > maximum ? maximum : value;

    private static uint Clamp(uint value, uint minimum, uint maximum)
        => value < minimum ? minimum : value > maximum ? maximum : value;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MONITORINFOEX
    {
        public uint cbSize;
        public RECT rcMonitor, rcWork;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string? szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

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

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX info);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG message, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostThreadMessage(uint id, uint message, UIntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint min, out uint current, out uint max);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint value);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr hMonitor, byte code, IntPtr type, out uint current, out uint max);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetVCPFeature(IntPtr hMonitor, byte code, uint value);
}

public sealed class PowerDisplayValue
{
    public uint Minimum { get; init; }
    public uint Current { get; init; }
    public uint Maximum { get; init; }
}

public sealed class PowerDisplayMonitor
{
    public string Id { get; init; } = "";
    public string DeviceName { get; init; } = "";
    public string Description { get; init; } = "";
    public int Left { get; init; }
    public int Top { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public Dictionary<byte, PowerDisplayValue> Values { get; init; } = new();
}

public sealed class PowerDisplayProfile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<PowerDisplayProfileMonitor> Monitors { get; set; } = new();
}

public sealed class PowerDisplayProfileMonitor
{
    public string MonitorId { get; set; } = "";
    public Dictionary<string, uint> Values { get; set; } = new();
}

public sealed class PowerDisplayCustomMapping
{
    public byte Code { get; init; }
    public string Name { get; init; } = "";
}
