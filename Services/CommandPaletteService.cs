using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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

    // ===================== Provider identity · 提供者識別 =====================
    public enum Provider { Apps, Modules, Files, Calculator, Run, System, Web }

    public static IReadOnlyList<Provider> AllProviders { get; } = new[]
    {
        Provider.Apps, Provider.Modules, Provider.Files,
        Provider.Calculator, Provider.Run, Provider.System, Provider.Web,
    };

    /// <summary>提供者嘅雙語名 · Bilingual display name for a provider.</summary>
    public static (string En, string Zh) ProviderName(Provider p) => p switch
    {
        Provider.Apps => ("Installed apps", "已安裝程式"),
        Provider.Modules => ("WinForge modules", "WinForge 模組"),
        Provider.Files => ("Files & folders", "檔案與資料夾"),
        Provider.Calculator => ("Calculator", "計算機"),
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
        if (Enabled) HookPump.Post(InstallHotkey);
    }

    /// <summary>重新套用設定（啟用狀態／熱鍵改變後）· Re-apply settings after the user toggles or rebinds.</summary>
    public static void Reapply()
    {
        HookPump.Post(() =>
        {
            RemoveHotkey();
            if (Enabled) InstallHotkey();
        });
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
        if (IsProviderEnabled(Provider.Modules)) AddModules(query, results);
        if (IsProviderEnabled(Provider.Run)) AddRunOrUrl(query, results);
        if (IsProviderEnabled(Provider.Files)) AddFiles(query, results);
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
