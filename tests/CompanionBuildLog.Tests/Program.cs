using WinForge.Services;

var failures = new List<string>();

Run("writes a complete UTF-8 build transcript", WritesCompleteTranscript);
Run("sanitizes companion IDs for log file names", SanitizesFileNames);
Run("retains only the configured number of logs per companion", AppliesRetentionPerCompanion);
Run("fails open when persistent storage is unavailable", StorageFailureDoesNotBreakBuild);

if (failures.Count == 0)
{
    Console.WriteLine("PASS 4/4 companion build-log tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/4 companion build-log tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {name}: {ex.Message}");
    }
}

static void WritesCompleteTranscript()
{
    WithTempDirectory(root =>
    {
        string? path;
        using (var log = CompanionBuildLog.Start("imageforge", "WinForge Image Editor", "影像編輯器",
                   @"C:\app\native\imageforge\main.cpp", "WinForgeImageEditor.exe", root))
        {
            Assert(log.IsAvailable, log.Error ?? "log was unavailable");
            path = log.FilePath;
            log.AppendStatus("Looking for a C++ toolchain…", "搵緊 C++ 工具鏈…");
            log.AppendOutput("clang++: compiling main.cpp");
            log.AppendOutput("完成 ✓");
            log.Finish("SUCCESS", "Companion launched.");
        }

        Assert(path is not null && File.Exists(path), "the log file was not created");
        var text = File.ReadAllText(path!);
        AssertContains(text, "WinForge companion build log");
        AssertContains(text, "WinForge Image Editor · 影像編輯器");
        AssertContains(text, "Looking for a C++ toolchain… · 搵緊 C++ 工具鏈…");
        AssertContains(text, "clang++: compiling main.cpp");
        AssertContains(text, "完成 ✓");
        AssertContains(text, "Outcome: SUCCESS");
    });
}

static void SanitizesFileNames()
{
    Assert(CompanionBuildLog.SafeStem(@" ..\A:B/C? ") == "a-b-c", "unsafe ID was not normalized");
    Assert(CompanionBuildLog.SafeStem("...") == "companion", "empty normalized ID needs a fallback");
}

static void AppliesRetentionPerCompanion()
{
    WithTempDirectory(root =>
    {
        for (int i = 0; i < 6; i++)
        {
            using var log = CompanionBuildLog.Start("audioforge", "Audio", "音訊", "main.cpp", "audio.exe",
                root, retention: 3);
            Assert(log.IsAvailable, log.Error ?? "log was unavailable");
            log.AppendOutput($"attempt {i}");
            log.Finish("SUCCESS");
        }

        using (var other = CompanionBuildLog.Start("imageforge", "Image", "影像", "main.cpp", "image.exe",
                   root, retention: 3))
        {
            other.Finish("SUCCESS");
        }

        Assert(Directory.GetFiles(root, "audioforge-*.log").Length == 3,
            "audioforge retention did not keep exactly three logs");
        Assert(Directory.GetFiles(root, "imageforge-*.log").Length == 1,
            "retention for one companion deleted another companion's log");
    });
}

static void StorageFailureDoesNotBreakBuild()
{
    WithTempDirectory(root =>
    {
        var fileWhereDirectoryShouldBe = Path.Combine(root, "not-a-directory");
        File.WriteAllText(fileWhereDirectoryShouldBe, "occupied");
        using var log = CompanionBuildLog.Start("imageforge", "Image", "影像", "main.cpp", "image.exe",
            fileWhereDirectoryShouldBe);
        Assert(!log.IsAvailable, "storage failure unexpectedly produced a writer");
        log.AppendStatus("Still building", "繼續編譯");
        log.AppendOutput("this must remain a no-op");
        log.Finish("SUCCESS");
    });
}

static void WithTempDirectory(Action<string> test)
{
    var root = Path.Combine(Path.GetTempPath(), $"winforge-companion-log-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try { test(root); }
    finally
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void AssertContains(string text, string expected) =>
    Assert(text.Contains(expected, StringComparison.Ordinal), $"missing expected text: {expected}");
