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
    private const string LatestReleaseApi = "https://api.github.com/repos/codingmachineedge/WinForge/releases/latest";
    private static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan SameTagRetryDelay = TimeSpan.FromHours(12);
    private static readonly HttpClient Http = CreateHttp();
    private static int _started;

    public static void StartAutomaticChecks(DispatcherQueue ui)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        _ = Task.Run(() => Loop(ui));
    }

    private static async Task Loop(DispatcherQueue ui)
    {
        await Task.Delay(FirstCheckDelay).ConfigureAwait(false);
        while (true)
        {
            await CheckAndApplyLatestAsync(ui).ConfigureAwait(false);
            await Task.Delay(CheckInterval).ConfigureAwait(false);
        }
    }

    private static async Task CheckAndApplyLatestAsync(DispatcherQueue ui)
    {
        if (!Enabled()) return;
        if (IsDevelopmentRun()) return;

        try
        {
            using var res = await Http.GetAsync(LatestReleaseApi).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return;

            await using var stream = await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream).ConfigureAwait(false);
            if (release is null || release.Draft || release.Prerelease) return;

            string latestTag = NormalizeTag(release.TagName);
            if (!IsNewer(latestTag, CurrentVersionTag())) return;
            if (RecentlyAttempted(latestTag)) return;

            var setup = release.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, "WinForge-Setup.exe", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(a.BrowserDownloadUrl));
            if (setup is null) return;

            string installer = await DownloadInstallerAsync(latestTag, setup.BrowserDownloadUrl).ConfigureAwait(false);
            SettingsStore.Set(LastAttemptTagKey, latestTag);
            SettingsStore.Set(LastAttemptUtcKey, DateTime.UtcNow.ToString("O"));

            if (LaunchInstallerAfterExit(installer, latestTag))
                ui.TryEnqueue(() => Application.Current.Exit());
        }
        catch (Exception ex)
        {
            CrashLogger.Log("app-update", ex);
        }
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
