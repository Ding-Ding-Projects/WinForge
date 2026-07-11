using System.Diagnostics;
using System.ComponentModel;

namespace DewEncryption.Core;

public sealed class DewCliService
{
    public string CliFileName { get; }
    public string HistoryGuiFileName { get; }

    public DewCliService(string cliFileName = "dew-encryption", string historyGuiFileName = "dew-encryption-gui")
    {
        CliFileName = cliFileName;
        HistoryGuiFileName = historyGuiFileName;
    }

    public Task<DewCommandResult> RunHelpAsync(CancellationToken cancellationToken = default)
    {
        return RunCliAsync(["--help"], cancellationToken);
    }

    public Task<DewCommandResult> RunSelectedAsync(IEnumerable<string> paths, CancellationToken cancellationToken = default)
    {
        return RunCliAsync(paths, cancellationToken);
    }

    public Task<DewCommandResult> SnapshotHistoryAsync(string path, CancellationToken cancellationToken = default)
    {
        return RunCliAsync([path], cancellationToken);
    }

    public Task<DewCommandResult> RefreshHistoryAsync(string path, CancellationToken cancellationToken = default)
    {
        return RunCliAsync(["history", DewPathService.RepoForSource(path)], cancellationToken);
    }

    public Task<DewCommandResult> OpenHistoryGuiAsync(string path, CancellationToken cancellationToken = default)
    {
        return RunToolWithFallbackAsync(HistoryGuiFileName, ["-m", "dew_encryption.gui"], [path, "--history"], cancellationToken);
    }

    public Task<DewCommandResult> SnapshotContainerHistoryAsync(string containerPath, CancellationToken cancellationToken = default)
    {
        return RunCliAsync(["container-history", containerPath, "--snapshot"], cancellationToken);
    }

    public Task<DewCommandResult> TestContainerHooksAsync(string eventName, string containerPath, string? mountPath = null, CancellationToken cancellationToken = default)
    {
        List<string> args = ["container-hooks", eventName, containerPath];
        if (!string.IsNullOrWhiteSpace(mountPath))
        {
            args.Add("--mount-path");
            args.Add(mountPath);
        }

        return RunCliAsync(args, cancellationToken);
    }

    public Task<DewCommandResult> ViewContainerHistoryAsync(string containerPath, CancellationToken cancellationToken = default)
    {
        return RunCliAsync(["container-history", containerPath], cancellationToken);
    }

    public Task<DewCommandResult> SyncDewDriveFolderAsync(string folder, string? registry, bool push, CancellationToken cancellationToken = default)
    {
        List<string> args = ["dew-drive", "sync", "--folder", folder];
        if (!string.IsNullOrWhiteSpace(registry))
        {
            args.Add("--registry");
            args.Add(registry);
        }

        if (push)
        {
            args.Add("--push");
        }

        return RunCliAsync(args, cancellationToken);
    }

    public Task<DewCommandResult> SyncDewDriveProfileAsync(string profileName, string? registry, bool push, string? password, CancellationToken cancellationToken = default)
    {
        List<string> args = ["dew-drive", "sync", profileName];
        if (!string.IsNullOrWhiteSpace(registry))
        {
            args.Add("--registry");
            args.Add(registry);
        }

        if (push)
        {
            args.Add("--push");
        }

        return RunCliAsync(args, cancellationToken, PasswordEnvironment(password));
    }

    public Task<DewCommandResult> PullDewDriveAsync(string registryRef, string outputFolder, string? password = null, bool force = false, CancellationToken cancellationToken = default)
    {
        List<string> args = ["dew-drive", "pull", registryRef, "--output", outputFolder, "--password-env", "DEW_DRIVE_PASSWORD"];
        if (force)
        {
            args.Add("--force");
        }

        return RunCliAsync(args, cancellationToken, PasswordEnvironment(password));
    }

    public Task<DewCommandResult> RestoreDewDriveFolderAsync(string folder, string commit = "HEAD", CancellationToken cancellationToken = default)
    {
        return RunCliAsync(["dew-drive", "restore", "--folder", folder, "--commit", commit], cancellationToken);
    }

    public Task<DewCommandResult> SaveVeraCryptSettingsAsync(VeraCryptSettings settings, CancellationToken cancellationToken = default)
    {
        List<string> args =
        [
            "veracrypt-settings",
            "--encryption",
            settings.Encryption,
            "--hash",
            settings.Hash,
            "--filesystem",
            settings.Filesystem,
            "--pim",
            settings.Pim,
            "--size-padding-mb",
            settings.SizePaddingMb.ToString(),
            "--size-multiplier",
            settings.SizeMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--minimum-size-mb",
            settings.MinimumSizeMb.ToString(),
            "--keep-source-after-encrypt",
            settings.KeepSourceAfterEncrypt ? "true" : "false",
            "--keep-container-after-decrypt",
            settings.KeepContainerAfterDecrypt ? "true" : "false",
        ];
        if (!string.IsNullOrWhiteSpace(settings.VeraCryptPath))
        {
            args.Add("--veracrypt-path");
            args.Add(settings.VeraCryptPath);
        }

        return RunCliAsync(args, cancellationToken);
    }

    public Task<DewCommandResult> InstallVeraCryptAsync(CancellationToken cancellationToken = default)
    {
        return RunProcessAsync(
            "winget",
            ["install", "-e", "--id", "IDRIX.VeraCrypt", "--silent", "--accept-source-agreements", "--accept-package-agreements"],
            cancellationToken);
    }

    public Task<DewCommandResult> RunCliAsync(IEnumerable<string> arguments, CancellationToken cancellationToken = default, IReadOnlyDictionary<string, string>? environment = null)
    {
        return RunToolWithFallbackAsync(CliFileName, ["-m", "dew_encryption.core"], arguments, cancellationToken, environment);
    }

    private static async Task<DewCommandResult> RunToolWithFallbackAsync(string fileName, IReadOnlyList<string> pythonModulePrefix, IEnumerable<string> arguments, CancellationToken cancellationToken, IReadOnlyDictionary<string, string>? environment = null)
    {
        string[] argumentArray = arguments.ToArray();
        try
        {
            return await RunProcessAsync(fileName, argumentArray, cancellationToken, environment: environment).ConfigureAwait(false);
        }
        catch (Win32Exception ex) when (IsFileNotFound(ex))
        {
            string python = Environment.GetEnvironmentVariable("DEW_ENCRYPTION_PYTHON") ?? "python";
            return await RunProcessAsync(python, pythonModulePrefix.Concat(argumentArray), cancellationToken, ResolvePythonWorkingDirectory(), environment).ConfigureAwait(false);
        }
    }

    private static IReadOnlyDictionary<string, string>? PasswordEnvironment(string? password)
    {
        return string.IsNullOrWhiteSpace(password) ? null : new Dictionary<string, string> { ["DEW_DRIVE_PASSWORD"] = password };
    }

    private static bool IsFileNotFound(Win32Exception exception)
    {
        return exception.NativeErrorCode is 2 or 3;
    }

    private static string? ResolvePythonWorkingDirectory()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "pyproject.toml")) &&
                Directory.Exists(Path.Combine(directory.FullName, "dew_encryption")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    public static async Task<DewCommandResult> RunProcessAsync(string fileName, IEnumerable<string> arguments, CancellationToken cancellationToken = default, string? workingDirectory = null, IReadOnlyDictionary<string, string>? environment = null)
    {
        string[] argumentArray = arguments.ToArray();
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }
        foreach (KeyValuePair<string, string> item in environment ?? new Dictionary<string, string>())
        {
            startInfo.Environment[item.Key] = item.Value;
        }

        foreach (string argument in argumentArray)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException($"Unable to start {fileName}.");
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return new DewCommandResult(fileName, argumentArray, process.ExitCode, await outputTask.ConfigureAwait(false), await errorTask.ConfigureAwait(false));
    }
}
