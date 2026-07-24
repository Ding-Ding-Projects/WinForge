using System.Collections.Concurrent;
using System.Globalization;
using WinForge.Services;

var tests = new (string Name, Func<Task> Body)[]
{
    ("EBU R128 parser accepts ffmpeg input_* fields", LoudnessParser),
    ("EBU R128 workflow measures then applies all measured fields", LoudnessWorkflow),
    ("silence trim covers leading trailing and internal gaps", SilenceWorkflow),
    ("vidstab detect and transform are sequenced and scratch is removed", StabilizeWorkflow),
    ("cropdetect uses the final valid crop rectangle", CropWorkflow),
    ("concat demuxer escapes paths and stream-copies selected clips", ConcatWorkflow),
    ("NVENC detection requires a successful hardware probe", NvencDetection),
    ("NVENC encode re-probes selected hardware codec", NvencEncode),
    ("target-size workflow probes duration and runs two x264 passes", TargetSizeWorkflow),
    ("target bitrate rejects impossible caps", TargetBitrateGuard),
    ("subtitle workflow builds safe burn-in and soft-mux vectors", SubtitleWorkflow),
    ("chapter JSON is parsed and every chapter is stream-copied", ChapterWorkflow),
    ("HEIC and JXL batch conversion is bounded and collision-safe", PhotoBatchWorkflow),
    ("image metadata strip removes global and stream metadata", MetadataWorkflow),
    ("failure preserves a pre-existing destination", FailedOutputPreserved),
    ("cancellation removes owned workspace and staged files", CancellationCleanup),
    ("filter escaping protects drive colons quotes commas and brackets", FilterEscaping),
};

int passed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Body();
        Console.WriteLine($"PASS  {test.Name}");
        passed++;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAIL  {test.Name}: {ex.Message}");
    }
}

Console.WriteLine($"{passed}/{tests.Length} tests passed.");
return passed == tests.Length ? 0 : 1;

static Task LoudnessParser()
{
    const string sample = "noise\n{\n  \"input_i\" : \"-21.34\",\n  \"input_tp\" : \"-0.52\",\n  \"input_lra\" : \"5.10\",\n  \"input_thresh\" : \"-31.70\"\n}\nnoise";
    True(MediaWorkflowExecutor.TryParseLoudnessMeasurement(sample, out var value), "measurement JSON was not parsed");
    Equal(-21.34, value.Integrated, "integrated loudness");
    Equal(-0.52, value.TruePeak, "true peak");
    Equal(5.10, value.Range, "loudness range");
    Equal(-31.70, value.Threshold, "threshold");
    var filter = MediaWorkflowExecutor.BuildSecondPassLoudnessFilter(value);
    Contains(filter, "measured_I=-21.34", "second-pass integrated field");
    Contains(filter, "measured_TP=-0.52", "second-pass true-peak field");
    Contains(filter, "measured_LRA=5.1", "second-pass range field");
    Contains(filter, "measured_thresh=-31.7", "second-pass threshold field");
    Contains(filter, "linear=true", "second-pass linear mode");
    return Task.CompletedTask;
}

static async Task LoudnessWorkflow()
{
    await WithFixture(async f =>
    {
        var runner = new FakeRunner(invocation =>
        {
            if (invocation.Arguments.Contains("-f") && invocation.Arguments.Contains("null"))
                return Result(true, "{\"input_i\":\"-22.0\",\"input_tp\":\"-1.0\",\"input_lra\":\"4.5\",\"input_thresh\":\"-32.0\"}");
            WriteLastOutput(invocation);
            return Result(true, "encoded");
        });
        var executor = f.Executor(runner);
        var output = Path.Combine(f.Output, "normalized & safe.mp4");
        var result = await executor.NormalizeLoudnessAsync(f.Input, output);
        True(result.Success, result.Message);
        Equal(2, runner.Invocations.Count, "two-pass invocation count");
        var second = runner.Invocations[1].Arguments;
        Contains(string.Join("|", second), "measured_I=-22", "measured loudness was not forwarded");
        True(second.Contains(f.Input), "input path was not preserved as one argument");
        True(File.Exists(output), "staged output was not promoted");
    });
}

static async Task SilenceWorkflow()
{
    await WithFixture(async f =>
    {
        var runner = OutputRunner();
        var audio = Path.Combine(f.Root, "voice & literal.wav");
        File.WriteAllText(audio, "fixture");
        var executor = f.Executor(runner);
        var result = await executor.TrimSilenceAsync(audio, Path.Combine(f.Output, "trimmed.wav"));
        True(result.Success, result.Message);
        var filter = ArgumentAfter(runner.Invocations.Single().Arguments, "-af");
        Contains(filter, "start_periods=1", "leading silence option");
        Contains(filter, "stop_periods=-1", "internal-gap option");
        Contains(filter, "stop_silence=0.3", "trailing silence option");
        SequenceEqual(new[] { "0:a:0" }, runner.Invocations.Single().Arguments
            .SkipWhile(argument => argument != "-map").Skip(1).Take(1), "audio-only stream mapping");

        var rejectedVideo = await executor.TrimSilenceAsync(f.Input, Path.Combine(f.Output, "unsafe.mp4"));
        True(!rejectedVideo.Success, "video input was accepted even though gap collapse would desynchronize it");
        Equal(1, runner.Invocations.Count, "rejected video should not start ffmpeg");
    });
}

static async Task StabilizeWorkflow()
{
    await WithFixture(async f =>
    {
        var runner = new FakeRunner(invocation =>
        {
            var joined = string.Join(" ", invocation.Arguments);
            if (joined.Contains("vidstabdetect", StringComparison.Ordinal))
            {
                File.WriteAllText(Path.Combine(invocation.WorkingDirectory!, "transforms.trf"), "fixture");
                return Result(true, "detect");
            }
            WriteLastOutput(invocation);
            return Result(true, "transform");
        });
        var output = Path.Combine(f.Output, "stable.mp4");
        var result = await f.Executor(runner).StabilizeVideoAsync(f.Input, output);
        True(result.Success, result.Message);
        Equal(2, runner.Invocations.Count, "vidstab pass count");
        Contains(string.Join(" ", runner.Invocations[0].Arguments), "vidstabdetect", "detection pass");
        Contains(string.Join(" ", runner.Invocations[1].Arguments), "vidstabtransform", "transform pass");
        EmptyDirectory(f.Temp, "stabilization workspace leaked");
    });
}

static async Task CropWorkflow()
{
    await WithFixture(async f =>
    {
        var runner = new FakeRunner(invocation =>
        {
            if (invocation.Arguments.Contains("cropdetect=round=2"))
                return Result(true, "crop=100:100:0:0\nframe=2 crop=1920:800:0:140");
            WriteLastOutput(invocation);
            return Result(true, "crop encoded");
        });
        var result = await f.Executor(runner).AutoCropAsync(f.Input, Path.Combine(f.Output, "cropped.mp4"));
        True(result.Success, result.Message);
        True(!runner.Invocations[0].Arguments.Contains("-ss"), "short clips would be skipped by a fixed detection seek");
        Equal("crop=1920:800:0:140", ArgumentAfter(runner.Invocations[1].Arguments, "-vf"), "last crop rectangle");
    });
}

static async Task ConcatWorkflow()
{
    await WithFixture(async f =>
    {
        var second = Path.Combine(f.Root, "second clip's & literal.mp4");
        File.WriteAllText(second, "fixture");
        string? listContent = null;
        var runner = new FakeRunner(invocation =>
        {
            var list = ArgumentAfter(invocation.Arguments, "-i");
            listContent = File.ReadAllText(list);
            WriteLastOutput(invocation);
            return Result(true, "joined");
        });
        var result = await f.Executor(runner).ConcatCopyAsync(new[] { f.Input, second }, Path.Combine(f.Output, "joined.mp4"));
        True(result.Success, result.Message);
        Contains(listContent!, "'\\''", "apostrophe was not escaped in concat list");
        True(runner.Invocations.Single().Arguments.Contains("copy"), "concat did not stream-copy");
        EmptyDirectory(f.Temp, "concat list workspace leaked");
    });
}

static async Task NvencDetection()
{
    await WithFixture(async f =>
    {
        var runner = new FakeRunner(invocation =>
        {
            if (invocation.Arguments.Contains("-encoders"))
                return Result(true, " V....D h264_nvenc NVIDIA H.264\n V....D hevc_nvenc NVIDIA HEVC\n V....D av1_nvenc NVIDIA AV1");
            var codec = ArgumentAfter(invocation.Arguments, "-c:v");
            return Result(codec is "h264_nvenc" or "av1_nvenc", codec);
        });
        var result = await f.Executor(runner).DetectNvencAsync();
        True(result.Outcome.Success, result.Outcome.Message);
        SequenceEqual(new[] { "h264_nvenc", "av1_nvenc" }, result.Codecs, "working NVENC codec list");
        Equal(4, runner.Invocations.Count, "encoder listing plus three hardware probes");
    });
}

static async Task NvencEncode()
{
    await WithFixture(async f =>
    {
        var runner = new FakeRunner(invocation =>
        {
            if (invocation.Arguments.Contains("-f") && invocation.Arguments.Contains("lavfi")) return Result(true, "probe");
            WriteLastOutput(invocation);
            return Result(true, "encoded");
        });
        var result = await f.Executor(runner).EncodeNvencAsync(f.Input, Path.Combine(f.Output, "nvenc.mp4"), "hevc_nvenc", 26);
        True(result.Success, result.Message);
        Equal(2, runner.Invocations.Count, "hardware probe plus encode");
        var encode = runner.Invocations[1].Arguments;
        Equal("hevc_nvenc", ArgumentAfter(encode, "-c:v"), "selected encoder");
        Equal("26", ArgumentAfter(encode, "-cq"), "selected quality");
    });
}

static async Task TargetSizeWorkflow()
{
    await WithFixture(async f =>
    {
        var runner = new FakeRunner(invocation =>
        {
            if (invocation.Executable.Contains("probe", StringComparison.OrdinalIgnoreCase)) return Result(true, "60.0");
            if (invocation.Arguments.Contains("1") && invocation.Arguments.Contains("-pass")) return Result(true, "pass 1");
            WriteLastOutput(invocation);
            return Result(true, "pass 2");
        });
        var result = await f.Executor(runner).EncodeTargetSizeAsync(f.Input, Path.Combine(f.Output, "25mb.mp4"), 25, 128);
        True(result.Success, result.Message);
        Equal(3, runner.Invocations.Count, "duration probe plus two encode passes");
        var expected = MediaWorkflowExecutor.ComputeTargetVideoBitrateKbps(25, 60, 128).ToString(CultureInfo.InvariantCulture) + "k";
        Equal(expected, ArgumentAfter(runner.Invocations[1].Arguments, "-b:v"), "pass-one bitrate");
        Equal(expected, ArgumentAfter(runner.Invocations[2].Arguments, "-b:v"), "pass-two bitrate");
        Equal("128k", ArgumentAfter(runner.Invocations[2].Arguments, "-b:a"), "audio bitrate");
        EmptyDirectory(f.Temp, "x264 passlog workspace leaked");
    });
}

static Task TargetBitrateGuard()
{
    Throws<InvalidOperationException>(() => MediaWorkflowExecutor.ComputeTargetVideoBitrateKbps(1, 7_200, 320), "impossible cap was accepted");
    return Task.CompletedTask;
}

static async Task SubtitleWorkflow()
{
    await WithFixture(async f =>
    {
        var subtitle = Path.Combine(f.Root, "Canto, subs [final].srt");
        File.WriteAllText(subtitle, "1\n00:00:00,000 --> 00:00:01,000\n你好");
        var runner = OutputRunner();
        var executor = f.Executor(runner);
        var burn = await executor.AddSubtitlesAsync(f.Input, subtitle, Path.Combine(f.Output, "burn.mp4"), MediaSubtitleMode.BurnIn);
        var soft = await executor.AddSubtitlesAsync(f.Input, subtitle, Path.Combine(f.Output, "soft.mp4"), MediaSubtitleMode.SoftMux);
        True(burn.Success && soft.Success, "subtitle workflows failed");
        Contains(ArgumentAfter(runner.Invocations[0].Arguments, "-vf"), "subtitles=filename=", "libass filter missing");
        True(runner.Invocations[1].Arguments.Contains(subtitle), "subtitle path was not one soft-mux argument");
        Equal("mov_text", ArgumentAfter(runner.Invocations[1].Arguments, "-c:s"), "MP4 subtitle codec");
        True(runner.Invocations[1].Arguments.Contains("language=yue"), "Cantonese language metadata");
    });
}

static async Task ChapterWorkflow()
{
    await WithFixture(async f =>
    {
        const string json = "{\"chapters\":[{\"start_time\":\"0.0\",\"end_time\":\"5.5\",\"tags\":{\"title\":\"Intro/Start\"}},{\"start_time\":\"5.5\",\"end_time\":\"10.0\",\"tags\":{\"title\":\"Main\"}}]}";
        var runner = new FakeRunner(invocation =>
        {
            if (invocation.Executable.Contains("probe", StringComparison.OrdinalIgnoreCase)) return Result(true, json);
            WriteLastOutput(invocation);
            return Result(true, "split");
        });
        var executor = f.Executor(runner);
        var scan = await executor.ReadChaptersAsync(f.Input);
        Equal(2, scan.Chapters.Count, "chapter count");
        Equal(5.5, scan.Chapters[0].EndSeconds, "chapter end");
        var split = await executor.SplitChaptersAsync(f.Input, f.Output);
        True(split.Success, split.Message);
        Equal(2, split.Outputs!.Count, "split output count");
        True(split.Outputs.All(File.Exists), "chapter outputs missing");
        True(runner.Invocations.Where(i => i.Executable.Contains("mpeg", StringComparison.OrdinalIgnoreCase)).All(i => i.Arguments.Contains("copy")), "chapter split re-encoded");
    });
}

static async Task PhotoBatchWorkflow()
{
    await WithFixture(async f =>
    {
        var inputFolder = Path.Combine(f.Root, "photos");
        Directory.CreateDirectory(inputFolder);
        File.WriteAllText(Path.Combine(inputFolder, "same.heic"), "fixture");
        File.WriteAllText(Path.Combine(inputFolder, "same.jxl"), "fixture");
        File.WriteAllText(Path.Combine(inputFolder, "ignore.txt"), "fixture");
        var runner = OutputRunner();
        var result = await f.Executor(runner).ConvertPhotoBatchAsync(inputFolder, f.Output, MediaPhotoFormat.Png);
        True(result.Success, result.Message);
        Equal(2, result.Outputs!.Count, "photo output count");
        Equal(2, result.Outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "colliding stems overwrote one another");
        True(runner.Invocations.All(i => i.Arguments.Contains("1") && i.Arguments.Contains("-frames:v")), "single-frame guard missing");
    });
}

static async Task MetadataWorkflow()
{
    await WithFixture(async f =>
    {
        var image = Path.Combine(f.Root, "private.jpg");
        File.WriteAllText(image, "fixture");
        var runner = OutputRunner();
        var result = await f.Executor(runner).StripImageMetadataAsync(image, Path.Combine(f.Output, "clean.jpg"));
        True(result.Success, result.Message);
        var args = runner.Invocations.Single().Arguments;
        Equal("-1", ArgumentAfter(args, "-map_metadata"), "global metadata removal");
        Equal("-1", ArgumentAfter(args, "-map_metadata:s"), "stream metadata removal");
        Equal("copy", ArgumentAfter(args, "-c:v"), "image was re-encoded");
    });
}

static async Task FailedOutputPreserved()
{
    await WithFixture(async f =>
    {
        var output = Path.Combine(f.Output, "existing.mp4");
        File.WriteAllText(output, "keep me");
        var runner = new FakeRunner(_ => Result(false, "fixture failure", 2));
        var result = await f.Executor(runner).TrimSilenceAsync(f.Input, output);
        True(!result.Success, "failed command was reported as success");
        Equal("keep me", File.ReadAllText(output), "failed command replaced destination");
        True(!Directory.EnumerateFiles(f.Output).Any(p => Path.GetFileName(p).Contains(".winforge-", StringComparison.Ordinal)), "staged output leaked");
    });
}

static async Task CancellationCleanup()
{
    await WithFixture(async f =>
    {
        var runner = new BlockingRunner();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));
        var result = await f.Executor(runner).StabilizeVideoAsync(f.Input, Path.Combine(f.Output, "cancelled.mp4"), cts.Token);
        True(!result.Success && result.Message == "Cancelled.", "cancellation was not reported truthfully");
        EmptyDirectory(f.Temp, "cancelled workspace leaked");
        True(!Directory.EnumerateFiles(f.Output).Any(), "cancelled staged output leaked");
    });
}

static Task FilterEscaping()
{
    var root = Path.GetPathRoot(Environment.CurrentDirectory)!;
    var path = Path.Combine(root, "folder,one", "sub's[final].srt");
    var escaped = MediaWorkflowExecutor.EscapeFilterValue(path);
    Contains(escaped, "\\:", "drive colon was not escaped");
    Contains(escaped, "\\,", "comma was not escaped");
    Contains(escaped, "\\'", "quote was not escaped");
    Contains(escaped, "\\[", "opening bracket was not escaped");
    Contains(escaped, "\\]", "closing bracket was not escaped");
    return Task.CompletedTask;
}

static FakeRunner OutputRunner() => new(invocation =>
{
    WriteLastOutput(invocation);
    return Result(true, "ok");
});

static void WriteLastOutput(Invocation invocation)
{
    var path = invocation.Arguments.Last();
    if (!Path.IsPathRooted(path)) throw new InvalidOperationException($"Fixture expected a rooted staged output, got '{path}'.");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, "fixture output");
}

static MediaCommandResult Result(bool success, string output, int exitCode = 0) => new(success, output, exitCode);

static string ArgumentAfter(IReadOnlyList<string> arguments, string key)
{
    int index = arguments.ToList().IndexOf(key);
    if (index < 0 || index + 1 >= arguments.Count) throw new InvalidOperationException($"Argument '{key}' was not found.");
    return arguments[index + 1];
}

static async Task WithFixture(Func<Fixture, Task> body)
{
    var root = Path.Combine(Path.GetTempPath(), "WinForge-MediaWorkflowTests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var output = Path.Combine(root, "output");
        var temp = Path.Combine(root, "temp");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(temp);
        var input = Path.Combine(root, "input & literal.mp4");
        File.WriteAllText(input, "fixture");
        await body(new Fixture(root, input, output, temp));
    }
    finally
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}

static void EmptyDirectory(string path, string message)
{
    if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).Any())
        throw new InvalidOperationException(message);
}

static void True(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string message) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{message}: expected '{expected}', got '{actual}'");
}

static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
        throw new InvalidOperationException($"{message}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
}

static void Contains(string text, string expected, string message)
{
    if (!text.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"{message}: '{expected}' not found in '{text}'");
}

static void Throws<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException(message);
}

sealed record Fixture(string Root, string Input, string Output, string Temp)
{
    public MediaWorkflowExecutor Executor(IMediaCommandRunner runner)
        => new(runner, "fixture-ffmpeg.exe", "fixture-ffprobe.exe", Temp);
}

sealed record Invocation(string Executable, IReadOnlyList<string> Arguments, string? WorkingDirectory);

sealed class FakeRunner(Func<Invocation, MediaCommandResult> handler) : IMediaCommandRunner
{
    public List<Invocation> Invocations { get; } = new();

    public Task<MediaCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var invocation = new Invocation(executable, arguments.ToArray(), workingDirectory);
        Invocations.Add(invocation);
        return Task.FromResult(handler(invocation));
    }
}

sealed class BlockingRunner : IMediaCommandRunner
{
    public async Task<MediaCommandResult> RunAsync(string executable, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        return new MediaCommandResult(false, "unreachable", 1);
    }
}
