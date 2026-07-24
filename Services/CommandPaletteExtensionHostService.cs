using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// Runs explicitly enabled Command Palette extension hosts as short-lived child
/// processes. The host protocol is JSON Lines over standard input/output and is
/// intentionally limited to validated WinForge effects and structured pages.
/// </summary>
public static class CommandPaletteExtensionHostService
{
    public const string Protocol = "winforge.command-palette.host/1";

    private const int MaxArguments = 24;
    private const int MaxArgumentCharacters = 512;
    private const int MaxResponseCharacters = 64 * 1024;
    private const int MaxFields = 16;
    private const int MaxActions = 8;
    private const int MaxChoices = 32;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Confirms that a declared host is a canonical, hash-pinned local EXE.
    /// This is called at import time and again immediately before each launch.
    /// </summary>
    public static bool TryValidateDefinition(CommandPaletteExtensionHostDefinition? definition, out string error)
    {
        error = string.Empty;
        if (definition is null)
        {
            error = "The host definition is missing.";
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(definition.Executable)
                || !Path.IsPathRooted(definition.Executable)
                || !string.Equals(Path.GetExtension(definition.Executable), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                error = "The host executable must be an absolute .exe path.";
                return false;
            }

            var fullPath = Path.GetFullPath(definition.Executable);
            if (!string.Equals(fullPath, definition.Executable, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                error = "The host executable is unavailable.";
                return false;
            }

            if (!IsSha256(definition.Sha256))
            {
                error = "The host SHA-256 value is invalid.";
                return false;
            }

            if (definition.Arguments.Count > MaxArguments
                || definition.Arguments.Any(argument => argument is null
                    || argument.Length > MaxArgumentCharacters
                    || argument.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0))
            {
                error = "The host arguments are invalid.";
                return false;
            }

            var expected = Convert.FromHexString(definition.Sha256);
            var actual = Convert.FromHexString(HashFile(fullPath));
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                error = "The host executable no longer matches its approved SHA-256 value.";
                return false;
            }

            return true;
        }
        catch
        {
            error = "The host executable could not be verified.";
            return false;
        }
    }

    public static CommandPaletteExtensionHostResponse Execute(CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command) =>
        ExecuteAsync(pack, command, null, null, null, CancellationToken.None).GetAwaiter().GetResult();

    public static Task<CommandPaletteExtensionHostResponse> ExecutePageActionAsync(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command,
        string pageId,
        string actionId,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(pack, command, pageId, actionId, fields, cancellationToken);

    private static async Task<CommandPaletteExtensionHostResponse> ExecuteAsync(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command,
        string? pageId,
        string? actionId,
        IReadOnlyDictionary<string, string>? fields,
        CancellationToken cancellationToken)
    {
        if (pack.Host is null)
        {
            return CommandPaletteExtensionHostResponse.Failed("This command has no host definition.");
        }

        if (IsElevated())
        {
            return CommandPaletteExtensionHostResponse.Failed("Extension hosts are unavailable while WinForge is elevated.");
        }

        if (!TryValidateDefinition(pack.Host, out var hostError))
        {
            return CommandPaletteExtensionHostResponse.Failed(hostError);
        }

        if (!TryCreateFields(fields, out var safeFields))
        {
            return CommandPaletteExtensionHostResponse.Failed("The extension page data is invalid.");
        }

        var requestId = Guid.NewGuid().ToString("N");
        var request = new HostRequestModel
        {
            Protocol = Protocol,
            RequestId = requestId,
            Kind = string.IsNullOrWhiteSpace(pageId) ? "execute" : "pageAction",
            PackId = pack.Id,
            CommandId = command.Id,
            CommandTarget = command.Target,
            PageId = pageId,
            ActionId = actionId,
            Fields = safeFields
        };

        Process? process = null;
        CancellationTokenSource? timeout = null;
        try
        {
            timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(RequestTimeout);

            var start = new ProcessStartInfo
            {
                FileName = pack.Host.Executable,
                WorkingDirectory = Path.GetDirectoryName(pack.Host.Executable) ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var argument in pack.Host.Arguments) start.ArgumentList.Add(argument);

            process = new Process { StartInfo = start };
            if (!process.Start())
            {
                return CommandPaletteExtensionHostResponse.Failed("The extension host did not start.");
            }

            var stderrDrain = DrainAsync(process.StandardError, timeout.Token);
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions)).ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            process.StandardInput.Close();

            var responseLine = await ReadBoundedLineAsync(process.StandardOutput, timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                return CommandPaletteExtensionHostResponse.Failed("The extension host returned no response.");
            }

            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            try { await stderrDrain.ConfigureAwait(false); } catch { }

            return ParseResponse(responseLine, requestId);
        }
        catch (OperationCanceledException)
        {
            return CommandPaletteExtensionHostResponse.Failed("The extension host timed out.");
        }
        catch
        {
            return CommandPaletteExtensionHostResponse.Failed("The extension host could not complete the request.");
        }
        finally
        {
            timeout?.Dispose();
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(true);
                }
                catch { }
                process.Dispose();
            }
        }
    }

    private static CommandPaletteExtensionHostResponse ParseResponse(string responseLine, string requestId)
    {
        if (responseLine.Length > MaxResponseCharacters)
        {
            return CommandPaletteExtensionHostResponse.Failed("The extension host response is too large.");
        }

        try
        {
            var response = JsonSerializer.Deserialize<HostResponseModel>(responseLine, JsonOptions);
            if (response is null
                || !string.Equals(response.Protocol, Protocol, StringComparison.Ordinal)
                || !string.Equals(response.RequestId, requestId, StringComparison.Ordinal))
            {
                return CommandPaletteExtensionHostResponse.Failed("The extension host response did not match this request.");
            }

            var kind = response.Kind?.Trim().ToLowerInvariant();
            var target = response.Target?.Trim() ?? string.Empty;
            return kind switch
            {
                "module" when IsRegisteredModule(target) => CommandPaletteExtensionHostResponse.Effect(CommandPaletteExtensionHostResponseKind.Module, target),
                "url" when IsSafeUrl(target) => CommandPaletteExtensionHostResponse.Effect(CommandPaletteExtensionHostResponseKind.Url, target),
                "copy" when target.Length is > 0 and <= 4096 => CommandPaletteExtensionHostResponse.Effect(CommandPaletteExtensionHostResponseKind.Copy, target),
                "page" when TryCreatePage(response.Page, out var page) => CommandPaletteExtensionHostResponse.Page(page),
                _ => CommandPaletteExtensionHostResponse.Failed("The extension host requested an unsupported response.")
            };
        }
        catch (JsonException)
        {
            return CommandPaletteExtensionHostResponse.Failed("The extension host returned invalid JSON.");
        }
    }

    private static bool TryCreatePage(HostPageModel? source, out CommandPaletteExtensionHostPage page)
    {
        page = default!;
        if (source is null || !IsSafeId(source.Id)) return false;

        var title = CleanSingleLine(source.Title, 160);
        if (string.IsNullOrWhiteSpace(title)) return false;
        var fields = source.Fields ?? new List<HostPageFieldModel>();
        var actions = source.Actions ?? new List<HostPageActionModel>();
        if (fields.Count > MaxFields || actions.Count > MaxActions) return false;

        var fieldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safeFields = new List<CommandPaletteExtensionHostField>(fields.Count);
        foreach (var field in fields)
        {
            if (field is null || !IsSafeId(field.Id) || !fieldIds.Add(field.Id)) return false;
            var label = CleanSingleLine(field.Label, 120);
            if (string.IsNullOrWhiteSpace(label)
                || !Enum.TryParse<CommandPaletteExtensionHostFieldType>(field.Type, true, out var type)) return false;

            var options = field.Options ?? new List<HostPageChoiceModel>();
            if (type == CommandPaletteExtensionHostFieldType.Choice && (options.Count is 0 or > MaxChoices)) return false;
            if (type != CommandPaletteExtensionHostFieldType.Choice && options.Count != 0) return false;

            var optionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safeOptions = new List<CommandPaletteExtensionHostChoice>(options.Count);
            foreach (var option in options)
            {
                if (option is null || !IsSafeId(option.Value) || !optionValues.Add(option.Value)) return false;
                var optionTitle = CleanSingleLine(option.Title, 120);
                if (string.IsNullOrWhiteSpace(optionTitle)) return false;
                safeOptions.Add(new CommandPaletteExtensionHostChoice(
                    option.Value,
                    optionTitle,
                    Fallback(CleanSingleLine(option.Zh, 120), optionTitle)));
            }

            var value = type == CommandPaletteExtensionHostFieldType.Toggle
                ? string.Equals(field.Value, "true", StringComparison.OrdinalIgnoreCase) ? "true" : "false"
                : CleanSingleLine(field.Value, 1024);
            if (type == CommandPaletteExtensionHostFieldType.Choice
                && !safeOptions.Any(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase)))
            {
                value = safeOptions[0].Value;
            }

            safeFields.Add(new CommandPaletteExtensionHostField(
                field.Id,
                label,
                Fallback(CleanSingleLine(field.Zh, 120), label),
                type,
                value,
                safeOptions));
        }

        var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safeActions = new List<CommandPaletteExtensionHostAction>(actions.Count);
        foreach (var action in actions)
        {
            if (action is null || !IsSafeId(action.Id) || !actionIds.Add(action.Id)) return false;
            var actionTitle = CleanSingleLine(action.Title, 120);
            if (string.IsNullOrWhiteSpace(actionTitle)) return false;
            safeActions.Add(new CommandPaletteExtensionHostAction(
                action.Id,
                actionTitle,
                Fallback(CleanSingleLine(action.Zh, 120), actionTitle),
                action.Primary));
        }

        page = new CommandPaletteExtensionHostPage(
            source.Id,
            title,
            Fallback(CleanSingleLine(source.Zh, 160), title),
            CleanMultiline(source.Body, 16 * 1024),
            CleanMultiline(source.ZhBody, 16 * 1024),
            safeFields,
            safeActions);
        return true;
    }

    private static bool TryCreateFields(IReadOnlyDictionary<string, string>? source, out Dictionary<string, string>? fields)
    {
        fields = null;
        if (source is null || source.Count == 0) return true;
        if (source.Count > MaxFields) return false;

        var safe = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (!IsSafeId(pair.Key) || pair.Value is null || pair.Value.Length > 4096) return false;
            safe[pair.Key] = pair.Value.Replace("\0", string.Empty, StringComparison.Ordinal);
        }
        fields = safe;
        return true;
    }

    private static async Task<string?> ReadBoundedLineAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var buffer = new char[1];
        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0) return builder.Length == 0 ? null : builder.ToString();
            var character = buffer[0];
            if (character == '\n') return builder.ToString();
            if (character != '\r') builder.Append(character);
            if (builder.Length > MaxResponseCharacters) throw new InvalidDataException("The response is too large.");
        }
    }

    private static async Task DrainAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        while (await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false) > 0)
        {
            // Intentionally discard stderr. Host diagnostics are not surfaced because they may contain secrets.
        }
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return true;
        }
    }

    private static bool IsRegisteredModule(string target) => ModuleRegistry.All.Any(module =>
        string.Equals(module.Tag, target, StringComparison.OrdinalIgnoreCase));

    private static bool IsSafeUrl(string target) => Uri.TryCreate(target, UriKind.Absolute, out var uri)
        && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    private static bool IsSafeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 3 or > 80) return false;
        foreach (var character in value)
        {
            var isLetter = character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
            var isDigit = character is >= '0' and <= '9';
            if (!isLetter && !isDigit && character is not '.' and not '_' and not '-') return false;
        }
        return true;
    }

    private static bool IsSha256(string? value) => value?.Length == 64 && value.All(Uri.IsHexDigit);

    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string CleanSingleLine(string? value, int maximumLength)
    {
        var cleaned = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength];
    }

    private static string CleanMultiline(string? value, int maximumLength)
    {
        var cleaned = (value ?? string.Empty).Replace("\0", string.Empty, StringComparison.Ordinal).Trim();
        return cleaned.Length <= maximumLength ? cleaned : cleaned[..maximumLength];
    }

    private static string Fallback(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private sealed class HostRequestModel
    {
        public string Protocol { get; set; } = "";
        public string RequestId { get; set; } = "";
        public string Kind { get; set; } = "";
        public string PackId { get; set; } = "";
        public string CommandId { get; set; } = "";
        public string CommandTarget { get; set; } = "";
        public string? PageId { get; set; }
        public string? ActionId { get; set; }
        public Dictionary<string, string>? Fields { get; set; }
    }

    private sealed class HostResponseModel
    {
        public string? Protocol { get; set; }
        public string? RequestId { get; set; }
        public string? Kind { get; set; }
        public string? Target { get; set; }
        public HostPageModel? Page { get; set; }
    }

    private sealed class HostPageModel
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Zh { get; set; }
        public string? Body { get; set; }
        public string? ZhBody { get; set; }
        public List<HostPageFieldModel>? Fields { get; set; }
        public List<HostPageActionModel>? Actions { get; set; }
    }

    private sealed class HostPageFieldModel
    {
        public string? Id { get; set; }
        public string? Label { get; set; }
        public string? Zh { get; set; }
        public string? Type { get; set; }
        public string? Value { get; set; }
        public List<HostPageChoiceModel>? Options { get; set; }
    }

    private sealed class HostPageChoiceModel
    {
        public string? Value { get; set; }
        public string? Title { get; set; }
        public string? Zh { get; set; }
    }

    private sealed class HostPageActionModel
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Zh { get; set; }
        public bool Primary { get; set; }
    }
}

public sealed record CommandPaletteExtensionHostDefinition(
    string Executable,
    string Sha256,
    IReadOnlyList<string> Arguments);

public enum CommandPaletteExtensionHostResponseKind
{
    None,
    Module,
    Url,
    Copy,
    Page
}

public sealed record CommandPaletteExtensionHostResponse(
    bool Success,
    CommandPaletteExtensionHostResponseKind Kind,
    string Target,
    CommandPaletteExtensionHostPage? Page,
    string Error)
{
    public static CommandPaletteExtensionHostResponse Failed(string error) =>
        new(false, CommandPaletteExtensionHostResponseKind.None, string.Empty, null, error);

    public static CommandPaletteExtensionHostResponse Effect(CommandPaletteExtensionHostResponseKind kind, string target) =>
        new(true, kind, target, null, string.Empty);

    public static CommandPaletteExtensionHostResponse Page(CommandPaletteExtensionHostPage page) =>
        new(true, CommandPaletteExtensionHostResponseKind.Page, string.Empty, page, string.Empty);
}

public sealed record CommandPaletteExtensionHostPage(
    string Id,
    string Title,
    string Zh,
    string Body,
    string ZhBody,
    IReadOnlyList<CommandPaletteExtensionHostField> Fields,
    IReadOnlyList<CommandPaletteExtensionHostAction> Actions);

public enum CommandPaletteExtensionHostFieldType
{
    Text,
    Toggle,
    Choice
}

public sealed record CommandPaletteExtensionHostField(
    string Id,
    string Label,
    string Zh,
    CommandPaletteExtensionHostFieldType Type,
    string Value,
    IReadOnlyList<CommandPaletteExtensionHostChoice> Options);

public sealed record CommandPaletteExtensionHostChoice(string Value, string Title, string Zh);

public sealed record CommandPaletteExtensionHostAction(string Id, string Title, string Zh, bool Primary);
