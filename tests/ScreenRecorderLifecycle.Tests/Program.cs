using System.Diagnostics;
using System.Reflection;
using WinForge.Services;

var root = Path.Combine(Path.GetTempPath(), "WinForge.ScreenRecorderLifecycle.Tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var script = Path.Combine(root, "stderr-heavy-recorder.cmd");
File.WriteAllText(script, """
@echo off
for /L %%i in (1,1,10000) do @echo recorder-progress-012345678901234567890123456789012345678901234567890123456789 1>&2
set /p line=
if /I "%line%"=="q" exit /b 0
exit /b 1
""");

try
{
    MediaService.IsInstalled = true;
    MediaService.FFmpeg = script;
    var started = ScreenRecorder.Start(Path.Combine(root, "unused.mp4"), 30);
    Assert(started.Success, "isolated recorder fixture did not start");

    var process = ActiveProcess() ?? throw new InvalidOperationException("recording process was not retained");
    var stopTask = ScreenRecorder.Stop();
    if (await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(12))) != stopTask)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        throw new InvalidOperationException("Stop did not finish while the fixture produced redirected stderr");
    }

    Assert((await stopTask).Success, "Stop did not report the fixture as saved");
    Console.WriteLine("PASS 1/1 Screen Recorder lifecycle tests");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"FAIL Screen Recorder lifecycle test: {ex.Message}");
    return 1;
}
finally
{
    try
    {
        var process = ActiveProcess();
        if (process is { HasExited: false }) process.Kill(entireProcessTree: true);
    }
    catch { }
    try { Directory.Delete(root, recursive: true); } catch { }
}

static Process? ActiveProcess() =>
    (Process?)typeof(ScreenRecorder).GetField("_proc", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
