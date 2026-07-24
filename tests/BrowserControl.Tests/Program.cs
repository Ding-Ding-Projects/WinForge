using System.Text.Json;
using WinForge.Services;

var tests = new (string Name, Action Body)[]
{
    ("URL normalization adds HTTPS", UrlAddsHttps),
    ("URL validation rejects non-HTTP schemes", UrlRejectsScheme),
    ("URL validation rejects oversized input", UrlRejectsOversize),
    ("app mode keeps URL in one argument", AppModeArgumentBoundary),
    ("Edge kiosk has the fullscreen contract", EdgeKioskContract),
    ("Local State maps real profile display names", LocalStateProfileMapping),
    ("profile launch uses the selected directory", SelectedProfileLaunch),
    ("profile containment rejects traversal", ProfileContainment),
    ("PWA shortcut parser reads app ID and profile", PwaShortcutParsing),
    ("PWA discovery deduplicates runtime shortcut targets", PwaDiscoveryDeduplicates),
    ("PWA launch uses parsed app ID and profile", PwaLaunchContract),
    ("flags and policy pages cover both browsers", InternalPages),
    ("cache cleanup deletes Cache and Code Cache", CacheCleanup),
    ("cache cleanup refuses while browser is running", CacheRequiresClosedBrowser),
    ("proxy input and bypass stay separate arguments", ProxyArgumentBoundary),
    ("invalid proxy input is rejected", ProxyValidation),
    ("throwaway sessions are GUID scoped and cleaned", ThrowawayLifecycle),
    ("ephemeral cleanup rejects paths outside its root", EphemeralContainment),
    ("feature enable and disable switches are validated", FeatureSwitches),
    ("feature command injection is rejected", FeatureValidation),
    ("remote debugging is loopback and isolated", RemoteDebugContract),
    ("remote debugging rejects unsafe ports", RemoteDebugPortValidation),
    ("winget browser package IDs and verbs are exact", WingetPlans),
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

Console.WriteLine($"Browser Control contract passed: {passed}/{tests.Length}.");
return 0;

static void UrlAddsHttps()
    => Equal("https://example.com/path", BrowserControlCore.NormalizeHttpUrl("example.com/path"), "normalized URL");

static void UrlRejectsScheme()
{
    Throws<ArgumentException>(() => BrowserControlCore.NormalizeHttpUrl("file:///C:/secret.txt"));
    Throws<ArgumentException>(() => BrowserControlCore.NormalizeHttpUrl("https://user:secret@example.com/"));
}

static void UrlRejectsOversize()
    => Throws<ArgumentException>(() => BrowserControlCore.NormalizeHttpUrl("https://example.com/" + new string('a', BrowserControlCore.MaxUrlLength)));

static void AppModeArgumentBoundary()
{
    var plan = BrowserControlCore.BuildAppModePlan(ChromiumBrowser.Chrome, "chrome.exe", "https://example.com/?a=1&b=two");
    Equal(1, plan.Arguments.Count, "argument count");
    Equal("--app=https://example.com/?a=1&b=two", plan.Arguments[0], "app argument");
}

static void EdgeKioskContract()
{
    var plan = BrowserControlCore.BuildKioskPlan(ChromiumBrowser.Edge, "msedge.exe", "example.com");
    Sequence(new[] { "--kiosk", "https://example.com/", "--edge-kiosk-type=fullscreen", "--kiosk-idle-timeout-minutes=0" }, plan.Arguments, "Edge kiosk argv");
}

static void LocalStateProfileMapping()
{
    WithTemp(root =>
    {
        Directory.CreateDirectory(Path.Combine(root, "Default"));
        Directory.CreateDirectory(Path.Combine(root, "Profile 2"));
        Directory.CreateDirectory(Path.Combine(root, "Crashpad"));
        var localState = new
        {
            profile = new
            {
                info_cache = new Dictionary<string, object>
                {
                    ["Default"] = new { name = "Work" },
                    ["Profile 2"] = new { shortcut_name = "Personal" },
                },
            },
        };
        File.WriteAllText(Path.Combine(root, "Local State"), JsonSerializer.Serialize(localState));

        var profiles = BrowserControlCore.DiscoverProfiles(ChromiumBrowser.Edge, root);
        Equal(2, profiles.Count, "profile count");
        Equal("Work", profiles.Single(p => p.DirectoryName == "Default").DisplayName, "Default display name");
        Equal("Personal", profiles.Single(p => p.DirectoryName == "Profile 2").DisplayName, "Profile 2 display name");
    });
}

static void SelectedProfileLaunch()
{
    WithTemp(root =>
    {
        var path = Path.Combine(root, "Profile 7");
        Directory.CreateDirectory(path);
        var profile = new BrowserProfile(ChromiumBrowser.Chrome, "Profile 7", "QA", root, path);
        var plan = BrowserControlCore.BuildProfilePlan(profile, "chrome.exe");
        Sequence(new[] { "--profile-directory=Profile 7" }, plan.Arguments, "profile argv");
    });
}

static void ProfileContainment()
{
    WithTemp(root =>
    {
        var outside = Path.Combine(Path.GetDirectoryName(root)!, "outside-profile");
        var profile = new BrowserProfile(ChromiumBrowser.Chrome, "Default", "Bad", root, outside);
        Throws<InvalidOperationException>(() => BrowserControlCore.BuildProfilePlan(profile, "chrome.exe"));
    });
}

static void PwaShortcutParsing()
{
    var shortcut = new BrowserShortcutData(
        Path.Combine(Path.GetTempPath(), "msedge_proxy.exe"),
        "--profile-directory=\"Profile 3\" --app-id=abcdefghijklmnopabcdefghijklmnop --app-launch-source=4",
        "Fixture PWA");
    True(BrowserControlCore.TryParsePwaShortcut(shortcut, Path.Combine(Path.GetTempPath(), "Fixture PWA.lnk"), out var pwa), "shortcut parsed");
    Equal(ChromiumBrowser.Edge, pwa!.Browser, "browser");
    Equal("Profile 3", pwa.ProfileDirectory, "profile");
    Equal("abcdefghijklmnopabcdefghijklmnop", pwa.AppId, "app id");
}

static void PwaDiscoveryDeduplicates()
{
    WithTemp(root =>
    {
        var first = Path.Combine(root, "One.lnk");
        var second = Path.Combine(root, "Two.lnk");
        File.WriteAllText(first, "fixture");
        File.WriteAllText(second, "fixture");
        BrowserShortcutData? Reader(string _) => new(
            Path.Combine(root, "chrome_proxy.exe"),
            "--profile-directory=Default --app-id=abcdefghijklmnopabcdefghijklmnop",
            "Fixture");
        var pwas = BrowserControlCore.DiscoverPwas(new[] { root }, Reader);
        Equal(1, pwas.Count, "deduplicated PWA count");
    });
}

static void PwaLaunchContract()
{
    var pwa = new BrowserPwa(
        ChromiumBrowser.Chrome,
        "Fixture",
        "abcdefghijklmnopabcdefghijklmnop",
        "Default",
        Path.Combine(Path.GetTempPath(), "chrome_proxy.exe"),
        Path.Combine(Path.GetTempPath(), "Fixture.lnk"));
    var plan = BrowserControlCore.BuildPwaPlan(pwa);
    Sequence(new[] { "--profile-directory=Default", "--app-id=abcdefghijklmnopabcdefghijklmnop" }, plan.Arguments, "PWA argv");
}

static void InternalPages()
{
    Equal("chrome://flags", BrowserControlCore.BuildInternalPagePlan(ChromiumBrowser.Chrome, "chrome.exe", false).Arguments.Single(), "Chrome flags");
    Equal("chrome://policy", BrowserControlCore.BuildInternalPagePlan(ChromiumBrowser.Chrome, "chrome.exe", true).Arguments.Single(), "Chrome policy");
    Equal("edge://flags", BrowserControlCore.BuildInternalPagePlan(ChromiumBrowser.Edge, "msedge.exe", false).Arguments.Single(), "Edge flags");
    Equal("edge://policy", BrowserControlCore.BuildInternalPagePlan(ChromiumBrowser.Edge, "msedge.exe", true).Arguments.Single(), "Edge policy");
}

static void CacheCleanup()
{
    WithTemp(root =>
    {
        var profilePath = Path.Combine(root, "Default");
        Directory.CreateDirectory(Path.Combine(profilePath, "Cache"));
        Directory.CreateDirectory(Path.Combine(profilePath, "Code Cache"));
        File.WriteAllText(Path.Combine(profilePath, "Cache", "cache.bin"), "x");
        File.WriteAllText(Path.Combine(profilePath, "Code Cache", "code.bin"), "y");
        var profile = new BrowserProfile(ChromiumBrowser.Chrome, "Default", "Work", root, profilePath);
        var report = BrowserControlCore.ClearProfileCaches(profile, _ => false);
        Equal(2, report.DeletedDirectoryCount, "deleted folders");
        False(Directory.Exists(Path.Combine(profilePath, "Cache")), "Cache deleted");
        False(Directory.Exists(Path.Combine(profilePath, "Code Cache")), "Code Cache deleted");
    });
}

static void CacheRequiresClosedBrowser()
{
    WithTemp(root =>
    {
        var profilePath = Path.Combine(root, "Default");
        Directory.CreateDirectory(Path.Combine(profilePath, "Cache"));
        var profile = new BrowserProfile(ChromiumBrowser.Edge, "Default", "Work", root, profilePath);
        Throws<InvalidOperationException>(() => BrowserControlCore.ClearProfileCaches(profile, _ => true));
        True(Directory.Exists(Path.Combine(profilePath, "Cache")), "cache preserved");
    });
}

static void ProxyArgumentBoundary()
{
    WithSession((directory, root) =>
    {
        var plan = BrowserControlCore.BuildProxyPlan(
            ChromiumBrowser.Edge,
            "msedge.exe",
            "socks5://127.0.0.1:1080",
            "*.local;127.0.0.1",
            "https://example.com/?a=1&b=two",
            directory,
            root);
        True(plan.Arguments.Contains("--proxy-server=socks5://127.0.0.1:1080"), "proxy argument");
        True(plan.Arguments.Contains("--proxy-bypass-list=*.local;127.0.0.1"), "bypass argument");
        True(plan.Arguments.Contains("https://example.com/?a=1&b=two"), "URL argument boundary");
    });
}

static void ProxyValidation()
{
    Throws<ArgumentException>(() => BrowserControlCore.NormalizeProxy("127.0.0.1:8080 --disable-web-security"));
    Throws<ArgumentException>(() => BrowserControlCore.NormalizeProxy("http://user:secret@127.0.0.1:8080"));
    Throws<ArgumentException>(() => BrowserControlCore.NormalizeProxy("http://127.0.0.1:8080/path"));
    Throws<ArgumentException>(() => BrowserControlCore.NormalizeBypassList("*.local; bad host"));
}

static void ThrowawayLifecycle()
{
    WithTemp(temp =>
    {
        var first = BrowserControlCore.CreateEphemeralDirectory(ChromiumBrowser.Chrome, "throwaway", temp, out var root);
        var second = BrowserControlCore.CreateEphemeralDirectory(ChromiumBrowser.Chrome, "throwaway", temp, out var root2);
        False(first.Equals(second, StringComparison.OrdinalIgnoreCase), "GUID uniqueness");
        Equal(root, root2, "session root");
        File.WriteAllText(Path.Combine(first, "fixture.txt"), "owned");
        True(BrowserControlCore.TryDeleteEphemeralDirectory(first, root), "first cleanup");
        True(BrowserControlCore.TryDeleteEphemeralDirectory(second, root), "second cleanup");
    });
}

static void EphemeralContainment()
{
    WithTemp(temp =>
    {
        var directory = BrowserControlCore.CreateEphemeralDirectory(ChromiumBrowser.Edge, "debug", temp, out var root);
        var outside = Path.Combine(temp, "outside");
        Directory.CreateDirectory(outside);
        False(BrowserControlCore.TryDeleteEphemeralDirectory(outside, root), "outside rejected");
        True(Directory.Exists(outside), "outside preserved");
        True(BrowserControlCore.TryDeleteEphemeralDirectory(directory, root), "owned path cleaned");
    });
}

static void FeatureSwitches()
{
    WithSession((directory, root) =>
    {
        var enabled = BrowserControlCore.BuildFeaturePlan(ChromiumBrowser.Chrome, "chrome.exe", "AlphaFeature, Beta.Feature", BrowserFeatureMode.Enable, "example.com", directory, root);
        True(enabled.Arguments.Contains("--enable-features=AlphaFeature,Beta.Feature"), "enable switch");
        var disabled = BrowserControlCore.BuildFeaturePlan(ChromiumBrowser.Chrome, "chrome.exe", "AlphaFeature", BrowserFeatureMode.Disable, "example.com", directory, root);
        True(disabled.Arguments.Contains("--disable-features=AlphaFeature"), "disable switch");
    });
}

static void FeatureValidation()
    => Throws<ArgumentException>(() => BrowserControlCore.NormalizeFeatureNames("GoodFeature,--remote-debugging-port=0"));

static void RemoteDebugContract()
{
    WithSession((directory, root) =>
    {
        var plan = BrowserControlCore.BuildRemoteDebugPlan(ChromiumBrowser.Edge, "msedge.exe", 9222, "example.com", directory, root);
        True(plan.Arguments.Contains("--remote-debugging-address=127.0.0.1"), "loopback address");
        True(plan.Arguments.Contains("--remote-debugging-port=9222"), "debug port");
        True(plan.Arguments.Any(a => a == "--user-data-dir=" + directory), "isolated directory");
        Equal(directory, plan.EphemeralDirectory, "tracked cleanup directory");
    });
}

static void RemoteDebugPortValidation()
{
    WithSession((directory, root) =>
        Throws<ArgumentOutOfRangeException>(() => BrowserControlCore.BuildRemoteDebugPlan(ChromiumBrowser.Edge, "msedge.exe", 80, "example.com", directory, root)));
}

static void WingetPlans()
{
    var chrome = BrowserControlCore.BuildWingetPlan(ChromiumBrowser.Chrome, BrowserPackageAction.Install);
    Equal("Google.Chrome", chrome.PackageId, "Chrome package ID");
    Equal("install", chrome.Arguments[0], "install verb");
    True(chrome.Arguments.Contains("--disable-interactivity"), "noninteractive switch");
    var edge = BrowserControlCore.BuildWingetPlan(ChromiumBrowser.Edge, BrowserPackageAction.Upgrade);
    Equal("Microsoft.Edge", edge.PackageId, "Edge package ID");
    Equal("upgrade", edge.Arguments[0], "upgrade verb");
}

static void WithSession(Action<string, string> action)
{
    WithTemp(temp =>
    {
        var directory = BrowserControlCore.CreateEphemeralDirectory(ChromiumBrowser.Edge, "fixture", temp, out var root);
        try { action(directory, root); }
        finally { BrowserControlCore.TryDeleteEphemeralDirectory(directory, root); }
    });
}

static void WithTemp(Action<string> action)
{
    var root = Path.Combine(Path.GetTempPath(), "WinForge-BrowserControl-Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try { action(root); }
    finally
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}

static void Sequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    Equal(expected.Count, actual.Count, label + " count");
    for (var i = 0; i < expected.Count; i++) Equal(expected[i], actual[i], $"{label}[{i}]");
}

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void True(bool value, string label)
{
    if (!value) throw new InvalidOperationException(label + " was false.");
}

static void False(bool value, string label) => True(!value, label);

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    catch (Exception ex) { throw new InvalidOperationException($"Expected {typeof(T).Name}, got {ex.GetType().Name}."); }
    throw new InvalidOperationException($"Expected {typeof(T).Name}, but no exception was thrown.");
}
