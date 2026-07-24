using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace WinForge.Services;

public enum ChromiumBrowser
{
    Chrome,
    Edge,
}

public enum BrowserFeatureMode
{
    Enable,
    Disable,
}

public enum BrowserPackageAction
{
    Install,
    Upgrade,
}

public sealed record BrowserProfile(
    ChromiumBrowser Browser,
    string DirectoryName,
    string DisplayName,
    string UserDataRoot,
    string FullPath)
{
    public string DisplayLabel => $"{DisplayName}  ({DirectoryName})";
}

public sealed record BrowserPwa(
    ChromiumBrowser Browser,
    string Name,
    string AppId,
    string ProfileDirectory,
    string TargetPath,
    string ShortcutPath)
{
    public string DisplayLabel => $"{Name}  ·  {ProfileDirectory}";
}

public sealed record BrowserLaunchPlan(
    ChromiumBrowser Browser,
    string FileName,
    IReadOnlyList<string> Arguments,
    string? EphemeralDirectory = null,
    string? EphemeralRoot = null);

public sealed record BrowserCacheClearReport(
    string ProfileDirectory,
    int DeletedDirectoryCount,
    IReadOnlyList<string> MissingDirectories);

public sealed record BrowserWingetPlan(
    ChromiumBrowser Browser,
    BrowserPackageAction Action,
    string PackageId,
    IReadOnlyList<string> Arguments);

public sealed record BrowserShortcutData(string TargetPath, string Arguments, string Description);

/// <summary>
/// Pure, bounded Browser Control contracts. User-provided values are validated here and leave this
/// layer only as discrete <see cref="ProcessStartInfo.ArgumentList"/> items. No method builds a shell
/// command line. · 純粹、有界嘅瀏覽器控制合約；用戶輸入只會以獨立參數離開呢層，唔會串成 shell 指令。
/// </summary>
public static class BrowserControlCore
{
    public const int MaxUrlLength = 2048;
    public const int MaxProxyLength = 256;
    public const int MaxBypassLength = 512;
    public const int MaxFeatureCount = 16;
    public const int MaxPwaResults = 500;
    public const int MinimumRemoteDebugPort = 1024;
    public const int MaximumRemoteDebugPort = 65535;

    private static readonly Regex FeatureNamePattern = new(
        "^[A-Za-z][A-Za-z0-9_.-]{0,127}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    private static readonly Regex PwaIdPattern = new(
        "^[A-Za-z0-9_-]{8,128}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static string BrowserName(ChromiumBrowser browser) => browser switch
    {
        ChromiumBrowser.Chrome => "Google Chrome",
        ChromiumBrowser.Edge => "Microsoft Edge",
        _ => throw new ArgumentOutOfRangeException(nameof(browser)),
    };

    public static string ProcessName(ChromiumBrowser browser) => browser switch
    {
        ChromiumBrowser.Chrome => "chrome",
        ChromiumBrowser.Edge => "msedge",
        _ => throw new ArgumentOutOfRangeException(nameof(browser)),
    };

    public static string PackageId(ChromiumBrowser browser) => browser switch
    {
        ChromiumBrowser.Chrome => "Google.Chrome",
        ChromiumBrowser.Edge => "Microsoft.Edge",
        _ => throw new ArgumentOutOfRangeException(nameof(browser)),
    };

    public static string UserDataRoot(ChromiumBrowser browser)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return browser switch
        {
            ChromiumBrowser.Chrome => Path.Combine(local, "Google", "Chrome", "User Data"),
            ChromiumBrowser.Edge => Path.Combine(local, "Microsoft", "Edge", "User Data"),
            _ => throw new ArgumentOutOfRangeException(nameof(browser)),
        };
    }

    public static string? ResolveExecutable(ChromiumBrowser browser)
    {
        var exe = browser == ChromiumBrowser.Chrome ? "chrome.exe" : "msedge.exe";
        foreach (var candidate in ExecutableCandidates(browser))
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch { }
        }

        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var root = RegistryKey.OpenBaseKey(hive, view);
                    using var key = root.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{exe}");
                    if (key?.GetValue(null) is string path && File.Exists(path))
                        return Path.GetFullPath(path);
                }
                catch { }
            }
        }

        var fromPath = FindOnPath(exe);
        return fromPath is null ? null : Path.GetFullPath(fromPath);
    }

    public static string NormalizeHttpUrl(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) throw new ArgumentException("Enter a website URL.", nameof(value));
        if (text.Length > MaxUrlLength) throw new ArgumentException($"URL must be {MaxUrlLength} characters or fewer.", nameof(value));
        if (text.Any(char.IsControl)) throw new ArgumentException("URL cannot contain control characters.", nameof(value));
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Only an absolute HTTP or HTTPS URL is allowed.", nameof(value));

        return uri.AbsoluteUri;
    }

    public static IReadOnlyList<BrowserProfile> DiscoverProfiles(ChromiumBrowser browser, string? userDataRoot = null)
    {
        var root = Path.GetFullPath(userDataRoot ?? UserDataRoot(browser));
        if (!Directory.Exists(root)) return Array.Empty<BrowserProfile>();

        var names = ReadProfileNames(Path.Combine(root, "Local State"));
        var results = new List<BrowserProfile>();
        IEnumerable<string> directories;
        try { directories = Directory.EnumerateDirectories(root).ToArray(); }
        catch { return Array.Empty<BrowserProfile>(); }

        foreach (var path in directories.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var directory = Path.GetFileName(path);
            if (!IsSafeProfileDirectoryName(directory)) continue;
            if (!names.ContainsKey(directory)
                && !directory.Equals("Default", StringComparison.OrdinalIgnoreCase)
                && !directory.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                continue;

            var display = names.TryGetValue(directory, out var mapped) && !string.IsNullOrWhiteSpace(mapped)
                ? mapped.Trim()
                : directory;
            results.Add(new BrowserProfile(browser, directory, display, root, Path.GetFullPath(path)));
        }

        return results;
    }

    public static IReadOnlyList<BrowserPwa> DiscoverPwas(
        IEnumerable<string>? startMenuRoots = null,
        Func<string, BrowserShortcutData?>? shortcutReader = null)
    {
        shortcutReader ??= TryReadShortcut;
        var roots = startMenuRoots?.ToArray() ?? new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
        };

        var results = new List<BrowserPwa>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r) && Directory.Exists(r)))
        {
            IEnumerable<string> links;
            try { links = Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories).Take(MaxPwaResults * 4).ToArray(); }
            catch { continue; }

            foreach (var link in links)
            {
                BrowserShortcutData? shortcut;
                try { shortcut = shortcutReader(link); }
                catch { continue; }
                if (shortcut is null) continue;
                if (!TryParsePwaShortcut(shortcut, link, out var pwa) || pwa is null) continue;
                var key = $"{pwa.Browser}|{pwa.ProfileDirectory}|{pwa.AppId}";
                if (!seen.Add(key)) continue;
                results.Add(pwa);
                if (results.Count >= MaxPwaResults) return results.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
        }

        return results.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public static bool TryParsePwaShortcut(
        BrowserShortcutData shortcut,
        string shortcutPath,
        out BrowserPwa? pwa)
    {
        pwa = null;
        if (string.IsNullOrWhiteSpace(shortcut.TargetPath) || string.IsNullOrWhiteSpace(shortcut.Arguments)) return false;
        var file = Path.GetFileName(shortcut.TargetPath).ToLowerInvariant();
        ChromiumBrowser browser;
        if (file is "chrome.exe" or "chrome_proxy.exe") browser = ChromiumBrowser.Chrome;
        else if (file is "msedge.exe" or "msedge_proxy.exe") browser = ChromiumBrowser.Edge;
        else return false;

        var args = TokenizeArguments(shortcut.Arguments);
        var appId = OptionValue(args, "--app-id");
        var profile = OptionValue(args, "--profile-directory") ?? "Default";
        if (appId is null || !PwaIdPattern.IsMatch(appId) || !IsSafeProfileDirectoryName(profile)) return false;

        var name = Path.GetFileNameWithoutExtension(shortcutPath).Trim();
        if (name.Length == 0) name = string.IsNullOrWhiteSpace(shortcut.Description) ? appId : shortcut.Description.Trim();
        pwa = new BrowserPwa(browser, name, appId, profile, Path.GetFullPath(shortcut.TargetPath), Path.GetFullPath(shortcutPath));
        return true;
    }

    public static BrowserLaunchPlan BuildAppModePlan(ChromiumBrowser browser, string executable, string url)
        => Plan(browser, executable, new[] { "--app=" + NormalizeHttpUrl(url) });

    public static BrowserLaunchPlan BuildKioskPlan(ChromiumBrowser browser, string executable, string url)
    {
        var normalized = NormalizeHttpUrl(url);
        return browser == ChromiumBrowser.Edge
            ? Plan(browser, executable, new[] { "--kiosk", normalized, "--edge-kiosk-type=fullscreen", "--kiosk-idle-timeout-minutes=0" })
            : Plan(browser, executable, new[] { "--kiosk", normalized });
    }

    public static BrowserLaunchPlan BuildProfilePlan(BrowserProfile profile, string executable)
    {
        ValidateProfile(profile);
        return Plan(profile.Browser, executable, new[] { "--profile-directory=" + profile.DirectoryName });
    }

    public static BrowserLaunchPlan BuildPwaPlan(BrowserPwa pwa, string? resolvedExecutable = null)
    {
        if (!PwaIdPattern.IsMatch(pwa.AppId)) throw new ArgumentException("PWA app ID is invalid.", nameof(pwa));
        if (!IsSafeProfileDirectoryName(pwa.ProfileDirectory)) throw new ArgumentException("PWA profile directory is invalid.", nameof(pwa));
        var file = Path.GetFileName(pwa.TargetPath).ToLowerInvariant();
        if (file is not ("chrome.exe" or "chrome_proxy.exe" or "msedge.exe" or "msedge_proxy.exe"))
            throw new ArgumentException("PWA shortcut target is not a supported Chromium browser.", nameof(pwa));
        return Plan(pwa.Browser, resolvedExecutable ?? pwa.TargetPath, new[]
        {
            "--profile-directory=" + pwa.ProfileDirectory,
            "--app-id=" + pwa.AppId,
        });
    }

    public static BrowserLaunchPlan BuildInternalPagePlan(ChromiumBrowser browser, string executable, bool policyPage)
    {
        var prefix = browser == ChromiumBrowser.Chrome ? "chrome" : "edge";
        return Plan(browser, executable, new[] { $"{prefix}://{(policyPage ? "policy" : "flags")}" });
    }

    public static BrowserLaunchPlan BuildProxyPlan(
        ChromiumBrowser browser,
        string executable,
        string proxy,
        string? bypass,
        string url,
        string ephemeralDirectory,
        string ephemeralRoot)
    {
        var server = NormalizeProxy(proxy);
        var bypassValue = NormalizeBypassList(bypass);
        var args = BaseIsolatedArguments(ephemeralDirectory).ToList();
        args.Add("--proxy-server=" + server);
        if (bypassValue.Length > 0) args.Add("--proxy-bypass-list=" + bypassValue);
        args.Add(NormalizeHttpUrl(url));
        return Plan(browser, executable, args, ephemeralDirectory, ephemeralRoot);
    }

    public static BrowserLaunchPlan BuildThrowawayPlan(
        ChromiumBrowser browser,
        string executable,
        string url,
        string ephemeralDirectory,
        string ephemeralRoot)
    {
        var args = BaseIsolatedArguments(ephemeralDirectory).ToList();
        args.Add(NormalizeHttpUrl(url));
        return Plan(browser, executable, args, ephemeralDirectory, ephemeralRoot);
    }

    public static BrowserLaunchPlan BuildFeaturePlan(
        ChromiumBrowser browser,
        string executable,
        string featureNames,
        BrowserFeatureMode mode,
        string url,
        string ephemeralDirectory,
        string ephemeralRoot)
    {
        var features = NormalizeFeatureNames(featureNames);
        var switchName = mode == BrowserFeatureMode.Enable ? "--enable-features=" : "--disable-features=";
        var args = BaseIsolatedArguments(ephemeralDirectory).ToList();
        args.Add(switchName + string.Join(',', features));
        args.Add(NormalizeHttpUrl(url));
        return Plan(browser, executable, args, ephemeralDirectory, ephemeralRoot);
    }

    public static BrowserLaunchPlan BuildRemoteDebugPlan(
        ChromiumBrowser browser,
        string executable,
        int port,
        string url,
        string ephemeralDirectory,
        string ephemeralRoot)
    {
        if (port is < MinimumRemoteDebugPort or > MaximumRemoteDebugPort)
            throw new ArgumentOutOfRangeException(nameof(port), $"Remote debugging port must be {MinimumRemoteDebugPort}–{MaximumRemoteDebugPort}.");
        var args = BaseIsolatedArguments(ephemeralDirectory).ToList();
        args.Add("--remote-debugging-address=127.0.0.1");
        args.Add("--remote-debugging-port=" + port);
        args.Add(NormalizeHttpUrl(url));
        return Plan(browser, executable, args, ephemeralDirectory, ephemeralRoot);
    }

    public static BrowserWingetPlan BuildWingetPlan(ChromiumBrowser browser, BrowserPackageAction action)
    {
        var package = PackageId(browser);
        var verb = action == BrowserPackageAction.Install ? "install" : "upgrade";
        return new BrowserWingetPlan(browser, action, package, new[]
        {
            verb,
            "--id", package,
            "--exact",
            "--silent",
            "--accept-source-agreements",
            "--accept-package-agreements",
            "--disable-interactivity",
        });
    }

    public static string CreateEphemeralDirectory(
        ChromiumBrowser browser,
        string purpose,
        string? temporaryRoot,
        out string sessionRoot)
    {
        if (string.IsNullOrWhiteSpace(purpose) || purpose.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Session purpose must be an ASCII word.", nameof(purpose));
        sessionRoot = Path.GetFullPath(Path.Combine(temporaryRoot ?? Path.GetTempPath(), "WinForge", "BrowserSessions"));
        Directory.CreateDirectory(sessionRoot);
        RejectReparsePoint(new DirectoryInfo(sessionRoot), "Browser session root");
        if (Directory.GetParent(sessionRoot) is { } ownerRoot)
            RejectReparsePoint(ownerRoot, "WinForge temporary root");
        var browserName = browser == ChromiumBrowser.Chrome ? "chrome" : "edge";
        var path = Path.Combine(sessionRoot, $"{browserName}-{purpose}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    public static bool TryDeleteEphemeralDirectory(string path, string sessionRoot)
    {
        var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(sessionRoot));
        var fullPath = Path.GetFullPath(path);
        if (!EnsureTrailingSeparator(fullPath).StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            || string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            if (!Directory.Exists(fullPath)) return true;
            if (Directory.Exists(sessionRoot)) RejectReparsePoint(new DirectoryInfo(sessionRoot), "Browser session root");
            var info = new DirectoryInfo(fullPath);
            if (ContainsReparsePoint(info)) return false;
            Directory.Delete(fullPath, recursive: true);
            return !Directory.Exists(fullPath);
        }
        catch { return false; }
    }

    public static BrowserCacheClearReport ClearProfileCaches(
        BrowserProfile profile,
        Func<ChromiumBrowser, bool>? isBrowserRunning = null)
    {
        ValidateProfile(profile);
        isBrowserRunning ??= BrowserProcessIsRunning;
        if (isBrowserRunning(profile.Browser))
            throw new InvalidOperationException($"Close every {BrowserName(profile.Browser)} window and background process before clearing this profile cache.");

        var deleted = 0;
        var missing = new List<string>();
        foreach (var relative in new[] { "Cache", "Code Cache" })
        {
            var candidate = Path.GetFullPath(Path.Combine(profile.FullPath, relative));
            if (!EnsureTrailingSeparator(candidate).StartsWith(EnsureTrailingSeparator(profile.FullPath), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cache directory escaped the selected profile.");
            if (!Directory.Exists(candidate))
            {
                missing.Add(relative);
                continue;
            }

            var info = new DirectoryInfo(candidate);
            if (ContainsReparsePoint(info))
                throw new IOException($"Refusing to follow a reparse point at {relative}.");
            Directory.Delete(candidate, recursive: true);
            deleted++;
        }

        return new BrowserCacheClearReport(profile.DirectoryName, deleted, missing);
    }

    public static IReadOnlyList<string> NormalizeFeatureNames(string? value)
    {
        var values = (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (values.Length == 0) throw new ArgumentException("Enter at least one Chromium feature name.", nameof(value));
        if (values.Length > MaxFeatureCount) throw new ArgumentException($"Use no more than {MaxFeatureCount} feature names.", nameof(value));
        if (values.Any(v => !FeatureNamePattern.IsMatch(v)))
            throw new ArgumentException("Feature names must start with a letter and contain only letters, numbers, dot, underscore, or hyphen.", nameof(value));
        return values;
    }

    public static string NormalizeProxy(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) throw new ArgumentException("Enter a proxy server.", nameof(value));
        if (text.Length > MaxProxyLength || text.Any(char.IsControl) || text.Any(char.IsWhiteSpace) || text.Contains('"'))
            throw new ArgumentException("Proxy server contains unsupported characters or is too long.", nameof(value));
        var candidate = text.Contains("://", StringComparison.Ordinal) ? text : "http://" + text;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https" or "socks4" or "socks5")
            || string.IsNullOrWhiteSpace(uri.Host)
            || !string.IsNullOrEmpty(uri.UserInfo)
            || uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || uri.Port is <= 0 or > 65535)
            throw new ArgumentException("Use host:port or an http://, https://, socks4://, or socks5:// proxy URL.", nameof(value));
        return text;
    }

    public static string NormalizeBypassList(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0) return string.Empty;
        if (text.Length > MaxBypassLength || text.Any(char.IsControl) || text.Contains('"'))
            throw new ArgumentException("Proxy bypass list contains unsupported characters or is too long.", nameof(value));
        foreach (var entry in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (entry.Length > 128 || entry.Any(char.IsWhiteSpace))
                throw new ArgumentException("Each proxy bypass entry must be a compact host pattern.", nameof(value));
        }
        return string.Join(';', text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static bool BrowserProcessIsRunning(ChromiumBrowser browser)
    {
        Process[] processes;
        try { processes = Process.GetProcessesByName(ProcessName(browser)); }
        catch { return true; }
        try { return processes.Length > 0; }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static IEnumerable<string> ExecutableCandidates(ChromiumBrowser browser)
    {
        var relative = browser == ChromiumBrowser.Chrome
            ? Path.Combine("Google", "Chrome", "Application", "chrome.exe")
            : Path.Combine("Microsoft", "Edge", "Application", "msedge.exe");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), relative);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), relative);
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), relative);
    }

    private static string? FindOnPath(string executable)
    {
        foreach (var raw in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            try
            {
                var directory = raw.Trim().Trim('"');
                if (directory.Length == 0) continue;
                var candidate = Path.Combine(directory, executable);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    private static Dictionary<string, string> ReadProfileNames(string localStatePath)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var stream = new FileStream(localStatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32 });
            if (!doc.RootElement.TryGetProperty("profile", out var profile)
                || !profile.TryGetProperty("info_cache", out var cache)
                || cache.ValueKind != JsonValueKind.Object)
                return result;
            foreach (var entry in cache.EnumerateObject())
            {
                if (!IsSafeProfileDirectoryName(entry.Name) || entry.Value.ValueKind != JsonValueKind.Object) continue;
                string? name = null;
                if (entry.Value.TryGetProperty("name", out var nameNode) && nameNode.ValueKind == JsonValueKind.String)
                    name = nameNode.GetString();
                if (string.IsNullOrWhiteSpace(name)
                    && entry.Value.TryGetProperty("shortcut_name", out var shortcutNode)
                    && shortcutNode.ValueKind == JsonValueKind.String)
                    name = shortcutNode.GetString();
                result[entry.Name] = name ?? entry.Name;
            }
        }
        catch { }
        return result;
    }

    private static bool IsSafeProfileDirectoryName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 80 || value is "." or "..") return false;
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar)) return false;
        return !value.Any(char.IsControl);
    }

    private static void ValidateProfile(BrowserProfile profile)
    {
        if (!IsSafeProfileDirectoryName(profile.DirectoryName)) throw new ArgumentException("Profile directory name is invalid.", nameof(profile));
        var root = EnsureTrailingSeparator(Path.GetFullPath(profile.UserDataRoot));
        var full = EnsureTrailingSeparator(Path.GetFullPath(profile.FullPath));
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Selected profile is outside the browser user-data root.");
        var expected = Path.GetFullPath(Path.Combine(profile.UserDataRoot, profile.DirectoryName));
        if (!string.Equals(expected.TrimEnd(Path.DirectorySeparatorChar), profile.FullPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Selected profile path does not match its directory name.");
        var profileDirectory = new DirectoryInfo(profile.FullPath);
        if (!profileDirectory.Exists) throw new DirectoryNotFoundException("Selected browser profile no longer exists.");
        RejectReparsePoint(profileDirectory, "Selected browser profile");
    }

    private static BrowserLaunchPlan Plan(
        ChromiumBrowser browser,
        string executable,
        IReadOnlyList<string> arguments,
        string? ephemeralDirectory = null,
        string? ephemeralRoot = null)
    {
        if (string.IsNullOrWhiteSpace(executable)) throw new FileNotFoundException($"{BrowserName(browser)} is not installed or could not be resolved.");
        if (arguments.Count == 0 || arguments.Any(a => a is null || a.Any(char.IsControl)))
            throw new ArgumentException("Browser argument list is invalid.", nameof(arguments));
        if ((ephemeralDirectory is null) != (ephemeralRoot is null))
            throw new ArgumentException("Ephemeral directory and root must be supplied together.");
        if (ephemeralDirectory is not null && ephemeralRoot is not null)
        {
            var fullRoot = EnsureTrailingSeparator(Path.GetFullPath(ephemeralRoot));
            var fullDirectory = EnsureTrailingSeparator(Path.GetFullPath(ephemeralDirectory));
            if (!fullDirectory.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fullDirectory, fullRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Isolated browser directory is outside WinForge's owned session root.");
            RejectReparsePoint(new DirectoryInfo(ephemeralRoot), "Browser session root");
            RejectReparsePoint(new DirectoryInfo(ephemeralDirectory), "Isolated browser directory");
        }
        return new BrowserLaunchPlan(browser, executable, arguments.ToArray(), ephemeralDirectory, ephemeralRoot);
    }

    private static IReadOnlyList<string> BaseIsolatedArguments(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException("The isolated browser directory does not exist.");
        return new[]
        {
            "--user-data-dir=" + Path.GetFullPath(directory),
            "--no-first-run",
            "--disable-sync",
        };
    }

    private static IReadOnlyList<string> TokenizeArguments(string commandLine)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < commandLine.Length; i++)
        {
            var c = commandLine[i];
            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (char.IsWhiteSpace(c) && !quoted)
            {
                if (current.Length > 0) { result.Add(current.ToString()); current.Clear(); }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    private static string? OptionValue(IReadOnlyList<string> arguments, string option)
    {
        for (var i = 0; i < arguments.Count; i++)
        {
            if (arguments[i].StartsWith(option + "=", StringComparison.OrdinalIgnoreCase))
                return arguments[i][(option.Length + 1)..].Trim();
            if (arguments[i].Equals(option, StringComparison.OrdinalIgnoreCase) && i + 1 < arguments.Count)
                return arguments[i + 1].Trim();
        }
        return null;
    }

    private static BrowserShortcutData? TryReadShortcut(string shortcutPath)
    {
        IShellLinkW? link = null;
        try
        {
            link = (IShellLinkW)(object)new ShellLink();
            ((IPersistFile)link).Load(shortcutPath, 0);
            var target = new StringBuilder(32768);
            var arguments = new StringBuilder(32768);
            var description = new StringBuilder(1024);
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            link.GetArguments(arguments, arguments.Capacity);
            link.GetDescription(description, description.Capacity);
            if (target.Length == 0) return null;
            return new BrowserShortcutData(target.ToString(), arguments.ToString(), description.ToString());
        }
        catch { return null; }
        finally
        {
            if (link is not null && Marshal.IsComObject(link))
            {
                try { Marshal.FinalReleaseComObject(link); } catch { }
            }
        }
    }

    private static string EnsureTrailingSeparator(string path)
        => path.EndsWith(Path.DirectorySeparatorChar) ? path : path + Path.DirectorySeparatorChar;

    private static bool ContainsReparsePoint(DirectoryInfo directory)
    {
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0) return true;
        foreach (var child in directory.EnumerateFileSystemInfos())
        {
            if ((child.Attributes & FileAttributes.ReparsePoint) != 0) return true;
            if (child is DirectoryInfo childDirectory && ContainsReparsePoint(childDirectory)) return true;
        }
        return false;
    }

    private static void RejectReparsePoint(DirectoryInfo directory, string label)
    {
        if (!directory.Exists) throw new DirectoryNotFoundException($"{label} does not exist.");
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"{label} cannot be a reparse point.");
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private sealed class ShellLink { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}
