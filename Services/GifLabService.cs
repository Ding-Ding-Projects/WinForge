using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// GIF 工作室引擎 · GIF Studio engine. Captures a screen region (or window / fullscreen) to a temp
/// folder of PNG frames via ffmpeg gdigrab, then lets the page delete / reorder / crop those frames
/// and re-encode the survivors to GIF, MP4 or APNG (palettegen/paletteuse for GIF). Everything runs
/// in-app — no redirect. A single working directory holds the frames and is cleaned up on Reset/close.
/// 全部喺 app 內做，唔會跳走。
/// </summary>
public static class GifLabService
{
    private static Process? _capProc;
    private static string _workDir = "";
    private static int _capturedFps = 15;

    /// <summary>而家錄緊嗎 · Whether a capture is currently running.</summary>
    public static bool IsCapturing => _capProc is { HasExited: false };

    /// <summary>影低嗰陣用嘅幀率 · The frame rate the current frames were captured at.</summary>
    public static int CapturedFps => _capturedFps;

    /// <summary>暫存資料夾（PNG 幀放呢度）· The temp working directory holding the PNG frames.</summary>
    public static string WorkDir => _workDir;

    // =========================================================================
    //  CAPTURE — ffmpeg gdigrab → temp dir of frame%05d.png
    // =========================================================================

    /// <summary>
    /// 開始影低一忽螢幕做 PNG 幀 · Start capturing a screen region into PNG frames at the given fps.
    /// Region is in physical pixels. When durationSeconds &gt; 0 ffmpeg stops itself; otherwise call
    /// StopCapture(). Any previous frames are cleared first.
    /// </summary>
    public static TweakResult StartCapture(int x, int y, int w, int h, int fps, int durationSeconds)
    {
        if (IsCapturing) return TweakResult.Fail("Already capturing.", "已經喺度影緊。");
        if (!MediaService.IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");
        if (w <= 0 || h <= 0) return TweakResult.Fail("No region selected.", "未揀區域。");

        Reset(); // fresh working dir + clear old frames
        EnsureWorkDir();
        _capturedFps = Math.Clamp(fps, 1, 60);

        // even dimensions keep yuv420p / scaling happy later
        w -= w % 2; h -= h % 2;

        var pattern = Path.Combine(_workDir, "frame%05d.png");
        var dur = durationSeconds > 0 ? $"-t {durationSeconds} " : "";
        var args = $"-y -f gdigrab -framerate {_capturedFps} " +
                   $"-offset_x {x} -offset_y {y} -video_size {w}x{h} -i desktop " +
                   dur + $"\"{pattern}\"";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = MediaService.FFmpeg,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            _capProc = Process.Start(psi);
            if (_capProc is null) return TweakResult.Fail("Failed to start ffmpeg.", "無法啟動 ffmpeg。");
            return TweakResult.Ok("Capturing…", "影緊…");
        }
        catch (Exception ex)
        {
            _capProc = null;
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>停止影低（自動或手動）· Stop the capture cleanly (graceful 'q' on stdin).</summary>
    public static async Task<TweakResult> StopCapture()
    {
        var p = _capProc;
        _capProc = null;
        if (p is null) return TweakResult.Ok("Stopped.", "已停止。");
        try
        {
            if (!p.HasExited)
            {
                await p.StandardInput.WriteLineAsync("q");
                await p.StandardInput.FlushAsync();
                p.StandardInput.Close();
                await p.WaitForExitAsync();
            }
            return TweakResult.Ok("Captured the frames.", "已影低啲幀。");
        }
        catch (Exception ex)
        {
            try { if (!p.HasExited) p.Kill(); } catch { }
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>等一個自動停止嘅影低跑完 · Await a self-terminating (duration-bound) capture.</summary>
    public static async Task<TweakResult> WaitForCaptureExit()
    {
        var p = _capProc;
        if (p is null) return TweakResult.Ok("Done.", "完成。");
        try { await p.WaitForExitAsync(); }
        catch { }
        _capProc = null;
        return TweakResult.Ok("Captured the frames.", "已影低啲幀。");
    }

    /// <summary>列出所有已影嘅幀檔（依檔名排序）· List captured frame files, sorted by name.</summary>
    public static IReadOnlyList<string> ListFrames()
    {
        if (string.IsNullOrEmpty(_workDir) || !Directory.Exists(_workDir)) return Array.Empty<string>();
        try
        {
            return Directory.GetFiles(_workDir, "frame*.png")
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch { return Array.Empty<string>(); }
    }

    // =========================================================================
    //  CROP — re-encode every frame to a sub-rectangle (uniform across all frames)
    // =========================================================================

    /// <summary>
    /// 統一裁切所有幀 · Apply a uniform crop (left/top/width/height, in source-frame pixels) to every
    /// frame in <paramref name="frames"/>, writing the cropped frames back over the originals. ffmpeg
    /// crop=W:H:X:Y. Returns the (unchanged) file list on success.
    /// </summary>
    public static async Task<TweakResult> CropFrames(IReadOnlyList<string> frames, int cropX, int cropY, int cropW, int cropH, CancellationToken ct = default)
    {
        if (!MediaService.IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");
        if (frames.Count == 0) return TweakResult.Fail("No frames to crop.", "冇幀可以裁切。");
        if (cropW < 2 || cropH < 2) return TweakResult.Fail("Crop area is too small.", "裁切範圍太細。");
        cropW -= cropW % 2; cropH -= cropH % 2;

        foreach (var f in frames)
        {
            if (ct.IsCancellationRequested) return TweakResult.Fail("Cancelled.", "已取消。");
            var tmp = f + ".crop.png";
            var args = $"-y -i \"{f}\" -vf \"crop={cropW}:{cropH}:{cropX}:{cropY}\" \"{tmp}\"";
            var r = await ShellRunner.Run(MediaService.FFmpeg, args, elevated: false, ct);
            if (!r.Success || !File.Exists(tmp))
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                return TweakResult.Fail("Cropping a frame failed.", "裁切某幀失敗。", r.Output);
            }
            try { File.Delete(f); File.Move(tmp, f); }
            catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
        }
        return TweakResult.Ok("Cropped all frames.", "已裁切所有幀。");
    }

    // =========================================================================
    //  EXPORT — build an ffmpeg concat list from the (possibly reordered/trimmed)
    //  frame order, then encode to GIF / MP4 / APNG.
    // =========================================================================

    public enum ExportFormat { Gif, Mp4, Apng }

    /// <summary>
    /// 匯出 · Encode the given ordered frame list to the chosen format. <paramref name="orderedFrames"/>
    /// is the surviving frames in display order (delete = omit, reorder = different order). fps is the
    /// playback rate; scaleWidth &lt;= 0 keeps source width; loop 0 = infinite (GIF/APNG).
    /// </summary>
    public static async Task<TweakResult> Export(
        IReadOnlyList<string> orderedFrames, string outputPath, ExportFormat format,
        int fps, int scaleWidth, int loop, CancellationToken ct = default)
    {
        if (!MediaService.IsInstalled) return TweakResult.Fail("ffmpeg not found.", "搵唔到 ffmpeg。");
        if (orderedFrames.Count == 0) return TweakResult.Fail("No frames to export.", "冇幀可以匯出。");

        fps = Math.Clamp(fps, 1, 60);
        var scaleFilter = scaleWidth > 0 ? $",scale={scaleWidth}:-1:flags=lanczos" : "";

        // Build a concat demuxer list so reorder / delete is honoured without renaming originals.
        var listFile = Path.Combine(WorkDirSafe(), $"concat-{Guid.NewGuid():N}.txt");
        var sb = new StringBuilder();
        foreach (var f in orderedFrames)
            sb.Append("file '").Append(f.Replace("\\", "/").Replace("'", "'\\''")).Append("'\n");
        try { await File.WriteAllTextAsync(listFile, sb.ToString(), new UTF8Encoding(false), ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }

        try
        {
            switch (format)
            {
                case ExportFormat.Mp4:
                {
                    var args = $"-y -r {fps} -f concat -safe 0 -i \"{listFile}\" " +
                               $"-vf \"format=yuv420p{scaleFilter}\" -c:v libx264 -preset medium -movflags +faststart \"{outputPath}\"";
                    var r = await ShellRunner.Run(MediaService.FFmpeg, args, elevated: false, ct);
                    return r.Success
                        ? TweakResult.Ok("Exported MP4.", "已匯出 MP4。", outputPath)
                        : TweakResult.Fail("MP4 export failed.", "MP4 匯出失敗。", r.Output);
                }

                case ExportFormat.Apng:
                {
                    var args = $"-y -r {fps} -f concat -safe 0 -i \"{listFile}\" " +
                               (string.IsNullOrEmpty(scaleFilter) ? "" : $"-vf \"{scaleFilter.TrimStart(',')}\" ") +
                               $"-f apng -plays {Math.Max(0, loop)} \"{outputPath}\"";
                    var r = await ShellRunner.Run(MediaService.FFmpeg, args, elevated: false, ct);
                    return r.Success
                        ? TweakResult.Ok("Exported APNG.", "已匯出 APNG。", outputPath)
                        : TweakResult.Fail("APNG export failed.", "APNG 匯出失敗。", r.Output);
                }

                default: // Gif — two-pass palette
                {
                    var pal = Path.Combine(WorkDirSafe(), $"pal-{Guid.NewGuid():N}.png");
                    try
                    {
                        var p1 = $"-y -r {fps} -f concat -safe 0 -i \"{listFile}\" " +
                                 $"-vf \"fps={fps}{scaleFilter},palettegen=stats_mode=diff\" \"{pal}\"";
                        var r1 = await ShellRunner.Run(MediaService.FFmpeg, p1, elevated: false, ct);
                        if (!r1.Success || !File.Exists(pal))
                            return TweakResult.Fail("Palette generation failed.", "調色板生成失敗。", r1.Output);

                        var p2 = $"-y -r {fps} -f concat -safe 0 -i \"{listFile}\" -i \"{pal}\" " +
                                 $"-lavfi \"fps={fps}{scaleFilter}[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=3\" " +
                                 $"-loop {Math.Max(0, loop)} \"{outputPath}\"";
                        var r2 = await ShellRunner.Run(MediaService.FFmpeg, p2, elevated: false, ct);
                        return r2.Success
                            ? TweakResult.Ok("Exported GIF.", "已匯出 GIF。", outputPath)
                            : TweakResult.Fail("GIF export failed.", "GIF 匯出失敗。", r2.Output);
                    }
                    finally { try { if (File.Exists(pal)) File.Delete(pal); } catch { } }
                }
            }
        }
        finally { try { if (File.Exists(listFile)) File.Delete(listFile); } catch { } }
    }

    // =========================================================================
    //  Working directory lifecycle
    // =========================================================================

    private static void EnsureWorkDir()
    {
        if (string.IsNullOrEmpty(_workDir))
            _workDir = Path.Combine(Path.GetTempPath(), "WinForge-GifLab", Guid.NewGuid().ToString("N"));
        try { Directory.CreateDirectory(_workDir); } catch { }
    }

    private static string WorkDirSafe()
    {
        EnsureWorkDir();
        return _workDir;
    }

    /// <summary>清走暫存幀同資料夾 · Delete the temp frames + working directory and forget it.</summary>
    public static void Reset()
    {
        try { if (!string.IsNullOrEmpty(_workDir) && Directory.Exists(_workDir)) Directory.Delete(_workDir, true); }
        catch { }
        _workDir = "";
    }
}
