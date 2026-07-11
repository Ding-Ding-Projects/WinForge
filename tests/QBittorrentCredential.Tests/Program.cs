using System.Collections.Concurrent;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("legacy plaintext migrates to an opaque DPAPI blob and is erased", LegacyPlaintextMigrates);
Run("current-user DPAPI protects a qBittorrent password round trip", CurrentUserDpapiRoundTrips);
Run("failed migration retains legacy data but never exposes it", FailedMigrationFailsClosed);
Run("unreadable blob retains legacy recovery data but never exposes it", UnreadableBlobFailsClosed);
Run("empty readable blob retains legacy recovery data", EmptyReadableBlobFailsClosed);
Run("save failure preserves an existing encrypted credential", FailedSavePreservesExistingBlob);
Run("forget removes both legacy and encrypted credential values", ForgetRemovesAllCredentialValues);
Run("new request generation cancels and rejects stale work", RequestGenerationRejectsStaleWork);
Run("page source keeps the uncategorised sentinel as an escaped runtime NUL", UncategorisedSentinelSourceIsEscaped);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} qBittorrent credential and refresh-generation tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} qBittorrent credential and refresh-generation tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {name}: {ex.Message}");
    }
}

void LegacyPlaintextMigrates()
{
    var settings = new FakeSettings
    {
        [QBittorrentCredentialStore.RememberKey] = "1",
        [QBittorrentCredentialStore.LegacyPasswordKey] = "legacy-secret",
    };
    var protector = new FakeProtector();
    var store = new QBittorrentCredentialStore(settings, protector);

    Equal("legacy-secret", store.ReadSavedPassword(), "migration did not preserve usable credential");
    Equal("", settings.Get(QBittorrentCredentialStore.LegacyPasswordKey, "missing"), "plaintext legacy value was retained");
    var blob = settings.Get(QBittorrentCredentialStore.PasswordBlobKey, "");
    Assert(!string.IsNullOrEmpty(blob), "encrypted value was not written");
    Assert(!blob.Contains("legacy-secret", StringComparison.Ordinal), "encrypted value contains plaintext secret");
}

void CurrentUserDpapiRoundTrips()
{
    const string secret = "qBittorrent-test-password";
    var protector = new DpapiQBittorrentSecretProtector();

    Assert(protector.TryProtect(secret, out var blob), "current-user DPAPI failed to protect the test password");
    Assert(!string.IsNullOrEmpty(blob), "DPAPI emitted an empty blob");
    Assert(!blob.Contains(secret, StringComparison.Ordinal), "DPAPI blob contains the plaintext test password");
    Assert(protector.TryUnprotect(blob, out var restored), "current-user DPAPI failed to unprotect its own blob");
    Equal(secret, restored, "DPAPI round trip changed the password");
}

void FailedMigrationFailsClosed()
{
    var settings = new FakeSettings
    {
        [QBittorrentCredentialStore.RememberKey] = "1",
        [QBittorrentCredentialStore.LegacyPasswordKey] = "legacy-secret",
    };
    var store = new QBittorrentCredentialStore(settings, new FakeProtector { FailProtect = true });

    Equal("", store.ReadSavedPassword(), "failed migration exposed plaintext credential");
    Equal("legacy-secret", settings.Get(QBittorrentCredentialStore.LegacyPasswordKey, ""), "failed migration destroyed recoverable legacy data");
    Equal("", settings.Get(QBittorrentCredentialStore.PasswordBlobKey, ""), "failed migration wrote a bogus encrypted value");
}

void UnreadableBlobFailsClosed()
{
    var settings = new FakeSettings
    {
        [QBittorrentCredentialStore.RememberKey] = "1",
        [QBittorrentCredentialStore.LegacyPasswordKey] = "legacy-secret",
        [QBittorrentCredentialStore.PasswordBlobKey] = "dpapi:unreadable",
    };
    var store = new QBittorrentCredentialStore(settings, new FakeProtector());

    Assert(!store.MigrateLegacyPassword(), "unreadable blob was accepted as a complete migration");
    Equal("legacy-secret", settings.Get(QBittorrentCredentialStore.LegacyPasswordKey, ""), "unreadable blob erased legacy recovery data");
    Equal("", store.ReadSavedPassword(), "unreadable blob exposed a legacy plaintext credential");
}

void EmptyReadableBlobFailsClosed()
{
    var settings = new FakeSettings
    {
        [QBittorrentCredentialStore.RememberKey] = "1",
        [QBittorrentCredentialStore.LegacyPasswordKey] = "legacy-secret",
        [QBittorrentCredentialStore.PasswordBlobKey] = "dpapi:empty",
    };
    var store = new QBittorrentCredentialStore(settings, new FakeProtector());

    Assert(!store.MigrateLegacyPassword(), "empty encrypted value was accepted as a complete migration");
    Equal("legacy-secret", settings.Get(QBittorrentCredentialStore.LegacyPasswordKey, ""), "empty encrypted value erased legacy recovery data");
}

void FailedSavePreservesExistingBlob()
{
    var settings = new FakeSettings
    {
        [QBittorrentCredentialStore.RememberKey] = "1",
        [QBittorrentCredentialStore.PasswordBlobKey] = "dpapi:existing",
    };
    var store = new QBittorrentCredentialStore(settings, new FakeProtector { FailProtect = true });

    Assert(!store.SaveRememberedPassword("replacement-secret", remember: true), "failed DPAPI protect reported success");
    Equal("dpapi:existing", settings.Get(QBittorrentCredentialStore.PasswordBlobKey, ""), "failed save overwrote the existing opaque credential");
    Assert(!settings.Values.Select(pair => pair.Value).Any(v => v.Contains("replacement-secret", StringComparison.Ordinal)), "failed save leaked replacement plaintext");
}

void ForgetRemovesAllCredentialValues()
{
    var settings = new FakeSettings
    {
        [QBittorrentCredentialStore.RememberKey] = "1",
        [QBittorrentCredentialStore.LegacyPasswordKey] = "legacy-secret",
        [QBittorrentCredentialStore.PasswordBlobKey] = "dpapi:existing",
    };
    var store = new QBittorrentCredentialStore(settings, new FakeProtector());

    Assert(store.SaveRememberedPassword(null, remember: false), "forget operation failed");
    Equal("0", settings.Get(QBittorrentCredentialStore.RememberKey, ""), "remember flag survived forget");
    Equal("", settings.Get(QBittorrentCredentialStore.LegacyPasswordKey, "missing"), "legacy value survived forget");
    Equal("", settings.Get(QBittorrentCredentialStore.PasswordBlobKey, "missing"), "encrypted value survived forget");
}

void RequestGenerationRejectsStaleWork()
{
    using var requests = new QBittorrentRequestGeneration();
    int first = requests.Restart();
    Assert(requests.TryGetToken(first, out var oldToken), "initial scope did not provide a token");
    int second = requests.Restart();

    Assert(oldToken.IsCancellationRequested, "restarting did not cancel stale work");
    Assert(!requests.IsCurrent(first), "stale generation remained current");
    Assert(requests.IsCurrent(second), "new generation was not current");
    Assert(!requests.TryGetToken(first, out _), "stale generation produced a live token");
}

void UncategorisedSentinelSourceIsEscaped()
{
    const string runtimeSentinel = "\0NONE";
    Assert(runtimeSentinel.Length == 5 && runtimeSentinel[0] == '\0', "C# escaped sentinel did not compile to a runtime NUL");

    var root = FindRepositoryRoot();
    var sourcePath = Path.Combine(root, "Pages", "QBittorrentModule.xaml.cs");
    var bytes = File.ReadAllBytes(sourcePath);
    Assert(Array.IndexOf(bytes, (byte)0) < 0, "page source contains a raw NUL byte");

    var source = File.ReadAllText(sourcePath);
    Assert(source.Contains("Tag = \"\\0NONE\"", StringComparison.Ordinal), "page source lost the C# escaped sentinel tag");
    Assert(source.Contains("_catFilter == \"\\0NONE\"", StringComparison.Ordinal), "page source lost the C# escaped sentinel comparison");
}

static string FindRepositoryRoot()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
    {
        if (File.Exists(Path.Combine(directory.FullName, "WinForge.sln"))) return directory.FullName;
    }

    throw new DirectoryNotFoundException("Could not locate the WinForge repository root.");
}

static void Equal<T>(T expected, T actual, string message) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FakeSettings : IQBittorrentSettingsStore
{
    private readonly ConcurrentDictionary<string, string> _values = new(StringComparer.Ordinal);

    public IEnumerable<KeyValuePair<string, string>> Values => _values;

    public string this[string key]
    {
        set => _values[key] = value;
    }

    public string Get(string key, string fallback) => _values.TryGetValue(key, out var value) ? value : fallback;
    public void Set(string key, string value) => _values[key] = value;
}

sealed class FakeProtector : IQBittorrentSecretProtector
{
    public bool FailProtect { get; init; }
    public bool TryProtect(string plain, out string protectedSecret)
    {
        protectedSecret = "";
        if (FailProtect) return false;
        protectedSecret = "dpapi:fake:" + plain.Length;
        return true;
    }

    public bool TryUnprotect(string protectedSecret, out string plain)
    {
        plain = protectedSecret == "dpapi:fake:13" ? "legacy-secret" : "";
        return protectedSecret is "dpapi:fake:13" or "dpapi:empty";
    }
}
