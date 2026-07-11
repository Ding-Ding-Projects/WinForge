using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 指令面板（PowerToys Run／Command Palette 式）· The Command Palette engine — a global quick-launcher.
///
/// 一個全域熱鍵（預設 Alt+Space）會彈出一個置中、置頂、無邊框嘅搜尋視窗。輸入嘅字會
/// 交畀多個「結果提供者」（已安裝程式、WinForge 模組、檔案／資料夾、計算機、執行指令／開網址、
/// 系統動作、網絡搜尋），每個都會貢獻有分數嘅結果。模糊比對 + 排名之後顯示，Enter 啟動。
///
/// A configurable global hotkey opens a centered, topmost, borderless search window. The typed
/// query is fanned out to a set of result providers (installed apps, WinForge modules, files/folders,
/// calculator, run command / open URL, system actions, web search fallback). Results are fuzzy-matched,
/// ranked, and the top/selected one is launched on Enter. Pure in-process — no external launcher.
/// </summary>
public static class CommandPaletteService
{
    // ===================== Settings keys · 設定鍵 =====================
    private const string KeyEnabled = "cmdpal.enabled";
    private const string KeyHotkey = "cmdpal.hotkey";        // e.g. "Alt+Space"
    private const string KeyMaxResults = "cmdpal.maxResults";
    private const string KeyProviderPrefix = "cmdpal.provider."; // + provider id
    private const string KeyDockPins = "cmdpal.dock.pins";
    private const string KeyBookmarks = "cmdpal.bookmarks";
    private const string KeyRemoteDesktopProfiles = "cmdpal.rdp.profiles";

    // ===================== Provider identity · 提供者識別 =====================
    public enum Provider { Apps, Bookmarks, RemoteDesktop, Performance, Windows, Modules, Files, Clipboard, Calculator, TimeDate, Settings, Services, Terminal, Run, System, Web }

    public static IReadOnlyList<Provider> AllProviders { get; } = new[]
    {
        Provider.Apps, Provider.Bookmarks, Provider.RemoteDesktop, Provider.Performance, Provider.Windows, Provider.Modules, Provider.Files, Provider.Clipboard,
        Provider.Calculator, Provider.TimeDate, Provider.Settings, Provider.Services, Provider.Terminal,
        Provider.Run, Provider.System, Provider.Web,
    };

    /// <summary>提供者嘅雙語名 · Bilingual display name for a provider.</summary>
    public static (string En, string Zh) ProviderName(Provider p) => p switch
    {
        Provider.Apps => ("Installed apps", "已安裝程式"),
        Provider.Bookmarks => ("Bookmarks", "書籤"),
        Provider.RemoteDesktop => ("Remote Desktop", "遠端桌面"),
        Provider.Performance => ("Performance metrics", "效能指標"),
        Provider.Windows => ("Open windows", "已開啟視窗"),
        Provider.Modules => ("WinForge modules", "WinForge 模組"),
        Provider.Files => ("Files & folders", "檔案與資料夾"),
        Provider.Clipboard => ("Clipboard history", "剪貼簿記錄"),
        Provider.Calculator => ("Calculator", "計算機"),
        Provider.TimeDate => ("Time & date", "時間與日期"),
        Provider.Settings => ("Windows Settings", "Windows 設定"),
        Provider.Services => ("Windows services", "Windows 服務"),
        Provider.Terminal => ("Terminal profiles", "終端機設定檔"),
        Provider.Run => ("Run / open URL", "執行／開網址"),
        Provider.System => ("System actions", "系統動作"),
        Provider.Web => ("Web search", "網絡搜尋"),
        _ => ("", ""),
    };

    // ===================== Enable / config state · 啟用與設定狀態 =====================

    public static bool Enabled
    {
        get => SettingsStore.Get(KeyEnabled, "True") == "True";
        set { SettingsStore.Set(KeyEnabled, value.ToString()); }
    }

    public static int MaxResults
    {
        get { return int.TryParse(SettingsStore.Get(KeyMaxResults, "8"), out var n) && n is >= 3 and <= 25 ? n : 8; }
        set { SettingsStore.Set(KeyMaxResults, Math.Clamp(value, 3, 25).ToString()); }
    }

    /// <summary>熱鍵字串（例如 "Alt+Space"）· The configured hotkey string, e.g. "Alt+Space".</summary>
    public static string HotkeyText
    {
        get => SettingsStore.Get(KeyHotkey, "Alt+Space");
        set { SettingsStore.Set(KeyHotkey, string.IsNullOrWhiteSpace(value) ? "Alt+Space" : value.Trim()); }
    }

    public static bool IsProviderEnabled(Provider p)
        => SettingsStore.Get(KeyProviderPrefix + p, "True") == "True";

    public static void SetProviderEnabled(Provider p, bool on)
        => SettingsStore.Set(KeyProviderPrefix + p, on.ToString());

    // ===================== Bookmarks · 書籤 =====================
    /// <summary>A user-managed web bookmark surfaced by Command Palette and eligible for Dock pins.</summary>
    public sealed class PaletteBookmark
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
    }

    public static IReadOnlyList<PaletteBookmark> Bookmarks => ReadBookmarks();

    public static bool TryAddBookmark(string? name, string? url, out PaletteBookmark bookmark)
    {
        bookmark = new PaletteBookmark();
        string normalized = NormalizeBookmarkUrl(url);
        if (string.IsNullOrEmpty(normalized)) return false;
        var bookmarks = ReadBookmarks();
        int existing = bookmarks.FindIndex(b => string.Equals(b.Url, normalized, StringComparison.OrdinalIgnoreCase));
        string label = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label) && Uri.TryCreate(normalized, UriKind.Absolute, out var uri)) label = uri.Host;
        if (string.IsNullOrWhiteSpace(label)) label = normalized;
        bookmark = new PaletteBookmark { Name = label, Url = normalized };
        if (existing >= 0) bookmarks[existing] = bookmark;
        else
        {
            if (bookmarks.Count >= 100) bookmarks.RemoveAt(0);
            bookmarks.Add(bookmark);
        }
        SaveBookmarks(bookmarks);
        return true;
    }

    public static void RemoveBookmark(PaletteBookmark bookmark)
    {
        if (bookmark is null || string.IsNullOrWhiteSpace(bookmark.Url)) return;
        var bookmarks = ReadBookmarks();
        bookmarks.RemoveAll(b => string.Equals(b.Url, bookmark.Url, StringComparison.OrdinalIgnoreCase));
        SaveBookmarks(bookmarks);
    }

    private static List<PaletteBookmark> ReadBookmarks()
    {
        try
        {
            return (JsonSerializer.Deserialize<List<PaletteBookmark>>(SettingsStore.Get(KeyBookmarks, "[]")) ?? new List<PaletteBookmark>())
                .Where(b => !string.IsNullOrWhiteSpace(b.Name) && !string.IsNullOrWhiteSpace(NormalizeBookmarkUrl(b.Url)))
                .Select(b => new PaletteBookmark { Name = b.Name.Trim(), Url = NormalizeBookmarkUrl(b.Url) })
                .ToList();
        }
        catch { return new List<PaletteBookmark>(); }
    }

    private static void SaveBookmarks(List<PaletteBookmark> bookmarks)
    {
        try { SettingsStore.Set(KeyBookmarks, JsonSerializer.Serialize(bookmarks.Take(100).ToList())); }
        catch { }
    }

    private static string NormalizeBookmarkUrl(string? value)
    {
        string url = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url)) return "";
        if (!url.Contains("://", StringComparison.Ordinal)) url = "https://" + url;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri.AbsoluteUri : "";
    }

    // ===================== Remote Desktop profiles · 遠端桌面設定檔 =====================
    /// <summary>RDP endpoint metadata only. Credentials remain with the Windows Remote Desktop client.</summary>
    public sealed class RemoteDesktopProfile
    {
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
    }

    public static IReadOnlyList<RemoteDesktopProfile> RemoteDesktopProfiles => ReadRemoteDesktopProfiles();

    public static bool TryAddRemoteDesktopProfile(string? name, string? host, out RemoteDesktopProfile profile)
    {
        profile = new RemoteDesktopProfile();
        string endpoint = NormalizeRemoteDesktopHost(host);
        if (string.IsNullOrWhiteSpace(endpoint)) return false;
        string label = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(label)) label = endpoint;
        var profiles = ReadRemoteDesktopProfiles();
        profile = new RemoteDesktopProfile { Name = label, Host = endpoint };
        int existing = profiles.FindIndex(p => string.Equals(p.Host, endpoint, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0) profiles[existing] = profile;
        else
        {
            if (profiles.Count >= 100) profiles.RemoveAt(0);
            profiles.Add(profile);
        }
        SaveRemoteDesktopProfiles(profiles);
        return true;
    }

    public static void RemoveRemoteDesktopProfile(RemoteDesktopProfile profile)
    {
        if (profile is null || string.IsNullOrWhiteSpace(profile.Host)) return;
        var profiles = ReadRemoteDesktopProfiles();
        profiles.RemoveAll(p => string.Equals(p.Host, profile.Host, StringComparison.OrdinalIgnoreCase));
        SaveRemoteDesktopProfiles(profiles);
    }

    private static List<RemoteDesktopProfile> ReadRemoteDesktopProfiles()
    {
        try
        {
            return (JsonSerializer.Deserialize<List<RemoteDesktopProfile>>(SettingsStore.Get(KeyRemoteDesktopProfiles, "[]")) ?? new List<RemoteDesktopProfile>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(NormalizeRemoteDesktopHost(p.Host)))
                .Select(p => new RemoteDesktopProfile { Name = p.Name.Trim(), Host = NormalizeRemoteDesktopHost(p.Host) })
                .ToList();
        }
        catch { return new List<RemoteDesktopProfile>(); }
    }

    private static void SaveRemoteDesktopProfiles(List<RemoteDesktopProfile> profiles)
    {
        try { SettingsStore.Set(KeyRemoteDesktopProfiles, JsonSerializer.Serialize(profiles.Take(100).ToList())); }
        catch { }
    }

    private static string NormalizeRemoteDesktopHost(string? value)
    {
        string endpoint = (value ?? "").Trim();
        if (endpoint.StartsWith("rdp://", StringComparison.OrdinalIgnoreCase)) endpoint = endpoint.Substring("rdp://".Length);
        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Any(char.IsWhiteSpace)
            || endpoint.IndexOfAny(new[] { '/', '\\', '"', '\'' }) >= 0) return "";
        return endpoint;
    }

    // ===================== Dock pins · Dock 釘選 =====================
    /// <summary>A persistent Dock entry captured from a Command Palette result.</summary>
    public sealed class DockPin
    {
        public string Query { get; set; } = "";
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Glyph { get; set; } = "";
        public string ProviderTag { get; set; } = "";
    }

    private static readonly DockPin[] DefaultDockPins =
    {
        new() { Query = "time", Title = "Time & date · 時間與日期", Glyph = ((char)0xE823).ToString(), ProviderTag = "Time" },
        new() { Query = "$display", Title = "Display · 顯示器", Glyph = ((char)0xE713).ToString(), ProviderTag = "Settings" },
        new() { Query = "terminal", Title = "Windows Terminal · Windows 終端機", Glyph = ((char)0xE756).ToString(), ProviderTag = "Terminal" },
    };

    /// <summary>Saved pins, or a small safe starter set until the user pins results.</summary>
    public static IReadOnlyList<DockPin> EffectiveDockPins
    {
        get
        {
            var pins = ReadDockPins();
            return pins.Count > 0 ? pins : DefaultDockPins;
        }
    }

    /// <summary>Toggle the selected result in the persistent Dock. Returns true when it was pinned.</summary>
    public static bool ToggleDockPin(CommandPaletteResult result, string? query)
    {
        if (result is null || string.IsNullOrWhiteSpace(result.Title)) return false;
        string normalizedQuery = string.IsNullOrWhiteSpace(query) ? result.Title : query.Trim();
        var pins = ReadDockPins();
        int existing = pins.FindIndex(p => string.Equals(p.Query, normalizedQuery, StringComparison.OrdinalIgnoreCase)
            && string.Equals(p.Title, result.Title, StringComparison.Ordinal)
            && string.Equals(p.ProviderTag, result.ProviderTag, StringComparison.Ordinal));
        if (existing >= 0)
        {
            pins.RemoveAt(existing);
            SaveDockPins(pins);
            CommandPaletteDockService.Refresh();
            return false;
        }

        if (pins.Count >= 8) pins.RemoveAt(0);
        pins.Add(new DockPin
        {
            Query = normalizedQuery,
            Title = result.Title,
            Subtitle = result.Subtitle,
            Glyph = result.Glyph,
            ProviderTag = result.ProviderTag,
        });
        SaveDockPins(pins);
        CommandPaletteDockService.Refresh();
        return true;
    }

    /// <summary>Invoke a dock pin by resolving its original query again; fall back to the palette if it changed.</summary>
    public static bool InvokeDockPin(DockPin pin)
    {
        try
        {
            var result = Query(pin.Query).FirstOrDefault(r => string.Equals(r.Title, pin.Title, StringComparison.Ordinal)
                && string.Equals(r.ProviderTag, pin.ProviderTag, StringComparison.Ordinal));
            if (result is not null) return result.Invoke();
        }
        catch { }

        try { CommandPaletteWindow.OpenWithQuery(pin.Query); } catch { }
        return false;
    }

    private static List<DockPin> ReadDockPins()
    {
        try
        {
            return JsonSerializer.Deserialize<List<DockPin>>(SettingsStore.Get(KeyDockPins, "[]")) ?? new List<DockPin>();
        }
        catch { return new List<DockPin>(); }
    }

    private static void SaveDockPins(List<DockPin> pins)
    {
        try { SettingsStore.Set(KeyDockPins, JsonSerializer.Serialize(pins.Take(8).ToList())); }
        catch { }
    }

    // ===================== Global hotkey (low-level keyboard hook) · 全域熱鍵 =====================
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_CONTROL = 0x11, VK_MENU = 0x12 /*Alt*/, VK_SHIFT = 0x10, VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    private static IntPtr _hook = IntPtr.Zero;
    private static LowLevelKeyboardProc? _proc;
    private static DispatcherQueue? _ui;
    private static readonly NativeMessagePump HookPump = new("WinForge-CommandPaletteHook");

    // Parsed hotkey: required modifiers + the main virtual key.
    private static bool _needCtrl, _needAlt, _needShift, _needWin;
    private static uint _vkMain;

    public static bool HotkeyActive => _hook != IntPtr.Zero;

    /// <summary>開機時呼叫：若已啟用就裝全域熱鍵 · Call at startup; installs the hotkey if the module is enabled.</summary>
    public static void Start(DispatcherQueue uiQueue)
    {
        _ui = uiQueue;
        CommandPaletteDockService.Initialize(uiQueue);
        if (Enabled)
        {
            HookPump.Post(InstallHotkey);
            EnsureServiceCache();
        }
    }

    /// <summary>重新套用設定（啟用狀態／熱鍵改變後）· Re-apply settings after the user toggles or rebinds.</summary>
    public static void Reapply()
    {
        HookPump.Post(() =>
        {
            RemoveHotkey();
            if (Enabled) InstallHotkey();
        });
        CommandPaletteDockService.Reapply();
    }

    private static void InstallHotkey()
    {
        if (_hook != IntPtr.Zero || _ui is null) return;
        ParseHotkey(HotkeyText);
        if (_vkMain == 0) return; // unparseable → no hook
        _proc = HookProc;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    private static void RemoveHotkey()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        _proc = null;
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                if (data.vkCode == _vkMain)
                {
                    bool ctrl = Down(VK_CONTROL);
                    bool alt = Down(VK_MENU);
                    bool shift = Down(VK_SHIFT);
                    bool win = Down(VK_LWIN) || Down(VK_RWIN);
                    if (ctrl == _needCtrl && alt == _needAlt && shift == _needShift && win == _needWin)
                    {
                        _ui?.TryEnqueue(() => { try { CommandPaletteWindow.Toggle(); } catch { } });
                        return (IntPtr)1; // swallow the chord
                    }
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    /// <summary>解析熱鍵字串成修飾鍵 + 主鍵 · Parse "Alt+Space" / "Ctrl+Shift+P" etc. into modifiers + main vk.</summary>
    private static void ParseHotkey(string text)
    {
        _needCtrl = _needAlt = _needShift = _needWin = false;
        _vkMain = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl": case "control": _needCtrl = true; break;
                case "alt": _needAlt = true; break;
                case "shift": _needShift = true; break;
                case "win": case "windows": case "meta": _needWin = true; break;
                default: _vkMain = VkFromName(raw); break;
            }
        }
    }

    private static uint VkFromName(string key)
    {
        key = key.Trim();
        switch (key.ToLowerInvariant())
        {
            case "space": return 0x20;
            case "enter": case "return": return 0x0D;
            case "tab": return 0x09;
            case "esc": case "escape": return 0x1B;
            case "f1": return 0x70; case "f2": return 0x71; case "f3": return 0x72; case "f4": return 0x73;
            case "f5": return 0x74; case "f6": return 0x75; case "f7": return 0x76; case "f8": return 0x77;
            case "f9": return 0x78; case "f10": return 0x79; case "f11": return 0x7A; case "f12": return 0x7B;
        }
        if (key.Length == 1)
        {
            char c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z' or >= '0' and <= '9') return c;
        }
        return 0;
    }

    /// <summary>可揀嘅熱鍵清單（畀設定頁下拉用）· The hotkey presets shown in the settings page.</summary>
    public static IReadOnlyList<string> HotkeyChoices { get; } = new[]
    {
        "Alt+Space", "Ctrl+Space", "Win+Space", "Ctrl+Shift+Space",
        "Alt+R", "Ctrl+Shift+P", "Win+R",
    };

    // ===================== Query → results · 查詢轉結果 =====================

    /// <summary>主搜尋：將查詢交畀啟用咗嘅提供者，模糊比對 + 排名 · Fan out the query, fuzzy-rank, return top N.</summary>
    public static List<CommandPaletteResult> Query(string query)
    {
        query = (query ?? "").Trim();
        var results = new List<CommandPaletteResult>();
        if (query.Length == 0)
        {
            // Empty query → show a few helpful starting points (recent modules + system actions hint).
            if (IsProviderEnabled(Provider.Modules)) results.AddRange(TopModules());
            return results.Take(MaxResults).ToList();
        }

        if (IsProviderEnabled(Provider.Calculator)) AddCalculator(query, results);
        if (IsProviderEnabled(Provider.Apps)) AddApps(query, results);
        if (IsProviderEnabled(Provider.Bookmarks)) AddBookmarks(query, results);
        if (IsProviderEnabled(Provider.RemoteDesktop)) AddRemoteDesktopProfiles(query, results);
        if (IsProviderEnabled(Provider.Performance)) AddPerformanceMetrics(query, results);
        if (IsProviderEnabled(Provider.Windows)) AddOpenWindows(query, results);
        if (IsProviderEnabled(Provider.Modules)) AddModules(query, results);
        if (IsProviderEnabled(Provider.Files)) AddFiles(query, results);
        if (IsProviderEnabled(Provider.Clipboard)) AddClipboard(query, results);
        if (IsProviderEnabled(Provider.TimeDate)) AddTimeAndDate(query, results);
        if (IsProviderEnabled(Provider.Settings)) AddWindowsSettings(query, results);
        if (IsProviderEnabled(Provider.Services)) AddWindowsServices(query, results);
        if (IsProviderEnabled(Provider.Terminal)) AddTerminalProfiles(query, results);
        if (IsProviderEnabled(Provider.Run)) AddRunOrUrl(query, results);
        if (IsProviderEnabled(Provider.System)) AddSystem(query, results);
        if (IsProviderEnabled(Provider.Web)) AddWeb(query, results);

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .Take(MaxResults)
            .ToList();
    }

    // ----- Fuzzy match · 模糊比對 -----
    // Returns a score in [0,100]; 0 means no match. Subsequence + word-boundary + prefix bonuses.
    public static double Fuzzy(string query, string target)
    {
        if (string.IsNullOrEmpty(query)) return 1;
        if (string.IsNullOrEmpty(target)) return 0;
        var q = query.ToLowerInvariant();
        var t = target.ToLowerInvariant();

        if (t == q) return 100;
        if (t.StartsWith(q)) return 90 + Math.Min(9, q.Length);
        if (t.Contains(q)) return 70 + Math.Min(9, q.Length);

        // Subsequence match with bonuses for matching at word boundaries.
        int ti = 0, qi = 0, matched = 0; double bonus = 0; bool prevBoundary = true;
        while (ti < t.Length && qi < q.Length)
        {
            bool atBoundary = ti == 0 || t[ti - 1] == ' ' || t[ti - 1] == '.' || t[ti - 1] == '-' || t[ti - 1] == '_';
            if (t[ti] == q[qi])
            {
                matched++;
                if (atBoundary || prevBoundary) bonus += 6;
                qi++;
            }
            prevBoundary = atBoundary;
            ti++;
        }
        if (qi < q.Length) return 0; // not all query chars consumed
        double coverage = (double)matched / Math.Max(1, t.Length);
        return Math.Min(65, 25 + bonus + coverage * 20);
    }

    // ----- WinForge modules · WinForge 模組 -----
    private static IEnumerable<CommandPaletteResult> TopModules()
    {
        foreach (var m in ModuleRegistry.All.Take(8))
            yield return ModuleResult(m);
    }

    private static void AddModules(string query, List<CommandPaletteResult> list)
    {
        foreach (var m in ModuleRegistry.All)
        {
            double best = Math.Max(Fuzzy(query, m.En), Math.Max(Fuzzy(query, m.Zh), Fuzzy(query, m.Keywords) * 0.85));
            if (best <= 0) continue;
            var r = ModuleResult(m);
            r.Score = best * 1.05; // modules are first-class in WinForge → slight boost
            list.Add(r);
        }
    }

    private static CommandPaletteResult ModuleResult(ModuleInfo m) => new()
    {
        Title = $"{m.En} · {m.Zh}",
        Subtitle = Loc.I.Pick("Open WinForge module", "開啟 WinForge 模組"),
        Glyph = string.IsNullOrEmpty(m.Glyph) ? ((char)0xE8FC).ToString() : m.Glyph,
        ProviderTag = Loc.I.Pick("Module", "模組"),
        Score = 1,
        Invoke = () => { try { Navigator.GoToModule?.Invoke(m.Tag); ShowShell(); } catch { } return true; },
    };

    // ----- Installed apps (Start Menu .lnk + UWP) · 已安裝程式 -----
    private static List<(string name, string path)>? _appCache;
    private static DateTime _appCacheAt;
    private static readonly object _appCacheGate = new();

    private static List<(string name, string path)> Apps()
    {
        lock (_appCacheGate)
        {
            if (_appCache is not null && (DateTime.UtcNow - _appCacheAt).TotalMinutes < 5) return _appCache;
            var apps = new List<(string, string)>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            })
            {
                try
                {
                    if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                    foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(lnk);
                        if (seen.Add(name)) apps.Add((name, lnk));
                    }
                    foreach (var url in Directory.EnumerateFiles(root, "*.url", SearchOption.AllDirectories))
                    {
                        var name = Path.GetFileNameWithoutExtension(url);
                        if (seen.Add(name)) apps.Add((name, url));
                    }
                }
                catch { /* skip unreadable roots */ }
            }
            // UWP apps via AppsFolder shell namespace → launched by AUMID through explorer.
            foreach (var (name, aumid) in UwpApps())
                if (seen.Add(name)) apps.Add((name, "shell:AppsFolder\\" + aumid));

            _appCache = apps;
            _appCacheAt = DateTime.UtcNow;
            return apps;
        }
    }

    private static void AddApps(string query, List<CommandPaletteResult> list)
    {
        foreach (var (name, path) in Apps())
        {
            double s = Fuzzy(query, name);
            if (s <= 0) continue;
            list.Add(new CommandPaletteResult
            {
                Title = name,
                Subtitle = path.StartsWith("shell:AppsFolder") ? Loc.I.Pick("Windows app", "Windows 應用程式") : path,
                Glyph = ((char)0xE71D).ToString(),
                ProviderTag = Loc.I.Pick("App", "程式"),
                Score = s,
                Invoke = () => { LaunchPath(path); return true; },
            });
        }
    }

    // ----- Calculator · 計算機 -----
    private static void AddCalculator(string query, List<CommandPaletteResult> list)
    {
        if (!LooksLikeMath(query)) return;
        if (TryEvaluate(query, out var value))
        {
            string val = value;
            list.Add(new CommandPaletteResult
            {
                Title = $"{query} = {val}",
                Subtitle = Loc.I.Pick("Press Enter to copy the result", "按 Enter 複製結果"),
                Glyph = ((char)0xE8EF).ToString(),
                ProviderTag = Loc.I.Pick("Calculator", "計算機"),
                Score = 200, // exact computed answer should top the list
                Invoke = () => { CopyText(val); return true; },
            });
        }
    }

    private static bool LooksLikeMath(string q)
    {
        q = q.Trim();
        if (q.Length == 0) return false;
        bool hasDigit = q.Any(char.IsDigit);
        bool hasOp = q.IndexOfAny(new[] { '+', '-', '*', '/', '(', ')', '%', '^' }) >= 0;
        // Only whitelist math-ish characters so plain words like "a-b-c" don't get treated as expressions.
        bool onlyMathChars = q.All(c => char.IsDigit(c) || char.IsWhiteSpace(c)
            || c is '+' or '-' or '*' or '/' or '(' or ')' or '%' or '^' or '.' or ',');
        return hasDigit && hasOp && onlyMathChars;
    }

    private static bool TryEvaluate(string expr, out string result)
    {
        result = "";
        try
        {
            var e = expr.Trim().Replace("^", "**");
            // DataTable.Compute supports + - * / % and parentheses; map ** unsupported → reject.
            if (e.Contains("**")) { return TryPow(expr, out result); }
            using var dt = new DataTable();
            var o = dt.Compute(e, null);
            if (o is null || o == DBNull.Value) return false;
            double d = Convert.ToDouble(o);
            if (double.IsNaN(d) || double.IsInfinity(d)) return false;
            result = FormatNumber(d);
            return true;
        }
        catch { return false; }
    }

    private static bool TryPow(string expr, out string result)
    {
        result = "";
        // Minimal a^b support for a single power expression.
        var parts = expr.Split('^');
        if (parts.Length != 2) return false;
        if (double.TryParse(parts[0].Trim(), out var a) && double.TryParse(parts[1].Trim(), out var b))
        {
            double d = Math.Pow(a, b);
            if (double.IsNaN(d) || double.IsInfinity(d)) return false;
            result = FormatNumber(d);
            return true;
        }
        return false;
    }

    private static string FormatNumber(double d)
        => Math.Abs(d - Math.Round(d)) < 1e-9 && Math.Abs(d) < 1e15
            ? ((long)Math.Round(d)).ToString()
            : d.ToString("0.######");

    // ----- Run command / open URL · 執行指令／開網址 -----
    private static void AddRunOrUrl(string query, List<CommandPaletteResult> list)
    {
        var q = query.Trim();
        if (q.StartsWith(">", StringComparison.Ordinal))
        {
            string command = q.Substring(1).Trim();
            if (string.IsNullOrWhiteSpace(command))
            {
                list.Add(new CommandPaletteResult
                {
                    Title = Loc.I.Pick("Command mode", "指令模式"),
                    Subtitle = Loc.I.Pick("Type > followed by a command to run it as the current user.", "輸入 > 再加指令，就會以目前使用者身分執行。"),
                    Glyph = ((char)0xE756).ToString(),
                    ProviderTag = Loc.I.Pick("Run", "執行"),
                    Score = 180,
                    Invoke = () => false,
                });
                return;
            }
            list.Add(new CommandPaletteResult
            {
                Title = Loc.I.Pick($"Run command: {command}", $"執行指令：{command}"),
                Subtitle = Loc.I.Pick("Explicit command mode · runs as the current user", "明確指令模式 · 以目前使用者身分執行"),
                Glyph = ((char)0xE756).ToString(),
                ProviderTag = Loc.I.Pick("Run", "執行"),
                Score = 220,
                Invoke = () => { RunExplicitCommand(command); return true; },
            });
            return;
        }
        bool isUrl = LooksLikeUrl(q);
        if (isUrl)
        {
            var url = q.Contains("://") ? q : "https://" + q;
            list.Add(new CommandPaletteResult
            {
                Title = Loc.I.Pick($"Open {url}", $"開啟 {url}"),
                Subtitle = Loc.I.Pick("Open in your default browser", "用預設瀏覽器開啟"),
                Glyph = ((char)0xE774).ToString(),
                ProviderTag = Loc.I.Pick("URL", "網址"),
                Score = 95,
                Invoke = () => { LaunchPath(url); return true; },
            });
            return;
        }
        // Treat as a Run command if it names an existing path or a well-known executable token.
        var token = q.Split(' ', 2)[0];
        var args = q.Length > token.Length ? q.Substring(token.Length).Trim() : "";
        if (LooksLikeCommand(token))
        {
            list.Add(new CommandPaletteResult
            {
                Title = Loc.I.Pick($"Run: {q}", $"執行：{q}"),
                Subtitle = Loc.I.Pick("Run command (like the Run dialog)", "執行指令（似「執行」對話框）"),
                Glyph = ((char)0xE756).ToString(),
                ProviderTag = Loc.I.Pick("Run", "執行"),
                Score = 60,
                Invoke = () => { RunCommand(token, args); return true; },
            });
        }
    }

    private static bool LooksLikeUrl(string q)
    {
        if (q.Contains(' ')) return false;
        if (q.StartsWith("http://") || q.StartsWith("https://")) return true;
        // bare domain like example.com / docs.microsoft.com/path
        var host = q.Split('/')[0];
        return host.Contains('.') && !host.StartsWith('.') && !host.EndsWith('.')
               && host.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == ':')
               && host.Any(char.IsLetter);
    }

    private static readonly HashSet<string> KnownCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd","powershell","pwsh","notepad","calc","mspaint","explorer","regedit","control","taskmgr",
        "msconfig","services.msc","devmgmt.msc","diskmgmt.msc","wt","code","ping","ipconfig","cmd.exe",
        "winver","cleanmgr","snippingtool","charmap","resmon","perfmon","dxdiag","msinfo32","wmic","sysdm.cpl",
    };

    private static bool LooksLikeCommand(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (KnownCommands.Contains(token)) return true;
        if (token.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || token.EndsWith(".msc", StringComparison.OrdinalIgnoreCase)
            || token.EndsWith(".cpl", StringComparison.OrdinalIgnoreCase)) return true;
        if (token.Contains('\\') || token.Contains('/')) return File.Exists(Environment.ExpandEnvironmentVariables(token));
        return false;
    }

    // ----- Bookmarks · 書籤 -----
    private static void AddBookmarks(string query, List<CommandPaletteResult> list)
    {
        var raw = query.Trim();
        bool mode = raw.Equals("bookmark", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("bookmarks", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("bookmark ", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("bookmarks ", StringComparison.OrdinalIgnoreCase);
        if (!mode) return;

        string needle = raw.StartsWith("bookmarks", StringComparison.OrdinalIgnoreCase) ? raw.Substring("bookmarks".Length).Trim()
            : raw.Substring("bookmark".Length).Trim();
        int rank = 0;
        foreach (var bookmark in Bookmarks)
        {
            double score = string.IsNullOrWhiteSpace(needle) ? 88 - rank
                : Math.Max(Fuzzy(needle, bookmark.Name), Fuzzy(needle, bookmark.Url));
            if (score <= 0) { rank++; continue; }
            list.Add(new CommandPaletteResult
            {
                Title = bookmark.Name,
                Subtitle = bookmark.Url,
                Glyph = ((char)0xE774).ToString(),
                ProviderTag = Loc.I.Pick("Bookmark", "書籤"),
                Score = 150 + score * 0.15,
                Invoke = () => { LaunchPath(bookmark.Url); return true; },
            });
            rank++;
        }
    }

    // ----- Performance metrics · 效能指標 -----
    private static void AddPerformanceMetrics(string query, List<CommandPaletteResult> list)
    {
        var raw = query.Trim();
        bool mode = raw.Equals("perf", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("performance", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("metrics", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("perf ", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("performance ", StringComparison.OrdinalIgnoreCase);
        if (!mode) return;

        var snapshot = PerformanceSnapshotService.Get();
        string cpu = snapshot.CpuPercent is double percent ? $"{percent:0.0}%" : Loc.I.Pick("Sampling...", "正在取樣...");
        AddMetric(list, Loc.I.Pick($"CPU: {cpu}", $"CPU：{cpu}"), Loc.I.Pick("System CPU utilization", "系統 CPU 使用率"), cpu, 190);

        ulong usedMemory = snapshot.TotalPhysicalMemory > snapshot.AvailablePhysicalMemory
            ? snapshot.TotalPhysicalMemory - snapshot.AvailablePhysicalMemory : 0;
        string memory = $"{PerformanceSnapshotService.FormatBytes(usedMemory)} / {PerformanceSnapshotService.FormatBytes(snapshot.TotalPhysicalMemory)}";
        AddMetric(list, Loc.I.Pick($"Memory: {memory}", $"記憶體：{memory}"), Loc.I.Pick("Used / total physical memory", "已用／總實體記憶體"), memory, 185);

        string uptime = PerformanceSnapshotService.FormatUptime(snapshot.Uptime);
        AddMetric(list, Loc.I.Pick($"Uptime: {uptime}", $"運作時間：{uptime}"), Loc.I.Pick("Time since Windows started", "Windows 開機後時間"), uptime, 180);

        if (snapshot.SystemDriveTotal > 0)
        {
            string disk = $"{PerformanceSnapshotService.FormatBytes(snapshot.SystemDriveFree)} free / {PerformanceSnapshotService.FormatBytes(snapshot.SystemDriveTotal)}";
            AddMetric(list, Loc.I.Pick($"System drive: {disk}", $"系統磁碟：{disk}"), snapshot.SystemDriveName, disk, 175);
        }

        list.Add(new CommandPaletteResult
        {
            Title = Loc.I.Pick("Open Task Manager", "開啟工作管理員"),
            Subtitle = Loc.I.Pick("Inspect live processes and performance", "檢查即時程序同效能"),
            Glyph = ((char)0xE9D9).ToString(),
            ProviderTag = Loc.I.Pick("Performance", "效能"),
            Score = 150,
            Invoke = () => { RunCommand("taskmgr.exe", ""); return true; },
        });
    }

    private static void AddMetric(List<CommandPaletteResult> list, string title, string subtitle, string copyValue, double score)
    {
        list.Add(new CommandPaletteResult
        {
            Title = title,
            Subtitle = Loc.I.Pick($"{subtitle} · Press Enter to copy", $"{subtitle} · 按 Enter 複製"),
            Glyph = ((char)0xE9D9).ToString(),
            ProviderTag = Loc.I.Pick("Performance", "效能"),
            Score = score,
            Invoke = () => { CopyText(copyValue); return true; },
        });
    }

    // ----- Remote Desktop · 遠端桌面 -----
    private static void AddRemoteDesktopProfiles(string query, List<CommandPaletteResult> list)
    {
        var raw = query.Trim();
        bool mode = raw.Equals("rdp", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("remote", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("remote desktop", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("rdp ", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("remote ", StringComparison.OrdinalIgnoreCase);
        if (!mode) return;

        string needle = raw.StartsWith("rdp", StringComparison.OrdinalIgnoreCase) ? raw.Substring("rdp".Length).Trim()
            : raw.StartsWith("remote desktop", StringComparison.OrdinalIgnoreCase) ? raw.Substring("remote desktop".Length).Trim()
            : raw.Substring("remote".Length).Trim();
        int rank = 0;
        foreach (var profile in RemoteDesktopProfiles)
        {
            double score = string.IsNullOrWhiteSpace(needle) ? 88 - rank
                : Math.Max(Fuzzy(needle, profile.Name), Fuzzy(needle, profile.Host));
            if (score <= 0) { rank++; continue; }
            list.Add(new CommandPaletteResult
            {
                Title = profile.Name,
                Subtitle = Loc.I.Pick($"{profile.Host} · Windows will request credentials if needed", $"{profile.Host} · 如有需要，Windows 會要求登入資料"),
                Glyph = ((char)0xE7F4).ToString(),
                ProviderTag = Loc.I.Pick("Remote Desktop", "遠端桌面"),
                Score = 155 + score * 0.15,
                Invoke = () => { RunCommand("mstsc.exe", "/v:" + profile.Host); return true; },
            });
            rank++;
        }
    }

    // ----- Window Walker · 視窗切換器 -----
    private static void AddOpenWindows(string query, List<CommandPaletteResult> list)
    {
        var raw = query.Trim();
        bool mode = raw.Equals("window", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("windows", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("win", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("window ", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("windows ", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("win ", StringComparison.OrdinalIgnoreCase);
        if (!mode) return;

        string needle = raw.StartsWith("windows", StringComparison.OrdinalIgnoreCase) ? raw.Substring("windows".Length).Trim()
            : raw.StartsWith("window", StringComparison.OrdinalIgnoreCase) ? raw.Substring("window".Length).Trim()
            : raw.Substring("win".Length).Trim();
        int rank = 0;
        foreach (var window in WindowWalkerService.List())
        {
            double score = string.IsNullOrWhiteSpace(needle) ? 88 - rank
                : Math.Max(Fuzzy(needle, window.Title), Fuzzy(needle, window.ProcessName));
            if (score <= 0) { rank++; continue; }
            list.Add(new CommandPaletteResult
            {
                Title = window.Title,
                Subtitle = Loc.I.Pick($"{window.ProcessName} · Press Enter to switch to this window",
                    $"{window.ProcessName} · 按 Enter 切換到呢個視窗"),
                Glyph = ((char)0xE7F4).ToString(),
                ProviderTag = Loc.I.Pick("Window", "視窗"),
                Score = 145 + score * 0.15,
                Invoke = () => { WindowWalkerService.Activate(window.Handle); return true; },
            });
            rank++;
            if (rank >= 32) break;
        }
    }

    // ----- Files & folders · 檔案與資料夾 -----
    private static void AddFiles(string query, List<CommandPaletteResult> list)
    {
        var q = query.Trim();

        // Direct path entered → offer to open it.
        var expanded = Environment.ExpandEnvironmentVariables(q);
        try
        {
            if ((q.Length > 2 && (q[1] == ':' || q.StartsWith("\\\\") || q.StartsWith("%"))) || q.StartsWith("~"))
            {
                if (q.StartsWith("~")) expanded = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + q.Substring(1);
                if (Directory.Exists(expanded))
                {
                    list.Add(FileResult(expanded, isDir: true, 88));
                    // Also list immediate children matching the trailing segment.
                    foreach (var child in EnumerateChildren(expanded).Take(6))
                        list.Add(FileResult(child.path, child.isDir, 50));
                    return;
                }
                if (File.Exists(expanded)) { list.Add(FileResult(expanded, isDir: false, 88)); return; }

                // Parent exists, partial child name → suggest matches.
                var parent = Path.GetDirectoryName(expanded);
                var leaf = Path.GetFileName(expanded);
                if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                {
                    foreach (var child in EnumerateChildren(parent))
                    {
                        double s = Fuzzy(leaf, Path.GetFileName(child.path));
                        if (s <= 0) continue;
                        var r = FileResult(child.path, child.isDir, 1);
                        r.Score = 40 + s * 0.4;
                        list.Add(r);
                    }
                    return;
                }
            }
        }
        catch { /* ignore path errors */ }

        // Indexed common folders by name.
        foreach (var (name, path) in CommonFolders())
        {
            double s = Fuzzy(q, name);
            if (s <= 0) continue;
            var r = FileResult(path, isDir: true, 1);
            r.Score = s * 0.55; // below apps/modules unless a strong match
            list.Add(r);
        }
    }

    private static IEnumerable<(string path, bool isDir)> EnumerateChildren(string dir)
    {
        IEnumerable<string> dirs = Array.Empty<string>(), files = Array.Empty<string>();
        try { dirs = Directory.EnumerateDirectories(dir); } catch { }
        try { files = Directory.EnumerateFiles(dir); } catch { }
        foreach (var d in dirs) yield return (d, true);
        foreach (var f in files) yield return (f, false);
    }

    private static CommandPaletteResult FileResult(string path, bool isDir, double score) => new()
    {
        Title = Path.GetFileName(path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : path,
        Subtitle = path,
        Glyph = isDir ? ((char)0xE8B7).ToString() : ((char)0xE7C3).ToString(),
        ProviderTag = isDir ? Loc.I.Pick("Folder", "資料夾") : Loc.I.Pick("File", "檔案"),
        Score = score,
        Invoke = () => { LaunchPath(path); return true; },
    };

    private static IEnumerable<(string name, string path)> CommonFolders()
    {
        (string n, Environment.SpecialFolder f)[] specs =
        {
            ("Desktop", Environment.SpecialFolder.Desktop),
            ("Documents", Environment.SpecialFolder.MyDocuments),
            ("Downloads", Environment.SpecialFolder.UserProfile), // adjusted below
            ("Pictures", Environment.SpecialFolder.MyPictures),
            ("Music", Environment.SpecialFolder.MyMusic),
            ("Videos", Environment.SpecialFolder.MyVideos),
            ("AppData", Environment.SpecialFolder.ApplicationData),
            ("Local AppData", Environment.SpecialFolder.LocalApplicationData),
            ("Program Files", Environment.SpecialFolder.ProgramFiles),
            ("Windows", Environment.SpecialFolder.Windows),
            ("User Profile", Environment.SpecialFolder.UserProfile),
            ("Startup", Environment.SpecialFolder.Startup),
            ("Recent", Environment.SpecialFolder.Recent),
            ("Temp", Environment.SpecialFolder.LocalApplicationData), // adjusted below
        };
        foreach (var (n, f) in specs)
        {
            string path;
            if (n == "Downloads") path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            else if (n == "Temp") path = Path.GetTempPath();
            else path = Environment.GetFolderPath(f);
            if (!string.IsNullOrEmpty(path)) yield return (n, path);
        }
    }

    // ----- Clipboard history · 剪貼簿記錄 -----
    private static void AddClipboard(string query, List<CommandPaletteResult> list)
    {
        var raw = query.Trim();
        bool mode = raw.Equals("clip", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("clipboard", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("clip ", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("clipboard ", StringComparison.OrdinalIgnoreCase);
        if (!mode) return;

        string needle = raw.StartsWith("clipboard", StringComparison.OrdinalIgnoreCase)
            ? raw.Substring("clipboard".Length).Trim()
            : raw.Substring("clip".Length).Trim();
        int rank = 0;
        foreach (var item in ClipboardService.History
                     .Where(i => i.Kind == ClipKind.Text && !string.IsNullOrWhiteSpace(i.Text))
                     .Take(16)
                     .ToList())
        {
            string preview = OneLine(item.Text);
            if (preview.Length > 88) preview = preview.Substring(0, 85) + "...";
            double score = string.IsNullOrWhiteSpace(needle) ? 82 - rank : Fuzzy(needle, item.Text);
            if (score <= 0) { rank++; continue; }
            list.Add(new CommandPaletteResult
            {
                Title = preview,
                Subtitle = Loc.I.Pick($"Clipboard item · {item.Time} · Enter restores it to the clipboard",
                    $"剪貼簿項目 · {item.Time} · 按 Enter 還原到剪貼簿"),
                Glyph = ((char)0xE8C1).ToString(),
                ProviderTag = Loc.I.Pick("Clipboard", "剪貼簿"),
                Score = 88 + score * 0.1,
                Invoke = () => { ClipboardService.CopyBack(item); return true; },
            });
            rank++;
        }
    }

    private static string OneLine(string text)
        => (text ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ").Trim();

    // ----- Time & date · 時間與日期 -----
    private static void AddTimeAndDate(string query, List<CommandPaletteResult> list)
    {
        var q = query.Trim();
        if (!(q.Equals("time", StringComparison.OrdinalIgnoreCase)
            || q.Equals("date", StringComparison.OrdinalIgnoreCase)
            || q.Equals("now", StringComparison.OrdinalIgnoreCase)
            || q.StartsWith("time ", StringComparison.OrdinalIgnoreCase)
            || q.StartsWith("date ", StringComparison.OrdinalIgnoreCase)
            || q.StartsWith("timezone", StringComparison.OrdinalIgnoreCase))) return;

        var now = DateTimeOffset.Now;
        string stamp = now.ToString("yyyy-MM-dd HH:mm:ss zzz");
        list.Add(new CommandPaletteResult
        {
            Title = stamp,
            Subtitle = Loc.I.Pick($"{TimeZoneInfo.Local.DisplayName} · Enter copies an ISO timestamp",
                $"{TimeZoneInfo.Local.DisplayName} · 按 Enter 複製 ISO 時間戳記"),
            Glyph = ((char)0xE823).ToString(),
            ProviderTag = Loc.I.Pick("Time & date", "時間與日期"),
            Score = 180,
            Invoke = () => { CopyText(now.ToString("O")); return true; },
        });
        list.Add(new CommandPaletteResult
        {
            Title = Loc.I.Pick("Open Date & time settings", "開啟日期與時間設定"),
            Subtitle = "ms-settings:dateandtime",
            Glyph = ((char)0xE713).ToString(),
            ProviderTag = Loc.I.Pick("Windows Settings", "Windows 設定"),
            Score = 130,
            Invoke = () => { LaunchPath("ms-settings:dateandtime"); return true; },
        });
    }

    // ----- Windows Settings ($ query) · Windows 設定（$ 查詢）-----
    private static readonly (string En, string Zh, string Uri, string[] Keys)[] SettingsPages = new[]
    {
        (En: "Display", Zh: "顯示器", Uri: "ms-settings:display", Keys: new[] { "display", "monitor", "screen", "顯示", "螢幕" }),
        (En: "Sound", Zh: "音效", Uri: "ms-settings:sound", Keys: new[] { "sound", "audio", "volume", "音效", "音量" }),
        (En: "Bluetooth & devices", Zh: "藍牙與裝置", Uri: "ms-settings:bluetooth", Keys: new[] { "bluetooth", "device", "藍牙", "裝置" }),
        (En: "Network & Internet", Zh: "網絡與網際網路", Uri: "ms-settings:network", Keys: new[] { "network", "wifi", "ethernet", "vpn", "網絡", "網路", "無線" }),
        (En: "Personalization", Zh: "個人化", Uri: "ms-settings:personalization", Keys: new[] { "personalization", "theme", "background", "個人化", "主題", "背景" }),
        (En: "Apps", Zh: "應用程式", Uri: "ms-settings:appsfeatures", Keys: new[] { "apps", "installed", "uninstall", "應用", "程式", "解除安裝" }),
        (En: "Power & battery", Zh: "電源與電池", Uri: "ms-settings:powersleep", Keys: new[] { "power", "battery", "sleep", "電源", "電池", "睡眠" }),
        (En: "Storage", Zh: "儲存空間", Uri: "ms-settings:storagesense", Keys: new[] { "storage", "disk", "儲存", "磁碟" }),
        (En: "Windows Update", Zh: "Windows 更新", Uri: "ms-settings:windowsupdate", Keys: new[] { "update", "windows update", "更新" }),
        (En: "Privacy & security", Zh: "私隱與安全性", Uri: "ms-settings:privacy", Keys: new[] { "privacy", "security", "permissions", "私隱", "安全", "權限" }),
        (En: "Accessibility", Zh: "協助工具", Uri: "ms-settings:easeofaccess", Keys: new[] { "accessibility", "ease", "narrator", "協助", "無障礙" }),
    };

    private static void AddWindowsSettings(string query, List<CommandPaletteResult> list)
    {
        var q = query.Trim();
        bool mode = q.StartsWith("$", StringComparison.Ordinal)
            || q.Equals("settings", StringComparison.OrdinalIgnoreCase)
            || q.StartsWith("settings ", StringComparison.OrdinalIgnoreCase)
            || q.Equals("設定", StringComparison.Ordinal)
            || q.StartsWith("設定 ", StringComparison.Ordinal);
        if (!mode) return;

        string needle = q.StartsWith("$", StringComparison.Ordinal) ? q.Substring(1).Trim()
            : q.StartsWith("settings", StringComparison.OrdinalIgnoreCase) ? q.Substring("settings".Length).Trim()
            : q.Substring("設定".Length).Trim();
        foreach (var page in SettingsPages)
        {
            double score = string.IsNullOrWhiteSpace(needle) ? 100 : Math.Max(Fuzzy(needle, page.En), Fuzzy(needle, page.Zh));
            foreach (var key in page.Keys) score = Math.Max(score, Fuzzy(needle, key));
            if (score <= 0) continue;
            list.Add(new CommandPaletteResult
            {
                Title = $"{page.En} · {page.Zh}",
                Subtitle = page.Uri,
                Glyph = ((char)0xE713).ToString(),
                ProviderTag = Loc.I.Pick("Windows Settings", "Windows 設定"),
                Score = 150 + score * 0.2,
                Invoke = () => { LaunchPath(page.Uri); return true; },
            });
        }
    }

    // ----- Windows services · Windows 服務 -----
    private static readonly object ServiceCacheGate = new();
    private static List<ServiceInfo> _serviceCache = new();
    private static DateTime _serviceCacheUpdated = DateTime.MinValue;
    private static int _serviceRefreshInProgress;

    private static void EnsureServiceCache(bool force = false)
    {
        lock (ServiceCacheGate)
        {
            if (!force && _serviceCache.Count > 0 && DateTime.UtcNow - _serviceCacheUpdated < TimeSpan.FromMinutes(2)) return;
        }
        if (System.Threading.Interlocked.CompareExchange(ref _serviceRefreshInProgress, 1, 0) != 0) return;
        _ = Task.Run(async () =>
        {
            try
            {
                var services = await ServiceManager.ListAsync();
                lock (ServiceCacheGate)
                {
                    _serviceCache = services;
                    _serviceCacheUpdated = DateTime.UtcNow;
                }
            }
            catch { }
            finally { System.Threading.Interlocked.Exchange(ref _serviceRefreshInProgress, 0); }
        });
    }

    private static void AddWindowsServices(string query, List<CommandPaletteResult> list)
    {
        if (!TryParseServiceQuery(query.Trim(), out var action, out var needle)) return;
        EnsureServiceCache();
        List<ServiceInfo> services;
        lock (ServiceCacheGate) services = _serviceCache.ToList();
        if (services.Count == 0)
        {
            list.Add(new CommandPaletteResult
            {
                Title = Loc.I.Pick("Loading Windows services...", "正在載入 Windows 服務..."),
                Subtitle = Loc.I.Pick("Keep typing or run the query again in a moment.", "稍候繼續輸入或再次執行查詢。"),
                Glyph = ((char)0xE895).ToString(),
                ProviderTag = Loc.I.Pick("Windows services", "Windows 服務"),
                Score = 100,
                Invoke = () => false,
            });
            return;
        }

        int rank = 0;
        foreach (var service in services.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            double score = string.IsNullOrWhiteSpace(needle) ? 80 - rank
                : Math.Max(Fuzzy(needle, service.DisplayName), Fuzzy(needle, service.Name));
            if (score <= 0) { rank++; continue; }
            string actionEn = action switch { "start" => "Start", "stop" => "Stop", "restart" => "Restart", _ => "" };
            string actionZh = action switch { "start" => "啟動", "stop" => "停止", "restart" => "重新啟動", _ => "" };
            list.Add(new CommandPaletteResult
            {
                Title = string.IsNullOrEmpty(action) ? $"{service.DisplayName} · {service.Name}" : $"{actionEn} {service.DisplayName} · {actionZh}",
                Subtitle = string.IsNullOrEmpty(action)
                    ? Loc.I.Pick($"{service.State} · Enter opens Windows Services", $"{service.State} · 按 Enter 開啟 Windows 服務")
                    : Loc.I.Pick($"{service.State} · Enter {actionEn.ToLowerInvariant()}s this service", $"{service.State} · 按 Enter {actionZh}呢個服務"),
                Glyph = ((char)0xE9F6).ToString(),
                ProviderTag = Loc.I.Pick("Windows services", "Windows 服務"),
                Score = 120 + score * 0.15,
                Invoke = () =>
                {
                    if (string.IsNullOrEmpty(action)) RunCommand("mmc.exe", "services.msc");
                    else RunServiceAction(action, service.Name);
                    return true;
                },
            });
            rank++;
            if (rank >= 24) break;
        }
    }

    private static bool TryParseServiceQuery(string query, out string action, out string needle)
    {
        action = "";
        needle = "";
        string rest;
        if (query.Equals("service", StringComparison.OrdinalIgnoreCase) || query.Equals("services", StringComparison.OrdinalIgnoreCase) || query.Equals("svc", StringComparison.OrdinalIgnoreCase)) rest = "";
        else if (query.StartsWith("services ", StringComparison.OrdinalIgnoreCase)) rest = query.Substring("services".Length).Trim();
        else if (query.StartsWith("service ", StringComparison.OrdinalIgnoreCase)) rest = query.Substring("service".Length).Trim();
        else if (query.StartsWith("svc ", StringComparison.OrdinalIgnoreCase)) rest = query.Substring("svc".Length).Trim();
        else return false;

        foreach (var candidate in new[] { "start", "stop", "restart" })
        {
            if (rest.Equals(candidate, StringComparison.OrdinalIgnoreCase) || rest.StartsWith(candidate + " ", StringComparison.OrdinalIgnoreCase))
            {
                action = candidate;
                rest = rest.Substring(candidate.Length).Trim();
                break;
            }
        }
        needle = rest;
        return true;
    }

    private static void RunServiceAction(string action, string serviceName)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                switch (action)
                {
                    case "start": await ServiceManager.Start(serviceName); break;
                    case "stop": await ServiceManager.Stop(serviceName); break;
                    case "restart": await ServiceManager.Restart(serviceName); break;
                }
            }
            catch { }
            finally { EnsureServiceCache(force: true); }
        });
    }

    // ----- Windows Terminal profiles · Windows 終端機設定檔 -----
    private static void AddTerminalProfiles(string query, List<CommandPaletteResult> list)
    {
        var q = query.Trim();
        bool mode = q.Equals("terminal", StringComparison.OrdinalIgnoreCase)
            || q.Equals("wt", StringComparison.OrdinalIgnoreCase)
            || q.StartsWith("terminal ", StringComparison.OrdinalIgnoreCase)
            || q.StartsWith("wt ", StringComparison.OrdinalIgnoreCase);
        if (!mode) return;

        string needle = q.StartsWith("terminal", StringComparison.OrdinalIgnoreCase) ? q.Substring("terminal".Length).Trim()
            : q.Substring("wt".Length).Trim();
        string? executable = WindowsTerminalService.ResolveWtExe();
        if (string.IsNullOrWhiteSpace(executable))
        {
            list.Add(new CommandPaletteResult
            {
                Title = Loc.I.Pick("Windows Terminal is not available", "Windows 終端機未可用"),
                Subtitle = Loc.I.Pick("Install Windows Terminal to launch its profiles from Command Palette.", "安裝 Windows 終端機後，就可以喺指令面板啟動設定檔。"),
                Glyph = ((char)0xE756).ToString(),
                ProviderTag = Loc.I.Pick("Terminal profiles", "終端機設定檔"),
                Score = 100,
                Invoke = () => false,
            });
            return;
        }

        try
        {
            string? settingsPath = WindowsTerminalService.Resolve();
            var profiles = string.IsNullOrWhiteSpace(settingsPath)
                ? new List<WtProfile>()
                : WindowsTerminalService.Profiles(WindowsTerminalService.Load(settingsPath));
            if (profiles.Count == 0)
            {
                list.Add(new CommandPaletteResult
                {
                    Title = Loc.I.Pick("Open Windows Terminal", "開啟 Windows 終端機"),
                    Subtitle = Loc.I.Pick("No saved profiles were found.", "搵唔到已儲存設定檔。"),
                    Glyph = ((char)0xE756).ToString(),
                    ProviderTag = Loc.I.Pick("Terminal profiles", "終端機設定檔"),
                    Score = 120,
                    Invoke = () => { LaunchTerminalProfile(executable, null); return true; },
                });
                return;
            }

            int rank = 0;
            foreach (var profile in profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Name)) continue;
                double score = string.IsNullOrWhiteSpace(needle) ? 80 - rank : Fuzzy(needle, profile.Name);
                if (score <= 0) { rank++; continue; }
                string profileName = profile.Name;
                list.Add(new CommandPaletteResult
                {
                    Title = profileName,
                    Subtitle = Loc.I.Pick("Press Enter to launch this Windows Terminal profile", "按 Enter 啟動呢個 Windows 終端機設定檔"),
                    Glyph = ((char)0xE756).ToString(),
                    ProviderTag = Loc.I.Pick("Terminal profile", "終端機設定檔"),
                    Score = 135 + score * 0.15,
                    Invoke = () => { LaunchTerminalProfile(executable, profileName); return true; },
                });
                rank++;
                if (rank >= 24) break;
            }
        }
        catch { }
    }

    private static void LaunchTerminalProfile(string executable, string? profileName)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = WindowsTerminalService.BuildLaunchArgs(profileName, null, newTab: true),
                UseShellExecute = true,
            });
        }
        catch { }
    }

    // ----- System actions · 系統動作 -----
    private static void AddSystem(string query, List<CommandPaletteResult> list)
    {
        foreach (var act in SystemActions())
        {
            double s = Math.Max(Fuzzy(query, act.en), Fuzzy(query, act.zh));
            // Also match against keyword aliases.
            foreach (var k in act.keys) s = Math.Max(s, Fuzzy(query, k));
            if (s <= 0) continue;
            list.Add(new CommandPaletteResult
            {
                Title = $"{act.en} · {act.zh}",
                Subtitle = Loc.I.Pick("System action", "系統動作"),
                Glyph = act.glyph,
                ProviderTag = Loc.I.Pick("System", "系統"),
                Score = s * 0.95,
                Invoke = () => { act.run(); return true; },
            });
        }
    }

    private static IEnumerable<(string en, string zh, string glyph, string[] keys, Action run)> SystemActions()
    {
        yield return ("Lock", "鎖定", ((char)0xE72E).ToString(), new[] { "lock", "鎖定", "鎖屏" },
            () => RunCommand("rundll32.exe", "user32.dll,LockWorkStation"));
        yield return ("Sign out", "登出", ((char)0xF3B1).ToString(), new[] { "logoff", "signout", "登出" },
            () => RunCommand("shutdown.exe", "/l"));
        yield return ("Sleep", "睡眠", ((char)0xEC46).ToString(), new[] { "sleep", "睡眠", "瞓" },
            () => RunCommand("rundll32.exe", "powrprof.dll,SetSuspendState 0,1,0"));
        yield return ("Restart", "重新啟動", ((char)0xE777).ToString(), new[] { "restart", "reboot", "重啟", "重新開機" },
            () => RunCommand("shutdown.exe", "/r /t 0"));
        yield return ("Shut down", "關機", ((char)0xE7E8).ToString(), new[] { "shutdown", "poweroff", "關機" },
            () => RunCommand("shutdown.exe", "/s /t 0"));
        yield return ("Hibernate", "休眠", ((char)0xE708).ToString(), new[] { "hibernate", "休眠" },
            () => RunCommand("shutdown.exe", "/h"));
        yield return ("Empty Recycle Bin", "清空回收筒", ((char)0xE74D).ToString(), new[] { "recycle", "empty", "trash", "回收筒", "垃圾桶", "清空" },
            EmptyRecycleBin);
        yield return ("Open WinForge", "開啟 WinForge", ((char)0xE80F).ToString(), new[] { "winforge", "shell", "home", "首頁" },
            ShowShell);
    }

    [DllImport("shell32.dll")]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private static void EmptyRecycleBin()
    {
        // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND
        try { SHEmptyRecycleBin(IntPtr.Zero, null, 0x00000001 | 0x00000002 | 0x00000004); } catch { }
    }

    // ----- Web search fallback · 網絡搜尋（後備）-----
    private static void AddWeb(string query, List<CommandPaletteResult> list)
    {
        var q = query.Trim();
        if (q.Length < 2) return;
        var encoded = Uri.EscapeDataString(q);
        list.Add(new CommandPaletteResult
        {
            Title = Loc.I.Pick($"Search the web for \"{q}\"", $"喺網上搜尋「{q}」"),
            Subtitle = Loc.I.Pick("Copy the web-search URL", "複製網絡搜尋網址"),
            Glyph = ((char)0xE721).ToString(),
            ProviderTag = Loc.I.Pick("Web", "網絡"),
            Score = 10, // always last unless nothing else matches
            Invoke = () => { CopyText("https://www.bing.com/search?q=" + encoded); return true; },
        });
    }

    // ===================== Launch helpers · 啟動輔助 =====================

    private static void LaunchPath(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (path.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true,
                };
                Process.Start(psi);
                return;
            }

            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { /* best effort */ }
    }

    private static void RunCommand(string file, string args)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(file)) return;
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args ?? "",
                UseShellExecute = true,
            };
            Process.Start(psi);
        }
        catch { /* best effort */ }
    }

    private static void RunExplicitCommand(string command)
    {
        try
        {
            if (command.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                LaunchPath(command);
                return;
            }
            string comSpec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            Process.Start(new ProcessStartInfo
            {
                FileName = comSpec,
                Arguments = "/d /s /c " + command,
                UseShellExecute = true,
            });
        }
        catch { /* explicit, best-effort user command */ }
    }

    private static void CopyText(string text)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
        catch { }
    }

    private static void ShowShell()
    {
        try
        {
            if (App.Shell is { } w)
            {
                w.AppWindow.Show();
                w.Activate();
            }
        }
        catch { }
    }

    // ----- UWP apps via AppsFolder · 透過 AppsFolder 列出 UWP 程式 -----
    private static IEnumerable<(string name, string aumid)> UwpApps()
    {
        // Enumerated from the per-user Start Apps registry-free way: read packaged apps from
        // %LOCALAPPDATA% manifests is heavy; instead we use the AppsFolder via shell:::{4234d49b...}.
        // For robustness without COM shell enumeration, parse the App Paths is insufficient for UWP,
        // so we surface common known UWP AUMIDs that are typically present. .lnk enumeration already
        // covers Win32 + most UWP that ship a Start tile; this list adds frequent store apps.
        var common = new (string name, string aumid)[]
        {
            ("Settings", "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel"),
            ("Calculator", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
            ("Microsoft Store", "Microsoft.WindowsStore_8wekyb3d8bbwe!App"),
            ("Photos", "Microsoft.Windows.Photos_8wekyb3d8bbwe!App"),
            ("Camera", "Microsoft.WindowsCamera_8wekyb3d8bbwe!App"),
            ("Snipping Tool", "Microsoft.ScreenSketch_8wekyb3d8bbwe!App"),
            ("Terminal", "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"),
            ("Paint", "Microsoft.Paint_8wekyb3d8bbwe!App"),
            ("Notepad", "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"),
            ("Clock", "Microsoft.WindowsAlarms_8wekyb3d8bbwe!App"),
        };
        foreach (var c in common) yield return c;
    }
}
