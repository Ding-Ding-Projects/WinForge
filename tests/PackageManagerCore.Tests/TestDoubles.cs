using System.Collections.Concurrent;

namespace WinForge.Models
{
    public sealed class LocalizedText
    {
        public string En { get; }
        public string Zh { get; }
        public string Display => En;
        public string Primary => En;
        public LocalizedText(string en, string zh) { En = en; Zh = zh; }
    }

    public sealed record TweakResult(bool Success, LocalizedText? Message = null, string? Output = null)
    {
        public string? Code { get; init; }
        public static TweakResult Ok(string en, string zh, string? output = null)
            => new(true, new LocalizedText(en, zh), output);
        public static TweakResult Fail(string en, string zh, string? output = null)
            => new(false, new LocalizedText(en, zh), output);
    }
}

namespace WinForge.Services
{
    public sealed class PackageItem
    {
        public string Name { get; set; } = "";
        public string Id { get; set; } = "";
        public string Version { get; set; } = "";
        public string AvailableVersion { get; set; } = "";
        public string Source { get; set; } = "";
        public string ManagerKey { get; set; } = "";
    }
    public static class SettingsStore
    {
        private static readonly ConcurrentDictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);
        public static string Get(string key, string fallback = "") => Values.TryGetValue(key, out var value) ? value : fallback;
        public static void Set(string key, string value) => Values[key] = value ?? "";
    }
    public static class PackageOperations
    {
        public enum Op { Install, Update, Uninstall }
        public static int BuildCalls { get; private set; }
        private static int _runCalls;
        public static int RunCalls => _runCalls;
        public static string LastPreviewSource { get; private set; } = "";
        public static string LastRunSource { get; private set; } = "";
        public static ConcurrentQueue<string> RunSources { get; } = new();
        public static Func<string, string, Op, InstallOptions, IProgress<string>?, CancellationToken, Task<WinForge.Models.TweakResult>>? Runner { get; set; }
        public static void Reset()
        {
            BuildCalls = 0;
            _runCalls = 0;
            LastPreviewSource = "";
            LastRunSource = "";
            while (RunSources.TryDequeue(out _)) { }
            Runner = null;
        }
        public static string BuildCommandPreview(string managerKey, string id, Op op, InstallOptions options)
            => BuildCommandPreview(managerKey, id, "", op, options);

        public static string BuildCommandPreview(string managerKey, string id, string? source, Op op, InstallOptions options)
        {
            BuildCalls++;
            LastPreviewSource = source ?? "";
            var extra = string.IsNullOrWhiteSpace(options.CustomArgsInstall) ? "" : " " + options.CustomArgsInstall.Trim();
            var sourcePart = string.IsNullOrWhiteSpace(source) ? "" : " --source " + source.Trim();
            return managerKey is "psgallery" or "pwsh7"
                ? $"Install-Package -Name \"{id}\"{sourcePart}{extra}"
                : $"{managerKey} {op.ToString().ToLowerInvariant()} {id}{sourcePart}{extra}";
        }

        public static Task<WinForge.Models.TweakResult> RunAsync(string managerKey, string id, Op op,
            InstallOptions options, IProgress<string>? progress, CancellationToken ct)
            => RunAsync(managerKey, id, "", op, options, progress, ct);

        public static Task<WinForge.Models.TweakResult> RunAsync(string managerKey, string id, string? source, Op op,
            InstallOptions options, IProgress<string>? progress, CancellationToken ct)
        {
            Interlocked.Increment(ref _runCalls);
            LastRunSource = source ?? "";
            RunSources.Enqueue(LastRunSource);
            return Runner?.Invoke(managerKey, id, op, options, progress, ct)
                ?? Task.FromResult(WinForge.Models.TweakResult.Ok("ok", "成功"));
        }
    }

    public static class PackageManagerRegistry
    {
        public static object? ByKey(string key) => string.IsNullOrWhiteSpace(key) || key == "unknown" ? null : new object();
    }
    public static class PackageManagerSettings
    {
        public static int ParallelOperationCount { get; set; } = 2;
        public static string ProxyPassword { get; set; } = "";
    }
    public static class IgnoredUpdates
    {
        public static bool IsIgnored(PackageItem item) => false;
    }
    public static class PackageNotifier
    {
        public static void ShowUpgrading(string name, string manager) { }
        public static void ShowSuccess(string name, string manager) { }
        public static void ShowError(string name, string? message, string manager) { }
    }
    public static class ShellRunner
    {
        public const string ProcessCleanupTimeoutCode = "process-cleanup-timeout";
    }
}
