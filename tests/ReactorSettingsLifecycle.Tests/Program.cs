var failures = new List<string>();
var passed = 0;

Run("live API timer attaches one named handler", LiveApiTimerAttachesOneNamedHandler);
Run("reload lifecycle restores and releases language subscription", ReloadLifecycleBalancesLanguageSubscription);
Run("timer callback is the single live API refresh path", TimerCallbackIsSingleRefreshPath);
Run("feature-power fallback is loaded and changed under the suppression guard", FeaturePowerFallbackUsesSuppressionGuard);
Run("manual EDG fill and fuel/slot UI are wired", ManualFeatureDieselFillAndCapacityUiAreWired);
Run("reactor page shares one canonical foreground/background session", CanonicalReactorSessionLifecycleIsBalanced);
Run("session-global shutdown and real effects follow the visible driver", SessionGlobalSafetyStateFollowsForegroundDriver);
Run("settings arm request returns to a visible reactor", SettingsArmRequestRequiresVisibleReactor);
Run("system-link lifecycle cancels stale restore and retries queued enable", SystemLinkLifecycleCancelsStaleRestore);
Run("Home Assistant lifecycle cancels stale OFF and reasserts", HomeAssistantLifecycleCancelsStaleRestore);

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

static string Source(string fileName = "ReactorSettingsModule.xaml.cs")
{
    string path = Path.Combine(AppContext.BaseDirectory, fileName);
    Assert(File.Exists(path), $"{fileName} was not copied into the test output");
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
    Equal(1, Count(timer, "UpdateFeaturePowerState();"), "feature-power refresh count per tick");
}

static void FeaturePowerFallbackUsesSuppressionGuard()
{
    string source = Source();
    string load = MethodBody(source, "private void LoadState()");
    string toggle = MethodBody(source, "private void DieselFallback_Toggled(object sender, RoutedEventArgs e)");
    string start = MethodBody(source, "private void StartFeatureDiesel_Click(object sender, RoutedEventArgs e)");
    string stop = MethodBody(source, "private void StopFeatureDiesel_Click(object sender, RoutedEventArgs e)");

    AssertContains(
        load,
        "DieselFallbackToggle.IsOn = ReactorFeaturePowerService.I.AllowEmergencyDieselFallback;",
        "LoadState does not restore the persisted feature-power policy");
    AssertContains(toggle, "if (_suppress) return;", "feature-power toggle does not honor the programmatic-update guard");
    AssertContains(
        toggle,
        "AllowEmergencyDieselFallback = DieselFallbackToggle.IsOn;",
        "feature-power toggle does not update the shared persisted policy");
    AssertContains(start, "StartEmergencyDiesel();", "manual EDG start control is not wired to the state machine");
    AssertContains(stop, "StopEmergencyDiesel();", "manual EDG stop control is not wired to the state machine");
}

static void ManualFeatureDieselFillAndCapacityUiAreWired()
{
    string source = Source();
    string xaml = Source("ReactorSettingsModule.xaml");
    string dependencySource = Source("ReactorDependencyPage.xaml.cs");
    string dependencyXaml = Source("ReactorDependencyPage.xaml");
    string fill = MethodBody(source, "private void FillFeatureDiesel_Click(object sender, RoutedEventArgs e)");
    string refresh = MethodBody(source, "private void UpdateFeaturePowerState()");
    string dependencyStatus = MethodBody(dependencySource, "private void UpdateStatus()");

    Equal(1, Count(xaml, "x:Name=\"FillFeatureDieselButton\""), "manual fill button declaration count");
    Equal(1, Count(xaml, "Click=\"FillFeatureDiesel_Click\""), "manual fill click binding count");
    AssertContains(
        fill,
        "ReactorFeaturePowerService.I.FillEmergencyDiesel();",
        "manual fill click does not call the shared EDG state machine");
    AssertContains(fill, "UpdateFeaturePowerState();", "manual fill click does not refresh the visible EDG state");

    AssertContains(xaml, "x:Name=\"FeatureDieselFuelProgress\"", "fuel-level progress control is missing");
    AssertContains(xaml, "x:Name=\"FeatureDieselFuelText\"", "fuel-level text control is missing");
    AssertContains(
        xaml,
        "AutomationProperties.Name=\"Emergency diesel fuel level · 應急柴油發電機油量\"",
        "fuel-level progress control is missing its bilingual accessible name");
    AssertContains(
        refresh,
        "FeatureDieselFuelProgress.Maximum = diesel.FuelCapacityLitres;",
        "fuel progress maximum is not driven by the session tank capacity");
    AssertContains(
        refresh,
        "FeatureDieselFuelProgress.Value = diesel.FuelLitres;",
        "fuel progress value is not driven by the live session fuel");
    AssertContains(
        refresh,
        "{diesel.ActiveModuleCount}/{diesel.MaxModuleCount}",
        "fuel readout does not report occupied EDG module outlets");
    AssertContains(
        refresh,
        "{diesel.AvailableModuleSlots} of {diesel.MaxModuleCount} module outlets free.",
        "running-state readout does not report free EDG module outlets");
    AssertContains(refresh, "模組插槽", "EDG outlet readout is missing Cantonese copy");
    AssertContains(
        refresh,
        "diesel.FuelLitres < diesel.FuelCapacityLitres",
        "manual fill control is not limited to a non-full tank");
    AssertContains(
        refresh,
        "StartFeatureDieselButton.IsEnabled = diesel.HasFuel;",
        "manual start control is not gated by available simulated fuel");

    Equal(
        2,
        Count(xaml, "AutomationProperties.HeadingLevel=\"Level2\""),
        "reactor-settings level-2 heading count");
    AssertInOrder(
        xaml,
        "feature-power settings heading is missing level-2 automation semantics",
        "x:Name=\"FeaturePowerTitle\"",
        "AutomationProperties.HeadingLevel=\"Level2\"");
    AssertInOrder(
        xaml,
        "diesel-voucher-exchange heading is missing level-2 automation semantics",
        "x:Name=\"VoucherTitle\"",
        "AutomationProperties.HeadingLevel=\"Level2\"");
    Equal(
        2,
        Count(dependencyXaml, "AutomationProperties.HeadingLevel=\"Level2\""),
        "reactor-dependency level-2 heading count");
    AssertInOrder(
        dependencyXaml,
        "dependency requirement heading is missing level-2 automation semantics",
        "x:Name=\"RequirementTitle\"",
        "AutomationProperties.HeadingLevel=\"Level2\"");
    AssertInOrder(
        dependencyXaml,
        "dependency diesel heading is missing level-2 automation semantics",
        "x:Name=\"DieselTitle\"",
        "AutomationProperties.HeadingLevel=\"Level2\"");

    AssertInOrder(
        dependencyStatus,
        "Cantonese dependency status must map reactor mode and generating state before rendering its snapshot",
        "string reactorModeZh = (snapshot.Mode ?? \"\").Trim().ToLowerInvariant() switch",
        "\"shutdown\" => \"停堆\"",
        "\"meltdown\" => \"熔毀\"",
        "\"offline\" or \"\" => \"離線\"",
        "string generatingZh = snapshot.IsGenerating ? \"有\" : \"無\";",
        "SnapshotText.Text = P(",
        "核電匯流排：模式={reactorModeZh}，發電={generatingZh}");
    AssertNotContains(
        dependencyStatus,
        "核電匯流排：模式={snapshot.Mode",
        "Cantonese dependency snapshot exposes the raw English reactor mode");
    AssertNotContains(
        dependencyStatus,
        "發電={snapshot.IsGenerating}",
        "Cantonese dependency snapshot exposes a raw Boolean generating value");
}

static void CanonicalReactorSessionLifecycleIsBalanced()
{
    string runtime = Source("ReactorSessionRuntime.cs");
    string module = Source("ReactorModule.xaml.cs");

    string runtimeConstructor = MethodBody(runtime, "private ReactorSessionRuntime()");
    string registerForeground = MethodBody(
        runtime,
        "public void RegisterForeground(ReactorSimulationSession session, string ownerToken)");
    string releaseForeground = MethodBody(
        runtime,
        "public bool ReleaseForeground(ReactorSimulationSession session, string ownerToken)");
    string backgroundTick = MethodBody(runtime, "private void OnBackgroundTick(object? sender, object e)");

    Equal(
        1,
        Count(runtime, "private readonly ReactorSimulationSession _session = new();"),
        "canonical runtime session allocation count");
    AssertContains(
        runtime,
        "public ReactorSimulationSession AcquireForPage() => _session;",
        "reactor pages do not acquire the canonical runtime session");
    AssertContains(
        runtime,
        "public bool PersistenceRestoreAttempted { get; set; }",
        "restore-once state is not owned by the canonical simulation session");
    Equal(1, Count(runtime, "_backgroundTimer.Tick += OnBackgroundTick;"), "background timer handler attach count");
    AssertContains(
        runtimeConstructor,
        "_backgroundTimer.Tick += OnBackgroundTick;",
        "singleton runtime constructor does not attach its named background handler");

    AssertContains(
        registerForeground,
        "if (!ReferenceEquals(session, _session) || string.IsNullOrWhiteSpace(ownerToken)) return;",
        "foreground registration accepts a non-canonical session or empty owner");
    AssertContains(registerForeground, "_backgroundTimer.Stop();", "foreground registration does not stop background driving");
    AssertContains(registerForeground, "_foregroundOwners.RemoveAll(", "foreground registration does not de-duplicate its owner");
    AssertContains(registerForeground, "_foregroundOwners.Add(ownerToken);", "foreground registration does not record its owner");
    AssertBefore(
        registerForeground,
        "_foregroundOwners.RemoveAll(",
        "_foregroundOwners.Add(ownerToken);",
        "foreground owner must be de-duplicated before it becomes the latest driver");

    AssertContains(
        releaseForeground,
        "if (!ReferenceEquals(session, _session) || string.IsNullOrWhiteSpace(ownerToken)) return false;",
        "foreground release accepts a non-canonical session or empty owner");
    AssertContains(releaseForeground, "_foregroundOwners.RemoveAll(", "foreground release does not remove its owner");
    AssertContains(
        releaseForeground,
        "if (_foregroundOwners.Count > 0)",
        "foreground release does not preserve another visible reactor owner");
    Equal(2, Count(releaseForeground, "return false;"), "foreground release false-result count");
    Equal(1, Count(releaseForeground, "return true;"), "last-owner background-handoff result count");
    AssertBefore(
        releaseForeground,
        "if (_foregroundOwners.Count > 0)",
        "_backgroundTimer.Start();",
        "background driving can start before all foreground owners leave");
    AssertContains(
        runtime,
        "string.Equals(_foregroundOwners[^1], ownerToken, StringComparison.Ordinal)",
        "the most recently registered visible page is not the sole foreground driver");

    AssertContains(
        backgroundTick,
        "if (_foregroundOwners.Count > 0)",
        "background tick does not yield to a visible foreground owner");
    AssertContains(backgroundTick, "_backgroundTimer.Stop();", "background tick does not stop after detecting a foreground owner");
    AssertContains(backgroundTick, "_session.Sim.Update(dt);", "background tick does not advance the canonical simulation");
    AssertContains(
        backgroundTick,
        "_session.SimClockSeconds += dt;",
        "background tick does not advance the canonical session clock");
    AssertContains(
        backgroundTick,
        "ReactorStatusApiService.I.Publish();",
        "background tick does not publish the canonical session status");

    string moduleConstructor = MethodBody(module, "public ReactorModule()");
    string attach = MethodBody(module, "private void AttachSessionHandlers()");
    string detach = MethodBody(module, "private void DetachSessionHandlers()");
    string registerPersistence = MethodBody(module, "private void RegisterPersistence()");
    string foregroundTick = MethodBody(module, "private void Tick(object? sender, object e)");

    AssertContains(
        moduleConstructor,
        "_session = ReactorSessionRuntime.I.AcquireForPage();",
        "reactor page does not acquire the canonical session");
    AssertContains(module, "private ReactorSimService _sim => _session.Sim;", "reactor page physics does not use the canonical session");
    AssertContains(
        module,
        "get => _session.SimClockSeconds;",
        "reactor page clock does not read the canonical session");
    AssertContains(
        module,
        "set => _session.SimClockSeconds = value;",
        "reactor page clock does not update the canonical session");
    AssertNotContains(module, "new ReactorSimService(", "reactor page constructs a competing simulation");
    AssertContains(
        module,
        "private readonly string _runtimeOwnerToken = \"reactor-page:\" + Guid.NewGuid().ToString(\"N\");",
        "reactor page does not own a stable unique foreground token");

    Equal(1, Count(moduleConstructor, "AttachSessionHandlers();"), "page-load session-handler attach count");
    Equal(1, Count(moduleConstructor, "DetachSessionHandlers();"), "page-unload session-handler detach count");
    Equal(1, Count(moduleConstructor, "RegisterForeground(_session, _runtimeOwnerToken);"), "page-load foreground registration count");
    Equal(1, Count(moduleConstructor, "ReleaseForeground(_session, _runtimeOwnerToken);"), "page-unload foreground release count");
    AssertBefore(
        moduleConstructor,
        "AttachSessionHandlers();",
        "RegisterForeground(_session, _runtimeOwnerToken);",
        "page must attach canonical-session handlers before becoming the foreground driver");
    AssertBefore(
        moduleConstructor,
        "DetachSessionHandlers();",
        "ReleaseForeground(_session, _runtimeOwnerToken);",
        "page must detach its handlers before handing the session to the background loop");
    AssertContains(
        foregroundTick,
        "if (!ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken))",
        "page timer has no separate observer path while another page owns the foreground session");

    AssertContains(attach, "if (_sessionHandlersAttached) return;", "session handler attachment is not idempotent");
    AssertContains(detach, "if (!_sessionHandlersAttached) return;", "session handler detachment is not idempotent");
    foreach (string handler in new[]
             {
                 "_sim.MeltdownOccurred",
                 "_sim.PzrCodeSafetyLifted",
                 "_sim.MssvValveLifted",
                 "ReactorSessionRuntime.I.ForegroundDriverChanged",
                 "ReactorRealShutdownArm.ArmedChanged",
                 "Loc.I.LanguageChanged",
             })
    {
        AssertContains(attach, $"{handler} +=", $"{handler} is not attached with the visible page");
        AssertContains(detach, $"{handler} -=", $"{handler} is not detached when the page unloads");
    }
    AssertContains(attach, "_sessionHandlersAttached = true;", "session handler attach flag is not set");
    AssertContains(detach, "_sessionHandlersAttached = false;", "session handler attach flag is not cleared");
    Equal(1, Count(moduleConstructor, "PersistenceService.I.Saved += OnStateSaved;"), "save-event attach count");
    Equal(1, Count(moduleConstructor, "PersistenceService.I.Saved -= OnStateSaved;"), "save-event detach count");

    AssertContains(registerPersistence, "if (_persistenceRegistered) return;", "persistence provider registration is not idempotent");
    AssertContains(
        registerPersistence,
        "bool restoreSavedState = !_session.PersistenceRestoreAttempted;",
        "persistence restore is not gated by canonical-session state");
    AssertContains(
        registerPersistence,
        "restoreSavedState: restoreSavedState",
        "restore-once decision is not passed to the persistence provider");
    AssertContains(
        registerPersistence,
        "_session.PersistenceRestoreAttempted = true;",
        "canonical session is not marked after the persistence restore attempt");
    AssertBefore(
        registerPersistence,
        "bool restoreSavedState = !_session.PersistenceRestoreAttempted;",
        "PersistenceService.I.Register(",
        "restore-once decision must be captured before provider registration");
    AssertBefore(
        registerPersistence,
        "PersistenceService.I.Register(",
        "_session.PersistenceRestoreAttempted = true;",
        "canonical session must be marked only after provider registration attempts restore");
}

static void SessionGlobalSafetyStateFollowsForegroundDriver()
{
    string runtime = Source("ReactorSessionRuntime.cs");
    string module = Source("ReactorModule.xaml.cs");
    string moduleXaml = Source("ReactorModule.xaml");
    string reactorWindows = Source("ReactorWindows.cs");
    string mirror = Source("ReactorHomeAssistantMirror.cs");

    string resetShutdown = MethodBody(runtime, "public void ResetRealShutdownSequence()");
    string registerForeground = MethodBody(
        runtime,
        "public void RegisterForeground(ReactorSimulationSession session, string ownerToken)");
    string releaseForeground = MethodBody(
        runtime,
        "public bool ReleaseForeground(ReactorSimulationSession session, string ownerToken)");
    string publishDriver = MethodBody(runtime, "private void PublishForegroundDriverChanged(string? previousDriver)");
    string moduleConstructor = MethodBody(module, "public ReactorModule()");
    string lastOwnerUnload = MethodBody(moduleConstructor, "if (movedToBackground)");
    string driverChanged = MethodBody(module, "private void OnForegroundDriverChanged(string? ownerToken)");
    string driverUi = MethodBody(module, "private void UpdateDriverUi(bool isDriver)");
    string syncObserverMeltdown = MethodBody(module, "private void SyncObserverMeltdownPresentation()");
    string clearLocalMeltdown = MethodBody(module, "private void ClearLocalMeltdownPresentation()");
    string buildScenarioCombo = MethodBody(module, "private void BuildScenarioCombo()");
    string scenarioChanged = MethodBody(module, "private void Scenario_Changed(object sender, SelectionChangedEventArgs e)");
    string isolateSg = MethodBody(module, "private void IsolateSg_Click(object sender, RoutedEventArgs e)");
    string defeatAmsac = MethodBody(module, "private void AmsacDefeat_Click(object sender, RoutedEventArgs e)");
    string openControlRoom = MethodBody(module, "private void OpenControlRoom_Click(object sender, RoutedEventArgs e)");
    string openChecklistWidget = MethodBody(module, "private void OpenChecklistWidget_Click(object sender, RoutedEventArgs e)");
    string openWidgets = MethodBody(module, "private void OpenWidgets_Click(object sender, RoutedEventArgs e)");
    string syncControlValues = MethodBody(module, "private void SyncControlValues()");
    string syncTopLevelControls = MethodBody(module, "private void SyncTopLevelControls()");
    string closeInteractiveSurfaces = MethodBody(reactorWindows, "public static void CloseInteractiveSurfaces()");
    string armChanged = MethodBody(module, "private void OnRealShutdownArmChanged(bool armed)");
    string renderDisarmed = MethodBody(armChanged, "void RenderDisarmed()");
    string applyAutoStart = MethodBody(module, "private void ApplyCommandLineAutoStart()");
    string onMeltdown = MethodBody(module, "private void OnMeltdown()");
    string renderMeltdown = MethodBody(module, "private void RenderMeltdownOverlay()");
    string startCountdown = MethodBody(module, "private void StartShutdownCountdown()");
    string updateCountdown = MethodBody(module, "private void UpdateShutdownCountdown()");
    string stopCountdown = MethodBody(module, "private void StopLocalShutdownCountdown()");
    string observerTick = MethodBody(
        MethodBody(module, "private void Tick(object? sender, object e)"),
        "if (!ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken))");
    string abort = MethodBody(module, "private void Abort_Click(object sender, RoutedEventArgs e)");
    string resetSimulation = MethodBody(module, "private void ResetSimulation()");
    string showShutdownFailed = MethodBody(module, "private void ShowShutdownFailed()");
    string showShutdownIssued = MethodBody(module, "private void ShowShutdownIssued()");
    string restoreHomeAssistant = MethodBody(mirror, "public void RestoreOff()");

    AssertContains(
        runtime,
        "public DateTime? RealShutdownDeadlineUtc { get; set; }",
        "real-shutdown deadline is not session-global");
    AssertContains(
        runtime,
        "public bool RealShutdownAborted { get; set; }",
        "real-shutdown abort latch is not session-global");
    AssertContains(
        runtime,
        "public bool RealShutdownIssued { get; set; }",
        "real-shutdown issued latch is not session-global");
    AssertContains(
        runtime,
        "public bool RealShutdownFailed { get; set; }",
        "real-shutdown failure latch is not session-global");
    AssertContains(
        runtime,
        "public bool CommandLineAutoStartApplied { get; set; }",
        "command-line auto-start latch is not session-global");
    AssertContains(resetShutdown, "RealShutdownDeadlineUtc = null;", "shutdown reset does not clear the session deadline");
    AssertContains(resetShutdown, "RealShutdownAborted = false;", "shutdown reset does not clear the session abort latch");
    AssertContains(resetShutdown, "RealShutdownIssued = false;", "shutdown reset does not clear the session issued latch");
    AssertContains(resetShutdown, "RealShutdownFailed = false;", "shutdown reset does not clear the session failure latch");
    AssertNotContains(module, "private bool _aborted", "reactor page retains a page-local shutdown abort latch");
    AssertNotContains(module, "private bool _shutdownIssued", "reactor page retains a page-local shutdown-issued latch");

    AssertContains(
        startCountdown,
        "if (_session.RealShutdownDeadlineUtc is null)",
        "a promoted foreground page can overwrite an existing session shutdown deadline");
    AssertContains(
        startCountdown,
        "_session.RealShutdownDeadlineUtc = DateTime.UtcNow.AddSeconds(10);",
        "countdown start does not create the session-global ten-second deadline");
    AssertContains(
        updateCountdown,
        "if (!ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken))",
        "a non-driver page can update or issue real shutdown");
    AssertContains(
        updateCountdown,
        "_session.RealShutdownAborted = true;",
        "disarming does not latch an abort in the canonical session");
    AssertContains(
        updateCountdown,
        "DateTime deadline = _session.RealShutdownDeadlineUtc ?? DateTime.UtcNow.AddSeconds(10);",
        "countdown display does not resume from the session deadline");
    AssertContains(
        updateCountdown,
        "if (remaining > 0 || _session.RealShutdownIssued) return;",
        "shutdown issue path is not guarded by the session-global issued latch");
    AssertInOrder(
        updateCountdown,
        "the issued latch and synchronous SaveAll must precede the real OS shutdown request",
        "_session.RealShutdownIssued = true;",
        "CrashLogger.Guard(\"reactor:pre-shutdown-flush\", () => PersistenceService.I.SaveAll());",
        "ReactorSimService.InitiateRealShutdown(msg);");
    AssertNotContains(
        updateCountdown,
        "PersistenceService.I.Flush()",
        "pre-shutdown persistence still uses the debounce-prone Flush path");
    AssertInOrder(
        updateCountdown,
        "a refused OS shutdown must become transferable session state before failure UI renders",
        "else",
        "_session.RealShutdownFailed = true;",
        "_session.RealShutdownDeadlineUtc = null;",
        "ShowShutdownFailed();");
    AssertContains(abort, "_session.RealShutdownAborted = true;", "ABORT does not update canonical-session state");
    AssertContains(abort, "_session.RealShutdownDeadlineUtc = null;", "ABORT does not cancel the canonical deadline");
    AssertContains(
        resetSimulation,
        "_session.ResetRealShutdownSequence();",
        "simulation reset does not reset all session-global shutdown state");

    AssertContains(
        runtime,
        "public event Action<string?>? ForegroundDriverChanged;",
        "runtime does not announce authoritative foreground-driver handoff");
    AssertContains(
        publishDriver,
        "handler(currentDriver);",
        "foreground-driver handoff is not delivered synchronously on the owning UI thread");
    Equal(
        1,
        Count(registerForeground, "PublishForegroundDriverChanged(previousDriver);"),
        "foreground registration handoff notification count");
    AssertInOrder(
        registerForeground,
        "an armed meltdown countdown must be aborted before the new driver is installed or notified",
        "string? previousDriver = CurrentForegroundDriver();",
        "if (!string.IsNullOrWhiteSpace(previousDriver)",
        "&& !string.Equals(previousDriver, ownerToken, StringComparison.Ordinal)",
        "&& ReactorRealShutdownArm.Armed",
        "&& session.Sim.Mode == ReactorMode.Meltdown",
        "&& !session.RealShutdownIssued",
        "&& !session.RealShutdownFailed",
        "session.RealShutdownAborted = true;",
        "session.RealShutdownDeadlineUtc = null;",
        "_foregroundOwners.RemoveAll(",
        "_foregroundOwners.Add(ownerToken);",
        "PublishForegroundDriverChanged(previousDriver);");
    Equal(
        2,
        Count(releaseForeground, "PublishForegroundDriverChanged(previousDriver);"),
        "foreground release handoff notification count");
    AssertContains(
        driverChanged,
        "_keepingAwake = AwakeService.Active;",
        "promoted driver does not adopt the process-global keep-awake hold");
    AssertContains(
        driverChanged,
        "StopLocalShutdownCountdown();",
        "demoted driver does not release its page-local shutdown timer");
    AssertNotContains(
        driverChanged,
        "_session.ResetRealShutdownSequence();",
        "driver-to-driver handoff incorrectly cancels the session shutdown sequence");
    AssertNotContains(
        driverChanged,
        "_session.RealShutdownDeadlineUtc = null;",
        "page-local handoff handler duplicates the runtime's pre-publication countdown abort");
    AssertNotContains(
        driverChanged,
        "_session.RealShutdownFailed = false;",
        "driver-to-driver handoff incorrectly clears a transferable shutdown failure");
    AssertInOrder(
        driverChanged,
        "a promoted meltdown driver must synchronously enter the shared overlay path",
        "if (isDriver)",
        "if (_sim.Mode == ReactorMode.Meltdown)",
        "_meltdownHandled = false;",
        "OnMeltdown();",
        "return;");
    AssertInOrder(
        driverChanged,
        "promotion outside Meltdown must clear any presentation left by shared observer state",
        "if (_sim.Mode == ReactorMode.Meltdown)",
        "OnMeltdown();",
        "else",
        "ClearLocalMeltdownPresentation();",
        "return;");
    AssertInOrder(
        driverChanged,
        "demotion must replace page-local countdown controls with the shared read-only meltdown presentation",
        "UpdateDriverUi(false);",
        "ReactorWindowManager.CloseInteractiveSurfaces();",
        "StopLocalShutdownCountdown();",
        "SyncObserverMeltdownPresentation();");
    AssertInOrder(
        syncObserverMeltdown,
        "observer meltdown synchronization must show shared failure while hiding every local safety action",
        "if (ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)) return;",
        "if (_sim.Mode == ReactorMode.Meltdown)",
        "StopLocalShutdownCountdown();",
        "MeltdownOverlay.Visibility = Visibility.Visible;",
        "CountdownBox.Visibility = Visibility.Collapsed;",
        "AbortButton.Visibility = Visibility.Collapsed;",
        "MeltdownCloseButton.Visibility = Visibility.Collapsed;",
        "This read-only window shows live shared state;",
        "return;",
        "ClearLocalMeltdownPresentation();");
    AssertInOrder(
        clearLocalMeltdown,
        "leaving shared Meltdown must stop the timer, collapse stale controls, and clear local FX state",
        "StopLocalShutdownCountdown();",
        "MeltdownOverlay.Visibility = Visibility.Collapsed;",
        "CountdownBox.Visibility = Visibility.Collapsed;",
        "AbortButton.Visibility = Visibility.Collapsed;",
        "MeltdownCloseButton.Visibility = Visibility.Collapsed;",
        "_meltdownHandled = false;",
        "_meltdownFxStarted = false;",
        "ReactorFx.ScreenShake(_scrollVisual, 0);",
        "ReactorFx.RedStrobe(strobeVisual, false);");
    AssertInOrder(
        clearLocalMeltdown,
        "already-clear meltdown presentation may return early only when no strobe or countdown remains",
        "MeltdownOverlay.Visibility == Visibility.Collapsed",
        "&& !_meltdownFxStarted",
        "&& _countdownTimer is null",
        "_meltdownHandled = false;",
        "return;");
    AssertInOrder(
        stopCountdown,
        "local shutdown timer cleanup must stop, detach, and clear the page-owned timer",
        "if (_countdownTimer is null) return;",
        "_countdownTimer.Stop();",
        "_countdownTimer.Tick -= OnShutdownCountdownTick;",
        "_countdownTimer = null;");
    AssertContains(
        onMeltdown,
        "if (DispatcherQueue.HasThreadAccess)",
        "meltdown transfer does not detect synchronous UI-thread access");
    AssertInOrder(
        onMeltdown,
        "meltdown transfer must render synchronously on the UI thread and enqueue only off-thread",
        "if (DispatcherQueue.HasThreadAccess)",
        "RenderMeltdownOverlay();",
        "else",
        "DispatcherQueue.TryEnqueue(RenderMeltdownOverlay);");
    AssertInOrder(
        renderMeltdown,
        "meltdown rendering must distinguish failed first, then already-issued, before any armed countdown",
        "if (_session.RealShutdownFailed)",
        "ShowShutdownFailed();",
        "else if (_session.RealShutdownIssued)",
        "ShowShutdownIssued();",
        "else if (ReactorRealShutdownArm.Armed)");
    AssertContains(
        showShutdownFailed,
        "No retry will occur unless you disarm and explicitly arm again.",
        "shutdown-failure rendering does not explain the explicit retry gate");
    AssertContains(
        showShutdownFailed,
        "AbortButton.Visibility = Visibility.Collapsed;",
        "failed shutdown rendering leaves a meaningless ABORT control visible");
    AssertContains(
        showShutdownIssued,
        "It has already been issued and can no longer be cancelled from WinForge.",
        "issued shutdown rendering does not explain that cancellation is no longer possible");
    AssertContains(
        showShutdownIssued,
        "AbortButton.Visibility = Visibility.Collapsed;",
        "issued shutdown rendering leaves ABORT visible");
    AssertContains(
        showShutdownIssued,
        "MeltdownCloseButton.Visibility = Visibility.Collapsed;",
        "issued shutdown rendering allows the already-issued state to be dismissed/reset");
    AssertContains(
        startCountdown,
        "AbortButton.Visibility = Visibility.Visible;",
        "active countdown does not restore the valid ABORT control");
    AssertContains(
        abort,
        "if (_session.RealShutdownIssued) return;",
        "ABORT handler can mutate a shutdown that has already been issued");
    AssertInOrder(
        armChanged,
        "an explicit re-arm must reset the transferable failed sequence before starting again",
        "if (armed)",
        "_session.ResetRealShutdownSequence();",
        "_meltdownHandled = false;",
        "OnMeltdown();");
    AssertInOrder(
        armChanged,
        "disarm must latch session safety state before rendering synchronously on the UI thread or queuing off-thread",
        "_session.RealShutdownAborted = true;",
        "_session.RealShutdownDeadlineUtc = null;",
        "void RenderDisarmed()",
        "if (DispatcherQueue.HasThreadAccess)",
        "RenderDisarmed();",
        "else",
        "DispatcherQueue.TryEnqueue(RenderDisarmed);");
    AssertInOrder(
        renderDisarmed,
        "a stale queued OFF render must recheck ARM, current authority, and shared Meltdown before stopping a fresh countdown",
        "if (ReactorRealShutdownArm.Armed",
        "|| !ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)",
        "|| _sim.Mode != ReactorMode.Meltdown)",
        "return;",
        "ShowShutdownAborted(");

    AssertInOrder(
        releaseForeground,
        "real effects must survive a driver handoff and be restored only after the last owner",
        "_foregroundOwners.RemoveAll(",
        "if (_foregroundOwners.Count > 0)",
        "PublishForegroundDriverChanged(previousDriver);",
        "return false;",
        "ReactorRealShutdownArm.Armed = false;",
        "ReactorRealShutdownArm.CancelPendingVisibleArm();",
        "_session.ResetRealShutdownSequence();",
        "ReactorHomeAssistantMirror.I.RestoreOff();",
        "_backgroundTimer.Start();",
        "PublishForegroundDriverChanged(previousDriver);",
        "return true;");
    AssertContains(
        releaseForeground,
        "ReactorRealShutdownArm.Armed = false;",
        "last-owner release does not automatically disarm real shutdown");
    AssertContains(
        releaseForeground,
        "ReactorRealShutdownArm.CancelPendingVisibleArm();",
        "last-owner release does not cancel a pending visible-arm request");
    AssertContains(
        releaseForeground,
        "ReactorHomeAssistantMirror.I.RestoreOff();",
        "last-owner release does not restore Home Assistant entities to off");

    AssertInOrder(
        moduleConstructor,
        "every loaded page must defer pending-ARM consumption until it is still loaded and owns current authority",
        "AttachSessionHandlers();",
        "ReactorSessionRuntime.I.RegisterForeground(_session, _runtimeOwnerToken);",
        "CrashLogger.Guard(\"Reactor.Render\", Render);",
        "TryApplyPendingDeepLink();",
        "DispatcherQueue.TryEnqueue(",
        "Microsoft.UI.Dispatching.DispatcherQueuePriority.Low",
        "if (IsLoaded",
        "ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)",
        "ReactorRealShutdownArm.ConsumePendingVisibleArm()",
        "ReactorRealShutdownArm.Armed = true;");
    AssertNotContains(
        moduleConstructor,
        "armWhenControlRoomReady",
        "a superseded page still consumes the pending ARM request before its deferred authority check");
    Equal(
        1,
        Count(moduleConstructor, "ReactorRealShutdownArm.ConsumePendingVisibleArm()"),
        "deferred pending-arm consume count");
    Equal(
        1,
        Count(moduleConstructor, "ReactorRealShutdownArm.Armed = true;"),
        "deferred pending-arm assignment count");
    AssertInOrder(
        moduleConstructor,
        "page-owned real effects must be restored only when ReleaseForeground reports the last owner",
        "bool movedToBackground =",
        "ReactorSessionRuntime.I.ReleaseForeground(_session, _runtimeOwnerToken);",
        "if (movedToBackground)",
        "ReactorAudioEngine.I.StopVoices();",
        "ReleaseKeepAwake(force: true);",
        "ReactorSystemLinkService.I.RestoreAllAsync();");
    AssertContains(
        lastOwnerUnload,
        "ReactorAudioEngine.I.StopVoices();",
        "last-owner unload does not stop the process-global reactor voices");
    Equal(
        1,
        Count(moduleConstructor, "ReactorAudioEngine.I.StopVoices();"),
        "page-unload global voice-stop count");
    AssertNotContains(
        driverChanged,
        "ReactorAudioEngine.I.StopVoices();",
        "foreground-driver handoff silences process-global reactor audio");
    Equal(1, Count(moduleConstructor, "ReleaseKeepAwake(force: true);"), "last-owner keep-awake release count");
    Equal(
        1,
        Count(moduleConstructor, "ReactorSystemLinkService.I.RestoreAllAsync();"),
        "last-owner Windows-setting restore count");

    foreach (string renderCall in new[]
             {
                 "UpdateAnnunciator(dt);",
                 "UpdateStatusBanner();",
                 "UpdateGauges();",
                 "UpdateAlarmTiles();",
                 "UpdateMimic();",
                 "UpdateAutoStartOverlay();",
                 "UpdateStripCharts();",
                 "UpdateNisPanels();",
                 "UpdateCsfPanel();",
                 "UpdateRpsPanel();",
                 "UpdateControlsLive();",
                 "UpdateStatusApiCard();",
             })
    {
        AssertContains(observerTick, renderCall, $"read-only observer does not keep {renderCall} live");
    }
    foreach (string forbiddenEffect in new[]
             {
                 "_simClock += dt;",
                 "_sim.DriveAutoStart(dt);",
                 "_sim.Update(dt);",
                 "ReactorStatusApiService.I.Publish();",
                 "UpdateKeepAwake();",
                 "UpdateSysLink(dt);",
                 "DriveHomeAssistant();",
                 "UpdateAudio();",
                 "MaybeHardSave(now);",
             })
    {
        AssertNotContains(
            observerTick,
            forbiddenEffect,
            $"read-only observer can advance shared physics or real effects via {forbiddenEffect}");
    }
    AssertInOrder(
        observerTick,
        "observer tick must refresh read-only state, synchronize the shared meltdown overlay, then return",
        "UpdateStatusBanner();",
        "UpdateDriverUi(false);",
        "SyncObserverMeltdownPresentation();",
        "if (_sim.Mode == ReactorMode.Meltdown)",
        "AnimateMeltdown(dt);",
        "return;");

    AssertContains(moduleXaml, "x:Name=\"ObserverInfoBar\"", "read-only observer InfoBar is missing");
    AssertContains(moduleXaml, "IsClosable=\"False\"", "read-only observer InfoBar can be dismissed while controls stay disabled");
    AssertInOrder(
        driverUi,
        "driver UI state must open a read-only notice before disabling named control surfaces",
        "ObserverInfoBar.IsOpen = !isDriver;",
        "\"Read-only reactor observer\"",
        "ControlsSurface.IsEnabled = isDriver;",
        "ScenarioCombo.IsEnabled = isDriver;",
        "IsolateSgToggle.IsEnabled = isDriver;",
        "AmsacDefeatToggle.IsEnabled = isDriver;",
        "ScramButton.IsEnabled = isDriver;",
        "ResetTripButton.IsEnabled = isDriver;",
        "AutoRunToggle.IsEnabled = isDriver;",
        "RevMeterMarkButton.IsEnabled = isDriver;",
        "OpenControlRoomButton.IsEnabled = isDriver;",
        "ChecklistWidgetButton.IsEnabled = isDriver;",
        "OpenWidgetsButton.IsEnabled = isDriver;");
    foreach (string controlName in new[]
             {
                 "ControlsSurface",
                 "ScenarioCombo",
                 "IsolateSgToggle",
                 "AmsacDefeatToggle",
                 "ScramButton",
                 "ResetTripButton",
                 "AutoRunToggle",
                 "RevMeterMarkButton",
                 "OpenControlRoomButton",
                 "ChecklistWidgetButton",
                 "OpenWidgetsButton",
             })
    {
        AssertContains(moduleXaml, $"x:Name=\"{controlName}\"", $"named driver-only surface {controlName} is missing from XAML");
    }

    Equal(
        2,
        Count(moduleConstructor, "UpdateDriverUi(ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken));"),
        "page-load driver UI applications");
    AssertInOrder(
        moduleConstructor,
        "page load must reapply authority after dynamic builders create additional controls",
        "ReactorSessionRuntime.I.RegisterForeground(_session, _runtimeOwnerToken);",
        "UpdateDriverUi(ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken));",
        "CrashLogger.Guard(\"Reactor.BuildControls\", BuildControls);",
        "CrashLogger.Guard(\"Reactor.BuildScenarioCombo\", BuildScenarioCombo);",
        "CrashLogger.Guard(\"Reactor.Render\", Render);",
        "UpdateDriverUi(ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken));");

    AssertInOrder(
        buildScenarioCombo,
        "scenario construction must suppress SelectionChanged and display the canonical session scenario",
        "bool wasSyncing = _syncingControlValues;",
        "_syncingControlValues = true;",
        "ScenarioCombo.SelectedIndex = ScenarioIndex(_sim.ActiveScenario);",
        "finally { _syncingControlValues = wasSyncing; }");
    AssertNotContains(
        buildScenarioCombo,
        "ScenarioCombo.SelectedIndex = 0",
        "scenario construction resets a shared active transient to Normal");
    AssertNotContains(
        buildScenarioCombo,
        "_sim.TriggerScenario",
        "scenario construction mutates the shared simulation");
    AssertInOrder(
        scenarioChanged,
        "scenario changes must reject synchronization callbacks and observer input before mutating the shared sim",
        "if (_syncingControlValues",
        "|| !ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)",
        "return;",
        "_sim.TriggerScenario(");
    AssertInOrder(
        syncControlValues,
        "live control synchronization must hold its suppression guard through top-level and generated controls",
        "_syncingControlValues = true;",
        "try",
        "SyncTopLevelControls();",
        "foreach (var sync in _controlSyncers)",
        "finally",
        "_syncingControlValues = false;");
    AssertInOrder(
        syncTopLevelControls,
        "top-level scenario controls must reflect shared simulation state instead of resetting it",
        "int scenarioIndex = ScenarioIndex(_sim.ActiveScenario);",
        "if (ScenarioCombo.SelectedIndex != scenarioIndex)",
        "ScenarioCombo.SelectedIndex = scenarioIndex;",
        "bool isolated = _sim.ActiveScenario == ReactorScenario.MainSteamLineBreak",
        "? _sim.MslbIsolated",
        ": _sim.SgtrIsolated;",
        "AmsacDefeatToggle.IsChecked != _sim.AmsacDefeated",
        "AmsacDefeatToggle.IsChecked = _sim.AmsacDefeated;");
    AssertNotContains(
        syncTopLevelControls,
        "_sim.TriggerScenario",
        "top-level synchronization retriggers or resets the shared scenario");
    AssertNotContains(
        syncTopLevelControls,
        "_sim.AmsacDefeated =",
        "top-level synchronization writes display state back into the shared sim");

    AssertInOrder(
        isolateSg,
        "observer isolate-SG input is not rejected before mutating shared scenario state",
        "if (!ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)) return;",
        "_sim.MslbIsolated = on;");
    AssertInOrder(
        defeatAmsac,
        "observer AMSAC input is not rejected before mutating shared scenario state",
        "if (!ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)) return;",
        "_sim.AmsacDefeated =");
    AssertInOrder(
        openControlRoom,
        "observer can launch an interactive full control-room companion",
        "if (!ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)) return;",
        "new ReactorHtmlWindow(");
    AssertInOrder(
        openChecklistWidget,
        "observer can launch an interactive startup-checklist companion",
        "if (!ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)) return;",
        "new ReactorStartupChecklistWindow(");
    AssertInOrder(
        openWidgets,
        "observer can launch a mutating SCRAM widget",
        "if (!ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)) return;",
        "new ReactorWidgetWindow(_sim, WidgetKind.Scram)");

    AssertInOrder(
        driverChanged,
        "authority demotion must disable local input and close all mutating companions immediately",
        "UpdateDriverUi(false);",
        "ReactorWindowManager.CloseInteractiveSurfaces();",
        "StopLocalShutdownCountdown();");
    AssertInOrder(
        moduleConstructor,
        "only a page that was authoritative may close mutating companions before relinquishing authority",
        "bool wasForegroundDriver =",
        "ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken);",
        "if (wasForegroundDriver)",
        "ReactorWindowManager.CloseInteractiveSurfaces();",
        "ReactorSessionRuntime.I.ReleaseForeground(_session, _runtimeOwnerToken);");
    Equal(
        2,
        Count(module, "ReactorWindowManager.CloseInteractiveSurfaces();"),
        "mutating-companion close paths");
    AssertInOrder(
        closeInteractiveSurfaces,
        "interactive-surface cleanup must include every full control surface and mutating widget",
        "if (w is ReactorHtmlWindow",
        "or ReactorControlRoomWindow",
        "or ReactorStartupChecklistWindow",
        "|| w is ReactorWidgetWindow { CanMutateSimulation: true }",
        "w.Close();");

    AssertContains(
        applyAutoStart,
        "if (_session.CommandLineAutoStartApplied || !App.AutoStartReactor) return;",
        "command-line auto-start is not guarded once per canonical session");
    AssertInOrder(
        applyAutoStart,
        "command-line auto-start latch must be set before applying the preset",
        "_session.CommandLineAutoStartApplied = true;",
        "_sim.ApplyAutoStartPreset();");
    AssertNotContains(module, "private bool _autoStartApplied", "command-line auto-start still uses a page-local latch");

    AssertContains(
        restoreHomeAssistant,
        "_lastAlarmOn = null;",
        "Home Assistant restore does not reset alarm edge memory");
    AssertContains(
        restoreHomeAssistant,
        "_lastGenOn = null;",
        "Home Assistant restore does not reset generation edge memory");
    AssertContains(
        restoreHomeAssistant,
        "await _ha.LightOff(id)",
        "Home Assistant restore does not turn selected lights off");
    AssertContains(
        restoreHomeAssistant,
        "await _ha.TurnOff(id)",
        "Home Assistant restore does not turn selected switches off");
}

static void SettingsArmRequestRequiresVisibleReactor()
{
    string settings = Source();
    string armState = Source("ReactorHomeAssistantMirror.cs");
    string module = Source("ReactorModule.xaml.cs");

    string toggle = MethodBody(settings, "private void Arm_Toggled(object sender, RoutedEventArgs e)");
    string consumePending = MethodBody(armState, "public static bool ConsumePendingVisibleArm()");
    string moduleConstructor = MethodBody(module, "public ReactorModule()");

    AssertContains(
        armState,
        "private static bool _pendingVisibleArm;",
        "real-shutdown ARM requests have no harmless pending-visible state");
    AssertContains(
        armState,
        "public static void RequestArmWhenControlRoomVisible() => _pendingVisibleArm = true;",
        "settings cannot request ARM for the next visible control room");
    AssertInOrder(
        consumePending,
        "pending visible ARM must be one-shot",
        "bool pending = _pendingVisibleArm;",
        "_pendingVisibleArm = false;",
        "return pending;");
    AssertContains(
        armState,
        "public static void CancelPendingVisibleArm() => _pendingVisibleArm = false;",
        "pending visible ARM cannot be cancelled");

    AssertContains(toggle, "if (_suppress) return;", "ARM toggle ignores its programmatic-update guard");
    AssertInOrder(
        toggle,
        "arming without a visible control room must stay disarmed and navigate back before returning",
        "if (ArmToggle.IsOn && !ReactorSessionRuntime.I.HasForegroundDriver)",
        "ReactorRealShutdownArm.RequestArmWhenControlRoomVisible();",
        "ArmToggle.IsOn = false;",
        "ReactorRealShutdownArm.Armed = false;",
        "AppNotificationService.Publish(",
        "Navigator.GoToModule?.Invoke(\"module.reactor\");",
        "return;");
    AssertContains(
        toggle,
        "if (!ArmToggle.IsOn) ReactorRealShutdownArm.CancelPendingVisibleArm();",
        "turning ARM off does not cancel a pending request");
    AssertContains(
        toggle,
        "ReactorRealShutdownArm.Armed = ArmToggle.IsOn;",
        "visible-control-room ARM changes do not update the in-memory safety gate");
    AssertInOrder(
        moduleConstructor,
        "the requested ARM must remain pending until a deferred callback confirms the page still owns authority",
        "ReactorSessionRuntime.I.RegisterForeground(_session, _runtimeOwnerToken);",
        "TryApplyPendingDeepLink();",
        "DispatcherQueue.TryEnqueue(",
        "Microsoft.UI.Dispatching.DispatcherQueuePriority.Low",
        "if (IsLoaded",
        "ReactorSessionRuntime.I.IsForegroundDriver(_runtimeOwnerToken)",
        "ReactorRealShutdownArm.ConsumePendingVisibleArm()",
        "ReactorRealShutdownArm.Armed = true;");
    AssertNotContains(
        moduleConstructor,
        "armWhenControlRoomReady",
        "a page still consumes pending ARM before it can be superseded by a newer foreground driver");
    Equal(
        1,
        Count(moduleConstructor, "ReactorRealShutdownArm.ConsumePendingVisibleArm()"),
        "pending ARM consumption inside the authoritative deferred callback");
}

static void SystemLinkLifecycleCancelsStaleRestore()
{
    string source = Source("ReactorSystemLinkService.cs");
    string enable = MethodBody(source, "public bool Enable()");
    string enableCore = MethodBody(source, "private void EnableCore(int generation)");
    string restore = MethodBody(source, "public void RestoreAll()");
    string restoreAsync = MethodBody(source, "public Task RestoreAllAsync()");
    string takeSnapshot = MethodBody(source, "private RestoreSnapshot? TakeRestoreSnapshotLocked()");
    string applyRestore = MethodBody(source, "private static void ApplyRestoreSnapshot(RestoreSnapshot snapshot)");
    string completeRestore = MethodBody(source, "private void CompleteRestore()");
    string apply = MethodBody(source, "public void Apply(ReactorSimService sim, double dt)");

    AssertContains(source, "private int _generation;", "system link has no lifecycle generation");
    AssertContains(source, "private int _enableBusy;", "system link has no enable single-flight state");
    AssertContains(source, "private bool _enableRequested;", "system link cannot remember enable during an in-flight probe");
    AssertContains(source, "private bool _restoreBusy;", "system link has no restore single-flight state");
    AssertContains(
        source,
        "private readonly object _lifecycleGate = new();",
        "system-link lifecycle transitions do not share one lock");
    AssertNotContains(
        source,
        "RestoreAllCore",
        "system link still has an unlocked restore helper that can perform slow OS work under lifecycle state");

    AssertInOrder(
        enable,
        "Enable must advance generation under the lifecycle lock before any fast return",
        "EnabledSetting = true;",
        "lock (_lifecycleGate)",
        "generation = Interlocked.Increment(ref _generation);",
        "if (Active) return true;");
    AssertInOrder(
        enable,
        "Enable during an already-claimed restore must queue a retry before considering the old snapshot",
        "if (_restoreBusy)",
        "_enableRequested = true;",
        "return true;",
        "if (_snapshotCaptured)",
        "_active = true;");
    AssertInOrder(
        enable,
        "Enable calls during an in-flight probe must request a retry instead of disappearing",
        "if (_enableBusy == 1)",
        "_enableRequested = true;",
        "return true;",
        "_enableBusy = 1;",
        "_enableRequested = false;",
        "Task.Run(() => EnableCore(generation))");

    AssertInOrder(
        enableCore,
        "EnableCore must reject stale probe results while holding the lifecycle lock",
        "lock (_lifecycleGate)",
        "if (Volatile.Read(ref _generation) != generation || !EnabledSetting) return;",
        "_snapshotCaptured = true;",
        "_active = true;");
    AssertInOrder(
        enableCore,
        "EnableCore must clear busy state and retry a queued visible enable after its probe",
        "finally",
        "lock (_lifecycleGate)",
        "_enableBusy = 0;",
        "retry = _enableRequested && EnabledSetting && !Active;",
        "_enableRequested = false;",
        "if (retry) Enable();");

    AssertContains(
        restore,
        "while (Volatile.Read(ref _applyBusy) == 1)",
        "synchronous restore can overlap an in-flight OS apply");
    AssertNotContains(
        restore,
        "for (",
        "synchronous restore still abandons an in-flight apply after a bounded retry");
    AssertInOrder(
        restore,
        "synchronous restore must claim and clear the snapshot under the lifecycle lock, then restore outside it",
        "while (Volatile.Read(ref _applyBusy) == 1)",
        "RestoreSnapshot? snapshot;",
        "lock (_lifecycleGate)",
        "Interlocked.Increment(ref _generation);",
        "_active = false;",
        "if (_restoreBusy) return;",
        "snapshot = TakeRestoreSnapshotLocked();",
        "if (snapshot is not null) _restoreBusy = true;",
        "if (snapshot is null) return;",
        "ApplyRestoreSnapshot(snapshot);",
        "finally { CompleteRestore(); }");

    AssertInOrder(
        restoreAsync,
        "async restore must own a generation under the same lock before queuing work",
        "lock (_lifecycleGate)",
        "restoreGeneration = Interlocked.Increment(ref _generation);",
        "_active = false;",
        "Task.Run(async () =>");
    AssertContains(
        restoreAsync,
        "while (Volatile.Read(ref _applyBusy) == 1)",
        "async restore can overlap an in-flight OS apply");
    AssertNotContains(
        restoreAsync,
        "for (",
        "async restore still abandons an in-flight apply after a bounded retry");
    AssertInOrder(
        restoreAsync,
        "async restore must reject a stale generation, claim the snapshot under lock, and restore outside it",
        "while (Volatile.Read(ref _applyBusy) == 1)",
        "RestoreSnapshot? snapshot;",
        "lock (_lifecycleGate)",
        "if (Volatile.Read(ref _generation) != restoreGeneration) return;",
        "if (_restoreBusy) return;",
        "snapshot = TakeRestoreSnapshotLocked();",
        "if (snapshot is not null) _restoreBusy = true;",
        "if (snapshot is null) return;",
        "ApplyRestoreSnapshot(snapshot);",
        "finally { CompleteRestore(); }");

    AssertInOrder(
        takeSnapshot,
        "snapshot claim must copy and clear every original before releasing the lifecycle lock",
        "if (!_snapshotCaptured) return null;",
        "var snapshot = new RestoreSnapshot(",
        "_origScheme,",
        "_origAccentBgr,",
        "_origBrightness,",
        "_origVolume);",
        "_origScheme = null;",
        "_origAccentBgr = null;",
        "_origBrightness = null;",
        "_origVolume = null;",
        "_snapshotCaptured = false;",
        "return snapshot;");
    AssertNotContains(
        takeSnapshot,
        "TrySet",
        "snapshot claim performs slow OS restoration while holding the lifecycle lock");
    AssertNotContains(
        takeSnapshot,
        "TryApply",
        "snapshot claim performs slow registry restoration while holding the lifecycle lock");
    AssertInOrder(
        applyRestore,
        "claimed originals are not restored through the expected OS paths",
        "TrySetActiveScheme",
        "TryApplyAccentBgr",
        "TrySetBrightness",
        "TrySetVolume");
    AssertInOrder(
        completeRestore,
        "restore completion must release its flight and consume a queued Enable before retrying outside the lock",
        "lock (_lifecycleGate)",
        "_restoreBusy = false;",
        "retry = _enableRequested && EnabledSetting;",
        "_enableRequested = false;",
        "if (retry) Enable();");

    AssertContains(
        apply,
        "int generation = Volatile.Read(ref _generation);",
        "queued system-link Apply work does not capture its lifecycle generation");
    Assert(
        Count(apply, "Volatile.Read(ref _generation) != generation") >= 4,
        "queued system-link Apply work does not recheck generation between real effects");
}

static void HomeAssistantLifecycleCancelsStaleRestore()
{
    string runtime = Source("ReactorSessionRuntime.cs");
    string mirror = Source("ReactorHomeAssistantMirror.cs");

    string registerForeground = MethodBody(
        runtime,
        "public void RegisterForeground(ReactorSimulationSession session, string ownerToken)");
    string resume = MethodBody(mirror, "public void Resume()");
    string enabled = MethodBody(mirror, "public bool Enabled");
    string drive = MethodBody(mirror, "public void Drive(bool alarmActive, bool generating)");
    string push = MethodBody(
        mirror,
        "private async Task PushAsync(bool needAlarm, bool alarmActive, bool needGen, bool generating, DateTime pushedAtUtc)");
    string restore = MethodBody(mirror, "public void RestoreOff()");

    AssertContains(
        mirror,
        "private int _busy;",
        "Home Assistant Drive and RestoreOff do not share an atomic single-flight gate");
    AssertContains(
        mirror,
        "private int _lifecycleGeneration;",
        "Home Assistant mirror has no visible/background lifecycle generation");
    AssertNotContains(mirror, "private bool _busy;", "Home Assistant single-flight gate is still non-atomic");

    AssertInOrder(
        registerForeground,
        "a new visible reactor must cancel stale HA OFF work before publishing its driver handoff",
        "_foregroundOwners.Add(ownerToken);",
        "ReactorHomeAssistantMirror.I.Resume();",
        "PublishForegroundDriverChanged(previousDriver);");
    AssertInOrder(
        resume,
        "Resume while disabled must preserve its pending OFF cleanup; enabled resumes invalidate stale work and reassert",
        "if (!_enabled) return;",
        "Interlocked.Increment(ref _lifecycleGeneration);",
        "_lastAlarmOn = null;",
        "_lastGenOn = null;",
        "_lastAlarmAssertUtc = DateTime.MinValue;",
        "_lastGenAssertUtc = DateTime.MinValue;");
    AssertInOrder(
        enabled,
        "enabling the mirror must start a resumed epoch while disabling restores OFF",
        "if (value)",
        "Resume();",
        "else",
        "RestoreOff();");

    AssertContains(
        drive,
        "Interlocked.CompareExchange(ref _busy, 1, 0) != 0",
        "HA Drive does not atomically acquire the shared single-flight gate");
    AssertContains(
        push,
        "finally { Volatile.Write(ref _busy, 0); }",
        "HA Drive does not release the shared single-flight gate");
    Equal(
        2,
        Count(mirror, "Interlocked.CompareExchange(ref _busy, 1, 0)"),
        "shared HA single-flight acquisition count");
    Equal(
        2,
        Count(mirror, "Volatile.Write(ref _busy, 0)"),
        "shared HA single-flight release count");

    AssertInOrder(
        restore,
        "RestoreOff must capture a new lifecycle generation before queuing OFF work",
        "int restoreGeneration = Interlocked.Increment(ref _lifecycleGeneration);",
        "_lastAlarmOn = null;",
        "_lastGenOn = null;",
        "Task.Run(async () =>");
    AssertInOrder(
        restore,
        "RestoreOff must wait without abandonment and reject a stale generation before acquiring the shared flight",
        "while (true)",
        "if (Volatile.Read(ref _lifecycleGeneration) != restoreGeneration) return;",
        "Interlocked.CompareExchange(ref _busy, 1, 0)",
        "break;",
        "await Task.Delay(25).ConfigureAwait(false);");
    AssertNotContains(
        restore,
        "for (",
        "RestoreOff still abandons cleanup after a bounded retry window");
    AssertNotContains(
        restore,
        "ownsFlight",
        "RestoreOff still needs a split ownership flag instead of one outer finally");
    Equal(
        1,
        Count(restore, "finally"),
        "RestoreOff outer finally count");
    Equal(
        1,
        Count(restore, "Volatile.Write(ref _busy, 0)"),
        "RestoreOff shared-flight release count");
    Assert(
        Count(restore, "Volatile.Read(ref _lifecycleGeneration) != restoreGeneration") >= 4,
        "RestoreOff does not cancel stale OFF work before each external entity operation");
    AssertContains(
        restore,
        "bool stillRestoring =",
        "RestoreOff does not distinguish a completed restore from a resumed visible epoch");
    AssertInOrder(
        restore,
        "stale RestoreOff completion must leave caches unknown so the resumed driver reasserts",
        "Volatile.Read(ref _lifecycleGeneration) == restoreGeneration;",
        "_lastAlarmOn = stillRestoring ? false : null;",
        "_lastGenOn = stillRestoring ? false : null;",
        "Volatile.Write(ref _busy, 0);");

    AssertContains(
        drive,
        "_lastAlarmOn != alarmActive",
        "resumed null alarm cache cannot trigger immediate HA reassertion");
    AssertContains(
        drive,
        "_lastGenOn != generating",
        "resumed null generation cache cannot trigger immediate HA reassertion");
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

static void AssertBefore(string text, string first, string second, string message)
{
    int firstIndex = text.IndexOf(first, StringComparison.Ordinal);
    int secondIndex = text.IndexOf(second, StringComparison.Ordinal);
    if (firstIndex < 0 || secondIndex < 0 || firstIndex >= secondIndex)
        throw new InvalidOperationException(message);
}

static void AssertInOrder(string text, string message, params string[] values)
{
    int searchFrom = 0;
    foreach (string value in values)
    {
        int found = text.IndexOf(value, searchFrom, StringComparison.Ordinal);
        if (found < 0)
            throw new InvalidOperationException($"{message}: missing or out of order '{value}'.");
        searchFrom = found + value.Length;
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
