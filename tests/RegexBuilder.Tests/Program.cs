using System.Text.RegularExpressions;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("literal builder escapes .NET metacharacters", LiteralEscapes);
Run("character class escapes class metacharacters", CharacterClassEscapes);
Run("guided pieces cover anchors groups alternation and quantifiers", GuidedPieces);
Run("invalid builder input is rejected", InvalidBuilderInput);
Run("valid captures and Unicode are reported", ValidCapturesAndUnicode);
Run("invalid patterns report syntax feedback", InvalidPattern);
Run("no-match evaluation stays successful", NoMatch);
Run("multiline option changes anchor behavior", Multiline);
Run("zero-width matches terminate safely", ZeroWidth);
Run("match results are capped", ResultCap);
Run("pattern and sample limits are enforced", SizeLimits);
Run("adversarial backtracking times out", AdversarialTimeout);
Run("plain text remains default and differs from regex", PlainVersusRegex);
Run("case sensitivity is an explicit synchronized flag", CaseSensitivity);
Run("ignore-pattern-whitespace flag reaches the matcher", IgnorePatternWhitespace);
Run("compiled matcher evaluates multiple candidate fields", MultipleCandidates);
Run("compiled matcher reports invalid syntax before filtering", InvalidCompiledMatcher);
Run("search candidate limits are enforced", SearchCandidateLimit);
Run("search timeout fails closed for the rest of a batch", SearchTimeoutFailsClosed);
Run("search session synchronizes query mode and flags", SessionSynchronization);
Run("search session applies a complete state atomically", SessionApply);
Run("search session preview reports live captures", SessionPreviewCaptures);
Run("shared filter keeps plain and regex semantics distinct", SharedFilter);
Run("changelog filter consumes the complete shared search spec", ChangelogSpec);
Run("Dashboard uses the synchronized search control", DashboardSurface);
Run("Category uses the synchronized search control", CategorySurface);
Run("Search Results uses the synchronized search control", SearchResultsSurface);
Run("Manual uses the synchronized search control", ManualSurface);
Run("App Launcher uses the synchronized search control", AppLauncherSurface);
Run("Licenses uses the synchronized search control", LicensesSurface);
Run("Native OSS Hub uses the synchronized search control", OpenSourceHubSurface);
Run("Settings Hub uses the synchronized search control", SettingsHubSurface);
Run("Command Palette uses the synchronized search control", CommandPaletteSurface);
Run("shared control exposes the full builder contract", FullBuilderSurface);
Run("all candidate XAML search/query controls are classified", ClassifiedInventory);
Run("current XAML and code-built search surfaces are explicitly inventoried", CurrentInventory);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} regex-builder safety tests");
    return 0;
}

foreach (string failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} regex-builder safety tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static void LiteralEscapes()
{
    var piece = RegexBuilderService.Build(RegexBuilderService.PieceKind.Literal, "a.b+$");
    Assert(piece.Ok, "literal piece failed");
    Assert(piece.Token == @"a\.b\+\$", $"unexpected token: {piece.Token}");
    Assert(Regex.IsMatch("a.b+$", piece.Token), "escaped token does not match the literal");
}

static void CharacterClassEscapes()
{
    var piece = RegexBuilderService.Build(RegexBuilderService.PieceKind.CharacterClass, "a-]^", option: true);
    Assert(piece.Ok, "character-class piece failed");
    Assert(piece.Token == @"[^a\-\]\^]", $"unexpected class: {piece.Token}");
    _ = new Regex(piece.Token);
}

static void GuidedPieces()
{
    Assert(RegexBuilderService.Build(RegexBuilderService.PieceKind.Anchor, null,
        anchor: RegexBuilderService.AnchorKind.StartOfString).Token == @"\A", "anchor mismatch");
    Assert(RegexBuilderService.Build(RegexBuilderService.PieceKind.Group, @"\w+", "word").Token == @"(?<word>\w+)",
        "named group mismatch");
    Assert(RegexBuilderService.Build(RegexBuilderService.PieceKind.Alternation, "cat", "dog").Token == "(?:cat|dog)",
        "alternation mismatch");
    Assert(RegexBuilderService.Build(RegexBuilderService.PieceKind.Quantifier, @"\d", "2,5").Token == @"(?:\d){2,5}",
        "quantifier mismatch");
}

static void InvalidBuilderInput()
{
    Assert(!RegexBuilderService.Build(RegexBuilderService.PieceKind.Group, "x", "bad-name").Ok,
        "invalid group name passed");
    Assert(!RegexBuilderService.Build(RegexBuilderService.PieceKind.Alternation, "x", "").Ok,
        "missing alternation branch passed");
    Assert(!RegexBuilderService.Build(RegexBuilderService.PieceKind.Quantifier, "x", "5,2").Ok,
        "descending quantifier passed");
}

static void ValidCapturesAndUnicode()
{
    var result = RegexTesterService.Evaluate(@"(?<word>\p{L}+)", "你好 café", "$1", RegexOptions.None);
    Assert(result.Ok, result.Error ?? "evaluation failed");
    Assert(result.Matches.Count == 2, "expected two Unicode words");
    Assert(result.Matches.All(match => match.Groups.Count == 1), "capture groups were not reported");
}

static void InvalidPattern()
{
    var result = RegexTesterService.Evaluate("(", "text", "", RegexOptions.None);
    Assert(!result.Ok && !string.IsNullOrWhiteSpace(result.Error), "invalid syntax was accepted");
}

static void NoMatch()
{
    var result = RegexTesterService.Evaluate("z+", "abc", "", RegexOptions.None);
    Assert(result.Ok && result.Matches.Count == 0, "no-match result is incorrect");
}

static void Multiline()
{
    var without = RegexTesterService.Evaluate("^two$", "one\ntwo", "", RegexOptions.None);
    var with = RegexTesterService.Evaluate("^two$", "one\ntwo", "", RegexOptions.Multiline);
    Assert(without.Matches.Count == 0 && with.Matches.Count == 1, "multiline anchors did not change behavior");
}

static void ZeroWidth()
{
    var result = RegexTesterService.Evaluate(@"(?=a)", "aaa", "", RegexOptions.None);
    Assert(result.Ok && result.Matches.Count == 3, "zero-width iteration did not terminate correctly");
    Assert(result.Matches.All(match => match.Length == 0), "zero-width lengths are wrong");
}

static void ResultCap()
{
    string input = new('a', RegexTesterService.MaxMatches + 10);
    var result = RegexTesterService.Evaluate("a", input, "", RegexOptions.None);
    Assert(result.Ok && result.Matches.Count == RegexTesterService.MaxMatches && result.MatchesTruncated,
        "match cap was not enforced");
}

static void SizeLimits()
{
    var pattern = RegexTesterService.Evaluate(new string('x', RegexTesterService.MaxPatternLength + 1), "", "", RegexOptions.None);
    var sample = RegexTesterService.Evaluate("x", new string('x', RegexTesterService.MaxInputLength + 1), "", RegexOptions.None);
    Assert(!pattern.Ok && !sample.Ok, "pattern or sample limit was not enforced");
}

static void AdversarialTimeout()
{
    string input = new string('a', 50_000) + "!";
    var result = RegexTesterService.Evaluate("^(a+)+$", input, "", RegexOptions.None);
    Assert(!result.Ok && result.Error?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true,
        "adversarial pattern did not time out");
}

static void PlainVersusRegex()
{
    var plain = SearchPatternService.Match("abc", new SearchPatternService.Spec("a.c"));
    var regex = SearchPatternService.Match("abc", new SearchPatternService.Spec("a.c", UseRegex: true));
    Assert(plain.Ok && !plain.IsMatch, "plain text should treat dot literally");
    Assert(regex.Ok && regex.IsMatch, "regex mode should treat dot as a metacharacter");
}

static void CaseSensitivity()
{
    var insensitive = SearchPatternService.Match("WinForge", new SearchPatternService.Spec("winforge"));
    var sensitive = SearchPatternService.Match("WinForge", new SearchPatternService.Spec("winforge", IgnoreCase: false));
    Assert(insensitive.Ok && insensitive.IsMatch, "default case-insensitive search failed");
    Assert(sensitive.Ok && !sensitive.IsMatch, "case-sensitive flag was ignored");
}

static void IgnorePatternWhitespace()
{
    var compact = SearchPatternService.Match("ab", new SearchPatternService.Spec("a b", UseRegex: true));
    var ignored = SearchPatternService.Match("ab", new SearchPatternService.Spec(
        "a b", UseRegex: true, IgnorePatternWhitespace: true));
    Assert(compact.Ok && !compact.IsMatch, "whitespace should be literal without the flag");
    Assert(ignored.Ok && ignored.IsMatch, "ignore-pattern-whitespace flag was not applied");
}

static void MultipleCandidates()
{
    var matcher = SearchPatternService.Compile(new SearchPatternService.Spec(@"^reactor$", UseRegex: true));
    var result = matcher.MatchAny(new string?[] { "dashboard", null, "reactor" });
    Assert(result.Ok && result.IsMatch, "matcher did not search all candidate fields");
}

static void InvalidCompiledMatcher()
{
    var matcher = SearchPatternService.Compile(new SearchPatternService.Spec("(", UseRegex: true));
    var result = matcher.Match("anything");
    Assert(!matcher.Ok && !result.Ok && !string.IsNullOrWhiteSpace(matcher.Error),
        "invalid syntax was not reported during compilation");
}

static void SearchCandidateLimit()
{
    var result = SearchPatternService.Match(
        new string('x', RegexTesterService.MaxInputLength + 1),
        new SearchPatternService.Spec("x", UseRegex: true));
    Assert(!result.Ok && result.Error?.Contains("candidate", StringComparison.OrdinalIgnoreCase) == true,
        "oversized candidate was accepted");
}

static void SearchTimeoutFailsClosed()
{
    var matcher = SearchPatternService.Compile(new SearchPatternService.Spec("^(a+)+$", UseRegex: true));
    var first = matcher.Match(new string('a', 50_000) + "!");
    var started = DateTime.UtcNow;
    var second = matcher.Match("safe");
    var elapsed = DateTime.UtcNow - started;
    Assert(!first.Ok && !second.Ok, "matcher retried after a timeout");
    Assert(elapsed < TimeSpan.FromMilliseconds(100), "failed matcher did not fail closed immediately");
}

static void SessionSynchronization()
{
    var session = new SearchPatternSession();
    int changes = 0;
    session.Changed += (_, _) => changes++;
    session.Query = @"^WinForge$";
    session.UseRegex = true;
    session.IgnoreCase = false;
    session.Multiline = true;
    Assert(changes == 4, $"expected four session changes, got {changes}");
    Assert(session.Spec.Query == @"^WinForge$" && session.Spec.UseRegex && !session.Spec.IgnoreCase
        && session.Spec.Multiline, "session state did not flow into the search spec");
}

static void SessionApply()
{
    var session = new SearchPatternSession();
    int changes = 0;
    session.Changed += (_, _) => changes++;
    session.Apply(new SearchPatternService.Spec(
        @"(?<word>\w+)", true, false, true, true, true, true));
    Assert(changes == 1, "atomic apply should publish one change");
    Assert(session.Spec is { UseRegex: true, IgnoreCase: false, Multiline: true, Singleline: true,
        IgnorePatternWhitespace: true, ExplicitCapture: true }, "not every flag synchronized");
}

static void SessionPreviewCaptures()
{
    var session = new SearchPatternSession();
    session.Apply(new SearchPatternService.Spec(@"(?<word>\p{L}+)", UseRegex: true));
    var result = session.Preview("你好 WinForge");
    Assert(result.Ok && result.Matches.Count == 2 && result.Matches.All(m => m.Groups.Count == 1),
        "session preview did not expose Unicode capture groups");
}

static void SharedFilter()
{
    string[] source = ["a.c", "abc", "zzz"];
    var plain = SearchPatternService.Filter(source, value => value, new SearchPatternService.Spec("a.c")).ToArray();
    var regex = SearchPatternService.Filter(source, value => value,
        new SearchPatternService.Spec("a.c", UseRegex: true)).ToArray();
    Assert(plain.SequenceEqual(new[] { "a.c" }), "plain filter treated metacharacters as regex");
    Assert(regex.SequenceEqual(new[] { "a.c", "abc" }), "regex filter did not use regex semantics");
}

static void ChangelogSpec()
{
    var entries = new[]
    {
        new ChangelogService.Entry("Release one", "alpha\nbeta", null, "abcdef1", "test"),
        new ChangelogService.Entry("Release two", "BETA", null, "abcdef2", "test"),
    };

    var multiline = new SearchPatternService.Spec(
        "^beta$",
        UseRegex: true,
        IgnoreCase: false,
        Multiline: true);
    IReadOnlyList<ChangelogService.Entry> filtered = ChangelogService.Filter(
        entries, multiline, null, null, out string? error);
    Assert(error is null && filtered.Count == 1 && filtered[0].Heading == "Release one",
        "changelog filtering did not honor the complete regex spec");

    var invalid = new SearchPatternService.Spec("(", UseRegex: true);
    _ = ChangelogService.Filter(entries, invalid, null, null, out error);
    Assert(!string.IsNullOrWhiteSpace(error), "invalid changelog pattern was accepted");
}

static void DashboardSurface() => SurfaceContract(
    "Pages/DashboardPage.xaml", "Pages/DashboardPage.xaml.cs", "SearchBox_PatternChanged", "SearchPatternService.Filter");

static void CategorySurface() => SurfaceContract(
    "Pages/CategoryPage.xaml", "Pages/CategoryPage.xaml.cs", "FilterBox_PatternChanged", "FilterBox.Spec");

static void SearchResultsSurface() => SurfaceContract(
    "Pages/SearchResultsPage.xaml", "Pages/SearchResultsPage.xaml.cs", "SearchBox_PatternChanged", "SearchBox.Spec");

static void ManualSurface() => SurfaceContract(
    "Pages/ManualPage.xaml", "Pages/ManualPage.xaml.cs", "FilterBox_PatternChanged", "ManualHits()");

static void AppLauncherSurface() => SurfaceContract(
    "Pages/AppLauncherModule.xaml", "Pages/AppLauncherModule.xaml.cs", "SearchBox_PatternChanged", "CompileMatcher()");

static void LicensesSurface() => SurfaceContract(
    "Pages/LicensesPage.xaml", "Pages/LicensesPage.xaml.cs", "SearchBox_PatternChanged", "LicenseCatalogService.Search(SearchBox.Spec");

static void OpenSourceHubSurface() => SurfaceContract(
    "Pages/OpenSourceAppHubModule.xaml", "Pages/OpenSourceAppHubModule.xaml.cs", "SearchBox_PatternChanged", "CompileMatcher()");

static void SettingsHubSurface() => SurfaceContract(
    "Pages/SettingsHubModule.xaml", "Pages/SettingsHubModule.xaml.cs", "FilterBox_PatternChanged", "Apply(FilterBox.Spec)");

static void CommandPaletteSurface()
{
    string source = ReadRepo("Services/CommandPaletteWindow.cs");
    Assert(source.Contains("private readonly SearchPatternBox _search", StringComparison.Ordinal),
        "command palette reverted to a plain search field");
    Assert(source.Contains("_search.PatternChanged", StringComparison.Ordinal),
        "command palette does not refresh from synchronized pattern state");
    Assert(source.Contains("_search.QuerySubmitted", StringComparison.Ordinal),
        "command palette has no query-only Enter activation");
    Assert(source.Contains("_search.CompileMatcher", StringComparison.Ordinal)
        && source.Contains("matcher.MatchAny", StringComparison.Ordinal),
        "command palette does not apply the complete bounded matcher to results");
    Assert(source.Contains("_searchError", StringComparison.Ordinal)
        && source.Contains("Search error:", StringComparison.Ordinal)
        && source.Contains("No results", StringComparison.Ordinal),
        "command palette does not expose error and no-result status");
    Assert(source.Contains("_search.FocusQuery()", StringComparison.Ordinal),
        "command palette does not focus the real query editor");
    Assert(source.Contains("SetAutomationId(_search, \"CommandPaletteSearchBox\")", StringComparison.Ordinal),
        "command palette search has no stable automation ID");
    Assert(source.Contains("AutomationNameProvider = () => Loc.I.Pick(", StringComparison.Ordinal)
        && source.Contains("Loc.I.LanguageChanged += OnLanguageChanged", StringComparison.Ordinal),
        "command palette does not refresh localized accessible names");
    int keyStart = source.IndexOf("private void OnSearchKeyDown", StringComparison.Ordinal);
    int keyEnd = source.IndexOf("private void OnListKeyDown", keyStart, StringComparison.Ordinal);
    Assert(keyStart >= 0 && keyEnd > keyStart
        && !source[keyStart..keyEnd].Contains("VirtualKey.Enter", StringComparison.Ordinal),
        "command palette handles Enter on the composite wrapper instead of the real query editor");
}

static void FullBuilderSurface()
{
    string xaml = ReadRepo("Controls/SearchPatternBox.xaml");
    string code = ReadRepo("Controls/SearchPatternBox.xaml.cs");
    foreach (string marker in new[]
    {
        "SearchPatternRawPattern", "IgnoreCaseCheck", "MultilineCheck", "SinglelineCheck",
        "IgnoreWhitespaceCheck", "ExplicitCaptureCheck", "GuidedExpander", "SearchPatternSample",
        "PreviewText", "CopyPatternButton",
    }) Assert(xaml.Contains(marker, StringComparison.Ordinal), $"builder XAML is missing {marker}");
    foreach (string marker in new[]
    {
        "PieceKind.Literal", "PieceKind.CharacterClass", "PieceKind.Anchor", "PieceKind.Group",
        "PieceKind.Alternation", "PieceKind.Quantifier", "SearchPatternSession", "CompileMatcher",
    }) Assert(code.Contains(marker, StringComparison.Ordinal), $"builder code is missing {marker}");
}

static void ClassifiedInventory()
{
    string csv = ReadRepo("docs/audits/search-surface-inventory-2026-07-24.csv");
    string[] rows = csv.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
    Assert(rows.Length == 103, $"expected a header plus 102 classified controls, got {rows.Length}");
    Assert(rows.Count(row => row.Contains(",\"integrated-core\",", StringComparison.Ordinal)) == 15, "integrated inventory count mismatch");
    Assert(rows.Count(row => row.Contains(",\"plain-text-later\",", StringComparison.Ordinal)) == 66, "remaining plain-text count mismatch");
    Assert(rows.Count(row => row.Contains(",\"specialized-dialect\",", StringComparison.Ordinal)) == 9, "specialized dialect count mismatch");
    Assert(rows.Count(row => row.Contains(",\"dedicated-pattern-tool\",", StringComparison.Ordinal)) == 7, "dedicated pattern count mismatch");
    Assert(rows.Count(row => row.Contains(",\"read-only-output\",", StringComparison.Ordinal)) == 2, "read-only output count mismatch");
    Assert(rows.Count(row => row.Contains(",\"shared-control-internal\",", StringComparison.Ordinal)) == 3, "shared control internal count mismatch");
    Assert(csv.Contains("BPF capture-filter dialect", StringComparison.Ordinal)
        && csv.Contains("AWS/JMESPath", StringComparison.Ordinal)
        && csv.Contains("Rename transformation input", StringComparison.Ordinal),
        "required specialized-dialect boundaries are missing");
}

static void CurrentInventory()
{
    string csv = ReadRepo("docs/audits/search-surface-inventory-2026-07-24.csv");
    var required = new (string Source, string Control)[]
    {
        ("Pages/DashboardPage.xaml", "SearchBox"),
        ("Pages/CategoryPage.xaml", "FilterBox"),
        ("Pages/SearchResultsPage.xaml", "SearchBox"),
        ("Pages/ManualPage.xaml", "FilterBox"),
        ("Pages/AppLauncherModule.xaml", "SearchBox"),
        ("Pages/LicensesPage.xaml", "SearchBox"),
        ("Pages/OpenSourceAppHubModule.xaml", "SearchBox"),
        ("Pages/SettingsHubModule.xaml", "FilterBox"),
        ("Pages/OfflineDocsPage.xaml", "SearchBox"),
        ("Pages/SupportTicketsPage.xaml", "TicketSearchBox"),
        ("Pages/TotpModule.xaml", "EntrySearchBox"),
        ("Pages/AboutPage.xaml.cs", "search"),
        ("Pages/SettingsPage.xaml.cs", "search"),
        ("MainWindow.xaml.cs", "NewTabPickerSearchBox"),
        ("Services/CommandPaletteWindow.cs", "_search"),
        ("Pages/BitwardenConnectionView.cs", "_searchBox"),
        ("Pages/PdfToolkitModule.Viewer.cs", "_viewerSearchBox"),
    };

    foreach (var surface in required)
    {
        string marker = $"\"{surface.Source}#{surface.Control}\",\"{surface.Source}\",";
        Assert(csv.Contains(marker, StringComparison.Ordinal), $"inventory is missing {surface.Source}#{surface.Control}");
    }
}

static void SurfaceContract(string xamlPath, string codePath, string eventMarker, string matcherMarker)
{
    string xaml = ReadRepo(xamlPath);
    string code = ReadRepo(codePath);
    Assert(xaml.Contains("<controls:SearchPatternBox", StringComparison.Ordinal),
        $"{xamlPath} does not expose the shared control");
    Assert(xaml.Contains("AutomationName=", StringComparison.Ordinal),
        $"{xamlPath} has no accessible search name");
    Assert(xaml.Contains(eventMarker, StringComparison.Ordinal),
        $"{xamlPath} does not bind the synchronized change event");
    Assert(code.Contains(eventMarker, StringComparison.Ordinal),
        $"{codePath} does not implement the synchronized change event");
    Assert(code.Contains(matcherMarker, StringComparison.Ordinal),
        $"{codePath} does not use the selected pattern and flags");
}

static string ReadRepo(string relativePath)
    => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

static string FindRepoRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "WinForge.csproj"))) return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find the WinForge repository root.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
