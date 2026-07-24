using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Executes Browser Control plans without a command shell. Ephemeral profiles are GUID-scoped and
/// tracked until the launched browser exits; cleanup is bounded and stale owned sessions are retried
/// only while both supported browsers are closed. · 唔經 command shell 執行瀏覽器方案；臨時設定檔
/// 會用 GUID 分隔，並追蹤到瀏覽器退出先清理。
/// </summary>
public static class BrowserControlService
{
    private static readonly ConcurrentDictionary<Guid, Process> OwnedSessions = new();

    public static IReadOnlyList<BrowserProfile> Profiles(ChromiumBrowser browser)
        => BrowserControlCore.DiscoverProfiles(browser);

    public static IReadOnlyList<BrowserPwa> Pwas(ChromiumBrowser? browser = null)
    {
        var all = BrowserControlCore.DiscoverPwas();
        return browser is null ? all : all.Where(p => p.Browser == browser.Value).ToArray();
    }

    public static TweakResult LaunchAppMode(ChromiumBrowser browser, string url)
        => Launch(BrowserControlCore.BuildAppModePlan(browser, RequiredExecutable(browser), url));

    public static TweakResult LaunchKiosk(ChromiumBrowser browser, string url)
        => Launch(BrowserControlCore.BuildKioskPlan(browser, RequiredExecutable(browser), url));

    public static TweakResult LaunchProfile(BrowserProfile profile)
        => Launch(BrowserControlCore.BuildProfilePlan(profile, RequiredExecutable(profile.Browser)));

    public static TweakResult LaunchPwa(BrowserPwa pwa)
        => Launch(BrowserControlCore.BuildPwaPlan(pwa, RequiredExecutable(pwa.Browser)));

    public static TweakResult OpenInternalPage(ChromiumBrowser browser, bool policyPage)
        => Launch(BrowserControlCore.BuildInternalPagePlan(browser, RequiredExecutable(browser), policyPage));

    public static TweakResult LaunchProxy(ChromiumBrowser browser, string proxy, string? bypass, string url)
    {
        var directory = BrowserControlCore.CreateEphemeralDirectory(browser, "proxy", null, out var root);
        try
        {
            return Launch(BrowserControlCore.BuildProxyPlan(
                browser, RequiredExecutable(browser), proxy, bypass, url, directory, root));
        }
        catch
        {
            BrowserControlCore.TryDeleteEphemeralDirectory(directory, root);
            throw;
        }
    }

    public static TweakResult LaunchThrowaway(ChromiumBrowser browser, string url)
    {
        var directory = BrowserControlCore.CreateEphemeralDirectory(browser, "throwaway", null, out var root);
        try
        {
            return Launch(BrowserControlCore.BuildThrowawayPlan(
                browser, RequiredExecutable(browser), url, directory, root));
        }
        catch
        {
            BrowserControlCore.TryDeleteEphemeralDirectory(directory, root);
            throw;
        }
    }

    public static TweakResult LaunchFeature(
        ChromiumBrowser browser,
        string featureNames,
        BrowserFeatureMode mode,
        string url)
    {
        var directory = BrowserControlCore.CreateEphemeralDirectory(browser, "feature", null, out var root);
        try
        {
            return Launch(BrowserControlCore.BuildFeaturePlan(
                browser, RequiredExecutable(browser), featureNames, mode, url, directory, root));
        }
        catch
        {
            BrowserControlCore.TryDeleteEphemeralDirectory(directory, root);
            throw;
        }
    }

    public static TweakResult LaunchRemoteDebug(ChromiumBrowser browser, int port, string url)
    {
        var directory = BrowserControlCore.CreateEphemeralDirectory(browser, "debug", null, out var root);
        try
        {
            return Launch(BrowserControlCore.BuildRemoteDebugPlan(
                browser, RequiredExecutable(browser), port, url, directory, root));
        }
        catch
        {
            BrowserControlCore.TryDeleteEphemeralDirectory(directory, root);
            throw;
        }
    }

    public static TweakResult ClearProfileCache(BrowserProfile profile)
    {
        try
        {
            var report = BrowserControlCore.ClearProfileCaches(profile);
            var missing = report.MissingDirectories.Count == 0
                ? string.Empty
                : $" Missing: {string.Join(", ", report.MissingDirectories)}.";
            return TweakResult.Ok(
                $"Cleared {report.DeletedDirectoryCount} cache folders for {profile.DisplayName}.{missing}",
                $"已為 {profile.DisplayName} 清除 {report.DeletedDirectoryCount} 個快取資料夾。{(report.MissingDirectories.Count == 0 ? string.Empty : $" 未存在：{string.Join("、", report.MissingDirectories)}。")}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail(ex.Message, $"清除快取失敗：{ex.Message}");
        }
    }

    public static Task<TweakResult> RunWingetAsync(
        ChromiumBrowser browser,
        BrowserPackageAction action,
        CancellationToken cancellationToken = default)
    {
        var plan = BrowserControlCore.BuildWingetPlan(browser, action);
        return ShellRunner.RunArguments(
            ShellRunner.ResolveExe("winget.exe"),
            plan.Arguments,
            elevated: false,
            ct: cancellationToken);
    }

    public static void CleanupStaleSessions(TimeSpan? minimumAge = null)
    {
        if (BrowserControlCore.BrowserProcessIsRunning(ChromiumBrowser.Chrome)
            || BrowserControlCore.BrowserProcessIsRunning(ChromiumBrowser.Edge))
            return;

        var root = Path.Combine(Path.GetTempPath(), "WinForge", "BrowserSessions");
        if (!Directory.Exists(root)) return;
        var age = minimumAge ?? TimeSpan.FromDays(1);
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try
            {
                if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(directory) < age) continue;
                BrowserControlCore.TryDeleteEphemeralDirectory(directory, root);
            }
            catch { }
        }
    }

    private static string RequiredExecutable(ChromiumBrowser browser)
        => BrowserControlCore.ResolveExecutable(browser)
            ?? throw new FileNotFoundException($"{BrowserControlCore.BrowserName(browser)} was not found. Install it first or use the winget actions in Browser Control.");

    private static TweakResult Launch(BrowserLaunchPlan plan)
    {
        if (AdminHelper.IsElevated)
        {
            CleanupFailedPlan(plan);
            return TweakResult.Fail(
                "Browser launches are disabled while WinForge is elevated. Restart WinForge normally so the browser does not inherit administrator rights.",
                "WinForge 用緊系統管理員權限時唔會開瀏覽器。請用一般權限重新開 WinForge，避免瀏覽器繼承管理員權限。");
        }

        var start = new ProcessStartInfo
        {
            FileName = plan.FileName,
            UseShellExecute = false,
        };
        foreach (var argument in plan.Arguments) start.ArgumentList.Add(argument);

        var process = new Process { StartInfo = start, EnableRaisingEvents = true };
        var key = Guid.NewGuid();
        OwnedSessions[key] = process;
        process.Exited += (_, _) => _ = Task.Run(async () =>
        {
            try
            {
                if (plan.EphemeralDirectory is not null && plan.EphemeralRoot is not null)
                {
                    for (var attempt = 0; attempt < 40; attempt++)
                    {
                        if (BrowserControlCore.TryDeleteEphemeralDirectory(plan.EphemeralDirectory, plan.EphemeralRoot)) break;
                        await Task.Delay(TimeSpan.FromSeconds(1.5)).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                OwnedSessions.TryRemove(key, out _);
                process.Dispose();
            }
        });

        try
        {
            if (!process.Start())
            {
                OwnedSessions.TryRemove(key, out _);
                process.Dispose();
                CleanupFailedPlan(plan);
                return TweakResult.Fail("The browser process did not start.", "瀏覽器程序未能啟動。");
            }
            return TweakResult.Ok(
                $"Started {BrowserControlCore.BrowserName(plan.Browser)} with a validated argument list.",
                $"已用驗證過嘅參數清單啟動 {BrowserControlCore.BrowserName(plan.Browser)}。");
        }
        catch (Exception ex)
        {
            OwnedSessions.TryRemove(key, out _);
            process.Dispose();
            CleanupFailedPlan(plan);
            return TweakResult.Fail(ex.Message, $"啟動失敗：{ex.Message}");
        }
    }

    private static void CleanupFailedPlan(BrowserLaunchPlan plan)
    {
        if (plan.EphemeralDirectory is not null && plan.EphemeralRoot is not null)
            BrowserControlCore.TryDeleteEphemeralDirectory(plan.EphemeralDirectory, plan.EphemeralRoot);
    }
}
