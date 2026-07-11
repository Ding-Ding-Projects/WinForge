using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Models
{
    public sealed record LocalizedText(string En, string Zh);

    public sealed record TweakResult(bool Success, LocalizedText? Message = null, string? Output = null)
    {
        public static TweakResult Ok(string en, string zh, string? output = null)
            => new(true, new LocalizedText(en, zh), output);

        public static TweakResult Fail(string en, string zh, string? output = null)
            => new(false, new LocalizedText(en, zh), output);
    }
}

namespace WinForge.Services
{
    using WinForge.Models;

    public sealed record AdbTestInvocation(string FileName, IReadOnlyList<string> Arguments, bool Capture);

    public static class ShellRunner
    {
        public static List<AdbTestInvocation> Invocations { get; } = new();

        public static void Reset() => Invocations.Clear();

        public static Task<TweakResult> RunArguments(string fileName, IReadOnlyList<string> arguments,
            bool elevated = false, CancellationToken ct = default)
        {
            Invocations.Add(new AdbTestInvocation(fileName, arguments.ToArray(), false));
            return Task.FromResult(TweakResult.Ok("Done.", "完成。"));
        }

        public static Task<string> CaptureArguments(string fileName, IReadOnlyList<string> arguments,
            CancellationToken ct = default)
        {
            Invocations.Add(new AdbTestInvocation(fileName, arguments.ToArray(), true));
            return Task.FromResult(string.Empty);
        }
    }
}
