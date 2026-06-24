using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// ViaProxy 包裝（Minecraft 版本代理）· Wraps the ViaProxy Java jar (ViaVersion/ViaBackwards/ViaRewind)
/// so a modern client can join servers of (almost) any Minecraft version and vice-versa. Mirrors
/// <see cref="MinecraftService"/>: locates <c>java.exe</c> (JAVA_HOME → PATH → common JDK roots), offers a
/// winget JDK install, downloads the latest <c>ViaProxy.jar</c> from GitHub releases to
/// <c>%LOCALAPPDATA%\WinForge\viaproxy</c>, then runs it headless via the <c>cli</c> sub-command as a
/// tracked process and streams its live output. GPL-3.0 — the jar is downloaded on demand, never bundled.
/// </summary>
public static class ViaProxyService
{
    public const string SourceUrl = "https://github.com/ViaVersion/ViaProxy";
    public const string LicenseUrl = "https://github.com/ViaVersion/ViaProxy/blob/main/LICENSE";
    private const string ReleasesApi = "https://api.github.com/repos/ViaVersion/ViaProxy/releases/latest";

    private static readonly HttpClient Http = BuildHttp();

    private static HttpClient BuildHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        h.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("WinForge", "1.0"));
        h.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return h;
    }

    // ── App-data folder + jar ────────────────────────────────────────────────

    /// <summary>%LOCALAPPDATA%\WinForge\viaproxy · The app-data folder ViaProxy runs from.</summary>
    public static string DataDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "viaproxy");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }
    }

    /// <summary>下載到嘅 jar（搵唔到就 null）· The downloaded ViaProxy jar, or null if absent.</summary>
    public static string? FindJar()
    {
        // explicit override first (custom jar picked by the user)
        var saved = SettingsStore.Get("viaproxy.jar", "");
        if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved)) return saved;

        try
        {
            // Newest ViaProxy*.jar in the data dir.
            var jars = Directory.GetFiles(DataDir, "ViaProxy*.jar")
                .OrderByDescending(File.GetLastWriteTimeUtc).ToList();
            return jars.Count > 0 ? jars[0] : null;
        }
        catch { return null; }
    }

    public static bool HasJar() => FindJar() is not null;

    public static void SetJar(string path) => SettingsStore.Set("viaproxy.jar", path);

    // ── Java / JDK location (mirrors MinecraftService) ───────────────────────

    /// <summary>搵 java.exe（JAVA_HOME、PATH，或常見安裝位置）· Locate java.exe.</summary>
    public static string? FindJava()
    {
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var j = Path.Combine(javaHome, "bin", "java.exe");
            if (File.Exists(j)) return j;
        }
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            try { var j = Path.Combine(dir.Trim(), "java.exe"); if (File.Exists(j)) return j; } catch { }
        }
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
                    var j2 = Path.Combine(sub, "java.exe");
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

    // ── Download the jar from GitHub releases ────────────────────────────────

    /// <summary>
    /// 下載最新 ViaProxy.jar · Download the latest ViaProxy jar from GitHub releases into <see cref="DataDir"/>.
    /// Returns the saved jar path on success. <paramref name="onProgress"/> receives status lines.
    /// </summary>
    public static async Task<(bool ok, string jar, string log)> DownloadLatestJar(
        Action<string>? onProgress = null, CancellationToken ct = default)
    {
        void P(string s) => onProgress?.Invoke(s);
        try
        {
            P("Querying GitHub for the latest ViaProxy release…");
            var json = await Http.GetStringAsync(ReleasesApi, ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";

            string? url = null;
            string assetName = "ViaProxy.jar";
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                // Prefer the main runnable jar (ViaProxy-x.y.z.jar), skip *-sources / *-javadoc.
                foreach (var a in assets.EnumerateArray())
                {
                    var name = a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Contains("sources", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Contains("javadoc", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!name.StartsWith("ViaProxy", StringComparison.OrdinalIgnoreCase)) continue;
                    url = a.TryGetProperty("browser_download_url", out var d) ? d.GetString() : null;
                    if (url is not null) { assetName = name; break; }
                }
            }
            if (url is null)
                return (false, "", "Could not find a ViaProxy*.jar asset in the latest release.");

            var dest = Path.Combine(DataDir, assetName);
            P($"Downloading {assetName} ({tag})…");
            using (var resp = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None);
                await src.CopyToAsync(fs, ct);
            }
            SettingsStore.Set("viaproxy.jar", dest);
            P($"Saved → {dest}");
            return (true, dest, $"Downloaded ViaProxy {tag}: {assetName}");
        }
        catch (Exception ex)
        {
            return (false, "", ex.Message);
        }
    }

    // ── Common target versions for the dropdown ──────────────────────────────

    /// <summary>
    /// 常見目標版本（畀下拉選單）· A curated list of common ViaProxy target versions for the dropdown. The jar
    /// supports many more — "AUTO" lets ViaProxy auto-detect the server's version.
    /// </summary>
    public static IReadOnlyList<string> CommonVersions { get; } = new[]
    {
        "AUTO",
        "1.21.4", "1.21.2", "1.21", "1.20.6", "1.20.5", "1.20.3", "1.20.2", "1.20",
        "1.19.4", "1.19.2", "1.19", "1.18.2", "1.18", "1.17.1", "1.17", "1.16.5",
        "1.16.4", "1.16.3", "1.16.1", "1.15.2", "1.14.4", "1.13.2", "1.12.2",
        "1.11.2", "1.10.2", "1.9.4", "1.8.x", "1.7.10", "b1.8.1", "c0.30",
    };

    // ── Run the proxy (tracked process) ──────────────────────────────────────

    private static Process? _proc;
    private static readonly object _gate = new();

    public static bool IsRunning
    {
        get { lock (_gate) { return _proc is { HasExited: false }; } }
    }

    /// <summary>代理運行設定 · Settings for a ViaProxy run.</summary>
    public sealed class RunOptions
    {
        /// <summary>本機綁定主機（預設 127.0.0.1）· Bind host (default 127.0.0.1).</summary>
        public string BindHost { get; set; } = "127.0.0.1";
        /// <summary>本機綁定埠（預設 25568）· Bind port (default 25568).</summary>
        public int BindPort { get; set; } = 25568;
        /// <summary>目標伺服器 host（必填）· Target server host (required).</summary>
        public string TargetHost { get; set; } = "";
        /// <summary>目標伺服器埠（預設 25565）· Target server port (default 25565).</summary>
        public int TargetPort { get; set; } = 25565;
        /// <summary>目標 MC 版本（AUTO 或具體版本）· Target MC version (AUTO or a specific version).</summary>
        public string TargetVersion { get; set; } = "AUTO";
        /// <summary>認證方法：none / account · Auth method.</summary>
        public string AuthMethod { get; set; } = "none";
        /// <summary>代理線上模式 · Proxy online mode (shows skins / signed chat on online-mode servers).</summary>
        public bool ProxyOnlineMode { get; set; }
        /// <summary>後端 SOCKS/HTTP 代理 URL（可空）· Upstream SOCKS/HTTP proxy URL (optional).</summary>
        public string BackendProxyUrl { get; set; } = "";
        /// <summary>允許 ping b1.7.3 或更舊伺服器 · Allow pinging very old (≤ b1.7.3) servers.</summary>
        public bool AllowBetaPinging { get; set; }
        /// <summary>BetaCraft 認證（classic 伺服器）· BetaCraft auth for classic online-mode servers.</summary>
        public bool BetacraftAuth { get; set; }
    }

    public static string LocalAddress(RunOptions o) => $"{o.BindHost}:{o.BindPort}";

    /// <summary>
    /// 啟動代理 · Start the proxy headless via the <c>cli</c> sub-command. Streams stdout/stderr to
    /// <paramref name="onOutput"/> (caller marshals to the UI thread) and calls <paramref name="onExit"/>.
    /// </summary>
    public static TweakResult Start(string jar, RunOptions opt, Action<string> onOutput, Action onExit)
    {
        lock (_gate)
        {
            if (_proc is { HasExited: false })
                return TweakResult.Fail("ViaProxy is already running.", "ViaProxy 已經喺度運行緊。");
        }

        var java = FindJava();
        if (java is null)
            return TweakResult.Fail("Java not found. Install a JDK (21+).", "搵唔到 Java。請安裝 JDK（21+）。");
        if (string.IsNullOrWhiteSpace(jar) || !File.Exists(jar))
            return TweakResult.Fail("ViaProxy.jar not found. Download it first.", "搵唔到 ViaProxy.jar。請先下載。");
        if (string.IsNullOrWhiteSpace(opt.TargetHost))
            return TweakResult.Fail("Enter the target server address.", "請輸入目標伺服器位址。");

        var bind = $"{(string.IsNullOrWhiteSpace(opt.BindHost) ? "127.0.0.1" : opt.BindHost.Trim())}:{opt.BindPort}";
        var target = $"{opt.TargetHost.Trim()}:{opt.TargetPort}";

        var args = new StringBuilder();
        args.Append($"-jar \"{jar}\" cli");
        args.Append($" --bind-address \"{bind}\"");
        args.Append($" --target-address \"{target}\"");
        args.Append($" --target-version \"{opt.TargetVersion}\"");
        args.Append($" --auth-method \"{(string.IsNullOrWhiteSpace(opt.AuthMethod) ? "none" : opt.AuthMethod)}\"");
        if (opt.ProxyOnlineMode) args.Append(" --proxy-online-mode true");
        if (opt.AllowBetaPinging) args.Append(" --allow-beta-pinging true");
        if (opt.BetacraftAuth) args.Append(" --betacraft-auth true");
        if (!string.IsNullOrWhiteSpace(opt.BackendProxyUrl))
            args.Append($" --backend-proxy-url \"{opt.BackendProxyUrl.Trim()}\"");

        var psi = new ProcessStartInfo
        {
            FileName = java,
            Arguments = args.ToString(),
            WorkingDirectory = DataDir,
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
                $"ViaProxy started on {bind} → {target} (v{opt.TargetVersion}). Connect Minecraft to {bind}.",
                $"ViaProxy 已喺 {bind} → {target}（版本 {opt.TargetVersion}）啟動。用 Minecraft 連去 {bind}。");
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
                return TweakResult.Fail("ViaProxy is not running.", "ViaProxy 冇喺度運行。");
            try
            {
                _proc.Kill(entireProcessTree: true);
                _proc = null;
                return TweakResult.Ok("Stopped ViaProxy.", "已停止 ViaProxy。");
            }
            catch (Exception ex)
            {
                return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
            }
        }
    }

    // ── Saved config (persists last form values) ─────────────────────────────

    public static void SaveConfig(RunOptions o)
    {
        SettingsStore.Set("viaproxy.bindHost", o.BindHost);
        SettingsStore.Set("viaproxy.bindPort", o.BindPort.ToString());
        SettingsStore.Set("viaproxy.targetHost", o.TargetHost);
        SettingsStore.Set("viaproxy.targetPort", o.TargetPort.ToString());
        SettingsStore.Set("viaproxy.targetVersion", o.TargetVersion);
        SettingsStore.Set("viaproxy.authMethod", o.AuthMethod);
        SettingsStore.Set("viaproxy.onlineMode", o.ProxyOnlineMode ? "1" : "0");
        SettingsStore.Set("viaproxy.backendProxy", o.BackendProxyUrl);
        SettingsStore.Set("viaproxy.allowBetaPing", o.AllowBetaPinging ? "1" : "0");
        SettingsStore.Set("viaproxy.betacraft", o.BetacraftAuth ? "1" : "0");
    }

    public static RunOptions LoadConfig()
    {
        int I(string k, int d) => int.TryParse(SettingsStore.Get(k, ""), out var v) ? v : d;
        return new RunOptions
        {
            BindHost = SettingsStore.Get("viaproxy.bindHost", "127.0.0.1"),
            BindPort = I("viaproxy.bindPort", 25568),
            TargetHost = SettingsStore.Get("viaproxy.targetHost", ""),
            TargetPort = I("viaproxy.targetPort", 25565),
            TargetVersion = SettingsStore.Get("viaproxy.targetVersion", "AUTO"),
            AuthMethod = SettingsStore.Get("viaproxy.authMethod", "none"),
            ProxyOnlineMode = SettingsStore.Get("viaproxy.onlineMode", "0") == "1",
            BackendProxyUrl = SettingsStore.Get("viaproxy.backendProxy", ""),
            AllowBetaPinging = SettingsStore.Get("viaproxy.allowBetaPing", "0") == "1",
            BetacraftAuth = SettingsStore.Get("viaproxy.betacraft", "0") == "1",
        };
    }
}
