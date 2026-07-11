using WinForge.Services;

var failures = new List<string>();
var passed = 0;

await RunAsync("editable names stay isolated argument-vector entries", EditableNamesStayArguments);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} QuickType security tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} QuickType security tests");
return 1;

async Task RunAsync(string name, Func<Task> test)
{
    try
    {
        await test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {name}: {ex.Message}");
    }
}

static async Task EditableNamesStayArguments()
{
    QuickTypeService.Rescan();
    ShellRunner.Reset();
    const string topLevel = "Root & whoami \"quoted\"";
    const string nameSpace = "Acme.Tools & echo unsafe";
    var result = await QuickTypeService.GenerateAsync("{\"id\":1}", new QuickTypeService.GenOptions
    {
        Input = QuickTypeService.InputKinds.Single(k => k.Key == "json"),
        Target = QuickTypeService.Targets.Single(t => t.Key == "cs"),
        TopLevelName = topLevel,
        Namespace = nameSpace,
        JustTypes = true,
        CSharpFramework = "SystemTextJson",
        CSharpArrayType = true,
        AcronymStyle = true,
    });

    Assert(result.Success, "generation fixture failed");
    Assert(ShellRunner.Invocations.All(i => !i.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase)),
        "QuickType reached cmd.exe");

    var call = ShellRunner.Invocations.Last(i => i.IsArgumentVector && i.FileName == "quicktype");
    Same(new[]
    {
        "--src-lang", "json",
        "--lang", "csharp",
        "--top-level", topLevel,
        "--just-types",
        "--namespace", nameSpace,
        "--framework", "SystemTextJson",
        "--array-type", "list",
        "--acronym-style", "original",
    }, call.Arguments.Take(call.Arguments.Count - 1).ToArray(), "fixed argv");
    Assert(call.Arguments[^1].EndsWith(".json", StringComparison.OrdinalIgnoreCase),
        "temporary input was not a single positional argument");
}

static void Same(IReadOnlyList<string> expected, IReadOnlyList<string> actual, string label)
{
    Equal(expected.Count, actual.Count, label + " count");
    for (var i = 0; i < expected.Count; i++) Equal(expected[i], actual[i], label + $"[{i}]");
}

static void Equal<T>(T expected, T actual, string label) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
