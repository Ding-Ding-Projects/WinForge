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
    public const int CurrentSchema = 2;
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
        public Dictionary<string, string?> OriginalHandlers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool CreateDesktopShortcuts { get; set; } = true;
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

    public static Result Configure(IReadOnlyList<string> names, bool createDesktopShortcuts, string brokerExe, string desktopExe) =>
        WithConfigLock(() => ConfigureCore(names, createDesktopShortcuts, brokerExe, desktopExe));

    public static Result AddProfile(string name, bool createDesktopShortcuts, string brokerExe, string desktopExe) =>
        WithConfigLock(() =>
        {
            var names = LoadConfig().Profiles.Select(p => p.Name).ToArray();
            var adopted = ConfigureCore(names, createDesktopShortcuts, brokerExe, desktopExe);
            return adopted.Success
                ? AddProfileCore(name, createDesktopShortcuts, brokerExe, desktopExe)
                : adopted;
        });

    private static Result AddProfileCore(string name, bool createDesktopShortcuts, string brokerExe, string desktopExe)
    {
        var error = ValidateName(name);
        if (error is not null) return Result.Fail(error);
        if (!File.Exists(brokerExe)) return Result.Fail("WinForgeLauncher.exe could not be located.");
        if (!File.Exists(desktopExe)) return Result.Fail("GitHub Desktop is not installed.");

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
            EnsureProfileFiles(profile);
            config.Profiles.Add(profile);
            config.CreateDesktopShortcuts = createDesktopShortcuts;
            config.DesktopExePath = desktopExe;

            var saved = SaveConfig(config);
            if (!saved.Success) return saved;

            CreateShortcut(ShortcutPath(StartMenuDirectory, profile.Name), brokerExe,
                $"--github-desktop-profile \"{profile.Id}\"", Path.GetDirectoryName(brokerExe) ?? "", desktopExe,
                $"GitHub Desktop profile: {profile.Name}");
            if (createDesktopShortcuts)
                CreateShortcut(ShortcutPath(DesktopDirectory, profile.Name), brokerExe,
                    $"--github-desktop-profile \"{profile.Id}\"", Path.GetDirectoryName(brokerExe) ?? "", desktopExe,
                    $"GitHub Desktop profile: {profile.Name}");
            return Result.Ok();
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    private static Result ConfigureCore(IReadOnlyList<string> names, bool createDesktopShortcuts, string brokerExe, string desktopExe)
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

        try
        {
            var config = LoadConfig();
            string activeBefore = ReadActiveProfile();
            var previousNames = config.Profiles.Select(p => p.Name).ToArray();
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
            config.CreateDesktopShortcuts = createDesktopShortcuts;
            config.DesktopExePath = desktopExe;

            Directory.CreateDirectory(StartMenuDirectory);
            foreach (var oldName in previousNames)
            {
                DeleteShortcutIfOwned(ShortcutPath(StartMenuDirectory, oldName), brokerExe);
                DeleteShortcutIfOwned(ShortcutPath(DesktopDirectory, oldName), brokerExe);
            }
            foreach (var profile in config.Profiles)
            {
                CreateShortcut(ShortcutPath(StartMenuDirectory, profile.Name), brokerExe,
                    $"--github-desktop-profile \"{profile.Id}\"", Path.GetDirectoryName(brokerExe) ?? "", desktopExe,
                    $"GitHub Desktop profile: {profile.Name}");
                if (createDesktopShortcuts)
                    CreateShortcut(ShortcutPath(DesktopDirectory, profile.Name), brokerExe,
                        $"--github-desktop-profile \"{profile.Id}\"", Path.GetDirectoryName(brokerExe) ?? "", desktopExe,
                        $"GitHub Desktop profile: {profile.Name}");
                else
                    DeleteShortcutIfOwned(ShortcutPath(DesktopDirectory, profile.Name), brokerExe);
            }

            var save = SaveConfig(config);
            if (!save.Success) return save;
            var activeProfile = config.Profiles.FirstOrDefault(p => IdEquals(p, activeBefore)) ?? config.Profiles[0];
            return SetActiveCore(activeProfile.Id, brokerExe, reassert: false);
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    public static Result Rename(string profileId, string newName, bool createDesktopShortcuts, string brokerExe, string desktopExe) =>
        WithConfigLock(() => RenameCore(profileId, newName, createDesktopShortcuts, brokerExe, desktopExe));

    private static Result RenameCore(string profileId, string newName, bool createDesktopShortcuts, string brokerExe, string desktopExe)
    {
        var error = ValidateName(newName);
        if (error is not null) return Result.Fail(error);
        if (!File.Exists(brokerExe)) return Result.Fail("WinForgeLauncher.exe could not be located.");
        if (!File.Exists(desktopExe)) return Result.Fail("GitHub Desktop is not installed.");
        try
        {
            var config = LoadConfig();
            if (config.Profiles.Any(p => !IdEquals(p, profileId) && string.Equals(p.Name, newName.Trim(), StringComparison.OrdinalIgnoreCase)))
                return Result.Fail("Profile names must be unique.");
            var profile = config.Profiles.FirstOrDefault(p => IdEquals(p, profileId));
            if (profile is null) return Result.Fail("Profile not found.");
            DeleteShortcutIfOwned(ShortcutPath(StartMenuDirectory, profile.Name), brokerExe);
            DeleteShortcutIfOwned(ShortcutPath(DesktopDirectory, profile.Name), brokerExe);
            profile.Name = newName.Trim();
            config.CreateDesktopShortcuts = createDesktopShortcuts;
            config.DesktopExePath = desktopExe;
            var saved = SaveConfig(config);
            if (!saved.Success) return saved;
            CreateShortcut(ShortcutPath(StartMenuDirectory, profile.Name), brokerExe,
                $"--github-desktop-profile \"{profile.Id}\"", Path.GetDirectoryName(brokerExe) ?? "", desktopExe,
                $"GitHub Desktop profile: {profile.Name}");
            if (createDesktopShortcuts)
                CreateShortcut(ShortcutPath(DesktopDirectory, profile.Name), brokerExe,
                    $"--github-desktop-profile \"{profile.Id}\"", Path.GetDirectoryName(brokerExe) ?? "", desktopExe,
                    $"GitHub Desktop profile: {profile.Name}");
            return Result.Ok();
        }
        catch (Exception ex) { return Result.Fail(ex.Message); }
    }

    public static Result RemoveProfile(string profileId, string brokerExe) =>
        WithConfigLock(() => RemoveProfileCore(profileId, brokerExe));

    public static Result AdoptAndRemoveProfile(
        string profileId, bool createDesktopShortcuts, string brokerExe, string desktopExe) =>
        WithConfigLock(() =>
        {
            var names = LoadConfig().Profiles.Select(p => p.Name).ToArray();
            var adopted = ConfigureCore(names, createDesktopShortcuts, brokerExe, desktopExe);
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
            foreach (var profile in config.Profiles)
            {
                DeleteShortcutIfOwned(ShortcutPath(StartMenuDirectory, profile.Name), brokerExe);
                DeleteShortcutIfOwned(ShortcutPath(DesktopDirectory, profile.Name), brokerExe);
            }
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

    private static void CreateShortcut(string path, string target, string arguments, string workingDirectory, string icon, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var link = (IShellLinkW)(object)new ShellLink();
        link.SetPath(target);
        link.SetArguments(arguments);
        link.SetWorkingDirectory(workingDirectory);
        link.SetDescription(description);
        link.SetIconLocation(icon, 0);
        ((IPersistFile)link).Save(path, true);
        Marshal.FinalReleaseComObject(link);
    }

    private static void DeleteShortcutIfOwned(string path, string brokerExe)
    {
        if (IsShortcutOwned(path, brokerExe))
        {
            try { File.Delete(path); } catch { }
        }
    }

    private static bool IsShortcutOwned(string path, string brokerExe)
    {
        if (!File.Exists(path)) return false;
        try
        {
            var link = (IShellLinkW)(object)new ShellLink();
            ((IPersistFile)link).Load(path, 0);
            var target = new StringBuilder(1024);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            Marshal.FinalReleaseComObject(link);
            return string.Equals(Path.GetFullPath(target.ToString()), Path.GetFullPath(brokerExe), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
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
