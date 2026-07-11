using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

public sealed class VsInstance
{
    public string InstanceId { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string ProductId { get; init; } = "";
    public string InstallationPath { get; init; } = "";
    public string InstallationVersion { get; init; } = "";
    public string ChannelUri { get; init; } = "";

    public string Edition
    {
        get
        {
            if (ProductId.Contains("Enterprise", StringComparison.OrdinalIgnoreCase)) return "Enterprise";
            if (ProductId.Contains("Professional", StringComparison.OrdinalIgnoreCase)) return "Professional";
            if (ProductId.Contains("Community", StringComparison.OrdinalIgnoreCase)) return "Community";
            if (ProductId.Contains("BuildTools", StringComparison.OrdinalIgnoreCase)) return "Build Tools";
            return DisplayName;
        }
    }

    public string Summary => string.IsNullOrWhiteSpace(ChannelUri)
        ? $"{Edition} · {InstallationVersion} · {InstallationPath}"
        : $"{Edition} · {InstallationVersion} · {InstallationPath} · {ChannelUri}";
}

/// <summary>
/// Visual Studio installer panel · Detect installed instances, export/import .vsconfig files and launch
/// modify / install flows through the real Visual Studio Installer and winget. Fully bilingual.
/// </summary>
public static class VisualStudioInstallerService
{
    public const string CommunityWingetId = "Microsoft.VisualStudio.2022.Community";
    public const string ProfessionalWingetId = "Microsoft.VisualStudio.2022.Professional";
    public const string EnterpriseWingetId = "Microsoft.VisualStudio.2022.Enterprise";
    public const string BuildToolsWingetId = "Microsoft.VisualStudio.2022.BuildTools";

    private static string InstallerDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer");

    public static string SetupExe => Path.Combine(InstallerDir, "setup.exe");
    public static string VsWhereExe => Path.Combine(InstallerDir, "vswhere.exe");

    public static bool HasInstaller => File.Exists(SetupExe);
    public static bool HasVsWhere => File.Exists(VsWhereExe);

    public static async Task<List<VsInstance>> ListInstancesAsync(CancellationToken ct = default)
    {
        if (!HasVsWhere) return new List<VsInstance>();

        var outp = await ShellRunner.Capture(VsWhereExe, "-all -prerelease -products * -format json", ct);
        if (string.IsNullOrWhiteSpace(outp)) return new List<VsInstance>();

        var list = new List<VsInstance>();
        try
        {
            using var doc = JsonDocument.Parse(outp);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                list.Add(new VsInstance
                {
                    InstanceId = ReadString(el, "instanceId"),
                    DisplayName = ReadString(el, "displayName", "productPath"),
                    ProductId = ReadString(el, "productId", "catalog", "productId"),
                    InstallationPath = ReadString(el, "installationPath"),
                    InstallationVersion = ReadString(el, "installationVersion"),
                    ChannelUri = ReadString(el, "channelUri", "installChannelUri"),
                });
            }
        }
        catch
        {
            return new List<VsInstance>();
        }

        return list
            .Where(x => !string.IsNullOrWhiteSpace(x.InstallationPath))
            .OrderByDescending(x => x.InstallationVersion, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static Task<TweakResult> ExportConfigAsync(VsInstance instance, string configPath, CancellationToken ct = default)
    {
        var args = $"export --installPath \"{instance.InstallationPath}\" --config \"{configPath}\"";
        return ShellRunner.RunStreaming(SetupExe, args, onLine: null, elevated: true,
            workingDirectory: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ct);
    }

    public static Task<TweakResult> ModifyWithConfigAsync(VsInstance instance, string configPath, CancellationToken ct = default)
    {
        var args = $"modify --installPath \"{instance.InstallationPath}\" --config \"{configPath}\"";
        return ShellRunner.RunStreaming(SetupExe, args, onLine: null, elevated: true,
            workingDirectory: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ct);
    }

    public static Task<TweakResult> ModifyWorkloadsAsync(VsInstance instance, IEnumerable<string> addIds, IEnumerable<string> removeIds, CancellationToken ct = default)
    {
        var args = $"modify --installPath \"{instance.InstallationPath}\"";
        foreach (var id in addIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            args += $" --add {id.Trim()}";
        foreach (var id in removeIds.Where(x => !string.IsNullOrWhiteSpace(x)))
            args += $" --remove {id.Trim()}";
        return ShellRunner.RunStreaming(SetupExe, args, onLine: null, elevated: true,
            workingDirectory: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ct);
    }

    public static Task<TweakResult> InstallEditionAsync(string wingetId, string? configPath, CancellationToken ct = default)
    {
        var args = $"install --id {wingetId} -e --silent --accept-source-agreements --accept-package-agreements --disable-interactivity";
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            var vsconfig = $"--passive --config \"{configPath}\"";
            args = $"install --id {wingetId} -e --override \"{vsconfig}\" --accept-source-agreements --accept-package-agreements --disable-interactivity";
        }

        return ShellRunner.RunStreaming("winget", args, onLine: null, elevated: true,
            workingDirectory: Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ct);
    }

    public static string WingetIdForEdition(string edition) => edition switch
    {
        "Professional" => ProfessionalWingetId,
        "Enterprise" => EnterpriseWingetId,
        "Build Tools" => BuildToolsWingetId,
        _ => CommunityWingetId,
    };

    private static string ReadString(JsonElement el, params string[] path)
    {
        JsonElement cur = el;
        foreach (var p in path)
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(p, out var next))
                return "";
            cur = next;
        }
        return cur.ValueKind == JsonValueKind.String ? cur.GetString() ?? "" : cur.ToString();
    }
}
