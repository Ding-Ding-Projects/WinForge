using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 一種安裝方法 · One way to install an agent (npm, winget, official installer…).
/// Label 係用嚟畀使用者撳嘅按鈕字；Run 真正執行安裝並回傳結果。
/// Label is the button text shown to the user; Run actually performs the install and returns a result.
/// </summary>
public sealed class AiInstallMethod
{
    /// <summary>方法名 · Short label, e.g. "npm", "winget", "official installer".</summary>
    public string Label { get; init; } = "";

    /// <summary>執行安裝 · Performs the install and returns a TweakResult.</summary>
    public Func<CancellationToken, Task<TweakResult>> Run { get; init; }
        = _ => Task.FromResult(TweakResult.Fail("Not implemented.", "未實作。"));
}

/// <summary>
/// 設定檔嘅種類，決定點樣驗證／提示 · Kind of config file, drives validation/hints.
/// </summary>
public enum AiConfigKind
{
    Json,
    Toml,
    Markdown,
    Text,
}

/// <summary>
/// 一個代理嘅設定檔 · One config file belonging to an agent.
/// RelativePath 係相對於使用者主目錄（~）嘅路徑，例如 ".claude/settings.json"。
/// RelativePath is relative to the user's home (~), e.g. ".claude/settings.json".
/// 如果 UseXdgConfig 為真，路徑會以 XDG_CONFIG_HOME（或 ~/.config）為基底。
/// When UseXdgConfig is true, the path is rooted at XDG_CONFIG_HOME (or ~/.config).
/// </summary>
public sealed class AiConfigFile
{
    public string LabelEn { get; init; } = "";
    public string LabelZh { get; init; } = "";

    /// <summary>相對路徑（用「/」分隔）· Relative path, "/"-separated.</summary>
    public string RelativePath { get; init; } = "";

    public AiConfigKind Kind { get; init; } = AiConfigKind.Text;

    /// <summary>true = 以 XDG_CONFIG_HOME／~/.config 為基底，否則以 ~ 為基底。</summary>
    public bool UseXdgConfig { get; init; }

    public string Label => Loc.I.Pick(LabelEn, LabelZh);
}

/// <summary>
/// 一個終端機 AI 編程代理 · One terminal AI coding agent (Claude Code, Codex, opencode…).
/// 包含偵測／啟動用嘅 CLI、文件連結、API key 環境變數，同一系列安裝方法。
/// Carries the CLI used to detect/launch it, docs link, API-key env var, and install methods.
/// </summary>
public sealed class AiAgent
{
    public string Key { get; init; } = "";
    public string NameEn { get; init; } = "";
    public string NameZh { get; init; } = "";
    public string DescEn { get; init; } = "";
    public string DescZh { get; init; } = "";

    /// <summary>偵測同啟動用嘅指令 · Command to detect on PATH and to launch, e.g. "claude".</summary>
    public string Cli { get; init; } = "";

    public string DocsUrl { get; init; } = "";

    /// <summary>API key 環境變數名（可空）· API-key env var, e.g. "ANTHROPIC_API_KEY" (nullable).</summary>
    public string? EnvKey { get; init; }

    public IReadOnlyList<AiInstallMethod> InstallMethods { get; init; } = Array.Empty<AiInstallMethod>();

    /// <summary>已知設定檔（路徑相對於 ~ 或 XDG）· Known config files for this agent.</summary>
    public IReadOnlyList<AiConfigFile> ConfigFiles { get; init; } = Array.Empty<AiConfigFile>();

    /// <summary>主要語言名 · Name in the primary language.</summary>
    public string Name => Loc.I.Pick(NameEn, NameZh);

    /// <summary>主要語言描述 · Description in the primary language.</summary>
    public string Desc => Loc.I.Pick(DescEn, DescZh);
}

/// <summary>
/// 終端機 AI 編程代理：資料 + 安裝／偵測／啟動輔助。
/// Terminal AI coding agents — data plus helpers to install, detect and launch them.
/// 全部防禦性寫法，永遠唔會擲例外。No redirect; defensive, never throws.
/// </summary>
public static class AiAgentService
{
    /// <summary>npm 安裝方法 · Build an npm-based global install method.</summary>
    private static AiInstallMethod Npm(string package) => new()
    {
        Label = "npm",
        Run = ct => ShellRunner.RunCmd($"npm install -g {package}", false, ct),
    };

    /// <summary>winget 安裝方法 · Build a winget-based install method.</summary>
    private static AiInstallMethod Winget(string wingetId) => new()
    {
        Label = "winget",
        Run = ct => PackageService.Install(wingetId, ct),
    };

    /// <summary>官方 PowerShell 安裝器 · Build an official PowerShell installer method (best-effort).</summary>
    private static AiInstallMethod Official(string script) => new()
    {
        Label = "official installer",
        Run = ct => ShellRunner.RunPowershell(script, false, ct),
    };

    /// <summary>六個內建代理 · The six built-in agents.</summary>
    public static readonly IReadOnlyList<AiAgent> All = new[]
    {
        new AiAgent
        {
            Key = "claude",
            NameEn = "Claude Code",
            NameZh = "Claude Code",
            DescEn = "Anthropic's agentic coding tool that lives in your terminal.",
            DescZh = "Anthropic 出嘅終端機 AI 編程代理，喺命令列幫你寫同改 code。",
            Cli = "claude",
            DocsUrl = "https://code.claude.com/docs",
            EnvKey = "ANTHROPIC_API_KEY",
            InstallMethods = new[]
            {
                Npm("@anthropic-ai/claude-code"),
                Official("irm https://claude.ai/install.ps1 | iex"),
            },
            ConfigFiles = new[]
            {
                new AiConfigFile { LabelEn = "settings.json", LabelZh = "settings.json", RelativePath = ".claude/settings.json", Kind = AiConfigKind.Json },
                new AiConfigFile { LabelEn = "CLAUDE.md", LabelZh = "CLAUDE.md", RelativePath = ".claude/CLAUDE.md", Kind = AiConfigKind.Markdown },
            },
        },
        new AiAgent
        {
            Key = "codex",
            NameEn = "OpenAI Codex CLI",
            NameZh = "OpenAI Codex CLI",
            DescEn = "OpenAI's open-source coding agent that runs in your terminal.",
            DescZh = "OpenAI 嘅開源終端機編程代理，喺命令列度跑。",
            Cli = "codex",
            DocsUrl = "https://developers.openai.com/codex/cli",
            EnvKey = "OPENAI_API_KEY",
            InstallMethods = new[]
            {
                Npm("@openai/codex"),
            },
            ConfigFiles = new[]
            {
                new AiConfigFile { LabelEn = "config.toml", LabelZh = "config.toml", RelativePath = ".codex/config.toml", Kind = AiConfigKind.Toml },
                new AiConfigFile { LabelEn = "AGENTS.md", LabelZh = "AGENTS.md", RelativePath = ".codex/AGENTS.md", Kind = AiConfigKind.Markdown },
            },
        },
        new AiAgent
        {
            Key = "opencode",
            NameEn = "opencode",
            NameZh = "opencode",
            DescEn = "Open-source AI coding agent built for the terminal.",
            DescZh = "為終端機而設嘅開源 AI 編程代理。",
            Cli = "opencode",
            DocsUrl = "https://opencode.ai/docs",
            EnvKey = null,
            InstallMethods = new[]
            {
                Npm("opencode-ai"),
                Official("irm https://opencode.ai/install.ps1 | iex"),
            },
            ConfigFiles = new[]
            {
                new AiConfigFile { LabelEn = "opencode.json", LabelZh = "opencode.json", RelativePath = "opencode/opencode.json", Kind = AiConfigKind.Json, UseXdgConfig = true },
                new AiConfigFile { LabelEn = "AGENTS.md", LabelZh = "AGENTS.md", RelativePath = ".config/opencode/AGENTS.md", Kind = AiConfigKind.Markdown },
            },
        },
        new AiAgent
        {
            Key = "pi",
            NameEn = "Pi coding agent",
            NameZh = "Pi 編程代理",
            DescEn = "Lightweight terminal coding agent by Mario Zechner (earendil).",
            DescZh = "Mario Zechner（earendil）整嘅輕量終端機編程代理。",
            Cli = "pi",
            DocsUrl = "https://pi.dev",
            EnvKey = "ANTHROPIC_API_KEY",
            InstallMethods = new[]
            {
                Npm("@mariozechner/pi-coding-agent"),
            },
            ConfigFiles = new[]
            {
                new AiConfigFile { LabelEn = "config.json", LabelZh = "config.json", RelativePath = ".pi/config.json", Kind = AiConfigKind.Json },
            },
        },
        new AiAgent
        {
            Key = "openclaw",
            NameEn = "OpenClaw",
            NameZh = "OpenClaw",
            DescEn = "Personal AI gateway and coding agent for your terminal.",
            DescZh = "個人 AI 閘道兼終端機編程代理。",
            Cli = "openclaw",
            DocsUrl = "https://docs.openclaw.ai",
            EnvKey = null,
            InstallMethods = new[]
            {
                Npm("openclaw"),
            },
            // 路徑未經官方文件證實，提供 Browse 後備 · Path not authoritatively documented; Browse fallback provided.
            ConfigFiles = new[]
            {
                new AiConfigFile { LabelEn = "config.json", LabelZh = "config.json", RelativePath = ".openclaw/config.json", Kind = AiConfigKind.Json },
            },
        },
        new AiAgent
        {
            Key = "hermes",
            NameEn = "Hermes Agent",
            NameZh = "Hermes 代理",
            DescEn = "Nous Research's terminal AI agent.",
            DescZh = "Nous Research 出嘅終端機 AI 代理。",
            Cli = "hermes",
            DocsUrl = "https://hermes-agent.nousresearch.com/docs",
            EnvKey = null,
            InstallMethods = new[]
            {
                Official("irm https://hermes-agent.nousresearch.com/install.ps1 | iex"),
            },
            // 路徑未經官方文件證實，提供 Browse 後備 · Path not authoritatively documented; Browse fallback provided.
            ConfigFiles = new[]
            {
                new AiConfigFile { LabelEn = "config.json", LabelZh = "config.json", RelativePath = ".hermes/config.json", Kind = AiConfigKind.Json },
            },
        },
    };

    /// <summary>
    /// 偵測代理係咪已安裝 · Is the agent installed?
    /// 先跑「&lt;cli&gt; --version」（輸出有嘢或結束代碼 0 就當有）；唔得就退而用「where &lt;cli&gt;」。
    /// Runs "&lt;cli&gt; --version" (ok if output present or exit 0); falls back to "where &lt;cli&gt;".
    /// </summary>
    public static async Task<bool> IsInstalledAsync(AiAgent a, CancellationToken ct = default)
    {
        if (a is null || string.IsNullOrWhiteSpace(a.Cli)) return false;
        try
        {
            var r = await ShellRunner.Run(a.Cli, "--version", false, ct);
            if (r.Success || !string.IsNullOrWhiteSpace(r.Output)) return true;
        }
        catch { }

        try
        {
            var w = await ShellRunner.Capture("where", a.Cli, ct);
            if (!string.IsNullOrWhiteSpace(w) && !w.Contains("Could not find", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch { }

        return false;
    }

    /// <summary>Node／npm 喺唔喺度 · Is Node (npm) available? Runs "npm --version".</summary>
    public static async Task<bool> NodeAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await ShellRunner.Run("npm", "--version", false, ct);
            return r.Success || !string.IsNullOrWhiteSpace(r.Output);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 喺 Windows 終端機開個新分頁跑個 CLI · Launch the agent in Windows Terminal (wt.exe).
    /// 失敗就退而用 cmd /k 開個新視窗。Falls back to "cmd /k" if wt.exe is unavailable.
    /// </summary>
    public static TweakResult Launch(AiAgent a, string? workingDir = null)
    {
        if (a is null || string.IsNullOrWhiteSpace(a.Cli))
            return TweakResult.Fail("No agent to launch.", "冇代理可以啟動。");

        var dir = !string.IsNullOrWhiteSpace(workingDir) && Directory.Exists(workingDir)
            ? workingDir
            : null;

        // 首選：Windows 終端機 · Preferred: Windows Terminal.
        try
        {
            // wt.exe -d <dir> cmd /k <cli>  — 用 cmd /k 令 CLI 退出後個視窗唔會即刻收埋。
            // cmd /k keeps the tab open after the CLI exits so the user can read any output.
            var args = "";
            if (dir is not null) args += $"-d \"{dir}\" ";
            args += $"cmd /k {a.Cli}";

            var psi = new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = args,
                UseShellExecute = true,
            };
            if (dir is not null) psi.WorkingDirectory = dir;

            using var p = Process.Start(psi);
            if (p is not null)
                return TweakResult.Ok($"Launched {a.NameEn} in Windows Terminal.",
                    $"已喺 Windows 終端機啟動 {a.NameZh}。");
        }
        catch { /* 跌落去用 cmd · fall through to cmd */ }

        // 後備：直接開 cmd /k · Fallback: plain cmd window.
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k {a.Cli}",
                UseShellExecute = true,
            };
            if (dir is not null) psi.WorkingDirectory = dir;

            using var p = Process.Start(psi);
            if (p is not null)
                return TweakResult.Ok($"Launched {a.NameEn}.", $"已啟動 {a.NameZh}。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not launch {a.NameEn}: {ex.Message}",
                $"無法啟動 {a.NameZh}：{ex.Message}");
        }

        return TweakResult.Fail($"Could not launch {a.NameEn}.", $"無法啟動 {a.NameZh}。");
    }

    /// <summary>
    /// 設定 API key 環境變數（使用者範圍）· Set the API-key env var (User scope) if EnvKey is set.
    /// 防禦性：EnvKey 為空或出錯都唔會擲例外。Defensive; no-op and never throws if EnvKey is null.
    /// </summary>
    public static void SetEnvKey(AiAgent a, string value)
    {
        if (a?.EnvKey is null) return;
        try
        {
            Environment.SetEnvironmentVariable(a.EnvKey, value, EnvironmentVariableTarget.User);
        }
        catch { }
    }

    /// <summary>讀返 API key 環境變數 · Read the API-key env var (User then Process), or null.</summary>
    public static string? GetEnvKey(AiAgent a)
    {
        if (a?.EnvKey is null) return null;
        try
        {
            var u = Environment.GetEnvironmentVariable(a.EnvKey, EnvironmentVariableTarget.User);
            if (!string.IsNullOrEmpty(u)) return u;
            return Environment.GetEnvironmentVariable(a.EnvKey);
        }
        catch
        {
            return null;
        }
    }
}
