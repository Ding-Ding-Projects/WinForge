var failures = new List<string>();
var passed = 0;

Run("live API timer attaches one named handler", LiveApiTimerAttachesOneNamedHandler);
Run("reload lifecycle restores and releases language subscription", ReloadLifecycleBalancesLanguageSubscription);
Run("timer callback is the single live API refresh path", TimerCallbackIsSingleRefreshPath);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} reactor-settings lifecycle tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} reactor-settings lifecycle tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static string Source()
{
    string path = Path.Combine(AppContext.BaseDirectory, "ReactorSettingsModule.xaml.cs");
    Assert(File.Exists(path), "Reactor Settings source was not copied into the test output");
    return File.ReadAllText(path);
}

static void LiveApiTimerAttachesOneNamedHandler()
{
    string source = Source();
    string constructor = MethodBody(source, "public ReactorSettingsModule()");
    string loaded = MethodBody(source, "private async void OnLoaded(object sender, RoutedEventArgs e)");

    Equal(1, Count(source, "_liveTimer.Tick +="), "live timer handler attach count");
    AssertContains(constructor, "_liveTimer.Tick += OnLiveTimerTick;", "constructor does not attach the named timer handler");
    AssertContains(constructor, "Loaded += OnLoaded;", "constructor does not use a named Loaded handler");
    AssertContains(constructor, "Unloaded += OnUnloaded;", "constructor does not use a named Unloaded handler");
    AssertNotContains(loaded, "_liveTimer.Tick +=", "OnLoaded can stack timer handlers across reloads");
    Equal(1, Count(loaded, "_liveTimer.Start();"), "OnLoaded timer-start count");
}

static void ReloadLifecycleBalancesLanguageSubscription()
{
    string source = Source();
    string loaded = MethodBody(source, "private async void OnLoaded(object sender, RoutedEventArgs e)");
    string unloaded = MethodBody(source, "private void OnUnloaded(object sender, RoutedEventArgs e)");
    string subscribe = MethodBody(source, "private void SubscribeLanguage()");
    string unsubscribe = MethodBody(source, "private void UnsubscribeLanguage()");

    AssertContains(loaded, "SubscribeLanguage();", "OnLoaded does not restore its language subscription");
    AssertContains(unloaded, "_liveTimer.Stop();", "OnUnloaded does not stop the live timer");
    AssertContains(unloaded, "UnsubscribeLanguage();", "OnUnloaded does not release its language subscription");
    AssertContains(subscribe, "if (_languageSubscribed) return;", "language subscribe path is not idempotent");
    AssertContains(subscribe, "Loc.I.LanguageChanged += OnLanguageChanged;", "language subscribe path is missing the named handler");
    AssertContains(unsubscribe, "if (!_languageSubscribed) return;", "language unsubscribe path is not idempotent");
    AssertContains(unsubscribe, "Loc.I.LanguageChanged -= OnLanguageChanged;", "language unsubscribe path is missing the named handler release");
}

static void TimerCallbackIsSingleRefreshPath()
{
    string source = Source();
    string timer = MethodBody(source, "private void OnLiveTimerTick(object? sender, object e)");

    Equal(1, Count(source, "private void OnLiveTimerTick("), "named live timer handler declaration count");
    Equal(1, Count(timer, "UpdateApiState();"), "live timer refresh count per tick");
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

static int Count(string text, string value)
{
    int count = 0;
    int index = 0;
    while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
    {
        count++;
        index += value.Length;
    }
    return count;
}

static void Equal(int expected, int actual, string label)
{
    if (expected != actual)
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void AssertContains(string text, string value, string message)
{
    if (!text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException(message);
}

static void AssertNotContains(string text, string value, string message)
{
    if (text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
