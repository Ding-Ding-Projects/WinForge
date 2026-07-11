using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;

namespace WinForge.Services;

/// <summary>觸發鍵：用咩鍵嚟啟動候選列 · Which key activates the accent popup.</summary>
public enum QuickAccentActivationKey
{
    Both,            // Space + 左右箭咀 · Space and arrows
    Space,           // 淨係 Space
    LeftRightArrow,  // 淨係左右箭咀
}

/// <summary>候選列位置 · Where the popup appears.</summary>
public enum QuickAccentPosition
{
    Caret,   // 跟住游標／插入點 · follow the caret / cursor
    Top,     // 螢幕頂中 · top centre
    Bottom,  // 螢幕底中 · bottom centre
    Center,  // 螢幕中央 · centre
}

/// <summary>觸發鍵分類 · The trigger key that was pressed.</summary>
internal enum TriggerKey { Space, Left, Right }

/// <summary>放手時要做嘅輸入動作 · What to type when the key is released.</summary>
internal enum InputType { None, Space, Left, Right, Char }

/// <summary>快速重音符設定（存 JSON） · Quick Accent settings (persisted as JSON).</summary>
public sealed class QuickAccentSettings
{
    public bool Enabled { get; set; }
    public QuickAccentActivationKey ActivationKey { get; set; } = QuickAccentActivationKey.Both;
    public QuickAccentPosition Position { get; set; } = QuickAccentPosition.Caret;
    public int InputDelayMs { get; set; } = 200;
    public bool StartSelectionFromTheLeft { get; set; }
    public List<string> SelectedSets { get; set; } = new() { "ALL" };
}

/// <summary>
/// 快速重音符（PowerToys Quick Accent 嘅原生克隆）· Native clone of PowerToys Quick Accent.
/// 用 WH_KEYBOARD_LL 全域鍵盤鈎：按住一個基底字母再撳啟動鍵（Space／左右箭咀），就會喺游標附近彈出
/// 該字母嘅重音符變體（例如 a → à á â ã ä å æ ā …）。箭咀／重複撳啟動鍵嚟揀，放手就用 SendInput
/// 將揀中嘅字元取代原本嘅字母插入。設定（開關、啟動鍵、語言集、位置、延遲）以 JSON 存喺 SettingsStore。
/// Low-level keyboard hook: hold a base letter, press the activation key, and a popup near the caret lists
/// the accent variants; arrows / repeated activation cycle the selection; releasing inserts the chosen
/// character via SendInput (replacing the base letter). Logic ported from PowerToys poweraccent.
/// </summary>
public static class QuickAccentService
{
    private const string SettingsKey = "quickaccent.settings";
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    public static QuickAccentSettings Settings { get; private set; } = new();
    public static event Action? Changed;

    private static bool _loaded;
    private static bool _running;

    // ===================== persistence =====================

    public static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        QuickAccentSettings? loaded = null;
        try
        {
            var raw = SettingsStore.Get(SettingsKey, "");
            if (!string.IsNullOrWhiteSpace(raw))
                loaded = JsonSerializer.Deserialize<QuickAccentSettings>(raw, JsonOpts);
        }
        catch { /* corrupt → defaults */ }

        Settings = NormalizeSettings(loaded ?? Settings);
    }

    public static void Save()
    {
        Settings = NormalizeSettings(Settings);
        try { SettingsStore.Set(SettingsKey, JsonSerializer.Serialize(Settings, JsonOpts)); }
        catch { }
        QuickAccentData.InvalidateCache();
        Changed?.Invoke();
    }

    /// <summary>Repair nullable or malformed persisted collection data before UI or hook code reads it.</summary>
    internal static QuickAccentSettings NormalizeSettings(QuickAccentSettings? settings)
    {
        var normalized = settings ?? new QuickAccentSettings();
        normalized.SelectedSets = (normalized.SelectedSets ?? new List<string>())
            .Where(set => !string.IsNullOrWhiteSpace(set))
            .Select(set => set.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalized.SelectedSets.Count == 0) normalized.SelectedSets.Add("ALL");

        if (!Enum.IsDefined(normalized.ActivationKey))
            normalized.ActivationKey = QuickAccentActivationKey.Both;
        if (!Enum.IsDefined(normalized.Position))
            normalized.Position = QuickAccentPosition.Caret;
        normalized.InputDelayMs = Math.Clamp(normalized.InputDelayMs, 0, 2_000);
        return normalized;
    }

    /// <summary>由設定攞返實際要用嘅語言集識別碼（處理 "ALL"）· Resolve the effective set ids (handles "ALL").</summary>
    private static IReadOnlyCollection<string> EffectiveSets()
    {
        if (Settings.SelectedSets.Any(s => string.Equals(s, "ALL", StringComparison.OrdinalIgnoreCase)))
            return QuickAccentData.All.Select(l => l.Id).ToList();
        return Settings.SelectedSets;
    }

    /// <summary>呢個語言集／全部組合下，有冇任何重音符? · Does the current selection define variants for this key?</summary>
    private static bool IsLanguageLetter(int vk) => QuickAccentData.GetCharacters(vk, EffectiveSets()).Length > 0;

    // ===================== lifecycle =====================

    /// <summary>套用設定 + 啟停鈎 · Apply the enabled flag (start/stop the hook).</summary>
    public static void Apply()
    {
        Load();
        if (Settings.Enabled) Start();
        else Stop();
    }

    public static void SetEnabled(bool enabled)
    {
        Load();
        Settings.Enabled = enabled;
        Save();
        Apply();
    }

    public static bool IsRunning => _running;

    public static void Start()
    {
        Load();
        if (_running) return;
        _running = true;
        QuickAccentPopup.Instance.EnsureStarted();
        _hookThread = new Thread(HookLoop) { IsBackground = true, Name = "WinForge-QuickAccent" };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
    }

    public static void Stop()
    {
        if (!_running) return;
        _running = false;
        if (_hookThreadId != 0) PostThreadMessage(_hookThreadId, WM_APP_QUIT, IntPtr.Zero, IntPtr.Zero);
        QuickAccentPopup.Instance.Hide();
        ResetState();
    }

    // ===================== Win32 interop =====================

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern short GetKeyState(int vKey);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int ptx, pty; }
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode; public uint scanCode; public uint flags; public uint time; public IntPtr dwExtraInfo; }

    // SendInput
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion u; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint WM_APP_QUIT = 0x8000 + 9;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    private const int VK_SPACE = 0x20;
    private const int VK_LEFT = 0x25;
    private const int VK_RIGHT = 0x27;
    private const int VK_BACK = 0x08;
    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_CAPITAL = 0x14;

    // ===================== hook thread =====================

    private static Thread? _hookThread;
    private static uint _hookThreadId;
    private static IntPtr _hookHandle = IntPtr.Zero;
    private static HookProc? _hookProc; // keep alive

    private static void HookLoop()
    {
        _hookThreadId = GetCurrentThreadId();
        PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
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
        _hookThreadId = 0;
    }

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_suppress)
        {
            try
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int w = (int)wParam;
                if (w == WM_KEYDOWN || w == WM_SYSKEYDOWN)
                {
                    if (OnKeyDown((int)data.vkCode)) return (IntPtr)1; // swallow
                }
                else if (w == WM_KEYUP || w == WM_SYSKEYUP)
                {
                    if (OnKeyUp((int)data.vkCode)) return (IntPtr)1;
                }
            }
            catch { }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    // ===================== state machine (ported from PowerToys KeyboardListener) =====================

    private static volatile bool _suppress;          // true while we synthesise SendInput
    private static bool _toolbarVisible;
    private static int _letterPressed;               // VK of the held base letter (0 = none)
    private static long _showTicks;                  // when popup was requested (for input delay)
    private static bool _triggeredWithSpace, _triggeredWithLeft, _triggeredWithRight;
    private static bool _leftShift, _rightShift;
    private static bool _initialShift;

    private static string[] _characters = Array.Empty<string>();
    private static int _selectedIndex = -1;

    private static readonly object _stateGate = new();

    private static void ResetState()
    {
        lock (_stateGate)
        {
            _toolbarVisible = false;
            _letterPressed = 0;
            _selectedIndex = -1;
            _characters = Array.Empty<string>();
            _triggeredWithSpace = _triggeredWithLeft = _triggeredWithRight = false;
            _leftShift = _rightShift = false;
        }
    }

    private static bool IsTriggerKey(int vk) => vk == VK_SPACE || vk == VK_LEFT || vk == VK_RIGHT;

    private static bool ShiftDown() => (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
    private static bool CapsOn() => (GetKeyState(VK_CAPITAL) & 1) != 0;

    private static bool OnKeyDown(int vk)
    {
        // Shift is only tracked once the toolbar is up, to avoid clashing with typing uppercase.
        if (_toolbarVisible && vk == VK_LSHIFT) _leftShift = true;
        if (_toolbarVisible && vk == VK_RSHIFT) _rightShift = true;

        if (QuickAccentData.IsLetterKey(vk) && IsLanguageLetter(vk))
        {
            if (_toolbarVisible && _letterPressed == vk)
            {
                // on-screen keyboards re-send KEYDOWN while held — swallow it
                return true;
            }
            _showTicks = DateTime.UtcNow.Ticks;
            _letterPressed = vk;
        }

        int triggerPressed = 0;
        if (_letterPressed != 0 && IsTriggerKey(vk))
        {
            triggerPressed = vk;
            bool letterReleased = (GetAsyncKeyState(_letterPressed) & 0x8000) == 0;
            var act = Settings.ActivationKey;
            if (letterReleased ||
                (triggerPressed == VK_SPACE && act == QuickAccentActivationKey.LeftRightArrow) ||
                ((triggerPressed == VK_LEFT || triggerPressed == VK_RIGHT) && act == QuickAccentActivationKey.Space))
            {
                triggerPressed = 0; // not an activation in this mode → let it through
            }
        }

        if (!_toolbarVisible && _letterPressed != 0 && triggerPressed != 0)
        {
            _triggeredWithSpace = triggerPressed == VK_SPACE;
            _triggeredWithLeft = triggerPressed == VK_LEFT;
            _triggeredWithRight = triggerPressed == VK_RIGHT;
            _toolbarVisible = true;
            ShowToolbar(_letterPressed);
        }

        if (_toolbarVisible && triggerPressed != 0)
        {
            bool shift = _leftShift || _rightShift;
            if (triggerPressed == VK_LEFT) ProcessNextChar(TriggerKey.Left, shift);
            else if (triggerPressed == VK_RIGHT) ProcessNextChar(TriggerKey.Right, shift);
            else if (triggerPressed == VK_SPACE) ProcessNextChar(TriggerKey.Space, shift);
            return true; // swallow the trigger so it doesn't reach the app
        }

        return false;
    }

    private static bool OnKeyUp(int vk)
    {
        if (vk == VK_LSHIFT) _leftShift = false;
        if (vk == VK_RSHIFT) _rightShift = false;

        if (QuickAccentData.IsLetterKey(vk) && IsLanguageLetter(vk) && vk == _letterPressed)
        {
            _letterPressed = 0;

            if (_toolbarVisible)
            {
                long elapsedMs = (DateTime.UtcNow.Ticks - _showTicks) / TimeSpan.TicksPerMillisecond;
                if (elapsedMs < Settings.InputDelayMs)
                {
                    // false start — too fast. Emit the trigger we swallowed if it was one.
                    if (_triggeredWithSpace) SendInputAndHide(InputType.Space);
                    else if (_triggeredWithLeft) SendInputAndHide(InputType.Left);
                    else if (_triggeredWithRight) SendInputAndHide(InputType.Right);
                    else SendInputAndHide(InputType.None);
                    return true;
                }

                SendInputAndHide(InputType.Char);
                return true;
            }
        }

        return false;
    }

    private static void ShowToolbar(int vk)
    {
        _initialShift = ShiftDown();
        _selectedIndex = -1;
        _characters = GetCharacters(vk);

        int delay = Settings.InputDelayMs;
        var snapshotLetter = vk;
        // Defer the visual show by the input delay so quick taps don't flash the popup.
        System.Threading.Tasks.Task.Run(() =>
        {
            if (delay > 0) Thread.Sleep(delay);
            // only show if still holding the same letter and still visible
            if (_toolbarVisible && _letterPressed == snapshotLetter && _characters.Length > 0)
            {
                QuickAccentPopup.Instance.Show(_characters, Settings.Position);
                if (_selectedIndex >= 0) QuickAccentPopup.Instance.Select(_selectedIndex);
            }
        });
    }

    private static string[] GetCharacters(int vk)
    {
        var chars = QuickAccentData.GetCharacters(vk, EffectiveSets());
        if (CapsOn() || ShiftDown())
            return ToUpper(chars);
        return chars;
    }

    private static void ProcessNextChar(TriggerKey trigger, bool shiftPressed)
    {
        bool hardwareShift = ShiftDown() && !_initialShift;
        shiftPressed = shiftPressed || hardwareShift;

        if (_characters.Length == 0) return;

        if (_toolbarVisible && _selectedIndex == -1)
        {
            if (trigger == TriggerKey.Space)
                _selectedIndex = shiftPressed ? _characters.Length - 1 : 0;
            else if (Settings.StartSelectionFromTheLeft)
                _selectedIndex = 0;
            else if (trigger == TriggerKey.Left)
                _selectedIndex = (_characters.Length / 2) - 1;
            else if (trigger == TriggerKey.Right)
                _selectedIndex = _characters.Length / 2;

            if (_selectedIndex < 0) _selectedIndex = 0;
            if (_selectedIndex > _characters.Length - 1) _selectedIndex = _characters.Length - 1;

            QuickAccentPopup.Instance.Show(_characters, Settings.Position);
            QuickAccentPopup.Instance.Select(_selectedIndex);
            return;
        }

        if (trigger == TriggerKey.Space)
        {
            if (shiftPressed)
                _selectedIndex = _selectedIndex == 0 ? _characters.Length - 1 : _selectedIndex - 1;
            else
                _selectedIndex = _selectedIndex < _characters.Length - 1 ? _selectedIndex + 1 : 0;
        }
        else if (trigger == TriggerKey.Left)
        {
            _selectedIndex--;
        }
        else if (trigger == TriggerKey.Right)
        {
            _selectedIndex++;
        }

        if (_selectedIndex < 0) _selectedIndex = _characters.Length - 1;
        if (_selectedIndex > _characters.Length - 1) _selectedIndex = 0;

        QuickAccentPopup.Instance.Select(_selectedIndex);
    }

    private static void SendInputAndHide(InputType type)
    {
        switch (type)
        {
            case InputType.Space: InsertText(" ", false); break;
            case InputType.Left: SendVk(VK_LEFT); break;
            case InputType.Right: SendVk(VK_RIGHT); break;
            case InputType.Char:
                if (_selectedIndex != -1 && _selectedIndex < _characters.Length)
                    InsertText(_characters[_selectedIndex], true);
                break;
        }

        QuickAccentPopup.Instance.Hide();
        _selectedIndex = -1;
        _toolbarVisible = false;
        _triggeredWithSpace = _triggeredWithLeft = _triggeredWithRight = false;
    }

    // ===================== SendInput helpers =====================

    /// <summary>插入字串；back=true 先撳一下 Backspace 刪走原本嘅字母 · Insert text, optionally backspacing the base letter first.</summary>
    private static void InsertText(string s, bool back)
    {
        _suppress = true;
        try
        {
            if (back)
            {
                var bs = new[]
                {
                    KeyVk(VK_BACK, false),
                    KeyVk(VK_BACK, true),
                };
                SendInput((uint)bs.Length, bs, Marshal.SizeOf<INPUT>());
                Thread.Sleep(1); // some apps (Terminal) need a beat to process the backspace
            }

            if (s.Length > 0)
            {
                var inputs = new INPUT[s.Length * 2];
                for (int i = 0; i < s.Length; i++)
                {
                    inputs[i * 2] = KeyChar(s[i], false);
                    inputs[i * 2 + 1] = KeyChar(s[i], true);
                }
                SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            }
        }
        catch { }
        finally
        {
            // brief tail so the synthesised events have flushed past our hook
            System.Threading.Tasks.Task.Run(() => { Thread.Sleep(20); _suppress = false; });
        }
    }

    private static void SendVk(int vk)
    {
        _suppress = true;
        try
        {
            var inputs = new[] { KeyVk((ushort)vk, false), KeyVk((ushort)vk, true) };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }
        catch { }
        finally
        {
            System.Threading.Tasks.Task.Run(() => { Thread.Sleep(20); _suppress = false; });
        }
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

    // ===================== uppercase mapping (ported) =====================

    private static string[] ToUpper(string[] array)
    {
        var result = new List<string>(array.Length);
        foreach (var s in array)
        {
            switch (s)
            {
                case "ß": result.Add("ẞ"); break;
                case "ı": result.Add("İ"); break;
                case "ϑ": break; // no sensible upper form
                default: result.Add(s.ToUpper(CultureInfo.InvariantCulture)); break;
            }
        }
        return result.ToArray();
    }

    // ===================== preview helper for the settings UI =====================

    /// <summary>預覽：畀定基底字元，返回現時選定語言集嘅變體 · Preview variants for a base char under the current selection.</summary>
    public static string[] PreviewFor(char baseChar)
    {
        Load();
        int vk = char.ToUpperInvariant(baseChar);
        if (!QuickAccentData.IsLetterKey(vk)) return Array.Empty<string>();
        return QuickAccentData.GetCharacters(vk, EffectiveSets());
    }
}
