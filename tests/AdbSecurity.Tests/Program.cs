using System.Diagnostics;
using WinForge.Services;

var failures = new List<string>();
var passed = 0;

await RunAsync("rejects shell metacharacters in wireless endpoint", RejectsInjectedEndpoint);
await RunAsync("connect uses adb argument list", ConnectUsesArgumentList);
await RunAsync("remote shell text stays one adb argument", RemoteShellStaysRemote);
await RunAsync("device paths stay remote shell arguments", DevicePathsStayRemote);
Run("logcat uses an argument list", LogcatUsesArgumentList);
await RunAsync("invalid serial never reaches runner", InvalidSerialNeverRuns);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} ADB security tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} ADB security tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

async Task RunAsync(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

static async Task RejectsInjectedEndpoint()
{
    ShellRunner.Reset();
    var result = await AdbService.Connect("127.0.0.1:5555 & whoami");
    Assert(!result.Success, "unsafe endpoint was accepted");
    Equal(0, ShellRunner.Invocations.Count, "unsafe endpoint reached a process runner");
}

static async Task ConnectUsesArgumentList()
{
    ShellRunner.Reset();
    var result = await AdbService.Connect("192.168.1.25:5555");
    Assert(result.Success, "valid endpoint was rejected");
    var call = ShellRunner.Invocations.Single();
    Equal("adb", call.FileName, "runner executable");
    Assert(!call.Capture, "connect should execute rather than capture");
    Same(new[] { "connect", "192.168.1.25:5555" }, call.Arguments, "connect argv");
    Assert(!call.FileName.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase), "cmd.exe was used");
}

static async Task RemoteShellStaysRemote()
{
    ShellRunner.Reset();
    const string command = "echo hello; id && getprop ro.product.model";
    _ = await AdbService.Shell("emulator-5554", command);
    var call = ShellRunner.Invocations.Single();
    Equal("adb", call.FileName, "runner executable");
    Assert(call.Capture, "shell should capture adb output");
    Same(new[] { "-s", "emulator-5554", "shell", command }, call.Arguments, "remote shell argv");
}

static async Task DevicePathsStayRemote()
{
    ShellRunner.Reset();
    var result = await AdbService.Delete("emulator-5554", "/sdcard/it's safe;whoami");
    Assert(result.Success, "device delete was rejected");
    var call = ShellRunner.Invocations.Single();
    Equal("adb", call.FileName, "runner executable");
    Same(new[] { "-s", "emulator-5554", "shell", "'rm' '-rf' '/sdcard/it'\"'\"'s safe;whoami'" },
        call.Arguments, "device path argv");
}

static void LogcatUsesArgumentList()
{
    ProcessStartInfo? psi = AdbService.CreateLogcatStartInfo("emulator-5554", "ActivityManager:D *:S");
    Assert(psi is not null, "valid logcat process was not created");
    Equal("adb", psi!.FileName, "logcat executable");
    Equal(string.Empty, psi.Arguments, "legacy command-line arguments were populated");
    Same(new[] { "-s", "emulator-5554", "logcat", "ActivityManager:D", "*:S" }, psi.ArgumentList, "logcat argv");
}

static async Task InvalidSerialNeverRuns()
{
    ShellRunner.Reset();
    var result = await AdbService.Install("emulator-5554 & whoami", "C:\\safe.apk");
    Assert(!result.Success, "unsafe serial was accepted");
    Equal(0, ShellRunner.Invocations.Count, "unsafe serial reached a process runner");
}

static void Same(IReadOnlyList<string> expected, IReadOnlyList<string> actual, string label)
{
    Equal(expected.Count, actual.Count, label + " count");
    for (var i = 0; i < expected.Count; i++) Equal(expected[i], actual[i], label + $"[{i}]");
}

static void Equal<T>(T expected, T actual, string label) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{label}: expected '{expected}', got '{actual}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
