using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 工作區服務（PowerToys Workspaces 式原生複製）· Workspaces service — a native clone of
/// PowerToys Workspaces. 擷取目前桌面上嘅應用程式視窗（exe 路徑、標題、位置／大小、顯示器、
/// 最大化／最小化狀態），存做有名工作區（JSON via SettingsStore），再可以重新啟動每個 app
/// 並且移動／改變新視窗大小返去儲存嘅範圍。
/// Captures the current desktop's app windows (exe path, title, position/size, monitor,
/// maximized/minimized state), saves them as named workspaces (JSON via SettingsStore), and can
/// relaunch each app then move/resize the new window back to the saved bounds. Pure Win32 + WMI,
/// no external tool.
/// </summary>
public static class WorkspacesService
{
    private const string Key = "workspaces.list";
    private static readonly object Gate = new();
    private static readonly List<Workspace> _all = new();

    /// <summary>清單一有改動就觸發 · Raised after any mutation to the workspace list.</summary>
    public static event EventHandler? Changed;

    static WorkspacesService() => Load();

    // ---------------------------------------------------------------- model

    /// <summary>記憶體中嘅工作區清單（只讀副本）· The in-memory workspace list (read-only copy).</summary>
    public static IReadOnlyList<Workspace> All
    {
        get { lock (Gate) return _all.ToArray(); }
    }

    public static Workspace? Get(string id)
    {
        lock (Gate) return _all.FirstOrDefault(w => w.Id == id);
    }

    // ---------------------------------------------------------------- persistence

    private static void Load()
    {
        lock (Gate)
        {
            _all.Clear();
            try
            {
                var json = SettingsStore.Get(Key, string.Empty);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonSerializer.Deserialize<List<Workspace>>(json);
                    if (loaded is not null)
                        foreach (var w in loaded)
                            if (w is not null && !string.IsNullOrWhiteSpace(w.Id))
                                _all.Add(w);
                }
            }
            catch { /* 損壞就當空 · ignore corrupt value */ }
        }
    }

    private static void SaveLocked()
    {
        try
        {
            SettingsStore.Set(Key, JsonSerializer.Serialize(_all,
                new JsonSerializerOptions { WriteIndented = false }));
        }
        catch { /* best effort */ }
    }

    private static void Persist()
    {
        lock (Gate) SaveLocked();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    // ---------------------------------------------------------------- mutations

    /// <summary>擷取目前桌面並存做新工作區 · Capture the current desktop and save it as a new workspace.</summary>
    public static Workspace CaptureNew(string name)
    {
        var ws = new Workspace
        {
            Name = string.IsNullOrWhiteSpace(name) ? DefaultName() : name.Trim(),
            Apps = CaptureApps(),
        };
        lock (Gate) { _all.Add(ws); SaveLocked(); }
        Changed?.Invoke(null, EventArgs.Empty);
        return ws;
    }

    /// <summary>用目前桌面重新擷取一個已存在嘅工作區 · Re-capture an existing workspace from the current desktop.</summary>
    public static void Recapture(string id)
    {
        lock (Gate)
        {
            var ws = _all.FirstOrDefault(w => w.Id == id);
            if (ws is null) return;
            ws.Apps = CaptureApps();
            SaveLocked();
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void Rename(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        lock (Gate)
        {
            var ws = _all.FirstOrDefault(w => w.Id == id);
            if (ws is null) return;
            ws.Name = name.Trim();
            SaveLocked();
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static void Delete(string id)
    {
        bool removed;
        lock (Gate)
        {
            removed = _all.RemoveAll(w => w.Id == id) > 0;
            if (removed) SaveLocked();
        }
        if (removed) Changed?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>儲存對工作區嘅編輯（app 啟用／停用、範圍、移除）· Persist edits made to a workspace's apps.</summary>
    public static void Save(Workspace edited)
    {
        if (edited is null || string.IsNullOrWhiteSpace(edited.Id)) return;
        lock (Gate)
        {
            var idx = _all.FindIndex(w => w.Id == edited.Id);
            if (idx < 0) return;
            _all[idx] = edited;
            SaveLocked();
        }
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static string DefaultName()
    {
        lock (Gate)
        {
            var baseName = "Workspace";
            int n = _all.Count + 1;
            string candidate;
            do { candidate = $"{baseName} {n++}"; }
            while (_all.Any(w => string.Equals(w.Name, candidate, StringComparison.OrdinalIgnoreCase)));
            return candidate;
        }
    }

    // ---------------------------------------------------------------- import / export

    /// <summary>匯出單一工作區做 JSON 檔 · Export one workspace to a JSON file.</summary>
    public static void ExportTo(string id, string path)
    {
        var ws = Get(id);
        if (ws is null) return;
        File.WriteAllText(path, JsonSerializer.Serialize(ws, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>由 JSON 檔匯入一個工作區（會配新 Id）· Import a workspace from a JSON file (assigns a fresh Id).</summary>
    public static Workspace? ImportFrom(string path)
    {
        try
        {
            var ws = JsonSerializer.Deserialize<Workspace>(File.ReadAllText(path));
            if (ws is null) return null;
            ws.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(ws.Name)) ws.Name = DefaultName();
            ws.Apps ??= new List<WorkspaceApp>();
            lock (Gate) { _all.Add(ws); SaveLocked(); }
            Changed?.Invoke(null, EventArgs.Empty);
            return ws;
        }
        catch { return null; }
    }

    // ================================================================ capture

    /// <summary>
    /// 列舉目前桌面上可見嘅頂層視窗，過濾走 shell／隱形視窗，組成工作區 app 清單。
    /// Enumerate visible top-level windows, filter out shell/invisible ones, build the app list.
    /// </summary>
    public static List<WorkspaceApp> CaptureApps()
    {
        var apps = new List<WorkspaceApp>();
        var monitors = MonitorRects();

        EnumWindows((h, _) =>
        {
            try
            {
                if (!IsWindowVisible(h)) return true;
                if (GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;       // skip owned dialogs
                long ex = (long)GetWindowLong(h, GWL_EXSTYLE);
                if ((ex & WS_EX_TOOLWINDOW) != 0) return true;                // skip tool windows
                int len = GetWindowTextLength(h);
                if (len == 0) return true;
                var sb = new StringBuilder(len + 1);
                GetWindowText(h, sb, sb.Capacity);
                var title = sb.ToString();
                if (string.IsNullOrWhiteSpace(title)) return true;

                GetWindowThreadProcessId(h, out uint pid);
                if (pid == 0) return true;

                Process? proc = null;
                try { proc = Process.GetProcessById((int)pid); } catch { }
                if (proc is null) return true;
                if (proc.ProcessName.Equals("WinForge", StringComparison.OrdinalIgnoreCase)) return true;
                // skip the shell itself and common system surfaces
                var pname = proc.ProcessName;
                if (IsShellProcess(pname, title)) return true;

                string exe = ProcessPath(pid);
                if (string.IsNullOrEmpty(exe)) return true;

                // placement / bounds / state
                var (x, y, w, hgt, state) = ReadPlacement(h);
                if (w <= 0 || hgt <= 0) return true;

                apps.Add(new WorkspaceApp
                {
                    ExePath = exe,
                    Args = CommandLineArgs(pid, exe),
                    Title = title,
                    ProcessName = pname,
                    X = x, Y = y, W = w, H = hgt,
                    Monitor = MonitorOf(monitors, x + w / 2, y + hgt / 2),
                    State = state,
                    Enabled = true,
                    DisplayName = LeafName(exe),
                });
            }
            catch { /* skip a problematic window */ }
            return true;
        }, IntPtr.Zero);

        return apps;
    }

    private static bool IsShellProcess(string pname, string title)
    {
        // explorer hosts the desktop, taskbar and Start; skip its non-file-window surfaces.
        if (pname.Equals("explorer", StringComparison.OrdinalIgnoreCase))
        {
            // "Program Manager" is the desktop; empty taskbar surfaces filtered by title already.
            if (title.Equals("Program Manager", StringComparison.OrdinalIgnoreCase)) return true;
        }
        string[] system =
        {
            "ApplicationFrameHost", "ShellExperienceHost", "SearchHost", "SearchApp",
            "StartMenuExperienceHost", "TextInputHost", "SystemSettings", "LockApp",
            "PeopleExperienceHost", "WidgetBoard", "Widgets", "dwm",
        };
        return system.Any(s => pname.Equals(s, StringComparison.OrdinalIgnoreCase));
    }

    private static (int x, int y, int w, int h, string state) ReadPlacement(IntPtr h)
    {
        var wp = new WINDOWPLACEMENT { length = Marshal.SizeOf<WINDOWPLACEMENT>() };
        string state = "normal";
        if (GetWindowPlacement(h, ref wp))
        {
            if (wp.showCmd == SW_SHOWMINIMIZED) state = "minimized";
            else if (wp.showCmd == SW_SHOWMAXIMIZED) state = "maximized";
        }
        // Use the actual on-screen rect for normal windows; the restore rect for max/min.
        if (state == "normal" && GetWindowRect(h, out RECT r))
            return (r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top, state);

        var nr = wp.rcNormalPosition;
        return (nr.Left, nr.Top, nr.Right - nr.Left, nr.Bottom - nr.Top, state);
    }

    /// <summary>由 PID 攞完整 exe 路徑 · Full executable path from a PID (QueryFullProcessImageName).</summary>
    private static string ProcessPath(uint pid)
    {
        IntPtr hp = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hp == IntPtr.Zero) return TryMainModule(pid);
        try
        {
            int cap = 1024;
            var sb = new StringBuilder(cap);
            if (QueryFullProcessImageName(hp, 0, sb, ref cap))
                return sb.ToString();
        }
        catch { }
        finally { CloseHandle(hp); }
        return TryMainModule(pid);
    }

    private static string TryMainModule(uint pid)
    {
        try { return Process.GetProcessById((int)pid).MainModule?.FileName ?? string.Empty; }
        catch { return string.Empty; }
    }

    /// <summary>
    /// 盡力由 WMI 攞命令列引數 · Best-effort command-line arguments via WMI (Win32_Process).
    /// 攞唔到（權限／WMI 不可用）就回傳空字串。Returns "" if unavailable.
    /// </summary>
    private static string CommandLineArgs(uint pid, string exe)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {pid}");
            foreach (ManagementObject mo in searcher.Get())
            {
                var cl = mo["CommandLine"] as string;
                if (string.IsNullOrWhiteSpace(cl)) return string.Empty;
                return StripExeFromCommandLine(cl, exe);
            }
        }
        catch { /* WMI not available / access denied -> no args */ }
        return string.Empty;
    }

    /// <summary>由完整命令列去走可執行檔本身，淨返引數 · Strip the exe token, leaving just the arguments.</summary>
    private static string StripExeFromCommandLine(string commandLine, string exe)
    {
        var cl = commandLine.Trim();
        // Quoted exe: "C:\...\app.exe" args
        if (cl.StartsWith("\"", StringComparison.Ordinal))
        {
            int end = cl.IndexOf('"', 1);
            if (end > 0) return cl[(end + 1)..].Trim();
        }
        // Unquoted: try matching the known exe path/leaf at the start.
        var leaf = LeafName(exe);
        int idx = cl.IndexOf(leaf, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            int after = idx + leaf.Length;
            if (after <= cl.Length) return cl[after..].Trim();
        }
        // Fallback: drop the first whitespace-delimited token.
        int sp = cl.IndexOf(' ');
        return sp > 0 ? cl[(sp + 1)..].Trim() : string.Empty;
    }

    private static string LeafName(string path)
    {
        try { return Path.GetFileName(path); } catch { return path; }
    }

    // ================================================================ launch / arrange

    /// <summary>
    /// 啟動工作區：對每個啟用嘅 app 用 Process.Start，再等新視窗出現並移動／改大細返去儲存範圍。
    /// Launch a workspace: Process.Start each enabled app, then wait for its new window and move/
    /// resize it back to the saved bounds. Best-effort matching by process name and title.
    /// 回傳 (已啟動, 已定位) 數目。Returns (launched, positioned) counts.
    /// </summary>
    public static async Task<(int launched, int positioned, List<string> errors)> LaunchAsync(
        string id, CancellationToken ct = default)
    {
        var ws = Get(id);
        var errors = new List<string>();
        if (ws is null) return (0, 0, errors);

        int launched = 0, positioned = 0;

        foreach (var app in ws.Apps.Where(a => a.Enabled))
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(app.ExePath) || !File.Exists(app.ExePath))
            {
                errors.Add($"{app.DisplayName}: missing exe · 搵唔到程式");
                continue;
            }

            // Snapshot windows already owned by this exe so we can detect the new one.
            var before = WindowsForExe(app.ExePath);

            int startedPid;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = app.ExePath,
                    UseShellExecute = true,
                    WorkingDirectory = SafeDir(app.ExePath),
                };
                if (!string.IsNullOrWhiteSpace(app.Args)) psi.Arguments = app.Args;
                var p = Process.Start(psi);
                startedPid = p?.Id ?? 0;
                launched++;
            }
            catch (Exception ex)
            {
                errors.Add($"{app.DisplayName}: {ex.Message}");
                continue;
            }

            // Wait (best-effort) for a brand-new window from this exe, then arrange it.
            var hwnd = await WaitForNewWindowAsync(app.ExePath, before, ct).ConfigureAwait(false);
            if (hwnd != IntPtr.Zero)
            {
                if (Arrange(hwnd, app)) positioned++;
            }
        }

        // stamp last-launched
        lock (Gate)
        {
            var w = _all.FirstOrDefault(x => x.Id == id);
            if (w is not null) { w.LastLaunchedTicks = DateTime.UtcNow.Ticks; SaveLocked(); }
        }
        Changed?.Invoke(null, EventArgs.Empty);

        return (launched, positioned, errors);
    }

    private static string SafeDir(string exe)
    {
        try { return Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory; }
        catch { return Environment.CurrentDirectory; }
    }

    private static async Task<IntPtr> WaitForNewWindowAsync(
        string exe, HashSet<IntPtr> before, CancellationToken ct)
    {
        // Poll for up to ~8 seconds for a new top-level window from this exe.
        for (int i = 0; i < 40; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(200, ct).ConfigureAwait(false);
            foreach (var h in WindowsForExe(exe))
                if (!before.Contains(h) && IsWindowVisible(h) && GetWindowTextLength(h) > 0)
                    return h;
        }
        // Fall back to any existing visible window for this exe (e.g. single-instance apps).
        return WindowsForExe(exe).FirstOrDefault(h => IsWindowVisible(h) && GetWindowTextLength(h) > 0);
    }

    private static HashSet<IntPtr> WindowsForExe(string exe)
    {
        var set = new HashSet<IntPtr>();
        var leaf = LeafName(exe);
        EnumWindows((h, _) =>
        {
            try
            {
                if (GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;
                GetWindowThreadProcessId(h, out uint pid);
                if (pid == 0) return true;
                var path = ProcessPath(pid);
                if (!string.IsNullOrEmpty(path) &&
                    (string.Equals(path, exe, StringComparison.OrdinalIgnoreCase) ||
                     LeafName(path).Equals(leaf, StringComparison.OrdinalIgnoreCase)))
                    set.Add(h);
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return set;
    }

    /// <summary>將一個視窗移動／改大細／設定狀態到儲存嘅設定 · Move/resize/state a window to the saved layout.</summary>
    private static bool Arrange(IntPtr h, WorkspaceApp app)
    {
        try
        {
            switch (app.State)
            {
                case "maximized":
                    ShowWindow(h, SW_RESTORE);
                    SetWindowPos(h, IntPtr.Zero, app.X, app.Y, Math.Max(app.W, 100), Math.Max(app.H, 100),
                        SWP_NOZORDER | SWP_NOACTIVATE);
                    ShowWindow(h, SW_MAXIMIZE);
                    break;
                case "minimized":
                    SetWindowPos(h, IntPtr.Zero, app.X, app.Y, Math.Max(app.W, 100), Math.Max(app.H, 100),
                        SWP_NOZORDER | SWP_NOACTIVATE);
                    ShowWindow(h, SW_MINIMIZE);
                    break;
                default:
                    ShowWindow(h, SW_RESTORE);
                    SetWindowPos(h, IntPtr.Zero, app.X, app.Y, Math.Max(app.W, 100), Math.Max(app.H, 100),
                        SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    break;
            }
            return true;
        }
        catch { return false; }
    }

    // ================================================================ monitors

    private static List<RECT> MonitorRects()
    {
        var list = new List<RECT>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr m, IntPtr hdc, ref RECT r, IntPtr d) =>
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(m, ref mi)) list.Add(mi.rcMonitor);
            else list.Add(r);
            return true;
        }, IntPtr.Zero);
        return list;
    }

    private static int MonitorOf(List<RECT> monitors, int x, int y)
    {
        for (int i = 0; i < monitors.Count; i++)
        {
            var r = monitors[i];
            if (x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom) return i;
        }
        return 0;
    }

    // ================================================================ P/Invoke

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprc, IntPtr data);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr h, StringBuilder s, int max);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] private static extern bool GetWindowPlacement(IntPtr h, ref WINDOWPLACEMENT wp);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);

    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr h, int flags, StringBuilder buffer, ref int size);

    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public int dwFlags; }

    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080;
    private const uint GW_OWNER = 4;
    private const int SW_RESTORE = 9, SW_MAXIMIZE = 3, SW_MINIMIZE = 6;
    private const int SW_SHOWMINIMIZED = 2, SW_SHOWMAXIMIZED = 3;
    private const uint SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
}
