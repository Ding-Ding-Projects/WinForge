using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 包住 ffmpeg / ffprobe · A thin wrapper over ffmpeg/ffprobe for the in-app Media module.
/// </summary>
public static class MediaService
{
    private static string? _ffmpeg;
    private static string? _ffprobe;

    private static string Which(string exe)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(';'))
        {
            try
            {
                var full = Path.Combine(dir.Trim(), exe);
                if (File.Exists(full)) return full;
            }
            catch { /* bad PATH entry */ }
        }
        return exe; // let the OS resolve at run time
    }

    public static string FFmpeg => _ffmpeg ??= Which("ffmpeg.exe");

    public static string FFprobe
    {
        get
        {
            if (_ffprobe is not null) return _ffprobe;
            var sibling = Path.Combine(Path.GetDirectoryName(FFmpeg) ?? "", "ffprobe.exe");
            _ffprobe = File.Exists(sibling) ? sibling : Which("ffprobe.exe");
            return _ffprobe;
        }
    }

    public static bool IsInstalled => File.Exists(FFmpeg) || !FFmpeg.Contains('\\');

    /// <summary>清快取，等啱啱裝完嘅 ffmpeg 即刻搵到 · Clear the cached paths so a just-installed ffmpeg is re-resolved.</summary>
    public static void Rescan() { _ffmpeg = null; _ffprobe = null; }

    public static string Input => AppState.CurrentMediaInput;
    public static string Output => AppState.CurrentMediaOutput;
    public static bool HasInput => !string.IsNullOrWhiteSpace(Input) && File.Exists(Input);
    public static bool HasOutput => !string.IsNullOrWhiteSpace(Output);

    private static string Q(string p) => $"\"{p}\"";

    /// <summary>用明確輸入／輸出執行 · Run with explicit input/output paths.</summary>
    public static Task<TweakResult> RunWith(string input, string? output, string args, bool useProbe, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(TweakResult.Fail("No input file selected.", "未揀輸入檔。"));

        var resolved = args.Replace("{in}", Q(input));
        if (!string.IsNullOrEmpty(output)) resolved = resolved.Replace("{out}", Q(output));

        string exe = useProbe ? FFprobe : FFmpeg;
        if (!useProbe && !resolved.Contains("-y ")) resolved = "-y " + resolved; // never prompt to overwrite

        var workDir = Path.GetDirectoryName(input);
        return ShellRunner.RunIn(workDir, exe, resolved, elevated: false, ct);
    }

    /// <summary>用 AppState 嘅輸入／輸出執行（畀目錄操作用）· Run using AppState input/output (catalog ops).</summary>
    public static Task<TweakResult> RunArgs(string args, bool needsOutput, bool useProbe, CancellationToken ct = default)
    {
        if (!HasInput) return Task.FromResult(TweakResult.Fail("No input file selected.", "未揀輸入檔。"));
        if (needsOutput && !HasOutput) return Task.FromResult(TweakResult.Fail("No output file selected.", "未揀輸出檔。"));
        return RunWith(Input, needsOutput ? Output : null, args, useProbe, ct);
    }

    // ---- quick actions: auto-derive output beside the input ----
    private static string Derive(string suffixExt)
    {
        var dir = Path.GetDirectoryName(Input) ?? "";
        var name = Path.GetFileNameWithoutExtension(Input);
        return Path.Combine(dir, name + suffixExt);
    }

    public static Task<TweakResult> Quick(string suffixExt, string args, CancellationToken ct = default)
        => HasInput ? RunWith(Input, Derive(suffixExt), args, useProbe: false, ct)
                    : Task.FromResult(TweakResult.Fail("No input file selected.", "未揀輸入檔。"));

    public static Task<TweakResult> Info(CancellationToken ct = default)
        => HasInput ? RunWith(Input, null, "-hide_banner -i {in}", useProbe: true, ct)
                    : Task.FromResult(TweakResult.Fail("No input file selected.", "未揀輸入檔。"));
}
