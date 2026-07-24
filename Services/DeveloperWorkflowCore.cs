using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinForge.Services;

public enum NodeVersionManager
{
    Fnm,
    Nvm,
    Volta,
}

public enum DeveloperCacheKind
{
    Npm,
    Pnpm,
    Pip,
    Docker,
}

public sealed record DeveloperCommandPlan(
    string Executable,
    IReadOnlyList<string> Arguments,
    bool RequiresElevation = false);

public sealed record PortListener(int ProcessId, string ProcessName, string LocalAddress, int LocalPort)
{
    public string DisplayLabel => $"{ProcessName} (PID {ProcessId}) · {LocalAddress}:{LocalPort}";
}

/// <summary>
/// Pure validation and command-planning contracts for the Developer &amp; Terminal workbench.
/// Values are emitted as discrete argv items; no user input is concatenated into cmd.exe.
/// </summary>
public static class DeveloperWorkflowCore
{
    public const int MinimumPort = 1;
    public const int MaximumPort = 65535;
    public const int MinimumDynamicPortStart = 1025;
    public const int MinimumTimedWaitSeconds = 30;
    public const int MaximumTimedWaitSeconds = 300;
    public const int MaximumListeners = 128;
    public const int MaximumVersionLength = 64;

    private static readonly Regex VersionPattern = new(
        @"^(?:v)?(?:\d{1,3})(?:\.\d{1,3}){0,2}(?:-[0-9A-Za-z.-]{1,40})?$|^(?:latest|lts|lts-[0-9A-Za-z.-]{1,24})$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static int ValidatePort(int port)
        => port is >= MinimumPort and <= MaximumPort
            ? port
            : throw new ArgumentOutOfRangeException(nameof(port), $"Port must be {MinimumPort}-{MaximumPort}.");

    public static IReadOnlyList<PortListener> ParseListeners(string? json, int expectedPort)
    {
        ValidatePort(expectedPort);
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "[]") return Array.Empty<PortListener>();

        using var document = JsonDocument.Parse(json);
        var elements = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.EnumerateArray().ToArray()
            : new[] { document.RootElement };
        var listeners = new List<PortListener>();
        foreach (var element in elements.Take(MaximumListeners))
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            var pid = ReadInt(element, "ProcessId", "OwningProcess");
            var port = ReadInt(element, "LocalPort");
            if (pid <= 0 || port != expectedPort) continue;
            var name = ReadString(element, "ProcessName");
            var address = ReadString(element, "LocalAddress");
            if (string.IsNullOrWhiteSpace(name)) name = "unknown";
            if (string.IsNullOrWhiteSpace(address)) address = "*";
            listeners.Add(new PortListener(pid, Bound(name, 128), Bound(address, 128), port));
        }
        return listeners
            .GroupBy(item => item.ProcessId)
            .Select(group => group.First())
            .OrderBy(item => item.ProcessId)
            .ToArray();
    }

    public static DeveloperCommandPlan BuildTerminatePlan(int processId)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        return new DeveloperCommandPlan("taskkill.exe", new[] { "/PID", processId.ToString(), "/T", "/F" });
    }

    public static bool ReviewedListenersStillMatch(
        IReadOnlyList<PortListener>? reviewed,
        IReadOnlyList<PortListener>? current)
    {
        if (reviewed is null || current is null || reviewed.Count == 0 || reviewed.Count != current.Count) return false;
        var expected = reviewed
            .Select(item => (item.ProcessId, Name: item.ProcessName.Trim()))
            .Distinct()
            .OrderBy(item => item.ProcessId)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var actual = current
            .Select(item => (item.ProcessId, Name: item.ProcessName.Trim()))
            .Distinct()
            .OrderBy(item => item.ProcessId)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return expected.Length == actual.Length && expected.Zip(actual)
            .All(pair => pair.First.ProcessId == pair.Second.ProcessId &&
                         string.Equals(pair.First.Name, pair.Second.Name, StringComparison.OrdinalIgnoreCase));
    }

    public static string ValidateNodeVersion(string? value)
    {
        var version = (value ?? string.Empty).Trim();
        if (version.Length is 0 or > MaximumVersionLength || !VersionPattern.IsMatch(version))
            throw new ArgumentException("Enter a semantic Node version, latest, lts, or lts-<name>.", nameof(value));
        return version;
    }

    public static DeveloperCommandPlan BuildNodeListPlan(NodeVersionManager manager, string executable)
        => manager switch
        {
            NodeVersionManager.Fnm => Plan(executable, "list"),
            NodeVersionManager.Nvm => Plan(executable, "list"),
            NodeVersionManager.Volta => Plan(executable, "list", "node"),
            _ => throw new ArgumentOutOfRangeException(nameof(manager)),
        };

    public static DeveloperCommandPlan BuildNodeInstallPlan(NodeVersionManager manager, string executable, string version)
    {
        version = ValidateNodeVersion(version);
        return manager switch
        {
            NodeVersionManager.Fnm => Plan(executable, "install", version),
            NodeVersionManager.Nvm => Plan(executable, "install", version),
            NodeVersionManager.Volta => Plan(executable, "install", $"node@{version}"),
            _ => throw new ArgumentOutOfRangeException(nameof(manager)),
        };
    }

    public static DeveloperCommandPlan BuildNodeShellPlan(NodeVersionManager manager, string executable, string version)
    {
        version = ValidateNodeVersion(version);
        if (manager == NodeVersionManager.Fnm)
        {
            var literal = PowerShellLiteral(executable);
            var command = $"& {literal} env --use-on-cd --shell powershell | Out-String | Invoke-Expression; & {literal} use {PowerShellLiteral(version)}";
            return Plan("powershell.exe", "-NoExit", "-NoProfile", "-Command", command);
        }
        if (manager == NodeVersionManager.Volta)
            return Plan(executable, "run", "--node", version, "powershell.exe", "-NoExit", "-NoProfile");

        throw new InvalidOperationException(
            "nvm-windows changes the machine-wide symlink and cannot provide an isolated per-shell selection. Use fnm or Volta for this action.");
    }

    public static DeveloperCommandPlan BuildCorepackStatusPlan()
        => Plan("corepack", "--version");

    public static DeveloperCommandPlan BuildCorepackEnablePlan()
        => Plan("corepack", "enable");

    public static DeveloperCommandPlan BuildCorepackPreparePlan(string manager, string channel)
    {
        manager = (manager ?? string.Empty).Trim().ToLowerInvariant();
        if (manager is not ("pnpm" or "yarn"))
            throw new ArgumentException("Corepack manager must be pnpm or yarn.", nameof(manager));
        channel = ValidatePackageChannel(channel);
        return Plan("corepack", "prepare", $"{manager}@{channel}", "--activate");
    }

    public static string ValidateDeveloperFolder(string? path, bool requireExisting = true)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose a developer folder.", nameof(path));
        var full = Path.GetFullPath(path.Trim());
        if (requireExisting && !Directory.Exists(full)) throw new DirectoryNotFoundException(full);
        var root = Path.GetPathRoot(full);
        if (string.Equals(TrimEnd(full), TrimEnd(root ?? string.Empty), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("A drive root is too broad for a Defender exclusion.", nameof(path));

        foreach (var protectedPath in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
        }.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
        {
            if (string.Equals(TrimEnd(full), TrimEnd(protectedPath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Windows and Program Files roots cannot be excluded.", nameof(path));
        }
        return full;
    }

    public static string BuildDefenderMutationScript(string path, bool add, bool requireExisting = true)
    {
        var full = ValidateDeveloperFolder(path, requireExisting);
        var verb = add ? "Add-MpPreference" : "Remove-MpPreference";
        return $"$ErrorActionPreference='Stop'; {verb} -ExclusionPath {PowerShellLiteral(full)}";
    }

    public static void ValidateTcpTuning(int start, int count, int timedWaitSeconds)
    {
        if (start is < MinimumDynamicPortStart or > MaximumPort)
            throw new ArgumentOutOfRangeException(nameof(start), $"Dynamic-port start must be {MinimumDynamicPortStart}-{MaximumPort}.");
        if (count <= 0 || (long)start + count > MaximumPort + 1L)
            throw new ArgumentOutOfRangeException(nameof(count), "Dynamic-port range must end at or below 65535.");
        if (timedWaitSeconds is < MinimumTimedWaitSeconds or > MaximumTimedWaitSeconds)
            throw new ArgumentOutOfRangeException(nameof(timedWaitSeconds), "TIME_WAIT must be 30-300 seconds.");
    }

    public static DeveloperCommandPlan BuildDynamicPortPlan(int start, int count)
    {
        ValidateTcpTuning(start, count, MinimumTimedWaitSeconds);
        return new DeveloperCommandPlan(
            "netsh.exe",
            new[] { "int", "ipv4", "set", "dynamicport", "tcp", $"start={start}", $"num={count}" },
            RequiresElevation: true);
    }

    public static string BuildTcpTuningScript(int start, int count, int timedWaitSeconds)
    {
        ValidateTcpTuning(start, count, timedWaitSeconds);
        return "$ErrorActionPreference='Stop'; " +
               $"& netsh.exe int ipv4 set dynamicport tcp start={start} num={count}; " +
               "if ($LASTEXITCODE -ne 0) { throw \"netsh failed with exit code $LASTEXITCODE\" }; " +
               $"Set-ItemProperty -LiteralPath 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters' -Name 'TcpTimedWaitDelay' -Type DWord -Value {timedWaitSeconds}";
    }

    public static DeveloperCommandPlan BuildCacheCleanPlan(DeveloperCacheKind kind)
        => kind switch
        {
            DeveloperCacheKind.Npm => Plan("npm", "cache", "clean", "--force"),
            DeveloperCacheKind.Pnpm => Plan("pnpm", "store", "prune"),
            DeveloperCacheKind.Pip => Plan("pip", "cache", "purge"),
            DeveloperCacheKind.Docker => Plan("docker", "builder", "prune", "-f"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "unknown";
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }

    public static string PowerShellLiteral(string value)
        => $"'{(value ?? string.Empty).Replace("'", "''")}'";

    private static DeveloperCommandPlan Plan(string executable, params string[] args)
    {
        if (string.IsNullOrWhiteSpace(executable)) throw new ArgumentException("Executable is required.", nameof(executable));
        return new DeveloperCommandPlan(executable, args);
    }

    private static string ValidatePackageChannel(string? value)
    {
        var channel = (value ?? string.Empty).Trim();
        if (channel.Length is 0 or > MaximumVersionLength || !Regex.IsMatch(channel, @"^[0-9A-Za-z][0-9A-Za-z._-]*$", RegexOptions.CultureInvariant))
            throw new ArgumentException("Package-manager channel contains unsupported characters.", nameof(value));
        return channel;
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (element.TryGetProperty(name, out var value) &&
                ((value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) ||
                 (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))))
                return number;
        return 0;
    }

    private static string ReadString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string Bound(string value, int maximum)
        => value.Length <= maximum ? value : value[..maximum];

    private static string TrimEnd(string path)
        => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}
