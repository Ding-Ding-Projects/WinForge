using WinForge.Services;

var failures = new List<string>();
int passed = 0;

Run("Storage Sense accepts every supported policy value", StorageSenseSupported);
Run("Storage Sense rejects unsupported cadence", StorageSenseRejectsCadence);
Run("Storage Sense rejects unsupported retention", StorageSenseRejectsRetention);
Run("Filter Keys accepts the bounded 20-second edge", FilterKeysBoundedEdge);
Run("Filter Keys rejects oversized timings", FilterKeysRejectsOversized);
Run("Filter Keys enablement preserves every unrelated API flag", FilterKeysPreservesApiFlags);
Run("Filter Keys disablement clears only FILTERKEYSON", FilterKeysClearsOnlyOnFlag);
Run("Windows Update pause window is UTC and bounded", UpdatePauseWindowBounded);
Run("Windows Update rejects a pause beyond 35 days", UpdatePauseRejectsUnsupported);
Run("Windows Update timestamps use stable UTC format", UpdateTimestampStable);
Run("published driver identity is normalized", DriverIdentityNormalized);
Run("published driver identity rejects shell text", DriverIdentityRejectsInjection);
Run("driver export preserves a spaced folder as one argument", DriverExportArgumentVector);
Run("driver rollback never adds force or reboot", DriverRollbackIsConservative);
Run("driver restore uses recursive exported INF discovery", DriverRestoreContract);
Run("association export requires an absolute XML path", AssociationExportValidation);
Run("association import requires an existing XML file", AssociationImportValidation);
Run("ResetBase command is explicit and complete", ResetBaseContract);
Run("Store reset script binds one validated package", StoreResetContract);
Run("Store re-register script validates manifest presence", StoreReregisterContract);
Run("Store package identity rejects PowerShell text", StorePackageRejectsInjection);
Run("startup impact classifies every audited source", StartupImpactClassification);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} system-maintenance core tests (pure contracts; no host mutation)");
    return 0;
}

foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} system-maintenance core tests");
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

static void StorageSenseSupported()
{
    foreach (int cadence in SystemMaintenanceContracts.StorageCadenceDays)
    foreach (int recycle in SystemMaintenanceContracts.RetentionDays)
    foreach (int downloads in SystemMaintenanceContracts.RetentionDays)
        SystemMaintenanceContracts.ValidateStorageSense(new StorageSenseSettings(true, cadence, recycle, downloads));
}

static void StorageSenseRejectsCadence()
    => Throws<ArgumentOutOfRangeException>(() =>
        SystemMaintenanceContracts.ValidateStorageSense(new StorageSenseSettings(true, 2, 30, 0)));

static void StorageSenseRejectsRetention()
    => Throws<ArgumentOutOfRangeException>(() =>
        SystemMaintenanceContracts.ValidateStorageSense(new StorageSenseSettings(true, 7, 90, 0)));

static void FilterKeysBoundedEdge()
    => SystemMaintenanceContracts.ValidateFilterKeys(new FilterKeysSettings(true, 20_000, 20_000, 20_000, 20_000));

static void FilterKeysRejectsOversized()
    => Throws<ArgumentOutOfRangeException>(() =>
        SystemMaintenanceContracts.ValidateFilterKeys(new FilterKeysSettings(true, 20_001, 0, 0, 0)));

static void FilterKeysPreservesApiFlags()
{
    const uint existing = 0x0000007e;
    Equal(0x0000007fu, SystemMaintenanceContracts.FilterKeysFlagsWithEnabled(existing, true),
        "enabled FILTERKEYS flags");
}

static void FilterKeysClearsOnlyOnFlag()
{
    const uint existing = 0x0000007f;
    Equal(0x0000007eu, SystemMaintenanceContracts.FilterKeysFlagsWithEnabled(existing, false),
        "disabled FILTERKEYS flags");
}

static void UpdatePauseWindowBounded()
{
    var start = new DateTimeOffset(2026, 7, 24, 12, 30, 0, TimeSpan.FromHours(-4));
    UpdatePauseWindow window = SystemMaintenanceContracts.BuildUpdatePauseWindow(start, 35);
    Equal(TimeSpan.Zero, window.Start.Offset, "pause start offset");
    Equal(TimeSpan.FromDays(35), window.End - window.Start, "pause length");
}

static void UpdatePauseRejectsUnsupported()
    => Throws<ArgumentOutOfRangeException>(() =>
        SystemMaintenanceContracts.BuildUpdatePauseWindow(DateTimeOffset.UtcNow, 36));

static void UpdateTimestampStable()
    => Equal("2026-07-24T16:30:00Z",
        SystemMaintenanceContracts.FormatUpdateTimestamp(new DateTimeOffset(2026, 7, 24, 12, 30, 0, TimeSpan.FromHours(-4))),
        "Windows Update timestamp");

static void DriverIdentityNormalized()
    => Equal("oem42.inf", SystemMaintenanceContracts.RequirePublishedDriverName(" OEM42.INF "), "normalized identity");

static void DriverIdentityRejectsInjection()
    => Throws<ArgumentException>(() => SystemMaintenanceContracts.RequirePublishedDriverName("oem42.inf & whoami"));

static void DriverExportArgumentVector()
{
    using var fixture = new Fixture();
    IReadOnlyList<string> args = SystemMaintenanceContracts.DriverExportArguments("oem7.inf", fixture.Root);
    Equal(3, args.Count, "driver export argument count");
    Equal(fixture.Root, args[2], "spaced folder remains one argument");
}

static void DriverRollbackIsConservative()
{
    IReadOnlyList<string> args = SystemMaintenanceContracts.DriverRollbackArguments("oem9.inf");
    Equal("/delete-driver", args[0], "rollback verb");
    Assert(args.Contains("/uninstall"), "rollback must detach the selected package");
    Assert(!args.Any(a => a.Equals("/force", StringComparison.OrdinalIgnoreCase)), "rollback unexpectedly forces removal");
    Assert(!args.Any(a => a.Equals("/reboot", StringComparison.OrdinalIgnoreCase)), "rollback unexpectedly schedules reboot");
}

static void DriverRestoreContract()
{
    using var fixture = new Fixture();
    IReadOnlyList<string> args = SystemMaintenanceContracts.DriverRestoreArguments(fixture.Root);
    Assert(args[1].EndsWith("*.inf", StringComparison.OrdinalIgnoreCase), "restore wildcard");
    Assert(args.Contains("/subdirs") && args.Contains("/install"), "restore recursion/install switches");
}

static void AssociationExportValidation()
{
    Throws<ArgumentException>(() => SystemMaintenanceContracts.DismExportDefaultAssociationsArguments("relative.xml"));
    using var fixture = new Fixture();
    string target = Path.Combine(fixture.Root, "associations.xml");
    IReadOnlyList<string> args = SystemMaintenanceContracts.DismExportDefaultAssociationsArguments(target);
    Equal("/Export-DefaultAppAssociations:" + target, args[1], "export argument");
    Throws<ArgumentException>(() => SystemMaintenanceContracts.DismExportDefaultAssociationsArguments(Path.Combine(fixture.Root, "bad.txt")));
}

static void AssociationImportValidation()
{
    using var fixture = new Fixture();
    string path = Path.Combine(fixture.Root, "associations.xml");
    Throws<FileNotFoundException>(() => SystemMaintenanceContracts.DismImportDefaultAssociationsArguments(path));
    File.WriteAllText(path, "<DefaultAssociations />");
    IReadOnlyList<string> args = SystemMaintenanceContracts.DismImportDefaultAssociationsArguments(path);
    Equal("/Import-DefaultAppAssociations:" + path, args[1], "import argument");
}

static void ResetBaseContract()
{
    IReadOnlyList<string> args = SystemMaintenanceContracts.ResetBaseArguments();
    Equal("/Online|/Cleanup-Image|/StartComponentCleanup|/ResetBase", string.Join('|', args), "ResetBase arguments");
}

static void StoreResetContract()
{
    string script = SystemMaintenanceContracts.BuildStoreResetScript("Microsoft.WindowsCalculator");
    Assert(script.Contains("Get-AppxPackage -Name 'Microsoft.WindowsCalculator'", StringComparison.Ordinal), "selected package missing");
    Assert(script.Contains("Reset-AppxPackage -ErrorAction Stop", StringComparison.Ordinal), "reset command missing");
}

static void StoreReregisterContract()
{
    string script = SystemMaintenanceContracts.BuildStoreReregisterScript("Microsoft.WindowsCalculator");
    Assert(script.Contains("AppXManifest.xml", StringComparison.Ordinal), "manifest path missing");
    Assert(script.Contains("Test-Path -LiteralPath", StringComparison.Ordinal), "manifest validation missing");
    Assert(script.Contains("Add-AppxPackage -DisableDevelopmentMode -Register", StringComparison.Ordinal), "re-register command missing");
}

static void StorePackageRejectsInjection()
    => Throws<ArgumentException>(() => SystemMaintenanceContracts.BuildStoreResetScript("Microsoft.App'; whoami; #"));

static void StartupImpactClassification()
{
    Equal(StartupImpact.Critical, SystemMaintenanceContracts.EstimateStartupImpact(StartupAutorunSource.Winlogon), "Winlogon impact");
    Equal(StartupImpact.Critical, SystemMaintenanceContracts.EstimateStartupImpact(StartupAutorunSource.AppInit), "AppInit impact");
    Equal(StartupImpact.High, SystemMaintenanceContracts.EstimateStartupImpact(StartupAutorunSource.AutoStartService), "service impact");
    Equal(StartupImpact.High, SystemMaintenanceContracts.EstimateStartupImpact(StartupAutorunSource.BootTask), "boot task impact");
    Equal(StartupImpact.Medium, SystemMaintenanceContracts.EstimateStartupImpact(StartupAutorunSource.Run), "Run impact");
    Equal(StartupImpact.Medium, SystemMaintenanceContracts.EstimateStartupImpact(StartupAutorunSource.LogonTask), "logon task impact");
    Equal(StartupImpact.Low, SystemMaintenanceContracts.EstimateStartupImpact(StartupAutorunSource.StartupFolder), "folder impact");
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'");
}

sealed class Fixture : IDisposable
{
    internal Fixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "WinForge System Maintenance Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    internal string Root { get; }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch { }
    }
}
