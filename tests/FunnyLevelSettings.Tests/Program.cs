using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("defaults and malformed persisted values stay within 1..5", DefaultsAndMalformedValues);
Run("English and Cantonese levels persist independently", IndependentPersistence);
Run("all three language modes resolve the selected authored variants", LanguageModeResolution);
Run("reload applies imported values once", ReloadImportedValues);
Run("out-of-range assignments fail without persistence", RejectOutOfRangeAssignments);
Run("ordinary safety-sensitive localization is tone-independent", OrdinaryLocalizationIsUnchanged);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} funny-level settings tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} funny-level settings tests");
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

static void DefaultsAndMalformedValues()
{
    var values = new Dictionary<string, string>
    {
        [FunnyLevelSettings.EnglishSettingKey] = "0",
        [FunnyLevelSettings.CantoneseSettingKey] = "definitely-not-a-level",
    };
    var settings = Create(values);

    Equal(FunnyLevelSettings.DefaultEnglishLevel, settings.EnglishLevel, "English fallback");
    Equal(FunnyLevelSettings.DefaultCantoneseLevel, settings.CantoneseLevel, "Cantonese fallback");
    Assert(settings.EnglishLevel is >= 1 and <= 5, "English fallback escaped the supported range");
    Assert(settings.CantoneseLevel is >= 1 and <= 5, "Cantonese fallback escaped the supported range");
}

static void IndependentPersistence()
{
    var values = new Dictionary<string, string>();
    var settings = Create(values);
    var events = 0;
    settings.Changed += (_, _) => events++;

    settings.EnglishLevel = 5;
    Equal("5", values[FunnyLevelSettings.EnglishSettingKey], "English persisted value");
    Assert(!values.ContainsKey(FunnyLevelSettings.CantoneseSettingKey), "English change wrote Cantonese state");
    Equal(FunnyLevelSettings.DefaultCantoneseLevel, settings.CantoneseLevel, "Cantonese level changed with English");
    Equal(1, events, "English change event count");

    settings.CantoneseLevel = 1;
    Equal("1", values[FunnyLevelSettings.CantoneseSettingKey], "Cantonese persisted value");
    Equal(5, settings.EnglishLevel, "English level changed with Cantonese");
    Equal(2, events, "independent change event count");

    settings.CantoneseLevel = 1;
    Equal(2, events, "no-op assignment raised an event");
}

static void LanguageModeResolution()
{
    var values = new Dictionary<string, string>
    {
        [FunnyLevelSettings.EnglishSettingKey] = "5",
        [FunnyLevelSettings.CantoneseSettingKey] = "1",
    };
    var settings = Create(values);
    var copy = PlayfulCopy.DashboardHero;

    Equal(copy.EnglishAt(5), settings.Pick(copy, AppLanguage.English), "English-only resolution");
    Equal(copy.CantoneseAt(1), settings.Pick(copy, AppLanguage.Cantonese), "Cantonese-only resolution");
    Equal(Loc.Both(copy.EnglishAt(5), copy.CantoneseAt(1)),
        settings.Pick(copy, AppLanguage.Bilingual), "bilingual resolution");

    for (var level = 1; level <= 5; level++)
    {
        settings.EnglishLevel = level;
        settings.CantoneseLevel = level;
        Equal(copy.EnglishAt(level), settings.English(copy), $"English level {level}");
        Equal(copy.CantoneseAt(level), settings.Cantonese(copy), $"Cantonese level {level}");
    }
}

static void ReloadImportedValues()
{
    var values = new Dictionary<string, string>();
    var settings = Create(values);
    var events = 0;
    settings.Changed += (_, _) => events++;

    values[FunnyLevelSettings.EnglishSettingKey] = "4";
    values[FunnyLevelSettings.CantoneseSettingKey] = "5";
    settings.ReloadFromSettings();
    Equal(4, settings.EnglishLevel, "imported English level");
    Equal(5, settings.CantoneseLevel, "imported Cantonese level");
    Equal(1, events, "reload event count");

    settings.ReloadFromSettings();
    Equal(1, events, "unchanged reload raised another event");
}

static void RejectOutOfRangeAssignments()
{
    var values = new Dictionary<string, string>();
    var settings = Create(values);

    Throws<ArgumentOutOfRangeException>(() => settings.EnglishLevel = 0, "English level 0");
    Throws<ArgumentOutOfRangeException>(() => settings.CantoneseLevel = 6, "Cantonese level 6");
    Assert(values.Count == 0, "invalid values were persisted");
}

static void OrdinaryLocalizationIsUnchanged()
{
    var values = new Dictionary<string, string>();
    var settings = Create(values);
    var safetyCopy = new LocalizedText(
        "Permanently delete this item?",
        "要永久刪除呢個項目？");

    var beforeEnglish = safetyCopy.Get(AppLanguage.English);
    var beforeCantonese = safetyCopy.Get(AppLanguage.Cantonese);
    settings.EnglishLevel = 5;
    settings.CantoneseLevel = 5;

    Equal(beforeEnglish, safetyCopy.Get(AppLanguage.English), "ordinary English copy changed");
    Equal(beforeCantonese, safetyCopy.Get(AppLanguage.Cantonese), "ordinary Cantonese copy changed");
}

static FunnyLevelSettings Create(Dictionary<string, string> values) => new(
    (key, fallback) => values.TryGetValue(key, out var value) ? value : fallback,
    (key, value) => values[key] = value);

static void Throws<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{message} did not throw {typeof(TException).Name}");
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
