using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

public sealed record DetectedNodeManager(NodeVersionManager Manager, string Executable, string DisplayName);

public sealed record DeveloperCacheSnapshot(
    DeveloperCacheKind Kind,
    string DisplayName,
    bool ToolAvailable,
    long? ReclaimableBytes,
    string Detail)
{
    public string Summary => ReclaimableBytes is long bytes
        ? $"{DisplayName}: {DeveloperWorkflowCore.FormatBytes(bytes)} · {Detail}"
        : $"{DisplayName}: {Detail}";
}

/// <summary>Windows execution layer for <see cref="DeveloperWorkflowCore"/> plans.</summary>
public static class DeveloperWorkflowService
{
    private const int MaximumMeasuredFiles = 250_000;

    public static async Task<IReadOnlyList<PortListener>> InspectPortAsync(int port, CancellationToken ct = default)
    {
        port = DeveloperWorkflowCore.ValidatePort(port);
        var script = "$ErrorActionPreference='Stop'; " +
                     $"@(Get-NetTCPConnection -State Listen -LocalPort {port} -ErrorAction SilentlyContinue | ForEach-Object {{ " +
                     "$owner = Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue; " +
                     "[pscustomobject]@{ProcessId=[int]$_.OwningProcess;ProcessName=if($owner){$owner.ProcessName}else{'unknown'};LocalAddress=[string]$_.LocalAddress;LocalPort=[int]$_.LocalPort} " +
                     "}) | ConvertTo-Json -Compress";
        var json = await ShellRunner.CapturePowershellJson(script, ct);
        return DeveloperWorkflowCore.ParseListeners(json, port);
    }

    public static async Task<TweakResult> TerminateListenersAsync(IEnumerable<PortListener> listeners, CancellationToken ct = default)
    {
        var unique = listeners?.GroupBy(item => item.ProcessId).Select(group => group.First()).ToArray()
                     ?? Array.Empty<PortListener>();
        if (unique.Length == 0) return TweakResult.Fail("No listener is selected.", "未揀任何監聽程序。");
        if (unique.Any(item => item.ProcessId == Environment.ProcessId))
            return TweakResult.Fail("WinForge will not terminate its own process.", "WinForge 唔會終止自己個程序。");

        var output = new StringBuilder();
        foreach (var listener in unique)
        {
            ct.ThrowIfCancellationRequested();
            var plan = DeveloperWorkflowCore.BuildTerminatePlan(listener.ProcessId);
            var result = await ShellRunner.RunArguments(plan.Executable, plan.Arguments, ct: ct);
            output.AppendLine($"{listener.DisplayLabel}: {(result.Success ? "terminated" : "failed")}");
            if (!string.IsNullOrWhiteSpace(result.Output)) output.AppendLine(result.Output);
            if (!result.Success)
                return TweakResult.Fail("A listener could not be terminated.", "有監聽程序未能終止。", output.ToString().Trim());
        }
        return TweakResult.Ok("Listener processes were terminated.", "監聽程序已終止。", output.ToString().Trim());
    }

    public static async Task<TweakResult> TerminateReviewedListenersAsync(
        int port,
        IReadOnlyList<PortListener> reviewed,
        CancellationToken ct = default)
    {
        var current = await InspectPortAsync(port, ct);
        if (!DeveloperWorkflowCore.ReviewedListenersStillMatch(reviewed, current))
            return TweakResult.Fail(
                "The listener set changed after review. Inspect the port again before terminating anything.",
                "審閱之後監聽程序有變。終止任何程序之前，請重新檢視個連接埠。");
        return await TerminateListenersAsync(current, ct);
    }

    public static IReadOnlyList<DetectedNodeManager> DetectNodeManagers()
    {
        var candidates = new[]
        {
            (NodeVersionManager.Fnm, "fnm", "fnm (per-shell)"),
            (NodeVersionManager.Volta, "volta", "Volta (per-shell)"),
            (NodeVersionManager.Nvm, "nvm", "nvm-windows (global fallback)"),
        };
        return candidates
            .Select(item => (item.Item1, Executable: FindExecutable(item.Item2), item.Item3))
            .Where(item => item.Executable is not null)
            .Select(item => new DetectedNodeManager(item.Item1, item.Executable!, item.Item3))
            .ToArray();
    }

    public static Task<TweakResult> ListNodeVersionsAsync(DetectedNodeManager manager, CancellationToken ct = default)
        => RunAsync(DeveloperWorkflowCore.BuildNodeListPlan(manager.Manager, manager.Executable), ct);

    public static Task<TweakResult> InstallNodeVersionAsync(DetectedNodeManager manager, string version, CancellationToken ct = default)
        => RunAsync(DeveloperWorkflowCore.BuildNodeInstallPlan(manager.Manager, manager.Executable, version), ct);

    public static TweakResult OpenNodeVersionShell(DetectedNodeManager manager, string version)
    {
        try
        {
            var plan = DeveloperWorkflowCore.BuildNodeShellPlan(manager.Manager, manager.Executable, version);
            var start = new ProcessStartInfo { FileName = plan.Executable, UseShellExecute = true };
            foreach (var argument in plan.Arguments) start.ArgumentList.Add(argument);
            if (Process.Start(start) is null)
                return TweakResult.Fail("The Node shell did not start.", "Node shell 未能啟動。");
            return TweakResult.Ok("A version-scoped Node shell was opened.", "已開啟指定版本嘅 Node shell。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(
                ex.Message,
                "未能開啟指定版本嘅 Node shell；請檢查版本同管理器。",
                ex.Message);
        }
    }

    public static Task<TweakResult> CorepackStatusAsync(CancellationToken ct = default)
        => RunAsync(DeveloperWorkflowCore.BuildCorepackStatusPlan(), ct);

    public static Task<TweakResult> EnableCorepackAsync(CancellationToken ct = default)
        => RunAsync(DeveloperWorkflowCore.BuildCorepackEnablePlan(), ct);

    public static Task<TweakResult> PrepareCorepackAsync(string manager, string channel, CancellationToken ct = default)
        => RunAsync(DeveloperWorkflowCore.BuildCorepackPreparePlan(manager, channel), ct);

    public static async Task<IReadOnlyList<string>> DefenderExclusionsAsync(CancellationToken ct = default)
    {
        const string script = "$ErrorActionPreference='Stop'; @((Get-MpPreference).ExclusionPath) | ConvertTo-Json -Compress";
        var raw = await ShellRunner.CapturePowershell(script, ct);
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        try
        {
            using var document = JsonDocument.Parse(raw.Trim().TrimStart('\uFEFF'));
            if (document.RootElement.ValueKind == JsonValueKind.Array)
                return document.RootElement.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? string.Empty)
                    .Where(item => item.Length > 0)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            if (document.RootElement.ValueKind == JsonValueKind.String)
                return new[] { document.RootElement.GetString() ?? string.Empty };
        }
        catch { }
        return Array.Empty<string>();
    }

    public static Task<TweakResult> SetDefenderExclusionAsync(string path, bool add, CancellationToken ct = default)
    {
        var script = DeveloperWorkflowCore.BuildDefenderMutationScript(path, add, requireExisting: add);
        return ShellRunner.RunPowershell(script, elevated: true, ct);
    }

    public static async Task<string> InspectTcpTuningAsync(CancellationToken ct = default)
    {
        var netsh = await ShellRunner.RunArguments("netsh.exe", new[] { "int", "ipv4", "show", "dynamicport", "tcp" }, ct: ct);
        var wait = await ShellRunner.CapturePowershell(
            "$v=(Get-ItemProperty -LiteralPath 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters' -Name TcpTimedWaitDelay -ErrorAction SilentlyContinue).TcpTimedWaitDelay; if($null -eq $v){'TcpTimedWaitDelay: Windows default'}else{\"TcpTimedWaitDelay: $v seconds\"}", ct);
        return ((netsh.Output ?? netsh.Message?.En ?? string.Empty) + "\n" + wait).Trim();
    }

    public static Task<TweakResult> ApplyTcpTuningAsync(int start, int count, int timedWaitSeconds, CancellationToken ct = default)
        => ShellRunner.RunPowershell(DeveloperWorkflowCore.BuildTcpTuningScript(start, count, timedWaitSeconds), elevated: true, ct);

    public static async Task<IReadOnlyList<DeveloperCacheSnapshot>> InspectCachesAsync(CancellationToken ct = default)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var snapshots = new List<DeveloperCacheSnapshot>
        {
            await InspectPathCache(DeveloperCacheKind.Npm, "npm", Path.Combine(local, "npm-cache"), "npm", ct),
        };

        var pnpmPathResult = await ShellRunner.RunArguments("pnpm", new[] { "store", "path" }, ct: ct);
        var pnpmPath = pnpmPathResult.Success ? LastNonEmptyLine(pnpmPathResult.Output) : null;
        snapshots.Add(await InspectPathCache(DeveloperCacheKind.Pnpm, "pnpm", pnpmPath, "pnpm", ct));

        var pipPathResult = await ShellRunner.RunArguments("pip", new[] { "cache", "dir" }, ct: ct);
        var pipPath = pipPathResult.Success ? LastNonEmptyLine(pipPathResult.Output) : Path.Combine(local, "pip", "Cache");
        snapshots.Add(await InspectPathCache(DeveloperCacheKind.Pip, "pip", pipPath, "pip", ct));

        var docker = await ShellRunner.RunArguments("docker", new[] { "system", "df" }, ct: ct);
        snapshots.Add(new DeveloperCacheSnapshot(
            DeveloperCacheKind.Docker,
            "Docker build cache",
            docker.Success,
            null,
            docker.Success ? Bound(docker.Output ?? "No reclaimable data reported.", 1200) : "Docker CLI/engine unavailable"));
        return snapshots;
    }

    public static async Task<TweakResult> CleanCachesAsync(IEnumerable<DeveloperCacheKind> kinds, CancellationToken ct = default)
    {
        var selected = kinds?.Distinct().ToArray() ?? Array.Empty<DeveloperCacheKind>();
        if (selected.Length == 0) return TweakResult.Fail("Select at least one reviewed cache.", "請揀至少一個已檢視快取。");
        var output = new StringBuilder();
        foreach (var kind in selected)
        {
            ct.ThrowIfCancellationRequested();
            var plan = DeveloperWorkflowCore.BuildCacheCleanPlan(kind);
            var result = await RunAsync(plan, ct);
            output.AppendLine($"{kind}: {(result.Success ? "cleaned" : "failed")}");
            if (!string.IsNullOrWhiteSpace(result.Output)) output.AppendLine(result.Output);
            if (!result.Success)
                return TweakResult.Fail("Cache cleanup stopped after a failed tool.", "有工具失敗，快取清理已停止。", output.ToString().Trim());
        }
        return TweakResult.Ok("Selected developer caches were cleaned.", "已清理揀選嘅開發快取。", output.ToString().Trim());
    }

    private static Task<TweakResult> RunAsync(DeveloperCommandPlan plan, CancellationToken ct)
        => ShellRunner.RunArguments(plan.Executable, plan.Arguments, plan.RequiresElevation, ct);

    private static async Task<DeveloperCacheSnapshot> InspectPathCache(
        DeveloperCacheKind kind, string display, string? path, string tool, CancellationToken ct)
    {
        var available = FindExecutable(tool) is not null;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return new DeveloperCacheSnapshot(kind, display, available, 0, path ?? "cache path unavailable");
        var measurement = await Task.Run(() => MeasureDirectory(path, ct), ct);
        var detail = measurement.Truncated
            ? $"{path} (bounded scan stopped at {MaximumMeasuredFiles:N0} files)"
            : path;
        return new DeveloperCacheSnapshot(kind, display, available, measurement.Bytes, detail);
    }

    private static (long Bytes, bool Truncated) MeasureDirectory(string root, CancellationToken ct)
    {
        long bytes = 0;
        var files = 0;
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var directory = stack.Pop();
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    ct.ThrowIfCancellationRequested();
                    if (++files > MaximumMeasuredFiles) return (bytes, true);
                    try { bytes = checked(bytes + new FileInfo(file).Length); } catch { }
                }
                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) stack.Push(child);
                    }
                    catch { }
                }
            }
            catch { }
        }
        return (bytes, false);
    }

    private static string? FindExecutable(string name)
    {
        var resolved = ShellRunner.ResolveExe(name);
        return Path.IsPathRooted(resolved) && File.Exists(resolved) ? resolved : null;
    }

    private static string? LastNonEmptyLine(string? value)
        => value?.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();

    private static string Bound(string value, int maximum)
        => value.Length <= maximum ? value : value[..maximum] + "…";
}
