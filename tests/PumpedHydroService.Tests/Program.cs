using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("explicit ticks alone advance stored energy", ExplicitTicksAdvanceState);
Run("charge and discharge use the documented split round-trip efficiency", ChargeAndDischargeUseLegEfficiency);
Run("generation mints from delivered MWh without a kWh multiplier", GenerationUsesMwhMintUnits);
Run("page loading and rendering are observational; only the guarded timer advances", PageLifecycleIsTimerOnly);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} pumped-hydro integrity tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} pumped-hydro integrity tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static void ExplicitTicksAdvanceState()
{
    var hydro = new PumpedHydroService();
    hydro.SetMode(HydroMode.Pump);

    Equal(0.0, hydro.StoredMWh, "new reservoir level");
    hydro.Tick(0, 600, reactorGenerating: true, requestPumpMW: 400);
    Equal(0.0, hydro.StoredMWh, "zero-duration tick changed stored energy");

    hydro.Tick(5, 600, reactorGenerating: true, requestPumpMW: 400);
    Assert(hydro.StoredMWh > 0, "positive timer tick did not charge the reservoir");
}

static void ChargeAndDischargeUseLegEfficiency()
{
    var hydro = new PumpedHydroService();
    hydro.SetMode(HydroMode.Pump);

    hydro.Tick(5, 600, reactorGenerating: true, requestPumpMW: 400);
    double legEfficiency = Math.Sqrt(PumpedHydroService.RoundTripEfficiency);
    double expectedCharged = 400 * (5.0 / 3600.0) * legEfficiency;
    Nearly(expectedCharged, hydro.StoredMWh, "stored energy after one deterministic charge tick");
    Nearly(400, hydro.PumpDrawMW, "pump draw");

    hydro.SetMode(HydroMode.Generate);
    hydro.Tick(5, 0, reactorGenerating: false, requestPumpMW: 0);
    double expectedRemaining = expectedCharged - PumpedHydroService.MaxGenMW * (5.0 / 3600.0);
    Nearly(expectedRemaining, hydro.StoredMWh, "stored energy after one deterministic generation tick");
    Nearly(PumpedHydroService.MaxGenMW, hydro.GenOutMW, "generation output");
}

static void GenerationUsesMwhMintUnits()
{
    var hydro = new PumpedHydroService();
    hydro.SetMode(HydroMode.Pump);
    for (int i = 0; i < 64; i++)
        hydro.Tick(5, 600, reactorGenerating: true, requestPumpMW: 400);

    hydro.SetMode(HydroMode.Generate);
    for (int i = 0; i < 63; i++)
        Equal(0.0, hydro.Tick(5, 0, reactorGenerating: false, requestPumpMW: 0),
            $"generation deposited early at tick {i + 1}");

    double deliveredMWh = 64 * PumpedHydroService.MaxGenMW * (5.0 / 3600.0) * Math.Sqrt(PumpedHydroService.RoundTripEfficiency);
    double expectedAccrued = deliveredMWh * PumpedHydroService.WattsPerDeliveredMWh;
    Assert(expectedAccrued >= 1 && expectedAccrued < 2, "test fixture no longer crosses exactly one whole-⚡ threshold");
    Nearly(0.036, PumpedHydroService.WattsFromDeliveredMWh(1), "one delivered MWh mint");

    Equal(1.0, hydro.Tick(5, 0, reactorGenerating: false, requestPumpMW: 0),
        "64th generation tick should deposit one whole ⚡");
    Equal(1.0, hydro.EarnedTotal, "session total after MWh-based minting");
    Nearly(ReactorEconomyService.MintPerMWSecond * 3600.0, PumpedHydroService.WattsPerDeliveredMWh,
        "MWh conversion rate");
}

static void PageLifecycleIsTimerOnly()
{
    string path = Path.Combine(AppContext.BaseDirectory, "PumpedHydroModule.xaml.cs");
    Assert(File.Exists(path), "page source was not copied into the test output");
    string source = File.ReadAllText(path);

    string loaded = MethodBody(source, "private void OnLoaded(object sender, RoutedEventArgs e)");
    string render = MethodBody(source, "private void Render()");
    string renderText = MethodBody(source, "private void RenderText()");
    string language = MethodBody(source, "private void OnLang(object? sender, EventArgs e)");
    string tick = MethodBody(source, "private void OnTick(object? sender, object e)");
    string advance = MethodBody(source, "private void AdvanceSimulation()");
    string unloaded = MethodBody(source, "private void OnUnloaded(object sender, RoutedEventArgs e)");

    AssertNotContains(loaded, "AdvanceSimulation(", "OnLoaded advanced the simulator");
    AssertNotContains(loaded, "_hydro.Tick(", "OnLoaded directly ticked the simulator");
    Equal(1, Count(loaded, "Render();"), "OnLoaded render count");
    Equal(1, Count(loaded, "_timer.Start();"), "OnLoaded timer-start count");

    AssertNotContains(render, "AdvanceSimulation(", "Render advanced the simulator");
    AssertNotContains(render, "_hydro.Tick(", "Render directly ticked the simulator");
    AssertNotContains(render, "ReactorEconomyService.I.Earn(", "Render minted currency");
    AssertNotContains(renderText, "AdvanceSimulation(", "RenderText advanced the simulator");
    AssertNotContains(renderText, "_hydro.Tick(", "RenderText directly ticked the simulator");
    AssertContains(language, "if (_isLoaded) Render();", "language refresh is not render-only");
    AssertNotContains(language, "AdvanceSimulation(", "language refresh advanced the simulator");
    AssertNotContains(language, "_hydro.Tick(", "language refresh directly ticked the simulator");

    AssertContains(tick, "if (!_isLoaded) return;", "timer does not guard an unloaded page");
    Equal(1, Count(tick, "AdvanceSimulation();"), "timer advancement count");
    AssertContains(source, "private const double SimulationTickSeconds = 0.5;", "fixed simulation interval changed");
    AssertContains(source, "TimeSpan.FromSeconds(SimulationTickSeconds)", "timer interval diverged from the simulation interval");
    Equal(1, Count(advance, "_hydro.Tick(SimulationTickSeconds"), "service tick count per timer event");
    AssertContains(unloaded, "_timer.Stop();", "Unloaded does not stop the timer");
    AssertContains(unloaded, "Loc.I.LanguageChanged -= OnLang;", "Unloaded does not release the language handler");
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

static void Equal(double expected, double actual, string label) => Nearly(expected, actual, label, tolerance: 1e-9);

static void Nearly(double expected, double actual, string label, double tolerance = 1e-8)
{
    if (Math.Abs(expected - actual) > tolerance)
        throw new InvalidOperationException($"{label}: expected '{expected:R}', got '{actual:R}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertContains(string text, string value, string message)
{
    if (!text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException(message);
}

static void AssertNotContains(string text, string value, string message)
{
    if (text.Contains(value, StringComparison.Ordinal)) throw new InvalidOperationException(message);
}
