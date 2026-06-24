using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Minecraft 伺服器架設器 · Minecraft server setupper. Native C# orchestration around a wrapped Java
/// runtime: download a Paper build via the PaperMC Fill v3 API, or compile Spigot with BuildTools.jar;
/// accept the EULA; edit <c>server.properties</c>; generate a <c>start.bat</c> with memory flags; run the
/// server as a tracked long-lived process with a live console and a stdin command box; and build plugins
/// from git source (Maven/Gradle) into the <c>plugins/</c> folder. Reuses Java/Maven helpers from
/// <see cref="MinecraftService"/>; no external redirects.
/// </summary>
public static class MinecraftServerService
{
    private const string SettingKey = "mc.server.dir";
    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        // PaperMC Fill v3 requires a descriptive User-Agent.
        h.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge/1.0 (Minecraft server setupper)");
        return h;
    }

    // ── Java / Maven helpers (shared with MinecraftService) ──────────────────
    public static string? FindJava() => MinecraftService.FindJava();
    public static bool HasJava() => MinecraftService.HasJava();
    public static bool HasMaven() => MinecraftService.HasMaven();
    public static Task<bool> AutoInstallJdk(CancellationToken ct = default) => MinecraftService.AutoInstallJdk(ct);

    /// <summary>搵 git.exe（PATH）· Locate git on PATH.</summary>
    public static string? FindGit()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try { var g = Path.Combine(dir.Trim(), "git.exe"); if (File.Exists(g)) return g; } catch { }
        }
        // common install root
        var pf = Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Git\cmd\git.exe");
        if (File.Exists(pf)) return pf;
        return null;
    }

    public static bool HasGit() => FindGit() is not null;

    /// <summary>搵 mvn（PATH）· Locate Maven on PATH (mirrors MinecraftService internal).</summary>
    public static string? FindMaven()
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

    // ── Server folder ────────────────────────────────────────────────────────

    /// <summary>已儲存嘅伺服器資料夾（預設係 Documents\MinecraftServer）· Persisted server directory.</summary>
    public static string ServerDir
    {
        get
        {
            var saved = SettingsStore.Get(SettingKey, "");
            if (!string.IsNullOrWhiteSpace(saved)) return saved;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MinecraftServer");
        }
    }

    public static void SetServerDir(string path) => SettingsStore.Set(SettingKey, path);

    public static string ServerJarPath => Path.Combine(ServerDir, "server.jar");
    public static bool HasServerJar => File.Exists(ServerJarPath);
    public static string PluginsDir => Path.Combine(ServerDir, "plugins");

    private static void EnsureServerDir() => Directory.CreateDirectory(ServerDir);

    // ── Paper (PaperMC Fill v3 API) ──────────────────────────────────────────

    private sealed class PaperProject
    {
        [JsonPropertyName("versions")] public Dictionary<string, List<string>>? Versions { get; set; }
    }

    private sealed class PaperBuild
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("channel")] public string? Channel { get; set; }
        [JsonPropertyName("downloads")] public Dictionary<string, PaperDownload>? Downloads { get; set; }
    }

    private sealed class PaperDownload
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
    }

    /// <summary>
    /// 攞 Paper 嘅版本清單（最新喺前）· Fetch Paper's available versions (newest first).
    /// </summary>
    public static async Task<(bool ok, List<string> versions, string error)> GetPaperVersions(CancellationToken ct = default)
    {
        try
        {
            var json = await Http.GetStringAsync("https://fill.papermc.io/v3/projects/paper", ct);
            var proj = JsonSerializer.Deserialize<PaperProject>(json);
            var list = new List<string>();
            if (proj?.Versions is not null)
            {
                // Versions is a map of minecraft-version-group → [versions]; flatten and keep order newest-first.
                foreach (var kv in proj.Versions)
                    foreach (var v in kv.Value)
                        if (!list.Contains(v)) list.Add(v);
            }
            // The API returns oldest→newest within groups; present newest first.
            list.Reverse();
            if (list.Count == 0) return (false, list, "No versions returned by the PaperMC API.");
            return (true, list, "");
        }
        catch (Exception ex)
        {
            return (false, new List<string>(), ex.Message);
        }
    }

    /// <summary>
    /// 下載指定版本嘅最新穩定 Paper build 做 server.jar · Download the latest stable Paper build for a
    /// version into server.jar. Streams progress (downloaded / total bytes).
    /// </summary>
    public static async Task<TweakResult> DownloadPaper(string version, Action<long, long>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(version))
            return TweakResult.Fail("Pick a version first.", "請先揀版本。");
        try
        {
            EnsureServerDir();
            var buildsJson = await Http.GetStringAsync(
                $"https://fill.papermc.io/v3/projects/paper/versions/{version}/builds", ct);
            var builds = JsonSerializer.Deserialize<List<PaperBuild>>(buildsJson) ?? new();
            if (builds.Count == 0)
                return TweakResult.Fail($"No builds for {version}.", $"{version} 冇任何 build。");

            // Prefer a stable (default) channel build; fall back to the newest of any channel.
            var build = builds.FirstOrDefault(b => string.Equals(b.Channel, "STABLE", StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(b.Channel, "default", StringComparison.OrdinalIgnoreCase))
                        ?? builds.OrderByDescending(b => b.Id).First();

            string? url = null;
            if (build.Downloads is not null)
            {
                if (build.Downloads.TryGetValue("server:default", out var d) && d.Url is not null) url = d.Url;
                else url = build.Downloads.Values.FirstOrDefault(x => x.Url is not null)?.Url;
            }
            if (url is null)
                return TweakResult.Fail("Build has no server download.", "呢個 build 冇 server 下載連結。");

            await DownloadToFile(url, ServerJarPath, progress, ct);
            return TweakResult.Ok(
                $"Downloaded Paper {version} (build #{build.Id}) → server.jar.",
                $"已下載 Paper {version}（build #{build.Id}）→ server.jar。");
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Download cancelled.", "下載已取消。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    private static async Task DownloadToFile(string url, string dest, Action<long, long>? progress, CancellationToken ct)
    {
        using var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var tmp = dest + ".part";
        await using (var file = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, n), ct);
                read += n;
                progress?.Invoke(read, total);
            }
        }
        if (File.Exists(dest)) File.Delete(dest);
        File.Move(tmp, dest);
    }

    // ── Spigot (BuildTools) ──────────────────────────────────────────────────

    private const string BuildToolsUrl =
        "https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar";

    public static string BuildToolsDir => Path.Combine(ServerDir, "buildtools");
    public static string BuildToolsJar => Path.Combine(BuildToolsDir, "BuildTools.jar");

    /// <summary>下載 BuildTools.jar · Download BuildTools.jar into the server's buildtools folder.</summary>
    public static async Task<TweakResult> DownloadBuildTools(Action<long, long>? progress = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(BuildToolsDir);
            await DownloadToFile(BuildToolsUrl, BuildToolsJar, progress, ct);
            return TweakResult.Ok("Downloaded BuildTools.jar.", "已下載 BuildTools.jar。");
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Download cancelled.", "下載已取消。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>
    /// 用 BuildTools 編譯 Spigot · Compile Spigot via BuildTools (java -jar BuildTools.jar --rev &lt;ver&gt;),
    /// then copy the produced spigot-*.jar to server.jar. Streams the (verbose, slow) build output.
    /// </summary>
    public static async Task<TweakResult> BuildSpigot(string version, Action<string> onOutput, CancellationToken ct = default)
    {
        var java = FindJava();
        if (java is null)
            return TweakResult.Fail("Java not found. Install a JDK (21+).", "搵唔到 Java。請安裝 JDK（21+）。");
        if (!File.Exists(BuildToolsJar))
            return TweakResult.Fail("BuildTools.jar not found. Download it first.", "搵唔到 BuildTools.jar。請先下載。");
        if (string.IsNullOrWhiteSpace(version))
            return TweakResult.Fail("Pick a version first (e.g. 1.21.4 or 'latest').", "請先揀版本（例如 1.21.4 或 'latest'）。");

        var psi = new ProcessStartInfo
        {
            FileName = java,
            Arguments = $"-jar \"{BuildToolsJar}\" --rev {version.Trim()}",
            WorkingDirectory = BuildToolsDir,
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
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
            if (!p.Start()) return TweakResult.Fail("Failed to start BuildTools.", "啟動 BuildTools 失敗。");
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { } });
            await p.WaitForExitAsync(ct);

            if (p.ExitCode != 0)
                return TweakResult.Fail($"BuildTools exited with code {p.ExitCode}.", $"BuildTools 結束代碼 {p.ExitCode}。");

            // Find the produced spigot jar (newest) and copy to server.jar.
            var spigot = Directory.EnumerateFiles(BuildToolsDir, "spigot-*.jar", SearchOption.TopDirectoryOnly)
                .OrderByDescending(f => new FileInfo(f).LastWriteTimeUtc)
                .FirstOrDefault();
            if (spigot is null)
                return TweakResult.Fail("Build finished but no spigot-*.jar was produced.", "建置完成但搵唔到 spigot-*.jar。");

            EnsureServerDir();
            File.Copy(spigot, ServerJarPath, overwrite: true);
            return TweakResult.Ok(
                $"Built Spigot {version} and copied {Path.GetFileName(spigot)} → server.jar.",
                $"已編譯 Spigot {version} 並複製 {Path.GetFileName(spigot)} → server.jar。");
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Build cancelled.", "建置已取消。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    // ── EULA ─────────────────────────────────────────────────────────────────

    public static string EulaPath => Path.Combine(ServerDir, "eula.txt");

    public static bool IsEulaAccepted()
    {
        try
        {
            if (!File.Exists(EulaPath)) return false;
            return File.ReadAllLines(EulaPath)
                .Any(l => l.Replace(" ", "").StartsWith("eula=true", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    /// <summary>寫 eula.txt（明確使用者動作）· Write eula.txt — must be an explicit user action.</summary>
    public static TweakResult SetEula(bool accepted)
    {
        try
        {
            EnsureServerDir();
            File.WriteAllText(EulaPath,
                "# Accepted via WinForge. By setting eula=true you agree to the Minecraft EULA (https://aka.ms/MinecraftEULA).\n"
                + $"eula={(accepted ? "true" : "false")}\n");
            return accepted
                ? TweakResult.Ok("EULA accepted (eula=true).", "已接受 EULA（eula=true）。")
                : TweakResult.Ok("EULA set to false.", "已將 EULA 設為 false。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    // ── server.properties ────────────────────────────────────────────────────

    public static string PropertiesPath => Path.Combine(ServerDir, "server.properties");

    /// <summary>讀 server.properties 做 key=value 字典（保留次序）· Read server.properties as ordered map.</summary>
    public static Dictionary<string, string> ReadProperties()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(PropertiesPath)) return map;
            foreach (var raw in File.ReadAllLines(PropertiesPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var key = line.Substring(0, i).Trim();
                var val = line.Substring(i + 1);
                map[key] = val;
            }
        }
        catch { }
        return map;
    }

    /// <summary>讀完整 server.properties 文字 · Read the raw server.properties text.</summary>
    public static string ReadPropertiesRaw()
    {
        try { return File.Exists(PropertiesPath) ? File.ReadAllText(PropertiesPath) : ""; }
        catch { return ""; }
    }

    /// <summary>合併寫入指定鍵值（保留其他行）· Write the given keys, preserving any other existing lines.</summary>
    public static TweakResult WriteProperties(IDictionary<string, string> updates)
    {
        try
        {
            EnsureServerDir();
            var lines = File.Exists(PropertiesPath) ? File.ReadAllLines(PropertiesPath).ToList() : new List<string>();
            var remaining = new Dictionary<string, string>(updates, StringComparer.OrdinalIgnoreCase);

            for (int idx = 0; idx < lines.Count; idx++)
            {
                var line = lines[idx].Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var key = line.Substring(0, i).Trim();
                if (remaining.TryGetValue(key, out var nv))
                {
                    lines[idx] = $"{key}={nv}";
                    remaining.Remove(key);
                }
            }
            foreach (var kv in remaining) lines.Add($"{kv.Key}={kv.Value}");

            File.WriteAllLines(PropertiesPath, lines);
            return TweakResult.Ok("Saved server.properties.", "已儲存 server.properties。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>直接覆寫 server.properties 全文 · Overwrite the raw server.properties text.</summary>
    public static TweakResult WritePropertiesRaw(string text)
    {
        try
        {
            EnsureServerDir();
            File.WriteAllText(PropertiesPath, text);
            return TweakResult.Ok("Saved server.properties.", "已儲存 server.properties。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    // ── Memory + start script ────────────────────────────────────────────────

    /// <summary>
    /// 產生 start.bat（記憶體旗標 + 可選 Aikar 旗標）· Generate start.bat with -Xms/-Xmx and optional
    /// Aikar's flags. Returns the script path.
    /// </summary>
    public static TweakResult GenerateStartScript(int xmsMb, int xmxMb, bool aikarFlags)
    {
        try
        {
            EnsureServerDir();
            var java = FindJava();
            var javaCmd = java is not null ? $"\"{java}\"" : "java";
            var flags = new StringBuilder();
            flags.Append($"-Xms{xmsMb}M -Xmx{xmxMb}M ");
            if (aikarFlags)
            {
                // Aikar's flags (https://docs.papermc.io/paper/aikars-flags).
                flags.Append("-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 ");
                flags.Append("-XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch ");
                flags.Append("-XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 -XX:G1HeapRegionSize=8M ");
                flags.Append("-XX:G1ReservePercent=20 -XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 ");
                flags.Append("-XX:InitiatingHeapOccupancyPercent=15 -XX:G1MixedGCLiveThresholdPercent=90 ");
                flags.Append("-XX:G1RSetUpdatingPauseTimePercent=5 -XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem ");
                flags.Append("-XX:MaxTenuringThreshold=1 -Dusing.aikars.flags=https://mcflags.emc.gs -Daikars.new.flags=true ");
            }
            var script =
                "@echo off\r\n" +
                "cd /d \"%~dp0\"\r\n" +
                $"{javaCmd} {flags}-jar server.jar nogui\r\n" +
                "pause\r\n";
            var path = Path.Combine(ServerDir, "start.bat");
            File.WriteAllText(path, script);
            return TweakResult.Ok($"Wrote start.bat ({xmsMb}–{xmxMb} MB).", $"已寫入 start.bat（{xmsMb}–{xmxMb} MB）。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    // ── Run the server (tracked process with stdin console) ──────────────────

    private static Process? _proc;
    private static readonly object _gate = new();

    public static bool IsRunning
    {
        get { lock (_gate) { return _proc is { HasExited: false }; } }
    }

    /// <summary>
    /// 啟動伺服器 · Start the server (java -Xms.. -Xmx.. -jar server.jar nogui) with stdin redirected so the
    /// console command box can send commands. Streams stdout/stderr to <paramref name="onOutput"/>.
    /// </summary>
    public static TweakResult Start(int xmsMb, int xmxMb, bool aikarFlags, Action<string> onOutput, Action onExit)
    {
        lock (_gate)
        {
            if (_proc is { HasExited: false })
                return TweakResult.Fail("The server is already running.", "伺服器已經喺度運行緊。");
        }

        var java = FindJava();
        if (java is null)
            return TweakResult.Fail("Java not found. Install a JDK (21+).", "搵唔到 Java。請安裝 JDK（21+）。");
        if (!HasServerJar)
            return TweakResult.Fail("server.jar not found. Download Paper or build Spigot first.", "搵唔到 server.jar。請先下載 Paper 或編譯 Spigot。");
        if (!IsEulaAccepted())
            return TweakResult.Fail("Accept the EULA before starting.", "啟動前請先接受 EULA。");

        var flags = new StringBuilder();
        flags.Append($"-Xms{xmsMb}M -Xmx{xmxMb}M ");
        if (aikarFlags)
            flags.Append("-XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions -XX:+DisableExplicitGC -XX:+AlwaysPreTouch ");
        var args = $"{flags}-jar \"{ServerJarPath}\" nogui";

        var psi = new ProcessStartInfo
        {
            FileName = java,
            Arguments = args,
            WorkingDirectory = ServerDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
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
                $"Server starting ({xmsMb}–{xmxMb} MB). Watch the console below.",
                $"伺服器啟動緊（{xmsMb}–{xmxMb} MB）。睇下面嘅主控台。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>
    /// 向伺服器 stdin 送一句指令（例如 stop / say / op）· Send a command to the server's stdin.
    /// </summary>
    public static TweakResult SendCommand(string command)
    {
        Process? p;
        lock (_gate) { p = _proc; }
        if (p is null || p.HasExited)
            return TweakResult.Fail("The server is not running.", "伺服器冇喺度運行。");
        try
        {
            p.StandardInput.WriteLine(command);
            p.StandardInput.Flush();
            return TweakResult.Ok($"Sent: {command}", $"已送出：{command}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>
    /// 優雅停止 · Graceful stop: send "stop" to stdin, wait, then kill the tree on timeout.
    /// </summary>
    public static async Task<TweakResult> StopGraceful(int timeoutSeconds = 20)
    {
        Process? p;
        lock (_gate) { p = _proc; }
        if (p is null || p.HasExited)
            return TweakResult.Fail("The server is not running.", "伺服器冇喺度運行。");
        try
        {
            try { p.StandardInput.WriteLine("stop"); p.StandardInput.Flush(); } catch { }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try { await p.WaitForExitAsync(cts.Token); }
            catch (OperationCanceledException)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
            }
            lock (_gate) { _proc = null; }
            return TweakResult.Ok("Server stopped.", "伺服器已停止。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>強制停止（殺整棵 process tree）· Force stop — kill the process tree.</summary>
    public static TweakResult Kill()
    {
        lock (_gate)
        {
            if (_proc is null || _proc.HasExited)
                return TweakResult.Fail("The server is not running.", "伺服器冇喺度運行。");
            try
            {
                _proc.Kill(entireProcessTree: true);
                _proc = null;
                return TweakResult.Ok("Server killed.", "已強制停止伺服器。");
            }
            catch (Exception ex)
            {
                return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
            }
        }
    }

    // ── Plugin builds (git clone + Maven / Gradle) ───────────────────────────

    /// <summary>偵測到嘅建置系統 · Detected build system.</summary>
    public enum BuildSystem { None, Maven, Gradle }

    /// <summary>
    /// 偵測一個資料夾嘅建置系統 · Detect a checkout's build system. Gradle (wrapper) wins if both present.
    /// </summary>
    public static BuildSystem DetectBuildSystem(string dir)
    {
        if (File.Exists(Path.Combine(dir, "gradlew.bat")) || File.Exists(Path.Combine(dir, "build.gradle"))
            || File.Exists(Path.Combine(dir, "build.gradle.kts")))
            return BuildSystem.Gradle;
        if (File.Exists(Path.Combine(dir, "pom.xml")))
            return BuildSystem.Maven;
        return BuildSystem.None;
    }

    /// <summary>
    /// 由 git 來源建置外掛並放入 plugins/ · Clone a plugin's git repo, build it (Maven or Gradle), and copy
    /// the produced jar into plugins/. Streams all output. Untrusted source builds run arbitrary build
    /// scripts — the caller must confirm intent.
    /// </summary>
    public static async Task<TweakResult> BuildPluginFromGit(string gitUrl, string pluginName, Action<string> onOutput,
        BuildSystem? forceSystem = null, CancellationToken ct = default)
    {
        var git = FindGit();
        if (git is null)
            return TweakResult.Fail("git not found on PATH. Install Git.", "PATH 上搵唔到 git。請安裝 Git。");
        var java = FindJava();
        if (java is null)
            return TweakResult.Fail("Java not found. Install a JDK (21+).", "搵唔到 Java。請安裝 JDK（21+）。");
        if (string.IsNullOrWhiteSpace(gitUrl))
            return TweakResult.Fail("Enter a git URL.", "請輸入 git URL。");

        try
        {
            EnsureServerDir();
            var workRoot = Path.Combine(ServerDir, "plugin-build");
            Directory.CreateDirectory(workRoot);
            var safe = string.Concat((pluginName ?? "plugin").Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            if (string.IsNullOrWhiteSpace(safe)) safe = "plugin";
            var checkout = Path.Combine(workRoot, safe);

            // Clone (or pull if already present).
            if (Directory.Exists(Path.Combine(checkout, ".git")))
            {
                onOutput($"[git] updating {checkout}");
                var pull = await RunStreaming(git, "pull --ff-only", checkout, onOutput, null, ct);
                if (!pull) onOutput("[git] pull failed; using existing checkout");
            }
            else
            {
                if (Directory.Exists(checkout)) { try { Directory.Delete(checkout, true); } catch { } }
                onOutput($"[git] cloning {gitUrl}");
                var clone = await RunStreaming(git, $"clone --depth 1 \"{gitUrl}\" \"{checkout}\"", workRoot, onOutput, null, ct);
                if (!clone) return TweakResult.Fail("git clone failed (see log).", "git clone 失敗（睇記錄）。");
            }

            var system = forceSystem ?? DetectBuildSystem(checkout);
            if (system == BuildSystem.None)
                return TweakResult.Fail("No pom.xml / build.gradle found in the repo.", "repo 入面搵唔到 pom.xml／build.gradle。");

            // Record jars present before, so we can find the newly produced one.
            var before = Directory.EnumerateFiles(checkout, "*.jar", SearchOption.AllDirectories).ToHashSet(StringComparer.OrdinalIgnoreCase);

            bool built;
            if (system == BuildSystem.Maven)
            {
                var mvn = FindMaven();
                if (mvn is null)
                    return TweakResult.Fail("Maven not found. Install it (winget Apache.Maven) to build Maven plugins.",
                        "搵唔到 Maven。請安裝（winget Apache.Maven）先可以建置 Maven 外掛。");
                onOutput("[maven] mvn -B -DskipTests package");
                built = await RunStreaming(mvn, "-B -DskipTests package", checkout, onOutput, JavaHome(java), ct);
            }
            else
            {
                var gradlew = Path.Combine(checkout, "gradlew.bat");
                string file, args;
                if (File.Exists(gradlew)) { file = gradlew; args = "shadowJar build -x test --no-daemon"; }
                else { file = "cmd.exe"; args = "/c gradle shadowJar build -x test --no-daemon"; }
                onOutput($"[gradle] {Path.GetFileName(file)} {args}");
                built = await RunStreaming(file, args, checkout, onOutput, JavaHome(java), ct);
                if (!built)
                {
                    // some projects have no shadowJar task — retry plain build
                    onOutput("[gradle] retrying: build -x test --no-daemon");
                    built = await RunStreaming(file, "build -x test --no-daemon", checkout, onOutput, JavaHome(java), ct);
                }
            }

            // Find the produced jar (prefer shaded/-all, exclude sources/javadoc).
            var candidates = Directory.EnumerateFiles(checkout, "*.jar", SearchOption.AllDirectories)
                .Where(f => !before.Contains(f))
                .Where(f =>
                {
                    var n = Path.GetFileName(f).ToLowerInvariant();
                    return !n.Contains("sources") && !n.Contains("javadoc") && !n.Contains("-part")
                           && !f.Replace('\\', '/').Contains("/buildtools/");
                })
                .OrderByDescending(f =>
                {
                    var n = Path.GetFileName(f).ToLowerInvariant();
                    return (n.Contains("-all") || n.Contains("shadow") ? 2 : 0) + (n.Contains("snapshot") ? 0 : 1);
                })
                .ThenByDescending(f => new FileInfo(f).Length)
                .ToList();

            if (candidates.Count == 0)
            {
                if (!built) return TweakResult.Fail("Build failed and no jar was produced (see log).", "建置失敗，亦冇產生 jar（睇記錄）。");
                return TweakResult.Fail("Build finished but no plugin jar was found.", "建置完成但搵唔到外掛 jar。");
            }

            var jar = candidates[0];
            Directory.CreateDirectory(PluginsDir);
            var dest = Path.Combine(PluginsDir, Path.GetFileName(jar));
            File.Copy(jar, dest, overwrite: true);
            onOutput($"[done] {Path.GetFileName(jar)} → plugins/");
            return TweakResult.Ok(
                $"Built {pluginName} → plugins/{Path.GetFileName(jar)}.",
                $"已建置 {pluginName} → plugins/{Path.GetFileName(jar)}。");
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Build cancelled.", "建置已取消。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    private static string? JavaHome(string? java)
    {
        if (java is null) return null;
        try { return Directory.GetParent(Path.GetDirectoryName(java)!)?.FullName; } catch { return null; }
    }

    /// <summary>執行一個程序，串流輸出，可取消 · Run a process streaming output; returns exit==0.</summary>
    private static async Task<bool> RunStreaming(string file, string args, string workDir, Action<string> onOutput,
        string? javaHome, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (javaHome is not null) psi.Environment["JAVA_HOME"] = javaHome;

        using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onOutput(e.Data); };
        if (!p.Start()) { onOutput($"[error] failed to start {file}"); return false; }
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        using var reg = ct.Register(() => { try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { } });
        await p.WaitForExitAsync(ct);
        return p.ExitCode == 0;
    }
}
