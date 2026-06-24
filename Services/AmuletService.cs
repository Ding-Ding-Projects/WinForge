using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Amulet Minecraft 世界編輯器包裝 · Wrapper for the Amulet Minecraft world editor (a Python/wxPython
/// desktop app). Mirrors <see cref="MinecraftService"/>: locates/extracts the bundled Amulet zip into a
/// managed app-data dir, detects a Python runtime (offering a winget auto-install when absent), ensures
/// Amulet's pip deps, launches Amulet (<c>python -m amulet_map_editor</c> or a bundled frozen exe) pointed
/// at a world the user picks, and tracks the process (running/stopped, last world, live log tail). Also
/// parses a world's <c>level.dat</c> (gzipped NBT) natively to surface name / version / dimensions / size /
/// last-played in a card before launching. Amulet stays a separate launched process (GPLv3) — never linked
/// into WinForge. No redirect.
/// </summary>
public static class AmuletService
{
    // ── App-data layout ──────────────────────────────────────────────────────

    /// <summary>WinForge 管理嘅 Amulet 根目錄 · WinForge-managed Amulet root (under LocalAppData).</summary>
    public static string AppDir
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "WinForge", "Amulet");
        }
    }

    /// <summary>解壓後嘅 Amulet 內容資料夾 · Where the Amulet zip is extracted to.</summary>
    public static string ExtractDir => Path.Combine(AppDir, "app");

    // ── Locate the bundled zip ───────────────────────────────────────────────

    /// <summary>常見搵 amulet_map_editor.zip 嘅位置 · Likely locations of the bundled Amulet zip.</summary>
    private static IEnumerable<string> ZipCandidates()
    {
        // explicit override first
        var saved = SettingsStore.Get("amulet.zip", "");
        if (!string.IsNullOrWhiteSpace(saved)) yield return saved;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(profile, "Downloads", "amulet_map_editor.zip");
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Path.Combine(docs, "amulet_map_editor.zip");
        yield return Path.Combine(AppDir, "amulet_map_editor.zip");
        // bundled next to the app
        yield return Path.Combine(AppContext.BaseDirectory, "amulet_map_editor.zip");
        yield return Path.Combine(AppContext.BaseDirectory, "Assets", "amulet_map_editor.zip");
    }

    /// <summary>搵到嘅 zip 路徑（搵唔到就 null）· The located Amulet zip, or null if absent.</summary>
    public static string? FindZip()
    {
        foreach (var c in ZipCandidates())
        {
            try { if (File.Exists(c)) return c; } catch { }
        }
        return null;
    }

    public static string ExpectedZipPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "amulet_map_editor.zip");

    public static void SetZip(string path) => SettingsStore.Set("amulet.zip", path);

    /// <summary>已經解壓咗未 · True once the zip has been extracted (a marker package/exe exists).</summary>
    public static bool IsExtracted() => FindEntryPoint() is not null;

    /// <summary>
    /// 解壓 Amulet zip 到管理資料夾 · Extract the bundled zip into the managed app-data dir.
    /// Idempotent; overwrites stale files. Returns a bilingual result.
    /// </summary>
    public static async Task<TweakResult> EnsureExtracted(CancellationToken ct = default)
    {
        if (IsExtracted())
            return TweakResult.Ok("Amulet is already extracted.", "Amulet 已經解壓好。", ExtractDir);

        var zip = FindZip();
        if (zip is null)
            return TweakResult.Fail(
                $"Amulet zip not found. Expected at {ExpectedZipPath}. Use “Locate zip…” to point at it.",
                $"搵唔到 Amulet 壓縮檔。預期喺 {ExpectedZipPath}。用「指定壓縮檔…」嚟揀。");

        try
        {
            Directory.CreateDirectory(ExtractDir);
            await Task.Run(() => ZipFile.ExtractToDirectory(zip, ExtractDir, overwriteFiles: true), ct);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Extraction failed: {ex.Message}", $"解壓失敗：{ex.Message}");
        }

        return FindEntryPoint() is not null
            ? TweakResult.Ok($"Extracted Amulet to {ExtractDir}.", $"已將 Amulet 解壓到 {ExtractDir}。", ExtractDir)
            : TweakResult.Fail(
                "Extracted, but no Amulet entry point (frozen .exe or amulet_map_editor package) was found inside.",
                "已解壓，但裏面搵唔到 Amulet 入口（凍結 .exe 或 amulet_map_editor 套件）。");
    }

    // ── Resolve the entry point (frozen exe vs Python source) ─────────────────

    /// <summary>Amulet 啟動方式 · How Amulet should be launched.</summary>
    public enum LaunchMode { None, FrozenExe, PythonModule }

    /// <summary>解析到嘅啟動入口 · A resolved launch entry point.</summary>
    public sealed record EntryPoint(LaunchMode Mode, string Path, string? PackageDir);

    /// <summary>
    /// 搵 Amulet 入口 · Find Amulet's entry point inside the extracted dir: a PyInstaller-frozen
    /// <c>amulet*.exe</c> (self-contained) is preferred; otherwise the <c>amulet_map_editor</c> Python
    /// package directory (run via <c>python -m amulet_map_editor</c>). Returns null if neither is present.
    /// </summary>
    public static EntryPoint? FindEntryPoint()
    {
        if (!Directory.Exists(ExtractDir)) return null;
        try
        {
            // 1. A frozen .exe shipped in the zip (self-contained — no Python needed).
            var exe = Directory.EnumerateFiles(ExtractDir, "*.exe", SearchOption.AllDirectories)
                .FirstOrDefault(f =>
                {
                    var n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    return (n.Contains("amulet") || n == "amulet_app") && !n.Contains("unins");
                });
            if (exe is not null) return new EntryPoint(LaunchMode.FrozenExe, exe, null);

            // 2. A Python source package (run with python -m amulet_map_editor).
            var pkg = Directory.EnumerateDirectories(ExtractDir, "amulet_map_editor", SearchOption.AllDirectories)
                .FirstOrDefault(d => File.Exists(Path.Combine(d, "__main__.py")));
            if (pkg is not null)
            {
                // -m runs from the parent of the package dir
                var parent = Directory.GetParent(pkg)?.FullName ?? ExtractDir;
                return new EntryPoint(LaunchMode.PythonModule, parent, pkg);
            }
        }
        catch { }
        return null;
    }

    // ── Python detection / install ───────────────────────────────────────────

    /// <summary>搵 Python（py launcher、PATH、常見安裝位）· Locate a Python interpreter.</summary>
    public static string? FindPython()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        var dirs = pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries);

        // 1. py launcher (ships with python.org installs) — most reliable on Windows.
        foreach (var dir in dirs)
        {
            try { var p = Path.Combine(dir.Trim(), "py.exe"); if (File.Exists(p)) return p; } catch { }
        }
        // 2. python.exe on PATH (skip the WindowsApps stub, which is a non-functional shim).
        foreach (var dir in dirs)
        {
            try
            {
                if (dir.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)) continue;
                var p = Path.Combine(dir.Trim(), "python.exe");
                if (File.Exists(p)) return p;
            }
            catch { }
        }
        // 3. Common install roots.
        foreach (var root in new[]
                 {
                     Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Programs\Python"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Python"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Python"),
                 })
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var sub in Directory.GetDirectories(root).OrderByDescending(d => d))
                {
                    var p = Path.Combine(sub, "python.exe");
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
        }
        return null;
    }

    public static bool HasPython() => FindPython() is not null;

    /// <summary>自動安裝 Python 3.12（winget）· Auto-install Python via winget.</summary>
    public static async Task<bool> AutoInstallPython(CancellationToken ct = default)
    {
        var ok = await PackageService.AutoInstall("Python.Python.3.12", ct);
        return ok && HasPython();
    }

    /// <summary>
    /// 安裝 Amulet 嘅 pip 相依 · Install Amulet's pip dependencies into the user site. Tries an editable
    /// install of the extracted source first; falls back to <c>pip install amulet-map-editor</c>.
    /// Streams pip output to <paramref name="onOutput"/>. Frozen builds need no deps and short-circuit.
    /// </summary>
    public static async Task<TweakResult> EnsureDeps(Action<string>? onOutput = null, CancellationToken ct = default)
    {
        var entry = FindEntryPoint();
        if (entry is null)
            return TweakResult.Fail("Extract Amulet first.", "請先解壓 Amulet。");
        if (entry.Mode == LaunchMode.FrozenExe)
            return TweakResult.Ok("Frozen build is self-contained — no pip deps needed.", "凍結版自帶相依 — 唔使 pip。");

        var py = FindPython();
        if (py is null)
            return TweakResult.Fail("Python not found. Install Python first.", "搵唔到 Python。請先安裝 Python。");

        // Prefer an editable install of the extracted source (gives the console-script + deps);
        // if there's no setup.py/pyproject next to the package, install the published wheel instead.
        var srcRoot = entry.PackageDir is not null ? Directory.GetParent(entry.PackageDir)?.FullName : null;
        var hasSetup = srcRoot is not null &&
                       (File.Exists(Path.Combine(srcRoot, "setup.py")) ||
                        File.Exists(Path.Combine(srcRoot, "pyproject.toml")) ||
                        File.Exists(Path.Combine(srcRoot, "setup.cfg")));

        var pyArgs = PyPrefix(py);
        string args = hasSetup
            ? $"{pyArgs}-m pip install --user \".\""
            : $"{pyArgs}-m pip install --user amulet-map-editor";
        var workdir = hasSetup ? srcRoot! : ExtractDir;

        return await StreamProcess(py, args, workdir, onOutput,
            okEn: "Amulet dependencies installed.", okZh: "Amulet 相依已安裝。",
            failEn: "pip install failed — see the log.", failZh: "pip 安裝失敗 — 睇吓記錄。", ct);
    }

    /// <summary>If we resolved the <c>py</c> launcher, pass <c>-3</c> so it picks Python 3.</summary>
    private static string PyPrefix(string py)
        => Path.GetFileName(py).Equals("py.exe", StringComparison.OrdinalIgnoreCase) ? "-3 " : "";

    private static async Task<TweakResult> StreamProcess(string file, string args, string workdir,
        Action<string>? onOutput, string okEn, string okZh, string failEn, string failZh, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            WorkingDirectory = workdir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        try
        {
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke(e.Data); };
            if (!p.Start()) return TweakResult.Fail("Failed to start the process.", "無法啟動程序。");
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            await p.WaitForExitAsync(ct);
            return p.ExitCode == 0 ? TweakResult.Ok(okEn, okZh) : TweakResult.Fail(failEn, failZh);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    // ── World validation ─────────────────────────────────────────────────────

    /// <summary>一個世界資料夾係咪有效（睇有冇 level.dat）· True if the folder looks like a MC world.</summary>
    public static bool IsValidWorld(string folder)
    {
        try { return Directory.Exists(folder) && File.Exists(Path.Combine(folder, "level.dat")); }
        catch { return false; }
    }

    /// <summary>常見 .minecraft\saves 路徑 · The default .minecraft\saves folder, if it exists.</summary>
    public static string? FindSavesFolder()
    {
        var appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var saves = Path.Combine(appdata, ".minecraft", "saves");
        return Directory.Exists(saves) ? saves : null;
    }

    // ── Run Amulet (tracked process) ─────────────────────────────────────────

    private static Process? _proc;
    private static readonly object _gate = new();

    public static bool IsRunning
    {
        get { lock (_gate) { return _proc is { HasExited: false }; } }
    }

    public static string LastWorld
    {
        get => SettingsStore.Get("amulet.lastWorld", "");
        private set => SettingsStore.Set("amulet.lastWorld", value);
    }

    /// <summary>
    /// 啟動 Amulet · Launch Amulet (optionally pointed at a world). Streams stdout/stderr to
    /// <paramref name="onOutput"/> (caller marshals to the UI thread) and calls <paramref name="onExit"/>
    /// when it stops. World path is remembered as the last-launched world.
    /// </summary>
    public static TweakResult Start(string? worldFolder, Action<string> onOutput, Action onExit)
    {
        lock (_gate)
        {
            if (_proc is { HasExited: false })
                return TweakResult.Fail("Amulet is already running.", "Amulet 已經喺度運行緊。");
        }

        var entry = FindEntryPoint();
        if (entry is null)
            return TweakResult.Fail("Amulet is not extracted yet. Extract it first.", "Amulet 仲未解壓。請先解壓。");

        if (!string.IsNullOrWhiteSpace(worldFolder) && !IsValidWorld(worldFolder!))
            return TweakResult.Fail(
                "That folder is not a Minecraft world (no level.dat).",
                "嗰個資料夾唔係 Minecraft 世界（冇 level.dat）。");

        string file;
        string args;
        string workdir;

        if (entry.Mode == LaunchMode.FrozenExe)
        {
            file = entry.Path;
            args = string.IsNullOrWhiteSpace(worldFolder) ? "" : $"\"{worldFolder}\"";
            workdir = Path.GetDirectoryName(entry.Path) ?? ExtractDir;
        }
        else
        {
            var py = FindPython();
            if (py is null)
                return TweakResult.Fail("Python not found. Install Python first.", "搵唔到 Python。請先安裝 Python。");
            file = py;
            var prefix = PyPrefix(py);
            args = string.IsNullOrWhiteSpace(worldFolder)
                ? $"{prefix}-m amulet_map_editor"
                : $"{prefix}-m amulet_map_editor \"{worldFolder}\"";
            workdir = entry.Path; // parent of the package dir
        }

        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            WorkingDirectory = workdir,
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
                lock (_gate) { _proc = null; }
                onExit();
            };
            if (!p.Start())
                return TweakResult.Fail("Failed to start Amulet.", "啟動 Amulet 失敗。");
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            lock (_gate) { _proc = p; }

            if (!string.IsNullOrWhiteSpace(worldFolder)) LastWorld = worldFolder!;

            return string.IsNullOrWhiteSpace(worldFolder)
                ? TweakResult.Ok("Amulet launched.", "Amulet 已啟動。")
                : TweakResult.Ok($"Amulet launched on {worldFolder}.", $"Amulet 已開啟 {worldFolder}。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>停止 Amulet · Stop Amulet (kills the process tree).</summary>
    public static TweakResult Stop()
    {
        lock (_gate)
        {
            if (_proc is null || _proc.HasExited)
                return TweakResult.Fail("Amulet is not running.", "Amulet 冇喺度運行。");
            try
            {
                _proc.Kill(entireProcessTree: true);
                _proc = null;
                return TweakResult.Ok("Stopped Amulet.", "已停止 Amulet。");
            }
            catch (Exception ex)
            {
                return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
            }
        }
    }

    // ── Backup a world (zip the folder) ──────────────────────────────────────

    /// <summary>
    /// 備份世界（壓縮成 zip）· Back up a world by zipping its folder to <paramref name="destZip"/>.
    /// </summary>
    public static async Task<TweakResult> BackupWorld(string worldFolder, string destZip, CancellationToken ct = default)
    {
        if (!IsValidWorld(worldFolder))
            return TweakResult.Fail("That folder is not a Minecraft world.", "嗰個資料夾唔係 Minecraft 世界。");
        try
        {
            if (File.Exists(destZip)) File.Delete(destZip);
            await Task.Run(() => ZipFile.CreateFromDirectory(worldFolder, destZip,
                CompressionLevel.Optimal, includeBaseDirectory: false), ct);
            return TweakResult.Ok($"World backed up to {destZip}.", $"世界已備份到 {destZip}。", destZip);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Backup failed: {ex.Message}", $"備份失敗：{ex.Message}");
        }
    }

    // ── Native level.dat (gzipped NBT) metadata ──────────────────────────────

    /// <summary>世界中繼資料（由 level.dat 讀出，雙語）· World metadata parsed natively from level.dat.</summary>
    public sealed class WorldMeta
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public int DataVersion { get; set; }
        public string Edition { get; set; } = "";      // Java / Bedrock-ish
        public long SizeBytes { get; set; }
        public DateTimeOffset? LastPlayed { get; set; }
        public List<string> Dimensions { get; set; } = new();
        public string Folder { get; set; } = "";

        public string SizeDisplay => HumanSize(SizeBytes);
        public string LastPlayedDisplay => LastPlayed?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "—";
        public string DimensionsDisplay => Dimensions.Count > 0 ? string.Join(", ", Dimensions) : "—";
    }

    /// <summary>
    /// 讀世界中繼資料 · Read world metadata from level.dat (gzipped NBT). Best-effort: returns what it can
    /// parse, never throws. Java edition level.dat is gzip-compressed big-endian NBT; we read the common
    /// keys (LevelName, Version{Name}, DataVersion, LastPlayed) plus folder size and dimension subfolders.
    /// </summary>
    public static WorldMeta ReadWorldMeta(string worldFolder)
    {
        var meta = new WorldMeta { Folder = worldFolder, Name = Path.GetFileName(worldFolder.TrimEnd('\\', '/')) };
        try
        {
            meta.SizeBytes = DirSize(worldFolder);
            meta.Dimensions = DetectDimensions(worldFolder);

            var levelDat = Path.Combine(worldFolder, "level.dat");
            if (!File.Exists(levelDat)) return meta;

            byte[] raw;
            try
            {
                using var fs = File.OpenRead(levelDat);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var ms = new MemoryStream();
                gz.CopyTo(ms);
                raw = ms.ToArray();
                meta.Edition = "Java";
            }
            catch
            {
                // Bedrock level.dat is little-endian, uncompressed, with an 8-byte header — not parsed here.
                raw = Array.Empty<byte>();
                meta.Edition = "Bedrock?";
            }

            if (raw.Length > 0)
            {
                var nbt = new NbtReader(raw);
                var root = nbt.ReadRootCompound();
                if (root is not null)
                {
                    // Java wraps everything under a "Data" compound.
                    var data = root.TryGetValue("Data", out var d) && d is NbtCompound dc ? dc : root;

                    if (data.TryGetValue("LevelName", out var ln) && ln is string lns && lns.Length > 0)
                        meta.Name = lns;

                    if (data.TryGetValue("Version", out var ver) && ver is NbtCompound vc)
                    {
                        if (vc.TryGetValue("Name", out var vn) && vn is string vns) meta.Version = vns;
                        if (vc.TryGetValue("Id", out var vi) && vi is int vii) meta.DataVersion = vii;
                    }
                    if (meta.DataVersion == 0 && data.TryGetValue("DataVersion", out var dv) && dv is int dvi)
                        meta.DataVersion = dvi;

                    if (data.TryGetValue("LastPlayed", out var lp) && lp is long lpl && lpl > 0)
                        meta.LastPlayed = DateTimeOffset.FromUnixTimeMilliseconds(lpl);
                }
            }
        }
        catch { }
        return meta;
    }

    private static List<string> DetectDimensions(string worldFolder)
    {
        var dims = new List<string>();
        try
        {
            if (Directory.Exists(Path.Combine(worldFolder, "region"))) dims.Add("Overworld");
            if (Directory.Exists(Path.Combine(worldFolder, "DIM-1"))) dims.Add("Nether");
            if (Directory.Exists(Path.Combine(worldFolder, "DIM1"))) dims.Add("End");
            // Datapack / custom dimensions live under dimensions\<namespace>\<id>.
            var custom = Path.Combine(worldFolder, "dimensions");
            if (Directory.Exists(custom))
                foreach (var ns in Directory.GetDirectories(custom))
                    foreach (var id in Directory.GetDirectories(ns))
                        dims.Add($"{Path.GetFileName(ns)}:{Path.GetFileName(id)}");
        }
        catch { }
        return dims;
    }

    private static long DirSize(string dir)
    {
        long total = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(f).Length; } catch { }
            }
        }
        catch { }
        return total;
    }

    private static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double s = bytes; int i = 0;
        while (s >= 1024 && i < u.Length - 1) { s /= 1024; i++; }
        return $"{s:0.#} {u[i]}";
    }

    // ── Minimal big-endian NBT reader (Java level.dat) ───────────────────────

    /// <summary>一個 NBT compound 標籤（名→值）· One NBT compound tag (name → value).</summary>
    public sealed class NbtCompound : Dictionary<string, object?> { }

    /// <summary>
    /// 極簡 NBT 讀取器 · A minimal big-endian NBT reader, just enough to pull the scalar keys WinForge
    /// surfaces from a Java level.dat. Lists/arrays are skipped (consumed but not materialised) so the
    /// stream stays aligned. Never throws past the public surface.
    /// </summary>
    private sealed class NbtReader
    {
        private readonly byte[] _b;
        private int _i;

        public NbtReader(byte[] bytes) { _b = bytes; _i = 0; }

        // Tag type ids.
        private const byte End = 0, Byte = 1, Short = 2, Int = 3, Long = 4, Float = 5, Double = 6,
            ByteArray = 7, Str = 8, List = 9, Compound = 10, IntArray = 11, LongArray = 12;

        public NbtCompound? ReadRootCompound()
        {
            try
            {
                var type = ReadByte();
                if (type != Compound) return null;
                ReadString(); // root name (usually empty)
                return ReadCompound();
            }
            catch { return null; }
        }

        private NbtCompound ReadCompound()
        {
            var c = new NbtCompound();
            while (true)
            {
                var type = ReadByte();
                if (type == End) break;
                var name = ReadString();
                c[name] = ReadPayload(type);
            }
            return c;
        }

        private object? ReadPayload(byte type)
        {
            switch (type)
            {
                case Byte: return ReadByte();
                case Short: return (int)ReadShort();
                case Int: return ReadInt();
                case Long: return ReadLong();
                case Float: _i += 4; return null;
                case Double: _i += 8; return null;
                case ByteArray: { var n = ReadInt(); _i += n; return null; }
                case Str: return ReadString();
                case List:
                {
                    var elem = ReadByte();
                    var n = ReadInt();
                    for (int k = 0; k < n; k++) ReadPayload(elem);
                    return null;
                }
                case Compound: return ReadCompound();
                case IntArray: { var n = ReadInt(); _i += n * 4; return null; }
                case LongArray: { var n = ReadInt(); _i += n * 8; return null; }
                default: return null;
            }
        }

        private byte ReadByte() => _b[_i++];

        private short ReadShort()
        {
            short v = (short)((_b[_i] << 8) | _b[_i + 1]);
            _i += 2; return v;
        }

        private int ReadInt()
        {
            int v = (_b[_i] << 24) | (_b[_i + 1] << 16) | (_b[_i + 2] << 8) | _b[_i + 3];
            _i += 4; return v;
        }

        private long ReadLong()
        {
            long v = 0;
            for (int k = 0; k < 8; k++) v = (v << 8) | _b[_i + k];
            _i += 8; return v;
        }

        private string ReadString()
        {
            int len = (ushort)((_b[_i] << 8) | _b[_i + 1]);
            _i += 2;
            var s = Encoding.UTF8.GetString(_b, _i, len);
            _i += len;
            return s;
        }
    }
}
