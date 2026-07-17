var failures = new List<string>();
var passed = 0;

Run("command-line shell route waits for NavigationView load", StartPageWaitsForLoadedNavigation);
Run("shell route selects exactly once and awaits the picker", StartPageOpensSinglePicker);
Run("automation contract retains stable dialog identifiers", DialogAutomationContractIsStable);

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

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
