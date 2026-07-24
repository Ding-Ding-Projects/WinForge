using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WinForge.Services;

if (args.Length >= 2 && args[0] == "--host-fixture")
{
    return await RunHostFixtureAsync(args[1]);
}

var failures = new List<string>();
var passed = 0;

Run("accepts a canonical local hash-pinned executable", AcceptsCanonicalPinnedExecutable);
Run("rejects a relative executable path", RejectsRelativeExecutable);
Run("rejects UNC and device executable paths", RejectsRemoteExecutablePaths);
Run("rejects an executable with the wrong hash", RejectsWrongHash);
Run("rejects unsafe host arguments", RejectsUnsafeArguments);
await RunAsync("fails closed for a disabled pack", DisabledPackFailsClosed);
await RunAsync("fails closed while elevated", ElevatedLaunchFailsClosed);
await RunAsync("requires the command declared by the pack", UndeclaredCommandFailsClosed);
await RunAsync("preserves bounded copy response text", CopyResponsePreservesText);
await RunAsync("accepts a validated bilingual structured page", StructuredPageIsValidated);
await RunAsync("rejects non-HTTP response URLs", UnsafeUrlResponseFailsClosed);
await RunAsync("rejects a mismatched response request id", MismatchedRequestFailsClosed);
await RunAsync("rejects an undefined structured field type", UndefinedFieldTypeFailsClosed);
await RunAsync("rejects multiple primary page actions", MultiplePrimaryActionsFailClosed);
await RunAsync("bounds the response line", OversizedResponseFailsClosed);
await RunAsync("cancellation kills a stalled host promptly", CancellationStopsHostPromptly);
await RunAsync("rejects oversized page field data before launch", OversizedPageFieldFailsClosed);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} Command Palette extension host tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} Command Palette extension host tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

async Task RunAsync(string name, Func<Task> test)
{
    try
    {
        await test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {name}: {exception.Message}");
    }
}

void AcceptsCanonicalPinnedExecutable()
{
    var definition = Definition("copy");
    Assert(CommandPaletteExtensionHostService.TryValidateDefinition(definition, out var error), error);
}

void RejectsRelativeExecutable()
{
    var definition = Definition("copy") with { Executable = "fixture.exe" };
    Assert(!CommandPaletteExtensionHostService.TryValidateDefinition(definition, out _), "relative path was accepted");
}

void RejectsRemoteExecutablePaths()
{
    foreach (var path in new[] { @"\\server\share\fixture.exe", @"\\?\C:\fixture.exe" })
    {
        var definition = Definition("copy") with { Executable = path };
        Assert(!CommandPaletteExtensionHostService.TryValidateDefinition(definition, out var error), $"remote/device path was accepted: {path}");
        Assert(error.Contains("local", StringComparison.OrdinalIgnoreCase), $"local-path policy was not reported for {path}");
    }
}

void RejectsWrongHash()
{
    var definition = Definition("copy") with { Sha256 = new string('0', 64) };
    Assert(!CommandPaletteExtensionHostService.TryValidateDefinition(definition, out var error), "wrong hash was accepted");
    Assert(error.Contains("SHA-256", StringComparison.Ordinal), "hash failure was not reported");
}

void RejectsUnsafeArguments()
{
    var definition = Definition("copy") with { Arguments = ["--host-fixture", "copy\r\nnext"] };
    Assert(!CommandPaletteExtensionHostService.TryValidateDefinition(definition, out _), "newline argument was accepted");
}

async Task DisabledPackFailsClosed()
{
    var (pack, command) = Fixture("copy", enabled: false);
    var response = await ExecuteAsync(pack, command);
    Assert(!response.Success && response.Error.Contains("disabled", StringComparison.OrdinalIgnoreCase), "disabled pack did not fail closed");
}

async Task ElevatedLaunchFailsClosed()
{
    var (pack, command) = Fixture("copy");
    var response = await CommandPaletteExtensionHostService.ExecuteForTestingAsync(pack, command, null, null, null, elevated: true);
    Assert(!response.Success && response.Error.Contains("elevated", StringComparison.OrdinalIgnoreCase), "elevated launch was accepted");
}

async Task UndeclaredCommandFailsClosed()
{
    var (pack, command) = Fixture("copy");
    var rogue = command with { Id = "rogue.command" };
    var response = await ExecuteAsync(pack, rogue);
    Assert(!response.Success && response.Error.Contains("approved", StringComparison.OrdinalIgnoreCase), "undeclared command was accepted");
}

async Task CopyResponsePreservesText()
{
    var (pack, command) = Fixture("copy");
    var response = await ExecuteAsync(pack, command);
    Assert(response.Success, response.Error);
    Equal(CommandPaletteExtensionHostResponseKind.Copy, response.Kind, "copy response kind");
    Equal("  bounded copy text  ", response.Target, "copy response text");
}

async Task StructuredPageIsValidated()
{
    var (pack, command) = Fixture("page");
    var response = await ExecuteAsync(pack, command);
    Assert(response.Success && response.Page is not null, response.Error);
    var page = response.Page!;
    Equal("Fixture status", page.Title, "page title");
    Equal("示範狀態", page.Zh, "page Cantonese title");
    Equal(3, page.Fields.Count, "page field count");
    Equal(1, page.Actions.Count, "page action count");
    Assert(page.Actions[0].Primary, "primary action was not retained");
}

async Task UnsafeUrlResponseFailsClosed()
{
    var (pack, command) = Fixture("file-url");
    var response = await ExecuteAsync(pack, command);
    Assert(!response.Success, "file URL response was accepted");
}

async Task MismatchedRequestFailsClosed()
{
    var (pack, command) = Fixture("mismatch");
    var response = await ExecuteAsync(pack, command);
    Assert(!response.Success && response.Error.Contains("match", StringComparison.OrdinalIgnoreCase), "mismatched request id was accepted");
}

async Task UndefinedFieldTypeFailsClosed()
{
    var (pack, command) = Fixture("undefined-field");
    var response = await ExecuteAsync(pack, command);
    Assert(!response.Success, "undefined numeric field type was accepted");
}

async Task MultiplePrimaryActionsFailClosed()
{
    var (pack, command) = Fixture("multiple-primary");
    var response = await ExecuteAsync(pack, command);
    Assert(!response.Success, "multiple primary actions were accepted");
}

async Task OversizedResponseFailsClosed()
{
    var (pack, command) = Fixture("oversized");
    var response = await ExecuteAsync(pack, command);
    Assert(!response.Success, "oversized response was accepted");
}

async Task CancellationStopsHostPromptly()
{
    var (pack, command) = Fixture("stall");
    using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
    var timer = Stopwatch.StartNew();
    var response = await ExecuteAsync(pack, command, cancellation.Token);
    timer.Stop();
    Assert(!response.Success, "stalled response succeeded");
    Assert(response.Error.Contains("cancelled", StringComparison.OrdinalIgnoreCase), "cancellation was not distinguished from timeout");
    Assert(timer.Elapsed < TimeSpan.FromSeconds(3), $"stalled host was not cancelled promptly ({timer.Elapsed})");
}

async Task OversizedPageFieldFailsClosed()
{
    var (pack, command) = Fixture("copy");
    var fields = new Dictionary<string, string> { ["field.value"] = new string('x', 4097) };
    var response = await CommandPaletteExtensionHostService.ExecuteForTestingAsync(
        pack,
        command,
        "page.status",
        "action.run",
        fields,
        elevated: false);
    Assert(!response.Success && response.Error.Contains("page data", StringComparison.OrdinalIgnoreCase), "oversized field data was accepted");
}

Task<CommandPaletteExtensionHostResponse> ExecuteAsync(
    CommandPaletteExtensionPack pack,
    CommandPaletteExtensionCommand command,
    CancellationToken cancellationToken = default) =>
    CommandPaletteExtensionHostService.ExecuteForTestingAsync(
        pack,
        command,
        null,
        null,
        null,
        elevated: false,
        cancellationToken);

(CommandPaletteExtensionPack Pack, CommandPaletteExtensionCommand Command) Fixture(string mode, bool enabled = true)
{
    var definition = Definition(mode);
    var command = new CommandPaletteExtensionCommand(
        "fixture.command",
        "Fixture command",
        "示範指令",
        "Protocol fixture",
        "協定示範",
        Array.Empty<string>(),
        Array.Empty<string>(),
        CommandPaletteExtensionAction.Host,
        "fixture.target",
        "F");
    var pack = new CommandPaletteExtensionPack(
        "fixture.pack",
        "Fixture pack",
        "示範套件",
        "Test fixture",
        "測試示範",
        definition,
        enabled,
        [command]);
    return (pack, command);
}

CommandPaletteExtensionHostDefinition Definition(string mode)
{
    var assemblyPath = Assembly.GetExecutingAssembly().Location;
    var executable = Path.Combine(
        Path.GetDirectoryName(assemblyPath)!,
        Path.GetFileNameWithoutExtension(assemblyPath) + ".exe");
    Assert(File.Exists(executable), $"test apphost was not found: {executable}");
    using var stream = new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.Read);
    return new CommandPaletteExtensionHostDefinition(
        Path.GetFullPath(executable),
        Convert.ToHexString(SHA256.HashData(stream)),
        ["--host-fixture", mode]);
}

static async Task<int> RunHostFixtureAsync(string mode)
{
    if (mode == "stall")
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        return 0;
    }

    var line = await Console.In.ReadLineAsync();
    if (string.IsNullOrWhiteSpace(line)) return 2;
    using var request = JsonDocument.Parse(line);
    var root = request.RootElement;
    var requestId = root.GetProperty("requestId").GetString() ?? string.Empty;
    var protocol = root.GetProperty("protocol").GetString() ?? string.Empty;

    if (mode == "oversized")
    {
        Console.Write(new string('x', (64 * 1024) + 1));
        return 0;
    }

    object response = mode switch
    {
        "copy" => new { protocol, requestId, kind = "copy", target = "  bounded copy text  " },
        "file-url" => new { protocol, requestId, kind = "url", target = "file:///C:/Windows/System32/calc.exe" },
        "mismatch" => new { protocol, requestId = "wrong-request", kind = "copy", target = "no" },
        "undefined-field" => PageResponse(protocol, requestId, fieldType: "999", multiplePrimary: false),
        "multiple-primary" => PageResponse(protocol, requestId, fieldType: "Text", multiplePrimary: true),
        "page" => PageResponse(protocol, requestId, fieldType: "Text", multiplePrimary: false, fullPage: true),
        _ => new { protocol, requestId, kind = "unsupported" }
    };

    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(response));
    return 0;
}

static object PageResponse(
    string protocol,
    string requestId,
    string fieldType,
    bool multiplePrimary,
    bool fullPage = false)
{
    var fields = fullPage
        ? new object[]
        {
            new { id = "field.text", label = "Message", zh = "訊息", type = fieldType, value = "hello", options = Array.Empty<object>() },
            new { id = "field.toggle", label = "Enabled", zh = "已啟用", type = "Toggle", value = "true", options = Array.Empty<object>() },
            new
            {
                id = "field.choice",
                label = "Scope",
                zh = "範圍",
                type = "Choice",
                value = "summary",
                options = new object[]
                {
                    new { value = "summary", title = "Summary", zh = "摘要" },
                    new { value = "detail", title = "Detail", zh = "詳細" }
                }
            }
        }
        : new object[]
        {
            new { id = "field.text", label = "Message", zh = "訊息", type = fieldType, value = "hello", options = Array.Empty<object>() }
        };
    var actions = multiplePrimary
        ? new object[]
        {
            new { id = "action.one", title = "One", zh = "一", primary = true },
            new { id = "action.two", title = "Two", zh = "二", primary = true }
        }
        : new object[]
        {
            new { id = "action.refresh", title = "Refresh", zh = "重新整理", primary = true }
        };
    return new
    {
        protocol,
        requestId,
        kind = "page",
        page = new
        {
            id = "page.status",
            title = "Fixture status",
            zh = "示範狀態",
            body = "Validated fixture page",
            zhBody = "已驗證示範頁",
            fields,
            actions
        }
    };
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'");
}
