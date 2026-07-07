using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Automation;

// ─────────────────────────────────────────────────────────────────────────────
//  winforge-shot · WinForge 驅動與截圖工具
//  A C# tool to DRIVE the WinForge desktop app (via UI Automation) and capture
//  screenshots (via PrintWindow) — i.e. "computer use" through a C# tool.
//
//  • Screenshots use PrintWindow(PW_RENDERFULLCONTENT), so the capture is WinForge's
//    OWN pixels even when another window (e.g. a Unity editor) overlaps it.
//  • Driving uses UI Automation: WinForge sets AutomationIds (e.g. ShellNavItem_module_camoufox,
//    ShellNavItem_dashboard) and Names, so we can find + invoke/select/toggle/type elements
//    without needing the window in the foreground.
//
//  Options (order-independent):
//    --page <alias>     Launch WinForge with this deep-link page first.
//    --exe  <path>      Explicit WinForge.exe (else auto-detected, then running instance).
//    --attach           Don't launch; use an already-running WinForge window.
//    --keep-open        Leave a launched instance running afterwards.
//    --wait <ms>        Initial settle time after the window appears (default 12000).
//
//  Actions (executed IN THE ORDER they appear on the command line):
//    --list-ui [depth]  Dump the UI Automation tree (AutomationId | Name | ControlType).
//    --invoke <key>     Find by AutomationId / Name (exact, then contains) and Invoke
//                       (falls back to Select / Toggle / a real mouse click).
//    --select <key>     SelectionItem.Select the element (nav items, list items).
//    --toggle <key>     Toggle the element (ToggleSwitch / CheckBox).
//    --settext <k=v>    Set a TextBox/ValuePattern element's value.
//    --click  <key>     Real mouse click at the element's center.
//    --sleep  <ms>      Wait.
//    --out    <file>    Save the working image now (capturing first if needed).
//
//  Wiki post-processing (operate on the in-memory "canvas"; geometry fields accept
//  pixels or NN%, '|' separated; auto-captures the window if no image is loaded yet):
//    --capture          Capture the window into the canvas (without saving).
//    --open  <file>     Load an existing PNG into the canvas (edit it instead of capturing).
//    --crop     <x|y|w|h>                 Crop to a region.
//    --scale    <pct | w:px>              Resize (e.g. 50, 50%, w:1200).
//    --highlight <x|y|w|h[|color|thick]>  Rounded call-out box with a glow.
//    --box      <x|y|w|h[|color|thick]>   Plain rectangle outline.
//    --ellipse  <x|y|w|h[|color|thick]>   Ellipse outline.
//    --arrow    <x1|y1|x2|y2[|color|thick]>  Arrow pointing at something.
//    --text     <x|y|message[|color|size|bg]>  Text label (optional rounded background).
//    --step     <x|y|number[|color|diam]> Numbered step badge (circle + number).
//    --redact   <x|y|w|h[|box|blur|pixelate]>  Hide personal info (default: solid box).
//
//  Example — open Camoufox, switch to its Git tab, screenshot:
//    winforge-shot --page camoufox --wait 14000 --invoke "Git" --sleep 800 --out cam-git.png
//
//  Example — annotated step-by-step shot (highlight a button, number it, redact a path):
//    winforge-shot --page git --wait 14000 \
//      --highlight "62%|18%|30%|9%|red" --step "60%|18%|1" \
//      --redact "2%|94%|40%|4%|blur" --text "5%|2%|Step 1 — click Clone|white|28|#111" \
//      --out git-step1.png
//
//  Example — annotate an EXISTING screenshot without relaunching the app:
//    winforge-shot --open docs/screenshot-vault.png --redact "10%|40%|35%|6%|box" --out vault-redacted.png
// ─────────────────────────────────────────────────────────────────────────────

string? page = null, exe = null;
bool attach = false, keepOpen = false;
int wait = 12000;
var actions = new List<(string verb, string arg)>();

for (int i = 0; i < args.Length; i++)
{
    string a = args[i].ToLowerInvariant();
    string Next() => i + 1 < args.Length ? args[++i] : "";
    switch (a)
    {
        case "--page": page = Next(); break;
        case "--exe": exe = Next(); break;
        case "--attach": attach = true; break;
        case "--keep-open": keepOpen = true; break;
        case "--wait": int.TryParse(Next(), out wait); break;

        case "--list-ui": actions.Add(("list-ui", (i + 1 < args.Length && !args[i + 1].StartsWith("--")) ? Next() : "3")); break;
        case "--invoke": actions.Add(("invoke", Next())); break;
        case "--select": actions.Add(("select", Next())); break;
        case "--toggle": actions.Add(("toggle", Next())); break;
        case "--settext": actions.Add(("settext", Next())); break;
        case "--click": actions.Add(("click", Next())); break;
        case "--sleep": actions.Add(("sleep", Next())); break;
        case "--out": actions.Add(("out", Next())); break;

        // wiki post-processing
        case "--capture": actions.Add(("capture", "")); break;
        case "--open": actions.Add(("open", Next())); break;
        case "--crop": actions.Add(("crop", Next())); break;
        case "--scale": actions.Add(("scale", Next())); break;
        case "--highlight": actions.Add(("highlight", Next())); break;
        case "--box": actions.Add(("box", Next())); break;
        case "--ellipse": actions.Add(("ellipse", Next())); break;
        case "--arrow": actions.Add(("arrow", Next())); break;
        case "--text": actions.Add(("text", Next())); break;
        case "--step": actions.Add(("step", Next())); break;
        case "--redact": actions.Add(("redact", Next())); break;

        case "-h": case "--help":
            Console.WriteLine("winforge-shot [--page x][--exe p][--attach][--keep-open][--wait ms] " +
                              "[--list-ui [depth]][--invoke key][--select key][--toggle key][--settext k=v][--click key][--sleep ms]\n" +
                              "  [--capture][--open f][--crop x|y|w|h][--scale pct][--highlight x|y|w|h][--box ...][--ellipse ...]\n" +
                              "  [--arrow x1|y1|x2|y2][--text x|y|msg][--step x|y|n][--redact x|y|w|h|mode][--out file]...");
            return 0;
    }
}
// Back-compat: a bare run with no actions still takes one screenshot.
if (actions.Count == 0) actions.Add(("out", "winforge-shot.png"));

// Decide whether we actually need the WinForge window. Pure image post-processing
// (e.g. --open an existing PNG, redact/annotate, --out) needs no app at all.
var windowVerbs = new HashSet<string> { "capture", "list-ui", "invoke", "select", "toggle", "click", "settext" };
bool hasOpen = actions.Any(x => x.verb == "open");
bool needsWindow = actions.Any(x => windowVerbs.Contains(x.verb)) || !hasOpen;

Process? launched = null;
bool wasRunning = Process.GetProcessesByName("WinForge").Length > 0;
IntPtr hwnd = IntPtr.Zero;
AutomationElement? root = null;

if (needsWindow)
{
if (!attach)
{
    string? path = exe ?? FindExe();
    if (path is null && !wasRunning) return Fail("WinForge.exe not found. Publish self-contained first, or pass --exe / --attach.");
    if (path is not null)
    {
        var psi = new ProcessStartInfo { FileName = path, UseShellExecute = true };
        if (!string.IsNullOrEmpty(page)) { psi.ArgumentList.Add("--page"); psi.ArgumentList.Add(page!); }
        launched = Process.Start(psi);
        Console.WriteLine($"Launched: {path} {(page is null ? "" : "--page " + page)}");
    }
}

// Wait for a window.
var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(2000, wait));
while (DateTime.UtcNow < deadline)
{
    var wins = FindWinForgeWindows();
    if (wins.Count > 0) { hwnd = wins[0].hwnd; Thread.Sleep(800); break; }
    Thread.Sleep(400);
}
if (hwnd == IntPtr.Zero) return Fail("No WinForge top-level window appeared. Try a longer --wait.");
Thread.Sleep(Math.Min(4000, Math.Max(1200, wait / 4)));

try { root = AutomationElement.FromHandle(hwnd); } catch { }
} // needsWindow

Bitmap? canvas = null;
// Capture the live window into the canvas (used by --capture and as the lazy
// fallback for any edit/--out when nothing has been loaded yet).
Bitmap? GrabWindow()
{
    if (hwnd == IntPtr.Zero) { Fail("no WinForge window to capture (use --open to edit a file)"); return null; }
    TryForeground(hwnd);
    Thread.Sleep(500);
    if (!Native.GetWindowRect(hwnd, out var wr) || wr.W <= 0) { Fail("bad window rect"); return null; }
    return CaptureBitmap(hwnd, wr);
}
// Ensure there's something to draw on before an edit op.
bool EnsureCanvas() { if (canvas is null) canvas = GrabWindow(); return canvas is not null; }

int outCount = 0;
foreach (var (verb, arg) in actions)
{
    switch (verb)
    {
        case "sleep": if (int.TryParse(arg, out var ms)) Thread.Sleep(ms); break;
        case "list-ui": DumpTree(root, int.TryParse(arg, out var d) ? d : 3); break;
        case "invoke": DoAction(root, hwnd, arg, "invoke"); break;
        case "select": DoAction(root, hwnd, arg, "select"); break;
        case "toggle": DoAction(root, hwnd, arg, "toggle"); break;
        case "click": DoAction(root, hwnd, arg, "click"); break;
        case "settext":
        {
            int eq = arg.IndexOf('=');
            if (eq <= 0) { Console.Error.WriteLine("settext expects key=value"); break; }
            SetText(root, arg[..eq], arg[(eq + 1)..]);
            break;
        }
        case "capture": canvas?.Dispose(); canvas = GrabWindow(); break;
        case "open":
            try { canvas?.Dispose(); canvas = new Bitmap(arg); Console.WriteLine($"[open] {arg} ({canvas.Width}x{canvas.Height})"); }
            catch (Exception ex) { Fail("open failed: " + ex.Message); }
            break;

        case "crop":   if (EnsureCanvas()) { var c = ImageOps.Crop(canvas!, Fields(arg)); canvas!.Dispose(); canvas = c; Console.WriteLine($"[crop] -> {canvas.Width}x{canvas.Height}"); } break;
        case "scale":  if (EnsureCanvas()) { var c = ImageOps.Scale(canvas!, arg); canvas!.Dispose(); canvas = c; Console.WriteLine($"[scale] -> {canvas.Width}x{canvas.Height}"); } break;
        case "highlight": if (EnsureCanvas()) { ImageOps.Highlight(canvas!, Fields(arg)); Console.WriteLine("[highlight]"); } break;
        case "box":    if (EnsureCanvas()) { ImageOps.Box(canvas!, Fields(arg)); Console.WriteLine("[box]"); } break;
        case "ellipse":if (EnsureCanvas()) { ImageOps.Ellipse(canvas!, Fields(arg)); Console.WriteLine("[ellipse]"); } break;
        case "arrow":  if (EnsureCanvas()) { ImageOps.Arrow(canvas!, Fields(arg)); Console.WriteLine("[arrow]"); } break;
        case "text":   if (EnsureCanvas()) { ImageOps.Text(canvas!, Fields(arg)); Console.WriteLine("[text]"); } break;
        case "step":   if (EnsureCanvas()) { ImageOps.Step(canvas!, Fields(arg)); Console.WriteLine("[step]"); } break;
        case "redact": if (EnsureCanvas()) { ImageOps.Redact(canvas!, Fields(arg)); Console.WriteLine("[redact]"); } break;

        case "out":
        {
            if (!EnsureCanvas()) break;
            var dir = Path.GetDirectoryName(Path.GetFullPath(arg));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            canvas!.Save(arg, ImageFormat.Png);
            Console.WriteLine($"OK  shot[{++outCount}] -> {Path.GetFullPath(arg)}  ({canvas.Width}x{canvas.Height})");
            break;
        }
    }
}
canvas?.Dispose();

if (launched is not null && !wasRunning && !keepOpen)
    try { if (!launched.HasExited) launched.Kill(entireProcessTree: true); } catch { }
return 0;


// ───────────────────────── UI Automation ─────────────────────────

static AutomationElement? FindEl(AutomationElement? root, string key)
{
    if (root is null) return null;
    try
    {
        var byId = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, key));
        if (byId is not null) return byId;
        var byName = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, key));
        if (byName is not null) return byName;
        // contains match on Name or AutomationId
        var all = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        foreach (AutomationElement e in all)
        {
            string n = Safe(() => e.Current.Name), id = Safe(() => e.Current.AutomationId);
            if ((!string.IsNullOrEmpty(n) && n.Contains(key, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(id) && id.Contains(key, StringComparison.OrdinalIgnoreCase)))
                return e;
        }
    }
    catch (Exception ex) { Console.Error.WriteLine("find error: " + ex.Message); }
    return null;
}

static void DoAction(AutomationElement? root, IntPtr hwnd, string key, string mode)
{
    var el = FindEl(root, key);
    if (el is null) { Console.Error.WriteLine($"[{mode}] not found: {key}"); return; }
    string label = Safe(() => el.Current.Name);
    try
    {
        switch (mode)
        {
            case "select" when el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var sp):
                ((SelectionItemPattern)sp).Select(); Console.WriteLine($"[select] {label}"); return;
            case "toggle" when el.TryGetCurrentPattern(TogglePattern.Pattern, out var tp):
                ((TogglePattern)tp).Toggle(); Console.WriteLine($"[toggle] {label}"); return;
            case "click":
                ClickCenter(hwnd, el); Console.WriteLine($"[click] {label}"); return;
        }
        if (el.TryGetCurrentPattern(InvokePattern.Pattern, out var ip)) { ((InvokePattern)ip).Invoke(); Console.WriteLine($"[invoke] {label}"); return; }
        if (el.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var sp2)) { ((SelectionItemPattern)sp2).Select(); Console.WriteLine($"[select] {label}"); return; }
        if (el.TryGetCurrentPattern(TogglePattern.Pattern, out var tp2)) { ((TogglePattern)tp2).Toggle(); Console.WriteLine($"[toggle] {label}"); return; }
        ClickCenter(hwnd, el); Console.WriteLine($"[click-fallback] {label}");
    }
    catch (Exception ex) { Console.Error.WriteLine($"[{mode}] failed on '{label}': {ex.Message}"); }
}

static void SetText(AutomationElement? root, string key, string value)
{
    var el = FindEl(root, key);
    if (el is null) { Console.Error.WriteLine($"[settext] not found: {key}"); return; }
    try
    {
        if (el.TryGetCurrentPattern(ValuePattern.Pattern, out var vp)) { ((ValuePattern)vp).SetValue(value); Console.WriteLine($"[settext] {key} = {value}"); }
        else Console.Error.WriteLine($"[settext] no ValuePattern on {key}");
    }
    catch (Exception ex) { Console.Error.WriteLine($"[settext] failed: {ex.Message}"); }
}

static void DumpTree(AutomationElement? root, int maxDepth)
{
    if (root is null) { Console.Error.WriteLine("no automation root"); return; }
    var walker = TreeWalker.ControlViewWalker;
    void Walk(AutomationElement el, int depth)
    {
        if (depth > maxDepth) return;
        string id = Safe(() => el.Current.AutomationId);
        string name = Safe(() => el.Current.Name);
        string ct = Safe(() => el.Current.ControlType.ProgrammaticName.Replace("ControlType.", ""));
        if (!string.IsNullOrEmpty(id) || !string.IsNullOrEmpty(name))
            Console.WriteLine($"{new string(' ', depth * 2)}{ct,-14} id='{id}' name='{Trunc(name, 60)}'");
        var child = walker.GetFirstChild(el);
        while (child is not null) { Walk(child, depth + 1); child = walker.GetNextSibling(child); }
    }
    try { Walk(root, 0); } catch (Exception ex) { Console.Error.WriteLine("dump error: " + ex.Message); }
}

static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");
static string Safe(Func<string> f) { try { return f() ?? ""; } catch { return ""; } }


// ───────────────────────── capture / window ─────────────────────────

static void ClickCenter(IntPtr hwnd, AutomationElement el)
{
    TryForeground(hwnd);
    Thread.Sleep(250);
    var r = el.Current.BoundingRectangle;
    int x = (int)(r.Left + r.Width / 2), y = (int)(r.Top + r.Height / 2);
    Native.SetCursorPos(x, y);
    Thread.Sleep(60);
    Native.mouse_event(Native.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
    Native.mouse_event(Native.MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
    Thread.Sleep(120);
}

// Split a '|'-separated annotation argument into trimmed fields.
static string[] Fields(string s) => (s ?? "").Split('|').Select(p => p.Trim()).ToArray();

static Bitmap CaptureBitmap(IntPtr hwnd, Native.RECT r)
{
    var bmp = new Bitmap(r.W, r.H, PixelFormat.Format32bppArgb);
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
            try { Native.PrintWindow(hwnd, hdc2, 0); }
            finally { g2.ReleaseHdc(hdc2); }
        }
    }
    return bmp;
}

static void TryForeground(IntPtr hwnd)
{
    try
    {
        if (Native.IsIconic(hwnd)) Native.ShowWindow(hwnd, Native.SW_RESTORE);
        else Native.ShowWindow(hwnd, Native.SW_SHOW);
        Native.BringWindowToTop(hwnd);
        IntPtr fg = Native.GetForegroundWindow();
        uint fgThread = Native.GetWindowThreadProcessId(fg, out _);
        uint thisThread = Native.GetCurrentThreadId();
        if (fgThread != thisThread) Native.AttachThreadInput(thisThread, fgThread, true);
        Native.SetForegroundWindow(hwnd);
        if (fgThread != thisThread) Native.AttachThreadInput(thisThread, fgThread, false);
    }
    catch { }
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
        if (Native.GetWindowRect(h, out var rr) && rr.W > 200 && rr.H > 150)
            result.Add((h, sb.ToString(), pid));
        return true;
    }, IntPtr.Zero);
    return result
        .OrderByDescending(w => !string.IsNullOrEmpty(w.Item2))
        .ThenByDescending(w => { Native.GetWindowRect(w.Item1, out var rr); return (long)rr.W * rr.H; })
        .ToList();
}

static int Fail(string msg) { Console.Error.WriteLine("ERROR: " + msg); return 2; }

static string? FindExe()
{
    string? rootDir = FindRepoRoot(AppContext.BaseDirectory);
    if (rootDir is null) return null;
    string[] tfms = { "net11.0-windows10.0.26100.0", "net10.0-windows10.0.26100.0" };
    var candidates = new List<string>();
    foreach (var tfm in tfms)
    {
        candidates.Add(Path.Combine(rootDir, "bin", "x64", "Debug", tfm, "win-x64", "publish", "WinForge.exe"));
        candidates.Add(Path.Combine(rootDir, "bin", "Debug", tfm, "win-x64", "publish", "WinForge.exe"));
        candidates.Add(Path.Combine(rootDir, "bin", "x64", "Debug", tfm, "win-x64", "WinForge.exe"));
        candidates.Add(Path.Combine(rootDir, "bin", "x64", "Release", tfm, "win-x64", "publish", "WinForge.exe"));
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


// ───────────────────────── native ─────────────────────────

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
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);

    public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; public int W => Right - Left; public int H => Bottom - Top; }

    public const int SW_RESTORE = 9, SW_SHOW = 5;
    public const uint PW_RENDERFULLCONTENT = 0x00000002;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002, MOUSEEVENTF_LEFTUP = 0x0004;
}
