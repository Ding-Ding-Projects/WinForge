using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>App adapter that keeps media workflow process arguments out of shell parsing.</summary>
internal sealed class ShellMediaCommandRunner : IMediaCommandRunner
{
    public async Task<MediaCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var result = await ShellRunner.RunArgumentsStreaming(
            executable,
            arguments,
            onLine: null,
            elevated: false,
            workingDirectory: workingDirectory,
            ct: cancellationToken);
        return new MediaCommandResult(result.Success, result.Output ?? result.Message?.En ?? string.Empty, result.Success ? 0 : 1);
    }
}

public sealed record MediaChapterServiceResult(TweakResult Result, IReadOnlyList<MediaChapter> Chapters);

public sealed record MediaNvencServiceResult(TweakResult Result, IReadOnlyList<string> Codecs);

/// <summary>
/// Bilingual facade for the bounded ffmpeg/ffprobe media workflows. The pure executor remains independently
/// testable while this class supplies the installed engine paths and WinForge result model.
/// </summary>
public static class MediaWorkflowService
{
    private static MediaWorkflowExecutor Executor()
        => new(new ShellMediaCommandRunner(), MediaService.FFmpeg, MediaService.FFprobe);

    public static async Task<TweakResult> NormalizeLoudnessAsync(string input, string output, CancellationToken ct = default)
        => ToResult(await Executor().NormalizeLoudnessAsync(input, output, ct),
            "EBU R128 normalization completed in two measured passes.",
            "EBU R128 響度正規化已完成，量度同套用兩步都做好。", "Loudness normalization");

    public static async Task<TweakResult> TrimSilenceAsync(string input, string output, CancellationToken ct = default)
        => ToResult(await Executor().TrimSilenceAsync(input, output, ct),
            "Leading, trailing, and internal silence was trimmed.",
            "頭尾同中間靜音已經剪走。", "Silence trimming");

    public static async Task<TweakResult> StabilizeVideoAsync(string input, string output, CancellationToken ct = default)
        => ToResult(await Executor().StabilizeVideoAsync(input, output, ct),
            "Two-pass video stabilization completed.",
            "兩步影片防震已完成。", "Video stabilization");

    public static async Task<TweakResult> AutoCropAsync(string input, string output, CancellationToken ct = default)
        => ToResult(await Executor().AutoCropAsync(input, output, ct),
            "Black bars were detected and cropped.",
            "黑邊已自動偵測同裁走。", "Black-bar crop");

    public static async Task<TweakResult> ConcatCopyAsync(IReadOnlyList<string> clips, string output, CancellationToken ct = default)
        => ToResult(await Executor().ConcatCopyAsync(clips, output, ct),
            "Clips were joined without re-encoding.",
            "片段已經無重編碼咁合併。", "Clip join");

    public static async Task<MediaNvencServiceResult> DetectNvencAsync(CancellationToken ct = default)
    {
        var result = await Executor().DetectNvencAsync(ct);
        return new MediaNvencServiceResult(ToResult(result.Outcome,
            $"Detected {result.Codecs.Count} working NVENC encoder(s).",
            $"偵測到 {result.Codecs.Count} 個可用 NVENC 編碼器。", "NVENC detection"), result.Codecs);
    }

    public static async Task<TweakResult> EncodeNvencAsync(
        string input,
        string output,
        string codec,
        int quality,
        CancellationToken ct = default)
        => ToResult(await Executor().EncodeNvencAsync(input, output, codec, quality, ct),
            $"Video encoded with {codec} after a hardware capability probe.",
            $"硬件能力檢查通過，影片已用 {codec} 編碼。", "NVENC encoding");

    public static async Task<TweakResult> EncodeTargetSizeAsync(
        string input,
        string output,
        double targetMegabytes,
        int audioKbps,
        CancellationToken ct = default)
        => ToResult(await Executor().EncodeTargetSizeAsync(input, output, targetMegabytes, audioKbps, ct),
            "Two-pass target-size encoding completed.",
            "兩步目標容量編碼已完成。", "Target-size encoding");

    public static async Task<TweakResult> AddSubtitlesAsync(
        string input,
        string subtitle,
        string output,
        MediaSubtitleMode mode,
        CancellationToken ct = default)
        => ToResult(await Executor().AddSubtitlesAsync(input, subtitle, output, mode, ct),
            mode == MediaSubtitleMode.BurnIn ? "Subtitles were burned into the video." : "A toggleable subtitle track was added.",
            mode == MediaSubtitleMode.BurnIn ? "字幕已燒入影片。" : "可開關字幕軌已加入影片。", "Subtitle workflow");

    public static async Task<MediaChapterServiceResult> ReadChaptersAsync(string input, CancellationToken ct = default)
    {
        var result = await Executor().ReadChaptersAsync(input, ct);
        return new MediaChapterServiceResult(ToResult(result.Outcome,
            $"Read {result.Chapters.Count} chapter(s).",
            $"讀到 {result.Chapters.Count} 個章節。", "Chapter scan"), result.Chapters);
    }

    public static async Task<TweakResult> SplitChaptersAsync(string input, string outputFolder, CancellationToken ct = default)
        => ToResult(await Executor().SplitChaptersAsync(input, outputFolder, ct),
            "Chapters were split without re-encoding.",
            "章節已無重編碼咁逐段分割。", "Chapter split");

    public static async Task<TweakResult> ConvertPhotoBatchAsync(
        string inputFolder,
        string outputFolder,
        MediaPhotoFormat format,
        CancellationToken ct = default)
        => ToResult(await Executor().ConvertPhotoBatchAsync(inputFolder, outputFolder, format, ct),
            $"HEIC/JPEG-XL photos were converted to {(format == MediaPhotoFormat.Jpeg ? "JPG" : "PNG")}.",
            $"HEIC／JPEG-XL 相片已批次轉做 {(format == MediaPhotoFormat.Jpeg ? "JPG" : "PNG")}。", "Photo conversion");

    public static async Task<TweakResult> StripImageMetadataAsync(string input, string output, CancellationToken ct = default)
        => ToResult(await Executor().StripImageMetadataAsync(input, output, ct),
            "EXIF, GPS, XMP, and other image metadata were removed without re-encoding.",
            "EXIF、GPS、XMP 同其他相片 metadata 已無重編碼咁移除。", "Metadata stripping");

    private static TweakResult ToResult(MediaWorkflowOutcome outcome, string successEn, string successZh, string action)
    {
        var detail = BuildDetail(outcome);
        if (outcome.Success) return TweakResult.Ok(successEn, successZh, detail);
        if (string.Equals(outcome.Message, "Cancelled.", StringComparison.OrdinalIgnoreCase))
            return TweakResult.Fail("Cancelled.", "已取消。", detail);
        return TweakResult.Fail(
            $"{action} did not complete: {outcome.Message}",
            "工作流程未完成；請檢查輸入同下面嘅 ffmpeg 詳情。",
            detail);
    }

    private static string? BuildDetail(MediaWorkflowOutcome outcome)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(outcome.Diagnostics)) parts.Add(outcome.Diagnostics.Trim());
        if (outcome.Outputs is { Count: > 0 })
        {
            parts.Add("Outputs:");
            parts.AddRange(outcome.Outputs.Select(path => Path.GetFullPath(path)));
        }
        return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);
    }
}
