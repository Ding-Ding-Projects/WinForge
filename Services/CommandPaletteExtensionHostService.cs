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
        if (!TryOpenVerifiedHost(definition, out var verificationLease, out error))
        {
            return false;
        }

        verificationLease!.Dispose();
        return true;
    }

    public static CommandPaletteExtensionHostResponse Execute(CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command) =>
        ExecuteCommandAsync(pack, command).GetAwaiter().GetResult();

    public static Task<CommandPaletteExtensionHostResponse> ExecuteCommandAsync(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveEnabledPack(pack, command, out var currentPack, out var currentCommand, out var error))
        {
            return Task.FromResult(CommandPaletteExtensionHostResponse.Failed(error));
        }

        return ExecuteCoreAsync(currentPack, currentCommand, null, null, null, cancellationToken, null);
    }

    public static Task<CommandPaletteExtensionHostResponse> ExecutePageActionAsync(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command,
        string pageId,
        string actionId,
        IReadOnlyDictionary<string, string> fields,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveEnabledPack(pack, command, out var currentPack, out var currentCommand, out var error))
        {
            return Task.FromResult(CommandPaletteExtensionHostResponse.Failed(error));
        }

        return ExecuteCoreAsync(currentPack, currentCommand, pageId, actionId, fields, cancellationToken, null);
    }

    internal static Task<CommandPaletteExtensionHostResponse> ExecuteForTestingAsync(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command,
        string? pageId,
        string? actionId,
        IReadOnlyDictionary<string, string>? fields,
        bool elevated,
        CancellationToken cancellationToken = default) =>
        ExecuteCoreAsync(pack, command, pageId, actionId, fields, cancellationToken, () => elevated);

    private static async Task<CommandPaletteExtensionHostResponse> ExecuteCoreAsync(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command,
        string? pageId,
        string? actionId,
        IReadOnlyDictionary<string, string>? fields,
        CancellationToken cancellationToken,
        Func<bool>? elevationProbe)
    {
        if (!pack.Enabled)
        {
            return CommandPaletteExtensionHostResponse.Failed("This extension pack is disabled.");
        }

        if (pack.Host is null)
        {
            return CommandPaletteExtensionHostResponse.Failed("This command has no host definition.");
        }

        if (!IsApprovedHostCommand(pack, command))
        {
            return CommandPaletteExtensionHostResponse.Failed("This host command is not approved by the enabled pack.");
        }

        var isPageAction = pageId is not null || actionId is not null;
        if (isPageAction && (!IsSafeId(pageId) || !IsSafeId(actionId)))
        {
            return CommandPaletteExtensionHostResponse.Failed("The extension page action is invalid.");
        }

        if ((elevationProbe ?? IsElevated)())
        {
            return CommandPaletteExtensionHostResponse.Failed("Extension hosts are unavailable while WinForge is elevated.");
        }

        if (!TryCreateFields(fields, out var safeFields))
        {
            return CommandPaletteExtensionHostResponse.Failed("The extension page data is invalid.");
        }

        if (!TryOpenVerifiedHost(pack.Host, out var verificationLease, out var hostError))
        {
            return CommandPaletteExtensionHostResponse.Failed(hostError);
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
            cancellationToken.ThrowIfCancellationRequested();
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

            // Keep a read-only, no-write/no-delete file lease from hashing through
            // CreateProcess so the pinned image cannot be swapped in that interval.
            verificationLease!.Dispose();
            verificationLease = null;

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return CommandPaletteExtensionHostResponse.Failed("The extension host request was cancelled.");
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
            verificationLease?.Dispose();
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

    private static bool TryOpenVerifiedHost(
        CommandPaletteExtensionHostDefinition? definition,
        out FileStream? verificationLease,
        out string error)
    {
        verificationLease = null;
        error = string.Empty;
        if (definition is null)
        {
            error = "The host definition is missing.";
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(definition.Executable)
                || !Path.IsPathFullyQualified(definition.Executable)
                || definition.Executable.StartsWith(@"\\", StringComparison.Ordinal)
                || !string.Equals(Path.GetExtension(definition.Executable), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                error = "The host executable must be a fully qualified local .exe path.";
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

            if (definition.Arguments is null
                || definition.Arguments.Count > MaxArguments
                || definition.Arguments.Any(argument => argument is null
                    || argument.Length > MaxArgumentCharacters
                    || argument.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0))
            {
                error = "The host arguments are invalid.";
                return false;
            }

            verificationLease = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var expected = Convert.FromHexString(definition.Sha256);
            var actual = SHA256.HashData(verificationLease);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
            {
                verificationLease.Dispose();
                verificationLease = null;
                error = "The host executable no longer matches its approved SHA-256 value.";
                return false;
            }

            return true;
        }
        catch
        {
            verificationLease?.Dispose();
            verificationLease = null;
            error = "The host executable could not be verified.";
            return false;
        }
    }

    private static bool IsApprovedHostCommand(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command) =>
        command.Action == CommandPaletteExtensionAction.Host
        && pack.Commands.Any(candidate =>
            candidate.Action == CommandPaletteExtensionAction.Host
            && string.Equals(candidate.Id, command.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Target, command.Target, StringComparison.Ordinal));

    private static bool TryResolveEnabledPack(
        CommandPaletteExtensionPack requestedPack,
        CommandPaletteExtensionCommand requestedCommand,
        out CommandPaletteExtensionPack currentPack,
        out CommandPaletteExtensionCommand currentCommand,
        out string error)
    {
        currentPack = default!;
        currentCommand = default!;
        error = string.Empty;

        var installed = CommandPaletteExtensionService.I.GetEnabledPackForExecution(requestedPack.Id);
        if (installed?.Host is null || requestedPack.Host is null)
        {
            error = "The extension pack is no longer enabled or available.";
            return false;
        }

        if (!HostDefinitionsMatch(installed.Host, requestedPack.Host))
        {
            error = "The extension host definition changed; reopen the command from the current pack.";
            return false;
        }

        var command = installed.Commands.FirstOrDefault(candidate =>
            candidate.Action == CommandPaletteExtensionAction.Host
            && string.Equals(candidate.Id, requestedCommand.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(candidate.Target, requestedCommand.Target, StringComparison.Ordinal));
        if (command is null)
        {
            error = "The extension host command changed or is no longer approved.";
            return false;
        }

        currentPack = installed;
        currentCommand = command;
        return true;
    }

    private static bool HostDefinitionsMatch(
        CommandPaletteExtensionHostDefinition left,
        CommandPaletteExtensionHostDefinition right) =>
        string.Equals(left.Executable, right.Executable, StringComparison.OrdinalIgnoreCase)
        && string.Equals(left.Sha256, right.Sha256, StringComparison.OrdinalIgnoreCase)
        && left.Arguments.SequenceEqual(right.Arguments, StringComparer.Ordinal);

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
            var rawTarget = response.Target ?? string.Empty;
            var target = rawTarget.Trim();
            return kind switch
            {
                "module" when IsRegisteredModule(target) => CommandPaletteExtensionHostResponse.Effect(CommandPaletteExtensionHostResponseKind.Module, target),
                "url" when IsSafeUrl(target) => CommandPaletteExtensionHostResponse.Effect(CommandPaletteExtensionHostResponseKind.Url, target),
                "copy" when rawTarget.Length is > 0 and <= 4096 => CommandPaletteExtensionHostResponse.Effect(CommandPaletteExtensionHostResponseKind.Copy, rawTarget),
                "page" when TryCreatePage(response.Page, out var page) => CommandPaletteExtensionHostResponse.StructuredPage(page),
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
        if (source is null || source.Id is not { } pageId || !IsSafeId(pageId)) return false;

        var title = CleanSingleLine(source.Title, 160);
        if (string.IsNullOrWhiteSpace(title)) return false;
        var fields = source.Fields ?? new List<HostPageFieldModel>();
        var actions = source.Actions ?? new List<HostPageActionModel>();
        if (fields.Count > MaxFields || actions.Count > MaxActions) return false;

        var fieldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safeFields = new List<CommandPaletteExtensionHostField>(fields.Count);
        foreach (var field in fields)
        {
            if (field?.Id is not { } fieldId || !IsSafeId(fieldId) || !fieldIds.Add(fieldId)) return false;
            var label = CleanSingleLine(field.Label, 120);
            if (string.IsNullOrWhiteSpace(label)
                || !Enum.TryParse<CommandPaletteExtensionHostFieldType>(field.Type, true, out var type)
                || !Enum.IsDefined(type)) return false;

            var options = field.Options ?? new List<HostPageChoiceModel>();
            if (type == CommandPaletteExtensionHostFieldType.Choice && (options.Count is 0 or > MaxChoices)) return false;
            if (type != CommandPaletteExtensionHostFieldType.Choice && options.Count != 0) return false;

            var optionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var safeOptions = new List<CommandPaletteExtensionHostChoice>(options.Count);
            foreach (var option in options)
            {
                if (option?.Value is not { } optionValue || !IsSafeId(optionValue) || !optionValues.Add(optionValue)) return false;
                var optionTitle = CleanSingleLine(option.Title, 120);
                if (string.IsNullOrWhiteSpace(optionTitle)) return false;
                safeOptions.Add(new CommandPaletteExtensionHostChoice(
                    optionValue,
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
                fieldId,
                label,
                Fallback(CleanSingleLine(field.Zh, 120), label),
                type,
                value,
                safeOptions));
        }

        var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var safeActions = new List<CommandPaletteExtensionHostAction>(actions.Count);
        var primaryActionCount = 0;
        foreach (var action in actions)
        {
            if (action?.Id is not { } actionId || !IsSafeId(actionId) || !actionIds.Add(actionId)) return false;
            if (action.Primary && ++primaryActionCount > 1) return false;
            var actionTitle = CleanSingleLine(action.Title, 120);
            if (string.IsNullOrWhiteSpace(actionTitle)) return false;
            safeActions.Add(new CommandPaletteExtensionHostAction(
                actionId,
                actionTitle,
                Fallback(CleanSingleLine(action.Zh, 120), actionTitle),
                action.Primary));
        }

        page = new CommandPaletteExtensionHostPage(
            pageId,
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

    public static CommandPaletteExtensionHostResponse StructuredPage(CommandPaletteExtensionHostPage page) =>
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
