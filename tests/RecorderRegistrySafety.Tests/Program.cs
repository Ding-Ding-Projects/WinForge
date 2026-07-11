using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("recording startup begins a managed stderr drain", StartsManagedErrorDrain);
Run("graceful recorder stop is bounded and does not force-kill", GracefulStop);
Run("stalled recorder is force-stopped after bounded graceful wait", ForcedStop);
Run("non-exiting recorder reports a bounded failure instead of hanging", StillRunningStop);
Run("never-completing process waits remain bounded", NeverCompletingWaitRemainsBounded);
Run("registry UI deletion reports a confirmed backend success", RegistryDeleteSuccess);
Run("registry UI deletion surfaces backend failure while old callers remain best-effort", RegistryDeleteFailureAndBestEffort);
Run("screen recorder source enters the managed process lifecycle", ScreenRecorderUsesManagedLifecycle);
Run("registry editor source displays success only through the result API", RegistryEditorUsesTruthfulDeleteResult);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} recorder lifecycle and registry-result tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} recorder lifecycle and registry-result tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

void StartsManagedErrorDrain()
{
    using var process = new FakeRecorderProcess();
    ScreenRecorderProcessLifecycle.Begin(process);
    Assert(process.ErrorDrainStarted, "lifecycle did not start the redirected stderr drain");
}

void GracefulStop()
{
    using var process = new FakeRecorderProcess(exitResults: [true]);
    var result = ScreenRecorderProcessLifecycle.StopAsync(process).GetAwaiter().GetResult();

    Equal(ScreenRecorderStopStatus.Saved, result.Status, "graceful stop status");
    Assert(result.Exited, "graceful stop did not report exit");
    Equal(0, process.KillCount, "graceful stop kill count");
    EqualSequence([ScreenRecorderProcessLifecycle.StopCommandTimeout], process.QuitTimeouts, "quit timeout");
    EqualSequence([ScreenRecorderProcessLifecycle.GracefulExitTimeout], process.ExitTimeouts, "exit timeout");
}

void ForcedStop()
{
    using var process = new FakeRecorderProcess(exitResults: [false, true]);
    var result = ScreenRecorderProcessLifecycle.StopAsync(process).GetAwaiter().GetResult();

    Equal(ScreenRecorderStopStatus.ForcedStop, result.Status, "forced stop status");
    Assert(result.Exited, "force-stopped process did not report exit");
    Equal(1, process.KillCount, "force-stop kill count");
    EqualSequence(
        [ScreenRecorderProcessLifecycle.GracefulExitTimeout, ScreenRecorderProcessLifecycle.ForcedExitTimeout],
        process.ExitTimeouts,
        "bounded exit timeouts");
}

void StillRunningStop()
{
    using var process = new FakeRecorderProcess(exitResults: [false, false]);
    var result = ScreenRecorderProcessLifecycle.StopAsync(process).GetAwaiter().GetResult();

    Equal(ScreenRecorderStopStatus.StillRunning, result.Status, "still-running stop status");
    Assert(!result.Exited, "still-running process was falsely reported as exited");
    Equal(1, process.KillCount, "still-running kill count");
    EqualSequence(
        [ScreenRecorderProcessLifecycle.GracefulExitTimeout, ScreenRecorderProcessLifecycle.ForcedExitTimeout],
        process.ExitTimeouts,
        "still-running bounded exit timeouts");
}

void NeverCompletingWaitRemainsBounded()
{
    using var process = new FakeRecorderProcess { NeverCompletesExit = true };
    var shortDeadline = TimeSpan.FromMilliseconds(10);
    var task = ScreenRecorderProcessLifecycle.StopAsync(process, shortDeadline, shortDeadline, shortDeadline);
    Assert(task.Wait(TimeSpan.FromSeconds(1)), "bounded lifecycle did not return from a never-completing wait");
    var result = task.GetAwaiter().GetResult();

    Equal(ScreenRecorderStopStatus.StillRunning, result.Status, "never-completing wait status");
    Assert(!result.Exited, "never-completing wait was falsely reported as exited");
    Equal(1, process.KillCount, "never-completing wait kill count");
    EqualSequence([shortDeadline, shortDeadline], process.ExitTimeouts, "never-completing bounded exit timeouts");
}

void RegistryDeleteSuccess()
{
    var backend = new FakeRegistryDeleteBackend();
    var result = RegistryHelper.TryDeleteValue(backend, RegRoot.HKCU, @"Software\Fixture", "Value");

    Assert(result.Success, "successful backend deletion was not reported as success");
    Assert(result.Error is null, "successful backend deletion exposed an error");
    Equal(1, backend.CallCount, "successful backend call count");
}

void RegistryDeleteFailureAndBestEffort()
{
    var backend = new FakeRegistryDeleteBackend { Error = new UnauthorizedAccessException("fixture access denied") };
    var result = RegistryHelper.TryDeleteValue(backend, RegRoot.HKLM, @"Software\Fixture", "Value");

    Assert(!result.Success, "failed backend deletion was falsely reported as success");
    Assert(result.Error is UnauthorizedAccessException, "failed backend deletion lost its error type");
    RegistryHelper.DeleteValue(backend, RegRoot.HKLM, @"Software\Fixture", "Value");
    Equal(2, backend.CallCount, "best-effort caller did not invoke the same deletion boundary");
}

void ScreenRecorderUsesManagedLifecycle()
{
    var source = ReadCopiedSource("ScreenRecorder.cs");
    Assert(source.Contains("RedirectStandardError = true", StringComparison.Ordinal), "Screen Recorder no longer redirects ffmpeg diagnostics");
    Assert(source.Contains("new ProcessScreenRecorderProcess(process)", StringComparison.Ordinal), "Screen Recorder does not wrap ffmpeg in the managed process adapter");
    Assert(source.Contains("ScreenRecorderProcessLifecycle.Begin(recorderProcess)", StringComparison.Ordinal), "Screen Recorder does not begin the managed stderr drain");
    Assert(source.Contains("ScreenRecorderProcessLifecycle.StopAsync(p)", StringComparison.Ordinal), "Screen Recorder does not use bounded stop lifecycle");
}

void RegistryEditorUsesTruthfulDeleteResult()
{
    var source = ReadCopiedSource("RegistryEditor.xaml.cs");
    var deleteBody = MethodBody(source, "private async void Delete_Click(");
    Assert(deleteBody.Contains("RegistryHelper.TryDeleteValue", StringComparison.Ordinal), "Registry Editor does not use the result-returning delete API");
    Assert(!deleteBody.Contains("RegistryHelper.DeleteValue(", StringComparison.Ordinal), "Registry Editor still uses the best-effort delete API");

    int failureGate = deleteBody.IndexOf("if (!result.Success)", StringComparison.Ordinal);
    int successNotice = deleteBody.IndexOf("ShowOk(", StringComparison.Ordinal);
    Assert(failureGate >= 0 && successNotice > failureGate, "Registry Editor can show success before it checks the deletion result");
}

static string ReadCopiedSource(string name)
{
    var path = Path.Combine(AppContext.BaseDirectory, name);
    if (!File.Exists(path)) throw new FileNotFoundException("Expected copied source was not available to the regression harness.", path);
    return File.ReadAllText(path);
}

static string MethodBody(string source, string signature)
{
    int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    if (signatureIndex < 0) throw new InvalidOperationException($"Could not find method signature '{signature}'.");
    int open = source.IndexOf('{', signatureIndex);
    if (open < 0) throw new InvalidOperationException($"Could not find opening brace for '{signature}'.");

    int depth = 0;
    for (int i = open; i < source.Length; i++)
    {
        if (source[i] == '{') depth++;
        else if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
    }

    throw new InvalidOperationException($"Could not find closing brace for '{signature}'.");
}

static void Equal<T>(T expected, T actual, string label) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void EqualSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string label)
{
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException($"{label}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

sealed class FakeRecorderProcess : IScreenRecorderProcess
{
    private readonly Queue<bool> _exitResults;
    private readonly TaskCompletionSource<bool> _neverCompletes = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public FakeRecorderProcess(IEnumerable<bool>? exitResults = null)
        => _exitResults = new Queue<bool>(exitResults ?? [true]);

    public bool HasExited { get; private set; }
    public bool ErrorDrainStarted { get; private set; }
    public bool NeverCompletesExit { get; init; }
    public int KillCount { get; private set; }
    public List<TimeSpan> QuitTimeouts { get; } = [];
    public List<TimeSpan> ExitTimeouts { get; } = [];

    public void BeginErrorDrain() => ErrorDrainStarted = true;

    public Task<bool> TrySendQuitAsync(TimeSpan timeout)
    {
        QuitTimeouts.Add(timeout);
        return Task.FromResult(true);
    }

    public Task<bool> WaitForExitAsync(TimeSpan timeout)
    {
        ExitTimeouts.Add(timeout);
        if (NeverCompletesExit) return _neverCompletes.Task;
        bool exits = _exitResults.Count > 0 && _exitResults.Dequeue();
        if (exits) HasExited = true;
        return Task.FromResult(exits);
    }

    public bool TryKill()
    {
        KillCount++;
        return true;
    }

    public void Dispose() { }
}

sealed class FakeRegistryDeleteBackend : RegistryHelper.IValueDeleteBackend
{
    public Exception? Error { get; init; }
    public int CallCount { get; private set; }

    public void DeleteValue(RegRoot root, string path, string name)
    {
        CallCount++;
        if (Error is not null) throw Error;
    }
}
