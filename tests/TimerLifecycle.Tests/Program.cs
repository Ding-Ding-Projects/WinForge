var failures = new List<string>();
var passed = 0;

Run("reload lifecycle balances the named language subscription", ReloadLifecycleBalancesLanguageSubscription);
Run("unload pauses every timer without invisible stopwatch elapsed time", UnloadPausesEveryTimer);
Run("reload renders the paused timer snapshots", ReloadRendersPausedSnapshots);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} timer lifecycle tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} timer lifecycle tests");
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

static string Source()
{
    string path = Path.Combine(AppContext.BaseDirectory, "TimerModule.xaml.cs");
    Assert(File.Exists(path), "Timer source was not copied into the test output");
    return File.ReadAllText(path);
}

static void ReloadLifecycleBalancesLanguageSubscription()
{
    string source = Source();
    string constructor = MethodBody(source, "public TimerModule()");
    string loaded = MethodBody(source, "private void OnLoaded(object sender, RoutedEventArgs e)");
    string unloaded = MethodBody(source, "private void OnUnloaded(object sender, RoutedEventArgs e)");
    string subscribe = MethodBody(source, "private void SubscribeLanguage()");
    string unsubscribe = MethodBody(source, "private void UnsubscribeLanguage()");

    AssertContains(constructor, "Loaded += OnLoaded;", "constructor does not use a named Loaded handler");
    AssertContains(constructor, "Unloaded += OnUnloaded;", "constructor does not use a named Unloaded handler");
    AssertContains(loaded, "SubscribeLanguage();", "load does not restore the language subscription");
    AssertContains(unloaded, "UnsubscribeLanguage();", "unload does not release the language subscription");
    AssertContains(subscribe, "if (_languageSubscribed) return;", "subscribe path is not idempotent");
    AssertContains(subscribe, "Loc.I.LanguageChanged += OnLang;", "subscribe path is missing the named language handler");
    AssertContains(unsubscribe, "if (!_languageSubscribed) return;", "unsubscribe path is not idempotent");
    AssertContains(unsubscribe, "Loc.I.LanguageChanged -= OnLang;", "unsubscribe path is missing the named language handler release");
}

static void UnloadPausesEveryTimer()
{
    string source = Source();
    string unload = MethodBody(source, "private void OnUnloaded(object sender, RoutedEventArgs e)");
    string pause = MethodBody(source, "private void PauseForUnload()");

    AssertContains(unload, "PauseForUnload();", "unload does not centralize timer cleanup");
    AssertContains(pause, "_swAccum += _sw.Elapsed;", "unload drops the active stopwatch elapsed time");
    AssertContains(pause, "_sw.Reset();", "unload leaves the stopwatch running invisibly");
    AssertContains(pause, "_swRunning = false;", "unload leaves the stopwatch flag running");
    AssertContains(pause, "_swTimer.Stop();", "unload does not stop the stopwatch timer");
    AssertContains(pause, "_cdTimer.Stop();", "unload does not stop the countdown timer");
    AssertContains(pause, "_cdRunning = false;", "unload leaves the countdown flag running");
    AssertContains(pause, "_pomoTimer.Stop();", "unload does not stop the Pomodoro timer");
    AssertContains(pause, "_pomoRunning = false;", "unload leaves the Pomodoro flag running");
}

static void ReloadRendersPausedSnapshots()
{
    string source = Source();
    string render = MethodBody(source, "private void Render()");

    AssertContains(render, "SwUpdateDisplay();", "reload does not refresh the stopwatch snapshot");
    AssertContains(render, "CdUpdateDisplay();", "reload does not refresh the countdown snapshot");
    AssertContains(render, "PomoUpdateDisplay();", "reload does not refresh the Pomodoro snapshot");
}

static string MethodBody(string source, string signature)
{
    int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    if (signatureIndex < 0) throw new InvalidOperationException($"method signature not found: {signature}");
    int openBrace = source.IndexOf('{', signatureIndex);
    if (openBrace < 0) throw new InvalidOperationException($"opening brace not found: {signature}");

    int depth = 0;
    for (int i = openBrace; i < source.Length; i++)
    {
        if (source[i] == '{') depth++;
        else if (source[i] == '}' && --depth == 0)
            return source[(openBrace + 1)..i];
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
