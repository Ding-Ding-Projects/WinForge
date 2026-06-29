using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// ─────────────────────────────────────────────────────────────────────────────
//  winforge-shot · WinForge 驅動與截圖工具
//  A small C# tool to drive the WinForge desktop app and capture screenshots.
//
//  Why this exists: the PowerShell driver used CopyFromScreen, which grabs whatever
//  pixels are physically on screen — so an overlapping window (e.g. a Unity editor)
//  gets captured instead of WinForge. This tool uses PrintWindow with
//  PW_RENDERFULLCONTENT, which asks the window to render ITS OWN pixels into a
//  bitmap, so the capture is correct even when WinForge is occluded or in the
//  background. It also force-foregrounds the window first (best effort).
//
//  Usage:
//    winforge-shot --page <alias> --out <file.png> [--wait <ms>] [--exe <path>]
//                  [--attach] [--keep-open] [--list-windows]
// ─────────────────────────────────────────────────────────────────────────────

// ── parse args ──
var o = new Opts();
for (int i = 0; i < args.Length; i++)
{
    string a = args[i];
    string Next() => i + 1 < args.Length ? args[++i] : "";
    switch (a.ToLowerInvariant())
    {
        case "--page": o.Page = Next(); break;
        case "--out": o.Out = Next(); break;
        case "--wait": int.TryParse(Next(), out o.Wait); break;
        case "--exe": o.Exe = Next(); break;
        case "--attach": o.Attach = true; break;
        case "--keep-open": o.KeepOpen = true; break;
        case "--list-windows": o.ListWindows = true; break;
        case "-h": case "--help":
            Console.WriteLine("winforge-shot --page <alias> --out <file.png> [--wait ms] [--exe path] [--attach] [--keep-open] [--list-windows]");
            return 0;
    }
}

if (o.ListWindows)
{
    foreach (var (h, t, pid) in FindWinForgeWindows()) Console.WriteLine($"hwnd=0x{h:X}  pid={pid}  title=\"{t}\"");
    return 0;
}

// ── locate / launch ──
Process? launched = null;
bool wasRunning = Process.GetProcessesByName("WinForge").Length > 0;

if (!o.Attach)
{
    string? exe = o.Exe ?? FindExe();
    if (exe is null && !wasRunning)
        return Fail("WinForge.exe not found. Build a self-contained publish first, or pass --exe / --attach.");
    if (exe is not null)
    {
        var psi = new ProcessStartInfo { FileName = exe, UseShellExecute = true };
        if (!string.IsNullOrEmpty(o.Page)) { psi.ArgumentList.Add("--page"); psi.ArgumentList.Add(o.Page!); }
        launched = Process.Start(psi);
        Console.WriteLine($"Launched: {exe} {(o.Page is null ? "" : "--page " + o.Page)}");
    }
}

// ── wait for a window ──
var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(2000, o.Wait));
IntPtr hwnd = IntPtr.Zero;
while (DateTime.UtcNow < deadline)
{
    var wins = FindWinForgeWindows();
    if (wins.Count > 0) hwnd = wins[0].hwnd;
    if (hwnd != IntPtr.Zero) { Thread.Sleep(800); break; }
    Thread.Sleep(400);
}
if (hwnd == IntPtr.Zero) return Fail("No WinForge top-level window appeared. Try a longer --wait.");

// Final settle so the page finishes rendering.
Thread.Sleep(Math.Min(4000, Math.Max(1200, o.Wait / 4)));

// ── best-effort foreground (PrintWindow works even if this fails) ──
TryForeground(hwnd);
Thread.Sleep(600);

// ── capture via PrintWindow(PW_RENDERFULLCONTENT) ──
if (!Native.GetWindowRect(hwnd, out var r) || r.W <= 0 || r.H <= 0)
    return Fail("Could not read the window rect.");

Capture(hwnd, r, o.Out);
Console.WriteLine($"OK  page={o.Page ?? "(current)"}  ->  {Path.GetFullPath(o.Out)}  ({r.W}x{r.H})");

// Clean up only an instance WE launched and the caller didn't ask to keep.
if (launched is not null && !wasRunning && !o.KeepOpen)
{
    try { if (!launched.HasExited) launched.Kill(entireProcessTree: true); } catch { }
}
return 0;


// ───────────────────────── local functions ─────────────────────────

static int Fail(string msg) { Console.Error.WriteLine("ERROR: " + msg); return 2; }

static void Capture(IntPtr hwnd, Native.RECT r, string outPath)
{
    using var bmp = new Bitmap(r.W, r.H, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(bmp))
    {
        IntPtr hdc = g.GetHdc();
        bool ok;
        try { ok = Native.PrintWindow(hwnd, hdc, Native.PW_RENDERFULLCONTENT); }
        finally { g.ReleaseHdc(hdc); }
        if (!ok)
        {
            using var g2 = Graphics.FromImage(bmp);
            IntPtr hdc2 = g2.GetHdc();
            try { Native.PrintWindow(hwnd, hdc2, 0); }   // fallback: plain PrintWindow
            finally { g2.ReleaseHdc(hdc2); }
        }
    }
    var dir = Path.GetDirectoryName(Path.GetFullPath(outPath));
    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    bmp.Save(outPath, ImageFormat.Png);
}

static void TryForeground(IntPtr hwnd)
{
    try
    {
        if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
        else Native.ShowWindow(hwnd, Native.SW_SHOW);
        Native.BringWindowToTop(hwnd);

        // The foreground lock means SetForegroundWindow often no-ops unless we attach
        // our input thread to the current foreground window's thread first.
        IntPtr fg = Native.GetForegroundWindow();
        uint fgThread = Native.GetWindowThreadProcessId(fg, out _);
        uint thisThread = Native.GetCurrentThreadId();
        if (fgThread != thisThread) Native.AttachThreadInput(thisThread, fgThread, true);
        Native.SetForegroundWindow(hwnd);
        if (fgThread != thisThread) Native.AttachThreadInput(thisThread, fgThread, false);
    }
    catch { /* foreground is best-effort; PrintWindow captures regardless */ }
}

static List<(IntPtr hwnd, string title, uint pid)> FindWinForgeWindows()
{
    var pids = Process.GetProcessesByName("WinForge").Select(p => (uint)p.Id).ToHashSet();
    var result = new List<(IntPtr, string, uint)>();
    if (pids.Count == 0) return result;

    Native.EnumWindows((h, _) =>
    {
        if (!Native.IsWindowVisible(h)) return true;
        Native.GetWindowThreadProcessId(h, out uint pid);
        if (!pids.Contains(pid)) return true;
        int len = Native.GetWindowTextLength(h);
        var sb = new System.Text.StringBuilder(len + 1);
        Native.GetWindowText(h, sb, sb.Capacity);
        string title = sb.ToString();
        if (Native.GetWindowRect(h, out var rr) && rr.W > 200 && rr.H > 150)
            result.Add((h, title, pid));
        return true;
    }, IntPtr.Zero);

    return result
        .OrderByDescending(w => !string.IsNullOrEmpty(w.Item2))
        .ThenByDescending(w => { Native.GetWindowRect(w.Item1, out var rr); return (long)rr.W * rr.H; })
        .ToList();
}

static string? FindExe()
{
    string? root = FindRepoRoot(AppContext.BaseDirectory);
    if (root is null) return null;
    string[] tfms = { "net11.0-windows10.0.26100.0", "net10.0-windows10.0.26100.0" };
    var candidates = new List<string>();
    foreach (var tfm in tfms)
    {
        candidates.Add(Path.Combine(root, "bin", "x64", "Debug", tfm, "win-x64", "publish", "WinForge.exe"));
        candidates.Add(Path.Combine(root, "bin", "Debug", tfm, "win-x64", "publish", "WinForge.exe"));
        candidates.Add(Path.Combine(root, "bin", "x64", "Debug", tfm, "win-x64", "WinForge.exe"));
        candidates.Add(Path.Combine(root, "bin", "x64", "Release", tfm, "win-x64", "publish", "WinForge.exe"));
    }
    return candidates.FirstOrDefault(File.Exists);
}

static string? FindRepoRoot(string start)
{
    var dir = new DirectoryInfo(start);
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "WinForge.csproj"))) return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}


// ───────────────────────── types ─────────────────────────

static class Native
{
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder s, int max);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);

    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; public int W => Right - Left; public int H => Bottom - Top; }

    public const int SW_RESTORE = 9, SW_SHOW = 5;
    public const uint PW_RENDERFULLCONTENT = 0x00000002; // needed for WinUI / DirectComposition windows
}

class Opts
{
    public string? Page; public string Out = "winforge-shot.png"; public int Wait = 12000;
    public string? Exe; public bool Attach; public bool KeepOpen; public bool ListWindows;
}
