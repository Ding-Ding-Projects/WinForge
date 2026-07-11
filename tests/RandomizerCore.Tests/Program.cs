using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("full Int32 range remains reachable and unique", FullIntRange);
Run("dice overflow is rejected before rolling", DiceOverflowRejected);
Run("minimum Int32 modifier remains representable", MinimumModifierIsSafe);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} Randomizer core tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} Randomizer core tests");
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

static void FullIntRange()
{
    var values = RandomizerService.Integers(int.MinValue, int.MaxValue, 512, unique: true);
    Equal(512, values.Count, "full-range result count");
    Equal(values.Count, values.Distinct().Count(), "full-range uniqueness");
    Assert(values.All(value => value >= int.MinValue && value <= int.MaxValue), "full-range bound");
    Assert(values.Any(value => value < 0), "negative half was not reachable");
    Assert(values.Any(value => value >= 0), "non-negative half was not reachable");
}

static void DiceOverflowRejected()
{
    var result = RandomizerService.RollDice("1d6+2147483647");
    Assert(!result.Ok, "overflowing modifier was accepted");
    Equal("bad", result.Error ?? "", "overflow error hint");
    Equal(0, result.Rolls.Count, "overflowing notation rolled before validation");
}

static void MinimumModifierIsSafe()
{
    var result = RandomizerService.RollDice("1d6-2147483648");
    Assert(result.Ok, "safe minimum modifier was rejected");
    Assert(result.Total >= int.MinValue && result.Total <= int.MaxValue, "minimum modifier total overflowed");
    Assert(result.Total >= -2_147_483_647 && result.Total <= -2_147_483_642,
        "minimum modifier total is outside the possible dice range");
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
