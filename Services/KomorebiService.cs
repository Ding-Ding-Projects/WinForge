using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 一個被 komorebi 管理嘅視窗 · One window managed by komorebi (from `komorebic state`).
/// </summary>
public sealed class KomoWindow
{
    public long Hwnd { get; init; }
    public string Title { get; init; } = "";
    public string Exe { get; init; } = "";
    public string Class { get; init; } = "";
}

/// <summary>一個工作區 · One workspace on a monitor.</summary>
public sealed class KomoWorkspace
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public string Layout { get; init; } = "";
    public bool Focused { get; init; }
    public List<KomoWindow> Windows { get; } = new();
}

/// <summary>一個顯示器 · One monitor.</summary>
public sealed class KomoMonitor
{
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public bool Focused { get; init; }
    public List<KomoWorkspace> Workspaces { get; } = new();
}

/// <summary>解析後嘅完整狀態 · Parsed snapshot of `komorebic state`.</summary>
public sealed class KomoState
{
    public List<KomoMonitor> Monitors { get; } = new();
    public bool Paused { get; init; }
    public bool IsTilingDisabled { get; init; }
    public string? RawError { get; init; }

    public int WindowCount
    {
        get
        {
            int n = 0;
            foreach (var m in Monitors)
                foreach (var w in m.Workspaces)
                    n += w.Windows.Count;
            return n;
        }
    }
}

/// <summary>
/// 應用程式內 Komorebi 控制 · In-app control over the Komorebi tiling window manager — a thin, defensive
/// wrapper around the `komorebic` CLI. Detects install, manages the daemon lifecycle (start/stop/restart),
/// parses `komorebic state` JSON into a monitors → workspaces → windows tree, and exposes layout / workspace /
/// toggle / padding / rule / config / autostart verbs. Never throws; degrades gracefully across CLI versions.
/// We only invoke the user-installed binary (komorebi is source-available, not redistributed).
/// </summary>
public static class KomorebiService
{
    /// <summary>winget 套件 ID · The winget package ID for installs.</summary>
    public const string WingetId = "LGUG2Z.komorebi";

    /// <summary>komorebic 行得到嘅可用 layout（雙語標籤喺 UI 處理）· Layout values accepted by `change-layout`.</summary>
    public static readonly (string Value, string En, string Zh)[] Layouts =
    {
        ("bsp", "BSP (binary split)", "BSP（二元分割）"),
        ("columns", "Columns", "直欄"),
        ("rows", "Rows", "橫列"),
        ("vertical-stack", "Vertical stack", "垂直堆疊"),
        ("horizontal-stack", "Horizontal stack", "水平堆疊"),
        ("ultrawide-vertical-stack", "Ultrawide vertical stack", "超寬垂直堆疊"),
        ("grid", "Grid", "網格"),
        ("right-main-vertical-stack", "Right-main vertical stack", "右主垂直堆疊"),
        ("scrolling", "Scrolling", "捲動"),
    };

    // ===== install / lifecycle =====

    /// <summary>komorebic 裝咗未（行 "komorebic --version"）· True if "komorebic --version" produced output.</summary>
    public static async Task<bool> IsInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var output = await ShellRunner.Capture("komorebic", "--version", ct);
            return output.Trim().Length > 0
                && !output.Contains("not recognized", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("not found", StringComparison.OrdinalIgnoreCase)
                && !output.Contains("cannot find", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    /// <summary>守護程序行緊未（state 有輸出就當行緊）· True if the daemon answers `komorebic state`.</summary>
    public static async Task<bool> IsRunningAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await ShellRunner.Run("komorebic", "state", false, ct);
            if (!r.Success) return false;
            var o = r.Output ?? "";
            return o.TrimStart().StartsWith("{");
        }
        catch { return false; }
    }

    /// <summary>啟動守護程序 · Start the komorebi daemon. extraFlags e.g. "--bar".</summary>
    public static Task<TweakResult> StartAsync(string extraFlags = "", CancellationToken ct = default)
        => Run($"start {extraFlags}".Trim(), ct);

    /// <summary>停止守護程序並還原視窗 · Stop the daemon and restore windows.</summary>
    public static Task<TweakResult> StopAsync(CancellationToken ct = default)
        => Run("stop", ct);

    /// <summary>重新啟動守護程序 · Restart: stop then start.</summary>
    public static async Task<TweakResult> RestartAsync(CancellationToken ct = default)
    {
        await Run("stop", ct);
        await Task.Delay(700, ct);
        return await Run("start", ct);
    }

    // ===== state =====

    /// <summary>讀取並解析 komorebic state · Read and parse `komorebic state` into a tree. Never throws.</summary>
    public static async Task<KomoState> GetStateAsync(CancellationToken ct = default)
    {
        string raw;
        try
        {
            var r = await ShellRunner.Run("komorebic", "state", false, ct);
            raw = r.Output ?? "";
        }
        catch (Exception ex) { return new KomoState { RawError = ex.Message }; }

        raw = raw.Trim().TrimStart('﻿');
        int a = raw.IndexOf('{'), b = raw.LastIndexOf('}');
        if (a < 0 || b <= a) return new KomoState { RawError = raw };
        raw = raw.Substring(a, b - a + 1);

        try { return Parse(raw); }
        catch (Exception ex) { return new KomoState { RawError = ex.Message }; }
    }

    private static KomoState Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        bool paused = TryBool(root, "is_paused");
        // some versions expose "tiling_paused" or a global flag; default false.
        var state = new KomoState { Paused = paused, IsTilingDisabled = false };

        if (!TryGetRing(root, "monitors", out var monitorsArr, out int monFocused))
            return state;

        int mi = 0;
        foreach (var mEl in monitorsArr)
        {
            var monitor = new KomoMonitor
            {
                Index = mi,
                Name = TryStr(mEl, "name") ?? TryStr(mEl, "device") ?? $"Monitor {mi}",
                Focused = mi == monFocused,
            };

            if (TryGetRing(mEl, "workspaces", out var wsArr, out int wsFocused))
            {
                int wi = 0;
                foreach (var wsEl in wsArr)
                {
                    var ws = new KomoWorkspace
                    {
                        Index = wi,
                        Name = TryStr(wsEl, "name") ?? Roman(wi),
                        Layout = ReadLayout(wsEl),
                        Focused = monitor.Focused && wi == wsFocused,
                    };

                    // containers ring → each container has windows ring
                    if (TryGetRing(wsEl, "containers", out var contArr, out _))
                    {
                        foreach (var cEl in contArr)
                            if (TryGetRing(cEl, "windows", out var winArr, out _))
                                foreach (var winEl in winArr)
                                    AddWindow(ws.Windows, winEl);
                    }

                    // floating windows ring
                    if (TryGetRing(wsEl, "floating_windows", out var floatArr, out _))
                        foreach (var winEl in floatArr)
                            AddWindow(ws.Windows, winEl);

                    // monocle container
                    if (wsEl.TryGetProperty("monocle_container", out var mono) && mono.ValueKind == JsonValueKind.Object)
                        if (TryGetRing(mono, "windows", out var monoWins, out _))
                            foreach (var winEl in monoWins)
                                AddWindow(ws.Windows, winEl);

                    monitor.Workspaces.Add(ws);
                    wi++;
                }
            }

            state.Monitors.Add(monitor);
            mi++;
        }

        return state;
    }

    private static void AddWindow(List<KomoWindow> list, JsonElement winEl)
    {
        if (winEl.ValueKind != JsonValueKind.Object) return;
        list.Add(new KomoWindow
        {
            Hwnd = winEl.TryGetProperty("hwnd", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetInt64() : 0,
            Title = TryStr(winEl, "title") ?? "",
            Exe = TryStr(winEl, "exe") ?? "",
            Class = TryStr(winEl, "class") ?? "",
        });
    }

    /// <summary>workspace.layout 可能係 {"Default":"BSP"} 或字串 · layout is {"Default":"BSP"}/{"Custom":…} or a string.</summary>
    private static string ReadLayout(JsonElement wsEl)
    {
        if (!wsEl.TryGetProperty("layout", out var lay)) return "";
        if (lay.ValueKind == JsonValueKind.String) return lay.GetString() ?? "";
        if (lay.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in lay.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String) return p.Value.GetString() ?? p.Name;
                return p.Name; // e.g. "Custom"
            }
        }
        return "";
    }

    // a Ring serializes as { "elements": [...], "focused": N }
    private static bool TryGetRing(JsonElement parent, string name, out JsonElement.ArrayEnumerator items, out int focused)
    {
        items = default;
        focused = 0;
        if (!parent.TryGetProperty(name, out var ring)) return false;
        if (ring.ValueKind == JsonValueKind.Array) { items = ring.EnumerateArray(); return true; }
        if (ring.ValueKind != JsonValueKind.Object) return false;
        if (ring.TryGetProperty("focused", out var f) && f.ValueKind == JsonValueKind.Number) focused = f.GetInt32();
        if (ring.TryGetProperty("elements", out var el) && el.ValueKind == JsonValueKind.Array)
        {
            items = el.EnumerateArray();
            return true;
        }
        return false;
    }

    private static string? TryStr(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool TryBool(JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True);

    private static string Roman(int i) => i switch
    {
        0 => "I", 1 => "II", 2 => "III", 3 => "IV", 4 => "V",
        5 => "VI", 6 => "VII", 7 => "VIII", 8 => "IX", 9 => "X",
        _ => (i + 1).ToString(),
    };

    // ===== verbs =====

    public static Task<TweakResult> ChangeLayoutAsync(string layout, CancellationToken ct = default)
        => Run($"change-layout {layout}", ct);

    public static Task<TweakResult> CycleLayoutAsync(bool next, CancellationToken ct = default)
        => Run($"cycle-layout {(next ? "next" : "previous")}", ct);

    public static Task<TweakResult> FocusWorkspaceAsync(int index, CancellationToken ct = default)
        => Run($"focus-workspace {index}", ct);

    public static Task<TweakResult> MoveToWorkspaceAsync(int index, CancellationToken ct = default)
        => Run($"move-to-workspace {index}", ct);

    public static Task<TweakResult> SendToWorkspaceAsync(int index, CancellationToken ct = default)
        => Run($"send-to-workspace {index}", ct);

    public static Task<TweakResult> ToggleTilingAsync(CancellationToken ct = default) => Run("toggle-tiling", ct);
    public static Task<TweakResult> ToggleFloatAsync(CancellationToken ct = default) => Run("toggle-float", ct);
    public static Task<TweakResult> ToggleMonocleAsync(CancellationToken ct = default) => Run("toggle-monocle", ct);
    public static Task<TweakResult> TogglePauseAsync(CancellationToken ct = default) => Run("toggle-pause", ct);
    public static Task<TweakResult> PromoteAsync(CancellationToken ct = default) => Run("promote", ct);
    public static Task<TweakResult> RetileAsync(CancellationToken ct = default) => Run("retile", ct);

    public static Task<TweakResult> WorkspacePaddingAsync(int monitor, int workspace, int size, CancellationToken ct = default)
        => Run($"workspace-padding {monitor} {workspace} {size}", ct);

    public static Task<TweakResult> ContainerPaddingAsync(int monitor, int workspace, int size, CancellationToken ct = default)
        => Run($"container-padding {monitor} {workspace} {size}", ct);

    public static Task<TweakResult> FocusedWorkspacePaddingAsync(int size, CancellationToken ct = default)
        => Run($"focused-workspace-padding {size}", ct);

    public static Task<TweakResult> FocusedContainerPaddingAsync(int size, CancellationToken ct = default)
        => Run($"focused-workspace-container-padding {size}", ct);

    public static Task<TweakResult> MouseFollowsFocusAsync(bool enable, CancellationToken ct = default)
        => Run($"mouse-follows-focus {(enable ? "enable" : "disable")}", ct);

    // ----- rules -----

    /// <summary>identifier ∈ exe|class|title|path · Add a workspace rule (pin app to monitor/workspace).</summary>
    public static Task<TweakResult> WorkspaceRuleAsync(string identifier, string id, int monitor, int workspace, CancellationToken ct = default)
        => Run($"workspace-rule {identifier} \"{id}\" {monitor} {workspace}", ct);

    /// <summary>identifier ∈ exe|class|title|path · Add an ignore rule (komorebi won't manage the app).</summary>
    public static Task<TweakResult> IgnoreRuleAsync(string identifier, string id, CancellationToken ct = default)
        => Run($"ignore-rule {identifier} \"{id}\"", ct);

    /// <summary>identifier ∈ exe|class|title|path · Add a manage rule (force-manage an app).</summary>
    public static Task<TweakResult> ManageRuleAsync(string identifier, string id, CancellationToken ct = default)
        => Run($"manage-rule {identifier} \"{id}\"", ct);

    /// <summary>Float the focused window for this session (session float rule).</summary>
    public static Task<TweakResult> SessionFloatRuleAsync(CancellationToken ct = default)
        => Run("session-float-rule", ct);

    public static Task<TweakResult> ClearSessionFloatRulesAsync(CancellationToken ct = default)
        => Run("clear-session-float-rules", ct);

    // ----- config / autostart -----

    public static Task<TweakResult> ReloadConfigurationAsync(CancellationToken ct = default)
        => Run("reload-configuration", ct);

    public static Task<TweakResult> ReplaceConfigurationAsync(string configPath, CancellationToken ct = default)
        => Run($"replace-configuration \"{configPath}\"", ct);

    public static Task<TweakResult> EnableAutostartAsync(bool withBar, CancellationToken ct = default)
        => Run($"enable-autostart{(withBar ? " --bar" : "")}", ct);

    public static Task<TweakResult> DisableAutostartAsync(CancellationToken ct = default)
        => Run("disable-autostart", ct);

    /// <summary>收集範例設定（quickstart）· Gather example configurations into the config dir.</summary>
    public static Task<TweakResult> QuickstartAsync(CancellationToken ct = default)
        => Run("quickstart", ct);

    /// <summary>檢查設定健康狀況 · Run komorebic's configuration health check.</summary>
    public static Task<TweakResult> CheckAsync(CancellationToken ct = default) => Run("check", ct);

    /// <summary>原始 state JSON（畀 raw 檢視）· Raw `komorebic state` JSON for the inspector.</summary>
    public static Task<TweakResult> RawStateAsync(CancellationToken ct = default) => Run("state", ct);

    /// <summary>原始 global-state JSON · Raw `komorebic global-state` JSON.</summary>
    public static Task<TweakResult> GlobalStateAsync(CancellationToken ct = default) => Run("global-state", ct);

    // ===== config path =====

    /// <summary>
    /// komorebi.json 嘅推測位置 · Best-effort path to komorebi.json: KOMOREBI_CONFIG_HOME, then
    /// %USERPROFILE%\.config\komorebi\komorebi.json, then %USERPROFILE%\komorebi.json.
    /// Returns the first that exists, or the most likely default if none exist.
    /// </summary>
    public static string GuessConfigPath()
    {
        try
        {
            var home = Environment.GetEnvironmentVariable("KOMOREBI_CONFIG_HOME");
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(home))
                candidates.Add(Path.Combine(home, "komorebi.json"));
            candidates.Add(Path.Combine(profile, ".config", "komorebi", "komorebi.json"));
            candidates.Add(Path.Combine(profile, "komorebi.json"));
            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            return candidates.Count > 0 ? candidates[0] : Path.Combine(profile, "komorebi.json");
        }
        catch { return "komorebi.json"; }
    }

    /// <summary>komorebi.json 存唔存在 · Whether a config file exists at the guessed location.</summary>
    public static bool ConfigExists()
    {
        try { return File.Exists(GuessConfigPath()); }
        catch { return false; }
    }

    // ===== core runner =====

    /// <summary>直接行一句 komorebic 指令 · Run a raw komorebic command and capture output. Never throws.</summary>
    public static Task<TweakResult> Run(string args, CancellationToken ct = default)
    {
        try { return ShellRunner.Run("komorebic", args, false, ct); }
        catch (Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
    }
}
