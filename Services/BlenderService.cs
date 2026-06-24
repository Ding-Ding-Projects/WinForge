using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 一個算圖工作（headless 算圖嘅參數）· One render job's parameters for a headless Blender render.
/// </summary>
public sealed class RenderJob
{
    public string BlendFile { get; set; } = "";
    public string OutputDir { get; set; } = "";
    /// <summary>輸出檔名樣板（# = 影格號碼補零）· Output filename template (# = frame-number padding).</summary>
    public string OutputName { get; set; } = "frame_####";
    public bool Animation { get; set; }          // false = single frame, true = -s/-e/-a range
    public int Frame { get; set; } = 1;          // single-frame number
    public int StartFrame { get; set; } = 1;     // animation range start
    public int EndFrame { get; set; } = 250;     // animation range end
    public string Engine { get; set; } = "";     // "" = use file's; CYCLES / BLENDER_EEVEE / BLENDER_WORKBENCH
    public string Format { get; set; } = "";     // "" = use file's; PNG / JPEG / OPEN_EXR / FFMPEG / TIFF / WEBP
    public int Samples { get; set; }             // 0 = use file's; else override via --python-expr
    public string Device { get; set; } = "";     // "" = use file's; CPU / GPU (Cycles only, via --python-expr)

    /// <summary>顯示用標題 · A short display title for the queue list.</summary>
    public string Title =>
        $"{Path.GetFileName(BlendFile)} · {(Animation ? $"{StartFrame}-{EndFrame}" : $"#{Frame}")}" +
        (string.IsNullOrEmpty(Engine) ? "" : $" · {Engine}");
}

/// <summary>
/// 包住 blender CLI · A thin wrapper over the installed Blender desktop binary (run headless via -b).
/// 偵測／安裝 Blender、用 GUI 開 .blend、headless 算圖（單影格或動畫）、跑 Python script、批次佇列。
/// Detect/install Blender, open .blend in the GUI, headless renders (single frame or animation) with live
/// progress parsing, run Python scripts, and a sequential batch queue. WinForge never links Blender's code —
/// it only launches blender.exe as a separate process (so GPLv2 is not a concern). Bilingual throughout.
/// </summary>
public static class BlenderService
{
    private static string? _exe;

    // ── Detection ─────────────────────────────────────────────────────────────

    /// <summary>搵 blender.exe（明確設定 → PATH → winget Links → Program Files）· Locate blender.exe.</summary>
    public static string FindBlender()
    {
        if (_exe is not null && File.Exists(_exe)) return _exe;

        // 1. explicit override saved by the user
        var saved = SettingsStore.Get("blender.exe", "");
        if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved)) return _exe = saved;

        // 2. PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try { var c = Path.Combine(dir.Trim(), "blender.exe"); if (File.Exists(c)) return _exe = c; }
            catch { }
        }

        // 3. winget per-user Links shim (BlenderFoundation.Blender drops blender.exe here)
        try
        {
            var links = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WinGet", "Links", "blender.exe");
            if (File.Exists(links)) return _exe = links;
        }
        catch { }

        // 4. Standard install roots — newest version folder first
        foreach (var root in new[]
                 {
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Blender Foundation"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Blender Foundation"),
                 })
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                // direct (root\Blender\blender.exe) and versioned (root\Blender 4.2\blender.exe)
                foreach (var sub in Directory.GetDirectories(root).OrderByDescending(d => d))
                {
                    var c = Path.Combine(sub, "blender.exe");
                    if (File.Exists(c)) return _exe = c;
                }
            }
            catch { }
        }

        return "";
    }

    public static bool IsInstalled => FindBlender().Length > 0;

    /// <summary>裝完之後叫呢個，等啱啱裝嘅 blender 即刻搵到 · Clear the cached path after an install.</summary>
    public static void Rescan() => _exe = null;

    /// <summary>引擎列狀態（雙語）· Engine-bar health (bilingual).</summary>
    public static (bool ok, string en, string zh) Health()
    {
        var exe = FindBlender();
        if (exe.Length == 0)
            return (false, "Blender not found. Install it (winget BlenderFoundation.Blender) or set its path.",
                "搵唔到 Blender。請安裝（winget BlenderFoundation.Blender）或者設定路徑。");
        return (true, $"Blender: {exe}", $"Blender：{exe}");
    }

    /// <summary>跑 blender --version 攞版本字串 · Read the version string via blender --version.</summary>
    public static async Task<string> GetVersion(CancellationToken ct = default)
    {
        var exe = FindBlender();
        if (exe.Length == 0) return "";
        var outp = await ShellRunner.Capture(exe, "--version", ct);
        foreach (var line in (outp ?? "").Replace("\r", "").Split('\n'))
            if (line.TrimStart().StartsWith("Blender", StringComparison.OrdinalIgnoreCase))
                return line.Trim();
        return (outp ?? "").Trim();
    }

    /// <summary>記住使用者揀嘅 blender.exe · Persist a user-chosen blender.exe path.</summary>
    public static void SetPath(string exe) { SettingsStore.Set("blender.exe", exe); _exe = File.Exists(exe) ? exe : null; }

    public static async Task<bool> AutoInstall(CancellationToken ct = default)
    {
        var ok = await PackageService.AutoInstall("BlenderFoundation.Blender", ct);
        Rescan();
        return ok && IsInstalled;
    }

    // ── Argument building (ORDER MATTERS in Blender's CLI) ────────────────────
    // Globals (-b, --python-expr, --python) come first; then -o / -F / -E (render
    // settings); then -f / -s / -e / -a (the action) LAST, or they use defaults.

    private static string Q(string p) => $"\"{p}\"";

    /// <summary>砌算圖參數 · Build the headless render argument string for a job.</summary>
    public static string BuildRenderArgs(RenderJob job)
    {
        var sb = new StringBuilder();
        sb.Append("-b ").Append(Q(job.BlendFile));

        // Optional Cycles overrides via --python-expr (must come before the action flags).
        var py = new List<string>();
        if (job.Samples > 0)
            py.Add($"import bpy; bpy.context.scene.cycles.samples={job.Samples}; bpy.context.scene.eevee.taa_render_samples={job.Samples}");
        if (!string.IsNullOrEmpty(job.Device) && job.Engine == "CYCLES")
        {
            // Enable the GPU compute device for Cycles, or force CPU.
            if (job.Device == "GPU")
                py.Add("import bpy; p=bpy.context.preferences.addons['cycles'].preferences; p.compute_device_type='CUDA'; p.get_devices(); [setattr(d,'use',True) for d in p.devices]; bpy.context.scene.cycles.device='GPU'");
            else
                py.Add("import bpy; bpy.context.scene.cycles.device='CPU'");
        }
        foreach (var expr in py) sb.Append(" --python-expr ").Append(Q(expr));

        // Output template: <dir>/<name>  (Blender turns #### into the zero-padded frame number)
        if (!string.IsNullOrWhiteSpace(job.OutputDir))
        {
            var name = string.IsNullOrWhiteSpace(job.OutputName) ? "frame_####" : job.OutputName;
            sb.Append(" -o ").Append(Q(Path.Combine(job.OutputDir, name)));
        }
        if (!string.IsNullOrEmpty(job.Format)) sb.Append(" -F ").Append(job.Format);
        if (!string.IsNullOrEmpty(job.Engine)) sb.Append(" -E ").Append(job.Engine);

        // Action LAST.
        if (job.Animation) sb.Append($" -s {job.StartFrame} -e {job.EndFrame} -a");
        else sb.Append($" -f {job.Frame}");

        return sb.ToString();
    }

    /// <summary>砌跑 Python script 嘅參數 · Build args to run a Python script against a file.</summary>
    public static string BuildScriptArgs(string blendFile, string scriptPath, string extraAfterDashDash = "")
    {
        var sb = new StringBuilder();
        sb.Append("-b ");
        if (!string.IsNullOrWhiteSpace(blendFile)) sb.Append(Q(blendFile)).Append(' ');
        sb.Append("--python ").Append(Q(scriptPath));
        if (!string.IsNullOrWhiteSpace(extraAfterDashDash)) sb.Append(" -- ").Append(extraAfterDashDash);
        return sb.ToString();
    }

    // ── GUI launch ────────────────────────────────────────────────────────────

    /// <summary>用 Blender GUI 開一個 .blend（或者直接開 Blender）· Open a .blend in the Blender GUI.</summary>
    public static TweakResult OpenGui(string? blendFile = null)
    {
        var exe = FindBlender();
        if (exe.Length == 0) return TweakResult.Fail("Blender not found.", "搵唔到 Blender。");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = string.IsNullOrWhiteSpace(blendFile) ? "" : Q(blendFile),
                UseShellExecute = true,
            };
            Process.Start(psi);
            return TweakResult.Ok("Opening in Blender…", "喺 Blender 開緊…");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ── Headless run (streamed, tracked, cancellable) ────────────────────────

    private static readonly object _gate = new();
    private static Process? _proc;

    public static bool IsRunning { get { lock (_gate) return _proc is { HasExited: false }; } }

    /// <summary>開始一個算圖工作，逐行串流輸出 · Start a render job, streaming each output line.</summary>
    public static TweakResult StartRender(RenderJob job, Action<string> onOutput, Action<int> onExit)
        => StartRaw(BuildRenderArgs(job), onOutput, onExit);

    /// <summary>對住一個檔案跑 Python script · Run a Python script against a file, streaming output.</summary>
    public static TweakResult StartScript(string blendFile, string scriptPath, Action<string> onOutput, Action<int> onExit)
    {
        if (!File.Exists(scriptPath)) return TweakResult.Fail("Script not found.", "搵唔到 script。");
        return StartRaw(BuildScriptArgs(blendFile, scriptPath), onOutput, onExit);
    }

    private static TweakResult StartRaw(string args, Action<string> onOutput, Action<int> onExit)
    {
        var exe = FindBlender();
        if (exe.Length == 0) return TweakResult.Fail("Blender not found.", "搵唔到 Blender。");
        lock (_gate)
        {
            if (_proc is { HasExited: false })
                return TweakResult.Fail("A render is already running.", "已經有一個算圖喺度運行緊。");
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        try
        {
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
            p.Exited += (_, _) =>
            {
                int code = 0;
                try { code = p.ExitCode; } catch { }
                lock (_gate) { if (_proc == p) _proc = null; }
                onExit(code);
            };
            if (!p.Start()) return TweakResult.Fail("Failed to start Blender.", "啟動 Blender 失敗。");
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            lock (_gate) { _proc = p; }
            return TweakResult.Ok("Render started.", "算圖已開始。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>取消／殺死成棵程序樹 · Cancel — kills the whole process tree.</summary>
    public static TweakResult Cancel()
    {
        Process? p;
        lock (_gate) { p = _proc; _proc = null; }
        if (p is null || p.HasExited) return TweakResult.Fail("Nothing is running.", "冇嘢喺度運行。");
        try { p.Kill(entireProcessTree: true); return TweakResult.Ok("Cancelled the render.", "已取消算圖。"); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ── Live progress parsing ────────────────────────────────────────────────

    /// <summary>由一行輸出抽算圖進度 · Parsed progress from one stdout line, or null if nothing useful.</summary>
    public readonly record struct ProgressInfo(int? CurrentFrame, double? Fraction, string? SavedPath, bool Done);

    /// <summary>
    /// 解析 Blender 嘅進度行 · Parse Blender's progress lines:
    /// "Fra:12 Mem:…" → current frame; "… | Rendered 120/1000 Tiles" or "Sample 64/128" → a fraction;
    /// "Saved: '…'" → an output path; "Blender quit" → done.
    /// </summary>
    public static ProgressInfo ParseLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return default;
        int? frame = null;
        double? frac = null;
        string? saved = null;
        bool done = line.Contains("Blender quit", StringComparison.OrdinalIgnoreCase);

        // Current frame: lines start with "Fra:<n>"
        if (line.StartsWith("Fra:", StringComparison.OrdinalIgnoreCase))
        {
            int i = 4, j = i;
            while (j < line.Length && char.IsDigit(line[j])) j++;
            if (j > i && int.TryParse(line.AsSpan(i, j - i), out var f)) frame = f;
        }

        // Saved path: Saved: 'C:\…\frame_0001.png'
        int sIdx = line.IndexOf("Saved:", StringComparison.OrdinalIgnoreCase);
        if (sIdx >= 0)
        {
            var rest = line.Substring(sIdx + "Saved:".Length).Trim().Trim('\'', '"');
            if (rest.Length > 0) saved = rest;
        }

        // Fraction: "Sample 64/128", "Rendered 120/1000 Tiles", "Rendering 3 / 5 samples"
        frac = ExtractFraction(line, "Sample") ?? ExtractFraction(line, "Rendered") ?? ExtractFraction(line, "Rendering");

        if (frame is null && frac is null && saved is null && !done) return default;
        return new ProgressInfo(frame, frac, saved, done);
    }

    private static double? ExtractFraction(string line, string keyword)
    {
        int k = line.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (k < 0) return null;
        // find "<num> / <num>" after the keyword
        var span = line.Substring(k + keyword.Length);
        int slash = span.IndexOf('/');
        if (slash < 0) return null;
        // walk left/right of the slash to capture the two integers
        int a = slash - 1; while (a >= 0 && (span[a] == ' ')) a--;
        int aEnd = a + 1; while (a >= 0 && char.IsDigit(span[a])) a--;
        int b = slash + 1; while (b < span.Length && span[b] == ' ') b++;
        int bStart = b; while (b < span.Length && char.IsDigit(span[b])) b++;
        if (aEnd <= a + 1 || b <= bStart) return null;
        if (int.TryParse(span.AsSpan(a + 1, aEnd - a - 1), out var cur) &&
            int.TryParse(span.AsSpan(bStart, b - bStart), out var tot) && tot > 0)
            return Math.Clamp((double)cur / tot, 0, 1);
        return null;
    }

    // ── Starter Python scripts (shipped to %LOCALAPPDATA%\WinForge\blender-scripts) ──

    public static string ScriptsDir
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "blender-scripts");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }
    }

    /// <summary>一個內建起步 script · One built-in starter script.</summary>
    public sealed record StarterScript(string Id, string FileName, string En, string Zh, string Body);

    public static IReadOnlyList<StarterScript> StarterScripts { get; } = new List<StarterScript>
    {
        new("gltf", "export_gltf.py",
            "Export scene to glTF (.glb)", "將場景匯出做 glTF（.glb）",
            // Writes <blendfile>.glb next to the .blend.
            "import bpy, os\n" +
            "src = bpy.data.filepath\n" +
            "out = os.path.splitext(src)[0] + '.glb' if src else os.path.join(bpy.app.tempdir, 'export.glb')\n" +
            "bpy.ops.export_scene.gltf(filepath=out, export_format='GLB')\n" +
            "print('Saved: ' + out)\n"),
        new("fbx", "export_fbx.py",
            "Export scene to FBX (.fbx)", "將場景匯出做 FBX（.fbx）",
            "import bpy, os\n" +
            "src = bpy.data.filepath\n" +
            "out = os.path.splitext(src)[0] + '.fbx' if src else os.path.join(bpy.app.tempdir, 'export.fbx')\n" +
            "bpy.ops.export_scene.fbx(filepath=out)\n" +
            "print('Saved: ' + out)\n"),
        new("obj", "export_obj.py",
            "Export scene to OBJ (.obj)", "將場景匯出做 OBJ（.obj）",
            "import bpy, os\n" +
            "src = bpy.data.filepath\n" +
            "out = os.path.splitext(src)[0] + '.obj' if src else os.path.join(bpy.app.tempdir, 'export.obj')\n" +
            "bpy.ops.wm.obj_export(filepath=out)\n" +
            "print('Saved: ' + out)\n"),
        new("info", "scene_info.py",
            "Print scene info (objects, frames)", "印出場景資料（物件、影格）",
            "import bpy\n" +
            "sc = bpy.context.scene\n" +
            "print('Scene: ' + sc.name)\n" +
            "print('Objects: ' + str(len(bpy.data.objects)))\n" +
            "print('Frame range: %d-%d' % (sc.frame_start, sc.frame_end))\n" +
            "print('Engine: ' + sc.render.engine)\n"),
    };

    /// <summary>將內建 script 寫落磁碟（若未存在），回傳路徑 · Materialise a starter script to disk; returns its path.</summary>
    public static string EnsureStarterScript(StarterScript s)
    {
        var path = Path.Combine(ScriptsDir, s.FileName);
        try { File.WriteAllText(path, s.Body); } catch { }
        return path;
    }

    /// <summary>用 explorer 開一個資料夾 · Open a folder in Explorer.</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { }
    }
}
