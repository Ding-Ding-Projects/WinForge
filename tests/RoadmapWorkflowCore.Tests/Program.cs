using WinForge.Services;

var tests = new (string Name, Action Body)[]
{
    ("port listener JSON is parsed and deduplicated", ListenerParse),
    ("port listener parser filters a different port", ListenerPortFilter),
    ("port validation rejects zero", PortRejectsZero),
    ("terminate plan preserves PID argument boundaries", TerminatePlan),
    ("reviewed listener identity must still match", ListenerIdentityMatch),
    ("changed listener identity is rejected", ListenerIdentityChanged),
    ("Node semantic and channel versions are accepted", NodeVersionAccepted),
    ("Node command injection is rejected", NodeVersionRejected),
    ("fnm list and install plans are exact", FnmPlans),
    ("fnm shell keeps one bounded PowerShell command", FnmShellPlan),
    ("Volta shell uses argument-vector node selection", VoltaShellPlan),
    ("nvm per-shell activation is refused", NvmShellRejected),
    ("Corepack enable plan is exact", CorepackEnable),
    ("Corepack pnpm prepare plan is exact", CorepackPnpm),
    ("Corepack manager and channel injection is rejected", CorepackRejected),
    ("Defender folder produces a quoted add plan", DefenderAdd),
    ("Defender drive-root exclusion is rejected", DefenderRootRejected),
    ("TCP dynamic range plan is exact", TcpPlan),
    ("TCP range overflow is rejected", TcpOverflowRejected),
    ("TIME_WAIT outside 30-300 is rejected", TimedWaitRejected),
    ("all four developer cache cleanup plans are exact", CachePlans),
    ("byte formatting is deterministic", ByteFormatting),
    ("archive masks parse semicolons and newlines", MaskParsing),
    ("archive mask parent traversal is rejected", MaskTraversalRejected),
    ("archive absolute masks are rejected", MaskAbsoluteRejected),
    ("archive create plan emits recursive include/exclude masks", ArchiveFilterPlan),
    ("archive create plan preserves all NTFS time switches", ArchiveTimePlan),
    ("archive password stays in one argument", ArchivePasswordBoundary),
    ("archive integrity test receives the password as one argument", ArchiveIntegrityPassword),
    ("archive integrity test targets the first split volume", ArchiveIntegrityVolume),
    ("archive volume input is bounded", ArchiveVolumeRejected),
    ("NTFS timestamp mode rejects non-7z format", ArchiveFormatRejected),
    ("archive delete accepts arbitrary reviewed masks", ArchiveDeletePlan),
    ("archive delete requires at least one mask", ArchiveDeleteEmptyRejected),
    ("move-after-test rejects an archive inside source", ArchiveMoveContainment),
    ("move-after-test accepts separate source and output", ArchiveMoveSafe),
    ("Home Assistant valid response is parsed exactly", HaValidResponse),
    ("Home Assistant misleading valid text is rejected", HaMisleadingResponse),
    ("Home Assistant gate accepts same endpoint and token", HaGateSuccess),
    ("Home Assistant gate rejects endpoint changes", HaGateEndpointChange),
    ("Home Assistant gate rejects token changes", HaGateTokenChange),
    ("Home Assistant gate expires", HaGateExpiry),
    ("failed Home Assistant checks clear the gate", HaGateFailureClears),
    ("Home Assistant restart consumes validation", HaGateConsume),
};

var passed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        passed++;
        Console.WriteLine($"PASS {passed:00}/{tests.Length:00}  {test.Name}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL {passed + 1:00}/{tests.Length:00}  {test.Name}: {ex.Message}");
        return 1;
    }
}

Console.WriteLine($"Roadmap workflow contract passed: {passed}/{tests.Length}.");
return 0;

static void ListenerParse()
{
    const string json = "[{\"ProcessId\":42,\"ProcessName\":\"dotnet\",\"LocalAddress\":\"127.0.0.1\",\"LocalPort\":8080},{\"ProcessId\":42,\"ProcessName\":\"dotnet\",\"LocalAddress\":\"::1\",\"LocalPort\":8080}]";
    var listeners = DeveloperWorkflowCore.ParseListeners(json, 8080);
    Equal(1, listeners.Count, "deduplicated listener count");
    Equal(42, listeners[0].ProcessId, "PID");
}

static void ListenerPortFilter()
    => Equal(0, DeveloperWorkflowCore.ParseListeners("{\"ProcessId\":42,\"ProcessName\":\"x\",\"LocalAddress\":\"*\",\"LocalPort\":8081}", 8080).Count, "filtered count");

static void PortRejectsZero() => Throws<ArgumentOutOfRangeException>(() => DeveloperWorkflowCore.ValidatePort(0));

static void TerminatePlan()
{
    var plan = DeveloperWorkflowCore.BuildTerminatePlan(321);
    Equal("taskkill.exe", plan.Executable, "executable");
    Sequence(new[] { "/PID", "321", "/T", "/F" }, plan.Arguments, "terminate argv");
}

static void ListenerIdentityMatch()
{
    var reviewed = new[] { new PortListener(42, "dotnet", "127.0.0.1", 8080) };
    var current = new[] { new PortListener(42, "DOTNET", "::1", 8080) };
    True(DeveloperWorkflowCore.ReviewedListenersStillMatch(reviewed, current), "same reviewed process");
}

static void ListenerIdentityChanged()
{
    var reviewed = new[] { new PortListener(42, "dotnet", "127.0.0.1", 8080) };
    True(!DeveloperWorkflowCore.ReviewedListenersStillMatch(reviewed,
        new[] { new PortListener(43, "node", "127.0.0.1", 8080) }), "changed process");
    True(!DeveloperWorkflowCore.ReviewedListenersStillMatch(reviewed,
        new[] { reviewed[0], new PortListener(44, "node", "::1", 8080) }), "added process");
}

static void NodeVersionAccepted()
{
    Equal("20.12.2", DeveloperWorkflowCore.ValidateNodeVersion("20.12.2"), "semver");
    Equal("lts-iron", DeveloperWorkflowCore.ValidateNodeVersion("lts-iron"), "LTS channel");
}

static void NodeVersionRejected()
{
    Throws<ArgumentException>(() => DeveloperWorkflowCore.ValidateNodeVersion("20; Stop-Process 1"));
    Throws<ArgumentException>(() => DeveloperWorkflowCore.ValidateNodeVersion("$(whoami)"));
}

static void FnmPlans()
{
    Sequence(new[] { "list" }, DeveloperWorkflowCore.BuildNodeListPlan(NodeVersionManager.Fnm, "fnm.exe").Arguments, "fnm list");
    Sequence(new[] { "install", "20.12.2" }, DeveloperWorkflowCore.BuildNodeInstallPlan(NodeVersionManager.Fnm, "fnm.exe", "20.12.2").Arguments, "fnm install");
}

static void FnmShellPlan()
{
    var plan = DeveloperWorkflowCore.BuildNodeShellPlan(NodeVersionManager.Fnm, @"C:\Tools\fnm.exe", "lts");
    Equal("powershell.exe", plan.Executable, "shell executable");
    Equal(4, plan.Arguments.Count, "shell argument count");
    ContainsText(plan.Arguments[3], "env --use-on-cd", "fnm env init");
    ContainsText(plan.Arguments[3], "use 'lts'", "fnm version literal");
}

static void VoltaShellPlan()
{
    var plan = DeveloperWorkflowCore.BuildNodeShellPlan(NodeVersionManager.Volta, "volta.exe", "22.1.0");
    Sequence(new[] { "run", "--node", "22.1.0", "powershell.exe", "-NoExit", "-NoProfile" }, plan.Arguments, "Volta argv");
}

static void NvmShellRejected()
    => Throws<InvalidOperationException>(() => DeveloperWorkflowCore.BuildNodeShellPlan(NodeVersionManager.Nvm, "nvm.exe", "20"));

static void CorepackEnable()
    => Sequence(new[] { "enable" }, DeveloperWorkflowCore.BuildCorepackEnablePlan().Arguments, "Corepack enable");

static void CorepackPnpm()
    => Sequence(new[] { "prepare", "pnpm@latest", "--activate" }, DeveloperWorkflowCore.BuildCorepackPreparePlan("pnpm", "latest").Arguments, "Corepack prepare");

static void CorepackRejected()
{
    Throws<ArgumentException>(() => DeveloperWorkflowCore.BuildCorepackPreparePlan("npm", "latest"));
    Throws<ArgumentException>(() => DeveloperWorkflowCore.BuildCorepackPreparePlan("pnpm", "latest;calc"));
}

static void DefenderAdd()
{
    WithTemp(root =>
    {
        var quoted = Path.Combine(root, "dev's repo");
        Directory.CreateDirectory(quoted);
        var script = DeveloperWorkflowCore.BuildDefenderMutationScript(quoted, add: true);
        ContainsText(script, "Add-MpPreference", "Defender verb");
        ContainsText(script, "dev''s repo", "PowerShell quote escaping");
    });
}

static void DefenderRootRejected()
    => Throws<ArgumentException>(() => DeveloperWorkflowCore.ValidateDeveloperFolder(Path.GetPathRoot(Path.GetTempPath())!, requireExisting: false));

static void TcpPlan()
{
    var plan = DeveloperWorkflowCore.BuildDynamicPortPlan(10000, 55000);
    Sequence(new[] { "int", "ipv4", "set", "dynamicport", "tcp", "start=10000", "num=55000" }, plan.Arguments, "netsh argv");
    True(plan.RequiresElevation, "TCP plan elevation");
}

static void TcpOverflowRejected()
    => Throws<ArgumentOutOfRangeException>(() => DeveloperWorkflowCore.ValidateTcpTuning(65000, 1000, 60));

static void TimedWaitRejected()
{
    Throws<ArgumentOutOfRangeException>(() => DeveloperWorkflowCore.ValidateTcpTuning(10000, 55000, 29));
    Throws<ArgumentOutOfRangeException>(() => DeveloperWorkflowCore.ValidateTcpTuning(10000, 55000, 301));
}

static void CachePlans()
{
    Sequence(new[] { "cache", "clean", "--force" }, DeveloperWorkflowCore.BuildCacheCleanPlan(DeveloperCacheKind.Npm).Arguments, "npm");
    Sequence(new[] { "store", "prune" }, DeveloperWorkflowCore.BuildCacheCleanPlan(DeveloperCacheKind.Pnpm).Arguments, "pnpm");
    Sequence(new[] { "cache", "purge" }, DeveloperWorkflowCore.BuildCacheCleanPlan(DeveloperCacheKind.Pip).Arguments, "pip");
    Sequence(new[] { "builder", "prune", "-f" }, DeveloperWorkflowCore.BuildCacheCleanPlan(DeveloperCacheKind.Docker).Arguments, "Docker");
}

static void ByteFormatting() => Equal("1.5 KiB", DeveloperWorkflowCore.FormatBytes(1536), "formatted bytes");

static void MaskParsing()
    => Sequence(new[] { "*.jpg", "src\\*.cs", "node_modules\\*" }, ArchiveWorkflowCore.ParseMasks("*.jpg; src\\*.cs\nnode_modules\\*"), "masks");

static void MaskTraversalRejected() => Throws<ArgumentException>(() => ArchiveWorkflowCore.ValidateMask("..\\secret"));
static void MaskAbsoluteRejected() => Throws<ArgumentException>(() => ArchiveWorkflowCore.ValidateMask(@"C:\secret\*"));

static void ArchiveFilterPlan()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var plan = ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(root, "out.7z"), source,
            Options(include: new[] { "*.jpg" }, exclude: new[] { "*.tmp", "node_modules\\*" }));
        Contains(plan.CreateArguments, "-ir!*.jpg", "include argv");
        Contains(plan.CreateArguments, "-xr!*.tmp", "exclude argv");
        Contains(plan.CreateArguments, "-xr!node_modules\\*", "folder exclude argv");
    });
}

static void ArchiveTimePlan()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "x");
        var plan = ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(root, "out.7z"), source, Options(preserve: true));
        foreach (var item in new[] { "-mtc=on", "-mta=on", "-mtm=on", "-ssp" }) Contains(plan.CreateArguments, item, item);
    });
}

static void ArchivePasswordBoundary()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "x");
        var plan = ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(root, "out.7z"), source, Options(password: "space & punctuation!"));
        Contains(plan.CreateArguments, "-pspace & punctuation!", "password argv");
        Equal(1, plan.CreateArguments.Count(item => item.StartsWith("-p", StringComparison.Ordinal)), "password argument count");
    });
}

static void ArchiveIntegrityPassword()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "x");
        var plan = ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(root, "out.7z"), source,
            Options(password: "space & punctuation!", move: true));
        Sequence(new[] { "t", Path.Combine(root, "out.7z"), "-pspace & punctuation!" }, plan.IntegrityArguments, "password integrity argv");
    });
}

static void ArchiveIntegrityVolume()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "x");
        var plan = ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(root, "out.7z"), source,
            Options(volume: "100m", move: true));
        Sequence(new[] { "t", Path.Combine(root, "out.7z.001") }, plan.IntegrityArguments, "split integrity argv");
    });
}

static void ArchiveVolumeRejected()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "x");
        Throws<ArgumentException>(() => ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(root, "out.7z"), source, Options(volume: "100m -sdel")));
    });
}

static void ArchiveFormatRejected()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source.txt");
        File.WriteAllText(source, "x");
        Throws<ArgumentException>(() => ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(root, "out.zip"), source, Options(format: "zip", preserve: true)));
    });
}

static void ArchiveDeletePlan()
    => Sequence(new[] { "d", Path.GetFullPath("sample.7z"), "logs\\*.log", "temp\\*", "-r" },
        ArchiveWorkflowCore.BuildDeleteArguments("sample.7z", new[] { "logs\\*.log", "temp\\*" }, recursive: true), "delete argv");

static void ArchiveDeleteEmptyRejected()
    => Throws<ArgumentException>(() => ArchiveWorkflowCore.BuildDeleteArguments("sample.7z", Array.Empty<string>(), recursive: true));

static void ArchiveMoveContainment()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        Throws<ArgumentException>(() => ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(source, "out.7z"), source, Options(move: true)));
    });
}

static void ArchiveMoveSafe()
{
    WithTemp(root =>
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var plan = ArchiveWorkflowCore.BuildCreatePlan(Path.Combine(root, "out.7z"), source, Options(move: true));
        True(plan.MoveSourceAfterIntegrityTest, "move gate");
        Sequence(new[] { "t", Path.Combine(root, "out.7z") }, plan.IntegrityArguments, "integrity argv");
        True(!plan.CreateArguments.Contains("-sdel"), "-sdel must not bypass integrity gate");
    });
}

static void HaValidResponse() => True(HomeAssistantRestartGate.IsValidResponse("{\"result\":\"valid\",\"errors\":null}"), "valid response");
static void HaMisleadingResponse() => True(!HomeAssistantRestartGate.IsValidResponse("{\"result\":\"invalid\",\"errors\":\"contains valid text\"}"), "misleading response");

static void HaGateSuccess()
{
    var gate = NewGate(out var now);
    True(gate.RecordCheck("http://ha.local:8123/", "token", true, "{\"result\":\"valid\"}", now), "record");
    True(gate.CanRestart("http://ha.local:8123", "token", now.AddSeconds(30)), "same credentials");
}

static void HaGateEndpointChange()
{
    var gate = NewGate(out var now);
    gate.RecordCheck("http://ha-a:8123", "token", true, "{\"result\":\"valid\"}", now);
    True(!gate.CanRestart("http://ha-b:8123", "token", now.AddSeconds(1)), "endpoint change");
}

static void HaGateTokenChange()
{
    var gate = NewGate(out var now);
    gate.RecordCheck("http://ha:8123", "token-a", true, "{\"result\":\"valid\"}", now);
    True(!gate.CanRestart("http://ha:8123", "token-b", now.AddSeconds(1)), "token change");
}

static void HaGateExpiry()
{
    var gate = NewGate(out var now);
    gate.RecordCheck("http://ha:8123", "token", true, "{\"result\":\"valid\"}", now);
    True(!gate.CanRestart("http://ha:8123", "token", now.AddMinutes(3)), "expired gate");
}

static void HaGateFailureClears()
{
    var gate = NewGate(out var now);
    gate.RecordCheck("http://ha:8123", "token", true, "{\"result\":\"valid\"}", now);
    gate.RecordCheck("http://ha:8123", "token", false, "{\"result\":\"valid\"}", now.AddSeconds(1));
    True(!gate.CanRestart("http://ha:8123", "token", now.AddSeconds(2)), "failed check cleared gate");
}

static void HaGateConsume()
{
    var gate = NewGate(out var now);
    gate.RecordCheck("http://ha:8123", "token", true, "{\"result\":\"valid\"}", now);
    gate.Consume();
    True(!gate.CanRestart("http://ha:8123", "token", now.AddSeconds(1)), "consumed gate");
}

static HomeAssistantRestartGate NewGate(out DateTimeOffset now)
{
    now = new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    return new HomeAssistantRestartGate(TimeSpan.FromMinutes(2));
}

static ArchiveCreateOptions Options(
    string format = "7z", string? password = null, string? volume = null,
    IReadOnlyList<string>? include = null, IReadOnlyList<string>? exclude = null,
    bool preserve = false, bool move = false)
    => new(format, 5, password, !string.IsNullOrEmpty(password), false, true, false, volume,
        include ?? Array.Empty<string>(), exclude ?? Array.Empty<string>(), preserve, move);

static void WithTemp(Action<string> body)
{
    var root = Path.Combine(Path.GetTempPath(), "winforge-roadmap-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try { body(root); }
    finally { try { Directory.Delete(root, recursive: true); } catch { } }
}

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void Sequence(IEnumerable<string> expected, IEnumerable<string> actual, string label)
{
    var left = expected.ToArray();
    var right = actual.ToArray();
    if (!left.SequenceEqual(right, StringComparer.Ordinal))
        throw new InvalidOperationException($"{label}: expected [{string.Join(", ", left)}], got [{string.Join(", ", right)}].");
}

static void Contains(IEnumerable<string> values, string expected, string label)
{
    if (!values.Contains(expected, StringComparer.Ordinal))
        throw new InvalidOperationException($"{label}: '{expected}' not found.");
}

static void ContainsText(string value, string expected, string label)
{
    if (!value.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"{label}: '{expected}' not found in '{value}'.");
}

static void True(bool value, string label)
{
    if (!value) throw new InvalidOperationException($"{label}: expected true.");
}

static void Throws<T>(Action body) where T : Exception
{
    try { body(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
