using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

/// <summary>Blender MCP server 設定 · Blender MCP server command/config payload.</summary>
public sealed record BlenderMcpServerConfig(
    string Name = "blender",
    string Command = "uvx",
    IReadOnlyList<string>? Args = null,
    IReadOnlyDictionary<string, string>? Env = null)
{
    public IReadOnlyList<string> EffectiveArgs => Args is { Count: > 0 } ? Args : new[] { "blender-mcp" };
}

/// <summary>一個可儲存／可同時運行嘅 Blender MCP 實例 · One saved/runnable Blender MCP instance.</summary>
public sealed class BlenderMcpInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "blender";
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 9876;
    public bool DisableTelemetry { get; set; } = true;
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public string Display => $"{Name} · {Host}:{Port}";
}

/// <summary>MCP 整合目標 · One AI-agent MCP integration target.</summary>
public sealed record BlenderMcpIntegrationTarget(string Id, string NameEn, string NameZh, string ConfigPath, string NotesEn, string NotesZh);

/// <summary>MCP 健康檢查結果 · Blender MCP deployment/config health.</summary>
public sealed record BlenderMcpHealth(bool BlenderFound, bool ServerCommandFound, bool ProbeOk, string BlenderPath, string MessageEn, string MessageZh);

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
    private static readonly object McpGate = new();
    private static readonly Dictionary<string, Process> McpProcesses = new();

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

    // ── Blender MCP server deployment + agent config helpers ────────────────

    public static BlenderMcpServerConfig DefaultMcpConfig()
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var exe = FindBlender();
        if (exe.Length > 0) env["BLENDER_EXECUTABLE"] = exe;
        return new BlenderMcpServerConfig(Env: env);
    }

    public static BlenderMcpServerConfig ConfigFor(BlenderMcpInstance inst)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BLENDER_HOST"] = inst.Host,
            ["BLENDER_PORT"] = inst.Port.ToString(),
        };
        if (inst.DisableTelemetry) env["DISABLE_TELEMETRY"] = "true";
        var exe = FindBlender();
        if (exe.Length > 0) env["BLENDER_EXECUTABLE"] = exe;
        return new BlenderMcpServerConfig(CleanMcpName(inst.Name), "uvx", new[] { "blender-mcp" }, env);
    }

    public static string McpDir
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "blender-mcp");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }
    }

    public static string McpInstancesFile => Path.Combine(McpDir, "instances.json");
    public static string McpAddonPath => Path.Combine(McpDir, "addon.py");

    public static IReadOnlyList<BlenderMcpInstance> LoadMcpInstances()
    {
        try
        {
            if (File.Exists(McpInstancesFile))
            {
                var list = JsonSerializer.Deserialize<List<BlenderMcpInstance>>(File.ReadAllText(McpInstancesFile)) ?? new();
                if (list.Count > 0) return list.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
        catch { }

        var seed = new List<BlenderMcpInstance> { new() };
        SaveMcpInstances(seed);
        return seed;
    }

    public static void SaveMcpInstances(IEnumerable<BlenderMcpInstance> instances)
    {
        Directory.CreateDirectory(McpDir);
        var json = JsonSerializer.Serialize(instances.ToList(), new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(McpInstancesFile, json, new UTF8Encoding(false));
    }

    public static BlenderMcpInstance CreateMcpInstance(string name, int port)
    {
        var all = LoadMcpInstances().ToList();
        port = Math.Clamp(port, 1024, 65535);
        if (all.Any(i => string.Equals(i.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) && i.Port == port))
            throw new InvalidOperationException($"Another Blender MCP instance already uses 127.0.0.1:{port}.");

        var clean = CleanMcpName(name);
        if (all.Any(i => string.Equals(i.Name, clean, StringComparison.OrdinalIgnoreCase)))
            clean = CleanMcpName($"{clean}-{port}");
        var inst = new BlenderMcpInstance
        {
            Name = clean,
            Port = port,
            CreatedAt = DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
        };
        all.Add(inst);
        SaveMcpInstances(all);
        return inst;
    }

    public static void DeleteMcpInstance(BlenderMcpInstance inst)
    {
        StopMcpInstance(inst);
        SaveMcpInstances(LoadMcpInstances().Where(i => i.Id != inst.Id));
    }

    public static bool IsMcpRunning(BlenderMcpInstance inst)
    {
        lock (McpGate)
            return McpProcesses.TryGetValue(inst.Id, out var p) && !p.HasExited;
    }

    public static TweakResult StartMcpInstance(BlenderMcpInstance inst, Action<string>? onOutput = null)
    {
        lock (McpGate)
            if (McpProcesses.TryGetValue(inst.Id, out var existing) && !existing.HasExited)
                return TweakResult.Ok("MCP instance is already running.", "MCP 實例已經運行緊。");

        var config = ConfigFor(inst);
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = config.Command,
                Arguments = JoinArgs(config.EffectiveArgs),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            if (config.Env is not null)
                foreach (var kv in config.Env)
                    psi.Environment[kv.Key] = kv.Value;

            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke($"[{inst.Name}] {e.Data}"); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onOutput?.Invoke($"[{inst.Name}] {e.Data}"); };
            p.Exited += (_, _) =>
            {
                lock (McpGate)
                    if (McpProcesses.TryGetValue(inst.Id, out var cur) && ReferenceEquals(cur, p))
                        McpProcesses.Remove(inst.Id);
                onOutput?.Invoke($"[{inst.Name}] MCP exited.");
            };
            if (!p.Start()) return TweakResult.Fail("Failed to start blender-mcp.", "啟動 blender-mcp 失敗。");
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            if (p.WaitForExit(1500))
            {
                return TweakResult.Fail(
                    $"blender-mcp exited immediately with code {p.ExitCode}. Run Deploy / verify server, then check uvx output.",
                    $"blender-mcp 即刻結束，代碼 {p.ExitCode}。請先執行部署／驗證伺服器，再檢查 uvx 輸出。");
            }
            lock (McpGate) McpProcesses[inst.Id] = p;
            return TweakResult.Ok($"Started {inst.Display}.", $"已啟動 {inst.Display}。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not start {config.Command}. Install uv, then deploy blender-mcp. {ex.Message}",
                $"無法啟動 {config.Command}。請先安裝 uv，再部署 blender-mcp。{ex.Message}");
        }
    }

    public static TweakResult StopMcpInstance(BlenderMcpInstance inst)
    {
        Process? p = null;
        lock (McpGate)
            if (McpProcesses.Remove(inst.Id, out var existing))
                p = existing;
        if (p is null || p.HasExited) return TweakResult.Ok("MCP instance is not running.", "MCP 實例未運行。");
        try { p.Kill(entireProcessTree: true); return TweakResult.Ok("Stopped MCP instance.", "已停止 MCP 實例。"); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static async Task<TweakResult> TestMcpBlenderSocket(BlenderMcpInstance inst, CancellationToken ct = default)
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(inst.Host, inst.Port, ct);
            return TweakResult.Ok($"Connected to Blender add-on at {inst.Host}:{inst.Port}.",
                $"已連到 Blender add-on：{inst.Host}:{inst.Port}。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not connect to Blender add-on at {inst.Host}:{inst.Port}. Start Blender and the BlenderMCP add-on socket server on this port. {ex.Message}",
                $"連唔到 {inst.Host}:{inst.Port} 嘅 Blender add-on。請開 Blender 同 BlenderMCP add-on socket server。{ex.Message}");
        }
    }

    public static async Task<TweakResult> DownloadMcpAddon(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient();
            var body = await http.GetStringAsync("https://raw.githubusercontent.com/ahujasid/blender-mcp/main/addon.py", ct);
            if (!body.Contains("BlenderMCP", StringComparison.OrdinalIgnoreCase))
                return TweakResult.Fail("Downloaded file did not look like the Blender MCP add-on.", "下載返嚟嘅檔案唔似 Blender MCP add-on。");
            await File.WriteAllTextAsync(McpAddonPath, body, new UTF8Encoding(false), ct);
            return TweakResult.Ok($"Downloaded add-on to {McpAddonPath}. Install it in Blender Preferences > Add-ons > Install.",
                $"已下載 add-on 到 {McpAddonPath}。請喺 Blender Preferences > Add-ons > Install 安裝。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static async Task<TweakResult> ExportMcpAgentBundle(BlenderMcpInstance inst, string targetDir, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(targetDir);
            var cfg = ConfigFor(inst);
            await File.WriteAllTextAsync(Path.Combine(targetDir, $"{cfg.Name}-codex.toml"), BuildCodexToml(cfg), new UTF8Encoding(false), ct);
            await File.WriteAllTextAsync(Path.Combine(targetDir, $"{cfg.Name}-mcp.json"), BuildMcpJson(cfg, false), new UTF8Encoding(false), ct);
            await File.WriteAllTextAsync(Path.Combine(targetDir, $"{cfg.Name}-opencode.json"), BuildMcpJson(cfg, true), new UTF8Encoding(false), ct);
            await File.WriteAllTextAsync(Path.Combine(targetDir, $"{cfg.Name}-claude-code.ps1"),
                $"claude mcp add {cfg.Name} -- {cfg.Command} {JoinArgs(cfg.EffectiveArgs)}{Environment.NewLine}", new UTF8Encoding(false), ct);
            var skillDir = Path.Combine(targetDir, cfg.Name);
            Directory.CreateDirectory(skillDir);
            await File.WriteAllTextAsync(Path.Combine(skillDir, "SKILL.md"), BuildMcpSkill(inst), new UTF8Encoding(false), ct);
            return TweakResult.Ok($"Exported MCP configs and agent skill to {targetDir}.", $"已匯出 MCP 設定同 agent skill 到 {targetDir}。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static async Task<TweakResult> GenerateMcpSkill(BlenderMcpInstance inst, string targetDir, CancellationToken ct = default)
    {
        try
        {
            var cfg = ConfigFor(inst);
            var skillDir = Path.Combine(targetDir, cfg.Name);
            Directory.CreateDirectory(skillDir);
            var path = Path.Combine(skillDir, "SKILL.md");
            await File.WriteAllTextAsync(path, BuildMcpSkill(inst), new UTF8Encoding(false), ct);
            return TweakResult.Ok($"Generated skill: {path}", $"已產生 skill：{path}");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static IReadOnlyList<BlenderMcpIntegrationTarget> McpTargets()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(config)) config = Path.Combine(home, ".config");
        return new List<BlenderMcpIntegrationTarget>
        {
            new("codex", "OpenAI Codex CLI", "OpenAI Codex CLI",
                Path.Combine(home, ".codex", "config.toml"),
                "Adds [mcp_servers.blender] to Codex config.", "加入 [mcp_servers.blender] 到 Codex 設定。"),
            new("claude", "Claude Code", "Claude Code",
                Path.Combine(home, ".claude", "settings.json"),
                "Writes a Claude Code MCP server entry; CLI helper is also available.", "寫入 Claude Code MCP server 項；亦可用 CLI helper。"),
            new("opencode", "OpenCode", "OpenCode",
                Path.Combine(config, "opencode", "opencode.json"),
                "Merges a local MCP server entry into OpenCode JSON.", "合併本機 MCP server 項到 OpenCode JSON。"),
        };
    }

    /// <summary>安裝 uv（如可用）同預熱 blender-mcp · Install uv if possible and warm the blender-mcp package.</summary>
    public static async Task<TweakResult> DeployBlenderMcpServer(CancellationToken ct = default)
    {
        var uv = ResolveOnPath("uvx") ?? ResolveOnPath("uv");
        if (uv is null)
        {
            var winget = ResolveOnPath("winget");
            if (winget is not null)
            {
                var r = await ShellRunner.Run("winget.exe", "install --id astral-sh.uv -e --accept-package-agreements --accept-source-agreements", false, ct);
                if (!r.Success) return r;
            }
        }

        var probe = await ProbeBlenderMcp(DefaultMcpConfig(), ct);
        return probe.ProbeOk
            ? TweakResult.Ok("Blender MCP server is installed and responds to --help.", "Blender MCP server 已安裝，並回應 --help。")
            : TweakResult.Fail(probe.MessageEn, probe.MessageZh);
    }

    /// <summary>檢查 Blender + MCP server command · Check Blender path and MCP command health.</summary>
    public static async Task<BlenderMcpHealth> ProbeBlenderMcp(BlenderMcpServerConfig? config = null, CancellationToken ct = default)
    {
        config ??= DefaultMcpConfig();
        var blender = FindBlender();
        var command = ResolveOnPath(config.Command);
        if (blender.Length == 0)
            return new(false, command is not null, false, "",
                "Blender is not installed or no blender.exe path is configured.", "未安裝 Blender，或者未設定 blender.exe 路徑。");
        if (command is null)
            return new(true, false, false, blender,
                $"{config.Command} was not found on PATH.", $"PATH 搵唔到 {config.Command}。");

        var ok = await ProbeCommand(config.Command, JoinArgs(config.EffectiveArgs.Append("--help")), config.Env, ct);
        return new(true, true, ok, blender,
            ok ? "Blender MCP server command responds." : "Blender MCP server command did not respond cleanly.",
            ok ? "Blender MCP server 指令有回應。" : "Blender MCP server 指令未能正常回應。");
    }

    /// <summary>寫入 Codex MCP 設定 · Add/update Codex [mcp_servers.blender] TOML block.</summary>
    public static TweakResult ConfigureCodexBlenderMcp(BlenderMcpServerConfig? config = null)
    {
        config ??= DefaultMcpConfig();
        var target = McpTargets().First(t => t.Id == "codex").ConfigPath;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var text = File.Exists(target) ? File.ReadAllText(target) : "";
            text = RemoveTomlTable(text, $"mcp_servers.{config.Name}");
            if (text.Length > 0 && !text.EndsWith("\n", StringComparison.Ordinal)) text += Environment.NewLine;
            text += BuildCodexToml(config);
            File.WriteAllText(target, text, new UTF8Encoding(false));
            return TweakResult.Ok("Codex Blender MCP config saved.", "已儲存 Codex Blender MCP 設定。", target);
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>用 Claude Code CLI 加入 MCP server · Add Blender MCP using the Claude Code CLI.</summary>
    public static Task<TweakResult> ConfigureClaudeBlenderMcpWithCli(BlenderMcpServerConfig? config = null, CancellationToken ct = default)
    {
        config ??= DefaultMcpConfig();
        var args = $"mcp add {config.Name} -- {config.Command} {JoinArgs(config.EffectiveArgs)}";
        return ShellRunner.Run("claude", args, false, ct);
    }

    /// <summary>寫入 Claude Code JSON MCP 設定 · Merge Blender MCP into Claude Code settings JSON.</summary>
    public static TweakResult ConfigureClaudeBlenderMcpJson(BlenderMcpServerConfig? config = null)
    {
        config ??= DefaultMcpConfig();
        var target = McpTargets().First(t => t.Id == "claude").ConfigPath;
        return MergeMcpJson(target, config, "mcpServers");
    }

    /// <summary>寫入 OpenCode JSON MCP 設定 · Merge Blender MCP into OpenCode config JSON.</summary>
    public static TweakResult ConfigureOpenCodeBlenderMcp(BlenderMcpServerConfig? config = null)
    {
        config ??= DefaultMcpConfig();
        var target = McpTargets().First(t => t.Id == "opencode").ConfigPath;
        return MergeMcpJson(target, config, "mcp");
    }

    public static string BuildCodexToml(BlenderMcpServerConfig config)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[mcp_servers.{config.Name}]");
        sb.AppendLine($"command = {TomlString(config.Command)}");
        sb.Append("args = [");
        sb.Append(string.Join(", ", config.EffectiveArgs.Select(TomlString)));
        sb.AppendLine("]");
        if (config.Env is { Count: > 0 })
        {
            sb.AppendLine("[mcp_servers." + config.Name + ".env]");
            foreach (var kv in config.Env)
                sb.AppendLine($"{kv.Key} = {TomlString(kv.Value)}");
        }
        return sb.ToString();
    }

    public static string BuildMcpJson(BlenderMcpServerConfig config, bool includeType)
    {
        var obj = BuildMcpJsonObject(config, includeType);
        return obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
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

    private static string? ResolveOnPath(string exe)
    {
        try
        {
            var path = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in path.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var c = Path.Combine(dir.Trim(), exe);
                if (File.Exists(c)) return c;
                if (!exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    c = Path.Combine(dir.Trim(), exe + ".exe");
                    if (File.Exists(c)) return c;
                    c = Path.Combine(dir.Trim(), exe + ".cmd");
                    if (File.Exists(c)) return c;
                }
            }
        }
        catch { }
        return null;
    }

    private static async Task<bool> ProbeCommand(string command, string arguments, IReadOnlyDictionary<string, string>? env, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            if (env is not null)
                foreach (var kv in env)
                    psi.Environment[kv.Key] = kv.Value;

            using var p = Process.Start(psi);
            if (p is null) return false;
            await p.WaitForExitAsync(timeout.Token);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string JoinArgs(IEnumerable<string> args)
        => string.Join(" ", args.Where(a => !string.IsNullOrWhiteSpace(a)).Select(Arg));

    private static string BuildMcpSkill(BlenderMcpInstance inst)
    {
        var cfg = ConfigFor(inst);
        return
            "---\n" +
            $"name: {cfg.Name}\n" +
            $"description: Use Blender MCP instance {inst.Display} for live Blender scene inspection and editing.\n" +
            "---\n\n" +
            $"# {cfg.Name}\n\n" +
            $"Use this skill when an agent should work against the Blender MCP instance `{inst.Display}`.\n\n" +
            "## Workflow\n" +
            "1. Confirm Blender is open and the BlenderMCP add-on socket server is started on the configured host and port.\n" +
            "2. Use the MCP tools from this server to inspect the scene before editing objects, materials, lighting, cameras, or renders.\n" +
            "3. For parallel work with multiple models or agents, assign each agent a different WinForge Blender MCP instance and port.\n" +
            "4. Before destructive edits, summarize the intended scene change and preserve user-created assets unless explicitly asked to replace them.\n\n" +
            "## Setup Check\n" +
            $"Install this folder as a Codex skill only after WinForge has configured an MCP server named `{cfg.Name}` for the target agent.\n\n" +
            "## Connection\n" +
            $"Server name: `{cfg.Name}`\n\n" +
            $"Host: `{inst.Host}`\n\n" +
            $"Port: `{inst.Port}`\n\n" +
            $"Command: `{cfg.Command} {JoinArgs(cfg.EffectiveArgs)}`\n";
    }

    private static string CleanMcpName(string value)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "blender" : value.Trim().ToLowerInvariant();
        var sb = new StringBuilder();
        foreach (var ch in raw)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-');
        var clean = sb.ToString().Trim('-');
        return clean.Length == 0 ? "blender" : clean;
    }

    private static string Arg(string value)
    {
        if (value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' or '/' or ':')) return value;
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string TomlString(string value)
        => "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string RemoveTomlTable(string text, string table)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var lines = text.Replace("\r", "").Split('\n');
        var sb = new StringBuilder();
        var skipping = false;
        var header = "[" + table + "]";
        var nested = "[" + table + ".";
        foreach (var line in lines)
        {
            var trim = line.Trim();
            if (trim.Equals(header, StringComparison.OrdinalIgnoreCase) ||
                trim.StartsWith(nested, StringComparison.OrdinalIgnoreCase))
            {
                skipping = true;
                continue;
            }
            if (skipping && trim.StartsWith("[") && !trim.StartsWith(nested, StringComparison.OrdinalIgnoreCase))
                skipping = false;
            if (!skipping) sb.AppendLine(line);
        }
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private static TweakResult MergeMcpJson(string target, BlenderMcpServerConfig config, string rootProperty)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            JsonObject root;
            if (File.Exists(target) && new FileInfo(target).Length > 0)
                root = JsonNode.Parse(File.ReadAllText(target), documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }) as JsonObject ?? new JsonObject();
            else
                root = new JsonObject();

            var mcp = root[rootProperty] as JsonObject;
            if (mcp is null)
            {
                mcp = new JsonObject();
                root[rootProperty] = mcp;
            }
            mcp[config.Name] = BuildMcpJsonObject(config, rootProperty == "mcp");

            File.WriteAllText(target, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            return TweakResult.Ok("Blender MCP config saved.", "已儲存 Blender MCP 設定。", target);
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    private static JsonObject BuildMcpJsonObject(BlenderMcpServerConfig config, bool includeType)
    {
        var obj = new JsonObject
        {
            ["command"] = config.Command,
            ["args"] = new JsonArray(config.EffectiveArgs.Select(a => JsonValue.Create(a)).ToArray()),
        };
        if (includeType) obj["type"] = "local";
        if (config.Env is { Count: > 0 })
        {
            var env = new JsonObject();
            foreach (var kv in config.Env) env[kv.Key] = kv.Value;
            obj["env"] = env;
        }
        return obj;
    }
}
