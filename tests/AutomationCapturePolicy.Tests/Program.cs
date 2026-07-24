using WinForge.Services;

var failures = new List<string>();
var passed = 0;

Run("accepts an absolute local PNG path", AcceptsLocalPng);
Run("rejects relative, non-PNG, UNC, and oversized paths", RejectsUnsupportedPaths);
Run("bounds automation dimensions and delay values", BoundsIntegerInputs);
Run("rejects malformed BGRA8 buffers", RejectsMalformedPixels);
Run("preserves opaque BGRA8 pixels", PreservesOpaquePixels);
Run("flattens transparent pixels to the selected background", FlattensTransparentPixels);
Run("composites premultiplied alpha and emits opaque pixels", CompositesPremultipliedPixels);
Run("driver atomically promotes every final capture path", DriverPromotesAtomically);
Run("capture failure logs omit target paths and exception messages", CaptureLogsAreSanitized);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} automation capture policy tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} automation capture policy tests");
return 1;

void Run(string name, Action test)
{
    try { test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception ex) { failures.Add($"FAIL {name}: {ex.Message}"); }
}

void AcceptsLocalPng()
{
    var requested = Path.Combine(Path.GetTempPath(), "WinForge Automation Capture", "capture.PNG");
    Assert(AutomationCapturePolicy.TryGetSupportedPath(requested, out var actual), "local PNG path was rejected");
    Equal(Path.GetFullPath(requested), actual, "canonical path");
}

void RejectsUnsupportedPaths()
{
    Assert(!AutomationCapturePolicy.TryGetSupportedPath("capture.png", out _), "relative path was accepted");
    Assert(!AutomationCapturePolicy.TryGetSupportedPath(Path.Combine(Path.GetTempPath(), "capture.jpg"), out _), "non-PNG path was accepted");
    Assert(!AutomationCapturePolicy.TryGetSupportedPath(@"\\server\share\capture.png", out _), "UNC path was accepted");
    Assert(!AutomationCapturePolicy.TryGetSupportedPath("C:\\" + new string('a', 1_022) + ".png", out _), "oversized path was accepted");
}

void BoundsIntegerInputs()
{
    Equal(640, AutomationCapturePolicy.ReadBoundedInt("640", 0, 640, 3_840), "minimum");
    Equal(3_840, AutomationCapturePolicy.ReadBoundedInt("3840", 0, 640, 3_840), "maximum");
    Equal(7, AutomationCapturePolicy.ReadBoundedInt("639", 7, 640, 3_840), "below minimum fallback");
    Equal(7, AutomationCapturePolicy.ReadBoundedInt("3841", 7, 640, 3_840), "above maximum fallback");
    Equal(7, AutomationCapturePolicy.ReadBoundedInt("not-a-number", 7, 640, 3_840), "invalid fallback");
    Equal(7, AutomationCapturePolicy.ReadBoundedInt(null, 7, 640, 3_840), "missing fallback");
}

void RejectsMalformedPixels()
{
    AssertThrows<InvalidOperationException>(
        () => AutomationCapturePolicy.FlattenPremultipliedPixels([0, 0, 0], 10, 20, 30),
        "malformed buffer was accepted");
}

void PreservesOpaquePixels()
{
    byte[] pixels = [1, 2, 3, 255];
    AutomationCapturePolicy.FlattenPremultipliedPixels(pixels, 10, 20, 30);
    EqualSequence([1, 2, 3, 255], pixels, "opaque pixel");
}

void FlattensTransparentPixels()
{
    byte[] pixels = [0, 0, 0, 0];
    AutomationCapturePolicy.FlattenPremultipliedPixels(pixels, 10, 20, 30);
    EqualSequence([10, 20, 30, 255], pixels, "transparent pixel");
}

void CompositesPremultipliedPixels()
{
    byte[] pixels = [50, 25, 10, 128];
    AutomationCapturePolicy.FlattenPremultipliedPixels(pixels, 10, 20, 30);
    EqualSequence([55, 35, 25, 255], pixels, "half-transparent pixel");
}

void DriverPromotesAtomically()
{
    var source = ReadCopiedSource("driver.ps1");
    Assert(source.Contains("MoveFileEx($sourceFull, $destinationFull, 0x9)", StringComparison.Ordinal),
        "driver does not use write-through atomic replacement");
    Equal(2, Count(source, "Publish-WfCapture $inProcessCapture $outFull"), "live-tree promotion count");
    Equal(1, Count(source, "Publish-WfCapture $finalCaptureTemp $outFull"), "PrintWindow promotion count");
    Assert(source.Contains("$bmp.Save($finalCaptureTemp", StringComparison.Ordinal),
        "PrintWindow does not save through a unique final temporary path");
    Assert(!source.Contains("$bmp.Save($outFull", StringComparison.Ordinal),
        "PrintWindow can still write directly to the requested evidence path");
    Assert(!source.Contains("Copy-Item -LiteralPath $inProcessCapture -Destination $outFull", StringComparison.Ordinal),
        "live-tree capture can still copy directly to the requested evidence path");
    Assert(source.Contains("Remove-WfCaptureFile $finalCaptureTemp", StringComparison.Ordinal),
        "final-promotion temporary cleanup is missing");
}

void CaptureLogsAreSanitized()
{
    var source = ReadCopiedSource("AutomationCaptureService.cs");
    Assert(!source.Contains("CrashLogger.Log(\"automation-capture\"", StringComparison.Ordinal),
        "capture failure still persists exception messages");
    var failureLine = source.Split('\n').Single(line => line.Contains("automation-capture-failed:", StringComparison.Ordinal));
    Assert(!failureLine.Contains("path", StringComparison.OrdinalIgnoreCase), "capture failure log includes a path value");
    Assert(!failureLine.Contains("Message", StringComparison.Ordinal), "capture failure log includes an exception message");
    Assert(failureLine.Contains("GetType().Name", StringComparison.Ordinal) && failureLine.Contains("HResult", StringComparison.Ordinal),
        "capture failure log lost its sanitized type/HRESULT diagnostic");
}

static string ReadCopiedSource(string name)
{
    var path = Path.Combine(AppContext.BaseDirectory, name);
    if (!File.Exists(path)) throw new FileNotFoundException("Expected copied capture source is unavailable.", path);
    return File.ReadAllText(path);
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

static void EqualSequence(IReadOnlyList<byte> expected, IReadOnlyList<byte> actual, string label)
{
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException($"{label}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}].");
}

static void AssertThrows<TException>(Action action, string message) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException(message);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
