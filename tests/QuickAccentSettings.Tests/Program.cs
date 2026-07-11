using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("null selected-set list falls back to ALL", NullListFallsBack);
Run("null entries and duplicates normalize safely", NullEntriesNormalize);
Run("invalid persisted enum and delay values normalize", InvalidValuesNormalize);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} Quick Accent settings tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} Quick Accent settings tests");
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

static void NullListFallsBack()
{
    var normalized = QuickAccentService.NormalizeSettings(new QuickAccentSettings
    {
        SelectedSets = null!,
    });

    Same(new[] { "ALL" }, normalized.SelectedSets, "null list fallback");
}

static void NullEntriesNormalize()
{
    var normalized = QuickAccentService.NormalizeSettings(new QuickAccentSettings
    {
        SelectedSets = new List<string> { null!, "", "  Latin  ", "latin" },
    });

    Same(new[] { "Latin" }, normalized.SelectedSets, "set normalization");
}

static void InvalidValuesNormalize()
{
    var normalized = QuickAccentService.NormalizeSettings(new QuickAccentSettings
    {
        ActivationKey = (QuickAccentActivationKey)99,
        Position = (QuickAccentPosition)(-1),
        InputDelayMs = 99_999,
    });

    Equal(QuickAccentActivationKey.Both, normalized.ActivationKey, "activation fallback");
    Equal(QuickAccentPosition.Caret, normalized.Position, "position fallback");
    Equal(2_000, normalized.InputDelayMs, "delay cap");
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
