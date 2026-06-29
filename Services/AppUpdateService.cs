using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WinForge.Services;

/// <summary>
/// Fully automatic GitHub release updater. It checks the latest WinForge release, downloads the
/// installer asset, exits this process, silently installs into the current app folder, then relaunches.
/// </summary>
public static class AppUpdateService
{
    private const string EnabledKey = "app.autoupdate.enabled";
    private const string LastAttemptTagKey = "app.autoupdate.lastAttemptTag";
    private const string LastAttemptUtcKey = "app.autoupdate.lastAttemptUtc";
    private const string LastInstalledNoticeTagKey = "app.autoupdate.lastInstalledNoticeTag";
    private const string LatestReleaseApi = "https://api.github.com/repos/codingmachineedge/WinForge/releases/latest";
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
        int AutoDismissMs = 0);

    public static event Action<AppUpdateNotice>? Notice;

    public static void StartAutomaticChecks(DispatcherQueue ui)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
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

        try
        {
            if (notifyQuietResult)
                Notify(ui, NoticeSeverity.Info,
                    "Checking for updates", "正在檢查更新",
                    "WinForge is checking GitHub releases in the background.",
                    "WinForge 正在背景檢查 GitHub release。",
                    5000);

            using var res = await Http.GetAsync(LatestReleaseApi).ConfigureAwait(false);
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

            string latestTag = NormalizeTag(release.TagName);
            string currentTag = CurrentVersionTag();
            if (!IsNewer(latestTag, currentTag))
            {
                if (notifyQuietResult)
                    Notify(ui, NoticeSeverity.Success,
                        "WinForge is up to date", "WinForge 已是最新",
                        $"Current version: v{currentTag}.",
                        $"目前版本：v{currentTag}。",
                        7000);
                return;
            }
            if (RecentlyAttempted(latestTag)) return;

            var setup = release.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, "WinForge-Setup.exe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl));
            if (setup is null)
            {
                Notify(ui, NoticeSeverity.Warning,
                    "Update found, installer missing", "找到更新但缺少安裝程式",
                    $"Release v{latestTag} does not include WinForge-Setup.exe. Auto update will retry later.",
                    $"Release v{latestTag} 沒有包含 WinForge-Setup.exe。自動更新稍後會重試。",
                    12000);
                return;
            }

            SettingsStore.Set(LastAttemptTagKey, latestTag);
            SettingsStore.Set(LastAttemptUtcKey, DateTime.UtcNow.ToString("O"));

            // Preferred path: hand off to the dedicated WinForgeUpdater WinUI app, which shows a real
            // progress window (download → wait → install → relaunch) so the update is never silent.
            if (LaunchUpdaterApp(latestTag, setup.BrowserDownloadUrl))
            {
                Notify(ui, NoticeSeverity.Info,
                    "Updating WinForge", "正在更新 WinForge",
                    $"WinForge will close and the updater will install v{latestTag} with a progress window, then reopen.",
                    $"WinForge 將會關閉，更新程式會用進度視窗安裝 v{latestTag}，然後重新開啟。",
                    0);
                await Task.Delay(2500).ConfigureAwait(false);
                ui.TryEnqueue(() => Application.Current.Exit());
                return;
            }

            // Fallback (older installs without WinForgeUpdater.exe): download in-app, then the silent script.
            Notify(ui, NoticeSeverity.Info,
                "Downloading WinForge update", "正在下載 WinForge 更新",
                $"Downloading v{latestTag}. You can keep using the app while it downloads.",
                $"正在下載 v{latestTag}。下載期間可以繼續使用 app。",
                0);
            string installer = await DownloadInstallerAsync(latestTag, setup.BrowserDownloadUrl).ConfigureAwait(false);

            if (LaunchInstallerAfterExit(installer, latestTag))
            {
                Notify(ui, NoticeSeverity.Info,
                    "Installing update", "正在安裝更新",
                    $"WinForge will close, install v{latestTag}, then reopen automatically.",
                    $"WinForge 將會關閉、安裝 v{latestTag}，然後自動重新開啟。",
                    0);
                await Task.Delay(2500).ConfigureAwait(false);
                ui.TryEnqueue(() => Application.Current.Exit());
            }
            else
            {
                Notify(ui, NoticeSeverity.Error,
                    "Could not start updater", "無法啟動更新程式",
                    $"The v{latestTag} installer was downloaded, but WinForge could not start the updater.",
                    $"已下載 v{latestTag} 安裝程式，但 WinForge 無法啟動更新程式。",
                    12000);
            }
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
        int autoDismissMs)
    {
        var notice = new AppUpdateNotice(severity, titleEn, titleZh, messageEn, messageZh, autoDismissMs);
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

    private static async Task<string> DownloadInstallerAsync(string tag, string url)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinForge", "updates");
        Directory.CreateDirectory(dir);

        string safeTag = SafeTag(tag);
        string path = Path.Combine(dir, $"WinForge-Setup-{safeTag}.exe");
        string tmp = path + ".tmp";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

        using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        await using (var input = await res.Content.ReadAsStreamAsync().ConfigureAwait(false))
        await using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            await input.CopyToAsync(output).ConfigureAwait(false);

        try { if (File.Exists(path)) File.Delete(path); } catch { }
        File.Move(tmp, path);
        return path;
    }

    /// <summary>
    /// 啟動專用 WinUI 更新程式（有進度視窗）· Launch the dedicated WinForgeUpdater WinUI app, which
    /// downloads (with a progress bar), waits for this process to exit, installs, and relaunches WinForge.
    /// Returns false if the updater exe isn't present (then the caller uses the silent fallback).
    /// </summary>
    private static bool LaunchUpdaterApp(string tag, string url)
    {
        try
        {
            string dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string updater = Path.Combine(dir, "WinForgeUpdater.exe");
            if (!File.Exists(updater)) return false;

            var psi = new ProcessStartInfo { FileName = updater, UseShellExecute = true };
            psi.ArgumentList.Add("--tag"); psi.ArgumentList.Add(tag);
            psi.ArgumentList.Add("--url"); psi.ArgumentList.Add(url);
            psi.ArgumentList.Add("--pid"); psi.ArgumentList.Add(Environment.ProcessId.ToString());
            psi.ArgumentList.Add("--install-dir"); psi.ArgumentList.Add(dir);
            psi.ArgumentList.Add("--exe"); psi.ArgumentList.Add(Path.Combine(dir, "WinForge.exe"));
            psi.ArgumentList.Add("--launcher"); psi.ArgumentList.Add(Path.Combine(dir, "WinForgeLauncher.exe"));
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            CrashLogger.Log("app-update:launch-updater-app", ex);
            return false;
        }
    }

    private static bool LaunchInstallerAfterExit(string installer, string tag)
    {
        try
        {
            string dir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string launcher = Path.Combine(dir, "WinForgeLauncher.exe");
            string exe = Path.Combine(dir, "WinForge.exe");
            string script = WriteUpdaterScript(tag);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(script);
            psi.ArgumentList.Add("-PidToWait");
            psi.ArgumentList.Add(Environment.ProcessId.ToString());
            psi.ArgumentList.Add("-Installer");
            psi.ArgumentList.Add(installer);
            psi.ArgumentList.Add("-InstallDir");
            psi.ArgumentList.Add(dir);
            psi.ArgumentList.Add("-Launcher");
            psi.ArgumentList.Add(launcher);
            psi.ArgumentList.Add("-Exe");
            psi.ArgumentList.Add(exe);
            Process.Start(psi);
            return true;
        }
        catch (Exception ex)
        {
            CrashLogger.Log("app-update:launch-installer", ex);
            return false;
        }
    }

    private static string WriteUpdaterScript(string tag)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinForge", "updates");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, $"apply-{SafeTag(tag)}.ps1");
        File.WriteAllText(path, """
param(
  [int]$PidToWait,
  [string]$Installer,
  [string]$InstallDir,
  [string]$Launcher,
  [string]$Exe
)
$ErrorActionPreference = 'SilentlyContinue'
try { Wait-Process -Id $PidToWait -Timeout 90 } catch {}
$args = @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CLOSEAPPLICATIONS', ('/DIR="' + $InstallDir + '"'))
$p = Start-Process -FilePath $Installer -ArgumentList $args -Wait -PassThru
$target = $null
if (Test-Path $Launcher) { $target = $Launcher }
elseif (Test-Path $Exe) { $target = $Exe }
if ($target) { Start-Process -FilePath $target -WorkingDirectory (Split-Path -Parent $target) }
""");
        return path;
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
        return NormalizeTag(value);
    }

    private static bool IsNewer(string latestTag, string currentTag)
    {
        if (!TryVersion(latestTag, out var latest) || !TryVersion(currentTag, out var current))
            return !string.Equals(latestTag, currentTag, StringComparison.OrdinalIgnoreCase);
        return latest > current;
    }

    private static bool TryVersion(string tag, out Version version) =>
        Version.TryParse(NormalizeTag(tag), out version!);

    private static string NormalizeTag(string? tag) =>
        (tag ?? "").Trim().TrimStart('v', 'V');

    private static string SafeTag(string? tag) =>
        string.Concat(NormalizeTag(tag).Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_'));

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
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
