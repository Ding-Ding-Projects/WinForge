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
/// Minecraft 世界下載器（包本地 GitHub repo）· Minecraft world downloader. Integrates the local
/// <c>minecraft-world-downloader</c> repo under C:\Users\…\Documents\GitHub: locates or builds its
/// fat jar with a located/bundled JDK, then runs the headless proxy (start/stop) and surfaces its live
/// output + the world output folder. If the repo is absent the UI reports the expected path. No redirect.
/// </summary>
public static class MinecraftService
{
    /// <summary>啲常見 GitHub 根目錄 · Likely GitHub roots to search for the repo.</summary>
    private static IEnumerable<string> GitHubRoots()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Path.Combine(docs, "GitHub");
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(profile, "Documents", "GitHub");
        yield return Path.Combine(profile, "GitHub");
        yield return Path.Combine(profile, "source", "repos");
    }

    private const string RepoName = "minecraft-world-downloader";

    /// <summary>搵到嘅 repo 路徑（搵唔到就 null）· The located repo path, or null if absent.</summary>
    public static string? FindRepo()
    {
        // explicit override first
        var saved = SettingsStore.Get("mc.repo", "");
        if (!string.IsNullOrWhiteSpace(saved) && Directory.Exists(saved) && File.Exists(Path.Combine(saved, "pom.xml")))
            return saved;

        foreach (var root in GitHubRoots())
        {
            var candidate = Path.Combine(root, RepoName);
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "pom.xml")))
                return candidate;
        }
        return null;
    }

    public static string ExpectedRepoPath
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GitHub", RepoName);

    public static void SetRepo(string path) => SettingsStore.Set("mc.repo", path);

    /// <summary>建置好嘅 fat jar（搵唔到就 null）· The built fat jar (target\world-downloader.jar), or null.</summary>
    public static string? FindJar(string repo)
    {
        var jar = Path.Combine(repo, "target", "world-downloader.jar");
        return File.Exists(jar) ? jar : null;
    }

    // ── Java / JDK location ──────────────────────────────────────────────────

    /// <summary>搵 java.exe（PATH、JAVA_HOME，或者常見安裝位置）· Locate java.exe.</summary>
    public static string? FindJava()
    {
        // 1. JAVA_HOME
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var j = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(j)) return j;
        }
        // 2. PATH
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try { var j = Path.Combine(dir.Trim(), "java.exe"); if (File.Exists(j)) return j; } catch { }
        }
        // 3. Common install roots (Eclipse Adoptium / Microsoft / Oracle), newest first.
        foreach (var root in new[]
                 {
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Eclipse Adoptium"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Microsoft\jdk"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Java"),
                     Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Zulu"),
                 })
        {
            try
            {
                if (!Directory.Exists(root)) continue;
                foreach (var sub in Directory.GetDirectories(root).OrderByDescending(d => d))
                {
                    var j = Path.Combine(sub, "bin", "java.exe");
                    if (File.Exists(j)) return j;
                    var j2 = Path.Combine(sub, "java.exe"); // Microsoft\jdk lays bin directly under a jdk-* folder
                    if (File.Exists(j2)) return j2;
                }
            }
            catch { }
        }
        return null;
    }

    public static bool HasJava() => FindJava() is not null;

    /// <summary>自動安裝 JDK 21（winget Microsoft.OpenJDK.21）· Auto-install a JDK via winget.</summary>
    public static async Task<bool> AutoInstallJdk(CancellationToken ct = default)
    {
        var ok = await PackageService.AutoInstall("Microsoft.OpenJDK.21", ct);
        return ok && HasJava();
    }

    private static string? FindMaven()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                foreach (var name in new[] { "mvn.cmd", "mvn.bat", "mvn.exe", "mvn" })
                {
                    var p = Path.Combine(dir.Trim(), name);
                    if (File.Exists(p)) return p;
                }
            }
            catch { }
        }
        return null;
    }

    public static bool HasMaven() => FindMaven() is not null;

    /// <summary>
    /// 用 Maven 建置 fat jar · Build the fat jar with Maven (mvn -q -DskipTests package). Returns the
    /// captured build log + the jar path on success.
    /// </summary>
    public static async Task<(bool ok, string jar, string log)> BuildJar(string repo, CancellationToken ct = default)
    {
        var mvn = FindMaven();
        if (mvn is null)
            return (false, "", "Maven (mvn) not found on PATH. Install it (e.g. winget install Apache.Maven) or use the prebuilt jar.");

        var java = FindJava();
        var psi = new ProcessStartInfo
        {
            FileName = mvn,
            Arguments = "-q -DskipTests package",
            WorkingDirectory = repo,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (java is not null)
        {
            var home = Directory.GetParent(Path.GetDirectoryName(java)!)?.FullName;
            if (home is not null) psi.Environment["JAVA_HOME"] = home;
        }

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return (false, "", "Failed to start Maven.");
            var outT = p.StandardOutput.ReadToEndAsync(ct);
            var errT = p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            var log = ((await outT) + "\n" + (await errT)).Trim();
            var jar = FindJar(repo);
            return (p.ExitCode == 0 && jar is not null, jar ?? "", log);
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    // ── Run the proxy (tracked process) ──────────────────────────────────────

    private static Process? _proc;
    private static readonly object _gate = new();

    public static bool IsRunning
    {
        get { lock (_gate) { return _proc is { HasExited: false }; } }
    }

    /// <summary>下載器運行時嘅設定 · Settings for a downloader run.</summary>
    public sealed class RunOptions
    {
        public string Server { get; set; } = "";
        public int LocalPort { get; set; } = 25565;
        public string OutputDir { get; set; } = "";
        public int ExtendedRenderDistance { get; set; } = 0;
        public bool AutoOpenContainers { get; set; }
    }

    /// <summary>
    /// 啟動代理 · Start the headless proxy. Streams stdout/stderr to <paramref name="onOutput"/> (already
    /// marshalled by the caller's dispatcher) and calls <paramref name="onExit"/> when it stops.
    /// </summary>
    public static TweakResult Start(string jar, RunOptions opt, Action<string> onOutput, Action onExit)
    {
        lock (_gate)
        {
            if (_proc is { HasExited: false })
                return TweakResult.Fail("The downloader is already running.", "下載器已經喺度運行緊。");
        }

        var java = FindJava();
        if (java is null)
            return TweakResult.Fail("Java not found. Install a JDK (21+).", "搵唔到 Java。請安裝 JDK（21+）。");
        if (!File.Exists(jar))
            return TweakResult.Fail("Jar not found. Build it first.", "搵唔到 jar。請先建置。");
        if (string.IsNullOrWhiteSpace(opt.Server))
            return TweakResult.Fail("Enter the server address.", "請輸入伺服器位址。");

        var output = string.IsNullOrWhiteSpace(opt.OutputDir)
            ? Path.Combine(Path.GetDirectoryName(jar)!, "world")
            : opt.OutputDir;
        try { Directory.CreateDirectory(output); } catch { }

        var args = new StringBuilder();
        args.Append($"-jar \"{jar}\" --no-gui");
        args.Append($" -s \"{opt.Server.Trim()}\"");
        args.Append($" -l {opt.LocalPort}");
        args.Append($" -o \"{output}\"");
        if (opt.ExtendedRenderDistance > 0) args.Append($" -r {opt.ExtendedRenderDistance}");
        if (opt.AutoOpenContainers) args.Append(" --auto-open-containers");

        var psi = new ProcessStartInfo
        {
            FileName = java,
            Arguments = args.ToString(),
            WorkingDirectory = Path.GetDirectoryName(jar),
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
                return TweakResult.Fail("Failed to start Java.", "啟動 Java 失敗。");
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            lock (_gate) { _proc = p; }

            return TweakResult.Ok(
                $"Proxy started on localhost:{opt.LocalPort} → {opt.Server}. Connect Minecraft to localhost:{opt.LocalPort}. World → {output}",
                $"代理已喺 localhost:{opt.LocalPort} → {opt.Server} 啟動。用 Minecraft 連去 localhost:{opt.LocalPort}。世界 → {output}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>停止代理 · Stop the proxy (kills the process tree).</summary>
    public static TweakResult Stop()
    {
        lock (_gate)
        {
            if (_proc is null || _proc.HasExited)
                return TweakResult.Fail("The downloader is not running.", "下載器冇喺度運行。");
            try
            {
                _proc.Kill(entireProcessTree: true);
                _proc = null;
                return TweakResult.Ok("Stopped the downloader.", "已停止下載器。");
            }
            catch (Exception ex)
            {
                return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
            }
        }
    }
}
