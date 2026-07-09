using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinForge.Catalog;
using WinForge.Models;

namespace WinForge.Services;

public sealed record GitHubDesktopProfileStatus(
    string Id,
    string Name,
    string DataPath,
    string GitConfigPath,
    bool IsDefault,
    bool IsActive,
    bool DataExists,
    bool StartMenuShortcutExists,
    bool DesktopShortcutExists,
    bool ShortcutsReady)
{
    public bool ShortcutExists => StartMenuShortcutExists || DesktopShortcutExists;
}

public sealed record GitHubDesktopProfilesStatus(
    bool DesktopInstalled,
    string? DesktopExe,
    bool IsConfigured,
    bool BrokerReady,
    bool HandlersOwned,
    bool ShortcutsReady,
    bool CreateStartMenuShortcuts,
    bool CreateDesktopShortcuts,
    string ActiveProfileId,
    IReadOnlyList<GitHubDesktopProfileStatus> Profiles);

/// <summary>
/// WinForge-facing adapter for the BCL-only profile broker. The real GitHub
/// Desktop client remains external; WinForge owns only isolation, launchers,
/// and callback routing for the selected local profile.
/// </summary>
public static class GitHubDesktopProfilesService
{
    private static readonly object Gate = new();

    public static GitHubDesktopProfilesStatus GetStatus()
    {
        try
        {
            var config = GitHubDesktopProfilesCore.LoadConfig();
            string active = GitHubDesktopProfilesCore.ReadActiveProfile();
            string? broker = GitHubDesktopProfilesCore.FindBrokerExecutable();
            string? desktop = ResolveDesktop();
            bool handlersOwned = broker is not null && GitHubDesktopProfilesCore.HandlersOwned(broker);
            bool OwnsStartShortcut(GitHubDesktopProfilesCore.Profile profile) => broker is not null &&
                GitHubDesktopProfilesCore.ShortcutMatchesProfile(
                    GitHubDesktopProfilesCore.GetShortcutPath(GitHubDesktopProfilesCore.StartMenuDirectory, profile.Name),
                    broker,
                    profile.Id,
                    profile.Name);
            bool OwnsDesktopShortcut(GitHubDesktopProfilesCore.Profile profile) => broker is not null &&
                GitHubDesktopProfilesCore.ShortcutMatchesProfile(
                    GitHubDesktopProfilesCore.GetShortcutPath(GitHubDesktopProfilesCore.DesktopDirectory, profile.Name),
                    broker,
                    profile.Id,
                    profile.Name);
            bool HasManagedStartShortcut(GitHubDesktopProfilesCore.Profile profile) => broker is not null &&
                GitHubDesktopProfilesCore.ShortcutOwned(
                    GitHubDesktopProfilesCore.GetShortcutPath(GitHubDesktopProfilesCore.StartMenuDirectory, profile.Name),
                    broker);
            bool HasManagedDesktopShortcut(GitHubDesktopProfilesCore.Profile profile) => broker is not null &&
                GitHubDesktopProfilesCore.ShortcutOwned(
                    GitHubDesktopProfilesCore.GetShortcutPath(GitHubDesktopProfilesCore.DesktopDirectory, profile.Name),
                    broker);
            bool OwnsBrokerShortcut(GitHubDesktopProfilesCore.Profile profile) => broker is not null &&
                (GitHubDesktopProfilesCore.ShortcutMatchesBrokerProfile(
                    GitHubDesktopProfilesCore.GetShortcutPath(GitHubDesktopProfilesCore.StartMenuDirectory, profile.Name),
                    broker,
                    profile.Id)
                 || GitHubDesktopProfilesCore.ShortcutMatchesBrokerProfile(
                    GitHubDesktopProfilesCore.GetShortcutPath(GitHubDesktopProfilesCore.DesktopDirectory, profile.Name),
                    broker,
                    profile.Id));
            var profiles = config.Profiles.Select((profile, index) =>
            {
                bool startExact = OwnsStartShortcut(profile);
                bool desktopExact = OwnsDesktopShortcut(profile);
                bool profileShortcutsReady =
                    (config.CreateStartMenuShortcuts ? startExact : !HasManagedStartShortcut(profile))
                    && (config.CreateDesktopShortcuts ? desktopExact : !HasManagedDesktopShortcut(profile));
                return new GitHubDesktopProfileStatus(
                    profile.Id,
                    profile.Name,
                    profile.DataPath,
                    profile.GitConfigPath,
                    profile.UsesDefaultGitConfig || index == 0,
                    string.Equals(profile.Id, active, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(profile.Name, active, StringComparison.OrdinalIgnoreCase),
                    Directory.Exists(profile.DataPath),
                    startExact,
                    desktopExact,
                    profileShortcutsReady);
            }).ToArray();
            bool shortcutsReady = config.PendingShortcutCleanup.Count == 0
                && GitHubDesktopProfilesCore.OfficialShortcutStateReady(config.CreateStartMenuShortcuts)
                && profiles.All(profile => profile.ShortcutsReady);
            bool isConfigured = config.ManagedByWinForge ||
                (File.Exists(GitHubDesktopProfilesCore.ConfigPath)
                 && handlersOwned
                 && config.Profiles.Any(OwnsBrokerShortcut));

            return new GitHubDesktopProfilesStatus(
                DesktopInstalled: desktop is not null,
                DesktopExe: desktop,
                IsConfigured: isConfigured,
                BrokerReady: broker is not null,
                HandlersOwned: handlersOwned,
                ShortcutsReady: shortcutsReady,
                CreateStartMenuShortcuts: config.CreateStartMenuShortcuts,
                CreateDesktopShortcuts: config.CreateDesktopShortcuts,
                ActiveProfileId: active,
                Profiles: profiles);
        }
        catch
        {
            return new GitHubDesktopProfilesStatus(false, null, false, false, false, false, true, true, "", Array.Empty<GitHubDesktopProfileStatus>());
        }
    }

    public static TweakResult Configure(IReadOnlyList<string> names, bool startMenuShortcuts, bool desktopShortcuts)
    {
        var guard = GuardInteractive();
        if (guard is not null) return guard;
        string? broker = GitHubDesktopProfilesCore.FindBrokerExecutable();
        string? desktop = ResolveDesktop();
        if (broker is null) return TweakResult.Fail(
            "WinForgeLauncher.exe was not found. Publish or reinstall WinForge, then try again.",
            "搵唔到 WinForgeLauncher.exe。請重新發佈或安裝 WinForge，之後再試。");
        if (desktop is null) return TweakResult.Fail(
            "GitHub Desktop is not installed yet.",
            "GitHub Desktop 仲未安裝。請先用上面嘅安裝按鈕。");

        lock (Gate)
        {
            var result = GitHubDesktopProfilesCore.Configure(names, startMenuShortcuts, desktopShortcuts, broker, desktop);
            return Convert(result,
                "GitHub Desktop profiles and callback routing are ready.",
                "GitHub Desktop 設定檔同登入回調路由已經就緒。");
        }
    }

    public static TweakResult Repair(bool startMenuShortcuts, bool desktopShortcuts)
    {
        var names = GitHubDesktopProfilesCore.LoadConfig().Profiles.Select(p => p.Name).ToArray();
        return Configure(names, startMenuShortcuts, desktopShortcuts);
    }

    public static TweakResult Add(string name, bool startMenuShortcuts, bool desktopShortcuts)
    {
        var guard = GuardInteractive();
        if (guard is not null) return guard;
        string? broker = GitHubDesktopProfilesCore.FindBrokerExecutable();
        string? desktop = ResolveDesktop();
        if (broker is null) return TweakResult.Fail("WinForgeLauncher.exe was not found.", "搵唔到 WinForgeLauncher.exe。");
        if (desktop is null) return TweakResult.Fail("GitHub Desktop is not installed yet.", "GitHub Desktop 仲未安裝。");
        var result = GitHubDesktopProfilesCore.AddProfile(name, startMenuShortcuts, desktopShortcuts, broker, desktop);
        return result.Success
            ? TweakResult.Ok("Profile added without changing existing profile data.", "已新增設定檔，現有設定檔資料冇被更改。")
            : Convert(result, "Profile added.", "已新增設定檔。");
    }

    public static TweakResult Remove(string profileId, bool startMenuShortcuts, bool desktopShortcuts)
    {
        var guard = GuardInteractive();
        if (guard is not null) return guard;
        string? broker = GitHubDesktopProfilesCore.FindBrokerExecutable();
        string? desktop = ResolveDesktop();
        if (broker is null) return TweakResult.Fail("WinForgeLauncher.exe was not found.", "搵唔到 WinForgeLauncher.exe。");
        if (desktop is null) return TweakResult.Fail("GitHub Desktop is not installed yet.", "GitHub Desktop 仲未安裝。");

        lock (Gate)
        {
            if (GitHubDesktopProfilesCore.LoadConfig().Profiles.Count <= 1)
                return TweakResult.Fail("At least one profile must remain.", "最少要保留一個設定檔。");

            // Adopt/repair every existing shortcut first. This keeps removal safe when the
            // shared config was originally created by the reusable PowerShell package.
            var result = GitHubDesktopProfilesCore.AdoptAndRemoveProfile(
                profileId, startMenuShortcuts, desktopShortcuts, broker, desktop);
            return Convert(result,
                "Profile removed from WinForge. Its data folder was kept.",
                "已經喺 WinForge 移除設定檔；資料夾會保留。");
        }
    }

    public static TweakResult Launch(string profileId)
    {
        var guard = GuardInteractive();
        if (guard is not null) return guard;
        lock (Gate)
        {
            var result = GitHubDesktopProfilesCore.LaunchProfile(profileId, null, ResolveDesktop(), GitHubDesktopProfilesCore.FindBrokerExecutable());
            return Convert(result, "GitHub Desktop launched for the selected profile.", "已經用所選設定檔啟動 GitHub Desktop。");
        }
    }

    public static TweakResult Activate(string profileId)
    {
        var guard = GuardInteractive();
        if (guard is not null) return guard;
        string? broker = GitHubDesktopProfilesCore.FindBrokerExecutable();
        if (broker is null) return TweakResult.Fail("WinForgeLauncher.exe was not found.", "搵唔到 WinForgeLauncher.exe。");
        lock (Gate)
        {
            var result = GitHubDesktopProfilesCore.SetActive(profileId, broker);
            return Convert(result,
                "This profile will receive the next GitHub Desktop sign-in callback.",
                "下一個 GitHub Desktop 登入回調會交畀呢個設定檔。");
        }
    }

    public static TweakResult Rename(string profileId, string name, bool startMenuShortcuts, bool desktopShortcuts)
    {
        var guard = GuardInteractive();
        if (guard is not null) return guard;
        string? broker = GitHubDesktopProfilesCore.FindBrokerExecutable();
        string? desktop = ResolveDesktop();
        if (broker is null) return TweakResult.Fail("WinForgeLauncher.exe was not found.", "搵唔到 WinForgeLauncher.exe。");
        if (desktop is null) return TweakResult.Fail("GitHub Desktop is not installed yet.", "GitHub Desktop 仲未安裝。");
        lock (Gate)
        {
            var result = GitHubDesktopProfilesCore.Rename(profileId, name, startMenuShortcuts, desktopShortcuts, broker, desktop);
            return Convert(result, "Profile renamed and shortcuts refreshed.", "設定檔已改名，捷徑亦已更新。");
        }
    }

    public static TweakResult OpenFolder(string profileId)
    {
        try
        {
            var profile = GitHubDesktopProfilesCore.LoadConfig().Profiles.FirstOrDefault(p =>
                string.Equals(p.Id, profileId, StringComparison.OrdinalIgnoreCase));
            if (profile is null) return TweakResult.Fail("Profile not found.", "搵唔到設定檔。");
            Directory.CreateDirectory(profile.DataPath);
            if (!UserProcessLauncher.TryStart(profile.DataPath, "", null, out string error))
                return TweakResult.Fail($"Could not open the profile folder: {error}", $"開唔到設定檔資料夾：{error}");
            return TweakResult.Ok("Profile folder opened.", "已開啟設定檔資料夾。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not open the profile folder: {ex.Message}", $"開唔到設定檔資料夾：{ex.Message}");
        }
    }

    public static TweakResult Uninstall()
    {
        var guard = GuardInteractive();
        if (guard is not null) return guard;
        string? broker = GitHubDesktopProfilesCore.FindBrokerExecutable();
        if (broker is null) return TweakResult.Fail("WinForgeLauncher.exe was not found.", "搵唔到 WinForgeLauncher.exe。");
        lock (Gate)
        {
            var result = GitHubDesktopProfilesCore.Uninstall(broker);
            return Convert(result,
                "WinForge shortcuts and callback routing were removed. Profile data was kept.",
                "已移除 WinForge 捷徑同回調路由；設定檔資料會保留。");
        }
    }

    private static string? ResolveDesktop()
    {
        var spec = ExternalApps.ById("githubdesktop");
        return spec is null ? GitHubDesktopProfilesCore.FindGitHubDesktopExecutable()
            : ExternalAppService.ResolveExe(spec) ?? GitHubDesktopProfilesCore.FindGitHubDesktopExecutable();
    }

    private static TweakResult? GuardInteractive() => AdminHelper.IsElevated
        ? TweakResult.Fail(
            "Profile launching is disabled while WinForge is elevated. Restart WinForge normally and try again.",
            "WinForge 以管理員身份運行嗰陣唔會啟動設定檔。請用普通權限重開 WinForge 再試。")
        : null;

    private static TweakResult Convert(GitHubDesktopProfilesCore.Result result, string okEn, string okZh) =>
        result.Success
            ? TweakResult.Ok(okEn, okZh)
            : TweakResult.Fail($"GitHub Desktop profile operation failed: {result.Error}", $"GitHub Desktop 設定檔操作失敗：{result.Error}", result.Error);
}
