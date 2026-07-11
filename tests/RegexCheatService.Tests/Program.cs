using System.Text.RegularExpressions;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("quantifier catalog uses the .NET atomic possessive equivalent", QuantifierCatalogUsesDotNetAtomicSyntax);
Run("ready-made recipes compile with the .NET regex parser", ReadyMadeRecipesCompile);
Run("filter keeps the atomic entry discoverable", AtomicEntryRemainsDiscoverable);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} regex-cheatsheet integrity tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} regex-cheatsheet integrity tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static void QuantifierCatalogUsesDotNetAtomicSyntax()
{
    var quantifiers = RegexCheatService.Filter(query: null, categoryEn: "Quantifiers");
    Assert(quantifiers.Count > 0, "quantifier catalog is empty");
    Assert(!quantifiers.Any(entry => entry.Token == "*+" || entry.Example.Contains("a*+", StringComparison.Ordinal)),
        "catalog still advertises the unsupported .NET possessive '*+' syntax");

    var atomic = Single(quantifiers, entry => entry.Token == "(?>a*)");
    Assert(atomic.DescEn.Contains(".NET equivalent", StringComparison.Ordinal),
        "atomic entry does not explain the .NET equivalent");
    Assert(atomic.DescZh.Contains(".NET", StringComparison.Ordinal),
        "atomic entry is missing its Cantonese .NET explanation");
    Assert(atomic.Example == "(?>a*)", "atomic entry example diverged from the supported pattern");

    _ = new Regex(atomic.Token);
}

static void ReadyMadeRecipesCompile()
{
    Assert(RegexCheatService.Recipes.Count > 0, "recipe catalog is empty");
    foreach (var recipe in RegexCheatService.Recipes)
    {
        try { _ = new Regex(recipe.Pattern); }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"recipe '{recipe.Name}' does not compile: {ex.Message}", ex);
        }
    }
}

static void AtomicEntryRemainsDiscoverable()
{
    var results = RegexCheatService.Filter(query: "atomic", categoryEn: null);
    Assert(results.Any(entry => entry.Token == "(?>a*)"), "atomic quantifier entry is not searchable");
}

static T Single<T>(IReadOnlyList<T> items, Func<T, bool> predicate)
{
    var matches = items.Where(predicate).ToList();
    if (matches.Count != 1) throw new InvalidOperationException($"expected one matching entry, found {matches.Count}");
    return matches[0];
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
