using System.Text.Json;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;
Run("JSON current-option round-trip", () => RoundTrip("json"));
Run("YAML current-option round-trip", () => RoundTrip("yaml"));
Run("XML current-option round-trip", () => RoundTrip("xml"));
Run("JSON legacy aliases", LegacyJson);
Run("YAML legacy aliases", LegacyYaml);
Run("XML legacy aliases", LegacyXml);
Run("blank source compatibility", BlankSource);
Run("explicit local-source incompatibility", LocalSource);
Run("default source command compatibility", DefaultSourceCommandCompatibility);
Run("valid source preview and identity forwarding", ValidSourcePreviewAndIdentity);
Run("malicious package-reference rejection", RejectsMaliciousReferences);
Run("malicious source rejection", RejectsMaliciousSource);
Run("malicious structured-option rejection", RejectsMaliciousStructuredOptions);
Run("valid manager-specific references", AcceptsValidReferences);
Run("security warnings use current fields", SecurityWarnings);
Run("HasOptions uses current schema", HasOptions);
await RunAsyncCase("coordinator rejects unsafe options", CoordinatorRejectsUnsafeOptions);
await RunAsyncCase("minor-update policy matches UniGetUI", MinorUpdatePolicy);
await RunAsyncCase("option-sensitive duplicate suppression", OptionSensitiveDeduplication);
await RunAsyncCase("source-sensitive duplicate suppression", SourceSensitiveDeduplication);
await RunAsyncCase("known source uninstall remains operable", KnownSourceUninstallCompatibility);
await RunAsyncCase("queued caller cancellation completes promptly", QueuedCancellation);
await RunAsyncCase("conflicting package operations serialize", ConflictingOperationsSerialize);
await RunAsyncCase("cancel all never starts queued work", CancelAllIsAtomic);
await RunAsyncCase("operation output is bounded and redacted", OutputIsBoundedAndRedacted);
await RunAsyncCase("cleanup timeout remains a failure", CleanupTimeoutRemainsFailure);
Run("tray view deep-link routing", TrayViewDeepLinkRouting);
if (failures.Count == 0) { Console.WriteLine($"PASS {passed}/{passed} package-manager core tests"); return 0; }
foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} package-manager core tests");
return 1;
void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

async Task RunAsyncCase(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static void TrayViewDeepLinkRouting()
{
    var cases = new[]
    {
        (PackageManagerViewTarget.Discover, "discover", 0),
        (PackageManagerViewTarget.Updates, "updates", 1),
        (PackageManagerViewTarget.Installed, "installed", 2),
        (PackageManagerViewTarget.Bundles, "bundles", 3),
        (PackageManagerViewTarget.Sources, "sources", 4),
        (PackageManagerViewTarget.Ignored, "ignored", 5),
        (PackageManagerViewTarget.Setup, "setup", 6),
        (PackageManagerViewTarget.Settings, "settings", 7),
        (PackageManagerViewTarget.Operations, "operations", 8),
    };

    foreach (var (target, fragment, index) in cases)
    {
        Equal($"module.packages#{fragment}", PackageManagerViewRouting.NavigationKey(target), "navigation key");
        Assert(PackageManagerViewRouting.TryGetViewIndex(fragment.ToUpperInvariant(), out var actual), "fragment did not parse");
        Equal(index, actual, "view index");
    }

    Assert(!PackageManagerViewRouting.TryGetViewIndex("unsupported", out _), "unsupported view was accepted");
    Assert(!PackageManagerViewRouting.TryGetViewIndex(null, out _), "empty view was accepted");
}

static void RoundTrip(string format)
{
    var original = FullBundle();
    string text;
    SerializableBundle actual;
    switch (format)
    {
        case "json": text = BundleService.ToJson(original); actual = BundleService.FromJson(text); break;
        case "yaml": text = BundleService.ToYaml(original); actual = BundleService.FromYaml(text); AssertContains(text, "\\n"); break;
        default: text = BundleService.ToXml(original); actual = BundleService.FromXml(text); break;
    }
    AssertContains(text, "Interactive"); AssertContains(text, "CustomArgsInstall");
    AssertNotContains(text, "InteractiveInstallation"); AssertNotContains(text, "CustomParameters_Install");
    Equal(1, actual.packages.Count, "package count");
    var expected = original.packages.Single(); var found = actual.packages.Single();
    Equal(expected.Id, found.Id, "id"); Equal(expected.Source, found.Source, "source");
    SameOptions(expected.InstallationOptions!, found.InstallationOptions);
}

static void LegacyJson()
{
    const string text = """
        {"export_version":3,"packages":[{"Id":"Example.Tool","ManagerName":"winget",
        "InstallationOptions":{"InteractiveInstallation":true,"InstallationScope":"machine",
        "CustomParameters_Install":["--channel","preview"],"CustomParameters_Update":"--force",
        "CustomParameters_Uninstall":["--purge"]}}],"incompatible_packages":[]}
        """;
    AssertLegacy(Options(BundleService.FromJson(text)), "machine", "--channel preview", "--force", "--purge");
    const string both = """
        {"export_version":3,"packages":[{"Id":"Example.Tool","ManagerName":"winget",
        "InstallationOptions":{"Interactive":false,"InteractiveInstallation":true,
        "Scope":"user","InstallationScope":"machine"}}],"incompatible_packages":[]}
        """;
    var canonical = Options(BundleService.FromJson(both));
    Assert(!canonical.Interactive, "legacy bool overrode canonical bool");
    Equal("user", canonical.Scope, "legacy scope overrode canonical scope");
}

static void LegacyYaml()
{
    const string text = """
        export_version: 3
        packages:
          - Id: Example.Tool
            ManagerName: winget
            InstallationOptions:
              InteractiveInstallation: true
              InstallationScope: user
              CustomParameters_Install:
                - --channel
                - "preview build"
              CustomParameters_Update: --force
              CustomParameters_Uninstall: --purge
        incompatible_packages:
        """;
    AssertLegacy(Options(BundleService.FromYaml(text)), "user", "--channel preview build", "--force", "--purge");
}

static void LegacyXml()
{
    const string text = """
        <Bundle><export_version>3</export_version><packages><Package>
        <Id>Example.Tool</Id><ManagerName>winget</ManagerName><InstallationOptions>
        <InteractiveInstallation>true</InteractiveInstallation><InstallationScope>machine</InstallationScope>
        <CustomParameters_Install><item>--channel</item><item>preview</item></CustomParameters_Install>
        <CustomParameters_Update>--force</CustomParameters_Update>
        <CustomParameters_Uninstall><item>--purge</item></CustomParameters_Uninstall>
        </InstallationOptions></Package></packages><incompatible_packages /></Bundle>
        """;
    AssertLegacy(Options(BundleService.FromXml(text)), "machine", "--channel preview", "--force", "--purge");
}

static void BlankSource()
{
    var options = new InstallOptions { AutoUpdate = true };
    var bundle = BundleService.ToBundle(new[]
    {
        new PackageItem { ManagerKey = "pip", Id = "requests", Name = "Requests", Source = "" },
    }, _ => options);
    Equal(1, bundle.packages.Count, "blank-source compatible count");
    Equal(0, bundle.incompatible_packages.Count, "blank-source incompatible count");
    Assert(ReferenceEquals(options, bundle.packages[0].InstallationOptions), "option lookup was not attached");
}

static void LocalSource()
{
    var bundle = BundleService.ToBundle(new[]
    {
        new PackageItem { ManagerKey = "winget", Id = "Example.Tool", Source = "Local PC" },
    });
    Equal(0, bundle.packages.Count, "local compatible count");
    Equal(1, bundle.incompatible_packages.Count, "local incompatible count");
}

static void DefaultSourceCommandCompatibility()
{
    PackageOperations.Reset();
    var command = BundleService.InstallCommandFor("winget", "Example.Tool", new InstallOptions(), source: "");
    Assert(command.Length > 0, "blank source did not preserve default command behavior");
    Equal("", PackageOperations.LastPreviewSource, "blank source changed preview input");
    Equal(1, PackageOperations.BuildCalls, "default source did not reach command builder");

    Assert(PackageSourcePolicy.TryResolve("dotnet", "Example.Tool", "nuget.org",
        PackageOperations.Op.Uninstall, out var dotnetUninstall, out _), "known dotnet uninstall source was rejected");
    Equal("", dotnetUninstall.CommandSuffix, "dotnet uninstall invented an unsupported source flag");
}

static void ValidSourcePreviewAndIdentity()
{
    PackageOperations.Reset();
    Assert(PackageSourcePolicy.TryResolve("winget", "Example.Tool", "msstore",
        PackageOperations.Op.Install, out var resolved, out _), "known winget source was rejected");
    Equal("--source msstore", resolved.CommandSuffix, "winget source suffix");

    var command = BundleService.InstallCommandFor("winget", "Example.Tool", new InstallOptions(), "msstore");
    AssertContains(command, "--source msstore");
    Equal("msstore", PackageOperations.LastPreviewSource, "source was dropped before preview");

    var wingetKey = PackageSourcePolicy.IdentityKey("winget", "Example.Tool", "winget", PackageOperations.Op.Install);
    var storeKey = PackageSourcePolicy.IdentityKey("winget", "Example.Tool", "msstore", PackageOperations.Op.Install);
    var storeCaseKey = PackageSourcePolicy.IdentityKey("winget", "Example.Tool", "MSStore", PackageOperations.Op.Install);
    Assert(wingetKey != storeKey, "different valid sources collapsed in shared identity");
    Equal(storeKey, storeCaseKey, "source identity was not canonicalized");

    Assert(PackageSourcePolicy.TryResolve("scoop", "7zip", "extras", PackageOperations.Op.Install,
        out var scoop, out _), "known Scoop bucket was rejected");
    Equal("extras/7zip", scoop.PackageId, "Scoop source did not qualify the package reference");
}

static void RejectsMaliciousReferences()
{
    PackageOperations.Reset();
    foreach (var id in new[]
    {
        "safe & calc", "safe;calc", "safe|calc", "$(calc)", "`calc`", "safe name",
        "../evil", "safe\ncalc", "pkg<in", "pkg>out", "pkg%PATH%",
    }) Equal("", BundleService.InstallCommandFor("winget", id), $"accepted malicious id {id}");
    Equal("", BundleService.InstallCommandFor("winget & calc", "Safe.Tool"), "accepted malicious manager");
    Equal("", BundleService.InstallCommandFor("unknown", "Safe.Tool"), "accepted unknown manager");
    Equal(0, PackageOperations.BuildCalls, "invalid reference reached command builder");

    var script = BundleService.GenerateInstallScript(new SerializableBundle
    {
        packages = new() { new() { ManagerName = "winget", Id = "safe & calc", Name = "Rejected" } },
    });
    AssertNotContains(script, "safe & calc");
    Equal(0, PackageOperations.BuildCalls, "invalid scripted reference reached command builder");
}

static void RejectsMaliciousSource()
{
    const string malicious = "winget & calc";
    PackageOperations.Reset();
    Equal("", BundleService.InstallCommandFor("winget", "Example.Tool", new InstallOptions(), malicious),
        "accepted malicious source");
    Equal(0, PackageOperations.BuildCalls, "malicious source reached command builder");

    var bundle = BundleService.ToBundle(new[]
    {
        new PackageItem { ManagerKey = "winget", Id = "Example.Tool", Source = malicious },
    });
    Equal(0, bundle.packages.Count, "malicious source stayed compatible in bundle");
    Equal(1, bundle.incompatible_packages.Count, "malicious source was not logged incompatible");

    var script = BundleService.GenerateInstallScript(new SerializableBundle
    {
        packages = new()
        {
            new() { ManagerName = "winget", Id = "Example.Tool", Name = "Unsafe source", Source = malicious },
        },
    });
    AssertNotContains(script, malicious);
    Equal(0, PackageOperations.BuildCalls, "malicious scripted source reached command builder");
}

static void AcceptsValidReferences()
{
    PackageOperations.Reset();
    var values = new[]
    {
        ("winget", "Microsoft.VCRedist.2015+.x64"), ("npm", "@scope/pkg-name"),
        ("bun", "@scope/pkg-name"), ("scoop", "extras/7zip"),
        ("vcpkg", "boost[filesystem,system]:x64-windows"), ("psgallery", "Az.Accounts"),
    };
    foreach (var (manager, id) in values)
        Assert(BundleService.InstallCommandFor(manager, id, new InstallOptions { CustomArgsInstall = "--test" }).Length > 0,
            $"rejected valid {manager} reference {id}");
    Equal(values.Length, PackageOperations.BuildCalls, "valid command-builder call count");
    AssertContains(BundleService.InstallCommandFor("psgallery", "Az.Accounts"), "-EncodedCommand");
}

static void RejectsMaliciousStructuredOptions()
{
    PackageOperations.Reset();
    var malicious = new InstallOptions { Version = "1.2.3 & calc" };
    Equal("", BundleService.InstallCommandFor("winget", "Example.Tool", malicious),
        "accepted malicious version");
    Equal(0, PackageOperations.BuildCalls, "unsafe options reached command builder");

    var bundle = new SerializableBundle
    {
        packages = new()
        {
            new() { ManagerName = "winget", Id = "Example.Tool", Name = "Unsafe", InstallationOptions = malicious },
        },
    };
    var script = BundleService.GenerateInstallScript(bundle);
    AssertNotContains(script, "1.2.3 & calc");
    var warnings = string.Join("\n", BundleService.Inspect(bundle).Warnings.Select(w => w.En));
    AssertContains(warnings, "unsafe structured install options");
}

static void SecurityWarnings()
{
    var report = BundleService.Inspect(new SerializableBundle
    {
        packages = new()
        {
            new()
            {
                ManagerName = "winget", Id = "Example.Tool", Name = "Example",
                InstallationOptions = new InstallOptions
                {
                    CustomArgsInstall = "--unsafe-extra", PostInstallCommand = "Write-Host done",
                    KillBeforeOperation = new() { "example" },
                },
            },
            new() { ManagerName = "winget", Id = "bad & calc", Name = "Invalid" },
        },
    });
    Assert(report.HasWarnings, "expected security warnings");
    var text = string.Join("\n", report.Warnings.Select(w => w.En));
    AssertContains(text, nameof(InstallOptions.CustomArgsInstall));
    AssertContains(text, nameof(InstallOptions.PostInstallCommand));
    AssertContains(text, "terminate processes");
    AssertContains(text, "invalid manager or package ID");
    AssertNotContains(text, "--unsafe-extra");
    AssertNotContains(text, "Write-Host done");
}

static void HasOptions()
{
    bool Has(InstallOptions o) => BundleService.HasOptions(new SerializablePackage { InstallationOptions = o });
    Assert(!Has(new()), "default options reported as non-trivial");
    Assert(Has(new() { AutoUpdate = true }), "AutoUpdate missing");
    Assert(Has(new() { AbortOnPreInstallFail = true }), "abort flag missing");
    Assert(Has(new() { CustomArgsUpdate = "--force" }), "custom args missing");
    Assert(Has(new() { ForceKill = true }), "ForceKill missing");
}

static async Task CoordinatorRejectsUnsafeOptions()
{
    PackageOperations.Reset();
    var snapshot = await PackageOperationCoordinator.RunAsync(
        Item("winget", UniqueId("unsafe")), PackageOperations.Op.Install,
        new InstallOptions { Version = "1.2.3 & calc" });
    Equal(PackageOperationStatus.Failed, snapshot.Status, "unsafe option status");
    Equal("invalid-version", snapshot.Result?.Code, "unsafe option code");
    Equal(0, PackageOperations.RunCalls, "unsafe option reached runner");
    PackageOperationCoordinator.ClearHistory();
}

static Task MinorUpdatePolicy()
{
    var options = new InstallOptions { SkipMinorUpdates = true };
    Assert(PackageOperationCoordinator.IsMinorUpdateSuppressed(new PackageItem
    {
        ManagerKey = "winget", Id = "Example.Tool", Version = "1.2.0", AvailableVersion = "1.2.1",
    }, options), "patch update was not suppressed");
    Assert(!PackageOperationCoordinator.IsMinorUpdateSuppressed(new PackageItem
    {
        ManagerKey = "winget", Id = "Example.Tool", Version = "1.2.0", AvailableVersion = "1.3.0",
    }, options), "minor-version update was incorrectly suppressed");
    Assert(!PackageOperationCoordinator.IsMinorUpdateSuppressed(new PackageItem
    {
        ManagerKey = "winget", Id = "Example.Tool", Version = "1.2", AvailableVersion = "1.2.0",
    }, options), "equivalent normalized version was incorrectly suppressed");
    Assert(PackageOperationCoordinator.IsMinorUpdateSuppressed(new PackageItem
    {
        ManagerKey = "winget", Id = "Example.Tool", Version = "1-2-3", AvailableVersion = "1-2-4",
    }, options), "hyphenated patch update was not suppressed");
    return Task.CompletedTask;
}

static async Task OptionSensitiveDeduplication()
{
    PackageOperations.Reset();
    PackageManagerSettings.ParallelOperationCount = 1;
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    PackageOperations.Runner = async (_, _, _, _, _, ct) =>
    {
        await release.Task.WaitAsync(ct);
        return WinForge.Models.TweakResult.Ok("ok", "成功");
    };

    var item = Item("winget", UniqueId("dedupe"));
    var first = PackageOperationCoordinator.Enqueue(item, PackageOperations.Op.Install,
        new InstallOptions { Version = "1.0.0" });
    await WaitUntil(() => PackageOperations.RunCalls == 1);
    var duplicate = PackageOperationCoordinator.Enqueue(item, PackageOperations.Op.Install,
        new InstallOptions { Version = "1.0.0" });
    var distinct = PackageOperationCoordinator.Enqueue(item, PackageOperations.Op.Install,
        new InstallOptions { Version = "2.0.0" });
    Equal(first.Id, duplicate.Id, "equivalent operation was not deduplicated");
    Assert(distinct.Id != first.Id, "different options were silently deduplicated");

    release.TrySetResult(true);
    await Task.WhenAll(first.Completion, duplicate.Completion, distinct.Completion).WaitAsync(TimeSpan.FromSeconds(5));
    Equal(2, PackageOperations.RunCalls, "distinct operation count");
    PackageOperationCoordinator.ClearHistory();
}

static async Task SourceSensitiveDeduplication()
{
    PackageOperations.Reset();
    PackageManagerSettings.ParallelOperationCount = 1;
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    PackageOperations.Runner = async (_, _, _, _, _, ct) =>
    {
        await release.Task.WaitAsync(ct);
        return WinForge.Models.TweakResult.Ok("ok", "成功");
    };

    var id = UniqueId("source-dedupe");
    var winget = Item("winget", id); winget.Source = "winget";
    var store = Item("winget", id); store.Source = "msstore";
    var first = PackageOperationCoordinator.Enqueue(winget, PackageOperations.Op.Install);
    await WaitUntil(() => PackageOperations.RunCalls == 1);
    var sameSourceDifferentCase = PackageOperationCoordinator.Enqueue(
        new PackageItem { ManagerKey = "winget", Id = id, Name = id, Source = "WINGET" },
        PackageOperations.Op.Install);
    var distinctSource = PackageOperationCoordinator.Enqueue(store, PackageOperations.Op.Install);
    Equal(first.Id, sameSourceDifferentCase.Id, "equivalent source operation was not deduplicated");
    Assert(distinctSource.Id != first.Id, "same ID from a distinct source was silently deduplicated");

    release.TrySetResult(true);
    await Task.WhenAll(first.Completion, sameSourceDifferentCase.Completion, distinctSource.Completion)
        .WaitAsync(TimeSpan.FromSeconds(5));
    Equal(2, PackageOperations.RunCalls, "distinct source operation count");
    Assert(PackageOperations.RunSources.Contains("winget"), "winget source was not forwarded to runner");
    Assert(PackageOperations.RunSources.Contains("msstore"), "msstore source was not forwarded to runner");
    PackageOperationCoordinator.ClearHistory();
}

static async Task KnownSourceUninstallCompatibility()
{
    PackageOperations.Reset();
    var item = Item("dotnet", UniqueId("known-source-uninstall"));
    item.Source = "nuget.org";
    var snapshot = await PackageOperationCoordinator.RunAsync(item, PackageOperations.Op.Uninstall);
    Equal(PackageOperationStatus.Succeeded, snapshot.Status, "known source uninstall status");
    Equal("nuget.org", snapshot.Source, "queue snapshot lost source metadata");
    Equal("nuget.org", PackageOperations.LastRunSource, "known source was dropped before runner");
    PackageOperationCoordinator.ClearHistory();
}

static async Task QueuedCancellation()
{
    PackageOperations.Reset();
    PackageManagerSettings.ParallelOperationCount = 1;
    var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    PackageOperations.Runner = async (_, _, _, _, _, ct) =>
    {
        await release.Task.WaitAsync(ct);
        return WinForge.Models.TweakResult.Ok("ok", "成功");
    };

    var first = PackageOperationCoordinator.Enqueue(Item("winget", UniqueId("block")), PackageOperations.Op.Install);
    await WaitUntil(() => PackageOperations.RunCalls == 1);
    using var cts = new CancellationTokenSource();
    var queued = PackageOperationCoordinator.Enqueue(Item("winget", UniqueId("cancel")),
        PackageOperations.Op.Install, ct: cts.Token);
    cts.Cancel();
    var cancelled = await queued.Completion.WaitAsync(TimeSpan.FromSeconds(3));
    Equal(PackageOperationStatus.Cancelled, cancelled.Status, "queued cancellation status");
    release.TrySetResult(true);
    await first.Completion.WaitAsync(TimeSpan.FromSeconds(3));
    Equal(1, PackageOperations.RunCalls, "cancelled queued operation reached runner");
    PackageOperationCoordinator.ClearHistory();
}

static async Task ConflictingOperationsSerialize()
{
    PackageOperations.Reset();
    PackageManagerSettings.ParallelOperationCount = 2;
    var releaseFirst = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    var secondStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    PackageOperations.Runner = async (_, _, _, _, _, ct) =>
    {
        int call = PackageOperations.RunCalls;
        if (call == 1) await releaseFirst.Task.WaitAsync(ct);
        else secondStarted.TrySetResult(true);
        return WinForge.Models.TweakResult.Ok("ok", "成功");
    };

    var item = Item("winget", UniqueId("serialize"));
    var install = PackageOperationCoordinator.Enqueue(item, PackageOperations.Op.Install);
    await WaitUntil(() => PackageOperations.RunCalls == 1);
    var uninstall = PackageOperationCoordinator.Enqueue(item, PackageOperations.Op.Uninstall);
    await Task.Delay(150);
    Assert(!secondStarted.Task.IsCompleted, "conflicting operation started concurrently");
    releaseFirst.TrySetResult(true);
    await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
    await Task.WhenAll(install.Completion, uninstall.Completion).WaitAsync(TimeSpan.FromSeconds(5));
    PackageOperationCoordinator.ClearHistory();
}

static async Task CancelAllIsAtomic()
{
    PackageOperations.Reset();
    PackageManagerSettings.ParallelOperationCount = 1;
    var runningStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    PackageOperations.Runner = async (_, _, _, _, _, ct) =>
    {
        runningStarted.TrySetResult(true);
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        return WinForge.Models.TweakResult.Ok("ok", "成功");
    };

    var running = PackageOperationCoordinator.Enqueue(Item("winget", UniqueId("cancelall-running")), PackageOperations.Op.Install);
    await runningStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
    var queuedOne = PackageOperationCoordinator.Enqueue(Item("winget", UniqueId("cancelall-one")), PackageOperations.Op.Install);
    var queuedTwo = PackageOperationCoordinator.Enqueue(Item("winget", UniqueId("cancelall-two")), PackageOperations.Op.Install);
    PackageOperationCoordinator.CancelAll();

    var snapshots = await Task.WhenAll(running.Completion, queuedOne.Completion, queuedTwo.Completion)
        .WaitAsync(TimeSpan.FromSeconds(5));
    Assert(snapshots.All(s => s.Status == PackageOperationStatus.Cancelled), "cancel all left a live operation");
    Equal(1, PackageOperations.RunCalls, "cancel all started queued work");
    PackageOperationCoordinator.ClearHistory();
}

static async Task OutputIsBoundedAndRedacted()
{
    PackageOperations.Reset();
    PackageManagerSettings.ParallelOperationCount = 1;
    PackageOperations.Runner = (_, _, _, _, progress, _) =>
    {
        progress?.Report("--token=super-secret");
        var output = new string('x', 20_000) + " password=hunter2";
        return Task.FromResult(WinForge.Models.TweakResult.Fail("token=super-secret", "token=super-secret", output));
    };
    var snapshot = await PackageOperationCoordinator.RunAsync(
        Item("winget", UniqueId("output")), PackageOperations.Op.Install);
    Assert(snapshot.OutputTail.Length <= 12_000, "output tail was not bounded");
    Assert(snapshot.Result?.Output?.Length <= 12_000, "result output was not bounded");
    AssertNotContains(snapshot.OutputTail, "hunter2");
    AssertNotContains(snapshot.Result?.Message?.En ?? "", "super-secret");
    PackageOperationCoordinator.ClearHistory();
}

static async Task CleanupTimeoutRemainsFailure()
{
    PackageOperations.Reset();
    var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    PackageOperations.Runner = async (_, _, _, _, _, ct) =>
    {
        started.TrySetResult(true);
        while (!ct.IsCancellationRequested) await Task.Delay(10);
        return WinForge.Models.TweakResult.Fail("child may still be running", "子程序可能仲運行緊")
            with { Code = ShellRunner.ProcessCleanupTimeoutCode };
    };
    using var cts = new CancellationTokenSource();
    var ticket = PackageOperationCoordinator.Enqueue(
        Item("winget", UniqueId("cleanup-timeout")), PackageOperations.Op.Install, ct: cts.Token);
    await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
    cts.Cancel();
    var snapshot = await ticket.Completion.WaitAsync(TimeSpan.FromSeconds(3));
    Equal(PackageOperationStatus.Failed, snapshot.Status, "cleanup timeout status");
    PackageOperationCoordinator.ClearHistory();
}

static PackageItem Item(string manager, string id) => new()
{
    ManagerKey = manager, Id = id, Name = id,
};

static string UniqueId(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

static async Task WaitUntil(Func<bool> condition)
{
    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (!condition())
    {
        if (DateTime.UtcNow >= deadline) throw new TimeoutException("condition was not reached");
        await Task.Delay(10);
    }
}

static SerializableBundle FullBundle() => new()
{
    packages = new()
    {
        new()
        {
            Id = "Example.Tool", Name = "Example", Version = "1.2.3", Source = "", ManagerName = "winget",
            InstallationOptions = new InstallOptions
            {
                CustomArgsInstall = "--install value", CustomArgsUpdate = "--update", CustomArgsUninstall = "--remove",
                RunAsAdministrator = true, Interactive = true, SkipHashCheck = true, PreRelease = true,
                RemoveDataOnUninstall = true, UninstallPreviousOnUpdate = true, SkipMinorUpdates = true, AutoUpdate = true,
                Scope = "machine", Architecture = "arm64", Version = "1.2.3-preview.4",
                CustomInstallLocation = @"C:\Program Files\Example",
                PreInstallCommand = "Write-Host before\nWrite-Host second", PostInstallCommand = "Write-Host\tafter",
                PreUpdateCommand = "pre-update", PostUpdateCommand = "post-update",
                PreUninstallCommand = "pre-uninstall", PostUninstallCommand = "post-uninstall",
                AbortOnPreInstallFail = true, AbortOnPreUpdateFail = true, AbortOnPreUninstallFail = true,
                KillBeforeOperation = new() { "example", "helper.exe" }, ForceKill = true,
            },
        },
    },
};

static InstallOptions Options(SerializableBundle bundle)
{
    Equal(1, bundle.packages.Count, "legacy package count");
    return bundle.packages[0].InstallationOptions ?? throw new InvalidOperationException("options lost");
}

static void AssertLegacy(InstallOptions options, string scope, string install, string update, string uninstall)
{
    Assert(options.Interactive, "interactive alias not read");
    Equal(scope, options.Scope, "scope alias");
    Equal(install, options.CustomArgsInstall, "install args alias");
    Equal(update, options.CustomArgsUpdate, "update args alias");
    Equal(uninstall, options.CustomArgsUninstall, "uninstall args alias");
}

static void SameOptions(InstallOptions expected, InstallOptions? actual)
{
    Assert(actual is not null, "options lost");
    var settings = new JsonSerializerOptions { WriteIndented = false };
    Equal(JsonSerializer.Serialize(expected, settings), JsonSerializer.Serialize(actual, settings), "options changed");
}

static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'");
}

static void AssertContains(string text, string value)
    => Assert(text.Contains(value, StringComparison.Ordinal), $"missing {value}");

static void AssertNotContains(string text, string value)
    => Assert(!text.Contains(value, StringComparison.Ordinal), $"unexpected {value}");
