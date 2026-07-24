using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("zero process IDs fail closed before COM activation", RejectsZeroPid);
Run("negative process IDs fail closed before COM activation", RejectsNegativePid);
Run("clear-to-system-default rejects invalid process IDs", RejectsInvalidClearPid);
Run("session interface aliases are released through one RCW owner", SessionAliasesReleaseOnce);
Run("policy-config aliases are released through the activation owner", PolicyConfigAliasReleasesOnce);
Run("activation helpers release incompatible objects only", ActivationHelpersOwnOneRcw);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} audio interop safety tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} audio interop safety tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

void RejectsZeroPid()
    => Assert(!AudioPolicyConfig.SetAppDefaultDevice(0, "fixture-endpoint"), "PID 0 reached the routing boundary");

void RejectsNegativePid()
    => Assert(!AudioPolicyConfig.SetAppDefaultDevice(-1, "fixture-endpoint"), "negative PID reached the routing boundary");

void RejectsInvalidClearPid()
    => Assert(!AudioPolicyConfig.ClearAppDefaultDevice(0), "invalid PID clear was accepted");

void SessionAliasesReleaseOnce()
{
    var source = ReadAudioMixerSource();
    Equal(3, Count(source, "Release(ctrl);"), "session owner release count");
    Equal(0, Count(source, "Release(ctrl2);"), "session-control alias release count");
    Equal(0, Count(source, "Release(vol);"), "simple-volume alias release count");
}

void PolicyConfigAliasReleasesOnce()
{
    var body = MethodBody(ReadAudioMixerSource(), "public static void SetDefaultEndpoint(");
    Assert(body.Contains("Release(client);", StringComparison.Ordinal), "activation owner is not released");
    Assert(!body.Contains("Release(cfg);", StringComparison.Ordinal), "policy interface alias is released separately");
}

void ActivationHelpersOwnOneRcw()
{
    var source = ReadAudioMixerSource();
    foreach (var signature in new[]
    {
        "private static IMMDeviceEnumerator CreateEnumerator(",
        "private static IAudioEndpointVolume ActivateEndpointVolume(",
        "private static IAudioSessionManager2 ActivateSessionManager("
    })
    {
        var body = MethodBody(source, signature);
        Equal(1, Count(body, "Release(o);"), $"incompatible activation cleanup for {signature}");
        Equal(0, Count(body, "ReleaseComObject"), $"direct activation release for {signature}");
    }
}

static string ReadAudioMixerSource()
{
    var path = Path.Combine(AppContext.BaseDirectory, "AudioMixer.cs");
    if (!File.Exists(path)) throw new FileNotFoundException("Copied AudioMixer source is unavailable.", path);
    return File.ReadAllText(path);
}

static string MethodBody(string source, string signature)
{
    var signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
    if (signatureIndex < 0) throw new InvalidOperationException($"Could not find '{signature}'.");
    var open = source.IndexOf('{', signatureIndex);
    if (open < 0) throw new InvalidOperationException($"Could not find the body for '{signature}'.");

    var depth = 0;
    for (var i = open; i < source.Length; i++)
    {
        if (source[i] == '{') depth++;
        else if (source[i] == '}' && --depth == 0) return source[open..(i + 1)];
    }
    throw new InvalidOperationException($"Could not find the end of '{signature}'.");
}

static int Count(string value, string needle)
{
    var count = 0;
    var offset = 0;
    while ((offset = value.IndexOf(needle, offset, StringComparison.Ordinal)) >= 0)
    {
        count++;
        offset += needle.Length;
    }
    return count;
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
