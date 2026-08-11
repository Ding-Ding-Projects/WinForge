using WinForge.Services;

var failures = new List<string>();
int passed = 0;

Run("canonical repository coordinates", CanonicalRepositoryCoordinates);
Run("managed release tags map to Windows versions", ReleaseVersionMapping);
Run("invalid or noncanonical tags fail closed", InvalidReleaseTags);
Run("newer-version comparison fails closed", NewerVersionComparison);
Run("portable asset name follows the version", PortableAssetName);
Run("GitHub SHA-256 digests normalize", DigestNormalization);
Run("malformed digests are rejected", DigestRejection);
Run("stable release resolves exact assets and digests", StableReleaseResolution);
Run("draft and prerelease records are ignored", NonStableReleaseRejection);
Run("extra or missing assets are incompatible", AssetSetRejection);
Run("wrong repository download URL is rejected", RepositoryUrlRejection);
Run("query, fragment, port, and userinfo are rejected", UrlDecorationRejection);
Run("install layout accepts direct expected children", InstallLayoutAcceptance);
Run("install layout rejects path escape and drive root", InstallLayoutRejection);
Run("staged installer stays in the update directory", StagedInstallerBoundary);
Run("persistent log stays in the update directory", UpdateLogBoundary);
Run("portable footprint includes app, launcher, updater, and manifest", PortableFootprint);
Run("portable footprint rejects traversal and omissions", PortableFootprintRejection);
Run("workflow publishes immutable exact-contract releases", WorkflowContract);
Run("Squirrel metadata points to the canonical repository", InstallerMetadataContract);
Run("app, updater, and launcher share the pure contract", RuntimeWiringContract);
Run("Squirrel update remains user-controlled", SquirrelUpdateActionContract);
Run("universal settings and offline changelog are wired", UniversalExperienceContract);
Run("pinned tabs persist in the session schema", PinnedTabContract);
Run("managed tree has no stale repository links", NoStaleManagedRepositoryLinks);
Run("WinForge-Native links retain their independent owner", NativeRepositoryLinksPreserved);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} managed-release contract tests (pure/static; no host mutation)");
    return 0;
}

foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} managed-release contract tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {name}: {ex.Message}");
    }
}

static void CanonicalRepositoryCoordinates()
{
    Equal("Ding-Ding-Projects/WinForge", ManagedReleaseContract.RepositorySlug, "repository slug");
    Equal("https://github.com/Ding-Ding-Projects/WinForge", ManagedReleaseContract.RepositoryUrl, "repository URL");
    Equal("https://api.github.com/repos/Ding-Ding-Projects/WinForge/releases/latest",
        ManagedReleaseContract.LatestReleaseApi, "latest-release API");
}

static void ReleaseVersionMapping()
{
    True(ManagedReleaseContract.TryParseReleaseVersion("v1.1.323", out Version? version), "valid release tag");
    Equal(new Version(1, 1, 323), version, "parsed version");
    True(ManagedReleaseContract.TryParseReleaseVersion("1.1.65535", out _), "maximum FileVersion build");
}

static void InvalidReleaseTags()
{
    foreach (string value in new[] { "1.0.1", "1.2.1", "1.1.0", "1.1.65536", "1.1.01", "1.1.2.3", "banana" })
        False(ManagedReleaseContract.TryParseReleaseVersion(value, out _), value);
}

static void NewerVersionComparison()
{
    True(ManagedReleaseContract.IsNewerRelease("v1.1.323", "1.1.322"), "newer stable release");
    False(ManagedReleaseContract.IsNewerRelease("v1.1.323", "1.1.323"), "same release");
    False(ManagedReleaseContract.IsNewerRelease("banana", "1.1.322"), "invalid latest tag");
    False(ManagedReleaseContract.IsNewerRelease("v1.1.323", "preview"), "invalid current version");
}

static void PortableAssetName()
    => Equal("WinForge-portable-x64-1.1.323.zip",
        ManagedReleaseContract.PortableAssetName("v1.1.323"), "portable name");

static void DigestNormalization()
{
    string lower = new('a', 64);
    Equal(new string('A', 64), ManagedReleaseContract.NormalizeSha256("sha256:" + lower), "normalized digest");
    True(ManagedReleaseContract.FixedTimeSha256Equals(lower, "sha256:" + lower), "fixed-time digest equality");
}

static void DigestRejection()
{
    Equal(string.Empty, ManagedReleaseContract.NormalizeSha256("sha256:1234"), "short digest");
    Equal(string.Empty, ManagedReleaseContract.NormalizeSha256("sha256:" + new string('z', 64)), "non-hex digest");
    False(ManagedReleaseContract.FixedTimeSha256Equals(new string('a', 64), new string('b', 64)), "different digest");
}

static void StableReleaseResolution()
{
    ManagedReleaseMetadata release = ValidRelease();
    True(ManagedReleaseContract.TryResolveRelease(release, out ManagedReleaseSelection? selected, out string reason), reason);
    NotNull(selected, "release selection");
    Equal("1.1.323", selected!.Version, "selected version");
    Equal(ManagedReleaseContract.InstallerAssetName, selected.Installer.Name, "installer name");
    Equal(ManagedReleaseContract.SquirrelReleasesAssetName, selected.Releases.Name, "RELEASES name");
    Equal(ManagedReleaseContract.SquirrelFullPackageName("1.1.323"), selected.FullPackage.Name, "full package name");
    Equal(ManagedReleaseContract.PortableAssetName("1.1.323"), selected.Portable.Name, "portable name");
}

static void NonStableReleaseRejection()
{
    ManagedReleaseMetadata stable = ValidRelease();
    False(ManagedReleaseContract.TryResolveRelease(stable with { Draft = true }, out _, out _), "draft");
    False(ManagedReleaseContract.TryResolveRelease(stable with { Prerelease = true }, out _, out _), "prerelease");
}

static void AssetSetRejection()
{
    ManagedReleaseMetadata stable = ValidRelease();
    False(ManagedReleaseContract.TryResolveRelease(stable with { Assets = stable.Assets.Take(1).ToArray() }, out _, out _),
        "missing portable");
    False(ManagedReleaseContract.TryResolveRelease(stable with
    {
        Assets = stable.Assets.Append(new ManagedReleaseAsset("notes.txt", "https://example.test/notes.txt", "sha256:" + new string('c', 64), 1)).ToArray()
    }, out _, out _), "unexpected asset");
}

static void RepositoryUrlRejection()
{
    ManagedReleaseMetadata stable = ValidRelease();
    var changed = stable.Assets.Select(asset => asset.Name == ManagedReleaseContract.InstallerAssetName
        ? asset with { BrowserDownloadUrl = asset.BrowserDownloadUrl.Replace("Ding-Ding-Projects", "codingmachineedge", StringComparison.Ordinal) }
        : asset).ToArray();
    False(ManagedReleaseContract.TryResolveRelease(stable with { Assets = changed }, out _, out _), "wrong owner");
}

static void UrlDecorationRejection()
{
    string baseUrl = "https://github.com/Ding-Ding-Projects/WinForge/releases/download/v1.1.323/Setup.exe";
    foreach (string value in new[]
             {
                 baseUrl + "?download=1",
                 baseUrl + "#asset",
                 baseUrl.Replace("github.com", "github.com:444", StringComparison.Ordinal),
                 baseUrl.Replace("https://", "https://user@", StringComparison.Ordinal)
             })
        False(ManagedReleaseContract.IsCanonicalReleaseDownload(value, "1.1.323", "Setup.exe"), value);
}

static void InstallLayoutAcceptance()
{
    string root = Path.Combine(Path.GetTempPath(), "WinForge Contract", "app");
    ManagedInstallLayout layout = ManagedReleaseContract.ValidateInstallLayout(
        root,
        Path.Combine(root, ManagedReleaseContract.LauncherFileName),
        Path.Combine(root, ManagedReleaseContract.ExecutableFileName));
    Equal(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar), layout.InstallDirectory, "install root");
}

static void InstallLayoutRejection()
{
    string root = Path.Combine(Path.GetTempPath(), "WinForge Contract", "app");
    Rejects<InvalidDataException>(() => ManagedReleaseContract.ValidateInstallLayout(
        root,
        Path.Combine(root, "sub", ManagedReleaseContract.LauncherFileName),
        Path.Combine(root, ManagedReleaseContract.ExecutableFileName)));
    string driveRoot = Path.GetPathRoot(Path.GetFullPath(root))!;
    Rejects<InvalidDataException>(() => ManagedReleaseContract.ValidateInstallLayout(
        driveRoot,
        Path.Combine(driveRoot, ManagedReleaseContract.LauncherFileName),
        Path.Combine(driveRoot, ManagedReleaseContract.ExecutableFileName)));
}

static void StagedInstallerBoundary()
{
    string root = Path.Combine(Path.GetTempPath(), "WinForge Contract", "updates");
    string installer = Path.Combine(root, "Setup-1.1.323.exe");
    Equal(Path.GetFullPath(installer), ManagedReleaseContract.ValidateStagedInstallerPath(installer, root), "staged setup");
    Rejects<InvalidDataException>(() => ManagedReleaseContract.ValidateStagedInstallerPath(
        Path.Combine(root, "sub", "Setup.exe"), root));
    Rejects<InvalidDataException>(() => ManagedReleaseContract.ValidateStagedInstallerPath(
        Path.Combine(root, "notepad.exe"), root));
}

static void UpdateLogBoundary()
{
    string root = Path.Combine(Path.GetTempPath(), "WinForge Contract", "updates");
    string log = Path.Combine(root, "install-1.1.323.log");
    Equal(Path.GetFullPath(log), ManagedReleaseContract.ValidateUpdateLogPath(log, root), "update log");
    Rejects<InvalidDataException>(() => ManagedReleaseContract.ValidateUpdateLogPath(
        Path.Combine(root, "sub", "install.log"), root));
}

static void PortableFootprint()
    => ManagedReleaseContract.ValidatePortableEntries(new[]
    {
        "WinForge.exe",
        "WinForgeLauncher.exe",
        "WinForge.release.json",
        "updater-runtime/WinForgeUpdater.exe",
        "Assets/AppIcon.ico"
    }, "1.1.323");

static void PortableFootprintRejection()
{
    Rejects<InvalidDataException>(() => ManagedReleaseContract.ValidatePortableEntries(new[]
    {
        "WinForge.exe", "WinForgeLauncher.exe", "WinForge.release.json", "../escape.txt"
    }, "1.1.323"));
    Rejects<InvalidDataException>(() => ManagedReleaseContract.ValidatePortableEntries(new[]
    {
        "WinForge.exe", "WinForgeLauncher.exe", "WinForge.release.json"
    }, "1.1.323"));
}

static void WorkflowContract()
{
    string text = ReadRepoFile(".github", "workflows", "release.yml");
    Contains(text, "push:", "push trigger");
    Contains(text, "workflow_dispatch:", "dispatch trigger");
    Contains(text, "Ding-Ding-Projects/WinForge", "canonical repository gate");
    Contains(text, "WinForge.release.json", "packaged manifest");
    Contains(text, "Setup.exe", "Squirrel installer asset");
    Contains(text, "RELEASES", "Squirrel release index");
    Contains(text, "Squirrel.Windows", "Squirrel packaging tool");
    Contains(text, "--no-msi", "MSI disabled");
    Contains(text, "WinForge-portable-x64-$env:RELEASE_VERSION.zip", "portable asset");
    Contains(text, "managed release tag already exists and is immutable", "immutable tag gate");
    Contains(text, "managed release digest mismatch", "GitHub/local digest proof");
    Contains(text, "managed release download URL mismatch", "download URL proof");
    Contains(text, "ProductVersion.Trim()", "version mapping proof");
}

static void InstallerMetadataContract()
{
    string text = ReadRepoFile("tools", "SquirrelPackaging", "SquirrelPackaging.csproj");
    Contains(text, "Squirrel.Windows", "Squirrel.Windows package");
    Contains(text, "2.0.1", "pinned Squirrel version");
    string script = ReadRepoFile("tools", "build-winforge.ps1");
    Contains(script, "--releasify", "Squirrel releasify");
    Contains(script, "--no-msi", "MSI disabled");
    Contains(script, "Setup.exe", "Setup output");
    Contains(script, "RELEASES", "RELEASES output");
    Contains(script, "Get-PeCertificateTable", "unsigned verification");
    Contains(script, "certificateTable.Size -ne 0", "certificate-table rejection");
}

static void RuntimeWiringContract()
{
    string app = ReadRepoFile("Services", "AppUpdateService.cs");
    string updater = ReadRepoFile("updater", "WinForgeUpdater", "MainWindow.xaml.cs");
    string launcher = ReadRepoFile("launcher", "Program.cs");
    Contains(app, "ManagedReleaseContract.LatestReleaseApi", "app endpoint");
    Contains(app, "ManagedReleaseContract.TryResolveRelease", "app asset selection");
    Contains(updater, "ManagedReleaseContract.ValidateInstallLayout", "updater layout boundary");
    Contains(updater, "ManagedReleaseContract.IsCanonicalReleaseDownload", "updater URL boundary");
    Contains(launcher, "ManagedReleaseContract.ValidateStagedInstallerPath", "launcher staging boundary");
    Contains(launcher, "ManagedReleaseContract.FixedTimeSha256Equals", "launcher digest boundary");
}

static void SquirrelUpdateActionContract()
{
    string app = ReadRepoFile("Services", "AppUpdateService.cs");
    string launcher = ReadRepoFile("launcher", "Program.cs");
    Contains(app, "Restart to install update", "restart action");
    Contains(app, "Later", "later action");
    Contains(app, "PendingInstallerPathKey", "staged installer state");
    Contains(app, "LaunchInstallerAfterExit", "user-selected handoff");
    False(app.Contains("LaunchUpdaterApp(", StringComparison.Ordinal), "obsolete visual updater handoff");
    Contains(launcher, "Starting unsigned Squirrel Setup.exe", "Squirrel handoff");
    False(launcher.Contains("/VERYSILENT", StringComparison.Ordinal), "legacy Inno command-line arguments");
}

static void UniversalExperienceContract()
{
    string settings = ReadRepoFile("Services", "UniversalSettingsService.cs");
    string page = ReadRepoFile("Pages", "SettingsPage.xaml.cs");
    string about = ReadRepoFile("Pages", "AboutPage.xaml.cs");
    string changelog = ReadRepoFile("Services", "ChangelogService.cs");
    Contains(settings, "universal.emojiDialogsEnabled", "emoji setting key");
    Contains(settings, "PasswordVault", "vault-backed School unlock");
    Contains(ReadRepoFile("Services", "NarratorService.cs"), "NarratorLanguage", "narration language");
    Contains(ReadRepoFile("Services", "AnnouncementService.cs"), "EnqueueCoalesced", "serialized replacement queue");
    Contains(page, "Show emojis in dialogs and message boxes", "emoji control");
    Contains(page, "temporarily removed language, funny-level, personal-vocabulary, and dim-sum controls", "School mode surface removal");
    Contains(about, "new SearchPatternBox", "offline changelog search");
    Contains(about, "CalendarDatePicker", "offline changelog date filter");
    Contains(changelog, "CHANGELOG.md", "offline changelog source");
    Contains(ReadRepoFile("WinForge.csproj"), "CHANGELOG.md", "bundled changelog");
}

static void PinnedTabContract()
{
    string session = ReadRepoFile("Services", "TabSessionService.cs");
    string main = ReadRepoFile("MainWindow.xaml.cs");
    Contains(session, "IsPinned", "pinned tab field");
    Contains(session, "AppendBoolean", "pinned tab persistence");
    Contains(main, "InsertTabInPinnedRegion", "pinned tab ordering");
    Contains(main, "Pin tab · 釘選分頁", "pin menu action");
    Contains(main, "Unpin tab · 取消釘選分頁", "unpin menu action");
}

static void NoStaleManagedRepositoryLinks()
{
    string root = RepoRoot();
    string[] extensions = [".cs", ".csproj", ".html", ".iss", ".js", ".json", ".md", ".ps1", ".xaml", ".yml", ".yaml"];
    foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
    {
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (relative.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
            relative.StartsWith("ThirdParty/", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase) ||
            relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase) ||
            !extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) continue;
        string text = File.ReadAllText(path);
        string retiredManagedSlug = "codingmachineedge/" + "WinForge";
        if (text.Contains(retiredManagedSlug, StringComparison.Ordinal) &&
            text.Replace("codingmachineedge/WinForge-Native", string.Empty, StringComparison.Ordinal)
                .Contains(retiredManagedSlug, StringComparison.Ordinal))
            throw new InvalidOperationException($"stale managed repository link: {relative}");
    }
}

static void NativeRepositoryLinksPreserved()
{
    Contains(ReadRepoFile("README.md"), "codingmachineedge/WinForge-Native", "README native link");
    Contains(ReadRepoFile("AGENT_MEMORY.md"), "codingmachineedge/WinForge-Native", "memory native link");
}

static ManagedReleaseMetadata ValidRelease()
{
    const string version = "1.1.323";
    string tag = "v" + version;
    string baseUrl = $"https://github.com/Ding-Ding-Projects/WinForge/releases/download/{tag}/";
    return new ManagedReleaseMetadata(tag, false, false, new[]
    {
        new ManagedReleaseAsset("Setup.exe", baseUrl + "Setup.exe", "sha256:" + new string('a', 64), 195_342_286),
        new ManagedReleaseAsset("RELEASES", baseUrl + "RELEASES", "sha256:" + new string('c', 64), 78),
        new ManagedReleaseAsset("WinForge-1.1.323-full.nupkg", baseUrl + "WinForge-1.1.323-full.nupkg", "sha256:" + new string('d', 64), 195_000_000),
        new ManagedReleaseAsset("WinForge-portable-x64-1.1.323.zip", baseUrl + "WinForge-portable-x64-1.1.323.zip", "sha256:" + new string('b', 64), 293_830_345)
    });
}

static string ReadRepoFile(params string[] parts) => File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(parts).ToArray()));

static string RepoRoot()
{
    for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        if (File.Exists(Path.Combine(directory.FullName, "WinForge.sln"))) return directory.FullName;
    throw new DirectoryNotFoundException("Could not locate WinForge.sln from the test output directory.");
}

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'");
}

static void True(bool value, string label)
{
    if (!value) throw new InvalidOperationException(label + " was false");
}

static void False(bool value, string label)
{
    if (value) throw new InvalidOperationException(label + " was true");
}

static void NotNull(object? value, string label)
{
    if (value is null) throw new InvalidOperationException(label + " was null");
}

static void Contains(string text, string expected, string label)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"{label}: missing '{expected}'");
}

static void Rejects<TException>(Action action) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException($"expected {typeof(TException).Name}");
}
