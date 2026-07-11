using System.Text.Json;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("valid settings retain current behavior and rotate backup atomically", ValidSettingsRotateBackup);
Run("truncated primary recovers from backup and preserves evidence", TruncatedPrimaryRecoversBackup);
Run("missing primary restores a valid backup", MissingPrimaryRecoversBackup);
Run("unrecoverable malformed storage fails closed on ordinary Set", UnrecoverableStorageFailsClosed);
Run("explicit valid import repairs a fail-closed store", ExplicitImportRepairsStore);
Run("explicit import preserves an existing valid recovery backup", ExplicitImportPreservesValidBackup);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} settings-store tests (temporary fixtures only)");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} settings-store tests");
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

static void ValidSettingsRotateBackup()
{
    using var fixture = new SettingsFixture();
    WriteSettings(fixture.Path, new Dictionary<string, string>
    {
        ["theme"] = "dark",
        ["language"] = "en",
    });

    var session = new SettingsStoreSession(fixture.Path);
    Equal("dark", session.Get("theme", ""), "valid primary was not loaded");
    Assert(session.PersistenceEnabled, "valid primary unexpectedly disabled persistence");

    session.Set("accent", "blue");
    var primary = ReadSettings(fixture.Path);
    var backup = ReadSettings(SettingsStorePersistence.BackupPathFor(fixture.Path));
    Equal("blue", primary["accent"], "new setting was not written");
    Equal("dark", backup["theme"], "previous primary was not retained as backup");
    Assert(!backup.ContainsKey("accent"), "backup contains the replacement snapshot");

    // Exercise File.Replace while a previous .bak already exists: the backup must advance to
    // the immediately preceding complete snapshot instead of failing or retaining stale data.
    session.Set("font", "12");
    primary = ReadSettings(fixture.Path);
    backup = ReadSettings(SettingsStorePersistence.BackupPathFor(fixture.Path));
    Equal("12", primary["font"], "second replacement was not written");
    Equal("blue", backup["accent"], "existing backup did not rotate to the prior snapshot");
    Assert(!backup.ContainsKey("font"), "backup contains the second replacement snapshot");
    AssertNoTemporaryFiles(fixture.Root);
}

static void TruncatedPrimaryRecoversBackup()
{
    using var fixture = new SettingsFixture();
    const string truncated = "{\"theme\": \"new\"";
    File.WriteAllText(fixture.Path, truncated);
    WriteSettings(SettingsStorePersistence.BackupPathFor(fixture.Path), new Dictionary<string, string>
    {
        ["theme"] = "safe",
        ["language"] = "yue",
    });

    var session = new SettingsStoreSession(fixture.Path);
    Assert(session.PersistenceEnabled, "valid backup could not restore persistence");
    Equal("safe", session.Get("theme", ""), "backup values were not loaded");
    Equal("yue", ReadSettings(fixture.Path)["language"], "primary was not restored from backup");
    Equal("safe", ReadSettings(SettingsStorePersistence.BackupPathFor(fixture.Path))["theme"],
        "recovery rotated the good backup away");

    var preserved = Directory.GetFiles(fixture.Root, "settings.json.corrupt-*");
    Equal(1, preserved.Length, "truncated primary was not preserved before recovery");
    Equal(truncated, File.ReadAllText(preserved[0]), "preserved primary changed");
    AssertNoTemporaryFiles(fixture.Root);
}

static void MissingPrimaryRecoversBackup()
{
    using var fixture = new SettingsFixture();
    WriteSettings(SettingsStorePersistence.BackupPathFor(fixture.Path), new Dictionary<string, string>
    {
        ["zoom"] = "125",
    });

    var session = new SettingsStoreSession(fixture.Path);
    Assert(session.PersistenceEnabled, "backup-only recovery left persistence disabled");
    Equal("125", session.Get("zoom", ""), "backup-only value was not loaded");
    Equal("125", ReadSettings(fixture.Path)["zoom"], "missing primary was not restored");
    Assert(Directory.GetFiles(fixture.Root, "settings.json.corrupt-*").Length == 0,
        "missing primary should not create a corrupt-file artifact");
}

static void UnrecoverableStorageFailsClosed()
{
    using var fixture = new SettingsFixture();
    const string malformed = "{\"theme\":";
    File.WriteAllText(fixture.Path, malformed);

    var session = new SettingsStoreSession(fixture.Path);
    Assert(!session.PersistenceEnabled, "malformed storage without recovery unexpectedly enabled writes");
    session.Set("theme", "light");

    Equal("light", session.Get("theme", ""), "in-memory settings should keep working during this run");
    Equal(malformed, File.ReadAllText(fixture.Path), "ordinary Set overwrote the malformed primary");
    Assert(!File.Exists(SettingsStorePersistence.BackupPathFor(fixture.Path)),
        "ordinary Set created a backup while persistence was fail-closed");
    AssertNoTemporaryFiles(fixture.Root);
}

static void ExplicitImportRepairsStore()
{
    using var fixture = new SettingsFixture();
    const string malformed = "{\"theme\":";
    File.WriteAllText(fixture.Path, malformed);
    var importPath = Path.Combine(fixture.Root, "known-good-import.json");
    WriteSettings(importPath, new Dictionary<string, string>
    {
        ["theme"] = "light",
        ["language"] = "en",
    });

    var session = new SettingsStoreSession(fixture.Path);
    Equal(2, session.ImportFrom(importPath), "valid import count");
    Assert(session.PersistenceEnabled, "explicit valid import did not repair persistence");
    Equal("light", ReadSettings(fixture.Path)["theme"], "imported value was not persisted");
    Assert(Directory.GetFiles(fixture.Root, "settings.json.corrupt-*").Length == 1,
        "explicit repair did not preserve the malformed primary");
}

static void ExplicitImportPreservesValidBackup()
{
    using var fixture = new SettingsFixture();
    File.WriteAllText(fixture.Path, "{\"theme\":");
    var backupPath = SettingsStorePersistence.BackupPathFor(fixture.Path);
    WriteSettings(backupPath, new Dictionary<string, string> { ["theme"] = "safe" });

    var imported = new Dictionary<string, string> { ["theme"] = "light" };
    Assert(SettingsStorePersistence.TryRepairFromExplicitImport(fixture.Path, imported),
        "explicit import helper could not repair malformed primary");
    Equal("light", ReadSettings(fixture.Path)["theme"], "explicit import was not installed");
    Equal("safe", ReadSettings(backupPath)["theme"], "valid recovery backup was overwritten");
    Assert(Directory.GetFiles(fixture.Root, "settings.json.corrupt-*").Length == 1,
        "explicit import did not preserve the malformed primary");
}

static void WriteSettings(string path, IReadOnlyDictionary<string, string> values) =>
    File.WriteAllText(path, JsonSerializer.Serialize(values));

static Dictionary<string, string> ReadSettings(string path) =>
    JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
    ?? throw new InvalidOperationException($"Could not parse expected settings fixture '{path}'.");

static void AssertNoTemporaryFiles(string root) =>
    Assert(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any(),
        "temporary settings file was left behind");

static void Assert(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'");
}

sealed class SettingsFixture : IDisposable
{
    internal SettingsFixture()
    {
        Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WinForge.SettingsStore.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Path = System.IO.Path.Combine(Root, "settings.json");
    }

    internal string Root { get; }
    internal string Path { get; }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch { /* retain only an isolated temporary fixture if cleanup is interrupted */ }
    }
}
