using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinForge.Services;

/// <summary>
/// Background GitHub release checker. It validates and stages an unsigned Squirrel.Windows setup,
/// then waits for the user to choose the restart/install action; active work is never interrupted.
/// </summary>
public static class AppUpdateService
{
    private const string EnabledKey = "app.autoupdate.enabled";
    private const string LastAttemptTagKey = "app.autoupdate.lastAttemptTag";
    private const string LastAttemptUtcKey = "app.autoupdate.lastAttemptUtc";
    private const string LastInstalledNoticeTagKey = "app.autoupdate.lastInstalledNoticeTag";
    private const string PendingInstallerPathKey = "app.autoupdate.pendingInstallerPath";
    private const string PendingInstallerShaKey = "app.autoupdate.pendingInstallerSha256";
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan SameTagRetryDelay = TimeSpan.FromHours(12);
    private static readonly HttpClient Http = CreateHttp();
    private static int _started;

    public enum NoticeSeverity { Info, Success, Warning, Error }

    public sealed record AppUpdateNotice(
        NoticeSeverity Severity,
        string TitleEn,
        string TitleZh,
        string MessageEn,
        string MessageZh,
        int AutoDismissMs = 0,
        IReadOnlyList<AppNoticeAction>? Actions = null);

    public static event Action<AppUpdateNotice>? Notice;

    public static void StartAutomaticChecks(DispatcherQueue ui)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        if (!AdminHelper.IsElevated) CleanupStagedUpdateHelpers();
        NotifyInstalledIfNeeded(ui);
        _ = Task.Run(() => Loop(ui));
    }

    private static async Task Loop(DispatcherQueue ui)
    {
        await Task.Delay(FirstCheckDelay).ConfigureAwait(false);
        bool firstCheck = true;
        while (true)
        {
            await CheckAndApplyLatestAsync(ui, firstCheck).ConfigureAwait(false);
            firstCheck = false;
            await Task.Delay(CheckInterval).ConfigureAwait(false);
        }
    }

    private static async Task CheckAndApplyLatestAsync(DispatcherQueue ui, bool notifyQuietResult)
    {
        if (!Enabled())
        {
            if (notifyQuietResult)
                Notify(ui, NoticeSeverity.Warning,
                    "Auto update is off", "自動更新已關閉",
                    "WinForge will not install new releases automatically.",
                    "WinForge 不會自動安裝新版本。",
                    7000);
            return;
        }
        if (IsDevelopmentRun())
        {
            if (notifyQuietResult)
                Notify(ui, NoticeSeverity.Info,
                    "Auto update skipped", "已略過自動更新",
                    "This is a development checkout, so release installers are not applied automatically.",
                    "這是開發 checkout，所以不會自動套用 release 安裝程式。",
                    7000);
            return;
        }
        if (AdminHelper.IsElevated)
        {
            if (notifyQuietResult)
                Notify(ui, NoticeSeverity.Warning,
                    "Auto update paused", "自動更新已暫停",
                    "WinForge will not update while running as administrator. Restart it normally to update safely.",
                    "WinForge 以系統管理員身分執行時唔會更新。請以一般權限重開，先至安全更新。",
                    10000);
            return;
        }

        try
        {
            if (notifyQuietResult)
                Notify(ui, NoticeSeverity.Info,
                    "Checking for updates", "正在檢查更新",
                    "WinForge is checking GitHub releases in the background.",
                    "WinForge 正在背景檢查 GitHub release。",
                    5000);

            using var res = await Http.GetAsync(ManagedReleaseContract.LatestReleaseApi).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                if (notifyQuietResult)
                    Notify(ui, NoticeSeverity.Warning,
                        "Update check failed", "更新檢查失敗",
                        $"GitHub returned HTTP {(int)res.StatusCode}. Auto update will retry later.",
                        $"GitHub 回傳 HTTP {(int)res.StatusCode}。自動更新稍後會重試。",
                        9000);
                return;
            }

            await using var stream = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream).ConfigureAwait(false);
            if (release is null || release.Draft || release.Prerelease) return;
            var metadata = new ManagedReleaseMetadata(
                release.TagName,
                release.Draft,
                release.Prerelease,
                release.Assets.Select(asset => new ManagedReleaseAsset(
                    asset.Name, asset.BrowserDownloadUrl, asset.Digest, asset.Size)).ToArray());
            if (!ManagedReleaseContract.TryResolveRelease(metadata, out ManagedReleaseSelection? selected, out string reason) ||
                selected is null)
            {
                Notify(ui, NoticeSeverity.Warning,
                    "Update release is incompatible", "更新版本唔相容",
                    $"The latest stable release does not satisfy WinForge's delivery contract: {reason}",
                    $"最新穩定版唔符合 WinForge 發佈合約：{reason}",
                    12000);
                return;
            }

            string latestTag = selected.Version;
            string currentTag = CurrentVersionTag();
            if (!ManagedReleaseContract.IsNewerRelease(latestTag, currentTag))
            {
                if (notifyQuietResult)
                    Notify(ui, NoticeSeverity.Success,
                        "WinForge is up to date", "WinForge 已是最新",
                        $"Current version: v{currentTag}.",
                        $"目前版本：v{currentTag}。",
                        7000);
                return;
            }
            string existingStaged = SettingsStore.Get(PendingInstallerPathKey, "");
            bool hasStaged = !string.IsNullOrWhiteSpace(existingStaged) && File.Exists(existingStaged);
            if (RecentlyAttempted(latestTag) && !hasStaged) return;

            ManagedReleaseAsset setup = selected.Installer;
            string expectedSha256 = selected.InstallerSha256;

            SettingsStore.Set(LastAttemptTagKey, latestTag);
            SettingsStore.Set(LastAttemptUtcKey, DateTime.UtcNow.ToString("O"));

            string staged = string.Equals(SettingsStore.Get(LastAttemptTagKey, ""), latestTag, StringComparison.OrdinalIgnoreCase) && hasStaged
                ? existingStaged
                : "";

            Notify(ui, NoticeSeverity.Info,
                "Downloading WinForge update", "正在下載 WinForge 更新",
                $"Downloading v{latestTag}. You can keep using the app while it downloads.",
                $"正在下載 v{latestTag}。下載期間可以繼續使用 app。",
                0);
            if (string.IsNullOrWhiteSpace(staged))
                staged = await DownloadInstallerAsync(latestTag, setup.BrowserDownloadUrl, expectedSha256).ConfigureAwait(false);

            SettingsStore.Set(PendingInstallerPathKey, staged);
            SettingsStore.Set(PendingInstallerShaKey, expectedSha256);
            var actions = new[]
            {
                new AppNoticeAction(
                    "Restart to install update",
                    "重新啟動並安裝更新",
                    DismissOnInvoke: true,
                    Handler: async () =>
                    {
                        if (!LaunchInstallerAfterExit(staged, latestTag, expectedSha256))
                        {
                            Notify(ui, NoticeSeverity.Error, "Could not start Squirrel Setup.exe", "無法啟動 Squirrel Setup.exe",
                                "The update is staged, but the safe Squirrel handoff could not start.",
                                "更新已準備好，但安全更新交接未能啟動。", 12000);
                            return;
                        }
                        await Task.Delay(1500).ConfigureAwait(false);
                        ui.TryEnqueue(() => Application.Current.Exit());
                    }),
                new AppNoticeAction("Later", "稍後", DismissOnInvoke: true, Handler: () => Task.CompletedTask),
            };
            Notify(ui, NoticeSeverity.Info,
                "Update ready to install", "更新已準備安裝",
                $"v{latestTag} is staged. It is unsigned and may trigger an unknown-publisher or SmartScreen warning. Choose Restart to install update when your work is saved, or Later.",
                $"v{latestTag} 已準備好。版本冇簽名，可能觸發 unknown-publisher 或 SmartScreen 警告。儲存好工作後揀「重新啟動並安裝更新」，或者揀稍後。",
                0,
                actions);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("app-update", ex);
            if (notifyQuietResult)
                Notify(ui, NoticeSeverity.Error,
                    "Update check failed", "更新檢查失敗",
                    "Auto update hit an error and will retry later.",
                    "自動更新遇到錯誤，稍後會重試。",
                    10000);
        }
    }

    private static void NotifyInstalledIfNeeded(DispatcherQueue ui)
    {
        try
        {
            string attempted = NormalizeTag(SettingsStore.Get(LastAttemptTagKey, ""));
            string current = CurrentVersionTag();
            if (string.IsNullOrWhiteSpace(attempted)) return;
            if (!string.Equals(attempted, current, StringComparison.OrdinalIgnoreCase)) return;
            ClearUpdatePendingFlag();
            SettingsStore.Set(PendingInstallerPathKey, "");
            SettingsStore.Set(PendingInstallerShaKey, "");
            if (string.Equals(SettingsStore.Get(LastInstalledNoticeTagKey, ""), current, StringComparison.OrdinalIgnoreCase)) return;

            SettingsStore.Set(LastInstalledNoticeTagKey, current);
            Notify(ui, NoticeSeverity.Success,
                "WinForge updated", "WinForge 已更新",
                $"Update v{current} installed successfully.",
                $"已成功安裝 v{current}。",
                12000);
        }
        catch { }
    }

    private static void Notify(
        DispatcherQueue ui,
        NoticeSeverity severity,
        string titleEn,
        string titleZh,
        string messageEn,
        string messageZh,
        int autoDismissMs,
        IReadOnlyList<AppNoticeAction>? actions = null)
    {
        var notice = new AppUpdateNotice(severity, titleEn, titleZh, messageEn, messageZh, autoDismissMs, actions);
        void Raise()
        {
            try { Notice?.Invoke(notice); } catch { }
        }

        try
        {
            if (ui.TryEnqueue(Raise)) return;
        }
        catch { }
        Raise();
    }

    private static bool Enabled() =>
        string.Equals(SettingsStore.Get(EnabledKey, "True"), "True", StringComparison.OrdinalIgnoreCase);

    private static bool RecentlyAttempted(string tag)
    {
        if (!string.Equals(SettingsStore.Get(LastAttemptTagKey, ""), tag, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!DateTime.TryParse(SettingsStore.Get(LastAttemptUtcKey, ""), out var when))
            return false;
        return DateTime.UtcNow - when.ToUniversalTime() < SameTagRetryDelay;
    }

    private static async Task<string> DownloadInstallerAsync(string tag, string url, string expectedSha256)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinForge", "updates");
        Directory.CreateDirectory(dir);

        string safeTag = SafeTag(tag);
        string path = Path.Combine(dir, $"Setup-{safeTag}.exe");
        string tmp = path + ".tmp";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

        try
        {
            using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();
            long? total = res.Content.Headers.ContentLength;
                if (total is null or <= 0 or > ManagedReleaseContract.MaximumInstallerBytes)
                throw new InvalidDataException("Installer download size is missing, empty, or exceeds 512 MB.");
            long copied = 0;
            await using (var input = await res.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    if (copied > ManagedReleaseContract.MaximumInstallerBytes - read)
                        throw new InvalidDataException("Installer download exceeded the 512 MB safety limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                    copied += read;
                }
            }
            if (copied != total.Value)
                throw new InvalidDataException("Installer download length did not match GitHub's content length.");
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            throw;
        }

        try { if (File.Exists(path)) File.Delete(path); } catch { }
        File.Move(tmp, path);
        string actual;
        await using (var downloaded = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            actual = Convert.ToHexString(await SHA256.HashDataAsync(downloaded).ConfigureAwait(false));
        if (!ManagedReleaseContract.FixedTimeSha256Equals(expectedSha256, actual))
        {
            try { File.Delete(path); } catch { }
            throw new InvalidDataException("Downloaded installer failed SHA-256 verification.");
        }
        return path;
    }

    private static bool LaunchInstallerAfterExit(string installer, string tag, string expectedSha256)
    {
        try
        {
            string dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string launcher = Path.Combine(dir, "WinForgeLauncher.exe");
            string exe = Path.Combine(dir, "WinForge.exe");
            if (!File.Exists(launcher)) return false;

            string updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "updates");
            Directory.CreateDirectory(updateDir);
            string helper = Path.Combine(updateDir, $"WinForgeApplyUpdate-{Guid.NewGuid():N}.exe");
            string log = Path.Combine(updateDir, $"install-{SafeTag(tag)}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.Copy(launcher, helper, overwrite: false);
            if (!TrySetUpdatePendingFlag()) return false;

            var psi = new ProcessStartInfo
            {
                FileName = helper,
                WorkingDirectory = updateDir,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--apply-update");
            foreach (var (name, value) in new[]
                     {
                         ("--installer", installer), ("--install-dir", dir), ("--launcher", launcher),
                         ("--exe", exe), ("--sha256", expectedSha256), ("--log", log),
                         ("--wait-pid", Environment.ProcessId.ToString())
                     })
            {
                psi.ArgumentList.Add(name);
                psi.ArgumentList.Add(value);
            }
            if (Process.Start(psi) is null) throw new InvalidOperationException("Update helper did not start.");
            return true;
        }
        catch (Exception ex)
        {
            ClearUpdatePendingFlag();
            CrashLogger.Log("app-update:launch-installer", ex);
            return false;
        }
    }

    private static string CurrentVersionTag()
    {
        string? info = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        string value = string.IsNullOrWhiteSpace(info)
            ? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0"
            : info;
        int plus = value.IndexOf('+');
        if (plus >= 0) value = value[..plus];
        return ManagedReleaseContract.NormalizeTag(value);
    }

    private static string NormalizeTag(string? tag) => ManagedReleaseContract.NormalizeTag(tag);

    private static string SafeTag(string? tag) =>
        string.Concat(NormalizeTag(tag).Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));

    private static void CleanupStagedUpdateHelpers()
    {
        try
        {
            string updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "updates");
            if (!Directory.Exists(updateDir)) return;
            foreach (string helper in Directory.EnumerateFiles(updateDir, "WinForgeApplyUpdate-*.exe"))
            {
                try { File.Delete(helper); } catch { /* a just-launched helper may still be mapped */ }
            }
        }
        catch { }
    }

    private static string UpdatePendingFlagPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinForge", "update.pending");

    private static bool TrySetUpdatePendingFlag()
    {
        string path = UpdatePendingFlagPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.WriteLine($"pid={Environment.ProcessId}");
                writer.WriteLine($"utc={DateTime.UtcNow:O}");
                return true;
            }
            catch (IOException)
            {
                DateTime modified = File.GetLastWriteTimeUtc(path);
                if (DateTime.UtcNow - modified <= TimeSpan.FromMinutes(10)) return false;
                File.Delete(path);
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream);
                writer.WriteLine($"pid={Environment.ProcessId}");
                writer.WriteLine($"utc={DateTime.UtcNow:O}");
                return true;
            }
        }
        catch { return false; }
    }

    private static void ClearUpdatePendingFlag()
    {
        try { File.Delete(UpdatePendingFlagPath()); } catch { }
    }

    private static bool IsDevelopmentRun()
    {
        if (Debugger.IsAttached) return true;
        try
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "WinForge.csproj")))
                    return true;
        }
        catch { }
        return false;
    }

    private static HttpClient CreateHttp()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge-AutoUpdater/1.0");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("assets")] GitHubAsset[] Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl,
        [property: JsonPropertyName("digest")] string? Digest,
        [property: JsonPropertyName("size")] long Size);
}
