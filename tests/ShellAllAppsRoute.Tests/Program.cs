var failures = new List<string>();
var passed = 0;

Run("command-line shell route waits for NavigationView load", StartPageWaitsForLoadedNavigation);
Run("shell route selects exactly once and awaits the picker", StartPageOpensSinglePicker);
Run("automation contract retains stable dialog identifiers", DialogAutomationContractIsStable);
Run("new-tab picker owns the shared bounded search contract", PickerSearchContract);
Run("category picker owns its searchable regex dropdown source contract", CategoryPickerSearchContract);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} shell All Apps route tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} shell All Apps route tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static string Source()
{
    var path = Path.Combine(AppContext.BaseDirectory, "MainWindow.xaml.cs");
    Assert(File.Exists(path), "MainWindow source was not copied into the test output");
    return File.ReadAllText(path);
}

static void StartPageWaitsForLoadedNavigation()
{
    var apply = MethodBody(Source(), "private void ApplyStartPage()");
    AssertContains(apply, "string.Equals(App.StartPage, AllAppsPickerKey, StringComparison.OrdinalIgnoreCase)",
        "ApplyStartPage does not recognize shell.allapps");
    AssertContains(apply, "NavView.Loaded += OnStartPageAllAppsLoaded;",
        "shell.allapps does not wait for NavigationView to have a XamlRoot");
}

static void StartPageOpensSinglePicker()
{
    var source = Source();
    var loaded = MethodBody(source, "private void OnStartPageAllAppsLoaded(object sender, RoutedEventArgs e)");
    var open = MethodBody(source, "private async Task OpenStartPageAllAppsAsync()");

    AssertContains(loaded, "NavView.Loaded -= OnStartPageAllAppsLoaded;",
        "loaded handler is not one-shot");
    AssertContains(loaded, "DispatcherQueue.TryEnqueue(() => _ = OpenStartPageAllAppsAsync());",
        "loaded handler does not queue the picker on the UI dispatcher");
    AssertContains(open, "FindByTag(AllAppsPickerKey)", "shell navigation item is not resolved by tag");
    AssertContains(open, "_syncingTabs = true;", "selection event is not suppressed for the direct route");
    AssertContains(open, "NavView.SelectedItem = item;", "shell route does not retain selected navigation state");
    AssertContains(open, "await OpenAllAppsPickerFromShellAsync();", "shell route does not await the picker");
}

static void DialogAutomationContractIsStable()
{
    var source = Source();
    AssertContains(source, "SetAutomationId(dialog, \"NewTabPickerDialog\")",
        "picker dialog automation id changed");
    AssertContains(source, "SetAutomationId(search, \"NewTabPickerSearchBox\")",
        "picker search automation id changed");
    AssertContains(source, "\"ShellNavItem_\" + AutomationSafeKey(t0)",
        "shell navigation automation-id convention changed");
}

static void PickerSearchContract()
{
    var picker = MethodBody(Source(), "private async Task ShowNewTabPickerAsync()");
    AssertContains(picker, "var search = new SearchPatternBox", "picker reverted to a plain search control");
    AssertNotContains(picker, "var search = new TextBox", "plain TextBox search remains in the picker");
    AssertNotContains(picker, "search.TextChanged +=", "picker bypasses SearchPatternBox.PatternChanged");
    AssertContains(picker, "search.PatternChanged += (_, _) => Render();", "picker does not refresh from synchronized pattern state");
    AssertContains(picker, "search.QuerySubmitted += (_, _) =>", "picker has no query-only Enter activation");
    AssertContains(picker, "search.Spec.UseRegex", "regex-mode empty-state logic loses whitespace patterns");
    AssertContains(picker, "NewTabPickerNoResults", "picker has no named empty/error state");
    AssertContains(picker, "SearchPatternService.Matcher matcher", "picker does not compile the complete SearchPatternBox.Spec");
    AssertContains(picker, "MatchesPickerEntry(entry, matcher)", "picker results do not use the compiled matcher");
    AssertContains(picker, "AutomationNameProvider = () => Loc.I.Pick(", "picker search has no language-refreshable accessible name");
    AssertContains(picker, "search.FocusQuery()", "picker does not focus the real query editor");
    AssertContains(picker, "SetAutomationId(search, \"NewTabPickerSearchBox\")", "picker search automation ID changed");

    var searchPatternBox = ReadRepo("Controls", "SearchPatternBox.xaml.cs");
    AssertContains(searchPatternBox, "QueryBox.Focus(FocusState.Programmatic)", "shared search control exposes no real-query focus path");
    AssertContains(searchPatternBox, "QueryBox_QuerySubmitted", "shared search control exposes no query-only Enter path");
}

static void CategoryPickerSearchContract()
{
    // This pure executable verifies source contracts. WinUI focus, flyout, layout, and UI Automation
    // behavior remain built-artifact/runtime evidence and are documented as such.
    var picker = MethodBody(Source(), "private async Task ShowNewTabPickerAsync()");
    AssertContains(picker, "var categoryBox = new SearchablePickerBox", "category picker reverted to a raw ComboBox");
    AssertNotContains(picker, "var categoryBox = new ComboBox", "plain category ComboBox remains in the picker");
    AssertContains(picker, "SearchAutomationId = \"NewTabPickerCategorySearchBox\"", "category search has no stable automation ID");
    AssertContains(picker, "SearchTextProvider = item =>", "category search does not include stable category metadata");
    AssertContains(picker, "categoryBox.SelectionChanged += (_, _) => Render();", "category selection no longer refreshes picker results");
    AssertContains(picker, "categoryBox.RefreshItems()", "category labels are not refreshed after language changes");
    AssertContains(picker, "FunnyLevelSettings.I.Changed += pickerToneChanged", "category result chrome is not refreshed after a tone change");
    AssertContains(picker, "FunnyLevelSettings.I.Changed -= pickerToneChanged", "category tone subscription is not cleaned up");

    var control = ReadRepo("Controls", "SearchablePickerBox.cs");
    AssertContains(control, "private readonly SearchPatternBox _search", "category control has no owned SearchPatternBox");
    AssertContains(control, "_search.PatternChanged +=", "category search does not react to synchronized pattern changes");
    AssertContains(control, "_search.QuerySubmitted +=", "category search has no query-only Enter path");
    AssertContains(control, "SearchPatternService.Matcher matcher = _search.CompileMatcher()", "category search does not validate the full pattern");
    AssertContains(control, "matcher.MatchAny(option.SearchValues)", "category search does not reuse one compiled matcher for every option");
    AssertNotContains(control, "_search.MatchAny(option.SearchValues)", "category search recompiles its matcher for every option");
    AssertContains(control, "Invalid .NET regex", "category search has no honest regex error state");
    AssertContains(control, "No matching categories.", "category search has no honest no-match state");
    AssertContains(control, "_flyout.Closed += Flyout_Closed", "category search has no close lifecycle");
    AssertContains(control, "private void Flyout_Closed", "category picker close handler is missing");
    AssertContains(control, "FocusPicker();", "category picker does not return focus after closing");
    AssertContains(control, "_search.QueryKeyDown += Search_QueryKeyDown", "category search has no query-field keyboard path");
    AssertContains(control, "VirtualKey.Down", "category search does not handle Down from the query field");
    AssertContains(control, "VirtualKey.Up", "category search does not handle Up from the query field");
    AssertContains(control, "VirtualKey.Escape", "category search has no clear-then-close Escape path");
    AssertContains(control, "HasSearchInput()", "category Escape behavior has no clear-versus-close decision");
    AssertContains(control, "_search.Spec.Query.Length > 0", "category Escape treats regex mode as input without a pattern");
    AssertNotContains(control, "_search.Spec.UseRegex || _search.Spec.Query.Length > 0", "category Escape requires two Escapes for an empty regex pattern");
    AssertContains(control, "_search.Clear()", "category search cannot clear before closing");
    AssertContains(control, "CommitHighlightedOption", "category query Enter does not commit the highlighted option");
    AssertContains(control, "_optionsList.SelectedItem as Option", "category query Enter ignores the highlighted option");
    AssertNotContains(control, "_pickerButton.Click += PickerButton_Click", "category picker has a second flyout opening path");
    AssertContains(control, "_pickerButton.Flyout = _flyout", "category picker has no authoritative flyout path");
    AssertContains(control, "_searchLabel.Text = P(\"Search options\", \"搜尋選項\")", "flyout chrome is not refreshed with language/tone");
    AssertContains(control, "_search.PlaceholderText = P(\"Search categories\", \"搜尋分類\")", "category placeholder is not refreshed with language/tone");
    AssertContains(control, "FunnyLevelSettings.I.Changed += OnToneChanged", "category picker does not subscribe to live funny-level changes");
    AssertContains(control, "FunnyLevelSettings.I.Changed -= OnToneChanged", "category picker does not unsubscribe from funny-level changes");
    AssertContains(control, "StyleEnglish", "English funny level does not style category copy");
    AssertContains(control, "StyleCantonese", "Cantonese funny level does not style category copy");
    AssertContains(control, "_pickerButton.XamlRoot?.Size.Width", "category flyout does not measure the available viewport");
    AssertContains(control, "_flyoutPanel.Width = width", "category flyout does not apply a viewport-aware width");
    AssertContains(control, "_search.MaxLayoutWidth", "nested regex builder does not receive the narrow-layout width cap");
    AssertContains(control, "_search.AutomationIdPrefix = _searchAutomationId", "category search descendants are not namespaced");
    AssertContains(control, "$\"{prefix}_Status\"", "category status automation ID is not namespaced");
    AssertNotContains(control, "SearchablePickerNoResults", "category status keeps a dead initializer instead of its final runtime ID");
    AssertContains(control, "_search.RefreshAutomationNames();", "nested search accessible names are not refreshed after tone changes");
    AssertContains(control, "RefreshItems();", "category option labels are not rebuilt after tone changes");
    AssertContains(control, "VirtualKey.Escape", "category list has no keyboard cancellation path");

    var searchPatternBox = ReadRepo("Controls", "SearchPatternBox.xaml.cs");
    AssertContains(searchPatternBox, "QueryKeyDown?.Invoke", "shared search control does not expose query-field key events");
    AssertContains(searchPatternBox, "AutomationIdPrefix", "shared search control cannot namespace descendant IDs");
    AssertContains(searchPatternBox, "MaxLayoutWidth", "shared search control cannot honor a narrow host width");
    AssertContains(searchPatternBox, "public void RefreshAutomationNames()", "shared search control has no host-triggered accessibility refresh");
    AssertContains(searchPatternBox, "FunnyLevelSettings.I.Changed += OnToneChanged", "shared search control does not observe funny-level changes");
    AssertContains(searchPatternBox, "FunnyLevelSettings.I.Changed -= OnToneChanged", "shared search control does not clean up funny-level changes");

    var mainWindow = Source();
    AssertContains(mainWindow, "FunnyLevelSettings.I.StyleEnglish(En)", "category labels bypass the English funny-level style");
    AssertContains(mainWindow, "FunnyLevelSettings.I.StyleCantonese(Zh)", "category labels bypass the Cantonese funny-level style");
    AssertContains(mainWindow, "FunnyLevelSettings.I.StyleEnglish(\"Category\")", "category header bypasses the English funny-level style");
    AssertContains(mainWindow, "FunnyLevelSettings.I.StyleCantonese(\"分類\")", "category header bypasses the Cantonese funny-level style");
    AssertContains(mainWindow, "categoryBox.RefreshItems();", "category option text is not rebuilt from the host tone update");
}

static string MethodBody(string source, string signature)
{
    var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    if (signatureIndex < 0) throw new InvalidOperationException($"method signature not found: {signature}");
    var openBrace = source.IndexOf('{', signatureIndex);
    if (openBrace < 0) throw new InvalidOperationException($"opening brace not found: {signature}");

    var depth = 0;
    for (var i = openBrace; i < source.Length; i++)
    {
        if (source[i] == '{') depth++;
        else if (source[i] == '}' && --depth == 0) return source[(openBrace + 1)..i];
    }
    throw new InvalidOperationException($"closing brace not found: {signature}");
}

static void AssertContains(string text, string value, string message)
{
    if (!text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException(message);
}

static void AssertNotContains(string text, string value, string message)
{
    if (text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException(message);
}

static string ReadRepo(params string[] parts)
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "WinForge.csproj")))
        {
            var path = directory.FullName;
            foreach (var part in parts) path = Path.Combine(path, part);
            return File.ReadAllText(path);
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not find the WinForge repository root.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
