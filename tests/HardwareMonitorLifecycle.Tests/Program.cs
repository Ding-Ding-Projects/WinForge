using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("overlapping monitor and battery leases share one driver until final release", OverlappingLeases);
Run("app shutdown closes an active driver once and rejects stale leases", ShutdownClosesOnce);
Run("failed driver open closes its partial candidate and later acquisition retries", FailedOpenIsCleanedAndRetried);
Run("lease disposal is idempotent", LeaseDisposalIsIdempotent);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} hardware-monitor lifecycle tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} hardware-monitor lifecycle tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static void OverlappingLeases()
{
    var drivers = new List<FakeDriver>();
    var coordinator = NewCoordinator(drivers);

    var monitor = RequireLease(coordinator.Acquire(), "System Monitor did not acquire its lease");
    var battery = RequireLease(coordinator.Acquire(), "Battery & Thermal did not acquire its lease");

    Equal(1, drivers.Count, "driver instance count");
    Equal(1, drivers[0].OpenCount, "driver open count");
    monitor.Dispose();
    Equal(0, drivers[0].CloseCount, "driver closed while Battery & Thermal still held a lease");

    bool used = battery.TryUse(driver => driver.UseCount++);
    Assert(used, "remaining Battery & Thermal lease could not sample the shared driver");
    Equal(1, drivers[0].UseCount, "driver sample count");

    battery.Dispose();
    Equal(1, drivers[0].CloseCount, "final page release did not close the driver");
}

static void ShutdownClosesOnce()
{
    var drivers = new List<FakeDriver>();
    var coordinator = NewCoordinator(drivers);
    var monitor = RequireLease(coordinator.Acquire(), "System Monitor did not acquire its lease");

    coordinator.Shutdown();
    Equal(1, drivers[0].CloseCount, "shutdown did not close the active driver");
    Assert(!monitor.TryUse(_ => throw new InvalidOperationException("stale lease was used")),
        "stale lease remained usable after shutdown");
    monitor.Dispose();
    coordinator.Shutdown();
    Equal(1, drivers[0].CloseCount, "shutdown or stale disposal closed the driver twice");
    Assert(coordinator.Acquire() is null, "shutdown coordinator reopened a driver");
}

static void FailedOpenIsCleanedAndRetried()
{
    var drivers = new List<FakeDriver>();
    int attempt = 0;
    var coordinator = new ResourceLeaseCoordinator<FakeDriver>(
        () =>
        {
            var driver = new FakeDriver { ThrowOnOpen = attempt++ == 0 };
            drivers.Add(driver);
            return driver;
        },
        static driver => driver.Open(),
        static driver => driver.Close());

    Assert(coordinator.Acquire() is null, "failing open handed out a lease");
    Equal(1, drivers.Count, "first candidate count");
    Equal(1, drivers[0].OpenCount, "failed candidate open count");
    Equal(1, drivers[0].CloseCount, "failed candidate was not closed");

    var retry = RequireLease(coordinator.Acquire(), "second open was not retried");
    Equal(2, drivers.Count, "retry candidate count");
    Equal(1, drivers[1].OpenCount, "retry open count");
    retry.Dispose();
    Equal(1, drivers[1].CloseCount, "successful retry was not closed on release");
}

static void LeaseDisposalIsIdempotent()
{
    var drivers = new List<FakeDriver>();
    var coordinator = NewCoordinator(drivers);
    var lease = RequireLease(coordinator.Acquire(), "lease acquisition failed");

    lease.Dispose();
    lease.Dispose();
    Equal(1, drivers[0].CloseCount, "double dispose closed the driver more than once");
    Assert(!lease.TryUse(_ => throw new InvalidOperationException("released lease was used")),
        "released lease was still usable");
}

static ResourceLeaseCoordinator<FakeDriver> NewCoordinator(List<FakeDriver> drivers) => new(
    () =>
    {
        var driver = new FakeDriver();
        drivers.Add(driver);
        return driver;
    },
    static driver => driver.Open(),
    static driver => driver.Close());

static ResourceLeaseCoordinator<FakeDriver>.Lease RequireLease(
    ResourceLeaseCoordinator<FakeDriver>.Lease? lease, string message) => lease ?? throw new InvalidOperationException(message);

static void Equal<T>(T expected, T actual, string label) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FakeDriver
{
    public bool ThrowOnOpen { get; init; }
    public int OpenCount { get; private set; }
    public int CloseCount { get; private set; }
    public int UseCount { get; set; }

    public void Open()
    {
        OpenCount++;
        if (ThrowOnOpen) throw new InvalidOperationException("simulated partial driver open failure");
    }

    public void Close() => CloseCount++;
}
