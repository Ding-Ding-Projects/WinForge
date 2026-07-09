using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// BCL-only GitHub Desktop profile broker shared by WinForge and WinForgeLauncher.
/// It never reads credentials or logs protocol URLs; it only isolates Electron data,
/// Git's global-config path, shortcuts, and callback routing.
/// </summary>
public static class GitHubDesktopProfilesCore
{
    public const int CurrentSchema = 3;
    public const string StateKeyPath = @"Software\GitHubDesktopProfiles";
    public static readonly string[] Protocols = ["x-github-client", "github-windows", "x-github-desktop-auth"];
    private static readonly Semaphore ConfigSemaphore = new(1, 1, "Local\\WinForge.GitHubDesktopProfiles.Config");

    public static string InstallRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktopProfiles");
    public static string ConfigPath => Path.Combine(InstallRoot, "profiles.json");
    public static string StartMenuDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Windows", "Start Menu", "Programs", "GitHub, Inc");
    public static string DesktopDirectory => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public static string GetShortcutPath(string folder, string profileName) => ShortcutPath(folder, profileName);

    public sealed class Profile
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string DataPath { get; set; } = "";
        public string GitConfigPath { get; set; } = "";
        public bool UsesDefaultGitConfig { get; set; }
    }

    public sealed class Config
    {
        public int SchemaVersion { get; set; } = CurrentSchema;
        public List<Profile> Profiles { get; set; } = [];
        public List<string> PendingShortcutCleanup { get; set; } = [];
        public Dictionary<string, string?> OriginalHandlers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool CreateStartMenuShortcuts { get; set; } = true;
        public bool CreateDesktopShortcuts { get; set; } = true;
        public bool ManagedByWinForge { get; set; }
        public string DesktopExePath { get; set; } = "";
    }

    public sealed record Result(bool Success, string Error = "")
    {
        public static Result Ok() => new(true);
        public static Result Fail(string error) => new(false, error);
    }

    public static Config LoadConfig()
    {
        Config? config = null;
        try
        {
            if (File.Exists(ConfigPath))
            {
                config = JsonSerializer.Deserialize<Config>(File.ReadAllText(ConfigPath), new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
        }
        catch { }

        config ??= new Config();
        config.Profiles ??= [];
        config.PendingShortcutCleanup ??= [];
        if (config.Profiles.Count == 0) config.Profiles = DefaultProfiles();
        if (config.OriginalHandlers is null)
            config.OriginalHandlers = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < config.Profiles.Count; i++)
        {
            var profile = config.Profiles[i];
            profile.Name = string.IsNullOrWhiteSpace(profile.Name) ? $"Profile {i + 1}" : profile.Name.Trim();
            profile.Id = UniqueId(string.IsNullOrWhiteSpace(profile.Id) ? Slug(profile.Name) : Slug(profile.Id), ids);
            if (string.IsNullOrWhiteSpace(profile.DataPath)) profile.DataPath = DefaultDataPath(i, profile.Name);
            if (string.IsNullOrWhiteSpace(profile.GitConfigPath))
                profile.GitConfigPath = i == 0
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig")
                    : Path.Combine(profile.DataPath, "gitconfig");
            if (i == 0) profile.UsesDefaultGitConfig = true;
        }
        config.SchemaVersion = CurrentSchema;
        return config;
    }

    public static Result SaveConfig(Config config)
    {
        try
        {
            Directory.CreateDirectory(InstallRoot);
            var temp = ConfigPath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temp, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temp, ConfigPath, true);
            return Result.Ok();
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    public static Result Configure(IReadOnlyList<string> names, bool createStartMenuShortcuts, bool createDesktopShortcuts, string brokerExe, string desktopExe) =>
        WithConfigLock(() => ConfigureCore(names, createStartMenuShortcuts, createDesktopShortcuts, brokerExe, desktopExe));

    public static Result AddProfile(string name, bool createStartMenuShortcuts, bool createDesktopShortcuts, string brokerExe, string desktopExe) =>
        WithConfigLock(() =>
        {
            var names = LoadConfig().Profiles.Select(p => p.Name).ToArray();
            var adopted = ConfigureCore(names, createStartMenuShortcuts, createDesktopShortcuts, brokerExe, desktopExe);
            return adopted.Success
                ? AddProfileCore(name, createStartMenuShortcuts, createDesktopShortcuts, brokerExe, desktopExe)
                : adopted;
        });

    private static Result AddProfileCore(string name, bool createStartMenuShortcuts, bool createDesktopShortcuts, string brokerExe, string desktopExe)
    {
        var error = ValidateName(name);
        if (error is not null) return Result.Fail(error);
        if (!File.Exists(brokerExe)) return Result.Fail("WinForgeLauncher.exe could not be located.");
        if (!File.Exists(desktopExe)) return Result.Fail("GitHub Desktop is not installed.");

        bool configSaved = false;
        try
        {
            var config = LoadConfig();
            string normalized = name.Trim();
            if (config.Profiles.Any(p => string.Equals(p.Name, normalized, StringComparison.OrdinalIgnoreCase)))
                return Result.Fail("Profile names must be unique.");

            var usedIds = new HashSet<string>(config.Profiles.Select(p => p.Id), StringComparer.OrdinalIgnoreCase);
            string dataPath = NewProfileDataPath(normalized, config.Profiles);
            var profile = new Profile
            {
                Id = UniqueId(Slug(normalized), usedIds),
                Name = normalized,
                DataPath = dataPath,
                GitConfigPath = Path.Combine(dataPath, "gitconfig"),
                UsesDefaultGitConfig = false,
            };
            if (createStartMenuShortcuts)
                EnsureShortcutReplaceable(ShortcutPath(StartMenuDirectory, profile.Name), brokerExe);
            if (createDesktopShortcuts)
                EnsureShortcutReplaceable(ShortcutPath(DesktopDirectory, profile.Name), brokerExe);
            EnsureProfileFiles(profile);
            config.Profiles.Add(profile);
            config.CreateStartMenuShortcuts = createStartMenuShortcuts;
            config.CreateDesktopShortcuts = createDesktopShortcuts;
            config.ManagedByWinForge = true;
            config.DesktopExePath = desktopExe;

            var saved = SaveConfig(config);
            if (!saved.Success) return saved;
            configSaved = true;

            ReconcileProfileShortcuts(profile, createStartMenuShortcuts, createDesktopShortcuts, brokerExe, desktopExe);
            return Result.Ok();
        }
        catch (Exception ex) { return OperationFailure(ex, configSaved); }
    }

    private static Result ConfigureCore(IReadOnlyList<string> names, bool createStartMenuShortcuts, bool createDesktopShortcuts, string brokerExe, string desktopExe)
    {
        if (names.Count == 0) return Result.Fail("At least one profile is required.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names)
        {
            var error = ValidateName(name);
            if (error is not null) return Result.Fail(error);
            if (!seen.Add(name.Trim())) return Result.Fail("Profile names must be unique.");
        }
        if (!File.Exists(brokerExe)) return Result.Fail("WinForgeLauncher.exe could not be located.");
        if (!File.Exists(desktopExe)) return Result.Fail("GitHub Desktop is not installed.");

        bool configSaved = false;
        try
        {
            var config = LoadConfig();
            string activeBefore = ReadActiveProfile();
            var previousNames = config.Profiles.Select(p => p.Name)
                .Concat(config.PendingShortcutCleanup)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var protocol in Protocols)
            {
                if (!config.OriginalHandlers.ContainsKey(protocol)) config.OriginalHandlers[protocol] = ReadHandler(protocol);
            }

            while (config.Profiles.Count < names.Count)
            {
                int i = config.Profiles.Count;
                config.Profiles.Add(new Profile
                {
                    Id = Slug(names[i]), Name = names[i].Trim(), DataPath = DefaultDataPath(i, names[i]),
                    GitConfigPath = i == 0
                        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig")
                        : Path.Combine(DefaultDataPath(i, names[i]), "gitconfig"),
                    UsesDefaultGitConfig = i == 0,
                });
            }
            if (config.Profiles.Count > names.Count) config.Profiles.RemoveRange(names.Count, config.Profiles.Count - names.Count);

            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < names.Count; i++)
            {
                config.Profiles[i].Name = names[i].Trim();
                config.Profiles[i].Id = UniqueId(string.IsNullOrWhiteSpace(config.Profiles[i].Id)
                    ? Slug(names[i]) : Slug(config.Profiles[i].Id), usedIds);
                config.Profiles[i].UsesDefaultGitConfig = i == 0;
                EnsureProfileFiles(config.Profiles[i]);
            }
            config.CreateStartMenuShortcuts = createStartMenuShortcuts;
            config.CreateDesktopShortcuts = createDesktopShortcuts;
            config.ManagedByWinForge = true;
            config.DesktopExePath = desktopExe;
            config.PendingShortcutCleanup = previousNames.ToList();

            EnsureOfficialShortcutStateCanBeManaged(createStartMenuShortcuts);
            foreach (var profile in config.Profiles)
            {
                if (createStartMenuShortcuts)
                    EnsureShortcutReplaceable(ShortcutPath(StartMenuDirectory, profile.Name), brokerExe);
                if (createDesktopShortcuts)
                    EnsureShortcutReplaceable(ShortcutPath(DesktopDirectory, profile.Name), brokerExe);
            }

            // Save the complete desired state before touching links. If a later
            // COM or filesystem operation fails, Repair routing can finish the
            // same target configuration deterministically.
            var save = SaveConfig(config);
            if (!save.Success) return save;
            configSaved = true;

            ReconcileOfficialShortcut(createStartMenuShortcuts);
            foreach (var oldName in previousNames)
            {
                DeleteShortcutIfOwned(ShortcutPath(StartMenuDirectory, oldName), brokerExe);
                DeleteShortcutIfOwned(ShortcutPath(DesktopDirectory, oldName), brokerExe);
            }
            foreach (var profile in config.Profiles)
                ReconcileProfileShortcuts(profile, createStartMenuShortcuts, createDesktopShortcuts, brokerExe, desktopExe);

            config.PendingShortcutCleanup.Clear();
            var cleanupSave = SaveConfig(config);
            if (!cleanupSave.Success)
                return Result.Fail(cleanupSave.Error + " Shortcuts are ready, but cleanup state could not be cleared; run Repair routing.");

            var activeProfile = config.Profiles.FirstOrDefault(p => IdEquals(p, activeBefore)) ?? config.Profiles[0];
            var activeResult = SetActiveCore(activeProfile.Id, brokerExe, reassert: false);
            return activeResult.Success
                ? activeResult
                : Result.Fail(activeResult.Error + " Configuration was saved; run Repair routing to complete the operation.");
        }
        catch (Exception ex) { return OperationFailure(ex, configSaved); }
    }

    public static Result Rename(string profileId, string newName, bool createStartMenuShortcuts, bool createDesktopShortcuts, string brokerExe, string desktopExe) =>
        WithConfigLock(() => RenameCore(profileId, newName, createStartMenuShortcuts, createDesktopShortcuts, brokerExe, desktopExe));

    private static Result RenameCore(string profileId, string newName, bool createStartMenuShortcuts, bool createDesktopShortcuts, string brokerExe, string desktopExe)
    {
        var error = ValidateName(newName);
        if (error is not null) return Result.Fail(error);
        if (!File.Exists(brokerExe)) return Result.Fail("WinForgeLauncher.exe could not be located.");
        if (!File.Exists(desktopExe)) return Result.Fail("GitHub Desktop is not installed.");
        bool configSaved = false;
        try
        {
            var config = LoadConfig();
            if (config.Profiles.Any(p => !IdEquals(p, profileId) && string.Equals(p.Name, newName.Trim(), StringComparison.OrdinalIgnoreCase)))
                return Result.Fail("Profile names must be unique.");
            var profile = config.Profiles.FirstOrDefault(p => IdEquals(p, profileId));
            if (profile is null) return Result.Fail("Profile not found.");
            var previousNames = config.Profiles.Select(p => p.Name)
                .Concat(config.PendingShortcutCleanup)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            profile.Name = newName.Trim();
            config.CreateStartMenuShortcuts = createStartMenuShortcuts;
            config.CreateDesktopShortcuts = createDesktopShortcuts;
            config.ManagedByWinForge = true;
            config.DesktopExePath = desktopExe;
            config.PendingShortcutCleanup = previousNames.ToList();

            EnsureOfficialShortcutStateCanBeManaged(createStartMenuShortcuts);
            foreach (var current in config.Profiles)
            {
                if (createStartMenuShortcuts)
                    EnsureShortcutReplaceable(ShortcutPath(StartMenuDirectory, current.Name), brokerExe);
                if (createDesktopShortcuts)
                    EnsureShortcutReplaceable(ShortcutPath(DesktopDirectory, current.Name), brokerExe);
            }

            var saved = SaveConfig(config);
            if (!saved.Success) return saved;
            configSaved = true;

            ReconcileOfficialShortcut(createStartMenuShortcuts);
            foreach (var previousName in previousNames)
            {
                DeleteShortcutIfOwned(ShortcutPath(StartMenuDirectory, previousName), brokerExe);
                DeleteShortcutIfOwned(ShortcutPath(DesktopDirectory, previousName), brokerExe);
            }
            foreach (var current in config.Profiles)
                ReconcileProfileShortcuts(current, createStartMenuShortcuts, createDesktopShortcuts, brokerExe, desktopExe);
            config.PendingShortcutCleanup.Clear();
            var cleanupSave = SaveConfig(config);
            if (!cleanupSave.Success)
                return Result.Fail(cleanupSave.Error + " Shortcuts are ready, but cleanup state could not be cleared; run Repair routing.");
            return Result.Ok();
        }
        catch (Exception ex) { return OperationFailure(ex, configSaved); }
    }

    public static Result RemoveProfile(string profileId, string brokerExe) =>
        WithConfigLock(() => RemoveProfileCore(profileId, brokerExe));

    public static Result AdoptAndRemoveProfile(
        string profileId, bool createStartMenuShortcuts, bool createDesktopShortcuts, string brokerExe, string desktopExe) =>
        WithConfigLock(() =>
        {
            var names = LoadConfig().Profiles.Select(p => p.Name).ToArray();
            var adopted = ConfigureCore(names, createStartMenuShortcuts, createDesktopShortcuts, brokerExe, desktopExe);
            return adopted.Success ? RemoveProfileCore(profileId, brokerExe) : adopted;
        });

    private static Result RemoveProfileCore(string profileId, string brokerExe)
    {
        if (!File.Exists(brokerExe)) return Result.Fail("WinForgeLauncher.exe could not be located.");
        try
        {
            var config = LoadConfig();
            var profile = config.Profiles.FirstOrDefault(p => IdEquals(p, profileId));
            if (profile is null) return Result.Fail("Profile not found.");
            if (config.Profiles.Count <= 1) return Result.Fail("At least one profile must remain.");
            if (profile.UsesDefaultGitConfig || ReferenceEquals(profile, config.Profiles[0]))
                return Result.Fail("The default profile cannot be removed. Rename it instead.");

            bool wasActive = IdEquals(profile, ReadActiveProfile());
            DeleteShortcutIfOwned(ShortcutPath(StartMenuDirectory, profile.Name), brokerExe);
            DeleteShortcutIfOwned(ShortcutPath(DesktopDirectory, profile.Name), brokerExe);
            config.Profiles.Remove(profile);

            var saved = SaveConfig(config);
            if (!saved.Success) return saved;
            if (wasActive) WriteActive(config.Profiles[0].Id, Guid.NewGuid().ToString("N"));
            if (HandlersOwned(brokerExe)) WriteHandlers(brokerExe);
            return Result.Ok();
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    private sealed record LaunchAttempt(Result Result, string ActiveToken = "", string BrokerExe = "");

    public static Result LaunchProfile(string profileId, string? protocolUrl = null, string? desktopExe = null, string? brokerExe = null) =>
        LaunchWithLock(() => LaunchProfileCore(profileId, protocolUrl, desktopExe, brokerExe));

    private static LaunchAttempt LaunchProfileCore(string profileId, string? protocolUrl, string? desktopExe, string? brokerExe)
    {
        desktopExe ??= FindGitHubDesktopExecutable();
        brokerExe ??= FindBrokerExecutable();
        if (desktopExe is null || !File.Exists(desktopExe)) return new(Result.Fail("GitHub Desktop is not installed."));
        if (brokerExe is null || !File.Exists(brokerExe)) return new(Result.Fail("WinForgeLauncher.exe could not be located."));
        if (protocolUrl is not null && !IsAllowedProtocolUrl(protocolUrl)) return new(Result.Fail("Unsupported GitHub Desktop callback URL."));

        try
        {
            var config = LoadConfig();
            var profile = config.Profiles.FirstOrDefault(p => IdEquals(p, profileId));
            if (profile is null) return new(Result.Fail("Profile not found."));
            EnsureProfileFiles(profile);

            string token = Guid.NewGuid().ToString("N");
            WriteActive(profile.Id, token);
            WriteHandlers(brokerExe);

            var psi = new ProcessStartInfo
            {
                FileName = desktopExe,
                WorkingDirectory = Path.GetDirectoryName(desktopExe) ?? "",
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("--user-data-dir=" + profile.DataPath);
            if (protocolUrl is not null)
            {
                psi.ArgumentList.Add("--protocol-launcher");
                psi.ArgumentList.Add(protocolUrl);
            }
            if (!profile.UsesDefaultGitConfig) psi.Environment["GIT_CONFIG_GLOBAL"] = profile.GitConfigPath;
            Process.Start(psi);
            return new(Result.Ok(), token, brokerExe);
        }
        catch (Exception ex) { return new(Result.Fail(ex.Message)); }
    }

    public static Result HandleProtocol(string protocolUrl, string? desktopExe = null, string? brokerExe = null)
    {
        if (!IsAllowedProtocolUrl(protocolUrl)) return Result.Fail("Unsupported GitHub Desktop callback URL.");
        return LaunchWithLock(() =>
        {
            var config = LoadConfig();
            var active = ReadActiveProfile();
            var profile = config.Profiles.FirstOrDefault(p => IdEquals(p, active)) ?? config.Profiles.FirstOrDefault();
            return profile is null
                ? new LaunchAttempt(Result.Fail("No GitHub Desktop profile is configured."))
                : LaunchProfileCore(profile.Id, protocolUrl, desktopExe, brokerExe);
        });
    }

    private static Result LaunchWithLock(Func<LaunchAttempt> operation)
    {
        LaunchAttempt? attempt = null;
        var result = WithConfigLock(() =>
        {
            attempt = operation();
            return attempt.Result;
        });
        if (!result.Success || attempt is null) return result;

        // GitHub Desktop may rewrite its protocol registrations during startup.
        // Reassert outside the config semaphore so a newer launch/activation can
        // immediately replace ActiveToken and stop this loop.
        try
        {
            for (int i = 0; i < 16; i++)
            {
                Thread.Sleep(500);
                if (!string.Equals(ReadActiveToken(), attempt.ActiveToken, StringComparison.Ordinal)) break;
                WriteHandlers(attempt.BrokerExe);
            }
            return result;
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    public static Result SetActive(string profileId, string brokerExe, bool reassert = true) =>
        WithConfigLock(() => SetActiveCore(profileId, brokerExe, reassert));

    private static Result SetActiveCore(string profileId, string brokerExe, bool reassert = true)
    {
        if (!File.Exists(brokerExe)) return Result.Fail("WinForgeLauncher.exe could not be located.");
        try
        {
            var config = LoadConfig();
            var profile = config.Profiles.FirstOrDefault(p => IdEquals(p, profileId));
            if (profile is null) return Result.Fail("Profile not found.");
            WriteActive(profile.Id, Guid.NewGuid().ToString("N"));
            WriteHandlers(brokerExe);
            if (reassert)
            {
                Thread.Sleep(100);
                WriteHandlers(brokerExe);
            }
            return Result.Ok();
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    public static Result Uninstall(string brokerExe) =>
        WithConfigLock(() => UninstallCore(brokerExe));

    private static Result UninstallCore(string brokerExe)
    {
        try
        {
            var config = LoadConfig();
            string managed = HandlerCommand(brokerExe);
            foreach (var protocol in Protocols)
            {
                if (!string.Equals(ReadHandler(protocol), managed, StringComparison.OrdinalIgnoreCase)) continue;
                if (config.OriginalHandlers.TryGetValue(protocol, out var original) && !string.IsNullOrWhiteSpace(original))
                    WriteHandler(protocol, original);
                else if (FindGitHubDesktopExecutable() is string desktop)
                    WriteHandler(protocol, $"\"{desktop}\" --protocol-launcher \"%1\"");
            }
            foreach (var profileName in config.Profiles.Select(p => p.Name)
                .Concat(config.PendingShortcutCleanup)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                DeleteShortcutIfOwned(ShortcutPath(StartMenuDirectory, profileName), brokerExe);
                DeleteShortcutIfOwned(ShortcutPath(DesktopDirectory, profileName), brokerExe);
            }
            ReconcileOfficialShortcut(createStartMenuShortcuts: false);
            config.ManagedByWinForge = false;
            config.PendingShortcutCleanup.Clear();
            var saved = SaveConfig(config);
            if (!saved.Success) return saved;
            try { Registry.CurrentUser.DeleteSubKeyTree(StateKeyPath, false); } catch { }
            return Result.Ok();
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    public static string? FindGitHubDesktopExecutable()
    {
        try
        {
            var configured = LoadConfig().DesktopExePath;
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                using var appPath = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\GitHubDesktop.exe");
                string? registered = (appPath?.GetValue("") as string)?.Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(registered) && File.Exists(registered)) return registered;
            }

            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GitHubDesktop");
            var candidates = Directory.Exists(root)
                ? Directory.EnumerateDirectories(root, "app-*").Select(d => Path.Combine(d, "GitHubDesktop.exe")).Where(File.Exists)
                    .OrderByDescending(ExeVersion).ThenByDescending(File.GetLastWriteTimeUtc).ToList()
                : [];
            if (candidates.Count > 0) return candidates[0];
            string stable = Path.Combine(root, "GitHubDesktop.exe");
            return File.Exists(stable) ? stable : null;
        }
        catch { return null; }
    }

    public static string? FindBrokerExecutable()
    {
        try
        {
            string? current = Environment.ProcessPath;
            if (current is not null && string.Equals(Path.GetFileName(current), "WinForgeLauncher.exe", StringComparison.OrdinalIgnoreCase)) return current;
            string beside = Path.Combine(AppContext.BaseDirectory, "WinForgeLauncher.exe");
            if (File.Exists(beside)) return beside;
            string installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WinForge", "WinForgeLauncher.exe");
            if (File.Exists(installed)) return installed;
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                string launcherBin = Path.Combine(dir.FullName, "launcher", "bin");
                if (!Directory.Exists(launcherBin)) continue;
                var hit = Directory.EnumerateFiles(launcherBin, "WinForgeLauncher.exe", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
                if (hit is not null) return hit;
            }
        }
        catch { }
        return null;
    }

    public static string ReadActiveProfile()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath);
            return key?.GetValue("ActiveProfile") as string ?? "";
        }
        catch { return ""; }
    }

    public static bool HandlersOwned(string brokerExe) => Protocols.All(p =>
        string.Equals(ReadHandler(p), HandlerCommand(brokerExe), StringComparison.OrdinalIgnoreCase));

    public static bool ShortcutOwned(string path, string brokerExe) => IsShortcutOwned(path, brokerExe);

    public static bool ShortcutMatchesProfile(
        string path, string brokerExe, string profileId, string profileName) =>
        IsShortcutForProfile(path, brokerExe, profileId, profileName);

    public static bool ShortcutMatchesBrokerProfile(
        string path, string brokerExe, string profileId) =>
        IsShortcutForBrokerProfile(path, brokerExe, profileId);

    public static bool OfficialShortcutStateReady(bool createStartMenuShortcuts)
    {
        if (createStartMenuShortcuts)
        {
            bool backupReady = !File.Exists(OfficialShortcutBackupPath)
                || IsStandardGitHubDesktopShortcut(OfficialShortcutBackupPath);
            return backupReady && !File.Exists(OfficialShortcutPath);
        }

        if (File.Exists(OfficialShortcutPath)) return true;
        return !File.Exists(OfficialShortcutBackupPath);
    }

    public static bool IsAllowedProtocolUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 16_384 || value.IndexOfAny(['"', '\r', '\n', '\0']) >= 0) return false;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && Protocols.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase);
    }

    private static List<Profile> DefaultProfiles()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] names = ["CodingMachineEdge", "CafePromenade", "INFTGroup7"];
        var list = new List<Profile>();
        for (int i = 0; i < names.Length; i++)
        {
            string data = DefaultDataPath(i, names[i]);
            list.Add(new Profile
            {
                Id = Slug(names[i]), Name = names[i], DataPath = data,
                GitConfigPath = i == 0 ? Path.Combine(home, ".gitconfig") : Path.Combine(data, "gitconfig"),
                UsesDefaultGitConfig = i == 0,
            });
        }
        return list;
    }

    private static string DefaultDataPath(int index, string name)
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (index == 0) return Path.Combine(roaming, "GitHub Desktop");
        string legacy = Path.Combine(roaming, $"GitHub Desktop Profile {index + 1}");
        return Directory.Exists(legacy) ? legacy : Path.Combine(roaming, "GitHub Desktop Profiles", name.Trim());
    }

    private static string NewProfileDataPath(string name, IReadOnlyList<Profile> existingProfiles)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitHub Desktop Profiles");
        var usedPaths = new HashSet<string>(
            existingProfiles.Select(p => Path.GetFullPath(p.DataPath)),
            StringComparer.OrdinalIgnoreCase);
        string candidate = Path.Combine(root, name);
        for (int suffix = 2; Directory.Exists(candidate) || usedPaths.Contains(Path.GetFullPath(candidate)); suffix++)
            candidate = Path.Combine(root, $"{name} ({suffix})");
        return candidate;
    }

    private static void EnsureProfileFiles(Profile profile)
    {
        Directory.CreateDirectory(profile.DataPath);
        if (profile.UsesDefaultGitConfig || File.Exists(profile.GitConfigPath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(profile.GitConfigPath)!);
        string source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gitconfig");
        if (File.Exists(source)) File.Copy(source, profile.GitConfigPath, false);
    }

    private static void WriteActive(string profileId, string token)
    {
        using var key = Registry.CurrentUser.CreateSubKey(StateKeyPath, true);
        key.SetValue("ActiveProfile", profileId, RegistryValueKind.String);
        key.SetValue("ActiveToken", token, RegistryValueKind.String);
    }

    private static string ReadActiveToken()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StateKeyPath);
            return key?.GetValue("ActiveToken") as string ?? "";
        }
        catch { return ""; }
    }

    private static void WriteHandlers(string brokerExe)
    {
        string command = HandlerCommand(brokerExe);
        foreach (var protocol in Protocols) WriteHandler(protocol, command);
    }

    private static string HandlerCommand(string brokerExe) => $"\"{brokerExe}\" --github-desktop-protocol \"%1\"";
    private static string? ReadHandler(string protocol)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{protocol}\shell\open\command");
            return key?.GetValue("") as string;
        }
        catch { return null; }
    }

    private static void WriteHandler(string protocol, string command)
    {
        using (var root = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}", true))
        {
            root.SetValue("", $"URL:{protocol} Protocol", RegistryValueKind.String);
            root.SetValue("URL Protocol", "", RegistryValueKind.String);
        }
        using var key = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{protocol}\shell\open\command", true);
        key.SetValue("", command, RegistryValueKind.String);
    }

    private static bool IdEquals(Profile profile, string? value) =>
        string.Equals(profile.Id, value, StringComparison.OrdinalIgnoreCase)
        || string.Equals(profile.Name, value, StringComparison.OrdinalIgnoreCase);

    private static Result WithConfigLock(Func<Result> operation)
    {
        bool entered;
        try { entered = ConfigSemaphore.WaitOne(TimeSpan.FromSeconds(12)); }
        catch { entered = false; }
        if (!entered) return Result.Fail("Another GitHub Desktop profile operation is still running.");
        try { return operation(); }
        finally
        {
            try { ConfigSemaphore.Release(); } catch { }
        }
    }

    private static Result OperationFailure(Exception exception, bool configSaved) =>
        Result.Fail(configSaved
            ? exception.Message + " Configuration was saved; run Repair routing to complete the operation."
            : exception.Message);

    private static string? ValidateName(string name)
    {
        string n = name.Trim();
        if (n.Length is < 1 or > 64) return "Profile names must contain 1 to 64 characters.";
        if (!string.Equals(n, name, StringComparison.Ordinal) || n.EndsWith('.') || n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "Profile names cannot contain invalid filename characters or leading/trailing spaces.";
        string stem = n.Split('.')[0];
        string[] reserved = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
        return reserved.Contains(stem, StringComparer.OrdinalIgnoreCase) ? "That profile name is reserved by Windows." : null;
    }

    private static string Slug(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        string slug = new(chars);
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        slug = slug.Trim('-');
        return slug.Length == 0 ? "profile" : slug;
    }

    private static string UniqueId(string seed, HashSet<string> used)
    {
        string id = seed;
        for (int i = 2; !used.Add(id); i++) id = seed + "-" + i;
        return id;
    }

    private static Version ExeVersion(string path)
    {
        try { return Version.TryParse(FileVersionInfo.GetVersionInfo(path).FileVersion, out var v) ? v : new Version(); }
        catch { return new Version(); }
    }

    private static string ShortcutPath(string folder, string name) => Path.Combine(folder, $"GitHub Desktop - {name}.lnk");
    private static string OfficialShortcutPath => Path.Combine(StartMenuDirectory, "GitHub Desktop.lnk");
    private static string OfficialShortcutBackupPath => Path.Combine(InstallRoot, "Original GitHub Desktop.lnk");

    private static void ReconcileProfileShortcuts(
        Profile profile,
        bool createStartMenuShortcuts,
        bool createDesktopShortcuts,
        string brokerExe,
        string desktopExe)
    {
        string arguments = $"--github-desktop-profile \"{profile.Id}\"";
        string workingDirectory = Path.GetDirectoryName(brokerExe) ?? "";
        string description = $"GitHub Desktop profile: {profile.Name}";
        ReconcileProfileShortcut(
            ShortcutPath(StartMenuDirectory, profile.Name),
            createStartMenuShortcuts,
            profile,
            brokerExe,
            desktopExe,
            arguments,
            workingDirectory,
            description);
        ReconcileProfileShortcut(
            ShortcutPath(DesktopDirectory, profile.Name),
            createDesktopShortcuts,
            profile,
            brokerExe,
            desktopExe,
            arguments,
            workingDirectory,
            description);
    }

    private static void ReconcileProfileShortcut(
        string path,
        bool create,
        Profile profile,
        string brokerExe,
        string desktopExe,
        string arguments,
        string workingDirectory,
        string description)
    {
        if (create)
        {
            CreateShortcut(path, brokerExe, arguments, workingDirectory, desktopExe, description);
            if (!IsShortcutForProfile(path, brokerExe, profile.Id, profile.Name))
                throw new IOException($"Shortcut verification failed: {Path.GetFileName(path)}");
        }
        else
        {
            DeleteShortcutIfOwned(path, brokerExe);
            if (IsShortcutOwned(path, brokerExe))
                throw new IOException($"Could not remove managed shortcut: {Path.GetFileName(path)}");
        }
    }

    private static void EnsureOfficialShortcutStateCanBeManaged(bool createStartMenuShortcuts)
    {
        if (File.Exists(OfficialShortcutBackupPath) &&
            (createStartMenuShortcuts || !File.Exists(OfficialShortcutPath)) &&
            !IsStandardGitHubDesktopShortcut(OfficialShortcutBackupPath))
        {
            throw new InvalidOperationException("The saved GitHub Desktop shortcut backup is not recognized.");
        }

        if (createStartMenuShortcuts && File.Exists(OfficialShortcutPath) &&
            !IsStandardGitHubDesktopShortcut(OfficialShortcutPath))
        {
            throw new InvalidOperationException(
                "GitHub Desktop.lnk already exists and is not the standard GitHub Desktop shortcut.");
        }
    }

    private static void ReconcileOfficialShortcut(bool createStartMenuShortcuts)
    {
        EnsureOfficialShortcutStateCanBeManaged(createStartMenuShortcuts);
        if (createStartMenuShortcuts)
        {
            if (!File.Exists(OfficialShortcutPath)) return;
            Directory.CreateDirectory(InstallRoot);
            if (!File.Exists(OfficialShortcutBackupPath))
                File.Copy(OfficialShortcutPath, OfficialShortcutBackupPath, overwrite: false);
            File.Delete(OfficialShortcutPath);
            if (File.Exists(OfficialShortcutPath))
                throw new IOException("Could not remove the standard GitHub Desktop shortcut.");
            return;
        }

        if (File.Exists(OfficialShortcutPath) || !File.Exists(OfficialShortcutBackupPath)) return;
        Directory.CreateDirectory(StartMenuDirectory);
        File.Copy(OfficialShortcutBackupPath, OfficialShortcutPath, overwrite: false);
        if (!IsStandardGitHubDesktopShortcut(OfficialShortcutPath))
            throw new IOException("The standard GitHub Desktop shortcut could not be restored safely.");
    }

    private static void CreateShortcut(string path, string target, string arguments, string workingDirectory, string icon, string description)
    {
        EnsureShortcutReplaceable(path, target);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)(object)new ShellLink();
            link.SetPath(target);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDirectory);
            link.SetDescription(description);
            link.SetIconLocation(icon, 0);
            ((IPersistFile)link).Save(path, true);
        }
        finally
        {
            if (link is not null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
        }
    }

    private static void EnsureShortcutReplaceable(string path, string brokerExe)
    {
        if (File.Exists(path) && !IsShortcutOwned(path, brokerExe))
            throw new InvalidOperationException(
                $"A shortcut named '{Path.GetFileName(path)}' already exists and is not managed by WinForge.");
    }

    private static void DeleteShortcutIfOwned(string path, string brokerExe)
    {
        if (IsShortcutOwned(path, brokerExe))
        {
            File.Delete(path);
            if (File.Exists(path))
                throw new IOException($"Could not remove managed shortcut: {Path.GetFileName(path)}");
        }
    }

    private static bool IsShortcutOwned(string path, string brokerExe)
    {
        _ = brokerExe;
        if (!TryReadShortcut(path, out string target, out string arguments)) return false;
        bool winForgeOwned = string.Equals(
                Path.GetFileName(target), "WinForgeLauncher.exe", StringComparison.OrdinalIgnoreCase)
            && TryGetSingleOptionValue(arguments, "--github-desktop-profile", out _);
        return winForgeOwned || IsReusableProfileShortcut(target, arguments, out _);
    }

    private static bool IsShortcutForProfile(
        string path, string brokerExe, string profileId, string profileName)
    {
        if (!TryReadShortcut(path, out string target, out string arguments)) return false;
        bool brokerMatch = PathsEqual(target, brokerExe)
            && TryGetSingleOptionValue(arguments, "--github-desktop-profile", out string actualId)
            && string.Equals(actualId, profileId, StringComparison.OrdinalIgnoreCase);
        bool reusableMatch = IsReusableProfileShortcut(target, arguments, out string actualName)
            && string.Equals(actualName, profileName, StringComparison.OrdinalIgnoreCase);
        return brokerMatch || reusableMatch;
    }

    private static bool IsShortcutForBrokerProfile(
        string path, string brokerExe, string profileId)
    {
        return TryReadShortcut(path, out string target, out string arguments)
            && PathsEqual(target, brokerExe)
            && TryGetSingleOptionValue(arguments, "--github-desktop-profile", out string actualId)
            && string.Equals(actualId, profileId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReusableProfileShortcut(
        string target, string arguments, out string profileName)
    {
        profileName = "";
        string launcher = Path.Combine(InstallRoot, "Launch-GitHubDesktopProfile.ps1");
        return string.Equals(Path.GetFileName(target), "powershell.exe", StringComparison.OrdinalIgnoreCase)
            && arguments.Contains($"-File \"{launcher}\"", StringComparison.OrdinalIgnoreCase)
            && TryGetTrailingOptionValue(arguments, "-ProfileName", out profileName);
    }

    private static bool IsStandardGitHubDesktopShortcut(string path)
    {
        if (!TryReadShortcut(path, out string target, out string arguments) ||
            !string.IsNullOrWhiteSpace(arguments) ||
            !string.Equals(Path.GetFileName(target), "GitHubDesktop.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            string root = Path.GetFullPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GitHubDesktop")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(target).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool TryReadShortcut(string path, out string target, out string arguments)
    {
        target = "";
        arguments = "";
        if (!File.Exists(path)) return false;
        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)(object)new ShellLink();
            ((IPersistFile)link).Load(path, 0);
            var targetBuffer = new StringBuilder(1024);
            var argumentBuffer = new StringBuilder(4096);
            link.GetPath(targetBuffer, targetBuffer.Capacity, IntPtr.Zero, 0);
            link.GetArguments(argumentBuffer, argumentBuffer.Capacity);
            target = targetBuffer.ToString();
            arguments = argumentBuffer.ToString();
            return !string.IsNullOrWhiteSpace(target);
        }
        catch { return false; }
        finally
        {
            if (link is not null && Marshal.IsComObject(link)) Marshal.FinalReleaseComObject(link);
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool TryGetSingleOptionValue(
        string arguments, string option, out string value)
    {
        value = "";
        string text = arguments.Trim();
        if (!text.StartsWith(option, StringComparison.OrdinalIgnoreCase) ||
            text.Length == option.Length ||
            !char.IsWhiteSpace(text[option.Length]))
        {
            return false;
        }

        string remainder = text[option.Length..].Trim();
        if (remainder.Length >= 2 && remainder[0] == '"' && remainder[^1] == '"')
        {
            remainder = remainder[1..^1];
            if (remainder.Contains('"')) return false;
        }
        else if (remainder.Any(char.IsWhiteSpace))
        {
            return false;
        }

        value = remainder;
        return value.Length > 0;
    }

    private static bool TryGetTrailingOptionValue(
        string arguments, string option, out string value)
    {
        value = "";
        int index = arguments.IndexOf(option, StringComparison.OrdinalIgnoreCase);
        if (index < 0 || (index > 0 && !char.IsWhiteSpace(arguments[index - 1]))) return false;
        return TryGetSingleOptionValue(arguments[index..], option, out value);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr findData, uint flags);
        void GetIDList(out IntPtr idList);
        void SetIDList(IntPtr idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int iconPathLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
}
