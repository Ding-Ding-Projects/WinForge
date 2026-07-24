using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>One safely separated ffmpeg/ffprobe invocation.</summary>
public sealed record MediaCommandResult(bool Success, string Output, int ExitCode = 0);

/// <summary>
/// Process seam used by the media workflow engine. Implementations must preserve every argument boundary
/// (ProcessStartInfo.ArgumentList in the application) and must terminate their child when cancelled.
/// </summary>
public interface IMediaCommandRunner
{
    Task<MediaCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken);
}

public enum MediaSubtitleMode
{
    BurnIn,
    SoftMux,
}

public enum MediaPhotoFormat
{
    Jpeg,
    Png,
}

public sealed record MediaLoudnessMeasurement(double Integrated, double TruePeak, double Range, double Threshold);

public sealed record MediaChapter(int Index, string Title, double StartSeconds, double EndSeconds)
{
    public double DurationSeconds => Math.Max(0, EndSeconds - StartSeconds);
}

public sealed record MediaWorkflowOutcome(
    bool Success,
    string Message,
    string Diagnostics = "",
    IReadOnlyList<string>? Outputs = null)
{
    public static MediaWorkflowOutcome Ok(string message, string diagnostics = "", IReadOnlyList<string>? outputs = null)
        => new(true, message, diagnostics, outputs);

    public static MediaWorkflowOutcome Fail(string message, string diagnostics = "", IReadOnlyList<string>? outputs = null)
        => new(false, message, diagnostics, outputs);
}

public sealed record MediaChapterOutcome(MediaWorkflowOutcome Outcome, IReadOnlyList<MediaChapter> Chapters);

public sealed record MediaNvencOutcome(MediaWorkflowOutcome Outcome, IReadOnlyList<string> Codecs);

/// <summary>
/// Bounded, cancellable ffmpeg/ffprobe workflows used by the Media page. Every user path is passed as a
/// distinct argument; generated outputs are first written to a unique same-directory file and promoted only
/// after ffmpeg succeeds. Multi-pass scratch data lives below one owned temporary directory and is removed in
/// a finally block.
/// </summary>
public sealed class MediaWorkflowExecutor
{
    public const int MaxBatchFiles = 500;
    public const int MaxChapters = 200;
    public const int MaxPathLength = 32_000;

    private static readonly string[] NvencCodecs = { "h264_nvenc", "hevc_nvenc", "av1_nvenc" };
    private static readonly HashSet<string> PhotoInputs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".heic", ".heif", ".jxl",
    };
    private static readonly HashSet<string> AudioInputs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus",
    };
    private static readonly HashSet<string> MetadataInputs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".jxl", ".tif", ".tiff",
    };

    private readonly IMediaCommandRunner _runner;
    private readonly string _ffmpeg;
    private readonly string _ffprobe;
    private readonly string _tempRoot;

    public MediaWorkflowExecutor(
        IMediaCommandRunner runner,
        string ffmpeg,
        string ffprobe,
        string? tempRoot = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _ffmpeg = string.IsNullOrWhiteSpace(ffmpeg) ? throw new ArgumentException("ffmpeg path is required.", nameof(ffmpeg)) : ffmpeg;
        _ffprobe = string.IsNullOrWhiteSpace(ffprobe) ? throw new ArgumentException("ffprobe path is required.", nameof(ffprobe)) : ffprobe;
        _tempRoot = Path.GetFullPath(tempRoot ?? Path.Combine(Path.GetTempPath(), "WinForge", "MediaWorkflows"));
    }

    public async Task<MediaWorkflowOutcome> NormalizeLoudnessAsync(
        string input,
        string output,
        CancellationToken cancellationToken = default)
    {
        try
        {
            input = RequireInputFile(input);
            output = RequireOutputFile(output, input);
            cancellationToken.ThrowIfCancellationRequested();

            var measurement = await RunAsync(_ffmpeg, new[]
            {
                "-hide_banner", "-nostdin", "-i", input,
                "-af", "loudnorm=I=-16:TP=-1.5:LRA=11:print_format=json",
                "-f", "null", NullDevice,
            }, Path.GetDirectoryName(input), cancellationToken);
            if (!measurement.Success)
                return MediaWorkflowOutcome.Fail("Loudness measurement failed.", measurement.Output);
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryParseLoudnessMeasurement(measurement.Output, out var measured))
                return MediaWorkflowOutcome.Fail("ffmpeg did not return a complete EBU R128 measurement.", measurement.Output);

            var filter = BuildSecondPassLoudnessFilter(measured);
            return await RunStagedOutputAsync(output, staged => new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-af", filter,
                "-map", "0:v?", "-map", "0:a?", "-c:v", "copy", staged,
            }, "EBU R128 normalization completed in two passes.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
    }

    public async Task<MediaWorkflowOutcome> TrimSilenceAsync(
        string input,
        string output,
        CancellationToken cancellationToken = default)
    {
        try
        {
            input = RequireInputFile(input);
            output = RequireOutputFile(output, input);
            if (!AudioInputs.Contains(Path.GetExtension(input)))
                return MediaWorkflowOutcome.Fail("Silence-gap removal accepts audio files only so collapsing gaps cannot desynchronize a video track.");
            const string filter = "silenceremove=start_periods=1:start_silence=0.1:start_threshold=-50dB:stop_periods=-1:stop_silence=0.3:stop_threshold=-50dB:detection=peak";
            return await RunStagedOutputAsync(output, staged => new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-af", filter, "-map", "0:a:0", staged,
            }, "Leading, trailing, and internal silence was trimmed.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
    }

    public async Task<MediaWorkflowOutcome> StabilizeVideoAsync(
        string input,
        string output,
        CancellationToken cancellationToken = default)
    {
        string? workspace = null;
        string? staged = null;
        try
        {
            input = RequireInputFile(input);
            output = RequireOutputFile(output, input);
            workspace = CreateWorkspace();
            staged = CreateStagedOutput(output);
            var transforms = Path.Combine(workspace, "transforms.trf");
            var detect = $"vidstabdetect=shakiness=8:accuracy=15:result='{EscapeFilterValue(transforms)}'";

            var first = await RunAsync(_ffmpeg, new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-vf", detect, "-f", "null", NullDevice,
            }, workspace, cancellationToken);
            if (!first.Success)
                return MediaWorkflowOutcome.Fail("Video stabilization analysis failed.", first.Output);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(transforms))
                return MediaWorkflowOutcome.Fail("ffmpeg reported success but did not create the stabilization transform file.", first.Output);

            var transform = $"vidstabtransform=input='{EscapeFilterValue(transforms)}':smoothing=30:zoom=0,unsharp=5:5:0.8:3:3:0.4";
            var second = await RunAsync(_ffmpeg, new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-vf", transform, "-c:v", "libx264", "-crf", "20", "-preset", "medium", "-c:a", "copy", staged,
            }, workspace, cancellationToken);
            return PromoteOrFail(second, staged, output, "Two-pass vidstab stabilization completed.");
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
        finally
        {
            TryDeleteFile(staged);
            TryDeleteDirectory(workspace);
        }
    }

    public async Task<MediaWorkflowOutcome> AutoCropAsync(
        string input,
        string output,
        CancellationToken cancellationToken = default)
    {
        try
        {
            input = RequireInputFile(input);
            output = RequireOutputFile(output, input);
            var detection = await RunAsync(_ffmpeg, new[]
            {
                "-hide_banner", "-nostdin", "-i", input,
                "-frames:v", "200", "-vf", "cropdetect=round=2", "-f", "null", NullDevice,
            }, Path.GetDirectoryName(input), cancellationToken);
            if (!detection.Success)
                return MediaWorkflowOutcome.Fail("Black-bar detection failed.", detection.Output);
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryParseCropRectangle(detection.Output, out var crop))
                return MediaWorkflowOutcome.Fail("No stable crop rectangle was found.", detection.Output);

            return await RunStagedOutputAsync(output, staged => new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-vf", crop, "-c:v", "libx264", "-crf", "20", "-preset", "medium", "-c:a", "copy", staged,
            }, $"Detected and applied {crop}.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
    }

    public async Task<MediaWorkflowOutcome> ConcatCopyAsync(
        IReadOnlyList<string> clips,
        string output,
        CancellationToken cancellationToken = default)
    {
        string? workspace = null;
        try
        {
            if (clips is null || clips.Count < 2)
                return MediaWorkflowOutcome.Fail("Choose at least two clips to join.");
            if (clips.Count > MaxBatchFiles)
                return MediaWorkflowOutcome.Fail($"At most {MaxBatchFiles} clips can be joined at once.");

            var inputs = clips.Select(RequireInputFile).ToArray();
            output = RequireOutputFile(output, inputs);
            workspace = CreateWorkspace();
            var listPath = Path.Combine(workspace, "concat.txt");
            await File.WriteAllTextAsync(listPath, BuildConcatListContent(inputs), new UTF8Encoding(false), cancellationToken);

            return await RunStagedOutputAsync(output, staged => new[]
            {
                "-hide_banner", "-nostdin", "-y", "-f", "concat", "-safe", "0", "-i", listPath,
                "-c", "copy", "-avoid_negative_ts", "make_zero", staged,
            }, "Clips were joined without re-encoding.", cancellationToken, workspace);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    public async Task<MediaNvencOutcome> DetectNvencAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var list = await RunAsync(_ffmpeg, new[] { "-hide_banner", "-encoders" }, null, cancellationToken);
            if (!list.Success)
                return new(MediaWorkflowOutcome.Fail("Could not inspect ffmpeg encoders.", list.Output), Array.Empty<string>());

            var compiled = ParseNvencEncoders(list.Output);
            var working = new List<string>();
            foreach (var codec in NvencCodecs.Where(compiled.Contains))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var probe = await ProbeNvencAsync(codec, cancellationToken);
                if (probe.Success) working.Add(codec);
            }

            return working.Count == 0
                ? new(MediaWorkflowOutcome.Fail("No working NVIDIA NVENC encoder was detected. The ffmpeg build and GPU/driver must both support it."), working)
                : new(MediaWorkflowOutcome.Ok($"Detected {working.Count} working NVENC encoder(s).", string.Join(", ", working)), working);
        }
        catch (OperationCanceledException)
        {
            return new(Cancelled(), Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return new(MediaWorkflowOutcome.Fail(ex.Message), Array.Empty<string>());
        }
    }

    public async Task<MediaWorkflowOutcome> EncodeNvencAsync(
        string input,
        string output,
        string codec,
        int quality,
        CancellationToken cancellationToken = default)
    {
        try
        {
            input = RequireInputFile(input);
            output = RequireOutputFile(output, input);
            if (!NvencCodecs.Contains(codec, StringComparer.Ordinal))
                return MediaWorkflowOutcome.Fail("Choose a supported NVENC codec.");
            if (quality is < 0 or > 51)
                return MediaWorkflowOutcome.Fail("NVENC quality must be between 0 and 51.");

            var probe = await ProbeNvencAsync(codec, cancellationToken);
            if (!probe.Success)
                return MediaWorkflowOutcome.Fail($"{codec} is present in ffmpeg but failed its hardware capability probe.", probe.Output);

            return await RunStagedOutputAsync(output, staged => new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-c:v", codec, "-preset", "p5", "-tune", "hq", "-rc", "vbr", "-cq", quality.ToString(CultureInfo.InvariantCulture),
                "-b:v", "0", "-c:a", "copy", staged,
            }, $"Encoded with {codec} after a successful hardware probe.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
    }

    public async Task<MediaWorkflowOutcome> EncodeTargetSizeAsync(
        string input,
        string output,
        double targetMegabytes,
        int audioKbps,
        CancellationToken cancellationToken = default)
    {
        string? workspace = null;
        string? staged = null;
        try
        {
            input = RequireInputFile(input);
            output = RequireOutputFile(output, input);
            if (targetMegabytes is < 1 or > 100_000 || double.IsNaN(targetMegabytes) || double.IsInfinity(targetMegabytes))
                return MediaWorkflowOutcome.Fail("Target size must be between 1 and 100,000 MiB.");
            if (audioKbps is < 32 or > 1_536)
                return MediaWorkflowOutcome.Fail("Audio bitrate must be between 32 and 1,536 kbps.");

            var durationProbe = await RunAsync(_ffprobe, new[]
            {
                "-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", input,
            }, Path.GetDirectoryName(input), cancellationToken);
            if (!durationProbe.Success || !TryParseDuration(durationProbe.Output, out var duration))
                return MediaWorkflowOutcome.Fail("Could not read a valid media duration.", durationProbe.Output);

            int videoKbps = ComputeTargetVideoBitrateKbps(targetMegabytes, duration, audioKbps);
            workspace = CreateWorkspace();
            staged = CreateStagedOutput(output);
            var passLog = Path.Combine(workspace, "x264-pass");
            var first = await RunAsync(_ffmpeg, new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-c:v", "libx264", "-b:v", $"{videoKbps}k", "-pass", "1", "-passlogfile", passLog,
                "-an", "-f", "null", NullDevice,
            }, workspace, cancellationToken);
            if (!first.Success)
                return MediaWorkflowOutcome.Fail("Target-size analysis pass failed.", first.Output);
            cancellationToken.ThrowIfCancellationRequested();

            var second = await RunAsync(_ffmpeg, new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-c:v", "libx264", "-b:v", $"{videoKbps}k", "-pass", "2", "-passlogfile", passLog,
                "-c:a", "aac", "-b:a", $"{audioKbps}k", "-movflags", "+faststart", staged,
            }, workspace, cancellationToken);
            return PromoteOrFail(second, staged, output, $"Two-pass target-size encode completed at {videoKbps} kbps video.");
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
        finally
        {
            TryDeleteFile(staged);
            TryDeleteDirectory(workspace);
        }
    }

    public async Task<MediaWorkflowOutcome> AddSubtitlesAsync(
        string input,
        string subtitle,
        string output,
        MediaSubtitleMode mode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            input = RequireInputFile(input);
            subtitle = RequireInputFile(subtitle);
            output = RequireOutputFile(output, new[] { input, subtitle });
            var subtitleExtension = Path.GetExtension(subtitle);
            if (!subtitleExtension.Equals(".srt", StringComparison.OrdinalIgnoreCase)
                && !subtitleExtension.Equals(".ass", StringComparison.OrdinalIgnoreCase))
                return MediaWorkflowOutcome.Fail("Subtitle input must be an SRT or ASS file.");

            if (mode == MediaSubtitleMode.BurnIn)
            {
                var filter = $"subtitles=filename='{EscapeFilterValue(subtitle)}':force_style='FontName=Microsoft JhengHei,FontSize=22'";
                return await RunStagedOutputAsync(output, staged => new[]
                {
                    "-hide_banner", "-nostdin", "-y", "-i", input,
                    "-vf", filter, "-c:v", "libx264", "-crf", "20", "-preset", "medium", "-c:a", "copy", staged,
                }, "Subtitles were burned into the video with libass.", cancellationToken);
            }

            var softCodec = IsMp4Family(output) ? "mov_text" : "copy";
            return await RunStagedOutputAsync(output, staged => new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input, "-i", subtitle,
                "-map", "0:v?", "-map", "0:a?", "-map", "1:0",
                "-c:v", "copy", "-c:a", "copy", "-c:s", softCodec,
                "-metadata:s:s:0", "language=yue", staged,
            }, "A toggleable subtitle track was muxed without re-encoding the video or audio.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
    }

    public async Task<MediaChapterOutcome> ReadChaptersAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            input = RequireInputFile(input);
            var probe = await RunAsync(_ffprobe, new[]
            {
                "-v", "error", "-show_chapters", "-print_format", "json", input,
            }, Path.GetDirectoryName(input), cancellationToken);
            if (!probe.Success)
                return new(MediaWorkflowOutcome.Fail("Could not read chapters.", probe.Output), Array.Empty<MediaChapter>());

            var chapters = ParseChapters(probe.Output);
            if (chapters.Count == 0)
                return new(MediaWorkflowOutcome.Fail("The selected media has no usable chapters.", probe.Output), chapters);
            if (chapters.Count > MaxChapters)
                return new(MediaWorkflowOutcome.Fail($"The file contains more than the supported {MaxChapters} chapters."), Array.Empty<MediaChapter>());

            return new(MediaWorkflowOutcome.Ok($"Read {chapters.Count} chapter(s).", probe.Output), chapters);
        }
        catch (OperationCanceledException)
        {
            return new(Cancelled(), Array.Empty<MediaChapter>());
        }
        catch (Exception ex)
        {
            return new(MediaWorkflowOutcome.Fail(ex.Message), Array.Empty<MediaChapter>());
        }
    }

    public async Task<MediaWorkflowOutcome> SplitChaptersAsync(
        string input,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        var completed = new List<string>();
        try
        {
            input = RequireInputFile(input);
            outputFolder = RequireExistingDirectory(outputFolder);
            var scan = await ReadChaptersAsync(input, cancellationToken);
            if (!scan.Outcome.Success) return scan.Outcome;

            var extension = Path.GetExtension(input);
            if (string.IsNullOrWhiteSpace(extension)) extension = ".mkv";
            foreach (var chapter in scan.Chapters)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var title = SanitizeFileName(chapter.Title);
                var output = Path.Combine(outputFolder, $"Chapter {chapter.Index:00} - {title}{extension}");
                output = GetUniqueDestination(output, completed);
                var result = await RunStagedOutputAsync(output, staged => new[]
                {
                    "-hide_banner", "-nostdin", "-y", "-ss", FormatSeconds(chapter.StartSeconds),
                    "-to", FormatSeconds(chapter.EndSeconds), "-i", input,
                    "-map", "0", "-c", "copy", "-avoid_negative_ts", "make_zero", staged,
                }, $"Split chapter {chapter.Index}.", cancellationToken);
                if (!result.Success)
                    return MediaWorkflowOutcome.Fail($"Chapter {chapter.Index} failed: {result.Message}", result.Diagnostics, completed);
                completed.Add(output);
            }

            return MediaWorkflowOutcome.Ok($"Split {completed.Count} chapter(s) without re-encoding.", outputs: completed);
        }
        catch (OperationCanceledException)
        {
            return MediaWorkflowOutcome.Fail("Cancelled.", outputs: completed);
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message, outputs: completed);
        }
    }

    public async Task<MediaWorkflowOutcome> ConvertPhotoBatchAsync(
        string inputFolder,
        string outputFolder,
        MediaPhotoFormat format,
        CancellationToken cancellationToken = default)
    {
        var completed = new List<string>();
        try
        {
            inputFolder = RequireExistingDirectory(inputFolder);
            outputFolder = RequireExistingDirectory(outputFolder);
            if (PathEquals(inputFolder, outputFolder))
                return MediaWorkflowOutcome.Fail("Choose a separate output folder so source photos cannot be overwritten.");

            var files = Directory.EnumerateFiles(inputFolder, "*", SearchOption.TopDirectoryOnly)
                .Where(p => PhotoInputs.Contains(Path.GetExtension(p)))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Take(MaxBatchFiles + 1)
                .ToList();
            if (files.Count == 0)
                return MediaWorkflowOutcome.Fail("No HEIC, HEIF, or JPEG-XL files were found in the source folder.");
            if (files.Count > MaxBatchFiles)
                return MediaWorkflowOutcome.Fail($"The batch is limited to {MaxBatchFiles} photos.");

            var extension = format == MediaPhotoFormat.Jpeg ? ".jpg" : ".png";
            foreach (var input in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var desired = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(input) + extension);
                var output = GetUniqueDestination(desired, completed);
                var result = await RunStagedOutputAsync(output, staged =>
                {
                    var args = new List<string> { "-hide_banner", "-nostdin", "-y", "-i", input, "-frames:v", "1" };
                    if (format == MediaPhotoFormat.Jpeg) args.AddRange(new[] { "-q:v", "2" });
                    args.Add(staged);
                    return args;
                }, $"Converted {Path.GetFileName(input)}.", cancellationToken);
                if (!result.Success)
                    return MediaWorkflowOutcome.Fail($"{Path.GetFileName(input)} failed: {result.Message}", result.Diagnostics, completed);
                completed.Add(output);
            }

            return MediaWorkflowOutcome.Ok($"Converted {completed.Count} photo(s).", outputs: completed);
        }
        catch (OperationCanceledException)
        {
            return MediaWorkflowOutcome.Fail("Cancelled.", outputs: completed);
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message, outputs: completed);
        }
    }

    public async Task<MediaWorkflowOutcome> StripImageMetadataAsync(
        string input,
        string output,
        CancellationToken cancellationToken = default)
    {
        try
        {
            input = RequireInputFile(input);
            output = RequireOutputFile(output, input);
            if (!MetadataInputs.Contains(Path.GetExtension(input)))
                return MediaWorkflowOutcome.Fail("Choose a supported image file before stripping metadata.");

            return await RunStagedOutputAsync(output, staged => new[]
            {
                "-hide_banner", "-nostdin", "-y", "-i", input,
                "-map", "0:v:0", "-map_metadata", "-1", "-map_metadata:s", "-1", "-c:v", "copy", staged,
            }, "EXIF, GPS, XMP, and other container metadata were removed without re-encoding the image.", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Cancelled();
        }
        catch (Exception ex)
        {
            return MediaWorkflowOutcome.Fail(ex.Message);
        }
    }

    public static bool TryParseLoudnessMeasurement(string text, out MediaLoudnessMeasurement measurement)
    {
        measurement = new(0, 0, 0, 0);
        if (string.IsNullOrWhiteSpace(text)) return false;
        bool I(string first, string second, out double value)
            => TryExtractJsonNumber(text, first, out value) || TryExtractJsonNumber(text, second, out value);
        if (!I("input_i", "measured_I", out var integrated)
            || !I("input_tp", "measured_TP", out var peak)
            || !I("input_lra", "measured_LRA", out var range)
            || !I("input_thresh", "measured_thresh", out var threshold))
            return false;
        measurement = new(integrated, peak, range, threshold);
        return true;
    }

    public static string BuildSecondPassLoudnessFilter(MediaLoudnessMeasurement measured)
        => "loudnorm=I=-16:TP=-1.5:LRA=11"
           + $":measured_I={FormatNumber(measured.Integrated)}"
           + $":measured_TP={FormatNumber(measured.TruePeak)}"
           + $":measured_LRA={FormatNumber(measured.Range)}"
           + $":measured_thresh={FormatNumber(measured.Threshold)}"
           + ":linear=true:print_format=summary";

    public static bool TryParseCropRectangle(string text, out string crop)
    {
        crop = string.Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var matches = Regex.Matches(text, @"(?<![A-Za-z0-9_])crop=(?<w>\d+):(?<h>\d+):(?<x>\d+):(?<y>\d+)", RegexOptions.CultureInvariant);
        if (matches.Count == 0) return false;
        var last = matches[^1];
        if (!int.TryParse(last.Groups["w"].Value, out var width) || width < 2
            || !int.TryParse(last.Groups["h"].Value, out var height) || height < 2)
            return false;
        crop = last.Value;
        return true;
    }

    public static HashSet<string> ParseNvencEncoders(string text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var codec in NvencCodecs)
            if (Regex.IsMatch(text ?? string.Empty, $@"(?m)^\s*[A-Z\.]+\s+{Regex.Escape(codec)}\s", RegexOptions.CultureInvariant))
                result.Add(codec);
        return result;
    }

    public static bool TryParseDuration(string text, out double seconds)
    {
        seconds = 0;
        var first = (text ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .FirstOrDefault(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
        return first is not null
               && double.TryParse(first, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)
               && seconds > 0
               && double.IsFinite(seconds);
    }

    public static int ComputeTargetVideoBitrateKbps(double targetMegabytes, double durationSeconds, int audioKbps)
    {
        if (!double.IsFinite(targetMegabytes) || targetMegabytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(targetMegabytes));
        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        if (audioKbps < 0) throw new ArgumentOutOfRangeException(nameof(audioKbps));
        var value = (targetMegabytes * 8_388.608 / durationSeconds) - audioKbps;
        if (value < 100) throw new InvalidOperationException("The target size is too small for this duration and audio bitrate.");
        if (value > 200_000) throw new InvalidOperationException("The calculated video bitrate exceeds the supported 200,000 kbps limit.");
        return (int)Math.Floor(value);
    }

    public static IReadOnlyList<MediaChapter> ParseChapters(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<MediaChapter>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("chapters", out var chapters) || chapters.ValueKind != JsonValueKind.Array)
                return Array.Empty<MediaChapter>();

            var result = new List<MediaChapter>();
            int index = 1;
            foreach (var item in chapters.EnumerateArray())
            {
                if (!TryReadNumber(item, "start_time", out var start)
                    || !TryReadNumber(item, "end_time", out var end)
                    || start < 0 || end <= start || !double.IsFinite(start) || !double.IsFinite(end))
                    continue;

                string title = $"Chapter {index:00}";
                if (item.TryGetProperty("tags", out var tags)
                    && tags.ValueKind == JsonValueKind.Object
                    && tags.TryGetProperty("title", out var titleProperty)
                    && titleProperty.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(titleProperty.GetString()))
                    title = titleProperty.GetString()!.Trim();
                result.Add(new(index++, title, start, end));
            }
            return result;
        }
        catch (JsonException)
        {
            return Array.Empty<MediaChapter>();
        }
    }

    public static string EscapeFilterValue(string path)
    {
        path = Path.GetFullPath(path).Replace('\\', '/');
        var builder = new StringBuilder(path.Length + 16);
        foreach (char ch in path)
        {
            if (ch is ':' or '\'' or ',' or ';' or '[' or ']' or '=') builder.Append('\\');
            builder.Append(ch);
        }
        return builder.ToString();
    }

    public static string BuildConcatListContent(IEnumerable<string> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files)
        {
            var full = Path.GetFullPath(file).Replace('\\', '/').Replace("'", "'\\''", StringComparison.Ordinal);
            builder.Append("file '").Append(full).AppendLine("'");
        }
        return builder.ToString();
    }

    private async Task<MediaCommandResult> ProbeNvencAsync(string codec, CancellationToken cancellationToken)
        => await RunAsync(_ffmpeg, new[]
        {
            "-hide_banner", "-nostdin", "-f", "lavfi", "-i", "color=size=64x64:rate=1:duration=0.1",
            "-frames:v", "1", "-an", "-c:v", codec, "-f", "null", NullDevice,
        }, null, cancellationToken);

    private async Task<MediaWorkflowOutcome> RunStagedOutputAsync(
        string output,
        Func<string, IReadOnlyList<string>> buildArguments,
        string successMessage,
        CancellationToken cancellationToken,
        string? workingDirectory = null)
    {
        string? staged = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            staged = CreateStagedOutput(output);
            var result = await RunAsync(_ffmpeg, buildArguments(staged), workingDirectory ?? Path.GetDirectoryName(output), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return PromoteOrFail(result, staged, output, successMessage);
        }
        finally
        {
            TryDeleteFile(staged);
        }
    }

    private async Task<MediaCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _runner.RunAsync(executable, arguments, workingDirectory, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static MediaWorkflowOutcome PromoteOrFail(MediaCommandResult result, string staged, string output, string successMessage)
    {
        if (!result.Success) return MediaWorkflowOutcome.Fail("ffmpeg exited without completing the workflow.", result.Output);
        if (!File.Exists(staged)) return MediaWorkflowOutcome.Fail("ffmpeg reported success but produced no output file.", result.Output);
        File.Move(staged, output, overwrite: true);
        return MediaWorkflowOutcome.Ok(successMessage, result.Output, new[] { output });
    }

    private string CreateWorkspace()
    {
        Directory.CreateDirectory(_tempRoot);
        var path = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateStagedOutput(string output)
    {
        var directory = Path.GetDirectoryName(output)!;
        var extension = Path.GetExtension(output);
        var stem = Path.GetFileNameWithoutExtension(output);
        return Path.Combine(directory, $".{stem}.winforge-{Guid.NewGuid():N}{extension}");
    }

    private static string RequireInputFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Choose an input file first.");
        var full = Path.GetFullPath(path);
        if (full.Length > MaxPathLength) throw new InvalidOperationException("The selected path is too long.");
        if (!File.Exists(full)) throw new FileNotFoundException("The selected input file no longer exists.", full);
        return full;
    }

    private static string RequireOutputFile(string output, params string[] inputs)
        => RequireOutputFile(output, (IEnumerable<string>)inputs);

    private static string RequireOutputFile(string output, IEnumerable<string> inputs)
    {
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("Choose an output file first.");
        var full = Path.GetFullPath(output);
        if (full.Length > MaxPathLength) throw new InvalidOperationException("The output path is too long.");
        var directory = Path.GetDirectoryName(full);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("The output folder does not exist.");
        if (inputs.Any(input => PathEquals(full, input)))
            throw new InvalidOperationException("Input and output must be different files.");
        return full;
    }

    private static string RequireExistingDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("Choose a folder first.");
        var full = Path.GetFullPath(path);
        if (full.Length > MaxPathLength) throw new InvalidOperationException("The selected folder path is too long.");
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException("The selected folder no longer exists.");
        return full;
    }

    private static string GetUniqueDestination(string desired, IEnumerable<string> reserved)
    {
        var reservedSet = new HashSet<string>(reserved.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(desired) && !reservedSet.Contains(Path.GetFullPath(desired))) return desired;
        var directory = Path.GetDirectoryName(desired)!;
        var stem = Path.GetFileNameWithoutExtension(desired);
        var extension = Path.GetExtension(desired);
        for (int i = 2; i <= 10_000; i++)
        {
            var candidate = Path.Combine(directory, $"{stem}-{i}{extension}");
            if (!File.Exists(candidate) && !reservedSet.Contains(Path.GetFullPath(candidate))) return candidate;
        }
        throw new IOException("Could not allocate a unique output filename.");
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string((value ?? string.Empty).Select(ch => invalid.Contains(ch) || char.IsControl(ch) ? '_' : ch).ToArray()).Trim().Trim('.');
        if (clean.Length == 0) clean = "Untitled";
        if (clean.Length > 60) clean = clean[..60].Trim();
        return clean;
    }

    private static bool PathEquals(string a, string b)
        => string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static bool TryExtractJsonNumber(string text, string property, out double value)
    {
        value = 0;
        var pattern = "[\\\"']" + Regex.Escape(property)
            + "[\\\"']\\s*:\\s*[\\\"']?(?<v>[+-]?(?:\\d+(?:\\.\\d+)?|\\.\\d+))[\\\"']?";
        var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    private static bool TryReadNumber(JsonElement element, string name, out double value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out var property)) return false;
        if (property.ValueKind == JsonValueKind.Number) return property.TryGetDouble(out value);
        return property.ValueKind == JsonValueKind.String
               && double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsMp4Family(string path)
        => Path.GetExtension(path).ToLowerInvariant() is ".mp4" or ".mov" or ".m4v";

    private static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatSeconds(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static MediaWorkflowOutcome Cancelled() => MediaWorkflowOutcome.Fail("Cancelled.");

    private static string NullDevice => OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort owned scratch cleanup */ }
    }

    private static void TryDeleteDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { /* best-effort owned scratch cleanup */ }
    }
}
