using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Models
{
    public sealed record TweakResult(bool Success, string? Output = null)
    {
        public static TweakResult Ok(string en, string zh, string? output = null) => new(true, output);
        public static TweakResult Fail(string en, string zh, string? output = null) => new(false, output);
    }
}

namespace WinForge.Services
{
    public sealed record Invocation(string FileName, IReadOnlyList<string> Arguments, bool IsArgumentVector);

    public static class ShellRunner
    {
        public static List<Invocation> Invocations { get; } = new();

        public static void Reset() => Invocations.Clear();

        public static Task<TweakResult> Run(string fileName, string arguments, bool elevated = false,
            CancellationToken ct = default)
        {
            Invocations.Add(new Invocation(fileName, new[] { arguments }, false));
            return Task.FromResult(TweakResult.Ok("Done.", "完成。"));
        }

        public static Task<TweakResult> RunArguments(string fileName, IReadOnlyList<string> arguments,
            bool elevated = false, CancellationToken ct = default)
        {
            Invocations.Add(new Invocation(fileName, arguments.ToArray(), true));
            return Task.FromResult(TweakResult.Ok("Done.", "完成。", "generated"));
        }

        public static Task<TweakResult> RunCmd(string command, bool elevated = false,
            CancellationToken ct = default)
        {
            Invocations.Add(new Invocation("cmd.exe", new[] { command }, false));
            return Task.FromResult(TweakResult.Ok("Done.", "完成。"));
        }

        public static Task<TweakResult> RunCmdStreaming(string command, IProgress<string>? onLine,
            bool elevated = false, CancellationToken ct = default)
        {
            Invocations.Add(new Invocation("cmd.exe", new[] { command }, false));
            return Task.FromResult(TweakResult.Ok("Done.", "完成。"));
        }

        public static Task<string> Capture(string fileName, string arguments, CancellationToken ct = default)
        {
            Invocations.Add(new Invocation(fileName, new[] { arguments }, false));
            return Task.FromResult(string.Empty);
        }
    }

    public static class PackageService
    {
        public static void RefreshProcessPath() { }
    }
}
