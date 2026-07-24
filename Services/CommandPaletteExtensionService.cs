using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// Stores user-managed Command Palette extension manifests in a WinForge-owned
/// location. The manifest format is deliberately declarative: it can open an
/// installed WinForge module, open an HTTP(S) URL, or copy text, but it cannot
/// execute arbitrary code or commands.
/// </summary>
public sealed class CommandPaletteExtensionService
{
    private const int MaxManifestBytes = 256 * 1024;
    private const int MaxPacks = 64;
    private const int MaxCommandsPerPack = 50;
    private const string EnabledSuffix = ".enabled";

    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _cacheGate = new();
    private IReadOnlyList<CommandPaletteExtensionPack>? _installed;

    public static CommandPaletteExtensionService I { get; } = new();

    private CommandPaletteExtensionService()
    {
    }

    public IReadOnlyList<CommandPaletteExtensionPack> Installed
    {
        get
        {
            lock (_cacheGate)
            {
                return _installed ??= LoadInstalled();
            }
        }
    }

    public CommandPaletteExtensionImportResult TryImport(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return CommandPaletteExtensionImportResult.Failed("The manifest file was not found.");
        }

        try
        {
            var info = new FileInfo(sourcePath);
            if (info.Length > MaxManifestBytes)
            {
                return CommandPaletteExtensionImportResult.Failed("The manifest is larger than the supported limit.");
            }

            var json = File.ReadAllText(sourcePath, Encoding.UTF8);
            var manifest = JsonSerializer.Deserialize<ManifestModel>(json, JsonOptions);
            if (!TryCreatePack(manifest, false, out var pack, out var error))
            {
                return CommandPaletteExtensionImportResult.Failed(error);
            }

            Directory.CreateDirectory(ExtensionDirectory);
            var manifestPath = GetManifestPath(pack.Id);
            var isNewPack = !File.Exists(manifestPath);
            if (isNewPack && Installed.Count >= MaxPacks)
            {
                return CommandPaletteExtensionImportResult.Failed("The extension pack limit has been reached.");
            }

            var temporaryPath = manifestPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, json, Utf8NoBom);
                File.Move(temporaryPath, manifestPath, true);

                // New packs are opt-in. Updating an existing pack keeps its explicit state.
                if (isNewPack && File.Exists(GetEnabledPath(pack.Id)))
                {
                    File.Delete(GetEnabledPath(pack.Id));
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                catch
                {
                    // A stale temporary import file is harmless and stays inside WinForge storage.
                }
            }

            var enabled = !isNewPack && IsEnabled(pack.Id);
            InvalidateCache();
            return CommandPaletteExtensionImportResult.Succeeded(pack with { Enabled = enabled });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return CommandPaletteExtensionImportResult.Failed("WinForge could not store the manifest safely.");
        }
    }

    public bool SetEnabled(string id, bool enabled)
    {
        if (!IsSafeId(id))
        {
            return false;
        }

        try
        {
            if (!File.Exists(GetManifestPath(id)))
            {
                return false;
            }

            Directory.CreateDirectory(ExtensionDirectory);
            var enabledPath = GetEnabledPath(id);
            if (enabled)
            {
                File.WriteAllText(enabledPath, "enabled", Encoding.ASCII);
            }
            else if (File.Exists(enabledPath))
            {
                File.Delete(enabledPath);
            }

            InvalidateCache();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public bool TryRemove(string id)
    {
        if (!IsSafeId(id))
        {
            return false;
        }

        try
        {
            var manifestPath = GetManifestPath(id);
            if (!File.Exists(manifestPath))
            {
                return false;
            }

            File.Delete(manifestPath);
            var enabledPath = GetEnabledPath(id);
            if (File.Exists(enabledPath))
            {
                File.Delete(enabledPath);
            }

            InvalidateCache();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public string CreateManifestTemplate()
    {
        var template = new ManifestModel
        {
            Schema = 1,
            Id = "example.quick-actions",
            Name = "Example quick actions",
            Zh = "示範快速操作",
            Description = "A safe declarative Command Palette extension pack.",
            ZhDescription = "安全、宣告式嘅 Command Palette 擴充套件。",
            Commands = new List<ManifestCommandModel>
            {
                new()
                {
                    Id = "open-awake",
                    Title = "Open Awake",
                    Zh = "開啟 Awake",
                    Subtitle = "Open the WinForge Awake module",
                    ZhSubtitle = "開啟 WinForge Awake 模組",
                    Keywords = new List<string> { "awake", "keep awake" },
                    Aliases = new List<string> { "wake" },
                    Action = "Module",
                    Target = "module.awake",
                    Glyph = "\uE8A7"
                }
            }
        };

        return JsonSerializer.Serialize(template, JsonOptions);
    }

    private IReadOnlyList<CommandPaletteExtensionPack> LoadInstalled()
    {
        try
        {
            if (!Directory.Exists(ExtensionDirectory))
            {
                return Array.Empty<CommandPaletteExtensionPack>();
            }

            return Directory.EnumerateFiles(ExtensionDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .Select(ReadPack)
                .OfType<CommandPaletteExtensionPack>()
                .GroupBy(pack => pack.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(pack => pack.Name, StringComparer.OrdinalIgnoreCase)
                .Take(MaxPacks)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<CommandPaletteExtensionPack>();
        }
    }

    private CommandPaletteExtensionPack? ReadPack(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > MaxManifestBytes)
            {
                return null;
            }

            var manifest = JsonSerializer.Deserialize<ManifestModel>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
            return TryCreatePack(manifest, IsEnabled(manifest?.Id), out var pack, out _) ? pack : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool TryCreatePack(ManifestModel? manifest, bool enabled, out CommandPaletteExtensionPack pack, out string error)
    {
        pack = default!;
        error = string.Empty;

        if (manifest is null || manifest.Schema != 1)
        {
            error = "The manifest schema is not supported.";
            return false;
        }

        var id = NormalizeId(manifest.Id);
        if (!IsSafeId(id))
        {
            error = "The extension id is invalid.";
            return false;
        }

        var name = CleanSingleLine(manifest.Name, 120);
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "The extension needs a name.";
            return false;
        }

        CommandPaletteExtensionHostDefinition? host = null;
        if (manifest.Host is not null && !TryCreateHost(manifest.Host, out host, out error))
        {
            return false;
        }

        var commands = manifest.Commands ?? new List<ManifestCommandModel>();
        if (commands.Count is 0 or > MaxCommandsPerPack)
        {
            error = "The extension command count is outside the supported range.";
            return false;
        }

        var commandIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safeCommands = new List<CommandPaletteExtensionCommand>(commands.Count);
        foreach (var command in commands)
        {
            if (command is null)
            {
                error = "The extension contains an invalid command.";
                return false;
            }

            var commandId = NormalizeId(command.Id);
            if (!IsSafeId(commandId) || !commandIds.Add(commandId))
            {
                error = "Each extension command needs a unique safe id.";
                return false;
            }

            var title = CleanSingleLine(command.Title, 140);
            if (string.IsNullOrWhiteSpace(title))
            {
                error = "Each extension command needs a title.";
                return false;
            }

            if (!Enum.TryParse<CommandPaletteExtensionAction>(command.Action, true, out var action))
            {
                error = "The extension command action is not supported.";
                return false;
            }

            var target = command.Target?.Trim() ?? string.Empty;
            if (!IsSafeTarget(action, target, host))
            {
                error = "The extension command target is not safe.";
                return false;
            }

            safeCommands.Add(new CommandPaletteExtensionCommand(
                commandId,
                title,
                Fallback(CleanSingleLine(command.Zh, 140), title),
                CleanSingleLine(command.Subtitle, 220),
                CleanSingleLine(command.ZhSubtitle, 220),
                CleanWords(command.Keywords, 20, 80),
                CleanWords(command.Aliases, 20, 80),
                action,
                target,
                Fallback(CleanSingleLine(command.Glyph, 8), "\uE8A7")));
        }

        var description = CleanSingleLine(manifest.Description, 280);
        var zhDescription = CleanSingleLine(manifest.ZhDescription, 280);
        if (host is not null)
        {
            description = AppendDescription(description,
                "Runs an explicitly enabled, hash-verified extension executable in a separate process; it is not sandboxed.");
            zhDescription = AppendDescription(zhDescription,
                "會喺獨立程序運行明確啟用、雜湊驗證嘅擴充套件可執行檔；唔係沙箱。");
        }

        pack = new CommandPaletteExtensionPack(
            id,
            name,
            Fallback(CleanSingleLine(manifest.Zh, 120), name),
            description,
            zhDescription,
            host,
            enabled,
            safeCommands);
        return true;
    }

    private static bool TryCreateHost(HostModel source, out CommandPaletteExtensionHostDefinition? host, out string error)
    {
        host = null;
        error = string.Empty;
        var executable = source.Executable?.Trim() ?? string.Empty;
        var sha256 = source.Sha256?.Trim().ToUpperInvariant() ?? string.Empty;
        if (executable.Length is 0 or > 1024 || sha256.Length != 64)
        {
            error = "The extension host definition is invalid.";
            return false;
        }

        var arguments = source.Arguments ?? new List<string>();
        if (arguments.Count > 24 || arguments.Any(argument => argument is null
            || argument.Length > 512 || argument.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0))
        {
            error = "The extension host arguments are invalid.";
            return false;
        }

        var definition = new CommandPaletteExtensionHostDefinition(executable, sha256, arguments.ToArray());
        if (!CommandPaletteExtensionHostService.TryValidateDefinition(definition, out error))
        {
            return false;
        }

        host = definition;
        return true;
    }

    private static bool IsSafeTarget(CommandPaletteExtensionAction action, string target, CommandPaletteExtensionHostDefinition? host)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        return action switch
        {
            CommandPaletteExtensionAction.Module => ModuleRegistry.All.Any(module => string.Equals(module.Tag, target, StringComparison.OrdinalIgnoreCase)),
            CommandPaletteExtensionAction.Url => Uri.TryCreate(target, UriKind.Absolute, out var uri)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)),
            CommandPaletteExtensionAction.Copy => target.Length <= 4096,
            CommandPaletteExtensionAction.Host => host is not null && IsSafeId(NormalizeId(target)),
            _ => false
        };
    }

    private static IReadOnlyList<string> CleanWords(IEnumerable<string>? values, int maximumCount, int maximumLength)
    {
        if (values is null)
        {
            return Array.Empty<string>();
        }

        return values
            .Select(value => CleanSingleLine(value, maximumLength))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maximumCount)
            .ToArray();
    }

    private static string CleanSingleLine(string? value, int maximumLength)
    {
        var cleaned = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength];
    }

    private static string Fallback(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string AppendDescription(string description, string note) =>
        string.IsNullOrWhiteSpace(description) ? note : description + " " + note;

    private static string NormalizeId(string? id) => CleanSingleLine(id, 80).ToLowerInvariant();

    private static bool IsSafeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length is < 3 or > 80)
        {
            return false;
        }

        foreach (var character in id)
        {
            var isLetter = character is >= 'a' and <= 'z';
            var isDigit = character is >= '0' and <= '9';
            if (!isLetter && !isDigit && character is not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    private static string ExtensionDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinForge",
        "CommandPaletteExtensions");

    private static string GetManifestPath(string id) => Path.Combine(ExtensionDirectory, id + ".json");

    private static string GetEnabledPath(string id) => Path.Combine(ExtensionDirectory, id + EnabledSuffix);

    private static bool IsEnabled(string? id) => IsSafeId(id) && File.Exists(GetEnabledPath(id!));

    private void InvalidateCache()
    {
        lock (_cacheGate)
        {
            _installed = null;
        }
    }

    private sealed class ManifestModel
    {
        public int Schema { get; set; }
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Zh { get; set; }
        public string? Description { get; set; }
        public string? ZhDescription { get; set; }
        public HostModel? Host { get; set; }
        public List<ManifestCommandModel>? Commands { get; set; }
    }

    private sealed class HostModel
    {
        public string? Executable { get; set; }
        public string? Sha256 { get; set; }
        public List<string>? Arguments { get; set; }
    }

    private sealed class ManifestCommandModel
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Zh { get; set; }
        public string? Subtitle { get; set; }
        public string? ZhSubtitle { get; set; }
        public List<string>? Keywords { get; set; }
        public List<string>? Aliases { get; set; }
        public string? Action { get; set; }
        public string? Target { get; set; }
        public string? Glyph { get; set; }
    }
}

public enum CommandPaletteExtensionAction
{
    Module,
    Url,
    Copy,
    Host
}

public sealed record CommandPaletteExtensionPack(
    string Id,
    string Name,
    string Zh,
    string Description,
    string ZhDescription,
    CommandPaletteExtensionHostDefinition? Host,
    bool Enabled,
    IReadOnlyList<CommandPaletteExtensionCommand> Commands);

public sealed record CommandPaletteExtensionCommand(
    string Id,
    string Title,
    string Zh,
    string Subtitle,
    string ZhSubtitle,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> Aliases,
    CommandPaletteExtensionAction Action,
    string Target,
    string Glyph);

public sealed record CommandPaletteExtensionImportResult(bool Success, CommandPaletteExtensionPack? Pack, string Error)
{
    public static CommandPaletteExtensionImportResult Succeeded(CommandPaletteExtensionPack pack) => new(true, pack, string.Empty);

    public static CommandPaletteExtensionImportResult Failed(string error) => new(false, null, error);
}
