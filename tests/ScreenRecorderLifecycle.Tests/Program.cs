using System.Diagnostics;
using System.Reflection;
using System.Text;
using WinForge.Services;

if (args.Length > 0 && args[0] == "-y" && args.Contains("gdigrab", StringComparer.OrdinalIgnoreCase))
    return await RunRecorderFixtureChildAsync();

var root = Path.Combine(Path.GetTempPath(), "WinForge.ScreenRecorderLifecycle.Tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    MediaService.IsInstalled = true;
    MediaService.FFmpeg = Path.Combine(AppContext.BaseDirectory, "ScreenRecorderLifecycle.Tests.exe");
    Assert(File.Exists(MediaService.FFmpeg), "isolated recorder fixture executable was not found");
    var started = ScreenRecorder.Start(Path.Combine(root, "unused.mp4"), 30);
    Assert(started.Success, "isolated recorder fixture did not start");

    var process = ActiveProcess() ?? throw new InvalidOperationException("recording process was not retained");
    var stopElapsed = Stopwatch.StartNew();
    var stopTask = ScreenRecorder.Stop();
    if (await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(12))) != stopTask)
    {
        try { if (!process.HasExited) process.TryKill(); } catch { }
        throw new InvalidOperationException("Stop did not finish while the fixture produced redirected stderr");
    }

    var stopped = await stopTask;
    stopElapsed.Stop();
    Assert(stopped.Success,
        $"Stop did not report the fixture as saved after {stopElapsed.ElapsedMilliseconds} ms: {stopped.En}");
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
        if (process is { HasExited: false }) process.TryKill();
    }
    catch { }
    try { Directory.Delete(root, recursive: true); } catch { }
}

static IScreenRecorderProcess? ActiveProcess() =>
    (IScreenRecorderProcess?)typeof(ScreenRecorder).GetField("_proc", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

static async Task<int> RunRecorderFixtureChildAsync()
{
    byte[] line = Encoding.ASCII.GetBytes(
        "recorder-progress-012345678901234567890123456789012345678901234567890123456789\r\n");
    byte[] diagnostics = new byte[line.Length * 10_000];
    for (var index = 0; index < 10_000; index++)
        Buffer.BlockCopy(line, 0, diagnostics, index * line.Length, line.Length);

    Stream error = Console.OpenStandardError();
    await error.WriteAsync(diagnostics);
    await error.FlushAsync();
    string? command = await Console.In.ReadLineAsync();
    return string.Equals(command, "q", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
