using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace WinForge.Services;

/// <summary>一個熱鍵動作嘅種類 · The kind of action a hotkey runs.</summary>
public enum MacroActionKind
{
    LaunchApp,      // Process.Start a program / file / URL
    RunPowerShell,  // run a PowerShell snippet
    SendKeys,       // replay keystrokes via SendInput
    OpenModule,     // bring WinForge to front and open a module by tag (Target = "module.xxx")
}

/// <summary>修飾鍵旗標（user32 MOD_*） · Modifier flags for RegisterHotKey (MOD_*).</summary>
[Flags]
public enum HotMod : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008,
}

/// <summary>一個熱鍵 → 動作綁定 · One hotkey → action binding (persisted to JSON).</summary>
public sealed class HotkeyBinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;

    // chord
    public uint Modifiers { get; set; }   // HotMod flags
    public uint VirtualKey { get; set; }  // VK_* code
    public string KeyName { get; set; } = ""; // friendly key label, e.g. "K"

    // action
    public MacroActionKind Action { get; set; } = MacroActionKind.LaunchApp;
    public string Target { get; set; } = "";    // path / URL (LaunchApp)
    public string Arguments { get; set; } = "";  // args (LaunchApp)
    public string Script { get; set; } = "";     // PowerShell body (RunPowerShell)
    public string Keys { get; set; } = "";       // text to type (SendKeys)

    public string Name { get; set; } = "";       // optional label

    public string ChordText()
    {
        var parts = new List<string>();
        var m = (HotMod)Modifiers;
        if (m.HasFlag(HotMod.Control)) parts.Add("Ctrl");
        if (m.HasFlag(HotMod.Alt)) parts.Add("Alt");
        if (m.HasFlag(HotMod.Shift)) parts.Add("Shift");
        if (m.HasFlag(HotMod.Win)) parts.Add("Win");
        parts.Add(string.IsNullOrEmpty(KeyName) ? $"0x{VirtualKey:X2}" : KeyName);
        return string.Join(" + ", parts);
    }

    public string ActionSummary() => Action switch
    {
        MacroActionKind.LaunchApp => string.IsNullOrWhiteSpace(Arguments) ? Target : $"{Target} {Arguments}",
        MacroActionKind.RunPowerShell => Script.Replace("\r", " ").Replace("\n", " "),
        MacroActionKind.SendKeys => Keys,
        MacroActionKind.OpenModule => ModuleRegistry.All.FirstOrDefault(m => m.Tag == Target) is { } mi
            ? $"{Loc.I.Pick("Open", "開啟")}: {Loc.I.Pick(mi.En, mi.Zh)}"
            : $"{Loc.I.Pick("Open module", "開啟模組")}: {Target}",
        _ => "",
    };
}

/// <summary>一個文字展開片語（縮寫 → 展開文字） · One text-expander snippet (trigger → expansion).</summary>
public sealed class Snippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; set; } = true;
    public string Trigger { get; set; } = "";   // e.g. ";addr"
    public string Expansion { get; set; } = ""; // text typed in its place
}

/// <summary>
/// 全域熱鍵 + 巨集執行器 + 文字展開（純 C#/Win32，無外部工具、無跳轉）。
/// Global hotkey + macro runner + text expander. Registers chords with user32 RegisterHotKey and pumps
/// WM_HOTKEY on a dedicated background message thread; each binding launches an app, runs a PowerShell
/// snippet, or replays input via SendInput. A WH_KEYBOARD_LL hook expands typed triggers from a snippet
/// store. Bindings + snippets persist as JSON via SettingsStore. Survives in the tray.
/// </summary>
public static class HotkeyMacroService
{
    public static ObservableCollection<HotkeyBinding> Bindings { get; } = new();
    public static ObservableCollection<Snippet> Snippets { get; } = new();

    /// <summary>提示用嘅最近事件文字 · Last status text (for the UI), e.g. "Fired Ctrl+Alt+K".</summary>
    public static string LastEvent { get; private set; } = "";
    public static event Action? Changed;       // store changed (saved)
    public static event Action? Fired;         // a hotkey fired / a snippet expanded (LastEvent updated)

    /// <summary>
    /// 由 MainWindow 接駁：一個 OpenModule 熱鍵撳咗，要求將 app 帶到前面並開某個模組（用 nav tag）。
    /// Wired by MainWindow: an OpenModule hotkey fired — bring the app to the front and open a module by tag.
    /// Invoked on the hotkey pump thread, so the handler must marshal onto the UI dispatcher itself.
    /// </summary>
    public static Action<string>? OpenModuleRequested;

    private const string BindingsKey = "hotkeymacro.bindings";
    private const string SnippetsKey = "hotkeymacro.snippets";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private static bool _loaded;
    private static bool _hotkeysRunning;
    private static bool _expanderRunning;

    // ===================== persistence =====================

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var b = SettingsStore.Get(BindingsKey, "");
            if (!string.IsNullOrWhiteSpace(b))
            {
                var list = JsonSerializer.Deserialize<List<HotkeyBinding>>(b, JsonOpts);
                if (list is not null) foreach (var x in list) Bindings.Add(x);
            }
            var s = SettingsStore.Get(SnippetsKey, "");
            if (!string.IsNullOrWhiteSpace(s))
            {
                var list = JsonSerializer.Deserialize<List<Snippet>>(s, JsonOpts);
                if (list is not null) foreach (var x in list) Snippets.Add(x);
            }
        }
        catch { /* corrupt → start empty */ }
    }

    private static void Save()
    {
        try
        {
            SettingsStore.Set(BindingsKey, JsonSerializer.Serialize(Bindings.ToList(), JsonOpts));
            SettingsStore.Set(SnippetsKey, JsonSerializer.Serialize(Snippets.ToList(), JsonOpts));
        }
        catch { }
        Changed?.Invoke();
    }

    public static void AddBinding(HotkeyBinding b) { Bindings.Add(b); Save(); RestartHotkeys(); }
    public static void RemoveBinding(HotkeyBinding b) { Bindings.Remove(b); Save(); RestartHotkeys(); }
    public static void UpdateBinding() { Save(); RestartHotkeys(); }

    public static void AddSnippet(Snippet s) { Snippets.Add(s); Save(); }
    public static void RemoveSnippet(Snippet s) { Snippets.Remove(s); Save(); }
    public static void UpdateSnippet() { Save(); }

    // ===================== global hotkeys (RegisterHotKey + WM_HOTKEY) =====================

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int pt_x; public int pt_y; }

    [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    private const uint WM_APP_RELOAD = 0x8000; // WM_APP — ask the loop to re-register
    private const uint WM_APP_QUIT = 0x8001;

    private static Thread? _hotkeyThread;
    private static uint _hotkeyThreadId;
    private static readonly Dictionary<int, HotkeyBinding> _registered = new();

    /// <summary>啟動背景熱鍵泵（idempotent） · Start the background hotkey pump (idempotent).</summary>
    public static void StartHotkeys()
    {
        Load();
        if (_hotkeysRunning) { RestartHotkeys(); return; }
        _hotkeysRunning = true;
        _hotkeyThread = new Thread(HotkeyLoop) { IsBackground = true, Name = "WinForge-Hotkeys" };
        _hotkeyThread.SetApartmentState(ApartmentState.STA);
        _hotkeyThread.Start();
    }

    /// <summary>叫個泵重新登記所有熱鍵 · Ask the pump to re-register all hotkeys.</summary>
    public static void RestartHotkeys()
    {
        if (_hotkeysRunning && _hotkeyThreadId != 0)
            PostThreadMessage(_hotkeyThreadId, WM_APP_RELOAD, IntPtr.Zero, IntPtr.Zero);
    }

    public static void StopHotkeys()
    {
        if (_hotkeysRunning && _hotkeyThreadId != 0)
            PostThreadMessage(_hotkeyThreadId, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
        _hotkeysRunning = false;
    }

    private static void HotkeyLoop()
    {
        _hotkeyThreadId = GetCurrentThreadId();
        // Force the thread to have a message queue.
        PeekMessageInit();
        RegisterAll();

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_HOTKEY)
            {
                int id = (int)msg.wParam;
                if (_registered.TryGetValue(id, out var binding))
                    RunBinding(binding);
            }
            else if (msg.message == WM_APP_RELOAD)
            {
                RegisterAll();
            }
            else if (msg.message == WM_APP_QUIT)
            {
                UnregisterAll();
                break;
            }
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint remove);
    private static void PeekMessageInit() => PeekMessage(out _, IntPtr.Zero, 0, 0, 0);

    private static int _nextId = 1;

    private static void RegisterAll()
    {
        UnregisterAll();
        foreach (var b in Bindings.ToList())
        {
            if (!b.Enabled || b.VirtualKey == 0) continue;
            int id = _nextId++;
            if (RegisterHotKey(IntPtr.Zero, id, b.Modifiers | MOD_NOREPEAT, b.VirtualKey))
                _registered[id] = b;
        }
    }

    private static void UnregisterAll()
    {
        foreach (var id in _registered.Keys.ToList())
            UnregisterHotKey(IntPtr.Zero, id);
        _registered.Clear();
    }

    private static void RunBinding(HotkeyBinding b)
    {
        LastEvent = $"{b.ChordText()} → {Loc.I.Pick("ran", "已執行")} ({b.Action})";
        Fired?.Invoke();
        try
        {
            switch (b.Action)
            {
                case MacroActionKind.LaunchApp:
                    if (!string.IsNullOrWhiteSpace(b.Target))
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = b.Target,
                            Arguments = b.Arguments ?? "",
                            UseShellExecute = true,
                        });
                    break;

                case MacroActionKind.RunPowerShell:
                    if (!string.IsNullOrWhiteSpace(b.Script))
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{b.Script.Replace("\"", "\\\"")}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                        });
                    break;

                case MacroActionKind.SendKeys:
                    if (!string.IsNullOrEmpty(b.Keys))
                        SendUnicodeString(b.Keys);
                    break;

                case MacroActionKind.OpenModule:
                    if (!string.IsNullOrWhiteSpace(b.Target))
                        OpenModuleRequested?.Invoke(b.Target);
                    break;
            }
        }
        catch (Exception ex)
        {
            LastEvent = $"{b.ChordText()} → {Loc.I.Pick("failed", "失敗")}: {ex.Message}";
            Fired?.Invoke();
        }
    }

    // ===================== SendInput (replay text) =====================

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_TAB = 0x09;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>逐個字元用 SendInput Unicode 模式打出去 · Type a string via SendInput (Unicode mode).</summary>
    public static void SendUnicodeString(string text)
    {
        var inputs = new List<INPUT>(text.Length * 2);
        foreach (char c in text)
        {
            if (c == '\n')
            {
                inputs.Add(KeyVk(VK_RETURN, false));
                inputs.Add(KeyVk(VK_RETURN, true));
                continue;
            }
            if (c == '\r') continue;
            if (c == '\t')
            {
                inputs.Add(KeyVk(VK_TAB, false));
                inputs.Add(KeyVk(VK_TAB, true));
                continue;
            }
            inputs.Add(KeyChar(c, false));
            inputs.Add(KeyChar(c, true));
        }
        if (inputs.Count > 0)
            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyChar(char c, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion { ki = new KEYBDINPUT { wVk = 0, wScan = c, dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0), time = 0, dwExtraInfo = IntPtr.Zero } }
    };

    private static INPUT KeyVk(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        u = new InputUnion { ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = up ? KEYEVENTF_KEYUP : 0, time = 0, dwExtraInfo = IntPtr.Zero } }
    };

    private static void SendBackspaces(int n)
    {
        const ushort VK_BACK = 0x08;
        var inputs = new List<INPUT>(n * 2);
        for (int i = 0; i < n; i++)
        {
            inputs.Add(KeyVk(VK_BACK, false));
            inputs.Add(KeyVk(VK_BACK, true));
        }
        if (inputs.Count > 0)
            SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>());
    }

    // ===================== text expander (WH_KEYBOARD_LL) =====================

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")]
    private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out] StringBuilder pwszBuff, int cchBuff, uint wFlags);
    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);
    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    private static Thread? _hookThread;
    private static uint _hookThreadId;
    private static IntPtr _hookHandle = IntPtr.Zero;
    private static HookProc? _hookProc; // keep alive to avoid GC of the delegate
    private static readonly StringBuilder _typed = new();
    private const int MaxBuffer = 64;
    private static volatile bool _suppressFromExpansion;

    public static bool ExpanderEnabled => _expanderRunning;

    public static void StartExpander()
    {
        Load();
        if (_expanderRunning) return;
        _expanderRunning = true;
        _hookThread = new Thread(HookLoop) { IsBackground = true, Name = "WinForge-Expander" };
        _hookThread.Start();
    }

    public static void StopExpander()
    {
        if (!_expanderRunning) return;
        if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
        _expanderRunning = false;
        _typed.Clear();
    }

    private static void HookLoop()
    {
        _hookThreadId = GetCurrentThreadId();
        PeekMessageInit();
        _hookProc = HookCallback;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);

        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_APP_QUIT) break;
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }

        if (_hookHandle != IntPtr.Zero) { UnhookWindowsHookEx(_hookHandle); _hookHandle = IntPtr.Zero; }
        _hookProc = null;
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_suppressFromExpansion && ((int)wParam == WM_KEYDOWN || (int)wParam == WM_SYSKEYDOWN))
        {
            try
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                ProcessKey(data.vkCode, data.scanCode);
            }
            catch { }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static void ProcessKey(uint vkCode, uint scanCode)
    {
        const uint VK_BACK = 0x08;
        if (vkCode == VK_BACK)
        {
            if (_typed.Length > 0) _typed.Remove(_typed.Length - 1, 1);
            return;
        }

        var ch = KeyToChar(vkCode, scanCode);
        if (ch is null)
        {
            // a non-text key (arrows, enter, tab, etc.) breaks the current word
            _typed.Clear();
            return;
        }

        _typed.Append(ch.Value);
        if (_typed.Length > MaxBuffer) _typed.Remove(0, _typed.Length - MaxBuffer);

        var buf = _typed.ToString();
        foreach (var s in Snippets.ToList())
        {
            if (!s.Enabled || string.IsNullOrEmpty(s.Trigger)) continue;
            if (buf.EndsWith(s.Trigger, StringComparison.Ordinal))
            {
                Expand(s);
                _typed.Clear();
                break;
            }
        }
    }

    private static void Expand(Snippet s)
    {
        // Replace the typed trigger with the expansion, off the hook thread so SendInput isn't re-hooked into a loop.
        _suppressFromExpansion = true;
        var trigger = s.Trigger;
        var expansion = s.Expansion;
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                Thread.Sleep(15);
                SendBackspaces(trigger.Length);
                Thread.Sleep(5);
                SendUnicodeString(expansion);
                LastEvent = $"{trigger} → {Loc.I.Pick("expanded", "已展開")}";
                Fired?.Invoke();
            }
            catch { }
            finally
            {
                Thread.Sleep(30);
                _suppressFromExpansion = false;
            }
        });
    }

    private static char? KeyToChar(uint vkCode, uint scanCode)
    {
        var keyState = new byte[256];
        if (!GetKeyboardState(keyState)) return null;
        var sb = new StringBuilder(4);
        uint sc = scanCode != 0 ? scanCode : MapVirtualKey(vkCode, 0);
        int rc = ToUnicode(vkCode, sc, keyState, sb, sb.Capacity, 0);
        if (rc == 1)
        {
            char c = sb[0];
            return char.IsControl(c) ? null : c;
        }
        return null;
    }

    // ===================== key list for the UI =====================

    /// <summary>畀 UI 揀嘅常用按鍵（名 + VK） · Common keys for the chord picker (name + VK).</summary>
    public static readonly (string Name, uint Vk)[] PickableKeys = BuildKeys();

    private static (string, uint)[] BuildKeys()
    {
        var list = new List<(string, uint)>();
        for (char c = 'A'; c <= 'Z'; c++) list.Add((c.ToString(), c));
        for (char c = '0'; c <= '9'; c++) list.Add((c.ToString(), c));
        for (int f = 1; f <= 12; f++) list.Add(($"F{f}", (uint)(0x70 + f - 1))); // VK_F1=0x70
        list.Add(("Space", 0x20));
        list.Add(("Enter", 0x0D));
        list.Add(("Tab", 0x09));
        list.Add(("Esc", 0x1B));
        list.Add(("Insert", 0x2D));
        list.Add(("Delete", 0x2E));
        list.Add(("Home", 0x24));
        list.Add(("End", 0x23));
        list.Add(("Page Up", 0x21));
        list.Add(("Page Down", 0x22));
        list.Add(("Print Screen", 0x2C));
        list.Add(("Left", 0x25));
        list.Add(("Up", 0x26));
        list.Add(("Right", 0x27));
        list.Add(("Down", 0x28));
        return list.ToArray();
    }
}
